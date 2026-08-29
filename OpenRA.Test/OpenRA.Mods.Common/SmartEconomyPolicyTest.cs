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
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class SmartEconomyPolicyTest
	{
		[Test]
		public void QueueStallEvidenceRequiresSustainedAbsentPaidProgress()
		{
			var evidence = QueueStallRecoveryPolicy.UpdateNoProgressEvidence(0, true, false, 25);
			Assert.That(evidence, Is.EqualTo(25));
			evidence = QueueStallRecoveryPolicy.UpdateNoProgressEvidence(evidence, true, false, 25);
			Assert.That(evidence, Is.EqualTo(50));
			Assert.That(QueueStallRecoveryPolicy.UpdateNoProgressEvidence(evidence, true, true, 25), Is.Zero,
				"ordinary paid streaming must reset stall evidence even when cash is low");
			Assert.That(QueueStallRecoveryPolicy.UpdateNoProgressEvidence(50, false, false, 25), Is.Zero,
				"ineligible queue states must not retain stale evidence");
		}

		[TestCase(true, 3, true, true, 3, QueueStallRecoveryEligibility.Eligible)]
		[TestCase(false, 3, true, true, 3, QueueStallRecoveryEligibility.LowPower)]
		[TestCase(true, 5, true, true, 3, QueueStallRecoveryEligibility.HarvesterTargetMet)]
		[TestCase(true, 3, false, true, 3, QueueStallRecoveryEligibility.MissingCriticalCandidate)]
		[TestCase(true, 3, true, false, 3, QueueStallRecoveryEligibility.SufficientFunds)]
		[TestCase(true, 3, true, true, 1, QueueStallRecoveryEligibility.InsufficientContention)]
		public void QueueStallEligibilityClassifiesPowerPrerequisiteAndContentionTransitions(
			bool normalPower, int liveHarvesters, bool criticalCandidate, bool cashConstrained, int fronts,
			QueueStallRecoveryEligibility expected)
		{
			Assert.That(QueueStallRecoveryPolicy.ClassifyEconomyObservation(
				normalPower, liveHarvesters, 5, criticalCandidate, cashConstrained, fronts), Is.EqualTo(expected));
		}

		[TestCase(3, true, true, 3, 250, true)]
		[TestCase(5, true, true, 3, 250, false)]
		[TestCase(3, false, true, 3, 250, false)]
		[TestCase(3, true, false, 3, 250, false)]
		[TestCase(3, true, true, 1, 250, false)]
		[TestCase(3, true, true, 3, 249, false)]
		public void QueueStallEconomyGateRequiresBelowFiveCriticalCandidateAndContention(
			int liveHarvesters, bool criticalCandidate, bool cashConstrained,
			int fronts, int evidence, bool expected)
		{
			Assert.That(QueueStallRecoveryPolicy.ShouldRecoverEconomy(liveHarvesters, 5,
				criticalCandidate, cashConstrained, fronts, evidence, 250), Is.EqualTo(expected));
		}

		[TestCase(true, true, false, true)]
		[TestCase(true, false, true, false)]
		[TestCase(false, true, false, false)]
		[TestCase(false, false, true, true)]
		[TestCase(false, true, true, true)]
		public void QueueStallCriticalCandidateRequiresUnloadingPathBeforeHarvester(
			bool usableRefinery, bool harvesterCandidate, bool refineryCandidate, bool expected)
		{
			Assert.That(QueueStallRecoveryPolicy.HasCriticalEconomyCandidate(
				usableRefinery, harvesterCandidate, refineryCandidate), Is.EqualTo(expected));
		}

		[TestCase(true, true, false, QueueStallRecoverySelectedFrontState.Active)]
		[TestCase(true, true, true, QueueStallRecoverySelectedFrontState.CompletedAwaitingExit)]
		[TestCase(false, true, false, QueueStallRecoverySelectedFrontState.Invalidated)]
		[TestCase(true, false, false, QueueStallRecoverySelectedFrontState.Invalidated)]
		public void QueueStallSelectedFrontDistinguishesExitWaitFromInvalidation(bool producerAvailable,
			bool selectedItemIsCurrent, bool selectedItemDone, QueueStallRecoverySelectedFrontState expected)
		{
			Assert.That(QueueStallRecoveryPolicy.ClassifySelectedFront(
				producerAvailable, selectedItemIsCurrent, selectedItemDone), Is.EqualTo(expected));
		}

		[TestCase(QueueStallRecoverySelectedFrontState.Active, false, false)]
		[TestCase(QueueStallRecoverySelectedFrontState.CompletedAwaitingExit, false, true)]
		[TestCase(QueueStallRecoverySelectedFrontState.CompletedAwaitingExit, true, false)]
		[TestCase(QueueStallRecoverySelectedFrontState.Invalidated, false, false)]
		public void QueueStallCompletedFrontIsTrackedOnlyUntilItsActorExits(
			QueueStallRecoverySelectedFrontState state, bool outcomeActorCompleted, bool expected)
		{
			Assert.That(QueueStallRecoveryPolicy.ShouldAwaitSelectedFrontOutcome(state, outcomeActorCompleted),
				Is.EqualTo(expected));
		}

		[TestCase(true, false, true)]
		[TestCase(true, true, true)]
		[TestCase(false, true, true)]
		[TestCase(false, false, false)]
		public void QueueStallDoneExitBlockedFrontPausesOrdinaryProductionUntilActorExits(
			bool active, bool awaitingSelectedExit, bool expected)
		{
			Assert.That(QueueStallRecoveryPolicy.ShouldPauseOrdinaryProduction(active, awaitingSelectedExit),
				Is.EqualTo(expected));
		}

		[TestCase(false, true, true, QueueStallRecoveryConstructionChoice.None)]
		[TestCase(true, true, true, QueueStallRecoveryConstructionChoice.ConstructionYardEnclosure)]
		[TestCase(true, false, true, QueueStallRecoveryConstructionChoice.NeedBasedSilo)]
		[TestCase(true, false, false, QueueStallRecoveryConstructionChoice.None)]
		public void QueueRecoveryAllowsOnlyEnclosureThenNeedBasedSilo(bool active,
			bool enclosureAvailable, bool siloAvailable,
			QueueStallRecoveryConstructionChoice expected)
		{
			Assert.That(QueueStallRecoveryPolicy.ChooseProtectedConstruction(active,
				enclosureAvailable, siloAvailable), Is.EqualTo(expected));
		}

		[TestCase(true, true, 1000, 1000, true)]
		[TestCase(true, true, 999, 1000, false)]
		[TestCase(true, false, 1000, 1000, false)]
		[TestCase(false, true, 1000, 1000, false)]
		public void AffordableCurrentOpeningResearchSurvivesQueueRecovery(bool current,
			bool buildable, int funds, int remaining, bool expected)
		{
			Assert.That(QueueStallRecoveryPolicy.ShouldProtectOpeningResearch(
				current, buildable, funds, remaining), Is.EqualTo(expected));
		}

		[Test]
		public void PostLoadSettlementDelaysOneSamplerWithoutOverflow()
		{
			Assert.That(SmartEconomyPolicy.PostLoadResumeTick(1303, 25), Is.EqualTo(1328));
			Assert.That(SmartEconomyPolicy.PostLoadResumeTick(1303, 0), Is.EqualTo(1304));
			Assert.That(SmartEconomyPolicy.PostLoadResumeTick(int.MaxValue - 5, 25), Is.EqualTo(int.MaxValue));
		}

		[Test]
		public void PressureRequiresSustainedEvidenceAndReleasesWithHysteresis()
		{
			var pressure = new SmartEconomyPressure(0, false);
			pressure = SmartEconomyPolicy.UpdatePressure(pressure, true, 25, 100, 25);
			Assert.That(pressure.Active, Is.False);

			pressure = SmartEconomyPolicy.UpdatePressure(pressure, true, 75, 100, 25);
			Assert.That(pressure.Active, Is.True);
			Assert.That(pressure.EvidenceTicks, Is.EqualTo(100));

			pressure = SmartEconomyPolicy.UpdatePressure(pressure, false, 50, 100, 25);
			Assert.That(pressure.Active, Is.True);

			pressure = SmartEconomyPolicy.UpdatePressure(pressure, false, 25, 100, 25);
			Assert.That(pressure.Active, Is.False);
		}

		[TestCase(0, 0)]
		[TestCase(1, 0)]
		[TestCase(2, 1)]
		[TestCase(4, 3)]
		public void WaitingHarvestersExcludeTheActiveServiceSlot(int linked, int expected)
		{
			Assert.That(SmartEconomyPolicy.WaitingHarvesters(linked), Is.EqualTo(expected));
		}

		[Test]
		public void CongestionRequiresEveryRefineryToBeOccupied()
		{
			Assert.That(SmartEconomyPolicy.WaitingHarvestersWhenAllRefineriesOccupied(Array.Empty<int>()), Is.Zero);
			Assert.That(SmartEconomyPolicy.WaitingHarvestersWhenAllRefineriesOccupied(new[] { 3, 0 }), Is.Zero);
			Assert.That(SmartEconomyPolicy.WaitingHarvestersWhenAllRefineriesOccupied(new[] { 1, 1 }), Is.Zero);
			Assert.That(SmartEconomyPolicy.WaitingHarvestersWhenAllRefineriesOccupied(new[] { 3, 1 }), Is.EqualTo(2));
			Assert.That(SmartEconomyPolicy.WaitingHarvestersWhenAllRefineriesOccupied(new[] { 2, 2 }), Is.EqualTo(2));
		}

		[Test]
		public void StoragePressureUsesCapacityAndHandlesMissingStorage()
		{
			Assert.That(SmartEconomyPolicy.StoragePressure(799, 1000, 80), Is.False);
			Assert.That(SmartEconomyPolicy.StoragePressure(800, 1000, 80), Is.True);
			Assert.That(SmartEconomyPolicy.StoragePressure(100, 0, 80), Is.False);
		}

		[Test]
		public void NeedBasedSiloRequiresAnExplicitOptIn()
		{
			Assert.That(SmartEconomyPolicy.WantsNeedBasedSilo(false, 800, 1000, 80), Is.False);
			Assert.That(SmartEconomyPolicy.WantsNeedBasedSilo(true, 799, 1000, 80), Is.False);
			Assert.That(SmartEconomyPolicy.WantsNeedBasedSilo(true, 800, 1000, 80), Is.True);
		}

		[Test]
		public void NeedBasedSiloClaimsOnlyAnActionableFreeUnownedQueueBoundary()
		{
			Assert.That(SmartEconomyPolicy.CanClaimNeedBasedSiloQueue(true, false, true, false), Is.True);
			Assert.That(SmartEconomyPolicy.CanClaimNeedBasedSiloQueue(false, false, true, false), Is.False,
				"No-pressure construction must retain ordinary tower selection.");
			Assert.That(SmartEconomyPolicy.CanClaimNeedBasedSiloQueue(true, true, true, false), Is.False,
				"Pressure must not cancel or displace production that already owns the queue.");
			Assert.That(SmartEconomyPolicy.CanClaimNeedBasedSiloQueue(true, false, false, false), Is.False,
				"An unavailable or unaffordable Silo must not create a phantom commitment.");
			Assert.That(SmartEconomyPolicy.CanClaimNeedBasedSiloQueue(true, false, true, true), Is.False,
				"Parallel Facts must not duplicate one unresolved Silo commitment.");
		}

		[Test]
		public void RefineryDemandCountsQueuesRequestsAndFreePendingHarvesters()
		{
			var demand = SmartEconomyPolicy.RefineryDemand(
				10, 1, 1, 2, 1, 0, 1, 3, 3);

			Assert.That(demand.CommittedHarvesters, Is.EqualTo(13));
			Assert.That(demand.CommittedRefineries, Is.EqualTo(3));
			Assert.That(demand.DesiredRefineries, Is.EqualTo(5));
			Assert.That(demand.Deficit, Is.EqualTo(2));
			Assert.That(demand.AvailableRequests, Is.EqualTo(2));
		}

		[Test]
		public void ParallelRefineryDemandIsBoundedAndCannotRepeatCommittedWork()
		{
			var initial = SmartEconomyPolicy.RefineryDemand(30, 0, 0, 2, 0, 0, 1, 3, 3);
			Assert.That(initial.DesiredRefineries, Is.EqualTo(10));
			Assert.That(initial.AvailableRequests, Is.EqualTo(3));

			var reserved = SmartEconomyPolicy.RefineryDemand(30, 0, 0, 2, 0, 3, 1, 3, 3);
			Assert.That(reserved.CommittedHarvesters, Is.EqualTo(33));
			Assert.That(reserved.CommittedRefineries, Is.EqualTo(5));
			Assert.That(reserved.AvailableRequests, Is.Zero);
		}

		[Test]
		public void RefineryDemandIsNotCappedByAnAuthoredBuildingLimit()
		{
			var demand = SmartEconomyPolicy.RefineryDemand(75, 0, 0, 24, 0, 0, 1, 2, 3);
			Assert.That(demand.DesiredRefineries, Is.EqualTo(38));
			Assert.That(demand.AvailableRequests, Is.EqualTo(3));
		}

		[Test]
		public void SustainedCongestionRequestsOneAdditionalUnloadingPointAtATime()
		{
			var demand = SmartEconomyPolicy.RefineryDemand(4, 0, 0, 2, 0, 0, 1, 2, 3, true);
			Assert.That(demand.DesiredRefineries, Is.EqualTo(3));
			Assert.That(demand.AvailableRequests, Is.EqualTo(1));

			var queued = SmartEconomyPolicy.RefineryDemand(4, 0, 0, 2, 1, 0, 1, 2, 3, true);
			Assert.That(queued.AvailableRequests, Is.Zero);
		}

		[Test]
		public void CashPauseCoversOnlyAnUnfundedFirstRefinery()
		{
			Assert.That(SmartEconomyPolicy.RefineryCashShortfall(1499, 1500, 0, 0, 0, 1), Is.EqualTo(1));
			Assert.That(SmartEconomyPolicy.RefineryCashShortfall(1500, 1500, 0, 0, 0, 1), Is.Zero);
			Assert.That(SmartEconomyPolicy.RefineryCashShortfall(0, 1500, 1, 0, 0, 1), Is.Zero,
				"A throughput deficit is not a missing critical refinery.");
			Assert.That(SmartEconomyPolicy.RefineryCashShortfall(0, 1500, 0, 1, 0, 1), Is.Zero);
			Assert.That(SmartEconomyPolicy.RefineryCashShortfall(0, 1500, 0, 0, 1, 1), Is.Zero);
			Assert.That(SmartEconomyPolicy.RefineryCashShortfall(0, 1500, 0, 0, 0, 0), Is.Zero,
				"Busy Facts cannot spend the reserved cash, so combat production must continue.");
		}

		[Test]
		public void SmartEconomySerializesOnlyPostEstablishmentRefineryRecovery()
		{
			Assert.That(SmartEconomyPolicy.NeedsSerializedRefineryRecovery(true, false, 0), Is.False,
				"An ordinary fresh start must remain inside the protected opening prefix.");
			Assert.That(SmartEconomyPolicy.NeedsSerializedRefineryRecovery(true, true, 0), Is.True);
			Assert.That(SmartEconomyPolicy.NeedsSerializedRefineryRecovery(true, true, 0, 1, 0), Is.False);
			Assert.That(SmartEconomyPolicy.NeedsSerializedRefineryRecovery(true, true, 0, 0, 1), Is.False);
			Assert.That(SmartEconomyPolicy.NeedsSerializedRefineryRecovery(true, true, 1), Is.False);
			Assert.That(SmartEconomyPolicy.NeedsSerializedRefineryRecovery(false, true, 0), Is.False,
				"Feature-disabled controls retain their legacy behavior.");
		}

		[Test]
		public void OpeningCanOwnTheFirstRefineryWithoutClaimingRecovery()
		{
			Assert.That(SmartEconomyPolicy.NeedsFirstRefineryCommitment(true, 0, 0, 0), Is.True);
			Assert.That(SmartEconomyPolicy.NeedsFirstRefineryCommitment(true, 0, 1, 0), Is.False);
			Assert.That(SmartEconomyPolicy.NeedsFirstRefineryCommitment(true, 0, 0, 1), Is.False);
			Assert.That(SmartEconomyPolicy.NeedsFirstRefineryCommitment(true, 1, 0, 0), Is.False);
		}

		[Test]
		public void ParallelReservationsUseOnlyUncommittedCash()
		{
			Assert.That(SmartEconomyPolicy.CanFundRefinery(4500, 1500, 1500, 1500), Is.True);
			Assert.That(SmartEconomyPolicy.CanFundRefinery(4499, 1500, 1500, 1500), Is.False);
		}

		[Test]
		public void OneThroughputRefineryMayStreamButParallelWorkMustBeFunded()
		{
			Assert.That(SmartEconomyPolicy.CanStartThroughputRefinery(0, 0, 0, 1500, 0), Is.True);
			Assert.That(SmartEconomyPolicy.CanStartThroughputRefinery(1499, 0, 0, 1500, 1), Is.False);
			Assert.That(SmartEconomyPolicy.CanStartThroughputRefinery(3000, 1500, 0, 1500, 1), Is.True);
		}

		[Test]
		public void EarlyVehicleCapacityUsesConfiguredShareOfFacts()
		{
			Assert.That(SmartEconomyPolicy.DesiredEarlyVehicleFactories(0, 50), Is.Zero);
			Assert.That(SmartEconomyPolicy.DesiredEarlyVehicleFactories(1, 50), Is.EqualTo(1));
			Assert.That(SmartEconomyPolicy.DesiredEarlyVehicleFactories(2, 50), Is.EqualTo(1));
			Assert.That(SmartEconomyPolicy.DesiredEarlyVehicleFactories(3, 50), Is.EqualTo(2));
			Assert.That(SmartEconomyPolicy.DesiredEarlyVehicleFactories(4, 50), Is.EqualTo(2));
			Assert.That(SmartEconomyPolicy.DesiredEarlyVehicleFactories(4, 25), Is.EqualTo(1));
			Assert.That(SmartEconomyPolicy.DesiredEarlyVehicleFactories(4, 75), Is.EqualTo(3));
		}

		[Test]
		public void VehicleCapacityTracksTheConfiguredRefineryConstructionBalance()
		{
			Assert.That(SmartEconomyPolicy.DesiredVehicleFactoriesForRefineryBalance(0, 50), Is.Zero);
			Assert.That(SmartEconomyPolicy.DesiredVehicleFactoriesForRefineryBalance(1, 50), Is.EqualTo(1));
			Assert.That(SmartEconomyPolicy.DesiredVehicleFactoriesForRefineryBalance(3, 50), Is.EqualTo(3));
			Assert.That(SmartEconomyPolicy.DesiredVehicleFactoriesForRefineryBalance(3, 25), Is.EqualTo(1));
			Assert.That(SmartEconomyPolicy.DesiredVehicleFactoriesForRefineryBalance(3, 75), Is.EqualTo(9));
			Assert.That(SmartEconomyPolicy.DesiredVehicleFactoriesForRefineryBalance(3, 0), Is.Zero);
			Assert.That(SmartEconomyPolicy.DesiredVehicleFactoriesForRefineryBalance(3, 100), Is.EqualTo(int.MaxValue));
		}

		[Test]
		public void ThroughputWorkLeavesConfiguredFactShareForAlternativeConstruction()
		{
			Assert.That(SmartEconomyPolicy.EffectiveParallelRefineryLimit(4, 3, 50, false), Is.EqualTo(3));
			Assert.That(SmartEconomyPolicy.EffectiveParallelRefineryLimit(4, 3, 50, true), Is.EqualTo(2));
			Assert.That(SmartEconomyPolicy.EffectiveParallelRefineryLimit(3, 3, 50, true), Is.EqualTo(1));
			Assert.That(SmartEconomyPolicy.EffectiveParallelRefineryLimit(2, 3, 50, true), Is.EqualTo(1));
			Assert.That(SmartEconomyPolicy.EffectiveParallelRefineryLimit(1, 3, 50, true), Is.Zero);
			Assert.That(SmartEconomyPolicy.EffectiveParallelRefineryLimit(12, 3, 50, true), Is.EqualTo(3));
		}

		[Test]
		public void ExcessCashRaisesExpansionTargetToConfiguredCeiling()
		{
			Assert.That(SmartEconomyPolicy.DesiredExpansionAssets(34999, 35000, 8), Is.Zero);
			Assert.That(SmartEconomyPolicy.DesiredExpansionAssets(35000, 35000, 8), Is.EqualTo(2));
			Assert.That(SmartEconomyPolicy.DesiredExpansionAssets(70000, 35000, 8), Is.EqualTo(3));
			Assert.That(SmartEconomyPolicy.DesiredExpansionAssets(105000, 35000, 8), Is.EqualTo(4));
			Assert.That(SmartEconomyPolicy.DesiredExpansionAssets(210000, 35000, 8), Is.EqualTo(7));
			Assert.That(SmartEconomyPolicy.DesiredExpansionAssets(999999, 35000, 8), Is.EqualTo(8));
		}

		[Test]
		public void ExpansionWaitsForConfiguredArmySupport()
		{
			Assert.That(SmartEconomyPolicy.ExpansionArmyReady(3319, 34570, 20), Is.False);
			Assert.That(SmartEconomyPolicy.ExpansionArmyReady(6914, 34570, 20), Is.True);
			Assert.That(SmartEconomyPolicy.ExpansionArmyReady(0, 34570, 0), Is.True);
			Assert.That(SmartEconomyPolicy.ExpansionArmyReady(0, 0, 20), Is.True);
		}
	}
}
