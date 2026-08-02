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

namespace OpenRA.Mods.Common.Traits
{
	public static class HarvesterRaidLogic
	{
		public static int RaidLimit(int totalHarvesters, int percent)
		{
			if (totalHarvesters <= 0 || percent <= 0)
				return 0;

			return (int)Math.Min(totalHarvesters,
				Math.Ceiling(totalHarvesters * Math.Min(100, percent) / 100d));
		}

		public static int AdditionalRefineries(int harvesters, int refineries, int harvestersPerRefinery, int maximumAdditional)
		{
			if (harvestersPerRefinery <= 0 || maximumAdditional <= 0)
				return 0;

			var required = (int)Math.Ceiling(Math.Max(0, harvesters) / (double)harvestersPerRefinery);
			return Math.Min(maximumAdditional, Math.Max(0, required - Math.Max(0, refineries)));
		}
	}
}
