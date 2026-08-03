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
	public static class FirstTowerPlacementLogic
	{
		public static CPos PreferredLocation(CPos yardTopLeft, CVec yardDimensions, CVec towerDimensions)
		{
			var yardWidth = Math.Max(1, yardDimensions.X);
			var towerWidth = Math.Max(1, towerDimensions.X);
			var towerHeight = Math.Max(1, towerDimensions.Y);
			return new CPos(yardTopLeft.X + (yardWidth - towerWidth) / 2, yardTopLeft.Y - towerHeight);
		}

		public static IEnumerable<CPos> CandidateLocations(CPos preferred, int radius)
		{
			var distance = Math.Max(0, radius);
			var cells = new List<CPos>();
			for (var y = -distance; y <= distance; y++)
				for (var x = -distance; x <= distance; x++)
					cells.Add(preferred + new CVec(x, y));

			return cells.OrderBy(c => (c - preferred).LengthSquared)
				.ThenBy(c => c.Y).ThenBy(c => c.X);
		}

		public static CPos? ClosestLegalLocation(CPos preferred, int radius, Func<CPos, bool> isLegal)
		{
			return CandidateLocations(preferred, radius).Where(isLegal)
				.Select(c => (CPos?)c).FirstOrDefault();
		}
	}
}
