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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits.Radar;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Manages AI base construction.")]
	public class BaseBuilderBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Tells the AI what building types are considered construction yards.")]
		public readonly HashSet<string> ConstructionYardTypes = new HashSet<string>();

		[Desc("Tells the AI what building types are considered vehicle production facilities.")]
		public readonly HashSet<string> VehiclesFactoryTypes = new HashSet<string>();

		[Desc("Tells the AI what building types are considered refineries.")]
		public readonly HashSet<string> RefineryTypes = new HashSet<string>();

		[Desc("Tells the AI what building types are considered power plants.")]
		public readonly HashSet<string> PowerTypes = new HashSet<string>();

		[Desc("Forces AI to treat these buildings as defenses and build them toward enemy.")]
		public readonly HashSet<string> PlaceAsDefenses = new HashSet<string>();

		[Desc("Anti-ground defense types whose first completed structure is placed beside the initial construction yard.")]
		public readonly HashSet<string> FirstTowerTypes = new HashSet<string>();

		[Desc("Reserve the first buildable OpeningDefenseTypes actor before ordinary shuffled defense selection.")]
		public readonly bool PrioritizeOpeningFirstTower = false;

		[Desc("Maximum cells searched around the preferred first-tower position when it is blocked.")]
		public readonly int FirstTowerSearchRadius = 20;

		[Desc("Write first-tower planning and completion diagnostics to debug.log.")]
		public readonly bool FirstTowerDebugLogging = false;

		[Desc("Chance of forced placing as defense.")]
		public readonly int PlaceAsDefenseChance = 50;

		[Desc("Tells the AI what building types are considered infantry production facilities.")]
		public readonly HashSet<string> BarracksTypes = new HashSet<string>();

		[Desc("Tells the AI what building types are considered production facilities.")]
		public readonly HashSet<string> ProductionTypes = new HashSet<string>();

		[Desc("Tells the AI what building types are considered naval production facilities.")]
		public readonly HashSet<string> NavalProductionTypes = new HashSet<string>();

		[Desc("Tells the AI what building types are considered silos (resource storage).")]
		public readonly HashSet<string> SiloTypes = new HashSet<string>();

		[Desc("Use the configured ordered opening goals before ordinary structure selection.")]
		public readonly bool EnableOpeningPolicy = false;

		[Desc("Opening structure alternatives. Only the earliest incomplete goal is coordinated; unrelated idle queues use normal logic.")]
		public readonly string[] OpeningPowerTypes = System.Array.Empty<string>();
		public readonly string[] OpeningSiloTypes = System.Array.Empty<string>();
		public readonly string[] OpeningDefenseTypes = System.Array.Empty<string>();
		public readonly string[] OpeningBarracksTypes = System.Array.Empty<string>();
		public readonly string[] OpeningRefineryTypes = System.Array.Empty<string>();
		public readonly string[] OpeningFactoryTypes = System.Array.Empty<string>();
		public readonly string[] OpeningAdditionalFactoryTypes = System.Array.Empty<string>();
		public readonly string[] OpeningRadarTypes = System.Array.Empty<string>();
		public readonly string[] OpeningHelipadTypes = System.Array.Empty<string>();
		public readonly string[] OpeningOptionalStructureTypes = System.Array.Empty<string>();
		public readonly int OpeningAdditionalFactoryCount = 0;

		[Desc("Opening unit alternatives and targets. External requests ignore ordinary unit delays.")]
		public readonly string[] OpeningSoldierTypes = System.Array.Empty<string>();
		public readonly string[] OpeningHarvesterTypes = System.Array.Empty<string>();
		public readonly string[] OpeningDefenseUnlockTypes = System.Array.Empty<string>();
		public readonly string OpeningMcvType = "mcv";
		public readonly int OpeningSoldierCount = 20;
		public readonly int OpeningHarvesterCount = 5;
		public readonly int OpeningMcvCount = 1;

		[Desc("Ticks before retrying an opening structure request that never entered any production queue.")]
		public readonly int OpeningRequestRetryDelay = 250;
		public readonly int OpeningUnitRequestCooldown = 60;

		[Desc("Ticks to remember an accepted opening MCV request after it leaves production, preventing a duplicate during actor creation. Retried after this timeout if no request, queued item, live MCV, or completed MCV remains.")]
		public readonly int OpeningMcvRequestTimeout = 3000;
		public readonly int OpeningProgressLogInterval = 750;

		[Desc("Write opening goal, request, completion, and stall diagnostics to debug.log.")]
		public readonly bool OpeningDebugLogging = false;

		[Desc("Ordered radar-capable buildings used to replace a radar after one was established and lost.")]
		public readonly string[] RadarRecoveryTypes = System.Array.Empty<string>();

		[Desc("Ticks to retain a same-tick global radar recovery reservation before retrying an idle queue.")]
		public readonly int RadarRecoveryReservationTimeout = 250;

		[Desc("Ticks between bounded live-provider and commitment observations.")]
		public readonly int RadarRecoveryScanInterval = 25;

		[Desc("Write bounded radar establishment, reservation, and release transitions to debug.log.")]
		public readonly bool RadarRecoveryDebugLogging = false;

		[Desc("Owned repair-building types that should scale with the repairable aircraft fleet.")]
		public readonly HashSet<string> AirRepairBuildingTypes = new HashSet<string>();

		[Desc("Desired maximum live repairable aircraft per repair building. Zero disables fleet-based scaling.")]
		public readonly int AirUnitsPerRepairBuilding = 0;

		[Desc("Minimum repair buildings requested after the configured opening completes. Capped by BuildingLimits.")]
		public readonly int MinimumPostOpeningAirRepairBuildings = 0;

		[Desc("Write fleet-based air repair-capacity decisions to debug.log.")]
		public readonly bool AirRepairCapacityDebugLogging = false;

		[Desc("Enable bounded one-project-at-a-time development of configured Tiberium blossom trees.")]
		public readonly bool EnableTiberiumFieldPolicy = false;

		[Desc("Bot types that retain the same BaseBuilder configuration but disable Tiberium field projects for matched controls.")]
		public readonly HashSet<string> TiberiumFieldExcludedBotTypes = new HashSet<string>();

		[Desc("Actor types treated as durable Tiberium field identities.")]
		public readonly HashSet<string> TiberiumFieldTreeTypes = new HashSet<string>();

		[Desc("Configured tree types that require containment before Resonator activation.")]
		public readonly HashSet<string> TiberiumFieldRedTreeTypes = new HashSet<string>();

		[Desc("Ordered Resonator alternatives owned exclusively by the field policy while enabled.")]
		public readonly string[] TiberiumFieldResonatorTypes = System.Array.Empty<string>();

		[Desc("Ordered ordinary Power Plant alternatives used for remote field extension.")]
		public readonly string[] TiberiumFieldPowerTypes = System.Array.Empty<string>();

		[Desc("Ordered containment wall alternatives. Do not include mined chain-link walls.")]
		public readonly string[] TiberiumFieldWallTypes = System.Array.Empty<string>();

		[Desc("Owned stable building types whose accessible edge may anchor a field entrance.")]
		public readonly string[] TiberiumFieldGateBuildingTypes = System.Array.Empty<string>();

		[Desc("Cells between a red field's contained actors and its perimeter.")]
		public readonly int TiberiumFieldPerimeterStandoff = 4;

		[Desc("Target useful progress in cells for each necessary Power Plant extension.")]
		public readonly int TiberiumFieldExtensionStep = 6;

		[Desc("Ticks between bounded live-tree observations.")]
		public readonly int TiberiumFieldScanInterval = 50;

		[Desc("Ticks between repeated field deferral summaries while the reason is unchanged.")]
		public readonly int TiberiumFieldProgressLogInterval = 750;

		[Desc("Cells around a tree used to rank observed useful resource demand.")]
		public readonly int TiberiumFieldDemandRadius = 6;

		[Desc("Ticks between missing-only maintenance scans of active red enclosures.")]
		public readonly int TiberiumFieldMaintenanceInterval = 1500;

		[Desc("Spendable cash protected from a new discretionary field commitment.")]
		public readonly int TiberiumFieldProtectedCash = 5000;

		[Desc("Ticks to retain a field queue reservation before production accepts it.")]
		public readonly int TiberiumFieldReservationTimeout = 750;

		[Desc("Ticks to wait for a placed Resonator to become live and powered.")]
		public readonly int TiberiumFieldPlacementTimeout = 3000;

		[Desc("Consecutive failed field placements before entering a visible deferred state.")]
		public readonly int TiberiumFieldMaximumRetries = 3;

		[Desc("Ticks to defer a field project after bounded placement failures.")]
		public readonly int TiberiumFieldRetryDelay = 1500;

		[Desc("Write bounded field identity, reservation, placement, and coverage transitions to debug.log.")]
		public readonly bool TiberiumFieldDebugLogging = false;

		[Desc("Production queues AI uses for buildings.")]
		public readonly HashSet<string> BuildingQueues = new HashSet<string> { "Building" };

		[Desc("Production queues AI uses for defenses.")]
		public readonly HashSet<string> DefenseQueues = new HashSet<string> { "Defense" };

		[Desc("Minimum distance in cells from center of the base when checking for building placement.")]
		public readonly int MinBaseRadius = 2;

		[Desc("Radius in cells around the center of the base to expand.")]
		public readonly int MaxBaseRadius = 20;

		[Desc("Minimum excess power the AI should try to maintain.")]
		public readonly int MinimumExcessPower = 0;

		[Desc("The targeted excess power the AI tries to maintain cannot rise above this.")]
		public readonly int MaximumExcessPower = 0;

		[Desc("Increase maintained excess power by this amount for every ExcessPowerIncreaseThreshold of base buildings.")]
		public readonly int ExcessPowerIncrement = 0;

		[Desc("Increase maintained excess power by ExcessPowerIncrement for every N base buildings.")]
		public readonly int ExcessPowerIncreaseThreshold = 1;

		[Desc("Number of refineries to build before building a barracks.")]
		public readonly int InititalMinimumRefineryCount = 1;

		[Desc("Number of refineries to build additionally after building a barracks.")]
		public readonly int AdditionalMinimumRefineryCount = 1;

		[Desc("Additional delay (in ticks) between structure production checks when there is no active production.",
			"StructureProductionRandomBonusDelay is added to this.")]
		public readonly int StructureProductionInactiveDelay = 125;

		[Desc("Additional delay (in ticks) added between structure production checks when actively building things.",
			"Note: this should be at least as large as the typical order latency to avoid duplicated build choices.")]
		public readonly int StructureProductionActiveDelay = 25;

		[Desc("A random delay (in ticks) of up to this is added to active/inactive production delays.")]
		public readonly int StructureProductionRandomBonusDelay = 10;

		[Desc("Delay (in ticks) until retrying to build structure after the last 3 consecutive attempts failed.")]
		public readonly int StructureProductionResumeDelay = 1500;

		[Desc("After how many failed attempts to place a structure should AI give up and wait",
			"for StructureProductionResumeDelay before retrying.")]
		public readonly int MaximumFailedPlacementAttempts = 3;

		[Desc("How many randomly chosen cells with resources to check when deciding refinery placement.")]
		public readonly int MaxResourceCellsToCheck = 3;

		[Desc("Delay (in ticks) until rechecking for new BaseProviders.")]
		public readonly int CheckForNewBasesDelay = 1500;

		[Desc("Chance that the AI will place the defenses in the direction of the closest enemy building.")]
		public readonly int PlaceDefenseTowardsEnemyChance = 100;

		[Desc("Minimum range at which to build defensive structures near a combat hotspot.")]
		public readonly int MinimumDefenseRadius = 5;

		[Desc("Maximum range at which to build defensive structures near a combat hotspot.")]
		public readonly int MaximumDefenseRadius = 20;

		[Desc("Bot types that may use normal SAM construction for economy coverage.")]
		public readonly HashSet<string> EconomyDefenseBotTypes = new HashSet<string>();

		[Desc("Prerequisites required before economy SAM coverage becomes active.")]
		public readonly string[] EconomyDefensePrerequisites = System.Array.Empty<string>();

		[Desc("Anti-air defense structures owned by the economy-coverage placement policy.")]
		public readonly HashSet<string> EconomyDefenseSamTypes = new HashSet<string>();

		[Desc("Economy anchor types, in priority groups: unloading refineries, resonators, then used silos.")]
		public readonly HashSet<string> EconomyDefenseRefineryTypes = new HashSet<string>();
		public readonly HashSet<string> EconomyDefenseResonatorTypes = new HashSet<string>();
		public readonly HashSet<string> EconomyDefenseSiloTypes = new HashSet<string>();

		[Desc("Maximum total live or pending SAM sites owned by economy coverage.")]
		public readonly int EconomyDefenseMaximumSamSites = 0;

		[Desc("Minimum and maximum cells from the selected economy anchor to search for a SAM site.")]
		public readonly int EconomyDefenseSamMinimumRadius = 2;
		public readonly int EconomyDefenseSamMaximumRadius = 8;

		[Desc("Reserve this many cells of weapon range beyond the anchor for its likely air approach.")]
		public readonly int EconomyDefenseSamCoverageMarginCells = 2;

		[Desc("Refinery approach corridor excluded from economy SAM footprints.")]
		public readonly int EconomyDefenseRefineryLaneLengthCells = 7;
		public readonly int EconomyDefenseRefineryLaneHalfWidthCells = 1;

		[Desc("Stored-resource percentage required before a silo is a material economy anchor.")]
		public readonly int EconomyDefenseUsedSiloThresholdPercent = 25;

		[Desc("Write bounded economy SAM selection, coverage, and placement diagnostics to debug.log.")]
		public readonly bool EconomyDefenseSamDebugLogging = false;

		[Desc("Try to build another production building if there is too much cash.")]
		public readonly int NewProductionCashThreshold = 5000;

		[Desc("Enable sustained refinery-congestion and excess-cash economy scaling.")]
		public readonly bool EnableSmartEconomy = false;

		[Desc("Bot types that retain smart-economy observation/logging but do not act on it. Intended for matched scenario controls.")]
		public readonly HashSet<string> SmartEconomyExcludedBotTypes = new HashSet<string>();

		[Desc("Ticks between bounded smart-economy observations.")]
		public readonly int SmartEconomyScanInterval = 25;

		[Desc("Maximum distance in cells from a refinery delivery cell at which a linked loaded harvester counts toward unload congestion.")]
		public readonly int SmartEconomyRefineryQueueRadius = 4;

		[Desc("Waiting harvesters, after excluding one active refinery service slot, required to observe congestion.")]
		public readonly int SmartEconomyWaitingHarvesterThreshold = 2;

		[Desc("Resource-unloading refinery types eligible for congestion relief. Keep non-unloading economy structures out of this list.")]
		public readonly HashSet<string> SmartEconomyRefineryTypes = new HashSet<string>();

		[Desc("Ticks of persistent unload congestion required before requesting another refinery.")]
		public readonly int SmartEconomyRefineryPressureDuration = 750;

		[Desc("Ticks of persistent harvester/refinery capacity deficit required before requesting another refinery.")]
		public readonly int SmartEconomyRefineryCapacityPressureDuration = 250;

		[Desc("Evidence ticks at or below which active refinery pressure is released.")]
		public readonly int SmartEconomyRefineryPressureRelease = 250;

		[Desc("Ticks to retain one congestion-relief refinery decision while it enters construction. Queued construction remains protected beyond this timeout.")]
		public readonly int SmartEconomyRefineryBuildTimeout = 750;

		[Desc("Committed harvesters supported by each unloading refinery when calculating throughput demand.")]
		public readonly int SmartEconomyHarvestersPerRefinery = 2;

		[Desc("Harvesters expected to spawn for free from each pending smart-economy refinery.")]
		public readonly int SmartEconomyFreeHarvestersPerRefinery = 1;

		[Desc("Maximum smart-economy refineries that may be queued or reserved concurrently across idle construction yards.")]
		public readonly int SmartEconomyMaximumParallelRefineries = 3;

		[Desc("Percentage of active Fact construction slots reserved for early vehicle-factory capacity while that capacity is inadequate.")]
		public readonly int SmartEconomyEarlyVehicleFactoryPercent = 50;

		[Desc("Stored-resource percentage that requests silo capacity ahead of discretionary scaling.")]
		public readonly int SmartEconomyStorageThresholdPercent = 80;

		[Desc("Spendable cash that begins sustained excess-cash observation after the opening.")]
		public readonly int SmartEconomyExcessCashThreshold = 20000;

		[Desc("Spendable cash below which active excess-cash pressure begins releasing.")]
		public readonly int SmartEconomyExcessCashReleaseThreshold = 12000;

		[Desc("Ticks of persistent excessive cash required before scaling expansion capacity.")]
		public readonly int SmartEconomyExcessCashPressureDuration = 750;

		[Desc("Evidence ticks at or below which active excess-cash pressure is released.")]
		public readonly int SmartEconomyExcessCashPressureRelease = 250;

		[Desc("MCV alternatives used for excess-cash base expansion.")]
		public readonly string[] SmartEconomyMcvTypes = System.Array.Empty<string>();

		[Desc("Additional sustained spendable cash required per desired expansion asset. This is separate from the lower pressure threshold so production can scale before several MCVs are requested.")]
		public readonly int SmartEconomyExpansionCashPerAsset = 35000;

		[Desc("Minimum army value as a percentage of non-army asset value before excess cash may request another MCV. Production scaling is not gated by this value.")]
		public readonly int SmartEconomyExpansionMinimumArmyPercent = 20;

		[Desc("Maximum combined deployed construction yards, live MCVs, and outstanding smart-economy MCV requests.")]
		public readonly int SmartEconomyMaximumExpansionAssets = 8;

		[Desc("Ticks between smart-economy attempts to request an expansion MCV.")]
		public readonly int SmartEconomyMcvRequestCooldown = 250;

		[Desc("Ticks to retain an accepted smart-economy MCV request while it moves from production into the world. Retried after this timeout only when no request or queued item remains and expansion capacity did not increase.")]
		public readonly int SmartEconomyMcvRequestTimeout = 3000;

		[Desc("Ticks between periodic smart-economy progress samples written when debug logging is enabled.")]
		public readonly int SmartEconomyProgressLogInterval = 750;

		[Desc("Write smart-economy observations, transitions, and requests to debug.log.")]
		public readonly bool SmartEconomyDebugLogging = false;

		[Desc("Radius in cells around a factory scanned for rally points by the AI.")]
		public readonly int RallyPointScanRadius = 8;

		[Desc("Radius in cells around each building with ProvideBuildableArea",
			"to check for a 3x3 area of water where naval structures can be built.",
			"Should match maximum adjacency of naval structures.")]
		public readonly int CheckForWaterRadius = 8;

		[Desc("Terrain types which are considered water for base building purposes.")]
		public readonly HashSet<string> WaterTerrainTypes = new HashSet<string> { "Water" };

		[Desc("Building types that are placed as walls using line building instead of being dropped",
			"on a free cell somewhere in the base. Leave empty to disable wall building.")]
		public readonly HashSet<string> WallTypes = new HashSet<string>();

		[Desc("Defensive building types the AI puts a wall in front of. Usually its towers.")]
		public readonly HashSet<string> WalledDefenseTypes = new HashSet<string>();

		[Desc("Preferred wall types used to enclose the first construction yard. Leave empty to disable the enclosure.")]
		public readonly string[] ConstructionYardEnclosureWallTypes = System.Array.Empty<string>();

		[Desc("Empty cells left between the construction yard footprint and its enclosure.")]
		public readonly int ConstructionYardEnclosureMargin = 1;

		[Desc("Width of the deliberate vehicle access opening in the construction-yard enclosure.")]
		public readonly int ConstructionYardEnclosureAccessWidth = 3;

		[Desc("Absolute world tick when construction-yard enclosure planning, reservations, and repairs stop.")]
		public readonly int ConstructionYardEnclosureCutoffTick = 7500;

		[Desc("Ticks between bounded missing-cell maintenance scans for the construction-yard enclosure.")]
		public readonly int ConstructionYardEnclosureMaintenanceInterval = 250;

		[Desc("Write construction-yard enclosure planning and failure diagnostics to debug.log.")]
		public readonly bool ConstructionYardEnclosureDebugLogging = false;

		[Desc("Defensive building types whose BuildingFractions ceiling adapts based on their measured kills-value/losses-value ratio",
			"(see AdaptiveWeighting). proc/nuke/silo/production buildings are already priority-overridden earlier in",
			"ChooseBuildingToBuild and must never be listed here - adaptation would fight that logic.")]
		public readonly HashSet<string> AdaptiveBuildingTypes = new HashSet<string>();

		[Desc("Combat samples (kills+losses) an adaptive defense type needs before its decayed score is fully trusted.")]
		public readonly int AdaptiveConfidenceSamples = 10;

		[Desc("Minimum/maximum share (0-1) of the base an adaptive defense type's ceiling may be nudged to.")]
		public readonly float AdaptiveWeightFloor = 0.01f;
		public readonly float AdaptiveWeightCeiling = 0.5f;

		[Desc("How many cells in front of a tower, on its enemy facing side, its wall is placed.")]
		public readonly int WallDistanceFromTower = 3;

		[Desc("Hard cap on the number of wall actors the AI will own. Zero disables wall building,",
			"which is the default so other mods and bots are unaffected.")]
		public readonly int MaximumWallSegments = 0;

		[Desc("Name of the locomotor used to verify the AI can still move around after walling.")]
		public readonly string WallPathCheckLocomotor = "wheeled";

		[Desc("Enable one attacked-tower defense cluster. Default-off so other bots and mods retain control behavior.")]
		public readonly bool EnableDefenseClusterPolicy = false;

		[Desc("Ordered tower alternatives owned by the active defense cluster.")]
		public readonly string[] DefenseClusterTowerTypes = System.Array.Empty<string>();

		[Desc("Configured tower capabilities. Actors are also validated against their loaded weapon target capability.")]
		public readonly HashSet<string> DefenseClusterAntiInfantryTypes = new HashSet<string>();
		public readonly HashSet<string> DefenseClusterAntiGroundTypes = new HashSet<string>();
		public readonly HashSet<string> DefenseClusterAntiAirTypes = new HashSet<string>();

		[Desc("Ordered local passive-repair facility alternatives.")]
		public readonly string[] DefenseClusterRepairFacilityTypes = System.Array.Empty<string>();

		[Desc("Minimum distinct live tower actors and maximum tower-center radius for a complete cluster.")]
		public readonly int DefenseClusterMinimumTowers = 3;
		public readonly int DefenseClusterRadius = 9;

		[Desc("Minimum and maximum top-left search radius for cluster tower/facility placements.")]
		public readonly int DefenseClusterPlacementMinimumRadius = 2;
		public readonly int DefenseClusterPlacementMaximumRadius = 8;

		[Desc("Ticks that the first attacked tower retains the primary anchor, and reservation/retry timing.")]
		public readonly int DefenseClusterAnchorLease = 750;
		public readonly int DefenseClusterReservationTimeout = 750;
		public readonly int DefenseClusterMaintenanceInterval = 125;
		public readonly int DefenseClusterRetryDelay = 750;
		public readonly int DefenseClusterMaximumPlacementFailures = 3;

		[Desc("Open-screen geometry: enemy-facing setback, half-width, and flank depth in cells.")]
		public readonly int DefenseClusterWallSetback = 3;
		public readonly int DefenseClusterWallHalfWidth = 4;
		public readonly int DefenseClusterWallFlankDepth = 3;

		[Desc("Maximum cells examined by each local route check.")]
		public readonly int DefenseClusterPathCheckMaximumCells = 3000;

		[Desc("Ticks to retain an attacked non-tower structure as the next ordinary defense placement center.")]
		public readonly int DefenseClusterOrdinaryDefenseLease = 750;

		[Desc("What buildings to the AI should build.", "What integer percentage of the total base must be this type of building.")]
		public readonly Dictionary<string, int> BuildingFractions = null;

		[Desc("What buildings should the AI have a maximum limit to build.")]
		public readonly Dictionary<string, int> BuildingLimits = null;

		[Desc("When should the AI start building specific buildings.")]
		public readonly Dictionary<string, int> BuildingDelays = null;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (RadarRecoveryTypes.Length > 0)
			{
				if (RadarRecoveryReservationTimeout <= 0 || RadarRecoveryScanInterval <= 0 ||
					RadarRecoveryTypes.Distinct(System.StringComparer.Ordinal).Count() != RadarRecoveryTypes.Length)
					throw new YamlException("Radar recovery types must be unique and use positive reservation and scan intervals.");

				foreach (var actorType in RadarRecoveryTypes)
					if (!rules.Actors.TryGetValue(actorType, out var actor) ||
						actor.TraitInfoOrDefault<BuildingInfo>() == null || !actor.HasTraitInfo<ProvidesRadarInfo>())
						throw new YamlException($"Radar recovery actor '{actorType}' must be a building with ProvidesRadar.");
			}

			if (EnableDefenseClusterPolicy)
			{
				if (DefenseClusterTowerTypes.Length == 0 || DefenseClusterRepairFacilityTypes.Length == 0 ||
					WallTypes.Count == 0 || DefenseClusterMinimumTowers < 3 || DefenseClusterRadius <= 0 ||
					DefenseClusterPlacementMinimumRadius < 0 ||
					DefenseClusterPlacementMaximumRadius < DefenseClusterPlacementMinimumRadius ||
					DefenseClusterRadius < DefenseClusterPlacementMaximumRadius || DefenseClusterAnchorLease <= 0 ||
					DefenseClusterReservationTimeout <= 0 || DefenseClusterMaintenanceInterval <= 0 ||
					DefenseClusterRetryDelay <= 0 || DefenseClusterMaximumPlacementFailures <= 0 ||
					DefenseClusterWallSetback <= 0 || DefenseClusterWallHalfWidth < 2 ||
					DefenseClusterWallFlankDepth <= 0 || DefenseClusterPathCheckMaximumCells < 64 ||
					DefenseClusterOrdinaryDefenseLease <= 0)
					throw new YamlException("Defense cluster tower/facility/wall types, count, radii, lease, retry, screen, and path caps must be configured and valid.");

				var towerTypes = new HashSet<string>(DefenseClusterTowerTypes);
				if (towerTypes.Count != DefenseClusterTowerTypes.Length ||
					DefenseClusterAntiInfantryTypes.Count == 0 || DefenseClusterAntiGroundTypes.Count == 0 ||
					DefenseClusterAntiAirTypes.Count == 0 ||
					DefenseClusterAntiInfantryTypes.Concat(DefenseClusterAntiGroundTypes)
						.Concat(DefenseClusterAntiAirTypes).Any(t => !towerTypes.Contains(t)))
					throw new YamlException("Defense cluster role types must be non-empty subsets of the unique tower list.");

				ValidateDefenseClusterRole(rules, DefenseClusterAntiInfantryTypes,
					new BitSet<TargetableType>("Ground", "Infantry"), "anti-infantry");
				ValidateDefenseClusterRole(rules, DefenseClusterAntiGroundTypes,
					new BitSet<TargetableType>("Ground", "Vehicle"), "anti-ground");
				ValidateDefenseClusterRole(rules, DefenseClusterAntiAirTypes,
					new BitSet<TargetableType>("Air"), "anti-air");

				foreach (var towerType in towerTypes)
					if (!rules.Actors.TryGetValue(towerType, out var tower) ||
						tower.TraitInfoOrDefault<BuildingInfo>() == null || !tower.TraitInfos<ArmamentInfo>().Any())
						throw new YamlException($"Defense cluster tower '{towerType}' must be a building with an armament.");

				foreach (var facilityType in DefenseClusterRepairFacilityTypes)
					if (!rules.Actors.TryGetValue(facilityType, out var facility) ||
						facility.TraitInfoOrDefault<BuildingInfo>() == null ||
						!facility.TraitInfos<GrantConditionInRangeInfo>().Any(t => t.Granter &&
							t.ValidRelationships.HasRelationship(PlayerRelationship.Ally) && t.Range.Length > 0))
						throw new YamlException($"Defense cluster facility '{facilityType}' must be a building with an allied repair-range granter.");

				foreach (var wallType in WallTypes)
					if (!rules.Actors.TryGetValue(wallType, out var wall) || wall.TraitInfoOrDefault<BuildingInfo>() == null ||
						wall.TraitInfoOrDefault<LineBuildInfo>() == null || wall.TraitInfoOrDefault<SellableInfo>() == null)
						throw new YamlException($"Defense cluster wall '{wallType}' must be a sellable line-build building.");
			}

			if (EconomyDefenseSamTypes.Count == 0)
				return;

			if (EconomyDefenseBotTypes.Count == 0 || EconomyDefensePrerequisites.Length == 0 ||
				EconomyDefenseRefineryTypes.Count == 0 || EconomyDefenseMaximumSamSites <= 0 ||
				EconomyDefenseSamMinimumRadius < 0 ||
				EconomyDefenseSamMaximumRadius < EconomyDefenseSamMinimumRadius ||
				EconomyDefenseSamCoverageMarginCells < 0 || EconomyDefenseRefineryLaneLengthCells <= 0 ||
				EconomyDefenseRefineryLaneHalfWidthCells < 0 || EconomyDefenseUsedSiloThresholdPercent < 0 ||
				EconomyDefenseUsedSiloThresholdPercent > 100)
				throw new YamlException("Economy SAM bot/prerequisite/actor types, cap, radii, lane, and silo threshold must be configured and valid.");

			foreach (var actorType in EconomyDefenseSamTypes.Concat(EconomyDefenseRefineryTypes)
				.Concat(EconomyDefenseResonatorTypes).Concat(EconomyDefenseSiloTypes))
				if (!rules.Actors.ContainsKey(actorType))
					throw new YamlException($"Economy SAM actor '{actorType}' does not exist.");

			foreach (var samType in EconomyDefenseSamTypes)
			{
				var actor = rules.Actors[samType];
				if (actor.TraitInfoOrDefault<BuildingInfo>() == null ||
					!actor.TraitInfos<ArmamentInfo>().Any())
					throw new YamlException($"Economy SAM actor '{samType}' must be a building with an armament.");
			}
		}

		static void ValidateDefenseClusterRole(Ruleset rules, IEnumerable<string> actorTypes,
			BitSet<TargetableType> targetTypes, string role)
		{
			foreach (var actorType in actorTypes)
				if (!rules.Actors.TryGetValue(actorType, out var actor) ||
					!actor.TraitInfos<ArmamentInfo>().Any(a => !string.IsNullOrEmpty(a.Weapon) &&
						rules.Weapons.TryGetValue(a.Weapon.ToLowerInvariant(), out var weapon) &&
						weapon.IsValidTarget(targetTypes)))
					throw new YamlException($"Defense cluster {role} actor '{actorType}' has no weapon capable of targeting that role.");
		}

		public override object Create(ActorInitializer init) { return new BaseBuilderBotModule(init.Self, this); }
	}

	public class BaseBuilderBotModule : ConditionalTrait<BaseBuilderBotModuleInfo>, IGameSaveTraitData,
		IBotTick, IBotPositionsUpdated, IBotRespondToAttack, IBotRequestPauseUnitProduction,
		IBotTemporaryUnitControl
	{
		const int OpeningRadarGoal = 5;
		const int OpeningDefenseGoal = 8;

		sealed class AirRepairBuildingReservation
		{
			public string Type;
			public int Tick;
		}

		public CPos GetRandomBaseCenter()
		{
			var randomConstructionYard = world.Actors.Where(a => a.Owner == player &&
				Info.ConstructionYardTypes.Contains(a.Info.Name))
				.RandomOrDefault(world.LocalRandom);

			return randomConstructionYard?.Location ?? initialBaseCenter;
		}

		public CPos DefenseCenter => defenseCenter;

		internal BaseBuilderWallPlanner WallPlanner { get; private set; }
		internal BaseBuilderFirstTowerPlanner FirstTowerPlanner { get; private set; }
		internal BaseBuilderTiberiumFieldManager TiberiumFieldManager { get; private set; }
		internal BaseBuilderDefenseClusterManager DefenseClusterManager { get; private set; }

		readonly World world;
		readonly Player player;
		PowerManager playerPower;
		PlayerResources playerResources;
		PlayerStatistics playerStatistics;
		TechTree techTree;
		IResourceLayer resourceLayer;
		IBotPositionsUpdated[] positionsUpdatedModules;
		IBotRequestUnitProduction[] unitProduction;
		IBotRallyPointManager[] rallyPointManagers;
		CPos initialBaseCenter;
		CPos defenseCenter;
		readonly Dictionary<int, int> openingStructureReservations = new Dictionary<int, int>();
		readonly HashSet<int> loggedCompletedOpeningGoals = new HashSet<int>();
		readonly HashSet<int> skippedOpeningGoals = new HashSet<int>();
		bool openingCompletionLogged;
		bool openingInitialized;
		int openingSoldierBuiltBaseline;
		int openingMcvBuiltBaseline;
		int nextOpeningSoldierRequestTick;
		int nextOpeningHarvesterRequestTick;
		int nextOpeningDefenseUnlockRequestTick;
		int nextOpeningMcvRequestTick;
		bool openingMcvRequestOutstanding;
		int openingMcvRequestExpiryTick;
		int nextOpeningProgressLogTick;
		readonly Dictionary<uint, AirRepairBuildingReservation> airRepairBuildingReservations =
			new Dictionary<uint, AirRepairBuildingReservation>();
		BaseBuilderSmartEconomyManager smartEconomy;
		BaseBuilderEconomyDefenseSamPlanner economyDefenseSam;
		BaseBuilderRadarRecoveryManager radarRecovery;
		BaseBuilderRadarStoragePressureManager radarStoragePressure;

		readonly List<BaseBuilderQueueManager> builders = new List<BaseBuilderQueueManager>();
		UnitBuilderBotModule[] unitBuilders;

		public BaseBuilderBotModule(Actor self, BaseBuilderBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			playerPower = self.Owner.PlayerActor.TraitOrDefault<PowerManager>();
			playerResources = self.Owner.PlayerActor.Trait<PlayerResources>();
			playerStatistics = self.Owner.PlayerActor.Trait<PlayerStatistics>();
			techTree = self.Owner.PlayerActor.Trait<TechTree>();
			resourceLayer = self.World.WorldActor.TraitOrDefault<IResourceLayer>();
			positionsUpdatedModules = self.Owner.PlayerActor.TraitsImplementing<IBotPositionsUpdated>().ToArray();
			unitBuilders = self.Owner.PlayerActor.TraitsImplementing<UnitBuilderBotModule>().ToArray();
			unitProduction = self.Owner.PlayerActor.TraitsImplementing<IBotRequestUnitProduction>().ToArray();
			rallyPointManagers = self.Owner.PlayerActor.TraitsImplementing<IBotRallyPointManager>().ToArray();
			WallPlanner = new BaseBuilderWallPlanner(this, player);
			FirstTowerPlanner = new BaseBuilderFirstTowerPlanner(this, player);
			DefenseClusterManager = new BaseBuilderDefenseClusterManager(this, player, playerPower);
			TiberiumFieldManager = new BaseBuilderTiberiumFieldManager(this, player,
				playerResources, playerPower, resourceLayer);
			if (Info.EnableSmartEconomy)
				smartEconomy = new BaseBuilderSmartEconomyManager(this, player, playerResources, unitProduction);
			if (Info.EconomyDefenseSamTypes.Count > 0)
				economyDefenseSam = new BaseBuilderEconomyDefenseSamPlanner(this, player, playerPower,
					playerResources, techTree);
			if (Info.RadarRecoveryTypes.Length > 0)
			{
				radarRecovery = new BaseBuilderRadarRecoveryManager(this, player);
				radarStoragePressure = new BaseBuilderRadarStoragePressureManager(this, player);
			}
		}

		internal ActorInfo EconomyDefenseSamBuilding(ProductionQueue queue, IEnumerable<ActorInfo> buildables)
		{
			return economyDefenseSam?.ChooseBuilding(queue, buildables);
		}

		internal bool OwnsEconomyDefenseSam(ProductionQueue queue, string actorType)
		{
			return economyDefenseSam?.OwnsPlacement(queue, actorType) == true;
		}

		internal CPos? EconomyDefenseSamLocation(ProductionQueue queue, string actorType, ActorInfo actorInfo,
			BuildingInfo buildingInfo,
			bool distanceToBaseIsImportant)
		{
			return economyDefenseSam?.ChooseLocation(queue, actorType, actorInfo, buildingInfo,
				distanceToBaseIsImportant);
		}

		internal bool AdaptiveProductionDebugLogging => unitBuilders.Any(u =>
			!u.IsTraitDisabled && u.Info.AdaptiveProductionDebugLogging);

		internal void LogAdaptiveProduction(string format, params object[] args)
		{
			AIUtils.BotDebug(format, args);
			Log.Write("debug", "AI adaptive production: " + format, args);
		}

		internal void LogProductionSpend(ActorInfo actor, ProductionQueue queue, int amount = 1)
		{
			if (!AdaptiveProductionDebugLogging)
				return;

			var cost = System.Math.Max(0, actor.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0) * System.Math.Max(1, amount);
			Log.Write("debug", "AI production spend: {0} tick={1} item={2} amount={3} queue={4} cost={5}",
				player, world.WorldTick, actor.Name, System.Math.Max(1, amount), queue.Info.Type, cost);
		}

		internal double AdaptiveProductionBuildingDemand(ActorInfo building)
		{
			return unitBuilders.Where(u => !u.IsTraitDisabled)
				.Select(u => u.ProductionBuildingDemand(building)).DefaultIfEmpty(0).Max();
		}

		internal ActorInfo RadarRecoveryBuilding(IEnumerable<ActorInfo> buildables)
		{
			return radarRecovery?.Candidate(buildables);
		}

		internal bool RadarRecoveryNeeded => radarRecovery?.NeedsRecovery == true;

		internal ActorInfo RadarRecoveryStoragePressureSilo(ProductionQueue queue, int minimumExcessPower)
		{
			return radarStoragePressure?.Candidate(queue, minimumExcessPower);
		}

		internal bool RadarRecoveryStoragePressureOwnsSelection => radarStoragePressure?.OwnsSelection == true;

		internal bool RadarRecoveryStoragePressureBlocksRadar => radarStoragePressure?.BlocksRadar == true;

		internal void ObserveRadarRecoveryQueueChoice(ProductionQueue queue, IEnumerable<ActorInfo> buildables,
			bool essentialPowerBlocked, bool essentialRefineryBlocked)
		{
			radarRecovery?.ObserveQueueChoice(queue, buildables, essentialPowerBlocked, essentialRefineryBlocked);
		}

		internal void ObserveBusyRadarRecoveryQueue(ProductionQueue queue, ProductionItem currentBuilding)
		{
			radarRecovery?.ObserveBusyQueue(queue, currentBuilding);
		}

		internal void RadarRecoveryPlacementFailed(ProductionQueue queue, string actorType)
		{
			radarRecovery?.PlacementFailed(queue, actorType);
		}

		internal bool TryReserveRadarRecovery(ProductionQueue queue, string actorType)
		{
			return radarRecovery?.TryReserve(queue, actorType) ?? false;
		}

		internal ActorInfo AirRepairCapacityBuilding(IEnumerable<ActorInfo> buildables)
		{
			if (Info.AirUnitsPerRepairBuilding <= 0 || Info.AirRepairBuildingTypes.Count == 0)
				return null;

			RefreshAirRepairBuildingReservations();
			foreach (var building in buildables.Where(b => Info.AirRepairBuildingTypes.Contains(b.Name))
				.OrderBy(b => b.Name, System.StringComparer.Ordinal))
			{
				if (!Info.BuildingLimits.TryGetValue(building.Name, out var limit))
					continue;

				var aircraft = RepairableAircraftCount(building.Name);
				var desired = System.Math.Min(limit, System.Math.Max(
					openingCompletionLogged ? System.Math.Max(0, Info.MinimumPostOpeningAirRepairBuildings) : 0,
					AirRepairCapacityPolicy.DesiredBuildings(aircraft, Info.AirUnitsPerRepairBuilding, limit)));
				var committed = CountActors(new[] { building.Name }) +
					CountQueuedOrPendingActors(new[] { building.Name }) +
					airRepairBuildingReservations.Values.Count(r => r.Type == building.Name);
				if (committed < desired)
					return building;
			}

			return null;
		}

		internal bool TryReserveAirRepairCapacity(ProductionQueue queue, string type)
		{
			if (queue == null || queue.AllQueued().Any() || !Info.AirRepairBuildingTypes.Contains(type))
				return false;

			RefreshAirRepairBuildingReservations();
			if (!Info.BuildingLimits.TryGetValue(type, out var limit))
				return false;

			var aircraft = RepairableAircraftCount(type);
			var desired = System.Math.Min(limit, System.Math.Max(
				openingCompletionLogged ? System.Math.Max(0, Info.MinimumPostOpeningAirRepairBuildings) : 0,
				AirRepairCapacityPolicy.DesiredBuildings(aircraft, Info.AirUnitsPerRepairBuilding, limit)));
			var committed = CountActors(new[] { type }) + CountQueuedOrPendingActors(new[] { type }) +
				airRepairBuildingReservations.Values.Count(r => r.Type == type);
			if (committed >= desired)
				return false;

			airRepairBuildingReservations[queue.Actor.ActorID] = new AirRepairBuildingReservation
			{
				Type = type,
				Tick = world.WorldTick
			};
			if (Info.AirRepairCapacityDebugLogging)
				Log.Write("debug", "AI air repair capacity: {0} reserved {1}: aircraft={2}, committed={3}, desired={4}.",
					player, type, aircraft, committed, desired);
			return true;
		}

		void RefreshAirRepairBuildingReservations()
		{
			foreach (var reservation in airRepairBuildingReservations.ToArray())
			{
				var actor = world.GetActorById(reservation.Key);
				var queued = actor != null && actor.TraitsImplementing<ProductionQueue>()
					.Any(q => q.AllQueued().Any(i => i.Item == reservation.Value.Type));
				if (queued || world.WorldTick - reservation.Value.Tick >= System.Math.Max(1, Info.OpeningRequestRetryDelay))
					airRepairBuildingReservations.Remove(reservation.Key);
			}
		}

		int RepairableAircraftCount(string repairBuildingType)
		{
			return world.Actors.Count(a => a.Owner == player && !a.IsDead && a.IsInWorld &&
				a.Info.HasTraitInfo<AircraftInfo>() &&
				a.Info.TraitInfoOrDefault<RepairableInfo>()?.RepairActors.Contains(repairBuildingType) == true);
		}

		protected override void TraitEnabled(Actor self)
		{
			foreach (var building in Info.BuildingQueues)
				builders.Add(new BaseBuilderQueueManager(this, building, player, playerPower, playerResources, resourceLayer));
			foreach (var defense in Info.DefenseQueues)
				builders.Add(new BaseBuilderQueueManager(this, defense, player, playerPower, playerResources, resourceLayer));
		}

		void IBotPositionsUpdated.UpdatedBaseCenter(CPos newLocation)
		{
			initialBaseCenter = newLocation;
		}

		void IBotPositionsUpdated.UpdatedDefenseCenter(CPos newLocation)
		{
			defenseCenter = newLocation;
		}

		bool IBotRequestPauseUnitProduction.PauseUnitProduction => !IsTraitDisabled &&
			(!HasAdequateRefineryCount || SmartEconomyShouldReserveCashForRefinery);

		void IBotTick.BotTick(IBot bot)
		{
			radarRecovery?.Update();
			WallPlanner?.Tick(bot);
			UpdateOpening(bot);
			smartEconomy?.Tick(bot);
			TiberiumFieldManager?.Tick();
			DefenseClusterManager?.Tick(bot);
			FirstTowerPlanner.Update();
			SetRallyPointsForNewProductionBuildings(bot);

			if (Info.PrioritizeOpeningFirstTower && !FirstTowerPlanner.Complete)
			{
				foreach (var b in builders.Where(b => b.IsDefenseQueue))
					b.Tick(bot);
				foreach (var b in builders.Where(b => !b.IsDefenseQueue))
					b.Tick(bot);
			}
			else
				foreach (var b in builders)
					b.Tick(bot);
		}

		bool IBotTemporaryUnitControl.IsUnitTemporarilyControlled(Actor actor)
		{
			return !IsTraitDisabled && WallPlanner != null && WallPlanner.IsUnitTemporarilyControlled(actor);
		}

		internal bool OpeningActive => Info.EnableOpeningPolicy && !openingCompletionLogged && !OpeningComplete;

		internal bool OpeningOwnsMcvProduction => Info.EnableOpeningPolicy && !openingCompletionLogged &&
			!string.IsNullOrEmpty(Info.OpeningMcvType) && OpeningMcvsBuilt < Info.OpeningMcvCount;

		internal bool SmartEconomyWantsRefinery => smartEconomy?.WantsRefinery ?? false;

		internal bool SmartEconomyEnabled => smartEconomy?.Enabled ?? false;

		internal bool SmartEconomyWantsProductionCapacity => smartEconomy?.WantsProductionCapacity ?? false;

		internal bool SmartEconomyWantsEarlyVehicleProductionCapacity =>
			smartEconomy?.WantsEarlyVehicleProductionCapacity ?? false;

		internal bool SmartEconomyWantsSilo => smartEconomy?.WantsSilo ?? false;

		internal HashSet<string> SmartEconomyRefineryTypes => Info.SmartEconomyRefineryTypes.Count > 0 ?
			Info.SmartEconomyRefineryTypes : Info.RefineryTypes;

		internal HashSet<string> SmartEconomyHarvesterTypes => unitBuilders
			.SelectMany(u => u.Info.HarvesterTypes).ToHashSet();

		internal bool SmartEconomyShouldReserveCashForRefinery => smartEconomy?.ShouldReserveCashForRefinery ?? false;

		internal bool SmartEconomySerializesMissingRefinery => smartEconomy?.SerializesMissingRefinery ?? false;

		internal bool TryReserveSmartEconomyRefinery(ProductionQueue queue, string type)
		{
			return smartEconomy?.TryReserveRefineryBuild(queue, type) ?? false;
		}

		internal bool TryReserveSmartEconomyMissingRefinery(ProductionQueue queue, string type)
		{
			return smartEconomy?.TryReserveMissingRefineryBuild(queue, type) ?? false;
		}

		internal bool TryReserveSmartEconomyVehicleFactory(ProductionQueue queue, string type)
		{
			return smartEconomy?.TryReserveVehicleFactoryBuild(queue, type) ?? false;
		}

		internal bool TryReserveSmartEconomyControlledRefinery(ProductionQueue queue, string type)
		{
			if (!SmartEconomyEnabled)
				return true;

			return SmartEconomySerializesMissingRefinery ?
				TryReserveSmartEconomyMissingRefinery(queue, type) :
				TryReserveSmartEconomyRefinery(queue, type);
		}

		internal bool CanBuildAnotherSmartEconomyRefinery(string type)
		{
			return SmartEconomyRefineryTypes.Contains(type) && IsCurrentlyBuildable(type);
		}

		internal void LogSmartEconomy(string format, params object[] args)
		{
			AIUtils.BotDebug(format, args);
			if (Info.SmartEconomyDebugLogging)
				Log.Write("debug", "AI smart economy: " + format, args);
		}

		internal ActorInfo OpeningBuilding(IEnumerable<ActorInfo> buildables)
		{
			if (!OpeningActive)
				return null;

			RefreshOpeningStructureReservations();
			var goals = OpeningStructureGoals;
			var completed = CompletedOpeningStructureGoals(goals);
			var buildableArray = buildables.ToArray();
			var goal = OpeningPolicyLogic.FirstBuildableGoal(
				goals, completed, openingStructureReservations.Keys.ToArray(), buildableArray.Select(a => a.Name));
			if (goal < 0)
				return null;

			var selected = OpeningPolicyLogic.FirstAvailable(goals[goal], buildableArray.Select(a => a.Name));
			if (selected == null)
				return null;

			openingStructureReservations[goal] = world.WorldTick;
			LogOpening("{0} reserved structure goal {1}: {2}", player, OpeningGoalName(goal), selected);
			return buildableArray.First(a => a.Name == selected);
		}

		internal bool IsOpeningStructureReserved(string type)
		{
			var goals = OpeningStructureGoals;
			return openingStructureReservations.Keys.Any(i => i >= 0 && i < goals.Count && goals[i].Contains(type));
		}

		IReadOnlyList<string[]> OpeningStructureGoals => new[]
		{
			Info.OpeningPowerTypes,
			Info.OpeningBarracksTypes,
			Info.OpeningRefineryTypes,
			Info.OpeningFactoryTypes,
			Info.OpeningAdditionalFactoryTypes,
			Info.OpeningRadarTypes,
			Info.OpeningHelipadTypes,
			Info.OpeningSiloTypes,
			Info.OpeningDefenseTypes
		};

		IReadOnlyList<int> OpeningStructureGoalCounts => new[]
		{
			1,
			1,
			1,
			1,
			System.Math.Max(0, Info.OpeningAdditionalFactoryCount),
			1,
			1,
			1,
			1
		};

		static string OpeningGoalName(int goal)
		{
			var names = new[] { "power", "barracks", "refinery", "factory", "additional factory", "radar", "helipad", "silo", "anti-ground defense" };
			return goal >= 0 && goal < names.Length ? names[goal] : goal.ToString();
		}

		HashSet<int> CompletedOpeningStructureGoals(IReadOnlyList<string[]> goals)
		{
			var completed = new HashSet<int>(skippedOpeningGoals);
			var counts = OpeningStructureGoalCounts;
			for (var i = 0; i < goals.Count; i++)
			{
				// Empty goals are intentionally disabled by configuration.
				if (goals[i].Length == 0 || counts[i] <= 0 || CountActors(goals[i]) >= counts[i])
				{
					completed.Add(i);
					if (loggedCompletedOpeningGoals.Add(i))
						LogOpening("{0} completed structure goal {1}", player, OpeningGoalName(i));
				}
			}

			return completed;
		}

		void RefreshOpeningStructureReservations()
		{
			var goals = OpeningStructureGoals;
			var counts = OpeningStructureGoalCounts;
			var queues = Info.BuildingQueues.Concat(Info.DefenseQueues)
				.SelectMany(q => AIUtils.FindQueues(player, q)).ToArray();
			foreach (var reservation in openingStructureReservations.ToArray())
			{
				var completed = reservation.Key < goals.Count &&
					CountActors(goals[reservation.Key]) >= counts[reservation.Key];
				var queued = reservation.Key < goals.Count && CountActors(goals[reservation.Key]) + queues.Sum(q =>
					q.AllQueued().Count(item => goals[reservation.Key].Contains(item.Item))) >= counts[reservation.Key];
				if (completed || OpeningPolicyLogic.RetryReservation(reservation.Value, world.WorldTick,
					Info.OpeningRequestRetryDelay, queued))
				{
					openingStructureReservations.Remove(reservation.Key);
					if (!completed)
						LogOpening("{0} released stalled structure goal {1} for retry", player, OpeningGoalName(reservation.Key));
				}
			}
		}

		void UpdateOpening(IBot bot)
		{
			if (!Info.EnableOpeningPolicy)
				return;

			if (!openingInitialized)
			{
				openingInitialized = true;
				openingSoldierBuiltBaseline = TotalBuilt(Info.OpeningSoldierTypes);
				openingMcvBuiltBaseline = string.IsNullOrEmpty(Info.OpeningMcvType) ? 0 :
					TotalBuilt(new[] { Info.OpeningMcvType });
			}

			if (openingCompletionLogged)
				return;

			var completedStructures = CompletedOpeningStructureGoals(OpeningStructureGoals);
			CompleteUnavailableOptionalOpeningStructureGoals(completedStructures);
			UpdateOpeningMcvRequestState(bot);
			if (Info.OpeningDebugLogging && world.WorldTick >= nextOpeningProgressLogTick)
			{
				nextOpeningProgressLogTick = world.WorldTick + System.Math.Max(1, Info.OpeningProgressLogInterval);
				LogOpening("{0} progress: structures={1}/{2}, soldiers={3}/{4}, harvesters={5}/{6}, mcvs-built={7}/{8}",
					player, completedStructures.Count, OpeningStructureGoals.Count,
					OpeningSoldiersBuilt, Info.OpeningSoldierCount,
					OpeningCommittedHarvesters, Info.OpeningHarvesterCount,
					OpeningMcvsBuilt, Info.OpeningMcvCount);
			}

			if (Info.OpeningSoldierTypes.Length > 0 && OpeningSoldiersBuilt < Info.OpeningSoldierCount &&
				world.WorldTick >= nextOpeningSoldierRequestTick &&
				RequestFirstAvailable(bot, Info.OpeningSoldierTypes, "opening soldiers"))
				nextOpeningSoldierRequestTick = world.WorldTick + System.Math.Max(1, Info.OpeningUnitRequestCooldown);

			if (Info.OpeningHarvesterTypes.Length > 0 && OpeningCommittedHarvesters < Info.OpeningHarvesterCount &&
				world.WorldTick >= nextOpeningHarvesterRequestTick &&
				RequestFirstAvailable(bot, Info.OpeningHarvesterTypes, "opening harvesters"))
				nextOpeningHarvesterRequestTick = world.WorldTick + System.Math.Max(1, Info.OpeningUnitRequestCooldown);

			if (!completedStructures.Contains(OpeningDefenseGoal) && completedStructures.Contains(OpeningRadarGoal) &&
				(!Info.PrioritizeOpeningFirstTower || !FirstTowerPlanner.HasBuildCommitment) &&
				Info.OpeningDefenseUnlockTypes.Length > 0 && CountActors(Info.OpeningDefenseUnlockTypes) == 0 &&
				world.WorldTick >= nextOpeningDefenseUnlockRequestTick &&
				RequestFirstAvailable(bot, Info.OpeningDefenseUnlockTypes, "opening defense unlock"))
				nextOpeningDefenseUnlockRequestTick = world.WorldTick + System.Math.Max(1, Info.OpeningUnitRequestCooldown);

			if (!string.IsNullOrEmpty(Info.OpeningMcvType) &&
				OpeningCommittedHarvesters >= Info.OpeningHarvesterCount &&
				OpeningMcvsBuilt < Info.OpeningMcvCount &&
				!openingMcvRequestOutstanding && !HasLiveActor(Info.OpeningMcvType) &&
				!HasRequestedOrQueued(bot, Info.OpeningMcvType) &&
				world.WorldTick >= nextOpeningMcvRequestTick &&
				Request(bot, Info.OpeningMcvType, "opening expansion MCV"))
			{
				openingMcvRequestOutstanding = true;
				openingMcvRequestExpiryTick = world.WorldTick + System.Math.Max(1, Info.OpeningMcvRequestTimeout);
				nextOpeningMcvRequestTick = world.WorldTick + System.Math.Max(1, Info.OpeningUnitRequestCooldown);
			}

			if (OpeningComplete && !openingCompletionLogged)
			{
				openingCompletionLogged = true;
				LogOpening("{0} completed opening policy", player);
			}
		}

		void UpdateOpeningMcvRequestState(IBot bot)
		{
			if (!openingMcvRequestOutstanding)
				return;

			if (OpeningMcvsBuilt >= Info.OpeningMcvCount)
			{
				openingMcvRequestOutstanding = false;
				return;
			}

			if (world.WorldTick < openingMcvRequestExpiryTick || HasLiveActor(Info.OpeningMcvType) ||
				HasQueued(Info.OpeningMcvType))
				return;

			CancelRequests(bot, Info.OpeningMcvType);
			openingMcvRequestOutstanding = false;
			LogOpening("{0} opening MCV request expired without a live, queued, or completed MCV; allowing retry", player);
		}

		void CompleteUnavailableOptionalOpeningStructureGoals(HashSet<int> completedGoals)
		{
			var goals = OpeningStructureGoals;
			var buildableTypes = Info.BuildingQueues.Concat(Info.DefenseQueues)
				.SelectMany(category => AIUtils.FindQueues(player, category))
				.SelectMany(queue => queue.BuildableItems()).Select(item => item.Name).ToArray();

			for (var i = 0; i < goals.Count; i++)
			{
				// A defense unlock that is buildable under the current technology means the
				// tower is only temporarily unavailable and must not be skipped.
				if (i == OpeningDefenseGoal && Info.OpeningDefenseUnlockTypes.Any(IsCurrentlyBuildable))
					continue;

				if (!OpeningPolicyLogic.CanSkipUnavailableGoal(i, goals, completedGoals,
					Info.OpeningOptionalStructureTypes, buildableTypes))
					continue;

				completedGoals.Add(i);
				skippedOpeningGoals.Add(i);
				if (loggedCompletedOpeningGoals.Add(i))
					LogOpening("{0} skipped unavailable optional structure goal {1}", player, OpeningGoalName(i));
			}
		}

		bool OpeningComplete => CompletedOpeningStructureGoals(OpeningStructureGoals).Count == OpeningStructureGoals.Count &&
			(Info.OpeningSoldierTypes.Length == 0 || OpeningSoldiersBuilt >= Info.OpeningSoldierCount) &&
			(Info.OpeningHarvesterTypes.Length == 0 || OpeningCommittedHarvesters >= Info.OpeningHarvesterCount) &&
			(string.IsNullOrEmpty(Info.OpeningMcvType) ||
				OpeningMcvsBuilt >= Info.OpeningMcvCount);

		internal int CountActors(IEnumerable<string> types)
		{
			var names = types as ICollection<string> ?? types.ToArray();
			return world.Actors.Count(a => a.Owner == player && !a.IsDead && names.Contains(a.Info.Name));
		}

		int OpeningCommittedHarvesters => CountActors(Info.OpeningHarvesterTypes) +
			CountQueuedOrPendingActors(Info.OpeningHarvesterTypes);

		internal int CountQueuedOrPendingActors(IEnumerable<string> types)
		{
			var names = types as ICollection<string> ?? types.ToArray();
			var queued = world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Actor.IsInWorld)
				.Sum(q => q.Trait.AllQueued().Count(i => names.Contains(i.Item)));
			var pending = world.ActorsWithTrait<IPendingProductionActors>()
				.Where(p => p.Actor.Owner == player && !p.Actor.IsDead && p.Actor.IsInWorld)
				.Sum(p => p.Trait.PendingActorTypes.Count(names.Contains));
			return queued + pending;
		}

		int OpeningSoldiersBuilt => System.Math.Max(0,
			TotalBuilt(Info.OpeningSoldierTypes) - openingSoldierBuiltBaseline);

		int OpeningMcvsBuilt => string.IsNullOrEmpty(Info.OpeningMcvType) ? 0 : System.Math.Max(0,
			TotalBuilt(new[] { Info.OpeningMcvType }) - openingMcvBuiltBaseline);

		int TotalBuilt(IEnumerable<string> types)
		{
			return types.Sum(type => playerStatistics.AdaptiveStats[type].BuiltCount);
		}

		bool HasLiveActor(string type)
		{
			return !string.IsNullOrEmpty(type) && world.Actors.Any(a => a.Owner == player && !a.IsDead && a.Info.Name == type);
		}

		bool HasRequestedOrQueued(IBot bot, string type)
		{
			return HasRequested(bot, type) || HasQueued(type);
		}

		bool HasRequested(IBot bot, string type)
		{
			return unitProduction.Any(r => r.IsTraitEnabled() && r.RequestedProductionCount(bot, type) > 0);
		}

		bool HasQueued(string type)
		{
			return world.ActorsWithTrait<ProductionQueue>().Any(q => q.Actor.Owner == player && !q.Actor.IsDead &&
				q.Actor.IsInWorld && q.Trait.AllQueued().Any(item => item.Item == type));
		}

		void CancelRequests(IBot bot, string type)
		{
			foreach (var requester in unitProduction.Where(r => r.IsTraitEnabled() &&
				r.RequestedProductionCount(bot, type) > 0))
				requester.CancelRequestedUnitProduction(bot, type);
		}

		internal bool RequestFirstAvailable(IBot bot, IEnumerable<string> types, string reason, bool requireIdleQueue = true)
		{
			var alternatives = types.ToArray();
			if (alternatives.Any(type => unitProduction.Any(r => r.IsTraitEnabled() &&
				r.RequestedProductionCount(bot, type) > 0)))
				return false;

			foreach (var type in alternatives)
				if (world.Map.Rules.Actors.ContainsKey(type) && Request(bot, type, reason, requireIdleQueue))
					return true;

			return false;
		}

		bool Request(IBot bot, string type, string reason, bool requireIdleQueue = true)
		{
			if (string.IsNullOrEmpty(type) || (requireIdleQueue ? !CanCurrentlyProduce(type) : !IsCurrentlyBuildable(type)))
				return false;

			var requester = unitProduction.FirstOrDefault(r => r.IsTraitEnabled() &&
				r.RequestedProductionCount(bot, type) == 0);
			if (requester == null)
				return false;

			requester.RequestUnitProduction(bot, type);
			LogOpening("{0} requested {1}: {2}", player, type, reason);
			return true;
		}

		bool CanCurrentlyProduce(string type)
		{
			return IsCurrentlyBuildable(type) && world.Map.Rules.Actors[type].TraitInfo<BuildableInfo>().Queue
				.Any(category => AIUtils.FindQueues(player, category)
				.Any(queue => !queue.AllQueued().Any() && queue.BuildableItems().Any(item => item.Name == type)));
		}

		internal bool IsCurrentlyBuildable(string type)
		{
			if (!world.Map.Rules.Actors.TryGetValue(type, out var actor))
				return false;

			var buildable = actor.TraitInfoOrDefault<BuildableInfo>();
			return buildable != null && buildable.Queue.Any(category => AIUtils.FindQueues(player, category)
				.Any(queue => queue.BuildableItems().Any(item => item.Name == type)));
		}

		void LogOpening(string format, params object[] args)
		{
			AIUtils.BotDebug(format, args);
			if (Info.OpeningDebugLogging)
				Log.Write("debug", "AI opening: " + format, args);
		}

		void IBotRespondToAttack.RespondToAttack(IBot bot, Actor self, AttackInfo e)
		{
			if (e.Attacker == null || e.Attacker.Disposed)
				return;

			if (e.Attacker.Owner.RelationshipWith(self.Owner) != PlayerRelationship.Enemy)
				return;

			if (!e.Attacker.Info.HasTraitInfo<ITargetableInfo>())
				return;

			DefenseClusterManager?.ObserveAttack(self, e);

			// Protect buildings
			if (self.Info.HasTraitInfo<BuildingInfo>())
				foreach (var n in positionsUpdatedModules)
					n.UpdatedDefenseCenter(e.Attacker.Location);
		}

		void SetRallyPointsForNewProductionBuildings(IBot bot)
		{
			foreach (var rp in world.ActorsWithTrait<RallyPoint>())
			{
				if (rp.Actor.Owner != player)
					continue;

				if (rallyPointManagers.Any(manager => manager.ManagesRallyPoint(rp.Actor)))
					continue;

				if (rp.Trait.Path.Count == 0 || !IsRallyPointValid(rp.Trait.Path[0], rp.Actor.Info.TraitInfoOrDefault<BuildingInfo>()))
				{
					bot.QueueOrder(new Order("SetRallyPoint", rp.Actor, Target.FromCell(world, ChooseRallyLocationNear(rp.Actor)), false)
					{
						SuppressVisualFeedback = true
					});
				}
			}
		}

		// Won't work for shipyards...
		CPos ChooseRallyLocationNear(Actor producer)
		{
			var possibleRallyPoints = world.Map.FindTilesInCircle(producer.Location, Info.RallyPointScanRadius)
				.Where(c => IsRallyPointValid(c, producer.Info.TraitInfoOrDefault<BuildingInfo>()));

			if (!possibleRallyPoints.Any())
			{
				AIUtils.BotDebug("{0} has no possible rallypoint near {1}", producer.Owner, producer.Location);
				return producer.Location;
			}

			return possibleRallyPoints.Random(world.LocalRandom);
		}

		bool IsRallyPointValid(CPos x, BuildingInfo info)
		{
			return info != null && world.IsCellBuildable(x, null, info);
		}

		// Require at least one refinery, unless we can't build it.
		public bool HasAdequateRefineryCount
		{
			get
			{
				// Smart-economy profiles must use the actual unloading refinery set so an
				// authored Resonator alias cannot satisfy core recovery. Preserve the original
				// common-name behavior for every profile that does not enable smart economy.
				var refineryTypes = Info.EnableSmartEconomy ? SmartEconomyRefineryTypes : Info.RefineryTypes;
				return !refineryTypes.Any() ||
					AIUtils.CountBuildingByCommonName(refineryTypes, player) > 0 ||
					AIUtils.CountBuildingByCommonName(Info.PowerTypes, player) == 0 ||
					AIUtils.CountBuildingByCommonName(Info.ConstructionYardTypes, player) == 0;
			}
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			var data = new List<MiniYamlNode>()
			{
				new MiniYamlNode("InitialBaseCenter", FieldSaver.FormatValue(initialBaseCenter)),
				new MiniYamlNode("DefenseCenter", FieldSaver.FormatValue(defenseCenter)),
				new MiniYamlNode("OpeningInitialized", FieldSaver.FormatValue(openingInitialized)),
				new MiniYamlNode("OpeningSoldierBuiltBaseline", FieldSaver.FormatValue(openingSoldierBuiltBaseline)),
				new MiniYamlNode("OpeningMcvBuiltBaseline", FieldSaver.FormatValue(openingMcvBuiltBaseline)),
				new MiniYamlNode("CompletedOpeningGoals", FieldSaver.FormatValue(loggedCompletedOpeningGoals.ToArray())),
				new MiniYamlNode("SkippedOpeningGoals", FieldSaver.FormatValue(skippedOpeningGoals.ToArray())),
				new MiniYamlNode("OpeningCompletionLogged", FieldSaver.FormatValue(openingCompletionLogged)),
				new MiniYamlNode("RadarRecoveryEverEstablished", FieldSaver.FormatValue(radarRecovery?.EverEstablished ?? false)),
				new MiniYamlNode("RadarRecoveryReservationQueueActorId", FieldSaver.FormatValue(radarRecovery?.ReservationQueueActorId ?? 0)),
				new MiniYamlNode("RadarRecoveryReservationQueueType", FieldSaver.FormatValue(radarRecovery?.ReservationQueueType ?? "")),
				new MiniYamlNode("RadarRecoveryReservationActorType", FieldSaver.FormatValue(radarRecovery?.ReservationActorType ?? "")),
				new MiniYamlNode("RadarRecoveryReservationTick", FieldSaver.FormatValue(radarRecovery?.ReservationTick ?? 0)),
				new MiniYamlNode("RadarRecoveryReservationCommitmentObserved", FieldSaver.FormatValue(radarRecovery?.ReservationCommitmentObserved ?? false)),
				new MiniYamlNode("RadarStorageReservationQueueActorId", FieldSaver.FormatValue(radarStoragePressure?.ReservationQueueActorId ?? 0)),
				new MiniYamlNode("RadarStorageReservationQueueType", FieldSaver.FormatValue(radarStoragePressure?.ReservationQueueType ?? "")),
				new MiniYamlNode("RadarStorageReservationActorType", FieldSaver.FormatValue(radarStoragePressure?.ReservationActorType ?? "")),
				new MiniYamlNode("RadarStorageReservationTick", FieldSaver.FormatValue(radarStoragePressure?.ReservationTick ?? 0)),
				new MiniYamlNode("RadarStorageReservationTargetCount", FieldSaver.FormatValue(radarStoragePressure?.TargetCount ?? 0)),
				new MiniYamlNode("RadarStorageOrderIssuedTick", FieldSaver.FormatValue(radarStoragePressure?.OrderIssuedTick ?? -1)),
				new MiniYamlNode("NextOpeningSoldierRequestTick", FieldSaver.FormatValue(nextOpeningSoldierRequestTick)),
				new MiniYamlNode("NextOpeningHarvesterRequestTick", FieldSaver.FormatValue(nextOpeningHarvesterRequestTick)),
				new MiniYamlNode("NextOpeningDefenseUnlockRequestTick", FieldSaver.FormatValue(nextOpeningDefenseUnlockRequestTick)),
				new MiniYamlNode("NextOpeningMcvRequestTick", FieldSaver.FormatValue(nextOpeningMcvRequestTick)),
				new MiniYamlNode("OpeningMcvRequestOutstanding", FieldSaver.FormatValue(openingMcvRequestOutstanding)),
				new MiniYamlNode("OpeningMcvRequestExpiryTick", FieldSaver.FormatValue(openingMcvRequestExpiryTick)),
				new MiniYamlNode("NextSmartEconomyScanTick", FieldSaver.FormatValue(smartEconomy?.NextScanTick ?? 0)),
				new MiniYamlNode("NextSmartEconomyMcvRequestTick", FieldSaver.FormatValue(smartEconomy?.NextMcvRequestTick ?? 0)),
				new MiniYamlNode("NextSmartEconomyProgressLogTick", FieldSaver.FormatValue(smartEconomy?.NextProgressLogTick ?? 0)),
				new MiniYamlNode("SmartEconomyRefineryBuildOutstanding", FieldSaver.FormatValue(smartEconomy?.RefineryBuildOutstanding ?? false)),
				new MiniYamlNode("SmartEconomyRefineryBuildExpiryTick", FieldSaver.FormatValue(smartEconomy?.RefineryBuildExpiryTick ?? 0)),
				new MiniYamlNode("SmartEconomyRefineryBuildTargetCount", FieldSaver.FormatValue(smartEconomy?.RefineryBuildTargetCount ?? 0)),
				new MiniYamlNode("SmartEconomyRefineryReservationQueueIds", FieldSaver.FormatValue(smartEconomy?.RefineryReservationQueueIds ?? System.Array.Empty<uint>())),
				new MiniYamlNode("SmartEconomyRefineryReservationTypes", FieldSaver.FormatValue(smartEconomy?.RefineryReservationTypes ?? System.Array.Empty<string>())),
				new MiniYamlNode("SmartEconomyRefineryReservationExpiryTicks", FieldSaver.FormatValue(smartEconomy?.RefineryReservationExpiryTicks ?? System.Array.Empty<int>())),
				new MiniYamlNode("SmartEconomyRefineryReservationTargetCounts", FieldSaver.FormatValue(smartEconomy?.RefineryReservationTargetCounts ?? System.Array.Empty<int>())),
				new MiniYamlNode("SmartEconomyRefineryReservationCosts", FieldSaver.FormatValue(smartEconomy?.RefineryReservationCosts ?? System.Array.Empty<int>())),
				new MiniYamlNode("SmartEconomyVehicleFactoryReservationQueueIds", FieldSaver.FormatValue(smartEconomy?.VehicleFactoryReservationQueueIds ?? System.Array.Empty<uint>())),
				new MiniYamlNode("SmartEconomyVehicleFactoryReservationTypes", FieldSaver.FormatValue(smartEconomy?.VehicleFactoryReservationTypes ?? System.Array.Empty<string>())),
				new MiniYamlNode("SmartEconomyVehicleFactoryReservationExpiryTicks", FieldSaver.FormatValue(smartEconomy?.VehicleFactoryReservationExpiryTicks ?? System.Array.Empty<int>())),
				new MiniYamlNode("SmartEconomyVehicleFactoryReservationTargetCounts", FieldSaver.FormatValue(smartEconomy?.VehicleFactoryReservationTargetCounts ?? System.Array.Empty<int>())),
				new MiniYamlNode("SmartEconomyMcvRequestOutstanding", FieldSaver.FormatValue(smartEconomy?.McvRequestOutstanding ?? false)),
				new MiniYamlNode("SmartEconomyMcvRequestExpiryTick", FieldSaver.FormatValue(smartEconomy?.McvRequestExpiryTick ?? 0)),
				new MiniYamlNode("SmartEconomyMcvRequestTargetAssets", FieldSaver.FormatValue(smartEconomy?.McvRequestTargetAssets ?? 0)),
				new MiniYamlNode("SmartEconomyRefineryEvidenceTicks", FieldSaver.FormatValue(smartEconomy?.RefineryPressure.EvidenceTicks ?? 0)),
				new MiniYamlNode("SmartEconomyRefineryPressureActive", FieldSaver.FormatValue(smartEconomy?.RefineryPressure.Active ?? false)),
				new MiniYamlNode("SmartEconomyCashEvidenceTicks", FieldSaver.FormatValue(smartEconomy?.CashPressure.EvidenceTicks ?? 0)),
				new MiniYamlNode("SmartEconomyCashPressureActive", FieldSaver.FormatValue(smartEconomy?.CashPressure.Active ?? false)),
				new MiniYamlNode("EconomyDefenseSamReservationActive", FieldSaver.FormatValue(economyDefenseSam?.HasBuildReservation ?? false)),
				new MiniYamlNode("EconomyDefenseSamReservationQueueActorId", FieldSaver.FormatValue(economyDefenseSam?.ReservationQueueActorId ?? 0)),
				new MiniYamlNode("EconomyDefenseSamReservationQueueType", FieldSaver.FormatValue(economyDefenseSam?.ReservationQueueType ?? "")),
				new MiniYamlNode("EconomyDefenseSamReservationActorType", FieldSaver.FormatValue(economyDefenseSam?.ReservationActorType ?? "")),
				new MiniYamlNode("EconomyDefenseSamReservationTick", FieldSaver.FormatValue(economyDefenseSam?.ReservationTick ?? 0)),
				new MiniYamlNode("FirstTowerPlacementComplete", FieldSaver.FormatValue(FirstTowerPlanner.Complete))
			};
			var enclosureState = WallPlanner?.IssueTraitData();
			if (enclosureState != null)
				data.Add(enclosureState);
			var fieldState = TiberiumFieldManager?.IssueTraitData();
			if (fieldState != null)
				data.Add(fieldState);
			var clusterState = DefenseClusterManager?.IssueTraitData();
			if (clusterState != null)
				data.Add(clusterState);

			return data;
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var initialBaseCenterNode = data.FirstOrDefault(n => n.Key == "InitialBaseCenter");
			if (initialBaseCenterNode != null)
				initialBaseCenter = FieldLoader.GetValue<CPos>("InitialBaseCenter", initialBaseCenterNode.Value.Value);

			var defenseCenterNode = data.FirstOrDefault(n => n.Key == "DefenseCenter");
			if (defenseCenterNode != null)
				defenseCenter = FieldLoader.GetValue<CPos>("DefenseCenter", defenseCenterNode.Value.Value);

			DefenseClusterManager?.ResolveTraitData(data);
			WallPlanner?.ResolveTraitData(data);

			var openingInitializedNode = data.FirstOrDefault(n => n.Key == "OpeningInitialized");
			if (openingInitializedNode != null)
				openingInitialized = FieldLoader.GetValue<bool>("OpeningInitialized", openingInitializedNode.Value.Value);
			var openingBaselineNode = data.FirstOrDefault(n => n.Key == "OpeningSoldierBuiltBaseline");
			if (openingBaselineNode != null)
				openingSoldierBuiltBaseline = FieldLoader.GetValue<int>("OpeningSoldierBuiltBaseline", openingBaselineNode.Value.Value);
			var openingMcvBaselineNode = data.FirstOrDefault(n => n.Key == "OpeningMcvBuiltBaseline");
			if (openingMcvBaselineNode != null)
				openingMcvBuiltBaseline = FieldLoader.GetValue<int>("OpeningMcvBuiltBaseline", openingMcvBaselineNode.Value.Value);
			var completedGoalsNode = data.FirstOrDefault(n => n.Key == "CompletedOpeningGoals");
			if (completedGoalsNode != null)
			{
				loggedCompletedOpeningGoals.Clear();
				loggedCompletedOpeningGoals.UnionWith(FieldLoader.GetValue<int[]>("CompletedOpeningGoals", completedGoalsNode.Value.Value));
			}

			var skippedGoalsNode = data.FirstOrDefault(n => n.Key == "SkippedOpeningGoals");
			if (skippedGoalsNode != null)
			{
				skippedOpeningGoals.Clear();
				skippedOpeningGoals.UnionWith(FieldLoader.GetValue<int[]>("SkippedOpeningGoals", skippedGoalsNode.Value.Value));
			}

			var completionNode = data.FirstOrDefault(n => n.Key == "OpeningCompletionLogged");
			if (completionNode != null)
				openingCompletionLogged = FieldLoader.GetValue<bool>("OpeningCompletionLogged", completionNode.Value.Value);

			var radarEstablishedNode = data.FirstOrDefault(n => n.Key == "RadarRecoveryEverEstablished");
			var radarQueueNode = data.FirstOrDefault(n => n.Key == "RadarRecoveryReservationQueueActorId");
			var radarQueueTypeNode = data.FirstOrDefault(n => n.Key == "RadarRecoveryReservationQueueType");
			var radarTypeNode = data.FirstOrDefault(n => n.Key == "RadarRecoveryReservationActorType");
			var radarTickNode = data.FirstOrDefault(n => n.Key == "RadarRecoveryReservationTick");
			var radarCommitmentObservedNode = data.FirstOrDefault(n => n.Key == "RadarRecoveryReservationCommitmentObserved");
			if (radarRecovery != null && radarEstablishedNode != null)
				radarRecovery.LoadState(
					FieldLoader.GetValue<bool>("RadarRecoveryEverEstablished", radarEstablishedNode.Value.Value),
					radarQueueNode != null ? FieldLoader.GetValue<uint>("RadarRecoveryReservationQueueActorId", radarQueueNode.Value.Value) : 0,
					radarQueueTypeNode != null ? FieldLoader.GetValue<string>("RadarRecoveryReservationQueueType", radarQueueTypeNode.Value.Value) : "",
					radarTypeNode != null ? FieldLoader.GetValue<string>("RadarRecoveryReservationActorType", radarTypeNode.Value.Value) : "",
					radarTickNode != null ? FieldLoader.GetValue<int>("RadarRecoveryReservationTick", radarTickNode.Value.Value) : 0,
					radarCommitmentObservedNode != null && FieldLoader.GetValue<bool>(
						"RadarRecoveryReservationCommitmentObserved", radarCommitmentObservedNode.Value.Value));

			var radarStorageQueueNode = data.FirstOrDefault(n => n.Key == "RadarStorageReservationQueueActorId");
			var radarStorageQueueTypeNode = data.FirstOrDefault(n => n.Key == "RadarStorageReservationQueueType");
			var radarStorageTypeNode = data.FirstOrDefault(n => n.Key == "RadarStorageReservationActorType");
			var radarStorageTickNode = data.FirstOrDefault(n => n.Key == "RadarStorageReservationTick");
			var radarStorageTargetNode = data.FirstOrDefault(n => n.Key == "RadarStorageReservationTargetCount");
			var radarStorageIssuedNode = data.FirstOrDefault(n => n.Key == "RadarStorageOrderIssuedTick");
			if (radarStoragePressure != null && radarStorageQueueNode != null)
				radarStoragePressure.LoadState(
					FieldLoader.GetValue<uint>("RadarStorageReservationQueueActorId", radarStorageQueueNode.Value.Value),
					radarStorageQueueTypeNode != null ? FieldLoader.GetValue<string>("RadarStorageReservationQueueType", radarStorageQueueTypeNode.Value.Value) : "",
					radarStorageTypeNode != null ? FieldLoader.GetValue<string>("RadarStorageReservationActorType", radarStorageTypeNode.Value.Value) : "",
					radarStorageTickNode != null ? FieldLoader.GetValue<int>("RadarStorageReservationTick", radarStorageTickNode.Value.Value) : 0,
					radarStorageTargetNode != null ? FieldLoader.GetValue<int>("RadarStorageReservationTargetCount", radarStorageTargetNode.Value.Value) : 0,
					radarStorageIssuedNode != null ? FieldLoader.GetValue<int>("RadarStorageOrderIssuedTick", radarStorageIssuedNode.Value.Value) : -1);

			var soldierRequestNode = data.FirstOrDefault(n => n.Key == "NextOpeningSoldierRequestTick");
			if (soldierRequestNode != null)
				nextOpeningSoldierRequestTick = FieldLoader.GetValue<int>("NextOpeningSoldierRequestTick", soldierRequestNode.Value.Value);
			var harvesterRequestNode = data.FirstOrDefault(n => n.Key == "NextOpeningHarvesterRequestTick");
			if (harvesterRequestNode != null)
				nextOpeningHarvesterRequestTick = FieldLoader.GetValue<int>("NextOpeningHarvesterRequestTick", harvesterRequestNode.Value.Value);
			var unlockRequestNode = data.FirstOrDefault(n => n.Key == "NextOpeningDefenseUnlockRequestTick");
			if (unlockRequestNode != null)
				nextOpeningDefenseUnlockRequestTick = FieldLoader.GetValue<int>("NextOpeningDefenseUnlockRequestTick", unlockRequestNode.Value.Value);
			var mcvRequestNode = data.FirstOrDefault(n => n.Key == "NextOpeningMcvRequestTick");
			if (mcvRequestNode != null)
				nextOpeningMcvRequestTick = FieldLoader.GetValue<int>("NextOpeningMcvRequestTick", mcvRequestNode.Value.Value);
			var mcvOutstandingNode = data.FirstOrDefault(n => n.Key == "OpeningMcvRequestOutstanding");
			if (mcvOutstandingNode != null)
				openingMcvRequestOutstanding = FieldLoader.GetValue<bool>("OpeningMcvRequestOutstanding", mcvOutstandingNode.Value.Value);
			var mcvExpiryNode = data.FirstOrDefault(n => n.Key == "OpeningMcvRequestExpiryTick");
			if (mcvExpiryNode != null)
				openingMcvRequestExpiryTick = FieldLoader.GetValue<int>("OpeningMcvRequestExpiryTick", mcvExpiryNode.Value.Value);

			var smartScanNode = data.FirstOrDefault(n => n.Key == "NextSmartEconomyScanTick");
			var smartMcvRequestNode = data.FirstOrDefault(n => n.Key == "NextSmartEconomyMcvRequestTick");
			var smartProgressLogNode = data.FirstOrDefault(n => n.Key == "NextSmartEconomyProgressLogTick");
			var smartRefineryOutstandingNode = data.FirstOrDefault(n => n.Key == "SmartEconomyRefineryBuildOutstanding");
			var smartRefineryExpiryNode = data.FirstOrDefault(n => n.Key == "SmartEconomyRefineryBuildExpiryTick");
			var smartRefineryTargetNode = data.FirstOrDefault(n => n.Key == "SmartEconomyRefineryBuildTargetCount");
			var smartRefineryQueueIdsNode = data.FirstOrDefault(n => n.Key == "SmartEconomyRefineryReservationQueueIds");
			var smartRefineryTypesNode = data.FirstOrDefault(n => n.Key == "SmartEconomyRefineryReservationTypes");
			var smartRefineryExpiryTicksNode = data.FirstOrDefault(n => n.Key == "SmartEconomyRefineryReservationExpiryTicks");
			var smartRefineryTargetCountsNode = data.FirstOrDefault(n => n.Key == "SmartEconomyRefineryReservationTargetCounts");
			var smartRefineryCostsNode = data.FirstOrDefault(n => n.Key == "SmartEconomyRefineryReservationCosts");
			var smartVehicleFactoryQueueIdsNode = data.FirstOrDefault(n => n.Key == "SmartEconomyVehicleFactoryReservationQueueIds");
			var smartVehicleFactoryTypesNode = data.FirstOrDefault(n => n.Key == "SmartEconomyVehicleFactoryReservationTypes");
			var smartVehicleFactoryExpiryTicksNode = data.FirstOrDefault(n => n.Key == "SmartEconomyVehicleFactoryReservationExpiryTicks");
			var smartVehicleFactoryTargetCountsNode = data.FirstOrDefault(n => n.Key == "SmartEconomyVehicleFactoryReservationTargetCounts");
			var smartMcvOutstandingNode = data.FirstOrDefault(n => n.Key == "SmartEconomyMcvRequestOutstanding");
			var smartMcvExpiryNode = data.FirstOrDefault(n => n.Key == "SmartEconomyMcvRequestExpiryTick");
			var smartMcvTargetNode = data.FirstOrDefault(n => n.Key == "SmartEconomyMcvRequestTargetAssets");
			var refineryEvidenceNode = data.FirstOrDefault(n => n.Key == "SmartEconomyRefineryEvidenceTicks");
			var refineryActiveNode = data.FirstOrDefault(n => n.Key == "SmartEconomyRefineryPressureActive");
			var cashEvidenceNode = data.FirstOrDefault(n => n.Key == "SmartEconomyCashEvidenceTicks");
			var cashActiveNode = data.FirstOrDefault(n => n.Key == "SmartEconomyCashPressureActive");
			if (smartEconomy != null && (smartScanNode != null || smartMcvRequestNode != null || smartProgressLogNode != null ||
				smartRefineryOutstandingNode != null || smartRefineryExpiryNode != null || smartRefineryTargetNode != null ||
				smartMcvOutstandingNode != null || smartMcvExpiryNode != null || smartMcvTargetNode != null ||
				refineryEvidenceNode != null || refineryActiveNode != null || cashEvidenceNode != null || cashActiveNode != null))
				smartEconomy.LoadState(
					smartScanNode != null ? FieldLoader.GetValue<int>("NextSmartEconomyScanTick", smartScanNode.Value.Value) : 0,
					smartMcvRequestNode != null ? FieldLoader.GetValue<int>("NextSmartEconomyMcvRequestTick", smartMcvRequestNode.Value.Value) : 0,
					smartProgressLogNode != null ? FieldLoader.GetValue<int>("NextSmartEconomyProgressLogTick", smartProgressLogNode.Value.Value) : 0,
					smartRefineryOutstandingNode != null && FieldLoader.GetValue<bool>("SmartEconomyRefineryBuildOutstanding", smartRefineryOutstandingNode.Value.Value),
					smartRefineryExpiryNode != null ? FieldLoader.GetValue<int>("SmartEconomyRefineryBuildExpiryTick", smartRefineryExpiryNode.Value.Value) : 0,
					smartRefineryTargetNode != null ? FieldLoader.GetValue<int>("SmartEconomyRefineryBuildTargetCount", smartRefineryTargetNode.Value.Value) : 0,
					smartMcvOutstandingNode != null && FieldLoader.GetValue<bool>("SmartEconomyMcvRequestOutstanding", smartMcvOutstandingNode.Value.Value),
					smartMcvExpiryNode != null ? FieldLoader.GetValue<int>("SmartEconomyMcvRequestExpiryTick", smartMcvExpiryNode.Value.Value) : 0,
					smartMcvTargetNode != null ? FieldLoader.GetValue<int>("SmartEconomyMcvRequestTargetAssets", smartMcvTargetNode.Value.Value) : 0,
					new SmartEconomyPressure(
						refineryEvidenceNode != null ? FieldLoader.GetValue<int>("SmartEconomyRefineryEvidenceTicks", refineryEvidenceNode.Value.Value) : 0,
						refineryActiveNode != null && FieldLoader.GetValue<bool>("SmartEconomyRefineryPressureActive", refineryActiveNode.Value.Value)),
					new SmartEconomyPressure(
						cashEvidenceNode != null ? FieldLoader.GetValue<int>("SmartEconomyCashEvidenceTicks", cashEvidenceNode.Value.Value) : 0,
						cashActiveNode != null && FieldLoader.GetValue<bool>("SmartEconomyCashPressureActive", cashActiveNode.Value.Value)));

			if (smartEconomy != null && smartRefineryQueueIdsNode != null && smartRefineryTypesNode != null &&
				smartRefineryExpiryTicksNode != null && smartRefineryTargetCountsNode != null && smartRefineryCostsNode != null)
				smartEconomy.LoadRefineryReservations(
					FieldLoader.GetValue<uint[]>("SmartEconomyRefineryReservationQueueIds", smartRefineryQueueIdsNode.Value.Value),
					FieldLoader.GetValue<string[]>("SmartEconomyRefineryReservationTypes", smartRefineryTypesNode.Value.Value),
					FieldLoader.GetValue<int[]>("SmartEconomyRefineryReservationExpiryTicks", smartRefineryExpiryTicksNode.Value.Value),
					FieldLoader.GetValue<int[]>("SmartEconomyRefineryReservationTargetCounts", smartRefineryTargetCountsNode.Value.Value),
					FieldLoader.GetValue<int[]>("SmartEconomyRefineryReservationCosts", smartRefineryCostsNode.Value.Value));

			if (smartEconomy != null && smartVehicleFactoryQueueIdsNode != null && smartVehicleFactoryTypesNode != null &&
				smartVehicleFactoryExpiryTicksNode != null && smartVehicleFactoryTargetCountsNode != null)
				smartEconomy.LoadVehicleFactoryReservations(
					FieldLoader.GetValue<uint[]>("SmartEconomyVehicleFactoryReservationQueueIds", smartVehicleFactoryQueueIdsNode.Value.Value),
					FieldLoader.GetValue<string[]>("SmartEconomyVehicleFactoryReservationTypes", smartVehicleFactoryTypesNode.Value.Value),
					FieldLoader.GetValue<int[]>("SmartEconomyVehicleFactoryReservationExpiryTicks", smartVehicleFactoryExpiryTicksNode.Value.Value),
					FieldLoader.GetValue<int[]>("SmartEconomyVehicleFactoryReservationTargetCounts", smartVehicleFactoryTargetCountsNode.Value.Value));

			var economySamActiveNode = data.FirstOrDefault(n => n.Key == "EconomyDefenseSamReservationActive");
			var economySamQueueActorNode = data.FirstOrDefault(n => n.Key == "EconomyDefenseSamReservationQueueActorId");
			var economySamQueueTypeNode = data.FirstOrDefault(n => n.Key == "EconomyDefenseSamReservationQueueType");
			var economySamActorTypeNode = data.FirstOrDefault(n => n.Key == "EconomyDefenseSamReservationActorType");
			var economySamTickNode = data.FirstOrDefault(n => n.Key == "EconomyDefenseSamReservationTick");
			if (economyDefenseSam != null && economySamActiveNode != null &&
				FieldLoader.GetValue<bool>("EconomyDefenseSamReservationActive", economySamActiveNode.Value.Value) &&
				economySamQueueActorNode != null && economySamQueueTypeNode != null &&
				economySamActorTypeNode != null && economySamTickNode != null)
				economyDefenseSam.RestoreBuildOwnership(
					FieldLoader.GetValue<uint>("EconomyDefenseSamReservationQueueActorId", economySamQueueActorNode.Value.Value),
					FieldLoader.GetValue<string>("EconomyDefenseSamReservationQueueType", economySamQueueTypeNode.Value.Value),
					FieldLoader.GetValue<string>("EconomyDefenseSamReservationActorType", economySamActorTypeNode.Value.Value),
					FieldLoader.GetValue<int>("EconomyDefenseSamReservationTick", economySamTickNode.Value.Value));

			var firstTowerNode = data.FirstOrDefault(n => n.Key == "FirstTowerPlacementComplete");
			if (firstTowerNode != null)
				FirstTowerPlanner.Complete = FieldLoader.GetValue<bool>("FirstTowerPlacementComplete", firstTowerNode.Value.Value);

			TiberiumFieldManager?.ResolveTraitData(data);
		}
	}
}
