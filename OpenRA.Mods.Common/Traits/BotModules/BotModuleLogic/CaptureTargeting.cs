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

using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	public enum SpecialistAssignmentPurpose
	{
		Capture,
		Demolition
	}

	public readonly struct SpecialistTargetReservationState
	{
		public readonly uint SpecialistId;
		public readonly uint TargetId;
		public readonly SpecialistAssignmentPurpose Purpose;
		public readonly int MaximumClaimants;

		public SpecialistTargetReservationState(uint specialistId, uint targetId,
			SpecialistAssignmentPurpose purpose, int maximumClaimants)
		{
			SpecialistId = specialistId;
			TargetId = targetId;
			Purpose = purpose;
			MaximumClaimants = maximumClaimants;
		}
	}

	public sealed class SpecialistTargetReservations
	{
		sealed class Reservation
		{
			public readonly SpecialistAssignmentPurpose Purpose;
			public readonly SortedSet<uint> Claimants = new SortedSet<uint>();

			public Reservation(SpecialistAssignmentPurpose purpose)
			{
				Purpose = purpose;
			}
		}

		readonly Dictionary<uint, Reservation> byTarget = new Dictionary<uint, Reservation>();
		readonly Dictionary<uint, uint> targetBySpecialist = new Dictionary<uint, uint>();

		public bool IsReserved(uint targetId)
		{
			return byTarget.ContainsKey(targetId);
		}

		public bool IsReservedForOtherPurpose(uint targetId, SpecialistAssignmentPurpose purpose)
		{
			return byTarget.TryGetValue(targetId, out var reservation) && reservation.Purpose != purpose;
		}

		public IReadOnlyList<uint> Claimants(uint targetId)
		{
			return byTarget.TryGetValue(targetId, out var reservation) ?
				reservation.Claimants.ToArray() : System.Array.Empty<uint>();
		}

		public bool TryGetReservation(uint specialistId, out uint targetId,
			out SpecialistAssignmentPurpose purpose)
		{
			purpose = default(SpecialistAssignmentPurpose);
			if (!targetBySpecialist.TryGetValue(specialistId, out targetId) ||
				!byTarget.TryGetValue(targetId, out var reservation))
				return false;

			purpose = reservation.Purpose;
			return true;
		}

		public bool Matches(uint specialistId, uint targetId,
			SpecialistAssignmentPurpose purpose, int expectedClaimants)
		{
			return targetBySpecialist.TryGetValue(specialistId, out var currentTarget) &&
				currentTarget == targetId && byTarget.TryGetValue(targetId, out var reservation) &&
				reservation.Purpose == purpose && reservation.Claimants.Contains(specialistId) &&
				reservation.Claimants.Count == System.Math.Max(1, expectedClaimants);
		}

		public bool TryReserve(uint specialistId, uint targetId, SpecialistAssignmentPurpose purpose, int maximumClaimants)
		{
			maximumClaimants = System.Math.Max(1, maximumClaimants);
			if (targetBySpecialist.TryGetValue(specialistId, out var currentTarget) && currentTarget == targetId &&
				byTarget.TryGetValue(targetId, out var currentReservation) && currentReservation.Purpose == purpose)
				return true;

			if (byTarget.TryGetValue(targetId, out var reservation) &&
				(reservation.Purpose != purpose || reservation.Claimants.Count >= maximumClaimants))
				return false;

			Release(specialistId);
			if (!byTarget.TryGetValue(targetId, out reservation))
			{
				reservation = new Reservation(purpose);
				byTarget.Add(targetId, reservation);
			}

			reservation.Claimants.Add(specialistId);
			targetBySpecialist.Add(specialistId, targetId);
			return true;
		}

		public IReadOnlyList<SpecialistTargetReservationState> Restore(
			IEnumerable<SpecialistTargetReservationState> savedReservations)
		{
			byTarget.Clear();
			targetBySpecialist.Clear();

			var restored = new List<SpecialistTargetReservationState>();
			var restoredSpecialists = new HashSet<uint>();
			foreach (var group in savedReservations
				.OrderBy(r => r.TargetId)
				.ThenBy(r => r.Purpose)
				.ThenBy(r => r.SpecialistId)
				.GroupBy(r => r.TargetId))
			{
				var reservations = group.ToArray();
				var purpose = reservations[0].Purpose;
				var maximumClaimants = reservations[0].MaximumClaimants;
				if (group.Key == 0 || reservations.Any(r => r.SpecialistId == 0 ||
					r.Purpose != purpose || r.MaximumClaimants != maximumClaimants) ||
					reservations.Select(r => r.SpecialistId).Distinct().Count() != reservations.Length ||
					reservations.Any(r => restoredSpecialists.Contains(r.SpecialistId)) ||
					reservations.Length != maximumClaimants || maximumClaimants < 1 || maximumClaimants > 2 ||
					(purpose == SpecialistAssignmentPurpose.Demolition && maximumClaimants != 1))
					continue;

				foreach (var reservation in reservations)
				{
					TryReserve(reservation.SpecialistId, reservation.TargetId,
						reservation.Purpose, reservation.MaximumClaimants);
					restoredSpecialists.Add(reservation.SpecialistId);
					restored.Add(reservation);
				}
			}

			return restored;
		}

		public void Release(uint specialistId)
		{
			if (!targetBySpecialist.TryGetValue(specialistId, out var targetId))
				return;

			targetBySpecialist.Remove(specialistId);
			if (!byTarget.TryGetValue(targetId, out var reservation))
				return;

			reservation.Claimants.Remove(specialistId);
			if (reservation.Claimants.Count == 0)
				byTarget.Remove(targetId);
		}
	}

	public readonly struct CaptureAllocation
	{
		public readonly int FirstTarget;
		public readonly int SecondTarget;
		public readonly double Score;

		public CaptureAllocation(int firstTarget, int secondTarget, double score)
		{
			FirstTarget = firstTarget;
			SecondTarget = secondTarget;
			Score = score;
		}
	}

	public readonly struct DemolitionAllocation
	{
		public readonly int Unit;
		public readonly int Target;

		public DemolitionAllocation(int unit, int target)
		{
			Unit = unit;
			Target = target;
		}
	}

	public static class CaptureTargeting
	{
		public static IReadOnlyList<DemolitionAllocation> TargetFirstDemolitionAllocation(
			long[,] distances, bool[,] viable)
		{
			if (distances == null || viable == null || distances.GetLength(0) != viable.GetLength(0) ||
				distances.GetLength(1) != viable.GetLength(1))
				throw new System.ArgumentException("Distance and viability matrices must have matching dimensions.");

			var units = distances.GetLength(0);
			var targets = distances.GetLength(1);
			var assignedUnits = new HashSet<int>();
			var result = new List<DemolitionAllocation>();
			for (var target = 0; target < targets; target++)
			{
				var bestUnit = -1;
				for (var unit = 0; unit < units; unit++)
				{
					if (assignedUnits.Contains(unit) || !viable[unit, target])
						continue;

					if (bestUnit < 0 || distances[unit, target] < distances[bestUnit, target])
						bestUnit = unit;
				}

				if (bestUnit < 0)
					continue;

				assignedUnits.Add(bestUnit);
				result.Add(new DemolitionAllocation(bestUnit, target));
			}

			return result;
		}

		public static bool IsCapabilityScopedOwnedRestorationCandidate(bool sameOwner, bool hasHusk,
			bool isBuilding, bool hasValidTransform, bool hasMatchingCapture)
		{
			return sameOwner && hasHusk && !isBuilding && hasValidTransform && hasMatchingCapture;
		}

		public static int EconomicValue(int directValue, int transformedValue)
		{
			return System.Math.Max(0, System.Math.Max(directValue, transformedValue));
		}

		public static double Score(int economicValue, double distanceCells, int distanceBiasCells)
		{
			var bias = System.Math.Max(1, distanceBiasCells);
			return System.Math.Max(0, economicValue) * bias / (bias + System.Math.Max(0, distanceCells));
		}

		public static bool RequiresEngineerPair(bool isBuilding, int hitPoints, int maxHitPoints,
			int soloCaptureHealthPercent)
		{
			if (!isBuilding)
				return false;

			if (maxHitPoints <= 0)
				return true;

			var threshold = System.Math.Max(0, soloCaptureHealthPercent);
			return 100L * hitPoints > (long)threshold * maxHitPoints;
		}

		public static bool ShouldRetarget(double currentScore, double replacementScore, int minimumImprovementPercent)
		{
			if (currentScore <= 0)
				return replacementScore > 0;

			return replacementScore * 100 > currentScore * (100 + System.Math.Max(0, minimumImprovementPercent));
		}

		public static bool ActivityGraceExpired(int missingActivitySinceTick, int worldTick, int graceTicks)
		{
			return missingActivitySinceTick >= 0 &&
				worldTick - missingActivitySinceTick > System.Math.Max(0, graceTicks);
		}

		public static bool ShouldRestoreAssignmentActivity(bool hasExpectedActivity,
			int missingActivitySinceTick, int worldTick, int graceTicks)
		{
			return hasExpectedActivity || !ActivityGraceExpired(missingActivitySinceTick, worldTick, graceTicks);
		}

		public static double PairScore(double firstScore, double secondScore)
		{
			return firstScore < 0 || secondScore < 0 ? -1 : System.Math.Min(firstScore, secondScore);
		}

		public static CaptureAllocation BestDistinctTargetAllocation(
			IReadOnlyList<double> firstScores,
			IReadOnlyList<double> secondScores,
			ISet<int> unavailable)
		{
			var best = new CaptureAllocation(-1, -1, 0);
			for (var first = -1; first < firstScores.Count; first++)
			{
				if (first >= 0 && (unavailable.Contains(first) || firstScores[first] < 0))
					continue;

				for (var second = -1; second < secondScores.Count; second++)
				{
					if (second >= 0 && (second == first || unavailable.Contains(second) || secondScores[second] < 0))
						continue;

					var score = (first < 0 ? 0 : firstScores[first]) + (second < 0 ? 0 : secondScores[second]);
					if (IsPreferredAllocation(first, second, score, best))
						best = new CaptureAllocation(first, second, score);
				}
			}

			return best;
		}

		static bool IsPreferredAllocation(int first, int second, double score, CaptureAllocation current)
		{
			if (score != current.Score)
				return score > current.Score;

			var assigned = (first >= 0 ? 1 : 0) + (second >= 0 ? 1 : 0);
			var currentAssigned = (current.FirstTarget >= 0 ? 1 : 0) + (current.SecondTarget >= 0 ? 1 : 0);
			if (assigned != currentAssigned)
				return assigned > currentAssigned;

			if ((first >= 0) != (current.FirstTarget >= 0))
				return first >= 0;

			var firstOrder = first < 0 ? int.MaxValue : first;
			var currentFirstOrder = current.FirstTarget < 0 ? int.MaxValue : current.FirstTarget;
			if (firstOrder != currentFirstOrder)
				return firstOrder < currentFirstOrder;

			var secondOrder = second < 0 ? int.MaxValue : second;
			var currentSecondOrder = current.SecondTarget < 0 ? int.MaxValue : current.SecondTarget;
			return secondOrder < currentSecondOrder;
		}

		public static int BestTargetIndex(
			IReadOnlyList<double> scores,
			IReadOnlyList<bool> buildings,
			IReadOnlyList<long> distances,
			ISet<int> assigned)
		{
			var best = -1;
			for (var i = 0; i < scores.Count; i++)
			{
				if (assigned.Contains(i))
					continue;

				if (best < 0 || scores[i] > scores[best] ||
					(scores[i] == scores[best] && buildings[i] && !buildings[best]) ||
					(scores[i] == scores[best] && buildings[i] == buildings[best] && distances[i] < distances[best]))
					best = i;
			}

			return best;
		}
	}
}
