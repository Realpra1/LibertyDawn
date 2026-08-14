#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public readonly struct EconomyDefenseSamAnchor
	{
		public readonly uint ActorId;
		public readonly int Priority;
		public readonly CPos Cell;

		public EconomyDefenseSamAnchor(uint actorId, int priority, CPos cell)
		{
			ActorId = actorId;
			Priority = priority;
			Cell = cell;
		}
	}

	public readonly struct EconomyDefenseSamCoverage
	{
		public readonly CPos Cell;
		public readonly int RadiusCells;

		public EconomyDefenseSamCoverage(CPos cell, int radiusCells)
		{
			Cell = cell;
			RadiusCells = Math.Max(0, radiusCells);
		}
	}

	/// <summary>
	/// Tracks the exact production queue and actor type reserved by economy SAM demand.
	/// Actor type alone is insufficient because ordinary authored SAM production must keep
	/// using the normal BaseBuilder placement policy.
	/// </summary>
	public sealed class EconomyDefenseSamBuildOwnership<TQueue> where TQueue : class
	{
		TQueue queue;
		string actorType;
		int reservedTick;

		public bool HasReservation => queue != null;
		public TQueue ReservedQueue => queue;
		public string ReservedActorType => actorType;
		public int ReservedTick => reservedTick;

		public bool TryReserve(TQueue candidateQueue, string candidateActorType, int currentTick)
		{
			if (candidateQueue == null || string.IsNullOrEmpty(candidateActorType) || HasReservation)
				return false;

			queue = candidateQueue;
			actorType = candidateActorType;
			reservedTick = currentTick;
			return true;
		}

		public bool TryRestore(TQueue candidateQueue, string candidateActorType, int candidateReservedTick,
			Func<TQueue, bool> queueIsAvailable, Func<TQueue, string, bool> matchingBuildIsQueued)
		{
			if (candidateQueue == null || string.IsNullOrEmpty(candidateActorType) || HasReservation ||
				queueIsAvailable == null || matchingBuildIsQueued == null ||
				!queueIsAvailable(candidateQueue) || !matchingBuildIsQueued(candidateQueue, candidateActorType))
				return false;

			queue = candidateQueue;
			actorType = candidateActorType;
			reservedTick = candidateReservedTick;
			return true;
		}

		public bool Owns(TQueue candidateQueue, string candidateActorType)
		{
			return ReferenceEquals(queue, candidateQueue) &&
				string.Equals(actorType, candidateActorType, StringComparison.Ordinal);
		}

		public void Refresh(int currentTick, int timeout, Func<TQueue, bool> queueIsAvailable,
			Func<TQueue, string, bool> matchingBuildIsQueued)
		{
			if (!HasReservation)
				return;

			if (!queueIsAvailable(queue) ||
				(!matchingBuildIsQueued(queue, actorType) && currentTick - reservedTick >= Math.Max(1, timeout)))
			{
				queue = null;
				actorType = null;
				reservedTick = 0;
			}
		}
	}

	/// <summary>World-independent transition and demand rules for economy field defense.</summary>
	public static class EconomyFieldDefensePolicy
	{
		public static bool RequiresAggressiveStance(UnitStance stance)
		{
			return stance != UnitStance.AttackAnything;
		}

		public static bool HasActionableAttack(AttackInfo attack)
		{
			return attack?.Damage != null && attack.Damage.Value > 0 && attack.Attacker != null;
		}

		public static int MissingProductionDemand(int target, int assigned, int queued, int ownedRequests)
		{
			return Math.Max(0, target - assigned - queued - ownedRequests);
		}

		public static int ReinforcementIntervalTicks(int seconds, int timestep)
		{
			var safeTimestep = Math.Max(1, timestep);
			var milliseconds = Math.Max(1, seconds) * 1000;
			return Math.Max(1, (milliseconds + safeTimestep - 1) / safeTimestep);
		}

		public static EconomyDefenseSamAnchor? FirstUncoveredSamAnchor(
			IEnumerable<EconomyDefenseSamAnchor> anchors, IEnumerable<EconomyDefenseSamCoverage> coverage)
		{
			var sites = coverage.ToArray();
			foreach (var anchor in anchors.OrderBy(a => a.Priority).ThenBy(a => a.ActorId))
			{
				var covered = sites.Any(site =>
				{
					var radiusSquared = site.RadiusCells * site.RadiusCells;
					return (site.Cell - anchor.Cell).LengthSquared <= radiusSquared;
				});

				if (!covered)
					return anchor;
			}

			return null;
		}

		public static bool ShouldRequestSam(bool enabled, bool hasSufficientPower, int liveSites,
			int pendingSites, int maximumSites, bool hasUncoveredAnchor)
		{
			return enabled && hasSufficientPower && hasUncoveredAnchor && liveSites >= 0 && pendingSites == 0 &&
				maximumSites > 0 && liveSites + pendingSites < maximumSites;
		}
	}
}
