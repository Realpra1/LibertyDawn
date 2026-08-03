#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Deterministic, bounded reservation ledger for AI transport missions.</summary>
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
