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
	/// <summary>Immutable lifecycle projection of the established shared stealth cache.</summary>
	sealed class StealthLifecycleStrategicView
	{
		public long Revision { get; }
		public StealthTargetAcquisitionCacheSnapshot Acquisition { get; }
		public StealthApproachStrategicCacheSnapshot Approach { get; }

		public StealthLifecycleStrategicView(long revision,
			StealthTargetAcquisitionCacheSnapshot acquisition,
			StealthApproachStrategicCacheSnapshot approach)
		{
			Revision = revision;
			Acquisition = acquisition ?? throw new ArgumentNullException(nameof(acquisition));
			Approach = approach ?? throw new ArgumentNullException(nameof(approach));
		}
	}

	#pragma warning disable SA1205
	abstract partial class StealthAIStateBase
	#pragma warning restore SA1205
	{
		internal static StealthLifecycleStrategicView ReadLifecycleStrategicView(Squad owner)
		{
			if (!TryReadLifecycleStrategicView(owner, out var view))
				throw new InvalidOperationException(
					"A stealth lifecycle strategic cache requires one live formation member.");
			return view;
		}

		internal static bool TryReadLifecycleStrategicView(Squad owner,
			out StealthLifecycleStrategicView view)
		{
			var formation = AirDecisionUnits(owner).Where(LiveLifecycleActor)
				.OrderBy(actor => actor.ActorID).ToArray();
			var representative = formation.FirstOrDefault();
			if (representative == null)
			{
				view = null;
				return false;
			}

			var cache = StealthInfluence(owner, representative) ?? throw new InvalidOperationException(
				"The established stealth influence cache is unavailable.");
			var coarseSize = StealthCoarseSize(owner);
			var routePenalty = owner.StealthDefinition.RouteThreatPenalty;
			var formationCloaked = formation.All(actor =>
				actor.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked));
			var danger = (representative.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked) ?
				cache.CloakedDanger : cache.Danger).ToArray();
			var friendly = LifecycleGroup(formation);
			var candidates = cache.Candidates.Where(candidate => LiveLifecycleActor(candidate.Actor) &&
				owner.Units.Any(unit => CanAttackTarget(unit, candidate.Actor)))
				.OrderBy(candidate => candidate.Actor.ActorID).ToArray();
			var targets = candidates.Select(candidate =>
			{
				var actor = candidate.Actor;
				var health = actor.TraitOrDefault<IHealth>();
				return new StealthStrategicTargetSnapshot(actor.ActorID,
					LifecycleCoarseCell(actor.Location, coarseSize), candidate.Priority,
					EconomicValue(actor), health?.HP ?? 0, health?.MaxHP ?? 0);
			}).ToArray();
			var enemyCells = targets.Select(target => target.StrategicCell).Distinct()
				.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
			var facts = Enumerable.Range(0, cache.Height).SelectMany(y =>
				Enumerable.Range(0, cache.Width).Select(x =>
				{
					var cell = new CPos(x, y);
					var defenders = DefenderPackage(cache, cell);
					var detected = cache.ThreatCoverageByCell.TryGetValue(cell, out var coverage) &&
						coverage.Any(threat => threat.DetectorRange > 0);
					return new StealthTargetThreatFacts(cell, friendly, LifecycleGroup(defenders),
						formationCloaked, detected, true);
				})).ToArray();
			var secondsPerCost = LifecycleSecondsPerCostUnit(owner, representative, coarseSize);
			var acquisition = new StealthTargetAcquisitionCacheSnapshot(cache.Width, cache.Height,
				danger, enemyCells, secondsPerCost, targets, facts, routePenalty);
			var approach = new StealthApproachStrategicCacheSnapshot(cache.Width, cache.Height,
				facts.Select(fact => new StealthApproachStrategicCellSnapshot(fact.StrategicCell,
					fact.EnemyGroup, fact.HasDetectorCoverage,
					fact.PlannedActionRevealsFormation)), routePenalty);
			view = new StealthLifecycleStrategicView(cache.Tick, acquisition, approach);
			return true;
		}

		internal static bool TryReadLifecycleStrategicRoute(Squad owner, uint actorId,
			CPos expectedStart, CPos destination, bool strategicCells, bool allowDangerousStart,
			out long revision, out IReadOnlyList<CPos> route)
		{
			revision = 0;
			route = Array.Empty<CPos>();
			var actor = owner.World.GetActorById(actorId);
			if (!LiveLifecycleActor(actor) || !owner.Units.Contains(actor))
				return false;
			var coarseSize = StealthCoarseSize(owner);
			if (expectedStart != LifecycleCoarseCell(actor.Location, coarseSize))
				return false;
			var cache = StealthInfluence(owner, actor);
			if (cache == null || destination.X < 0 || destination.Y < 0 ||
				destination.X >= cache.Width || destination.Y >= cache.Height)
				return false;
			var danger = actor.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked) ?
				cache.CloakedDanger : cache.Danger;
			var exact = StealthRouteToCell(owner, actor, cache, destination,
				danger, allowDangerousStart);
			if (exact == null)
				return false;
			revision = cache.Tick;
			route = strategicCells ? exact.Select(cell => LifecycleCoarseCell(cell, coarseSize))
				.Distinct().ToArray() : exact.ToArray();
			return route.Count != 0;
		}

		internal static bool TryReadLifecycleLongRoute(Squad owner, CPos liveDestination,
			out long revision, out IReadOnlyList<CPos> route)
		{
			var representative = AirDecisionUnits(owner).Where(LiveLifecycleActor)
				.OrderBy(actor => actor.ActorID).FirstOrDefault();
			if (representative == null)
			{
				revision = 0;
				route = Array.Empty<CPos>();
				return false;
			}

			var coarseSize = StealthCoarseSize(owner);
			return TryReadLifecycleStrategicRoute(owner, representative.ActorID,
				LifecycleCoarseCell(representative.Location, coarseSize),
				LifecycleCoarseCell(liveDestination, coarseSize), false, true,
				out revision, out route);
		}

		internal static bool TryReadLifecycleFleeRoute(Squad owner,
			StealthApproachMission mission, out long revision, out double danger,
			out IReadOnlyList<CPos> route)
		{
			var representative = AirDecisionUnits(owner).Where(LiveLifecycleActor)
				.OrderBy(actor => actor.ActorID).FirstOrDefault();
			if (representative == null)
			{
				revision = 0;
				danger = 0;
				route = Array.Empty<CPos>();
				return false;
			}

			var cache = StealthInfluence(owner, representative);
			if (cache == null)
			{
				revision = 0;
				danger = 0;
				route = Array.Empty<CPos>();
				return false;
			}

			var coarseSize = StealthCoarseSize(owner);
			var current = LifecycleCoarseCell(representative.Location, coarseSize);
			var cachedDanger = representative.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked) ?
				cache.CloakedDanger : cache.Danger;
			var offsets = new[]
			{
				new CVec(-2, -2), new CVec(0, -2), new CVec(2, -2), new CVec(-2, 0),
				new CVec(2, 0), new CVec(-2, 2), new CVec(0, 2), new CVec(2, 2)
			};
			var candidates = offsets.Select(offset => current + offset)
				.Where(cell => cell.X >= 0 && cell.Y >= 0 && cell.X < cache.Width && cell.Y < cache.Height)
				.OrderBy(cell => cachedDanger[cell.Y * cache.Width + cell.X])
				.ThenByDescending(cell => (cell - mission.StrategicCell).LengthSquared)
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X);
			foreach (var candidate in candidates)
				if (TryReadLifecycleStrategicRoute(owner, representative.ActorID, current,
					candidate, false, true, out revision, out route))
				{
					danger = Math.Max(0, cachedDanger[candidate.Y * cache.Width + candidate.X]);
					return true;
				}

			revision = cache.Tick;
			danger = 0;
			route = Array.Empty<CPos>();
			return false;
		}

		internal static IReadOnlyList<CPos> ReadLifecycleApproachRoute(Squad owner,
			CPos originStrategicCell, CPos destinationStrategicCell)
		{
			var representative = AirDecisionUnits(owner).Where(LiveLifecycleActor)
				.OrderBy(actor => actor.ActorID).FirstOrDefault();
			var coarseSize = StealthCoarseSize(owner);
			if (representative == null || originStrategicCell.X < 0 || originStrategicCell.Y < 0)
				return Array.Empty<CPos>();

			var cache = StealthInfluence(owner, representative);
			if (cache == null)
				return Array.Empty<CPos>();
			var danger = representative.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked) ?
				cache.CloakedDanger : cache.Danger;
			var current = LifecycleCoarseCell(representative.Location, coarseSize);
			var approaches = Enumerable.Range(-1, 3).SelectMany(y => Enumerable.Range(-1, 3)
				.Where(x => x != 0 || y != 0).Select(x => new CPos(
					destinationStrategicCell.X + x, destinationStrategicCell.Y + y)))
				.Where(cell => cell.X >= 0 && cell.Y >= 0 && cell.X < cache.Width && cell.Y < cache.Height)
				.OrderBy(cell => danger[cell.Y * cache.Width + cell.X])
				.ThenBy(cell => (cell - current).LengthSquared)
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X);
			foreach (var approach in approaches)
				if (TryReadLifecycleStrategicRoute(owner, representative.ActorID, current, approach,
					true, true, out _, out var route))
					return route;

			return Array.Empty<CPos>();
		}

		static StealthCombatGroupSnapshot[] LifecycleGroup(IEnumerable<Actor> actors)
		{
			return actors.GroupBy(actor => actor.Info.Name, StringComparer.OrdinalIgnoreCase)
				.OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
				.Select(group => new StealthCombatGroupSnapshot(group.Key, group.Count(),
					group.Select(EconomicValue).DefaultIfEmpty().Max())).ToArray();
		}

		static float LifecycleSecondsPerCostUnit(Squad owner, Actor representative, int coarseSize)
		{
			var speed = CurrentGroundSpeed(representative);
			if (speed <= 0)
				throw new InvalidOperationException("A stealth lifecycle route requires a mobile formation member.");
			return Math.Max(float.Epsilon,
				coarseSize * 1024f * owner.World.Timestep / (speed * 1000f));
		}

		static CPos LifecycleCoarseCell(CPos cell, int coarseSize)
		{
			return new CPos(cell.X / coarseSize, cell.Y / coarseSize);
		}

		static bool LiveLifecycleActor(Actor actor)
		{
			return actor != null && actor.IsInWorld && !actor.IsDead;
		}
	}
}
