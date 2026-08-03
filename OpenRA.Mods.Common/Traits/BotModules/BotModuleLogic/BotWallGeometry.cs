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

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// The world-independent half of the bot wall planner: which way a wall faces, where its cells
	/// are, and the one cheap check that stops the bot walling itself in. Kept free of World so it
	/// can be unit tested. Everything here is deterministic - no randomness, no iteration over
	/// unordered collections.
	/// </summary>
	public static class BotWallGeometry
	{
		static readonly CVec[] Neighbours =
		{
			new CVec(1, 0),
			new CVec(-1, 0),
			new CVec(0, 1),
			new CVec(0, -1)
		};

		/// <summary>
		/// Unit vector from <paramref name="from"/> towards <paramref name="to"/>, snapped to whichever
		/// of the four axis directions is dominant. Walls are axis aligned, so this is all the
		/// direction resolution there is. Deterministic, including the diagonal tie break.
		/// </summary>
		public static CVec DominantDirection(CPos from, CPos to)
		{
			var dx = to.X - from.X;
			var dy = to.Y - from.Y;
			if (Math.Abs(dx) >= Math.Abs(dy))
				return new CVec(dx >= 0 ? 1 : -1, 0);

			return new CVec(0, dy >= 0 ? 1 : -1);
		}

		/// <summary>The axis a wall runs along when it faces <paramref name="facing"/>.</summary>
		public static CVec Perpendicular(CVec facing)
		{
			return facing.X != 0 ? new CVec(0, 1) : new CVec(1, 0);
		}

		/// <summary>
		/// <paramref name="length"/> cells running along <paramref name="axis"/>, centred on
		/// <paramref name="center"/>.
		/// </summary>
		public static List<CPos> LineCells(CPos center, CVec axis, int length)
		{
			var cells = new List<CPos>();
			var count = length.Clamp(1, 64);
			for (var i = 0; i < count; i++)
				cells.Add(center + (axis * (i - (count / 2))));

			return cells;
		}

		public static List<CPos> EnclosureCorners(CPos topLeft, CVec dimensions, int margin)
		{
			var gap = Math.Max(0, margin);
			var left = topLeft.X - gap;
			var top = topLeft.Y - gap;
			var right = topLeft.X + Math.Max(1, dimensions.X) - 1 + gap;
			var bottom = topLeft.Y + Math.Max(1, dimensions.Y) - 1 + gap;
			return new List<CPos>
			{
				new CPos(left, top),
				new CPos(right, top),
				new CPos(right, bottom),
				new CPos(left, bottom)
			};
		}

		public static List<CPos> EnclosurePerimeter(CPos topLeft, CVec dimensions, int margin)
		{
			var corners = EnclosureCorners(topLeft, dimensions, margin);
			var cells = new List<CPos>();
			for (var x = corners[0].X; x <= corners[1].X; x++)
			{
				cells.Add(new CPos(x, corners[0].Y));
				if (corners[2].Y != corners[0].Y)
					cells.Add(new CPos(x, corners[2].Y));
			}

			for (var y = corners[0].Y + 1; y < corners[3].Y; y++)
			{
				cells.Add(new CPos(corners[0].X, y));
				if (corners[1].X != corners[0].X)
					cells.Add(new CPos(corners[1].X, y));
			}

			return cells;
		}

		/// <summary>
		/// The longest contiguous run of usable cells in <paramref name="cells"/>, or an empty list if
		/// the longest run is shorter than <paramref name="minLength"/>. This is where "prefer one long
		/// wall over several short ones" actually lives: two anchors are paid for either way, so the run
		/// that gives the most free cells between them wins. Ties keep the earlier run, which makes the
		/// result a pure function of the input order.
		/// </summary>
		public static List<CPos> LongestUsableRun(List<CPos> cells, Func<CPos, bool> isUsable, int minLength)
		{
			var result = new List<CPos>();
			if (cells == null)
				return result;

			var bestStart = -1;
			var bestLength = 0;
			var runStart = -1;

			for (var i = 0; i <= cells.Count; i++)
			{
				if (i < cells.Count && isUsable(cells[i]))
				{
					if (runStart < 0)
						runStart = i;

					continue;
				}

				if (runStart >= 0 && i - runStart > bestLength)
				{
					bestStart = runStart;
					bestLength = i - runStart;
				}

				runStart = -1;
			}

			if (bestLength < minLength)
				return result;

			for (var i = 0; i < bestLength; i++)
				result.Add(cells[bestStart + i]);

			return result;
		}

		/// <summary>
		/// Bounded 4-connected flood fill that answers one question: can something standing at
		/// <paramref name="start"/> still get <paramref name="escapeDistance"/> cells away? 4-connected
		/// is stricter than what the ground locomotors actually permit, so anything reported reachable
		/// really is reachable. The cell budget keeps the cost fixed regardless of map size, and the
		/// search stops the moment it escapes, so the accepting case is far cheaper than the budget.
		/// </summary>
		public static bool CanEscape(CPos start, Func<CPos, bool> isBlocked, int maxCells, int escapeDistance)
		{
			if (isBlocked(start))
				return false;

			var escapeSquared = escapeDistance * escapeDistance;
			if (escapeSquared <= 0)
				return true;

			var budget = maxCells.Clamp(64, 20000);
			var visited = new HashSet<CPos> { start };
			var queue = new Queue<CPos>();
			queue.Enqueue(start);

			var cells = 0;
			while (queue.Count > 0 && cells < budget)
			{
				var cell = queue.Dequeue();
				cells++;

				if ((cell - start).LengthSquared >= escapeSquared)
					return true;

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

			return false;
		}
	}
}
