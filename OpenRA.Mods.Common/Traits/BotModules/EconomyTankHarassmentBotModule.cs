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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Coordinates one bounded Medium Tank economy harassment mission.")]
	public sealed class EconomyTankHarassmentBotModuleInfo : ConditionalTraitInfo
	{
		public readonly string[] RequiredPrerequisites = Array.Empty<string>();
		public readonly HashSet<string> TankTypes = new HashSet<string>();
		public readonly Dictionary<string, int> TargetPriorities = new Dictionary<string, int>();
		public readonly int MinimumTanks = 2;
		public readonly int MaximumTanks = 4;
		public readonly int MobileDefenseReserve = 2;
		public readonly int ScanInterval = 50;
		public readonly int OrderInterval = 50;
		public readonly int MissionTimeout = 900;
		public readonly int NoProgressTimeout = 250;
		public readonly int TargetDefenseRadiusCells = 6;
		public readonly int MaximumTargetDefenderValuePercent = 75;
		public readonly int MaximumTargetCandidates = 48;
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (RequiredPrerequisites.Length == 0 || TankTypes.Count == 0 || TargetPriorities.Count == 0 ||
				MinimumTanks <= 0 || MaximumTanks < MinimumTanks || MaximumTanks > 4 || MobileDefenseReserve < 1 ||
				ScanInterval <= 0 || OrderInterval <= 0 || MissionTimeout <= 0 || NoProgressTimeout <= 0 ||
				NoProgressTimeout > MissionTimeout || TargetDefenseRadiusCells <= 0 ||
				MaximumTargetDefenderValuePercent < 0 || MaximumTargetCandidates <= 0 ||
				TargetPriorities.Any(p => p.Value <= 0))
				throw new YamlException("Economy tank harassment prerequisites, types, targets, group bounds, and timeouts must be configured and valid.");

			foreach (var type in TankTypes.Concat(TargetPriorities.Keys).Distinct())
				if (!rules.Actors.ContainsKey(type))
					throw new YamlException($"Economy tank harassment actor '{type}' does not exist.");
		}

		public override object Create(ActorInitializer init) { return new EconomyTankHarassmentBotModule(init.Self, this); }
	}

	public sealed class EconomyTankHarassmentBotModule : ConditionalTrait<EconomyTankHarassmentBotModuleInfo>,
		IBotEnabled, IBotTick, IBotUnitReservations, INotifyAppliedDamage, IGameSaveTraitData
	{
		readonly World world;
		readonly Player player;
		readonly HashSet<uint> tanks = new HashSet<uint>();
		IBot bot;
		TechTree techTree;
		DomainIndex domainIndex;
		EconomyTroopProductionBotModule readiness;
		SquadManagerBotModule squadManager;
		IBotUnitReservations[] otherReservations;
		IBotTransportReservations[] transportReservations;
		Actor target;
		int scanTicks;
		int lastOrderTick;
		int missionStartedTick;
		int lastProgressTick;
		long bestDistanceSquared = long.MaxValue;
		int previousTargetHp = int.MaxValue;
		int firstEligibleTargetTick = -1;
		int missionDamage;
		int missionKills;
		string lastState;

		public EconomyTankHarassmentBotModule(Actor self, EconomyTankHarassmentBotModuleInfo info)
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
			RefreshCollaborators();
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self) { scanTicks = 1; }

		protected override void TraitDisabled(Actor self) { Release("bot condition disabled"); }

		void IBotEnabled.BotEnabled(IBot enabledBot) { bot = enabledBot; }

		bool IBotUnitReservations.IsUnitReserved(Actor actor)
		{
			return actor != null && tanks.Contains(actor.ActorID);
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (IsTraitDisabled || player.WinState != WinState.Undefined || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			RefreshCollaborators();
			if (!techTree.HasPrerequisites(Info.RequiredPrerequisites) || readiness?.IsReadyForRaid != true)
			{
				Release("readiness or Economy authority lost");
				return;
			}

			if (tanks.Count > 0 && EconomyTroopPolicy.MissionExpired(world.WorldTick, missionStartedTick,
				lastProgressTick, Info.MissionTimeout, Info.NoProgressTimeout))
			{
				Release("timeout or no progress");
				return;
			}

			var eligible = world.Actors.Where(a => Info.TankTypes.Contains(a.Info.Name) && IsClaimable(a))
				.OrderByDescending(a => tanks.Contains(a.ActorID)).ThenBy(a => a.ActorID).ToList();
			var count = EconomyTroopPolicy.RaidGroupSize(eligible.Count, Info.MobileDefenseReserve,
				Info.MinimumTanks, Info.MaximumTanks);
			if (count == 0)
			{
				Release("credible mobile reserve unavailable");
				return;
			}

			var selected = eligible.Take(count).ToList();
			var selectedTarget = SelectTarget(selected);
			if (selectedTarget == null)
			{
				Release("no reachable exposed target");
				return;
			}

			if (firstEligibleTargetTick < 0)
			{
				firstEligibleTargetTick = world.WorldTick;
				LogState($"eligible target={selectedTarget.Info.Name}#{selectedTarget.ActorID} tanks={string.Join(",", selected.Select(a => a.ActorID))}");
			}

			var starting = tanks.Count == 0;
			Replace(tanks, selected);
			if (starting)
			{
				missionStartedTick = lastProgressTick = world.WorldTick;
				bestDistanceSquared = long.MaxValue;
				previousTargetHp = int.MaxValue;
				missionDamage = 0;
				missionKills = 0;
			}

			var targetChanged = target != selectedTarget;
			target = selectedTarget;
			var center = selected.Select(a => a.CenterPosition).Average();
			var distanceSquared = (target.CenterPosition - center).HorizontalLengthSquared;
			var targetHp = target.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue;
			if (targetChanged || EconomyTroopPolicy.HasProgress(distanceSquared, bestDistanceSquared,
				targetHp, previousTargetHp))
			{
				lastProgressTick = world.WorldTick;
				bestDistanceSquared = distanceSquared;
				previousTargetHp = targetHp;
			}

			if (targetChanged || world.WorldTick >= lastOrderTick + Info.OrderInterval)
			{
				lastOrderTick = world.WorldTick;
				bot.QueueOrder(new Order("Attack", null, Target.FromActor(target), false,
					groupedActors: selected.ToArray()));
			}

			LogState($"active tanks={string.Join(",", selected.Select(a => a.ActorID))} target={target.Info.Name}#{target.ActorID} eligible-delay={world.WorldTick - firstEligibleTargetTick}");
		}

		void INotifyAppliedDamage.AppliedDamage(Actor self, Actor damaged, AttackInfo e)
		{
			if (IsTraitDisabled || target == null || damaged != target || e.Attacker == null ||
				!tanks.Contains(e.Attacker.ActorID) || player.RelationshipWith(damaged.Owner) != PlayerRelationship.Enemy)
				return;

			var applied = Math.Max(0, e.Damage.Value);
			if (applied <= 0)
				return;

			var firstDamage = missionDamage == 0;
			missionDamage += applied;
			var killed = e.DamageState == DamageState.Dead && e.PreviousDamageState != DamageState.Dead;
			if (killed)
				missionKills++;

			if (firstDamage || killed)
				LogState($"outcome attacker={e.Attacker.Info.Name}#{e.Attacker.ActorID} target={damaged.Info.Name}#{damaged.ActorID} damage={applied} total-damage={missionDamage} killed={killed} kills={missionKills}");
		}

		void RefreshCollaborators()
		{
			if (readiness == null || readiness.IsTraitDisabled)
				readiness = player.PlayerActor.TraitsImplementing<EconomyTroopProductionBotModule>()
					.FirstOrDefault(m => !m.IsTraitDisabled);

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
				!transportReservations.Any(r => r.IsTransportReserved(actor)) &&
				!otherReservations.Any(r => r.IsUnitReserved(actor)) &&
				!(squadManager?.IsUnitProtectingBase(actor) ?? false);
		}

		static bool IsResupplying(Actor actor)
		{
			return actor.CurrentActivity is Resupply || actor.CurrentActivity?.NextActivity is Resupply;
		}

		Actor SelectTarget(List<Actor> selected)
		{
			var center = selected.Select(a => a.CenterPosition).Average();
			var raidValue = selected.Sum(ActorValue);
			return world.Actors.Where(a => IsEnemyTarget(a) && IsVisible(a) &&
				Info.TargetPriorities.ContainsKey(a.Info.Name) && IsReachable(selected[0], a.Location) &&
				selected.Any(t => StateBase.CanAttackTarget(t, a)))
				.OrderBy(a => (a.CenterPosition - center).HorizontalLengthSquared).ThenBy(a => a.ActorID)
				.Take(Info.MaximumTargetCandidates)
				.Where(a => EconomyTroopPolicy.IsExposedTarget(NearbyDefenderValue(a), raidValue,
					Info.MaximumTargetDefenderValuePercent))
				.Select(a => new
				{
					Actor = a,
					Score = (long)Info.TargetPriorities[a.Info.Name] * 1000000 -
						(a.CenterPosition - center).HorizontalLengthSquared / (1024L * 1024L) +
						(a == target ? 500000 : 0)
				})
				.OrderByDescending(c => c.Score).ThenBy(c => c.Actor.ActorID)
				.Select(c => c.Actor).FirstOrDefault();
		}

		long NearbyDefenderValue(Actor candidate)
		{
			return world.FindActorsInCircle(candidate.CenterPosition, WDist.FromCells(Info.TargetDefenseRadiusCells))
				.Where(a => IsEnemyTarget(a) && a != candidate && a.Info.HasTraitInfo<AttackBaseInfo>())
				.Sum(a => (long)ActorValue(a));
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

		static int ActorValue(Actor actor)
		{
			return Math.Max(1, actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1);
		}

		static void Replace(HashSet<uint> destination, IEnumerable<Actor> actors)
		{
			destination.Clear();
			destination.UnionWith(actors.Select(a => a.ActorID));
		}

		void Release(string reason)
		{
			var released = tanks.Select(world.GetActorById).Where(IsOwnedUsable).OrderBy(a => a.ActorID).ToArray();
			var targetDescription = target == null ? "none" : $"{target.Info.Name}#{target.ActorID}";
			var duration = missionStartedTick > 0 ? world.WorldTick - missionStartedTick : 0;
			var destination = released.Length > 0 ?
				squadManager?.GroundRecoveryDestination() ?? released[0].Location : CPos.Zero;
			if (released.Length > 0 && bot != null)
			{
				bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(world, destination), false,
					groupedActors: released));
			}

			tanks.Clear();
			target = null;
			missionStartedTick = lastProgressTick = lastOrderTick = 0;
			bestDistanceSquared = long.MaxValue;
			previousTargetHp = int.MaxValue;
			if (released.Length > 0)
				LogState($"released reason={reason} actors={string.Join(",", released.Select(a => a.ActorID))} target={targetDescription} duration={duration} damage={missionDamage} kills={missionKills} return={destination}");

			firstEligibleTargetTick = -1;
			missionDamage = 0;
			missionKills = 0;
		}

		void LogState(string state)
		{
			if (state == lastState)
				return;

			lastState = state;
			if (!Info.DebugLogging)
				return;

			AIUtils.BotDebug("AI ({0}) economy tank raid: {1}", player.ClientIndex, state);
			Log.Write("debug", "AI economy tank raid: {0} (client {1}) at tick {2}: {3}",
				player, player.ClientIndex, world.WorldTick, state);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			return IsTraitDisabled ? null : new List<MiniYamlNode>
			{
				new MiniYamlNode("EconomyTankRaidScanTicks", FieldSaver.FormatValue(scanTicks)),
				new MiniYamlNode("EconomyTankRaidLastOrderTick", FieldSaver.FormatValue(lastOrderTick)),
				new MiniYamlNode("EconomyTankRaidStartedTick", FieldSaver.FormatValue(missionStartedTick)),
				new MiniYamlNode("EconomyTankRaidLastProgressTick", FieldSaver.FormatValue(lastProgressTick)),
				new MiniYamlNode("EconomyTankRaidBestDistance", FieldSaver.FormatValue(bestDistanceSquared)),
				new MiniYamlNode("EconomyTankRaidPreviousHp", FieldSaver.FormatValue(previousTargetHp)),
				new MiniYamlNode("EconomyTankRaidFirstEligibleTick", FieldSaver.FormatValue(firstEligibleTargetTick)),
				new MiniYamlNode("EconomyTankRaidDamage", FieldSaver.FormatValue(missionDamage)),
				new MiniYamlNode("EconomyTankRaidKills", FieldSaver.FormatValue(missionKills)),
				new MiniYamlNode("EconomyTankRaidTarget", FieldSaver.FormatValue(target?.ActorID ?? 0)),
				new MiniYamlNode("EconomyTankRaidTanks", FieldSaver.FormatValue(tanks.OrderBy(id => id).ToArray()))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			foreach (var node in data)
				switch (node.Key)
				{
					case "EconomyTankRaidScanTicks": scanTicks = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyTankRaidLastOrderTick": lastOrderTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyTankRaidStartedTick": missionStartedTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyTankRaidLastProgressTick": lastProgressTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyTankRaidBestDistance": bestDistanceSquared = FieldLoader.GetValue<long>(node.Key, node.Value.Value); break;
					case "EconomyTankRaidPreviousHp": previousTargetHp = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyTankRaidFirstEligibleTick": firstEligibleTargetTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyTankRaidDamage": missionDamage = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyTankRaidKills": missionKills = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyTankRaidTarget":
						var targetId = FieldLoader.GetValue<uint>(node.Key, node.Value.Value);
						target = targetId == 0 ? null : world.GetActorById(targetId);
						break;
					case "EconomyTankRaidTanks":
						tanks.Clear();
						tanks.UnionWith(FieldLoader.GetValue<uint[]>(node.Key, node.Value.Value));
						break;
				}
		}
	}
}
