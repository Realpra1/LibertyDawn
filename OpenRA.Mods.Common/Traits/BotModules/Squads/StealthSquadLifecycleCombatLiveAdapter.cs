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
		readonly StealthSquadLiveLocalActors localActors;

		public StealthSquadLifecycleCombatLiveAdapter(Squad squad)
		{
			this.squad = squad ?? throw new ArgumentNullException(nameof(squad));
			localActors = new StealthSquadLiveLocalActors(squad);
		}

		StealthApproachLiveSnapshot IStealthApproachLiveWorld.Read(StealthApproachMission mission)
		{
			var memberActors = Members();
			var enemies = LocalEnemies(mission, memberActors);
			var detectors = DetectorCircles(enemies);
			var members = memberActors.Select(actor => new StealthApproachMemberSnapshot(actor.ActorID,
				Coarse(actor.Location), squad.AirReinforcements.Contains(actor.ActorID), actor.IsIdle)).ToArray();
			var defenderActors = enemies.Where(actor => IsDefender(actor, memberActors)).ToArray();
			var cloaked = FormationCloaked(memberActors);
			var detected = memberActors.Any(actor => HasDetectorCoverage(actor.CenterPosition, detectors));
			var safety = CurrentPositionSafety(memberActors, defenderActors, cloaked, detected, false);
			return new StealthApproachLiveSnapshot(TargetValid(mission, enemies), members, Group(memberActors),
				Group(enemies), defenderActors.Select(actor => actor.ActorID), cloaked,
				detected, true, safety.Threat == null,
				safety.Threat?.ActorID, safety.Threat?.Location, safety.Score);
		}

		public bool CurrentPlannedAttackSafe(StealthApproachMission mission)
		{
			var members = Members();
			if (members.Count == 0)
				return false;
			var enemies = LocalEnemies(mission, members);
			var defenders = enemies.Where(actor => IsDefender(actor, members)).ToArray();
			var detectors = DetectorCircles(enemies);
			var cloaked = FormationCloaked(members);
			var detected = members.Any(actor => HasDetectorCoverage(actor.CenterPosition, detectors));
			return CurrentPositionSafety(members, defenders, cloaked, detected, true).Threat == null;
		}

		(StealthTargetThreatScore Score, Actor Threat) CurrentPositionSafety(
			IReadOnlyList<Actor> members, IReadOnlyList<Actor> defenders,
			bool formationCloaked, bool detected, bool plannedDecloak)
		{
			if ((formationCloaked && !detected && !plannedDecloak) || defenders.Count == 0)
				return (new StealthTargetThreatScore(0, 0), null);

			var calculator = squad.SquadManager.CombatThreatCalculator;
			var threats = defenders.Select(defender =>
			{
				// Threat matchup and distance must describe the same live actor. Using a
				// nearest member's weapons with the squad-average position could declare an
				// exposed formation safe and prematurely cancel its escape.
				var representative = StealthSquadLiveLocalActors.Representative(members, defender);
				var pair = GeneralizedCombatPlannedDecloakThreat.Calculate(
					calculator, representative, defender, GroundTargetTypes);
				var margin = squad.StealthDefinition?.ThreatRangeBufferCells ?? 0;
				var distance = Math.Max(0,
					(representative.CenterPosition - defender.CenterPosition).HorizontalLength /
					1024d - margin);
				return (Actor: defender, Rating:
					GeneralizedCombatThreatCalculator.DefenderThreatAtDistance(
						pair, distance, includeDefenderHitRadius: true));
			}).OrderByDescending(item => item.Rating).ThenBy(item => item.Actor.ActorID).ToArray();
			var immediate = threats.FirstOrDefault();
			var score = new StealthTargetThreatScore(immediate.Rating, 0);
			return immediate.Rating > 0 ? (score, immediate.Actor) : (score, null);
		}

		StealthUndefendedAttackLiveSnapshot IStealthUndefendedAttackLiveWorld.Read(
			StealthApproachMission mission)
		{
			var memberActors = Members();
			var enemies = LocalEnemies(mission, memberActors);
			var detectors = DetectorCircles(enemies);
			var members = memberActors.Select(actor =>
			{
				var health = Health(actor);
				return new StealthUndefendedAttackMemberSnapshot(actor.ActorID, actor.Info.Name,
					Value(actor), actor.Location, health.HP, health.Max, WeaponRange(actor), actor.IsIdle);
			}).ToArray();
			var targets = enemies.Where(actor => Coarse(actor.Location) == mission.StrategicCell)
				.Select(actor =>
				{
					var health = Health(actor);
					return new StealthUndefendedAttackTargetSnapshot(actor.ActorID, actor.Info.Name,
						Coarse(actor.Location), actor.Location, Priority(actor), Value(actor),
						health.HP, health.Max);
				}).ToArray();
			var defenders = enemies.Where(actor => IsDefender(actor, memberActors))
				.Select(actor => actor.ActorID).ToArray();
			return new StealthUndefendedAttackLiveSnapshot(squad.World.WorldTick, members, targets,
				defenders, FormationCloaked(memberActors),
				memberActors.Any(actor => HasDetectorCoverage(actor.CenterPosition, detectors)), true);
		}

		StealthCrushLiveSnapshot IStealthCrushLiveWorld.Read(StealthApproachMission mission)
		{
			var memberActors = Members();
			var enemies = LocalEnemies(mission, memberActors);
			var detectors = DetectorCircles(enemies);
			var members = memberActors.Select(actor =>
				new StealthCrushMemberSnapshot(actor.ActorID, actor.Location,
					needsMovementOrder: actor.IsIdle)).ToArray();
			var actors = enemies.Select(actor => new StealthCrushActorSnapshot(
				actor.ActorID, actor.Info.Name, Coarse(actor.Location), actor.Location, Priority(actor),
				IsDefender(actor, memberActors), IsObjective(actor, mission), IsInfantry(actor), CanCrush(actor),
				memberActors.Any(member => DetectorCoversSegment(
					member.CenterPosition, actor.CenterPosition, detectors)))).ToArray();
			return new StealthCrushLiveSnapshot(squad.World.WorldTick, members, actors,
				FormationCloaked(memberActors));
		}

		StealthKiteLiveSnapshot IStealthKiteLiveWorld.Read(StealthApproachMission mission)
		{
			var memberActors = Members();
			var localEnemies = LocalEnemies(mission, memberActors);
			var detectors = DetectorCircles(localEnemies);
			var defenderActors = localEnemies.Where(actor => IsDefender(actor, memberActors)).ToArray();
			var cloaked = FormationCloaked(memberActors);
			var detected = memberActors.Any(actor =>
				HasDetectorCoverage(actor.CenterPosition, detectors));
			var members = memberActors.Select(actor =>
			{
				var health = Health(actor);
				return new StealthKiteMemberSnapshot(actor.ActorID, actor.Location, WeaponRange(actor),
					hitPoints: health.HP, maximumHitPoints: health.Max,
					needsMovementOrder: actor.IsIdle);
			}).ToArray();
			var actors = localEnemies.Select(actor =>
			{
				var health = Health(actor);
				return new StealthKiteActorSnapshot(actor.ActorID, actor.Info.Name, actor.Location,
					health.HP, health.Max, WeaponRange(actor), IsDefender(actor, memberActors),
					IsObjective(actor, mission), IsInfantry(actor), CanCrush(actor),
					HasDetectorCoverage(actor.CenterPosition, detectors),
					isInLocalEngagementArea: localActors.IsInEngagementArea(mission, memberActors, actor),
					priorityValue: (long)Priority(actor) * Value(actor));
			}).ToArray();
			var candidateCells = KiteCandidateCells(localEnemies, memberActors);
			return new StealthKiteLiveSnapshot(squad.World.WorldTick, members, actors,
				candidateCells,
				cloaked,
				formationDetected: detected,
				kitingEnabled: squad.StealthDefinition?.EnableKiting != false,
				minimumKitePriorityValue: squad.StealthDefinition?.MinimumKitePriorityValue ?? 0,
				currentPositionSafe: CurrentPositionSafety(memberActors, defenderActors,
					cloaked, detected, true).Threat == null);
		}

		bool IStealthKiteLiveWorld.CanReach(uint targetActorId, CPos cell)
		{
			var target = Resolve(targetActorId);
			var member = StealthSquadLiveLocalActors.Representative(Members(), target);
			var mobile = member?.TraitOrDefault<Mobile>();
			if (mobile == null || !squad.World.Map.Contains(cell) ||
				!mobile.CanEnterCell(cell, null, BlockedByActor.Immovable))
				return false;
			if (member.Location == cell)
				return true;
			return squad.World.WorldActor.Trait<IPathFinder>().FindUnitPath(
				member.Location, cell, member, null, BlockedByActor.Immovable).Count != 0;
		}

		uint? IStealthKiteLiveWorld.BlockingActor(uint targetActorId, CPos firingCell)
		{
			return BlockingActor(targetActorId, firingCell);
		}

		uint? IStealthMassAttackLiveWorld.BlockingActor(uint targetActorId, CPos firingCell)
		{
			return BlockingActor(targetActorId, firingCell);
		}

		uint? BlockingActor(uint targetActorId, CPos firingCell)
		{
			var target = Resolve(targetActorId);
			var member = StealthSquadLiveLocalActors.Representative(Members(), target);
			if (member == null || target == null)
				return null;

			var source = squad.World.Map.CenterOfCell(firingCell);
			return squad.World.FindBlockingActorsOnLine(source, target.CenterPosition, WDist.Zero)
				.Where(actor => actor != target && Live(actor) &&
					squad.SquadManager.IsPreferredEnemyUnit(actor) &&
					actor.TraitsImplementing<IBlocksProjectiles>().Any(blocker =>
						Exts.IsTraitEnabled(blocker) && blocker.ValidRelationships.HasRelationship(
							actor.Owner.RelationshipWith(member.Owner))))
				.OrderBy(actor => (actor.CenterPosition - source).HorizontalLengthSquared)
				.ThenBy(actor => actor.ActorID).Select(actor => (uint?)actor.ActorID).FirstOrDefault();
		}

		StealthMassAttackLiveSnapshot IStealthMassAttackLiveWorld.Read(
			StealthApproachMission mission, CPos attackCenter)
		{
			var memberActors = Members();
			var localEnemies = LocalEnemies(mission, memberActors);
			var package = localEnemies.Where(actor => IsObjective(actor, mission) ||
				ThreatensAttackArea(actor, attackCenter, memberActors)).ToArray();
			var detectors = DetectorCircles(package);
			var members = memberActors.Select(actor =>
			{
				var health = Health(actor);
				return new StealthMassAttackMemberSnapshot(actor.ActorID, actor.Location,
					WeaponRange(actor), health.HP, health.Max,
					needsMovementOrder: actor.IsIdle);
			}).ToArray();
			var actors = package.Select(actor =>
			{
				var health = Health(actor);
				return new StealthMassAttackActorSnapshot(actor.ActorID, actor.Info.Name, actor.Location,
					health.HP, health.Max, WeaponRange(actor), IsDefender(actor, memberActors),
					IsObjective(actor, mission), HasDetectorCoverage(actor.CenterPosition, detectors));
			}).ToArray();
			return new StealthMassAttackLiveSnapshot(squad.World.WorldTick, members, actors,
				CandidateCells(4), FormationCloaked(memberActors));
		}

		bool ThreatensAttackArea(Actor actor, CPos attackCenter,
			IReadOnlyList<Actor> members)
		{
			if (!IsDefender(actor, members))
				return false;
			var friendlyRange = members.Select(WeaponRange).DefaultIfEmpty().Min();
			var reach = WeaponRange(actor) + friendlyRange + 2;
			var dx = (long)actor.Location.X - attackCenter.X;
			var dy = (long)actor.Location.Y - attackCenter.Y;
			return dx * dx + dy * dy <= (long)reach * reach;
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

		Actor[] LocalEnemies(StealthApproachMission mission,
			IReadOnlyList<Actor> members)
		{
			return localActors.Enemies(mission, members);
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

		IEnumerable<CPos> KiteCandidateCells(IReadOnlyList<Actor> enemies,
			IReadOnlyList<Actor> members)
		{
			var mobile = members.Select(actor => actor.TraitOrDefault<Mobile>()).FirstOrDefault();
			if (mobile == null || enemies.Count == 0)
				return Array.Empty<CPos>();

			var range = Math.Max(1, members.Min(WeaponRange));
			var innerRange = Math.Max(0, range - 1);
			var outerSquared = range * range;
			var innerSquared = innerRange * innerRange;
			var offsets = Enumerable.Range(-range, range * 2 + 1).SelectMany(y =>
				Enumerable.Range(-range, range * 2 + 1).Select(x => new CVec(x, y)))
				.Where(offset =>
				{
					var distanceSquared = offset.X * offset.X + offset.Y * offset.Y;
					return distanceSquared <= outerSquared && distanceSquared > innerSquared;
				}).ToArray();
			return enemies.SelectMany(enemy =>
			{
				var target = squad.World.Map.CellContaining(enemy.CenterPosition);
				return offsets.Select(offset => squad.World.Map.Clamp(target + offset));
			}).Where(cell => mobile.CanEnterCell(cell, null, BlockedByActor.Immovable))
				.Distinct().OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
		}

		bool TargetValid(StealthApproachMission mission, IEnumerable<Actor> enemies)
		{
			return enemies.Any(actor => Coarse(actor.Location) == mission.StrategicCell);
		}

		bool IsObjective(Actor actor, StealthApproachMission mission)
		{
			return Coarse(actor.Location) == mission.StrategicCell && Priority(actor) > 0;
		}

		bool IsDefender(Actor actor, IReadOnlyList<Actor> members)
		{
			if (IsDetector(actor))
				return true;

			var representative = StealthSquadLiveLocalActors.Representative(members, actor);
			return representative != null && GeneralizedCombatPlannedDecloakThreat.Calculate(
				squad.SquadManager.CombatThreatCalculator, representative, actor,
				GroundTargetTypes).Reverse.CanTarget;
		}

		static bool HasDetectorCoverage(WPos position,
			IReadOnlyList<(WPos Center, int Range)> detectors)
		{
			return detectors.Any(detector =>
				(detector.Center - position).HorizontalLength <= detector.Range);
		}

		static (WPos Center, int Range)[] DetectorCircles(IEnumerable<Actor> enemies)
		{
			return enemies.SelectMany(actor =>
				actor.TraitsImplementing<DetectCloaked>().Where(detector => !detector.IsTraitDisabled)
					.Select(detector => (actor.CenterPosition, detector.Range.Length))).ToArray();
		}

		static bool DetectorCoversSegment(WPos start, WPos end,
			IReadOnlyList<(WPos Center, int Range)> detectors)
		{
			var dx = (double)end.X - start.X;
			var dy = (double)end.Y - start.Y;
			var lengthSquared = dx * dx + dy * dy;
			return detectors.Any(detector =>
			{
				var projection = lengthSquared <= 0 ? 0 : Math.Clamp(
					((detector.Center.X - start.X) * dx + (detector.Center.Y - start.Y) * dy) /
					lengthSquared, 0, 1);
				var closestX = start.X + projection * dx;
				var closestY = start.Y + projection * dy;
				var distanceX = detector.Center.X - closestX;
				var distanceY = detector.Center.Y - closestY;
				return distanceX * distanceX + distanceY * distanceY <=
					(double)detector.Range * detector.Range;
			});
		}

		bool CanCrush(Actor target)
		{
			if (squad.StealthDefinition?.CrushInfantryTargets == false)
				return false;

			var members = Members();
			var unit = StealthSquadLiveLocalActors.Representative(members, target);
			var mobile = unit?.TraitOrDefault<Mobile>();
			return mobile != null && target.TraitsImplementing<ICrushable>()
				.Any(crushable => crushable.CrushableBy(target, unit,
					mobile.Info.LocomotorInfo.Crushes));
		}

		static bool FormationCloaked(IReadOnlyList<Actor> members)
		{
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
