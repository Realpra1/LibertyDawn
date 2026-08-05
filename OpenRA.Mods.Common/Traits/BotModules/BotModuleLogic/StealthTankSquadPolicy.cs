#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;

namespace OpenRA.Mods.Common.Traits
{
	public enum StealthTankSquadRole { Harass, Attack }

	public static class StealthTankSquadPolicy
	{
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
			int currentTargetBonusPercent, int clusterMultiplierPercent = 100)
		{
			var score = Math.Max(0, priority) * (long)Math.Max(1, economicValue) /
				Math.Max(1, distanceCells + 6);
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

		public static int BufferedRange(int rangeCells, int bufferCells)
		{
			return rangeCells > 0 ? rangeCells + Math.Max(0, bufferCells) : 0;
		}
	}
}
