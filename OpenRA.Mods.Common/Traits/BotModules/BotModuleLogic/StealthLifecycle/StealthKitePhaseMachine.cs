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
	sealed class StealthKitePhaseDecision
	{
		public StealthKitePhase Phase { get; }
		public int FireBaselineTargetHitPoints { get; }
		public StealthKiteOrderToken DesiredOrder { get; }
		public bool ShouldApplyOrder { get; }

		public StealthKitePhaseDecision(StealthKitePhase phase, int fireBaselineTargetHitPoints,
			StealthKiteOrderToken desiredOrder, bool shouldApplyOrder)
		{
			Phase = phase;
			FireBaselineTargetHitPoints = fireBaselineTargetHitPoints;
			DesiredOrder = desiredOrder ?? throw new ArgumentNullException(nameof(desiredOrder));
			ShouldApplyOrder = shouldApplyOrder;
		}
	}

	static class StealthKitePhaseMachine
	{
		public static bool FireCompleted(StealthKiteLiveDecision decision,
			StealthKiteActorSnapshot target, int baselineHitPoints,
			StealthKiteOrderToken lastOrder)
		{
			return (baselineHitPoints >= 0 && target.HitPoints < baselineHitPoints) ||
				(lastOrder != null && lastOrder.Action == StealthKiteAction.Fire &&
					lastOrder.Equals(decision.CompletedOrderToken));
		}

		public static StealthKitePhaseDecision Advance(StealthKiteHandoff handoff,
			StealthKiteLiveDecision decision, StealthKiteActorSnapshot target, StealthKitePlan plan,
			StealthKitePhase phase, int fireBaselineTargetHitPoints,
			StealthKiteOrderToken lastOrder, bool forcePosition)
		{
			if (forcePosition)
				phase = StealthKitePhase.Position;
			StealthKiteAction action;
			CPos cell;
			uint? targetId = null;
			if (phase == StealthKitePhase.Position && decision.MembersAt(plan.FireCell))
			{
				phase = StealthKitePhase.Fire;
				fireBaselineTargetHitPoints = target.HitPoints;
				action = StealthKiteAction.Fire;
				targetId = target.ActorId;
				cell = target.CurrentCell;
			}
			else if (phase == StealthKitePhase.Position)
			{
				fireBaselineTargetHitPoints = -1;
				action = StealthKiteAction.Position;
				cell = plan.FireCell;
			}
			else if (phase == StealthKitePhase.Fire && FireCompleted(
				decision, target, fireBaselineTargetHitPoints, lastOrder))
			{
				phase = StealthKitePhase.Withdraw;
				action = StealthKiteAction.Withdraw;
				cell = plan.WithdrawCell;
			}
			else if (phase == StealthKitePhase.Fire)
			{
				action = StealthKiteAction.Fire;
				targetId = target.ActorId;
				cell = target.CurrentCell;
			}
			else if (decision.MembersAt(plan.WithdrawCell))
			{
				phase = StealthKitePhase.Position;
				fireBaselineTargetHitPoints = -1;
				action = StealthKiteAction.Position;
				cell = plan.FireCell;
			}
			else
			{
				action = StealthKiteAction.Withdraw;
				cell = plan.WithdrawCell;
			}

			var activityRevision = decision.HasActivityObservation ? decision.ActivityRevision : 0;
			var phaseRevision = lastOrder == null ? 0 : lastOrder.PhaseRevision +
				(lastOrder.Action == action ? 0 : 1);
			var desired = new StealthKiteOrderToken(handoff.Owner, handoff.Epoch, action,
				decision.Members.Select(member => member.ActorId), targetId, cell,
				phaseRevision, activityRevision);
			var shouldApply = !desired.Equals(lastOrder) || (decision.HasActivityObservation &&
				!desired.Equals(decision.ActiveOrderToken));
			return new StealthKitePhaseDecision(phase, fireBaselineTargetHitPoints,
				desired, shouldApply);
		}

		public static void ValidateSaved(StealthKiteLiveDecision decision,
			StealthKiteActorSnapshot target, StealthKitePlan plan, StealthKitePhase phase,
			int fireBaselineTargetHitPoints, StealthKiteOrderToken order)
		{
			if (order == null || !order.ActorIds.SequenceEqual(
				decision.Members.Select(member => member.ActorId)) ||
				order.ActivityRevision != decision.ActivityRevision)
				throw new InvalidOperationException("Saved Kite order token is not current live activity.");
			if (phase == StealthKitePhase.Position && (decision.MembersAt(plan.FireCell) ||
				fireBaselineTargetHitPoints != -1 || order.Action != StealthKiteAction.Position ||
				order.TargetActorId.HasValue || order.Cell != plan.FireCell))
				throw new InvalidOperationException("Saved Kite Position phase is inconsistent.");
			if (phase == StealthKitePhase.Fire && (!decision.MembersAt(plan.FireCell) ||
				fireBaselineTargetHitPoints != target.HitPoints ||
				FireCompleted(decision, target, fireBaselineTargetHitPoints, order) ||
				order.Action != StealthKiteAction.Fire || order.TargetActorId != target.ActorId ||
				order.Cell != target.CurrentCell))
				throw new InvalidOperationException("Saved Kite Fire phase is inconsistent.");
			if (phase == StealthKitePhase.Withdraw && (decision.MembersAt(plan.WithdrawCell) ||
				fireBaselineTargetHitPoints < 0 || order.Action != StealthKiteAction.Withdraw ||
				order.TargetActorId.HasValue || order.Cell != plan.WithdrawCell))
				throw new InvalidOperationException("Saved Kite Withdraw phase is inconsistent.");
		}
	}
}
