#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * version 3 or later.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RedTiberiumBombPolicyTest
	{
		[TestCase(20, 1500, true)]
		[TestCase(10, 1500, false)]
		[TestCase(10, 3000, true)]
		public void FractionalLaunchRateDoesNotRoundUpSmallArmies(int harvesters, int elapsedTicks, bool expected)
		{
			var budget = RedTiberiumBombPolicy.AccrueLaunchBudget(0, harvesters, elapsedTicks, 5, 1500, 1);
			Assert.That(RedTiberiumBombPolicy.CanLaunch(budget, 1500), Is.EqualTo(expected));
		}

		[Test]
		public void StoredBudgetIsBoundedAndSpentExactlyOnce()
		{
			var budget = RedTiberiumBombPolicy.AccrueLaunchBudget(0, 75, 6000, 5, 1500, 1);
			Assert.That(budget, Is.EqualTo(RedTiberiumBombPolicy.LaunchCost(1500)));
			Assert.That(RedTiberiumBombPolicy.SpendLaunch(budget, 1500), Is.Zero);
		}

		[Test]
		public void TargetScoreCombinesConfiguredPriorityAndEconomicValue()
		{
			Assert.That(RedTiberiumBombPolicy.TargetScore(10000, 4000), Is.EqualTo(40000000));
			Assert.That(RedTiberiumBombPolicy.TargetScore(-1, 4000), Is.Zero);
		}

		[Test]
		public void StallPolicyRequiresNoProgressForWholeInterval()
		{
			Assert.That(RedTiberiumBombPolicy.MadeProgress(99, 100), Is.True);
			Assert.That(RedTiberiumBombPolicy.HasStalled(499, 250, 250), Is.False);
			Assert.That(RedTiberiumBombPolicy.HasStalled(500, 250, 250), Is.True);
		}
	}
}
