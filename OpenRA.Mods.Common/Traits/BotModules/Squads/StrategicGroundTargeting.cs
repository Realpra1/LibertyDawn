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

		public GroundTargetPlan(Actor actor)
		{
			Actor = actor;
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
			public List<Actor> Targets;
			public List<Actor> Screens;
			public long Utility;
			public int DefenderValue;
			public int Score;
			public bool EffectivelyUndefended;
		}

		static readonly Dictionary<SquadManagerBotModule, GroundWorldCache> WorldCaches =
			new Dictionary<SquadManagerBotModule, GroundWorldCache>();

		static int ActorValue(Actor actor)
		{
			return Math.Max(1, actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1);
		}

		static int TargetValue(Actor actor, SquadManagerBotModuleInfo info, bool harassment)
		{
			if (harassment && info.StealthHarassmentTargetPriority.Count > 0)
				return info.StealthHarassmentTargetPriority.TryGetValue(actor.Info.Name, out var harassmentValue) ?
					Math.Max(0, harassmentValue) : 0;

			var priorities = info.GroundTargetPriority;
			if (priorities.TryGetValue(actor.Info.Name, out var configured))
				return Math.Max(0, configured);

			if (actor.Info.HasTraitInfo<HarvesterInfo>())
				return info.GroundTargetHarvesterValue;
			if (!actor.Info.HasTraitInfo<BuildingInfo>())
				return info.GroundTargetUnitValue;
			if (actor.Info.HasTraitInfo<ProductionInfo>() || actor.Info.HasTraitInfo<RefineryInfo>())
				return info.GroundTargetProductionValue;

			return info.GroundTargetBuildingValue;
		}

		static int TargetUtility(Actor actor, SquadManagerBotModuleInfo info, bool harassment)
		{
			long value = TargetValue(actor, info, harassment);
			var economicValue = actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;
			value = value * (100 + Math.Min(100, economicValue / 100)) / 100;
			var health = actor.TraitOrDefault<IHealth>();
			if (health != null && health.MaxHP > 0)
				value = value * 10000 / (10000 + health.HP);

			return (int)Math.Clamp(value, 0, int.MaxValue);
		}

		static bool CanThreatenSquad(Actor enemy, Squad owner)
		{
			return owner.Units.Any(unit => StateBase.CanAttackTarget(enemy, unit));
		}

		static bool ThreatCoversTargets(Actor enemy, Squad owner, IEnumerable<Actor> targets)
		{
			var maximumRange = 0;
			foreach (var armament in enemy.TraitsImplementing<Armament>())
			{
				if (armament.IsTraitDisabled || !owner.Units.Any(unit =>
					armament.Weapon.IsValidTarget(unit.GetEnabledTargetTypes())))
					continue;

				maximumRange = Math.Max(maximumRange, armament.MaxRange().Length);
			}

			if (maximumRange <= 0)
				return false;

			var maximumRangeSquared = (long)maximumRange * maximumRange;
			return targets.Any(target =>
				(enemy.CenterPosition - target.CenterPosition).LengthSquared <= maximumRangeSquared);
		}

		static bool DetectorCoversTargets(Actor enemy, IEnumerable<Actor> targets)
		{
			var detector = enemy.TraitOrDefault<DetectCloaked>();
			if (detector == null || detector.Range.Length <= 0)
				return false;

			var rangeSquared = (long)detector.Range.Length * detector.Range.Length;
			return targets.Any(target =>
				(enemy.CenterPosition - target.CenterPosition).LengthSquared <= rangeSquared);
		}

		public static GroundTargetPlan FindBestTarget(Squad owner)
		{
			if (!owner.IsValid)
				return null;

			var info = owner.SquadManager.Info;
			var harassment = owner.Type == SquadType.StealthHarassment;
			var size = info.GroundInfluenceCellSize;
			var map = owner.World.Map;
			var center = owner.CenterPosition;
			var ownCell = map.CellContaining(center);
			var attackerValue = owner.Units.Sum(ActorValue);
			var squadSpeed = owner.Units.Select(a => a.Info.TraitInfoOrDefault<MobileInfo>())
				.Where(m => m != null).Select(m => m.Speed).DefaultIfEmpty(info.GroundTargetReferenceSpeed).Min();
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
				.Where(a => owner.Units.Any(u => StateBase.CanAttackTarget(u, a)))
				.Select(a => (Actor: a, Utility: TargetUtility(a, info, harassment)))
				.Where(a => a.Utility > 0).ToList();
			if (targets.Count == 0)
				return null;

			var grouped = targets.GroupBy(t => new CPos(t.Actor.Location.X / size, t.Actor.Location.Y / size))
				.OrderBy(g => g.Key.X).ThenBy(g => g.Key.Y).ToList();
			var distances = grouped.Select(g =>
			{
				var x = g.Key.X * size + size / 2;
				var y = g.Key.Y * size + size / 2;
				return (long)Math.Abs(ownCell.X - x) + Math.Abs(ownCell.Y - y);
			}).ToList();
			var utilities = grouped.Select(g => (int)Math.Min(int.MaxValue, g.Sum(t => (long)t.Utility))).ToList();
			var selected = AirThreatGeometry.SelectTargetCandidates(distances, utilities,
				info.GroundTargetClosestCandidates, info.GroundTargetHighestValueCandidates);
			var plans = new List<CellPlan>();

			foreach (var index in selected)
			{
				var group = grouped[index];
				var groupActors = group.Select(t => t.Actor).ToList();
				var cell = group.Key;
				var worldCell = map.Clamp(new CPos(cell.X * size + size / 2, cell.Y * size + size / 2));
				var screens = enemies.Where(e =>
				{
					var sameCell = e.Location.X / size == cell.X && e.Location.Y / size == cell.Y;
					return (CanThreatenSquad(e, owner) && (sameCell || ThreatCoversTargets(e, owner, groupActors))) ||
						(harassment && DetectorCoversTargets(e, groupActors));
				}).Distinct().OrderBy(a => a.ActorID).ToList();
				var defenderValue = screens.Sum(e => ActorValue(e) +
					(harassment && DetectorCoversTargets(e, groupActors) ? info.StealthHarassmentDetectorValue : 0));
				var pathCells = Math.Abs(ownCell.X - worldCell.X) + Math.Abs(ownCell.Y - worldCell.Y);
				var utility = group.Sum(t => (long)t.Utility);
				var score = StrategicGroundScoring.ScoreCell(utility, attackerValue, defenderValue,
					pathCells, squadSpeed, info.GroundTargetReferenceSpeed, info.GroundTargetDistancePenalty,
					info.GroundDefenderOvermatchDecayPercent);
				plans.Add(new CellPlan
				{
					Cell = cell,
					Targets = group.OrderByDescending(t => t.Utility).ThenBy(t => t.Actor.ActorID)
						.Select(t => t.Actor).ToList(),
					Screens = screens,
					Utility = utility,
					DefenderValue = defenderValue,
					Score = score,
					EffectivelyUndefended = StrategicGroundScoring.IsEffectivelyUndefended(attackerValue,
						defenderValue, info.GroundEffectivelyUndefendedRatio),
				});
			}

			CellPlan bestCell;
			Actor target;
			if (harassment)
			{
				// Harass exposed value first. Clearing a screen is a fallback only when every bounded
				// prize cell is defended and the entire screen is small enough for this squad.
				bestCell = plans.Where(p => p.EffectivelyUndefended)
					.OrderByDescending(p => p.Score).ThenBy(p => p.Cell.X).ThenBy(p => p.Cell.Y).FirstOrDefault();
				if (bestCell != null)
					target = bestCell.Targets[0];
				else
				{
					bestCell = plans.Where(p => p.DefenderValue <= (long)attackerValue *
						info.StealthHarassmentWeakScreenPercent / 100)
						.Where(p => p.Screens.Any(s => owner.Units.Any(u => StateBase.CanAttackTarget(u, s))))
						.OrderByDescending(p => p.Score).ThenBy(p => p.Cell.X).ThenBy(p => p.Cell.Y).FirstOrDefault();
					if (bestCell == null)
						return null;

					target = bestCell.Screens.Where(s => owner.Units.Any(u => StateBase.CanAttackTarget(u, s)))
						.OrderBy(ActorValue).ThenBy(a => a.ActorID).First();
				}
			}
			else
			{
				bestCell = plans.OrderByDescending(p => p.Score).ThenBy(p => p.Cell.X).ThenBy(p => p.Cell.Y).First();
				target = bestCell.Targets[0];
			}

			if (info.GroundTargetDebugLogging)
				Log.Write("debug", "Ground target [{0}] selected {1}#{2}: cell={3} utility={4} attackers={5} defenders={6} score={7} effectively-undefended={8} candidates={9}.",
					harassment ? "stealth" : "assault", target.Info.Name, target.ActorID, bestCell.Cell,
					bestCell.Utility, attackerValue, bestCell.DefenderValue, bestCell.Score,
					bestCell.EffectivelyUndefended, plans.Count);

			return new GroundTargetPlan(target);
		}
	}
}
