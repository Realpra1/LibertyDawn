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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Creates and observes a deterministic, map-scoped performance workload. Intended for test maps only.")]
	public class PerformanceBaselineInfo : TraitInfo, IRulesetLoaded
	{
		public readonly int WarmupTick = 500;
		public readonly int MeasurementTicks = 2500;
		public readonly int SampleInterval = 100;
		public readonly int MinimumBots = 5;
		public readonly int MinimumMobileActors = 300;
		public readonly int SetupActorsPerBot = 340;
		public readonly int SetupInnerRadius = 4;
		public readonly int SetupOuterRadius = 28;
		public readonly int SetupAircraftInterval = 25;
		public readonly bool EnableMeasurements = true;
		public readonly string[] GdiGroundActorTypes = { "e1", "e2", "jeep", "mtnk", "msam", "htnk" };
		public readonly string[] NodGroundActorTypes = { "e1", "e3", "bggy", "ltnk", "ftnk", "stnk" };
		public readonly string GdiAircraftType = "orca";
		public readonly string NodAircraftType = "heli";

		public override object Create(ActorInitializer init) { return new PerformanceBaseline(this); }

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (WarmupTick < 0 || MeasurementTicks < 2500 || SampleInterval < 1 ||
				MeasurementTicks % SampleInterval != 0)
				throw new YamlException("PerformanceBaseline requires a non-negative warm-up, at least 2500 measured ticks, and a sample interval that divides the measurement window.");

			if (MinimumBots < 5 || MinimumMobileActors < 300 || SetupActorsPerBot < MinimumMobileActors ||
				SetupAircraftInterval < 2)
				throw new YamlException("PerformanceBaseline requires at least five bots and at least 300 setup/live mobile actors per bot.");

			foreach (var actorType in GdiGroundActorTypes.Concat(NodGroundActorTypes)
				.Concat(new[] { GdiAircraftType, NodAircraftType }).Distinct())
			{
				if (!rules.Actors.TryGetValue(actorType, out var actorInfo))
					throw new YamlException($"PerformanceBaseline actor type '{actorType}' does not exist.");

				if (!actorInfo.HasTraitInfo<MobileInfo>() && !actorInfo.HasTraitInfo<AircraftInfo>())
					throw new YamlException($"PerformanceBaseline actor type '{actorType}' is not mobile or aircraft.");
			}
		}
	}

	public class PerformanceBaseline : IWorldLoaded, ITick
	{
		sealed class BotSnapshot
		{
			public int LiveMobile;
			public int Queued;
			public int Moving;
			public int Busy;

			public void Reset()
			{
				LiveMobile = 0;
				Queued = 0;
				Moving = 0;
				Busy = 0;
			}
		}

		const string LogChannel = "performance_baseline";
		readonly PerformanceBaselineInfo info;
		readonly Dictionary<Player, BotSnapshot> botSnapshots = new Dictionary<Player, BotSnapshot>();
		readonly Dictionary<ActorInfo, bool> mobileActorTypes = new Dictionary<ActorInfo, bool>();
		Dictionary<uint, WPos> previousPositions = new Dictionary<uint, WPos>();
		Dictionary<uint, WPos> currentPositions = new Dictionary<uint, WPos>();
		Player[] bots = Array.Empty<Player>();
		long worldLoaded;
		long measurementStarted;
		bool configured;

		public PerformanceBaseline(PerformanceBaselineInfo info)
		{
			this.info = info;
		}

		public void WorldLoaded(World world, WorldRenderer wr)
		{
			worldLoaded = Stopwatch.GetTimestamp();
			bots = world.Players
				.Where(p => p.Playable && p.IsBot && !p.NonCombatant)
				.OrderBy(p => p.ClientIndex)
				.ToArray();

			if (bots.Length < info.MinimumBots)
				throw new InvalidOperationException($"PerformanceBaseline found {bots.Length} active bots; {info.MinimumBots} are required.");

			foreach (var bot in bots)
				SpawnWorkload(world, bot);

			if (info.EnableMeasurements)
			{
				foreach (var bot in bots)
					botSnapshots.Add(bot, new BotSnapshot());

				Log.AddChannel(LogChannel, "performance-baseline.csv");
				Log.Write(LogChannel,
					"world_tick,local_tick,elapsed_ms,warmup_elapsed_ms,total_live_actors,total_effects,player,bot_type,faction,team,spawn,live_mobile,queued,moving,busy,orders,cash,resources,earned,spent,units_killed,units_dead");
			}

			Log.Write("debug", "Performance baseline configured: warmup={0}, measured_ticks={1}, sample_interval={2}, bots={3}, floor={4}, setup_per_bot={5}, aircraft_interval={6}, measurements={7}.",
				info.WarmupTick, info.MeasurementTicks, info.SampleInterval, bots.Length,
				info.MinimumMobileActors, info.SetupActorsPerBot, info.SetupAircraftInterval,
				info.EnableMeasurements);
			var botIdentity = bots.Select(player =>
			{
				var client = world.LobbyInfo.ClientWithIndex(player.ClientIndex);
				return string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}|{4}",
					player.InternalName, player.BotType, player.Faction.InternalName,
					client?.Team ?? 0, player.SpawnPoint);
			});
			var startingCashValues = bots.Select(player => player.PlayerActor.Trait<PlayerResources>().Cash)
				.Distinct().ToArray();
			if (startingCashValues.Length != 1)
				throw new InvalidOperationException("PerformanceBaseline requires identical effective starting cash for every bot.");

			var startingCash = startingCashValues[0];
			Log.Write("debug", "Performance baseline accepted lobby identity: shortgame={0}, startingcash={1}, bots={2}.",
				world.WorldActor.Trait<MapOptions>().ShortGame, startingCash, string.Join(";", botIdentity));
			configured = true;
		}

		void SpawnWorkload(World world, Player player)
		{
			var groundActorTypes = player.Faction.InternalName == "gdi" ?
				info.GdiGroundActorTypes : info.NodGroundActorTypes;
			var aircraftType = player.Faction.InternalName == "gdi" ?
				info.GdiAircraftType : info.NodAircraftType;
			var cells = world.Map.FindTilesInAnnulus(player.HomeLocation, info.SetupInnerRadius, info.SetupOuterRadius)
				.OrderBy(c => StableCellOrder(c, player.ClientIndex))
				.ThenBy(c => c.Y)
				.ThenBy(c => c.X)
				.ToArray();
			var nextCell = 0;
			var spawned = 0;

			for (var i = 0; i < info.SetupActorsPerBot; i++)
			{
				var actorType = ((i + 1) % info.SetupAircraftInterval == 0 ?
					aircraftType : groundActorTypes[i % groundActorTypes.Length]).ToLowerInvariant();
				var actorInfo = world.Map.Rules.Actors[actorType];
				var positionable = actorInfo.TraitInfo<IPositionableInfo>();
				CPos cell = default(CPos);
				SubCell subCell = SubCell.Invalid;

				while (nextCell < cells.Length)
				{
					cell = cells[nextCell++];
					if (!positionable.CanEnterCell(world, null, cell))
						continue;

					subCell = positionable.SharesCell ? world.ActorMap.FreeSubCell(cell) : SubCell.FullCell;
					if (subCell != SubCell.Invalid)
						break;
				}

				if (subCell == SubCell.Invalid)
					throw new InvalidOperationException($"PerformanceBaseline could place only {spawned} of {info.SetupActorsPerBot} actors for {player.InternalName}.");

				world.CreateActor(actorType, new TypeDictionary
				{
					new OwnerInit(player),
					new LocationInit(cell),
					new SubCellInit(subCell),
					new FacingInit(new WAngle((i * 137 + player.ClientIndex * 53) & 1023)),
				});
				spawned++;
			}

			Log.Write("debug", "Performance baseline setup player={0}, bot={1}, faction={2}, spawn={3}, live_mobile_created={4}.",
				player.InternalName, player.BotType, player.Faction.InternalName, player.SpawnPoint, spawned);
		}

		static int StableCellOrder(CPos cell, int playerIndex)
		{
			unchecked
			{
				return (cell.X * 73856093) ^ (cell.Y * 19349663) ^ (playerIndex * 83492791);
			}
		}

		void ITick.Tick(Actor self)
		{
			var world = self.World;
			if (!configured || world.WorldTick < info.WarmupTick ||
				world.WorldTick > info.WarmupTick + info.MeasurementTicks ||
				(world.WorldTick - info.WarmupTick) % info.SampleInterval != 0)
				return;

			if (!info.EnableMeasurements)
			{
				Log.Write("debug", "Performance baseline control progress: world tick {0}, local tick {1}.",
					world.WorldTick, Game.LocalTick);
				return;
			}

			Sync.RunUnsynced(world, () => Sample(world));
		}

		void Sample(World world)
		{
			if (world.WorldTick == info.WarmupTick)
				measurementStarted = Stopwatch.GetTimestamp();

			var elapsed = (Stopwatch.GetTimestamp() - measurementStarted) * 1000D / Stopwatch.Frequency;
			var warmupElapsed = (measurementStarted - worldLoaded) * 1000D / Stopwatch.Frequency;
			var totalEffects = world.Effects.Count();
			var totalLiveActors = 0;
			var minimumLiveMobile = int.MaxValue;

			foreach (var snapshot in botSnapshots.Values)
				snapshot.Reset();

			foreach (var actor in world.Actors)
			{
				if (!actor.IsInWorld || actor.IsDead)
					continue;

				totalLiveActors++;
				if (!botSnapshots.TryGetValue(actor.Owner, out var snapshot) || !IsMobile(actor.Info))
					continue;

				snapshot.LiveMobile++;
				if (!actor.IsIdle)
					snapshot.Busy++;

				var position = actor.CenterPosition;
				currentPositions[actor.ActorID] = position;
				if (previousPositions.TryGetValue(actor.ActorID, out var previous) && previous != position)
					snapshot.Moving++;
			}

			foreach (var queue in world.ActorsWithTrait<ProductionQueue>())
				if (queue.Actor.IsInWorld && !queue.Actor.IsDead &&
					botSnapshots.TryGetValue(queue.Actor.Owner, out var snapshot))
					snapshot.Queued += queue.Trait.AllQueued().Count();

			foreach (var player in bots)
			{
				var snapshot = botSnapshots[player];
				minimumLiveMobile = Math.Min(minimumLiveMobile, snapshot.LiveMobile);
				var resources = player.PlayerActor.TraitOrDefault<PlayerResources>();
				var statistics = player.PlayerActor.TraitOrDefault<PlayerStatistics>();
				var client = world.LobbyInfo.ClientWithIndex(player.ClientIndex);

				Log.Write(LogChannel, string.Join(",", new[]
				{
					world.WorldTick.ToString(CultureInfo.InvariantCulture),
					Game.LocalTick.ToString(CultureInfo.InvariantCulture),
					elapsed.ToString("F3", CultureInfo.InvariantCulture),
					warmupElapsed.ToString("F3", CultureInfo.InvariantCulture),
					totalLiveActors.ToString(CultureInfo.InvariantCulture),
					totalEffects.ToString(CultureInfo.InvariantCulture),
					player.InternalName,
					player.BotType,
					player.Faction.InternalName,
					(client?.Team ?? 0).ToString(CultureInfo.InvariantCulture),
					player.SpawnPoint.ToString(CultureInfo.InvariantCulture),
					snapshot.LiveMobile.ToString(CultureInfo.InvariantCulture),
					snapshot.Queued.ToString(CultureInfo.InvariantCulture),
					snapshot.Moving.ToString(CultureInfo.InvariantCulture),
					snapshot.Busy.ToString(CultureInfo.InvariantCulture),
					(statistics?.OrderCount ?? 0).ToString(CultureInfo.InvariantCulture),
					(resources?.Cash ?? 0).ToString(CultureInfo.InvariantCulture),
					(resources?.Resources ?? 0).ToString(CultureInfo.InvariantCulture),
					(resources?.Earned ?? 0).ToString(CultureInfo.InvariantCulture),
					(resources?.Spent ?? 0).ToString(CultureInfo.InvariantCulture),
					(statistics?.UnitsKilled ?? 0).ToString(CultureInfo.InvariantCulture),
					(statistics?.UnitsDead ?? 0).ToString(CultureInfo.InvariantCulture),
				}));
			}

			var reusablePositions = previousPositions;
			previousPositions = currentPositions;
			currentPositions = reusablePositions;
			currentPositions.Clear();

			Log.Write("debug", "Performance baseline progress: world tick {0}, local tick {1}, elapsed_ms={2:F3}, live_actors={3}, effects={4}, minimum_live_mobile={5}.",
				world.WorldTick, Game.LocalTick, elapsed, totalLiveActors, totalEffects, minimumLiveMobile);

			if (world.WorldTick == info.WarmupTick + info.MeasurementTicks)
				Log.Write("debug", "Performance baseline measured interval complete: start={0}, end={1}, wall_ms={2:F3}.",
					info.WarmupTick, world.WorldTick, elapsed);
		}

		bool IsMobile(ActorInfo actorInfo)
		{
			if (!mobileActorTypes.TryGetValue(actorInfo, out var isMobile))
			{
				isMobile = actorInfo.HasTraitInfo<MobileInfo>() || actorInfo.HasTraitInfo<AircraftInfo>();
				mobileActorTypes.Add(actorInfo, isMobile);
			}

			return isMobile;
		}
	}
}
