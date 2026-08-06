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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	sealed class GroundTargetPlan
	{
		public readonly Actor Actor;
		public readonly int Score;
		public readonly CPos StrategicCell;

		public GroundTargetPlan(Actor actor, int score, CPos strategicCell)
		{
			Actor = actor;
			Score = score;
			StrategicCell = strategicCell;
		}
	}

	static class StrategicGroundTargeting
	{
		sealed class GroundWorldCache
		{
			public int Tick;
			public List<Actor> Enemies;
		}

		sealed class CellPlan
		{
			public CPos Cell;
			public List<(Actor Actor, int Utility)> Targets;
			public long Utility;
			public int DefenderValue;
			public int EffectiveDefenderValue;
			public int DistanceCells;
			public int Score;
		}

		static readonly Dictionary<SquadManagerBotModule, GroundWorldCache> WorldCaches =
			new Dictionary<SquadManagerBotModule, GroundWorldCache>();

		static int ActorValue(Actor actor)
		{
			return Math.Max(1, actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1);
		}

		static int TargetValue(Actor actor, Squad owner, IReadOnlyCollection<Actor> formation)
		{
			var info = owner.SquadManager.Info;
			if (info.GroundTargetPriority.TryGetValue(actor.Info.Name, out var configured))
				return Math.Max(0, configured);

			var value = actor.Info.HasTraitInfo<HarvesterInfo>() ? info.GroundTargetHarvesterValue :
				actor.Info.HasTraitInfo<ProductionInfo>() || actor.Info.HasTraitInfo<RefineryInfo>() ?
					info.GroundTargetProductionValue :
				actor.Info.HasTraitInfo<BuildingInfo>() ? info.GroundTargetBuildingValue :
					info.GroundTargetUnitValue;
			if (!formation.Any(unit => StateBase.CanAttackTarget(actor, unit)))
				value += info.GroundTargetDefencelessBonus;

			return Math.Max(0, value);
		}

		static int TargetUtility(Actor actor, Squad owner, IReadOnlyCollection<Actor> formation)
		{
			long value = TargetValue(actor, owner, formation) + owner.SquadManager.GroundAirTargetBonus(actor);
			var economicValue = actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;
			value = value * (100 + Math.Min(100, economicValue / 100)) / 100;
			var health = actor.TraitOrDefault<IHealth>();
			if (health != null && health.MaxHP > 0)
				value = StrategicGroundScoring.RemainingHealthPriority(
					(int)Math.Min(int.MaxValue, value), health.HP, health.MaxHP);

			return (int)Math.Clamp(value, 0, int.MaxValue);
		}

		static bool CanThreatenFormation(Actor enemy, IReadOnlyCollection<Actor> formation)
		{
			return formation.Any(unit => StateBase.CanAttackTarget(enemy, unit));
		}

		static bool ThreatCoversTargets(Actor enemy, IReadOnlyCollection<Actor> formation,
			IEnumerable<Actor> targets)
		{
			var maximumRange = 0;
			foreach (var armament in enemy.TraitsImplementing<Armament>())
			{
				if (armament.IsTraitDisabled || !formation.Any(unit =>
					armament.Weapon.IsValidTarget(unit.GetEnabledTargetTypes())))
					continue;

				maximumRange = Math.Max(maximumRange, armament.MaxRange().Length);
			}

			if (maximumRange <= 0)
				return false;

			var rangeSquared = (long)maximumRange * maximumRange;
			return targets.Any(target =>
				(enemy.CenterPosition - target.CenterPosition).LengthSquared <= rangeSquared);
		}

		static bool FormationCanReach(Squad owner, IReadOnlyCollection<Actor> formation, CPos target)
		{
			var domainIndex = owner.World.WorldActor.TraitOrDefault<DomainIndex>();
			if (domainIndex == null)
				return true;

			foreach (var unit in formation)
			{
				var mobile = unit.TraitOrDefault<Mobile>();
				if (mobile != null && !domainIndex.IsPassable(unit.Location, target, mobile.Locomotor))
					return false;
			}

			return true;
		}

		public static GroundTargetPlan FindBestTarget(Squad owner)
		{
			var formation = owner.GroundFormationUnits(bootstrapIfEmpty: true);
			if (formation.Count == 0)
				return null;

			var info = owner.SquadManager.Info;
			var size = info.GroundInfluenceCellSize;
			var map = owner.World.Map;
			var center = owner.GroundFormationCenter;
			var ownCell = map.CellContaining(center);
			var attackerValue = formation.Sum(ActorValue);
			var slowestSpeed = formation.Select(a => a.Info.TraitInfoOrDefault<MobileInfo>())
				.Where(m => m != null).Select(m => m.Speed)
				.DefaultIfEmpty(info.GroundTargetReferenceSpeed).Min();

			if (!WorldCaches.TryGetValue(owner.SquadManager, out var cache) ||
				owner.World.WorldTick - cache.Tick >= info.GroundInfluenceCacheInterval)
			{
				cache = new GroundWorldCache
				{
					Tick = owner.World.WorldTick,
					Enemies = owner.World.Actors.Where(owner.SquadManager.IsPreferredEnemyUnit)
						.OrderBy(a => a.ActorID).ToList(),
				};
				WorldCaches[owner.SquadManager] = cache;
			}

			var enemies = cache.Enemies.Where(owner.SquadManager.IsPreferredEnemyUnit).ToList();
			var targets = enemies.Where(owner.SquadManager.IsNotHiddenUnit)
				.Where(a => formation.Any(u => StateBase.CanAttackTarget(u, a)))
				.Where(a => FormationCanReach(owner, formation, a.Location))
				.Select(a => (Actor: a, Utility: TargetUtility(a, owner, formation)))
				.Where(a => a.Utility > 0).ToList();
			if (targets.Count == 0)
				return null;

			var grouped = targets.GroupBy(t => new CPos(t.Actor.Location.X / size, t.Actor.Location.Y / size))
				.OrderBy(g => g.Key.Y).ThenBy(g => g.Key.X).ToList();
			var distances = grouped.Select(g =>
			{
				var cell = map.Clamp(new CPos(g.Key.X * size + size / 2, g.Key.Y * size + size / 2));
				return (map.CenterOfCell(cell) - center).LengthSquared;
			}).ToList();
			var utilities = grouped.Select(g => (int)Math.Min(int.MaxValue, g.Sum(t => (long)t.Utility))).ToList();
			var selected = AirThreatGeometry.SelectTargetCandidates(distances, utilities,
				info.GroundTargetClosestCandidates, info.GroundTargetHighestValueCandidates);
			if (info.GroundTargetHarvesterCandidates > 0)
			{
				var selectedSet = new HashSet<int>(selected);
				foreach (var index in Enumerable.Range(0, grouped.Count)
					.Where(i => grouped[i].Any(t => t.Actor.Info.HasTraitInfo<HarvesterInfo>()))
					.OrderBy(i => distances[i]).ThenBy(i => i).Take(info.GroundTargetHarvesterCandidates))
					selectedSet.Add(index);

				selected = selectedSet.OrderBy(i => i).ToList();
			}

			var plans = new List<CellPlan>();
			foreach (var index in selected)
			{
				var group = grouped[index];
				var groupActors = group.Select(t => t.Actor).ToList();
				var worldCell = map.Clamp(new CPos(group.Key.X * size + size / 2, group.Key.Y * size + size / 2));
				var defenders = enemies.Where(e =>
				{
					var sameCell = e.Location.X / size == group.Key.X && e.Location.Y / size == group.Key.Y;
					return CanThreatenFormation(e, formation) &&
						(sameCell || ThreatCoversTargets(e, formation, groupActors));
				}).Distinct().OrderBy(a => a.ActorID).ToList();
				var defenderValue = defenders.Sum(ActorValue);
				var distanceCells = (map.CenterOfCell(worldCell) - center).Length / 1024;
				var utility = group.Sum(t => (long)t.Utility);
				plans.Add(new CellPlan
				{
					Cell = group.Key,
					Targets = group.OrderByDescending(t => t.Utility).ThenBy(t => t.Actor.ActorID).ToList(),
					Utility = utility,
					DefenderValue = defenderValue,
					EffectiveDefenderValue = StrategicGroundScoring.EffectiveDefenderValue(attackerValue,
						defenderValue, info.GroundDefenderOvermatchDecayPercent),
					DistanceCells = distanceCells,
					Score = StrategicGroundScoring.ScoreCell(utility, attackerValue, defenderValue,
						distanceCells, slowestSpeed, info.GroundTargetReferenceSpeed,
						info.GroundTargetDistancePenalty, info.GroundDefenderOvermatchDecayPercent),
				});
			}

			var best = plans.OrderByDescending(p => p.Score).ThenBy(p => p.Cell.Y).ThenBy(p => p.Cell.X)
				.FirstOrDefault();
			if (best == null)
				return null;

			var target = best.Targets[0].Actor;
			if (info.GroundTargetDebugLogging)
				Log.Write("debug", "Ground target [{0}] selected {1}#{2}: cell={3} utility={4} attackers={5} defenders={6} effective-defenders={7} distance={8} slowest-speed={9} score={10} effectively-undefended={11} cells={12} formation={13} reinforcements={14} air-mark={15}.",
					owner.Bot.Player.PlayerName, target.Info.Name, target.ActorID, best.Cell, best.Utility,
					attackerValue, best.DefenderValue, best.EffectiveDefenderValue, best.DistanceCells, slowestSpeed,
					best.Score, StrategicGroundScoring.IsEffectivelyUndefended(attackerValue, best.DefenderValue,
						info.GroundEffectivelyUndefendedRatio), plans.Count, formation.Count,
					owner.GroundReinforcements.Count, owner.SquadManager.GroundAirTargetBonus(target));

			return new GroundTargetPlan(target, best.Score, best.Cell);
		}
	}
}
