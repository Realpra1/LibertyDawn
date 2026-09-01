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
	/// Resolves every participant from the current World and delegates all rating math to the
	/// standard generalized calculator. Candidate geometry applies only canonical current range,
	/// cloak, and detector targetability; actor type never excludes a live enemy.
	/// </summary>
	public sealed class GeneralizedCombatRecalculateFleeThreatAdapter :
		IStealthRecalculateFleeThreatAdapter
	{
		readonly GeneralizedCombatThreatCalculator calculator;
		readonly Func<uint, Actor> resolveLiveActor;
		readonly BitSet<TargetableType> plannedTargetTypesOverride;

		public GeneralizedCombatRecalculateFleeThreatAdapter(
			GeneralizedCombatThreatCalculator calculator, Func<uint, Actor> resolveLiveActor,
			BitSet<TargetableType> plannedTargetTypesOverride)
		{
			this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
			this.resolveLiveActor = resolveLiveActor ?? throw new ArgumentNullException(nameof(resolveLiveActor));
			this.plannedTargetTypesOverride = plannedTargetTypesOverride;
		}

		public StealthTargetThreatScore CalculateEntryCrossover(
			StealthRecalculateFleeEntryThreatFacts facts)
		{
			if (facts == null || !facts.PlannedDecloak || !facts.PlannedAttack ||
				!facts.PlannedCurrentRangeEngagement)
				throw new ArgumentException("Entry revalidation requires the exact planned live attack.", nameof(facts));
			var friendly = facts.MemberActorIds.Select(Resolve).ToArray();
			var enemies = facts.Enemies.Select(enemy => ResolveExact(enemy.ActorId, enemy.CurrentCell)).ToArray();
			var target = ResolveExact(facts.SelectedTargetActorId, facts.SelectedTargetCurrentCell);
			if (!enemies.Contains(target))
				throw new InvalidOperationException("Entry target is not one of the exact current enemies.");
			return StandardEntryScore(friendly, enemies);
		}

		public StealthTargetThreatScore CalculateRouteDanger(
			StealthRecalculateFleeThreatFacts facts)
		{
			if (facts == null || facts.PlannedDecloak || facts.PlannedAttack ||
				facts.PlannedCurrentRangeEngagement)
				throw new ArgumentException("Flee movement must not reveal or attack.", nameof(facts));
			var friendly = facts.Members.Select(member =>
				ResolveExact(member.ActorId, member.CurrentCell)).ToArray();
			var coveringFacts = facts.Enemies.Where(enemy =>
				(!facts.FormationCloaked || facts.HasDetectorCoverage) &&
				DistanceSquared(enemy.CurrentCell, facts.CandidateCell) <=
					(long)enemy.CurrentWeaponRangeCells * enemy.CurrentWeaponRangeCells).ToArray();
			foreach (var enemy in facts.Enemies)
				ResolveExact(enemy.ActorId, enemy.CurrentCell);
			if (coveringFacts.Length == 0)
				return new StealthTargetThreatScore(0, 0);
			var covering = coveringFacts.Select(enemy => Resolve(enemy.ActorId)).ToArray();
			return StandardRouteScore(friendly, covering);
		}

		StealthTargetThreatScore StandardEntryScore(Actor[] friendly, Actor[] enemies)
		{
			var crossover = calculator.EstimateLiveMixedGroupCrossover(friendly, enemies,
				plannedTargetTypesOverride, true);
			var maximumThreat = friendly.SelectMany(attacker => enemies.Select(defender =>
				calculator.CalculateLive(attacker, defender, plannedTargetTypesOverride, true)
					.DefenderThreatInAttackerEquivalents))
				.DefaultIfEmpty().Max();
			return CheckedScore(maximumThreat, crossover);
		}

		StealthTargetThreatScore StandardRouteScore(Actor[] friendly, Actor[] covering)
		{
			var crossover = calculator.EstimateLiveMixedGroupCrossover(friendly, covering,
				plannedTargetTypesOverride, true);
			var cumulativeThreat = SumFinite(friendly.SelectMany(attacker => covering.Select(defender =>
				calculator.CalculateLive(attacker, defender, plannedTargetTypesOverride, true)
					.DefenderThreatInAttackerEquivalents)));
			return CheckedScore(cumulativeThreat, crossover);
		}

		static double SumFinite(System.Collections.Generic.IEnumerable<double> contributions)
		{
			var sum = 0d;
			foreach (var contribution in contributions)
			{
				if (!double.IsFinite(contribution) || contribution < 0 ||
					contribution > double.MaxValue - sum)
					throw new InvalidOperationException(
						"Standard RecalculateFlee threat contributions must have a finite sum.");
				sum += contribution;
			}

			return sum;
		}

		static StealthTargetThreatScore CheckedScore(double threat, double crossover)
		{
			if (!double.IsFinite(threat) || threat < 0 || !double.IsFinite(crossover) || crossover < 0)
				throw new InvalidOperationException("Standard RecalculateFlee scores must be finite and nonnegative.");
			return new StealthTargetThreatScore(threat, crossover);
		}

		Actor ResolveExact(uint actorId, CPos cell)
		{
			var actor = Resolve(actorId);
			if (actor.Location != cell)
				throw new InvalidOperationException($"RecalculateFlee actor {actorId} moved during live evaluation.");
			return actor;
		}

		Actor Resolve(uint actorId)
		{
			var actor = resolveLiveActor(actorId);
			if (actor == null || actor.IsDead || !actor.IsInWorld || actor.ActorID != actorId)
				throw new InvalidOperationException($"RecalculateFlee actor {actorId} is not valid in the live World.");
			return actor;
		}

		static long DistanceSquared(CPos left, CPos right)
		{
			var dx = (long)left.X - right.X;
			var dy = (long)left.Y - right.Y;
			return dx * dx + dy * dy;
		}
	}
}
