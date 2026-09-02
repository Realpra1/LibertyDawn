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

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Current-World-only views for Approach and all local combat owners.</summary>
	sealed class StealthSquadLifecycleCombatLiveAdapter : IStealthApproachLiveWorld,
		IStealthUndefendedAttackLiveWorld, IStealthCrushLiveWorld, IStealthKiteLiveWorld,
		IStealthMassAttackLiveWorld
	{
		static readonly BitSet<TargetableType> GroundTargetTypes =
			new BitSet<TargetableType>("Ground");
		readonly Squad squad;

		public StealthSquadLifecycleCombatLiveAdapter(Squad squad)
		{
			this.squad = squad ?? throw new ArgumentNullException(nameof(squad));
		}

		StealthApproachLiveSnapshot IStealthApproachLiveWorld.Read(StealthApproachMission mission)
		{
			var memberActors = Members();
			var members = memberActors.Select(actor => new StealthApproachMemberSnapshot(actor.ActorID,
				Coarse(actor.Location), squad.AirReinforcements.Contains(actor.ActorID), actor.IsIdle)).ToArray();
			var enemies = LocalEnemies(mission).ToArray();
			var defenderActors = enemies.Where(IsDefender).ToArray();
			var cloaked = FormationCloaked();
			var detected = memberActors.Any(actor => HasDetectorCoverage(actor.Location));
			var safety = CurrentPositionSafety(memberActors, defenderActors, cloaked, detected);
			return new StealthApproachLiveSnapshot(TargetValid(mission), members, Group(memberActors),
				Group(enemies), defenderActors.Select(actor => actor.ActorID), cloaked,
				detected, true, safety.Threat == null,
				safety.Threat?.ActorID, safety.Threat?.Location, safety.Score);
		}

		(StealthTargetThreatScore Score, Actor Threat) CurrentPositionSafety(
			IReadOnlyList<Actor> members, IReadOnlyList<Actor> defenders,
			bool formationCloaked, bool detected)
		{
			if ((formationCloaked && !detected) || defenders.Count == 0)
				return (new StealthTargetThreatScore(0, 0), null);

			var calculator = squad.SquadManager.CombatThreatCalculator;
			var threats = members.SelectMany(member => defenders.Select(defender =>
			{
				var pair = calculator.CalculateLive(member, defender, GroundTargetTypes, true);
				var dx = (long)member.Location.X - defender.Location.X;
				var dy = (long)member.Location.Y - defender.Location.Y;
				var distance = Math.Sqrt(dx * dx + dy * dy);
				return (Actor: defender, Rating:
					GeneralizedCombatThreatCalculator.DefenderThreatAtDistance(pair, distance));
			})).OrderByDescending(item => item.Rating).ThenBy(item => item.Actor.ActorID).ToArray();
			var immediate = threats.FirstOrDefault();
			var crossover = calculator.EstimateLiveMixedGroupCrossover(
				members, defenders, GroundTargetTypes, true);
			var score = new StealthTargetThreatScore(immediate.Rating, crossover);
			return immediate.Rating > 0 ? (score, immediate.Actor) : (score, null);
		}

		StealthUndefendedAttackLiveSnapshot IStealthUndefendedAttackLiveWorld.Read(
			StealthApproachMission mission)
		{
			var members = Members().Select(actor =>
			{
				var health = Health(actor);
				return new StealthUndefendedAttackMemberSnapshot(actor.ActorID, actor.Info.Name,
					Value(actor), actor.Location, health.HP, health.Max, WeaponRange(actor));
			}).ToArray();
			var enemies = LocalEnemies(mission).ToArray();
			var targets = enemies.Where(actor => Coarse(actor.Location) == mission.StrategicCell)
				.Select(actor =>
				{
					var health = Health(actor);
					return new StealthUndefendedAttackTargetSnapshot(actor.ActorID, actor.Info.Name,
						Coarse(actor.Location), actor.Location, Priority(actor), Value(actor),
						health.HP, health.Max);
				}).ToArray();
			var defenders = enemies.Where(IsDefender).Select(actor => actor.ActorID).ToArray();
			return new StealthUndefendedAttackLiveSnapshot(squad.World.WorldTick, members, targets,
				defenders, FormationCloaked(),
				Members().Any(actor => HasDetectorCoverage(actor.Location)), true);
		}

		StealthCrushLiveSnapshot IStealthCrushLiveWorld.Read(StealthApproachMission mission)
		{
			var members = Members().Select(actor =>
				new StealthCrushMemberSnapshot(actor.ActorID, actor.Location)).ToArray();
			var actors = LocalEnemies(mission).Select(actor => new StealthCrushActorSnapshot(
				actor.ActorID, actor.Info.Name, Coarse(actor.Location), actor.Location, Priority(actor),
				IsDefender(actor), IsObjective(actor, mission), IsInfantry(actor), CanCrush(actor),
				HasDetectorCoverage(actor.Location))).ToArray();
			return new StealthCrushLiveSnapshot(squad.World.WorldTick, members, actors, FormationCloaked());
		}

		StealthKiteLiveSnapshot IStealthKiteLiveWorld.Read(StealthApproachMission mission)
		{
			var members = Members().Select(actor =>
			{
				var health = Health(actor);
				return new StealthKiteMemberSnapshot(actor.ActorID, actor.Location, WeaponRange(actor),
					hitPoints: health.HP, maximumHitPoints: health.Max,
					needsMovementOrder: actor.IsIdle);
			}).ToArray();
			var actors = LocalEnemies(mission).Select(actor =>
			{
				var health = Health(actor);
				return new StealthKiteActorSnapshot(actor.ActorID, actor.Info.Name, actor.Location,
					health.HP, health.Max, WeaponRange(actor), IsDefender(actor),
					IsObjective(actor, mission), IsInfantry(actor), CanCrush(actor),
					HasDetectorCoverage(actor.Location));
			}).ToArray();
			return new StealthKiteLiveSnapshot(squad.World.WorldTick, members, actors,
				CandidateCells(4), FormationCloaked(),
				formationDetected: Members().Any(actor => HasDetectorCoverage(actor.Location)));
		}

		StealthMassAttackLiveSnapshot IStealthMassAttackLiveWorld.Read(StealthApproachMission mission)
		{
			var members = Members().Select(actor =>
			{
				var health = Health(actor);
				return new StealthMassAttackMemberSnapshot(actor.ActorID, actor.Location,
					WeaponRange(actor), health.HP, health.Max,
					needsMovementOrder: actor.IsIdle);
			}).ToArray();
			var actors = LocalEnemies(mission).Select(actor =>
			{
				var health = Health(actor);
				return new StealthMassAttackActorSnapshot(actor.ActorID, actor.Info.Name, actor.Location,
					health.HP, health.Max, WeaponRange(actor), IsDefender(actor),
					IsObjective(actor, mission), HasDetectorCoverage(actor.Location));
			}).ToArray();
			return new StealthMassAttackLiveSnapshot(squad.World.WorldTick, members, actors,
				CandidateCells(Math.Max(4, squad.SquadManager.Info.DangerScanRadius)), FormationCloaked());
		}

		public Actor Resolve(uint actorId)
		{
			var actor = squad.World.GetActorById(actorId);
			return Live(actor) && actor.ActorID == actorId ? actor : null;
		}

		public IReadOnlyList<Actor> Members()
		{
			return squad.AirFormationUnits(bootstrapIfEmpty: true).Where(Live)
				.OrderBy(actor => actor.ActorID).ToArray();
		}

		public CPos ActiveCenter()
		{
			var members = Members();
			if (members.Count == 0)
				throw new InvalidOperationException("A lifecycle owner has no active squad center.");
			return Coarse(squad.World.Map.CellContaining(members.Select(actor => actor.CenterPosition).Average()));
		}

		public IEnumerable<StealthActiveSquadTargetSnapshot> OtherActiveSquads()
		{
			return squad.SquadManager.Squads.Where(other => other != squad && other.Type == SquadType.Stealth &&
				other.IsValid && other.AirTargetStrategicCell.HasValue).Select(other =>
				new StealthActiveSquadTargetSnapshot(other.Units.Where(Live).Select(actor => actor.ActorID)
					.DefaultIfEmpty().Min(), other.AirTargetStrategicCell.Value));
		}

		IEnumerable<Actor> LocalEnemies(StealthApproachMission mission)
		{
			var members = Members();
			return squad.World.Actors.Where(actor => Live(actor) &&
				squad.SquadManager.IsPreferredEnemyUnit(actor) &&
				(StealthAIThreatGeometry.IsSameOrAdjacentCoarseCell(Coarse(actor.Location),
					mission.StrategicCell) || CanThreatenLocalAction(actor, members)))
				.OrderBy(actor => actor.ActorID);
		}

		static bool CanThreatenLocalAction(Actor enemy, IReadOnlyList<Actor> members)
		{
			var range = WeaponRange(enemy) + 4;
			if (range <= 4)
				return false;
			var rangeSquared = (long)range * range;
			return members.Any(member =>
			{
				var dx = (long)member.Location.X - enemy.Location.X;
				var dy = (long)member.Location.Y - enemy.Location.Y;
				return dx * dx + dy * dy <= rangeSquared;
			});
		}

		IEnumerable<CPos> CandidateCells(int radius)
		{
			var center = squad.World.Map.CellContaining(squad.AirFormationCenter);
			var mobile = Members().Select(actor => actor.TraitOrDefault<Mobile>()).FirstOrDefault();
			return Enumerable.Range(-radius, radius * 2 + 1).SelectMany(y =>
				Enumerable.Range(-radius, radius * 2 + 1)
				.Select(x => squad.World.Map.Clamp(new CPos(center.X + x, center.Y + y))))
				.Where(cell => mobile != null &&
					mobile.CanEnterCell(cell, null, BlockedByActor.Immovable))
				.Distinct().OrderBy(cell => cell.Y).ThenBy(cell => cell.X);
		}

		bool TargetValid(StealthApproachMission mission)
		{
			return squad.World.Actors.Any(actor => Live(actor) &&
				squad.SquadManager.IsPreferredEnemyUnit(actor) &&
				Coarse(actor.Location) == mission.StrategicCell);
		}

		bool IsObjective(Actor actor, StealthApproachMission mission)
		{
			return Coarse(actor.Location) == mission.StrategicCell && Priority(actor) > 0;
		}

		bool IsDefender(Actor actor) { return WeaponRange(actor) > 0 || IsDetector(actor); }

		bool HasDetectorCoverage(CPos cell)
		{
			var position = squad.World.Map.CenterOfCell(cell);
			return squad.World.Actors.Where(actor => Live(actor) &&
				squad.SquadManager.IsPreferredEnemyUnit(actor)).Any(actor =>
				actor.TraitsImplementing<DetectCloaked>().Where(detector => !detector.IsTraitDisabled)
					.Any(detector => (actor.CenterPosition - position).HorizontalLength <= detector.Range.Length));
		}

		bool CanCrush(Actor target)
		{
			return Members().All(unit =>
			{
				var mobile = unit.TraitOrDefault<Mobile>();
				return mobile != null && target.TraitsImplementing<ICrushable>()
					.Any(crushable => crushable.CrushableBy(target, unit,
						mobile.Info.LocomotorInfo.Crushes));
			});
		}

		bool FormationCloaked()
		{
			var members = Members();
			return members.Count != 0 && members.All(actor =>
				actor.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked));
		}

		int Priority(Actor actor)
		{
			var definition = squad.StealthDefinition;
			if (definition == null)
				return 0;
			var attack = definition.IncludeAttackGroup &&
				squad.StealthSquadIndex == definition.MaximumHarassmentGroups;
			var configured = attack ? definition.AttackTargetPriorities :
				definition.HarassmentTargetPriorities;
			if (configured.TryGetValue(actor.Info.Name, out var priority))
				return priority;
			if (definition.HarvesterTypes.Contains(actor.Info.Name) || actor.Info.HasTraitInfo<HarvesterInfo>())
				return 5000;
			if (actor.Info.HasTraitInfo<LineBuildNodeInfo>())
				return definition.WallTargetPriority;
			return actor.Info.HasTraitInfo<MobileInfo>() ? definition.TankTargetPriority :
				definition.StructureTargetPriority;
		}

		StealthCombatGroupSnapshot[] Group(IEnumerable<Actor> actors)
		{
			return actors.GroupBy(actor => actor.Info.Name, StringComparer.Ordinal)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.Select(group => new StealthCombatGroupSnapshot(group.Key, group.Count(),
					group.Sum(Value))).ToArray();
		}

		CPos Coarse(CPos cell)
		{
			var size = Math.Max(1, squad.StealthDefinition?.StrategicCellSize ?? 1);
			return new CPos(cell.X / size, cell.Y / size);
		}

		static (int HP, int Max) Health(Actor actor)
		{
			var health = actor.TraitOrDefault<IHealth>();
			return (health?.HP ?? 0, health?.MaxHP ?? 0);
		}

		static int Value(Actor actor)
		{
			return actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;
		}

		static bool Live(Actor actor)
		{
			return actor != null && actor.IsInWorld && !actor.IsDead;
		}

		static bool IsDetector(Actor actor)
		{
			return actor.TraitsImplementing<DetectCloaked>()
				.Any(detector => !detector.IsTraitDisabled);
		}

		static bool IsInfantry(Actor actor)
		{
			return actor.TraitsImplementing<ICrushable>().Any();
		}

		static int WeaponRange(Actor actor)
		{
			return actor.TraitsImplementing<Armament>()
				.Where(armament => !armament.IsTraitDisabled)
				.Select(armament => (int)Math.Ceiling(armament.MaxRange().Length / 1024f))
				.DefaultIfEmpty().Max();
		}
	}
}
