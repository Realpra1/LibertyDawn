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
	public sealed class StealthTankSquadPolicyTest
	{
		[TestCase(0, 0)]
		[TestCase(1, 0)]
		[TestCase(2, 2)]
		[TestCase(3, 2)]
		[TestCase(4, 2)]
		[TestCase(9, 4)]
		[TestCase(10, 5)]
		[TestCase(20, 10)]
		public void SpecialistReservationLeavesRoughlyHalfForOrdinaryArmies(int total, int expected)
		{
			Assert.That(StealthTankSquadPolicy.SpecialistCount(total), Is.EqualTo(expected));
		}

		[TestCase(0, 0)]
		[TestCase(1, 0)]
		[TestCase(2, 1)]
		[TestCase(3, 2)]
		[TestCase(4, 2)]
		[TestCase(10, 5)]
		public void HalfPreservingReservationNeverConsumesTheOpeningPair(int total, int expected)
		{
			Assert.That(StealthTankSquadPolicy.SpecialistCount(total, false), Is.EqualTo(expected));
		}

		[Test]
		public void LargeForceCreatesTwoHarassmentGroupsAndOneAttackGroup()
		{
			Assert.That(StealthTankSquadPolicy.GroupForIndex(0, 6), Is.EqualTo(0));
			Assert.That(StealthTankSquadPolicy.GroupForIndex(1, 6), Is.EqualTo(0));
			Assert.That(StealthTankSquadPolicy.GroupForIndex(2, 6), Is.EqualTo(1));
			Assert.That(StealthTankSquadPolicy.GroupForIndex(3, 6), Is.EqualTo(1));
			Assert.That(StealthTankSquadPolicy.GroupForIndex(4, 6), Is.EqualTo(2));
			Assert.That(StealthTankSquadPolicy.GroupForIndex(5, 6), Is.EqualTo(2));
			Assert.That(StealthTankSquadPolicy.RoleForGroup(2), Is.EqualTo(StealthTankSquadRole.Attack));
		}

		[Test]
		public void ChemicalConfigurationAlwaysCreatesOneHarassmentGroup()
		{
			for (var i = 0; i < 6; i++)
				Assert.That(StealthTankSquadPolicy.GroupForIndex(i, 6, 1, false), Is.Zero);

			Assert.That(StealthTankSquadPolicy.RoleForGroup(0, 1, false),
				Is.EqualTo(StealthTankSquadRole.Harass));
		}

		[Test]
		public void ScoringRewardsValueAndIncumbencyButPenalizesDistance()
		{
			var baseline = StealthTankSquadPolicy.TargetScore(1000, 1000, 20, 100);
			Assert.That(StealthTankSquadPolicy.TargetScore(1000, 2000, 20, 100), Is.GreaterThan(baseline));
			Assert.That(StealthTankSquadPolicy.TargetScore(1000, 1000, 40, 100), Is.LessThan(baseline));
			Assert.That(StealthTankSquadPolicy.TargetScore(1000, 1000, 20, 125), Is.GreaterThan(baseline));
			Assert.That(StealthTankSquadPolicy.TargetScore(1000, 1000, 20, 100, 100, 3),
				Is.LessThan(baseline));
		}

		[Test]
		public void InfantryClusterBonusIsBoundedAndImprovesTargetScore()
		{
			Assert.That(StealthTankSquadPolicy.InfantryClusterMultiplier(0, 50, 300), Is.EqualTo(100));
			Assert.That(StealthTankSquadPolicy.InfantryClusterMultiplier(2, 50, 300), Is.EqualTo(200));
			Assert.That(StealthTankSquadPolicy.InfantryClusterMultiplier(20, 50, 300), Is.EqualTo(300));
			Assert.That(StealthTankSquadPolicy.TargetScore(1000, 100, 10, 100, 200),
				Is.GreaterThan(StealthTankSquadPolicy.TargetScore(1000, 100, 10, 100)));
		}

		[TestCase(4999, 1000, 5, false)]
		[TestCase(5000, 1000, 5, true)]
		[TestCase(10000, 0, 5, false)]
		public void DefendedAreasRequireConfiguredOvermatch(int squadValue, int defendingValue,
			int requiredRatio, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.CanCarefullyClear(squadValue, defendingValue, requiredRatio),
				Is.EqualTo(expected));
		}

		[TestCase(19, 20, 5000, 1000, 5, false)]
		[TestCase(20, 20, 4999, 1000, 5, false)]
		[TestCase(20, 20, 5000, 1000, 5, true)]
		public void DefenderClearingRequiresPatienceAndOvermatch(int scans, int requiredScans,
			int squadValue, int defendingValue, int requiredRatio, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.CanAttemptDefenderClear(scans, requiredScans,
				squadValue, defendingValue, requiredRatio), Is.EqualTo(expected));
		}

		[Test]
		public void DefenderClearingChoosesBestUnlockFromWeakestPackages()
		{
			Assert.That(StealthTankSquadPolicy.SelectDefenderClearOpportunity(
				new[] { 300, 100, 200, 50 }, new long[] { 9000, 1000, 8000, 500 }, 3), Is.EqualTo(2));
			Assert.That(StealthTankSquadPolicy.SelectDefenderClearOpportunity(
				new[] { 300, 100, 200, 50 }, new long[] { 9000, 1000, 8000, 500 }, 2), Is.EqualTo(1));
		}

		[Test]
		public void SafetyBufferDoesNotInventAThreatCapability()
		{
			Assert.That(StealthTankSquadPolicy.BufferedRange(0, 2), Is.Zero);
			Assert.That(StealthTankSquadPolicy.BufferedRange(5, 2), Is.EqualTo(7));
		}
	}
}
