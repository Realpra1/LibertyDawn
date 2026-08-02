#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class OpeningGarrisonLogicTest
	{
		[Test]
		public void OpeningProductionBalancesProgressAndCompletesBothGoals()
		{
			Assert.That(OpeningGarrisonLogic.ShouldBuildRifle(0, 10, 0, 7), Is.True);
			Assert.That(OpeningGarrisonLogic.ShouldBuildRifle(1, 10, 0, 7), Is.False);
			Assert.That(OpeningGarrisonLogic.ShouldBuildRifle(10, 10, 2, 7), Is.False);
			Assert.That(OpeningGarrisonLogic.ShouldBuildRifle(5, 10, 7, 7), Is.True);
		}

		[Test]
		public void RallyRingSurroundsButDoesNotOverlapBuilding()
		{
			var topLeft = new CPos(20, 30);
			var cells = OpeningGarrisonLogic.CellsAroundBuilding(topLeft, new CVec(3, 3), 1);
			Assert.That(cells.Count, Is.EqualTo(16));
			Assert.That(cells.Distinct().Count(), Is.EqualTo(cells.Count));
			Assert.That(cells.Any(c => c.X >= 20 && c.X <= 22 && c.Y >= 30 && c.Y <= 32), Is.False);
		}
	}
}
