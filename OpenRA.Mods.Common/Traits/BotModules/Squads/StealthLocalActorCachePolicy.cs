#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License.
 */
#endregion

using System;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	public static class StealthLocalActorCachePolicy
	{
		public static int LocalRadiusCells(int dangerRadius, int strategicCellSize, int padding)
		{
			return Math.Max(dangerRadius + padding, strategicCellSize * 3 + padding);
		}

		public static int CoveringRadiusCells(int localRadius, int maximumWeaponRange, int padding)
		{
			return Math.Max(localRadius, maximumWeaponRange + padding);
		}

		public static int RefreshInterval(int baseInterval, int maximumInterval, int planningFactor)
		{
			return Math.Min(maximumInterval, (int)Math.Min(int.MaxValue,
				(long)baseInterval * Math.Max(1, planningFactor)));
		}

		public static int MovementBufferCells(int localRadius)
		{
			return Math.Max(1, localRadius / 4);
		}

		public static bool RequiresRefresh(bool hasRoster, int currentTick, int refreshTick,
			CPos missionCell, CPos cachedMissionCell, CPos centerCell, CPos cachedCenterCell,
			int movementBufferCells)
		{
			if (!hasRoster || currentTick >= refreshTick || missionCell != cachedMissionCell)
				return true;

			var dx = (long)centerCell.X - cachedCenterCell.X;
			var dy = (long)centerCell.Y - cachedCenterCell.Y;
			return dx * dx + dy * dy > (long)movementBufferCells * movementBufferCells;
		}
	}
}
