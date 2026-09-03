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
using System.Collections.Generic;
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

		public IStealthMassAttackThreatEvaluation Begin(StealthMassAttackThreatFacts facts)
		{
			return new LiveEvaluation(calculator, Resolve, plannedTargetTypesOverride, facts);
		}

		Actor Resolve(uint actorId)
		{
			var actor = resolveLiveActor(actorId);
			if (actor == null || actor.IsDead || !actor.IsInWorld || actor.ActorID != actorId)
				throw new InvalidOperationException($"MassAttack actor {actorId} is not valid in the live World.");
			return actor;
		}

		sealed class LiveEvaluation : IStealthMassAttackThreatEvaluation
		{
			readonly GeneralizedCombatThreatCalculator calculator;
			readonly BitSet<TargetableType> plannedTargetTypesOverride;
			readonly Actor[] friendly;
			readonly Actor representative;
			readonly Actor[] enemy;
			readonly Dictionary<uint, Actor> enemyById;
			readonly Dictionary<(uint Attacker, uint Defender),
				GeneralizedCombatThreatCalculator.PairThreat> pairThreats;
			readonly uint[] friendlyIds;
			readonly uint[] enemyIds;
			readonly (uint Id, CPos Cell)[] enemyPositions;
			readonly bool formationCloaked;
			readonly StealthTargetThreatScore standard;

			public LiveEvaluation(GeneralizedCombatThreatCalculator calculator,
				Func<uint, Actor> resolve, BitSet<TargetableType> plannedTargetTypesOverride,
				StealthMassAttackThreatFacts facts)
			{
				ValidateShape(facts);
				this.calculator = calculator;
				this.plannedTargetTypesOverride = plannedTargetTypesOverride;
				friendlyIds = facts.FriendlyActorIds.ToArray();
				enemyIds = facts.EnemyActorIds.ToArray();
				enemyPositions = facts.Enemies.Select(item => (item.ActorId, item.CurrentCell)).ToArray();
				formationCloaked = facts.FormationCloaked;
				friendly = friendlyIds.Select(resolve).ToArray();
				enemy = enemyIds.Select(resolve).ToArray();
				representative = friendly.OrderBy(attacker => enemy.Min(defender =>
					(attacker.CenterPosition - defender.CenterPosition).HorizontalLengthSquared))
					.ThenBy(attacker => attacker.ActorID).First();
				enemyById = enemy.ToDictionary(actor => actor.ActorID);
				ValidateLiveEnemyPositions();

				var mixed = calculator.CalculateLiveMixedGroupThreat(friendly, enemy,
					plannedTargetTypesOverride, true,
					preserveRulesDefenderThreatForPlannedExposure: true);
				pairThreats = enemy.Select(defender =>
					(Key: (representative.ActorID, defender.ActorID),
						Threat: GeneralizedCombatPlannedDecloakThreat.Calculate(
							calculator, representative, defender, plannedTargetTypesOverride)))
					.ToDictionary(item => item.Key, item => item.Threat);
				standard = new StealthTargetThreatScore(pairThreats.Values.Select(pair =>
					pair.DefenderThreatInAttackerEquivalents).DefaultIfEmpty().Max(), mixed.Crossover);
			}

			public StealthMassAttackThreatResult Calculate(StealthMassAttackThreatFacts facts)
			{
				ValidateShape(facts);
				if (!facts.FriendlyActorIds.SequenceEqual(friendlyIds) ||
					!facts.EnemyActorIds.SequenceEqual(enemyIds) ||
					facts.FormationCloaked != formationCloaked ||
					!facts.Enemies.Select(item => (item.ActorId, item.CurrentCell)).SequenceEqual(enemyPositions))
					throw new InvalidOperationException("MassAttack evaluation facts changed within one live decision.");
				ValidateLiveEnemyPositions();
				if (!enemyById.TryGetValue(facts.SelectedTargetActorId, out var target) ||
					target.Location != facts.SelectedTargetCurrentCell)
					throw new InvalidOperationException("The MassAttack target moved during live evaluation.");

				var targetThreat = Pair(representative, target).DefenderThreatInAttackerEquivalents;
				var approved = GeneralizedCombatLiveCellSafety.CanAttackSafely(calculator,
					new[] { representative }, enemy, target, facts.PlannedCell, facts.FormationRadiusCells,
					plannedTargetTypesOverride, Pair);
				return new StealthMassAttackThreatResult(standard, targetThreat, approved);
			}

			GeneralizedCombatThreatCalculator.PairThreat Pair(Actor attacker, Actor defender)
			{
				return pairThreats[(attacker.ActorID, defender.ActorID)];
			}

			void ValidateLiveEnemyPositions()
			{
				if (enemyPositions.Any(item => enemyById[item.Id].Location != item.Cell))
					throw new InvalidOperationException("A MassAttack enemy moved during live evaluation.");
			}

			static void ValidateShape(StealthMassAttackThreatFacts facts)
			{
				if (facts == null || !facts.PlannedReveal || !facts.PlannedAttack ||
					!facts.FullCurrentFiringRangeExposure)
					throw new ArgumentException(
						"MassAttack requires one explicit planned live attack.", nameof(facts));
			}
		}
	}
}
