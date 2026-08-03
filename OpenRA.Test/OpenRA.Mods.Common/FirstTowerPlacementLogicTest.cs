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
	public class FirstTowerPlacementLogicTest
	{
		[Test]
		public void PreferredCellIsTopMiddleBesideConstructionYard()
		{
			Assert.That(FirstTowerPlacementLogic.PreferredLocation(new CPos(10, 20), new CVec(3, 3), new CVec(1, 1)),
				Is.EqualTo(new CPos(11, 19)));
		}

		[Test]
		public void FallbackCandidatesStartAtPreferredAndIncreaseByDistance()
		{
			var preferred = new CPos(10, 10);
			var candidates = FirstTowerPlacementLogic.CandidateLocations(preferred, 2).ToArray();
			Assert.That(candidates[0], Is.EqualTo(preferred));
			Assert.That(candidates.Length, Is.EqualTo(25));
			Assert.That(candidates.Select(c => (c - preferred).LengthSquared), Is.Ordered);
		}

		[Test]
		public void BlockedPreferredCellUsesNearestDeterministicLegalFallback()
		{
			var preferred = new CPos(10, 10);
			var selected = FirstTowerPlacementLogic.ClosestLegalLocation(preferred, 2,
				c => c != preferred && c != new CPos(10, 9));
			Assert.That(selected, Is.EqualTo(new CPos(9, 10)));
		}
	}
}
