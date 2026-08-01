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

		[Desc("Unit types controlled by another bot module and excluded from random weighted production.")]
		public readonly HashSet<string> ExternallyManagedTypes = new HashSet<string>();

		[Desc("Chance (0-1) of picking from EconomyTypes over the rest of the pool on a given build, when the economy gate hasn't forced it.")]
		public readonly float EconomyCombatSplit = 0.5f;

		[Desc("Combat samples (kills+losses) an adaptive type needs before its decayed score is fully trusted.",
			"Below this its adapted weight stays close to authored - this is what stops one early loss from tanking a type forever.")]
		public readonly int AdaptiveConfidenceSamples = 10;

		[Desc("Minimum share of the adaptive build-probability mass a single adaptive type may hold.")]
		public readonly float AdaptiveWeightFloor = 0.01f;

		[Desc("Maximum share of the adaptive build-probability mass a single adaptive type may hold.")]
		public readonly float AdaptiveWeightCeiling = 0.5f;

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

		public override object Create(ActorInitializer init) { return new UnitBuilderBotModule(init.Self, this); }
	}

	public class UnitBuilderBotModule : ConditionalTrait<UnitBuilderBotModuleInfo>, IBotTick, IBotNotifyIdleBaseUnits,
		IBotRequestUnitProduction, IBotRequestTaggedUnitProduction, IGameSaveTraitData
	{
		public const int FeedbackTime = 30; // ticks; = a bit over 1s. must be >= netlag.

		readonly World world;
		readonly Player player;

		readonly List<string> queuedBuildRequests = new List<string>();
		readonly List<string> queuedBuildRequestTags = new List<string>();

		IBotRequestPauseUnitProduction[] requestPause;
		PlayerResources playerResources;
		PlayerStatistics playerStats;
		int idleUnitCount;

		int ticks;

		// Refreshed once per FeedbackTime window (not per queue/BuildUnit call) so enabling
		// WeightedUnitSelection costs one extra O(player actor count) scan per ~1.2s, not five.
		int cachedDeployedConstructionYards;
		int cachedLiveHarvesters;

		// Player-level income/spend over the current adaptive rollover window; used only by the
		// economy gate. Not game-save persisted - after a load the very first window's reading is a
		// one-off no-op (delta computed against zero), which self-corrects at the next rollover.
		int lastWindowEarned;
		int lastWindowSpent;
		int incomeThisWindow;
		int spendThisWindow;

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
			if (requestPause.Any(rp => rp.PauseUnitProduction))
				return;

			ticks++;

			if (ticks % FeedbackTime == 0)
			{
				if (Info.WeightedUnitSelection)
				{
					RefreshAdaptiveCounts();
					MaybeRollAdaptiveWindow();
				}

				var buildRequest = queuedBuildRequests.FirstOrDefault();
				if (buildRequest != null)
				{
					var accepted = BuildUnit(bot, buildRequest);

					// Preserve tagged policy requests until a production queue actually accepts them.
					// Untagged legacy requests retain their one-shot behavior so an unavailable actor
					// cannot block the existing MCV/harvester request queue indefinitely.
					if (accepted || string.IsNullOrEmpty(queuedBuildRequestTags[0]))
					{
						queuedBuildRequests.RemoveAt(0);
						queuedBuildRequestTags.RemoveAt(0);
					}
				}

				foreach (var q in Info.UnitQueues)
					BuildUnit(bot, q, idleUnitCount < Info.IdleBaseUnitsMaximum);
			}
		}

		// BotDebug is read by humans watching a match, not tuned against by other code, so trade the
		// exact yaml key for something legible: "Minigunner (e1)" instead of just "e1".
		string DisplayName(string type)
		{
			var tooltip = world.Map.Rules.Actors[type].TraitInfoOrDefault<TooltipInfo>();
			return !string.IsNullOrEmpty(tooltip?.Name) ? $"{tooltip.Name} ({type})" : type;
		}

		void RefreshAdaptiveCounts()
		{
			cachedDeployedConstructionYards = 0;
			cachedLiveHarvesters = 0;

			foreach (var a in world.Actors)
			{
				if (a.Owner != player || a.IsDead)
					continue;

				if (a.Info.Name == Info.ConstructionYardActor)
					cachedDeployedConstructionYards++;
				else if (Info.EconomyTypes.Contains(a.Info.Name))
					cachedLiveHarvesters++;
			}
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
					AIUtils.BotDebug("{0} adaptive score for {1}: {2:0.00} -> {3:0.00} (built {4}, killed {5}, lost {6})",
						player, DisplayName(kv.Key), previous, stats.DecayedScore, stats.BuiltCount, stats.KillsCount, stats.LossesCount);
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
				AIUtils.BotDebug("{0} adaptive weights: {1}", player, string.Join(", ", table));
			}

			var ticksPerWindow = Math.Max(1, 60000 / world.Timestep);
			playerStats.NextAdaptiveRolloverTick = world.WorldTick + ticksPerWindow;
		}

		void IBotRequestUnitProduction.RequestUnitProduction(IBot bot, string requestedActor)
		{
			queuedBuildRequests.Add(requestedActor);
			queuedBuildRequestTags.Add(string.Empty);
		}

		int IBotRequestUnitProduction.RequestedProductionCount(IBot bot, string requestedActor)
		{
			return queuedBuildRequests.Count(r => r == requestedActor);
		}

		void IBotRequestTaggedUnitProduction.RequestUnitProduction(IBot bot, string requestedActor, string requestTag, int count)
		{
			if (string.IsNullOrEmpty(requestTag))
				throw new ArgumentException("Tagged production requests require a non-empty tag.", nameof(requestTag));

			for (var i = 0; i < Math.Max(0, count); i++)
			{
				queuedBuildRequests.Add(requestedActor);
				queuedBuildRequestTags.Add(requestTag);
			}
		}

		int IBotRequestTaggedUnitProduction.RequestedProductionCount(IBot bot, string requestTag)
		{
			return queuedBuildRequestTags.Count(t => t == requestTag);
		}

		void IBotRequestTaggedUnitProduction.CancelUnitProduction(IBot bot, string requestTag)
		{
			for (var i = queuedBuildRequestTags.Count - 1; i >= 0; i--)
			{
				if (queuedBuildRequestTags[i] != requestTag)
					continue;

				queuedBuildRequestTags.RemoveAt(i);
				queuedBuildRequests.RemoveAt(i);
			}
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

			if (Info.UnitsToBuild != null && !Info.UnitsToBuild.ContainsKey(name))
				return;

			if (Info.UnitDelays != null &&
				Info.UnitDelays.ContainsKey(name) &&
				Info.UnitDelays[name] > world.WorldTick)
				return;

			if (Info.UnitLimits != null &&
				Info.UnitLimits.ContainsKey(name) &&
				world.Actors.Count(a => a.Owner == player && a.Info.Name == name) >= Info.UnitLimits[name])
				return;

			int queueAmount = System.Math.Max(1,
				world.ActorsHavingTrait<Building>().Where(a => a.Owner == player).Count() / 20);

			bot.QueueOrder(Order.StartProduction(queue.Actor, name, queueAmount));
		}

		// In cases where we want to build a specific unit but don't know the queue name (because there's more than one possibility)
		bool BuildUnit(IBot bot, string name)
		{
			var actorInfo = world.Map.Rules.Actors[name];
			if (actorInfo == null)
				return false;

			var buildableInfo = actorInfo.TraitInfoOrDefault<BuildableInfo>();
			if (buildableInfo == null)
				return false;

			ProductionQueue queue = null;
			foreach (var pq in buildableInfo.Queue)
			{
				queue = AIUtils.FindQueues(player, pq).FirstOrDefault(q => !q.AllQueued().Any());
				if (queue != null)
					break;
			}

			if (queue != null)
			{
				bot.QueueOrder(Order.StartProduction(queue.Actor, name, 1));
				AIUtils.BotDebug("{0} decided to build {1} (external request)", queue.Actor.Owner, DisplayName(name));
				return true;
			}

			return false;
		}

		ActorInfo ChooseRandomUnitToBuild(ProductionQueue queue)
		{
			var buildableThings = queue.BuildableItems()
				.Where(a => !Info.ExternallyManagedTypes.Contains(a.Name)).ToArray();
			if (!buildableThings.Any())
				return null;

			if (!Info.WeightedUnitSelection || Info.UnitsToBuild == null)
			{
				var unit = buildableThings.Random(world.LocalRandom);
				return HasAdequateAirUnitReloadBuildings(unit) ? unit : null;
			}

			return ChooseWeightedUnitToBuild(buildableThings.ToList());
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
				.Where(t => buildableNames.Contains(t) && Info.UnitsToBuild.ContainsKey(t) && !Info.ExternallyManagedTypes.Contains(t))
				.ToDictionary(t => t, t => (double)Info.UnitsToBuild[t]);

			var combatPool = Info.UnitsToBuild.Keys
				.Where(t => buildableNames.Contains(t) && !Info.EconomyTypes.Contains(t) && !Info.ExternallyManagedTypes.Contains(t))
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
			var buildableThings = queue.BuildableItems();
			if (!buildableThings.Any())
				return null;

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
				new MiniYamlNode("QueuedBuildRequestTags", FieldSaver.FormatValue(queuedBuildRequestTags.ToArray())),
				new MiniYamlNode("IdleUnitCount", FieldSaver.FormatValue(idleUnitCount))
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
				queuedBuildRequestTags.Clear();
				var tagsNode = data.FirstOrDefault(n => n.Key == "QueuedBuildRequestTags");
				if (tagsNode != null)
					queuedBuildRequestTags.AddRange(FieldLoader.GetValue<string[]>("QueuedBuildRequestTags", tagsNode.Value.Value));

				while (queuedBuildRequestTags.Count < queuedBuildRequests.Count)
					queuedBuildRequestTags.Add(string.Empty);
				if (queuedBuildRequestTags.Count > queuedBuildRequests.Count)
					queuedBuildRequestTags.RemoveRange(queuedBuildRequests.Count, queuedBuildRequestTags.Count - queuedBuildRequests.Count);
			}

			var idleUnitCountNode = data.FirstOrDefault(n => n.Key == "IdleUnitCount");
			if (idleUnitCountNode != null)
				idleUnitCount = FieldLoader.GetValue<int>("IdleUnitCount", idleUnitCountNode.Value.Value);
		}
	}
}
