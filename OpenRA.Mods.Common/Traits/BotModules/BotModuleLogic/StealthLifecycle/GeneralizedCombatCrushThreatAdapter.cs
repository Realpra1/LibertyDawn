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
	/// <summary>Resolves every participant from the live World and applies standard combat math.</summary>
	public sealed class GeneralizedCombatCrushThreatAdapter : IStealthCrushThreatAdapter
	{
		readonly GeneralizedCombatThreatCalculator calculator;
		readonly Func<uint, Actor> resolveLiveActor;

		public GeneralizedCombatCrushThreatAdapter(
			GeneralizedCombatThreatCalculator calculator, Func<uint, Actor> resolveLiveActor)
		{
			this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
			this.resolveLiveActor = resolveLiveActor ?? throw new ArgumentNullException(nameof(resolveLiveActor));
		}

		public StealthCrushSafetyResult Calculate(StealthCrushThreatFacts facts)
		{
			if (facts == null)
				throw new ArgumentNullException(nameof(facts));
			if (!facts.RemainCloakedAction || facts.PlannedActionRevealsFormation ||
				!facts.PlannedCurrentRangeEngagement)
				throw new InvalidOperationException("Crush safety requires a remain-cloaked live action context.");

			var friendly = facts.FriendlyActorIds.Select(Resolve).ToArray();
			var enemy = facts.EnemyActorIds.Select(Resolve).ToArray();
			var selected = Resolve(facts.SelectedTargetActorId);
			if (selected.Location != facts.SelectedTargetCurrentCell)
				throw new InvalidOperationException("The selected Crush infantry moved during live safety evaluation.");

			var crossover = calculator.EstimateLiveMixedGroupCrossover(
				friendly, enemy, null, plannedCurrentRangeEngagement: true);
			var maximumThreat = friendly.SelectMany(attacker => enemy.Select(defender =>
				calculator.CalculateLive(attacker, defender, null,
					plannedCurrentRangeEngagement: true).DefenderThreatInAttackerEquivalents))
				.DefaultIfEmpty().Max();

			// The standard calculation is still required for the live package context. A
			// remain-cloaked action makes that weapon threat ineffective only while the
			// formation is actually cloaked and the exact live action has no detector coverage.
			if (facts.FormationCloaked && !facts.HasDetectorCoverage)
				return new StealthCrushSafetyResult(
					new StealthTargetThreatScore(0, double.PositiveInfinity), true);

			return new StealthCrushSafetyResult(
				new StealthTargetThreatScore(maximumThreat, crossover), false);
		}

		Actor Resolve(uint actorId)
		{
			var actor = resolveLiveActor(actorId);
			if (actor == null || actor.IsDead || !actor.IsInWorld || actor.ActorID != actorId)
				throw new InvalidOperationException(
					$"Crush actor {actorId} is not valid in the live World.");
			return actor;
		}
	}
}
