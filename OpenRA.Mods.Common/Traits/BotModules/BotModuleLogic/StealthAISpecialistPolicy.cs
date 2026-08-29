#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenRA.Mods.Common.Traits.BotModules.Squads;

#pragma warning disable SA1507

namespace OpenRA.Mods.Common.Traits
{
	public enum StealthTankSquadRole { Harass, Attack }
	public enum SpecialistDefenderClearAction { None, CrushInfantry, SnipeTank, AttackUnarmedDetector }
	public enum SpecialistLostActivityRouteDecision { None, RetainShared, SameEndpointMemberRoute, AlternateEndpoint }
	public enum SpecialistRepairDisposition { Active, Repair, Rejoin }
	public readonly struct StealthEfficiencyWindowState
	{
		public int StartTick { get; }
		public long RawKilledValue { get; }
		public int KillCount { get; }
		public long ActorTicks { get; }
		public long TotalDamage { get; }
		public uint[] Actors { get; }

		public StealthEfficiencyWindowState(int startTick, long rawKilledValue, int killCount,
			long actorTicks, long totalDamage, uint[] actors)
		{
			StartTick = startTick;
			RawKilledValue = rawKilledValue;
			KillCount = killCount;
			ActorTicks = actorTicks;
			TotalDamage = totalDamage;
			Actors = actors ?? Array.Empty<uint>();
		}
	}

	public sealed class StealthEfficiencyWindow
	{
		readonly HashSet<uint> actors = new HashSet<uint>();

		public int StartTick { get; }
		public long RawKilledValue { get; private set; }
		public int KillCount { get; private set; }
		public long ActorTicks { get; private set; }
		public long TotalDamage { get; private set; }
		public uint[] Actors => actors.OrderBy(id => id).ToArray();

		public StealthEfficiencyWindow(int startTick)
		{
			StartTick = startTick;
		}

		public static StealthEfficiencyWindow Restore(StealthEfficiencyWindowState state)
		{
			var window = new StealthEfficiencyWindow(state.StartTick)
			{
				RawKilledValue = state.RawKilledValue,
				KillCount = state.KillCount,
				ActorTicks = state.ActorTicks,
				TotalDamage = state.TotalDamage
			};
			window.actors.UnionWith(state.Actors);
			return window;
		}

		public StealthEfficiencyWindowState ExportState()
		{
			return new StealthEfficiencyWindowState(
				StartTick, RawKilledValue, KillCount, ActorTicks, TotalDamage, Actors);
		}

		public void Observe(IEnumerable<uint> liveActors)
		{
			var live = liveActors.Distinct().ToArray();
			ActorTicks += live.Length;
			actors.UnionWith(live);
		}

		public void RecordKill(long value)
		{
			KillCount++;
			RawKilledValue += Math.Max(0, value);
		}

		public void RecordDamage(uint actorId, long value)
		{
			actors.Add(actorId);
			TotalDamage += Math.Max(0, value);
		}

		public StealthAISpecialistPolicy.StealthEfficiencySummary Summary()
		{
			return StealthAISpecialistPolicy.StealthEfficiency(
				RawKilledValue, ActorTicks, TotalDamage, actors.Count);
		}
	}

	public enum StealthTankPlanInvalidation
	{
		None,
		TargetChanged,
		MembershipChanged,
		TargetMoved,
		RouteUnsafe,
		LostActivity,
		NoProgress
	}

	public enum StealthTankTargetReassessment
	{
		RetainIncumbent,
		SwitchToChallenger,
		Abandon
	}

	public enum BotStationaryWatchdogExemption
	{
		None,
		Firing,
		Repairing
	}

	public enum StealthCadenceQuickClearMode
	{
		UndefendedValue,
		Kite
	}

	// Persistent production state for one real stealth-squad generation. The configured
	// definition/index is only a reusable slot label; GenerationId is the immutable identity.
	public sealed class StealthKillCadenceGeneration
	{
		public int GenerationId { get; private set; }
		public int GenerationStartTick { get; private set; }
		public int WindowStartTick { get; private set; }
		public int LastObservedTick { get; private set; }
		public int CadenceAge { get; private set; }
		public int AttributedKills { get; private set; }
		public bool CadenceFailed { get; private set; }
		public bool MismatchFailed { get; private set; }

		public StealthKillCadenceGeneration(int generationId, int generationStartTick)
		{
			if (generationId <= 0)
				throw new ArgumentOutOfRangeException(nameof(generationId));
			if (generationStartTick < 0)
				throw new ArgumentOutOfRangeException(nameof(generationStartTick));

			GenerationId = generationId;
			GenerationStartTick = generationStartTick;
			WindowStartTick = generationStartTick;
			LastObservedTick = generationStartTick;
		}

		public static StealthKillCadenceGeneration Restore(int generationId,
			int generationStartTick, int windowStartTick, int lastObservedTick,
			int cadenceAge, int attributedKills, bool cadenceFailed, bool mismatchFailed)
		{
			if (windowStartTick < generationStartTick || lastObservedTick < generationStartTick ||
				cadenceAge < 0 || attributedKills < 0)
				throw new ArgumentOutOfRangeException(nameof(cadenceAge));

			var generation = new StealthKillCadenceGeneration(generationId, generationStartTick)
			{
				WindowStartTick = windowStartTick,
				LastObservedTick = lastObservedTick,
				CadenceAge = cadenceAge,
				AttributedKills = attributedKills,
				CadenceFailed = cadenceFailed,
				MismatchFailed = mismatchFailed
			};
			generation.CheckMismatch(lastObservedTick);
			return generation;
		}

		public bool Observe(int tick, bool active)
		{
			if (tick < LastObservedTick)
				throw new ArgumentOutOfRangeException(nameof(tick));

			var mismatchBefore = MismatchFailed;
			if (active)
				CadenceAge += tick - LastObservedTick;
			LastObservedTick = tick;
			CheckMismatch(tick);
			return !mismatchBefore && MismatchFailed;
		}

		public bool AttributeKill(int tick)
		{
			Observe(tick, true);
			if (MismatchFailed)
				return false;

			CadenceAge = 0;
			WindowStartTick = tick;
			AttributedKills++;
			CadenceFailed = false;
			return true;
		}

		public void MarkCadenceFailed()
		{
			CadenceFailed = true;
		}

		void CheckMismatch(int tick)
		{
			if (CadenceAge > tick - GenerationStartTick)
				MismatchFailed = true;
		}
	}

	public sealed class StealthCadenceGenerationRecord
	{
		public readonly string SquadDefinition;
		public readonly int SquadIndex;
		public readonly StealthKillCadenceGeneration Generation;

		public StealthCadenceGenerationRecord(string squadDefinition, int squadIndex,
			StealthKillCadenceGeneration generation)
		{
			SquadDefinition = squadDefinition ?? throw new ArgumentNullException(nameof(squadDefinition));
			SquadIndex = squadIndex;
			Generation = generation ?? throw new ArgumentNullException(nameof(generation));
		}
	}

