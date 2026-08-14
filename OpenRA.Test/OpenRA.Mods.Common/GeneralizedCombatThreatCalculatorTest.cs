#region Copyright & License Information
/* Copyright 2007-2021 The OpenRA Developers (see AUTHORS) */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

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
		public void ThreatEquivalentHandlesDefenselessAndMutuallyHarmlessActors()
		{
			Assert.That(GeneralizedCombatThreatCalculator.ThreatEquivalent(0, 0), Is.Zero);
			Assert.That(GeneralizedCombatThreatCalculator.ThreatEquivalent(0, 2), Is.Zero);
			Assert.That(GeneralizedCombatThreatCalculator.ThreatEquivalent(2, 0), Is.EqualTo(double.PositiveInfinity));
			Assert.That(GeneralizedCombatThreatCalculator.ThreatEquivalent(2, 4), Is.EqualTo(0.5));
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
	}
}
