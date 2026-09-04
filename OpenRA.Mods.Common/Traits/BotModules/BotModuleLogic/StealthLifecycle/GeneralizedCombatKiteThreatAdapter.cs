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
	/// Resolves each participant from the live World and uses the standard generalized
	/// calculator. Cell geometry only validates the requested current-range action; no
	/// second threat score, actor cache, or danger cutoff exists here.
	/// </summary>
	public sealed class GeneralizedCombatKiteThreatAdapter : IStealthKiteThreatAdapter
	{
		readonly GeneralizedCombatThreatCalculator calculator;
		readonly Func<uint, Actor> resolveLiveActor;
		readonly BitSet<TargetableType> plannedTargetTypesOverride;
		readonly int safetyMarginCells;
		readonly Dictionary<(uint Attacker, uint Defender),
			GeneralizedCombatThreatCalculator.PairThreat> pairThreats = new Dictionary<
				(uint Attacker, uint Defender), GeneralizedCombatThreatCalculator.PairThreat>();
		int cachedTick = -1;
		uint[] cachedFriendlyIds = Array.Empty<uint>();
		uint[] cachedEnemyIds = Array.Empty<uint>();
		StealthTargetThreatScore? cachedScore;

		public GeneralizedCombatKiteThreatAdapter(GeneralizedCombatThreatCalculator calculator,
			Func<uint, Actor> resolveLiveActor, BitSet<TargetableType> plannedTargetTypesOverride,
			int safetyMarginCells = 0)
		{
			if (safetyMarginCells < 0)
				throw new ArgumentOutOfRangeException(nameof(safetyMarginCells));
			this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
			this.resolveLiveActor = resolveLiveActor ?? throw new ArgumentNullException(nameof(resolveLiveActor));
			this.plannedTargetTypesOverride = plannedTargetTypesOverride;
			this.safetyMarginCells = safetyMarginCells;
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

			PrepareTick(friendly[0].World.WorldTick);
			var score = StandardScore(friendly, enemy, facts.PlannedCurrentRangeEngagement);
			var representative = Representative(friendly, enemy);
			var approved = GeneralizedCombatLiveCellSafety.CanAttackSafely(calculator,
				new[] { representative }, enemy, Resolve(target.ActorId), facts.PlannedCell,
				facts.FormationRadiusCells, plannedTargetTypesOverride,
				Pair, safetyMarginCells);
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
			PrepareTick(friendly[0].World.WorldTick);
			return StandardScore(friendly, enemy, true);
		}

		StealthTargetThreatScore StandardScore(Actor[] friendly, Actor[] enemy,
			bool plannedCurrentRangeEngagement)
		{
			var friendlyIds = friendly.Select(actor => actor.ActorID).ToArray();
			var enemyIds = enemy.Select(actor => actor.ActorID).ToArray();
			if (cachedScore.HasValue && cachedFriendlyIds.SequenceEqual(friendlyIds) &&
				cachedEnemyIds.SequenceEqual(enemyIds))
				return cachedScore.Value;
			var result = calculator.CalculateLiveMixedGroupThreat(friendly, enemy,
				plannedTargetTypesOverride, plannedCurrentRangeEngagement);
			var overmatch = GeneralizedCombatCrossover.Overmatch(
				friendly.Length, enemy.Length, result.Crossover);
			cachedFriendlyIds = friendlyIds;
			cachedEnemyIds = enemyIds;
			return (cachedScore = new StealthTargetThreatScore(
				result.ThreatRating, overmatch)).Value;
		}

		static Actor Representative(IReadOnlyList<Actor> friendly, IReadOnlyList<Actor> enemy)
		{
			return friendly.OrderBy(attacker => enemy.Min(defender =>
				(attacker.CenterPosition - defender.CenterPosition).HorizontalLengthSquared))
				.ThenBy(attacker => attacker.ActorID).First();
		}

		GeneralizedCombatThreatCalculator.PairThreat Pair(Actor attacker, Actor defender)
		{
			var key = (attacker.ActorID, defender.ActorID);
			if (!pairThreats.TryGetValue(key, out var pair))
				pairThreats.Add(key, pair = GeneralizedCombatPlannedDecloakThreat.Calculate(
					calculator, attacker, defender, plannedTargetTypesOverride));
			return pair;
		}

		void PrepareTick(int tick)
		{
			if (cachedTick == tick)
				return;
			cachedTick = tick;
			pairThreats.Clear();
			cachedFriendlyIds = Array.Empty<uint>();
			cachedEnemyIds = Array.Empty<uint>();
			cachedScore = null;
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
