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
	public static class TechnologyCounterLogic
	{
		public static string DominantBranch(IReadOnlyDictionary<string, int> counts)
		{
			if (counts == null)
				return null;

			return counts.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value)
				.ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key).FirstOrDefault();
		}

		public static bool DelayElapsed(int currentTick, int observedSinceTick, int switchDelay)
		{
			return currentTick - observedSinceTick >= Math.Max(0, switchDelay);
		}
	}
}
