#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class EconomyTroopPolicyTest
	{
		[Test]
		public void ReadinessRequiresEconomyScreenArtilleryAntiAirCashAndSafety()
		{
			Assert.That(EconomyTroopPolicy.IsReady(3, 3, 4, 4, 1, 1, 1, 1, 2500, 2500, false), Is.True);
			Assert.That(EconomyTroopPolicy.IsReady(2, 3, 4, 4, 1, 1, 1, 1, 2500, 2500, false), Is.False);
			Assert.That(EconomyTroopPolicy.IsReady(3, 3, 3, 4, 1, 1, 1, 1, 2500, 2500, false), Is.False);
			Assert.That(EconomyTroopPolicy.IsReady(3, 3, 4, 4, 0, 1, 1, 1, 2500, 2500, false), Is.False);
			Assert.That(EconomyTroopPolicy.IsReady(3, 3, 4, 4, 1, 1, 0, 1, 2500, 2500, false), Is.False);
			Assert.That(EconomyTroopPolicy.IsReady(3, 3, 4, 4, 1, 1, 1, 1, 2499, 2500, false), Is.False);
			Assert.That(EconomyTroopPolicy.IsReady(3, 3, 4, 4, 1, 1, 1, 1, 2500, 2500, true), Is.False);
		}

		[Test]
		public void ReadinessCashThresholdSupportsEntryAndMaintenanceHysteresis()
		{
			Assert.That(EconomyTroopPolicy.IsReady(3, 3, 4, 4, 1, 1, 1, 1, 999, 1000, false), Is.False);
			Assert.That(EconomyTroopPolicy.IsReady(3, 3, 4, 4, 1, 1, 1, 1, 1000, 1000, false), Is.True);
			Assert.That(EconomyTroopPolicy.IsReady(3, 3, 4, 4, 1, 1, 1, 1, 0, 0, false), Is.True);
			Assert.That(EconomyTroopPolicy.IsReady(3, 3, 4, 4, 1, 1, 1, 1, 0, 0, true), Is.False);
		}

		[Test]
		public void ReadinessRequiresContinuousObservationAndMaintenance()
		{
			Assert.That(EconomyTroopPolicy.ReadinessDecision(false, false, true, 100, -1, 300),
				Is.EqualTo(EconomyReadinessDecision.NotReady));
			Assert.That(EconomyTroopPolicy.ReadinessDecision(false, true, true, 100, -1, 300),
				Is.EqualTo(EconomyReadinessDecision.Observing));
			Assert.That(EconomyTroopPolicy.ReadinessDecision(false, true, true, 399, 100, 300),
				Is.EqualTo(EconomyReadinessDecision.Observing));
			Assert.That(EconomyTroopPolicy.ReadinessDecision(false, true, true, 400, 100, 300),
				Is.EqualTo(EconomyReadinessDecision.Ready));
			Assert.That(EconomyTroopPolicy.ReadinessDecision(false, true, false, 400, 100, 300),
				Is.EqualTo(EconomyReadinessDecision.NotReady));
			Assert.That(EconomyTroopPolicy.ReadinessDecision(true, false, true, 400, -1, 300),
				Is.EqualTo(EconomyReadinessDecision.Ready));
			Assert.That(EconomyTroopPolicy.ReadinessDecision(true, true, false, 400, -1, 300),
				Is.EqualTo(EconomyReadinessDecision.NotReady));
		}

		[Test]
		public void ReadinessObservationResetsWhenEntryBudgetLapses()
		{
			Assert.That(EconomyTroopPolicy.ReadinessDecision(false, false, true, 399, 100, 300),
				Is.EqualTo(EconomyReadinessDecision.NotReady));
			Assert.That(EconomyTroopPolicy.ReadinessDecision(false, false, true, 400, 100, 300),
				Is.EqualTo(EconomyReadinessDecision.NotReady));
			Assert.That(EconomyTroopPolicy.ReadinessDecision(true, false, true, 400, -1, 300),
				Is.EqualTo(EconomyReadinessDecision.Ready),
				"The lower maintenance budget applies only after readiness has been established.");
		}

		[Test]
		public void MammothPriorityRequiresBothLargestTypeAndTargetValueShare()
		{
			Assert.That(EconomyTroopPolicy.ShouldRequestMammoth(3400, 3200, 6200, 55), Is.True,
				"Largest type is still below the target value share.");
			Assert.That(EconomyTroopPolicy.ShouldRequestMammoth(3400, 3500, 6000, 55), Is.True,
				"Target share alone cannot leave another direct-fire type larger.");
			Assert.That(EconomyTroopPolicy.ShouldRequestMammoth(5100, 2400, 8500, 55), Is.False);
		}

		[TestCase(8, 2, 2, 4, 4)]
		[TestCase(5, 2, 2, 4, 3)]
		[TestCase(3, 2, 2, 4, 0)]
		public void RaidGroupIsCappedAndLeavesMobileReserve(int eligible, int reserve,
			int minimum, int maximum, int expected)
		{
			Assert.That(EconomyTroopPolicy.RaidGroupSize(eligible, reserve, minimum, maximum), Is.EqualTo(expected));
		}

		[Test]
		public void MissionTimeoutAndNoProgressAreIndependentBounds()
		{
			Assert.That(EconomyTroopPolicy.MissionExpired(99, 0, 50, 100, 50), Is.False);
			Assert.That(EconomyTroopPolicy.MissionExpired(100, 0, 90, 100, 50), Is.True);
			Assert.That(EconomyTroopPolicy.MissionExpired(75, 0, 25, 100, 50), Is.True);
			Assert.That(EconomyTroopPolicy.HasProgress(99, 100, 1000, 1000), Is.True);
			Assert.That(EconomyTroopPolicy.HasProgress(100, 100, 999, 1000), Is.True);
			Assert.That(EconomyTroopPolicy.HasProgress(100, 100, 1000, 1000), Is.False);
		}

		[Test]
		public void ExposedTargetRejectsExcessDefenderValue()
		{
			Assert.That(EconomyTroopPolicy.IsExposedTarget(2400, 3200, 75), Is.True);
			Assert.That(EconomyTroopPolicy.IsExposedTarget(2401, 3200, 75), Is.False);
		}

		[Test]
		public void ApproachUsesShortestUsableRangeAndPreservesFallback()
		{
			Assert.That(EconomyTroopPolicy.SelectApproachRange(
				new[] { WDist.FromCells(7), WDist.FromCells(5) }, WDist.FromCells(7)),
				Is.EqualTo(WDist.FromCells(5)));
			Assert.That(EconomyTroopPolicy.SelectApproachRange(
				new[] { WDist.FromCells(7) }, WDist.FromCells(5)), Is.EqualTo(WDist.FromCells(7)));
			Assert.That(EconomyTroopPolicy.SelectApproachRange(
				System.Array.Empty<WDist>(), WDist.FromCells(7)), Is.EqualTo(WDist.FromCells(7)));
		}

		[Test]
		public void CrushCandidateMustRemainNearBoundedFormationRoute()
		{
			var start = new WPos(512, 512, 0);
			var end = new WPos(20 * 1024 + 512, 512, 0);
			Assert.That(EconomyTroopPolicy.IsNearRoute(new WPos(10 * 1024 + 512, 3 * 1024 + 512, 0),
				start, end, WDist.FromCells(3)), Is.True);
			Assert.That(EconomyTroopPolicy.IsNearRoute(new WPos(10 * 1024 + 512, 4 * 1024 + 512, 0),
				start, end, WDist.FromCells(3)), Is.False);
			Assert.That(EconomyTroopPolicy.IsNearRoute(new WPos(25 * 1024 + 512, 512, 0),
				start, end, WDist.FromCells(3)), Is.False);
		}

		[Test]
		public void CrushOrderDoesNotRefreshActiveSameTargetMovement()
		{
			Assert.That(EconomyTroopPolicy.ShouldIssueCrushOrder(575, new uint[] { 575 }), Is.False);
			Assert.That(EconomyTroopPolicy.ShouldIssueCrushOrder(575, new uint[] { 574 }), Is.True);
			Assert.That(EconomyTroopPolicy.ShouldIssueCrushOrder(575, System.Array.Empty<uint>()), Is.True);
		}

		[Test]
		public void CrushMissionTracksObjectiveIdentityRatherThanItsMovingCell()
		{
			Assert.That(EconomyTroopPolicy.IsSameCrushObjective(568, 568), Is.True);
			Assert.That(EconomyTroopPolicy.IsSameCrushObjective(568, 569), Is.False);
			Assert.That(EconomyTroopPolicy.IsSameCrushObjective(0, 0), Is.False);
		}
	}
}
