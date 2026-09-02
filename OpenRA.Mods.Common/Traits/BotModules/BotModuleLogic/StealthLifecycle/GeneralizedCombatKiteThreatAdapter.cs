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
	/// Resolves each participant from the live World and uses the standard generalized
	/// calculator. Cell geometry only validates the requested current-range action; no
	/// second threat score, actor cache, or danger cutoff exists here.
	/// </summary>
	public sealed class GeneralizedCombatKiteThreatAdapter : IStealthKiteThreatAdapter
	{
		readonly GeneralizedCombatThreatCalculator calculator;
		readonly Func<uint, Actor> resolveLiveActor;
		readonly BitSet<TargetableType> plannedTargetTypesOverride;

		public GeneralizedCombatKiteThreatAdapter(GeneralizedCombatThreatCalculator calculator,
			Func<uint, Actor> resolveLiveActor, BitSet<TargetableType> plannedTargetTypesOverride)
		{
			this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
			this.resolveLiveActor = resolveLiveActor ?? throw new ArgumentNullException(nameof(resolveLiveActor));
			this.plannedTargetTypesOverride = plannedTargetTypesOverride;
		}

		public StealthKiteSafetyResult Calculate(StealthKiteThreatFacts facts)
		{
			if (facts == null)
				throw new ArgumentNullException(nameof(facts));
			if (facts.PlannedDecloak != facts.PlannedAttack ||
				facts.PlannedCurrentRangeEngagement != facts.PlannedAttack)
				throw new InvalidOperationException("Kite reveal and current-range facts are inconsistent.");

			var friendly = facts.FriendlyActorIds.Select(Resolve).ToArray();
			var enemy = facts.EnemyActorIds.Select(Resolve).ToArray();
			foreach (var enemyFact in facts.Enemies)
			{
				var live = Resolve(enemyFact.ActorId);
				if (live.Location != enemyFact.CurrentCell)
					throw new InvalidOperationException("A Kite enemy moved during live safety evaluation.");
			}

			var target = facts.Enemies.Single(actor => actor.ActorId == facts.SelectedTargetActorId);
			if (target.CurrentCell != facts.SelectedTargetCurrentCell)
				throw new InvalidOperationException("The selected Kite target position is inconsistent.");

			var score = StandardScore(friendly, enemy, facts.PlannedCurrentRangeEngagement);
			var approved = GeneralizedCombatLiveCellSafety.CanAttackSafely(calculator,
				friendly, enemy, Resolve(target.ActorId), facts.PlannedCell,
				facts.FormationRadiusCells, plannedTargetTypesOverride);
			return new StealthKiteSafetyResult(score, approved);
		}

		public StealthTargetThreatScore CalculateAttackCrossover(StealthKiteFallbackFacts facts)
		{
			if (facts == null || !facts.PlannedDecloak || !facts.PlannedAttack ||
				!facts.PlannedCurrentRangeEngagement)
				throw new ArgumentException("Kite fallback requires one explicit planned live attack.", nameof(facts));
			var friendly = facts.FriendlyActorIds.Select(Resolve).ToArray();
			var enemy = facts.EnemyActorIds.Select(Resolve).ToArray();
			var target = Resolve(facts.SelectedTargetActorId);
			if (target.Location != facts.SelectedTargetCurrentCell)
				throw new InvalidOperationException("The Kite fallback target moved during live evaluation.");
			return StandardScore(friendly, enemy, true);
		}

		StealthTargetThreatScore StandardScore(Actor[] friendly, Actor[] enemy,
			bool plannedCurrentRangeEngagement)
		{
			var result = calculator.CalculateLiveMixedGroupThreat(friendly, enemy,
				plannedTargetTypesOverride, plannedCurrentRangeEngagement);
			return new StealthTargetThreatScore(result.ThreatRating, result.Crossover);
		}

		Actor Resolve(uint actorId)
		{
			var actor = resolveLiveActor(actorId);
			if (actor == null || actor.IsDead || !actor.IsInWorld || actor.ActorID != actorId)
				throw new InvalidOperationException($"Kite actor {actorId} is not valid in the live World.");
			return actor;
		}
	}
}
