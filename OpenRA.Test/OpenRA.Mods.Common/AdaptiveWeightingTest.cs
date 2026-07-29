#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class AdaptiveWeightingTest
	{
		[TestCase(TestName = "Minute score floors losses at 1, not 0")]
		public void MinuteScoreFloorsLosses()
		{
			Assert.That(AdaptiveWeighting.MinuteScore(500, 0), Is.EqualTo(500.0));
			Assert.That(AdaptiveWeighting.MinuteScore(0, 0), Is.EqualTo(0.0));
			Assert.That(AdaptiveWeighting.MinuteScore(100, 200), Is.EqualTo(0.5));
		}

		[TestCase(TestName = "A window with no kills and no losses leaves the decayed score untouched")]
		public void DecayScoreSkipsEmptyWindow()
		{
			Assert.That(AdaptiveWeighting.DecayScore(1.3, 0, 0, 0.5), Is.EqualTo(1.3));
		}

		[TestCase(TestName = "A window with evidence blends toward the observation by the configured weight")]
		public void DecayScoreBlends()
		{
			// previous 1.0, observed 100/50=2.0, blended 50/50 -> 1.5
			Assert.That(AdaptiveWeighting.DecayScore(1.0, 100, 50, 0.5), Is.EqualTo(1.5));

			// A small blend weight moves the score only a little.
			var moved = AdaptiveWeighting.DecayScore(1.0, 100, 50, 0.1);
			Assert.That(moved, Is.EqualTo(1.1).Within(1e-9));
		}

		[TestCase(TestName = "Confidence ramps linearly from 0 to 1 and never overshoots")]
		public void ConfidenceRamp()
		{
			Assert.That(AdaptiveWeighting.Confidence(0, 10), Is.EqualTo(0.0));
			Assert.That(AdaptiveWeighting.Confidence(5, 10), Is.EqualTo(0.5));
			Assert.That(AdaptiveWeighting.Confidence(10, 10), Is.EqualTo(1.0));
			Assert.That(AdaptiveWeighting.Confidence(1000, 10), Is.EqualTo(1.0));
		}

		[TestCase(TestName = "Confidence with a zero threshold is always fully trusted")]
		public void ConfidenceZeroThreshold()
		{
			Assert.That(AdaptiveWeighting.Confidence(0, 0), Is.EqualTo(1.0));
		}

		[TestCase(TestName = "Adapted weight at zero confidence equals the authored weight, regardless of score")]
		public void AdaptedWeightColdStart()
		{
			Assert.That(AdaptiveWeighting.AdaptedWeight(40, 0.0, 0.0), Is.EqualTo(40.0));
			Assert.That(AdaptiveWeighting.AdaptedWeight(40, 5.0, 0.0), Is.EqualTo(40.0));
		}

		[TestCase(TestName = "Adapted weight never goes negative even at full confidence and zero score")]
		public void AdaptedWeightNeverNegative()
		{
			Assert.That(AdaptiveWeighting.AdaptedWeight(40, 0.0, 1.0), Is.EqualTo(0.0));
		}

		[TestCase(TestName = "Adapted weight above break-even score increases the authored weight")]
		public void AdaptedWeightBoost()
		{
			// score 2.0 (paid for itself twice over), full confidence -> weight doubles.
			Assert.That(AdaptiveWeighting.AdaptedWeight(40, 2.0, 1.0), Is.EqualTo(80.0));
		}

		[TestCase(TestName = "Clamped shares of a single buildable type is always 100%")]
		public void ClampedSharesSingleType()
		{
			var shares = AdaptiveWeighting.ClampedShares(new Dictionary<string, double> { { "e1", 999 } }, 0.01, 0.5);
			Assert.That(shares["e1"], Is.EqualTo(1.0));
		}

		[TestCase(TestName = "Clamped shares of an empty set is empty")]
		public void ClampedSharesEmpty()
		{
			var shares = AdaptiveWeighting.ClampedShares(new Dictionary<string, double>(), 0.01, 0.5);
			Assert.That(shares, Is.Empty);
		}

		[TestCase(TestName = "Clamped shares sum to 1 and respect floor/ceiling after redistribution")]
		public void ClampedSharesRedistribute()
		{
			// Raw weights 90/5/5 -> unclamped shares 0.9/0.05/0.05. Ceiling of 0.5 clamps "a" down,
			// and the 0.4 that frees up should split evenly between "b" and "c" (they were tied).
			var weights = new Dictionary<string, double> { { "a", 90 }, { "b", 5 }, { "c", 5 } };
			var shares = AdaptiveWeighting.ClampedShares(weights, 0.01, 0.5);

			Assert.That(shares["a"], Is.EqualTo(0.5).Within(1e-9));
			Assert.That(shares["b"], Is.EqualTo(0.25).Within(1e-9));
			Assert.That(shares["c"], Is.EqualTo(0.25).Within(1e-9));

			var sum = shares["a"] + shares["b"] + shares["c"];
			Assert.That(sum, Is.EqualTo(1.0).Within(1e-9));
		}

		[TestCase(TestName = "Clamped shares raise a starved type up to the floor")]
		public void ClampedSharesFloor()
		{
			// "c" is almost never picked (weight 0.1 out of ~200) - the floor must still guarantee it 1%.
			var weights = new Dictionary<string, double> { { "a", 100 }, { "b", 100 }, { "c", 0.1 } };
			var shares = AdaptiveWeighting.ClampedShares(weights, 0.01, 0.5);

			Assert.That(shares["c"], Is.EqualTo(0.01).Within(1e-9));
			Assert.That(shares["a"] + shares["b"] + shares["c"], Is.EqualTo(1.0).Within(1e-9));
		}

		[TestCase(TestName = "Weighted pick walks the cumulative distribution in sorted key order")]
		public void WeightedPickWalksCumulative()
		{
			var shares = new Dictionary<string, double> { { "a", 0.5 }, { "b", 0.3 }, { "c", 0.2 } };

			Assert.That(AdaptiveWeighting.WeightedPick(shares, 0.0f), Is.EqualTo("a"));
			Assert.That(AdaptiveWeighting.WeightedPick(shares, 0.49f), Is.EqualTo("a"));
			Assert.That(AdaptiveWeighting.WeightedPick(shares, 0.51f), Is.EqualTo("b"));
			Assert.That(AdaptiveWeighting.WeightedPick(shares, 0.79f), Is.EqualTo("b"));
			Assert.That(AdaptiveWeighting.WeightedPick(shares, 0.81f), Is.EqualTo("c"));
			Assert.That(AdaptiveWeighting.WeightedPick(shares, 0.999999f), Is.EqualTo("c"));
		}

		[TestCase(TestName = "Weighted pick on an empty distribution returns null")]
		public void WeightedPickEmpty()
		{
			Assert.That(AdaptiveWeighting.WeightedPick(new Dictionary<string, double>(), 0.5f), Is.Null);
		}

		[TestCase(TestName = "Economy gate forces economy when spend outpaces income")]
		public void EconomyGateSpendOutpacesIncome()
		{
			Assert.That(AdaptiveWeighting.ShouldForceEconomy(100, 500, 5, 1), Is.True);
			Assert.That(AdaptiveWeighting.ShouldForceEconomy(500, 100, 5, 1), Is.False);
		}

		[TestCase(TestName = "Economy gate forces economy when harvester count is below the floor")]
		public void EconomyGateHarvesterFloor()
		{
			Assert.That(AdaptiveWeighting.ShouldForceEconomy(1000, 100, 0, 1), Is.True);
			Assert.That(AdaptiveWeighting.ShouldForceEconomy(1000, 100, 1, 1), Is.False);
		}

		[TestCase(TestName = "MCV priority forces below the deployed-construction-yard threshold")]
		public void McvPriorityThreshold()
		{
			Assert.That(AdaptiveWeighting.ShouldForceMcv(0, 2), Is.True);
			Assert.That(AdaptiveWeighting.ShouldForceMcv(1, 2), Is.True);
			Assert.That(AdaptiveWeighting.ShouldForceMcv(2, 2), Is.False);
		}
	}
}
