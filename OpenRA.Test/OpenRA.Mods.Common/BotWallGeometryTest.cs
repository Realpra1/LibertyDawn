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

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class BotWallGeometryTest
	{
		const int Radius = 2;

		static List<CPos> RingCells(CPos center, int radius, int sideCount, CVec toBase)
		{
			var cells = new List<CPos>();
			foreach (var side in BotWallGeometry.OrderRingSides(toBase, sideCount))
				foreach (var c in BotWallGeometry.SideCells(center, radius, side))
					if (!cells.Contains(c))
						cells.Add(c);

			return cells;
		}

		static List<CPos> FullRingCells(CPos center, int radius)
		{
			var cells = new List<CPos>();
			for (var side = 0; side < 4; side++)
				foreach (var c in BotWallGeometry.SideCells(center, radius, side))
					if (!cells.Contains(c))
						cells.Add(c);

			return cells;
		}

		[TestCase(TestName = "A ring never gets all four sides, whatever is asked for")]
		public void RingIsNeverClosed()
		{
			for (var dx = -3; dx <= 3; dx++)
			{
				for (var dy = -3; dy <= 3; dy++)
				{
					var toBase = new CVec(dx, dy);
					foreach (var requested in new[] { 1, 2, 3, 4, 99 })
					{
						var sides = BotWallGeometry.OrderRingSides(toBase, requested);
						Assert.That(sides.Length, Is.LessThanOrEqualTo(3),
							"Ring for toBase " + toBase + " requested " + requested + " must not close.");
						Assert.That(sides.Length, Is.GreaterThanOrEqualTo(1));
						Assert.That(sides.Distinct().Count(), Is.EqualTo(sides.Length));
					}
				}
			}
		}

		[TestCase(TestName = "The dropped side is the one facing our own base")]
		public void GapFacesTheBase()
		{
			// Base is to the west of the tower, so the west side (index 3) must be the gap.
			Assert.That(BotWallGeometry.OrderRingSides(new CVec(-8, 0), 3), Does.Not.Contain(3));

			// Base to the east -> east side (index 1) is the gap.
			Assert.That(BotWallGeometry.OrderRingSides(new CVec(8, 0), 3), Does.Not.Contain(1));

			// Base to the north -> north side (index 0) is the gap.
			Assert.That(BotWallGeometry.OrderRingSides(new CVec(0, -8), 3), Does.Not.Contain(0));

			// Base to the south -> south side (index 2) is the gap.
			Assert.That(BotWallGeometry.OrderRingSides(new CVec(0, 8), 3), Does.Not.Contain(2));
		}

		[TestCase(TestName = "The gap is wide enough for a unit to drive through")]
		public void GapIsWideEnough()
		{
			var center = new CPos(50, 50);
			var full = FullRingCells(center, Radius);

			for (var dx = -3; dx <= 3; dx++)
			{
				for (var dy = -3; dy <= 3; dy++)
				{
					var built = RingCells(center, Radius, 3, new CVec(dx, dy));
					var open = full.Where(c => !built.Contains(c)).ToList();

					// One full side minus its two shared corners.
					Assert.That(open.Count, Is.GreaterThanOrEqualTo((2 * Radius) + 1 - 2),
							"toBase " + new CVec(dx, dy) + " left only " + open.Count + " open perimeter cells.");
				}
			}
		}

		// --- reachability -------------------------------------------------------------------

		const int MaxCells = 2000;
		const int EscapeDistance = 20;
		const int Tolerance = 90;

		static BotWallGeometry.FloodResult Flood(CPos start, HashSet<CPos> blocked, HashSet<CPos> resources)
		{
			return BotWallGeometry.Flood(start,
				c => c.X < 0 || c.Y < 0 || c.X >= 120 || c.Y >= 120 || blocked.Contains(c),
				c => resources != null && resources.Contains(c),
				MaxCells, EscapeDistance);
		}

		[TestCase(TestName = "An open field floods to the cell budget and finds a way out")]
		public void OpenFieldIsOpen()
		{
			var result = Flood(new CPos(60, 60), new HashSet<CPos>(), new HashSet<CPos> { new CPos(70, 60) });
			Assert.That(result.Cells, Is.EqualTo(MaxCells));
			Assert.That(result.ReachedEscape, Is.True);
			Assert.That(result.ReachedResource, Is.True);
		}

		[TestCase(TestName = "A three sided ring around a tower does not seal the base")]
		public void ThreeSidedRingIsAccepted()
		{
			var start = new CPos(60, 60);
			var resources = new HashSet<CPos> { new CPos(70, 60) };
			var baseline = Flood(start, new HashSet<CPos>(), resources);

			// Tower five cells east of the construction yard, base center at the yard.
			var tower = new CPos(65, 60);
			var planned = new HashSet<CPos>(RingCells(tower, Radius, 3, start - tower));

			var candidate = Flood(start, planned, resources);
			Assert.That(BotWallGeometry.KeepsBaseOpen(baseline, candidate, Tolerance), Is.True);
		}

		[TestCase(TestName = "A closed ring around the base is rejected")]
		public void ClosedRingIsRejected()
		{
			var start = new CPos(60, 60);
			var resources = new HashSet<CPos> { new CPos(70, 60) };
			var baseline = Flood(start, new HashSet<CPos>(), resources);

			// This is what the planner must never be able to produce: all four sides.
			var planned = new HashSet<CPos>(FullRingCells(start, 4));

			var candidate = Flood(start, planned, resources);
			Assert.That(candidate.Cells, Is.LessThan(MaxCells));
			Assert.That(candidate.ReachedEscape, Is.False);
			Assert.That(candidate.ReachedResource, Is.False);
			Assert.That(BotWallGeometry.KeepsBaseOpen(baseline, candidate, Tolerance), Is.False);
		}

		[TestCase(TestName = "A wall that closes the last gap in a cliff line is rejected")]
		public void RingThatClosesATerrainPocketIsRejected()
		{
			// A cliff box around the base with a single one cell doorway at (64, 60).
			var terrain = new HashSet<CPos>();
			for (var i = 56; i <= 64; i++)
			{
				terrain.Add(new CPos(i, 56));
				terrain.Add(new CPos(i, 64));
				terrain.Add(new CPos(56, i));
				terrain.Add(new CPos(64, i));
			}

			terrain.Remove(new CPos(64, 60));

			var start = new CPos(60, 60);
			var resources = new HashSet<CPos> { new CPos(70, 60) };
			var baseline = Flood(start, terrain, resources);

			// The baseline can still get out through the doorway.
			Assert.That(baseline.Cells, Is.EqualTo(MaxCells));
			Assert.That(baseline.ReachedEscape, Is.True);
			Assert.That(baseline.ReachedResource, Is.True);

			// A wall segment across the doorway seals the base into a 49 cell pocket.
			var planned = new HashSet<CPos>(terrain) { new CPos(64, 60) };
			var candidate = Flood(start, planned, resources);

			Assert.That(candidate.Cells, Is.EqualTo(49));
			Assert.That(candidate.ReachedResource, Is.False);
			Assert.That(BotWallGeometry.KeepsBaseOpen(baseline, candidate, Tolerance), Is.False);
		}

		[TestCase(TestName = "Losing tiberium access alone is enough to reject a wall")]
		public void LosingResourceAccessIsRejected()
		{
			var withResource = new BotWallGeometry.FloodResult { Cells = MaxCells, ReachedResource = true, ReachedEscape = true };
			var withoutResource = new BotWallGeometry.FloodResult { Cells = MaxCells, ReachedResource = false, ReachedEscape = true };
			var withoutEscape = new BotWallGeometry.FloodResult { Cells = MaxCells, ReachedResource = true, ReachedEscape = false };

			Assert.That(BotWallGeometry.KeepsBaseOpen(withResource, withoutResource, Tolerance), Is.False);
			Assert.That(BotWallGeometry.KeepsBaseOpen(withResource, withoutEscape, Tolerance), Is.False);
			Assert.That(BotWallGeometry.KeepsBaseOpen(withResource, withResource, Tolerance), Is.True);
		}
	}
}
