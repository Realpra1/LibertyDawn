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
	public enum StealthRecalculateFleeSource
	{
		KiteNoSafePlan,
		KiteUnsafeCurrentPosition,
		ApproachUnsafeCurrentPosition,
		MassAttackCrossover
	}

	public enum StealthRecalculateFleeDisposition { Retain, TargetAcquisition }
	public enum StealthRecalculateFleeLiveCause { Traversing, NoTarget, NoRoute, MemberLoss, Completed }

	/// <summary>Immutable, scored reason that alone may create RecalculateFlee ownership.</summary>
	public sealed class StealthRecalculateFleeEntryEvidence
	{
		readonly ReadOnlyCollection<uint> memberIds;
		readonly ReadOnlyCollection<uint> enemyIds;
		public StealthRecalculateFleeSource Source { get; }
		public BehaviorId SourceOwner => Source == StealthRecalculateFleeSource.MassAttackCrossover ?
			BehaviorId.MassAttack : Source == StealthRecalculateFleeSource.ApproachUnsafeCurrentPosition ?
			BehaviorId.Approach : BehaviorId.Kite;
		public OwnershipEpoch SourceEpoch { get; }
		public string LiveFingerprint { get; }
		public uint SelectedTargetActorId { get; }
		public CPos SelectedTargetCurrentCell { get; }
		public IReadOnlyList<uint> MemberActorIds => memberIds;
		public IReadOnlyList<uint> EnemyActorIds => enemyIds;
		public bool FormationCloaked { get; }
		public bool PlannedDecloak => true;
		public bool PlannedAttack => true;
		public bool PlannedCurrentRangeEngagement => true;
		public StealthTargetThreatScore StandardScore { get; }

		internal StealthRecalculateFleeEntryEvidence(StealthKiteResult source)
		{
			var evidence = source?.FallbackEvidence;
			if (source == null || source.Handoff == null || source.Handoff.Owner != BehaviorId.Kite ||
				source.Disposition != StealthKiteDisposition.RecalculateFlee ||
				(evidence?.Reason != StealthKiteFallbackReason.NoSafePlan &&
					evidence?.Reason != StealthKiteFallbackReason.UnsafeCurrentPosition) ||
				evidence.AttackFacts == null || !evidence.AttackScore.HasValue ||
				(evidence.Reason == StealthKiteFallbackReason.NoSafePlan &&
					evidence.AttackScore.Value.Crossover > 2) ||
				!source.ActiveMemberActorIds.SequenceEqual(evidence.AttackFacts.FriendlyActorIds) ||
				!source.LiveDefenderActorIds.SequenceEqual(evidence.AttackFacts.EnemyActorIds) ||
				!source.LiveDefenderActorIds.SequenceEqual(evidence.DefenderActorIds) ||
				source.SelectedTargetActorId != evidence.AttackFacts.SelectedTargetActorId ||
				source.SelectedTargetCurrentCell != evidence.AttackFacts.SelectedTargetCurrentCell)
				throw new ArgumentException("RecalculateFlee requires canonical Kite escape evidence.", nameof(source));
			Source = evidence.Reason == StealthKiteFallbackReason.NoSafePlan ?
				StealthRecalculateFleeSource.KiteNoSafePlan :
				StealthRecalculateFleeSource.KiteUnsafeCurrentPosition;
			SourceEpoch = source.Handoff.Epoch;
			LiveFingerprint = evidence.LiveFingerprint;
			SelectedTargetActorId = evidence.AttackFacts.SelectedTargetActorId;
			SelectedTargetCurrentCell = evidence.AttackFacts.SelectedTargetCurrentCell;
			memberIds = CopyIds(evidence.AttackFacts.FriendlyActorIds, nameof(source));
			enemyIds = CopyIds(evidence.AttackFacts.EnemyActorIds, nameof(source));
			FormationCloaked = evidence.AttackFacts.FormationCloaked;
			StandardScore = evidence.AttackScore.Value;
		}

		internal StealthRecalculateFleeEntryEvidence(StealthMassAttackResult source)
		{
			if (source == null || source.Handoff == null || source.Handoff.Owner != BehaviorId.MassAttack ||
				source.Disposition != StealthMassAttackDisposition.RecalculateFlee ||
				source.ThreatFacts == null || !source.Threat.HasValue ||
				source.Threat.Value.StandardScore.Crossover > 1 ||
				!source.ActiveMemberActorIds.SequenceEqual(source.ThreatFacts.FriendlyActorIds) ||
				!source.LiveDefenderActorIds.SequenceEqual(source.ThreatFacts.EnemyActorIds) ||
				source.SelectedTargetActorId != source.ThreatFacts.SelectedTargetActorId ||
				source.SelectedTargetCurrentCell != source.ThreatFacts.SelectedTargetCurrentCell)
				throw new ArgumentException("RecalculateFlee requires canonical MassAttack <=1 crossover evidence.", nameof(source));
			Source = StealthRecalculateFleeSource.MassAttackCrossover;
			SourceEpoch = source.Handoff.Epoch;
			LiveFingerprint = StealthRecalculateFleeFingerprint.FromMassAttack(source.ThreatFacts);
			SelectedTargetActorId = source.ThreatFacts.SelectedTargetActorId;
			SelectedTargetCurrentCell = source.ThreatFacts.SelectedTargetCurrentCell;
			memberIds = CopyIds(source.ThreatFacts.FriendlyActorIds, nameof(source));
			enemyIds = CopyIds(source.ThreatFacts.EnemyActorIds, nameof(source));
			FormationCloaked = source.ThreatFacts.FormationCloaked;
			StandardScore = source.Threat.Value.StandardScore;
		}

		internal StealthRecalculateFleeEntryEvidence(StealthApproachResult source)
		{
			if (source == null || source.Handoff == null || source.Handoff.Owner != BehaviorId.Approach ||
				source.Disposition != StealthApproachDisposition.RecalculateFlee ||
				source.CurrentPositionSafe || !source.ImmediateThreatActorId.HasValue ||
				!source.ImmediateThreatCurrentCell.HasValue || !source.LocalThreatScore.HasValue ||
				!source.LiveDefenderActorIds.Contains(source.ImmediateThreatActorId.Value))
				throw new ArgumentException("RecalculateFlee requires canonical unsafe Approach evidence.", nameof(source));
			Source = StealthRecalculateFleeSource.ApproachUnsafeCurrentPosition;
			SourceEpoch = source.Handoff.Epoch;
			LiveFingerprint = string.Join("|", "approach", string.Join(",", source.ActiveMemberActorIds),
				string.Join(",", source.LiveDefenderActorIds));
			SelectedTargetActorId = source.ImmediateThreatActorId.Value;
			SelectedTargetCurrentCell = source.ImmediateThreatCurrentCell.Value;
			memberIds = CopyIds(source.ActiveMemberActorIds, nameof(source));
			enemyIds = CopyIds(source.LiveDefenderActorIds, nameof(source));
			FormationCloaked = source.FormationCloaked;
			StandardScore = source.LocalThreatScore.Value;
		}

		internal StealthRecalculateFleeEntryEvidence(StealthRecalculateFleeSource source,
			OwnershipEpoch sourceEpoch, string fingerprint, uint targetId, CPos targetCell,
			IEnumerable<uint> members, IEnumerable<uint> enemies, bool formationCloaked,
			StealthTargetThreatScore standardScore)
		{
			if (!Enum.IsDefined(typeof(StealthRecalculateFleeSource), source) ||
				string.IsNullOrEmpty(fingerprint) || targetId == 0 ||
				(source == StealthRecalculateFleeSource.KiteNoSafePlan &&
					standardScore.Crossover > 2) ||
				(source == StealthRecalculateFleeSource.MassAttackCrossover &&
					standardScore.Crossover > 1))
				throw new ArgumentException("Invalid persisted RecalculateFlee entry evidence.");
			Source = source;
			SourceEpoch = sourceEpoch;
			LiveFingerprint = fingerprint;
			SelectedTargetActorId = targetId;
			SelectedTargetCurrentCell = targetCell;
			memberIds = CopyIds(members, nameof(members));
			enemyIds = CopyIds(enemies, nameof(enemies));
			if (!enemyIds.Contains(targetId))
				throw new ArgumentException("Entry enemies must contain the selected target.", nameof(enemies));
			FormationCloaked = formationCloaked;
			StandardScore = standardScore;
		}

		static ReadOnlyCollection<uint> CopyIds(IEnumerable<uint> ids, string name)
		{
			var copy = ids?.OrderBy(id => id).ToArray();
			if (copy == null || copy.Length == 0 || copy.Any(id => id == 0) ||
				copy.Distinct().Count() != copy.Length)
				throw new ArgumentException("Entry identities must be unique and nonzero.", name);
			return Array.AsReadOnly(copy);
		}
	}

	/// <summary>Typed immutable boundary into the disabled Step 5 owner.</summary>
	public sealed class StealthRecalculateFleeHandoff
	{
		internal StealthBehaviorHandoff Handoff { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public StealthApproachMission Mission { get; }
		public StealthRecalculateFleeEntryEvidence Evidence { get; }

		internal StealthRecalculateFleeHandoff(StealthBehaviorHandoff handoff,
			StealthKiteResult source)
		{
			Handoff = RequireHandoff(handoff);
			Mission = source?.Mission ?? throw new ArgumentNullException(nameof(source));
			Evidence = new StealthRecalculateFleeEntryEvidence(source);
			ValidateEpoch();
		}

		internal StealthRecalculateFleeHandoff(StealthBehaviorHandoff handoff,
			StealthMassAttackResult source)
		{
			Handoff = RequireHandoff(handoff);
			Mission = source?.Mission ?? throw new ArgumentNullException(nameof(source));
			Evidence = new StealthRecalculateFleeEntryEvidence(source);
			ValidateEpoch();
		}

		internal StealthRecalculateFleeHandoff(StealthBehaviorHandoff handoff,
			StealthApproachResult source)
		{
			Handoff = RequireHandoff(handoff);
			Mission = source?.Mission ?? throw new ArgumentNullException(nameof(source));
			Evidence = new StealthRecalculateFleeEntryEvidence(source);
			ValidateEpoch();
		}

		internal StealthRecalculateFleeHandoff(StealthBehaviorHandoff handoff,
			StealthApproachMission mission, StealthRecalculateFleeEntryEvidence evidence)
		{
			Handoff = RequireHandoff(handoff);
			Mission = mission ?? throw new ArgumentNullException(nameof(mission));
			Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
			ValidateEpoch();
		}

		void ValidateEpoch()
		{
			if (Evidence.SourceEpoch.Value == long.MaxValue ||
				Handoff.Epoch.Value != Evidence.SourceEpoch.Value + 1)
				throw new ArgumentException("RecalculateFlee requires the immediately preceding source epoch.");
		}

		static StealthBehaviorHandoff RequireHandoff(StealthBehaviorHandoff handoff)
		{
			if (handoff == null || handoff.Owner != BehaviorId.RecalculateFlee)
				throw new ArgumentException("The handoff must belong to RecalculateFlee.", nameof(handoff));
			return handoff;
		}
	}

	public sealed class StealthRecalculateFleeMemberSnapshot
	{
		public uint ActorId { get; }
		public CPos CurrentCell { get; }
		public int CurrentWeaponRangeCells { get; }
		public int HitPoints { get; }
		public int MaximumHitPoints { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }
		public bool NeedsMovementOrder { get; }
		public bool IsValid => IsInWorld && !IsDead &&
			(MaximumHitPoints <= 0 || HitPoints > 0);

		public StealthRecalculateFleeMemberSnapshot(uint actorId, CPos currentCell,
			int currentWeaponRangeCells, int hitPoints = 100, int maximumHitPoints = 100,
			bool isInWorld = true, bool isDead = false, bool needsMovementOrder = false)
		{
			if (actorId == 0 || currentWeaponRangeCells < 0 || hitPoints < 0 || maximumHitPoints < 0)
				throw new ArgumentException("Invalid RecalculateFlee live member.");
			ActorId = actorId;
			CurrentCell = currentCell;
			CurrentWeaponRangeCells = currentWeaponRangeCells;
			HitPoints = hitPoints;
			MaximumHitPoints = maximumHitPoints;
			IsInWorld = isInWorld;
			IsDead = isDead;
			NeedsMovementOrder = needsMovementOrder;
		}
	}

	public sealed class StealthRecalculateFleeEnemySnapshot
	{
		public uint ActorId { get; }
		public string ActorType { get; }
		public CPos CurrentCell { get; }
		public int HitPoints { get; }
		public int MaximumHitPoints { get; }
		public int CurrentWeaponRangeCells { get; }
		public bool HasDetectorCoverage { get; }
		public bool IsInLocalEngagementArea { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }
		public bool IsTargetable { get; }
		public bool IsValid => IsInWorld && !IsDead && IsTargetable &&
			(MaximumHitPoints <= 0 || HitPoints > 0);

		public StealthRecalculateFleeEnemySnapshot(uint actorId, string actorType, CPos currentCell,
			int hitPoints, int maximumHitPoints, int currentWeaponRangeCells,
			bool hasDetectorCoverage, bool isInLocalEngagementArea = true,
			bool isInWorld = true, bool isDead = false, bool isTargetable = true)
		{
			if (actorId == 0 || string.IsNullOrWhiteSpace(actorType) || hitPoints < 0 ||
				maximumHitPoints < 0 || currentWeaponRangeCells < 0)
				throw new ArgumentException("Invalid RecalculateFlee live enemy.");
			ActorId = actorId;
			ActorType = actorType.ToLowerInvariant();
			CurrentCell = currentCell;
			HitPoints = hitPoints;
			MaximumHitPoints = maximumHitPoints;
			CurrentWeaponRangeCells = currentWeaponRangeCells;
			HasDetectorCoverage = hasDetectorCoverage;
			IsInLocalEngagementArea = isInLocalEngagementArea;
			IsInWorld = isInWorld;
			IsDead = isDead;
			IsTargetable = isTargetable;
		}
	}

	public sealed class StealthRecalculateFleeCandidateSnapshot
	{
		public CPos Cell { get; }
		public bool IsPassable { get; }
		public bool RequiresStrategicRouting { get; }
		public bool HasDetectorCoverage { get; }
		public StealthRecalculateFleeCandidateSnapshot(CPos cell, bool isPassable,
			bool requiresStrategicRouting = false, bool hasDetectorCoverage = false)
		{
			Cell = cell;
			IsPassable = isPassable;
			RequiresStrategicRouting = requiresStrategicRouting;
			HasDetectorCoverage = hasDetectorCoverage;
		}
	}

	public sealed class StealthRecalculateFleeLiveSnapshot
	{
		readonly ReadOnlyCollection<StealthRecalculateFleeMemberSnapshot> members;
		readonly ReadOnlyCollection<StealthRecalculateFleeEnemySnapshot> enemies;
		readonly ReadOnlyCollection<StealthRecalculateFleeCandidateSnapshot> candidates;
		public int Tick { get; }
		public IReadOnlyList<StealthRecalculateFleeMemberSnapshot> Members => members;
		public IReadOnlyList<StealthRecalculateFleeEnemySnapshot> Enemies => enemies;
		public IReadOnlyList<StealthRecalculateFleeCandidateSnapshot> Candidates => candidates;
		public bool FormationCloaked { get; }
		public string SourceFingerprint { get; }
		public bool HasActivityObservation { get; }
		public long ActivityRevision { get; }
		public StealthRecalculateFleeOrderToken ActiveOrderToken { get; }
		public StealthRecalculateFleeOrderToken CompletedOrderToken { get; }

		public StealthRecalculateFleeLiveSnapshot(int tick,
			IEnumerable<StealthRecalculateFleeMemberSnapshot> members,
			IEnumerable<StealthRecalculateFleeEnemySnapshot> enemies,
			IEnumerable<StealthRecalculateFleeCandidateSnapshot> candidates,
			bool formationCloaked, string sourceFingerprint,
			bool hasActivityObservation = false, long activityRevision = 0,
			StealthRecalculateFleeOrderToken activeOrderToken = null,
			StealthRecalculateFleeOrderToken completedOrderToken = null)
		{
			if (tick < 0 || activityRevision < 0 || members == null || enemies == null || candidates == null ||
				string.IsNullOrEmpty(sourceFingerprint))
				throw new ArgumentException("Invalid RecalculateFlee live snapshot.");
			var memberCopy = members.OrderBy(member => member?.ActorId).ToArray();
			var enemyCopy = enemies.OrderBy(enemy => enemy?.ActorId).ToArray();
			var candidateCopy = candidates.OrderBy(candidate => candidate?.Cell.Y)
				.ThenBy(candidate => candidate?.Cell.X).ToArray();
			if (memberCopy.Length == 0 || memberCopy.Any(member => member == null) ||
				memberCopy.Select(member => member.ActorId).Distinct().Count() != memberCopy.Length ||
				enemyCopy.Any(enemy => enemy == null) ||
				enemyCopy.Select(enemy => enemy.ActorId).Distinct().Count() != enemyCopy.Length ||
				candidateCopy.Any(candidate => candidate == null) ||
				candidateCopy.Select(candidate => candidate.Cell).Distinct().Count() != candidateCopy.Length ||
				(!hasActivityObservation && (activityRevision != 0 || activeOrderToken != null || completedOrderToken != null)) ||
				(activeOrderToken != null && activeOrderToken.ActivityRevision != activityRevision) ||
				(completedOrderToken != null && completedOrderToken.ActivityRevision > activityRevision))
				throw new ArgumentException("Noncanonical RecalculateFlee live snapshot.");
			Tick = tick;
			this.members = Array.AsReadOnly(memberCopy);
			this.enemies = Array.AsReadOnly(enemyCopy);
			this.candidates = Array.AsReadOnly(candidateCopy);
			FormationCloaked = formationCloaked;
			SourceFingerprint = sourceFingerprint;
			HasActivityObservation = hasActivityObservation;
			ActivityRevision = activityRevision;
			ActiveOrderToken = activeOrderToken;
			CompletedOrderToken = completedOrderToken;
		}
	}

	public interface IStealthRecalculateFleeLiveWorld
	{
		StealthRecalculateFleeLiveSnapshot Read(StealthApproachMission mission);
	}

	public sealed class StealthRecalculateFleeStrategicCacheSnapshot
	{
		readonly ReadOnlyCollection<CPos> waypoints;
		public long Revision { get; }
		public IReadOnlyList<CPos> Waypoints => waypoints;
		public StealthRecalculateFleeStrategicCacheSnapshot(long revision, IEnumerable<CPos> waypoints)
		{
			if (revision < 0 || waypoints == null)
				throw new ArgumentException("Invalid long-route cache snapshot.");
			Revision = revision;
			this.waypoints = Array.AsReadOnly(waypoints.ToArray());
		}
	}

	public interface IStealthRecalculateFleeStrategicCache
	{
		StealthRecalculateFleeStrategicCacheSnapshot ReadLongRoute(
			StealthApproachMission mission, CPos liveDestination);
	}
}
