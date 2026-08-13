#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System.Collections.Generic;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>One-shot, exact outcomes for objective transport handoffs that time out after unload.</summary>
	public sealed class TransportObjectiveTimeoutLedger
	{
		readonly Dictionary<uint, uint> timedOutObjectives = new Dictionary<uint, uint>();

		public void Record(uint passengerId, uint objectiveId)
		{
			timedOutObjectives[passengerId] = objectiveId;
		}

		public bool TryConsume(uint passengerId, uint objectiveId)
		{
			if (!timedOutObjectives.TryGetValue(passengerId, out var recordedObjective) || recordedObjective != objectiveId)
				return false;

			timedOutObjectives.Remove(passengerId);
			return true;
		}

		public void Clear(uint passengerId)
		{
			timedOutObjectives.Remove(passengerId);
		}
	}
}
