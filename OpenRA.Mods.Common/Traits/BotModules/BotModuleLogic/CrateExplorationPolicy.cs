#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * version 3 or later.
 */
#endregion

using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	public static class CrateExplorationPolicy
	{
		public static bool IsEmergency(int spendableCash, bool hasMcv, int cashThreshold)
		{
			return spendableCash <= cashThreshold || !hasMcv;
		}

		public static bool MadeProgress(long distanceSquared, long bestDistanceSquared)
		{
			return distanceSquared < bestDistanceSquared;
		}

		public static bool HasStalled(int currentTick, int lastProgressTick, int stallInterval)
		{
			return currentTick - lastProgressTick >= stallInterval;
		}

		public static int[] RankRegions(IReadOnlyList<int> lastVisibleTicks, ISet<int> assignedRegions)
		{
			return Enumerable.Range(0, lastVisibleTicks.Count)
				.Where(i => assignedRegions == null || !assignedRegions.Contains(i))
				.OrderBy(i => lastVisibleTicks[i] < 0 ? 0 : 1)
				.ThenBy(i => lastVisibleTicks[i])
				.ThenBy(i => i)
				.ToArray();
		}
	}
}
