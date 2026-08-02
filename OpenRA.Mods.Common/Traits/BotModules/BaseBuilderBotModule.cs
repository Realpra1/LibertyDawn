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
	[Desc("Manages AI base construction.")]
	public class BaseBuilderBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Tells the AI what building types are considered construction yards.")]
		public readonly HashSet<string> ConstructionYardTypes = new HashSet<string>();

		[Desc("Tells the AI what building types are considered vehicle production facilities.")]
		public readonly HashSet<string> VehiclesFactoryTypes = new HashSet<string>();

		[Desc("Tells the AI what building types are considered refineries.")]
		public readonly HashSet<string> RefineryTypes = new HashSet<string>();

		[Desc("Harvester types used by the conservative refinery-congestion proxy.")]
		public readonly HashSet<string> HarvesterTypes = new HashSet<string>();

		[Desc("When positive, maintain one refinery per this many harvesters, in addition to the normal opening minimum.")]
		public readonly int CongestionHarvestersPerRefinery = 0;

		[Desc("Maximum refineries the congestion proxy may add above the normal minimum.")]
		public readonly int MaximumCongestionRefineries = 0;

		[Desc("Tells the AI what building types are considered power plants.")]
		public readonly HashSet<string> PowerTypes = new HashSet<string>();

		[Desc("Forces AI to treat these buildings as defenses and build them toward enemy.")]
		public readonly HashSet<string> PlaceAsDefenses = new HashSet<string>();

		[Desc("Chance of forced placing as defense.")]
		public readonly int PlaceAsDefenseChance = 50;

		[Desc("Tells the AI what building types are considered infantry production facilities.")]
		public readonly HashSet<string> BarracksTypes = new HashSet<string>();

		[Desc("Tells the AI what building types are considered production facilities.")]
		public readonly HashSet<string> ProductionTypes = new HashSet<string>();

		[Desc("Tells the AI what building types are considered naval production facilities.")]
		public readonly HashSet<string> NavalProductionTypes = new HashSet<string>();

		[Desc("Tells the AI what building types are considered silos (resource storage).")]
		public readonly HashSet<string> SiloTypes = new HashSet<string>();

		[Desc("Use a single coordinator to enforce the configured cross-queue opening sequence.")]
		public readonly bool EnableOpeningPolicy = false;

		[Desc("Opening alternatives, ordered from most to least preferred. Unbuildable alternatives are skipped.")]
		public readonly string[] OpeningPowerTypes = Array.Empty<string>();
		public readonly string[] OpeningBarracksTypes = Array.Empty<string>();
		public readonly string[] OpeningRefineryTypes = Array.Empty<string>();
		public readonly string[] OpeningFactoryTypes = Array.Empty<string>();
		public readonly string[] OpeningHarvesterTypes = Array.Empty<string>();
		public readonly string[] OpeningRadarTypes = Array.Empty<string>();
		public readonly string[] OpeningSiloTypes = Array.Empty<string>();

		[Desc("Mobile construction vehicle requested during the opening and later excess-cash expansion.")]
		public readonly string OpeningMcvType = "mcv";

		public readonly int OpeningHarvesterCount = 5;
		public readonly int OpeningConstructionYardCount = 2;
		public readonly int OpeningUnavailableRetries = 8;

		[Desc("Place the first newly produced combat tower around a construction yard.")]
		public readonly bool FirstCombatTowerNearConstructionYard = false;

		[Desc("Request expansion MCVs above this cash+stored-resource threshold. Zero disables this behavior.")]
		public readonly int ExcessCashExpansionThreshold = 0;
		public readonly int ExcessCashExpansionCooldown = 1500;
		public readonly int ExcessCashConstructionYardTarget = 2;

		[Desc("Stored-resource percentage that triggers silo construction.")]
		public readonly int SiloBuildResourcePercent = 80;

		[Desc("Production queues AI uses for buildings.")]
		public readonly HashSet<string> BuildingQueues = new HashSet<string> { "Building" };

		[Desc("Production queues AI uses for defenses.")]
		public readonly HashSet<string> DefenseQueues = new HashSet<string> { "Defense" };

		[Desc("Minimum distance in cells from center of the base when checking for building placement.")]
		public readonly int MinBaseRadius = 2;

		[Desc("Radius in cells around the center of the base to expand.")]
		public readonly int MaxBaseRadius = 20;

		[Desc("Minimum excess power the AI should try to maintain.")]
		public readonly int MinimumExcessPower = 0;

		[Desc("The targeted excess power the AI tries to maintain cannot rise above this.")]
		public readonly int MaximumExcessPower = 0;

		[Desc("Increase maintained excess power by this amount for every ExcessPowerIncreaseThreshold of base buildings.")]
		public readonly int ExcessPowerIncrement = 0;

		[Desc("Increase maintained excess power by ExcessPowerIncrement for every N base buildings.")]
		public readonly int ExcessPowerIncreaseThreshold = 1;

		[Desc("Number of refineries to build before building a barracks.")]
		public readonly int InititalMinimumRefineryCount = 1;

		[Desc("Number of refineries to build additionally after building a barracks.")]
		public readonly int AdditionalMinimumRefineryCount = 1;

		[Desc("Additional delay (in ticks) between structure production checks when there is no active production.",
			"StructureProductionRandomBonusDelay is added to this.")]
		public readonly int StructureProductionInactiveDelay = 125;

		[Desc("Additional delay (in ticks) added between structure production checks when actively building things.",
			"Note: this should be at least as large as the typical order latency to avoid duplicated build choices.")]
		public readonly int StructureProductionActiveDelay = 25;

		[Desc("A random delay (in ticks) of up to this is added to active/inactive production delays.")]
		public readonly int StructureProductionRandomBonusDelay = 10;

		[Desc("Delay (in ticks) until retrying to build structure after the last 3 consecutive attempts failed.")]
		public readonly int StructureProductionResumeDelay = 1500;

		[Desc("After how many failed attempts to place a structure should AI give up and wait",
			"for StructureProductionResumeDelay before retrying.")]
		public readonly int MaximumFailedPlacementAttempts = 3;

		[Desc("How many randomly chosen cells with resources to check when deciding refinery placement.")]
		public readonly int MaxResourceCellsToCheck = 3;

		[Desc("Delay (in ticks) until rechecking for new BaseProviders.")]
		public readonly int CheckForNewBasesDelay = 1500;

		[Desc("Chance that the AI will place the defenses in the direction of the closest enemy building.")]
		public readonly int PlaceDefenseTowardsEnemyChance = 100;

		[Desc("Minimum range at which to build defensive structures near a combat hotspot.")]
		public readonly int MinimumDefenseRadius = 5;

		[Desc("Maximum range at which to build defensive structures near a combat hotspot.")]
		public readonly int MaximumDefenseRadius = 20;

		[Desc("Try to build another production building if there is too much cash.")]
		public readonly int NewProductionCashThreshold = 5000;

		[Desc("Radius in cells around a factory scanned for rally points by the AI.")]
		public readonly int RallyPointScanRadius = 8;

		[Desc("Radius in cells around each building with ProvideBuildableArea",
			"to check for a 3x3 area of water where naval structures can be built.",
			"Should match maximum adjacency of naval structures.")]
		public readonly int CheckForWaterRadius = 8;

		[Desc("Terrain types which are considered water for base building purposes.")]
		public readonly HashSet<string> WaterTerrainTypes = new HashSet<string> { "Water" };

		[Desc("Building types that are placed as walls using line building instead of being dropped",
			"on a free cell somewhere in the base. Leave empty to disable wall building.")]
		public readonly HashSet<string> WallTypes = new HashSet<string>();

		[Desc("Defensive building types the AI puts a wall in front of. Usually its towers.")]
		public readonly HashSet<string> WalledDefenseTypes = new HashSet<string>();

		[Desc("Defensive building types whose BuildingFractions ceiling adapts based on their measured kills-value/losses-value ratio",
			"(see AdaptiveWeighting). proc/nuke/silo/production buildings are already priority-overridden earlier in",
			"ChooseBuildingToBuild and must never be listed here - adaptation would fight that logic.")]
		public readonly HashSet<string> AdaptiveBuildingTypes = new HashSet<string>();

		[Desc("Combat samples (kills+losses) an adaptive defense type needs before its decayed score is fully trusted.")]
		public readonly int AdaptiveConfidenceSamples = 10;

		[Desc("Minimum/maximum share (0-1) of the base an adaptive defense type's ceiling may be nudged to.")]
		public readonly float AdaptiveWeightFloor = 0.01f;
		public readonly float AdaptiveWeightCeiling = 0.5f;

		[Desc("How many cells in front of a tower, on its enemy facing side, its wall is placed.")]
		public readonly int WallDistanceFromTower = 3;

		[Desc("Hard cap on the number of wall actors the AI will own. Zero disables wall building,",
			"which is the default so other mods and bots are unaffected.")]
		public readonly int MaximumWallSegments = 0;

		[Desc("Name of the locomotor used to verify the AI can still move around after walling.")]
		public readonly string WallPathCheckLocomotor = "wheeled";

		[Desc("What buildings to the AI should build.", "What integer percentage of the total base must be this type of building.")]
		public readonly Dictionary<string, int> BuildingFractions = null;

		[Desc("What buildings should the AI have a maximum limit to build.")]
		public readonly Dictionary<string, int> BuildingLimits = null;

		[Desc("When should the AI start building specific buildings.")]
		public readonly Dictionary<string, int> BuildingDelays = null;

		public override object Create(ActorInitializer init) { return new BaseBuilderBotModule(init.Self, this); }
	}

	public class BaseBuilderBotModule : ConditionalTrait<BaseBuilderBotModuleInfo>, IGameSaveTraitData,
		IBotTick, IBotPositionsUpdated, IBotRespondToAttack, IBotRequestPauseUnitProduction
	{
		public CPos GetRandomBaseCenter()
		{
			var randomConstructionYard = world.Actors.Where(a => a.Owner == player &&
				Info.ConstructionYardTypes.Contains(a.Info.Name))
				.RandomOrDefault(world.LocalRandom);

			return randomConstructionYard?.Location ?? initialBaseCenter;
		}

		public CPos DefenseCenter => Info.FirstCombatTowerNearConstructionYard && !firstCombatTowerOrdered ? GetRandomBaseCenter() : defenseCenter;

		internal BaseBuilderWallPlanner WallPlanner { get; private set; }

		readonly World world;
		readonly Player player;
		PowerManager playerPower;
		PlayerResources playerResources;
		IResourceLayer resourceLayer;
		IBotPositionsUpdated[] positionsUpdatedModules;
		CPos initialBaseCenter;
		CPos defenseCenter;
		OpeningStage openingStage;
		int openingUnavailableAttempts;
		int openingBuildingPendingTick;
		int nextExcessCashExpansionTick;
		bool openingBuildingPending;
		bool firstCombatTowerOrdered;
		IBotRequestUnitProduction[] unitProduction;

		readonly List<BaseBuilderQueueManager> builders = new List<BaseBuilderQueueManager>();

		public BaseBuilderBotModule(Actor self, BaseBuilderBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			playerPower = self.Owner.PlayerActor.TraitOrDefault<PowerManager>();
			playerResources = self.Owner.PlayerActor.Trait<PlayerResources>();
			resourceLayer = self.World.WorldActor.TraitOrDefault<IResourceLayer>();
			positionsUpdatedModules = self.Owner.PlayerActor.TraitsImplementing<IBotPositionsUpdated>().ToArray();
			unitProduction = self.Owner.PlayerActor.TraitsImplementing<IBotRequestUnitProduction>().ToArray();
			WallPlanner = new BaseBuilderWallPlanner(this, player);
		}

		protected override void TraitEnabled(Actor self)
		{
			foreach (var building in Info.BuildingQueues)
				builders.Add(new BaseBuilderQueueManager(this, building, player, playerPower, playerResources, resourceLayer));
			foreach (var defense in Info.DefenseQueues)
				builders.Add(new BaseBuilderQueueManager(this, defense, player, playerPower, playerResources, resourceLayer));
		}

		void IBotPositionsUpdated.UpdatedBaseCenter(CPos newLocation)
		{
			initialBaseCenter = newLocation;
		}

		void IBotPositionsUpdated.UpdatedDefenseCenter(CPos newLocation)
		{
			defenseCenter = newLocation;
		}

		bool IBotRequestPauseUnitProduction.PauseUnitProduction => !IsTraitDisabled && (!HasAdequateRefineryCount || OpeningActive);

		void IBotTick.BotTick(IBot bot)
		{
			UpdateOpening(bot);
			RequestExcessCashExpansion(bot);
			SetRallyPointsForNewProductionBuildings(bot);

			foreach (var b in builders)
				b.Tick(bot);
		}

		internal bool OpeningActive => Info.EnableOpeningPolicy && openingStage != OpeningStage.Complete;
		internal bool PlaceFirstCombatTowerByConstructionYard =>
			Info.FirstCombatTowerNearConstructionYard && !firstCombatTowerOrdered;

		internal ActorInfo OpeningBuilding(IEnumerable<ActorInfo> buildables)
		{
			if (!OpeningActive)
				return null;

			var preferred = OpeningTypes(openingStage);
			if (preferred == null)
				return null;

			if (openingBuildingPending)
			{
				var queued = Info.BuildingQueues.Concat(Info.DefenseQueues)
					.SelectMany(q => AIUtils.FindQueues(player, q))
					.Any(q => q.AllQueued().Any(item => preferred.Contains(item.Item)));
				if (queued || world.WorldTick - openingBuildingPendingTick <
					Math.Max(1, Info.StructureProductionInactiveDelay * 2))
					return null;

				// A queued order never appeared, or its production/placement was canceled.
				// Release the cross-queue lock and let the same stage retry.
				openingBuildingPending = false;
			}

			var buildableArray = buildables.ToArray();
			var selected = OpeningPolicyLogic.FirstAvailable(preferred, buildableArray.Select(a => a.Name));
			var actor = buildableArray.FirstOrDefault(a => a.Name == selected);
			if (actor != null)
			{
				openingUnavailableAttempts = 0;
				openingBuildingPending = true;
				openingBuildingPendingTick = world.WorldTick;
				return actor;
			}

			if (OpeningPolicyLogic.ShouldSkipUnavailable(++openingUnavailableAttempts, Info.OpeningUnavailableRetries))
			{
				AIUtils.BotDebug("{0} skipped unavailable opening stage {1}", player, openingStage);
				openingUnavailableAttempts = 0;
				openingStage++;
			}

			return null;
		}

		internal void NotifyCombatTowerOrdered(ActorInfo actor)
		{
			if (!firstCombatTowerOrdered && actor.HasTraitInfo<AttackBaseInfo>())
			{
				firstCombatTowerOrdered = true;
				AIUtils.BotDebug("{0} placed its first combat tower by a construction yard", player);
			}
		}

		string[] OpeningTypes(OpeningStage stage)
		{
			switch (stage)
			{
				case OpeningStage.Power: return Info.OpeningPowerTypes;
				case OpeningStage.Barracks: return Info.OpeningBarracksTypes;
				case OpeningStage.Refinery: return Info.OpeningRefineryTypes;
				case OpeningStage.Factory: return Info.OpeningFactoryTypes;
				case OpeningStage.Radar: return Info.OpeningRadarTypes;
				case OpeningStage.Silo: return Info.OpeningSiloTypes;
				default: return null;
			}
		}

		void UpdateOpening(IBot bot)
		{
			if (!OpeningActive)
				return;

			while (OpeningActive && OpeningStageSatisfied(openingStage))
			{
				AIUtils.BotDebug("{0} completed opening stage {1}", player, openingStage);
				openingStage++;
				openingUnavailableAttempts = 0;
				openingBuildingPending = false;
			}

			if (openingStage == OpeningStage.Harvesters)
				RequestFirstAvailable(bot, Info.OpeningHarvesterTypes, "opening harvesters");
			else if (openingStage == OpeningStage.Mcv && !HasLiveActor(Info.OpeningMcvType))
				Request(bot, Info.OpeningMcvType, "opening expansion");
		}

		bool OpeningStageSatisfied(OpeningStage stage)
		{
			switch (stage)
			{
				case OpeningStage.Power: return CountActors(Info.OpeningPowerTypes) > 0;
				case OpeningStage.Barracks: return CountActors(Info.OpeningBarracksTypes) > 0;
				case OpeningStage.Refinery: return CountActors(Info.OpeningRefineryTypes) > 0;
				case OpeningStage.Factory: return CountActors(Info.OpeningFactoryTypes) > 0;
				case OpeningStage.Harvesters: return CountActors(Info.OpeningHarvesterTypes) >= Info.OpeningHarvesterCount;
				case OpeningStage.Mcv: return CountActors(Info.ConstructionYardTypes) >= Info.OpeningConstructionYardCount;
				case OpeningStage.Radar: return CountActors(Info.OpeningRadarTypes) > 0;
				case OpeningStage.Silo: return CountActors(Info.OpeningSiloTypes) > 0;
				default: return true;
			}
		}

		int CountActors(IEnumerable<string> types)
		{
			var names = types as ICollection<string> ?? types.ToArray();
			return world.Actors.Count(a => a.Owner == player && !a.IsDead && names.Contains(a.Info.Name));
		}

		bool HasLiveActor(string type) { return world.Actors.Any(a => a.Owner == player && !a.IsDead && a.Info.Name == type); }

		void RequestFirstAvailable(IBot bot, IEnumerable<string> types, string reason)
		{
			foreach (var type in types)
				if (world.Map.Rules.Actors.ContainsKey(type) && Request(bot, type, reason))
					return;
		}

		bool Request(IBot bot, string type, string reason)
		{
			if (string.IsNullOrEmpty(type))
				return false;

			var requester = unitProduction.FirstOrDefault(r => r.IsTraitEnabled() &&
				r.RequestedProductionCount(bot, type) == 0);
			if (requester == null)
				return false;

			requester.RequestUnitProduction(bot, type);
			AIUtils.BotDebug("{0} requested {1}: {2}", player, type, reason);
			return true;
		}

		void RequestExcessCashExpansion(IBot bot)
		{
			if (OpeningActive || Info.ExcessCashExpansionThreshold <= 0 || world.WorldTick < nextExcessCashExpansionTick ||
				playerResources.Cash + playerResources.Resources < Info.ExcessCashExpansionThreshold ||
				CountActors(Info.ConstructionYardTypes) >= Info.ExcessCashConstructionYardTarget || HasLiveActor(Info.OpeningMcvType))
				return;

			if (Request(bot, Info.OpeningMcvType, "excess-cash expansion"))
				nextExcessCashExpansionTick = world.WorldTick + Info.ExcessCashExpansionCooldown;
		}

		void IBotRespondToAttack.RespondToAttack(IBot bot, Actor self, AttackInfo e)
		{
			if (e.Attacker == null || e.Attacker.Disposed)
				return;

			if (e.Attacker.Owner.RelationshipWith(self.Owner) != PlayerRelationship.Enemy)
				return;

			if (!e.Attacker.Info.HasTraitInfo<ITargetableInfo>())
				return;

			// Protect buildings
			if (self.Info.HasTraitInfo<BuildingInfo>())
				foreach (var n in positionsUpdatedModules)
					n.UpdatedDefenseCenter(e.Attacker.Location);
		}

		void SetRallyPointsForNewProductionBuildings(IBot bot)
		{
			foreach (var rp in world.ActorsWithTrait<RallyPoint>())
			{
				if (rp.Actor.Owner != player)
					continue;

				if (rp.Trait.Path.Count == 0 || !IsRallyPointValid(rp.Trait.Path[0], rp.Actor.Info.TraitInfoOrDefault<BuildingInfo>()))
				{
					bot.QueueOrder(new Order("SetRallyPoint", rp.Actor, Target.FromCell(world, ChooseRallyLocationNear(rp.Actor)), false)
					{
						SuppressVisualFeedback = true
					});
				}
			}
		}

		// Won't work for shipyards...
		CPos ChooseRallyLocationNear(Actor producer)
		{
			var possibleRallyPoints = world.Map.FindTilesInCircle(producer.Location, Info.RallyPointScanRadius)
				.Where(c => IsRallyPointValid(c, producer.Info.TraitInfoOrDefault<BuildingInfo>()));

			if (!possibleRallyPoints.Any())
			{
				AIUtils.BotDebug("{0} has no possible rallypoint near {1}", producer.Owner, producer.Location);
				return producer.Location;
			}

			return possibleRallyPoints.Random(world.LocalRandom);
		}

		bool IsRallyPointValid(CPos x, BuildingInfo info)
		{
			return info != null && world.IsCellBuildable(x, null, info);
		}

		// Require at least one refinery, unless we can't build it.
		public bool HasAdequateRefineryCount =>
			!Info.RefineryTypes.Any() ||
			AIUtils.CountBuildingByCommonName(Info.RefineryTypes, player) >= MinimumRefineryCount ||
			AIUtils.CountBuildingByCommonName(Info.PowerTypes, player) == 0 ||
			AIUtils.CountBuildingByCommonName(Info.ConstructionYardTypes, player) == 0;

		internal int CongestionRefineryShortfall
		{
			get
			{
				var harvesters = AIUtils.CountActorByCommonName(Info.HarvesterTypes, player);
				var refineries = AIUtils.CountBuildingByCommonName(Info.RefineryTypes, player);
				return HarvesterRaidLogic.AdditionalRefineries(harvesters, refineries,
					Info.CongestionHarvestersPerRefinery, Info.MaximumCongestionRefineries);
			}
		}

		int MinimumRefineryCount
		{
			get
			{
				var normal = AIUtils.CountBuildingByCommonName(Info.BarracksTypes, player) > 0 ?
					Info.InititalMinimumRefineryCount + Info.AdditionalMinimumRefineryCount : Info.InititalMinimumRefineryCount;
				var refineries = AIUtils.CountBuildingByCommonName(Info.RefineryTypes, player);
				return Math.Max(normal, refineries + CongestionRefineryShortfall);
			}
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>()
			{
				new MiniYamlNode("InitialBaseCenter", FieldSaver.FormatValue(initialBaseCenter)),
				new MiniYamlNode("DefenseCenter", FieldSaver.FormatValue(defenseCenter)),
				new MiniYamlNode("OpeningStage", FieldSaver.FormatValue((int)openingStage)),
				new MiniYamlNode("OpeningUnavailableAttempts", FieldSaver.FormatValue(openingUnavailableAttempts)),
				new MiniYamlNode("OpeningBuildingPending", FieldSaver.FormatValue(openingBuildingPending)),
				new MiniYamlNode("OpeningBuildingPendingTick", FieldSaver.FormatValue(openingBuildingPendingTick)),
				new MiniYamlNode("NextExcessCashExpansionTick", FieldSaver.FormatValue(nextExcessCashExpansionTick)),
				new MiniYamlNode("FirstCombatTowerOrdered", FieldSaver.FormatValue(firstCombatTowerOrdered))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var initialBaseCenterNode = data.FirstOrDefault(n => n.Key == "InitialBaseCenter");
			if (initialBaseCenterNode != null)
				initialBaseCenter = FieldLoader.GetValue<CPos>("InitialBaseCenter", initialBaseCenterNode.Value.Value);

			var defenseCenterNode = data.FirstOrDefault(n => n.Key == "DefenseCenter");
			if (defenseCenterNode != null)
				defenseCenter = FieldLoader.GetValue<CPos>("DefenseCenter", defenseCenterNode.Value.Value);

			var openingStageNode = data.FirstOrDefault(n => n.Key == "OpeningStage");
			if (openingStageNode != null)
				openingStage = (OpeningStage)FieldLoader.GetValue<int>("OpeningStage", openingStageNode.Value.Value);
			var unavailableNode = data.FirstOrDefault(n => n.Key == "OpeningUnavailableAttempts");
			if (unavailableNode != null)
				openingUnavailableAttempts = FieldLoader.GetValue<int>("OpeningUnavailableAttempts", unavailableNode.Value.Value);
			var pendingNode = data.FirstOrDefault(n => n.Key == "OpeningBuildingPending");
			if (pendingNode != null)
				openingBuildingPending = FieldLoader.GetValue<bool>("OpeningBuildingPending", pendingNode.Value.Value);
			var pendingTickNode = data.FirstOrDefault(n => n.Key == "OpeningBuildingPendingTick");
			if (pendingTickNode != null)
				openingBuildingPendingTick = FieldLoader.GetValue<int>("OpeningBuildingPendingTick", pendingTickNode.Value.Value);
			var expansionNode = data.FirstOrDefault(n => n.Key == "NextExcessCashExpansionTick");
			if (expansionNode != null)
				nextExcessCashExpansionTick = FieldLoader.GetValue<int>("NextExcessCashExpansionTick", expansionNode.Value.Value);
			var towerNode = data.FirstOrDefault(n => n.Key == "FirstCombatTowerOrdered");
			if (towerNode != null)
				firstCombatTowerOrdered = FieldLoader.GetValue<bool>("FirstCombatTowerOrdered", towerNode.Value.Value);
		}
	}

	enum OpeningStage
	{
		Power,
		Barracks,
		Refinery,
		Factory,
		Harvesters,
		Mcv,
		Radar,
		Silo,
		Complete
	}
}
