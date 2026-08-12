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

		enum PendingWallPurpose
		{
			None,
			Tower,
			Enclosure
		}

		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;

		// The LineBuild anchors of the wall currently being ordered. Two of them, or none.
		readonly List<CPos> pendingAnchors = new List<CPos>();
		readonly HashSet<CPos> pendingIndividualWallCells = new HashSet<CPos>();
		string pendingWallType;
		PendingWallPurpose pendingPurpose;

		// Towers we have already planned a wall for, or tried and failed to.
		readonly HashSet<uint> handledTowers = new HashSet<uint>();
		readonly Queue<uint> handledTowerOrder = new Queue<uint>();
		readonly HashSet<CPos> observedEnclosureWalls = new HashSet<CPos>();
		readonly Dictionary<CPos, int> issuedEnclosureCells = new Dictionary<CPos, int>();
		readonly Dictionary<uint, int> nextEnclosureQueueLogTicks = new Dictionary<uint, int>();
		readonly ConstructionYardEnclosureBuildOwnership<ProductionQueue> enclosureBuildOwnership =
			new ConstructionYardEnclosureBuildOwnership<ProductionQueue>();
		ConstructionYardEnclosurePlan enclosurePlan;
		uint enclosureYardActorId;
		string enclosureYardType;
		CPos enclosureYardLocation;
		CVec enclosureYardDimensions;
		bool enclosureBound;
		bool enclosureStopped;
		bool enclosureStopLogged;
		int nextEnclosureScanTick;
		readonly HashSet<CPos> clusterWallCells = new HashSet<CPos>();
		uint clusterScreenAnchorId;

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
			get
			{
				return Info.MaximumWallSegments > 0 &&
					(Info.WallTypes.Count > 0 || Info.ConstructionYardEnclosureWallTypes.Length > 0) &&
					(Info.WalledDefenseTypes.Count > 0 || Info.ConstructionYardEnclosureWallTypes.Length > 0);
			}
		}

		public bool IsWallType(string actorType)
		{
			return Info.WallTypes.Contains(actorType) || Info.ConstructionYardEnclosureWallTypes.Contains(actorType);
		}

		public bool OwnsClusterWallCell(CPos cell)
		{
			return clusterWallCells.Contains(cell);
		}

		public void Tick()
		{
			EnsureEnclosureState();
		}

		public bool OverlapsConstructionYardEnclosure(CPos location, BuildingInfo buildingInfo)
		{
			EnsureEnclosureState();
			return EnclosureActive && buildingInfo != null &&
				ConstructionYardEnclosurePolicy.Overlaps(enclosurePlan, buildingInfo.Tiles(location));
		}

		public bool IsConstructionYardEnclosureReserved(CPos cell)
		{
			EnsureEnclosureState();
			return EnclosureActive && enclosurePlan.WallCells.Contains(cell);
		}

		public void LogReservationDecision(string actorType, CPos reserved, CPos selected, bool overridden)
		{
			LogEnclosure(overridden ?
				"{0} tick={1} reservation override actor={2} cell={3}: no comparable legal alternative." :
				"{0} tick={1} reservation avoided actor={2} reserved={3} alternative={4}.",
				player, world.WorldTick, actorType, reserved, selected);
		}

		bool EnclosureActive => ConstructionYardEnclosurePolicy.IsActive(world.WorldTick,
			Info.ConstructionYardEnclosureCutoffTick, enclosureBound, enclosureStopped);

		public int LimitConstructionYardEnclosurePollDelay(int normalDelay)
		{
			EnsureEnclosureState();
			return ConstructionYardEnclosurePolicy.QueuePollDelay(normalDelay,
				Info.ConstructionYardEnclosureMaintenanceInterval, EnclosureActive);
		}

		public void LogConstructionYardEnclosureQueueState(ProductionQueue queue,
			ProductionItem currentBuilding, int cash, int resources)
		{
			if (!Info.ConstructionYardEnclosureDebugLogging || queue == null)
				return;

			EnsureEnclosureState();
			if (!EnclosureActive ||
				(nextEnclosureQueueLogTicks.TryGetValue(queue.Actor.ActorID, out var nextLogTick) &&
				world.WorldTick < nextLogTick))
				return;

			nextEnclosureQueueLogTicks[queue.Actor.ActorID] = NextEnclosureScanTick();
			if (currentBuilding == null)
				LogEnclosure("{0} tick={1} queue-state yard={2}@{3} queue={4}/{5} item=idle cash={6} resources={7}.",
					player, world.WorldTick, enclosureYardActorId, enclosureYardLocation,
					queue.Actor.ActorID, queue.Info.Type, cash, resources);
			else
				LogEnclosure("{0} tick={1} queue-state yard={2}@{3} queue={4}/{5} item={6} done={7} started={8} paused={9} remaining-time={10} remaining-cost={11} cash={12} resources={13}.",
					player, world.WorldTick, enclosureYardActorId, enclosureYardLocation,
					queue.Actor.ActorID, queue.Info.Type, currentBuilding.Item, currentBuilding.Done,
					currentBuilding.Started, currentBuilding.Paused, currentBuilding.RemainingTimeActual,
					currentBuilding.RemainingCost, cash, resources);
		}

		public ActorInfo ConstructionYardEnclosureWall(ProductionQueue queue,
			IEnumerable<ActorInfo> buildables, Actor[] playerBuildings)
		{
			if (!Enabled || queue == null || Info.ConstructionYardEnclosureWallTypes.Length == 0 ||
				WallCount(playerBuildings) >= Info.MaximumWallSegments)
				return null;

			RefreshEnclosureBuildOwnership();
			var available = buildables.ToDictionary(a => a.Name);
			foreach (var type in Info.ConstructionYardEnclosureWallTypes)
				if (available.TryGetValue(type, out var wall) && PeekAnchor(type) != null &&
					pendingPurpose == PendingWallPurpose.Enclosure)
				{
					if (!enclosureBuildOwnership.HasReservation &&
						!enclosureBuildOwnership.TryReserve(queue, type, world.WorldTick))
						return null;
					if (!enclosureBuildOwnership.Owns(queue, type))
						return null;

					LogEnclosure("{0} tick={1} requested wall={2} yard={3}@{4} queue={5}/{6} cash/queue accepted for production choice.",
						player, world.WorldTick, type, enclosureYardActorId, enclosureYardLocation,
						queue.Actor.ActorID, queue.Info.Type);
					return wall;
				}

			return null;
		}

		/// <summary>Gate used when deciding what to put into the production queue.</summary>
		public bool WantsToBuildWall(ProductionQueue queue, string actorType, Actor[] playerBuildings)
		{
			if (!Enabled || !IsWallType(actorType))
				return false;

			if (WallCount(playerBuildings) >= Info.MaximumWallSegments)
				return false;

			var cell = PeekAnchor(actorType);
			return cell != null && (pendingPurpose != PendingWallPurpose.Enclosure ||
				enclosureBuildOwnership.Owns(queue, actorType));
		}

		/// <summary>Consumes the next anchor. The caller issues a "LineBuild" order for it.</summary>
		public CPos? TakeWallCell(ProductionQueue queue, string actorType)
		{
			return TakeWallCell(queue, actorType, out _);
		}

		public CPos? TakeWallCell(ProductionQueue queue, string actorType, out bool useLineBuild)
		{
			useLineBuild = true;
			RefreshEnclosureBuildOwnership();
			var cell = PeekAnchor(actorType);
			if (cell != null && pendingPurpose == PendingWallPurpose.Enclosure &&
				!enclosureBuildOwnership.Owns(queue, actorType))
			{
				LogEnclosure("{0} tick={1} withheld enclosure placement wall={2} queue={3}: pending endpoint has another owner.",
					player, world.WorldTick, actorType, queue?.Actor.ActorID ?? 0);
				return null;
			}

			if (cell != null)
			{
				if (pendingPurpose == PendingWallPurpose.Enclosure)
				{
					issuedEnclosureCells[cell.Value] = world.WorldTick;
					LogEnclosure("{0} tick={1} issued LineBuild yard={2}/{3}@{4} wall={5} cell={6} repair={7}.",
						player, world.WorldTick, enclosureYardActorId, enclosureYardType, enclosureYardLocation,
						actorType, cell.Value, observedEnclosureWalls.Contains(cell.Value));
					enclosureBuildOwnership.Release();
				}

				pendingAnchors.RemoveAt(0);
				useLineBuild = !pendingIndividualWallCells.Remove(cell.Value);
				if (pendingAnchors.Count == 0)
				{
					if (pendingPurpose == PendingWallPurpose.Enclosure)
						nextEnclosureScanTick = NextEnclosureScanTick();
					ClearPendingAnchors();
				}
			}

			return cell;
		}

		CPos? PeekAnchor(string actorType)
		{
			if (!Enabled)
				return null;

			EnsureEnclosureState();
			if (!EnclosureActive && pendingPurpose == PendingWallPurpose.Enclosure)
				ClearPendingAnchors();

			if (pendingAnchors.Count > 0 && pendingWallType != actorType)
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
			{
				if (pendingPurpose == PendingWallPurpose.Enclosure && Info.ConstructionYardEnclosureDebugLogging)
					LogEnclosure("{0} tick={1} dropped stale enclosure anchor yard={2}@{3} cell={4} reason={5}.",
						player, world.WorldTick, enclosureYardActorId, enclosureYardLocation,
						pendingAnchors[0], DescribeEnclosureCell(pendingAnchors[0], wallInfo, bi));
				pendingAnchors.RemoveAt(0);
			}

			if (pendingAnchors.Count == 0)
				ClearPendingAnchors();
		}

		void ClearPendingAnchors()
		{
			pendingAnchors.Clear();
			pendingIndividualWallCells.Clear();
			pendingWallType = null;
			pendingPurpose = PendingWallPurpose.None;
			enclosureBuildOwnership.Release();
		}

		void RefreshEnclosureBuildOwnership()
		{
			if (enclosureBuildOwnership.Refresh(world.WorldTick, Info.StructureProductionActiveDelay,
				queue => queue.Actor != null && queue.Actor.Owner == player && queue.Actor.IsInWorld &&
					!queue.Actor.IsDead && queue.Enabled,
				(queue, actorType) => queue.AllQueued().Any(i => i.Item == actorType)))
				LogEnclosure("{0} tick={1} released stale enclosure queue ownership yard={2}@{3}.",
					player, world.WorldTick, enclosureYardActorId, enclosureYardLocation);
		}

		int WallCount(Actor[] playerBuildings)
		{
			var count = 0;
			for (var i = 0; i < playerBuildings.Length; i++)
				if (IsWallType(playerBuildings[i].Info.Name))
					count++;

			return count;
		}

		bool CanAnchorAt(CPos cell, ActorInfo wallInfo, BuildingInfo bi)
		{
			return world.CanPlaceBuilding(cell, wallInfo, bi, null)
				&& bi.IsCloseEnoughToBase(world, player, wallInfo, cell);
		}

		string DescribeEnclosureCell(CPos cell, ActorInfo wallInfo, BuildingInfo bi)
		{
			if (!world.Map.Contains(cell))
				return "map-edge";
			if (HasOwnWall(cell))
				return "own-wall";

			var actors = world.ActorMap.GetActorsAt(cell)
				.Where(a => a.IsInWorld && !a.IsDead)
				.OrderBy(a => a.ActorID)
				.Select(a => $"{a.Info.Name}#{a.ActorID}/{a.Owner.InternalName}")
				.ToArray();
			if (actors.Length > 0)
				return "occupied:" + string.Join(",", actors);
			if (locomotor != null &&
				locomotor.MovementCostForCell(cell) == PathGraph.MovementCostForUnreachableCell)
				return "terrain-unreachable";
			if (!world.CanPlaceBuilding(cell, wallInfo, bi, null))
				return "placement-illegal";
			if (!bi.IsCloseEnoughToBase(world, player, wallInfo, cell))
				return "outside-build-radius";

			return "legal";
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

		int NextEnclosureScanTick()
		{
			var interval = Math.Max(1, Info.ConstructionYardEnclosureMaintenanceInterval);
			return world.WorldTick > int.MaxValue - interval ? int.MaxValue : world.WorldTick + interval;
		}

		void EnsureEnclosureState()
		{
			if (Info.ConstructionYardEnclosureWallTypes.Length == 0 || enclosureStopped)
				return;

			if (world.WorldTick >= Math.Max(0, Info.ConstructionYardEnclosureCutoffTick))
			{
				StopEnclosure("cutoff");
				return;
			}

			if (!enclosureBound)
			{
				var yardId = ConstructionYardEnclosurePolicy.SelectInitialYardActorId(
					world.ActorsHavingTrait<Building>()
					.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
						Info.ConstructionYardTypes.Contains(a.Info.Name))
					.Select(a => a.ActorID), true);
				var yard = yardId.HasValue ? world.GetActorById(yardId.Value) : null;
				var building = yard?.Info.TraitInfoOrDefault<BuildingInfo>();
				if (building == null)
					return;

				enclosureBound = true;
				enclosureYardActorId = yard.ActorID;
				enclosureYardType = yard.Info.Name;
				enclosureYardLocation = yard.Location;
				enclosureYardDimensions = building.Dimensions;
				enclosurePlan = ConstructionYardEnclosurePolicy.CreatePlan(yard.Location, building.Dimensions,
					Info.ConstructionYardEnclosureMargin.Clamp(0, 8),
					Info.ConstructionYardEnclosureAccessWidth);
				nextEnclosureScanTick = world.WorldTick;
				LogEnclosure("{0} tick={1} bound first yard={2}/{3}@{4} walls={5} access={6} cutoff={7}.",
					player, world.WorldTick, enclosureYardActorId, enclosureYardType, enclosureYardLocation,
					enclosurePlan.WallCells.Length, string.Join(",", enclosurePlan.AccessCells),
					Info.ConstructionYardEnclosureCutoffTick);
			}

			var live = world.GetActorById(enclosureYardActorId);
			if (live == null || live.Owner != player || !live.IsInWorld || live.IsDead ||
				live.Info.Name != enclosureYardType || live.Location != enclosureYardLocation)
				StopEnclosure("bound yard ceased to be the original Fact");
		}

		void StopEnclosure(string reason)
		{
			enclosureStopped = true;
			if (pendingPurpose == PendingWallPurpose.Enclosure)
				ClearPendingAnchors();

			if (enclosureStopLogged)
				return;

			enclosureStopLogged = true;
			LogEnclosure("{0} tick={1} stopped yard={2}/{3}@{4} reason={5}; reservations released.",
				player, world.WorldTick, enclosureYardActorId, enclosureYardType ?? "none",
				enclosureYardLocation, reason);
		}

		// --- planning -----------------------------------------------------------------------------

		/// <summary>Preserves the first-Fact enclosure, then plans the active cluster's open screen.</summary>
		void PlanNext(ActorInfo wallInfo, BuildingInfo wallBuildingInfo)
		{
			ResolveWorldTraits();
			EnsureEnclosureState();

			if (TryPlanConstructionYardEnclosure(wallInfo, wallBuildingInfo))
				return;

			if (world.WorldTick < nextPlanTick)
				return;

			var cluster = baseBuilder.DefenseClusterManager;
			var anchor = cluster?.ActiveAnchor;
			if (cluster == null || !cluster.Enabled || !cluster.ReadyForWallScreen || anchor == null)
			{
				nextPlanTick = world.WorldTick + PlanRetryDelay;
				return;
			}

			var facing = BotWallGeometry.DominantDirection(anchor.Location, cluster.ScreenEnemyLocation);
			var variants = BotWallGeometry.OpenScreenVariants(anchor.Location, facing,
				Info.DefenseClusterWallSetback, Info.DefenseClusterWallHalfWidth,
				Info.DefenseClusterWallFlankDepth);
			if (clusterScreenAnchorId == anchor.ActorID && variants.Any(lines =>
				lines.SelectMany(line => line).Distinct().All(HasOwnWall)))
			{
				nextPlanTick = world.WorldTick + PlanRetryDelay;
				return;
			}

			string rejectionReason = null;
			CPos? rejectionCell = null;
			List<List<CPos>> acceptedLines = null;
			List<CPos> planned = null;
			var attemptedVariants = 0;
			foreach (var lines in variants)
			{
				attemptedVariants++;
				planned = lines.SelectMany(line => line).Distinct().ToList();
				if (CanUseClusterScreen(lines, planned, wallInfo, wallBuildingInfo,
					out rejectionReason, out rejectionCell))
				{
					acceptedLines = lines;
					break;
				}
			}

			if (acceptedLines == null)
			{
				nextPlanTick = world.WorldTick + PlanRetryDelay;
				if (Game.Settings.Debug.BotDebug)
					Log.Write("debug", "AI defense cluster: {0} tick={1} rejected screen anchor={2} variants={3} cells={4} reason={5} cell={6} facing={7}",
						player, world.WorldTick, anchor.ActorID, attemptedVariants, planned?.Count ?? 0,
						rejectionReason, rejectionCell?.ToString() ?? "none", facing);
				return;
			}

			pendingAnchors.Clear();
			pendingIndividualWallCells.Clear();
			if (planned.Any(HasOwnWall))
			{
				foreach (var cell in planned.Where(c => !HasOwnWall(c)))
				{
					pendingAnchors.Add(cell);
					pendingIndividualWallCells.Add(cell);
				}
			}
			else
				foreach (var placement in BotWallGeometry.OpenScreenPlacements(acceptedLines))
				{
					pendingAnchors.Add(placement.Cell);
					if (!placement.UseLineBuild)
						pendingIndividualWallCells.Add(placement.Cell);
				}

			if (pendingAnchors.Count > 0)
			{
				pendingWallType = wallInfo.Name;
				pendingPurpose = PendingWallPurpose.Tower;
				clusterScreenAnchorId = anchor.ActorID;
				clusterWallCells.UnionWith(planned);
				if (Game.Settings.Debug.BotDebug)
					Log.Write("debug", "AI defense cluster: {0} tick={1} planned open-screen anchor={2} cells={3} anchors={4} individual={5} variant={6}/{7}",
						player, world.WorldTick, anchor.ActorID, planned.Count, pendingAnchors.Count,
						pendingIndividualWallCells.Count, attemptedVariants, variants.Count);
				return;
			}

			nextPlanTick = world.WorldTick + PlanRetryDelay;
		}

		bool CanUseClusterScreen(List<List<CPos>> lines, List<CPos> planned, ActorInfo wallInfo,
			BuildingInfo wallBuildingInfo, out string rejectionReason, out CPos? rejectionCell)
		{
			rejectionReason = null;
			rejectionCell = null;
			var maxRun = MaxWallRun(wallInfo);
			foreach (var line in lines)
			{
				if (line.Count > maxRun)
				{
					rejectionReason = "line-range";
					return false;
				}

				foreach (var cell in line)
				{
					if (HasOwnWall(cell))
						continue;
					if (!world.CanPlaceBuilding(cell, wallInfo, wallBuildingInfo, null))
					{
						rejectionReason = "placement";
						rejectionCell = cell;
					return false;
				}

				if (!wallBuildingInfo.IsCloseEnoughToBase(world, player, wallInfo, cell))
					{
						rejectionReason = "adjacency";
						rejectionCell = cell;
						return false;
					}
				}
			}

			if (KeepsBaseOpen(planned))
				return true;
			rejectionReason = "access";
			return false;
		}

		bool TryPlanConstructionYardEnclosure(ActorInfo wallInfo, BuildingInfo wallBuildingInfo)
		{
			if (!Info.ConstructionYardEnclosureWallTypes.Contains(wallInfo.Name) || !EnclosureActive ||
				world.WorldTick < nextEnclosureScanTick)
				return false;

			foreach (var cell in enclosurePlan.WallCells)
				if (HasOwnWall(cell))
				{
					observedEnclosureWalls.Add(cell);
					if (issuedEnclosureCells.Remove(cell, out var issuedTick))
						LogEnclosure("{0} tick={1} confirmed wall yard={2}@{3} cell={4} latency={5}.",
							player, world.WorldTick, enclosureYardActorId, enclosureYardLocation,
							cell, world.WorldTick - issuedTick);
				}

			var wallCount = world.ActorsHavingTrait<Building>()
				.Count(a => a.Owner == player && a.IsInWorld && !a.IsDead && IsWallType(a.Info.Name));
			var remainingCapacity = Info.MaximumWallSegments - wallCount;
			if (remainingCapacity <= 0)
			{
				nextEnclosureScanTick = NextEnclosureScanTick();
				LogEnclosure("{0} tick={1} deferred yard={2}@{3}: wall cap {4}/{5}.",
					player, world.WorldTick, enclosureYardActorId, enclosureYardLocation,
					wallCount, Info.MaximumWallSegments);
				return false;
			}

			var run = ConstructionYardEnclosurePolicy.FirstLegalMissingRun(enclosurePlan,
				HasOwnWall, c => CanAnchorAt(c, wallInfo, wallBuildingInfo));
			if (run.Length == 0)
			{
				nextEnclosureScanTick = NextEnclosureScanTick();
				var missingCells = enclosurePlan.WallCells.Where(c => !HasOwnWall(c)).ToArray();
				LogEnclosure("{0} tick={1} pending yard={2}@{3}: missing={4} legal=0 access={5}.",
					player, world.WorldTick, enclosureYardActorId, enclosureYardLocation,
					missingCells.Length, string.Join(",", enclosurePlan.AccessCells));
				if (Info.ConstructionYardEnclosureDebugLogging)
					LogEnclosure("{0} tick={1} pending-cell-status yard={2}@{3}: {4}.",
						player, world.WorldTick, enclosureYardActorId, enclosureYardLocation,
						string.Join(";", missingCells.Select(c =>
							c + "=" + DescribeEnclosureCell(c, wallInfo, wallBuildingInfo))));
				return false;
			}

			run = run.Take(Math.Min(remainingCapacity, MaxWallRun(wallInfo))).ToArray();
			pendingAnchors.Add(run[0]);
			if (run.Length > 1)
				pendingAnchors.Add(run[run.Length - 1]);
			pendingWallType = wallInfo.Name;
			pendingPurpose = PendingWallPurpose.Enclosure;
			LogEnclosure("{0} tick={1} planned yard={2}/{3}@{4} wall={5} run={6}->{7} cells={8} anchors={9} repair={10}.",
				player, world.WorldTick, enclosureYardActorId, enclosureYardType, enclosureYardLocation,
				wallInfo.Name, run[0], run[run.Length - 1], run.Length, pendingAnchors.Count,
				run.Any(observedEnclosureWalls.Contains));
			return true;
		}

		void LogEnclosure(string format, params object[] args)
		{
			AIUtils.BotDebug(format, args);
			if (Info.ConstructionYardEnclosureDebugLogging)
				Log.Write("debug", "AI wall enclosure: " + format, args);
		}

		public MiniYamlNode IssueTraitData()
		{
			if (Info.ConstructionYardEnclosureWallTypes.Length == 0)
				return null;

			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", FieldSaver.FormatValue(3)),
				new MiniYamlNode("Bound", FieldSaver.FormatValue(enclosureBound)),
				new MiniYamlNode("Stopped", FieldSaver.FormatValue(enclosureStopped)),
				new MiniYamlNode("NextScanTick", FieldSaver.FormatValue(nextEnclosureScanTick))
			};
			if (enclosureBound)
			{
				nodes.Add(new MiniYamlNode("YardActorId", FieldSaver.FormatValue(enclosureYardActorId)));
				nodes.Add(new MiniYamlNode("YardType", FieldSaver.FormatValue(enclosureYardType)));
				nodes.Add(new MiniYamlNode("YardLocation", FieldSaver.FormatValue(enclosureYardLocation)));
				nodes.Add(new MiniYamlNode("YardDimensions", FieldSaver.FormatValue(enclosureYardDimensions)));
				nodes.Add(new MiniYamlNode("WallCellBits", FieldSaver.FormatValue(
					ConstructionYardEnclosurePolicy.EncodeCells(enclosurePlan.WallCells))));
				nodes.Add(new MiniYamlNode("AccessCellBits", FieldSaver.FormatValue(
					ConstructionYardEnclosurePolicy.EncodeCells(enclosurePlan.AccessCells))));

				var observed = enclosurePlan.WallCells.Where(observedEnclosureWalls.Contains).ToArray();
				nodes.Add(new MiniYamlNode("ObservedWallCellBits", FieldSaver.FormatValue(
					ConstructionYardEnclosurePolicy.EncodeCells(observed))));

				var issued = enclosurePlan.WallCells.Where(issuedEnclosureCells.ContainsKey).ToArray();
				nodes.Add(new MiniYamlNode("IssuedCellBits", FieldSaver.FormatValue(
					ConstructionYardEnclosurePolicy.EncodeCells(issued))));
				nodes.Add(new MiniYamlNode("IssuedCellTicks", FieldSaver.FormatValue(
					issued.Select(c => issuedEnclosureCells[c]).ToArray())));

				if (pendingPurpose == PendingWallPurpose.Enclosure)
				{
					nodes.Add(new MiniYamlNode("PendingWallType", FieldSaver.FormatValue(pendingWallType)));
					nodes.Add(new MiniYamlNode("PendingAnchorBits", FieldSaver.FormatValue(
						ConstructionYardEnclosurePolicy.EncodeCells(pendingAnchors))));
					if (enclosureBuildOwnership.HasReservation)
					{
						nodes.Add(new MiniYamlNode("PendingQueueActorId", FieldSaver.FormatValue(
							enclosureBuildOwnership.ReservedQueue.Actor.ActorID)));
						nodes.Add(new MiniYamlNode("PendingQueueType", FieldSaver.FormatValue(
							enclosureBuildOwnership.ReservedQueue.Info.Type)));
						nodes.Add(new MiniYamlNode("PendingQueueReservedTick", FieldSaver.FormatValue(
							enclosureBuildOwnership.ReservedTick)));
					}
				}
			}

			return new MiniYamlNode("ConstructionYardEnclosureState", new MiniYaml("", nodes));
		}

		public void ResolveTraitData(List<MiniYamlNode> data)
		{
			if (Info.ConstructionYardEnclosureWallTypes.Length == 0)
				return;

			var state = data.FirstOrDefault(n => n.Key == "ConstructionYardEnclosureState");
			if (state == null)
			{
				enclosureStopped = true;
				LogEnclosure("{0} tick={1} loaded legacy save without enclosure identity; policy disabled to prevent later-Fact selection.",
					player, world.WorldTick);
				return;
			}

			try
			{
				var nodes = state.Value.Nodes;
				var version = ReadSavedValue<int>(nodes, "Version");
				if (version != 2 && version != 3)
					throw new InvalidOperationException("unsupported version " + version);

				enclosureBound = ReadSavedValue<bool>(nodes, "Bound");
				enclosureStopped = ReadSavedValue<bool>(nodes, "Stopped");
				nextEnclosureScanTick = ReadSavedValue<int>(nodes, "NextScanTick");
				if (!enclosureBound)
					return;

				enclosureYardActorId = ReadSavedValue<uint>(nodes, "YardActorId");
				enclosureYardType = ReadSavedValue<string>(nodes, "YardType");
				enclosureYardLocation = ReadSavedValue<CPos>(nodes, "YardLocation");
				enclosureYardDimensions = ReadSavedValue<CVec>(nodes, "YardDimensions");
				var wallCells = ReadSavedCells(nodes, "WallCellBits");
				var accessCells = ReadSavedCells(nodes, "AccessCellBits");
				enclosurePlan = ConstructionYardEnclosurePolicy.CreatePlan(enclosureYardLocation,
					enclosureYardDimensions, Info.ConstructionYardEnclosureMargin.Clamp(0, 8),
					Info.ConstructionYardEnclosureAccessWidth);
				if (!ConstructionYardEnclosurePolicy.MatchesSavedPlan(enclosurePlan, wallCells, accessCells))
					throw new InvalidOperationException("saved plan does not match configured geometry");

				var observed = ReadSavedCells(nodes, "ObservedWallCellBits");
				if (!ConstructionYardEnclosurePolicy.IsValidWallCellSubset(
					enclosurePlan, observed, enclosurePlan.WallCells.Length))
					throw new InvalidOperationException("observed wall cells are not a bounded plan subset");
				observedEnclosureWalls.Clear();
				observedEnclosureWalls.UnionWith(observed);

				var issued = ReadSavedCells(nodes, "IssuedCellBits");
				var issuedTicks = ReadSavedValue<int[]>(nodes, "IssuedCellTicks");
				if (issued.Length != issuedTicks.Length ||
					issuedTicks.Any(t => !ConstructionYardEnclosurePolicy.IsValidSavedTick(t, world.WorldTick)) ||
					!ConstructionYardEnclosurePolicy.IsValidWallCellSubset(
						enclosurePlan, issued, enclosurePlan.WallCells.Length))
					throw new InvalidOperationException("issued wall cells and ticks are inconsistent");
				issuedEnclosureCells.Clear();
				for (var i = 0; i < issued.Length; i++)
					issuedEnclosureCells.Add(issued[i], issuedTicks[i]);

				var pendingTypeNode = nodes.FirstOrDefault(n => n.Key == "PendingWallType");
				var pendingBitsNode = nodes.FirstOrDefault(n => n.Key == "PendingAnchorBits");
				if ((pendingTypeNode == null) != (pendingBitsNode == null))
					throw new InvalidOperationException("pending wall type and anchors must be saved together");
				ClearPendingAnchors();
				if (pendingTypeNode != null)
				{
					var restoredType = FieldLoader.GetValue<string>("PendingWallType", pendingTypeNode.Value.Value);
					var restoredAnchors = ConstructionYardEnclosurePolicy.DecodeCells(
						FieldLoader.GetValue<int[]>("PendingAnchorBits", pendingBitsNode.Value.Value));
					if (!Info.ConstructionYardEnclosureWallTypes.Contains(restoredType) ||
						restoredAnchors.Length == 0 ||
						!ConstructionYardEnclosurePolicy.IsValidWallCellSubset(enclosurePlan, restoredAnchors, 2))
						throw new InvalidOperationException("pending enclosure anchors are invalid");

					pendingWallType = restoredType;
					pendingPurpose = PendingWallPurpose.Enclosure;
					pendingAnchors.AddRange(restoredAnchors);

					if (version == 3 && !TryRestoreEnclosureBuildOwnership(nodes, restoredType))
					{
						ClearPendingAnchors();
						LogEnclosure("{0} tick={1} discarded pending enclosure anchors yard={2}@{3}: exact queued build owner unavailable.",
							player, world.WorldTick, enclosureYardActorId, enclosureYardLocation);
					}
					else if (version == 2)
					{
						ClearPendingAnchors();
						LogEnclosure("{0} tick={1} loaded version-2 enclosure state without queue ownership; pending anchors released for safe replanning.",
							player, world.WorldTick);
					}
				}

				LogEnclosure("{0} tick={1} restored yard={2}/{3}@{4} stopped={5} next-scan={6} access={7}.",
					player, world.WorldTick, enclosureYardActorId, enclosureYardType, enclosureYardLocation,
					enclosureStopped, nextEnclosureScanTick, string.Join(",", enclosurePlan.AccessCells));
			}
			catch (Exception ex)
			{
				ClearPendingAnchors();
				observedEnclosureWalls.Clear();
				issuedEnclosureCells.Clear();
				enclosureBound = false;
				enclosurePlan = null;
				enclosureStopped = true;
				LogEnclosure("{0} tick={1} rejected invalid saved enclosure state ({2}: {3}); policy disabled.",
					player, world.WorldTick, ex.GetType().Name, ex.Message);
			}
		}

		bool TryRestoreEnclosureBuildOwnership(List<MiniYamlNode> nodes, string actorType)
		{
			var queueActorId = ReadSavedValue<uint>(nodes, "PendingQueueActorId");
			var queueType = ReadSavedValue<string>(nodes, "PendingQueueType");
			var reservedTick = ReadSavedValue<int>(nodes, "PendingQueueReservedTick");
			if (!ConstructionYardEnclosurePolicy.IsValidSavedTick(reservedTick, world.WorldTick))
				return false;

			var queueActor = world.GetActorById(queueActorId);
			if (queueActor == null || queueActor.Owner != player || !queueActor.IsInWorld || queueActor.IsDead)
				return false;

			var queues = queueActor.TraitsImplementing<ProductionQueue>()
				.Where(q => string.Equals(q.Info.Type, queueType, StringComparison.Ordinal)).Take(2).ToArray();
			var restored = queues.Length == 1 && enclosureBuildOwnership.TryRestore(queues[0], actorType, reservedTick,
				queue => queue.Enabled && queue.Actor.Owner == player && queue.Actor.IsInWorld && !queue.Actor.IsDead,
				(queue, type) => queue.AllQueued().Any(i => i.Item == type));
			if (restored)
				LogEnclosure("{0} tick={1} restored enclosure queue owner yard={2}@{3} wall={4} queue={5}/{6} reserved-tick={7}.",
					player, world.WorldTick, enclosureYardActorId, enclosureYardLocation, actorType,
					queueActorId, queueType, reservedTick);
			return restored;
		}

		static T ReadSavedValue<T>(List<MiniYamlNode> nodes, string key)
		{
			var node = nodes.FirstOrDefault(n => n.Key == key);
			if (node == null)
				throw new InvalidOperationException("missing " + key);

			return FieldLoader.GetValue<T>(key, node.Value.Value);
		}

		static CPos[] ReadSavedCells(List<MiniYamlNode> nodes, string key)
		{
			return ConstructionYardEnclosurePolicy.DecodeCells(ReadSavedValue<int[]>(nodes, key));
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
				if (b.Owner == player && IsWallType(b.Info.Name))
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
