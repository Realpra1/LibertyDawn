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
			StealthApproachMission mission, string sourceFingerprint)
		{
			var members = Members().Select(actor =>
			{
				var health = Health(actor);
				return new StealthRecalculateFleeMemberSnapshot(actor.ActorID, actor.Location,
					WeaponRange(actor), health.HP, health.Max,
					needsMovementOrder: actor.IsIdle);
			}).ToArray();
			var enemies = LocalEnemies().Select(actor =>
			{
				var health = Health(actor);
				return new StealthRecalculateFleeEnemySnapshot(actor.ActorID, actor.Info.Name,
					actor.Location, health.HP, health.Max, WeaponRange(actor), IsDetector(actor));
			}).ToArray();
			var center = Center();
			var mobile = Members().Select(actor => actor.TraitOrDefault<Mobile>()).FirstOrDefault();
			var candidates = CandidateCells(center, 8).Select(cell =>
				new StealthRecalculateFleeCandidateSnapshot(cell, mobile != null &&
					mobile.CanEnterCell(cell, null, BlockedByActor.Immovable),
					(cell - center).LengthSquared > 16, HasDetectorCoverage(cell))).ToArray();
			return new StealthRecalculateFleeLiveSnapshot(squad.World.WorldTick, members, enemies,
				candidates, FormationCloaked(), sourceFingerprint);
		}

		public StealthRepairLiveSnapshot ReadRepair(StealthRepairHandoff handoff)
		{
			var members = Members().Select(actor =>
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
						Members().FirstOrDefault()))).ToArray();
			var enemies = LocalEnemies().Select(actor =>
			{
				var health = Health(actor);
				return new StealthRepairEnemySnapshot(actor.ActorID, actor.Info.Name, actor.Location,
					health.HP, health.Max, WeaponRange(actor), IsDetector(actor));
			}).ToArray();
			var center = Center();
			var routes = options.Select(option =>
			{
				var route = CardinalRoute(center, option.CurrentCell).ToArray();
				if (route.Length == 0)
					route = new[] { option.CurrentCell };
				return new StealthRepairRouteSnapshot(option.ActorId,
					option.ActorId, route, true, route.Length > 8,
					route.Any(HasDetectorCoverage));
			}).ToArray();
			return new StealthRepairLiveSnapshot(squad.World.WorldTick, handoff.DamageEventId,
				handoff.DamageTick, handoff.DamageSourceActorId, handoff.DamageAmount,
				handoff.Resume.ContextFingerprint, members, options, enemies,
				Array.Empty<StealthRepairStaticActorSnapshot>(), routes, FormationCloaked());
		}

		IReadOnlyList<Actor> Members()
		{
			return squad.AirFormationUnits(bootstrapIfEmpty: true).Where(Live)
				.OrderBy(actor => actor.ActorID).ToArray();
		}

		IEnumerable<Actor> LocalEnemies()
		{
			var center = squad.World.Map.CenterOfCell(Center());
			var radius = WDist.FromCells(Math.Max(1, squad.SquadManager.Info.DangerScanRadius)).Length;
			return squad.World.Actors.Where(actor => Live(actor) &&
				squad.SquadManager.IsPreferredEnemyUnit(actor) &&
				(actor.CenterPosition - center).HorizontalLength <= radius)
				.OrderBy(actor => actor.ActorID);
		}

		CPos Center()
		{
			var members = Members();
			return members.Count == 0 ? default(CPos) : squad.World.Map.CellContaining(
				members.Select(actor => actor.CenterPosition).Average());
		}

		IEnumerable<CPos> CandidateCells(CPos center, int radius)
		{
			return Enumerable.Range(-radius, radius * 2 + 1).SelectMany(y =>
				Enumerable.Range(-radius, radius * 2 + 1)
					.Select(x => squad.World.Map.Clamp(new CPos(center.X + x, center.Y + y))))
				.Distinct().OrderBy(cell => cell.Y).ThenBy(cell => cell.X);
		}

		bool HasDetectorCoverage(CPos cell)
		{
			var position = squad.World.Map.CenterOfCell(cell);
			return squad.World.Actors.Where(actor => Live(actor) &&
				squad.SquadManager.IsPreferredEnemyUnit(actor)).Any(actor =>
				actor.TraitsImplementing<DetectCloaked>().Where(detector => !detector.IsTraitDisabled)
					.Any(detector => (actor.CenterPosition - position).HorizontalLength <= detector.Range.Length));
		}

		bool FormationCloaked()
		{
			var members = Members();
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
		readonly string fingerprint;

		public StealthRecalculateFleeLiveWorld(
			StealthSquadLifecycleRecoveryLiveAdapter adapter, string fingerprint)
		{
			this.adapter = adapter;
			this.fingerprint = fingerprint;
		}

		public StealthRecalculateFleeLiveSnapshot Read(StealthApproachMission mission)
		{
			return adapter.ReadFlee(mission, fingerprint);
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
