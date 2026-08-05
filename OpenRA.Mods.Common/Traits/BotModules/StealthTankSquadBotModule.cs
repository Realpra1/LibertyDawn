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
		public readonly int ThreatRangeBufferCells = 2;
		public readonly int DetectorRangeBufferCells = 2;
		public readonly int KiteRangeMarginCells = 1;
		public readonly int CarefulClearValueRatio = 5;
		public readonly int MinimumLateHarassmentGroupSize = 3;
		public readonly int TargetSwitchImprovementPercent = 25;
		public readonly int InfantryTargetPriority = 1200;
		public readonly int StructureTargetPriority = 500;
		public readonly int TankTargetPriority = 1500;
		public readonly int InfantryClusterRadiusCells = 0;
		public readonly int InfantryClusterBonusPercentPerNearbyActor = 0;
		public readonly int MaximumInfantryClusterMultiplierPercent = 100;
		public readonly bool CrushInfantryTargets = true;
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (UnitTypes.Count == 0 || string.IsNullOrWhiteSpace(SquadLabel) || ScanInterval <= 0 || OrderInterval <= 0 ||
				MaximumTargetCandidates <= 0 || MaximumHarassmentGroups <= 0 ||
				ThreatRangeBufferCells < 0 || DetectorRangeBufferCells < 0 ||
				KiteRangeMarginCells < 0 || CarefulClearValueRatio <= 0 || MinimumLateHarassmentGroupSize <= 0 ||
				TargetSwitchImprovementPercent < 0 || InfantryTargetPriority < 0 || StructureTargetPriority < 0 ||
				TankTargetPriority < 0 || InfantryClusterRadiusCells < 0 || InfantryClusterBonusPercentPerNearbyActor < 0 ||
				MaximumInfantryClusterMultiplierPercent < 100)
				throw new YamlException("Stealth squad types, labels, intervals, bounds, priorities, buffers, and ratios must be positive and valid.");
		}

		public override object Create(ActorInitializer init) { return new StealthTankSquadBotModule(init.Self, this); }
	}

	public class StealthTankSquadBotModule : ConditionalTrait<StealthTankSquadBotModuleInfo>,
		IBotEnabled, IBotTick, IBotUnitReservations
	{
		sealed class SpecialistGroup
		{
			public readonly int Index;
			public readonly List<Actor> Units = new List<Actor>();
			public Actor Target;
			public long TargetScore;
			public int LastOrderTick;
			public int LastNoTargetLogTick;

			public SpecialistGroup(int index) { Index = index; }
		}

		sealed class Threat
		{
			public Actor Actor;
			public int WeaponRangeCells;
			public int DetectorRangeCells;
			public int Value;
		}

		static readonly BitSet<TargetableType> TankTargetTypes = new BitSet<TargetableType>("Tank");
		static readonly BitSet<TargetableType> GroundTargetTypes = new BitSet<TargetableType>("Ground");
		static readonly BitSet<TargetableType> InfantryTargetTypes = new BitSet<TargetableType>("Infantry");
		static readonly BitSet<TargetableType> StructureTargetTypes = new BitSet<TargetableType>("Structure");

		readonly World world;
		readonly Player player;
		readonly HashSet<uint> reserved = new HashSet<uint>();
		readonly SpecialistGroup[] groups;
		IBot bot;
		IBotTransportReservations[] transportReservations;
		int scanTicks;
		int lastEligibleCount = -1;

		public StealthTankSquadBotModule(Actor self, StealthTankSquadBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
			var groupCount = info.MaximumHarassmentGroups + (info.IncludeAttackGroup ? 1 : 0);
			groups = Enumerable.Range(0, groupCount).Select(i => new SpecialistGroup(i)).ToArray();
		}

		protected override void Created(Actor self)
		{
			transportReservations = self.Owner.PlayerActor.TraitsImplementing<IBotTransportReservations>().ToArray();
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self)
		{
			// Establish reservations before the ordinary squad manager can claim newly available tanks.
			scanTicks = 1;
		}

		protected override void TraitDisabled(Actor self)
		{
			reserved.Clear();
			lastEligibleCount = -1;
			foreach (var group in groups)
			{
				group.Units.Clear();
				group.Target = null;
			}
		}

		void IBotEnabled.BotEnabled(IBot enabledBot) { bot = enabledBot; }

		bool IBotUnitReservations.IsUnitReserved(Actor actor)
		{
			return actor != null && reserved.Contains(actor.ActorID);
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (IsTraitDisabled || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			Rebalance();
			if (reserved.Count == 0)
				return;

			var enemies = world.Actors.Where(IsEnemyTarget).OrderBy(a => a.ActorID).ToList();

			// Build one shared threat snapshot per bot scan. Do not truncate by actor creation order:
			// late-built detectors and defenses are at least as important as opening units.
			var threats = enemies.Select(CreateThreat).Where(t => t != null).ToList();
			foreach (var group in groups)
				UpdateGroup(group, enemies, threats);
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
			var eligible = world.Actors.Where(IsEligible).OrderBy(a => a.ActorID).ToList();
			var desired = StealthTankSquadPolicy.SpecialistCount(eligible.Count, Info.ReserveOpeningPair);
			var selected = eligible.Where(a => reserved.Contains(a.ActorID)).Take(desired).ToList();
			selected.AddRange(eligible.Where(a => !reserved.Contains(a.ActorID)).Take(desired - selected.Count));

			var previous = new HashSet<uint>(reserved);
			reserved.Clear();
			foreach (var actor in selected)
				reserved.Add(actor.ActorID);

			foreach (var group in groups)
				group.Units.Clear();
			for (var i = 0; i < selected.Count; i++)
			{
				var groupIndex = StealthTankSquadPolicy.GroupForIndex(i, selected.Count,
					Info.MaximumHarassmentGroups, Info.IncludeAttackGroup);
				if (groupIndex >= 0)
					groups[groupIndex].Units.Add(selected[i]);
			}

			foreach (var group in groups)
				if (group.Target != null && (!group.Target.IsInWorld || group.Target.IsDead ||
					player.RelationshipWith(group.Target.Owner) != PlayerRelationship.Enemy))
					group.Target = null;

			if (Info.DebugLogging && (eligible.Count != lastEligibleCount || !previous.SetEquals(reserved)))
				Log.Write("debug", "AI stealth squads {0} [{1}]: total={2} reserved={3} groups={4} ordinary={5}.",
					Info.SquadLabel, player.PlayerName, eligible.Count, reserved.Count,
					string.Join("/", groups.Select(g => g.Units.Count)), eligible.Count - reserved.Count);

			lastEligibleCount = eligible.Count;
		}

		bool IsEnemyTarget(Actor actor)
		{
			return actor != null && actor.IsInWorld && !actor.IsDead &&
				player.RelationshipWith(actor.Owner) == PlayerRelationship.Enemy &&
				!actor.Info.HasTraitInfo<HuskInfo>() && !actor.GetEnabledTargetTypes().IsEmpty;
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
				Value = Math.Max(1, actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1)
			};
		}

		void UpdateGroup(SpecialistGroup group, List<Actor> enemies, List<Threat> threats)
		{
			group.Units.RemoveAll(a => !IsEligible(a) || !reserved.Contains(a.ActorID));
			if (group.Units.Count == 0)
				return;

			var role = StealthTankSquadPolicy.RoleForGroup(group.Index,
				Info.MaximumHarassmentGroups, Info.IncludeAttackGroup);
			var center = group.Units.Select(a => a.CenterPosition).Average();
			var ownRange = group.Units.SelectMany(a => a.TraitsImplementing<Armament>())
				.Where(a => !a.IsTraitDisabled && a.Weapon.IsValidTarget(GroundTargetTypes))
				.Select(a => (int)Math.Ceiling(a.MaxRange().Length / 1024f)).DefaultIfEmpty(0).Max();
			var squadValue = group.Units.Sum(a => Math.Max(1, a.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1));

			var candidates = enemies.Select(a => new
			{
				Actor = a,
				Priority = Priority(role, a, group.Units.Count),
				Distance = (a.CenterPosition - center).Length / 1024,
				ClusterMultiplier = InfantryClusterMultiplier(role, a)
			}).Where(c => c.Priority > 0)
				.OrderByDescending(c => StealthTankSquadPolicy.TargetScore(c.Priority,
					c.Actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1, c.Distance,
					c.Actor == group.Target ? 100 + Info.TargetSwitchImprovementPercent : 100,
					c.ClusterMultiplier))
				.ThenBy(c => c.Actor.ActorID).Take(Info.MaximumTargetCandidates).ToList();

			Actor selected = null;
			long selectedScore = 0;
			var selectedDanger = 0;
			var dangerousCandidates = 0;
			Actor rejectedTarget = null;
			Actor rejectedBlocker = null;
			foreach (var candidate in candidates)
			{
				var danger = DangerAlongRun(center, candidate.Actor, threats, ownRange,
					role == StealthTankSquadRole.Harass, out var defendingValue, out var strongestDefender);
				if (danger && (role == StealthTankSquadRole.Harass ||
					!StealthTankSquadPolicy.CanCarefullyClear(squadValue, defendingValue, Info.CarefulClearValueRatio)))
				{
					dangerousCandidates++;
					if (rejectedTarget == null)
					{
						rejectedTarget = candidate.Actor;
						rejectedBlocker = strongestDefender;
					}

					continue;
				}

				var score = StealthTankSquadPolicy.TargetScore(candidate.Priority,
					candidate.Actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1, candidate.Distance,
					candidate.Actor == group.Target ? 100 + Info.TargetSwitchImprovementPercent : 100,
					candidate.ClusterMultiplier);
				if (score <= selectedScore)
					continue;

				selected = candidate.Actor;
				selectedScore = score;
				selectedDanger = defendingValue;
			}

			if (selected == null)
			{
				if (Info.DebugLogging && world.WorldTick >= group.LastNoTargetLogTick + Info.ScanInterval * 10)
				{
					group.LastNoTargetLogTick = world.WorldTick;
					Log.Write("debug", "AI stealth squad {0} [{1}:{2}] {3} waiting: units={4} candidates={5} dangerous={6} rejected={7} blocker={8}.",
						Info.SquadLabel, player.PlayerName, group.Index, role, group.Units.Count, candidates.Count, dangerousCandidates,
						rejectedTarget == null ? "none" : rejectedTarget.Info.Name + "#" + rejectedTarget.ActorID,
						rejectedBlocker == null ? "none" : rejectedBlocker.Info.Name + "#" + rejectedBlocker.ActorID);
				}

				group.Target = null;
				group.TargetScore = 0;
				return;
			}

			var changed = selected != group.Target;
			group.Target = selected;
			group.TargetScore = selectedScore;
			if (!changed && world.WorldTick < group.LastOrderTick + Info.OrderInterval)
				return;

			group.LastOrderTick = world.WorldTick;
			var crush = Info.CrushInfantryTargets && role == StealthTankSquadRole.Harass &&
				selected.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes);
			var order = crush ? new Order("Move", null, Target.FromCell(world, selected.Location), false,
				groupedActors: group.Units.ToArray()) :
				new Order("Attack", null, Target.FromActor(selected), false, groupedActors: group.Units.ToArray());
			bot.QueueOrder(order);

			if (Info.DebugLogging)
				Log.Write("debug", "AI stealth squad {0} [{1}:{2}] {3} target {4}#{5}: units={6} score={7} defended-value={8} order={9}.",
					Info.SquadLabel, player.PlayerName, group.Index, role, selected.Info.Name, selected.ActorID, group.Units.Count,
					selectedScore, selectedDanger, crush ? "crush" : "attack");
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
			out int defendingValue, out Actor strongestDefender)
		{
			defendingValue = 0;
			strongestDefender = null;
			var strongestValue = 0;
			var dangerous = false;
			foreach (var threat in threats)
			{
				var detectorRange = StealthTankSquadPolicy.BufferedRange(threat.DetectorRangeCells,
					Info.DetectorRangeBufferCells);
				var ignoreWeapon = stopAtFirstDanger &&
					threat.Actor.GetEnabledTargetTypes().Overlaps(Info.IgnoredHarassmentWeaponThreatTypes);
				var weaponRange = StealthTankSquadPolicy.BufferedRange(ignoreWeapon ? 0 : threat.WeaponRangeCells,
					Info.ThreatRangeBufferCells);
				var targetDistance = (threat.Actor.CenterPosition - target.CenterPosition).Length / 1024;
				var endpointDanger = (detectorRange > 0 && targetDistance <= detectorRange) ||
					(weaponRange > 0 && targetDistance <= weaponRange &&
						(threat.Actor != target || ownRange < threat.WeaponRangeCells + Info.KiteRangeMarginCells));
				var canKiteTarget = threat.Actor == target && detectorRange <= 0 &&
					ownRange >= threat.WeaponRangeCells + Info.KiteRangeMarginCells;

				var routeDanger = SegmentPassesWithin(start, target.CenterPosition, threat.Actor.CenterPosition,
					Math.Max(detectorRange, canKiteTarget ? 0 : weaponRange));
				if (!endpointDanger && !routeDanger)
					continue;

				dangerous = true;
				defendingValue += threat.Value;
				if (threat.Value > strongestValue)
				{
					strongestValue = threat.Value;
					strongestDefender = threat.Actor;
				}

				if (stopAtFirstDanger)
					return true;
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
