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
	public enum StealthApproachDisposition
	{
		Moving,
		AwaitingSafeRoute,
		Reacquire,
		UndefendedAttack,
		CrushEvaluation
	}

	public enum StealthApproachArrivalClassification
	{
		None,
		Undefended,
		Defended
	}

	public sealed class StealthApproachStrategicCellSnapshot
	{
		readonly ReadOnlyCollection<StealthCombatGroupSnapshot> enemyGroup;

		public CPos StrategicCell { get; }
		public IReadOnlyList<StealthCombatGroupSnapshot> EnemyGroup => enemyGroup;
		public bool HasDetectorCoverage { get; }
		public bool PlannedActionRevealsFormation { get; }

		public StealthApproachStrategicCellSnapshot(CPos strategicCell,
			IEnumerable<StealthCombatGroupSnapshot> enemyGroup, bool hasDetectorCoverage,
			bool plannedActionRevealsFormation = false)
		{
			StrategicCell = strategicCell;
			this.enemyGroup = NormalizeGroup(enemyGroup, nameof(enemyGroup));
			HasDetectorCoverage = hasDetectorCoverage;
			PlannedActionRevealsFormation = plannedActionRevealsFormation;
		}

		internal static ReadOnlyCollection<StealthCombatGroupSnapshot> NormalizeGroup(
			IEnumerable<StealthCombatGroupSnapshot> group, string parameterName)
		{
			if (group == null)
				throw new ArgumentNullException(parameterName);

			var snapshots = group.ToArray();
			if (snapshots.Any(snapshot => snapshot == null) || snapshots
				.Select(snapshot => snapshot.ActorType).Distinct(StringComparer.Ordinal).Count() != snapshots.Length)
				throw new ArgumentException("Combat group actor types must be unique.", parameterName);

			return Array.AsReadOnly(snapshots.OrderBy(snapshot => snapshot.ActorType,
				StringComparer.Ordinal).ToArray());
		}
	}

	/// <summary>Immutable strategic-cache input for long-range Approach routing.</summary>
	public sealed class StealthApproachStrategicCacheSnapshot
	{
		readonly ReadOnlyCollection<StealthApproachStrategicCellSnapshot> cells;

		public int Width { get; }
		public int Height { get; }
		public IReadOnlyList<StealthApproachStrategicCellSnapshot> Cells => cells;

		public StealthApproachStrategicCacheSnapshot(int width, int height,
			IEnumerable<StealthApproachStrategicCellSnapshot> cells)
		{
			if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(width));
			if (cells == null)
				throw new ArgumentNullException(nameof(cells));

			var normalized = cells.OrderBy(cell => cell?.StrategicCell.Y)
				.ThenBy(cell => cell?.StrategicCell.X).ToArray();
			if (normalized.Length != width * height || normalized.Any(cell => cell == null) ||
				normalized.Select(cell => cell.StrategicCell).Distinct().Count() != normalized.Length ||
				normalized.Any(cell => cell.StrategicCell.X < 0 || cell.StrategicCell.Y < 0 ||
					cell.StrategicCell.X >= width || cell.StrategicCell.Y >= height))
				throw new ArgumentException("Approach cache cells must exactly cover the strategic grid.", nameof(cells));

			Width = width;
			Height = height;
			this.cells = Array.AsReadOnly(normalized);
		}
	}

	public interface IStealthApproachStrategicCache
	{
		StealthApproachStrategicCacheSnapshot ReadSnapshot();
	}

	public sealed class StealthApproachMemberSnapshot
	{
		public uint ActorId { get; }
		public CPos StrategicCell { get; }
		public bool IsReinforcement { get; }

		public StealthApproachMemberSnapshot(uint actorId, CPos strategicCell, bool isReinforcement = false)
		{
			if (actorId == 0)
				throw new ArgumentOutOfRangeException(nameof(actorId));

			ActorId = actorId;
			StrategicCell = strategicCell;
			IsReinforcement = isReinforcement;
		}
	}

	public sealed class StealthApproachLiveSnapshot
	{
		readonly ReadOnlyCollection<StealthApproachMemberSnapshot> members;
		readonly ReadOnlyCollection<StealthCombatGroupSnapshot> localFriendlyGroup;
		readonly ReadOnlyCollection<StealthCombatGroupSnapshot> localEnemyGroup;
		readonly ReadOnlyCollection<uint> liveDefenderActorIds;

		public bool TargetIsValid { get; }
		public IReadOnlyList<StealthApproachMemberSnapshot> Members => members;
		public IReadOnlyList<StealthCombatGroupSnapshot> LocalFriendlyGroup => localFriendlyGroup;
		public IReadOnlyList<StealthCombatGroupSnapshot> LocalEnemyGroup => localEnemyGroup;
		public IReadOnlyList<uint> LiveDefenderActorIds => liveDefenderActorIds;
		public bool FormationCloaked { get; }
		public bool HasDetectorCoverage { get; }
		public bool PlannedActionRevealsFormation { get; }

		public StealthApproachLiveSnapshot(bool targetIsValid,
			IEnumerable<StealthApproachMemberSnapshot> members,
			IEnumerable<StealthCombatGroupSnapshot> localFriendlyGroup,
			IEnumerable<StealthCombatGroupSnapshot> localEnemyGroup,
			IEnumerable<uint> liveDefenderActorIds,
			bool formationCloaked, bool hasDetectorCoverage,
			bool plannedActionRevealsFormation = false)
		{
			if (members == null)
				throw new ArgumentNullException(nameof(members));
			if (liveDefenderActorIds == null)
				throw new ArgumentNullException(nameof(liveDefenderActorIds));

			var normalizedMembers = members.OrderBy(member => member?.ActorId).ToArray();
			if (normalizedMembers.Length == 0 || normalizedMembers.Any(member => member == null) ||
				normalizedMembers.Select(member => member.ActorId).Distinct().Count() != normalizedMembers.Length ||
				normalizedMembers.All(member => member.IsReinforcement))
				throw new ArgumentException("Approach requires unique members and at least one active core member.", nameof(members));
			var defenders = liveDefenderActorIds.OrderBy(id => id).ToArray();
			if (defenders.Any(id => id == 0) || defenders.Distinct().Count() != defenders.Length)
				throw new ArgumentException("Live defender identities must be unique and nonzero.", nameof(liveDefenderActorIds));

			TargetIsValid = targetIsValid;
			this.members = Array.AsReadOnly(normalizedMembers);
			this.localFriendlyGroup = StealthApproachStrategicCellSnapshot.NormalizeGroup(
				localFriendlyGroup, nameof(localFriendlyGroup));
			this.localEnemyGroup = StealthApproachStrategicCellSnapshot.NormalizeGroup(
				localEnemyGroup, nameof(localEnemyGroup));
			this.liveDefenderActorIds = Array.AsReadOnly(defenders);
			FormationCloaked = formationCloaked;
			HasDetectorCoverage = hasDetectorCoverage;
			PlannedActionRevealsFormation = plannedActionRevealsFormation;
		}
	}

	public interface IStealthApproachLiveWorld
	{
		StealthApproachLiveSnapshot Read(StealthApproachMission mission);
	}

	public interface IStealthApproachMovementOrders
	{
		void IssueMove(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, CPos destinationStrategicCell);
	}

	public sealed class StealthApproachResult
	{
		readonly ReadOnlyCollection<CPos> route;
		readonly ReadOnlyCollection<uint> activeMemberActorIds;
		readonly ReadOnlyCollection<uint> liveDefenderActorIds;

		internal StealthBehaviorHandoff Handoff { get; }
		public StealthApproachMission Mission { get; }
		public StealthApproachDisposition Disposition { get; }
		public StealthApproachArrivalClassification ArrivalClassification { get; }
		public CPos ActiveSquadCenter { get; }
		public IReadOnlyList<CPos> Route => route;
		public int RouteIndex { get; }
		public IReadOnlyList<uint> ActiveMemberActorIds => activeMemberActorIds;
		public IReadOnlyList<uint> LiveDefenderActorIds => liveDefenderActorIds;
		public StealthTargetThreatScore? LocalThreatScore { get; }

		internal StealthApproachResult(StealthBehaviorHandoff handoff, StealthApproachMission mission,
			StealthApproachDisposition disposition, StealthApproachArrivalClassification arrivalClassification,
			CPos activeSquadCenter, IEnumerable<CPos> route, int routeIndex,
			IEnumerable<uint> activeMemberActorIds, IEnumerable<uint> liveDefenderActorIds,
			StealthTargetThreatScore? localThreatScore)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			Mission = mission ?? throw new ArgumentNullException(nameof(mission));
			Disposition = disposition;
			ArrivalClassification = arrivalClassification;
			ActiveSquadCenter = activeSquadCenter;
			this.route = Array.AsReadOnly((route ?? throw new ArgumentNullException(nameof(route))).ToArray());
			RouteIndex = routeIndex;
			this.activeMemberActorIds = Array.AsReadOnly(
				(activeMemberActorIds ?? throw new ArgumentNullException(nameof(activeMemberActorIds))).ToArray());
			this.liveDefenderActorIds = Array.AsReadOnly(
				(liveDefenderActorIds ?? throw new ArgumentNullException(nameof(liveDefenderActorIds))).ToArray());
			LocalThreatScore = localThreatScore;
		}
	}

	public sealed class StealthUndefendedAttackHandoff
	{
		internal StealthBehaviorHandoff Handoff { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public StealthApproachMission Mission { get; }

		internal StealthUndefendedAttackHandoff(StealthBehaviorHandoff handoff,
			StealthApproachMission mission)
		{
			Handoff = handoff;
			Mission = mission;
		}
	}

	public sealed class StealthCrushEvaluationHandoff
	{
		readonly ReadOnlyCollection<uint> liveDefenderActorIds;
		internal StealthBehaviorHandoff Handoff { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public StealthApproachMission Mission { get; }
		public IReadOnlyList<uint> LiveDefenderActorIds => liveDefenderActorIds;

		internal StealthCrushEvaluationHandoff(StealthBehaviorHandoff handoff,
			StealthApproachMission mission, IEnumerable<uint> defenders)
		{
			Handoff = handoff;
			Mission = mission;
			liveDefenderActorIds = Array.AsReadOnly(defenders.ToArray());
		}
	}

	public sealed class StealthApproachTransition
	{
		public StealthBehaviorHandoff Reacquisition { get; }
		public StealthUndefendedAttackHandoff UndefendedAttack { get; }
		public StealthCrushEvaluationHandoff CrushEvaluation { get; }

		internal StealthApproachTransition(StealthBehaviorHandoff handoff,
			StealthApproachResult result)
		{
			if (result.Disposition == StealthApproachDisposition.Reacquire)
				Reacquisition = handoff;
			else if (result.Disposition == StealthApproachDisposition.UndefendedAttack)
				UndefendedAttack = new StealthUndefendedAttackHandoff(handoff, result.Mission);
			else
			{
				CrushEvaluation = new StealthCrushEvaluationHandoff(
					handoff, result.Mission, result.LiveDefenderActorIds);
			}
		}
	}
}
