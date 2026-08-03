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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Owns the optional infantry transport mission lifecycle while sharing reservations and air routing
	/// with the general transport manager.
	/// </summary>
	public sealed class InfantryAssaultTransportManager
	{
		enum MissionStage { Gathering, Travelling, Unloading }

		sealed class Mission
		{
			public readonly int Id;
			public readonly Actor Transport;
			public readonly List<Actor> Passengers;
			public readonly CPos Destination;
			public readonly int CreatedTick;
			public readonly UnitStance? OriginalStance;
			public readonly bool UsesAircraft;
			public MissionStage Stage;
			public int LastOrderTick;
			public bool EmergencyUnload;

			public Mission(int id, Actor transport, List<Actor> passengers, CPos destination,
				int createdTick, UnitStance? originalStance, bool usesAircraft)
			{
				Id = id;
				Transport = transport;
				Passengers = passengers;
				Destination = destination;
				CreatedTick = LastOrderTick = createdTick;
				OriginalStance = originalStance;
				UsesAircraft = usesAircraft;
			}
		}

		readonly World world;
		readonly Player player;
		readonly TransportManagerBotModuleInfo info;
		readonly TransportMissionCoordinator coordinator;
		readonly Action<Actor, CPos, Order> issueRoutedAirMove;
		readonly Action requestTransportHelicopter;
		UnitBuilderBotModule[] production;
		Mission mission;
		bool selected;
		int nextMissionTick;

		public InfantryAssaultTransportManager(World world, Player player, TransportManagerBotModuleInfo info,
			TransportMissionCoordinator coordinator, Action<Actor, CPos, Order> issueRoutedAirMove,
			Action requestTransportHelicopter)
		{
			this.world = world;
			this.player = player;
			this.info = info;
			this.coordinator = coordinator;
			this.issueRoutedAirMove = issueRoutedAirMove;
			this.requestTransportHelicopter = requestTransportHelicopter;
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

			if (world.WorldTick - mission.CreatedTick >= info.MissionTimeoutTicks && mission.Stage != MissionStage.Unloading)
			{
				BeginUnload(bot, "mission timeout", true);
				return;
			}

			if (mission.Stage == MissionStage.Gathering)
				AdvanceGathering(bot, cargo);
			else if (mission.Stage == MissionStage.Travelling)
				AdvanceTravel(bot);
			else if (cargo.IsEmpty())
				FinishMission(bot, mission.EmergencyUnload ? "emergency unload complete" : "assault handoff", false);
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
				FinishMission(bot, "transport attacked before loading", true);
				return;
			}

			BeginUnload(bot, "transport damaged", true);
		}

		void AdvanceGathering(IBot bot, Cargo cargo)
		{
			var gatheringTicks = world.WorldTick - mission.CreatedTick;
			if (InfantryAssaultPolicy.ReadyToTravel(cargo.PassengerCount, mission.Passengers.Count,
				info.MinimumAssaultPassengers, gatheringTicks, info.AssaultGatherTimeoutTicks))
			{
				mission.Stage = MissionStage.Travelling;
				mission.LastOrderTick = world.WorldTick;
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
				else
					BeginUnload(bot, "insufficient passengers", false);

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
			if ((mission.Transport.Location - mission.Destination).LengthSquared <=
				info.UnloadRangeCells * info.UnloadRangeCells)
			{
				BeginUnload(bot, "destination reached", false);
				return;
			}

			if ((mission.Transport.IsIdle || mission.Transport.CurrentActivity == null) && ReadyToRetry())
			{
				IssueTravelOrder(bot);
				mission.LastOrderTick = world.WorldTick;
				Debug("mission {0} reissued travel order to {1}", mission.Id, mission.Destination);
			}
		}

		void IssueTravelOrder(IBot bot)
		{
			if (mission.UsesAircraft)
				issueRoutedAirMove(mission.Transport, mission.Destination, null);
			else
				bot.QueueOrder(new Order("Move", mission.Transport, Target.FromCell(world, mission.Destination), false));
		}

		void BeginUnload(IBot bot, string reason, bool emergency)
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
				!coordinator.IsReserved(a.ActorID)).OrderBy(a => a.ActorID).ToList();
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
			mission = new Mission(missionId, transport, passengers, destination, world.WorldTick, stance, usesAircraft);
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
				!coordinator.IsReserved(a.ActorID) && a.TraitOrDefault<Cargo>()?.IsEmpty() == true && !NeedsRepair(a))
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

		void Debug(string format, params object[] args)
		{
			if (info.DebugLogging)
				Log.Write("debug", "AI infantry assault [{0}]: {1}", player.InternalName, string.Format(format, args));
		}
	}
}
