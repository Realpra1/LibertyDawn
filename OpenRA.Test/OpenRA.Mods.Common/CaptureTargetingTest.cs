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
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class CaptureTargetingTest
	{
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
	}
}
