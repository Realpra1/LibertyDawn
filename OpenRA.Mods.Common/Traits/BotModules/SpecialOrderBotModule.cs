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

		[Desc("Ticks without getting closer to the destination before a supply order is cancelled and retargeted.")]
		public readonly int SupplyStallRetryInterval = 250;

		[Desc("Write special-order assignments to debug.log.")]
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (SupplyStallRetryInterval <= 0)
				throw new YamlException("SupplyStallRetryInterval must be greater than zero.");
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
		}

		readonly World world;
		readonly Player player;
		readonly Dictionary<uint, SupplyAssignment> supplyAssignments = new Dictionary<uint, SupplyAssignment>();
		int scanTicks;

		public SpecialOrderBotModule(Actor self, SpecialOrderBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
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
			ReviewAssignments(bot);
			AssignSupplyOrders(bot);
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
				var targetEligible = IsEligibleCashTarget(truck, target);
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

				var reason = !targetEligible ? "target unavailable" : truck.IsIdle ?
					"order ended without delivery" : "no forward progress";
				IssueSupplyOrder(bot, truck, assignment.TargetId, reason);
			}
		}

		void AssignSupplyOrders(IBot bot)
		{
			foreach (var truck in world.Actors.Where(a => a.Owner == player && a.IsIdle &&
				Info.SupplyActorTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<DeliversCashInfo>() &&
				!supplyAssignments.ContainsKey(a.ActorID)))
			{
				IssueSupplyOrder(bot, truck, 0, "new assignment");
			}
		}

		void IssueSupplyOrder(IBot bot, Actor truck, uint previousTargetId, string reason)
		{
			var candidates = EligibleCashTargets(truck).OrderBy(a =>
				(a.CenterPosition - truck.CenterPosition).LengthSquared).ThenBy(a => a.ActorID).ToList();
			var target = candidates.FirstOrDefault(a => a.ActorID != previousTargetId) ?? candidates.FirstOrDefault();
			if (target == null)
			{
				bot.QueueOrder(new Order("Stop", truck, false));
				supplyAssignments.Remove(truck.ActorID);
				Debug("supply {0}#{1} cancelled ({2}): no eligible building", truck.Info.Name, truck.ActorID, reason);
				return;
			}

			bot.QueueOrder(new Order("DeliverCash", truck, Target.FromActor(target), false));
			supplyAssignments[truck.ActorID] = new SupplyAssignment
			{
				TargetId = target.ActorID,
				BestDistanceSquared = (truck.CenterPosition - target.CenterPosition).LengthSquared,
				LastProgressTick = world.WorldTick,
			};
			Debug("supply {0}#{1} -> {2}#{3} ({4})", truck.Info.Name, truck.ActorID,
				target.Info.Name, target.ActorID, reason);
		}

		IEnumerable<Actor> EligibleCashTargets(Actor truck)
		{
			var deliveryType = truck.Info.TraitInfo<DeliversCashInfo>().Type;
			return world.Actors.Where(a => a.Owner == player && !a.IsDead && a.IsInWorld)
				.Where(a =>
				{
					var accepts = a.Info.TraitInfoOrDefault<AcceptsDeliveredCashInfo>();
					return accepts != null && SpecialOrderTargeting.AcceptsDelivery(a.Owner.RelationshipWith(player),
						deliveryType, accepts.ValidTypes, accepts.ValidRelationships);
				});
		}

		bool IsEligibleCashTarget(Actor truck, Actor target)
		{
			if (target == null || target.IsDead || !target.IsInWorld || target.Owner != player)
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
