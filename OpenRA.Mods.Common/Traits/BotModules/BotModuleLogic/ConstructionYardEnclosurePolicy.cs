#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	public sealed class ConstructionYardEnclosurePlan
	{
		public CPos[] WallCells { get; }
		public CPos[] AccessCells { get; }
		public CPos[][] WallSegments { get; }

		public ConstructionYardEnclosurePlan(CPos[] wallCells, CPos[] accessCells, CPos[][] wallSegments)
		{
			WallCells = wallCells;
			AccessCells = accessCells;
			WallSegments = wallSegments;
		}
	}

	/// <summary>
	/// Owns one pending enclosure-wall build from selection through placement. A wall type alone
	/// is insufficient once a second Fact contributes another construction queue: only the queue
	/// that reserved the current endpoint may consume it.
	/// </summary>
	public sealed class ConstructionYardEnclosureBuildOwnership<TQueue> where TQueue : class
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

		public bool Refresh(int currentTick, int timeout, Func<TQueue, bool> queueIsAvailable,
			Func<TQueue, string, bool> matchingBuildIsQueued)
		{
			if (!HasReservation)
				return false;

			if (queueIsAvailable(queue) &&
				(matchingBuildIsQueued(queue, actorType) ||
				currentTick - reservedTick < Math.Max(1, timeout)))
				return false;

			Release();
			return true;
		}

		public void Release()
		{
			queue = null;
			actorType = null;
			reservedTick = 0;
		}
	}

	/// <summary>Deterministic, world-independent policy for the bounded first-Fact enclosure.</summary>
	public static class ConstructionYardEnclosurePolicy
	{
		public const int MaximumInitialRetries = 8;

		public static int NextInitialRetryCount(int currentCount)
		{
			return Math.Min(MaximumInitialRetries, Math.Max(0, currentCount) + 1);
		}

		public static bool InitialRetryLimitReached(int retryCount)
		{
			return retryCount >= MaximumInitialRetries;
		}

		public static bool IssuedCellRetryDue(int currentTick, int issuedTick, int retryInterval)
		{
			return currentTick - issuedTick >= Math.Max(1, retryInterval);
		}

		public static bool IsSatisfiedCell(bool hasWall, bool terrainSealsCell)
		{
			return hasWall || terrainSealsCell;
		}

		public static ConstructionYardEnclosurePlan CreatePlan(CPos topLeft, CVec dimensions, int margin, int accessWidth)
		{
			var corners = BotWallGeometry.EnclosureCorners(topLeft, dimensions, margin);
			var left = corners[0].X;
			var top = corners[0].Y;
			var right = corners[2].X;
			var bottom = corners[2].Y;
			var sideWidth = right - left + 1;
			var gateWidth = accessWidth.Clamp(1, Math.Max(1, sideWidth - 2));
			var gateStart = left + (sideWidth - gateWidth) / 2;

			// These are virtual path-probe origins outside the enclosure, not an entrance.
			// Facts do not produce moving units, so the completed wall must be a full ring.
			var access = Enumerable.Range(gateStart, gateWidth)
				.Select(x => new CPos(x, bottom + 1)).ToArray();
			var segments = new List<CPos[]>();
			AddSegment(segments, Enumerable.Range(left, sideWidth).Select(x => new CPos(x, top)));
			AddSegment(segments, Range(top + 1, bottom - 1).Select(y => new CPos(right, y)));
			AddSegment(segments, ReverseRange(left, right).Select(x => new CPos(x, bottom)));
			AddSegment(segments, ReverseRange(top + 1, bottom - 1).Select(y => new CPos(left, y)));

			return new ConstructionYardEnclosurePlan(segments.SelectMany(s => s).ToArray(), access,
				segments.ToArray());
		}

		static IEnumerable<int> Range(int first, int last)
		{
			for (var value = first; value <= last; value++)
				yield return value;
		}

		static IEnumerable<int> ReverseRange(int first, int last)
		{
			for (var value = last; value >= first; value--)
				yield return value;
		}

		static void AddSegment(List<CPos[]> segments, IEnumerable<CPos> cells)
		{
			var segment = cells.ToArray();
			if (segment.Length > 0)
				segments.Add(segment);
		}

		public static CPos[] FirstLegalMissingRun(ConstructionYardEnclosurePlan plan,
			Func<CPos, bool> isPresent, Func<CPos, bool> isLegal)
		{
			if (plan == null)
				return Array.Empty<CPos>();

			foreach (var segment in plan.WallSegments)
			{
				var run = new List<CPos>();
				foreach (var cell in segment)
				{
					if (!isPresent(cell) && isLegal(cell))
					{
						run.Add(cell);
						continue;
					}

					if (run.Count > 0)
						return run.ToArray();
				}

				if (run.Count > 0)
					return run.ToArray();
			}

			return Array.Empty<CPos>();
		}

		public static CPos[] OrderedLegalMissingCells(ConstructionYardEnclosurePlan plan,
			CPos yardLocation, Func<CPos, bool> isPresent, Func<CPos, bool> isLegal)
		{
			if (plan == null || isPresent == null || isLegal == null || plan.WallCells.Length == 0)
				return Array.Empty<CPos>();

			var left = plan.WallCells.Min(c => c.X);
			var right = plan.WallCells.Max(c => c.X);
			var top = plan.WallCells.Min(c => c.Y);
			var bottom = plan.WallCells.Max(c => c.Y);
			var present = plan.WallCells.Where(isPresent).ToArray();

			return plan.WallCells.Select((cell, index) => new { Cell = cell, Index = index })
				.Where(candidate => !isPresent(candidate.Cell) && isLegal(candidate.Cell))

				// Spread paid anchors as far apart as possible.  LineBuild can then fill the legal
				// cells between them for free instead of stacking the next paid segment beside the
				// last one.  With no walls yet the corner/stable-plan ordering remains deterministic.
				.OrderByDescending(candidate => present.Length == 0 ? 0 :
					present.Min(wall => (candidate.Cell - wall).LengthSquared))
				.ThenBy(candidate => !((candidate.Cell.X == left || candidate.Cell.X == right) &&
					(candidate.Cell.Y == top || candidate.Cell.Y == bottom)))
				.ThenBy(candidate => candidate.Index)
				.ThenBy(candidate => candidate.Cell.X)
				.ThenBy(candidate => candidate.Cell.Y)
				.Select(candidate => candidate.Cell).ToArray();
		}

		public static bool IsSafeLineBuildConnection(ConstructionYardEnclosurePlan plan,
			CPos first, CPos second)
		{
			if (plan == null || first.Layer != second.Layer ||
				(first.X != second.X && first.Y != second.Y))
				return false;

			var step = new CVec(Math.Sign(second.X - first.X), Math.Sign(second.Y - first.Y));
			var cell = first;
			while (true)
			{
				if (!plan.WallCells.Contains(cell))
					return false;
				if (cell == second)
					return true;
				cell += step;
			}
		}

		/// <summary>
		/// Validates the native pathfinder contract: paths are returned destination-to-source.
		/// The predicates let the live planner reject dynamic blockers and the bound Fact footprint
		/// without making this deterministic policy depend on World or actor state.
		/// </summary>
		public static bool IsExactReversedRoute(IEnumerable<CPos> route, CPos origin, CPos destination,
			Func<CPos, bool> isBlocked, Func<CPos, bool> isFactFootprint)
		{
			if (route == null || isBlocked == null || isFactFootprint == null)
				return false;

			var cells = route.ToArray();
			if (cells.Length < 2 || cells[0] != destination || cells[cells.Length - 1] != origin ||
				cells.Distinct().Count() != cells.Length)
				return false;

			for (var i = 0; i < cells.Length; i++)
			{
				if (isBlocked(cells[i]) || isFactFootprint(cells[i]))
					return false;

				if (i == 0)
					continue;

				var step = cells[i] - cells[i - 1];
				if (cells[i].Layer != cells[i - 1].Layer || step.LengthSquared < 1 || step.LengthSquared > 2)
					return false;
			}

			return true;
		}

		public static bool TryFirstExactRoute(IEnumerable<CPos> accessCells, CPos yardLocation, CPos destination,
			Func<CPos, bool> canEnter, Func<CPos, CPos, IEnumerable<CPos>> findRoute,
			Func<CPos, bool> isBlocked, Func<CPos, bool> isFactFootprint,
			out CPos origin, out CPos[] route)
		{
			origin = default;
			route = null;
			if (accessCells == null || canEnter == null || findRoute == null ||
				isBlocked == null || isFactFootprint == null || !canEnter(destination))
				return false;

			foreach (var candidate in accessCells.Where(canEnter)
				.OrderBy(c => (c - yardLocation).LengthSquared)
				.ThenBy(c => c.X).ThenBy(c => c.Y))
			{
				var found = findRoute(candidate, destination)?.ToArray();
				if (!IsExactReversedRoute(found, candidate, destination, isBlocked, isFactFootprint))
					continue;

				origin = candidate;
				route = found;
				return true;
			}

			return false;
		}

		public static string FirstAvailableType(IEnumerable<string> preferenceOrder, Func<string, bool> isAvailable)
		{
			if (preferenceOrder == null || isAvailable == null)
				return null;

			return preferenceOrder.FirstOrDefault(isAvailable);
		}

		public static bool Overlaps(ConstructionYardEnclosurePlan plan, IEnumerable<CPos> occupiedCells)
		{
			if (plan == null || occupiedCells == null)
				return false;

			var walls = new HashSet<CPos>(plan.WallCells);
			return occupiedCells.Any(walls.Contains);
		}

		public static CPos? FirstLegalUnreservedCell(IEnumerable<CPos> cells,
			Func<CPos, bool> isLegal, Func<CPos, bool> isReserved)
		{
			if (cells == null || isLegal == null || isReserved == null)
				return null;

			foreach (var cell in cells)
				if (isLegal(cell) && !isReserved(cell))
					return cell;

			return null;
		}

		public static bool MatchesSavedPlan(ConstructionYardEnclosurePlan expected,
			IEnumerable<CPos> wallCells, IEnumerable<CPos> accessCells)
		{
			return expected != null && wallCells != null && accessCells != null &&
				expected.WallCells.SequenceEqual(wallCells) && expected.AccessCells.SequenceEqual(accessCells);
		}

		public static int[] EncodeCells(IEnumerable<CPos> cells)
		{
			return cells?.Select(c => c.Bits).ToArray() ?? Array.Empty<int>();
		}

		public static CPos[] DecodeCells(IEnumerable<int> bits)
		{
			return bits?.Select(b => new CPos(b)).ToArray() ?? Array.Empty<CPos>();
		}

		public static bool IsValidWallCellSubset(ConstructionYardEnclosurePlan plan,
			IEnumerable<CPos> cells, int maximumCount)
		{
			if (plan == null || cells == null || maximumCount < 0)
				return false;

			var retained = cells.ToArray();
			if (retained.Length > maximumCount || retained.Distinct().Count() != retained.Length)
				return false;

			var wallCells = new HashSet<CPos>(plan.WallCells);
			return retained.All(wallCells.Contains);
		}

		public static bool IsValidSavedTick(int savedTick, int currentWorldTick)
		{
			return savedTick >= 0 && currentWorldTick >= 0 && savedTick <= currentWorldTick;
		}

		public static int QueuePollDelay(int normalDelay, int maintenanceInterval, bool enclosureActive)
		{
			if (!enclosureActive)
				return normalDelay;

			return Math.Min(Math.Max(1, normalDelay), Math.Max(1, maintenanceInterval));
		}

		public static bool IsActive(int worldTick, int cutoffTick, bool bound, bool stopped)
		{
			return bound && !stopped && worldTick < Math.Max(0, cutoffTick);
		}

		public static uint? SelectInitialYardActorId(IEnumerable<uint> liveYardActorIds, bool mayBind)
		{
			if (!mayBind || liveYardActorIds == null)
				return null;

			return liveYardActorIds.OrderBy(id => id).Select(id => (uint?)id).FirstOrDefault();
		}
	}
}
