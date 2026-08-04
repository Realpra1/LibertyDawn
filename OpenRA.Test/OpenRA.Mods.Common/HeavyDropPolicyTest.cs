#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class HeavyDropPolicyTest
	{
		[TestCase(false, 10000, 7500, 10, 10, 10, false)]
		[TestCase(true, 7499, 7500, 10, 10, 10, false)]
		[TestCase(true, 10000, 7500, 9, 10, 10, false)]
		[TestCase(true, 10000, 7500, 10, 9, 10, false)]
		[TestCase(true, 10000, 7500, 10, 10, 10, true)]
		[TestCase(true, 0, 0, 10, 10, 10, true)]
		public void PreparationRequiresEligibleLateGameAndCompleteWave(bool eligible, int tick,
			int minimumTick, int passengers, int transports, int desired, bool expected)
		{
			Assert.That(HeavyDropPolicy.CanPrepare(eligible, tick, minimumTick,
				passengers, transports, desired), Is.EqualTo(expected));
		}

		[Test]
		public void FullWaveLeavesImmediatelyButPartialWaveWaitsForTimeout()
		{
			Assert.That(HeavyDropPolicy.ReadyToTravel(10, 10, 8, 1, 3000), Is.True);
			Assert.That(HeavyDropPolicy.ReadyToTravel(8, 10, 8, 2999, 3000), Is.False);
			Assert.That(HeavyDropPolicy.ReadyToTravel(8, 10, 8, 3000, 3000), Is.True);
			Assert.That(HeavyDropPolicy.ReadyToTravel(7, 10, 8, 3000, 3000), Is.False);
		}

		[TestCase(0f, 0f, 3400, 3400, true)]
		[TestCase(0.01f, 0f, 0, 3400, false)]
		[TestCase(0f, 0f, 3401, 3400, false)]
		public void DropSafetyIncludesBothStoppingAaAndGroundDefense(float danger, float maximumDanger,
			int defenders, int maximumDefenders, bool expected)
		{
			Assert.That(HeavyDropPolicy.IsDropSiteSafe(danger, maximumDanger,
				defenders, maximumDefenders), Is.EqualTo(expected));
		}

		[TestCase(3, 0, 3)]
		[TestCase(3, 2, 1)]
		[TestCase(3, 3, 0)]
		[TestCase(3, 5, 0)]
		public void BoardingConcurrencyNeverExceedsItsBound(int limit, int active, int expected)
		{
			Assert.That(HeavyDropPolicy.AvailableBoardingSlots(limit, active), Is.EqualTo(expected));
		}

		[Test]
		public void TargetScoreRewardsValueAndBehindPositionButPenalizesDefenseAndDistance()
		{
			var baseline = HeavyDropPolicy.TargetScore(2000, 1000, 50, 0);
			Assert.That(HeavyDropPolicy.TargetScore(3000, 1000, 50, 0), Is.GreaterThan(baseline));
			Assert.That(HeavyDropPolicy.TargetScore(2000, 2000, 50, 0), Is.LessThan(baseline));
			Assert.That(HeavyDropPolicy.TargetScore(2000, 1000, 100, 0), Is.LessThan(baseline));
			Assert.That(HeavyDropPolicy.TargetScore(2000, 1000, 50, 2), Is.GreaterThan(baseline));
		}
	}
}
