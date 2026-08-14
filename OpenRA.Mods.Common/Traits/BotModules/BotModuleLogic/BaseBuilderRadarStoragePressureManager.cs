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
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	// CNC-101 owns storage-pressure silo policy. This narrow bridge lets its
	// actionable commitment become visible across independent construction queues
	// before radar recovery claims a different queue in the same decision window.
	sealed class BaseBuilderRadarStoragePressureManager
	{
		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		readonly PowerManager playerPower;
		readonly PlayerResources playerResources;
		uint reservationQueueActorId;
		string reservationQueueType = "";
		string reservationActorType = "";
		int reservationTick;
		int targetCount;
		int orderIssuedTick = -1;

		public BaseBuilderRadarStoragePressureManager(BaseBuilderBotModule baseBuilder, Player player)
		{
			this.baseBuilder = baseBuilder;
			this.player = player;
			world = player.World;
			playerPower = player.PlayerActor.TraitOrDefault<PowerManager>();
			playerResources = player.PlayerActor.Trait<PlayerResources>();
		}

		public uint ReservationQueueActorId => reservationQueueActorId;
		public string ReservationQueueType => reservationQueueType;
		public string ReservationActorType => reservationActorType;
		public int ReservationTick => reservationTick;
		public int TargetCount => targetCount;
		public int OrderIssuedTick => orderIssuedTick;

		bool HasReservation => reservationQueueActorId != 0;

		public bool OwnsSelection
		{
			get
			{
				Refresh();
				return HasReservation;
			}
		}

		public bool BlocksRadar
		{
			get
			{
				Refresh();
				var queued = HasQueuedCommitment();
				return RadarRecoveryPolicy.StorageCommitmentBlocksRadar(HasReservation,
					queued, orderIssuedTick == world.WorldTick);
			}
		}

		public ActorInfo Candidate(ProductionQueue currentQueue, int minimumExcessPower)
		{
			Refresh();
			if (!baseBuilder.RadarRecoveryNeeded ||
				!RadarRecoveryPolicy.HasActionableStoragePressure(
					playerResources.Resources, playerResources.ResourceCapacity))
				return null;

			if (baseBuilder.CountQueuedOrPendingActors(baseBuilder.Info.SiloTypes) > 0)
				return null;

			if (!HasReservation)
				ReserveFirstActionableQueue(minimumExcessPower);

			if (!HasReservation || reservationQueueActorId != currentQueue.Actor.ActorID ||
				reservationQueueType != currentQueue.Info.Type ||
				orderIssuedTick == world.WorldTick)
				return null;

			var candidate = currentQueue.BuildableItems().FirstOrDefault(a => a.Name == reservationActorType);
			if (candidate == null || !IsActionable(currentQueue, candidate, minimumExcessPower))
			{
				Release("reserved silo is no longer actionable");
				return null;
			}

			orderIssuedTick = world.WorldTick;
			LogTransition("{0} issued storage-pressure silo before radar recovery: tick={1}, type={2}, queue={3}",
				player, world.WorldTick, reservationActorType, reservationQueueActorId);
			return candidate;
		}

		public void LoadState(uint queueActorId, string queueType, string actorType, int tick, int savedTargetCount,
			int savedOrderIssuedTick)
		{
			reservationQueueActorId = queueActorId;
			reservationQueueType = queueType ?? "";
			reservationActorType = actorType ?? "";
			reservationTick = tick;
			targetCount = savedTargetCount;
			orderIssuedTick = savedOrderIssuedTick;
			Refresh();
		}

		void ReserveFirstActionableQueue(int minimumExcessPower)
		{
			var queues = baseBuilder.Info.BuildingQueues.Concat(baseBuilder.Info.DefenseQueues)
				.Distinct(StringComparer.Ordinal)
				.SelectMany(category => AIUtils.FindQueues(player, category))
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Actor.IsInWorld && !q.AllQueued().Any())
				.OrderBy(q => q.Actor.ActorID)
				.ThenBy(q => q.Info.Type, StringComparer.Ordinal);

			foreach (var queue in queues)
			{
				var candidate = queue.BuildableItems()
					.Where(a => baseBuilder.Info.SiloTypes.Contains(a.Name) && CanBuildAnother(a.Name))
					.OrderBy(a => a.Name, StringComparer.Ordinal)
					.FirstOrDefault(a => IsActionable(queue, a, minimumExcessPower));
				if (candidate == null)
					continue;

				reservationQueueActorId = queue.Actor.ActorID;
				reservationQueueType = queue.Info.Type;
				reservationActorType = candidate.Name;
				reservationTick = world.WorldTick;
				targetCount = baseBuilder.CountActors(baseBuilder.Info.SiloTypes) + 1;
				orderIssuedTick = -1;
				LogTransition("{0} reserved storage-pressure silo before radar recovery: tick={1}, type={2}, " +
					"queue={3}, target={4}, resources={5}/{6}", player, world.WorldTick,
					reservationActorType, $"{reservationQueueActorId}/{reservationQueueType}", targetCount,
					playerResources.Resources, playerResources.ResourceCapacity);
				return;
			}
		}

		bool CanBuildAnother(string actorType)
		{
			if (!baseBuilder.Info.BuildingLimits.TryGetValue(actorType, out var limit))
				return true;

			var committed = baseBuilder.CountActors(new[] { actorType }) +
				baseBuilder.CountQueuedOrPendingActors(new[] { actorType }) +
				(baseBuilder.IsOpeningStructureReserved(actorType) ? 1 : 0);
			return committed < limit;
		}

		bool IsActionable(ProductionQueue queue, ActorInfo actorInfo, int minimumExcessPower)
		{
			var power = actorInfo.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(p => p.Amount);
			var sufficientPower = playerPower == null || power + playerPower.ExcessPower >= minimumExcessPower;
			var sufficientFunds = Math.Max(0, playerResources.Cash + playerResources.Resources) >=
				queue.GetProductionCost(actorInfo);
			return sufficientPower && sufficientFunds;
		}

		void Refresh()
		{
			if (!HasReservation)
				return;

			if (targetCount > 0 && baseBuilder.CountActors(baseBuilder.Info.SiloTypes) >= targetCount)
			{
				Release("storage-pressure silo completed");
				return;
			}

			var queueActor = world.GetActorById(reservationQueueActorId);
			var validQueue = queueActor != null && queueActor.Owner == player && !queueActor.IsDead &&
				queueActor.IsInWorld && queueActor.TraitsImplementing<ProductionQueue>()
					.Any(q => q.Info.Type == reservationQueueType);
			if (!validQueue)
			{
				Release("reserved silo queue was lost");
				return;
			}

			if (HasQueuedCommitment())
				return;

			if (RadarRecoveryPolicy.ReservationExpired(reservationTick, world.WorldTick,
				baseBuilder.Info.RadarRecoveryReservationTimeout))
				Release("storage-pressure silo reservation timed out before production");
		}

		bool HasQueuedCommitment()
		{
			if (!HasReservation)
				return false;

			var queueActor = world.GetActorById(reservationQueueActorId);
			return queueActor != null && queueActor.TraitsImplementing<ProductionQueue>()
				.Any(q => q.Info.Type == reservationQueueType &&
					q.AllQueued().Any(i => i.Item == reservationActorType));
		}

		void Release(string reason)
		{
			if (!HasReservation)
				return;

			LogTransition("{0} released storage-pressure silo {1} on queue actor {2}: {3}",
				player, reservationActorType, reservationQueueActorId, reason);
			reservationQueueActorId = 0;
			reservationQueueType = "";
			reservationActorType = "";
			reservationTick = 0;
			targetCount = 0;
			orderIssuedTick = -1;
		}

		void LogTransition(string format, params object[] args)
		{
			if (baseBuilder.Info.RadarRecoveryDebugLogging || Game.Settings.Debug.BotDebug)
				Log.Write("debug", "AI radar recovery: " + format, args);
		}
	}
}
