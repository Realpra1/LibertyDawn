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
	/// Preserves the first construction-yard enclosure and replaces the old arbitrary tower line with
	/// one bounded, access-checked open screen around the active defense cluster. Planning is local,
	/// deterministic, and retried only after a fixed delay.
	/// </summary>
	class BaseBuilderWallPlanner
	{
		/// <summary>Ticks to wait after a pass that found nothing to wall.</summary>
		const int PlanRetryDelay = 500;

		/// <summary>
		/// Distance in cells the bot must still be able to get from its construction yard after a wall
		/// goes up. Deliberately short: the question is "is there still a way out of here", not "can we
		/// still reach everything", and a short answer is a cheap one.
		/// </summary>
		const int EscapeDistance = 20;

		const int MaxEnclosureAttempts = 8;

		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;

		// Wall anchors currently being ordered. Cluster screens mix individual rear anchors with
		// LineBuild front anchors so the inward flank ends can never auto-connect to each other.
		readonly List<CPos> pendingAnchors = new List<CPos>();
		readonly HashSet<CPos> pendingIndividualWallCells = new HashSet<CPos>();
		string pendingWallType;

		readonly HashSet<uint> handledEnclosureYards = new HashSet<uint>();
		readonly Dictionary<uint, int> enclosureAttempts = new Dictionary<uint, int>();
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

		public ActorInfo ConstructionYardEnclosureWall(IEnumerable<ActorInfo> buildables, Actor[] playerBuildings)
		{
			if (!Enabled || Info.ConstructionYardEnclosureWallTypes.Length == 0 || WallCount(playerBuildings) >= Info.MaximumWallSegments)
				return null;

			var available = buildables.ToDictionary(a => a.Name);
			foreach (var type in Info.ConstructionYardEnclosureWallTypes)
				if (available.TryGetValue(type, out var wall) && PeekAnchor(type) != null)
					return wall;

			return null;
		}

		/// <summary>Gate used when deciding what to put into the production queue.</summary>
		public bool WantsToBuildWall(string actorType, Actor[] playerBuildings)
		{
			if (!Enabled || !IsWallType(actorType))
				return false;

			if (WallCount(playerBuildings) >= Info.MaximumWallSegments)
				return false;

			return PeekAnchor(actorType) != null;
		}

		/// <summary>Consumes the next anchor and reports whether the caller should use LineBuild.</summary>
		public CPos? TakeWallCell(string actorType, out bool useLineBuild)
		{
			useLineBuild = true;
			var cell = PeekAnchor(actorType);
			if (cell != null)
			{
				pendingAnchors.RemoveAt(0);
				useLineBuild = !pendingIndividualWallCells.Remove(cell.Value);
				if (pendingAnchors.Count == 0)
				{
					pendingWallType = null;
					pendingIndividualWallCells.Clear();
				}
			}

			return cell;
		}

		CPos? PeekAnchor(string actorType)
		{
			if (!Enabled)
				return null;

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
				pendingIndividualWallCells.Remove(pendingAnchors[0]);
				pendingAnchors.RemoveAt(0);
			}

			if (pendingAnchors.Count == 0)
			{
				pendingWallType = null;
				pendingIndividualWallCells.Clear();
			}
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

		/// <summary>Preserves the first-Fact enclosure, then plans only the active cluster's open screen.</summary>
		void PlanNext(ActorInfo wallInfo, BuildingInfo wallBuildingInfo)
		{
			ResolveWorldTraits();

			if (world.WorldTick < nextPlanTick)
				return;

			if (TryPlanConstructionYardEnclosure(wallInfo, wallBuildingInfo))
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
					Log.Write("debug", "AI defense cluster: {0} tick={1} rejected screen anchor={2} variants={3} " +
						"cells={4} reason={5} cell={6} facing={7}", player, world.WorldTick, anchor.ActorID,
						attemptedVariants, planned?.Count ?? 0, rejectionReason,
						rejectionCell?.ToString() ?? "none", facing);
				return;
			}

			pendingAnchors.Clear();
			pendingIndividualWallCells.Clear();
			if (planned.Any(HasOwnWall))
			{
				// A partial/reloaded screen has arbitrary existing connectors. Place only the missing
				// intended cells individually so LineBuild cannot bridge across the inward opening.
				foreach (var cell in planned.Where(c => !HasOwnWall(c)))
				{
					pendingAnchors.Add(cell);
					pendingIndividualWallCells.Add(cell);
				}
			}
			else
			{
				foreach (var placement in BotWallGeometry.OpenScreenPlacements(acceptedLines))
				{
					pendingAnchors.Add(placement.Cell);
					if (!placement.UseLineBuild)
						pendingIndividualWallCells.Add(placement.Cell);
				}
			}

			if (pendingAnchors.Count > 0)
			{
				pendingWallType = wallInfo.Name;
				clusterScreenAnchorId = anchor.ActorID;
				clusterWallCells.UnionWith(planned);
				if (Game.Settings.Debug.BotDebug)
					Log.Write("debug", "AI defense cluster: {0} tick={1} planned open-screen anchor={2} cells={3} " +
						"anchors={4} individual={5} variant={6}/{7}", player, world.WorldTick, anchor.ActorID,
						planned.Count, pendingAnchors.Count, pendingIndividualWallCells.Count,
						attemptedVariants, variants.Count);
				return;
			}

			// Nothing usable this pass. Don't come back for a while - a failed pass is the expensive one.
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
			if (!Info.ConstructionYardEnclosureWallTypes.Contains(wallInfo.Name))
				return false;

			var yard = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
					Info.ConstructionYardTypes.Contains(a.Info.Name) && !handledEnclosureYards.Contains(a.ActorID))
				.OrderBy(a => a.ActorID).FirstOrDefault();
			if (yard == null)
				return false;

			var yardBuilding = yard.Info.TraitInfoOrDefault<BuildingInfo>();
			if (yardBuilding == null)
			{
				handledEnclosureYards.Add(yard.ActorID);
				return false;
			}

			var margin = Info.ConstructionYardEnclosureMargin.Clamp(0, 8);
			var corners = BotWallGeometry.EnclosureCorners(yard.Location, yardBuilding.Dimensions, margin);
			var perimeter = BotWallGeometry.EnclosurePerimeter(yard.Location, yardBuilding.Dimensions, margin);
			if (perimeter.All(HasOwnWall))
			{
				handledEnclosureYards.Add(yard.ActorID);
				LogEnclosure("{0} completed wall enclosure around {1} at {2}.", player, yard.Info.Name, yard.Location);
				return false;
			}

			var missingWallCells = perimeter.Count(c => !HasOwnWall(c));
			var wallCount = world.ActorsHavingTrait<Building>()
				.Count(a => a.Owner == player && a.IsInWorld && !a.IsDead && IsWallType(a.Info.Name));
			if (wallCount + missingWallCells > Info.MaximumWallSegments)
			{
				handledEnclosureYards.Add(yard.ActorID);
				LogEnclosure("{0} skipped enclosing {1} at {2}: {3} existing plus {4} required walls exceeds cap {5}.",
					player, yard.Info.Name, yard.Location, wallCount, missingWallCells, Info.MaximumWallSegments);
				return false;
			}

			var attempts = enclosureAttempts.TryGetValue(yard.ActorID, out var previous) ? previous + 1 : 1;
			enclosureAttempts[yard.ActorID] = attempts;
			if (attempts > MaxEnclosureAttempts)
			{
				handledEnclosureYards.Add(yard.ActorID);
				LogEnclosure("{0} gave up enclosing {1} at {2} after {3} attempts.",
					player, yard.Info.Name, yard.Location, MaxEnclosureAttempts);
				return false;
			}

			var lineRange = MaxWallRun(wallInfo);
			if (corners[1].X - corners[0].X + 1 > lineRange || corners[3].Y - corners[0].Y + 1 > lineRange ||
				perimeter.Any(c => !HasOwnWall(c) && !CanAnchorAt(c, wallInfo, wallBuildingInfo)))
			{
				nextPlanTick = world.WorldTick + PlanRetryDelay;
				LogEnclosure("{0} cannot yet enclose {1} at {2} with {3} (attempt {4}/{5}).",
					player, yard.Info.Name, yard.Location, wallInfo.Name, attempts, MaxEnclosureAttempts);
				return false;
			}

			foreach (var corner in corners)
				if (!HasOwnWall(corner))
					pendingAnchors.Add(corner);

			if (pendingAnchors.Count == 0)
			{
				nextPlanTick = world.WorldTick + PlanRetryDelay;
				return false;
			}

			pendingWallType = wallInfo.Name;
			LogEnclosure("{0} planned {1}-cell {2} enclosure around {3} at {4} using {5} anchors.",
				player, perimeter.Count, wallInfo.Name, yard.Info.Name, yard.Location, pendingAnchors.Count);
			return true;
		}

		void LogEnclosure(string format, params object[] args)
		{
			AIUtils.BotDebug(format, args);
			if (Info.ConstructionYardEnclosureDebugLogging)
				Log.Write("debug", "AI wall enclosure: " + format, args);
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

			// A multi-sided screen has no safe structural fallback when a valid path start cannot be found.
			if (start == null)
				return false;

			var planned = new HashSet<CPos>(line);
			if (planned.Contains(start.Value))
				return false;

			return BotWallGeometry.CanEscape(start.Value,
				c => planned.Contains(c) || IsBlocked(c),
				Info.DefenseClusterPathCheckMaximumCells,
				EscapeDistance);
		}

		CPos? FindPathCheckStart(CPos around)
		{
			foreach (var cell in world.Map.FindTilesInCircle(around, 6)
				.OrderByDescending(c => (c - around).LengthSquared).ThenBy(c => c.X).ThenBy(c => c.Y))
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

		public MiniYamlNode IssueTraitData()
		{
			if (!Info.EnableDefenseClusterPolicy)
				return null;

			return new MiniYamlNode("DefenseClusterWallState", new MiniYaml("", new List<MiniYamlNode>
			{
				new MiniYamlNode("PendingWallType", FieldSaver.FormatValue(pendingWallType ?? "")),
				new MiniYamlNode("PendingAnchors", FieldSaver.FormatValue(pendingAnchors.ToArray())),
				new MiniYamlNode("PendingIndividualWallCells", FieldSaver.FormatValue(pendingIndividualWallCells
					.OrderBy(c => c.X).ThenBy(c => c.Y).ToArray())),
				new MiniYamlNode("ClusterScreenAnchorId", FieldSaver.FormatValue(clusterScreenAnchorId)),
				new MiniYamlNode("ClusterWallCells", FieldSaver.FormatValue(clusterWallCells
					.OrderBy(c => c.X).ThenBy(c => c.Y).ToArray())),
				new MiniYamlNode("NextPlanTick", FieldSaver.FormatValue(nextPlanTick))
			}));
		}

		public void ResolveTraitData(List<MiniYamlNode> data)
		{
			if (!Info.EnableDefenseClusterPolicy)
				return;

			var state = data.FirstOrDefault(n => n.Key == "DefenseClusterWallState");
			if (state == null)
				return;

			try
			{
				var nodes = state.Value.Nodes;
				pendingWallType = Read(nodes, "PendingWallType", "");
				pendingAnchors.Clear();
				pendingAnchors.AddRange(Read(nodes, "PendingAnchors", System.Array.Empty<CPos>()));
				pendingIndividualWallCells.Clear();
				pendingIndividualWallCells.UnionWith(Read(nodes, "PendingIndividualWallCells",
					System.Array.Empty<CPos>()));
				clusterScreenAnchorId = Read<uint>(nodes, "ClusterScreenAnchorId", 0);
				clusterWallCells.Clear();
				clusterWallCells.UnionWith(Read(nodes, "ClusterWallCells", System.Array.Empty<CPos>()));
				nextPlanTick = Read<int>(nodes, "NextPlanTick", world.WorldTick);
			}
			catch (System.Exception ex)
			{
				pendingWallType = null;
				pendingAnchors.Clear();
				pendingIndividualWallCells.Clear();
				clusterScreenAnchorId = 0;
				clusterWallCells.Clear();
				if (Game.Settings.Debug.BotDebug)
					Log.Write("debug", "AI defense cluster: {0} tick={1} invalid wall save type={2} message={3}; reconstructing",
						player, world.WorldTick, ex.GetType().Name, ex.Message);
			}
		}

		static T Read<T>(List<MiniYamlNode> nodes, string key, T fallback)
		{
			var node = nodes.FirstOrDefault(n => n.Key == key);
			return node == null ? fallback : FieldLoader.GetValue<T>(key, node.Value.Value);
		}
	}
}
