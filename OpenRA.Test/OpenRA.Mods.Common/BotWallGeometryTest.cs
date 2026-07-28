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
		const int MaxCells = 2000;
		const int EscapeDistance = 20;

		static bool OutsideMap(CPos c)
		{
			return c.X < 0 || c.Y < 0 || c.X >= 120 || c.Y >= 120;
		}

		static bool CanEscape(CPos start, HashSet<CPos> blocked)
		{
			return BotWallGeometry.CanEscape(start, c => OutsideMap(c) || blocked.Contains(c), MaxCells, EscapeDistance);
		}

		// --- facing -------------------------------------------------------------------------------

		[TestCase(TestName = "The wall's facing snaps to an axis and never returns zero")]
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

		[TestCase(TestName = "A wall runs across the direction it faces, not along it")]
		public void WallRunsAcrossItsFacing()
		{
			foreach (var facing in new[] { new CVec(1, 0), new CVec(-1, 0), new CVec(0, 1), new CVec(0, -1) })
			{
				var axis = BotWallGeometry.Perpendicular(facing);
				Assert.That((axis.X * facing.X) + (axis.Y * facing.Y), Is.EqualTo(0), "facing " + facing);
				Assert.That(System.Math.Abs(axis.X) + System.Math.Abs(axis.Y), Is.EqualTo(1));
			}
		}

		// --- the line -----------------------------------------------------------------------------

		[TestCase(TestName = "A wall line is straight, contiguous and centred on the tower")]
		public void LineIsStraightAndCentred()
		{
			// A tower at (50, 50) facing an enemy to the east, walled three cells in front.
			var facing = new CVec(1, 0);
			var cells = BotWallGeometry.LineCells(new CPos(50, 50) + (facing * 3), BotWallGeometry.Perpendicular(facing), 15);

			Assert.That(cells.Count, Is.EqualTo(15));
			Assert.That(cells.Distinct().Count(), Is.EqualTo(15));

			// The whole line sits in front of the tower, and covers the tower's own row.
			Assert.That(cells.All(c => c.X == 53), Is.True);
			Assert.That(cells, Does.Contain(new CPos(53, 50)));

			// Contiguous, in order.
			for (var i = 1; i < cells.Count; i++)
				Assert.That(cells[i].Y - cells[i - 1].Y, Is.EqualTo(1));
		}

		[TestCase(TestName = "The longest run wins, because both ends are paid for either way")]
		public void LongestRunWins()
		{
			var cells = BotWallGeometry.LineCells(new CPos(50, 50), new CVec(0, 1), 15);

			// Two gaps, leaving runs of 3, 5 and 5 cells. The 5-cell runs must beat the 3-cell one, and
			// the earlier of the two equal runs is taken so the result does not depend on scan order.
			var blocked = new HashSet<CPos> { cells[3], cells[9] };
			var run = BotWallGeometry.LongestUsableRun(cells, c => !blocked.Contains(c), 4);

			Assert.That(run.Count, Is.EqualTo(5));
			Assert.That(run[0], Is.EqualTo(cells[4]));
			Assert.That(run[run.Count - 1], Is.EqualTo(cells[8]));
		}

		[TestCase(TestName = "A run shorter than the minimum is not worth two anchors")]
		public void ShortRunsAreRejected()
		{
			var cells = BotWallGeometry.LineCells(new CPos(50, 50), new CVec(0, 1), 15);
			var open = new HashSet<CPos> { cells[2], cells[3], cells[4] };

			Assert.That(BotWallGeometry.LongestUsableRun(cells, open.Contains, 4), Is.Empty);
			Assert.That(BotWallGeometry.LongestUsableRun(cells, open.Contains, 3).Count, Is.EqualTo(3));
			Assert.That(BotWallGeometry.LongestUsableRun(cells, c => false, 1), Is.Empty);
		}

		[TestCase(TestName = "A run touching the end of the window is still found")]
		public void RunAtTheEndOfTheWindowIsFound()
		{
			var cells = BotWallGeometry.LineCells(new CPos(50, 50), new CVec(0, 1), 10);
			var blocked = new HashSet<CPos>(cells.Take(4));

			var run = BotWallGeometry.LongestUsableRun(cells, c => !blocked.Contains(c), 4);
			Assert.That(run.Count, Is.EqualTo(6));
			Assert.That(run[run.Count - 1], Is.EqualTo(cells[cells.Count - 1]));
		}

		// --- reachability -------------------------------------------------------------------------

		[TestCase(TestName = "An open field is escapable")]
		public void OpenFieldIsOpen()
		{
			Assert.That(CanEscape(new CPos(60, 60), new HashSet<CPos>()), Is.True);
		}

		[TestCase(TestName = "One wall line in front of a tower never seals the base")]
		public void AWallLineAloneIsAccepted()
		{
			// The longest line the engine will build for free, right next to the flood start.
			var line = BotWallGeometry.LineCells(new CPos(63, 60), new CVec(0, 1), 15);
			Assert.That(CanEscape(new CPos(60, 60), new HashSet<CPos>(line)), Is.True);
		}

		[TestCase(TestName = "A wall that closes the last gap in a terrain pocket is rejected")]
		public void WallThatClosesAPocketIsRejected()
		{
			// A box of cliffs around the base with a single doorway in its east wall.
			var terrain = new HashSet<CPos>();
			for (var i = 52; i <= 68; i++)
			{
				terrain.Add(new CPos(i, 52));
				terrain.Add(new CPos(i, 68));
				terrain.Add(new CPos(52, i));
				if (i < 58 || i > 62)
					terrain.Add(new CPos(68, i));
			}

			// The doorway on its own leaves us able to get clear of the base.
			Assert.That(CanEscape(new CPos(60, 60), terrain), Is.True);

			// A wall line across the doorway does not.
			var line = BotWallGeometry.LineCells(new CPos(68, 60), new CVec(0, 1), 5);
			var blocked = new HashSet<CPos>(terrain);
			foreach (var c in line)
				blocked.Add(c);

			Assert.That(CanEscape(new CPos(60, 60), blocked), Is.False);
		}

		[TestCase(TestName = "A start cell that is itself blocked can never escape")]
		public void BlockedStartIsRejected()
		{
			var blocked = new HashSet<CPos> { new CPos(60, 60) };
			Assert.That(CanEscape(new CPos(60, 60), blocked), Is.False);
		}

		[TestCase(TestName = "The flood is bounded, so a sealed base costs at most the cell budget")]
		public void SealedBaseIsBounded()
		{
			// A one-cell pocket: the search terminates immediately rather than spending the budget.
			var blocked = new HashSet<CPos>
			{
				new CPos(59, 60), new CPos(61, 60), new CPos(60, 59), new CPos(60, 61)
			};

			Assert.That(CanEscape(new CPos(60, 60), blocked), Is.False);
			Assert.That(BotWallGeometry.CanEscape(new CPos(60, 60), c => OutsideMap(c) || blocked.Contains(c), MaxCells, 0), Is.True);
		}
	}
}
