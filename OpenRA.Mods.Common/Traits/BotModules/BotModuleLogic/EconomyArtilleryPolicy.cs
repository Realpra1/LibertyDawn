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
	/// <summary>Deterministic, world-independent composition and targeting policy for economy artillery.</summary>
	public static class EconomyArtilleryPolicy
	{
		/// <summary>
		/// Selects the whole-unit count closest to the configured share of artillery value.
		/// The minimum is used for roles such as the required first mobile anti-air escort.
		/// </summary>
		public static int EscortCount(int artilleryValue, int escortCost, int percent, int available, int minimum = 0)
		{
			if (artilleryValue <= 0 || escortCost <= 0 || percent < 0 || available <= 0)
				return 0;

			minimum = Math.Clamp(minimum, 0, available);
			var targetValue = (long)artilleryValue * percent;
			var lower = (int)Math.Min(available, targetValue / (escortCost * 100L));
			var upper = Math.Min(available, lower + 1);
			var lowerError = Math.Abs(targetValue - (long)lower * escortCost * 100);
			var upperError = Math.Abs(targetValue - (long)upper * escortCost * 100);
			var closest = upperError < lowerError ? upper : lower;
			return Math.Max(minimum, closest);
		}

		public static long TargetScore(int priority, int value, long distanceSquared)
		{
			if (priority <= 0)
				return 0;

			var distanceCellsSquared = Math.Max(0, distanceSquared / (1024L * 1024L));
			return (long)priority * 1000000 + Math.Max(1, value) * 1000L - Math.Min(999999, distanceCellsSquared);
		}

		/// <summary>Only structures are artillery objectives: armed emplacements outrank other blockers.</summary>
		public static int TargetPriority(bool isBuilding, bool isArmed)
		{
			return !isBuilding ? 0 : isArmed ? 3000 : 2000;
		}

		public static bool ShouldIssueOrders(bool targetChanged, int currentTick, int lastOrderTick, int interval)
		{
			return targetChanged || interval <= 0 || currentTick >= lastOrderTick + interval;
		}
	}
}
