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
using System.Text;

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
		public CPos TargetCurrentCell { get; }

		internal StealthMassAttackOrderToken(BehaviorId owner, OwnershipEpoch epoch,
			StealthMassAttackPhase phase, long activityRevision, long attemptRevision,
			IEnumerable<uint> actorIds,
			uint targetActorId, CPos targetCurrentCell)
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
			TargetCurrentCell = targetCurrentCell;
		}

		public bool Equals(StealthMassAttackOrderToken other)
		{
			return other != null && Owner == other.Owner && Epoch == other.Epoch &&
				Phase == other.Phase && ActivityRevision == other.ActivityRevision &&
				AttemptRevision == other.AttemptRevision &&
				TargetActorId == other.TargetActorId && TargetCurrentCell == other.TargetCurrentCell &&
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
				hash = (hash * 397) ^ TargetCurrentCell.GetHashCode();
				foreach (var actorId in actorIds)
					hash = (hash * 397) ^ actorId.GetHashCode();
				return hash;
			}
		}
	}

	sealed class StealthMassAttackLiveFingerprint : IEquatable<StealthMassAttackLiveFingerprint>
	{
		public string Canonical { get; }
		public StealthMassAttackLiveFingerprint(string canonical)
		{
			Canonical = !string.IsNullOrEmpty(canonical) ? canonical :
				throw new ArgumentException("MassAttack fingerprints must be non-empty.", nameof(canonical));
		}

		public static StealthMassAttackLiveFingerprint CreateEntry(StealthMassAttackLiveSnapshot live,
			IEnumerable<StealthMassAttackActorSnapshot> defenders,
			StealthMassAttackActorSnapshot target)
		{
			var text = new StringBuilder();
			text.Append("C=").Append(live.FormationCloaked ? 1 : 0).Append(";R=1;M=");
			foreach (var member in live.Members.OrderBy(member => member.ActorId))
				text.Append(member.ActorId).Append(',').Append(member.CurrentCell.Bits).Append(',')
					.Append(member.CurrentWeaponRangeCells).Append(',').Append(member.HitPoints).Append(',')
					.Append(member.MaximumHitPoints).Append(',').Append(member.IsInWorld ? 1 : 0).Append(',')
					.Append(member.IsDead ? 1 : 0).Append('|');
			text.Append(";T=").Append(target?.ActorId ?? 0).Append(',').Append(target?.CurrentCell.Bits ?? 0)
				.Append(',').Append(target?.HitPoints ?? 0).Append(',').Append(target?.MaximumHitPoints ?? 0)
				.Append(',').Append(target?.CurrentWeaponRangeCells ?? 0).Append(";E=");
			foreach (var enemy in defenders.OrderBy(enemy => enemy.ActorId))
				text.Append(enemy.ActorId).Append(',').Append(enemy.CurrentCell.Bits).Append(',')
					.Append(enemy.HitPoints).Append(',').Append(enemy.MaximumHitPoints).Append(',')
					.Append(enemy.CurrentWeaponRangeCells).Append(',')
					.Append(enemy.HasDetectorCoverage ? 1 : 0).Append('|');
			text.Append(";P=");
			foreach (var cell in live.CandidateCells)
				text.Append(cell.Bits).Append('|');
			return new StealthMassAttackLiveFingerprint(text.ToString());
		}

		public static StealthMassAttackLiveFingerprint CreateCurrent(
			StealthMassAttackLiveSnapshot live, StealthMassAttackActorSnapshot target)
		{
			var text = new StringBuilder();
			text.Append("C=").Append(live.FormationCloaked ? 1 : 0).Append(";M=");
			foreach (var member in live.Members)
				text.Append(member.ActorId).Append(',').Append(member.CurrentCell.Bits).Append(',')
					.Append(member.CurrentWeaponRangeCells).Append(',').Append(member.HitPoints).Append(',')
					.Append(member.MaximumHitPoints).Append(',').Append(member.IsInWorld ? 1 : 0).Append(',')
					.Append(member.IsDead ? 1 : 0).Append('|');
			text.Append(";T=").Append(target?.ActorId ?? 0).Append(";A=");
			foreach (var actor in live.Actors)
				text.Append(actor.ActorId).Append(',').Append(actor.ActorType).Append(',')
					.Append(actor.CurrentCell.Bits).Append(',').Append(actor.HitPoints).Append(',')
					.Append(actor.MaximumHitPoints).Append(',').Append(actor.CurrentWeaponRangeCells).Append(',')
					.Append(actor.IsInLocalEngagementArea ? 1 : 0).Append(',')
					.Append(actor.IsDefender ? 1 : 0).Append(',')
					.Append(actor.IsMissionObjective ? 1 : 0).Append(',')
					.Append(actor.HasDetectorCoverage ? 1 : 0).Append(',')
					.Append(actor.IsInWorld ? 1 : 0).Append(',').Append(actor.IsDead ? 1 : 0).Append(',')
					.Append(actor.IsTargetable ? 1 : 0).Append('|');
			return new StealthMassAttackLiveFingerprint(text.ToString());
		}

		public bool Equals(StealthMassAttackLiveFingerprint other)
		{
			return other != null && Canonical == other.Canonical;
		}

		public override bool Equals(object obj) { return Equals(obj as StealthMassAttackLiveFingerprint); }
		public override int GetHashCode() { return Canonical.GetHashCode(); }
	}

	sealed class StealthMassAttackEvaluation
	{
		public StealthMassAttackThreatFacts Facts { get; }
		public StealthMassAttackThreatResult Threat { get; }
		public StealthMassAttackEvaluation(StealthMassAttackThreatFacts facts,
			StealthMassAttackThreatResult threat)
		{
			Facts = facts ?? throw new ArgumentNullException(nameof(facts));
			Threat = threat;
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

			if (!TargetShape() || (Disposition == StealthMassAttackDisposition.Retain) !=
				(LastOrderToken != null) ||
				(Disposition == StealthMassAttackDisposition.Retain &&
					Threat.Value.StandardScore.Crossover <= 1) ||
				(Disposition == StealthMassAttackDisposition.RecalculateFlee &&
					Threat.Value.StandardScore.Crossover > 1))
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
					LastOrderToken.TargetActorId == SelectedTargetActorId &&
					LastOrderToken.TargetCurrentCell == SelectedTargetCurrentCell));
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

		internal StealthMassAttackTransition(StealthBehaviorHandoff handoff,
			StealthMassAttackResult result)
		{
			if (result.Disposition == StealthMassAttackDisposition.Retain)
				Retained = handoff;
			else if (result.Disposition == StealthMassAttackDisposition.UndefendedAttack)
				UndefendedAttack = new StealthUndefendedAttackHandoff(handoff, result.Mission);
			else if (result.Disposition == StealthMassAttackDisposition.Reacquire)
				Reacquisition = handoff;
			else
				RecalculateFlee = handoff;
		}
	}
}
