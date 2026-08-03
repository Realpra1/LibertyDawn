#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits.BotModules.BotModuleLogic;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Uses existing player orders for specialist AI units.")]
	public class SpecialOrderBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between special-order scans. Zero disables the module.")]
		public readonly int ScanInterval = 125;

		[Desc("Actor types that deliver cash to compatible buildings owned by the bot.")]
		public readonly HashSet<string> SupplyActorTypes = new HashSet<string>();

		[ActorReference]
		[Desc("Supply actor requested when a crippled ally needs financial aid.")]
		public readonly string AlliedAidSupplyActor = null;

		[ActorReference]
		[Desc("Owned production buildings that each permit one allied-aid dispatch per interval.")]
		public readonly HashSet<string> AlliedAidFactoryTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> AlliedAidHarvesterTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> AlliedAidRefineryTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> AlliedAidMcvTypes = new HashSet<string>();

		[Desc("Maximum spendable cash at which an ally can request recovery aid.")]
		public readonly int AlliedAidMaximumCash = 0;

		[Desc("Rolling ticks between allied-aid dispatches allowed by each available factory.")]
		public readonly int AlliedAidInterval = 7500;

		[Desc("Ticks before an unfulfilled allied-aid production request is released for retry.")]
		public readonly int AlliedAidRequestTimeout = 750;

		[Desc("Ticks without getting closer to the destination before a supply order is cancelled and retargeted.")]
		public readonly int SupplyStallRetryInterval = 250;

		[Desc("Write special-order assignments to debug.log.")]
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (SupplyStallRetryInterval <= 0 || AlliedAidInterval <= 0 || AlliedAidRequestTimeout <= 0 ||
				AlliedAidMaximumCash < 0)
				throw new YamlException("Supply and allied-aid intervals must be positive and maximum cash cannot be negative.");

			if (!string.IsNullOrEmpty(AlliedAidSupplyActor) && !SupplyActorTypes.Contains(AlliedAidSupplyActor))
				throw new YamlException("AlliedAidSupplyActor must also be listed in SupplyActorTypes.");
		}

		public override object Create(ActorInitializer init) { return new SpecialOrderBotModule(init.Self, this); }
	}

	public class SpecialOrderBotModule : ConditionalTrait<SpecialOrderBotModuleInfo>, IBotTick
	{
		sealed class SupplyAssignment
		{
			public uint TargetId;
			public long BestDistanceSquared;
			public int LastProgressTick;
			public int AidRecipientIndex = -1;
		}

		readonly World world;
		readonly Player player;
		readonly Dictionary<uint, SupplyAssignment> supplyAssignments = new Dictionary<uint, SupplyAssignment>();
		readonly AlliedSupplyAidManager alliedAid;
		int scanTicks;

		public SpecialOrderBotModule(Actor self, SpecialOrderBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
			alliedAid = new AlliedSupplyAidManager(world, player, info);
		}

		protected override void Created(Actor self)
		{
			alliedAid.Initialize();
		}

		protected override void TraitEnabled(Actor self)
		{
			scanTicks = world.LocalRandom.Next(0, Math.Max(1, Info.ScanInterval));
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (Info.ScanInterval <= 0 || player.WinState != WinState.Undefined || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			alliedAid.Refresh(bot, supplyAssignments.Keys);
			ReviewAssignments(bot);
			AssignSupplyOrders(bot);
			alliedAid.RequestTruck(bot);
		}

		void ReviewAssignments(IBot bot)
		{
			foreach (var truckId in supplyAssignments.Keys.OrderBy(id => id).ToList())
			{
				var truck = world.GetActorById(truckId);
				if (!IsUsableOwnedTruck(truck))
				{
					supplyAssignments.Remove(truckId);
					continue;
				}

				var assignment = supplyAssignments[truckId];
				var target = world.GetActorById(assignment.TargetId);
				var aidNoLongerNeeded = assignment.AidRecipientIndex >= 0 &&
					!alliedAid.ShouldAid(alliedAid.PlayerByClientIndex(assignment.AidRecipientIndex));
				var targetEligible = IsEligibleCashTarget(truck, target, assignment.AidRecipientIndex);
				if (targetEligible)
				{
					var distance = (truck.CenterPosition - target.CenterPosition).LengthSquared;
					if (SpecialOrderTargeting.MadeDeliveryProgress(distance, assignment.BestDistanceSquared))
					{
						assignment.BestDistanceSquared = distance;
						assignment.LastProgressTick = world.WorldTick;
					}
				}

				if (!SpecialOrderTargeting.ShouldRetargetDelivery(truck.IsIdle, targetEligible,
					world.WorldTick, assignment.LastProgressTick, Info.SupplyStallRetryInterval))
					continue;

				var reason = aidNoLongerNeeded ? "ally recovered or unavailable" : !targetEligible ?
					"target unavailable" : truck.IsIdle ?
					"order ended without delivery" : "no forward progress";
				IssueSupplyOrder(bot, truck, assignment.TargetId, assignment.AidRecipientIndex, false, reason);
			}
		}

		void AssignSupplyOrders(IBot bot)
		{
			foreach (var truck in world.Actors.Where(a => a.Owner == player && a.IsIdle &&
				Info.SupplyActorTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<DeliversCashInfo>() &&
				!supplyAssignments.ContainsKey(a.ActorID)).OrderBy(a => a.ActorID))
			{
				IssueSupplyOrder(bot, truck, 0, -1, true, "new assignment");
			}
		}

		void IssueSupplyOrder(IBot bot, Actor truck, uint previousTargetId, int previousAidRecipientIndex,
			bool allowNewAidDispatch, string reason)
		{
			Player aidRecipient = null;
			if (previousAidRecipientIndex >= 0)
				aidRecipient = alliedAid.FindRecipient(truck, alliedAid.PlayerByClientIndex(previousAidRecipientIndex));
			else if (allowNewAidDispatch && alliedAid.CanAssignNewTruck())
				aidRecipient = alliedAid.FindRecipient(truck, null);

			var targetOwner = aidRecipient ?? player;
			var candidates = EligibleCashTargets(truck, targetOwner).OrderBy(a =>
				(a.CenterPosition - truck.CenterPosition).LengthSquared).ThenBy(a => a.ActorID).ToList();
			var target = candidates.FirstOrDefault(a => a.ActorID != previousTargetId) ?? candidates.FirstOrDefault();
			if (target == null)
			{
				bot.QueueOrder(new Order("Stop", truck, false));
				supplyAssignments.Remove(truck.ActorID);
				Debug("supply {0}#{1} cancelled ({2}): no eligible building", truck.Info.Name, truck.ActorID, reason);
				return;
			}

			if (aidRecipient != null && previousAidRecipientIndex < 0)
				alliedAid.RecordDispatch();

			bot.QueueOrder(new Order("DeliverCash", truck, Target.FromActor(target), false));
			supplyAssignments[truck.ActorID] = new SupplyAssignment
			{
				TargetId = target.ActorID,
				BestDistanceSquared = (truck.CenterPosition - target.CenterPosition).LengthSquared,
				LastProgressTick = world.WorldTick,
				AidRecipientIndex = aidRecipient?.ClientIndex ?? -1,
			};
			Debug("supply {0}#{1} -> {2}#{3} owner={4} ({5})", truck.Info.Name, truck.ActorID,
				target.Info.Name, target.ActorID, targetOwner.ClientIndex,
				aidRecipient != null ? $"allied aid; {reason}" : reason);
		}

		IEnumerable<Actor> EligibleCashTargets(Actor truck, Player targetOwner)
		{
			var deliveryType = truck.Info.TraitInfo<DeliversCashInfo>().Type;
			return world.Actors.Where(a => a.Owner == targetOwner && !a.IsDead && a.IsInWorld)
				.Where(a =>
				{
					var accepts = a.Info.TraitInfoOrDefault<AcceptsDeliveredCashInfo>();
					return accepts != null && SpecialOrderTargeting.AcceptsDelivery(a.Owner.RelationshipWith(player),
						deliveryType, accepts.ValidTypes, accepts.ValidRelationships);
				});
		}

		bool IsEligibleCashTarget(Actor truck, Actor target, int aidRecipientIndex)
		{
			if (target == null || target.IsDead || !target.IsInWorld)
				return false;

			if (aidRecipientIndex < 0 && target.Owner != player)
				return false;

			if (aidRecipientIndex >= 0 &&
				(target.Owner.ClientIndex != aidRecipientIndex || !alliedAid.ShouldAid(target.Owner)))
				return false;

			var accepts = target.Info.TraitInfoOrDefault<AcceptsDeliveredCashInfo>();
			var delivers = truck.Info.TraitInfoOrDefault<DeliversCashInfo>();
			return accepts != null && delivers != null && SpecialOrderTargeting.AcceptsDelivery(
				target.Owner.RelationshipWith(player), delivers.Type, accepts.ValidTypes, accepts.ValidRelationships);
		}

		bool IsUsableOwnedTruck(Actor truck)
		{
			return truck != null && truck.Owner == player && !truck.IsDead && truck.IsInWorld &&
				Info.SupplyActorTypes.Contains(truck.Info.Name);
		}

		void Debug(string format, params object[] args)
		{
			if (Info.DebugLogging)
				Log.Write("debug", "AI ({0}) special orders: {1}", player.ClientIndex, string.Format(format, args));
		}
	}
}
