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

		public GeneralizedCombatUndefendedAttackThreatAdapter(
			GeneralizedCombatThreatCalculator calculator, Func<uint, Actor> resolveLiveActor)
		{
			this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
			this.resolveLiveActor = resolveLiveActor ?? throw new ArgumentNullException(nameof(resolveLiveActor));
		}

		public StealthUndefendedAttackSafetyResult Calculate(
			StealthUndefendedAttackThreatFacts facts)
		{
			if (facts == null)
				throw new ArgumentNullException(nameof(facts));
			if (!facts.PlannedCurrentRangeEngagement)
				throw new InvalidOperationException(
					"UndefendedAttack safety requires the standard current-range override.");

			if (facts.FormationCloaked && !facts.HasDetectorCoverage &&
				!facts.PlannedActionRevealsFormation)
				return new StealthUndefendedAttackSafetyResult(
					new StealthTargetThreatScore(0, double.PositiveInfinity), true, false);

			var friendly = facts.FriendlyActorIds.Select(Resolve).ToArray();
			var enemy = facts.EnemyActorIds.Select(Resolve).ToArray();
			var crossover = calculator.EstimateLiveMixedGroupCrossover(
				friendly, enemy, null, plannedCurrentRangeEngagement: true);
			var maximumThreat = friendly.SelectMany(attacker => enemy.Select(defender =>
				calculator.CalculateLive(attacker, defender, null,
					plannedCurrentRangeEngagement: true).DefenderThreatInAttackerEquivalents))
				.DefaultIfEmpty().Max();
			var score = new StealthTargetThreatScore(maximumThreat, crossover);
			return new StealthUndefendedAttackSafetyResult(
				score, maximumThreat <= 0, maximumThreat > 0);
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
