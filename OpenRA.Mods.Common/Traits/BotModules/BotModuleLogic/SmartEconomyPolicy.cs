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

	public readonly struct SmartEconomyRefineryDemand
	{
		public readonly int CommittedHarvesters;
		public readonly int CommittedRefineries;
		public readonly int DesiredRefineries;
		public readonly int Deficit;
		public readonly int AvailableRequests;

		public SmartEconomyRefineryDemand(int committedHarvesters, int committedRefineries,
			int desiredRefineries, int deficit, int availableRequests)
		{
			CommittedHarvesters = Math.Max(0, committedHarvesters);
			CommittedRefineries = Math.Max(0, committedRefineries);
			DesiredRefineries = Math.Max(0, desiredRefineries);
			Deficit = Math.Max(0, deficit);
			AvailableRequests = Math.Max(0, availableRequests);
		}
	}

	/// <summary>
	/// Pure deterministic policy used by the base builder's bounded economy sampler.
	/// Keeping time accumulation and expansion sizing independent of World makes the
	/// thresholds easy to verify without changing resource or harvester mechanics.
	/// </summary>
	public static class SmartEconomyPolicy
	{
		public static int PostLoadResumeTick(int currentTick, int settleTicks)
		{
			return (int)Math.Min(int.MaxValue,
				(long)Math.Max(0, currentTick) + Math.Max(1, settleTicks));
		}

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

		public static bool WantsNeedBasedSilo(bool enabled, int storedResources, int resourceCapacity,
			int thresholdPercent)
		{
			return enabled && StoragePressure(storedResources, resourceCapacity, thresholdPercent);
		}

		public static SmartEconomyRefineryDemand RefineryDemand(int liveHarvesters, int queuedHarvesters,
			int requestedHarvesters, int liveRefineries, int queuedRefineries, int reservedRefineries,
			int freeHarvestersPerPendingRefinery, int harvestersPerRefinery, int maximumParallelRefineries,
			bool sustainedCongestion = false)
		{
			var pendingRefineries = Math.Max(0, queuedRefineries) + Math.Max(0, reservedRefineries);
			var committedRefineries = Math.Max(0, liveRefineries) + pendingRefineries;
			var committedHarvesters = Math.Max(0, liveHarvesters) + Math.Max(0, queuedHarvesters) +
				Math.Max(0, requestedHarvesters) + pendingRefineries * Math.Max(0, freeHarvestersPerPendingRefinery);
			var refineryCapacity = Math.Max(1, harvestersPerRefinery);
			var ratioTarget = (committedHarvesters + refineryCapacity - 1) / refineryCapacity;
			var congestionTarget = Math.Max(0, liveRefineries) + (sustainedCongestion ? 1 : 0);
			var desired = Math.Max(congestionTarget, ratioTarget);
			var deficit = Math.Max(0, desired - committedRefineries);
			var parallelCapacity = Math.Max(0, maximumParallelRefineries) - pendingRefineries;

			return new SmartEconomyRefineryDemand(committedHarvesters, committedRefineries, desired,
				deficit, Math.Min(deficit, Math.Max(0, parallelCapacity)));
		}

		public static int RefineryCashShortfall(int spendableCash, int refineryCost,
			int liveRefineries, int queuedRefineries, int reservedRefineries, int idleRefineryQueues)
		{
			if (liveRefineries > 0 || queuedRefineries > 0 || reservedRefineries > 0 || idleRefineryQueues <= 0)
				return 0;

			return Math.Max(0, Math.Max(0, refineryCost) - Math.Max(0, spendableCash));
		}

		public static bool NeedsSerializedRefineryRecovery(bool enabled, bool hadUsableRefinery,
			int liveRefineries, int queuedRefineries = 0, int reservedRefineries = 0)
		{
			return enabled && hadUsableRefinery && liveRefineries <= 0 &&
				queuedRefineries <= 0 && reservedRefineries <= 0;
		}

		public static bool NeedsFirstRefineryCommitment(bool enabled, int liveRefineries,
			int queuedRefineries, int reservedRefineries)
		{
			return enabled && liveRefineries <= 0 && queuedRefineries <= 0 && reservedRefineries <= 0;
		}

		public static bool CanFundRefinery(int spendableCash, int queuedRemainingCost,
			int reservedCost, int refineryCost)
		{
			var committedCost = (long)Math.Max(0, queuedRemainingCost) + Math.Max(0, reservedCost) +
				Math.Max(0, refineryCost);
			return Math.Max(0, spendableCash) >= committedCost;
		}

		public static bool CanStartThroughputRefinery(int spendableCash, int queuedRemainingCost,
			int reservedCost, int refineryCost, int pendingRefineries)
		{
			// A low-cash AI with busy unit queues may never accumulate the full price at one
			// instant. Permit one streaming construction commitment, but require every
			// additional parallel commitment to be fully funded.
			return Math.Max(0, pendingRefineries) == 0 || CanFundRefinery(spendableCash,
				queuedRemainingCost, reservedCost, refineryCost);
		}

		public static int DesiredEarlyVehicleFactories(int activeFactQueues, int vehicleFactoryPercent)
		{
			var facts = Math.Max(0, activeFactQueues);
			var percent = Math.Clamp(vehicleFactoryPercent, 0, 100);
			return (int)Math.Min(int.MaxValue, ((long)facts * percent + 99) / 100);
		}

		public static int DesiredVehicleFactoriesForRefineryBalance(int committedRefineries, int vehicleFactoryPercent)
		{
			var refineries = Math.Max(0, committedRefineries);
			var percent = Math.Clamp(vehicleFactoryPercent, 0, 100);
			if (refineries == 0 || percent == 0)
				return 0;

			if (percent == 100)
				return int.MaxValue;

			var refineryPercent = 100 - percent;
			return (int)Math.Min(int.MaxValue,
				((long)refineries * percent + refineryPercent - 1) / refineryPercent);
		}

		public static int EffectiveParallelRefineryLimit(int activeFactQueues,
			int configuredLimit, int vehicleFactoryPercent, bool preserveAlternativeConstruction)
		{
			var usableQueues = Math.Max(0, activeFactQueues);
			if (preserveAlternativeConstruction)
			{
				var reservedVehicleSlots = DesiredEarlyVehicleFactories(usableQueues, vehicleFactoryPercent);
				usableQueues = Math.Max(0, usableQueues - reservedVehicleSlots);
			}

			return Math.Min(Math.Max(0, configuredLimit), usableQueues);
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
