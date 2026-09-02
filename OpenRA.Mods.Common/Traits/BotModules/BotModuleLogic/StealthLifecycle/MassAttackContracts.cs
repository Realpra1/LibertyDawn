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
	public enum StealthMassAttackPhase { Advance, Attack }
	public enum StealthMassAttackDisposition
	{
		Retain,
		UndefendedAttack,
		Reacquire,
		RecalculateFlee,
		StrategicRecalculation
	}

	enum StealthMassAttackEntryState { Pristine, Validated, SkippedZeroMembers, ExitedRecalculate }

	public sealed class StealthMassAttackEntryEvidence
	{
		readonly ReadOnlyCollection<uint> friendlyActorIds;
		readonly ReadOnlyCollection<uint> enemyActorIds;
		public StealthKiteFallbackReason Reason => StealthKiteFallbackReason.NoSafePlan;
		public string LiveFingerprint { get; }
		public uint SelectedTargetActorId { get; }
		public CPos SelectedTargetCurrentCell { get; }
		public IReadOnlyList<uint> FriendlyActorIds => friendlyActorIds;
		public IReadOnlyList<uint> EnemyActorIds => enemyActorIds;
		public bool FormationCloaked { get; }
		public bool PlannedReveal => true;
		public bool PlannedAttack => true;
		public bool FullCurrentFiringRangeExposure => true;
		public StealthTargetThreatScore StandardScore { get; }

		internal StealthMassAttackEntryEvidence(StealthKiteFallbackEvidence source)
		{
			if (source?.Reason != StealthKiteFallbackReason.NoSafePlan ||
				source.AttackFacts == null || !source.AttackScore.HasValue ||
				source.AttackScore.Value.Crossover <= 2)
				throw new ArgumentException("MassAttack requires canonical >2 Kite no-safe-plan evidence.", nameof(source));
			LiveFingerprint = source.LiveFingerprint;
			SelectedTargetActorId = source.AttackFacts.SelectedTargetActorId;
			SelectedTargetCurrentCell = source.AttackFacts.SelectedTargetCurrentCell;
			friendlyActorIds = CopyIds(source.AttackFacts.FriendlyActorIds, nameof(source));
			enemyActorIds = CopyIds(source.AttackFacts.EnemyActorIds, nameof(source));
			if (!enemyActorIds.SequenceEqual(source.DefenderActorIds) ||
				!enemyActorIds.Contains(SelectedTargetActorId) ||
				!source.AttackFacts.PlannedDecloak || !source.AttackFacts.PlannedAttack ||
				!source.AttackFacts.PlannedCurrentRangeEngagement)
				throw new ArgumentException("Kite evidence is not an exact planned live attack.", nameof(source));
			FormationCloaked = source.AttackFacts.FormationCloaked;
			StandardScore = source.AttackScore.Value;
		}

		internal StealthMassAttackEntryEvidence(string fingerprint, uint targetId,
			CPos targetCell, IEnumerable<uint> friendIds, IEnumerable<uint> enemyIds,
			bool formationCloaked, StealthTargetThreatScore score)
		{
			if (string.IsNullOrEmpty(fingerprint) || targetId == 0 || score.Crossover <= 2)
				throw new ArgumentException("Invalid persisted MassAttack entry evidence.");
			LiveFingerprint = fingerprint;
			SelectedTargetActorId = targetId;
			SelectedTargetCurrentCell = targetCell;
			friendlyActorIds = CopyIds(friendIds, nameof(friendIds));
			enemyActorIds = CopyIds(enemyIds, nameof(enemyIds));
			if (!enemyActorIds.Contains(targetId))
				throw new ArgumentException("MassAttack evidence enemies must contain its target.");
			FormationCloaked = formationCloaked;
			StandardScore = score;
		}

		static ReadOnlyCollection<uint> CopyIds(IEnumerable<uint> ids, string name)
		{
			var copy = ids?.OrderBy(id => id).ToArray();
			if (copy == null || copy.Length == 0 || copy.Any(id => id == 0) ||
				copy.Distinct().Count() != copy.Length)
				throw new ArgumentException("MassAttack evidence identities must be unique and nonzero.", name);
			return Array.AsReadOnly(copy);
		}
	}

	public sealed class StealthMassAttackHandoff
	{
		internal StealthBehaviorHandoff Handoff { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public StealthApproachMission Mission { get; }
		public StealthMassAttackEntryEvidence Evidence { get; }

		internal StealthMassAttackHandoff(StealthBehaviorHandoff handoff,
			StealthApproachMission mission, StealthKiteFallbackEvidence evidence)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.MassAttack)
				throw new ArgumentException("The handoff must belong to MassAttack.", nameof(handoff));
			Mission = mission ?? throw new ArgumentNullException(nameof(mission));
			Evidence = new StealthMassAttackEntryEvidence(evidence);
		}

		internal StealthMassAttackHandoff(StealthBehaviorHandoff handoff,
			StealthApproachMission mission, StealthMassAttackEntryEvidence evidence)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.MassAttack)
				throw new ArgumentException("The handoff must belong to MassAttack.", nameof(handoff));
			Mission = mission ?? throw new ArgumentNullException(nameof(mission));
			Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
		}
	}

	public sealed class StealthMassAttackMemberSnapshot
	{
		public uint ActorId { get; }
		public CPos CurrentCell { get; }
		public int CurrentWeaponRangeCells { get; }
		public int HitPoints { get; }
		public int MaximumHitPoints { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }
		public bool NeedsMovementOrder { get; }
		public bool IsValid => IsInWorld && !IsDead && (MaximumHitPoints <= 0 || HitPoints > 0);

		public StealthMassAttackMemberSnapshot(uint actorId, CPos currentCell,
			int currentWeaponRangeCells, int hitPoints = 100, int maximumHitPoints = 100,
			bool isInWorld = true, bool isDead = false, bool needsMovementOrder = false)
		{
			if (actorId == 0 || currentWeaponRangeCells < 0 || hitPoints < 0 || maximumHitPoints < 0)
				throw new ArgumentOutOfRangeException(nameof(actorId));
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

	public sealed class StealthMassAttackActorSnapshot
	{
		public uint ActorId { get; }
		public string ActorType { get; }
		public CPos CurrentCell { get; }
		public int HitPoints { get; }
		public int MaximumHitPoints { get; }
		public int CurrentWeaponRangeCells { get; }
		public bool IsInLocalEngagementArea { get; }
		public bool IsDefender { get; }
		public bool IsMissionObjective { get; }
		public bool HasDetectorCoverage { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }
		public bool IsTargetable { get; }
		public bool IsValid => IsInWorld && !IsDead && IsTargetable &&
			(MaximumHitPoints <= 0 || HitPoints > 0);

		public StealthMassAttackActorSnapshot(uint actorId, string actorType, CPos currentCell,
			int hitPoints, int maximumHitPoints, int currentWeaponRangeCells,
			bool isDefender, bool isMissionObjective, bool hasDetectorCoverage,
			bool isInLocalEngagementArea = true, bool isInWorld = true,
			bool isDead = false, bool isTargetable = true)
		{
			if (actorId == 0 || string.IsNullOrWhiteSpace(actorType) || hitPoints < 0 ||
				maximumHitPoints < 0 || currentWeaponRangeCells < 0)
				throw new ArgumentException("Invalid MassAttack live actor.");
			ActorId = actorId;
			ActorType = actorType.ToLowerInvariant();
			CurrentCell = currentCell;
			HitPoints = hitPoints;
			MaximumHitPoints = maximumHitPoints;
			CurrentWeaponRangeCells = currentWeaponRangeCells;
			IsDefender = isDefender;
			IsMissionObjective = isMissionObjective;
			HasDetectorCoverage = hasDetectorCoverage;
			IsInLocalEngagementArea = isInLocalEngagementArea;
			IsInWorld = isInWorld;
			IsDead = isDead;
			IsTargetable = isTargetable;
		}
	}

	public sealed class StealthMassAttackLiveSnapshot
	{
		readonly ReadOnlyCollection<StealthMassAttackMemberSnapshot> members;
		readonly ReadOnlyCollection<StealthMassAttackActorSnapshot> actors;
		readonly ReadOnlyCollection<CPos> candidateCells;
		public int Tick { get; }
		public IReadOnlyList<StealthMassAttackMemberSnapshot> Members => members;
		public IReadOnlyList<StealthMassAttackActorSnapshot> Actors => actors;
		public IReadOnlyList<CPos> CandidateCells => candidateCells;
		public bool FormationCloaked { get; }
		public bool HasActivityObservation { get; }
		public long ActivityRevision { get; }
		public StealthMassAttackOrderToken ActiveOrderToken { get; }
		public StealthMassAttackOrderToken CompletedOrderToken { get; }

		public StealthMassAttackLiveSnapshot(int tick,
			IEnumerable<StealthMassAttackMemberSnapshot> members,
			IEnumerable<StealthMassAttackActorSnapshot> actors, IEnumerable<CPos> candidateCells,
			bool formationCloaked, bool hasActivityObservation = false,
			long activityRevision = 0, StealthMassAttackOrderToken activeOrderToken = null,
			StealthMassAttackOrderToken completedOrderToken = null)
		{
			if (tick < 0 || activityRevision < 0 || members == null || actors == null || candidateCells == null)
				throw new ArgumentException("Invalid MassAttack live snapshot.");
			var memberCopy = members.OrderBy(member => member?.ActorId).ToArray();
			var actorCopy = actors.OrderBy(actor => actor?.ActorId).ToArray();
			var cells = candidateCells.Distinct().OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
			if (memberCopy.Length == 0 || memberCopy.Any(member => member == null) ||
				memberCopy.Select(member => member.ActorId).Distinct().Count() != memberCopy.Length ||
				actorCopy.Any(actor => actor == null) ||
				actorCopy.Select(actor => actor.ActorId).Distinct().Count() != actorCopy.Length ||
				(!hasActivityObservation && (activityRevision != 0 || activeOrderToken != null ||
					completedOrderToken != null)) ||
				(activeOrderToken != null && activeOrderToken.ActivityRevision != activityRevision) ||
				(completedOrderToken != null && completedOrderToken.ActivityRevision > activityRevision))
				throw new ArgumentException("MassAttack live snapshot is noncanonical.");
			Tick = tick;
			this.members = Array.AsReadOnly(memberCopy);
			this.actors = Array.AsReadOnly(actorCopy);
			this.candidateCells = Array.AsReadOnly(cells);
			FormationCloaked = formationCloaked;
			HasActivityObservation = hasActivityObservation;
			ActivityRevision = activityRevision;
			ActiveOrderToken = activeOrderToken;
			CompletedOrderToken = completedOrderToken;
		}
	}

	public interface IStealthMassAttackLiveWorld
	{
		StealthMassAttackLiveSnapshot Read(StealthApproachMission mission);
	}

	public sealed class StealthMassAttackThreatFacts
	{
		readonly ReadOnlyCollection<uint> friendlyActorIds;
		readonly ReadOnlyCollection<uint> enemyActorIds;
		readonly ReadOnlyCollection<StealthMassAttackActorSnapshot> enemies;
		public uint SelectedTargetActorId { get; }
		public CPos SelectedTargetCurrentCell { get; }
		public CPos PlannedCell { get; }
		public int FormationRadiusCells { get; }
		public IReadOnlyList<uint> FriendlyActorIds => friendlyActorIds;
		public IReadOnlyList<uint> EnemyActorIds => enemyActorIds;
		public IReadOnlyList<StealthMassAttackActorSnapshot> Enemies => enemies;
		public bool FormationCloaked { get; }
		public bool HasDetectorCoverage => enemies.Any(enemy => enemy.HasDetectorCoverage);
		public bool PlannedReveal => true;
		public bool PlannedAttack => true;
		public bool FullCurrentFiringRangeExposure => true;

		public StealthMassAttackThreatFacts(uint targetId, CPos targetCell, CPos plannedCell,
			IEnumerable<uint> friendIds, IEnumerable<StealthMassAttackActorSnapshot> enemies,
			bool formationCloaked, int formationRadiusCells = 0)
		{
			if (targetId == 0 || friendIds == null || enemies == null || formationRadiusCells < 0)
				throw new ArgumentException("Invalid MassAttack threat facts.");
			var friends = friendIds.OrderBy(id => id).ToArray();
			var enemyCopy = enemies.OrderBy(enemy => enemy?.ActorId).ToArray();
			if (friends.Length == 0 || friends.Any(id => id == 0) ||
				friends.Distinct().Count() != friends.Length || enemyCopy.Length == 0 ||
				enemyCopy.Any(enemy => enemy == null) ||
				enemyCopy.Select(enemy => enemy.ActorId).Distinct().Count() != enemyCopy.Length ||
				!enemyCopy.Any(enemy => enemy.ActorId == targetId && enemy.CurrentCell == targetCell))
				throw new ArgumentException("MassAttack facts require exact live participants and target.");
			SelectedTargetActorId = targetId;
			SelectedTargetCurrentCell = targetCell;
			PlannedCell = plannedCell;
			FormationRadiusCells = formationRadiusCells;
			friendlyActorIds = Array.AsReadOnly(friends);
			this.enemies = Array.AsReadOnly(enemyCopy);
			enemyActorIds = Array.AsReadOnly(enemyCopy.Select(enemy => enemy.ActorId).ToArray());
			FormationCloaked = formationCloaked;
		}
	}

	public readonly struct StealthMassAttackThreatResult
	{
		public StealthTargetThreatScore StandardScore { get; }
		public double SelectedTargetThreat { get; }
		public bool AttackApproved { get; }
		public StealthMassAttackThreatResult(StealthTargetThreatScore score,
			double selectedTargetThreat, bool attackApproved)
		{
			if (double.IsNaN(selectedTargetThreat) || double.IsInfinity(selectedTargetThreat) ||
				selectedTargetThreat < 0)
				throw new ArgumentOutOfRangeException(nameof(selectedTargetThreat));
			StandardScore = score;
			SelectedTargetThreat = selectedTargetThreat;
			AttackApproved = attackApproved;
		}
	}

	public interface IStealthMassAttackThreatAdapter
	{
		IStealthMassAttackThreatEvaluation Begin(StealthMassAttackThreatFacts facts);
	}

	/// <summary>One immutable live combat snapshot shared by a single MassAttack decision.</summary>
	public interface IStealthMassAttackThreatEvaluation
	{
		StealthMassAttackThreatResult Calculate(StealthMassAttackThreatFacts facts);
	}

	public interface IStealthMassAttackOrders
	{
		void IssueMove(BehaviorId owner, OwnershipEpoch epoch, IReadOnlyList<uint> actorIds,
			uint targetActorId, CPos destinationCell, StealthMassAttackOrderToken token);
		void IssueAttack(BehaviorId owner, OwnershipEpoch epoch, IReadOnlyList<uint> actorIds,
			uint targetActorId, CPos targetCurrentCell, StealthMassAttackOrderToken token);
	}
}
