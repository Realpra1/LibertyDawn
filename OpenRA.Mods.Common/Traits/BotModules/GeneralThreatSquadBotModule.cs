#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. You can redistribute
 * it and/or modify it under the terms of the GNU General Public License.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Claims otherwise-unassigned combat units and focus-fires the enemy type most threatening to the squad's top economic-mass type.")]
	public sealed class GeneralThreatSquadBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between complete target reassessments. A dead or invalid target is replaced immediately.")]
		public readonly int ReconsiderInterval = 25;

		[Desc("Write target, membership, and order decisions to debug.log.")]
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (ReconsiderInterval <= 0)
				throw new YamlException("General threat squad ReconsiderInterval must be greater than zero.");
		}

		public override object Create(ActorInitializer init)
		{
			return new GeneralThreatSquadBotModule(init.Self, this);
		}
	}

	public readonly struct GeneralThreatTypeScore
	{
		public readonly string ActorType;
		public readonly double Threat;
		public readonly long EconomicMass;

		public GeneralThreatTypeScore(string actorType, double threat, long economicMass)
		{
			ActorType = actorType;
			Threat = threat;
			EconomicMass = economicMass;
		}
	}

	public static class GeneralThreatSquadPolicy
	{
		public static string SelectTopEconomicMassType(
			IEnumerable<GeneralizedCombatThreatCalculator.GroupTypeCount> types)
		{
			return types.Where(t => t.ActorType != null && t.Count > 0)
				.OrderByDescending(t => t.EconomicMass)
				.ThenBy(t => t.ActorType, StringComparer.Ordinal)
				.Select(t => t.ActorType).FirstOrDefault();
		}

		public static string SelectHighestThreatType(IEnumerable<GeneralThreatTypeScore> types)
		{
			return types.Where(t => t.ActorType != null && double.IsFinite(t.Threat) && t.Threat >= 0)
				.OrderByDescending(t => t.Threat)
				.ThenByDescending(t => t.EconomicMass)
				.ThenBy(t => t.ActorType, StringComparer.Ordinal)
				.Select(t => t.ActorType).FirstOrDefault();
		}

		public static bool ShouldReconsider(int worldTick, int nextReconsiderTick, bool targetValid)
		{
			return !targetValid || worldTick >= nextReconsiderTick;
		}
	}

	public sealed class GeneralThreatSquadBotModule : ConditionalTrait<GeneralThreatSquadBotModuleInfo>,
		IBotEnabled, IBotTick, IBotUnitReservations, IGameSaveTraitData
	{
		readonly World world;
		readonly Player player;
		readonly GeneralizedCombatThreatCalculator threatCalculator;
		readonly HashSet<uint> reserved = new HashSet<uint>();
		IUnassignedCombatUnitRegistry unassignedCombatUnits;
		IBot bot;
		Actor target;
		int nextReconsiderTick;

		public GeneralThreatSquadBotModule(Actor self, GeneralThreatSquadBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
			threatCalculator = new GeneralizedCombatThreatCalculator(world.Map.Rules, world.Map.Grid.SubCellOffsets);
		}

		protected override void Created(Actor self)
		{
			unassignedCombatUnits = self.Owner.PlayerActor.Trait<IUnassignedCombatUnitRegistry>();
		}

		void IBotEnabled.BotEnabled(IBot enabledBot)
		{
			bot = enabledBot;
			nextReconsiderTick = world.WorldTick;
		}

		bool IBotUnitReservations.IsUnitReserved(Actor actor)
		{
			return actor != null && reserved.Contains(actor.ActorID);
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (bot == null)
				return;

			PruneMembers();
			RecruitUnassigned();
			var members = Members();
			if (members.Length == 0)
			{
				target = null;
				return;
			}

			var targetValid = IsEnemyTarget(target) && members.Any(a => CanAttackTarget(a, target));
			if (!GeneralThreatSquadPolicy.ShouldReconsider(world.WorldTick, nextReconsiderTick, targetValid))
				return;

			nextReconsiderTick = world.WorldTick + Info.ReconsiderInterval;
			target = SelectTarget(members);
			if (target == null)
				return;

			var attackers = members.Where(a => CanAttackTarget(a, target)).ToArray();
			if (attackers.Length == 0)
				return;

			bot.QueueOrder(new Order("Attack", null, Target.FromActor(target), false, groupedActors: attackers));
			if (Info.DebugLogging)
				Log.Write("debug", "General threat squad [{0}]: tick={1} members={2} top={3} target={4}#{5} " +
					"attackers={6} order=Attack.", player.PlayerName, world.WorldTick, members.Length,
					TopType(members), target.Info.Name, target.ActorID, attackers.Length);
		}

		void RecruitUnassigned()
		{
			var recruits = unassignedCombatUnits.UnassignedActors.Where(IsEligibleMember)
				.OrderBy(a => a.ActorID).ToArray();
			if (recruits.Length == 0)
				return;

			unassignedCombatUnits.ClaimActors(recruits);
			foreach (var actor in recruits)
			{
				reserved.Add(actor.ActorID);
				if (actor.TraitOrDefault<AutoTarget>() is AutoTarget autoTarget &&
					autoTarget.Stance != UnitStance.AttackAnything)
					bot.QueueOrder(new Order("SetUnitStance", actor, false)
					{
						ExtraData = (uint)UnitStance.AttackAnything
					});
			}

			nextReconsiderTick = world.WorldTick;
			if (Info.DebugLogging)
				Log.Write("debug", "General threat squad [{0}]: tick={1} recruited={2} total={3} stance=AttackAnything.",
					player.PlayerName, world.WorldTick, recruits.Length, reserved.Count);
		}

		Actor SelectTarget(Actor[] members)
		{
			var topType = TopType(members);
			if (topType == null)
				return null;

			var enemies = world.Actors.Where(IsEnemyTarget)
				.Where(enemy => members.Any(member => CanAttackTarget(member, enemy)))
				.OrderBy(enemy => enemy.ActorID).ToArray();
			if (enemies.Length == 0)
				return null;

			var enemyScores = enemies.GroupBy(enemy => enemy.Info.Name, StringComparer.OrdinalIgnoreCase)
				.Select(group =>
				{
					var economicMass = group.Sum(EconomicValue);
					return threatCalculator.TryGet(topType, group.Key, out var threat) ?
						new GeneralThreatTypeScore(group.Key, threat.DefenderThreatInAttackerEquivalents, economicMass) :
						new GeneralThreatTypeScore(null, 0, 0);
				}).ToArray();
			var selectedType = GeneralThreatSquadPolicy.SelectHighestThreatType(enemyScores);
			if (selectedType == null)
				return null;

			var center = members.Select(a => a.CenterPosition).Average();
			return enemies.Where(enemy => enemy.Info.Name.Equals(selectedType, StringComparison.OrdinalIgnoreCase))
				.ClosestTo(center);
		}

		string TopType(IEnumerable<Actor> members)
		{
			var types = members.GroupBy(actor => actor.Info.Name, StringComparer.OrdinalIgnoreCase)
				.Select(group => new GeneralizedCombatThreatCalculator.GroupTypeCount(group.Key, group.Count(),
					group.Select(EconomicValue).DefaultIfEmpty(0).Max()));
			return GeneralThreatSquadPolicy.SelectTopEconomicMassType(types);
		}

		int EconomicValue(Actor actor)
		{
			return Math.Max(0, actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0);
		}

		bool IsEligibleMember(Actor actor)
		{
			return actor != null && actor.Owner == player && actor.IsInWorld && !actor.IsDead &&
				actor.Info.HasTraitInfo<AttackBaseInfo>() &&
				(actor.Info.HasTraitInfo<MobileInfo>() || actor.Info.HasTraitInfo<AircraftInfo>());
		}

		bool IsEnemyTarget(Actor actor)
		{
			return actor != null && actor.IsInWorld && !actor.IsDead &&
				player.RelationshipWith(actor.Owner) == PlayerRelationship.Enemy &&
				actor.CanBeViewedByPlayer(player) && !actor.GetEnabledTargetTypes().IsEmpty;
		}

		static bool CanAttackTarget(Actor actor, Actor candidate)
		{
			return actor != null && candidate != null &&
				StateBase.CanAttackTarget(actor, candidate);
		}

		Actor[] Members()
		{
			return reserved.Select(world.GetActorById).Where(IsEligibleMember).OrderBy(a => a.ActorID).ToArray();
		}

		void PruneMembers()
		{
			reserved.RemoveWhere(id => !IsEligibleMember(world.GetActorById(id)));
			if (!IsEnemyTarget(target))
				target = null;
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("ReservedGeneralThreatUnits", FieldSaver.FormatValue(reserved.OrderBy(id => id).ToArray()))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var node = data.FirstOrDefault(n => n.Key == "ReservedGeneralThreatUnits");
			if (node == null)
				return;

			reserved.Clear();
			reserved.UnionWith(FieldLoader.GetValue<uint[]>(node.Key, node.Value.Value));
			target = null;
			nextReconsiderTick = world.WorldTick;
		}
	}
}
