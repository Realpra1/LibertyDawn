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
