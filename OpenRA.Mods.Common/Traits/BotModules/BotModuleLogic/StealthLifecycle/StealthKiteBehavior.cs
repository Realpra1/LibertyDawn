#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Reactive live Kite owner: fire safely now, move to one safe cell, or hand off.</summary>
	public sealed class StealthKiteBehavior
	{
		readonly StealthKiteHandoff handoff;
		readonly StealthApproachMission mission;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthKiteLiveWorld liveWorld;
		readonly IStealthKiteThreatAdapter threatAdapter;
		readonly IStealthKiteOrders orders;
		readonly StealthBehaviorExecutionLease executionLease = new StealthBehaviorExecutionLease();
		uint? targetId;
		StealthKiteOrderToken lastOrder;

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
			var decision = StealthKiteLiveDecision.Create(ReadLive(revision));
			if (decision.Defenders.Length == 0)
				return Result(decision, decision.TargetlessDisposition.Value,
					StealthKitePhase.Position, null, null, null, null, revision);

			if (decision.Members.Length == 0)
			{
				var empty = new StealthKiteFallbackEvidence(StealthKiteFallbackReason.NoLiveMembers,
					"no-live-members", decision.DefenderActorIds, null, null);
				return Result(decision, StealthKiteDisposition.RecalculateFlee,
					StealthKitePhase.Position, null, null, null, empty, revision);
			}

			var target = decision.ResolveTarget(targetId);
			var currentCell = decision.CurrentFormationCell();
			if (currentCell.HasValue)
			{
				var liveSafety = decision.MemberCells.Select(cell =>
					Calculate(decision.ThreatFacts(target, cell), revision)).ToArray();
				if (liveSafety.All(safety => safety.Approved))
					return Retain(decision, target, currentCell.Value, liveSafety[0],
						StealthKiteAction.Fire, revision);
				if (decision.FormationExposed)
					return FleeUnsafeCurrentPosition(decision, target, revision);
			}

			foreach (var cell in decision.OrderedCandidateCells(target, currentCell))
			{
				var safety = Calculate(decision.ThreatFacts(target, cell), revision);
				if (safety.Approved)
					return Retain(decision, target, cell, safety, StealthKiteAction.Position, revision);
			}

			var fallbackFacts = decision.FallbackFacts(target);
			var score = CalculateFallback(fallbackFacts, revision);
			var fallback = new StealthKiteFallbackEvidence(StealthKiteFallbackReason.NoSafePlan,
				decision.LiveIdentity(target), decision.DefenderActorIds, fallbackFacts, score);
			var disposition = score.Crossover > 2 ?
				StealthKiteDisposition.MassAttack : StealthKiteDisposition.RecalculateFlee;
			return Result(decision, disposition, StealthKitePhase.Position,
				target, null, null, fallback, revision);
		}

		StealthKiteResult FleeUnsafeCurrentPosition(StealthKiteLiveDecision decision,
			StealthKiteActorSnapshot target, long revision)
		{
			var facts = decision.FallbackFacts(target);
			var score = CalculateFallback(facts, revision);
			var fallback = new StealthKiteFallbackEvidence(
				StealthKiteFallbackReason.UnsafeCurrentPosition,
				decision.LiveIdentity(target), decision.DefenderActorIds, facts, score);
			return Result(decision, StealthKiteDisposition.RecalculateFlee,
				StealthKitePhase.Position, target, null, null, fallback, revision);
		}

		StealthKiteResult Retain(StealthKiteLiveDecision decision,
			StealthKiteActorSnapshot target, CPos cell, StealthKiteSafetyResult safety,
			StealthKiteAction action, long revision)
		{
			var token = DesiredOrder(decision, target, cell, action);
			var shouldApply = !SameIntent(token, lastOrder) ||
				(action == StealthKiteAction.Position &&
					decision.Members.Any(member => member.NeedsMovementOrder));
			if (shouldApply)
				ApplyOrder(token, revision);
			return Result(decision, StealthKiteDisposition.Retain,
				action == StealthKiteAction.Fire ? StealthKitePhase.Fire : StealthKitePhase.Position,
				target, cell, safety, null, revision, token);
		}

		StealthKiteOrderToken DesiredOrder(StealthKiteLiveDecision decision,
			StealthKiteActorSnapshot target, CPos cell, StealthKiteAction action)
		{
			var targetActorId = action == StealthKiteAction.Fire ? target.ActorId : (uint?)null;
			var comparable = new StealthKiteOrderToken(handoff.Owner, handoff.Epoch, action,
				decision.Members.Select(member => member.ActorId), targetActorId,
				action == StealthKiteAction.Fire ? target.CurrentCell : cell,
				lastOrder?.PhaseRevision ?? 0, 0);
			if (action == StealthKiteAction.Fire && SameIntent(comparable, lastOrder))
				return comparable;
			return new StealthKiteOrderToken(handoff.Owner, handoff.Epoch, action,
				comparable.ActorIds, targetActorId, comparable.Cell,
				(lastOrder?.PhaseRevision ?? -1) + 1, comparable.ActivityRevision);
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

		StealthKiteResult Result(StealthKiteLiveDecision decision,
			StealthKiteDisposition disposition, StealthKitePhase phase,
			StealthKiteActorSnapshot target, CPos? fireCell, StealthKiteSafetyResult? safety,
			StealthKiteFallbackEvidence fallback, long revision, StealthKiteOrderToken order = null)
		{
			var result = new StealthKiteResult(handoff.Handoff, mission, disposition, phase,
				target?.ActorId, target?.CurrentCell, fireCell,
				decision.Members.Select(member => member.ActorId), decision.DefenderActorIds,
				decision.ObjectiveActorIds, safety, fallback);
			executionLease.Commit(revision, "Kite", EnsureActiveOwnership, () =>
			{
				targetId = target?.ActorId;
				lastOrder = order;
			});
			return result;
		}

		StealthKiteLiveSnapshot ReadLive(long revision)
		{
			executionLease.Verify(revision, "Kite", EnsureActiveOwnership);
			var live = liveWorld.Read(mission) ??
				throw new InvalidOperationException("The live Kite view returned no snapshot.");
			executionLease.Verify(revision, "Kite", EnsureActiveOwnership);
			return live;
		}

		StealthKiteSafetyResult Calculate(StealthKiteThreatFacts facts, long revision)
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

		static bool SameIntent(StealthKiteOrderToken left, StealthKiteOrderToken right)
		{
			return left != null && right != null && left.Owner == right.Owner &&
				left.Epoch == right.Epoch && left.Action == right.Action &&
				left.TargetActorId == right.TargetActorId && left.Cell == right.Cell &&
				left.ActorIds.SequenceEqual(right.ActorIds);
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException("Stale Kite ownership cannot execute.");
		}
	}
}
