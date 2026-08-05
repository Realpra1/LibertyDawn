#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * version 3 or later.
 */
#endregion

using System;

namespace OpenRA.Mods.Common.Traits
{
	public static class RedTiberiumBombPolicy
	{
		public static long LaunchCost(int gameTicksPerMinute)
		{
			return 100L * Math.Max(1, gameTicksPerMinute);
		}

		public static long AccrueLaunchBudget(long currentBudget, int harvesterCount, int elapsedTicks,
			int launchPercentPerMinute, int gameTicksPerMinute, int maximumStoredLaunches)
		{
			var cost = LaunchCost(gameTicksPerMinute);
			var cap = cost * Math.Max(1, maximumStoredLaunches);
			var earned = Math.Max(0, harvesterCount) * (long)Math.Max(0, launchPercentPerMinute) *
				Math.Max(0, elapsedTicks);
			return Math.Min(cap, Math.Max(0, currentBudget) + earned);
		}

		public static bool CanLaunch(long budget, int gameTicksPerMinute)
		{
			return budget >= LaunchCost(gameTicksPerMinute);
		}

		public static long SpendLaunch(long budget, int gameTicksPerMinute)
		{
			return Math.Max(0, budget - LaunchCost(gameTicksPerMinute));
		}

		public static long TargetScore(int configuredPriority, int economicValue)
		{
			return Math.Max(0, configuredPriority) * (long)Math.Max(1, economicValue);
		}

		public static bool MadeProgress(long distanceSquared, long bestDistanceSquared)
		{
			return distanceSquared < bestDistanceSquared;
		}

		public static bool HasStalled(int currentTick, int lastProgressTick, int stallInterval)
		{
			return currentTick - lastProgressTick >= stallInterval;
		}
	}
}
