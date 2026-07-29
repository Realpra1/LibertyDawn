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
using System.Globalization;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Attach this to the player actor to collect observer stats.")]
	public class PlayerStatisticsInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new PlayerStatistics(init.Self); }
	}

	public class PlayerStatistics : ITick, IResolveOrder, INotifyCreated, IWorldLoaded, IGameSaveTraitData
	{
		PlayerResources resources;
		PlayerExperience experience;

		public int OrderCount;

		public int Experience => experience != null ? experience.Experience : 0;

		// Low resolution (every 30 seconds) record of earnings, covering the entire game
		public List<int> IncomeSamples = new List<int>(100);
		public int Income;
		public int DisplayIncome;

		public List<int> ArmySamples = new List<int>(100);

		public int KillsCost;
		public int DeathsCost;

		public int UnitsKilled;
		public int UnitsDead;

		public int BuildingsKilled;
		public int BuildingsDead;

		public int ArmyValue;
		public int AssetsValue;

		// High resolution (every second) record of earnings, limited to the last minute
		readonly Queue<int> earnedSeconds = new Queue<int>(60);

		int lastIncome;
		int lastIncomeTick;
		int ticks;

		bool armyGraphDisabled;
		bool incomeGraphDisabled;
		public readonly Cache<string, ArmyUnit> Units;

		// Per actor-type built/kills/losses ledger for the adaptive-AI build weighting (see
		// AdaptiveWeighting.cs). Lives here rather than on a bot module because it is per-player
		// state shared by every bot module attached to that player (unit and building queues alike),
		// and because the *Info config objects those modules read are shared by reference across every
		// player using the same bot personality - mutable learned state can never live there.
		public readonly Cache<string, AdaptiveTypeStats> AdaptiveStats;

		// World tick the adaptive ledger's per-minute window is next due to roll into DecayedScore.
		// Owned here, not per-bot-module, so it is only ever rolled once per minute even though both
		// UnitBuilderBotModule and BaseBuilderQueueManager read from AdaptiveStats.
		public int NextAdaptiveRolloverTick;

		public PlayerStatistics(Actor self)
		{
			Units = new Cache<string, ArmyUnit>(name => new ArmyUnit(self.World.Map.Rules.Actors[name], self.Owner));
			AdaptiveStats = new Cache<string, AdaptiveTypeStats>(name => new AdaptiveTypeStats());
		}

		void INotifyCreated.Created(Actor self)
		{
			resources = self.TraitOrDefault<PlayerResources>();
			experience = self.TraitOrDefault<PlayerExperience>();

			incomeGraphDisabled = resources == null;
		}

		void ITick.Tick(Actor self)
		{
			ticks++;

			var timestep = self.World.Timestep;
			if (ticks * timestep >= 30000)
			{
				ticks = 0;

				if (!armyGraphDisabled && (ArmyValue != 0 || self.Owner.WinState == WinState.Undefined))
					ArmySamples.Add(ArmyValue);
				else
					armyGraphDisabled = true;

				if (!incomeGraphDisabled && (Income != 0 || self.Owner.WinState == WinState.Undefined))
					IncomeSamples.Add(Income);
				else
					incomeGraphDisabled = true;
			}

			if (resources == null)
				return;

			var tickDelta = self.World.WorldTick - lastIncomeTick;
			if (tickDelta * timestep >= 1000)
			{
				lastIncomeTick = self.World.WorldTick;

				var lastEarned = earnedSeconds.Count > 59 ? earnedSeconds.Dequeue() : 0;
				lastIncome = DisplayIncome = Income;
				Income = resources.Earned - lastEarned;
				earnedSeconds.Enqueue(resources.Earned);
			}
			else
				DisplayIncome = int2.Lerp(lastIncome, Income, tickDelta * timestep, 1000);
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString.StartsWith("Dev"))
				return;

			OrderCount++;
		}

		public void WorldLoaded(World w, WorldRenderer wr)
		{
			if (!armyGraphDisabled)
				ArmySamples.Add(ArmyValue);

			if (!incomeGraphDisabled)
				IncomeSamples.Add(Income);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			var entries = new List<MiniYamlNode>();
			foreach (var kv in AdaptiveStats.OrderBy(kv => kv.Key, StringComparer.Ordinal))
			{
				var s = kv.Value;
				var packed = FieldSaver.FormatValue(new[] { s.BuiltCount, s.BuiltValue, s.KillsCount, s.KillsValue, s.LossesCount, s.LossesValue, s.MinuteKillsValue, s.MinuteLossesValue })
					+ "|" + s.DecayedScore.ToString("R", CultureInfo.InvariantCulture);
				entries.Add(new MiniYamlNode(kv.Key, packed));
			}

			return new List<MiniYamlNode>()
			{
				new MiniYamlNode("AdaptiveStats", null, entries),
				new MiniYamlNode("NextAdaptiveRolloverTick", FieldSaver.FormatValue(NextAdaptiveRolloverTick)),
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			var adaptiveNode = data.FirstOrDefault(n => n.Key == "AdaptiveStats");
			if (adaptiveNode != null)
			{
				foreach (var entry in adaptiveNode.Value.Nodes)
				{
					var parts = entry.Value.Value.Split('|');
					var ints = FieldLoader.GetValue<int[]>("AdaptiveStats", parts[0]);
					var stats = AdaptiveStats[entry.Key];
					stats.BuiltCount = ints[0];
					stats.BuiltValue = ints[1];
					stats.KillsCount = ints[2];
					stats.KillsValue = ints[3];
					stats.LossesCount = ints[4];
					stats.LossesValue = ints[5];
					stats.MinuteKillsValue = ints[6];
					stats.MinuteLossesValue = ints[7];
					stats.DecayedScore = double.Parse(parts[1], CultureInfo.InvariantCulture);
				}
			}

			var rolloverNode = data.FirstOrDefault(n => n.Key == "NextAdaptiveRolloverTick");
			if (rolloverNode != null)
				NextAdaptiveRolloverTick = FieldLoader.GetValue<int>("NextAdaptiveRolloverTick", rolloverNode.Value.Value);
		}
	}

	// Cumulative built/kills/losses ledger for one actor type, owned by the player that built/lost/killed
	// it. See AdaptiveWeighting.cs for the (pure, unit-tested) math that turns this into a build weight.
	public class AdaptiveTypeStats
	{
		public int BuiltCount;
		public int BuiltValue;

		public int KillsCount;
		public int KillsValue;

		public int LossesCount;
		public int LossesValue;

		// Current adaptive-rollover window (see PlayerStatistics.NextAdaptiveRolloverTick); rolled into
		// DecayedScore and reset by UnitBuilderBotModule's periodic check.
		public int MinuteKillsValue;
		public int MinuteLossesValue;

		// Exponentially-decayed kills/losses value ratio, blended once per rollover window. Starts at 1
		// ("break-even") so an untested type neither boosts nor suppresses its own authored weight.
		public double DecayedScore = 1;
	}

	public class ArmyUnit
	{
		public readonly ActorInfo ActorInfo;
		public readonly Animation Icon;
		public readonly string IconPalette;
		public readonly bool IconPaletteIsPlayerPalette;
		public readonly int ProductionQueueOrder;
		public readonly int BuildPaletteOrder;
		public readonly TooltipInfo TooltipInfo;
		public readonly BuildableInfo BuildableInfo;

		public int Count { get; set; }

		public ArmyUnit(ActorInfo actorInfo, Player owner)
		{
			ActorInfo = actorInfo;

			var queues = owner.World.Map.Rules.Actors.Values
				.SelectMany(a => a.TraitInfos<ProductionQueueInfo>());

			BuildableInfo = actorInfo.TraitInfoOrDefault<BuildableInfo>();
			TooltipInfo = actorInfo.TraitInfos<TooltipInfo>().FirstOrDefault(info => info.EnabledByDefault);

			var rsi = actorInfo.TraitInfoOrDefault<RenderSpritesInfo>();

			if (BuildableInfo != null && rsi != null)
			{
				var image = rsi.GetImage(actorInfo, owner.World.Map.Rules.Sequences, owner.Faction.Name);
				Icon = new Animation(owner.World, image);
				Icon.Play(BuildableInfo.Icon);
				IconPalette = BuildableInfo.IconPalette;
				IconPaletteIsPlayerPalette = BuildableInfo.IconPaletteIsPlayerPalette;
				BuildPaletteOrder = BuildableInfo.BuildPaletteOrder;
				ProductionQueueOrder = queues.Where(q => BuildableInfo.Queue.Contains(q.Type))
					.Select(q => q.DisplayOrder)
					.MinByOrDefault(o => o);
			}
		}
	}

	[Desc("Attach this to a unit to update observer stats.")]
	public class UpdatesPlayerStatisticsInfo : TraitInfo
	{
		[Desc("Add to army value in statistics")]
		public bool AddToArmyValue = false;

		[Desc("Add to assets value in statistics")]
		public bool AddToAssetsValue = true;

		[ActorReference]
		[Desc("Count this actor as a different type in the spectator army display.")]
		public string OverrideActor = null;

		public override object Create(ActorInitializer init) { return new UpdatesPlayerStatistics(this, init.Self); }
	}

	public class UpdatesPlayerStatistics : INotifyKilled, INotifyCreated, INotifyOwnerChanged, INotifyActorDisposing
	{
		readonly UpdatesPlayerStatisticsInfo info;
		readonly string actorName;
		readonly int cost = 0;

		PlayerStatistics playerStats;
		bool includedInArmyValue = false;
		bool includedInAssetsValue = false;

		public UpdatesPlayerStatistics(UpdatesPlayerStatisticsInfo info, Actor self)
		{
			this.info = info;
			var valuedInfo = self.Info.TraitInfoOrDefault<ValuedInfo>();
			cost = valuedInfo != null ? valuedInfo.Cost : 0;
			playerStats = self.Owner.PlayerActor.Trait<PlayerStatistics>();
			actorName = info.OverrideActor ?? self.Info.Name;
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (self.Owner.WinState != WinState.Undefined)
				return;

			if (includedInArmyValue)
			{
				playerStats.ArmyValue -= cost;
				includedInArmyValue = false;
				playerStats.Units[actorName].Count--;
			}

			if (includedInAssetsValue)
			{
				playerStats.AssetsValue -= cost;
				includedInAssetsValue = false;
			}

			playerStats.DeathsCost += cost;

			if (!self.Owner.NonCombatant)
			{
				var lossStats = playerStats.AdaptiveStats[actorName];
				lossStats.LossesCount++;
				lossStats.LossesValue += cost;
				lossStats.MinuteLossesValue += cost;
			}

			if (e.Attacker == self)
				return;

			var attackerStats = e.Attacker.Owner.PlayerActor.Trait<PlayerStatistics>();
			if (self.Info.HasTraitInfo<BuildingInfo>())
			{
				if (!self.Owner.NonCombatant)
					attackerStats.BuildingsKilled++;

				playerStats.BuildingsDead++;
			}
			else if (self.Info.HasTraitInfo<IPositionableInfo>())
			{
				if (!self.Owner.NonCombatant)
					attackerStats.UnitsKilled++;

				playerStats.UnitsDead++;
			}

			if (!self.Owner.NonCombatant)
			{
				attackerStats.KillsCost += cost;

				var killStats = attackerStats.AdaptiveStats[e.Attacker.Info.Name];
				killStats.KillsCount++;
				killStats.KillsValue += cost;
				killStats.MinuteKillsValue += cost;
			}
		}

		void INotifyCreated.Created(Actor self)
		{
			includedInArmyValue = info.AddToArmyValue;
			if (includedInArmyValue)
			{
				playerStats.ArmyValue += cost;
				playerStats.Units[actorName].Count++;
			}

			includedInAssetsValue = info.AddToAssetsValue;
			if (includedInAssetsValue)
				playerStats.AssetsValue += cost;

			var builtStats = playerStats.AdaptiveStats[actorName];
			builtStats.BuiltCount++;
			builtStats.BuiltValue += cost;
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			var newOwnerStats = newOwner.PlayerActor.Trait<PlayerStatistics>();
			if (includedInArmyValue)
			{
				playerStats.ArmyValue -= cost;
				newOwnerStats.ArmyValue += cost;
				playerStats.Units[actorName].Count--;
				newOwnerStats.Units[actorName].Count++;
			}

			if (includedInAssetsValue)
			{
				playerStats.AssetsValue -= cost;
				newOwnerStats.AssetsValue += cost;
			}

			playerStats = newOwnerStats;
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (includedInArmyValue)
			{
				playerStats.ArmyValue -= cost;
				includedInArmyValue = false;
				playerStats.Units[actorName].Count--;
			}

			if (includedInAssetsValue)
			{
				playerStats.AssetsValue -= cost;
				includedInAssetsValue = false;
			}
		}
	}
}
