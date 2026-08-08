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

using System.Collections.Generic;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class DefenseClusterPolicyTest
	{
		[Test]
		public void WallSaleRequiresClusterProvenanceAndNeverUsesProtectedEnclosureCells()
		{
			Assert.That(DefenseClusterPolicy.IsSellableWallPurpose(false, true, false), Is.True,
				"a legacy local wall captured when the cluster activates may be sold after a causal check");
			Assert.That(DefenseClusterPolicy.IsSellableWallPurpose(false, false, true), Is.True,
				"a wall planned for the active cluster keeps its explicit provenance");
			Assert.That(DefenseClusterPolicy.IsSellableWallPurpose(false, false, false), Is.False,
				"an unrelated owned wall must not become sellable merely because it is nearby");
			Assert.That(DefenseClusterPolicy.IsSellableWallPurpose(true, true, true), Is.False,
				"the first construction-yard enclosure remains protected regardless of other provenance");
		}

		[Test]
		public void FirstHitAnchorsAndLeaseKeepsOnePendingCandidate()
		{
			var state = new DefenseClusterPolicyState();
			Assert.That(state.ObserveTowerAttack(10, 100, 50, false), Is.True);
			Assert.That(state.AnchorActorId, Is.EqualTo(10));

			Assert.That(state.ObserveTowerAttack(20, 110, 50, false), Is.False);
			Assert.That(state.AnchorActorId, Is.EqualTo(10));
			Assert.That(state.PendingActorId, Is.EqualTo(20));

			state.ObserveTowerAttack(30, 120, 50, false);
			Assert.That(state.PendingActorId, Is.EqualTo(30));
			Assert.That(state.TryPromotePending(149, 50, false), Is.False);
			Assert.That(state.TryPromotePending(150, 50, false), Is.True);
			Assert.That(state.AnchorActorId, Is.EqualTo(30));
		}

		[Test]
		public void SameAnchorRetainsIdentityAndTiePrefersCurrent()
		{
			var state = new DefenseClusterPolicyState();
			state.ObserveTowerAttack(10, 100, 25, false);
			state.ObserveTowerAttack(20, 100, 25, false);
			Assert.That(state.TryPromotePending(125, 25, false), Is.False,
				"a candidate not attacked more recently must not replace the anchor");

			state.ObserveTowerAttack(10, 130, 25, false);
			Assert.That(state.AnchorActorId, Is.EqualTo(10));
			Assert.That(state.AnchorAttackTick, Is.EqualTo(130));
		}

		[Test]
		public void NearbyTowerPressureRetainsTheStrongpointAndSupersedesAnOlderPendingHit()
		{
			Assert.That(DefenseClusterPolicy.IsWithinCluster(new CPos(10, 10), new CPos(19, 10), 9), Is.True);
			Assert.That(DefenseClusterPolicy.IsWithinCluster(new CPos(10, 10), new CPos(20, 10), 9), Is.False);

			var state = new DefenseClusterPolicyState();
			state.ObserveTowerAttack(10, 100, 50, false);
			state.ObserveTowerAttack(20, 110, 50, false);
			state.ObserveActiveClusterPressure(120);

			Assert.That(state.TryPromotePending(150, 50, false), Is.False,
				"older distant pressure must not displace a more recently attacked active strongpoint");
			Assert.That(state.AnchorActorId, Is.EqualTo(10));
			Assert.That(state.PendingActorId, Is.EqualTo(20));

			Assert.That(state.ObserveTowerAttack(20, 151, 50, false), Is.True);
			Assert.That(state.AnchorActorId, Is.EqualTo(20));
		}

		[Test]
		public void CompletionMayPromoteOnlyANewerPendingAttack()
		{
			var state = new DefenseClusterPolicyState();
			state.ObserveTowerAttack(10, 100, 1000, false);
			Assert.That(state.ObserveTowerAttack(20, 101, 1000, true), Is.True);
			Assert.That(state.AnchorActorId, Is.EqualTo(20));
		}

		[Test]
		public void InvalidationPromotesAValidPendingCandidateAndDropsAnInvalidOne()
		{
			var state = new DefenseClusterPolicyState();
			state.ObserveTowerAttack(10, 100, 100, false);
			state.ObserveTowerAttack(20, 110, 100, false);
			Assert.That(state.InvalidateAnchor(120, 100, id => id == 20), Is.True);
			Assert.That(state.AnchorActorId, Is.EqualTo(20));

			state.ObserveTowerAttack(30, 130, 100, false);
			Assert.That(state.InvalidateAnchor(140, 100, id => false), Is.False);
			Assert.That(state.HasAnchor, Is.False);
			Assert.That(state.HasPending, Is.False);
		}

		[Test]
		public void PlacementFailuresAreBoundedAndProgressResetsThem()
		{
			var state = new DefenseClusterPolicyState();
			Assert.That(state.RecordPlacementFailure(3), Is.False);
			Assert.That(state.RecordPlacementFailure(3), Is.False);
			state.RecordPlacementProgress();
			Assert.That(state.PlacementFailures, Is.Zero);
			Assert.That(state.RecordPlacementFailure(1), Is.True);
		}

		[Test]
		public void MultiRoleTowerCoversRolesButStillCountsAsOneActor()
		{
			var all = DefenseClusterRole.AntiInfantry | DefenseClusterRole.AntiGround | DefenseClusterRole.AntiAir;
			Assert.That(DefenseClusterPolicy.IsComplete(1, 3, all, new[] { all }, true), Is.False);
			Assert.That(DefenseClusterPolicy.IsComplete(3, 3, all, new[] { all }, true), Is.True);
			Assert.That(DefenseClusterPolicy.IsComplete(3, 3, all,
				new[] { DefenseClusterRole.AntiInfantry, DefenseClusterRole.AntiGround }, true), Is.False);
			Assert.That(DefenseClusterPolicy.IsComplete(3, 3, all, new[] { all }, false), Is.False);
		}

		[Test]
		public void MissingRoleWinsBeforeCosmeticActorCountAndReservationsCountOnlyAsCommitted()
		{
			var roles = new Dictionary<string, DefenseClusterRole>
			{
				["multi"] = DefenseClusterRole.AntiGround | DefenseClusterRole.AntiAir,
				["inf"] = DefenseClusterRole.AntiInfantry,
				["ground"] = DefenseClusterRole.AntiGround
			};
			var required = DefenseClusterRole.AntiInfantry | DefenseClusterRole.AntiGround | DefenseClusterRole.AntiAir;
			var selected = DefenseClusterPolicy.ChooseMissingTower(new[] { "multi", "inf", "ground" },
				t => roles[t], required, DefenseClusterRole.AntiInfantry, 1, 1, 3, t => true);
			Assert.That(selected, Is.EqualTo("multi"));

			selected = DefenseClusterPolicy.ChooseMissingTower(new[] { "multi", "inf", "ground" },
				t => roles[t], required, required, 1, 2, 3, t => true);
			Assert.That(selected, Is.Null, "queued/reserved actors suppress duplicates once the actor minimum is committed");
		}

		[Test]
		public void RestoredStatePreservesAnchorPendingLeaseAndRetryFacts()
		{
			var state = new DefenseClusterPolicyState();
			state.Restore(10, 100, 200, 20, 150, 2);
			Assert.That(state.AnchorActorId, Is.EqualTo(10));
			Assert.That(state.PendingActorId, Is.EqualTo(20));
			Assert.That(state.AnchorLeaseUntilTick, Is.EqualTo(200));
			Assert.That(state.PlacementFailures, Is.EqualTo(2));
		}

		[Test]
		public void RepairSiteProtectionSurvivesTransientLegalityAndRoundTripUntilAnchorChanges()
		{
			var state = new DefenseClusterRepairSiteState();
			var site = new CPos(34, 30);
			var approach = new CPos(33, 31);
			var enemy = new CPos(40, 20);
			Assert.That(state.Protect(10, "fix", site, approach, enemy), Is.True);
			Assert.That(state.Matches(10, "fix"), Is.True);
			Assert.That(state.Protect(10, "fix", site, approach, enemy), Is.False,
				"re-observing the same site must not create a new transition");

			var restored = new DefenseClusterRepairSiteState();
			restored.Restore(state.AnchorActorId, state.FacilityType, state.Site, state.ApproachCell,
				state.EnemyLocation);
			Assert.That(restored.Matches(10, "fix"), Is.True);
			Assert.That(restored.ApproachCell, Is.EqualTo(approach));
			Assert.That(restored.EnemyLocation, Is.EqualTo(enemy));
			Assert.That(restored.Matches(20, "fix"), Is.False);
			Assert.That(restored.Clear(), Is.True);
			Assert.That(restored.HasSite, Is.False);
		}

		[Test]
		public void LostRepairReservationWaitsForTransientItemAbsenceButRecoversImmediatelyFromProducerLoss()
		{
			Assert.That(DefenseClusterPolicy.ReservationIsLost(true, true, 1000, 750), Is.False);
			Assert.That(DefenseClusterPolicy.ReservationIsLost(true, false, 749, 750), Is.False,
				"a transient queue observation must not duplicate an in-flight item");
			Assert.That(DefenseClusterPolicy.ReservationIsLost(true, false, 750, 750), Is.True);
			Assert.That(DefenseClusterPolicy.ReservationIsLost(false, false, 1, 750), Is.True,
				"a destroyed or captured producer cannot still own the goal");

			Assert.That(DefenseClusterPolicy.CanUseRepairProducer(false, 20, 10), Is.True);
			Assert.That(DefenseClusterPolicy.CanUseRepairProducer(true, 20, 10), Is.False,
				"a recovery retry waits for the deterministic stable construction yard");
			Assert.That(DefenseClusterPolicy.CanUseRepairProducer(true, 10, 10), Is.True);

			Assert.That(DefenseClusterPolicy.CanQueueRepairRecovery(true, false, false, 1), Is.True,
				"one ordinary item may remain ahead of the bounded repair handoff");
			Assert.That(DefenseClusterPolicy.CanQueueRepairRecovery(true, true, false, 1), Is.False,
				"a recovery episode may enqueue only one handoff");
			Assert.That(DefenseClusterPolicy.CanQueueRepairRecovery(true, false, true, 1), Is.False,
				"opening, power, and refinery recovery retain priority");
			Assert.That(DefenseClusterPolicy.CanQueueRepairRecovery(true, false, false, 2), Is.False,
				"the handoff must not grow an already multi-item queue");
		}
	}
}
