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

namespace OpenRA.Mods.Common.Traits
{
	public static class UnitCapPolicy
	{
		public static int AllowedQueueAmount(int requested, int committedUnits, int totalUnitLimit,
			bool isHarvester, int committedHarvesters, int harvesterLimit, bool countsTowardUnitLimit)
		{
			var allowed = Math.Max(0, requested);
			if (countsTowardUnitLimit && totalUnitLimit > 0)
				allowed = Math.Min(allowed, Math.Max(0, totalUnitLimit - committedUnits));

			if (isHarvester && harvesterLimit > 0)
				allowed = Math.Min(allowed, Math.Max(0, harvesterLimit - committedHarvesters));

			return allowed;
		}
	}
}
