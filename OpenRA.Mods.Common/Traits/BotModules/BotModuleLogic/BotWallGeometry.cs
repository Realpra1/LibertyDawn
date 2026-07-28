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

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// The world-independent half of the bot wall planner: ring layout and the reachability check
	/// that stops the bot from walling itself in. Kept free of World so it can be unit tested.
	/// Everything here is deterministic - no randomness, no iteration over unordered collections.
	/// </summary>
	public static class BotWallGeometry
	{
		/// <summary>Outward normals of the four sides of a ring, in the order N, E, S, W.</summary>
		public static readonly CVec[] SideNormals =
		{
			new CVec(0, -1),
			new CVec(1, 0),
			new CVec(0, 1),
			new CVec(-1, 0)
		};

		static readonly CVec[] Neighbours =
		{
			new CVec(1, 0),
			new CVec(-1, 0),
			new CVec(0, 1),
			new CVec(0, -1)
		};

		public struct FloodResult
		{
			public int Cells;
			public bool ReachedResource;
			public bool ReachedEscape;
		}

		/// <summary>
		/// Returns the indices of the ring sides to build, most outward facing first.
		/// <paramref name="count"/> is clamped to 1..3, so at least one full side of the ring is
		/// always left open and the ring can never enclose anything on its own. The side that gets
		/// dropped first is the one pointing at <paramref name="toBase"/>, i.e. the side our own
		/// units need in order to reach the structure we are ringing.
		/// </summary>
		public static int[] OrderRingSides(CVec toBase, int count)
		{
			return Enumerable.Range(0, 4)
				.OrderBy(i => (SideNormals[i].X * toBase.X) + (SideNormals[i].Y * toBase.Y))
				.ThenBy(i => i)
				.Take(count.Clamp(1, 3))
				.ToArray();
		}

		/// <summary>Cells of one side of a square ring, corners included, in a stable order.</summary>
		public static List<CPos> SideCells(CPos center, int radius, int side)
		{
			var cells = new List<CPos>();
			switch (side)
			{
				case 0:
					for (var x = center.X - radius; x <= center.X + radius; x++)
						cells.Add(new CPos(x, center.Y - radius));
					break;
				case 1:
					for (var y = center.Y - radius; y <= center.Y + radius; y++)
						cells.Add(new CPos(center.X + radius, y));
					break;
				case 2:
					for (var x = center.X - radius; x <= center.X + radius; x++)
						cells.Add(new CPos(x, center.Y + radius));
					break;
				default:
					for (var y = center.Y - radius; y <= center.Y + radius; y++)
						cells.Add(new CPos(center.X - radius, y));
					break;
			}

			return cells;
		}

		/// <summary>
		/// Bounded 4-connected flood fill. 4-connected is stricter than what the ground locomotors
		/// actually permit, so anything reported reachable really is reachable. The cell budget
		/// keeps the cost fixed regardless of map size.
		/// </summary>
		public static FloodResult Flood(CPos start, Func<CPos, bool> isBlocked, Func<CPos, bool> hasResource,
			int maxCells, int escapeDistance)
		{
			var escapeSquared = escapeDistance * escapeDistance;
			var visited = new HashSet<CPos> { start };
			var queue = new Queue<CPos>();
			queue.Enqueue(start);

			var result = default(FloodResult);
			while (queue.Count > 0 && result.Cells < maxCells)
			{
				var cell = queue.Dequeue();
				result.Cells++;

				if (!result.ReachedResource && hasResource != null && hasResource(cell))
					result.ReachedResource = true;

				if (!result.ReachedEscape && (cell - start).LengthSquared >= escapeSquared)
					result.ReachedEscape = true;

				foreach (var v in Neighbours)
				{
					var next = cell + v;
					if (!visited.Add(next))
						continue;

					if (isBlocked(next))
						continue;

					queue.Enqueue(next);
				}
			}

			return result;
		}

		/// <summary>
		/// Compares the reachable area before and after a planned wall. The candidate must keep
		/// access to tiberium, must still be able to get clear of the base, and must not lose more
		/// than <paramref name="tolerancePercent"/> of the area it had.
		/// </summary>
		public static bool KeepsBaseOpen(FloodResult baseline, FloodResult candidate, int tolerancePercent)
		{
			if (baseline.ReachedResource && !candidate.ReachedResource)
				return false;

			if (baseline.ReachedEscape && !candidate.ReachedEscape)
				return false;

			return candidate.Cells * 100 >= baseline.Cells * tolerancePercent.Clamp(0, 100);
		}
	}
}
