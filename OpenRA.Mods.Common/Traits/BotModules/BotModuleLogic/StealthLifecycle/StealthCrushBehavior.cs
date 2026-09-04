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
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Reactive live Crush owner: crush one safe live infantry target or hand off to Kite.</summary>
	public sealed class StealthCrushBehavior
	{
		readonly StealthCrushEvaluationHandoff handoff;
		readonly StealthApproachMission mission;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthCrushLiveWorld liveWorld;
		readonly IStealthCrushThreatAdapter threatAdapter;
		readonly IStealthCrushOrders orders;
		readonly StealthBehaviorExecutionLease executionLease = new StealthBehaviorExecutionLease();
		uint? targetId;
		CPos? targetCell;
		uint[] orderedMembers = Array.Empty<uint>();
		long attemptRevision;

		public StealthCrushBehavior(StealthCrushEvaluationHandoff handoff,
			IStealthLifecycleOwnershipGuard ownershipGuard, IStealthCrushLiveWorld liveWorld,
			IStealthCrushThreatAdapter threatAdapter, IStealthCrushOrders orders)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.CrushEvaluation)
				throw new ArgumentException("Crush requires Crush ownership.", nameof(handoff));
			mission = handoff.Mission ?? throw new ArgumentException(
				"Crush requires one immutable mission.", nameof(handoff));
			this.ownershipGuard = ownershipGuard ?? throw new ArgumentNullException(nameof(ownershipGuard));
			this.liveWorld = liveWorld ?? throw new ArgumentNullException(nameof(liveWorld));
			this.threatAdapter = threatAdapter ?? throw new ArgumentNullException(nameof(threatAdapter));
			this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
		}

		public StealthCrushResult Execute()
		{
			var revision = executionLease.Acquire("Crush", EnsureActiveOwnership);
			try { return Execute(revision); }
			finally { executionLease.Release(revision); }
		}

		StealthCrushResult Execute(long revision)
		{
			var decision = StealthCrushLiveDecision.Create(ReadLive(revision), mission);
			if (decision.TargetlessDisposition.HasValue)
			{
				var fallbackTarget = decision.TargetlessDisposition == StealthCrushDisposition.Kite ?
					decision.SelectFallbackKiteTarget() : null;
				return Result(decision, decision.TargetlessDisposition.Value,
					fallbackTarget, null, revision);
			}

			var target = decision.SelectTarget(targetId);
			var safety = Calculate(decision.ThreatFacts(target), revision);
			if (!safety.Approved)
				return Result(decision, StealthCrushDisposition.Kite, target, safety, revision);

			var members = decision.Members.Select(member => member.ActorId).ToArray();
			var sameAttempt = targetId == target.ActorId && targetCell == target.CurrentCell &&
				orderedMembers.SequenceEqual(members);
			if (sameAttempt && decision.Members.All(member => member.NeedsMovementOrder))
				return Result(decision, StealthCrushDisposition.Kite, target, null, revision);
			var needsOrder = targetId != target.ActorId || targetCell != target.CurrentCell ||
				!orderedMembers.SequenceEqual(members) ||
				decision.Members.All(member => member.NeedsMovementOrder);
			if (needsOrder)
			{
				executionLease.Verify(revision, "Crush", EnsureActiveOwnership);
				attemptRevision++;
				orders.IssueCrush(handoff.Owner, handoff.Epoch, members,
					target.ActorId, target.CurrentCell, attemptRevision);
				executionLease.Verify(revision, "Crush", EnsureActiveOwnership);
			}

			return Result(decision, StealthCrushDisposition.Retain, target, safety, revision);
		}

		StealthCrushResult Result(StealthCrushLiveDecision decision,
			StealthCrushDisposition disposition, StealthCrushActorSnapshot target,
			StealthCrushSafetyResult? safety, long revision)
		{
			var members = decision.Members.Select(member => member.ActorId).ToArray();
			var result = new StealthCrushResult(handoff.Handoff, mission, disposition,
				target?.ActorId, target?.CurrentCell, members,
				decision.DefenderActorIds, decision.ObjectiveActorIds, safety);
			executionLease.Commit(revision, "Crush", EnsureActiveOwnership,
				() =>
				{
					targetId = disposition == StealthCrushDisposition.Retain ? target?.ActorId : null;
					targetCell = disposition == StealthCrushDisposition.Retain ? target?.CurrentCell : null;
					orderedMembers = disposition == StealthCrushDisposition.Retain ? members : Array.Empty<uint>();
				});
			return result;
		}

		StealthCrushLiveSnapshot ReadLive(long revision)
		{
			executionLease.Verify(revision, "Crush", EnsureActiveOwnership);
			var live = liveWorld.Read(mission) ??
				throw new InvalidOperationException("The live Crush view returned no snapshot.");
			executionLease.Verify(revision, "Crush", EnsureActiveOwnership);
			return live;
		}

		StealthCrushSafetyResult Calculate(StealthCrushThreatFacts facts, long revision)
		{
			executionLease.Verify(revision, "Crush", EnsureActiveOwnership);
			var result = threatAdapter.Calculate(facts);
			executionLease.Verify(revision, "Crush", EnsureActiveOwnership);
			return result;
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException("Stale Crush ownership cannot execute.");
		}
	}
}
