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

namespace OpenRA.Mods.Common.Traits
{
	class BaseBuilderTiberiumFieldManager
	{
		enum ProjectPhase
		{
			Planned,
			PlanningEnclosure,
			Reserved,
			Producing,
			AwaitingEnclosure,
			AwaitingActor,
			PlanningExtension,
			AwaitingRouteProof
		}

		sealed class FieldProject
		{
			public uint TreeActorId;
			public string TreeType;
			public CPos TreeLocation;
			public string ResonatorType;
			public CPos ResonatorLocation;
			public uint QueueActorId;
			public ProjectPhase Phase;
			public int DeadlineTick;
			public int RetryCount;
			public int DeferredUntilTick;
			public int PlannedTick;
			public int LastQueueOfferTick;
			public int NextWaitingLogTick;
			public int NextProgressCheckTick;
			public int NoProgressDeferralCount;
			public CPos[] RedWallCells;
			public CPos[] RedGateCells;
			public CPos[][] RedWallSegments;
			public int RedSegmentIndex;
			public int RedAnchorIndex;
			public CPos? RedTargetCell;
			public string ActiveActorType;
			public bool MaintenanceOnly;
			public CPos? ExtensionTargetCell;
			public int ExtensionCount;
			public int ExtensionProgressCells;
			public uint RouteHarvesterActorId;
			public uint RouteRefineryActorId;
			public CPos RouteResourceCell;
			public TiberiumFieldRoundTripStage RouteStage;
			public int RouteLastContents;
			public bool OrdinaryRouteProven;
			public bool StealthRouteProven;
		}

		sealed class StealthRouteObservation
		{
			public CPos PreviousCell;
			public bool SawGate;
			public TiberiumFieldRouteZone ApproachZone;
		}

		sealed class ActiveRedEnclosure
		{
			public uint TreeActorId;
			public string TreeType;
			public CPos TreeLocation;
			public uint ResonatorActorId;
			public string ResonatorType;
			public CPos ResonatorLocation;
			public CPos[] WallCells;
			public CPos[] GateCells;
			public CPos[][] WallSegments;
			public int NextMaintenanceTick;
		}

		sealed class HarvesterAccessObservation
		{
			public int LastContents;
			public uint TreeActorId;
			public int LoadedAmount;
		}

		readonly struct GateRouteSearchSummary
		{
			public readonly int InsideResources;
			public readonly int EligibleHarvesters;
			public readonly int LinkedRefineries;
			public readonly int HarvestableCandidates;
			public readonly int OutboundPaths;
			public readonly int InboundPaths;

			public GateRouteSearchSummary(int insideResources, int eligibleHarvesters,
				int linkedRefineries, int harvestableCandidates, int outboundPaths, int inboundPaths)
			{
				InsideResources = insideResources;
				EligibleHarvesters = eligibleHarvesters;
				LinkedRefineries = linkedRefineries;
				HarvestableCandidates = harvestableCandidates;
				OutboundPaths = outboundPaths;
				InboundPaths = inboundPaths;
			}
		}

		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		readonly PlayerResources playerResources;
		readonly PowerManager playerPower;
		readonly IResourceLayer resourceLayer;
		readonly bool enabled;
		readonly Dictionary<uint, uint> assignedResonators = new Dictionary<uint, uint>();
		readonly HashSet<uint> knownTrees = new HashSet<uint>();
		readonly HashSet<uint> loggedDeferredRedTrees = new HashSet<uint>();
		readonly Dictionary<uint, HarvesterAccessObservation> harvesterAccess =
			new Dictionary<uint, HarvesterAccessObservation>();
		readonly HashSet<uint> provenHarvestTrees = new HashSet<uint>();
		readonly Dictionary<uint, ActiveRedEnclosure> activeRedEnclosures =
			new Dictionary<uint, ActiveRedEnclosure>();
		readonly Dictionary<uint, StealthRouteObservation> stealthRouteObservations =
			new Dictionary<uint, StealthRouteObservation>();
		readonly IBotUnitReservations[] unitReservations;
		readonly IRedTiberiumBombMission[] redBombMissions;

		FieldProject project;
		int nextScanTick;
		int nextAdmissionLogTick;
		int nextWallAdmissionLogTick;
		int nextRouteDiagnosticTick;
		bool initialTreeScanComplete;
		bool unbuildableLogged;
		TiberiumFieldAdmissionResult? lastAdmissionResult;
		TiberiumFieldAdmissionResult? lastWallAdmissionResult;

		public bool Enabled => enabled;

		public BaseBuilderTiberiumFieldManager(BaseBuilderBotModule baseBuilder, Player player,
			PlayerResources playerResources, PowerManager playerPower, IResourceLayer resourceLayer)
		{
			this.baseBuilder = baseBuilder;
			world = player.World;
			this.player = player;
			this.playerResources = playerResources;
			this.playerPower = playerPower;
			this.resourceLayer = resourceLayer;
			unitReservations = player.PlayerActor.TraitsImplementing<IBotUnitReservations>().ToArray();
			redBombMissions = player.PlayerActor.TraitsImplementing<IRedTiberiumBombMission>().ToArray();
			enabled = baseBuilder.Info.EnableTiberiumFieldPolicy &&
				!baseBuilder.Info.TiberiumFieldExcludedBotTypes.Contains(player.BotType) &&
				baseBuilder.Info.TiberiumFieldTreeTypes.Count > 0 &&
				baseBuilder.Info.TiberiumFieldResonatorTypes.Length > 0;
		}

		public bool OwnsActorType(string type)
		{
			return enabled && baseBuilder.Info.TiberiumFieldResonatorTypes.Contains(type);
		}

		public MiniYamlNode IssueTraitData()
		{
			if (!enabled)
				return null;

			var nodes = new List<MiniYamlNode>
			{
				SaveValue("NextScanTick", nextScanTick)
			};
			if (project != null)
				nodes.Add(SaveProject(project));
			foreach (var enclosure in activeRedEnclosures.Values.OrderBy(e => e.TreeActorId))
				nodes.Add(SaveActiveEnclosure(enclosure));

			return new MiniYamlNode("TiberiumFieldState", new MiniYaml("", nodes));
		}

		public void ResolveTraitData(List<MiniYamlNode> data)
		{
			if (!enabled)
				return;

			var state = data.FirstOrDefault(n => n.Key == "TiberiumFieldState");
			if (state == null)
				return;

			project = null;
			activeRedEnclosures.Clear();
			try
			{
				nextScanTick = ReadValue(state.Value.Nodes, "NextScanTick", world.WorldTick);
				var projectNode = state.Value.Nodes.FirstOrDefault(n => n.Key == "Project");
				if (projectNode != null)
					project = LoadProject(projectNode);
				foreach (var enclosureNode in state.Value.Nodes.Where(n => n.Key == "ActiveRedEnclosure"))
				{
					var enclosure = LoadActiveEnclosure(enclosureNode);
					activeRedEnclosures.Add(enclosure.TreeActorId, enclosure);
				}

				Log("{0} tick={1} load-restored project={2} active-enclosures={3} next-scan={4}",
					player, world.WorldTick, project?.TreeActorId ?? 0,
					activeRedEnclosures.Count, nextScanTick);
			}
			catch (Exception ex)
			{
				project = null;
				activeRedEnclosures.Clear();
				Log("{0} tick={1} load-invalid field-state type={2} message={3}; " +
					"discarding persisted intent for deterministic world reconstruction",
					player, world.WorldTick, ex.GetType().Name, ex.Message);
			}
		}

		public void Tick()
		{
			if (!enabled)
				return;

			ObserveGateTraffic();
			if (world.WorldTick < nextScanTick)
				return;

			nextScanTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
				baseBuilder.Info.TiberiumFieldScanInterval);
			var trees = LiveTrees();
			LogTreeTransitions(trees);
			RefreshAssignments(trees);
			ObserveHarvesterAccess(trees);
			RefreshActiveRedEnclosures(trees);
			RefreshProjectState(trees);
			if (project == null)
				PlanNextProject(trees);
			else
				LogPlannedWait();
		}

		public ActorInfo TryChooseBuilding(ProductionQueue queue, IEnumerable<ActorInfo> buildables)
		{
			if (!enabled || project == null || queue == null)
				return null;

			if (project.Phase == ProjectPhase.PlanningEnclosure)
			{
				project.LastQueueOfferTick = world.WorldTick;
				return TryChooseEnclosureWall(queue, buildables);
			}

			if (project.Phase == ProjectPhase.PlanningExtension)
				return TryChooseExtensionPower(queue, buildables);
			project.LastQueueOfferTick = world.WorldTick;

			if (project.Phase != ProjectPhase.Planned || world.WorldTick < project.DeferredUntilTick ||
				queue.AllQueued().Any())
				return null;

			var configuredResonator = world.Map.Rules.Actors[project.ResonatorType];
			var buildableInfo = configuredResonator.TraitInfo<BuildableInfo>();
			if (!buildableInfo.Queue.Contains(queue.Info.Type))
				return null;

			var resonator = buildables.FirstOrDefault(a => a.Name == project.ResonatorType);
			if (resonator == null)
			{
				if (!unbuildableLogged)
				{
					unbuildableLogged = true;
					Log("{0} tick={1} buildability-deferred tree={2}/{3}@{4} resonator={5}@{6} " +
						"queue={7}/{8} reason=Unbuildable prerequisites={9}", player, world.WorldTick,
						project.TreeActorId, project.TreeType, project.TreeLocation,
						project.ResonatorType, project.ResonatorLocation, queue.Actor.ActorID,
						queue.Info.Type, string.Join(",", buildableInfo.Prerequisites));
				}

				return null;
			}

			if (unbuildableLogged)
			{
				unbuildableLogged = false;
				Log("{0} tick={1} buildability-resumed tree={2}/{3}@{4} resonator={5}@{6} queue={7}/{8}",
					player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
					project.ResonatorType, project.ResonatorLocation, queue.Actor.ActorID, queue.Info.Type);
			}

			var cost = queue.GetProductionCost(resonator);
			var spendableCash = Math.Max(0, playerResources.Cash + playerResources.Resources);
			var power = resonator.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(p => p.Amount);
			var hasPowerMargin = playerPower == null ||
				playerPower.ExcessPower + power >= baseBuilder.Info.MinimumExcessPower;
			var hasHarvesterRoute = baseBuilder.CountActors(baseBuilder.SmartEconomyHarvesterTypes) > 0 &&
				CountUsefulResourceCells(project.TreeLocation) > 0;
			var isRedTree = baseBuilder.Info.TiberiumFieldRedTreeTypes.Contains(project.TreeType);
			var redContainmentReady = !isRedTree || (RedEnclosureComplete() && project.OrdinaryRouteProven);
			if (isRedTree)
				hasHarvesterRoute = project.OrdinaryRouteProven;
			var criticalRecovery = baseBuilder.SmartEconomySerializesMissingRefinery ||
				baseBuilder.SmartEconomyShouldReserveCashForRefinery || baseBuilder.SmartEconomyWantsSilo;
			var admission = TiberiumFieldPolicy.EvaluateAdmission(enabled,
				redContainmentReady,
				baseBuilder.OpeningActive, criticalRecovery,
				baseBuilder.CountActors(baseBuilder.SmartEconomyRefineryTypes) > 0,
				playerResources.ResourceCapacity > 0, hasHarvesterRoute, hasPowerMargin,
				spendableCash, baseBuilder.Info.TiberiumFieldProtectedCash, cost);
			if (admission != TiberiumFieldAdmissionResult.Admitted)
			{
				if (lastAdmissionResult != admission || world.WorldTick >= nextAdmissionLogTick)
				{
					lastAdmissionResult = admission;
					nextAdmissionLogTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
						baseBuilder.Info.TiberiumFieldProgressLogInterval);
					Log("{0} tick={1} admission-deferred tree={2}/{3}@{4} resonator={5}@{6} " +
						"reason={7} cash={8} protected={9} power={10} storage={11} opening={12} recovery={13}",
						player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
						resonator.Name, project.ResonatorLocation, admission, spendableCash,
						baseBuilder.Info.TiberiumFieldProtectedCash,
						playerPower?.ExcessPower ?? int.MaxValue, playerResources.ResourceCapacity,
						baseBuilder.OpeningActive, criticalRecovery);
				}

				return null;
			}

