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
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Disabled local live-world owner for Engagement/Kite. Every safety-affecting live fact is
	/// fingerprinted; strategic/actor caches and target claims are deliberately absent.
	/// </summary>
	public sealed class StealthKiteBehavior
	{
		sealed class OwnerState
		{
			public int LastObservedTick = -1;
			public int LastPlanTick = -1;
			public StealthKitePhase Phase;
			public StealthKiteDisposition Disposition = StealthKiteDisposition.Reacquire;
			public uint? TargetId;
			public CPos? TargetCell;
			public int TargetHitPoints;
			public int TargetMaximumHitPoints;
			public int FireBaselineTargetHitPoints = -1;
			public StealthKiteLiveFingerprint Fingerprint;
			public StealthKitePlan Plan;
			public StealthKiteFallbackEvidence FallbackEvidence;
			public uint[] DefenderIds = Array.Empty<uint>();
			public uint[] ObjectiveIds = Array.Empty<uint>();
			public StealthKiteOrderToken LastOrderToken;

			public OwnerState Clone()
			{
				var clone = (OwnerState)MemberwiseClone();
				clone.DefenderIds = DefenderIds.ToArray();
				clone.ObjectiveIds = ObjectiveIds.ToArray();
				return clone;
			}
		}

		readonly StealthKiteHandoff handoff;
		readonly StealthApproachMission mission;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthKiteLiveWorld liveWorld;
		readonly IStealthKiteThreatAdapter threatAdapter;
		readonly IStealthKiteOrders orders;
		readonly StealthBehaviorExecutionLease executionLease = new StealthBehaviorExecutionLease();
		OwnerState state = new OwnerState();

		public StealthKiteBehavior(StealthKiteHandoff handoff,
			IStealthLifecycleOwnershipGuard ownershipGuard, IStealthKiteLiveWorld liveWorld,
			IStealthKiteThreatAdapter threatAdapter, IStealthKiteOrders orders)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.Kite)
				throw new ArgumentException("Kite requires Kite ownership.", nameof(handoff));
			mission = handoff.Mission ?? throw new ArgumentException(
				"Kite requires one immutable mission.", nameof(handoff));
			this.ownershipGuard = ownershipGuard ?? throw new ArgumentNullException(nameof(ownershipGuard));
			this.liveWorld = liveWorld ?? throw new ArgumentNullException(nameof(liveWorld));
			this.threatAdapter = threatAdapter ?? throw new ArgumentNullException(nameof(threatAdapter));
			this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
		}

		public StealthKiteResult Execute()
		{
			var revision = executionLease.Acquire("Kite", EnsureActiveOwnership);
			try { return Execute(revision); }
			finally { executionLease.Release(revision); }
		}

		StealthKiteResult Execute(long revision)
		{
			var live = ReadLive("execute", revision);
			var decision = StealthKiteLiveDecision.Create(live);
			var prospective = state.Clone();
			if (live.Tick < prospective.LastObservedTick)
				throw new InvalidOperationException("Live Kite ticks must not move backwards.");
			prospective.LastObservedTick = live.Tick;
			prospective.DefenderIds = decision.DefenderActorIds.ToArray();
			prospective.ObjectiveIds = decision.ObjectiveActorIds.ToArray();
			if (decision.Defenders.Length == 0)
			{
				ClearTarget(prospective);
				prospective.Disposition = decision.TargetlessDisposition.Value;
				return CommitAndResult(prospective, decision, revision);
			}

			if (decision.Members.Length == 0)
			{
				ClearTarget(prospective);
				prospective.Fingerprint = decision.Fingerprint(null);
				prospective.FallbackEvidence = StealthKitePlanBuilder.NoLiveMembers(
					decision, prospective.Fingerprint);
				prospective.Disposition = StealthKiteDisposition.RecalculateFlee;
				return CommitAndResult(prospective, decision, revision);
			}

			var target = decision.ResolveTarget(prospective.TargetId);
			var targetIdentityChanged = prospective.TargetId != target.ActorId;
			var fingerprint = decision.Fingerprint(target);
			var fingerprintChanged = !fingerprint.Equals(prospective.Fingerprint);
			var completedFire = prospective.Phase == StealthKitePhase.Fire &&
				!targetIdentityChanged && StealthKitePhaseMachine.FireCompleted(decision, target,
					prospective.FireBaselineTargetHitPoints, prospective.LastOrderToken);
			SetTarget(prospective, target);
			if (decision.CanReturnToCrush(target))
			{
				ClearPlan(prospective);
				prospective.Fingerprint = fingerprint;
				prospective.LastPlanTick = -1;
				prospective.Disposition = StealthKiteDisposition.CrushEvaluation;
				return CommitAndResult(prospective, decision, revision);
			}

			if (fingerprintChanged)
			{
				prospective.Plan = StealthKitePlanBuilder.Build(decision, target,
					fingerprint, facts => CalculateSafety(facts, revision));
				prospective.Fingerprint = fingerprint;
				prospective.LastPlanTick = live.Tick;
			}

			if (prospective.Plan == null)
			{
				prospective.FallbackEvidence = StealthKitePlanBuilder.NoSafePlan(decision,
					target, fingerprint, facts => CalculateFallback(facts, revision));
				prospective.Disposition = prospective.FallbackEvidence.AttackScore.Value.Crossover > 2 ?
					StealthKiteDisposition.MassAttack : StealthKiteDisposition.RecalculateFlee;
				prospective.LastOrderToken = null;
				prospective.FireBaselineTargetHitPoints = -1;
				prospective.Phase = StealthKitePhase.Position;
				return CommitAndResult(prospective, decision, revision);
			}

			prospective.FallbackEvidence = null;
			var phase = StealthKitePhaseMachine.Advance(handoff, decision, target, prospective.Plan,
				prospective.Phase, prospective.FireBaselineTargetHitPoints,
				prospective.LastOrderToken, fingerprintChanged && !completedFire &&
					prospective.Phase != StealthKitePhase.Withdraw);
			if (phase.ShouldApplyOrder)
				ApplyOrder(phase.DesiredOrder, revision);
			prospective.Phase = phase.Phase;
			prospective.FireBaselineTargetHitPoints = phase.FireBaselineTargetHitPoints;
			prospective.LastOrderToken = phase.DesiredOrder;
			prospective.Disposition = StealthKiteDisposition.Retain;
			return CommitAndResult(prospective, decision, revision);
		}

		public MiniYamlNode SerializePrivateState(string key = "Kite")
		{
			return StealthKitePersistence.Serialize(key, handoff, mission, ToPrivateState(state));
		}

		public void RestorePrivateState(MiniYamlNode node)
		{
			var revision = executionLease.Acquire("Kite", EnsureActiveOwnership);
			try
			{
				var restored = StealthKitePersistence.Restore(node, handoff, mission);
				var live = ReadLive("restore", revision);
				ValidateRestored(restored, live, revision);
				var prospective = FromPrivateState(restored);
				executionLease.Commit(revision, "Kite", EnsureActiveOwnership,
					() => state = prospective);
			}
			finally { executionLease.Release(revision); }
		}

		void ValidateRestored(StealthKitePrivateState restored,
			StealthKiteLiveSnapshot live, long revision)
		{
			var decision = StealthKiteLiveDecision.Create(live);
			if (live.Tick < restored.LastObservedTick ||
				!restored.DefenderIds.SequenceEqual(decision.DefenderActorIds) ||
				!restored.ObjectiveIds.SequenceEqual(decision.ObjectiveActorIds))
				throw new InvalidOperationException("Saved Kite classification is not current live state.");
			if (decision.Defenders.Length == 0)
			{
				if (restored.TargetId.HasValue || restored.Disposition != decision.TargetlessDisposition.Value)
					throw new InvalidOperationException("Saved Kite targetless handoff has no live cause.");
				return;
			}

			if (decision.Members.Length == 0)
			{
				var fingerprint = decision.Fingerprint(null);
				if (restored.Disposition != StealthKiteDisposition.RecalculateFlee ||
					restored.FallbackEvidence?.Reason != StealthKiteFallbackReason.NoLiveMembers ||
					restored.Fingerprint == null || !restored.Fingerprint.Equals(fingerprint) ||
					restored.FallbackEvidence.LiveFingerprint != fingerprint.Canonical)
					throw new InvalidOperationException("Saved zero-member fallback has no current live cause.");
				return;
			}

			var target = decision.ResolveTarget(restored.TargetId);
			if (!restored.TargetId.HasValue || restored.TargetId != target.ActorId ||
				restored.TargetCell != target.CurrentCell || restored.TargetHitPoints != target.HitPoints ||
				restored.TargetMaximumHitPoints != target.MaximumHitPoints)
				throw new InvalidOperationException("Saved Kite target is not current live state.");
			var currentFingerprint = decision.Fingerprint(target);
			if (restored.Fingerprint == null || !restored.Fingerprint.Equals(currentFingerprint))
				throw new InvalidOperationException("Saved Kite safety fingerprint is stale.");
			if (restored.Disposition == StealthKiteDisposition.CrushEvaluation)
			{
				if (!decision.CanReturnToCrush(target))
					throw new InvalidOperationException("Saved Kite-to-Crush handoff has no live cause.");
				return;
			}

			var currentPlan = StealthKitePlanBuilder.Build(decision, target, currentFingerprint,
				facts => CalculateSafety(facts, revision));
			if (currentPlan == null)
			{
				var evidence = StealthKitePlanBuilder.NoSafePlan(decision, target, currentFingerprint,
					facts => CalculateFallback(facts, revision));
				if (!StealthKitePersistence.SameFallback(restored.FallbackEvidence, evidence) ||
					restored.Disposition != (evidence.AttackScore.Value.Crossover > 2 ?
						StealthKiteDisposition.MassAttack : StealthKiteDisposition.RecalculateFlee))
					throw new InvalidOperationException("Saved Kite fallback does not match live standard safety.");
				return;
			}

			if (restored.Disposition != StealthKiteDisposition.Retain ||
				!StealthKitePersistence.SamePlan(restored.Plan, currentPlan))
				throw new InvalidOperationException("Saved Kite plan does not match live standard safety.");
			StealthKitePhaseMachine.ValidateSaved(decision, target, currentPlan, restored.Phase,
				restored.FireBaselineTargetHitPoints, restored.LastOrderToken);
		}

		StealthKiteLiveSnapshot ReadLive(string operation, long revision)
		{
			executionLease.Verify(revision, "Kite", EnsureActiveOwnership);
			var live = liveWorld.Read(mission) ?? throw new InvalidOperationException(
				"The live Kite view returned no snapshot during " + operation + ".");
			executionLease.Verify(revision, "Kite", EnsureActiveOwnership);
			return live;
		}

		StealthKiteSafetyResult CalculateSafety(StealthKiteThreatFacts facts, long revision)
		{
			executionLease.Verify(revision, "Kite", EnsureActiveOwnership);
			var result = threatAdapter.Calculate(facts);
			executionLease.Verify(revision, "Kite", EnsureActiveOwnership);
			return result;
		}

		StealthTargetThreatScore CalculateFallback(StealthKiteFallbackFacts facts, long revision)
		{
			executionLease.Verify(revision, "Kite", EnsureActiveOwnership);
			var result = threatAdapter.CalculateAttackCrossover(facts);
			executionLease.Verify(revision, "Kite", EnsureActiveOwnership);
			return result;
		}

		void ApplyOrder(StealthKiteOrderToken token, long revision)
		{
			executionLease.Verify(revision, "Kite", EnsureActiveOwnership);
			if (token.Action == StealthKiteAction.Fire)
				orders.IssueAttack(handoff.Owner, handoff.Epoch, token.ActorIds,
					token.TargetActorId.Value, token.Cell, token);
			else
				orders.IssueMove(handoff.Owner, handoff.Epoch, token.ActorIds, token.Cell, token);
			executionLease.Verify(revision, "Kite", EnsureActiveOwnership);
		}

		StealthKiteResult CommitAndResult(OwnerState prospective,
			StealthKiteLiveDecision decision, long revision)
		{
			var result = new StealthKiteResult(handoff.Handoff, mission, prospective.Disposition,
				prospective.Phase, prospective.TargetId, prospective.TargetCell,
				prospective.Plan?.FireCell, prospective.Plan?.WithdrawCell,
				decision.Members.Select(member => member.ActorId), prospective.DefenderIds,
				prospective.ObjectiveIds, prospective.Plan?.FireSafety,
				prospective.FallbackEvidence);
			executionLease.Commit(revision, "Kite", EnsureActiveOwnership, () => state = prospective);
			return result;
		}

		static void SetTarget(OwnerState state, StealthKiteActorSnapshot target)
		{
			state.TargetId = target.ActorId;
			state.TargetCell = target.CurrentCell;
			state.TargetHitPoints = target.HitPoints;
			state.TargetMaximumHitPoints = target.MaximumHitPoints;
		}

		static void ClearPlan(OwnerState state)
		{
			state.Plan = null;
			state.FallbackEvidence = null;
			state.LastOrderToken = null;
			state.FireBaselineTargetHitPoints = -1;
			state.Phase = StealthKitePhase.Position;
		}

		static void ClearTarget(OwnerState state)
		{
			state.TargetId = null;
			state.TargetCell = null;
			state.TargetHitPoints = 0;
			state.TargetMaximumHitPoints = 0;
			state.Fingerprint = null;
			state.LastPlanTick = -1;
			ClearPlan(state);
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException("Stale Kite ownership cannot execute or restore state.");
		}

		static StealthKitePrivateState ToPrivateState(OwnerState state)
		{
			return new StealthKitePrivateState(state.LastObservedTick, state.LastPlanTick,
				state.Phase, state.Disposition, state.TargetId, state.TargetCell,
				state.TargetHitPoints, state.TargetMaximumHitPoints,
				state.FireBaselineTargetHitPoints, state.Fingerprint, state.Plan,
				state.FallbackEvidence, state.DefenderIds, state.ObjectiveIds, state.LastOrderToken);
		}

		static OwnerState FromPrivateState(StealthKitePrivateState state)
		{
			return new OwnerState
			{
				LastObservedTick = state.LastObservedTick,
				LastPlanTick = state.LastPlanTick,
				Phase = state.Phase,
				Disposition = state.Disposition,
				TargetId = state.TargetId,
				TargetCell = state.TargetCell,
				TargetHitPoints = state.TargetHitPoints,
				TargetMaximumHitPoints = state.TargetMaximumHitPoints,
				FireBaselineTargetHitPoints = state.FireBaselineTargetHitPoints,
				Fingerprint = state.Fingerprint,
				Plan = state.Plan,
				FallbackEvidence = state.FallbackEvidence,
				DefenderIds = state.DefenderIds.ToArray(),
				ObjectiveIds = state.ObjectiveIds.ToArray(),
				LastOrderToken = state.LastOrderToken
			};
		}
	}
}
