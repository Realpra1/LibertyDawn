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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Uses existing unit orders for AI supply delivery, crate recovery, and emergency liquidation.")]
	public class SpecialOrderBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between special-order scans. Zero disables the module.")]
		public readonly int ScanInterval = 125;

		[Desc("Actor types that deliver cash using the existing DeliverCash order.")]
		public readonly HashSet<string> SupplyActorTypes = new HashSet<string>();

		[ActorReference]
		[Desc("Supply actor requested from UnitBuilder for an economically stranded ally. Empty disables allied supply production.")]
		public readonly string AlliedSupplyProductionActor = null;

		[ActorReference]
		[Desc("Factory/airfield equivalents that each grant one allied supply request per quota window.")]
		public readonly HashSet<string> AlliedSupplyProductionActorTypes = new HashSet<string>();

		[Desc("Ticks between allied supply production requests per currently available production actor/queue.")]
		public readonly int AlliedSupplyProductionQuotaInterval = 7500;

		[Desc("Cash at or below which an allied player may receive an available supply unit.")]
		public readonly int AlliedRescueCashThreshold = 0;

		[Desc("Only rescue allies that own none of these economy actor types.")]
		public readonly HashSet<string> AlliedEconomyActorTypes = new HashSet<string>();

		[Desc("Do not rescue an ally with none of these recovery-capable actor types.")]
		public readonly HashSet<string> AlliedRecoveryActorTypes = new HashSet<string>();

		[Desc("Collect visible crate actors using idle mobile units.")]
		public readonly bool CollectVisibleCrates = false;

		[Desc("When stranded without cash or an MCV, permit every idle mobile unit to search for crates.")]
		public readonly bool EmergencyCrateSearch = false;

		[Desc("MCV actor types used by emergency recovery checks.")]
		public readonly HashSet<string> McvActorTypes = new HashSet<string>();

		[Desc("Building types that may be sold as a last resort. Keep this allow-list conservative.")]
		public readonly HashSet<string> EmergencySellActorTypes = new HashSet<string>();

		[Desc("Minimum ticks between emergency building sales.")]
		public readonly int EmergencySellInterval = 1500;

		[Desc("Write special-order assignments to debug.log.")]
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (AlliedSupplyProductionQuotaInterval <= 0)
				throw new YamlException("AlliedSupplyProductionQuotaInterval must be positive.");
		}

		public override object Create(ActorInitializer init) { return new SpecialOrderBotModule(init.Self, this); }
	}

	public class SpecialOrderBotModule : ConditionalTrait<SpecialOrderBotModuleInfo>, IBotTick, IGameSaveTraitData
	{
		readonly World world;
		readonly Player player;
		readonly PlayerResources resources;
		readonly Dictionary<uint, uint> assignments = new Dictionary<uint, uint>();
		readonly Dictionary<string, AlliedSupplyProductionState> alliedSupplyStates =
			new Dictionary<string, AlliedSupplyProductionState>(StringComparer.Ordinal);
		IBotRequestTaggedUnitProduction[] taggedProduction;
		int scanTicks;
		int nextEmergencySaleTick;

		public SpecialOrderBotModule(Actor self, SpecialOrderBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
			resources = self.TraitOrDefault<PlayerResources>();
		}

		protected override void TraitEnabled(Actor self)
		{
			scanTicks = world.LocalRandom.Next(0, Math.Max(1, Info.ScanInterval));
		}

		protected override void Created(Actor self)
		{
			taggedProduction = self.Owner.PlayerActor.TraitsImplementing<IBotRequestTaggedUnitProduction>().ToArray();
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (Info.ScanInterval <= 0 || player.WinState != WinState.Undefined || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			CleanupAssignments();
			UpdateAlliedSupplyProduction(bot);
			AssignSupplyOrders(bot);
			AssignCrateOrders(bot);
			TryEmergencySale(bot);
		}

		void UpdateAlliedSupplyProduction(IBot bot)
		{
			if (string.IsNullOrEmpty(Info.AlliedSupplyProductionActor) || taggedProduction == null || taggedProduction.Length == 0)
				return;

			var requester = taggedProduction.FirstOrDefault(p => p.IsTraitEnabled());
			if (requester == null)
				return;
			var availableQueues = AvailableSupplyProductionQueues();
			var ownNeedsSupply = resources != null && resources.Cash + resources.Resources <= Info.AlliedRescueCashThreshold &&
				HasCashDeliveryTarget(player);

			foreach (var ally in world.Players.Where(p => !p.NonCombatant && !p.Spectating && p != player &&
				player.RelationshipWith(p) == PlayerRelationship.Ally).OrderBy(p => p.ClientIndex))
			{
				if (!alliedSupplyStates.TryGetValue(ally.InternalName, out var state))
					alliedSupplyStates.Add(ally.InternalName, state = new AlliedSupplyProductionState { WindowStartTick = -1 });

				var actors = world.Actors.Where(a => a.Owner == ally && !a.IsDead && a.IsInWorld).ToArray();
				var allyResources = ally.PlayerActor.TraitOrDefault<PlayerResources>();
				var lowCash = allyResources != null && allyResources.Cash + allyResources.Resources <= Info.AlliedRescueCashThreshold;
				var hasEconomy = actors.Any(a => Info.AlliedEconomyActorTypes.Contains(a.Info.Name));
				var hasMcv = actors.Any(a => Info.McvActorTypes.Contains(a.Info.Name));
				var needsSupply = lowCash && !hasEconomy && !hasMcv;
				var hasProduction = actors.Any(a => Info.AlliedSupplyProductionActorTypes.Contains(a.Info.Name));
				var hasMobile = actors.Any(a => a.Info.HasTraitInfo<MobileInfo>());
				var observation = new AlliedSupplyProductionObservation(needsSupply,
					hasProduction || hasMcv || hasMobile, ownNeedsSupply, availableQueues);
				var hadGivenUp = state.GaveUp;
				var decision = AlliedSupplyProductionPolicy.Evaluate(state, observation, world.WorldTick,
					Info.AlliedSupplyProductionQuotaInterval);
				var requestTag = SupplyRequestTag(ally);

				switch (decision.Action)
				{
					case AlliedSupplyProductionAction.Request:
						requester.RequestUnitProduction(bot, Info.AlliedSupplyProductionActor, requestTag, decision.RequestCount);
						Debug("requested {0} x{1} for stranded ally {2}; queues={3}, window={4}",
							Info.AlliedSupplyProductionActor, decision.RequestCount, ally.PlayerName, availableQueues, state.WindowStartTick);
						break;
					case AlliedSupplyProductionAction.Cancel:
					case AlliedSupplyProductionAction.GiveUp:
						var pending = requester.RequestedProductionCount(bot, requestTag);
						if (pending > 0)
						{
							requester.CancelUnitProduction(bot, requestTag);
							Debug("canceled {0} pending allied supply request(s) for {1}", pending, ally.PlayerName);
						}

						if (decision.Action == AlliedSupplyProductionAction.GiveUp && !hadGivenUp)
							Debug("gave up allied supply production for terminal ally {0}", ally.PlayerName);
						break;
				}
			}
		}

		int AvailableSupplyProductionQueues()
		{
			var actorInfo = world.Map.Rules.Actors[Info.AlliedSupplyProductionActor];
			var buildable = actorInfo?.TraitInfoOrDefault<BuildableInfo>();
			if (buildable == null)
				return 0;

			return buildable.Queue.SelectMany(q => AIUtils.FindQueues(player, q))
				.Where(q => Info.AlliedSupplyProductionActorTypes.Contains(q.Actor.Info.Name) &&
					q.BuildableItems().Any(a => a.Name == Info.AlliedSupplyProductionActor))
				.Select(q => q.Actor.ActorID).Distinct().Count();
		}

		bool HasCashDeliveryTarget(Player owner)
		{
			return world.Actors.Any(a => a.Owner == owner && !a.IsDead && a.IsInWorld &&
				a.Info.HasTraitInfo<AcceptsDeliveredCashInfo>());
		}

		static string SupplyRequestTag(Player ally) { return "allied-supply:" + ally.InternalName; }

		void CleanupAssignments()
		{
			var actors = world.Actors.ToDictionary(a => a.ActorID);
			foreach (var unitId in assignments.Keys.ToArray())
			{
				if (!actors.TryGetValue(unitId, out var unit) || unit.IsDead || !unit.IsInWorld || unit.IsIdle ||
					!actors.TryGetValue(assignments[unitId], out var target) || target.IsDead || !target.IsInWorld)
					assignments.Remove(unitId);
			}
		}

		void AssignSupplyOrders(IBot bot)
		{
			if (Info.SupplyActorTypes.Count == 0)
				return;

			foreach (var truck in world.Actors.Where(a => a.Owner == player && a.IsIdle &&
				Info.SupplyActorTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<DeliversCashInfo>() &&
				!assignments.ContainsKey(a.ActorID)))
			{
				var target = FindSupplyTarget(truck);
				if (target == null)
					continue;

				bot.QueueOrder(new Order("DeliverCash", truck, Target.FromActor(target), false));
				assignments[truck.ActorID] = target.ActorID;
				Debug("supply {0}#{1} -> {2} {3}#{4}", truck.Info.Name, truck.ActorID,
					target.Owner.PlayerName, target.Info.Name, target.ActorID);
			}
		}

		Actor FindSupplyTarget(Actor truck)
		{
			var own = EligibleCashTargets(truck, player).ClosestTo(truck.CenterPosition);
			if (resources != null && resources.Cash + resources.Resources <= Info.AlliedRescueCashThreshold && own != null)
				return own;

			foreach (var ally in world.Players.Where(p => !p.NonCombatant && !p.Spectating && p != player &&
				player.RelationshipWith(p) == PlayerRelationship.Ally).OrderBy(p => p.ClientIndex))
			{
				var allyResources = ally.PlayerActor.TraitOrDefault<PlayerResources>();
				if (allyResources == null || allyResources.Cash + allyResources.Resources > Info.AlliedRescueCashThreshold)
					continue;

				var allyActors = world.Actors.Where(a => a.Owner == ally && !a.IsDead && a.IsInWorld).ToArray();
				if (allyActors.Any(a => Info.AlliedEconomyActorTypes.Contains(a.Info.Name)) ||
					!allyActors.Any(a => Info.AlliedRecoveryActorTypes.Contains(a.Info.Name)))
					continue;

				var target = EligibleCashTargets(truck, ally).ClosestTo(truck.CenterPosition);
				if (target != null)
					return target;
			}

			return own;
		}

		IEnumerable<Actor> EligibleCashTargets(Actor truck, Player owner)
		{
			var deliveryType = truck.Info.TraitInfo<DeliversCashInfo>().Type;
			return world.Actors.Where(a => a.Owner == owner && !a.IsDead && a.IsInWorld)
				.Where(a =>
				{
					var accepts = a.Info.TraitInfoOrDefault<AcceptsDeliveredCashInfo>();
					return accepts != null && accepts.ValidRelationships.HasRelationship(a.Owner.RelationshipWith(player)) &&
						(accepts.ValidTypes.Count == 0 || (!string.IsNullOrEmpty(deliveryType) && accepts.ValidTypes.Contains(deliveryType)));
				});
		}

		void AssignCrateOrders(IBot bot)
		{
			if (!Info.CollectVisibleCrates)
				return;

			var crates = world.Actors.Where(a => !a.IsDead && a.IsInWorld && a.Info.HasTraitInfo<CrateInfo>() &&
				a.CanBeViewedByPlayer(player)).OrderBy(a => a.ActorID).ToArray();
			if (crates.Length == 0)
				return;

			var stranded = IsStranded();
			var collectors = world.Actors.Where(a => a.Owner == player && a.IsIdle && a.Info.HasTraitInfo<MobileInfo>() &&
				!Info.SupplyActorTypes.Contains(a.Info.Name) && !assignments.ContainsKey(a.ActorID));
			if (!stranded || !Info.EmergencyCrateSearch)
				collectors = collectors.Take(1);

			foreach (var unit in collectors)
			{
				var crate = crates.Where(c => !assignments.ContainsValue(c.ActorID)).ClosestTo(unit.CenterPosition);
				if (crate == null)
					break;

				bot.QueueOrder(new Order("Move", unit, Target.FromCell(world, crate.Location), false));
				assignments[unit.ActorID] = crate.ActorID;
				Debug("crate search {0}#{1} -> {2}#{3}", unit.Info.Name, unit.ActorID, crate.Info.Name, crate.ActorID);
			}
		}

		bool IsStranded()
		{
			if (resources == null || resources.Cash + resources.Resources > 0)
				return false;

			return !world.Actors.Any(a => a.Owner == player && !a.IsDead && a.IsInWorld &&
				Info.McvActorTypes.Contains(a.Info.Name));
		}

		void TryEmergencySale(IBot bot)
		{
			if (!IsStranded() || Info.EmergencySellActorTypes.Count == 0 || world.WorldTick < nextEmergencySaleTick)
				return;

			if (world.Actors.Any(a => a.Owner == player && !a.IsDead && a.IsInWorld && a.Info.HasTraitInfo<MobileInfo>()))
				return;

			var ownBuildings = world.Actors.Where(a => a.Owner == player && !a.IsDead && a.IsInWorld &&
				a.Info.HasTraitInfo<BuildingInfo>()).ToArray();
			if (ownBuildings.Length <= 1)
				return;

			var building = ownBuildings.Where(a =>
				Info.EmergencySellActorTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<SellableInfo>())
				.OrderBy(a => a.GetSellValue()).ThenBy(a => a.ActorID).FirstOrDefault();
			if (building == null)
				return;

			bot.QueueOrder(new Order("Sell", building, false));
			nextEmergencySaleTick = world.WorldTick + Math.Max(1, Info.EmergencySellInterval);
			Debug("emergency sale {0}#{1}, value={2}", building.Info.Name, building.ActorID, building.GetSellValue());
		}

		void Debug(string format, params object[] args)
		{
			if (Info.DebugLogging)
				AIUtils.BotDebug("AI ({0}) special orders: {1}", player.ClientIndex, string.Format(format, args));
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			var states = alliedSupplyStates.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToArray();
			return new List<MiniYamlNode>
			{
				new MiniYamlNode("AlliedSupplyPlayers", FieldSaver.FormatValue(states.Select(kv => kv.Key).ToArray())),
				new MiniYamlNode("AlliedSupplyWindowStarts", FieldSaver.FormatValue(states.Select(kv => kv.Value.WindowStartTick).ToArray())),
				new MiniYamlNode("AlliedSupplyRequestCounts", FieldSaver.FormatValue(states.Select(kv => kv.Value.RequestsInWindow).ToArray())),
				new MiniYamlNode("AlliedSupplyGaveUp", FieldSaver.FormatValue(states.Select(kv => kv.Value.GaveUp).ToArray()))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var playersNode = data.FirstOrDefault(n => n.Key == "AlliedSupplyPlayers");
			if (playersNode == null)
				return;

			var players = FieldLoader.GetValue<string[]>("AlliedSupplyPlayers", playersNode.Value.Value);
			var starts = LoadStateArray<int>(data, "AlliedSupplyWindowStarts");
			var counts = LoadStateArray<int>(data, "AlliedSupplyRequestCounts");
			var gaveUp = LoadStateArray<bool>(data, "AlliedSupplyGaveUp");
			alliedSupplyStates.Clear();
			for (var i = 0; i < players.Length; i++)
				alliedSupplyStates[players[i]] = new AlliedSupplyProductionState
				{
					WindowStartTick = i < starts.Length ? starts[i] : -1,
					RequestsInWindow = i < counts.Length ? counts[i] : 0,
					GaveUp = i < gaveUp.Length && gaveUp[i]
				};
		}

		static T[] LoadStateArray<T>(List<MiniYamlNode> data, string key)
		{
			var node = data.FirstOrDefault(n => n.Key == key);
			return node == null ? Array.Empty<T>() : FieldLoader.GetValue<T[]>(key, node.Value.Value);
		}
	}
}
