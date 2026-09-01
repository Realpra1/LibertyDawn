#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Assigns live tanks and keeps reinforcements out of the active center until arrival.</summary>
	public sealed class StealthSquadConstructionBehavior
	{
		readonly StealthBehaviorHandoff handoff;
		readonly HashSet<uint> expectedMembers;
		readonly HashSet<uint> reinforcements = new HashSet<uint>();
		readonly IStealthSquadConstructionSafetyService safety;

		public StealthSquadConstructionBehavior(StealthBehaviorHandoff handoff,
			IEnumerable<uint> expectedMemberActorIds, IStealthSquadConstructionSafetyService safety)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.SquadConstruction)
				throw new ArgumentException("SquadConstruction requires its ownership.", nameof(handoff));
			var members = expectedMemberActorIds?.ToArray() ??
				throw new ArgumentNullException(nameof(expectedMemberActorIds));
			if (members.Length == 0 || members.Any(id => id == 0) ||
				members.Distinct().Count() != members.Length)
				throw new ArgumentException("Expected members require unique identities.",
					nameof(expectedMemberActorIds));
			expectedMembers = members.ToHashSet();
			this.safety = safety ?? throw new ArgumentNullException(nameof(safety));
		}

		public StealthSquadConstructionResult Execute(
			IEnumerable<StealthSquadConstructionMemberSnapshot> memberSnapshots,
			IEnumerable<StealthSquadConstructionSquadSnapshot> squadSnapshots)
		{
			var suppliedMembers = memberSnapshots?.ToArray() ??
				throw new ArgumentNullException(nameof(memberSnapshots));
			var suppliedSquads = squadSnapshots?.OrderBy(squad => squad.SquadId).ToArray() ??
				throw new ArgumentNullException(nameof(squadSnapshots));
			if (suppliedMembers.GroupBy(member => member.ActorId).Any(group => group.Count() != 1) ||
				suppliedSquads.GroupBy(squad => squad.SquadId).Any(group => group.Count() != 1))
				throw new ArgumentException("SquadConstruction snapshots require unique identities.");

			var members = suppliedMembers.Where(member => expectedMembers.Contains(member.ActorId) &&
				member.IsInWorld && !member.IsDead && member.IsStealthTank)
				.OrderBy(member => member.ActorId).ToArray();
			reinforcements.RemoveWhere(id => members.All(member => member.ActorId != id));
			if (members.Length == 0)
				return Result(StealthSquadConstructionDisposition.Terminated,
					Array.Empty<StealthSquadAssignment>(), Array.Empty<StealthSquadCenter>());

			var squads = suppliedSquads.ToDictionary(squad => squad.SquadId);
			var centers = suppliedSquads.ToDictionary(squad => squad.SquadId,
				squad => new List<uint>());
			foreach (var member in members.Where(member => member.AssignedSquadId.HasValue &&
				squads.ContainsKey(member.AssignedSquadId.Value) && !reinforcements.Contains(member.ActorId) &&
				IsSameOrAdjacent(member.StrategicCell,
					squads[member.AssignedSquadId.Value].CurrentStrategicCell)))
				centers[member.AssignedSquadId.Value].Add(member.ActorId);

			var activeMemberIds = centers.SelectMany(center => center.Value).ToHashSet();
			var pending = members.Where(member => !activeMemberIds.Contains(member.ActorId)).ToList();
			var assignments = new List<StealthSquadAssignment>();
			var newCenter = pending.FirstOrDefault();
			if (centers.All(center => center.Value.Count == 0) && newCenter.ActorId != 0)
			{
				pending.Remove(newCenter);
				var squadId = suppliedSquads.Length == 0 ? 0 : suppliedSquads[0].SquadId;
				squads[squadId] = new StealthSquadConstructionSquadSnapshot(squadId, newCenter.StrategicCell);
				centers[squadId] = new List<uint> { newCenter.ActorId };
				reinforcements.Remove(newCenter.ActorId);
				assignments.Add(Assignment(newCenter.ActorId, squadId,
					StealthSquadAssignmentDisposition.NewCenter));
			}

			foreach (var member in pending)
			{
				var squadId = member.AssignedSquadId.HasValue &&
					centers.TryGetValue(member.AssignedSquadId.Value, out var assigned) && assigned.Count != 0 ?
					member.AssignedSquadId.Value : centers.Where(center => center.Value.Count != 0)
						.OrderBy(center => center.Value.Count).ThenBy(center => center.Key)
						.Select(center => center.Key).FirstOrDefault(-1);
				if (squadId < 0)
					continue;
				var destination = squads[squadId].CurrentStrategicCell;
				if (IsSameOrAdjacent(member.StrategicCell, destination))
				{
					centers[squadId].Add(member.ActorId);
					reinforcements.Remove(member.ActorId);
					assignments.Add(Assignment(member.ActorId, squadId,
						StealthSquadAssignmentDisposition.ActiveMember));
					continue;
				}

				var safe = safety.TryFindSafeRoute(member.ActorId, member.StrategicCell,
					destination, out var proposedRoute);
				var route = safe && proposedRoute != null ? proposedRoute.ToArray() : Array.Empty<CPos>();
				reinforcements.Add(member.ActorId);
				if (route.Length == 0 || !IsSameOrAdjacent(route[route.Length - 1], destination))
					assignments.Add(Assignment(member.ActorId, squadId,
						StealthSquadAssignmentDisposition.SafeHoldReinforcement));
				else
					assignments.Add(new StealthSquadAssignment(member.ActorId, squadId,
						StealthSquadAssignmentDisposition.RoutedReinforcement, route));
			}

			var activeCenters = centers.Where(center => center.Value.Count != 0)
				.OrderBy(center => center.Key).Select(center => new StealthSquadCenter(center.Key,
					squads[center.Key].CurrentStrategicCell, center.Value.Distinct().OrderBy(id => id))).ToArray();
			return Result(activeCenters.Length == 0 ? StealthSquadConstructionDisposition.Terminated :
				StealthSquadConstructionDisposition.Completed,
				assignments.OrderBy(assignment => assignment.ActorId), activeCenters);
		}

		StealthSquadConstructionResult Result(StealthSquadConstructionDisposition disposition,
			IEnumerable<StealthSquadAssignment> assignments, IEnumerable<StealthSquadCenter> centers)
		{
			return new StealthSquadConstructionResult(handoff, disposition, assignments, centers);
		}

		static StealthSquadAssignment Assignment(uint actorId, int squadId,
			StealthSquadAssignmentDisposition disposition)
		{
			return new StealthSquadAssignment(actorId, squadId, disposition, Array.Empty<CPos>());
		}

		static bool IsSameOrAdjacent(CPos left, CPos right)
		{
			return Math.Max(Math.Abs((long)left.X - right.X), Math.Abs((long)left.Y - right.Y)) <= 1;
		}
	}
}
