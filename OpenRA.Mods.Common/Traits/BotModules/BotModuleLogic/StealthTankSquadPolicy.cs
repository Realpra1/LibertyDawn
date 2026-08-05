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
		public static int SpecialistCount(int total)
		{
			if (total < 2)
				return 0;
			if (total < 4)
				return 2;

			return total / 2;
		}

		public static int GroupForIndex(int index, int specialistCount)
		{
			if (index < 0 || index >= specialistCount)
				return -1;

			if (specialistCount <= 3)
				return 0;
			if (specialistCount == 4)
				return index < 2 ? 0 : 1;

			// Keep two tanks together for cooperative anti-tank work. The remaining tanks are
			// split between two harassment groups, which naturally grow in large late-game armies.
			var harassmentCount = specialistCount - 2;
			var firstHarassment = (harassmentCount + 1) / 2;
			if (index < firstHarassment)
				return 0;
			if (index < harassmentCount)
				return 1;

			return 2;
		}

		public static StealthTankSquadRole RoleForGroup(int group)
		{
			return group == 2 ? StealthTankSquadRole.Attack : StealthTankSquadRole.Harass;
		}

		public static long TargetScore(int priority, int economicValue, int distanceCells, int currentTargetBonusPercent)
		{
			var score = Math.Max(0, priority) * (long)Math.Max(1, economicValue) /
				Math.Max(1, distanceCells + 6);
			return score * Math.Max(100, currentTargetBonusPercent) / 100;
		}

		public static bool CanCarefullyClear(int squadValue, int defendingValue, int requiredValueRatio)
		{
			return squadValue > 0 && defendingValue > 0 && requiredValueRatio > 0 &&
				squadValue >= (long)defendingValue * requiredValueRatio;
		}
	}
}
