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
		public readonly bool LowFrequencyTriggerControl = true;
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
		IBotEnabled, IBotTick, IBotUnitReservations, IBotRespondToAttack, IGameSaveTraitData, IAdvancedBotTick
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
		readonly EconomyFieldDefenseDirtyAssignments dirtyAssignments = new EconomyFieldDefenseDirtyAssignments();
		readonly Dictionary<uint, string> dirtyReasons = new Dictionary<uint, string>();
		readonly Dictionary<uint, CPos> dirtyEnemyTargets = new Dictionary<uint, CPos>();
		readonly Dictionary<uint, CPos> lastUrgentTargets = new Dictionary<uint, CPos>();
		readonly Dictionary<uint, int> lastUrgentOrderTicks = new Dictionary<uint, int>();
		IBot bot;
		IBotUnitReservations[] otherReservations;
		IBotTransportReservations[] transportReservations;
		IUnassignedCombatUnitRegistry unassignedCombatUnits;
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
		bool advancedBehaviorEnabled = true;
		int scanTicks = 1;
		int routineScans;
		int dirtyEnqueued;
		int dirtyDeduplicated;
		int dirtyProcessed;
		int dirtyNoOp;
		int dirtyRecoveries;
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
			unassignedCombatUnits = player.PlayerActor.TraitOrDefault<IUnassignedCombatUnitRegistry>();
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

		void IBotRespondToAttack.RespondToAttack(IBot enabledBot, Actor self, AttackInfo e)
		{
			if (!Info.LowFrequencyTriggerControl || IsTraitDisabled || player.WinState != WinState.Undefined || self == null ||
				self.Owner != player || e.Damage.Value <= 0 || e.Attacker == null || e.Attacker == self ||
				player.RelationshipWith(e.Attacker.Owner) != PlayerRelationship.Enemy)
				return;

			if (fields.ContainsKey(self.ActorID))
			{
				var field = fields[self.ActorID];
				var detected = IsDetectedEnemy(e.Attacker);
				foreach (var actorId in field.Tanks.Concat(field.Infantry).Concat(field.AntiAir).OrderBy(id => id))
					MarkDirty(field.HarvesterId, actorId, "associated-harvester-attacked",
						detected ? e.Attacker.Location : (CPos?)null);

				Debug("associated harvester attack field={0} guard-wake-count={1} detected={2}", field.HarvesterId,
					field.Tanks.Count + field.Infantry.Count + field.AntiAir.Count, detected);
				return;
			}

			foreach (var field in fields.Values.OrderBy(field => field.HarvesterId))
				if (IsAssigned(field, self.ActorID))
				{
					MarkDirty(field.HarvesterId, self.ActorID, "assigned-guard-attacked",
						IsDetectedEnemy(e.Attacker) ? e.Attacker.Location : (CPos?)null);
					return;
				}
		}

		bool IsDetectedEnemy(Actor actor)
		{
			return IsOwnedUsableEnemy(actor) && player.Shroud.IsVisible(actor.Location) && actor.CanBeViewedByPlayer(player);
		}

		bool IsOwnedUsableEnemy(Actor actor)
		{
			return actor != null && actor.IsInWorld && !actor.IsDead &&
				player.RelationshipWith(actor.Owner) == PlayerRelationship.Enemy;
		}

		string IAdvancedBotTick.FailsafeModuleId => "EconomyFieldDefenseBotModule";

		void IAdvancedBotTick.SetAdvancedBehaviorEnabled(bool enabled)
		{
			if (advancedBehaviorEnabled == enabled)
				return;

			advancedBehaviorEnabled = enabled;
			if (!enabled)
			{
				var releasedActors = reserved.Select(world.GetActorById).Where(IsOwnedUsable).OrderBy(a => a.ActorID).ToArray();
				squadManager?.RetainFailsafeReleasedActors("EconomyFieldDefenseBotModule", releasedActors);
				if (Info.DebugLogging && reserved.Count > 0)
					Debug("released all fields reason=failsafe-degraded actors={0}", string.Join(",",
						releasedActors.Select(a => a.Info.Name + "#" + a.ActorID)));

				ClearState("failsafe degraded");
			}
			else
			{
				scanTicks = 1;
				Debug("enabled for recovery probe");
			}
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (IsTraitDisabled || !advancedBehaviorEnabled || player.WinState != WinState.Undefined)
				return;

			if (Info.LowFrequencyTriggerControl)
			{
				DetectCachedInvalidations();
				if (--scanTicks > 0)
				{
					ProcessDirtyAssignments();
					return;
				}
			}
			else if (--scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			if (!techTree.HasPrerequisites(Info.RequiredPrerequisites))
			{
				ClearState("economy capability unavailable");
				return;
			}

			routineScans++;
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
			dirtyAssignments.Clear();
			dirtyReasons.Clear();
			dirtyEnemyTargets.Clear();
			Debug("routine scan={0} next={1} fields={2} dirty-enqueued={3} dirty-deduplicated={4}" +
				" dirty-processed={5} dirty-noop={6} dirty-recoveries={7}", routineScans,
				world.WorldTick + Info.ScanInterval, fields.Count, dirtyEnqueued, dirtyDeduplicated,
				dirtyProcessed, dirtyNoOp, dirtyRecoveries);
		}

		void DetectCachedInvalidations()
		{
			foreach (var field in fields.Values.OrderBy(field => field.HarvesterId).ToArray())
			{
				var harvester = world.GetActorById(field.HarvesterId);
				var station = harvester?.TraitOrDefault<IHarvesterFieldStation>();
				if (!IsOwnedUsable(harvester) || station == null || !station.HasCommittedField ||
					station.CommittedField != field.Station)
					MarkDirty(field.HarvesterId, field.HarvesterId, "committed-field-invalidated");

				foreach (var actorId in field.Tanks.Concat(field.Infantry).Concat(field.AntiAir).OrderBy(id => id))
					if (!IsOwnedUsable(world.GetActorById(actorId)))
						MarkDirty(field.HarvesterId, actorId, "assigned-guard-lost");
			}
		}

		void MarkDirty(uint fieldId, uint actorId, string reason, CPos? detectedEnemy = null)
		{
			if (!dirtyAssignments.Enqueue(fieldId, actorId))
			{
				dirtyDeduplicated++;
				if (EconomyFieldDefensePolicy.MergeDetectedDirtyEvent(actorId, reason, detectedEnemy,
					dirtyReasons, dirtyEnemyTargets))
					Debug("dirty merge field={0} actor={1} result=target-upgraded target={2} reason={3}",
						fieldId, actorId, detectedEnemy.Value, reason);

				return;
			}

			dirtyEnqueued++;
			dirtyReasons[actorId] = reason;
			EconomyFieldDefensePolicy.MergeDetectedDirtyEvent(actorId, reason, detectedEnemy,
				dirtyReasons, dirtyEnemyTargets);
			Debug("dirty enqueue field={0} actor={1} reason={2}", fieldId, actorId, reason);
		}

		void ProcessDirtyAssignments()
		{
			if (dirtyAssignments.Count == 0)
				return;

			if (!techTree.HasPrerequisites(Info.RequiredPrerequisites))
			{
				ClearState("economy capability unavailable");
				return;
			}

			RefreshSquadManager();
			foreach (var pending in dirtyAssignments.Drain())
			{
				dirtyProcessed++;
				var reason = dirtyReasons.TryGetValue(pending.ActorId, out var dirtyReason) ?
					dirtyReason : "cached-validity";
				dirtyReasons.Remove(pending.ActorId);
				var hasEnemyTarget = dirtyEnemyTargets.TryGetValue(pending.ActorId, out var enemyTarget);
				dirtyEnemyTargets.Remove(pending.ActorId);
				if (!fields.TryGetValue(pending.FieldId, out var field))
					continue;

				if (pending.ActorId == field.HarvesterId)
				{
					ProcessDirtyHarvester(field, reason);
					continue;
				}

				ProcessDirtyGuard(field, pending.ActorId, reason, hasEnemyTarget ? enemyTarget : (CPos?)null);
			}
		}

		void ProcessDirtyHarvester(FieldAssignment field, string reason)
		{
			var harvester = world.GetActorById(field.HarvesterId);
			var station = harvester?.TraitOrDefault<IHarvesterFieldStation>();
			if (!IsOwnedUsable(harvester) || station == null || !station.HasCommittedField)
			{
				dirtyRecoveries++;
				RemoveField(field.HarvesterId, reason);
				UpdateProductionRequests();
				Debug("dirty validation field={0} actor={1} result=field-released reason={2}",
					field.HarvesterId, field.HarvesterId, reason);
				return;
			}

			if (field.Station == station.CommittedField)
			{
				dirtyNoOp++;
				Debug("dirty validation field={0} actor={1} result=ordinary-targeting-no-order reason={2}",
					field.HarvesterId, field.HarvesterId, reason);
				return;
			}

			var oldStation = field.Station;
			field.Station = station.CommittedField;
			field.Destinations.Clear();
			dirtyRecoveries++;
			Debug("dirty validation field={0} actor={1} result=station-transition old={2} new={3} reason={4}",
				field.HarvesterId, field.HarvesterId, oldStation, field.Station, reason);
			RebalanceField(field);
			PositionField(field);
			UpdateProductionRequests();
			LogComposition();
		}

		void ProcessDirtyGuard(FieldAssignment field, uint actorId, string reason, CPos? detectedEnemy)
		{
			var role = AssignedRole(field, actorId);
			if (role == null)
				return;

			var actor = world.GetActorById(actorId);
			var rejection = ClaimRejectionReason(actor, field.Station, false);
			if (rejection != null)
			{
				dirtyRecoveries++;
				ReleaseAssigned(field, actorId, rejection);
				FillRole(field, role);
				PositionField(field);
				UpdateProductionRequests();
				LogComposition();
				Debug("dirty validation field={0} actor={1} result=replaced-or-stable-deficit reason={2}:{3}",
					field.HarvesterId, actorId, reason, rejection);
				return;
			}

			if (detectedEnemy.HasValue && HandleUrgentAttackMove(field, actor, detectedEnemy.Value, reason,
				out var urgentOrderIssued))
			{
				if (urgentOrderIssued)
					dirtyRecoveries++;
				else
					dirtyNoOp++;
				return;
			}

			var ordersBefore = lastOrderTicks.TryGetValue(actorId, out var lastOrder) ? lastOrder : -1;
			var destinationBefore = field.Destinations.TryGetValue(actorId, out var destination) ? destination : CPos.Zero;
			var used = CachedDestinationsExcept(actorId);
			PositionActor(field, actor, used, false);
			var recovered = !IsAssigned(field, actorId) ||
				(lastOrderTicks.TryGetValue(actorId, out var currentOrder) && currentOrder != ordersBefore) ||
				(field.Destinations.TryGetValue(actorId, out var currentDestination) && currentDestination != destinationBefore);
			if (recovered)
			{
				dirtyRecoveries++;
				FillRole(field, role);
				PositionField(field);
				UpdateProductionRequests();
				Debug("dirty validation field={0} actor={1} result=local-recovery reason={2}",
					field.HarvesterId, actorId, reason);
			}
			else
			{
				dirtyNoOp++;
				Debug("dirty validation field={0} actor={1} result=ordinary-targeting-no-order reason={2}",
					field.HarvesterId, actorId, reason);
			}
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
				var actor = world.GetActorById(id);
				if (actor != null)
					unassignedCombatUnits?.RegisterReleasedActors(new[] { actor });
				RestoreStance(id);
				reserved.Remove(id);
				lastOrderTicks.Remove(id);
				routeProgress.Remove(id);
				routeRejectedUntil.Remove(id);
				lastUrgentTargets.Remove(id);
				lastUrgentOrderTicks.Remove(id);
			}

			fields.Remove(harvesterId);
			dirtyAssignments.RemoveField(harvesterId);
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

			unassignedCombatUnits?.ClaimActors(reserved.Select(world.GetActorById).Where(a => a != null));
		}

		void RebalanceField(FieldAssignment field)
		{
			var releases = Info.DebugLogging ? new List<ReleaseDiagnostic>() : null;
			Prune(field, field.Tanks, Info.TankTypes, "tank", releases);
			Prune(field, field.Infantry, Info.InfantryTypes, "infantry", releases);
			Prune(field, field.AntiAir, Info.AntiAirTypes, "anti-air", releases);
			Fill(field, field.Tanks, Info.TankTypes, Info.TanksPerHarvester);
			Fill(field, field.Infantry, Info.InfantryTypes, Info.InfantryPerHarvester);
			Fill(field, field.AntiAir, Info.AntiAirTypes, Info.AntiAirPerHarvester);
		}

		void FillRole(FieldAssignment field, string role)
		{
			if (role == "tank")
				Fill(field, field.Tanks, Info.TankTypes, Info.TanksPerHarvester);
			else if (role == "infantry")
				Fill(field, field.Infantry, Info.InfantryTypes, Info.InfantryPerHarvester);
			else
				Fill(field, field.AntiAir, Info.AntiAirTypes, Info.AntiAirPerHarvester);
		}

		string AssignedRole(FieldAssignment field, uint actorId)
		{
			return field.Tanks.Contains(actorId) ? "tank" : field.Infantry.Contains(actorId) ?
				"infantry" : field.AntiAir.Contains(actorId) ? "anti-air" : null;
		}

		bool IsAssigned(FieldAssignment field, uint actorId) { return AssignedRole(field, actorId) != null; }

		void ReleaseAssigned(FieldAssignment field, uint actorId, string reason)
		{
			var actor = world.GetActorById(actorId);
			if (actor != null)
			{
				Release(field, actor, reason);
				return;
			}

			RestoreStance(actorId);
			field.Tanks.Remove(actorId);
			field.Infantry.Remove(actorId);
			field.AntiAir.Remove(actorId);
			field.Destinations.Remove(actorId);
			reserved.Remove(actorId);
			lastOrderTicks.Remove(actorId);
			routeProgress.Remove(actorId);
			routeRejectedUntil.Remove(actorId);
			lastUrgentTargets.Remove(actorId);
			lastUrgentOrderTicks.Remove(actorId);
			Debug("released missing defender={0} field={1} reason={2}", actorId, field.HarvesterId, reason);
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
					if (actor != null)
						unassignedCombatUnits?.RegisterReleasedActors(new[] { actor });
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

		void PositionField(FieldAssignment field)
		{
			var used = CachedDestinationsExcept(0);
			foreach (var actorId in field.Tanks.Concat(field.Infantry).Concat(field.AntiAir).OrderBy(id => id).ToArray())
			{
				var actor = world.GetActorById(actorId);
				if (IsOwnedUsable(actor))
					PositionActor(field, actor, used, false);
			}
		}

		HashSet<CPos> CachedDestinationsExcept(uint actorId)
		{
			return fields.Values.SelectMany(field => field.Destinations)
				.Where(pair => pair.Key != actorId).Select(pair => pair.Value).ToHashSet();
		}

		bool HandleUrgentAttackMove(FieldAssignment field, Actor actor, CPos enemyCell, string reason,
			out bool orderIssued)
		{
			orderIssued = false;
			if (lastUrgentTargets.TryGetValue(actor.ActorID, out var previousTarget) &&
				!EconomyFieldDefensePolicy.IsMateriallyNewUrgentTarget(previousTarget, enemyCell, 2))
			{
				Debug("urgent guard validation field={0} actor={1} result=deduplicated target={2} reason={3}",
					field.HarvesterId, actor.ActorID, enemyCell, reason);
				return true;
			}

			if (lastUrgentOrderTicks.TryGetValue(actor.ActorID, out var previousOrderTick) &&
				!EconomyFieldDefensePolicy.UrgentOrderIntervalElapsed(
					world.WorldTick, previousOrderTick, Info.OrderInterval))
			{
				Debug("urgent guard validation field={0} actor={1} result=rate-limited target={2}" +
					" previous={3} retry={4} reason={5}", field.HarvesterId, actor.ActorID, enemyCell,
					previousTarget, previousOrderTick + Info.OrderInterval, reason);
				return true;
			}

			var autoTarget = actor.TraitOrDefault<AutoTarget>();
			if (autoTarget != null && autoTarget.Stance != UnitStance.AttackAnything)
				bot.QueueOrder(new Order("SetUnitStance", actor, false) { ExtraData = (uint)UnitStance.AttackAnything });

			// The engine's AttackMove and locomotor own path selection and combat. The field-defense
			// module only points a reacting guard at the detected nearby threat; it must not perform
			// another safety search or reject the reaction because the target cell is occupied/blocked.
			bot.QueueOrder(new Order("AttackMove", actor, Target.FromCell(world, enemyCell), false));
			lastUrgentTargets[actor.ActorID] = enemyCell;
			lastUrgentOrderTicks[actor.ActorID] = world.WorldTick;
			lastOrderTicks[actor.ActorID] = world.WorldTick;
			orderIssued = true;
			Debug("urgent guard validation field={0} actor={1} result=aggressive-attack-move" +
				" enemy={2} destination={2} reason={3}", field.HarvesterId, actor.ActorID, enemyCell, reason);
			return true;
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
				PositionActor(field, actor, used, attackMove);
		}

		void PositionActor(FieldAssignment field, Actor actor, HashSet<CPos> used, bool attackMove)
		{
			LogUnsafeOccupancy(actor, field);
			var destinationChanged = false;
			if (!field.Destinations.TryGetValue(actor.ActorID, out var destination) || used.Contains(destination) ||
					!IsSafeCell(actor, destination) ||
					(refineryTraffic.Contains(destination) && actor.Location != destination) ||
					!CanOccupyCachedDestination(actor, destination))
			{
				if (!TryFindDestination(actor, field.Station, used, out destination, out _))
				{
					Release(field, actor, "no-safe-route");
					return;
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
					return;
				}

				if (!busyAttack && world.WorldTick < progress.LastProgressTick + Info.RouteStallTicks)
					return;
			}

			var needsMove = destinationChanged ? !withinTolerance : actor.IsIdle ? !withinTolerance : outsideLeash;
			if (!needsMove ||
					(lastOrderTicks.TryGetValue(actor.ActorID, out var last) && world.WorldTick < last + Info.OrderInterval))
				return;

			if (!TryFindSafePath(actor, destination, out var path))
			{
				Release(field, actor, "route-invalidated");
				return;
			}

			if (path.Count < 2)
			{
				if (routeProgress.TryGetValue(actor.ActorID, out var settled))
					settled.EnRoute = false;
				return;
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

		static bool CanOccupyCachedDestination(Actor actor, CPos destination)
		{
			if (actor.Location == destination)
				return true;

			var mobile = actor.TraitOrDefault<Mobile>();
			return mobile != null && mobile.CanEnterCell(destination, check: BlockedByActor.Immovable);
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
			unassignedCombatUnits?.RegisterReleasedActors(new[] { actor });
			RestoreStance(actor.ActorID);
			field.Tanks.Remove(actor.ActorID);
			field.Infantry.Remove(actor.ActorID);
			field.AntiAir.Remove(actor.ActorID);
			field.Destinations.Remove(actor.ActorID);
			reserved.Remove(actor.ActorID);
			lastOrderTicks.Remove(actor.ActorID);
			routeProgress.Remove(actor.ActorID);
			lastUrgentTargets.Remove(actor.ActorID);
			lastUrgentOrderTicks.Remove(actor.ActorID);
			if (reason == "no-safe-route" || reason == "route-invalidated")
				routeRejectedUntil[actor.ActorID] = world.WorldTick + Info.RouteRetryTicks;

			Debug("released defender={0} field={1} reason={2}", actor.ActorID, field.HarvesterId, reason);
		}

		void ClearState(string reason)
		{
			unassignedCombatUnits?.RegisterReleasedActors(reserved.Select(world.GetActorById).Where(a => a != null));
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
			dirtyAssignments.Clear();
			dirtyReasons.Clear();
			dirtyEnemyTargets.Clear();
			lastUrgentTargets.Clear();
			lastUrgentOrderTicks.Clear();
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
			var dirty = dirtyAssignments.Snapshot();
			var dirtyEnemies = dirtyEnemyTargets.OrderBy(pair => pair.Key).ToArray();
			var urgent = lastUrgentTargets.Where(pair => reserved.Contains(pair.Key))
				.OrderBy(pair => pair.Key).ToArray();

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("EconomyFieldDefenseScanTicks", FieldSaver.FormatValue(scanTicks)),
				new MiniYamlNode("EconomyFieldDefenseNextScanTick",
					FieldSaver.FormatValue(world.WorldTick + Math.Max(1, scanTicks))),
				new MiniYamlNode("EconomyFieldDefenseStanceActors", FieldSaver.FormatValue(stances.Select(p => p.Key).ToArray())),
				new MiniYamlNode("EconomyFieldDefenseStances", FieldSaver.FormatValue(stances.Select(p => (int)p.Value).ToArray())),
				new MiniYamlNode("EconomyFieldDefenseFields", "", fieldNodes),
				new MiniYamlNode("EconomyFieldDefenseRoutes", "", routes),
				new MiniYamlNode("EconomyFieldDefenseRejectedRoutes", "", rejectedRoutes),
				new MiniYamlNode("EconomyFieldDefenseDirtyFields",
					FieldSaver.FormatValue(dirty.Select(item => item.FieldId).ToArray())),
				new MiniYamlNode("EconomyFieldDefenseDirtyActors",
					FieldSaver.FormatValue(dirty.Select(item => item.ActorId).ToArray())),
				new MiniYamlNode("EconomyFieldDefenseDirtyEnemyActors",
					FieldSaver.FormatValue(dirtyEnemies.Select(pair => pair.Key).ToArray())),
				new MiniYamlNode("EconomyFieldDefenseDirtyEnemyCells",
					FieldSaver.FormatValue(dirtyEnemies.Select(pair => pair.Value.Bits).ToArray())),
				new MiniYamlNode("EconomyFieldDefenseUrgentActors",
					FieldSaver.FormatValue(urgent.Select(pair => pair.Key).ToArray())),
				new MiniYamlNode("EconomyFieldDefenseUrgentCells",
					FieldSaver.FormatValue(urgent.Select(pair => pair.Value.Bits).ToArray())),
				new MiniYamlNode("EconomyFieldDefenseUrgentTicks",
					FieldSaver.FormatValue(urgent.Select(pair => lastUrgentOrderTicks.TryGetValue(pair.Key,
						out var tick) ? tick : -1).ToArray()))
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
			dirtyAssignments.Clear();
			dirtyReasons.Clear();
			dirtyEnemyTargets.Clear();
			lastUrgentTargets.Clear();
			lastUrgentOrderTicks.Clear();
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

			unassignedCombatUnits?.ClaimActors(reserved.Select(world.GetActorById).Where(a => a != null));

			RestoreRouteState(data);
			RestoreDirtyAssignments(data);
			RestoreUrgentAssignments(data);
		}

		void RestoreDirtyAssignments(List<MiniYamlNode> data)
		{
			var fieldsNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseDirtyFields");
			var actorsNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseDirtyActors");
			if (fieldsNode == null || actorsNode == null)
				return;

			var fieldIds = FieldLoader.GetValue<uint[]>(fieldsNode.Key, fieldsNode.Value.Value);
			var actorIds = FieldLoader.GetValue<uint[]>(actorsNode.Key, actorsNode.Value.Value);
			for (var i = 0; i < Math.Min(fieldIds.Length, actorIds.Length); i++)
				if (fields.ContainsKey(fieldIds[i]))
					dirtyAssignments.Enqueue(fieldIds[i], actorIds[i]);

			var enemyActorsNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseDirtyEnemyActors");
			var enemyCellsNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseDirtyEnemyCells");
			if (enemyActorsNode == null || enemyCellsNode == null)
				return;

			var enemyActors = FieldLoader.GetValue<uint[]>(enemyActorsNode.Key, enemyActorsNode.Value.Value);
			var enemyCells = FieldLoader.GetValue<int[]>(enemyCellsNode.Key, enemyCellsNode.Value.Value);
			for (var i = 0; i < Math.Min(enemyActors.Length, enemyCells.Length); i++)
				if (reserved.Contains(enemyActors[i]))
					dirtyEnemyTargets[enemyActors[i]] = new CPos(enemyCells[i]);
		}

		void RestoreUrgentAssignments(List<MiniYamlNode> data)
		{
			var actorsNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseUrgentActors");
			var cellsNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseUrgentCells");
			var ticksNode = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseUrgentTicks");
			if (actorsNode == null || cellsNode == null || ticksNode == null)
				return;

			var actors = FieldLoader.GetValue<uint[]>(actorsNode.Key, actorsNode.Value.Value);
			var cells = FieldLoader.GetValue<int[]>(cellsNode.Key, cellsNode.Value.Value);
			var ticks = FieldLoader.GetValue<int[]>(ticksNode.Key, ticksNode.Value.Value);
			for (var i = 0; i < Math.Min(actors.Length, Math.Min(cells.Length, ticks.Length)); i++)
				if (reserved.Contains(actors[i]) && ticks[i] >= 0 && ticks[i] <= world.WorldTick)
				{
					lastUrgentTargets[actors[i]] = new CPos(cells[i]);
					lastUrgentOrderTicks[actors[i]] = ticks[i];
				}
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
