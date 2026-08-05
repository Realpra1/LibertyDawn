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
	class BaseBuilderSmartEconomyManager
	{
		sealed class RefineryReservation
		{
			public readonly uint QueueActorId;
			public readonly string Type;
			public readonly int ExpiryTick;
			public readonly int TargetCount;
			public readonly int Cost;

			public RefineryReservation(uint queueActorId, string type, int expiryTick, int targetCount, int cost)
			{
				QueueActorId = queueActorId;
				Type = type;
				ExpiryTick = expiryTick;
				TargetCount = targetCount;
				Cost = cost;
			}
		}

		sealed class VehicleFactoryReservation
		{
			public readonly uint QueueActorId;
			public readonly string Type;
			public readonly int ExpiryTick;
			public readonly int TargetCount;

			public VehicleFactoryReservation(uint queueActorId, string type, int expiryTick, int targetCount)
			{
				QueueActorId = queueActorId;
				Type = type;
				ExpiryTick = expiryTick;
				TargetCount = targetCount;
			}
		}

		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		readonly PlayerResources playerResources;
		readonly PlayerStatistics playerStatistics;
		readonly IBotRequestUnitProduction[] unitProduction;
		readonly HashSet<string> harvesterTypes;
		readonly Dictionary<uint, RefineryReservation> refineryReservations = new Dictionary<uint, RefineryReservation>();
		readonly Dictionary<uint, VehicleFactoryReservation> vehicleFactoryReservations = new Dictionary<uint, VehicleFactoryReservation>();
		readonly bool enabled;

		int nextScanTick;
		int postLoadResumeTick;
		int nextMcvRequestTick;
		int nextProgressLogTick;
		int nextRefineryBlockedLogTick;
		int waitingHarvesters;
		int spendableCash;
		int liveHarvesters;
		int queuedHarvesters;
		int requestedHarvesters;
		int liveRefineries;
		int queuedRefineries;
		int reservedRefineries;
		int idleRefineryQueues;
		int refineryCashShortfall;
		int effectiveParallelRefineryLimit;
		int combatUnitQueues;
		int totalUnitQueues;
		int activeFactQueues;
		int liveVehicleFactories;
		int queuedVehicleFactories;
		int reservedVehicleFactories;
		int desiredVehicleFactories;
		bool vehicleFactoryViable;
		bool mcvRequestOutstanding;
		int mcvRequestExpiryTick;
		int mcvRequestTargetAssets;
		SmartEconomyPressure refineryPressure;
		SmartEconomyPressure cashPressure;
		SmartEconomyRefineryDemand refineryDemand;

		bool RequestsReady => world.WorldTick >= postLoadResumeTick;

		public bool WantsRefinery => RequestsReady && enabled && liveRefineries > 0 &&
			refineryPressure.Active && refineryDemand.AvailableRequests > 0;
		public bool Enabled => enabled;
		public bool WantsProductionCapacity => RequestsReady && enabled && cashPressure.Active;
		public bool WantsEarlyVehicleProductionCapacity => RequestsReady && enabled && liveRefineries > 0 &&
			vehicleFactoryViable && liveVehicleFactories + queuedVehicleFactories + reservedVehicleFactories < desiredVehicleFactories;
		public bool WantsSilo => RequestsReady && enabled && SmartEconomyPolicy.StoragePressure(playerResources.Resources,
			playerResources.ResourceCapacity, baseBuilder.Info.SmartEconomyStorageThresholdPercent);
		public bool SerializesMissingRefinery => RequestsReady && enabled &&
			baseBuilder.CountActors(baseBuilder.SmartEconomyRefineryTypes) == 0;
		public bool ShouldReserveCashForRefinery { get; private set; }

		public int NextScanTick => nextScanTick;
		public int NextMcvRequestTick => nextMcvRequestTick;
		public int NextProgressLogTick => nextProgressLogTick;
		public bool RefineryBuildOutstanding => refineryReservations.Count > 0;
		public int RefineryBuildExpiryTick => refineryReservations.Values.Select(r => r.ExpiryTick).DefaultIfEmpty(0).Max();
		public int RefineryBuildTargetCount => refineryReservations.Values.Select(r => r.TargetCount).DefaultIfEmpty(0).Max();
		public uint[] RefineryReservationQueueIds => refineryReservations.Keys.OrderBy(id => id).ToArray();
		public string[] RefineryReservationTypes => refineryReservations.OrderBy(r => r.Key).Select(r => r.Value.Type).ToArray();
		public int[] RefineryReservationExpiryTicks => refineryReservations.OrderBy(r => r.Key).Select(r => r.Value.ExpiryTick).ToArray();
		public int[] RefineryReservationTargetCounts => refineryReservations.OrderBy(r => r.Key).Select(r => r.Value.TargetCount).ToArray();
		public int[] RefineryReservationCosts => refineryReservations.OrderBy(r => r.Key).Select(r => r.Value.Cost).ToArray();
		public uint[] VehicleFactoryReservationQueueIds => vehicleFactoryReservations.Keys.OrderBy(id => id).ToArray();
		public string[] VehicleFactoryReservationTypes => vehicleFactoryReservations.OrderBy(r => r.Key).Select(r => r.Value.Type).ToArray();
		public int[] VehicleFactoryReservationExpiryTicks => vehicleFactoryReservations.OrderBy(r => r.Key).Select(r => r.Value.ExpiryTick).ToArray();
		public int[] VehicleFactoryReservationTargetCounts => vehicleFactoryReservations.OrderBy(r => r.Key).Select(r => r.Value.TargetCount).ToArray();
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
			harvesterTypes = baseBuilder.SmartEconomyHarvesterTypes;
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

			spendableCash = Math.Max(0, playerResources.Cash + playerResources.Resources);
			UpdateRefineryBuildState();
			UpdateVehicleFactoryBuildState();
			waitingHarvesters = CountWaitingHarvestersNearRefineries();
			RefreshRefineryDemand(bot);
			var capacityDeficitObserved = refineryDemand.Deficit > 0;
			var congestionObserved = refineryPressure.Active ? waitingHarvesters > 0 :
				waitingHarvesters >= Math.Max(1, baseBuilder.Info.SmartEconomyWaitingHarvesterThreshold);
			var refineryPressureObserved = capacityDeficitObserved || congestionObserved;
			var refineryActivationTicks = capacityDeficitObserved ?
				baseBuilder.Info.SmartEconomyRefineryCapacityPressureDuration :
				baseBuilder.Info.SmartEconomyRefineryPressureDuration;
			refineryPressure = SmartEconomyPolicy.UpdatePressure(refineryPressure, refineryPressureObserved,
				elapsed, refineryActivationTicks,
				baseBuilder.Info.SmartEconomyRefineryPressureRelease);
			RecalculateRefineryDemand();
			var minimumRefineryCost = MinimumBuildableRefineryCost();
			refineryCashShortfall = enabled && refineryPressure.Active && refineryDemand.AvailableRequests > 0 &&
				minimumRefineryCost > 0 ? SmartEconomyPolicy.RefineryCashShortfall(spendableCash,
					minimumRefineryCost, liveRefineries, queuedRefineries, reservedRefineries, idleRefineryQueues) : 0;
			ShouldReserveCashForRefinery = refineryCashShortfall > 0;

			var cashThreshold = cashPressure.Active ? baseBuilder.Info.SmartEconomyExcessCashReleaseThreshold :
				baseBuilder.Info.SmartEconomyExcessCashThreshold;
			var cashPressureObserved = !baseBuilder.OpeningActive && baseBuilder.Info.SmartEconomyExcessCashThreshold > 0 &&
				spendableCash >= Math.Max(0, cashThreshold);
			cashPressure = SmartEconomyPolicy.UpdatePressure(cashPressure, cashPressureObserved, elapsed,
				baseBuilder.Info.SmartEconomyExcessCashPressureDuration,
				baseBuilder.Info.SmartEconomyExcessCashPressureRelease);
			RecalculateRefineryDemand();

			if (previousRefineryPressure != refineryPressure.Active || previousCashPressure != cashPressure.Active)
				baseBuilder.LogSmartEconomy(
					"{0} pressure transition: refinery={1} capacity-deficit={2} waiters={3} evidence={4}; cash={5} funds={6} evidence={7}",
					player, refineryPressure.Active, refineryDemand.Deficit, waitingHarvesters,
					refineryPressure.EvidenceTicks, cashPressure.Active, spendableCash, cashPressure.EvidenceTicks);

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
				"{0} status: enabled={1}, harvesters={2} live+{3} queued+{4} requested ({5} free-from-pending), waiters={6}, refinery-pressure={7}/{8}, refineries={9} live+{10} queued+{11} reserved/{12} desired, deficit={13}/{14} available, idle-facts={15}/{16} active, refinery-cash-shortfall={17}, vehicle-factories={18} live+{19} queued+{20} reserved/{21} desired viable={22}, combat-vehicle-queues={23}/{24}, stored={25}/{26}, funds={27}, earned={28}, spent={29}, income={30}, army={31}, assets={32}, silos={33}, production={34}, cash-pressure={35}/{36}, expansion={37} yards+{38} mcvs+{39} requested+{40} queued/{41} desired, expansion-ready={42}, mcv-outstanding={43}/{44}",
				player, enabled, liveHarvesters, queuedHarvesters, requestedHarvesters,
				(queuedRefineries + reservedRefineries) * Math.Max(0, baseBuilder.Info.SmartEconomyFreeHarvestersPerRefinery),
				waitingHarvesters, refineryPressure.EvidenceTicks, refineryPressure.Active, refineries,
				queuedRefineries, reservedRefineries, refineryDemand.DesiredRefineries, refineryDemand.Deficit,
				refineryDemand.AvailableRequests, idleRefineryQueues, activeFactQueues, refineryCashShortfall,
				liveVehicleFactories, queuedVehicleFactories, reservedVehicleFactories, desiredVehicleFactories,
				vehicleFactoryViable, combatUnitQueues, totalUnitQueues, playerResources.Resources,
				playerResources.ResourceCapacity, spendableCash,
				playerResources.Earned, playerResources.Spent, playerStatistics.Income, playerStatistics.ArmyValue,
				playerStatistics.AssetsValue, silos, productionBuildings, cashPressure.EvidenceTicks,
				cashPressure.Active, constructionYards, liveMcvs, requestedMcvs, queuedMcvs,
				desiredExpansionAssets, expansionArmyReady, mcvRequestOutstanding, mcvRequestTargetAssets);
		}

		public bool TryReserveRefineryBuild(ProductionQueue queue, string type)
		{
			if (!WantsRefinery || queue == null || queue.AllQueued().Any() ||
				!baseBuilder.SmartEconomyRefineryTypes.Contains(type) || refineryReservations.ContainsKey(queue.Actor.ActorID))
				return false;

			var queuedQueueIds = QueuedRefineryQueueIds();
			if (!baseBuilder.CanBuildAnotherSmartEconomyRefinery(type))
				return false;

			var cost = queue.GetProductionCost(world.Map.Rules.Actors[type]);
			var queuedRemainingCost = QueuedRefineryRemainingCost();
			var reservedCost = refineryReservations.Values
				.Where(r => !queuedQueueIds.Contains(r.QueueActorId)).Sum(r => r.Cost);
			if (!SmartEconomyPolicy.CanStartThroughputRefinery(spendableCash, queuedRemainingCost,
				reservedCost, cost, queuedRefineries + reservedRefineries))
			{
				if (baseBuilder.Info.SmartEconomyDebugLogging && world.WorldTick >= nextRefineryBlockedLogTick)
				{
					nextRefineryBlockedLogTick = world.WorldTick +
						Math.Max(1, baseBuilder.Info.SmartEconomyProgressLogInterval);
					baseBuilder.LogSmartEconomy(
						"{0} deferred refinery on Fact {1}: funds={2}, queued-remaining={3}, reserved={4}, next-cost={5}",
						player, queue.Actor.ActorID, spendableCash, queuedRemainingCost, reservedCost, cost);
				}

				return false;
			}

			var targetCount = refineryDemand.CommittedRefineries + 1;
			refineryReservations.Add(queue.Actor.ActorID, new RefineryReservation(queue.Actor.ActorID, type,
				world.WorldTick + Math.Max(1, baseBuilder.Info.SmartEconomyRefineryBuildTimeout), targetCount, cost));
			reservedRefineries++;
			RecalculateRefineryDemand();
			baseBuilder.LogSmartEconomy(
				"{0} reserved capacity refinery: type={1}, fact={2}, cost={3}, target-count={4}, remaining-deficit={5}, parallel={6}/{7}",
				player, type, queue.Actor.ActorID, cost, targetCount, refineryDemand.Deficit,
				queuedRefineries + reservedRefineries, effectiveParallelRefineryLimit);
			return true;
		}

		public bool TryReserveVehicleFactoryBuild(ProductionQueue queue, string type)
		{
			if (!WantsEarlyVehicleProductionCapacity || queue == null || queue.AllQueued().Any() ||
				!baseBuilder.Info.VehiclesFactoryTypes.Contains(type) || vehicleFactoryReservations.ContainsKey(queue.Actor.ActorID) ||
				!CanBuildVehicleFactoryType(type))
				return false;

			var targetCount = liveVehicleFactories + queuedVehicleFactories + reservedVehicleFactories + 1;
			vehicleFactoryReservations.Add(queue.Actor.ActorID, new VehicleFactoryReservation(queue.Actor.ActorID, type,
				world.WorldTick + Math.Max(1, baseBuilder.Info.SmartEconomyRefineryBuildTimeout), targetCount));
			reservedVehicleFactories++;
			RecalculateRefineryDemand();
			baseBuilder.LogSmartEconomy(
				"{0} reserved early vehicle factory: type={1}, fact={2}, target={3}/{4}; refinery parallel limit={5}",
				player, type, queue.Actor.ActorID, targetCount, desiredVehicleFactories, effectiveParallelRefineryLimit);
			return true;
		}

		public bool TryReserveMissingRefineryBuild(ProductionQueue queue, string type)
		{
			if (queue == null || queue.AllQueued().Any() || !baseBuilder.SmartEconomyRefineryTypes.Contains(type) ||
				!baseBuilder.CanBuildAnotherSmartEconomyRefinery(type))
				return false;

			var live = baseBuilder.CountActors(baseBuilder.SmartEconomyRefineryTypes);
			var queuedQueueIds = QueuedRefineryQueueIds();
			if (!SmartEconomyPolicy.NeedsSerializedFirstRefinery(enabled, live, queuedQueueIds.Count,
				refineryReservations.Count))
				return false;

			var cost = queue.GetProductionCost(world.Map.Rules.Actors[type]);
			refineryReservations.Add(queue.Actor.ActorID, new RefineryReservation(queue.Actor.ActorID, type,
				world.WorldTick + Math.Max(1, baseBuilder.Info.SmartEconomyRefineryBuildTimeout), 1, cost));
			reservedRefineries++;
			RecalculateRefineryDemand();
			baseBuilder.LogSmartEconomy(
				"{0} reserved serialized missing refinery: type={1}, fact={2}, cost={3}; holding other Facts until one is live",
				player, type, queue.Actor.ActorID, cost);
			return true;
		}

		void UpdateRefineryBuildState()
		{
			if (refineryReservations.Count == 0)
				return;

			var live = baseBuilder.CountActors(baseBuilder.SmartEconomyRefineryTypes);
			var queuedQueueIds = QueuedRefineryQueueIds();
			foreach (var reservation in refineryReservations.Values.OrderBy(r => r.TargetCount)
				.ThenBy(r => r.QueueActorId).ToArray())
			{
				if (live >= reservation.TargetCount)
				{
					refineryReservations.Remove(reservation.QueueActorId);
					baseBuilder.LogSmartEconomy(
						"{0} capacity refinery completed: fact={1}, refineries={2}/{3}",
						player, reservation.QueueActorId, live, reservation.TargetCount);
					continue;
				}

				if (queuedQueueIds.Contains(reservation.QueueActorId) || world.WorldTick < reservation.ExpiryTick)
					continue;

				refineryReservations.Remove(reservation.QueueActorId);
				baseBuilder.LogSmartEconomy(
					"{0} capacity refinery reservation expired: fact={1}, type={2}, refineries={3}/{4}",
					player, reservation.QueueActorId, reservation.Type, live, reservation.TargetCount);
			}
		}

		void UpdateVehicleFactoryBuildState()
		{
			if (vehicleFactoryReservations.Count == 0)
				return;

			var live = baseBuilder.CountActors(baseBuilder.Info.VehiclesFactoryTypes);
			var queuedQueueIds = QueuedVehicleFactoryQueueIds();
			var liveQueueActorIds = OwnedFactConstructionQueues().Select(q => q.Actor.ActorID).ToHashSet();
			foreach (var reservation in vehicleFactoryReservations.Values.OrderBy(r => r.TargetCount)
				.ThenBy(r => r.QueueActorId).ToArray())
			{
				if (queuedQueueIds.Contains(reservation.QueueActorId))
				{
					vehicleFactoryReservations.Remove(reservation.QueueActorId);
					continue;
				}

				if (live >= reservation.TargetCount)
				{
					vehicleFactoryReservations.Remove(reservation.QueueActorId);
					baseBuilder.LogSmartEconomy(
						"{0} early vehicle factory completed: fact={1}, factories={2}/{3}",
						player, reservation.QueueActorId, live, reservation.TargetCount);
					continue;
				}

				if (liveQueueActorIds.Contains(reservation.QueueActorId) && world.WorldTick < reservation.ExpiryTick)
					continue;

				vehicleFactoryReservations.Remove(reservation.QueueActorId);
				baseBuilder.LogSmartEconomy(
					"{0} early vehicle factory reservation expired: fact={1}, type={2}, factories={3}/{4}",
					player, reservation.QueueActorId, reservation.Type, live, reservation.TargetCount);
			}
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

		void RefreshRefineryDemand(IBot bot)
		{
			var queuedQueueIds = QueuedRefineryQueueIds();
			var factQueues = OwnedFactConstructionQueues().ToArray();
			liveHarvesters = baseBuilder.CountActors(harvesterTypes);
			queuedHarvesters = QueuedActors(harvesterTypes) + PendingProductionActors(harvesterTypes);
			requestedHarvesters = RequestedActors(bot, harvesterTypes);
			liveRefineries = baseBuilder.CountActors(baseBuilder.SmartEconomyRefineryTypes);
			queuedRefineries = QueuedActors(baseBuilder.SmartEconomyRefineryTypes);
			reservedRefineries = refineryReservations.Values.Count(r => !queuedQueueIds.Contains(r.QueueActorId));
			idleRefineryQueues = OwnedProductionQueues().Count(q => !q.Trait.AllQueued().Any() &&
				q.Trait.BuildableItems().Any(a => baseBuilder.SmartEconomyRefineryTypes.Contains(a.Name)));
			activeFactQueues = factQueues.Select(q => q.Actor.ActorID).Distinct().Count();
			liveVehicleFactories = baseBuilder.CountActors(baseBuilder.Info.VehiclesFactoryTypes);
			queuedVehicleFactories = QueuedActors(baseBuilder.Info.VehiclesFactoryTypes);
			reservedVehicleFactories = vehicleFactoryReservations.Count;
			desiredVehicleFactories = SmartEconomyPolicy.DesiredEarlyVehicleFactories(activeFactQueues,
				baseBuilder.Info.SmartEconomyEarlyVehicleFactoryPercent);
			vehicleFactoryViable = baseBuilder.Info.VehiclesFactoryTypes.Any(CanBuildVehicleFactoryType);

			var unitQueues = OwnedProductionQueues().Where(q =>
				q.Trait.BuildableItems().Any(a => harvesterTypes.Contains(a.Name))).ToArray();
			totalUnitQueues = unitQueues.Length;
			combatUnitQueues = unitQueues.Count(q => q.Trait.AllQueued().Any(i => !harvesterTypes.Contains(i.Item)));
			RecalculateRefineryDemand();
		}

		void RecalculateRefineryDemand()
		{
			var pendingRefineries = queuedRefineries + reservedRefineries;
			desiredVehicleFactories = Math.Max(
				SmartEconomyPolicy.DesiredEarlyVehicleFactories(activeFactQueues,
					baseBuilder.Info.SmartEconomyEarlyVehicleFactoryPercent),
				SmartEconomyPolicy.DesiredVehicleFactoriesForRefineryBalance(
					liveRefineries + pendingRefineries, baseBuilder.Info.SmartEconomyEarlyVehicleFactoryPercent));
			effectiveParallelRefineryLimit = SmartEconomyPolicy.EffectiveParallelRefineryLimit(activeFactQueues,
				baseBuilder.Info.SmartEconomyMaximumParallelRefineries,
				baseBuilder.Info.SmartEconomyEarlyVehicleFactoryPercent, WantsEarlyVehicleProductionCapacity);
			refineryDemand = SmartEconomyPolicy.RefineryDemand(liveHarvesters, queuedHarvesters,
				requestedHarvesters, liveRefineries, queuedRefineries, reservedRefineries,
				baseBuilder.Info.SmartEconomyFreeHarvestersPerRefinery,
				baseBuilder.Info.SmartEconomyHarvestersPerRefinery,
				effectiveParallelRefineryLimit,
				refineryPressure.Active && waitingHarvesters > 0);
		}

		IEnumerable<TraitPair<ProductionQueue>> OwnedProductionQueues()
		{
			return world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Actor.IsInWorld);
		}

		IEnumerable<TraitPair<ProductionQueue>> OwnedFactConstructionQueues()
		{
			return OwnedProductionQueues().Where(q =>
				baseBuilder.Info.ConstructionYardTypes.Contains(q.Actor.Info.Name) &&
				baseBuilder.Info.BuildingQueues.Contains(q.Trait.Info.Type));
		}

		bool CanBuildVehicleFactoryType(string type)
		{
			if (!baseBuilder.Info.VehiclesFactoryTypes.Contains(type) || !world.Map.Rules.Actors.ContainsKey(type))
				return false;

			var committed = world.Actors.Count(a => a.Owner == player && !a.IsDead && a.Info.Name == type) +
				OwnedProductionQueues().Sum(q => q.Trait.AllQueued().Count(item => item.Item == type)) +
				vehicleFactoryReservations.Values.Count(r => r.Type == type);
			if (baseBuilder.Info.BuildingLimits != null &&
				baseBuilder.Info.BuildingLimits.TryGetValue(type, out var limit) && committed >= limit)
				return false;

			return OwnedFactConstructionQueues().Any(q => q.Trait.BuildableItems().Any(a => a.Name == type));
		}

		int QueuedActors(HashSet<string> types)
		{
			return OwnedProductionQueues().Sum(q => q.Trait.AllQueued().Count(item => types.Contains(item.Item)));
		}

		int PendingProductionActors(HashSet<string> types)
		{
			return world.ActorsWithTrait<IPendingProductionActors>()
				.Where(p => p.Actor.Owner == player && !p.Actor.IsDead && p.Actor.IsInWorld)
				.Sum(p => p.Trait.PendingActorTypes.Count(types.Contains));
		}

		int RequestedActors(IBot bot, HashSet<string> types)
		{
			return types.Sum(type => unitProduction.Where(r => r.IsTraitEnabled())
				.Select(r => r.RequestedProductionCount(bot, type)).DefaultIfEmpty(0).Max());
		}

		HashSet<uint> QueuedRefineryQueueIds()
		{
			return OwnedProductionQueues().Where(q => q.Trait.AllQueued()
				.Any(item => baseBuilder.SmartEconomyRefineryTypes.Contains(item.Item)))
				.Select(q => q.Actor.ActorID).ToHashSet();
		}

		HashSet<uint> QueuedVehicleFactoryQueueIds()
		{
			return OwnedProductionQueues().Where(q => q.Trait.AllQueued()
				.Any(item => baseBuilder.Info.VehiclesFactoryTypes.Contains(item.Item)))
				.Select(q => q.Actor.ActorID).ToHashSet();
		}

		int QueuedRefineryRemainingCost()
		{
			return OwnedProductionQueues().SelectMany(q => q.Trait.AllQueued())
				.Where(item => baseBuilder.SmartEconomyRefineryTypes.Contains(item.Item))
				.Sum(item => item.RemainingCost);
		}

		int MinimumBuildableRefineryCost()
		{
			return OwnedProductionQueues().SelectMany(q => q.Trait.BuildableItems()
				.Where(a => baseBuilder.SmartEconomyRefineryTypes.Contains(a.Name))
				.Select(a => q.Trait.GetProductionCost(a))).Where(cost => cost > 0).DefaultIfEmpty(0).Min();
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
			postLoadResumeTick = SmartEconomyPolicy.PostLoadResumeTick(world.WorldTick,
				baseBuilder.Info.SmartEconomyScanInterval);
			nextScanTick = Math.Max(savedNextScanTick, postLoadResumeTick);
			nextMcvRequestTick = savedNextMcvRequestTick;
			nextProgressLogTick = savedNextProgressLogTick;
			refineryReservations.Clear();
			vehicleFactoryReservations.Clear();
			if (savedRefineryBuildOutstanding)
				refineryReservations.Add(0, new RefineryReservation(0, string.Empty,
					savedRefineryBuildExpiryTick, savedRefineryBuildTargetCount, 0));
			mcvRequestOutstanding = savedMcvRequestOutstanding;
			mcvRequestExpiryTick = savedMcvRequestExpiryTick;
			mcvRequestTargetAssets = savedMcvRequestTargetAssets;
			refineryPressure = savedRefineryPressure;
			cashPressure = savedCashPressure;
			ShouldReserveCashForRefinery = false;
			baseBuilder.LogSmartEconomy("{0} restored: deferring new economy requests until tick {1}",
				player, postLoadResumeTick);
		}

		public void LoadRefineryReservations(uint[] queueActorIds, string[] types, int[] expiryTicks,
			int[] targetCounts, int[] costs)
		{
			var count = new[]
			{
				queueActorIds?.Length ?? 0, types?.Length ?? 0, expiryTicks?.Length ?? 0,
				targetCounts?.Length ?? 0, costs?.Length ?? 0
			}.Min();
			if (count == 0)
				return;

			refineryReservations.Clear();
			for (var i = 0; i < count; i++)
			{
				var queueActorId = queueActorIds[i];
				if (refineryReservations.ContainsKey(queueActorId))
					continue;

				refineryReservations.Add(queueActorId, new RefineryReservation(queueActorId, types[i],
					expiryTicks[i], targetCounts[i], costs[i]));
			}
		}

		public void LoadVehicleFactoryReservations(uint[] queueActorIds, string[] types, int[] expiryTicks,
			int[] targetCounts)
		{
			var count = new[]
			{
				queueActorIds?.Length ?? 0, types?.Length ?? 0, expiryTicks?.Length ?? 0,
				targetCounts?.Length ?? 0
			}.Min();
			if (count == 0)
				return;

			vehicleFactoryReservations.Clear();
			for (var i = 0; i < count; i++)
			{
				var queueActorId = queueActorIds[i];
				if (vehicleFactoryReservations.ContainsKey(queueActorId))
					continue;

				vehicleFactoryReservations.Add(queueActorId, new VehicleFactoryReservation(queueActorId, types[i],
					expiryTicks[i], targetCounts[i]));
			}
		}
	}
}
