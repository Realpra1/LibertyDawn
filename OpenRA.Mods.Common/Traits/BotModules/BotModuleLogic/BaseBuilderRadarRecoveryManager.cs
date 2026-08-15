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
using OpenRA.Mods.Common.Traits.Radar;

namespace OpenRA.Mods.Common.Traits
{
	public static class RadarRecoveryPolicy
	{
		public enum ProviderTransition
		{
			None,
			Established,
			Lost,
			Restored,
			BecameOperational,
			BecameUnavailable
		}

		public static bool NeedsRecovery(bool configured, bool everEstablished,
			bool hasLiveProvider, bool hasCommitment)
		{
			return configured && everEstablished && !hasLiveProvider && !hasCommitment;
		}

		public static bool RecordProviderEstablishment(bool everEstablished, bool ownedProviderAdded)
		{
			return everEstablished || ownedProviderAdded;
		}

		public static bool ReservationExpired(int reservedTick, int currentTick, int timeout)
		{
			return currentTick - reservedTick >= Math.Max(1, timeout);
		}

		public static bool ReservationMatchesQueue(uint reservedQueueActorId, string reservedQueueType,
			uint queueActorId, string queueType)
		{
			return reservedQueueActorId == queueActorId &&
				string.Equals(reservedQueueType, queueType, StringComparison.Ordinal);
		}

		public static bool ReservationMustRelease(bool validQueue, bool commitmentWasObserved,
			bool hasCommittedRecovery, bool reservationExpired)
		{
			return !validQueue || (commitmentWasObserved && !hasCommittedRecovery) ||
				(!hasCommittedRecovery && reservationExpired);
		}

		public static bool RestoreCommitmentObservation(bool savedObservation, bool hasCommittedRecovery)
		{
			return savedObservation || hasCommittedRecovery;
		}

		public static bool HasActionableStoragePressure(int storedResources, int resourceCapacity)
		{
			return resourceCapacity > 0 && (long)Math.Max(0, storedResources) * 5 > (long)resourceCapacity * 4;
		}

		public static bool StorageCommitmentBlocksRadar(bool hasReservation,
			bool hasQueuedCommitment, bool orderIssuedThisTick)
		{
			return hasReservation && !hasQueuedCommitment && !orderIssuedThisTick;
		}

		public static bool ShouldRefreshObservation(bool initialized, int lastScanTick,
			int nextPeriodicScanTick, int currentTick, bool queueChoiceRequiresFreshState)
		{
			return !initialized || currentTick >= nextPeriodicScanTick ||
				(queueChoiceRequiresFreshState && lastScanTick != currentTick);
		}

		public static ProviderTransition ObserveProviderTransition(bool initialized, bool everEstablished,
			bool wasLive, bool wasOperational, bool isLive, bool isOperational)
		{
			if (!initialized)
				return isLive ? ProviderTransition.Established : ProviderTransition.None;

			if (wasLive != isLive)
				return isLive ? (everEstablished ? ProviderTransition.Restored : ProviderTransition.Established) :
					ProviderTransition.Lost;

			if (isLive && wasOperational != isOperational)
				return isOperational ? ProviderTransition.BecameOperational : ProviderTransition.BecameUnavailable;

			return ProviderTransition.None;
		}
	}

	sealed class BaseBuilderRadarRecoveryManager
	{
		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		readonly PowerManager playerPower;
		readonly PlayerResources playerResources;
		bool everEstablished;
		bool observationInitialized;
		bool hasLiveProvider;
		bool hasOperationalProvider;
		int committedRecoveryCount;
		int lastScanTick = -1;
		int nextScanTick;
		readonly Dictionary<uint, string> lastQueueBlockers = new Dictionary<uint, string>();
		uint[] previousProviderActorIds = Array.Empty<uint>();
		HashSet<uint> downstreamActorIdsAtLoss;
		bool downstreamCompletionLogged;
		uint reservationQueueActorId;
		string reservationQueueType = "";
		string reservationActorType = "";
		int reservationTick;
		bool reservationCommitmentObserved;

