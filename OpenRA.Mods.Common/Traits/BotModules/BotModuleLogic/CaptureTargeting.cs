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

	public static class CaptureTargeting
	{
		public static int EconomicValue(int directValue, int transformedValue)
		{
			return System.Math.Max(0, System.Math.Max(directValue, transformedValue));
		}

		public static double Score(int economicValue, double distanceCells, int distanceBiasCells)
		{
			var bias = System.Math.Max(1, distanceBiasCells);
			return System.Math.Max(0, economicValue) * bias / (bias + System.Math.Max(0, distanceCells));
		}

		public static bool RequiresEngineerPair(bool isBuilding, int healthPercent, int soloCaptureHealthPercent)
		{
			return isBuilding && healthPercent > soloCaptureHealthPercent;
		}

		public static bool ShouldRetarget(double currentScore, double replacementScore, int minimumImprovementPercent)
		{
			if (currentScore <= 0)
				return replacementScore > 0;

			return replacementScore * 100 > currentScore * (100 + System.Math.Max(0, minimumImprovementPercent));
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
