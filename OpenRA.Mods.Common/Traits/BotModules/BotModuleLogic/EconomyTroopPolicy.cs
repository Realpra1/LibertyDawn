#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;

namespace OpenRA.Mods.Common.Traits
{
	public enum EconomyReadinessDecision { NotReady, Observing, Ready }

	/// <summary>Deterministic, world-independent policy shared by the Economy troop roles.</summary>
	public static class EconomyTroopPolicy
	{
		public static bool IsReady(int harvesters, int minimumHarvesters, int screen,
			int minimumScreen, int artillery, int minimumArtillery, int antiAir,
			int minimumAntiAir, int availableCash, int minimumCash, bool criticalThreat)
		{
			return harvesters >= minimumHarvesters && screen >= minimumScreen &&
				artillery >= minimumArtillery && antiAir >= minimumAntiAir &&
				availableCash >= minimumCash && !criticalThreat;
		}

		public static EconomyReadinessDecision ReadinessDecision(bool alreadyReady,
			bool entryReady, bool maintenanceReady, int currentTick, int observationStartedTick,
			int observationTicks)
		{
			if (!maintenanceReady || (!alreadyReady && !entryReady) || observationTicks <= 0)
				return EconomyReadinessDecision.NotReady;

			if (alreadyReady)
				return EconomyReadinessDecision.Ready;

			if (observationStartedTick < 0)
				return entryReady ? EconomyReadinessDecision.Observing : EconomyReadinessDecision.NotReady;

			return currentTick >= observationStartedTick + observationTicks ?
				EconomyReadinessDecision.Ready : EconomyReadinessDecision.Observing;
		}

		public static bool ShouldRequestMammoth(long mammothValue, long largestOtherTypeValue,
			long directFireVehicleValue, int targetPercent)
		{
			if (targetPercent <= 0 || targetPercent > 100)
				return false;

			return mammothValue <= largestOtherTypeValue || directFireVehicleValue <= 0 ||
				mammothValue * 100 < directFireVehicleValue * targetPercent;
		}

		public static int RaidGroupSize(int eligible, int mobileReserve, int minimum, int maximum)
		{
			if (eligible <= mobileReserve || minimum <= 0 || maximum < minimum)
				return 0;

			var available = Math.Min(maximum, eligible - mobileReserve);
			return available >= minimum ? available : 0;
		}

		public static bool HasProgress(long distanceSquared, long bestDistanceSquared,
			int targetHp, int previousTargetHp)
		{
			return distanceSquared < bestDistanceSquared || targetHp < previousTargetHp;
		}

		public static bool MissionExpired(int currentTick, int startedTick, int lastProgressTick,
			int timeout, int noProgressTimeout)
		{
			return currentTick >= startedTick + timeout || currentTick >= lastProgressTick + noProgressTimeout;
		}

		public static bool ShouldIssueCrushOrder(uint targetId, IEnumerable<uint> activeTargetIds)
		{
			foreach (var activeTargetId in activeTargetIds)
				if (activeTargetId == targetId)
					return false;

			return true;
		}

		public static bool IsSameCrushObjective(uint expectedObjectiveId, uint currentObjectiveId)
		{
			return expectedObjectiveId != 0 && expectedObjectiveId == currentObjectiveId;
		}

		public static bool IsExposedTarget(long nearbyDefenderValue, long raidValue, int maximumDefenderPercent)
		{
			return raidValue > 0 && maximumDefenderPercent >= 0 &&
				nearbyDefenderValue * 100 <= raidValue * maximumDefenderPercent;
		}

		public static WDist SelectApproachRange(IEnumerable<WDist> usableRanges, WDist fallback)
		{
			var shortest = WDist.MaxValue;
			foreach (var range in usableRanges)
				if (range > WDist.Zero && range < shortest)
					shortest = range;

			return shortest != WDist.MaxValue ? shortest : fallback;
		}

		/// <summary>Cheap squared-distance test against the current formation-to-objective segment.</summary>
		public static bool IsNearRoute(WPos point, WPos routeStart, WPos routeEnd, WDist maximumDetour)
		{
			var limit = (long)Math.Max(0, maximumDetour.Length);
			return AirThreatGeometry.DistanceSquaredToSegment(point, routeStart, routeEnd) <= limit * limit;
		}
	}
}
