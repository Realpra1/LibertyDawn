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
	public sealed class CovertHarassmentPolicyTest
	{
		[TestCase(1, 3, 4, 8, 1)]
		[TestCase(3, 3, 4, 8, 1)]
		[TestCase(4, 3, 4, 8, 2)]
		[TestCase(12, 3, 4, 8, 4)]
		[TestCase(30, 3, 4, 2, 2)]
		public void SupportCountIsProportionalAndBounded(int core, int ratio,
			int maximum, int available, int expected)
		{
			Assert.That(CovertHarassmentPolicy.SupportCount(core, ratio, maximum, available), Is.EqualTo(expected));
		}

		[Test]
		public void SupportCountRejectsInvalidOrMissingInputs()
		{
			Assert.That(CovertHarassmentPolicy.SupportCount(0, 3, 4, 4), Is.Zero);
			Assert.That(CovertHarassmentPolicy.SupportCount(4, 0, 4, 4), Is.Zero);
			Assert.That(CovertHarassmentPolicy.SupportCount(4, 3, 4, 0), Is.Zero);
		}

		[Test]
		public void TowersRequireSupportAndWaitOnlyForMissingSupport()
		{
			Assert.That(CovertHarassmentPolicy.CanSelectTarget(true, 0), Is.False);
			Assert.That(CovertHarassmentPolicy.CanSelectTarget(true, 1), Is.True);
			Assert.That(CovertHarassmentPolicy.ShouldWaitForSupport(true, 2, 1), Is.True);
			Assert.That(CovertHarassmentPolicy.ShouldWaitForSupport(true, 2, 2), Is.False);
			Assert.That(CovertHarassmentPolicy.ShouldWaitForSupport(false, 2, 0), Is.False);
		}

		[Test]
		public void TargetScorePrefersPriorityValueDistanceAndIncumbent()
		{
			var baseline = CovertHarassmentPolicy.TargetScore(5000, 500, 20L * 20 * 1024 * 1024, false);
			Assert.That(CovertHarassmentPolicy.TargetScore(6000, 1, 100L * 100 * 1024 * 1024, false), Is.GreaterThan(baseline));
			Assert.That(CovertHarassmentPolicy.TargetScore(5000, 1000, 20L * 20 * 1024 * 1024, false), Is.GreaterThan(baseline));
			Assert.That(CovertHarassmentPolicy.TargetScore(5000, 500, 10L * 10 * 1024 * 1024, false), Is.GreaterThan(baseline));
			Assert.That(CovertHarassmentPolicy.TargetScore(5000, 500, 20L * 20 * 1024 * 1024, true), Is.GreaterThan(baseline));
		}

		[Test]
		public void OrdersAreRateLimitedUnlessTargetChanges()
		{
			Assert.That(CovertHarassmentPolicy.ShouldIssueOrders(true, 10, 9, 50), Is.True);
			Assert.That(CovertHarassmentPolicy.ShouldIssueOrders(false, 20, 10, 50), Is.False);
			Assert.That(CovertHarassmentPolicy.ShouldIssueOrders(false, 60, 10, 50), Is.True);
		}
	}
}
