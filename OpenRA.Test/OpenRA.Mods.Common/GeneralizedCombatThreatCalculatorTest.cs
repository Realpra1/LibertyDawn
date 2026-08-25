#region Copyright & License Information
/* Copyright 2007-2021 The OpenRA Developers (see AUTHORS) */
#endregion

using System.Linq;
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
		public void InfantrySplashUsesSubCellFalloffForAnExpectedCellmate()
		{
			var zones = new[]
			{
				new GeneralizedCombatThreatCalculator.SplashZone(0, 0.5, 1, 0)
			};
			var offsets = new[] { new WVec(0, 0, 0), new WVec(512, 0, 0) };

			// The second infantry is 0.5 cells away. Its 0.125-cell hit radius
			// leaves 0.375 cells of falloff distance, giving 25% splash damage.
			Assert.That(GeneralizedCombatThreatCalculator.InfantrySplashFactor(zones, 0.125, offsets),
				Is.EqualTo(1.125).Within(0.000001));
		}

		[Test]
		public void InfantrySplashUsesOneAndAHalfTargetsInOtherAffectedCells()
		{
			var zones = new[]
			{
				new GeneralizedCombatThreatCalculator.SplashZone(0, 2, 1, 1)
			};

			// Four affected cells contain the primary target plus three other cells.
			Assert.That(GeneralizedCombatThreatCalculator.InfantrySplashFactor(
				zones, 0.125, new[] { WVec.Zero }), Is.EqualTo(5.5));
		}

		[Test]
		public void ExpectedShotDamageCapsThePrimaryUnitButPreservesSplash()
		{
			Assert.That(GeneralizedCombatThreatCalculator.ExpectedShotDamage(6000, new[]
			{
				new GeneralizedCombatThreatCalculator.ShotDamageProfile(45000, 1, 1)
			}), Is.EqualTo(6000));

			Assert.That(GeneralizedCombatThreatCalculator.ExpectedShotDamage(6000, new[]
			{
				new GeneralizedCombatThreatCalculator.ShotDamageProfile(4000, 1, 1),
				new GeneralizedCombatThreatCalculator.ShotDamageProfile(4000, 1, 2)
			}), Is.EqualTo(10000));
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

		[TestCase(0, 0)]
		[TestCase(1, 1)]
		[TestCase(2, 3)]
		[TestCase(10, 55)]
		public void DiscreteCombatPotentialSumsSurvivingActorCounts(double actorCount, double expected)
		{
			Assert.That(GeneralizedCombatThreatCalculator.DiscreteCombatPotential(actorCount), Is.EqualTo(expected));
			Assert.That(GeneralizedCombatThreatCalculator.ActorCountForDiscreteCombatPotential(expected),
				Is.EqualTo(actorCount).Within(0.000001));
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
		public void CachedAmmoUsesFullMagazineThenReloadLimitedDamage()
		{
			var ammo = GeneralizedCombatThreatCalculator.CalculateAmmoDamageProfile(new[]
			{
				new GeneralizedCombatThreatCalculator.AmmoArmamentProfile(250, 0.1, "primary")
			}, new[]
			{
				new GeneralizedCombatThreatCalculator.AmmoPoolProfile("primary", 20, 2d / 105)
			});

			Assert.That(ammo.FullDamagePerTick, Is.EqualTo(250));
			Assert.That(ammo.FullAmmoTicks, Is.EqualTo(4200d / 17).Within(0.000001));
			Assert.That(ammo.ReloadingDamagePerTick, Is.EqualTo(1000d / 21).Within(0.000001));
			Assert.That(GeneralizedCombatThreatCalculator.AmmoAdjustedTimeToKill(4500, ammo,
				System.Array.Empty<GeneralizedCombatThreatCalculator.HealingProfile>()), Is.EqualTo(18));
			Assert.That(GeneralizedCombatThreatCalculator.AmmoAdjustedTimeToKill(100000, ammo,
				System.Array.Empty<GeneralizedCombatThreatCalculator.HealingProfile>()), Is.EqualTo(1050).Within(0.000001));
		}

		[Test]
		public void CachedAmmoWithoutReloadCannotDamagePastMagazineCapacity()
		{
			var ammo = GeneralizedCombatThreatCalculator.CalculateAmmoDamageProfile(new[]
			{
				new GeneralizedCombatThreatCalculator.AmmoArmamentProfile(10, 1, "primary")
			}, new[]
			{
				new GeneralizedCombatThreatCalculator.AmmoPoolProfile("primary", 5, 0)
			});

			Assert.That(ammo.FullAmmoTicks, Is.EqualTo(5));
			Assert.That(ammo.ReloadingDamagePerTick, Is.Zero);
			Assert.That(GeneralizedCombatThreatCalculator.AmmoAdjustedTimeToKill(50, ammo,
				System.Array.Empty<GeneralizedCombatThreatCalculator.HealingProfile>()), Is.EqualTo(5));
			Assert.That(GeneralizedCombatThreatCalculator.AmmoAdjustedTimeToKill(51, ammo,
				System.Array.Empty<GeneralizedCombatThreatCalculator.HealingProfile>()), Is.EqualTo(double.PositiveInfinity));
		}

		[Test]
		public void CachedAmmoLeavesUnlimitedAndSelfSustainingArmamentsAtFullDamage()
		{
			var ammo = GeneralizedCombatThreatCalculator.CalculateAmmoDamageProfile(new[]
			{
				new GeneralizedCombatThreatCalculator.AmmoArmamentProfile(20, 0),
				new GeneralizedCombatThreatCalculator.AmmoArmamentProfile(30, 0.5, "primary")
			}, new[]
			{
				new GeneralizedCombatThreatCalculator.AmmoPoolProfile("primary", 4, 0.5)
			});

			Assert.That(ammo.FullDamagePerTick, Is.EqualTo(50));
			Assert.That(ammo.FullAmmoTicks, Is.EqualTo(double.PositiveInfinity));
			Assert.That(ammo.ReloadingDamagePerTick, Is.EqualTo(50));
		}

		[Test]
		public void CachedAmmoAggregatesArmamentsSharingOnePool()
		{
			var ammo = GeneralizedCombatThreatCalculator.CalculateAmmoDamageProfile(new[]
			{
				new GeneralizedCombatThreatCalculator.AmmoArmamentProfile(40, 0.5, "primary"),
				new GeneralizedCombatThreatCalculator.AmmoArmamentProfile(20, 0.25, "primary")
			}, new[]
			{
				new GeneralizedCombatThreatCalculator.AmmoPoolProfile("primary", 15, 0.25)
			});

			Assert.That(ammo.FullDamagePerTick, Is.EqualTo(60));
			Assert.That(ammo.FullAmmoTicks, Is.EqualTo(30));
			Assert.That(ammo.ReloadingDamagePerTick, Is.EqualTo(20));
		}

		[Test]
		public void CachedAmmoCarriesRemainingHealthAndHealingIntoReloadPhase()
		{
			var ammo = new GeneralizedCombatThreatCalculator.AmmoDamageProfile(10, 5, 6);
			var healing = new[] { new GeneralizedCombatThreatCalculator.HealingProfile(2, 50) };

			Assert.That(GeneralizedCombatThreatCalculator.AmmoAdjustedTimeToKill(100, ammo, healing),
				Is.EqualTo(17.5));
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
		public void CrossoverPercentageSearchFindsExactDiscreteEstimate()
		{
			const double rifleThreatToTower = 1d / 2100;
			var result = GeneralizedCombatThreatCalculator.FindCrossover(rifleThreatToTower, count =>
			{
				var potential = GeneralizedCombatThreatCalculator.DiscreteCombatPotential(count);
				return new GeneralizedCombatThreatCalculator.GroupThreat(count,
					2100d / potential, rifleThreatToTower * potential);
			});

			Assert.That(result.Found, Is.True);
			Assert.That(result.InitialEstimate, Is.EqualTo(65));
			Assert.That(result.UnitCount, Is.EqualTo(65));
			Assert.That(result.Evaluations, Is.EqualTo(5));
		}

		[Test]
		public void CrossoverEstimateBiasCanCorrectAHighDiscreteEstimate()
		{
			const double unroundedEstimate = 100.1;
			var baseThreat = 1 / GeneralizedCombatThreatCalculator.DiscreteCombatPotential(unroundedEstimate);
			var result = GeneralizedCombatThreatCalculator.FindCrossover(baseThreat, count =>
				new GeneralizedCombatThreatCalculator.GroupThreat(count,
					count >= 100 ? 0.9 : 1.1, count >= 100 ? 1.1 : 0.9));

			Assert.That(result.Found, Is.True);
			Assert.That(result.InitialEstimate, Is.EqualTo(100));
			Assert.That(result.UnitCount, Is.EqualTo(100));
		}

		[Test]
		public void CrossoverPercentageSearchBracketsOverestimateBeforeBinarySearch()
		{
			const double baseThreat = 1d / 2100;
			var result = GeneralizedCombatThreatCalculator.FindCrossover(baseThreat, count =>
				new GeneralizedCombatThreatCalculator.GroupThreat(count,
					count >= 38 ? 0.9 : 1.1, count >= 38 ? 1.1 : 0.9));

			Assert.That(result.Found, Is.True);
			Assert.That(result.InitialEstimate, Is.EqualTo(65));
			Assert.That(result.UnitCount, Is.EqualTo(38));
			Assert.That(result.Evaluations, Is.EqualTo(9));
		}

		[Test]
		public void CrossoverPercentageSearchExpandsUnderestimateBeforeBinarySearch()
		{
			const double baseThreat = 1d / 55;
			var result = GeneralizedCombatThreatCalculator.FindCrossover(baseThreat, count =>
				new GeneralizedCombatThreatCalculator.GroupThreat(count,
					count >= 38 ? 0.9 : 1.1, count >= 38 ? 1.1 : 0.9));

			Assert.That(result.Found, Is.True);
			Assert.That(result.InitialEstimate, Is.EqualTo(10));
			Assert.That(result.UnitCount, Is.EqualTo(38));
			Assert.That(result.Evaluations, Is.EqualTo(16));
		}

		[Test]
		public void CrossoverPercentageSearchAdvancesByAtLeastOneFromOne()
		{
			var evaluated = new System.Collections.Generic.List<int>();
			var result = GeneralizedCombatThreatCalculator.FindCrossover(1, count =>
			{
				evaluated.Add(count);
				return new GeneralizedCombatThreatCalculator.GroupThreat(count,
					count >= 3 ? 0.9 : 1.1, count >= 3 ? 1.1 : 0.9);
			});

			Assert.That(result.Found, Is.True);
			Assert.That(result.InitialEstimate, Is.EqualTo(1));
			Assert.That(result.UnitCount, Is.EqualTo(3));
			Assert.That(evaluated, Is.EqualTo(new[] { 1, 2, 3 }));
		}

		[Test]
		public void CrossoverPercentageSearchStopsAtMaximumWithoutCrossover()
		{
			var result = GeneralizedCombatThreatCalculator.FindCrossover(1, count =>
				new GeneralizedCombatThreatCalculator.GroupThreat(count, 1.1, 0.9), 100);

			Assert.That(result.Found, Is.False);
			Assert.That(result.UnitCount, Is.EqualTo(100));
			Assert.That(result.Evaluations, Is.EqualTo(34));
		}

		[Test]
		public void CrossoverWithoutBaseThreatStartsAtMaximum()
		{
			var result = GeneralizedCombatThreatCalculator.FindCrossover(0, count =>
				new GeneralizedCombatThreatCalculator.GroupThreat(count, 1.1, 0.9), 100);

			Assert.That(result.Found, Is.False);
			Assert.That(result.InitialEstimate, Is.EqualTo(100));
			Assert.That(result.UnitCount, Is.EqualTo(100));
			Assert.That(result.Evaluations, Is.EqualTo(1));
		}

		[Test]
		public void CrossoverWithoutBaseThreatChecksMaximumThenRestartsFromTunedEstimate()
		{
			var evaluated = new System.Collections.Generic.List<int>();
			var result = GeneralizedCombatThreatCalculator.FindCrossover(0, count =>
			{
				evaluated.Add(count);
				return new GeneralizedCombatThreatCalculator.GroupThreat(count,
					count >= 38 ? 0.9 : 1.1, count >= 38 ? 1.1 : 0.9);
			}, 100);

			Assert.That(result.Found, Is.True);
			Assert.That(result.InitialEstimate, Is.EqualTo(100));
			Assert.That(result.UnitCount, Is.EqualTo(38));
			Assert.That(result.Evaluations, Is.EqualTo(8));
			Assert.That(evaluated, Is.EqualTo(new[] { 100, 26, 29, 32, 35, 38, 36, 37 }));
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
			var threat = 558d / 156;
			var requiredPotential = threat * GeneralizedCombatThreatCalculator.DiscreteCombatPotential(109);
			var expected = GeneralizedCombatThreatCalculator.ActorCountForDiscreteCombatPotential(requiredPotential) / 109;
			Assert.That(crossover, Is.EqualTo(expected).Within(0.000001));
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
			var threat = 13d / 4;
			var requiredPotential = threat * GeneralizedCombatThreatCalculator.DiscreteCombatPotential(4);
			var expected = GeneralizedCombatThreatCalculator.ActorCountForDiscreteCombatPotential(requiredPotential) / 4;
			Assert.That(crossover, Is.EqualTo(expected).Within(0.000001));
		}

		[Test]
		public void MixedGroupCrossoverAdaptsEnemyTypesToNineLookupBudget()
		{
			var twoOurs = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("a", 2, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("b", 1, 100)
			};
			var fiveTheirs = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("v", 5, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("w", 4, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("x", 3, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("y", 2, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("z", 1, 100)
			};
			var lookups = 0;
			GeneralizedCombatThreatCalculator.EstimateMixedGroupCrossover(twoOurs, fiveTheirs,
				(ourType, theirType) => { lookups++; return 1; });
			Assert.That(lookups, Is.EqualTo(8));

			var oneOur = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("a", 1, 100)
			};
			var tenTheirs = System.Linq.Enumerable.Range(0, 10)
				.Select(i => new GeneralizedCombatThreatCalculator.GroupTypeCount("enemy-" + i, 10 - i, 100))
				.ToArray();
			lookups = 0;
			GeneralizedCombatThreatCalculator.EstimateMixedGroupCrossover(oneOur, tenTheirs,
				(ourType, theirType) => { lookups++; return 1; });
			Assert.That(lookups, Is.EqualTo(9));
		}

		[Test]
		public void ShouldEngageUsesSafetyFactorWithoutAnEnemyNumberFloor()
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
				(ourType, theirType) => 0), Is.True);
		}

		[Test]
		public void ShouldEngageRequiresFiveTimesOmittedEnemyEconomicMass()
		{
			var ours = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("ours-a", 2, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("ours-b", 1, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("ours-c", 1, 100)
			};
			var theirs = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("large", 10, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("medium", 8, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("small", 6, 100),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("hidden-counter", 1, 100)
			};

			Assert.That(GeneralizedCombatThreatCalculator.ShouldEngageMixedGroups(ours, theirs,
				(ourType, theirType) => 0.0001), Is.False);
			Assert.That(GeneralizedCombatThreatCalculator.ShouldEngageMixedGroups(ours, theirs,
				(ourType, theirType) => 0.0001, omittedEconomicMassFactor: 4), Is.True);
		}

		[Test]
		public void OneTypeAttackerIncludesGuardTowerFourthEnemyType()
		{
			var theirs = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("e3", 8, 250),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("bike", 4, 500),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("mtnk", 3, 800),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("gtwr", 1, 600)
			};
			var ratings = new System.Collections.Generic.Dictionary<string, double>
			{
				{ "e3", 0.127273 }, { "bike", 3.428571 }, { "mtnk", 85.25 }, { "gtwr", 200 }
			};
			var lookups = 0;
			bool EngageWithRifles(int count) => GeneralizedCombatThreatCalculator.ShouldEngageMixedGroups(
				new[] { new GeneralizedCombatThreatCalculator.GroupTypeCount("e1", count, 120) }, theirs,
				(ourType, theirType) => { lookups++; return ratings[theirType]; });

			Assert.That(EngageWithRifles(133), Is.False);
			Assert.That(EngageWithRifles(134), Is.True);
			Assert.That(lookups, Is.EqualTo(8));
		}
	}
}
