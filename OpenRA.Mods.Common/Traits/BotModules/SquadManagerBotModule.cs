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
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
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

		[Desc("Actor types that are valid for naval squads.")]
		public readonly HashSet<string> NavalUnitsTypes = new HashSet<string>();

		[Desc("Actor types that are excluded from ground attacks.")]
		public readonly HashSet<string> AirUnitsTypes = new HashSet<string>();

		[Desc("Actor types that should generally be excluded from attack squads.")]
		public readonly HashSet<string> ExcludeFromSquadsTypes = new HashSet<string>();

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

			if (AirTargetFullAmmoDistanceBonus < 0 || AirTargetAaClearUnlockPercent < 0 ||
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
		IBotPositionsUpdated, INotifyKilled, INotifyAppliedDamage, IGameSaveTraitData
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

		readonly Predicate<Actor> unitCannotBeOrdered;
		readonly List<Actor> unitsHangingAroundTheBase = new List<Actor>();
		readonly Dictionary<string, AdaptiveAirRiskController> adaptiveAirRisk =
			new Dictionary<string, AdaptiveAirRiskController>(StringComparer.OrdinalIgnoreCase);

		// Units that the bot already knows about. Any unit not on this list needs to be given a role.
		readonly List<Actor> activeUnits = new List<Actor>();

		public List<Squad> Squads = new List<Squad>();

		IBot bot;
		IBotPositionsUpdated[] notifyPositionsUpdated;
		IBotNotifyIdleBaseUnits[] notifyIdleBaseUnits;

		CPos initialBaseCenter;

		int rushTicks;
		int assignRolesTicks;
		int attackForceTicks;
		int minAttackForceDelayTicks;
		int airSafetyTicks;
		int adaptiveAirRiskTicks;

		public SquadManagerBotModule(Actor self, SquadManagerBotModuleInfo info)
			: base(info)
		{
			World = self.World;
			Player = self.Owner;

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
		}

		protected override void TraitEnabled(Actor self)
		{
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

			if (Info.AirAdaptiveRiskInterval > 0)
				adaptiveAirRiskTicks = Info.AirAdaptiveRiskInterval;
		}

		void IBotEnabled.BotEnabled(IBot bot)
		{
			this.bot = bot;
		}

		void IBotTick.BotTick(IBot bot)
		{
			AssignRolesToIdleUnits(bot);
		}

		internal Actor FindClosestEnemy(WPos pos)
		{
			var units = World.Actors.Where(IsPreferredEnemyUnit);
			return units.Where(IsNotHiddenUnit).ClosestTo(pos) ?? units.ClosestTo(pos);
		}

		internal Actor FindClosestEnemy(WPos pos, WDist radius)
		{
			return World.FindActorsInCircle(pos, radius).Where(a => IsPreferredEnemyUnit(a) && IsNotHiddenUnit(a)).ClosestTo(pos);
		}

		void CleanSquads()
		{
			Squads.RemoveAll(s => !s.IsValid);
			foreach (var s in Squads)
			{
				s.Units.RemoveAll(unitCannotBeOrdered);
				if (s.Type == SquadType.Air)
					s.CleanAirMembership();
			}
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

		void AssignRolesToIdleUnits(IBot bot)
		{
			CleanSquads();

			activeUnits.RemoveAll(unitCannotBeOrdered);
			unitsHangingAroundTheBase.RemoveAll(unitCannotBeOrdered);
			foreach (var n in notifyIdleBaseUnits)
				n.UpdatedIdleBaseUnits(unitsHangingAroundTheBase);

			if (--rushTicks <= 0)
			{
				rushTicks = Info.RushInterval;
				TryToRushAttack(bot);
			}

			if (--attackForceTicks <= 0)
			{
				attackForceTicks = Info.AttackForceInterval;
				foreach (var s in Squads)
					s.Update();
			}

			// Air squads re-check the anti-air around themselves far more often than the state machine
			// runs, so they can break off a run that has become lethal instead of dying on it.
			// PERF: one bounded circle scan per air squad per interval, and there is at most one air squad.
			if (Info.AirSafetyCheckInterval > 0 && --airSafetyTicks <= 0)
			{
				airSafetyTicks = Info.AirSafetyCheckInterval;
				foreach (var s in Squads)
					s.TickAirSafety();
			}

			if (Info.AirAdaptiveRiskInterval > 0 && --adaptiveAirRiskTicks <= 0)
			{
				adaptiveAirRiskTicks = Info.AirAdaptiveRiskInterval;
				UpdateAdaptiveAirRisk();
			}

			if (--assignRolesTicks <= 0)
			{
				assignRolesTicks = Info.AssignRolesInterval;
				FindNewUnits(bot);
			}

			if (--minAttackForceDelayTicks <= 0)
			{
				minAttackForceDelayTicks = Info.MinimumAttackForceDelay;
				CreateAttackForce(bot);
			}
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
					!activeUnits.Contains(a));

			foreach (var a in newUnits)
			{
				if (Info.AirUnitsTypes.Contains(a.Info.Name))
				{
					var air = GetAirSquadWithRoom(bot, a);

					// Every air squad is full and we may not start another. Leave the aircraft out of
					// activeUnits so it stays at base and is picked up as soon as a slot frees up, rather
					// than oversizing a harassment squad.
					if (air == null)
						continue;

					air.Units.Add(a);
					if (air.Units.Count > 1)
						air.MarkAirReinforcement(a);
				}
				else if (Info.NavalUnitsTypes.Contains(a.Info.Name))
				{
					var ships = GetSquadOfType(SquadType.Naval);
					if (ships == null)
						ships = RegisterNewSquad(bot, SquadType.Naval);

					ships.Units.Add(a);
				}
				else
					unitsHangingAroundTheBase.Add(a);

				activeUnits.Add(a);
			}

			// Notifying here rather than inside the loop, should be fine and saves a bunch of notification calls
			foreach (var n in notifyIdleBaseUnits)
				n.UpdatedIdleBaseUnits(unitsHangingAroundTheBase);
		}

		void CreateAttackForce(IBot bot)
		{
			// Create an attack force when we have enough units around our base.
			// (don't bother leaving any behind for defense)
			var randomizedSquadSize = Info.SquadSize + World.LocalRandom.Next(Info.SquadSizeRandomBonus);

			if (unitsHangingAroundTheBase.Count >= randomizedSquadSize)
			{
				var attackForce = RegisterNewSquad(bot, SquadType.Assault);

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
						!Info.AirUnitsTypes.Contains(unit.Info.Name) && unit.Info.HasTraitInfo<AttackBaseInfo>());

				foreach (var a in ownUnits)
					protectSq.Units.Add(a);
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

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (IsTraitDisabled || self.Owner != Player || e.Attacker == null || e.Attacker == self ||
				Player.RelationshipWith(e.Attacker.Owner) != PlayerRelationship.Enemy)
				return;

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
			}
		}
	}
}
