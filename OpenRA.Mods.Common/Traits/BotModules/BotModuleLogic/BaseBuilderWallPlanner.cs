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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Pathfinder;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Plans straight wall lines around the bot's defensive structures and hands them to
	/// <see cref="BaseBuilderQueueManager"/> one LineBuild anchor at a time.
	///
	/// Two independent guarantees stop the bot from sealing itself in:
	///  * Structural: at most three of the four sides of a ring are ever built, and the side that
	///    gets dropped first is always the one facing the middle of our own base. A ring therefore
	///    can never be closed, whatever else happens.
	///  * Behavioural: before a ring is accepted, the area reachable from the construction yard is
	///    flood filled twice - once as the world is now, once with the planned wall cells treated
	///    as solid. If the second flood loses reachable area, loses access to tiberium, or loses
	///    the ability to get clear of the base, the ring is thrown away.
	///
	/// The planner never touches an RNG, so the same world state always produces the same plan.
	/// Bot modules run host-only inside Sync.RunUnsynced; where the surrounding bot code does need
	/// randomness it uses World.LocalRandom, never SharedRandom.
	/// </summary>
	class BaseBuilderWallPlanner
	{
		const int MaxHandledTargets = 128;

		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		readonly IResourceLayer resourceLayer;

		// Anchor cells that still need a LineBuild order, in the order they must be placed.
		readonly List<CPos> pendingAnchors = new List<CPos>();

		// Locations of structures we have already tried to ring, so we don't re-plan them forever.
		readonly List<CPos> handledTargets = new List<CPos>();

		Locomotor locomotor;
		BuildingInfluence buildingInfluence;
		bool worldTraitsResolved;

		public BaseBuilderWallPlanner(BaseBuilderBotModule baseBuilder, Player player, IResourceLayer resourceLayer)
		{
			this.baseBuilder = baseBuilder;
			this.player = player;
			this.resourceLayer = resourceLayer;
			world = player.World;
		}

		BaseBuilderBotModuleInfo Info { get { return baseBuilder.Info; } }

		public bool Enabled { get { return Info.MaximumWallSegments > 0 && Info.WallTypes.Count > 0; } }

		public bool IsWallType(string actorType)
		{
			return Info.WallTypes.Contains(actorType);
		}

		/// <summary>Gate used when deciding what to put into the production queue.</summary>
		public bool WantsToBuildWall(string actorType, Actor[] playerBuildings)
		{
			if (!Enabled || !IsWallType(actorType))
				return false;

			var wallCount = 0;
			for (var i = 0; i < playerBuildings.Length; i++)
				if (Info.WallTypes.Contains(playerBuildings[i].Info.Name))
					wallCount++;

			if (wallCount >= Info.MaximumWallSegments)
				return false;

			return PeekAnchor(actorType) != null;
		}

		/// <summary>Consumes the next anchor. The caller issues a "LineBuild" order for it.</summary>
		public CPos? TakeWallCell(string actorType)
		{
			var cell = PeekAnchor(actorType);
			if (cell != null)
				pendingAnchors.RemoveAt(0);

			return cell;
		}

		CPos? PeekAnchor(string actorType)
		{
			if (!Enabled)
				return null;

			var actorInfo = world.Map.Rules.Actors[actorType];
			var bi = actorInfo.TraitInfoOrDefault<BuildingInfo>();
			if (bi == null || actorInfo.TraitInfoOrDefault<LineBuildInfo>() == null)
				return null;

			DropStaleAnchors(actorInfo, bi);

			if (pendingAnchors.Count == 0)
			{
				PlanNextRing(actorInfo, bi);
				DropStaleAnchors(actorInfo, bi);
			}

			if (pendingAnchors.Count == 0)
				return null;

			return pendingAnchors[0];
		}

		void DropStaleAnchors(ActorInfo actorInfo, BuildingInfo bi)
		{
			// Anchors are planned well before the wall finishes building, so the world may have
			// moved on. Anything we can no longer legally start a building on is dropped.
			while (pendingAnchors.Count > 0 && !CanAnchorAt(pendingAnchors[0], actorInfo, bi))
				pendingAnchors.RemoveAt(0);
		}

		bool CanAnchorAt(CPos cell, ActorInfo actorInfo, BuildingInfo bi)
		{
			return world.CanPlaceBuilding(cell, actorInfo, bi, null)
				&& bi.IsCloseEnoughToBase(world, player, actorInfo, cell);
		}

		void ResolveWorldTraits()
		{
			if (worldTraitsResolved)
				return;

			worldTraitsResolved = true;
			buildingInfluence = world.WorldActor.TraitOrDefault<BuildingInfluence>();
			locomotor = world.WorldActor.TraitsImplementing<Locomotor>()
				.FirstOrDefault(l => l.Info.Name == Info.WallPathCheckLocomotor);

			if (locomotor == null)
				AIUtils.BotDebug("{0} has no locomotor named '{1}'; wall pathability checks ignore terrain.",
					player, Info.WallPathCheckLocomotor);
		}

		void PlanNextRing(ActorInfo wallInfo, BuildingInfo wallBuildingInfo)
		{
			ResolveWorldTraits();

			var defenseCenter = baseBuilder.DefenseCenter;
			var baseCenter = baseBuilder.GetRandomBaseCenter();

			// Deterministic ordering: closest to the current hot spot first, ActorID as the tie break.
			var candidates = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead
					&& Info.WallRingTypes.Contains(a.Info.Name)
					&& !handledTargets.Contains(a.Location))
				.OrderBy(a => (a.Location - defenseCenter).LengthSquared)
				.ThenBy(a => a.ActorID)
				.Take(Info.WallPlanAttempts.Clamp(1, 8))
				.ToList();

			if (candidates.Count == 0)
				return;

			var pathStart = FindPathCheckStart(baseCenter);
			var haveBaseline = false;
			var baseline = default(BotWallGeometry.FloodResult);
			if (pathStart != null)
			{
				baseline = RunFlood(pathStart.Value, null);
				haveBaseline = true;
			}

			foreach (var target in candidates)
			{
				// Whatever happens, don't consider this structure again - re-evaluating a ring that
				// already failed every time the queue asks for a wall is not worth the scan.
				MarkHandled(target.Location);

				var anchors = new List<CPos>();
				var planned = new HashSet<CPos>();
				PlanRing(target.Location, baseCenter, wallInfo, wallBuildingInfo, anchors, planned);

				if (anchors.Count == 0)
					continue;

				if (haveBaseline)
				{
					if (planned.Contains(pathStart.Value))
						continue;

					var candidate = RunFlood(pathStart.Value, planned);
					if (!BotWallGeometry.KeepsBaseOpen(baseline, candidate, Info.WallPathCheckTolerance))
					{
						AIUtils.BotDebug("{0} rejected a wall ring at {1}: it would cut the base off ({2} -> {3} cells).",
							player, target.Location, baseline.Cells, candidate.Cells);
						continue;
					}
				}

				pendingAnchors.AddRange(anchors);
				return;
			}
		}

		void MarkHandled(CPos cell)
		{
			handledTargets.Add(cell);
			if (handledTargets.Count > MaxHandledTargets)
				handledTargets.RemoveRange(0, handledTargets.Count - MaxHandledTargets);
		}

		/// <summary>
		/// Lays out up to three sides of a square ring around <paramref name="center"/>, dropping
		/// the side(s) facing the middle of our own base first. See <see cref="BotWallGeometry"/>.
		/// </summary>
		void PlanRing(CPos center, CPos baseCenter, ActorInfo wallInfo, BuildingInfo bi, List<CPos> anchors, HashSet<CPos> planned)
		{
			var radius = Info.WallRingRadius.Clamp(1, 8);
			var lineBuild = wallInfo.TraitInfoOrDefault<LineBuildInfo>();
			var maxRun = lineBuild != null ? lineBuild.Range.Clamp(1, 32) : 1;

			foreach (var side in BotWallGeometry.OrderRingSides(baseCenter - center, Info.WallRingSides))
				AddSide(BotWallGeometry.SideCells(center, radius, side), wallInfo, bi, maxRun, anchors, planned);
		}

		void AddSide(List<CPos> cells, ActorInfo wallInfo, BuildingInfo bi, int maxRun, List<CPos> anchors, HashSet<CPos> planned)
		{
			var run = new List<CPos>();
			foreach (var cell in cells)
			{
				// A cell already claimed by an earlier side (the shared corners) terminates the run;
				// the line we build still joins up with it because it is directly adjacent.
				if (planned.Contains(cell) || !world.CanPlaceBuilding(cell, wallInfo, bi, null))
				{
					FlushRun(run, wallInfo, bi, anchors, planned);
					continue;
				}

				run.Add(cell);
				if (run.Count == maxRun)
					FlushRun(run, wallInfo, bi, anchors, planned);
			}

			FlushRun(run, wallInfo, bi, anchors, planned);
		}

		void FlushRun(List<CPos> run, ActorInfo wallInfo, BuildingInfo bi, List<CPos> anchors, HashSet<CPos> planned)
		{
			if (run.Count < Info.MinimumWallLineLength)
			{
				run.Clear();
				return;
			}

			// LineBuild fills the cells between two anchors for free, but the anchors themselves are
			// ordinary building placements and still have to satisfy the buildable area adjacency rule.
			var first = -1;
			var last = -1;
			for (var i = 0; i < run.Count; i++)
			{
				if (!bi.IsCloseEnoughToBase(world, player, wallInfo, run[i]))
					continue;

				if (first < 0)
					first = i;

				last = i;
			}

			if (first < 0 || last - first + 1 < Info.MinimumWallLineLength)
			{
				run.Clear();
				return;
			}

			anchors.Add(run[first]);
			if (last != first)
				anchors.Add(run[last]);

			for (var i = first; i <= last; i++)
				planned.Add(run[i]);

			run.Clear();
		}

		CPos? FindPathCheckStart(CPos baseCenter)
		{
			foreach (var cell in world.Map.FindTilesInCircle(baseCenter, 6))
				if (!IsBlocked(cell, null))
					return cell;

			return null;
		}

		BotWallGeometry.FloodResult RunFlood(CPos start, HashSet<CPos> extraBlocked)
		{
			return BotWallGeometry.Flood(start,
				c => IsBlocked(c, extraBlocked),
				c => resourceLayer != null && resourceLayer.GetResource(c).Type != null,
				Info.WallPathCheckMaxCells.Clamp(64, 20000),
				Info.WallEscapeDistance);
		}

		bool IsBlocked(CPos cell, HashSet<CPos> extraBlocked)
		{
			if (!world.Map.Contains(cell))
				return true;

			if (extraBlocked != null && extraBlocked.Contains(cell))
				return true;

			if (locomotor != null && locomotor.MovementCostForCell(cell) == PathGraph.MovementCostForUnreachableCell)
				return true;

			return buildingInfluence != null && buildingInfluence.AnyBuildingAt(cell);
		}
	}
}
