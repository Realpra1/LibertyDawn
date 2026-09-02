#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. You can redistribute
 * it and/or modify it under the terms of the GNU General Public License.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Applies the standard live calculator at one proposed formation cell.</summary>
	static class GeneralizedCombatLiveCellSafety
	{
		public static bool CanAttackSafely(GeneralizedCombatThreatCalculator calculator,
			IReadOnlyList<Actor> friendly, IReadOnlyList<Actor> enemies, Actor target,
			CPos plannedCell, int formationRadiusCells,
			BitSet<TargetableType>? plannedTargetTypesOverride,
			Func<Actor, Actor, GeneralizedCombatThreatCalculator.PairThreat> calculatePair = null)
		{
			if (calculator == null || friendly == null || enemies == null || target == null)
				throw new ArgumentNullException(calculator == null ? nameof(calculator) :
					friendly == null ? nameof(friendly) : enemies == null ? nameof(enemies) : nameof(target));
			if (friendly.Count == 0 || formationRadiusCells < 0)
				return false;
			if (calculatePair == null)
				calculatePair = (attacker, defender) => calculator.CalculateLive(
					attacker, defender, plannedTargetTypesOverride, true);

			var targetDistance = Distance(plannedCell, target.Location) + formationRadiusCells;
			if (friendly.Any(attacker => !GeneralizedCombatThreatCalculator.CanEngageAtDistance(
				calculatePair(attacker, target).Forward,
				targetDistance)))
				return false;

			return friendly.All(attacker => enemies.All(enemy =>
			{
				var distance = Math.Max(0, Distance(plannedCell, enemy.Location) - formationRadiusCells);
				var pair = calculatePair(attacker, enemy);
				return GeneralizedCombatThreatCalculator.DefenderThreatAtDistance(pair, distance) <= 0;
			}));
		}

		public static bool IsCurrentAttackSafe(GeneralizedCombatThreatCalculator calculator,
			IReadOnlyList<Actor> friendly, IReadOnlyList<Actor> enemies,
			BitSet<TargetableType>? plannedTargetTypesOverride = null)
		{
			if (calculator == null || friendly == null || enemies == null)
				throw new ArgumentNullException(calculator == null ? nameof(calculator) :
					friendly == null ? nameof(friendly) : nameof(enemies));

			return friendly.All(attacker => enemies.All(enemy =>
			{
				var pair = calculator.CalculateLive(attacker, enemy,
					plannedTargetTypesOverride, true);
				return GeneralizedCombatThreatCalculator.DefenderThreatAtDistance(
					pair, Distance(attacker.Location, enemy.Location)) <= 0;
			}));
		}

		static double Distance(CPos left, CPos right)
		{
			var dx = (long)left.X - right.X;
			var dy = (long)left.Y - right.Y;
			return Math.Sqrt(dx * dx + dy * dy);
		}
	}
}
