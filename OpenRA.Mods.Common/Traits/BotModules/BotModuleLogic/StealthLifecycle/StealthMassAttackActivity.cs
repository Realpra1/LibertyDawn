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
	sealed class StealthMassAttackActivityContext
	{
		enum PairRelation { Lost, Prior, Current }

		public bool HasObservation { get; }
		public long Revision { get; }
		public StealthMassAttackOrderToken Active { get; }
		public StealthMassAttackOrderToken Completed { get; }

		public StealthMassAttackActivityContext(bool hasObservation, long revision,
			StealthMassAttackOrderToken active, StealthMassAttackOrderToken completed)
		{
			if (revision < 0 || (!hasObservation && (revision != 0 || active != null || completed != null)) ||
				(active != null && active.ActivityRevision != revision) ||
				(completed != null && completed.ActivityRevision > revision))
				throw new ArgumentException("MassAttack activity context is noncanonical.");
			HasObservation = hasObservation;
			Revision = revision;
			Active = active;
			Completed = completed;
		}

		public static StealthMassAttackActivityContext From(StealthMassAttackLiveDecision decision)
		{
			return new StealthMassAttackActivityContext(decision.HasActivityObservation,
				decision.ActivityRevision, decision.ActiveOrderToken, decision.CompletedOrderToken);
		}

		public StealthMassAttackOrderToken Next(StealthMassAttackHandoff handoff,
			StealthMassAttackPhase phase, uint[] members, uint targetId, CPos targetCell,
			StealthMassAttackOrderToken previous, StealthMassAttackOrderToken prior,
			out bool shouldApply, out StealthMassAttackOrderToken nextPrior)
		{
			var relation = ValidateBridge(handoff, previous, prior);
			if (relation == PairRelation.Prior)
			{
				if (!MatchesAction(previous, handoff, phase, members, targetId, targetCell))
					throw new InvalidOperationException(
						"MassAttack pending successor no longer matches the desired action.");
				shouldApply = true;
				nextPrior = prior;
				return previous;
			}

			if (Active != null && MatchesAction(Active, handoff, phase, members, targetId, targetCell))
			{
				shouldApply = false;
				nextPrior = null;
				return Active;
			}

			var attempt = Math.Max(previous?.AttemptRevision ?? -1,
				Completed?.AttemptRevision ?? -1) + 1;
			if (attempt < 0)
				throw new InvalidOperationException("MassAttack order attempt revision is exhausted.");
			shouldApply = true;
			nextPrior = previous;
			return new StealthMassAttackOrderToken(handoff.Owner, handoff.Epoch, phase,
				Revision, attempt, members, targetId, targetCell);
		}

		public bool Same(StealthMassAttackActivityContext other)
		{
			return other != null && HasObservation == other.HasObservation && Revision == other.Revision &&
				Equals(Active, other.Active) && Equals(Completed, other.Completed);
		}

		public void ValidateSaved(StealthMassAttackHandoff handoff,
			StealthMassAttackPhase phase, uint[] members, uint? targetId, CPos? targetCell,
			StealthMassAttackOrderToken lastOrder, StealthMassAttackOrderToken priorOrder)
		{
			if (!targetId.HasValue)
			{
				if (Active != null || Completed != null)
					throw new InvalidOperationException("Targetless MassAttack state cannot retain activity.");
				return;
			}

			ValidateBridge(handoff, lastOrder, priorOrder);
			if (lastOrder != null &&
				!MatchesAction(lastOrder, handoff, phase, members, targetId.Value, targetCell.Value))
				throw new InvalidOperationException("Saved MassAttack order does not match the current action.");
		}

		public void ValidatePriorPair(StealthMassAttackHandoff handoff,
			StealthMassAttackOrderToken lastOrder, StealthMassAttackOrderToken priorOrder)
		{
			ValidateBridge(handoff, lastOrder, priorOrder);
		}

		public void ValidateTerminalPair(StealthMassAttackHandoff handoff,
			StealthMassAttackPhase phase, uint[] members, uint targetId, CPos targetCell)
		{
			if ((Active != null && !MatchesAction(Active, handoff, phase, members, targetId, targetCell)) ||
				(Completed != null && !Related(Completed, handoff, members, targetId, targetCell)) ||
				(Active != null && Completed != null && !Active.Equals(Completed) &&
					!IsImmediatePredecessor(Active, Completed)))
				throw new InvalidOperationException("Terminal MassAttack activity pair is unrelated.");
		}

		PairRelation ValidateBridge(StealthMassAttackHandoff handoff,
			StealthMassAttackOrderToken lastOrder, StealthMassAttackOrderToken priorOrder)
		{
			if (priorOrder != null && (lastOrder == null ||
				!IsIssuedSuccessor(lastOrder, priorOrder, handoff)))
				throw new InvalidOperationException("MassAttack prior/successor bridge is inconsistent.");

			var observed = Active ?? Completed;
			if (observed == null)
				return PairRelation.Lost;
			if (priorOrder != null && observed.Equals(priorOrder))
			{
				ValidateObservedPair(handoff, priorOrder, null);
				return PairRelation.Prior;
			}

			if (lastOrder != null && observed.Equals(lastOrder))
			{
				ValidateObservedPair(handoff, lastOrder, priorOrder);
				return PairRelation.Current;
			}

			throw new InvalidOperationException("MassAttack live activity is not the saved prior or current action.");
		}

		void ValidateObservedPair(StealthMassAttackHandoff handoff,
			StealthMassAttackOrderToken expected, StealthMassAttackOrderToken completedBridge)
		{
			var completedIsBridge = completedBridge != null && Active != null &&
				Active.Equals(expected) && Completed != null && Completed.Equals(completedBridge);
			if (!Owns(expected, handoff) || (Active != null && !Active.Equals(expected)) ||
				(Active == null && (Completed == null || !Completed.Equals(expected))) ||
				(Active != null && Completed != null && !Completed.Equals(Active) &&
					!completedIsBridge && !IsImmediatePredecessor(Active, Completed)))
				throw new InvalidOperationException("MassAttack observed activity pair is inconsistent.");
		}

		bool IsIssuedSuccessor(StealthMassAttackOrderToken current,
			StealthMassAttackOrderToken prior, StealthMassAttackHandoff handoff)
		{
			return Owns(current, handoff) && Owns(prior, handoff) &&
				prior.ActivityRevision <= current.ActivityRevision && prior.AttemptRevision != long.MaxValue &&
				current.AttemptRevision == prior.AttemptRevision + 1;
		}

		static bool IsImmediatePredecessor(StealthMassAttackOrderToken current,
			StealthMassAttackOrderToken completed)
		{
			return Related(completed, current) && completed.AttemptRevision != long.MaxValue &&
				current.AttemptRevision == completed.AttemptRevision + 1;
		}

		static bool MatchesAction(StealthMassAttackOrderToken token, StealthMassAttackHandoff handoff,
			StealthMassAttackPhase phase, uint[] members, uint targetId, CPos targetCell)
		{
			return token.Owner == handoff.Owner && token.Epoch == handoff.Epoch && token.Phase == phase &&
				token.ActorIds.SequenceEqual(members) && token.TargetActorId == targetId &&
				token.TargetCurrentCell == targetCell;
		}

		static bool Related(StealthMassAttackOrderToken token, StealthMassAttackHandoff handoff,
			uint[] members, uint targetId, CPos targetCell)
		{
			return token.Owner == handoff.Owner && token.Epoch == handoff.Epoch &&
				token.ActorIds.SequenceEqual(members) && token.TargetActorId == targetId &&
				token.TargetCurrentCell == targetCell;
		}

		static bool Related(StealthMassAttackOrderToken token, StealthMassAttackOrderToken action)
		{
			return token.Owner == action.Owner && token.Epoch == action.Epoch &&
				token.ActorIds.SequenceEqual(action.ActorIds) && token.TargetActorId == action.TargetActorId &&
				token.TargetCurrentCell == action.TargetCurrentCell;
		}

		static bool Owns(StealthMassAttackOrderToken token, StealthMassAttackHandoff handoff)
		{
			return token.Owner == handoff.Owner && token.Epoch == handoff.Epoch;
		}
	}
}