			lastAdmissionResult = TiberiumFieldAdmissionResult.Admitted;
			project.ActiveActorType = resonator.Name;
			project.QueueActorId = queue.Actor.ActorID;
			project.Phase = ProjectPhase.Reserved;
			project.DeadlineTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
				baseBuilder.Info.TiberiumFieldReservationTimeout);
			Log("{0} tick={1} reserved tree={2}/{3}@{4} phase={5} resonator={6}@{7} " +
				"queue={8}/{9} expiry={10} cash={11} protected={12} power={13} route=resource-backed",
				player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
				project.Phase, resonator.Name, project.ResonatorLocation, queue.Actor.ActorID,
				queue.Info.Type, project.DeadlineTick, spendableCash,
				baseBuilder.Info.TiberiumFieldProtectedCash,
				playerPower?.ExcessPower ?? int.MaxValue);
			return resonator;
		}

		ActorInfo TryChooseExtensionPower(ProductionQueue queue, IEnumerable<ActorInfo> buildables)
		{
			if (world.WorldTick < project.DeferredUntilTick || queue.AllQueued().Any())
				return null;
			var queueSupportsPower = baseBuilder.Info.TiberiumFieldPowerTypes
				.Where(world.Map.Rules.Actors.ContainsKey)
				.Select(t => world.Map.Rules.Actors[t].TraitInfoOrDefault<BuildableInfo>())
				.Any(b => b != null && b.Queue.Contains(queue.Info.Type));
			if (!queueSupportsPower)
				return null;
			project.LastQueueOfferTick = world.WorldTick;

			var available = buildables.ToDictionary(a => a.Name);
			var hasBuildablePower = false;
			foreach (var type in baseBuilder.Info.TiberiumFieldPowerTypes)
			{
				if (!available.TryGetValue(type, out var power) ||
					!power.TraitInfo<BuildableInfo>().Queue.Contains(queue.Info.Type))
					continue;
				hasBuildablePower = true;

				var target = ChooseExtensionCell(power);
				if (!target.HasValue)
					continue;

				var spendableCash = Math.Max(0, playerResources.Cash + playerResources.Resources);
				var criticalRecovery = baseBuilder.SmartEconomySerializesMissingRefinery ||
					baseBuilder.SmartEconomyShouldReserveCashForRefinery || baseBuilder.SmartEconomyWantsSilo;
				var admission = TiberiumFieldPolicy.EvaluateAdmission(enabled, true,
					baseBuilder.OpeningActive, criticalRecovery,
					baseBuilder.CountActors(baseBuilder.SmartEconomyRefineryTypes) > 0,
					playerResources.ResourceCapacity > 0,
					baseBuilder.CountActors(baseBuilder.SmartEconomyHarvesterTypes) > 0 &&
						CountUsefulResourceCells(project.TreeLocation) > 0,
					playerPower == null || playerPower.ExcessPower >= baseBuilder.Info.MinimumExcessPower,
					spendableCash, baseBuilder.Info.TiberiumFieldProtectedCash,
					queue.GetProductionCost(power));
				if (admission != TiberiumFieldAdmissionResult.Admitted)
				{
					if (lastAdmissionResult != admission || world.WorldTick >= nextAdmissionLogTick)
					{
						lastAdmissionResult = admission;
						nextAdmissionLogTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
							baseBuilder.Info.TiberiumFieldProgressLogInterval);
						Log("{0} tick={1} extension-admission-deferred tree={2}/{3}@{4} " +
							"power={5}@{6} reason={7} cash={8} protected={9} recovery={10}",
							player, world.WorldTick, project.TreeActorId, project.TreeType,
							project.TreeLocation, power.Name, target.Value.Cell, admission,
							spendableCash, baseBuilder.Info.TiberiumFieldProtectedCash, criticalRecovery);
					}

					return null;
				}

				lastAdmissionResult = TiberiumFieldAdmissionResult.Admitted;
				project.ActiveActorType = power.Name;
				project.ExtensionTargetCell = target.Value.Cell;
				project.ExtensionProgressCells = target.Value.ProgressCells;
				project.QueueActorId = queue.Actor.ActorID;
				project.Phase = ProjectPhase.Reserved;
				project.DeadlineTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
					baseBuilder.Info.TiberiumFieldReservationTimeout);
				Log("{0} tick={1} extension-reserved tree={2}/{3}@{4} step={5} power={6}@{7} " +
					"progress={8} queue={9}/{10} expiry={11} cash={12} protected={13}", player,
					world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
					project.ExtensionCount + 1, power.Name, project.ExtensionTargetCell,
					project.ExtensionProgressCells, queue.Actor.ActorID, queue.Info.Type,
					project.DeadlineTick, spendableCash, baseBuilder.Info.TiberiumFieldProtectedCash);
				return power;
			}

			if (TiberiumFieldPolicy.ShouldDeferNoProgress(world.WorldTick,
				project.NextProgressCheckTick, false, false))
			{
				project.NoProgressDeferralCount++;
				project.DeferredUntilTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
					baseBuilder.Info.TiberiumFieldRetryDelay);
				project.NextProgressCheckTick = project.DeferredUntilTick;
				Log("{0} tick={1} extension-no-progress-deferred tree={2}/{3}@{4} " +
					"reason={5} completed-steps={6} defer-count={7} deferred-until={8} " +
					"queue-owner=released", player, world.WorldTick, project.TreeActorId,
					project.TreeType, project.TreeLocation,
					hasBuildablePower ? "NoLegalProgressCell" : "NoBuildablePowerType",
					project.ExtensionCount, project.NoProgressDeferralCount,
					project.DeferredUntilTick);
			}

			return null;
		}

		ActorInfo TryChooseEnclosureWall(ProductionQueue queue, IEnumerable<ActorInfo> buildables)
		{
			if (world.WorldTick < project.DeferredUntilTick || queue.AllQueued().Any() ||
				project.RedWallSegments == null || project.RedSegmentIndex >= project.RedWallSegments.Length)
				return null;

			var available = buildables.ToDictionary(a => a.Name);
			var spendableCash = Math.Max(0, playerResources.Cash + playerResources.Resources);
			ActorInfo wall = null;
			CPos? targetCell = null;
			var currentSegmentMissing = project.RedWallSegments[project.RedSegmentIndex]
				.Count(c => !HasOwnWall(c));
			var remainingOrders = project.MaintenanceOnly ? project.RedWallCells.Count(c => !HasOwnWall(c)) :
				TiberiumFieldPolicy.RemainingWallOrders(project.RedWallSegments.Length,
					project.RedSegmentIndex, project.RedAnchorIndex, currentSegmentMissing);
			var criticalRecovery = baseBuilder.SmartEconomySerializesMissingRefinery ||
				baseBuilder.SmartEconomyShouldReserveCashForRefinery || baseBuilder.SmartEconomyWantsSilo;
			var lastRejectedAdmission = (TiberiumFieldAdmissionResult?)null;
			var hasBuildableWall = false;
			foreach (var type in baseBuilder.Info.TiberiumFieldWallTypes)
			{
				if (!available.TryGetValue(type, out var candidate) ||
					candidate.TraitInfoOrDefault<LineBuildInfo>() == null ||
					!candidate.TraitInfo<BuildableInfo>().Queue.Contains(queue.Info.Type))
					continue;
				hasBuildableWall = true;

				var candidateTarget = ChooseNextRedWallCell(candidate);
				if (!candidateTarget.HasValue)
					continue;

				var remainingCost = (long)queue.GetProductionCost(candidate) * remainingOrders;
				var candidateAdmission = TiberiumFieldPolicy.EvaluateAdmission(enabled, true,
					baseBuilder.OpeningActive, criticalRecovery,
					baseBuilder.CountActors(baseBuilder.SmartEconomyRefineryTypes) > 0,
					playerResources.ResourceCapacity > 0,
					baseBuilder.CountActors(baseBuilder.SmartEconomyHarvesterTypes) > 0 &&
						CountUsefulResourceCells(project.TreeLocation) > 0,
					playerPower == null || playerPower.ExcessPower >= baseBuilder.Info.MinimumExcessPower,
					spendableCash, baseBuilder.Info.TiberiumFieldProtectedCash,
					(int)Math.Min(int.MaxValue, remainingCost));
				if (candidateAdmission != TiberiumFieldAdmissionResult.Admitted)
				{
					lastRejectedAdmission = candidateAdmission;
					continue;
				}

				wall = candidate;
				targetCell = candidateTarget;
				break;
			}

			if (wall == null)
			{
				if (lastRejectedAdmission.HasValue &&
					(lastWallAdmissionResult != lastRejectedAdmission || world.WorldTick >= nextWallAdmissionLogTick))
				{
					lastWallAdmissionResult = lastRejectedAdmission;
					nextWallAdmissionLogTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
						baseBuilder.Info.TiberiumFieldProgressLogInterval);
					Log("{0} tick={1} wall-admission-deferred tree={2}/{3}@{4} segment={5}/{6} " +
						"reason={7} remaining-orders={8} cash={9} protected={10} power={11} recovery={12}",
						player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
						project.RedSegmentIndex + 1, project.RedWallSegments.Length, lastRejectedAdmission,
						remainingOrders, spendableCash, baseBuilder.Info.TiberiumFieldProtectedCash,
						playerPower?.ExcessPower ?? int.MaxValue, criticalRecovery);
				}

				if (TiberiumFieldPolicy.ShouldDeferNoProgress(world.WorldTick,
					project.NextProgressCheckTick, false, lastRejectedAdmission.HasValue))
				{
					project.NoProgressDeferralCount++;
					project.QueueActorId = 0;
					project.ActiveActorType = null;
					project.RedTargetCell = null;
					project.DeferredUntilTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
						baseBuilder.Info.TiberiumFieldMaintenanceInterval);
					project.NextProgressCheckTick = project.DeferredUntilTick;
					var missing = project.RedWallSegments[project.RedSegmentIndex]
						.Count(c => !HasOwnWall(c));
					var obstructed = project.RedAnchorIndex >= 2 ? CountResourceObstructedSegmentCells() : 0;
					Log("{0} tick={1} enclosure-no-progress-deferred tree={2}/{3}@{4} " +
						"segment={5}/{6} reason={7} missing={8} resource-obstructed={9} " +
						"defer-count={10} deferred-until={11} queue-owner=released " +
						"perimeter-retained=true gate-retained=true activation=blocked", player,
						world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
						project.RedSegmentIndex + 1, project.RedWallSegments.Length,
						hasBuildableWall ? "NoLegalEnclosureCell" : "NoBuildableWallType",
						missing, obstructed, project.NoProgressDeferralCount,
						project.DeferredUntilTick);
				}

				return null;
			}

			lastWallAdmissionResult = TiberiumFieldAdmissionResult.Admitted;
			project.ActiveActorType = wall.Name;
			project.RedTargetCell = targetCell;
			project.QueueActorId = queue.Actor.ActorID;
			project.Phase = ProjectPhase.Reserved;
			project.DeadlineTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
				baseBuilder.Info.TiberiumFieldReservationTimeout);
			Log("{0} tick={1} wall-reserved tree={2}/{3}@{4} segment={5}/{6} endpoint={7} cell={8} " +
				"wall={9} queue={10}/{11} expiry={12} cash={13} protected={14} activation=blocked",
				player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
				project.RedSegmentIndex + 1, project.RedWallSegments.Length,
				project.RedAnchorIndex < 2 ? $"{project.RedAnchorIndex + 1}/2" : "gap",
				targetCell, wall.Name,
				queue.Actor.ActorID, queue.Info.Type, project.DeadlineTick, spendableCash,
				baseBuilder.Info.TiberiumFieldProtectedCash);
			return wall;
		}

		public bool TryGetPlacement(uint queueActorId, string actorType, out CPos? location, out bool lineBuild)
		{
			location = null;
			lineBuild = false;
			if (!enabled || project == null || project.QueueActorId != queueActorId ||
				project.ActiveActorType != actorType ||
				(project.Phase != ProjectPhase.Reserved && project.Phase != ProjectPhase.Producing))
				return false;

			if (baseBuilder.Info.TiberiumFieldPowerTypes.Contains(actorType))
			{
				if (!project.ExtensionTargetCell.HasValue)
					return true;

				var powerInfo = world.Map.Rules.Actors[actorType];
				var powerBuilding = powerInfo.TraitInfoOrDefault<BuildingInfo>();
				if (powerBuilding != null && IsLegalExtensionCell(project.ExtensionTargetCell.Value,
					powerInfo, powerBuilding))
					location = project.ExtensionTargetCell.Value;

				return true;
			}

			if (baseBuilder.Info.TiberiumFieldWallTypes.Contains(actorType))
			{
				lineBuild = true;
				if (!project.RedTargetCell.HasValue)
					return true;

				var reservedCell = project.RedTargetCell.Value;
				var targetCell = reservedCell;
				var wallInfo = world.Map.Rules.Actors[actorType];
				var wallBuilding = wallInfo.TraitInfoOrDefault<BuildingInfo>();
				bool IsLegal(CPos cell) => wallBuilding != null &&
					world.CanPlaceBuilding(cell, wallInfo, wallBuilding, null) &&
					wallBuilding.IsCloseEnoughToBase(world, player, wallInfo, cell);
				if (!IsLegal(targetCell) && project.RedAnchorIndex >= 2)
				{
					var segment = project.RedWallSegments[project.RedSegmentIndex];
					var retarget = TiberiumFieldPolicy.FirstLegalAlternativeCell(
						segment.Where(c => !HasOwnWall(c)), reservedCell, IsLegal);
					if (retarget.HasValue)
					{
						targetCell = retarget.Value;
						project.RedTargetCell = targetCell;
						Log("{0} tick={1} wall-placement-retarget tree={2}/{3}@{4} segment={5}/{6} " +
							"from={7} to={8} wall={9} queue={10} reason=reserved-cell-illegal " +
							"boundary-preserved=true gate-preserved=true", player, world.WorldTick,
							project.TreeActorId, project.TreeType, project.TreeLocation,
							project.RedSegmentIndex + 1, project.RedWallSegments.Length,
							reservedCell, targetCell, actorType, queueActorId);
					}
				}

				if (IsLegal(targetCell))
					location = targetCell;

				return true;
			}

			var actorInfo = world.Map.Rules.Actors[actorType];
			var buildingInfo = actorInfo.TraitInfoOrDefault<BuildingInfo>();
			if (buildingInfo != null && IsLegalResonatorSite(project.ResonatorLocation, actorInfo, buildingInfo,
				world.GetActorById(project.TreeActorId)))
				location = project.ResonatorLocation;

			return true;
		}

		public void PlacementOrdered()
		{
			if (project == null)
				return;

			if (baseBuilder.Info.TiberiumFieldPowerTypes.Contains(project.ActiveActorType))
			{
				project.Phase = ProjectPhase.AwaitingActor;
				project.DeadlineTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
					baseBuilder.Info.TiberiumFieldPlacementTimeout);
				Log("{0} tick={1} extension-order tree={2}/{3}@{4} step={5} power={6}@{7} " +
					"progress={8} expiry={9}", player, world.WorldTick, project.TreeActorId,
					project.TreeType, project.TreeLocation, project.ExtensionCount + 1,
					project.ActiveActorType, project.ExtensionTargetCell,
					project.ExtensionProgressCells, project.DeadlineTick);
				return;
			}

			if (baseBuilder.Info.TiberiumFieldWallTypes.Contains(project.ActiveActorType))
			{
				var wallType = project.ActiveActorType;
				var targetCell = project.RedTargetCell.Value;
				if (project.RedAnchorIndex < 2)
				{
					project.RedAnchorIndex++;
					if (project.RedAnchorIndex == 2)
					{
						project.Phase = ProjectPhase.AwaitingEnclosure;
						project.DeadlineTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
							baseBuilder.Info.TiberiumFieldPlacementTimeout);
						project.NextProgressCheckTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
							baseBuilder.Info.TiberiumFieldMaintenanceInterval);
						Log("{0} tick={1} wall-segment-order tree={2}/{3}@{4} phase={5} segment={6}/{7} " +
							"from={8} to={9} wall={10} expiry={11} activation=blocked",
							player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
							project.Phase, project.RedSegmentIndex + 1, project.RedWallSegments.Length,
							project.RedWallSegments[project.RedSegmentIndex].First(),
							project.RedWallSegments[project.RedSegmentIndex].Last(), wallType,
							project.DeadlineTick);
						return;
					}

					project.QueueActorId = 0;
					project.ActiveActorType = null;
					project.RedTargetCell = null;
					project.Phase = ProjectPhase.PlanningEnclosure;
					project.DeadlineTick = 0;
					project.RetryCount = 0;
					project.NextProgressCheckTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
						baseBuilder.Info.TiberiumFieldMaintenanceInterval);
					Log("{0} tick={1} wall-anchor-order tree={2} segment={3}/{4} anchor=1/2 cell={5} " +
						"wall={6} reservation=released next-endpoint=pending activation=blocked",
						player, world.WorldTick, project.TreeActorId,
						project.RedSegmentIndex + 1, project.RedWallSegments.Length,
						targetCell, wallType);
					return;
				}

				project.Phase = ProjectPhase.AwaitingEnclosure;
				project.DeadlineTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
					baseBuilder.Info.TiberiumFieldPlacementTimeout);
				project.NextProgressCheckTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
					baseBuilder.Info.TiberiumFieldMaintenanceInterval);
				Log("{0} tick={1} wall-gap-order tree={2}/{3}@{4} phase={5} segment={6}/{7} " +
					"cell={8} wall={9} expiry={10} activation=blocked",
					player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
					project.Phase, project.RedSegmentIndex + 1, project.RedWallSegments.Length,
					targetCell, wallType, project.DeadlineTick);
				return;
			}

			project.Phase = ProjectPhase.AwaitingActor;
			project.DeadlineTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
				baseBuilder.Info.TiberiumFieldPlacementTimeout);
			Log("{0} tick={1} placement-order tree={2}/{3}@{4} phase={5} resonator={6}@{7} queue={8} expiry={9}",
				player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
				project.Phase, project.ResonatorType, project.ResonatorLocation,
				project.QueueActorId, project.DeadlineTick);
		}

		public void PlacementFailed(string reason)
		{
			if (project != null)
				RetryProject(reason);
		}

		Actor[] LiveTrees()
		{
			return world.Actors.Where(a => a.IsInWorld && !a.IsDead &&
				baseBuilder.Info.TiberiumFieldTreeTypes.Contains(a.Info.Name))
				.OrderBy(a => a.ActorID).ToArray();
		}

		void LogTreeTransitions(Actor[] trees)
		{
			var liveIds = trees.Select(t => t.ActorID).ToHashSet();
			if (!initialTreeScanComplete)
			{
				initialTreeScanComplete = true;
				knownTrees.UnionWith(liveIds);
				var types = string.Join(",", trees.GroupBy(t => t.Info.Name)
					.OrderBy(g => g.Key, StringComparer.Ordinal)
					.Select(g => $"{g.Key}:{g.Count()}"));
				Log("{0} tick={1} initial-tree-scan count={2} types={3}",
					player, world.WorldTick, trees.Length, types);
				return;
			}

			foreach (var tree in trees)
				if (knownTrees.Add(tree.ActorID))
					Log("{0} tick={1} discovered-transformed tree={2}/{3}@{4} red={5}", player, world.WorldTick,
						tree.ActorID, tree.Info.Name, tree.Location,
						baseBuilder.Info.TiberiumFieldRedTreeTypes.Contains(tree.Info.Name));

			knownTrees.IntersectWith(liveIds);
			loggedDeferredRedTrees.IntersectWith(liveIds);
		}

		void RefreshAssignments(Actor[] trees)
		{
			var resonators = world.Actors.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
				baseBuilder.Info.TiberiumFieldResonatorTypes.Contains(a.Info.Name) &&
				(playerPower == null || playerPower.PowerState == PowerState.Normal) &&
				(a.TraitOrDefault<ModifiesResources>()?.Range ?? WDist.Zero) != WDist.Zero)
				.OrderBy(a => a.ActorID).ToArray();
			var coverageCandidates = new List<TiberiumFieldCoverageCandidate>();
			foreach (var resonator in resonators)
			{
				var range = resonator.Trait<ModifiesResources>().Range;
				foreach (var tree in trees)
				{
					var distance = (resonator.CenterPosition - tree.CenterPosition).HorizontalLengthSquared;
					if (distance <= range.LengthSquared)
						coverageCandidates.Add(new TiberiumFieldCoverageCandidate(
							tree.ActorID, resonator.ActorID, distance));
				}
			}

			var next = TiberiumFieldPolicy.SelectOneToOneCoverage(coverageCandidates)
				.ToDictionary(c => c.TreeActorId, c => c.ResonatorActorId);
			foreach (var assignment in next.OrderBy(a => a.Key))
			{
				if (assignedResonators.TryGetValue(assignment.Key, out var previous) && previous == assignment.Value)
					continue;

				var tree = trees.First(t => t.ActorID == assignment.Key);
				var resonator = resonators.First(r => r.ActorID == assignment.Value);
				var range = resonator.Trait<ModifiesResources>().Range;
				var distance = (resonator.CenterPosition - tree.CenterPosition).HorizontalLength;
				Log("{0} tick={1} coverage tree={2}/{3}@{4} resonator={5}/{6}@{7} " +
					"distance={8} range={9} powered=true assignment=one-to-one",
					player, world.WorldTick, tree.ActorID, tree.Info.Name, tree.Location,
					resonator.ActorID, resonator.Info.Name, resonator.Location, distance, range.Length);
			}

			foreach (var lost in assignedResonators.Where(a => !next.ContainsKey(a.Key)).ToArray())
				Log("{0} tick={1} coverage-lost tree={2} resonator={3} power-state={4}; releasing assignment",
					player, world.WorldTick, lost.Key, lost.Value,
					playerPower?.PowerState.ToString() ?? "unmanaged");

			assignedResonators.Clear();
			foreach (var assignment in next)
				assignedResonators.Add(assignment.Key, assignment.Value);
		}

		void RefreshProjectState(Actor[] trees)
		{
			if (project == null)
				return;

			var tree = trees.FirstOrDefault(t => t.ActorID == project.TreeActorId);
			if (tree == null)
			{
				Log("{0} tick={1} released project tree={2}/{3}@{4}: tree no longer live",
					player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation);
				project = null;
				return;
			}

			if (project.Phase == ProjectPhase.AwaitingActor &&
				baseBuilder.Info.TiberiumFieldPowerTypes.Contains(project.ActiveActorType) &&
				project.ExtensionTargetCell.HasValue)
			{
				var extension = world.ActorsHavingTrait<Building>()
					.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
						a.Info.Name == project.ActiveActorType &&
						a.Location == project.ExtensionTargetCell.Value)
					.OrderBy(a => a.ActorID).FirstOrDefault();
				if (extension != null)
				{
					project.ExtensionCount++;
					var placedType = project.ActiveActorType;
					var placedCell = project.ExtensionTargetCell.Value;
					project.QueueActorId = 0;
					project.ActiveActorType = null;
					project.ExtensionTargetCell = null;
					project.DeadlineTick = 0;
					project.RetryCount = 0;
					var projectReady = ProjectPartsWithinBuildArea(tree);
					project.Phase = projectReady ?
						(project.RedWallCells == null ? ProjectPhase.Planned : ProjectPhase.PlanningEnclosure) :
						ProjectPhase.PlanningExtension;
					project.PlannedTick = world.WorldTick;
					Log("{0} tick={1} extension-complete tree={2}/{3}@{4} step={5} " +
						"power={6}/{7}@{8} progress={9} next={10}", player, world.WorldTick,
						project.TreeActorId, project.TreeType, project.TreeLocation,
						project.ExtensionCount, extension.ActorID, placedType, placedCell,
						project.ExtensionProgressCells, projectReady ?
							(project.RedWallCells == null ? "resonator" : "enclosure") : "extension");
					return;
				}
			}

			if ((project.Phase == ProjectPhase.Planned ||
				project.Phase == ProjectPhase.PlanningEnclosure ||
				project.Phase == ProjectPhase.AwaitingRouteProof) && !ProjectPartsWithinBuildArea(tree))
			{
				ResetRouteProof(false);
				project.QueueActorId = 0;
				project.ActiveActorType = null;
				project.RedTargetCell = null;
				project.Phase = ProjectPhase.PlanningExtension;
				project.PlannedTick = world.WorldTick;
				Log("{0} tick={1} extension-required tree={2}/{3}@{4} resonator={5}@{6} " +
					"reason=project-parts-outside-build-area completed-steps={7}", player,
					world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
					project.ResonatorType, project.ResonatorLocation, project.ExtensionCount);
				return;
			}

			if (assignedResonators.TryGetValue(project.TreeActorId, out var resonatorId) &&
				(project.RedWallCells == null || (RedEnclosureComplete() &&
					(project.MaintenanceOnly || project.OrdinaryRouteProven))))
			{
				if (project.MaintenanceOnly)
				{
					if (activeRedEnclosures.TryGetValue(project.TreeActorId, out var maintained))
						maintained.NextMaintenanceTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
							baseBuilder.Info.TiberiumFieldMaintenanceInterval);
					Log("{0} tick={1} maintenance-complete tree={2}/{3}@{4} resonator={5} " +
						"gate={6} next-scan={7} missing=0", player, world.WorldTick,
						project.TreeActorId, project.TreeType, project.TreeLocation, resonatorId,
						string.Join(";", project.RedGateCells),
						maintained?.NextMaintenanceTick ?? world.WorldTick);
					project = null;
					return;
				}

				if (project.RedWallCells != null)
					RegisterActiveRedEnclosure(resonatorId);
				Log("{0} tick={1} completed project tree={2}/{3}@{4} resonator={5} phase=complete",
					player, world.WorldTick, project.TreeActorId, project.TreeType,
					project.TreeLocation, resonatorId);
				project = null;
				return;
			}

			if (project.Phase == ProjectPhase.PlanningEnclosure)
			{
				var previousSegment = project.RedSegmentIndex;
				var ownedWalls = world.ActorsHavingTrait<Building>()
					.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
						baseBuilder.Info.TiberiumFieldWallTypes.Contains(a.Info.Name))
					.Select(a => a.Location).ToArray();
				project.RedSegmentIndex = TiberiumFieldPolicy.FirstIncompleteSegmentIndex(
					project.RedWallSegments, ownedWalls, project.RedSegmentIndex);
				if (project.RedSegmentIndex > previousSegment)
				{
					project.RedAnchorIndex = project.MaintenanceOnly ? 2 : 0;
					project.QueueActorId = 0;
					project.ActiveActorType = null;
					project.RedTargetCell = null;
					project.RetryCount = 0;
					project.NextProgressCheckTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
						baseBuilder.Info.TiberiumFieldMaintenanceInterval);
					Log("{0} tick={1} wall-segments-observed-complete tree={2}/{3}@{4} " +
						"from={5} through={6}/{7} gate-preserved=true activation=blocked",
						player, world.WorldTick, project.TreeActorId, project.TreeType,
						project.TreeLocation, previousSegment + 1, project.RedSegmentIndex,
						project.RedWallSegments.Length);
				}

				if (project.RedSegmentIndex >= project.RedWallSegments.Length &&
					!project.MaintenanceOnly && RedEnclosureComplete())
					BeginRedRouteProof();

				return;
			}

			if (project.Phase == ProjectPhase.AwaitingRouteProof)
			{
				RefreshRedRouteProof();
				return;
			}

			if (project.Phase == ProjectPhase.PlanningExtension)
				return;

			if (project.Phase == ProjectPhase.AwaitingEnclosure)
			{
				if (RedSegmentComplete(project.RedWallSegments[project.RedSegmentIndex]))
				{
					Log("{0} tick={1} wall-segment-complete tree={2}/{3}@{4} segment={5}/{6} " +
						"wall={7} gate-preserved=true activation=blocked", player, world.WorldTick,
						project.TreeActorId, project.TreeType, project.TreeLocation,
						project.RedSegmentIndex + 1, project.RedWallSegments.Length, project.ActiveActorType);
					project.RedSegmentIndex++;
					while (project.RedSegmentIndex < project.RedWallSegments.Length &&
						RedSegmentComplete(project.RedWallSegments[project.RedSegmentIndex]))
						project.RedSegmentIndex++;
					project.RedAnchorIndex = project.MaintenanceOnly ? 2 : 0;
					project.NextProgressCheckTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
						baseBuilder.Info.TiberiumFieldMaintenanceInterval);
					project.RetryCount = 0;
					project.QueueActorId = 0;
					project.ActiveActorType = null;
					project.RedTargetCell = null;
					if (project.RedSegmentIndex >= project.RedWallSegments.Length && RedEnclosureComplete())
					{
						if (project.MaintenanceOnly)
							return;

						BeginRedRouteProof();
					}
					else
						project.Phase = ProjectPhase.PlanningEnclosure;
				}
				else if (project.RedAnchorIndex >= 2)
				{
					// LineBuild stops at resource or other non-buildable cells. Release the
					// completed order and opportunistically fill only legal missing cells as
					// ordinary harvesting clears them; the intended gate is not in this set.
					project.QueueActorId = 0;
					project.ActiveActorType = null;
					project.RedTargetCell = null;
					project.Phase = ProjectPhase.PlanningEnclosure;
					project.DeadlineTick = 0;
				}
				else if (TiberiumFieldPolicy.DeadlineReached(world.WorldTick, project.DeadlineTick))
					RetryProject("wall endpoint did not become available before timeout");

				return;
			}

			if (project.Phase == ProjectPhase.Planned || project.Phase == ProjectPhase.AwaitingActor)
			{
				if (project.Phase == ProjectPhase.AwaitingActor &&
					TiberiumFieldPolicy.DeadlineReached(world.WorldTick, project.DeadlineTick))
					RetryProject("placed actor did not become live and powered before timeout");
				return;
			}

			var queueActor = world.GetActorById(project.QueueActorId);
			var queued = queueActor != null && queueActor.Owner == player && queueActor.IsInWorld &&
				!queueActor.IsDead && queueActor.TraitsImplementing<ProductionQueue>()
					.Any(q => q.AllQueued().Any(i => i.Item == project.ActiveActorType));
			if (queued && project.Phase == ProjectPhase.Reserved)
			{
				project.Phase = ProjectPhase.Producing;
				Log("{0} tick={1} production-accepted tree={2}/{3}@{4} phase={5} " +
					"item={6} resonator-site={7} queue={8}", player, world.WorldTick,
					project.TreeActorId, project.TreeType, project.TreeLocation, project.Phase,
					project.ActiveActorType, project.ResonatorLocation, project.QueueActorId);
				return;
			}

			if (!queued && TiberiumFieldPolicy.DeadlineReached(world.WorldTick, project.DeadlineTick))
				RetryProject("queue reservation expired without matching production");
		}

		void BeginRedRouteProof()
		{
			project.Phase = ProjectPhase.AwaitingRouteProof;
			project.PlannedTick = world.WorldTick;
			project.QueueActorId = 0;
			project.ActiveActorType = null;
			project.RedTargetCell = null;
			ResetRouteProof(false);
			Log("{0} tick={1} red-enclosure-complete tree={2}/{3}@{4} wall-cells={5} " +
				"gate={6} gate-width={7} activation=blocked reason=awaiting-real-harvester-route",
				player, world.WorldTick, project.TreeActorId, project.TreeType,
				project.TreeLocation, project.RedWallCells.Length,
				string.Join(";", project.RedGateCells), project.RedGateCells.Length);
		}

		void RegisterActiveRedEnclosure(uint resonatorActorId)
		{
			var resonator = world.GetActorById(resonatorActorId);
			if (resonator == null || !resonator.IsInWorld || resonator.IsDead || resonator.Owner != player)
				return;

			var enclosure = new ActiveRedEnclosure
			{
				TreeActorId = project.TreeActorId,
				TreeType = project.TreeType,
				TreeLocation = project.TreeLocation,
				ResonatorActorId = resonatorActorId,
				ResonatorType = resonator.Info.Name,
				ResonatorLocation = resonator.Location,
				WallCells = project.RedWallCells.ToArray(),
				GateCells = project.RedGateCells.ToArray(),
				WallSegments = project.RedWallSegments.Select(s => s.ToArray()).ToArray(),
				NextMaintenanceTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
					baseBuilder.Info.TiberiumFieldMaintenanceInterval)
			};
			activeRedEnclosures[project.TreeActorId] = enclosure;
			Log("{0} tick={1} red-enclosure-active tree={2}/{3}@{4} resonator={5}/{6}@{7} " +
				"wall-cells={8} gate={9} next-maintenance={10}", player, world.WorldTick,
				enclosure.TreeActorId, enclosure.TreeType, enclosure.TreeLocation,
				enclosure.ResonatorActorId, enclosure.ResonatorType, enclosure.ResonatorLocation,
				enclosure.WallCells.Length, string.Join(";", enclosure.GateCells),
				enclosure.NextMaintenanceTick);
		}

		void RefreshActiveRedEnclosures(Actor[] trees)
		{
			var liveTreeIds = trees.Select(t => t.ActorID).ToHashSet();
			foreach (var enclosure in activeRedEnclosures.Values.OrderBy(e => e.TreeActorId).ToArray())
			{
				if (!liveTreeIds.Contains(enclosure.TreeActorId) ||
					!assignedResonators.TryGetValue(enclosure.TreeActorId, out var resonatorId) ||
					resonatorId != enclosure.ResonatorActorId)
				{
					activeRedEnclosures.Remove(enclosure.TreeActorId);
					assignedResonators.Remove(enclosure.TreeActorId);
					Log("{0} tick={1} red-enclosure-released tree={2}/{3}@{4} resonator={5} " +
						"reason=tree-or-powered-resonator-invalid", player, world.WorldTick,
						enclosure.TreeActorId, enclosure.TreeType, enclosure.TreeLocation,
						enclosure.ResonatorActorId);
					continue;
				}

				if (!TiberiumFieldPolicy.DeadlineReached(world.WorldTick, enclosure.NextMaintenanceTick))
					continue;

				enclosure.NextMaintenanceTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
					baseBuilder.Info.TiberiumFieldMaintenanceInterval);
				var missing = TiberiumFieldPolicy.MissingPlannedCells(enclosure.WallCells,
					enclosure.WallCells.Where(HasOwnWall));
				Log("{0} tick={1} maintenance-scan tree={2}/{3}@{4} resonator={5} missing={6} " +
					"gate={7} next-scan={8}", player, world.WorldTick, enclosure.TreeActorId,
					enclosure.TreeType, enclosure.TreeLocation, enclosure.ResonatorActorId,
					missing.Length, string.Join(";", enclosure.GateCells), enclosure.NextMaintenanceTick);
				if (missing.Length == 0 || project != null)
					continue;

				var firstIncompleteSegment = Array.FindIndex(enclosure.WallSegments,
					s => s.Any(c => !HasOwnWall(c)));
				if (firstIncompleteSegment < 0)
					continue;

				project = new FieldProject
				{
					TreeActorId = enclosure.TreeActorId,
					TreeType = enclosure.TreeType,
					TreeLocation = enclosure.TreeLocation,
					ResonatorType = enclosure.ResonatorType,
					ResonatorLocation = enclosure.ResonatorLocation,
					Phase = ProjectPhase.PlanningEnclosure,
					MaintenanceOnly = true,
					PlannedTick = world.WorldTick,
					NextWaitingLogTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
						baseBuilder.Info.TiberiumFieldProgressLogInterval),
					NextProgressCheckTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
						baseBuilder.Info.TiberiumFieldMaintenanceInterval),
					RedWallCells = enclosure.WallCells.ToArray(),
					RedGateCells = enclosure.GateCells.ToArray(),
					RedWallSegments = enclosure.WallSegments.Select(s => s.ToArray()).ToArray(),
					RedSegmentIndex = firstIncompleteSegment,
					RedAnchorIndex = 2
				};
				Log("{0} tick={1} maintenance-queued tree={2}/{3}@{4} resonator={5} " +
					"segment={6}/{7} missing={8} gate-preserved=true", player, world.WorldTick,
					project.TreeActorId, project.TreeType, project.TreeLocation,
					enclosure.ResonatorActorId, project.RedSegmentIndex + 1,
					project.RedWallSegments.Length, missing.Length);
			}
		}

		void PlanNextProject(Actor[] trees)
		{
			var resonatorInfo = baseBuilder.Info.TiberiumFieldResonatorTypes
				.Where(world.Map.Rules.Actors.ContainsKey)
				.Select(t => world.Map.Rules.Actors[t])
				.FirstOrDefault(a => a.TraitInfoOrDefault<BuildingInfo>() != null &&
					a.TraitInfoOrDefault<ModifiesResourcesInfo>() != null);
			if (resonatorInfo == null)
				return;

			var buildingInfo = resonatorInfo.TraitInfo<BuildingInfo>();
			var sites = new Dictionary<uint, CPos>();
			var candidates = new List<TiberiumFieldProjectCandidate>();
			var redPlans = new Dictionary<uint, TiberiumFieldPerimeterPlan>();
			var ownedWallCells = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
					baseBuilder.Info.TiberiumFieldWallTypes.Contains(a.Info.Name))
				.Select(a => a.Location).ToHashSet();
			foreach (var tree in trees.Where(t => !assignedResonators.ContainsKey(t.ActorID)))
			{
				var site = ChooseResonatorSite(tree, resonatorInfo, buildingInfo);
				var demand = CountUsefulResourceCells(tree.Location);
				var isRed = baseBuilder.Info.TiberiumFieldRedTreeTypes.Contains(tree.Info.Name);
				TiberiumFieldPerimeterPlan redPlan = null;
				if (isRed && site.HasValue)
				{
					var gateBuilding = world.ActorsHavingTrait<Building>()
						.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
							baseBuilder.Info.TiberiumFieldGateBuildingTypes.Contains(a.Info.Name))
						.OrderBy(a => (a.Location - tree.Location).LengthSquared)
						.ThenBy(a => a.ActorID).FirstOrDefault();
					if (gateBuilding != null)
						redPlan = TiberiumFieldPolicy.PlanRedPerimeter(tree.Location,
							buildingInfo.Tiles(site.Value), gateBuilding.Location,
							baseBuilder.Info.TiberiumFieldPerimeterStandoff);
				}

				var routeFeasible = site.HasValue && demand > 0 && (!isRed || redPlan != null);
				var safety = -NearestOwnedBuildingDistanceSquared(tree.Location);
				long cost = Math.Max(0, resonatorInfo.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0);
				if (redPlan != null)
				{
					var wallOrders = TiberiumFieldPolicy.RemainingWallOrdersFromWorld(
						redPlan.WallSegments, ownedWallCells);
					var wallCost = baseBuilder.Info.TiberiumFieldWallTypes
						.Where(world.Map.Rules.Actors.ContainsKey)
						.Select(t => Math.Max(0, world.Map.Rules.Actors[t]
							.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0))
						.DefaultIfEmpty(0).Min();
					cost += (long)wallOrders * wallCost;
				}

				if (site.HasValue && !IsLegalResonatorSite(site.Value, resonatorInfo, buildingInfo, tree))
					cost += baseBuilder.Info.TiberiumFieldPowerTypes
						.Where(world.Map.Rules.Actors.ContainsKey)
						.Select(t => Math.Max(0, world.Map.Rules.Actors[t]
							.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0))
						.DefaultIfEmpty(0).Min();
				candidates.Add(new TiberiumFieldProjectCandidate(tree.ActorID, routeFeasible,
					demand, safety, (int)Math.Min(int.MaxValue, cost)));
				if (site.HasValue)
					sites.Add(tree.ActorID, site.Value);
				if (redPlan != null)
					redPlans.Add(tree.ActorID, redPlan);
			}

			var selected = TiberiumFieldPolicy.BestProject(candidates.Where(c => c.RouteFeasible));
			if (!selected.HasValue || !sites.TryGetValue(selected.Value.TreeActorId, out var location))
				return;

			var selectedTree = trees.First(t => t.ActorID == selected.Value.TreeActorId);
			var selectedRed = baseBuilder.Info.TiberiumFieldRedTreeTypes.Contains(selectedTree.Info.Name);
			redPlans.TryGetValue(selectedTree.ActorID, out var selectedRedPlan);
			project = new FieldProject
			{
				TreeActorId = selectedTree.ActorID,
				TreeType = selectedTree.Info.Name,
				TreeLocation = selectedTree.Location,
				ResonatorType = resonatorInfo.Name,
				ResonatorLocation = location,
				Phase = selectedRed ? ProjectPhase.PlanningEnclosure : ProjectPhase.Planned,
				PlannedTick = world.WorldTick,
				NextWaitingLogTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
					baseBuilder.Info.TiberiumFieldProgressLogInterval),
				NextProgressCheckTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
					baseBuilder.Info.TiberiumFieldMaintenanceInterval),
				RedWallCells = selectedRedPlan?.WallCells,
				RedGateCells = selectedRedPlan?.GateCells,
				RedWallSegments = selectedRedPlan?.WallSegments
			};
			if (!ProjectPartsWithinBuildArea(selectedTree))
				project.Phase = ProjectPhase.PlanningExtension;
			var effectRange = resonatorInfo.TraitInfo<ModifiesResourcesInfo>().Range;
			var center = world.Map.CenterOfCell(location) + buildingInfo.CenterOffset(world);
			var distance = (center - selectedTree.CenterPosition).HorizontalLength;
			Log("{0} tick={1} planned tree={2}/{3}@{4} phase={5} resonator={6}@{7} " +
				"distance={8} range={9} demand={10} safety={11} remaining={12} route=resource-backed",
				player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
				project.Phase, project.ResonatorType, project.ResonatorLocation,
				distance, effectRange.Length, selected.Value.UsefulDemand,
				selected.Value.SafetyScore, selected.Value.RemainingCommitment);
			if (selectedRed)
				Log("{0} tick={1} red-enclosure-planned tree={2}/{3}@{4} resonator={5}@{6} " +
					"standoff={7} wall-cells={8} segments={9} gate={10} gate-width={11} activation=blocked",
					player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
					project.ResonatorType, project.ResonatorLocation,
					baseBuilder.Info.TiberiumFieldPerimeterStandoff, project.RedWallCells.Length,
					project.RedWallSegments.Length, string.Join(";", project.RedGateCells),
					project.RedGateCells.Length);
			if (project.Phase == ProjectPhase.PlanningExtension)
				Log("{0} tick={1} extension-required tree={2}/{3}@{4} resonator={5}@{6} " +
					"reason=initial-site-outside-build-area configured-step={7}", player,
					world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
					project.ResonatorType, project.ResonatorLocation,
					baseBuilder.Info.TiberiumFieldExtensionStep);
		}

		void RefreshRedRouteProof()
		{
			if (!RedEnclosureComplete())
			{
				ResetRouteProof(true);
				project.RedSegmentIndex = Array.FindIndex(project.RedWallSegments,
					s => s.Any(c => !HasOwnWall(c)));
				project.RedSegmentIndex = Math.Max(0, project.RedSegmentIndex);
				project.RedAnchorIndex = 2;
				project.Phase = ProjectPhase.PlanningEnclosure;
				Log("{0} tick={1} route-proof-invalidated tree={2}/{3}@{4} " +
					"reason=enclosure-breached activation=blocked", player, world.WorldTick,
					project.TreeActorId, project.TreeType, project.TreeLocation);
				return;
			}

			if (!TryFindOrdinaryGateRoute(out var harvester, out var refinery,
				out var resourceCell, out var outboundLength, out var inboundLength,
				out var searchSummary))
			{
				if (project.RouteHarvesterActorId != 0)
				{
					ResetRouteProof(false);
					Log("{0} tick={1} route-path-lost tree={2}/{3}@{4} " +
						"reason=no-bidirectional-ordinary-locomotor-path activation=blocked",
						player, world.WorldTick, project.TreeActorId, project.TreeType,
						project.TreeLocation);
				}

				if (world.WorldTick >= nextRouteDiagnosticTick)
				{
					nextRouteDiagnosticTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
						baseBuilder.Info.TiberiumFieldProgressLogInterval);
					Log("{0} tick={1} route-path-rejected tree={2}/{3}@{4} gate={5} " +
						"inside-resources={6} eligible-ordinary-harvesters={7} linked-refineries={8} " +
						"harvestable-candidates={9} outbound-paths={10} inbound-paths={11} " +
						"exact-gate-paths=0 activation=blocked", player, world.WorldTick,
						project.TreeActorId, project.TreeType, project.TreeLocation,
						string.Join(";", project.RedGateCells), searchSummary.InsideResources,
						searchSummary.EligibleHarvesters, searchSummary.LinkedRefineries,
						searchSummary.HarvestableCandidates, searchSummary.OutboundPaths,
						searchSummary.InboundPaths);
				}

				return;
			}

			if (project.RouteHarvesterActorId != harvester.ActorID ||
				project.RouteRefineryActorId != refinery.ActorID || project.RouteResourceCell != resourceCell)
			{
				project.RouteHarvesterActorId = harvester.ActorID;
				project.RouteRefineryActorId = refinery.ActorID;
				project.RouteResourceCell = resourceCell;
				project.RouteStage = TiberiumFieldRoundTripStage.AwaitingRefinery;
				project.RouteLastContents = harvester.Trait<Harvester>().Contents.Values.Sum();
				Log("{0} tick={1} route-path-validated tree={2}/{3}@{4} harvester={5}/{6}@{7} " +
					"refinery={8}/{9}@{10} resource={11} gate={12} outbound-cells={13} " +
					"inbound-cells={14} locomotor=actual activation=blocked reason=awaiting-live-round-trip",
					player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
					harvester.ActorID, harvester.Info.Name, harvester.Location, refinery.ActorID,
					refinery.Info.Name, refinery.Location, resourceCell,
					string.Join(";", project.RedGateCells), outboundLength, inboundLength);
			}

			var relevantStealthMissions = RefreshRelevantStealthMissions();
			if (!project.OrdinaryRouteProven || (relevantStealthMissions > 0 && !project.StealthRouteProven))
				return;

			project.Phase = ProjectPhase.Planned;
			project.PlannedTick = world.WorldTick;
			Log("{0} tick={1} red-route-proof-complete tree={2}/{3}@{4} harvester={5} " +
				"refinery={6} resource={7} gate={8} ordinary-round-trip=true " +
				"reserved-stealth={9} activation=eligible", player, world.WorldTick,
				project.TreeActorId, project.TreeType, project.TreeLocation,
				project.RouteHarvesterActorId, project.RouteRefineryActorId,
				project.RouteResourceCell, string.Join(";", project.RedGateCells),
				relevantStealthMissions == 0 ? "not-active" : "crossing-proven");
		}

		bool TryFindOrdinaryGateRoute(out Actor selectedHarvester, out Actor selectedRefinery,
			out CPos selectedResource, out int outboundLength, out int inboundLength,
			out GateRouteSearchSummary summary)
		{
			selectedHarvester = null;
			selectedRefinery = null;
			selectedResource = default(CPos);
			outboundLength = 0;
			inboundLength = 0;
			summary = default(GateRouteSearchSummary);
			if (project.RedWallCells == null || resourceLayer == null)
				return false;

			var tree = world.GetActorById(project.TreeActorId);
			if (tree == null)
				return false;

			var resources = world.Map.FindTilesInCircle(tree.Location,
				Math.Max(1, baseBuilder.Info.TiberiumFieldDemandRadius))
				.Where(c => TiberiumFieldPolicy.RouteZone(c, project.RedWallCells,
					project.RedGateCells) == TiberiumFieldRouteZone.Inside &&
					resourceLayer.GetResource(c).Type != null)
				.OrderBy(c => (c - tree.Location).LengthSquared).ThenBy(c => c.Y).ThenBy(c => c.X)
				.Take(16).ToArray();
			if (resources.Length == 0)
			{
				summary = new GateRouteSearchSummary(0, 0, 0, 0, 0, 0);
				return false;
			}

			var harvesters = world.ActorsWithTrait<Harvester>()
				.Where(p => p.Actor.Owner == player && p.Actor.IsInWorld && !p.Actor.IsDead &&
					baseBuilder.SmartEconomyHarvesterTypes.Contains(p.Actor.Info.Name) &&
					p.Actor.TraitOrDefault<Mobile>() != null &&
					!unitReservations.Any(r => r.IsUnitReserved(p.Actor)))
				.OrderBy(p => p.Actor.ActorID).Take(8).ToArray();
			var linkedRefineries = 0;
			var harvestableCandidates = 0;
			var outboundPaths = 0;
			var inboundPaths = 0;
			foreach (var pair in harvesters)
			{
				var refinery = pair.Trait.LinkedProc ?? pair.Trait.LastLinkedProc;
				var accept = refinery?.TraitOrDefault<IAcceptResources>();
				if (refinery == null || refinery.Owner != player || !refinery.IsInWorld || refinery.IsDead ||
					accept == null || !baseBuilder.SmartEconomyRefineryTypes.Contains(refinery.Info.Name))
					continue;
				linkedRefineries++;

				var delivery = refinery.Location + accept.DeliveryOffset;
				foreach (var resource in resources.Where(c => pair.Trait.CanHarvestCell(pair.Actor, c)))
				{
					harvestableCandidates++;
					var mobile = pair.Actor.Trait<Mobile>();
					var outbound = mobile.Pathfinder.FindUnitPath(delivery, resource, pair.Actor,
						refinery, BlockedByActor.Immovable);
					if (outbound.Count == 0)
						continue;
					outboundPaths++;

					var inbound = mobile.Pathfinder.FindUnitPath(resource, delivery, pair.Actor,
						refinery, BlockedByActor.Immovable);
					if (inbound.Count == 0)
						continue;
					inboundPaths++;
					if (!TiberiumFieldPolicy.IsBidirectionalGatePath(outbound, inbound, project.RedGateCells))
						continue;

					selectedHarvester = pair.Actor;
					selectedRefinery = refinery;
					selectedResource = resource;
					outboundLength = outbound.Count;
					inboundLength = inbound.Count;
					summary = new GateRouteSearchSummary(resources.Length, harvesters.Length,
						linkedRefineries, harvestableCandidates, outboundPaths, inboundPaths);
					return true;
				}
			}

			summary = new GateRouteSearchSummary(resources.Length, harvesters.Length,
				linkedRefineries, harvestableCandidates, outboundPaths, inboundPaths);
			return false;
		}

		void ObserveGateTraffic()
		{
			if (project == null || project.Phase != ProjectPhase.AwaitingRouteProof ||
				project.RedWallCells == null)
				return;

			ObserveOrdinaryGateTraffic();
			ObserveStealthGateTraffic();
		}

		void ObserveOrdinaryGateTraffic()
		{
			if (project.RouteHarvesterActorId == 0 || project.OrdinaryRouteProven)
				return;

			var actor = world.GetActorById(project.RouteHarvesterActorId);
			var refinery = world.GetActorById(project.RouteRefineryActorId);
			var harvester = actor?.TraitOrDefault<Harvester>();
			var accept = refinery?.TraitOrDefault<IAcceptResources>();
			if (actor == null || actor.Owner != player || !actor.IsInWorld || actor.IsDead ||
				harvester == null || refinery == null || refinery.Owner != player || !refinery.IsInWorld ||
				refinery.IsDead || accept == null || unitReservations.Any(r => r.IsUnitReserved(actor)))
				return;

			var contents = harvester.Contents.Values.Sum();
			var delivery = refinery.Location + accept.DeliveryOffset;
			var zone = TiberiumFieldPolicy.RouteZone(actor.Location,
				project.RedWallCells, project.RedGateCells);
			var station = actor.TraitOrDefault<IHarvesterFieldStation>();
			var emptyAtRefinery = actor.Location == delivery && harvester.IsEmpty;
			var harvested = contents > project.RouteLastContents && zone == TiberiumFieldRouteZone.Inside;
			var committedInside = station != null && station.HasCommittedField &&
				TiberiumFieldPolicy.RouteZone(station.CommittedField, project.RedWallCells,
					project.RedGateCells) == TiberiumFieldRouteZone.Inside;
			var unloadedAtRefinery = actor.Location == delivery && harvester.IsEmpty && committedInside &&
				(harvester.LinkedProc == refinery || harvester.LastLinkedProc == refinery);
			var previous = project.RouteStage;
			project.RouteStage = TiberiumFieldPolicy.AdvanceRoundTrip(previous, zone,
				emptyAtRefinery, harvested, unloadedAtRefinery);
			project.RouteLastContents = contents;
			if (project.RouteStage == previous)
				return;

			Log("{0} tick={1} gate-round-trip-progress tree={2}/{3}@{4} harvester={5}/{6}@{7} " +
				"stage={8}->{9} zone={10} contents={11} refinery={12} gate={13}",
				player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
				actor.ActorID, actor.Info.Name, actor.Location, previous, project.RouteStage,
				zone, contents, refinery.ActorID, string.Join(";", project.RedGateCells));
			if (project.RouteStage != TiberiumFieldRoundTripStage.Complete)
				return;

			project.OrdinaryRouteProven = true;
			Log("{0} tick={1} gate-round-trip-proven tree={2}/{3}@{4} harvester={5}/{6} " +
				"refinery={7}/{8} resource={9} gate={10} locomotor=actual unload-observed=true",
				player, world.WorldTick, project.TreeActorId, project.TreeType, project.TreeLocation,
				actor.ActorID, actor.Info.Name, refinery.ActorID, refinery.Info.Name,
				station.CommittedField, string.Join(";", project.RedGateCells));
		}

		int RefreshRelevantStealthMissions()
		{
			var relevant = world.ActorsWithTrait<Harvester>()
				.Where(p => p.Actor.Owner == player && p.Actor.IsInWorld && !p.Actor.IsDead &&
					TryGetRedBombMissionResource(p.Actor, out var cell) &&
					TiberiumFieldPolicy.RouteZone(cell, project.RedWallCells,
						project.RedGateCells) == TiberiumFieldRouteZone.Inside)
				.Select(p => p.Actor).OrderBy(a => a.ActorID).ToArray();
			var ids = relevant.Select(a => a.ActorID).ToHashSet();
			foreach (var stale in stealthRouteObservations.Keys.Where(id => !ids.Contains(id)).ToArray())
				stealthRouteObservations.Remove(stale);
			foreach (var actor in relevant)
				if (!stealthRouteObservations.ContainsKey(actor.ActorID))
					stealthRouteObservations.Add(actor.ActorID,
						new StealthRouteObservation { PreviousCell = actor.Location });

			return relevant.Length;
		}

		void ObserveStealthGateTraffic()
		{
			if (project.StealthRouteProven)
				return;

			foreach (var pair in stealthRouteObservations.OrderBy(p => p.Key).ToArray())
			{
				var actor = world.GetActorById(pair.Key);
				if (actor == null || actor.Owner != player || !actor.IsInWorld || actor.IsDead ||
					!TryGetRedBombMissionResource(actor, out var resourceCell) ||
					TiberiumFieldPolicy.RouteZone(resourceCell, project.RedWallCells,
						project.RedGateCells) != TiberiumFieldRouteZone.Inside)
					continue;

				var observation = pair.Value;
				var previousZone = TiberiumFieldPolicy.RouteZone(observation.PreviousCell,
					project.RedWallCells, project.RedGateCells);
				var zone = TiberiumFieldPolicy.RouteZone(actor.Location,
					project.RedWallCells, project.RedGateCells);
				if (zone == TiberiumFieldRouteZone.Gate && previousZone != TiberiumFieldRouteZone.Gate)
				{
					observation.SawGate = true;
					observation.ApproachZone = previousZone;
				}
				else if (observation.SawGate && zone != TiberiumFieldRouteZone.Gate)
				{
					if (zone != observation.ApproachZone &&
						zone != TiberiumFieldRouteZone.Gate &&
						observation.ApproachZone != TiberiumFieldRouteZone.Gate)
					{
						project.StealthRouteProven = true;
						Log("{0} tick={1} reserved-stealth-gate-crossing tree={2}/{3}@{4} " +
							"harvester={5}/{6} resource={7} gate={8} direction={9}-to-{10} " +
							"reservation=RedTiberiumBomb locomotor=actual", player, world.WorldTick,
							project.TreeActorId, project.TreeType, project.TreeLocation,
							actor.ActorID, actor.Info.Name, resourceCell,
							string.Join(";", project.RedGateCells), observation.ApproachZone, zone);
						return;
					}

					observation.SawGate = false;
				}

				observation.PreviousCell = actor.Location;
			}
		}

		bool TryGetRedBombMissionResource(Actor actor, out CPos resourceCell)
		{
			foreach (var mission in redBombMissions)
				if (mission.TryGetMissionResourceCell(actor, out resourceCell))
					return true;

			resourceCell = default(CPos);
			return false;
		}

		void ResetRouteProof(bool clearCompleted)
		{
			project.RouteHarvesterActorId = 0;
			project.RouteRefineryActorId = 0;
			project.RouteResourceCell = default(CPos);
			project.RouteStage = TiberiumFieldRoundTripStage.AwaitingRefinery;
			project.RouteLastContents = 0;
			stealthRouteObservations.Clear();
			if (clearCompleted)
			{
				project.OrdinaryRouteProven = false;
				project.StealthRouteProven = false;
			}
		}

		void ObserveHarvesterAccess(Actor[] trees)
		{
			var liveHarvesterIds = new HashSet<uint>();
			var treeById = trees.ToDictionary(t => t.ActorID);
			var radiusSquared = Math.Max(1, baseBuilder.Info.TiberiumFieldDemandRadius);
			radiusSquared *= radiusSquared;
			foreach (var harvesterActor in world.ActorsHavingTrait<Harvester>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
					baseBuilder.SmartEconomyHarvesterTypes.Contains(a.Info.Name))
				.OrderBy(a => a.ActorID))
			{
				liveHarvesterIds.Add(harvesterActor.ActorID);
				var harvester = harvesterActor.Trait<Harvester>();
				var contents = harvester.Contents.Values.Sum();
				if (!harvesterAccess.TryGetValue(harvesterActor.ActorID, out var observation))
				{
					observation = new HarvesterAccessObservation { LastContents = contents };
					harvesterAccess.Add(harvesterActor.ActorID, observation);
				}

				if (contents > observation.LastContents)
				{
					var field = assignedResonators.Keys.Where(id => treeById.ContainsKey(id) &&
						!provenHarvestTrees.Contains(id))
						.Select(id => treeById[id])
						.Where(t => (harvesterActor.Location - t.Location).LengthSquared <= radiusSquared)
						.OrderBy(t => (harvesterActor.Location - t.Location).LengthSquared)
						.ThenBy(t => t.ActorID).FirstOrDefault();
					if (field != null && observation.TreeActorId == 0 &&
						!harvesterAccess.Values.Any(o => o.TreeActorId == field.ActorID))
					{
						observation.TreeActorId = field.ActorID;
						observation.LoadedAmount = contents;
						Log("{0} tick={1} harvest-loaded tree={2}/{3}@{4} harvester={5}/{6}@{7} " +
							"contents={8} locomotor=actual",
							player, world.WorldTick, field.ActorID, field.Info.Name, field.Location,
							harvesterActor.ActorID, harvesterActor.Info.Name, harvesterActor.Location, contents);
					}
				}

				if (observation.TreeActorId != 0 && contents < observation.LastContents)
				{
					var refinery = harvester.LinkedProc ?? harvester.LastLinkedProc;
					if (refinery != null && refinery.Owner == player && refinery.IsInWorld && !refinery.IsDead &&
						baseBuilder.SmartEconomyRefineryTypes.Contains(refinery.Info.Name))
					{
						if (provenHarvestTrees.Add(observation.TreeActorId))
							Log("{0} tick={1} harvest-round-trip tree={2} harvester={3}/{4}@{5} " +
							"refinery={6}/{7}@{8} loaded={9} remaining={10} unload-observed=true",
							player, world.WorldTick, observation.TreeActorId, harvesterActor.ActorID,
							harvesterActor.Info.Name, harvesterActor.Location, refinery.ActorID,
							refinery.Info.Name, refinery.Location, observation.LoadedAmount, contents);
						observation.TreeActorId = 0;
						observation.LoadedAmount = 0;
					}
				}

				observation.LastContents = contents;
			}

			foreach (var id in harvesterAccess.Keys.Where(id => !liveHarvesterIds.Contains(id)).ToArray())
				harvesterAccess.Remove(id);
		}

		void LogPlannedWait()
		{
			if ((project.Phase != ProjectPhase.Planned &&
				project.Phase != ProjectPhase.PlanningEnclosure &&
				project.Phase != ProjectPhase.PlanningExtension &&
				project.Phase != ProjectPhase.AwaitingRouteProof) ||
				!TiberiumFieldPolicy.DeadlineReached(world.WorldTick, project.NextWaitingLogTick))
				return;

			var obstructedCells = project.Phase == ProjectPhase.PlanningEnclosure && project.RedAnchorIndex >= 2 ?
				CountResourceObstructedSegmentCells() : 0;
			var reason = world.WorldTick < project.DeferredUntilTick ? "RetryBackoff" :
				obstructedCells > 0 ? "EnclosureCellsResourceObstructed" :
				project.Phase == ProjectPhase.AwaitingRouteProof ?
					(project.RouteHarvesterActorId == 0 ? "NoBidirectionalGatePath" :
						project.OrdinaryRouteProven ? "ReservedStealthGateCrossingPending" :
						"LiveOrdinaryRoundTripPending") :
				project.Phase == ProjectPhase.PlanningExtension ? "ExtensionQueueOrPlacementUnavailable" :
				project.LastQueueOfferTick < project.PlannedTick ? "NoEligibleIdleQueue" : "QueueOrAdmissionPreempted";
			project.NextWaitingLogTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
				baseBuilder.Info.TiberiumFieldProgressLogInterval);
			Log("{0} tick={1} project-waiting tree={2}/{3}@{4} phase={5} reason={6} " +
				"planned={7} last-queue-offer={8} deferred-until={9} obstructed-cells={10}", player, world.WorldTick,
				project.TreeActorId, project.TreeType, project.TreeLocation, project.Phase, reason,
				project.PlannedTick, project.LastQueueOfferTick, project.DeferredUntilTick, obstructedCells);
		}

		CPos? ChooseNextRedWallCell(ActorInfo wallInfo)
		{
			var segment = project.RedWallSegments[project.RedSegmentIndex];
			var wallBuilding = wallInfo.TraitInfo<BuildingInfo>();
			if (project.RedAnchorIndex == 0 && HasOwnWall(segment[0]))
				project.RedAnchorIndex = 1;
			if (project.RedAnchorIndex == 1 && HasOwnWall(segment[segment.Length - 1]))
				project.RedAnchorIndex = 2;

			IEnumerable<CPos> candidates = project.RedAnchorIndex == 0 ? new[] { segment[0] } :
				project.RedAnchorIndex == 1 ? new[] { segment[segment.Length - 1] } :
				segment.Where(c => !HasOwnWall(c));
			return candidates.Where(c => world.CanPlaceBuilding(c, wallInfo, wallBuilding, null) &&
				wallBuilding.IsCloseEnoughToBase(world, player, wallInfo, c))
				.Select(c => (CPos?)c).FirstOrDefault();
		}

		int CountResourceObstructedSegmentCells()
		{
			return resourceLayer == null ? 0 : project.RedWallSegments[project.RedSegmentIndex]
				.Count(c => !HasOwnWall(c) && resourceLayer.GetResource(c).Type != null);
		}

		bool HasOwnWall(CPos cell)
		{
			return world.ActorMap.GetActorsAt(cell).Any(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
				baseBuilder.Info.TiberiumFieldWallTypes.Contains(a.Info.Name));
		}

		bool RedSegmentComplete(IEnumerable<CPos> cells)
		{
			var ownedWalls = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
					baseBuilder.Info.TiberiumFieldWallTypes.Contains(a.Info.Name))
				.Select(a => a.Location).ToHashSet();
			return cells.All(ownedWalls.Contains);
		}

		bool RedEnclosureComplete()
		{
			return RedEnclosureComplete(project);
		}

		bool RedEnclosureComplete(FieldProject fieldProject)
		{
			return fieldProject.RedWallCells != null && RedSegmentComplete(fieldProject.RedWallCells) &&
				fieldProject.RedGateCells.All(g => !world.ActorsHavingTrait<Building>().Any(a =>
					a.Owner == player && a.IsInWorld && !a.IsDead &&
					baseBuilder.Info.TiberiumFieldWallTypes.Contains(a.Info.Name) && a.Location == g));
		}

		CPos? ChooseResonatorSite(Actor tree, ActorInfo resonatorInfo, BuildingInfo buildingInfo)
		{
			var range = resonatorInfo.TraitInfo<ModifiesResourcesInfo>().Range;
			var cells = WDist.ToCells(range);
			foreach (var cell in world.Map.FindTilesInCircle(tree.Location, cells)
				.OrderBy(c => (c - tree.Location).LengthSquared)
				.ThenBy(c => c.Y).ThenBy(c => c.X))
				if (IsPotentialResonatorSite(cell, resonatorInfo, buildingInfo, tree))
					return cell;

			return null;
		}

		bool IsLegalResonatorSite(CPos cell, ActorInfo resonatorInfo, BuildingInfo buildingInfo, Actor tree)
		{
			return IsPotentialResonatorSite(cell, resonatorInfo, buildingInfo, tree) &&
				buildingInfo.IsCloseEnoughToBase(world, player, resonatorInfo, cell);
		}

		bool IsPotentialResonatorSite(CPos cell, ActorInfo resonatorInfo, BuildingInfo buildingInfo, Actor tree)
		{
			if (tree == null || !tree.IsInWorld || tree.IsDead ||
				!world.CanPlaceBuilding(cell, resonatorInfo, buildingInfo, null))
				return false;

			if (resourceLayer != null && buildingInfo.Tiles(cell)
				.Any(c => resourceLayer.GetResource(c).Type != null))
				return false;

			var center = world.Map.CenterOfCell(cell) + buildingInfo.CenterOffset(world);
			return (center - tree.CenterPosition).HorizontalLengthSquared <=
				resonatorInfo.TraitInfo<ModifiesResourcesInfo>().Range.LengthSquared;
		}

		bool ProjectPartsWithinBuildArea(Actor tree)
		{
			var resonatorInfo = world.Map.Rules.Actors[project.ResonatorType];
			var resonatorBuilding = resonatorInfo.TraitInfo<BuildingInfo>();
			if (!IsLegalResonatorSite(project.ResonatorLocation, resonatorInfo, resonatorBuilding, tree))
				return false;

			if (project.RedWallCells == null)
				return true;

			var walls = baseBuilder.Info.TiberiumFieldWallTypes
				.Where(world.Map.Rules.Actors.ContainsKey)
				.Select(t => world.Map.Rules.Actors[t])
				.Where(a => a.TraitInfoOrDefault<BuildingInfo>() != null)
				.ToArray();
			return walls.Length > 0 && project.RedWallCells.Where(c => !HasOwnWall(c)).All(cell => walls.Any(w =>
				w.TraitInfo<BuildingInfo>().IsCloseEnoughToBase(world, player, w, cell)));
		}

		TiberiumFieldExtensionCandidate? ChooseExtensionCell(ActorInfo powerInfo)
		{
			var buildingInfo = powerInfo.TraitInfoOrDefault<BuildingInfo>();
			if (buildingInfo == null)
				return null;

			var target = project.ResonatorLocation;
			var anchors = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead)
				.OrderBy(a => (a.Location - target).LengthSquared)
				.ThenBy(a => a.ActorID).Take(8).ToArray();
			if (anchors.Length == 0)
				return null;

			var beforeDistance = Math.Sqrt(anchors.Min(a => (a.Location - target).LengthSquared));
			var searchRadius = Math.Max(2, baseBuilder.Info.TiberiumFieldExtensionStep + 2);
			var seen = new HashSet<CPos>();
			var candidates = new List<TiberiumFieldExtensionCandidate>();
			foreach (var anchor in anchors)
				foreach (var cell in world.Map.FindTilesInCircle(anchor.Location, searchRadius))
				{
					if (!seen.Add(cell) || !IsLegalExtensionCell(cell, powerInfo, buildingInfo))
						continue;

					var distanceSquared = (cell - target).LengthSquared;
					var progress = (int)Math.Round(beforeDistance - Math.Sqrt(distanceSquared));
					if (progress > 0)
						candidates.Add(new TiberiumFieldExtensionCandidate(cell, progress, distanceSquared));
				}

			return TiberiumFieldPolicy.BestExtensionCell(candidates,
				baseBuilder.Info.TiberiumFieldExtensionStep);
		}

		bool IsLegalExtensionCell(CPos cell, ActorInfo powerInfo, BuildingInfo buildingInfo)
		{
			return world.CanPlaceBuilding(cell, powerInfo, buildingInfo, null) &&
				buildingInfo.IsCloseEnoughToBase(world, player, powerInfo, cell) &&
				(resourceLayer == null || buildingInfo.Tiles(cell)
					.All(c => resourceLayer.GetResource(c).Type == null));
		}

		int CountUsefulResourceCells(CPos treeLocation)
		{
			if (resourceLayer == null)
				return 0;

			return world.Map.FindTilesInCircle(treeLocation,
				Math.Max(1, baseBuilder.Info.TiberiumFieldDemandRadius))
				.Count(c => resourceLayer.GetResource(c).Type != null);
		}

		int NearestOwnedBuildingDistanceSquared(CPos location)
		{
			return world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead)
				.Select(a => (a.Location - location).LengthSquared)
				.DefaultIfEmpty(int.MaxValue).Min();
		}

		void RetryProject(string reason)
		{
			var extensionWork = project.Phase == ProjectPhase.PlanningExtension ||
				baseBuilder.Info.TiberiumFieldPowerTypes.Contains(project.ActiveActorType);
			var enclosureWork = project.RedWallSegments != null &&
				project.RedSegmentIndex < project.RedWallSegments.Length &&
				(project.Phase == ProjectPhase.PlanningEnclosure ||
					project.Phase == ProjectPhase.AwaitingEnclosure ||
					baseBuilder.Info.TiberiumFieldWallTypes.Contains(project.ActiveActorType));
			project.RetryCount++;
			project.QueueActorId = 0;
			project.ActiveActorType = null;
			project.RedTargetCell = null;
			project.ExtensionTargetCell = null;
			project.Phase = extensionWork ? ProjectPhase.PlanningExtension :
				enclosureWork ? ProjectPhase.PlanningEnclosure : ProjectPhase.Planned;
			if (project.RetryCount >= Math.Max(1, baseBuilder.Info.TiberiumFieldMaximumRetries))
			{
				project.RetryCount = 0;
				project.DeferredUntilTick = TiberiumFieldPolicy.NextDeadline(world.WorldTick,
					baseBuilder.Info.TiberiumFieldRetryDelay);
			}

			Log("{0} tick={1} retry tree={2}/{3}@{4} phase={5} resonator={6}@{7} " +
				"reason={8} retry={9}/{10} deferred-until={11}", player, world.WorldTick,
				project.TreeActorId, project.TreeType, project.TreeLocation, project.Phase,
				project.ResonatorType, project.ResonatorLocation, reason, project.RetryCount,
				Math.Max(1, baseBuilder.Info.TiberiumFieldMaximumRetries), project.DeferredUntilTick);
		}

		static MiniYamlNode SaveValue(string key, object value)
		{
			return new MiniYamlNode(key, FieldSaver.FormatValue(value));
		}

		static MiniYamlNode SaveCells(string key, IEnumerable<CPos> cells)
		{
			return new MiniYamlNode(key, new MiniYaml("", cells
				.Select(c => SaveValue("Cell", c)).ToList()));
		}

		static MiniYamlNode SaveSegments(string key, IEnumerable<CPos[]> segments)
		{
			return new MiniYamlNode(key, new MiniYaml("", segments.Select(segment =>
				new MiniYamlNode("Segment", new MiniYaml("", segment
					.Select(c => SaveValue("Cell", c)).ToList()))).ToList()));
		}

		MiniYamlNode SaveProject(FieldProject value)
		{
			var nodes = new List<MiniYamlNode>
			{
				SaveValue("TreeActorId", value.TreeActorId),
				SaveValue("TreeType", value.TreeType),
				SaveValue("TreeLocation", value.TreeLocation),
				SaveValue("ResonatorType", value.ResonatorType),
				SaveValue("ResonatorLocation", value.ResonatorLocation),
				SaveValue("QueueActorId", value.QueueActorId),
				SaveValue("Phase", (int)value.Phase),
				SaveValue("DeadlineTick", value.DeadlineTick),
				SaveValue("RetryCount", value.RetryCount),
				SaveValue("DeferredUntilTick", value.DeferredUntilTick),
				SaveValue("PlannedTick", value.PlannedTick),
				SaveValue("LastQueueOfferTick", value.LastQueueOfferTick),
				SaveValue("NextWaitingLogTick", value.NextWaitingLogTick),
				SaveValue("NextProgressCheckTick", value.NextProgressCheckTick),
				SaveValue("NoProgressDeferralCount", value.NoProgressDeferralCount),
				SaveValue("RedSegmentIndex", value.RedSegmentIndex),
				SaveValue("RedAnchorIndex", value.RedAnchorIndex),
				SaveValue("HasRedTargetCell", value.RedTargetCell.HasValue),
				SaveValue("RedTargetCell", value.RedTargetCell ?? CPos.Zero),
				SaveValue("ActiveActorType", value.ActiveActorType ?? ""),
				SaveValue("MaintenanceOnly", value.MaintenanceOnly),
				SaveValue("HasExtensionTargetCell", value.ExtensionTargetCell.HasValue),
				SaveValue("ExtensionTargetCell", value.ExtensionTargetCell ?? CPos.Zero),
				SaveValue("ExtensionCount", value.ExtensionCount),
				SaveValue("ExtensionProgressCells", value.ExtensionProgressCells),
				SaveValue("RouteHarvesterActorId", value.RouteHarvesterActorId),
				SaveValue("RouteRefineryActorId", value.RouteRefineryActorId),
				SaveValue("RouteResourceCell", value.RouteResourceCell),
				SaveValue("RouteStage", (int)value.RouteStage),
				SaveValue("RouteLastContents", value.RouteLastContents),
				SaveValue("OrdinaryRouteProven", value.OrdinaryRouteProven),
				SaveValue("StealthRouteProven", value.StealthRouteProven)
			};
			if (value.RedWallCells != null)
			{
				nodes.Add(SaveCells("RedWallCells", value.RedWallCells));
				nodes.Add(SaveCells("RedGateCells", value.RedGateCells));
				nodes.Add(SaveSegments("RedWallSegments", value.RedWallSegments));
			}

			return new MiniYamlNode("Project", new MiniYaml("", nodes));
		}

		MiniYamlNode SaveActiveEnclosure(ActiveRedEnclosure value)
		{
			return new MiniYamlNode("ActiveRedEnclosure", new MiniYaml("", new List<MiniYamlNode>
			{
				SaveValue("TreeActorId", value.TreeActorId),
				SaveValue("TreeType", value.TreeType),
				SaveValue("TreeLocation", value.TreeLocation),
				SaveValue("ResonatorActorId", value.ResonatorActorId),
				SaveValue("ResonatorType", value.ResonatorType),
				SaveValue("ResonatorLocation", value.ResonatorLocation),
				SaveValue("NextMaintenanceTick", value.NextMaintenanceTick),
				SaveCells("WallCells", value.WallCells),
				SaveCells("GateCells", value.GateCells),
				SaveSegments("WallSegments", value.WallSegments)
			}));
		}

		FieldProject LoadProject(MiniYamlNode node)
		{
			var nodes = node.Value.Nodes;
			var phase = ReadValue(nodes, "Phase", -1);
			if (!Enum.IsDefined(typeof(ProjectPhase), phase))
				throw new InvalidOperationException($"unknown project phase {phase}");

			var loaded = new FieldProject
			{
				TreeActorId = ReadValue<uint>(nodes, "TreeActorId"),
				TreeType = ReadValue<string>(nodes, "TreeType"),
				TreeLocation = ReadValue<CPos>(nodes, "TreeLocation"),
				ResonatorType = ReadValue<string>(nodes, "ResonatorType"),
				ResonatorLocation = ReadValue<CPos>(nodes, "ResonatorLocation"),
				QueueActorId = ReadValue<uint>(nodes, "QueueActorId"),
				Phase = (ProjectPhase)phase,
				DeadlineTick = ReadValue<int>(nodes, "DeadlineTick"),
				RetryCount = ReadValue<int>(nodes, "RetryCount"),
				DeferredUntilTick = ReadValue<int>(nodes, "DeferredUntilTick"),
				PlannedTick = ReadValue<int>(nodes, "PlannedTick"),
				LastQueueOfferTick = ReadValue<int>(nodes, "LastQueueOfferTick"),
				NextWaitingLogTick = ReadValue<int>(nodes, "NextWaitingLogTick"),
				NextProgressCheckTick = ReadValue(nodes, "NextProgressCheckTick",
					TiberiumFieldPolicy.NextDeadline(world.WorldTick,
						baseBuilder.Info.TiberiumFieldMaintenanceInterval)),
				NoProgressDeferralCount = ReadValue(nodes, "NoProgressDeferralCount", 0),
				RedSegmentIndex = ReadValue<int>(nodes, "RedSegmentIndex"),
				RedAnchorIndex = ReadValue<int>(nodes, "RedAnchorIndex"),
				ActiveActorType = ReadValue(nodes, "ActiveActorType", ""),
				MaintenanceOnly = ReadValue(nodes, "MaintenanceOnly", false),
				ExtensionCount = ReadValue(nodes, "ExtensionCount", 0),
				ExtensionProgressCells = ReadValue(nodes, "ExtensionProgressCells", 0),
				RouteHarvesterActorId = ReadValue<uint>(nodes, "RouteHarvesterActorId"),
				RouteRefineryActorId = ReadValue<uint>(nodes, "RouteRefineryActorId"),
				RouteResourceCell = ReadValue(nodes, "RouteResourceCell", default(CPos)),
				RouteLastContents = ReadValue(nodes, "RouteLastContents", 0),
				OrdinaryRouteProven = ReadValue(nodes, "OrdinaryRouteProven", false),
				StealthRouteProven = ReadValue(nodes, "StealthRouteProven", false)
			};
			var routeStage = ReadValue(nodes, "RouteStage", 0);
			if (!Enum.IsDefined(typeof(TiberiumFieldRoundTripStage), routeStage))
				throw new InvalidOperationException($"unknown route stage {routeStage}");
			loaded.RouteStage = (TiberiumFieldRoundTripStage)routeStage;
			if (ReadValue(nodes, "HasRedTargetCell", false))
				loaded.RedTargetCell = ReadValue<CPos>(nodes, "RedTargetCell");
			if (ReadValue(nodes, "HasExtensionTargetCell", false))
			{
				loaded.ExtensionTargetCell = ReadValue<CPos>(nodes, "ExtensionTargetCell");
				if (!world.Map.Contains(loaded.ExtensionTargetCell.Value))
					throw new InvalidOperationException($"extension target {loaded.ExtensionTargetCell} is outside the map");
			}

			var wallNode = nodes.FirstOrDefault(n => n.Key == "RedWallCells");
			if (wallNode != null)
			{
				loaded.RedWallCells = ReadCells(nodes, "RedWallCells");
				loaded.RedGateCells = ReadCells(nodes, "RedGateCells");
				loaded.RedWallSegments = ReadSegments(nodes, "RedWallSegments");
				ValidatePerimeter(loaded.RedWallCells, loaded.RedGateCells, loaded.RedWallSegments);
				var activationEligible = (loaded.Phase == ProjectPhase.Planned ||
					loaded.Phase == ProjectPhase.AwaitingRouteProof) && !loaded.MaintenanceOnly;
				var enclosureComplete = loaded.RedSegmentIndex == loaded.RedWallSegments.Length &&
					RedEnclosureComplete(loaded);
				if (!TiberiumFieldPolicy.IsValidSavedSegmentCursor(loaded.RedSegmentIndex,
					loaded.RedWallSegments.Length, activationEligible, enclosureComplete))
					throw new InvalidOperationException($"segment cursor {loaded.RedSegmentIndex}/" +
						$"{loaded.RedWallSegments.Length} is incompatible with phase {loaded.Phase}, " +
						$"maintenance={loaded.MaintenanceOnly}, enclosure-complete={enclosureComplete}");
				if (loaded.OrdinaryRouteProven && loaded.RouteStage != TiberiumFieldRoundTripStage.Complete)
					throw new InvalidOperationException("completed ordinary route proof has an incomplete route stage");
				if (loaded.Phase == ProjectPhase.AwaitingRouteProof && !enclosureComplete)
					throw new InvalidOperationException("route proof phase requires a complete live enclosure");
				if (loaded.Phase == ProjectPhase.Planned && loaded.RedSegmentIndex == loaded.RedWallSegments.Length &&
					!loaded.MaintenanceOnly && !loaded.OrdinaryRouteProven)
					loaded.Phase = ProjectPhase.AwaitingRouteProof;
			}
			else if (loaded.RouteHarvesterActorId != 0 || loaded.RouteRefineryActorId != 0 ||
				loaded.OrdinaryRouteProven || loaded.StealthRouteProven)
				throw new InvalidOperationException("non-red project contains red gate route state");

			ValidateIdentity(loaded.TreeActorId, loaded.TreeType, loaded.TreeLocation,
				loaded.ResonatorType, loaded.ResonatorLocation);
			if (loaded.RedWallCells != null)
				ValidateExpectedPerimeter(loaded.TreeLocation, loaded.ResonatorType,
					loaded.ResonatorLocation, loaded.RedWallCells, loaded.RedGateCells,
					loaded.RedWallSegments);
			ValidateRouteActors(loaded);
			return loaded;
		}

		ActiveRedEnclosure LoadActiveEnclosure(MiniYamlNode node)
		{
			var nodes = node.Value.Nodes;
			var loaded = new ActiveRedEnclosure
			{
				TreeActorId = ReadValue<uint>(nodes, "TreeActorId"),
				TreeType = ReadValue<string>(nodes, "TreeType"),
				TreeLocation = ReadValue<CPos>(nodes, "TreeLocation"),
				ResonatorActorId = ReadValue<uint>(nodes, "ResonatorActorId"),
				ResonatorType = ReadValue<string>(nodes, "ResonatorType"),
				ResonatorLocation = ReadValue<CPos>(nodes, "ResonatorLocation"),
				NextMaintenanceTick = ReadValue<int>(nodes, "NextMaintenanceTick"),
				WallCells = ReadCells(nodes, "WallCells"),
				GateCells = ReadCells(nodes, "GateCells"),
				WallSegments = ReadSegments(nodes, "WallSegments")
			};
			ValidateIdentity(loaded.TreeActorId, loaded.TreeType, loaded.TreeLocation,
				loaded.ResonatorType, loaded.ResonatorLocation);
			ValidatePerimeter(loaded.WallCells, loaded.GateCells, loaded.WallSegments);
			ValidateExpectedPerimeter(loaded.TreeLocation, loaded.ResonatorType,
				loaded.ResonatorLocation, loaded.WallCells, loaded.GateCells,
				loaded.WallSegments);
			var resonator = world.GetActorById(loaded.ResonatorActorId);
			if (resonator == null || !resonator.IsInWorld || resonator.IsDead ||
				resonator.Owner != player || resonator.Info.Name != loaded.ResonatorType ||
				resonator.Location != loaded.ResonatorLocation)
				throw new InvalidOperationException($"active resonator {loaded.ResonatorActorId}/{loaded.ResonatorType} is invalid");

			return loaded;
		}

		void ValidateIdentity(uint treeActorId, string treeType, CPos treeLocation,
			string resonatorType, CPos resonatorLocation)
		{
			var tree = world.GetActorById(treeActorId);
			if (tree == null || !tree.IsInWorld || tree.IsDead || tree.Info.Name != treeType ||
				!baseBuilder.Info.TiberiumFieldTreeTypes.Contains(treeType))
				throw new InvalidOperationException($"tree {treeActorId}/{treeType} is invalid");
			if (!baseBuilder.Info.TiberiumFieldResonatorTypes.Contains(resonatorType) ||
				!world.Map.Rules.Actors.TryGetValue(resonatorType, out var resonatorInfo))
				throw new InvalidOperationException($"resonator type {resonatorType} is not configured");

			var buildingInfo = resonatorInfo.TraitInfoOrDefault<BuildingInfo>();
			var modifierInfo = resonatorInfo.TraitInfoOrDefault<ModifiesResourcesInfo>();
			if (buildingInfo == null || modifierInfo == null)
				throw new InvalidOperationException($"resonator type {resonatorType} lacks required traits");

			var footprint = buildingInfo.Tiles(resonatorLocation).ToArray();
			if (footprint.Any(c => !world.Map.Contains(c)))
				throw new InvalidOperationException($"saved resonator footprint {resonatorType}@{resonatorLocation} is outside the map");

			var center = world.Map.CenterOfCell(resonatorLocation) + buildingInfo.CenterOffset(world);
			var effectDistanceSquared = (center - tree.CenterPosition).HorizontalLengthSquared;
			if (!TiberiumFieldPolicy.IsValidSavedSpatialIdentity(treeLocation, tree.Location,
				footprint, world.Map.Contains,
				effectDistanceSquared, modifierInfo.Range.LengthSquared))
				throw new InvalidOperationException($"saved spatial identity tree {treeActorId}@{treeLocation} " +
					$"resonator {resonatorType}@{resonatorLocation} is invalid");
		}

		void ValidateRouteActors(FieldProject loaded)
		{
			if (loaded.RouteHarvesterActorId == 0 && loaded.RouteRefineryActorId == 0)
			{
				if (loaded.RouteStage != TiberiumFieldRoundTripStage.AwaitingRefinery ||
					loaded.OrdinaryRouteProven)
					throw new InvalidOperationException("saved route progress is missing its harvester or refinery");

				return;
			}

			var harvester = world.GetActorById(loaded.RouteHarvesterActorId);
			var refinery = world.GetActorById(loaded.RouteRefineryActorId);
			if (harvester == null || harvester.Owner != player || !harvester.IsInWorld || harvester.IsDead ||
				harvester.TraitOrDefault<Harvester>() == null ||
				refinery == null || refinery.Owner != player || !refinery.IsInWorld || refinery.IsDead ||
				refinery.TraitOrDefault<IAcceptResources>() == null ||
				!world.Map.Contains(loaded.RouteResourceCell) || loaded.RedWallCells == null ||
				TiberiumFieldPolicy.RouteZone(loaded.RouteResourceCell, loaded.RedWallCells,
					loaded.RedGateCells) != TiberiumFieldRouteZone.Inside)
				throw new InvalidOperationException("saved harvester gate route identity is invalid");
		}

		void ValidatePerimeter(CPos[] walls, CPos[] gates, CPos[][] segments)
		{
			if (!TiberiumFieldPolicy.IsValidSavedPerimeter(walls, gates, segments) ||
				walls.Concat(gates).Any(c => !world.Map.Contains(c)))
				throw new InvalidOperationException("saved red perimeter or gate is invalid");
		}

		void ValidateExpectedPerimeter(CPos treeLocation, string resonatorType,
			CPos resonatorLocation, CPos[] walls, CPos[] gates, CPos[][] segments)
		{
			var buildingInfo = world.Map.Rules.Actors[resonatorType].TraitInfo<BuildingInfo>();
			var expected = TiberiumFieldPolicy.PlanRedPerimeter(treeLocation,
				buildingInfo.Tiles(resonatorLocation), gates[0],
				baseBuilder.Info.TiberiumFieldPerimeterStandoff);
			if (!TiberiumFieldPolicy.SavedPerimeterMatchesPlan(expected, walls, gates, segments))
				throw new InvalidOperationException("saved red perimeter does not match the configured field geometry");
		}

		static CPos[] ReadCells(IEnumerable<MiniYamlNode> nodes, string key)
		{
			var node = nodes.FirstOrDefault(n => n.Key == key) ??
				throw new InvalidOperationException($"missing saved cell list {key}");
			return node.Value.Nodes.Where(n => n.Key == "Cell")
				.Select(n => FieldLoader.GetValue<CPos>(key, n.Value.Value)).ToArray();
		}

		static CPos[][] ReadSegments(IEnumerable<MiniYamlNode> nodes, string key)
		{
			var node = nodes.FirstOrDefault(n => n.Key == key) ??
				throw new InvalidOperationException($"missing saved segment list {key}");
			return node.Value.Nodes.Where(n => n.Key == "Segment").Select(segment =>
				segment.Value.Nodes.Where(n => n.Key == "Cell")
					.Select(n => FieldLoader.GetValue<CPos>(key, n.Value.Value)).ToArray()).ToArray();
		}

		static T ReadValue<T>(IEnumerable<MiniYamlNode> nodes, string key, T fallback = default)
		{
			var node = nodes.FirstOrDefault(n => n.Key == key);
			return node == null ? fallback : FieldLoader.GetValue<T>(key, node.Value.Value);
		}

		void Log(string format, params object[] args)
		{
			AIUtils.BotDebug(format, args);
			if (baseBuilder.Info.TiberiumFieldDebugLogging)
				OpenRA.Log.Write("debug", "AI tiberium field: " + format, args);
		}
	}
}
