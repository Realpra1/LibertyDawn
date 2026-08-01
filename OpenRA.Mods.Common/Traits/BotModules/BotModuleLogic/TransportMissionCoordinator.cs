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
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	public enum TransportMissionKind { Rescue, InfantryAssault, LongDistanceInfantry, HeavyDrop }

	/// <summary>
	/// Deterministic reservation ledger shared by all AI transport strategies. It prevents a transport
	/// or passenger from being claimed by two strategies and bounds outstanding work independently of
	/// the number of actors on the map.
	/// </summary>
	public sealed class TransportMissionCoordinator
	{
		readonly int maximumMissions;
		readonly Dictionary<uint, int> actorReservations = new Dictionary<uint, int>();
		readonly Dictionary<int, HashSet<uint>> missionActors = new Dictionary<int, HashSet<uint>>();
		int nextMissionId = 1;

		public int MissionCount => missionActors.Count;

		public TransportMissionCoordinator(int maximumMissions)
		{
			if (maximumMissions <= 0)
				throw new ArgumentOutOfRangeException(nameof(maximumMissions));

			this.maximumMissions = maximumMissions;
		}

		public int TryReserve(IEnumerable<uint> actorIds)
		{
			if (missionActors.Count >= maximumMissions || actorIds == null)
				return 0;

			var ids = actorIds.Distinct().OrderBy(id => id).ToArray();
			if (ids.Length == 0 || ids.Any(actorReservations.ContainsKey))
				return 0;

			var missionId = nextMissionId++;
			if (nextMissionId <= 0)
				nextMissionId = 1;

			var reserved = new HashSet<uint>(ids);
			missionActors.Add(missionId, reserved);
			foreach (var id in reserved)
				actorReservations.Add(id, missionId);

			return missionId;
		}

		public bool IsReserved(uint actorId) { return actorReservations.ContainsKey(actorId); }

		public bool IsReservedBy(int missionId, uint actorId)
		{
			return actorReservations.TryGetValue(actorId, out var owner) && owner == missionId;
		}

		public void Release(int missionId)
		{
			if (!missionActors.TryGetValue(missionId, out var actors))
				return;

			foreach (var id in actors)
				actorReservations.Remove(id);

			missionActors.Remove(missionId);
		}
	}
}
