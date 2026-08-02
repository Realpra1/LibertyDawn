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

namespace OpenRA.Mods.Common.Traits
{
	public static class OpeningGarrisonLogic
	{
		public static bool ShouldBuildRifle(int riflesBuilt, int rifleGoal, int rocketsBuilt, int rocketGoal)
		{
			if (riflesBuilt >= rifleGoal)
				return false;

			if (rocketsBuilt >= rocketGoal)
				return true;

			return (long)riflesBuilt * Math.Max(1, rocketGoal) <= (long)rocketsBuilt * Math.Max(1, rifleGoal);
		}

		public static List<CPos> CellsAroundBuilding(CPos topLeft, CVec dimensions, int distance)
		{
			var gap = Math.Max(1, distance);
			var left = topLeft.X - gap;
			var top = topLeft.Y - gap;
			var right = topLeft.X + Math.Max(1, dimensions.X) - 1 + gap;
			var bottom = topLeft.Y + Math.Max(1, dimensions.Y) - 1 + gap;
			var cells = new List<CPos>();

			for (var x = left; x <= right; x++)
			{
				cells.Add(new CPos(x, top));
				if (bottom != top)
					cells.Add(new CPos(x, bottom));
			}

			for (var y = top + 1; y < bottom; y++)
			{
				cells.Add(new CPos(left, y));
				if (right != left)
					cells.Add(new CPos(right, y));
			}

			return cells;
		}
	}
}
