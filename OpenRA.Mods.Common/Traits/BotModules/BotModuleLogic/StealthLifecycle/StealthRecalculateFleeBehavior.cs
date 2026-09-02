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

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Flee owner: follow one short route from the cached strategic threat map.</summary>
	public sealed class StealthRecalculateFleeBehavior
	{
		readonly StealthRecalculateFleeHandoff handoff;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthRecalculateFleeLiveWorld liveWorld;
		readonly IStealthRecalculateFleeStrategicCache strategicCache;
		readonly IStealthRecalculateFleeOrders orders;
		readonly StealthBehaviorExecutionLease executionLease = new StealthBehaviorExecutionLease();
		CPos? destination;
		CPos[] route = Array.Empty<CPos>();
		int routeProgress;
		long routeRevision;
		long? cacheRevision;
		StealthTargetThreatScore? selectedDanger;
		StealthRecalculateFleeOrderToken lastOrder;

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

		StealthRecalculateFleeResult Execute(long revision)
		{
			var decision = StealthRecalculateFleeLiveDecision.Create(ReadLive(revision));
			if (decision.Members.Length == 0)
				return NoRoute(decision, StealthRecalculateFleeLiveCause.MemberLoss, revision);
			if (decision.Enemies.Length == 0)
				return TargetGone(decision, revision);
			var cached = route.Length != 0 && destination.HasValue && cacheRevision.HasValue && selectedDanger.HasValue ?
				new StealthRecalculateFleeStrategicCacheSnapshot(cacheRevision.Value, selectedDanger.Value, route) :
				ReadEscapeRoute(revision);
			var selectedRoute = cached.Waypoints.ToArray();
			if (selectedRoute.Length == 0 || selectedRoute.Distinct().Count() != selectedRoute.Length)
				return NoRoute(decision, StealthRecalculateFleeLiveCause.NoRoute, revision);
			var selectedDestination = selectedRoute[selectedRoute.Length - 1];
			var selectedCacheRevision = cached.Revision;

			var changed = destination != selectedDestination ||
				!route.SequenceEqual(selectedRoute) || lastOrder == null ||
				!lastOrder.ActorIds.SequenceEqual(decision.MemberActorIds);
			var progress = changed ? 0 : routeProgress;
			var revisionNumber = changed ? checked(routeRevision + 1) : routeRevision;
			var priorOrder = changed ? null : lastOrder;
			if (!changed && decision.Arrived(selectedRoute[progress]))
			{
				if (progress == selectedRoute.Length - 1)
					return Commit(decision, StealthRecalculateFleeDisposition.TargetAcquisition,
						StealthRecalculateFleeLiveCause.Completed, selectedDestination, cached.Danger,
						selectedRoute, progress, selectedCacheRevision, priorOrder,
						revisionNumber, revision);
				progress++;
				revisionNumber = checked(revisionNumber + 1);
				priorOrder = null;
			}

			var waypoint = selectedRoute[progress];
			var retry = priorOrder != null && decision.Members.Any(member => member.NeedsMovementOrder);
			var desired = priorOrder;
			if (priorOrder == null || retry)
			{
				desired = new StealthRecalculateFleeOrderToken(handoff.Owner, handoff.Epoch,
					decision.MemberActorIds, waypoint, revisionNumber,
					retry ? checked(priorOrder.ActivityRevision + 1) : 0);
				ApplyOrder(desired, selectedRoute, progress, revision);
			}

			var cause = decision.MemberActorIds.SequenceEqual(handoff.Evidence.MemberActorIds) ?
				StealthRecalculateFleeLiveCause.Traversing : StealthRecalculateFleeLiveCause.MemberLoss;
			return Commit(decision, StealthRecalculateFleeDisposition.Retain, cause,
				selectedDestination, cached.Danger, selectedRoute, progress, selectedCacheRevision,
				desired, revisionNumber, revision);
		}

		StealthRecalculateFleeResult NoRoute(StealthRecalculateFleeLiveDecision decision,
			StealthRecalculateFleeLiveCause cause, long revision)
		{
			return Commit(decision, StealthRecalculateFleeDisposition.Retain, cause,
				null, null,
				Array.Empty<CPos>(), 0, null, null, routeRevision, revision);
		}

		StealthRecalculateFleeResult TargetGone(StealthRecalculateFleeLiveDecision decision,
			long revision)
		{
			return Commit(decision, StealthRecalculateFleeDisposition.TargetAcquisition,
				StealthRecalculateFleeLiveCause.NoTarget,
				null, null,
				Array.Empty<CPos>(), 0, null, null, routeRevision, revision);
		}

		StealthRecalculateFleeResult Commit(StealthRecalculateFleeLiveDecision decision,
			StealthRecalculateFleeDisposition disposition, StealthRecalculateFleeLiveCause cause,
			CPos? selected, StealthTargetThreatScore? danger, CPos[] selectedRoute,
			int progress, long? selectedCacheRevision,
			StealthRecalculateFleeOrderToken order, long revisionNumber, long executionRevision)
		{
			var result = new StealthRecalculateFleeResult(handoff, disposition, cause,
				decision.MemberActorIds, decision.EnemyActorIds, selected, danger, selectedRoute,
				progress, order, decision.Fingerprint, selectedCacheRevision);
			executionLease.Commit(executionRevision, "RecalculateFlee", EnsureActiveOwnership, () =>
			{
				destination = selected;
				route = selectedRoute;
				routeProgress = progress;
				cacheRevision = selectedCacheRevision;
				selectedDanger = danger;
				lastOrder = order;
				routeRevision = revisionNumber;
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

		void ApplyOrder(StealthRecalculateFleeOrderToken order,
			IReadOnlyList<CPos> selectedRoute, int progress, long revision)
		{
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			orders.IssueMove(handoff.Owner, handoff.Epoch, order.ActorIds,
				order.DestinationCell, selectedRoute, progress, order);
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException("Stale RecalculateFlee ownership cannot execute.");
		}
	}
}
