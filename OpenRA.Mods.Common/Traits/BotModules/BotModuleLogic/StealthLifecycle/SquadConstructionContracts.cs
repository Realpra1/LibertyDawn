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

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Typed immutable no-squad boundary used when a combat owner loses every member. The prior
	/// mission is context only: SquadConstruction remains the sole owner of rebuilding a squad.
	/// </summary>
	public sealed class StealthSquadConstructionRecoveryHandoff
	{
		internal StealthBehaviorHandoff Handoff { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public StealthApproachMission Mission { get; }

		internal StealthSquadConstructionRecoveryHandoff(StealthBehaviorHandoff handoff,
			StealthApproachMission mission)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.SquadConstruction)
				throw new ArgumentException("Recovery must belong to SquadConstruction.", nameof(handoff));
			Mission = mission ?? throw new ArgumentNullException(nameof(mission));
		}
	}

	public enum StealthSquadConstructionDisposition
	{
		Completed,
		Terminated
	}

	public enum StealthSquadAssignmentDisposition
	{
		NewCenter,
		ActiveMember,
		RoutedReinforcement,
		SafeHoldReinforcement
	}

	public readonly struct StealthSquadConstructionMemberSnapshot
	{
		public uint ActorId { get; }
		public CPos StrategicCell { get; }
		public int? AssignedSquadId { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }
		public bool IsStealthTank { get; }

		public StealthSquadConstructionMemberSnapshot(uint actorId, CPos strategicCell,
			int? assignedSquadId = null, bool isInWorld = true, bool isDead = false,
			bool isStealthTank = true)
		{
			if (assignedSquadId < 0)
				throw new ArgumentOutOfRangeException(nameof(assignedSquadId));

			ActorId = actorId;
			StrategicCell = strategicCell;
			AssignedSquadId = assignedSquadId;
			IsInWorld = isInWorld;
			IsDead = isDead;
			IsStealthTank = isStealthTank;
		}
	}

	public readonly struct StealthSquadConstructionSquadSnapshot
	{
		public int SquadId { get; }
		public CPos CurrentStrategicCell { get; }

		public StealthSquadConstructionSquadSnapshot(int squadId, CPos currentStrategicCell)
		{
			if (squadId < 0)
				throw new ArgumentOutOfRangeException(nameof(squadId));

			SquadId = squadId;
			CurrentStrategicCell = currentStrategicCell;
		}
	}

	/// <summary>
	/// Owner-scoped safety query. It returns data only; the disabled lifecycle has no order channel.
	/// </summary>
	public interface IStealthSquadConstructionSafetyService
	{
		bool TryFindSafeRoute(uint actorId, CPos originStrategicCell,
			CPos destinationStrategicCell, out IReadOnlyList<CPos> routeStrategicCells);
	}

	public sealed class StealthSquadAssignment
	{
		readonly ReadOnlyCollection<CPos> safeRouteStrategicCells;

		public uint ActorId { get; }
		public int SquadId { get; }
		public StealthSquadAssignmentDisposition Disposition { get; }
		public IReadOnlyList<CPos> SafeRouteStrategicCells => safeRouteStrategicCells;
		public bool IsActiveCenterMember =>
			Disposition == StealthSquadAssignmentDisposition.NewCenter ||
			Disposition == StealthSquadAssignmentDisposition.ActiveMember;

		internal StealthSquadAssignment(uint actorId, int squadId,
			StealthSquadAssignmentDisposition disposition, IEnumerable<CPos> safeRouteStrategicCells)
		{
			if (actorId == 0)
				throw new ArgumentOutOfRangeException(nameof(actorId));
			if (squadId < 0)
				throw new ArgumentOutOfRangeException(nameof(squadId));
			if (!Enum.IsDefined(typeof(StealthSquadAssignmentDisposition), disposition))
				throw new ArgumentOutOfRangeException(nameof(disposition));
			if (safeRouteStrategicCells == null)
				throw new ArgumentNullException(nameof(safeRouteStrategicCells));

			ActorId = actorId;
			SquadId = squadId;
			Disposition = disposition;
			this.safeRouteStrategicCells = Array.AsReadOnly(
				new List<CPos>(safeRouteStrategicCells).ToArray());
		}
	}

	public sealed class StealthSquadCenter
	{
		readonly ReadOnlyCollection<uint> memberActorIds;

		public int SquadId { get; }
		public CPos StrategicCell { get; }
		public IReadOnlyList<uint> MemberActorIds => memberActorIds;

		internal StealthSquadCenter(int squadId, CPos strategicCell,
			IEnumerable<uint> memberActorIds)
		{
			if (squadId < 0)
				throw new ArgumentOutOfRangeException(nameof(squadId));
			if (memberActorIds == null)
				throw new ArgumentNullException(nameof(memberActorIds));

			SquadId = squadId;
			StrategicCell = strategicCell;
			this.memberActorIds = Array.AsReadOnly(new List<uint>(memberActorIds).ToArray());
		}
	}

	public sealed class StealthSquadConstructionResult
	{
		readonly ReadOnlyCollection<StealthSquadAssignment> assignments;
		readonly ReadOnlyCollection<StealthSquadCenter> centers;

		internal StealthBehaviorHandoff Handoff { get; }
		public StealthSquadConstructionDisposition Disposition { get; }
		public IReadOnlyList<StealthSquadAssignment> Assignments => assignments;
		public IReadOnlyList<StealthSquadCenter> Centers => centers;
		public bool IsComplete => Disposition == StealthSquadConstructionDisposition.Completed;

		internal StealthSquadConstructionResult(StealthBehaviorHandoff handoff,
			StealthSquadConstructionDisposition disposition,
			IEnumerable<StealthSquadAssignment> assignments,
			IEnumerable<StealthSquadCenter> centers)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (!Enum.IsDefined(typeof(StealthSquadConstructionDisposition), disposition))
				throw new ArgumentOutOfRangeException(nameof(disposition));
			if (assignments == null)
				throw new ArgumentNullException(nameof(assignments));
			if (centers == null)
				throw new ArgumentNullException(nameof(centers));

			Disposition = disposition;
			this.assignments = Array.AsReadOnly(new List<StealthSquadAssignment>(assignments).ToArray());
			this.centers = Array.AsReadOnly(new List<StealthSquadCenter>(centers).ToArray());
		}
	}
}
