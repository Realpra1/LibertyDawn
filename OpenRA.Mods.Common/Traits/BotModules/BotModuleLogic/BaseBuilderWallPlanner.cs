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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Plans straight wall lines for the bot and hands them to <see cref="BaseBuilderQueueManager"/>
	/// one LineBuild anchor at a time. It also reserves the cells behind those walls, so the bot's
	/// defensive structures end up *behind* concrete rather than having concrete retro-fitted around
	/// them later.
	///
	/// The wall is always planned first and the turret second. Two kinds of job are planned:
	///
	///  * Choke walls. A choke is a short pinch of passable ground between two blockers that sits on
	///    an otherwise open corridor. Walling it funnels attackers into a gap that a turret covers.
	///  * Shielded defence sites. A cell on the enemy facing side of the base is chosen *because* a
	///    wall ring fits around it; the ring is queued, and the cell itself is reserved for the next
	///    defensive structure the build queue produces.
	///
	/// Three independent guarantees stop the bot from sealing itself in:
	///  * Structural, rings: at most three of the four sides of a ring are ever built, and the side
	///    dropped first is always the one facing the middle of our own base.
	///  * Structural, chokes: ChokeGapCells is clamped to at least one, so some of a choke is always
	///    left open however the yaml is configured.
	///  * Behavioural: before any wall is accepted the area reachable from the construction yard is
	///    flood filled twice - once as the world is now, once with the planned wall cells treated as
	///    solid. If the second flood loses reachable area, loses access to tiberium, loses the ability
	///    to get clear of the base, or loses any of the places the first flood could reach (our other
	///    construction yards and refineries, and for a choke both mouths of its corridor), the plan is
	///    thrown away. Choke walls use a longer escape distance than rings, sized to cover the base
	///    radius the bot expands into, so it cannot wall itself out of ground it is about to claim.
	///
	/// The planner never touches an RNG, so the same world state always produces the same plan. Bot
	/// modules run host-only inside Sync.RunUnsynced; where the surrounding bot code does need
	/// randomness it uses World.LocalRandom, never SharedRandom.
	///
	/// Cost: choke detection is a bounded, cached, once-per-base-location scan - see ScanChokes.
	/// Everything else runs only when a wall line has been fully consumed, and a failed planning pass
	/// puts the planner to sleep for WallPlanRetryDelay ticks.
	/// </summary>
	class BaseBuilderWallPlanner
	{
		const int MaxHandledSites = 128;
		const int MaxPendingSlots = 32;
		const int MaxSiteCellsExamined = 4096;
		const int MaxTrackedOwnBuildings = 8;

		struct PendingSlot
		{
			public CPos Cell;

			// Number of consumed wall anchors at which this slot opens up. Walls first, turrets after.
			public int UnlockAt;
		}

		sealed class ChokePlan
		{
			public CPos Center;
			public List<CPos> Span;
			public CVec Axis;
			public int DistanceSquared;
		}

		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		readonly IResourceLayer resourceLayer;

		// Anchor cells that still need a LineBuild order, in the order they must be placed.
		readonly List<CPos> pendingAnchors = new List<CPos>();

		// Cells reserved for defensive structures, behind walls we have already queued.
		readonly List<PendingSlot> pendingSlots = new List<PendingSlot>();

		// Sites we have already tried to shield, so we don't re-plan them forever.
		readonly HashSet<CPos> handledSites = new HashSet<CPos>();
		readonly Queue<CPos> handledSiteOrder = new Queue<CPos>();

		readonly List<ChokePlan> chokes = new List<ChokePlan>();
		readonly HashSet<CPos> handledChokes = new HashSet<CPos>();

		bool chokesScanned;
		CPos chokeScanCenter;
		int chokeReconsiderTick;
		int walledChokes;

		int consumedAnchors;
		int nextPlanTick;

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

		/// <summary>A defensive structure the planner wants to site behind one of its walls.</summary>
		public bool IsShieldedDefenseType(string actorType)
		{
			return Enabled && Info.ShieldedDefenseTypes.Contains(actorType);
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
				ConsumeAnchor(0);

			return cell;
		}

		/// <summary>
		/// Consumes a cell reserved behind a wall we have already queued, or null if there is none -
		/// in which case the caller falls back to its ordinary defence placement logic.
		/// </summary>
		public CPos? TakeDefenseCell(string actorType)
		{
			if (!IsShieldedDefenseType(actorType))
				return null;

			var actorInfo = world.Map.Rules.Actors[actorType];
			var bi = actorInfo.TraitInfoOrDefault<BuildingInfo>();
			if (bi == null)
				return null;

			for (var i = 0; i < pendingSlots.Count; i++)
			{
				// The wall this slot hides behind has not been fully ordered yet.
				if (pendingSlots[i].UnlockAt > consumedAnchors)
					continue;

				var cell = pendingSlots[i].Cell;
				if (!CanAnchorAt(cell, actorInfo, bi))
				{
					pendingSlots.RemoveAt(i--);
					continue;
				}

				pendingSlots.RemoveAt(i);
				return cell;
			}

			return null;
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
				PlanNext(actorInfo, bi);
				DropStaleAnchors(actorInfo, bi);
			}

			if (pendingAnchors.Count == 0)
				return null;

			return pendingAnchors[0];
		}

		void ConsumeAnchor(int index)
		{
			pendingAnchors.RemoveAt(index);

			// Counted whether the anchor was ordered or dropped, so a slot waiting on a wall that
			// turned out to be unbuildable still opens up instead of being stranded forever.
			consumedAnchors++;
		}

		void DropStaleAnchors(ActorInfo actorInfo, BuildingInfo bi)
		{
			// Anchors are planned well before the wall finishes building, so the world may have
			// moved on. Anything we can no longer legally start a building on is dropped.
			while (pendingAnchors.Count > 0 && !CanAnchorAt(pendingAnchors[0], actorInfo, bi))
				ConsumeAnchor(0);
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

		// --- planning -----------------------------------------------------------------------------
		void PlanNext(ActorInfo wallInfo, BuildingInfo wallBuildingInfo)
		{
			ResolveWorldTraits();

			if (world.WorldTick < nextPlanTick)
				return;

			var baseCenter = StableBaseCenter();

			if (PlanChokeWall(baseCenter, wallInfo, wallBuildingInfo))
				return;

			if (PlanShieldedDefenseSite(baseCenter, wallInfo, wallBuildingInfo))
				return;

			// Nothing usable this pass. Don't come back for a while - a failed pass is the expensive one.
			nextPlanTick = world.WorldTick + Info.WallPlanRetryDelay.Clamp(1, 100000);
		}

		/// <summary>
		/// The construction yard with the lowest ActorID, or the module's own base centre if we have
		/// no yard. Deliberately not GetRandomBaseCenter: a base centre that jumps between yards would
		/// invalidate the choke cache every time it is read.
		/// </summary>
		CPos StableBaseCenter()
		{
			var yard = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead && Info.ConstructionYardTypes.Contains(a.Info.Name))
				.OrderBy(a => a.ActorID)
				.FirstOrDefault();

			return yard?.Location ?? baseBuilder.GetRandomBaseCenter();
		}

		int MaxWallRun(ActorInfo wallInfo)
		{
			var lineBuild = wallInfo.TraitInfoOrDefault<LineBuildInfo>();
			return lineBuild != null ? lineBuild.Range.Clamp(1, 32) : 1;
		}

		/// <summary>
		/// Picks a cell on the enemy facing side of the base that a wall ring fits around, queues the
		/// ring, and reserves the cell itself for the next defensive structure. This is the inversion
		/// of the original behaviour: the wall decides where the turret goes, not the other way round.
		/// </summary>
		bool PlanShieldedDefenseSite(CPos baseCenter, ActorInfo wallInfo, BuildingInfo wallBuildingInfo)
		{
			if (Info.ShieldedDefenseTypes.Count == 0)
				return false;

			var closestEnemy = world.ActorsHavingTrait<Building>()
				.Where(a => !a.Disposed && player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy)
				.ClosestTo(world.Map.CenterOfCell(baseBuilder.DefenseCenter));

			var targetCell = closestEnemy != null ? closestEnemy.Location : baseBuilder.DefenseCenter;

			var minRadius = Info.MinimumDefenseRadius.Clamp(1, 128);
			var maxRadius = Info.MaximumDefenseRadius.Clamp(minRadius, 128);
			var radius = Info.WallRingRadius.Clamp(1, 8);
			var maxRun = MaxWallRun(wallInfo);
			var attempts = Info.WallPlanAttempts.Clamp(1, 8);

			var pathStart = FindPathCheckStart(baseCenter);
			var targets = OwnBuildingWaypoints();

			for (var attempt = 0; attempt < attempts; attempt++)
			{
				var site = FindShieldSite(baseCenter, targetCell, minRadius, maxRadius, wallInfo, wallBuildingInfo);
				if (site == null)
					return false;

				// Whatever happens, don't consider this site again - re-evaluating a site that already
				// failed, every time the queue asks for a wall, is not worth the scan.
				MarkSiteHandled(site.Value);

				var anchors = new List<CPos>();
				var planned = new HashSet<CPos>();
				foreach (var side in BotWallGeometry.OrderRingSides(baseCenter - site.Value, Info.WallRingSides))
					AddLine(BotWallGeometry.SideCells(site.Value, radius, side), wallInfo, wallBuildingInfo, maxRun, anchors, planned);

				if (anchors.Count == 0)
					continue;

				if (!KeepsBaseOpen(pathStart, planned, targets, Info.WallPathCheckMaxCells, Info.WallEscapeDistance))
				{
					AIUtils.BotDebug("{0} rejected a wall ring at {1}: it would cut the base off.", player, site.Value);
					continue;
				}

				Commit(anchors, new List<CPos> { site.Value });
				return true;
			}

			return false;
		}

		/// <summary>
		/// Cheapest-first search for a wall-ringable cell in the defence annulus, closest to the enemy.
		/// The placement check only runs on cells that beat the best score so far, so the expensive part
		/// executes a handful of times rather than once per cell.
		/// </summary>
		CPos? FindShieldSite(CPos baseCenter, CPos targetCell, int minRadius, int maxRadius, ActorInfo wallInfo, BuildingInfo bi)
		{
			CPos? best = null;
			var bestScore = int.MaxValue;
			var examined = 0;

			foreach (var cell in world.Map.FindTilesInAnnulus(baseCenter, minRadius, maxRadius))
			{
				if (++examined > MaxSiteCellsExamined)
					break;

				if (handledSites.Contains(cell))
					continue;

				var score = (cell - targetCell).LengthSquared;
				if (best != null && score >= bestScore)
					continue;

				if (!CanAnchorAt(cell, wallInfo, bi))
					continue;

				best = cell;
				bestScore = score;
			}

			return best;
		}

		/// <summary>
		/// Walls the narrowest cached choke that survives the reachability check, and reserves the cells
		/// just behind it, on our side, for turrets.
		/// </summary>
		bool PlanChokeWall(CPos baseCenter, ActorInfo wallInfo, BuildingInfo wallBuildingInfo)
		{
			if (Info.MaximumWalledChokes <= 0 || walledChokes >= Info.MaximumWalledChokes)
				return false;

			EnsureChokes(baseCenter);

			var maxRun = MaxWallRun(wallInfo);
			var corridor = Info.ChokeMinCorridorLength.Clamp(1, 16);
			var attempts = Info.WallPlanAttempts.Clamp(1, 8);
			var tried = 0;

			var pathStart = FindPathCheckStart(baseCenter);

			foreach (var choke in chokes)
			{
				if (handledChokes.Contains(choke.Center))
					continue;

				if (++tried > attempts)
					break;

				handledChokes.Add(choke.Center);

				// Never the whole span: ChokeGapCells is clamped to at least one open cell.
				var wallable = BotWallGeometry.WallableChokeCells(choke.Span, Info.ChokeGapCells, baseCenter);
				if (wallable.Count < Info.MinimumWallLineLength)
					continue;

				var anchors = new List<CPos>();
				var planned = new HashSet<CPos>();
				AddLine(wallable, wallInfo, wallBuildingInfo, maxRun, anchors, planned);
				if (anchors.Count == 0)
					continue;

				var inward = InwardDirection(choke, baseCenter);
				var targets = OwnBuildingWaypoints();

				// Both mouths of the corridor have to survive the wall. This is what stops the bot
				// sealing itself in, or sealing itself away from whatever lies past the choke.
				AddWaypoint(targets, choke.Center + (inward * (corridor + 1)));
				AddWaypoint(targets, choke.Center - (inward * (corridor + 1)));

				if (!KeepsBaseOpen(pathStart, planned, targets, Info.ChokePathCheckMaxCells, Info.ChokeEscapeDistance))
				{
					AIUtils.BotDebug("{0} rejected a choke wall at {1}: it would cut the base off.", player, choke.Center);
					continue;
				}

				var slots = BotWallGeometry.SlotsBehind(wallable, inward, Info.WallTurretSetback)
					.Where(c => !planned.Contains(c))
					.Take(2)
					.ToList();

				Commit(anchors, slots);
				walledChokes++;
				AIUtils.BotDebug("{0} is walling a {1} cell choke at {2}.", player, choke.Span.Count, choke.Center);
				return true;
			}

			return false;
		}

		/// <summary>The direction along the choke's corridor that points at our own base.</summary>
		static CVec InwardDirection(ChokePlan choke, CPos baseCenter)
		{
			var perpendicular = choke.Axis == BotWallGeometry.ChokeAxes[0]
				? BotWallGeometry.ChokeAxes[1]
				: BotWallGeometry.ChokeAxes[0];

			var delta = baseCenter - choke.Center;
			var along = perpendicular.X != 0 ? delta.X : delta.Y;
			return along >= 0 ? perpendicular : -perpendicular;
		}

		void Commit(List<CPos> anchors, List<CPos> slots)
		{
			pendingAnchors.AddRange(anchors);

			// A slot only opens once every anchor of the wall in front of it has been consumed, so the
			// concrete is always ordered before the thing it protects.
			var unlockAt = consumedAnchors + pendingAnchors.Count;
			foreach (var slot in slots)
				pendingSlots.Add(new PendingSlot { Cell = slot, UnlockAt = unlockAt });

			if (pendingSlots.Count > MaxPendingSlots)
				pendingSlots.RemoveRange(0, pendingSlots.Count - MaxPendingSlots);
		}

		void MarkSiteHandled(CPos cell)
		{
			if (!handledSites.Add(cell))
				return;

			handledSiteOrder.Enqueue(cell);
			while (handledSiteOrder.Count > MaxHandledSites)
				handledSites.Remove(handledSiteOrder.Dequeue());
		}

		// --- wall lines ---------------------------------------------------------------------------

		/// <summary>
		/// Turns a run of cells into LineBuild anchor pairs, splitting wherever a cell is unusable or
		/// the LineBuild range is exhausted.
		/// </summary>
		void AddLine(List<CPos> cells, ActorInfo wallInfo, BuildingInfo bi, int maxRun, List<CPos> anchors, HashSet<CPos> planned)
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

		// --- choke detection ----------------------------------------------------------------------
		void EnsureChokes(CPos baseCenter)
		{
			var rescan = Info.ChokeRescanDistance.Clamp(1, 128);
			if (chokesScanned && (baseCenter - chokeScanCenter).LengthSquared <= rescan * rescan)
			{
				// Chokes we could not wall earlier may have become placeable as the base grew.
				if (world.WorldTick >= chokeReconsiderTick)
				{
					handledChokes.Clear();
					chokeReconsiderTick = world.WorldTick + Info.ChokeReconsiderDelay.Clamp(1, 1000000);
				}

				return;
			}

			ScanChokes(baseCenter);
		}

		/// <summary>
		/// The single expensive thing this class does, and the reason everything above it is cached.
		/// Runs once per base location: on the first choke wall the bot ever plans, and again only if
		/// the base centre moves more than ChokeRescanDistance cells (i.e. the bot relocated).
		///
		/// Bounded by construction: at most ChokeScanMaxCells cells are examined, each costing at most
		/// 2 axes * 2 directions * (ChokeMaxWidth + ChokeMinCorridorLength) terrain lookups, and the
		/// scan stops early once MaximumCachedChokes chokes have been found. With the shipped SkyNet
		/// numbers that is 1200 cells and an upper bound of roughly 48,000 array lookups, once.
		/// </summary>
		void ScanChokes(CPos baseCenter)
		{
			chokesScanned = true;
			chokeScanCenter = baseCenter;
			chokes.Clear();
			handledChokes.Clear();
			chokeReconsiderTick = world.WorldTick + Info.ChokeReconsiderDelay.Clamp(1, 1000000);

			if (locomotor == null)
				return;

			var minRadius = Info.ChokeScanMinRadius.Clamp(0, 128);
			var maxRadius = Info.ChokeScanRadius.Clamp(minRadius, 128);
			var maxCells = Info.ChokeScanMaxCells.Clamp(0, 8000);
			var maxWidth = Info.ChokeMaxWidth.Clamp(1, 16);
			var corridor = Info.ChokeMinCorridorLength.Clamp(1, 16);
			var wanted = Info.MaximumCachedChokes.Clamp(1, 32);

			// Cells belonging to a choke we already took, so the same corridor is not cached twice.
			var covered = new HashSet<CPos>();
			var examined = 0;

			foreach (var cell in world.Map.FindTilesInAnnulus(baseCenter, minRadius, maxRadius))
			{
				if (++examined > maxCells)
					break;

				if (covered.Contains(cell))
					continue;

				if (!BotWallGeometry.TryFindChoke(cell, IsTerrainBlocked, maxWidth, corridor, out var span, out var axis))
					continue;

				chokes.Add(new ChokePlan
				{
					Center = cell,
					Span = span,
					Axis = axis,
					DistanceSquared = (cell - baseCenter).LengthSquared
				});

				var perpendicular = axis == BotWallGeometry.ChokeAxes[0]
					? BotWallGeometry.ChokeAxes[1]
					: BotWallGeometry.ChokeAxes[0];

				foreach (var c in span)
					for (var k = -corridor; k <= corridor; k++)
						covered.Add(c + (perpendicular * k));

				if (chokes.Count >= wanted)
					break;
			}

			// Narrowest first, then nearest, then a coordinate tie break so the order is total and the
			// result does not depend on List.Sort being stable.
			chokes.Sort((a, b) =>
			{
				var c = a.Span.Count.CompareTo(b.Span.Count);
				if (c != 0)
					return c;

				c = a.DistanceSquared.CompareTo(b.DistanceSquared);
				if (c != 0)
					return c;

				c = a.Center.X.CompareTo(b.Center.X);
				return c != 0 ? c : a.Center.Y.CompareTo(b.Center.Y);
			});
		}

		// --- reachability -------------------------------------------------------------------------

		/// <summary>
		/// Passable cells near each of our construction yards and refineries. These are the places the
		/// bot must still be able to reach after walling; losing any of them rejects the plan.
		/// </summary>
		HashSet<CPos> OwnBuildingWaypoints()
		{
			var waypoints = new HashSet<CPos>();
			var buildings = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead
					&& (Info.ConstructionYardTypes.Contains(a.Info.Name) || Info.RefineryTypes.Contains(a.Info.Name)))
				.OrderBy(a => a.ActorID)
				.Take(MaxTrackedOwnBuildings);

			foreach (var b in buildings)
			{
				var near = FindPathCheckStart(b.Location);
				if (near != null)
					waypoints.Add(near.Value);
			}

			return waypoints;
		}

		void AddWaypoint(HashSet<CPos> waypoints, CPos cell)
		{
			if (!IsBlocked(cell, null))
				waypoints.Add(cell);
		}

		bool KeepsBaseOpen(CPos? pathStart, HashSet<CPos> planned, HashSet<CPos> targets, int maxCells, int escapeDistance)
		{
			// Without a start cell there is nothing to compare, so fall back to the structural
			// guarantees (three-sided rings, always-open choke gaps) alone.
			if (pathStart == null)
				return true;

			if (planned.Contains(pathStart.Value))
				return false;

			var baseline = RunFlood(pathStart.Value, null, targets, maxCells, escapeDistance);
			var candidate = RunFlood(pathStart.Value, planned, targets, maxCells, escapeDistance);
			return BotWallGeometry.KeepsBaseOpen(baseline, candidate, Info.WallPathCheckTolerance);
		}

		CPos? FindPathCheckStart(CPos around)
		{
			foreach (var cell in world.Map.FindTilesInCircle(around, 6))
				if (!IsBlocked(cell, null))
					return cell;

			return null;
		}

		BotWallGeometry.FloodResult RunFlood(CPos start, HashSet<CPos> extraBlocked, HashSet<CPos> targets, int maxCells, int escapeDistance)
		{
			return BotWallGeometry.Flood(start,
				c => IsBlocked(c, extraBlocked),
				c => resourceLayer != null && resourceLayer.GetResource(c).Type != null,
				maxCells.Clamp(64, 20000),
				escapeDistance,
				targets);
		}

		/// <summary>Terrain only. Chokes are a property of the map, not of what we happened to build.</summary>
		bool IsTerrainBlocked(CPos cell)
		{
			if (!world.Map.Contains(cell))
				return true;

			return locomotor != null && locomotor.MovementCostForCell(cell) == PathGraph.MovementCostForUnreachableCell;
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