		public BaseBuilderRadarRecoveryManager(BaseBuilderBotModule baseBuilder, Player player)
		{
			this.baseBuilder = baseBuilder;
			this.player = player;
			world = player.World;
			playerPower = player.PlayerActor.TraitOrDefault<PowerManager>();
			playerResources = player.PlayerActor.TraitOrDefault<PlayerResources>();
			world.ActorAdded += ActorAdded;
		}

		void ActorAdded(Actor actor)
		{
			// ActorAdded is the bounded lifecycle event that closes the gap between
			// periodic observations. ActorInfo is available even when trait instance
			// initialization has not completed yet.
			var ownedProviderAdded = actor.Owner == player &&
				actor.Info.TraitInfoOrDefault<ProvidesRadarInfo>() != null;
			everEstablished = RadarRecoveryPolicy.RecordProviderEstablishment(
				everEstablished, ownedProviderAdded);
		}

		public bool EverEstablished => everEstablished;
		public uint ReservationQueueActorId => reservationQueueActorId;
		public string ReservationQueueType => reservationQueueType;
		public string ReservationActorType => reservationActorType;
		public int ReservationTick => reservationTick;
		public bool ReservationCommitmentObserved => reservationCommitmentObserved;

		public bool NeedsRecovery
		{
			get
			{
				Update(true);
				return RadarRecoveryPolicy.NeedsRecovery(baseBuilder.Info.RadarRecoveryTypes.Length > 0,
					everEstablished, HasLiveProvider, HasCommittedRecovery || HasReservation);
			}
		}

		bool HasReservation => reservationQueueActorId != 0;

		bool HasLiveProvider => hasLiveProvider;

		bool HasCommittedRecovery => committedRecoveryCount > 0;

