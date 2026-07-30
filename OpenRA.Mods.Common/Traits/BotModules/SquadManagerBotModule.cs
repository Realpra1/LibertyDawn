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
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Manages AI squads.")]
	public class SquadManagerBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Actor types that are valid for naval squads.")]
		public readonly HashSet<string> NavalUnitsTypes = new HashSet<string>();

		[Desc("Actor types that are excluded from ground attacks.")]
		public readonly HashSet<string> AirUnitsTypes = new HashSet<string>();

		[Desc("Actor types that should generally be excluded from attack squads.")]
		public readonly HashSet<string> ExcludeFromSquadsTypes = new HashSet<string>();

		[Desc("Actor types that are considered construction yards (base builders).")]
		public readonly HashSet<string> ConstructionYardTypes = new HashSet<string>();

		[Desc("Enemy building types around which to scan for targets for naval squads.")]
		public readonly HashSet<string> NavalProductionTypes = new HashSet<string>();

		[Desc("Own actor types that are prioritized when defending.")]
		public readonly HashSet<string> ProtectionTypes = new HashSet<string>();

		[Desc("Minimum number of units AI must have before attacking.")]
		public readonly int SquadSize = 8;

		[Desc("Random number of up to this many units is added to squad size when creating an attack squad.")]
		public readonly int SquadSizeRandomBonus = 30;

		[Desc("Delay (in ticks) between giving out orders to units.")]
		public readonly int AssignRolesInterval = 50;

		[Desc("Delay (in ticks) between attempting rush attacks.")]
		public readonly int RushInterval = 600;

		[Desc("Delay (in ticks) between updating squads.")]
		public readonly int AttackForceInterval = 75;

		[Desc("Minimum delay (in ticks) between creating squads.")]
		public readonly int MinimumAttackForceDelay = 0;

		[Desc("Radius in cells around enemy BaseBuilder (Construction Yard) where AI scans for targets to rush.")]
		public readonly int RushAttackScanRadius = 15;

		[Desc("Radius in cells around the base that should be scanned for units to be protected.")]
		public readonly int ProtectUnitScanRadius = 15;

		[Desc("Maximum distance in cells from center of the base when checking for MCV deployment location.",
			"Only applies if RestrictMCVDeploymentFallbackToBase is enabled and there's at least one construction yard.")]
		public readonly int MaxBaseRadius = 20;

		[Desc("Radius in cells that squads should scan for enemies around their position while idle.")]
		public readonly int IdleScanRadius = 10;

		[Desc("Radius in cells that squads should scan for danger around their position to make flee decisions.")]
		public readonly int DangerScanRadius = 10;

		[Desc("Radius in cells that attack squads should scan for enemies around their position when trying to attack.")]
		public readonly int AttackScanRadius = 12;

		[Desc("Radius in cells that protecting squads should scan for enemies around their position.")]
		public readonly int ProtectionScanRadius = 8;

		[Desc("Enemy target types to never target.")]
		public readonly BitSet<TargetableType> IgnoredEnemyTargetTypes = default(BitSet<TargetableType>);

		[Desc("Air squads score every enemy actor they find and attack the highest scoring one.",
			"Score awarded to an enemy harvester. Harvesters are the softest worthwhile target,",
			"so this should normally be the highest of the AirTarget*Value fields.")]
		public readonly int AirTargetHarvesterValue = 500;

		[Desc("Score awarded to an enemy production building or refinery.")]
		public readonly int AirTargetProductionValue = 350;

		[Desc("Score awarded to any other enemy building.")]
		public readonly int AirTargetBuildingValue = 150;

		[Desc("Score awarded to an enemy mobile unit.")]
		public readonly int AirTargetUnitValue = 100;

		[Desc("Score deducted per enemy anti-air capable actor sharing a scanned area (DangerScanRadius) with a candidate target.",
			"Raise this to make air squads more scared of SAM sites and mobile AA.")]
		public readonly int AirTargetAntiAirPenalty = 300;

		[Desc("Score deducted per cell of distance between the air squad and a candidate target.")]
		public readonly int AirTargetDistancePenalty = 1;

		[Desc("Minimum score a candidate must reach before an air squad commits to attacking it.",
			"Candidates scoring below this are ignored and the squad stays idle.")]
		public readonly int AirTargetMinimumScore = 1;

		[Desc("Number of map grid cells sampled per air target scan. Bounds the cost of the scan",
			"independently of map size; lowering it trades responsiveness for CPU time on large maps.")]
		public readonly int AirTargetScanSamples = 24;

		[Desc("Extra score for a candidate that has no weapon able to shoot at aircraft.",
			"Aircraft do poor damage to structures, so this is what makes an undefended harvester or tank",
			"outrank a building rather than merely score near it. Zero keeps the stock behaviour.")]
		public readonly int AirTargetDefencelessBonus = 0;

		[Desc("Delay (in ticks) between anti-air safety checks for air squads. Unlike target scoring this",
			"runs regardless of squad state, so aircraft keep watching for anti-air while approaching a",
			"target, while attacking it and on the way home. Zero disables it and restores the stock",
			"behaviour of only checking when a target is selected.")]
		public readonly int AirSafetyCheckInterval = 0;

		[Desc("Radius in cells around an air squad that is scanned for anti-air by the safety check.")]
		public readonly int AirThreatScanRadius = 12;

		[Desc("An air squad retreats when the number of anti-air actors near it, multiplied by this,",
			"exceeds the number of aircraft in the squad. Higher values make air squads more cowardly.")]
		public readonly int AirThreatFleeMultiplier = 3;

		[Desc("How long (in ticks) an air squad remembers where it saw enemy anti-air.")]
		public readonly int AirThreatMemoryTicks = 900;

		[Desc("Maximum number of remembered anti-air sightings per air squad.")]
		public readonly int AirThreatMemorySize = 12;

		[Desc("Anti-air sightings closer together than this many cells are merged into one remembered",
			"threat, so a cluster of SAM sites cannot flood the memory.")]
		public readonly int AirThreatMemoryMergeRadius = 3;

		[Desc("Minimum delay (in ticks) between successive retreat orders for the same air squad.")]
		public readonly int AirRetreatOrderInterval = 50;

		[Desc("Score deducted per known anti-air position within AirRouteThreatRadius of the straight line",
			"an air squad would fly to reach a candidate target. This is what stops squads picking a soft",
			"target on the far side of a SAM belt. Zero disables route scoring.")]
		public readonly int AirRouteThreatPenalty = 0;

		[Desc("Half-width in cells of the flight corridor checked for anti-air by AirRouteThreatPenalty.")]
		public readonly int AirRouteThreatRadius = 8;

		[Desc("Maximum number of aircraft in one air squad. Air squads are a harassment force, not the main",
			"push, so they want to be small. Aircraft beyond the cap join another air squad if one has room",
			"and MaximumAirSquads allows it, otherwise they wait at base until a slot frees up.",
			"Zero means unlimited, which is the stock behaviour.")]
		public readonly int AirSquadSize = 0;

		[Desc("Maximum number of air squads that may exist at once. Zero means unlimited.",
			"Each air squad costs its own target scan and anti-air safety check, so this is the knob that",
			"bounds the CPU cost of air behaviour regardless of how many aircraft the bot builds.")]
		public readonly int MaximumAirSquads = 0;

		[Desc("Distance in cells an air squad hops away from the nearest anti-air it knows about when it",
			"breaks off a run. Small values keep harassment local - the squad slips out, re-scans and comes",
			"straight back in, instead of flying across the map to one of its own buildings.",
			"Zero restores the stock behaviour of retreating to an own building.")]
		public readonly int AirEvadeDistance = 0;

		[Desc("Random lateral spread in cells added to every evasion hop, so successive hops work their way",
			"around the outside of an enemy base instead of shuttling along the same line. It is also the",
			"whole move when the squad has no remembered threat to run from and just wants to re-scan from",
			"somewhere else. Zero disables the wander.")]
		public readonly int AirEvadeJitter = 0;

		[Desc("Multiplier applied to a defender's own anti-air weapon range when deciding whether it is",
			"close enough to count as covering a position. The bot's scans use flat radii unrelated to any",
			"specific weapon's range, so this is what makes the danger zone around a long-range SAM wider",
			"than the zone around a short-range gun, both discovered by the same scan. 1.5 means a squad",
			"treats a defender as dangerous out to 150% of its actual weapon range.")]
		public readonly float AirThreatRangeBuffer = 1.5f;

		[Desc("Consecutive AirIdleState scans (each AttackForceInterval ticks apart) that find no target",
			"scoring above AirTargetMinimumScore before the squad stops waiting for an undefended target",
			"and instead forces an attack on whatever scores best with anti-air and route-threat penalties",
			"relaxed - better than idling forever when the whole enemy base is defended. Zero disables this",
			"and restores the stock behaviour of idling indefinitely.")]
		public readonly int AirMassedAttackIdleThreshold = 0;

		[Desc("An aircraft below this fraction of its maximum health (0-1) breaks off and returns to the",
			"nearest building that can repair its type, the same way SendHomeToResupply already does for",
			"ammo. Zero disables this.")]
		public readonly float HealthRetreatThreshold = 0f;

		[Desc("Per-actor-type target score overrides for Orca-type air squads (squads containing at least",
			"one actor of type OrcaArchetypeActor), keyed by target ActorName - same shape as UnitsToBuild",
			"elsewhere in this mod. Checked before the generic AirTarget*Value classification; actor types",
			"not listed here still fall back to it. Empty disables per-archetype scoring for this squad type.")]
		public readonly Dictionary<string, int> AirTargetPriorityOrca = new Dictionary<string, int>();

		[Desc("As AirTargetPriorityOrca, for Heli-type air squads (squads containing at least one actor of",
			"type HeliArchetypeActor).")]
		public readonly Dictionary<string, int> AirTargetPriorityHeli = new Dictionary<string, int>();

		[Desc("Actor type that identifies a squad as the \"Orca\" archetype for AirTargetPriorityOrca.")]
		public readonly string OrcaArchetypeActor = "orca";

		[Desc("Actor type that identifies a squad as the \"Heli\" archetype for AirTargetPriorityHeli.")]
		public readonly string HeliArchetypeActor = "heli";

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);

			if (DangerScanRadius <= 0)
				throw new YamlException("DangerScanRadius must be greater than zero.");

			if (AirTargetScanSamples <= 0)
				throw new YamlException("AirTargetScanSamples must be greater than zero.");

			if (AirSafetyCheckInterval > 0 && AirThreatScanRadius <= 0)
				throw new YamlException("AirThreatScanRadius must be greater than zero when AirSafetyCheckInterval is set.");

			if (AirThreatFleeMultiplier <= 0)
				throw new YamlException("AirThreatFleeMultiplier must be greater than zero.");

			if (AirThreatMemorySize < 0)
				throw new YamlException("AirThreatMemorySize must not be negative.");

			if (AirRouteThreatPenalty != 0 && AirRouteThreatRadius <= 0)
				throw new YamlException("AirRouteThreatRadius must be greater than zero when AirRouteThreatPenalty is set.");

			if (AirSquadSize < 0)
				throw new YamlException("AirSquadSize must not be negative.");

			if (MaximumAirSquads < 0)
				throw new YamlException("MaximumAirSquads must not be negative.");

			if (AirEvadeDistance < 0)
				throw new YamlException("AirEvadeDistance must not be negative.");

			if (AirEvadeJitter < 0)
				throw new YamlException("AirEvadeJitter must not be negative.");

			if (AirThreatRangeBuffer <= 0)
				throw new YamlException("AirThreatRangeBuffer must be greater than zero.");

			if (AirMassedAttackIdleThreshold < 0)
				throw new YamlException("AirMassedAttackIdleThreshold must not be negative.");

			if (HealthRetreatThreshold < 0 || HealthRetreatThreshold >= 1)
				throw new YamlException("HealthRetreatThreshold must be at least zero and less than one.");
		}

		public override object Create(ActorInitializer init) { return new SquadManagerBotModule(init.Self, this); }
	}

	public class SquadManagerBotModule : ConditionalTrait<SquadManagerBotModuleInfo>, IBotEnabled, IBotTick, IBotRespondToAttack, IBotPositionsUpdated, IGameSaveTraitData
	{
		public CPos GetRandomBaseCenter()
		{
			var randomConstructionYard = World.Actors.Where(a => a.Owner == Player &&
				Info.ConstructionYardTypes.Contains(a.Info.Name))
				.RandomOrDefault(World.LocalRandom);

			return randomConstructionYard?.Location ?? initialBaseCenter;
		}

		public readonly World World;
		public readonly Player Player;

		readonly Predicate<Actor> unitCannotBeOrdered;
		readonly List<Actor> unitsHangingAroundTheBase = new List<Actor>();

		// Units that the bot already knows about. Any unit not on this list needs to be given a role.
		readonly List<Actor> activeUnits = new List<Actor>();

		public List<Squad> Squads = new List<Squad>();

		IBot bot;
		IBotPositionsUpdated[] notifyPositionsUpdated;
		IBotNotifyIdleBaseUnits[] notifyIdleBaseUnits;

		CPos initialBaseCenter;

		int rushTicks;
		int assignRolesTicks;
		int attackForceTicks;
		int minAttackForceDelayTicks;
		int airSafetyTicks;

		public SquadManagerBotModule(Actor self, SquadManagerBotModuleInfo info)
			: base(info)
		{
			World = self.World;
			Player = self.Owner;

			unitCannotBeOrdered = a => a == null || a.Owner != Player || a.IsDead || !a.IsInWorld;
		}

		// Use for proactive targeting.
		public bool IsPreferredEnemyUnit(Actor a)
		{
			if (a == null || a.IsDead || Player.RelationshipWith(a.Owner) != PlayerRelationship.Enemy || a.Info.HasTraitInfo<HuskInfo>() || a.Info.HasTraitInfo<AircraftInfo>())
				return false;

			var targetTypes = a.GetEnabledTargetTypes();
			return !targetTypes.IsEmpty && !targetTypes.Overlaps(Info.IgnoredEnemyTargetTypes);
		}

		public bool IsNotHiddenUnit(Actor a)
		{
			var hasModifier = false;
			var visModifiers = a.TraitsImplementing<IVisibilityModifier>();
			foreach (var v in visModifiers)
			{
				if (v.IsVisible(a, Player))
					return true;

				hasModifier = true;
			}

			return !hasModifier;
		}

		protected override void Created(Actor self)
		{
			notifyPositionsUpdated = self.Owner.PlayerActor.TraitsImplementing<IBotPositionsUpdated>().ToArray();
			notifyIdleBaseUnits = self.Owner.PlayerActor.TraitsImplementing<IBotNotifyIdleBaseUnits>().ToArray();
		}

		protected override void TraitEnabled(Actor self)
		{
			// Avoid all AIs trying to rush in the same tick, randomize their initial rush a little.
			var smallFractionOfRushInterval = Info.RushInterval / 20;
			rushTicks = World.LocalRandom.Next(Info.RushInterval - smallFractionOfRushInterval, Info.RushInterval + smallFractionOfRushInterval);

			// Avoid all AIs reevaluating assignments on the same tick, randomize their initial evaluation delay.
			assignRolesTicks = World.LocalRandom.Next(0, Info.AssignRolesInterval);
			attackForceTicks = World.LocalRandom.Next(0, Info.AttackForceInterval);
			minAttackForceDelayTicks = World.LocalRandom.Next(0, Info.MinimumAttackForceDelay);

			// Spread the air safety checks of all the bots across the interval instead of spiking on one tick.
			if (Info.AirSafetyCheckInterval > 0)
				airSafetyTicks = World.LocalRandom.Next(0, Info.AirSafetyCheckInterval);
		}

		void IBotEnabled.BotEnabled(IBot bot)
		{
			this.bot = bot;
		}

		void IBotTick.BotTick(IBot bot)
		{
			AssignRolesToIdleUnits(bot);
		}

		internal Actor FindClosestEnemy(WPos pos)
		{
			var units = World.Actors.Where(IsPreferredEnemyUnit);
			return units.Where(IsNotHiddenUnit).ClosestTo(pos) ?? units.ClosestTo(pos);
		}

		internal Actor FindClosestEnemy(WPos pos, WDist radius)
		{
			return World.FindActorsInCircle(pos, radius).Where(a => IsPreferredEnemyUnit(a) && IsNotHiddenUnit(a)).ClosestTo(pos);
		}

		void CleanSquads()
		{
			Squads.RemoveAll(s => !s.IsValid);
			foreach (var s in Squads)
				s.Units.RemoveAll(unitCannotBeOrdered);
		}

		// HACK: Use of this function requires that there is one squad of this type.
		Squad GetSquadOfType(SquadType type)
		{
			return Squads.FirstOrDefault(s => s.Type == type);
		}

		/// <summary>
		/// The air squad a newly built aircraft should join, honouring AirSquadSize and MaximumAirSquads.
		/// Returns null when every air squad is full and no more are allowed - the caller then leaves the
		/// aircraft unassigned, so it waits at base and is reconsidered on the next AssignRolesInterval.
		/// Deterministic: Squads is an ordered list and is walked in order.
		/// </summary>
		Squad GetAirSquadWithRoom(IBot bot)
		{
			var squadCount = 0;
			foreach (var s in Squads)
			{
				if (s.Type != SquadType.Air)
					continue;

				squadCount++;
				if (Info.AirSquadSize <= 0 || s.Units.Count < Info.AirSquadSize)
					return s;
			}

			if (Info.MaximumAirSquads > 0 && squadCount >= Info.MaximumAirSquads)
				return null;

			return RegisterNewSquad(bot, SquadType.Air);
		}

		Squad RegisterNewSquad(IBot bot, SquadType type, Actor target = null)
		{
			var ret = new Squad(bot, this, type, target);
			Squads.Add(ret);
			return ret;
		}

		void AssignRolesToIdleUnits(IBot bot)
		{
			CleanSquads();

			activeUnits.RemoveAll(unitCannotBeOrdered);
			unitsHangingAroundTheBase.RemoveAll(unitCannotBeOrdered);
			foreach (var n in notifyIdleBaseUnits)
				n.UpdatedIdleBaseUnits(unitsHangingAroundTheBase);

			if (--rushTicks <= 0)
			{
				rushTicks = Info.RushInterval;
				TryToRushAttack(bot);
			}

			if (--attackForceTicks <= 0)
			{
				attackForceTicks = Info.AttackForceInterval;
				foreach (var s in Squads)
					s.Update();
			}

			// Air squads re-check the anti-air around themselves far more often than the state machine
			// runs, so they can break off a run that has become lethal instead of dying on it.
			// PERF: one bounded circle scan per air squad per interval, and there is at most one air squad.
			if (Info.AirSafetyCheckInterval > 0 && --airSafetyTicks <= 0)
			{
				airSafetyTicks = Info.AirSafetyCheckInterval;
				foreach (var s in Squads)
					s.TickAirSafety();
			}

			if (--assignRolesTicks <= 0)
			{
				assignRolesTicks = Info.AssignRolesInterval;
				FindNewUnits(bot);
			}

			if (--minAttackForceDelayTicks <= 0)
			{
				minAttackForceDelayTicks = Info.MinimumAttackForceDelay;
				CreateAttackForce(bot);
			}
		}

		void FindNewUnits(IBot bot)
		{
			var newUnits = World.ActorsHavingTrait<IPositionable>()
				.Where(a => a.Owner == Player &&
					!Info.ExcludeFromSquadsTypes.Contains(a.Info.Name) &&
					!activeUnits.Contains(a));

			foreach (var a in newUnits)
			{
				if (Info.AirUnitsTypes.Contains(a.Info.Name))
				{
					var air = GetAirSquadWithRoom(bot);

					// Every air squad is full and we may not start another. Leave the aircraft out of
					// activeUnits so it stays at base and is picked up as soon as a slot frees up, rather
					// than oversizing a harassment squad.
					if (air == null)
						continue;

					air.Units.Add(a);
				}
				else if (Info.NavalUnitsTypes.Contains(a.Info.Name))
				{
					var ships = GetSquadOfType(SquadType.Naval);
					if (ships == null)
						ships = RegisterNewSquad(bot, SquadType.Naval);

					ships.Units.Add(a);
				}
				else
					unitsHangingAroundTheBase.Add(a);

				activeUnits.Add(a);
			}

			// Notifying here rather than inside the loop, should be fine and saves a bunch of notification calls
			foreach (var n in notifyIdleBaseUnits)
				n.UpdatedIdleBaseUnits(unitsHangingAroundTheBase);
		}

		void CreateAttackForce(IBot bot)
		{
			// Create an attack force when we have enough units around our base.
			// (don't bother leaving any behind for defense)
			var randomizedSquadSize = Info.SquadSize + World.LocalRandom.Next(Info.SquadSizeRandomBonus);

			if (unitsHangingAroundTheBase.Count >= randomizedSquadSize)
			{
				var attackForce = RegisterNewSquad(bot, SquadType.Assault);

				foreach (var a in unitsHangingAroundTheBase)
					attackForce.Units.Add(a);

				unitsHangingAroundTheBase.Clear();
				foreach (var n in notifyIdleBaseUnits)
					n.UpdatedIdleBaseUnits(unitsHangingAroundTheBase);
			}
		}

		void TryToRushAttack(IBot bot)
		{
			var allEnemyBaseBuilder = AIUtils.FindEnemiesByCommonName(Info.ConstructionYardTypes, Player);

			var ownUnits = activeUnits
				.Where(unit => unit.IsIdle && unit.Info.HasTraitInfo<AttackBaseInfo>()
					&& !Info.AirUnitsTypes.Contains(unit.Info.Name) && !Info.NavalUnitsTypes.Contains(unit.Info.Name) && !Info.ExcludeFromSquadsTypes.Contains(unit.Info.Name)).ToList();

			if (!allEnemyBaseBuilder.Any() || ownUnits.Count < Info.SquadSize)
				return;

			foreach (var b in allEnemyBaseBuilder)
			{
				// Don't rush enemy aircraft!
				var enemies = World.FindActorsInCircle(b.CenterPosition, WDist.FromCells(Info.RushAttackScanRadius))
					.Where(unit => IsPreferredEnemyUnit(unit) && unit.Info.HasTraitInfo<AttackBaseInfo>() && !Info.AirUnitsTypes.Contains(unit.Info.Name) && !Info.NavalUnitsTypes.Contains(unit.Info.Name)).ToList();

				if (AttackOrFleeFuzzy.Rush.CanAttack(ownUnits, enemies))
				{
					var target = enemies.Any() ? enemies.Random(World.LocalRandom) : b;
					var rush = GetSquadOfType(SquadType.Rush);
					if (rush == null)
						rush = RegisterNewSquad(bot, SquadType.Rush, target);

					foreach (var a3 in ownUnits)
						rush.Units.Add(a3);

					return;
				}
			}
		}

		void ProtectOwn(IBot bot, Actor attacker)
		{
			var protectSq = GetSquadOfType(SquadType.Protection);
			if (protectSq == null)
				protectSq = RegisterNewSquad(bot, SquadType.Protection, attacker);

			if (!protectSq.IsTargetValid)
				protectSq.TargetActor = attacker;

			if (!protectSq.IsValid)
			{
				var ownUnits = World.FindActorsInCircle(World.Map.CenterOfCell(GetRandomBaseCenter()), WDist.FromCells(Info.ProtectUnitScanRadius))
					.Where(unit => unit.Owner == Player && !Info.ProtectionTypes.Contains(unit.Info.Name) && unit.Info.HasTraitInfo<AttackBaseInfo>());

				foreach (var a in ownUnits)
					protectSq.Units.Add(a);
			}
		}

		void IBotPositionsUpdated.UpdatedBaseCenter(CPos newLocation)
		{
			initialBaseCenter = newLocation;
		}

		void IBotPositionsUpdated.UpdatedDefenseCenter(CPos newLocation) { }

		void IBotRespondToAttack.RespondToAttack(IBot bot, Actor self, AttackInfo e)
		{
			if (!IsPreferredEnemyUnit(e.Attacker))
				return;

			if (Info.ProtectionTypes.Contains(self.Info.Name))
			{
				foreach (var n in notifyPositionsUpdated)
					n.UpdatedDefenseCenter(e.Attacker.Location);

				ProtectOwn(bot, e.Attacker);
			}
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>()
			{
				new MiniYamlNode("Squads", "", Squads.Select(s => new MiniYamlNode("Squad", s.Serialize())).ToList()),
				new MiniYamlNode("InitialBaseCenter", FieldSaver.FormatValue(initialBaseCenter)),
				new MiniYamlNode("UnitsHangingAroundTheBase", FieldSaver.FormatValue(unitsHangingAroundTheBase
					.Where(a => !unitCannotBeOrdered(a))
					.Select(a => a.ActorID)
					.ToArray())),
				new MiniYamlNode("ActiveUnits", FieldSaver.FormatValue(activeUnits
					.Where(a => !unitCannotBeOrdered(a))
					.Select(a => a.ActorID)
					.ToArray())),
				new MiniYamlNode("RushTicks", FieldSaver.FormatValue(rushTicks)),
				new MiniYamlNode("AssignRolesTicks", FieldSaver.FormatValue(assignRolesTicks)),
				new MiniYamlNode("AttackForceTicks", FieldSaver.FormatValue(attackForceTicks)),
				new MiniYamlNode("MinAttackForceDelayTicks", FieldSaver.FormatValue(minAttackForceDelayTicks)),
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var initialBaseCenterNode = data.FirstOrDefault(n => n.Key == "InitialBaseCenter");
			if (initialBaseCenterNode != null)
				initialBaseCenter = FieldLoader.GetValue<CPos>("InitialBaseCenter", initialBaseCenterNode.Value.Value);

			var unitsHangingAroundTheBaseNode = data.FirstOrDefault(n => n.Key == "UnitsHangingAroundTheBase");
			if (unitsHangingAroundTheBaseNode != null)
			{
				unitsHangingAroundTheBase.Clear();
				unitsHangingAroundTheBase.AddRange(FieldLoader.GetValue<uint[]>("UnitsHangingAroundTheBase", unitsHangingAroundTheBaseNode.Value.Value)
					.Select(a => self.World.GetActorById(a)).Where(a => a != null));
			}

			var activeUnitsNode = data.FirstOrDefault(n => n.Key == "ActiveUnits");
			if (activeUnitsNode != null)
			{
				activeUnits.Clear();
				activeUnits.AddRange(FieldLoader.GetValue<uint[]>("ActiveUnits", activeUnitsNode.Value.Value)
					.Select(a => self.World.GetActorById(a)).Where(a => a != null));
			}

			var rushTicksNode = data.FirstOrDefault(n => n.Key == "RushTicks");
			if (rushTicksNode != null)
				rushTicks = FieldLoader.GetValue<int>("RushTicks", rushTicksNode.Value.Value);

			var assignRolesTicksNode = data.FirstOrDefault(n => n.Key == "AssignRolesTicks");
			if (assignRolesTicksNode != null)
				assignRolesTicks = FieldLoader.GetValue<int>("AssignRolesTicks", assignRolesTicksNode.Value.Value);

			var attackForceTicksNode = data.FirstOrDefault(n => n.Key == "AttackForceTicks");
			if (attackForceTicksNode != null)
				attackForceTicks = FieldLoader.GetValue<int>("AttackForceTicks", attackForceTicksNode.Value.Value);

			var minAttackForceDelayTicksNode = data.FirstOrDefault(n => n.Key == "MinAttackForceDelayTicks");
			if (minAttackForceDelayTicksNode != null)
				minAttackForceDelayTicks = FieldLoader.GetValue<int>("MinAttackForceDelayTicks", minAttackForceDelayTicksNode.Value.Value);

			var squadsNode = data.FirstOrDefault(n => n.Key == "Squads");
			if (squadsNode != null)
			{
				Squads.Clear();
				foreach (var n in squadsNode.Value.Nodes)
					Squads.Add(Squad.Deserialize(bot, this, n.Value));
			}
		}
	}
}
