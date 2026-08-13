#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System.Linq;
using System.Reflection;
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

		[Test]
		public void SpecialistInfluenceCacheMatchesAirLifetimeBoundary()
		{
			Assert.That(StealthTankSquadPolicy.ShouldRefreshInfluenceMap(int.MinValue, 1, 125), Is.True);
			Assert.That(StealthTankSquadPolicy.ShouldRefreshInfluenceMap(1, 76, 125), Is.False);
			Assert.That(StealthTankSquadPolicy.ShouldRefreshInfluenceMap(1, 125, 125), Is.False);
			Assert.That(StealthTankSquadPolicy.ShouldRefreshInfluenceMap(1, 126, 125), Is.True);
		}

		[Test]
		public void SpecialistInfluenceCacheIsInstanceOwnedAndCannotContaminateAirCache()
		{
			var specialistCache = typeof(StealthTankSquadBotModule).GetField("influenceMap",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(specialistCache, Is.Not.Null);
			Assert.That(specialistCache.IsStatic, Is.False);

			var airState = typeof(StealthTankSquadBotModule).Assembly.GetType(
				"OpenRA.Mods.Common.Traits.BotModules.Squads.AirStateBase");
			var airCaches = airState.GetField("InfluenceCaches", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(airCaches, Is.Not.Null);
			Assert.That(specialistCache.FieldType, Is.Not.EqualTo(airCaches.FieldType));
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

		[TestCase(true, false, true, 1, 8, 2, 0, 1, SpecialistDefenderClearAction.CrushInfantry)]
		[TestCase(true, false, true, 2, 8, 2, 0, 1, SpecialistDefenderClearAction.None)]
		[TestCase(true, false, false, 1, 8, 2, 0, 1, SpecialistDefenderClearAction.None)]
		[TestCase(true, false, true, 1, 8, 2, 2, 1, SpecialistDefenderClearAction.None)]
		[TestCase(false, true, true, 3, 8, 5, 0, 3, SpecialistDefenderClearAction.SnipeTank)]
		[TestCase(false, true, true, 3, 7, 5, 0, 3, SpecialistDefenderClearAction.None)]
		[TestCase(false, true, true, 3, 8, 5, 2, 3, SpecialistDefenderClearAction.None)]
		[TestCase(false, false, true, 1, 8, 0, 16, 3, SpecialistDefenderClearAction.AttackUnarmedDetector,
			Description = "MHQ is Ground/Vehicle, not Structure or Tank; detector capability owns this fallback.")]
		[TestCase(false, false, true, 2, 8, 0, 16, 3, SpecialistDefenderClearAction.None)]
		[TestCase(false, false, true, 1, 8, 5, 16, 3, SpecialistDefenderClearAction.None)]
		public void DefenderClearingRequiresAnExplicitSafeCapability(bool infantry, bool tank, bool canCrush,
			int packageCount, int ownRange, int weaponRange, int detectorRange, int kiteMargin,
			SpecialistDefenderClearAction expected)
		{
			Assert.That(StealthTankSquadPolicy.DefenderClearAction(infantry, tank, canCrush,
				packageCount, ownRange, weaponRange, detectorRange, kiteMargin), Is.EqualTo(expected));
		}

		[TestCase(true, false, false, false, SpecialistRepairDisposition.Active,
			Description = "No repair path leaves a damaged member active until death.")]
		[TestCase(true, false, false, true, SpecialistRepairDisposition.Repair)]
		[TestCase(false, true, true, true, SpecialistRepairDisposition.Rejoin)]
		[TestCase(false, false, false, true, SpecialistRepairDisposition.Active)]
		public void RepairIsOpportunisticAndNeverAnIndefiniteWait(bool damaged,
			bool repairing, bool fullyRepaired, bool reachableRepair, SpecialistRepairDisposition expected)
		{
			Assert.That(StealthTankSquadPolicy.RepairDisposition(damaged, repairing,
				fullyRepaired, reachableRepair), Is.EqualTo(expected));
		}

		[Test]
		public void SafetyBufferDoesNotInventAThreatCapability()
		{
			Assert.That(StealthTankSquadPolicy.BufferedRange(0, 2), Is.Zero);
			Assert.That(StealthTankSquadPolicy.BufferedRange(5, 2), Is.EqualTo(7));
		}

		[TestCase(true, 0, 3, 8, true)]
		[TestCase(true, 0, 8, 8, false)]
		[TestCase(false, 0, 3, 8, false)]
		[TestCase(true, 5, 3, 8, false)]
		public void OnlyUnarmedPrimaryTargetDetectorMayBeOutranged(bool threatIsTarget,
			int weaponRange, int detectorRange, int ownRange, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.CanOutrangeTargetDetector(threatIsTarget,
				weaponRange, detectorRange, ownRange), Is.EqualTo(expected));
		}

		[TestCase(true, false, false, false, Description = "A lone unarmed detector cannot punish revealed fire.")]
		[TestCase(true, true, false, true, Description = "A separate detector and armed support cover the firing cell.")]
		[TestCase(true, true, true, true, Description = "One armed detector supplies both capabilities.")]
		[TestCase(true, false, false, false, Description = "Removing the shooter immediately leaves detector-only coverage.")]
		[TestCase(true, false, false, false, Description = "An ignored weapon is filtered before it can support a detector.")]
		[TestCase(false, true, true, true, Description = "An already-engaged weapon remains an immediate threat without a detector.")]
		public void EngagementSafetyRequiresArmedPunishmentForDetectorExposure(bool detectorExposure,
			bool armedCoverage, bool engagedWeaponExposure, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.IsEngagementThreat(detectorExposure,
				armedCoverage, engagedWeaponExposure), Is.EqualTo(expected));
		}

		[TestCase(true, true, true, false, false)]
		[TestCase(true, true, false, true, false)]
		[TestCase(true, false, false, false, false)]
		[TestCase(false, true, false, false, false,
			Description = "A target first suspended by this safety check cannot resume in that same check.")]
		[TestCase(true, true, false, false, true)]
		public void SuspendedEngagementResumesOnlyAfterShooterOrHazardClears(bool wasAlreadySuspended,
			bool hasValidTarget,
			bool localThreatExposure, bool resourceHazard, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.ShouldResumeSuspendedEngagement(wasAlreadySuspended, hasValidTarget,
				localThreatExposure, resourceHazard), Is.EqualTo(expected));
		}

		[TestCase(true, true, false, false, true)]
		[TestCase(true, true, true, false, false)]
		[TestCase(true, true, false, true, false)]
		[TestCase(true, false, false, false, false)]
		[TestCase(false, true, false, false, false)]
		public void StrategicApproachScanCannotTurnDetectorAloneIntoAnActiveEngagementVeto(
			bool hasValidTarget, bool isEngaged, bool localThreatExposure, bool resourceHazard, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.ShouldRetainActiveEngagement(hasValidTarget,
				isEngaged, localThreatExposure, resourceHazard), Is.EqualTo(expected));
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
