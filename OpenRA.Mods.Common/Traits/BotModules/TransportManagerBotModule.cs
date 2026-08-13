#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Coordinates route-failure rescue, infantry assaults, and heavy-unit air drops.")]
	public class TransportManagerBotModuleInfo : ConditionalTraitInfo
	{
		[ActorReference]
		public readonly HashSet<string> TransportHelicopterTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> RescuePassengerTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> GroundTransportTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> AssaultPassengerTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> AssaultEngineerTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> AssaultCommandoTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> AssaultTargetTypes = new HashSet<string>();

		[Desc("Bot types allowed to select the optional infantry transport strategy.")]
		public readonly HashSet<string> AssaultBotTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> HeavyDropPassengerTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> HeavyDropTargetTypes = new HashSet<string>();

		[Desc("Bot types allowed to use the mid/late-game heavy air-drop strategy.")]
		public readonly HashSet<string> HeavyDropBotTypes = new HashSet<string>();

		[ActorReference]
		public readonly string TransportHelicopterActor = null;

		[ActorReference]
		public readonly string GroundTransportActor = null;

		public readonly int ScanInterval = 75;
		public readonly int MaximumActiveMissions = 4;
		public readonly int TransportHelicopterLimit = 10;
		public readonly int PersistentBlockedScans = 3;
		public readonly int BlockedDiagnosticIntervalScans = 8;
		public readonly int MaximumCandidatesPerScan = 32;
		public readonly int MoveIntentMaximumAge = 1500;
		public readonly int PickupRangeCells = 3;
		public readonly int UnloadRangeCells = 4;
		public readonly int LandingSearchRadiusCells = 8;
		public readonly int LandingUsefulnessRadiusCells = 16;
		public readonly int LandingMaximumCandidates = 128;
		public readonly int LandingReplanInterval = 75;
		public readonly int LandingHoldTicks = 300;
		public readonly int SafeReturnLandingSearchRadiusCells = 8;
		public readonly int SafeReturnUsefulnessRadiusCells = 64;
		public readonly int LandingThreatRangeBufferCells = 1;
		public readonly int LandingCoarseCellSize = 4;
		public readonly int LandingRouteThreatPenalty = 100;
		public readonly int MissionTimeoutTicks = 3000;
		public readonly int IdleServiceInterval = 250;
		public readonly int IdleStagingRadius = 10;
		public readonly int RepairHealthPercent = 50;
		public readonly int AssaultSelectionPercent = 0;
		public readonly int MinimumAssaultPassengers = 3;
		public readonly int MaximumAssaultPassengers = 8;
		public readonly int AssaultGatherTimeoutTicks = 750;
		public readonly int AssaultCooldownTicks = 7500;
		public readonly int AssaultOrderRetryTicks = 75;
		public readonly int AssaultLandingSearchRadiusCells = 8;
		public readonly int AssaultLandingUsefulnessRadiusCells = 16;
		public readonly int HeavyDropMinimumGameTicks = 7500;
		public readonly int HeavyDropMinimumPassengers = 8;
		public readonly int HeavyDropMaximumPassengers = 10;
		public readonly int HeavyDropConcurrentBoarding = 10;
		public readonly int HeavyDropBoardingRetryTicks = 750;
		public readonly int HeavyDropGatherTimeoutTicks = 3000;
		public readonly int HeavyDropMissionTimeoutTicks = 9000;
		public readonly int HeavyDropCooldownTicks = 7500;
		public readonly int HeavyDropReplanInterval = 150;
		public readonly int HeavyDropTargetCandidateLimit = 12;
		public readonly int HeavyDropLandingRadius = 8;
		public readonly int HeavyDropFormationRadius = 8;
		public readonly int HeavyDropFormationSpacing = 3;
		public readonly int HeavyDropLandingSearchRadiusCells = 3;
		public readonly int HeavyDropLandingUsefulnessRadiusCells = 16;
		public readonly int HeavyDropUnloadRangeCells = 1;
		public readonly int HeavyDropDefenseRadius = 7;
		public readonly int HeavyDropMaximumDefenderValue = 3400;
		public readonly float HeavyDropMaximumAaDanger = 0f;
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (ScanInterval <= 0 || MaximumActiveMissions <= 0 || TransportHelicopterLimit <= 0 ||
				PersistentBlockedScans <= 0 || BlockedDiagnosticIntervalScans <= 0 ||
				MaximumCandidatesPerScan <= 0 || MoveIntentMaximumAge <= 0 ||
				PickupRangeCells <= 0 || UnloadRangeCells <= 0 || LandingSearchRadiusCells <= 0 ||
				LandingUsefulnessRadiusCells <= 0 || LandingMaximumCandidates <= 0 || LandingReplanInterval <= 0 ||
				LandingHoldTicks <= 0 || LandingThreatRangeBufferCells < 0 || LandingCoarseCellSize <= 0 ||
				SafeReturnLandingSearchRadiusCells <= 0 || SafeReturnUsefulnessRadiusCells <= 0 ||
				LandingRouteThreatPenalty < 0 || MissionTimeoutTicks <= 0 ||
				IdleServiceInterval <= 0 || IdleStagingRadius <= 0 || RepairHealthPercent <= 0 || RepairHealthPercent > 100 ||
				AssaultSelectionPercent < 0 || AssaultSelectionPercent > 100 || MinimumAssaultPassengers <= 0 ||
				MaximumAssaultPassengers < MinimumAssaultPassengers || AssaultGatherTimeoutTicks <= 0 || AssaultCooldownTicks <= 0 ||
				AssaultOrderRetryTicks <= 0 || AssaultLandingSearchRadiusCells <= 0 ||
				AssaultLandingUsefulnessRadiusCells <= 0 || HeavyDropMinimumGameTicks < 0 || HeavyDropMinimumPassengers <= 0 ||
				HeavyDropMaximumPassengers < HeavyDropMinimumPassengers || HeavyDropConcurrentBoarding <= 0 ||
				HeavyDropConcurrentBoarding > HeavyDropMaximumPassengers || HeavyDropBoardingRetryTicks <= 0 ||
				HeavyDropGatherTimeoutTicks <= 0 ||
				HeavyDropMissionTimeoutTicks <= HeavyDropGatherTimeoutTicks ||
				HeavyDropCooldownTicks <= 0 || HeavyDropReplanInterval <= 0 || HeavyDropTargetCandidateLimit <= 0 ||
				HeavyDropLandingRadius <= 0 || HeavyDropFormationRadius <= 0 || HeavyDropFormationSpacing <= 0 ||
				HeavyDropLandingSearchRadiusCells <= 0 || HeavyDropLandingUsefulnessRadiusCells <= 0 ||
				HeavyDropUnloadRangeCells < 0 || HeavyDropDefenseRadius <= 0 || HeavyDropMaximumDefenderValue < 0 ||
				HeavyDropMaximumAaDanger < 0)
				throw new YamlException("AI transport counts, ranges, intervals, timeouts, and repair threshold must be positive and valid.");
		}

		public override object Create(ActorInitializer init) { return new TransportManagerBotModule(init.Self, this); }
	}

	public class TransportManagerBotModule : ConditionalTrait<TransportManagerBotModuleInfo>,
		IBotEnabled, IBotTick, IBotTransportReservations, IBotUnitReservations,
		IBotTransportObjectiveService, IBotRespondToAttack
	{
		enum MissionStage { Gathering, Travelling, Unloading, Handoff }

		sealed class Mission
		{
			public readonly int Id;
			public readonly Actor Transport;
			public readonly Actor Passenger;
			public readonly CPos Destination;
			public readonly Actor Objective;
			public readonly int CreatedTick;
			public int DeadlineTick;
			public MissionStage Stage;
			public int LastOrderTick;
			public bool CaptureHandoffQueued;
			public int PlanFailureTick;
			public int LastRoutedPlanRevision;
			public TransportUnloadPlan UnloadPlan;
			public readonly TransportRescueRecoveryLifecycle Recovery = new TransportRescueRecoveryLifecycle();
			public CPos? RecoveryObjective;
			public bool Returning => Recovery.Phase != TransportRescueRecoveryPhase.Active;
			public bool Terminal => Recovery.Phase == TransportRescueRecoveryPhase.Terminal;

			public Mission(int id, Actor transport, Actor passenger, CPos destination, int tick, int deadlineTick)
			{
				Id = id;
				Transport = transport;
				Passenger = passenger;
				Destination = destination;
				CreatedTick = LastOrderTick = tick;
				DeadlineTick = deadlineTick;
			}

			public Mission(int id, Actor transport, Actor passenger, Actor objective, int tick, int deadlineTick)
				: this(id, transport, passenger, objective.Location, tick, deadlineTick)
			{
				Objective = objective;
			}
		}

		sealed class BlockedObservation
		{
			public CPos Destination;
			public int Count;
		}

		readonly World world;
		readonly Player player;
		readonly TransportMissionCoordinator coordinator;
		readonly TransportObjectiveTimeoutLedger timedOutObjectives = new TransportObjectiveTimeoutLedger();
		readonly TransportUnloadPlanner unloadPlanner;
		readonly List<Mission> missions = new List<Mission>();
		readonly Dictionary<uint, BlockedObservation> blocked = new Dictionary<uint, BlockedObservation>();
		readonly Dictionary<uint, CPos> safeIdleStagingCells = new Dictionary<uint, CPos>();
		readonly InfantryAssaultTransportManager infantryAssault;
		readonly HeavyDropTransportManager heavyDrop;
		IBot bot;
		UnitBuilderBotModule[] production;
		IBotUnitReservations[] otherUnitReservations;
		SquadManagerBotModule squadManager;
		int scanTicks;
		int serviceTicks;

		public TransportManagerBotModule(Actor self, TransportManagerBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
			coordinator = new TransportMissionCoordinator(info.MaximumActiveMissions);
			unloadPlanner = new TransportUnloadPlanner(world, player, info, coordinator);
			infantryAssault = new InfantryAssaultTransportManager(world, player, info, coordinator, unloadPlanner,
				IssueRoutedMove, RequestTransportHelicopter, actor => IsReservedForOtherBehavior(actor), RememberSafeIdleStaging);
			heavyDrop = new HeavyDropTransportManager(world, player, info, coordinator, unloadPlanner,
				IssueRoutedMove, RequestTransportHelicopter, () => squadManager,
				actor => IsReservedForOtherBehavior(actor),
				RememberSafeIdleStaging);
		}

		protected override void Created(Actor self)
		{
			production = self.Owner.PlayerActor.TraitsImplementing<UnitBuilderBotModule>().ToArray();
			otherUnitReservations = self.Owner.PlayerActor.TraitsImplementing<IBotUnitReservations>()
				.Where(r => !ReferenceEquals(r, this)).ToArray();
			infantryAssault.Initialize(production);
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self)
		{
			scanTicks = Info.ScanInterval;
			serviceTicks = Info.IdleServiceInterval;
			infantryAssault.Enable();
			heavyDrop.Enable();
		}

		void IBotEnabled.BotEnabled(IBot enabledBot) { bot = enabledBot; }

		bool IBotTransportReservations.IsTransportReserved(Actor actor)
		{
			return actor != null && coordinator.IsReserved(actor.ActorID);
		}

		bool IBotUnitReservations.IsUnitReserved(Actor actor)
		{
			// The coordinator reserves both carriers and pending passengers. Exposing both through the
			// generic seam prevents unrelated specialist managers from replacing their mission orders.
			return actor != null && coordinator.IsReserved(actor.ActorID);
		}

		bool IBotTransportObjectiveService.CanTransportTo(
			Actor passenger, Actor objective, IBotUnitReservations reservationOwner)
		{
			var available = TryPrepareObjectiveTransport(passenger, objective, reservationOwner,
				out _, out _, out _, out var rejection);
			if (!available)
				Debug("objective transport unavailable passenger={0} objective={1}: {2}",
					passenger, objective, rejection);

			return available;
		}

		bool IBotTransportObjectiveService.TryRequestTransport(
			Actor passenger, Actor objective, IBotUnitReservations reservationOwner)
		{
			if (passenger == null || reservationOwner == null || !reservationOwner.IsUnitReserved(passenger) ||
				objective == null || objective.IsDead || !objective.IsInWorld ||
				missions.Any(existing => existing.Passenger == passenger))
				return false;

			timedOutObjectives.Clear(passenger.ActorID);

			if (!TryPrepareObjectiveTransport(passenger, objective, reservationOwner,
				out var transport, out var pickupRoute, out _, out var rejection))
			{
				Debug("objective transport rejected passenger={0} objective={1}: {2}",
					passenger, objective, rejection);
				return false;
			}

			var missionId = coordinator.TryReserve(new[] { transport.ActorID, passenger.ActorID });
			if (missionId == 0 || !unloadPlanner.TryPlan(missionId, transport, new[] { passenger }, objective.Location,
				Info.LandingSearchRadiusCells, Info.LandingUsefulnessRadiusCells, 1,
				out var unloadPlan, out rejection))
			{
				if (missionId != 0)
					coordinator.Release(missionId);

				Debug("objective transport reservation rejected passenger={0} objective={1}: {2}",
					passenger, objective, rejection);
				return false;
			}

			var distanceCells = Math.Abs(transport.Location.X - passenger.Location.X) +
				Math.Abs(transport.Location.Y - passenger.Location.Y) +
				Math.Abs(passenger.Location.X - objective.Location.X) + Math.Abs(passenger.Location.Y - objective.Location.Y);
			var speed = transport.Info.TraitInfoOrDefault<AircraftInfo>()?.Speed ?? 1;
			var travelAllowance = (int)Math.Min(int.MaxValue,
				distanceCells * 1024L * 3 / Math.Max(1, speed) + 1000);
			var mission = new Mission(missionId, transport, passenger, objective, world.WorldTick,
				world.WorldTick + Math.Max(Info.MissionTimeoutTicks, travelAllowance))
			{
				UnloadPlan = unloadPlan
			};
			missions.Add(mission);
			IssueRoutedMove(transport, passenger.Location, pickupRoute);
			Debug("created objective mission {0}: transport={1} passenger={2} objective={3} " +
				"carrier={4} exit={5}", missionId, transport, passenger, objective,
				unloadPlan.CarrierCell, unloadPlan.ExitCells[0]);
			return true;
		}

		bool IBotTransportObjectiveService.IsTransporting(Actor passenger)
		{
			return passenger != null && missions.Any(mission => mission.Passenger == passenger);
		}

		bool IBotTransportObjectiveService.TryConsumeTimedOutObjective(Actor passenger, Actor objective)
		{
			return passenger != null && objective != null &&
				timedOutObjectives.TryConsume(passenger.ActorID, objective.ActorID);
		}

		void IBotTransportObjectiveService.CancelTransport(Actor passenger)
		{
			for (var i = missions.Count - 1; i >= 0; i--)
			{
				var mission = missions[i];
				if (mission.Passenger != passenger)
					continue;

				var cargo = mission.Transport.TraitOrDefault<Cargo>();
				if (cargo != null && cargo.Passengers.Any(actor => actor == passenger))
					RecoverTimedOutCargo(mission, "objective canceled");
				else
				{
					if (bot != null && IsUsable(mission.Transport))
						bot.QueueOrder(new Order("Stop", mission.Transport, false));

					FinishMission(i, "objective canceled before pickup");
				}
			}
		}

		bool IsReservedForOtherBehavior(Actor actor, IBotUnitReservations ignoredReservation = null)
		{
			return actor != null && otherUnitReservations != null &&
				otherUnitReservations.Any(r => !ReferenceEquals(r, ignoredReservation) && r.IsUnitReserved(actor));
		}

		void IBotRespondToAttack.RespondToAttack(IBot enabledBot, Actor self, AttackInfo e)
		{
			if (!IsTraitDisabled)
			{
				infantryAssault.RespondToAttack(enabledBot, self, e);
				heavyDrop.RespondToAttack(enabledBot, self, e);
			}
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (IsTraitDisabled)
				return;

			if (--scanTicks <= 0)
			{
				scanTicks = Info.ScanInterval;
				RefreshSquadManager();
				unloadPlanner.RefreshSnapshot();
				AdvanceMissions();

				// Reserve a complete strategic wave before opportunistic one-unit transport missions
				// can consume its carriers or passengers.
				heavyDrop.Tick(enabledBot);
				var createdRescue = coordinator.MissionCount < Info.MaximumActiveMissions && TryCreateRescueMission();
				if (!createdRescue)
					infantryAssault.Tick(enabledBot);
				else
					infantryAssault.Advance(enabledBot);
			}

			if (--serviceTicks <= 0)
			{
				serviceTicks = Info.IdleServiceInterval;
				RefreshSquadManager();
				ServiceIdleTransports();
			}
		}

		void RefreshSquadManager()
		{
			if (squadManager == null || squadManager.IsTraitDisabled)
				squadManager = player.PlayerActor.TraitsImplementing<SquadManagerBotModule>()
					.FirstOrDefault(m => !m.IsTraitDisabled);
		}

		void AdvanceMissions()
		{
			for (var i = missions.Count - 1; i >= 0; i--)
			{
				var mission = missions[i];
				if (!IsUsable(mission.Transport))
				{
					FinishMission(i, "transport unavailable");
					continue;
				}

				if (!IsMissionPassengerUsable(mission))
				{
					FinishMission(i, "passenger unavailable");
					continue;
				}

				var cargo = mission.Transport.TraitOrDefault<Cargo>();
				if (cargo == null)
				{
					FinishMission(i, "transport lost cargo trait");
					continue;
				}

				if (mission.Recovery.TryEnterTerminal(world.WorldTick))
				{
					EnterTerminalRecovery(mission);
					continue;
				}

				if (!mission.Returning && world.WorldTick > mission.DeadlineTick)
				{
					if (mission.Stage == MissionStage.Handoff)
						FinishMission(i, "capture handoff timed out");
					else if (cargo.IsEmpty())
						FinishMission(i, "timed out before pickup");
					else
						RecoverTimedOutCargo(mission);

					continue;
				}

				if (mission.Terminal)
				{
					AdvanceTerminalRecovery(mission, cargo, i);
					continue;
				}

				if (mission.Stage == MissionStage.Gathering)
					AdvanceGathering(mission, cargo, i);
				else if (mission.Stage == MissionStage.Travelling)
					AdvanceTravel(mission);
				else if (cargo.IsEmpty() && mission.Stage != MissionStage.Handoff)
				{
					Debug("mission {0} physical handoff tick={1}: passenger={2} cell={3} cargo=0 objective={4} outcome={5}",
						mission.Id, world.WorldTick, mission.Passenger, mission.Passenger.Location, mission.Destination,
						mission.Returning ? "safe-recovery" : "useful-rescue");
					if (mission.Objective == null)
					{
						QueueObjectiveHandoff(mission);
						FinishMission(i, mission.Returning ? "safe recovery unload complete" : "rescue complete");
					}
					else
						BeginObjectiveHandoff(mission);
				}
				else if (mission.Stage == MissionStage.Handoff)
					AdvanceObjectiveHandoff(mission, i);
				else if (!EnsureUnloadPlan(mission, mission.UnloadPlan?.Objective ??
					mission.RecoveryObjective ?? mission.Destination, out var reason))
					HandlePlanFailure(mission, reason);
				else if ((mission.Transport.Location - mission.UnloadPlan.CarrierCell).LengthSquared >
					Info.UnloadRangeCells * Info.UnloadRangeCells)
				{
					mission.Stage = MissionStage.Travelling;
					if (IssueUnloadPlanRoute(mission))
						mission.LastOrderTick = world.WorldTick;
					else
						HandlePlanFailure(mission, "replanned unload route failed");
				}
				else if (ReadyToRetry(mission))
				{
					QueuePlannedUnload(mission);
					mission.LastOrderTick = world.WorldTick;
				}
			}
		}

		void AdvanceGathering(Mission mission, Cargo cargo, int index)
		{
			if (cargo.Passengers.Any(a => a == mission.Passenger))
			{
				mission.Stage = MissionStage.Travelling;
				mission.LastOrderTick = world.WorldTick;
				if (EnsureUnloadPlan(mission, mission.Destination, out var reason) && IssueUnloadPlanRoute(mission))
					Debug("mission {0} travelling to planned carrier={1} exit={2} objective={3} revision={4} snapshot={5}",
						mission.Id, mission.UnloadPlan.CarrierCell, mission.UnloadPlan.ExitCells[0],
						mission.Destination, mission.UnloadPlan.Revision, mission.UnloadPlan.SnapshotTick);
				else
					HandlePlanFailure(mission, reason ?? "threat route failed");
				return;
			}

			if (NeedsRepair(mission.Transport))
			{
				FinishMission(index, "damaged before pickup");
				return;
			}

			if (!ReadyToRetry(mission))
				return;

			var passenger = mission.Passenger.Trait<Passenger>();
			if (passenger.ReservedCargo == cargo)
				return;

			if ((mission.Transport.Location - mission.Passenger.Location).LengthSquared <=
				Info.PickupRangeCells * Info.PickupRangeCells)
				bot.QueueOrder(new Order("EnterTransport", mission.Passenger,
					Target.FromActor(mission.Transport), false));
			else if (IsCarrierIdle(mission.Transport))
				IssueRoutedMove(mission.Transport, mission.Passenger.Location);
			else
				return;

			mission.LastOrderTick = world.WorldTick;
		}

		void AdvanceTravel(Mission mission)
		{
			var objective = mission.UnloadPlan?.Objective ?? mission.RecoveryObjective ?? mission.Destination;
			if (!EnsureUnloadPlan(mission, objective, out var reason))
			{
				HandlePlanFailure(mission, reason);
				return;
			}

			if (mission.LastRoutedPlanRevision != mission.UnloadPlan.Revision)
			{
				if (IssueUnloadPlanRoute(mission))
					mission.LastOrderTick = world.WorldTick;
				else
					HandlePlanFailure(mission, "replacement threat route failed");

				return;
			}

			if ((mission.Transport.Location - mission.UnloadPlan.CarrierCell).LengthSquared <=
				Info.UnloadRangeCells * Info.UnloadRangeCells)
			{
				mission.Stage = MissionStage.Unloading;
				mission.LastOrderTick = world.WorldTick;
				QueuePlannedUnload(mission);
				Debug("mission {0} committing exact unload: carrier={1} exit={2} objective={3} revision={4} snapshot={5}",
					mission.Id, mission.UnloadPlan.CarrierCell, mission.UnloadPlan.ExitCells[0],
					mission.UnloadPlan.Objective, mission.UnloadPlan.Revision, mission.UnloadPlan.SnapshotTick);
				return;
			}

			if (IsCarrierIdle(mission.Transport) && ReadyToRetry(mission))
			{
				if (IssueUnloadPlanRoute(mission))
					mission.LastOrderTick = world.WorldTick;
				else
					HandlePlanFailure(mission, "threat route failed");
			}
		}

		bool EnsureUnloadPlan(Mission mission, CPos objective, out string rejection)
		{
			var previous = mission.UnloadPlan;
			var invalidation = "none";
			if (previous != null && previous.Objective == objective)
			{
				if (unloadPlanner.Revalidate(mission.Id, mission.Transport, new[] { mission.Passenger },
					previous, Info.LandingUsefulnessRadiusCells, out rejection))
				{
					mission.PlanFailureTick = 0;
					return true;
				}

				invalidation = rejection;
			}
			else if (previous != null)
			{
				invalidation = "objective changed";
			}

			var revision = (previous?.Revision ?? 0) + 1;
			if (!unloadPlanner.TryPlan(mission.Id, mission.Transport, new[] { mission.Passenger }, objective,
				Info.LandingSearchRadiusCells, Info.LandingUsefulnessRadiusCells, revision,
				out var replacement, out rejection))
			{
				mission.UnloadPlan = null;
				return false;
			}

			mission.UnloadPlan = replacement;
			mission.PlanFailureTick = 0;
			if (previous == null || previous.CarrierCell != replacement.CarrierCell ||
				!previous.ExitCells.SequenceEqual(replacement.ExitCells))
				Debug("mission {0} selected unload plan: objective={1} carrier={2} exits={3} revision={4} " +
					"snapshot={5} threats={6} candidates={7} firstRejection={8} firstThreatRejection={9} " +
					"replannedBecause={10}",
					mission.Id, objective, replacement.CarrierCell,
					string.Join(",", replacement.ExitCells), replacement.Revision, replacement.SnapshotTick,
					unloadPlanner.SnapshotThreatCount, replacement.CandidatesEvaluated,
					replacement.FirstRejection ?? "none", replacement.FirstThreatRejection ?? "none", invalidation);

			return true;
		}

		bool IssueUnloadPlanRoute(Mission mission)
		{
			var route = unloadPlanner.Route(mission.Transport, mission.UnloadPlan);
			if (route == null || (route.Count == 0 && mission.Transport.Location != mission.UnloadPlan.CarrierCell))
			{
				Debug("mission {0} rejected unload route: planner returned failed route for carrier={1}",
					mission.Id, mission.UnloadPlan?.CarrierCell);
				return false;
			}

			var queued = false;
			foreach (var waypoint in route)
			{
				bot.QueueOrder(new Order("Move", mission.Transport, Target.FromCell(world, waypoint), queued));
				queued = true;
			}

			mission.LastRoutedPlanRevision = mission.UnloadPlan.Revision;
			Debug("mission {0} routed to safe unload carrier={1}: result={2} waypoints={3} snapshot={4} revision={5}",
				mission.Id, mission.UnloadPlan.CarrierCell, route.Count == 1 ? "same-sector/direct-safe" : "threat-aware",
				route.Count, mission.UnloadPlan.SnapshotTick, mission.UnloadPlan.Revision);
			return true;
		}

		void QueuePlannedUnload(Mission mission)
		{
			bot.QueueOrder(TransportUnloadOrder.Create(world, mission.Transport, mission.UnloadPlan));
		}

		void HandlePlanFailure(Mission mission, string reason)
		{
			if (mission.PlanFailureTick == 0)
			{
				mission.PlanFailureTick = world.WorldTick;
				bot.QueueOrder(new Order("Stop", mission.Transport, false));
				Debug("mission {0} holding without unload plan: {1}", mission.Id, reason);
			}

			if (world.WorldTick - mission.PlanFailureTick < Info.LandingHoldTicks)
				return;

			if (!mission.Returning)
			{
				RecoverTimedOutCargo(mission, "bounded unload-plan hold expired");
				return;
			}

			if (TryPlanRecoveryFallback(mission, out var fallbackRejection) && IssueUnloadPlanRoute(mission))
			{
				mission.Stage = MissionStage.Travelling;
				mission.LastOrderTick = world.WorldTick;
				Debug("mission {0} terminal safe fallback: searchCenter={1}, recoveryObjective={2}, " +
					"carrier={3}, exit={4}", mission.Id, mission.Transport.Location,
					mission.RecoveryObjective, mission.UnloadPlan.CarrierCell, mission.UnloadPlan.ExitCells[0]);
				return;
			}

			mission.PlanFailureTick = world.WorldTick;
			Debug("mission {0} bounded safe hold renewed: recoveryObjective={1}, reason={2}",
				mission.Id, mission.RecoveryObjective, fallbackRejection);
		}

		void EnterTerminalRecovery(Mission mission)
		{
			bot.QueueOrder(new Order("Stop", mission.Transport, false));
			mission.UnloadPlan = null;
			mission.Stage = MissionStage.Travelling;
			mission.PlanFailureTick = world.WorldTick;
			mission.LastRoutedPlanRevision = 0;
			coordinator.ParkLoadedMission(mission.Id);
			Debug("mission {0} terminal loaded recovery: carrier={1} passenger={2} recoveryObjective={3} " +
				"deadline={4} outcome=parked-reserved", mission.Id, mission.Transport, mission.Passenger,
				mission.RecoveryObjective, mission.Recovery.DeadlineTick);
		}

		void AdvanceTerminalRecovery(Mission mission, Cargo cargo, int index)
		{
			if (cargo.IsEmpty())
			{
				Debug("mission {0} physical handoff after terminal recovery: passenger={1} cell={2} cargo=0 objective={3}",
					mission.Id, mission.Passenger, mission.Passenger.Location, mission.Destination);
				QueueObjectiveHandoff(mission);
				FinishMission(index, "terminal safe recovery unload complete");
				return;
			}

			if (mission.UnloadPlan == null)
			{
				if (world.WorldTick - mission.PlanFailureTick < Info.LandingHoldTicks)
					return;

				if (TryPlanRecoveryFallback(mission, out var rejection) && IssueUnloadPlanRoute(mission))
				{
					mission.Stage = MissionStage.Travelling;
					mission.LastOrderTick = world.WorldTick;
					Debug("mission {0} resumed terminal safe recovery: recoveryObjective={1} carrier={2} exit={3}",
						mission.Id, mission.RecoveryObjective, mission.UnloadPlan.CarrierCell,
						mission.UnloadPlan.ExitCells[0]);
					return;
				}

				coordinator.ReleaseCells(mission.Id);
				mission.UnloadPlan = null;
				mission.PlanFailureTick = world.WorldTick;
				return;
			}

			var objective = mission.RecoveryObjective ?? mission.Destination;
			if (!EnsureUnloadPlan(mission, objective, out var reason))
			{
				ParkTerminalPlan(mission, reason);
				return;
			}

			if (mission.LastRoutedPlanRevision != mission.UnloadPlan.Revision)
			{
				if (IssueUnloadPlanRoute(mission))
					mission.LastOrderTick = world.WorldTick;
				else
					ParkTerminalPlan(mission, "replacement threat route failed");

				return;
			}

			if ((mission.Transport.Location - mission.UnloadPlan.CarrierCell).LengthSquared <=
				Info.UnloadRangeCells * Info.UnloadRangeCells)
			{
				mission.Stage = MissionStage.Unloading;
				mission.LastOrderTick = world.WorldTick;
				QueuePlannedUnload(mission);
				Debug("mission {0} committing terminal exact unload: carrier={1} exit={2} objective={3} " +
					"revision={4} snapshot={5}", mission.Id, mission.UnloadPlan.CarrierCell,
					mission.UnloadPlan.ExitCells[0], mission.UnloadPlan.Objective,
					mission.UnloadPlan.Revision, mission.UnloadPlan.SnapshotTick);
				return;
			}

			if (IsCarrierIdle(mission.Transport) && ReadyToRetry(mission))
			{
				if (IssueUnloadPlanRoute(mission))
					mission.LastOrderTick = world.WorldTick;
				else
					ParkTerminalPlan(mission, "threat route failed");
			}
		}

		void QueueObjectiveHandoff(Mission mission)
		{
			if (mission.Objective != null && !mission.Objective.IsDead && mission.Objective.IsInWorld)
			{
				bot.QueueOrder(new Order("CaptureActor", mission.Passenger, Target.FromActor(mission.Objective), false));
				Debug("mission {0} handed off capture tick={1} passenger={2} target={3}",
					mission.Id, world.WorldTick, mission.Passenger, mission.Objective);
			}
			else
				bot.QueueOrder(new Order("Move", mission.Passenger, Target.FromCell(world, mission.Destination), false));
		}

		void BeginObjectiveHandoff(Mission mission)
		{
			mission.Stage = MissionStage.Handoff;

			// The unload/cargo transition can defer order delivery by a scan. Keep the strategic
			// claim through that narrow gap, but never let a rejected CaptureActor strand it.
			mission.DeadlineTick = world.WorldTick + Math.Max(Info.ScanInterval, Info.LandingReplanInterval) * 3;
			Debug("mission {0} capture handoff began at tick {1}: deadline={2} passenger={3} target={4}",
				mission.Id, world.WorldTick, mission.DeadlineTick, mission.Passenger, mission.Objective);
		}

		void AdvanceObjectiveHandoff(Mission mission, int index)
		{
			if (mission.Objective == null || mission.Objective.IsDead || !mission.Objective.IsInWorld)
			{
				FinishMission(index, "objective removed after unload");
				return;
			}

			var passenger = mission.Passenger.TraitOrDefault<Passenger>();
			if (!mission.Passenger.IsInWorld || passenger?.Transport != null || HasRideTransportActivity(mission.Passenger))
				return;

			if (!mission.CaptureHandoffQueued)
			{
				QueueObjectiveHandoff(mission);
				mission.CaptureHandoffQueued = true;
				Debug("mission {0} capture handoff queued at tick {1}: passenger={2} target={3}",
					mission.Id, world.WorldTick, mission.Passenger, mission.Objective);
				return;
			}

			if (!HasCaptureActivity(mission.Passenger))
				return;

			Debug("mission {0} capture handoff active at tick {1}: passenger={2} target={3}",
				mission.Id, world.WorldTick, mission.Passenger, mission.Objective);
			FinishMission(index, "capture handoff active");
		}

		static bool HasRideTransportActivity(Actor actor)
		{
			return actor.CurrentActivity != null && actor.CurrentActivity
				.ActivitiesImplementing<Activities.RideTransport>().Any();
		}

		static bool HasCaptureActivity(Actor actor)
		{
			return actor.CurrentActivity != null && actor.CurrentActivity
				.ActivitiesImplementing<Activities.CaptureActor>().Any();
		}

		void ParkTerminalPlan(Mission mission, string reason)
		{
			bot.QueueOrder(new Order("Stop", mission.Transport, false));
			coordinator.ReleaseCells(mission.Id);
			mission.UnloadPlan = null;
			mission.PlanFailureTick = world.WorldTick;
			mission.LastRoutedPlanRevision = 0;
			Debug("mission {0} terminal recovery plan invalidated: recoveryObjective={1} reason={2}",
				mission.Id, mission.RecoveryObjective, reason);
		}

		bool TryPlanRecoveryFallback(Mission mission, out string rejection)
		{
			var objective = mission.RecoveryObjective ?? mission.Destination;
			var revision = (mission.UnloadPlan?.Revision ?? 0) + 1;
			if (!unloadPlanner.TryPlanWithoutClaim(mission.Id, mission.Transport, new[] { mission.Passenger },
				mission.Transport.Location, objective, Info.SafeReturnLandingSearchRadiusCells,
				Info.SafeReturnUsefulnessRadiusCells, revision, Array.Empty<CPos>(),
				out var replacement, out rejection) ||
				!unloadPlanner.TryClaimPlans(mission.Id, new[] { replacement }, out rejection))
				return false;

			mission.UnloadPlan = replacement;
			mission.PlanFailureTick = 0;
			return true;
		}

		bool TryPrepareObjectiveTransport(Actor passenger, Actor objective,
			IBotUnitReservations reservationOwner, out Actor transport, out List<CPos> pickupRoute,
			out TransportUnloadPlan unloadPlan, out string rejection)
		{
			transport = null;
			pickupRoute = null;
			unloadPlan = null;
			rejection = "transport service is unavailable";
			if (bot == null || !IsUsable(passenger) || objective == null || objective.IsDead || !objective.IsInWorld ||
				passenger.TraitOrDefault<Passenger>()?.Transport != null ||
				coordinator.MissionCount >= Info.MaximumActiveMissions)
				return false;

			RefreshSquadManager();
			transport = FindAvailableTransport(passenger, reservationOwner);
			if (transport == null)
			{
				rejection = "no existing compatible healthy empty transport";
				return false;
			}

			if (AirStateBase.SafeIndependentAirThreatAt(squadManager, passenger.Location) > 0)
			{
				rejection = "pickup cell is covered by anti-air threat";
				return false;
			}

			pickupRoute = AirStateBase.SafeIndependentAirRoute(squadManager, transport, passenger.Location);
			if (pickupRoute == null)
			{
				rejection = "no bounded pickup route";
				return false;
			}

			return unloadPlanner.TryPlanWithoutClaim(0, transport, new[] { passenger }, objective.Location,
				Info.LandingSearchRadiusCells, Info.LandingUsefulnessRadiusCells, 1, Array.Empty<CPos>(),
				out unloadPlan, out rejection);
		}

		bool TryCreateRescueMission()
		{
			foreach (var id in blocked.Keys.Where(id => !IsUsable(world.GetActorById(id))).ToList())
				blocked.Remove(id);

			var candidates = world.Actors
				.Where(a => IsUsable(a) && a.Owner == player && Info.RescuePassengerTypes.Contains(a.Info.Name) &&
					!coordinator.IsReserved(a.ActorID) && !IsReservedForOtherBehavior(a) &&
					a.TraitOrDefault<Passenger>()?.Transport == null)
				.OrderBy(a => a.ActorID).Take(Info.MaximumCandidatesPerScan);
			foreach (var actor in candidates)
			{
				var mobile = actor.TraitOrDefault<Mobile>();
				if (mobile?.LastMoveOrderDestination == null ||
					world.WorldTick - mobile.LastMoveOrderTick > Info.MoveIntentMaximumAge ||
					mobile.MoveResult != MoveResult.CompleteDestinationBlocked ||
					(actor.Location - mobile.LastMoveOrderDestination.Value).LengthSquared <=
					Info.UnloadRangeCells * Info.UnloadRangeCells)
				{
					blocked.Remove(actor.ActorID);
					continue;
				}

				var destination = mobile.LastMoveOrderDestination.Value;
				if (!blocked.TryGetValue(actor.ActorID, out var observation) || observation.Destination != destination)
					blocked[actor.ActorID] = observation = new BlockedObservation { Destination = destination };

				if (++observation.Count < Info.PersistentBlockedScans)
					continue;

				var logObservation = observation.Count == Info.PersistentBlockedScans ||
					(observation.Count - Info.PersistentBlockedScans) % Info.BlockedDiagnosticIntervalScans == 0;
				if (logObservation)
					Debug("confirmed persistent route failure for {0} to {1} after {2} scans",
						actor, destination, observation.Count);

				var transport = FindAvailableTransport(actor);
				if (transport == null)
				{
					if (logObservation)
						Debug("blocked {0} to {1} is eligible but no healthy empty transport is available",
							actor, destination);
					RequestTransportHelicopter();
					return false;
				}

				var missionId = coordinator.TryReserve(new[] { transport.ActorID, actor.ActorID });
				if (missionId == 0)
					continue;

				var distanceCells = Math.Abs(transport.Location.X - actor.Location.X) +
					Math.Abs(transport.Location.Y - actor.Location.Y) +
					Math.Abs(actor.Location.X - destination.X) + Math.Abs(actor.Location.Y - destination.Y);
				var speed = transport.Info.TraitInfoOrDefault<AircraftInfo>()?.Speed ?? 1;
				var travelAllowance = (int)Math.Min(int.MaxValue,
					distanceCells * 1024L * 3 / Math.Max(1, speed) + 1000);
				var deadline = world.WorldTick + Math.Max(Info.MissionTimeoutTicks, travelAllowance);
				var mission = new Mission(missionId, transport, actor, destination, world.WorldTick, deadline);
				missions.Add(mission);
				blocked.Remove(actor.ActorID);
				IssueRoutedMove(transport, actor.Location);
				Debug("created rescue mission {0}: transport={1} passenger={2} destination={3}",
					missionId, transport, actor, destination);
				return true;
			}

			return false;
		}

		Actor FindAvailableTransport(Actor passenger, IBotUnitReservations ignoredReservation = null)
		{
			return world.Actors.Where(a => IsUsable(a) && a.Owner == player &&
				Info.TransportHelicopterTypes.Contains(a.Info.Name) && !coordinator.IsReserved(a.ActorID) &&
				!IsReservedForOtherBehavior(a) &&
				!IsReservedForOtherBehavior(passenger, ignoredReservation) &&
				a.TraitOrDefault<Cargo>()?.IsEmpty() == true && !NeedsRepair(a) &&
				a.Trait<Cargo>().HasSpace(passenger.Trait<Passenger>().Info.Weight))
				.OrderBy(a => (a.Location - passenger.Location).LengthSquared).ThenBy(a => a.ActorID).FirstOrDefault();
		}

		void RequestTransportHelicopter()
		{
			if (string.IsNullOrEmpty(Info.TransportHelicopterActor) || bot == null)
				return;

			if (!world.Map.Rules.Actors.TryGetValue(Info.TransportHelicopterActor, out var transportInfo))
				return;

			var buildable = transportInfo.TraitInfoOrDefault<BuildableInfo>();
			if (buildable == null || !buildable.Queue.Any(queueType => AIUtils.FindQueues(player, queueType)
				.Any(queue => queue.BuildableItems().Any(item => item.Name == Info.TransportHelicopterActor))))
				return;

			var committed = world.Actors.Count(a => a.Owner == player && !a.IsDead &&
				Info.TransportHelicopterTypes.Contains(a.Info.Name));
			foreach (var queue in world.ActorsWithTrait<ProductionQueue>().Where(q => q.Actor.Owner == player))
				committed += queue.Trait.AllQueued().Count(i => Info.TransportHelicopterTypes.Contains(i.Item));

			var builder = production.FirstOrDefault(p => !p.IsTraitDisabled);
			if (builder == null)
				return;

			var requester = (IBotRequestUnitProduction)builder;
			committed += requester.RequestedProductionCount(bot, Info.TransportHelicopterActor);
			if (committed >= Info.TransportHelicopterLimit ||
				requester.RequestedProductionCount(bot, Info.TransportHelicopterActor) > 0)
				return;

			requester.RequestUnitProduction(bot, Info.TransportHelicopterActor);
			Debug("requested {0}: committed={1}/{2}", Info.TransportHelicopterActor,
				committed, Info.TransportHelicopterLimit);
		}

		void ServiceIdleTransports()
		{
			if (bot == null || squadManager == null)
				return;

			var baseCenter = squadManager.GetRandomBaseCenter();
			foreach (var transport in world.Actors.Where(a => IsUsable(a) && a.Owner == player &&
				Info.TransportHelicopterTypes.Contains(a.Info.Name) && !coordinator.IsReserved(a.ActorID) &&
				!IsReservedForOtherBehavior(a) &&
				a.TraitOrDefault<Cargo>()?.IsEmpty() == true && IsCarrierIdle(a)).OrderBy(a => a.ActorID))
			{
				if (NeedsRepair(transport))
				{
					safeIdleStagingCells.Remove(transport.ActorID);
					var repairable = transport.TraitOrDefault<Repairable>();
					var repair = repairable?.FindRepairBuilding(transport);
					if (repair != null)
					{
						IssueRoutedMove(transport, repair.Location, new Order("Repair", transport,
							Target.FromActor(repair), true));
						Debug("sent damaged idle {0} to repair at {1}", transport, repair);
						continue;
					}
				}
				else if (safeIdleStagingCells.TryGetValue(transport.ActorID, out var safeStagingCell))
				{
					if ((transport.Location - safeStagingCell).LengthSquared <=
						Info.UnloadRangeCells * Info.UnloadRangeCells)
						continue;

					safeIdleStagingCells.Remove(transport.ActorID);
				}

				if ((transport.Location - baseCenter).LengthSquared > Info.IdleStagingRadius * Info.IdleStagingRadius)
				{
					IssueRoutedMove(transport, baseCenter);
					Debug("staged idle {0} at base", transport);
				}
			}
		}

		void RememberSafeIdleStaging(Actor transport, CPos cell)
		{
			if (transport != null)
				safeIdleStagingCells[transport.ActorID] = cell;
		}

		void IssueRoutedMove(Actor transport, CPos destination, Order finalOrder = null)
		{
			var route = AirStateBase.SafeIndependentAirRoute(squadManager, transport, destination) ?? new List<CPos>();
			IssueRoutedMove(transport, destination, route, finalOrder);
		}

		void IssueRoutedMove(Actor transport, CPos destination, List<CPos> route, Order finalOrder = null)
		{
			Debug("routed {0} to {1} via {2} threat-aware waypoint(s)", transport, destination, route.Count);
			var queued = false;
			foreach (var waypoint in route)
			{
				bot.QueueOrder(new Order("Move", transport, Target.FromCell(world, waypoint), queued));
				queued = true;
			}

			if (route.Count == 0 || route[route.Count - 1] != destination)
			{
				bot.QueueOrder(new Order("Move", transport, Target.FromCell(world, destination), queued));
				queued = true;
			}

			if (finalOrder != null)
				bot.QueueOrder(finalOrder);
		}

		void RecoverTimedOutCargo(Mission mission, string reason = "mission deadline expired")
		{
			if (!mission.Recovery.TryBeginReturn(world.WorldTick, Info.MissionTimeoutTicks))
				return;

			var baseCenter = squadManager?.GetRandomBaseCenter() ?? mission.Transport.Location;
			mission.RecoveryObjective = baseCenter;
			mission.Stage = MissionStage.Travelling;
			mission.LastOrderTick = world.WorldTick;
			mission.UnloadPlan = null;
			mission.PlanFailureTick = 0;
			if (EnsureUnloadPlan(mission, baseCenter, out var rejection) && IssueUnloadPlanRoute(mission))
				Debug("mission {0} withdrawing for planned safe unload: reason={1} carrier={2} exit={3}",
					mission.Id, reason, mission.UnloadPlan.CarrierCell, mission.UnloadPlan.ExitCells[0]);
			else
				HandlePlanFailure(mission, $"safe recovery plan unavailable: {rejection}");
		}

		bool NeedsRepair(Actor actor)
		{
			var health = actor.TraitOrDefault<IHealth>();
			return health != null && health.HP * 100L < health.MaxHP * Info.RepairHealthPercent;
		}

		bool ReadyToRetry(Mission mission)
		{
			return world.WorldTick - mission.LastOrderTick >= Info.ScanInterval;
		}

		void FinishMission(int index, string reason)
		{
			var mission = missions[index];
			if (reason == "capture handoff timed out" && mission.Objective != null)
				timedOutObjectives.Record(mission.Passenger.ActorID, mission.Objective.ActorID);

			coordinator.Release(mission.Id);
			missions.RemoveAt(index);
			Debug("released mission {0}: {1}", mission.Id, reason);
		}

		static bool IsUsable(Actor actor)
		{
			return actor != null && !actor.IsDead && actor.IsInWorld;
		}

		static bool IsMissionPassengerUsable(Mission mission)
		{
			if (mission.Passenger == null || mission.Passenger.IsDead)
				return false;

			var passenger = mission.Passenger.TraitOrDefault<Passenger>();
			return mission.Passenger.IsInWorld || passenger?.Transport == mission.Transport;
		}

		static bool IsCarrierIdle(Actor actor)
		{
			return actor.IsIdle || actor.CurrentActivity is FlyIdle;
		}

		void Debug(string format, params object[] args)
		{
			if (Info.DebugLogging)
				Log.Write("debug", "AI transport [{0}]: {1}", player.InternalName, string.Format(format, args));
		}
	}
}
