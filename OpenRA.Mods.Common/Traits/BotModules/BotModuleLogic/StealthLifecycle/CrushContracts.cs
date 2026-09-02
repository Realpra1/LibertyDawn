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
	public enum StealthCrushDisposition
	{
		Retain,
		Kite,
		UndefendedAttack,
		Reacquire
	}

	public sealed class StealthCrushMemberSnapshot
	{
		public uint ActorId { get; }
		public CPos CurrentCell { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }
		public bool IsValid => IsInWorld && !IsDead;

		public StealthCrushMemberSnapshot(uint actorId, CPos currentCell,
			bool isInWorld = true, bool isDead = false)
		{
			if (actorId == 0)
				throw new ArgumentOutOfRangeException(nameof(actorId));
			ActorId = actorId;
			CurrentCell = currentCell;
			IsInWorld = isInWorld;
			IsDead = isDead;
		}
	}

	public sealed class StealthCrushActorSnapshot
	{
		public uint ActorId { get; }
		public string ActorType { get; }
		public CPos StrategicCell { get; }
		public CPos CurrentCell { get; }
		public int ConfiguredPriority { get; }
		public bool IsDefender { get; }
		public bool IsMissionObjective { get; }
		public bool IsInfantry { get; }
		public bool CanBeCrushedByFormation { get; }
		public bool HasDetectorCoverage { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }
		public bool IsTargetable { get; }
		public bool IsValid => IsInWorld && !IsDead && IsTargetable;

		public StealthCrushActorSnapshot(uint actorId, string actorType,
			CPos strategicCell, CPos currentCell, int configuredPriority,
			bool isDefender, bool isMissionObjective, bool isInfantry,
			bool canBeCrushedByFormation, bool hasDetectorCoverage,
			bool isInWorld = true, bool isDead = false, bool isTargetable = true)
		{
			if (actorId == 0)
				throw new ArgumentOutOfRangeException(nameof(actorId));
			if (string.IsNullOrWhiteSpace(actorType))
				throw new ArgumentException("Actor types must be non-empty.", nameof(actorType));
			ActorId = actorId;
			ActorType = actorType.ToLowerInvariant();
			StrategicCell = strategicCell;
			CurrentCell = currentCell;
			ConfiguredPriority = configuredPriority;
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

	public sealed class StealthCrushLiveSnapshot
	{
		readonly ReadOnlyCollection<StealthCrushMemberSnapshot> members;
		readonly ReadOnlyCollection<StealthCrushActorSnapshot> actors;

		public int Tick { get; }
		public IReadOnlyList<StealthCrushMemberSnapshot> Members => members;
		public IReadOnlyList<StealthCrushActorSnapshot> Actors => actors;
		public bool FormationCloaked { get; }

		public StealthCrushLiveSnapshot(int tick,
			IEnumerable<StealthCrushMemberSnapshot> members,
			IEnumerable<StealthCrushActorSnapshot> actors, bool formationCloaked)
		{
			if (tick < 0)
				throw new ArgumentOutOfRangeException(nameof(tick));
			if (members == null || actors == null)
				throw new ArgumentNullException(members == null ? nameof(members) : nameof(actors));
			var normalizedMembers = members.OrderBy(member => member?.ActorId).ToArray();
			var normalizedActors = actors.OrderBy(actor => actor?.ActorId).ToArray();
			if (normalizedMembers.Length == 0 || normalizedMembers.Any(member => member == null) ||
				normalizedMembers.Select(member => member.ActorId).Distinct().Count() != normalizedMembers.Length)
				throw new ArgumentException("Live squad members must have unique identities.", nameof(members));
			if (normalizedActors.Any(actor => actor == null) ||
				normalizedActors.Select(actor => actor.ActorId).Distinct().Count() != normalizedActors.Length)
				throw new ArgumentException("Live actors must have unique identities.", nameof(actors));
			Tick = tick;
			this.members = Array.AsReadOnly(normalizedMembers);
			this.actors = Array.AsReadOnly(normalizedActors);
			FormationCloaked = formationCloaked;
		}
	}

	public interface IStealthCrushLiveWorld
	{
		StealthCrushLiveSnapshot Read(StealthApproachMission mission);
	}

	public sealed class StealthCrushThreatFacts
	{
		readonly ReadOnlyCollection<uint> friendlyActorIds;
		readonly ReadOnlyCollection<uint> enemyActorIds;

		public uint SelectedTargetActorId { get; }
		public CPos SelectedTargetCurrentCell { get; }
		public IReadOnlyList<uint> FriendlyActorIds => friendlyActorIds;
		public IReadOnlyList<uint> EnemyActorIds => enemyActorIds;
		public bool FormationCloaked { get; }
		public bool HasDetectorCoverage { get; }
		public bool RemainCloakedAction => true;
		public bool PlannedActionRevealsFormation => false;
		public bool PlannedCurrentRangeEngagement => true;

		public StealthCrushThreatFacts(uint selectedTargetActorId,
			CPos selectedTargetCurrentCell, IEnumerable<uint> friendlyActorIds,
			IEnumerable<uint> enemyActorIds, bool formationCloaked,
			bool hasDetectorCoverage)
		{
			if (selectedTargetActorId == 0)
				throw new ArgumentOutOfRangeException(nameof(selectedTargetActorId));
			SelectedTargetActorId = selectedTargetActorId;
			SelectedTargetCurrentCell = selectedTargetCurrentCell;
			this.friendlyActorIds = Normalize(friendlyActorIds, nameof(friendlyActorIds));
			this.enemyActorIds = Normalize(enemyActorIds, nameof(enemyActorIds));
			if (!this.enemyActorIds.Contains(selectedTargetActorId))
				throw new ArgumentException("The selected infantry must belong to the live defender context.",
					nameof(enemyActorIds));
			FormationCloaked = formationCloaked;
			HasDetectorCoverage = hasDetectorCoverage;
		}

		static ReadOnlyCollection<uint> Normalize(IEnumerable<uint> ids, string parameterName)
		{
			if (ids == null)
				throw new ArgumentNullException(parameterName);
			var normalized = ids.OrderBy(id => id).ToArray();
			if (normalized.Length == 0 || normalized.Any(id => id == 0) ||
				normalized.Distinct().Count() != normalized.Length)
				throw new ArgumentException("Live actor identities must be unique and nonzero.", parameterName);
			return Array.AsReadOnly(normalized);
		}
	}

	public readonly struct StealthCrushSafetyResult
	{
		public StealthTargetThreatScore Score { get; }
		public bool Approved { get; }

		public StealthCrushSafetyResult(StealthTargetThreatScore score, bool approved)
		{
			Score = score;
			Approved = approved;
		}
	}

	public interface IStealthCrushThreatAdapter
	{
		StealthCrushSafetyResult Calculate(StealthCrushThreatFacts facts);
	}

	public interface IStealthCrushOrders
	{
		void IssueCrush(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, uint targetActorId, CPos targetCurrentCell,
			long attemptRevision);
	}

	public sealed class StealthCrushResult
	{
		readonly ReadOnlyCollection<uint> activeMemberActorIds;
		readonly ReadOnlyCollection<uint> liveDefenderActorIds;
		readonly ReadOnlyCollection<uint> liveObjectiveActorIds;
		internal StealthBehaviorHandoff Handoff { get; }

		public StealthApproachMission Mission { get; }
		public StealthCrushDisposition Disposition { get; }
		public uint? SelectedTargetActorId { get; }
		public CPos? SelectedTargetCurrentCell { get; }
		public IReadOnlyList<uint> ActiveMemberActorIds => activeMemberActorIds;
		public IReadOnlyList<uint> LiveDefenderActorIds => liveDefenderActorIds;
		public IReadOnlyList<uint> LiveObjectiveActorIds => liveObjectiveActorIds;
		public StealthCrushSafetyResult? Safety { get; }

		internal StealthCrushResult(StealthBehaviorHandoff handoff,
			StealthApproachMission mission, StealthCrushDisposition disposition,
			uint? selectedTargetActorId, CPos? selectedTargetCurrentCell,
			IEnumerable<uint> activeMemberActorIds, IEnumerable<uint> liveDefenderActorIds,
			IEnumerable<uint> liveObjectiveActorIds, StealthCrushSafetyResult? safety)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			Mission = mission ?? throw new ArgumentNullException(nameof(mission));
			Disposition = disposition;
			SelectedTargetActorId = selectedTargetActorId;
			SelectedTargetCurrentCell = selectedTargetCurrentCell;
			this.activeMemberActorIds = Array.AsReadOnly(activeMemberActorIds.ToArray());
			this.liveDefenderActorIds = Array.AsReadOnly(liveDefenderActorIds.ToArray());
			this.liveObjectiveActorIds = Array.AsReadOnly(liveObjectiveActorIds.ToArray());
			Safety = safety;
		}
	}

	public sealed class StealthKiteHandoff
	{
		readonly ReadOnlyCollection<uint> liveDefenderActorIds;
		internal StealthBehaviorHandoff Handoff { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public StealthApproachMission Mission { get; }
		public IReadOnlyList<uint> LiveDefenderActorIds => liveDefenderActorIds;

		internal StealthKiteHandoff(StealthBehaviorHandoff handoff,
			StealthApproachMission mission, IEnumerable<uint> liveDefenderActorIds)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.Kite)
				throw new ArgumentException("The handoff must belong to Kite.", nameof(handoff));
			Mission = mission ?? throw new ArgumentNullException(nameof(mission));
			if (liveDefenderActorIds == null)
				throw new ArgumentNullException(nameof(liveDefenderActorIds));
			var normalized = liveDefenderActorIds.OrderBy(id => id).ToArray();
			if (normalized.Length == 0 || normalized.Any(id => id == 0) ||
				normalized.Distinct().Count() != normalized.Length)
				throw new ArgumentException("Kite requires unique live defender identities.",
					nameof(liveDefenderActorIds));
			this.liveDefenderActorIds = Array.AsReadOnly(normalized);
		}
	}

	public sealed class StealthCrushTransition
	{
		public StealthBehaviorHandoff Retained { get; }
		public StealthKiteHandoff Kite { get; }
		public StealthUndefendedAttackHandoff UndefendedAttack { get; }
		public StealthBehaviorHandoff Reacquisition { get; }

		internal StealthCrushTransition(StealthBehaviorHandoff handoff,
			StealthCrushResult result)
		{
			if (result.Disposition == StealthCrushDisposition.Retain)
				Retained = handoff;
			else if (result.Disposition == StealthCrushDisposition.Kite)
				Kite = new StealthKiteHandoff(handoff, result.Mission, result.LiveDefenderActorIds);
			else if (result.Disposition == StealthCrushDisposition.UndefendedAttack)
				UndefendedAttack = new StealthUndefendedAttackHandoff(handoff, result.Mission);
			else
				Reacquisition = handoff;
		}
	}
}
