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

			var selected = Resolve(facts.SelectedTargetActorId);
			if (selected.Location != facts.SelectedTargetCurrentCell)
				throw new InvalidOperationException("The selected Crush infantry moved during live safety evaluation.");
			if (facts.FormationCloaked && !facts.HasDetectorCoverage)
				return new StealthCrushSafetyResult(
					new StealthTargetThreatScore(0, double.PositiveInfinity), true);

			var friendly = facts.FriendlyActorIds.Select(Resolve).ToArray();
			var enemy = facts.EnemyActorIds.Select(Resolve).ToArray();
			var representative = friendly.OrderBy(attacker => enemy.Min(defender =>
				(attacker.CenterPosition - defender.CenterPosition).HorizontalLengthSquared))
				.ThenBy(attacker => attacker.ActorID).First();
			var crossover = calculator.EstimateLiveMixedGroupCrossover(
				friendly, enemy, null, plannedCurrentRangeEngagement: true);
			var maximumThreat = enemy.Select(defender =>
				calculator.CalculateLive(representative, defender, null,
					plannedCurrentRangeEngagement: true).DefenderThreatInAttackerEquivalents)
				.DefaultIfEmpty().Max();

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
