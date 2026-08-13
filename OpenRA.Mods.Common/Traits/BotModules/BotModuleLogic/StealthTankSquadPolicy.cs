#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	public enum StealthTankSquadRole { Harass, Attack }
	public enum StealthTankPlanInvalidation
	{
		None,
		TargetChanged,
		MembershipChanged,
		TargetMoved,
		RouteUnsafe,
		NoProgress
	}

	public static class StealthTankSquadPolicy
	{
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

		public static StealthTankPlanInvalidation ClassifyPlanInvalidation(bool hasPlan,
			bool targetChanged, bool membershipChanged, bool targetMoved, bool routeUnsafe,
			int currentTick, int lastProgressTick, int retryInterval)
		{
			if (!hasPlan || targetChanged)
				return StealthTankPlanInvalidation.TargetChanged;
			if (membershipChanged)
				return StealthTankPlanInvalidation.MembershipChanged;
			if (targetMoved)
				return StealthTankPlanInvalidation.TargetMoved;
			if (routeUnsafe)
				return StealthTankPlanInvalidation.RouteUnsafe;
			if (currentTick >= lastProgressTick + Math.Max(1, retryInterval))
				return StealthTankPlanInvalidation.NoProgress;

			return StealthTankPlanInvalidation.None;
		}

		public static int SpecialistCount(int total, bool reserveOpeningPair = true)
		{
			if (total < 2)
				return 0;
			if (!reserveOpeningPair)
				return (total + 1) / 2;
			if (total < 4)
				return 2;

			return total / 2;
		}

		public static int GroupForIndex(int index, int specialistCount,
			int maximumHarassmentGroups = 2, bool includeAttackGroup = true)
		{
			if (index < 0 || index >= specialistCount || maximumHarassmentGroups <= 0)
				return -1;
			if (maximumHarassmentGroups == 1 && !includeAttackGroup)
				return 0;

			if (includeAttackGroup && maximumHarassmentGroups == 2 && specialistCount <= 3)
				return 0;
			if (includeAttackGroup && maximumHarassmentGroups == 2 && specialistCount == 4)
				return index < 2 ? 0 : 1;

			// Keep two tanks together for cooperative anti-tank work. The remaining tanks are
			// split between two harassment groups, which naturally grow in large late-game armies.
			var attackCount = includeAttackGroup && specialistCount >= 5 ? 2 : 0;
			var harassmentCount = specialistCount - attackCount;
			if (index >= harassmentCount)
				return maximumHarassmentGroups;

			return Math.Min(maximumHarassmentGroups - 1,
				index * maximumHarassmentGroups / Math.Max(1, harassmentCount));
		}

		public static StealthTankSquadRole RoleForGroup(int group,
			int maximumHarassmentGroups = 2, bool includeAttackGroup = true)
		{
			return includeAttackGroup && group == maximumHarassmentGroups ?
				StealthTankSquadRole.Attack : StealthTankSquadRole.Harass;
		}

		public static long TargetScore(int priority, int economicValue, int distanceCells,
			int currentTargetBonusPercent, int clusterMultiplierPercent = 100, int distancePenalty = 1)
		{
			var score = Math.Max(0, priority) * (long)Math.Max(1, economicValue) /
				Math.Max(1, Math.Max(0, distanceCells) * Math.Max(1, distancePenalty) + 6);
			return score * Math.Max(100, currentTargetBonusPercent) / 100 *
				Math.Max(100, clusterMultiplierPercent) / 100;
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

		public static int SelectDefenderClearOpportunity(int[] defendingValues, long[] unlockedScores, int weakestCount)
		{
			if (defendingValues == null || unlockedScores == null || weakestCount <= 0 ||
				defendingValues.Length == 0 || defendingValues.Length != unlockedScores.Length)
				return -1;

			return Enumerable.Range(0, defendingValues.Length)
				.OrderBy(i => Math.Max(0, defendingValues[i]))
				.ThenBy(i => i)
				.Take(weakestCount)
				.OrderByDescending(i => unlockedScores[i])
				.ThenBy(i => i)
				.First();
		}

		public static int BufferedRange(int rangeCells, int bufferCells)
		{
			return rangeCells > 0 ? rangeCells + Math.Max(0, bufferCells) : 0;
		}

		public static int TransitThreatRange(int detectorRangeCells, int weaponRangeCells,
			bool weaponIsEngaged, bool canKiteTarget)
		{
			var weaponRange = weaponIsEngaged && !canKiteTarget ? weaponRangeCells : 0;
			return Math.Max(detectorRangeCells, weaponRange);
		}
	}
}
