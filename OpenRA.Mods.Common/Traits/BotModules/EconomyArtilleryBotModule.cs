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
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Coordinates one economy-technology MRLS battery and a small value-based defensive screen.")]
	public class EconomyArtilleryBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Live prerequisites required to activate this capability.")]
		public readonly string[] RequiredPrerequisites = Array.Empty<string>();

		public readonly HashSet<string> ArtilleryTypes = new HashSet<string>();
		public readonly HashSet<string> AntiAirTypes = new HashSet<string>();
		public readonly HashSet<string> TankTypes = new HashSet<string>();
		public readonly HashSet<string> InfantryTypes = new HashSet<string>();
		public readonly int EscortValuePercent = 5;
		public readonly int ScanInterval = 25;
		public readonly int OrderInterval = 25;
		public readonly int MaximumTargetCandidates = 48;
		public readonly int DefenderDistanceCells = 2;
		public readonly int ScoutSafetyRadiusCells = 5;
		public readonly int ScoutRangeMarginCells = 1;
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (RequiredPrerequisites.Length == 0 || ArtilleryTypes.Count == 0 || AntiAirTypes.Count == 0 ||
				TankTypes.Count == 0 || InfantryTypes.Count == 0 || EscortValuePercent < 0 ||
				ScanInterval <= 0 || OrderInterval <= 0 || MaximumTargetCandidates <= 0 ||
				DefenderDistanceCells < 0 || ScoutSafetyRadiusCells < 0 || ScoutRangeMarginCells < 0)
				throw new YamlException("Economy artillery prerequisites, actor types, intervals, bounds, and distances must be configured and valid.");

			foreach (var actorType in ArtilleryTypes.Concat(AntiAirTypes).Concat(TankTypes).Concat(InfantryTypes))
				if (!rules.Actors.ContainsKey(actorType))
					throw new YamlException($"Economy artillery actor '{actorType}' does not exist.");
		}

		public override object Create(ActorInitializer init) { return new EconomyArtilleryBotModule(init.Self, this); }
	}

	public class EconomyArtilleryBotModule : ConditionalTrait<EconomyArtilleryBotModuleInfo>,
		IBotEnabled, IBotTick, IBotUnitReservations, IGameSaveTraitData
	{
		static readonly BitSet<TargetableType> GroundTargetTypes = new BitSet<TargetableType>("Ground");

		readonly World world;
		readonly Player player;
		readonly HashSet<uint> reserved = new HashSet<uint>();
		readonly HashSet<uint> artillery = new HashSet<uint>();
		readonly HashSet<uint> antiAir = new HashSet<uint>();
		readonly HashSet<uint> tanks = new HashSet<uint>();
		readonly HashSet<uint> infantry = new HashSet<uint>();
		IBot bot;
		IBotUnitReservations[] otherReservations;
		IBotTransportReservations[] transportReservations;
		IBotRequestUnitProduction[] productionRequesters;
		SquadManagerBotModule squadManager;
		DomainIndex domainIndex;
		TechTree techTree;
		Actor target;
		CPos lastCenter;
		int scanTicks;
		int lastOrderTick;
		bool hasLastCenter;
		bool ownsAntiAirRequest;
		string lastComposition;

		public EconomyArtilleryBotModule(Actor self, EconomyArtilleryBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			techTree = player.PlayerActor.Trait<TechTree>();
			domainIndex = world.WorldActor.Trait<DomainIndex>();
			otherReservations = player.PlayerActor.TraitsImplementing<IBotUnitReservations>()
				.Where(r => !ReferenceEquals(r, this)).ToArray();
			transportReservations = player.PlayerActor.TraitsImplementing<IBotTransportReservations>().ToArray();
			productionRequesters = player.PlayerActor.TraitsImplementing<IBotRequestUnitProduction>().ToArray();
			RefreshSquadManager();
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self)
		{
			// Reserve newly available launchers before the ordinary squad manager scans them.
			scanTicks = 1;
		}

		protected override void TraitDisabled(Actor self)
		{
			CancelOwnedAntiAirRequest();
			ClearState("bot condition disabled");
		}

		void IBotEnabled.BotEnabled(IBot enabledBot) { bot = enabledBot; }

		bool IBotUnitReservations.IsUnitReserved(Actor actor)
		{
			return actor != null && reserved.Contains(actor.ActorID);
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (IsTraitDisabled || player.WinState != WinState.Undefined || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			if (!techTree.HasPrerequisites(Info.RequiredPrerequisites))
			{
				CancelOwnedAntiAirRequest();
				ClearState("economy capability unavailable");
				return;
			}

			RefreshSquadManager();
			Rebalance();
			if (artillery.Count == 0)
			{
				CancelOwnedAntiAirRequest();
				target = null;
				return;
			}

			RequestFirstAntiAir();
			UpdateOrders();
		}

		void RefreshSquadManager()
		{
			if (squadManager == null || squadManager.IsTraitDisabled)
				squadManager = player.PlayerActor.TraitsImplementing<SquadManagerBotModule>()
					.FirstOrDefault(m => !m.IsTraitDisabled);
		}

		bool IsOwnedUsable(Actor actor)
		{
			return actor != null && actor.Owner == player && actor.IsInWorld && !actor.IsDead;
		}

		bool IsClaimable(Actor actor)
		{
			return IsOwnedUsable(actor) &&
				!(transportReservations?.Any(r => r.IsTransportReserved(actor)) ?? false) &&
				!(otherReservations?.Any(r => r.IsUnitReserved(actor)) ?? false) &&
				!(squadManager?.IsUnitProtectingBase(actor) ?? false);
		}

		List<Actor> Claimable(HashSet<string> types)
		{
			return world.Actors.Where(a => types.Contains(a.Info.Name) && IsClaimable(a))
				.OrderBy(a => a.ActorID).ToList();
		}

		void Rebalance()
		{
			var launchers = Claimable(Info.ArtilleryTypes);
			var artilleryValue = launchers.Sum(ActorValue);
			var selectedAntiAir = SelectEscorts(Claimable(Info.AntiAirTypes), antiAir,
				artilleryValue, Info.EscortValuePercent, launchers.Count > 0 ? 1 : 0);
			var selectedTanks = SelectEscorts(Claimable(Info.TankTypes), tanks,
				artilleryValue, Info.EscortValuePercent, 0);
			var selectedInfantry = SelectEscorts(Claimable(Info.InfantryTypes), infantry,
				artilleryValue, Info.EscortValuePercent, 0);

			artillery.Clear();
			artillery.UnionWith(launchers.Select(a => a.ActorID));
			Replace(antiAir, selectedAntiAir);
			Replace(tanks, selectedTanks);
			Replace(infantry, selectedInfantry);
			reserved.Clear();
			reserved.UnionWith(artillery);
			reserved.UnionWith(antiAir);
			reserved.UnionWith(tanks);
			reserved.UnionWith(infantry);

			var rejection = Info.DebugLogging ? RejectionSummary() : null;
			var composition = $"mlrs={artillery.Count}/{artilleryValue} msam={antiAir.Count} tank={tanks.Count} rifle={infantry.Count}" +
				(rejection == null ? "" : " " + rejection);
			if (composition != lastComposition)
			{
				Debug("composition {0}", composition);
				lastComposition = composition;
			}
		}

		string RejectionSummary()
		{
			var relevantTypes = new HashSet<string>(Info.ArtilleryTypes.Concat(Info.AntiAirTypes)
				.Concat(Info.TankTypes).Concat(Info.InfantryTypes));
			var candidates = world.Actors.Where(a => IsOwnedUsable(a) && relevantTypes.Contains(a.Info.Name)).ToList();
			var transport = candidates.Count(a => transportReservations.Any(r => r.IsTransportReserved(a)));
			var other = candidates.Count(a => otherReservations.Any(r => r.IsUnitReserved(a)));
			var protection = candidates.Count(a => squadManager?.IsUnitProtectingBase(a) ?? false);
			return $"candidates={candidates.Count} rejected=transport:{transport}/other:{other}/protection:{protection}";
		}

		static void Replace(HashSet<uint> destination, IEnumerable<Actor> actors)
		{
			destination.Clear();
			destination.UnionWith(actors.Select(a => a.ActorID));
		}

		static int ActorValue(Actor actor)
		{
			return Math.Max(1, actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1);
		}

		List<Actor> SelectEscorts(List<Actor> eligible, HashSet<uint> previous,
			int artilleryValue, int percent, int minimum)
		{
			if (eligible.Count == 0)
				return new List<Actor>();

			var cost = ActorValue(eligible[0]);
			var count = EconomyArtilleryPolicy.EscortCount(artilleryValue, cost, percent, eligible.Count, minimum);
			return eligible.OrderByDescending(a => previous.Contains(a.ActorID)).ThenBy(a => a.ActorID).Take(count).ToList();
		}

		void UpdateOrders()
		{
			var launchers = Actors(artillery);
			if (launchers.Count == 0)
				return;

			var centerPosition = launchers.Select(a => a.CenterPosition).Average();
			lastCenter = world.Map.CellContaining(centerPosition);
			hasLastCenter = true;

			var previousTarget = target;
			var selected = SelectTarget(launchers, centerPosition);
			var targetChanged = selected != previousTarget;
			target = selected;
			if (targetChanged)
				Debug("target transition {0} -> {1}", TargetStatus(previousTarget), TargetStatus(target));

			if (!EconomyArtilleryPolicy.ShouldIssueOrders(targetChanged, world.WorldTick, lastOrderTick, Info.OrderInterval))
				return;

			lastOrderTick = world.WorldTick;
			if (target == null)
			{
				StopLaunchers(launchers);
				PositionDefenders(centerPosition, null, null);
				return;
			}

			var visible = IsVisible(target);
			Actor scout = null;
			if (!visible)
			{
				StopLaunchers(launchers);
				scout = TryScout(target, centerPosition);
			}
			else
			{
				var armed = launchers.Where(HasAmmo).ToArray();
				foreach (var empty in launchers.Where(a => !HasAmmo(a)))
					bot.QueueOrder(new Order("Stop", empty, false));

				if (armed.Length > 0)
					bot.QueueOrder(new Order("Attack", null, Target.FromActor(target), false, groupedActors: armed));
			}

			PositionDefenders(centerPosition, target, scout);
			Debug("orders target={0}#{1} visible={2} armed={3}/{4} center={5} scout={6}",
				target.Info.Name, target.ActorID, visible, launchers.Count(HasAmmo), launchers.Count,
				lastCenter, scout == null ? "none" : scout.Info.Name + "#" + scout.ActorID);
		}

		static string TargetStatus(Actor actor)
		{
			return actor == null ? "none" :
				$"{actor.Info.Name}#{actor.ActorID}[in-world={actor.IsInWorld},dead={actor.IsDead}]";
		}

		Actor SelectTarget(List<Actor> launchers, WPos center)
		{
			if (target != null && IsEnemyTarget(target) && launchers.Any(a =>
				StateBase.CanAttackTarget(a, target) && IsReachable(a, target.Location)))
				return target;

			var candidates = world.Actors.Where(a => IsEnemyTarget(a) && IsVisible(a) &&
				launchers.Any(l => StateBase.CanAttackTarget(l, a)) && IsReachable(launchers[0], a.Location))
				.OrderBy(a => (a.CenterPosition - center).HorizontalLengthSquared).ThenBy(a => a.ActorID)
				.Take(Info.MaximumTargetCandidates);

			return candidates
				.Select(a => new
				{
					Actor = a,
					Score = EconomyArtilleryPolicy.TargetScore(TargetPriority(a), ActorValue(a),
						(a.CenterPosition - center).HorizontalLengthSquared)
				})
				.Where(c => c.Score > 0)
				.OrderByDescending(c => c.Score).ThenBy(c => c.Actor.ActorID)
				.Select(c => c.Actor).FirstOrDefault();
		}

		int TargetPriority(Actor actor)
		{
			return EconomyArtilleryPolicy.TargetPriority(actor.Info.HasTraitInfo<BuildingInfo>(),
				actor.Info.HasTraitInfo<AttackBaseInfo>());
		}

		bool IsEnemyTarget(Actor actor)
		{
			return actor != null && actor.IsInWorld && !actor.IsDead &&
				player.RelationshipWith(actor.Owner) == PlayerRelationship.Enemy &&
				!actor.Info.HasTraitInfo<HuskInfo>() && !actor.GetEnabledTargetTypes().IsEmpty;
		}

		bool IsVisible(Actor actor)
		{
			return player.Shroud.IsVisible(actor.Location) && actor.CanBeViewedByPlayer(player);
		}

		bool IsReachable(Actor actor, CPos destination)
		{
			var mobile = actor.TraitOrDefault<Mobile>();
			return mobile != null && domainIndex.IsPassable(actor.Location, destination, mobile.Locomotor);
		}

		static bool HasAmmo(Actor actor)
		{
			var pools = actor.TraitsImplementing<AmmoPool>().ToArray();
			return pools.Length == 0 || pools.All(p => p.HasAmmo);
		}

		void StopLaunchers(IEnumerable<Actor> launchers)
		{
			foreach (var launcher in launchers)
				bot.QueueOrder(new Order("Stop", launcher, false));
		}

		Actor TryScout(Actor hiddenTarget, WPos center)
		{
			foreach (var scout in Actors(infantry))
			{
				var reveal = scout.TraitsImplementing<RevealsShroud>().Where(r => !r.IsTraitDisabled)
					.Select(r => r.Range.Length).DefaultIfEmpty(0).Max();
				var distance = Math.Max(0, reveal - Info.ScoutRangeMarginCells * 1024);
				var destinationPosition = hiddenTarget.CenterPosition +
					AirThreatGeometry.ScaleToLength(center - hiddenTarget.CenterPosition, distance);
				var destination = world.Map.CellContaining(destinationPosition);
				if (!IsReachable(scout, destination) || !IsScoutDestinationSafe(destinationPosition))
					continue;

				bot.QueueOrder(new Order("Move", scout, Target.FromCell(world, destination), false));
				return scout;
			}

			return null;
		}

		bool IsScoutDestinationSafe(WPos destination)
		{
			return !world.FindActorsInCircle(destination, WDist.FromCells(Info.ScoutSafetyRadiusCells)).Any(a =>
				IsEnemyTarget(a) && a.TraitsImplementing<Armament>().Any(arm =>
					!arm.IsTraitDisabled && arm.Weapon.IsValidTarget(GroundTargetTypes)));
		}

		void PositionDefenders(WPos center, Actor guardedTarget, Actor scout)
		{
			var threat = NearestThreat(center);
			var facing = threat?.CenterPosition ?? guardedTarget?.CenterPosition ?? center;
			var screenPosition = center + AirThreatGeometry.ScaleToLength(facing - center, Info.DefenderDistanceCells * 1024);
			var screenCell = world.Map.CellContaining(screenPosition);
			foreach (var defender in Actors(antiAir).Concat(Actors(tanks)).Concat(Actors(infantry)))
			{
				if (defender == scout || !IsReachable(defender, screenCell))
					continue;

				var order = tanks.Contains(defender.ActorID) || infantry.Contains(defender.ActorID) ? "AttackMove" : "Move";
				bot.QueueOrder(new Order(order, defender, Target.FromCell(world, screenCell), false));
			}
		}

		Actor NearestThreat(WPos center)
		{
			return world.FindActorsInCircle(center, WDist.FromCells(12)).Where(a => IsEnemyTarget(a) &&
				a.TraitsImplementing<Armament>().Any(arm => !arm.IsTraitDisabled && arm.Weapon.IsValidTarget(GroundTargetTypes)))
				.OrderBy(a => (a.CenterPosition - center).HorizontalLengthSquared).ThenBy(a => a.ActorID).FirstOrDefault();
		}

		List<Actor> Actors(HashSet<uint> ids)
		{
			return ids.Select(world.GetActorById).Where(IsOwnedUsable).OrderBy(a => a.ActorID).ToList();
		}

		void RequestFirstAntiAir()
		{
			if (antiAir.Count > 0)
			{
				ownsAntiAirRequest = false;
				return;
			}

			var actorType = Info.AntiAirTypes.OrderBy(t => t, StringComparer.Ordinal).First();
			var requester = productionRequesters.FirstOrDefault(Exts.IsTraitEnabled);
			if (requester == null || requester.RequestedProductionCount(bot, actorType) > 0 || IsQueued(actorType) ||
				!HasFreeBuildableQueue(actorType))
				return;

			requester.RequestUnitProduction(bot, actorType);
			ownsAntiAirRequest = true;
			Debug("requested required first anti-air escort {0}", actorType);
		}

		void CancelOwnedAntiAirRequest()
		{
			if (!ownsAntiAirRequest || bot == null)
				return;

			foreach (var actorType in Info.AntiAirTypes)
				foreach (var requester in productionRequesters ?? Array.Empty<IBotRequestUnitProduction>())
					if (requester.RequestedProductionCount(bot, actorType) > 0)
						requester.CancelRequestedUnitProduction(bot, actorType);

			ownsAntiAirRequest = false;
		}

		bool IsQueued(string actorType)
		{
			return world.ActorsWithTrait<ProductionQueue>().Any(q => q.Actor.Owner == player &&
				q.Trait.AllQueued().Any(item => item.Item == actorType));
		}

		bool HasFreeBuildableQueue(string actorType)
		{
			if (!world.Map.Rules.Actors.TryGetValue(actorType, out var actorInfo))
				return false;

			var buildable = actorInfo.TraitInfoOrDefault<BuildableInfo>();
			return buildable != null && buildable.Queue.Any(queueType => AIUtils.FindQueues(player, queueType)
				.Any(queue => !queue.AllQueued().Any() && queue.BuildableItems().Any(item => item.Name == actorType)));
		}

		void ClearState(string reason)
		{
			if (reserved.Count > 0 || target != null)
				Debug("released cluster: {0}", reason);

			reserved.Clear();
			artillery.Clear();
			antiAir.Clear();
			tanks.Clear();
			infantry.Clear();
			target = null;
			hasLastCenter = false;
			lastComposition = null;
		}

		void Debug(string format, params object[] args)
		{
			if (!Info.DebugLogging)
				return;

			var message = string.Format(format, args);
			AIUtils.BotDebug("AI ({0}) economy artillery: {1}", player.ClientIndex, message);
			Log.Write("debug", "AI economy artillery: {0} (client {1}) at tick {2}: {3}",
				player, player.ClientIndex, world.WorldTick, message);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("EconomyArtilleryScanTicks", FieldSaver.FormatValue(scanTicks)),
				new MiniYamlNode("EconomyArtilleryLastOrderTick", FieldSaver.FormatValue(lastOrderTick)),
				new MiniYamlNode("EconomyArtilleryHasCenter", FieldSaver.FormatValue(hasLastCenter)),
				new MiniYamlNode("EconomyArtilleryCenter", FieldSaver.FormatValue(lastCenter)),
				new MiniYamlNode("EconomyArtilleryTarget", FieldSaver.FormatValue(target?.ActorID ?? 0)),
				new MiniYamlNode("EconomyArtilleryOwnsAaRequest", FieldSaver.FormatValue(ownsAntiAirRequest)),
				new MiniYamlNode("EconomyArtilleryLaunchers", FieldSaver.FormatValue(artillery.OrderBy(id => id).ToArray())),
				new MiniYamlNode("EconomyArtilleryAntiAir", FieldSaver.FormatValue(antiAir.OrderBy(id => id).ToArray())),
				new MiniYamlNode("EconomyArtilleryTanks", FieldSaver.FormatValue(tanks.OrderBy(id => id).ToArray())),
				new MiniYamlNode("EconomyArtilleryInfantry", FieldSaver.FormatValue(infantry.OrderBy(id => id).ToArray()))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			foreach (var node in data)
				switch (node.Key)
				{
					case "EconomyArtilleryScanTicks": scanTicks = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyArtilleryLastOrderTick": lastOrderTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyArtilleryHasCenter": hasLastCenter = FieldLoader.GetValue<bool>(node.Key, node.Value.Value); break;
					case "EconomyArtilleryCenter": lastCenter = FieldLoader.GetValue<CPos>(node.Key, node.Value.Value); break;
					case "EconomyArtilleryTarget":
						var targetId = FieldLoader.GetValue<uint>(node.Key, node.Value.Value);
						target = targetId == 0 ? null : world.GetActorById(targetId);
						break;
					case "EconomyArtilleryOwnsAaRequest": ownsAntiAirRequest = FieldLoader.GetValue<bool>(node.Key, node.Value.Value); break;
					case "EconomyArtilleryLaunchers": LoadIds(artillery, node); break;
					case "EconomyArtilleryAntiAir": LoadIds(antiAir, node); break;
					case "EconomyArtilleryTanks": LoadIds(tanks, node); break;
					case "EconomyArtilleryInfantry": LoadIds(infantry, node); break;
				}

			reserved.Clear();
			reserved.UnionWith(artillery);
			reserved.UnionWith(antiAir);
			reserved.UnionWith(tanks);
			reserved.UnionWith(infantry);
		}

		static void LoadIds(HashSet<uint> ids, MiniYamlNode node)
		{
			ids.Clear();
			ids.UnionWith(FieldLoader.GetValue<uint[]>(node.Key, node.Value.Value));
		}
	}
}
