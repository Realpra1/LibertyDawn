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
	/// Disabled Start owner. It normalizes durable actor identities and can only hand them to
	/// SquadConstruction. It deliberately has no dependency on order or combat services.
	/// </summary>
	public sealed class StealthStartBehavior
	{
		const int PrivateSaveVersion = 1;
		readonly StealthBehaviorHandoff handoff;

		public StealthStartBehavior(StealthBehaviorHandoff handoff)
		{
			if (handoff == null)
				throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.Start)
				throw new ArgumentException("The Start behavior requires Start ownership.", nameof(handoff));

			this.handoff = handoff;
		}

		public StealthStartResult Execute(StealthLifecycleObservation observation,
			IEnumerable<StealthStartMemberSnapshot> members)
		{
			if (observation.Kind != StealthLifecycleObservationKind.UnitBuilt &&
				observation.Kind != StealthLifecycleObservationKind.RepairCompleted)
				return Result(observation, StealthStartDisposition.ObservationOnly, Array.Empty<uint>());

			var normalized = members == null ? Array.Empty<uint>() : members
				.Where(member => member.ActorId != 0 && member.IsInWorld && !member.IsDead)
				.Select(member => member.ActorId)
				.Distinct()
				.OrderBy(actorId => actorId)
				.ToArray();
			if (observation.SubjectActorId == 0 || normalized.Length == 0 ||
				Array.BinarySearch(normalized, observation.SubjectActorId) < 0)
				return Result(observation, StealthStartDisposition.Terminated, Array.Empty<uint>());

			return Result(observation, StealthStartDisposition.Transition, normalized);
		}

		public MiniYamlNode SerializePrivateState(StealthStartResult result, string key = "Start")
		{
			ValidateOwnedResult(result);
			return new MiniYamlNode(key, "", new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", PrivateSaveVersion.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Owner", result.Handoff.Owner.ToString()),
				new MiniYamlNode("Epoch", result.Handoff.Epoch.Value.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Source", result.Source.ToString()),
				new MiniYamlNode("SubjectActorId", result.SubjectActorId.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Disposition", result.Disposition.ToString()),
				new MiniYamlNode("MemberActorIds", FieldSaver.FormatValue(result.MemberActorIds.ToArray()))
			});
		}

		public StealthStartResult RestorePrivateState(MiniYamlNode node)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));

			var values = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var child in node.Value.Nodes)
				values.Add(child.Key, child.Value.Value);

			if (!TryReadInt(values, "Version", out var version) || version != PrivateSaveVersion)
				throw new InvalidOperationException("Unsupported stealth Start private save schema.");
			if (!values.TryGetValue("Owner", out var ownerText) ||
				!Enum.TryParse(ownerText, out BehaviorId owner) || owner != BehaviorId.Start)
				throw new InvalidOperationException("Invalid stealth Start owner in private save state.");
			if (!values.TryGetValue("Epoch", out var epochText) ||
				!long.TryParse(epochText, NumberStyles.None, CultureInfo.InvariantCulture, out var epoch) || epoch <= 0)
				throw new InvalidOperationException("Invalid stealth Start epoch in private save state.");
			if (owner != handoff.Owner || epoch != handoff.Epoch.Value)
				throw new InvalidOperationException("Stale stealth Start ownership in private save state.");
			if (!values.TryGetValue("Source", out var sourceText) ||
				!Enum.TryParse(sourceText, out StealthLifecycleObservationKind source) ||
				!Enum.IsDefined(typeof(StealthLifecycleObservationKind), source))
				throw new InvalidOperationException("Invalid stealth Start source in private save state.");
			if (!values.TryGetValue("SubjectActorId", out var subjectText) ||
				!uint.TryParse(subjectText, NumberStyles.None, CultureInfo.InvariantCulture, out var subjectActorId))
				throw new InvalidOperationException("Invalid stealth Start subject in private save state.");
			if (!values.TryGetValue("Disposition", out var dispositionText) ||
				!Enum.TryParse(dispositionText, out StealthStartDisposition disposition) ||
				!Enum.IsDefined(typeof(StealthStartDisposition), disposition))
				throw new InvalidOperationException("Invalid stealth Start disposition in private save state.");
			if (!values.TryGetValue("MemberActorIds", out var membersText))
				throw new InvalidOperationException("Missing stealth Start members in private save state.");

			var members = FieldLoader.GetValue<uint[]>("MemberActorIds", membersText);
			ValidatePersistentResult(source, subjectActorId, disposition, members);
			return new StealthStartResult(handoff, source, subjectActorId, disposition, members);
		}

		StealthStartResult Result(StealthLifecycleObservation observation,
			StealthStartDisposition disposition, IEnumerable<uint> members)
		{
			return new StealthStartResult(handoff, observation.Kind,
				observation.SubjectActorId, disposition, members);
		}

		void ValidateOwnedResult(StealthStartResult result)
		{
			if (result == null)
				throw new ArgumentNullException(nameof(result));
			if (result.Handoff.Owner != handoff.Owner || result.Handoff.Epoch != handoff.Epoch)
				throw new ArgumentException("The Start result belongs to another ownership epoch.", nameof(result));

			ValidatePersistentResult(result.Source, result.SubjectActorId,
				result.Disposition, result.MemberActorIds);
		}

		static void ValidatePersistentResult(StealthLifecycleObservationKind source, uint subjectActorId,
			StealthStartDisposition disposition, IEnumerable<uint> memberActorIds)
		{
			var members = memberActorIds.ToArray();
			var transitionSource = source == StealthLifecycleObservationKind.UnitBuilt ||
				source == StealthLifecycleObservationKind.RepairCompleted;
			var membersAreNormalized = members.All(id => id != 0) &&
				members.SequenceEqual(members.Distinct().OrderBy(id => id));
			var valid = disposition == StealthStartDisposition.ObservationOnly ?
				!transitionSource && members.Length == 0 :
				disposition == StealthStartDisposition.Terminated ?
					transitionSource && members.Length == 0 :
					transitionSource && subjectActorId != 0 && members.Length != 0 &&
					membersAreNormalized && Array.BinarySearch(members, subjectActorId) >= 0;
			if (!valid)
				throw new InvalidOperationException("Invalid stealth Start private save invariants.");
		}

		static bool TryReadInt(Dictionary<string, string> values, string key, out int value)
		{
			value = 0;
			return values.TryGetValue(key, out var text) &&
				int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}
	}
}
