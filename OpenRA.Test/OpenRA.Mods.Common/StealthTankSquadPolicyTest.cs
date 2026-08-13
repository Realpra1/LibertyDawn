#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class StealthTankSquadPolicyTest
	{
		[Test]
		public void BothProfilesUseOneSharedControlImplementation()
		{
			var profileSpecificImplementations = typeof(StealthTankSquadBotModule).Assembly.GetTypes()
				.Where(t => t != typeof(StealthTankSquadBotModule) &&
					typeof(StealthTankSquadBotModule).IsAssignableFrom(t)).ToArray();

			Assert.That(profileSpecificImplementations, Is.Empty,
				"Stealth and Chemical must remain configured instances of one control module.");
		}

		[TestCase("stealth-tank")]
		[TestCase("chemical")]
		public void BothProfilesUseTheSameLifecycleContract(string profile)
		{
			Assert.That(profile, Is.Not.Empty);
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(true, false,
				false, false, false, 149, 75, 75), Is.EqualTo(StealthTankPlanInvalidation.None));
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(true, false,
				false, false, true, 149, 75, 75), Is.EqualTo(StealthTankPlanInvalidation.RouteUnsafe));
		}

		[Test]
		public void StrategicWorkRunsOnlyAtTheConfiguredCadence()
		{
			var countdown = 1;
			var scanTicks = new System.Collections.Generic.List<int>();
			for (var tick = 1; tick <= 225; tick++)
				if (StealthTankSquadPolicy.ShouldRunStrategicScan(ref countdown, 75))
					scanTicks.Add(tick);

			Assert.That(scanTicks, Is.EqualTo(new[] { 1, 76, 151 }));
		}

		[Test]
		public void StrategicFactsAreSharedOnlyWithinOneWorldTick()
		{
			Assert.That(StealthTankSquadPolicy.ShouldRefreshStrategicView(75, 75), Is.False);
			Assert.That(StealthTankSquadPolicy.ShouldRefreshStrategicView(75, 76), Is.True);
		}

		[TestCase(false, false, false, false, false, 75, 75, StealthTankPlanInvalidation.TargetChanged)]
		[TestCase(true, true, false, false, false, 75, 75, StealthTankPlanInvalidation.TargetChanged)]
		[TestCase(true, false, true, false, false, 75, 75, StealthTankPlanInvalidation.MembershipChanged)]
		[TestCase(true, false, false, true, false, 75, 75, StealthTankPlanInvalidation.TargetMoved)]
		[TestCase(true, false, false, false, true, 75, 75, StealthTankPlanInvalidation.RouteUnsafe)]
		[TestCase(true, false, false, false, false, 149, 75, StealthTankPlanInvalidation.None)]
		[TestCase(true, false, false, false, false, 150, 75, StealthTankPlanInvalidation.NoProgress)]
		public void StablePlansRetryOnlyOnExplicitInvalidation(bool hasPlan, bool targetChanged,
			bool membershipChanged, bool targetMoved, bool routeUnsafe, int currentTick,
			int lastProgressTick, StealthTankPlanInvalidation expected)
		{
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(hasPlan, targetChanged,
				membershipChanged, targetMoved, routeUnsafe, currentTick, lastProgressTick, 75),
				Is.EqualTo(expected));
		}

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

		[TestCase(0, 7, false, false, 0)]
		[TestCase(0, 7, true, false, 7)]
		[TestCase(5, 7, false, false, 5)]
		[TestCase(5, 7, true, false, 7)]
		[TestCase(5, 7, true, true, 5)]
		public void TransitOnlyTreatsEngagedWeaponsAsCrossfire(int detectorRange, int weaponRange,
			bool weaponIsEngaged, bool canKiteTarget, int expected)
		{
			Assert.That(StealthTankSquadPolicy.TransitThreatRange(detectorRange, weaponRange,
				weaponIsEngaged, canKiteTarget), Is.EqualTo(expected));
		}
	}
}
