// Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
// This file is licensed under the GNU General Public License version 3 or later.

using System.Collections.Generic;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public sealed class StealthSquadDefinition
	{
		public readonly HashSet<string> UnitTypes = new HashSet<string>();
		public readonly string SquadLabel = "stealth-tank";
		public readonly Dictionary<string, int> HarassmentTargetPriorities = new Dictionary<string, int>();
		public readonly Dictionary<string, int> LateHarassmentTargetPriorities = new Dictionary<string, int>();
		public readonly Dictionary<string, int> AttackTargetPriorities = new Dictionary<string, int>();
		public readonly Dictionary<string, int> HarassmentArmorPriorities = new Dictionary<string, int>();
		public readonly BitSet<TargetableType> ExcludedHarassmentTargetTypes = default(BitSet<TargetableType>);
		public readonly BitSet<TargetableType> IgnoredHarassmentWeaponThreatTypes = default(BitSet<TargetableType>);
		public readonly int ScanInterval = 75;
		public readonly int OrderInterval = 75;
		public readonly int MaximumTargetCandidates = 48;
		public readonly int MaximumHarassmentGroups = 2;
		public readonly bool IncludeAttackGroup = true;
		public readonly bool ReserveOpeningPair = true;
		public readonly bool ClaimAllEligible = false;
		public readonly int ThreatRangeBufferCells = 0;
		public readonly int DetectorRangeBufferCells = 2;
		public readonly int RouteThreatPenalty = 4;
		public readonly int MaximumRouteStretchPercent = 150;
		public readonly int KiteRangeMarginCells = 1;
		public readonly int CarefulClearValueRatio = 5;
		public readonly int MinimumLateHarassmentGroupSize = 3;
		public readonly int TargetSwitchImprovementPercent = 25;
		public readonly int HarassmentDistancePenalty = 1;
		[Desc("Number of reachable target-bearing strategic cells collected by the STNK outward frontier.")]
		public readonly int OutwardTargetCellLimit = 10;
		[Desc("Preferred maximum driving time in seconds for an undefended target. Farther safe targets remain a fallback.")]
		public readonly int MaximumUndefendedTargetTravelSeconds = 20;
		public readonly bool EnableKiting = true;
		[Desc("Minimum configured priority-times-value for an economic objective to be considered by Kite.")]
		public readonly long MinimumKitePriorityValue = 250000;
		public readonly int MinimumKiteSpeedPercent = 120;
		public readonly int MassClearEntryCrossoverPercent = 200;
		public readonly int MassClearAbortCrossoverPercent = 100;
		public readonly HashSet<string> HarvesterTypes = new HashSet<string>();
		public readonly HashSet<string> HarvesterWaitingAnchorTypes = new HashSet<string>();
		public readonly int ResourceWaitingSearchRadius;
		public readonly int ResourceWaitingOrderInterval = 750;
		public readonly HashSet<string> AvoidResourceTypes = new HashSet<string>();
		public readonly int PendingResourceExplosionAvoidanceRadius;
		public readonly int HazardRouteWaypointSpacing = 4;
		public readonly int StrategicCellSize = StealthAISpecialistPolicy.RequiredStrategicCellSize;
		public readonly int MissionRetryInterval;
		public readonly int NearbyTargetReactionRadiusCells;
		public readonly int DefenderClearFallbackScans = 20;
		public readonly int DefenderClearValueRatio = 1;
		public readonly int DefenderClearWeakestCandidates = 3;
		public readonly int InfantryTargetPriority = 1200;
		public readonly int WallTargetPriority = 1;
		public readonly int StructureTargetPriority = 500;
		public readonly int TankTargetPriority = 1500;
		public readonly int InfantryClusterRadiusCells;
		public readonly int InfantryClusterBonusPercentPerNearbyActor;
		public readonly int MaximumInfantryClusterMultiplierPercent = 100;
		public readonly bool CrushInfantryTargets = true;
		public readonly bool DebugLogging;

		public StealthSquadDefinition(MiniYaml yaml) { FieldLoader.Load(this, yaml); }

		public void Validate(Ruleset rules)
		{
			if (UnitTypes.Count == 0 || string.IsNullOrWhiteSpace(SquadLabel) || ScanInterval <= 0 ||
				OrderInterval <= 0 || MaximumTargetCandidates <= 0 || MaximumHarassmentGroups <= 0 ||
				MaximumHarassmentGroups + (IncludeAttackGroup ? 1 : 0) >
					StealthAISpecialistPolicy.MaximumSquadCount || ThreatRangeBufferCells < 0 ||
				DetectorRangeBufferCells < 0 || RouteThreatPenalty < 0 || MaximumRouteStretchPercent < 100 ||
				KiteRangeMarginCells < 0 || CarefulClearValueRatio <= 0 || MinimumLateHarassmentGroupSize <= 0 ||
				TargetSwitchImprovementPercent < 0 || HarassmentDistancePenalty <= 0 ||
				OutwardTargetCellLimit < 5 || OutwardTargetCellLimit > 10 ||
				MaximumUndefendedTargetTravelSeconds <= 0 || MinimumKitePriorityValue < 0 ||
				MinimumKiteSpeedPercent < 100 ||
				MassClearEntryCrossoverPercent <= MassClearAbortCrossoverPercent ||
				MassClearAbortCrossoverPercent < 0 ||
				ResourceWaitingSearchRadius < 0 || ResourceWaitingOrderInterval <= 0 ||
				PendingResourceExplosionAvoidanceRadius < 0 || HazardRouteWaypointSpacing <= 0 ||
				StrategicCellSize < 0 || MissionRetryInterval < 0 || NearbyTargetReactionRadiusCells < 0 ||
				DefenderClearFallbackScans < 0 || DefenderClearValueRatio <= 0 ||
				DefenderClearWeakestCandidates <= 0 || InfantryTargetPriority < 0 || WallTargetPriority < 0 ||
				StructureTargetPriority < 0 || TankTargetPriority < 0 || InfantryClusterRadiusCells < 0 ||
				InfantryClusterBonusPercentPerNearbyActor < 0 || MaximumInfantryClusterMultiplierPercent < 100)
				throw new YamlException("Stealth squad definitions require positive and valid bounds, priorities, buffers, and ratios.");

			foreach (var actorName in UnitTypes)
				if (!rules.Actors.TryGetValue(actorName, out var actor) || !actor.HasTraitInfo<AttackBaseInfo>())
					throw new YamlException($"Stealth squad actor '{actorName}' must exist and have an attack trait.");
		}
	}
}
