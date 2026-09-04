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
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Reactive live Kite owner: fire safely now, move to one safe cell, or hand off.</summary>
	public sealed class StealthKiteBehavior
	{
		const int MaximumPositionAttempts = 8;
		readonly StealthKiteHandoff handoff;
		readonly StealthApproachMission mission;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthKiteLiveWorld liveWorld;
		readonly IStealthKiteThreatAdapter threatAdapter;
		readonly IStealthKiteOrders orders;
		readonly Func<CPos, bool> isCoordinatedMassAttackTarget;
		readonly StealthBehaviorExecutionLease executionLease = new StealthBehaviorExecutionLease();
		readonly HashSet<CPos> unreachableCells = new HashSet<CPos>();
		uint? targetId;
		bool targetWasKiteTarget;
		bool targetWasBlockingObjective;
		StealthKiteOrderToken lastOrder;
		int positionAttempts;

		public StealthKiteBehavior(StealthKiteHandoff handoff,
			IStealthLifecycleOwnershipGuard ownershipGuard, IStealthKiteLiveWorld liveWorld,
			IStealthKiteThreatAdapter threatAdapter, IStealthKiteOrders orders,
			Func<CPos, bool> isCoordinatedMassAttackTarget = null)
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
			this.isCoordinatedMassAttackTarget = isCoordinatedMassAttackTarget ?? (_ => false);
		}

		public StealthKiteResult Execute()
		{
			var revision = executionLease.Acquire("Kite", EnsureActiveOwnership);
			try { return Execute(revision); }
			finally { executionLease.Release(revision); }
		}

		StealthKiteResult Execute(long revision)
		{
			var decision = StealthKiteLiveDecision.Create(
				ReadLive(revision), handoff.RequiredKiteActorId, targetId);
			var retainBlockingObjective = targetWasBlockingObjective;
			var retainKiteTarget = targetWasKiteTarget;
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

			if (decision.TargetlessDisposition == StealthKiteDisposition.CrushEvaluation)
			{
				return Result(decision, StealthKiteDisposition.CrushEvaluation,
					StealthKitePhase.Position, null, null, null, null, revision);
			}

			if (decision.TargetlessDisposition == StealthKiteDisposition.Reacquire)
			{
				return Result(decision, StealthKiteDisposition.Reacquire,
					StealthKitePhase.Position, null, null, null, null, revision);
			}

			if (retainBlockingObjective && targetId.HasValue && decision.Actor(targetId.Value) == null)
			{
				if (decision.FormationHidden)
					return Result(decision, StealthKiteDisposition.Reacquire,
						StealthKitePhase.Position, null, null, null, null, revision);
				retainBlockingObjective = false;
			}
			else if (retainBlockingObjective && targetId.HasValue)
			{
				var blocker = decision.Actor(targetId.Value);
				var closestLiveTarget = decision.OrderedTargets(null).FirstOrDefault();
				if (decision.IsCloserToFormation(closestLiveTarget, blocker))
					retainBlockingObjective = false;
			}

			if (retainKiteTarget && targetId.HasValue &&
				decision.KiteTargets.All(actor => actor.ActorId != targetId.Value))
			{
				if (decision.CrushableInfantry.Any(actor => actor.ActorId == targetId.Value))
					return Result(decision, StealthKiteDisposition.CrushEvaluation,
						StealthKitePhase.Position, null, null, null, null, revision);
				retainKiteTarget = false;
				if (decision.KiteTargets.Length == 0 && decision.FormationHidden)
					return Result(decision, StealthKiteDisposition.Reacquire,
						StealthKitePhase.Position, null, null, null, null, revision);
			}

			var targets = retainBlockingObjective && targetId.HasValue ?
				new[] { decision.Actor(targetId.Value) } : retainKiteTarget && targetId.HasValue ?
				new[] { decision.ResolveTarget(targetId) } : decision.OrderedTargets(null);
			var target = targets[0];
			if (isCoordinatedMassAttackTarget(target.CurrentCell))
				return NoSafeResult(decision, target, revision);

			var currentCell = decision.CurrentFormationCell();
			if (decision.KitingEnabled && currentCell.HasValue &&
				!decision.FormationHidden && !decision.CurrentPositionSafe)
				return FleeUnsafeCurrentPosition(decision, decision.ClosestDefender(), revision);

			if (decision.KitingEnabled && target.ActorId == targetId &&
				lastOrder?.Action == StealthKiteAction.Position)
			{
				var currentSafety = Calculate(decision.CurrentThreatFacts(target), revision);
				if (currentSafety.Approved)
					return Retain(decision, target, decision.RepresentativeCell(target),
						currentSafety, StealthKiteAction.Fire, revision);
				var plannedSafety = Calculate(decision.PlannedThreatFacts(target, lastOrder.Cell), revision);
				if (plannedSafety.Approved && decision.Members.Any(member => !member.NeedsMovementOrder))
					return RetainMoving(decision, target, plannedSafety, revision);
			}

			if (lastOrder?.Action == StealthKiteAction.Position && currentCell.HasValue &&
				decision.Members.All(member => member.NeedsMovementOrder) &&
				currentCell.Value != lastOrder.Cell)
				unreachableCells.Add(lastOrder.Cell);

			if (decision.KitingEnabled)
				foreach (var candidate in targets)
					if (TrySafeAction(decision, candidate, currentCell, revision,
						out var cell, out var safety, out var action))
						return Retain(decision, candidate, cell, safety, action, revision);

			if (decision.CrushableInfantry.Length != 0 && !handoff.RequiredKiteActorId.HasValue)
				return Result(decision, StealthKiteDisposition.CrushEvaluation,
					StealthKitePhase.Position, null, null, null, null, revision);

			foreach (var fallbackTarget in decision.OrderedFallbackObjectives())
				if (TrySafeAction(decision, fallbackTarget, currentCell, revision,
					out var fallbackCell, out var fallbackSafety, out var fallbackAction))
					return Retain(decision, fallbackTarget, fallbackCell,
						fallbackSafety, fallbackAction, revision);

			return NoSafeResult(decision, target, revision);
		}

		StealthKiteResult NoSafeResult(StealthKiteLiveDecision decision,
			StealthKiteActorSnapshot target, long revision)
		{
			var coordinatedMassAttack = isCoordinatedMassAttackTarget(target.CurrentCell);
			var attackPackage = decision.AttackPackage(target)
				.Where(actor => actor.IsDefender).ToArray();
			if (attackPackage.Length == 0)
			{
				target = decision.ClosestDefender();
				attackPackage = decision.AttackPackage(target)
					.Where(actor => actor.IsDefender).ToArray();
			}

			var fallbackFacts = decision.FallbackFacts(target);
			var score = CalculateFallback(fallbackFacts, revision);
			var fallback = new StealthKiteFallbackEvidence(StealthKiteFallbackReason.NoSafePlan,
				decision.LiveIdentity(target), attackPackage.Select(actor => actor.ActorId), fallbackFacts, score,
				coordinatedMassAttack);
			var disposition = score.Crossover > 2 || coordinatedMassAttack ?
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
				decision.LiveIdentity(target), decision.AttackPackage(target)
					.Where(actor => actor.IsDefender).Select(actor => actor.ActorId), facts, score);
			return Result(decision, StealthKiteDisposition.RecalculateFlee,
				StealthKitePhase.Position, target, null, null, fallback, revision);
		}

		bool TrySafeAction(StealthKiteLiveDecision decision,
			StealthKiteActorSnapshot target, CPos? currentCell, long revision,
			out CPos cell, out StealthKiteSafetyResult safety, out StealthKiteAction action)
		{
			if (currentCell.HasValue)
			{
				var liveSafety = Calculate(decision.CurrentThreatFacts(target), revision);
				if (liveSafety.Approved)
				{
					cell = decision.RepresentativeCell(target);
					safety = liveSafety;
					action = StealthKiteAction.Fire;
					return true;
				}
			}

			foreach (var candidate in decision.OrderedCandidateCells(target, currentCell)
				.Where(candidateCell => !unreachableCells.Contains(candidateCell)))
			{
				if (positionAttempts >= MaximumPositionAttempts)
					break;
				var candidateSafety = Calculate(decision.PlannedThreatFacts(target, candidate), revision);
				if (!candidateSafety.Approved)
					continue;
				if (!liveWorld.CanReach(target.ActorId, candidate))
				{
					unreachableCells.Add(candidate);
					continue;
				}

				cell = candidate;
				safety = candidateSafety;
				action = StealthKiteAction.Position;
				return true;
			}

			cell = default;
			safety = default;
			action = default;
			return false;
		}

		StealthKiteResult Retain(StealthKiteLiveDecision decision,
			StealthKiteActorSnapshot target, CPos cell, StealthKiteSafetyResult safety,
			StealthKiteAction action, long revision)
		{
			var blockingObjective = targetWasBlockingObjective && target.ActorId == targetId;
			if (action == StealthKiteAction.Fire)
			{
				var blockerId = liveWorld.BlockingActor(target.ActorId, cell);
				var blocker = blockerId.HasValue ? decision.Actor(blockerId.Value) : null;
				if (blocker != null)
				{
					target = blocker;
					safety = Calculate(decision.PlannedThreatFacts(target, cell), revision);
					blockingObjective = true;
				}
			}

			var retryCompletedAttack = action == StealthKiteAction.Fire &&
				lastOrder?.Action == StealthKiteAction.Fire &&
				lastOrder.TargetActorId == target.ActorId &&
				decision.Members.All(member => member.NeedsMovementOrder);
			var token = DesiredOrder(decision, target, cell, action, retryCompletedAttack);
			var shouldApply = retryCompletedAttack || !SameIntent(token, lastOrder) ||
				(action == StealthKiteAction.Position &&
					decision.Members.All(member => member.NeedsMovementOrder));
			if (shouldApply)
				ApplyOrder(token, revision);

			return Result(decision, StealthKiteDisposition.Retain,
				action == StealthKiteAction.Fire ? StealthKitePhase.Fire : StealthKitePhase.Position,
				target, cell, safety, null, revision, token, blockingObjective);
		}

		StealthKiteResult RetainMoving(StealthKiteLiveDecision decision,
			StealthKiteActorSnapshot target, StealthKiteSafetyResult safety, long revision)
		{
			return Result(decision, StealthKiteDisposition.Retain,
				StealthKitePhase.Position, target, lastOrder.Cell, safety, null, revision, lastOrder);
		}

		StealthKiteOrderToken DesiredOrder(StealthKiteLiveDecision decision,
			StealthKiteActorSnapshot target, CPos cell, StealthKiteAction action,
			bool retryCompletedAttack)
		{
			var targetActorId = action == StealthKiteAction.Fire ? target.ActorId : (uint?)null;
			var comparable = new StealthKiteOrderToken(handoff.Owner, handoff.Epoch, action,
				decision.Members.Select(member => member.ActorId), targetActorId,
				action == StealthKiteAction.Fire ? target.CurrentCell : cell,
				lastOrder?.PhaseRevision ?? 0, 0);
			if (action == StealthKiteAction.Fire && !retryCompletedAttack &&
				SameIntent(comparable, lastOrder))
				return comparable;
			return new StealthKiteOrderToken(handoff.Owner, handoff.Epoch, action,
				comparable.ActorIds, targetActorId, comparable.Cell,
				(lastOrder?.PhaseRevision ?? -1) + 1, comparable.ActivityRevision);
		}

		void ApplyOrder(StealthKiteOrderToken token, long revision)
		{
			executionLease.Verify(revision, "Kite", EnsureActiveOwnership);
			if (token.Action == StealthKiteAction.Fire)
			{
				orders.IssueAttack(handoff.Owner, handoff.Epoch, token.ActorIds,
					token.TargetActorId.Value, token.Cell, token);
				positionAttempts = 0;
			}
			else
			{
				orders.IssueMove(handoff.Owner, handoff.Epoch, token.ActorIds, token.Cell, token);
				positionAttempts++;
			}

			executionLease.Verify(revision, "Kite", EnsureActiveOwnership);
		}

		StealthKiteResult Result(StealthKiteLiveDecision decision,
			StealthKiteDisposition disposition, StealthKitePhase phase,
			StealthKiteActorSnapshot target, CPos? fireCell, StealthKiteSafetyResult? safety,
			StealthKiteFallbackEvidence fallback, long revision, StealthKiteOrderToken order = null,
			bool blockingObjective = false)
		{
			var result = new StealthKiteResult(handoff.Handoff, mission, disposition, phase,
				target?.ActorId, target?.CurrentCell, fireCell,
				decision.Members.Select(member => member.ActorId), decision.DefenderActorIds,
				decision.ObjectiveActorIds, safety, fallback);
			executionLease.Commit(revision, "Kite", EnsureActiveOwnership, () =>
			{
				targetId = target?.ActorId;
				targetWasKiteTarget = target != null && decision.KiteTargets.Contains(target);
				targetWasBlockingObjective = target != null && blockingObjective;
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
				left.TargetActorId == right.TargetActorId &&
				(left.Action == StealthKiteAction.Fire || left.Cell == right.Cell) &&
				left.ActorIds.SequenceEqual(right.ActorIds);
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException("Stale Kite ownership cannot execute.");
		}
	}
}
