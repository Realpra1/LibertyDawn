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
	/// Disabled live-only owner for Engagement/MassAttack. Entry is restricted to exact >2 Kite
	/// no-safe-plan evidence. It stays committed while the standard live crossover remains >1.
	/// </summary>
	public sealed class StealthMassAttackBehavior
	{
		sealed class OwnerState
		{
			public StealthMassAttackEntryState EntryState;
			public int LastObservedTick = -1;
			public int LastEvaluationTick = -1;
			public StealthMassAttackPhase Phase;
			public StealthMassAttackDisposition Disposition = StealthMassAttackDisposition.Reacquire;
			public uint? TargetId;
			public CPos? TargetCell;
			public int TargetHitPoints;
			public int TargetMaximumHitPoints;
			public StealthMassAttackLiveFingerprint Fingerprint;
			public StealthMassAttackEvaluation Evaluation;
			public uint[] DefenderIds = Array.Empty<uint>();
			public uint[] ObjectiveIds = Array.Empty<uint>();
			public StealthMassAttackActivityContext Activity =
				new StealthMassAttackActivityContext(false, 0, null, null);
			public StealthMassAttackOrderToken LastOrderToken;
			public StealthMassAttackOrderToken PriorOrderToken;

			public OwnerState Clone()
			{
				var clone = (OwnerState)MemberwiseClone();
				clone.DefenderIds = DefenderIds.ToArray();
				clone.ObjectiveIds = ObjectiveIds.ToArray();
				return clone;
			}
		}

		readonly StealthMassAttackHandoff handoff;
		readonly StealthApproachMission mission;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthMassAttackLiveWorld liveWorld;
		readonly IStealthMassAttackThreatAdapter threatAdapter;
		readonly IStealthMassAttackOrders orders;
		readonly StealthBehaviorExecutionLease executionLease = new StealthBehaviorExecutionLease();
		OwnerState state = new OwnerState();

		public StealthMassAttackBehavior(StealthMassAttackHandoff handoff,
			IStealthLifecycleOwnershipGuard ownershipGuard, IStealthMassAttackLiveWorld liveWorld,
			IStealthMassAttackThreatAdapter threatAdapter, IStealthMassAttackOrders orders)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.MassAttack)
				throw new ArgumentException("MassAttack requires MassAttack ownership.", nameof(handoff));
			mission = handoff.Mission ?? throw new ArgumentException(
				"MassAttack requires one immutable mission.", nameof(handoff));
			this.ownershipGuard = ownershipGuard ?? throw new ArgumentNullException(nameof(ownershipGuard));
			this.liveWorld = liveWorld ?? throw new ArgumentNullException(nameof(liveWorld));
			this.threatAdapter = threatAdapter ?? throw new ArgumentNullException(nameof(threatAdapter));
			this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
		}

		public StealthMassAttackResult Execute()
		{
			var revision = executionLease.Acquire("MassAttack", EnsureActiveOwnership);
			try { return Execute(revision); }
			finally { executionLease.Release(revision); }
		}

		StealthMassAttackResult Execute(long revision)
		{
			var live = ReadLive("execute", revision);
			var decision = StealthMassAttackLiveDecision.Create(live);
			var prospective = state.Clone();
			if (live.Tick < prospective.LastObservedTick)
				throw new InvalidOperationException("Live MassAttack ticks must not move backwards.");
			if (decision.Members.Length == 0)
			{
				prospective.EntryState = StealthMassAttackEntryState.SkippedZeroMembers;
				prospective.LastObservedTick = live.Tick;
				prospective.DefenderIds = decision.DefenderActorIds.ToArray();
				prospective.ObjectiveIds = decision.ObjectiveActorIds.ToArray();
				prospective.Activity = new StealthMassAttackActivityContext(false, 0, null, null);
				ClearSelection(prospective);
				prospective.Disposition = StealthMassAttackDisposition.RecalculateFlee;
				return CommitAndResult(prospective, decision, revision);
			}

			if (prospective.EntryState == StealthMassAttackEntryState.SkippedZeroMembers ||
				prospective.EntryState == StealthMassAttackEntryState.ExitedRecalculate)
				throw new InvalidOperationException("Completed MassAttack ownership cannot resume with live members.");
			if (prospective.EntryState == StealthMassAttackEntryState.Pristine)
				ValidateEntry(decision, revision);
			prospective.EntryState = StealthMassAttackEntryState.Validated;
			prospective.LastObservedTick = live.Tick;
			prospective.DefenderIds = decision.DefenderActorIds.ToArray();
			prospective.ObjectiveIds = decision.ObjectiveActorIds.ToArray();
			prospective.Activity = StealthMassAttackActivityContext.From(decision);
			if (decision.TargetlessDisposition.HasValue)
			{
				prospective.Activity.ValidatePriorPair(handoff,
					prospective.LastOrderToken, prospective.PriorOrderToken);
				ClearSelection(prospective);
				prospective.Activity = new StealthMassAttackActivityContext(false, 0, null, null);
				prospective.Disposition = decision.TargetlessDisposition.Value;
				return CommitAndResult(prospective, decision, revision);
			}

			var retained = prospective.TargetId.HasValue ? decision.FindTarget(prospective.TargetId.Value) : null;
			var evaluated = EvaluateAll(decision, revision);
			var selected = retained ?? evaluated.OrderByDescending(item => item.Value.Threat.SelectedTargetThreat)
				.ThenBy(item => item.Key.ActorId).First().Key;
			prospective.TargetId = selected.ActorId;
			prospective.TargetCell = selected.CurrentCell;
			prospective.TargetHitPoints = selected.HitPoints;
			prospective.TargetMaximumHitPoints = selected.MaximumHitPoints;
			prospective.Fingerprint = decision.Fingerprint(selected);
			prospective.Evaluation = evaluated[selected];
			prospective.LastEvaluationTick = live.Tick;
			retained = selected;

			prospective.Phase = decision.PhaseFor(retained);
			if (prospective.Evaluation.Threat.StandardScore.Crossover <= 1)
			{
				prospective.Activity.ValidatePriorPair(handoff,
					prospective.LastOrderToken, prospective.PriorOrderToken);
				prospective.EntryState = StealthMassAttackEntryState.ExitedRecalculate;
				prospective.Disposition = StealthMassAttackDisposition.RecalculateFlee;
				prospective.Activity = new StealthMassAttackActivityContext(false, 0, null, null);
				return CommitAndResult(prospective, decision, revision);
			}

			var desired = prospective.Activity.Next(handoff, prospective.Phase,
				decision.MemberActorIds, retained.ActorId, retained.CurrentCell,
				prospective.LastOrderToken, prospective.PriorOrderToken,
				out var shouldApply, out var priorOrder);
			if (shouldApply)
				ApplyOrder(desired, revision);
			prospective.LastOrderToken = desired;
			prospective.PriorOrderToken = priorOrder;
			prospective.Disposition = StealthMassAttackDisposition.Retain;
			return CommitAndResult(prospective, decision, revision);
		}

		public MiniYamlNode SerializePrivateState(string key = "MassAttack")
		{
			return StealthMassAttackPersistence.Serialize(key, handoff, mission, ToPrivateState(state));
		}

		public void RestorePrivateState(MiniYamlNode node)
		{
			var revision = executionLease.Acquire("MassAttack", EnsureActiveOwnership);
			try
			{
				var restored = StealthMassAttackPersistence.Restore(node, handoff, mission);
				var live = ReadLive("restore", revision);
				ValidateRestored(restored, live, revision);
				var prospective = FromPrivateState(restored);
				executionLease.Commit(revision, "MassAttack", EnsureActiveOwnership,
					() => state = prospective);
			}
			finally { executionLease.Release(revision); }
		}

		internal void RestorePersistedState(MiniYamlNode node)
		{
			var revision = executionLease.Acquire("MassAttack", EnsureActiveOwnership);
			try
			{
				var restored = StealthMassAttackPersistence.Restore(node, handoff, mission);
				var prospective = FromPrivateState(restored);
				executionLease.Commit(revision, "MassAttack", EnsureActiveOwnership,
					() => state = prospective);
			}
			finally { executionLease.Release(revision); }
		}

		void ValidateEntry(StealthMassAttackLiveDecision decision, long revision)
		{
			var evidence = handoff.Evidence;
			var target = decision.FindTarget(evidence.SelectedTargetActorId);
			if (target == null || target.CurrentCell != evidence.SelectedTargetCurrentCell ||
				!decision.MemberActorIds.SequenceEqual(evidence.FriendlyActorIds) ||
				!decision.DefenderActorIds.SequenceEqual(evidence.EnemyActorIds) ||
				decision.FormationCloaked != evidence.FormationCloaked ||
				decision.EntryFingerprint(target).Canonical != evidence.LiveFingerprint)
				throw new InvalidOperationException("MassAttack entry evidence is stale or inconsistent.");
			var current = Calculate(decision.Facts(target), revision);
			if (!SameScore(current.StandardScore, evidence.StandardScore) ||
				current.StandardScore.Crossover <= 2)
				throw new InvalidOperationException("MassAttack entry score is not the canonical live >2 result.");
		}

		void ValidateRestored(StealthMassAttackPrivateState restored,
			StealthMassAttackLiveSnapshot live, long revision)
		{
			var decision = StealthMassAttackLiveDecision.Create(live);
			var currentActivity = StealthMassAttackActivityContext.From(decision);
			if (restored.EntryState == StealthMassAttackEntryState.Pristine)
			{
				if (!restored.Activity.Same(currentActivity))
					throw new InvalidOperationException("Pristine MassAttack activity is not current.");
				return;
			}

			if (restored.EntryState == StealthMassAttackEntryState.SkippedZeroMembers)
			{
				if (decision.Members.Length != 0 || live.Tick != restored.LastObservedTick ||
					!restored.DefenderIds.SequenceEqual(decision.DefenderActorIds) ||
					!restored.ObjectiveIds.SequenceEqual(decision.ObjectiveActorIds))
					throw new InvalidOperationException("Saved skipped-zero MassAttack cause is not current.");
				return;
			}

			var exited = restored.EntryState == StealthMassAttackEntryState.ExitedRecalculate;
			if (live.Tick != restored.LastObservedTick ||
				restored.LastEvaluationTick > live.Tick ||
				!restored.DefenderIds.SequenceEqual(decision.DefenderActorIds) ||
				!restored.ObjectiveIds.SequenceEqual(decision.ObjectiveActorIds) ||
				(!exited && !restored.Activity.Same(currentActivity)))
				throw new InvalidOperationException("Saved MassAttack state is not current live state.");
			if (decision.TargetlessDisposition.HasValue)
			{
				if (restored.Disposition != decision.TargetlessDisposition.Value ||
					restored.TargetId.HasValue || restored.Phase != StealthMassAttackPhase.Advance)
					throw new InvalidOperationException("Saved MassAttack targetless disposition has no live cause.");
				restored.Activity.ValidateSaved(handoff, restored.Phase,
					decision.MemberActorIds, null, null, null, null);
				return;
			}

			var target = restored.TargetId.HasValue ? decision.FindTarget(restored.TargetId.Value) : null;
			if (target == null || target.CurrentCell != restored.TargetCell ||
				target.HitPoints != restored.TargetHitPoints ||
				target.MaximumHitPoints != restored.TargetMaximumHitPoints ||
				restored.Fingerprint == null || !restored.Fingerprint.Equals(decision.Fingerprint(target)))
				throw new InvalidOperationException("Saved MassAttack target fingerprint is stale.");
			var evaluated = EvaluateAll(decision, revision)[target];
			if (!SameEvaluation(restored.Evaluation, evaluated))
				throw new InvalidOperationException("Saved MassAttack standard live evaluation is stale.");
			var expected = evaluated.Threat.StandardScore.Crossover <= 1 ?
				StealthMassAttackDisposition.RecalculateFlee : StealthMassAttackDisposition.Retain;
			if (restored.Disposition != expected)
				throw new InvalidOperationException("Saved MassAttack commitment has no current crossover cause.");
			var phase = decision.PhaseFor(target);
			if (restored.Phase != phase)
				throw new InvalidOperationException("Saved MassAttack phase is not current.");
			if (expected == StealthMassAttackDisposition.Retain)
			{
				if (restored.LastOrderToken == null ||
					restored.LastOrderToken.Owner != handoff.Owner ||
					restored.LastOrderToken.Epoch != handoff.Epoch ||
					restored.LastOrderToken.Phase != phase ||
					!restored.LastOrderToken.ActorIds.SequenceEqual(decision.MemberActorIds) ||
					restored.LastOrderToken.TargetActorId != target.ActorId ||
					restored.LastOrderToken.TargetCurrentCell != target.CurrentCell)
					throw new InvalidOperationException("Saved MassAttack order token is not current live activity.");
			}

			if (exited)
				currentActivity.ValidatePriorPair(handoff,
					restored.LastOrderToken, restored.PriorOrderToken);
			else
				restored.Activity.ValidateSaved(handoff, restored.Phase, decision.MemberActorIds,
					restored.TargetId, restored.TargetCell, restored.LastOrderToken,
					restored.PriorOrderToken);
		}

		Dictionary<StealthMassAttackActorSnapshot, StealthMassAttackEvaluation> EvaluateAll(
			StealthMassAttackLiveDecision decision, long revision)
		{
			var result = new Dictionary<StealthMassAttackActorSnapshot, StealthMassAttackEvaluation>();
			StealthTargetThreatScore? standard = null;
			foreach (var target in decision.Defenders)
			{
				var facts = decision.Facts(target);
				var threat = Calculate(facts, revision);
				if (standard.HasValue && !SameScore(standard.Value, threat.StandardScore))
					throw new InvalidOperationException("MassAttack crossover changed across one live evaluation.");
				standard = threat.StandardScore;
				result.Add(target, new StealthMassAttackEvaluation(facts, threat));
			}

			return result;
		}

		StealthMassAttackLiveSnapshot ReadLive(string operation, long revision)
		{
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
			var live = liveWorld.Read(mission) ?? throw new InvalidOperationException(
				"The live MassAttack view returned no snapshot during " + operation + ".");
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
			return live;
		}

		StealthMassAttackThreatResult Calculate(StealthMassAttackThreatFacts facts, long revision)
		{
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
			var result = threatAdapter.Calculate(facts);
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
			return result;
		}

		void ApplyOrder(StealthMassAttackOrderToken token, long revision)
		{
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
			var actors = Array.AsReadOnly(token.ActorIds.ToArray());
			if (token.Phase == StealthMassAttackPhase.Attack)
				orders.IssueAttack(handoff.Owner, handoff.Epoch, actors,
					token.TargetActorId, token.TargetCurrentCell, token);
			else
				orders.IssueMove(handoff.Owner, handoff.Epoch, actors,
					token.TargetActorId, token.TargetCurrentCell, token);
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
		}

		StealthMassAttackResult CommitAndResult(OwnerState prospective,
			StealthMassAttackLiveDecision decision, long revision)
		{
			var result = new StealthMassAttackResult(handoff, mission,
				prospective.Disposition, prospective.Phase, prospective.TargetId,
				prospective.TargetCell, decision.MemberActorIds, prospective.DefenderIds,
				prospective.ObjectiveIds, prospective.Evaluation?.Facts,
				prospective.Evaluation?.Threat,
				prospective.Disposition == StealthMassAttackDisposition.Retain ?
					prospective.LastOrderToken : null);
			executionLease.Commit(revision, "MassAttack", EnsureActiveOwnership, () => state = prospective);
			return result;
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException("Stale MassAttack ownership cannot execute or restore state.");
		}

		static bool SameScore(StealthTargetThreatScore left, StealthTargetThreatScore right)
		{
			return left.ThreatRating.Equals(right.ThreatRating) && left.Crossover.Equals(right.Crossover);
		}

		static bool SameEvaluation(StealthMassAttackEvaluation left, StealthMassAttackEvaluation right)
		{
			return StealthMassAttackPersistenceNodes.SameFacts(left?.Facts, right?.Facts) &&
				left.Threat.SelectedTargetThreat.Equals(right.Threat.SelectedTargetThreat) &&
				SameScore(left.Threat.StandardScore, right.Threat.StandardScore);
		}

		static void ClearSelection(OwnerState state)
		{
			state.TargetId = null;
			state.TargetCell = null;
			state.TargetHitPoints = 0;
			state.TargetMaximumHitPoints = 0;
			state.LastEvaluationTick = -1;
			state.Fingerprint = null;
			state.Evaluation = null;
			state.LastOrderToken = null;
			state.PriorOrderToken = null;
			state.Phase = StealthMassAttackPhase.Advance;
		}

		static StealthMassAttackPrivateState ToPrivateState(OwnerState state)
		{
			return new StealthMassAttackPrivateState(state.EntryState, state.LastObservedTick,
				state.LastEvaluationTick, state.Phase, state.Disposition, state.TargetId,
				state.TargetCell, state.TargetHitPoints, state.TargetMaximumHitPoints,
				state.Fingerprint, state.Evaluation, state.DefenderIds, state.ObjectiveIds,
				state.Activity, state.LastOrderToken, state.PriorOrderToken);
		}

		static OwnerState FromPrivateState(StealthMassAttackPrivateState state)
		{
			return new OwnerState
			{
				EntryState = state.EntryState,
				LastObservedTick = state.LastObservedTick,
				LastEvaluationTick = state.LastEvaluationTick,
				Phase = state.Phase,
				Disposition = state.Disposition,
				TargetId = state.TargetId,
				TargetCell = state.TargetCell,
				TargetHitPoints = state.TargetHitPoints,
				TargetMaximumHitPoints = state.TargetMaximumHitPoints,
				Fingerprint = state.Fingerprint,
				Evaluation = state.Evaluation,
				DefenderIds = state.DefenderIds.ToArray(),
				ObjectiveIds = state.ObjectiveIds.ToArray(),
				Activity = state.Activity,
				LastOrderToken = state.LastOrderToken,
				PriorOrderToken = state.PriorOrderToken
			};
		}
	}
}
