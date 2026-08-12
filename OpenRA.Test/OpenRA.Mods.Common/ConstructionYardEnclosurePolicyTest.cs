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

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class ConstructionYardEnclosurePolicyTest
	{
		static ConstructionYardEnclosurePlan FactPlan()
		{
			return ConstructionYardEnclosurePolicy.CreatePlan(new CPos(50, 50), new CVec(3, 3), 1, 3);
		}

		[Test]
		public void FactPlanHasStableThreeCellSouthAccess()
		{
			var plan = FactPlan();
			Assert.That(plan.AccessCells, Is.EqualTo(new[]
			{
				new CPos(50, 53), new CPos(51, 53), new CPos(52, 53)
			}));
			Assert.That(plan.WallCells, Has.Length.EqualTo(13));
			Assert.That(plan.WallCells.Intersect(plan.AccessCells), Is.Empty);
			Assert.That(plan.WallCells.Distinct().Count(), Is.EqualTo(plan.WallCells.Length));
			Assert.That(ConstructionYardEnclosurePolicy.CreatePlan(
				new CPos(50, 50), new CVec(3, 3), 1, 3).WallCells, Is.EqualTo(plan.WallCells));
		}

		[Test]
		public void BlockedCellDoesNotVetoIndependentLegalRun()
		{
			var plan = FactPlan();
			var blocked = new CPos(51, 49);
			var run = ConstructionYardEnclosurePolicy.FirstLegalMissingRun(plan,
				c => false, c => c != blocked);
			Assert.That(run, Is.EqualTo(new[] { new CPos(49, 49), new CPos(50, 49) }));

			var alreadyBuilt = new HashSet<CPos>(run);
			run = ConstructionYardEnclosurePolicy.FirstLegalMissingRun(plan,
				alreadyBuilt.Contains, c => c != blocked);
			Assert.That(run, Is.EqualTo(new[] { new CPos(52, 49), new CPos(53, 49) }));
		}

		[Test]
		public void InteriorDestroyedCellBecomesRepairCandidate()
		{
			var plan = FactPlan();
			var hole = new CPos(49, 51);
			var present = new HashSet<CPos>(plan.WallCells.Where(c => c != hole));
			Assert.That(ConstructionYardEnclosurePolicy.FirstLegalMissingRun(plan,
				present.Contains, c => true), Is.EqualTo(new[] { hole }));
		}

		[Test]
		public void TransientAndFixedBlockageRemainPendingWithoutChangingPlan()
		{
			var plan = FactPlan();
			var present = new HashSet<CPos>(plan.WallCells.Take(5));
			var fixedCell = plan.WallCells[5];
			var transientCell = plan.WallCells[6];
			var first = ConstructionYardEnclosurePolicy.FirstLegalMissingRun(plan,
				present.Contains, c => c != fixedCell && c != transientCell);
			Assert.That(first, Is.Not.Empty);
			Assert.That(first, Does.Not.Contain(fixedCell));
			Assert.That(first, Does.Not.Contain(transientCell));
			Assert.That(plan.WallCells, Does.Contain(fixedCell));
			Assert.That(plan.WallCells, Does.Contain(transientCell));
		}

		[Test]
		public void MissingDestinationsAreCornerFirstThenNearestWithStablePlanTies()
		{
			var plan = FactPlan();
			var blockedCorner = new CPos(49, 49);
			var present = new HashSet<CPos>(plan.WallCells.Where(c =>
				c != blockedCorner && c != new CPos(53, 49) && c != new CPos(49, 53) &&
				c != new CPos(50, 49) && c != new CPos(49, 50)));

			var ordered = ConstructionYardEnclosurePolicy.OrderedLegalMissingCells(plan,
				new CPos(50, 50), present.Contains, c => c != blockedCorner);

			Assert.That(ordered, Is.EqualTo(new[]
			{
				new CPos(53, 49),
				new CPos(49, 53),
				new CPos(50, 49),
				new CPos(49, 50)
			}));
			Assert.That(plan.WallCells, Does.Contain(blockedCorner),
				"A temporary blocker must not mutate or remove the immutable destination.");
		}

		[Test]
		public void ExactRouteValidationRejectsReversedOccupiedAndFactCrossingPaths()
		{
			var origin = new CPos(51, 53);
			var destination = new CPos(53, 51);
			var factCells = new HashSet<CPos>(new[] { new CPos(51, 52) });
			var occupied = new HashSet<CPos>();
			var exactReversedPath = new[]
			{
				destination, new CPos(53, 52), new CPos(52, 53), origin
			};

			Assert.That(ConstructionYardEnclosurePolicy.IsExactReversedRoute(exactReversedPath,
				origin, destination, occupied.Contains, factCells.Contains), Is.True);
			Assert.That(ConstructionYardEnclosurePolicy.IsExactReversedRoute(exactReversedPath.Reverse(),
				origin, destination, occupied.Contains, factCells.Contains), Is.False);

			occupied.Add(new CPos(52, 53));
			Assert.That(ConstructionYardEnclosurePolicy.IsExactReversedRoute(exactReversedPath,
				origin, destination, occupied.Contains, factCells.Contains), Is.False);
			occupied.Clear();
			factCells.Add(new CPos(53, 52));
			Assert.That(ConstructionYardEnclosurePolicy.IsExactReversedRoute(exactReversedPath,
				origin, destination, occupied.Contains, factCells.Contains), Is.False);
		}

		[Test]
		public void WallPreferenceFallsBackWithoutInventingAnotherType()
		{
			var preference = new[] { "brik", "sbag", "cycl" };
			Assert.That(ConstructionYardEnclosurePolicy.FirstAvailableType(preference,
				type => type == "sbag" || type == "cycl"), Is.EqualTo("sbag"));
			Assert.That(ConstructionYardEnclosurePolicy.FirstAvailableType(preference,
				type => type == "wood"), Is.Null);
		}

		[Test]
		public void ReservationOverlapUsesActualBuildingFootprintCells()
		{
			var plan = FactPlan();
			Assert.That(ConstructionYardEnclosurePolicy.Overlaps(plan,
				new[] { new CPos(48, 48), new CPos(49, 49) }), Is.True);
			Assert.That(ConstructionYardEnclosurePolicy.Overlaps(plan,
				new[] { new CPos(50, 50), new CPos(51, 50) }), Is.False);
		}

		[Test]
		public void RandomPlacementFallbackFindsUnreservedCellBeyondBoundedSample()
		{
			var candidates = Enumerable.Range(0, 9).Select(x => new CPos(x, 0)).ToArray();
			var reserved = new HashSet<CPos>(candidates.Take(8));
			Assert.That(ConstructionYardEnclosurePolicy.FirstLegalUnreservedCell(candidates,
				_ => true, reserved.Contains), Is.EqualTo(candidates[8]));
			Assert.That(ConstructionYardEnclosurePolicy.FirstLegalUnreservedCell(candidates,
				_ => true, _ => true), Is.Null);
		}

		[Test]
		public void CutoffAndIdentitySelectionAreLiteralBoundaries()
		{
			Assert.That(ConstructionYardEnclosurePolicy.IsActive(7499, 7500, true, false), Is.True);
			Assert.That(ConstructionYardEnclosurePolicy.IsActive(7500, 7500, true, false), Is.False);
			Assert.That(ConstructionYardEnclosurePolicy.IsActive(1, 7500, true, true), Is.False);
			Assert.That(ConstructionYardEnclosurePolicy.SelectInitialYardActorId(
				new uint[] { 19, 4, 12 }, true), Is.EqualTo(4));
			Assert.That(ConstructionYardEnclosurePolicy.SelectInitialYardActorId(
				new uint[] { 4, 12 }, false), Is.Null);
		}

		[Test]
		public void ExactCutoffTickDeactivatesWithoutWaitingForAnotherMaintenanceInterval()
		{
			const int cutoff = 7500;
			Assert.That(ConstructionYardEnclosurePolicy.IsActive(cutoff - 1, cutoff, true, false), Is.True);
			Assert.That(ConstructionYardEnclosurePolicy.IsActive(cutoff, cutoff, true, false), Is.False);
			Assert.That(ConstructionYardEnclosurePolicy.IsActive(cutoff + 250, cutoff, true, false), Is.False);
		}

		[Test]
		public void SavedPlanRequiresExactOrderedGeometryAndAccess()
		{
			var plan = FactPlan();
			Assert.That(ConstructionYardEnclosurePolicy.MatchesSavedPlan(plan,
				plan.WallCells, plan.AccessCells), Is.True);
			Assert.That(ConstructionYardEnclosurePolicy.MatchesSavedPlan(plan,
				plan.WallCells.Reverse(), plan.AccessCells), Is.False);
			Assert.That(ConstructionYardEnclosurePolicy.MatchesSavedPlan(plan,
				plan.WallCells, plan.AccessCells.Take(2)), Is.False);
		}

		[Test]
		public void SavedCellsRoundTripThroughBoundedIntegerBits()
		{
			var plan = FactPlan();
			var serialized = FieldSaver.FormatValue(ConstructionYardEnclosurePolicy.EncodeCells(plan.WallCells));
			var restored = ConstructionYardEnclosurePolicy.DecodeCells(
				FieldLoader.GetValue<int[]>("WallCellBits", serialized));
			Assert.That(restored, Is.EqualTo(plan.WallCells));
			Assert.That(ConstructionYardEnclosurePolicy.MatchesSavedPlan(
				plan, restored, plan.AccessCells), Is.True);
		}

		[Test]
		public void SavedPendingAndObservedCellsMustBeDistinctBoundedPlanSubsets()
		{
			var plan = FactPlan();
			Assert.That(ConstructionYardEnclosurePolicy.IsValidWallCellSubset(
				plan, plan.WallCells.Take(2), 2), Is.True);
			Assert.That(ConstructionYardEnclosurePolicy.IsValidWallCellSubset(
				plan, new[] { plan.WallCells[0], plan.WallCells[0] }, 2), Is.False);
			Assert.That(ConstructionYardEnclosurePolicy.IsValidWallCellSubset(
				plan, plan.WallCells.Take(3), 2), Is.False);
			Assert.That(ConstructionYardEnclosurePolicy.IsValidWallCellSubset(
				plan, new[] { plan.AccessCells[0] }, 2), Is.False);
		}

		[Test]
		public void PendingEndpointOwnershipSerializesTheExactQueue()
		{
			var firstFactQueue = new object();
			var laterFactQueue = new object();
			var ownership = new ConstructionYardEnclosureBuildOwnership<object>();

			Assert.That(ownership.TryReserve(firstFactQueue, "sbag", 100), Is.True);
			Assert.That(ownership.Owns(firstFactQueue, "sbag"), Is.True);
			Assert.That(ownership.Owns(laterFactQueue, "sbag"), Is.False);
			Assert.That(ownership.TryReserve(laterFactQueue, "sbag", 100), Is.False,
				"A second Fact queue must not request the same pending endpoint.");

			Assert.That(ownership.Refresh(100, 25, _ => true, (_, __) => false), Is.False,
				"The reservation must survive the tick before StartProduction is resolved.");
			Assert.That(ownership.Refresh(200, 25, _ => true,
				(queue, type) => ReferenceEquals(queue, firstFactQueue) && type == "sbag"), Is.False);

			ownership.Release();
			Assert.That(ownership.TryReserve(laterFactQueue, "sbag", 201), Is.True,
				"The next independent endpoint may move to an available queue after placement.");
		}

		[Test]
		public void PendingEndpointOwnershipRestoresOnlyAQueuedMatchingBuild()
		{
			var loadedQueue = new object();
			var ownership = new ConstructionYardEnclosureBuildOwnership<object>();
			Assert.That(ownership.TryRestore(loadedQueue, "sbag", 5900, _ => true,
				(queue, type) => ReferenceEquals(queue, loadedQueue) && type == "sbag"), Is.True);
			Assert.That(ownership.Owns(loadedQueue, "sbag"), Is.True);

			var stale = new ConstructionYardEnclosureBuildOwnership<object>();
			Assert.That(stale.TryRestore(loadedQueue, "sbag", 5900, _ => true, (_, __) => false), Is.False,
				"Stale save data must not redirect an unrelated wall build to the saved endpoint.");
			Assert.That(stale.HasReservation, Is.False);
		}

		[TestCase(0, 0, true)]
		[TestCase(6199, 6200, true)]
		[TestCase(-1, 6200, false)]
		[TestCase(6201, 6200, false)]
		public void SavedOwnershipTicksCannotBeNegativeOrFromTheFuture(
			int savedTick, int currentWorldTick, bool expected)
		{
			Assert.That(ConstructionYardEnclosurePolicy.IsValidSavedTick(savedTick, currentWorldTick),
				Is.EqualTo(expected));
		}

		[TestCase(600, 250, true, 250)]
		[TestCase(40, 250, true, 40)]
		[TestCase(600, 250, false, 600)]
		public void ActiveEnclosureBoundsOnlyLongQueuePollDelays(
			int normalDelay, int maintenanceInterval, bool enclosureActive, int expected)
		{
			Assert.That(ConstructionYardEnclosurePolicy.QueuePollDelay(
				normalDelay, maintenanceInterval, enclosureActive), Is.EqualTo(expected));
		}
	}
}
