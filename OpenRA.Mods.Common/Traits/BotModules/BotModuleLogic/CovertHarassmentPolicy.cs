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
	/// <summary>World-independent formation and target policy for the fast covert harassment squad.</summary>
	public static class CovertHarassmentPolicy
	{
		public static int SupportCount(int coreCount, int coresPerSupport, int maximumSupport, int availableSupport)
		{
			if (coreCount <= 0 || coresPerSupport <= 0 || maximumSupport <= 0 || availableSupport <= 0)
				return 0;

			var desired = (coreCount + coresPerSupport - 1) / coresPerSupport;
			return Math.Min(availableSupport, Math.Min(maximumSupport, desired));
		}

		public static bool CanSelectTarget(bool isTower, int supportCount)
		{
			return !isTower || supportCount > 0;
		}

		public static bool ShouldWaitForSupport(bool isTower, int supportCount, int readySupportCount)
		{
			return isTower && supportCount > 0 && readySupportCount < supportCount;
		}

		public static long TargetScore(int priority, int value, long distanceSquared, bool incumbent)
		{
			if (priority <= 0)
				return 0;

			var distanceCellsSquared = Math.Max(0, distanceSquared / (1024L * 1024L));
			var score = (long)priority * 1000000 + Math.Max(1, value) * 1000L - Math.Min(999999, distanceCellsSquared);
			return incumbent ? score * 110 / 100 : score;
		}

		public static bool ShouldIssueOrders(bool targetChanged, int currentTick, int lastOrderTick, int interval)
		{
			return targetChanged || interval <= 0 || currentTick >= lastOrderTick + interval;
		}
	}
}
