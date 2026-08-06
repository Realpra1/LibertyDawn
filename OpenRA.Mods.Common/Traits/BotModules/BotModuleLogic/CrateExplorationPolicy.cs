#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * version 3 or later.
 */
#endregion

using System;
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

		public static int[] SelectNormalScoutCandidates(IReadOnlyList<string> actorTypes,
			IReadOnlyDictionary<string, int> priorities, int limit)
		{
			if (limit <= 0)
				return Array.Empty<int>();

			return Enumerable.Range(0, actorTypes.Count)
				.Where(i => priorities.ContainsKey(actorTypes[i]))
				.OrderByDescending(i => priorities[actorTypes[i]])
				.ThenBy(i => i)
				.Take(limit)
				.ToArray();
		}

		public static int[] BuildDistributedCoverageOrder(IReadOnlyList<CPos> centers)
		{
			if (centers.Count == 0)
				return Array.Empty<int>();

			var count = centers.Count;
			var sumX = centers.Sum(c => (long)c.X);
			var sumY = centers.Sum(c => (long)c.Y);
			var centerDistance = new long[count];
			long DistanceFromCenter(int i)
			{
				var dx = centers[i].X * (long)count - sumX;
				var dy = centers[i].Y * (long)count - sumY;
				return dx * dx + dy * dy;
			}

			for (var i = 0; i < count; i++)
				centerDistance[i] = DistanceFromCenter(i);

			long DistanceSquared(int a, int b)
			{
				var dx = (long)centers[a].X - centers[b].X;
				var dy = (long)centers[a].Y - centers[b].Y;
				return dx * dx + dy * dy;
			}

			var selected = new bool[count];
			var minimumDistance = Enumerable.Repeat(long.MaxValue, count).ToArray();
			var order = new int[count];
			for (var rank = 0; rank < count; rank++)
			{
				var next = -1;
				for (var i = 0; i < count; i++)
				{
					if (selected[i])
						continue;

					var spread = rank == 0 ? centerDistance[i] : minimumDistance[i];
					var nextSpread = next < 0 ? -1 : rank == 0 ? centerDistance[next] : minimumDistance[next];
					if (next < 0 || spread > nextSpread ||
						(spread == nextSpread && centerDistance[i] > centerDistance[next]) ||
						(spread == nextSpread && centerDistance[i] == centerDistance[next] && i < next))
						next = i;
				}

				order[rank] = next;
				selected[next] = true;
				for (var i = 0; i < count; i++)
					if (!selected[i])
						minimumDistance[i] = Math.Min(minimumDistance[i], DistanceSquared(i, next));
			}

			return order;
		}

		public static int[] RankRegions(IReadOnlyList<int> lastVisibleTicks, ISet<int> assignedRegions,
			IReadOnlyList<int> coverageRanks, int coverageCursor)
		{
			if (coverageRanks.Count != lastVisibleTicks.Count)
				throw new ArgumentException("Coverage ranks and visibility history must have the same length.");

			var count = lastVisibleTicks.Count;
			var cursor = count == 0 ? 0 : ((coverageCursor % count) + count) % count;
			return Enumerable.Range(0, lastVisibleTicks.Count)
				.Where(i => assignedRegions == null || !assignedRegions.Contains(i))
				.OrderBy(i => lastVisibleTicks[i] < 0 ? 0 : 1)
				.ThenBy(i => lastVisibleTicks[i])
				.ThenBy(i => (coverageRanks[i] - cursor + count) % count)
				.ThenBy(i => i)
				.ToArray();
		}
	}
}
