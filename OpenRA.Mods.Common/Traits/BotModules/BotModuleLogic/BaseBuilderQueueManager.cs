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
	class BaseBuilderQueueManager
	{
		readonly string category;

		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		readonly PowerManager playerPower;
		readonly PlayerResources playerResources;
		readonly PlayerStatistics playerStats;
		readonly IResourceLayer resourceLayer;

		int waitTicks;
		Actor[] playerBuildings;
		int failCount;
		int failRetryTicks;
		int checkForBasesTicks;
		int cachedBases;
		int cachedBuildings;
		int minimumExcessPower;
		CPos? lastUsedDefenseLocation = null;

		WaterCheck waterState = WaterCheck.NotChecked;

		public bool IsDefenseQueue => baseBuilder.Info.DefenseQueues.Contains(category);

		public BaseBuilderQueueManager(BaseBuilderBotModule baseBuilder, string category, Player p, PowerManager pm,
			PlayerResources pr, IResourceLayer rl)
		{
			this.baseBuilder = baseBuilder;
			world = p.World;
			player = p;
			playerPower = pm;
			playerResources = pr;
			playerStats = p.PlayerActor.Trait<PlayerStatistics>();
			resourceLayer = rl;
			this.category = category;
			failRetryTicks = baseBuilder.Info.StructureProductionResumeDelay;
			minimumExcessPower = baseBuilder.Info.MinimumExcessPower;
			if (!baseBuilder.Info.NavalProductionTypes.Any())
				waterState = WaterCheck.DontCheck;
		}

		// BotDebug is read by humans watching a match, so trade the exact yaml key for something
		// legible: "Guard Tower (gtwr)" instead of just "gtwr".
		string DisplayName(string type)
		{
			var tooltip = world.Map.Rules.Actors[type].TraitInfoOrDefault<TooltipInfo>();
			return !string.IsNullOrEmpty(tooltip?.Name) ? $"{tooltip.Name} ({type})" : type;
		}

		public void Tick(IBot bot)
		{
			// If failed to place something N consecutive times, wait M ticks until resuming building production
			if (failCount >= baseBuilder.Info.MaximumFailedPlacementAttempts && --failRetryTicks <= 0)
			{
				var currentBuildings = world.ActorsHavingTrait<Building>().Count(a => a.Owner == player);
				var baseProviders = world.ActorsHavingTrait<BaseProvider>().Count(a => a.Owner == player);

				// Only bother resetting failCount if either a) the number of buildings has decreased since last failure M ticks ago,
				// or b) number of BaseProviders (construction yard or similar) has increased since then.
				// Otherwise reset failRetryTicks instead to wait again.
				if (currentBuildings < cachedBuildings || baseProviders > cachedBases)
					failCount = 0;
				else
					failRetryTicks = baseBuilder.Info.StructureProductionResumeDelay;
			}

			if (waterState == WaterCheck.NotChecked)
			{
				if (AIUtils.IsAreaAvailable<BaseProvider>(world, player, world.Map, baseBuilder.Info.MaxBaseRadius, baseBuilder.Info.WaterTerrainTypes))
					waterState = WaterCheck.EnoughWater;
				else
				{
					waterState = WaterCheck.NotEnoughWater;
					checkForBasesTicks = baseBuilder.Info.CheckForNewBasesDelay;
				}
			}

			if (waterState == WaterCheck.NotEnoughWater && --checkForBasesTicks <= 0)
			{
				var currentBases = world.ActorsHavingTrait<BaseProvider>().Count(a => a.Owner == player);

				if (currentBases > cachedBases)
				{
					cachedBases = currentBases;
					waterState = WaterCheck.NotChecked;
				}
			}

			// Only update once per second or so
			if (--waitTicks > 0)
				return;

			playerBuildings = world.ActorsHavingTrait<Building>().Where(a => a.Owner == player).ToArray();
			var excessPowerBonus = baseBuilder.Info.ExcessPowerIncrement * (playerBuildings.Count() / baseBuilder.Info.ExcessPowerIncreaseThreshold.Clamp(1, int.MaxValue));
			minimumExcessPower = (baseBuilder.Info.MinimumExcessPower + excessPowerBonus).Clamp(baseBuilder.Info.MinimumExcessPower, baseBuilder.Info.MaximumExcessPower);

			var active = false;
			foreach (var queue in AIUtils.FindQueues(player, category))
				if (TickQueue(bot, queue))
					active = true;

			// Add a random factor so not every AI produces at the same tick early in the game.
			// Minimum should not be negative as delays in HackyAI could be zero.
			var randomFactor = world.LocalRandom.Next(0, baseBuilder.Info.StructureProductionRandomBonusDelay);

			var nextWaitTicks = active ? baseBuilder.Info.StructureProductionActiveDelay + randomFactor
				: baseBuilder.Info.StructureProductionInactiveDelay + randomFactor;
			if (IsDefenseQueue && baseBuilder.WallPlanner != null)
				nextWaitTicks = baseBuilder.WallPlanner.LimitConstructionYardEnclosurePollDelay(nextWaitTicks);
			waitTicks = nextWaitTicks;
		}

		bool TickQueue(IBot bot, ProductionQueue queue)
		{
			var currentBuilding = queue.AllQueued().FirstOrDefault();
			if (currentBuilding != null)
			{
				var priorityRecoveryActive = baseBuilder.OpeningActive ||
					baseBuilder.SmartEconomySerializesMissingRefinery ||
					(playerPower != null && (playerPower.PowerState != PowerState.Normal ||
						playerPower.ExcessPower < minimumExcessPower));
				var recovery = baseBuilder.DefenseClusterManager?.TryChooseQueuedRepairRecovery(
					queue, queue.BuildableItems(), priorityRecoveryActive);
				if (recovery != null)
				{
					bot.QueueOrder(Order.StartProduction(queue.Actor, recovery.Name, 1));
					baseBuilder.LogProductionSpend(recovery, queue);
				}
			}
			if (IsDefenseQueue)
				baseBuilder.WallPlanner?.LogConstructionYardEnclosureQueueState(queue, currentBuilding,
					playerResources.Cash, playerResources.Resources);

			// Waiting to build something
			if (currentBuilding == null && failCount < baseBuilder.Info.MaximumFailedPlacementAttempts)
			{
				var item = ChooseBuildingToBuild(queue);
				if (item == null)
					return false;

				bot.QueueOrder(Order.StartProduction(queue.Actor, item.Name, 1));
				baseBuilder.LogProductionSpend(item, queue);
			}
			else if (currentBuilding != null && currentBuilding.Done)
			{
				// Production is complete
				// Choose the placement logic
				// HACK: HACK HACK HACK
				// TODO: Derive this from BuildingCommonNames instead
				var type = BuildingType.Building;
				CPos? location = null;
				string orderString = "PlaceBuilding";
				var fieldPlacement = false;

				// Check if Building is a plug for other Building
				var actorInfo = world.Map.Rules.Actors[currentBuilding.Item];
				var plugInfo = actorInfo.TraitInfoOrDefault<PlugInfo>();
				if (plugInfo != null)
				{
					var possibleBuilding = world.ActorsWithTrait<Pluggable>().FirstOrDefault(a =>
						a.Actor.Owner == player && a.Trait.AcceptsPlug(a.Actor, plugInfo.Type));

					if (possibleBuilding.Actor != null)
					{
						orderString = "PlacePlug";
						location = possibleBuilding.Actor.Location + possibleBuilding.Trait.Info.Offset;
					}
				}
				else if (baseBuilder.TiberiumFieldManager != null &&
					baseBuilder.TiberiumFieldManager.TryGetPlacement(
						queue.Actor.ActorID, actorInfo.Name, out location, out var fieldLineBuild))
				{
					fieldPlacement = true;
					if (fieldLineBuild)
						orderString = "LineBuild";
				}
				else if (baseBuilder.DefenseClusterManager?.OwnsPlacement(queue, actorInfo.Name) == true)
				{
					location = baseBuilder.DefenseClusterManager.ChooseLocation(queue, actorInfo,
						actorInfo.TraitInfo<BuildingInfo>());
				}
				else if (baseBuilder.WallPlanner != null && baseBuilder.WallPlanner.IsWallType(actorInfo.Name))
				{
					location = baseBuilder.WallPlanner.TakeWallCell(queue, actorInfo.Name, out var wallLineBuild);
					if (wallLineBuild)
						orderString = "LineBuild";
				}
				else
				{
					var economySamPlacement = baseBuilder.OwnsEconomyDefenseSam(queue, actorInfo.Name);
					if (economySamPlacement)
						location = baseBuilder.EconomyDefenseSamLocation(queue, currentBuilding.Item, actorInfo,
							actorInfo.TraitInfo<BuildingInfo>(), true);
					else if (baseBuilder.FirstTowerPlanner.AppliesTo(actorInfo.Name))
						location = baseBuilder.FirstTowerPlanner.ChooseLocation(actorInfo, actorInfo.TraitInfo<BuildingInfo>());

					if (location == null && !economySamPlacement)
					{
						// Check if Building is a defense and if we should place it towards the enemy or not.
						if (actorInfo.HasTraitInfo<AttackBaseInfo>() && world.LocalRandom.Next(100) < baseBuilder.Info.PlaceDefenseTowardsEnemyChance)
							type = BuildingType.Defense;
						else if (baseBuilder.Info.PlaceAsDefenses.Contains(actorInfo.Name) && world.LocalRandom.Next(100) < baseBuilder.Info.PlaceAsDefenseChance)
							type = BuildingType.Defense;
						else if (baseBuilder.Info.RefineryTypes.Contains(actorInfo.Name))
							type = BuildingType.Refinery;

						location = ChooseBuildLocation(currentBuilding.Item, true, type);
					}
				}

				if (location == null)
				{
					if (fieldPlacement)
						baseBuilder.TiberiumFieldManager.PlacementFailed("reserved site became illegal before placement");

					AIUtils.BotDebug($"{player} has nowhere to place {DisplayName(currentBuilding.Item)}");
					bot.QueueOrder(Order.CancelProduction(queue.Actor, currentBuilding.Item, 1));
					failCount += failCount;

					// If we just reached the maximum fail count, cache the number of current structures
					if (failCount == baseBuilder.Info.MaximumFailedPlacementAttempts)
					{
						cachedBuildings = world.ActorsHavingTrait<Building>().Count(a => a.Owner == player);
						cachedBases = world.ActorsHavingTrait<BaseProvider>().Count(a => a.Owner == player);
					}
				}
				else
				{
					failCount = 0;

					bot.QueueOrder(new Order(orderString, player.PlayerActor, Target.FromCell(world, location.Value), false)
					{
						// Building to place
						TargetString = currentBuilding.Item,

						// Actor ID to associate the placement with
						ExtraData = queue.Actor.ActorID,
						SuppressVisualFeedback = true
					});
					if (fieldPlacement)
						baseBuilder.TiberiumFieldManager.PlacementOrdered();

					return true;
				}
			}

			return true;
		}

		ActorInfo GetProducibleBuilding(HashSet<string> actors, IEnumerable<ActorInfo> buildables, Func<ActorInfo, double> orderBy = null)
		{
			var available = buildables.Where(actor =>
			{
				// Are we able to build this?
				if (!actors.Contains(actor.Name))
					return false;

				if (!baseBuilder.Info.BuildingLimits.ContainsKey(actor.Name))
					return true;

				var committed = playerBuildings.Count(a => a.Info.Name == actor.Name) +
					baseBuilder.CountQueuedOrPendingActors(new[] { actor.Name }) +
					(baseBuilder.IsOpeningStructureReserved(actor.Name) ? 1 : 0);
				return committed < baseBuilder.Info.BuildingLimits[actor.Name];
			});

			if (orderBy != null)
				return available.MaxByOrDefault(orderBy);

			return available.RandomOrDefault(world.LocalRandom);
		}

		ActorInfo PreferredOpeningFirstTower(IEnumerable<ActorInfo> buildables)
		{
			if (!baseBuilder.Info.PrioritizeOpeningFirstTower || !baseBuilder.OpeningActive ||
				baseBuilder.FirstTowerPlanner.Complete)
				return null;

			foreach (var type in baseBuilder.Info.OpeningDefenseTypes)
			{
				if (!baseBuilder.Info.FirstTowerTypes.Contains(type))
					continue;

				var tower = GetProducibleBuilding(new HashSet<string> { type }, buildables);
				if (tower != null)
					return tower;
			}

			return null;
		}

		bool HasSufficientPowerForActor(ActorInfo actorInfo)
		{
			return playerPower == null || (actorInfo.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault)
				.Sum(p => p.Amount) + playerPower.ExcessPower) >= baseBuilder.Info.MinimumExcessPower;
		}

		ActorInfo ChooseBuildingToBuild(ProductionQueue queue)
		{
			var buildableThings = queue.BuildableItems();

			// This gets used quite a bit, so let's cache it here
			var power = GetProducibleBuilding(baseBuilder.Info.PowerTypes, buildableThings,
				a => a.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(p => p.Amount));

			// First priority is to get out of a low power situation
			if (playerPower != null && playerPower.ExcessPower < minimumExcessPower)
			{
				if (power != null && power.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(p => p.Amount) > 0)
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (low power)", queue.Actor.Owner, DisplayName(power.Name));
					return power;
				}
			}

			// A smart AI with no live unloading refinery is in critical recovery. Let one
			// Fact fund the serialized refinery (or the power prerequisite above), and
			// keep every other construction queue from splitting the remaining cash.
			if (baseBuilder.SmartEconomySerializesMissingRefinery)
			{
				var refinery = GetProducibleBuilding(baseBuilder.SmartEconomyRefineryTypes, buildableThings);
				if (refinery != null && HasSufficientPowerForActor(refinery) &&
					baseBuilder.TryReserveSmartEconomyMissingRefinery(queue, refinery.Name))
				{
					AIUtils.BotDebug("{0} decided to build {1}: serialized missing-refinery recovery",
						queue.Actor.Owner, DisplayName(refinery.Name));
					return refinery;
				}

				return null;
			}

			var enclosureWall = baseBuilder.WallPlanner?.ConstructionYardEnclosureWall(queue, buildableThings, playerBuildings);
			if (enclosureWall != null)
			{
				AIUtils.BotDebug("{0} decided to build {1}: construction-yard enclosure",
					queue.Actor.Owner, DisplayName(enclosureWall.Name));
				return enclosureWall;
			}

			// Once the first refinery is live, establish the configured share of useful
			// vehicle-factory capacity before any refinery source can consume those Facts.
			if (baseBuilder.SmartEconomyWantsEarlyVehicleProductionCapacity)
			{
				var vehicleFactories = buildableThings.Where(a => baseBuilder.Info.VehiclesFactoryTypes.Contains(a.Name))
					.OrderByDescending(a => AdaptiveWeighting.ProductionBuildingScore(
						baseBuilder.AdaptiveProductionBuildingDemand(a), playerBuildings.Count(b => b.Info.Name == a.Name)))
					.ThenBy(a => a.Name, StringComparer.Ordinal).ToArray();
				foreach (var vehicleFactory in vehicleFactories)
				{
					if (HasSufficientPowerForActor(vehicleFactory) &&
						baseBuilder.TryReserveSmartEconomyVehicleFactory(queue, vehicleFactory.Name))
					{
						baseBuilder.LogSmartEconomy("{0} decided to build {1}: early vehicle-production priority",
							queue.Actor.Owner, DisplayName(vehicleFactory.Name));
						return vehicleFactory;
					}
				}

				if (power != null && vehicleFactories.Any(v => !HasSufficientPowerForActor(v)))
				{
					baseBuilder.LogSmartEconomy("{0} decided to build {1}: early vehicle factory requires power",
						queue.Actor.Owner, DisplayName(power.Name));
					return power;
				}
			}

			var opening = baseBuilder.OpeningBuilding(buildableThings);
			if (opening != null)
			{
				if (baseBuilder.SmartEconomyRefineryTypes.Contains(opening.Name) &&
					!baseBuilder.TryReserveSmartEconomyControlledRefinery(queue, opening.Name))
					opening = null;
			}

			if (opening != null)
			{
				AIUtils.BotDebug("{0} decided to build {1}: parallel opening policy", queue.Actor.Owner, DisplayName(opening.Name));
				return opening;
			}

			// Defense queues are independent from the ordered building queue. Give their first
			// build to the configured opening-defense preference instead of the shuffled fallback.
			var firstTower = PreferredOpeningFirstTower(buildableThings);
			if (firstTower != null)
			{
				if (!HasSufficientPowerForActor(firstTower))
					return null;

				if (baseBuilder.FirstTowerPlanner.TryReserveBuild(firstTower.Name))
				{
					AIUtils.BotDebug("{0} decided to build {1}: preferred opening first tower",
						queue.Actor.Owner, DisplayName(firstTower.Name));
					return firstTower;
				}

				return null;
			}

			// Once the authored opening and power prerequisites are satisfied, an uncovered
			// economy anchor may reserve one normal SAM build. Existing powered overlapping
			// coverage and a single in-flight reservation suppress duplicate sites.
			var economySam = baseBuilder.EconomyDefenseSamBuilding(queue, buildableThings);
			if (economySam != null)
			{
				AIUtils.BotDebug("{0} decided to build {1}: uncovered economy air approach",
					queue.Actor.Owner, DisplayName(economySam.Name));
				return economySam;
			}

			// Next is to build up a strong economy
			if (!baseBuilder.HasAdequateRefineryCount)
			{
				var refinery = GetProducibleBuilding(baseBuilder.SmartEconomyRefineryTypes, buildableThings);
				if (refinery != null && HasSufficientPowerForActor(refinery) &&
					baseBuilder.TryReserveSmartEconomyControlledRefinery(queue, refinery.Name))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (refinery)", queue.Actor.Owner, DisplayName(refinery.Name));
					return refinery;
				}

				if (power != null && refinery != null && !HasSufficientPowerForActor(refinery))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, DisplayName(power.Name));
					return power;
				}
			}

			// Persistent loaded-harvester congestion is stronger evidence than a fixed
			// harvester/refinery ratio. Add unloading capacity before discretionary scaling.
			if (baseBuilder.SmartEconomyWantsRefinery)
			{
				var refinery = GetProducibleBuilding(baseBuilder.SmartEconomyRefineryTypes, buildableThings);
				if (refinery != null && HasSufficientPowerForActor(refinery) &&
					baseBuilder.TryReserveSmartEconomyRefinery(queue, refinery.Name))
				{
					baseBuilder.LogSmartEconomy("{0} decided to build {1}: sustained unload congestion",
						queue.Actor.Owner, DisplayName(refinery.Name));
					return refinery;
				}

				if (power != null && refinery != null && !HasSufficientPowerForActor(refinery))
				{
					baseBuilder.LogSmartEconomy("{0} decided to build {1}: congested refinery requires power",
						queue.Actor.Owner, DisplayName(power.Name));
					return power;
				}
			}

			// Aircraft production and repair are independent activities, but each occupied pad
			// can service only one aircraft at a time. Scale repair capacity with the live fleet.
			var airRepair = baseBuilder.AirRepairCapacityBuilding(buildableThings);
			if (airRepair != null && HasSufficientPowerForActor(airRepair) &&
				baseBuilder.TryReserveAirRepairCapacity(queue, airRepair.Name))
			{
				AIUtils.BotDebug("{0} decided to build {1}: aircraft repair-capacity demand",
					queue.Actor.Owner, DisplayName(airRepair.Name));
				return airRepair;
			}

			if (power != null && airRepair != null && !HasSufficientPowerForActor(airRepair))
			{
				AIUtils.BotDebug("{0} decided to build {1}: aircraft repair capacity requires power",
					queue.Actor.Owner, DisplayName(power.Name));
				return power;
			}

			// Field development is discretionary and serialized globally. It is considered only
			// after opening/refinery/power/repair owners, and it applies its own cash and route gate.
			var fieldBuilding = baseBuilder.TiberiumFieldManager?.TryChooseBuilding(queue, buildableThings);
			if (fieldBuilding != null)
			{
				AIUtils.BotDebug("{0} decided to build {1}: reserved Tiberium field project",
					queue.Actor.Owner, DisplayName(fieldBuilding.Name));
				return fieldBuilding;
			}

			// Preserve the original random production-building selector when cash is floating.
			// Smart economy only decides refinery work; all other choices retain authored limits.
			var availableFunds = Math.Max(0, playerResources.Cash + playerResources.Resources);
			if (baseBuilder.Info.NewProductionCashThreshold > 0 && availableFunds > baseBuilder.Info.NewProductionCashThreshold)
			{
				var production = GetProducibleBuilding(baseBuilder.Info.ProductionTypes, buildableThings);
				if (production != null && HasSufficientPowerForActor(production))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (production)",
						queue.Actor.Owner, DisplayName(production.Name));
					return production;
				}

				if (power != null && production != null && !HasSufficientPowerForActor(production))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, DisplayName(power.Name));
					return power;
				}
			}

			// Only consider building this if there is enough water inside the base perimeter and there are close enough adjacent buildings
			if (waterState == WaterCheck.EnoughWater && baseBuilder.Info.NewProductionCashThreshold > 0
				&& playerResources.Resources > baseBuilder.Info.NewProductionCashThreshold
				&& AIUtils.IsAreaAvailable<GivesBuildableArea>(world, player, world.Map, baseBuilder.Info.CheckForWaterRadius, baseBuilder.Info.WaterTerrainTypes))
			{
				var navalproduction = GetProducibleBuilding(baseBuilder.Info.NavalProductionTypes, buildableThings);
				if (navalproduction != null && HasSufficientPowerForActor(navalproduction))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (navalproduction)", queue.Actor.Owner, DisplayName(navalproduction.Name));
					return navalproduction;
				}

				if (power != null && navalproduction != null && !HasSufficientPowerForActor(navalproduction))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, DisplayName(power.Name));
					return power;
				}
			}

			// Create some head room for resource storage if we really need it
			if (playerResources.Resources > 0.8 * playerResources.ResourceCapacity)
			{
				var silo = GetProducibleBuilding(baseBuilder.Info.SiloTypes, buildableThings);
				if (silo != null && HasSufficientPowerForActor(silo))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (silo)", queue.Actor.Owner, DisplayName(silo.Name));
					return silo;
				}

				if (power != null && silo != null && !HasSufficientPowerForActor(silo))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, DisplayName(power.Name));
					return power;
				}
			}

			// If other building types are limited we allow AI to build less important buildings:
			Dictionary<string, int> limitedFractions = new Dictionary<string, int>();
			int totalLimitedFrac = 0;

			foreach (var playerBuilding in playerBuildings)
			{
				var name = playerBuilding.Info.Name;
				if (limitedFractions.ContainsKey(name))
					continue;
				var count = playerBuildings.Count(a => a.Info.Name == name);
				if (baseBuilder.Info.BuildingLimits.ContainsKey(name) && baseBuilder.Info.BuildingLimits[name] <= count)
				{
					limitedFractions.Add(name, baseBuilder.Info.BuildingFractions.ContainsKey(name) ? baseBuilder.Info.BuildingFractions[name] : 0);
					totalLimitedFrac += limitedFractions[name];
				}
			}

			// Build everything else
			foreach (var frac in baseBuilder.Info.BuildingFractions.Shuffle(world.LocalRandom))
			{
				var name = frac.Key;
				if (limitedFractions.ContainsKey(name))
					continue;

				// An enabled field manager owns every Resonator request so a generic fraction cannot
				// create an unassigned actor or duplicate a queue reservation.
				if (baseBuilder.TiberiumFieldManager?.OwnsActorType(name) == true)
					continue;

				// While a smart AI has no live refinery, every refinery source shares the one
				// serialized recovery reservation. This also covers the ordinary authored
				// building-fraction fallback after the higher-priority paths declined a queue.
				// Does this building have initial delay, if so have we passed it?
				if (baseBuilder.Info.BuildingDelays != null &&
					baseBuilder.Info.BuildingDelays.ContainsKey(name) &&
					baseBuilder.Info.BuildingDelays[name] > world.WorldTick)
					continue;

				// Can we build this structure?
				if (!buildableThings.Any(b => b.Name == name))
					continue;

				// Walls are only worth queueing if the planner has a line ready for them, and only
				// until we hit the segment cap. Everything about where they go is decided there.
				if (baseBuilder.WallPlanner != null && baseBuilder.WallPlanner.IsWallType(name)
					&& !baseBuilder.WallPlanner.WantsToBuildWall(queue, name, playerBuildings))
					continue;

				// Do we want to build this structure? Adaptive defense types get their authored ceiling
				// nudged by measured kills-value/losses-value performance instead of using it as-is.
				var count = playerBuildings.Count(a => a.Info.Name == name);
				var fractionValue = baseBuilder.Info.AdaptiveBuildingTypes.Contains(name) ? AdaptiveFraction(name, frac.Value) : frac.Value;
				if (count * 100 > (fractionValue + totalLimitedFrac) * playerBuildings.Length)
					continue;

				// If we're considering to build a naval structure, check whether there is enough water inside the base perimeter
				// and any structure providing buildable area close enough to that water.
				// TODO: Extend this check to cover any naval structure, not just production.
				if (baseBuilder.Info.NavalProductionTypes.Contains(name)
					&& (waterState == WaterCheck.NotEnoughWater
						|| !AIUtils.IsAreaAvailable<GivesBuildableArea>(world, player, world.Map, baseBuilder.Info.CheckForWaterRadius, baseBuilder.Info.WaterTerrainTypes)))
					continue;

				// Will this put us into low power?
				var actor = world.Map.Rules.Actors[name];
				if (playerPower != null && (playerPower.ExcessPower < minimumExcessPower || !HasSufficientPowerForActor(actor)))
				{
					// Try building a power plant instead
					if (power != null && power.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(pi => pi.Amount) > 0)
					{
						if (playerPower.PowerOutageRemainingTicks > 0)
							AIUtils.BotDebug("{0} decided to build {1}: Priority override (is low power)", queue.Actor.Owner, DisplayName(power.Name));
						else
							AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, DisplayName(power.Name));

						return power;
					}
				}

				// Enabled smart bots route every refinery source through the same per-Fact
				// reservation after all other authored checks have accepted the candidate.
				if (baseBuilder.SmartEconomyRefineryTypes.Contains(name) &&
					!baseBuilder.TryReserveSmartEconomyControlledRefinery(queue, name))
					continue;

				// Lets build this
				AIUtils.BotDebug("{0} decided to build {1}: Desired is {2} ({3} / {4}); current is {5} / {4}",
					queue.Actor.Owner, DisplayName(name), frac.Value, frac.Value * playerBuildings.Length, playerBuildings.Length, count);
				if (baseBuilder.AdaptiveProductionDebugLogging && baseBuilder.Info.AdaptiveBuildingTypes.Contains(name))
					baseBuilder.LogAdaptiveProduction(
						"{0} selected adaptive defense building {1}: authored={2} adapted={3} owned={4} buildings={5}",
						player, DisplayName(name), frac.Value, fractionValue, count, playerBuildings.Length);

				return actor;
			}

			// Too spammy to keep enabled all the time, but very useful when debugging specific issues.
			// AIUtils.BotDebug("{0} couldn't decide what to build for queue {1}.", queue.Actor.Owner, queue.Info.Group);
			return null;
		}

		// BuildingFractions is already a percentage ceiling, so the adaptive floor/ceiling (Q6: "1%/50%")
		// applies directly to it - no need for the probability-share normalization UnitBuilderBotModule
		// uses, since that model doesn't fit this ceiling-and-shuffle selection at all.
		int AdaptiveFraction(string name, int authoredFraction)
		{
			var stats = playerStats.AdaptiveStats[name];
			var confidence = AdaptiveWeighting.Confidence(stats.KillsCount + stats.LossesCount, baseBuilder.Info.AdaptiveConfidenceSamples);
			var adapted = AdaptiveWeighting.AdaptedWeight(authoredFraction, stats.DecayedScore, confidence);

			var floor = baseBuilder.Info.AdaptiveWeightFloor * 100;
			var ceiling = baseBuilder.Info.AdaptiveWeightCeiling * 100;
			adapted = Math.Clamp(adapted, floor, ceiling);

			return (int)Math.Round(adapted);
		}

		CPos? ChooseBuildLocation(string actorType, bool distanceToBaseIsImportant, BuildingType type)
		{
			var actorInfo = world.Map.Rules.Actors[actorType];
			var bi = actorInfo.TraitInfoOrDefault<BuildingInfo>();
			if (bi == null)
				return null;

			// Find the buildable cell that is closest to pos and centered around center
			Func<CPos, CPos, int, int, CPos?> findPos = (center, target, minRange, maxRange) =>
			{
				var candidateCells = world.Map.FindTilesInAnnulus(center, minRange, maxRange);
				const int ComparableCandidateLimit = 8;
				var randomlyOrdered = center == target;

				// Sort by distance to target if we have one
				IEnumerable<CPos> cells;
				if (!randomlyOrdered)
					cells = candidateCells.OrderBy(c => (c - target).LengthSquared);
				else
					cells = candidateCells.Shuffle(world.LocalRandom);

				CPos? reservedFallback = null;
				var legalCandidates = 0;
				foreach (var cell in cells)
				{
					if (!world.CanPlaceBuilding(cell, actorInfo, bi, null))
						continue;

					if (distanceToBaseIsImportant && !bi.IsCloseEnoughToBase(world, player, actorInfo, cell))
						continue;

					legalCandidates++;
					if (!baseBuilder.WallPlanner.OverlapsConstructionYardEnclosure(cell, bi))
					{
						if (reservedFallback != null)
							baseBuilder.WallPlanner.LogReservationDecision(actorType,
								reservedFallback.Value, cell, false);

						return cell;
					}

					if (reservedFallback == null)
						reservedFallback = cell;
					if (legalCandidates >= ComparableCandidateLimit)
						break;
				}

				if (reservedFallback != null && randomlyOrdered)
				{
					var alternative = ConstructionYardEnclosurePolicy.FirstLegalUnreservedCell(candidateCells,
						cell => world.CanPlaceBuilding(cell, actorInfo, bi, null) &&
							(!distanceToBaseIsImportant || bi.IsCloseEnoughToBase(world, player, actorInfo, cell)),
						cell => baseBuilder.WallPlanner.OverlapsConstructionYardEnclosure(cell, bi));
					if (alternative != null)
					{
						baseBuilder.WallPlanner.LogReservationDecision(actorType,
							reservedFallback.Value, alternative.Value, false);
						return alternative;
					}
				}

				if (reservedFallback != null)
					baseBuilder.WallPlanner.LogReservationDecision(actorType,
						reservedFallback.Value, reservedFallback.Value, true);

				return reservedFallback;
			};

			var baseCenter = baseBuilder.GetRandomBaseCenter();

			switch (type)
			{
				case BuildingType.Defense:

					// Build near the closest enemy structure
					var closestEnemy = world.ActorsHavingTrait<Building>().Where(a => !a.Disposed && player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy)
						.ClosestTo(world.Map.CenterOfCell(baseBuilder.DefenseCenter));

					var targetCell = closestEnemy != null ? closestEnemy.Location : baseCenter;
					lastUsedDefenseLocation = findPos(lastUsedDefenseLocation ?? baseBuilder.DefenseCenter,
						targetCell, baseBuilder.Info.MinimumDefenseRadius, baseBuilder.Info.MaximumDefenseRadius);
					return lastUsedDefenseLocation;

				case BuildingType.Refinery:

					// Try and place the refinery near a resource field
					if (resourceLayer != null)
					{
						var nearbyResources = world.Map.FindTilesInAnnulus(baseCenter, baseBuilder.Info.MinBaseRadius, baseBuilder.Info.MaxBaseRadius)
							.Where(a => resourceLayer.GetResource(a).Type != null)
							.Shuffle(world.LocalRandom).Take(baseBuilder.Info.MaxResourceCellsToCheck);

						foreach (var r in nearbyResources)
						{
							var found = findPos(baseCenter, r, baseBuilder.Info.MinBaseRadius, baseBuilder.Info.MaxBaseRadius);
							if (found != null)
								return found;
						}
					}

					// Try and find a free spot somewhere else in the base
					return findPos(baseCenter, baseCenter, baseBuilder.Info.MinBaseRadius, baseBuilder.Info.MaxBaseRadius);

				case BuildingType.Building:
					return findPos(baseCenter, baseCenter, baseBuilder.Info.MinBaseRadius,
						distanceToBaseIsImportant ? baseBuilder.Info.MaxBaseRadius : world.Map.Grid.MaximumTileSearchRange);
			}

			// Can't find a build location
			return null;
		}
	}
}
