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

namespace OpenRA.Mods.Common.Traits
{
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
