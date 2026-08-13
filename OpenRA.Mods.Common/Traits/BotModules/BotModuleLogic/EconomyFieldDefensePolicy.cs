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

namespace OpenRA.Mods.Common.Traits
{
	public readonly struct HarvesterFieldContextState
	{
		public readonly bool HasPending;
		public readonly CPos Pending;
		public readonly bool HasCommitted;
		public readonly CPos Committed;

		public HarvesterFieldContextState(bool hasPending, CPos pending, bool hasCommitted, CPos committed)
		{
			HasPending = hasPending;
			Pending = pending;
			HasCommitted = hasCommitted;
			Committed = committed;
		}
	}

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

	public readonly struct EconomyDefenseDirtyAssignment
	{
		public readonly uint FieldId;
		public readonly uint ActorId;

		public EconomyDefenseDirtyAssignment(uint fieldId, uint actorId)
		{
			FieldId = fieldId;
			ActorId = actorId;
		}
	}

	/// <summary>
	/// Deduplicates urgent field-defense validity work without polling world state.
	/// Entries drain in actor-id order so damage callback order cannot affect orders.
	/// </summary>
	public sealed class EconomyFieldDefenseDirtyAssignments
	{
		readonly Dictionary<uint, HashSet<uint>> actorsByField = new Dictionary<uint, HashSet<uint>>();

		public int Count => actorsByField.Values.Sum(actors => actors.Count);

		public bool Enqueue(uint fieldId, uint actorId)
		{
			if (!actorsByField.TryGetValue(fieldId, out var actors))
			{
				actors = new HashSet<uint>();
				actorsByField.Add(fieldId, actors);
			}

			return actors.Add(actorId);
		}

		public EconomyDefenseDirtyAssignment[] Drain()
		{
			var pending = Snapshot();
			actorsByField.Clear();
			return pending;
		}

		public EconomyDefenseDirtyAssignment[] Snapshot()
		{
			return actorsByField.OrderBy(pair => pair.Key)
				.SelectMany(pair => pair.Value.OrderBy(actorId => actorId)
					.Select(actorId => new EconomyDefenseDirtyAssignment(pair.Key, actorId))).ToArray();
		}

		public void RemoveField(uint fieldId) { actorsByField.Remove(fieldId); }

		public void Clear() { actorsByField.Clear(); }
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
		public static HarvesterFieldContextState Harvested(HarvesterFieldContextState state, CPos actualCell)
		{
			return new HarvesterFieldContextState(true, actualCell, state.HasCommitted, state.Committed);
		}

		public static HarvesterFieldContextState UnloadCompleted(HarvesterFieldContextState state, bool isEmpty)
		{
			if (!isEmpty || !state.HasPending)
				return state;

			return new HarvesterFieldContextState(false, state.Pending, true, state.Pending);
		}

		public static HarvesterFieldContextState UnloadAborted(HarvesterFieldContextState state)
		{
			return state;
		}

		public static int RoleDemand(int harvesterCount, int countPerHarvester)
		{
			return Math.Max(0, harvesterCount) * Math.Max(0, countPerHarvester);
		}

		public static int OutstandingRequestDemand(int target, int assigned, int queued, int ownedRequests, int maximumOutstanding)
		{
			var missing = Math.Max(0, target - assigned - queued - ownedRequests);
			return Math.Min(Math.Max(0, maximumOutstanding - ownedRequests), missing);
		}

		public static bool ShouldReform(long distanceSquared, int toleranceCells, int leashCells)
		{
			var leash = Math.Max(toleranceCells, leashCells) * 1024L;
			return distanceSquared > leash * leash;
		}

		public static bool IsWithinFormation(CPos currentCell, CPos destinationCell,
			long distanceSquared, int toleranceCells)
		{
			var tolerance = Math.Max(0, toleranceCells) * 1024L;
			return currentCell == destinationCell || distanceSquared <= tolerance * tolerance;
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

		public static int ProjectedResourceHazardRadius(int activeModifierRangeCells, int safetyMarginCells)
		{
			return Math.Max(0, activeModifierRangeCells) + Math.Max(0, safetyMarginCells);
		}

		public static bool RequiresProjectedResourceSafety(bool isInfantry)
		{
			return isInfantry;
		}

		public static bool IsMateriallyNewUrgentTarget(CPos previous, CPos current, int deduplicationRadiusCells)
		{
			var radius = Math.Max(0, deduplicationRadiusCells);
			return (current - previous).LengthSquared > radius * radius;
		}

		public static int RestoredScanTicks(int nextScanTick, int currentWorldTick, int scanInterval)
		{
			var interval = Math.Max(1, scanInterval);
			if (nextScanTick < 0)
				return 1;

			var next = (long)nextScanTick;
			var current = (long)currentWorldTick;
			if (next <= current)
				next += ((current - next) / interval + 1) * interval;

			return (int)Math.Min(interval, Math.Max(1, next - current));
		}
	}
}
