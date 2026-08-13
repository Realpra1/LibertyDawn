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
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Coordinates one fast bike/buggy covert harassment squad with bounded artillery support.")]
	public class CovertHarassmentBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Live prerequisites required to activate this capability.")]
		public readonly string[] RequiredPrerequisites = Array.Empty<string>();

		public readonly HashSet<string> CoreTypes = new HashSet<string>();
		public readonly HashSet<string> SupportTypes = new HashSet<string>();
		public readonly HashSet<string> TowerTypes = new HashSet<string>();
		public readonly Dictionary<string, int> TargetPriorities = new Dictionary<string, int>();
		public readonly int MinimumCoreUnits = 2;
		public readonly int MaximumCoreUnits = 12;
		public readonly int CoreUnitsPerSupport = 3;
		public readonly int MaximumSupportUnits = 4;
		public readonly int SupportJoinRadiusCells = 5;
		public readonly int SupportFollowDistanceCells = 3;
		public readonly int ScanInterval = 50;
		public readonly int OrderInterval = 50;
		public readonly int MaximumTargetCandidates = 48;
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
			if (RequiredPrerequisites.Length == 0 || CoreTypes.Count == 0 || SupportTypes.Count == 0 ||
				TowerTypes.Count == 0 || TargetPriorities.Count == 0 || MinimumCoreUnits <= 0 ||
				MaximumCoreUnits < MinimumCoreUnits || CoreUnitsPerSupport <= 0 || MaximumSupportUnits <= 0 ||
				SupportJoinRadiusCells <= 0 || SupportFollowDistanceCells < 0 || ScanInterval <= 0 ||
				OrderInterval <= 0 || MaximumTargetCandidates <= 0 || TargetPriorities.Any(p => p.Value <= 0) ||
				FailsafeTestAdvancedWorkMilliseconds < 0 || FailsafeTestAdvancedWorkFromTick < 0 ||
				FailsafeTestAdvancedWorkUntilTick < 0 || (FailsafeTestAdvancedWorkUntilTick > 0 &&
					FailsafeTestAdvancedWorkUntilTick <= FailsafeTestAdvancedWorkFromTick))
				throw new YamlException("Covert harassment prerequisites, types, priorities, group bounds, and intervals must be configured and valid.");

			foreach (var actorType in CoreTypes.Concat(SupportTypes).Concat(TowerTypes).Concat(TargetPriorities.Keys))
				if (!rules.Actors.ContainsKey(actorType))
					throw new YamlException($"Covert harassment actor '{actorType}' does not exist.");
		}

		public override object Create(ActorInitializer init) { return new CovertHarassmentBotModule(init.Self, this); }
	}

	public class CovertHarassmentBotModule : ConditionalTrait<CovertHarassmentBotModuleInfo>,
		IBotEnabled, IBotTick, IBotUnitReservations, IGameSaveTraitData, IAdvancedBotTick
	{
		readonly World world;
		readonly Player player;
		readonly HashSet<uint> reserved = new HashSet<uint>();
		readonly HashSet<uint> core = new HashSet<uint>();
		readonly HashSet<uint> support = new HashSet<uint>();
		IBot bot;
		IBotUnitReservations[] otherReservations;
		IBotTransportReservations[] transportReservations;
		IUnassignedCombatUnitRegistry unassignedCombatUnits;
		SquadManagerBotModule squadManager;
		DomainIndex domainIndex;
		TechTree techTree;
		Actor target;
		int scanTicks;
		int lastOrderTick;
		string lastComposition;
		bool advancedBehaviorEnabled = true;

		public CovertHarassmentBotModule(Actor self, CovertHarassmentBotModuleInfo info)
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
			unassignedCombatUnits = player.PlayerActor.TraitOrDefault<IUnassignedCombatUnitRegistry>();
			RefreshSquadManager();
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self)
		{
			// Reserve newly available core vehicles before ordinary squad discovery runs.
			scanTicks = 1;
		}

		protected override void TraitDisabled(Actor self)
		{
			ClearState("bot condition disabled");
		}

		void IBotEnabled.BotEnabled(IBot enabledBot) { bot = enabledBot; }

		bool IBotUnitReservations.IsUnitReserved(Actor actor)
		{
			return actor != null && reserved.Contains(actor.ActorID);
		}

		string IAdvancedBotTick.FailsafeModuleId => "CovertHarassmentBotModule";

		void IAdvancedBotTick.SetAdvancedBehaviorEnabled(bool enabled)
		{
			if (advancedBehaviorEnabled == enabled)
				return;

			advancedBehaviorEnabled = enabled;
			if (!enabled)
			{
				var releasedActors = Actors(reserved);
				squadManager?.RetainFailsafeReleasedActors("CovertHarassmentBotModule", releasedActors);
				if (Info.DebugLogging && reserved.Count > 0)
					Debug("released squad: reason=failsafe-degraded actors={0}", string.Join(",",
						releasedActors.Select(a => a.Info.Name + "#" + a.ActorID)));

				ClearState("failsafe degraded");
			}
			else
			{
				scanTicks = 1;
				Debug("enabled for recovery probe");
			}
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (IsTraitDisabled || !advancedBehaviorEnabled || player.WinState != WinState.Undefined)
				return;

			RunFailsafeTestPressure();
			if (--scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			if (!techTree.HasPrerequisites(Info.RequiredPrerequisites))
			{
				ClearState("covert capability unavailable");
				return;
			}

			RefreshSquadManager();
			Rebalance();
			if (core.Count < Info.MinimumCoreUnits)
				return;

			UpdateOrders();
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
			return IsOwnedUsable(actor) && !IsResupplying(actor) &&
				!(transportReservations?.Any(r => r.IsTransportReserved(actor)) ?? false) &&
				!(otherReservations?.Any(r => r.IsUnitReserved(actor)) ?? false) &&
				!(squadManager?.IsUnitProtectingBase(actor) ?? false);
		}

		static bool IsResupplying(Actor actor)
		{
			var activity = actor.CurrentActivity;
			return activity is Resupply || activity?.NextActivity is Resupply;
		}

		List<Actor> Claimable(HashSet<string> types)
		{
			return world.Actors.Where(a => types.Contains(a.Info.Name) && IsClaimable(a))
				.OrderBy(a => a.ActorID).ToList();
		}

		void Rebalance()
		{
			var eligibleCore = Claimable(Info.CoreTypes);
			var selectedCore = PreservePrevious(eligibleCore, core, Info.MaximumCoreUnits);
			if (selectedCore.Count < Info.MinimumCoreUnits)
			{
				ClearState("insufficient core vehicles");
				return;
			}

			var eligibleSupport = Claimable(Info.SupportTypes);
			var supportCount = CovertHarassmentPolicy.SupportCount(selectedCore.Count,
				Info.CoreUnitsPerSupport, Info.MaximumSupportUnits, eligibleSupport.Count);
			var selectedSupport = PreservePrevious(eligibleSupport, support, supportCount);

			Replace(core, selectedCore);
			Replace(support, selectedSupport);
			reserved.Clear();
			reserved.UnionWith(core);
			reserved.UnionWith(support);
			unassignedCombatUnits?.ClaimActors(Actors(reserved));

			var bikeCount = selectedCore.Count(a => a.Info.Name == "bike");
			var buggyCount = selectedCore.Count(a => a.Info.Name == "bggy");
			var composition = $"core={core.Count} bikes={bikeCount} buggies={buggyCount} support={support.Count}";
			if (Info.DebugLogging)
				composition += " " + RejectionSummary();
			if (composition != lastComposition)
			{
				Debug("composition {0}", composition);
				lastComposition = composition;
			}
		}

		static List<Actor> PreservePrevious(List<Actor> eligible, HashSet<uint> previous, int count)
		{
			return eligible.OrderByDescending(a => previous.Contains(a.ActorID)).ThenBy(a => a.ActorID)
				.Take(count).ToList();
		}

		static void Replace(HashSet<uint> destination, IEnumerable<Actor> actors)
		{
			destination.Clear();
			destination.UnionWith(actors.Select(a => a.ActorID));
		}

		string RejectionSummary()
		{
			var types = new HashSet<string>(Info.CoreTypes.Concat(Info.SupportTypes));
			var candidates = world.Actors.Where(a => IsOwnedUsable(a) && types.Contains(a.Info.Name)).ToList();
			var transports = candidates.Count(a => transportReservations.Any(r => r.IsTransportReserved(a)));
			var others = candidates.Count(a => otherReservations.Any(r => r.IsUnitReserved(a)));
			var protection = candidates.Count(a => squadManager?.IsUnitProtectingBase(a) ?? false);
			var resupplying = candidates.Count(IsResupplying);
			return $"candidates={candidates.Count} rejected=transport:{transports}/other:{others}/protection:{protection}/repair:{resupplying}";
		}

		void UpdateOrders()
		{
			var cores = Actors(core);
			if (cores.Count < Info.MinimumCoreUnits)
				return;

			var supports = Actors(support);
			var center = cores.Select(a => a.CenterPosition).Average();
			var supportCenter = supports.Count == 0 ? "none" :
				world.Map.CellContaining(supports.Select(a => a.CenterPosition).Average()).ToString();
			var previousTarget = target;
			target = SelectTarget(cores, supports, center);
			var targetChanged = target != previousTarget;
			if (targetChanged)
				Debug("target transition {0} -> {1}", TargetStatus(previousTarget), TargetStatus(target));

			if (!CovertHarassmentPolicy.ShouldIssueOrders(targetChanged,
				world.WorldTick, lastOrderTick, Info.OrderInterval))
				return;

			lastOrderTick = world.WorldTick;
			if (target == null)
			{
				Stop(cores);
				FollowCore(supports, center, null);
				return;
			}

			var isTower = Info.TowerTypes.Contains(target.Info.Name);
			var joinRadius = WDist.FromCells(Info.SupportJoinRadiusCells).Length;
			var readySupport = supports.Count(a => (a.CenterPosition - center).Length <= joinRadius);
			if (CovertHarassmentPolicy.ShouldWaitForSupport(isTower, supports.Count, readySupport))
			{
				Stop(cores);
				FollowCore(supports, center, target);
				Debug("waiting tower={0}#{1} center={2} support-center={3} ready-support={4}/{5}", target.Info.Name,
					target.ActorID, world.Map.CellContaining(center), supportCenter, readySupport, supports.Count);
				return;
			}

			if (isTower)
			{
				var attackers = cores.Concat(supports).ToArray();
				bot.QueueOrder(new Order("Attack", null, Target.FromActor(target), false, groupedActors: attackers));
				Debug("heavy attack target={0}#{1} center={2} support-center={3} core={4} support={5}", target.Info.Name,
					target.ActorID, world.Map.CellContaining(center), supportCenter, cores.Count, supports.Count);
				return;
			}

			bot.QueueOrder(new Order("Attack", null, Target.FromActor(target), false, groupedActors: cores.ToArray()));
			FollowCore(supports, center, target);
			Debug("raid target={0}#{1} center={2} support-center={3} core={4} support-following={5}", target.Info.Name,
				target.ActorID, world.Map.CellContaining(center), supportCenter, cores.Count, supports.Count);
		}

		Actor SelectTarget(List<Actor> cores, List<Actor> supports, WPos center)
		{
			var candidates = world.Actors.Where(a => IsEnemyTarget(a) && IsVisible(a) &&
				Info.TargetPriorities.ContainsKey(a.Info.Name) && IsReachable(cores[0], a.Location))
				.OrderBy(a => (a.CenterPosition - center).HorizontalLengthSquared).ThenBy(a => a.ActorID)
				.Take(Info.MaximumTargetCandidates)
				.Where(a =>
				{
					var tower = Info.TowerTypes.Contains(a.Info.Name);
					return CovertHarassmentPolicy.CanSelectTarget(tower, supports.Count) &&
						(tower ? supports.Any(s => StateBase.CanAttackTarget(s, a)) :
						cores.Any(c => StateBase.CanAttackTarget(c, a)));
				});

			return candidates.Select(a => new
				{
					Actor = a,
					Score = CovertHarassmentPolicy.TargetScore(Info.TargetPriorities[a.Info.Name],
						ActorValue(a), (a.CenterPosition - center).HorizontalLengthSquared, a == target)
				})
				.OrderByDescending(c => c.Score).ThenBy(c => c.Actor.ActorID)
				.Select(c => c.Actor).FirstOrDefault();
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

		void FollowCore(IEnumerable<Actor> supports, WPos center, Actor facingTarget)
		{
			var destinationPosition = center;
			if (facingTarget != null && Info.SupportFollowDistanceCells > 0)
				destinationPosition += AirThreatGeometry.ScaleToLength(center - facingTarget.CenterPosition,
					Info.SupportFollowDistanceCells * 1024);

			var destination = world.Map.Clamp(world.Map.CellContaining(destinationPosition));
			foreach (var actor in supports)
				if (IsReachable(actor, destination))
					bot.QueueOrder(new Order("Move", actor, Target.FromCell(world, destination), false));
		}

		void Stop(IEnumerable<Actor> actors)
		{
			foreach (var actor in actors)
				bot.QueueOrder(new Order("Stop", actor, false));
		}

		List<Actor> Actors(HashSet<uint> ids)
		{
			return ids.Select(world.GetActorById).Where(IsOwnedUsable).OrderBy(a => a.ActorID).ToList();
		}

		static int ActorValue(Actor actor)
		{
			return Math.Max(1, actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1);
		}

		static string TargetStatus(Actor actor)
		{
			return actor == null ? "none" :
				$"{actor.Info.Name}#{actor.ActorID}[in-world={actor.IsInWorld},dead={actor.IsDead}]";
		}

		void ClearState(string reason)
		{
			unassignedCombatUnits?.RegisterReleasedActors(Actors(reserved));
			if (reserved.Count > 0 || target != null)
				Debug("released squad: {0}", reason);

			reserved.Clear();
			core.Clear();
			support.Clear();
			target = null;
			lastComposition = null;
		}

		void Debug(string format, params object[] args)
		{
			if (!Info.DebugLogging)
				return;

			var message = string.Format(format, args);
			AIUtils.BotDebug("AI ({0}) covert harassment: {1}", player.ClientIndex, message);
			Log.Write("debug", "AI covert harassment: {0} (client {1}) at tick {2}: {3}",
				player, player.ClientIndex, world.WorldTick, message);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("CovertHarassmentScanTicks", FieldSaver.FormatValue(scanTicks)),
				new MiniYamlNode("CovertHarassmentLastOrderTick", FieldSaver.FormatValue(lastOrderTick)),
				new MiniYamlNode("CovertHarassmentTarget", FieldSaver.FormatValue(target?.ActorID ?? 0)),
				new MiniYamlNode("CovertHarassmentCore", FieldSaver.FormatValue(core.OrderBy(id => id).ToArray())),
				new MiniYamlNode("CovertHarassmentSupport", FieldSaver.FormatValue(support.OrderBy(id => id).ToArray()))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			foreach (var node in data)
				switch (node.Key)
				{
					case "CovertHarassmentScanTicks": scanTicks = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "CovertHarassmentLastOrderTick": lastOrderTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "CovertHarassmentTarget":
						var targetId = FieldLoader.GetValue<uint>(node.Key, node.Value.Value);
						target = targetId == 0 ? null : world.GetActorById(targetId);
						break;
					case "CovertHarassmentCore": LoadIds(core, node); break;
					case "CovertHarassmentSupport": LoadIds(support, node); break;
				}

			reserved.Clear();
			reserved.UnionWith(core);
			reserved.UnionWith(support);
			unassignedCombatUnits?.ClaimActors(Actors(reserved));
		}

		static void LoadIds(HashSet<uint> ids, MiniYamlNode node)
		{
			ids.Clear();
			ids.UnionWith(FieldLoader.GetValue<uint[]>(node.Key, node.Value.Value));
		}
	}
}
