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
	/// Resolves every local identity from the current World and delegates all danger math to the
	/// standard generalized calculator. Route geometry applies only current range and cloak/detector
	/// targetability; no actor type is excluded and Repair never plans reveal or attack.
	/// </summary>
	public sealed class GeneralizedCombatRepairThreatAdapter : IStealthRepairThreatAdapter
	{
		readonly GeneralizedCombatThreatCalculator calculator;
		readonly Func<uint, Actor> resolveLiveActor;
		readonly BitSet<TargetableType> plannedTargetTypesOverride;

		public GeneralizedCombatRepairThreatAdapter(GeneralizedCombatThreatCalculator calculator,
			Func<uint, Actor> resolveLiveActor, BitSet<TargetableType> plannedTargetTypesOverride)
		{
			this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
			this.resolveLiveActor = resolveLiveActor ?? throw new ArgumentNullException(nameof(resolveLiveActor));
			this.plannedTargetTypesOverride = plannedTargetTypesOverride;
		}

		public StealthTargetThreatScore CalculateRouteDanger(StealthRepairThreatFacts facts)
		{
			if (facts == null || facts.PlannedDecloak || facts.PlannedAttack ||
				facts.PlannedCurrentRangeEngagement)
				throw new ArgumentException("Repair movement must not reveal or attack.", nameof(facts));
			var friendly = facts.Members.Select(member => ResolveExact(member.ActorId,
				member.CurrentCell)).ToArray();
			foreach (var enemy in facts.Enemies)
				ResolveExact(enemy.ActorId, enemy.CurrentCell);
			var coveringFacts = facts.Enemies.Where(enemy =>
				(!facts.FormationCloaked || facts.HasDetectorCoverage) && facts.RouteCells.Any(cell =>
					DistanceSquared(enemy.CurrentCell, cell) <=
						(long)enemy.CurrentWeaponRangeCells * enemy.CurrentWeaponRangeCells)).ToArray();
			if (coveringFacts.Length == 0)
				return new StealthTargetThreatScore(0, 0);
			var covering = coveringFacts.Select(enemy => Resolve(enemy.ActorId)).ToArray();
			var crossover = calculator.EstimateLiveMixedGroupCrossover(friendly, covering,
				plannedTargetTypesOverride, true);
			var cumulativeThreat = SumFinite(friendly.SelectMany(attacker => covering.Select(defender =>
				calculator.CalculateLive(attacker, defender, plannedTargetTypesOverride, true)
					.DefenderThreatInAttackerEquivalents)));
			if (!double.IsFinite(crossover) || crossover < 0)
				throw new InvalidOperationException("Standard Repair crossover must be finite and nonnegative.");
			return new StealthTargetThreatScore(cumulativeThreat, crossover);
		}

		static double SumFinite(IEnumerable<double> contributions)
		{
			var sum = 0d;
			foreach (var contribution in contributions)
			{
				if (!double.IsFinite(contribution) || contribution < 0 ||
					contribution > double.MaxValue - sum)
					throw new InvalidOperationException(
						"Standard Repair threat contributions must have a finite sum.");
				sum += contribution;
			}

			return sum;
		}

		Actor ResolveExact(uint actorId, CPos cell)
		{
			var actor = Resolve(actorId);
			if (actor.Location != cell)
				throw new InvalidOperationException($"Repair actor {actorId} moved during live evaluation.");
			return actor;
		}

		Actor Resolve(uint actorId)
		{
			var actor = resolveLiveActor(actorId);
			if (actor == null || actor.IsDead || !actor.IsInWorld || actor.ActorID != actorId)
				throw new InvalidOperationException($"Repair actor {actorId} is not valid in the live World.");
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
