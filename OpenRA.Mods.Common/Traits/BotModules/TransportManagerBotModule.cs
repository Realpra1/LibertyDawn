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
		public readonly int MaximumCandidatesPerScan = 32;
		public readonly int MoveIntentMaximumAge = 1500;
		public readonly int PickupRangeCells = 3;
		public readonly int UnloadRangeCells = 4;
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
		public readonly int HeavyDropUnloadRangeCells = 1;
		public readonly int HeavyDropDefenseRadius = 7;
		public readonly int HeavyDropMaximumDefenderValue = 3400;
		public readonly float HeavyDropMaximumAaDanger = 0f;
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (ScanInterval <= 0 || MaximumActiveMissions <= 0 || TransportHelicopterLimit <= 0 ||
				PersistentBlockedScans <= 0 || MaximumCandidatesPerScan <= 0 || MoveIntentMaximumAge <= 0 ||
				PickupRangeCells <= 0 || UnloadRangeCells <= 0 || MissionTimeoutTicks <= 0 ||
				IdleServiceInterval <= 0 || IdleStagingRadius <= 0 || RepairHealthPercent <= 0 || RepairHealthPercent > 100 ||
				AssaultSelectionPercent < 0 || AssaultSelectionPercent > 100 || MinimumAssaultPassengers <= 0 ||
				MaximumAssaultPassengers < MinimumAssaultPassengers || AssaultGatherTimeoutTicks <= 0 || AssaultCooldownTicks <= 0 ||
				AssaultOrderRetryTicks <= 0 || HeavyDropMinimumGameTicks < 0 || HeavyDropMinimumPassengers <= 0 ||
				HeavyDropMaximumPassengers < HeavyDropMinimumPassengers || HeavyDropConcurrentBoarding <= 0 ||
				HeavyDropConcurrentBoarding > HeavyDropMaximumPassengers || HeavyDropBoardingRetryTicks <= 0 ||
				HeavyDropGatherTimeoutTicks <= 0 ||
				HeavyDropMissionTimeoutTicks <= HeavyDropGatherTimeoutTicks ||
				HeavyDropCooldownTicks <= 0 || HeavyDropReplanInterval <= 0 || HeavyDropTargetCandidateLimit <= 0 ||
				HeavyDropLandingRadius <= 0 || HeavyDropFormationRadius <= 0 || HeavyDropFormationSpacing <= 0 ||
				HeavyDropUnloadRangeCells < 0 || HeavyDropDefenseRadius <= 0 || HeavyDropMaximumDefenderValue < 0 ||
				HeavyDropMaximumAaDanger < 0)
				throw new YamlException("AI transport counts, ranges, intervals, timeouts, and repair threshold must be positive and valid.");
		}

		public override object Create(ActorInitializer init) { return new TransportManagerBotModule(init.Self, this); }
	}

	public class TransportManagerBotModule : ConditionalTrait<TransportManagerBotModuleInfo>,
		IBotEnabled, IBotTick, IBotTransportReservations, IBotRespondToAttack
	{
		enum MissionStage { Gathering, Travelling, Unloading }

		sealed class Mission
		{
			public readonly int Id;
			public readonly Actor Transport;
			public readonly Actor Passenger;
			public readonly CPos Destination;
			public readonly int CreatedTick;
			public int DeadlineTick;
			public MissionStage Stage;
			public int LastOrderTick;

			public Mission(int id, Actor transport, Actor passenger, CPos destination, int tick, int deadlineTick)
			{
				Id = id;
				Transport = transport;
				Passenger = passenger;
				Destination = destination;
				CreatedTick = LastOrderTick = tick;
				DeadlineTick = deadlineTick;
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
		readonly List<Mission> missions = new List<Mission>();
		readonly Dictionary<uint, BlockedObservation> blocked = new Dictionary<uint, BlockedObservation>();
		readonly InfantryAssaultTransportManager infantryAssault;
		readonly HeavyDropTransportManager heavyDrop;
		IBot bot;
		UnitBuilderBotModule[] production;
		SquadManagerBotModule squadManager;
		int scanTicks;
		int serviceTicks;

		public TransportManagerBotModule(Actor self, TransportManagerBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
			coordinator = new TransportMissionCoordinator(info.MaximumActiveMissions);
			infantryAssault = new InfantryAssaultTransportManager(world, player, info, coordinator,
				IssueRoutedMove, RequestTransportHelicopter);
			heavyDrop = new HeavyDropTransportManager(world, player, info, coordinator,
				IssueRoutedMove, RequestTransportHelicopter, () => squadManager);
		}

		protected override void Created(Actor self)
		{
			production = self.Owner.PlayerActor.TraitsImplementing<UnitBuilderBotModule>().ToArray();
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

				if (world.WorldTick > mission.DeadlineTick)
				{
					if (cargo.IsEmpty())
						FinishMission(i, "timed out before pickup");
					else
						RecoverTimedOutCargo(mission);

					continue;
				}

				if (mission.Stage == MissionStage.Gathering)
					AdvanceGathering(mission, cargo, i);
				else if (mission.Stage == MissionStage.Travelling)
					AdvanceTravel(mission);
				else if (cargo.IsEmpty())
				{
					bot.QueueOrder(new Order("Move", mission.Passenger,
						Target.FromCell(world, mission.Destination), false));
					FinishMission(i, "rescue complete");
				}
				else if (IsCarrierIdle(mission.Transport) && ReadyToRetry(mission))
				{
					bot.QueueOrder(new Order("Unload", mission.Transport, false));
					mission.LastOrderTick = world.WorldTick;
				}
			}
		}

		void AdvanceGathering(Mission mission, Cargo cargo, int index)
		{
			if (cargo.Passengers.Any(a => a == mission.Passenger))
			{
				mission.Stage = MissionStage.Travelling;
				IssueRoutedMove(mission.Transport, mission.Destination);
				mission.LastOrderTick = world.WorldTick;
				Debug("mission {0} travelling to {1}", mission.Id, mission.Destination);
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
			if ((mission.Transport.Location - mission.Destination).LengthSquared <=
				Info.UnloadRangeCells * Info.UnloadRangeCells)
			{
				mission.Stage = MissionStage.Unloading;
				mission.LastOrderTick = world.WorldTick;
				bot.QueueOrder(new Order("Unload", mission.Transport, false));
				return;
			}

			if (IsCarrierIdle(mission.Transport) && ReadyToRetry(mission))
			{
				IssueRoutedMove(mission.Transport, mission.Destination);
				mission.LastOrderTick = world.WorldTick;
			}
		}

		bool TryCreateRescueMission()
		{
			foreach (var id in blocked.Keys.Where(id => !IsUsable(world.GetActorById(id))).ToList())
				blocked.Remove(id);

			var candidates = world.Actors
				.Where(a => IsUsable(a) && a.Owner == player && Info.RescuePassengerTypes.Contains(a.Info.Name) &&
					!coordinator.IsReserved(a.ActorID) && a.TraitOrDefault<Passenger>()?.Transport == null)
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

				Debug("confirmed persistent route failure for {0} to {1} after {2} scans",
					actor, destination, observation.Count);

				var transport = FindAvailableTransport(actor);
				if (transport == null)
				{
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

		Actor FindAvailableTransport(Actor passenger)
		{
			return world.Actors.Where(a => IsUsable(a) && a.Owner == player &&
				Info.TransportHelicopterTypes.Contains(a.Info.Name) && !coordinator.IsReserved(a.ActorID) &&
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
				a.TraitOrDefault<Cargo>()?.IsEmpty() == true && IsCarrierIdle(a)).OrderBy(a => a.ActorID))
			{
				if (NeedsRepair(transport))
				{
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

				if ((transport.Location - baseCenter).LengthSquared > Info.IdleStagingRadius * Info.IdleStagingRadius)
				{
					IssueRoutedMove(transport, baseCenter);
					Debug("staged idle {0} at base", transport);
				}
			}
		}

		void IssueRoutedMove(Actor transport, CPos destination, Order finalOrder = null)
		{
			var route = AirStateBase.SafeIndependentAirRoute(squadManager, transport, destination) ?? new List<CPos>();
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

		void RecoverTimedOutCargo(Mission mission)
		{
			var baseCenter = squadManager?.GetRandomBaseCenter() ?? mission.Transport.Location;
			mission.Stage = MissionStage.Unloading;
			mission.LastOrderTick = world.WorldTick;
			mission.DeadlineTick = world.WorldTick + Info.MissionTimeoutTicks;
			IssueRoutedMove(mission.Transport, baseCenter,
				new Order("Unload", mission.Transport, true));
			Debug("mission {0} timed out carrying cargo; returning to base for safe unload", mission.Id);
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
