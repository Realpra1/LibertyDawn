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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Coordinates opt-in AI rescue and assault transport missions.")]
	public class TransportManagerBotModuleInfo : ConditionalTraitInfo
	{
		[ActorReference]
		[Desc("Aircraft cargo actors used for rescue, long-distance transport, and heavy drops.")]
		public readonly HashSet<string> TransportHelicopterTypes = new HashSet<string>();

		[ActorReference]
		[Desc("Ground cargo actors used for infantry assault missions.")]
		public readonly HashSet<string> GroundTransportTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> InfantryPassengerTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> RescuePassengerTypes = new HashSet<string>();

		[ActorReference]
		public readonly HashSet<string> HeavyDropPassengerTypes = new HashSet<string>();

		[ActorReference]
		[Desc("Enemy actor types considered as transport assault/drop destinations.")]
		public readonly HashSet<string> DestinationTargetTypes = new HashSet<string>();

		[ActorReference]
		[Desc("Transport helicopter requested from unit production when a mission is waiting.")]
		public readonly string TransportHelicopterActor = null;

		public readonly bool EnableUnroutableRescue = false;
		public readonly bool EnableGroundInfantryAssault = false;
		public readonly bool EnableLongDistanceInfantryTransport = false;
		public readonly bool EnableHeavyDrop = false;
		public readonly bool DebugLogging = false;

		[Desc("Ticks between bounded transport planning updates.")]
		public readonly int ScanInterval = 75;

		public readonly int MaximumActiveMissions = 4;
		public readonly int TransportHelicopterLimit = 10;
		public readonly int PersistentUnroutableScans = 3;
		public readonly int MaximumRescueCandidatesPerScan = 32;
		public readonly int MoveIntentMaximumAge = 1500;
		public readonly int MinimumAssaultPassengers = 3;
		public readonly int MaximumAssaultPassengers = 8;
		public readonly int LongDistancePassengerCount = 8;
		[Desc("Desired number of concurrent one-vehicle helicopter missions in a heavy drop wave.")]
		public readonly int HeavyDropUnitCount = 10;
		public readonly int LongDistanceMinimumCells = 35;
		public readonly int PickupRangeCells = 3;
		public readonly int UnloadRangeCells = 4;
		public readonly int DefenselessScanRadius = 10;
		public readonly int MissionTimeoutTicks = 3000;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (ScanInterval <= 0 || MaximumActiveMissions <= 0 || TransportHelicopterLimit < 0 ||
				PersistentUnroutableScans <= 0 || MaximumRescueCandidatesPerScan <= 0 || MoveIntentMaximumAge <= 0 || MinimumAssaultPassengers <= 0 ||
				MaximumAssaultPassengers < MinimumAssaultPassengers || LongDistancePassengerCount <= 0 ||
				HeavyDropUnitCount <= 0 || LongDistanceMinimumCells <= 0 || PickupRangeCells <= 0 ||
				UnloadRangeCells <= 0 || DefenselessScanRadius <= 0 || MissionTimeoutTicks <= 0)
				throw new YamlException("AI transport counts, ranges, intervals, and timeouts must be positive and internally consistent.");
		}

		public override object Create(ActorInitializer init) { return new TransportManagerBotModule(init.Self, this); }
	}

	public class TransportManagerBotModule : ConditionalTrait<TransportManagerBotModuleInfo>, IBotEnabled, IBotTick,
		IBotTransportReservations, INotifyDamage
	{
		enum MissionStage { Gathering, Travelling, Unloading }

		sealed class Mission
		{
			public readonly int Id;
			public readonly TransportMissionKind Kind;
			public readonly Actor Transport;
			public readonly List<Actor> Passengers;
			public readonly CPos Destination;
			public readonly int CreatedTick;
			public MissionStage Stage;
			public int LastOrderTick;
			public bool EmergencyUnload;

			public Mission(int id, TransportMissionKind kind, Actor transport, List<Actor> passengers,
				CPos destination, int createdTick)
			{
				Id = id;
				Kind = kind;
				Transport = transport;
				Passengers = passengers;
				Destination = destination;
				CreatedTick = createdTick;
				LastOrderTick = createdTick;
			}
		}

		sealed class UnroutableObservation
		{
			public CPos Destination;
			public int Count;
		}

		readonly World world;
		readonly Player player;
		readonly TransportMissionCoordinator coordinator;
		readonly List<Mission> missions = new List<Mission>();
		readonly Dictionary<uint, UnroutableObservation> unroutable = new Dictionary<uint, UnroutableObservation>();
		IBot bot;
		IBotRequestUnitProduction[] production;
		int scanTicks;

		public TransportManagerBotModule(Actor self, TransportManagerBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
			coordinator = new TransportMissionCoordinator(info.MaximumActiveMissions);
		}

		protected override void Created(Actor self)
		{
			production = self.Owner.PlayerActor.TraitsImplementing<IBotRequestUnitProduction>().ToArray();
		}

		protected override void TraitEnabled(Actor self)
		{
			scanTicks = Info.ScanInterval;
		}

		void IBotEnabled.BotEnabled(IBot enabledBot) { bot = enabledBot; }

		bool IBotTransportReservations.IsTransportReserved(Actor actor)
		{
			return actor != null && coordinator.IsReserved(actor.ActorID);
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (IsTraitDisabled || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			CleanAndAdvanceMissions();
			if (coordinator.MissionCount >= Info.MaximumActiveMissions)
				return;

			if (Info.EnableUnroutableRescue && TryCreateRescueMission())
				return;
			if (Info.EnableHeavyDrop && TryCreateHeavyDrop())
				return;
			if (Info.EnableGroundInfantryAssault && TryCreateInfantryMission(false))
				return;
			if (Info.EnableLongDistanceInfantryTransport)
				TryCreateInfantryMission(true);
		}

		void INotifyDamage.Damaged(Actor damaged, AttackInfo e)
		{
			if (IsTraitDisabled || e.Damage.Value <= 0 || damaged.Owner != player)
				return;

			var mission = missions.FirstOrDefault(m => m.Transport == damaged);
			var cargo = damaged.TraitOrDefault<Cargo>();
			if (bot == null || mission == null || cargo == null || cargo.IsEmpty())
				return;

			mission.EmergencyUnload = true;
			mission.Stage = MissionStage.Unloading;
			bot.QueueOrder(new Order("Unload", damaged, false));
			Debug("mission {0} emergency-unloading {1} after damage", mission.Id, damaged);
		}

		void CleanAndAdvanceMissions()
		{
			for (var i = missions.Count - 1; i >= 0; i--)
			{
				var mission = missions[i];
				if (!IsUsable(mission.Transport) || world.WorldTick - mission.CreatedTick > Info.MissionTimeoutTicks)
				{
					FinishMission(i, "invalid or timed out");
					continue;
				}

				var cargo = mission.Transport.TraitOrDefault<Cargo>();
				if (cargo == null)
				{
					FinishMission(i, "lost cargo trait");
					continue;
				}

				if (mission.Stage == MissionStage.Unloading)
				{
					if (cargo.IsEmpty())
						FinishMission(i, mission.EmergencyUnload ? "emergency unload complete" : "complete");
					else if (mission.Transport.IsIdle && world.WorldTick - mission.LastOrderTick >= Info.ScanInterval)
					{
						bot.QueueOrder(new Order("Unload", mission.Transport, false));
						mission.LastOrderTick = world.WorldTick;
					}

					continue;
				}

				if (mission.Stage == MissionStage.Gathering)
					AdvanceGathering(mission, cargo);
				else
					AdvanceTravel(mission, cargo);
			}
		}

		void AdvanceGathering(Mission mission, Cargo cargo)
		{
			var loadedIds = new HashSet<uint>(cargo.Passengers.Select(a => a.ActorID));
			var waiting = mission.Passengers.FirstOrDefault(a => IsUsable(a) && !loadedIds.Contains(a.ActorID));
			var minimum = mission.Kind == TransportMissionKind.InfantryAssault ? Info.MinimumAssaultPassengers : mission.Passengers.Count;
			if (waiting == null)
			{
				if (cargo.PassengerCount >= minimum)
					StartTravel(mission);
				return;
			}

			if (world.WorldTick - mission.LastOrderTick < Info.ScanInterval)
				return;
			if (waiting.Trait<Passenger>().ReservedCargo == cargo)
				return;

			if ((mission.Transport.Location - waiting.Location).LengthSquared > Info.PickupRangeCells * Info.PickupRangeCells)
				bot.QueueOrder(new Order("Move", mission.Transport, Target.FromCell(world, waiting.Location), false));
			else
				bot.QueueOrder(new Order("EnterTransport", waiting, Target.FromActor(mission.Transport), false));

			mission.LastOrderTick = world.WorldTick;
		}

		void StartTravel(Mission mission)
		{
			mission.Stage = MissionStage.Travelling;
			mission.LastOrderTick = world.WorldTick;
			bot.QueueOrder(new Order("Move", mission.Transport, Target.FromCell(world, mission.Destination), false));
			Debug("mission {0} travelling to {1}", mission.Id, mission.Destination);
		}

		void AdvanceTravel(Mission mission, Cargo cargo)
		{
			var distance = (mission.Transport.Location - mission.Destination).LengthSquared;
			if (distance <= Info.UnloadRangeCells * Info.UnloadRangeCells)
			{
				mission.Stage = MissionStage.Unloading;
				mission.LastOrderTick = world.WorldTick;
				bot.QueueOrder(new Order("Unload", mission.Transport, false));
				return;
			}

			if (mission.Transport.IsIdle && world.WorldTick - mission.LastOrderTick >= Info.ScanInterval)
			{
				bot.QueueOrder(new Order("Move", mission.Transport, Target.FromCell(world, mission.Destination), false));
				mission.LastOrderTick = world.WorldTick;
			}
		}

		bool TryCreateRescueMission()
		{
			foreach (var actorId in unroutable.Keys.Where(id => !IsUsable(world.GetActorById(id))).ToList())
				unroutable.Remove(actorId);

			var candidates = OwnedActors(Info.RescuePassengerTypes)
				.Where(a => a.TraitOrDefault<Mobile>()?.LastMoveOrderDestination != null)
				.OrderBy(a => a.ActorID).Take(Info.MaximumRescueCandidatesPerScan).ToList();

			foreach (var actor in candidates)
			{
				var mobile = actor.Trait<Mobile>();
				if (world.WorldTick - mobile.LastMoveOrderTick > Info.MoveIntentMaximumAge ||
					(actor.Location - mobile.LastMoveOrderDestination.Value).LengthSquared <= Info.UnloadRangeCells * Info.UnloadRangeCells)
				{
					unroutable.Remove(actor.ActorID);
					continue;
				}

				var path = mobile.Pathfinder.FindUnitPath(actor.Location, mobile.LastMoveOrderDestination.Value,
					actor, null, BlockedByActor.Stationary);
				if (path.Count > 0)
				{
					unroutable.Remove(actor.ActorID);
					continue;
				}

				if (!unroutable.TryGetValue(actor.ActorID, out var observation) || observation.Destination != mobile.LastMoveOrderDestination.Value)
					unroutable[actor.ActorID] = observation = new UnroutableObservation { Destination = mobile.LastMoveOrderDestination.Value };

				if (++observation.Count < Info.PersistentUnroutableScans)
					continue;

				var transport = FindAvailableTransport(Info.TransportHelicopterTypes, actor.Location);
				if (transport == null)
				{
					RequestTransportHelicopter();
					return false;
				}

				return CreateMission(TransportMissionKind.Rescue, transport, new List<Actor> { actor }, observation.Destination);
			}

			return false;
		}

		bool TryCreateInfantryMission(bool longDistance)
		{
			var targets = EnemyDestinations(false).ToList();
			if (targets.Count == 0)
				return false;

			var transports = longDistance ? Info.TransportHelicopterTypes : Info.GroundTransportTypes;
			var destination = targets[0].Location;
			var transport = FindAvailableTransport(transports, destination);
			if (transport == null)
			{
				if (longDistance)
					RequestTransportHelicopter();
				return false;
			}

			var count = longDistance ? Info.LongDistancePassengerCount : Info.MaximumAssaultPassengers;
			var passengers = OwnedActors(Info.InfantryPassengerTypes)
				.OrderBy(a => (a.Location - transport.Location).LengthSquared).ThenBy(a => a.ActorID).Take(count).ToList();
			var minimum = longDistance ? count : Info.MinimumAssaultPassengers;
			if (passengers.Count < minimum)
				return false;
			if (longDistance && (passengers[0].Location - destination).LengthSquared <
				Info.LongDistanceMinimumCells * Info.LongDistanceMinimumCells)
				return false;

			return CreateMission(longDistance ? TransportMissionKind.LongDistanceInfantry : TransportMissionKind.InfantryAssault,
				transport, passengers, destination);
		}

		bool TryCreateHeavyDrop()
		{
			if (missions.Count(m => m.Kind == TransportMissionKind.HeavyDrop) >= Info.HeavyDropUnitCount)
				return false;

			var target = EnemyDestinations(true).FirstOrDefault();
			if (target == null)
				return false;

			var transport = FindAvailableTransport(Info.TransportHelicopterTypes, target.Location);
			if (transport == null)
			{
				RequestTransportHelicopter();
				return false;
			}

			var passenger = OwnedActors(Info.HeavyDropPassengerTypes)
				.OrderBy(a => (a.Location - transport.Location).LengthSquared).ThenBy(a => a.ActorID)
				.FirstOrDefault(a => transport.Trait<Cargo>().HasSpace(a.Trait<Passenger>().Info.Weight));
			if (passenger == null)
				return false;

			return CreateMission(TransportMissionKind.HeavyDrop, transport, new List<Actor> { passenger }, target.Location);
		}

		bool CreateMission(TransportMissionKind kind, Actor transport, List<Actor> passengers, CPos destination)
		{
			var missionId = coordinator.TryReserve(new[] { transport.ActorID }.Concat(passengers.Select(a => a.ActorID)));
			if (missionId == 0)
				return false;

			var mission = new Mission(missionId, kind, transport, passengers, destination, world.WorldTick);
			missions.Add(mission);
			bot.QueueOrder(new Order("Move", transport, Target.FromCell(world, passengers[0].Location), false));
			Debug("created {0} mission {1}: transport={2}, passengers={3}, destination={4}",
				kind, missionId, transport, passengers.Count, destination);
			return true;
		}

		IEnumerable<Actor> OwnedActors(HashSet<string> types)
		{
			return world.Actors.Where(a => IsUsable(a) && a.Owner == player && types.Contains(a.Info.Name) &&
				!coordinator.IsReserved(a.ActorID) && a.TraitOrDefault<Passenger>()?.Transport == null);
		}

		Actor FindAvailableTransport(HashSet<string> types, CPos near)
		{
			if (types == Info.TransportHelicopterTypes && missions.Count(m =>
				Info.TransportHelicopterTypes.Contains(m.Transport.Info.Name)) >= Info.TransportHelicopterLimit)
				return null;

			return world.Actors.Where(a => IsUsable(a) && a.Owner == player && types.Contains(a.Info.Name) &&
				!coordinator.IsReserved(a.ActorID) && a.TraitOrDefault<Cargo>()?.IsEmpty() == true)
				.OrderBy(a => (a.Location - near).LengthSquared).ThenBy(a => a.ActorID).FirstOrDefault();
		}

		IEnumerable<Actor> EnemyDestinations(bool requireDefenseless)
		{
			return world.Actors.Where(a => IsUsable(a) && player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy &&
				(Info.DestinationTargetTypes.Count == 0 || Info.DestinationTargetTypes.Contains(a.Info.Name)) &&
				(!requireDefenseless || IsDefenseless(a)))
				.OrderByDescending(a => a.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0).ThenBy(a => a.ActorID);
		}

		bool IsDefenseless(Actor target)
		{
			return !world.FindActorsInCircle(target.CenterPosition, WDist.FromCells(Info.DefenselessScanRadius))
				.Any(a => a != target && IsUsable(a) && player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy &&
					a.Info.HasTraitInfo<AttackBaseInfo>());
		}

		void RequestTransportHelicopter()
		{
			if (string.IsNullOrEmpty(Info.TransportHelicopterActor) || production == null || production.Length == 0)
				return;

			var live = world.Actors.Count(a => a.Owner == player && !a.IsDead && Info.TransportHelicopterTypes.Contains(a.Info.Name));
			var requested = production.Max(p => p.RequestedProductionCount(bot, Info.TransportHelicopterActor));
			if (live + requested >= Info.TransportHelicopterLimit || requested > 0)
				return;

			production[0].RequestUnitProduction(bot, Info.TransportHelicopterActor);
			Debug("requested transport helicopter {0} ({1}/{2} live)", Info.TransportHelicopterActor, live, Info.TransportHelicopterLimit);
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

		void Debug(string format, params object[] args)
		{
			if (Info.DebugLogging)
				Log.Write("debug", "AI transport [{0}]: {1}", player.InternalName, string.Format(format, args));
		}
	}
}
