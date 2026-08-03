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

			waitTicks = active ? baseBuilder.Info.StructureProductionActiveDelay + randomFactor
				: baseBuilder.Info.StructureProductionInactiveDelay + randomFactor;
		}

		bool TickQueue(IBot bot, ProductionQueue queue)
		{
			var currentBuilding = queue.AllQueued().FirstOrDefault();

			// Waiting to build something
			if (currentBuilding == null && failCount < baseBuilder.Info.MaximumFailedPlacementAttempts)
			{
				var item = ChooseBuildingToBuild(queue);
				if (item == null)
					return false;

				bot.QueueOrder(Order.StartProduction(queue.Actor, item.Name, 1));
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
				else if (baseBuilder.WallPlanner != null && baseBuilder.WallPlanner.IsWallType(actorInfo.Name))
				{
					// Walls are laid out in lines by the wall planner rather than dropped on some
					// free cell in the base like an ordinary building.
					orderString = "LineBuild";
					location = baseBuilder.WallPlanner.TakeWallCell(actorInfo.Name);
				}
				else
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

				if (location == null)
				{
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

				return playerBuildings.Count(a => a.Info.Name == actor.Name) < baseBuilder.Info.BuildingLimits[actor.Name];
			});

			if (orderBy != null)
				return available.MaxByOrDefault(orderBy);

			return available.RandomOrDefault(world.LocalRandom);
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

			var opening = baseBuilder.OpeningBuilding(buildableThings);
			if (opening != null)
			{
				AIUtils.BotDebug("{0} decided to build {1}: parallel opening policy", queue.Actor.Owner, DisplayName(opening.Name));
				return opening;
			}

			if (baseBuilder.OpeningActive)
				return null;

			// Next is to build up a strong economy
			if (!baseBuilder.HasAdequateRefineryCount)
			{
				var refinery = GetProducibleBuilding(baseBuilder.Info.RefineryTypes, buildableThings);
				if (refinery != null && HasSufficientPowerForActor(refinery))
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

			// Make sure that we can spend as fast as we are earning
			var availableFunds = Math.Max(0, playerResources.Cash + playerResources.Resources);
			if (baseBuilder.Info.NewProductionCashThreshold > 0 && availableFunds > baseBuilder.Info.NewProductionCashThreshold)
			{
				var productionCandidates = buildableThings.Where(a => baseBuilder.Info.ProductionTypes.Contains(a.Name)).ToList();
				if (baseBuilder.AdaptiveProductionDebugLogging && productionCandidates.Count > 0)
					baseBuilder.LogAdaptiveProduction("{0} production-building scores (funds {1}): {2}", player, availableFunds,
						string.Join(", ", productionCandidates.OrderBy(a => a.Name).Select(a =>
						{
							var demand = baseBuilder.AdaptiveProductionBuildingDemand(a);
							var owned = playerBuildings.Count(b => b.Info.Name == a.Name);
							return FormattableString.Invariant(
								$"{DisplayName(a.Name)} demand={demand:0.00} owned={owned} score={AdaptiveWeighting.ProductionBuildingScore(demand, owned):0.00}");
						})));

				var production = GetProducibleBuilding(baseBuilder.Info.ProductionTypes, productionCandidates,
					a => AdaptiveWeighting.ProductionBuildingScore(baseBuilder.AdaptiveProductionBuildingDemand(a),
						playerBuildings.Count(b => b.Info.Name == a.Name)));
				if (production != null && HasSufficientPowerForActor(production))
				{
					var demand = baseBuilder.AdaptiveProductionBuildingDemand(production);
					var owned = playerBuildings.Count(b => b.Info.Name == production.Name);
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (production, adaptive demand {2:0.00})",
						queue.Actor.Owner, DisplayName(production.Name), demand);
					if (baseBuilder.AdaptiveProductionDebugLogging)
						baseBuilder.LogAdaptiveProduction(
							"{0} selected production building {1}: demand={2:0.00} owned={3} score={4:0.00} funds={5}",
							player, DisplayName(production.Name), demand, owned,
							AdaptiveWeighting.ProductionBuildingScore(demand, owned), availableFunds);

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
					&& !baseBuilder.WallPlanner.WantsToBuildWall(name, playerBuildings))
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
				var cells = world.Map.FindTilesInAnnulus(center, minRange, maxRange);

				// Sort by distance to target if we have one
				if (center != target)
					cells = cells.OrderBy(c => (c - target).LengthSquared);
				else
					cells = cells.Shuffle(world.LocalRandom);

				foreach (var cell in cells)
				{
					if (!world.CanPlaceBuilding(cell, actorInfo, bi, null))
						continue;

					if (distanceToBaseIsImportant && !bi.IsCloseEnoughToBase(world, player, actorInfo, cell))
						continue;

					return cell;
				}

				return null;
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
