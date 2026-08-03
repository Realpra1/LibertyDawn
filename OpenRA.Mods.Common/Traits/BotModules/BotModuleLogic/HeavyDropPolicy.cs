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
	public static class HeavyDropPolicy
	{
		public static bool CanPrepare(bool eligibleBot, int worldTick, int minimumGameTicks,
			int passengerCount, int transportCount, int desiredCount)
		{
			return eligibleBot && worldTick >= minimumGameTicks &&
				passengerCount >= desiredCount && transportCount >= desiredCount;
		}

		public static bool ReadyToTravel(int loadedCount, int plannedCount, int minimumCount,
			int gatheringTicks, int gatheringTimeoutTicks)
		{
			return loadedCount >= minimumCount &&
				(loadedCount >= plannedCount || gatheringTicks >= gatheringTimeoutTicks);
		}

		public static bool IsDropSiteSafe(float aaDanger, float maximumAaDanger,
			int defenderValue, int maximumDefenderValue)
		{
			return aaDanger <= maximumAaDanger && defenderValue <= maximumDefenderValue;
		}

		public static int AvailableBoardingSlots(int concurrentLimit, int activeBoarding)
		{
			return Math.Max(0, concurrentLimit - Math.Max(0, activeBoarding));
		}

		public static long TargetScore(int targetValue, int defenderValue, int distanceCells, int behindDot)
		{
			var value = Math.Max(0, targetValue);
			var defense = Math.Max(0, defenderValue);
			var distance = Math.Max(0, distanceCells);
			return value * 10000L + Math.Max(0, behindDot) * 100L - defense * 10L - distance;
		}
	}
}
