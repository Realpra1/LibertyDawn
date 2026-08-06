#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Loads stealth harvesters with unstable resources and sends them toward valuable enemy structures.")]
	public class RedTiberiumBombBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between bounded mission scans. Zero disables the module.")]
		public readonly int ScanInterval = 50;

		[ActorReference]
		[Desc("Harvester actor types eligible for bomb missions.")]
		public readonly HashSet<string> StealthHarvesterTypes = new HashSet<string>();

		[ActorReference]
		[Desc("All actor types counted when calculating the percentage launch rate.")]
		public readonly HashSet<string> HarvesterTypes = new HashSet<string>();

		[Desc("Unstable resource types that arm a mission.")]
		public readonly HashSet<string> UnstableResourceTypes = new HashSet<string>();

		[Desc("Maximum percentage of live harvesters committed per game minute.")]
		public readonly int LaunchPercentPerMinute = 5;

		[Desc("Game ticks in one game minute.")]
		public readonly int GameTicksPerMinute = 1500;

		[Desc("Maximum completed launch allowances stored while no mission is started.")]
		public readonly int MaximumStoredLaunches = 1;

		[Desc("Maximum target actors considered for each mission.")]
		public readonly int MaximumTargetCandidates = 32;

		[Desc("Maximum distance in cells between the destination and target footprint.")]
		public readonly int TargetApproachRadius = 1;

		[Desc("Ticks between repeated orders when a mission stops making progress.")]
		public readonly int MissionStallInterval = 250;

		[Desc("Minimum ticks between equivalent mission orders.")]
		public readonly int OrderRetryInterval = 75;

		[Desc("Configured target priority by actor type. Economic value breaks out actors within each priority.")]
		public readonly Dictionary<string, int> TargetPriorities = new Dictionary<string, int>();

		[Desc("Write bounded mission decisions to debug.log.")]
		public readonly bool DebugLogging = false;

		[Desc("Minimum ticks between periodic debug summaries. Mission events are logged immediately.")]
		public readonly int DebugSummaryInterval = 500;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (ScanInterval <= 0 || StealthHarvesterTypes.Count == 0 || HarvesterTypes.Count == 0 ||
				UnstableResourceTypes.Count == 0 || LaunchPercentPerMinute <= 0 || LaunchPercentPerMinute > 100 ||
				GameTicksPerMinute <= 0 || MaximumStoredLaunches <= 0 || MaximumTargetCandidates <= 0 ||
				TargetApproachRadius <= 0 || MissionStallInterval < ScanInterval || OrderRetryInterval <= 0 ||
				DebugSummaryInterval <= 0 || TargetPriorities.Count == 0 || TargetPriorities.Any(kv => kv.Value <= 0))
				throw new YamlException("Red-Tiberium bomb mission actors, resources, rate, bounds, and target priorities must be valid.");
		}

		public override object Create(ActorInitializer init) { return new RedTiberiumBombBotModule(init.Self, this); }
	}

	public class RedTiberiumBombBotModule : ConditionalTrait<RedTiberiumBombBotModuleInfo>,
		IBotTick, IBotUnitReservations, IBotHarvesterResourcePolicy, IGameSaveTraitData
	{
		enum MissionState { Harvesting, Armed, WaitingToDetonate, DeployOrdered }

		sealed class Mission
		{
			public uint HarvesterId;
			public uint TargetActorId;
			public CPos ResourceCell;
			public CPos Destination;
			public CPos LastPosition;
			public MissionState State;
			public long BestDistanceSquared;
			public int LastProgressTick;
			public int LastOrderTick;
			public int InitialTargetHp;
			public int LastTargetHp;
		}

		readonly World world;
		readonly Player player;
		readonly Dictionary<uint, Mission> missions = new Dictionary<uint, Mission>();
		IResourceLayer resourceLayer;
		ResourceClaimLayer claimLayer;
		DomainIndex domainIndex;
		IPathFinder pathfinder;
		IBotUnitReservations[] otherReservations;
		int scanTicks;
		int lastBudgetTick;
		int lastDebugSummaryTick;
		long launchBudget;

		public RedTiberiumBombBotModule(Actor self, RedTiberiumBombBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			resourceLayer = world.WorldActor.TraitOrDefault<IResourceLayer>();
			claimLayer = world.WorldActor.TraitOrDefault<ResourceClaimLayer>();
			domainIndex = world.WorldActor.Trait<DomainIndex>();
			pathfinder = world.WorldActor.Trait<IPathFinder>();
			otherReservations = player.PlayerActor.TraitsImplementing<IBotUnitReservations>()
				.Where(r => !ReferenceEquals(r, this)).ToArray();
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self)
		{
			scanTicks = 2;
			lastBudgetTick = world.WorldTick;
		}

		protected override void TraitDisabled(Actor self)
		{
			missions.Clear();
			launchBudget = 0;
		}

		bool IBotUnitReservations.IsUnitReserved(Actor actor)
		{
			return actor != null && missions.ContainsKey(actor.ActorID);
		}

		bool IBotHarvesterResourcePolicy.CanHarvestResource(Actor harvester, string resourceType)
		{
			if (IsTraitDisabled || harvester == null || harvester.Owner != player ||
				!Info.StealthHarvesterTypes.Contains(harvester.Info.Name) ||
				!Info.UnstableResourceTypes.Contains(resourceType))
				return true;

			return false;
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (IsTraitDisabled || player.WinState != WinState.Undefined || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			var redCells = FindUnstableResourceCells();
			ReviewMissions(bot, redCells);

			var allHarvesters = world.Actors.Count(IsLiveHarvester);
			var candidates = world.Actors.Where(IsEligibleStealthHarvester)
				.Where(a => !missions.ContainsKey(a.ActorID) && !IsReservedByOtherModule(a))
				.OrderByDescending(a => a.Trait<Harvester>().IsUnstable)
				.ThenByDescending(a => a.Trait<Harvester>().IsEmpty)
				.ThenBy(a => a.Trait<Harvester>().Fullness)
				.ThenBy(a => a.ActorID).ToList();
			var hasTarget = HasUnreservedTarget(candidates.FirstOrDefault());
			var hasResource = redCells.Count > 0 || candidates.Any(a => a.Trait<Harvester>().IsUnstable);
			var elapsed = Math.Max(0, world.WorldTick - lastBudgetTick);
			lastBudgetTick = world.WorldTick;
			if (candidates.Count > 0 && hasTarget && hasResource)
				launchBudget = RedTiberiumBombPolicy.AccrueLaunchBudget(launchBudget, allHarvesters, elapsed,
					Info.LaunchPercentPerMinute, Info.GameTicksPerMinute, Info.MaximumStoredLaunches);

			while (candidates.Count > 0 && RedTiberiumBombPolicy.CanLaunch(launchBudget, Info.GameTicksPerMinute))
			{
				var harvester = candidates[0];
				candidates.RemoveAt(0);
				if (!TryStartMission(bot, harvester, redCells))
					continue;

				launchBudget = RedTiberiumBombPolicy.SpendLaunch(launchBudget, Info.GameTicksPerMinute);
			}

			if (world.WorldTick - lastDebugSummaryTick >= Info.DebugSummaryInterval)
			{
				lastDebugSummaryTick = world.WorldTick;
				Debug("scan harvesters={0} eligible={1} red-cells={2} active={3} budget={4}/{5}",
					allHarvesters, candidates.Count, redCells.Count, missions.Count, launchBudget,
					RedTiberiumBombPolicy.LaunchCost(Info.GameTicksPerMinute));
			}
		}

		bool IsLiveHarvester(Actor actor)
		{
			return actor != null && actor.Owner == player && actor.IsInWorld && !actor.IsDead &&
				Info.HarvesterTypes.Contains(actor.Info.Name) && actor.Info.HasTraitInfo<HarvesterInfo>();
		}

		bool IsEligibleStealthHarvester(Actor actor)
		{
			return actor != null && actor.Owner == player && actor.IsInWorld && !actor.IsDead &&
				Info.StealthHarvesterTypes.Contains(actor.Info.Name) && actor.TraitOrDefault<Harvester>() != null &&
				actor.TraitOrDefault<Mobile>() != null;
		}

		bool IsReservedByOtherModule(Actor actor)
		{
			return otherReservations != null && otherReservations.Any(r => r.IsUnitReserved(actor));
		}

		List<CPos> FindUnstableResourceCells()
		{
			if (resourceLayer == null || resourceLayer.IsEmpty)
				return new List<CPos>();

			return world.Map.AllCells.Where(c => c.Layer == 0)
				.Where(c =>
				{
					var resource = resourceLayer.GetResource(c);
					return resource.Density > 0 && Info.UnstableResourceTypes.Contains(resource.Type);
				})
				.OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
		}

		bool TryStartMission(IBot bot, Actor harvester, List<CPos> redCells)
		{
			if (!TrySelectTarget(harvester, out var target, out var destination, out var rejection))
			{
				Debug("rejected {0}#{1}: no target ({2})", harvester.Info.Name, harvester.ActorID, rejection);
				return false;
			}

			var trait = harvester.Trait<Harvester>();
			var mission = new Mission
			{
				HarvesterId = harvester.ActorID,
				TargetActorId = target.ActorID,
				Destination = destination,
				LastPosition = harvester.Location,
				InitialTargetHp = TargetHp(target),
				LastTargetHp = TargetHp(target),
				LastProgressTick = world.WorldTick,
				LastOrderTick = world.WorldTick
			};

			if (trait.IsUnstable)
			{
				mission.State = MissionState.Armed;
				mission.BestDistanceSquared = DistanceSquared(harvester.Location, destination);
				missions.Add(harvester.ActorID, mission);
				IssueAttackMove(bot, harvester, mission, "claimed already-armed harvester");
				return true;
			}

			if (!TrySelectResourceCell(harvester, redCells, out var resourceCell))
			{
				Debug("rejected {0}#{1}: no reachable unclaimed unstable resource", harvester.Info.Name,
					harvester.ActorID);
				return false;
			}

			mission.State = MissionState.Harvesting;
			mission.ResourceCell = resourceCell;
			mission.BestDistanceSquared = DistanceSquared(harvester.Location, resourceCell);
			missions.Add(harvester.ActorID, mission);
			bot.QueueOrder(new Order("HarvestUnstable", harvester, Target.FromCell(world, resourceCell), false));
			Debug("launched {0}#{1}: harvest actual {2} at {3}, then target {4}#{5} via {6}",
				harvester.Info.Name, harvester.ActorID, resourceLayer.GetResource(resourceCell).Type, resourceCell,
				target.Info.Name, target.ActorID, destination);
			return true;
		}

		void ReviewMissions(IBot bot, List<CPos> redCells)
		{
			foreach (var harvesterId in missions.Keys.OrderBy(id => id).ToList())
			{
				var mission = missions[harvesterId];
				var harvester = world.GetActorById(harvesterId);
				if (!IsEligibleStealthHarvester(harvester))
				{
					LogMissionEnd(mission, harvester);
					missions.Remove(harvesterId);
					continue;
				}

				mission.LastPosition = harvester.Location;
				var trait = harvester.Trait<Harvester>();
				if (mission.State == MissionState.Harvesting && trait.IsUnstable)
				{
					mission.State = MissionState.Armed;
					if (!EnsureTarget(harvester, mission))
					{
						Debug("armed {0}#{1} with real unstable cargo but no enemy target; holding away from refinery",
							harvester.Info.Name, harvester.ActorID);
						continue;
					}

					mission.BestDistanceSquared = DistanceSquared(harvester.Location, mission.Destination);
					mission.LastProgressTick = world.WorldTick;
					IssueAttackMove(bot, harvester, mission, "armed with real unstable cargo");
					continue;
				}

				if (mission.State == MissionState.Harvesting)
				{
					ReviewHarvestingMission(bot, harvester, mission, redCells);
					continue;
				}

				ReviewArmedMission(bot, harvester, mission);
			}
		}

		void ReviewHarvestingMission(IBot bot, Actor harvester, Mission mission, List<CPos> redCells)
		{
			var resource = resourceLayer.GetResource(mission.ResourceCell);
			if (resource.Density <= 0 || !Info.UnstableResourceTypes.Contains(resource.Type))
			{
				if (!TrySelectResourceCell(harvester, redCells, out var replacement))
				{
					Debug("released {0}#{1}: unstable resource vanished or became unreachable",
						harvester.Info.Name, harvester.ActorID);
					missions.Remove(harvester.ActorID);
					return;
				}

				mission.ResourceCell = replacement;
				mission.BestDistanceSquared = DistanceSquared(harvester.Location, replacement);
				mission.LastProgressTick = world.WorldTick;
				Debug("retargeted {0}#{1} to replacement unstable resource at {2}", harvester.Info.Name,
					harvester.ActorID, replacement);
			}

			var distance = DistanceSquared(harvester.Location, mission.ResourceCell);
			if (RedTiberiumBombPolicy.MadeProgress(distance, mission.BestDistanceSquared))
			{
				mission.BestDistanceSquared = distance;
				mission.LastProgressTick = world.WorldTick;
			}

			if ((harvester.IsIdle || RedTiberiumBombPolicy.HasStalled(world.WorldTick, mission.LastProgressTick,
				Info.MissionStallInterval)) && world.WorldTick - mission.LastOrderTick >= Info.OrderRetryInterval)
			{
				bot.QueueOrder(new Order("HarvestUnstable", harvester,
					Target.FromCell(world, mission.ResourceCell), false));
				mission.LastOrderTick = world.WorldTick;
				mission.LastProgressTick = world.WorldTick;
				Debug("reissued harvest for {0}#{1} at {2}", harvester.Info.Name, harvester.ActorID,
					mission.ResourceCell);
			}
		}

		void ReviewArmedMission(IBot bot, Actor harvester, Mission mission)
		{
			if (!EnsureTarget(harvester, mission))
			{
				if (mission.State != MissionState.WaitingToDetonate && harvester.IsIdle)
					Debug("armed {0}#{1} has no replacement target; holding at former enemy destination {2}",
						harvester.Info.Name, harvester.ActorID, mission.Destination);
				return;
			}

			var target = GetMissionTarget(mission);
			mission.LastTargetHp = TargetHp(target);
			if (IsAtTarget(harvester, target, mission.Destination))
			{
				var detonation = harvester.TraitOrDefault<UnstableHarvesterDetonation>();
				if (detonation?.CanDetonate == true &&
					world.WorldTick - mission.LastOrderTick >= Info.OrderRetryInterval)
				{
					bot.QueueOrder(new Order("DetonateUnstableHarvester", harvester, false));
					mission.State = MissionState.DeployOrdered;
					mission.LastOrderTick = world.WorldTick;
					Debug("ordered ready {0}#{1} to deploy at {2} in blast range of {3}#{4}; unstable-age={5}",
						harvester.Info.Name, harvester.ActorID, harvester.Location, target.Info.Name,
						target.ActorID, detonation.UnstableTicks);
				}
				else if (mission.State != MissionState.WaitingToDetonate &&
					mission.State != MissionState.DeployOrdered)
				{
					mission.State = MissionState.WaitingToDetonate;
					Debug("arrived {0}#{1} at {2} in blast range of {3}#{4}; waiting for deploy readiness age={5}",
						harvester.Info.Name, harvester.ActorID, harvester.Location, target.Info.Name, target.ActorID,
						detonation?.UnstableTicks ?? -1);
				}

				return;
			}

			mission.State = MissionState.Armed;
			var distance = DistanceSquared(harvester.Location, mission.Destination);
			if (RedTiberiumBombPolicy.MadeProgress(distance, mission.BestDistanceSquared))
			{
				mission.BestDistanceSquared = distance;
				mission.LastProgressTick = world.WorldTick;
			}

			if ((harvester.IsIdle || RedTiberiumBombPolicy.HasStalled(world.WorldTick, mission.LastProgressTick,
				Info.MissionStallInterval)) && world.WorldTick - mission.LastOrderTick >= Info.OrderRetryInterval)
			{
				if (TryFindApproachCell(harvester, target, out var replacement))
					mission.Destination = replacement;

				IssueAttackMove(bot, harvester, mission, harvester.IsIdle ? "idle retry" : "stalled retry");
			}
		}

		bool EnsureTarget(Actor harvester, Mission mission)
		{
			var target = GetMissionTarget(mission);
			if (IsValidTarget(target))
				return true;

			var oldTarget = target;
			if (!TrySelectTarget(harvester, out target, out var destination, out var rejection, mission.HarvesterId))
			{
				if (oldTarget != null && oldTarget.Owner.RelationshipWith(player) != PlayerRelationship.Enemy)
					mission.Destination = FindFriendlySafeFallback(harvester);

				mission.TargetActorId = 0;
				Debug("no replacement target for armed {0}#{1}: {2}", harvester.Info.Name,
					harvester.ActorID, rejection);
				return false;
			}

			mission.TargetActorId = target.ActorID;
			mission.Destination = destination;
			mission.InitialTargetHp = TargetHp(target);
			mission.LastTargetHp = mission.InitialTargetHp;
			mission.BestDistanceSquared = DistanceSquared(harvester.Location, destination);
			mission.LastProgressTick = world.WorldTick;
			Debug("retargeted armed {0}#{1} from {2} to {3}#{4} via {5}", harvester.Info.Name,
				harvester.ActorID, oldTarget?.Info.Name ?? "missing", target.Info.Name, target.ActorID, destination);
			return true;
		}

		void IssueAttackMove(IBot bot, Actor harvester, Mission mission, string reason)
		{
			bot.QueueOrder(new Order("Move", harvester, Target.FromCell(world, mission.Destination), false));
			mission.LastOrderTick = world.WorldTick;
			mission.LastProgressTick = world.WorldTick;
			mission.BestDistanceSquared = DistanceSquared(harvester.Location, mission.Destination);
			Debug("ordered armed {0}#{1} -> {2} targeting actor #{3}: {4}", harvester.Info.Name,
				harvester.ActorID, mission.Destination, mission.TargetActorId, reason);
		}

		bool TrySelectResourceCell(Actor harvester, IEnumerable<CPos> cells, out CPos selected)
		{
			var reserved = missions.Values.Where(m => m.State == MissionState.Harvesting)
				.Select(m => m.ResourceCell).ToHashSet();
			foreach (var cell in cells.Where(c => !reserved.Contains(c))
				.OrderBy(c => DistanceSquared(harvester.Location, c)).ThenBy(c => c.Y).ThenBy(c => c.X))
			{
				if (claimLayer != null && claimLayer.CanClaimCell(harvester, cell) && CanReachCell(harvester, cell))
				{
					selected = cell;
					return true;
				}
			}

			selected = default(CPos);
			return false;
		}

		bool HasUnreservedTarget(Actor harvester)
		{
			return harvester != null && world.Actors.Any(a => IsValidTarget(a) &&
				!missions.Values.Any(m => m.TargetActorId == a.ActorID));
		}

		bool TrySelectTarget(Actor harvester, out Actor selected, out CPos destination, out string rejection,
			uint replacingMissionHarvesterId = 0)
		{
			selected = null;
			destination = default(CPos);
			rejection = "no configured live enemy structure";
			if (harvester == null)
				return false;

			var reservedTargets = missions.Values.Where(m => m.HarvesterId != replacingMissionHarvesterId)
				.Select(m => m.TargetActorId).Where(id => id != 0).ToHashSet();
			foreach (var target in world.Actors.Where(a => IsValidTarget(a) && !reservedTargets.Contains(a.ActorID))
				.Select(a => new
				{
					Actor = a,
					Score = RedTiberiumBombPolicy.TargetScore(Info.TargetPriorities[a.Info.Name],
						a.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1),
					Distance = (harvester.CenterPosition - a.CenterPosition).LengthSquared
				})
				.OrderByDescending(c => c.Score).ThenBy(c => c.Distance).ThenBy(c => c.Actor.ActorID)
				.Take(Info.MaximumTargetCandidates))
			{
				if (!TryFindApproachCell(harvester, target.Actor, out var approach, replacingMissionHarvesterId))
				{
					rejection = "configured targets have no reachable distinct approach cell";
					continue;
				}

				selected = target.Actor;
				destination = approach;
				return true;
			}

			return false;
		}

		bool IsValidTarget(Actor actor)
		{
			return actor != null && actor.IsInWorld && !actor.IsDead &&
				player.RelationshipWith(actor.Owner) == PlayerRelationship.Enemy &&
				Info.TargetPriorities.ContainsKey(actor.Info.Name) && actor.OccupiesSpace != null;
		}

		bool TryFindApproachCell(Actor harvester, Actor target, out CPos selected, uint replacingMissionHarvesterId = 0)
		{
			selected = default(CPos);
			if (harvester == null || target?.OccupiesSpace == null)
				return false;

			var footprint = target.OccupiesSpace.OccupiedCells().Select(c => c.Cell).Distinct().ToArray();
			if (footprint.Length == 0)
				footprint = new[] { target.Location };

			var cells = footprint.AsEnumerable();
			for (var i = 0; i < Info.TargetApproachRadius; i++)
				cells = Util.ExpandFootprint(cells, true);

			var occupied = footprint.ToHashSet();
			var reservedDestinations = missions.Values.Where(m => m.HarvesterId != replacingMissionHarvesterId)
				.Select(m => m.Destination).ToHashSet();
			foreach (var cell in cells.Distinct().Where(c => !occupied.Contains(c) && !reservedDestinations.Contains(c))
				.Where(c => world.Map.Contains(c) && c.Layer == 0)
				.OrderBy(c => DistanceSquared(harvester.Location, c)).ThenBy(c => c.Y).ThenBy(c => c.X))
			{
				if (CanReachCell(harvester, cell))
				{
					selected = cell;
					return true;
				}
			}

			return false;
		}

		bool CanReachCell(Actor actor, CPos cell)
		{
			var mobile = actor?.TraitOrDefault<Mobile>();
			if (mobile == null || !world.Map.Contains(cell) ||
				mobile.Locomotor.MovementCostForCell(cell) == PathGraph.MovementCostForUnreachableCell ||
				!mobile.CanEnterCell(cell, check: BlockedByActor.Immovable) ||
				!domainIndex.IsPassable(actor.Location, cell, mobile.Locomotor))
				return false;

			if (actor.Location == cell)
				return true;

			return pathfinder.FindUnitPath(actor.Location, cell, actor, null, BlockedByActor.Immovable).Count > 0;
		}

		bool IsAtTarget(Actor harvester, Actor target, CPos destination)
		{
			if (harvester.Location == destination)
				return true;

			var footprint = target.OccupiesSpace.OccupiedCells().Select(c => c.Cell);
			var radiusSquared = Info.TargetApproachRadius * Info.TargetApproachRadius;
			return footprint.Any(c => DistanceSquared(harvester.Location, c) <= radiusSquared);
		}

		CPos FindFriendlySafeFallback(Actor harvester)
		{
			var mobile = harvester.Trait<Mobile>();
			var alliedStructures = world.Actors.Where(a => a.IsInWorld && !a.IsDead && a.OccupiesSpace != null &&
				player.RelationshipWith(a.Owner).HasRelationship(PlayerRelationship.Ally) &&
				a.Info.HasTraitInfo<BuildingInfo>()).ToArray();
			var candidates = world.Map.AllCells.Where(c => c.Layer == 0 &&
				mobile.Locomotor.MovementCostForCell(c) != PathGraph.MovementCostForUnreachableCell &&
				domainIndex.IsPassable(harvester.Location, c, mobile.Locomotor));
			return candidates.OrderByDescending(c => alliedStructures.Length == 0 ? 0 : alliedStructures
				.Min(a => (world.Map.CenterOfCell(c) - a.CenterPosition).LengthSquared))
				.ThenBy(c => c.Y).ThenBy(c => c.X).FirstOrDefault(harvester.Location);
		}

		void LogMissionEnd(Mission mission, Actor harvester)
		{
			var target = GetMissionTarget(mission);
			var targetHp = TargetHp(target);
			var nearTarget = target != null && target.OccupiesSpace != null &&
				target.OccupiesSpace.OccupiedCells().Any(c =>
					DistanceSquared(mission.LastPosition, c.Cell) <= Info.TargetApproachRadius * Info.TargetApproachRadius);
			Debug("ended bomber #{0} state={1} last={2} target={3}#{4} target-hp={5}->{6} near-target={7} reason={8}",
				mission.HarvesterId, mission.State, mission.LastPosition, target?.Info.Name ?? "missing",
				mission.TargetActorId, mission.InitialTargetHp, targetHp, nearTarget,
				harvester == null ? "disposed" : harvester.Owner != player ? "ownership changed" :
				harvester.IsDead ? "detonated or killed" : "unavailable");
		}

		static int TargetHp(Actor target)
		{
			return target?.TraitOrDefault<IHealth>()?.HP ?? 0;
		}

		Actor GetMissionTarget(Mission mission)
		{
			return mission.TargetActorId == 0 ? null : world.GetActorById(mission.TargetActorId);
		}

		static long DistanceSquared(CPos a, CPos b)
		{
			return (a - b).LengthSquared;
		}

		void Debug(string format, params object[] args)
		{
			if (!Info.DebugLogging)
				return;

			var message = string.Format(format, args);
			AIUtils.BotDebug("AI ({0}) red-Tiberium bomb: {1}", player.ClientIndex, message);
			Log.Write("debug", "AI red-Tiberium bomb: {0} (client {1}) at tick {2}: {3}",
				player, player.ClientIndex, world.WorldTick, message);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("RedBombScanTicks", FieldSaver.FormatValue(scanTicks)),
				new MiniYamlNode("RedBombLastBudgetTick", FieldSaver.FormatValue(lastBudgetTick)),
				new MiniYamlNode("RedBombLaunchBudget", FieldSaver.FormatValue(launchBudget)),
				new MiniYamlNode("RedBombMissions", "", missions.OrderBy(kv => kv.Key).Select(kv =>
					new MiniYamlNode("Mission", "", new List<MiniYamlNode>
					{
						new MiniYamlNode("Harvester", FieldSaver.FormatValue(kv.Value.HarvesterId)),
						new MiniYamlNode("Target", FieldSaver.FormatValue(kv.Value.TargetActorId)),
						new MiniYamlNode("Resource", FieldSaver.FormatValue(kv.Value.ResourceCell)),
						new MiniYamlNode("Destination", FieldSaver.FormatValue(kv.Value.Destination)),
						new MiniYamlNode("LastPosition", FieldSaver.FormatValue(kv.Value.LastPosition)),
						new MiniYamlNode("State", FieldSaver.FormatValue((int)kv.Value.State)),
						new MiniYamlNode("BestDistance", FieldSaver.FormatValue(kv.Value.BestDistanceSquared)),
						new MiniYamlNode("LastProgress", FieldSaver.FormatValue(kv.Value.LastProgressTick)),
						new MiniYamlNode("LastOrder", FieldSaver.FormatValue(kv.Value.LastOrderTick)),
						new MiniYamlNode("InitialTargetHp", FieldSaver.FormatValue(kv.Value.InitialTargetHp)),
						new MiniYamlNode("LastTargetHp", FieldSaver.FormatValue(kv.Value.LastTargetHp))
					})).ToList())
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			foreach (var node in data)
				switch (node.Key)
				{
					case "RedBombScanTicks":
						scanTicks = FieldLoader.GetValue<int>(node.Key, node.Value.Value);
						break;
					case "RedBombLastBudgetTick":
						lastBudgetTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value);
						break;
					case "RedBombLaunchBudget":
						launchBudget = FieldLoader.GetValue<long>(node.Key, node.Value.Value);
						break;
					case "RedBombMissions":
						missions.Clear();
						foreach (var missionNode in node.Value.Nodes)
							LoadMission(missionNode);
						break;
				}
		}

		void LoadMission(MiniYamlNode node)
		{
			T Load<T>(string key, T fallback = default(T))
			{
				var value = node.Value.Nodes.FirstOrDefault(n => n.Key == key);
				return value == null ? fallback : FieldLoader.GetValue<T>(key, value.Value.Value);
			}

			var harvesterId = Load<uint>("Harvester");
			if (harvesterId == 0)
				return;

			missions[harvesterId] = new Mission
			{
				HarvesterId = harvesterId,
				TargetActorId = Load<uint>("Target"),
				ResourceCell = Load<CPos>("Resource"),
				Destination = Load<CPos>("Destination"),
				LastPosition = Load<CPos>("LastPosition"),
				State = (MissionState)Load<int>("State"),
				BestDistanceSquared = Load<long>("BestDistance"),
				LastProgressTick = Load<int>("LastProgress"),
				LastOrderTick = Load<int>("LastOrder"),
				InitialTargetHp = Load<int>("InitialTargetHp"),
				LastTargetHp = Load<int>("LastTargetHp")
			};
		}
	}
}
