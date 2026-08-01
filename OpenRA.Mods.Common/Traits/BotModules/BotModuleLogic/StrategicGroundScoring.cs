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

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Deterministic arithmetic used by strategic ground squads. This is deliberately free of
	/// World and Actor so target policy can be tested without constructing a simulation.
	/// </summary>
	public static class StrategicGroundScoring
	{
		const int BasisPoints = 10000;

		/// <summary>
		/// Applies a geometric decay to defender value for each complete attacker/defender multiple.
		/// Fractional multiples are linearly interpolated between the adjacent geometric steps.
		/// With the default 50% decay, five-to-one overmatch leaves 3.125% effective resistance.
		/// </summary>
		public static int EffectiveDefenderValue(int attackerValue, int defenderValue, int decayPercent)
		{
			if (defenderValue <= 0)
				return 0;

			if (attackerValue <= 0)
				return defenderValue;

			decayPercent = Math.Clamp(decayPercent, 0, 100);
			var wholeMultiples = Math.Min(32, attackerValue / defenderValue);
			var remainder = attackerValue % defenderValue;
			long factor = BasisPoints;
			for (var i = 0; i < wholeMultiples; i++)
				factor = factor * decayPercent / 100;

			if (wholeMultiples < 32 && remainder > 0)
			{
				var nextFactor = factor * decayPercent / 100;
				factor -= (factor - nextFactor) * remainder / defenderValue;
			}

			return (int)Math.Clamp((long)defenderValue * factor / BasisPoints, 0, int.MaxValue);
		}

		public static bool IsEffectivelyUndefended(int attackerValue, int defenderValue, int overmatchRatio)
		{
			return defenderValue <= 0 || (attackerValue > 0 && overmatchRatio > 0 &&
				(long)attackerValue >= (long)defenderValue * overmatchRatio);
		}

		/// <summary>
		/// Scores a target-rich strategic cell after defender and travel liabilities. The travel cost
		/// scales with the slowest squad member so mixed groups do not plan as if every unit were fast.
		/// </summary>
		public static int ScoreCell(long targetValue, int attackerValue, int defenderValue,
			int pathCells, int squadSpeed, int referenceSpeed, int distancePenalty, int defenderDecayPercent)
		{
			if (targetValue <= 0 || attackerValue <= 0 || squadSpeed <= 0 || referenceSpeed <= 0)
				return 0;

			var effectiveDefender = EffectiveDefenderValue(attackerValue, defenderValue, defenderDecayPercent);
			var defenseCost = (long)effectiveDefender * 1024 / Math.Max(1, attackerValue);
			var travelCost = (long)Math.Max(0, pathCells) * Math.Max(0, distancePenalty) * referenceSpeed /
				Math.Max(1, squadSpeed);
			var score = targetValue * 1024 / (1024 + defenseCost);
			score = score * 1024 / (1024 + travelCost);
			return (int)Math.Clamp(score, 0, int.MaxValue);
		}
	}
}
