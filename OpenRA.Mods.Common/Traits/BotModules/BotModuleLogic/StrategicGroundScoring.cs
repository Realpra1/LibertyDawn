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
	/// <summary>Deterministic, world-independent policy used by strategic ground squads.</summary>
	public static class StrategicGroundScoring
	{
		const int BasisPoints = 10000;

		/// <summary>
		/// Geometrically discounts resistance as attacker overmatch grows. Fractional multiples are
		/// interpolated. The default 50% decay leaves 3.125% resistance at five-to-one: negligible,
		/// but deliberately not zero.
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
		/// Pauses an established formation only for a material reinforcement wave. This lets a few
		/// stragglers catch up during ordinary movement without repeatedly stopping the whole army.
		/// </summary>
		public static bool ShouldHoldForReinforcements(int formationCount, int reinforcementCount,
			int minimumReinforcements, int reinforcementRatioPercent)
		{
			return formationCount > 0 && reinforcementCount >= minimumReinforcements &&
				(long)reinforcementCount * 100 >= (long)formationCount * reinforcementRatioPercent;
		}

		/// <summary>Prevents the squad from ordering units that are owned by a higher-priority temporary role.</summary>
		public static bool CanOrderGroundReinforcement(bool isProtectingBase, bool isTemporarilyControlled)
		{
			return !isProtectingBase && !isTemporarilyControlled;
		}

		/// <summary>
		/// Gives damaged targets a bounded finishing incentive. Full health keeps the authored value and
		/// near-zero health approaches, but never reaches, twice that value.
		/// </summary>
		public static int RemainingHealthPriority(int priority, int hp, int maxHp)
		{
			if (priority <= 0 || maxHp <= 0)
				return Math.Max(0, priority);

			var remainingHp = Math.Clamp(hp, 1, maxHp);
			return (int)Math.Min(int.MaxValue, (long)priority * (2L * maxHp - remainingHp) / maxHp);
		}

		/// <summary>Scores regional opportunity after defender resistance and slowest-member travel time.</summary>
		public static int ScoreCell(long targetValue, int attackerValue, int defenderValue,
			int distanceCells, int slowestSpeed, int referenceSpeed, int distancePenalty, int defenderDecayPercent)
		{
			if (targetValue <= 0 || attackerValue <= 0 || slowestSpeed <= 0 || referenceSpeed <= 0)
				return 0;

			var effectiveDefender = EffectiveDefenderValue(attackerValue, defenderValue, defenderDecayPercent);
			var defenseCost = (long)effectiveDefender * 1024 / Math.Max(1, attackerValue);
			var travelCost = (long)Math.Max(0, distanceCells) * Math.Max(0, distancePenalty) * referenceSpeed /
				Math.Max(1, slowestSpeed);
			var score = targetValue * 1024 / (1024 + defenseCost);
			score = score * 1024 / (1024 + travelCost);
			return (int)Math.Clamp(score, 0, int.MaxValue);
		}
	}
}
