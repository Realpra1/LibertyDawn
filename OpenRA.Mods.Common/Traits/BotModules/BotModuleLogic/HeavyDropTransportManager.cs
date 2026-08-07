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
	/// Coordinates a complete multi-carrier heavy-unit drop as one reservation. Rescue and infantry
	/// transports therefore cannot steal individual carriers or passengers while the wave assembles.
	/// </summary>
	public sealed class HeavyDropTransportManager
	{
		enum WaveStage { Gathering, Travelling, Unloading }

		sealed class Pair
		{
			public readonly Actor Transport;
			public readonly Actor Passenger;
			public readonly CPos PickupDestination;
			public CPos Destination;
			public int LastOrderTick;
			public bool PickupOrdered;
			public bool BoardingOrdered;
			public TransportUnloadPlan UnloadPlan;

			public Pair(Actor transport, Actor passenger, CPos pickupDestination, CPos destination, int tick)
			{
				Transport = transport;
				Passenger = passenger;
				PickupDestination = pickupDestination;
				Destination = destination;
				LastOrderTick = tick;
			}
		}

		sealed class DropPlan
		{
			public readonly Actor Target;
			public readonly CPos LandingCenter;
			public readonly float AaDanger;
			public readonly int DefenderValue;
			public readonly long Score;
			public readonly string FirstThreatRejection;

			public DropPlan(Actor target, CPos landingCenter, float aaDanger, int defenderValue, long score,
				string firstThreatRejection)
			{
				Target = target;
				LandingCenter = landingCenter;
				AaDanger = aaDanger;
				DefenderValue = defenderValue;
				Score = score;
				FirstThreatRejection = firstThreatRejection;
			}
		}

		sealed class Wave
		{
			public readonly int Id;
			public readonly int CreatedTick;
			public readonly List<Pair> Pairs;
			public Actor Target;
			public CPos LandingCenter;
			public WaveStage Stage;
			public int LastPlanTick;
			public int TimeoutOriginTick;
			public bool ReturningToAssembly;
			public bool Aborted;
			public bool SafeReturnFallback;

			public Wave(int id, int tick, List<Pair> pairs, DropPlan plan)
			{
				Id = id;
				CreatedTick = LastPlanTick = TimeoutOriginTick = tick;
				Pairs = pairs;
				Target = plan.Target;
				LandingCenter = plan.LandingCenter;
			}
		}

		readonly World world;
		readonly Player player;
		readonly TransportManagerBotModuleInfo info;
		readonly TransportMissionCoordinator coordinator;
		readonly TransportUnloadPlanner unloadPlanner;
		readonly Action<Actor, CPos, Order> issueRoutedAirMove;
		readonly Action requestTransportHelicopter;
		readonly Func<SquadManagerBotModule> squadManager;
		readonly Func<Actor, bool> isReservedForOtherBehavior;
		readonly Action<Actor, CPos> rememberSafeIdleStaging;
		Wave wave;
		bool enabled;
		int nextWaveTick;
		int lastBlockedDiagnosticTick = -1;
		string lastBlockedDiagnostic;

		public HeavyDropTransportManager(World world, Player player, TransportManagerBotModuleInfo info,
			TransportMissionCoordinator coordinator, TransportUnloadPlanner unloadPlanner,
			Action<Actor, CPos, Order> issueRoutedAirMove,
			Action requestTransportHelicopter, Func<SquadManagerBotModule> squadManager,
			Func<Actor, bool> isReservedForOtherBehavior, Action<Actor, CPos> rememberSafeIdleStaging)
		{
			this.world = world;
			this.player = player;
			this.info = info;
			this.coordinator = coordinator;
			this.unloadPlanner = unloadPlanner;
			this.issueRoutedAirMove = issueRoutedAirMove;
			this.requestTransportHelicopter = requestTransportHelicopter;
			this.squadManager = squadManager;
			this.isReservedForOtherBehavior = isReservedForOtherBehavior;
			this.rememberSafeIdleStaging = rememberSafeIdleStaging;
		}

		public void Enable()
		{
			enabled = info.HeavyDropBotTypes.Contains(player.BotType);
			nextWaveTick = Math.Max(world.WorldTick, info.HeavyDropMinimumGameTicks);
			Debug("strategy {0}: bot={1}, earliest={2}", enabled ? "enabled" : "disabled",
				player.BotType, nextWaveTick);
		}

		public void Tick(IBot bot)
		{
			Advance(bot);
			if (!enabled || wave != null || world.WorldTick < nextWaveTick ||
				player.WinState != WinState.Undefined || squadManager() == null)
				return;

			TryCreateWave(bot);
		}

		public void Advance(IBot bot)
		{
			if (wave == null)
				return;

			if (wave.Stage == WaveStage.Gathering)
				AdvanceGathering(bot);
			else if (wave.Stage == WaveStage.Travelling)
				AdvanceTravelling(bot);
			else
				AdvanceUnloading(bot);
		}

		public void RespondToAttack(IBot bot, Actor actor, AttackInfo attack)
		{
			if (wave == null || attack.Damage.Value <= 0)
				return;

			var pair = wave.Pairs.FirstOrDefault(p => p.Transport == actor);
			if (pair == null)
				return;

			var cargo = actor.TraitOrDefault<Cargo>();
			if (cargo == null || cargo.IsEmpty())
				return;

			if (NeedsRepair(actor) && !wave.ReturningToAssembly)
			{
				var loadedPairs = wave.Pairs.Where(p => IsPairUsable(p) && IsLoaded(p)).ToList();
				ReturnWaveToAssembly(bot, loadedPairs, $"carrier {actor} seriously damaged");
			}
			else
				Debug("wave {0} retained safe unload plans after incidental carrier damage: carrier={1} damage={2}",
					wave.Id, actor, attack.Damage.Value);
		}

		void TryCreateWave(IBot bot)
		{
			var passengers = AvailablePassengers().Take(info.HeavyDropMaximumPassengers).ToList();
			if (passengers.Count < info.HeavyDropMaximumPassengers)
				return;

			var passengerWeight = passengers[0].Trait<Passenger>().Info.Weight;
			var transports = AvailableTransports(passengers[0].Location, passengerWeight)
				.Take(info.HeavyDropMaximumPassengers).ToList();
			if (transports.Count < info.HeavyDropMaximumPassengers)
			{
				requestTransportHelicopter();
				DebugBlocked("preparing wave: passengers={0}/{1}, carriers={2}/{1}",
					passengers.Count, info.HeavyDropMaximumPassengers, transports.Count);
				return;
			}

			if (!HeavyDropPolicy.CanPrepare(enabled, world.WorldTick, info.HeavyDropMinimumGameTicks,
				passengers.Count, transports.Count, info.HeavyDropMaximumPassengers))
				return;

			var plan = FindDropPlan(transports[0], passengers[0]);
			if (plan == null)
			{
				DebugBlocked("wave ready but no undefended drop site was found");
				return;
			}

			passengers = passengers.Take(info.HeavyDropMaximumPassengers).ToList();
			transports = PairNearestTransports(passengers, transports.Take(passengers.Count).ToList());
			var destinations = FindDistinctDestinations(plan.LandingCenter, passengers);
			if (destinations.Count < info.HeavyDropMinimumPassengers)
			{
				DebugBlocked("rejected drop site {0}: only {1}/{2} distinct landing cells",
					plan.LandingCenter, destinations.Count, info.HeavyDropMinimumPassengers);
				return;
			}

			var pickupDestinations = FindDistinctPickupDestinations(passengers, transports);
			if (pickupDestinations.Count < passengers.Count)
			{
				DebugBlocked("rejected wave assembly: only {0}/{1} distinct Mammoth-passable pickup cells",
					pickupDestinations.Count, passengers.Count);
				return;
			}

			var count = Math.Min(Math.Min(passengers.Count, transports.Count), destinations.Count);
			var ids = transports.Take(count).Select(a => a.ActorID)
				.Concat(passengers.Take(count).Select(a => a.ActorID));
			var missionId = coordinator.TryReserve(ids);
			if (missionId == 0)
				return;

			var pairs = Enumerable.Range(0, count)
				.Select(i => new Pair(transports[i], passengers[i], pickupDestinations[i],
					destinations[i], world.WorldTick)).ToList();
			wave = new Wave(missionId, world.WorldTick, pairs, plan);
			lastBlockedDiagnostic = null;
			lastBlockedDiagnosticTick = -1;
			nextWaveTick = world.WorldTick + info.HeavyDropCooldownTicks;
			foreach (var pair in pairs)
			{
				bot.QueueOrder(new Order("Stop", pair.Transport, false));
				bot.QueueOrder(new Order("Stop", pair.Passenger, false));
				IssueRoutedLanding(pair.Transport, pair.PickupDestination);
				pair.PickupOrdered = true;
			}

			Debug("created wave {0}: pairs={1}, target={2}#{3}, landing={4}, AA={5:0.00}, defenders={6}, " +
				"score={7}, firstStrategicThreatRejection={8}",
				missionId, pairs.Count, plan.Target.Info.Name, plan.Target.ActorID, plan.LandingCenter,
				plan.AaDanger, plan.DefenderValue, plan.Score, plan.FirstThreatRejection ?? "none");
			Debug("wave {0} routed all {1} carriers concurrently to distinct pickup cells", missionId, pairs.Count);
		}

		void AdvanceGathering(IBot bot)
		{
			var livePairs = wave.Pairs.Where(IsPairUsable).ToList();
			var loaded = livePairs.Count(IsLoaded);
			var elapsed = world.WorldTick - wave.CreatedTick;
			if (HeavyDropPolicy.ReadyToTravel(loaded, livePairs.Count, info.HeavyDropMinimumPassengers,
				elapsed, info.HeavyDropGatherTimeoutTicks))
			{
				DiscardUnloadedPairs(bot);
				wave.Stage = WaveStage.Travelling;
				if (!IssueWaveTravel(bot, "assembled"))
					ReturnWaveToAssembly(bot, wave.Pairs.Where(IsLoaded).ToList(),
						"assembled wave has no complete safe drop plans");
				return;
			}

			if (elapsed >= info.HeavyDropGatherTimeoutTicks && loaded < info.HeavyDropMinimumPassengers)
			{
				DebugGatheringFailure(livePairs, loaded);
				BeginAbortUnload(bot, "insufficient loaded carriers");
				return;
			}

			foreach (var pair in livePairs.Where(p => !IsLoaded(p) && p.PickupOrdered &&
				!p.BoardingOrdered && PickupReady(p)).OrderBy(p => p.Passenger.ActorID))
			{
				bot.QueueOrder(new Order("EnterTransport", pair.Passenger,
					Target.FromActor(pair.Transport), false));
				pair.BoardingOrdered = true;
				pair.LastOrderTick = world.WorldTick;
				Debug("wave {0} boarding staged pair: carrier={1}, passenger={2}, pickup={3}",
					wave.Id, pair.Transport, pair.Passenger, pair.PickupDestination);
			}

			foreach (var pair in livePairs.Where(p => !IsLoaded(p) && BoardingOrderExpired(p)))
			{
				bot.QueueOrder(new Order("Stop", pair.Passenger, false));
				pair.BoardingOrdered = false;
				Debug("wave {0} recovering expired boarding approach: carrier={1}, passenger={2}",
					wave.Id, pair.Transport, pair.Passenger);
			}

			foreach (var pair in livePairs.Where(p => !IsLoaded(p) && PickupOrderExpired(p)))
			{
				pair.PickupOrdered = false;
				Debug("wave {0} recovering expired pickup route: carrier={1}, pickup={2}",
					wave.Id, pair.Transport, pair.PickupDestination);
			}

			var boarding = livePairs.Count(p => !IsLoaded(p) && IsBoarding(p));
			var availableSlots = HeavyDropPolicy.AvailableBoardingSlots(info.HeavyDropConcurrentBoarding, boarding);
			foreach (var pair in livePairs.Where(p => !IsLoaded(p) && !IsBoarding(p) && ReadyToRetry(p))
				.Take(availableSlots))
			{
				IssueRoutedLanding(pair.Transport, pair.PickupDestination);
				pair.PickupOrdered = true;
				pair.LastOrderTick = world.WorldTick;
			}
		}

		void DiscardUnloadedPairs(IBot bot)
		{
			var unloaded = wave.Pairs.Where(p => !IsLoaded(p)).ToList();
			foreach (var pair in unloaded)
			{
				bot.QueueOrder(new Order("Stop", pair.Transport, false));
				bot.QueueOrder(new Order("Stop", pair.Passenger, false));
			}

			wave.Pairs.RemoveAll(p => !IsLoaded(p));
			if (unloaded.Count > 0)
				Debug("wave {0} released {1} unassembled pairs before departure", wave.Id, unloaded.Count);
		}

		void DebugGatheringFailure(List<Pair> pairs, int loaded)
		{
			Debug("wave {0} gathering timed out: loaded={1}/{2}, minimum={3}",
				wave.Id, loaded, pairs.Count, info.HeavyDropMinimumPassengers);
			foreach (var pair in pairs.Where(p => !IsLoaded(p)).OrderBy(p => p.Passenger.ActorID))
			{
				var passenger = pair.Passenger.TraitOrDefault<Passenger>();
				var aircraft = pair.Transport.TraitOrDefault<Aircraft>();
				Debug("wave {0} unboarded pair: carrier={1} at {2}, passenger={3} at {4}, pickup={5}, distance2={6}, " +
					"reserved={7}, idle={8}, landed={9}, carrierActivity={10}, passengerActivity={11}",
					wave.Id, pair.Transport, pair.Transport.Location, pair.Passenger, pair.Passenger.Location,
					pair.PickupDestination, (pair.Transport.Location - pair.PickupDestination).LengthSquared,
					passenger?.ReservedCargo == pair.Transport.TraitOrDefault<Cargo>(), pair.Passenger.IsIdle,
					aircraft?.AtLandAltitude == true, pair.Transport.CurrentActivity?.GetType().Name ?? "none",
					pair.Passenger.CurrentActivity?.GetType().Name ?? "none");
			}
		}

		void AdvanceTravelling(IBot bot)
		{
			if (world.WorldTick - wave.TimeoutOriginTick >= info.HeavyDropMissionTimeoutTicks)
			{
				DebugTravelFailure("mission timeout");
				if (!wave.ReturningToAssembly)
					ReturnWaveToAssembly(bot, wave.Pairs.Where(p => IsPairUsable(p) && IsLoaded(p)).ToList(),
						"mission timeout");
				else
					BeginAbortUnload(bot, "safe-return timeout");
				return;
			}

			var loadedPairs = wave.Pairs.Where(p => IsPairUsable(p) && IsLoaded(p)).ToList();
			if (loadedPairs.Count == 0)
			{
				wave.Stage = WaveStage.Unloading;
				AdvanceUnloading(bot);
				return;
			}

			if (world.WorldTick - wave.LastPlanTick >= info.HeavyDropReplanInterval)
			{
				wave.LastPlanTick = world.WorldTick;
				if (!RevalidateWavePlans(loadedPairs, out var rejection))
				{
					Debug("wave {0} invalidated unload-plan set: {1}", wave.Id, rejection);
					if (TryPlanWave(loadedPairs, out rejection))
					{
						IssueWaveRoutes(bot, loadedPairs, "landing plans refreshed");
						return;
					}

					if (wave.ReturningToAssembly)
					{
						foreach (var pair in loadedPairs)
							bot.QueueOrder(new Order("Stop", pair.Transport, false));

						Debug("wave {0} holding outside known danger: no safe assembly unload-plan set; reason={1}",
							wave.Id, rejection);
						return;
					}

					var replacement = FindDropPlan(loadedPairs[0].Transport, loadedPairs[0].Passenger);
					if (replacement == null)
					{
						ReturnWaveToAssembly(bot, loadedPairs,
							"destination became defended and no safe replacement exists");
						return;
					}

					var destinations = FindDistinctDestinations(replacement.LandingCenter,
						loadedPairs.Select(p => p.Passenger).ToList());
					if (destinations.Count < loadedPairs.Count)
						return;

					wave.Target = replacement.Target;
					wave.LandingCenter = replacement.LandingCenter;
					for (var i = 0; i < loadedPairs.Count; i++)
						loadedPairs[i].Destination = destinations[i];

					if (!IssueWaveTravel(bot, "destination replanned"))
						ReturnWaveToAssembly(bot, loadedPairs, "replacement lacks complete safe unload plans");

					return;
				}
			}

			var committing = loadedPairs.Any(pair => pair.UnloadPlan != null &&
				(pair.Transport.Location - pair.UnloadPlan.CarrierCell).LengthSquared <=
				info.HeavyDropUnloadRangeCells * info.HeavyDropUnloadRangeCells);
			if (committing && !RevalidateWavePlans(loadedPairs, out var commitRejection))
			{
				if (TryPlanWave(loadedPairs, out commitRejection))
					IssueWaveRoutes(bot, loadedPairs, "plans changed before descent");
				else if (!wave.ReturningToAssembly)
					ReturnWaveToAssembly(bot, loadedPairs, "no complete safe plan before descent");

				return;
			}

			foreach (var pair in loadedPairs)
			{
				if (pair.UnloadPlan != null &&
					(pair.Transport.Location - pair.UnloadPlan.CarrierCell).LengthSquared <=
					info.HeavyDropUnloadRangeCells * info.HeavyDropUnloadRangeCells)
				{
					pair.LastOrderTick = world.WorldTick;
					bot.QueueOrder(TransportUnloadOrder.Create(world, pair.Transport, pair.UnloadPlan));
					Debug("wave {0} carrier {1} committing exact unload: carrierCell={2}, exit={3}, " +
						"passenger={4}, revision={5}, snapshot={6}, outcome={7}", wave.Id, pair.Transport,
						pair.UnloadPlan.CarrierCell, pair.UnloadPlan.ExitCells[0], pair.Passenger,
						pair.UnloadPlan.Revision, pair.UnloadPlan.SnapshotTick,
						wave.ReturningToAssembly ? "safe-return" : "heavy-assault");
					continue;
				}

				if (IsCarrierIdle(pair.Transport) && ReadyToRetry(pair))
				{
					IssuePairRoute(bot, pair);
					pair.LastOrderTick = world.WorldTick;
				}
			}

			if (wave.Pairs.Where(IsPairUsable).All(p => !IsLoaded(p)))
				wave.Stage = WaveStage.Unloading;
		}

		void DebugTravelFailure(string reason)
		{
			Debug("wave {0} travel failure: {1}, center={2}, target={3}#{4}", wave.Id, reason,
				wave.LandingCenter, wave.Target?.Info.Name ?? "none", wave.Target?.ActorID ?? 0);
			foreach (var pair in wave.Pairs.Where(p => IsTransportUsable(p.Transport) && IsLoaded(p))
				.OrderBy(p => p.Transport.ActorID))
				Debug("wave {0} carrier status: carrier={1} at {2}, destination={3}, distance2={4}, idle={5}, activity={6}",
					wave.Id, pair.Transport, pair.Transport.Location, pair.Destination,
					(pair.Transport.Location - pair.Destination).LengthSquared, IsCarrierIdle(pair.Transport),
					pair.Transport.CurrentActivity?.GetType().Name ?? "none");
		}

		void AdvanceUnloading(IBot bot)
		{
			var carrying = wave.Pairs.Where(p => IsTransportUsable(p.Transport) &&
				p.Transport.TraitOrDefault<Cargo>()?.IsEmpty() == false).ToList();
			if (carrying.Count == 0)
			{
				FinishWave(bot, wave.ReturningToAssembly ? "safe assembly return complete" :
					"ground-force handoff complete");
				return;
			}

			if (!RevalidateWavePlans(carrying, out var rejection))
			{
				if (!TryPlanWave(carrying, out rejection))
				{
					if (!wave.ReturningToAssembly)
						ReturnWaveToAssembly(bot, carrying, "remaining unload plans invalidated");

					return;
				}

				wave.Stage = WaveStage.Travelling;
				IssueWaveRoutes(bot, carrying, "remaining unload plans refreshed");
				return;
			}

			foreach (var pair in carrying.Where(ReadyToRetry))
			{
				bot.QueueOrder(TransportUnloadOrder.Create(world, pair.Transport, pair.UnloadPlan));
				pair.LastOrderTick = world.WorldTick;
			}
		}

		void ReturnWaveToAssembly(IBot bot, List<Pair> loadedPairs, string reason)
		{
			wave.ReturningToAssembly = true;
			wave.Stage = WaveStage.Travelling;
			wave.TimeoutOriginTick = world.WorldTick;
			foreach (var pair in loadedPairs)
				pair.Destination = pair.PickupDestination;

			if (!IssueWaveTravel(bot, $"{reason}; returning to safe assembly cells") &&
				!IssueWaveCurrentFallback(bot, loadedPairs, reason))
			{
				foreach (var pair in loadedPairs)
					bot.QueueOrder(new Order("Stop", pair.Transport, false));

				Debug("wave {0} holding outside known danger: no safe assembly plan", wave.Id);
			}
		}

		bool IssueWaveCurrentFallback(IBot bot, List<Pair> pairs, string reason)
		{
			var unavailable = new HashSet<CPos>();
			var replacements = new List<KeyValuePair<Pair, TransportUnloadPlan>>();
			foreach (var pair in pairs.OrderBy(p => p.Transport.ActorID))
			{
				var revision = (pair.UnloadPlan?.Revision ?? 0) + 1;
				var fallbackCenter = world.Map.Clamp(pair.PickupDestination +
					(pair.PickupDestination - pair.Transport.Location).Sign() *
					info.SafeReturnLandingSearchRadiusCells);
				if (!unloadPlanner.TryPlanWithoutClaim(wave.Id, pair.Transport, new[] { pair.Passenger },
					fallbackCenter, pair.PickupDestination, info.SafeReturnLandingSearchRadiusCells,
					info.SafeReturnUsefulnessRadiusCells, revision, unavailable, out var plan, out _))
					return false;

				replacements.Add(new KeyValuePair<Pair, TransportUnloadPlan>(pair, plan));
				unavailable.Add(plan.CarrierCell);
				foreach (var exit in plan.ExitCells)
					unavailable.Add(exit);
			}

			if (!unloadPlanner.TryClaimPlans(wave.Id, replacements.Select(r => r.Value), out _))
				return false;

			foreach (var replacement in replacements)
				replacement.Key.UnloadPlan = replacement.Value;

			IssueWaveRoutes(bot, pairs, reason + "; current-position safe fallback");
			wave.SafeReturnFallback = true;
			Debug("wave {0} terminal safe fallback routed {1} loaded carriers from current positions",
				wave.Id, pairs.Count);
			return true;
		}

		bool IssueWaveTravel(IBot bot, string reason)
		{
			var pairs = wave.Pairs.Where(p => IsPairUsable(p) && IsLoaded(p)).ToList();
			if (!TryPlanWave(pairs, out var rejection))
			{
				Debug("wave {0} cannot travel: no complete unload-plan set; reason={1}", wave.Id, rejection);
				return false;
			}

			IssueWaveRoutes(bot, pairs, reason);
			wave.SafeReturnFallback = false;

			Debug("wave {0} travelling with {1} carriers to {2}: {3}",
				wave.Id, pairs.Count, wave.LandingCenter, reason);
			return true;
		}

		bool TryPlanWave(List<Pair> pairs, out string rejection)
		{
			var unavailable = new HashSet<CPos>();
			var replacements = new List<KeyValuePair<Pair, TransportUnloadPlan>>();
			foreach (var pair in pairs.OrderBy(p => p.Transport.ActorID))
			{
				var revision = (pair.UnloadPlan?.Revision ?? 0) + 1;
				var handoffObjective = wave.ReturningToAssembly || wave.Target == null ?
					pair.Destination : wave.Target.Location;
				if (!unloadPlanner.TryPlanWithoutClaim(wave.Id, pair.Transport, new[] { pair.Passenger },
					pair.Destination, handoffObjective, info.HeavyDropLandingSearchRadiusCells,
					info.HeavyDropLandingUsefulnessRadiusCells, revision, unavailable,
					out var plan, out rejection))
					return false;

				replacements.Add(new KeyValuePair<Pair, TransportUnloadPlan>(pair, plan));
				unavailable.Add(plan.CarrierCell);
				foreach (var exit in plan.ExitCells)
					unavailable.Add(exit);
			}

			if (!unloadPlanner.TryClaimPlans(wave.Id, replacements.Select(r => r.Value), out rejection))
				return false;

			foreach (var replacement in replacements)
			{
				replacement.Key.UnloadPlan = replacement.Value;
				Debug("wave {0} selected unload plan: carrier={1}, passenger={2}, objective={3}, " +
					"carrierCell={4}, exit={5}, revision={6}, snapshot={7}, candidates={8}, " +
					"firstThreatRejection={9}", wave.Id, replacement.Key.Transport,
					replacement.Key.Passenger, replacement.Key.Destination, replacement.Value.CarrierCell,
					replacement.Value.ExitCells[0], replacement.Value.Revision, replacement.Value.SnapshotTick,
					replacement.Value.CandidatesEvaluated,
					replacement.Value.FirstThreatRejection ?? "none");
			}

			rejection = null;
			return true;
		}

		bool RevalidateWavePlans(List<Pair> pairs, out string rejection)
		{
			var unavailable = new HashSet<CPos>();
			var plans = new List<TransportUnloadPlan>();
			foreach (var pair in pairs.OrderBy(p => p.Transport.ActorID))
			{
				if (!unloadPlanner.RevalidateWithoutClaim(wave.Id, pair.Transport, new[] { pair.Passenger },
					pair.UnloadPlan, info.HeavyDropLandingUsefulnessRadiusCells, unavailable, out rejection))
					return false;

				plans.Add(pair.UnloadPlan);
				unavailable.Add(pair.UnloadPlan.CarrierCell);
				foreach (var exit in pair.UnloadPlan.ExitCells)
					unavailable.Add(exit);
			}

			return unloadPlanner.TryClaimPlans(wave.Id, plans, out rejection);
		}

		void IssueWaveRoutes(IBot bot, List<Pair> pairs, string reason)
		{
			foreach (var pair in pairs.OrderBy(p => p.Transport.ActorID))
			{
				if (!IssuePairRoute(bot, pair))
				{
					bot.QueueOrder(new Order("Stop", pair.Transport, false));
					Debug("wave {0} route failed without unsafe direct append: carrier={1}, carrierCell={2}",
						wave.Id, pair.Transport, pair.UnloadPlan?.CarrierCell);
				}

				pair.LastOrderTick = world.WorldTick;
			}

			Debug("wave {0} issued {1} threat-aware plan routes: {2}", wave.Id, pairs.Count, reason);
		}

		bool IssuePairRoute(IBot bot, Pair pair)
		{
			var route = unloadPlanner.Route(pair.Transport, pair.UnloadPlan);
			if (route == null || (route.Count == 0 && pair.Transport.Location != pair.UnloadPlan.CarrierCell))
				return false;

			var queued = false;
			foreach (var waypoint in route)
			{
				bot.QueueOrder(new Order("Move", pair.Transport, Target.FromCell(world, waypoint), queued));
				queued = true;
			}

			Debug("wave {0} routed carrier={1} to carrierCell={2}: waypoints={3}, snapshot={4}",
				wave.Id, pair.Transport, pair.UnloadPlan.CarrierCell, route.Count, pair.UnloadPlan.SnapshotTick);
			return true;
		}

		void IssueRoutedLanding(Actor transport, CPos destination)
		{
			issueRoutedAirMove(transport, destination,
				new Order("Land", transport, Target.FromCell(world, destination), true));
		}

		void BeginAbortUnload(IBot bot, string reason)
		{
			wave.Aborted = true;
			wave.ReturningToAssembly = true;
			wave.Stage = WaveStage.Travelling;
			wave.TimeoutOriginTick = world.WorldTick;
			var loadedPairs = wave.Pairs.Where(p => IsPairUsable(p) && IsLoaded(p)).ToList();
			foreach (var pair in wave.Pairs.Where(p => IsPairUsable(p) && !IsLoaded(p)))
			{
				bot.QueueOrder(new Order("Stop", pair.Passenger, false));
				bot.QueueOrder(new Order("Stop", pair.Transport, false));
				pair.LastOrderTick = world.WorldTick;
			}

			if (loadedPairs.Count == 0)
			{
				Debug("wave {0} aborted without loaded cargo: {1}", wave.Id, reason);
				FinishWave(bot, "abort restored unloaded pairs");
				return;
			}

			foreach (var pair in loadedPairs)
				pair.Destination = pair.PickupDestination;

			if (!IssueWaveTravel(bot, $"abort: {reason}; returning for planned safe unload"))
			{
				foreach (var pair in loadedPairs)
					bot.QueueOrder(new Order("Stop", pair.Transport, false));

				Debug("wave {0} aborted and holding loaded carriers: no safe assembly unload plans", wave.Id);
			}
		}

		DropPlan FindDropPlan(Actor carrier, Actor passenger)
		{
			var manager = squadManager();
			if (manager == null)
				return null;

			var origin = carrier.Location;

			var targets = world.Actors.Where(a => IsActorUsable(a) &&
				player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy &&
				info.HeavyDropTargetTypes.Contains(a.Info.Name))
				.OrderByDescending(EconomicValue).ThenBy(a => (a.Location - origin).LengthSquared)
				.ThenBy(a => a.ActorID).Take(info.HeavyDropTargetCandidateLimit);
			DropPlan best = null;
			string firstThreatRejection = null;
			foreach (var target in targets)
			{
				var away = (target.Location - origin).Sign();
				foreach (var direction in CVec.Directions.OrderByDescending(d => CVec.Dot(d, away))
					.ThenBy(d => d.X).ThenBy(d => d.Y))
				{
					var intended = world.Map.Clamp(target.Location + direction * info.HeavyDropLandingRadius);
					var landing = MoveableCellNear(passenger, intended);
					if (!unloadPlanner.TryPlanWithoutClaim(0, carrier, new[] { passenger }, landing,
						target.Location, info.HeavyDropLandingSearchRadiusCells,
						info.HeavyDropLandingUsefulnessRadiusCells, 1, Array.Empty<CPos>(),
						out var exactPlan, out var rejection))
					{
						if (firstThreatRejection == null && rejection.Contains(" weapon "))
							firstThreatRejection = rejection;

						continue;
					}

					firstThreatRejection = firstThreatRejection ?? exactPlan.FirstThreatRejection;

					var defenderValue = DefenderValueAt(exactPlan.CarrierCell);
					if (!HeavyDropPolicy.IsDropSiteSafe(0, info.HeavyDropMaximumAaDanger,
						defenderValue, info.HeavyDropMaximumDefenderValue))
						continue;

					var distance = Math.Abs(exactPlan.CarrierCell.X - origin.X) +
						Math.Abs(exactPlan.CarrierCell.Y - origin.Y);
					var score = HeavyDropPolicy.TargetScore(EconomicValue(target), defenderValue, distance,
						CVec.Dot(direction, away));
					if (best == null || score > best.Score ||
						(score == best.Score && target.ActorID < best.Target.ActorID))
						best = new DropPlan(target, exactPlan.CarrierCell, 0, defenderValue, score,
							firstThreatRejection);
				}
			}

			return best == null ? null : new DropPlan(best.Target, best.LandingCenter, best.AaDanger,
				best.DefenderValue, best.Score, firstThreatRejection);
		}

		List<CPos> FindDistinctDestinations(CPos center, List<Actor> passengers)
		{
			var candidates = world.Map.FindTilesInCircle(center, info.HeavyDropFormationRadius)
				.OrderBy(c => (c - center).LengthSquared).ThenBy(c => c.X).ThenBy(c => c.Y).ToList();
			var used = new HashSet<CPos>();
			var result = new List<CPos>();
			foreach (var passenger in passengers)
			{
				var mobile = passenger.TraitOrDefault<Mobile>();
				var destination = candidates.Cast<CPos?>().FirstOrDefault(c => c.HasValue &&
					used.All(u => (u - c.Value).LengthSquared >=
						info.HeavyDropFormationSpacing * info.HeavyDropFormationSpacing) &&
					mobile != null && HasUnloadSpace(mobile, c.Value));
				if (!destination.HasValue)
					continue;

				used.Add(destination.Value);
				result.Add(destination.Value);
			}

			return result;
		}

		bool HasUnloadSpace(Mobile passenger, CPos carrierCell)
		{
			return world.Map.FindTilesInCircle(carrierCell, 1).Any(c => c != carrierCell &&
				passenger.CanEnterCell(c, check: BlockedByActor.Immovable));
		}

		List<CPos> FindDistinctPickupDestinations(List<Actor> passengers, List<Actor> transports)
		{
			var used = new HashSet<CPos>();
			var spacingSquared = info.HeavyDropFormationSpacing * info.HeavyDropFormationSpacing;
			var result = new List<CPos>();
			for (var i = 0; i < passengers.Count; i++)
			{
				var passenger = passengers[i];
				var mobile = passenger.TraitOrDefault<Mobile>();
				var aircraft = transports[i].TraitOrDefault<Aircraft>();
				var pickup = world.Map.FindTilesInCircle(passenger.Location, info.HeavyDropFormationRadius)
					.OrderBy(c => (c - passenger.Location).LengthSquared).ThenBy(c => c.X).ThenBy(c => c.Y)
					.Cast<CPos?>().FirstOrDefault(c => c.HasValue && c.Value != passenger.Location &&
						used.All(u => (u - c.Value).LengthSquared >= spacingSquared) &&
						mobile != null && aircraft != null && aircraft.CanLand(c.Value) &&
						HasReachableBoardingApproach(passenger, mobile, c.Value));
				if (!pickup.HasValue)
					continue;

				used.Add(pickup.Value);
				result.Add(pickup.Value);
			}

			return result;
		}

		bool HasReachableBoardingApproach(Actor passenger, Mobile mobile, CPos pickup)
		{
			foreach (var approach in world.Map.FindTilesInCircle(pickup, 1).Where(c => c != pickup))
			{
				if (!mobile.CanEnterCell(approach, check: BlockedByActor.Immovable))
					continue;

				if (approach == passenger.Location || mobile.Pathfinder.FindUnitPath(passenger.Location,
					approach, passenger, null, BlockedByActor.Immovable).Count > 0)
					return true;
			}

			return false;
		}

		CPos MoveableCellNear(Actor passenger, CPos intended)
		{
			var mobile = passenger.TraitOrDefault<Mobile>();
			if (mobile == null)
				return intended;

			return mobile.CanEnterCell(intended, check: BlockedByActor.Immovable) ? intended :
				mobile.NearestMoveableCell(intended, 1, 6);
		}

		int DefenderValueAt(CPos cell)
		{
			var radiusSquared = info.HeavyDropDefenseRadius * info.HeavyDropDefenseRadius;
			return world.Actors.Where(a => IsActorUsable(a) &&
				player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy &&
				a.Info.HasTraitInfo<AttackBaseInfo>() && (a.Location - cell).LengthSquared <= radiusSquared)
				.Sum(EconomicValue);
		}

		IEnumerable<Actor> AvailablePassengers()
		{
			return world.Actors.Where(a => IsActorUsable(a) && a.Owner == player &&
				info.HeavyDropPassengerTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<PassengerInfo>() &&
				a.TraitOrDefault<Passenger>()?.Transport == null && !coordinator.IsReserved(a.ActorID) &&
				!isReservedForOtherBehavior(a))
				.OrderBy(a => a.ActorID);
		}

		IEnumerable<Actor> AvailableTransports(CPos origin, int passengerWeight)
		{
			return world.Actors.Where(a => IsTransportUsable(a) && a.Owner == player &&
				info.TransportHelicopterTypes.Contains(a.Info.Name) && !coordinator.IsReserved(a.ActorID) &&
				!isReservedForOtherBehavior(a) &&
				a.TraitOrDefault<Cargo>()?.IsEmpty() == true && a.Trait<Cargo>().HasSpace(passengerWeight) && !NeedsRepair(a))
				.OrderBy(a => (a.Location - origin).LengthSquared).ThenBy(a => a.ActorID);
		}

		static List<Actor> PairNearestTransports(List<Actor> passengers, List<Actor> transports)
		{
			var remaining = new List<Actor>(transports);
			var paired = new List<Actor>();
			foreach (var passenger in passengers)
			{
				var transport = remaining.OrderBy(a => (a.Location - passenger.Location).LengthSquared)
					.ThenBy(a => a.ActorID).First();
				paired.Add(transport);
				remaining.Remove(transport);
			}

			return paired;
		}

		bool IsPairUsable(Pair pair)
		{
			return IsTransportUsable(pair.Transport) && pair.Passenger != null && !pair.Passenger.IsDead &&
				(pair.Passenger.IsInWorld || pair.Passenger.TraitOrDefault<Passenger>()?.Transport == pair.Transport);
		}

		static bool IsLoaded(Pair pair)
		{
			return pair.Transport.TraitOrDefault<Cargo>()?.Passengers.Any(a => a == pair.Passenger) == true;
		}

		static bool IsBoarding(Pair pair)
		{
			return pair.PickupOrdered || pair.BoardingOrdered ||
				(pair.Passenger.TraitOrDefault<Passenger>()?.ReservedCargo == pair.Transport.TraitOrDefault<Cargo>());
		}

		bool BoardingOrderExpired(Pair pair)
		{
			return pair.BoardingOrdered && !IsCargoReserved(pair) && pair.Passenger.IsIdle &&
				world.WorldTick - pair.LastOrderTick >= info.HeavyDropBoardingRetryTicks;
		}

		bool PickupOrderExpired(Pair pair)
		{
			return pair.PickupOrdered && !pair.BoardingOrdered && !PickupReady(pair) &&
				IsCarrierIdle(pair.Transport) && world.WorldTick - pair.LastOrderTick >= info.HeavyDropBoardingRetryTicks;
		}

		bool PickupReady(Pair pair)
		{
			var mobile = pair.Passenger.TraitOrDefault<Mobile>();
			var aircraft = pair.Transport.TraitOrDefault<Aircraft>();
			return mobile != null && HeavyDropPolicy.CanBoardAtPickup(
				pair.Transport.Location == pair.PickupDestination,
				aircraft?.AtLandAltitude == true,
				mobile.CanEnterCell(pair.Transport.Location, pair.Transport, BlockedByActor.Immovable));
		}

		static bool IsCargoReserved(Pair pair)
		{
			return pair.Passenger.TraitOrDefault<Passenger>()?.ReservedCargo ==
				pair.Transport.TraitOrDefault<Cargo>();
		}

		bool NeedsRepair(Actor actor)
		{
			var health = actor.TraitOrDefault<IHealth>();
			return health != null && health.HP * 100L < health.MaxHP * info.RepairHealthPercent;
		}

		bool ReadyToRetry(Pair pair)
		{
			return world.WorldTick - pair.LastOrderTick >= info.AssaultOrderRetryTicks;
		}

		static bool IsActorUsable(Actor actor)
		{
			return actor != null && !actor.IsDead && actor.IsInWorld;
		}

		static bool IsTransportUsable(Actor actor)
		{
			return IsActorUsable(actor) && actor.TraitOrDefault<Cargo>() != null;
		}

		static bool IsCarrierIdle(Actor actor)
		{
			return actor.IsIdle || actor.CurrentActivity == null || actor.CurrentActivity is FlyIdle;
		}

		static int EconomicValue(Actor actor)
		{
			return Math.Max(0, actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0);
		}

		void FinishWave(IBot bot, string reason)
		{
			var completedWave = wave;
			var passengers = completedWave.Pairs.Select(p => p.Passenger).ToList();
			if (completedWave.SafeReturnFallback)
				foreach (var pair in completedWave.Pairs.Where(p => IsTransportUsable(p.Transport) &&
					p.UnloadPlan != null))
					rememberSafeIdleStaging(pair.Transport, pair.UnloadPlan.CarrierCell);

			coordinator.Release(completedWave.Id);

			var manager = squadManager();
			if (!completedWave.Aborted && !completedWave.ReturningToAssembly)
			{
				var adopted = manager?.AdoptTransportedAssault(bot, passengers, completedWave.Target) ?? 0;
				Debug("released wave {0}: {1}; adopted={2}/{3}, target={4}#{5}", completedWave.Id,
					reason, adopted, passengers.Count, completedWave.Target?.Info.Name ?? "none",
					completedWave.Target?.ActorID ?? 0);
			}
			else
			{
				var restored = manager?.RestoreTransportedUnits(passengers) ?? 0;
				Debug("released wave {0}: {1}; restored={2}/{3} to ordinary squads", completedWave.Id,
					reason, restored, passengers.Count);
			}

			wave = null;
		}

		void Debug(string format, params object[] args)
		{
			if (info.DebugLogging)
				Log.Write("debug", "AI heavy drop [{0}]: {1}", player.InternalName, string.Format(format, args));
		}

		void DebugBlocked(string format, params object[] args)
		{
			if (!info.DebugLogging)
				return;

			var message = string.Format(format, args);
			var interval = info.ScanInterval * info.BlockedDiagnosticIntervalScans;
			if (message == lastBlockedDiagnostic && lastBlockedDiagnosticTick >= 0 &&
				world.WorldTick - lastBlockedDiagnosticTick < interval)
				return;

			lastBlockedDiagnostic = message;
			lastBlockedDiagnosticTick = world.WorldTick;
			Log.Write("debug", "AI heavy drop [{0}]: {1}", player.InternalName, message);
		}
	}
}
