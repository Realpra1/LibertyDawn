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
using System.Diagnostics;
using System.Linq;
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	enum GeneralGroundMissionStatus
	{
		MissingSquad,
		InvalidTarget,
		Urgent,
		Ready
	}

	enum StealthManagerAttributionPhase
	{
		ManagerTick,
		EfficiencyTelemetry,
		EnsureStealthSquads,
		RebalanceStealthSquads,
		RecruitUnassigned,
		AssignRoles,
		SchedulerSelection,
		GuardDirtyCheck,
		IncrementalPath,
		DependencyValidation,
		ThreatRouteCell,
		LocalPlanning,
		DiagnosticEmission,
		Count
	}

	sealed class StealthManagerAttributionCounter
	{
		public long ElapsedTicks;
		public long Calls;
		public long Operations;

		public void Clear()
		{
			ElapsedTicks = 0;
			Calls = 0;
			Operations = 0;
		}
	}

	[Desc("Manages AI squads.")]
	public class SquadManagerBotModuleInfo : ConditionalTraitInfo
	{
		public class AirSquadDefinition
		{
			[Desc("Actor types accepted by this squad. Empty accepts any configured air unit.")]
			public readonly HashSet<string> UnitTypes = new HashSet<string>();

			[Desc("Number of independent squads of this archetype.")]
			public readonly int SquadCount = 1;

			[Desc("Target and threat profile used by this squad.")]
			public readonly string Profile = "Generic";

			[Desc("Maximum aircraft per squad. Zero means unlimited.")]
			public readonly int MaximumSize = 0;

			public AirSquadDefinition(MiniYaml yaml) { FieldLoader.Load(this, yaml); }
		}

		[FieldLoader.LoadUsing(nameof(LoadAirSquadDefinitions))]
		[Desc("Named compatibility-aware air squad definitions. Empty preserves stock air squad assignment.")]
		public readonly Dictionary<string, AirSquadDefinition> AirSquadDefinitions = new Dictionary<string, AirSquadDefinition>();

		static object LoadAirSquadDefinitions(MiniYaml yaml)
		{
			var ret = new Dictionary<string, AirSquadDefinition>();
			var definitions = yaml.Nodes.FirstOrDefault(n => n.Key == "AirSquadDefinitions");
			if (definitions != null)
				foreach (var d in definitions.Value.Nodes)
					ret[d.Key] = new AirSquadDefinition(d.Value);

			return ret;
		}

		[FieldLoader.LoadUsing(nameof(LoadStealthSquadDefinitions))]
		[Desc("Named ground-specialist squad definitions owned by this manager.")]
		public readonly Dictionary<string, StealthSquadDefinition> StealthSquadDefinitions =
			new Dictionary<string, StealthSquadDefinition>();

		static object LoadStealthSquadDefinitions(MiniYaml yaml)
		{
			var ret = new Dictionary<string, StealthSquadDefinition>();
			var definitions = yaml.Nodes.FirstOrDefault(n => n.Key == "StealthSquadDefinitions");
			if (definitions != null)
				foreach (var definition in definitions.Value.Nodes)
					ret[definition.Key] = new StealthSquadDefinition(definition.Value);

			return ret;
		}

		[Desc("Actor types that are valid for naval squads.")]
		public readonly HashSet<string> NavalUnitsTypes = new HashSet<string>();

		[Desc("Actor types that are excluded from ground attacks.")]
		public readonly HashSet<string> AirUnitsTypes = new HashSet<string>();

		[Desc("Actor types that should generally be excluded from attack squads.")]
		public readonly HashSet<string> ExcludeFromSquadsTypes = new HashSet<string>();

		[Desc("Combat actor types this module may retain under its bounded aggressive AttackMove fallback.",
			"Active reservations still take priority, so approved specialist squads keep their members.")]
		public readonly HashSet<string> FailsafeDirectCombatTypes = new HashSet<string>();

		[Desc("Maximum ticks between degraded fallback reconsiderations. Unchanged active orders are not reissued.")]
		public readonly int FailsafeReconsiderInterval = 75;

		[Desc("Use only the bounded direct AttackMove fallback while advanced squads are disabled.",
			"This is intended for isolated baseline benchmarks, not normal bot definitions.")]
		public readonly bool SimpleAttackMoveFallbackWhenDisabled = false;

		[Desc("Test-only unsynced advanced-work pressure in milliseconds. Leave at zero outside isolated failsafe evidence maps.")]
		public readonly int FailsafeTestAdvancedWorkMilliseconds = 0;
		[Desc("First world tick for test-only advanced-work pressure.")]
		public readonly int FailsafeTestAdvancedWorkFromTick = 0;
		[Desc("Exclusive final world tick for test-only advanced-work pressure. Zero leaves it unbounded.")]
		public readonly int FailsafeTestAdvancedWorkUntilTick = 0;

		[Desc("Actor types that are considered construction yards (base builders).")]
		public readonly HashSet<string> ConstructionYardTypes = new HashSet<string>();

		[Desc("Enemy building types around which to scan for targets for naval squads.")]
		public readonly HashSet<string> NavalProductionTypes = new HashSet<string>();

		[Desc("Own actor types that are prioritized when defending.")]
		public readonly HashSet<string> ProtectionTypes = new HashSet<string>();

		[Desc("Minimum number of units AI must have before attacking.")]
		public readonly int SquadSize = 8;

		[Desc("Random number of up to this many units is added to squad size when creating an attack squad.")]
		public readonly int SquadSizeRandomBonus = 30;

		[Desc("Delay (in ticks) between giving out orders to units.")]
		public readonly int AssignRolesInterval = 50;

		[Desc("Delay (in ticks) between attempting rush attacks.")]
		public readonly int RushInterval = 600;

		[Desc("Delay (in ticks) between updating squads.")]
		public readonly int AttackForceInterval = 75;

		[Desc("Minimum delay (in ticks) between creating squads.")]
		public readonly int MinimumAttackForceDelay = 0;

		[Desc("Radius in cells around enemy BaseBuilder (Construction Yard) where AI scans for targets to rush.")]
		public readonly int RushAttackScanRadius = 15;

		[Desc("Radius in cells around the base that should be scanned for units to be protected.")]
		public readonly int ProtectUnitScanRadius = 15;

		[Desc("Maximum distance in cells from center of the base when checking for MCV deployment location.",
			"Only applies if RestrictMCVDeploymentFallbackToBase is enabled and there's at least one construction yard.")]
		public readonly int MaxBaseRadius = 20;

		[Desc("Radius in cells that squads should scan for enemies around their position while idle.")]
		public readonly int IdleScanRadius = 10;

		[Desc("Radius in cells that squads should scan for danger around their position to make flee decisions.")]
		public readonly int DangerScanRadius = 10;

		[Desc("Radius in cells that attack squads should scan for enemies around their position when trying to attack.")]
		public readonly int AttackScanRadius = 12;

		[Desc("Radius in cells that protecting squads should scan for enemies around their position.")]
		public readonly int ProtectionScanRadius = 8;

		[Desc("Enemy target types to never target.")]
		public readonly BitSet<TargetableType> IgnoredEnemyTargetTypes = default(BitSet<TargetableType>);

		[Desc("Air squads score every enemy actor they find and attack the highest scoring one.",
			"Score awarded to an enemy harvester. Harvesters are the softest worthwhile target,",
			"so this should normally be the highest of the AirTarget*Value fields.")]
		public readonly int AirTargetHarvesterValue = 500;

		[Desc("Score awarded to an enemy production building or refinery.")]
		public readonly int AirTargetProductionValue = 350;

		[Desc("Score awarded to any other enemy building.")]
		public readonly int AirTargetBuildingValue = 150;

		[Desc("Score awarded to line-build wall targets before generic building classification.")]
		public readonly int AirTargetWallValue = 1;

		[Desc("Score awarded to an enemy mobile unit.")]
		public readonly int AirTargetUnitValue = 100;

		[Desc("Score deducted per enemy anti-air capable actor sharing a scanned area (DangerScanRadius) with a candidate target.",
			"Raise this to make air squads more scared of SAM sites and mobile AA.")]
		public readonly int AirTargetAntiAirPenalty = 300;

		[Desc("Score deducted per cell of distance between the air squad and a candidate target.")]
		public readonly int AirTargetDistancePenalty = 1;

		[Desc("Aircraft speed used as the neutral point for air-target travel-time scoring.",
			"Slower squads pay proportionally more distance cost and faster squads pay less.")]
		public readonly int AirTargetReferenceSpeed = 160;

		[Desc("Additional percentage of distance cost applied at full ammo, scaled linearly by magazine fullness.",
			"This makes ready aircraft prefer nearby targets they can engage immediately.")]
		public readonly int AirTargetFullAmmoDistanceBonus = 100;

		[Desc("Percentage of a defended strategic cell's total target value credited to a killable AA actor.",
			"The credit is divided by remaining AA danger, so removing a small screen can unlock a rich cell",
			"without making dense SAM clusters attractive.")]
		public readonly int AirTargetAaClearUnlockPercent = 100;

		[Desc("Completed target scans without an undefended cell before an air squad may consider deliberately",
			"attacking an AA actor. Zero allows immediate consideration.")]
		public readonly int AirTargetAaClearFallbackScans = 0;

		[Desc("Minimum ratio of ammo-weighted aircraft cost to the summed cost of AA covering the target",
			"before deliberate AA clearing is eligible. Zero disables the value-ratio requirement.")]
		public readonly int AirTargetAaClearValueRatio = 0;

		[Desc("Number of eligible AA-clearing opportunities with the lowest summed effectiveness-times-value danger",
			"that remain in contention. The one unlocking the most target value is selected from this shortlist.",
			"Zero preserves ordinary score-only selection.")]
		public readonly int AirTargetAaClearWeakestCandidates = 0;

		[Desc("Maximum distance in cells between air-squad formation centers when combining their value",
			"and orders for a deliberate AA-clearing attack. Zero keeps AA clearing squad-local.")]
		public readonly int AirTargetAaClearSupportRadius = 0;

		[Desc("Actor types whose air-target priority increases while most non-AA targets are covered by AA.")]
		public readonly HashSet<string> AirTargetPowerActors = new HashSet<string>();

		[Desc("Percentage of attackable non-AA targets that must be covered before power-target priority starts rising.",
			"At or below this threshold the authored priority is used; at 100% coverage the configured maximum is used.")]
		public readonly int AirTargetPowerCoverageThresholdPercent = 100;

		[Desc("Maximum priority assigned to AirTargetPowerActors at 100% AA coverage. Zero disables the boost.")]
		public readonly int AirTargetPowerPriorityMaximum = 0;

		[Desc("Minimum score a candidate must reach before an air squad commits to attacking it.",
			"Candidates scoring below this are ignored and the squad stays idle.")]
		public readonly int AirTargetMinimumScore = 1;

		[Desc("Minimum percentage by which a periodic replacement target must outscore the current target.")]
		public readonly int AirTargetSwitchImprovementPercent = 50;

		[Desc("Ticks without distance or damage progress before an armed air squad replans its target.")]
		public readonly int AirTargetStallTicks = 150;

		[Desc("Number of nearest air target candidates routed per strategic scan.")]
		public readonly int AirTargetClosestCandidates = 15;

		[Desc("Number of highest-value air target candidates routed per strategic scan. These are combined",
			"with AirTargetClosestCandidates and deduplicated, so distant opportunities remain eligible.")]
		public readonly int AirTargetHighestValueCandidates = 10;

		[Desc("Additional nearest strategic cells containing harvesters that are always included in air target evaluation.")]
		public readonly int AirTargetHarvesterCandidates = 0;

		[Desc("Write air target scores and route decisions to debug.log.")]
		public readonly bool AirTargetDebugLogging = false;

		[Desc("Ticks between adaptive air-risk observations. Zero disables adaptation and preserves authored behavior.")]
		public readonly int AirAdaptiveRiskInterval = 0;

		[Desc("Ticks of adaptive risk history consulted after an enemy-caused aircraft loss.")]
		public readonly int AirAdaptiveRiskRollbackTicks = 1500;

		[Desc("Minimum live aircraft in a profile before readiness can increase its adaptive risk.")]
		public readonly int AirAdaptiveRiskMinimumUnits = 3;

		[Desc("Adaptive risk basis points gained per observation when every Apache is at full ammo.")]
		public readonly int AirAdaptiveRiskApacheFullAmmoGrowth = 0;

		[Desc("Adaptive risk basis points gained per observation when every Orca is at full ammo.")]
		public readonly int AirAdaptiveRiskOrcaFullAmmoGrowth = 0;

		[Desc("Adaptive risk basis points lost per observation while a profile is below its minimum force size.")]
		public readonly int AirAdaptiveRiskLowUnitDecay = 0;

		[Desc("Adaptive risk basis points credited per value point of an enemy killed by an Apache.")]
		public readonly int AirAdaptiveRiskApacheKillGrowth = 0;

		[Desc("Adaptive risk basis points credited per value point of an enemy killed by an Orca.")]
		public readonly int AirAdaptiveRiskOrcaKillGrowth = 0;

		[Desc("Additional adaptive risk basis points removed for every enemy-caused aircraft loss.")]
		public readonly int AirAdaptiveRiskLossDecrement = 0;

		[Desc("Width and height in map cells of one coarse air influence-map cell.")]
		public readonly int AirInfluenceCellSize = 6;

		[Desc("Ticks between rebuilding the shared strategic air influence map.")]
		public readonly int AirInfluenceCacheInterval = 125;

		[Desc("Extra score for a candidate that has no weapon able to shoot at aircraft.",
			"Aircraft do poor damage to structures, so this is what makes an undefended harvester or tank",
			"outrank a building rather than merely score near it. Zero keeps the stock behaviour.")]
		public readonly int AirTargetDefencelessBonus = 0;

		[Desc("Delay (in ticks) between anti-air safety checks for air squads. Unlike target scoring this",
			"runs regardless of squad state, so aircraft keep watching for anti-air while approaching a",
			"target, while attacking it and on the way home. Zero disables it and restores the stock",
			"behaviour of only checking when a target is selected.")]
		public readonly int AirSafetyCheckInterval = 0;
		[Desc("Ticks between lightweight local safety checks for ground stealth squads.")]
		public readonly int StealthSafetyCheckInterval = 25;
		[Desc("Ticks between bounded live checks of an owned stealth Crush or Kite target.")]
		public readonly int StealthLiveTargetCheckInterval = 12;
		[Desc("Ticks between live pending-Blue-explosion checks for ground stealth squads.")]
		public readonly int StealthBlueSafetyCheckInterval = 5;

		[Desc("Radius in cells around an air squad that is scanned for anti-air by the safety check.")]
		public readonly int AirThreatScanRadius = 12;

		[Desc("An air squad retreats when the number of anti-air actors near it, multiplied by this,",
			"exceeds the number of aircraft in the squad. Higher values make air squads more cowardly.")]
		public readonly int AirThreatFleeMultiplier = 3;

		[Desc("How long (in ticks) an air squad remembers where it saw enemy anti-air.")]
		public readonly int AirThreatMemoryTicks = 900;

		[Desc("Maximum number of remembered anti-air sightings per air squad.")]
		public readonly int AirThreatMemorySize = 12;

		[Desc("Anti-air sightings closer together than this many cells are merged into one remembered",
			"threat, so a cluster of SAM sites cannot flood the memory.")]
		public readonly int AirThreatMemoryMergeRadius = 3;

		[Desc("Minimum delay (in ticks) between successive retreat orders for the same air squad.")]
		public readonly int AirRetreatOrderInterval = 50;

		[Desc("Cost multiplier for anti-air influence encountered by coarse A* routes. Zero makes routes",
			"prefer distance alone.")]
		public readonly int AirRouteThreatPenalty = 0;

		[Desc("Half-width in cells of the flight corridor checked for anti-air by AirRouteThreatPenalty.")]
		public readonly int AirRouteThreatRadius = 8;

		[Desc("Legacy maximum number of aircraft in one generic air squad. Named AirSquadDefinitions use",
			"their own MaximumSize. Zero means unlimited.")]
		public readonly int AirSquadSize = 0;

		[Desc("Maximum number of air squads that may exist at once. Zero means unlimited.",
			"Each air squad costs its own target scan and anti-air safety check, so this is the knob that",
			"bounds the CPU cost of air behaviour regardless of how many aircraft the bot builds.")]
		public readonly int MaximumAirSquads = 0;

		[Desc("Distance in cells an air squad hops away from the nearest anti-air it knows about when it",
			"breaks off a run. Small values keep harassment local - the squad slips out, re-scans and comes",
			"straight back in, instead of flying across the map to one of its own buildings.",
			"Zero restores the stock behaviour of retreating to an own building.")]
		public readonly int AirEvadeDistance = 0;

		[Desc("Random lateral spread in cells added to every evasion hop, so successive hops work their way",
			"around the outside of an enemy base instead of shuttling along the same line. It is also the",
			"whole move when the squad has no remembered threat to run from and just wants to re-scan from",
			"somewhere else. Zero disables the wander.")]
		public readonly int AirEvadeJitter = 0;

		[Desc("Multiplier applied to a defender's own anti-air weapon range when deciding whether it is",
			"close enough to count as covering a position. The bot's scans use flat radii unrelated to any",
			"specific weapon's range, so this is what makes the danger zone around a long-range SAM wider",
			"than the zone around a short-range gun, both discovered by the same scan. 1.5 means a squad",
			"treats a defender as dangerous out to 150% of its actual weapon range.")]
		public readonly float AirThreatRangeBuffer = 1.5f;

		[Desc("Optional actor-specific overrides for derived anti-air threat weight. Zero makes that actor",
			"irrelevant to air routing, destination danger, and local flee checks.")]
		public readonly Dictionary<string, float> AirThreatWeightOverrides = new Dictionary<string, float>();

		[Desc("Consecutive AirIdleState scans (each AttackForceInterval ticks apart) that find no target",
			"scoring above AirTargetMinimumScore before the squad stops waiting for an undefended target",
			"and instead accepts the best finite-cost route. Anti-air costs remain in force, but this is better",
			"than idling forever when the whole enemy base is defended. Zero disables this",
			"and restores the stock behaviour of idling indefinitely.")]
		public readonly int AirMassedAttackIdleThreshold = 0;

		[Desc("An aircraft below this fraction of its maximum health (0-1) breaks off and returns to the",
			"nearest building that can repair its type, the same way SendHomeToResupply already does for",
			"ammo. Zero disables this.")]
		public readonly float HealthRetreatThreshold = 0f;

		[Desc("Allied actor types whose passive repair aura may be used by damaged air units.",
			"Allied actors are never entered or reserved; the aircraft waits within their configured aura instead.")]
		public readonly HashSet<string> AirPassiveRepairActors = new HashSet<string>();

		[Desc("Use bounded strategic-cell target selection for one cohesive general ground attack squad.")]
		public readonly bool UseStrategicGroundTargeting = false;

		[Desc("Keep one general ground attack squad and attach later eligible units as reinforcements.")]
		public readonly bool UseCohesiveGroundSquad = false;

		[Desc("Width and height in map cells of one coarse ground strategic cell.")]
		public readonly int GroundInfluenceCellSize = 6;

		[Desc("Ticks between rebuilding the enemy list shared by strategic ground squads.")]
		public readonly int GroundInfluenceCacheInterval = 125;

		[Desc("Nearest, highest-value, and nearest harvester cells evaluated by each ground scan.")]
		public readonly int GroundTargetClosestCandidates = 12;
		public readonly int GroundTargetHighestValueCandidates = 8;
		public readonly int GroundTargetHarvesterCandidates = 8;

		[Desc("Generic strategic target values. GroundTargetPriority overrides individual actor types.")]
		public readonly int GroundTargetHarvesterValue = 7000;
		public readonly int GroundTargetProductionValue = 3500;
		public readonly int GroundTargetBuildingValue = 2500;
		public readonly int GroundTargetUnitValue = 1000;
		public readonly int GroundTargetDefencelessBonus = 2500;

		[Desc("Per-actor strategic ground target values. Values in one coarse cell are summed.")]
		public readonly Dictionary<string, int> GroundTargetPriority = new Dictionary<string, int>();

		[Desc("Distance cost per cell, neutral movement speed, geometric defender decay percentage,",
			"and attacker-to-defender ratio considered effectively undefended.")]
		public readonly int GroundTargetDistancePenalty = 8;
		public readonly int GroundTargetReferenceSpeed = 100;
		public readonly int GroundDefenderOvermatchDecayPercent = 50;
		public readonly int GroundEffectivelyUndefendedRatio = 5;

		[Desc("Cells from the established formation center at which an incoming ground reinforcement joins it.")]
		public readonly int GroundReinforcementJoinRadius = 5;

		[Desc("Minimum incoming units before the formation waits to regroup. Zero derives half SquadSize.")]
		public readonly int GroundReinforcementHoldMinimum = 0;

		[Desc("Incoming-unit count as a percentage of formation size that triggers a regroup hold.")]
		public readonly int GroundReinforcementHoldRatioPercent = 100;

		[Desc("Extra finite priority and lifetime for anti-air actors marked by an air squad's clearing plan.")]
		public readonly int GroundAirMarkedAaBonus = 7500;
		public readonly int GroundAirMarkedAaDuration = 500;

		[Desc("Write strategic ground target, formation, and air-mark decisions to debug.log.")]
		public readonly bool GroundTargetDebugLogging = false;

		[Desc("Per-actor-type target score overrides for Orca-type air squads (squads containing at least",
			"one actor of type OrcaArchetypeActor), keyed by target ActorName - same shape as UnitsToBuild",
			"elsewhere in this mod. Checked before the generic AirTarget*Value classification; actor types",
			"not listed here still fall back to it. Empty disables per-archetype scoring for this squad type.")]
		public readonly Dictionary<string, int> AirTargetPriorityOrca = new Dictionary<string, int>();

		[Desc("As AirTargetPriorityOrca, for Heli-type air squads (squads containing at least one actor of",
			"type HeliArchetypeActor).")]
		public readonly Dictionary<string, int> AirTargetPriorityHeli = new Dictionary<string, int>();

		[Desc("Actor type that identifies a squad as the \"Orca\" archetype for AirTargetPriorityOrca.")]
		public readonly string OrcaArchetypeActor = "orca";

		[Desc("Actor type that identifies a squad as the \"Heli\" archetype for AirTargetPriorityHeli.")]
		public readonly string HeliArchetypeActor = "heli";

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			foreach (var definition in StealthSquadDefinitions.Values)
				definition.Validate(rules);

			var specialistSquadCount = StealthSquadDefinitions.Values.Sum(definition =>
				definition.MaximumHarassmentGroups + (definition.IncludeAttackGroup ? 1 : 0));
			if (specialistSquadCount > StealthAISpecialistPolicy.MaximumSquadCount)
				throw new YamlException($"StealthSquadDefinitions configure {specialistSquadCount} squads; " +
					$"the aggregate maximum is {StealthAISpecialistPolicy.MaximumSquadCount}.");

			if (DangerScanRadius <= 0)
				throw new YamlException("DangerScanRadius must be greater than zero.");

			if (AirTargetClosestCandidates < 0 || AirTargetHighestValueCandidates < 0 ||
				AirTargetClosestCandidates + AirTargetHighestValueCandidates <= 0)
				throw new YamlException("At least one air target candidate count must be greater than zero.");

			if (AirTargetHarvesterCandidates < 0)
				throw new YamlException("AirTargetHarvesterCandidates must not be negative.");

			if (AirInfluenceCellSize <= 0)
				throw new YamlException("AirInfluenceCellSize must be greater than zero.");

			if (AirInfluenceCacheInterval <= 0)
				throw new YamlException("AirInfluenceCacheInterval must be greater than zero.");

			if (AirTargetReferenceSpeed <= 0)
				throw new YamlException("AirTargetReferenceSpeed must be greater than zero.");

			if (AirTargetWallValue < 0 || AirTargetFullAmmoDistanceBonus < 0 || AirTargetAaClearUnlockPercent < 0 ||
				AirTargetAaClearFallbackScans < 0 || AirTargetAaClearValueRatio < 0 ||
				AirTargetAaClearWeakestCandidates < 0 || AirTargetAaClearSupportRadius < 0)
				throw new YamlException("Air target ammo-distance and AA-clear modifiers must not be negative.");

			if (AirTargetPowerCoverageThresholdPercent < 0 || AirTargetPowerCoverageThresholdPercent > 100 ||
				AirTargetPowerPriorityMaximum < 0)
				throw new YamlException("Air target power coverage settings must use a 0-100 threshold and non-negative priority.");

			if (AirTargetSwitchImprovementPercent < 0 || AirTargetStallTicks <= 0)
				throw new YamlException("Air target switch improvement must be non-negative and the stall timeout must be positive.");

			if (AirAdaptiveRiskInterval < 0 || AirAdaptiveRiskRollbackTicks < 0 || AirAdaptiveRiskMinimumUnits < 0 ||
				AirAdaptiveRiskApacheFullAmmoGrowth < 0 || AirAdaptiveRiskOrcaFullAmmoGrowth < 0 ||
				AirAdaptiveRiskLowUnitDecay < 0 || AirAdaptiveRiskApacheKillGrowth < 0 ||
				AirAdaptiveRiskOrcaKillGrowth < 0 || AirAdaptiveRiskLossDecrement < 0)
				throw new YamlException("Adaptive air-risk settings must not be negative.");

			if (AirSafetyCheckInterval > 0 && AirThreatScanRadius <= 0)
				throw new YamlException("AirThreatScanRadius must be greater than zero when AirSafetyCheckInterval is set.");
			if (StealthSafetyCheckInterval < 0 || StealthLiveTargetCheckInterval < 0 ||
				StealthBlueSafetyCheckInterval < 0)
				throw new YamlException("Stealth safety intervals must not be negative.");

			if (AirThreatFleeMultiplier <= 0)
				throw new YamlException("AirThreatFleeMultiplier must be greater than zero.");

			if (AirThreatMemorySize < 0)
				throw new YamlException("AirThreatMemorySize must not be negative.");

			if (AirRouteThreatPenalty != 0 && AirRouteThreatRadius <= 0)
				throw new YamlException("AirRouteThreatRadius must be greater than zero when AirRouteThreatPenalty is set.");

			if (AirSquadSize < 0)
				throw new YamlException("AirSquadSize must not be negative.");

			if (MaximumAirSquads < 0)
				throw new YamlException("MaximumAirSquads must not be negative.");

			foreach (var definition in AirSquadDefinitions)
			{
				if (definition.Value.SquadCount <= 0)
					throw new YamlException($"Air squad definition '{definition.Key}' must have a positive SquadCount.");
				if (definition.Value.MaximumSize < 0)
					throw new YamlException($"Air squad definition '{definition.Key}' must not have a negative MaximumSize.");
			}

			if (AirEvadeDistance < 0)
				throw new YamlException("AirEvadeDistance must not be negative.");

			if (AirEvadeJitter < 0)
				throw new YamlException("AirEvadeJitter must not be negative.");

			if (AirThreatRangeBuffer <= 0)
				throw new YamlException("AirThreatRangeBuffer must be greater than zero.");

			if (AirThreatWeightOverrides.Any(kv => kv.Value < 0))
				throw new YamlException("AirThreatWeightOverrides values must not be negative.");

			if (AirMassedAttackIdleThreshold < 0)
				throw new YamlException("AirMassedAttackIdleThreshold must not be negative.");

			if (HealthRetreatThreshold < 0 || HealthRetreatThreshold >= 1)
				throw new YamlException("HealthRetreatThreshold must be at least zero and less than one.");

			if (GroundInfluenceCellSize <= 0 || GroundInfluenceCacheInterval <= 0 || GroundTargetReferenceSpeed <= 0 ||
				GroundEffectivelyUndefendedRatio <= 0 || GroundReinforcementJoinRadius <= 0 ||
				GroundReinforcementHoldMinimum < 0 || GroundReinforcementHoldRatioPercent <= 0)
				throw new YamlException("Ground strategic cell, cache, speed, overmatch, and reinforcement values must be positive.");

			if (GroundTargetClosestCandidates < 0 || GroundTargetHighestValueCandidates < 0 ||
				GroundTargetHarvesterCandidates < 0 ||
				GroundTargetClosestCandidates + GroundTargetHighestValueCandidates <= 0)
				throw new YamlException("At least one ground target candidate count must be greater than zero.");

			if (GroundTargetDistancePenalty < 0 || GroundDefenderOvermatchDecayPercent < 0 ||
				GroundDefenderOvermatchDecayPercent > 100 || GroundTargetDefencelessBonus < 0 ||
				GroundAirMarkedAaBonus < 0 || GroundAirMarkedAaDuration < 0)
				throw new YamlException("Ground strategic penalties, percentages, and air-mark values are invalid.");

			if (FailsafeReconsiderInterval <= 0)
				throw new YamlException("FailsafeReconsiderInterval must be greater than zero.");
			if (FailsafeTestAdvancedWorkMilliseconds < 0 || FailsafeTestAdvancedWorkFromTick < 0 ||
				FailsafeTestAdvancedWorkUntilTick < 0 || (FailsafeTestAdvancedWorkUntilTick > 0 &&
					FailsafeTestAdvancedWorkUntilTick <= FailsafeTestAdvancedWorkFromTick))
				throw new YamlException("Failsafe test pressure and tick bounds must be non-negative and valid.");
			if (FailsafeDirectCombatTypes.Any(t => ExcludeFromSquadsTypes.Contains(t) || NavalUnitsTypes.Contains(t) ||
				(!SimpleAttackMoveFallbackWhenDisabled && AirUnitsTypes.Contains(t))))
				throw new YamlException("FailsafeDirectCombatTypes cannot include excluded or naval actor types; " +
					"air actors require SimpleAttackMoveFallbackWhenDisabled.");
			foreach (var actorName in FailsafeDirectCombatTypes)
				if (!rules.Actors.TryGetValue(actorName, out var actor) || !actor.HasTraitInfo<AttackBaseInfo>())
					throw new YamlException($"FailsafeDirectCombatTypes actor '{actorName}' must exist and have an attack trait.");

			foreach (var actorName in AirPassiveRepairActors)
			{
				if (!rules.Actors.TryGetValue(actorName, out var actor) ||
					!actor.TraitInfos<GrantConditionInRangeInfo>().Any(t => t.Granter &&
						t.ValidRelationships.HasRelationship(PlayerRelationship.Ally)))
					throw new YamlException($"AirPassiveRepairActors actor '{actorName}' must grant an allied condition in range.");
			}
		}

		public override object Create(ActorInitializer init) { return new SquadManagerBotModule(init.Self, this); }
	}

	public class SquadManagerBotModule : ConditionalTrait<SquadManagerBotModuleInfo>, IBotEnabled, IBotTick, IBotRespondToAttack,
		IBotPositionsUpdated, INotifyKilled, INotifyAppliedDamage, IGameSaveTraitData, IAdvancedBotTick,
		IAdvancedBotPlanningThrottle, IAdvancedBotFailsafeWindowDiagnostics
	{
		public CPos GetRandomBaseCenter()
		{
			var randomConstructionYard = World.Actors.Where(a => a.Owner == Player &&
				Info.ConstructionYardTypes.Contains(a.Info.Name))
				.RandomOrDefault(World.LocalRandom);

			return randomConstructionYard?.Location ?? initialBaseCenter;
		}

		public readonly World World;
		public readonly Player Player;
		internal readonly GeneralizedCombatThreatCalculator CombatThreatCalculator;

		readonly Predicate<Actor> unitCannotBeOrdered;
		readonly List<Actor> unitsHangingAroundTheBase = new List<Actor>();
		readonly Dictionary<string, AdaptiveAirRiskController> adaptiveAirRisk =
			new Dictionary<string, AdaptiveAirRiskController>(StringComparer.OrdinalIgnoreCase);
		static readonly Dictionary<Ruleset, GeneralizedCombatThreatCalculator> CombatThreatCalculators =
			new Dictionary<Ruleset, GeneralizedCombatThreatCalculator>();
		readonly Dictionary<uint, int> airMarkedGroundTargets = new Dictionary<uint, int>();
		readonly Dictionary<uint, (string Definition, int Index)> stealthSquadAssignments =
			new Dictionary<uint, (string Definition, int Index)>();
		readonly HashSet<(string Definition, int Index)> expiredStealthSquadSlots =
			new HashSet<(string Definition, int Index)>();
		int nextStealthSquadGenerationId = 1;
		int stealthManagerAllowanceTick = -1;
		bool stealthManagerAllowanceConsumed;
		string stealthManagerRoundRobinDefinition;
		int stealthManagerRoundRobinIndex = -1;
		int stealthManagerRoundRobinKind = -1;
		readonly StealthManagerAttributionCounter[] stealthManagerAttribution =
			Enumerable.Range(0, (int)StealthManagerAttributionPhase.Count)
				.Select(_ => new StealthManagerAttributionCounter()).ToArray();
		int stealthManagerAttributionWindowStartTick;
		readonly StealthSquadOverlayPublisher stealthOverlayPublisher =
			new StealthSquadOverlayPublisher();

		// Units that the bot already knows about. Any unit not on this list needs to be given a role.
		readonly List<Actor> activeUnits = new List<Actor>();

		public List<Squad> Squads = new List<Squad>();

		const int StealthCatchUpWorkKind = 0;
		const int StealthLiveLocalPlanningWorkKind = 1;

		internal static long BeginStealthManagerAttributionPhase()
		{
			return Game.Settings.Debug.BotDebug ? Stopwatch.GetTimestamp() : 0;
		}

		internal void RecordStealthManagerAttributionPhase(
			StealthManagerAttributionPhase phase, long started, int operations)
		{
			if (!Game.Settings.Debug.BotDebug)
				return;

			var counter = stealthManagerAttribution[(int)phase];
			counter.ElapsedTicks += Math.Max(0, Stopwatch.GetTimestamp() - started);
			counter.Calls++;
			counter.Operations += Math.Max(0, operations);
		}

		internal void AddStealthManagerAttributionOperations(
			StealthManagerAttributionPhase phase, int operations)
		{
			if (!Game.Settings.Debug.BotDebug)
				return;

			stealthManagerAttribution[(int)phase].Operations += Math.Max(0, operations);
		}

		void EmitStealthManagerAttribution(string summary, int windowTicks, string transition)
		{
			if (!Game.Settings.Debug.BotDebug)
				return;

			string Phase(StealthManagerAttributionPhase phase)
			{
				var counter = stealthManagerAttribution[(int)phase];
				var milliseconds = 1000d * counter.ElapsedTicks / Stopwatch.Frequency;
				return string.Format("{0:0.###}/{1}/{2}", milliseconds,
					counter.Calls, counter.Operations);
			}

			var emissionStarted = BeginStealthManagerAttributionPhase();
			Log.Write("debug", "stealth_manager_phase_attribution|owner={0}|bot_id={1}|summary={2}|tick={3}|" +
				"window_start_tick={4}|window_ticks={5}|transition={6}|units=milliseconds/calls/operations|" +
				"manager_tick={7}|efficiency_telemetry={8}|ensure_stealth_squads={9}|" +
				"rebalance_stealth_squads={10}|recruit_unassigned={11}|assign_roles={12}|" +
				"scheduler_selection={13}|guard_dirty_check={14}|incremental_path={15}|" +
				"dependency_validation={16}|threat_route_cell={17}|local_planning_inclusive={18}|" +
				"diagnostic_emission={19}|overlap=manager-and-local-inclusive,child-phases-nested|" +
				"diagnostic_only=true", Player.PlayerName, Player.PlayerActor.ActorID,
				summary, World.WorldTick,
				stealthManagerAttributionWindowStartTick, windowTicks, transition,
				Phase(StealthManagerAttributionPhase.ManagerTick),
				Phase(StealthManagerAttributionPhase.EfficiencyTelemetry),
				Phase(StealthManagerAttributionPhase.EnsureStealthSquads),
				Phase(StealthManagerAttributionPhase.RebalanceStealthSquads),
				Phase(StealthManagerAttributionPhase.RecruitUnassigned),
				Phase(StealthManagerAttributionPhase.AssignRoles),
				Phase(StealthManagerAttributionPhase.SchedulerSelection),
				Phase(StealthManagerAttributionPhase.GuardDirtyCheck),
				Phase(StealthManagerAttributionPhase.IncrementalPath),
				Phase(StealthManagerAttributionPhase.DependencyValidation),
				Phase(StealthManagerAttributionPhase.ThreatRouteCell),
				Phase(StealthManagerAttributionPhase.LocalPlanning),
				Phase(StealthManagerAttributionPhase.DiagnosticEmission));

			foreach (var counter in stealthManagerAttribution)
				counter.Clear();
			stealthManagerAttributionWindowStartTick = World.WorldTick;
			RecordStealthManagerAttributionPhase(
				StealthManagerAttributionPhase.DiagnosticEmission, emissionStarted, 1);
		}

		bool HasStealthCatchUpManagerWork(Squad squad)
		{
			return squad.Type == SquadType.Stealth && squad.StealthProfile == "stealth-tank" &&
				squad.AirFormationUnits().Count > 0 && squad.Units.Any(unit =>
					unit != null && !unit.IsDead && unit.IsInWorld &&
					squad.AirReinforcements.Contains(unit.ActorID) &&
					!squad.AirUnitsRepairing.Contains(unit.ActorID));
		}

		static bool IsRevealedIdleStealthSafetyMember(Squad squad, Actor unit,
			bool activeLocalMember, bool cloakRevealArmed)
		{
			var live = unit != null && !unit.IsDead && unit.IsInWorld;
			var revealed = live && unit.TraitsImplementing<Cloak>().Any(cloak => !cloak.Cloaked);
			return StealthAISpecialistPolicy.IsRevealedIdleSafetyEligible(
				cloakRevealArmed, activeLocalMember && squad.StealthProfile == "stealth-tank" &&
					live && unit.Info.Name == "stnk", live,
				live && squad.AirUnitsRepairing.Contains(unit.ActorID), live && unit.IsIdle, revealed);
		}

		static void RefreshStealthRevealedIdleSafetyDemand(Squad squad)
		{
			if (squad.UsesModularStealthLifecycle)
			{
				squad.StealthRevealedIdleSafetyCloakArmed.Clear();
				squad.StealthRevealedIdleSafetyPending.Clear();
				squad.StealthRevealedIdleSafetyRequested = false;
				return;
			}

			var owned = squad.Type == SquadType.Stealth ? squad.Units.Where(unit => unit != null &&
				!unit.IsDead && unit.IsInWorld && unit.Info.Name == "stnk")
				.OrderBy(unit => unit.ActorID).ToArray() : Array.Empty<Actor>();
			var ownedIds = new HashSet<uint>(owned.Select(unit => unit.ActorID));
			var activeLocalIds = new HashSet<uint>(squad.Type == SquadType.Stealth ?
				squad.AirFormationUnits().Where(unit => unit != null && !unit.IsDead && unit.IsInWorld)
					.Select(unit => unit.ActorID) : Enumerable.Empty<uint>());
			squad.StealthRevealedIdleSafetyCloakArmed.RemoveWhere(actorId => !ownedIds.Contains(actorId));
			squad.StealthRevealedIdleSafetyPending.RemoveWhere(actorId =>
			{
				var unit = owned.FirstOrDefault(actor => actor.ActorID == actorId);
				return !IsRevealedIdleStealthSafetyMember(squad, unit,
					activeLocalIds.Contains(actorId), true);
			});
			foreach (var unit in owned)
			{
				var cloaked = unit.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked);
				if (cloaked)
				{
					squad.StealthRevealedIdleSafetyCloakArmed.Add(unit.ActorID);
					continue;
				}

				if (!squad.StealthRevealedIdleSafetyCloakArmed.Remove(unit.ActorID))
					continue;
				if (IsRevealedIdleStealthSafetyMember(squad, unit,
					activeLocalIds.Contains(unit.ActorID), true))
					squad.StealthRevealedIdleSafetyPending.Add(unit.ActorID);
			}

			squad.StealthRevealedIdleSafetyRequested =
				squad.StealthRevealedIdleSafetyPending.Count > 0;
		}

		internal void RegisterStealthOwnershipTransferLocalReview(Squad squad)
		{
			if (squad.UsesModularStealthLifecycle)
				return;

			squad.StealthLocalSafetyRequested = true;
			squad.StealthLiveTargetRequested = true;
			RegisterStealthManagerWorkDemand(squad, StealthLiveLocalPlanningWorkKind);
		}

		static int StealthManagerWorkRequestedTick(Squad squad, int kind)
		{
			if (kind == StealthCatchUpWorkKind)
				return squad.StealthCatchUpWorkRequestedTick;
			return squad.StealthLocalPlanningWorkRequestedTick;
		}

		void RegisterStealthManagerWorkDemand(Squad squad, int kind)
		{
			if (StealthManagerWorkRequestedTick(squad, kind) >= 0)
				return;

			if (kind == StealthCatchUpWorkKind)
				squad.StealthCatchUpWorkRequestedTick = World.WorldTick;
			else
				squad.StealthLocalPlanningWorkRequestedTick = World.WorldTick;
		}

		static void ClearStealthManagerWorkDemand(Squad squad, int kind)
		{
			if (kind == StealthCatchUpWorkKind)
				squad.StealthCatchUpWorkRequestedTick = -1;
			else
				squad.StealthLocalPlanningWorkRequestedTick = -1;
		}

		void RefreshStealthManagerWorkDemands()
		{
			foreach (var squad in Squads)
			{
				if (squad.UsesModularStealthLifecycle)
				{
					squad.StealthLocalSafetyRequested = false;
					squad.StealthLiveTargetRequested = false;
					squad.StealthBlueSafetyRequested = false;
					squad.StealthRevealedIdleSafetyRequested = false;
					ClearStealthManagerWorkDemand(squad, StealthCatchUpWorkKind);
					ClearStealthManagerWorkDemand(squad, StealthLiveLocalPlanningWorkKind);
					continue;
				}

				RefreshStealthRevealedIdleSafetyDemand(squad);
				if (HasStealthCatchUpManagerWork(squad))
					RegisterStealthManagerWorkDemand(squad, StealthCatchUpWorkKind);
				else
					ClearStealthManagerWorkDemand(squad, StealthCatchUpWorkKind);

				if (squad.Type == SquadType.Stealth && (squad.StealthRevealedIdleSafetyRequested ||
					squad.StealthLocalSafetyRequested ||
					squad.StealthLiveTargetRequested || squad.StealthBlueSafetyRequested))
					RegisterStealthManagerWorkDemand(squad, StealthLiveLocalPlanningWorkKind);
				else
					ClearStealthManagerWorkDemand(squad, StealthLiveLocalPlanningWorkKind);
			}
		}

		bool TryConsumeStealthManagerAllowance(Squad requester, int requestedKind)
		{
			var attributionStarted = BeginStealthManagerAttributionPhase();
			try
			{
			RegisterStealthManagerWorkDemand(requester, requestedKind);
			if (stealthManagerAllowanceTick != World.WorldTick)
			{
				stealthManagerAllowanceTick = World.WorldTick;
				stealthManagerAllowanceConsumed = false;
			}

			if (stealthManagerAllowanceConsumed)
				return false;

			var eligible = Squads.Where(squad => squad.Type == SquadType.Stealth &&
				!squad.UsesModularStealthLifecycle).SelectMany(squad =>
			{
				var work = new List<(Squad Squad, int Kind, int DueTick)>();
				if (HasStealthCatchUpManagerWork(squad))
					work.Add((squad, StealthCatchUpWorkKind,
						squad.StealthCatchUpWorkRequestedTick));
				if (squad.StealthRevealedIdleSafetyRequested || squad.StealthLocalSafetyRequested ||
					squad.StealthLiveTargetRequested ||
					squad.StealthBlueSafetyRequested)
					work.Add((squad, StealthLiveLocalPlanningWorkKind,
						squad.StealthLocalPlanningWorkRequestedTick));
				return work;
			}).ToArray();
			if (Game.Settings.Debug.BotDebug)
				AddStealthManagerAttributionOperations(
					StealthManagerAttributionPhase.SchedulerSelection, eligible.Length);
			if (eligible.Length == 0)
				return false;

			// Age is reset only by service or observed ineligibility, so continuously denied action work
			// outranks newer demand. The established cursor remains the deterministic fairness tie-break.
			var oldestDueTick = eligible.Min(work => work.DueTick);
			var oldestWork = eligible.Where(work => work.DueTick == oldestDueTick)
				.OrderBy(work => work.Squad.StealthSquadDefinition, StringComparer.Ordinal)
				.ThenBy(work => work.Squad.StealthSquadIndex)
				.ThenBy(work => work.Kind).ToArray();
			if (Game.Settings.Debug.BotDebug)
				AddStealthManagerAttributionOperations(
					StealthManagerAttributionPhase.SchedulerSelection, oldestWork.Length);

			var selected = oldestWork.FirstOrDefault(work => stealthManagerRoundRobinDefinition == null ||
				string.CompareOrdinal(work.Squad.StealthSquadDefinition,
					stealthManagerRoundRobinDefinition) > 0 ||
				(work.Squad.StealthSquadDefinition == stealthManagerRoundRobinDefinition &&
					(work.Squad.StealthSquadIndex > stealthManagerRoundRobinIndex ||
						(work.Squad.StealthSquadIndex == stealthManagerRoundRobinIndex &&
							work.Kind > stealthManagerRoundRobinKind))));
			if (selected.Squad == null)
				selected = oldestWork[0];
			if (requester != selected.Squad || requestedKind != selected.Kind)
				return false;

			stealthManagerAllowanceConsumed = true;
			stealthManagerRoundRobinDefinition = selected.Squad.StealthSquadDefinition;
			stealthManagerRoundRobinIndex = selected.Squad.StealthSquadIndex;
			stealthManagerRoundRobinKind = selected.Kind;
			ClearStealthManagerWorkDemand(selected.Squad, selected.Kind);
			return true;
			}
			finally
			{
				RecordStealthManagerAttributionPhase(
					StealthManagerAttributionPhase.SchedulerSelection,
					attributionStarted, 0);
			}
		}

		internal bool TryConsumeStealthCatchUpRoutingAllowance(Squad requester)
		{
			return TryConsumeStealthManagerAllowance(requester, StealthCatchUpWorkKind);
		}

		bool TryConsumeStealthLiveLocalPlanningAllowance(Squad requester)
		{
			return TryConsumeStealthManagerAllowance(requester, StealthLiveLocalPlanningWorkKind);
		}

		IBot bot;
		IBotPositionsUpdated[] notifyPositionsUpdated;
		IBotNotifyIdleBaseUnits[] notifyIdleBaseUnits;
		IBotTransportReservations[] transportReservations;
		IBotUnitReservations[] unitReservations;
		IBotTemporaryUnitControl[] temporaryUnitControls;
		IUnassignedCombatUnitRegistry unassignedCombatUnits;

		CPos initialBaseCenter;

		int rushTicks;
		int assignRolesTicks;
		int attackForceTicks;
		int strategicSquadUpdateCycles = 1;
		int minAttackForceDelayTicks;
		int airSafetyTicks;
		int stealthSafetyTicks;
		int stealthLiveTargetTicks;
		int stealthBlueSafetyTicks;
		int adaptiveAirRiskTicks;
		int stealthRecruitTicks;
		int planningIntervalFactor = 1;
		bool advancedBehaviorEnabled = true;
		int fallbackReconsiderTicks;
		Actor fallbackTarget;
		readonly HashSet<uint> fallbackOrderedActors = new HashSet<uint>();
		readonly Dictionary<uint, CPos> fallbackOrderTargets = new Dictionary<uint, CPos>();
		readonly BotModules.AdvancedBotFallbackOwnership releasedFallbackOwnership =
			new BotModules.AdvancedBotFallbackOwnership();
		long stealthEfficiencyKillValue;
		long stealthEfficiencyActorTicks;
		long stealthEfficiencyDamageTaken;
		readonly HashSet<uint> stealthEfficiencyActors = new HashSet<uint>();
		readonly Dictionary<int, StealthEfficiencyWindow> stealthGenerationEfficiency =
			new Dictionary<int, StealthEfficiencyWindow>();
		readonly Dictionary<int, StealthCadenceGenerationRecord> stealthCadenceGenerations =
			new Dictionary<int, StealthCadenceGenerationRecord>();
		int stealthEfficiencyNextReportTick;
		int stealthEfficiencyWindowStartTick;
		bool stealthEfficiencyTerminalReported;
		bool stealthEfficiencyTerminalSubscribed;

		public SquadManagerBotModule(Actor self, SquadManagerBotModuleInfo info)
			: base(info)
		{
			World = self.World;
			Player = self.Owner;
			if (!CombatThreatCalculators.TryGetValue(World.Map.Rules, out CombatThreatCalculator))
			{
				CombatThreatCalculator = new GeneralizedCombatThreatCalculator(
					World.Map.Rules, World.Map.Grid.SubCellOffsets);
				CombatThreatCalculators.Add(World.Map.Rules, CombatThreatCalculator);
			}

			unitCannotBeOrdered = a => a == null || a.Owner != Player || a.IsDead || !a.IsInWorld;

			if (info.AirAdaptiveRiskInterval > 0)
			{
				var historyCapacity = Math.Max(2, info.AirAdaptiveRiskRollbackTicks / info.AirAdaptiveRiskInterval + 2);
				adaptiveAirRisk.Add("Apache", new AdaptiveAirRiskController(historyCapacity));
				adaptiveAirRisk.Add("Orca", new AdaptiveAirRiskController(historyCapacity));
			}
		}

		internal float AirRiskMultiplier(string profile)
		{
			return adaptiveAirRisk.TryGetValue(profile, out var controller) ?
				controller.MultiplierBasisPoints / (float)AdaptiveAirRiskController.BasisPointsPerMultiplier : 1f;
		}

		internal void MarkGroundTargetForAirSupport(Actor actor)
		{
			if (!Info.UseStrategicGroundTargeting || Info.GroundAirMarkedAaBonus <= 0 ||
				Info.GroundAirMarkedAaDuration <= 0 || !IsPreferredEnemyUnit(actor))
				return;

			var expiry = World.WorldTick + Info.GroundAirMarkedAaDuration;
			if (!airMarkedGroundTargets.TryGetValue(actor.ActorID, out var currentExpiry) || currentExpiry < expiry)
				airMarkedGroundTargets[actor.ActorID] = expiry;

			if (Info.GroundTargetDebugLogging)
				Log.Write("debug", "Ground air mark [{0}] {1}#{2}: bonus={3} expires={4}.",
					Player.PlayerName, actor.Info.Name, actor.ActorID, Info.GroundAirMarkedAaBonus, expiry);
		}

		internal int GroundAirTargetBonus(Actor actor)
		{
			if (actor == null || !airMarkedGroundTargets.TryGetValue(actor.ActorID, out var expiry))
				return 0;

			if (actor.IsDead || !actor.IsInWorld || World.WorldTick >= expiry)
			{
				airMarkedGroundTargets.Remove(actor.ActorID);
				return 0;
			}

			return Info.GroundAirMarkedAaBonus;
		}

		// Use for proactive targeting.
		public bool IsPreferredEnemyUnit(Actor a)
		{
			if (a == null || a.IsDead || Player.RelationshipWith(a.Owner) != PlayerRelationship.Enemy || a.Info.HasTraitInfo<HuskInfo>() || a.Info.HasTraitInfo<AircraftInfo>())
				return false;

			var targetTypes = a.GetEnabledTargetTypes();
			return !targetTypes.IsEmpty && !targetTypes.Overlaps(Info.IgnoredEnemyTargetTypes);
		}

		public bool IsNotHiddenUnit(Actor a)
		{
			var hasModifier = false;
			var visModifiers = a.TraitsImplementing<IVisibilityModifier>();
			foreach (var v in visModifiers)
			{
				if (v.IsVisible(a, Player))
					return true;

				hasModifier = true;
			}

			return !hasModifier;
		}

		protected override void Created(Actor self)
		{
			notifyPositionsUpdated = self.Owner.PlayerActor.TraitsImplementing<IBotPositionsUpdated>().ToArray();
			notifyIdleBaseUnits = self.Owner.PlayerActor.TraitsImplementing<IBotNotifyIdleBaseUnits>().ToArray();
			transportReservations = self.Owner.PlayerActor.TraitsImplementing<IBotTransportReservations>().ToArray();
			unitReservations = self.Owner.PlayerActor.TraitsImplementing<IBotUnitReservations>().ToArray();
			temporaryUnitControls = self.Owner.PlayerActor.TraitsImplementing<IBotTemporaryUnitControl>().ToArray();
			unassignedCombatUnits = self.Owner.PlayerActor.TraitOrDefault<IUnassignedCombatUnitRegistry>();
		}

		protected override void TraitEnabled(Actor self)
		{
			stealthEfficiencyWindowStartTick = World.WorldTick;
			stealthEfficiencyTerminalReported = false;
			UpdateStealthEfficiencyTerminalSubscription();

			// Avoid all AIs trying to rush in the same tick, randomize their initial rush a little.
			var smallFractionOfRushInterval = Info.RushInterval / 20;
			rushTicks = World.LocalRandom.Next(Info.RushInterval - smallFractionOfRushInterval, Info.RushInterval + smallFractionOfRushInterval);

			// Avoid all AIs reevaluating assignments on the same tick, randomize their initial evaluation delay.
			assignRolesTicks = World.LocalRandom.Next(0, Info.AssignRolesInterval);
			attackForceTicks = World.LocalRandom.Next(0, Info.AttackForceInterval);
			minAttackForceDelayTicks = World.LocalRandom.Next(0, Info.MinimumAttackForceDelay);

			// Spread the air safety checks of all the bots across the interval instead of spiking on one tick.
			if (Info.AirSafetyCheckInterval > 0)
				airSafetyTicks = World.LocalRandom.Next(0, Info.AirSafetyCheckInterval);
			if (Info.StealthSafetyCheckInterval > 0)
				stealthSafetyTicks = World.LocalRandom.Next(0, Info.StealthSafetyCheckInterval);
			if (Info.StealthLiveTargetCheckInterval > 0)
				stealthLiveTargetTicks = World.LocalRandom.Next(0, Info.StealthLiveTargetCheckInterval);
			if (Info.StealthBlueSafetyCheckInterval > 0)
				stealthBlueSafetyTicks = World.LocalRandom.Next(0, Info.StealthBlueSafetyCheckInterval);

			if (Info.AirAdaptiveRiskInterval > 0)
				adaptiveAirRiskTicks = Info.AirAdaptiveRiskInterval;
		}

		protected override void TraitDisabled(Actor self)
		{
			UpdateStealthEfficiencyTerminalSubscription();
		}

		void IBotEnabled.BotEnabled(IBot bot)
		{
			this.bot = bot;
			UpdateStealthEfficiencyTerminalSubscription();
			EnsureStealthSquads(bot);
			stealthRecruitTicks = 1;
		}

		void UpdateStealthEfficiencyTerminalSubscription()
		{
			var shouldSubscribe = StealthAISpecialistPolicy.ShouldOwnStealthEfficiencyTerminal(
				bot != null, !IsTraitDisabled);
			if (shouldSubscribe == stealthEfficiencyTerminalSubscribed)
				return;

			if (shouldSubscribe)
				World.GameEnding += EmitTerminalStealthWatchdogSummaries;
			else
				World.GameEnding -= EmitTerminalStealthWatchdogSummaries;
			stealthEfficiencyTerminalSubscribed = shouldSubscribe;
		}

		void IBotTick.BotTick(IBot bot)
		{
			var attributionStarted = BeginStealthManagerAttributionPhase();
			try
			{
				var phaseStarted = BeginStealthManagerAttributionPhase();
				UpdateStealthEfficiencyTelemetry();
				RecordStealthManagerAttributionPhase(
					StealthManagerAttributionPhase.EfficiencyTelemetry, phaseStarted, 1);
				phaseStarted = BeginStealthManagerAttributionPhase();
				EnsureStealthSquads(bot);
				RecordStealthManagerAttributionPhase(
					StealthManagerAttributionPhase.EnsureStealthSquads, phaseStarted, 1);
				if (advancedBehaviorEnabled && --stealthRecruitTicks <= 0)
				{
					phaseStarted = BeginStealthManagerAttributionPhase();
					RebalanceStealthSquads();
					RecordStealthManagerAttributionPhase(
						StealthManagerAttributionPhase.RebalanceStealthSquads, phaseStarted, 1);
					stealthRecruitTicks = Info.StealthSquadDefinitions.Count == 0 ? int.MaxValue :
						StrategicPlanningInterval(Info.StealthSquadDefinitions.Values.Min(
							definition => definition.ScanInterval));
				}

				phaseStarted = BeginStealthManagerAttributionPhase();
				RecruitUnassignedCombatUnits(bot);
				RecordStealthManagerAttributionPhase(
					StealthManagerAttributionPhase.RecruitUnassigned, phaseStarted, 1);
				phaseStarted = BeginStealthManagerAttributionPhase();
				if (advancedBehaviorEnabled)
				{
					RunFailsafeTestPressure();
					AssignRolesToIdleUnits(bot);
				}
				else
					AssignRolesToIdleUnitsDegraded(bot);
				RecordStealthManagerAttributionPhase(
					StealthManagerAttributionPhase.AssignRoles, phaseStarted, 1);
				stealthOverlayPublisher.Publish(this, bot);
			}
			finally
			{
				RecordStealthManagerAttributionPhase(
					StealthManagerAttributionPhase.ManagerTick, attributionStarted, 1);
			}
		}

		void UpdateStealthEfficiencyTelemetry()
		{
			var live = World.Actors.Where(actor => actor.Owner == Player && actor.IsInWorld &&
				!actor.IsDead && actor.Info.Name == "stnk").OrderBy(actor => actor.ActorID).ToArray();
			stealthEfficiencyActorTicks = StealthAISpecialistPolicy.AccumulateActorTicks(
				stealthEfficiencyActorTicks, live.Length);
			stealthEfficiencyActors.UnionWith(live.Select(actor => actor.ActorID));
			foreach (var squad in Squads.Where(s => s.Type == SquadType.Stealth &&
				s.StealthKillCadenceGeneration != null))
			{
				var generation = squad.StealthKillCadenceGeneration;
				if (!stealthGenerationEfficiency.TryGetValue(generation.GenerationId, out var generationWindow))
				{
					generationWindow = new StealthEfficiencyWindow(generation.GenerationStartTick);
					stealthGenerationEfficiency.Add(generation.GenerationId, generationWindow);
				}

				generationWindow.Observe(squad.Units.Where(unit => unit != null && unit.Owner == Player &&
					unit.IsInWorld && !unit.IsDead && unit.Info.Name == "stnk").Select(unit => unit.ActorID));
			}

			if (World.WorldTick < stealthEfficiencyNextReportTick)
				return;

			stealthEfficiencyNextReportTick = World.WorldTick + Math.Max(1, 60000 / World.Timestep);
			EmitStealthEfficiencySummary("periodic");
		}

		void EmitTerminalStealthWatchdogSummaries()
		{
			if (!StealthAISpecialistPolicy.TryBeginStealthTerminalSummary(
				ref stealthEfficiencyTerminalReported, bot != null, !IsTraitDisabled))
				return;

			foreach (var squad in Squads.Where(squad => squad.Type == SquadType.Stealth)
				.OrderBy(squad => squad.StealthSquadDefinition, StringComparer.Ordinal)
				.ThenBy(squad => squad.StealthSquadIndex))
				StealthAIStateBase.EmitStealthRecurringDiagnosticSummary(squad, "terminal");
			EmitTerminalStealthCadenceSummaries();
			EmitTerminalStealthGenerationEfficiencySummaries();
			EmitStealthEfficiencySummary("terminal");
			EmitStealthManagerAttribution("terminal",
				Math.Max(0, World.WorldTick - stealthManagerAttributionWindowStartTick), "game-ending");
		}

		void EmitTerminalStealthGenerationEfficiencySummaries()
		{
			foreach (var pair in stealthGenerationEfficiency.OrderBy(entry => entry.Key))
				EmitStealthGenerationEfficiencySummary(pair.Key, pair.Value, "terminal");
		}

		void EmitStealthGenerationEfficiencySummary(int generationId,
			StealthEfficiencyWindow window, string summary)
		{
			var attributionStarted = BeginStealthManagerAttributionPhase();
			try
			{
				Log.Write("debug", "Stealth efficiency control membership owner={0} bot_id={1} " +
					"control=bot generation={2} generation-start={3} generation-end={4} kills={5} members=[{6}] " +
					"actor-time-denominator=sum-live-member-ticks summary={7} diagnostic_only=true.",
					Player.PlayerName, Player.PlayerActor.ActorID, generationId, window.StartTick, World.WorldTick,
					window.KillCount, window.Actors.Select(id => "stnk#" + id).JoinWith(","), summary);
				if (Game.Settings.Debug.BotDebug)
					AddStealthManagerAttributionOperations(
						StealthManagerAttributionPhase.DiagnosticEmission, 1);
				Log.Write("debug", StealthAISpecialistPolicy.FormatStealthEfficiencySummary(
					summary + "-generation-" + generationId, Player.PlayerActor.ActorID,
					window.StartTick, World.WorldTick, window.Summary()));
				if (Game.Settings.Debug.BotDebug)
					AddStealthManagerAttributionOperations(
						StealthManagerAttributionPhase.DiagnosticEmission, 1);
			}
			finally
			{
				RecordStealthManagerAttributionPhase(
					StealthManagerAttributionPhase.DiagnosticEmission,
					attributionStarted, 0);
			}
		}

		void EmitTerminalStealthCadenceSummaries()
		{
			foreach (var record in stealthCadenceGenerations.Values
				.OrderBy(record => record.Generation.GenerationId))
			{
				var generation = record.Generation;
				var squad = Squads.SingleOrDefault(s => s.Type == SquadType.Stealth &&
					s.StealthKillCadenceGeneration?.GenerationId == generation.GenerationId);
				EmitStealthCadenceSummary(record, squad, "terminal");
			}
		}

		void EmitStealthCadenceSummary(StealthCadenceGenerationRecord record, Squad squad, string summary)
		{
			var generation = record.Generation;
			var stnks = squad?.Units.Where(unit => unit != null && !unit.IsDead && unit.IsInWorld &&
				unit.Info.Name == "stnk").Distinct().OrderBy(unit => unit.ActorID).ToArray();
			stnks = stnks ?? Array.Empty<Actor>();
			generation.Observe(World.WorldTick, stnks.Length > 0);
			var maximumTicks = Math.Max(1, 45000 / Math.Max(1, World.Timestep));
			var cadenceFailed = generation.CadenceFailed || generation.MismatchFailed ||
				StealthAISpecialistPolicy.KillCadenceFailed(generation.CadenceAge, maximumTicks);
			var status = cadenceFailed ? "failure" : stnks.Length == 0 ? "exempt" : "pass";
			var attributionStarted = BeginStealthManagerAttributionPhase();
			try
			{
				Log.Write("debug", "Stealth kill watchdog [stealth-tank] squad result: owner={0} tick={1} " +
					"generation={2} generation-start={3} window-start={4} squad={5}#{6} " +
					"cadence-age={7}/{8} generation-kills={9} stnks={10} formation={11} " +
					"reinforcements={12} members=[{13}] cadence-failed={14} status={15} summary={16} " +
					"retained-generations={17}.",
					Player.PlayerName, World.WorldTick, generation.GenerationId, generation.GenerationStartTick,
					generation.WindowStartTick, record.SquadDefinition, record.SquadIndex,
					generation.CadenceAge, maximumTicks, generation.AttributedKills, stnks.Length,
					squad?.AirFormationUnits().Count ?? 0, squad?.AirReinforcements.Count ?? 0,
					stnks.Select(unit => unit.Info.Name + "#" + unit.ActorID).JoinWith(","),
					cadenceFailed, status, summary, stealthCadenceGenerations.Count);
				if (Game.Settings.Debug.BotDebug)
					AddStealthManagerAttributionOperations(
						StealthManagerAttributionPhase.DiagnosticEmission, 1);
			}
			finally
			{
				RecordStealthManagerAttributionPhase(
					StealthManagerAttributionPhase.DiagnosticEmission,
					attributionStarted, 0);
			}
		}

		void FinalizeStealthGenerationDiagnostics(Squad squad, string summary)
		{
			StealthAIStateBase.EmitStealthRecurringDiagnosticSummary(squad, summary);
			var generation = squad.StealthKillCadenceGeneration;
			if (generation == null)
				return;

			if (StealthAISpecialistPolicy.TryTakeStealthGeneration(stealthCadenceGenerations,
				generation.GenerationId, out var cadence))
				EmitStealthCadenceSummary(cadence, squad, summary);
			if (StealthAISpecialistPolicy.TryTakeStealthGeneration(stealthGenerationEfficiency,
				generation.GenerationId, out var efficiency))
				EmitStealthGenerationEfficiencySummary(generation.GenerationId, efficiency, summary);
		}

		void FinalizeOrphanedLoadedStealthGenerationDiagnostics()
		{
			var activeGenerationIds = Squads.Where(squad => squad.Type == SquadType.Stealth &&
				squad.StealthKillCadenceGeneration != null)
				.Select(squad => squad.StealthKillCadenceGeneration.GenerationId).ToHashSet();
			var orphanedIds = stealthCadenceGenerations.Keys.Concat(stealthGenerationEfficiency.Keys)
				.Where(id => !activeGenerationIds.Contains(id)).Distinct().OrderBy(id => id).ToArray();
			foreach (var generationId in orphanedIds)
			{
				if (StealthAISpecialistPolicy.TryTakeStealthGeneration(stealthCadenceGenerations,
					generationId, out var cadence))
					EmitStealthCadenceSummary(cadence, null, "retired-load");
				if (StealthAISpecialistPolicy.TryTakeStealthGeneration(stealthGenerationEfficiency,
					generationId, out var efficiency))
					EmitStealthGenerationEfficiencySummary(generationId, efficiency, "retired-load");
			}
		}

		void RegisterStealthCadenceGeneration(Squad squad)
		{
			var generation = squad.StealthKillCadenceGeneration;
			if (generation == null)
				return;

			stealthCadenceGenerations[generation.GenerationId] = new StealthCadenceGenerationRecord(
				squad.StealthSquadDefinition, squad.StealthSquadIndex, generation);
			if (!stealthGenerationEfficiency.ContainsKey(generation.GenerationId))
				stealthGenerationEfficiency.Add(generation.GenerationId,
					new StealthEfficiencyWindow(generation.GenerationStartTick));
		}

		void EmitStealthEfficiencySummary(string summary)
		{
			var attributionStarted = BeginStealthManagerAttributionPhase();
			try
			{
				var metric = StealthAISpecialistPolicy.StealthEfficiency(
					stealthEfficiencyKillValue, stealthEfficiencyActorTicks,
					stealthEfficiencyDamageTaken, stealthEfficiencyActors.Count);
				Log.Write("debug", StealthAISpecialistPolicy.FormatStealthEfficiencySummary(
					summary, Player.PlayerActor.ActorID, stealthEfficiencyWindowStartTick,
					World.WorldTick, metric));
				if (Game.Settings.Debug.BotDebug)
					AddStealthManagerAttributionOperations(
						StealthManagerAttributionPhase.DiagnosticEmission, 1);
			}
			finally
			{
				RecordStealthManagerAttributionPhase(
					StealthManagerAttributionPhase.DiagnosticEmission,
					attributionStarted, 0);
			}
		}

		void RunFailsafeTestPressure()
		{
			if (Info.FailsafeTestAdvancedWorkMilliseconds == 0 || World.WorldTick < Info.FailsafeTestAdvancedWorkFromTick ||
				(Info.FailsafeTestAdvancedWorkUntilTick > 0 && World.WorldTick >= Info.FailsafeTestAdvancedWorkUntilTick))
				return;

			var deadline = Stopwatch.GetTimestamp() +
				(long)Info.FailsafeTestAdvancedWorkMilliseconds * Stopwatch.Frequency / 1000;
			while (Stopwatch.GetTimestamp() < deadline)
			{
			}
		}

		string IAdvancedBotTick.FailsafeModuleId => "SquadManagerBotModule";

		void IAdvancedBotPlanningThrottle.SetPlanningIntervalFactor(int factor)
		{
			var next = Math.Max(1, factor);
			if (planningIntervalFactor == next)
				return;

			var increasing = next > planningIntervalFactor;
			planningIntervalFactor = next;
			AdjustPlanningTimer(ref rushTicks, Info.RushInterval, increasing);
			AdjustPlanningTimer(ref assignRolesTicks, Info.AssignRolesInterval, increasing);
			AdjustPlanningTimer(ref minAttackForceDelayTicks, Info.MinimumAttackForceDelay, increasing);
			AdjustPlanningTimer(ref adaptiveAirRiskTicks, Info.AirAdaptiveRiskInterval, increasing);
			strategicSquadUpdateCycles = increasing ? Math.Max(strategicSquadUpdateCycles, next) :
				Math.Min(strategicSquadUpdateCycles, next);
			if (Info.StealthSquadDefinitions.Count != 0)
				AdjustPlanningTimer(ref stealthRecruitTicks,
					Info.StealthSquadDefinitions.Values.Min(definition => definition.ScanInterval), increasing);
		}

		internal int StrategicPlanningInterval(int interval)
		{
			return interval <= 0 ? interval : (int)Math.Min(int.MaxValue, (long)interval * planningIntervalFactor);
		}

		void AdjustPlanningTimer(ref int timer, int interval, bool increasing)
		{
			if (interval <= 0 || timer == int.MaxValue)
				return;

			var adjusted = StrategicPlanningInterval(interval);
			timer = increasing ? Math.Max(timer, adjusted) : Math.Min(timer, adjusted);
		}

		void IAdvancedBotFailsafeWindowDiagnostics.EmitAdvancedFailsafeWindowDiagnostics(
			int sampleInterval, string transition)
		{
			EmitStealthManagerAttribution("failsafe-window", sampleInterval, transition);
		}

		void IAdvancedBotTick.SetAdvancedBehaviorEnabled(bool enabled)
		{
			if (advancedBehaviorEnabled == enabled)
				return;

			advancedBehaviorEnabled = enabled;
			fallbackReconsiderTicks = 0;
			fallbackTarget = null;
			fallbackOrderedActors.Clear();
			fallbackOrderTargets.Clear();
			if (!enabled)
				ReleaseStealthSquads("failsafe-degraded");
			else
				stealthRecruitTicks = 1;

			if (enabled && Info.GroundTargetDebugLogging)
			{
				var retained = releasedFallbackOwnership.Groups.SelectMany(g => g.Value)
					.Select(World.GetActorById).Where(a => !unitCannotBeOrdered(a)).OrderBy(a => a.ActorID).ToArray();
				if (retained.Length > 0)
					Log.Write("debug", "Squad failsafe handoff [{0}]: owner=SquadManagerBotModule state=ordinary-manager " +
						"actors={1}.", Player.PlayerName,
						string.Join(",", retained.Select(a => a.Info.Name + "#" + a.ActorID)));
			}
		}

		internal void RetainFailsafeReleasedActors(string source, IEnumerable<Actor> actors)
		{
			var released = actors.Where(a => !unitCannotBeOrdered(a)).OrderBy(a => a.ActorID).ToArray();
			unassignedCombatUnits?.RegisterReleasedActors(released);
			releasedFallbackOwnership.Retain(source, released.Select(a => a.ActorID));
			fallbackReconsiderTicks = 0;
			if (Info.GroundTargetDebugLogging && released.Length > 0)
				Log.Write("debug", "Squad failsafe handoff [{0}]: owner=SquadManagerBotModule source={1} retained={2} " +
					"actors={3}.", Player.PlayerName, source, released.Length,
					string.Join(",", released.Select(a => a.Info.Name + "#" + a.ActorID)));
		}

		internal Actor FindClosestEnemy(WPos pos)
		{
			var units = World.Actors.Where(IsPreferredEnemyUnit);
			return units.Where(IsNotHiddenUnit).ClosestTo(pos) ?? units.ClosestTo(pos);
		}

		Actor FindFallbackTarget(WPos pos)
		{
			if (!Info.SimpleAttackMoveFallbackWhenDisabled)
				return FindClosestEnemy(pos);

			var bases = World.ActorsHavingTrait<MustBeDestroyed>(t => t.Info.RequiredForShortGame)
				.Where(IsPreferredEnemyUnit);
			return bases.Where(IsNotHiddenUnit).ClosestTo(pos) ?? bases.ClosestTo(pos) ?? FindClosestEnemy(pos);
		}

		internal Actor FindClosestEnemy(WPos pos, WDist radius)
		{
			return World.FindActorsInCircle(pos, radius).Where(a => IsPreferredEnemyUnit(a) && IsNotHiddenUnit(a)).ClosestTo(pos);
		}

		void CleanSquads()
		{
			foreach (var id in airMarkedGroundTargets.Where(m => World.WorldTick >= m.Value)
				.Select(m => m.Key).ToList())
				airMarkedGroundTargets.Remove(id);

			Squads.RemoveAll(s => !s.IsValid && s.Type != SquadType.Stealth);
			foreach (var s in Squads)
			{
				s.Units.RemoveAll(a => unitCannotBeOrdered(a) || IsReservedForSpecialBehavior(a));
				if (s.Type == SquadType.Air || s.Type == SquadType.Stealth)
					s.CleanAirMembership();
				else if (s.Type == SquadType.GeneralAttack)
					s.CleanGroundMembership();
			}
		}

		void EnsureStealthSquads(IBot enabledBot)
		{
			foreach (var configured in Info.StealthSquadDefinitions.OrderBy(entry => entry.Key))
			{
				var count = configured.Value.MaximumHarassmentGroups +
					(configured.Value.IncludeAttackGroup ? 1 : 0);
				var configuredSquads = Squads.Where(s => s.Type == SquadType.Stealth &&
					s.StealthSquadDefinition == configured.Key).OrderBy(s => s.StealthSquadIndex).ToList();
				for (var i = 0; i < count; i++)
				{
					if (configuredSquads.Any(existing => existing.StealthSquadIndex == i) ||
						expiredStealthSquadSlots.Contains((configured.Key, i)))
						continue;

					var squad = RegisterNewSquad(enabledBot, SquadType.Stealth);
					squad.StealthSquadDefinition = configured.Key;
					squad.StealthSquadIndex = i;
					configuredSquads.Add(squad);
				}
			}
		}

		void RebalanceStealthSquads()
		{
			var remadeSquads = new HashSet<Squad>();

			// activeUnits is the manager-owned actor cache, while the registry admits newly produced
			// or released combat units before they are claimed. Keep the frequent specialist lifecycle
			// owner-bounded instead of materializing every actor in the world.
			var recruitmentActors = activeUnits
				.Concat(unassignedCombatUnits?.UnassignedActors ?? Array.Empty<Actor>())
				.Where(actor => actor != null)
				.Distinct()
				.OrderBy(actor => actor.ActorID)
				.ToList();
			foreach (var actorId in stealthSquadAssignments.Keys
				.Where(id => World.GetActorById(id) == null).ToList())
				stealthSquadAssignments.Remove(actorId);

			foreach (var configured in Info.StealthSquadDefinitions.OrderBy(entry => entry.Key))
			{
				var specialistSquads = Squads.Where(s => s.Type == SquadType.Stealth &&
					s.StealthSquadDefinition == configured.Key).OrderBy(s => s.StealthSquadIndex).ToList();
				var previousMembership = specialistSquads.SelectMany(s => s.Units.Select(a =>
					new { Actor = a, Squad = s })).ToDictionary(entry => entry.Actor.ActorID);
				var eligible = recruitmentActors.Where(a => !unitCannotBeOrdered(a) &&
					configured.Value.UnitTypes.Contains(a.Info.Name) && !IsReservedForTransport(a) &&
					!IsUnitTemporarilyControlled(a)).OrderBy(a => a.ActorID).ToList();
				var unassigned = eligible.Count(actor => !stealthSquadAssignments.TryGetValue(
					actor.ActorID, out var assignment) || assignment.Definition != configured.Key);
				foreach (var slot in expiredStealthSquadSlots.Where(slot => slot.Definition == configured.Key)
					.OrderBy(slot => slot.Index).Take(unassigned).ToArray())
				{
					var remade = RegisterNewSquad(bot, SquadType.Stealth);
					remade.StealthSquadDefinition = slot.Definition;
					remade.StealthSquadIndex = slot.Index;
					specialistSquads.Add(remade);
					remadeSquads.Add(remade);
					expiredStealthSquadSlots.Remove(slot);
				}

				specialistSquads = specialistSquads.OrderBy(squad => squad.StealthSquadIndex).ToList();
				foreach (var squad in specialistSquads)
				{
					if (squad.StealthKillCadenceGeneration != null)
						squad.StealthKillCadenceGeneration.Observe(World.WorldTick,
							squad.Units.Any(unit => !unit.IsDead && unit.IsInWorld && unit.Info.Name == "stnk"));
					squad.Units.Clear();
				}

				for (var i = 0; i < eligible.Count; i++)
				{
					var actor = eligible[i];
					var groupIndex = previousMembership.TryGetValue(actor.ActorID, out var membership) ?
						membership.Squad.StealthSquadIndex :
						stealthSquadAssignments.TryGetValue(actor.ActorID, out var assignment) &&
						assignment.Definition == configured.Key ? assignment.Index : specialistSquads
							.OrderBy(candidateSquad => candidateSquad.Units.Count)
							.ThenBy(candidateSquad => candidateSquad.StealthSquadIndex)
							.First().StealthSquadIndex;
					var assignedSquad = specialistSquads.FirstOrDefault(squad =>
						squad.StealthSquadIndex == groupIndex);
					if (assignedSquad == null)
						continue;

					stealthSquadAssignments[actor.ActorID] = (configured.Key, groupIndex);
					if (assignedSquad.StealthKillCadenceGeneration == null)
					{
						assignedSquad.StealthKillCadenceGeneration = new StealthKillCadenceGeneration(
							nextStealthSquadGenerationId++, World.WorldTick);
						if (Info.AirTargetDebugLogging)
							Log.Write("debug", "Stealth squad lifecycle [{0}] generation-start: tick={1} " +
								"generation={2} squad={3}#{4} member=stnk#{5} lifecycle={6} " +
								"cadence-window=fresh window-start={1}.", configured.Key, World.WorldTick,
								assignedSquad.StealthKillCadenceGeneration.GenerationId,
								assignedSquad.StealthSquadDefinition, assignedSquad.StealthSquadIndex,
								actor.ActorID, remadeSquads.Contains(assignedSquad) ? "remake" : "initial");
					}

					RegisterStealthCadenceGeneration(assignedSquad);

					assignedSquad.Units.Add(actor);
					if (!previousMembership.ContainsKey(actor.ActorID) && assignedSquad.Units.Count > 1)
						assignedSquad.MarkAirReinforcement(actor);
					foreach (var other in Squads.Where(other => other != assignedSquad))
						other.Units.Remove(actor);
					unitsHangingAroundTheBase.Remove(actor);
					if (!activeUnits.Contains(actor))
						activeUnits.Add(actor);
					unassignedCombatUnits?.ClaimActors(new[] { actor });
				}

				foreach (var squad in specialistSquads)
				{
					if (squad.IsValid || recruitmentActors.Any(actor => !unitCannotBeOrdered(actor) &&
						stealthSquadAssignments.TryGetValue(actor.ActorID, out var assignment) &&
						assignment.Definition == configured.Key && assignment.Index == squad.StealthSquadIndex))
					{
						squad.StealthEmptySinceTick = -1;
						continue;
					}

					if (squad.StealthEmptySinceTick < 0)
					{
						squad.StealthEmptySinceTick = World.WorldTick;
						continue;
					}

					if (World.WorldTick - squad.StealthEmptySinceTick < configured.Value.ScanInterval)
						continue;

					FinalizeStealthGenerationDiagnostics(squad, "retired");
					Squads.Remove(squad);
					expiredStealthSquadSlots.Add((configured.Key, squad.StealthSquadIndex));
					if (Info.AirTargetDebugLogging)
						Log.Write("debug", "Stealth squad lifecycle [{0}] empty timeout: tick={1} " +
							"generation={2} removed-squad={3}#{4} empty-age={5} transient-affinity=False " +
							"status=timed-out cadence-age={6}/{7} generation-kills={8} cadence-failed={9}.", configured.Key,
							World.WorldTick, squad.StealthKillCadenceGeneration?.GenerationId ?? 0,
							squad.StealthSquadDefinition, squad.StealthSquadIndex,
							World.WorldTick - squad.StealthEmptySinceTick, squad.StealthKillCadenceAge,
							Math.Max(1, 45000 / Math.Max(1, World.Timestep)),
							squad.StealthDebugKillCadenceKills, squad.StealthDebugKillCadenceFailed);
				}
			}
		}

		bool IsManagerOwnedSpecialist(Actor actor)
		{
			return actor != null && Squads.Any(s => s.Type == SquadType.Stealth && s.Units.Contains(actor));
		}

		void ReleaseStealthSquads(string reason)
		{
			foreach (var squad in Squads.Where(s => s.Type == SquadType.Stealth))
			{
				var released = squad.Units.Where(a => !unitCannotBeOrdered(a)).OrderBy(a => a.ActorID).ToArray();
				unassignedCombatUnits?.RegisterReleasedActors(released);
				if (reason == "failsafe-degraded")
					RetainFailsafeReleasedActors($"SquadManagerBotModule/{squad.StealthProfile}", released);
				squad.Units.Clear();
			}
		}

		bool IsReservedForTransport(Actor actor)
		{
			return transportReservations != null && transportReservations.Any(r => r.IsTransportReserved(actor));
		}

		bool IsReservedForSpecialBehavior(Actor actor)
		{
			return IsReservedForTransport(actor) ||
				(unitReservations != null && unitReservations.Any(r => r.IsUnitReserved(actor)));
		}

		internal bool IsUnitProtectingBase(Actor actor)
		{
			return actor != null && Squads.Any(s => s.Type == SquadType.Protection && s.Units.Contains(actor));
		}

		internal bool IsUnitTemporarilyControlled(Actor actor)
		{
			return actor != null && temporaryUnitControls != null &&
				temporaryUnitControls.Any(c => c.IsUnitTemporarilyControlled(actor));
		}

		internal bool TryGetGeneralGroundMission(Actor actor, out Actor target,
			out WPos formationCenter, out CPos objective, out bool urgent)
		{
			var status = GetGeneralGroundMissionStatus(actor, out target, out formationCenter, out objective);
			urgent = status == GeneralGroundMissionStatus.Urgent;
			return status == GeneralGroundMissionStatus.Ready || urgent;
		}

		internal GeneralGroundMissionStatus GetGeneralGroundMissionStatus(Actor actor, out Actor target,
			out WPos formationCenter, out CPos objective)
		{
			var squad = Squads.FirstOrDefault(s => s.Type == SquadType.GeneralAttack && s.Units.Contains(actor));
			if (squad == null)
			{
				target = null;
				formationCenter = WPos.Zero;
				objective = CPos.Zero;
				return GeneralGroundMissionStatus.MissingSquad;
			}

			formationCenter = squad.GroundFormationCenter;
			if (!squad.IsTargetValid)
			{
				target = null;
				objective = CPos.Zero;
				return GeneralGroundMissionStatus.InvalidTarget;
			}

			target = squad.TargetActor;
			objective = target.Location;
			return IsUnitProtectingBase(actor) || squad.FuzzyStateMachine.Is<GroundUnitsFleeState>() ?
				GeneralGroundMissionStatus.Urgent : GeneralGroundMissionStatus.Ready;
		}

		internal CPos GroundRecoveryDestination()
		{
			var squad = Squads.FirstOrDefault(s => s.Type == SquadType.GeneralAttack);
			if (squad?.IsTargetValid == true)
				return squad.TargetActor.Location;

			if (squad?.IsValid == true)
				return World.Map.CellContaining(squad.GroundFormationCenter);

			return initialBaseCenter;
		}

		// HACK: Use of this function requires that there is one squad of this type.
		Squad GetSquadOfType(SquadType type)
		{
			return Squads.FirstOrDefault(s => s.Type == type);
		}

		/// <summary>
		/// The compatible air squad a newly built aircraft should join. Named definitions are matched
		/// most-specific-first and balanced by current size; without definitions the stock caps apply.
		/// </summary>
		Squad GetAirSquadWithRoom(IBot bot, Actor actor)
		{
			if (Info.AirSquadDefinitions.Count > 0)
			{
				var compatible = Info.AirSquadDefinitions
					.Where(d => d.Value.UnitTypes.Count == 0 || d.Value.UnitTypes.Contains(actor.Info.Name))
					.OrderBy(d => d.Value.UnitTypes.Count == 0 ? int.MaxValue : d.Value.UnitTypes.Count)
					.ThenBy(d => d.Key).ToList();
				if (compatible.Count == 0)
					return null;

				var selected = compatible[0];
				var squads = Squads.Where(s => s.Type == SquadType.Air && s.AirSquadDefinition == selected.Key).ToList();
				if (squads.Count < selected.Value.SquadCount)
				{
					var created = RegisterNewSquad(bot, SquadType.Air);
					created.AirSquadDefinition = selected.Key;
					return created;
				}

				return squads.Where(s => selected.Value.MaximumSize <= 0 || s.Units.Count < selected.Value.MaximumSize)
					.OrderBy(s => s.Units.Count).FirstOrDefault();
			}

			var squadCount = 0;
			foreach (var s in Squads)
			{
				if (s.Type != SquadType.Air)
					continue;

				squadCount++;
				if (Info.AirSquadSize <= 0 || s.Units.Count < Info.AirSquadSize)
					return s;
			}

			if (Info.MaximumAirSquads > 0 && squadCount >= Info.MaximumAirSquads)
				return null;

			return RegisterNewSquad(bot, SquadType.Air);
		}

		Squad RegisterNewSquad(IBot bot, SquadType type, Actor target = null)
		{
			var ret = new Squad(bot, this, type, target);
			Squads.Add(ret);
			return ret;
		}

		internal int AdoptTransportedAssault(IBot bot, IEnumerable<Actor> transportedUnits, Actor preferredTarget)
		{
			var units = transportedUnits.Where(a => !unitCannotBeOrdered(a) &&
				!Info.ExcludeFromSquadsTypes.Contains(a.Info.Name) &&
				!Info.AirUnitsTypes.Contains(a.Info.Name) && !Info.NavalUnitsTypes.Contains(a.Info.Name) &&
				a.Info.HasTraitInfo<AttackBaseInfo>()).Distinct().OrderBy(a => a.ActorID).ToList();
			if (units.Count == 0)
				return 0;

			foreach (var squad in Squads)
				squad.Units.RemoveAll(units.Contains);

			unitsHangingAroundTheBase.RemoveAll(units.Contains);
			activeUnits.RemoveAll(units.Contains);
			activeUnits.AddRange(units);
			unassignedCombatUnits?.ClaimActors(units);

			var target = IsPreferredEnemyUnit(preferredTarget) ? preferredTarget :
				FindClosestEnemy(units.Select(a => a.CenterPosition).Average());
			var assault = RegisterNewSquad(bot, SquadType.Assault, target);
			assault.Units.AddRange(units);
			if (target != null)
			{
				bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(World, target.Location), false,
					groupedActors: units.ToArray()));
				assault.FuzzyStateMachine.ChangeState(assault, new GroundUnitsAttackMoveState(), true);
			}

			foreach (var n in notifyIdleBaseUnits)
				n.UpdatedIdleBaseUnits(unitsHangingAroundTheBase);

			return units.Count;
		}

		internal int RestoreTransportedUnits(IEnumerable<Actor> transportedUnits)
		{
			var units = transportedUnits.Where(a => !unitCannotBeOrdered(a) &&
				!Info.ExcludeFromSquadsTypes.Contains(a.Info.Name) &&
				!Info.AirUnitsTypes.Contains(a.Info.Name) && !Info.NavalUnitsTypes.Contains(a.Info.Name))
				.Distinct().OrderBy(a => a.ActorID).ToList();

			foreach (var squad in Squads)
				squad.Units.RemoveAll(units.Contains);

			unitsHangingAroundTheBase.RemoveAll(units.Contains);
			activeUnits.RemoveAll(units.Contains);
			unitsHangingAroundTheBase.AddRange(units);
			activeUnits.AddRange(units);
			unassignedCombatUnits?.ClaimActors(units);
			foreach (var n in notifyIdleBaseUnits)
				n.UpdatedIdleBaseUnits(unitsHangingAroundTheBase);

			return units.Count;
		}

		void AssignRolesToIdleUnits(IBot bot)
		{
			CleanSquads();
			RefreshStealthManagerWorkDemands();

			activeUnits.RemoveAll(unitCannotBeOrdered);
			activeUnits.RemoveAll(IsReservedForSpecialBehavior);
			unitsHangingAroundTheBase.RemoveAll(unitCannotBeOrdered);
			unitsHangingAroundTheBase.RemoveAll(IsReservedForSpecialBehavior);
			foreach (var n in notifyIdleBaseUnits)
				n.UpdatedIdleBaseUnits(unitsHangingAroundTheBase);

			// Record every due live-local request before any strategic state can consume the shared
			// allowance. Requests persist and coalesce by squad until serviced; scheduler denial does
			// not invalidate the activity already owned by the squad lifecycle.
			if (Info.StealthSafetyCheckInterval > 0 && --stealthSafetyTicks <= 0)
			{
				stealthSafetyTicks = Info.StealthSafetyCheckInterval;
				foreach (var squad in Squads.Where(squad => squad.Type == SquadType.Stealth))
				{
					if (squad.UsesModularStealthLifecycle)
					{
						squad.TickModularStealthLocalSafety();
						continue;
					}

					squad.StealthLocalSafetyRequested = true;
					RegisterStealthManagerWorkDemand(squad, StealthLiveLocalPlanningWorkKind);
				}
			}

			if (Info.StealthLiveTargetCheckInterval > 0 && --stealthLiveTargetTicks <= 0)
			{
				stealthLiveTargetTicks = Info.StealthLiveTargetCheckInterval;
				foreach (var squad in Squads.Where(squad => squad.Type == SquadType.Stealth &&
					!squad.UsesModularStealthLifecycle))
				{
					squad.StealthLiveTargetRequested = true;
					RegisterStealthManagerWorkDemand(squad, StealthLiveLocalPlanningWorkKind);
				}
			}

			if (Info.StealthBlueSafetyCheckInterval > 0 && --stealthBlueSafetyTicks <= 0)
			{
				stealthBlueSafetyTicks = Info.StealthBlueSafetyCheckInterval;
				foreach (var squad in Squads.Where(squad => squad.Type == SquadType.Stealth &&
					!squad.UsesModularStealthLifecycle))
				{
					squad.StealthBlueSafetyRequested = true;
					RegisterStealthManagerWorkDemand(squad, StealthLiveLocalPlanningWorkKind);
				}
			}

			if (--rushTicks <= 0)
			{
				rushTicks = StrategicPlanningInterval(Info.RushInterval);
				TryToRushAttack(bot);
			}

			if (--attackForceTicks <= 0)
			{
				attackForceTicks = Info.AttackForceInterval;
				var updateStrategicSquads = --strategicSquadUpdateCycles <= 0;
				if (updateStrategicSquads)
					strategicSquadUpdateCycles = planningIntervalFactor;
				foreach (var s in Squads)
					if (s.UsesModularStealthLifecycle || updateStrategicSquads)
						s.Update();
			}

			// Air squads re-check the anti-air around themselves far more often than the state machine
			// runs, so they can break off a run that has become lethal instead of dying on it.
			// PERF: one bounded circle scan per air squad per interval, and there is at most one air squad.
			if (Info.AirSafetyCheckInterval > 0 && --airSafetyTicks <= 0)
			{
				airSafetyTicks = Info.AirSafetyCheckInterval;
				foreach (var s in Squads.Where(s => s.Type == SquadType.Air))
					s.TickAirSafety();
			}

			foreach (var squad in Squads.Where(squad => squad.Type == SquadType.Stealth &&
				!squad.UsesModularStealthLifecycle)
				.OrderBy(squad => squad.StealthSquadDefinition, StringComparer.Ordinal)
				.ThenBy(squad => squad.StealthSquadIndex))
			{
				if (!squad.StealthRevealedIdleSafetyRequested && !squad.StealthLocalSafetyRequested &&
					!squad.StealthLiveTargetRequested &&
					!squad.StealthBlueSafetyRequested)
					continue;
				if (!TryConsumeStealthLiveLocalPlanningAllowance(squad))
					continue;

				var attributionStarted = BeginStealthManagerAttributionPhase();
				try
				{
				if (squad.StealthRevealedIdleSafetyRequested)
				{
					var complete = squad.TickStealthRevealedIdleSafety(out var repositionIssued);
					if (complete)
					{
						squad.StealthRevealedIdleSafetyRequested = false;
						squad.StealthRevealedIdleSafetyPending.Clear();
					}

					if (!complete || repositionIssued)
					{
						if (repositionIssued)
						{
							squad.StealthLocalSafetyRequested = false;
							squad.StealthLiveTargetRequested = false;
							squad.StealthBlueSafetyRequested = false;
						}

						continue;
					}
				}

				var runSafety = squad.StealthLocalSafetyRequested;
				var runLiveTarget = squad.StealthLiveTargetRequested;
				var runBlueSafety = squad.StealthBlueSafetyRequested;
				squad.StealthLocalSafetyRequested = false;
				squad.StealthLiveTargetRequested = false;
				squad.StealthBlueSafetyRequested = false;
				if (runSafety)
					squad.TickAirSafety();
				else if (runBlueSafety)
					squad.TickStealthBlueSafety();
				if (runLiveTarget)
					squad.TickStealthLiveTarget();
				}
				finally
				{
					RecordStealthManagerAttributionPhase(
						StealthManagerAttributionPhase.LocalPlanning,
						attributionStarted, 1);
				}
			}

			if (Info.AirAdaptiveRiskInterval > 0 && --adaptiveAirRiskTicks <= 0)
			{
				adaptiveAirRiskTicks = StrategicPlanningInterval(Info.AirAdaptiveRiskInterval);
				UpdateAdaptiveAirRisk();
			}

			if (--assignRolesTicks <= 0)
			{
				assignRolesTicks = StrategicPlanningInterval(Info.AssignRolesInterval);
				FindNewUnits(bot);
			}

			if (--minAttackForceDelayTicks <= 0)
			{
				minAttackForceDelayTicks = StrategicPlanningInterval(Info.MinimumAttackForceDelay);
				CreateAttackForce(bot);
			}
		}

		void AssignRolesToIdleUnitsDegraded(IBot bot)
		{
			CleanSquads();
			activeUnits.RemoveAll(unitCannotBeOrdered);
			activeUnits.RemoveAll(IsReservedForSpecialBehavior);
			unitsHangingAroundTheBase.RemoveAll(unitCannotBeOrdered);
			unitsHangingAroundTheBase.RemoveAll(IsReservedForSpecialBehavior);

			if (!Info.SimpleAttackMoveFallbackWhenDisabled && --rushTicks <= 0)
			{
				rushTicks = StrategicPlanningInterval(Info.RushInterval);
				TryToRushAttack(bot);
			}

			if (!Info.SimpleAttackMoveFallbackWhenDisabled && --attackForceTicks <= 0)
			{
				attackForceTicks = Info.AttackForceInterval;
				foreach (var squad in Squads)
					if (squad.Type != SquadType.GeneralAttack && squad.Type != SquadType.Air)
						squad.Update();
			}

			if (--assignRolesTicks <= 0)
			{
				assignRolesTicks = StrategicPlanningInterval(Info.AssignRolesInterval);
				FindNewUnits(bot);
			}

			if (!Info.SimpleAttackMoveFallbackWhenDisabled && --minAttackForceDelayTicks <= 0)
			{
				minAttackForceDelayTicks = StrategicPlanningInterval(Info.MinimumAttackForceDelay);
				CreateAttackForce(bot);
			}

			foreach (var n in notifyIdleBaseUnits)
				n.UpdatedIdleBaseUnits(unitsHangingAroundTheBase);

			if (--fallbackReconsiderTicks > 0)
				return;
			fallbackReconsiderTicks = Info.FailsafeReconsiderInterval;

			var retainedGroups = releasedFallbackOwnership.Groups.Select(g => new KeyValuePair<string, Actor[]>(g.Key,
				g.Value.Select(World.GetActorById).Where(a => !unitCannotBeOrdered(a)).OrderBy(a => a.ActorID).ToArray())).ToArray();
			var releasedGroups = retainedGroups.Select(g => new KeyValuePair<string, Actor[]>(g.Key,
				g.Value.Where(a => BotModules.AdvancedBotFallbackOwnership.IsEligibleForGenericFallback(Info.FailsafeDirectCombatTypes,
					a.Info.Name, a.Info.HasTraitInfo<AttackBaseInfo>()) && !IsReservedForSpecialBehavior(a) &&
					!IsUnitProtectingBase(a) && !IsUnitTemporarilyControlled(a)).ToArray())).Where(g => g.Value.Length > 0).ToArray();
			var releasedActorIds = new HashSet<uint>(releasedGroups.SelectMany(g => g.Value).Select(a => a.ActorID));
			if (Info.GroundTargetDebugLogging)
				foreach (var group in retainedGroups.Where(g => g.Value.Length > 0))
				{
					var fallback = group.Value.Where(a => releasedActorIds.Contains(a.ActorID)).ToArray();
					var protection = group.Value.Where(IsUnitProtectingBase).ToArray();
					var reserved = group.Value.Where(a => !protection.Contains(a) && IsReservedForSpecialBehavior(a)).ToArray();
					var temporary = group.Value.Where(a => !protection.Contains(a) && !reserved.Contains(a) &&
						IsUnitTemporarilyControlled(a)).ToArray();
					Log.Write("debug", "Squad failsafe released control [{0}]: source={1} fallback={2} protection={3} " +
						"reserved={4} temporary={5} actors={6}.", Player.PlayerName, group.Key, fallback.Length,
						protection.Length, reserved.Length, temporary.Length,
						string.Join(",", group.Value.Select(a => a.Info.Name + "#" + a.ActorID)));
				}

			var candidates = activeUnits.Where(a => !releasedActorIds.Contains(a.ActorID) && !unitCannotBeOrdered(a) &&
				Info.FailsafeDirectCombatTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<AttackBaseInfo>() &&
				!IsReservedForSpecialBehavior(a) && !IsUnitProtectingBase(a) && !IsUnitTemporarilyControlled(a))
				.OrderBy(a => a.ActorID).ToList();
			var controlledActors = candidates.Concat(releasedGroups.SelectMany(g => g.Value)).ToArray();
			fallbackOrderedActors.RemoveWhere(id => controlledActors.All(a => a.ActorID != id));
			foreach (var id in fallbackOrderTargets.Keys.Where(id => controlledActors.All(a => a.ActorID != id)).ToList())
				fallbackOrderTargets.Remove(id);
			if (controlledActors.Length == 0)
				return;

			if (!IsPreferredEnemyUnit(fallbackTarget) || !IsNotHiddenUnit(fallbackTarget))
			{
				fallbackTarget = FindFallbackTarget(controlledActors.Select(a => a.CenterPosition).Average());
				fallbackOrderedActors.Clear();
				fallbackOrderTargets.Clear();
			}

			if (fallbackTarget == null)
				return;

			QueueFailsafeFallback(bot, "ordinary-direct", candidates);
			foreach (var group in releasedGroups)
				QueueFailsafeFallback(bot, group.Key, group.Value);
		}

		void RecruitUnassignedCombatUnits(IBot bot)
		{
			if (unassignedCombatUnits == null)
				return;

			var candidates = unassignedCombatUnits.UnassignedActors
				.Where(a => !unitCannotBeOrdered(a) && !activeUnits.Contains(a) &&
					!IsReservedForSpecialBehavior(a))
				.OrderBy(a => a.ActorID).ToArray();
			if (candidates.Length == 0)
				return;

			var adopted = new List<Actor>();
			var adoptedGroundActors = new List<Actor>();
			var legacyFallback = new List<Actor>();
			var genericFallback = new List<Actor>();
			foreach (var actor in candidates)
			{
				// A loaded squad may already own the actor before the registry has observed the claim.
				if (Squads.Any(s => s.Units.Contains(actor)))
				{
					activeUnits.Add(actor);
					adopted.Add(actor);
					continue;
				}

				if (advancedBehaviorEnabled)
				{
					if (AdoptCurrentSquadUnit(bot, actor, adoptedGroundActors))
					{
						adopted.Add(actor);
						continue;
					}
				}

				var preCodexAssaultAvailable = !Info.SimpleAttackMoveFallbackWhenDisabled &&
					!Info.ExcludeFromSquadsTypes.Contains(actor.Info.Name) &&
					!Info.AirUnitsTypes.Contains(actor.Info.Name) && !Info.NavalUnitsTypes.Contains(actor.Info.Name) &&
					actor.Info.HasTraitInfo<AttackBaseInfo>();
				var genericFallbackEligible = BotModules.AdvancedBotFallbackOwnership.IsEligibleForGenericFallback(
					Info.FailsafeDirectCombatTypes, actor.Info.Name, actor.Info.HasTraitInfo<AttackBaseInfo>()) &&
					!IsUnitProtectingBase(actor) && !IsUnitTemporarilyControlled(actor);
				switch (BotModules.UnassignedCombatUnitRecruitmentPolicy.SelectFallback(advancedBehaviorEnabled,
					preCodexAssaultAvailable, genericFallbackEligible))
				{
					case BotModules.UnassignedCombatFallbackDisposition.PreCodexAssault:
						// GeneralAttack and Air are the disabled advanced paths. The closest pre-Codex
						// owner for released ground combat is the ordinary assault squad.
						legacyFallback.Add(actor);
						continue;
					case BotModules.UnassignedCombatFallbackDisposition.GenericFallback:
						genericFallback.Add(actor);
						continue;
				}

				// Outside the explicit simple benchmark, aircraft, naval units, and excluded specialists
				// remain registered for a compatible owner or specialist reclaim.
			}

			if (legacyFallback.Count > 0)
				AdoptLegacyFallbackAssault(bot, legacyFallback, adopted);
			if (genericFallback.Count > 0)
				AdoptGenericFallback(bot, genericFallback, adopted);

			if (adopted.Count == 0)
				return;

			unassignedCombatUnits.ClaimActors(adopted);
			if (Info.GroundTargetDebugLogging && adopted.Count <= 32)
				Log.Write("debug", "Squad registry recruitment [{0}]: owner=SquadManagerBotModule advanced={1} " +
					"actors={2}.", Player.PlayerName, advancedBehaviorEnabled,
					string.Join(",", adopted.OrderBy(a => a.ActorID).Select(a => a.Info.Name + "#" + a.ActorID)));
		}

		void AdoptGenericFallback(IBot bot, List<Actor> actors, List<Actor> adopted)
		{
			foreach (var actor in actors)
			{
				activeUnits.Add(actor);
				adopted.Add(actor);
			}

			if (!IsPreferredEnemyUnit(fallbackTarget) || !IsNotHiddenUnit(fallbackTarget))
				fallbackTarget = FindFallbackTarget(actors.Select(a => a.CenterPosition).Average());
			if (fallbackTarget != null)
				QueueFailsafeFallback(bot, "unassigned-registry", actors);
		}

		bool AdoptCurrentSquadUnit(IBot bot, Actor actor, List<Actor> adoptedGroundActors)
		{
			if (IsManagerOwnedSpecialist(actor))
			{
				if (!activeUnits.Contains(actor))
					activeUnits.Add(actor);
				return true;
			}

			// Configured stealth specialists can enter the registry one tick before their persistent
			// specialist squad claims them. Preserve the ordinary strategic destination during that
			// handoff, but never give the temporary ground owner an opportunistic AttackMove that can
			// decloak the unit before live local threat/firing-cell safety has run.
			if (Info.StealthSquadDefinitions.Values.Any(definition =>
				definition.UnitTypes.Contains(actor.Info.Name)))
			{
				var destination = FindClosestEnemy(actor.CenterPosition);
				if (destination != null)
					bot.QueueOrder(new Order("Move", actor,
						Target.FromCell(World, destination.Location), false));
				if (!activeUnits.Contains(actor))
					activeUnits.Add(actor);
				if (Info.GroundTargetDebugLogging || Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth strategic handoff [{0}]: actor={1}#{2} " +
						"destination={3} generic-attackmove=False specialist-claim=pending.",
						Player.PlayerName, actor.Info.Name, actor.ActorID,
						destination?.Location.ToString() ?? "none");
				return true;
			}

			if (Info.ExcludeFromSquadsTypes.Contains(actor.Info.Name))
				return false;

			if (Info.AirUnitsTypes.Contains(actor.Info.Name))
			{
				var air = GetAirSquadWithRoom(bot, actor);
				if (air == null)
					return false;

				air.Units.Add(actor);
				if (air.Units.Count > 1)
					air.MarkAirReinforcement(actor);
			}
			else if (Info.NavalUnitsTypes.Contains(actor.Info.Name))
			{
				var ships = GetSquadOfType(SquadType.Naval) ?? RegisterNewSquad(bot, SquadType.Naval);
				ships.Units.Add(actor);
			}
			else if (Info.UseCohesiveGroundSquad)
			{
				adoptedGroundActors.Add(actor);
				var ground = GetSquadOfType(SquadType.GeneralAttack);
				if (ground == null)
					unitsHangingAroundTheBase.Add(actor);
				else
				{
					ground.Units.Add(actor);
					ground.MarkGroundReinforcement(actor);
				}
			}
			else
			{
				adoptedGroundActors.Add(actor);
				var assault = Squads.FirstOrDefault(s => s.Type == SquadType.Assault && s.IsValid);
				var target = FindClosestEnemy(actor.CenterPosition);
				if (assault == null)
					assault = RegisterNewSquad(bot, SquadType.Assault, target);
				else if (target != null)
					assault.TargetActor = target;

				assault.Units.Add(actor);
				if (target != null)
				{
					QueueAggressiveStance(bot, new[] { actor });
					bot.QueueOrder(new Order("AttackMove", actor,
						Target.FromCell(World, target.Location), false));
					assault.FuzzyStateMachine.ChangeState(assault, new GroundUnitsAttackMoveState(), true);
					if (Info.GroundTargetDebugLogging)
						Log.Write("debug", "Squad immediate pre-Codex adoption [{0}]: actor={1}#{2} " +
							"target={3}#{4} stance=AttackAnything order=AttackMove.", Player.PlayerName,
							actor.Info.Name, actor.ActorID, target.Info.Name, target.ActorID);
				}
			}

			activeUnits.Add(actor);
			return true;
		}

		void AdoptLegacyFallbackAssault(IBot bot, List<Actor> actors, List<Actor> adopted)
		{
			var capabilityChecked = actors.All(a => a.Info.HasTraitInfo<AttackBaseInfo>());
			var assault = Squads.FirstOrDefault(s => s.Type == SquadType.Assault && s.IsValid);
			var target = FindClosestEnemy(actors.Select(a => a.CenterPosition).Average());
			if (assault == null)
				assault = RegisterNewSquad(bot, SquadType.Assault, target);
			else if (target != null)
				assault.TargetActor = target;

			foreach (var actor in actors)
			{
				assault.Units.Add(actor);
				activeUnits.Add(actor);
				adopted.Add(actor);
			}

			if (target == null)
				return;

			QueueAggressiveStance(bot, actors);
			bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(World, target.Location), false,
				groupedActors: actors.ToArray()));
			assault.FuzzyStateMachine.ChangeState(assault, new GroundUnitsAttackMoveState(), true);
			if (Info.GroundTargetDebugLogging)
				Log.Write("debug", "Squad registry fallback [{0}]: owner=SquadManagerBotModule source=pre-codex-assault " +
					"accepted={1} actors={2} target={3}#{4} capability-checked={5} stance=AttackAnything " +
					"order=AttackMove.", Player.PlayerName, actors.Count,
					string.Join(",", actors.Select(a => a.Info.Name + "#" + a.ActorID)), target.Info.Name, target.ActorID,
					capabilityChecked);
		}

		static void QueueAggressiveStance(IBot bot, IEnumerable<Actor> actors)
		{
			foreach (var actor in actors)
				if (actor.TraitOrDefault<AutoTarget>() is AutoTarget autoTarget && autoTarget.Stance != UnitStance.AttackAnything)
					bot.QueueOrder(new Order("SetUnitStance", actor, false) { ExtraData = (uint)UnitStance.AttackAnything });
		}

		void QueueFailsafeFallback(IBot bot, string source, IEnumerable<Actor> candidates)
		{
			var orderable = candidates.Where(a => !fallbackOrderedActors.Contains(a.ActorID) ||
				!fallbackOrderTargets.TryGetValue(a.ActorID, out var target) || target != fallbackTarget.Location ||
				(Info.SimpleAttackMoveFallbackWhenDisabled && a.IsIdle)).ToArray();
			if (orderable.Length == 0)
				return;

			QueueAggressiveStance(bot, orderable);
			bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(World, fallbackTarget.Location), false,
				groupedActors: orderable));
			foreach (var actor in orderable)
			{
				fallbackOrderedActors.Add(actor.ActorID);
				fallbackOrderTargets[actor.ActorID] = fallbackTarget.Location;
			}

			if (Info.GroundTargetDebugLogging)
				Log.Write("debug", "Squad failsafe fallback [{0}]: owner=SquadManagerBotModule source={1} accepted={2} " +
					"actors={3} target={4}#{5} order=AttackMove cadence={6}.", Player.PlayerName, source, orderable.Length,
					string.Join(",", orderable.Select(a => a.Info.Name + "#" + a.ActorID)), fallbackTarget.Info.Name,
					fallbackTarget.ActorID, Info.FailsafeReconsiderInterval);
		}

		void FindNewUnits(IBot bot)
		{
			// activeUnits is bookkeeping, not proof of squad membership. Recover any live aircraft whose
			// squad entry was lost so it can never remain permanently idle at the base.
			var assignedAircraft = new HashSet<Actor>(Squads.Where(s => s.Type == SquadType.Air).SelectMany(s => s.Units));
			activeUnits.RemoveAll(a => a != null && Info.AirUnitsTypes.Contains(a.Info.Name) && !assignedAircraft.Contains(a));

			var newUnits = World.ActorsHavingTrait<IPositionable>()
				.Where(a => a.Owner == Player &&
					!Info.ExcludeFromSquadsTypes.Contains(a.Info.Name) &&
					!IsReservedForSpecialBehavior(a) &&
					!activeUnits.Contains(a));

			var adoptedGroundActors = new List<Actor>();
			foreach (var a in newUnits)
			{
				if (AdoptCurrentSquadUnit(bot, a, adoptedGroundActors))
					unassignedCombatUnits?.ClaimActors(new[] { a });
			}

			// Keep actor-level evidence bounded: oversized initial rosters are not useful handoff telemetry.
			if (Info.GroundTargetDebugLogging && adoptedGroundActors.Count > 0 && adoptedGroundActors.Count <= 32)
				Log.Write("debug", "Squad ordinary adoption [{0}]: owner=SquadManagerBotModule actors={1}.",
					Player.PlayerName, string.Join(",", adoptedGroundActors
						.OrderBy(a => a.ActorID).Select(a => a.Info.Name + "#" + a.ActorID)));

			// Notifying here rather than inside the loop, should be fine and saves a bunch of notification calls
			foreach (var n in notifyIdleBaseUnits)
				n.UpdatedIdleBaseUnits(unitsHangingAroundTheBase);
		}

		void CreateAttackForce(IBot bot)
		{
			// Create an attack force when we have enough units around our base.
			// (don't bother leaving any behind for defense)
			var randomizedSquadSize = Info.UseCohesiveGroundSquad ? Info.SquadSize :
				Info.SquadSize + World.LocalRandom.Next(Info.SquadSizeRandomBonus);

			if (unitsHangingAroundTheBase.Count >= randomizedSquadSize)
			{
				var attackForce = Info.UseCohesiveGroundSquad ? GetSquadOfType(SquadType.GeneralAttack) : null;
				if (attackForce == null)
					attackForce = RegisterNewSquad(bot,
						Info.UseCohesiveGroundSquad ? SquadType.GeneralAttack : SquadType.Assault);

				foreach (var a in unitsHangingAroundTheBase)
					attackForce.Units.Add(a);

				unitsHangingAroundTheBase.Clear();
				foreach (var n in notifyIdleBaseUnits)
					n.UpdatedIdleBaseUnits(unitsHangingAroundTheBase);
			}
		}

		void TryToRushAttack(IBot bot)
		{
			var allEnemyBaseBuilder = AIUtils.FindEnemiesByCommonName(Info.ConstructionYardTypes, Player);

			var ownUnits = activeUnits
				.Where(unit => unit.IsIdle && unit.Info.HasTraitInfo<AttackBaseInfo>()
					&& !Info.AirUnitsTypes.Contains(unit.Info.Name) && !Info.NavalUnitsTypes.Contains(unit.Info.Name) && !Info.ExcludeFromSquadsTypes.Contains(unit.Info.Name)).ToList();

			if (!allEnemyBaseBuilder.Any() || ownUnits.Count < Info.SquadSize)
				return;

			foreach (var b in allEnemyBaseBuilder)
			{
				// Don't rush enemy aircraft!
				var enemies = World.FindActorsInCircle(b.CenterPosition, WDist.FromCells(Info.RushAttackScanRadius))
					.Where(unit => IsPreferredEnemyUnit(unit) && unit.Info.HasTraitInfo<AttackBaseInfo>() && !Info.AirUnitsTypes.Contains(unit.Info.Name) && !Info.NavalUnitsTypes.Contains(unit.Info.Name)).ToList();

				if (AttackOrFleeFuzzy.Rush.CanAttack(ownUnits, enemies))
				{
					var target = enemies.Any() ? enemies.Random(World.LocalRandom) : b;
					var rush = GetSquadOfType(SquadType.Rush);
					if (rush == null)
						rush = RegisterNewSquad(bot, SquadType.Rush, target);

					foreach (var a3 in ownUnits)
						rush.Units.Add(a3);

					return;
				}
			}
		}

		void ProtectOwn(IBot bot, Actor attacker)
		{
			var protectSq = GetSquadOfType(SquadType.Protection);
			if (protectSq == null)
				protectSq = RegisterNewSquad(bot, SquadType.Protection, attacker);

			if (!protectSq.IsTargetValid)
				protectSq.TargetActor = attacker;

			if (!protectSq.IsValid)
			{
				var ownUnits = World.FindActorsInCircle(World.Map.CenterOfCell(GetRandomBaseCenter()), WDist.FromCells(Info.ProtectUnitScanRadius))
					.Where(unit => unit.Owner == Player && !Info.ProtectionTypes.Contains(unit.Info.Name) &&
						!Info.AirUnitsTypes.Contains(unit.Info.Name) && unit.Info.HasTraitInfo<AttackBaseInfo>() &&
						!IsReservedForSpecialBehavior(unit))
					.OrderBy(unit => unit.ActorID).ToList();

				foreach (var a in ownUnits)
				{
					protectSq.Units.Add(a);
					foreach (var ground in Squads.Where(s => s.Type == SquadType.GeneralAttack && s.Units.Contains(a)))
						ground.MarkGroundReinforcement(a);
				}

				if (Info.GroundTargetDebugLogging && ownUnits.Count > 0)
					Log.Write("debug", "Ground protection [{0}] activated against {1}#{2}: defenders={3} general-shared={4}.",
						Player.PlayerName, attacker.Info.Name, attacker.ActorID, ownUnits.Count,
						ownUnits.Count(a => Squads.Any(s => s.Type == SquadType.GeneralAttack && s.Units.Contains(a))));
			}
		}

		void IBotPositionsUpdated.UpdatedBaseCenter(CPos newLocation)
		{
			initialBaseCenter = newLocation;
		}

		void IBotPositionsUpdated.UpdatedDefenseCenter(CPos newLocation) { }

		string AirProfileFor(Actor actor)
		{
			if (actor == null || !Info.AirUnitsTypes.Contains(actor.Info.Name))
				return null;

			var squad = Squads.FirstOrDefault(s => s.Type == SquadType.Air && s.Units.Contains(actor));
			if (squad != null)
				return squad.AirProfile;

			return Info.AirSquadDefinitions
				.Where(d => d.Value.UnitTypes.Count == 0 || d.Value.UnitTypes.Contains(actor.Info.Name))
				.OrderBy(d => d.Value.UnitTypes.Count == 0 ? int.MaxValue : d.Value.UnitTypes.Count)
				.ThenBy(d => d.Key)
				.Select(d => d.Value.Profile)
				.FirstOrDefault();
		}

		int FullAmmoGrowth(string profile)
		{
			return profile.Equals("Apache", StringComparison.OrdinalIgnoreCase) ? Info.AirAdaptiveRiskApacheFullAmmoGrowth :
				Info.AirAdaptiveRiskOrcaFullAmmoGrowth;
		}

		int KillGrowth(string profile)
		{
			return profile.Equals("Apache", StringComparison.OrdinalIgnoreCase) ? Info.AirAdaptiveRiskApacheKillGrowth :
				Info.AirAdaptiveRiskOrcaKillGrowth;
		}

		void UpdateAdaptiveAirRisk()
		{
			foreach (var entry in adaptiveAirRisk)
			{
				var units = World.Actors.Where(a => a.Owner == Player && !a.IsDead && a.IsInWorld &&
					entry.Key.Equals(AirProfileFor(a), StringComparison.OrdinalIgnoreCase)).ToList();
				var fullAmmo = units.Count(a =>
				{
					var pools = a.TraitsImplementing<AmmoPool>().ToList();
					return pools.Count > 0 && pools.All(p => p.HasFullAmmo);
				});

				var previous = entry.Value.BonusBasisPoints;
				entry.Value.Update(World.WorldTick, fullAmmo, units.Count, Info.AirAdaptiveRiskMinimumUnits,
					FullAmmoGrowth(entry.Key), Info.AirAdaptiveRiskLowUnitDecay);
				if (Info.AirTargetDebugLogging && previous != entry.Value.BonusBasisPoints)
					Log.Write("debug", "Air adaptive [{0}]: units={1} full-ammo={2} bonus={3} multiplier={4:0.00}.",
						entry.Key, units.Count, fullAmmo, entry.Value.BonusBasisPoints, AirRiskMultiplier(entry.Key));
			}
		}

		void INotifyAppliedDamage.AppliedDamage(Actor self, Actor damaged, AttackInfo e)
		{
			if (IsTraitDisabled || e.DamageState != DamageState.Dead || e.PreviousDamageState == DamageState.Dead ||
				e.Attacker == null || e.Attacker.Owner != Player || Player.RelationshipWith(damaged.Owner) != PlayerRelationship.Enemy)
				return;

			if (e.Attacker.Info.Name == "stnk")
			{
				var generationId = StealthAISpecialistPolicy.KillTimeOwnerGeneration(e.Attacker.ActorID,
					Squads.Where(s => s.Type == SquadType.Stealth && s.StealthProfile == "stealth-tank" &&
						s.StealthKillCadenceGeneration != null).SelectMany(s => s.Units.Select(unit =>
						new KeyValuePair<uint, int>(unit.ActorID,
							s.StealthKillCadenceGeneration.GenerationId))));
				var stealthSquad = Squads.FirstOrDefault(s => s.StealthKillCadenceGeneration?.GenerationId == generationId);
				if (stealthSquad != null)
				{
					var killedValue = Math.Max(0, damaged.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0);
					stealthEfficiencyKillValue += killedValue;
					if (!stealthGenerationEfficiency.TryGetValue(generationId, out var generationWindow))
					{
						generationWindow = new StealthEfficiencyWindow(
							stealthSquad.StealthKillCadenceGeneration.GenerationStartTick);
						stealthGenerationEfficiency.Add(generationId, generationWindow);
					}

					generationWindow.RecordKill(killedValue);
					if (!stealthSquad.StealthKillCadenceGeneration.AttributeKill(World.WorldTick))
						ReportStealthCadenceMismatch(stealthSquad, World.WorldTick);
					if (Info.AirTargetDebugLogging)
						Log.Write("debug", "Stealth kill watchdog [stealth-tank] STNK-attributed kill: " +
							"tick={0} generation={1} squad={2}#{3} attacker=stnk#{4} victim={5}#{6} " +
							"generation-kills={7} cadence-age=0 window-start={0}.",
							World.WorldTick, stealthSquad.StealthKillCadenceGeneration.GenerationId,
							stealthSquad.StealthSquadDefinition,
							stealthSquad.StealthSquadIndex, e.Attacker.ActorID, damaged.Info.Name,
							damaged.ActorID, stealthSquad.StealthDebugKillCadenceKills);
				}
			}

			if (Info.GroundTargetDebugLogging)
			{
				var groundSquad = Squads.FirstOrDefault(s => s.Type == SquadType.GeneralAttack &&
					s.GroundFormationUnits().Contains(e.Attacker));
				if (groundSquad != null)
					Log.Write("debug", "Ground outcome [{0}] destroyed {1}#{2}: attacker={3}#{4} mission-target={5} formation={6} reinforcements={7}.",
						Player.PlayerName, damaged.Info.Name, damaged.ActorID, e.Attacker.Info.Name,
						e.Attacker.ActorID, groundSquad.TargetActor == damaged,
						groundSquad.GroundFormationUnits().Count, groundSquad.GroundReinforcements.Count);

				var protectionSquad = Squads.FirstOrDefault(s => s.Type == SquadType.Protection && s.Units.Contains(e.Attacker));
				if (protectionSquad != null)
					Log.Write("debug", "Ground protection outcome [{0}] destroyed {1}#{2}: defender={3}#{4}.",
						Player.PlayerName, damaged.Info.Name, damaged.ActorID,
						e.Attacker.Info.Name, e.Attacker.ActorID);
			}

			var profile = AirProfileFor(e.Attacker);
			if (profile == null || !adaptiveAirRisk.TryGetValue(profile, out var controller))
				return;

			var growth = KillGrowth(profile);
			if (growth <= 0)
				return;

			var value = damaged.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;
			controller.RecordKill(value, growth);
			if (Info.AirTargetDebugLogging)
				Log.Write("debug", "Air adaptive [{0}]: credited {1} value for killing {2}#{3}.",
					profile, value, damaged.Info.Name, damaged.ActorID);
		}

		void ReportStealthCadenceMismatch(Squad squad, int tick)
		{
			var generation = squad.StealthKillCadenceGeneration;
			var message = $"Stealth kill watchdog permanent generation-age mismatch generation=" +
				$"{generation.GenerationId} squad={squad.StealthSquadDefinition}#{squad.StealthSquadIndex} " +
				$"tick={tick} generation-start={generation.GenerationStartTick} " +
				$"generation-elapsed={tick - generation.GenerationStartTick} cadence-age={generation.CadenceAge}.";
			Log.Write("debug", message);
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (IsTraitDisabled || self.Owner != Player || e.Attacker == null || e.Attacker == self ||
				Player.RelationshipWith(e.Attacker.Owner) != PlayerRelationship.Enemy)
				return;

			if (Game.Settings.Debug.BotDebug &&
				StealthAISpecialistPolicy.IsObeliskAttributedStealthTankDeath(
					self.Info.Name, e.Attacker.Info.Name))
			{
				var message = $"Stealth Obelisk death watchdog failure owner={self.Owner.PlayerName} " +
					$"victim=stnk#{self.ActorID} attacker=obli#{e.Attacker.ActorID} " +
					$"tick={World.WorldTick}.";
				Log.Write("debug", message);
			}

			var profile = AirProfileFor(self);
			if (profile == null || !adaptiveAirRisk.TryGetValue(profile, out var controller))
				return;

			var previous = controller.BonusBasisPoints;
			controller.RecordEnemyLoss(World.WorldTick, Info.AirAdaptiveRiskRollbackTicks, Info.AirAdaptiveRiskLossDecrement);
			if (Info.AirTargetDebugLogging)
				Log.Write("debug", "Air adaptive [{0}]: enemy loss {1}#{2}, bonus {3}->{4}.",
					profile, self.Info.Name, self.ActorID, previous, controller.BonusBasisPoints);
		}

		void IBotRespondToAttack.RespondToAttack(IBot bot, Actor self, AttackInfo e)
		{
			if (self.Owner == Player && self.Info.Name == "stnk")
			{
				stealthEfficiencyActors.Add(self.ActorID);
				stealthEfficiencyDamageTaken += Math.Max(0, e.Damage.Value);
				var squad = Squads.FirstOrDefault(candidate => candidate.Type == SquadType.Stealth &&
					candidate.StealthKillCadenceGeneration != null && candidate.Units.Contains(self));
				if (squad != null)
				{
					var generation = squad.StealthKillCadenceGeneration;
					if (!stealthGenerationEfficiency.TryGetValue(generation.GenerationId, out var generationWindow))
					{
						generationWindow = new StealthEfficiencyWindow(generation.GenerationStartTick);
						stealthGenerationEfficiency.Add(generation.GenerationId, generationWindow);
					}

					generationWindow.RecordDamage(self.ActorID, e.Damage.Value);
				}
			}

			var modularStealth = Squads.FirstOrDefault(candidate => candidate.Type == SquadType.Stealth &&
				candidate.Units.Contains(self));
			if (modularStealth?.ObserveModularStealthDamage(self, e) == true)
				return;

			if (!IsPreferredEnemyUnit(e.Attacker))
				return;

			if (Info.ProtectionTypes.Contains(self.Info.Name))
			{
				foreach (var n in notifyPositionsUpdated)
					n.UpdatedDefenseCenter(e.Attacker.Location);

				ProtectOwn(bot, e.Attacker);
			}
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			var releasedFallback = releasedFallbackOwnership.Export();
			return new List<MiniYamlNode>()
			{
				new MiniYamlNode("Squads", "", Squads.Select(s => new MiniYamlNode("Squad", s.Serialize())).ToList()),
				new MiniYamlNode("InitialBaseCenter", FieldSaver.FormatValue(initialBaseCenter)),
				new MiniYamlNode("UnitsHangingAroundTheBase", FieldSaver.FormatValue(unitsHangingAroundTheBase
					.Where(a => !unitCannotBeOrdered(a))
					.Select(a => a.ActorID)
					.ToArray())),
				new MiniYamlNode("ActiveUnits", FieldSaver.FormatValue(activeUnits
					.Where(a => !unitCannotBeOrdered(a))
					.Select(a => a.ActorID)
					.ToArray())),
				new MiniYamlNode("RushTicks", FieldSaver.FormatValue(rushTicks)),
				new MiniYamlNode("AssignRolesTicks", FieldSaver.FormatValue(assignRolesTicks)),
				new MiniYamlNode("AttackForceTicks", FieldSaver.FormatValue(attackForceTicks)),
				new MiniYamlNode("MinAttackForceDelayTicks", FieldSaver.FormatValue(minAttackForceDelayTicks)),
				new MiniYamlNode("AdaptiveAirRiskTicks", FieldSaver.FormatValue(adaptiveAirRiskTicks)),
				new MiniYamlNode("NextStealthSquadGenerationId", FieldSaver.FormatValue(nextStealthSquadGenerationId)),
				new MiniYamlNode("AdvancedBehaviorEnabled", FieldSaver.FormatValue(advancedBehaviorEnabled)),
				new MiniYamlNode("StealthEfficiencyKillValue", FieldSaver.FormatValue(stealthEfficiencyKillValue)),
				new MiniYamlNode("StealthEfficiencyActorTicks", FieldSaver.FormatValue(stealthEfficiencyActorTicks)),
				new MiniYamlNode("StealthEfficiencyDamageTaken", FieldSaver.FormatValue(stealthEfficiencyDamageTaken)),
				new MiniYamlNode("StealthEfficiencyActors", FieldSaver.FormatValue(stealthEfficiencyActors.ToArray())),
				new MiniYamlNode("StealthEfficiencyNextReportTick", FieldSaver.FormatValue(stealthEfficiencyNextReportTick)),
				new MiniYamlNode("StealthEfficiencyWindowStartTick", FieldSaver.FormatValue(stealthEfficiencyWindowStartTick)),
				StealthAISpecialistPolicy.SaveStealthGenerationEfficiency(stealthGenerationEfficiency),
				StealthAISpecialistPolicy.SaveStealthCadenceGenerations(stealthCadenceGenerations.Values),
				new MiniYamlNode("FallbackReconsiderTicks", FieldSaver.FormatValue(fallbackReconsiderTicks)),
				new MiniYamlNode("FallbackTarget", FieldSaver.FormatValue(fallbackTarget?.ActorID ?? 0)),
				new MiniYamlNode("FallbackOrderedActors", FieldSaver.FormatValue(fallbackOrderedActors.ToArray())),
				new MiniYamlNode("FallbackOrderTargetActors", FieldSaver.FormatValue(fallbackOrderTargets.Keys.ToArray())),
				new MiniYamlNode("FallbackOrderTargetCells", FieldSaver.FormatValue(fallbackOrderTargets.Values.ToArray())),
				new MiniYamlNode("FallbackReleasedSources", FieldSaver.FormatValue(releasedFallback.Sources)),
				new MiniYamlNode("FallbackReleasedActors", FieldSaver.FormatValue(releasedFallback.ActorIds)),
				new MiniYamlNode("AdaptiveAirRisk", "", adaptiveAirRisk.OrderBy(e => e.Key).Select(e =>
				{
					var state = e.Value.ExportState();
					return new MiniYamlNode(e.Key, "", new List<MiniYamlNode>
					{
						new MiniYamlNode("Bonus", FieldSaver.FormatValue(state.BonusBasisPoints)),
						new MiniYamlNode("PendingKillBonus", FieldSaver.FormatValue(state.PendingKillBonusBasisPoints)),
						new MiniYamlNode("HistoryTicks", FieldSaver.FormatValue(state.History.Select(h => h.Tick).ToArray())),
						new MiniYamlNode("HistoryBonuses", FieldSaver.FormatValue(state.History.Select(h => h.BonusBasisPoints).ToArray())),
					});
				}).ToList()),
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var initialBaseCenterNode = data.FirstOrDefault(n => n.Key == "InitialBaseCenter");
			if (initialBaseCenterNode != null)
				initialBaseCenter = FieldLoader.GetValue<CPos>("InitialBaseCenter", initialBaseCenterNode.Value.Value);

			var unitsHangingAroundTheBaseNode = data.FirstOrDefault(n => n.Key == "UnitsHangingAroundTheBase");
			if (unitsHangingAroundTheBaseNode != null)
			{
				unitsHangingAroundTheBase.Clear();
				unitsHangingAroundTheBase.AddRange(FieldLoader.GetValue<uint[]>("UnitsHangingAroundTheBase", unitsHangingAroundTheBaseNode.Value.Value)
					.Select(a => self.World.GetActorById(a)).Where(a => a != null));
			}

			var activeUnitsNode = data.FirstOrDefault(n => n.Key == "ActiveUnits");
			if (activeUnitsNode != null)
			{
				activeUnits.Clear();
				activeUnits.AddRange(FieldLoader.GetValue<uint[]>("ActiveUnits", activeUnitsNode.Value.Value)
					.Select(a => self.World.GetActorById(a)).Where(a => a != null));
			}

			var rushTicksNode = data.FirstOrDefault(n => n.Key == "RushTicks");
			if (rushTicksNode != null)
				rushTicks = FieldLoader.GetValue<int>("RushTicks", rushTicksNode.Value.Value);

			var assignRolesTicksNode = data.FirstOrDefault(n => n.Key == "AssignRolesTicks");
			if (assignRolesTicksNode != null)
				assignRolesTicks = FieldLoader.GetValue<int>("AssignRolesTicks", assignRolesTicksNode.Value.Value);

			var attackForceTicksNode = data.FirstOrDefault(n => n.Key == "AttackForceTicks");
			if (attackForceTicksNode != null)
				attackForceTicks = FieldLoader.GetValue<int>("AttackForceTicks", attackForceTicksNode.Value.Value);

			var minAttackForceDelayTicksNode = data.FirstOrDefault(n => n.Key == "MinAttackForceDelayTicks");
			if (minAttackForceDelayTicksNode != null)
				minAttackForceDelayTicks = FieldLoader.GetValue<int>("MinAttackForceDelayTicks", minAttackForceDelayTicksNode.Value.Value);

			var adaptiveAirRiskTicksNode = data.FirstOrDefault(n => n.Key == "AdaptiveAirRiskTicks");
			if (adaptiveAirRiskTicksNode != null)
				adaptiveAirRiskTicks = FieldLoader.GetValue<int>("AdaptiveAirRiskTicks", adaptiveAirRiskTicksNode.Value.Value);

			var nextStealthGenerationNode = data.FirstOrDefault(n => n.Key == "NextStealthSquadGenerationId");
			if (nextStealthGenerationNode != null)
				nextStealthSquadGenerationId = FieldLoader.GetValue<int>(
					"NextStealthSquadGenerationId", nextStealthGenerationNode.Value.Value);

			var advancedBehaviorNode = data.FirstOrDefault(n => n.Key == "AdvancedBehaviorEnabled");
			if (advancedBehaviorNode != null)
				advancedBehaviorEnabled = FieldLoader.GetValue<bool>("AdvancedBehaviorEnabled", advancedBehaviorNode.Value.Value);
			var efficiencyKillNode = data.FirstOrDefault(n => n.Key == "StealthEfficiencyKillValue");
			if (efficiencyKillNode != null)
				stealthEfficiencyKillValue = FieldLoader.GetValue<long>(
					"StealthEfficiencyKillValue", efficiencyKillNode.Value.Value);
			var efficiencyTicksNode = data.FirstOrDefault(n => n.Key == "StealthEfficiencyActorTicks");
			if (efficiencyTicksNode != null)
				stealthEfficiencyActorTicks = FieldLoader.GetValue<long>(
					"StealthEfficiencyActorTicks", efficiencyTicksNode.Value.Value);
			var efficiencyDamageNode = data.FirstOrDefault(n => n.Key == "StealthEfficiencyDamageTaken");
			if (efficiencyDamageNode != null)
				stealthEfficiencyDamageTaken = FieldLoader.GetValue<long>(
					"StealthEfficiencyDamageTaken", efficiencyDamageNode.Value.Value);
			var efficiencyActorsNode = data.FirstOrDefault(n => n.Key == "StealthEfficiencyActors");
			if (efficiencyActorsNode != null)
			{
				stealthEfficiencyActors.Clear();
				stealthEfficiencyActors.UnionWith(FieldLoader.GetValue<uint[]>(
					"StealthEfficiencyActors", efficiencyActorsNode.Value.Value));
			}

			var efficiencyReportNode = data.FirstOrDefault(n => n.Key == "StealthEfficiencyNextReportTick");
			if (efficiencyReportNode != null)
				stealthEfficiencyNextReportTick = FieldLoader.GetValue<int>(
					"StealthEfficiencyNextReportTick", efficiencyReportNode.Value.Value);
			var efficiencyWindowNode = data.FirstOrDefault(n => n.Key == "StealthEfficiencyWindowStartTick");
			if (efficiencyWindowNode != null)
				stealthEfficiencyWindowStartTick = FieldLoader.GetValue<int>(
					"StealthEfficiencyWindowStartTick", efficiencyWindowNode.Value.Value);
			var generationEfficiencyNode = data.FirstOrDefault(n => n.Key == "StealthGenerationEfficiency");
			if (StealthAISpecialistPolicy.TryLoadStealthGenerationEfficiency(
				generationEfficiencyNode, out var generationEfficiency))
			{
				stealthGenerationEfficiency.Clear();
				foreach (var pair in generationEfficiency)
					stealthGenerationEfficiency.Add(pair.Key, pair.Value);
			}

			var cadenceGenerationsNode = data.FirstOrDefault(n => n.Key == "StealthCadenceGenerations");
			if (StealthAISpecialistPolicy.TryLoadStealthCadenceGenerations(
				cadenceGenerationsNode, out var cadenceGenerations))
			{
				stealthCadenceGenerations.Clear();
				foreach (var record in cadenceGenerations)
					stealthCadenceGenerations.Add(record.Generation.GenerationId, record);
			}

			var fallbackTicksNode = data.FirstOrDefault(n => n.Key == "FallbackReconsiderTicks");
			if (fallbackTicksNode != null)
				fallbackReconsiderTicks = FieldLoader.GetValue<int>("FallbackReconsiderTicks", fallbackTicksNode.Value.Value);
			var fallbackTargetNode = data.FirstOrDefault(n => n.Key == "FallbackTarget");
			if (fallbackTargetNode != null)
				fallbackTarget = self.World.GetActorById(FieldLoader.GetValue<uint>("FallbackTarget", fallbackTargetNode.Value.Value));
			var fallbackActorsNode = data.FirstOrDefault(n => n.Key == "FallbackOrderedActors");
			if (fallbackActorsNode != null)
			{
				fallbackOrderedActors.Clear();
				foreach (var actorId in FieldLoader.GetValue<uint[]>("FallbackOrderedActors", fallbackActorsNode.Value.Value))
					if (self.World.GetActorById(actorId) != null)
						fallbackOrderedActors.Add(actorId);
			}

			var fallbackTargetActorsNode = data.FirstOrDefault(n => n.Key == "FallbackOrderTargetActors");
			var fallbackTargetCellsNode = data.FirstOrDefault(n => n.Key == "FallbackOrderTargetCells");
			if (fallbackTargetActorsNode != null && fallbackTargetCellsNode != null)
			{
				var actorIds = FieldLoader.GetValue<uint[]>("FallbackOrderTargetActors", fallbackTargetActorsNode.Value.Value);
				var cells = FieldLoader.GetValue<CPos[]>("FallbackOrderTargetCells", fallbackTargetCellsNode.Value.Value);
				fallbackOrderTargets.Clear();
				for (var i = 0; i < Math.Min(actorIds.Length, cells.Length); i++)
					if (self.World.GetActorById(actorIds[i]) != null)
						fallbackOrderTargets[actorIds[i]] = cells[i];
			}

			var fallbackReleasedSourcesNode = data.FirstOrDefault(n => n.Key == "FallbackReleasedSources");
			var fallbackReleasedActorsNode = data.FirstOrDefault(n => n.Key == "FallbackReleasedActors");
			if (fallbackReleasedSourcesNode != null && fallbackReleasedActorsNode != null)
				releasedFallbackOwnership.Import(
					FieldLoader.GetValue<string[]>("FallbackReleasedSources", fallbackReleasedSourcesNode.Value.Value),
					FieldLoader.GetValue<uint[]>("FallbackReleasedActors", fallbackReleasedActorsNode.Value.Value));

			var adaptiveAirRiskNode = data.FirstOrDefault(n => n.Key == "AdaptiveAirRisk");
			if (adaptiveAirRiskNode != null)
				foreach (var profileNode in adaptiveAirRiskNode.Value.Nodes)
				{
					if (!adaptiveAirRisk.TryGetValue(profileNode.Key, out var controller))
						continue;

					var fields = profileNode.Value.Nodes.ToDictionary(n => n.Key);
					if (!fields.TryGetValue("Bonus", out var bonusNode) ||
						!fields.TryGetValue("PendingKillBonus", out var pendingNode) ||
						!fields.TryGetValue("HistoryTicks", out var ticksNode) ||
						!fields.TryGetValue("HistoryBonuses", out var bonusesNode))
						continue;

					var ticks = FieldLoader.GetValue<int[]>("HistoryTicks", ticksNode.Value.Value);
					var bonuses = FieldLoader.GetValue<int[]>("HistoryBonuses", bonusesNode.Value.Value);
					if (ticks.Length != bonuses.Length)
						continue;

					controller.ImportState(new AdaptiveAirRiskState(
						FieldLoader.GetValue<int>("Bonus", bonusNode.Value.Value),
						FieldLoader.GetValue<int>("PendingKillBonus", pendingNode.Value.Value),
						ticks.Select((tick, i) => new AdaptiveAirRiskCheckpoint(tick, bonuses[i])).ToArray()));
				}

			var squadsNode = data.FirstOrDefault(n => n.Key == "Squads");
			if (squadsNode != null)
			{
				Squads.Clear();
				foreach (var n in squadsNode.Value.Nodes)
					Squads.Add(Squad.Deserialize(bot, this, n.Value));

				nextStealthSquadGenerationId = Math.Max(nextStealthSquadGenerationId,
					Squads.Where(s => s.StealthKillCadenceGeneration != null)
						.Select(s => s.StealthKillCadenceGeneration.GenerationId + 1).DefaultIfEmpty(1).Max());
				foreach (var squad in Squads.Where(s => s.Type == SquadType.Stealth &&
					s.StealthKillCadenceGeneration == null && s.Units.Any(unit => unit != null &&
						!unit.IsDead && unit.IsInWorld && unit.Info.Name == "stnk")))
					squad.StealthKillCadenceGeneration = new StealthKillCadenceGeneration(
						nextStealthSquadGenerationId++, World.WorldTick);
				foreach (var squad in Squads.Where(s => s.Type == SquadType.Stealth))
					RegisterStealthCadenceGeneration(squad);
				FinalizeOrphanedLoadedStealthGenerationDiagnostics();

				EnsureStealthSquads(bot);
			}
		}
	}
}
