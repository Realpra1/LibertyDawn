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
	/// <summary>
	/// Deterministic, world-independent technology-counter policy.
	/// </summary>
	public static class TechnologyCounterLogic
	{
		public static string DominantBranch(IReadOnlyDictionary<string, int> progress)
		{
			if (progress == null)
				return null;

			return progress.Where(kv => kv.Value > 0)
				.OrderByDescending(kv => kv.Value)
				.ThenBy(kv => kv.Key, StringComparer.Ordinal)
				.Select(kv => kv.Key).FirstOrDefault();
		}

		public static bool DelayElapsed(int currentTick, int observedSinceTick, int switchDelay)
		{
			return observedSinceTick >= 0 && currentTick - observedSinceTick >= Math.Max(0, switchDelay);
		}

		public static string DesiredBranch(string currentDesiredBranch, string initialBranch,
			string observedEnemyBranch, int currentTick, int observedSinceTick, int switchDelay,
			IReadOnlyDictionary<string, string> counters)
		{
			var fallback = string.IsNullOrEmpty(currentDesiredBranch) ? initialBranch : currentDesiredBranch;
			if (string.IsNullOrEmpty(observedEnemyBranch) ||
				!DelayElapsed(currentTick, observedSinceTick, switchDelay) || counters == null)
				return fallback;

			return counters.TryGetValue(observedEnemyBranch, out var counter) ? counter : fallback;
		}

		public static string BranchToDowngrade(IReadOnlyDictionary<string, int> ownProgress, string desiredBranch)
		{
			if (ownProgress == null)
				return null;

			return ownProgress.Where(kv => kv.Value > 0 &&
				!kv.Key.Equals(desiredBranch, StringComparison.OrdinalIgnoreCase))
				.OrderByDescending(kv => kv.Value)
				.ThenBy(kv => kv.Key, StringComparer.Ordinal)
				.Select(kv => kv.Key).FirstOrDefault();
		}

		public static string NextUpgrade(IReadOnlyList<string> orderedUpgrades, ISet<string> ownedActorTypes)
		{
			if (orderedUpgrades == null || ownedActorTypes == null)
				return null;

			return orderedUpgrades.FirstOrDefault(upgrade => !ownedActorTypes.Contains(upgrade));
		}
	}
}
