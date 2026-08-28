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

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	// Mutable planning data is kept separate from the state-machine behavior that consumes it.
	sealed class AirInfluenceCache
	{
		public int Tick;
		public int Width;
		public int Height;
		public float[] Danger;
		public List<(Actor Actor, int Utility, float ConfiguredWeight)> Candidates;
		public List<(Actor Actor, float StoppingWeight, int RangeCells)> Threats;
	}

	sealed class AirRepairPlan
	{
		public Actor Building;
		public Actor FallbackBuilding;
		public List<CPos> Route;
		public bool RepairAtEnd;
		public int CandidateCount;
		public int RejectedByAa;
	}

	sealed class AirRepairHoldingPlan
	{
		public Actor Shelter;
		public CPos Destination;
		public List<CPos> Route;
	}

	sealed class AirTargetPlan
	{
		public readonly Actor Actor;
		public readonly int Score;
		public readonly bool IsUndefended;
		public readonly List<CPos> Route;
		public readonly bool ClearsAa;
		public readonly List<Squad> SupportSquads;
		public readonly CPos? AaProtectedCell;
		public readonly IReadOnlyCollection<uint> AaThreatIds;
		public readonly StealthClearMode StealthMode;
		public readonly IReadOnlyCollection<uint> StealthPackage;
		public readonly CPos? StealthClearCenterCell;
		public readonly bool StealthAggressiveMass;
		public readonly CPos? StealthPostAttackCell;
		public long ServiceMilliseconds = long.MaxValue;

		public AirTargetPlan(Actor actor, int score, bool isUndefended, List<CPos> route,
			bool clearsAa = false, List<Squad> supportSquads = null, CPos? aaProtectedCell = null,
			IReadOnlyCollection<uint> aaThreatIds = null, StealthClearMode stealthMode = StealthClearMode.None,
			IReadOnlyCollection<uint> stealthPackage = null, CPos? stealthClearCenterCell = null,
			bool stealthAggressiveMass = false, CPos? stealthPostAttackCell = null)
		{
			Actor = actor;
			Score = score;
			IsUndefended = isUndefended;
			Route = route;
			ClearsAa = clearsAa;
			SupportSquads = supportSquads;
			AaProtectedCell = aaProtectedCell;
			AaThreatIds = aaThreatIds;
			StealthMode = stealthMode;
			StealthPackage = stealthPackage;
			StealthClearCenterCell = stealthClearCenterCell;
			StealthAggressiveMass = stealthAggressiveMass;
			StealthPostAttackCell = stealthPostAttackCell;
		}
	}

	sealed class GroundThreat
	{
		public Actor Actor;
		public int WeaponRange;
		public int DetectorRange;
		public int Speed;
	}

	sealed class StealthInfluenceCache
	{
		public int Tick;
		public int Width;
		public int Height;
		public float[] Danger;
		public float[] CloakedDanger;
		public float[] MobilityDanger;
		public List<(Actor Actor, int Priority)> Candidates;
		public List<GroundThreat> Threats;
		public Dictionary<Actor, GroundThreat> ThreatByActor;
		public Dictionary<CPos, List<Actor>> EnemyActorsByCell;
		public Dictionary<CPos, List<GroundThreat>> ThreatCoverageByCell;
		public HashSet<CPos> PendingExplosionCells;
	}

	sealed class DefendedCellPlan
	{
		public StealthAIDefendedAirAction Action;
		public Actor ClearTarget;
		public List<Squad> SupportSquads;
		public List<uint> AaThreatIds;
		public double DangerValue;
		public long UnlockedValue;
		public long CellKillTicks;
		public long ProtectedKillTicks;
		public long AaClearTicks;
		public long ClearAircraftValue;
		public float ClearReferenceWeight;
	}
}
