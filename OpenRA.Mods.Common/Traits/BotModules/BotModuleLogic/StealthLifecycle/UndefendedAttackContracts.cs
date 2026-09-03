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
	public enum StealthUndefendedAttackDisposition
	{
		Retain,
		Reacquire,
		CrushEvaluation
	}

	public sealed class StealthUndefendedAttackMemberSnapshot
	{
		public uint ActorId { get; }
		public string ActorType { get; }
		public int EconomicValue { get; }
		public CPos CurrentCell { get; }
		public int HitPoints { get; }
		public int MaximumHitPoints { get; }
		public int CurrentWeaponRangeCells { get; }
		public bool NeedsAttackOrder { get; }

		public StealthUndefendedAttackMemberSnapshot(uint actorId, string actorType,
			int economicValue, CPos currentCell, int hitPoints, int maximumHitPoints,
			int currentWeaponRangeCells, bool needsAttackOrder = false)
		{
			if (actorId == 0)
				throw new ArgumentOutOfRangeException(nameof(actorId));
			if (string.IsNullOrWhiteSpace(actorType))
				throw new ArgumentException("Actor types must be non-empty.", nameof(actorType));
			if (economicValue < 0 || hitPoints < 0 || maximumHitPoints < 0 || currentWeaponRangeCells < 0)
				throw new ArgumentOutOfRangeException(nameof(economicValue));

			ActorId = actorId;
			ActorType = actorType.ToLowerInvariant();
			EconomicValue = economicValue;
			CurrentCell = currentCell;
			HitPoints = hitPoints;
			MaximumHitPoints = maximumHitPoints;
			CurrentWeaponRangeCells = currentWeaponRangeCells;
			NeedsAttackOrder = needsAttackOrder;
		}
	}

	public sealed class StealthUndefendedAttackTargetSnapshot
	{
		public uint ActorId { get; }
		public string ActorType { get; }
		public CPos StrategicCell { get; }
		public CPos CurrentCell { get; }
		public int ConfiguredPriority { get; }
		public int ActorValue { get; }
		public int HitPoints { get; }
		public int MaximumHitPoints { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }
		public bool IsTargetable { get; }

		public StealthUndefendedAttackTargetSnapshot(uint actorId, string actorType,
			CPos strategicCell, CPos currentCell, int configuredPriority, int actorValue,
			int hitPoints, int maximumHitPoints, bool isInWorld = true,
			bool isDead = false, bool isTargetable = true)
		{
			if (actorId == 0)
				throw new ArgumentOutOfRangeException(nameof(actorId));
			if (string.IsNullOrWhiteSpace(actorType))
				throw new ArgumentException("Actor types must be non-empty.", nameof(actorType));
			if (actorValue < 0 || hitPoints < 0 || maximumHitPoints < 0)
				throw new ArgumentOutOfRangeException(nameof(actorValue));

			ActorId = actorId;
			ActorType = actorType.ToLowerInvariant();
			StrategicCell = strategicCell;
			CurrentCell = currentCell;
			ConfiguredPriority = configuredPriority;
			ActorValue = actorValue;
			HitPoints = hitPoints;
			MaximumHitPoints = maximumHitPoints;
			IsInWorld = isInWorld;
			IsDead = isDead;
			IsTargetable = isTargetable;
		}

		public bool IsValid => IsInWorld && !IsDead && IsTargetable &&
			(MaximumHitPoints <= 0 || HitPoints > 0);
	}

	public sealed class StealthUndefendedAttackLiveSnapshot
	{
		readonly ReadOnlyCollection<StealthUndefendedAttackMemberSnapshot> members;
		readonly ReadOnlyCollection<StealthUndefendedAttackTargetSnapshot> targets;
		readonly ReadOnlyCollection<uint> liveDefenderActorIds;

		public int Tick { get; }
		public IReadOnlyList<StealthUndefendedAttackMemberSnapshot> Members => members;
		public IReadOnlyList<StealthUndefendedAttackTargetSnapshot> Targets => targets;
		public IReadOnlyList<uint> LiveDefenderActorIds => liveDefenderActorIds;
		public bool FormationCloaked { get; }
		public bool HasDetectorCoverage { get; }
		public bool PlannedActionRevealsFormation { get; }

		public StealthUndefendedAttackLiveSnapshot(int tick,
			IEnumerable<StealthUndefendedAttackMemberSnapshot> members,
			IEnumerable<StealthUndefendedAttackTargetSnapshot> targets,
			IEnumerable<uint> liveDefenderActorIds, bool formationCloaked,
			bool hasDetectorCoverage, bool plannedActionRevealsFormation)
		{
			if (tick < 0)
				throw new ArgumentOutOfRangeException(nameof(tick));
			if (members == null || targets == null || liveDefenderActorIds == null)
				throw new ArgumentNullException(members == null ? nameof(members) :
					targets == null ? nameof(targets) : nameof(liveDefenderActorIds));

			var normalizedMembers = members.OrderBy(member => member?.ActorId).ToArray();
			var normalizedTargets = targets.OrderBy(target => target?.ActorId).ToArray();
			var defenders = liveDefenderActorIds.OrderBy(id => id).ToArray();
			if (normalizedMembers.Length == 0 || normalizedMembers.Any(member => member == null) ||
				normalizedMembers.Select(member => member.ActorId).Distinct().Count() != normalizedMembers.Length)
				throw new ArgumentException("Live members must be unique and non-empty.", nameof(members));
			if (normalizedTargets.Any(target => target == null) ||
				normalizedTargets.Select(target => target.ActorId).Distinct().Count() != normalizedTargets.Length)
				throw new ArgumentException("Live targets must have unique identities.", nameof(targets));
			if (defenders.Any(id => id == 0) || defenders.Distinct().Count() != defenders.Length)
				throw new ArgumentException("Live defenders must have unique nonzero identities.", nameof(liveDefenderActorIds));

			Tick = tick;
			this.members = Array.AsReadOnly(normalizedMembers);
			this.targets = Array.AsReadOnly(normalizedTargets);
			this.liveDefenderActorIds = Array.AsReadOnly(defenders);
			FormationCloaked = formationCloaked;
			HasDetectorCoverage = hasDetectorCoverage;
			PlannedActionRevealsFormation = plannedActionRevealsFormation;
		}
	}

	public interface IStealthUndefendedAttackLiveWorld
	{
		StealthUndefendedAttackLiveSnapshot Read(StealthApproachMission mission);
	}

	public sealed class StealthUndefendedAttackThreatFacts
	{
		readonly ReadOnlyCollection<uint> friendlyActorIds;
		readonly ReadOnlyCollection<uint> enemyActorIds;

		public uint SelectedTargetActorId { get; }
		public IReadOnlyList<uint> FriendlyActorIds => friendlyActorIds;
		public IReadOnlyList<uint> EnemyActorIds => enemyActorIds;
		public bool FormationCloaked { get; }
		public bool HasDetectorCoverage { get; }
		public bool PlannedActionRevealsFormation { get; }
		public bool PlannedCurrentRangeEngagement => true;
		public bool AnyMemberCurrentlyInRange { get; }

		public StealthUndefendedAttackThreatFacts(uint selectedTargetActorId,
			IEnumerable<uint> friendlyActorIds, IEnumerable<uint> enemyActorIds,
			bool formationCloaked, bool hasDetectorCoverage,
			bool plannedActionRevealsFormation, bool anyMemberCurrentlyInRange)
		{
			if (selectedTargetActorId == 0)
				throw new ArgumentOutOfRangeException(nameof(selectedTargetActorId));
			SelectedTargetActorId = selectedTargetActorId;
			this.friendlyActorIds = NormalizeIds(friendlyActorIds, nameof(friendlyActorIds), false);
			this.enemyActorIds = NormalizeIds(enemyActorIds, nameof(enemyActorIds), true);
			FormationCloaked = formationCloaked;
			HasDetectorCoverage = hasDetectorCoverage;
			PlannedActionRevealsFormation = plannedActionRevealsFormation;
			AnyMemberCurrentlyInRange = anyMemberCurrentlyInRange;
		}

		static ReadOnlyCollection<uint> NormalizeIds(IEnumerable<uint> ids,
			string parameterName, bool allowEmpty)
		{
			if (ids == null)
				throw new ArgumentNullException(parameterName);
			var normalized = ids.OrderBy(id => id).ToArray();
			if ((!allowEmpty && normalized.Length == 0) || normalized.Any(id => id == 0) ||
				normalized.Distinct().Count() != normalized.Length)
				throw new ArgumentException("Live actor identities must be unique and nonzero.", parameterName);
			return Array.AsReadOnly(normalized);
		}
	}

	public readonly struct StealthUndefendedAttackSafetyResult
	{
		public StealthTargetThreatScore Score { get; }
		public bool Approved { get; }
		public bool RequiresReacquisition { get; }

		public StealthUndefendedAttackSafetyResult(StealthTargetThreatScore score,
			bool approved, bool requiresReacquisition)
		{
			if (approved && requiresReacquisition)
				throw new ArgumentException("Approved safety cannot require reacquisition.");
			Score = score;
			Approved = approved;
			RequiresReacquisition = requiresReacquisition;
		}
	}

	public interface IStealthUndefendedAttackThreatAdapter
	{
		StealthUndefendedAttackSafetyResult Calculate(StealthUndefendedAttackThreatFacts facts);
	}

	public interface IStealthUndefendedAttackOrders
	{
		void IssueAttack(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, uint targetActorId, long orderRevision);
	}

	public sealed class StealthUndefendedAttackResult
	{
		readonly ReadOnlyCollection<uint> attackMemberActorIds;
		readonly ReadOnlyCollection<uint> liveDefenderActorIds;
		internal StealthBehaviorHandoff Handoff { get; }

		public StealthApproachMission Mission { get; }
		public StealthUndefendedAttackDisposition Disposition { get; }
		public uint? SelectedTargetActorId { get; }
		public IReadOnlyList<uint> AttackMemberActorIds => attackMemberActorIds;
		public IReadOnlyList<uint> LiveDefenderActorIds => liveDefenderActorIds;
		public StealthUndefendedAttackSafetyResult? Safety { get; }

		internal StealthUndefendedAttackResult(StealthBehaviorHandoff handoff,
			StealthApproachMission mission, StealthUndefendedAttackDisposition disposition,
			uint? selectedTargetActorId, IEnumerable<uint> attackMemberActorIds,
			IEnumerable<uint> liveDefenderActorIds, StealthUndefendedAttackSafetyResult? safety)
		{
			Handoff = handoff;
			Mission = mission;
			Disposition = disposition;
			SelectedTargetActorId = selectedTargetActorId;
			this.attackMemberActorIds = Array.AsReadOnly(attackMemberActorIds.ToArray());
			this.liveDefenderActorIds = Array.AsReadOnly(liveDefenderActorIds.ToArray());
			Safety = safety;
		}
	}

	public sealed class StealthUndefendedAttackTransition
	{
		public StealthBehaviorHandoff Retained { get; }
		public StealthBehaviorHandoff Reacquisition { get; }
		public StealthCrushEvaluationHandoff CrushEvaluation { get; }

		internal StealthUndefendedAttackTransition(StealthBehaviorHandoff handoff,
			StealthUndefendedAttackResult result)
		{
			if (result.Disposition == StealthUndefendedAttackDisposition.Retain)
				Retained = handoff;
			else if (result.Disposition == StealthUndefendedAttackDisposition.Reacquire)
				Reacquisition = handoff;
			else
				CrushEvaluation = new StealthCrushEvaluationHandoff(
					handoff, result.Mission, result.LiveDefenderActorIds);
		}
	}
}
