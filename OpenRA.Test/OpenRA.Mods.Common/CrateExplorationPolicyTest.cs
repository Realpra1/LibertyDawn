#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * version 3 or later.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class CrateExplorationPolicyTest
	{
		[TestCase(0, true, 0, true)]
		[TestCase(1, true, 0, false)]
		[TestCase(5000, false, 0, true)]
		[TestCase(0, false, 0, true)]
		public void EmergencyNeedsMoneyAndAnMcvToRemainInactive(int cash, bool hasMcv,
			int threshold, bool expected)
		{
			Assert.That(CrateExplorationPolicy.IsEmergency(cash, hasMcv, threshold), Is.EqualTo(expected));
		}

		[Test]
		public void NeverSeenThenOldestRegionsAreRankedDeterministically()
		{
			var ranked = CrateExplorationPolicy.RankRegions(new[] { 20, -1, 10, -1, 10 },
				new HashSet<int> { 1 }, new[] { 0, 1, 2, 3, 4 }, 0);
			Assert.That(ranked, Is.EqualTo(new[] { 3, 2, 4, 0 }));
		}

		[Test]
		public void OrdinaryScoutCandidatesUseConfiguredTypesPriorityAndLimit()
		{
			var priorities = new Dictionary<string, int> { { "e1", 100 }, { "e4", 200 }, { "e5", 300 } };
			var actorTypes = new[] { "e1", "e2", "e4", "e5", "e1", "e5", "jeep" };
			Assert.That(CrateExplorationPolicy.SelectNormalScoutCandidates(actorTypes, priorities, 4),
				Is.EqualTo(new[] { 3, 5, 2, 0 }));
			Assert.That(CrateExplorationPolicy.SelectNormalScoutCandidates(actorTypes, priorities, 10),
				Is.EqualTo(new[] { 3, 5, 2, 0, 4 }));
			Assert.That(CrateExplorationPolicy.SelectNormalScoutCandidates(Enumerable.Repeat("e1", 12).ToArray(),
				priorities, 10), Has.Length.EqualTo(10));
			Assert.That(CrateExplorationPolicy.SelectNormalScoutCandidates(new[] { "e1", "e4" }, priorities, 10),
				Has.Length.EqualTo(2));
			Assert.That(CrateExplorationPolicy.SelectNormalScoutCandidates(actorTypes, priorities, 0), Is.Empty);
		}

		[Test]
		public void CoverageOrderSpreadsEqualAgeScoutsAcrossAllCorners()
		{
			var centers = new[]
			{
				new CPos(0, 0), new CPos(1, 0), new CPos(2, 0),
				new CPos(0, 1), new CPos(1, 1), new CPos(2, 1),
				new CPos(0, 2), new CPos(1, 2), new CPos(2, 2)
			};
			var order = CrateExplorationPolicy.BuildDistributedCoverageOrder(centers);
			Assert.That(order.Take(4), Is.EquivalentTo(new[] { 0, 2, 6, 8 }));
			Assert.That(order, Is.EquivalentTo(Enumerable.Range(0, centers.Length)));
		}

		[Test]
		public void CoverageCursorRotatesEqualAgeRegionsWithoutBreakingStalePriority()
		{
			var ranks = new[] { 0, 3, 1, 4, 2 };
			var ranked = CrateExplorationPolicy.RankRegions(new[] { -1, -1, -1, -1, -1 },
				new HashSet<int> { 3 }, ranks, 2);
			Assert.That(ranked, Is.EqualTo(new[] { 4, 1, 0, 2 }));

			ranked = CrateExplorationPolicy.RankRegions(new[] { 10, -1, 5, -1, 0 },
				new HashSet<int>(), ranks, 4);
			Assert.That(ranked.Take(2), Is.EqualTo(new[] { 3, 1 }));
			Assert.That(ranked.Skip(2), Is.EqualTo(new[] { 4, 2, 0 }));
		}

		[Test]
		public void AsymmetricCoverageOrderIsCompleteAndDeterministic()
		{
			var centers = new[] { new CPos(4, 0), new CPos(1, 2), new CPos(9, 3), new CPos(2, 8), new CPos(8, 9) };
			var first = CrateExplorationPolicy.BuildDistributedCoverageOrder(centers);
			var second = CrateExplorationPolicy.BuildDistributedCoverageOrder(centers);
			Assert.That(first, Is.EqualTo(second));
			Assert.That(first, Is.EquivalentTo(Enumerable.Range(0, centers.Length)));
		}

		[Test]
		public void ProgressAndStallBoundariesAreExact()
		{
			Assert.That(CrateExplorationPolicy.MadeProgress(99, 100), Is.True);
			Assert.That(CrateExplorationPolicy.MadeProgress(100, 100), Is.False);
			Assert.That(CrateExplorationPolicy.HasStalled(499, 0, 500), Is.False);
			Assert.That(CrateExplorationPolicy.HasStalled(500, 0, 500), Is.True);
		}
	}
}
