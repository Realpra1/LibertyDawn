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
	/// <summary>Reactive live MassAttack owner: attack the greatest threat while crossover is above one.</summary>
	public sealed class StealthMassAttackBehavior
	{
		const int MaximumSafeCellChecks = 8;
		const int OrderRetryIntervalTicks = 75;
		readonly StealthMassAttackHandoff handoff;
		readonly StealthApproachMission mission;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthMassAttackLiveWorld liveWorld;
		readonly IStealthMassAttackThreatAdapter threatAdapter;
		readonly IStealthMassAttackOrders orders;
		readonly StealthBehaviorExecutionLease executionLease = new StealthBehaviorExecutionLease();
		readonly HashSet<uint> unreachableObjectives = new HashSet<uint>();
		StealthMassAttackOrderToken lastOrder;
		uint? retainedDefenderActorId;
		int lastOrderTick = int.MinValue;
		long attemptRevision;

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
			var decision = StealthMassAttackLiveDecision.Create(ReadLive(revision));
			if (decision.TargetlessDisposition.HasValue)
				return Targetless(decision, decision.TargetlessDisposition.Value, revision);

			var currentCell = decision.CurrentFormationCell();
			var representativeCell = decision.RepresentativeCell();
			var retainedTarget = retainedDefenderActorId.HasValue ?
				decision.FindTarget(retainedDefenderActorId.Value) : null;
			var evaluation = BeginEvaluation(
				decision.Facts(retainedTarget ?? decision.Defenders[0], representativeCell), revision);
			var selected = retainedTarget != null ? Evaluate(retainedTarget) : decision.Defenders
				.Select(Evaluate).OrderByDescending(choice => choice.Threat.SelectedTargetThreat)
				.ThenBy(choice => DistanceSquared(currentCell, choice.Target.CurrentCell))
				.ThenBy(choice => choice.Target.ActorId).First();
			if (selected.Threat.StandardScore.Crossover <= 1 &&
				!handoff.Evidence.CoordinatedMassAttack)
				return Result(decision, StealthMassAttackDisposition.RecalculateFlee,
					StealthMassAttackPhase.Advance, selected.Target, selected.Facts,
					selected.Threat, null, revision);
			if (lastOrder?.Phase == StealthMassAttackPhase.Advance &&
				lastOrder.TargetActorId == selected.Target.ActorId &&
				lastOrder.ActorIds.SequenceEqual(decision.MemberActorIds) &&
				decision.Members.Any(member => !member.NeedsMovementOrder))
			{
				var retainedFacts = decision.Facts(selected.Target, lastOrder.OrderCell);
				var retainedThreat = Calculate(evaluation, retainedFacts, revision);
				if (retainedThreat.AttackApproved)
					return Result(decision, StealthMassAttackDisposition.Retain,
						StealthMassAttackPhase.Advance, selected.Target, retainedFacts,
						retainedThreat, lastOrder, revision);
			}

			var orderTarget = selected.Target;
			var retainedObjective = lastOrder == null ? null :
				decision.FindObjective(lastOrder.TargetActorId);
			if (retainedObjective != null && retainedObjective.ActorId != selected.Target.ActorId)
			{
				if (decision.Members.All(member => member.NeedsMovementOrder) &&
					(long)decision.Tick - lastOrderTick >= OrderRetryIntervalTicks)
					unreachableObjectives.Add(retainedObjective.ActorId);
				else
					orderTarget = retainedObjective;
			}

			var phase = StealthMassAttackPhase.Attack;
			var orderCell = orderTarget.CurrentCell;
			var selectedFacts = selected.Facts;
			var threat = selected.Threat;
			if (orderTarget.ActorId == selected.Target.ActorId && !selected.Threat.AttackApproved)
			{
				var safeCellFound = false;
				foreach (var candidate in decision.OrderedCandidateCells(selected.Target, currentCell)
					.Take(MaximumSafeCellChecks))
				{
					var candidateFacts = decision.Facts(selected.Target, candidate);
					var candidateThreat = Calculate(evaluation, candidateFacts, revision);
					if (!candidateThreat.AttackApproved)
						continue;
					phase = StealthMassAttackPhase.Advance;
					orderCell = candidate;
					selectedFacts = candidateFacts;
					threat = candidateThreat;
					safeCellFound = true;
					break;
				}

				// MassAttack is entered only after Kite proves that no safe local action exists
				// and crossover is above two. A safe firing cell is still preferable, but its
				// absence is the reason to commit the approved mass attack, not to flee again.
				if (!safeCellFound)
				{
					var stalled = lastOrder?.Phase == StealthMassAttackPhase.Attack &&
						lastOrder.TargetActorId == selected.Target.ActorId &&
						decision.Members.All(member => member.NeedsMovementOrder) &&
						(long)decision.Tick - lastOrderTick >= OrderRetryIntervalTicks;
					var liveBlockerId = stalled ?
						liveWorld.BlockingActor(selected.Target.ActorId, representativeCell) : null;
					var blocker = liveBlockerId.HasValue ?
						decision.FindObjective(liveBlockerId.Value) : stalled ?
						decision.OrderedObjectives(currentCell, selected.Target)
							.FirstOrDefault(actor => !unreachableObjectives.Contains(actor.ActorId)) : null;
					if (blocker != null)
					{
						orderTarget = blocker;
						orderCell = blocker.CurrentCell;
					}
				}
			}

			var sameIntent = SameIntent(lastOrder, phase,
				decision.MemberActorIds, orderTarget, orderCell);
			var shouldApply = !sameIntent || (decision.Members.All(member => member.NeedsMovementOrder) &&
				(long)decision.Tick - lastOrderTick >= OrderRetryIntervalTicks);
			if (shouldApply)
				attemptRevision++;
			var desired = new StealthMassAttackOrderToken(handoff.Owner, handoff.Epoch, phase, 0,
				attemptRevision, decision.MemberActorIds, orderTarget.ActorId, orderCell);
			if (shouldApply)
				ApplyOrder(desired, revision);
			return Result(decision, StealthMassAttackDisposition.Retain, phase,
				selected.Target, selectedFacts, threat, desired, revision);

			(StealthMassAttackActorSnapshot Target, StealthMassAttackThreatFacts Facts,
				StealthMassAttackThreatResult Threat) Evaluate(StealthMassAttackActorSnapshot target)
			{
				var facts = decision.Facts(target, representativeCell);
				return (target, facts, Calculate(evaluation, facts, revision));
			}
		}

		StealthMassAttackResult Targetless(StealthMassAttackLiveDecision decision,
			StealthMassAttackDisposition disposition, long revision)
		{
			return Result(decision, disposition, StealthMassAttackPhase.Advance,
				null, null, null, null, revision);
		}

		StealthMassAttackResult Result(StealthMassAttackLiveDecision decision,
			StealthMassAttackDisposition disposition, StealthMassAttackPhase phase,
			StealthMassAttackActorSnapshot target, StealthMassAttackThreatFacts facts,
			StealthMassAttackThreatResult? threat, StealthMassAttackOrderToken order, long revision)
		{
			var result = new StealthMassAttackResult(handoff, mission, disposition, phase,
				target?.ActorId, target?.CurrentCell, decision.MemberActorIds,
				decision.DefenderActorIds, decision.ObjectiveActorIds, facts, threat, order);
			executionLease.Commit(revision, "MassAttack", EnsureActiveOwnership, () =>
			{
				retainedDefenderActorId = disposition == StealthMassAttackDisposition.Retain ?
					target?.ActorId : null;
				if (order != null && (lastOrder == null ||
					lastOrder.AttemptRevision != order.AttemptRevision))
					lastOrderTick = decision.Tick;
				lastOrder = order;
			});
			return result;
		}

		StealthMassAttackLiveSnapshot ReadLive(long revision)
		{
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
			var live = liveWorld.Read(mission, handoff.Evidence.SelectedTargetCurrentCell) ??
				throw new InvalidOperationException("The live MassAttack view returned no snapshot.");
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
			return live;
		}

		IStealthMassAttackThreatEvaluation BeginEvaluation(
			StealthMassAttackThreatFacts facts, long revision)
		{
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
			var evaluation = threatAdapter.Begin(facts) ??
				throw new InvalidOperationException("MassAttack threat adapter returned no live evaluation.");
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
			return evaluation;
		}

		StealthMassAttackThreatResult Calculate(IStealthMassAttackThreatEvaluation evaluation,
			StealthMassAttackThreatFacts facts, long revision)
		{
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
			var result = evaluation.Calculate(facts);
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
			return result;
		}

		void ApplyOrder(StealthMassAttackOrderToken token, long revision)
		{
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
			if (token.Phase == StealthMassAttackPhase.Attack)
				orders.IssueAttack(handoff.Owner, handoff.Epoch, token.ActorIds,
					token.TargetActorId, token.OrderCell, token);
			else
				orders.IssueMove(handoff.Owner, handoff.Epoch, token.ActorIds,
					token.TargetActorId, token.OrderCell, token);
			executionLease.Verify(revision, "MassAttack", EnsureActiveOwnership);
		}

		static bool SameIntent(StealthMassAttackOrderToken order,
			StealthMassAttackPhase phase, uint[] members, StealthMassAttackActorSnapshot target,
			CPos orderCell)
		{
			return order != null && order.Phase == phase && order.TargetActorId == target.ActorId &&
				(phase == StealthMassAttackPhase.Attack || order.OrderCell == orderCell) &&
				order.ActorIds.SequenceEqual(members);
		}

		static long DistanceSquared(CPos left, CPos right)
		{
			var dx = (long)left.X - right.X;
			var dy = (long)left.Y - right.Y;
			return dx * dx + dy * dy;
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException("Stale MassAttack ownership cannot execute.");
		}
	}
}
