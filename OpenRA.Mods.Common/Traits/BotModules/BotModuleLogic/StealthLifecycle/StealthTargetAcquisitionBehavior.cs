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
	/// <summary>Step 3: bounded A*-like search over the immutable strategic cache.</summary>
	public sealed class StealthTargetAcquisitionBehavior
	{
		public const int MaximumOptions = 10;
		public const int MaximumTravelSeconds = 30;
		public const int MaximumPrimitiveOperations = 65536;
		public const int MaximumFallbackSteps = 4;

		readonly StealthBehaviorHandoff handoff;
		readonly IStealthTargetAcquisitionCache cache;
		CPos? moveCloserDestination;

		public StealthTargetAcquisitionBehavior(StealthBehaviorHandoff handoff,
			IStealthTargetAcquisitionCache cache)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetAcquisition)
				throw new ArgumentException("TargetAcquisition requires its ownership.", nameof(handoff));
			this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
		}

		public StealthTargetAcquisitionResult Execute(CPos activeSquadCenter,
			CPos? incumbentStrategicCell = null)
		{
			var snapshot = cache.ReadSnapshot() ??
				throw new InvalidOperationException("The TargetAcquisition cache returned no snapshot.");
			if (!Contains(snapshot, activeSquadCenter) ||
				(incumbentStrategicCell.HasValue && !Contains(snapshot, incumbentStrategicCell.Value)))
				throw new ArgumentOutOfRangeException(nameof(activeSquadCenter));
			if (moveCloserDestination.HasValue &&
				!IsSameOrAdjacent(activeSquadCenter, moveCloserDestination.Value))
				return new StealthTargetAcquisitionResult(handoff, activeSquadCenter,
					incumbentStrategicCell, StealthTargetAcquisitionDisposition.MoveCloserAndRescan,
					Array.Empty<StealthTargetOption>(), moveCloserDestination, 0, 0);
			moveCloserDestination = null;

			var enemyCells = snapshot.EnemyStrategicCells.Distinct()
				.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
			var required = incumbentStrategicCell.HasValue ?
				Array.IndexOf(enemyCells, incumbentStrategicCell.Value) : -1;
			var search = StealthAIThreatGeometry.StartReachableTargetCellSearch(
				snapshot.Danger.ToArray(), snapshot.Width, snapshot.Height,
				activeSquadCenter.X, activeSquadCenter.Y, enemyCells, snapshot.RouteThreatPenalty,
				incumbentStrategicCell.HasValue ? MaximumOptions - 1 : MaximumOptions, required,
				MaximumTravelSeconds / snapshot.SecondsPerCostUnit);
			search.Advance(MaximumPrimitiveOperations);
			var reachable = search.Complete ? search.Result.Targets :
				new List<StealthAIThreatGeometry.ReachableTargetCell>();
			var candidates = reachable.Select(target => new
				{
					Cell = enemyCells[target.TargetIndex],
					TravelMilliseconds = ToTravelMilliseconds(target.RouteCost,
						snapshot.SecondsPerCostUnit)
				})
				.Where(candidate => candidate.TravelMilliseconds <= MaximumTravelSeconds * 1000)
				.OrderBy(candidate => candidate.TravelMilliseconds)
				.ThenBy(candidate => candidate.Cell.Y).ThenBy(candidate => candidate.Cell.X).ToArray();

			var options = new List<StealthTargetOption>(MaximumOptions);
			if (incumbentStrategicCell.HasValue)
			{
				var incumbent = candidates.FirstOrDefault(candidate =>
					candidate.Cell == incumbentStrategicCell.Value);
				options.Add(Option(snapshot, incumbentStrategicCell.Value,
					incumbent?.TravelMilliseconds, true));
			}

			foreach (var candidate in candidates)
			{
				if (options.Count == MaximumOptions)
					break;
				if (options.All(option => option.StrategicCell != candidate.Cell))
					options.Add(Option(snapshot, candidate.Cell, candidate.TravelMilliseconds, false));
			}

			var needsRescan = enemyCells.Length == 0 ||
				options.Count < Math.Min(MaximumOptions, enemyCells.Length);
			var disposition = !needsRescan ? StealthTargetAcquisitionDisposition.ReadyForValueFilter :
				enemyCells.Length == 0 ? StealthTargetAcquisitionDisposition.AwaitingCache :
				StealthTargetAcquisitionDisposition.MoveCloserAndRescan;
			var moveCloser = needsRescan && enemyCells.Length != 0 ?
				MoveCloser(activeSquadCenter, enemyCells, snapshot, reachable) : null;
			moveCloserDestination = moveCloser;
			return new StealthTargetAcquisitionResult(handoff, activeSquadCenter,
				incumbentStrategicCell, disposition, options, moveCloser,
				search.PrimitiveOperations, search.ExpandedCells);
		}

		static StealthTargetOption Option(StealthTargetAcquisitionCacheSnapshot snapshot,
			CPos cell, int? travelMilliseconds, bool incumbent)
		{
			return new StealthTargetOption(cell, travelMilliseconds, incumbent,
				snapshot.StrategicTargets.Where(target => target.StrategicCell == cell),
				snapshot.ThreatFacts.FirstOrDefault(facts => facts.StrategicCell == cell));
		}

		static CPos? MoveCloser(CPos start, IReadOnlyList<CPos> enemies,
			StealthTargetAcquisitionCacheSnapshot snapshot,
			IReadOnlyList<StealthAIThreatGeometry.ReachableTargetCell> reachable)
		{
			var routed = reachable.Where(target => target.Route.Count != 0)
				.OrderBy(target => target.RouteCost)
				.ThenBy(target => enemies[target.TargetIndex].Y)
				.ThenBy(target => enemies[target.TargetIndex].X).FirstOrDefault();
			if (routed != null)
				return routed.Route[Math.Min(MaximumFallbackSteps, routed.Route.Count) - 1];

			var targetCell = enemies.OrderBy(cell => DistanceSquared(start, cell))
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).First();
			var current = start;
			for (var i = 0; i < MaximumFallbackSteps; i++)
			{
				var currentDistance = DistanceSquared(current, targetCell);
				var next = Neighbors(current, snapshot.Width, snapshot.Height)
					.Where(cell => DistanceSquared(cell, targetCell) < currentDistance)
					.OrderBy(cell => DistanceSquared(cell, targetCell))
					.ThenBy(cell => snapshot.Danger[cell.Y * snapshot.Width + cell.X])
					.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).FirstOrDefault(current);
				if (next == current)
					break;
				current = next;
			}

			return current == start ? (CPos?)null : current;
		}

		static IEnumerable<CPos> Neighbors(CPos cell, int width, int height)
		{
			if (cell.X > 0) yield return new CPos(cell.X - 1, cell.Y);
			if (cell.X + 1 < width) yield return new CPos(cell.X + 1, cell.Y);
			if (cell.Y > 0) yield return new CPos(cell.X, cell.Y - 1);
			if (cell.Y + 1 < height) yield return new CPos(cell.X, cell.Y + 1);
		}

		static long DistanceSquared(CPos left, CPos right)
		{
			var dx = (long)left.X - right.X;
			var dy = (long)left.Y - right.Y;
			return dx * dx + dy * dy;
		}

		static bool IsSameOrAdjacent(CPos left, CPos right)
		{
			return Math.Abs(left.X - right.X) <= 1 && Math.Abs(left.Y - right.Y) <= 1;
		}

		static bool Contains(StealthTargetAcquisitionCacheSnapshot snapshot, CPos cell)
		{
			return cell.X >= 0 && cell.Y >= 0 && cell.X < snapshot.Width && cell.Y < snapshot.Height;
		}

		static int ToTravelMilliseconds(float routeCost, float secondsPerCostUnit)
		{
			return (int)Math.Min(int.MaxValue,
				Math.Round(routeCost * secondsPerCostUnit * 1000d, MidpointRounding.AwayFromZero));
		}
	}
}
