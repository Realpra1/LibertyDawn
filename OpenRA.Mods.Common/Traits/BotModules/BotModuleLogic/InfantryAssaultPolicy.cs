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
	public static class InfantryAssaultPolicy
	{
		public static bool SelectStrategy(bool eligibleBot, int selectionPercent, int roll)
		{
			return eligibleBot && selectionPercent > 0 && roll >= 0 && roll < selectionPercent;
		}

		public static bool ReadyToTravel(int loadedPassengers, int plannedPassengers, int minimumPassengers,
			int gatheringTicks, int gatheringTimeoutTicks)
		{
			return loadedPassengers >= minimumPassengers &&
				(loadedPassengers >= plannedPassengers || gatheringTicks >= gatheringTimeoutTicks);
		}

		public static bool AbandonGathering(int loadedPassengers, int minimumPassengers,
			int gatheringTicks, int gatheringTimeoutTicks)
		{
			return loadedPassengers < minimumPassengers && gatheringTicks >= gatheringTimeoutTicks;
		}

		public static long TargetScore(int economicValue, int distanceCells)
		{
			return Math.Max(0, economicValue) * 1000L / Math.Max(1, distanceCells + 8);
		}
	}
}