		public void Update(bool queueChoiceRequiresFreshState = false)
		{
			if (!RadarRecoveryPolicy.ShouldRefreshObservation(observationInitialized, lastScanTick,
				nextScanTick, world.WorldTick, queueChoiceRequiresFreshState))
				return;

			// ProvidesRadar is a sparse indexed trait set. Restricting observation to it avoids
			// a full-world scan whenever an idle queue needs current capability state.
			var providers = world.ActorsWithTrait<ProvidesRadar>()
				.Where(p => p.Actor.Owner == player && !p.Actor.IsDead && p.Actor.IsInWorld)
				.OrderBy(p => p.Actor.ActorID).ToArray();
			var providerIds = providers.Select(p => p.Actor.ActorID).Distinct().ToArray();
			var liveProvider = providers.Length > 0;
			var operationalProvider = providers.Any(p => !p.Trait.IsTraitDisabled);
			var committed = baseBuilder.CountQueuedOrPendingActors(baseBuilder.Info.RadarRecoveryTypes);
			lastScanTick = world.WorldTick;
			nextScanTick = world.WorldTick + Math.Max(1, baseBuilder.Info.RadarRecoveryScanInterval);

			var wasLiveProvider = hasLiveProvider;
			var wasOperationalProvider = hasOperationalProvider;
			var providerTransition = RadarRecoveryPolicy.ObserveProviderTransition(observationInitialized,
				everEstablished, wasLiveProvider, wasOperationalProvider, liveProvider, operationalProvider);
			if (providerTransition == RadarRecoveryPolicy.ProviderTransition.Lost)
			{
				LogTransition("{0} lost radar capability at tick {1}: reason={2}, previous={3}, " +
					"power={4}/{5} state={6}, refineries={7}, commitments={8}",
					player, world.WorldTick, LossReason(previousProviderActorIds),
					ProviderSummary(previousProviderActorIds), playerPower?.PowerProvided ?? 0,
					playerPower?.PowerDrained ?? 0, playerPower?.PowerState.ToString() ?? "unmanaged",
					baseBuilder.CountActors(baseBuilder.SmartEconomyRefineryTypes), committed);
				if (DebugLoggingEnabled)
				{
					downstreamActorIdsAtLoss = LiveHeadquartersDependentActors().Select(a => a.ActorID).ToHashSet();
					downstreamCompletionLogged = false;
				}
			}

			hasLiveProvider = liveProvider;
			hasOperationalProvider = operationalProvider;
			committedRecoveryCount = committed;
			previousProviderActorIds = providerIds;
			observationInitialized = true;

			if (hasLiveProvider)
			{
				lastQueueBlockers.Clear();
				if (providerTransition == RadarRecoveryPolicy.ProviderTransition.Established)
					LogTransition("{0} established its first live radar provider at tick {1}: providers={2}, operational={3}",
						player, world.WorldTick, ProviderSummary(providerIds), hasOperationalProvider);
				else if (providerTransition == RadarRecoveryPolicy.ProviderTransition.Restored)
					LogTransition("{0} restored radar capability at tick {1}: providers={2}, operational={3}, " +
						"power={4}/{5} state={6}", player, world.WorldTick, ProviderSummary(providerIds),
						hasOperationalProvider, playerPower?.PowerProvided ?? 0, playerPower?.PowerDrained ?? 0,
						playerPower?.PowerState.ToString() ?? "unmanaged");
				else if (providerTransition == RadarRecoveryPolicy.ProviderTransition.BecameOperational ||
					providerTransition == RadarRecoveryPolicy.ProviderTransition.BecameUnavailable)
					LogTransition("{0} radar provider operational state changed at tick {1}: providers={2}, " +
						"operational={3}, power={4}/{5} state={6}", player, world.WorldTick,
						ProviderSummary(providerIds), hasOperationalProvider, playerPower?.PowerProvided ?? 0,
						playerPower?.PowerDrained ?? 0, playerPower?.PowerState.ToString() ?? "unmanaged");

				everEstablished = true;
				ReleaseReservation("live radar provider satisfies recovery");
				LogDownstreamCompletion();
				return;
			}

			if (!HasReservation)
				return;

			var queueActor = world.GetActorById(reservationQueueActorId);
			var validQueue = queueActor != null && queueActor.Owner == player && !queueActor.IsDead &&
				queueActor.IsInWorld && queueActor.TraitsImplementing<ProductionQueue>().Any(q =>
					RadarRecoveryPolicy.ReservationMatchesQueue(reservationQueueActorId, reservationQueueType,
						q.Actor.ActorID, q.Info.Type));
			var reservationExpired = RadarRecoveryPolicy.ReservationExpired(reservationTick, world.WorldTick,
				baseBuilder.Info.RadarRecoveryReservationTimeout);
			if (RadarRecoveryPolicy.ReservationMustRelease(validQueue, reservationCommitmentObserved,
				HasCommittedRecovery, reservationExpired))
			{
				var reason = !validQueue ? "reserved queue was lost" : reservationCommitmentObserved ?
					"production commitment disappeared before completion" : "reservation timed out before production";
				ReleaseReservation(reason);
				return;
			}

			if (HasCommittedRecovery && !reservationCommitmentObserved)
			{
				reservationCommitmentObserved = true;
				LogTransition("{0} radar recovery {1} entered production on queue {2}/{3} at tick {4}: commitments={5}",
					player, reservationActorType, reservationQueueActorId, reservationQueueType,
					world.WorldTick, committedRecoveryCount);
			}
		}

