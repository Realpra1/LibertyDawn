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
	/// Owns the one SkyNet attacked-tower cluster: attack identity, serialized queue reservation,
	/// local placement, repair support, and minimum causal wall sales. All scans are cluster-local
	/// or bounded actor lists and all ordering has an explicit deterministic tie-break.
	/// </summary>
	class BaseBuilderDefenseClusterManager
	{
		sealed class Reservation
		{
			public uint QueueActorId;
			public string ActorType;
			public int Tick;
			public bool RepairFacility;
		}

		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		readonly PowerManager playerPower;
		readonly DefenseClusterPolicyState state = new DefenseClusterPolicyState();
		readonly DefenseClusterRepairSiteState repairSiteState = new DefenseClusterRepairSiteState();
		readonly HashSet<CPos> legacyClusterWallCells = new HashSet<CPos>();
		readonly bool enabled;
		readonly BuildingInfluence buildingInfluence;
		readonly IResourceLayer resourceLayer;
		readonly Locomotor locomotor;
		Reservation reservation;
		int nextMaintenanceTick;
		int nextRetryTick;
		int nextStatusLogTick;
		int nextRepairDiagnosticTick;
		int nextRepairRecoveryDiagnosticTick;
		int ordinaryDefenseExpiryTick;
		uint ordinaryDefenseActorId;
		CPos ordinaryDefenseCenter;
		CPos lastEnemyLocation;
		bool completionLogged;
		bool repairRecoveryPending;
		bool repairRecoveryHandoffUsed;

		public BaseBuilderDefenseClusterManager(BaseBuilderBotModule baseBuilder, Player player,
			PowerManager playerPower)
		{
			this.baseBuilder = baseBuilder;
			this.player = player;
			this.playerPower = playerPower;
			world = player.World;
			enabled = baseBuilder.Info.EnableDefenseClusterPolicy;
			if (!enabled)
				return;

			buildingInfluence = world.WorldActor.TraitOrDefault<BuildingInfluence>();
			resourceLayer = world.WorldActor.TraitOrDefault<IResourceLayer>();
			locomotor = world.WorldActor.TraitsImplementing<Locomotor>()
				.FirstOrDefault(l => l.Info.Name == baseBuilder.Info.WallPathCheckLocomotor);
		}

		BaseBuilderBotModuleInfo Info => baseBuilder.Info;
		public bool Enabled => enabled;
		public uint AnchorActorId => state.AnchorActorId;
		public Actor ActiveAnchor => ValidTower(state.AnchorActorId);
		public CPos EnemyLocation => lastEnemyLocation;
		public CPos ScreenEnemyLocation => repairSiteState.Matches(state.AnchorActorId, null) ?
			repairSiteState.EnemyLocation : lastEnemyLocation;

		public void ObserveAttack(Actor self, AttackInfo attack)
		{
			if (!enabled || self == null || attack?.Attacker == null || attack.Damage.Value <= 0 ||
				self.Owner != player || !self.IsInWorld || self.IsDead || attack.Attacker.Disposed ||
				attack.Attacker.IsDead || player.RelationshipWith(attack.Attacker.Owner) != PlayerRelationship.Enemy)
				return;

			if (Info.DefenseClusterTowerTypes.Contains(self.Info.Name))
			{
				var activeAnchor = ActiveAnchor;
				if (activeAnchor != null && self.ActorID != activeAnchor.ActorID &&
					DefenseClusterPolicy.IsWithinCluster(activeAnchor.Location, self.Location,
						Info.DefenseClusterRadius))
				{
					state.ObserveActiveClusterPressure(world.WorldTick);
					lastEnemyLocation = attack.Attacker.Location;
					return;
				}

				var oldAnchor = state.AnchorActorId;
				var changed = state.ObserveTowerAttack(self.ActorID, world.WorldTick,
					Info.DefenseClusterAnchorLease, MinimumClusterComplete());
				if (state.AnchorActorId == self.ActorID)
					lastEnemyLocation = attack.Attacker.Location;
				if (changed)
				{
					completionLogged = false;
					nextRetryTick = 0;
					ClearProtectedRepairSite("anchor-change");
					RememberLegacyClusterWalls(ActiveAnchor);
					Log("anchor {0}->{1} attacked={2}#{3} cell={4} attacker={5}#{6} damage={7}",
						oldAnchor, state.AnchorActorId, self.Info.Name, self.ActorID, self.Location,
						attack.Attacker.Info.Name, attack.Attacker.ActorID, attack.Damage.Value);
				}
				else if (state.PendingActorId == self.ActorID)
					Log("pending anchor={0} candidate={1} tick={2} lease-until={3}",
						state.AnchorActorId, state.PendingActorId, state.PendingAttackTick, state.AnchorLeaseUntilTick);
			}
			else if (self.Info.HasTraitInfo<BuildingInfo>())
			{
				if (ordinaryDefenseExpiryTick > world.WorldTick && ordinaryDefenseActorId != self.ActorID)
					return;

				var changed = ordinaryDefenseActorId != self.ActorID || ordinaryDefenseExpiryTick <= world.WorldTick;
				ordinaryDefenseActorId = self.ActorID;
				ordinaryDefenseCenter = self.Location;
				ordinaryDefenseExpiryTick = world.WorldTick + Info.DefenseClusterOrdinaryDefenseLease;
				if (changed)
					Log("ordinary-defense-center building={0}#{1} cell={2} expires={3}",
						self.Info.Name, self.ActorID, self.Location, ordinaryDefenseExpiryTick);
			}
		}

		public bool TryTakeOrdinaryDefenseCenter(out CPos center)
		{
			center = default;
			if (!enabled || ordinaryDefenseExpiryTick <= world.WorldTick)
				return false;

			center = ordinaryDefenseCenter;
			ordinaryDefenseExpiryTick = 0;
			ordinaryDefenseActorId = 0;
			Log("ordinary-defense-center consumed cell={0}", center);
			return true;
		}

		public void Tick(IBot bot)
		{
			if (!enabled || world.IsReplay || world.WorldTick < nextMaintenanceTick)
				return;

			nextMaintenanceTick = world.WorldTick + Info.DefenseClusterMaintenanceInterval;
			RefreshReservation();
			var anchor = ActiveAnchor;
			if (state.HasAnchor && anchor == null)
			{
				InvalidateAnchor("dead/captured/missing");
				anchor = ActiveAnchor;
			}

			if (anchor == null)
				return;

			var liveRepair = LocalRepairFacility(anchor);
			if (liveRepair != null && repairRecoveryPending)
			{
				ClearRepairRecovery();
				Log("repair-recovery-complete actor={0}#{1} cell={2}", liveRepair.Info.Name,
					liveRepair.ActorID, liveRepair.Location);
			}

			if (state.HasPending && ValidTower(state.PendingActorId) == null)
				state.Restore(state.AnchorActorId, state.AnchorAttackTick, state.AnchorLeaseUntilTick,
					0, 0, state.PlacementFailures);

			if (state.TryPromotePending(world.WorldTick, Info.DefenseClusterAnchorLease, MinimumClusterComplete()))
			{
				completionLogged = false;
				ClearProtectedRepairSite("pending-promotion");
				RememberLegacyClusterWalls(ActiveAnchor);
				Log("promoted pending anchor={0}", state.AnchorActorId);
				anchor = ActiveAnchor;
			}

			var complete = MinimumClusterComplete();
			if (complete && !completionLogged)
			{
				completionLogged = true;
				var towers = LiveNearbyTowers(anchor).ToArray();
				Log("complete anchor={0}#{1} cell={2} towers={3} roles={4} repair={5}",
					anchor.Info.Name, anchor.ActorID, anchor.Location, towers.Length,
					DefenseClusterPolicy.CoveredRoles(towers.Where(IsOperationalTower).Select(RolesForActor)),
					LocalRepairFacility(anchor)?.ActorID ?? 0);
			}
			else if (!complete)
				completionLogged = false;

			if (!complete && world.WorldTick >= nextStatusLogTick)
			{
				nextStatusLogTick = world.WorldTick + Math.Max(Info.DefenseClusterMaintenanceInterval,
					Info.DefenseClusterRetryDelay);
				var towers = LiveNearbyTowers(anchor).ToArray();
				Log("status anchor={0} towers-live={1} towers-queued={2} roles-required={3} roles-operational={4} " +
					"repair-live={5} repair-queued={6} reservation={7} power={8}", anchor.ActorID, towers.Length,
					baseBuilder.CountQueuedOrPendingActors(Info.DefenseClusterTowerTypes), RequiredRoles(),
					DefenseClusterPolicy.CoveredRoles(towers.Where(IsOperationalTower).Select(RolesForActor)),
					LocalRepairFacility(anchor)?.ActorID ?? 0,
					baseBuilder.CountQueuedOrPendingActors(Info.DefenseClusterRepairFacilityTypes),
					reservation?.ActorType ?? "none", playerPower?.PowerState.ToString() ?? "unknown");
			}

			TrySellCausalWall(bot, anchor);
		}

		public ActorInfo TryChoosePriorityRepairFacility(ProductionQueue queue, IEnumerable<ActorInfo> buildables)
		{
			if (!enabled || queue == null || world.WorldTick < nextRetryTick ||
				(playerPower != null && playerPower.PowerState != PowerState.Normal))
				return null;

			var anchor = ActiveAnchor;
			var towers = anchor == null ? Array.Empty<Actor>() : LiveNearbyTowers(anchor).ToArray();
			var requiredRoles = RequiredRoles();
			var operationalRoles = DefenseClusterPolicy.CoveredRoles(
				towers.Where(IsOperationalTower).Select(RolesForActor));
			var repairReadyTowerCount = Math.Max(1, Info.DefenseClusterMinimumTowers - 1);
			if (anchor == null || towers.Length < repairReadyTowerCount ||
				(operationalRoles & requiredRoles) != requiredRoles || LocalRepairFacility(anchor) != null ||
				baseBuilder.CountQueuedOrPendingActors(Info.DefenseClusterRepairFacilityTypes) != 0)
				return null;

			RefreshReservation();
			if (reservation != null)
				return null;

			var stableProducer = PreferredRepairProducer(queue.Info.Type);
			if (!DefenseClusterPolicy.CanUseRepairProducer(repairRecoveryPending, queue.Actor.ActorID,
				stableProducer?.ActorID ?? 0))
				return null;

			var available = buildables.ToDictionary(a => a.Name);
			foreach (var facilityType in Info.DefenseClusterRepairFacilityTypes)
				if (available.TryGetValue(facilityType, out var facility) && CanCommit(facilityType))
					return Reserve(queue, facility, true);

			return null;
		}

		public ActorInfo TryChooseQueuedRepairRecovery(ProductionQueue queue,
			IEnumerable<ActorInfo> buildables, bool priorityRecoveryActive)
		{
			if (!enabled || queue == null || world.WorldTick < nextRetryTick || !repairRecoveryPending)
				return null;

			var stableProducer = PreferredRepairProducer(queue.Info.Type);
			if (stableProducer == null || queue.Actor.ActorID != stableProducer.ActorID)
				return null;

			var queuedItems = queue.AllQueued().Count();
			if (!DefenseClusterPolicy.CanQueueRepairRecovery(repairRecoveryPending,
				repairRecoveryHandoffUsed, priorityRecoveryActive, queuedItems))
			{
				var reason = repairRecoveryHandoffUsed ? "handoff-used" :
					priorityRecoveryActive ? RepairRecoveryPriorityReason() : $"queue-depth-{queuedItems}";
				LogRepairRecoveryBlocked(queue, queuedItems, reason);
				return null;
			}

			var anchor = ActiveAnchor;
			var towers = anchor == null ? Array.Empty<Actor>() : LiveNearbyTowers(anchor).ToArray();
			var requiredRoles = RequiredRoles();
			var operationalRoles = DefenseClusterPolicy.CoveredRoles(
				towers.Where(IsOperationalTower).Select(RolesForActor));
			var repairReadyTowerCount = Math.Max(1, Info.DefenseClusterMinimumTowers - 1);
			if (anchor == null || towers.Length < repairReadyTowerCount ||
				(operationalRoles & requiredRoles) != requiredRoles || LocalRepairFacility(anchor) != null ||
				baseBuilder.CountQueuedOrPendingActors(Info.DefenseClusterRepairFacilityTypes) != 0)
			{
				var reason = anchor == null ? "anchor-inactive" :
					towers.Length < repairReadyTowerCount ? "tower-count" :
					(operationalRoles & requiredRoles) != requiredRoles ? "roles-unready" :
					LocalRepairFacility(anchor) != null ? "repair-live" : "repair-queued";
				LogRepairRecoveryBlocked(queue, queuedItems, reason);
				return null;
			}

			RefreshReservation();
			if (reservation != null)
			{
				LogRepairRecoveryBlocked(queue, queuedItems, "reservation-active");
				return null;
			}

			var available = buildables.ToDictionary(a => a.Name);
			foreach (var facilityType in Info.DefenseClusterRepairFacilityTypes)
				if (available.TryGetValue(facilityType, out var facility) && CanCommit(facilityType))
				{
					repairRecoveryHandoffUsed = true;
					Log("repair-recovery-handoff actor={0} producer={1}#{2}@{3} queued-ahead=1",
						facility.Name, queue.Actor.Info.Name, queue.Actor.ActorID, queue.Actor.Location);
					return Reserve(queue, facility, true);
				}

			LogRepairRecoveryBlocked(queue, queuedItems, "facility-unavailable-or-limited");

			return null;
		}

		string RepairRecoveryPriorityReason()
		{
			if (baseBuilder.OpeningActive)
				return "opening";
			if (baseBuilder.SmartEconomySerializesMissingRefinery)
				return "missing-refinery";
			if (playerPower != null && playerPower.PowerState != PowerState.Normal)
				return $"power-{playerPower.PowerState}";

			return "power-reserve";
		}

		void LogRepairRecoveryBlocked(ProductionQueue queue, int queuedItems, string reason)
		{
			if (world.WorldTick < nextRepairRecoveryDiagnosticTick)
				return;

			nextRepairRecoveryDiagnosticTick = world.WorldTick + Math.Max(
				Info.DefenseClusterMaintenanceInterval, Info.DefenseClusterRetryDelay);
			var head = queue.AllQueued().FirstOrDefault();
			Log("repair-recovery-blocked producer={0}#{1}@{2} queue={3} queued={4} head={5} head-done={6} reason={7}",
				queue.Actor.Info.Name, queue.Actor.ActorID, queue.Actor.Location, queue.Info.Type, queuedItems,
				head?.Item ?? "none", head?.Done ?? false, reason);
		}

		public ActorInfo TryChooseBuilding(ProductionQueue queue, IEnumerable<ActorInfo> buildables)
		{
			if (!enabled || queue == null || world.WorldTick < nextRetryTick || ActiveAnchor == null ||
				(playerPower != null && playerPower.PowerState != PowerState.Normal))
				return null;

			RefreshReservation();
			if (reservation != null)
				return null;

			var available = buildables.ToDictionary(a => a.Name);
			var anchor = ActiveAnchor;
			var towers = LiveNearbyTowers(anchor).ToArray();
			var operationalRoles = DefenseClusterPolicy.CoveredRoles(towers.Where(IsOperationalTower).Select(RolesForActor));
			var requiredRoles = RequiredRoles();
			var committed = baseBuilder.CountQueuedOrPendingActors(Info.DefenseClusterTowerTypes);
			var type = DefenseClusterPolicy.ChooseMissingTower(Info.DefenseClusterTowerTypes,
				RolesForType, requiredRoles, operationalRoles, towers.Length, committed,
				Info.DefenseClusterMinimumTowers, t => available.ContainsKey(t) && CanCommit(t));
			if (type != null)
				return Reserve(queue, available[type], false);

			return null;
		}

		ActorInfo Reserve(ProductionQueue queue, ActorInfo actor, bool repairFacility)
		{
			reservation = new Reservation
			{
				QueueActorId = queue.Actor.ActorID,
				ActorType = actor.Name,
				Tick = world.WorldTick,
				RepairFacility = repairFacility
			};
			Log("reserved goal={0} actor={1} queue={2}#{3} producer={4}@{5} recovery={6}",
				repairFacility ? "repair" : "tower", actor.Name, queue.Info.Type, queue.Actor.ActorID,
				queue.Actor.Info.Name, queue.Actor.Location, repairFacility && repairRecoveryPending);
			return actor;
		}

		public bool OwnsPlacement(ProductionQueue queue, string actorType)
		{
			RefreshReservation();
			return enabled && reservation != null && queue != null &&
				reservation.QueueActorId == queue.Actor.ActorID && reservation.ActorType == actorType;
		}

		public CPos? ChooseLocation(ProductionQueue queue, ActorInfo actorInfo, BuildingInfo buildingInfo)
		{
			if (!OwnsPlacement(queue, actorInfo.Name))
				return null;

			var anchor = ActiveAnchor;
			if (anchor == null)
				return null;

			var cells = world.Map.FindTilesInAnnulus(anchor.Location,
				Info.DefenseClusterPlacementMinimumRadius, Info.DefenseClusterPlacementMaximumRadius);
			IEnumerable<CPos> ordered;
			if (reservation.RepairFacility)
			{
				var baseCenter = StableBaseCenter();
				ordered = cells.Where(c => (c - baseCenter).LengthSquared <= (anchor.Location - baseCenter).LengthSquared)
					.OrderBy(c => (c - baseCenter).LengthSquared).ThenBy(c => c.X).ThenBy(c => c.Y);
			}
			else
				ordered = cells.OrderBy(c => (c - lastEnemyLocation).LengthSquared).ThenBy(c => c.X).ThenBy(c => c.Y);

			if (reservation.RepairFacility && TryGetProtectedRepairSite(anchor, actorInfo.Name,
				out _, out _, out var protectedSite))
			{
				if (CanUseRepairSite(protectedSite, actorInfo, buildingInfo, anchor, out var rejectionReason))
				{
					Log("placement goal=repair actor={0} cell={1} anchor={2} protected=true",
						actorInfo.Name, protectedSite, anchor.ActorID);
					return protectedSite;
				}

				Log("repair-site-protected-unusable anchor={0} actor={1} site={2} reason={3}",
					anchor.ActorID, actorInfo.Name, protectedSite, rejectionReason);
			}

			var protectedRepairCells = reservation.RepairFacility ? new HashSet<CPos>() :
				PotentialRepairReservationCells(anchor);

			foreach (var cell in ordered)
			{
				if (!world.CanPlaceBuilding(cell, actorInfo, buildingInfo, null) ||
					!buildingInfo.IsCloseEnoughToBase(world, player, actorInfo, cell))
					continue;
				if (!reservation.RepairFacility && buildingInfo.Tiles(cell).Any(protectedRepairCells.Contains))
					continue;

				if (reservation.RepairFacility && !CanUseRepairSite(cell, actorInfo, buildingInfo, anchor))
					continue;
				if (reservation.RepairFacility)
					ProtectRepairSite(anchor, actorInfo, cell);

				Log("placement goal={0} actor={1} cell={2} anchor={3}",
					reservation.RepairFacility ? "repair" : "tower", actorInfo.Name, cell, anchor.ActorID);
				return cell;
			}

			return null;
		}

		bool CanUseRepairSite(CPos cell, ActorInfo actorInfo, BuildingInfo buildingInfo, Actor anchor)
		{
			return CanUseRepairSite(cell, actorInfo, buildingInfo, anchor, out _);
		}

		bool CanUseRepairSite(CPos cell, ActorInfo actorInfo, BuildingInfo buildingInfo, Actor anchor,
			out string rejectionReason)
		{
			if (!world.CanPlaceBuilding(cell, actorInfo, buildingInfo, null))
			{
				rejectionReason = "placement";
				return false;
			}

			if (!buildingInfo.IsCloseEnoughToBase(world, player, actorInfo, cell))
			{
				rejectionReason = "adjacency";
				return false;
			}

			if (!WouldCoverAnchor(cell, actorInfo, anchor))
			{
				rejectionReason = "coverage";
				return false;
			}

			if (!WouldCoverPotentialScreen(cell, actorInfo, anchor, ScreenEnemyLocation))
			{
				rejectionReason = "screen-coverage";
				return false;
			}

			if (!HasFacilityApproach(cell, buildingInfo, null))
			{
				rejectionReason = "approach";
				return false;
			}

			rejectionReason = "none";
			return true;
		}

		public void PlacementOrdered()
		{
			if (reservation == null)
				return;

			var repairFacility = reservation.RepairFacility;
			Log("placement-ordered actor={0} queue={1} recovery={2}", reservation.ActorType,
				reservation.QueueActorId, repairFacility && repairRecoveryPending);
			reservation = null;
			if (repairFacility)
				ClearRepairRecovery();
			state.RecordPlacementProgress();
		}

		public void PlacementFailed(string reason)
		{
			if (reservation == null)
				return;

			Log("placement-failed actor={0} reason={1}", reservation.ActorType, reason);
			reservation = null;
			if (state.RecordPlacementFailure(Info.DefenseClusterMaximumPlacementFailures))
				InvalidateAnchor("bounded placement infeasibility");
			else
				nextRetryTick = world.WorldTick + Info.DefenseClusterRetryDelay;
		}

		public bool ReadyForWallScreen
		{
			get
			{
				var anchor = ActiveAnchor;
				if (anchor == null)
					return false;

				var towers = LiveNearbyTowers(anchor).ToArray();
				return towers.Length >= Info.DefenseClusterMinimumTowers && LocalRepairFacility(anchor) != null;
			}
		}

		public bool MinimumClusterComplete()
		{
			var anchor = ActiveAnchor;
			if (anchor == null)
				return false;

			var towers = LiveNearbyTowers(anchor).ToArray();
			return DefenseClusterPolicy.IsComplete(towers.Length, Info.DefenseClusterMinimumTowers,
				RequiredRoles(), towers.Where(IsOperationalTower).Select(RolesForActor),
				LocalRepairFacility(anchor) != null);
		}

		IEnumerable<Actor> LiveNearbyTowers(Actor anchor)
		{
			var radiusSquared = Info.DefenseClusterRadius * Info.DefenseClusterRadius;
			return world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
					Info.DefenseClusterTowerTypes.Contains(a.Info.Name) &&
					(a.Location - anchor.Location).LengthSquared <= radiusSquared)
				.OrderBy(a => a.ActorID);
		}

		Actor LocalRepairFacility(Actor anchor)
		{
			var baseCenter = StableBaseCenter();
			return world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
					Info.DefenseClusterRepairFacilityTypes.Contains(a.Info.Name) &&
					(a.Location - baseCenter).LengthSquared <= (anchor.Location - baseCenter).LengthSquared &&
					FacilityCoversCluster(a, anchor) && HasFacilityApproach(a.Location,
						a.Info.TraitInfo<BuildingInfo>(), a))
				.OrderBy(a => a.ActorID).FirstOrDefault();
		}

		bool FacilityCoversCluster(Actor facility, Actor anchor)
		{
			var range = facility.Info.TraitInfos<GrantConditionInRangeInfo>()
				.Where(t => t.Granter).Select(t => t.Range).OrderByDescending(r => r.Length).FirstOrDefault();
			if (range.Length <= 0)
				return false;

			if (!LiveNearbyTowers(anchor).Any(t =>
				(facility.CenterPosition - t.CenterPosition).HorizontalLengthSquared <= range.LengthSquared))
				return false;

			return CoversEveryPotentialScreen(facility.CenterPosition, range.LengthSquared, anchor,
				ScreenEnemyLocation);
		}

		bool WouldCoverAnchor(CPos cell, ActorInfo facilityInfo, Actor anchor)
		{
			var range = facilityInfo.TraitInfos<GrantConditionInRangeInfo>()
				.Where(t => t.Granter).Select(t => t.Range).OrderByDescending(r => r.Length).FirstOrDefault();
			if (range.Length <= 0)
				return false;

			var center = world.Map.CenterOfCell(cell) + facilityInfo.TraitInfo<BuildingInfo>().CenterOffset(world);
			return LiveNearbyTowers(anchor).Any(t =>
				(center - t.CenterPosition).HorizontalLengthSquared <= range.LengthSquared);
		}

		bool WouldCoverPotentialScreen(CPos cell, ActorInfo facilityInfo, Actor anchor, CPos enemyLocation)
		{
			var range = facilityInfo.TraitInfos<GrantConditionInRangeInfo>()
				.Where(t => t.Granter).Select(t => t.Range).OrderByDescending(r => r.Length).FirstOrDefault();
			if (range.Length <= 0)
				return false;

			var center = world.Map.CenterOfCell(cell) + facilityInfo.TraitInfo<BuildingInfo>().CenterOffset(world);
			return CoversEveryPotentialScreen(center, range.LengthSquared, anchor, enemyLocation);
		}

		bool CoversEveryPotentialScreen(WPos facilityCenter, long rangeSquared, Actor anchor,
			CPos enemyLocation)
		{
			var facing = BotWallGeometry.DominantDirection(anchor.Location, enemyLocation);
			return BotWallGeometry.OpenScreenVariants(anchor.Location, facing,
				Info.DefenseClusterWallSetback, Info.DefenseClusterWallHalfWidth,
				Info.DefenseClusterWallFlankDepth).All(lines => lines.SelectMany(line => line).Any(c =>
					(world.Map.CenterOfCell(c) - facilityCenter).HorizontalLengthSquared <= rangeSquared));
		}

		bool HasFacilityApproach(CPos cell, BuildingInfo buildingInfo, Actor ignoredActor)
		{
			return TryChooseFacilityApproachCell(cell, buildingInfo, ignoredActor, out _);
		}

		bool TryChooseFacilityApproachCell(CPos cell, BuildingInfo buildingInfo, Actor ignoredActor,
			out CPos approachCell)
		{
			approachCell = default;
			var footprint = new HashSet<CPos>(buildingInfo.Tiles(cell));
			var approach = new HashSet<CPos>();
			foreach (var tile in footprint)
			{
				approach.Add(tile + new CVec(1, 0));
				approach.Add(tile + new CVec(-1, 0));
				approach.Add(tile + new CVec(0, 1));
				approach.Add(tile + new CVec(0, -1));
			}

			approach.ExceptWith(footprint);
			approach.RemoveWhere(c => IsBlocked(c, ignoredActor));
			if (approach.Count == 0)
				return false;

			var start = FindPathStart(StableBaseCenter(), ignoredActor);
			if (start == null)
				return false;

			var baseCenter = StableBaseCenter();
			foreach (var candidate in approach.OrderBy(c => (c - baseCenter).LengthSquared)
				.ThenBy(c => c.X).ThenBy(c => c.Y))
			{
				if (!BotWallGeometry.CanReachAny(start.Value, new[] { candidate },
					c => footprint.Contains(c) || IsBlocked(c, ignoredActor),
					Info.DefenseClusterPathCheckMaximumCells))
					continue;

				approachCell = candidate;
				return true;
			}

			return false;
		}

		bool IsOperationalTower(Actor actor)
		{
			return (playerPower == null || playerPower.PowerState == PowerState.Normal) &&
				actor.TraitsImplementing<AttackBase>().Any(a => !a.IsTraitDisabled && !a.IsTraitPaused) &&
				actor.TraitsImplementing<Armament>().Any(a => !a.IsTraitDisabled && !a.IsTraitPaused);
		}

		DefenseClusterRole RequiredRoles()
		{
			var roles = DefenseClusterRole.None;
			foreach (var type in Info.DefenseClusterTowerTypes)
			{
				if (Info.BuildingLimits != null && Info.BuildingLimits.TryGetValue(type, out var limit) && limit <= 0)
					continue;
				if (!baseBuilder.IsCurrentlyBuildable(type) &&
					!world.Actors.Any(a => a.Owner == player && a.IsInWorld && !a.IsDead && a.Info.Name == type))
					continue;

				roles |= RolesForType(type);
			}

			return roles;
		}

		DefenseClusterRole RolesForActor(Actor actor) => RolesForType(actor.Info.Name);

		DefenseClusterRole RolesForType(string type)
		{
			var roles = DefenseClusterRole.None;
			if (Info.DefenseClusterAntiInfantryTypes.Contains(type))
				roles |= DefenseClusterRole.AntiInfantry;
			if (Info.DefenseClusterAntiGroundTypes.Contains(type))
				roles |= DefenseClusterRole.AntiGround;
			if (Info.DefenseClusterAntiAirTypes.Contains(type))
				roles |= DefenseClusterRole.AntiAir;
			return roles;
		}

		bool CanCommit(string type)
		{
			if (Info.BuildingLimits == null || !Info.BuildingLimits.TryGetValue(type, out var limit))
				return true;

			var committed = world.Actors.Count(a => a.Owner == player && a.IsInWorld && !a.IsDead && a.Info.Name == type) +
				baseBuilder.CountQueuedOrPendingActors(new[] { type });
			return committed < limit;
		}

		void RefreshReservation()
		{
			if (reservation == null)
				return;

			var queueActor = world.GetActorById(reservation.QueueActorId);
			var producerValid = queueActor != null && queueActor.Owner == player && queueActor.IsInWorld &&
				!queueActor.IsDead;
			var queued = producerValid &&
				queueActor.TraitsImplementing<ProductionQueue>()
					.Any(q => q.AllQueued().Any(i => i.Item == reservation.ActorType));
			var age = world.WorldTick - reservation.Tick;
			if (DefenseClusterPolicy.ReservationIsLost(producerValid, queued, age,
				Info.DefenseClusterReservationTimeout))
			{
				var reason = queueActor == null ? "producer-missing" :
					queueActor.Owner != player ? "producer-owner" :
					!queueActor.IsInWorld ? "producer-not-in-world" :
					queueActor.IsDead ? "producer-dead" : "item-missing-timeout";
				Log("reservation-lost actor={0} queue={1} goal={2} reason={3} age={4}",
					reservation.ActorType, reservation.QueueActorId,
					reservation.RepairFacility ? "repair" : "tower", reason, age);
				if (reservation.RepairFacility)
				{
					if (!repairRecoveryPending)
						repairRecoveryHandoffUsed = false;
					repairRecoveryPending = true;
					nextRetryTick = world.WorldTick;
					Log("repair-recovery-pending actor={0} lost-queue={1} reason={2}",
						reservation.ActorType, reservation.QueueActorId, reason);
				}

				reservation = null;
			}
		}

		void ClearRepairRecovery()
		{
			repairRecoveryPending = false;
			repairRecoveryHandoffUsed = false;
		}

		Actor PreferredRepairProducer(string queueType)
		{
			return world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
					Info.ConstructionYardTypes.Contains(a.Info.Name))
				.SelectMany(a => a.TraitsImplementing<ProductionQueue>()
					.Where(q => q.Info.Type == queueType)
					.Select(q => new { Actor = a, Queue = q }))
				.OrderBy(p => p.Queue.AllQueued().Count())
				.ThenBy(p => p.Actor.ActorID)
				.Select(p => p.Actor)
				.FirstOrDefault();
		}

		void TrySellCausalWall(IBot bot, Actor anchor)
		{
			if (bot == null || LocalRepairFacility(anchor) != null)
				return;

			if (TryFindRepairSite(anchor, false, out _, out _, out _, out _))
				return;
			if (!TryFindRepairSite(anchor, true, out var facilityInfo, out _, out var site, out var wall))
				return;

			ProtectRepairSite(anchor, facilityInfo, site);
			bot.QueueOrder(new Order("Sell", wall, false));
			nextMaintenanceTick = world.WorldTick + Info.DefenseClusterMaintenanceInterval;
			Log("sold wall={0}#{1} cell={2} reason=restored-local-repair-site site={3}",
				wall.Info.Name, wall.ActorID, wall.Location, site);
		}

		HashSet<CPos> PotentialRepairReservationCells(Actor anchor)
		{
			if (TryGetProtectedRepairSite(anchor, null, out _, out var protectedBuilding, out var protectedSite))
				return BotWallGeometry.WithApproachCell(protectedBuilding.Tiles(protectedSite),
					repairSiteState.ApproachCell);

			if (!TryFindRepairSite(anchor, false, out var facilityInfo, out var buildingInfo, out var cell, out _) &&
				!TryFindRepairSite(anchor, true, out facilityInfo, out buildingInfo, out cell, out _))
				return new HashSet<CPos>();

			ProtectRepairSite(anchor, facilityInfo, cell);
			return BotWallGeometry.WithApproachCell(buildingInfo.Tiles(cell), repairSiteState.ApproachCell);
		}

		bool TryGetProtectedRepairSite(Actor anchor, string facilityType, out ActorInfo facilityInfo,
			out BuildingInfo buildingInfo, out CPos site)
		{
			facilityInfo = null;
			buildingInfo = null;
			site = default;
			if (anchor == null || !repairSiteState.Matches(anchor.ActorID, facilityType) ||
				!world.Map.Rules.Actors.TryGetValue(repairSiteState.FacilityType, out facilityInfo))
				return false;

			buildingInfo = facilityInfo.TraitInfoOrDefault<BuildingInfo>();
			if (buildingInfo == null)
				return false;

			site = repairSiteState.Site;
			return true;
		}

		void ProtectRepairSite(Actor anchor, ActorInfo facilityInfo, CPos site)
		{
			if (anchor == null || facilityInfo == null)
				return;

			var buildingInfo = facilityInfo.TraitInfoOrDefault<BuildingInfo>();
			if (buildingInfo == null || !TryChooseFacilityApproachCell(site, buildingInfo, null, out var approachCell) ||
				!repairSiteState.Protect(anchor.ActorID, facilityInfo.Name, site, approachCell, lastEnemyLocation))
				return;

			Log("repair-site-protected anchor={0} actor={1} site={2} approach={3}",
				anchor.ActorID, facilityInfo.Name, site, approachCell);
		}

		void ClearProtectedRepairSite(string reason)
		{
			if (!repairSiteState.HasSite)
				return;

			var anchorId = repairSiteState.AnchorActorId;
			var facilityType = repairSiteState.FacilityType;
			var site = repairSiteState.Site;
			repairSiteState.Clear();
			Log("repair-site-cleared anchor={0} actor={1} site={2} reason={3}",
				anchorId, facilityType, site, reason);
		}

		bool TryFindRepairSite(Actor anchor, bool requireWallRemoval, out ActorInfo facilityInfo,
			out BuildingInfo buildingInfo, out CPos site, out Actor blockingWall)
		{
			facilityInfo = null;
			buildingInfo = null;
			site = default;
			blockingWall = null;
			var baseCenter = StableBaseCenter();
			var candidates = world.Map.FindTilesInAnnulus(anchor.Location,
				Info.DefenseClusterPlacementMinimumRadius, Info.DefenseClusterPlacementMaximumRadius)
				.Where(c => (c - baseCenter).LengthSquared <= (anchor.Location - baseCenter).LengthSquared)
				.OrderBy(c => (c - baseCenter).LengthSquared).ThenBy(c => c.X).ThenBy(c => c.Y).ToArray();
			var considered = 0;
			var adjacencyRejected = 0;
			var coverageRejected = 0;
			var alreadyLegal = 0;
			var noCandidateWall = 0;
			var afterRemovalRejected = 0;
			var approachRejected = 0;
			var firstAfterRemovalReason = "none";

			foreach (var facilityType in Info.DefenseClusterRepairFacilityTypes)
			{
				if (!world.Map.Rules.Actors.TryGetValue(facilityType, out var candidateInfo))
					continue;
				var candidateBuilding = candidateInfo.TraitInfoOrDefault<BuildingInfo>();
				if (candidateBuilding == null)
					continue;

				foreach (var cell in candidates)
				{
					considered++;
					if (!candidateBuilding.IsCloseEnoughToBase(world, player, candidateInfo, cell))
					{
						adjacencyRejected++;
						continue;
					}

					if (!WouldCoverAnchor(cell, candidateInfo, anchor))
					{
						coverageRejected++;
						continue;
					}

					if (!WouldCoverPotentialScreen(cell, candidateInfo, anchor, lastEnemyLocation))
					{
						coverageRejected++;
						continue;
					}

					if (!requireWallRemoval)
					{
						if (!world.CanPlaceBuilding(cell, candidateInfo, candidateBuilding, null) ||
							!HasFacilityApproach(cell, candidateBuilding, null))
							continue;

						facilityInfo = candidateInfo;
						buildingInfo = candidateBuilding;
						site = cell;
						return true;
					}

					if (world.CanPlaceBuilding(cell, candidateInfo, candidateBuilding, null))
					{
						alreadyLegal++;
						continue;
					}

					var walls = candidateBuilding.Tiles(cell)
						.SelectMany(t => buildingInfluence?.GetBuildingsAt(t) ?? Enumerable.Empty<Actor>())
						.Where(IsSellableClusterWallCandidate).Distinct().OrderBy(a => a.ActorID).ToArray();
					if (walls.Length == 0)
					{
						noCandidateWall++;
						continue;
					}

					foreach (var wall in walls)
					{
						if (!CanPlaceBuildingAfterRemovingWall(cell, candidateBuilding, wall, out var rejectionReason))
						{
							afterRemovalRejected++;
							if (firstAfterRemovalReason == "none")
								firstAfterRemovalReason = rejectionReason;
							continue;
						}

						if (!HasFacilityApproach(cell, candidateBuilding, wall))
						{
							approachRejected++;
							continue;
						}

						facilityInfo = candidateInfo;
						buildingInfo = candidateBuilding;
						site = cell;
						blockingWall = wall;
						LogRepairSiteDiagnostic("causal", anchor, considered, adjacencyRejected, coverageRejected,
							alreadyLegal, noCandidateWall, afterRemovalRejected, approachRejected,
							"site=" + cell + " wall=" + wall.Info.Name + "#" + wall.ActorID +
							"@" + wall.Location + " before=false after=true approach=true");
						return true;
					}
				}
			}

			if (requireWallRemoval)
				LogRepairSiteDiagnostic("rejected", anchor, considered, adjacencyRejected, coverageRejected,
					alreadyLegal, noCandidateWall, afterRemovalRejected, approachRejected,
					"first-after-reason=" + firstAfterRemovalReason);

			return false;
		}

		bool CanPlaceBuildingAfterRemovingWall(CPos cell, BuildingInfo buildingInfo, Actor wall,
			out string rejectionReason)
		{
			rejectionReason = "none";
			if (wall == null || buildingInfluence == null || buildingInfo.AllowInvalidPlacement)
			{
				rejectionReason = "missing-wall-or-influence";
				return false;
			}

			foreach (var tile in buildingInfo.Tiles(cell))
			{
				if (!world.Map.Contains(tile))
				{
					rejectionReason = "outside-map@" + tile;
					return false;
				}

				if (!buildingInfo.AllowPlacementOnResources && resourceLayer != null &&
					resourceLayer.GetResource(tile).Type != null)
				{
					rejectionReason = "resource@" + tile;
					return false;
				}

				if (world.Map.Ramp[tile] != 0 || !buildingInfo.TerrainTypes.Contains(world.Map.GetTerrainInfo(tile).Type))
				{
					rejectionReason = "terrain@" + tile;
					return false;
				}

				var occupant = world.ActorMap.GetActorsAt(tile).Where(a => a != wall)
					.OrderBy(a => a.ActorID).FirstOrDefault();
				if (occupant != null)
				{
					rejectionReason = "actor=" + occupant.Info.Name + "#" + occupant.ActorID + "@" + tile;
					return false;
				}

				var influenced = buildingInfluence.GetBuildingsAt(tile).Where(a => a != wall)
					.OrderBy(a => a.ActorID).FirstOrDefault();
				if (influenced != null)
				{
					rejectionReason = "building=" + influenced.Info.Name + "#" + influenced.ActorID + "@" + tile;
					return false;
				}
			}

			return true;
		}

		void LogRepairSiteDiagnostic(string outcome, Actor anchor, int considered, int adjacencyRejected,
			int coverageRejected, int alreadyLegal, int noCandidateWall, int afterRemovalRejected,
			int approachRejected, string detail)
		{
			if (world.WorldTick < nextRepairDiagnosticTick)
				return;

			nextRepairDiagnosticTick = world.WorldTick + Math.Max(Info.DefenseClusterMaintenanceInterval,
				Info.DefenseClusterRetryDelay);
			Log("repair-site-{0} anchor={1} candidates={2} adjacency-rejected={3} coverage-rejected={4} " +
				"already-legal={5} no-candidate-wall={6} after-removal-rejected={7} approach-rejected={8} {9}",
				outcome, anchor.ActorID, considered, adjacencyRejected, coverageRejected, alreadyLegal,
				noCandidateWall, afterRemovalRejected, approachRejected, detail);
		}

		bool IsSellableClusterWallCandidate(Actor wall)
		{
			if (wall == null || wall.Owner != player || !wall.IsInWorld || wall.IsDead ||
				!Info.WallTypes.Contains(wall.Info.Name) || wall.TraitOrDefault<Sellable>()?.IsTraitDisabled != false)
				return false;

			var protectedEnclosure = IsProtectedConstructionYardWall(wall.Location);
			var legacyClusterWall = legacyClusterWallCells.Contains(wall.Location);
			var plannedClusterWall = baseBuilder.WallPlanner?.OwnsClusterWallCell(wall.Location) == true;
			return DefenseClusterPolicy.IsSellableWallPurpose(protectedEnclosure,
				legacyClusterWall, plannedClusterWall);
		}

		bool IsProtectedConstructionYardWall(CPos cell)
		{
			// First-Fact enclosure cells are owned by the enclosure planner forever, even if
			// the saved in-memory planner bookkeeping has been reconstructed.
			foreach (var yard in world.ActorsHavingTrait<Building>().Where(a => a.Owner == player && a.IsInWorld &&
				!a.IsDead && Info.ConstructionYardTypes.Contains(a.Info.Name)).OrderBy(a => a.ActorID))
			{
				var yardBuilding = yard.Info.TraitInfoOrDefault<BuildingInfo>();
				if (yardBuilding != null && BotWallGeometry.EnclosurePerimeter(yard.Location,
					yardBuilding.Dimensions, Info.ConstructionYardEnclosureMargin).Contains(cell))
					return true;
			}

			return false;
		}

		void RememberLegacyClusterWalls(Actor anchor)
		{
			legacyClusterWallCells.Clear();
			if (anchor == null)
				return;

			var radiusSquared = Info.DefenseClusterRadius * Info.DefenseClusterRadius;
			foreach (var wall in world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
					Info.WallTypes.Contains(a.Info.Name) &&
					(a.Location - anchor.Location).LengthSquared <= radiusSquared)
				.OrderBy(a => a.ActorID))
				if (!IsProtectedConstructionYardWall(wall.Location))
					legacyClusterWallCells.Add(wall.Location);
		}

		Actor ValidTower(uint actorId)
		{
			if (actorId == 0)
				return null;
			var actor = world.GetActorById(actorId);
			return actor != null && actor.Owner == player && actor.IsInWorld && !actor.IsDead &&
				Info.DefenseClusterTowerTypes.Contains(actor.Info.Name) ? actor : null;
		}

		void InvalidateAnchor(string reason)
		{
			var old = state.AnchorActorId;
			completionLogged = false;
			ClearProtectedRepairSite("anchor-invalidation");
			state.InvalidateAnchor(world.WorldTick, Info.DefenseClusterAnchorLease,
				id => ValidTower(id) != null);
			if (!state.HasAnchor)
			{
				reservation = null;
				ClearRepairRecovery();
			}

			RememberLegacyClusterWalls(ActiveAnchor);
			nextRetryTick = world.WorldTick + Info.DefenseClusterRetryDelay;
			Log("invalidated anchor={0} reason={1} replacement={2}", old, reason, state.AnchorActorId);
		}

		CPos StableBaseCenter()
		{
			var yard = world.ActorsHavingTrait<Building>().Where(a => a.Owner == player && a.IsInWorld &&
				!a.IsDead && Info.ConstructionYardTypes.Contains(a.Info.Name)).OrderBy(a => a.ActorID).FirstOrDefault();
			return yard?.Location ?? baseBuilder.DefenseCenter;
		}

		CPos? FindPathStart(CPos around, Actor ignoredActor)
		{
			foreach (var cell in world.Map.FindTilesInCircle(around, 6)
				.OrderByDescending(c => (c - around).LengthSquared).ThenBy(c => c.X).ThenBy(c => c.Y))
				if (!IsBlocked(cell, ignoredActor))
					return cell;
			return null;
		}

		bool IsBlocked(CPos cell, Actor ignoredActor)
		{
			if (!world.Map.Contains(cell) ||
				(locomotor != null && locomotor.MovementCostForCell(cell) == PathGraph.MovementCostForUnreachableCell))
				return true;

			return buildingInfluence != null && buildingInfluence.GetBuildingsAt(cell).Any(a => a != ignoredActor);
		}

		void Log(string format, params object[] args)
		{
			if (!Game.Settings.Debug.BotDebug)
				return;

			OpenRA.Log.Write("debug", "AI defense cluster: {0} tick={1} {2}",
				player, world.WorldTick, string.Format(format, args));
		}

		public MiniYamlNode IssueTraitData()
		{
			if (!enabled)
				return null;

			return new MiniYamlNode("DefenseClusterState", new MiniYaml("", new List<MiniYamlNode>
			{
				Save("AnchorActorId", state.AnchorActorId),
				Save("AnchorAttackTick", state.AnchorAttackTick),
				Save("AnchorLeaseUntilTick", state.AnchorLeaseUntilTick),
				Save("PendingActorId", state.PendingActorId),
				Save("PendingAttackTick", state.PendingAttackTick),
				Save("PlacementFailures", state.PlacementFailures),
				Save("NextMaintenanceTick", nextMaintenanceTick),
				Save("NextRetryTick", nextRetryTick),
				Save("NextStatusLogTick", nextStatusLogTick),
				Save("NextRepairRecoveryDiagnosticTick", nextRepairRecoveryDiagnosticTick),
				Save("OrdinaryDefenseActorId", ordinaryDefenseActorId),
				Save("OrdinaryDefenseCenter", ordinaryDefenseCenter),
				Save("OrdinaryDefenseExpiryTick", ordinaryDefenseExpiryTick),
				Save("LastEnemyLocation", lastEnemyLocation),
				Save("ProtectedRepairAnchorActorId", repairSiteState.AnchorActorId),
				Save("ProtectedRepairFacilityType", repairSiteState.FacilityType ?? ""),
				Save("ProtectedRepairSite", repairSiteState.Site),
				Save("ProtectedRepairApproachCell", repairSiteState.ApproachCell),
				Save("ProtectedRepairEnemyLocation", repairSiteState.EnemyLocation),
				Save("LegacyClusterWallCells", legacyClusterWallCells
					.OrderBy(c => c.X).ThenBy(c => c.Y).ToArray()),
				Save("ReservationQueueActorId", reservation?.QueueActorId ?? 0),
				Save("ReservationActorType", reservation?.ActorType ?? ""),
				Save("ReservationTick", reservation?.Tick ?? 0),
				Save("ReservationRepairFacility", reservation?.RepairFacility ?? false),
				Save("RepairRecoveryPending", repairRecoveryPending),
				Save("RepairRecoveryHandoffUsed", repairRecoveryHandoffUsed)
			}));
		}

		public void ResolveTraitData(List<MiniYamlNode> data)
		{
			if (!enabled)
				return;

			var node = data.FirstOrDefault(n => n.Key == "DefenseClusterState");
			if (node == null)
				return;

			try
			{
				var nodes = node.Value.Nodes;
				state.Restore(Read<uint>(nodes, "AnchorActorId", 0), Read<int>(nodes, "AnchorAttackTick", 0),
					Read<int>(nodes, "AnchorLeaseUntilTick", 0), Read<uint>(nodes, "PendingActorId", 0),
					Read<int>(nodes, "PendingAttackTick", 0), Read<int>(nodes, "PlacementFailures", 0));
				nextMaintenanceTick = Read<int>(nodes, "NextMaintenanceTick", world.WorldTick);
				nextRetryTick = Read<int>(nodes, "NextRetryTick", world.WorldTick);
				nextStatusLogTick = Read<int>(nodes, "NextStatusLogTick", world.WorldTick);
				nextRepairRecoveryDiagnosticTick = Read<int>(nodes,
					"NextRepairRecoveryDiagnosticTick", world.WorldTick);
				ordinaryDefenseActorId = Read<uint>(nodes, "OrdinaryDefenseActorId", 0);
				ordinaryDefenseCenter = Read(nodes, "OrdinaryDefenseCenter", default(CPos));
				ordinaryDefenseExpiryTick = Read<int>(nodes, "OrdinaryDefenseExpiryTick", 0);
				lastEnemyLocation = Read(nodes, "LastEnemyLocation", default(CPos));
				repairSiteState.Restore(Read<uint>(nodes, "ProtectedRepairAnchorActorId", 0),
					Read(nodes, "ProtectedRepairFacilityType", ""),
					Read(nodes, "ProtectedRepairSite", default(CPos)),
					Read(nodes, "ProtectedRepairApproachCell", default(CPos)),
					Read(nodes, "ProtectedRepairEnemyLocation", lastEnemyLocation));
				if (repairSiteState.HasSite && (repairSiteState.AnchorActorId != state.AnchorActorId ||
					!Info.DefenseClusterRepairFacilityTypes.Contains(repairSiteState.FacilityType) ||
					!world.Map.Rules.Actors.ContainsKey(repairSiteState.FacilityType)))
					repairSiteState.Clear();
				legacyClusterWallCells.Clear();
				legacyClusterWallCells.UnionWith(Read(nodes, "LegacyClusterWallCells", Array.Empty<CPos>()));
				if (legacyClusterWallCells.Count == 0)
					RememberLegacyClusterWalls(ActiveAnchor);
				var reservationType = Read(nodes, "ReservationActorType", "");
				if (!string.IsNullOrEmpty(reservationType))
					reservation = new Reservation
					{
						QueueActorId = Read<uint>(nodes, "ReservationQueueActorId", 0),
						ActorType = reservationType,
						Tick = Read<int>(nodes, "ReservationTick", 0),
						RepairFacility = Read(nodes, "ReservationRepairFacility", false)
					};
				repairRecoveryPending = Read(nodes, "RepairRecoveryPending", false);
				repairRecoveryHandoffUsed = Read(nodes, "RepairRecoveryHandoffUsed", false);
				Log("load-restored anchor={0} pending={1} reservation={2}", state.AnchorActorId,
					state.PendingActorId, reservation?.ActorType ?? "none");
			}
			catch (Exception ex)
			{
				state.Restore(0, 0, 0, 0, 0, 0);
				reservation = null;
				ClearRepairRecovery();
				repairSiteState.Clear();
				legacyClusterWallCells.Clear();
				Log("load-invalid type={0} message={1}; reconstructing from live world", ex.GetType().Name, ex.Message);
			}
		}

		static MiniYamlNode Save<T>(string key, T value) =>
			new MiniYamlNode(key, FieldSaver.FormatValue(value));

		static T Read<T>(List<MiniYamlNode> nodes, string key, T fallback)
		{
			var node = nodes.FirstOrDefault(n => n.Key == key);
			return node == null ? fallback : FieldLoader.GetValue<T>(key, node.Value.Value);
		}
	}
}
