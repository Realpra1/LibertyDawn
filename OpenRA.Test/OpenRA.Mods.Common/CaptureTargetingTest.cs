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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class CaptureTargetingTest
	{
		[Test]
		public void OwnedRestorationExceptionRequiresTheCompleteNonBuildingCapabilityContract()
		{
			Assert.That(CaptureTargeting.IsCapabilityScopedOwnedRestorationCandidate(
				true, true, false, true, true), Is.True);
			Assert.That(CaptureTargeting.IsCapabilityScopedOwnedRestorationCandidate(
				false, true, false, true, true), Is.False, "Other owners stay under configured relationships.");
			Assert.That(CaptureTargeting.IsCapabilityScopedOwnedRestorationCandidate(
				true, true, true, true, true), Is.False, "Owned buildings are never exposed.");
			Assert.That(CaptureTargeting.IsCapabilityScopedOwnedRestorationCandidate(
				true, false, false, true, true), Is.False, "A transform alone is not a husk contract.");
			Assert.That(CaptureTargeting.IsCapabilityScopedOwnedRestorationCandidate(
				true, true, false, false, true), Is.False, "A husk without a valid transform is not restorable.");
			Assert.That(CaptureTargeting.IsCapabilityScopedOwnedRestorationCandidate(
				true, true, false, true, false), Is.False, "The Engineer must match an enabled capture contract.");
		}

		[Test]
		public void RecoveredActorMakesHuskEconomicallyValuable()
		{
			Assert.That(CaptureTargeting.EconomicValue(0, 1500), Is.EqualTo(1500));
			Assert.That(CaptureTargeting.EconomicValue(2000, 1500), Is.EqualTo(2000));
		}

		[Test]
		public void SelectsValueThenBuildingThenDistanceAndDoesNotDuplicate()
		{
			var scores = new double[] { 1000, 1500, 1500, 1500 };
			var buildings = new[] { true, false, true, true };
			var distances = new long[] { 1, 1, 100, 25 };
			var assigned = new HashSet<int>();

			var first = CaptureTargeting.BestTargetIndex(scores, buildings, distances, assigned);
			Assert.That(first, Is.EqualTo(3));
			assigned.Add(first);
			Assert.That(CaptureTargeting.BestTargetIndex(scores, buildings, distances, assigned), Is.EqualTo(2));
		}

		[Test]
		public void TargetScoreBalancesEconomicValueAgainstTravelDistance()
		{
			var nearbyHusk = CaptureTargeting.Score(1100, 2, 10);
			var distantBuilding = CaptureTargeting.Score(4000, 170, 10);

			Assert.That(nearbyHusk, Is.GreaterThan(distantBuilding));
			Assert.That(CaptureTargeting.Score(4000, 0, 10), Is.EqualTo(4000));
		}

		[Test]
		public void BuildingPairRequirementUsesExactHealthRatio()
		{
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, 79, 100, 80), Is.False);
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, 80, 100, 80), Is.False);
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, 80001, 100000, 80), Is.True);
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, 81, 100, 80), Is.True);
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, 1, 0, 80), Is.True);
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, 1, -1, 80), Is.True);
			Assert.That(CaptureTargeting.RequiresEngineerPair(false, int.MaxValue, 1, 80), Is.False);
			Assert.That(CaptureTargeting.RequiresEngineerPair(true, int.MaxValue, int.MaxValue, 80), Is.True);
		}

		[Test]
		public void RetargetingUsesStrictImprovementMargin()
		{
			Assert.That(CaptureTargeting.ShouldRetarget(100, 124, 25), Is.False);
			Assert.That(CaptureTargeting.ShouldRetarget(100, 125, 25), Is.False);
			Assert.That(CaptureTargeting.ShouldRetarget(100, 126, 25), Is.True);
		}

		[Test]
		public void SharedReservationsAllowOnlyTheRequiredCapturePair()
		{
			var reservations = new SpecialistTargetReservations();

			Assert.That(reservations.TryReserve(10, 100, SpecialistAssignmentPurpose.Capture, 2), Is.True);
			Assert.That(reservations.TryReserve(11, 100, SpecialistAssignmentPurpose.Capture, 2), Is.True);
			Assert.That(reservations.TryReserve(12, 100, SpecialistAssignmentPurpose.Capture, 2), Is.False);
			Assert.That(reservations.Claimants(100), Is.EqualTo(new uint[] { 10, 11 }));
		}

		[Test]
		public void SharedReservationsRetainIncumbentsAndExcludeTheOtherPurposeInEitherArrivalOrder()
		{
			var captureFirst = new SpecialistTargetReservations();
			Assert.That(captureFirst.TryReserve(10, 100, SpecialistAssignmentPurpose.Capture, 1), Is.True);
			Assert.That(captureFirst.TryReserve(10, 100, SpecialistAssignmentPurpose.Capture, 1), Is.True);
			Assert.That(captureFirst.TryReserve(20, 100, SpecialistAssignmentPurpose.Demolition, 1), Is.False);

			var demolitionFirst = new SpecialistTargetReservations();
			Assert.That(demolitionFirst.TryReserve(20, 100, SpecialistAssignmentPurpose.Demolition, 1), Is.True);
			Assert.That(demolitionFirst.TryReserve(10, 100, SpecialistAssignmentPurpose.Capture, 2), Is.False);
		}

		[Test]
		public void SharedReservationsReleaseAndRetargetDeterministically()
		{
			var reservations = new SpecialistTargetReservations();
			Assert.That(reservations.TryReserve(20, 200, SpecialistAssignmentPurpose.Demolition, 1), Is.True);
			Assert.That(reservations.TryReserve(10, 200, SpecialistAssignmentPurpose.Capture, 1), Is.False);

			reservations.Release(20);
			Assert.That(reservations.TryReserve(10, 200, SpecialistAssignmentPurpose.Capture, 1), Is.True);
			Assert.That(reservations.TryReserve(10, 201, SpecialistAssignmentPurpose.Capture, 1), Is.True);
			Assert.That(reservations.IsReserved(200), Is.False);
			Assert.That(reservations.Claimants(201), Is.EqualTo(new uint[] { 10 }));
		}

		[Test]
		public void SharedReservationsMatchExactPurposeTargetAndClaimantCardinality()
		{
			var reservations = new SpecialistTargetReservations();
			Assert.That(reservations.TryReserve(10, 100, SpecialistAssignmentPurpose.Capture, 2), Is.True);
			Assert.That(reservations.Matches(10, 100, SpecialistAssignmentPurpose.Capture, 2), Is.False,
				"An incomplete pair is not a coherent paired assignment.");
			Assert.That(reservations.TryReserve(11, 100, SpecialistAssignmentPurpose.Capture, 2), Is.True);
			Assert.That(reservations.Matches(10, 100, SpecialistAssignmentPurpose.Capture, 2), Is.True);
			Assert.That(reservations.Matches(10, 101, SpecialistAssignmentPurpose.Capture, 2), Is.False);
			Assert.That(reservations.Matches(10, 100, SpecialistAssignmentPurpose.Demolition, 2), Is.False);
			Assert.That(reservations.Matches(10, 100, SpecialistAssignmentPurpose.Capture, 1), Is.False);

			Assert.That(reservations.TryGetReservation(10, out var targetId, out var purpose), Is.True);
			Assert.That(targetId, Is.EqualTo(100));
			Assert.That(purpose, Is.EqualTo(SpecialistAssignmentPurpose.Capture));
		}

		[Test]
		public void MissingActivityUsesTheExistingBoundedGrace()
		{
			Assert.That(CaptureTargeting.ActivityGraceExpired(-1, 100, 10), Is.False);
			Assert.That(CaptureTargeting.ActivityGraceExpired(100, 110, 10), Is.False);
			Assert.That(CaptureTargeting.ActivityGraceExpired(100, 111, 10), Is.True);
			Assert.That(CaptureTargeting.ActivityGraceExpired(100, 101, 0), Is.True);
		}

		[Test]
		public void RestoredAssignmentUsesPersistedMissingActivityGraceInsteadOfOriginalAssignmentTick()
		{
			const int assignedTick = 50;
			const int missingActivitySinceTick = 195;
			const int savedWorldTick = 200;

			Assert.That(CaptureTargeting.ShouldRestoreAssignmentActivity(false,
				assignedTick, savedWorldTick, 10), Is.False,
				"The original assignment tick is intentionally too old for a new activity grace.");
			Assert.That(CaptureTargeting.ShouldRestoreAssignmentActivity(false,
				missingActivitySinceTick, savedWorldTick, 10), Is.True,
				"A save inside the persisted missing-activity grace must restore the assignment and exact claim.");
			Assert.That(CaptureTargeting.ShouldRestoreAssignmentActivity(false,
				missingActivitySinceTick, 205, 10), Is.True);
			Assert.That(CaptureTargeting.ShouldRestoreAssignmentActivity(false,
				missingActivitySinceTick, 206, 10), Is.False,
				"Restoration must not extend the grace beyond its saved boundary.");
			Assert.That(CaptureTargeting.ShouldRestoreAssignmentActivity(true,
				assignedTick, savedWorldTick, 10), Is.True,
				"Expected live activity remains authoritative regardless of assignment age.");
		}

		[Test]
		public void CommandoOwnershipSaveRecordsRoundTripEveryLifecycleField()
		{
			var moduleType = typeof(CaptureManagerBotModule);
			var confirmation = new MiniYamlNode("Confirmation", "", new List<MiniYamlNode>
			{
				new MiniYamlNode("Specialist", FieldSaver.FormatValue((uint)17)),
				new MiniYamlNode("IdleSinceTick", FieldSaver.FormatValue(193))
			});
			var fallback = new MiniYamlNode("Fallback", "", new List<MiniYamlNode>
			{
				new MiniYamlNode("Specialist", FieldSaver.FormatValue((uint)23)),
				new MiniYamlNode("Purpose", FieldSaver.FormatValue(1)),
				new MiniYamlNode("Target", FieldSaver.FormatValue((uint)0)),
				new MiniYamlNode("Destination", FieldSaver.FormatValue(new CPos(41, 29))),
				new MiniYamlNode("AssignedTick", FieldSaver.FormatValue(200)),
				new MiniYamlNode("ReconsiderTick", FieldSaver.FormatValue(325))
			});

			Assert.That(RoundTripPrivateSaveRecord(moduleType, "LoadCommandoConfirmation",
				"SaveCommandoConfirmation", confirmation), Is.EqualTo(Serialize(confirmation)));
			Assert.That(RoundTripPrivateSaveRecord(moduleType, "LoadCommandoFallback",
				"SaveCommandoFallback", fallback), Is.EqualTo(Serialize(fallback)));
		}

		static string RoundTripPrivateSaveRecord(Type moduleType, string loadName, string saveName,
			MiniYamlNode node)
		{
			var flags = BindingFlags.Static | BindingFlags.NonPublic;
			var loaded = moduleType.GetMethod(loadName, flags).Invoke(null, new object[] { node });
			return Serialize((MiniYamlNode)moduleType.GetMethod(saveName, flags).Invoke(null, new[] { loaded }));
		}

		static string Serialize(MiniYamlNode node) { return new List<MiniYamlNode> { node }.WriteToString(); }

		[Test]
		public void SharedReservationsRestoreBothIncumbentPurposesDeterministically()
		{
			var captureFirst = new SpecialistTargetReservations();
			var restoredCapture = captureFirst.Restore(new[]
			{
				new SpecialistTargetReservationState(11, 100, SpecialistAssignmentPurpose.Capture, 2),
				new SpecialistTargetReservationState(10, 100, SpecialistAssignmentPurpose.Capture, 2)
			});

			Assert.That(restoredCapture.Select(r => r.SpecialistId), Is.EqualTo(new uint[] { 10, 11 }));
			Assert.That(captureFirst.Claimants(100), Is.EqualTo(new uint[] { 10, 11 }));
			Assert.That(captureFirst.TryReserve(20, 100, SpecialistAssignmentPurpose.Demolition, 1), Is.False);

			var demolitionFirst = new SpecialistTargetReservations();
			var restoredDemolition = demolitionFirst.Restore(new[]
			{
				new SpecialistTargetReservationState(20, 200, SpecialistAssignmentPurpose.Demolition, 1)
			});

			Assert.That(restoredDemolition.Select(r => r.SpecialistId), Is.EqualTo(new uint[] { 20 }));
			Assert.That(demolitionFirst.TryReserve(10, 200, SpecialistAssignmentPurpose.Capture, 1), Is.False);
		}

		[Test]
		public void SharedReservationRestoreDropsIncompletePairsButKeepsValidAssignments()
		{
			var reservations = new SpecialistTargetReservations();
			var restored = reservations.Restore(new[]
			{
				new SpecialistTargetReservationState(10, 100, SpecialistAssignmentPurpose.Capture, 2),
				new SpecialistTargetReservationState(20, 200, SpecialistAssignmentPurpose.Demolition, 1)
			});

			Assert.That(restored.Select(r => r.SpecialistId), Is.EqualTo(new uint[] { 20 }));
			Assert.That(reservations.IsReserved(100), Is.False);
			Assert.That(reservations.Claimants(200), Is.EqualTo(new uint[] { 20 }));
		}

		[Test]
		public void SharedReservationPairCanShrinkToAValidSavedSoloClaim()
		{
			var reservations = new SpecialistTargetReservations();
			Assert.That(reservations.TryReserve(10, 100, SpecialistAssignmentPurpose.Capture, 2), Is.True);
			Assert.That(reservations.TryReserve(11, 100, SpecialistAssignmentPurpose.Capture, 2), Is.True);

			reservations.Release(11);
			Assert.That(reservations.Claimants(100), Is.EqualTo(new uint[] { 10 }));

			var restored = reservations.Restore(new[]
			{
				new SpecialistTargetReservationState(10, 100, SpecialistAssignmentPurpose.Capture, 1)
			});
			Assert.That(restored.Select(r => r.SpecialistId), Is.EqualTo(new uint[] { 10 }));
			Assert.That(reservations.Claimants(100), Is.EqualTo(new uint[] { 10 }));
		}

		[Test]
		public void AutonomousDemolitionSafetyLatchesRelationshipInvalidation()
		{
			var safety = new DemolitionSafety(PlayerRelationship.Enemy);

			Assert.That(safety.IsValid(PlayerRelationship.Enemy), Is.True);
			Assert.That(safety.IsValid(PlayerRelationship.Ally), Is.False);
			Assert.That(safety.Invalidated, Is.True);
			Assert.That(safety.IsValid(PlayerRelationship.Enemy), Is.False,
				"A later hostile relationship must not reactivate an obsolete autonomous charge.");
		}

		[Test]
		public void AutonomousDemolitionSafetyAcceptsEveryConfiguredRelationship()
		{
			var safety = new DemolitionSafety(PlayerRelationship.Enemy | PlayerRelationship.Neutral);

			Assert.That(safety.IsValid(PlayerRelationship.Enemy), Is.True);
			Assert.That(safety.IsValid(PlayerRelationship.Neutral), Is.True);
			Assert.That(safety.Invalidated, Is.False);
		}

		[Test]
		public void PairUsesWorseMemberAndComparesDistinctSoloTargets()
		{
			Assert.That(CaptureTargeting.PairScore(900, 300), Is.EqualTo(300));
			Assert.That(CaptureTargeting.PairScore(900, -1), Is.EqualTo(-1));

			var firstScores = new double[] { 80, 60, -1 };
			var secondScores = new double[] { 70, 100, -1 };
			var allocation = CaptureTargeting.BestDistinctTargetAllocation(
				firstScores, secondScores, new HashSet<int>());
			Assert.That(allocation.FirstTarget, Is.EqualTo(0));
			Assert.That(allocation.SecondTarget, Is.EqualTo(1));
			Assert.That(allocation.Score, Is.EqualTo(180));

			allocation = CaptureTargeting.BestDistinctTargetAllocation(
				firstScores, secondScores, new HashSet<int> { 1 });
			Assert.That(allocation.FirstTarget, Is.EqualTo(0));
			Assert.That(allocation.SecondTarget, Is.EqualTo(-1));
			Assert.That(allocation.Score, Is.EqualTo(80));
		}

		[Test]
		public void DistinctSoloAllocationBreaksEqualTiesDeterministically()
		{
			var scores = new double[] { 100, 100 };
			var allocation = CaptureTargeting.BestDistinctTargetAllocation(
				scores, scores, new HashSet<int>());
			Assert.That(allocation.FirstTarget, Is.EqualTo(0));
			Assert.That(allocation.SecondTarget, Is.EqualTo(1));
			Assert.That(allocation.Score, Is.EqualTo(200));
		}

		[Test]
		public void DemolitionAllocationIsTargetFirstAndChoosesNearestViableSpecialist()
		{
			var distances = new long[,]
			{
				{ 900, 4 },
				{ 9, 100 },
				{ 1, 1 }
			};
			var viable = new bool[,]
			{
				{ true, true },
				{ true, true },
				{ false, true }
			};

			var allocation = CaptureTargeting.TargetFirstDemolitionAllocation(distances, viable);

			Assert.That(allocation.Select(pair => pair.Target), Is.EqualTo(new[] { 0, 1 }));
			Assert.That(allocation.Select(pair => pair.Unit), Is.EqualTo(new[] { 1, 2 }),
				"The lower-ID far specialist must not consume the target before the nearer viable specialist.");
		}

		[Test]
		public void DemolitionAllocationUsesStableUnitTieAndLeavesSurplusUnassigned()
		{
			var distances = new long[,]
			{
				{ 25 },
				{ 25 },
				{ 4 }
			};
			var viable = new bool[,]
			{
				{ true },
				{ true },
				{ false }
			};

			var allocation = CaptureTargeting.TargetFirstDemolitionAllocation(distances, viable);

			Assert.That(allocation.Count, Is.EqualTo(1));
			Assert.That(allocation[0].Unit, Is.EqualTo(0));
			Assert.That(allocation[0].Target, Is.EqualTo(0));
		}

		[Test]
		public void OwnerlessConfirmationRequiresAStableIdleUnreservedGrace()
		{
			Assert.That(CaptureTargeting.ConfirmedOwnerless(100, 109, 10,
				idle: true, hasActivity: false, reserved: false, transportOwned: false), Is.False);
			Assert.That(CaptureTargeting.ConfirmedOwnerless(100, 110, 10,
				idle: true, hasActivity: false, reserved: false, transportOwned: false), Is.True);
			Assert.That(CaptureTargeting.ConfirmedOwnerless(100, 120, 10,
				idle: false, hasActivity: false, reserved: false, transportOwned: false), Is.False);
			Assert.That(CaptureTargeting.ConfirmedOwnerless(100, 120, 10,
				idle: true, hasActivity: true, reserved: false, transportOwned: false), Is.False);
			Assert.That(CaptureTargeting.ConfirmedOwnerless(100, 120, 10,
				idle: true, hasActivity: false, reserved: true, transportOwned: false), Is.False);
			Assert.That(CaptureTargeting.ConfirmedOwnerless(100, 120, 10,
				idle: true, hasActivity: false, reserved: false, transportOwned: true), Is.False);
		}

		[TestCase(false, false, false, DemolitionApproachResponse.Direct)]
		[TestCase(true, true, false, DemolitionApproachResponse.FightThrough)]
		[TestCase(true, false, true, DemolitionApproachResponse.RouteAround)]
		[TestCase(true, false, false, DemolitionApproachResponse.WithdrawOrHold)]
		public void DemolitionApproachHasExplicitThreatStateExits(bool threatened, bool favorableFight,
			bool alternateRoute, DemolitionApproachResponse expected)
		{
			Assert.That(CaptureTargeting.DemolitionApproach(threatened, favorableFight, alternateRoute),
				Is.EqualTo(expected));
		}

		[TestCase(false, false, false)]
		[TestCase(false, true, false)]
		[TestCase(true, false, false)]
		[TestCase(true, true, true)]
		public void HoldReroutesOnlyWhenItsDestinationBecomesThreatenedAndAnotherSafeCellExists(
			bool destinationThreatened, bool safeDestinationFound, bool expected)
		{
			Assert.That(CaptureTargeting.ShouldRerouteHold(destinationThreatened, safeDestinationFound),
				Is.EqualTo(expected));
		}

		[Test]
		public void ThreatCoverageMarginPrioritizesLaneCoverageBeforeDistantValue()
		{
			Assert.That(CaptureTargeting.ThreatCoverageMargin(distanceSquared: 36, range: 7),
				Is.LessThan(CaptureTargeting.ThreatCoverageMargin(distanceSquared: 400, range: 10)));
		}

		[TestCase(false, false, false)]
		[TestCase(true, true, false)]
		[TestCase(true, false, true)]
		public void CapturePreemptsOnlyReversibleDemolition(bool actionableCapture, bool plantedCharge,
			bool expected)
		{
			Assert.That(CaptureTargeting.CanPreemptDemolition(actionableCapture, plantedCharge),
				Is.EqualTo(expected));
		}
	}
}
