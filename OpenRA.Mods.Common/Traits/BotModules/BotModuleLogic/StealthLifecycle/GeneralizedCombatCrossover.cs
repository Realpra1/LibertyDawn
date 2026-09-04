// Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
// This file is licensed under the GNU General Public License version 3 or later.

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Converts the calculator's required attacker ratio into current-force overmatch.</summary>
	public static class GeneralizedCombatCrossover
	{
		public static double Overmatch(int friendlyCount, int enemyCount, double requiredAttackersPerEnemy)
		{
			if (friendlyCount <= 0 || enemyCount <= 0 || !double.IsFinite(requiredAttackersPerEnemy))
				return 0;
			return requiredAttackersPerEnemy > 0 ?
				friendlyCount / (enemyCount * requiredAttackersPerEnemy) : double.PositiveInfinity;
		}
	}
}
