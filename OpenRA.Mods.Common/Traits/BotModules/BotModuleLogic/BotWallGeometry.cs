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

		/// <summary>The two axes a choke can be pinched along. Walls are axis aligned, so these are enough.</summary>
		public static readonly CVec[] ChokeAxes =
		{
			new CVec(1, 0),
			new CVec(0, 1)
		};

		public struct FloodResult
		{
			public int Cells;
			public bool ReachedResource;
			public bool ReachedEscape;

			/// <summary>How many of the cells handed to <see cref="Flood"/> as targets were reached.</summary>
			public int TargetsReached;
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
			return Flood(start, isBlocked, hasResource, maxCells, escapeDistance, null);
		}

		/// <summary>
		/// As above, but also counts how many of <paramref name="targets"/> the flood reaches. Used to
		/// prove a planned wall does not cut the bot off from places it already owns or needs - its
		/// other construction yards, its refineries, and the far side of a choke it is about to wall.
		/// Every cell is dequeued at most once, so the count is a distinct count.
		/// </summary>
		public static FloodResult Flood(CPos start, Func<CPos, bool> isBlocked, Func<CPos, bool> hasResource,
			int maxCells, int escapeDistance, HashSet<CPos> targets)
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

				if (targets != null && targets.Contains(cell))
					result.TargetsReached++;

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
		/// access to tiberium, must still be able to get clear of the base, must not lose any of the
		/// places the baseline could reach (own construction yards, refineries, the far side of a
		/// choke), and must not lose more than <paramref name="tolerancePercent"/> of the area it had.
		/// </summary>
		public static bool KeepsBaseOpen(FloodResult baseline, FloodResult candidate, int tolerancePercent)
		{
			if (baseline.ReachedResource && !candidate.ReachedResource)
				return false;

			if (baseline.ReachedEscape && !candidate.ReachedEscape)
				return false;

			if (candidate.TargetsReached < baseline.TargetsReached)
				return false;

			return candidate.Cells * 100 >= baseline.Cells * tolerancePercent.Clamp(0, 100);
		}

		// --- choke detection ------------------------------------------------------------------

		/// <summary>
		/// Walks from <paramref name="from"/> along <paramref name="dir"/> while the cells are passable,
		/// for at most <paramref name="maxSteps"/> steps. Returns the number of passable cells stepped
		/// over; <paramref name="hitBlocker"/> says whether the walk stopped because it ran into
		/// something rather than because it ran out of steps.
		/// </summary>
		public static int Run(CPos from, CVec dir, Func<CPos, bool> isBlocked, int maxSteps, out bool hitBlocker)
		{
			for (var i = 1; i <= maxSteps; i++)
			{
				if (isBlocked(from + (dir * i)))
				{
					hitBlocker = true;
					return i - 1;
				}
			}

			hitBlocker = false;
			return maxSteps < 0 ? 0 : maxSteps;
		}

		/// <summary>
		/// A choke is a short pinch of passable ground between two blockers, sitting on a corridor that
		/// is open in the perpendicular direction. This tests one cell for that shape and, if it matches,
		/// returns the full span across the pinch plus the axis the span runs along. The corridor itself
		/// therefore runs perpendicular to <paramref name="axis"/>.
		///
		/// Cost is bounded: at most 2 axes * 2 directions * (maxWidth + minCorridorLength) blocked-cell
		/// lookups, and most cells fail on the first direction.
		/// </summary>
		public static bool TryFindChoke(CPos cell, Func<CPos, bool> isBlocked, int maxWidth, int minCorridorLength,
			out List<CPos> span, out CVec axis)
		{
			span = null;
			axis = CVec.Zero;

			if (maxWidth < 1 || isBlocked(cell))
				return false;

			for (var i = 0; i < ChokeAxes.Length; i++)
			{
				var a = ChokeAxes[i];
				var perpendicular = ChokeAxes[1 - i];

				var back = Run(cell, -a, isBlocked, maxWidth, out var backBlocked);
				if (!backBlocked)
					continue;

				var forward = Run(cell, a, isBlocked, maxWidth, out var forwardBlocked);
				if (!forwardBlocked)
					continue;

				var width = back + forward + 1;
				if (width > maxWidth)
					continue;

				// A pinch that is closed off on the perpendicular axis too is a pocket, not a corridor -
				// walling it achieves nothing and the flood check would reject it anyway.
				Run(cell, perpendicular, isBlocked, minCorridorLength, out var aheadBlocked);
				if (aheadBlocked)
					continue;

				Run(cell, -perpendicular, isBlocked, minCorridorLength, out var behindBlocked);
				if (behindBlocked)
					continue;

				span = new List<CPos>(width);
				for (var s = -back; s <= forward; s++)
					span.Add(cell + (a * s));

				axis = a;
				return true;
			}

			return false;
		}

		/// <summary>
		/// The part of a choke span that may actually be walled. At least <paramref name="gapCells"/>
		/// cells are always left open, so a choke can never be structurally sealed however the yaml is
		/// configured - the same "gaps stay" guarantee <see cref="OrderRingSides"/> gives for rings.
		/// The gap is left at the end of the span nearest <paramref name="towardBase"/>, which is the
		/// side our own units come from.
		/// </summary>
		public static List<CPos> WallableChokeCells(List<CPos> span, int gapCells, CPos towardBase)
		{
			var result = new List<CPos>();
			if (span == null || span.Count == 0)
				return result;

			var gap = gapCells.Clamp(1, span.Count);
			var keep = span.Count - gap;
			if (keep <= 0)
				return result;

			var startIsNearer = (span[0] - towardBase).LengthSquared <= (span[span.Count - 1] - towardBase).LengthSquared;
			var offset = startIsNearer ? gap : 0;
			for (var i = 0; i < keep; i++)
				result.Add(span[offset + i]);

			return result;
		}

		/// <summary>
		/// Cells <paramref name="setback"/> behind a wall line, ordered from the middle of the line
		/// outwards. This is where turrets go: the wall is between them and whatever the line faces,
		/// and the middle of the line is covered first because that is the part most shot at.
		/// </summary>
		public static List<CPos> SlotsBehind(List<CPos> line, CVec inward, int setback)
		{
			var slots = new List<CPos>();
			if (line == null || line.Count == 0)
				return slots;

			var offset = inward * setback.Clamp(1, 8);
			var middle = line.Count / 2;
			for (var d = 0; d < line.Count; d++)
			{
				if (middle - d >= 0)
					slots.Add(line[middle - d] + offset);

				if (d > 0 && middle + d < line.Count)
					slots.Add(line[middle + d] + offset);
			}

			return slots;
		}

		/// <summary>
		/// Unit vector from <paramref name="from"/> towards <paramref name="to"/>, snapped to whichever
		/// of the four axis directions is dominant. Deterministic, including the diagonal tie break.
		/// </summary>
		public static CVec DominantDirection(CPos from, CPos to)
		{
			var dx = to.X - from.X;
			var dy = to.Y - from.Y;
			if (Math.Abs(dx) >= Math.Abs(dy))
				return new CVec(dx >= 0 ? 1 : -1, 0);

			return new CVec(0, dy >= 0 ? 1 : -1);
		}
	}
}
