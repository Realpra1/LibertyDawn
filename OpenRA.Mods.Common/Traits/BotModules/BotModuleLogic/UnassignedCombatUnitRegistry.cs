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

		public uint[] ActorIds => actorIds.ToArray();

		public bool Contains(uint actorId) { return actorIds.Contains(actorId); }

		public bool Register(uint actorId)
		{
			return actorId != 0 && actorIds.Add(actorId);
		}

		public bool Remove(uint actorId) { return actorIds.Remove(actorId); }

		public void Remove(IEnumerable<uint> ids)
		{
			foreach (var id in ids)
				actorIds.Remove(id);
		}
	}
}
