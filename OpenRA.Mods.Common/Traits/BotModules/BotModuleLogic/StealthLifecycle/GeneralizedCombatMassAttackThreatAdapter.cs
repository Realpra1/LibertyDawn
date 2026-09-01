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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Resolves every participant from the live World and applies the standard generalized
	/// calculator with explicit planned reveal and full current firing-range exposure.
	/// Detector state remains evidence and is deliberately not a MassAttack rejection gate.
	/// </summary>
	public sealed class GeneralizedCombatMassAttackThreatAdapter : IStealthMassAttackThreatAdapter
	{
		readonly GeneralizedCombatThreatCalculator calculator;
		readonly Func<uint, Actor> resolveLiveActor;
		readonly BitSet<TargetableType> plannedTargetTypesOverride;

		public GeneralizedCombatMassAttackThreatAdapter(GeneralizedCombatThreatCalculator calculator,
			Func<uint, Actor> resolveLiveActor, BitSet<TargetableType> plannedTargetTypesOverride)
		{
			this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
			this.resolveLiveActor = resolveLiveActor ?? throw new ArgumentNullException(nameof(resolveLiveActor));
			this.plannedTargetTypesOverride = plannedTargetTypesOverride;
		}

		public StealthMassAttackThreatResult Calculate(StealthMassAttackThreatFacts facts)
		{
			if (facts == null || !facts.PlannedReveal || !facts.PlannedAttack ||
				!facts.FullCurrentFiringRangeExposure)
				throw new ArgumentException("MassAttack requires one explicit planned live attack.", nameof(facts));
			var friendly = facts.FriendlyActorIds.Select(Resolve).ToArray();
			var enemy = facts.EnemyActorIds.Select(Resolve).ToArray();
			foreach (var enemyFact in facts.Enemies)
			{
				var actor = Resolve(enemyFact.ActorId);
				if (actor.Location != enemyFact.CurrentCell)
					throw new InvalidOperationException("A MassAttack enemy moved during live evaluation.");
			}

			var target = Resolve(facts.SelectedTargetActorId);
			if (target.Location != facts.SelectedTargetCurrentCell)
				throw new InvalidOperationException("The MassAttack target moved during live evaluation.");
			var crossover = calculator.EstimateLiveMixedGroupCrossover(friendly, enemy,
				plannedTargetTypesOverride, true);
			var pairThreats = friendly.SelectMany(attacker => enemy.Select(defender =>
				calculator.CalculateLive(attacker, defender, plannedTargetTypesOverride, true)
					.DefenderThreatInAttackerEquivalents)).ToArray();
			var standard = new StealthTargetThreatScore(pairThreats.DefaultIfEmpty().Max(), crossover);
			var targetThreat = friendly.Sum(attacker => calculator.CalculateLive(attacker, target,
				plannedTargetTypesOverride, true).DefenderThreatInAttackerEquivalents);
			return new StealthMassAttackThreatResult(standard, targetThreat);
		}

		Actor Resolve(uint actorId)
		{
			var actor = resolveLiveActor(actorId);
			if (actor == null || actor.IsDead || !actor.IsInWorld || actor.ActorID != actorId)
				throw new InvalidOperationException($"MassAttack actor {actorId} is not valid in the live World.");
			return actor;
		}
	}
}
