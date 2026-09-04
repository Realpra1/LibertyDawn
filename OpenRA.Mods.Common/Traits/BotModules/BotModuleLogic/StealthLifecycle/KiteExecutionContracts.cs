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
	public sealed class StealthKiteOrderToken : IEquatable<StealthKiteOrderToken>
	{
		readonly ReadOnlyCollection<uint> actorIds;
		public BehaviorId Owner { get; }
		public OwnershipEpoch Epoch { get; }
		public StealthKiteAction Action { get; }
		public IReadOnlyList<uint> ActorIds => actorIds;
		public uint? TargetActorId { get; }
		public CPos Cell { get; }
		public long PhaseRevision { get; }
		public long ActivityRevision { get; }

		internal StealthKiteOrderToken(BehaviorId owner, OwnershipEpoch epoch,
			StealthKiteAction action, IEnumerable<uint> actorIds, uint? targetActorId,
			CPos cell, long phaseRevision, long activityRevision)
		{
			if (owner != BehaviorId.Kite || !Enum.IsDefined(typeof(StealthKiteAction), action) ||
				phaseRevision < 0 || activityRevision < 0 || targetActorId == 0 ||
				(targetActorId.HasValue != (action == StealthKiteAction.Fire)))
				throw new ArgumentException("Invalid Kite order token.");
			var normalized = actorIds?.OrderBy(id => id).ToArray();
			if (normalized == null || normalized.Length == 0 || normalized.Any(id => id == 0) ||
				normalized.Distinct().Count() != normalized.Length)
				throw new ArgumentException("Kite order tokens require unique live members.", nameof(actorIds));
			Owner = owner;
			Epoch = epoch;
			Action = action;
			this.actorIds = Array.AsReadOnly(normalized);
			TargetActorId = targetActorId;
			Cell = cell;
			PhaseRevision = phaseRevision;
			ActivityRevision = activityRevision;
		}

		public bool Equals(StealthKiteOrderToken other)
		{
			return other != null && Owner == other.Owner && Epoch == other.Epoch &&
				Action == other.Action && TargetActorId == other.TargetActorId && Cell == other.Cell &&
				PhaseRevision == other.PhaseRevision && ActivityRevision == other.ActivityRevision &&
				actorIds.SequenceEqual(other.actorIds);
		}

		public override bool Equals(object obj) { return Equals(obj as StealthKiteOrderToken); }
		public override int GetHashCode()
		{
			unchecked
			{
				var hash = ((int)Owner * 397) ^ Epoch.GetHashCode();
				hash = (hash * 397) ^ (int)Action;
				hash = (hash * 397) ^ Cell.GetHashCode();
				hash = (hash * 397) ^ PhaseRevision.GetHashCode();
				hash = (hash * 397) ^ ActivityRevision.GetHashCode();
				hash = (hash * 397) ^ TargetActorId.GetHashCode();
				foreach (var actorId in actorIds)
					hash = (hash * 397) ^ actorId.GetHashCode();
				return hash;
			}
		}
	}

	public enum StealthKiteFallbackReason { NoLiveMembers, UnsafeCurrentPosition, NoSafePlan }

	public sealed class StealthKiteFallbackFacts
	{
		readonly ReadOnlyCollection<uint> friendlyActorIds;
		readonly ReadOnlyCollection<uint> enemyActorIds;
		public uint SelectedTargetActorId { get; }
		public CPos SelectedTargetCurrentCell { get; }
		public IReadOnlyList<uint> FriendlyActorIds => friendlyActorIds;
		public IReadOnlyList<uint> EnemyActorIds => enemyActorIds;
		public bool FormationCloaked { get; }
		public bool PlannedDecloak => true;
		public bool PlannedAttack => true;
		public bool PlannedCurrentRangeEngagement => true;

		public StealthKiteFallbackFacts(uint selectedTargetActorId, CPos selectedTargetCurrentCell,
			IEnumerable<uint> friendlyActorIds, IEnumerable<uint> enemyActorIds, bool formationCloaked)
		{
			if (selectedTargetActorId == 0)
				throw new ArgumentOutOfRangeException(nameof(selectedTargetActorId));
			var friendly = Normalize(friendlyActorIds, false, nameof(friendlyActorIds));
			var enemy = Normalize(enemyActorIds, false, nameof(enemyActorIds));
			if (!enemy.Contains(selectedTargetActorId))
				throw new ArgumentException("Fallback enemies must contain the selected live target.");
			SelectedTargetActorId = selectedTargetActorId;
			SelectedTargetCurrentCell = selectedTargetCurrentCell;
			this.friendlyActorIds = friendly;
			this.enemyActorIds = enemy;
			FormationCloaked = formationCloaked;
		}

		static ReadOnlyCollection<uint> Normalize(IEnumerable<uint> ids, bool allowEmpty, string name)
		{
			if (ids == null)
				throw new ArgumentNullException(name);
			var normalized = ids.OrderBy(id => id).ToArray();
			if ((!allowEmpty && normalized.Length == 0) || normalized.Any(id => id == 0) ||
				normalized.Distinct().Count() != normalized.Length)
				throw new ArgumentException("Fallback identities must be unique and nonzero.", name);
			return Array.AsReadOnly(normalized);
		}
	}

	public sealed class StealthKiteFallbackEvidence
	{
		readonly ReadOnlyCollection<uint> defenderActorIds;
		public StealthKiteFallbackReason Reason { get; }
		public string LiveFingerprint { get; }
		public IReadOnlyList<uint> DefenderActorIds => defenderActorIds;
		public StealthKiteFallbackFacts AttackFacts { get; }
		public StealthTargetThreatScore? AttackScore { get; }
		public bool CoordinatedMassAttack { get; }

		internal StealthKiteFallbackEvidence(StealthKiteFallbackReason reason,
			string liveFingerprint, IEnumerable<uint> defenderActorIds,
			StealthKiteFallbackFacts attackFacts, StealthTargetThreatScore? attackScore,
			bool coordinatedMassAttack = false)
		{
			if (!Enum.IsDefined(typeof(StealthKiteFallbackReason), reason) ||
				string.IsNullOrEmpty(liveFingerprint) || defenderActorIds == null)
				throw new ArgumentException("Invalid Kite fallback evidence.");
			var defenders = defenderActorIds.OrderBy(id => id).ToArray();
			if (defenders.Length == 0 || defenders.Any(id => id == 0) ||
				defenders.Distinct().Count() != defenders.Length)
				throw new ArgumentException("Fallback evidence requires current defenders.");
			if (reason != StealthKiteFallbackReason.NoLiveMembers ?
				attackFacts == null || !attackScore.HasValue : attackFacts != null || attackScore.HasValue)
				throw new ArgumentException("Fallback evidence does not match its reason.");
			Reason = reason;
			LiveFingerprint = liveFingerprint;
			this.defenderActorIds = Array.AsReadOnly(defenders);
			AttackFacts = attackFacts;
			AttackScore = attackScore;
			CoordinatedMassAttack = coordinatedMassAttack;
		}
	}
}
