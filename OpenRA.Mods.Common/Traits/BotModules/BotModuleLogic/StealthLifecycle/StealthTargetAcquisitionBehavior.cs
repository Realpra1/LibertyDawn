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

		readonly StealthBehaviorHandoff handoff;
		readonly IStealthTargetAcquisitionCache cache;
		readonly int? squadCornerIndex;
		CPos? moveCloserDestination;
		bool? moveCloserFormationCloaked;

		public StealthTargetAcquisitionBehavior(StealthBehaviorHandoff handoff,
			IStealthTargetAcquisitionCache cache, int? squadCornerIndex = null)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetAcquisition)
				throw new ArgumentException("TargetAcquisition requires its ownership.", nameof(handoff));
			this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
			if (squadCornerIndex < 0)
				throw new ArgumentOutOfRangeException(nameof(squadCornerIndex));
			this.squadCornerIndex = squadCornerIndex;
		}

		public StealthTargetAcquisitionResult Execute(CPos activeSquadCenter,
			CPos? incumbentStrategicCell = null, bool movementFinished = false)
		{
			var snapshot = cache.ReadSnapshot() ??
				throw new InvalidOperationException("The TargetAcquisition cache returned no snapshot.");
			if (!Contains(snapshot, activeSquadCenter) ||
				(incumbentStrategicCell.HasValue && !Contains(snapshot, incumbentStrategicCell.Value)))
				throw new ArgumentOutOfRangeException(nameof(activeSquadCenter));
			if (moveCloserDestination.HasValue &&
				moveCloserFormationCloaked == snapshot.FormationCloaked && !movementFinished &&
				!IsSameOrAdjacent(activeSquadCenter, moveCloserDestination.Value))
				return new StealthTargetAcquisitionResult(handoff, activeSquadCenter,
					incumbentStrategicCell, StealthTargetAcquisitionDisposition.MoveCloserAndRescan,
					Array.Empty<StealthTargetOption>(), moveCloserDestination, 0, 0);
			moveCloserDestination = null;
			moveCloserFormationCloaked = null;

			var allEnemyCells = snapshot.EnemyStrategicCells.Distinct()
				.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
			var scanOrigin = BiasedScanOrigin(activeSquadCenter,
				snapshot.Width, snapshot.Height, squadCornerIndex);
			var highValueCells = HighValueCells(snapshot);
			var enemyCells = highValueCells.Count == 0 ? allEnemyCells :
				allEnemyCells.Where(highValueCells.Contains).ToArray();
			var required = incumbentStrategicCell.HasValue ?
				Array.IndexOf(enemyCells, incumbentStrategicCell.Value) : -1;
			var search = StealthAIThreatGeometry.StartReachableTargetCellSearch(
				snapshot.Danger.ToArray(), snapshot.Width, snapshot.Height,
				scanOrigin.X, scanOrigin.Y, enemyCells, snapshot.RouteThreatPenalty,
				incumbentStrategicCell.HasValue ? MaximumOptions - 1 : MaximumOptions, required,
				float.MaxValue);
			search.Advance(MaximumPrimitiveOperations);
			var discovered = search.Complete ? search.Result.Targets :
				new List<StealthAIThreatGeometry.ReachableTargetCell>();
			var discoveredCells = discovered.Select(target => enemyCells[target.TargetIndex])
				.Concat(incumbentStrategicCell.HasValue ? new[] { incumbentStrategicCell.Value } : Array.Empty<CPos>())
				.Distinct().ToArray();
			var routed = new List<StealthAIThreatGeometry.ReachableTargetCell>();
			var routeOperations = 0;
			var routeExpandedCells = 0;
			if (discoveredCells.Length != 0)
			{
				var routeSearch = StealthAIThreatGeometry.StartReachableTargetCellSearch(
					snapshot.Danger.ToArray(), snapshot.Width, snapshot.Height,
					activeSquadCenter.X, activeSquadCenter.Y, discoveredCells,
					snapshot.RouteThreatPenalty, discoveredCells.Length, -1, float.MaxValue);
				routeSearch.Advance(MaximumPrimitiveOperations);
				if (routeSearch.Complete)
					routed = routeSearch.Result.Targets;
				routeOperations = routeSearch.PrimitiveOperations;
				routeExpandedCells = routeSearch.ExpandedCells;
			}

			var candidates = routed.Select(target => new
				{
					Cell = discoveredCells[target.TargetIndex],
					DiscoveryRank = target.TargetIndex,
					TravelMilliseconds = ToTravelMilliseconds(target.RouteCost,
						snapshot.SecondsPerCostUnit)
				})
				.Where(candidate => candidate.TravelMilliseconds <= MaximumTravelSeconds * 1000)
				.OrderBy(candidate => candidate.DiscoveryRank)
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

			// Moving closer is the far-from-all-targets fallback. Once any live target cell is
			// reachable within the bounded travel window, phase 4 should choose it instead of
			// retaining a long blind acquisition move through local combat.
			var hasReachableTarget = options.Any(option => option.EstimatedTravelMilliseconds.HasValue);
			var needsRescan = enemyCells.Length == 0 || !hasReachableTarget;
			var disposition = !needsRescan ? StealthTargetAcquisitionDisposition.ReadyForValueFilter :
				enemyCells.Length == 0 ? StealthTargetAcquisitionDisposition.AwaitingCache :
				StealthTargetAcquisitionDisposition.MoveCloserAndRescan;
			var moveCloser = needsRescan && enemyCells.Length != 0 ?
				MoveCloser(activeSquadCenter, enemyCells, snapshot) : null;
			moveCloserDestination = moveCloser;
			moveCloserFormationCloaked = moveCloser.HasValue ? (bool?)snapshot.FormationCloaked : null;
			return new StealthTargetAcquisitionResult(handoff, activeSquadCenter,
				incumbentStrategicCell, disposition, options, moveCloser,
				search.PrimitiveOperations + routeOperations,
				search.ExpandedCells + routeExpandedCells);
		}

		static StealthTargetOption Option(StealthTargetAcquisitionCacheSnapshot snapshot,
			CPos cell, int? travelMilliseconds, bool incumbent)
		{
			return new StealthTargetOption(cell, travelMilliseconds, incumbent,
				snapshot.StrategicTargets.Where(target => target.StrategicCell == cell),
				snapshot.ThreatFacts.FirstOrDefault(facts => facts.StrategicCell == cell));
		}

		static HashSet<CPos> HighValueCells(StealthTargetAcquisitionCacheSnapshot snapshot)
		{
			return snapshot.StrategicTargets.GroupBy(target => target.StrategicCell)
				.Where(group =>
				{
					long total = 0;
					foreach (var target in group)
					{
						var value = StealthAISpecialistPolicy.StrategicTargetValueByRemainingHealth(
							target.ConfiguredPriority, target.ActorValue,
							target.HitPoints, target.MaximumHitPoints);
						if (value <= 0)
							continue;
						total = long.MaxValue - total < value ? long.MaxValue : total + value;
					}

					return StealthAISpecialistPolicy.MeetsMinimumStrategicCellValue(total);
				}).Select(group => group.Key).ToHashSet();
		}

		static CPos? MoveCloser(CPos start, IReadOnlyList<CPos> enemies,
			StealthTargetAcquisitionCacheSnapshot snapshot)
		{
			var search = StealthAIThreatGeometry.StartReachableTargetCellSearch(
				snapshot.Danger.ToArray(), snapshot.Width, snapshot.Height,
				start.X, start.Y, enemies, snapshot.RouteThreatPenalty, 1, -1, float.MaxValue);
			search.Advance(MaximumPrimitiveOperations);
			var reachable = search.Complete ? search.Result.Targets :
				new List<StealthAIThreatGeometry.ReachableTargetCell>();
			var routed = reachable.Where(target => target.Route.Count != 0)
				.OrderBy(target => target.RouteCost)
				.ThenBy(target => enemies[target.TargetIndex].Y)
				.ThenBy(target => enemies[target.TargetIndex].X).FirstOrDefault();
			if (routed == null)
				return GreedyMoveCloser(start, enemies, snapshot);

			var maximumCost = MaximumTravelSeconds / snapshot.SecondsPerCostUnit;
			var cost = 0f;
			var boundedRoute = new List<CPos>();
			foreach (var cell in routed.Route)
			{
				var stepCost = 1 + Math.Max(0, snapshot.Danger[cell.Y * snapshot.Width + cell.X]) *
					snapshot.RouteThreatPenalty;
				if (cost + stepCost > maximumCost)
					break;
				cost += stepCost;
				boundedRoute.Add(cell);
			}

			return boundedRoute.Count == 0 ? (CPos?)null :
				StealthStrategicRouteGeometry.EndOfFirstStraightLeg(start, boundedRoute);
		}

		static CPos? GreedyMoveCloser(CPos start, IReadOnlyList<CPos> enemies,
			StealthTargetAcquisitionCacheSnapshot snapshot)
		{
			var currentDistance = enemies.Min(enemy => DistanceSquared(start, enemy));
			var next = Neighbors(start, snapshot.Width, snapshot.Height)
				.Select(cell => new
				{
					Cell = cell,
					Distance = enemies.Min(enemy => DistanceSquared(cell, enemy)),
					Danger = snapshot.Danger[cell.Y * snapshot.Width + cell.X]
				})
				.Where(candidate => candidate.Distance < currentDistance)
				.OrderBy(candidate => candidate.Danger)
				.ThenBy(candidate => candidate.Distance)
				.ThenBy(candidate => candidate.Cell.Y).ThenBy(candidate => candidate.Cell.X)
				.FirstOrDefault();
			return next?.Cell;
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

		static CPos BiasedScanOrigin(CPos center, int width, int height, int? cornerIndex)
		{
			if (!cornerIndex.HasValue)
				return center;

			CPos corner;
			switch (cornerIndex.Value % 4)
			{
				case 0: corner = new CPos(0, 0); break;
				case 1: corner = new CPos(width - 1, 0); break;
				case 2: corner = new CPos(0, height - 1); break;
				default: corner = new CPos(width - 1, height - 1); break;
			}

			return new CPos((3 * center.X + corner.X) / 4,
				(3 * center.Y + corner.Y) / 4);
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
