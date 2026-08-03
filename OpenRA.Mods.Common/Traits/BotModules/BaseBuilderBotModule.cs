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

		[Desc("Tells the AI what building types are considered power plants.")]
		public readonly HashSet<string> PowerTypes = new HashSet<string>();

		[Desc("Forces AI to treat these buildings as defenses and build them toward enemy.")]
		public readonly HashSet<string> PlaceAsDefenses = new HashSet<string>();

		[Desc("Anti-ground defense types whose first completed structure is placed beside the initial construction yard.")]
		public readonly HashSet<string> FirstTowerTypes = new HashSet<string>();

		[Desc("Maximum cells searched around the preferred first-tower position when it is blocked.")]
		public readonly int FirstTowerSearchRadius = 20;

		[Desc("Write first-tower planning and completion diagnostics to debug.log.")]
		public readonly bool FirstTowerDebugLogging = false;

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

		[Desc("Use the configured parallel opening goals before ordinary structure selection.")]
		public readonly bool EnableOpeningPolicy = false;

		[Desc("Opening structure alternatives in goal order. Separate production queues may work on different goals concurrently.")]
		public readonly string[] OpeningPowerTypes = System.Array.Empty<string>();
		public readonly string[] OpeningSiloTypes = System.Array.Empty<string>();
		public readonly string[] OpeningDefenseTypes = System.Array.Empty<string>();
		public readonly string[] OpeningBarracksTypes = System.Array.Empty<string>();
		public readonly string[] OpeningRefineryTypes = System.Array.Empty<string>();
		public readonly string[] OpeningFactoryTypes = System.Array.Empty<string>();
		public readonly string[] OpeningRadarTypes = System.Array.Empty<string>();
		public readonly string[] OpeningOptionalStructureTypes = System.Array.Empty<string>();

		[Desc("Opening unit alternatives and targets. External requests ignore ordinary unit delays.")]
		public readonly string[] OpeningSoldierTypes = System.Array.Empty<string>();
		public readonly string[] OpeningHarvesterTypes = System.Array.Empty<string>();
		public readonly string[] OpeningDefenseUnlockTypes = System.Array.Empty<string>();
		public readonly string OpeningMcvType = "mcv";
		public readonly int OpeningSoldierCount = 20;
		public readonly int OpeningHarvesterCount = 5;
		public readonly int OpeningMcvCount = 1;

		[Desc("Ticks before retrying an opening structure request that never entered any production queue.")]
		public readonly int OpeningRequestRetryDelay = 250;
		public readonly int OpeningUnitRequestCooldown = 60;
		public readonly int OpeningProgressLogInterval = 750;

		[Desc("Write opening goal, request, completion, and stall diagnostics to debug.log.")]
		public readonly bool OpeningDebugLogging = false;

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

		[Desc("Preferred wall types used to enclose the first construction yard. Leave empty to disable the enclosure.")]
		public readonly string[] ConstructionYardEnclosureWallTypes = System.Array.Empty<string>();

		[Desc("Empty cells left between the construction yard footprint and its enclosure.")]
		public readonly int ConstructionYardEnclosureMargin = 1;

		[Desc("Write construction-yard enclosure planning and failure diagnostics to debug.log.")]
		public readonly bool ConstructionYardEnclosureDebugLogging = false;

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

		public CPos DefenseCenter => defenseCenter;

		internal BaseBuilderWallPlanner WallPlanner { get; private set; }
		internal BaseBuilderFirstTowerPlanner FirstTowerPlanner { get; private set; }

		readonly World world;
		readonly Player player;
		PowerManager playerPower;
		PlayerResources playerResources;
		PlayerStatistics playerStatistics;
		IResourceLayer resourceLayer;
		IBotPositionsUpdated[] positionsUpdatedModules;
		IBotRequestUnitProduction[] unitProduction;
		IBotRallyPointManager[] rallyPointManagers;
		CPos initialBaseCenter;
		CPos defenseCenter;
		readonly Dictionary<int, int> openingStructureReservations = new Dictionary<int, int>();
		readonly HashSet<int> loggedCompletedOpeningGoals = new HashSet<int>();
		bool openingCompletionLogged;
		bool openingInitialized;
		int openingSoldierBuiltBaseline;
		int openingMcvBuiltBaseline;
		int nextOpeningSoldierRequestTick;
		int nextOpeningHarvesterRequestTick;
		int nextOpeningDefenseUnlockRequestTick;
		int nextOpeningMcvRequestTick;
		int nextOpeningProgressLogTick;

		readonly List<BaseBuilderQueueManager> builders = new List<BaseBuilderQueueManager>();
		UnitBuilderBotModule[] unitBuilders;

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
			playerStatistics = self.Owner.PlayerActor.Trait<PlayerStatistics>();
			resourceLayer = self.World.WorldActor.TraitOrDefault<IResourceLayer>();
			positionsUpdatedModules = self.Owner.PlayerActor.TraitsImplementing<IBotPositionsUpdated>().ToArray();
			unitBuilders = self.Owner.PlayerActor.TraitsImplementing<UnitBuilderBotModule>().ToArray();
			unitProduction = self.Owner.PlayerActor.TraitsImplementing<IBotRequestUnitProduction>().ToArray();
			rallyPointManagers = self.Owner.PlayerActor.TraitsImplementing<IBotRallyPointManager>().ToArray();
			WallPlanner = new BaseBuilderWallPlanner(this, player);
			FirstTowerPlanner = new BaseBuilderFirstTowerPlanner(this, player);
		}

		internal bool AdaptiveProductionDebugLogging => unitBuilders.Any(u =>
			!u.IsTraitDisabled && u.Info.AdaptiveProductionDebugLogging);

		internal void LogAdaptiveProduction(string format, params object[] args)
		{
			AIUtils.BotDebug(format, args);
			Log.Write("debug", "AI adaptive production: " + format, args);
		}

		internal double AdaptiveProductionBuildingDemand(ActorInfo building)
		{
			return unitBuilders.Where(u => !u.IsTraitDisabled)
				.Select(u => u.ProductionBuildingDemand(building)).DefaultIfEmpty(0).Max();
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

		bool IBotRequestPauseUnitProduction.PauseUnitProduction => !IsTraitDisabled && !HasAdequateRefineryCount;

		void IBotTick.BotTick(IBot bot)
		{
			UpdateOpening(bot);
			FirstTowerPlanner.Update();
			SetRallyPointsForNewProductionBuildings(bot);

			foreach (var b in builders)
				b.Tick(bot);
		}

		internal bool OpeningActive => Info.EnableOpeningPolicy && !OpeningComplete;

		internal ActorInfo OpeningBuilding(IEnumerable<ActorInfo> buildables)
		{
			if (!OpeningActive)
				return null;

			RefreshOpeningStructureReservations();
			var goals = OpeningStructureGoals;
			var completed = CompletedOpeningStructureGoals(goals);
			var buildableArray = buildables.ToArray();
			var goal = OpeningPolicyLogic.FirstBuildableGoal(
				goals, completed, openingStructureReservations.Keys.ToArray(), buildableArray.Select(a => a.Name));
			if (goal < 0)
				return null;

			var selected = OpeningPolicyLogic.FirstAvailable(goals[goal], buildableArray.Select(a => a.Name));
			if (selected == null)
				return null;

			openingStructureReservations[goal] = world.WorldTick;
			LogOpening("{0} reserved structure goal {1}: {2}", player, OpeningGoalName(goal), selected);
			return buildableArray.First(a => a.Name == selected);
		}

		IReadOnlyList<string[]> OpeningStructureGoals => new[]
		{
			Info.OpeningPowerTypes,
			Info.OpeningSiloTypes,
			Info.OpeningDefenseTypes,
			Info.OpeningBarracksTypes,
			Info.OpeningRefineryTypes,
			Info.OpeningFactoryTypes,
			Info.OpeningRadarTypes
		};

		static string OpeningGoalName(int goal)
		{
			var names = new[] { "power", "silo", "anti-ground defense", "barracks", "refinery", "factory", "radar" };
			return goal >= 0 && goal < names.Length ? names[goal] : goal.ToString();
		}

		HashSet<int> CompletedOpeningStructureGoals(IReadOnlyList<string[]> goals)
		{
			var completed = new HashSet<int>(loggedCompletedOpeningGoals);
			for (var i = 0; i < goals.Count; i++)
			{
				// Empty goals are intentionally disabled by configuration.
				if (goals[i].Length == 0 || CountActors(goals[i]) > 0)
				{
					completed.Add(i);
					if (loggedCompletedOpeningGoals.Add(i))
						LogOpening("{0} completed structure goal {1}", player, OpeningGoalName(i));
				}
			}

			return completed;
		}

		void RefreshOpeningStructureReservations()
		{
			var goals = OpeningStructureGoals;
			var queues = Info.BuildingQueues.Concat(Info.DefenseQueues)
				.SelectMany(q => AIUtils.FindQueues(player, q)).ToArray();
			foreach (var reservation in openingStructureReservations.ToArray())
			{
				var completed = reservation.Key < goals.Count && CountActors(goals[reservation.Key]) > 0;
				var queued = reservation.Key < goals.Count && queues.Any(q =>
					q.AllQueued().Any(item => goals[reservation.Key].Contains(item.Item)));
				if (completed || OpeningPolicyLogic.RetryReservation(reservation.Value, world.WorldTick,
					Info.OpeningRequestRetryDelay, queued))
				{
					openingStructureReservations.Remove(reservation.Key);
					if (!completed)
						LogOpening("{0} released stalled structure goal {1} for retry", player, OpeningGoalName(reservation.Key));
				}
			}
		}

		void UpdateOpening(IBot bot)
		{
			if (!Info.EnableOpeningPolicy)
				return;

			if (!openingInitialized)
			{
				openingInitialized = true;
				openingSoldierBuiltBaseline = TotalBuilt(Info.OpeningSoldierTypes);
				openingMcvBuiltBaseline = string.IsNullOrEmpty(Info.OpeningMcvType) ? 0 :
					TotalBuilt(new[] { Info.OpeningMcvType });
			}

			var completedStructures = CompletedOpeningStructureGoals(OpeningStructureGoals);
			CompleteUnavailableOptionalOpeningStructureGoals(completedStructures);
			if (Info.OpeningDebugLogging && world.WorldTick >= nextOpeningProgressLogTick)
			{
				nextOpeningProgressLogTick = world.WorldTick + System.Math.Max(1, Info.OpeningProgressLogInterval);
				LogOpening("{0} progress: structures={1}/{2}, soldiers={3}/{4}, harvesters={5}/{6}, mcvs-built={7}/{8}",
					player, completedStructures.Count, OpeningStructureGoals.Count,
					OpeningSoldiersBuilt, Info.OpeningSoldierCount,
					CountActors(Info.OpeningHarvesterTypes), Info.OpeningHarvesterCount,
					OpeningMcvsBuilt, Info.OpeningMcvCount);
			}

			if (Info.OpeningSoldierTypes.Length > 0 && OpeningSoldiersBuilt < Info.OpeningSoldierCount &&
				world.WorldTick >= nextOpeningSoldierRequestTick &&
				RequestFirstAvailable(bot, Info.OpeningSoldierTypes, "opening soldiers"))
				nextOpeningSoldierRequestTick = world.WorldTick + System.Math.Max(1, Info.OpeningUnitRequestCooldown);

			if (Info.OpeningHarvesterTypes.Length > 0 && CountActors(Info.OpeningHarvesterTypes) < Info.OpeningHarvesterCount &&
				world.WorldTick >= nextOpeningHarvesterRequestTick &&
				RequestFirstAvailable(bot, Info.OpeningHarvesterTypes, "opening harvesters"))
				nextOpeningHarvesterRequestTick = world.WorldTick + System.Math.Max(1, Info.OpeningUnitRequestCooldown);

			if (!completedStructures.Contains(2) && completedStructures.Contains(6) &&
				Info.OpeningDefenseUnlockTypes.Length > 0 && CountActors(Info.OpeningDefenseUnlockTypes) == 0 &&
				world.WorldTick >= nextOpeningDefenseUnlockRequestTick &&
				RequestFirstAvailable(bot, Info.OpeningDefenseUnlockTypes, "opening defense unlock"))
				nextOpeningDefenseUnlockRequestTick = world.WorldTick + System.Math.Max(1, Info.OpeningUnitRequestCooldown);

			if (!string.IsNullOrEmpty(Info.OpeningMcvType) &&
				CountActors(Info.OpeningHarvesterTypes) >= Info.OpeningHarvesterCount &&
				OpeningMcvsBuilt < Info.OpeningMcvCount &&
				!HasLiveActor(Info.OpeningMcvType) && world.WorldTick >= nextOpeningMcvRequestTick &&
				Request(bot, Info.OpeningMcvType, "opening expansion MCV"))
				nextOpeningMcvRequestTick = world.WorldTick + System.Math.Max(1, Info.OpeningUnitRequestCooldown);

			if (OpeningComplete && !openingCompletionLogged)
			{
				openingCompletionLogged = true;
				LogOpening("{0} completed opening policy", player);
			}
		}

		void CompleteUnavailableOptionalOpeningStructureGoals(HashSet<int> completedGoals)
		{
			var goals = OpeningStructureGoals;
			var buildableTypes = Info.BuildingQueues.Concat(Info.DefenseQueues)
				.SelectMany(category => AIUtils.FindQueues(player, category))
				.SelectMany(queue => queue.BuildableItems()).Select(item => item.Name).ToArray();

			for (var i = 0; i < goals.Count; i++)
			{
				// A defense unlock that is buildable under the current technology means the
				// tower is only temporarily unavailable and must not be skipped.
				if (i == 2 && Info.OpeningDefenseUnlockTypes.Any(IsCurrentlyBuildable))
					continue;

				if (!OpeningPolicyLogic.CanSkipUnavailableGoal(i, goals, completedGoals,
					Info.OpeningOptionalStructureTypes, buildableTypes))
					continue;

				completedGoals.Add(i);
				if (loggedCompletedOpeningGoals.Add(i))
					LogOpening("{0} skipped unavailable optional structure goal {1}", player, OpeningGoalName(i));
			}
		}

		bool OpeningComplete => CompletedOpeningStructureGoals(OpeningStructureGoals).Count == OpeningStructureGoals.Count &&
			(Info.OpeningSoldierTypes.Length == 0 || OpeningSoldiersBuilt >= Info.OpeningSoldierCount) &&
			(Info.OpeningHarvesterTypes.Length == 0 || CountActors(Info.OpeningHarvesterTypes) >= Info.OpeningHarvesterCount) &&
			(string.IsNullOrEmpty(Info.OpeningMcvType) ||
				OpeningMcvsBuilt >= Info.OpeningMcvCount);

		int CountActors(IEnumerable<string> types)
		{
			var names = types as ICollection<string> ?? types.ToArray();
			return world.Actors.Count(a => a.Owner == player && !a.IsDead && names.Contains(a.Info.Name));
		}

		int OpeningSoldiersBuilt => System.Math.Max(0,
			TotalBuilt(Info.OpeningSoldierTypes) - openingSoldierBuiltBaseline);

		int OpeningMcvsBuilt => string.IsNullOrEmpty(Info.OpeningMcvType) ? 0 : System.Math.Max(0,
			TotalBuilt(new[] { Info.OpeningMcvType }) - openingMcvBuiltBaseline);

		int TotalBuilt(IEnumerable<string> types)
		{
			return types.Sum(type => playerStatistics.AdaptiveStats[type].BuiltCount);
		}

		bool HasLiveActor(string type)
		{
			return !string.IsNullOrEmpty(type) && world.Actors.Any(a => a.Owner == player && !a.IsDead && a.Info.Name == type);
		}

		bool RequestFirstAvailable(IBot bot, IEnumerable<string> types, string reason)
		{
			var alternatives = types.ToArray();
			if (alternatives.Any(type => unitProduction.Any(r => r.IsTraitEnabled() &&
				r.RequestedProductionCount(bot, type) > 0)))
				return false;

			foreach (var type in alternatives)
				if (world.Map.Rules.Actors.ContainsKey(type) && Request(bot, type, reason))
					return true;

			return false;
		}

		bool Request(IBot bot, string type, string reason)
		{
			if (string.IsNullOrEmpty(type) || !CanCurrentlyProduce(type))
				return false;

			var requester = unitProduction.FirstOrDefault(r => r.IsTraitEnabled() &&
				r.RequestedProductionCount(bot, type) == 0);
			if (requester == null)
				return false;

			requester.RequestUnitProduction(bot, type);
			LogOpening("{0} requested {1}: {2}", player, type, reason);
			return true;
		}

		bool CanCurrentlyProduce(string type)
		{
			return IsCurrentlyBuildable(type) && world.Map.Rules.Actors[type].TraitInfo<BuildableInfo>().Queue
				.Any(category => AIUtils.FindQueues(player, category)
				.Any(queue => !queue.AllQueued().Any() && queue.BuildableItems().Any(item => item.Name == type)));
		}

		bool IsCurrentlyBuildable(string type)
		{
			if (!world.Map.Rules.Actors.TryGetValue(type, out var actor))
				return false;

			var buildable = actor.TraitInfoOrDefault<BuildableInfo>();
			return buildable != null && buildable.Queue.Any(category => AIUtils.FindQueues(player, category)
				.Any(queue => queue.BuildableItems().Any(item => item.Name == type)));
		}

		void LogOpening(string format, params object[] args)
		{
			AIUtils.BotDebug(format, args);
			if (Info.OpeningDebugLogging)
				Log.Write("debug", "AI opening: " + format, args);
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

				if (rallyPointManagers.Any(manager => manager.ManagesRallyPoint(rp.Actor)))
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

		int MinimumRefineryCount => AIUtils.CountBuildingByCommonName(Info.BarracksTypes, player) > 0 ? Info.InititalMinimumRefineryCount + Info.AdditionalMinimumRefineryCount : Info.InititalMinimumRefineryCount;

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>()
			{
				new MiniYamlNode("InitialBaseCenter", FieldSaver.FormatValue(initialBaseCenter)),
				new MiniYamlNode("DefenseCenter", FieldSaver.FormatValue(defenseCenter)),
				new MiniYamlNode("OpeningInitialized", FieldSaver.FormatValue(openingInitialized)),
				new MiniYamlNode("OpeningSoldierBuiltBaseline", FieldSaver.FormatValue(openingSoldierBuiltBaseline)),
				new MiniYamlNode("OpeningMcvBuiltBaseline", FieldSaver.FormatValue(openingMcvBuiltBaseline)),
				new MiniYamlNode("CompletedOpeningGoals", FieldSaver.FormatValue(loggedCompletedOpeningGoals.ToArray())),
				new MiniYamlNode("NextOpeningSoldierRequestTick", FieldSaver.FormatValue(nextOpeningSoldierRequestTick)),
				new MiniYamlNode("NextOpeningHarvesterRequestTick", FieldSaver.FormatValue(nextOpeningHarvesterRequestTick)),
				new MiniYamlNode("NextOpeningDefenseUnlockRequestTick", FieldSaver.FormatValue(nextOpeningDefenseUnlockRequestTick)),
				new MiniYamlNode("NextOpeningMcvRequestTick", FieldSaver.FormatValue(nextOpeningMcvRequestTick)),
				new MiniYamlNode("FirstTowerPlacementComplete", FieldSaver.FormatValue(FirstTowerPlanner.Complete))
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

			var openingInitializedNode = data.FirstOrDefault(n => n.Key == "OpeningInitialized");
			if (openingInitializedNode != null)
				openingInitialized = FieldLoader.GetValue<bool>("OpeningInitialized", openingInitializedNode.Value.Value);
			var openingBaselineNode = data.FirstOrDefault(n => n.Key == "OpeningSoldierBuiltBaseline");
			if (openingBaselineNode != null)
				openingSoldierBuiltBaseline = FieldLoader.GetValue<int>("OpeningSoldierBuiltBaseline", openingBaselineNode.Value.Value);
			var openingMcvBaselineNode = data.FirstOrDefault(n => n.Key == "OpeningMcvBuiltBaseline");
			if (openingMcvBaselineNode != null)
				openingMcvBuiltBaseline = FieldLoader.GetValue<int>("OpeningMcvBuiltBaseline", openingMcvBaselineNode.Value.Value);
			var completedGoalsNode = data.FirstOrDefault(n => n.Key == "CompletedOpeningGoals");
			if (completedGoalsNode != null)
			{
				loggedCompletedOpeningGoals.Clear();
				loggedCompletedOpeningGoals.UnionWith(FieldLoader.GetValue<int[]>("CompletedOpeningGoals", completedGoalsNode.Value.Value));
			}

			var soldierRequestNode = data.FirstOrDefault(n => n.Key == "NextOpeningSoldierRequestTick");
			if (soldierRequestNode != null)
				nextOpeningSoldierRequestTick = FieldLoader.GetValue<int>("NextOpeningSoldierRequestTick", soldierRequestNode.Value.Value);
			var harvesterRequestNode = data.FirstOrDefault(n => n.Key == "NextOpeningHarvesterRequestTick");
			if (harvesterRequestNode != null)
				nextOpeningHarvesterRequestTick = FieldLoader.GetValue<int>("NextOpeningHarvesterRequestTick", harvesterRequestNode.Value.Value);
			var unlockRequestNode = data.FirstOrDefault(n => n.Key == "NextOpeningDefenseUnlockRequestTick");
			if (unlockRequestNode != null)
				nextOpeningDefenseUnlockRequestTick = FieldLoader.GetValue<int>("NextOpeningDefenseUnlockRequestTick", unlockRequestNode.Value.Value);
			var mcvRequestNode = data.FirstOrDefault(n => n.Key == "NextOpeningMcvRequestTick");
			if (mcvRequestNode != null)
				nextOpeningMcvRequestTick = FieldLoader.GetValue<int>("NextOpeningMcvRequestTick", mcvRequestNode.Value.Value);

			var firstTowerNode = data.FirstOrDefault(n => n.Key == "FirstTowerPlacementComplete");
			if (firstTowerNode != null)
				FirstTowerPlanner.Complete = FieldLoader.GetValue<bool>("FirstTowerPlacementComplete", firstTowerNode.Value.Value);
		}
	}
}
