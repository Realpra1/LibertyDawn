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
	/// <summary>Reactive live flee owner: follow the least-dangerous current escape route.</summary>
	public sealed class StealthRecalculateFleeBehavior
	{
		readonly StealthRecalculateFleeHandoff handoff;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthRecalculateFleeLiveWorld liveWorld;
		readonly IStealthRecalculateFleeThreatAdapter threatAdapter;
		readonly IStealthRecalculateFleeStrategicCache strategicCache;
		readonly IStealthRecalculateFleeOrders orders;
		readonly StealthBehaviorExecutionLease executionLease = new StealthBehaviorExecutionLease();
		CPos? destination;
		CPos[] route = Array.Empty<CPos>();
		int routeProgress;
		long routeRevision;
		long? cacheRevision;
		StealthRecalculateFleeOrderToken lastOrder;

		public StealthRecalculateFleeBehavior(StealthRecalculateFleeHandoff handoff,
			IStealthLifecycleOwnershipGuard ownershipGuard,
			IStealthRecalculateFleeLiveWorld liveWorld,
			IStealthRecalculateFleeThreatAdapter threatAdapter,
			IStealthRecalculateFleeStrategicCache strategicCache,
			IStealthRecalculateFleeOrders orders)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.RecalculateFlee || handoff.Mission == null)
				throw new ArgumentException("RecalculateFlee requires one typed handoff.", nameof(handoff));
			this.ownershipGuard = ownershipGuard ?? throw new ArgumentNullException(nameof(ownershipGuard));
			this.liveWorld = liveWorld ?? throw new ArgumentNullException(nameof(liveWorld));
			this.threatAdapter = threatAdapter ?? throw new ArgumentNullException(nameof(threatAdapter));
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
			if (decision.PassableCandidates.Length == 0)
				return NoRoute(decision, StealthRecalculateFleeLiveCause.NoRoute, revision);

			var evaluations = decision.PassableCandidates.Select(candidate =>
				decision.Evaluate(candidate, facts => Calculate(facts, revision))).ToArray();
			if (!TrySelectRoute(evaluations, revision, out var selected,
				out var selectedRoute, out var selectedCacheRevision))
				return NoRoute(decision, StealthRecalculateFleeLiveCause.NoRoute, revision);

			var changed = destination != selected.Candidate.Cell ||
				!route.SequenceEqual(selectedRoute) || lastOrder == null ||
				!lastOrder.ActorIds.SequenceEqual(decision.MemberActorIds);
			var progress = changed ? 0 : routeProgress;
			var revisionNumber = changed ? checked(routeRevision + 1) : routeRevision;
			var priorOrder = changed ? null : lastOrder;
			if (!changed && decision.Arrived(selectedRoute[progress]))
			{
				if (progress == selectedRoute.Length - 1)
					return Commit(decision, StealthRecalculateFleeDisposition.TargetAcquisition,
						StealthRecalculateFleeLiveCause.Completed, evaluations, selected,
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
				evaluations, selected, selectedRoute, progress, selectedCacheRevision,
				desired, revisionNumber, revision);
		}

		bool TrySelectRoute(IReadOnlyList<StealthRecalculateFleeRouteEvaluation> evaluations,
			long revision, out StealthRecalculateFleeRouteEvaluation selected,
			out CPos[] selectedRoute, out long? selectedCacheRevision)
		{
			var candidates = StealthRecalculateFleeLiveDecision.OrderedBySafety(evaluations).ToList();
			var retained = destination.HasValue ? candidates.FirstOrDefault(candidate =>
				candidate.Candidate.Cell == destination.Value) : null;
			if (retained != null && SameScore(retained.StandardDanger, candidates[0].StandardDanger))
			{
				candidates.Remove(retained);
				candidates.Insert(0, retained);
			}

			foreach (var candidate in candidates)
			{
				if (!candidate.Candidate.RequiresStrategicRouting)
				{
					selected = candidate;
					selectedRoute = new[] { candidate.Candidate.Cell };
					selectedCacheRevision = null;
					return true;
				}

				if (destination == candidate.Candidate.Cell && route.Length != 0 && cacheRevision.HasValue)
				{
					selected = candidate;
					selectedRoute = route;
					selectedCacheRevision = cacheRevision;
					return true;
				}

				var cached = ReadLongRoute(candidate.Candidate.Cell, revision);
				var waypoints = cached.Waypoints.ToArray();
				if (waypoints.Length == 0 || waypoints.Distinct().Count() != waypoints.Length)
					continue;
				selected = candidate;
				selectedRoute = waypoints;
				selectedCacheRevision = cached.Revision;
				return true;
			}

			selected = null;
			selectedRoute = Array.Empty<CPos>();
			selectedCacheRevision = null;
			return false;
		}

		StealthRecalculateFleeResult NoRoute(StealthRecalculateFleeLiveDecision decision,
			StealthRecalculateFleeLiveCause cause, long revision)
		{
			return Commit(decision, StealthRecalculateFleeDisposition.Retain, cause,
				Array.Empty<StealthRecalculateFleeRouteEvaluation>(), null,
				Array.Empty<CPos>(), 0, null, null, routeRevision, revision);
		}

		StealthRecalculateFleeResult TargetGone(StealthRecalculateFleeLiveDecision decision,
			long revision)
		{
			return Commit(decision, StealthRecalculateFleeDisposition.TargetAcquisition,
				StealthRecalculateFleeLiveCause.NoTarget,
				Array.Empty<StealthRecalculateFleeRouteEvaluation>(), null,
				Array.Empty<CPos>(), 0, null, null, routeRevision, revision);
		}

		StealthRecalculateFleeResult Commit(StealthRecalculateFleeLiveDecision decision,
			StealthRecalculateFleeDisposition disposition, StealthRecalculateFleeLiveCause cause,
			IEnumerable<StealthRecalculateFleeRouteEvaluation> evaluations,
			StealthRecalculateFleeRouteEvaluation selected, CPos[] selectedRoute,
			int progress, long? selectedCacheRevision,
			StealthRecalculateFleeOrderToken order, long revisionNumber, long executionRevision)
		{
			var result = new StealthRecalculateFleeResult(handoff, disposition, cause,
				decision.MemberActorIds, decision.EnemyActorIds, evaluations,
				selected?.Candidate.Cell, selected?.StandardDanger, selectedRoute,
				progress, order, decision.Fingerprint, selectedCacheRevision);
			executionLease.Commit(executionRevision, "RecalculateFlee", EnsureActiveOwnership, () =>
			{
				destination = selected?.Candidate.Cell;
				route = selectedRoute;
				routeProgress = progress;
				cacheRevision = selectedCacheRevision;
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

		StealthTargetThreatScore Calculate(StealthRecalculateFleeThreatFacts facts, long revision)
		{
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			var score = threatAdapter.CalculateRouteDanger(facts);
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			return score;
		}

		StealthRecalculateFleeStrategicCacheSnapshot ReadLongRoute(CPos cell, long revision)
		{
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			var cached = strategicCache.ReadLongRoute(handoff.Mission, cell) ??
				throw new InvalidOperationException("Long-route cache returned no snapshot.");
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

		static bool SameScore(StealthTargetThreatScore left, StealthTargetThreatScore right)
		{
			return left.ThreatRating.Equals(right.ThreatRating) && left.Crossover.Equals(right.Crossover);
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException("Stale RecalculateFlee ownership cannot execute.");
		}
	}
}
