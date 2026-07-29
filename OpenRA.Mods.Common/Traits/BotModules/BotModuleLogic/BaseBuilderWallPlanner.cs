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
	/// One idea, in full: find a defensive tower that has no wall in front of it, and put a wall in
	/// front of it.
	///
	/// The wall is a single straight line on the tower's enemy facing side, handed to
	/// <see cref="BaseBuilderQueueManager"/> as two LineBuild anchors. The engine fills every cell
	/// between the anchors for free, so only the two ends are ever paid for - which is why the planner
	/// takes the *longest* placeable run in the window rather than the first one that fits. One long
	/// wall costs the same two segments as a short one, so short ones are simply waste.
	///
	/// Two things stop the bot walling itself in:
	///  * Structural: a wall is one straight line, never a ring, so it cannot enclose anything alone.
	///  * Behavioural: before a line is accepted, a bounded flood fill from beside the construction
	///    yard has to still get EscapeDistance cells clear of it with the line treated as solid.
	///
	/// The planner never touches an RNG, so the same world state always produces the same plan. Bot
	/// modules run host-only inside Sync.RunUnsynced; where the surrounding bot code does need
	/// randomness it uses World.LocalRandom, never SharedRandom.
	///
	/// Cost: a planning pass looks at up to MaxTowersPerPass towers and runs at most one flood fill,
	/// and only happens once the previous line has been fully ordered. A pass that finds nothing puts
	/// the planner to sleep for PlanRetryDelay ticks.
	/// </summary>
	class BaseBuilderWallPlanner
	{
		/// <summary>Towers examined in a single planning pass.</summary>
		const int MaxTowersPerPass = 4;

		/// <summary>Ticks to wait after a pass that found nothing to wall.</summary>
		const int PlanRetryDelay = 500;

		/// <summary>
		/// Distance in cells the bot must still be able to get from its construction yard after a wall
		/// goes up. Deliberately short: the question is "is there still a way out of here", not "can we
		/// still reach everything", and a short answer is a cheap one.
		/// </summary>
		const int EscapeDistance = 20;

		/// <summary>
		/// Cell budget for the "can we still get out" flood fill. Reaching EscapeDistance across open
		/// ground costs about 840 cells, so this leaves better than twice the headroom for terrain that
		/// makes the route wind. Running out of budget just means the wall is not built.
		/// </summary>
		const int PathCheckMaxCells = 3000;

		/// <summary>A wall this short buys almost no free cells between its two paid-for anchors.</summary>
		const int MinLineLength = 4;

		/// <summary>Towers remembered as already dealt with, oldest dropped first.</summary>
		const int MaxHandledTowers = 64;

		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;

		// The LineBuild anchors of the wall currently being ordered. Two of them, or none.
		readonly List<CPos> pendingAnchors = new List<CPos>();

		// Towers we have already planned a wall for, or tried and failed to.
		readonly HashSet<uint> handledTowers = new HashSet<uint>();
		readonly Queue<uint> handledTowerOrder = new Queue<uint>();

		int nextPlanTick;

		Locomotor locomotor;
		BuildingInfluence buildingInfluence;
		bool worldTraitsResolved;

		public BaseBuilderWallPlanner(BaseBuilderBotModule baseBuilder, Player player)
		{
			this.baseBuilder = baseBuilder;
			this.player = player;
			world = player.World;
		}

		BaseBuilderBotModuleInfo Info { get { return baseBuilder.Info; } }

		public bool Enabled
		{
			get { return Info.MaximumWallSegments > 0 && Info.WallTypes.Count > 0 && Info.WalledDefenseTypes.Count > 0; }
		}

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

			var wallInfo = world.Map.Rules.Actors[actorType];
			var bi = wallInfo.TraitInfoOrDefault<BuildingInfo>();
			if (bi == null || wallInfo.TraitInfoOrDefault<LineBuildInfo>() == null)
				return null;

			DropStaleAnchors(wallInfo, bi);

			if (pendingAnchors.Count == 0)
			{
				PlanNext(wallInfo, bi);
				DropStaleAnchors(wallInfo, bi);
			}

			if (pendingAnchors.Count == 0)
				return null;

			return pendingAnchors[0];
		}

		void DropStaleAnchors(ActorInfo wallInfo, BuildingInfo bi)
		{
			// Anchors are planned before the wall is ordered, so the world may have moved on. Anything
			// we can no longer legally start a building on is dropped.
			while (pendingAnchors.Count > 0 && !CanAnchorAt(pendingAnchors[0], wallInfo, bi))
				pendingAnchors.RemoveAt(0);
		}

		bool CanAnchorAt(CPos cell, ActorInfo wallInfo, BuildingInfo bi)
		{
			return world.CanPlaceBuilding(cell, wallInfo, bi, null)
				&& bi.IsCloseEnoughToBase(world, player, wallInfo, cell);
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

		/// <summary>
		/// Finds the unwalled tower closest to the enemy and lays one wall line across its enemy facing
		/// side. Towers are ordered by distance then ActorID, so the choice is a pure function of the
		/// world state.
		/// </summary>
		void PlanNext(ActorInfo wallInfo, BuildingInfo wallBuildingInfo)
		{
			ResolveWorldTraits();

			if (world.WorldTick < nextPlanTick)
				return;

			var targetCell = EnemyDirectionTarget();
			var maxRun = MaxWallRun(wallInfo);
			var setback = Info.WallDistanceFromTower.Clamp(1, 8);

			var towers = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead
					&& Info.WalledDefenseTypes.Contains(a.Info.Name)
					&& !handledTowers.Contains(a.ActorID))
				.OrderBy(a => (a.Location - targetCell).LengthSquared)
				.ThenBy(a => a.ActorID)
				.Take(MaxTowersPerPass);

			foreach (var tower in towers)
			{
				// Whatever happens, don't reconsider this tower: re-running a plan that already failed,
				// every time the build queue asks for a wall, is not worth the scan.
				MarkTowerHandled(tower.ActorID);

				var facing = BotWallGeometry.DominantDirection(tower.Location, targetCell);
				var window = BotWallGeometry.LineCells(tower.Location + (facing * setback),
					BotWallGeometry.Perpendicular(facing), maxRun);

				// Already shielded - the wall we would build is the wall that is already there.
				if (window.Any(HasOwnWall))
					continue;

				var line = BotWallGeometry.LongestUsableRun(window,
					c => CanAnchorAt(c, wallInfo, wallBuildingInfo), MinLineLength);

				if (line.Count == 0)
					continue;

				if (!KeepsBaseOpen(line))
				{
					AIUtils.BotDebug("{0} rejected a wall in front of {1} at {2}: it would cut the base off.",
						player, tower.Info.Name, tower.Location);
					continue;
				}

				// Two anchors however long the line is - LineBuild fills everything between them for free.
				pendingAnchors.Add(line[0]);
				pendingAnchors.Add(line[line.Count - 1]);

				AIUtils.BotDebug("{0} is walling {1} cells in front of {2} at {3}.",
					player, line.Count, tower.Info.Name, tower.Location);
				return;
			}

			// Nothing usable this pass. Don't come back for a while - a failed pass is the expensive one.
			nextPlanTick = world.WorldTick + PlanRetryDelay;
		}

		/// <summary>
		/// The cell every wall faces: the enemy building nearest to where we are being attacked, or the
		/// defence centre itself when we cannot see one.
		/// </summary>
		CPos EnemyDirectionTarget()
		{
			var defenseCenter = baseBuilder.DefenseCenter;
			var closestEnemy = world.ActorsHavingTrait<Building>()
				.Where(a => !a.Disposed && player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy)
				.ClosestTo(world.Map.CenterOfCell(defenseCenter));

			return closestEnemy?.Location ?? defenseCenter;
		}

		/// <summary>
		/// The construction yard with the lowest ActorID, or the module's own base centre if we have no
		/// yard. Deliberately not GetRandomBaseCenter: the planner uses no randomness.
		/// </summary>
		CPos StableBaseCenter()
		{
			var yard = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead && Info.ConstructionYardTypes.Contains(a.Info.Name))
				.OrderBy(a => a.ActorID)
				.FirstOrDefault();

			return yard?.Location ?? baseBuilder.DefenseCenter;
		}

		int MaxWallRun(ActorInfo wallInfo)
		{
			var lineBuild = wallInfo.TraitInfoOrDefault<LineBuildInfo>();
			return lineBuild != null ? lineBuild.Range.Clamp(1, 32) : 1;
		}

		void MarkTowerHandled(uint actorID)
		{
			if (!handledTowers.Add(actorID))
				return;

			handledTowerOrder.Enqueue(actorID);
			while (handledTowerOrder.Count > MaxHandledTowers)
				handledTowers.Remove(handledTowerOrder.Dequeue());
		}

		bool HasOwnWall(CPos cell)
		{
			if (buildingInfluence == null)
				return false;

			foreach (var b in buildingInfluence.GetBuildingsAt(cell))
				if (b.Owner == player && Info.WallTypes.Contains(b.Info.Name))
					return true;

			return false;
		}

		// --- reachability -------------------------------------------------------------------------

		/// <summary>
		/// The cheap half of "do not seal yourself in": with the planned line treated as solid, a unit
		/// standing beside our construction yard must still be able to get EscapeDistance cells clear of
		/// it. One bounded flood fill, and only for a line that has already passed every other test.
		/// </summary>
		bool KeepsBaseOpen(List<CPos> line)
		{
			var start = FindPathCheckStart(StableBaseCenter());

			// Without a start cell there is nothing to check, so fall back to the structural guarantee
			// that one straight line cannot enclose anything on its own.
			if (start == null)
				return true;

			var planned = new HashSet<CPos>(line);
			if (planned.Contains(start.Value))
				return false;

			return BotWallGeometry.CanEscape(start.Value,
				c => planned.Contains(c) || IsBlocked(c),
				PathCheckMaxCells,
				EscapeDistance);
		}

		CPos? FindPathCheckStart(CPos around)
		{
			foreach (var cell in world.Map.FindTilesInCircle(around, 6))
				if (!IsBlocked(cell))
					return cell;

			return null;
		}

		bool IsBlocked(CPos cell)
		{
			if (!world.Map.Contains(cell))
				return true;

			if (locomotor != null && locomotor.MovementCostForCell(cell) == PathGraph.MovementCostForUnreachableCell)
				return true;

			return buildingInfluence != null && buildingInfluence.AnyBuildingAt(cell);
		}
	}
}
