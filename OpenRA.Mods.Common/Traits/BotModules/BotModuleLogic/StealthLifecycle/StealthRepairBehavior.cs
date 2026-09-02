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
	/// <summary>Reactive live Repair owner: take the safest route, heal, or resume the fight.</summary>
	public sealed class StealthRepairBehavior
	{
		readonly StealthRepairHandoff handoff;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthRepairLiveWorld liveWorld;
		readonly IStealthRepairThreatAdapter threatAdapter;
		readonly IStealthRepairStrategicCache strategicCache;
		readonly IStealthRepairOrders orders;
		readonly StealthBehaviorExecutionLease executionLease = new StealthBehaviorExecutionLease();
		uint? optionId;
		uint? routeId;
		CPos[] route = Array.Empty<CPos>();
		int routeProgress;
		long routeRevision;
		long? cacheRevision;
		StealthRepairOrderToken lastOrder;

		public StealthRepairBehavior(StealthRepairHandoff handoff,
			IStealthLifecycleOwnershipGuard ownershipGuard, IStealthRepairLiveWorld liveWorld,
			IStealthRepairThreatAdapter threatAdapter, IStealthRepairStrategicCache strategicCache,
			IStealthRepairOrders orders)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.Repair || handoff.Mission == null || handoff.Resume == null)
				throw new ArgumentException("Repair requires one typed Damage handoff.", nameof(handoff));
			this.ownershipGuard = ownershipGuard ?? throw new ArgumentNullException(nameof(ownershipGuard));
			this.liveWorld = liveWorld ?? throw new ArgumentNullException(nameof(liveWorld));
			this.threatAdapter = threatAdapter ?? throw new ArgumentNullException(nameof(threatAdapter));
			this.strategicCache = strategicCache ?? throw new ArgumentNullException(nameof(strategicCache));
			this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
		}

		public StealthRepairResult Execute()
		{
			var revision = executionLease.Acquire("Repair", EnsureActiveOwnership);
			try { return Execute(revision); }
			finally { executionLease.Release(revision); }
		}

		StealthRepairResult Execute(long revision)
		{
			var decision = StealthRepairLiveDecision.Create(ReadLive(revision));
			if (decision.Members.Length == 0)
				return Terminal(decision, StealthRepairDisposition.SquadConstruction,
					StealthRepairLiveCause.NoLiveMembers, Array.Empty<uint>(), null, revision);

			var repairMembers = RepairMembers(decision);
			var repairIds = repairMembers.Select(member => member.ActorId).ToArray();
			if (repairMembers.Length == 0)
			{
				var completedMembers = CompletionMembers(decision);
				var completion = decision.Completion(completedMembers);
				if (completion != null)
					return Terminal(decision, StealthRepairDisposition.Start,
						StealthRepairLiveCause.RepairComplete,
						completedMembers.Select(member => member.ActorId).ToArray(), completion, revision);
			}

			if (repairMembers.Length == 0 || decision.PassableRoutes.Length == 0)
				return Terminal(decision, StealthRepairDisposition.ResumeFight,
					StealthRepairLiveCause.NoSafeRepair, repairIds, null, revision);

			var evaluations = decision.PassableRoutes.Select(candidate => decision.Evaluate(candidate,
				repairMembers, facts => Calculate(facts, revision))).ToArray();
			if (!TrySelectRoute(evaluations, decision, repairMembers, revision,
				out var selected, out var selectedRoute, out var selectedCacheRevision))
				return Terminal(decision, StealthRepairDisposition.ResumeFight,
					StealthRepairLiveCause.NoSafeRepair, repairIds, null, revision);

			var atOption = decision.AtOption(selected.Option, repairMembers);
			var kind = atOption ? StealthRepairOrderKind.Repair : StealthRepairOrderKind.Retreat;
			var changed = optionId != selected.Option.ActorId ||
				routeId != selected.Route.StableIdentity || !route.SequenceEqual(selectedRoute) ||
				lastOrder == null || lastOrder.Kind != kind ||
				!lastOrder.ActorIds.SequenceEqual(repairIds);
			var progress = changed ?
				(selected.Route.RequiresStrategicRouting && !atOption ? 0 : selectedRoute.Length - 1) :
				routeProgress;
			var revisionNumber = changed ? checked(routeRevision + 1) : routeRevision;
			var priorOrder = changed ? null : lastOrder;
			if (!changed && kind == StealthRepairOrderKind.Retreat &&
				progress < selectedRoute.Length - 1 &&
				decision.Arrived(selectedRoute[progress], repairMembers))
			{
				progress++;
				revisionNumber = checked(revisionNumber + 1);
				priorOrder = null;
			}

			var retryRetreat = priorOrder != null && kind == StealthRepairOrderKind.Retreat &&
				repairMembers.Any(member => member.NeedsMovementOrder);
			var desired = priorOrder != null && !retryRetreat ? priorOrder :
				new StealthRepairOrderToken(handoff.Owner, handoff.Epoch, repairIds,
					selected.Option.ActorId, selected.Route.StableIdentity, kind, revisionNumber,
					retryRetreat ? checked(priorOrder.ActivityRevision + 1) : 0);
			if (priorOrder == null || retryRetreat)
				ApplyOrder(selected, desired, selectedRoute, progress, revision);
			return Commit(decision, StealthRepairDisposition.Retain,
				atOption ? StealthRepairLiveCause.Healing : StealthRepairLiveCause.Retreating,
				repairIds, evaluations, selected, selectedRoute, progress,
				selectedCacheRevision, desired, revisionNumber, null, revision);
		}

		bool TrySelectRoute(IReadOnlyList<StealthRepairRouteEvaluation> evaluations,
			StealthRepairLiveDecision decision, IReadOnlyList<StealthRepairMemberSnapshot> repairMembers,
			long revision, out StealthRepairRouteEvaluation selected,
			out CPos[] selectedRoute, out long? selectedCacheRevision)
		{
			foreach (var candidate in StealthRepairLiveDecision.OrderedSafe(evaluations))
			{
				if (!candidate.Route.RequiresStrategicRouting || decision.AtOption(candidate.Option, repairMembers))
				{
					selected = candidate;
					selectedRoute = candidate.Route.Cells.ToArray();
					selectedCacheRevision = null;
					return true;
				}

				if (routeId == candidate.Route.StableIdentity && route.Length != 0 && cacheRevision.HasValue)
				{
					selected = candidate;
					selectedRoute = route;
					selectedCacheRevision = cacheRevision;
					return true;
				}

				var cached = ReadLongRoute(candidate, revision);
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

		StealthRepairResult Terminal(StealthRepairLiveDecision decision,
			StealthRepairDisposition disposition, StealthRepairLiveCause cause,
			uint[] repairIds, StealthRepairCompletionEvidence completion, long revision)
		{
			return Commit(decision, disposition, cause, repairIds,
				Array.Empty<StealthRepairRouteEvaluation>(), null, Array.Empty<CPos>(),
				0, null, null, routeRevision, completion, revision);
		}

		StealthRepairResult Commit(StealthRepairLiveDecision decision,
			StealthRepairDisposition disposition, StealthRepairLiveCause cause, uint[] repairIds,
			IEnumerable<StealthRepairRouteEvaluation> evaluations,
			StealthRepairRouteEvaluation selected, CPos[] selectedRoute, int progress,
			long? selectedCacheRevision, StealthRepairOrderToken order,
			long revisionNumber, StealthRepairCompletionEvidence completion, long executionRevision)
		{
			var result = new StealthRepairResult(handoff, disposition, cause,
				decision.MemberActorIds, repairIds, decision.EnemyActorIds, evaluations,
				selected?.Option.ActorId, selected?.Route.StableIdentity, progress,
				selected?.StandardDanger, order, completion, decision.Fingerprint,
				selectedCacheRevision);
			executionLease.Commit(executionRevision, "Repair", EnsureActiveOwnership, () =>
			{
				optionId = selected?.Option.ActorId;
				routeId = selected?.Route.StableIdentity;
				route = selectedRoute;
				routeProgress = progress;
				cacheRevision = selectedCacheRevision;
				lastOrder = order;
				routeRevision = revisionNumber;
			});
			return result;
		}

		StealthRepairMemberSnapshot[] RepairMembers(StealthRepairLiveDecision decision)
		{
			return decision.Members.Where(member => handoff.DamagedMembers.Any(
				damaged => damaged.ActorId == member.ActorId) && member.HitPoints < member.MaximumHitPoints)
				.OrderBy(member => member.ActorId).ToArray();
		}

		StealthRepairMemberSnapshot[] CompletionMembers(StealthRepairLiveDecision decision)
		{
			return decision.Members.Where(member => handoff.DamagedMembers.Any(
				damaged => damaged.ActorId == member.ActorId) && member.IsRepaired)
				.OrderBy(member => member.ActorId).ToArray();
		}

		StealthRepairLiveSnapshot ReadLive(long revision)
		{
			executionLease.Verify(revision, "Repair", EnsureActiveOwnership);
			var live = liveWorld.Read(handoff.Mission) ??
				throw new InvalidOperationException("The live Repair view returned no snapshot.");
			executionLease.Verify(revision, "Repair", EnsureActiveOwnership);
			return live;
		}

		StealthTargetThreatScore Calculate(StealthRepairThreatFacts facts, long revision)
		{
			executionLease.Verify(revision, "Repair", EnsureActiveOwnership);
			var score = threatAdapter.CalculateRouteDanger(facts);
			executionLease.Verify(revision, "Repair", EnsureActiveOwnership);
			return score;
		}

		StealthRepairStrategicCacheSnapshot ReadLongRoute(
			StealthRepairRouteEvaluation selected, long revision)
		{
			executionLease.Verify(revision, "Repair", EnsureActiveOwnership);
			var cached = strategicCache.ReadLongRoute(handoff.Mission, selected.Option.ActorId,
				selected.Route.Cells) ?? throw new InvalidOperationException(
					"Repair route cache returned no snapshot.");
			executionLease.Verify(revision, "Repair", EnsureActiveOwnership);
			return cached;
		}

		void ApplyOrder(StealthRepairRouteEvaluation selected, StealthRepairOrderToken order,
			IReadOnlyList<CPos> selectedRoute, int progress, long revision)
		{
			executionLease.Verify(revision, "Repair", EnsureActiveOwnership);
			orders.IssueRepair(handoff.Owner, handoff.Epoch, order.ActorIds,
				selected.Option.ActorId, selectedRoute, progress, order.Kind, order);
			executionLease.Verify(revision, "Repair", EnsureActiveOwnership);
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException("Stale Repair ownership cannot execute.");
		}
	}
}
