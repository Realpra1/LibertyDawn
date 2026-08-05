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
using System.Collections.Generic;

namespace OpenRA.Mods.Common.Traits
{
	public readonly struct SmartEconomyPressure
	{
		public readonly int EvidenceTicks;
		public readonly bool Active;

		public SmartEconomyPressure(int evidenceTicks, bool active)
		{
			EvidenceTicks = Math.Max(0, evidenceTicks);
			Active = active;
		}
	}

	/// <summary>
	/// Pure deterministic policy used by the base builder's bounded economy sampler.
	/// Keeping time accumulation and expansion sizing independent of World makes the
	/// thresholds easy to verify without changing resource or harvester mechanics.
	/// </summary>
	public static class SmartEconomyPolicy
	{
		public static SmartEconomyPressure UpdatePressure(SmartEconomyPressure current,
			bool pressureObserved, int elapsedTicks, int activationTicks, int releaseTicks)
		{
			var elapsed = Math.Max(1, elapsedTicks);
			var activation = Math.Max(1, activationTicks);
			var release = Math.Clamp(releaseTicks, 0, activation - 1);
			var evidence = pressureObserved ? Math.Min(activation, current.EvidenceTicks + elapsed) :
				Math.Max(0, current.EvidenceTicks - elapsed);
			var active = current.Active ? evidence > release : evidence >= activation;
			return new SmartEconomyPressure(evidence, active);
		}

		public static int WaitingHarvesters(int nearbyLinkedHarvesters, int simultaneousServiceSlots = 1)
		{
			return Math.Max(0, nearbyLinkedHarvesters - Math.Max(0, simultaneousServiceSlots));
		}

		public static int WaitingHarvestersWhenAllRefineriesOccupied(IEnumerable<int> nearbyLinkedHarvesters,
			int simultaneousServiceSlots = 1)
		{
			var refineryCount = 0;
			var waitingHarvesters = 0;
			foreach (var linkedHarvesters in nearbyLinkedHarvesters)
			{
				refineryCount++;
				if (linkedHarvesters <= 0)
					return 0;

				waitingHarvesters += WaitingHarvesters(linkedHarvesters, simultaneousServiceSlots);
			}

			return refineryCount > 0 ? waitingHarvesters : 0;
		}

		public static bool StoragePressure(int storedResources, int resourceCapacity, int thresholdPercent)
		{
			if (resourceCapacity <= 0)
				return false;

			var threshold = Math.Clamp(thresholdPercent, 0, 100);
			return (long)Math.Max(0, storedResources) * 100 >= (long)resourceCapacity * threshold;
		}

		public static int DesiredExpansionAssets(int spendableCash, int threshold, int maximumAssets)
		{
			if (threshold <= 0 || spendableCash < threshold || maximumAssets <= 0)
				return 0;

			// The first sustained threshold funds one additional base. Each complete threshold
			// beyond that raises the target again until the authored safety ceiling is reached.
			var desired = 1 + Math.Max(1, spendableCash / threshold);
			return Math.Min(maximumAssets, desired);
		}

		public static bool ExpansionArmyReady(int armyValue, int assetValue, int minimumPercent)
		{
			if (minimumPercent <= 0 || assetValue <= 0)
				return true;

			return (long)Math.Max(0, armyValue) * 100 >=
				(long)Math.Max(0, assetValue) * Math.Clamp(minimumPercent, 0, 100);
		}
	}
}
