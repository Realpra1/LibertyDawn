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
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Collects currently visible crates and explores stale map regions during economic emergencies.")]
	public class CrateCollectorBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between bounded crate/exploration scans. Zero disables the module.")]
		public readonly int ScanInterval = 250;

		[Desc("Ticks before the first scan, allowing the initial shroud to become current.")]
		public readonly int InitialScanDelay = 2;

		[Desc("Width and height in cells of one exploration region.")]
		public readonly int CoarseCellSize = 6;

		[Desc("Maximum stale regions considered for each new scout assignment.")]
		public readonly int MaximumRegionCandidates = 32;

		[Desc("Ticks without getting closer before an assignment is released and replanned.")]
		public readonly int AssignmentStallInterval = 500;

		[Desc("Spendable cash at or below which emergency exploration is enabled.")]
		public readonly int EmergencyCashThreshold = 0;

		[ActorReference]
		[Desc("Owned actors that prevent the no-MCV emergency condition.")]
		public readonly HashSet<string> McvTypes = new HashSet<string>();

		[ActorReference]
		[Desc("Actors never commandeered for crate collection or exploration.")]
		public readonly HashSet<string> ExcludedCollectorTypes = new HashSet<string>();

		[ActorReference]
		[Desc("Nonessential buildings that may be sold to create a scout as a last resort.")]
		public readonly HashSet<string> EmergencySellActorTypes = new HashSet<string>();

		[Desc("Minimum ticks between emergency building sales.")]
		public readonly int EmergencySellInterval = 1500;

		[Desc("Minimum collector health percentage.")]
		public readonly int MinimumCollectorHealthPercent = 75;

		[Desc("Crush class used by collectible crates.")]
		public readonly string CrateCrushClass = "crate";

		[Desc("Write bounded crate and exploration decisions to debug.log.")]
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (ScanInterval <= 0 || InitialScanDelay <= 0 || CoarseCellSize <= 0 || MaximumRegionCandidates <= 0 ||
				AssignmentStallInterval < ScanInterval || EmergencyCashThreshold < 0 ||
				EmergencySellInterval <= 0 || MinimumCollectorHealthPercent <= 0 ||
				MinimumCollectorHealthPercent > 100 || string.IsNullOrEmpty(CrateCrushClass))
				throw new YamlException("Crate collection intervals, region bounds, health, cash, and crush class must be valid.");
		}

		public override object Create(ActorInitializer init) { return new CrateCollectorBotModule(init.Self, this); }
	}

	public class CrateCollectorBotModule : ConditionalTrait<CrateCollectorBotModuleInfo>,
		IBotTick, IBotUnitReservations, IGameSaveTraitData
	{
		enum AssignmentKind { Crate, Scout }

		sealed class Assignment
		{
			public AssignmentKind Kind;
			public uint TargetActorId;
			public int RegionIndex = -1;
			public CPos Destination;
			public long BestDistanceSquared;
			public int LastProgressTick;
		}

		sealed class Region
		{
			public readonly int Index;
			public readonly List<CPos> Cells = new List<CPos>();

			public Region(int index) { Index = index; }
		}

		readonly World world;
		readonly Player player;
		readonly Dictionary<uint, Assignment> assignments = new Dictionary<uint, Assignment>();
		readonly List<Region> regions = new List<Region>();
		PlayerResources resources;
		DomainIndex domainIndex;
		IBotUnitReservations[] otherUnitReservations;
		IBotTransportReservations[] transportReservations;
		SquadManagerBotModule squadManager;
		int[] lastVisibleTicks = Array.Empty<int>();
		int scanTicks;
		int nextEmergencySaleTick;
		bool initialScanPending;

		public CrateCollectorBotModule(Actor self, CrateCollectorBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			resources = self.TraitOrDefault<PlayerResources>();
			domainIndex = world.WorldActor.Trait<DomainIndex>();
			otherUnitReservations = self.Owner.PlayerActor.TraitsImplementing<IBotUnitReservations>()
				.Where(r => !ReferenceEquals(r, this)).ToArray();
			transportReservations = self.Owner.PlayerActor.TraitsImplementing<IBotTransportReservations>().ToArray();
			squadManager = self.Owner.PlayerActor.TraitsImplementing<SquadManagerBotModule>()
				.FirstOrDefault(m => !m.IsTraitDisabled);
			BuildRegions();
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self)
		{
			// Provisionally reserve eligible collectors until the initial shroud has been populated.
			initialScanPending = true;
			scanTicks = Info.InitialScanDelay;
		}

		protected override void TraitDisabled(Actor self)
		{
			initialScanPending = false;
			assignments.Clear();
		}

		bool IBotUnitReservations.IsUnitReserved(Actor actor)
		{
			return actor != null && (assignments.ContainsKey(actor.ActorID) ||
				(initialScanPending && IsSuitableCollector(actor)));
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (IsTraitDisabled || player.WinState != WinState.Undefined || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			if (squadManager == null || squadManager.IsTraitDisabled)
				squadManager = player.PlayerActor.TraitsImplementing<SquadManagerBotModule>()
					.FirstOrDefault(m => !m.IsTraitDisabled);

			RefreshRegionVisibility();
			var emergency = IsEmergency();
			var temporarilyBlocked = ReviewAssignments(emergency);
			var visibleCrates = VisibleCrates();

			if (!emergency)
				ReleaseScoutAssignments("emergency ended");
			else if (visibleCrates.Any(c => !assignments.Values.Any(a =>
				a.Kind == AssignmentKind.Crate && a.TargetActorId == c.ActorID)))
				ReleaseScoutAssignments("visible crate takes priority");

			var collectors = SuitableCollectors()
				.Where(a => !assignments.ContainsKey(a.ActorID) && !temporarilyBlocked.Contains(a.ActorID))
				.Where(a => emergency || a.IsIdle)
				.OrderBy(a => a.ActorID).ToList();
			AssignVisibleCrates(bot, visibleCrates, collectors);

			collectors.RemoveAll(a => assignments.ContainsKey(a.ActorID));
			if (emergency)
				AssignScouts(bot, collectors);

			if (emergency && !world.Actors.Any(IsPotentialCollector))
				TryEmergencySale(bot);

			Debug("scan emergency={0} visible-crates={1} assignments={2} collectors={3}",
				emergency, visibleCrates.Count, assignments.Count, collectors.Count);
			initialScanPending = false;
		}

		void BuildRegions()
		{
			var width = (world.Map.MapSize.X + Info.CoarseCellSize - 1) / Info.CoarseCellSize;
			var byIndex = new Dictionary<int, Region>();
			foreach (var cell in world.Map.AllCells.Where(c => c.Layer == 0))
			{
				var x = Math.Clamp(cell.X / Info.CoarseCellSize, 0, width - 1);
				var y = Math.Max(0, cell.Y / Info.CoarseCellSize);
				var index = y * width + x;
				if (!byIndex.TryGetValue(index, out var region))
					byIndex.Add(index, region = new Region(index));

				region.Cells.Add(cell);
			}

			regions.AddRange(byIndex.Values.OrderBy(r => r.Index));
			for (var i = 0; i < regions.Count; i++)
			{
				var center = RegionCenter(regions[i]);
				regions[i].Cells.Sort((a, b) =>
				{
					var distance = (world.Map.CenterOfCell(a) - world.Map.CenterOfCell(center)).LengthSquared
						.CompareTo((world.Map.CenterOfCell(b) - world.Map.CenterOfCell(center)).LengthSquared);
					if (distance != 0)
						return distance;

					return a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
				});
			}

			lastVisibleTicks = Enumerable.Repeat(-1, regions.Count).ToArray();
		}

		CPos RegionCenter(Region region)
		{
			var x = (int)region.Cells.Average(c => c.X);
			var y = (int)region.Cells.Average(c => c.Y);
			return world.Map.Clamp(new CPos(x, y));
		}

		void RefreshRegionVisibility()
		{
			for (var i = 0; i < regions.Count; i++)
				if (regions[i].Cells.Any(player.Shroud.IsVisible))
					lastVisibleTicks[i] = world.WorldTick;
		}

		bool IsEmergency()
		{
			var spendable = resources != null ? resources.Cash + resources.Resources : 0;
			var hasMcv = world.Actors.Any(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
				Info.McvTypes.Contains(a.Info.Name));
			return CrateExplorationPolicy.IsEmergency(spendable, hasMcv, Info.EmergencyCashThreshold);
		}

		HashSet<uint> ReviewAssignments(bool emergency)
		{
			var temporarilyBlocked = new HashSet<uint>();
			foreach (var unitId in assignments.Keys.OrderBy(id => id).ToList())
			{
				var assignment = assignments[unitId];
				var unit = world.GetActorById(unitId);
				var releaseReason = string.Empty;
				if (!IsSuitableCollector(unit))
					releaseReason = "collector unavailable";
				else if (assignment.Kind == AssignmentKind.Crate)
				{
					var crate = world.GetActorById(assignment.TargetActorId);
					if (!IsVisibleCrate(crate))
						releaseReason = "crate gone or hidden";
				}
				else if (!emergency)
					releaseReason = "emergency ended";
				else if (assignment.RegionIndex < 0 || assignment.RegionIndex >= regions.Count ||
					lastVisibleTicks[assignment.RegionIndex] == world.WorldTick)
					releaseReason = "region reached";

				if (string.IsNullOrEmpty(releaseReason) && unit != null)
				{
					var distance = (unit.CenterPosition - world.Map.CenterOfCell(assignment.Destination)).LengthSquared;
					if (CrateExplorationPolicy.MadeProgress(distance, assignment.BestDistanceSquared))
					{
						assignment.BestDistanceSquared = distance;
						assignment.LastProgressTick = world.WorldTick;
					}
					else if (CrateExplorationPolicy.HasStalled(world.WorldTick, assignment.LastProgressTick,
						Info.AssignmentStallInterval))
					{
						releaseReason = "stalled";
						temporarilyBlocked.Add(unitId);
					}
				}

				if (!string.IsNullOrEmpty(releaseReason))
					ReleaseAssignment(unitId, releaseReason);
			}

			return temporarilyBlocked;
		}

		void ReleaseScoutAssignments(string reason)
		{
			foreach (var unitId in assignments.Where(kv => kv.Value.Kind == AssignmentKind.Scout)
				.Select(kv => kv.Key).OrderBy(id => id).ToList())
				ReleaseAssignment(unitId, reason);
		}

		void ReleaseAssignment(uint unitId, string reason)
		{
			if (!assignments.TryGetValue(unitId, out var assignment))
				return;

			assignments.Remove(unitId);
			Debug("released {0}#{1} kind={2} target={3} region={4}: {5}",
				world.GetActorById(unitId)?.Info.Name ?? "missing", unitId, assignment.Kind,
				assignment.TargetActorId, assignment.RegionIndex, reason);
		}

		List<Actor> VisibleCrates()
		{
			return world.Actors.Where(IsVisibleCrate).OrderBy(a => a.ActorID).ToList();
		}

		bool IsVisibleCrate(Actor actor)
		{
			return actor != null && actor.IsInWorld && !actor.IsDead && actor.Info.HasTraitInfo<CrateInfo>() &&
				player.Shroud.IsVisible(actor.Location) && actor.CanBeViewedByPlayer(player);
		}

		List<Actor> SuitableCollectors()
		{
			return world.Actors.Where(IsSuitableCollector).OrderBy(a => a.ActorID).ToList();
		}

		bool IsSuitableCollector(Actor actor)
		{
			if (!IsPotentialCollector(actor))
				return false;

			var health = actor.TraitOrDefault<IHealth>();
			if (health != null && 100L * health.HP < Info.MinimumCollectorHealthPercent * (long)health.MaxHP)
				return false;

			var passenger = actor.TraitOrDefault<Passenger>();
			if (passenger?.Transport != null || passenger?.ReservedCargo != null)
				return false;

			if (transportReservations != null && transportReservations.Any(r => r.IsTransportReserved(actor)))
				return false;

			if (otherUnitReservations != null && otherUnitReservations.Any(r => r.IsUnitReserved(actor)))
				return false;

			return true;
		}

		bool IsPotentialCollector(Actor actor)
		{
			if (actor == null || actor.Owner != player || !actor.IsInWorld || actor.IsDead ||
				Info.ExcludedCollectorTypes.Contains(actor.Info.Name))
				return false;

			var mobile = actor.TraitOrDefault<Mobile>();
			if (mobile != null && mobile.Locomotor.Info.Crushes.Contains(Info.CrateCrushClass))
				return true;

			var aircraft = actor.TraitOrDefault<Aircraft>();
			return aircraft != null && aircraft.Info.CanForceLand &&
				aircraft.Info.Crushes.Contains(Info.CrateCrushClass);
		}

		void AssignVisibleCrates(IBot bot, List<Actor> crates, List<Actor> collectors)
		{
			var assignedCrates = assignments.Values.Where(a => a.Kind == AssignmentKind.Crate)
				.Select(a => a.TargetActorId).ToHashSet();
			foreach (var crate in crates.Where(c => !assignedCrates.Contains(c.ActorID)))
			{
				Actor selected = null;
				List<CPos> airRoute = null;
				foreach (var collector in collectors.OrderBy(a =>
					(a.CenterPosition - crate.CenterPosition).LengthSquared).ThenBy(a => a.ActorID))
				{
					if (TryPlanCrateCollection(collector, crate, out airRoute, out var rejection))
					{
						selected = collector;
						break;
					}

					Debug("rejected {0}#{1} -> visible crate {2}#{3} at {4}: {5}", collector.Info.Name,
						collector.ActorID, crate.Info.Name, crate.ActorID, crate.Location, rejection);
				}

				if (selected == null)
					continue;

				if (selected.TraitOrDefault<Aircraft>() != null)
					QueueAirRoute(bot, selected, airRoute, crate.Location, true);
				else
					bot.QueueOrder(new Order("Move", selected, Target.FromCell(world, crate.Location), false));

				assignments[selected.ActorID] = NewAssignment(AssignmentKind.Crate, selected,
					crate.Location, crate.ActorID, -1);
				collectors.Remove(selected);
				assignedCrates.Add(crate.ActorID);
				Debug("assigned {0}#{1} -> visible crate {2}#{3} at {4} mode={5}", selected.Info.Name,
					selected.ActorID, crate.Info.Name, crate.ActorID, crate.Location,
					selected.TraitOrDefault<Aircraft>() != null ? "safe-land" : "ground");
			}
		}

		bool TryPlanCrateCollection(Actor collector, Actor crate, out List<CPos> airRoute, out string rejection)
		{
			airRoute = null;
			rejection = string.Empty;
			var mobile = collector.TraitOrDefault<Mobile>();
			if (mobile != null)
			{
				if (mobile.Locomotor.MovementCostForCell(crate.Location) == PathGraph.MovementCostForUnreachableCell)
				{
					rejection = "unwalkable crate cell";
					return false;
				}

				if (!domainIndex.IsPassable(collector.Location, crate.Location, mobile.Locomotor))
				{
					rejection = "different movement domain";
					return false;
				}

				return true;
			}

			var aircraft = collector.TraitOrDefault<Aircraft>();
			if (aircraft == null)
			{
				rejection = "not mobile or aircraft";
				return false;
			}

			if (squadManager == null)
			{
				rejection = "no air threat manager";
				return false;
			}

			if (!aircraft.CanLand(crate.Location, blockedByMobile: false))
			{
				rejection = "crate cell cannot be landed on";
				return false;
			}

			var destinationThreat = AirStateBase.SafeIndependentAirThreatAt(squadManager, crate.Location);
			if (destinationThreat > 0f)
			{
				rejection = $"stopping AA threat {destinationThreat}";
				return false;
			}

			airRoute = AirStateBase.SafeIndependentAirRoute(squadManager, collector, crate.Location);
			if (airRoute == null)
			{
				rejection = "no bounded air route";
				return false;
			}

			return true;
		}

		void AssignScouts(IBot bot, List<Actor> collectors)
		{
			var assignedRegions = assignments.Values.Where(a => a.Kind == AssignmentKind.Scout && a.RegionIndex >= 0)
				.Select(a => a.RegionIndex).ToHashSet();
			foreach (var collector in collectors.OrderBy(a => a.ActorID))
			{
				var ranked = CrateExplorationPolicy.RankRegions(lastVisibleTicks, assignedRegions)
					.Where(i => lastVisibleTicks[i] != world.WorldTick).Take(Info.MaximumRegionCandidates);
				var selectedRegion = -1;
				var destination = default(CPos);
				List<CPos> airRoute = null;
				foreach (var regionIndex in ranked)
					if (TryFindScoutDestination(collector, regionIndex, out destination, out airRoute))
					{
						selectedRegion = regionIndex;
						break;
					}

				if (selectedRegion < 0)
					continue;

				if (collector.TraitOrDefault<Aircraft>() != null)
					QueueAirRoute(bot, collector, airRoute, destination, false);
				else
					bot.QueueOrder(new Order("Move", collector, Target.FromCell(world, destination), false));

				assignments[collector.ActorID] = NewAssignment(AssignmentKind.Scout, collector,
					destination, 0, selectedRegion);
				assignedRegions.Add(selectedRegion);
				Debug("assigned {0}#{1} -> scout region={2} cell={3}", collector.Info.Name,
					collector.ActorID, selectedRegion, destination);
			}
		}

		bool TryFindScoutDestination(Actor collector, int regionIndex, out CPos destination, out List<CPos> airRoute)
		{
			destination = default(CPos);
			airRoute = null;
			var region = regions[regionIndex];
			var mobile = collector.TraitOrDefault<Mobile>();
			if (mobile != null)
			{
				foreach (var cell in region.Cells)
					if (mobile.Locomotor.MovementCostForCell(cell) != PathGraph.MovementCostForUnreachableCell &&
						domainIndex.IsPassable(collector.Location, cell, mobile.Locomotor))
					{
						destination = cell;
						return true;
					}

				return false;
			}

			if (collector.TraitOrDefault<Aircraft>() == null || squadManager == null)
				return false;

			foreach (var cell in region.Cells)
			{
				if (AirStateBase.SafeIndependentAirThreatAt(squadManager, cell) > 0f)
					continue;

				var route = AirStateBase.SafeIndependentAirRoute(squadManager, collector, cell);
				if (route == null)
					continue;

				destination = cell;
				airRoute = route;
				return true;
			}

			return false;
		}

		Assignment NewAssignment(AssignmentKind kind, Actor unit, CPos destination, uint targetActorId, int regionIndex)
		{
			return new Assignment
			{
				Kind = kind,
				TargetActorId = targetActorId,
				RegionIndex = regionIndex,
				Destination = destination,
				BestDistanceSquared = (unit.CenterPosition - world.Map.CenterOfCell(destination)).LengthSquared,
				LastProgressTick = world.WorldTick
			};
		}

		void QueueAirRoute(IBot bot, Actor aircraft, List<CPos> route, CPos destination, bool land)
		{
			var queued = false;
			foreach (var waypoint in route ?? Enumerable.Empty<CPos>())
			{
				bot.QueueOrder(new Order("Move", aircraft, Target.FromCell(world, waypoint), queued));
				queued = true;
			}

			bot.QueueOrder(new Order(land ? "Land" : "Move", aircraft,
				Target.FromCell(world, destination), queued));
		}

		void TryEmergencySale(IBot bot)
		{
			if (world.WorldTick < nextEmergencySaleTick || Info.EmergencySellActorTypes.Count == 0)
				return;

			var buildings = world.Actors.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
				a.Info.HasTraitInfo<BuildingInfo>()).OrderBy(a => a.ActorID).ToList();
			if (buildings.Count <= 1)
				return;

			var building = buildings.Where(a => Info.EmergencySellActorTypes.Contains(a.Info.Name))
				.Where(a => a.TraitOrDefault<Sellable>()?.IsTraitDisabled == false)
				.Where(CanSpawnSuitableScout)
				.OrderBy(a => a.GetSellValue()).ThenBy(a => a.ActorID).FirstOrDefault();
			if (building == null)
				return;

			bot.QueueOrder(new Order("Sell", building, false));
			nextEmergencySaleTick = world.WorldTick + Info.EmergencySellInterval;

			// Re-scan before ordinary squads can claim the newly spawned sale survivors.
			scanTicks = 1;
			Debug("sold nonessential {0}#{1} for emergency scout recovery", building.Info.Name, building.ActorID);
		}

		bool CanSpawnSuitableScout(Actor building)
		{
			return building.Info.TraitInfos<SpawnActorsOnSellInfo>().Any(s => s.ActorTypes.Any(type =>
				world.Map.Rules.Actors.TryGetValue(type, out var info) && IsSuitableGroundCollectorInfo(info)));
		}

		bool IsSuitableGroundCollectorInfo(ActorInfo actorInfo)
		{
			if (Info.ExcludedCollectorTypes.Contains(actorInfo.Name))
				return false;

			var mobile = actorInfo.TraitInfoOrDefault<MobileInfo>();
			return mobile != null && mobile.LocomotorInfo.Crushes.Contains(Info.CrateCrushClass);
		}

		void Debug(string format, params object[] args)
		{
			if (!Info.DebugLogging)
				return;

			var message = string.Format(format, args);
			AIUtils.BotDebug("AI ({0}) crate exploration: {1}", player.ClientIndex, message);
			Log.Write("debug", "AI crate exploration: {0} (client {1}) at tick {2}: {3}",
				player, player.ClientIndex, world.WorldTick, message);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("CrateScanTicks", FieldSaver.FormatValue(scanTicks)),
				new MiniYamlNode("CrateInitialScanPending", FieldSaver.FormatValue(initialScanPending)),
				new MiniYamlNode("CrateNextEmergencySaleTick", FieldSaver.FormatValue(nextEmergencySaleTick)),
				new MiniYamlNode("CrateRegionLastVisibleTicks", FieldSaver.FormatValue(lastVisibleTicks)),
				new MiniYamlNode("CrateAssignments", "", assignments.OrderBy(kv => kv.Key).Select(kv =>
					new MiniYamlNode("Assignment", "", new List<MiniYamlNode>
					{
						new MiniYamlNode("Unit", FieldSaver.FormatValue(kv.Key)),
						new MiniYamlNode("Kind", FieldSaver.FormatValue((int)kv.Value.Kind)),
						new MiniYamlNode("Target", FieldSaver.FormatValue(kv.Value.TargetActorId)),
						new MiniYamlNode("Region", FieldSaver.FormatValue(kv.Value.RegionIndex)),
						new MiniYamlNode("Destination", FieldSaver.FormatValue(kv.Value.Destination)),
						new MiniYamlNode("BestDistance", FieldSaver.FormatValue(kv.Value.BestDistanceSquared)),
						new MiniYamlNode("LastProgress", FieldSaver.FormatValue(kv.Value.LastProgressTick))
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
					case "CrateScanTicks":
						scanTicks = FieldLoader.GetValue<int>(node.Key, node.Value.Value);
						break;
					case "CrateInitialScanPending":
						initialScanPending = FieldLoader.GetValue<bool>(node.Key, node.Value.Value);
						break;
					case "CrateNextEmergencySaleTick":
						nextEmergencySaleTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value);
						break;
					case "CrateRegionLastVisibleTicks":
						var loaded = FieldLoader.GetValue<int[]>(node.Key, node.Value.Value);
						if (loaded.Length == lastVisibleTicks.Length)
							lastVisibleTicks = loaded;
						break;
					case "CrateAssignments":
						assignments.Clear();
						foreach (var assignmentNode in node.Value.Nodes)
							LoadAssignment(assignmentNode);
						break;
				}
		}

		void LoadAssignment(MiniYamlNode node)
		{
			T Load<T>(string key, T fallback = default(T))
			{
				var value = node.Value.Nodes.FirstOrDefault(n => n.Key == key);
				return value == null ? fallback : FieldLoader.GetValue<T>(key, value.Value.Value);
			}

			var unitId = Load<uint>("Unit");
			if (unitId == 0)
				return;

			assignments[unitId] = new Assignment
			{
				Kind = (AssignmentKind)Load<int>("Kind"),
				TargetActorId = Load<uint>("Target"),
				RegionIndex = Load("Region", -1),
				Destination = Load<CPos>("Destination"),
				BestDistanceSquared = Load<long>("BestDistance"),
				LastProgressTick = Load<int>("LastProgress")
			};
		}
	}
}
