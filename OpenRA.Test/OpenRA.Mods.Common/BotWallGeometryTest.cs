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

		[TestCase(TestName = "Losing a place we could reach before is enough to reject a wall")]
		public void LosingAWaypointIsRejected()
		{
			var both = new BotWallGeometry.FloodResult { Cells = MaxCells, ReachedResource = true, ReachedEscape = true, TargetsReached = 2 };
			var one = new BotWallGeometry.FloodResult { Cells = MaxCells, ReachedResource = true, ReachedEscape = true, TargetsReached = 1 };

			Assert.That(BotWallGeometry.KeepsBaseOpen(both, one, Tolerance), Is.False);
			Assert.That(BotWallGeometry.KeepsBaseOpen(both, both, Tolerance), Is.True);

			// Gaining reach is obviously fine.
			Assert.That(BotWallGeometry.KeepsBaseOpen(one, both, Tolerance), Is.True);
		}

		// --- chokes -------------------------------------------------------------------------
		const int ChokeMaxWidth = 6;
		const int Corridor = 4;

		/// <summary>
		/// A cliff line down x = 64 with a three cell doorway at y = 58..60. The only way from the
		/// left half of the world to the right half is through it.
		/// </summary>
		static HashSet<CPos> CliffWithDoorway()
		{
			var terrain = new HashSet<CPos>();
			for (var y = 0; y < 120; y++)
				terrain.Add(new CPos(64, y));

			terrain.Remove(new CPos(64, 58));
			terrain.Remove(new CPos(64, 59));
			terrain.Remove(new CPos(64, 60));
			return terrain;
		}

		static bool Blocked(HashSet<CPos> terrain, CPos c)
		{
			return c.X < 0 || c.Y < 0 || c.X >= 120 || c.Y >= 120 || terrain.Contains(c);
		}

		static BotWallGeometry.FloodResult Flood(CPos start, HashSet<CPos> blocked, HashSet<CPos> resources,
			HashSet<CPos> targets, int maxCells)
		{
			return BotWallGeometry.Flood(start,
				c => Blocked(blocked, c),
				c => resources != null && resources.Contains(c),
				maxCells, EscapeDistance, targets);
		}

		[TestCase(TestName = "A doorway in a cliff line is detected as a choke")]
		public void DoorwayIsAChoke()
		{
			var terrain = CliffWithDoorway();
			var found = BotWallGeometry.TryFindChoke(new CPos(64, 59), c => Blocked(terrain, c),
				ChokeMaxWidth, Corridor, out var span, out var axis);

			Assert.That(found, Is.True);
			Assert.That(axis, Is.EqualTo(new CVec(0, 1)), "The span runs along the cliff, so the corridor runs across it.");
			Assert.That(span.Count, Is.EqualTo(3));
			Assert.That(span, Is.EquivalentTo(new[] { new CPos(64, 58), new CPos(64, 59), new CPos(64, 60) }));
		}

		[TestCase(TestName = "Open ground is not a choke")]
		public void OpenGroundIsNotAChoke()
		{
			var empty = new HashSet<CPos>();
			Assert.That(BotWallGeometry.TryFindChoke(new CPos(30, 30), c => Blocked(empty, c),
				ChokeMaxWidth, Corridor, out _, out _), Is.False);
		}

		[TestCase(TestName = "A dead end pocket is not a choke")]
		public void PocketIsNotAChoke()
		{
			// A 3x3 hole in solid rock: pinched on both axes, so there is no corridor to funnel anyone into.
			var terrain = new HashSet<CPos>();
			for (var x = 28; x <= 34; x++)
				for (var y = 28; y <= 34; y++)
					terrain.Add(new CPos(x, y));

			for (var x = 30; x <= 32; x++)
				for (var y = 30; y <= 32; y++)
					terrain.Remove(new CPos(x, y));

			Assert.That(BotWallGeometry.TryFindChoke(new CPos(31, 31), c => Blocked(terrain, c),
				ChokeMaxWidth, Corridor, out _, out _), Is.False);
		}

		[TestCase(TestName = "A choke wall always leaves a gap, whatever the yaml asks for")]
		public void ChokeWallAlwaysLeavesAGap()
		{
			var towardBase = new CPos(0, 59);
			for (var width = 1; width <= 8; width++)
			{
				var span = new List<CPos>();
				for (var i = 0; i < width; i++)
					span.Add(new CPos(64, 56 + i));

				foreach (var requested in new[] { -5, 0, 1, 2, 99 })
				{
					var wallable = BotWallGeometry.WallableChokeCells(span, requested, towardBase);
					Assert.That(wallable.Count, Is.LessThan(span.Count),
						"width " + width + " gapCells " + requested + " walled the entire choke.");
					Assert.That(wallable.Distinct().Count(), Is.EqualTo(wallable.Count));
					foreach (var c in wallable)
						Assert.That(span, Does.Contain(c));
				}
			}
		}

		[TestCase(TestName = "Walling a choke shut traps the base and is rejected")]
		public void SealingAChokeIsRejected()
		{
			var terrain = CliffWithDoorway();
			var start = new CPos(60, 60);

			// Tiberium and one of our own refineries are both on the far side of the choke.
			var resources = new HashSet<CPos> { new CPos(80, 60) };
			var targets = new HashSet<CPos> { new CPos(90, 60) };

			var baseline = Flood(start, terrain, resources, targets, MaxCells);
			Assert.That(baseline.ReachedResource, Is.True);
			Assert.That(baseline.TargetsReached, Is.EqualTo(1));

			// Concrete across all three doorway cells.
			var planned = new HashSet<CPos>(terrain)
			{
				new CPos(64, 58), new CPos(64, 59), new CPos(64, 60)
			};

			var candidate = Flood(start, planned, resources, targets, MaxCells);

			// The left half is enormous, so the flood still hits its cell budget and still gets clear
			// of the base. Area and escape alone would happily accept this wall - it is the resource
			// and waypoint checks that catch it.
			Assert.That(candidate.Cells, Is.EqualTo(MaxCells));
			Assert.That(candidate.ReachedEscape, Is.True);
			Assert.That(candidate.ReachedResource, Is.False);
			Assert.That(candidate.TargetsReached, Is.EqualTo(0));
			Assert.That(BotWallGeometry.KeepsBaseOpen(baseline, candidate, Tolerance), Is.False);
		}

		[TestCase(TestName = "Walling a choke but leaving its gap keeps the base open")]
		public void ChokeWallWithAGapIsAccepted()
		{
			var terrain = CliffWithDoorway();
			var start = new CPos(60, 60);
			var resources = new HashSet<CPos> { new CPos(80, 60) };
			var targets = new HashSet<CPos> { new CPos(90, 60) };

			Assert.That(BotWallGeometry.TryFindChoke(new CPos(64, 59), c => Blocked(terrain, c),
				ChokeMaxWidth, Corridor, out var span, out _), Is.True);

			var wallable = BotWallGeometry.WallableChokeCells(span, 1, start);
			Assert.That(wallable.Count, Is.EqualTo(2));

			var baseline = Flood(start, terrain, resources, targets, MaxCells);
			var planned = new HashSet<CPos>(terrain);
			foreach (var c in wallable)
				planned.Add(c);

			var candidate = Flood(start, planned, resources, targets, MaxCells);
			Assert.That(candidate.ReachedResource, Is.True);
			Assert.That(candidate.TargetsReached, Is.EqualTo(1));
			Assert.That(BotWallGeometry.KeepsBaseOpen(baseline, candidate, Tolerance), Is.True);
		}

		[TestCase(TestName = "Turret slots sit behind the wall, on our side, middle first")]
		public void TurretSlotsSitBehindTheWall()
		{
			// A wall line running north-south at x = 64; our base is to the west.
			var line = new List<CPos>();
			for (var y = 56; y <= 60; y++)
				line.Add(new CPos(64, y));

			var inward = new CVec(-1, 0);
			var slots = BotWallGeometry.SlotsBehind(line, inward, 2);

			Assert.That(slots.Count, Is.EqualTo(line.Count));
			Assert.That(slots.Distinct().Count(), Is.EqualTo(slots.Count));

			// Every slot is setback cells further from the enemy than the wall it hides behind.
			foreach (var s in slots)
			{
				Assert.That(s.X, Is.EqualTo(62));
				Assert.That(line, Does.Contain(new CPos(64, s.Y)));
			}

			// The middle of the line is covered first.
			Assert.That(slots[0], Is.EqualTo(new CPos(62, 58)));

			// A zero or negative setback would put the turret inside the wall; it is clamped away.
			Assert.That(BotWallGeometry.SlotsBehind(line, inward, 0)[0], Is.EqualTo(new CPos(63, 58)));
		}

		[TestCase(TestName = "The inward direction snaps to an axis and never returns zero")]
		public void DominantDirectionIsAlwaysAnAxis()
		{
			for (var dx = -3; dx <= 3; dx++)
			{
				for (var dy = -3; dy <= 3; dy++)
				{
					var d = BotWallGeometry.DominantDirection(new CPos(50, 50), new CPos(50 + dx, 50 + dy));
					Assert.That(System.Math.Abs(d.X) + System.Math.Abs(d.Y), Is.EqualTo(1),
						"delta " + dx + "," + dy + " gave " + d);
				}
			}

			Assert.That(BotWallGeometry.DominantDirection(new CPos(50, 50), new CPos(40, 51)), Is.EqualTo(new CVec(-1, 0)));
			Assert.That(BotWallGeometry.DominantDirection(new CPos(50, 50), new CPos(51, 60)), Is.EqualTo(new CVec(0, 1)));
		}
	}
}
