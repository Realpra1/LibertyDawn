#region Copyright & License Information
/* Copyright 2007-2021 The OpenRA Developers (see AUTHORS) */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class GeneralizedCombatThreatCalculatorTest
	{
		[TestCase(512, 0.5)]
		[TestCase(1024, 1)]
		[TestCase(1536, 1.5)]
		public void ConvertsWorldDistanceToFractionalCells(int length, double cells)
		{
			Assert.That(GeneralizedCombatThreatCalculator.Cells(new WDist(length)), Is.EqualTo(cells));
		}

		[Test]
		public void ExpectedHitChanceUsesHitOrSplashRadiusAndIsBounded()
		{
			Assert.That(GeneralizedCombatThreatCalculator.ExpectedHitChance(0.5, 2), Is.EqualTo(0.25));
			Assert.That(GeneralizedCombatThreatCalculator.ExpectedHitChance(3, 2), Is.EqualTo(1));
			Assert.That(GeneralizedCombatThreatCalculator.ExpectedHitChance(0.25, 0), Is.EqualTo(1));
		}

		[Test]
		public void SplashUsesWeightedNonOverlappingAnnuliWithExactSubCellInnerRadius()
		{
			var zones = new[]
			{
				new GeneralizedCombatThreatCalculator.SplashZone(0, 0.5, 1, 1),
				new GeneralizedCombatThreatCalculator.SplashZone(0.5, 1.5, 1, 0.5),
				new GeneralizedCombatThreatCalculator.SplashZone(1.5, 3, 0.5, 0)
			};

			// Exact radius-weighted interpolation: .25 + 1.4166667 + 1.5 = 3.1666667.
			Assert.That(GeneralizedCombatThreatCalculator.SplashFactor(zones), Is.EqualTo(3.1666666667).Within(0.000001));
			Assert.That(zones[1].AffectedCells, Is.EqualTo(2).Within(0.000001));
		}

		[Test]
		public void CombinedMultiplierMultipliesRatherThanAdds()
		{
			var hit = GeneralizedCombatThreatCalculator.ExpectedHitChance(1, 2);
			var splash = GeneralizedCombatThreatCalculator.SplashFactor(new[]
			{
				new GeneralizedCombatThreatCalculator.SplashZone(0, 2, 1, 1)
			});

			Assert.That(hit * splash, Is.EqualTo(2));
		}

		[Test]
		public void ThreatEquivalentCapsDefenselessActorsAndHandlesMutuallyHarmlessActors()
		{
			Assert.That(GeneralizedCombatThreatCalculator.ThreatEquivalent(0, 0), Is.Zero);
			Assert.That(GeneralizedCombatThreatCalculator.ThreatEquivalent(0, 2), Is.Zero);
			Assert.That(GeneralizedCombatThreatCalculator.ThreatEquivalent(2, 0),
				Is.EqualTo(GeneralizedCombatThreatCalculator.MaximumThreatRating));
			Assert.That(GeneralizedCombatThreatCalculator.ThreatEquivalent(2, 4), Is.EqualTo(0.5));
			Assert.That(GeneralizedCombatThreatCalculator.ThreatEquivalent(500, 1),
				Is.EqualTo(GeneralizedCombatThreatCalculator.MaximumThreatRating));
		}

		[Test]
		public void CanonicalPairCountAndKeyDoNotRecalculateReverseMatchups()
		{
			Assert.That(GeneralizedCombatThreatCalculator.CanonicalPairCount(17), Is.EqualTo(153));
			Assert.That(GeneralizedCombatThreatCalculator.CanonicalKey("ORCA", "mtnk"),
				Is.EqualTo(("mtnk", "orca")));
			Assert.That(GeneralizedCombatThreatCalculator.CanonicalKey("mtnk", "ORCA"),
				Is.EqualTo(("mtnk", "orca")));
		}

		[TestCase(-1, 1)]
		[TestCase(0, 1)]
		[TestCase(1, 1.25)]
		[TestCase(2, 1.5625)]
		[TestCase(3, 2.44)]
		[TestCase(99, 2.44)]
		public void CachedVeterancyUsesSimpleBoundedFactors(int level, double expected)
		{
			Assert.That(GeneralizedCombatThreatCalculator.VeterancyFactor(level), Is.EqualTo(expected));
		}

		[Test]
		public void CachedVeterancyScalesRelativeExchangeAndEqualRanksCancel()
		{
			Assert.That(GeneralizedCombatThreatCalculator.ScaleCachedExchange(4, 1.5625, 1), Is.EqualTo(6.25));
			Assert.That(GeneralizedCombatThreatCalculator.ScaleCachedExchange(4, 2.44, 2.44), Is.EqualTo(4));
			Assert.That(GeneralizedCombatThreatCalculator.ScaleCachedExchange(4, 1.25, 2.44),
				Is.EqualTo(4 * 1.25 / 2.44).Within(0.000001));
		}

		[Test]
		public void EffectiveRangeFindsProjectileFlightAndTargetDisplacementIntersection()
		{
			var range = GeneralizedCombatThreatCalculator.EffectiveRangeCells(11, 1,
				160 / 1024d, 54 / 1024d, 1.33203125, false, false, 3);

			Assert.That(range, Is.EqualTo(3.9467593).Within(0.0001));
		}

		[Test]
		public void EffectiveRangeHonorsInstantHomingAndMinimumRangeCases()
		{
			Assert.That(GeneralizedCombatThreatCalculator.EffectiveRangeCells(3, 0,
				0, 70 / 1024d, 0.5, true, false, 11), Is.EqualTo(3));
			Assert.That(GeneralizedCombatThreatCalculator.EffectiveRangeCells(10, 2,
				50 / 1024d, 100 / 1024d, 0.5, false, true, 4), Is.EqualTo(4));
			Assert.That(GeneralizedCombatThreatCalculator.EffectiveRangeCells(35, 4,
				50 / 1024d, 100 / 1024d, 0.5, false, false, 3), Is.Zero);
		}

		[Test]
		public void EachRangeFactorUsesEnemyEffectiveRangeOverOwnBaseRangeIndependently()
		{
			Assert.That(GeneralizedCombatThreatCalculator.EffectiveRangeFactor(3, 35),
				Is.EqualTo(3d / 35).Within(0.000001));
			Assert.That(GeneralizedCombatThreatCalculator.EffectiveRangeFactor(0, 3), Is.Zero);
			Assert.That(GeneralizedCombatThreatCalculator.EffectiveRangeFactor(4, 0), Is.Zero);
		}

		[Test]
		public void RangeFactorScalesEachRawExchangeScoreExactlyOnce()
		{
			Assert.That(GeneralizedCombatThreatCalculator.RangeAdjustedThreatEquivalent(4, 2, 3, 35),
				Is.EqualTo(2 * 3d / 35).Within(0.000001));
			Assert.That(GeneralizedCombatThreatCalculator.RangeAdjustedThreatEquivalent(4, 2, 0, 3), Is.Zero);
			Assert.That(GeneralizedCombatThreatCalculator.RangeAdjustedThreatEquivalent(4, 0, 3, 0),
				Is.EqualTo(GeneralizedCombatThreatCalculator.MaximumThreatRating));
		}

		[Test]
		public void HealingAdjustedTimeToKillHandlesContinuousAndThresholdHealing()
		{
			var continuous = new[] { new GeneralizedCombatThreatCalculator.HealingProfile(2, 100) };
			Assert.That(GeneralizedCombatThreatCalculator.HealingAdjustedTimeToKill(100, 10, continuous),
				Is.EqualTo(12.5));

			var belowHalf = new[] { new GeneralizedCombatThreatCalculator.HealingProfile(5, 50) };
			Assert.That(GeneralizedCombatThreatCalculator.HealingAdjustedTimeToKill(100, 10, belowHalf),
				Is.EqualTo(15));
			Assert.That(GeneralizedCombatThreatCalculator.HealingAdjustedTimeToKill(100, 5, belowHalf),
				Is.EqualTo(double.PositiveInfinity));
		}

		[Test]
		public void CachedAircraftUseAirborneTargetTypeInsteadOfConditionalGroundUnion()
		{
			var types = GeneralizedCombatThreatCalculator.CachedTargetTypes(true, new[]
			{
				new BitSet<TargetableType>("Ground", "Vehicle"),
				new BitSet<TargetableType>("Air")
			});

			Assert.That(types.SetEquals(new BitSet<TargetableType>("Air")), Is.True);
		}

		[Test]
		public void CrossoverEstimateFindsRifleGuardTowerBoundaryInConstantEvaluations()
		{
			const double rifleThreatToTower = 1d / 2100;
			var result = GeneralizedCombatThreatCalculator.FindCrossover(rifleThreatToTower, count =>
				new GeneralizedCombatThreatCalculator.GroupThreat(count,
					2100d / (count * count), rifleThreatToTower * count * count));

			Assert.That(result.Found, Is.True);
			Assert.That(result.InitialEstimate, Is.EqualTo(46));
			Assert.That(result.UnitCount, Is.EqualTo(46));
			Assert.That(result.Evaluations, Is.EqualTo(2));
		}

		[Test]
		public void MixedGroupCrossoverUsesTopEconomicMassAndNineCountWeightedLookups()
		{
			var ours = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("a", 10, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("b", 2, 200),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("c", 1, 300),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("ignored-ours", 100, 1)
			};
			var theirs = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("x", 5, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("y", 3, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("z", 1, 200),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("ignored-theirs", 100, 1)
			};
			var ourFactors = new System.Collections.Generic.Dictionary<string, double>
			{
				{ "a", 1 }, { "b", 2 }, { "c", 4 }
			};
			var theirFactors = new System.Collections.Generic.Dictionary<string, double>
			{
				{ "x", 4 }, { "y", 1 }, { "z", 0.25 }
			};
			var lookups = 0;

			var crossover = GeneralizedCombatThreatCalculator.EstimateMixedGroupCrossover(ours, theirs,
				(ourType, theirType) =>
				{
					lookups++;
					return ourFactors[ourType] * theirFactors[theirType];
				});

			Assert.That(lookups, Is.EqualTo(9));
			Assert.That(crossover, Is.EqualTo(System.Math.Sqrt(558d / 156)).Within(0.000001));
		}

		[Test]
		public void MixedGroupCrossoverSupportsFewerThanThreeTypes()
		{
			var ours = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("a", 2, 100)
			};
			var theirs = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("x", 3, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("y", 1, 100)
			};
			var lookups = 0;

			var crossover = GeneralizedCombatThreatCalculator.EstimateMixedGroupCrossover(ours, theirs,
				(ourType, theirType) =>
				{
					lookups++;
					return theirType == "x" ? 4 : 1;
				});

			Assert.That(lookups, Is.EqualTo(2));
			Assert.That(crossover, Is.EqualTo(System.Math.Sqrt(13d / 4)).Within(0.000001));
		}

		[Test]
		public void ShouldEngageUsesSafetyFactorAndMinimumEnemyShare()
		{
			var tenOurs = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("a", 10, 100)
			};
			var fifteenOurs = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("a", 15, 100)
			};
			var tenTheirs = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("x", 10, 100)
			};

			Assert.That(GeneralizedCombatThreatCalculator.ShouldEngageMixedGroups(tenOurs, tenTheirs,
				(ourType, theirType) => 1), Is.False);
			Assert.That(GeneralizedCombatThreatCalculator.ShouldEngageMixedGroups(fifteenOurs, tenTheirs,
				(ourType, theirType) => 1), Is.True);
			Assert.That(GeneralizedCombatThreatCalculator.ShouldEngageMixedGroups(tenOurs, tenTheirs,
				(ourType, theirType) => 1, safetyFactor: 1), Is.True);

			var oneOur = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("a", 1, 100)
			};
			var twentyTheirs = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("x", 20, 100)
			};
			Assert.That(GeneralizedCombatThreatCalculator.ShouldEngageMixedGroups(oneOur, twentyTheirs,
				(ourType, theirType) => 0.0001), Is.False);
			Assert.That(GeneralizedCombatThreatCalculator.ShouldEngageMixedGroups(tenOurs, twentyTheirs,
				(ourType, theirType) => 0.0001), Is.True);
		}
	}
}
