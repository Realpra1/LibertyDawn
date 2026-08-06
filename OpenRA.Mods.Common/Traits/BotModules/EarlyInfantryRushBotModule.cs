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
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Coordinates VIKI's bounded early chemical harassment and two spaced grenadier rushes.")]
	public class EarlyInfantryRushBotModuleInfo : ConditionalTraitInfo
	{
		public readonly string[] RequiredPrerequisites = Array.Empty<string>();
		public readonly string GrenadierType = null;
		public readonly string ChemicalType = null;
		public readonly Dictionary<string, int> ChemicalTargetPriorities = new Dictionary<string, int>();
		public readonly Dictionary<string, int> GrenadierTargetPriorities = new Dictionary<string, int>();
		public readonly HashSet<string> ConstructionAssetTypes = new HashSet<string>();
		public readonly HashSet<string> RefineryAssetTypes = new HashSet<string>();
		public readonly HashSet<string> HarvesterAssetTypes = new HashSet<string>();
		public readonly int GrenadierGroupSize = 10;
		public readonly int MaximumGrenadierGroups = 2;
		public readonly int MaximumChemicalWarriors = 4;
		public readonly int EarlyGameEndTick = 9000;
		public readonly int RequestInterval = 50;
		public readonly int OrderInterval = 25;
		public readonly int InitialReservationTicks = 5;
		public readonly int MaximumTargetCandidates = 48;
		public readonly int ChemicalApproachRadiusCells = 3;
		public readonly int FormationRadiusCells = 5;
		public readonly int FormationSpacingCells = 2;
		public readonly int FormationToleranceCells = 1;
		public readonly int EnemyAvoidanceRadiusCells = 6;
		public readonly int ReformDistanceCells = 5;
		public readonly int PostKillHoldTicks = 75;
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (RequiredPrerequisites.Length == 0 || string.IsNullOrEmpty(GrenadierType) ||
				string.IsNullOrEmpty(ChemicalType) || ChemicalTargetPriorities.Count == 0 ||
				GrenadierTargetPriorities.Count == 0 || ConstructionAssetTypes.Count == 0 ||
				RefineryAssetTypes.Count == 0 || HarvesterAssetTypes.Count == 0 || GrenadierGroupSize <= 0 ||
				MaximumGrenadierGroups <= 0 || MaximumChemicalWarriors <= 0 || EarlyGameEndTick <= 0 ||
				RequestInterval <= 0 || OrderInterval <= 0 || InitialReservationTicks <= 0 ||
				MaximumTargetCandidates <= 0 || ChemicalApproachRadiusCells <= 0 ||
				FormationRadiusCells <= 1 || FormationSpacingCells <= 0 || FormationToleranceCells < 0 ||
				EnemyAvoidanceRadiusCells <= 0 || ReformDistanceCells <= 0 || PostKillHoldTicks <= 0 ||
				ChemicalTargetPriorities.Any(p => p.Value <= 0) || GrenadierTargetPriorities.Any(p => p.Value <= 0))
				throw new YamlException("Early infantry rush types, targets, asset groups, bounds, distances, and intervals must be configured and valid.");

			foreach (var actorType in new[] { GrenadierType, ChemicalType }
				.Concat(ChemicalTargetPriorities.Keys).Concat(GrenadierTargetPriorities.Keys)
				.Concat(ConstructionAssetTypes).Concat(RefineryAssetTypes).Concat(HarvesterAssetTypes))
				if (!rules.Actors.ContainsKey(actorType))
					throw new YamlException($"Early infantry rush actor '{actorType}' does not exist.");
		}

		public override object Create(ActorInitializer init) { return new EarlyInfantryRushBotModule(init.Self, this); }
	}

	public class EarlyInfantryRushBotModule : ConditionalTrait<EarlyInfantryRushBotModuleInfo>,
		IBotEnabled, IBotTick, IBotUnitReservations, IGameSaveTraitData
	{
		sealed class GrenadierGroup
		{
			public readonly HashSet<uint> Units = new HashSet<uint>();
			public int Index;
			public Actor Target;
			public int HoldUntilTick;
			public int LastOrderTick;
			public string LastMode;
		}

		sealed class ChemicalMission
		{
			public uint UnitId;
			public Actor Target;
			public int LastOrderTick;
		}

		static readonly BitSet<TargetableType> GroundTargetTypes = new BitSet<TargetableType>("Ground");

		readonly World world;
		readonly Player player;
		readonly HashSet<uint> reserved = new HashSet<uint>();
		readonly HashSet<uint> pendingGrenadiers = new HashSet<uint>();
		readonly HashSet<uint> launchedChemicals = new HashSet<uint>();
		readonly Dictionary<uint, string> chemicalWaitSignatures = new Dictionary<uint, string>();
		readonly List<GrenadierGroup> groups = new List<GrenadierGroup>();
		readonly Dictionary<uint, ChemicalMission> chemicalMissions = new Dictionary<uint, ChemicalMission>();
		IBot bot;
		IBotUnitReservations[] otherReservations;
		CrateCollectorBotModule[] crateCollectors;
		IBotTransportReservations[] transportReservations;
		IBotRequestUnitProduction[] productionRequesters;
		SquadManagerBotModule squadManager;
		DomainIndex domainIndex;
		TechTree techTree;
		int launchedGroups;
		int nextRequestTick;
		bool ownsGrenadierRequest;
		bool ownsChemicalRequest;
		bool initialReservationPending;
		bool ended;

		public EarlyInfantryRushBotModule(Actor self, EarlyInfantryRushBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			techTree = player.PlayerActor.Trait<TechTree>();
			domainIndex = world.WorldActor.Trait<DomainIndex>();
			crateCollectors = player.PlayerActor.TraitsImplementing<CrateCollectorBotModule>().ToArray();
			otherReservations = player.PlayerActor.TraitsImplementing<IBotUnitReservations>()
				.Where(r => !ReferenceEquals(r, this) && !(r is CrateCollectorBotModule)).ToArray();
			transportReservations = player.PlayerActor.TraitsImplementing<IBotTransportReservations>().ToArray();
			productionRequesters = player.PlayerActor.TraitsImplementing<IBotRequestUnitProduction>().ToArray();
			RefreshSquadManager();
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self)
		{
			ended = false;
			initialReservationPending = true;
		}

		protected override void TraitDisabled(Actor self)
		{
			CancelOwnedRequests();
			ClearState("bot condition disabled");
		}

		void IBotEnabled.BotEnabled(IBot enabledBot) { bot = enabledBot; }

		bool IBotUnitReservations.IsUnitReserved(Actor actor)
		{
			return actor != null && (reserved.Contains(actor.ActorID) ||
				(initialReservationPending && IsPotentialEarlyInfantry(actor)));
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (IsTraitDisabled || player.WinState != WinState.Undefined)
				return;

			RefreshRequestOwnership();
			if (world.WorldTick >= Info.EarlyGameEndTick)
			{
				if (!ended)
				{
					ended = true;
					CancelOwnedRequests();
					ClearState("early window ended");
				}

				return;
			}

			if (!techTree.HasPrerequisites(Info.RequiredPrerequisites))
			{
				CancelOwnedRequests();
				ClearState("covert capability unavailable");
				return;
			}

			RefreshSquadManager();
			ReviewAssignments();
			ClaimGrenadiers();
			ClaimChemicalWarriors();
			LaunchGrenadierGroups();
			RebuildReservations();
			UpdateChemicalOrders();
			UpdateGrenadierOrders();
			RequestNeededInfantry();
			if (world.WorldTick >= Info.InitialReservationTicks)
				initialReservationPending = false;
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

		bool IsPotentialEarlyInfantry(Actor actor)
		{
			return IsOwnedUsable(actor) && world.WorldTick < Info.EarlyGameEndTick &&
				(techTree?.HasPrerequisites(Info.RequiredPrerequisites) ?? false) &&
				(actor.Info.Name == Info.GrenadierType || actor.Info.Name == Info.ChemicalType) &&
				!HasCargoContention(actor) && !IsResupplying(actor);
		}

		bool HasCargoContention(Actor actor)
		{
			var passenger = actor.TraitOrDefault<Passenger>();
			return passenger?.Transport != null || passenger?.ReservedCargo != null ||
				(transportReservations?.Any(r => r.IsTransportReserved(actor)) ?? false);
		}

		static bool IsResupplying(Actor actor)
		{
			var activity = actor.CurrentActivity;
			return activity is Resupply || activity?.NextActivity is Resupply;
		}

		bool IsClaimable(Actor actor)
		{
			return IsOwnedUsable(actor) && !HasCargoContention(actor) && !IsResupplying(actor) &&
				!(crateCollectors?.Any(c => c.HasAssignment(actor)) ?? false) &&
				!(otherReservations?.Any(r => r.IsUnitReserved(actor)) ?? false) &&
				!(squadManager?.IsUnitProtectingBase(actor) ?? false);
		}

		void ReviewAssignments()
		{
			foreach (var id in pendingGrenadiers.ToList())
				if (!IsClaimable(world.GetActorById(id)))
					pendingGrenadiers.Remove(id);

			foreach (var group in groups.ToList())
			{
				foreach (var id in group.Units.ToList())
					if (!IsClaimable(world.GetActorById(id)))
						group.Units.Remove(id);

				if (group.Units.Count == 0)
				{
					Debug("grenadier group released: no usable members");
					groups.Remove(group);
				}
			}

			foreach (var mission in chemicalMissions.Values.ToList())
			{
				var unit = world.GetActorById(mission.UnitId);
				if (!IsClaimable(unit) || !IsValidChemicalTarget(unit, mission.Target))
				{
					Debug("chemical {0} mission complete/released: target={1}", mission.UnitId, TargetStatus(mission.Target));
					chemicalMissions.Remove(mission.UnitId);
				}
			}
		}

		void ClaimGrenadiers()
		{
			var needed = Math.Max(0, (Info.MaximumGrenadierGroups - launchedGroups) * Info.GrenadierGroupSize -
				pendingGrenadiers.Count);
			if (needed == 0)
				return;

			var grouped = groups.SelectMany(g => g.Units).ToHashSet();
			foreach (var actor in world.Actors.Where(a => a.Info.Name == Info.GrenadierType &&
				!pendingGrenadiers.Contains(a.ActorID) && !grouped.Contains(a.ActorID) && IsClaimable(a))
				.OrderBy(a => a.ActorID).Take(needed))
				pendingGrenadiers.Add(actor.ActorID);
		}

		void ClaimChemicalWarriors()
		{
			if (launchedChemicals.Count >= Info.MaximumChemicalWarriors)
				return;

			var assignedTargets = chemicalMissions.Values.Where(m => IsEnemyTarget(m.Target))
				.Select(m => m.Target.ActorID).ToHashSet();
			foreach (var actor in world.Actors.Where(a => a.Info.Name == Info.ChemicalType &&
				!launchedChemicals.Contains(a.ActorID) && IsClaimable(a)).OrderBy(a => a.ActorID))
			{
				if (launchedChemicals.Count >= Info.MaximumChemicalWarriors)
					break;

				var target = SelectChemicalTarget(actor, assignedTargets);
				if (target == null)
				{
					LogChemicalWait(actor, assignedTargets);
					continue;
				}

				chemicalWaitSignatures.Remove(actor.ActorID);
				launchedChemicals.Add(actor.ActorID);
				assignedTargets.Add(target.ActorID);
				chemicalMissions.Add(actor.ActorID, new ChemicalMission
				{
					UnitId = actor.ActorID,
					Target = target,
					LastOrderTick = int.MinValue
				});
				Debug("chemical {0} assigned independently to {1}", actor.ActorID, TargetStatus(target));
			}
		}

		void LaunchGrenadierGroups()
		{
			while (EarlyInfantryRushPolicy.CanLaunchGroup(pendingGrenadiers.Count, Info.GrenadierGroupSize,
				launchedGroups, Info.MaximumGrenadierGroups))
			{
				var members = pendingGrenadiers.Select(world.GetActorById).Where(IsClaimable)
					.OrderBy(a => a.ActorID).Take(Info.GrenadierGroupSize).ToList();
				if (members.Count != Info.GrenadierGroupSize)
					return;

				var excluded = groups.Where(g => IsEnemyTarget(g.Target)).Select(g => g.Target.ActorID).ToHashSet();
				var target = SelectGrenadierTarget(members, excluded);
				if (target == null)
					return;

				var group = new GrenadierGroup
				{
					Index = launchedGroups + 1,
					Target = target,
					LastOrderTick = int.MinValue
				};
				group.Units.UnionWith(members.Select(a => a.ActorID));
				foreach (var id in group.Units)
					pendingGrenadiers.Remove(id);

				groups.Add(group);
				launchedGroups++;
				Debug("launched grenadier group {0}/{1}: members={2} target={3}", group.Index,
					Info.MaximumGrenadierGroups, string.Join(",", group.Units.OrderBy(id => id)), TargetStatus(target));
			}
		}

		void RebuildReservations()
		{
			reserved.Clear();
			reserved.UnionWith(pendingGrenadiers);
			reserved.UnionWith(groups.SelectMany(g => g.Units));
			reserved.UnionWith(chemicalMissions.Keys);
		}

		void UpdateChemicalOrders()
		{
			foreach (var mission in chemicalMissions.Values.OrderBy(m => m.UnitId))
			{
				if (world.WorldTick < mission.LastOrderTick + Info.OrderInterval)
					continue;

				var unit = world.GetActorById(mission.UnitId);
				if (!IsClaimable(unit) || !IsValidChemicalTarget(unit, mission.Target))
					continue;

				mission.LastOrderTick = world.WorldTick;
				bot.QueueOrder(new Order("Attack", unit, Target.FromActor(mission.Target), false));
			}
		}

		void UpdateGrenadierOrders()
		{
			var claimedTargets = new HashSet<uint>();
			foreach (var group in groups.ToList())
			{
				var units = group.Units.Select(world.GetActorById).Where(IsClaimable)
					.OrderBy(a => a.ActorID).ToList();
				if (units.Count == 0)
					continue;

				if (group.Target != null && (!group.Target.IsInWorld || group.Target.IsDead))
				{
					Debug("grenadier group {0} target destroyed {1}; holding until {2}", group.Index, TargetStatus(group.Target),
						world.WorldTick + Info.PostKillHoldTicks);
					group.Target = null;
					group.HoldUntilTick = world.WorldTick + Info.PostKillHoldTicks;
					group.LastOrderTick = int.MinValue;
					StopGroup(group, units, "post-kill-hold");
					continue;
				}

				if (EarlyInfantryRushPolicy.IsHolding(world.WorldTick, group.HoldUntilTick))
				{
					if (world.WorldTick >= group.LastOrderTick + Info.OrderInterval)
						StopGroup(group, units, "post-kill-hold");
					continue;
				}

				if (!IsValidGrenadierTarget(units, group.Target))
					group.Target = SelectGrenadierTarget(units, claimedTargets);

				if (group.Target == null)
				{
					Debug("grenadier group {0} mission complete: no visible reachable base target", group.Index);
					groups.Remove(group);
					continue;
				}

				claimedTargets.Add(group.Target.ActorID);
				var threat = NearestEnemyUnit(units);
				if (threat != null)
				{
					if (world.WorldTick >= group.LastOrderTick + Info.OrderInterval || group.LastMode != "reform")
						ReformAway(group, units, threat);
					continue;
				}

				if (world.WorldTick < group.LastOrderTick + Info.OrderInterval && group.LastMode == "attack")
					continue;

				var destinations = TargetFormation(units, group.Target);
				if (destinations.Count != units.Count)
				{
					StopGroup(group, units, "no-spaced-formation");
					continue;
				}

				var toleranceSquared = Info.FormationToleranceCells * Info.FormationToleranceCells;
				var positioned = units.Select((unit, i) => (unit.Location - destinations[i]).LengthSquared <= toleranceSquared).All(v => v);
				group.LastOrderTick = world.WorldTick;
				if (!positioned)
				{
					for (var i = 0; i < units.Count; i++)
						bot.QueueOrder(new Order("Move", units[i], Target.FromCell(world, destinations[i]), false));
					LogGroupMode(group, "forming", "target={0} cells={1}", TargetStatus(group.Target),
						string.Join(";", destinations));
					continue;
				}

				foreach (var unit in units)
					bot.QueueOrder(new Order("Attack", unit, Target.FromActor(group.Target), false));
				LogGroupMode(group, "attack", "target={0} members={1}", TargetStatus(group.Target), units.Count);
			}
		}

		void StopGroup(GrenadierGroup group, List<Actor> units, string mode)
		{
			group.LastOrderTick = world.WorldTick;
			foreach (var unit in units)
				bot.QueueOrder(new Order("Stop", unit, false));
			LogGroupMode(group, mode, "members={0}", units.Count);
		}

		void ReformAway(GrenadierGroup group, List<Actor> units, Actor threat)
		{
			var center = units.Select(a => a.CenterPosition).Average();
			var away = center - threat.CenterPosition;
			if (away == WVec.Zero)
				away = new WVec(1024, 0, 0);

			var intended = world.Map.Clamp(world.Map.CellContaining(threat.CenterPosition +
				AirThreatGeometry.ScaleToLength(away, Info.ReformDistanceCells * 1024)));
			var destinations = LocalFormation(units, intended);
			if (destinations.Count != units.Count)
			{
				StopGroup(group, units, "blocked-reform");
				return;
			}

			group.LastOrderTick = world.WorldTick;
			for (var i = 0; i < units.Count; i++)
				bot.QueueOrder(new Order("Move", units[i], Target.FromCell(world, destinations[i]), false));
			LogGroupMode(group, "reform", "threat={0} center={1}", TargetStatus(threat), intended);
		}

		void LogGroupMode(GrenadierGroup group, string mode, string format, params object[] args)
		{
			if (group.LastMode == mode)
				return;

			group.LastMode = mode;
			Debug("grenadier group {0} {1}: {2}", group.Index, mode, string.Format(format, args));
		}

		List<CPos> TargetFormation(List<Actor> units, Actor target)
		{
			var center = world.Map.CellContaining(units.Select(a => a.CenterPosition).Average());
			var minimumRadius = Math.Max(1, Info.FormationRadiusCells - 1);
			var minimumSquared = minimumRadius * minimumRadius;
			var candidates = world.Map.FindTilesInCircle(target.Location, Info.FormationRadiusCells)
				.Where(c => (c - target.Location).LengthSquared >= minimumSquared && CanEnter(units[0], c))
				.OrderBy(c => (c - center).LengthSquared).ThenBy(c => c.X).ThenBy(c => c.Y);
			return EarlyInfantryRushPolicy.SelectSpacedCells(candidates, units.Count, Info.FormationSpacingCells);
		}

		List<CPos> LocalFormation(List<Actor> units, CPos center)
		{
			var candidates = world.Map.FindTilesInCircle(center, Info.FormationRadiusCells)
				.Where(c => CanEnter(units[0], c))
				.OrderBy(c => (c - center).LengthSquared).ThenBy(c => c.X).ThenBy(c => c.Y);
			return EarlyInfantryRushPolicy.SelectSpacedCells(candidates, units.Count, Info.FormationSpacingCells);
		}

		bool CanEnter(Actor actor, CPos cell)
		{
			var mobile = actor.TraitOrDefault<Mobile>();
			return mobile != null && world.Map.Contains(cell) &&
				mobile.CanEnterCell(cell, check: BlockedByActor.Immovable) &&
				domainIndex.IsPassable(actor.Location, cell, mobile.Locomotor);
		}

		Actor NearestEnemyUnit(List<Actor> units)
		{
			var center = units.Select(a => a.CenterPosition).Average();
			return world.FindActorsInCircle(center, WDist.FromCells(Info.EnemyAvoidanceRadiusCells))
				.Where(a => IsEnemyTarget(a) && IsVisible(a) && a.Info.HasTraitInfo<MobileInfo>() &&
					a.TraitsImplementing<Armament>().Any(arm => !arm.IsTraitDisabled &&
						arm.Weapon.IsValidTarget(GroundTargetTypes)))
				.OrderBy(a => (a.CenterPosition - center).HorizontalLengthSquared).ThenBy(a => a.ActorID)
				.FirstOrDefault();
		}

		Actor SelectChemicalTarget(Actor unit, HashSet<uint> excluded)
		{
			var candidates = ChemicalTargets(unit).Where(a => !excluded.Contains(a.ActorID)).ToList();
			return candidates.Count > 0 ? candidates[0] : null;
		}

		void LogChemicalWait(Actor unit, HashSet<uint> excluded)
		{
			if (!Info.DebugLogging)
				return;

			var configured = world.Actors.Where(a => IsEnemyTarget(a) &&
				Info.ChemicalTargetPriorities.ContainsKey(a.Info.Name)).ToList();
			var visible = configured.Where(IsVisible).ToList();
			var attackable = visible.Where(a => StateBase.CanAttackTarget(unit, a)).ToList();
			var reachable = attackable.Where(a => HasReachableChemicalApproach(unit, a)).ToList();
			var signature = $"{configured.Count}/{visible.Count}/{attackable.Count}/{reachable.Count}/" +
				$"{reachable.Count(a => !excluded.Contains(a.ActorID))}:" +
				string.Join(",", reachable.OrderBy(a => a.ActorID).Select(a => a.ActorID));
			if (chemicalWaitSignatures.TryGetValue(unit.ActorID, out var previous) && previous == signature)
				return;

			chemicalWaitSignatures[unit.ActorID] = signature;
			Debug("chemical {0} waiting: configured={1} visible={2} attackable={3} reachable={4} unreserved={5} targets={6}",
				unit.ActorID, configured.Count, visible.Count, attackable.Count, reachable.Count,
				reachable.Count(a => !excluded.Contains(a.ActorID)),
				string.Join(",", reachable.OrderBy(a => a.ActorID).Select(a => $"{a.Info.Name}#{a.ActorID}")));
		}

		List<Actor> ChemicalTargets(Actor unit)
		{
			return world.Actors.Where(a => IsEnemyTarget(a) && IsVisible(a) &&
				Info.ChemicalTargetPriorities.ContainsKey(a.Info.Name) && StateBase.CanAttackTarget(unit, a) &&
				HasReachableChemicalApproach(unit, a))
				.OrderBy(a => (a.CenterPosition - unit.CenterPosition).HorizontalLengthSquared).ThenBy(a => a.ActorID)
				.Take(Info.MaximumTargetCandidates)
				.Select(a => new
				{
					Actor = a,
					Score = EarlyInfantryRushPolicy.TargetScore(Info.ChemicalTargetPriorities[a.Info.Name],
						ActorValue(a), (a.CenterPosition - unit.CenterPosition).HorizontalLengthSquared, false)
				})
				.OrderByDescending(c => c.Score).ThenBy(c => c.Actor.ActorID).Select(c => c.Actor).ToList();
		}

		Actor SelectGrenadierTarget(List<Actor> units, HashSet<uint> excluded)
		{
			var candidates = GrenadierTargets(units).Where(a => !excluded.Contains(a.ActorID)).ToList();
			if (candidates.Count == 0)
				candidates = GrenadierTargets(units);
			return candidates.FirstOrDefault();
		}

		List<Actor> GrenadierTargets(List<Actor> units)
		{
			var center = units.Select(a => a.CenterPosition).Average();
			return world.Actors.Where(a => IsEnemyTarget(a) && IsVisible(a) &&
				Info.GrenadierTargetPriorities.ContainsKey(a.Info.Name) && units.Any(u => StateBase.CanAttackTarget(u, a)))
				.OrderBy(a => (a.CenterPosition - center).HorizontalLengthSquared).ThenBy(a => a.ActorID)
				.Take(Info.MaximumTargetCandidates).Where(a => TargetFormation(units, a).Count == units.Count)
				.Select(a => new
				{
					Actor = a,
					Score = EarlyInfantryRushPolicy.TargetScore(Info.GrenadierTargetPriorities[a.Info.Name],
						ActorValue(a), (a.CenterPosition - center).HorizontalLengthSquared, false)
				})
				.OrderByDescending(c => c.Score).ThenBy(c => c.Actor.ActorID).Select(c => c.Actor).ToList();
		}

		bool IsValidChemicalTarget(Actor unit, Actor target)
		{
			return IsOwnedUsable(unit) && IsEnemyTarget(target) && IsVisible(target) &&
				Info.ChemicalTargetPriorities.ContainsKey(target.Info.Name) && StateBase.CanAttackTarget(unit, target) &&
				HasReachableChemicalApproach(unit, target);
		}

		bool HasReachableChemicalApproach(Actor unit, Actor target)
		{
			return world.Map.FindTilesInCircle(target.Location, Info.ChemicalApproachRadiusCells)
				.Any(cell => CanEnter(unit, cell));
		}

		bool IsValidGrenadierTarget(List<Actor> units, Actor target)
		{
			return units.Count > 0 && IsEnemyTarget(target) && IsVisible(target) &&
				Info.GrenadierTargetPriorities.ContainsKey(target.Info.Name) &&
				units.Any(u => StateBase.CanAttackTarget(u, target)) && TargetFormation(units, target).Count == units.Count;
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

		void RequestNeededInfantry()
		{
			if (world.WorldTick < nextRequestTick || !HasCriticalAssets())
				return;

			var remainingGroups = Info.MaximumGrenadierGroups - launchedGroups;
			var grenadierTarget = Math.Max(0, remainingGroups * Info.GrenadierGroupSize);
			var grouped = groups.SelectMany(g => g.Units).ToHashSet();
			var availableGrenadiers = world.Actors.Count(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
				a.Info.Name == Info.GrenadierType && !grouped.Contains(a.ActorID));
			var grenadierCommitted = availableGrenadiers + QueuedCount(Info.GrenadierType) +
				RequestedCount(Info.GrenadierType);

			var chemicalTarget = Math.Max(0, Info.MaximumChemicalWarriors - launchedChemicals.Count);
			var availableChemicals = world.Actors.Count(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
				a.Info.Name == Info.ChemicalType && !launchedChemicals.Contains(a.ActorID));
			var chemicalCommitted = availableChemicals + QueuedCount(Info.ChemicalType) + RequestedCount(Info.ChemicalType);

			var desired = EarlyInfantryRushPolicy.NextProduction(grenadierCommitted, grenadierTarget,
				chemicalCommitted, chemicalTarget);
			var actorType = desired == EarlyInfantryProductionType.Grenadier ? Info.GrenadierType :
				desired == EarlyInfantryProductionType.Chemical ? Info.ChemicalType : null;
			if (actorType == null || RequestedCount(actorType) > 0 || QueuedCount(actorType) > 0 ||
				!HasFreeBuildableQueue(actorType))
				return;

			var requester = productionRequesters.FirstOrDefault(Exts.IsTraitEnabled);
			if (requester == null)
				return;

			requester.RequestUnitProduction(bot, actorType);
			if (actorType == Info.GrenadierType)
				ownsGrenadierRequest = true;
			else
				ownsChemicalRequest = true;
			nextRequestTick = world.WorldTick + Info.RequestInterval;
			Debug("requested {0}: grenadiers={1}/{2}, chemicals={3}/{4}", actorType,
				grenadierCommitted, grenadierTarget, chemicalCommitted, chemicalTarget);
		}

		bool HasCriticalAssets()
		{
			return OwnsAny(Info.ConstructionAssetTypes) && OwnsAny(Info.RefineryAssetTypes) &&
				OwnsAny(Info.HarvesterAssetTypes);
		}

		bool OwnsAny(HashSet<string> types)
		{
			return world.Actors.Any(a => a.Owner == player && a.IsInWorld && !a.IsDead && types.Contains(a.Info.Name));
		}

		int RequestedCount(string actorType)
		{
			return productionRequesters.Where(Exts.IsTraitEnabled)
				.Sum(r => r.RequestedProductionCount(bot, actorType));
		}

		int QueuedCount(string actorType)
		{
			return world.ActorsWithTrait<ProductionQueue>().Where(q => q.Actor.Owner == player)
				.Sum(q => q.Trait.AllQueued().Count(i => i.Item == actorType));
		}

		bool HasFreeBuildableQueue(string actorType)
		{
			if (!world.Map.Rules.Actors.TryGetValue(actorType, out var actorInfo))
				return false;

			var buildable = actorInfo.TraitInfoOrDefault<BuildableInfo>();
			return buildable != null && buildable.Queue.Any(queueType => AIUtils.FindQueues(player, queueType)
				.Any(queue => !queue.AllQueued().Any() && queue.BuildableItems().Any(item => item.Name == actorType)));
		}

		void RefreshRequestOwnership()
		{
			if (bot == null || productionRequesters == null)
				return;

			if (ownsGrenadierRequest && RequestedCount(Info.GrenadierType) == 0)
				ownsGrenadierRequest = false;
			if (ownsChemicalRequest && RequestedCount(Info.ChemicalType) == 0)
				ownsChemicalRequest = false;
		}

		void CancelOwnedRequests()
		{
			if (bot == null || productionRequesters == null)
				return;

			CancelOwnedRequest(Info.GrenadierType, ref ownsGrenadierRequest);
			CancelOwnedRequest(Info.ChemicalType, ref ownsChemicalRequest);
		}

		void CancelOwnedRequest(string actorType, ref bool owned)
		{
			if (!owned)
				return;

			foreach (var requester in productionRequesters.Where(Exts.IsTraitEnabled))
				if (requester.RequestedProductionCount(bot, actorType) > 0)
					requester.CancelRequestedUnitProduction(bot, actorType);
			owned = false;
		}

		void ClearState(string reason)
		{
			if (reserved.Count > 0 || pendingGrenadiers.Count > 0 || groups.Count > 0 || chemicalMissions.Count > 0)
				Debug("released early infantry: {0}", reason);
			reserved.Clear();
			pendingGrenadiers.Clear();
			groups.Clear();
			chemicalMissions.Clear();
			chemicalWaitSignatures.Clear();
			initialReservationPending = false;
		}

		static int ActorValue(Actor actor)
		{
			return Math.Max(1, actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1);
		}

		static string TargetStatus(Actor actor)
		{
			return actor == null ? "none" : $"{actor.Info.Name}#{actor.ActorID}[in-world={actor.IsInWorld},dead={actor.IsDead}]";
		}

		void Debug(string format, params object[] args)
		{
			if (!Info.DebugLogging)
				return;

			var message = string.Format(format, args);
			AIUtils.BotDebug("AI ({0}) early infantry: {1}", player.ClientIndex, message);
			Log.Write("debug", "AI early infantry: {0} (client {1}) at tick {2}: {3}",
				player, player.ClientIndex, world.WorldTick, message);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("EarlyInfantryLaunchedGroups", FieldSaver.FormatValue(launchedGroups)),
				new MiniYamlNode("EarlyInfantryNextRequestTick", FieldSaver.FormatValue(nextRequestTick)),
				new MiniYamlNode("EarlyInfantryOwnsGrenadierRequest", FieldSaver.FormatValue(ownsGrenadierRequest)),
				new MiniYamlNode("EarlyInfantryOwnsChemicalRequest", FieldSaver.FormatValue(ownsChemicalRequest)),
				new MiniYamlNode("EarlyInfantryInitialReservationPending", FieldSaver.FormatValue(initialReservationPending)),
				new MiniYamlNode("EarlyInfantryEnded", FieldSaver.FormatValue(ended)),
				new MiniYamlNode("EarlyInfantryPending", FieldSaver.FormatValue(pendingGrenadiers.OrderBy(id => id).ToArray())),
				new MiniYamlNode("EarlyInfantryLaunchedChemicals", FieldSaver.FormatValue(launchedChemicals.OrderBy(id => id).ToArray())),
				new MiniYamlNode("EarlyInfantryGroups", "", groups.Select(g =>
					new MiniYamlNode("Group", "", new List<MiniYamlNode>
					{
						new MiniYamlNode("Index", FieldSaver.FormatValue(g.Index)),
						new MiniYamlNode("Units", FieldSaver.FormatValue(g.Units.OrderBy(id => id).ToArray())),
						new MiniYamlNode("Target", FieldSaver.FormatValue(g.Target?.ActorID ?? 0)),
						new MiniYamlNode("HoldUntil", FieldSaver.FormatValue(g.HoldUntilTick)),
						new MiniYamlNode("LastOrder", FieldSaver.FormatValue(g.LastOrderTick))
					})).ToList()),
				new MiniYamlNode("EarlyInfantryChemicalMissions", "", chemicalMissions.OrderBy(kv => kv.Key).Select(kv =>
					new MiniYamlNode("Mission", "", new List<MiniYamlNode>
					{
						new MiniYamlNode("Unit", FieldSaver.FormatValue(kv.Key)),
						new MiniYamlNode("Target", FieldSaver.FormatValue(kv.Value.Target?.ActorID ?? 0)),
						new MiniYamlNode("LastOrder", FieldSaver.FormatValue(kv.Value.LastOrderTick))
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
					case "EarlyInfantryLaunchedGroups": launchedGroups = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EarlyInfantryNextRequestTick": nextRequestTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EarlyInfantryOwnsGrenadierRequest": ownsGrenadierRequest = FieldLoader.GetValue<bool>(node.Key, node.Value.Value); break;
					case "EarlyInfantryOwnsChemicalRequest": ownsChemicalRequest = FieldLoader.GetValue<bool>(node.Key, node.Value.Value); break;
					case "EarlyInfantryInitialReservationPending": initialReservationPending = FieldLoader.GetValue<bool>(node.Key, node.Value.Value); break;
					case "EarlyInfantryEnded": ended = FieldLoader.GetValue<bool>(node.Key, node.Value.Value); break;
					case "EarlyInfantryPending": LoadIds(pendingGrenadiers, node); break;
					case "EarlyInfantryLaunchedChemicals": LoadIds(launchedChemicals, node); break;
					case "EarlyInfantryGroups":
						groups.Clear();
						foreach (var groupNode in node.Value.Nodes)
							LoadGroup(groupNode);
						break;
					case "EarlyInfantryChemicalMissions":
						chemicalMissions.Clear();
						foreach (var missionNode in node.Value.Nodes)
							LoadChemicalMission(missionNode);
						break;
				}

			RebuildReservations();
		}

		void LoadGroup(MiniYamlNode node)
		{
			T Load<T>(string key, T fallback = default(T))
			{
				var value = node.Value.Nodes.FirstOrDefault(n => n.Key == key);
				return value == null ? fallback : FieldLoader.GetValue<T>(key, value.Value.Value);
			}

			var group = new GrenadierGroup
			{
				Index = Load<int>("Index"),
				HoldUntilTick = Load<int>("HoldUntil"),
				LastOrderTick = Load<int>("LastOrder"),
				Target = ActorById(Load<uint>("Target"))
			};
			group.Units.UnionWith(Load("Units", Array.Empty<uint>()));
			if (group.Units.Count > 0)
				groups.Add(group);
		}

		void LoadChemicalMission(MiniYamlNode node)
		{
			T Load<T>(string key, T fallback = default(T))
			{
				var value = node.Value.Nodes.FirstOrDefault(n => n.Key == key);
				return value == null ? fallback : FieldLoader.GetValue<T>(key, value.Value.Value);
			}

			var unitId = Load<uint>("Unit");
			if (unitId == 0)
				return;

			chemicalMissions[unitId] = new ChemicalMission
			{
				UnitId = unitId,
				Target = ActorById(Load<uint>("Target")),
				LastOrderTick = Load<int>("LastOrder")
			};
		}

		Actor ActorById(uint id)
		{
			return id == 0 ? null : world.GetActorById(id);
		}

		static void LoadIds(HashSet<uint> ids, MiniYamlNode node)
		{
			ids.Clear();
			ids.UnionWith(FieldLoader.GetValue<uint[]>(node.Key, node.Value.Value));
		}
	}
}
