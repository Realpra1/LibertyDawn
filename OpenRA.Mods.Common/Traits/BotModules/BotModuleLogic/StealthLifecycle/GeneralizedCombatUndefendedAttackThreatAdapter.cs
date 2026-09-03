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
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Resolves actor identities against the live World and delegates all combat math to the
	/// standard calculator. The current-range override deliberately bypasses stale armament ranges.
	/// </summary>
	public sealed class GeneralizedCombatUndefendedAttackThreatAdapter :
		IStealthUndefendedAttackThreatAdapter
	{
		readonly GeneralizedCombatThreatCalculator calculator;
		readonly Func<uint, Actor> resolveLiveActor;
		readonly int safetyMarginCells;

		public GeneralizedCombatUndefendedAttackThreatAdapter(
			GeneralizedCombatThreatCalculator calculator, Func<uint, Actor> resolveLiveActor,
			int safetyMarginCells = 0)
		{
			if (safetyMarginCells < 0)
				throw new ArgumentOutOfRangeException(nameof(safetyMarginCells));
			this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
			this.resolveLiveActor = resolveLiveActor ?? throw new ArgumentNullException(nameof(resolveLiveActor));
			this.safetyMarginCells = safetyMarginCells;
		}

		public StealthUndefendedAttackSafetyResult Calculate(
			StealthUndefendedAttackThreatFacts facts)
		{
			if (facts == null)
				throw new ArgumentNullException(nameof(facts));
			if (!facts.PlannedCurrentRangeEngagement)
				throw new InvalidOperationException(
					"UndefendedAttack safety requires the standard current-range override.");

			var friendly = facts.FriendlyActorIds.Select(Resolve).ToArray();
			var enemy = facts.EnemyActorIds.Select(Resolve).ToArray();
			var representative = friendly.OrderBy(attacker => enemy.Min(defender =>
				(attacker.CenterPosition - defender.CenterPosition).HorizontalLengthSquared))
				.ThenBy(attacker => attacker.ActorID).First();
			var evaluatedFriendly = new[] { representative };
			var crossover = calculator.EstimateLiveMixedGroupCrossover(
				evaluatedFriendly, enemy, null, plannedCurrentRangeEngagement: true);
			var maximumThreat = enemy.Select(defender =>
			{
				var pair = calculator.CalculateLive(representative, defender, null,
					plannedCurrentRangeEngagement: true);
				var dx = (long)representative.Location.X - defender.Location.X;
				var dy = (long)representative.Location.Y - defender.Location.Y;
				return GeneralizedCombatThreatCalculator.DefenderThreatAtDistance(
					pair, Math.Sqrt(dx * dx + dy * dy), includeDefenderHitRadius: true);
			}).DefaultIfEmpty().Max();
			var score = new StealthTargetThreatScore(maximumThreat, crossover);
			return new StealthUndefendedAttackSafetyResult(score,
				GeneralizedCombatLiveCellSafety.IsCurrentAttackSafe(calculator, evaluatedFriendly, enemy, null,
					(attacker, defender) => GeneralizedCombatPlannedDecloakThreat.Calculate(
						calculator, attacker, defender), safetyMarginCells), false);
		}

		Actor Resolve(uint actorId)
		{
			var actor = resolveLiveActor(actorId);
			if (actor == null || actor.IsDead || !actor.IsInWorld || actor.ActorID != actorId)
				throw new InvalidOperationException(
					$"UndefendedAttack actor {actorId} is not valid in the live World.");
			return actor;
		}
	}
}
