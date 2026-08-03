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
	public static class PlayerUnitSamplingPolicy
	{
		public static bool CanLearn(bool sourceIsBot, bool humanOnly, bool playable, bool nonCombatant,
			bool queueCompatible, bool mobile, int builtCount)
		{
			return playable && !nonCombatant && (!humanOnly || !sourceIsBot) && queueCompatible && mobile && builtCount > 0;
		}

		public static string Pick(IReadOnlyDictionary<string, double> rawChances, double maximumCombinedChance, double roll)
		{
			if (rawChances.Count == 0)
				return null;

			var positive = rawChances.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key, StringComparer.Ordinal).ToArray();
			var rawTotal = positive.Sum(kv => kv.Value);
			if (rawTotal <= 0)
				return null;

			var total = Math.Min(Math.Clamp(maximumCombinedChance, 0, 1), rawTotal);
			if (roll < 0 || roll >= total)
				return null;

			var scale = total / rawTotal;
			var cursor = 0d;
			foreach (var candidate in positive)
			{
				cursor += candidate.Value * scale;
				if (roll < cursor)
					return candidate.Key;
			}

			return positive[positive.Length - 1].Key;
		}
	}
}
