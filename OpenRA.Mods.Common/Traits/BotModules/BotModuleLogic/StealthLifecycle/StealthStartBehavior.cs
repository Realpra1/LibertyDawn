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
	/// <summary>Starts built or repaired live stealth tanks in SquadConstruction.</summary>
	public sealed class StealthStartBehavior
	{
		readonly StealthBehaviorHandoff handoff;

		public StealthStartBehavior(StealthBehaviorHandoff handoff)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.Start)
				throw new ArgumentException("Start requires Start ownership.", nameof(handoff));
		}

		public StealthStartResult Execute(StealthLifecycleObservation observation,
			IEnumerable<StealthStartMemberSnapshot> members)
		{
			if (observation.Kind != StealthLifecycleObservationKind.UnitBuilt &&
				observation.Kind != StealthLifecycleObservationKind.RepairCompleted)
				return Result(observation, StealthStartDisposition.ObservationOnly, Array.Empty<uint>());

			var live = members?.Where(member => member.ActorId != 0 && member.IsInWorld && !member.IsDead)
				.Select(member => member.ActorId).Distinct().OrderBy(actorId => actorId).ToArray() ??
				Array.Empty<uint>();
			if (observation.SubjectActorId == 0 || !live.Contains(observation.SubjectActorId))
				return Result(observation, StealthStartDisposition.Terminated, Array.Empty<uint>());
			return Result(observation, StealthStartDisposition.Transition, live);
		}

		StealthStartResult Result(StealthLifecycleObservation observation,
			StealthStartDisposition disposition, IEnumerable<uint> members)
		{
			return new StealthStartResult(handoff, observation.Kind,
				observation.SubjectActorId, disposition, members);
		}
	}
}
