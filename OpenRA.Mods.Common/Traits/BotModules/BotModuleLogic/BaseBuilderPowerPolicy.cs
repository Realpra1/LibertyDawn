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
	public static class BaseBuilderPowerPolicy
	{
		public static int SecondsToTicks(int seconds, int gameTimeTimestep)
		{
			var safeTimestep = Math.Max(1, gameTimeTimestep);
			var milliseconds = Math.Max(0L, seconds) * 1000L;
			return (int)Math.Min(int.MaxValue,
				(milliseconds + safeTimestep - 1) / safeTimestep);
		}

		public static int TargetExcessPower(int worldTick, int gameTimeTimestep, bool criticalRecovery,
			int minimum, int maximum, int delayedBuffer, int delaySeconds,
			int increment, int incrementThreshold, int buildingCount)
		{
			var safeMinimum = Math.Max(0, minimum);
			var safeMaximum = Math.Max(safeMinimum, maximum);
			var delayTicks = SecondsToTicks(delaySeconds, gameTimeTimestep);
			if (criticalRecovery || worldTick < delayTicks)
				return safeMinimum;

			var threshold = Math.Max(1, incrementThreshold);
			var buildingBonus = Math.Max(0, increment) * (Math.Max(0, buildingCount) / threshold);
			var target = (long)safeMinimum + Math.Max(0, delayedBuffer) + buildingBonus;
			return (int)Math.Min(safeMaximum, target);
		}
	}
}
