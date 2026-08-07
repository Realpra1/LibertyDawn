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
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	public enum TiberiumFieldAdmissionResult
	{
		Admitted,
		Disabled,
		RedContainmentIncomplete,
		OpeningActive,
		CriticalRecovery,
		MissingUnloadingRefinery,
		MissingStorage,
		MissingHarvesterRoute,
		InsufficientPower,
		InsufficientCash
	}

	public readonly struct TiberiumFieldCoverageCandidate
	{
		public readonly uint TreeActorId;
		public readonly uint ResonatorActorId;
		public readonly long DistanceSquared;

		public TiberiumFieldCoverageCandidate(uint treeActorId, uint resonatorActorId, long distanceSquared)
		{
			TreeActorId = treeActorId;
			ResonatorActorId = resonatorActorId;
			DistanceSquared = Math.Max(0, distanceSquared);
		}
	}

	public readonly struct TiberiumFieldProjectCandidate
	{
		public readonly uint TreeActorId;
		public readonly bool RouteFeasible;
		public readonly int UsefulDemand;
		public readonly int SafetyScore;
		public readonly int RemainingCommitment;

		public TiberiumFieldProjectCandidate(uint treeActorId, bool routeFeasible,
			int usefulDemand, int safetyScore, int remainingCommitment)
		{
			TreeActorId = treeActorId;
			RouteFeasible = routeFeasible;
			UsefulDemand = Math.Max(0, usefulDemand);
			SafetyScore = safetyScore;
			RemainingCommitment = Math.Max(0, remainingCommitment);
		}
	}

	public readonly struct TiberiumFieldExtensionCandidate
	{
		public readonly CPos Cell;
		public readonly int ProgressCells;
		public readonly long TargetDistanceSquared;

		public TiberiumFieldExtensionCandidate(CPos cell, int progressCells, long targetDistanceSquared)
		{
			Cell = cell;
			ProgressCells = progressCells;
			TargetDistanceSquared = Math.Max(0, targetDistanceSquared);
		}
	}

	public sealed class TiberiumFieldPerimeterPlan
	{
		public readonly CPos[] WallCells;
		public readonly CPos[] GateCells;
		public readonly CPos[][] WallSegments;

		public TiberiumFieldPerimeterPlan(CPos[] wallCells, CPos[] gateCells, CPos[][] wallSegments)
		{
			WallCells = wallCells;
			GateCells = gateCells;
			WallSegments = wallSegments;
		}
	}

	/// <summary>
	/// Pure deterministic invariants shared by the bounded field-project manager.
	/// World queries and production ownership remain in the manager so these rules
	/// can be tested without constructing a simulation.
	/// </summary>
	public static class TiberiumFieldPolicy
	{
		public static bool IsValidSavedSpatialIdentity(CPos savedTreeLocation,
			CPos liveTreeLocation, IEnumerable<CPos> resonatorFootprint,
			Func<CPos, bool> isMapCell, long effectDistanceSquared, long effectRangeSquared)
		{
			return savedTreeLocation == liveTreeLocation && resonatorFootprint.All(isMapCell) &&
				effectDistanceSquared <= effectRangeSquared;
		}

		public static bool IsValidSavedSegmentCursor(int segmentIndex, int segmentCount,
			bool activationEligible, bool enclosureComplete)
		{
			if (segmentIndex < 0 || segmentCount <= 0 || segmentIndex > segmentCount)
				return false;

			return segmentIndex < segmentCount || (activationEligible && enclosureComplete);
		}

		public static bool IsValidSavedPerimeter(IEnumerable<CPos> wallCells,
			IEnumerable<CPos> gateCells, IEnumerable<IEnumerable<CPos>> segments)
		{
			var walls = wallCells.ToArray();
			var gates = gateCells.ToArray();
			var segmentCells = segments.SelectMany(s => s).ToArray();
			return walls.Length > 0 && walls.Distinct().Count() == walls.Length &&
				gates.Length > 0 && gates.Length <= 2 && gates.Distinct().Count() == gates.Length &&
				!walls.Intersect(gates).Any() && segmentCells.Distinct().ToHashSet().SetEquals(walls);
		}

		public static bool SavedPerimeterMatchesPlan(TiberiumFieldPerimeterPlan expected,
			IEnumerable<CPos> wallCells, IEnumerable<CPos> gateCells,
			IEnumerable<IEnumerable<CPos>> segments)
		{
			if (expected == null || !expected.WallCells.SequenceEqual(wallCells) ||
				!expected.GateCells.SequenceEqual(gateCells))
				return false;

			var savedSegments = segments.Select(s => s.ToArray()).ToArray();
			return expected.WallSegments.Length == savedSegments.Length &&
				expected.WallSegments.Zip(savedSegments, (planned, saved) => planned.SequenceEqual(saved)).All(equal => equal);
		}

		public static CPos[] MissingPlannedCells(IEnumerable<CPos> plannedCells,
			IEnumerable<CPos> presentCells)
		{
			var present = presentCells.ToHashSet();
			return plannedCells.Where(c => !present.Contains(c)).ToArray();
		}

		public static int FirstIncompleteSegmentIndex(IEnumerable<IEnumerable<CPos>> segments,
			IEnumerable<CPos> presentCells, int startIndex)
		{
			var planned = segments.Select(s => s.ToArray()).ToArray();
			var present = presentCells.ToHashSet();
			var index = Math.Max(0, startIndex);
			while (index < planned.Length && planned[index].All(present.Contains))
				index++;

			return index;
		}

		public static CPos? FirstLegalAlternativeCell(IEnumerable<CPos> candidates,
			CPos reservedCell, Func<CPos, bool> isLegal)
		{
			foreach (var candidate in candidates)
				if (candidate != reservedCell && isLegal(candidate))
					return candidate;

			return null;
		}

		public static int RemainingWallOrders(int segmentCount, int segmentIndex, int anchorIndex)
		{
			return RemainingWallOrders(segmentCount, segmentIndex, anchorIndex, 0);
		}

		public static int RemainingWallOrders(int segmentCount, int segmentIndex,
			int anchorIndex, int currentSegmentMissingCells)
		{
			var remainingSegments = Math.Max(0, segmentCount - Math.Max(0, segmentIndex));
			if (remainingSegments == 0)
				return 0;

			var completedAnchors = Math.Max(0, Math.Min(2, anchorIndex));
			var currentSegmentOrders = completedAnchors < 2 ? 2 - completedAnchors :
				Math.Max(0, currentSegmentMissingCells);
			return currentSegmentOrders + (remainingSegments - 1) * 2;
		}

		public static int RemainingWallOrdersFromWorld(IEnumerable<IEnumerable<CPos>> segments,
			IEnumerable<CPos> presentCells)
		{
			var present = presentCells.ToHashSet();
			var orders = 0;
			foreach (var source in segments)
			{
				var segment = source.ToArray();
				if (segment.Length == 0)
					continue;

				var missing = segment.Count(c => !present.Contains(c));
				if (missing == 0)
					continue;

				var missingEndpoints = new[] { segment[0], segment[segment.Length - 1] }
					.Distinct().Count(c => !present.Contains(c));
				orders += missingEndpoints > 0 ? missingEndpoints : missing;
			}

			return orders;
		}

		public static TiberiumFieldPerimeterPlan PlanRedPerimeter(CPos tree,
			IEnumerable<CPos> resonatorFootprint, CPos gateTarget, int standoff)
		{
			var contained = resonatorFootprint.Append(tree).ToArray();
			if (contained.Length == 0)
				return null;

			var margin = Math.Max(1, standoff);
			var minX = contained.Min(c => c.X) - margin;
			var maxX = contained.Max(c => c.X) + margin;
			var minY = contained.Min(c => c.Y) - margin;
			var maxY = contained.Max(c => c.Y) + margin;
			if (maxX - minX < 3 || maxY - minY < 3)
				return null;

			// Choose the side nearest the stable gate-building target. Ties follow a fixed
			// east, south, west, north order so simulation collection order is irrelevant.
			var side = new[]
			{
				(Name: "east", Distance: DistanceSquaredToSegment(gateTarget, maxX, minY, maxX, maxY)),
				(Name: "south", Distance: DistanceSquaredToSegment(gateTarget, minX, maxY, maxX, maxY)),
				(Name: "west", Distance: DistanceSquaredToSegment(gateTarget, minX, minY, minX, maxY)),
				(Name: "north", Distance: DistanceSquaredToSegment(gateTarget, minX, minY, maxX, minY))
			}.OrderBy(s => s.Distance).First().Name;
			CPos[] gate;
			if (side == "east" || side == "west")
			{
				var y = Math.Max(minY + 1, Math.Min(maxY - 2, gateTarget.Y));
				var x = side == "east" ? maxX : minX;
				gate = new[] { new CPos(x, y), new CPos(x, y + 1) };
			}
			else
			{
				var x = Math.Max(minX + 1, Math.Min(maxX - 2, gateTarget.X));
				var y = side == "south" ? maxY : minY;
				gate = new[] { new CPos(x, y), new CPos(x + 1, y) };
			}

			var gateSet = gate.ToHashSet();
			var wall = new List<CPos>();
			for (var x = minX; x <= maxX; x++)
			{
				wall.Add(new CPos(x, minY));
				wall.Add(new CPos(x, maxY));
			}

			for (var y = minY + 1; y < maxY; y++)
			{
				wall.Add(new CPos(minX, y));
				wall.Add(new CPos(maxX, y));
			}

			var segments = new List<CPos[]>();
			AddSegment(segments, HorizontalSegment(minY, minX,
				side == "north" ? gate[0].X - 1 : maxX));
			if (side == "north")
				AddSegment(segments, HorizontalSegment(minY, gate[1].X + 1, maxX));
			if (side == "east")
			{
				AddSegment(segments, VerticalSegment(maxX, minY + 1, gate[0].Y - 1));
				AddSegment(segments, VerticalSegment(maxX, gate[1].Y + 1, maxY - 1));
			}
			else
				AddSegment(segments, VerticalSegment(maxX, minY + 1, maxY - 1));
			AddSegment(segments, HorizontalSegment(maxY, minX,
				side == "south" ? gate[0].X - 1 : maxX));
			if (side == "south")
				AddSegment(segments, HorizontalSegment(maxY, gate[1].X + 1, maxX));
			if (side == "west")
			{
				AddSegment(segments, VerticalSegment(minX, minY + 1, gate[0].Y - 1));
				AddSegment(segments, VerticalSegment(minX, gate[1].Y + 1, maxY - 1));
			}
			else
				AddSegment(segments, VerticalSegment(minX, minY + 1, maxY - 1));

			return new TiberiumFieldPerimeterPlan(wall.Where(c => !gateSet.Contains(c))
				.OrderBy(c => c.Y).ThenBy(c => c.X).ToArray(), gate, segments.ToArray());
		}

		static CPos[] HorizontalSegment(int y, int fromX, int toX)
		{
			return fromX > toX ? Array.Empty<CPos>() :
				Enumerable.Range(fromX, toX - fromX + 1).Select(x => new CPos(x, y)).ToArray();
		}

		static CPos[] VerticalSegment(int x, int fromY, int toY)
		{
			return fromY > toY ? Array.Empty<CPos>() :
				Enumerable.Range(fromY, toY - fromY + 1).Select(y => new CPos(x, y)).ToArray();
		}

		static void AddSegment(List<CPos[]> segments, CPos[] segment)
		{
			if (segment.Length > 0)
				segments.Add(segment);
		}

		static long DistanceSquaredToSegment(CPos target, int minX, int minY, int maxX, int maxY)
		{
			var x = Math.Max(minX, Math.Min(maxX, target.X));
			var y = Math.Max(minY, Math.Min(maxY, target.Y));
			var dx = (long)target.X - x;
			var dy = (long)target.Y - y;
			return dx * dx + dy * dy;
		}

		public static TiberiumFieldCoverageCandidate[] SelectOneToOneCoverage(
			IEnumerable<TiberiumFieldCoverageCandidate> candidates)
		{
			var assignedTrees = new HashSet<uint>();
			var assignedResonators = new HashSet<uint>();
			var result = new List<TiberiumFieldCoverageCandidate>();
			foreach (var candidate in candidates
				.OrderBy(c => c.DistanceSquared)
				.ThenBy(c => c.TreeActorId)
				.ThenBy(c => c.ResonatorActorId))
			{
				if (!assignedTrees.Add(candidate.TreeActorId))
					continue;

				if (!assignedResonators.Add(candidate.ResonatorActorId))
				{
					assignedTrees.Remove(candidate.TreeActorId);
					continue;
				}

				result.Add(candidate);
			}

			return result.OrderBy(c => c.TreeActorId).ToArray();
		}

		public static TiberiumFieldProjectCandidate? BestProject(
			IEnumerable<TiberiumFieldProjectCandidate> candidates)
		{
			var ranked = candidates
				.OrderByDescending(c => c.RouteFeasible)
				.ThenByDescending(c => c.UsefulDemand)
				.ThenByDescending(c => c.SafetyScore)
				.ThenBy(c => c.RemainingCommitment)
				.ThenBy(c => c.TreeActorId)
				.ToArray();
			return ranked.Length > 0 ? ranked[0] : (TiberiumFieldProjectCandidate?)null;
		}

		public static TiberiumFieldExtensionCandidate? BestExtensionCell(
			IEnumerable<TiberiumFieldExtensionCandidate> candidates, int targetStep)
		{
			var step = Math.Max(1, targetStep);
			var ranked = candidates.Where(c => c.ProgressCells > 0)
				.OrderBy(c => Math.Abs(c.ProgressCells - step))
				.ThenBy(c => c.TargetDistanceSquared)
				.ThenBy(c => c.Cell.Y)
				.ThenBy(c => c.Cell.X)
				.ToArray();
			return ranked.Length > 0 ? ranked[0] : (TiberiumFieldExtensionCandidate?)null;
		}

		public static bool CanAdmit(bool enabled, bool redContainmentReady, bool openingActive,
			bool criticalRecovery, bool hasUnloadingRefinery, bool hasStorage, bool hasHarvesterRoute,
			bool hasPowerMargin, int spendableCash, int protectedCash, int projectCost)
		{
			return EvaluateAdmission(enabled, redContainmentReady, openingActive, criticalRecovery,
				hasUnloadingRefinery, hasStorage, hasHarvesterRoute, hasPowerMargin,
				spendableCash, protectedCash, projectCost) == TiberiumFieldAdmissionResult.Admitted;
		}

		public static TiberiumFieldAdmissionResult EvaluateAdmission(bool enabled,
			bool redContainmentReady, bool openingActive, bool criticalRecovery,
			bool hasUnloadingRefinery, bool hasStorage, bool hasHarvesterRoute,
			bool hasPowerMargin, int spendableCash, int protectedCash, int projectCost)
		{
			if (!enabled)
				return TiberiumFieldAdmissionResult.Disabled;
			if (!redContainmentReady)
				return TiberiumFieldAdmissionResult.RedContainmentIncomplete;
			if (openingActive)
				return TiberiumFieldAdmissionResult.OpeningActive;
			if (criticalRecovery)
				return TiberiumFieldAdmissionResult.CriticalRecovery;
			if (!hasUnloadingRefinery)
				return TiberiumFieldAdmissionResult.MissingUnloadingRefinery;
			if (!hasStorage)
				return TiberiumFieldAdmissionResult.MissingStorage;
			if (!hasHarvesterRoute)
				return TiberiumFieldAdmissionResult.MissingHarvesterRoute;
			if (!hasPowerMargin)
				return TiberiumFieldAdmissionResult.InsufficientPower;
			if ((long)Math.Max(0, spendableCash) <
				(long)Math.Max(0, protectedCash) + Math.Max(0, projectCost))
				return TiberiumFieldAdmissionResult.InsufficientCash;

			return TiberiumFieldAdmissionResult.Admitted;
		}

		public static int NextDeadline(int currentTick, int delay)
		{
			return (int)Math.Min(int.MaxValue,
				(long)Math.Max(0, currentTick) + Math.Max(1, delay));
		}

		public static bool DeadlineReached(int currentTick, int deadline)
		{
			return Math.Max(0, currentTick) >= Math.Max(0, deadline);
		}

		public static bool ShouldDeferNoProgress(int currentTick, int nextProgressCheckTick,
			bool hasLegalWork, bool admissionRejected)
		{
			return !hasLegalWork && !admissionRejected &&
				DeadlineReached(currentTick, nextProgressCheckTick);
		}
	}
}