	public sealed class StealthTankReinforcementSaveGroup
	{
		public int GroupIndex;
		public uint[] Members;
		public KeyValuePair<uint, uint>[] PlanTargets = Array.Empty<KeyValuePair<uint, uint>>();
		public uint[] SafeHolds = Array.Empty<uint>();
	}

	public static class StealthAISpecialistPolicy
	{
		public readonly struct StealthEfficiencySummary
		{
			public readonly long RawKilledValue;
			public readonly long ActorTicks;
			public readonly double ActorMinutes;
			public readonly int UniqueStnks;
			public readonly long TotalDamage;
			public readonly double? AverageDamage;
			public readonly double? Primary;
			public readonly double? DamageAdjusted;
			public readonly bool InfiniteDamageAdjusted;

			public StealthEfficiencySummary(long rawKilledValue, long actorTicks,
				double actorMinutes, int uniqueStnks, long totalDamage, double? averageDamage,
				double? primary, double? damageAdjusted, bool infiniteDamageAdjusted)
			{
				RawKilledValue = rawKilledValue;
				ActorTicks = actorTicks;
				ActorMinutes = actorMinutes;
				UniqueStnks = uniqueStnks;
				TotalDamage = totalDamage;
				AverageDamage = averageDamage;
				Primary = primary;
				DamageAdjusted = damageAdjusted;
				InfiniteDamageAdjusted = infiniteDamageAdjusted;
			}
		}

		public const int MaximumSquadCount = 4;
		public const int RequiredStrategicCellSize = 6;
		public const int NearbyReactionMaximumLatencyTicks = 25;
		public const float HardRouteDangerThreshold = 1f;
		public const float SoftResourceRouteCost = 0.05f;
		public const float HardDetectorRouteInfluence = 1f;
		public const int ReinforcementSaveVersion = 2;
		public const int StealthGenerationEfficiencySaveVersion = 1;
		public const int StealthCadenceGenerationSaveVersion = 1;
		public const int AggressiveMassEntryCrossoverPercent = 500;
		public const long MinimumStrategicCellValue = 5000L * 1100L;

