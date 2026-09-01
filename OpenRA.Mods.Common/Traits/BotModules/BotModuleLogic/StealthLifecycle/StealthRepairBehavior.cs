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
	public sealed class StealthRepairBehavior
	{
		readonly StealthRepairHandoff handoff;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthRepairLiveWorld liveWorld;
		readonly IStealthRepairThreatAdapter threatAdapter;
		readonly IStealthRepairStrategicCache strategicCache;
		readonly IStealthRepairOrders orders;
		readonly StealthBehaviorExecutionLease executionLease = new StealthBehaviorExecutionLease();
		StealthRepairOwnerState state = new StealthRepairOwnerState();

		public StealthRepairBehavior(StealthRepairHandoff handoff,
			IStealthLifecycleOwnershipGuard ownershipGuard, IStealthRepairLiveWorld liveWorld,
			IStealthRepairThreatAdapter threatAdapter, IStealthRepairStrategicCache strategicCache,
			IStealthRepairOrders orders)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.Repair || handoff.Source == null ||
				handoff.Mission == null || handoff.Resume == null)
				throw new ArgumentException("Repair requires one complete typed Damage handoff.", nameof(handoff));
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
			var decision = StealthRepairLiveDecision.Create(ReadLive("execute", revision));
			var prospective = state.Clone();
			if (decision.Tick < prospective.LastObservedTick)
				throw new InvalidOperationException("Live Repair ticks must not move backwards.");
			if (!prospective.EntryValidated)
				ValidateEntry(decision);
			prospective.EntryValidated = true;
			prospective.LastObservedTick = decision.Tick;
			prospective.Fingerprint = decision.Fingerprint;
			prospective.MemberIds = decision.MemberActorIds;
			prospective.EnemyIds = decision.EnemyActorIds;
			prospective.Disposition = StealthRepairDisposition.Retain;
			prospective.Completion = null;

			if (decision.Members.Length == 0)
			{
				ClearRoute(prospective, true, true);
				prospective.LiveCause = StealthRepairLiveCause.NoLiveMembers;
				prospective.Disposition = StealthRepairDisposition.SquadConstruction;
				return CommitAndResult(prospective, Array.Empty<uint>(), revision);
			}

			var repairMembers = RepairMembers(decision);
			var repairIds = repairMembers.Select(member => member.ActorId).ToArray();
			if (repairMembers.Length == 0)
			{
				var completionMembers = CompletionMembers(decision);
				var completion = decision.Completion(completionMembers);
				if (completion != null)
				{
					var completionIds = completionMembers.Select(member => member.ActorId).ToArray();
					ClearRoute(prospective, false);
					prospective.Completion = completion;
					prospective.LiveCause = StealthRepairLiveCause.RepairComplete;
					prospective.Disposition = StealthRepairDisposition.Start;
					return CommitAndResult(prospective, completionIds, revision);
				}
			}

			if (repairMembers.Length == 0 || decision.PassableRoutes.Length == 0)
				return ResumeFight(prospective, repairIds, revision);

			var evaluated = decision.PassableRoutes.Select(route => decision.Evaluate(route,
				repairMembers, facts => CalculateDanger(facts, revision))).ToArray();
			if (!TrySelectRoute(evaluated, prospective, decision, repairMembers, revision,
				out var selected, out var orderedRoute, out var cacheRevision))
			{
				return ResumeFight(prospective, repairIds, revision);
			}

			var atOption = decision.AtOption(selected.Option, repairMembers);
			var kind = atOption ? StealthRepairOrderKind.Repair : StealthRepairOrderKind.Retreat;
			var routeChanged = !prospective.OptionId.HasValue ||
				prospective.OptionId.Value != selected.Option.ActorId ||
				prospective.RouteId != selected.Route.StableIdentity ||
				!prospective.OrderedRoute.SequenceEqual(orderedRoute) ||
				prospective.LastOrderToken == null || prospective.LastOrderToken.Kind != kind ||
				!prospective.LastOrderToken.ActorIds.SequenceEqual(repairIds);
			if (routeChanged && prospective.LastOrderToken != null)
				AdvanceRouteRevision(prospective);
			if (routeChanged)
			{
				prospective.RouteProgress = selected.Route.RequiresStrategicRouting && !atOption ?
					0 : orderedRoute.Length - 1;
				prospective.LastOrderToken = null;
			}

			prospective.Evaluations = evaluated;
			prospective.OptionId = selected.Option.ActorId;
			prospective.RouteId = selected.Route.StableIdentity;
			prospective.Danger = selected.StandardDanger;
			prospective.OrderedRoute = orderedRoute;
			prospective.LongRouteCacheRevision = cacheRevision;
			if (!routeChanged && kind == StealthRepairOrderKind.Retreat &&
				prospective.LastOrderToken != null && prospective.RouteProgress < orderedRoute.Length - 1 &&
				repairMembers.All(member => member.CurrentCell == orderedRoute[prospective.RouteProgress]))
			{
				AdvanceRouteRevision(prospective);
				prospective.RouteProgress++;
				prospective.LastOrderToken = null;
			}

			var desired = prospective.LastOrderToken ?? new StealthRepairOrderToken(handoff.Owner, handoff.Epoch,
				repairIds, selected.Option.ActorId, selected.Route.StableIdentity, kind,
				prospective.RouteRevision, decision.ActivityRevision);
			var observed = decision.HasActivityObservation &&
				(desired.Equals(decision.ActiveOrderToken) || desired.Equals(decision.CompletedOrderToken));
			if (!observed)
				ApplyOrder(selected, desired, prospective.OrderedRoute,
					prospective.RouteProgress, revision);
			prospective.LastOrderToken = desired;
			prospective.LiveCause = atOption ? StealthRepairLiveCause.Healing :
				StealthRepairLiveCause.Retreating;
			return CommitAndResult(prospective, repairIds, revision);
		}

		public MiniYamlNode SerializePrivateState(string key = "Repair")
		{
			return StealthRepairPersistence.Serialize(key, handoff, state);
		}

		public void RestorePrivateState(MiniYamlNode node)
		{
			RestoreState(node, true);
		}

		internal void RestorePersistedState(MiniYamlNode node)
		{
			RestoreState(node, false);
		}

		void RestoreState(MiniYamlNode node, bool validateLive)
		{
			var revision = executionLease.Acquire("Repair", EnsureActiveOwnership);
			try
			{
				var restored = StealthRepairPersistence.Restore(node, handoff);
				if (validateLive)
					ValidateRestored(restored,
						StealthRepairLiveDecision.Create(ReadLive("restore", revision)), revision);
				executionLease.Commit(revision, "Repair", EnsureActiveOwnership,
					() => state = restored.Clone());
			}
			finally { executionLease.Release(revision); }
		}

		void ValidateEntry(StealthRepairLiveDecision decision)
		{
			var resume = handoff.Resume;
			if (decision.DamageEventId != handoff.DamageEventId ||
				decision.DamageTick != handoff.DamageTick ||
				decision.DamageSourceActorId != handoff.DamageSourceActorId ||
				decision.DamageAmount != handoff.DamageAmount ||
				decision.ResumeFingerprint != resume.ContextFingerprint ||
				!decision.MemberActorIds.SequenceEqual(resume.MemberActorIds) ||
				!decision.EnemyActorIds.SequenceEqual(resume.EnemyActorIds) ||
				handoff.DamagedMembers.Any(damaged => !decision.Members.Any(member =>
					member.ActorId == damaged.ActorId && member.HitPoints == damaged.HitPoints &&
					member.MaximumHitPoints == damaged.MaximumHitPoints)) ||
				(resume.SelectedTargetActorId.HasValue && !decision.Enemies.Any(enemy =>
					enemy.ActorId == resume.SelectedTargetActorId &&
					enemy.CurrentCell == resume.SelectedTargetCurrentCell)))
				throw new InvalidOperationException("Repair entry Damage cause or resume context is stale or forged.");
		}

		void ValidateRestored(StealthRepairOwnerState restored,
			StealthRepairLiveDecision decision, long revision)
		{
			if (!restored.EntryValidated)
			{
				ValidateEntry(decision);
				if (restored.LastObservedTick != -1)
					throw new InvalidOperationException("Pristine Repair state is not canonical.");

				return;
			}

			if (restored.LastObservedTick != decision.Tick ||
				restored.Fingerprint != decision.Fingerprint ||
				!restored.MemberIds.SequenceEqual(decision.MemberActorIds) ||
				!restored.EnemyIds.SequenceEqual(decision.EnemyActorIds))
				throw new InvalidOperationException("Saved Repair live facts are stale.");

			var expected = EvaluateCurrent(restored, decision, revision);
			if (!SameDecision(restored, expected))
				throw new InvalidOperationException("Saved Repair route, safety, progress, or completion was altered.");
			if (restored.LastOrderToken != null &&
				(restored.LastOrderToken.Epoch != handoff.Epoch ||
				(decision.HasActivityObservation &&
					restored.LastOrderToken.ActivityRevision > decision.ActivityRevision)))
				throw new InvalidOperationException("Saved Repair order token is stale or forged.");
			if (restored.LongRouteCacheRevision.HasValue)
			{
				var selected = restored.Evaluations.Single(evaluation =>
					evaluation.Route.StableIdentity == restored.RouteId);
				var cached = ReadLongRoute(selected, revision);
				if (cached.Revision != restored.LongRouteCacheRevision ||
					!cached.Waypoints.SequenceEqual(restored.OrderedRoute))
					throw new InvalidOperationException("Saved passive Repair long-route revision is stale.");
			}
		}

		StealthRepairOwnerState EvaluateCurrent(StealthRepairOwnerState restored,
			StealthRepairLiveDecision decision, long revision)
		{
			var expected = restored.Clone();
			expected.EntryValidated = true;
			expected.LastObservedTick = decision.Tick;
			expected.Fingerprint = decision.Fingerprint;
			expected.MemberIds = decision.MemberActorIds;
			expected.EnemyIds = decision.EnemyActorIds;
			expected.Disposition = StealthRepairDisposition.Retain;
			expected.Completion = null;
			if (decision.Members.Length == 0)
			{
				ClearRoute(expected, true, true);
				expected.Disposition = StealthRepairDisposition.SquadConstruction;
				expected.LiveCause = StealthRepairLiveCause.NoLiveMembers;

				return expected;
			}

			var repairMembers = RepairMembers(decision);
			if (repairMembers.Length == 0)
			{
				var completion = decision.Completion(CompletionMembers(decision));
				if (completion != null)
				{
					ClearRoute(expected, false);
					expected.Disposition = StealthRepairDisposition.Start;
					expected.LiveCause = StealthRepairLiveCause.RepairComplete;
					expected.Completion = completion;

					return expected;
				}
			}

			if (repairMembers.Length == 0 || decision.PassableRoutes.Length == 0)
			{
				ClearRoute(expected, true, true);
				expected.Disposition = StealthRepairDisposition.ResumeFight;
				expected.LiveCause = StealthRepairLiveCause.NoSafeRepair;

				return expected;
			}

			expected.Evaluations = decision.PassableRoutes.Select(route => decision.Evaluate(route,
				repairMembers, facts => CalculateDanger(facts, revision))).ToArray();
			if (!TrySelectRoute(expected.Evaluations, restored, decision, repairMembers, revision,
				out var selected, out var orderedRoute, out var cacheRevision))
			{
				ClearRoute(expected, true, true);
				expected.Disposition = StealthRepairDisposition.ResumeFight;
				expected.LiveCause = StealthRepairLiveCause.NoSafeRepair;

				return expected;
			}

			expected.OptionId = selected.Option.ActorId;
			expected.RouteId = selected.Route.StableIdentity;
			expected.OrderedRoute = orderedRoute;
			expected.LongRouteCacheRevision = cacheRevision;
			expected.Danger = selected.StandardDanger;
			expected.LiveCause = decision.AtOption(selected.Option, repairMembers) ?
				StealthRepairLiveCause.Healing : StealthRepairLiveCause.Retreating;
			return expected;
		}

		StealthRepairResult ResumeFight(StealthRepairOwnerState prospective,
			uint[] repairIds, long revision, bool clearEvaluations = true)
		{
			ClearRoute(prospective, clearEvaluations, true);
			prospective.LiveCause = StealthRepairLiveCause.NoSafeRepair;
			prospective.Disposition = StealthRepairDisposition.ResumeFight;
			return CommitAndResult(prospective, repairIds, revision);
		}

		StealthRepairMemberSnapshot[] RepairMembers(StealthRepairLiveDecision decision)
		{
			return decision.Members.Where(member => handoff.DamagedMembers.Any(
				damaged => damaged.ActorId == member.ActorId) &&
				member.HitPoints < member.MaximumHitPoints)
				.OrderBy(member => member.ActorId).ToArray();
		}

		StealthRepairMemberSnapshot[] CompletionMembers(StealthRepairLiveDecision decision)
		{
			return decision.Members.Where(member => handoff.DamagedMembers.Any(
				damaged => damaged.ActorId == member.ActorId) && member.IsRepaired)
				.OrderBy(member => member.ActorId).ToArray();
		}

		StealthRepairLiveSnapshot ReadLive(string operation, long revision)
		{
			executionLease.Verify(revision, "Repair", EnsureActiveOwnership);
			var live = liveWorld.Read(handoff.Mission) ?? throw new InvalidOperationException(
				"The live Repair view returned no snapshot during " + operation + ".");
			executionLease.Verify(revision, "Repair", EnsureActiveOwnership);
			return live;
		}

		StealthTargetThreatScore CalculateDanger(StealthRepairThreatFacts facts, long revision)
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
				Array.AsReadOnly(selected.Route.Cells.ToArray())) ??
				throw new InvalidOperationException("Passive Repair route cache returned no snapshot.");
			executionLease.Verify(revision, "Repair", EnsureActiveOwnership);
			return cached;
		}

		bool TrySelectRoute(IReadOnlyList<StealthRepairRouteEvaluation> evaluations,
			StealthRepairOwnerState current, StealthRepairLiveDecision decision,
			IReadOnlyList<StealthRepairMemberSnapshot> repairMembers, long revision,
			out StealthRepairRouteEvaluation selected, out CPos[] orderedRoute,
			out long? cacheRevision)
		{
			foreach (var candidate in StealthRepairLiveDecision.OrderedSafe(evaluations))
			{
				if (!candidate.Route.RequiresStrategicRouting ||
					decision.AtOption(candidate.Option, repairMembers))
				{
					selected = candidate;
					orderedRoute = candidate.Route.Cells.ToArray();
					cacheRevision = null;
					return true;
				}

				if (current.RouteId == candidate.Route.StableIdentity && current.OrderedRoute.Length != 0 &&
					current.LongRouteCacheRevision.HasValue)
				{
					selected = candidate;
					orderedRoute = current.OrderedRoute.ToArray();
					cacheRevision = current.LongRouteCacheRevision;
					return true;
				}

				var cached = ReadLongRoute(candidate, revision);
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

		void ApplyOrder(StealthRepairRouteEvaluation selected,
			StealthRepairOrderToken token, IReadOnlyList<CPos> orderedRoute,
			int routeProgress, long revision)
		{
			executionLease.Verify(revision, "Repair", EnsureActiveOwnership);
			orders.IssueRepair(handoff.Owner, handoff.Epoch,
				Array.AsReadOnly(token.ActorIds.ToArray()), selected.Option.ActorId,
				Array.AsReadOnly(orderedRoute.ToArray()), routeProgress, token.Kind, token);
			executionLease.Verify(revision, "Repair", EnsureActiveOwnership);
		}

		StealthRepairResult CommitAndResult(StealthRepairOwnerState prospective,
			uint[] repairIds, long revision)
		{
			var result = new StealthRepairResult(handoff, prospective.Disposition,
				prospective.LiveCause, prospective.MemberIds, repairIds, prospective.EnemyIds,
				prospective.Evaluations, prospective.OptionId, prospective.RouteId,
				prospective.RouteProgress, prospective.Danger, prospective.LastOrderToken,
				prospective.Completion, prospective.Fingerprint, prospective.LongRouteCacheRevision);
			executionLease.Commit(revision, "Repair", EnsureActiveOwnership, () => state = prospective);
			return result;
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException("Stale Repair ownership cannot execute or restore state.");
		}

		static void ClearRoute(StealthRepairOwnerState state, bool evaluations,
			bool retireToken = false)
		{
			if (evaluations)
				state.Evaluations = Array.Empty<StealthRepairRouteEvaluation>();
			state.OptionId = null;
			state.RouteId = null;
			state.RouteProgress = 0;
			state.Danger = null;
			if (retireToken)
				state.LastOrderToken = null;
			state.LongRouteCacheRevision = null;
			state.OrderedRoute = Array.Empty<CPos>();
		}

		static bool SameDecision(StealthRepairOwnerState saved, StealthRepairOwnerState expected)
		{
			var compareEvaluations = expected.Disposition != StealthRepairDisposition.Start;
			if (saved.Disposition != expected.Disposition || saved.LiveCause != expected.LiveCause ||
				saved.OptionId != expected.OptionId || saved.RouteId != expected.RouteId ||
				saved.RouteProgress != expected.RouteProgress ||
				!saved.OrderedRoute.SequenceEqual(expected.OrderedRoute) ||
				saved.Danger.HasValue != expected.Danger.HasValue ||
				(compareEvaluations && saved.Evaluations.Length != expected.Evaluations.Length) ||
				(saved.Danger.HasValue && !StealthRepairResult.SameScore(saved.Danger.Value,
					expected.Danger.Value)))
				return false;
			if ((saved.Completion == null) != (expected.Completion == null) ||
				(saved.Completion != null && (saved.Completion.Tick != expected.Completion.Tick ||
					!saved.Completion.Members.Select(member => (member.ActorId, member.HitPoints,
						member.MaximumHitPoints)).SequenceEqual(expected.Completion.Members.Select(member =>
							(member.ActorId, member.HitPoints, member.MaximumHitPoints))))))
				return false;
			return !compareEvaluations ||
				saved.Evaluations.Zip(expected.Evaluations, SameEvaluation).All(equal => equal);
		}

		static void AdvanceRouteRevision(StealthRepairOwnerState state)
		{
			if (state.RouteRevision == long.MaxValue)
				throw new InvalidOperationException("Repair route revision is exhausted.");
			state.RouteRevision++;
		}

		static bool SameEvaluation(StealthRepairRouteEvaluation left,
			StealthRepairRouteEvaluation right)
		{
			return left.Option.ActorId == right.Option.ActorId &&
				left.Option.CurrentCell == right.Option.CurrentCell &&
				left.Route.StableIdentity == right.Route.StableIdentity &&
				left.Route.Cells.SequenceEqual(right.Route.Cells) &&
				left.Route.IsPassable == right.Route.IsPassable &&
				left.Route.RequiresStrategicRouting == right.Route.RequiresStrategicRouting &&
				left.Route.HasDetectorCoverage == right.Route.HasDetectorCoverage &&
				StealthRepairResult.SameScore(left.StandardDanger, right.StandardDanger) &&
				SameFacts(left.Facts, right.Facts);
		}

		static bool SameFacts(StealthRepairThreatFacts left, StealthRepairThreatFacts right)
		{
			return left.RepairOptionActorId == right.RepairOptionActorId &&
				left.FormationCloaked == right.FormationCloaked &&
				left.HasDetectorCoverage == right.HasDetectorCoverage &&
				left.RouteCells.SequenceEqual(right.RouteCells) &&
				left.Members.Count == right.Members.Count && left.Enemies.Count == right.Enemies.Count &&
				left.Members.Zip(right.Members, (a, b) => a.ActorId == b.ActorId &&
					a.CurrentCell == b.CurrentCell && a.CurrentWeaponRangeCells == b.CurrentWeaponRangeCells &&
					a.HitPoints == b.HitPoints && a.MaximumHitPoints == b.MaximumHitPoints).All(equal => equal) &&
				left.Enemies.Zip(right.Enemies, (a, b) => a.ActorId == b.ActorId &&
					a.ActorType == b.ActorType && a.CurrentCell == b.CurrentCell &&
					a.HitPoints == b.HitPoints && a.MaximumHitPoints == b.MaximumHitPoints &&
					a.CurrentWeaponRangeCells == b.CurrentWeaponRangeCells &&
					a.IsDetector == b.IsDetector).All(equal => equal);
		}
	}
}
