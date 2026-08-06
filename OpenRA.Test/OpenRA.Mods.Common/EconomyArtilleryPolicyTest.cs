#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class EconomyArtilleryPolicyTest
	{
		[TestCase(1800, 120, 5, 10, 0, 1)]
		[TestCase(7200, 800, 5, 10, 0, 0)]
		[TestCase(8100, 800, 5, 10, 0, 1)]
		[TestCase(18000, 600, 5, 10, 0, 1)]
		public void EscortCountRoundsToClosestValueShare(int artilleryValue, int escortCost,
			int percent, int available, int minimum, int expected)
		{
			Assert.That(EconomyArtilleryPolicy.EscortCount(
				artilleryValue, escortCost, percent, available, minimum), Is.EqualTo(expected));
		}

		[Test]
		public void RequiredFirstAntiAirIsTheOnlyIntentionalSmallClusterException()
		{
			Assert.That(EconomyArtilleryPolicy.EscortCount(900, 600, 5, 4, 1), Is.EqualTo(1));
			Assert.That(EconomyArtilleryPolicy.EscortCount(0, 600, 5, 4, 1), Is.Zero);
		}

		[Test]
		public void EscortCountIsBoundedByAvailability()
		{
			Assert.That(EconomyArtilleryPolicy.EscortCount(100000, 120, 5, 2), Is.EqualTo(2));
			Assert.That(EconomyArtilleryPolicy.EscortCount(1000, 0, 5, 2), Is.Zero);
		}

		[Test]
		public void TargetScorePrefersPriorityThenValueAndDistance()
		{
			var baseline = EconomyArtilleryPolicy.TargetScore(2000, 500, 20L * 20 * 1024 * 1024);
			Assert.That(EconomyArtilleryPolicy.TargetScore(3000, 1, 1000L * 1000 * 1024 * 1024), Is.GreaterThan(baseline));
			Assert.That(EconomyArtilleryPolicy.TargetScore(2000, 1000, 20L * 20 * 1024 * 1024), Is.GreaterThan(baseline));
			Assert.That(EconomyArtilleryPolicy.TargetScore(2000, 500, 10L * 10 * 1024 * 1024), Is.GreaterThan(baseline));
		}

		[Test]
		public void OnlyStructuresAreArtilleryObjectives()
		{
			Assert.That(EconomyArtilleryPolicy.TargetPriority(true, true), Is.EqualTo(3000));
			Assert.That(EconomyArtilleryPolicy.TargetPriority(true, false), Is.EqualTo(2000));
			Assert.That(EconomyArtilleryPolicy.TargetPriority(false, true), Is.Zero);
		}

		[Test]
		public void OrdersAreRateLimitedUnlessTheTargetChanges()
		{
			Assert.That(EconomyArtilleryPolicy.ShouldIssueOrders(true, 10, 9, 25), Is.True);
			Assert.That(EconomyArtilleryPolicy.ShouldIssueOrders(false, 20, 10, 25), Is.False);
			Assert.That(EconomyArtilleryPolicy.ShouldIssueOrders(false, 35, 10, 25), Is.True);
		}
	}
}
