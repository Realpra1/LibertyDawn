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
using OpenRA.Mods.Common.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Owns the optional infantry transport mission lifecycle while sharing reservations and air routing
	/// with the general transport manager.
	/// </summary>
	public sealed class InfantryAssaultTransportManager
	{
		enum MissionStage { Gathering, Travelling, Unloading, RecoveringCarrier }

		sealed class Mission
		{
			public readonly int Id;
			public readonly Actor Transport;
			public readonly Actor Target;
			public readonly List<Actor> Passengers;
			public readonly CPos Destination;
			public readonly CPos AssemblyCell;
			public readonly int CreatedTick;
			public readonly UnitStance? OriginalStance;
			public readonly bool UsesAircraft;
			public MissionStage Stage;
			public int LastOrderTick;
			public bool EmergencyUnload;
			public bool Returning;
			public bool CarrierRecoveryFallback;
			public int CarrierRecoveryDeadlineTick;
			public int PlanFailureTick;
			public int LastRoutedPlanRevision;
			public TransportUnloadPlan UnloadPlan;

			public Mission(int id, Actor transport, Actor target, List<Actor> passengers, CPos destination,
				CPos assemblyCell, int createdTick, UnitStance? originalStance, bool usesAircraft)
			{
				Id = id;
				Transport = transport;
				Target = target;
				Passengers = passengers;
				Destination = destination;
				AssemblyCell = assemblyCell;
				CreatedTick = LastOrderTick = createdTick;
				OriginalStance = originalStance;
				UsesAircraft = usesAircraft;
			}
		}

		readonly World world;
		readonly Player player;
		readonly TransportManagerBotModuleInfo info;
		readonly TransportMissionCoordinator coordinator;
		readonly TransportUnloadPlanner unloadPlanner;
		readonly Action<Actor, CPos, Order> issueRoutedAirMove;
		readonly Action requestTransportHelicopter;
		readonly Func<Actor, bool> isReservedForOtherBehavior;
		readonly Action<Actor, CPos> rememberSafeIdleStaging;
		UnitBuilderBotModule[] production;
		Mission mission;
		bool selected;
		int nextMissionTick;

		public InfantryAssaultTransportManager(World world, Player player, TransportManagerBotModuleInfo info,
			TransportMissionCoordinator coordinator, TransportUnloadPlanner unloadPlanner,
			Action<Actor, CPos, Order> issueRoutedAirMove,
			Action requestTransportHelicopter, Func<Actor, bool> isReservedForOtherBehavior,
			Action<Actor, CPos> rememberSafeIdleStaging)
		{
			this.world = world;
			this.player = player;
			this.info = info;
			this.coordinator = coordinator;
			this.unloadPlanner = unloadPlanner;
			this.issueRoutedAirMove = issueRoutedAirMove;
			this.requestTransportHelicopter = requestTransportHelicopter;
			this.isReservedForOtherBehavior = isReservedForOtherBehavior;
			this.rememberSafeIdleStaging = rememberSafeIdleStaging;
		}

		public void Initialize(UnitBuilderBotModule[] production)
		{
			this.production = production;
		}

		public void Enable()
		{
			var eligible = info.AssaultBotTypes.Contains(player.BotType);
			var roll = eligible && info.AssaultSelectionPercent > 0 ? world.LocalRandom.Next(100) : 100;
			selected = InfantryAssaultPolicy.SelectStrategy(eligible, info.AssaultSelectionPercent, roll);
			Debug("strategy {0}: bot={1}, roll={2}, chance={3}%", selected ? "selected" : "not selected",
				player.BotType, roll, info.AssaultSelectionPercent);
		}

		public void Tick(IBot bot)
		{
			Advance(bot);
			if (!selected || mission != null || world.WorldTick < nextMissionTick ||
				player.WinState != WinState.Undefined || !HasAssaultTarget())
				return;

			TryCreateMission(bot);
		}

		public void Advance(IBot bot)
		{
			if (mission == null)
				return;

			if (!IsUsable(mission.Transport))
			{
				FinishMission(bot, "transport unavailable", true);
				return;
			}

			var cargo = mission.Transport.TraitOrDefault<Cargo>();
			if (cargo == null)
			{
				FinishMission(bot, "transport lost cargo trait", true);
				return;
			}

			if (!mission.Returning && world.WorldTick - mission.CreatedTick >= info.MissionTimeoutTicks &&
				mission.Stage != MissionStage.Unloading && mission.Stage != MissionStage.RecoveringCarrier)
			{
				if (mission.UsesAircraft)
					BeginSafeReturn(bot, cargo, "mission timeout");
				else
					BeginUnload(bot, cargo, "mission timeout", true);

				return;
			}

			if (mission.Stage == MissionStage.RecoveringCarrier)
				AdvanceCarrierRecovery(bot);
			else if (mission.Stage == MissionStage.Gathering)
				AdvanceGathering(bot, cargo);
			else if (mission.Stage == MissionStage.Travelling)
				AdvanceTravel(bot);
			else if (cargo.IsEmpty())
				CompleteHandoff(bot);
			else if (mission.UsesAircraft)
				AdvanceAircraftUnloading(bot, cargo);
			else if (ReadyToRetry())
			{
				bot.QueueOrder(new Order("Unload", mission.Transport, false));
				mission.LastOrderTick = world.WorldTick;
			}
		}

		public void RespondToAttack(IBot bot, Actor actor, AttackInfo attack)
		{
			if (mission == null || mission.Transport != actor || attack.Damage.Value <= 0)
				return;

			var cargo = actor.TraitOrDefault<Cargo>();
			if (cargo == null || cargo.IsEmpty())
			{
				if (mission.Stage == MissionStage.RecoveringCarrier)
				{
					Debug("mission {0} retained carrier recovery after damage={1} at {2}",
						mission.Id, attack.Damage.Value, actor.Location);
					return;
				}

				FinishMission(bot, "transport attacked before loading", true);
				return;
			}

			if (!mission.UsesAircraft)
			{
				BeginUnload(bot, cargo, "transport damaged", true);
				return;
			}

			if (NeedsRepair(actor))
				BeginSafeReturn(bot, cargo, "carrier seriously damaged");
			else
				Debug("mission {0} retained safe plan after incidental carrier damage={1}",
					mission.Id, attack.Damage.Value);
		}

		void AdvanceGathering(IBot bot, Cargo cargo)
		{
			var gatheringTicks = world.WorldTick - mission.CreatedTick;
			if (InfantryAssaultPolicy.ReadyToTravel(cargo.PassengerCount, mission.Passengers.Count,
				info.MinimumAssaultPassengers, gatheringTicks, info.AssaultGatherTimeoutTicks))
			{
				mission.Stage = MissionStage.Travelling;
				mission.LastOrderTick = world.WorldTick;
				if (mission.UsesAircraft)
				{
					if (!EnsureAircraftPlan(cargo, mission.Destination, out var rejection) ||
						!IssueAircraftPlanRoute(bot))
					{
						HandleAircraftPlanFailure(bot, cargo, rejection ?? "threat route failed");
						return;
					}
				}
				else
					IssueTravelOrder(bot);

				Debug("mission {0} travelling with {1}/{2} passengers to {3}", mission.Id,
					cargo.PassengerCount, mission.Passengers.Count, mission.Destination);
				return;
			}

			if (InfantryAssaultPolicy.AbandonGathering(cargo.PassengerCount, info.MinimumAssaultPassengers,
				gatheringTicks, info.AssaultGatherTimeoutTicks))
			{
				if (cargo.IsEmpty())
					FinishMission(bot, "insufficient passengers", true);
				else if (mission.UsesAircraft)
					BeginSafeReturn(bot, cargo, "insufficient passengers");
				else
					BeginUnload(bot, cargo, "insufficient passengers", false);

				return;
			}

			if (!ReadyToRetry())
				return;

			foreach (var passenger in mission.Passengers.Where(IsUsable).OrderBy(a => a.ActorID))
			{
				var passengerTrait = passenger.TraitOrDefault<Passenger>();
				if (passengerTrait?.Transport == mission.Transport || passengerTrait?.ReservedCargo == cargo)
					continue;

				bot.QueueOrder(new Order("EnterTransport", passenger, Target.FromActor(mission.Transport), false));
			}

			mission.LastOrderTick = world.WorldTick;
		}

		void AdvanceTravel(IBot bot)
		{
			if (mission.UsesAircraft)
			{
				AdvanceAircraftTravel(bot);
				return;
			}

			if ((mission.Transport.Location - mission.Destination).LengthSquared <=
				info.UnloadRangeCells * info.UnloadRangeCells)
			{
				BeginUnload(bot, mission.Transport.Trait<Cargo>(), "destination reached", false);
				return;
			}

			if (IsCarrierIdle(mission.Transport) && ReadyToRetry())
			{
				IssueTravelOrder(bot);
				mission.LastOrderTick = world.WorldTick;
				Debug("mission {0} reissued travel order to {1}", mission.Id, mission.Destination);
			}
		}

		void AdvanceAircraftTravel(IBot bot)
		{
			var cargo = mission.Transport.Trait<Cargo>();
			var objective = mission.Returning ? mission.AssemblyCell : mission.Destination;
			if (!EnsureAircraftPlan(cargo, objective, out var rejection))
			{
				HandleAircraftPlanFailure(bot, cargo, rejection);
				return;
			}

			if (mission.LastRoutedPlanRevision != mission.UnloadPlan.Revision)
			{
				if (IssueAircraftPlanRoute(bot))
					mission.LastOrderTick = world.WorldTick;
				else
					HandleAircraftPlanFailure(bot, cargo, "replacement threat route failed");

				return;
			}

			if ((mission.Transport.Location - mission.UnloadPlan.CarrierCell).LengthSquared <=
				info.UnloadRangeCells * info.UnloadRangeCells)
			{
				mission.Stage = MissionStage.Unloading;
				mission.LastOrderTick = world.WorldTick;
				bot.QueueOrder(TransportUnloadOrder.Create(world, mission.Transport, mission.UnloadPlan));
				Debug("mission {0} committing exact aircraft unload: carrier={1} exits={2} objective={3} " +
					"revision={4} snapshot={5} outcome={6}", mission.Id, mission.UnloadPlan.CarrierCell,
					string.Join(",", mission.UnloadPlan.ExitCells), objective, mission.UnloadPlan.Revision,
					mission.UnloadPlan.SnapshotTick, mission.Returning ? "safe-return" : "assault");
				return;
			}

			if (IsCarrierIdle(mission.Transport) && ReadyToRetry())
			{
				if (IssueAircraftPlanRoute(bot))
					mission.LastOrderTick = world.WorldTick;
				else
					HandleAircraftPlanFailure(bot, cargo, "threat route failed");
			}
		}

		void AdvanceAircraftUnloading(IBot bot, Cargo cargo)
		{
			var objective = mission.Returning ? mission.AssemblyCell : mission.Destination;
			if (!EnsureAircraftPlan(cargo, objective, out var rejection))
			{
				HandleAircraftPlanFailure(bot, cargo, rejection);
				return;
			}

			if ((mission.Transport.Location - mission.UnloadPlan.CarrierCell).LengthSquared >
				info.UnloadRangeCells * info.UnloadRangeCells)
			{
				mission.Stage = MissionStage.Travelling;
				if (IssueAircraftPlanRoute(bot))
					mission.LastOrderTick = world.WorldTick;
				else
					HandleAircraftPlanFailure(bot, cargo, "replanned unload route failed");
				return;
			}

			if (ReadyToRetry())
			{
				bot.QueueOrder(TransportUnloadOrder.Create(world, mission.Transport, mission.UnloadPlan));
				mission.LastOrderTick = world.WorldTick;
			}
		}

		bool EnsureAircraftPlan(Cargo cargo, CPos objective, out string rejection)
		{
			var passengers = cargo.Passengers.Where(a => mission.Passengers.Contains(a) && !a.IsDead)
				.OrderBy(a => a.ActorID).ToArray();
			var previous = mission.UnloadPlan;
			var invalidation = "none";
			if (previous != null && previous.Objective == objective)
			{
				if (unloadPlanner.Revalidate(mission.Id, mission.Transport, passengers, previous,
					info.AssaultLandingUsefulnessRadiusCells, out rejection))
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
			if (!unloadPlanner.TryPlan(mission.Id, mission.Transport, passengers, objective,
				info.AssaultLandingSearchRadiusCells, info.AssaultLandingUsefulnessRadiusCells,
				revision, out var replacement, out rejection))
			{
				mission.UnloadPlan = null;
				return false;
			}

			mission.UnloadPlan = replacement;
			mission.PlanFailureTick = 0;
			if (previous == null || previous.CarrierCell != replacement.CarrierCell ||
				!previous.ExitCells.SequenceEqual(replacement.ExitCells))
				Debug("mission {0} selected aircraft unload plan: objective={1} carrier={2} exits={3} " +
					"revision={4} snapshot={5} threats={6} candidates={7} firstThreatRejection={8} replannedBecause={9}",
					mission.Id, objective, replacement.CarrierCell, string.Join(",", replacement.ExitCells),
					replacement.Revision, replacement.SnapshotTick, unloadPlanner.SnapshotThreatCount,
					replacement.CandidatesEvaluated, replacement.FirstThreatRejection ?? "none", invalidation);

			return true;
		}

		bool IssueAircraftPlanRoute(IBot bot)
		{
			var route = unloadPlanner.Route(mission.Transport, mission.UnloadPlan);
			if (route == null || (route.Count == 0 && mission.Transport.Location != mission.UnloadPlan.CarrierCell))
				return false;

			var queued = false;
			foreach (var waypoint in route)
			{
				bot.QueueOrder(new Order("Move", mission.Transport, Target.FromCell(world, waypoint), queued));
				queued = true;
			}

			mission.LastRoutedPlanRevision = mission.UnloadPlan.Revision;
			Debug("mission {0} routed aircraft plan: carrier={1} waypoints={2} snapshot={3} revision={4}",
				mission.Id, mission.UnloadPlan.CarrierCell, route.Count, mission.UnloadPlan.SnapshotTick,
				mission.UnloadPlan.Revision);
			return true;
		}

		void HandleAircraftPlanFailure(IBot bot, Cargo cargo, string rejection)
		{
			if (mission.PlanFailureTick == 0)
			{
				mission.PlanFailureTick = world.WorldTick;
				bot.QueueOrder(new Order("Stop", mission.Transport, false));
				Debug("mission {0} holding aircraft without unload plan: objective={1} reason={2}",
					mission.Id, mission.Returning ? mission.AssemblyCell : mission.Destination, rejection);
			}

			if (world.WorldTick - mission.PlanFailureTick < info.LandingHoldTicks)
				return;

			if (!mission.Returning)
			{
				BeginSafeReturn(bot, cargo, "bounded assault-plan hold expired");
				return;
			}

			if (TryPlanSafeReturnFallback(cargo, out var fallbackRejection) && IssueAircraftPlanRoute(bot))
			{
				mission.Stage = MissionStage.Travelling;
				mission.LastOrderTick = world.WorldTick;
				Debug("mission {0} terminal safe fallback: searchCenter={1}, assembly={2}, carrier={3}, exits={4}",
					mission.Id, mission.Transport.Location, mission.AssemblyCell, mission.UnloadPlan.CarrierCell,
					string.Join(",", mission.UnloadPlan.ExitCells));
				return;
			}

			mission.PlanFailureTick = world.WorldTick;
			Debug("mission {0} bounded safe hold renewed: assembly={1}, reason={2}",
				mission.Id, mission.AssemblyCell, fallbackRejection);
		}

		bool TryPlanSafeReturnFallback(Cargo cargo, out string rejection)
		{
			var passengers = cargo.Passengers.Where(a => mission.Passengers.Contains(a) && !a.IsDead)
				.OrderBy(a => a.ActorID).ToArray();
			var revision = (mission.UnloadPlan?.Revision ?? 0) + 1;
			if (!unloadPlanner.TryPlanWithoutClaim(mission.Id, mission.Transport, passengers,
				mission.Transport.Location, mission.AssemblyCell, info.SafeReturnLandingSearchRadiusCells,
				info.SafeReturnUsefulnessRadiusCells, revision, Array.Empty<CPos>(),
				out var replacement, out rejection) ||
				!unloadPlanner.TryClaimPlans(mission.Id, new[] { replacement }, out rejection))
				return false;

			mission.UnloadPlan = replacement;
			mission.PlanFailureTick = 0;
			return true;
		}

		void BeginSafeReturn(IBot bot, Cargo cargo, string reason)
		{
			mission.Returning = true;
			mission.CarrierRecoveryFallback = false;
			mission.EmergencyUnload = false;
			mission.Stage = MissionStage.Travelling;
			mission.UnloadPlan = null;
			mission.PlanFailureTick = 0;
			mission.LastOrderTick = world.WorldTick;
			coordinator.ReleaseCells(mission.Id);
			if (EnsureAircraftPlan(cargo, mission.AssemblyCell, out var rejection) && IssueAircraftPlanRoute(bot))
				Debug("mission {0} withdrawing to safe assembly unload: reason={1} carrier={2} exits={3}",
					mission.Id, reason, mission.UnloadPlan.CarrierCell, string.Join(",", mission.UnloadPlan.ExitCells));
			else
				HandleAircraftPlanFailure(bot, cargo, "safe assembly plan unavailable: " + rejection);
		}

		void CompleteHandoff(IBot bot)
		{
			if (!mission.UsesAircraft)
			{
				FinishMission(bot, mission.EmergencyUnload ? "emergency unload complete" : "assault handoff", false);
				return;
			}

			// The retained plan is narrowed as passengers physically exit. Handoff must include every
			// surviving mission passenger that is now back in the world, not only the last unload order.
			var delivered = mission.Passengers.Where(IsUsable)
				.OrderBy(a => a.ActorID).ToArray();
			if (!mission.Returning && delivered.Length > 0)
			{
				var destination = IsUsable(mission.Target) ? mission.Target.Location : mission.Destination;
				bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(world, destination), false,
					groupedActors: delivered));
				Debug("mission {0} physical aircraft handoff: passengers={1} actualCells={2} destination={3} " +
					"cargo=0 outcome=assault", mission.Id, delivered.Length,
					string.Join(",", delivered.Select(a => a.ActorID + ":" + a.Location)), destination);
				BeginCarrierRecovery(bot);
				return;
			}
			else
				Debug("mission {0} physical aircraft handoff: passengers={1} assembly={2} cargo=0 outcome=safe-return",
					mission.Id, delivered.Length, mission.AssemblyCell);

			FinishMission(bot, mission.Returning ? "safe aircraft return complete" : "air assault handoff", false);
		}

		void BeginCarrierRecovery(IBot bot)
		{
			mission.Returning = true;
			mission.Stage = MissionStage.RecoveringCarrier;
			mission.UnloadPlan = null;
			mission.PlanFailureTick = 0;
			mission.LastOrderTick = world.WorldTick;
			mission.CarrierRecoveryDeadlineTick = world.WorldTick + info.MissionTimeoutTicks;
			coordinator.ReleaseCells(mission.Id);
			if (EnsureCarrierRecoveryPlan(out var rejection) && IssueAircraftPlanRoute(bot))
				Debug("mission {0} began post-handoff carrier recovery: assembly={1} carrier={2} revision={3}",
					mission.Id, mission.AssemblyCell, mission.UnloadPlan.CarrierCell, mission.UnloadPlan.Revision);
			else
				HandleCarrierRecoveryFailure(bot, rejection ?? "carrier recovery route failed");
		}

		void AdvanceCarrierRecovery(IBot bot)
		{
			if (!EnsureCarrierRecoveryPlan(out var rejection))
			{
				HandleCarrierRecoveryFailure(bot, rejection);
				return;
			}

			if (mission.LastRoutedPlanRevision != mission.UnloadPlan.Revision)
			{
				if (IssueAircraftPlanRoute(bot))
					mission.LastOrderTick = world.WorldTick;
				else
					HandleCarrierRecoveryFailure(bot, "replacement carrier recovery route failed");

				return;
			}

			if ((mission.Transport.Location - mission.UnloadPlan.CarrierCell).LengthSquared <=
				info.UnloadRangeCells * info.UnloadRangeCells)
			{
				Debug("mission {0} post-handoff carrier recovered: location={1} planned={2} assembly={3}",
					mission.Id, mission.Transport.Location, mission.UnloadPlan.CarrierCell, mission.AssemblyCell);
				if (mission.CarrierRecoveryFallback)
					rememberSafeIdleStaging(mission.Transport, mission.UnloadPlan.CarrierCell);

				FinishMission(bot, "air assault carrier recovered", false);
				return;
			}

			if (IsCarrierIdle(mission.Transport) && ReadyToRetry())
			{
				if (IssueAircraftPlanRoute(bot))
					mission.LastOrderTick = world.WorldTick;
				else
					HandleCarrierRecoveryFailure(bot, "carrier recovery route failed");
			}
		}

		bool EnsureCarrierRecoveryPlan(out string rejection)
		{
			var previous = mission.UnloadPlan;
			if (previous != null && previous.Objective == mission.AssemblyCell &&
				unloadPlanner.Revalidate(mission.Id, mission.Transport, Array.Empty<Actor>(), previous, 0, out rejection))
			{
				mission.PlanFailureTick = 0;
				return true;
			}

			var revision = (previous?.Revision ?? 0) + 1;
			if (!unloadPlanner.TryPlanCarrierRecovery(mission.Id, mission.Transport, mission.AssemblyCell,
				info.SafeReturnLandingSearchRadiusCells, revision, out var replacement, out rejection))
			{
				mission.UnloadPlan = null;
				return false;
			}

			mission.UnloadPlan = replacement;
			mission.CarrierRecoveryFallback = false;
			mission.PlanFailureTick = 0;
			Debug("mission {0} selected post-handoff carrier recovery: assembly={1} carrier={2} revision={3} " +
				"snapshot={4} threats={5} candidates={6} firstThreatRejection={7}", mission.Id,
				mission.AssemblyCell, replacement.CarrierCell, replacement.Revision, replacement.SnapshotTick,
				unloadPlanner.SnapshotThreatCount, replacement.CandidatesEvaluated,
				replacement.FirstThreatRejection ?? "none");
			return true;
		}

		void HandleCarrierRecoveryFailure(IBot bot, string rejection)
		{
			if (world.WorldTick >= mission.CarrierRecoveryDeadlineTick)
			{
				Debug("mission {0} terminal empty-carrier recovery timeout: assembly={1} location={2} reason={3}",
					mission.Id, mission.AssemblyCell, mission.Transport.Location, rejection);
				FinishMission(bot, "empty carrier recovery timed out", false);
				return;
			}

			if (mission.PlanFailureTick == 0)
			{
				mission.PlanFailureTick = world.WorldTick;
				bot.QueueOrder(new Order("Stop", mission.Transport, false));
				Debug("mission {0} holding empty carrier for safe recovery: assembly={1} reason={2}",
					mission.Id, mission.AssemblyCell, rejection);
			}

			if (world.WorldTick - mission.PlanFailureTick < info.LandingHoldTicks)
				return;

			var revision = (mission.UnloadPlan?.Revision ?? 0) + 1;
			var fallbackCenter = world.Map.Clamp(mission.AssemblyCell +
				(mission.AssemblyCell - mission.Transport.Location).Sign() * info.SafeReturnLandingSearchRadiusCells);
			if (unloadPlanner.TryPlanCarrierRecovery(mission.Id, mission.Transport,
				fallbackCenter, mission.AssemblyCell, info.SafeReturnLandingSearchRadiusCells,
				revision, out var replacement, out var fallbackRejection))
			{
				mission.UnloadPlan = replacement;
				mission.PlanFailureTick = 0;
				if (IssueAircraftPlanRoute(bot))
				{
					mission.CarrierRecoveryFallback = true;
					mission.LastOrderTick = world.WorldTick;
					Debug("mission {0} empty-carrier terminal fallback: searchCenter={1} assembly={2} " +
						"carrier={3} revision={4}", mission.Id, fallbackCenter,
						mission.AssemblyCell, replacement.CarrierCell, replacement.Revision);
					return;
				}
			}

			mission.PlanFailureTick = world.WorldTick;
			Debug("mission {0} bounded empty-carrier recovery hold: assembly={1} reason={2}",
				mission.Id, mission.AssemblyCell, fallbackRejection ?? rejection);
		}

		void IssueTravelOrder(IBot bot)
		{
			if (mission.UsesAircraft)
				issueRoutedAirMove(mission.Transport, mission.Destination, null);
			else
				bot.QueueOrder(new Order("Move", mission.Transport, Target.FromCell(world, mission.Destination), false));
		}

		void BeginUnload(IBot bot, Cargo cargo, string reason, bool emergency)
		{
			if (mission == null)
				return;

			mission.Stage = MissionStage.Unloading;
			mission.EmergencyUnload |= emergency;
			mission.LastOrderTick = world.WorldTick;
			bot.QueueOrder(new Order("Unload", mission.Transport, false));
			Debug("mission {0} unloading at {1}: {2}", mission.Id, mission.Transport.Location, reason);
		}

		bool TryCreateMission(IBot bot)
		{
			var passengerPool = world.Actors.Where(a => IsUsable(a) && a.Owner == player && a.IsIdle &&
				info.AssaultPassengerTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<PassengerInfo>() &&
				!coordinator.IsReserved(a.ActorID) && !isReservedForOtherBehavior(a)).OrderBy(a => a.ActorID).ToList();
			if (passengerPool.Count < info.MinimumAssaultPassengers)
				return false;

			var groundTransport = FindTransport(info.GroundTransportTypes, passengerPool[0].Location);
			var groundRouteUnavailable = groundTransport != null &&
				!TryCreateWithTransport(bot, groundTransport, passengerPool, false);
			if (groundTransport != null && !groundRouteUnavailable)
				return true;

			if (groundTransport == null && GroundTransportTechnologyAvailable())
			{
				RequestGroundTransport(bot);
				return false;
			}

			var helicopter = FindTransport(info.TransportHelicopterTypes, passengerPool[0].Location);
			if (helicopter != null && TryCreateWithTransport(bot, helicopter, passengerPool, true))
				return true;

			if (groundRouteUnavailable || GroundProductionQueueAvailable())
				requestTransportHelicopter();

			return false;
		}

		bool TryCreateWithTransport(IBot bot, Actor transport, List<Actor> passengerPool, bool usesAircraft)
		{
			var cargo = transport.TraitOrDefault<Cargo>();
			if (cargo == null || !cargo.IsEmpty())
				return false;

			var passengers = SelectPassengers(passengerPool, transport, cargo.Info.MaxWeight);
			if (passengers.Count < info.MinimumAssaultPassengers)
				return false;

			var target = FindTarget(transport, usesAircraft);
			if (target == null)
				return false;

			var destination = usesAircraft ? target.Location : transport.Trait<Mobile>().NearestMoveableCell(target.Location, 2, 6);
			if (!usesAircraft && !HasGroundRoute(transport, destination))
				return false;

			var missionId = coordinator.TryReserve(new[] { transport.ActorID }.Concat(passengers.Select(a => a.ActorID)));
			if (missionId == 0)
				return false;

			var stance = transport.TraitOrDefault<AutoTarget>()?.Stance;
			mission = new Mission(missionId, transport, target, passengers, destination, transport.Location,
				world.WorldTick, stance, usesAircraft);
			nextMissionTick = world.WorldTick + info.AssaultCooldownTicks;
			bot.QueueOrder(new Order("Stop", transport, false));
			SetStance(bot, transport, UnitStance.HoldFire);
			foreach (var passenger in passengers)
				bot.QueueOrder(new Order("EnterTransport", passenger, Target.FromActor(transport), false));

			Debug("created mission {0}: transport={1} passengers={2} specialists={3} target={4}#{5} destination={6}",
				missionId, transport, passengers.Count,
				passengers.Count(a => info.AssaultEngineerTypes.Contains(a.Info.Name) || info.AssaultCommandoTypes.Contains(a.Info.Name)),
				target.Info.Name, target.ActorID, destination);
			return true;
		}

		List<Actor> SelectPassengers(List<Actor> candidates, Actor transport, int maximumWeight)
		{
			var nearby = candidates.OrderBy(a => (a.Location - transport.Location).LengthSquared)
				.ThenBy(a => a.ActorID).ToList();
			var engineers = nearby.Where(a => info.AssaultEngineerTypes.Contains(a.Info.Name)).Take(2).ToList();
			var commandos = nearby.Where(a => info.AssaultCommandoTypes.Contains(a.Info.Name)).Take(1).ToList();
			var preferred = new List<Actor>();
			if (engineers.Count == 2)
				preferred.AddRange(engineers);
			else if (commandos.Count > 0)
				preferred.AddRange(commandos);

			preferred.AddRange(nearby.Where(a => !info.AssaultEngineerTypes.Contains(a.Info.Name) &&
				!preferred.Contains(a)));

			var selectedPassengers = new List<Actor>();
			var weight = 0;
			foreach (var passenger in preferred)
			{
				var passengerWeight = passenger.Trait<Passenger>().Info.Weight;
				if (selectedPassengers.Count >= info.MaximumAssaultPassengers || weight + passengerWeight > maximumWeight)
					continue;

				selectedPassengers.Add(passenger);
				weight += passengerWeight;
			}

			return selectedPassengers;
		}

		Actor FindTarget(Actor transport, bool usesAircraft)
		{
			return world.Actors.Where(a => IsUsable(a) && player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy &&
				info.AssaultTargetTypes.Contains(a.Info.Name))
				.Select(a => new
				{
					Actor = a,
					Score = InfantryAssaultPolicy.TargetScore(a.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0,
						Math.Abs(a.Location.X - transport.Location.X) + Math.Abs(a.Location.Y - transport.Location.Y)),
				})
				.OrderByDescending(candidate => candidate.Score).ThenBy(candidate => candidate.Actor.ActorID)
				.Select(candidate => candidate.Actor)
				.FirstOrDefault(a => usesAircraft || HasGroundRoute(transport,
					transport.Trait<Mobile>().NearestMoveableCell(a.Location, 2, 6)));
		}

		bool HasGroundRoute(Actor transport, CPos destination)
		{
			var mobile = transport.TraitOrDefault<Mobile>();
			return mobile != null && (transport.Location == destination || mobile.Pathfinder.FindUnitPath(
				transport.Location, destination, transport, null, BlockedByActor.Stationary).Count > 0);
		}

		Actor FindTransport(HashSet<string> types, CPos origin)
		{
			return world.Actors.Where(a => IsUsable(a) && a.Owner == player && types.Contains(a.Info.Name) &&
				!coordinator.IsReserved(a.ActorID) && !isReservedForOtherBehavior(a) &&
				a.TraitOrDefault<Cargo>()?.IsEmpty() == true && !NeedsRepair(a))
				.OrderBy(a => (a.Location - origin).LengthSquared).ThenBy(a => a.ActorID).FirstOrDefault();
		}

		bool GroundTransportTechnologyAvailable()
		{
			if (string.IsNullOrEmpty(info.GroundTransportActor) ||
				!world.Map.Rules.Actors.TryGetValue(info.GroundTransportActor, out var transportInfo))
				return false;

			var buildable = transportInfo.TraitInfoOrDefault<BuildableInfo>();
			return buildable != null && buildable.Queue.Any(queueType => AIUtils.FindQueues(player, queueType)
				.Any(queue => queue.BuildableItems().Any(item => item.Name == info.GroundTransportActor)));
		}

		bool GroundProductionQueueAvailable()
		{
			if (string.IsNullOrEmpty(info.GroundTransportActor) ||
				!world.Map.Rules.Actors.TryGetValue(info.GroundTransportActor, out var transportInfo))
				return false;

			var buildable = transportInfo.TraitInfoOrDefault<BuildableInfo>();
			return buildable != null && buildable.Queue.Any(queueType => AIUtils.FindQueues(player, queueType).Any());
		}

		bool HasAssaultTarget()
		{
			return world.Actors.Any(a => IsUsable(a) &&
				player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy && info.AssaultTargetTypes.Contains(a.Info.Name));
		}

		void RequestGroundTransport(IBot bot)
		{
			if (string.IsNullOrEmpty(info.GroundTransportActor) || production == null)
				return;

			var builder = production.FirstOrDefault(p => !p.IsTraitDisabled);
			if (builder == null)
				return;

			var requester = (IBotRequestUnitProduction)builder;
			var committed = world.Actors.Count(a => a.Owner == player && !a.IsDead &&
				info.GroundTransportTypes.Contains(a.Info.Name));
			foreach (var queue in world.ActorsWithTrait<ProductionQueue>().Where(q => q.Actor.Owner == player))
				committed += queue.Trait.AllQueued().Count(i => info.GroundTransportTypes.Contains(i.Item));

			committed += requester.RequestedProductionCount(bot, info.GroundTransportActor);
			if (committed > 0)
				return;

			requester.RequestUnitProduction(bot, info.GroundTransportActor);
			Debug("requested ground transport {0}", info.GroundTransportActor);
		}

		void FinishMission(IBot bot, string reason, bool stopPassengers)
		{
			if (mission == null)
				return;

			if (IsUsable(mission.Transport))
			{
				if (mission.OriginalStance.HasValue)
					SetStance(bot, mission.Transport, mission.OriginalStance.Value);
			}

			if (stopPassengers)
				foreach (var passenger in mission.Passengers.Where(IsUsable))
					bot.QueueOrder(new Order("Stop", passenger, false));

			Debug("released mission {0}: {1}", mission.Id, reason);
			coordinator.Release(mission.Id);
			mission = null;
		}

		static void SetStance(IBot bot, Actor actor, UnitStance stance)
		{
			if (actor.Info.HasTraitInfo<AutoTargetInfo>())
				bot.QueueOrder(new Order("SetUnitStance", actor, false) { ExtraData = (uint)stance });
		}

		bool NeedsRepair(Actor actor)
		{
			var health = actor.TraitOrDefault<IHealth>();
			return health != null && health.HP * 100L < health.MaxHP * info.RepairHealthPercent;
		}

		bool ReadyToRetry()
		{
			return world.WorldTick - mission.LastOrderTick >= info.AssaultOrderRetryTicks;
		}

		static bool IsUsable(Actor actor)
		{
			return actor != null && !actor.IsDead && actor.IsInWorld;
		}

		static bool IsCarrierIdle(Actor actor)
		{
			return actor.IsIdle || actor.CurrentActivity == null || actor.CurrentActivity is FlyIdle;
		}

		void Debug(string format, params object[] args)
		{
			if (info.DebugLogging)
				Log.Write("debug", "AI infantry assault [{0}]: {1}", player.InternalName, string.Format(format, args));
		}
	}
}
