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

using System.Collections.Generic;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class CaptureTargetingTest
	{
		[Test]
		public void RecoveredActorMakesHuskEconomicallyValuable()
		{
			Assert.That(CaptureTargeting.EconomicValue(0, 1500), Is.EqualTo(1500));
			Assert.That(CaptureTargeting.EconomicValue(2000, 1500), Is.EqualTo(2000));
		}

		[Test]
		public void SelectsValueThenBuildingThenDistanceAndDoesNotDuplicate()
		{
			var scores = new double[] { 1000, 1500, 1500, 1500 };
			var buildings = new[] { true, false, true, true };
			var distances = new long[] { 1, 1, 100, 25 };
			var assigned = new HashSet<int>();

			var first = CaptureTargeting.BestTargetIndex(scores, buildings, distances, assigned);
			Assert.That(first, Is.EqualTo(3));
			assigned.Add(first);
			Assert.That(CaptureTargeting.BestTargetIndex(scores, buildings, distances, assigned), Is.EqualTo(2));
		}

		[Test]
		public void TargetScoreBalancesEconomicValueAgainstTravelDistance()
		{
			var nearbyHusk = CaptureTargeting.Score(1100, 2, 10);
			var distantBuilding = CaptureTargeting.Score(4000, 170, 10);

			Assert.That(nearbyHusk, Is.GreaterThan(distantBuilding));
			Assert.That(CaptureTargeting.Score(4000, 0, 10), Is.EqualTo(4000));
		}

		[Test]
		public void BuildingPairRequirementUsesExactHealthRatio()
		{
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, 79, 100, 80), Is.False);
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, 80, 100, 80), Is.False);
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, 80001, 100000, 80), Is.True);
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, 81, 100, 80), Is.True);
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, 1, 0, 80), Is.True);
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, 1, -1, 80), Is.True);
			Assert.That(CaptureTargeting.RequiresEngineerPair(false, int.MaxValue, 1, 80), Is.False);
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, int.MaxValue, int.MaxValue, 80), Is.True);
		}

		[Test]
		public void RetargetingUsesStrictImprovementMargin()
		{
			Assert.That(CaptureTargeting.ShouldRetarget(100, 124, 25), Is.False);
			Assert.That(CaptureTargeting.ShouldRetarget(100, 125, 25), Is.False);
			Assert.That(CaptureTargeting.ShouldRetarget(100, 126, 25), Is.True);
		}

		[Test]
		public void PairUsesWorseMemberAndComparesDistinctSoloTargets()
		{
			Assert.That(CaptureTargeting.PairScore(900, 300), Is.EqualTo(300));
			Assert.That(CaptureTargeting.PairScore(900, -1), Is.EqualTo(-1));

			var firstScores = new double[] { 80, 60, -1 };
			var secondScores = new double[] { 70, 100, -1 };
			var allocation = CaptureTargeting.BestDistinctTargetAllocation(
				firstScores, secondScores, new HashSet<int>());
			Assert.That(allocation.FirstTarget, Is.EqualTo(0));
			Assert.That(allocation.SecondTarget, Is.EqualTo(1));
			Assert.That(allocation.Score, Is.EqualTo(180));

			allocation = CaptureTargeting.BestDistinctTargetAllocation(
				firstScores, secondScores, new HashSet<int> { 1 });
			Assert.That(allocation.FirstTarget, Is.EqualTo(0));
			Assert.That(allocation.SecondTarget, Is.EqualTo(-1));
			Assert.That(allocation.Score, Is.EqualTo(80));
		}

		[Test]
		public void DistinctSoloAllocationBreaksEqualTiesDeterministically()
		{
			var scores = new double[] { 100, 100 };
			var allocation = CaptureTargeting.BestDistinctTargetAllocation(
				scores, scores, new HashSet<int>());
			Assert.That(allocation.FirstTarget, Is.EqualTo(0));
			Assert.That(allocation.SecondTarget, Is.EqualTo(1));
			Assert.That(allocation.Score, Is.EqualTo(200));
		}
	}
}
