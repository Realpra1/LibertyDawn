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
	public readonly struct OpenScreenWallPlacement
	{
		public readonly CPos Cell;
		public readonly bool UseLineBuild;

		public OpenScreenWallPlacement(CPos cell, bool useLineBuild)
		{
			Cell = cell;
			UseLineBuild = useLineBuild;
		}
	}

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
		/// Returns the occupied cells plus one already-proven approach cell. Reserving every
		/// perimeter cell can make a cramped three-tower cluster impossible; one persisted
		/// reachable lane is enough to prevent cluster towers from sealing the facility.
		/// </summary>
		public static HashSet<CPos> WithApproachCell(IEnumerable<CPos> cells, CPos approachCell)
		{
			var reserved = new HashSet<CPos>(cells ?? Enumerable.Empty<CPos>());
			reserved.Add(approachCell);

			return reserved;
		}

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

		/// <summary>
		/// Three shallow sides around a cluster: one enemy-facing line and two short flanks.
		/// The base-facing side is deliberately absent. Each returned line is ordered and the
		/// whole result is necessarily non-collinear when flankDepth is positive.
		/// </summary>
		public static List<List<CPos>> OpenScreenLines(CPos center, CVec enemyFacing,
			int setback, int halfWidth, int flankDepth)
		{
			var facing = DominantDirection(new CPos(0, 0), new CPos(enemyFacing.X, enemyFacing.Y));
			var across = Perpendicular(facing);
			var frontCenter = center + facing * Math.Max(1, setback);
			var width = Math.Max(2, halfWidth);
			var depth = Math.Max(1, flankDepth);

			var front = new List<CPos>();
			for (var i = -width; i <= width; i++)
				front.Add(frontCenter + across * i);

			var left = new List<CPos>();
			var right = new List<CPos>();
			for (var i = 0; i <= depth; i++)
			{
				left.Add(frontCenter - across * width - facing * i);
				right.Add(frontCenter + across * width - facing * i);
			}

			return new List<List<CPos>> { front, left, right };
		}

		/// <summary>
		/// The configured open screen followed by at most two proportionally smaller centered variants,
		/// then one-cell lateral translations of the smallest screen. Every candidate retains one front
		/// and both flanks, so fallback never degrades into the deprecated single straight line. The
		/// small fixed bound keeps planning deterministic and cheap.
		/// </summary>
		public static List<List<List<CPos>>> OpenScreenVariants(CPos center, CVec enemyFacing,
			int setback, int halfWidth, int flankDepth)
		{
			var width = Math.Max(2, halfWidth);
			var depth = Math.Max(1, flankDepth);
			var shrinkSteps = Math.Min(2, Math.Max(width - 2, depth - 1));
			var variants = new List<List<List<CPos>>>(shrinkSteps + 1);
			for (var i = 0; i <= shrinkSteps; i++)
			{
				var candidateWidth = Math.Max(2, width - i);
				var candidateDepth = Math.Max(1, depth - i);
				variants.Add(OpenScreenLines(center, enemyFacing, setback, candidateWidth, candidateDepth));
			}

			// Nearby defenses intentionally do not extend buildable area. A centered screen can therefore
			// miss an established local facility's build area by one cell even when an equally shallow
			// lateral placement is legal. Try only the two immediate translations of the smallest shape.
			var facing = DominantDirection(new CPos(0, 0), new CPos(enemyFacing.X, enemyFacing.Y));
			var across = Perpendicular(facing);
			var smallestWidth = Math.Max(2, width - shrinkSteps);
			var smallestDepth = Math.Max(1, depth - shrinkSteps);
			variants.Add(OpenScreenLines(center - across, enemyFacing, setback, smallestWidth, smallestDepth));
			variants.Add(OpenScreenLines(center + across, enemyFacing, setback, smallestWidth, smallestDepth));

			return variants;
		}

		/// <summary>
		/// Emits a fresh three-sided screen without ever LineBuilding between the two inward flank ends.
		/// The rear flank anchors are placed individually first. The two front anchors then LineBuild to
		/// those ends and to each other, producing exactly the front and two flanks while leaving the
		/// inward side open.
		/// </summary>
		public static List<OpenScreenWallPlacement> OpenScreenPlacements(List<List<CPos>> lines)
		{
			var result = new List<OpenScreenWallPlacement>();
			if (lines == null || lines.Count != 3 || lines.Any(line => line == null || line.Count < 2))
				return result;

			var front = lines[0];
			var left = lines[1];
			var right = lines[2];
			result.Add(new OpenScreenWallPlacement(left[left.Count - 1], false));
			result.Add(new OpenScreenWallPlacement(right[right.Count - 1], false));
			result.Add(new OpenScreenWallPlacement(front[0], true));
			result.Add(new OpenScreenWallPlacement(front[front.Count - 1], true));
			return result;
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

		/// <summary>Bounded deterministic 4-connected reachability to any one of the target cells.</summary>
		public static bool CanReachAny(CPos start, IEnumerable<CPos> targets,
			Func<CPos, bool> isBlocked, int maxCells)
		{
			if (isBlocked(start))
				return false;

			var remaining = new HashSet<CPos>(targets);
			if (remaining.Count == 0)
				return false;
			if (remaining.Contains(start))
				return true;

			var budget = maxCells.Clamp(64, 20000);
			var visited = new HashSet<CPos> { start };
			var queue = new Queue<CPos>();
			queue.Enqueue(start);
			var cells = 0;
			while (queue.Count > 0 && cells++ < budget)
			{
				var cell = queue.Dequeue();
				foreach (var v in Neighbours)
				{
					var next = cell + v;
					if (!visited.Add(next) || isBlocked(next))
						continue;
					if (remaining.Contains(next))
						return true;

					queue.Enqueue(next);
				}
			}

			return false;
		}
	}
}
