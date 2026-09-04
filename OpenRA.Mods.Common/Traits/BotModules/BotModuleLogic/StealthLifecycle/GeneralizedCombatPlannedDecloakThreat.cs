#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. You can redistribute
 * it and/or modify it under the terms of the GNU General Public License.
 */
#endregion

using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Standard-calculator matchup for an action that will reveal the attacker. Live combat state
	/// remains authoritative, except that a temporarily disabled defender keeps its rules-derived
	/// weapon threat for the short exposed window.
	/// </summary>
	static class GeneralizedCombatPlannedDecloakThreat
	{
		public static GeneralizedCombatThreatCalculator.PairThreat Calculate(
			GeneralizedCombatThreatCalculator calculator, Actor attacker, Actor defender,
			BitSet<TargetableType>? plannedTargetTypesOverride = null)
		{
			return calculator.CalculateLive(attacker, defender, plannedTargetTypesOverride,
				plannedCurrentRangeEngagement: true,
				preserveRulesDefenderThreatForPlannedExposure: true);
		}
	}
}
