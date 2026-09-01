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
	/// Disconnected lifecycle scaffold. Ownership changes only when the current handoff is returned
	/// in a behavior result; observing time or world events can never select another owner.
	/// </summary>
	public sealed class StealthLifecycleController : IStealthLifecycleOwnershipGuard
	{
		BehaviorId owner;
		OwnershipEpoch epoch;
		int lastObservedTick;

		public BehaviorId Owner => owner;
		public OwnershipEpoch Epoch => epoch;
		public int LastObservedTick => lastObservedTick;
		public StealthBehaviorHandoff CurrentHandoff => new StealthBehaviorHandoff(owner, epoch);

		public bool IsActive(BehaviorId candidateOwner, OwnershipEpoch candidateEpoch)
		{
			return owner == candidateOwner && epoch == candidateEpoch;
		}

		public StealthLifecycleController()
			: this(BehaviorId.Start, new OwnershipEpoch(1), -1) { }

		StealthLifecycleController(BehaviorId owner, OwnershipEpoch epoch, int lastObservedTick)
		{
			if (!Enum.IsDefined(typeof(BehaviorId), owner))
				throw new ArgumentOutOfRangeException(nameof(owner));
			if (epoch.Value <= 0)
				throw new ArgumentOutOfRangeException(nameof(epoch));
			if (lastObservedTick < -1)
				throw new ArgumentOutOfRangeException(nameof(lastObservedTick));

			this.owner = owner;
			this.epoch = epoch;
			this.lastObservedTick = lastObservedTick;
		}

		public void Observe(StealthLifecycleObservationFrame frame)
		{
			if (frame == null)
				throw new ArgumentNullException(nameof(frame));
			if (frame.Tick < lastObservedTick)
				throw new ArgumentOutOfRangeException(nameof(frame),
					"Lifecycle observations must be supplied in tick order.");

			lastObservedTick = frame.Tick;
		}

		public bool TryAccept(StealthStartResult result, out StealthBehaviorHandoff nextHandoff)
		{
			nextHandoff = null;
			if (result == null || !result.HasTransition || owner != BehaviorId.Start ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			nextHandoff = AdvanceTo(BehaviorId.SquadConstruction);
			return true;
		}

		public bool TryAccept(StealthSquadConstructionResult result,
			out StealthBehaviorHandoff nextHandoff)
		{
			nextHandoff = null;
			if (result == null || !result.IsComplete || owner != BehaviorId.SquadConstruction ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			nextHandoff = AdvanceTo(BehaviorId.TargetAcquisition);
			return true;
		}

		public bool TryAccept(StealthTargetAcquisitionResult result,
			out StealthTargetValueFilterHandoff nextHandoff)
		{
			nextHandoff = null;
			if (result == null || !result.IsReadyForValueFilter || owner != BehaviorId.TargetAcquisition ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			nextHandoff = new StealthTargetValueFilterHandoff(
				AdvanceTo(BehaviorId.TargetValueFilter), result.Options);
			return true;
		}

		public bool TryAccept(StealthTargetValueFilterResult result,
			out StealthTargetThreatFilterHandoff nextHandoff)
		{
			nextHandoff = null;
			if (result == null || !result.IsReadyForThreatFilter || owner != BehaviorId.TargetValueFilter ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			nextHandoff = new StealthTargetThreatFilterHandoff(
				AdvanceTo(BehaviorId.TargetThreatFilter), result.Options);
			return true;
		}

		public bool TryAccept(StealthTargetThreatFilterResult result,
			out StealthTargetDistanceChoiceHandoff nextHandoff)
		{
			nextHandoff = null;
			if (result == null || !result.IsReadyForDistanceChoice || owner != BehaviorId.TargetThreatFilter ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			nextHandoff = new StealthTargetDistanceChoiceHandoff(
				AdvanceTo(BehaviorId.TargetDistanceChoice), result.Options);
			return true;
		}

		public bool TryAccept(StealthTargetDistanceChoiceResult result,
			out StealthApproachHandoff nextHandoff)
		{
			nextHandoff = null;
			if (result == null || !result.IsReadyForApproach || owner != BehaviorId.TargetDistanceChoice ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			nextHandoff = new StealthApproachHandoff(
				AdvanceTo(BehaviorId.Approach), result.Mission);
			return true;
		}

		public bool TryAccept(StealthApproachResult result,
			out StealthApproachTransition transition)
		{
			transition = null;
			if (result == null || owner != BehaviorId.Approach ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			BehaviorId nextOwner;
			switch (result.Disposition)
			{
				case StealthApproachDisposition.Reacquire:
					nextOwner = BehaviorId.TargetAcquisition;
					break;
				case StealthApproachDisposition.UndefendedAttack:
					if (result.ArrivalClassification != StealthApproachArrivalClassification.Undefended ||
						result.LiveDefenderActorIds.Count != 0)
						return false;
					nextOwner = BehaviorId.UndefendedAttack;
					break;
				case StealthApproachDisposition.CrushEvaluation:
					if (result.ArrivalClassification != StealthApproachArrivalClassification.Defended ||
						result.LiveDefenderActorIds.Count == 0)
						return false;
					nextOwner = BehaviorId.CrushEvaluation;
					break;
				default:
					return false;
			}

			transition = new StealthApproachTransition(AdvanceTo(nextOwner), result);
			return true;
		}

		public bool TryAccept(StealthUndefendedAttackResult result,
			out StealthUndefendedAttackTransition transition)
		{
			transition = null;
			if (result == null || owner != BehaviorId.UndefendedAttack ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			StealthBehaviorHandoff nextHandoff;
			switch (result.Disposition)
			{
				case StealthUndefendedAttackDisposition.Retain:
					if (result.LiveDefenderActorIds.Count != 0)
						return false;
					nextHandoff = CurrentHandoff;
					break;
				case StealthUndefendedAttackDisposition.Reacquire:
					if (result.LiveDefenderActorIds.Count != 0)
						return false;
					nextHandoff = AdvanceTo(BehaviorId.TargetAcquisition);
					break;
				case StealthUndefendedAttackDisposition.CrushEvaluation:
					if (result.LiveDefenderActorIds.Count == 0)
						return false;
					nextHandoff = AdvanceTo(BehaviorId.CrushEvaluation);
					break;
				default:
					return false;
			}

			transition = new StealthUndefendedAttackTransition(nextHandoff, result);
			return true;
		}

		public bool TryAccept(StealthCrushResult result, out StealthCrushTransition transition)
		{
			transition = null;
			if (result == null || owner != BehaviorId.CrushEvaluation ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			StealthBehaviorHandoff nextHandoff;
			switch (result.Disposition)
			{
				case StealthCrushDisposition.Retain:
					if (result.LiveDefenderActorIds.Count == 0 ||
						!result.SelectedTargetActorId.HasValue || !result.Safety.HasValue ||
						!result.Safety.Value.Approved)
						return false;
					nextHandoff = CurrentHandoff;
					break;
				case StealthCrushDisposition.Kite:
					if (result.LiveDefenderActorIds.Count == 0 ||
						(result.Safety.HasValue && result.Safety.Value.Approved))
						return false;
					nextHandoff = AdvanceTo(BehaviorId.Kite);
					break;
				case StealthCrushDisposition.UndefendedAttack:
					if (result.LiveDefenderActorIds.Count != 0 || result.LiveObjectiveActorIds.Count == 0)
						return false;
					nextHandoff = AdvanceTo(BehaviorId.UndefendedAttack);
					break;
				case StealthCrushDisposition.Reacquire:
					if (result.LiveDefenderActorIds.Count != 0 || result.LiveObjectiveActorIds.Count != 0)
						return false;
					nextHandoff = AdvanceTo(BehaviorId.TargetAcquisition);
					break;
				default:
					return false;
			}

			transition = new StealthCrushTransition(nextHandoff, result);
			return true;
		}

		public bool TryAccept(StealthKiteResult result, out StealthKiteTransition transition)
		{
			transition = null;
			if (result == null || owner != BehaviorId.Kite ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			BehaviorId nextOwner;
			switch (result.Disposition)
			{
				case StealthKiteDisposition.Retain:
					if (result.ActiveMemberActorIds.Count == 0 || result.LiveDefenderActorIds.Count == 0 ||
						!result.SelectedTargetActorId.HasValue || !result.FireCell.HasValue ||
						!result.WithdrawCell.HasValue || !result.Safety.HasValue ||
						!result.Safety.Value.Approved || result.FallbackEvidence != null)
						return false;
					nextOwner = BehaviorId.Kite;
					break;
				case StealthKiteDisposition.CrushEvaluation:
					if (result.ActiveMemberActorIds.Count == 0 || result.LiveDefenderActorIds.Count == 0 ||
						!result.SelectedTargetActorId.HasValue || result.FireCell.HasValue ||
						result.WithdrawCell.HasValue || result.Safety.HasValue || result.FallbackEvidence != null)
						return false;
					nextOwner = BehaviorId.CrushEvaluation;
					break;
				case StealthKiteDisposition.UndefendedAttack:
					if (result.LiveDefenderActorIds.Count != 0 || result.LiveObjectiveActorIds.Count == 0 ||
						result.SelectedTargetActorId.HasValue || result.FireCell.HasValue ||
						result.WithdrawCell.HasValue || result.Safety.HasValue || result.FallbackEvidence != null)
						return false;
					nextOwner = BehaviorId.UndefendedAttack;
					break;
				case StealthKiteDisposition.Reacquire:
					if (result.LiveDefenderActorIds.Count != 0 || result.LiveObjectiveActorIds.Count != 0 ||
						result.SelectedTargetActorId.HasValue || result.FireCell.HasValue ||
						result.WithdrawCell.HasValue || result.Safety.HasValue || result.FallbackEvidence != null)
						return false;
					nextOwner = BehaviorId.TargetAcquisition;
					break;
				case StealthKiteDisposition.MassAttack:
					if (!ValidKiteFallback(result, true))
						return false;
					nextOwner = BehaviorId.MassAttack;
					break;
				case StealthKiteDisposition.RecalculateFlee:
					if (!ValidKiteFallback(result, false))
						return false;
					nextOwner = result.FallbackEvidence.Reason ==
						StealthKiteFallbackReason.NoLiveMembers ?
						BehaviorId.SquadConstruction : BehaviorId.RecalculateFlee;
					break;
				default:
					return false;
			}

			var nextHandoff = nextOwner == BehaviorId.Kite ? CurrentHandoff : AdvanceTo(nextOwner);
			transition = new StealthKiteTransition(nextHandoff, result);
			return true;
		}

		public bool TryAccept(StealthMassAttackResult result,
			out StealthMassAttackTransition transition)
		{
			transition = null;
			if (result == null || owner != BehaviorId.MassAttack ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch ||
				result.Source == null || !ReferenceEquals(result.Mission, result.Source.Mission) ||
				result.Source.Handoff.Owner != owner || result.Source.Handoff.Epoch != epoch)
				return false;

			StealthBehaviorHandoff nextHandoff;
			switch (result.Disposition)
			{
				case StealthMassAttackDisposition.Retain:
					if (!ValidMassTarget(result) || result.Threat.Value.StandardScore.Crossover <= 1 ||
						!ValidMassOrder(result))
						return false;
					nextHandoff = CurrentHandoff;
					break;
				case StealthMassAttackDisposition.UndefendedAttack:
					if (!ValidMassTargetless(result) || result.ActiveMemberActorIds.Count == 0 ||
						result.LiveDefenderActorIds.Count != 0 || result.LiveObjectiveActorIds.Count == 0)
						return false;
					nextHandoff = AdvanceTo(BehaviorId.UndefendedAttack);
					break;
				case StealthMassAttackDisposition.Reacquire:
					if (!ValidMassTargetless(result) || result.ActiveMemberActorIds.Count == 0 ||
						result.LiveDefenderActorIds.Count != 0 || result.LiveObjectiveActorIds.Count != 0)
						return false;
					nextHandoff = AdvanceTo(BehaviorId.TargetAcquisition);
					break;
				case StealthMassAttackDisposition.RecalculateFlee:
					var zeroMembers = result.ActiveMemberActorIds.Count == 0;
					if ((!zeroMembers && (!ValidMassTarget(result) ||
							result.Threat.Value.StandardScore.Crossover > 1)) ||
						(zeroMembers && !ValidMassTargetless(result)) ||
						result.LastOrderToken != null)
						return false;
					nextHandoff = AdvanceTo(zeroMembers ? BehaviorId.SquadConstruction :
						BehaviorId.RecalculateFlee);
					break;
				default:
					return false;
			}

			transition = new StealthMassAttackTransition(nextHandoff, result);
			return true;
		}

		public bool TryAccept(StealthRecalculateFleeResult result,
			out StealthRecalculateFleeTransition transition)
		{
			transition = null;
			if (result == null || owner != BehaviorId.RecalculateFlee ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch ||
				result.Source == null || !ReferenceEquals(result.Mission, result.Source.Mission))
				return false;

			StealthBehaviorHandoff next;
			if (result.Disposition == StealthRecalculateFleeDisposition.Retain)
			{
				if (result.LiveCause == StealthRecalculateFleeLiveCause.Completed)
					return false;
				next = CurrentHandoff;
			}
			else if (result.Disposition == StealthRecalculateFleeDisposition.TargetAcquisition)
			{
				if (result.LiveCause != StealthRecalculateFleeLiveCause.Completed ||
					!result.SelectedDestinationCell.HasValue || result.LastOrderToken == null)
					return false;
				next = AdvanceTo(BehaviorId.TargetAcquisition);
			}
			else
				return false;

			transition = new StealthRecalculateFleeTransition(next, result);
			return true;
		}

		/// <summary>Bounded Damage-to-Repair seam; Damage itself remains unimplemented here.</summary>
		public bool TryAccept(StealthDamageRepairRequest request,
			out StealthRepairHandoff repairHandoff)
		{
			return StealthRepairControllerSeams.TryAccept(request, owner, epoch,
				AdvanceTo, out repairHandoff);
		}

		/// <summary>Bounded Repair yield seam; no runtime controller loop is registered.</summary>
		public bool TryAccept(StealthRepairResult result, out StealthRepairTransition transition)
		{
			return StealthRepairControllerSeams.TryAccept(result, owner, epoch,
				CurrentHandoff, AdvanceTo, out transition);
		}

		static bool ValidMassTarget(StealthMassAttackResult result)
		{
			var facts = result.ThreatFacts;
			return result.ActiveMemberActorIds.Count != 0 && result.LiveDefenderActorIds.Count != 0 &&
				result.SelectedTargetActorId.HasValue && result.SelectedTargetCurrentCell.HasValue &&
				facts != null && result.Threat.HasValue &&
				result.LiveDefenderActorIds.Contains(result.SelectedTargetActorId.Value) &&
				facts.SelectedTargetActorId == result.SelectedTargetActorId.Value &&
				facts.SelectedTargetCurrentCell == result.SelectedTargetCurrentCell.Value &&
				facts.FriendlyActorIds.SequenceEqual(result.ActiveMemberActorIds) &&
				facts.EnemyActorIds.SequenceEqual(result.LiveDefenderActorIds) &&
				facts.PlannedReveal && facts.PlannedAttack && facts.FullCurrentFiringRangeExposure;
		}

		static bool ValidMassTargetless(StealthMassAttackResult result)
		{
			return result.Phase == StealthMassAttackPhase.Advance &&
				!result.SelectedTargetActorId.HasValue && !result.SelectedTargetCurrentCell.HasValue &&
				result.ThreatFacts == null && !result.Threat.HasValue && result.LastOrderToken == null;
		}

		static bool ValidMassOrder(StealthMassAttackResult result)
		{
			var token = result.LastOrderToken;
			return token != null && token.Owner == BehaviorId.MassAttack &&
				token.Epoch == result.Handoff.Epoch && token.Phase == result.Phase &&
				token.ActorIds.SequenceEqual(result.ActiveMemberActorIds) &&
				token.TargetActorId == result.SelectedTargetActorId &&
				token.TargetCurrentCell == result.SelectedTargetCurrentCell;
		}

		static bool ValidKiteFallback(StealthKiteResult result, bool massAttack)
		{
			var evidence = result.FallbackEvidence;
			if (evidence == null || result.LiveDefenderActorIds.Count == 0 ||
				result.FireCell.HasValue || result.WithdrawCell.HasValue || result.Safety.HasValue ||
				!evidence.DefenderActorIds.SequenceEqual(result.LiveDefenderActorIds))
				return false;
			if (evidence.Reason == StealthKiteFallbackReason.NoLiveMembers)
				return !massAttack && result.ActiveMemberActorIds.Count == 0 &&
					!result.SelectedTargetActorId.HasValue && evidence.AttackFacts == null &&
					!evidence.AttackScore.HasValue;

			var facts = evidence.AttackFacts;
			var score = evidence.AttackScore;
			return evidence.Reason == StealthKiteFallbackReason.NoSafePlan &&
				result.ActiveMemberActorIds.Count != 0 && result.SelectedTargetActorId.HasValue &&
				result.SelectedTargetCurrentCell.HasValue && facts != null && score.HasValue &&
				facts.SelectedTargetActorId == result.SelectedTargetActorId.Value &&
				facts.SelectedTargetCurrentCell == result.SelectedTargetCurrentCell.Value &&
				facts.FriendlyActorIds.SequenceEqual(result.ActiveMemberActorIds) &&
				facts.EnemyActorIds.SequenceEqual(result.LiveDefenderActorIds) &&
				massAttack == (score.Value.Crossover > 2);
		}

		StealthBehaviorHandoff AdvanceTo(BehaviorId nextOwner)
		{
			if (epoch.Value == long.MaxValue)
				throw new InvalidOperationException("The stealth lifecycle ownership epoch is exhausted.");

			owner = nextOwner;
			epoch = new OwnershipEpoch(epoch.Value + 1);
			return CurrentHandoff;
		}

		public StealthLifecycleSavePayload ExportState()
		{
			return new StealthLifecycleSavePayload(owner, epoch, lastObservedTick);
		}

		public static StealthLifecycleController Restore(StealthLifecycleSavePayload payload)
		{
			if (payload == null)
				throw new ArgumentNullException(nameof(payload));

			return new StealthLifecycleController(payload.Owner, payload.Epoch, payload.LastObservedTick);
		}
	}
}
