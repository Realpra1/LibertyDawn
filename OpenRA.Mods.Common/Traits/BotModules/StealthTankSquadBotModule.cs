#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Manages a bounded number of specialist stealth harassment and attack squads.")]
	public class StealthTankSquadBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Actor types eligible for specialist stealth squads.")]
		public readonly HashSet<string> UnitTypes = new HashSet<string>();

		[Desc("Short diagnostic name used to distinguish multiple configured specialist managers.")]
		public readonly string SquadLabel = "stealth-tank";

		[Desc("Actor-specific harassment priorities. Unlisted harvesters, structures and infantry use class fallbacks.")]
		public readonly Dictionary<string, int> HarassmentTargetPriorities = new Dictionary<string, int>();

		[Desc("Additional harassment priorities enabled only after a specialist group has grown.")]
		public readonly Dictionary<string, int> LateHarassmentTargetPriorities = new Dictionary<string, int>();

		[Desc("Actor-specific cooperative attack priorities. Tank target types otherwise receive the highest fallback.")]
		public readonly Dictionary<string, int> AttackTargetPriorities = new Dictionary<string, int>();

		[Desc("Structure armor types and their harassment priorities. Actor-specific priorities take precedence.")]
		public readonly Dictionary<string, int> HarassmentArmorPriorities = new Dictionary<string, int>();

		[Desc("Target types that harassment groups must never deliberately select.")]
		public readonly BitSet<TargetableType> ExcludedHarassmentTargetTypes = default(BitSet<TargetableType>);

		[Desc("Target types whose weapons do not make a harassment route dangerous. Detector ranges still apply.")]
		public readonly BitSet<TargetableType> IgnoredHarassmentWeaponThreatTypes = default(BitSet<TargetableType>);

		public readonly int ScanInterval = 75;
		public readonly int OrderInterval = 75;
		public readonly int MaximumTargetCandidates = 48;
		public readonly int MaximumHarassmentGroups = 2;
		public readonly bool IncludeAttackGroup = true;
		public readonly bool ReserveOpeningPair = true;
		[Desc("Claim every eligible unit for this specialist manager instead of leaving an ordinary-army reserve.")]
		public readonly bool ClaimAllEligible = false;
		public readonly int ThreatRangeBufferCells = 2;
		public readonly int DetectorRangeBufferCells = 2;
		public readonly int KiteRangeMarginCells = 1;
		public readonly int CarefulClearValueRatio = 5;
		public readonly int MinimumLateHarassmentGroupSize = 3;
		public readonly int TargetSwitchImprovementPercent = 25;
		public readonly int HarassmentDistancePenalty = 1;
		public readonly HashSet<string> HarvesterTypes = new HashSet<string>();
		public readonly HashSet<string> HarvesterWaitingAnchorTypes = new HashSet<string>();
		public readonly int ResourceWaitingSearchRadius = 0;
		public readonly int ResourceWaitingOrderInterval = 750;
		[Desc("Resource types specialist routes, firing positions, and waiting positions must avoid.")]
		public readonly HashSet<string> AvoidResourceTypes = new HashSet<string>();
		[Desc("Cells around a pending resource explosion that specialist routes and firing positions avoid.")]
		public readonly int PendingResourceExplosionAvoidanceRadius = 0;
		[Desc("Maximum cells between queued hazard-aware route waypoints.")]
		public readonly int HazardRouteWaypointSpacing = 4;
		[Desc("Width and height in map cells of one specialist strategic cell. Zero retains waypoint-sized legacy cells.")]
		public readonly int StrategicCellSize = 0;
		[Desc("Retry interval for a stable mission that has made no progress. Zero uses OrderInterval.")]
		public readonly int MissionRetryInterval = 0;
		[Desc("Radius for the bounded fast scan of nearby undefended targets. Zero disables the fast scan.")]
		public readonly int NearbyTargetReactionRadiusCells = 0;
		[Desc("Retreat one strategic cell after a specialist fires and reveals itself.")]
		public readonly bool RetreatAfterReveal = false;
		[Desc("Consecutive all-defended scans before a harassment group may clear a weak defender.")]
		public readonly int DefenderClearFallbackScans = 20;
		[Desc("Required specialist value multiple over the complete defended route package before clearing its weakest member.")]
		public readonly int DefenderClearValueRatio = 1;
		[Desc("Number of lowest-total-defense opportunities considered before choosing by unlocked target score.")]
		public readonly int DefenderClearWeakestCandidates = 3;
		public readonly int InfantryTargetPriority = 1200;
		[Desc("Priority assigned to line-build wall targets before the generic structure fallback.")]
		public readonly int WallTargetPriority = 1;
		public readonly int StructureTargetPriority = 500;
		public readonly int TankTargetPriority = 1500;
		public readonly int InfantryClusterRadiusCells = 0;
		public readonly int InfantryClusterBonusPercentPerNearbyActor = 0;
		public readonly int MaximumInfantryClusterMultiplierPercent = 100;
		public readonly bool CrushInfantryTargets = true;
		[Desc("Test-only unsynced advanced-work pressure in milliseconds. Leave at zero outside isolated failsafe evidence maps.")]
		public readonly int FailsafeTestAdvancedWorkMilliseconds = 0;
		[Desc("First world tick for test-only advanced-work pressure.")]
		public readonly int FailsafeTestAdvancedWorkFromTick = 0;
		[Desc("Exclusive final world tick for test-only advanced-work pressure. Zero leaves it unbounded.")]
		public readonly int FailsafeTestAdvancedWorkUntilTick = 0;
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (UnitTypes.Count == 0 || string.IsNullOrWhiteSpace(SquadLabel) || ScanInterval <= 0 || OrderInterval <= 0 ||
				MaximumTargetCandidates <= 0 || MaximumHarassmentGroups <= 0 ||
				StealthTankSquadPolicy.SquadCount(MaximumHarassmentGroups, IncludeAttackGroup) >
					StealthTankSquadPolicy.MaximumSquadCount ||
				ThreatRangeBufferCells < 0 || DetectorRangeBufferCells < 0 ||
				KiteRangeMarginCells < 0 || CarefulClearValueRatio <= 0 || MinimumLateHarassmentGroupSize <= 0 ||
				TargetSwitchImprovementPercent < 0 || HarassmentDistancePenalty <= 0 ||
				ResourceWaitingSearchRadius < 0 || ResourceWaitingOrderInterval <= 0 ||
				PendingResourceExplosionAvoidanceRadius < 0 || HazardRouteWaypointSpacing <= 0 || StrategicCellSize < 0 ||
				MissionRetryInterval < 0 || NearbyTargetReactionRadiusCells < 0 ||
				(RetreatAfterReveal && StrategicCellSize != StealthTankSquadPolicy.RequiredStrategicCellSize) ||
				DefenderClearFallbackScans < 0 || DefenderClearValueRatio <= 0 ||
				DefenderClearWeakestCandidates <= 0 ||
				InfantryTargetPriority < 0 || WallTargetPriority < 0 || StructureTargetPriority < 0 ||
				TankTargetPriority < 0 || InfantryClusterRadiusCells < 0 || InfantryClusterBonusPercentPerNearbyActor < 0 ||
				MaximumInfantryClusterMultiplierPercent < 100 || FailsafeTestAdvancedWorkMilliseconds < 0 ||
				FailsafeTestAdvancedWorkFromTick < 0 || FailsafeTestAdvancedWorkUntilTick < 0 ||
				(FailsafeTestAdvancedWorkUntilTick > 0 &&
					FailsafeTestAdvancedWorkUntilTick <= FailsafeTestAdvancedWorkFromTick))
				throw new YamlException("Stealth squad types, labels, intervals, bounds, priorities, buffers, and ratios must be positive and valid.");
		}

		public override object Create(ActorInitializer init) { return new StealthTankSquadBotModule(init.Self, this); }
	}

	public class StealthTankSquadBotModule : ConditionalTrait<StealthTankSquadBotModuleInfo>,
		IBotEnabled, IBotTick, IBotUnitReservations, IAdvancedBotTick, IBotPerformanceIdentity, IGameSaveTraitData
	{
		sealed class SpecialistGroup
		{
			public readonly int Index;
			public readonly List<Actor> Units = new List<Actor>();
			public readonly HashSet<uint> Reinforcements = new HashSet<uint>();
			public Actor Target;
			public long TargetScore;
			public int LastOrderTick;
			public int LastNoTargetLogTick;
			public int ConsecutiveNoSafeTargetScans;
			public readonly Dictionary<uint, List<CPos>> RetainedRoutes = new Dictionary<uint, List<CPos>>();
			public readonly Dictionary<uint, int> RetainedRouteIndices = new Dictionary<uint, int>();
			public bool MembershipChanged;
			public bool HasPlan;
			public CPos PlannedTargetLocation;
			public int LastProgressTick;
			public long LastTargetDistanceSquared = long.MaxValue;
			public int LastTargetHp = int.MaxValue;
			public Actor SuspendedEngagementTarget;
			public Actor RetreatTarget;
			public readonly Dictionary<uint, CPos> RetreatDestinations = new Dictionary<uint, CPos>();

			public SpecialistGroup(int index) { Index = index; }
		}

		sealed class DefendedOpportunity
		{
			public Actor ProtectedTarget;
			public Actor ClearTarget;
			public SpecialistDefenderClearAction ClearAction;
			public int MinimumAttackRangeCells;
			public int DefendingValue;
			public long UnlockedScore;
		}

		sealed class Threat
		{
			public Actor Actor;
			public int WeaponRangeCells;
			public int DetectorRangeCells;
			public int Value;
			public bool WeaponIsEngaged;
		}

		sealed class StrategicView
		{
			public int Tick = int.MinValue;
			public List<Actor> Enemies;
			public List<Threat> Threats;
		}

		sealed class SpecialistInfluenceMap
		{
			public int Tick;
			public readonly int CoarseSize;
			public readonly int Width;
			public readonly int Height;
			public readonly float[] Danger;

			public SpecialistInfluenceMap(int tick, int coarseSize, int width, int height)
			{
				Tick = tick;
				CoarseSize = coarseSize;
				Width = width;
				Height = height;
				Danger = new float[width * height];
			}
		}

		const int LocalSafetySearchRadiusCells = 20;

		static readonly BitSet<TargetableType> TankTargetTypes = new BitSet<TargetableType>("Tank");
		static readonly BitSet<TargetableType> GroundTargetTypes = new BitSet<TargetableType>("Ground");
		static readonly BitSet<TargetableType> InfantryTargetTypes = new BitSet<TargetableType>("Infantry");
		static readonly BitSet<TargetableType> StructureTargetTypes = new BitSet<TargetableType>("Structure");

		readonly World world;
		readonly Player player;
		readonly HashSet<uint> reserved = new HashSet<uint>();
		readonly HashSet<uint> repairing = new HashSet<uint>();
		readonly Dictionary<uint, uint> repairTargets = new Dictionary<uint, uint>();
		readonly Dictionary<uint, int> nextRepairEvaluation = new Dictionary<uint, int>();
		readonly Dictionary<uint, bool> lastCloaked = new Dictionary<uint, bool>();
		readonly Dictionary<CPos, bool> resourceHazardCache = new Dictionary<CPos, bool>();
		StealthTankRetreatSaveGroup[] pendingRetreatRestore;
		StealthTankReinforcementSaveGroup[] pendingReinforcementRestore;
		readonly SpecialistGroup[] groups;
		readonly StrategicView strategicView = new StrategicView();
		SpecialistInfluenceMap influenceMap;
		IBot bot;
		SquadManagerBotModule squadManager;
		StealthTankSquadBotModule strategicViewOwner;
		IBotTransportReservations[] transportReservations;
		IUnassignedCombatUnitRegistry unassignedCombatUnits;
		IResourceLayer resourceLayer;
		DomainIndex domainIndex;
		int scanTicks;
		int localSafetyTicks;
		int lastEligibleCount = -1;
		bool advancedBehaviorEnabled = true;
		int scanPlanRetentions;
		int scanPlanInvalidations;
		int scanPathSearches;
		int scanQueuedOrders;
		int scanObservedTargetDamage;
		int scanCandidateThreatTests;
		int scanInfluenceCellTests;
		int scanResourceCellTests;
		long scanEnemySnapshotTicks;
		long scanThreatFactsTicks;
		long scanCandidateTicks;
		long scanCandidateThreatTicks;
		long scanDefendedFallbackTicks;
		long scanRetainedSafetyTicks;
		long scanThreatMapTicks;
		int scanInfluenceBuilds;
		int scanInfluenceHits;
		long scanPathTicks;
		long scanOrderTicks;

		public StealthTankSquadBotModule(Actor self, StealthTankSquadBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
			var groupCount = StealthTankSquadPolicy.SquadCount(
				info.MaximumHarassmentGroups, info.IncludeAttackGroup);
			groups = Enumerable.Range(0, groupCount).Select(i => new SpecialistGroup(i)).ToArray();
		}

		protected override void Created(Actor self)
		{
			RefreshSquadManager();
			strategicViewOwner = player.PlayerActor.TraitsImplementing<StealthTankSquadBotModule>()
				.FirstOrDefault() ?? this;
			transportReservations = self.Owner.PlayerActor.TraitsImplementing<IBotTransportReservations>().ToArray();
			unassignedCombatUnits = self.Owner.PlayerActor.TraitOrDefault<IUnassignedCombatUnitRegistry>();
			resourceLayer = world.WorldActor.TraitOrDefault<IResourceLayer>();
			domainIndex = world.WorldActor.TraitOrDefault<DomainIndex>();
			squadManager = player.PlayerActor.TraitsImplementing<SquadManagerBotModule>()
				.FirstOrDefault(s => !s.IsTraitDisabled);
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self)
		{
			// Establish reservations before the ordinary squad manager can claim newly available tanks.
			scanTicks = 1;
			localSafetyTicks = StealthTankSquadPolicy.NearbyReactionMaximumLatencyTicks;
		}

		protected override void TraitDisabled(Actor self)
		{
			ReleaseSpecialists("trait-disabled");
		}

		void ReleaseSpecialists(string reason)
		{
			var released = reserved.OrderBy(id => id).ToArray();
			var releasedActors = released.Select(player.World.GetActorById).Where(a => a != null).ToArray();
			unassignedCombatUnits?.RegisterReleasedActors(releasedActors);
			if (reason == "failsafe-degraded")
			{
				RefreshSquadManager();
				squadManager?.RetainFailsafeReleasedActors(
					$"StealthTankSquadBotModule/{Info.SquadLabel}", releasedActors);
			}

			reserved.Clear();
			repairing.Clear();
			repairTargets.Clear();
			nextRepairEvaluation.Clear();
			lastCloaked.Clear();
			pendingRetreatRestore = null;
			pendingReinforcementRestore = null;
			lastEligibleCount = -1;
			foreach (var group in groups)
			{
				group.Units.Clear();
				group.Reinforcements.Clear();
				group.Target = null;
				group.SuspendedEngagementTarget = null;
				group.RetreatTarget = null;
				group.RetreatDestinations.Clear();
				group.ConsecutiveNoSafeTargetScans = 0;
				ClearRetainedPlan(group);
			}

			if (Info.DebugLogging)
				Log.Write("debug", "AI stealth squads {0} [{1}] released: reason={2} count={3} actors={4}.",
					Info.SquadLabel, player.PlayerName, reason, released.Length, string.Join(",", released));
		}

		void RefreshSquadManager()
		{
			if (squadManager == null || squadManager.IsTraitDisabled)
				squadManager = player.PlayerActor.TraitsImplementing<SquadManagerBotModule>()
					.FirstOrDefault(s => !s.IsTraitDisabled);
		}

		void IBotEnabled.BotEnabled(IBot enabledBot) { bot = enabledBot; }

		bool IBotUnitReservations.IsUnitReserved(Actor actor)
		{
			return actor != null && StealthTankSquadPolicy.ShouldReserveUnit(
				reserved.Contains(actor.ActorID), Info.ClaimAllEligible, IsEligible(actor));
		}

		string IBotPerformanceIdentity.PerformanceIdentity =>
			$"{GetType().Name}/{Info.SquadLabel}";

		string IAdvancedBotTick.FailsafeModuleId =>
			$"{GetType().Name}/{Info.SquadLabel}";

		int StrategicCellSize => Info.StrategicCellSize > 0 ?
			Info.StrategicCellSize : Info.HazardRouteWaypointSpacing;

		void IAdvancedBotTick.SetAdvancedBehaviorEnabled(bool enabled)
		{
			if (advancedBehaviorEnabled == enabled)
				return;

			advancedBehaviorEnabled = enabled;
			if (enabled)
			{
				scanTicks = 1;
				if (Info.DebugLogging)
					Log.Write("debug", "AI stealth squads {0} [{1}] enabled for recovery probe.",
						Info.SquadLabel, player.PlayerName);
			}
			else
				ReleaseSpecialists("failsafe-degraded");
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (IsTraitDisabled || !advancedBehaviorEnabled)
				return;

			RunFailsafeTestPressure();

			// Dispatch is O(1) except at the explicit 25-tick engagement-safety or
			// configured 75-tick strategic boundaries.
			if (--localSafetyTicks <= 0)
			{
				localSafetyTicks = StealthTankSquadPolicy.NearbyReactionMaximumLatencyTicks;
				var localSafetyStarted = Stopwatch.GetTimestamp();
				var localSafetyOrders = RunEngagementSafety();
				localSafetyOrders += RunNearbyTargetReaction();
				if (Game.IsBenchmarking)
					RecordPhase("local-safety", Stopwatch.GetTimestamp() - localSafetyStarted, localSafetyOrders);
			}

			if (!StealthTankSquadPolicy.ShouldRunStrategicScan(ref scanTicks, Info.ScanInterval))
				return;
			var scanStarted = Stopwatch.GetTimestamp();

			resourceHazardCache.Clear();
			Rebalance();
			if (reserved.Count == 0)
				return;

			scanPlanRetentions = 0;
			scanPlanInvalidations = 0;
			scanPathSearches = 0;
			scanQueuedOrders = 0;
			scanObservedTargetDamage = 0;
			scanCandidateThreatTests = 0;
			scanInfluenceCellTests = 0;
			scanResourceCellTests = 0;
			scanEnemySnapshotTicks = 0;
			scanThreatFactsTicks = 0;
			scanCandidateTicks = 0;
			scanCandidateThreatTicks = 0;
			scanDefendedFallbackTicks = 0;
			scanRetainedSafetyTicks = 0;
			scanThreatMapTicks = 0;
			scanInfluenceBuilds = 0;
			scanInfluenceHits = 0;
			scanPathTicks = 0;
			scanOrderTicks = 0;
			var view = strategicViewOwner.GetStrategicView(out var viewHit);
			foreach (var group in groups)
				UpdateGroup(group, view.Enemies, view.Threats);

			var scanElapsed = Stopwatch.GetTimestamp() - scanStarted;
			if (Game.IsBenchmarking)
			{
				RecordPhase("scan-total", scanElapsed, scanQueuedOrders);
				RecordPhase("enemy-snapshot", scanEnemySnapshotTicks);
				RecordPhase("threat-facts", scanThreatFactsTicks);
				RecordPhase("candidate-collection", scanCandidateTicks);
				RecordPhase("candidate-threat-tests", scanCandidateThreatTicks);
				RecordPhase("defended-fallback", scanDefendedFallbackTicks);
				RecordPhase("retained-safety", scanRetainedSafetyTicks);
				RecordPhase("threat-map", scanThreatMapTicks);
				RecordPhase("coarse-route", scanPathTicks);
				RecordPhase("orders", scanOrderTicks, scanQueuedOrders);
			}

			if (Info.DebugLogging)
				Log.Write("debug", "AI stealth performance {0} [{1}] tick={2}: view={3} enemies={4} threats={5} retained-plans={6} invalidated-plans={7} route-searches={8} queued-orders={9} observed-target-damage={10} phases-ms=snapshot:{11:0.###},facts:{12:0.###},candidates:{13:0.###},candidate-threats:{14:0.###},fallback:{15:0.###},retained-safety:{16:0.###},threat-map:{17:0.###},coarse-route:{18:0.###},orders:{19:0.###} tests=candidate-threat:{20},influence-cell:{21},resource-cell:{22} influence=build:{23},hit:{24},size:{25}x{26}.",
					Info.SquadLabel, player.PlayerName, world.WorldTick, viewHit ? "hit" : "build",
					view.Enemies.Count, view.Threats.Count, scanPlanRetentions, scanPlanInvalidations,
					scanPathSearches, scanQueuedOrders, scanObservedTargetDamage,
					Milliseconds(scanEnemySnapshotTicks), Milliseconds(scanThreatFactsTicks),
					Milliseconds(scanCandidateTicks), Milliseconds(scanCandidateThreatTicks),
					Milliseconds(scanDefendedFallbackTicks), Milliseconds(scanRetainedSafetyTicks),
					Milliseconds(scanThreatMapTicks), Milliseconds(scanPathTicks), Milliseconds(scanOrderTicks),
					scanCandidateThreatTests, scanInfluenceCellTests, scanResourceCellTests,
					scanInfluenceBuilds, scanInfluenceHits, influenceMap?.Width ?? 0, influenceMap?.Height ?? 0);
		}

		void RecordPhase(string phase, long ticks, int orders = 0)
		{
			Game.RecordBotModuleSample(player.ClientIndex,
				$"Specialist/{Info.SquadLabel}/{phase}", Milliseconds(ticks), orders);
		}

		int RunNearbyTargetReaction()
		{
			if (Info.NearbyTargetReactionRadiusCells <= 0)
				return 0;

			var ordersBefore = scanQueuedOrders;
			foreach (var group in groups)
			{
				if (group.SuspendedEngagementTarget != null || StealthTankSquadPolicy.ShouldBlockReassessment(
					group.RetreatDestinations.Count))
					continue;

				var active = group.Units.Where(a => IsActiveCoreSpecialist(group, a)).ToArray();
				if (active.Length == 0)
					continue;

				var center = active.Select(a => a.CenterPosition).Average();
				var nearby = world.FindActorsInCircle(center,
					WDist.FromCells(Info.NearbyTargetReactionRadiusCells))
					.Where(IsEnemyTarget).OrderBy(a => a.ActorID).ToList();
				if (nearby.Count == 0)
					continue;

				// A retained nearby mission already satisfies the bounded reaction policy.
				// Observe it without replacing the target or reissuing its orders.
				if (group.Target != null && nearby.Contains(group.Target))
				{
					if (Info.DebugLogging)
						Log.Write("debug", "AI stealth squad {0} [{1}:{2}] nearby reaction tick={3}: target={4}#{5} distance={6} radius={7} bounded-latency={8} retained=true order-churn=false.",
							Info.SquadLabel, player.PlayerName, group.Index, world.WorldTick,
							group.Target.Info.Name, group.Target.ActorID,
							(group.Target.CenterPosition - center).Length / 1024,
							Info.NearbyTargetReactionRadiusCells,
							StealthTankSquadPolicy.NearbyReactionMaximumLatencyTicks);
					continue;
				}

				var previousTarget = group.Target;
				var view = strategicViewOwner.GetStrategicView(out _);
				var localCandidates = StealthTankSquadPolicy.NearbyReassessmentCandidates(
					nearby, IsEnemyTarget(group.Target) ? group.Target : null, (a, b) => a == b);
				UpdateGroup(group, localCandidates, view.Threats);
				var currentTarget = group.Target;
				if (previousTarget == null && currentTarget != null && Info.DebugLogging)
					Log.Write("debug", "AI stealth squad {0} [{1}:{2}] nearby reaction tick={3}: target={4}#{5} distance={6} radius={7} bounded-latency={8}.",
						Info.SquadLabel, player.PlayerName, group.Index, world.WorldTick,
						currentTarget.Info.Name, currentTarget.ActorID,
						(currentTarget.CenterPosition - center).Length / 1024,
						Info.NearbyTargetReactionRadiusCells,
						StealthTankSquadPolicy.NearbyReactionMaximumLatencyTicks);
				else if (previousTarget != null && currentTarget != null &&
					currentTarget != previousTarget && Info.DebugLogging)
					Log.Write("debug", "AI stealth squad {0} [{1}:{2}] nearby reaction tick={3}: switched incumbent={4}#{5} to target={6}#{7} distance={8} radius={9} bounded-latency={10} threshold={11}% stop=false cancel=false idle-gap=false.",
						Info.SquadLabel, player.PlayerName, group.Index, world.WorldTick,
						previousTarget.Info.Name, previousTarget.ActorID,
						currentTarget.Info.Name, currentTarget.ActorID,
						(currentTarget.CenterPosition - center).Length / 1024,
						Info.NearbyTargetReactionRadiusCells,
						StealthTankSquadPolicy.NearbyReactionMaximumLatencyTicks,
						Info.TargetSwitchImprovementPercent);
			}

			return scanQueuedOrders - ordersBefore;
		}

		int RunEngagementSafety()
		{
			var orders = 0;
			var detectorFound = false;
			foreach (var unit in reserved.Select(world.GetActorById).Where(IsEligible).OrderBy(a => a.ActorID))
				orders += UpdateRepairLifecycle(unit);

			foreach (var group in groups)
			{
				if (UpdateStrategicRetreat(group, out var retreatOrders))
				{
					orders += retreatOrders;
					continue;
				}

				var activeSpecialists = group.Units.Where(a => IsActiveCoreSpecialist(group, a)).OrderBy(a => a.ActorID).ToArray();
				var missionTarget = group.Target;
				var missionTargetValid = IsEnemyTarget(missionTarget);
				if (StealthTankSquadPolicy.ShouldBeginPostMissionRetreat(Info.RetreatAfterReveal,
					missionTarget != null, missionTargetValid))
				{
					orders += BeginStrategicRetreat(group, activeSpecialists, missionTarget,
						Array.Empty<Actor>(), "target-complete");
					continue;
				}

				var newlyRevealed = activeSpecialists.Where(WasNewlyRevealed).ToArray();
				if (Info.RetreatAfterReveal && newlyRevealed.Length > 0 && missionTargetValid && Info.DebugLogging)
					Log.Write("debug", "AI stealth squad {0} [{1}:{2}] reveal retained tick={3}: target={4}#{5} revealed={6} finish-target=true retreat-deferred=true stop=false cancel=false idle-gap=false.",
						Info.SquadLabel, player.PlayerName, group.Index, world.WorldTick,
						missionTarget.Info.Name, missionTarget.ActorID,
						string.Join(",", newlyRevealed.Select(a => a.ActorID)));

				var wasSuspended = group.SuspendedEngagementTarget != null;
				var engaged = group.Units.Where(a => IsActiveCoreSpecialist(group, a) && a.CurrentActivity != null &&
					a.CurrentActivity.ActivitiesImplementing<IActivityNotifyStanceChanged>().Any()).ToArray();
				if (wasSuspended)
					engaged = group.Units.Where(a => IsActiveCoreSpecialist(group, a)).ToArray();
				if (engaged.Length == 0)
					continue;

				var suspendedThreatRemains = false;
				var suspendedResourceHazard = false;
				foreach (var unit in engaged)
				{
					var localThreatExposure = HasLocalThreatExposure(unit, out var detector,
						out var detectorRange, out var armedSupport, out var armedRange,
						out var engagedWeaponExposure);
					var detectorExposure = detector != null;
					var armedCoverage = armedSupport != null;
					var blueAdjacent = Info.AvoidResourceTypes.Count > 0 && resourceLayer != null &&
						world.Map.FindTilesInAnnulus(unit.Location, 0, 1).Any(c =>
						{
							var type = resourceLayer.GetResource(c).Type;
							return type != null && Info.AvoidResourceTypes.Contains(type);
						});
					if (!localThreatExposure && !blueAdjacent)
						continue;

					if (wasSuspended)
					{
						suspendedThreatRemains |= localThreatExposure;
						suspendedResourceHazard |= blueAdjacent;
						continue;
					}

					bot.QueueOrder(new Order("Stop", unit, false));
					orders++;
					detectorFound |= detectorExposure;
					if (Info.DebugLogging)
					{
						var activity = unit.CurrentActivity?.GetType().Name ?? "none";
						var armament = string.Join(",", unit.TraitsImplementing<Armament>()
							.Where(a => !a.IsTraitDisabled).Select(a =>
								$"{a.Info.Weapon}:reload={a.IsReloading}:delay={a.FireDelay}:burst={a.Burst}"));
						Log.Write("debug", "AI stealth local safety {0} [{1}:{2}] tick={3} stopped {4}#{5}: activity={6} armament={7} detector={8} armed-coverage={9} engaged-weapon={10} blue-adjacent={11} detector-source={12} detector-owner={13} detector-buffered-range={14} armed-source={15} armed-owner={16} armed-buffered-range={17}.",
							Info.SquadLabel, player.PlayerName, group.Index, world.WorldTick, unit.Info.Name, unit.ActorID,
							activity, armament,
							detectorExposure, armedCoverage, engagedWeaponExposure, blueAdjacent,
							detector == null ? "none" : detector.Info.Name + "#" + detector.ActorID,
							detector == null ? "none" : detector.Owner.InternalName, detectorRange,
							armedSupport == null ? "none" : armedSupport.Info.Name + "#" + armedSupport.ActorID,
							armedSupport == null ? "none" : armedSupport.Owner.InternalName, armedRange);
					}

					if (detectorExposure && armedCoverage)
					{
						group.SuspendedEngagementTarget = group.Target;
						ClearRetainedPlan(group);
					}
					else
					{
						group.Target = null;
						ClearRetainedPlan(group);
					}
				}

				var suspendedTarget = group.SuspendedEngagementTarget;
				var validSuspendedTarget = IsEnemyTarget(suspendedTarget);
				if (StealthTankSquadPolicy.ShouldResumeSuspendedEngagement(wasSuspended, validSuspendedTarget,
					suspendedThreatRemains, suspendedResourceHazard))
				{
					var units = group.Units.Where(a => IsActiveCoreSpecialist(group, a)).ToArray();
					if (units.Length == 0)
						continue;
					bot.QueueOrder(new Order("Attack", null, Target.FromActor(suspendedTarget), false,
						groupedActors: units));
					orders++;
					group.Target = suspendedTarget;
					group.SuspendedEngagementTarget = null;
					BeginRetainedPlan(group, suspendedTarget,
						units.Select(a => a.CenterPosition).Average());
					if (Info.DebugLogging)
						Log.Write("debug", "AI stealth local safety {0} [{1}:{2}] tick={3} resumed target {4}#{5}: detector-only-or-clear=true units={6} activities={7}.",
							Info.SquadLabel, player.PlayerName, group.Index, world.WorldTick,
							suspendedTarget.Info.Name, suspendedTarget.ActorID, units.Length,
							string.Join(",", units.Select(a => a.Info.Name + "#" + a.ActorID + ":" +
								(a.CurrentActivity?.GetType().Name ?? "none"))));
				}
				else if (group.SuspendedEngagementTarget != null && !validSuspendedTarget)
				{
					group.SuspendedEngagementTarget = null;
					group.Target = null;
					ClearRetainedPlan(group);
				}
			}

			// A detector found by the bounded engagement check may have appeared after
			// the shared strategic snapshot. Force the next slow scan to see it rather
			// than briefly reissuing the invalidated route from stale coarse facts.
			if (detectorFound)
			{
				strategicViewOwner.strategicView.Tick = int.MinValue;
				influenceMap = null;
			}

			return orders;
		}

		bool WasNewlyRevealed(Actor unit)
		{
			var cloaks = unit.TraitsImplementing<Cloak>().Where(c => !c.IsTraitDisabled).ToArray();
			if (cloaks.Length == 0)
				return false;

			var cloaked = cloaks.Any(c => c.Cloaked);
			var hadPrevious = lastCloaked.TryGetValue(unit.ActorID, out var previous);
			lastCloaked[unit.ActorID] = cloaked;
			return hadPrevious && previous && !cloaked;
		}

		int BeginStrategicRetreat(SpecialistGroup group, Actor[] units, Actor target,
			Actor[] triggeringUnits, string reason)
		{
			group.RetreatTarget = target;
			group.RetreatDestinations.Clear();
			group.Target = null;
			group.SuspendedEngagementTarget = null;
			ClearRetainedPlan(group);
			var orders = 0;
			foreach (var unit in units)
			{
				var destination = FindStrategicRetreatDestination(unit, target.Location);
				if (destination == null)
					continue;

				group.RetreatDestinations[unit.ActorID] = destination.Value;
				bot.QueueOrder(new Order("Move", unit, Target.FromCell(world, destination.Value), false));
				orders++;
			}

			group.LastOrderTick = world.WorldTick;
			if (Info.DebugLogging)
			{
				var geometry = group.RetreatDestinations.OrderBy(kv => kv.Key).Select(kv =>
				{
					var from = StealthTankSquadPolicy.StrategicCell(world.GetActorById(kv.Key).Location,
						StrategicCellSize);
					var to = StealthTankSquadPolicy.StrategicCell(kv.Value, StrategicCellSize);
					var delta = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
					return kv.Key + ":" + from + ">" + to + ":delta=" + delta;
				}).ToArray();
				Log.Write("debug", "AI stealth squad {0} [{1}:{2}] post-mission retreat tick={3}: target={4}#{5} reason={6} trigger-units={7} ordered={8} strategic-size={9} all-one-cell={10} geometry={11} destinations={12}.",
					Info.SquadLabel, player.PlayerName, group.Index, world.WorldTick, target.Info.Name, target.ActorID,
					reason, string.Join(",", triggeringUnits.Select(a => a.ActorID)), orders, StrategicCellSize,
					geometry.Length == orders && geometry.All(g => g.EndsWith("delta=1")),
					string.Join(",", geometry),
					string.Join(",", group.RetreatDestinations.OrderBy(kv => kv.Key)
						.Select(kv => kv.Key + ":" + kv.Value)));
			}

			return orders;
		}

		bool UpdateStrategicRetreat(SpecialistGroup group, out int orders)
		{
			orders = 0;
			if (!StealthTankSquadPolicy.ShouldBlockReassessment(group.RetreatDestinations.Count))
				return false;

			foreach (var actorId in group.RetreatDestinations.Keys.ToArray())
			{
				var unit = world.GetActorById(actorId);
				var eligible = IsEligible(unit);
				var reachedDestination = eligible && StealthTankSquadPolicy.IsSameStrategicCell(
					unit.Location, group.RetreatDestinations[actorId], StrategicCellSize);
				if (StealthTankSquadPolicy.IsRetreatResponsibilityResolved(
					eligible, repairing.Contains(actorId), reachedDestination))
					group.RetreatDestinations.Remove(actorId);
			}

			var retreatTarget = group.RetreatTarget;
			var retreatTargetValid = IsEnemyTarget(retreatTarget);
			var completion = StealthTankSquadPolicy.CompleteRetreat(
				group.RetreatDestinations.Count, retreatTargetValid);
			if (completion == StealthTankRetreatCompletion.ContinueRetreat)
				return true;

			group.Target = completion == StealthTankRetreatCompletion.ReassessWithIncumbent ?
				retreatTarget : null;
			if (Info.DebugLogging)
				Log.Write("debug", "AI stealth squad {0} [{1}:{2}] retreat complete tick={3}: strategic-size={4}; retreat-target={5} target-valid={6} reassessment enabled incumbent={7} completion={8} target-loss=false stop=false cancel=false idle-gap=false.",
					Info.SquadLabel, player.PlayerName, group.Index, world.WorldTick, StrategicCellSize,
					retreatTarget == null ? "none" : retreatTarget.Info.Name + "#" + retreatTarget.ActorID,
					retreatTargetValid,
					group.Target == null ? "none" : group.Target.Info.Name + "#" + group.Target.ActorID,
					completion);
			group.RetreatTarget = null;
			scanTicks = 1;
			return false;
		}

		CPos? FindStrategicRetreatDestination(Actor unit, CPos target)
		{
			var mobile = unit.TraitOrDefault<Mobile>();
			if (mobile == null)
				return null;

			var desired = StealthTankSquadPolicy.OneStrategicCellRetreat(unit.Location, target,
				StrategicCellSize, world.Map.MapSize.X, world.Map.MapSize.Y);
			var coarseX = desired.X / StrategicCellSize;
			var coarseY = desired.Y / StrategicCellSize;
			return Enumerable.Range(coarseY * StrategicCellSize, StrategicCellSize)
				.SelectMany(y => Enumerable.Range(coarseX * StrategicCellSize, StrategicCellSize)
					.Select(x => new CPos(x, y))).Where(world.Map.Contains)
				.Where(c => mobile.CanEnterCell(c) &&
					(domainIndex == null || domainIndex.IsPassable(unit.Location, c, mobile.Locomotor)))
				.OrderBy(c => (c - desired).LengthSquared).ThenBy(c => c.Y).ThenBy(c => c.X)
				.Cast<CPos?>().FirstOrDefault();
		}

		int UpdateRepairLifecycle(Actor unit)
		{
			var health = unit.TraitOrDefault<IHealth>();
			RefreshSquadManager();
			var threshold = squadManager?.Info.HealthRetreatThreshold ?? 0f;
			if (health == null || threshold <= 0)
				return 0;

			var wasRepairing = repairing.Contains(unit.ActorID);
			var fullyRepaired = health.HP >= health.MaxHP;
			var damaged = health.HP < health.MaxHP * threshold;
			if (wasRepairing && fullyRepaired)
			{
				repairing.Remove(unit.ActorID);
				repairTargets.Remove(unit.ActorID);
				nextRepairEvaluation.Remove(unit.ActorID);
				foreach (var group in groups.Where(g => g.Units.Contains(unit)))
				{
					if (group.Units.Any(a => a != unit &&
						IsActiveCoreSpecialist(group, a)))
						group.Reinforcements.Add(unit.ActorID);
					else
						group.MembershipChanged = true;
				}

				if (Info.DebugLogging)
					Log.Write("debug", "AI stealth repair {0} [{1}] {2}#{3}: fully repaired; rejoined active squad.",
						Info.SquadLabel, player.PlayerName, unit.Info.Name, unit.ActorID);
				return ResumeRetreatResponsibility(unit, "fully-repaired");
			}

			if (wasRepairing && repairTargets.TryGetValue(unit.ActorID, out var currentTargetId) &&
				world.GetActorById(currentTargetId) is Actor currentTarget && currentTarget.Owner.IsAlliedWith(player) &&
				currentTarget.IsInWorld && !currentTarget.IsDead && !unit.IsIdle)
				return 0;

			if (!wasRepairing && damaged && nextRepairEvaluation.TryGetValue(unit.ActorID, out var nextTick) &&
				world.WorldTick < nextTick)
				return 0;

			nextRepairEvaluation[unit.ActorID] = world.WorldTick + 125;
			Actor facility = null;
			var route = damaged || wasRepairing ? FindRepairRoute(unit, out facility) : null;
			var disposition = StealthTankSquadPolicy.RepairDisposition(damaged, wasRepairing,
				fullyRepaired, route != null && facility != null);
			if (disposition != SpecialistRepairDisposition.Repair)
			{
				var resumedRetreatOrders = 0;
				if (wasRepairing)
				{
					repairing.Remove(unit.ActorID);
					repairTargets.Remove(unit.ActorID);
					foreach (var group in groups.Where(g => g.Units.Contains(unit)))
						group.MembershipChanged = true;
					resumedRetreatOrders = ResumeRetreatResponsibility(unit, "repair-canceled");
				}

				if (damaged && Info.DebugLogging)
					Log.Write("debug", "AI stealth repair {0} [{1}] {2}#{3}: {4}/{5} HP, no compatible reachable safe repair path; staying active.",
						Info.SquadLabel, player.PlayerName, unit.Info.Name, unit.ActorID, health.HP, health.MaxHP);
				return resumedRetreatOrders;
			}

			if (wasRepairing && repairTargets.TryGetValue(unit.ActorID, out var targetId) &&
				targetId == facility.ActorID && !unit.IsIdle)
				return 0;

			var queued = false;
			foreach (var waypoint in route)
			{
				bot.QueueOrder(new Order("Move", unit, Target.FromCell(world, waypoint), queued));
				queued = true;
			}

			bot.QueueOrder(new Order("Repair", unit, Target.FromActor(facility), queued));

			repairing.Add(unit.ActorID);
			repairTargets[unit.ActorID] = facility.ActorID;
			foreach (var group in groups.Where(g => g.Units.Contains(unit)))
				group.MembershipChanged = true;
			if (Info.DebugLogging)
				Log.Write("debug", "AI stealth repair {0} [{1}] {2}#{3}: {4}/{5} HP, moving by {6} waypoint(s) to compatible safe repair aura {7}#{8}; queued Repair order; retreat-pending={9} destinations={10}.",
					Info.SquadLabel, player.PlayerName, unit.Info.Name, unit.ActorID, health.HP,
					health.MaxHP, route.Count, facility.Info.Name, facility.ActorID,
					groups.Any(g => g.RetreatDestinations.ContainsKey(unit.ActorID)),
					string.Join(",", groups.Where(g => g.RetreatDestinations.ContainsKey(unit.ActorID))
						.Select(g => g.Index + ":" + g.RetreatDestinations[unit.ActorID])));
			return route.Count + 1;
		}

		int ResumeRetreatResponsibility(Actor unit, string reason)
		{
			var orders = 0;
			foreach (var group in groups.Where(g => g.RetreatDestinations.TryGetValue(
				unit.ActorID, out _)))
			{
				var destination = group.RetreatDestinations[unit.ActorID];
				if (StealthTankSquadPolicy.IsSameStrategicCell(
					unit.Location, destination, StrategicCellSize))
					continue;

				bot.QueueOrder(new Order("Move", unit, Target.FromCell(world, destination), false));
				orders++;
				if (Info.DebugLogging)
					Log.Write("debug", "AI stealth retreat repair-resume {0} [{1}:{2}] tick={3}: unit={4} reason={5} destination={6} barrier=True.",
						Info.SquadLabel, player.PlayerName, group.Index, world.WorldTick,
						unit.ActorID, reason, destination);
			}

			return orders;
		}

		List<CPos> FindRepairRoute(Actor unit, out Actor facility)
		{
			facility = null;
			var repairable = unit.Info.TraitInfoOrDefault<RepairableInfo>();
			var mobile = unit.TraitOrDefault<Mobile>();
			if (repairable == null || repairable.RepairActors.Count == 0 || mobile == null)
				return null;

			var map = StealthTankSquadPolicy.ResolveRepairInfluence(
				strategicViewOwner.strategicView.Threats, threats => GetInfluenceMap(threats));
			if (map == null)
				return null;

			List<CPos> bestRoute = null;
			foreach (var candidate in world.ActorsHavingTrait<RepairsUnits>()
				.Where(a => !a.IsDead && a.IsInWorld && a.Owner.IsAlliedWith(player) &&
					repairable.RepairActors.Contains(a.Info.Name))
				.OrderBy(a => a.Owner == player ? 0 : 1).ThenBy(a => a.ActorID))
			{
				foreach (var destination in world.Map.FindTilesInAnnulus(candidate.Location, 1, 6)
					.Where(c => mobile.CanEnterCell(c) && !IsResourceHazard(c) && !IsInfluencedCell(map, c) &&
						(domainIndex == null || domainIndex.IsPassable(unit.Location, c, mobile.Locomotor)))
					.OrderBy(c => (c - unit.Location).LengthSquared).ThenBy(c => c.Y).ThenBy(c => c.X))
				{
					var route = FindCoarseSafeRoute(unit.Location, destination, map);
					if (route == null || (bestRoute != null && route.Count >= bestRoute.Count))
						continue;

					facility = candidate;
					bestRoute = route;
					break;
				}
			}

			return bestRoute;
		}

		bool IsActiveSpecialist(Actor actor)
		{
			return IsEligible(actor) && !repairing.Contains(actor.ActorID);
		}

		bool IsActiveCoreSpecialist(SpecialistGroup group, Actor actor)
		{
			return IsActiveSpecialist(actor) && !group.Reinforcements.Contains(actor.ActorID);
		}

		bool HasLocalThreatExposure(Actor unit, out Actor detector, out int detectorRange,
			out Actor armedSupport, out int armedRange, out bool engagedWeaponExposure)
		{
			detector = null;
			detectorRange = 0;
			armedSupport = null;
			armedRange = 0;
			engagedWeaponExposure = false;
			foreach (var actor in world.FindActorsInCircle(unit.CenterPosition,
				WDist.FromCells(LocalSafetySearchRadiusCells)).Where(IsEnemyTarget).OrderBy(a => a.ActorID))
			{
				var threat = CreateThreat(actor);
				if (threat == null)
					continue;

				var bufferedDetectorRange = StealthTankSquadPolicy.BufferedRange(threat.DetectorRangeCells,
					Info.DetectorRangeBufferCells);
				var ignoreWeapon = actor.GetEnabledTargetTypes().Overlaps(Info.IgnoredHarassmentWeaponThreatTypes);
				var weaponRange = StealthTankSquadPolicy.BufferedRange(
					ignoreWeapon ? 0 : threat.WeaponRangeCells, Info.ThreatRangeBufferCells);
				var distance = (actor.CenterPosition - unit.CenterPosition).Length;
				if (detector == null && bufferedDetectorRange > 0 && distance <= bufferedDetectorRange * 1024)
				{
					detector = actor;
					detectorRange = bufferedDetectorRange;
				}

				if (armedSupport == null && weaponRange > 0 && distance <= weaponRange * 1024)
				{
					armedSupport = actor;
					armedRange = weaponRange;
				}

				engagedWeaponExposure |= threat.WeaponIsEngaged && weaponRange > 0 &&
					distance <= weaponRange * 1024;
			}

			return StealthTankSquadPolicy.IsEngagementThreat(detector != null,
				armedSupport != null, engagedWeaponExposure);
		}

		static double Milliseconds(long ticks)
		{
			return ticks * 1000.0 / Stopwatch.Frequency;
		}

		StrategicView GetStrategicView(out bool cacheHit)
		{
			cacheHit = !StealthTankSquadPolicy.ShouldRefreshStrategicView(strategicView.Tick, world.WorldTick);
			if (cacheHit)
				return strategicView;

			strategicView.Tick = world.WorldTick;
			var started = Stopwatch.GetTimestamp();
			strategicView.Enemies = world.Actors.Where(IsEnemyTarget).OrderBy(a => a.ActorID).ToList();
			scanEnemySnapshotTicks += Stopwatch.GetTimestamp() - started;

			// Share bounded facts, not positions, target types, profile scores, or risk judgments.
			// Late-built detectors and defenses remain represented because the view is never truncated.
			started = Stopwatch.GetTimestamp();
			strategicView.Threats = strategicView.Enemies.Select(CreateThreat).Where(t => t != null).ToList();
			scanThreatFactsTicks += Stopwatch.GetTimestamp() - started;
			return strategicView;
		}

		void RunFailsafeTestPressure()
		{
			if (Info.FailsafeTestAdvancedWorkMilliseconds == 0 ||
				world.WorldTick < Info.FailsafeTestAdvancedWorkFromTick ||
				(Info.FailsafeTestAdvancedWorkUntilTick > 0 &&
					world.WorldTick >= Info.FailsafeTestAdvancedWorkUntilTick))
				return;

			var deadline = Stopwatch.GetTimestamp() +
				(long)Info.FailsafeTestAdvancedWorkMilliseconds * Stopwatch.Frequency / 1000;
			while (Stopwatch.GetTimestamp() < deadline)
			{
			}
		}

		bool IsTransportReserved(Actor actor)
		{
			return transportReservations != null && transportReservations.Any(r => r.IsTransportReserved(actor));
		}

		bool IsEligible(Actor actor)
		{
			return actor != null && actor.Owner == player && actor.IsInWorld && !actor.IsDead &&
				Info.UnitTypes.Contains(actor.Info.Name) && !IsTransportReserved(actor);
		}

		void Rebalance()
		{
			var previousCoreGroups = groups.Select(g => g.Units.Where(a =>
				!g.Reinforcements.Contains(a.ActorID)).Select(a => a.ActorID).ToArray()).ToArray();
			var previousMembership = groups.SelectMany(g => g.Units.Select(a =>
				new { a.ActorID, g.Index, Reinforcement = g.Reinforcements.Contains(a.ActorID) }))
				.ToDictionary(x => x.ActorID);
			var eligible = world.Actors.Where(IsEligible).OrderBy(a => a.ActorID).ToList();
			var selectedIds = StealthTankSquadPolicy.SelectSpecialistIds(
				eligible.Select(a => a.ActorID), reserved, Info.ReserveOpeningPair, Info.ClaimAllEligible);
			var eligibleById = eligible.ToDictionary(a => a.ActorID);
			var selected = selectedIds.Select(id => eligibleById[id]).ToList();
			var eligibleIds = new HashSet<uint>(eligible.Select(a => a.ActorID));
			foreach (var actorId in lastCloaked.Keys.Where(id => !eligibleIds.Contains(id)).ToArray())
				lastCloaked.Remove(actorId);
			foreach (var actor in selected.Where(a => !lastCloaked.ContainsKey(a.ActorID)))
			{
				var cloaks = actor.TraitsImplementing<Cloak>().Where(c => !c.IsTraitDisabled).ToArray();
				if (cloaks.Length > 0)
					lastCloaked.Add(actor.ActorID, cloaks.Any(c => c.Cloaked));
			}

			var previous = new HashSet<uint>(reserved);
			reserved.Clear();
			foreach (var actor in selected)
			{
				reserved.Add(actor.ActorID);
				unassignedCombatUnits?.ClaimActors(new[] { actor });
			}

			var hasEstablishedCore = previousCoreGroups.Any(ids => ids.Any(selectedIds.Contains));
			foreach (var group in groups)
			{
				group.Units.Clear();
				group.Reinforcements.Clear();
			}

			for (var i = 0; i < selected.Count; i++)
			{
				var wasPreviouslyAssigned = previousMembership.TryGetValue(selected[i].ActorID, out var membership);
				var groupIndex = wasPreviouslyAssigned ? membership.Index :
					StealthTankSquadPolicy.GroupForIndex(i, selected.Count,
						Info.MaximumHarassmentGroups, Info.IncludeAttackGroup);
				if (!wasPreviouslyAssigned && hasEstablishedCore)
					groupIndex = StealthTankSquadPolicy.ReinforcementGroup(groupIndex,
						previousCoreGroups.Select(ids => ids.Count(selectedIds.Contains)).ToArray());
				if (groupIndex >= 0)
				{
					groups[groupIndex].Units.Add(selected[i]);
					if ((wasPreviouslyAssigned && membership.Reinforcement) ||
						StealthTankSquadPolicy.ShouldStageReinforcement(
							hasEstablishedCore, wasPreviouslyAssigned))
						groups[groupIndex].Reinforcements.Add(selected[i].ActorID);
				}
			}

			ApplyPendingReinforcementRestore();

			// A group whose complete core was destroyed needs one survivor/replacement to
			// establish a formation that the remaining staged units can safely join. Run
			// this after restore so a staged-only saved group cannot wait forever.
			foreach (var group in groups)
			{
				var recoveryCore = StealthTankSquadPolicy.RecoveryCore(
					group.Units.Select(a => a.ActorID), group.Reinforcements);
				if (recoveryCore != null)
					group.Reinforcements.Remove(recoveryCore.Value);
			}

			for (var i = 0; i < groups.Length; i++)
				groups[i].MembershipChanged = !previousCoreGroups[i].SequenceEqual(groups[i].Units
					.Where(a => !groups[i].Reinforcements.Contains(a.ActorID)).Select(a => a.ActorID));

			ApplyPendingRetreatRestore();

			foreach (var group in groups)
				if (group.Target != null && (!group.Target.IsInWorld || group.Target.IsDead ||
					player.RelationshipWith(group.Target.Owner) != PlayerRelationship.Enemy))
				{
					group.Target = null;
					ClearRetainedPlan(group);
				}

			foreach (var group in groups)
				foreach (var actorId in group.RetreatDestinations.Keys
					.Where(id => !group.Units.Any(a => a.ActorID == id)).ToArray())
					group.RetreatDestinations.Remove(actorId);

			if (Info.DebugLogging && (eligible.Count != lastEligibleCount || !previous.SetEquals(reserved)))
				Log.Write("debug", "AI stealth squads {0} [{1}]: total={2} reserved={3} groups={4} ordinary={5} actors={6}.",
					Info.SquadLabel, player.PlayerName, eligible.Count, reserved.Count,
					string.Join("/", groups.Select(g => g.Units.Count)), eligible.Count - reserved.Count,
					string.Join(",", selected.Select(a => a.ActorID)));

			lastEligibleCount = eligible.Count;
		}

		void UpdateReinforcements(SpecialistGroup group, List<Threat> threats)
		{
			var core = group.Units.Where(a => IsActiveCoreSpecialist(group, a))
				.OrderBy(a => a.ActorID).ToArray();
			if (core.Length == 0 || group.Reinforcements.Count == 0)
				return;

			// Snapshot before promotion so one arrival cannot pull the join boundary toward
			// another. This mirrors Air's stable formation/destination admission test.
			var coreLocation = new CPos((int)core.Average(a => a.Location.X),
				(int)core.Average(a => a.Location.Y));
			var missionLocation = IsEnemyTarget(group.Target) ? group.Target.Location :
				group.HasPlan ? group.PlannedTargetLocation : (CPos?)null;
			foreach (var unit in group.Units.Where(a => group.Reinforcements.Contains(a.ActorID) &&
				StealthTankSquadPolicy.CanAdvanceReinforcement(IsActiveSpecialist(a),
					group.RetreatDestinations.ContainsKey(a.ActorID))).OrderBy(a => a.ActorID).ToArray())
			{
				var nearCore = StealthTankSquadPolicy.IsSameOrAdjacentStrategicCell(
					unit.Location, coreLocation, StrategicCellSize);
				var nearMission = missionLocation != null &&
					StealthTankSquadPolicy.IsSameOrAdjacentStrategicCell(
						unit.Location, missionLocation.Value, StrategicCellSize);
				if (nearCore || nearMission)
				{
					group.Reinforcements.Remove(unit.ActorID);
					if (IsEnemyTarget(group.Target))
					{
						bot.QueueOrder(new Order("Attack", unit, Target.FromActor(group.Target), false));
						scanQueuedOrders++;
					}

					if (Info.DebugLogging)
						Log.Write("debug", "AI stealth reinforcement {0} [{1}:{2}] tick={3}: unit={4} joined=True near-core={5} near-destination={6} strategic-size={7} core-mission-preserved=True target={8} stop=false cancel=false idle-gap=false.",
							Info.SquadLabel, player.PlayerName, group.Index, world.WorldTick,
							unit.ActorID, nearCore, nearMission, StrategicCellSize,
							group.Target == null ? "none" : group.Target.Info.Name + "#" + group.Target.ActorID);
					continue;
				}

				var destinationAnchor = missionLocation ?? coreLocation;
				var route = FindReinforcementRoute(unit, destinationAnchor, threats);
				if (route == null || route.Count == 0)
				{
					// Air's safe hold is an order to the unit's current location. It cancels no
					// core activity and naturally retries at the next strategic scan.
					bot.QueueOrder(new Order("Move", unit, Target.FromCell(world, unit.Location), false));
					scanQueuedOrders++;
					if (Info.DebugLogging)
						Log.Write("debug", "AI stealth reinforcement {0} [{1}:{2}] tick={3}: unit={4} staged=True routed=False safe-hold=True retry={5} core-mission-preserved=True target={6} unsafe-direct=false core-stop=false.",
							Info.SquadLabel, player.PlayerName, group.Index, world.WorldTick,
							unit.ActorID, Info.ScanInterval,
							group.Target == null ? "none" : group.Target.Info.Name + "#" + group.Target.ActorID);
					continue;
				}

				var queued = false;
				for (var i = Math.Min(Info.HazardRouteWaypointSpacing, route.Count - 1);
					i < route.Count; i += Info.HazardRouteWaypointSpacing)
				{
					bot.QueueOrder(new Order("Move", unit, Target.FromCell(world, route[i]), queued));
					queued = true;
					scanQueuedOrders++;
				}

				if (route.Count > 1 && (route.Count - 1) % Info.HazardRouteWaypointSpacing != 0)
				{
					bot.QueueOrder(new Order("Move", unit,
						Target.FromCell(world, route[route.Count - 1]), queued));
					scanQueuedOrders++;
				}

				if (Info.DebugLogging)
					Log.Write("debug", "AI stealth reinforcement {0} [{1}:{2}] tick={3}: unit={4} staged=True routed=True waypoints={5} destination={6} core-cell={7} mission-cell={8} core-mission-preserved=True core-stop=false.",
						Info.SquadLabel, player.PlayerName, group.Index, world.WorldTick,
						unit.ActorID, route.Count, route[route.Count - 1],
						StealthTankSquadPolicy.StrategicCell(coreLocation, StrategicCellSize),
						missionLocation == null ? "none" :
							StealthTankSquadPolicy.StrategicCell(missionLocation.Value, StrategicCellSize).ToString());
			}
		}

		List<CPos> FindReinforcementRoute(Actor unit, CPos anchor, List<Threat> threats)
		{
			var mobile = unit.TraitOrDefault<Mobile>();
			if (mobile == null)
				return null;

			var map = GetInfluenceMap(threats);
			return world.Map.FindTilesInAnnulus(anchor, 0, StrategicCellSize * 2)
				.Where(c => StealthTankSquadPolicy.IsSameOrAdjacentStrategicCell(
					c, anchor, StrategicCellSize) && mobile.CanEnterCell(c) &&
					!IsResourceHazard(c) && !IsInfluencedCell(map, c) &&
					(domainIndex == null || domainIndex.IsPassable(unit.Location, c, mobile.Locomotor)))
				.OrderBy(c => (c - unit.Location).LengthSquared).ThenBy(c => c.Y).ThenBy(c => c.X)
				.Take(16)
				.Select(c => FindCoarseSafeRoute(unit.Location, c, map))
				.FirstOrDefault(route => route != null && route.Count > 0);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			var data = new List<MiniYamlNode>
			{
				new MiniYamlNode("ReservedSpecialists",
					FieldSaver.FormatValue(reserved.OrderBy(id => id).ToArray()))
			};
			data.Add(StealthTankSquadPolicy.SaveRetreatState(groups
				.Where(group => group.RetreatDestinations.Count > 0)
				.Select(group => new StealthTankRetreatSaveGroup
				{
					GroupIndex = group.Index,
					TargetId = group.RetreatTarget?.ActorID ?? 0,
					Destinations = group.RetreatDestinations.OrderBy(pair => pair.Key).ToArray()
				})));
			data.Add(StealthTankSquadPolicy.SaveReinforcementState(groups
				.Where(group => group.Reinforcements.Count > 0)
				.Select(group => new StealthTankReinforcementSaveGroup
				{
					GroupIndex = group.Index,
					Members = group.Reinforcements.OrderBy(id => id).ToArray()
				})));
			return data;
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var reservedNode = data.FirstOrDefault(n => n.Key == "ReservedSpecialists");
			if (reservedNode == null)
				return;

			reserved.Clear();
			reserved.UnionWith(FieldLoader.GetValue<uint[]>(reservedNode.Key, reservedNode.Value.Value));
			foreach (var group in groups)
			{
				group.RetreatTarget = null;
				group.RetreatDestinations.Clear();
			}

			var retreatNode = data.FirstOrDefault(n => n.Key == "StealthTankRetreatState");
			pendingRetreatRestore = StealthTankSquadPolicy.TryLoadRetreatState(retreatNode, out var restored) ?
				restored : Array.Empty<StealthTankRetreatSaveGroup>();
			var reinforcementNode = data.FirstOrDefault(n => n.Key == "StealthTankReinforcementState");
			pendingReinforcementRestore = StealthTankSquadPolicy.TryLoadReinforcementState(
				reinforcementNode, out var restoredReinforcements) ? restoredReinforcements :
				Array.Empty<StealthTankReinforcementSaveGroup>();
			scanTicks = 1;
		}

		void ApplyPendingReinforcementRestore()
		{
			if (pendingReinforcementRestore == null)
				return;

			var restored = 0;
			var dropped = 0;
			foreach (var saved in pendingReinforcementRestore)
			{
				if (saved.GroupIndex < 0 || saved.GroupIndex >= groups.Length)
				{
					dropped += saved.Members.Length;
					continue;
				}

				var group = groups[saved.GroupIndex];
				foreach (var actorId in saved.Members)
					if (group.Units.Any(a => a.ActorID == actorId) && reserved.Contains(actorId))
					{
						group.Reinforcements.Add(actorId);
						restored++;
					}
					else
						dropped++;
			}

			if (Info.DebugLogging)
				Log.Write("debug", "AI stealth reinforcement restore {0} [{1}] tick={2}: version={3} restored={4} dropped={5} staged={6}.",
					Info.SquadLabel, player.PlayerName, world.WorldTick,
					StealthTankSquadPolicy.ReinforcementSaveVersion, restored, dropped,
					string.Join(",", groups.SelectMany(g => g.Reinforcements.OrderBy(id => id)
						.Select(id => g.Index + ":" + id))));
			pendingReinforcementRestore = null;
		}

		void ApplyPendingRetreatRestore()
		{
			if (pendingRetreatRestore == null)
				return;

			var restoredGroups = 0;
			var restoredMembers = 0;
			var droppedMembers = 0;
			var fallbackMembers = 0;
			var fallbackGeometry = new List<string>();
			foreach (var saved in pendingRetreatRestore)
			{
				if (saved.GroupIndex < 0 || saved.GroupIndex >= groups.Length)
				{
					droppedMembers += saved.Destinations.Length;
					continue;
				}

				var group = groups[saved.GroupIndex];
				group.RetreatDestinations.Clear();
				var target = world.GetActorById(saved.TargetId);
				group.RetreatTarget = IsEnemyTarget(target) ? target : null;
				foreach (var savedDestination in saved.Destinations)
				{
					var unit = world.GetActorById(savedDestination.Key);
					if (!IsEligible(unit) || !reserved.Contains(savedDestination.Key) ||
						!group.Units.Contains(unit))
					{
						droppedMembers++;
						continue;
					}

					if (StealthTankSquadPolicy.IsSameStrategicCell(unit.Location,
						savedDestination.Value, StrategicCellSize))
						continue;

					var savedDestinationValid = ValidateRestoredRetreatDestination(unit,
						savedDestination.Value, group.RetreatTarget);
					var destination = savedDestinationValid ? savedDestination.Value :
						group.RetreatTarget == null ? (CPos?)null :
						FindStrategicRetreatDestination(unit, group.RetreatTarget.Location);
					if (destination == null)
					{
						droppedMembers++;
						continue;
					}

					group.RetreatDestinations.Add(unit.ActorID, destination.Value);
					if (!savedDestinationValid)
					{
						bot.QueueOrder(new Order("Move", unit,
							Target.FromCell(world, destination.Value), false));
						var from = StealthTankSquadPolicy.StrategicCell(unit.Location, StrategicCellSize);
						var to = StealthTankSquadPolicy.StrategicCell(destination.Value, StrategicCellSize);
						var targetCell = StealthTankSquadPolicy.StrategicCell(
							group.RetreatTarget.Location, StrategicCellSize);
						var delta = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
						var away = StealthTankSquadPolicy.IsRetreatDestinationAwayFromTarget(
							unit.Location, destination.Value, group.RetreatTarget.Location,
							StrategicCellSize, world.Map.MapSize.X, world.Map.MapSize.Y);
						fallbackGeometry.Add(unit.ActorID + ":" + from + ">" + to +
							":target=" + targetCell + ":delta=" + delta + ":away=" + away);
						fallbackMembers++;
					}

					restoredMembers++;
				}

				if (StealthTankSquadPolicy.ShouldBlockReassessment(group.RetreatDestinations.Count))
				{
					group.Target = null;
					group.SuspendedEngagementTarget = null;
					ClearRetainedPlan(group);
					restoredGroups++;
				}
				else
					group.RetreatTarget = null;
			}

			if (Info.DebugLogging)
				Log.Write("debug", "AI stealth retreat restore {0} [{1}] tick={2}: version={3} groups={4} members={5} dropped={6} fallback={7} barrier={8} targets={9} destinations={10} fallback-geometry={11}.",
					Info.SquadLabel, player.PlayerName, world.WorldTick,
					StealthTankSquadPolicy.RetreatSaveVersion, restoredGroups, restoredMembers,
					droppedMembers, fallbackMembers,
					groups.Any(g => StealthTankSquadPolicy.ShouldBlockReassessment(g.RetreatDestinations.Count)),
					string.Join(",", groups.Where(g => g.RetreatDestinations.Count > 0)
						.Select(g => g.Index + ":" + (g.RetreatTarget?.ActorID.ToString() ?? "none"))),
					string.Join(",", groups.SelectMany(g => g.RetreatDestinations.OrderBy(pair => pair.Key)
						.Select(pair => g.Index + ":" + pair.Key + ":" + pair.Value))),
					string.Join(",", fallbackGeometry));
			pendingRetreatRestore = null;
		}

		bool ValidateRestoredRetreatDestination(Actor unit, CPos destination, Actor target)
		{
			var mobile = unit.TraitOrDefault<Mobile>();
			if (mobile == null || !world.Map.Contains(destination) || !mobile.CanEnterCell(destination) ||
				(domainIndex != null && !domainIndex.IsPassable(unit.Location, destination, mobile.Locomotor)))
				return false;

			var from = StealthTankSquadPolicy.StrategicCell(unit.Location, StrategicCellSize);
			var to = StealthTankSquadPolicy.StrategicCell(destination, StrategicCellSize);
			return Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y)) == 1 &&
				(target == null || StealthTankSquadPolicy.IsRetreatDestinationAwayFromTarget(
					unit.Location, destination, target.Location, StrategicCellSize,
					world.Map.MapSize.X, world.Map.MapSize.Y));
		}

		bool IsEnemyTarget(Actor actor)
		{
			return actor != null && actor.IsInWorld && !actor.IsDead &&
				player.RelationshipWith(actor.Owner) == PlayerRelationship.Enemy &&
				!actor.Info.HasTraitInfo<HuskInfo>() && !actor.GetEnabledTargetTypes().IsEmpty;
		}

		static bool CanAttackTarget(Actor unit, Actor target)
		{
			var targetTypes = target.GetEnabledTargetTypes();
			return unit.TraitsImplementing<Armament>().Any(a =>
				!a.IsTraitDisabled && a.Weapon.IsValidTarget(targetTypes));
		}

		Threat CreateThreat(Actor actor)
		{
			var weaponRange = 0;
			foreach (var armament in actor.TraitsImplementing<Armament>())
				if (!armament.IsTraitDisabled && armament.Weapon.IsValidTarget(GroundTargetTypes))
					weaponRange = Math.Max(weaponRange, (int)Math.Ceiling(armament.MaxRange().Length / 1024f));

			var detectorRange = actor.TraitsImplementing<DetectCloaked>()
				.Where(d => !d.IsTraitDisabled).Select(d => (int)Math.Ceiling(d.Range.Length / 1024f)).DefaultIfEmpty().Max();
			if (weaponRange <= 0 && detectorRange <= 0)
				return null;

			return new Threat
			{
				Actor = actor,
				WeaponRangeCells = weaponRange,
				DetectorRangeCells = detectorRange,
				Value = Math.Max(1, actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1),
				WeaponIsEngaged = actor.CurrentActivity != null &&
					actor.CurrentActivity.ActivitiesImplementing<IActivityNotifyStanceChanged>().Any()
			};
		}

		void UpdateGroup(SpecialistGroup group, List<Actor> enemies, List<Threat> threats)
		{
			group.Units.RemoveAll(a => !IsEligible(a) || !reserved.Contains(a.ActorID));
			group.Reinforcements.RemoveWhere(id => !group.Units.Any(a => a.ActorID == id));
			UpdateReinforcements(group, threats);
			var activeUnits = group.Units.Where(a => IsActiveCoreSpecialist(group, a)).ToArray();
			if (group.Units.Count == 0)
			{
				group.Target = null;
				group.RetreatTarget = null;
				group.RetreatDestinations.Clear();
				ClearRetainedPlan(group);
				return;
			}

			if (activeUnits.Length == 0)
				return;
			if (StealthTankSquadPolicy.ShouldBlockReassessment(group.RetreatDestinations.Count))
				return;

			// Engagement-local safety owns this short hold and checks it every 25 ticks.
			// Do not let the slower strategic approach scan replace the suspended target.
			if (group.SuspendedEngagementTarget != null)
				return;

			var activeEngagement = group.Target != null && IsEnemyTarget(group.Target) &&
				activeUnits.Any(a => a.CurrentActivity != null &&
					a.CurrentActivity.ActivitiesImplementing<IActivityNotifyStanceChanged>().Any());
			var activeLocalThreat = false;
			var activeResourceHazard = false;
			if (activeEngagement)
				foreach (var unit in activeUnits)
				{
					activeLocalThreat |= HasLocalThreatExposure(unit, out _, out _, out _, out _, out _);
					activeResourceHazard |= Info.AvoidResourceTypes.Count > 0 && resourceLayer != null &&
						world.Map.FindTilesInAnnulus(unit.Location, 0, 1).Any(c =>
						{
							var type = resourceLayer.GetResource(c).Type;
							return type != null && Info.AvoidResourceTypes.Contains(type);
						});
				}

			// Once firing has begun, the 25-tick engagement check owns local detector/support
			// safety. Do not let the slower concealed-approach map turn a lone detector into
			// an engagement veto; armed overlap and hazards still invalidate immediately.
			if (StealthTankSquadPolicy.ShouldRetainActiveEngagement(activeEngagement,
				activeEngagement, activeLocalThreat, activeResourceHazard))
			{
				scanPlanRetentions++;
				return;
			}

			var role = StealthTankSquadPolicy.RoleForGroup(group.Index,
				Info.MaximumHarassmentGroups, Info.IncludeAttackGroup);
			var center = activeUnits.Select(a => a.CenterPosition).Average();
			var ownRange = activeUnits.SelectMany(a => a.TraitsImplementing<Armament>())
				.Where(a => !a.IsTraitDisabled && a.Weapon.IsValidTarget(GroundTargetTypes))
				.Select(a => (int)Math.Ceiling(a.MaxRange().Length / 1024f)).DefaultIfEmpty(0).Max();
			var squadValue = activeUnits.Sum(a => Math.Max(1, a.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1));
			var representative = activeUnits.OrderBy(a =>
				(a.CenterPosition - center).HorizontalLengthSquared).ThenBy(a => a.ActorID).First();
			var incumbent = group.Target;
			var targetCrossedStrategicCell = group.HasPlan && incumbent != null &&
				!StealthTankSquadPolicy.IsSameStrategicCell(
					incumbent.Location, group.PlannedTargetLocation, StrategicCellSize);

			var phaseStarted = Stopwatch.GetTimestamp();
			var rankedCandidates = enemies.Select(a => new
			{
				Actor = a,
				Priority = Priority(role, a, activeUnits.Length),
				Attackable = activeUnits.Any(u => CanAttackTarget(u, a)),
				Distance = (a.CenterPosition - center).Length / 1024,
				ClusterMultiplier = InfantryClusterMultiplier(role, a)
			}).Where(c => c.Priority > 0 && c.Attackable)
				.OrderByDescending(c => StealthTankSquadPolicy.TargetScore(c.Priority,
					c.Actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1, c.Distance,
					c.Actor == group.Target ? 100 + Info.TargetSwitchImprovementPercent : 100,
					c.ClusterMultiplier, role == StealthTankSquadRole.Harass ? Info.HarassmentDistancePenalty : 1))
				.ThenBy(c => c.Actor.ActorID).ToList();
			var candidateCells = rankedCandidates.GroupBy(c =>
				StealthTankSquadPolicy.StrategicCell(c.Actor.Location, StrategicCellSize))
				.Select(g => new
				{
					Cell = g.Key,
					Candidates = g.ToList(),
					Utility = g.Max(c => StealthTankSquadPolicy.TargetScore(c.Priority,
						c.Actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1, c.Distance,
						100, c.ClusterMultiplier,
						role == StealthTankSquadRole.Harass ? Info.HarassmentDistancePenalty : 1))
				}).OrderBy(c => c.Cell.Y).ThenBy(c => c.Cell.X).ToList();
			var requiredCellIndex = incumbent == null ? -1 : candidateCells.FindIndex(c =>
				c.Candidates.Any(candidate => candidate.Actor == incumbent));
			var cellDistances = candidateCells.Select(c =>
			{
				var cell = world.Map.Clamp(new CPos(c.Cell.X * StrategicCellSize + StrategicCellSize / 2,
					c.Cell.Y * StrategicCellSize + StrategicCellSize / 2));
				return (world.Map.CenterOfCell(cell) - center).LengthSquared;
			}).ToList();
			var cellUtilities = candidateCells.Select(c => (int)Math.Min(int.MaxValue, c.Utility)).ToList();
			var closestCellCount = Info.MaximumTargetCandidates / 2;
			var highestValueCellCount = Info.MaximumTargetCandidates - closestCellCount;
			var ordinaryCellIndices = AirThreatGeometry.SelectTargetCandidates(cellDistances, cellUtilities,
				closestCellCount, highestValueCellCount);
			var selectedCellIndices = AirThreatGeometry.SelectTargetCandidates(cellDistances, cellUtilities,
				closestCellCount, highestValueCellCount, requiredCellIndex);
			var candidates = selectedCellIndices.SelectMany(i =>
			{
				var cell = candidateCells[i];
				var bestChallenger = cell.Candidates.FirstOrDefault(candidate => candidate.Actor != incumbent);
				var cellIncumbent = cell.Candidates.FirstOrDefault(candidate => candidate.Actor == incumbent);
				return new[] { bestChallenger, cellIncumbent }.Where(candidate => candidate != null);
			}).Distinct().ToList();
			var incumbentOutsideCandidateCap = requiredCellIndex >= 0 &&
				!ordinaryCellIndices.Contains(requiredCellIndex);
			scanCandidateTicks += Stopwatch.GetTimestamp() - phaseStarted;

			Actor selected = null;
			long selectedScore = 0;
			var selectedDanger = 0;
			Actor freshIncumbent = null;
			long freshIncumbentScore = 0;
			var freshIncumbentDanger = 0;
			Actor freshChallenger = null;
			long freshChallengerScore = 0;
			var freshChallengerDanger = 0;
			Actor freshWall = null;
			long freshWallScore = 0;
			var dangerousCandidates = 0;
			Actor rejectedTarget = null;
			Actor rejectedBlocker = null;
			var selectedClearAction = SpecialistDefenderClearAction.None;
			var selectedMinimumAttackRange = 0;
			List<CPos> selectedRoute = null;
			var candidateRoutes = new Dictionary<Actor, List<CPos>>();
			var strategicRouteCache = new Dictionary<CPos, List<CPos>>();
			var unroutableStrategicCells = new HashSet<CPos>();
			var unroutableCandidates = 0;
			var defendedOpportunities = new List<DefendedOpportunity>();
			phaseStarted = Stopwatch.GetTimestamp();
			foreach (var candidate in candidates)
			{
				var upperScore = StealthTankSquadPolicy.TargetScore(candidate.Priority,
					candidate.Actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1,
					StealthTankSquadPolicy.OptimisticApproachDistance(candidate.Distance, ownRange),
					100, candidate.ClusterMultiplier,
					role == StealthTankSquadRole.Harass ? Info.HarassmentDistancePenalty : 1);
				if (candidate.Actor != incumbent && freshChallenger != null && freshChallengerDanger == 0 &&
					(upperScore < freshChallengerScore ||
						(upperScore == freshChallengerScore && candidate.Actor.ActorID >= freshChallenger.ActorID)))
					continue;

				var candidateCrush = Info.CrushInfantryTargets && role == StealthTankSquadRole.Harass &&
					candidate.Actor.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes);
				var defendingValue = 0;
				Actor strongestDefender = null;
				var defenders = new List<Threat>();
				var routeStarted = Stopwatch.GetTimestamp();
				var route = HazardAwarePath(representative, candidate.Actor, threats, ownRange, candidateCrush,
					false, 0, false,
					strategicRouteCache, unroutableStrategicCells);
				scanPathTicks += Stopwatch.GetTimestamp() - routeStarted;
				if (route == null || route.Count == 0)
				{
					var danger = DangerAlongRun(center, candidate.Actor, threats, ownRange,
						role == StealthTankSquadRole.Harass, out defendingValue, out strongestDefender,
						out defenders);
					if (danger && (role == StealthTankSquadRole.Harass ||
						!StealthTankSquadPolicy.CanCarefullyClear(squadValue, defendingValue, Info.CarefulClearValueRatio)))
					{
						dangerousCandidates++;
						if (role == StealthTankSquadRole.Harass)
						{
							var clear = defenders.Select(d => (Threat: d, Action: DefenderClearAction(d,
								defenders.Count, ownRange))).Where(c => c.Action != SpecialistDefenderClearAction.None)
								.OrderBy(c => c.Threat.Value).ThenBy(c => c.Threat.Actor.ActorID).FirstOrDefault();
							if (clear.Threat != null)
								defendedOpportunities.Add(new DefendedOpportunity
								{
									ProtectedTarget = candidate.Actor,
									ClearTarget = clear.Threat.Actor,
									ClearAction = clear.Action,
									MinimumAttackRangeCells = clear.Action == SpecialistDefenderClearAction.SnipeTank ?
										StealthTankSquadPolicy.BufferedRange(clear.Threat.WeaponRangeCells,
											Info.ThreatRangeBufferCells) + Info.KiteRangeMarginCells : 0,
									DefendingValue = defendingValue,
									UnlockedScore = StealthTankSquadPolicy.TargetScore(candidate.Priority,
										candidate.Actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1,
										candidate.Distance, 100, candidate.ClusterMultiplier,
										Info.HarassmentDistancePenalty)
								});
						}

						if (rejectedTarget == null)
						{
							rejectedTarget = candidate.Actor;
							rejectedBlocker = strongestDefender;
						}

						continue;
					}

					if (role == StealthTankSquadRole.Attack && danger)
					{
						routeStarted = Stopwatch.GetTimestamp();
						route = HazardAwarePath(representative, candidate.Actor, threats, ownRange,
							candidateCrush, true, 0);
						scanPathTicks += Stopwatch.GetTimestamp() - routeStarted;
					}

					if (route == null || route.Count == 0)
					{
						unroutableCandidates++;
						continue;
					}
				}

				candidateRoutes[candidate.Actor] = route;
				var routeDistance = StealthTankSquadPolicy.RouteDistanceCells(representative.Location, route);
				var freshScore = StealthTankSquadPolicy.TargetScore(candidate.Priority,
					candidate.Actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1, routeDistance,
					100, candidate.ClusterMultiplier,
					role == StealthTankSquadRole.Harass ? Info.HarassmentDistancePenalty : 1);
				if (candidate.Actor == incumbent)
				{
					freshIncumbent = candidate.Actor;
					freshIncumbentScore = freshScore;
					freshIncumbentDanger = defendingValue;
				}

				if (candidate.Actor != incumbent && (freshChallenger == null ||
					(defendingValue == 0 && freshChallengerDanger > 0) ||
					((defendingValue == 0) == (freshChallengerDanger == 0) && (freshScore > freshChallengerScore ||
						(freshScore == freshChallengerScore && candidate.Actor.ActorID < freshChallenger.ActorID)))))
				{
					freshChallenger = candidate.Actor;
					freshChallengerScore = freshScore;
					freshChallengerDanger = defendingValue;
				}

				if (candidate.Actor.Info.HasTraitInfo<LineBuildNodeInfo>() &&
					(freshWall == null || freshScore > freshWallScore ||
						(freshScore == freshWallScore && candidate.Actor.ActorID < freshWall.ActorID)))
				{
					freshWall = candidate.Actor;
					freshWallScore = freshScore;
				}
			}

			scanCandidateThreatTicks += Stopwatch.GetTimestamp() - phaseStarted;
			if (incumbent == null && freshChallenger != null)
			{
				selected = freshChallenger;
				selectedScore = freshChallengerScore;
				selectedDanger = freshChallengerDanger;
				selectedRoute = candidateRoutes[freshChallenger];
			}

			phaseStarted = Stopwatch.GetTimestamp();
			if (selected == null)
			{
				var allUsefulTargetsUnavailable = StealthTankSquadPolicy.AreAllCandidatesUnavailable(
					candidates.Count, dangerousCandidates, unroutableCandidates);
				if (allUsefulTargetsUnavailable && group.ConsecutiveNoSafeTargetScans < int.MaxValue)
					group.ConsecutiveNoSafeTargetScans++;
				else if (!allUsefulTargetsUnavailable)
					group.ConsecutiveNoSafeTargetScans = 0;

				var eligibleClears = defendedOpportunities.Where(o =>
					StealthTankSquadPolicy.CanAttemptDefenderClear(group.ConsecutiveNoSafeTargetScans,
						Info.DefenderClearFallbackScans, squadValue, o.DefendingValue,
						Info.DefenderClearValueRatio))
					.GroupBy(o => o.ClearTarget.ActorID)
					.Select(g => g.OrderBy(o => o.DefendingValue).ThenByDescending(o => o.UnlockedScore).First())
					.ToList();
				while (eligibleClears.Count > 0)
				{
					var clearIndex = StealthTankSquadPolicy.SelectDefenderClearOpportunity(
						eligibleClears.Select(o => o.DefendingValue).ToArray(),
						eligibleClears.Select(o => o.UnlockedScore).ToArray(), Info.DefenderClearWeakestCandidates);
					if (clearIndex < 0)
						break;

					var clear = eligibleClears[clearIndex];
					var clearByCrushing = clear.ClearAction == SpecialistDefenderClearAction.CrushInfantry;
					var routeStarted = Stopwatch.GetTimestamp();
					var clearRoute = HazardAwarePath(representative, clear.ClearTarget, threats, ownRange,
						clearByCrushing, false, clear.MinimumAttackRangeCells,
						StealthTankSquadPolicy.ShouldIgnoreSelectedDefenderInfluence(clear.ClearAction));
					scanPathTicks += Stopwatch.GetTimestamp() - routeStarted;
					if (clearRoute == null || clearRoute.Count == 0)
					{
						eligibleClears.RemoveAt(clearIndex);
						continue;
					}

					selected = clear.ClearTarget;
					selectedClearAction = clear.ClearAction;
					selectedMinimumAttackRange = clear.MinimumAttackRangeCells;
					selectedScore = clear.UnlockedScore;
					selectedDanger = clear.DefendingValue;
					selectedRoute = clearRoute;
					if (Info.DebugLogging)
						Log.Write("debug", "AI stealth squad {0} [{1}:{2}] selected reachable weakest defender {3}#{4} by {5} after {6} all-defended scans; protected={7}#{8} package-value={9} unlocked-score={10}.",
							Info.SquadLabel, player.PlayerName, group.Index, selected.Info.Name, selected.ActorID,
							clear.ClearAction, group.ConsecutiveNoSafeTargetScans, clear.ProtectedTarget.Info.Name,
							clear.ProtectedTarget.ActorID, clear.DefendingValue, clear.UnlockedScore);
					break;
				}
			}

			if (incumbent != null)
			{
				var challenger = freshChallenger;
				var challengerScore = freshChallengerScore;
				var challengerDanger = freshChallengerDanger;
				if (freshIncumbent == null && challenger == null && selected != incumbent)
				{
					challenger = selected;
					challengerScore = selectedScore;
					challengerDanger = selectedDanger;
				}

				var reassessment = StealthTankSquadPolicy.ReassessTarget(
					freshIncumbent != null, freshIncumbentDanger == 0, freshIncumbentScore,
					challenger != null, challengerDanger == 0, challengerScore,
					Info.TargetSwitchImprovementPercent);
				if (reassessment == StealthTankTargetReassessment.RetainIncumbent)
				{
					selected = freshIncumbent;
					selectedScore = freshIncumbentScore;
					selectedDanger = freshIncumbentDanger;
					selectedClearAction = SpecialistDefenderClearAction.None;
					selectedMinimumAttackRange = 0;
				}
				else if (reassessment == StealthTankTargetReassessment.SwitchToChallenger)
				{
					var retainedClearAction = challenger == selected ? selectedClearAction : SpecialistDefenderClearAction.None;
					var retainedMinimumAttackRange = challenger == selected ? selectedMinimumAttackRange : 0;
					selected = challenger;
					selectedScore = challengerScore;
					selectedDanger = challengerDanger;
					selectedClearAction = retainedClearAction;
					selectedMinimumAttackRange = retainedMinimumAttackRange;
				}
				else
					selected = null;

				if (selected != null && selectedClearAction == SpecialistDefenderClearAction.None)
					candidateRoutes.TryGetValue(selected, out selectedRoute);

				if (Info.DebugLogging && targetCrossedStrategicCell)
				{
					var previousCell = StealthTankSquadPolicy.StrategicCell(
						group.PlannedTargetLocation, StrategicCellSize);
					var currentCell = StealthTankSquadPolicy.StrategicCell(
						incumbent.Location, StrategicCellSize);
					Log.Write("debug", "AI stealth target {0} [{1}:{2}] {3}#{4} moved strategic cell {5}->{6}; fresh reassessment={7} incumbent-valid={8} incumbent-score={9} challenger={10} challenger-score={11} best-wall={12} wall-score={13} wall-priority={14} candidate-cap={15} candidate-count={16} incumbent-outside-cap={17} threshold={18}% refresh-order={19} incumbent-undefended={20} challenger-undefended={21} target-loss=false stop=false cancel=false idle-gap=false.",
						Info.SquadLabel, player.PlayerName, group.Index, incumbent.Info.Name, incumbent.ActorID,
						previousCell, currentCell, reassessment, freshIncumbent != null, freshIncumbentScore,
						challenger == null ? "none" : challenger.Info.Name + "#" + challenger.ActorID,
						challengerScore, freshWall == null ? "none" : freshWall.Info.Name + "#" + freshWall.ActorID,
						freshWallScore, Info.WallTargetPriority, Info.MaximumTargetCandidates,
						candidates.Count, incumbentOutsideCandidateCap, Info.TargetSwitchImprovementPercent,
						reassessment == StealthTankTargetReassessment.RetainIncumbent,
						freshIncumbentDanger == 0, challengerDanger == 0);
				}
				else if (Info.DebugLogging && incumbentOutsideCandidateCap)
					Log.Write("debug", "AI stealth target {0} [{1}:{2}] ordinary reassessment evaluated rank-over-cap incumbent {3}#{4}: reassessment={5} candidate-cap={6} candidate-count={7} threshold={8}%.",
						Info.SquadLabel, player.PlayerName, group.Index, incumbent.Info.Name, incumbent.ActorID,
						reassessment, Info.MaximumTargetCandidates, candidates.Count,
						Info.TargetSwitchImprovementPercent);
			}

			if (selected != null && selectedRoute == null)
			{
				unroutableCandidates++;
				selected = null;
			}

			if (selected == null)
			{
				if (Info.DebugLogging && world.WorldTick >= group.LastNoTargetLogTick + Info.ScanInterval * 10)
				{
					group.LastNoTargetLogTick = world.WorldTick;
					Log.Write("debug", "AI stealth squad {0} [{1}:{2}] {3} waiting: units={4} candidates={5} dangerous={6} unroutable={7} rejected={8} blocker={9}.",
						Info.SquadLabel, player.PlayerName, group.Index, role, activeUnits.Length, candidates.Count, dangerousCandidates,
						unroutableCandidates,
						rejectedTarget == null ? "none" : rejectedTarget.Info.Name + "#" + rejectedTarget.ActorID,
						rejectedBlocker == null ? "none" : rejectedBlocker.Info.Name + "#" + rejectedBlocker.ActorID);
				}

				var abandonedTarget = group.Target != null;
				group.Target = null;
				group.TargetScore = 0;
				ClearRetainedPlan(group);
				if (abandonedTarget)
				{
					bot.QueueOrder(new Order("Stop", null, false, groupedActors: activeUnits));
					scanQueuedOrders++;
					group.LastOrderTick = world.WorldTick;
				}

				if (role == StealthTankSquadRole.Harass)
					WaitNearHarvesterField(group, enemies, threats, ownRange, abandonedTarget);
				return;
			}
			else if (dangerousCandidates < candidates.Count)
				group.ConsecutiveNoSafeTargetScans = 0;
			scanDefendedFallbackTicks += Stopwatch.GetTimestamp() - phaseStarted;

			var changed = selected != group.Target;
			if (!changed && group.HasPlan)
				ObserveProgress(group, selected, center);

			phaseStarted = Stopwatch.GetTimestamp();
			var routeUnsafe = group.HasPlan && group.RetainedRoutes.Count > 0 &&
				HasUnsafeRetainedRoute(group, selected, threats, ownRange);
			scanRetainedSafetyTicks += Stopwatch.GetTimestamp() - phaseStarted;

			// Like Air, retaining a live moving actor retains its one-shot mission orders.
			// Attack follows the actor after the precomputed approach; a route is only
			// refreshed for a real switch, membership/safety change, or bounded stall.
			var invalidation = StealthTankSquadPolicy.ClassifyPlanInvalidation(group.HasPlan,
				changed, group.MembershipChanged,
				false,
				routeUnsafe, world.WorldTick, group.LastProgressTick,
				Info.MissionRetryInterval > 0 ? Info.MissionRetryInterval : Info.OrderInterval);
			group.Target = selected;
			group.TargetScore = selectedScore;
			if (invalidation == StealthTankPlanInvalidation.None)
			{
				scanPlanRetentions++;
				return;
			}

			scanPlanInvalidations++;

			group.LastOrderTick = world.WorldTick;
			ClearRetainedPlan(group);
			var crush = selectedClearAction == SpecialistDefenderClearAction.CrushInfantry ||
				(selectedClearAction == SpecialistDefenderClearAction.None && Info.CrushInfantryTargets &&
					role == StealthTankSquadRole.Harass &&
					selected.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes));
			ApplyHazardAwarePlan(group, selected, selectedRoute, crush);

			BeginRetainedPlan(group, selected, center);

			if (Info.DebugLogging)
				Log.Write("debug", "AI stealth squad {0} [{1}:{2}] {3} target {4}#{5}: units={6} score={7} defended-value={8} order={9}.",
					Info.SquadLabel, player.PlayerName, group.Index, role, selected.Info.Name, selected.ActorID, activeUnits.Length,
					selectedScore, selectedDanger, crush ? "crush" : "hazard-routed attack");
		}

		SpecialistDefenderClearAction DefenderClearAction(Threat defender, int packageDefenderCount,
			int ownRange)
		{
			var types = defender.Actor.GetEnabledTargetTypes();
			return StealthTankSquadPolicy.DefenderClearAction(
				types.Overlaps(InfantryTargetTypes), types.Overlaps(TankTargetTypes), Info.CrushInfantryTargets,
				packageDefenderCount, ownRange, defender.WeaponRangeCells, defender.DetectorRangeCells,
				Info.ThreatRangeBufferCells + Info.KiteRangeMarginCells);
		}

		void ClearRetainedPlan(SpecialistGroup group)
		{
			group.RetainedRoutes.Clear();
			group.RetainedRouteIndices.Clear();
			group.HasPlan = false;
			group.LastTargetDistanceSquared = long.MaxValue;
			group.LastTargetHp = int.MaxValue;
		}

		void BeginRetainedPlan(SpecialistGroup group, Actor target, WPos center)
		{
			group.HasPlan = true;
			group.PlannedTargetLocation = target.Location;
			group.LastProgressTick = world.WorldTick;
			group.LastTargetDistanceSquared = (target.CenterPosition - center).HorizontalLengthSquared;
			group.LastTargetHp = target.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue;
			group.MembershipChanged = false;
		}

		void ObserveProgress(SpecialistGroup group, Actor target, WPos center)
		{
			var distance = (target.CenterPosition - center).HorizontalLengthSquared;
			var hp = target.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue;
			if (group.LastTargetHp != int.MaxValue && hp < group.LastTargetHp)
				scanObservedTargetDamage += group.LastTargetHp - hp;

			if (distance < group.LastTargetDistanceSquared || hp < group.LastTargetHp)
				group.LastProgressTick = world.WorldTick;

			group.LastTargetDistanceSquared = distance;
			group.LastTargetHp = hp;
		}

		bool HasUnsafeRetainedRoute(SpecialistGroup group, Actor target, List<Threat> threats, int ownRange)
		{
			var lookahead = Math.Max(1, Info.HazardRouteWaypointSpacing * 3);
			foreach (var unit in group.Units.Where(a => IsActiveCoreSpecialist(group, a)))
			{
				if (!group.RetainedRoutes.TryGetValue(unit.ActorID, out var route) || route.Count == 0)
					continue;

				var start = group.RetainedRouteIndices.TryGetValue(unit.ActorID, out var retainedIndex) ?
					Math.Min(retainedIndex, route.Count - 1) : 0;
				var closest = start;
				var closestDistance = (route[start] - unit.Location).LengthSquared;
				for (var i = start + 1; i < route.Count; i++)
				{
					var distance = (route[i] - unit.Location).LengthSquared;
					if (distance >= closestDistance)
						break;

					closest = i;
					closestDistance = distance;
				}

				group.RetainedRouteIndices[unit.ActorID] = closest;
				for (var i = closest; i < Math.Min(route.Count, closest + lookahead); i++)
					if (IsTransitThreatenedCell(route[i], target, threats, ownRange))
						return true;
			}

			return false;
		}

		void WaitNearHarvesterField(SpecialistGroup group, IEnumerable<Actor> enemies,
			List<Threat> threats, int ownRange, bool force)
		{
			if (resourceLayer == null || domainIndex == null || Info.ResourceWaitingSearchRadius <= 0 ||
				(!force && world.WorldTick < group.LastOrderTick + Info.ResourceWaitingOrderInterval))
				return;

			var first = group.Units.FirstOrDefault(a => IsActiveCoreSpecialist(group, a));
			var mobile = first?.TraitOrDefault<Mobile>();
			if (first == null || mobile == null)
				return;

			var anchors = enemies.Where(a => Info.HarvesterTypes.Contains(a.Info.Name) ||
				Info.HarvesterWaitingAnchorTypes.Contains(a.Info.Name))
				.OrderBy(a => (a.CenterPosition - first.CenterPosition).HorizontalLengthSquared)
				.ThenBy(a => a.ActorID).Take(8);
			foreach (var anchor in anchors)
			{
				var cells = world.Map.FindTilesInAnnulus(anchor.Location, 0, Info.ResourceWaitingSearchRadius)
					.Where(c => resourceLayer.GetResource(c).Type != null && !IsResourceHazard(c) &&
						!IsTransitThreatenedCell(c, null, threats, ownRange) &&
						mobile.CanEnterCell(c) && domainIndex.IsPassable(first.Location, c, mobile.Locomotor))
					.OrderBy(c => (c - first.Location).LengthSquared)
					.ThenBy(c => c.Y).ThenBy(c => c.X).Take(1).ToArray();
				if (cells.Length == 0)
					continue;
				var cell = cells[0];

				group.LastOrderTick = world.WorldTick;
				bot.QueueOrder(new Order("Move", null, Target.FromCell(world, cell), false,
					groupedActors: group.Units.Where(a => IsActiveCoreSpecialist(group, a)).ToArray()));
				scanQueuedOrders++;
				if (Info.DebugLogging)
					Log.Write("debug", "AI stealth squad {0} [{1}:{2}] waiting near harvester field at {3} from anchor {4}#{5}.",
						Info.SquadLabel, player.PlayerName, group.Index, cell, anchor.Info.Name, anchor.ActorID);
				return;
			}
		}

		void ApplyHazardAwarePlan(SpecialistGroup group, Actor target, List<CPos> path, bool crush)
		{
			var units = group.Units.Where(a => IsActiveCoreSpecialist(group, a)).OrderBy(a => a.ActorID).ToArray();
			if (units.Length == 0)
				return;

			// Like AirSquad, specialists retain and submit one shared coarse threat route per formation.
			// DomainIndex rejects disconnected targets; the engine owns locomotor terrain/blocker
			// refinement while the specialist planner avoids broad engaged-threat corridors.
			var routedUnits = units.Length;
			var waypointCount = 0;
			var phaseStarted = Stopwatch.GetTimestamp();
			foreach (var unit in units)
			{
				group.RetainedRoutes[unit.ActorID] = path;
				group.RetainedRouteIndices[unit.ActorID] = 0;
			}

			var queued = false;
			for (var i = Math.Min(Info.HazardRouteWaypointSpacing, path.Count - 1);
				i < path.Count; i += Info.HazardRouteWaypointSpacing)
			{
				bot.QueueOrder(new Order("Move", null, Target.FromCell(world, path[i]), queued,
					groupedActors: units));
				scanQueuedOrders++;
				queued = true;
				waypointCount++;
			}

			if (path.Count > 1 && (path.Count - 1) % Info.HazardRouteWaypointSpacing != 0)
			{
				bot.QueueOrder(new Order("Move", null, Target.FromCell(world, path[path.Count - 1]), queued,
					groupedActors: units));
				scanQueuedOrders++;
				queued = true;
				waypointCount++;
			}

			if (crush)
			{
				if (!queued)
				{
					bot.QueueOrder(new Order("Move", null, Target.FromCell(world, target.Location), false,
						groupedActors: units));
					scanQueuedOrders++;
				}
			}
			else
			{
				bot.QueueOrder(new Order("Attack", null, Target.FromActor(target), queued,
					groupedActors: units));
				scanQueuedOrders++;
			}

			scanOrderTicks += Stopwatch.GetTimestamp() - phaseStarted;

			if (Info.DebugLogging)
				Log.Write("debug", "AI stealth squad {0} [{1}:{2}] hazard route to {3}#{4}: routed={5} withheld={6} waypoints={7} avoided-resources={8} pending-radius={9} action={10}.",
					Info.SquadLabel, player.PlayerName, group.Index, target.Info.Name, target.ActorID,
					routedUnits, 0, waypointCount, string.Join("/", Info.AvoidResourceTypes.OrderBy(t => t)),
					Info.PendingResourceExplosionAvoidanceRadius, crush ? "crush" : "attack");
		}

		List<CPos> HazardAwarePath(Actor unit, Actor target, List<Threat> threats, int ownRange,
			bool crush, bool allowDangerousEndpoint, int minimumAttackRangeCells, bool ignoreTargetDetector = false,
			Dictionary<CPos, List<CPos>> routeCache = null, HashSet<CPos> unroutableCells = null)
		{
			var mobile = unit.TraitOrDefault<Mobile>();
			if (mobile == null)
				return null;

			var map = GetInfluenceMap(threats, ignoreTargetDetector ? target : null);
			var minimumRange = minimumAttackRangeCells > 0 ?
				Math.Clamp(minimumAttackRangeCells, 1, Math.Max(1, ownRange)) : Math.Max(1, ownRange - 1);
			var approachCells = (crush ? new[] { target.Location } :
				world.Map.FindTilesInAnnulus(target.Location,
					minimumRange, Math.Max(1, ownRange)))
				.Where(c => mobile.CanEnterCell(c, target,
					crush ? BlockedByActor.None : BlockedByActor.Immovable) &&
					(domainIndex == null || domainIndex.IsPassable(unit.Location, c, mobile.Locomotor)) &&
					(allowDangerousEndpoint || !IsInfluencedCell(map, c)))
				.OrderBy(c => (c - unit.Location).LengthSquared).ThenBy(c => c.Y).ThenBy(c => c.X)
				.Take(1).ToArray();
			if (approachCells.Length == 0)
				return null;

			var destination = approachCells[0];
			var startX = Math.Clamp(unit.Location.X / map.CoarseSize, 0, map.Width - 1);
			var startY = Math.Clamp(unit.Location.Y / map.CoarseSize, 0, map.Height - 1);
			var goalX = Math.Clamp(destination.X / map.CoarseSize, 0, map.Width - 1);
			var goalY = Math.Clamp(destination.Y / map.CoarseSize, 0, map.Height - 1);
			var routeKey = new CPos(goalX, goalY);
			if (unroutableCells != null && unroutableCells.Contains(routeKey))
				return null;

			List<CPos> result;
			if (routeCache != null && routeCache.TryGetValue(routeKey, out var cachedRoute))
				result = new List<CPos>(cachedRoute);
			else
			{
				scanPathSearches++;
				var route = ThreatAwareRoutePlanner.FindRoute(map.Danger, map.Width, map.Height,
					startX, startY, goalX, goalY, map.Width * map.Height);
				if (route == null || route.Any(c => map.Danger[c.Y * map.Width + c.X] > 0))
				{
					unroutableCells?.Add(routeKey);
					return null;
				}

				result = ThreatAwareRoutePlanner.SmoothRoute(map.Danger, map.Width, map.Height,
					startX, startY, route).Select(c => world.Map.Clamp(new CPos(
					c.X * map.CoarseSize + map.CoarseSize / 2,
					c.Y * map.CoarseSize + map.CoarseSize / 2))).ToList();
				routeCache?.Add(routeKey, new List<CPos>(result));
			}

			if (result.Count == 0 || result[result.Count - 1] != destination)
				result.Add(destination);

			return result;
		}

		List<CPos> FindCoarseSafeRoute(CPos start, CPos destination, SpecialistInfluenceMap map)
		{
			var startX = Math.Clamp(start.X / map.CoarseSize, 0, map.Width - 1);
			var startY = Math.Clamp(start.Y / map.CoarseSize, 0, map.Height - 1);
			var goalX = Math.Clamp(destination.X / map.CoarseSize, 0, map.Width - 1);
			var goalY = Math.Clamp(destination.Y / map.CoarseSize, 0, map.Height - 1);
			var route = ThreatAwareRoutePlanner.FindRoute(map.Danger, map.Width, map.Height,
				startX, startY, goalX, goalY, map.Width * map.Height);
			if (route == null || route.Any(c => map.Danger[c.Y * map.Width + c.X] > 0))
				return null;

			var result = ThreatAwareRoutePlanner.SmoothRoute(map.Danger, map.Width, map.Height,
				startX, startY, route).Select(c => world.Map.Clamp(new CPos(
					c.X * map.CoarseSize + map.CoarseSize / 2,
					c.Y * map.CoarseSize + map.CoarseSize / 2))).ToList();
			if (result.Count == 0 || result[result.Count - 1] != destination)
				result.Add(destination);

			return result;
		}

		bool IsResourceHazard(CPos cell)
		{
			scanResourceCellTests++;
			if (resourceLayer == null)
				return false;
			if (resourceHazardCache.TryGetValue(cell, out var cached))
				return cached;

			var type = resourceLayer.GetResource(cell).Type;
			if (type != null && Info.AvoidResourceTypes.Contains(type))
			{
				resourceHazardCache.Add(cell, true);
				return true;
			}

			var radius = Info.PendingResourceExplosionAvoidanceRadius;
			var result = radius > 0 && world.Map.FindTilesInAnnulus(cell, 0, radius)
				.Any(resourceLayer.IsExplosionPending);
			resourceHazardCache.Add(cell, result);
			return result;
		}

		bool IsTransitThreatenedCell(CPos cell, Actor intendedTarget, List<Threat> threats, int ownRange)
		{
			return IsInfluencedCell(GetInfluenceMap(threats), cell);
		}

		bool IsInfluencedCell(SpecialistInfluenceMap map, CPos cell)
		{
			scanInfluenceCellTests++;
			if (!world.Map.Contains(cell))
				return true;

			var x = Math.Clamp(cell.X / map.CoarseSize, 0, map.Width - 1);
			var y = Math.Clamp(cell.Y / map.CoarseSize, 0, map.Height - 1);
			return map.Danger[y * map.Width + x] > 0;
		}

		SpecialistInfluenceMap GetInfluenceMap(List<Threat> threats, Actor ignoredThreat = null)
		{
			const int CacheInterval = 125;
			if (ignoredThreat != null)
				return BuildInfluenceMap(threats, ignoredThreat);

			var cacheCurrent = influenceMap != null &&
				!StealthTankSquadPolicy.ShouldRefreshInfluenceMap(influenceMap.Tick, world.WorldTick, CacheInterval);
			if (cacheCurrent)
			{
				scanInfluenceHits++;
				return influenceMap;
			}

			influenceMap = BuildInfluenceMap(threats, null);
			scanInfluenceBuilds++;
			return influenceMap;
		}

		SpecialistInfluenceMap BuildInfluenceMap(List<Threat> threats, Actor ignoredThreat)
		{
			var started = Stopwatch.GetTimestamp();
			var coarseSize = Math.Max(1, StrategicCellSize);
			var width = (world.Map.MapSize.X + coarseSize - 1) / coarseSize;
			var height = (world.Map.MapSize.Y + coarseSize - 1) / coarseSize;
			var map = new SpecialistInfluenceMap(world.WorldTick, coarseSize, width, height);
			foreach (var threat in threats)
			{
				if (threat.Actor == ignoredThreat)
					continue;

				var detectorRange = StealthTankSquadPolicy.BufferedRange(threat.DetectorRangeCells,
					Info.DetectorRangeBufferCells);
				var ignoreWeapon = threat.Actor.GetEnabledTargetTypes()
					.Overlaps(Info.IgnoredHarassmentWeaponThreatTypes);
				var weaponRange = StealthTankSquadPolicy.BufferedRange(
					ignoreWeapon ? 0 : threat.WeaponRangeCells,
					Info.ThreatRangeBufferCells);
				MarkThreatRange(map, threat, Math.Max(detectorRange, weaponRange));
			}

			MarkPendingExplosionCells(map);

			scanThreatMapTicks += Stopwatch.GetTimestamp() - started;
			return map;
		}

		void MarkPendingExplosionCells(SpecialistInfluenceMap map)
		{
			if (resourceLayer == null || Info.PendingResourceExplosionAvoidanceRadius <= 0)
				return;

			var radius = Info.PendingResourceExplosionAvoidanceRadius + map.CoarseSize;
			for (var y = 0; y < map.Height; y++)
				for (var x = 0; x < map.Width; x++)
				{
					scanResourceCellTests++;
					var center = world.Map.Clamp(new CPos(x * map.CoarseSize + map.CoarseSize / 2,
						y * map.CoarseSize + map.CoarseSize / 2));
					if (world.Map.FindTilesInAnnulus(center, 0, radius).Any(resourceLayer.IsExplosionPending))
						map.Danger[y * map.Width + x] += 1;
				}
		}

		void MarkThreatRange(SpecialistInfluenceMap map, Threat threat, int range)
		{
			if (range <= 0)
				return;

			// Mark any coarse cell whose footprint can intersect the live threat envelope.
			var conservativeRange = range + map.CoarseSize;
			var minX = Math.Max(0, (threat.Actor.Location.X - conservativeRange) / map.CoarseSize);
			var maxX = Math.Min(map.Width - 1, (threat.Actor.Location.X + conservativeRange) / map.CoarseSize);
			var minY = Math.Max(0, (threat.Actor.Location.Y - conservativeRange) / map.CoarseSize);
			var maxY = Math.Min(map.Height - 1, (threat.Actor.Location.Y + conservativeRange) / map.CoarseSize);
			var rangeLength = conservativeRange * 1024;
			for (var y = minY; y <= maxY; y++)
				for (var x = minX; x <= maxX; x++)
				{
					var cell = world.Map.Clamp(new CPos(x * map.CoarseSize + map.CoarseSize / 2,
						y * map.CoarseSize + map.CoarseSize / 2));
					if ((world.Map.CenterOfCell(cell) - threat.Actor.CenterPosition).Length <= rangeLength)
						map.Danger[y * map.Width + x] += 1;
				}
		}

		int InfantryClusterMultiplier(StealthTankSquadRole role, Actor actor)
		{
			if (role != StealthTankSquadRole.Harass || Info.InfantryClusterRadiusCells <= 0 ||
				Info.InfantryClusterBonusPercentPerNearbyActor <= 0 ||
				!actor.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes))
				return 100;

			var nearby = world.FindActorsInCircle(actor.CenterPosition, WDist.FromCells(Info.InfantryClusterRadiusCells))
				.Count(a => a != actor && IsEnemyTarget(a) && a.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes));
			return StealthTankSquadPolicy.InfantryClusterMultiplier(nearby,
				Info.InfantryClusterBonusPercentPerNearbyActor, Info.MaximumInfantryClusterMultiplierPercent);
		}

		int Priority(StealthTankSquadRole role, Actor actor, int groupSize)
		{
			var types = actor.GetEnabledTargetTypes();
			if (role == StealthTankSquadRole.Harass && types.Overlaps(Info.ExcludedHarassmentTargetTypes))
				return 0;

			if (role == StealthTankSquadRole.Harass && groupSize >= Info.MinimumLateHarassmentGroupSize &&
				Info.LateHarassmentTargetPriorities.TryGetValue(actor.Info.Name, out var latePriority))
				return latePriority;

			var configured = role == StealthTankSquadRole.Harass ?
				Info.HarassmentTargetPriorities : Info.AttackTargetPriorities;
			if (configured.TryGetValue(actor.Info.Name, out var priority))
				return priority;

			if (role == StealthTankSquadRole.Attack)
				return types.Overlaps(TankTargetTypes) ? 8000 : 0;
			if (types.Overlaps(InfantryTargetTypes))
				return Info.InfantryTargetPriority;
			if (actor.Info.HasTraitInfo<LineBuildNodeInfo>())
				return Info.WallTargetPriority;
			if (types.Overlaps(StructureTargetTypes))
			{
				var armorType = actor.Info.TraitInfoOrDefault<ArmorInfo>()?.Type;
				if (armorType != null && Info.HarassmentArmorPriorities.TryGetValue(armorType, out var armorPriority))
					return armorPriority;

				return Info.StructureTargetPriority;
			}

			return types.Overlaps(TankTargetTypes) ? Info.TankTargetPriority : 0;
		}

		bool DangerAlongRun(WPos start, Actor target, List<Threat> threats, int ownRange, bool stopAtFirstDanger,
			out int defendingValue, out Actor strongestDefender, out List<Threat> defenders)
		{
			defendingValue = 0;
			strongestDefender = null;
			defenders = new List<Threat>();
			var strongestValue = 0;
			var dangerous = false;
			foreach (var threat in threats)
			{
				scanCandidateThreatTests++;
				var detectorRange = StealthTankSquadPolicy.BufferedRange(threat.DetectorRangeCells,
					Info.DetectorRangeBufferCells);
				var ignoreWeapon = stopAtFirstDanger &&
					threat.Actor.GetEnabledTargetTypes().Overlaps(Info.IgnoredHarassmentWeaponThreatTypes);
				var weaponRange = StealthTankSquadPolicy.BufferedRange(ignoreWeapon ? 0 : threat.WeaponRangeCells,
					Info.ThreatRangeBufferCells);
				var targetDistance = (threat.Actor.CenterPosition - target.CenterPosition).Length / 1024;

				// Candidate screening only proves whether an approach may exist. The route planner
				// still keeps this detector in its influence map and must find a firing cell outside
				// its coverage. Never apply this exception to an armed target or a separate detector.
				var canOutrangeTargetDetector = StealthTankSquadPolicy.CanOutrangeTargetDetector(
					threat.Actor == target, threat.WeaponRangeCells, detectorRange, ownRange);
				if (Info.DebugLogging && canOutrangeTargetDetector)
					Log.Write("debug", "AI stealth target-local detector {0} [{1}] target={2}#{3} target-owner={4} raw-detector-range={5} buffered-detector-range={6} own-range={7} candidate-screen=permitted route-coverage=required.",
						Info.SquadLabel, player.PlayerName, target.Info.Name, target.ActorID,
						target.Owner.InternalName, threat.DetectorRangeCells, detectorRange, ownRange);
				var endpointDanger = (!canOutrangeTargetDetector && detectorRange > 0 &&
					targetDistance <= detectorRange) ||
					(weaponRange > 0 && targetDistance <= weaponRange &&
						(threat.Actor != target || ownRange < threat.WeaponRangeCells + Info.KiteRangeMarginCells));
				var canKiteTarget = threat.Actor == target && detectorRange <= 0 &&
					ownRange >= threat.WeaponRangeCells + Info.KiteRangeMarginCells;

				var routeDanger = SegmentPassesWithin(start, target.CenterPosition, threat.Actor.CenterPosition,
					StealthTankSquadPolicy.TransitThreatRange(canOutrangeTargetDetector ? 0 : detectorRange, weaponRange,
						threat.WeaponIsEngaged, canKiteTarget));
				if (!endpointDanger && !routeDanger)
					continue;

				dangerous = true;
				defendingValue += threat.Value;
				defenders.Add(threat);
				if (threat.Value > strongestValue)
				{
					strongestValue = threat.Value;
					strongestDefender = threat.Actor;
				}
			}

			return dangerous;
		}

		static bool SegmentPassesWithin(WPos from, WPos to, WPos threat, int rangeCells)
		{
			if (rangeCells <= 0)
				return false;

			var dx = to.X - from.X;
			var dy = to.Y - from.Y;
			var lengthSquared = (long)dx * dx + (long)dy * dy;
			if (lengthSquared == 0)
				return (threat - from).Length <= rangeCells * 1024;

			var tx = threat.X - from.X;
			var ty = threat.Y - from.Y;
			var projection = Math.Max(0d, Math.Min(1d, ((long)tx * dx + (long)ty * dy) / (double)lengthSquared));
			var closestX = from.X + dx * projection;
			var closestY = from.Y + dy * projection;
			var distanceX = threat.X - closestX;
			var distanceY = threat.Y - closestY;
			var range = rangeCells * 1024d;
			return distanceX * distanceX + distanceY * distanceY <= range * range;
		}
	}
}
