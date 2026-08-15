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
	public static class OpeningPolicyLogic
	{
		public static bool HoldOptionalConstructionForFirstRefinery(bool refineryGoalActive,
			int liveRefineries, bool refineryCommitted, bool refineryActionable)
		{
			return refineryGoalActive && liveRefineries <= 0 && (refineryCommitted || refineryActionable);
		}

		public static bool RequiredPrefixComplete(IReadOnlyCollection<int> completedGoals, int prefixGoalCount)
		{
			if (completedGoals == null || prefixGoalCount <= 0)
				return false;

			return Enumerable.Range(0, prefixGoalCount).All(completedGoals.Contains);
		}

		public static int FirstBuildableGoal(
			IReadOnlyList<string[]> orderedGoals,
			IReadOnlyCollection<int> completedGoals,
			IReadOnlyCollection<int> reservedGoals,
			IEnumerable<string> buildableTypes)
		{
			if (orderedGoals == null || buildableTypes == null)
				return -1;

			var buildable = new HashSet<string>(buildableTypes);
			for (var i = 0; i < orderedGoals.Count; i++)
			{
				if (completedGoals.Contains(i))
					continue;

				// A reservation is an accepted commitment, so another independent Fact may
				// reserve the next ordered goal instead of idling until construction completes.
				if (reservedGoals.Contains(i))
					continue;

				return orderedGoals[i].Any(buildable.Contains) ? i : -1;
			}

			return -1;
		}

		public static string FirstAvailable(IEnumerable<string> preferred, IEnumerable<string> available)
		{
			if (preferred == null || available == null)
				return null;

			var availableNames = new HashSet<string>(available);
			return preferred.FirstOrDefault(availableNames.Contains);
		}

		public static bool RetryReservation(int requestedTick, int currentTick, int retryDelay, bool isQueued)
		{
			return !isQueued && currentTick - requestedTick >= Math.Max(1, retryDelay);
		}

		public static bool CanSkipUnavailableGoal(
			int goal,
			IReadOnlyList<string[]> orderedGoals,
			IReadOnlyCollection<int> completedGoals,
			IEnumerable<string> optionalTypes,
			IEnumerable<string> buildableTypes)
		{
			if (orderedGoals == null || completedGoals == null || optionalTypes == null || buildableTypes == null ||
				goal < 0 || goal >= orderedGoals.Count || completedGoals.Contains(goal))
				return false;

			var optional = new HashSet<string>(optionalTypes);
			var buildable = new HashSet<string>(buildableTypes);
			if (!orderedGoals[goal].Any(optional.Contains) || orderedGoals[goal].Any(buildable.Contains))
				return false;

			return Enumerable.Range(0, orderedGoals.Count).All(i =>
				i == goal || orderedGoals[i].Any(optional.Contains) || completedGoals.Contains(i));
		}
	}
}