		public void ObserveQueueChoice(ProductionQueue queue, IEnumerable<ActorInfo> buildables,
			bool essentialPowerBlocked, bool essentialRefineryBlocked)
		{
			Update(true);
			if (!RadarRecoveryPolicy.NeedsRecovery(baseBuilder.Info.RadarRecoveryTypes.Length > 0,
				everEstablished, HasLiveProvider, HasCommittedRecovery || HasReservation))
			{
				lastQueueBlockers.Clear();
				return;
			}

			if (!DebugLoggingEnabled)
				return;

			var candidate = FindCandidate(buildables);
			var funds = (playerResources?.Cash ?? 0) + (playerResources?.Resources ?? 0);
			var cost = candidate?.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;
			var blocker = essentialPowerBlocked ? "essential-power" :
				essentialRefineryBlocked ? "essential-refinery" :
				candidate == null ? "prerequisite-or-building-limit" :
				funds < cost ? "funds" : "eligible-idle-queue";

			if (!QueueBlockerChanged(queue, blocker))
				return;

			LogTransition("{0} radar recovery queue state at tick {1}: blocker={2}, queue={3}, " +
				"power={4}/{5} state={6}, refineries={7}, funds={8}, candidate={9}, cost={10}",
				player, world.WorldTick, blocker, queue.Actor.ActorID,
				playerPower?.PowerProvided ?? 0, playerPower?.PowerDrained ?? 0,
				playerPower?.PowerState.ToString() ?? "unmanaged",
				baseBuilder.CountActors(baseBuilder.SmartEconomyRefineryTypes), funds,
				candidate?.Name ?? "none", cost);
		}

		public void ObserveBusyQueue(ProductionQueue queue, ProductionItem currentBuilding)
		{
			if (currentBuilding == null)
				return;

			Update(true);
			if (!RadarRecoveryPolicy.NeedsRecovery(baseBuilder.Info.RadarRecoveryTypes.Length > 0,
				everEstablished, HasLiveProvider, HasCommittedRecovery || HasReservation))
				return;

			var blocker = $"busy-queue:{currentBuilding.Item}";
			if (!DebugLoggingEnabled || !QueueBlockerChanged(queue, blocker))
				return;

			LogTransition("{0} radar recovery queue state at tick {1}: blocker={2}, queue={3}",
				player, world.WorldTick, blocker, queue.Actor.ActorID);
		}

		public void PlacementFailed(ProductionQueue queue, string actorType)
		{
			if (!baseBuilder.Info.RadarRecoveryTypes.Contains(actorType))
				return;

			const string Blocker = "no-legal-placement";
			if (!QueueBlockerChanged(queue, Blocker))
				return;

			LogTransition("{0} radar recovery placement failed at tick {1}: blocker={2}, queue={3}, actor={4}",
				player, world.WorldTick, Blocker, queue.Actor.ActorID, actorType);
		}

		public ActorInfo Candidate(IEnumerable<ActorInfo> buildables)
		{
			Update(true);
			if (!RadarRecoveryPolicy.NeedsRecovery(baseBuilder.Info.RadarRecoveryTypes.Length > 0,
				everEstablished, HasLiveProvider, HasCommittedRecovery || HasReservation))
				return null;

			return FindCandidate(buildables);
		}

		public bool TryReserve(ProductionQueue queue, string actorType)
		{
			Update();
			if (queue == null || queue.AllQueued().Any() || HasReservation || HasCommittedRecovery ||
				HasLiveProvider || !everEstablished || !baseBuilder.Info.RadarRecoveryTypes.Contains(actorType))
				return false;

			reservationQueueActorId = queue.Actor.ActorID;
			reservationQueueType = queue.Info.Type;
			reservationActorType = actorType;
			reservationTick = world.WorldTick;
			reservationCommitmentObserved = false;
			LogTransition("{0} reserved radar recovery {1} on queue {2}/{3} at tick {4}",
				player, actorType, reservationQueueActorId, reservationQueueType, reservationTick);
			LogTransition("{0} recovery viability at tick {1}: queue={2}, power={3}/{4} state={5}, " +
				"refineries={6}, funds={7}, commitments={8}", player, world.WorldTick, queue.Actor.ActorID,
				playerPower?.PowerProvided ?? 0, playerPower?.PowerDrained ?? 0,
				playerPower?.PowerState.ToString() ?? "unmanaged",
				baseBuilder.CountActors(baseBuilder.SmartEconomyRefineryTypes),
				(playerResources?.Cash ?? 0) + (playerResources?.Resources ?? 0), committedRecoveryCount);
			return true;
		}

