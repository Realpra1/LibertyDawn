#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Prevalidated active-versus-pending roster committed only after Step 2 is accepted.</summary>
	sealed class StealthSquadConstructionMembershipPlan
	{
		readonly ReadOnlyCollection<uint> active;
		readonly ReadOnlyCollection<uint> pending;
		public OwnershipEpoch Epoch { get; }
		public IReadOnlyList<uint> ActiveActorIds => active;
		public IReadOnlyList<uint> PendingActorIds => pending;

		StealthSquadConstructionMembershipPlan(OwnershipEpoch epoch,
			IEnumerable<uint> active, IEnumerable<uint> pending)
		{
			Epoch = epoch;
			this.active = Array.AsReadOnly(active.ToArray());
			this.pending = Array.AsReadOnly(pending.ToArray());
		}

		public static StealthSquadConstructionMembershipPlan Create(
			StealthSquadConstructionResult result, int squadId,
			IEnumerable<uint> currentLiveActorIds)
		{
			if (result == null || !result.IsComplete || squadId < 0 || currentLiveActorIds == null)
				throw new ArgumentException("Completed construction membership is required.");
			var current = Canonical(currentLiveActorIds, "current squad membership");
			if (result.Centers.Count != 1 || result.Centers[0].SquadId != squadId ||
				result.Assignments.Any(assignment => assignment.SquadId != squadId))
				throw new InvalidOperationException("Construction membership belongs to another squad.");
			var active = Canonical(result.Centers[0].MemberActorIds, "active construction center");
			var pending = Canonical(result.Assignments.Where(assignment =>
				!assignment.IsActiveCenterMember).Select(assignment => assignment.ActorId),
				"pending construction members", true);
			if (active.Intersect(pending).Any() ||
				!active.Concat(pending).OrderBy(id => id).SequenceEqual(current))
				throw new InvalidOperationException("Construction membership is stale or partial.");
			if (result.Assignments.Where(assignment => assignment.IsActiveCenterMember)
				.Any(assignment => !active.Contains(assignment.ActorId)))
				throw new InvalidOperationException("Active construction assignment is outside its center.");
			return new StealthSquadConstructionMembershipPlan(result.Handoff.Epoch, active, pending);
		}

		static uint[] Canonical(IEnumerable<uint> ids, string name, bool allowEmpty = false)
		{
			var copy = ids?.OrderBy(id => id).ToArray();
			if (copy == null || (!allowEmpty && copy.Length == 0) || copy.Any(id => id == 0) ||
				copy.Distinct().Count() != copy.Length)
				throw new InvalidOperationException("Invalid " + name + ".");
			return copy;
		}
	}
}