		public static MiniYamlNode SaveStealthGenerationEfficiency(
			IEnumerable<KeyValuePair<int, StealthEfficiencyWindow>> windows)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", FieldSaver.FormatValue(StealthGenerationEfficiencySaveVersion))
			};
			nodes.AddRange(windows.OrderBy(pair => pair.Key).Select(pair =>
			{
				var state = pair.Value.ExportState();
				return new MiniYamlNode("Generation", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("Id", FieldSaver.FormatValue(pair.Key)),
					new MiniYamlNode("StartTick", FieldSaver.FormatValue(state.StartTick)),
					new MiniYamlNode("RawKilledValue", FieldSaver.FormatValue(state.RawKilledValue)),
					new MiniYamlNode("KillCount", FieldSaver.FormatValue(state.KillCount)),
					new MiniYamlNode("ActorTicks", FieldSaver.FormatValue(state.ActorTicks)),
					new MiniYamlNode("TotalDamage", FieldSaver.FormatValue(state.TotalDamage)),
					new MiniYamlNode("Actors", FieldSaver.FormatValue(state.Actors))
				});
			}));
			return new MiniYamlNode("StealthGenerationEfficiency", "", nodes);
		}

		public static bool TryLoadStealthGenerationEfficiency(MiniYamlNode node,
			out KeyValuePair<int, StealthEfficiencyWindow>[] windows)
		{
			windows = Array.Empty<KeyValuePair<int, StealthEfficiencyWindow>>();
			if (node == null)
				return false;

			try
			{
				var versionNode = node.Value.Nodes.Single(n => n.Key == "Version");
				if (FieldLoader.GetValue<int>(versionNode.Key, versionNode.Value.Value) !=
					StealthGenerationEfficiencySaveVersion)
					return false;

				var loaded = node.Value.Nodes.Where(n => n.Key == "Generation").Select(generation =>
				{
					var fields = generation.Value.Nodes.ToDictionary(n => n.Key);
					var id = FieldLoader.GetValue<int>("Id", fields["Id"].Value.Value);
					var state = new StealthEfficiencyWindowState(
						FieldLoader.GetValue<int>("StartTick", fields["StartTick"].Value.Value),
						FieldLoader.GetValue<long>("RawKilledValue", fields["RawKilledValue"].Value.Value),
						FieldLoader.GetValue<int>("KillCount", fields["KillCount"].Value.Value),
						FieldLoader.GetValue<long>("ActorTicks", fields["ActorTicks"].Value.Value),
						FieldLoader.GetValue<long>("TotalDamage", fields["TotalDamage"].Value.Value),
						FieldLoader.GetValue<uint[]>("Actors", fields["Actors"].Value.Value));
					if (id <= 0 || state.StartTick < 0 || state.RawKilledValue < 0 || state.KillCount < 0 ||
						state.ActorTicks < 0 || state.TotalDamage < 0 ||
						state.Actors.Distinct().Count() != state.Actors.Length)
						throw new InvalidOperationException();

					return new KeyValuePair<int, StealthEfficiencyWindow>(
						id, StealthEfficiencyWindow.Restore(state));
				}).ToArray();

				if (loaded.Select(pair => pair.Key).Distinct().Count() != loaded.Length)
					return false;

				windows = loaded;
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public static MiniYamlNode SaveStealthCadenceGenerations(
			IEnumerable<StealthCadenceGenerationRecord> records)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", FieldSaver.FormatValue(StealthCadenceGenerationSaveVersion))
			};
			nodes.AddRange(records.OrderBy(record => record.Generation.GenerationId).Select(record =>
			{
				var generation = record.Generation;
				return new MiniYamlNode("Generation", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("Id", FieldSaver.FormatValue(generation.GenerationId)),
					new MiniYamlNode("Definition", record.SquadDefinition),
					new MiniYamlNode("SquadIndex", FieldSaver.FormatValue(record.SquadIndex)),
					new MiniYamlNode("StartTick", FieldSaver.FormatValue(generation.GenerationStartTick)),
					new MiniYamlNode("WindowStartTick", FieldSaver.FormatValue(generation.WindowStartTick)),
					new MiniYamlNode("LastObservedTick", FieldSaver.FormatValue(generation.LastObservedTick)),
					new MiniYamlNode("CadenceAge", FieldSaver.FormatValue(generation.CadenceAge)),
					new MiniYamlNode("AttributedKills", FieldSaver.FormatValue(generation.AttributedKills)),
					new MiniYamlNode("CadenceFailed", FieldSaver.FormatValue(generation.CadenceFailed)),
					new MiniYamlNode("MismatchFailed", FieldSaver.FormatValue(generation.MismatchFailed))
				});
			}));
			return new MiniYamlNode("StealthCadenceGenerations", "", nodes);
		}

		public static bool TryLoadStealthCadenceGenerations(MiniYamlNode node,
			out StealthCadenceGenerationRecord[] records)
		{
			records = Array.Empty<StealthCadenceGenerationRecord>();
			if (node == null)
				return false;

			try
			{
				var version = node.Value.Nodes.Single(n => n.Key == "Version");
				if (FieldLoader.GetValue<int>(version.Key, version.Value.Value) != StealthCadenceGenerationSaveVersion)
					return false;

				var loaded = node.Value.Nodes.Where(n => n.Key == "Generation").Select(generationNode =>
				{
					var fields = generationNode.Value.Nodes.ToDictionary(n => n.Key);
					var generation = StealthKillCadenceGeneration.Restore(
						FieldLoader.GetValue<int>("Id", fields["Id"].Value.Value),
						FieldLoader.GetValue<int>("StartTick", fields["StartTick"].Value.Value),
						FieldLoader.GetValue<int>("WindowStartTick", fields["WindowStartTick"].Value.Value),
						FieldLoader.GetValue<int>("LastObservedTick", fields["LastObservedTick"].Value.Value),
						FieldLoader.GetValue<int>("CadenceAge", fields["CadenceAge"].Value.Value),
						FieldLoader.GetValue<int>("AttributedKills", fields["AttributedKills"].Value.Value),
						FieldLoader.GetValue<bool>("CadenceFailed", fields["CadenceFailed"].Value.Value),
						FieldLoader.GetValue<bool>("MismatchFailed", fields["MismatchFailed"].Value.Value));
					return new StealthCadenceGenerationRecord(fields["Definition"].Value.Value,
						FieldLoader.GetValue<int>("SquadIndex", fields["SquadIndex"].Value.Value), generation);
				}).ToArray();

				if (loaded.Any(record => string.IsNullOrEmpty(record.SquadDefinition) || record.SquadIndex < 0) ||
					loaded.Select(record => record.Generation.GenerationId).Distinct().Count() != loaded.Length)
					return false;

				records = loaded;
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public static BotStationaryWatchdogExemption StationaryWatchdogExemption(
			bool sustainedFiring, bool observedHealing)
		{
			if (observedHealing)
				return BotStationaryWatchdogExemption.Repairing;

			return sustainedFiring ? BotStationaryWatchdogExemption.Firing :
				BotStationaryWatchdogExemption.None;
		}

		public static int ObservedRepairAmount(int previousHealth, int currentHealth)
		{
			return Math.Max(0, currentHealth - previousHealth);
		}

		public static int FiringEpisodeCadenceTicks(int reloadDelay,
			IEnumerable<int> burstDelays, int toleranceTicks)
		{
			if (reloadDelay < 0)
				throw new ArgumentOutOfRangeException(nameof(reloadDelay));
			if (toleranceTicks < 0)
				throw new ArgumentOutOfRangeException(nameof(toleranceTicks));

			return Math.Max(1, reloadDelay + (burstDelays?.Sum() ?? 0) + toleranceTicks);
		}

		public static bool IsSustainedFiringEpisode(int lastDischargeTick, int currentTick,
			int cadenceTicks, bool sameTarget, bool sameAttackActivity, bool targetValid)
		{
			return lastDischargeTick != int.MinValue && cadenceTicks > 0 &&
				currentTick >= lastDischargeTick && currentTick - lastDischargeTick <= cadenceTicks &&
				sameTarget && sameAttackActivity && targetValid;
		}

		public static int NextStationaryWatchdogAge(int currentAge, bool moved,
			BotStationaryWatchdogExemption exemption)
		{
			if (currentAge < 0)
				throw new ArgumentOutOfRangeException(nameof(currentAge));

			if (moved)
				return 0;

			return exemption == BotStationaryWatchdogExemption.None ? currentAge + 1 : currentAge;
		}

		public static bool StationaryWatchdogFailed(int stationaryAge, int maximumStationaryTicks)
		{
			return maximumStationaryTicks > 0 && stationaryAge >= maximumStationaryTicks;
		}

		public static long StrategicTargetValue(int priority, int actorValue)
		{
			return priority > 0 && actorValue > 0 ? priority * (long)actorValue : 0;
		}

		internal static long StrategicTargetValueByRemainingHealth(
			int priority, int actorValue, int hitPoints, int maximumHitPoints)
		{
			var value = StrategicTargetValue(priority, actorValue);
			if (value == 0 || maximumHitPoints <= 0)
				return value;

			var remaining = Math.Clamp(hitPoints, 1, maximumHitPoints);
			return value * Math.Min(maximumHitPoints, remaining * 4L) / remaining;
		}

		public static bool MeetsMinimumStrategicCellValue(long value)
		{
			return value >= MinimumStrategicCellValue;
		}

		public static bool TargetCellIsInActiveTier(long value, bool highTierExists)
		{
			var high = MeetsMinimumStrategicCellValue(value);
			return highTierExists ? high : !high;
		}

		public static IReadOnlyList<T> HighestPriorityEligibleEngagements<T>(
			IEnumerable<(T Item, int Priority)> eligible)
		{
			if (eligible == null)
				throw new ArgumentNullException(nameof(eligible));

			var candidates = eligible.ToList();
			if (candidates.Count == 0)
				return Array.Empty<T>();

			var highestPriority = candidates.Max(candidate => candidate.Priority);
			return candidates.Where(candidate => candidate.Priority == highestPriority)
				.Select(candidate => candidate.Item).ToList();
		}

		public static int FirstUnoccupiedEnterableDestination(
			IReadOnlyList<bool> occupied, IReadOnlyList<bool> enterable)
		{
			if (occupied == null)
				throw new ArgumentNullException(nameof(occupied));
			if (enterable == null)
				throw new ArgumentNullException(nameof(enterable));
			if (occupied.Count != enterable.Count)
				throw new ArgumentException("Destination state lengths must match.");

			for (var i = 0; i < occupied.Count; i++)
				if (!occupied[i] && enterable[i])
					return i;

			return -1;
		}

		public static bool DestinationBelongsToStrategicCell(
			int destinationX, int destinationY, int strategicCellSize,
			int expectedStrategicX, int expectedStrategicY)
		{
			return strategicCellSize > 0 &&
				destinationX / strategicCellSize == expectedStrategicX &&
				destinationY / strategicCellSize == expectedStrategicY;
		}

		public static float CloakAwareRouteDanger(float mobilityDanger, float weaponDanger,
			bool detectorCoverage, bool currentlyCloaked)
		{
			if (!currentlyCloaked)
				return mobilityDanger + weaponDanger;

			// Detection is harmless by itself, but lets every covering weapon engage the
			// cloaked unit. Make that guarded overlap hard danger without pricing weapons
			// that cannot currently acquire the unit.
			return mobilityDanger + (detectorCoverage && weaponDanger > 0 ?
				HardDetectorRouteInfluence : 0);
		}

		public static bool PlannedExposureIsSafe(bool coveringWeapon, bool nextCellSafe,
			bool existingExposureException)
		{
			return existingExposureException || (!coveringWeapon && nextCellSafe);
		}

		public static bool CloakedCrushExposureIsSafe(bool formationCloaked,
			bool targetDetectorCovered, bool nextCellDetectorCovered)
		{
			return formationCloaked && !targetDetectorCovered && !nextCellDetectorCovered;
		}

		public static bool CloakedCrushRouteIsSafe(bool formationCloaked,
			IEnumerable<bool> waypointDetectorCoverage)
		{
			return formationCloaked && waypointDetectorCoverage != null &&
				!waypointDetectorCoverage.Any(covered => covered);
		}

		public static long AccumulateActorTicks(long actorTicks, int liveActors)
		{
			return actorTicks + Math.Max(0, liveActors);
		}

		public static bool ShouldOwnStealthEfficiencyTerminal(bool botEnabled, bool traitEnabled)
		{
			return botEnabled && traitEnabled;
		}

		public static bool TryBeginStealthTerminalSummary(ref bool reported, bool botEnabled, bool traitEnabled)
		{
			if (reported || !ShouldOwnStealthEfficiencyTerminal(botEnabled, traitEnabled))
				return false;

			reported = true;
			return true;
		}

		public static int[] TerminalStealthGenerationIds(
			IEnumerable<StealthKillCadenceGeneration> generations)
		{
			return generations.Where(generation => generation != null && generation.GenerationId > 0)
				.Select(generation => generation.GenerationId).Distinct().OrderBy(id => id).ToArray();
		}

		public static bool TryTakeStealthGeneration<T>(IDictionary<int, T> activeGenerations,
			int generationId, out T generation)
		{
			if (!activeGenerations.TryGetValue(generationId, out generation))
				return false;

			activeGenerations.Remove(generationId);
			return true;
		}

		public static StealthEfficiencySummary StealthEfficiency(long rawKilledValue,
			long actorTicks, long totalDamage, int uniqueStnks)
		{
			var actorMinutes = actorTicks / 3000d;
			double? primary = actorMinutes == 0 ? (double?)null : rawKilledValue / actorMinutes;
			double? averageDamage = uniqueStnks == 0 ? (double?)null : totalDamage / (double)uniqueStnks;
			var infinite = primary > 0 && averageDamage == 0;
			double? damageAdjusted = primary == null || averageDamage == null || averageDamage == 0 ?
				null : primary / averageDamage;

			return new StealthEfficiencySummary(rawKilledValue, actorTicks, actorMinutes,
				uniqueStnks, totalDamage, averageDamage, primary, damageAdjusted, infinite);
		}

		static string EfficiencyValue(double? value, bool infinite = false)
		{
			if (infinite)
				return "infinite";
			if (value == null)
				return "unavailable";

			return value.Value.ToString("R", CultureInfo.InvariantCulture);
		}

		public static string FormatStealthEfficiencySummary(string summary, uint botId,
			int windowStartTick, int windowEndTick, StealthEfficiencySummary metric)
		{
			return string.Join("|", new[]
			{
				"stealth_efficiency_watchdog",
				"summary=" + summary,
				"bot_id=" + botId.ToString(CultureInfo.InvariantCulture),
				"scope=stnk",
				"window_start_tick=" + windowStartTick.ToString(CultureInfo.InvariantCulture),
				"window_end_tick=" + windowEndTick.ToString(CultureInfo.InvariantCulture),
				"raw_killed_value=" + metric.RawKilledValue.ToString(CultureInfo.InvariantCulture),
				"actor_ticks=" + metric.ActorTicks.ToString(CultureInfo.InvariantCulture),
				"actor_minutes=" + EfficiencyValue(metric.ActorMinutes),
				"unique_stnks=" + metric.UniqueStnks.ToString(CultureInfo.InvariantCulture),
				"total_damage=" + metric.TotalDamage.ToString(CultureInfo.InvariantCulture),
				"average_damage=" + EfficiencyValue(metric.AverageDamage),
				"primary=" + EfficiencyValue(metric.Primary),
				"damage_adjusted=" + EfficiencyValue(metric.DamageAdjusted, metric.InfiniteDamageAdjusted),
				"diagnostic_only=true"
			});
		}

		public static int WeightedRouteDistanceCells(float routeCost, int strategicCellSize)
		{
			if (!float.IsFinite(routeCost) || routeCost < 0)
				return int.MaxValue;

			return (int)Math.Min(int.MaxValue,
				Math.Ceiling(routeCost * Math.Max(1, strategicCellSize)));
		}









		public static MiniYamlNode SaveReinforcementState(
			IEnumerable<StealthTankReinforcementSaveGroup> groups)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", FieldSaver.FormatValue(ReinforcementSaveVersion))
			};
			nodes.AddRange(groups.OrderBy(g => g.GroupIndex).Select(group =>
				new MiniYamlNode("Group", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("Index", FieldSaver.FormatValue(group.GroupIndex)),
					new MiniYamlNode("Members", FieldSaver.FormatValue(group.Members.OrderBy(id => id).ToArray())),
					new MiniYamlNode("PlanTargets", "", group.PlanTargets.OrderBy(pair => pair.Key)
						.Select(pair => new MiniYamlNode("Plan", "", new List<MiniYamlNode>
						{
							new MiniYamlNode("Member", FieldSaver.FormatValue(pair.Key)),
							new MiniYamlNode("Target", FieldSaver.FormatValue(pair.Value))
						})).ToList()),
					new MiniYamlNode("SafeHolds", FieldSaver.FormatValue(group.SafeHolds.OrderBy(id => id).ToArray()))
				})));
			return new MiniYamlNode("StealthTankReinforcementState", "", nodes);
		}

		public static bool TryLoadReinforcementState(MiniYamlNode state,
			out StealthTankReinforcementSaveGroup[] groups)
		{
			groups = Array.Empty<StealthTankReinforcementSaveGroup>();
			if (state == null)
				return false;

			try
			{
				var version = state.Value.Nodes.Single(n => n.Key == "Version");
				var loadedVersion = FieldLoader.GetValue<int>(version.Key, version.Value.Value);
				if (loadedVersion != 1 && loadedVersion != ReinforcementSaveVersion)
					return false;

				var loaded = state.Value.Nodes.Where(n => n.Key == "Group").Select(groupNode =>
				{
					var indexNode = groupNode.Value.Nodes.Single(n => n.Key == "Index");
					var membersNode = groupNode.Value.Nodes.Single(n => n.Key == "Members");
					var targetsNode = groupNode.Value.Nodes.FirstOrDefault(n => n.Key == "PlanTargets");
					var holdsNode = groupNode.Value.Nodes.FirstOrDefault(n => n.Key == "SafeHolds");
					if (loadedVersion >= 2 && (targetsNode == null || holdsNode == null))
						throw new InvalidOperationException();

					return new StealthTankReinforcementSaveGroup
					{
						GroupIndex = FieldLoader.GetValue<int>(indexNode.Key, indexNode.Value.Value),
						Members = FieldLoader.GetValue<uint[]>(membersNode.Key, membersNode.Value.Value),
						PlanTargets = loadedVersion >= 2 && targetsNode != null ?
							targetsNode.Value.Nodes.Where(n => n.Key == "Plan").Select(plan =>
							{
								var memberNode = plan.Value.Nodes.Single(n => n.Key == "Member");
								var targetNode = plan.Value.Nodes.Single(n => n.Key == "Target");
								return new KeyValuePair<uint, uint>(
									FieldLoader.GetValue<uint>(memberNode.Key, memberNode.Value.Value),
									FieldLoader.GetValue<uint>(targetNode.Key, targetNode.Value.Value));
							}).ToArray() :
							Array.Empty<KeyValuePair<uint, uint>>(),
						SafeHolds = loadedVersion >= 2 && holdsNode != null ?
							FieldLoader.GetValue<uint[]>(holdsNode.Key, holdsNode.Value.Value) : Array.Empty<uint>()
					};
				}).ToArray();
				if (loaded.Any(g => g.GroupIndex < 0 || g.Members.Length == 0 ||
					g.Members.Distinct().Count() != g.Members.Length ||
					g.PlanTargets.Select(pair => pair.Key).Distinct().Count() != g.PlanTargets.Length ||
					g.PlanTargets.Any(pair => !g.Members.Contains(pair.Key)) ||
					g.SafeHolds.Distinct().Count() != g.SafeHolds.Length ||
					g.SafeHolds.Any(id => !g.PlanTargets.Any(pair => pair.Key == id))) ||
					loaded.Select(g => g.GroupIndex).Distinct().Count() != loaded.Length ||
					loaded.SelectMany(g => g.Members).Distinct().Count() != loaded.Sum(g => g.Members.Length))
					return false;

				groups = loaded;
				return true;
			}
			catch (InvalidOperationException) { return false; }
			catch (FormatException) { return false; }
			catch (OverflowException) { return false; }
			catch (YamlException) { return false; }
		}







		public static bool IsSameStrategicCell(CPos a, CPos b, int strategicCellSize)
		{
			return StrategicCell(a, strategicCellSize) == StrategicCell(b, strategicCellSize);
		}

		public static CPos StrategicCell(CPos cell, int strategicCellSize)
		{
			var size = Math.Max(1, strategicCellSize);
			return new CPos(cell.X / size, cell.Y / size);
		}

		public static bool IsSameOrAdjacentStrategicCell(CPos a, CPos b, int strategicCellSize)
		{
			var ac = StrategicCell(a, strategicCellSize);
			var bc = StrategicCell(b, strategicCellSize);
			return Math.Max(Math.Abs(ac.X - bc.X), Math.Abs(ac.Y - bc.Y)) <= 1;
		}

		public static bool ShouldStageReinforcement(bool hasEstablishedCore,
			bool wasPreviouslyAssigned)
		{
			return hasEstablishedCore && !wasPreviouslyAssigned;
		}


		public static bool CanAdvanceReinforcement(bool active)
		{
			return active;
		}

		public static bool ShouldIssueReinforcementOrder(bool retainedPlanMatches,
			bool retainedSafeHold, bool isIdle, bool routeAvailable, bool issuedThisTick)
		{
			if (issuedThisTick)
				return false;

			if (!routeAvailable)
				return !retainedPlanMatches || !retainedSafeHold;

			return !retainedPlanMatches || retainedSafeHold || isIdle;
		}

		public static bool ShouldPreserveBusyReinforcement(bool retainedPlanMatches, bool isIdle)
		{
			return retainedPlanMatches && !isIdle;
		}

		public static bool ShouldRetryFailedReinforcementSearch(bool sameTarget,
			bool sameOrigin, bool sameAnchor, bool sameRouteContext)
		{
			return !(sameTarget && sameOrigin && sameAnchor && sameRouteContext);
		}

		public static bool ShouldRetryFailedMobilitySearch(bool sameOrigin,
			bool sameAnchor, bool sameRouteContext)
		{
			return !(sameOrigin && sameAnchor && sameRouteContext);
		}

		public static bool ShouldIssueSafeMobilityRoute(bool isIdle,
			bool exactSegmentsUsable, bool identicalFailedRoute)
		{
			return isIdle && exactSegmentsUsable && !identicalFailedRoute;
		}

		public static bool ShouldRestoreReinforcementPlan(bool validMember,
			bool validTarget, bool ownsActivity)
		{
			return validMember && validTarget && ownsActivity;
		}

		public static bool ShouldRestoreReinforcementMember(bool eligible,
			bool reserved, bool selected)
		{
			return eligible && reserved && selected;
		}


		public static bool IsHardRouteDanger(float danger)
		{
			return danger >= HardRouteDangerThreshold;
		}


		public static uint? RecoveryCore(IEnumerable<uint> members, ISet<uint> reinforcements)
		{
			var ordered = members.OrderBy(id => id).ToArray();
			return ordered.Length > 0 && ordered.All(reinforcements.Contains) ? ordered[0] : (uint?)null;
		}




		public static bool ShouldReserveUnit(bool alreadyReserved, bool claimAllEligible, bool eligible)
		{
			return alreadyReserved || (claimAllEligible && eligible);
		}

		public static bool ShouldRunStrategicScan(ref int countdown, int interval)
		{
			if (--countdown > 0)
				return false;

			countdown = Math.Max(1, interval);
			return true;
		}

		public static bool ShouldRefreshStrategicView(int cachedTick, int currentTick)
		{
			return cachedTick != currentTick;
		}

		public static bool ShouldRefreshInfluenceMap(int cachedTick, int currentTick, int interval)
		{
			return cachedTick == int.MinValue || currentTick - cachedTick >= Math.Max(1, interval);
		}










		public static List<CPos> ForwardExactGroundRoute(IEnumerable<CPos> reversedPathfinderRoute)
		{
			// IPathFinder.FindUnitPath returns target-to-source. Plans and submitted
			// waypoints use source-to-target, matching the coarse/Air route contract.
			return reversedPathfinderRoute.Reverse().ToList();
		}

		public static bool RouteStretchIsDisproportionate(int selectedDistance,
			int directDistance, int maximumStretchPercent)
		{
			return directDistance > 0 && selectedDistance > 0 && maximumStretchPercent >= 100 &&
				selectedDistance * 100L > directDistance * (long)maximumStretchPercent;
		}





		public static int OptimisticApproachDistance(int targetDistanceCells, int weaponRangeCells)
		{
			return Math.Max(0, targetDistanceCells - Math.Max(0, weaponRangeCells));
		}

		public static StealthTankTargetReassessment ReassessTarget(bool incumbentValid,
			bool incumbentUndefended, long incumbentScore, bool challengerValid,
			bool challengerUndefended, long challengerScore, int minimumImprovementPercent)
		{
			if (!incumbentValid)
				return challengerValid ? StealthTankTargetReassessment.SwitchToChallenger :
					StealthTankTargetReassessment.Abandon;

			return AirThreatGeometry.ShouldSwitchTarget(incumbentUndefended, incumbentScore,
				challengerValid, challengerUndefended, challengerScore, minimumImprovementPercent) ?
				StealthTankTargetReassessment.SwitchToChallenger :
				StealthTankTargetReassessment.RetainIncumbent;
		}

		public static StealthTankTargetReassessment ReassessTargetWithWallFallback(bool incumbentValid,
			bool incumbentUndefended, long incumbentScore, bool challengerValid,
			bool challengerUndefended, long challengerScore, int minimumImprovementPercent,
			bool incumbentIsWall, bool challengerIsWall)
		{
			if (!incumbentValid)
				return challengerValid ? StealthTankTargetReassessment.SwitchToChallenger :
					StealthTankTargetReassessment.Abandon;

			if (!challengerValid)
				return StealthTankTargetReassessment.RetainIncumbent;
			if (incumbentIsWall != challengerIsWall)
				return incumbentIsWall ? StealthTankTargetReassessment.SwitchToChallenger :
					StealthTankTargetReassessment.RetainIncumbent;

			return ReassessTarget(true, incumbentUndefended, incumbentScore,
				true, challengerUndefended, challengerScore, minimumImprovementPercent);
		}

		public static List<T> NearbyReassessmentCandidates<T>(IEnumerable<T> nearbyCandidates,
			T incumbent, Func<T, T, bool> sameCandidate)
		{
			var candidates = nearbyCandidates.ToList();
			if (incumbent != null && !candidates.Any(candidate => sameCandidate(candidate, incumbent)))
				candidates.Add(incumbent);

			return candidates;
		}

		public static int InfantryClusterMultiplier(int nearbyInfantry, int bonusPercentPerActor,
			int maximumMultiplierPercent)
		{
			var multiplier = 100L + Math.Max(0, nearbyInfantry) * (long)Math.Max(0, bonusPercentPerActor);
			return (int)Math.Min(Math.Max(100, maximumMultiplierPercent), multiplier);
		}

		public static bool CanCarefullyClear(int squadValue, int defendingValue, int requiredValueRatio)
		{
			return squadValue > 0 && defendingValue > 0 && requiredValueRatio > 0 &&
				squadValue >= (long)defendingValue * requiredValueRatio;
		}

		public static bool CanAttemptDefenderClear(int consecutiveNoSafeTargetScans, int requiredScans,
			int squadValue, int defendingValue, int requiredValueRatio)
		{
			return consecutiveNoSafeTargetScans >= Math.Max(0, requiredScans) &&
				CanCarefullyClear(squadValue, defendingValue, requiredValueRatio);
		}

		public static bool AreAllCandidatesUnavailable(int candidateCount, int dangerousCandidates,
			int unroutableCandidates)
		{
			return candidateCount > 0 && Math.Max(0, dangerousCandidates) +
				Math.Max(0, unroutableCandidates) >= candidateCount;
		}


		public static SpecialistDefenderClearAction DefenderClearAction(bool isInfantry, bool isTank,
			bool canCrushInfantry, int packageDefenderCount, int ownRangeCells,
			int defenderWeaponRangeCells, int defenderDetectorRangeCells, int safetyMarginCells)
		{
			// The selected infantry is removed from the route influence map, while every
			// other package defender remains authoritative. This permits a patient
			// overmatching squad to crush a reachable edge defender without pretending
			// that the rest of a multi-defender package is safe.
			if (isInfantry && canCrushInfantry && packageDefenderCount > 0 && defenderDetectorRangeCells <= 0)
				return SpecialistDefenderClearAction.CrushInfantry;

			if (isTank && defenderWeaponRangeCells > 0 && defenderDetectorRangeCells <= 0 &&
				ownRangeCells >= defenderWeaponRangeCells + Math.Max(0, safetyMarginCells))
				return SpecialistDefenderClearAction.SnipeTank;

			// A lone detector cannot punish revealed fire. Keep this deliberately narrower
			// than ordinary structure targeting: it is only a fallback blocker capability,
			// and any overlapping armed defender makes packageDefenderCount greater than one.
			if (packageDefenderCount == 1 && defenderWeaponRangeCells <= 0 &&
				defenderDetectorRangeCells > 0 && ownRangeCells > 0)
				return SpecialistDefenderClearAction.AttackUnarmedDetector;

			return SpecialistDefenderClearAction.None;
		}

		public static bool ShouldIgnoreSelectedDefenderInfluence(SpecialistDefenderClearAction action)
		{
			return action != SpecialistDefenderClearAction.None;
		}

		public static SpecialistRepairDisposition RepairDisposition(bool damagedBelowThreshold,
			bool isRepairing, bool fullyRepaired, bool hasCompatibleReachableRepair)
		{
			if (isRepairing && fullyRepaired)
				return SpecialistRepairDisposition.Rejoin;
			if (damagedBelowThreshold && hasCompatibleReachableRepair)
				return SpecialistRepairDisposition.Repair;

			// No compatible reachable facility is never a parking state. A damaged
			// specialist remains owned by, and active in, its combat squad.
			return SpecialistRepairDisposition.Active;
		}

		public static bool ShouldUseNearestSafeMobilityFallback(bool isIdle,
			bool hasAnchorDirectedRoute, bool hasNearestSafeRoute)
		{
			// Match Air's second-stage escape without replacing a busy order. Damage and
			// an unavailable repair facility do not turn an active squad member into a hold.
			return isIdle && !hasAnchorDirectedRoute && hasNearestSafeRoute;
		}

		public static SpecialistLostActivityRouteDecision LostActivityRouteDecision(
			bool sharedRouteUsable, bool sameEndpointMemberRouteUsable,
			bool alternateEndpointRouteUsable)
		{
			if (sharedRouteUsable)
				return SpecialistLostActivityRouteDecision.RetainShared;
			if (sameEndpointMemberRouteUsable)
				return SpecialistLostActivityRouteDecision.SameEndpointMemberRoute;
			if (alternateEndpointRouteUsable)
				return SpecialistLostActivityRouteDecision.AlternateEndpoint;

			return SpecialistLostActivityRouteDecision.None;
		}

		public static bool FailedMemberRouteRemainsApplicable(bool sameTarget,
			bool sameTargetLocation, bool sameOrigin)
		{
			// A physically stuck ground member must not forget a collapsed route merely
			// because the strategic scanner cycles between live targets. Literal actor
			// movement is the authoritative retry boundary; target-specific alternate
			// endpoints remain independently eligible.
			return sameOrigin;
		}

		public static bool ShouldValidateIdleMemberRoute(StealthTankPlanInvalidation invalidation)
		{
			return invalidation == StealthTankPlanInvalidation.TargetChanged ||
				invalidation == StealthTankPlanInvalidation.LostActivity;
		}

		public static bool ShouldRecomputeSameEndpointMemberRoute(
			bool failedRouteMatchesSharedRoute)
		{
			return !failedRouteMatchesSharedRoute;
		}










		public static bool SubmittedGroundWaypointIsUsable(bool waypointIsHardSafe,
			bool exactSegmentReachable, bool internalEngineRefinementIsHardSafe)
		{
			// The cached coarse route owns threat and soft-resource costs at submitted
			// waypoints. Ground pathfinding only proves locomotor reachability between
			// them; re-vetoing its private refinement cells would invent a second route
			// policy and can reject every otherwise valid coarse plan.
			return waypointIsHardSafe && exactSegmentReachable;
		}

		public static T[] LostActivityPlanMembers<T>(IEnumerable<T> activeMembers,
			Func<T, bool> isIdle)
		{
			return activeMembers.Where(isIdle).ToArray();
		}

		public static T[] TargetChangedPlanMembers<T>(IEnumerable<T> activeMembers,
			Func<T, bool> isIdle, bool canSubmitThisTick)
		{
			// Air records the new target immediately but does not replace a formation
			// member's busy activity. LostActivity submits the pending mission when
			// the old activity completes and that member becomes idle. The submission
			// latch is shared by nearby and strategic producers in the same world tick.
			return canSubmitThisTick ? activeMembers.Where(isIdle).ToArray() : Array.Empty<T>();
		}

		public static bool CanApplyPendingTargetPlan(int currentTick, int lastOrderTick)
		{
			return currentTick > lastOrderTick;
		}

		public static bool ShouldRetainWholeGroupEngagement(bool retainActiveEngagement,
			bool hasPendingIdleMember, bool incumbentIsWallFallback = false)
		{
			// Air preserves each busy attacker independently, but still services an
			// idle joiner. A group-wide early return is only valid when no member is
			// waiting for its deferred target-change handoff. A last-resort wall
			// incumbent must also rescan so that a newly available strategic target
			// can replace the mission without replacing any busy actor activity.
			return retainActiveEngagement && !hasPendingIdleMember && !incumbentIsWallFallback;
		}

		public static TInfluence ResolveRepairInfluence<TFacts, TInfluence>(TFacts sharedThreatFacts,
			Func<TFacts, TInfluence> getPrivateInfluence)
			where TFacts : class
			where TInfluence : class
		{
			// Threat facts belong to the elected shared-view owner. Their interpretation and
			// cache belong to the profile that is currently evaluating its repair route.
			return sharedThreatFacts == null ? null : getPrivateInfluence(sharedThreatFacts);
		}

		public static int BufferedRange(int rangeCells, int bufferCells)
		{
			return rangeCells > 0 ? rangeCells + Math.Max(0, bufferCells) : 0;
		}

		public static bool IsWithinUndefendedTravelPreference(long travelMilliseconds, int maximumSeconds)
		{
			return travelMilliseconds >= 0 && maximumSeconds > 0 &&
				travelMilliseconds <= maximumSeconds * 1000L;
		}

		public static bool CanKite(int ownSpeed, int enemySpeed, int ownRangeCells,
			int enemyRangeCells, int marginCells, int minimumSpeedPercent)
		{
			return ownSpeed > 0 && enemySpeed >= 0 && ownRangeCells > enemyRangeCells + marginCells &&
				ownSpeed * 100L >= enemySpeed * (long)minimumSpeedPercent;
		}

		public static bool MissingCanonicalThreatIsZero(int enabledWeaponRangeCells,
			int enabledDetectorRangeCells)
		{
			// The shared calculator intentionally has no row for an actor that cannot threaten
			// the formation. Absence is zero only when live traits independently confirm that
			// the actor is neither armed nor a detector; missing armed data remains invalid.
			return enabledWeaponRangeCells <= 0 && enabledDetectorRangeCells <= 0;
		}

		public static bool ShouldEnterMassClear(double overmatch, int entryPercent)
		{
			return double.IsFinite(overmatch) && overmatch * 100 > entryPercent;
		}

		public static bool ShouldAbortMassClear(double overmatch, int abortPercent)
		{
			return !double.IsFinite(overmatch) || overmatch * 100 <= abortPercent;
		}

		public static bool ShouldEnterAggressiveMass(double overmatch)
		{
			return double.IsFinite(overmatch) &&
				overmatch * 100 > AggressiveMassEntryCrossoverPercent;
		}

		public static bool CanOutrangeTargetDetector(bool threatIsTarget, int weaponRangeCells,
			int detectorRangeCells, int ownRangeCells)
		{
			return threatIsTarget && weaponRangeCells <= 0 && detectorRangeCells > 0 &&
				ownRangeCells > detectorRangeCells;
		}

		public static bool CanOutrangeUndetectingTarget(int weaponRangeCells,
			int detectorRangeCells, int ownRangeCells)
		{
			return weaponRangeCells > 0 && detectorRangeCells <= 0 &&
				ownRangeCells > weaponRangeCells;
		}

		public static bool IsEngagementThreat(bool detectorExposure, bool armedCoverage,
			bool engagedWeaponExposure)
		{
			// Firing reveals a Stealth Tank, so detection alone cannot punish an engagement.
			// Keep the existing immediate response to a weapon that is already engaged, and
			// otherwise require detector and ground-weapon coverage to overlap the firing cell.
			return engagedWeaponExposure || (detectorExposure && armedCoverage);
		}

		public static bool IsHardPlannedDecloakThreat(bool plannedDecloak, double canonicalThreat)
		{
			return plannedDecloak && canonicalThreat >= HardRouteDangerThreshold;
		}

		public static double AccumulateMaximumCanonicalThreat(double maximumThreat, double canonicalThreat)
		{
			return Math.Max(maximumThreat, canonicalThreat);
		}

		public static int StrategicTargetReviewIntervalTicks(int timestep, int configuredInterval)
		{
			return Math.Max(Math.Max(1, configuredInterval),
				(int)Math.Ceiling(5000d / Math.Max(1, timestep)));
		}

		public static bool ShouldRetainActiveEngagement(bool hasValidTarget, bool isEngaged,
			bool localThreatExposure, bool resourceHazard)
		{
			return hasValidTarget && isEngaged && !localThreatExposure && !resourceHazard;
		}

		public static int NextKillCadenceAge(int currentAge, int elapsedTicks,
			bool stnkKill, bool exempt)
		{
			if (currentAge < 0)
				throw new ArgumentOutOfRangeException(nameof(currentAge));
			if (elapsedTicks < 0)
				throw new ArgumentOutOfRangeException(nameof(elapsedTicks));

			if (stnkKill)
				return 0;

			return exempt ? currentAge : currentAge + elapsedTicks;
		}

		public static bool KillCadenceFailed(int ageTicks, int maximumTicks)
		{
			return maximumTicks > 0 && ageTicks >= maximumTicks;
		}

		public static int KillTimeOwnerGeneration(uint attackerId,
			IEnumerable<KeyValuePair<uint, int>> currentMembership)
		{
			return currentMembership.Where(entry => entry.Key == attackerId)
				.Select(entry => entry.Value).FirstOrDefault();
		}

		public static bool IsObeliskAttributedStealthTankDeath(string victimType, string attackerType)
		{
			return string.Equals(victimType, "stnk", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(attackerType, "obli", StringComparison.OrdinalIgnoreCase);
		}

		public static bool ShouldDispatchOwnedMissionImmediately(bool isStealthTank,
			bool targetValid, int routeWaypoints, bool routeQueued)
		{
			return isStealthTank && targetValid && routeWaypoints > 0 && !routeQueued;
		}

		public static bool ShouldPreserveOwnedMissionRoute(bool isStealthTank,
			bool sameActor, bool routeQueued, bool activeUnit,
			StealthClearMode currentMode, StealthClearMode plannedMode)
		{
			return isStealthTank && sameActor && routeQueued && activeUnit &&
				currentMode == plannedMode;
		}

		public static bool ShouldUseBoundedCrush(long serviceMilliseconds, int maximumSeconds)
		{
			return IsWithinUndefendedTravelPreference(serviceMilliseconds, maximumSeconds);
		}

		public static bool ShouldRefreshQueuedCrushRoute(bool ordinaryCrush, bool routeQueued,
			bool trackedOrderIsCurrent, bool cachedLocalTarget, bool targetChangedCell)
		{
			return ordinaryCrush && routeQueued && !trackedOrderIsCurrent &&
				cachedLocalTarget && targetChangedCell;
		}

		public static int KillCadenceFinishMarginTicks(int reviewTicks, int stallTicks)
		{
			return Math.Max(1, reviewTicks) + Math.Max(1, stallTicks);
		}

		public static bool CanFinishWithinKillCadence(long serviceMilliseconds, int timestep,
			int ageTicks, int maximumTicks, int finishMarginTicks)
		{
			if (serviceMilliseconds < 0 || serviceMilliseconds == long.MaxValue || timestep <= 0 ||
				maximumTicks <= 0)
				return false;

			var remainingTicks = Math.Max(0, maximumTicks - Math.Max(0, ageTicks) -
				Math.Max(0, finishMarginTicks));
			return serviceMilliseconds <= remainingTicks * (long)timestep;
		}

		public static long CachedMobileServiceMilliseconds(long serviceMilliseconds, int timestep,
			int finishMarginTicks, bool mobile)
		{
			if (serviceMilliseconds < 0 || serviceMilliseconds == long.MaxValue || timestep <= 0)
				return long.MaxValue;

			if (!mobile)
				return serviceMilliseconds;

			var reserveMilliseconds = Math.Max(0, finishMarginTicks) * (long)timestep;
			return serviceMilliseconds > long.MaxValue - reserveMilliseconds ? long.MaxValue :
				serviceMilliseconds + reserveMilliseconds;
		}

		public static bool IsKillCadenceUrgent(int ageTicks, int maximumTicks, int finishMarginTicks)
		{
			return maximumTicks > 0 && Math.Max(0, ageTicks) + Math.Max(0, finishMarginTicks) >=
				(maximumTicks + 2) / 3;
		}

		public static int CadenceUrgentLocalQuickClearRank(bool isStealthTank, bool cadenceUrgent,
			bool cachedLocal, bool ownedOrDeconflicted, bool safe, bool hasRoute, bool finishable,
			bool withinLocalServiceWindow, StealthCadenceQuickClearMode mode,
			bool eligibleUndefendedValue, bool eligibleKite)
		{
			if (!isStealthTank || !cadenceUrgent || !cachedLocal || !ownedOrDeconflicted ||
				!safe || !hasRoute || !finishable || !withinLocalServiceWindow)
				return int.MaxValue;

			if (mode == StealthCadenceQuickClearMode.UndefendedValue && eligibleUndefendedValue)
				return 0;

			return mode == StealthCadenceQuickClearMode.Kite && eligibleKite ?
				1 : int.MaxValue;
		}

		public static (long DistanceSquared, long ThreatTieBreak, int ValueTieBreak, uint ActorId)
			CachedLocalKiteOrderKey(long distanceSquared, long cachedThreat, int cachedValue, uint actorId)
		{
			return (Math.Max(0, distanceSquared), -Math.Max(0, cachedThreat),
				-Math.Max(0, cachedValue), actorId);
		}

		public static bool ShouldReplaceNonFinishableMission(long incumbentServiceMilliseconds,
			long challengerServiceMilliseconds, int timestep, int ageTicks, int maximumTicks,
			int finishMarginTicks, bool incumbentMobile = true, bool challengerMobile = true)
		{
			if (challengerServiceMilliseconds < 0 || challengerServiceMilliseconds == long.MaxValue)
				return false;

			var incumbentFits = CanFinishWithinKillCadence(incumbentServiceMilliseconds, timestep,
				ageTicks, maximumTicks, finishMarginTicks);
			var challengerFits = CanFinishWithinKillCadence(challengerServiceMilliseconds, timestep,
				ageTicks, maximumTicks, finishMarginTicks);
			var minimumImprovementMilliseconds = Math.Max(0, finishMarginTicks) *
				(long)Math.Max(1, timestep);
			var materiallyShorter = challengerServiceMilliseconds < incumbentServiceMilliseconds &&
				incumbentServiceMilliseconds - challengerServiceMilliseconds >= minimumImprovementMilliseconds;
			var preservesStationaryFinish = !incumbentMobile && challengerMobile;
			var replacementShorter = challengerServiceMilliseconds < incumbentServiceMilliseconds &&
				(!preservesStationaryFinish || materiallyShorter);
			return (!incumbentFits && (challengerFits || replacementShorter)) ||
				(IsKillCadenceUrgent(ageTicks, maximumTicks, finishMarginTicks) &&
				replacementShorter);
		}

		public static bool ShouldReplaceLowValueWallWithConfirmedMovingMtnkKite(
			bool isStealthTank, bool incumbentIsWall, bool challengerIsMovingMtnk,
			StealthClearMode challengerMode, int consecutiveConfirmations,
			long challengerServiceMilliseconds, int maximumLocalTravelSeconds)
		{
			return isStealthTank && incumbentIsWall && challengerIsMovingMtnk &&
				challengerMode == StealthClearMode.Kite && consecutiveConfirmations >= 2 &&
				IsWithinUndefendedTravelPreference(
					challengerServiceMilliseconds, maximumLocalTravelSeconds);
		}

		public static int TransitThreatRange(int detectorRangeCells, int weaponRangeCells,
			bool weaponIsEngaged, bool canKiteTarget)
		{
			var weaponRange = weaponIsEngaged && !canKiteTarget ? weaponRangeCells : 0;
			return Math.Max(detectorRangeCells, weaponRange);
		}
	}
}
