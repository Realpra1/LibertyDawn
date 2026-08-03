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
	[Desc("Controls AI unit production.")]
	public class UnitBuilderBotModuleInfo : ConditionalTraitInfo
	{
		// TODO: Investigate whether this might the (or at least one) reason why bots occasionally get into a state of doing nothing.
		// Reason: If this is less than SquadSize, the bot might get stuck between not producing more units due to this,
		// but also not creating squads since there aren't enough idle units.
		[Desc("Only produce units as long as there are less than this amount of units idling inside the base.")]
		public readonly int IdleBaseUnitsMaximum = 12;

		[Desc("Production queues AI uses for producing units.")]
		public readonly HashSet<string> UnitQueues = new HashSet<string> { "Vehicle", "Infantry", "Plane", "Ship", "Aircraft" };

		[Desc("What units to the AI should build.", "What relative share of the total army must be this type of unit.")]
		public readonly Dictionary<string, int> UnitsToBuild = null;

		[Desc("What units should the AI have a maximum limit to train.")]
		public readonly Dictionary<string, int> UnitLimits = null;

		[Desc("Total live and queued mobile-unit ceiling. Positive values replace individual UnitLimits.")]
		public readonly int TotalUnitLimit = 0;

		[Desc("Unit types sharing the independent harvester ceiling.")]
		public readonly HashSet<string> HarvesterTypes = new HashSet<string>();

		[Desc("Total live and queued HarvesterTypes ceiling. Zero disables this ceiling.")]
		public readonly int HarvesterLimit = 0;

		[Desc("Write periodic live/queued unit-cap diagnostics to debug.log.")]
		public readonly bool UnitCapDebugLogging = false;

		[Desc("Ticks between unit-cap diagnostic snapshots.")]
		public readonly int UnitCapLogInterval = 1500;

		[Desc("When should the AI start train specific units.")]
		public readonly Dictionary<string, int> UnitDelays = null;

		[Desc("Replace the uniform random pick over buildable units with an adaptive, weighted-random draw over UnitsToBuild.",
			"Without this, UnitsToBuild's numbers only act as a whitelist while IdleBaseUnitsMaximum is high enough to always allow production.")]
		public readonly bool WeightedUnitSelection = false;

		[Desc("Unit types whose weight adapts based on their measured kills-value/losses-value ratio (see AdaptiveWeighting).",
			"Types left out - upgrades, downgrades, mcv, mhq - keep their authored weight forever, since they can never be scored by kills.")]
		public readonly HashSet<string> AdaptiveTypes = new HashSet<string>();

		[Desc("Unit types considered economy (harvesters). Never adaptively scored - a harvester dying to tiberium isn't underperforming -",
			"but bucketed into their own share of the weighted draw via EconomyCombatSplit and the harvester-floor economy gate.")]
		public readonly HashSet<string> EconomyTypes = new HashSet<string>();

		[Desc("Chance (0-1) of picking from EconomyTypes over the rest of the pool on a given build, when the economy gate hasn't forced it.")]
		public readonly float EconomyCombatSplit = 0.5f;

		[Desc("Combat samples (kills+losses) an adaptive type needs before its decayed score is fully trusted.",
			"Below this its adapted weight stays close to authored - this is what stops one early loss from tanking a type forever.")]
		public readonly int AdaptiveConfidenceSamples = 10;

		[Desc("Minimum share of the adaptive build-probability mass a single adaptive type may hold.")]
		public readonly float AdaptiveWeightFloor = 0.01f;

		[Desc("Maximum share of the adaptive build-probability mass a single adaptive type may hold.")]
		public readonly float AdaptiveWeightCeiling = 0.5f;

		[Desc("Learn mobile unit types produced by other playable humans but absent from UnitsToBuild.")]
		public readonly bool LearnHumanBuiltUnits = false;

		[Desc("Only learn from human players. Disable only for automated test scenarios.")]
		public readonly bool SampleHumanPlayersOnly = true;

		[Desc("Initial independent build chance (0-1) assigned to each learned unit type.")]
		public readonly float SampledUnitChance = 0.05f;

		[Desc("Maximum combined chance (0-1) reserved for all learned unit types.")]
		public readonly float MaximumSampledUnitChance = 0.5f;

		[Desc("Write learned-unit discoveries and selections to debug.log.")]
		public readonly bool UnitSamplingDebugLogging = false;

		[Desc("Ticks between learned-unit sampling heartbeat diagnostics.")]
		public readonly int UnitSamplingLogInterval = 1500;

		[Desc("Blend weight given to the most recent minute's kill/loss ratio when updating a type's decayed score.")]
		public readonly float AdaptationMinuteWeight = 0.5f;

		[Desc("Live harvesters (of any EconomyTypes) below this floor forces an economy pick regardless of the coin flip.")]
		public readonly int HarvesterFloor = 1;

		[Desc("Force-prioritize MCV production while fewer than this many construction yards are deployed.")]
		public readonly int McvPriorityThreshold = 2;

		[Desc("Actor type name of the deployed construction yard, used by the MCV priority check.")]
		public readonly string ConstructionYardActor = "fact";

		[Desc("Actor type name of the mobile construction vehicle, exempted from adaptive scoring and boosted by McvPriorityThreshold.")]
		public readonly string McvActor = "mcv";

		[Desc("Write adaptive unit/building demand scores and cross-queue production decisions to debug.log.")]
		public readonly bool AdaptiveProductionDebugLogging = false;

		[Desc("Minimum ticks between full adaptive candidate-table log entries. Selection decisions are always logged.")]
		public readonly int AdaptiveProductionLogInterval = 250;

		public override object Create(ActorInitializer init) { return new UnitBuilderBotModule(init.Self, this); }
	}

	public class UnitBuilderBotModule : ConditionalTrait<UnitBuilderBotModuleInfo>, IBotTick, IBotNotifyIdleBaseUnits, IBotRequestUnitProduction, IGameSaveTraitData
	{
		public const int FeedbackTime = 30; // ticks; = a bit over 1s. must be >= netlag.

		readonly World world;
		readonly Player player;

		readonly List<string> queuedBuildRequests = new List<string>();
		readonly HashSet<string> sampledUnitTypes = new HashSet<string>();

		IBotRequestPauseUnitProduction[] requestPause;
		PlayerResources playerResources;
		PlayerStatistics playerStats;
		int idleUnitCount;

		int ticks;

		// Refreshed once per FeedbackTime window (not per queue/BuildUnit call), keeping the
		// adaptive economy and unit-cap scans to one O(player actor count) pass per ~1.2s.
		int cachedDeployedConstructionYards;
		int cachedLiveHarvesters;
		int cachedCommittedUnits;
		int cachedCommittedHarvesters;
		int nextUnitCapLogTick;
		int nextUnitSamplingLogTick;

		// Player-level income/spend over the current adaptive rollover window; used only by the
		// economy gate. Not game-save persisted - after a load the very first window's reading is a
		// one-off no-op (delta computed against zero), which self-corrects at the next rollover.
		int lastWindowEarned;
		int lastWindowSpent;
		int incomeThisWindow;
		int spendThisWindow;
		int nextAdaptiveProductionLogTick;
		bool adaptiveInitializationLogged;

		sealed class UnitBuildOffer
		{
			public ProductionQueue Queue;
			public ActorInfo Unit;
			public string Category;
			public double Score;
			public int Cost;
		}

		public UnitBuilderBotModule(Actor self, UnitBuilderBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			requestPause = self.Owner.PlayerActor.TraitsImplementing<IBotRequestPauseUnitProduction>().ToArray();
			playerResources = self.Owner.PlayerActor.Trait<PlayerResources>();
			playerStats = self.Owner.PlayerActor.Trait<PlayerStatistics>();
		}

		void IBotNotifyIdleBaseUnits.UpdatedIdleBaseUnits(List<Actor> idleUnits)
		{
			idleUnitCount = idleUnits.Count;
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (Info.AdaptiveProductionDebugLogging && !adaptiveInitializationLogged)
			{
				adaptiveInitializationLogged = true;
				LogAdaptiveProduction("{0} module active for queues: {1}", player,
					string.Join(", ", Info.UnitQueues.OrderBy(q => q, StringComparer.Ordinal)));
			}

			var pauseRandomProduction = requestPause.Any(rp => rp.PauseUnitProduction);
			if (pauseRandomProduction && queuedBuildRequests.Count == 0)
				return;
			ticks++;

			if (ticks % FeedbackTime == 0)
			{
				RefreshProductionCounts();
				DiscoverSampledUnitTypes();
				if (Info.WeightedUnitSelection)
				{
					MaybeRollAdaptiveWindow();
				}

				MaybeLogUnitCapacity();

				var buildRequest = queuedBuildRequests.FirstOrDefault();
				if (buildRequest != null)
				{
					BuildUnit(bot, buildRequest);
					queuedBuildRequests.Remove(buildRequest);
					return;
				}

				if (!pauseRandomProduction)
				{
					if (Info.WeightedUnitSelection && Info.UnitsToBuild != null &&
						idleUnitCount < Info.IdleBaseUnitsMaximum)
						BuildAdaptiveUnitsAcrossQueues(bot);
					else
						foreach (var q in Info.UnitQueues)
							BuildUnit(bot, q, idleUnitCount < Info.IdleBaseUnitsMaximum);
				}
			}
		}

		// BotDebug is read by humans watching a match, not tuned against by other code, so trade the
		// exact yaml key for something legible: "Minigunner (e1)" instead of just "e1".
		string DisplayName(string type)
		{
			var tooltip = world.Map.Rules.Actors[type].TraitInfoOrDefault<TooltipInfo>();
			return !string.IsNullOrEmpty(tooltip?.Name) ? $"{tooltip.Name} ({type})" : type;
		}

		void LogAdaptiveProduction(string format, params object[] args)
		{
			AIUtils.BotDebug(format, args);
			Log.Write("debug", "AI adaptive production: " + format, args);
		}

		void RefreshProductionCounts()
		{
			cachedDeployedConstructionYards = 0;
			cachedLiveHarvesters = 0;
			cachedCommittedUnits = 0;
			cachedCommittedHarvesters = 0;

			foreach (var a in world.Actors)
			{
				if (a.Owner != player || a.IsDead)
					continue;

				if (a.Info.Name == Info.ConstructionYardActor)
					cachedDeployedConstructionYards++;
				else if (Info.EconomyTypes.Contains(a.Info.Name))
					cachedLiveHarvesters++;

				if (CountsTowardUnitLimit(a.Info))
					cachedCommittedUnits++;

				if (Info.HarvesterTypes.Contains(a.Info.Name))
					cachedCommittedHarvesters++;
			}

			foreach (var queue in world.ActorsWithTrait<ProductionQueue>().Where(q => q.Actor.Owner == player))
				foreach (var item in queue.Trait.AllQueued())
					if (world.Map.Rules.Actors.TryGetValue(item.Item, out var actorInfo))
					{
						if (CountsTowardUnitLimit(actorInfo))
							cachedCommittedUnits++;

						if (Info.HarvesterTypes.Contains(actorInfo.Name))
							cachedCommittedHarvesters++;
					}
		}

		void MaybeLogUnitCapacity()
		{
			if (!Info.UnitCapDebugLogging || world.WorldTick < nextUnitCapLogTick)
				return;

			nextUnitCapLogTick = world.WorldTick + Math.Max(1, Info.UnitCapLogInterval);
			Log.Write("debug", "AI unit capacity: {0} mobile={1}/{2}, harvesters={3}/{4} (live plus queued).",
				player, cachedCommittedUnits, Info.TotalUnitLimit, cachedCommittedHarvesters, Info.HarvesterLimit);
		}

		void MaybeRollAdaptiveWindow()
		{
			if (world.WorldTick < playerStats.NextAdaptiveRolloverTick)
				return;

			var earned = playerResources.Earned;
			var spent = playerResources.Spent;
			incomeThisWindow = earned - lastWindowEarned;
			spendThisWindow = spent - lastWindowSpent;
			lastWindowEarned = earned;
			lastWindowSpent = spent;

			foreach (var kv in playerStats.AdaptiveStats.OrderBy(kv => kv.Key, StringComparer.Ordinal))
			{
				var stats = kv.Value;
				var previous = stats.DecayedScore;
				stats.DecayedScore = AdaptiveWeighting.DecayScore(previous, stats.MinuteKillsValue, stats.MinuteLossesValue, Info.AdaptationMinuteWeight);
				stats.MinuteKillsValue = 0;
				stats.MinuteLossesValue = 0;

				if (Math.Abs(stats.DecayedScore - previous) > 0.05)
				{
					var args = new object[]
					{
						player, DisplayName(kv.Key), previous, stats.DecayedScore,
						stats.BuiltCount, stats.KillsCount, stats.LossesCount
					};
					if (Info.AdaptiveProductionDebugLogging)
						LogAdaptiveProduction("{0} score for {1}: {2:0.00} -> {3:0.00} (built {4}, killed {5}, lost {6})", args);
					else
						AIUtils.BotDebug("{0} adaptive score for {1}: {2:0.00} -> {3:0.00} (built {4}, killed {5}, lost {6})", args);
				}
			}

			if (Info.AdaptiveTypes.Count > 0)
			{
				var table = Info.AdaptiveTypes.OrderBy(t => t, StringComparer.Ordinal).Select(t =>
				{
					var stats = playerStats.AdaptiveStats[t];
					var confidence = AdaptiveWeighting.Confidence(stats.KillsCount + stats.LossesCount, Info.AdaptiveConfidenceSamples);
					var authored = Info.UnitsToBuild.TryGetValue(t, out var w) ? w : 0;
					var adapted = AdaptiveWeighting.AdaptedWeight(authored, stats.DecayedScore, confidence);
					return FormattableString.Invariant($"{DisplayName(t)}={adapted:0.0}(authored {authored}, score {stats.DecayedScore:0.00}, conf {confidence:0.00})");
				});
				if (Info.AdaptiveProductionDebugLogging)
					LogAdaptiveProduction("{0} weights: {1}", player, string.Join(", ", table));
				else
					AIUtils.BotDebug("{0} adaptive weights: {1}", player, string.Join(", ", table));
			}

			var ticksPerWindow = Math.Max(1, 60000 / world.Timestep);
			playerStats.NextAdaptiveRolloverTick = world.WorldTick + ticksPerWindow;
		}

		void IBotRequestUnitProduction.RequestUnitProduction(IBot bot, string requestedActor)
		{
			queuedBuildRequests.Add(requestedActor);
		}

		int IBotRequestUnitProduction.RequestedProductionCount(IBot bot, string requestedActor)
		{
			return queuedBuildRequests.Count(r => r == requestedActor);
		}

		void BuildUnit(IBot bot, string category, bool buildRandom)
		{
			// Pick a free queue
			var queue = AIUtils.FindQueues(player, category).FirstOrDefault(q => !q.AllQueued().Any());
			if (queue == null)
				return;

			var unit = buildRandom ?
				ChooseRandomUnitToBuild(queue) :
				ChooseUnitToBuild(queue);

			if (unit == null)
				return;

			var name = unit.Name;

			if (Info.UnitsToBuild != null && !Info.UnitsToBuild.ContainsKey(name) && !sampledUnitTypes.Contains(name))
				return;

			if (Info.UnitDelays != null &&
				Info.UnitDelays.ContainsKey(name) &&
				Info.UnitDelays[name] > world.WorldTick)
				return;

			if (Info.TotalUnitLimit <= 0 && Info.UnitLimits != null &&
				Info.UnitLimits.ContainsKey(name) &&
				world.Actors.Count(a => a.Owner == player && a.Info.Name == name) >= Info.UnitLimits[name])
				return;

			var queueAmount = System.Math.Max(1,
				world.ActorsHavingTrait<Building>().Where(a => a.Owner == player).Count() / 20);
			queueAmount = AllowedQueueAmount(unit, queueAmount);
			if (queueAmount <= 0)
				return;

			bot.QueueOrder(Order.StartProduction(queue.Actor, name, queueAmount));
			RecordQueued(unit, queueAmount);
			if (sampledUnitTypes.Contains(name))
				LogSampling("{0} queued learned unit {1}: amount={2}, queue={3}.",
					player, DisplayName(name), queueAmount, queue.Info.Type);
		}

		void BuildAdaptiveUnitsAcrossQueues(IBot bot)
		{
			var offers = new List<UnitBuildOffer>();
			var seenQueues = new HashSet<ProductionQueue>();
			foreach (var category in Info.UnitQueues.OrderBy(q => q, StringComparer.Ordinal))
				foreach (var queue in AIUtils.FindQueues(player, category))
				{
					if (!seenQueues.Add(queue) || queue.AllQueued().Any())
						continue;

					var unit = ChooseRandomUnitToBuild(queue);
					if (unit == null || !CanBuildCandidateUnit(unit))
						continue;

					offers.Add(new UnitBuildOffer
					{
						Queue = queue,
						Unit = unit,
						Category = category,
						Score = AdaptiveProductionScore(unit.Name),
						Cost = Math.Max(0, unit.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0)
					});
				}

			var logTable = Info.AdaptiveProductionDebugLogging && world.WorldTick >= nextAdaptiveProductionLogTick;
			if (logTable)
				nextAdaptiveProductionLogTick = world.WorldTick + Math.Max(1, Info.AdaptiveProductionLogInterval);

			if (offers.Count == 0)
			{
				if (logTable)
					LogAdaptiveProduction("{0} has no candidates: {1}", player,
						string.Join(", ", Info.UnitQueues.OrderBy(q => q, StringComparer.Ordinal).Select(category =>
						{
							var queues = AIUtils.FindQueues(player, category).ToList();
							return $"{category} queues={queues.Count} free={queues.Count(q => !q.AllQueued().Any())}";
						})));

				return;
			}

			var budget = Math.Max(0, playerResources.Cash + playerResources.Resources);
			var selected = new HashSet<int>(AdaptiveWeighting.SelectAffordableOffers(
				offers.Select(o => o.Score).ToArray(), offers.Select(o => o.Cost).ToArray(), budget));
			if (logTable)
			{
				LogAdaptiveProduction("{0} candidates (budget {1}): {2}", player, budget,
					string.Join(", ", offers.Select((o, i) => FormattableString.Invariant(
						$"{o.Category}:{DisplayName(o.Unit.Name)} score={o.Score:0.00} cost={o.Cost} {(selected.Contains(i) ? "selected" : "deferred")}"))));
			}

			foreach (var index in selected)
			{
				var offer = offers[index];
				if (AllowedQueueAmount(offer.Unit, 1) <= 0)
					continue;

				bot.QueueOrder(Order.StartProduction(offer.Queue.Actor, offer.Unit.Name, 1));
				RecordQueued(offer.Unit, 1);
				if (sampledUnitTypes.Contains(offer.Unit.Name))
					LogSampling("{0} queued learned unit {1}: amount=1, queue={2}.",
						player, DisplayName(offer.Unit.Name), offer.Queue.Info.Type);

				if (Info.AdaptiveProductionDebugLogging)
					LogAdaptiveProduction("{0} selected {1} on {2}: score={3:0.00} cost={4} budget={5}",
						player, DisplayName(offer.Unit.Name), offer.Category, offer.Score, offer.Cost, budget);
			}
		}

		bool CanBuildCandidateUnit(ActorInfo unit)
		{
			var name = unit.Name;
			if (!Info.UnitsToBuild.ContainsKey(name) && !sampledUnitTypes.Contains(name))
				return false;

			if (Info.UnitDelays != null && Info.UnitDelays.TryGetValue(name, out var delay) && delay > world.WorldTick)
				return false;

			return AllowedQueueAmount(unit, 1) > 0 &&
				(Info.UnitLimits == null || !Info.UnitLimits.TryGetValue(name, out var limit) ||
				world.Actors.Count(a => a.Owner == player && a.Info.Name == name) < limit);
		}

		double AdaptiveProductionScore(string type)
		{
			if (sampledUnitTypes.Contains(type))
				return SampledChance(world.Map.Rules.Actors[type]) * Info.UnitsToBuild.Values.Sum();

			if (type == Info.McvActor && AdaptiveWeighting.ShouldForceMcv(
				cachedDeployedConstructionYards, Info.McvPriorityThreshold))
				return 1000000;

			if (Info.EconomyTypes.Contains(type) && AdaptiveWeighting.ShouldForceEconomy(
				incomeThisWindow, spendThisWindow, cachedLiveHarvesters, Info.HarvesterFloor))
				return 500000;

			return AdaptiveTypeWeight(type);
		}

		internal double ProductionBuildingDemand(ActorInfo building)
		{
			if (!Info.WeightedUnitSelection || Info.UnitsToBuild == null)
				return 0;

			var productionTypes = new HashSet<string>(building.TraitInfos<ProductionInfo>()
				.SelectMany(p => p.Produces), StringComparer.OrdinalIgnoreCase);
			if (productionTypes.Count == 0)
				return 0;

			return Info.UnitsToBuild.Keys.Concat(sampledUnitTypes).Distinct().Where(world.Map.Rules.Actors.ContainsKey)
				.Where(t => world.Map.Rules.Actors[t].TraitInfoOrDefault<BuildableInfo>()?.Queue
					.Any(productionTypes.Contains) == true)
				.Select(AdaptiveProductionScore).DefaultIfEmpty(0).Max();
		}

		// In cases where we want to build a specific unit but don't know the queue name (because there's more than one possibility)
		void BuildUnit(IBot bot, string name)
		{
			var actorInfo = world.Map.Rules.Actors[name];
			if (actorInfo == null)
				return;

			var buildableInfo = actorInfo.TraitInfoOrDefault<BuildableInfo>();
			if (buildableInfo == null)
				return;

			ProductionQueue queue = null;
			foreach (var pq in buildableInfo.Queue)
			{
				queue = AIUtils.FindQueues(player, pq).FirstOrDefault(q => !q.AllQueued().Any());
				if (queue != null)
					break;
			}

			if (queue != null && AllowedQueueAmount(actorInfo, 1) > 0)
			{
				bot.QueueOrder(Order.StartProduction(queue.Actor, name, 1));
				RecordQueued(actorInfo, 1);
				AIUtils.BotDebug("{0} decided to build {1} (external request)", queue.Actor.Owner, DisplayName(name));
			}
		}

		ActorInfo ChooseRandomUnitToBuild(ProductionQueue queue)
		{
			var buildableThings = queue.BuildableItems().Where(CanQueue).ToArray();
			if (!buildableThings.Any())
				return null;

			var sampled = buildableThings.Where(a => sampledUnitTypes.Contains(a.Name)).ToArray();
			var sampledPick = ChooseSampledUnit(sampled);
			if (sampledPick != null)
				return HasAdequateAirUnitReloadBuildings(sampledPick) ? sampledPick : null;

			var configured = Info.UnitsToBuild == null ? buildableThings :
				buildableThings.Where(a => Info.UnitsToBuild.ContainsKey(a.Name)).ToArray();
			if (configured.Length == 0)
				return sampled.Length > 0 ? sampled.Random(world.LocalRandom) : null;

			if (!Info.WeightedUnitSelection || Info.UnitsToBuild == null)
			{
				var unit = configured.Random(world.LocalRandom);
				return HasAdequateAirUnitReloadBuildings(unit) ? unit : null;
			}

			return ChooseWeightedUnitToBuild(configured.ToList());
		}

		ActorInfo ChooseWeightedUnitToBuild(List<ActorInfo> buildable)
		{
			// MCV is expansion, not a fighting unit that can be scored by kills - prioritize it over
			// everything else while we don't have enough construction yards deployed.
			if (Info.UnitsToBuild.ContainsKey(Info.McvActor) && buildable.Any(b => b.Name == Info.McvActor) &&
				AdaptiveWeighting.ShouldForceMcv(cachedDeployedConstructionYards, Info.McvPriorityThreshold))
			{
				var mcv = world.Map.Rules.Actors[Info.McvActor];
				return HasAdequateAirUnitReloadBuildings(mcv) ? mcv : null;
			}

			var buildableNames = new HashSet<string>(buildable.Select(b => b.Name));

			var economyPool = Info.EconomyTypes
				.Where(t => buildableNames.Contains(t) && Info.UnitsToBuild.ContainsKey(t))
				.ToDictionary(t => t, t => (double)Info.UnitsToBuild[t]);

			var combatPool = Info.UnitsToBuild.Keys
				.Where(t => buildableNames.Contains(t) && !Info.EconomyTypes.Contains(t))
				.ToDictionary(t => t, AdaptiveTypeWeight);

			var forceEconomy = economyPool.Count > 0 &&
				AdaptiveWeighting.ShouldForceEconomy(incomeThisWindow, spendThisWindow, cachedLiveHarvesters, Info.HarvesterFloor);

			var pickEconomy = economyPool.Count > 0 &&
				(forceEconomy || combatPool.Count == 0 || world.LocalRandom.NextFloat() < Info.EconomyCombatSplit);

			var pool = pickEconomy ? economyPool : combatPool;
			if (pool.Count == 0)
			{
				pickEconomy = !pickEconomy;
				pool = pickEconomy ? economyPool : combatPool;
			}

			if (pool.Count == 0)
				return null;

			// Economy types are never clamped - they aren't scored, so there's nothing to keep off a floor
			// or ceiling. Only the adaptive combat pool gets the [floor, ceiling] build-share guarantee.
			var floor = pickEconomy ? 0 : Info.AdaptiveWeightFloor;
			var ceiling = pickEconomy ? 1 : Info.AdaptiveWeightCeiling;
			var shares = AdaptiveWeighting.ClampedShares(pool, floor, ceiling);

			var picked = AdaptiveWeighting.WeightedPick(shares, world.LocalRandom.NextFloat());
			if (picked == null)
				return null;

			var actor = world.Map.Rules.Actors[picked];
			return HasAdequateAirUnitReloadBuildings(actor) ? actor : null;
		}

		double AdaptiveTypeWeight(string type)
		{
			var authored = Info.UnitsToBuild[type];
			if (!Info.AdaptiveTypes.Contains(type))
				return authored;

			var stats = playerStats.AdaptiveStats[type];
			var confidence = AdaptiveWeighting.Confidence(stats.KillsCount + stats.LossesCount, Info.AdaptiveConfidenceSamples);
			return AdaptiveWeighting.AdaptedWeight(authored, stats.DecayedScore, confidence);
		}

		ActorInfo ChooseUnitToBuild(ProductionQueue queue)
		{
			var buildableThings = queue.BuildableItems().Where(CanQueue).ToArray();
			if (!buildableThings.Any())
				return null;

			var sampled = buildableThings.Where(a => sampledUnitTypes.Contains(a.Name)).ToArray();
			var sampledPick = ChooseSampledUnit(sampled);
			if (sampledPick != null)
				return HasAdequateAirUnitReloadBuildings(sampledPick) ? sampledPick : null;

			var myUnits = player.World
				.ActorsHavingTrait<IPositionable>()
				.Where(a => a.Owner == player)
				.Select(a => a.Info.Name).ToList();

			foreach (var unit in Info.UnitsToBuild.Shuffle(world.LocalRandom))
				if (buildableThings.Any(b => b.Name == unit.Key))
					if (myUnits.Count(a => a == unit.Key) * 100 < unit.Value * myUnits.Count)
						if (HasAdequateAirUnitReloadBuildings(world.Map.Rules.Actors[unit.Key]))
							return world.Map.Rules.Actors[unit.Key];

			return null;
		}

		bool CanQueue(ActorInfo actorInfo)
		{
			return AllowedQueueAmount(actorInfo, 1) > 0;
		}

		int AllowedQueueAmount(ActorInfo actorInfo, int requested)
		{
			return UnitCapPolicy.AllowedQueueAmount(requested, cachedCommittedUnits, Info.TotalUnitLimit,
				Info.HarvesterTypes.Contains(actorInfo.Name), cachedCommittedHarvesters, Info.HarvesterLimit,
				CountsTowardUnitLimit(actorInfo));
		}

		void RecordQueued(ActorInfo actorInfo, int amount)
		{
			if (CountsTowardUnitLimit(actorInfo))
				cachedCommittedUnits += amount;

			if (Info.HarvesterTypes.Contains(actorInfo.Name))
				cachedCommittedHarvesters += amount;
		}

		static bool CountsTowardUnitLimit(ActorInfo actorInfo)
		{
			return actorInfo.HasTraitInfo<MobileInfo>() || actorInfo.HasTraitInfo<AircraftInfo>();
		}

		void DiscoverSampledUnitTypes()
		{
			if (!Info.LearnHumanBuiltUnits || Info.UnitsToBuild == null)
				return;

			var sources = world.Players.Where(p => p != player && p.Playable && !p.NonCombatant &&
				(!Info.SampleHumanPlayersOnly || !p.IsBot)).ToArray();
			foreach (var source in sources)
			{
				var statistics = source.PlayerActor.TraitOrDefault<PlayerStatistics>();
				if (statistics == null)
					continue;

				foreach (var sample in statistics.AdaptiveStats.Where(kv => kv.Value.BuiltCount > 0))
				{
					if (Info.UnitsToBuild.ContainsKey(sample.Key) || sampledUnitTypes.Contains(sample.Key) ||
						!world.Map.Rules.Actors.TryGetValue(sample.Key, out var actorInfo))
						continue;

					var buildable = actorInfo.TraitInfoOrDefault<BuildableInfo>();
					var queueCompatible = buildable != null && buildable.Queue.Any(Info.UnitQueues.Contains);
					var mobile = actorInfo.HasTraitInfo<MobileInfo>() || actorInfo.HasTraitInfo<AircraftInfo>();
					if (!PlayerUnitSamplingPolicy.CanLearn(source.IsBot, Info.SampleHumanPlayersOnly,
						source.Playable, source.NonCombatant, queueCompatible, mobile, sample.Value.BuiltCount))
						continue;

					sampledUnitTypes.Add(sample.Key);
					LogSampling("{0} learned {1} from {2}: built={3}, base-chance={4:P0}.",
						player, DisplayName(sample.Key), source, sample.Value.BuiltCount, Info.SampledUnitChance);
				}
			}

			if (Info.UnitSamplingDebugLogging && world.WorldTick >= nextUnitSamplingLogTick)
			{
				nextUnitSamplingLogTick = world.WorldTick + Math.Max(1, Info.UnitSamplingLogInterval);
				Log.Write("debug", "AI unit sampling: {0} scan: eligible-sources={1}, learned={2}, own-built={3}.",
					player, sources.Length, sampledUnitTypes.Count,
					playerStats.AdaptiveStats.Sum(kv => kv.Value.BuiltCount));
			}
		}

		ActorInfo ChooseSampledUnit(ActorInfo[] buildableSamples)
		{
			if (buildableSamples.Length == 0)
				return null;

			var chances = buildableSamples.ToDictionary(a => a.Name, SampledChance);
			var picked = PlayerUnitSamplingPolicy.Pick(chances, Info.MaximumSampledUnitChance,
				world.LocalRandom.NextFloat());
			if (picked == null)
				return null;

			LogSampling("{0} selected learned unit {1}: adjusted-chance={2:P1}, learned-types={3}.",
				player, DisplayName(picked), chances[picked], sampledUnitTypes.Count);
			return world.Map.Rules.Actors[picked];
		}

		double SampledChance(ActorInfo actorInfo)
		{
			if (!Info.WeightedUnitSelection)
				return Info.SampledUnitChance;

			var stats = playerStats.AdaptiveStats[actorInfo.Name];
			var confidence = AdaptiveWeighting.Confidence(stats.KillsCount + stats.LossesCount, Info.AdaptiveConfidenceSamples);
			return AdaptiveWeighting.AdaptedWeight(Info.SampledUnitChance, stats.DecayedScore, confidence);
		}

		void LogSampling(string format, params object[] args)
		{
			AIUtils.BotDebug(format, args);
			if (Info.UnitSamplingDebugLogging)
				Log.Write("debug", "AI unit sampling: " + format, args);
		}

		// For mods like RA (number of RearmActors must match the number of aircraft)
		bool HasAdequateAirUnitReloadBuildings(ActorInfo actorInfo)
		{
			var aircraftInfo = actorInfo.TraitInfoOrDefault<AircraftInfo>();
			if (aircraftInfo == null)
				return true;

			// If actor isn't Rearmable, it doesn't need a RearmActor to reload
			var rearmableInfo = actorInfo.TraitInfoOrDefault<RearmableInfo>();
			if (rearmableInfo == null)
				return true;

			var countOwnAir = AIUtils.CountActorsWithTrait<IPositionable>(actorInfo.Name, player);
			var countBuildings = rearmableInfo.RearmActors.Sum(b => AIUtils.CountActorsWithTrait<Building>(b, player));
			if (countOwnAir >= countBuildings)
				return false;

			return true;
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>()
			{
				new MiniYamlNode("QueuedBuildRequests", FieldSaver.FormatValue(queuedBuildRequests.ToArray())),
				new MiniYamlNode("IdleUnitCount", FieldSaver.FormatValue(idleUnitCount)),
				new MiniYamlNode("SampledUnitTypes", FieldSaver.FormatValue(sampledUnitTypes.OrderBy(t => t, StringComparer.Ordinal).ToArray()))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var queuedBuildRequestsNode = data.FirstOrDefault(n => n.Key == "QueuedBuildRequests");
			if (queuedBuildRequestsNode != null)
			{
				queuedBuildRequests.Clear();
				queuedBuildRequests.AddRange(FieldLoader.GetValue<string[]>("QueuedBuildRequests", queuedBuildRequestsNode.Value.Value));
			}

			var idleUnitCountNode = data.FirstOrDefault(n => n.Key == "IdleUnitCount");
			if (idleUnitCountNode != null)
				idleUnitCount = FieldLoader.GetValue<int>("IdleUnitCount", idleUnitCountNode.Value.Value);

			var sampledNode = data.FirstOrDefault(n => n.Key == "SampledUnitTypes");
			if (sampledNode != null)
			{
				sampledUnitTypes.Clear();
				sampledUnitTypes.UnionWith(FieldLoader.GetValue<string[]>("SampledUnitTypes", sampledNode.Value.Value));
			}
		}
	}
}
