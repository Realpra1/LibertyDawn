#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;

namespace OpenRA.Mods.Common.Traits
{
	public static class AirRepairCapacityPolicy
	{
		public static int DesiredBuildings(int repairableAircraft, int aircraftPerBuilding, int buildingLimit)
		{
			if (repairableAircraft <= 0 || aircraftPerBuilding <= 0 || buildingLimit <= 0)
				return 0;

			return Math.Min(buildingLimit, (repairableAircraft + aircraftPerBuilding - 1) / aircraftPerBuilding);
		}
	}
}
