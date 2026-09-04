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
	/// <summary>Current-World-only Step 5 and Repair views.</summary>
	sealed class StealthSquadLifecycleRecoveryLiveAdapter
	{
		readonly Squad squad;

		public StealthSquadLifecycleRecoveryLiveAdapter(Squad squad)
		{
			this.squad = squad ?? throw new ArgumentNullException(nameof(squad));
		}

		public StealthRecalculateFleeLiveSnapshot ReadFlee(
			StealthApproachMission mission, uint escapeThreatActorId, string sourceFingerprint)
		{
			var memberActors = Members();
			var members = memberActors.Select(actor =>
			{
				var health = Health(actor);
				return new StealthRecalculateFleeMemberSnapshot(actor.ActorID, actor.Location,
					WeaponRange(actor), health.HP, health.Max,
					needsMovementOrder: actor.IsIdle);
			}).ToArray();
			var target = squad.World.GetActorById(escapeThreatActorId);
			var enemies = new[] { target }.Where(actor => Live(actor) &&
				squad.SquadManager.IsPreferredEnemyUnit(actor)).Select(actor =>
			{
				var health = Health(actor);
				return new StealthRecalculateFleeEnemySnapshot(actor.ActorID, actor.Info.Name,
					actor.Location, health.HP, health.Max, WeaponRange(actor), IsDetector(actor));
			}).ToArray();
			return new StealthRecalculateFleeLiveSnapshot(squad.World.WorldTick, members, enemies,
				FormationCloaked(memberActors), sourceFingerprint);
		}

		public StealthRepairLiveSnapshot ReadRepair(StealthRepairHandoff handoff)
		{
			var memberActors = Members();
			var center = Center(memberActors);
			var members = memberActors.Select(actor =>
			{
				var health = Health(actor);
				return new StealthRepairMemberSnapshot(actor.ActorID, actor.Location,
					WeaponRange(actor), health.HP, health.Max,
					needsMovementOrder: actor.IsIdle);
			}).ToArray();
			var options = squad.World.ActorsHavingTrait<RepairsUnits>()
				.Where(actor => Live(actor) && actor.Owner.IsAlliedWith(squad.Bot.Player))
				.Select(actor => new StealthRepairOptionSnapshot(actor.ActorID,
					actor.Location, Reservable.IsAvailableFor(actor,
						memberActors.FirstOrDefault()))).ToArray();
			var enemies = LocalEnemies(center).Select(actor =>
			{
				var health = Health(actor);
				return new StealthRepairEnemySnapshot(actor.ActorID, actor.Info.Name, actor.Location,
					health.HP, health.Max, WeaponRange(actor), IsDetector(actor));
			}).ToArray();
			var detectors = EnemyDetectorCoverage().ToArray();
			var routes = options.Select(option =>
			{
				var route = CardinalRoute(center, option.CurrentCell).ToArray();
				if (route.Length == 0)
					route = new[] { option.CurrentCell };
				return new StealthRepairRouteSnapshot(option.ActorId,
					option.ActorId, route, true, route.Length > 8,
					route.Any(cell => HasDetectorCoverage(cell, detectors)));
			}).ToArray();
			return new StealthRepairLiveSnapshot(squad.World.WorldTick, handoff.DamageEventId,
				handoff.DamageTick, handoff.DamageSourceActorId, handoff.DamageAmount,
				handoff.Resume.ContextFingerprint, members, options, enemies,
				Array.Empty<StealthRepairStaticActorSnapshot>(), routes, FormationCloaked(memberActors));
		}

		public bool HasRepairOption()
		{
			return squad.World.ActorsHavingTrait<RepairsUnits>()
				.Any(actor => Live(actor) && actor.Owner.IsAlliedWith(squad.Bot.Player));
		}

		IReadOnlyList<Actor> Members()
		{
			return squad.AirFormationUnits(bootstrapIfEmpty: true).Where(Live)
				.OrderBy(actor => actor.ActorID).ToArray();
		}

		IEnumerable<Actor> LocalEnemies(CPos centerCell)
		{
			var center = squad.World.Map.CenterOfCell(centerCell);
			var radius = WDist.FromCells(Math.Max(1, squad.SquadManager.Info.DangerScanRadius)).Length;
			return squad.World.Actors.Where(actor => Live(actor) &&
				squad.SquadManager.IsPreferredEnemyUnit(actor) &&
				(actor.CenterPosition - center).HorizontalLength <= radius)
				.OrderBy(actor => actor.ActorID);
		}

		CPos Center(IReadOnlyList<Actor> members)
		{
			return members.Count == 0 ? default(CPos) : squad.World.Map.CellContaining(
				members.Select(actor => actor.CenterPosition).Average());
		}

		IEnumerable<(WPos Position, int Range)> EnemyDetectorCoverage()
		{
			return squad.World.Actors.Where(actor => Live(actor) &&
				squad.SquadManager.IsPreferredEnemyUnit(actor)).SelectMany(actor =>
					actor.TraitsImplementing<DetectCloaked>()
						.Where(detector => !detector.IsTraitDisabled)
						.Select(detector => (actor.CenterPosition, detector.Range.Length)));
		}

		bool HasDetectorCoverage(CPos cell, IReadOnlyList<(WPos Position, int Range)> detectors)
		{
			var position = squad.World.Map.CenterOfCell(cell);
			return detectors.Any(detector =>
				(detector.Position - position).HorizontalLength <= detector.Range);
		}

		static bool FormationCloaked(IReadOnlyList<Actor> members)
		{
			return members.Count != 0 && members.All(actor =>
				actor.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked));
		}

		static IEnumerable<CPos> CardinalRoute(CPos start, CPos destination)
		{
			var current = start;
			while (current.X != destination.X)
			{
				current = new CPos(current.X + Math.Sign(destination.X - current.X), current.Y);
				yield return current;
			}

			while (current.Y != destination.Y)
			{
				current = new CPos(current.X, current.Y + Math.Sign(destination.Y - current.Y));
				yield return current;
			}
		}

		static (int HP, int Max) Health(Actor actor)
		{
			var health = actor.TraitOrDefault<IHealth>();
			return (health?.HP ?? 0, health?.MaxHP ?? 0);
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

		static int WeaponRange(Actor actor)
		{
			return actor.TraitsImplementing<Armament>()
				.Where(armament => !armament.IsTraitDisabled)
				.Select(armament => (int)Math.Ceiling(armament.MaxRange().Length / 1024f))
				.DefaultIfEmpty().Max();
		}
	}

	sealed class StealthRecalculateFleeLiveWorld : IStealthRecalculateFleeLiveWorld
	{
		readonly StealthSquadLifecycleRecoveryLiveAdapter adapter;
		readonly StealthSquadLifecycleCombatLiveAdapter combat;
		readonly uint escapeThreatActorId;
		readonly string fingerprint;

		public StealthRecalculateFleeLiveWorld(
			StealthSquadLifecycleRecoveryLiveAdapter adapter,
			StealthSquadLifecycleCombatLiveAdapter combat, uint escapeThreatActorId,
			string fingerprint)
		{
			this.adapter = adapter;
			this.combat = combat;
			this.escapeThreatActorId = escapeThreatActorId;
			this.fingerprint = fingerprint;
		}

		public StealthRecalculateFleeLiveSnapshot Read(StealthApproachMission mission)
		{
			var live = adapter.ReadFlee(mission, escapeThreatActorId, fingerprint);
			var currentPositionSafe = combat.CurrentPlannedAttackSafe(mission);
			return new StealthRecalculateFleeLiveSnapshot(live.Tick, live.Members, live.Enemies,
				live.FormationCloaked, live.SourceFingerprint,
				currentPositionSafe: currentPositionSafe);
		}
	}

	sealed class StealthRepairLiveWorld : IStealthRepairLiveWorld
	{
		readonly StealthSquadLifecycleRecoveryLiveAdapter adapter;
		readonly StealthRepairHandoff handoff;

		public StealthRepairLiveWorld(StealthSquadLifecycleRecoveryLiveAdapter adapter,
			StealthRepairHandoff handoff)
		{
			this.adapter = adapter;
			this.handoff = handoff;
		}

		public StealthRepairLiveSnapshot Read(StealthApproachMission mission)
		{
			return adapter.ReadRepair(handoff);
		}
	}
}
