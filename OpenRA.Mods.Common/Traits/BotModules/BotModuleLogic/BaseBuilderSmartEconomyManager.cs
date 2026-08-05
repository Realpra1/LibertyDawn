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
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	class BaseBuilderSmartEconomyManager
	{
		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		readonly PlayerResources playerResources;
		readonly PlayerStatistics playerStatistics;
		readonly IBotRequestUnitProduction[] unitProduction;
		readonly bool enabled;

		int nextScanTick;
		int nextMcvRequestTick;
		int nextProgressLogTick;
		int waitingHarvesters;
		int spendableCash;
		bool refineryBuildOutstanding;
		int refineryBuildExpiryTick;
		int refineryBuildTargetCount;
		bool mcvRequestOutstanding;
		int mcvRequestExpiryTick;
		int mcvRequestTargetAssets;
		SmartEconomyPressure refineryPressure;
		SmartEconomyPressure cashPressure;

		public bool WantsRefinery => enabled && refineryPressure.Active && waitingHarvesters > 0 && !refineryBuildOutstanding;
		public bool WantsProductionCapacity => enabled && cashPressure.Active;
		public bool WantsSilo => enabled && SmartEconomyPolicy.StoragePressure(playerResources.Resources,
			playerResources.ResourceCapacity, baseBuilder.Info.SmartEconomyStorageThresholdPercent);
		public bool ShouldReserveCashForRefinery { get; private set; }

		public int NextScanTick => nextScanTick;
		public int NextMcvRequestTick => nextMcvRequestTick;
		public int NextProgressLogTick => nextProgressLogTick;
		public bool RefineryBuildOutstanding => refineryBuildOutstanding;
		public int RefineryBuildExpiryTick => refineryBuildExpiryTick;
		public int RefineryBuildTargetCount => refineryBuildTargetCount;
		public bool McvRequestOutstanding => mcvRequestOutstanding;
		public int McvRequestExpiryTick => mcvRequestExpiryTick;
		public int McvRequestTargetAssets => mcvRequestTargetAssets;
		public SmartEconomyPressure RefineryPressure => refineryPressure;
		public SmartEconomyPressure CashPressure => cashPressure;

		public BaseBuilderSmartEconomyManager(BaseBuilderBotModule baseBuilder, Player player,
			PlayerResources playerResources, IBotRequestUnitProduction[] unitProduction)
		{
			this.baseBuilder = baseBuilder;
			world = player.World;
			this.player = player;
			this.playerResources = playerResources;
			playerStatistics = player.PlayerActor.Trait<PlayerStatistics>();
			this.unitProduction = unitProduction;
			enabled = !baseBuilder.Info.SmartEconomyExcludedBotTypes.Contains(player.BotType);
		}

		public void Tick(IBot bot)
		{
			if (world.WorldTick < nextScanTick)
				return;

			var elapsed = Math.Max(1, baseBuilder.Info.SmartEconomyScanInterval);
			nextScanTick = world.WorldTick + elapsed;
			var previousRefineryPressure = refineryPressure.Active;
			var previousCashPressure = cashPressure.Active;

			waitingHarvesters = CountWaitingHarvestersNearRefineries();
			var refineryPressureObserved = refineryPressure.Active ? waitingHarvesters > 0 :
				waitingHarvesters >= Math.Max(1, baseBuilder.Info.SmartEconomyWaitingHarvesterThreshold);
			refineryPressure = SmartEconomyPolicy.UpdatePressure(refineryPressure, refineryPressureObserved,
				elapsed, baseBuilder.Info.SmartEconomyRefineryPressureDuration,
				baseBuilder.Info.SmartEconomyRefineryPressureRelease);
			ShouldReserveCashForRefinery = enabled && refineryPressure.Active && waitingHarvesters > 0 &&
				baseBuilder.CanBuildAnotherSmartEconomyRefinery();

			spendableCash = Math.Max(0, playerResources.Cash + playerResources.Resources);
			var cashThreshold = cashPressure.Active ? baseBuilder.Info.SmartEconomyExcessCashReleaseThreshold :
				baseBuilder.Info.SmartEconomyExcessCashThreshold;
			var cashPressureObserved = !baseBuilder.OpeningActive && baseBuilder.Info.SmartEconomyExcessCashThreshold > 0 &&
				spendableCash >= Math.Max(0, cashThreshold);
			cashPressure = SmartEconomyPolicy.UpdatePressure(cashPressure, cashPressureObserved, elapsed,
				baseBuilder.Info.SmartEconomyExcessCashPressureDuration,
				baseBuilder.Info.SmartEconomyExcessCashPressureRelease);

			if (previousRefineryPressure != refineryPressure.Active || previousCashPressure != cashPressure.Active)
				baseBuilder.LogSmartEconomy(
					"{0} pressure transition: refinery={1} waiters={2} evidence={3}; cash={4} funds={5} evidence={6}",
					player, refineryPressure.Active, waitingHarvesters, refineryPressure.EvidenceTicks,
					cashPressure.Active, spendableCash, cashPressure.EvidenceTicks);

			UpdateRefineryBuildState();
			UpdateExpansionRequestState(bot);
			LogProgress(bot);

			TryRequestExpansionMcv(bot);
		}

		void LogProgress(IBot bot)
		{
			if (!baseBuilder.Info.SmartEconomyDebugLogging || world.WorldTick < nextProgressLogTick)
				return;

			nextProgressLogTick = world.WorldTick + Math.Max(1, baseBuilder.Info.SmartEconomyProgressLogInterval);
			var constructionYards = baseBuilder.CountActors(baseBuilder.Info.ConstructionYardTypes);
			var liveMcvs = baseBuilder.CountActors(baseBuilder.Info.SmartEconomyMcvTypes);
			var requestedMcvs = RequestedExpansionMcvs(bot);
			var queuedMcvs = QueuedExpansionMcvs();
			var desiredExpansionAssets = cashPressure.Active ? SmartEconomyPolicy.DesiredExpansionAssets(spendableCash,
				baseBuilder.Info.SmartEconomyExpansionCashPerAsset, baseBuilder.Info.SmartEconomyMaximumExpansionAssets) : 0;
			var expansionArmyReady = SmartEconomyPolicy.ExpansionArmyReady(playerStatistics.ArmyValue,
				playerStatistics.AssetsValue, baseBuilder.Info.SmartEconomyExpansionMinimumArmyPercent);
			var refineries = baseBuilder.CountActors(baseBuilder.SmartEconomyRefineryTypes);
			var silos = baseBuilder.CountActors(baseBuilder.Info.SiloTypes);
			var productionBuildings = baseBuilder.CountActors(baseBuilder.Info.ProductionTypes);
			baseBuilder.LogSmartEconomy(
				"{0} status: enabled={1}, waiters={2}, refinery-pressure={3}/{4}, stored={5}/{6}, funds={7}, earned={8}, spent={9}, income={10}, army={11}, assets={12}, refineries={13}, refinery-outstanding={14}/{15}, silos={16}, production={17}, cash-pressure={18}/{19}, expansion={20} yards+{21} mcvs+{22} requested+{23} queued/{24} desired, expansion-ready={25}, mcv-outstanding={26}/{27}",
				player, enabled, waitingHarvesters, refineryPressure.EvidenceTicks, refineryPressure.Active,
				playerResources.Resources, playerResources.ResourceCapacity, spendableCash, playerResources.Earned,
				playerResources.Spent, playerStatistics.Income, playerStatistics.ArmyValue, playerStatistics.AssetsValue,
				refineries, refineryBuildOutstanding, refineryBuildTargetCount, silos, productionBuildings,
				cashPressure.EvidenceTicks, cashPressure.Active,
				constructionYards, liveMcvs, requestedMcvs, queuedMcvs, desiredExpansionAssets, expansionArmyReady,
				mcvRequestOutstanding, mcvRequestTargetAssets);
		}

		public bool TryReserveRefineryBuild(string type)
		{
			if (!WantsRefinery || !baseBuilder.SmartEconomyRefineryTypes.Contains(type) || QueuedRefineries() > 0)
				return false;

			refineryBuildOutstanding = true;
			refineryBuildTargetCount = baseBuilder.CountActors(baseBuilder.SmartEconomyRefineryTypes) + 1;
			refineryBuildExpiryTick = world.WorldTick + Math.Max(1, baseBuilder.Info.SmartEconomyRefineryBuildTimeout);
			baseBuilder.LogSmartEconomy("{0} reserved congestion-relief refinery: type={1}, target-count={2}",
				player, type, refineryBuildTargetCount);
			return true;
		}

		void UpdateRefineryBuildState()
		{
			if (!refineryBuildOutstanding)
				return;

			var live = baseBuilder.CountActors(baseBuilder.SmartEconomyRefineryTypes);
			if (live >= refineryBuildTargetCount)
			{
				refineryBuildOutstanding = false;
				baseBuilder.LogSmartEconomy("{0} congestion-relief refinery completed: refineries={1}/{2}",
					player, live, refineryBuildTargetCount);
				return;
			}

			if (QueuedRefineries() > 0 || world.WorldTick < refineryBuildExpiryTick)
				return;

			refineryBuildOutstanding = false;
			baseBuilder.LogSmartEconomy("{0} congestion-relief refinery reservation expired: refineries={1}/{2}",
				player, live, refineryBuildTargetCount);
		}

		void UpdateExpansionRequestState(IBot bot)
		{
			if (!mcvRequestOutstanding)
				return;

			var live = CurrentExpansionAssets();
			if (live >= mcvRequestTargetAssets)
			{
				mcvRequestOutstanding = false;
				baseBuilder.LogSmartEconomy("{0} expansion MCV request fulfilled: assets={1}/{2}",
					player, live, mcvRequestTargetAssets);
				return;
			}

			if (world.WorldTick < mcvRequestExpiryTick || QueuedExpansionMcvs() > 0)
				return;

			CancelExpansionRequests(bot);
			mcvRequestOutstanding = false;
			baseBuilder.LogSmartEconomy("{0} expansion MCV request expired: assets={1}/{2}; allowing retry",
				player, live, mcvRequestTargetAssets);
		}

		void TryRequestExpansionMcv(IBot bot)
		{
			if (!enabled || !cashPressure.Active || mcvRequestOutstanding || world.WorldTick < nextMcvRequestTick ||
				baseBuilder.Info.SmartEconomyMcvTypes.Length == 0)
				return;

			var desired = SmartEconomyPolicy.DesiredExpansionAssets(spendableCash,
				baseBuilder.Info.SmartEconomyExpansionCashPerAsset,
				baseBuilder.Info.SmartEconomyMaximumExpansionAssets);
			var live = CurrentExpansionAssets();
			var requested = RequestedExpansionMcvs(bot);
			var queued = QueuedExpansionMcvs();
			if (live + requested + queued >= desired)
				return;

			if (!SmartEconomyPolicy.ExpansionArmyReady(playerStatistics.ArmyValue, playerStatistics.AssetsValue,
				baseBuilder.Info.SmartEconomyExpansionMinimumArmyPercent))
				return;

			if (!baseBuilder.RequestFirstAvailable(bot, baseBuilder.Info.SmartEconomyMcvTypes,
				"sustained excess cash expansion", false))
				return;

			mcvRequestOutstanding = true;
			mcvRequestTargetAssets = live + 1;
			mcvRequestExpiryTick = world.WorldTick + Math.Max(1, baseBuilder.Info.SmartEconomyMcvRequestTimeout);
			nextMcvRequestTick = world.WorldTick + Math.Max(1, baseBuilder.Info.SmartEconomyMcvRequestCooldown);
			baseBuilder.LogSmartEconomy("{0} requested expansion MCV: funds={1}, assets={2}, requested={3}, queued={4}, desired={5}",
				player, spendableCash, live, requested, queued, desired);
		}

		int CurrentExpansionAssets()
		{
			return baseBuilder.CountActors(baseBuilder.Info.ConstructionYardTypes) +
				baseBuilder.CountActors(baseBuilder.Info.SmartEconomyMcvTypes);
		}

		int RequestedExpansionMcvs(IBot bot)
		{
			return baseBuilder.Info.SmartEconomyMcvTypes.Sum(type => unitProduction
				.Where(r => r.IsTraitEnabled()).Select(r => r.RequestedProductionCount(bot, type)).DefaultIfEmpty(0).Max());
		}

		void CancelExpansionRequests(IBot bot)
		{
			foreach (var type in baseBuilder.Info.SmartEconomyMcvTypes)
				foreach (var requester in unitProduction.Where(r => r.IsTraitEnabled() &&
					r.RequestedProductionCount(bot, type) > 0))
					requester.CancelRequestedUnitProduction(bot, type);
		}

		int QueuedExpansionMcvs()
		{
			return world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Actor.IsInWorld)
				.Sum(q => q.Trait.AllQueued().Count(item => baseBuilder.Info.SmartEconomyMcvTypes.Contains(item.Item)));
		}

		int QueuedRefineries()
		{
			return world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Actor.IsInWorld)
				.Sum(q => q.Trait.AllQueued().Count(item => baseBuilder.SmartEconomyRefineryTypes.Contains(item.Item)));
		}

		int CountWaitingHarvestersNearRefineries()
		{
			var refineries = world.ActorsWithTrait<IAcceptResources>()
				.Where(r => r.Actor.Owner == player && !r.Actor.IsDead && r.Actor.IsInWorld &&
					baseBuilder.SmartEconomyRefineryTypes.Contains(r.Actor.Info.Name))
				.OrderBy(r => r.Actor.ActorID).ToDictionary(r => r.Actor,
					r => r.Actor.Location + r.Trait.DeliveryOffset);
			var radiusSquared = Math.Max(0, baseBuilder.Info.SmartEconomyRefineryQueueRadius);
			radiusSquared *= radiusSquared;
			var nearbyLinked = refineries.Keys.ToDictionary(r => r, r => 0);
			foreach (var harvester in world.ActorsWithTrait<Harvester>())
			{
				if (harvester.Actor.Owner != player || harvester.Actor.IsDead || !harvester.Actor.IsInWorld ||
					harvester.Trait.IsEmpty || harvester.Trait.LinkedProc == null ||
					!refineries.TryGetValue(harvester.Trait.LinkedProc, out var deliveryCell) ||
					(harvester.Actor.Location - deliveryCell).LengthSquared > radiusSquared)
					continue;

				nearbyLinked[harvester.Trait.LinkedProc]++;
			}

			// A short queue at the nearest refinery is ordinary harvester behavior while
			// another unloading refinery is idle. Only treat overflow as congestion once
			// every usable refinery has an active delivery.
			return SmartEconomyPolicy.WaitingHarvestersWhenAllRefineriesOccupied(nearbyLinked.Values);
		}

		public void LoadState(int savedNextScanTick, int savedNextMcvRequestTick, int savedNextProgressLogTick,
			bool savedRefineryBuildOutstanding, int savedRefineryBuildExpiryTick, int savedRefineryBuildTargetCount,
			bool savedMcvRequestOutstanding, int savedMcvRequestExpiryTick, int savedMcvRequestTargetAssets,
			SmartEconomyPressure savedRefineryPressure, SmartEconomyPressure savedCashPressure)
		{
			nextScanTick = savedNextScanTick;
			nextMcvRequestTick = savedNextMcvRequestTick;
			nextProgressLogTick = savedNextProgressLogTick;
			refineryBuildOutstanding = savedRefineryBuildOutstanding;
			refineryBuildExpiryTick = savedRefineryBuildExpiryTick;
			refineryBuildTargetCount = savedRefineryBuildTargetCount;
			mcvRequestOutstanding = savedMcvRequestOutstanding;
			mcvRequestExpiryTick = savedMcvRequestExpiryTick;
			mcvRequestTargetAssets = savedMcvRequestTargetAssets;
			refineryPressure = savedRefineryPressure;
			cashPressure = savedCashPressure;
			ShouldReserveCashForRefinery = enabled && refineryPressure.Active && waitingHarvesters > 0 &&
				baseBuilder.CanBuildAnotherSmartEconomyRefinery();
		}
	}
}
