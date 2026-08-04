#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.BotModules.BotModuleLogic
{
	public sealed class AlliedSupplyAidManager
	{
		sealed class AllyRecoveryCounts
		{
			public int SpendableCash;
			public int Harvesters;
			public int Refineries;
			public int Mcvs;
			public int ProductionBuildings;
			public int MobileUnits;

			public AlliedRecoverySnapshot Snapshot => new AlliedRecoverySnapshot(SpendableCash, Harvesters,
				Refineries, Mcvs, ProductionBuildings, MobileUnits);
		}

		readonly World world;
		readonly Player player;
		readonly SpecialOrderBotModuleInfo info;
		readonly Dictionary<int, AlliedRecoverySnapshot> recovery = new Dictionary<int, AlliedRecoverySnapshot>();
		readonly List<int> recentDispatchTicks = new List<int>();
		readonly List<int> pendingRequestTicks = new List<int>();
		UnitBuilderBotModule[] production;

		public AlliedSupplyAidManager(World world, Player player, SpecialOrderBotModuleInfo info)
		{
			this.world = world;
			this.player = player;
			this.info = info;
		}

		public void Initialize()
		{
			production = player.PlayerActor.TraitsImplementing<UnitBuilderBotModule>().ToArray();
		}

		public void Refresh(IBot bot, IReadOnlyCollection<uint> assignedSupplyIds)
		{
			RefreshRecoverySnapshots();
			ExpireRateLimits(bot, assignedSupplyIds);
		}

		public bool ShouldAid(Player ally)
		{
			return IsPotentialRecipient(ally) && recovery.TryGetValue(ally.ClientIndex, out var snapshot) &&
				AlliedRecoveryPolicy.ShouldAid(snapshot, info.AlliedAidMaximumCash);
		}

		public Player PlayerByClientIndex(int clientIndex)
		{
			return world.Players.FirstOrDefault(p => p.ClientIndex == clientIndex);
		}

		public Player FindRecipient(Actor truck, Player preferred)
		{
			var delivery = truck.Info.TraitInfoOrDefault<DeliversCashInfo>();
			if (delivery == null)
				return null;

			if (ShouldAid(preferred) && HasEligibleCashTarget(preferred, delivery))
				return preferred;

			return FindRecipient(delivery);
		}

		public bool CanAssignNewTruck()
		{
			return pendingRequestTicks.Count > 0 || AlliedRecoveryPolicy.AvailableDispatches(
				AvailableFactories(), recentDispatchTicks.Count, 0) > 0;
		}

		public void RecordDispatch()
		{
			if (pendingRequestTicks.Count > 0)
				pendingRequestTicks.RemoveAt(0);

			recentDispatchTicks.Add(world.WorldTick);
		}

		public void RequestTruck(IBot bot)
		{
			if (string.IsNullOrEmpty(info.AlliedAidSupplyActor) || production == null ||
				AlliedRecoveryPolicy.AvailableDispatches(AvailableFactories(), recentDispatchTicks.Count,
					pendingRequestTicks.Count) <= 0)
				return;

			if (!world.Map.Rules.Actors.TryGetValue(info.AlliedAidSupplyActor, out var supplyInfo))
				return;

			var delivery = supplyInfo.TraitInfoOrDefault<DeliversCashInfo>();
			var buildable = supplyInfo.TraitInfoOrDefault<BuildableInfo>();
			var recipient = delivery != null ? FindRecipient(delivery) : null;
			var builder = production.FirstOrDefault(p => !p.IsTraitDisabled);
			if (recipient == null || buildable == null || builder == null || !CanQueueSupply(buildable, supplyInfo.Name))
				return;

			var valued = supplyInfo.TraitInfoOrDefault<ValuedInfo>();
			var resources = player.PlayerActor.TraitOrDefault<PlayerResources>();
			if (resources == null || resources.Cash + resources.Resources < (valued?.Cost ?? 0))
				return;

			var requester = (IBotRequestUnitProduction)builder;
			if (requester.RequestedProductionCount(bot, supplyInfo.Name) > 0)
				return;

			requester.RequestUnitProduction(bot, supplyInfo.Name);
			pendingRequestTicks.Add(world.WorldTick);
			Debug("requested allied-aid {0} for player {1}: factories={2}, recent={3}, pending={4}",
				supplyInfo.Name, recipient.ClientIndex, AvailableFactories(), recentDispatchTicks.Count,
				pendingRequestTicks.Count);
		}

		Player FindRecipient(DeliversCashInfo delivery)
		{
			return world.Players.Where(ShouldAid)
				.Where(p => HasEligibleCashTarget(p, delivery))
				.OrderBy(p => recovery[p.ClientIndex].SpendableCash)
				.ThenBy(p => p.ClientIndex).FirstOrDefault();
		}

		bool HasEligibleCashTarget(Player targetOwner, DeliversCashInfo delivery)
		{
			return world.Actors.Where(a => a.Owner == targetOwner && !a.IsDead && a.IsInWorld).Any(a =>
			{
				var accepts = a.Info.TraitInfoOrDefault<AcceptsDeliveredCashInfo>();
				return accepts != null && SpecialOrderTargeting.AcceptsDelivery(
					a.Owner.RelationshipWith(player), delivery.Type, accepts.ValidTypes, accepts.ValidRelationships);
			});
		}

		void RefreshRecoverySnapshots()
		{
			recovery.Clear();
			var counts = world.Players.Where(IsPotentialRecipient).ToDictionary(p => p, p =>
			{
				var resources = p.PlayerActor.TraitOrDefault<PlayerResources>();
				return new AllyRecoveryCounts
				{
					SpendableCash = resources != null ? resources.Cash + resources.Resources : int.MaxValue,
				};
			});

			foreach (var actor in world.Actors)
			{
				if (actor.IsDead || !actor.IsInWorld || !counts.TryGetValue(actor.Owner, out var ally))
					continue;

				if (info.AlliedAidHarvesterTypes.Contains(actor.Info.Name))
					ally.Harvesters++;
				if (info.AlliedAidRefineryTypes.Contains(actor.Info.Name))
					ally.Refineries++;
				if (info.AlliedAidMcvTypes.Contains(actor.Info.Name))
					ally.Mcvs++;
				if (actor.Info.HasTraitInfo<ProductionInfo>())
					ally.ProductionBuildings++;
				if (actor.Info.HasTraitInfo<IMoveInfo>())
					ally.MobileUnits++;
			}

			foreach (var pair in counts)
				recovery.Add(pair.Key.ClientIndex, pair.Value.Snapshot);
		}

		bool IsPotentialRecipient(Player ally)
		{
			return ally != null && ally != player && ally.Playable && !ally.NonCombatant &&
				ally.WinState == WinState.Undefined && ally.IsAlliedWith(player);
		}

		void ExpireRateLimits(IBot bot, IReadOnlyCollection<uint> assignedSupplyIds)
		{
			recentDispatchTicks.RemoveAll(t => world.WorldTick - t >= info.AlliedAidInterval);
			var protectedRequests = Math.Min(CommittedUnassignedSupplies(bot, assignedSupplyIds), pendingRequestTicks.Count);
			for (var i = pendingRequestTicks.Count - 1; i >= protectedRequests; i--)
				if (world.WorldTick - pendingRequestTicks[i] >= info.AlliedAidRequestTimeout)
					pendingRequestTicks.RemoveAt(i);
		}

		int CommittedUnassignedSupplies(IBot bot, IReadOnlyCollection<uint> assignedSupplyIds)
		{
			if (string.IsNullOrEmpty(info.AlliedAidSupplyActor))
				return 0;

			var committed = world.Actors.Count(a => a.Owner == player && !a.IsDead && a.IsInWorld &&
				a.Info.Name == info.AlliedAidSupplyActor && !assignedSupplyIds.Contains(a.ActorID));
			foreach (var queue in world.ActorsWithTrait<ProductionQueue>().Where(q => q.Actor.Owner == player))
				committed += queue.Trait.AllQueued().Count(i => i.Item == info.AlliedAidSupplyActor);

			var builder = production?.FirstOrDefault(p => !p.IsTraitDisabled);
			if (builder != null)
				committed += ((IBotRequestUnitProduction)builder).RequestedProductionCount(bot, info.AlliedAidSupplyActor);

			return committed;
		}

		int AvailableFactories()
		{
			return world.Actors.Count(a => a.Owner == player && !a.IsDead && a.IsInWorld &&
				info.AlliedAidFactoryTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<ProductionInfo>());
		}

		bool CanQueueSupply(BuildableInfo buildable, string actorType)
		{
			return buildable.Queue.Any(queueType => AIUtils.FindQueues(player, queueType)
				.Any(q => !q.AllQueued().Any() && q.BuildableItems().Any(a => a.Name == actorType)));
		}

		void Debug(string format, params object[] args)
		{
			if (info.DebugLogging)
				Log.Write("debug", "AI ({0}) special orders: {1}", player.ClientIndex, string.Format(format, args));
		}
	}
}
