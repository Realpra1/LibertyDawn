#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits.BotModules
{
	public enum UnassignedCombatFallbackDisposition
	{
		Unclaimed,
		PreCodexAssault,
		GenericFallback
	}

	public static class UnassignedCombatUnitRecruitmentPolicy
	{
		public static UnassignedCombatFallbackDisposition SelectFallback(bool advancedBehaviorEnabled,
			bool preCodexAssaultAvailable, bool genericFallbackEligible)
		{
			if (!advancedBehaviorEnabled && preCodexAssaultAvailable)
				return UnassignedCombatFallbackDisposition.PreCodexAssault;

			return genericFallbackEligible ? UnassignedCombatFallbackDisposition.GenericFallback :
				UnassignedCombatFallbackDisposition.Unclaimed;
		}
	}

	public sealed class UnassignedCombatUnitRegistry
	{
		readonly SortedSet<uint> actorIds = new SortedSet<uint>();
		readonly HashSet<uint> claimedActorIds = new HashSet<uint>();

		public uint[] ActorIds => actorIds.ToArray();
		public uint[] ClaimedActorIds => claimedActorIds.OrderBy(id => id).ToArray();

		public bool Contains(uint actorId) { return actorIds.Contains(actorId); }
		public bool IsClaimed(uint actorId) { return claimedActorIds.Contains(actorId); }

		public bool Register(uint actorId)
		{
			return actorId != 0 && !claimedActorIds.Contains(actorId) && actorIds.Add(actorId);
		}

		public bool Remove(uint actorId) { return actorIds.Remove(actorId); }

		public bool Release(uint actorId)
		{
			return actorId != 0 && claimedActorIds.Remove(actorId);
		}

		public bool Claim(uint actorId)
		{
			if (actorId == 0)
				return false;

			var changed = actorIds.Remove(actorId);
			return claimedActorIds.Add(actorId) || changed;
		}

		public bool Forget(uint actorId)
		{
			return actorIds.Remove(actorId) | claimedActorIds.Remove(actorId);
		}

		public void Import(IEnumerable<uint> unassignedIds, IEnumerable<uint> claimedIds)
		{
			actorIds.Clear();
			claimedActorIds.Clear();
			foreach (var id in claimedIds.Where(id => id != 0))
				claimedActorIds.Add(id);

			foreach (var id in unassignedIds.Where(id => id != 0 && !claimedActorIds.Contains(id)))
				actorIds.Add(id);
		}

		public void Remove(IEnumerable<uint> ids)
		{
			foreach (var id in ids)
				actorIds.Remove(id);
		}

		public static int StaggeredAuditStartOffset(int interval, int playerIndex, int playerCount)
		{
			if (interval <= 0 || playerCount <= 0)
				return 0;

			return interval * System.Math.Max(0, System.Math.Min(playerIndex, playerCount - 1)) / playerCount;
		}

		public static uint[] NextAuditActorIds(ref uint nextActorId, uint endActorId, int maximumIds)
		{
			var ids = new List<uint>(System.Math.Max(0, maximumIds));
			while (ids.Count < maximumIds && nextActorId <= endActorId)
				ids.Add(nextActorId++);

			return ids.ToArray();
		}

		public static uint StableActorIdDigest(IEnumerable<uint> ids)
		{
			unchecked
			{
				var digest = 2166136261u;
				foreach (var id in ids.OrderBy(id => id))
					digest = (digest ^ id) * 16777619u;
				return digest;
			}
		}
	}
}
