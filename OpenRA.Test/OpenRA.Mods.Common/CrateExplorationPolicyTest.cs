#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * version 3 or later.
 */
#endregion

using System.Collections.Generic;
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
				new HashSet<int> { 1 });
			Assert.That(ranked, Is.EqualTo(new[] { 3, 2, 4, 0 }));
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
