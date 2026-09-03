#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. You can redistribute
 * it and/or modify it under the terms of the GNU General Public License.
 */
#endregion

using System;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Flee owner: issue one short cached-map escape move, then reconsider combat.</summary>
	public sealed class StealthRecalculateFleeBehavior
	{
		readonly StealthRecalculateFleeHandoff handoff;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthRecalculateFleeLiveWorld liveWorld;
		readonly IStealthRecalculateFleeStrategicCache strategicCache;
		readonly IStealthRecalculateFleeOrders orders;
		readonly StealthBehaviorExecutionLease executionLease = new StealthBehaviorExecutionLease();
		StealthRecalculateFleeOrderToken lastOrder;
		CPos[] route = Array.Empty<CPos>();
		StealthTargetThreatScore? danger;
		long? cacheRevision;
		bool? routeFormationCloaked;
		long routeRevision;

		public StealthRecalculateFleeBehavior(StealthRecalculateFleeHandoff handoff,
			IStealthLifecycleOwnershipGuard ownershipGuard,
			IStealthRecalculateFleeLiveWorld liveWorld,
			IStealthRecalculateFleeStrategicCache strategicCache,
			IStealthRecalculateFleeOrders orders)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.RecalculateFlee || handoff.Mission == null)
				throw new ArgumentException("RecalculateFlee requires one typed handoff.", nameof(handoff));
			this.ownershipGuard = ownershipGuard ?? throw new ArgumentNullException(nameof(ownershipGuard));
			this.liveWorld = liveWorld ?? throw new ArgumentNullException(nameof(liveWorld));
			this.strategicCache = strategicCache ?? throw new ArgumentNullException(nameof(strategicCache));
			this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
		}

		public StealthRecalculateFleeResult Execute()
		{
			var revision = executionLease.Acquire("RecalculateFlee", EnsureActiveOwnership);
			try { return Execute(revision); }
			finally { executionLease.Release(revision); }
		}

		StealthRecalculateFleeResult Execute(long executionRevision)
		{
			var decision = StealthRecalculateFleeLiveDecision.Create(ReadLive(executionRevision));
			if (decision.Members.Length == 0)
				return Terminal(decision, StealthRecalculateFleeDisposition.Retain,
					StealthRecalculateFleeLiveCause.MemberLoss, executionRevision);
			if (decision.Enemies.Length == 0)
				return Terminal(decision, StealthRecalculateFleeDisposition.TargetAcquisition,
					StealthRecalculateFleeLiveCause.NoTarget, executionRevision);

			if (lastOrder != null)
			{
				if (decision.CurrentPositionSafe)
					return Terminal(decision, StealthRecalculateFleeDisposition.TargetAcquisition,
						StealthRecalculateFleeLiveCause.SafeToReconsider, executionRevision);
				if (decision.Arrived(lastOrder.DestinationCell))
					return Terminal(decision, StealthRecalculateFleeDisposition.TargetAcquisition,
						StealthRecalculateFleeLiveCause.Completed, executionRevision);

				var sameMembers = lastOrder.ActorIds.SequenceEqual(decision.MemberActorIds);
				var sameExposure = routeFormationCloaked == decision.FormationCloaked;
				if (sameMembers && sameExposure && decision.Members.Any(member => !member.NeedsMovementOrder))
					return Commit(decision, StealthRecalculateFleeDisposition.Retain,
						StealthRecalculateFleeLiveCause.Traversing, route, danger.Value,
						cacheRevision.Value, lastOrder, executionRevision);
			}

			var cached = ReadEscapeRoute(executionRevision);
			var formationCells = decision.Members.Select(member => member.CurrentCell).ToHashSet();
			var candidates = cached.Waypoints.Where(cell => !formationCells.Contains(cell)).ToArray();
			if (candidates.Length == 0)
				return Terminal(decision, StealthRecalculateFleeDisposition.TargetAcquisition,
					StealthRecalculateFleeLiveCause.NoRoute, executionRevision);

			var destination = candidates[0];
			var selectedRoute = new[] { destination };
			var token = new StealthRecalculateFleeOrderToken(handoff.Owner, handoff.Epoch,
				decision.MemberActorIds, destination, ++routeRevision, 0);
			ApplyOrder(token, selectedRoute, executionRevision);
			var cause = decision.MemberActorIds.SequenceEqual(handoff.Evidence.MemberActorIds) ?
				StealthRecalculateFleeLiveCause.Traversing : StealthRecalculateFleeLiveCause.MemberLoss;
			return Commit(decision, StealthRecalculateFleeDisposition.Retain, cause,
				selectedRoute, cached.Danger, cached.Revision, token, executionRevision);
		}

		StealthRecalculateFleeResult Terminal(StealthRecalculateFleeLiveDecision decision,
			StealthRecalculateFleeDisposition disposition, StealthRecalculateFleeLiveCause cause,
			long executionRevision)
		{
			return Commit(decision, disposition, cause, Array.Empty<CPos>(), null, null, null,
				executionRevision);
		}

		StealthRecalculateFleeResult Commit(StealthRecalculateFleeLiveDecision decision,
			StealthRecalculateFleeDisposition disposition, StealthRecalculateFleeLiveCause cause,
			CPos[] selectedRoute, StealthTargetThreatScore? selectedDanger,
			long? selectedCacheRevision, StealthRecalculateFleeOrderToken order,
			long executionRevision)
		{
			var destination = selectedRoute.Length == 0 ? (CPos?)null : selectedRoute[selectedRoute.Length - 1];
			var result = new StealthRecalculateFleeResult(handoff, disposition, cause,
				decision.MemberActorIds, decision.EnemyActorIds, destination, selectedDanger,
				selectedRoute, 0, order, decision.Fingerprint, selectedCacheRevision);
			executionLease.Commit(executionRevision, "RecalculateFlee", EnsureActiveOwnership, () =>
			{
				route = selectedRoute;
				danger = selectedDanger;
				cacheRevision = selectedCacheRevision;
				lastOrder = order;
				routeFormationCloaked = order == null ? (bool?)null : decision.FormationCloaked;
			});
			return result;
		}

		StealthRecalculateFleeLiveSnapshot ReadLive(long revision)
		{
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			var live = liveWorld.Read(handoff.Mission) ??
				throw new InvalidOperationException("The live RecalculateFlee view returned no snapshot.");
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			return live;
		}

		StealthRecalculateFleeStrategicCacheSnapshot ReadEscapeRoute(long revision)
		{
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			var cached = strategicCache.ReadEscapeRoute(handoff.Mission) ??
				throw new InvalidOperationException("Escape-route cache returned no snapshot.");
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			return cached;
		}

		void ApplyOrder(StealthRecalculateFleeOrderToken order, CPos[] selectedRoute, long revision)
		{
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			orders.IssueMove(handoff.Owner, handoff.Epoch, order.ActorIds,
				order.DestinationCell, selectedRoute, 0, order);
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException("Stale RecalculateFlee ownership cannot execute.");
		}
	}
}
