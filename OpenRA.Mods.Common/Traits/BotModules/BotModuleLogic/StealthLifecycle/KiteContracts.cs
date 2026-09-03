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
	public enum StealthKitePhase { Position, Fire }
	public enum StealthKiteAction { Position, Fire }
	public enum StealthKiteDisposition
	{
		Retain,
		CrushEvaluation,
		UndefendedAttack,
		Reacquire,
		MassAttack,
		RecalculateFlee
	}

	public sealed class StealthKiteMemberSnapshot
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

		public StealthKiteMemberSnapshot(uint actorId, CPos currentCell,
			int currentWeaponRangeCells, bool isInWorld = true, bool isDead = false,
			int hitPoints = 100, int maximumHitPoints = 100,
			bool needsMovementOrder = false)
		{
			if (actorId == 0 || currentWeaponRangeCells < 0 || hitPoints < 0 || maximumHitPoints < 0)
				throw new ArgumentOutOfRangeException(actorId == 0 ? nameof(actorId) : nameof(currentWeaponRangeCells));
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

	public sealed class StealthKiteActorSnapshot
	{
		public uint ActorId { get; }
		public string ActorType { get; }
		public CPos CurrentCell { get; }
		public int HitPoints { get; }
		public int MaximumHitPoints { get; }
		public int CurrentWeaponRangeCells { get; }
		public long PriorityValue { get; }
		public bool IsInLocalEngagementArea { get; }
		public bool IsDefender { get; }
		public bool IsMissionObjective { get; }
		public bool IsInfantry { get; }
		public bool CanBeCrushedByFormation { get; }
		public bool HasDetectorCoverage { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }
		public bool IsTargetable { get; }
		public bool IsValid => IsInWorld && !IsDead && IsTargetable &&
			(MaximumHitPoints <= 0 || HitPoints > 0);

		public StealthKiteActorSnapshot(uint actorId, string actorType, CPos currentCell,
			int hitPoints, int maximumHitPoints, int currentWeaponRangeCells,
			bool isDefender, bool isMissionObjective, bool isInfantry,
			bool canBeCrushedByFormation, bool hasDetectorCoverage,
			bool isInLocalEngagementArea = true, bool isInWorld = true,
			bool isDead = false, bool isTargetable = true, long priorityValue = 0)
		{
			if (actorId == 0)
				throw new ArgumentOutOfRangeException(nameof(actorId));
			if (string.IsNullOrWhiteSpace(actorType))
				throw new ArgumentException("Actor types must be non-empty.", nameof(actorType));
			if (hitPoints < 0 || maximumHitPoints < 0 || currentWeaponRangeCells < 0)
				throw new ArgumentOutOfRangeException(nameof(hitPoints));
			ActorId = actorId;
			ActorType = actorType.ToLowerInvariant();
			CurrentCell = currentCell;
			HitPoints = hitPoints;
			MaximumHitPoints = maximumHitPoints;
			CurrentWeaponRangeCells = currentWeaponRangeCells;
			PriorityValue = priorityValue;
			IsInLocalEngagementArea = isInLocalEngagementArea;
			IsDefender = isDefender;
			IsMissionObjective = isMissionObjective;
			IsInfantry = isInfantry;
			CanBeCrushedByFormation = canBeCrushedByFormation;
			HasDetectorCoverage = hasDetectorCoverage;
			IsInWorld = isInWorld;
			IsDead = isDead;
			IsTargetable = isTargetable;
		}
	}

	public sealed class StealthKiteLiveSnapshot
	{
		readonly ReadOnlyCollection<StealthKiteMemberSnapshot> members;
		readonly ReadOnlyCollection<StealthKiteActorSnapshot> actors;
		readonly ReadOnlyCollection<CPos> candidateCells;
		public int Tick { get; }
		public IReadOnlyList<StealthKiteMemberSnapshot> Members => members;
		public IReadOnlyList<StealthKiteActorSnapshot> Actors => actors;
		public IReadOnlyList<CPos> CandidateCells => candidateCells;
		public bool FormationCloaked { get; }
		public bool FormationDetected { get; }
		public bool KitingEnabled { get; }
		public long MinimumKitePriorityValue { get; }
		public bool HasActivityObservation { get; }
		public long ActivityRevision { get; }
		public StealthKiteOrderToken ActiveOrderToken { get; }
		public StealthKiteOrderToken CompletedOrderToken { get; }

		public StealthKiteLiveSnapshot(int tick, IEnumerable<StealthKiteMemberSnapshot> members,
			IEnumerable<StealthKiteActorSnapshot> actors, IEnumerable<CPos> candidateCells,
			bool formationCloaked, bool hasActivityObservation = false,
			long activityRevision = 0, StealthKiteOrderToken activeOrderToken = null,
			StealthKiteOrderToken completedOrderToken = null,
			bool formationDetected = false, bool kitingEnabled = true,
			long minimumKitePriorityValue = 0)
		{
			if (tick < 0 || activityRevision < 0 || minimumKitePriorityValue < 0)
				throw new ArgumentOutOfRangeException(nameof(tick));
			if ((!hasActivityObservation && (activityRevision != 0 || activeOrderToken != null ||
					completedOrderToken != null)) ||
				(activeOrderToken != null && activeOrderToken.ActivityRevision != activityRevision))
				throw new ArgumentException("Kite activity observations must be canonical and current.");
			if (members == null || actors == null || candidateCells == null)
				throw new ArgumentNullException(members == null ? nameof(members) :
					actors == null ? nameof(actors) : nameof(candidateCells));
			var normalizedMembers = members.OrderBy(member => member?.ActorId).ToArray();
			var normalizedActors = actors.OrderBy(actor => actor?.ActorId).ToArray();
			var cells = candidateCells.Distinct().OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
			if (normalizedMembers.Length == 0 || normalizedMembers.Any(member => member == null) ||
				normalizedMembers.Select(member => member.ActorId).Distinct().Count() != normalizedMembers.Length)
				throw new ArgumentException("Live Kite members must have unique identities.", nameof(members));
			if (normalizedActors.Any(actor => actor == null) || normalizedActors.Select(actor => actor.ActorId)
				.Distinct().Count() != normalizedActors.Length)
				throw new ArgumentException("Live Kite actors must have unique identities.", nameof(actors));
			Tick = tick;
			this.members = Array.AsReadOnly(normalizedMembers);
			this.actors = Array.AsReadOnly(normalizedActors);
			this.candidateCells = Array.AsReadOnly(cells);
			FormationCloaked = formationCloaked;
			FormationDetected = formationDetected;
			KitingEnabled = kitingEnabled;
			MinimumKitePriorityValue = minimumKitePriorityValue;
			HasActivityObservation = hasActivityObservation;
			ActivityRevision = activityRevision;
			ActiveOrderToken = activeOrderToken;
			CompletedOrderToken = completedOrderToken;
		}
	}

	public interface IStealthKiteLiveWorld
	{
		StealthKiteLiveSnapshot Read(StealthApproachMission mission);
	}

	public sealed class StealthKiteThreatFacts
	{
		readonly ReadOnlyCollection<uint> friendlyActorIds;
		readonly ReadOnlyCollection<uint> enemyActorIds;
		readonly ReadOnlyCollection<StealthKiteActorSnapshot> enemies;
		public StealthKiteAction Action { get; }
		public uint SelectedTargetActorId { get; }
		public CPos SelectedTargetCurrentCell { get; }
		public CPos PlannedCell { get; }
		public int FriendlyCurrentFiringRangeCells { get; }
		public int FormationRadiusCells { get; }
		public IReadOnlyList<uint> FriendlyActorIds => friendlyActorIds;
		public IReadOnlyList<uint> EnemyActorIds => enemyActorIds;
		public IReadOnlyList<StealthKiteActorSnapshot> Enemies => enemies;
		public bool FormationCloaked { get; }
		public bool PlannedDecloak { get; }
		public bool PlannedAttack { get; }
		public bool PlannedCurrentRangeEngagement => PlannedAttack;

		public StealthKiteThreatFacts(StealthKiteAction action, uint selectedTargetActorId,
			CPos selectedTargetCurrentCell, CPos plannedCell, int friendlyCurrentFiringRangeCells,
			IEnumerable<uint> friendlyActorIds, IEnumerable<StealthKiteActorSnapshot> enemies,
			bool formationCloaked, bool plannedDecloak, bool plannedAttack,
			int formationRadiusCells = 0)
		{
			if (!Enum.IsDefined(typeof(StealthKiteAction), action) || selectedTargetActorId == 0 ||
				friendlyCurrentFiringRangeCells < 0 || formationRadiusCells < 0)
				throw new ArgumentOutOfRangeException(nameof(action));
			var friendIds = Normalize(friendlyActorIds, nameof(friendlyActorIds));
			if (enemies == null)
				throw new ArgumentNullException(nameof(enemies));
			var enemyActors = enemies.OrderBy(enemy => enemy?.ActorId).ToArray();
			if (enemyActors.Length == 0 || enemyActors.Any(enemy => enemy == null) ||
				enemyActors.Select(enemy => enemy.ActorId).Distinct().Count() != enemyActors.Length ||
				!enemyActors.Any(enemy => enemy.ActorId == selectedTargetActorId))
				throw new ArgumentException("Kite threat enemies must be unique and contain the target.", nameof(enemies));
			Action = action;
			SelectedTargetActorId = selectedTargetActorId;
			SelectedTargetCurrentCell = selectedTargetCurrentCell;
			PlannedCell = plannedCell;
			FriendlyCurrentFiringRangeCells = friendlyCurrentFiringRangeCells;
			FormationRadiusCells = formationRadiusCells;
			this.friendlyActorIds = friendIds;
			this.enemies = Array.AsReadOnly(enemyActors);
			enemyActorIds = Array.AsReadOnly(enemyActors.Select(enemy => enemy.ActorId).ToArray());
			FormationCloaked = formationCloaked;
			PlannedDecloak = plannedDecloak;
			PlannedAttack = plannedAttack;
		}

		static ReadOnlyCollection<uint> Normalize(IEnumerable<uint> ids, string name)
		{
			if (ids == null)
				throw new ArgumentNullException(name);
			var result = ids.OrderBy(id => id).ToArray();
			if (result.Length == 0 || result.Any(id => id == 0) || result.Distinct().Count() != result.Length)
				throw new ArgumentException("Live actor identities must be unique and nonzero.", name);
			return Array.AsReadOnly(result);
		}
	}

	public readonly struct StealthKiteSafetyResult
	{
		public StealthTargetThreatScore Score { get; }
		public bool Approved { get; }
		public StealthKiteSafetyResult(StealthTargetThreatScore score, bool approved)
		{
			Score = score;
			Approved = approved;
		}
	}

	public interface IStealthKiteThreatAdapter
	{
		StealthKiteSafetyResult Calculate(StealthKiteThreatFacts facts);
		StealthTargetThreatScore CalculateAttackCrossover(StealthKiteFallbackFacts facts);
	}

	/// <summary>
	/// Applies Kite orders using the token as an external idempotency key. Repeated calls with an
	/// equal token must not create a second external order, including after a prior callback threw.
	/// </summary>
	public interface IStealthKiteOrders
	{
		void IssueMove(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, CPos cell, StealthKiteOrderToken token);
		void IssueAttack(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, uint targetActorId, CPos targetCurrentCell,
			StealthKiteOrderToken token);
	}

	public sealed class StealthKiteResult
	{
		readonly ReadOnlyCollection<uint> memberIds;
		readonly ReadOnlyCollection<uint> defenderIds;
		readonly ReadOnlyCollection<uint> objectiveIds;
		internal StealthBehaviorHandoff Handoff { get; }
		public StealthApproachMission Mission { get; }
		public StealthKiteDisposition Disposition { get; }
		public StealthKitePhase Phase { get; }
		public uint? SelectedTargetActorId { get; }
		public CPos? SelectedTargetCurrentCell { get; }
		public CPos? FireCell { get; }
		public IReadOnlyList<uint> ActiveMemberActorIds => memberIds;
		public IReadOnlyList<uint> LiveDefenderActorIds => defenderIds;
		public IReadOnlyList<uint> LiveObjectiveActorIds => objectiveIds;
		public StealthKiteSafetyResult? Safety { get; }
		public StealthKiteFallbackEvidence FallbackEvidence { get; }

		internal StealthKiteResult(StealthBehaviorHandoff handoff, StealthApproachMission mission,
			StealthKiteDisposition disposition, StealthKitePhase phase, uint? selectedTargetActorId,
			CPos? selectedTargetCurrentCell, CPos? fireCell,
			IEnumerable<uint> members, IEnumerable<uint> defenders, IEnumerable<uint> objectives,
			StealthKiteSafetyResult? safety, StealthKiteFallbackEvidence fallbackEvidence)
		{
			Handoff = handoff;
			Mission = mission;
			Disposition = disposition;
			Phase = phase;
			SelectedTargetActorId = selectedTargetActorId;
			SelectedTargetCurrentCell = selectedTargetCurrentCell;
			FireCell = fireCell;
			memberIds = Array.AsReadOnly(members.ToArray());
			defenderIds = Array.AsReadOnly(defenders.ToArray());
			objectiveIds = Array.AsReadOnly(objectives.ToArray());
			Safety = safety;
			FallbackEvidence = fallbackEvidence;
		}
	}

	public sealed class StealthKiteTransition
	{
		public StealthBehaviorHandoff Retained { get; }
		public StealthCrushEvaluationHandoff CrushEvaluation { get; }
		public StealthUndefendedAttackHandoff UndefendedAttack { get; }
		public StealthBehaviorHandoff Reacquisition { get; }
		public StealthBehaviorHandoff MassAttack { get; }
		public StealthMassAttackHandoff MassAttackEntry { get; }
		public StealthBehaviorHandoff RecalculateFlee { get; }
		public StealthRecalculateFleeHandoff RecalculateFleeEntry { get; }
		public StealthBehaviorHandoff SquadConstruction { get; }
		public StealthSquadConstructionRecoveryHandoff SquadConstructionEntry { get; }

		internal StealthKiteTransition(StealthBehaviorHandoff handoff, StealthKiteResult result)
		{
			if (result.Disposition == StealthKiteDisposition.Retain)
				Retained = handoff;
			else if (result.Disposition == StealthKiteDisposition.CrushEvaluation)
				CrushEvaluation = new StealthCrushEvaluationHandoff(handoff,
					result.Mission, result.LiveDefenderActorIds);
			else if (result.Disposition == StealthKiteDisposition.UndefendedAttack)
				UndefendedAttack = new StealthUndefendedAttackHandoff(handoff, result.Mission);
			else if (result.Disposition == StealthKiteDisposition.Reacquire)
				Reacquisition = handoff;
			else if (result.Disposition == StealthKiteDisposition.MassAttack)
			{
				MassAttack = handoff;
				MassAttackEntry = new StealthMassAttackHandoff(
					handoff, result.Mission, result.FallbackEvidence);
			}
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
