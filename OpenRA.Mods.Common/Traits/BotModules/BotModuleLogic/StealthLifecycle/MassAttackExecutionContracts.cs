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
using System.Collections.ObjectModel;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	public sealed class StealthMassAttackOrderToken : IEquatable<StealthMassAttackOrderToken>
	{
		readonly ReadOnlyCollection<uint> actorIds;
		public BehaviorId Owner { get; }
		public OwnershipEpoch Epoch { get; }
		public StealthMassAttackPhase Phase { get; }
		public long ActivityRevision { get; }
		public long AttemptRevision { get; }
		public IReadOnlyList<uint> ActorIds => actorIds;
		public uint TargetActorId { get; }
		public CPos OrderCell { get; }

		internal StealthMassAttackOrderToken(BehaviorId owner, OwnershipEpoch epoch,
			StealthMassAttackPhase phase, long activityRevision, long attemptRevision,
			IEnumerable<uint> actorIds,
			uint targetActorId, CPos orderCell)
		{
			var actors = actorIds?.OrderBy(id => id).ToArray();
			if (owner != BehaviorId.MassAttack || !Enum.IsDefined(typeof(StealthMassAttackPhase), phase) ||
				activityRevision < 0 || attemptRevision < 0 || targetActorId == 0 ||
				actors == null || actors.Length == 0 ||
				actors.Any(id => id == 0) || actors.Distinct().Count() != actors.Length)
				throw new ArgumentException("Invalid MassAttack order token.");
			Owner = owner;
			Epoch = epoch;
			Phase = phase;
			ActivityRevision = activityRevision;
			AttemptRevision = attemptRevision;
			this.actorIds = Array.AsReadOnly(actors);
			TargetActorId = targetActorId;
			OrderCell = orderCell;
		}

		public bool Equals(StealthMassAttackOrderToken other)
		{
			return other != null && Owner == other.Owner && Epoch == other.Epoch &&
				Phase == other.Phase && ActivityRevision == other.ActivityRevision &&
				AttemptRevision == other.AttemptRevision &&
				TargetActorId == other.TargetActorId && OrderCell == other.OrderCell &&
				actorIds.SequenceEqual(other.actorIds);
		}

		public override bool Equals(object obj) { return Equals(obj as StealthMassAttackOrderToken); }
		public override int GetHashCode()
		{
			unchecked
			{
				var hash = ((int)Owner * 397) ^ Epoch.GetHashCode();
				hash = (hash * 397) ^ (int)Phase;
				hash = (hash * 397) ^ ActivityRevision.GetHashCode();
				hash = (hash * 397) ^ AttemptRevision.GetHashCode();
				hash = (hash * 397) ^ TargetActorId.GetHashCode();
				hash = (hash * 397) ^ OrderCell.GetHashCode();
				foreach (var actorId in actorIds)
					hash = (hash * 397) ^ actorId.GetHashCode();
				return hash;
			}
		}
	}

	public sealed class StealthMassAttackResult
	{
		readonly ReadOnlyCollection<uint> memberIds;
		readonly ReadOnlyCollection<uint> defenderIds;
		readonly ReadOnlyCollection<uint> objectiveIds;
		internal StealthMassAttackHandoff Source { get; }
		internal StealthBehaviorHandoff Handoff { get; }
		public StealthApproachMission Mission { get; }
		public StealthMassAttackDisposition Disposition { get; }
		public StealthMassAttackPhase Phase { get; }
		public uint? SelectedTargetActorId { get; }
		public CPos? SelectedTargetCurrentCell { get; }
		public IReadOnlyList<uint> ActiveMemberActorIds => memberIds;
		public IReadOnlyList<uint> LiveDefenderActorIds => defenderIds;
		public IReadOnlyList<uint> LiveObjectiveActorIds => objectiveIds;
		public StealthMassAttackThreatFacts ThreatFacts { get; }
		public StealthMassAttackThreatResult? Threat { get; }
		public StealthMassAttackOrderToken LastOrderToken { get; }

		internal StealthMassAttackResult(StealthMassAttackHandoff source,
			StealthApproachMission mission, StealthMassAttackDisposition disposition,
			StealthMassAttackPhase phase, uint? targetId, CPos? targetCell,
			IEnumerable<uint> members, IEnumerable<uint> defenders, IEnumerable<uint> objectives,
			StealthMassAttackThreatFacts facts, StealthMassAttackThreatResult? threat,
			StealthMassAttackOrderToken lastOrderToken)
		{
			Source = source ?? throw new ArgumentNullException(nameof(source));
			Handoff = source.Handoff;
			if (!ReferenceEquals(mission, source.Mission))
				throw new ArgumentException("MassAttack results must preserve the exact mission.", nameof(mission));
			Mission = mission;
			Disposition = disposition;
			Phase = phase;
			SelectedTargetActorId = targetId;
			SelectedTargetCurrentCell = targetCell;
			memberIds = Canonical(members, nameof(members));
			defenderIds = Canonical(defenders, nameof(defenders));
			objectiveIds = Canonical(objectives, nameof(objectives));
			ThreatFacts = facts;
			Threat = threat;
			LastOrderToken = lastOrderToken;
			ValidateShape();
		}

		void ValidateShape()
		{
			if (!Enum.IsDefined(typeof(StealthMassAttackDisposition), Disposition) ||
				!Enum.IsDefined(typeof(StealthMassAttackPhase), Phase))
				throw new ArgumentException("MassAttack result has an invalid decision.");
			if (Disposition == StealthMassAttackDisposition.UndefendedAttack)
			{
				if (!TargetlessShape() || memberIds.Count == 0 || defenderIds.Count != 0 ||
					objectiveIds.Count == 0)
					throw new ArgumentException("MassAttack undefended result has no exact live cause.");
				return;
			}

			if (Disposition == StealthMassAttackDisposition.Reacquire)
			{
				if (!TargetlessShape() || memberIds.Count == 0 || defenderIds.Count != 0 ||
					objectiveIds.Count != 0)
					throw new ArgumentException("MassAttack reacquire result has no exact live cause.");
				return;
			}

			if (Disposition == StealthMassAttackDisposition.RecalculateFlee && memberIds.Count == 0)
			{
				if (!TargetlessShape())
					throw new ArgumentException("MassAttack zero-member result has no exact live cause.");
				return;
			}

			if (Disposition == StealthMassAttackDisposition.StrategicRecalculation)
			{
				if (!TargetShape() || LastOrderToken != null ||
					Threat.Value.StandardScore.Crossover <= 1)
					throw new ArgumentException("MassAttack strategic recalculation has no exact live cause.");
				return;
			}

			if (!TargetShape() || (Disposition == StealthMassAttackDisposition.Retain) !=
				(LastOrderToken != null) ||
				(Disposition == StealthMassAttackDisposition.Retain &&
					Threat.Value.StandardScore.Crossover <= 1 &&
					!Source.Evidence.CoordinatedMassAttack) ||
				(Disposition == StealthMassAttackDisposition.RecalculateFlee &&
					Threat.Value.StandardScore.Crossover > 1 && Threat.Value.AttackApproved))
				throw new ArgumentException("MassAttack targeted result has no exact live cause.");
		}

		bool TargetlessShape()
		{
			return Phase == StealthMassAttackPhase.Advance && !SelectedTargetActorId.HasValue &&
				!SelectedTargetCurrentCell.HasValue && ThreatFacts == null && !Threat.HasValue &&
				LastOrderToken == null;
		}

		bool TargetShape()
		{
			return memberIds.Count != 0 && defenderIds.Count != 0 && SelectedTargetActorId.HasValue &&
				SelectedTargetCurrentCell.HasValue && ThreatFacts != null && Threat.HasValue &&
				defenderIds.Contains(SelectedTargetActorId.Value) &&
				ThreatFacts.SelectedTargetActorId == SelectedTargetActorId.Value &&
				ThreatFacts.SelectedTargetCurrentCell == SelectedTargetCurrentCell.Value &&
				ThreatFacts.FriendlyActorIds.SequenceEqual(memberIds) &&
				ThreatFacts.EnemyActorIds.SequenceEqual(defenderIds) &&
				(LastOrderToken == null || (LastOrderToken.Owner == BehaviorId.MassAttack &&
					LastOrderToken.Epoch == Handoff.Epoch && LastOrderToken.Phase == Phase &&
					LastOrderToken.ActorIds.SequenceEqual(memberIds) &&
					(LastOrderToken.TargetActorId == SelectedTargetActorId ||
						objectiveIds.Contains(LastOrderToken.TargetActorId))));
		}

		static ReadOnlyCollection<uint> Canonical(IEnumerable<uint> ids, string parameter)
		{
			var copy = ids?.ToArray();
			if (copy == null || copy.Any(id => id == 0) ||
				!copy.SequenceEqual(copy.OrderBy(id => id)) || copy.Distinct().Count() != copy.Length)
				throw new ArgumentException("MassAttack result identities must be canonical.", parameter);
			return Array.AsReadOnly(copy);
		}
	}

	public sealed class StealthMassAttackTransition
	{
		public StealthBehaviorHandoff Retained { get; }
		public StealthUndefendedAttackHandoff UndefendedAttack { get; }
		public StealthBehaviorHandoff Reacquisition { get; }
		public StealthBehaviorHandoff RecalculateFlee { get; }
		public StealthRecalculateFleeHandoff RecalculateFleeEntry { get; }
		public StealthBehaviorHandoff SquadConstruction { get; }
		public StealthSquadConstructionRecoveryHandoff SquadConstructionEntry { get; }

		internal StealthMassAttackTransition(StealthBehaviorHandoff handoff,
			StealthMassAttackResult result)
		{
			if (result.Disposition == StealthMassAttackDisposition.Retain)
				Retained = handoff;
			else if (result.Disposition == StealthMassAttackDisposition.UndefendedAttack)
				UndefendedAttack = new StealthUndefendedAttackHandoff(handoff, result.Mission);
			else if (result.Disposition == StealthMassAttackDisposition.Reacquire)
				Reacquisition = handoff;
			else if (result.Disposition == StealthMassAttackDisposition.StrategicRecalculation)
				Reacquisition = handoff;
			else if (handoff.Owner == BehaviorId.RecalculateFlee)
			{
				RecalculateFlee = handoff;
				RecalculateFleeEntry = new StealthRecalculateFleeHandoff(handoff, result);
			}
			else
			{
				SquadConstruction = handoff;
				SquadConstructionEntry = new StealthSquadConstructionRecoveryHandoff(
					handoff, result.Mission);
			}
		}
	}
}
