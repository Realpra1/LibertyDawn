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
using System.Globalization;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Disabled SquadConstruction owner. It alone assigns unowned members and stages safe
	/// reinforcements. Staged members do not participate in a center until they arrive.
	/// </summary>
	public sealed class StealthSquadConstructionBehavior
	{
		const int PrivateSaveVersion = 1;
		readonly StealthBehaviorHandoff handoff;
		readonly HashSet<uint> expectedMemberActorIds;
		readonly HashSet<uint> stagedReinforcementActorIds = new HashSet<uint>();
		readonly IStealthSquadConstructionSafetyService safety;

		public StealthSquadConstructionBehavior(StealthBehaviorHandoff handoff,
			IEnumerable<uint> expectedMemberActorIds, IStealthSquadConstructionSafetyService safety)
		{
			if (handoff == null)
				throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.SquadConstruction)
				throw new ArgumentException(
					"The SquadConstruction behavior requires SquadConstruction ownership.", nameof(handoff));
			if (expectedMemberActorIds == null)
				throw new ArgumentNullException(nameof(expectedMemberActorIds));

			var members = expectedMemberActorIds.ToArray();
			if (members.Length == 0 || members.Any(id => id == 0) || members.Distinct().Count() != members.Length)
				throw new ArgumentException(
					"Expected SquadConstruction member identities must be nonzero and unique.",
					nameof(expectedMemberActorIds));

			this.handoff = handoff;
			this.expectedMemberActorIds = new HashSet<uint>(members);
			this.safety = safety ?? throw new ArgumentNullException(nameof(safety));
		}

		public StealthSquadConstructionResult Execute(
			IEnumerable<StealthSquadConstructionMemberSnapshot> memberSnapshots,
			IEnumerable<StealthSquadConstructionSquadSnapshot> squadSnapshots)
		{
			if (memberSnapshots == null)
				throw new ArgumentNullException(nameof(memberSnapshots));
			if (squadSnapshots == null)
				throw new ArgumentNullException(nameof(squadSnapshots));

			var suppliedMembers = memberSnapshots.ToArray();
			if (suppliedMembers.GroupBy(member => member.ActorId).Any(group => group.Count() != 1))
				throw new ArgumentException("SquadConstruction member snapshots must be unique.",
					nameof(memberSnapshots));

			var suppliedSquads = squadSnapshots.OrderBy(squad => squad.SquadId).ToArray();
			if (suppliedSquads.GroupBy(squad => squad.SquadId).Any(group => group.Count() != 1))
				throw new ArgumentException("SquadConstruction squad snapshots must be unique.",
					nameof(squadSnapshots));

			var members = suppliedMembers.Where(member => expectedMemberActorIds.Contains(member.ActorId) &&
				member.ActorId != 0 && member.IsInWorld && !member.IsDead && member.IsStealthTank)
				.OrderBy(member => member.ActorId).ToArray();
			var liveMemberActorIds = new HashSet<uint>(members.Select(member => member.ActorId));
			stagedReinforcementActorIds.RemoveWhere(actorId => !liveMemberActorIds.Contains(actorId));
			if (members.Length == 0)
				return Result(StealthSquadConstructionDisposition.Terminated,
					Array.Empty<StealthSquadAssignment>(), Array.Empty<StealthSquadCenter>());

			var squads = suppliedSquads.ToDictionary(squad => squad.SquadId);
			var centers = suppliedSquads.ToDictionary(squad => squad.SquadId,
				squad => new List<uint>());
			foreach (var member in members.Where(member => member.AssignedSquadId != null &&
				squads.ContainsKey(member.AssignedSquadId.Value) &&
				!stagedReinforcementActorIds.Contains(member.ActorId)))
				centers[member.AssignedSquadId.Value].Add(member.ActorId);

			var pending = members.Where(member => member.AssignedSquadId == null ||
				stagedReinforcementActorIds.Contains(member.ActorId)).ToList();
			var assignments = new List<StealthSquadAssignment>();
			var newCenterIndex = pending.FindIndex(member => member.AssignedSquadId == null);
			if (centers.All(pair => pair.Value.Count == 0) && newCenterIndex >= 0)
			{
				var newCenter = pending[newCenterIndex];
				pending.RemoveAt(newCenterIndex);
				var squadId = suppliedSquads.Length == 0 ? 0 : suppliedSquads[0].SquadId;
				squads[squadId] = new StealthSquadConstructionSquadSnapshot(
					squadId, newCenter.StrategicCell);
				centers[squadId] = new List<uint> { newCenter.ActorId };
				stagedReinforcementActorIds.Remove(newCenter.ActorId);
				assignments.Add(Assignment(newCenter.ActorId, squadId,
					StealthSquadAssignmentDisposition.NewCenter));
			}

			foreach (var member in pending)
			{
				var squadId = member.AssignedSquadId != null &&
					centers.TryGetValue(member.AssignedSquadId.Value, out var assignedCenter) &&
					assignedCenter.Count != 0 ? member.AssignedSquadId.Value :
					centers.Where(pair => pair.Value.Count != 0)
						.OrderBy(pair => pair.Value.Count).ThenBy(pair => pair.Key)
						.Select(pair => pair.Key).FirstOrDefault(-1);
				if (squadId < 0)
					continue;

				var destination = squads[squadId].CurrentStrategicCell;
				if (IsSameOrAdjacent(member.StrategicCell, destination))
				{
					centers[squadId].Add(member.ActorId);
					stagedReinforcementActorIds.Remove(member.ActorId);
					assignments.Add(Assignment(member.ActorId, squadId,
						StealthSquadAssignmentDisposition.ActiveMember));
					continue;
				}

				var hasSafeRoute = safety.TryFindSafeRoute(member.ActorId, member.StrategicCell,
					destination, out var proposedRoute);
				var route = hasSafeRoute && proposedRoute != null ? proposedRoute.ToArray() : Array.Empty<CPos>();
				if (route.Length == 0 || !IsSameOrAdjacent(route[route.Length - 1], destination))
				{
					stagedReinforcementActorIds.Add(member.ActorId);
					assignments.Add(Assignment(member.ActorId, squadId,
						StealthSquadAssignmentDisposition.SafeHoldReinforcement));
					continue;
				}

				stagedReinforcementActorIds.Add(member.ActorId);
				assignments.Add(new StealthSquadAssignment(member.ActorId, squadId,
					StealthSquadAssignmentDisposition.RoutedReinforcement, route));
			}

			var activeCenters = centers.Where(pair => pair.Value.Count != 0)
				.OrderBy(pair => pair.Key)
				.Select(pair => new StealthSquadCenter(pair.Key,
					squads[pair.Key].CurrentStrategicCell,
					pair.Value.Distinct().OrderBy(actorId => actorId)))
				.ToArray();
			return Result(activeCenters.Length == 0 ?
				StealthSquadConstructionDisposition.Terminated :
				StealthSquadConstructionDisposition.Completed,
				assignments.OrderBy(assignment => assignment.ActorId), activeCenters);
		}

		public MiniYamlNode SerializePrivateState(StealthSquadConstructionResult result,
			string key = "SquadConstruction")
		{
			ValidateOwnedResult(result);
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", PrivateSaveVersion.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Owner", result.Handoff.Owner.ToString()),
				new MiniYamlNode("Epoch", result.Handoff.Epoch.Value.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Disposition", result.Disposition.ToString())
			};

			foreach (var assignment in result.Assignments)
			{
				var assignmentNodes = new List<MiniYamlNode>
				{
					new MiniYamlNode("ActorId", assignment.ActorId.ToString(CultureInfo.InvariantCulture)),
					new MiniYamlNode("SquadId", assignment.SquadId.ToString(CultureInfo.InvariantCulture)),
					new MiniYamlNode("Disposition", assignment.Disposition.ToString())
				};
				assignmentNodes.AddRange(assignment.SafeRouteStrategicCells.Select(cell =>
					new MiniYamlNode("RouteCell", FieldSaver.FormatValue(cell))));
				nodes.Add(new MiniYamlNode("Assignment", "", assignmentNodes));
			}

			foreach (var center in result.Centers)
				nodes.Add(new MiniYamlNode("Center", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("SquadId", center.SquadId.ToString(CultureInfo.InvariantCulture)),
					new MiniYamlNode("StrategicCell", FieldSaver.FormatValue(center.StrategicCell)),
					new MiniYamlNode("MemberActorIds", FieldSaver.FormatValue(center.MemberActorIds.ToArray()))
				}));

			return new MiniYamlNode(key, "", nodes);
		}

		public StealthSquadConstructionResult RestorePrivateState(MiniYamlNode node)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));

			var scalarNodes = node.Value.Nodes.Where(child => child.Key != "Assignment" && child.Key != "Center");
			var values = ReadUniqueValues(scalarNodes, "SquadConstruction private state");
			if (!TryReadInt(values, "Version", out var version) || version != PrivateSaveVersion)
				throw new InvalidOperationException("Unsupported stealth SquadConstruction private save schema.");
			if (!values.TryGetValue("Owner", out var ownerText) ||
				!Enum.TryParse(ownerText, out BehaviorId owner) || owner != BehaviorId.SquadConstruction)
				throw new InvalidOperationException("Invalid stealth SquadConstruction owner in private save state.");
			if (!values.TryGetValue("Epoch", out var epochText) ||
				!long.TryParse(epochText, NumberStyles.None, CultureInfo.InvariantCulture, out var epoch) || epoch <= 0)
				throw new InvalidOperationException("Invalid stealth SquadConstruction epoch in private save state.");
			if (owner != handoff.Owner || epoch != handoff.Epoch.Value)
				throw new InvalidOperationException("Stale stealth SquadConstruction ownership in private save state.");
			if (!values.TryGetValue("Disposition", out var dispositionText) ||
				!Enum.TryParse(dispositionText, out StealthSquadConstructionDisposition disposition) ||
				!Enum.IsDefined(typeof(StealthSquadConstructionDisposition), disposition))
				throw new InvalidOperationException("Invalid stealth SquadConstruction disposition in private save state.");

			var assignments = node.Value.Nodes.Where(child => child.Key == "Assignment")
				.Select(RestoreAssignment).ToArray();
			var centers = node.Value.Nodes.Where(child => child.Key == "Center")
				.Select(RestoreCenter).ToArray();
			var result = Result(disposition, assignments, centers);
			ValidateOwnedResult(result);
			stagedReinforcementActorIds.Clear();
			foreach (var actorId in result.Assignments.Where(assignment =>
				!assignment.IsActiveCenterMember).Select(assignment => assignment.ActorId))
				stagedReinforcementActorIds.Add(actorId);
			return result;
		}

		StealthSquadAssignment RestoreAssignment(MiniYamlNode node)
		{
			var values = ReadUniqueValues(node.Value.Nodes.Where(child => child.Key != "RouteCell"),
				"SquadConstruction assignment");
			if (!TryReadUInt(values, "ActorId", out var actorId) || actorId == 0 ||
				!expectedMemberActorIds.Contains(actorId))
				throw new InvalidOperationException("Invalid stealth SquadConstruction assignment actor.");
			if (!TryReadInt(values, "SquadId", out var squadId) || squadId < 0)
				throw new InvalidOperationException("Invalid stealth SquadConstruction assignment squad.");
			if (!values.TryGetValue("Disposition", out var dispositionText) ||
				!Enum.TryParse(dispositionText, out StealthSquadAssignmentDisposition disposition) ||
				!Enum.IsDefined(typeof(StealthSquadAssignmentDisposition), disposition))
				throw new InvalidOperationException("Invalid stealth SquadConstruction assignment disposition.");
			return new StealthSquadAssignment(actorId, squadId, disposition,
				node.Value.Nodes.Where(child => child.Key == "RouteCell").Select(child =>
					FieldLoader.GetValue<CPos>("RouteCell", child.Value.Value)));
		}

		static StealthSquadCenter RestoreCenter(MiniYamlNode node)
		{
			var values = ReadUniqueValues(node.Value.Nodes, "SquadConstruction center");
			if (!TryReadInt(values, "SquadId", out var squadId) || squadId < 0)
				throw new InvalidOperationException("Invalid stealth SquadConstruction center squad.");
			if (!values.TryGetValue("StrategicCell", out var cellText) ||
				!values.TryGetValue("MemberActorIds", out var membersText))
				throw new InvalidOperationException("Incomplete stealth SquadConstruction center.");

			return new StealthSquadCenter(squadId,
				FieldLoader.GetValue<CPos>("StrategicCell", cellText),
				FieldLoader.GetValue<uint[]>("MemberActorIds", membersText));
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

		void ValidateOwnedResult(StealthSquadConstructionResult result)
		{
			if (result == null)
				throw new ArgumentNullException(nameof(result));
			if (result.Handoff.Owner != handoff.Owner || result.Handoff.Epoch != handoff.Epoch)
				throw new ArgumentException(
					"The SquadConstruction result belongs to another ownership epoch.", nameof(result));

			ValidatePersistentResult(result);
		}

		void ValidatePersistentResult(StealthSquadConstructionResult result)
		{
			var assignments = result.Assignments.ToArray();
			var centers = result.Centers.ToArray();
			if (result.Disposition == StealthSquadConstructionDisposition.Terminated)
			{
				if (assignments.Length != 0 || centers.Length != 0)
					throw new InvalidOperationException("Invalid terminated SquadConstruction private state.");
				return;
			}

			var normalizedAssignments = assignments.All(assignment =>
				expectedMemberActorIds.Contains(assignment.ActorId)) &&
				assignments.Select(assignment => assignment.ActorId).SequenceEqual(
					assignments.Select(assignment => assignment.ActorId).Distinct().OrderBy(actorId => actorId));
			var normalizedCenters = centers.Length != 0 &&
				centers.Select(center => center.SquadId).SequenceEqual(
					centers.Select(center => center.SquadId).Distinct().OrderBy(squadId => squadId)) &&
				centers.All(center => center.MemberActorIds.Count != 0 &&
					center.MemberActorIds.All(actorId => actorId != 0 && expectedMemberActorIds.Contains(actorId)) &&
					center.MemberActorIds.SequenceEqual(
						center.MemberActorIds.Distinct().OrderBy(actorId => actorId)));
			if (!normalizedAssignments || !normalizedCenters ||
				centers.SelectMany(center => center.MemberActorIds).Distinct().Count() !=
				centers.Sum(center => center.MemberActorIds.Count))
				throw new InvalidOperationException("Invalid normalized SquadConstruction private state.");

			var allCenterMemberActorIds = new HashSet<uint>(
				centers.SelectMany(center => center.MemberActorIds));
			foreach (var assignment in assignments)
			{
				var center = centers.SingleOrDefault(candidate => candidate.SquadId == assignment.SquadId);
				if (center == null)
					throw new InvalidOperationException("SquadConstruction assignment has no active squad center.");

				var isInCenter = center.MemberActorIds.Contains(assignment.ActorId);
				var isInAnyCenter = allCenterMemberActorIds.Contains(assignment.ActorId);
				var routed = assignment.Disposition == StealthSquadAssignmentDisposition.RoutedReinforcement;
				if (isInCenter != assignment.IsActiveCenterMember ||
					(!assignment.IsActiveCenterMember && isInAnyCenter) ||
					routed != (assignment.SafeRouteStrategicCells.Count != 0) ||
					(routed && !IsSameOrAdjacent(
						assignment.SafeRouteStrategicCells[assignment.SafeRouteStrategicCells.Count - 1],
						center.StrategicCell)))
					throw new InvalidOperationException("Invalid SquadConstruction assignment admission state.");
			}
		}

		static bool IsSameOrAdjacent(CPos a, CPos b)
		{
			return Math.Max(Math.Abs((long)a.X - b.X), Math.Abs((long)a.Y - b.Y)) <= 1;
		}

		static Dictionary<string, string> ReadUniqueValues(IEnumerable<MiniYamlNode> nodes, string context)
		{
			var values = new Dictionary<string, string>(StringComparer.Ordinal);
			try
			{
				foreach (var child in nodes)
					values.Add(child.Key, child.Value.Value);
			}
			catch (ArgumentException ex)
			{
				throw new InvalidOperationException("Duplicate " + context + " field.", ex);
			}

			return values;
		}

		static bool TryReadInt(Dictionary<string, string> values, string key, out int value)
		{
			value = 0;
			return values.TryGetValue(key, out var text) &&
				int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}

		static bool TryReadUInt(Dictionary<string, string> values, string key, out uint value)
		{
			value = 0;
			return values.TryGetValue(key, out var text) &&
				uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
		}
	}
}
