#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Builds a small opening infantry garrison and rallies the first barracks beside the construction yard.")]
	public class OpeningGarrisonBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Infantry production structures eligible for the opening rally point.")]
		public readonly HashSet<string> BarracksTypes = new HashSet<string>();

		[Desc("Construction yards that the first barracks should rally beside.")]
		public readonly HashSet<string> ConstructionYardTypes = new HashSet<string>();

		[Desc("Preferred basic rifle infantry types, in fallback order.")]
		public readonly string[] RifleTypes = Array.Empty<string>();

		[Desc("Preferred anti-armor infantry types, in fallback order.")]
		public readonly string[] RocketTypes = Array.Empty<string>();

		[Desc("Initial construction-yard or MCV types used as the emergency-garrison anchor.")]
		public readonly HashSet<string> EmergencyAnchorTypes = new HashSet<string>();

		[Desc("Combat infantry types that count as nearby emergency defenders.")]
		public readonly HashSet<string> EmergencyDefenderTypes = new HashSet<string>();

		[Desc("Cheap emergency infantry requested when the anchor has too few nearby defenders, in fallback order.")]
		public readonly string[] EmergencyInfantryTypes = Array.Empty<string>();

		[Desc("Number of emergency defenders maintained near the initial anchor and radius in cells.")]
		public readonly int EmergencyDefenderCount = 0;
		public readonly int EmergencyDefenderRadius = 10;

		[Desc("Number of rifle and rocket infantry forced during the opening.")]
		public readonly int RifleCount = 10;
		public readonly int RocketCount = 7;

		[Desc("Ticks between opening production requests.")]
		public readonly int RequestInterval = 50;

		[Desc("Maximum ring distance from the construction-yard footprint used to find a legal rally cell.")]
		public readonly int MaximumRallyDistance = 3;

		[Desc("Prefer bottom-middle rally cells beneath the construction yard before considering its other sides.")]
		public readonly bool PreferRallyBelowConstructionYard = false;

		[Desc("Write opening-garrison decisions to debug.log.")]
		public readonly bool DebugLogging = false;

		public override object Create(ActorInitializer init) { return new OpeningGarrisonBotModule(init.Self, this); }
	}

	public class OpeningGarrisonBotModule : ConditionalTrait<OpeningGarrisonBotModuleInfo>, IBotTick,
		IBotRallyPointManager, IGameSaveTraitData
	{
		readonly World world;
		readonly Player player;
		IBotRequestUnitProduction[] unitProduction;
		IBotRequestPauseUnitProduction[] productionPauses;
		PlayerStatistics playerStatistics;
		int nextRequestTick;
		int nextRallyRetryTick;
		uint pendingRallyBarracksId;
		CPos pendingRallyTarget;
		bool rallyOrderIssued;
		bool completionLogged;
		int emergencyBurstRequests;

		public OpeningGarrisonBotModule(Actor self, OpeningGarrisonBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			unitProduction = player.PlayerActor.TraitsImplementing<IBotRequestUnitProduction>().ToArray();
			productionPauses = player.PlayerActor.TraitsImplementing<IBotRequestPauseUnitProduction>().ToArray();
			playerStatistics = player.PlayerActor.Trait<PlayerStatistics>();
		}

		void IBotTick.BotTick(IBot bot)
		{
			UpdateOpeningRallyPoint(bot);
			if (world.WorldTick >= nextRequestTick && TryRequestEmergencyGarrison(bot))
			{
				nextRequestTick = world.WorldTick + Math.Max(1, Info.RequestInterval);
				return;
			}

			var riflesBuilt = BuiltCount(Info.RifleTypes);
			var rocketsBuilt = BuiltCount(Info.RocketTypes);
			if (riflesBuilt >= Info.RifleCount && rocketsBuilt >= Info.RocketCount)
			{
				if (!completionLogged)
				{
					completionLogged = true;
					LogDecision("{0} completed opening garrison: rifles={1}/{2}, rockets={3}/{4}.",
						player, riflesBuilt, Info.RifleCount, rocketsBuilt, Info.RocketCount);
				}

				return;
			}

			if (world.WorldTick < nextRequestTick)
				return;

			// Emergency defenders above remain available, but the discretionary opening
			// garrison must respect economy owners that are protecting construction cash.
			// These requests are external UnitBuilder work and would otherwise bypass its
			// ordinary random-production pause while the first Refinery is still producing.
			if (productionPauses.Any(p => p.PauseUnitProduction))
				return;

			if (Info.RifleTypes.Concat(Info.RocketTypes).Any(type => unitProduction.Any(p => p.IsTraitEnabled() &&
				p.RequestedProductionCount(bot, type) > 0)))
				return;

			var preferRifle = OpeningGarrisonLogic.ShouldBuildRifle(riflesBuilt, Info.RifleCount, rocketsBuilt, Info.RocketCount);
			var preferred = preferRifle ? Info.RifleTypes : Info.RocketTypes;
			if (TryRequest(bot, preferred, riflesBuilt, rocketsBuilt))
				nextRequestTick = world.WorldTick + Math.Max(1, Info.RequestInterval);
		}

		bool TryRequestEmergencyGarrison(IBot bot)
		{
			if (Info.EmergencyDefenderCount <= 0 || Info.EmergencyInfantryTypes.Length == 0 ||
				Info.EmergencyAnchorTypes.Count == 0 || Info.EmergencyDefenderTypes.Count == 0)
				return false;

			var anchor = world.Actors.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
				Info.EmergencyAnchorTypes.Contains(a.Info.Name)).OrderBy(a => a.ActorID).FirstOrDefault();
			if (anchor == null)
				return false;

			var radiusSquared = (long)Math.Max(1, Info.EmergencyDefenderRadius) * Math.Max(1, Info.EmergencyDefenderRadius);
			var nearby = world.Actors.Count(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
				Info.EmergencyDefenderTypes.Contains(a.Info.Name) &&
				(long)(a.Location - anchor.Location).LengthSquared <= radiusSquared);
			if (nearby > 0 && emergencyBurstRequests >= Info.EmergencyDefenderCount)
				emergencyBurstRequests = 0;

			if (OpeningGarrisonLogic.EmergencyRequestsNeeded(nearby, emergencyBurstRequests,
				Info.EmergencyDefenderCount) <= 0)
				return false;

			foreach (var type in Info.EmergencyInfantryTypes)
			{
				if (!CanProduce(type))
					continue;

				var producer = unitProduction.FirstOrDefault(p => p.IsTraitEnabled());
				if (producer == null)
					return false;

				producer.RequestUnitProduction(bot, type);
				emergencyBurstRequests++;
				LogDecision("{0} requested emergency infantry {1}: anchor={2}#{3} nearby=0, burst={4}/{5}.",
					player, type, anchor.Info.Name, anchor.ActorID, emergencyBurstRequests, Info.EmergencyDefenderCount);
				return true;
			}

			return false;
		}

		bool TryRequest(IBot bot, IEnumerable<string> types, int riflesBuilt, int rocketsBuilt)
		{
			var alternatives = types.ToArray();
			if (alternatives.Length == 0 || alternatives.Any(type => unitProduction.Any(p => p.IsTraitEnabled() &&
				p.RequestedProductionCount(bot, type) > 0)))
				return false;

			foreach (var type in alternatives)
			{
				if (!CanProduce(type))
					continue;

				var producer = unitProduction.FirstOrDefault(p => p.IsTraitEnabled());
				if (producer == null)
					return false;

				producer.RequestUnitProduction(bot, type);
				LogDecision("{0} requested opening infantry {1}: rifles={2}/{3}, rockets={4}/{5}.",
					player, type, riflesBuilt, Info.RifleCount, rocketsBuilt, Info.RocketCount);
				return true;
			}

			return false;
		}

		bool CanProduce(string type)
		{
			if (!world.Map.Rules.Actors.TryGetValue(type, out var actor))
				return false;

			var buildable = actor.TraitInfoOrDefault<BuildableInfo>();
			return buildable != null && buildable.Queue.Any(category => AIUtils.FindQueues(player, category)
				.Any(queue => !queue.AllQueued().Any() && queue.BuildableItems().Any(item => item.Name == type)));
		}

		int BuiltCount(IEnumerable<string> types)
		{
			return types.Sum(type => playerStatistics.AdaptiveStats[type].BuiltCount);
		}

		void UpdateOpeningRallyPoint(IBot bot)
		{
			if (rallyOrderIssued)
			{
				var managed = world.ActorsWithTrait<RallyPoint>()
					.FirstOrDefault(a => a.Actor.ActorID == pendingRallyBarracksId && a.Actor.IsInWorld && !a.Actor.IsDead);
				if (managed.Actor != null && managed.Trait.Path.Count > 0 && managed.Trait.Path[0] == pendingRallyTarget)
					return;

				rallyOrderIssued = false;
				pendingRallyBarracksId = 0;
			}

			if (pendingRallyBarracksId != 0)
			{
				var pending = world.ActorsWithTrait<RallyPoint>()
					.FirstOrDefault(a => a.Actor.ActorID == pendingRallyBarracksId && a.Actor.IsInWorld && !a.Actor.IsDead);
				if (pending.Actor != null && pending.Trait.Path.Count > 0 && pending.Trait.Path[0] == pendingRallyTarget)
				{
					rallyOrderIssued = true;
					LogDecision("{0} confirmed opening rally for {1} at {2}: target={3}.",
						player, pending.Actor.Info.Name, pending.Actor.Location, pendingRallyTarget);
					return;
				}

				if (world.WorldTick < nextRallyRetryTick)
					return;

				pendingRallyBarracksId = 0;
			}

			TrySetOpeningRallyPoint(bot);
		}

		void TrySetOpeningRallyPoint(IBot bot)
		{
			var barracks = world.ActorsWithTrait<RallyPoint>()
				.Where(a => a.Actor.Owner == player && a.Actor.IsInWorld && !a.Actor.IsDead && Info.BarracksTypes.Contains(a.Actor.Info.Name))
				.OrderBy(a => a.Actor.ActorID).FirstOrDefault();
			if (barracks.Actor == null)
				return;

			var yard = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead && Info.ConstructionYardTypes.Contains(a.Info.Name))
				.OrderBy(a => a.ActorID).FirstOrDefault();
			if (yard == null)
				return;

			var yardInfo = yard.Info.TraitInfoOrDefault<BuildingInfo>();
			if (yardInfo == null)
				return;

			CPos? target = null;
			var targetDistance = 0;
			var targetDirection = "fallback";
			if (Info.PreferRallyBelowConstructionYard)
			{
				// Keep the first infantry clear of the Fact's lower enclosure corners and wall.
				for (var distance = 2; distance <= Math.Max(2, Info.MaximumRallyDistance) && target == null; distance++)
				{
					target = OpeningGarrisonLogic.CellsBelowBuilding(yard.Location, yardInfo.Dimensions, distance)
						.Where(IsLegalRallyCell).Select(c => (CPos?)c).FirstOrDefault();
					targetDistance = distance;
					targetDirection = "below";
				}
			}

			if (target == null)
				targetDirection = "fallback";

			for (var distance = 1; distance <= Math.Max(1, Info.MaximumRallyDistance) && target == null; distance++)
			{
				target = OpeningGarrisonLogic.CellsAroundBuilding(yard.Location, yardInfo.Dimensions, distance)
					.Where(IsLegalRallyCell)
					.OrderBy(c => (c - barracks.Actor.Location).LengthSquared)
					.Select(c => (CPos?)c).FirstOrDefault();
				targetDistance = distance;
			}

			if (target == null)
			{
				LogDecision("{0} could not find a legal opening rally cell near {1} at {2}.", player, yard.Info.Name, yard.Location);
				return;
			}

			bot.QueueOrder(new Order("SetRallyPoint", barracks.Actor, Target.FromCell(world, target.Value), false)
			{
				SuppressVisualFeedback = true
			});
			pendingRallyBarracksId = barracks.Actor.ActorID;
			pendingRallyTarget = target.Value;
			nextRallyRetryTick = world.WorldTick + Math.Max(150, Info.RequestInterval);
			LogDecision("{0} requested opening rally for first {1} at {2} beside {3} at {4}; " +
				"target={5}, distance={6}, direction={7}.", player, barracks.Actor.Info.Name, barracks.Actor.Location,
				yard.Info.Name, yard.Location, target.Value, targetDistance, targetDirection);
		}

		bool IsLegalRallyCell(CPos cell)
		{
			return world.Map.Contains(cell) && !world.ActorMap.GetActorsAt(cell)
				.Any(a => a.Info.HasTraitInfo<BuildingInfo>());
		}

		bool IBotRallyPointManager.ManagesRallyPoint(Actor producer)
		{
			if (IsTraitDisabled || producer.Owner != player || !Info.BarracksTypes.Contains(producer.Info.Name))
				return false;

			var firstBarracks = world.Actors
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead && Info.BarracksTypes.Contains(a.Info.Name))
				.OrderBy(a => a.ActorID).FirstOrDefault();
			return firstBarracks == producer;
		}

		void LogDecision(string format, params object[] args)
		{
			AIUtils.BotDebug(format, args);
			if (Info.DebugLogging)
				Log.Write("debug", "AI opening garrison: " + format, args);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("NextRequestTick", FieldSaver.FormatValue(nextRequestTick)),
				new MiniYamlNode("PendingRallyBarracksId", FieldSaver.FormatValue(pendingRallyBarracksId)),
				new MiniYamlNode("PendingRallyTarget", FieldSaver.FormatValue(pendingRallyTarget)),
				new MiniYamlNode("RallyOrderIssued", FieldSaver.FormatValue(rallyOrderIssued)),
				new MiniYamlNode("CompletionLogged", FieldSaver.FormatValue(completionLogged)),
				new MiniYamlNode("EmergencyBurstRequests", FieldSaver.FormatValue(emergencyBurstRequests))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var nextRequestNode = data.FirstOrDefault(n => n.Key == "NextRequestTick");
			if (nextRequestNode != null)
				nextRequestTick = FieldLoader.GetValue<int>("NextRequestTick", nextRequestNode.Value.Value);

			var pendingBarracksNode = data.FirstOrDefault(n => n.Key == "PendingRallyBarracksId");
			if (pendingBarracksNode != null)
				pendingRallyBarracksId = FieldLoader.GetValue<uint>("PendingRallyBarracksId", pendingBarracksNode.Value.Value);

			var pendingTargetNode = data.FirstOrDefault(n => n.Key == "PendingRallyTarget");
			if (pendingTargetNode != null)
				pendingRallyTarget = FieldLoader.GetValue<CPos>("PendingRallyTarget", pendingTargetNode.Value.Value);

			var rallyNode = data.FirstOrDefault(n => n.Key == "RallyOrderIssued");
			if (rallyNode != null)
				rallyOrderIssued = FieldLoader.GetValue<bool>("RallyOrderIssued", rallyNode.Value.Value);

			var completionNode = data.FirstOrDefault(n => n.Key == "CompletionLogged");
			if (completionNode != null)
				completionLogged = FieldLoader.GetValue<bool>("CompletionLogged", completionNode.Value.Value);

			var emergencyNode = data.FirstOrDefault(n => n.Key == "EmergencyBurstRequests");
			if (emergencyNode != null)
				emergencyBurstRequests = FieldLoader.GetValue<int>("EmergencyBurstRequests", emergencyNode.Value.Value);
		}
	}
}
