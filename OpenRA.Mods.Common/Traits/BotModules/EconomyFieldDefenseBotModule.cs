#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Stations a mixed screen at successfully delivered economy fields.")]
	public class EconomyFieldDefenseBotModuleInfo : ConditionalTraitInfo
	{
		public readonly string[] RequiredPrerequisites = Array.Empty<string>();
		public readonly HashSet<string> HarvesterTypes = new HashSet<string>();
		public readonly HashSet<string> TankTypes = new HashSet<string>();
		public readonly HashSet<string> InfantryTypes = new HashSet<string>();
		public readonly HashSet<string> AntiAirTypes = new HashSet<string>();
		public readonly HashSet<string> AvoidResourceTypes = new HashSet<string>();
		public readonly int TanksPerHarvester = 1;
		public readonly int InfantryPerHarvester = 2;
		public readonly int AntiAirPerHarvester = 1;
		public readonly int ScanInterval = 25;
		public readonly int OrderInterval = 25;
		public readonly int StationMinimumRadiusCells = 2;
		public readonly int StationMaximumRadiusCells = 6;
		public readonly int FormationToleranceCells = 1;
		public readonly int EngagementLeashCells = 8;
		public readonly int RefineryLaneLengthCells = 7;
		public readonly int RefineryLaneHalfWidthCells = 1;
		public readonly int MaximumRouteCells = 96;
		public readonly int ResourceSafetyMarginCells = 2;
		public readonly int ResourceModifierSafetyMarginCells = 1;
		public readonly int RouteStallTicks = 250;
		public readonly int RouteRetryTicks = 250;
		public readonly int MaximumCandidatesPerRole = 48;
		public readonly int MaximumOutstandingRequestsPerRole = 1;
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (RequiredPrerequisites.Length == 0 || HarvesterTypes.Count == 0 || TankTypes.Count == 0 ||
				InfantryTypes.Count == 0 || AntiAirTypes.Count == 0 || AvoidResourceTypes.Count == 0 ||
				TanksPerHarvester <= 0 || InfantryPerHarvester <= 0 || AntiAirPerHarvester <= 0 ||
				ScanInterval <= 0 || OrderInterval <= 0 || StationMinimumRadiusCells < 0 ||
				StationMaximumRadiusCells < StationMinimumRadiusCells || FormationToleranceCells < 0 ||
				EngagementLeashCells < FormationToleranceCells || RefineryLaneLengthCells <= 0 ||
				RefineryLaneHalfWidthCells < 0 || MaximumRouteCells < 2 || ResourceSafetyMarginCells < 0 ||
				ResourceModifierSafetyMarginCells < 0 ||
				RouteStallTicks <= 0 ||
				RouteRetryTicks <= 0 || MaximumCandidatesPerRole <= 0 || MaximumOutstandingRequestsPerRole <= 0)
				throw new YamlException("Economy field-defense prerequisites, actor/resource types, counts, intervals, radii, lanes, and bounds must be configured and valid.");

			foreach (var actorType in HarvesterTypes.Concat(TankTypes).Concat(InfantryTypes).Concat(AntiAirTypes))
				if (!rules.Actors.ContainsKey(actorType))
					throw new YamlException($"Economy field-defense actor '{actorType}' does not exist.");

			foreach (var actorType in TankTypes.Concat(InfantryTypes).Concat(AntiAirTypes))
			{
				var moveAlongPath = rules.Actors[actorType].TraitInfoOrDefault<MoveAlongPathInfo>();
				if (moveAlongPath == null || moveAlongPath.MaximumPathCells < MaximumRouteCells ||
					moveAlongPath.ResourceSafetyMarginCells < ResourceSafetyMarginCells ||
					!moveAlongPath.AvoidResourceTypes.IsSupersetOf(AvoidResourceTypes))
					throw new YamlException($"Economy field-defense actor '{actorType}' must support the configured exact resource-safe route.");
			}

			var resourceTypes = rules.Actors[SystemActors.World].TraitInfo<ResourceLayerInfo>().ResourceTypes;
			foreach (var resourceType in AvoidResourceTypes)
				if (!resourceTypes.ContainsKey(resourceType))
					throw new YamlException($"Economy field-defense resource '{resourceType}' does not exist.");
		}

		public override object Create(ActorInitializer init) { return new EconomyFieldDefenseBotModule(init.Self, this); }
	}

	public sealed class EconomyFieldDefenseBotModule : ConditionalTrait<EconomyFieldDefenseBotModuleInfo>,
		IBotEnabled, IBotTick, IBotUnitReservations, IGameSaveTraitData
	{
		const string ProductionRequestOwner = "EconomyFieldDefense";

		sealed class FieldAssignment
		{
			public readonly uint HarvesterId;
			public CPos Station;
			public readonly HashSet<uint> Tanks = new HashSet<uint>();
			public readonly HashSet<uint> Infantry = new HashSet<uint>();
			public readonly HashSet<uint> AntiAir = new HashSet<uint>();
			public readonly Dictionary<uint, CPos> Destinations = new Dictionary<uint, CPos>();

			public FieldAssignment(uint harvesterId, CPos station)
			{
				HarvesterId = harvesterId;
				Station = station;
			}
		}

		sealed class RouteProgress
		{
			public long BestDistanceSquared;
			public int LastProgressTick;
			public bool EnRoute;
		}

		sealed class ReleaseDiagnostic
		{
			public readonly FieldAssignment Field;
			public readonly uint ActorId;
			public readonly string ActorType;
			public readonly string Role;
			public readonly string Reason;
			public readonly HashSet<string> RoleTypes;

			public ReleaseDiagnostic(FieldAssignment field, uint actorId, string actorType, string role,
				string reason, HashSet<string> roleTypes)
			{
				Field = field;
				ActorId = actorId;
				ActorType = actorType;
				Role = role;
				Reason = reason;
				RoleTypes = roleTypes;
			}
		}

		readonly World world;
		readonly Player player;
		readonly Dictionary<uint, FieldAssignment> fields = new Dictionary<uint, FieldAssignment>();
		readonly HashSet<uint> reserved = new HashSet<uint>();
		readonly Dictionary<uint, int> lastOrderTicks = new Dictionary<uint, int>();
		readonly Dictionary<uint, RouteProgress> routeProgress = new Dictionary<uint, RouteProgress>();
		readonly Dictionary<uint, int> routeRejectedUntil = new Dictionary<uint, int>();
		readonly Dictionary<uint, CPos> lastUnsafeOccupancy = new Dictionary<uint, CPos>();
		readonly Dictionary<uint, UnitStance> originalStances = new Dictionary<uint, UnitStance>();
		readonly Dictionary<uint, string> lastSafetyStates = new Dictionary<uint, string>();
		IBot bot;
		IBotUnitReservations[] otherReservations;
		IBotTransportReservations[] transportReservations;
		SquadManagerBotModule squadManager;
		DomainIndex domainIndex;
		IResourceLayer resourceLayer;
		TechTree techTree;
		PowerManager powerManager;
		IBotRequestOwnedUnitProduction[] productionRequesters;
		IBotRequestOwnedUnitProduction productionRequester;
		HashSet<CPos> refineryTraffic = new HashSet<CPos>();
		HashSet<CPos> projectedResourceHazards = new HashSet<CPos>();
		HashSet<CPos> ownedMovementHazards = new HashSet<CPos>();
		readonly Dictionary<uint, string> lastFieldCompositions = new Dictionary<uint, string>();
		int scanTicks = 1;
		string lastComposition;

		public EconomyFieldDefenseBotModule(Actor self, EconomyFieldDefenseBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			techTree = player.PlayerActor.Trait<TechTree>();
			domainIndex = world.WorldActor.Trait<DomainIndex>();
			resourceLayer = world.WorldActor.Trait<IResourceLayer>();
			powerManager = player.PlayerActor.TraitOrDefault<PowerManager>();
			productionRequesters = player.PlayerActor.TraitsImplementing<IBotRequestOwnedUnitProduction>().ToArray();
			otherReservations = player.PlayerActor.TraitsImplementing<IBotUnitReservations>()
				.Where(r => !ReferenceEquals(r, this)).ToArray();
			transportReservations = player.PlayerActor.TraitsImplementing<IBotTransportReservations>().ToArray();
			RefreshSquadManager();
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self) { scanTicks = 1; }

		protected override void TraitDisabled(Actor self) { ClearState("bot condition disabled"); }

		void IBotEnabled.BotEnabled(IBot enabledBot) { bot = enabledBot; }

		bool IBotUnitReservations.IsUnitReserved(Actor actor)
		{
			return actor != null && reserved.Contains(actor.ActorID);
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (IsTraitDisabled || player.WinState != WinState.Undefined || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			if (!techTree.HasPrerequisites(Info.RequiredPrerequisites))
			{
				ClearState("economy capability unavailable");
				return;
			}

			RefreshSquadManager();
			UpdateFields();
			refineryTraffic = BuildRefineryTraffic();
			projectedResourceHazards = BuildProjectedResourceHazards();
			ownedMovementHazards = new HashSet<CPos>(refineryTraffic);
			ownedMovementHazards.UnionWith(projectedResourceHazards);
			Rebalance();
			UpdateProductionRequests();
			UpdateOrders();
			LogComposition();
		}

		void RefreshSquadManager()
		{
			if (squadManager == null || squadManager.IsTraitDisabled)
				squadManager = player.PlayerActor.TraitsImplementing<SquadManagerBotModule>()
					.FirstOrDefault(m => !m.IsTraitDisabled);
		}

		void UpdateFields()
		{
			var current = world.ActorsWithTrait<IHarvesterFieldStation>()
				.Where(p => IsOwnedUsable(p.Actor) && Info.HarvesterTypes.Contains(p.Actor.Info.Name) && p.Trait.HasCommittedField)
				.OrderBy(p => p.Actor.ActorID).ToArray();
			var currentIds = current.Select(p => p.Actor.ActorID).ToHashSet();
			foreach (var stale in fields.Keys.Where(id => !currentIds.Contains(id)).ToArray())
				RemoveField(stale, "harvester-or-station-unavailable");

			foreach (var pair in current)
			{
				if (!fields.TryGetValue(pair.Actor.ActorID, out var field))
				{
					fields.Add(pair.Actor.ActorID, new FieldAssignment(pair.Actor.ActorID, pair.Trait.CommittedField));
					Debug("committed station harvester={0} station={1}", pair.Actor.ActorID, pair.Trait.CommittedField);
				}
				else if (field.Station != pair.Trait.CommittedField)
				{
					Debug("station transition harvester={0} old={1} new={2}", pair.Actor.ActorID, field.Station, pair.Trait.CommittedField);
					field.Station = pair.Trait.CommittedField;
					field.Destinations.Clear();
				}
			}
		}

		void RemoveField(uint harvesterId, string reason)
		{
			if (!fields.TryGetValue(harvesterId, out var field))
				return;

			foreach (var id in field.Tanks.Concat(field.Infantry).Concat(field.AntiAir).ToArray())
			{
				RestoreStance(id);
				reserved.Remove(id);
				lastOrderTicks.Remove(id);
				routeProgress.Remove(id);
				routeRejectedUntil.Remove(id);
			}

			fields.Remove(harvesterId);
			lastFieldCompositions.Remove(harvesterId);
			Debug("released field harvester={0} reason={1}", harvesterId, reason);
		}

		void Rebalance()
		{
			foreach (var id in routeRejectedUntil.Where(p => world.WorldTick >= p.Value || !IsOwnedUsable(world.GetActorById(p.Key)))
				.Select(p => p.Key).ToArray())
				routeRejectedUntil.Remove(id);

			reserved.Clear();
			var releases = Info.DebugLogging ? new List<ReleaseDiagnostic>() : null;
			foreach (var field in fields.Values.OrderBy(f => f.HarvesterId))
			{
				Prune(field, field.Tanks, Info.TankTypes, "tank", releases);
				Prune(field, field.Infantry, Info.InfantryTypes, "infantry", releases);
				Prune(field, field.AntiAir, Info.AntiAirTypes, "anti-air", releases);
			}

			if (releases != null)
				foreach (var release in releases)
				{
					var candidate = FindCandidate(release.Field, release.RoleTypes);
					Debug("released defender={0} type={1} field={2} active=true role={3} remaining={4} target={5}" +
						" reason={6} eligible-replacement={7}", release.ActorId, release.ActorType,
						release.Field.HarvesterId, release.Role, RoleCount(release.Field, release.Role),
						RoleTarget(release.Role), release.Reason,
						candidate == null ? "none" : $"{candidate.ActorID}:{candidate.Info.Name}");
				}

			// Give every field a useful mixed nucleus before filling the second infantry slot.
			foreach (var field in fields.Values.OrderBy(f => f.HarvesterId))
			{
				Fill(field, field.Tanks, Info.TankTypes, 1);
				Fill(field, field.Infantry, Info.InfantryTypes, 1);
				Fill(field, field.AntiAir, Info.AntiAirTypes, 1);
			}

			foreach (var field in fields.Values.OrderBy(f => f.HarvesterId))
			{
				Fill(field, field.Tanks, Info.TankTypes, Info.TanksPerHarvester);
				Fill(field, field.Infantry, Info.InfantryTypes, Info.InfantryPerHarvester);
				Fill(field, field.AntiAir, Info.AntiAirTypes, Info.AntiAirPerHarvester);
			}
		}

		void Prune(FieldAssignment field, HashSet<uint> actors, HashSet<string> types, string role,
			List<ReleaseDiagnostic> releases)
		{
			foreach (var id in actors.ToArray())
			{
				var actor = world.GetActorById(id);
				var reason = actor != null && !types.Contains(actor.Info.Name) ?
					"wrong-type" : ClaimRejectionReason(actor, field.Station, false);
				if (reason != null)
				{
					var actorType = actor?.Info.Name ?? "missing";
					RestoreStance(id);
					actors.Remove(id);
					field.Destinations.Remove(id);
					lastOrderTicks.Remove(id);
					routeProgress.Remove(id);
					releases?.Add(new ReleaseDiagnostic(field, id, actorType, role, reason, types));
					continue;
				}

				reserved.Add(id);
				OwnDefensiveStance(actor);
			}
		}

		void Fill(FieldAssignment field, HashSet<uint> assignment, HashSet<string> types, int targetCount)
		{
			while (assignment.Count < targetCount)
			{
				var candidate = FindCandidate(field, types);
				if (candidate == null)
					return;

				assignment.Add(candidate.ActorID);
				reserved.Add(candidate.ActorID);
				OwnDefensiveStance(candidate);
				Debug("assigned defender={0} type={1} field={2} station={3}", candidate.ActorID,
					candidate.Info.Name, field.HarvesterId, field.Station);
			}
		}

		Actor FindCandidate(FieldAssignment field, HashSet<string> types)
		{
			return world.Actors.Where(a => types.Contains(a.Info.Name) && !reserved.Contains(a.ActorID) &&
				(!routeRejectedUntil.TryGetValue(a.ActorID, out var retryTick) || world.WorldTick >= retryTick))
				.OrderBy(a => (a.CenterPosition - world.Map.CenterOfCell(field.Station)).HorizontalLengthSquared)
				.ThenBy(a => a.ActorID).Take(Info.MaximumCandidatesPerRole)
				.FirstOrDefault(a => IsClaimable(a, field.Station, true));
		}

		bool IsClaimable(Actor actor, CPos station, bool checkProtection)
		{
			return ClaimRejectionReason(actor, station, checkProtection) == null;
		}

		string ClaimRejectionReason(Actor actor, CPos station, bool checkProtection)
		{
			if (actor == null)
				return "missing";
			if (actor.IsDead)
				return "dead";
			if (actor.Owner != player)
				return "wrong-owner";
			if (!actor.IsInWorld)
				return "not-in-world";

			var mobile = actor.TraitOrDefault<Mobile>();
			if (mobile == null)
				return "missing-mobile";
			if (!CanSynchronizeMovementSafety(actor))
				return "movement-safety-unavailable";
			if (!domainIndex.IsPassable(actor.Location, station, mobile.Locomotor))
				return "unreachable-domain";
			if (refineryTraffic.Contains(actor.Location))
				return "refinery-traffic";
			if (HasAvoidedResource(actor.Location))
				return "resource";

			var transportReservation = transportReservations.FirstOrDefault(r => r.IsTransportReserved(actor));
			if (transportReservation != null)
				return $"transport-reservation:{transportReservation.GetType().Name}";

			var otherReservation = otherReservations.FirstOrDefault(r => r.IsUnitReserved(actor));
			if (otherReservation != null)
				return $"unit-reservation:{otherReservation.GetType().Name}";

			return checkProtection && (squadManager?.IsUnitProtectingBase(actor) ?? false) ? "base-protection" : null;
		}

		int RoleCount(FieldAssignment field, string role)
		{
			return role == "tank" ? field.Tanks.Count : role == "infantry" ? field.Infantry.Count : field.AntiAir.Count;
		}

		int RoleTarget(string role)
		{
			return role == "tank" ? Info.TanksPerHarvester :
				role == "infantry" ? Info.InfantryPerHarvester : Info.AntiAirPerHarvester;
		}

		bool IsOwnedUsable(Actor actor)
		{
			return actor != null && actor.Owner == player && actor.IsInWorld && !actor.IsDead;
		}

		HashSet<CPos> BuildRefineryTraffic()
		{
			var cells = new HashSet<CPos>();
			foreach (var pair in world.ActorsWithTrait<IAcceptResources>()
				.Where(p => IsOwnedUsable(p.Actor)).OrderBy(p => p.Actor.ActorID))
			{
				var building = pair.Actor.Info.TraitInfoOrDefault<BuildingInfo>();
				if (building != null)
					cells.UnionWith(building.Tiles(pair.Actor.Location));

				var delivery = pair.Actor.Location + pair.Trait.DeliveryOffset;
				var dx = Math.Sign(pair.Trait.DeliveryOffset.X);
				var dy = Math.Sign(pair.Trait.DeliveryOffset.Y);
				if (dx == 0 && dy == 0)
					dy = 1;

				for (var distance = 0; distance <= Info.RefineryLaneLengthCells; distance++)
					for (var width = -Info.RefineryLaneHalfWidthCells; width <= Info.RefineryLaneHalfWidthCells; width++)
						cells.Add(delivery + new CVec(dx * distance - dy * width, dy * distance + dx * width));
			}

			return cells;
		}

		HashSet<CPos> BuildProjectedResourceHazards()
		{
			var cells = new HashSet<CPos>();
			foreach (var pair in world.ActorsWithTrait<ModifiesResources>()
				.Where(p => p.Actor.IsInWorld && !p.Actor.IsDead).OrderBy(p => p.Actor.ActorID))
			{
				var range = WDist.ToCells(pair.Trait.Range);
				if (range <= 0)
					continue;

				cells.UnionWith(world.Map.FindTilesInCircle(pair.Actor.Location,
					EconomyFieldDefensePolicy.ProjectedResourceHazardRadius(range,
						Info.ResourceModifierSafetyMarginCells)));
			}

			return cells;
		}

		void UpdateOrders()
		{
			var used = new HashSet<CPos>();
			foreach (var field in fields.Values.OrderBy(f => f.HarvesterId))
			{
				Position(field, field.Tanks, used, false);
				Position(field, field.Infantry, used, false);
				Position(field, field.AntiAir, used, false);
			}
		}

		void OwnDefensiveStance(Actor actor)
		{
			var infantry = IsInfantry(actor);
			var hazards = EconomyFieldDefensePolicy.RequiresProjectedResourceSafety(infantry) ?
				ownedMovementHazards : refineryTraffic;
			var encodedSafety = MoveAlongPath.EncodeSafetyCells(hazards);
			if (!lastSafetyStates.TryGetValue(actor.ActorID, out var previousSafety) || previousSafety != encodedSafety)
			{
				bot.QueueOrder(MoveAlongPath.CreateSafetyOrder(actor, true, hazards));
				lastSafetyStates[actor.ActorID] = encodedSafety;
			}

			var autoTarget = actor?.TraitOrDefault<AutoTarget>();
			if (autoTarget == null || originalStances.ContainsKey(actor.ActorID))
				return;

			originalStances.Add(actor.ActorID, autoTarget.Stance);
			if (autoTarget.Stance != UnitStance.Defend)
				bot.QueueOrder(new Order("SetUnitStance", actor, false) { ExtraData = (uint)UnitStance.Defend });

			// The previous owner may have left an explicit attack activity whose movement permission
			// is fixed when it is created. Clear it before issuing the exact station route below.
			bot.QueueOrder(new Order("Stop", actor, false));
			Debug("owned stance defender={0} original={1} active={2}", actor.ActorID, autoTarget.Stance, UnitStance.Defend);
		}

		void RestoreStance(uint actorId)
		{
			var actor = world.GetActorById(actorId);
			if (bot != null && actor?.TraitOrDefault<MoveAlongPath>() != null)
				bot.QueueOrder(MoveAlongPath.CreateSafetyOrder(actor, false));
			lastSafetyStates.Remove(actorId);
			if (!originalStances.TryGetValue(actorId, out var stance))
				return;

			originalStances.Remove(actorId);
			if (bot != null && IsOwnedUsable(actor) && actor.Info.HasTraitInfo<AutoTargetInfo>())
			{
				bot.QueueOrder(new Order("SetUnitStance", actor, false) { ExtraData = (uint)stance });
				Debug("restored stance defender={0} stance={1}", actorId, stance);
			}
		}

		bool CanSynchronizeMovementSafety(Actor actor)
		{
			var moveAlongPath = actor?.Info.TraitInfoOrDefault<MoveAlongPathInfo>();
			if (moveAlongPath == null)
				return false;

			var hazards = EconomyFieldDefensePolicy.RequiresProjectedResourceSafety(IsInfantry(actor)) ?
				ownedMovementHazards : refineryTraffic;
			return hazards.Count <= moveAlongPath.MaximumSafetyCells;
		}

		void UpdateProductionRequests()
		{
			productionRequester = productionRequesters.FirstOrDefault(Exts.IsTraitEnabled);
			if (productionRequester == null || bot == null)
				return;

			var hasRefinery = world.ActorsWithTrait<IAcceptResources>().Any(p => IsOwnedUsable(p.Actor));
			if (fields.Count == 0 || !hasRefinery || (powerManager != null && powerManager.ExcessPower < 0))
			{
				CancelProductionRequests(fields.Count == 0 ? "no committed fields" :
					!hasRefinery ? "refinery recovery" : "power recovery");
				return;
			}

			UpdateRoleProduction(Info.TankTypes, fields.Count * Info.TanksPerHarvester,
				fields.Values.Sum(f => f.Tanks.Count));
			UpdateRoleProduction(Info.InfantryTypes, fields.Count * Info.InfantryPerHarvester,
				fields.Values.Sum(f => f.Infantry.Count));
			UpdateRoleProduction(Info.AntiAirTypes, fields.Count * Info.AntiAirPerHarvester,
				fields.Values.Sum(f => f.AntiAir.Count));
		}

		void UpdateRoleProduction(HashSet<string> types, int target, int assigned)
		{
			var actorType = types.OrderBy(t => t, StringComparer.Ordinal).First();
			var owned = productionRequester.RequestedProductionCount(bot, ProductionRequestOwner, actorType);
			var queued = world.ActorsWithTrait<ProductionQueue>().Where(q => q.Actor.Owner == player)
				.Sum(q => q.Trait.AllQueued().Count(item => item.Item == actorType));
			var needed = EconomyFieldDefensePolicy.OutstandingRequestDemand(target, assigned, queued, owned,
				Info.MaximumOutstandingRequestsPerRole);

			if (target <= assigned + queued && owned > 0)
			{
				productionRequester.CancelRequestedUnitProduction(bot, ProductionRequestOwner, actorType);
				Debug("cancelled owned production type={0} target={1} assigned={2} queued={3}",
					actorType, target, assigned, queued);
				return;
			}

			if (needed == 0 || !HasBuildableQueue(actorType))
				return;

			for (var i = 0; i < needed; i++)
				productionRequester.RequestUnitProduction(bot, ProductionRequestOwner, actorType);
			Debug("requested production type={0} target={1} assigned={2} queued={3} owned={4}",
				actorType, target, assigned, queued, owned + needed);
		}

		bool HasBuildableQueue(string actorType)
		{
			if (!world.Map.Rules.Actors.TryGetValue(actorType, out var actorInfo))
				return false;

			var buildable = actorInfo.TraitInfoOrDefault<BuildableInfo>();
			return buildable != null && buildable.Queue.Any(queueType => AIUtils.FindQueues(player, queueType)
				.Any(queue => queue.BuildableItems().Any(item => item.Name == actorType)));
		}

		void CancelProductionRequests(string reason)
		{
			if (bot == null)
				return;

			var cancelled = false;
			foreach (var requester in productionRequesters ?? Array.Empty<IBotRequestOwnedUnitProduction>())
				foreach (var actorType in Info.TankTypes.Concat(Info.InfantryTypes).Concat(Info.AntiAirTypes)
					.Distinct().OrderBy(t => t, StringComparer.Ordinal))
					if (requester.RequestedProductionCount(bot, ProductionRequestOwner, actorType) > 0)
					{
						requester.CancelRequestedUnitProduction(bot, ProductionRequestOwner, actorType);
						cancelled = true;
					}

			if (cancelled)
				Debug("cancelled all owned production reason={0}", reason);
		}

		void Position(FieldAssignment field, HashSet<uint> actorIds, HashSet<CPos> used, bool attackMove)
		{
			foreach (var actor in actorIds.Select(world.GetActorById).Where(IsOwnedUsable).OrderBy(a => a.ActorID).ToArray())
			{
				LogUnsafeOccupancy(actor, field);
				var destinationChanged = false;
				if (!field.Destinations.TryGetValue(actor.ActorID, out var destination) || used.Contains(destination) ||
					!IsSafeCell(actor, destination) ||
					(refineryTraffic.Contains(destination) && actor.Location != destination))
				{
					if (!TryFindDestination(actor, field.Station, used, out destination, out _))
					{
						Release(field, actor, "no-safe-route");
						continue;
					}

					field.Destinations[actor.ActorID] = destination;
					routeProgress.Remove(actor.ActorID);
					destinationChanged = true;
				}

				used.Add(destination);
				var distance = (actor.CenterPosition - world.Map.CenterOfCell(destination)).HorizontalLengthSquared;
				var withinTolerance = EconomyFieldDefensePolicy.IsWithinFormation(actor.Location,
					destination, distance, Info.FormationToleranceCells);
				var outsideLeash = EconomyFieldDefensePolicy.ShouldReform(distance,
					Info.FormationToleranceCells, Info.EngagementLeashCells);
				var busyAttack = IsBusyAttacking(actor);
				var reason = destinationChanged ? "new-destination" : actor.IsIdle ? "idle-retry" :
					busyAttack ? "pursuit-break" : "stalled-route";

				if (routeProgress.TryGetValue(actor.ActorID, out var progress) && progress.EnRoute)
				{
					if (distance < progress.BestDistanceSquared)
					{
						progress.BestDistanceSquared = distance;
						progress.LastProgressTick = world.WorldTick;
					}

					if (withinTolerance)
					{
						progress.EnRoute = false;
						continue;
					}

					if (!busyAttack && world.WorldTick < progress.LastProgressTick + Info.RouteStallTicks)
						continue;
				}

				var needsMove = destinationChanged ? !withinTolerance : actor.IsIdle ? !withinTolerance : outsideLeash;
				if (!needsMove ||
					(lastOrderTicks.TryGetValue(actor.ActorID, out var last) && world.WorldTick < last + Info.OrderInterval))
					continue;

				if (!TryFindSafePath(actor, destination, out var path))
				{
					Release(field, actor, "route-invalidated");
					continue;
				}

				if (path.Count < 2)
				{
					if (routeProgress.TryGetValue(actor.ActorID, out var settled))
						settled.EnRoute = false;
					continue;
				}

				QueueRoute(actor, destination, path, attackMove);
				lastOrderTicks[actor.ActorID] = world.WorldTick;
				if (!routeProgress.TryGetValue(actor.ActorID, out progress))
				{
					progress = new RouteProgress();
					routeProgress.Add(actor.ActorID, progress);
				}

				progress.BestDistanceSquared = distance;
				progress.LastProgressTick = world.WorldTick;
				progress.EnRoute = true;
				Debug("reform defender={0} field={1} destination={2} waypoints={3} reason={4}", actor.ActorID,
					field.HarvesterId, destination, path.Count, reason);
			}
		}

		void LogUnsafeOccupancy(Actor actor, FieldAssignment field)
		{
			var resourceType = resourceLayer.GetResource(actor.Location).Type;
			var resource = resourceType != null && Info.AvoidResourceTypes.Contains(resourceType);
			var traffic = refineryTraffic.Contains(actor.Location);
			var forbidden = traffic || (resource && IsInfantry(actor));
			if (!resource && !traffic)
			{
				lastUnsafeOccupancy.Remove(actor.ActorID);
				return;
			}

			if (!lastUnsafeOccupancy.TryGetValue(actor.ActorID, out var previous) || previous != actor.Location)
			{
				Debug(forbidden ?
					"forbidden occupancy defender={0} type={1} field={2} cell={3} resource={4} traffic={5}" :
					"preferred resource avoidance missed defender={0} type={1} field={2} cell={3} resource={4} traffic={5}",
					actor.ActorID, actor.Info.Name, field.HarvesterId, actor.Location,
					resourceType ?? "none", traffic);
				lastUnsafeOccupancy[actor.ActorID] = actor.Location;
			}
		}

		static bool IsBusyAttacking(Actor actor)
		{
			if (actor.IsIdle || actor.CurrentActivity == null)
				return false;

			if (actor.CurrentActivity.GetType() == typeof(OpenRA.Mods.Common.Activities.Attack))
				return true;

			return actor.CurrentActivity.NextActivity?.GetType() == typeof(OpenRA.Mods.Common.Activities.Attack);
		}

		bool TryFindDestination(Actor actor, CPos station, HashSet<CPos> used, out CPos destination, out List<CPos> path)
		{
			foreach (var candidate in world.Map.FindTilesInAnnulus(station,
				Info.StationMinimumRadiusCells, Info.StationMaximumRadiusCells)
					.Where(c => !used.Contains(c) && IsSafeCell(actor, c))
					.OrderBy(c => (world.Map.CenterOfCell(c) - world.Map.CenterOfCell(station)).HorizontalLengthSquared)
					.ThenBy(c => c.Y).ThenBy(c => c.X))
			{
				var mobile = actor.TraitOrDefault<Mobile>();
				if (mobile == null || (actor.Location != candidate && !mobile.CanEnterCell(candidate, check: BlockedByActor.Immovable)) ||
					!TryFindSafePath(actor, candidate, out path))
					continue;

				destination = candidate;
				return true;
			}

			destination = CPos.Zero;
			path = null;
			return false;
		}

		bool TryFindSafePath(Actor actor, CPos destination, out List<CPos> path)
		{
			var mobile = actor.TraitOrDefault<Mobile>();
			if (mobile == null)
			{
				path = null;
				return false;
			}

			using (var search = PathSearch.ToTargetCellByPredicate(world, mobile.Locomotor, actor,
				new[] { actor.Location }, c => c == destination, BlockedByActor.Immovable,
				c => IsSafeCell(actor, c) ? 0 : PathGraph.PathCostForInvalidPath))
				path = mobile.Pathfinder.FindPath(search);

			return path.Count > 0 && path.Count <= Info.MaximumRouteCells;
		}

		bool IsSafeCell(Actor actor, CPos cell)
		{
			if (!world.Map.Contains(cell) || refineryTraffic.Contains(cell) ||
				(EconomyFieldDefensePolicy.RequiresProjectedResourceSafety(IsInfantry(actor)) &&
					projectedResourceHazards.Contains(cell)))
				return false;

			return world.Map.FindTilesInAnnulus(cell, 0, Info.ResourceSafetyMarginCells).All(c =>
			{
				var resourceType = resourceLayer.GetResource(c).Type;
				return resourceType == null || !Info.AvoidResourceTypes.Contains(resourceType);
			});
		}

		bool IsInfantry(Actor actor)
		{
			return actor != null && Info.InfantryTypes.Contains(actor.Info.Name);
		}

		bool HasAvoidedResource(CPos cell)
		{
			var resourceType = resourceLayer.GetResource(cell).Type;
			return resourceType != null && Info.AvoidResourceTypes.Contains(resourceType);
		}

		void QueueRoute(Actor actor, CPos destination, List<CPos> path, bool attackMove)
		{
			bot.QueueOrder(MoveAlongPath.CreateOrder(world, actor, path, attackMove));
		}

		void Release(FieldAssignment field, Actor actor, string reason)
		{
			RestoreStance(actor.ActorID);
			field.Tanks.Remove(actor.ActorID);
			field.Infantry.Remove(actor.ActorID);
			field.AntiAir.Remove(actor.ActorID);
			field.Destinations.Remove(actor.ActorID);
			reserved.Remove(actor.ActorID);
			lastOrderTicks.Remove(actor.ActorID);
			routeProgress.Remove(actor.ActorID);
			if (reason == "no-safe-route" || reason == "route-invalidated")
				routeRejectedUntil[actor.ActorID] = world.WorldTick + Info.RouteRetryTicks;

			Debug("released defender={0} field={1} reason={2}", actor.ActorID, field.HarvesterId, reason);
		}

		void ClearState(string reason)
		{
			CancelProductionRequests(reason);
			foreach (var id in originalStances.Keys.ToArray())
				RestoreStance(id);
			if (fields.Count > 0 || reserved.Count > 0)
				Debug("released all fields reason={0}", reason);

			fields.Clear();
			reserved.Clear();
			lastFieldCompositions.Clear();
			lastOrderTicks.Clear();
			routeProgress.Clear();
			routeRejectedUntil.Clear();
			lastUnsafeOccupancy.Clear();
			originalStances.Clear();
			lastSafetyStates.Clear();
			lastComposition = null;
			lastFieldCompositions.Clear();
		}

		void LogComposition()
		{
			foreach (var field in fields.Values.OrderBy(f => f.HarvesterId))
			{
				var fieldComposition = $"tanks={field.Tanks.Count} infantry={field.Infantry.Count} aa={field.AntiAir.Count}";
				if (!lastFieldCompositions.TryGetValue(field.HarvesterId, out var previous) || previous != fieldComposition)
				{
					Debug("field composition harvester={0} station={1} {2}",
						field.HarvesterId, field.Station, fieldComposition);
					lastFieldCompositions[field.HarvesterId] = fieldComposition;
				}
			}

			var composition = $"fields={fields.Count} tanks={fields.Values.Sum(f => f.Tanks.Count)}" +
				$" infantry={fields.Values.Sum(f => f.Infantry.Count)} aa={fields.Values.Sum(f => f.AntiAir.Count)}";
			if (composition == lastComposition)
				return;

			Debug("composition {0}", composition);
			lastComposition = composition;
		}

		void Debug(string format, params object[] args)
		{
			if (!Info.DebugLogging)
				return;

			var message = string.Format(format, args);
			AIUtils.BotDebug("AI ({0}) economy field defense: {1}", player.ClientIndex, message);
			Log.Write("debug", "AI economy field defense: {0} (client {1}) at tick {2}: {3}",
				player, player.ClientIndex, world.WorldTick, message);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			var fieldNodes = fields.Values.OrderBy(f => f.HarvesterId).Select(field =>
			{
				var destinations = field.Destinations.OrderBy(p => p.Key).ToArray();
				return new MiniYamlNode("Field", FieldSaver.FormatValue(field.HarvesterId), new List<MiniYamlNode>
				{
					new MiniYamlNode("Station", FieldSaver.FormatValue(field.Station)),
					new MiniYamlNode("Tanks", FieldSaver.FormatValue(field.Tanks.OrderBy(id => id).ToArray())),
					new MiniYamlNode("Infantry", FieldSaver.FormatValue(field.Infantry.OrderBy(id => id).ToArray())),
					new MiniYamlNode("AntiAir", FieldSaver.FormatValue(field.AntiAir.OrderBy(id => id).ToArray())),
					new MiniYamlNode("DestinationActors", FieldSaver.FormatValue(destinations.Select(p => p.Key).ToArray())),
					new MiniYamlNode("DestinationCells", FieldSaver.FormatValue(destinations.Select(p => p.Value.Bits).ToArray()))
				});
			}).ToList();
			var stances = originalStances.OrderBy(p => p.Key).ToArray();
			var routes = lastOrderTicks.Keys.Concat(routeProgress.Keys).Distinct()
				.Where(reserved.Contains).OrderBy(id => id).Select(id =>
				{
					var nodes = new List<MiniYamlNode>
					{
						new MiniYamlNode("Actor", FieldSaver.FormatValue(id))
					};
					if (lastOrderTicks.TryGetValue(id, out var lastOrder))
						nodes.Add(new MiniYamlNode("LastOrder", FieldSaver.FormatValue(lastOrder)));

					if (routeProgress.TryGetValue(id, out var progress))
					{
						nodes.Add(new MiniYamlNode("BestDistance", FieldSaver.FormatValue(progress.BestDistanceSquared)));
						nodes.Add(new MiniYamlNode("LastProgress", FieldSaver.FormatValue(progress.LastProgressTick)));
						nodes.Add(new MiniYamlNode("EnRoute", FieldSaver.FormatValue(progress.EnRoute)));
					}

					return new MiniYamlNode("Route", "", nodes);
				}).ToList();
			var rejectedRoutes = routeRejectedUntil.Where(p => p.Value > world.WorldTick)
				.OrderBy(p => p.Key).Select(p => new MiniYamlNode("RejectedRoute", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("Actor", FieldSaver.FormatValue(p.Key)),
					new MiniYamlNode("RetryTick", FieldSaver.FormatValue(p.Value))
				})).ToList();

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("EconomyFieldDefenseScanTicks", FieldSaver.FormatValue(scanTicks)),
				new MiniYamlNode("EconomyFieldDefenseNextScanTick",
					FieldSaver.FormatValue(world.WorldTick + Math.Max(1, scanTicks))),
				new MiniYamlNode("EconomyFieldDefenseStanceActors", FieldSaver.FormatValue(stances.Select(p => p.Key).ToArray())),
				new MiniYamlNode("EconomyFieldDefenseStances", FieldSaver.FormatValue(stances.Select(p => (int)p.Value).ToArray())),
				new MiniYamlNode("EconomyFieldDefenseFields", "", fieldNodes),
				new MiniYamlNode("EconomyFieldDefenseRoutes", "", routes),
				new MiniYamlNode("EconomyFieldDefenseRejectedRoutes", "", rejectedRoutes)
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			fields.Clear();
			reserved.Clear();
			lastOrderTicks.Clear();
			routeProgress.Clear();
			routeRejectedUntil.Clear();
			lastUnsafeOccupancy.Clear();
			originalStances.Clear();
			lastSafetyStates.Clear();
			var nextScanNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseNextScanTick");
			if (nextScanNode != null)
			{
				var nextScanTick = FieldLoader.GetValue<int>(nextScanNode.Key, nextScanNode.Value.Value);
				scanTicks = EconomyFieldDefensePolicy.RestoredScanTicks(nextScanTick,
					world.WorldTick, Info.ScanInterval);
				Debug("restored scan phase next={0} current={1} ticks={2}",
					nextScanTick, world.WorldTick, scanTicks);
			}
			else
			{
				var scanNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseScanTicks");
				if (scanNode != null)
				{
					var savedScanTicks = FieldLoader.GetValue<int>(scanNode.Key, scanNode.Value.Value);
					var legacyNextScanTick = world.WorldTick - 1 + Math.Max(1, savedScanTicks);
					scanTicks = EconomyFieldDefensePolicy.RestoredScanTicks(legacyNextScanTick,
						world.WorldTick, Info.ScanInterval);
					Debug("restored legacy scan phase saved={0} next={1} current={2} ticks={3}",
						savedScanTicks, legacyNextScanTick, world.WorldTick, scanTicks);
				}
			}

			var stanceActorNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseStanceActors");
			var stanceNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseStances");
			if (stanceActorNode != null && stanceNode != null)
			{
				var actors = FieldLoader.GetValue<uint[]>(stanceActorNode.Key, stanceActorNode.Value.Value);
				var stances = FieldLoader.GetValue<int[]>(stanceNode.Key, stanceNode.Value.Value);
				for (var i = 0; i < Math.Min(actors.Length, stances.Length); i++)
					if (Enum.IsDefined(typeof(UnitStance), stances[i]) && !originalStances.ContainsKey(actors[i]))
						originalStances.Add(actors[i], (UnitStance)stances[i]);
			}

			var fieldsNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseFields");
			if (fieldsNode == null)
				return;

			foreach (var node in fieldsNode.Value.Nodes.Where(n => n.Key == "Field"))
			{
				var harvesterId = FieldLoader.GetValue<uint>(node.Key, node.Value.Value);
				var stationNode = node.Value.Nodes.FirstOrDefault(n => n.Key == "Station");
				if (stationNode == null || fields.ContainsKey(harvesterId))
					continue;

				var field = new FieldAssignment(harvesterId,
					FieldLoader.GetValue<CPos>(stationNode.Key, stationNode.Value.Value));
				LoadIds(field.Tanks, node, "Tanks");
				LoadIds(field.Infantry, node, "Infantry");
				LoadIds(field.AntiAir, node, "AntiAir");
				var actorNode = node.Value.Nodes.FirstOrDefault(n => n.Key == "DestinationActors");
				var cellNode = node.Value.Nodes.FirstOrDefault(n => n.Key == "DestinationCells");
				if (actorNode != null && cellNode != null)
				{
					var actors = FieldLoader.GetValue<uint[]>(actorNode.Key, actorNode.Value.Value);
					var cells = FieldLoader.GetValue<int[]>(cellNode.Key, cellNode.Value.Value);
					for (var i = 0; i < Math.Min(actors.Length, cells.Length); i++)
						field.Destinations[actors[i]] = new CPos(cells[i]);
				}

				fields.Add(harvesterId, field);
				reserved.UnionWith(field.Tanks);
				reserved.UnionWith(field.Infantry);
				reserved.UnionWith(field.AntiAir);
			}

			RestoreRouteState(data);
		}

		void RestoreRouteState(List<MiniYamlNode> data)
		{
			var routesNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseRoutes");
			if (routesNode != null)
				foreach (var node in routesNode.Value.Nodes.Where(n => n.Key == "Route"))
				{
					T Load<T>(string key, T fallback = default(T))
					{
						var value = node.Value.Nodes.FirstOrDefault(n => n.Key == key);
						return value == null ? fallback : FieldLoader.GetValue<T>(key, value.Value.Value);
					}

					var actorId = Load<uint>("Actor");
					var actor = world.GetActorById(actorId);
					if (actorId == 0 || !reserved.Contains(actorId) || !IsOwnedUsable(actor))
						continue;

					var lastOrderNode = node.Value.Nodes.FirstOrDefault(n => n.Key == "LastOrder");
					if (lastOrderNode != null)
					{
						var lastOrder = FieldLoader.GetValue<int>(lastOrderNode.Key, lastOrderNode.Value.Value);
						if (lastOrder >= 0 && lastOrder <= world.WorldTick)
							lastOrderTicks[actorId] = lastOrder;
					}

					var bestDistanceNode = node.Value.Nodes.FirstOrDefault(n => n.Key == "BestDistance");
					var lastProgressNode = node.Value.Nodes.FirstOrDefault(n => n.Key == "LastProgress");
					var enRouteNode = node.Value.Nodes.FirstOrDefault(n => n.Key == "EnRoute");
					if (bestDistanceNode == null || lastProgressNode == null || enRouteNode == null)
						continue;

					var bestDistance = Load<long>("BestDistance", -1);
					var lastProgress = Load<int>("LastProgress", -1);
					if (bestDistance < 0 || lastProgress < 0 || lastProgress > world.WorldTick)
						continue;

					routeProgress[actorId] = new RouteProgress
					{
						BestDistanceSquared = bestDistance,
						LastProgressTick = lastProgress,
						EnRoute = Load<bool>("EnRoute")
					};
					Debug("restored route defender={0} last-order={1} best-distance={2} last-progress={3} en-route={4}",
						actorId, lastOrderTicks.TryGetValue(actorId, out var lastOrderTick) ? lastOrderTick : -1,
						bestDistance, lastProgress, routeProgress[actorId].EnRoute);
				}

			var rejectedNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseRejectedRoutes");
			if (rejectedNode == null)
				return;

			foreach (var node in rejectedNode.Value.Nodes.Where(n => n.Key == "RejectedRoute"))
			{
				var actorNode = node.Value.Nodes.FirstOrDefault(n => n.Key == "Actor");
				var retryNode = node.Value.Nodes.FirstOrDefault(n => n.Key == "RetryTick");
				if (actorNode == null || retryNode == null)
					continue;

				var actorId = FieldLoader.GetValue<uint>(actorNode.Key, actorNode.Value.Value);
				var retryTick = FieldLoader.GetValue<int>(retryNode.Key, retryNode.Value.Value);
				var actor = world.GetActorById(actorId);
				if (actorId != 0 && !reserved.Contains(actorId) && IsOwnedUsable(actor) &&
					retryTick > world.WorldTick && retryTick <= world.WorldTick + Info.RouteRetryTicks)
				{
					routeRejectedUntil[actorId] = retryTick;
					Debug("restored route rejection defender={0} retry-tick={1}", actorId, retryTick);
				}
			}
		}

		static void LoadIds(HashSet<uint> destination, MiniYamlNode parent, string key)
		{
			var node = parent.Value.Nodes.FirstOrDefault(n => n.Key == key);
			if (node != null)
				destination.UnionWith(FieldLoader.GetValue<uint[]>(node.Key, node.Value.Value));
		}
	}
}