		public void LoadState(bool established, uint queueActorId, string queueType, string actorType, int tick,
			bool commitmentObserved)
		{
			everEstablished = established;
			reservationQueueActorId = queueActorId;
			reservationQueueType = queueType ?? "";
			reservationActorType = actorType ?? "";
			reservationTick = tick;

			// Preserve the active-production phase so a cancellation immediately after load
			// releases the exact queue without waiting for the pre-production timeout. Update
			// still validates both the queue identity and current commitment before retaining it.
			reservationCommitmentObserved = RadarRecoveryPolicy.RestoreCommitmentObservation(
				commitmentObserved, committedRecoveryCount > 0);
			Update();
		}

		void ReleaseReservation(string reason)
		{
			if (!HasReservation)
				return;

			LogTransition("{0} released radar recovery {1} on queue {2}/{3}: {4}",
				player, reservationActorType, reservationQueueActorId, reservationQueueType, reason);
			reservationQueueActorId = 0;
			reservationQueueType = "";
			reservationActorType = "";
			reservationTick = 0;
			reservationCommitmentObserved = false;
		}

		ActorInfo FindCandidate(IEnumerable<ActorInfo> buildables)
		{
			foreach (var type in baseBuilder.Info.RadarRecoveryTypes)
			{
				var candidate = buildables.FirstOrDefault(actor => actor.Name == type);
				if (candidate != null)
					return candidate;
			}

			return null;
		}

		bool QueueBlockerChanged(ProductionQueue queue, string blocker)
		{
			if (!DebugLoggingEnabled)
				return false;

			var queueActorId = queue.Actor.ActorID;
			if (lastQueueBlockers.TryGetValue(queueActorId, out var previous) && previous == blocker)
				return false;

			lastQueueBlockers[queueActorId] = blocker;
			return true;
		}

		void LogTransition(string format, params object[] args)
		{
			if (DebugLoggingEnabled)
				Log.Write("debug", "AI radar recovery: " + format, args);
		}

		bool DebugLoggingEnabled => baseBuilder.Info.RadarRecoveryDebugLogging || Game.Settings.Debug.BotDebug;

		string LossReason(IEnumerable<uint> actorIds)
		{
			var actors = actorIds.Select(world.GetActorById).Where(a => a != null).ToArray();
			if (actors.Any(a => !a.IsDead && a.IsInWorld && a.Owner != player))
				return "captured";

			return "destroyed-or-removed";
		}

		string ProviderSummary(IEnumerable<uint> actorIds)
		{
			return string.Join(",", actorIds.Select(id =>
			{
				var actor = world.GetActorById(id);
				return actor == null ? $"unknown#{id}" : $"{actor.Info.Name}#{id}";
			}));
		}

		IEnumerable<Actor> LiveHeadquartersDependentActors()
		{
			return world.Actors.Where(a => a.Owner == player && !a.IsDead && a.IsInWorld &&
				a.Info.TraitInfoOrDefault<BuildableInfo>()?.Prerequisites.Contains("hq") == true);
		}

		void LogDownstreamCompletion()
		{
			if (downstreamCompletionLogged || downstreamActorIdsAtLoss == null || !hasOperationalProvider)
				return;

			var completed = LiveHeadquartersDependentActors()
				.FirstOrDefault(a => !downstreamActorIdsAtLoss.Contains(a.ActorID));
			if (completed == null)
				return;

			downstreamCompletionLogged = true;
			LogTransition("{0} completed post-recovery headquarters-dependent actor at tick {1}: actor={2}#{3}",
				player, world.WorldTick, completed.Info.Name, completed.ActorID);
		}
	}
}
