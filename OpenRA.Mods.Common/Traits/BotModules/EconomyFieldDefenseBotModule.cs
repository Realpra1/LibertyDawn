#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Keeps a bounded number of simple guard squads near active economy harvesters.")]
	public class EconomyFieldDefenseBotModuleInfo : ConditionalTraitInfo
	{
		public readonly string[] RequiredPrerequisites = Array.Empty<string>();
		public readonly HashSet<string> HarvesterTypes = new HashSet<string>();
		public readonly HashSet<string> TankTypes = new HashSet<string>();
		public readonly HashSet<string> InfantryTypes = new HashSet<string>();
		public readonly HashSet<string> AntiAirTypes = new HashSet<string>();
		public readonly int TanksPerHarvester = 1;
		public readonly int InfantryPerHarvester = 2;
		public readonly int AntiAirPerHarvester = 1;

		[Desc("Maximum number of independent economy guard squads.")]
		public readonly int MaximumGuardSquads = 5;

		[Desc("Game-time seconds between recruitment and production-request updates.")]
		public readonly int ReinforcementIntervalSeconds = 10;

		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (RequiredPrerequisites.Length == 0 || HarvesterTypes.Count == 0 || TankTypes.Count == 0 ||
				InfantryTypes.Count == 0 || AntiAirTypes.Count == 0 || TanksPerHarvester <= 0 ||
				InfantryPerHarvester <= 0 || AntiAirPerHarvester <= 0 || MaximumGuardSquads <= 0 ||
				ReinforcementIntervalSeconds <= 0)
				throw new YamlException("Economy field-defense prerequisites, actor types, squad counts, and intervals must be configured and valid.");

			foreach (var actorType in HarvesterTypes.Concat(TankTypes).Concat(InfantryTypes).Concat(AntiAirTypes))
				if (!rules.Actors.ContainsKey(actorType))
					throw new YamlException($"Economy field-defense actor '{actorType}' does not exist.");
		}

		public override object Create(ActorInitializer init) { return new EconomyFieldDefenseBotModule(init.Self, this); }
	}

	public sealed class EconomyFieldDefenseBotModule : ConditionalTrait<EconomyFieldDefenseBotModuleInfo>,
		IBotEnabled, IBotTick, IBotUnitReservations, IBotRespondToAttack, IGameSaveTraitData, IAdvancedBotTick
	{
		const string ProductionRequestOwner = "EconomyFieldDefense";

		sealed class GuardSquad
		{
			public readonly uint HarvesterId;
			public CPos Anchor;
			public readonly HashSet<uint> Tanks = new HashSet<uint>();
			public readonly HashSet<uint> Infantry = new HashSet<uint>();
			public readonly HashSet<uint> AntiAir = new HashSet<uint>();
			public int LastResponseTick = int.MinValue;

			public GuardSquad(uint harvesterId, CPos anchor)
			{
				HarvesterId = harvesterId;
				Anchor = anchor;
			}

			public IEnumerable<uint> Members => Tanks.Concat(Infantry).Concat(AntiAir);
		}

		readonly World world;
		readonly Player player;
		readonly List<GuardSquad> squads = new List<GuardSquad>();
		readonly HashSet<uint> reserved = new HashSet<uint>();
		IBot bot;
		IResourceLayer resourceLayer;
		TechTree techTree;
		IUnassignedCombatUnitRegistry unassignedCombatUnits;
		IBotRequestOwnedUnitProduction[] productionRequesters;
		IBotRequestOwnedUnitProduction productionRequester;
		bool advancedBehaviorEnabled = true;
		int maintenanceTicks = 1;

		public EconomyFieldDefenseBotModule(Actor self, EconomyFieldDefenseBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
			world.ActorAdded += ActorAdded;
		}

		protected override void Created(Actor self)
		{
			resourceLayer = world.WorldActor.Trait<IResourceLayer>();
			techTree = player.PlayerActor.Trait<TechTree>();
			unassignedCombatUnits = player.PlayerActor.Trait<IUnassignedCombatUnitRegistry>();
			productionRequesters = player.PlayerActor.TraitsImplementing<IBotRequestOwnedUnitProduction>().ToArray();
			maintenanceTicks = InitialMaintenanceDelay();
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self) { maintenanceTicks = InitialMaintenanceDelay(); }

		protected override void TraitDisabled(Actor self) { ClearState("bot condition disabled"); }

		void IBotEnabled.BotEnabled(IBot enabledBot) { bot = enabledBot; }

		bool IBotUnitReservations.IsUnitReserved(Actor actor)
		{
			return actor != null && reserved.Contains(actor.ActorID);
		}

		string IAdvancedBotTick.FailsafeModuleId => "EconomyFieldDefenseBotModule";

		void IAdvancedBotTick.SetAdvancedBehaviorEnabled(bool enabled)
		{
			if (advancedBehaviorEnabled == enabled)
				return;

			advancedBehaviorEnabled = enabled;
			if (!enabled)
				ClearState("failsafe degraded");
			else
				maintenanceTicks = 1;
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (IsTraitDisabled || !advancedBehaviorEnabled || player.WinState != WinState.Undefined || --maintenanceTicks > 0)
				return;

			maintenanceTicks = ReinforcementIntervalTicks();
			if (!techTree.HasPrerequisites(Info.RequiredPrerequisites))
			{
				ClearState("economy capability unavailable");
				return;
			}

			MaintainSquads();
		}

		void IBotRespondToAttack.RespondToAttack(IBot enabledBot, Actor self, AttackInfo attack)
		{
			if (IsTraitDisabled || !advancedBehaviorEnabled || player.WinState != WinState.Undefined ||
				self == null || self.Disposed || self.Owner != player || !Info.HarvesterTypes.Contains(self.Info.Name) ||
				!EconomyFieldDefensePolicy.HasActionableAttack(attack))
				return;

			var attacker = attack.Attacker;
			if (!IsEnemySpatialTarget(attacker))
				return;

			var squad = squads.FirstOrDefault(s => s.HarvesterId == self.ActorID) ??
				squads.OrderBy(s => (world.Map.CenterOfCell(s.Anchor) - self.CenterPosition).HorizontalLengthSquared)
					.ThenBy(s => s.HarvesterId).FirstOrDefault();

			// A single impact may damage several harvesters or notify through several warheads.
			// Queue only one grouped response for this squad in the current world tick.
			if (squad == null || squad.LastResponseTick == world.WorldTick)
				return;

			squad.LastResponseTick = world.WorldTick;
			QueueAttackMove(squad.Members.Select(world.GetActorById).Where(IsOwnedUsable), attacker.Location);

			Debug("response harvester={0} attacker={1}#{2} guards={3}", self.ActorID,
				attacker.Info.Name, attacker.ActorID, squad.Members.Count());
		}

		void MaintainSquads()
		{
			productionRequester = productionRequesters.FirstOrDefault(Exts.IsTraitEnabled);
			PruneAndRefreshSquads();
			CreateMissingSquads();

			var idle = (unassignedCombatUnits?.UnassignedActors ?? Array.Empty<Actor>())
				.Where(IsOwnedUsable).OrderBy(a => a.ActorID).ToList();
			var recruited = new HashSet<uint>();
			foreach (var squad in squads.OrderBy(s => s.HarvesterId))
			{
				FillRole(squad, squad.Tanks, Info.TankTypes, Info.TanksPerHarvester, idle, recruited);
				FillRole(squad, squad.Infantry, Info.InfantryTypes, Info.InfantryPerHarvester, idle, recruited);
				FillRole(squad, squad.AntiAir, Info.AntiAirTypes, Info.AntiAirPerHarvester, idle, recruited);
			}

			if (recruited.Count > 0)
				unassignedCombatUnits?.ClaimActors(recruited.Select(world.GetActorById).Where(a => a != null));

			reserved.Clear();
			foreach (var squad in squads)
				reserved.UnionWith(squad.Members);

			foreach (var squad in squads.OrderBy(s => s.HarvesterId))
			{
				var guards = squad.Members.Select(world.GetActorById).Where(IsOwnedUsable).OrderBy(a => a.ActorID).ToArray();
				var newGuards = guards.Where(a => recruited.Contains(a.ActorID)).ToArray();
				if (newGuards.Length > 0)
					QueueAttackMove(newGuards, squad.Anchor);
				else if (guards.Length > 0 && guards.All(a => a.IsIdle))
					QueueAttackMove(guards, squad.Anchor);
			}

			UpdateProductionRequests();
			Debug("maintenance squads={0} guards={1} idle={2} next-seconds={3}", squads.Count,
				reserved.Count, idle.Count, Info.ReinforcementIntervalSeconds);
		}

		void ActorAdded(Actor actor)
		{
			if (actor == null || !IsGuardType(actor.Info.Name))
				return;

			// Claim a produced replacement at frame end, after its owner and combat traits
			// are initialized and before the ordinary squad manager can adopt it next tick.
			world.AddFrameEndTask(w =>
			{
				if (bot == null || techTree == null || unassignedCombatUnits == null ||
					IsTraitDisabled || !advancedBehaviorEnabled ||
					!techTree.HasPrerequisites(Info.RequiredPrerequisites) || !IsOwnedUsable(actor))
					return;

				foreach (var squad in squads.OrderBy(s => s.HarvesterId))
				{
					HashSet<uint> role = null;
					if (Info.TankTypes.Contains(actor.Info.Name) && squad.Tanks.Count < Info.TanksPerHarvester)
						role = squad.Tanks;
					else if (Info.InfantryTypes.Contains(actor.Info.Name) && squad.Infantry.Count < Info.InfantryPerHarvester)
						role = squad.Infantry;
					else if (Info.AntiAirTypes.Contains(actor.Info.Name) && squad.AntiAir.Count < Info.AntiAirPerHarvester)
						role = squad.AntiAir;

					if (role == null)
						continue;

					role.Add(actor.ActorID);
					reserved.Add(actor.ActorID);
					unassignedCombatUnits.ClaimActors(new[] { actor });
					QueueAttackMove(new[] { actor }, squad.Anchor);
					Debug("claimed produced guard={0}#{1} harvester={2}", actor.Info.Name,
						actor.ActorID, squad.HarvesterId);
					return;
				}
			});
		}

		void PruneAndRefreshSquads()
		{
			foreach (var squad in squads.OrderBy(s => s.HarvesterId).ToArray())
			{
				var harvester = world.GetActorById(squad.HarvesterId);
				if (!IsOwnedHarvester(harvester))
				{
					ReleaseSquad(squad, "harvester unavailable");
					continue;
				}

				if (IsOnTiberium(harvester))
					squad.Anchor = harvester.Location;

				PruneRole(squad.Tanks, Info.TankTypes, Info.TanksPerHarvester);
				PruneRole(squad.Infantry, Info.InfantryTypes, Info.InfantryPerHarvester);
				PruneRole(squad.AntiAir, Info.AntiAirTypes, Info.AntiAirPerHarvester);
			}

			foreach (var extra in squads.OrderBy(s => s.HarvesterId).Skip(Info.MaximumGuardSquads).ToArray())
				ReleaseSquad(extra, "squad cap");
		}

		void CreateMissingSquads()
		{
			var assignedHarvesters = squads.Select(s => s.HarvesterId).ToHashSet();
			foreach (var harvester in world.ActorsWithTrait<Harvester>()
				.Where(p => IsOwnedHarvester(p.Actor) && IsOnTiberium(p.Actor) && !assignedHarvesters.Contains(p.Actor.ActorID))
				.Select(p => p.Actor).OrderBy(a => a.ActorID))
			{
				if (squads.Count >= Info.MaximumGuardSquads)
					break;

				squads.Add(new GuardSquad(harvester.ActorID, harvester.Location));
				assignedHarvesters.Add(harvester.ActorID);
				Debug("created squad harvester={0} anchor={1}", harvester.ActorID, harvester.Location);
			}
		}

		void PruneRole(HashSet<uint> members, HashSet<string> types, int target)
		{
			var release = members.Where(id => !IsOwnedRole(world.GetActorById(id), types))
				.Concat(members.Where(id => IsOwnedRole(world.GetActorById(id), types)).OrderBy(id => id).Skip(target))
				.Distinct().ToArray();
			foreach (var id in release)
			{
				members.Remove(id);
				reserved.Remove(id);
				var actor = world.GetActorById(id);
				if (IsOwnedUsable(actor))
					unassignedCombatUnits?.RegisterReleasedActors(new[] { actor });
			}
		}

		void FillRole(GuardSquad squad, HashSet<uint> members, HashSet<string> types, int target,
			List<Actor> idle, HashSet<uint> recruited)
		{
			while (members.Count < target)
			{
				var actor = idle.FirstOrDefault(a => types.Contains(a.Info.Name));
				if (actor == null)
					return;

				idle.Remove(actor);
				members.Add(actor.ActorID);
				recruited.Add(actor.ActorID);
				Debug("recruited guard={0}#{1} harvester={2}", actor.Info.Name, actor.ActorID, squad.HarvesterId);
			}
		}

		void UpdateProductionRequests()
		{
			if (productionRequester == null || bot == null)
				return;

			if (squads.Count == 0)
			{
				CancelProductionRequests("no active guard squads");
				return;
			}

			UpdateRoleProduction(Info.TankTypes, squads.Count * Info.TanksPerHarvester,
				squads.Sum(s => s.Tanks.Count));
			UpdateRoleProduction(Info.InfantryTypes, squads.Count * Info.InfantryPerHarvester,
				squads.Sum(s => s.Infantry.Count));
			UpdateRoleProduction(Info.AntiAirTypes, squads.Count * Info.AntiAirPerHarvester,
				squads.Sum(s => s.AntiAir.Count));
		}

		void UpdateRoleProduction(HashSet<string> types, int target, int assigned)
		{
			var actorType = types.OrderBy(t => t, StringComparer.Ordinal).First();
			var owned = productionRequester.RequestedProductionCount(bot, ProductionRequestOwner, actorType);
			var queued = world.ActorsWithTrait<ProductionQueue>().Where(q => q.Actor.Owner == player)
				.Sum(q => q.Trait.AllQueued().Count(item => item.Item == actorType));
			var needed = EconomyFieldDefensePolicy.MissingProductionDemand(target, assigned, queued, owned);

			if (target <= assigned + queued && owned > 0)
			{
				productionRequester.CancelRequestedUnitProduction(bot, ProductionRequestOwner, actorType);
				return;
			}

			if (needed == 0 || !HasBuildableQueue(actorType))
				return;

			for (var i = 0; i < needed; i++)
				productionRequester.RequestUnitProduction(bot, ProductionRequestOwner, actorType);
		}

		bool HasBuildableQueue(string actorType)
		{
			if (!world.Map.Rules.Actors.TryGetValue(actorType, out var actorInfo))
				return false;

			var buildable = actorInfo.TraitInfoOrDefault<BuildableInfo>();
			return buildable != null && buildable.Queue.Any(queueType => AIUtils.FindQueues(player, queueType)
				.Any(queue => queue.BuildableItems().Any(item => item.Name == actorType)));
		}

		void CancelProductionRequests(string reason)
		{
			if (bot == null)
				return;

			foreach (var requester in productionRequesters ?? Array.Empty<IBotRequestOwnedUnitProduction>())
				foreach (var actorType in Info.TankTypes.Concat(Info.InfantryTypes).Concat(Info.AntiAirTypes)
					.Distinct().OrderBy(t => t, StringComparer.Ordinal))
					if (requester.RequestedProductionCount(bot, ProductionRequestOwner, actorType) > 0)
						requester.CancelRequestedUnitProduction(bot, ProductionRequestOwner, actorType);

			Debug("cancelled production reason={0}", reason);
		}

		void ReleaseSquad(GuardSquad squad, string reason)
		{
			var actors = squad.Members.Select(world.GetActorById).Where(IsOwnedUsable).ToArray();
			foreach (var actor in actors)
				reserved.Remove(actor.ActorID);
			unassignedCombatUnits?.RegisterReleasedActors(actors);
			squads.Remove(squad);
			Debug("released squad harvester={0} reason={1}", squad.HarvesterId, reason);
		}

		void ClearState(string reason)
		{
			var actors = reserved.Select(world.GetActorById).Where(IsOwnedUsable).ToArray();
			reserved.Clear();
			unassignedCombatUnits?.RegisterReleasedActors(actors);
			squads.Clear();
			CancelProductionRequests(reason);
		}

		void QueueAttackMove(IEnumerable<Actor> actors, CPos destination)
		{
			var group = actors.Where(IsOwnedUsable).OrderBy(a => a.ActorID).ToArray();
			if (bot != null && group.Length > 0)
				bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(world, destination), false,
					groupedActors: group));
		}

		bool IsOwnedHarvester(Actor actor)
		{
			return IsOwnedUsable(actor) && Info.HarvesterTypes.Contains(actor.Info.Name) && actor.OccupiesSpace != null;
		}

		bool IsOwnedRole(Actor actor, HashSet<string> types)
		{
			return IsOwnedUsable(actor) && types.Contains(actor.Info.Name);
		}

		bool IsGuardType(string actorType)
		{
			return Info.TankTypes.Contains(actorType) || Info.InfantryTypes.Contains(actorType) ||
				Info.AntiAirTypes.Contains(actorType);
		}

		bool IsOwnedUsable(Actor actor)
		{
			return actor != null && !actor.Disposed && actor.Owner == player && actor.IsInWorld && !actor.IsDead;
		}

		bool IsOnTiberium(Actor actor)
		{
			return actor?.OccupiesSpace != null && resourceLayer.GetResource(actor.Location).Type != null;
		}

		bool IsEnemySpatialTarget(Actor actor)
		{
			return actor != null && !actor.Disposed && actor.IsInWorld && !actor.IsDead && actor.Owner != null &&
				actor.OccupiesSpace != null && player.RelationshipWith(actor.Owner) == PlayerRelationship.Enemy;
		}

		int ReinforcementIntervalTicks()
		{
			return EconomyFieldDefensePolicy.ReinforcementIntervalTicks(
				Info.ReinforcementIntervalSeconds, world.Timestep);
		}

		int InitialMaintenanceDelay()
		{
			var interval = ReinforcementIntervalTicks();
			var count = Math.Max(1, world.LobbyInfo.Clients.Count);
			return 1 + interval * Math.Max(0, player.ClientIndex) / count;
		}

		void Debug(string format, params object[] args)
		{
			if (!Info.DebugLogging)
				return;

			Log.Write("debug", "AI economy field defense [{0}] tick={1}: {2}", player.PlayerName,
				world.WorldTick, string.Format(format, args));
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			var nodes = squads.OrderBy(s => s.HarvesterId).Select(squad =>
				new MiniYamlNode("Field", FieldSaver.FormatValue(squad.HarvesterId), new List<MiniYamlNode>
				{
					new MiniYamlNode("Station", FieldSaver.FormatValue(squad.Anchor)),
					new MiniYamlNode("Tanks", FieldSaver.FormatValue(squad.Tanks.OrderBy(id => id).ToArray())),
					new MiniYamlNode("Infantry", FieldSaver.FormatValue(squad.Infantry.OrderBy(id => id).ToArray())),
					new MiniYamlNode("AntiAir", FieldSaver.FormatValue(squad.AntiAir.OrderBy(id => id).ToArray())),
					new MiniYamlNode("LastResponseTick", FieldSaver.FormatValue(squad.LastResponseTick))
				})).ToList();

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("EconomyFieldDefenseMaintenanceTicks", FieldSaver.FormatValue(maintenanceTicks)),
				new MiniYamlNode("EconomyFieldDefenseFields", "", nodes)
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			squads.Clear();
			reserved.Clear();
			var maintenance = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseMaintenanceTicks");
			maintenanceTicks = maintenance == null ? 1 : Math.Min(ReinforcementIntervalTicks(),
				Math.Max(1, FieldLoader.GetValue<int>(maintenance.Key, maintenance.Value.Value)));

			var fields = data.FirstOrDefault(n => n.Key == "EconomyFieldDefenseFields");
			if (fields == null)
				return;

			foreach (var node in fields.Value.Nodes.Where(n => n.Key == "Field").Take(Info.MaximumGuardSquads))
			{
				var harvesterId = FieldLoader.GetValue<uint>(node.Key, node.Value.Value);
				var station = node.Value.Nodes.FirstOrDefault(n => n.Key == "Station");
				if (station == null || squads.Any(s => s.HarvesterId == harvesterId) ||
					!IsOwnedHarvester(world.GetActorById(harvesterId)))
					continue;

				var squad = new GuardSquad(harvesterId,
					FieldLoader.GetValue<CPos>(station.Key, station.Value.Value));
				LoadRole(squad.Tanks, node, "Tanks", Info.TankTypes, Info.TanksPerHarvester);
				LoadRole(squad.Infantry, node, "Infantry", Info.InfantryTypes, Info.InfantryPerHarvester);
				LoadRole(squad.AntiAir, node, "AntiAir", Info.AntiAirTypes, Info.AntiAirPerHarvester);
				var response = node.Value.Nodes.FirstOrDefault(n => n.Key == "LastResponseTick");
				if (response != null)
					squad.LastResponseTick = FieldLoader.GetValue<int>(response.Key, response.Value.Value);
				squads.Add(squad);
				reserved.UnionWith(squad.Members);
			}

			unassignedCombatUnits?.ClaimActors(reserved.Select(world.GetActorById).Where(a => a != null));
		}

		void LoadRole(HashSet<uint> destination, MiniYamlNode parent, string key,
			HashSet<string> types, int maximum)
		{
			var node = parent.Value.Nodes.FirstOrDefault(n => n.Key == key);
			if (node != null)
				foreach (var id in FieldLoader.GetValue<uint[]>(node.Key, node.Value.Value)
					.Distinct().OrderBy(id => id).Take(maximum))
					if (!reserved.Contains(id) && IsOwnedRole(world.GetActorById(id), types))
					{
						destination.Add(id);
						reserved.Add(id);
					}
		}
	}
}
