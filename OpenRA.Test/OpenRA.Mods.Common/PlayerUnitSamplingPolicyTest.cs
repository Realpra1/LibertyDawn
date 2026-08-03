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

namespace OpenRA.Test
{
	[TestFixture]
	public class PlayerUnitSamplingPolicyTest
	{
		[Test]
		public void OnlyEligibleHumanBuiltMobileUnitsAreLearned()
		{
			Assert.That(PlayerUnitSamplingPolicy.CanLearn(false, true, true, false, true, true, 1), Is.True);
			Assert.That(PlayerUnitSamplingPolicy.CanLearn(true, true, true, false, true, true, 1), Is.False);
			Assert.That(PlayerUnitSamplingPolicy.CanLearn(false, true, true, false, true, false, 1), Is.False);
			Assert.That(PlayerUnitSamplingPolicy.CanLearn(false, true, true, false, false, true, 1), Is.False);
		}

		[Test]
		public void EachLearnedTypeStartsWithFivePercentSelectionChance()
		{
			var chances = new Dictionary<string, double> { { "alpha", .05 }, { "beta", .05 } };
			Assert.That(PlayerUnitSamplingPolicy.Pick(chances, .5, .01), Is.EqualTo("alpha"));
			Assert.That(PlayerUnitSamplingPolicy.Pick(chances, .5, .06), Is.EqualTo("beta"));
			Assert.That(PlayerUnitSamplingPolicy.Pick(chances, .5, .11), Is.Null);
		}

		[Test]
		public void CombinedLearnedChanceIsBounded()
		{
			var chances = new Dictionary<string, double>();
			for (var i = 0; i < 20; i++)
				chances.Add(i.ToString(), .05);

			Assert.That(PlayerUnitSamplingPolicy.Pick(chances, .5, .49), Is.Not.Null);
			Assert.That(PlayerUnitSamplingPolicy.Pick(chances, .5, .51), Is.Null);
		}
	}
}
