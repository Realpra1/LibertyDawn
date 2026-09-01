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
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Disabled live-only Step 5 owner. It retains exclusive ownership while repeatedly evaluating
	/// every passable live candidate with the standard threat adapter. Strategic cache data is read
	/// only after a live long-route winner exists and can never alter safety, orders, or transitions.
	/// </summary>
	public sealed class StealthRecalculateFleeBehavior
	{
		readonly StealthRecalculateFleeHandoff handoff;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthRecalculateFleeLiveWorld liveWorld;
		readonly IStealthRecalculateFleeThreatAdapter threatAdapter;
		readonly IStealthRecalculateFleeStrategicCache strategicCache;
		readonly IStealthRecalculateFleeOrders orders;
		readonly StealthBehaviorExecutionLease executionLease = new StealthBehaviorExecutionLease();
		StealthRecalculateFleeOwnerState state = new StealthRecalculateFleeOwnerState();

		public StealthRecalculateFleeBehavior(StealthRecalculateFleeHandoff handoff,
			IStealthLifecycleOwnershipGuard ownershipGuard,
			IStealthRecalculateFleeLiveWorld liveWorld,
			IStealthRecalculateFleeThreatAdapter threatAdapter,
			IStealthRecalculateFleeStrategicCache strategicCache,
			IStealthRecalculateFleeOrders orders)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.RecalculateFlee || handoff.Mission == null || handoff.Evidence == null)
				throw new ArgumentException("RecalculateFlee requires one complete typed handoff.", nameof(handoff));
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
			var live = ReadLive("execute", revision);
			var decision = StealthRecalculateFleeLiveDecision.Create(live);
			var prospective = state.Clone();
			if (decision.Tick < prospective.LastObservedTick)
				throw new InvalidOperationException("Live RecalculateFlee ticks must not move backwards.");
			if (!prospective.EntryValidated)
				ValidateEntry(decision, revision);
			prospective.EntryValidated = true;
			prospective.LastObservedTick = decision.Tick;
			prospective.LastEvaluationTick = decision.Tick;
			prospective.Fingerprint = decision.Fingerprint;
			prospective.MemberIds = decision.MemberActorIds.ToArray();
			prospective.EnemyIds = decision.EnemyActorIds.ToArray();
			prospective.Disposition = StealthRecalculateFleeDisposition.Retain;

			if (decision.Members.Length == 0)
			{
				ClearRoute(prospective);
				prospective.LiveCause = StealthRecalculateFleeLiveCause.MemberLoss;
				return CommitAndResult(prospective, revision);
			}

			if (decision.Enemies.Length == 0)
			{
				ClearRoute(prospective);
				prospective.LiveCause = StealthRecalculateFleeLiveCause.NoTarget;
				return CommitAndResult(prospective, revision);
			}

			if (decision.PassableCandidates.Length == 0)
			{
				ClearRoute(prospective);
				prospective.LiveCause = StealthRecalculateFleeLiveCause.NoRoute;
				return CommitAndResult(prospective, revision);
			}

			var evaluated = decision.PassableCandidates.Select(candidate =>
				decision.Evaluate(candidate, facts => CalculateRoute(facts, revision))).ToArray();
			if (!TrySelectRoute(evaluated, prospective, revision, out var selected,
				out var orderedRoute, out var cacheRevision))
			{
				ClearRoute(prospective);
				prospective.LiveCause = StealthRecalculateFleeLiveCause.NoRoute;
				return CommitAndResult(prospective, revision);
			}

			var routeChanged = !prospective.Destination.HasValue ||
				prospective.Destination.Value != selected.Candidate.Cell ||
				!prospective.OrderedRoute.SequenceEqual(orderedRoute) ||
				prospective.LastOrderToken == null ||
				!prospective.LastOrderToken.ActorIds.SequenceEqual(decision.MemberActorIds);
			if (routeChanged && prospective.LastOrderToken != null)
				AdvanceRouteRevision(prospective);
			if (routeChanged)
			{
				prospective.RouteProgress = 0;
				prospective.LastOrderToken = null;
			}

			prospective.Evaluations = evaluated;
			prospective.Destination = selected.Candidate.Cell;
			prospective.Danger = selected.StandardDanger;
			prospective.OrderedRoute = orderedRoute;
			prospective.LongRouteCacheRevision = cacheRevision;
			if (!routeChanged && prospective.LastOrderToken != null &&
				decision.Arrived(prospective.OrderedRoute[prospective.RouteProgress]))
			{
				if (prospective.RouteProgress == prospective.OrderedRoute.Length - 1)
				{
					prospective.LiveCause = StealthRecalculateFleeLiveCause.Completed;
					prospective.Disposition = StealthRecalculateFleeDisposition.TargetAcquisition;
					return CommitAndResult(prospective, revision);
				}

				AdvanceRouteRevision(prospective);
				prospective.RouteProgress++;
				prospective.LastOrderToken = null;
			}

			var waypoint = prospective.OrderedRoute[prospective.RouteProgress];
			var desired = prospective.LastOrderToken ?? new StealthRecalculateFleeOrderToken(
				handoff.Owner, handoff.Epoch, decision.MemberActorIds, waypoint,
				prospective.RouteRevision, decision.ActivityRevision);
			var observed = decision.HasActivityObservation &&
				(desired.Equals(decision.ActiveOrderToken) || desired.Equals(decision.CompletedOrderToken));
			if (!observed)
				ApplyOrder(desired, prospective.OrderedRoute, prospective.RouteProgress, revision);
			prospective.LastOrderToken = desired;
			prospective.LiveCause = decision.MemberActorIds.SequenceEqual(handoff.Evidence.MemberActorIds) ?
				StealthRecalculateFleeLiveCause.Traversing : StealthRecalculateFleeLiveCause.MemberLoss;
			return CommitAndResult(prospective, revision);
		}

		public MiniYamlNode SerializePrivateState(string key = "RecalculateFlee")
		{
			return StealthRecalculateFleePersistence.Serialize(key, handoff, state);
		}

		public void RestorePrivateState(MiniYamlNode node)
		{
			var revision = executionLease.Acquire("RecalculateFlee", EnsureActiveOwnership);
			try
			{
				var restored = StealthRecalculateFleePersistence.Restore(node, handoff);
				var live = ReadLive("restore", revision);
				ValidateRestored(restored, StealthRecalculateFleeLiveDecision.Create(live), revision);
				var prospective = restored.Clone();
				executionLease.Commit(revision, "RecalculateFlee", EnsureActiveOwnership,
					() => state = prospective);
			}
			finally { executionLease.Release(revision); }
		}

		internal void RestorePersistedState(MiniYamlNode node)
		{
			var revision = executionLease.Acquire("RecalculateFlee", EnsureActiveOwnership);
			try
			{
				var restored = StealthRecalculateFleePersistence.Restore(node, handoff);
				executionLease.Commit(revision, "RecalculateFlee", EnsureActiveOwnership,
					() => state = restored.Clone());
			}
			finally { executionLease.Release(revision); }
		}

		void ValidateEntry(StealthRecalculateFleeLiveDecision decision, long revision)
		{
			var evidence = handoff.Evidence;
			if (!decision.MemberActorIds.SequenceEqual(evidence.MemberActorIds) ||
				!decision.EnemyActorIds.SequenceEqual(evidence.EnemyActorIds) ||
				decision.FormationCloaked != evidence.FormationCloaked ||
				decision.SourceFingerprint != evidence.LiveFingerprint ||
				!decision.Enemies.Any(enemy => enemy.ActorId == evidence.SelectedTargetActorId &&
					enemy.CurrentCell == evidence.SelectedTargetCurrentCell))
				throw new InvalidOperationException("RecalculateFlee entry cause is stale or forged.");
			var current = CalculateEntry(decision.EntryFacts(evidence), revision);
			if (!SameScore(current, evidence.StandardScore) ||
				(evidence.Source == StealthRecalculateFleeSource.KiteNoSafePlan ?
					current.Crossover > 2 : current.Crossover > 1))
				throw new InvalidOperationException("RecalculateFlee entry threshold is not current and canonical.");
		}

		void ValidateRestored(StealthRecalculateFleeOwnerState restored,
			StealthRecalculateFleeLiveDecision decision, long revision)
		{
			if (!restored.EntryValidated)
			{
				ValidateEntry(decision, revision);
				if (restored.LastObservedTick != -1)
					throw new InvalidOperationException("Pristine RecalculateFlee state is not canonical.");
				return;
			}

			if (restored.LastObservedTick != decision.Tick ||
				restored.LastEvaluationTick != decision.Tick ||
				restored.Fingerprint != decision.Fingerprint ||
				!restored.MemberIds.SequenceEqual(decision.MemberActorIds) ||
				!restored.EnemyIds.SequenceEqual(decision.EnemyActorIds))
				throw new InvalidOperationException("Saved RecalculateFlee live facts are stale.");
			var expected = EvaluateForRestore(restored, decision, revision);
			if (!SameStateDecision(restored, expected))
				throw new InvalidOperationException("Saved RecalculateFlee route or standard scores are stale.");
			var expectedCause = ExpectedCause(restored, decision);
			var expectedDisposition = expectedCause == StealthRecalculateFleeLiveCause.Completed ?
				StealthRecalculateFleeDisposition.TargetAcquisition :
				StealthRecalculateFleeDisposition.Retain;
			if (restored.LiveCause != expectedCause || restored.Disposition != expectedDisposition)
				throw new InvalidOperationException("Saved RecalculateFlee live cause is stale or forged.");
			if (restored.LastOrderToken != null &&
				(restored.LastOrderToken.Epoch != handoff.Epoch ||
				restored.LastOrderToken.ActivityRevision > decision.ActivityRevision ||
					(!decision.HasActivityObservation && restored.LastOrderToken.ActivityRevision != 0)))
				throw new InvalidOperationException("Saved RecalculateFlee token has a stale epoch.");
			if (restored.LongRouteCacheRevision.HasValue)
			{
				var cached = ReadLongRoute(restored.Destination.Value, revision);
				if (cached.Revision != restored.LongRouteCacheRevision ||
					!cached.Waypoints.SequenceEqual(restored.OrderedRoute))
					throw new InvalidOperationException("Saved RecalculateFlee cached route was altered or stale.");
			}
		}

		StealthRecalculateFleeOwnerState EvaluateForRestore(StealthRecalculateFleeOwnerState restored,
			StealthRecalculateFleeLiveDecision decision, long revision)
		{
			var expected = restored.Clone();
			expected.MemberIds = decision.MemberActorIds;
			expected.EnemyIds = decision.EnemyActorIds;
			if (decision.Members.Length == 0) { ClearRoute(expected); expected.LiveCause = StealthRecalculateFleeLiveCause.MemberLoss; return expected; }
			if (decision.Enemies.Length == 0) { ClearRoute(expected); expected.LiveCause = StealthRecalculateFleeLiveCause.NoTarget; return expected; }
			if (decision.PassableCandidates.Length == 0) { ClearRoute(expected); expected.LiveCause = StealthRecalculateFleeLiveCause.NoRoute; return expected; }
			expected.Evaluations = decision.PassableCandidates.Select(candidate =>
				decision.Evaluate(candidate, facts => CalculateRoute(facts, revision))).ToArray();
			if (!TrySelectRoute(expected.Evaluations, expected, revision, out var selected,
				out var orderedRoute, out var cacheRevision))
			{
				ClearRoute(expected);
				expected.LiveCause = StealthRecalculateFleeLiveCause.NoRoute;
				return expected;
			}

			expected.Destination = selected.Candidate.Cell;
			expected.Danger = selected.StandardDanger;
			expected.OrderedRoute = orderedRoute;
			expected.LongRouteCacheRevision = cacheRevision;
			return expected;
		}

		StealthRecalculateFleeLiveCause ExpectedCause(StealthRecalculateFleeOwnerState restored,
			StealthRecalculateFleeLiveDecision decision)
		{
			if (decision.Members.Length == 0)
				return StealthRecalculateFleeLiveCause.MemberLoss;
			if (decision.Enemies.Length == 0)
				return StealthRecalculateFleeLiveCause.NoTarget;
			if (decision.PassableCandidates.Length == 0)
				return StealthRecalculateFleeLiveCause.NoRoute;
			if (restored.LastOrderToken != null && restored.OrderedRoute.Length != 0 &&
				restored.RouteProgress == restored.OrderedRoute.Length - 1 &&
				decision.Arrived(restored.OrderedRoute[restored.RouteProgress]))
				return StealthRecalculateFleeLiveCause.Completed;
			return decision.MemberActorIds.SequenceEqual(handoff.Evidence.MemberActorIds) ?
				StealthRecalculateFleeLiveCause.Traversing :
				StealthRecalculateFleeLiveCause.MemberLoss;
		}

		StealthRecalculateFleeLiveSnapshot ReadLive(string operation, long revision)
		{
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			var live = liveWorld.Read(handoff.Mission) ?? throw new InvalidOperationException(
				"The live RecalculateFlee view returned no snapshot during " + operation + ".");
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			return live;
		}

		StealthTargetThreatScore CalculateEntry(StealthRecalculateFleeEntryThreatFacts facts, long revision)
		{
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			var score = threatAdapter.CalculateEntryCrossover(facts);
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			return score;
		}

		StealthTargetThreatScore CalculateRoute(StealthRecalculateFleeThreatFacts facts, long revision)
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
				throw new InvalidOperationException("Long-route cache returned no passive snapshot.");
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			return cached;
		}

		bool TrySelectRoute(IReadOnlyList<StealthRecalculateFleeRouteEvaluation> evaluations,
			StealthRecalculateFleeOwnerState current, long revision,
			out StealthRecalculateFleeRouteEvaluation selected, out CPos[] orderedRoute,
			out long? cacheRevision)
		{
			foreach (var candidate in StealthRecalculateFleeLiveDecision.OrderedBySafety(evaluations))
			{
				if (!candidate.Candidate.RequiresStrategicRouting)
				{
					selected = candidate;
					orderedRoute = new[] { candidate.Candidate.Cell };
					cacheRevision = null;
					return true;
				}

				if (current.Destination == candidate.Candidate.Cell && current.OrderedRoute.Length != 0 &&
					current.LongRouteCacheRevision.HasValue)
				{
					selected = candidate;
					orderedRoute = current.OrderedRoute.ToArray();
					cacheRevision = current.LongRouteCacheRevision;
					return true;
				}

				var cached = ReadLongRoute(candidate.Candidate.Cell, revision);
				var waypoints = cached.Waypoints.ToArray();
				if (waypoints.Length == 0 || waypoints.Distinct().Count() != waypoints.Length)
					continue;
				selected = candidate;
				orderedRoute = waypoints;
				cacheRevision = cached.Revision;
				return true;
			}

			selected = null;
			orderedRoute = Array.Empty<CPos>();
			cacheRevision = null;
			return false;
		}

		void ApplyOrder(StealthRecalculateFleeOrderToken token,
			IReadOnlyList<CPos> orderedRoute, int routeProgress, long revision)
		{
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
			orders.IssueMove(handoff.Owner, handoff.Epoch,
				Array.AsReadOnly(token.ActorIds.ToArray()), token.DestinationCell,
				Array.AsReadOnly(orderedRoute.ToArray()), routeProgress, token);
			executionLease.Verify(revision, "RecalculateFlee", EnsureActiveOwnership);
		}

		StealthRecalculateFleeResult CommitAndResult(
			StealthRecalculateFleeOwnerState prospective, long revision)
		{
			var result = new StealthRecalculateFleeResult(handoff, prospective.Disposition,
				prospective.LiveCause, prospective.MemberIds, prospective.EnemyIds,
				prospective.Evaluations, prospective.Destination, prospective.Danger,
				prospective.OrderedRoute, prospective.RouteProgress,
				prospective.LastOrderToken, prospective.Fingerprint,
				prospective.LongRouteCacheRevision);
			executionLease.Commit(revision, "RecalculateFlee", EnsureActiveOwnership,
				() => state = prospective);
			return result;
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException("Stale RecalculateFlee ownership cannot execute or restore state.");
		}

		static void ClearRoute(StealthRecalculateFleeOwnerState state)
		{
			state.Evaluations = Array.Empty<StealthRecalculateFleeRouteEvaluation>();
			state.Destination = null;
			state.Danger = null;
			state.LastOrderToken = null;
			state.LongRouteCacheRevision = null;
			state.OrderedRoute = Array.Empty<CPos>();
			state.RouteProgress = 0;
		}

		static void AdvanceRouteRevision(StealthRecalculateFleeOwnerState state)
		{
			if (state.RouteRevision == long.MaxValue)
				throw new InvalidOperationException("RecalculateFlee route revision is exhausted.");
			state.RouteRevision++;
		}

		static bool SameStateDecision(StealthRecalculateFleeOwnerState saved,
			StealthRecalculateFleeOwnerState expected)
		{
			if (saved.Evaluations.Length != expected.Evaluations.Length ||
				saved.Destination != expected.Destination || saved.Danger.HasValue != expected.Danger.HasValue ||
				saved.RouteProgress != expected.RouteProgress ||
				!saved.OrderedRoute.SequenceEqual(expected.OrderedRoute))
				return false;
			if (saved.Danger.HasValue && !SameScore(saved.Danger.Value, expected.Danger.Value))
				return false;
			return saved.Evaluations.Zip(expected.Evaluations, (left, right) =>
				left.Candidate.Cell == right.Candidate.Cell &&
				left.Candidate.IsPassable == right.Candidate.IsPassable &&
				left.Candidate.RequiresStrategicRouting == right.Candidate.RequiresStrategicRouting &&
				left.Candidate.HasDetectorCoverage == right.Candidate.HasDetectorCoverage &&
				SameScore(left.StandardDanger, right.StandardDanger) &&
				SameFacts(left.Facts, right.Facts)).All(equal => equal);
		}

		static bool SameFacts(StealthRecalculateFleeThreatFacts left,
			StealthRecalculateFleeThreatFacts right)
		{
			return left.CandidateCell == right.CandidateCell &&
				left.FormationCloaked == right.FormationCloaked &&
				left.HasDetectorCoverage == right.HasDetectorCoverage &&
				left.Members.Count == right.Members.Count &&
				left.Enemies.Count == right.Enemies.Count &&
				left.Members.Zip(right.Members, (a, b) => a.ActorId == b.ActorId &&
					a.CurrentCell == b.CurrentCell &&
					a.CurrentWeaponRangeCells == b.CurrentWeaponRangeCells &&
					a.HitPoints == b.HitPoints && a.MaximumHitPoints == b.MaximumHitPoints &&
					a.IsInWorld == b.IsInWorld && a.IsDead == b.IsDead).All(equal => equal) &&
				left.Enemies.Zip(right.Enemies, (a, b) => a.ActorId == b.ActorId &&
					a.ActorType == b.ActorType && a.CurrentCell == b.CurrentCell &&
					a.HitPoints == b.HitPoints && a.MaximumHitPoints == b.MaximumHitPoints &&
					a.CurrentWeaponRangeCells == b.CurrentWeaponRangeCells &&
					a.HasDetectorCoverage == b.HasDetectorCoverage &&
					a.IsInLocalEngagementArea == b.IsInLocalEngagementArea &&
					a.IsInWorld == b.IsInWorld && a.IsDead == b.IsDead &&
					a.IsTargetable == b.IsTargetable).All(equal => equal);
		}

		static bool SameScore(StealthTargetThreatScore left, StealthTargetThreatScore right)
		{
			return left.ThreatRating.Equals(right.ThreatRating) && left.Crossover.Equals(right.Crossover);
		}
	}
}
