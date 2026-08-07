#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class TiberiumFieldPolicyTest
	{
		[Test]
		public void SavedSpatialIdentityRequiresLiveTreeExactFootprintAndEffectRange()
		{
			var tree = new CPos(10, 10);
			var footprint = new[] { new CPos(14, 10), new CPos(15, 10) };
			Assert.That(TiberiumFieldPolicy.IsValidSavedSpatialIdentity(tree, tree, footprint,
				c => c.X >= 0 && c.X < 20 && c.Y >= 0 && c.Y < 20, 25, 36), Is.True);
			Assert.That(TiberiumFieldPolicy.IsValidSavedSpatialIdentity(tree, new CPos(11, 10),
				footprint, c => true, 25, 36), Is.False);
			Assert.That(TiberiumFieldPolicy.IsValidSavedSpatialIdentity(tree, tree,
				new[] { new CPos(19, 10), new CPos(20, 10) }, c => c.X < 20, 25, 36), Is.False);
			Assert.That(TiberiumFieldPolicy.IsValidSavedSpatialIdentity(tree, tree, footprint,
				c => true, 37, 36), Is.False);
		}

		[Test]
		public void RouteZonesDistinguishTheExactGateFromFieldInterior()
		{
			var walls = new[]
			{
				new CPos(1, 1), new CPos(2, 1), new CPos(3, 1),
				new CPos(1, 2), new CPos(1, 3), new CPos(3, 3)
			};
			var gate = new[] { new CPos(3, 2) };
			Assert.That(TiberiumFieldPolicy.RouteZone(new CPos(3, 2), walls, gate),
				Is.EqualTo(TiberiumFieldRouteZone.Gate));
			Assert.That(TiberiumFieldPolicy.RouteZone(new CPos(2, 2), walls, gate),
				Is.EqualTo(TiberiumFieldRouteZone.Inside));
			Assert.That(TiberiumFieldPolicy.RouteZone(new CPos(4, 2), walls, gate),
				Is.EqualTo(TiberiumFieldRouteZone.Outside));
		}

		[Test]
		public void GatePathMustCrossTheEntranceInBothDirections()
		{
			var gate = new[] { new CPos(3, 2), new CPos(3, 3) };
			var outbound = new[] { new CPos(5, 2), gate[0], new CPos(2, 2) };
			var inbound = new[] { new CPos(2, 2), gate[1], new CPos(5, 2) };
			Assert.That(TiberiumFieldPolicy.IsBidirectionalGatePath(outbound, inbound, gate), Is.True);
			Assert.That(TiberiumFieldPolicy.IsBidirectionalGatePath(outbound,
				new[] { new CPos(2, 2), new CPos(2, 3) }, gate), Is.False);
		}

		[Test]
		public void LiveRoundTripRequiresRefineryGateHarvestGateAndUnloadInOrder()
		{
			var stage = TiberiumFieldRoundTripStage.AwaitingRefinery;
			stage = TiberiumFieldPolicy.AdvanceRoundTrip(stage,
				TiberiumFieldRouteZone.Outside, true, false, false);
			Assert.That(stage, Is.EqualTo(TiberiumFieldRoundTripStage.Outbound));
			stage = TiberiumFieldPolicy.AdvanceRoundTrip(stage,
				TiberiumFieldRouteZone.Inside, false, true, false);
			Assert.That(stage, Is.EqualTo(TiberiumFieldRoundTripStage.Outbound),
				"Skipping the planned entrance must not count.");
			stage = TiberiumFieldPolicy.AdvanceRoundTrip(stage,
				TiberiumFieldRouteZone.Gate, false, false, false);
			stage = TiberiumFieldPolicy.AdvanceRoundTrip(stage,
				TiberiumFieldRouteZone.Inside, false, false, false);
			stage = TiberiumFieldPolicy.AdvanceRoundTrip(stage,
				TiberiumFieldRouteZone.Inside, false, true, false);
			stage = TiberiumFieldPolicy.AdvanceRoundTrip(stage,
				TiberiumFieldRouteZone.Gate, false, false, false);
			stage = TiberiumFieldPolicy.AdvanceRoundTrip(stage,
				TiberiumFieldRouteZone.Outside, false, false, false);
			stage = TiberiumFieldPolicy.AdvanceRoundTrip(stage,
				TiberiumFieldRouteZone.Outside, false, false, true);
			Assert.That(stage, Is.EqualTo(TiberiumFieldRoundTripStage.Complete));
		}

		[Test]
		public void SavedTerminalSegmentCursorRequiresCompletedActivationEligibleEnclosure()
		{
			Assert.That(TiberiumFieldPolicy.IsValidSavedSegmentCursor(4, 5, false, false), Is.True);
			Assert.That(TiberiumFieldPolicy.IsValidSavedSegmentCursor(5, 5, true, true), Is.True);
			Assert.That(TiberiumFieldPolicy.IsValidSavedSegmentCursor(5, 5, false, true), Is.False);
			Assert.That(TiberiumFieldPolicy.IsValidSavedSegmentCursor(5, 5, true, false), Is.False);
			Assert.That(TiberiumFieldPolicy.IsValidSavedSegmentCursor(6, 5, true, true), Is.False);
		}

		[Test]
		public void SavedPerimeterValidationRejectsGateOverlapAndIncompleteSegments()
		{
			var walls = new[] { new CPos(1, 1), new CPos(2, 1), new CPos(3, 1) };
			var gate = new[] { new CPos(4, 1), new CPos(5, 1) };
			Assert.That(TiberiumFieldPolicy.IsValidSavedPerimeter(walls, gate,
				new[] { walls }), Is.True);
			Assert.That(TiberiumFieldPolicy.IsValidSavedPerimeter(walls,
				new[] { new CPos(3, 1) }, new[] { walls }), Is.False);
			Assert.That(TiberiumFieldPolicy.IsValidSavedPerimeter(walls, gate,
				new[] { walls.Take(2) }), Is.False);
		}

		[Test]
		public void SavedPerimeterMustMatchExactConfiguredGeometry()
		{
			var footprint = new[] { new CPos(14, 10), new CPos(15, 10) };
			var plan = TiberiumFieldPolicy.PlanRedPerimeter(new CPos(10, 10),
				footprint, new CPos(20, 10), 4);
			Assert.That(TiberiumFieldPolicy.SavedPerimeterMatchesPlan(plan,
				plan.WallCells, plan.GateCells, plan.WallSegments), Is.True);

			var shifted = TiberiumFieldPolicy.PlanRedPerimeter(new CPos(11, 10),
				footprint, new CPos(20, 10), 4);
			Assert.That(TiberiumFieldPolicy.SavedPerimeterMatchesPlan(shifted,
				plan.WallCells, plan.GateCells, plan.WallSegments), Is.False);
			Assert.That(TiberiumFieldPolicy.SavedPerimeterMatchesPlan(plan,
				plan.WallCells, plan.GateCells.Reverse(), plan.WallSegments), Is.False);
			Assert.That(TiberiumFieldPolicy.SavedPerimeterMatchesPlan(plan,
				plan.WallCells, plan.GateCells, plan.WallSegments.Reverse()), Is.False);
		}

		[Test]
		public void MissingMaintenanceCellsExcludeEveryPresentPlannedCell()
		{
			var planned = new[]
			{
				new CPos(1, 1), new CPos(2, 1), new CPos(3, 1), new CPos(4, 1)
			};
			var missing = TiberiumFieldPolicy.MissingPlannedCells(planned,
				new[] { new CPos(1, 1), new CPos(3, 1), new CPos(9, 9) });

			Assert.That(missing, Is.EqualTo(new[] { new CPos(2, 1), new CPos(4, 1) }));
		}

		[Test]
		public void ExistingCompleteSegmentsAdvanceOnlyToFirstLiveWorldGap()
		{
			var segments = new[]
			{
				new[] { new CPos(1, 1), new CPos(2, 1) },
				new[] { new CPos(3, 1), new CPos(4, 1) },
				new[] { new CPos(5, 1) }
			};
			Assert.That(TiberiumFieldPolicy.FirstIncompleteSegmentIndex(segments,
				new[] { new CPos(1, 1), new CPos(2, 1), new CPos(3, 1), new CPos(5, 1) }, 0),
				Is.EqualTo(1));
			Assert.That(TiberiumFieldPolicy.FirstIncompleteSegmentIndex(segments,
				segments.SelectMany(s => s), 0), Is.EqualTo(3));
			Assert.That(TiberiumFieldPolicy.FirstIncompleteSegmentIndex(segments,
				segments.SelectMany(s => s), 2), Is.EqualTo(3));
		}

		[Test]
		public void WallRetargetStaysOnPlannedCellsAndSkipsReservedCell()
		{
			var planned = new[] { new CPos(10, 4), new CPos(11, 4), new CPos(12, 4) };
			var selected = TiberiumFieldPolicy.FirstLegalAlternativeCell(planned,
				new CPos(10, 4), c => c == new CPos(12, 4));

			Assert.That(selected, Is.EqualTo(new CPos(12, 4)));
			Assert.That(TiberiumFieldPolicy.FirstLegalAlternativeCell(planned,
				new CPos(10, 4), c => false), Is.Null);
		}

		[Test]
		public void CoverageIsOneToOneAndDeterministic()
		{
			var selected = TiberiumFieldPolicy.SelectOneToOneCoverage(new[]
			{
				new TiberiumFieldCoverageCandidate(2, 12, 4),
				new TiberiumFieldCoverageCandidate(1, 12, 1),
				new TiberiumFieldCoverageCandidate(1, 11, 4),
				new TiberiumFieldCoverageCandidate(2, 11, 9)
			});

			Assert.That(selected, Has.Length.EqualTo(2));
			Assert.That(selected[0].TreeActorId, Is.EqualTo(1));
			Assert.That(selected[0].ResonatorActorId, Is.EqualTo(12));
			Assert.That(selected[1].TreeActorId, Is.EqualTo(2));
			Assert.That(selected[1].ResonatorActorId, Is.EqualTo(11));
		}

		[Test]
		public void ProjectRankingUsesRouteDemandSafetyCommitmentThenIdentity()
		{
			var selected = TiberiumFieldPolicy.BestProject(new[]
			{
				new TiberiumFieldProjectCandidate(1, false, 100, 100, 1),
				new TiberiumFieldProjectCandidate(4, true, 10, 4, 1000),
				new TiberiumFieldProjectCandidate(3, true, 10, 5, 1000),
				new TiberiumFieldProjectCandidate(2, true, 10, 5, 500)
			});

			Assert.That(selected.HasValue, Is.True);
			Assert.That(selected.Value.TreeActorId, Is.EqualTo(2));
		}

		[Test]
		public void ExtensionRankingPrefersUsefulConfiguredStepDeterministically()
		{
			var selected = TiberiumFieldPolicy.BestExtensionCell(new[]
			{
				new TiberiumFieldExtensionCandidate(new CPos(9, 9), 0, 1),
				new TiberiumFieldExtensionCandidate(new CPos(8, 8), 4, 16),
				new TiberiumFieldExtensionCandidate(new CPos(7, 7), 7, 25),
				new TiberiumFieldExtensionCandidate(new CPos(6, 7), 5, 25)
			}, 6);

			Assert.That(selected.HasValue, Is.True);
			Assert.That(selected.Value.Cell, Is.EqualTo(new CPos(6, 7)));
			Assert.That(TiberiumFieldPolicy.BestExtensionCell(new[]
			{
				new TiberiumFieldExtensionCandidate(new CPos(1, 1), 0, 1)
			}, 6), Is.Null);
		}

		[Test]
		public void AdmissionProtectsEveryCriticalOwnerAndReservedCash()
		{
			Assert.That(TiberiumFieldPolicy.CanAdmit(true, true, false, false,
				true, true, true, true, 6500, 5000, 1500), Is.True);
			Assert.That(TiberiumFieldPolicy.CanAdmit(true, true, true, false,
				true, true, true, true, 6500, 5000, 1500), Is.False);
			Assert.That(TiberiumFieldPolicy.CanAdmit(true, true, false, true,
				true, true, true, true, 6500, 5000, 1500), Is.False);
			Assert.That(TiberiumFieldPolicy.CanAdmit(true, true, false, false,
				false, true, true, true, 6500, 5000, 1500), Is.False);
			Assert.That(TiberiumFieldPolicy.CanAdmit(true, true, false, false,
				true, true, true, true, 6499, 5000, 1500), Is.False);
			Assert.That(TiberiumFieldPolicy.EvaluateAdmission(true, true, true, false,
				true, true, true, true, 6500, 5000, 1500),
				Is.EqualTo(TiberiumFieldAdmissionResult.OpeningActive));
			Assert.That(TiberiumFieldPolicy.EvaluateAdmission(true, true, false, false,
				true, true, true, true, 6499, 5000, 1500),
				Is.EqualTo(TiberiumFieldAdmissionResult.InsufficientCash));
		}

		[Test]
		public void DeadlineArithmeticClampsAndHonorsBoundaries()
		{
			Assert.That(TiberiumFieldPolicy.NextDeadline(100, 0), Is.EqualTo(101));
			Assert.That(TiberiumFieldPolicy.NextDeadline(int.MaxValue - 5, 1500), Is.EqualTo(int.MaxValue));
			Assert.That(TiberiumFieldPolicy.DeadlineReached(99, 100), Is.False);
			Assert.That(TiberiumFieldPolicy.DeadlineReached(100, 100), Is.True);
		}

		[Test]
		public void NoProgressDeferralWaitsForCadenceAndDoesNotMaskAdmission()
		{
			Assert.That(TiberiumFieldPolicy.ShouldDeferNoProgress(1499, 1500, false, false), Is.False);
			Assert.That(TiberiumFieldPolicy.ShouldDeferNoProgress(1500, 1500, false, false), Is.True);
			Assert.That(TiberiumFieldPolicy.ShouldDeferNoProgress(1500, 1500, true, false), Is.False);
			Assert.That(TiberiumFieldPolicy.ShouldDeferNoProgress(1500, 1500, false, true), Is.False);
		}

		[Test]
		public void WallOrderBudgetCountsBothLineBuildEndpointsAndRetainedProgress()
		{
			Assert.That(TiberiumFieldPolicy.RemainingWallOrders(5, 0, 0), Is.EqualTo(10));
			Assert.That(TiberiumFieldPolicy.RemainingWallOrders(5, 0, 1), Is.EqualTo(9));
			Assert.That(TiberiumFieldPolicy.RemainingWallOrders(5, 0, 2, 6), Is.EqualTo(14));
			Assert.That(TiberiumFieldPolicy.RemainingWallOrders(5, 2, 0), Is.EqualTo(6));
			Assert.That(TiberiumFieldPolicy.RemainingWallOrders(5, 5, 0), Is.Zero);
		}

		[Test]
		public void LiveWorldWallCommitmentCountsEndpointsOrRetainedGapsOnly()
		{
			var segments = new[]
			{
				new[] { new CPos(1, 1), new CPos(2, 1), new CPos(3, 1) },
				new[] { new CPos(3, 1), new CPos(3, 2), new CPos(3, 3) }
			};
			Assert.That(TiberiumFieldPolicy.RemainingWallOrdersFromWorld(segments,
				Array.Empty<CPos>()), Is.EqualTo(4));
			Assert.That(TiberiumFieldPolicy.RemainingWallOrdersFromWorld(segments,
				new[] { new CPos(1, 1), new CPos(3, 1), new CPos(3, 3) }), Is.EqualTo(2));
			Assert.That(TiberiumFieldPolicy.RemainingWallOrdersFromWorld(segments,
				segments.SelectMany(s => s)), Is.Zero);
		}

		[Test]
		public void RedPerimeterContainsTreeAndFootprintWithTwoCellGateTowardAnchor()
		{
			var plan = TiberiumFieldPolicy.PlanRedPerimeter(new CPos(10, 10),
				new[] { new CPos(12, 10), new CPos(13, 10) }, new CPos(30, 10), 4);

			Assert.That(plan, Is.Not.Null);
			Assert.That(plan.GateCells, Is.EqualTo(new[] { new CPos(17, 10), new CPos(17, 11) }));
			Assert.That(plan.WallCells, Does.Not.Contain(plan.GateCells[0]));
			Assert.That(plan.WallCells, Does.Not.Contain(plan.GateCells[1]));
			Assert.That(plan.WallCells, Does.Contain(new CPos(6, 6)));
			Assert.That(plan.WallCells, Does.Contain(new CPos(17, 14)));
			Assert.That(plan.WallCells, Has.Length.EqualTo(36));
			Assert.That(plan.WallSegments, Has.Length.EqualTo(5));
			Assert.That(plan.WallSegments.SelectMany(s => s).Distinct().Count(), Is.EqualTo(36));
		}
	}
}
