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
		sealed class RepairInfluenceState
		{
			public readonly string Profile;
			public readonly int Weight;
			public object CachedFacts;

			public RepairInfluenceState(string profile, int weight)
			{
				Profile = profile;
				Weight = weight;
			}

			public string GetPrivateInfluence(object sharedFacts)
			{
				CachedFacts = sharedFacts;
				return $"{Profile}:{Weight}";
			}
		}

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

		[Test]
		public void StealthStrategicGeometryUsesExactSixBySixCells()
		{
			Assert.That(StealthTankSquadPolicy.RequiredStrategicCellSize, Is.EqualTo(6));
			Assert.That(StealthTankSquadPolicy.StrategicCell(new CPos(0, 0), 6), Is.EqualTo(new CPos(0, 0)));
			Assert.That(StealthTankSquadPolicy.StrategicCell(new CPos(5, 5), 6), Is.EqualTo(new CPos(0, 0)));
			Assert.That(StealthTankSquadPolicy.StrategicCell(new CPos(6, 6), 6), Is.EqualTo(new CPos(1, 1)));
			Assert.That(StealthTankSquadPolicy.IsSameStrategicCell(
				new CPos(6, 6), new CPos(11, 11), 6), Is.True);
			Assert.That(StealthTankSquadPolicy.IsSameStrategicCell(
				new CPos(5, 5), new CPos(6, 6), 6), Is.False);
		}

		[Test]
		public void MobileTargetInsideMissionCellDoesNotCauseImmediateOrderChurn()
		{
			var movedInsideCell = !StealthTankSquadPolicy.IsSameStrategicCell(
				new CPos(7, 8), new CPos(10, 11), 6);
			Assert.That(movedInsideCell, Is.False);
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(true, false,
				false, movedInsideCell, false, 299, 0, 300), Is.EqualTo(StealthTankPlanInvalidation.None));
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(true, false,
				false, movedInsideCell, false, 300, 0, 300), Is.EqualTo(StealthTankPlanInvalidation.NoProgress));
		}

		[TestCase(18, 18, 6, 6, 27, 27)]
		[TestCase(18, 18, 30, 18, 15, 15)]
		[TestCase(1, 1, 8, 1, 3, 9)]
		public void RevealRetreatMovesExactlyOneAdjacentSixCellStrategicCell(
			int unitX, int unitY, int targetX, int targetY, int expectedX, int expectedY)
		{
			var start = StealthTankSquadPolicy.StrategicCell(new CPos(unitX, unitY), 6);
			var destination = StealthTankSquadPolicy.OneStrategicCellRetreat(
				new CPos(unitX, unitY), new CPos(targetX, targetY), 6, 96, 96);
			var destinationCell = StealthTankSquadPolicy.StrategicCell(destination, 6);

			Assert.That(destination, Is.EqualTo(new CPos(expectedX, expectedY)));
			Assert.That(System.Math.Max(System.Math.Abs(destinationCell.X - start.X),
				System.Math.Abs(destinationCell.Y - start.Y)), Is.EqualTo(1));
		}

		[Test]
		public void NearbyUndefendedReactionHasATwentyFiveTickBound()
		{
			Assert.That(StealthTankSquadPolicy.NearbyReactionMaximumLatencyTicks, Is.EqualTo(25));
		}

		[Test]
		public void VersionedMultiUnitRetreatSaveRestoresBarrierUntilEveryMemberCompletes()
		{
			var saved = StealthTankSquadPolicy.SaveRetreatState(new[]
			{
				new StealthTankRetreatSaveGroup
				{
					GroupIndex = 0,
					TargetId = 91,
					Destinations = new[]
					{
						new System.Collections.Generic.KeyValuePair<uint, CPos>(11, new CPos(27, 21)),
						new System.Collections.Generic.KeyValuePair<uint, CPos>(12, new CPos(33, 27))
					}
				}
			});

			Assert.That(StealthTankSquadPolicy.TryLoadRetreatState(saved, out var restored), Is.True);
			Assert.That(restored, Has.Length.EqualTo(1));
			Assert.That(restored[0].TargetId, Is.EqualTo(91));
			Assert.That(restored[0].Destinations.Select(d => d.Key), Is.EqualTo(new uint[] { 11, 12 }));

			var remaining = restored[0].Destinations.ToDictionary(d => d.Key, d => d.Value);
			Assert.That(StealthTankSquadPolicy.ShouldBlockReassessment(remaining.Count), Is.True);
			remaining.Remove(11);
			Assert.That(StealthTankSquadPolicy.ShouldBlockReassessment(remaining.Count), Is.True,
				"One completed member must not release the restored multi-unit retreat barrier.");
			remaining.Remove(12);
			Assert.That(StealthTankSquadPolicy.ShouldBlockReassessment(remaining.Count), Is.False);
		}

		[Test]
		public void MalformedOrFutureRetreatSaveFallsBackWithoutState()
		{
			var malformed = new MiniYamlNode("StealthTankRetreatState", "", new[]
			{
				new MiniYamlNode("Version", FieldSaver.FormatValue(
					StealthTankSquadPolicy.RetreatSaveVersion + 1))
			}.ToList());

			Assert.That(StealthTankSquadPolicy.TryLoadRetreatState(malformed, out var restored), Is.False);
			Assert.That(restored, Is.Empty);
		}

		[Test]
		public void RestoredRetreatRecomputesAwayFromLiveTargetThatMovedAcrossUnit()
		{
			var unit = new CPos(18, 18);
			var originalTarget = new CPos(30, 18);
			var movedTarget = new CPos(6, 18);
			var savedDestination = StealthTankSquadPolicy.OneStrategicCellRetreat(
				unit, originalTarget, 6, 96, 96);
			var saved = StealthTankSquadPolicy.SaveRetreatState(new[]
			{
				new StealthTankRetreatSaveGroup
				{
					GroupIndex = 0,
					TargetId = 91,
					Destinations = new[]
					{
						new System.Collections.Generic.KeyValuePair<uint, CPos>(11, savedDestination)
					}
				}
			});

			Assert.That(StealthTankSquadPolicy.TryLoadRetreatState(saved, out var restored), Is.True);
			Assert.That(StealthTankSquadPolicy.IsRetreatDestinationAwayFromTarget(unit,
				restored[0].Destinations[0].Value, movedTarget, 6, 96, 96), Is.False,
				"The saved westward destination now points toward the moved live target.");

			var recomputed = StealthTankSquadPolicy.OneStrategicCellRetreat(unit, movedTarget, 6, 96, 96);
			Assert.That(StealthTankSquadPolicy.IsRetreatDestinationAwayFromTarget(
				unit, recomputed, movedTarget, 6, 96, 96), Is.True);
			Assert.That(recomputed.X, Is.GreaterThan(unit.X),
				"Restore must resume east, away from the target that moved west of the unit.");
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

		[Test]
		public void ClaimAllPolicySelectsEveryDistinctEligibleTankAndReservesNewDiscoveries()
		{
			var selected = StealthTankSquadPolicy.SelectSpecialistIds(
				new uint[] { 14, 11, 13, 12, 14 }, new uint[] { 11, 99 }, true, true);

			Assert.That(selected, Is.EqualTo(new uint[] { 11, 12, 13, 14 }));
			Assert.That(selected, Is.Unique);
			Assert.That(StealthTankSquadPolicy.ShouldReserveUnit(false, true, true), Is.True,
				"A produced or captured eligible tank must be reserved before the next strategic scan.");
			Assert.That(StealthTankSquadPolicy.ShouldReserveUnit(false, true, false), Is.False);
		}

		[Test]
		public void SpecialistSquadTopologyHasAHardFourSquadCeiling()
		{
			Assert.That(StealthTankSquadPolicy.SquadCount(2, true), Is.EqualTo(3));
			Assert.That(StealthTankSquadPolicy.SquadCount(3, true),
				Is.EqualTo(StealthTankSquadPolicy.MaximumSquadCount));
			Assert.That(StealthTankSquadPolicy.SquadCount(4, true),
				Is.GreaterThan(StealthTankSquadPolicy.MaximumSquadCount));
			Assert.That(StealthTankSquadPolicy.SquadCount(int.MaxValue, true),
				Is.GreaterThan(StealthTankSquadPolicy.MaximumSquadCount),
				"Overflow must not let an invalid configuration bypass the four-squad ruleset guard.");
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
		public void PartnerDeathRetainsLoneSpecialistOwnershipAndActiveGroup()
		{
			var selected = StealthTankSquadPolicy.SelectSpecialistIds(new uint[] { 11 },
				new uint[] { 10, 11 }, true);

			Assert.That(selected, Is.EqualTo(new uint[] { 11 }));
			Assert.That(StealthTankSquadPolicy.GroupForIndex(0, selected.Length), Is.Zero);
			Assert.That(StealthTankSquadPolicy.RepairDisposition(true, false, false, false),
				Is.EqualTo(SpecialistRepairDisposition.Active));
		}

		[Test]
		public void CompatibleReplacementReformsWithTheOwnedSurvivor()
		{
			var selected = StealthTankSquadPolicy.SelectSpecialistIds(new uint[] { 11, 12 },
				new uint[] { 11 }, true);

			Assert.That(selected, Is.EqualTo(new uint[] { 11, 12 }));
			Assert.That(selected.Select(id => StealthTankSquadPolicy.GroupForIndex(
				System.Array.IndexOf(selected, id), selected.Length)), Is.EqualTo(new[] { 0, 0 }));
		}

		[Test]
		public void RestoredLoneOwnershipSurvivesSaveLoadRebalance()
		{
			var savedReservedIds = new uint[] { 11 };
			var selected = StealthTankSquadPolicy.SelectSpecialistIds(new uint[] { 11 },
				savedReservedIds, true);

			Assert.That(selected, Is.EqualTo(savedReservedIds));
		}

		[Test]
		public void ReformationCannotCreateOwnerlessOrDuplicateReservations()
		{
			var selected = StealthTankSquadPolicy.SelectSpecialistIds(
				new uint[] { 12, 11, 12 }, new uint[] { 11, 11, 99 }, true);

			Assert.That(selected, Is.EqualTo(new uint[] { 11, 12 }));
			Assert.That(selected, Is.Unique);
			Assert.That(selected, Has.None.EqualTo(99));
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
		public void BothProfilesUseSharedRepairFactsWithPrivateInfluenceState()
		{
			var ownerThreatFacts = new object();
			var stealth = new RepairInfluenceState("stealth-tank", 7);
			var chemical = new RepairInfluenceState("chemical", 13);

			var stealthInfluence = StealthTankSquadPolicy.ResolveRepairInfluence(
				ownerThreatFacts, stealth.GetPrivateInfluence);
			var chemicalInfluence = StealthTankSquadPolicy.ResolveRepairInfluence(
				ownerThreatFacts, chemical.GetPrivateInfluence);

			Assert.That(stealthInfluence, Is.EqualTo("stealth-tank:7"));
			Assert.That(chemicalInfluence, Is.EqualTo("chemical:13"));
			Assert.That(stealth.CachedFacts, Is.SameAs(ownerThreatFacts));
			Assert.That(chemical.CachedFacts, Is.SameAs(ownerThreatFacts));
			Assert.That(stealthInfluence, Is.Not.EqualTo(chemicalInfluence),
				"Shared factual threats must not share profile weights or influence caches.");
		}

		[Test]
		public void MissingSharedRepairFactsRetainsActiveNoRepairFallback()
		{
			var influenceBuilds = 0;
			var influence = StealthTankSquadPolicy.ResolveRepairInfluence<object, string>(null, facts =>
			{
				influenceBuilds++;
				return "unexpected";
			});

			Assert.That(influence, Is.Null);
			Assert.That(influenceBuilds, Is.Zero);
			Assert.That(StealthTankSquadPolicy.RepairDisposition(true, false, false,
				influence != null), Is.EqualTo(SpecialistRepairDisposition.Active));
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
