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
using System.Diagnostics;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Projectiles;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	public enum StealthClearMode { None, Kite, Crush, CrushBridge, Mass }

	#pragma warning disable SA1205
	abstract partial class StealthAIStateBase : StateBase
	#pragma warning restore SA1205
	{
		static readonly BitSet<TargetableType> AirTargetTypes = new BitSet<TargetableType>("Air");

		// BEGIN CNC96A GROUND EXTENSION
		#pragma warning disable SA1512, SA1513, SA1515
		static readonly BitSet<TargetableType> GroundTargetTypes = new BitSet<TargetableType>("Ground");
		static readonly BitSet<TargetableType> InfantryTargetTypes = new BitSet<TargetableType>("Infantry");
		static readonly BitSet<TargetableType> StructureTargetTypes = new BitSet<TargetableType>("Structure");
		// END CNC96A GROUND EXTENSION

		// Bot logic is host-only. Sharing one cache per manager/profile prevents two same-type squads
		// rebuilding the same world influence map during the configured strategic cache interval.
		static readonly Dictionary<SquadManagerBotModule, Dictionary<string, AirInfluenceCache>> InfluenceCaches =
			new Dictionary<SquadManagerBotModule, Dictionary<string, AirInfluenceCache>>();

		protected const int MissileUnitMultiplier = 3;
		const float ApacheMobileThreatMultiplier = 4f;
		const float ApacheStaticThreatMultiplier = 8f;

		/// <summary>True if this actor has a live armament that can shoot at aircraft.</summary>
		protected static bool IsAntiAirCapable(Actor unit)
		{
			if (unit == null || unit.Info.HasTraitInfo<AircraftInfo>())
				return false;

			// PERF: Avoid LINQ.
			foreach (var ab in unit.TraitsImplementing<AttackBase>())
			{
				if (ab.IsTraitDisabled || ab.IsTraitPaused)
					continue;

				foreach (var a in ab.Armaments)
					if (a.Weapon.IsValidTarget(AirTargetTypes))
						return true;
			}

			return false;
		}

		/// <summary>Sum of <see cref="StealthAIThreatGeometry.AaEffectiveness"/> weights, not a raw headcount.</summary>
		protected static float CountAntiAirUnits(Squad owner, IEnumerable<Actor> units)
		{
			var weight = 0f;
			foreach (var unit in units)
			{
				var profile = AntiAirProfile(unit);
				weight += StoppingThreatWeight(owner, unit, profile.Weight);
			}

			return weight;
		}

		static float ConfiguredThreatWeight(Squad owner, Actor actor, float derivedWeight)
		{
			return StealthAIThreatGeometry.ConfiguredThreatWeight(actor.Info.Name, derivedWeight,
				owner.SquadManager.Info.AirThreatWeightOverrides);
		}

		static float StoppingThreatWeight(Squad owner, Actor actor, float derivedWeight)
		{
			var weight = ConfiguredThreatWeight(owner, actor, derivedWeight);
			if (owner.AirProfile.Equals("Apache", StringComparison.OrdinalIgnoreCase) && weight >= .75f)
				weight *= actor.Info.TraitInfoOrDefault<MobileInfo>() == null ?
					ApacheStaticThreatMultiplier : ApacheMobileThreatMultiplier;

			return weight;
		}

		static float AaClearReferenceThreatWeight(Squad owner)
		{
			// Normalize clearing strength against the canonical building SAM. This preserves the authored
			// 10x requirement for a full-strength SAM site, while proportionally reducing the aircraft value
			// needed to clear less effective AA such as an MSAM.
			var samWeight = StealthAIThreatGeometry.ConfiguredThreatWeight("sam", 1f,
				owner.SquadManager.Info.AirThreatWeightOverrides);
			if (owner.AirProfile.Equals("Apache", StringComparison.OrdinalIgnoreCase) && samWeight >= .75f)
				samWeight *= ApacheStaticThreatMultiplier;

			return Math.Max(.01f, samWeight);
		}

		static float TransitThreatWeight(Squad owner, Actor actor,
			(float Weight, float RangeCells, float BaseRangeCells, int FastestProjectileSpeed) profile, int aircraftSpeed)
		{
			var weight = StoppingThreatWeight(owner, actor, profile.Weight);
			if (owner.AirProfile.Equals("Orca", StringComparison.OrdinalIgnoreCase))
				weight = StealthAIThreatGeometry.OrcaTransitThreatWeight(weight,
					StealthAIThreatGeometry.CanOutrun(aircraftSpeed, profile.FastestProjectileSpeed));

			return weight;
		}

		protected static List<Actor> AirDecisionUnits(Squad owner)
		{
			return owner.AirFormationUnits(bootstrapIfEmpty: true);
		}

		static CPos CoarseCell(Squad owner, CPos cell)
		{
			var coarseSize = StealthCoarseSize(owner);
			return new CPos(cell.X / coarseSize, cell.Y / coarseSize);
		}

		static void RecordAirPhase(Squad owner, string phase, long started)
		{
			if (!Game.IsBenchmarking)
				return;

			var elapsed = 1000.0 * Math.Max(0, Stopwatch.GetTimestamp() - started) / Stopwatch.Frequency;
			var category = owner.Type == SquadType.Stealth ? "StealthSquad" : "AirSquad";
			Game.RecordBotModuleSample(owner.Bot.Player.ClientIndex,
				$"{category}/{owner.AirProfile}/{phase}", elapsed, 0);
		}

		static List<CPos> FindAirRoute(Squad owner, float[] danger, int width, int height,
			int startX, int startY, int goalX, int goalY, float dangerCost)
		{
			var started = Stopwatch.GetTimestamp();
			try
			{
				return ThreatAwareRoutePlanner.FindRoute(danger, width, height,
					startX, startY, goalX, goalY, dangerCost);
			}
			finally
			{
				RecordAirPhase(owner, "coarse-route", started);
			}
		}

		protected static void PromoteArrivedAirReinforcements(Squad owner)
		{
			if (owner.AirReinforcements.Count == 0)
				return;

			// Snapshot these before promotion so a newly joined aircraft cannot move the center and cause a
			// chain of increasingly distant reinforcements to join in the same tick.
			var formation = owner.AirFormationUnits();
			CPos? formationCell = formation.Count == 0 ? (CPos?)null :
				CoarseCell(owner, owner.World.Map.CellContaining(owner.AirFormationCenter));
			CPos? destinationCell = owner.IsTargetValid ? CoarseCell(owner, owner.TargetActor.Location) :
				owner.AirTargetStrategicCell;

			var stealthMemberJoined = false;
			foreach (var aircraft in owner.Units)
			{
				if (!owner.AirReinforcements.Contains(aircraft.ActorID) ||
					owner.AirUnitsRepairing.Contains(aircraft.ActorID))
					continue;

				var aircraftCell = CoarseCell(owner, aircraft.Location);
				var nearFormation = formationCell != null &&
					StealthAIThreatGeometry.IsSameOrAdjacentCoarseCell(aircraftCell, formationCell.Value);
				var nearDestination = destinationCell != null &&
					StealthAIThreatGeometry.IsSameOrAdjacentCoarseCell(aircraftCell, destinationCell.Value);
				if (!nearFormation && !nearDestination)
					continue;

				owner.JoinAirFormation(aircraft);
				if (owner.Type == SquadType.Stealth)
				{
					stealthMemberJoined = true;
					if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
						Log.Write("debug", "Stealth specialist claim [{0}] accepted: tick={1} " +
							"member={2}#{3} formation={4} atomic-formation-transfer=pending " +
							"immediate-local-hold=True oldest-due-review=pending.",
							owner.StealthProfile, owner.World.WorldTick,
							aircraft.Info.Name, aircraft.ActorID,
							owner.AirFormationUnits().Count);
				}

				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Air reinforcement [{0}] {1}#{2}: joined formation near {3}; aircraft-cell={4} formation-cell={5} destination-cell={6}.",
						owner.AirProfile, aircraft.Info.Name, aircraft.ActorID,
						nearFormation ? "squad" : "destination", aircraftCell,
						formationCell?.ToString() ?? "none", destinationCell?.ToString() ?? "none");
			}

			if (!stealthMemberJoined)
				return;

			var joinedFormation = owner.AirFormationUnits().Where(unit => !unit.IsDead && unit.IsInWorld)
				.OrderBy(unit => unit.ActorID).ToArray();
			foreach (var member in joinedFormation)
				owner.Bot.QueueOrder(new Order("Stop", member, false));
			owner.AirRoute.Clear();
			owner.AirRouteQueued = false;
			owner.AirNextTargetReviewTick = Math.Min(
				owner.AirNextTargetReviewTick, owner.World.WorldTick);

			var strategicCell = owner.AirTargetStrategicCell;
			if (strategicCell == null && owner.IsTargetValid)
				strategicCell = CoarseCell(owner, owner.TargetActor.Location);
			var joinedCell = joinedFormation.Length == 0 ? (CPos?)null : CoarseCell(owner,
				owner.World.Map.CellContaining(joinedFormation.Select(unit => unit.CenterPosition).Average()));
			var localStage = strategicCell != null && joinedCell != null &&
				StealthAIThreatGeometry.IsSameOrAdjacentCoarseCell(joinedCell.Value, strategicCell.Value);
			var handoff = localStage ? "live-local" : "cached-strategic";
			if (localStage)
				owner.SquadManager.RegisterStealthOwnershipTransferLocalReview(owner);
			else if (strategicCell != null)
				ResumeCachedStealthStrategicRouteAfterJoin(owner, joinedFormation, strategicCell.Value);
			else
				BeginStealthEnemyApproach(owner);
			if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
				Log.Write("debug", "Stealth specialist claim [{0}] atomic ownership transfer: tick={1} " +
					"members=[{2}] stale-pre-claim-movement=cancelled-all " +
					"handoff={3} strategic-cell={4} local-stage={5} " +
					"strategic-authority=cached-influence local-authority=current-live-review.",
					owner.StealthProfile, owner.World.WorldTick, joinedFormation.Select(unit =>
						$"{unit.Info.Name}#{unit.ActorID}@{unit.Location}").JoinWith(","),
					handoff, strategicCell?.ToString() ?? "none", localStage);
		}

		static bool ResumeCachedStealthStrategicRouteAfterJoin(Squad owner,
			IReadOnlyCollection<Actor> joinedFormation, CPos strategicCell)
		{
			var members = joinedFormation.Where(unit => unit != null && !unit.IsDead && unit.IsInWorld)
				.OrderBy(unit => unit.ActorID).ToArray();
			var representative = members.FirstOrDefault();
			var cache = representative == null ? null : StealthInfluence(owner, representative);
			if (cache == null)
				return false;

			var routes = new Dictionary<uint, List<CPos>>();
			foreach (var member in members)
			{
				var route = StealthRouteToCell(owner, member, cache, strategicCell,
					owner.IsTargetValid ? owner.TargetActor : null);
				if (route == null)
					return false;
				routes.Add(member.ActorID, route);
			}

			foreach (var member in members)
			{
				var queued = false;
				foreach (var waypoint in routes[member.ActorID].Where(cell => cell != member.Location))
				{
					owner.Bot.QueueOrder(new Order("Move", member,
						Target.FromCell(owner.World, waypoint), queued));
					queued = true;
				}
			}

			owner.AirRouteQueued = routes.Values.Any(route => route.Count > 0);
			owner.StealthCoreRouteIssues++;
			return true;
		}

		protected static void RoutePendingStealthReinforcements(Squad owner)
		{
			if (owner.Type != SquadType.Stealth)
				return;

			var pending = owner.Units.Where(unit => unit != null && !unit.IsDead && unit.IsInWorld &&
				owner.AirReinforcements.Contains(unit.ActorID) &&
				!owner.AirUnitsRepairing.Contains(unit.ActorID)).OrderBy(unit => unit.ActorID).ToArray();
			var incumbents = owner.AirFormationUnits().Where(unit => !unit.IsDead && unit.IsInWorld)
				.OrderBy(unit => unit.ActorID).ToArray();
			if (pending.Length == 0 || incumbents.Length == 0)
				return;

			QueueStealthReinforcementsToFormation(owner);
			if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
				Log.Write("debug", "Stealth specialist reinforcement catch-up [{0}]: tick={1} " +
					"incumbents=[{2}] pending=[{3}] incumbent-activity=retained " +
					"strategic-position=retained reinforcement-catchup=True.",
					owner.StealthProfile, owner.World.WorldTick,
					incumbents.Select(unit => $"{unit.Info.Name}#{unit.ActorID}@{unit.Location}").JoinWith(","),
					pending.Select(unit => $"{unit.Info.Name}#{unit.ActorID}@{unit.Location}").JoinWith(","));
		}

		internal static void RoutePendingStealthReinforcementsForModularLifecycle(Squad owner)
		{
			RoutePendingStealthReinforcements(owner);
		}

		/// <summary>Largest <see cref="DamageWarhead.Damage"/> among a weapon's warheads, or 0.</summary>
		static int WeaponDamage(WeaponInfo weapon)
		{
			var best = 0;
			foreach (var w in weapon.Warheads)
				if (w is DamageWarhead dw && dw.Damage > best)
					best = dw.Damage;

			return best;
		}

		/// <summary>
		/// Conservative host-side estimate of whether the fully loaded members of a squad can destroy a
		/// target with their current magazines. Partially loaded members contribute nothing instead of
		/// vetoing an otherwise ready formation. This intentionally uses rules data only: target HP and
		/// armor, weapon damage/versus/burst, and actual ammo state. It is used to recognize disposable
		/// AA such as rocket infantry without actor-name exceptions.
		/// </summary>
		static bool CanEliminateWithFullAmmo(IEnumerable<Actor> units, Actor target)
		{
			var health = target.TraitOrDefault<IHealth>();
			if (health == null)
				return false;

			var armorType = target.Info.TraitInfoOrDefault<ArmorInfo>()?.Type;
			long availableDamage = 0;
			var hasAttacker = false;
			foreach (var unit in units)
			{
				if (!CanAttackTarget(unit, target))
					continue;

				var ammoPools = unit.TraitsImplementing<AmmoPool>().ToArray();
				if (!FullAmmo(ammoPools))
					continue;

				var attacks = ammoPools.Length == 0 ? 1 : ammoPools.Min(a => a.CurrentAmmoCount);
				var bestAttackDamage = 0;
				foreach (var armament in unit.TraitsImplementing<Armament>())
				{
					if (armament.IsTraitDisabled || armament.IsTraitPaused ||
						!armament.Weapon.IsValidTarget(target.GetEnabledTargetTypes()))
						continue;

					var attackDamage = 0;
					foreach (var warhead in armament.Weapon.Warheads)
						if (warhead is DamageWarhead damage && damage.Damage > 0)
						{
							var versus = armorType != null && damage.Versus.TryGetValue(armorType, out var modifier) ?
								modifier : 100;
							attackDamage += damage.Damage * versus / 100;
						}

					bestAttackDamage = Math.Max(bestAttackDamage, attackDamage * armament.Weapon.Burst);
				}

				if (bestAttackDamage <= 0)
					continue;

				hasAttacker = true;
				availableDamage += (long)bestAttackDamage * attacks;
			}

			// Leave margin for misses, overkill between aircraft, and conditional damage modifiers.
			return hasAttacker && availableDamage >= health.HP * 2L;
		}

		/// <summary>
		/// Conservative time-to-kill estimate using current magazines. The two-times damage margin matches
		/// <see cref="CanEliminateWithFullAmmo"/> and covers misses, overkill and conditional modifiers.
		/// Returning <see cref="long.MaxValue"/> means the current force cannot reliably finish the set in
		/// one exposed operation.
		/// </summary>
		static long EstimatedKillTicks(IEnumerable<Actor> units, IEnumerable<Actor> targets)
		{
			long totalTicks = 0;
			foreach (var target in targets.Distinct().OrderBy(a => a.ActorID))
			{
				var health = target.TraitOrDefault<IHealth>();
				if (health == null || health.HP <= 0)
					continue;

				var armorType = target.Info.TraitInfoOrDefault<ArmorInfo>()?.Type;
				long availableDamage = 0;
				var damagePerTick = 0d;
				foreach (var unit in units)
				{
					if (!CanAttackTarget(unit, target))
						continue;

					var pools = unit.TraitsImplementing<AmmoPool>().ToArray();
					var attacks = pools.Length == 0 ? int.MaxValue : pools.Min(a => a.CurrentAmmoCount);
					if (attacks <= 0)
						continue;

					var bestVolleyDamage = 0;
					var bestDamagePerTick = 0d;
					foreach (var armament in unit.TraitsImplementing<Armament>())
					{
						if (armament.IsTraitDisabled || armament.IsTraitPaused ||
							!armament.Weapon.IsValidTarget(target.GetEnabledTargetTypes()))
							continue;

						var shotDamage = 0;
						foreach (var warhead in armament.Weapon.Warheads)
							if (warhead is DamageWarhead damage && damage.Damage > 0)
							{
								var versus = armorType != null && damage.Versus.TryGetValue(armorType, out var modifier) ?
									modifier : 100;
								shotDamage += damage.Damage * versus / 100;
							}

						var volleyDamage = shotDamage * armament.Weapon.Burst;
						var burstDelay = 0;
						if (armament.Weapon.Burst > 1 && armament.Weapon.BurstDelays.Length > 0)
							burstDelay = armament.Weapon.BurstDelays.Length == 1 ?
								armament.Weapon.BurstDelays[0] * (armament.Weapon.Burst - 1) :
								armament.Weapon.BurstDelays.Sum();

						var cycleTicks = Math.Max(1, armament.Weapon.ReloadDelay + burstDelay);
						var rate = volleyDamage / (double)cycleTicks;
						if (rate <= bestDamagePerTick)
							continue;

						bestDamagePerTick = rate;
						bestVolleyDamage = volleyDamage;
					}

					if (bestVolleyDamage <= 0)
						continue;

					damagePerTick += bestDamagePerTick;
					if (availableDamage != long.MaxValue)
					{
						var contribution = (long)bestVolleyDamage * attacks;
						availableDamage = attacks == int.MaxValue || long.MaxValue - availableDamage < contribution ?
							long.MaxValue : availableDamage + contribution;
					}
				}

				var requiredDamage = health.HP * 2L;
				if (damagePerTick <= 0 || availableDamage < requiredDamage)
					return long.MaxValue;

				var targetTicks = (long)Math.Ceiling(requiredDamage / damagePerTick);
				if (long.MaxValue - totalTicks < targetTicks)
					return long.MaxValue;

				totalTicks += targetTicks;
			}

			return Math.Max(1, totalTicks);
		}

		/// <summary>Average magazine fullness in the range 0..1. Units without ammo pools count as ready.</summary>
		static float AmmoReadiness(IEnumerable<Actor> units)
		{
			var total = 0f;
			var count = 0;
			foreach (var unit in units)
			{
				var pools = unit.TraitsImplementing<AmmoPool>().ToArray();
				if (pools.Length == 0)
				{
					total++;
					count++;
					continue;
				}

				foreach (var pool in pools)
				{
					total += pool.Info.Ammo <= 0 ? 1f : pool.CurrentAmmoCount / (float)pool.Info.Ammo;
					count++;
				}
			}

			return count == 0 ? 1f : total / count;
		}

		static float UnitAmmoReadiness(Actor unit)
		{
			var pools = unit.TraitsImplementing<AmmoPool>().ToArray();
			if (pools.Length == 0)
				return 1f;

			var total = 0f;
			foreach (var pool in pools)
				total += pool.Info.Ammo <= 0 ? 1f : pool.CurrentAmmoCount / (float)pool.Info.Ammo;

			return total / pools.Length;
		}

		static int EconomicValue(Actor actor)
		{
			return Math.Max(1, actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1);
		}

		static long AmmoWeightedSquadValue(IEnumerable<Actor> units)
		{
			long value = 0;
			foreach (var unit in units)
				value += (long)(EconomicValue(unit) * UnitAmmoReadiness(unit));

			return value;
		}

		static bool IsCoveredByAa(Actor target,
			IEnumerable<(Actor Actor, float StoppingWeight, int RangeCells)> threats)
		{
			foreach (var threat in threats)
			{
				if (threat.Actor.IsDead || threat.Actor == target)
					continue;

				var distance = (threat.Actor.CenterPosition - target.CenterPosition).Length / 1024;
				if (distance <= threat.RangeCells)
					return true;
			}

			return false;
		}

		/// <summary>Inaccuracy of a weapon's projectile, or 0 when the projectile type carries none.</summary>
		static int WeaponInaccuracy(WeaponInfo weapon)
		{
			switch (weapon.Projectile)
			{
				case MissileInfo missile: return missile.Inaccuracy.Length;
				case BulletInfo bullet: return bullet.Inaccuracy.Length;
				default: return 0;
			}
		}

		/// <summary>
		/// Top speed (WDist/tick, same scale as <c>AircraftInfo.Speed</c>) of a weapon's projectile, or
		/// <see cref="int.MaxValue"/> when the projectile type is not one this bot knows how to read a
		/// speed from - treated as "cannot outrun" rather than silently granting outrun immunity against a
		/// threat it cannot actually judge.
		/// </summary>
		static int WeaponProjectileSpeed(WeaponInfo weapon)
		{
			switch (weapon.Projectile)
			{
				case MissileInfo missile: return missile.Speed.Length;
				case BulletInfo bullet:
				{
					var fastest = 0;
					foreach (var s in bullet.Speed)
						if (s.Length > fastest)
							fastest = s.Length;

					return fastest;
				}

				default: return int.MaxValue;
			}
		}

		/// <summary>
		/// A unit's anti-air capability in one pass over its armaments: how dangerous it really is (see
		/// <see cref="StealthAIThreatGeometry.AaEffectiveness"/>), the range of its AA weapon in cells, and the
		/// fastest projectile speed it can threaten an aircraft with. Zero-weight (not anti-air capable at
		/// all, or an aircraft itself) short-circuits to zero ranges and <see cref="int.MaxValue"/> speed.
		/// </summary>
		protected static (float Weight, float RangeCells, float BaseRangeCells, int FastestProjectileSpeed) AntiAirProfile(Actor unit)
		{
			if (unit == null || unit.Info.HasTraitInfo<AircraftInfo>())
				return (0, 0, 0, int.MaxValue);

			Armament bestAa = null;
			Armament bestPrimary = null;

			// PERF: Avoid LINQ.
			foreach (var ab in unit.TraitsImplementing<AttackBase>())
			{
				if (ab.IsTraitDisabled || ab.IsTraitPaused)
					continue;

				foreach (var a in ab.Armaments)
				{
					if (a.Weapon.IsValidTarget(AirTargetTypes))
					{
						if (bestAa == null || a.MaxRange() > bestAa.MaxRange())
							bestAa = a;
					}
					else if (bestPrimary == null || a.MaxRange() > bestPrimary.MaxRange())
						bestPrimary = a;
				}
			}

			if (bestAa == null)
				return (0, 0, 0, int.MaxValue);

			var weight = bestPrimary == null
				? 1f
				: StealthAIThreatGeometry.AaEffectiveness(
					WeaponInaccuracy(bestAa.Weapon), WeaponDamage(bestAa.Weapon),
					WeaponInaccuracy(bestPrimary.Weapon), WeaponDamage(bestPrimary.Weapon));

			return (weight, bestAa.MaxRange().Length / 1024f, bestAa.Weapon.Range.Length / 1024f,
				WeaponProjectileSpeed(bestAa.Weapon));
		}

		enum AirTargetClass { Unit, Wall, Building, Production, Harvester }

		// BEGIN CNC96A GROUND EXTENSION
		static readonly Dictionary<SquadManagerBotModule, Dictionary<string, StealthInfluenceCache>>
			StealthInfluenceCaches =
				new Dictionary<SquadManagerBotModule, Dictionary<string, StealthInfluenceCache>>();

		internal static void PrimeStealthInfluenceForTest(Squad owner)
		{
			if (StealthInfluenceCaches.TryGetValue(owner.SquadManager, out var profiles))
				profiles.Remove(owner.StealthProfile);
			var representative = owner.AirFormationUnits(bootstrapIfEmpty: true)
				.OrderBy(a => a.ActorID).FirstOrDefault();
			if (representative != null)
				StealthInfluence(owner, representative);
		}
		// END CNC96A GROUND EXTENSION

		protected static AirTargetPlan FindDefenselessTarget(Squad owner)
		{
			return FindBestAirTarget(owner);
		}

		static int BaseTargetUtility(Actor actor, SquadManagerBotModuleInfo info,
			Dictionary<string, int> archetypePriority, float antiAirWeight, int? priorityOverride = null)
		{
			var value = (long)Math.Max(1, priorityOverride ?? TargetValue(actor, info, archetypePriority));
			var valued = actor.Info.TraitInfoOrDefault<ValuedInfo>();
			if (valued != null)
				value = value * (100 + Math.Min(valued.Cost / 100, 100)) / 100;

			value = value * 100 / (100 + (int)(antiAirWeight * info.AirTargetAntiAirPenalty));
			return Math.Max(1, (int)Math.Min(int.MaxValue, value));
		}

		static int CurrentTargetUtility(Actor actor, int baseUtility)
		{
			var health = actor.TraitOrDefault<IHealth>();
			return health == null
				? baseUtility
				: StealthAIThreatGeometry.RemainingHealthPriority(baseUtility, health.HP, health.MaxHP);
		}

		static int BoundedStealthTargetUtility(Actor actor, int baseUtility)
		{
			var health = actor.TraitOrDefault<IHealth>();
			if (health == null || health.MaxHP <= 0)
				return baseUtility;

			var remainingHp = Math.Clamp(health.HP, 1, health.MaxHP);
			return (int)Math.Min(int.MaxValue,
				(long)baseUtility * Math.Min(health.MaxHP, remainingHp * 4L) / remainingHp);
		}

		// BEGIN CNC96A GROUND EXTENSION
		protected static int StealthPriority(Squad owner, Actor actor)
		{
			var definition = owner.StealthDefinition;
			if (definition == null)
				return 0;

			var targetTypes = actor.GetEnabledTargetTypes();
			var attackRole = definition.IncludeAttackGroup &&
				owner.StealthSquadIndex == definition.MaximumHarassmentGroups;
			var configured = attackRole ? definition.AttackTargetPriorities :
				definition.HarassmentTargetPriorities;
			if (configured.TryGetValue(actor.Info.Name, out var priority))
				return priority;
			if (definition.HarvesterTypes.Contains(actor.Info.Name) || actor.Info.HasTraitInfo<HarvesterInfo>())
				return 5000;
			if (!attackRole && targetTypes.Overlaps(definition.ExcludedHarassmentTargetTypes))
				return 0;
			if (actor.Info.HasTraitInfo<LineBuildNodeInfo>())
				return 1;

			switch (actor.Info.Name)
			{
				case "mcv":
				case "amcv": return 5000;
				case "fact":
				case "proc":
				case "nuk2":
				case "hq":
				case "eye":
				case "tmpl": return 2500;
				case "nuke":
				case "arty":
				case "mlrs": return 1250;
			}

			if (targetTypes.Overlaps(InfantryTargetTypes))
				return 1;
			if (targetTypes.Overlaps(StructureTargetTypes))
			{
				var armor = actor.Info.TraitInfoOrDefault<ArmorInfo>()?.Type;
				if (armor != null && definition.HarassmentArmorPriorities.TryGetValue(armor, out priority))
					return priority;
				return 600;
			}

			return actor.Info.HasTraitInfo<MobileInfo>() ? 100 : 0;
		}

		static List<GroundThreat> StealthThreats(Squad owner)
		{
			var threats = new List<GroundThreat>();
			var representative = AirDecisionUnits(owner).OrderBy(a => a.ActorID).FirstOrDefault();
			foreach (var actor in owner.World.Actors.Where(owner.SquadManager.IsPreferredEnemyUnit)
				.OrderBy(a => a.ActorID))
			{
				var weaponRange = actor.TraitsImplementing<Armament>()
					.Where(a => !a.IsTraitDisabled && a.Weapon.IsValidTarget(GroundTargetTypes))
					.Select(a => (int)Math.Ceiling(a.MaxRange().Length / 1024f)).DefaultIfEmpty().Max();
				var detectorRange = actor.TraitsImplementing<DetectCloaked>().Where(d => !d.IsTraitDisabled)
					.Select(d => (int)Math.Ceiling(d.Range.Length / 1024f)).DefaultIfEmpty().Max();
				var canonicalThreat = 0d;
				if (representative != null)
					owner.SquadManager.CombatThreatCalculator.TryGetDefenderThreat(
						representative, actor, out canonicalThreat);
				if (weaponRange > 0 || detectorRange > 0)
					threats.Add(new GroundThreat
					{
						Actor = actor,
						WeaponRange = weaponRange,
						DetectorRange = detectorRange,
						Speed = actor.TraitOrDefault<Mobile>()?.MovementSpeedForCell(actor, actor.Location) ?? 0,
						CanonicalThreat = canonicalThreat
					});
			}

			return threats;
		}

		static bool ThreatCoversPosition(GroundThreat threat, WPos position, bool weapon, int buffer = 0)
		{
			if (threat.Actor.IsDead || !threat.Actor.IsInWorld)
				return false;

			var range = weapon ? threat.WeaponRange : threat.DetectorRange;
			return range > 0 && (threat.Actor.CenterPosition - position).HorizontalLength <=
				WDist.FromCells(range + Math.Max(0, buffer)).Length;
		}

		protected static bool RevealedAttackPositionIsCovered(Actor target, IEnumerable<GroundThreat> threats,
			Actor ignoredThreat = null)
		{
			return threats.Any(t => t.Actor != ignoredThreat && ThreatCoversPosition(t, target.CenterPosition, true));
		}

		static bool CachedThreatCoversReveal(Squad owner, GroundThreat threat, WPos position,
			Actor ignoredThreat = null)
		{
			if (threat.Actor == ignoredThreat)
				return false;

			var definition = owner.StealthDefinition;
			return ThreatCoversPosition(threat, position, true, definition.ThreatRangeBufferCells);
		}

		static string CachedRevealThreatSummary(IEnumerable<GroundThreat> threats, Actor ignoredThreat = null)
		{
			var active = threats.Where(t => t.Actor != ignoredThreat && !t.Actor.IsDead && t.Actor.IsInWorld)
				.OrderBy(t => t.Actor.ActorID).ToList();
			return string.Format("count={0} facts={1}", active.Count, string.Join(",", active.Take(8)
				.Select(t => string.Format("{0}#{1}:weapon={2}:detector={3}",
					t.Actor.Info.Name, t.Actor.ActorID, t.WeaponRange, t.DetectorRange))));
		}

		static bool LiveKiteThreatCoversPosition(Squad owner, Actor unit, Actor target,
			WPos position, GroundThreat threat)
		{
			if (threat.Actor == target)
				return false;

			var definition = owner.StealthDefinition;
			if (ThreatCoversPosition(threat, position, false, definition.DetectorRangeBufferCells))
				return true;

			if (!ThreatCoversPosition(threat, position, true, definition.ThreatRangeBufferCells))
				return false;

			var distance = (threat.Actor.CenterPosition - position).HorizontalLength / 1024d;
			var livePair = owner.SquadManager.CombatThreatCalculator.CalculateLive(
				unit, threat.Actor, GroundTargetTypes, true);
			var canonicalThreat = GeneralizedCombatThreatCalculator.DefenderThreatAtDistance(
				livePair, distance);
			return canonicalThreat > 0;
		}

		static bool LiveKitePositionIsCovered(Squad owner, Actor unit, Actor target, WPos position)
		{
			return LiveHostileGroundThreats(owner).Any(threat =>
				LiveKiteThreatCoversPosition(owner, unit, target, position, threat));
		}

		static bool PlannedDecloakThreatCoversPosition(Squad owner, Actor unit,
			WPos position, GroundThreat threat)
		{
			if (ThreatCoversPosition(threat, position, false,
				owner.StealthDefinition.DetectorRangeBufferCells))
				return true;

			if (!ThreatCoversPosition(threat, position, true))
				return false;

			var distance = (threat.Actor.CenterPosition - position).HorizontalLength / 1024d;
			var livePair = owner.SquadManager.CombatThreatCalculator.CalculateLive(
				unit, threat.Actor, GroundTargetTypes, true);
			return GeneralizedCombatThreatCalculator.DefenderThreatAtDistance(livePair, distance) > 0;
		}

		static bool LivePlannedDecloakThreatCoversPosition(Squad owner, Actor unit,
			WPos position, out Actor coveringThreat)
		{
			coveringThreat = null;
			if (unit == null || unit.IsDead || !unit.IsInWorld)
				return false;

			foreach (var actor in owner.World.Actors.Where(actor => actor != null && !actor.IsDead &&
				actor.IsInWorld && actor.OccupiesSpace != null &&
				owner.SquadManager.IsNotHiddenUnit(actor) && actor.AppearsHostileTo(unit))
				.OrderBy(actor => actor.ActorID))
			{
				var distance = (actor.CenterPosition - position).HorizontalLength / 1024d;
				var livePair = owner.SquadManager.CombatThreatCalculator.CalculateLive(
					unit, actor, GroundTargetTypes, true);
				if (GeneralizedCombatThreatCalculator.DefenderThreatAtDistance(
					livePair, distance) <= 0)
					continue;

				coveringThreat = actor;
				return true;
			}

			return false;
		}

		protected static bool ShouldWithholdLivePlannedDecloakEngagement(Squad owner, Actor unit,
			CPos? validatedFiringCell, out Actor coveringThreat, out string reason)
		{
			var currentCellCovered = LivePlannedDecloakThreatCoversPosition(
				owner, unit, unit.CenterPosition, out coveringThreat);
			var reachedValidatedFiringCell = validatedFiringCell == null ||
				unit.Location == validatedFiringCell.Value;
			var withhold = StealthAISpecialistPolicy.ShouldWithholdLivePlannedDecloakEngagement(
				currentCellCovered, validatedFiringCell != null, reachedValidatedFiringCell);
			reason = currentCellCovered ? "current-cell-live-planned-decloak-threat" :
				!reachedValidatedFiringCell ? "validated-firing-cell-not-reached" : "approved";
			return withhold;
		}

		static bool CurrentActivityIsAttack(Actor unit)
		{
			if (unit == null || unit.IsIdle || unit.CurrentActivity == null)
				return false;

			var type = unit.CurrentActivity.GetType();
			return type == typeof(Attack) || type == typeof(FlyAttack);
		}

		protected static bool CancelUnsafeLivePlannedDecloakContinuation(Squad owner,
			IEnumerable<Actor> formation)
		{
			foreach (var unit in formation.Where(unit => unit != null && !unit.IsDead && unit.IsInWorld &&
				!owner.AirUnitsRepairing.Contains(unit.ActorID)).OrderBy(unit => unit.ActorID))
			{
				var revealedQueuedAttack = unit.TraitsImplementing<Cloak>().Any(cloak => !cloak.Cloaked) &&
					BusyAttack(unit);
				if (!CurrentActivityIsAttack(unit) && !revealedQueuedAttack)
					continue;

				owner.StealthValidatedFiringCells.TryGetValue(unit.ActorID, out var firingCell);
				var hasFiringCell = owner.StealthValidatedFiringCells.ContainsKey(unit.ActorID);
				if (!ShouldWithholdLivePlannedDecloakEngagement(owner, unit,
					hasFiringCell ? firingCell : (CPos?)null,
					out var coveringThreat, out var reason))
					continue;

				owner.Bot.QueueOrder(new Order("Stop", unit, false));
				owner.AirRouteQueued = false;
				owner.AirNextTargetReviewTick = Math.Min(
					owner.AirNextTargetReviewTick, owner.World.WorldTick);
				if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth live planned-decloak engagement veto [{0}] tick={1}: " +
						"phase=continuation unit={2}#{3} unit-cell={4} target={5}#{6} " +
						"validated-firing-cell={7} covering-threat={8} reason={9} " +
						"combat-order=cancel safe-recalculation=immediate.", owner.StealthProfile,
						owner.World.WorldTick, unit.Info.Name, unit.ActorID, unit.Location,
						owner.TargetActor?.Info.Name ?? "none", owner.TargetActor?.ActorID ?? 0,
						hasFiringCell ? firingCell.ToString() : "current-live-approved",
						coveringThreat == null ? "none" : coveringThreat.Info.Name + "#" +
							coveringThreat.ActorID, reason);
				return true;
			}

			return false;
		}

		protected static bool HoldUnsafeClaimedStealthApproach(Squad owner,
			IReadOnlyCollection<Actor> formation)
		{
			if (owner.Type != SquadType.Stealth || owner.StealthClearMode != StealthClearMode.None ||
				!owner.IsTargetValid || !owner.AirRouteQueued || formation.Count == 0)
				return false;

			var liveThreats = LiveHostileGroundThreats(owner);
			var held = false;
			foreach (var unit in formation.Where(actor => actor.IsInWorld && !actor.IsDead &&
				actor.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked)))
			{
				var mobile = unit.TraitOrDefault<Mobile>();
				if (mobile == null || mobile.ToCell == unit.Location)
					continue;

				var nextPosition = owner.World.Map.CenterOfCell(mobile.ToCell);
				var covering = liveThreats.Where(threat =>
					PlannedDecloakThreatCoversPosition(owner, unit, nextPosition, threat)).ToList();
				if (covering.Count == 0)
					continue;

				owner.Bot.QueueOrder(new Order("Stop", unit, false));
				held = true;
				if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth pre-dispatch safety hold [{0}] tick={1}: unit={2}#{3} " +
						"cell={4} next-cell={5} cloaked=True threats=[{6}] " +
						"authority=live-standard-current-range immediate-arbitration=True.",
						owner.StealthProfile, owner.World.WorldTick, unit.Info.Name, unit.ActorID,
						unit.Location, mobile.ToCell, covering.Select(threat => string.Format(
							"{0}#{1}@{2}", threat.Actor.Info.Name, threat.Actor.ActorID,
							threat.Actor.Owner.InternalName)).JoinWith(","));
			}

			if (held)
			{
				owner.AirRouteQueued = false;
				owner.AirNextTargetReviewTick = Math.Min(
					owner.AirNextTargetReviewTick, owner.World.WorldTick);
			}

			return held;
		}

		static string LiveKiteCoveringThreatSummary(Squad owner, IReadOnlyCollection<Actor> formation,
			Actor target, WPos position)
		{
			return LiveHostileGroundThreats(owner).Where(threat => formation.Any(unit =>
				LiveKiteThreatCoversPosition(owner, unit, target, position, threat)))
				.Select(threat => string.Format("{0}#{1}@{2}:cell={3}", threat.Actor.Info.Name,
					threat.Actor.ActorID, threat.Actor.Owner.InternalName, threat.Actor.Location))
				.JoinWith(",");
		}

		static List<CPos> LiveKiteFiringRoute(Squad owner, IReadOnlyCollection<Actor> formation,
			Actor target, CPos firingCell)
		{
			var representative = formation.OrderBy(actor => actor.ActorID).FirstOrDefault();
			var mobile = representative?.TraitOrDefault<Mobile>();
			if (mobile == null)
				return null;

			var reversed = representative.Location == firingCell ? new List<CPos> { firingCell } :
				mobile.Pathfinder.FindUnitPath(representative.Location, firingCell, representative,
					null, BlockedByActor.Immovable);
			if (reversed.Count == 0)
				return null;

			var route = StealthAISpecialistPolicy.ForwardExactGroundRoute(reversed);
			if (route.Count == 0 || route[route.Count - 1] != firingCell)
				route.Add(firingCell);

			var liveThreats = LiveHostileGroundThreats(owner);
			var detectorBuffer = owner.StealthDefinition.DetectorRangeBufferCells;
			if (route.Any(cell => liveThreats.Any(threat => ThreatCoversPosition(threat,
				owner.World.Map.CenterOfCell(cell), false, detectorBuffer))))
				return null;

			return route;
		}

		static List<GroundThreat> CachedPackageThreats(StealthInfluenceCache cache,
			IEnumerable<Actor> package)
		{
			var threats = new List<GroundThreat>();
			foreach (var actor in package)
				if (cache.ThreatByActor.TryGetValue(actor, out var threat))
					threats.Add(threat);
			return threats;
		}

		static CPos? SafeOrdinaryFiringCell(Squad owner, Actor representative,
			StealthInfluenceCache cache, Actor target)
		{
			var range = GroundWeaponRange(representative, target);
			var mobile = representative.TraitOrDefault<Mobile>();
			if (range <= 0 || mobile == null)
				return null;

			var localThreats = CachedPackageThreats(cache, DefenderPackage(owner, cache, target));
			var targetThreat = localThreats.FirstOrDefault(threat => threat.Actor == target);
			var outrangesTarget = targetThreat != null &&
				StealthAISpecialistPolicy.CanOutrangeUndetectingTarget(
					targetThreat.WeaponRange, targetThreat.DetectorRange, range);
			var minimumRange = outrangesTarget ? targetThreat.WeaponRange + 1 : 1;
			var coarseSize = StealthCoarseSize(owner);
			return owner.World.Map.FindTilesInAnnulus(target.Location, minimumRange, range)
				.Where(c => mobile.CanEnterCell(c, null, BlockedByActor.Immovable))
				.Where(c => !outrangesTarget || targetThreat == null ||
					StealthAIThreatGeometry.IsOutsideWeaponRange(owner.World.Map.Clamp(new CPos(
						c.X / coarseSize * coarseSize + coarseSize / 2,
						c.Y / coarseSize * coarseSize + coarseSize / 2)),
						target.Location, targetThreat.WeaponRange))
				.Where(c => !localThreats.Any(t => CachedThreatCoversReveal(
					owner, t, owner.World.Map.CenterOfCell(c), outrangesTarget ? target : null)))
				.OrderBy(c => (c - representative.Location).LengthSquared)
				.ThenBy(c => c.Y).ThenBy(c => c.X).Cast<CPos?>().FirstOrDefault();
		}

		static bool CoveringWeaponAt(Squad owner, StealthInfluenceCache cache, WPos position,
			Actor ignoredThreat = null)
		{
			return cache.Threats.Any(threat => CachedThreatCoversReveal(
				owner, threat, position, ignoredThreat));
		}

		static CPos? SafePostAttackStrategicCell(Squad owner, Actor representative,
			StealthInfluenceCache cache, CPos firingCell)
		{
			var mobile = representative.TraitOrDefault<Mobile>();
			if (mobile == null)
				return null;

			var size = StealthCoarseSize(owner);
			var origin = CoarseCell(owner, firingCell);
			return Enumerable.Range(-1, 3).SelectMany(dy => Enumerable.Range(-1, 3)
				.Select(dx => new CPos(origin.X + dx, origin.Y + dy)))
				.Where(coarse => coarse != origin && coarse.X >= 0 && coarse.Y >= 0 &&
					coarse.X < cache.Width && coarse.Y < cache.Height)
				.Select(coarse => owner.World.Map.Clamp(new CPos(
					coarse.X * size + size / 2, coarse.Y * size + size / 2)))
				.Where(cell => mobile.CanEnterCell(cell, null, BlockedByActor.Immovable) &&
					!CoarseCellHasForbiddenResource(owner, CoarseCell(owner, cell), true) &&
					!CoveringWeaponAt(owner, cache, owner.World.Map.CenterOfCell(cell)))
				.OrderBy(cell => (cell - firingCell).LengthSquared)
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).Cast<CPos?>().FirstOrDefault();
		}

		static List<GroundThreat> LiveHostileGroundThreats(Squad owner)
		{
			var activeMembers = AirDecisionUnits(owner).Where(actor => !actor.IsDead && actor.IsInWorld).ToArray();
			return owner.World.Actors.Where(actor => actor != null && !actor.IsDead && actor.IsInWorld &&
				actor.OccupiesSpace != null && owner.SquadManager.IsNotHiddenUnit(actor) &&
				activeMembers.Any(actor.AppearsHostileTo))
				.Select(LiveGroundThreat)
				.Where(threat => threat.WeaponRange > 0 || threat.DetectorRange > 0)
				.OrderBy(threat => threat.Actor.ActorID).ToList();
		}

		static bool OrdinaryCrushExposureIsSafe(Squad owner,
			Actor target, CPos? nextStrategicCell)
		{
			return OrdinaryCrushExposureIsSafe(owner, target, nextStrategicCell,
				out _, out _, out _);
		}

		static bool OrdinaryCrushExposureIsSafe(Squad owner,
			Actor target, CPos? nextStrategicCell, out bool formationCloaked,
			out bool targetDetectorCovered, out bool nextCellDetectorCovered)
		{
			formationCloaked = owner.AirFormationUnits(bootstrapIfEmpty: true)
				.Where(unit => !unit.IsDead && unit.IsInWorld)
				.All(unit => unit.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked));
			targetDetectorCovered = false;
			nextCellDetectorCovered = false;
			if (nextStrategicCell == null)
				return false;

			var size = StealthCoarseSize(owner);
			var next = owner.World.Map.Clamp(new CPos(
				nextStrategicCell.Value.X * size + size / 2,
				nextStrategicCell.Value.Y * size + size / 2));
			var liveThreats = LiveHostileGroundThreats(owner);
			targetDetectorCovered = liveThreats.Any(threat => ThreatCoversPosition(
				threat, target.CenterPosition, false, owner.StealthDefinition.DetectorRangeBufferCells));
			nextCellDetectorCovered = liveThreats.Any(threat => ThreatCoversPosition(
				threat, owner.World.Map.CenterOfCell(next), false,
				owner.StealthDefinition.DetectorRangeBufferCells));
			return StealthAISpecialistPolicy.CloakedCrushExposureIsSafe(
				formationCloaked, targetDetectorCovered, nextCellDetectorCovered);
		}

		static bool CloakedCrushRouteIsSafe(Squad owner,
			IEnumerable<CPos> route)
		{
			var formationCloaked = owner.AirFormationUnits(bootstrapIfEmpty: true)
				.Where(unit => !unit.IsDead && unit.IsInWorld)
				.All(unit => unit.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked));
			var liveThreats = LiveHostileGroundThreats(owner);
			var detectorCoverage = route?.Select(cell => liveThreats.Any(threat =>
				ThreatCoversPosition(threat, owner.World.Map.CenterOfCell(cell), false,
					owner.StealthDefinition.DetectorRangeBufferCells)));
			return StealthAISpecialistPolicy.CloakedCrushRouteIsSafe(
				formationCloaked, detectorCoverage);
		}

		protected static bool OrdinaryAttackExposureIsSafe(Squad owner, StealthInfluenceCache cache,
			Actor unit, Actor target, CPos? nextCell)
		{
			if (cache == null || nextCell == null)
				return false;

			var targetThreat = cache.ThreatByActor.TryGetValue(target, out var threat) &&
				StealthAISpecialistPolicy.CanOutrangeUndetectingTarget(
					threat.WeaponRange, threat.DetectorRange, GroundWeaponRange(unit, target)) ? target : null;
			return StealthAISpecialistPolicy.PlannedExposureIsSafe(
				CoveringWeaponAt(owner, cache, unit.CenterPosition, targetThreat),
				!CoveringWeaponAt(owner, cache, owner.World.Map.CenterOfCell(nextCell.Value)), false);
		}

		static void LogDirectSafeRouteEvidence(Squad owner, StealthInfluenceCache cache,
			Actor target, CPos firingCell, IReadOnlyList<CPos> route, string phase)
		{
			if (!owner.SquadManager.Info.AirTargetDebugLogging ||
				owner.StealthProfile != "stealth-tank")
				return;

			var targetThreat = cache.ThreatByActor.TryGetValue(target, out var threat) ? threat : null;
			var weaponRange = targetThreat?.WeaponRange ?? 0;
			var weaponRangeSquared = weaponRange * weaponRange;
			var waypointFacts = route.Take(16).Select(cell =>
			{
				var distanceSquared = (cell - target.Location).LengthSquared;
				return string.Format("{0}:distance-squared={1}:outside={2}", cell,
					distanceSquared, distanceSquared > weaponRangeSquared);
			}).ToArray();
			var allOutside = route.All(cell =>
				(cell - target.Location).LengthSquared > weaponRangeSquared);
			var minimumDistanceSquared = route.Select(cell =>
				(cell - target.Location).LengthSquared).DefaultIfEmpty(int.MaxValue).Min();

			Log.Write("debug", "Stealth direct safe route [{0}] tick={1}: phase={2} target={3}#{4} " +
				"target-cell={5} target-coarse={6} firing-cell={7} direct-coarse={8} " +
				"weapon-range={9} weapon-range-squared={10} route-waypoints={11} " +
				"minimum-distance-squared={12} all-outside={13} members={14} waypoint-facts=[{15}].",
				owner.StealthProfile, owner.World.WorldTick, phase, target.Info.Name, target.ActorID, target.Location,
				CoarseCell(owner, target.Location), firingCell, CoarseCell(owner, firingCell),
				weaponRange, weaponRangeSquared, route.Count, minimumDistanceSquared, allOutside,
				string.Join(",", owner.AirFormationUnits().OrderBy(unit => unit.ActorID).Select(unit =>
					string.Format("stnk#{0}:cell={1}:cloaked={2}", unit.ActorID, unit.Location,
						unit.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked)))),
				string.Join(",", waypointFacts));
		}

		static int CurrentGroundSpeed(Actor actor)
		{
			var mobile = actor.TraitOrDefault<Mobile>();
			return mobile?.MovementSpeedForCell(actor, actor.Location) ?? 0;
		}

		protected static int GroundWeaponRange(Actor actor, Actor target)
		{
			var types = target.GetEnabledTargetTypes();
			return actor.TraitsImplementing<Armament>()
				.Where(a => !a.IsTraitDisabled && !a.IsTraitPaused && a.Weapon.IsValidTarget(types))
				.Select(a => (int)Math.Floor(a.MaxRange().Length / 1024f)).DefaultIfEmpty().Max();
		}

		static GroundThreat LiveGroundThreat(Actor actor)
		{
			return new GroundThreat
			{
				Actor = actor,
				WeaponRange = actor.TraitsImplementing<Armament>()
					.Where(a => !a.IsTraitDisabled && !a.IsTraitPaused &&
						a.Weapon.IsValidTarget(GroundTargetTypes))
					.Select(a => (int)Math.Ceiling(a.MaxRange().Length / 1024f)).DefaultIfEmpty().Max(),
				DetectorRange = actor.TraitsImplementing<DetectCloaked>().Where(d => !d.IsTraitDisabled)
					.Select(d => (int)Math.Ceiling(d.Range.Length / 1024f)).DefaultIfEmpty().Max(),
				Speed = CurrentGroundSpeed(actor)
			};
		}

		static long RouteTravelMilliseconds(Squad owner, Actor unit, IReadOnlyList<CPos> route, Actor target)
		{
			var speed = CurrentGroundSpeed(unit);
			if (speed <= 0)
				return long.MaxValue;

			long distance = 0;
			var position = unit.CenterPosition;
			foreach (var waypoint in route)
			{
				var next = owner.World.Map.CenterOfCell(waypoint);
				distance += (next - position).HorizontalLength;
				position = next;
			}

			distance += (target.CenterPosition - position).HorizontalLength;
			return distance * owner.World.Timestep / speed;
		}

		static long StealthMissionServiceMilliseconds(Squad owner, Actor representative,
			IEnumerable<Actor> formation, AirTargetPlan plan)
		{
			if (plan?.Route == null)
				return long.MaxValue;

			var travel = RouteTravelMilliseconds(owner, representative, plan.Route, plan.Actor);
			var killTicks = EstimatedKillTicks(formation, new[] { plan.Actor });
			var service = killTicks == long.MaxValue || travel == long.MaxValue ? long.MaxValue :
				Math.Min(long.MaxValue, travel + killTicks * owner.World.Timestep);
			return StealthAISpecialistPolicy.CachedMobileServiceMilliseconds(service,
				owner.World.Timestep, StealthAISpecialistPolicy.KillCadenceFinishMarginTicks(
					owner.SquadManager.Info.AirInfluenceCacheInterval,
					owner.SquadManager.Info.AirTargetStallTicks),
				plan.Actor.Info.HasTraitInfo<MobileInfo>());
		}

		static bool IsLiveLocalCombatActor(Squad owner, IReadOnlyCollection<Actor> activeMembers, Actor actor)
		{
			if (actor == null || actor.IsDead || !actor.IsInWorld ||
				actor.OccupiesSpace == null || !owner.SquadManager.IsNotHiddenUnit(actor) ||
				!activeMembers.Any(actor.AppearsHostileTo))
				return false;

			var threat = LiveGroundThreat(actor);
			return StealthPriority(owner, actor) > 0 || threat.WeaponRange > 0 || threat.DetectorRange > 0;
		}

		static List<Actor> LiveDefenderPackage(Squad owner, CPos center)
		{
			var coarseSize = StealthCoarseSize(owner);
			var activeMembers = AirDecisionUnits(owner).Where(actor => !actor.IsDead && actor.IsInWorld)
				.OrderBy(actor => actor.ActorID).ToArray();
			var actors = owner.World.Actors.Where(actor => IsLiveLocalCombatActor(owner, activeMembers, actor))
				.Where(actor =>
				{
					var cell = new CPos(actor.Location.X / coarseSize, actor.Location.Y / coarseSize);
					return Math.Abs(cell.X - center.X) <= 1 && Math.Abs(cell.Y - center.Y) <= 1;
				})
				.OrderByDescending(actor => StealthPriority(owner, actor))
				.ThenBy(actor => actor.ActorID).ToList();
			RecordStealthLiveLocalDiagnostic(owner, center, activeMembers, actors);
			return actors;
		}

		static void RecordStealthLiveLocalDiagnostic(Squad owner, CPos center,
			IReadOnlyCollection<Actor> activeMembers, IReadOnlyCollection<Actor> actors)
		{
			if (!owner.SquadManager.Info.AirTargetDebugLogging && !Game.Settings.Debug.BotDebug)
				return;

			var signature = 17;
			unchecked
			{
				signature = signature * 31 + center.GetHashCode();
				foreach (var member in activeMembers)
					signature = signature * 31 + (int)member.ActorID;
				foreach (var actor in actors)
				{
					signature = signature * 31 + (int)actor.ActorID;
					signature = signature * 31 + actor.Owner.ClientIndex;
					signature = signature * 31 + StealthPriority(owner, actor);
				}
			}

			owner.StealthLiveLocalDiagnosticSamples++;
			var first = !owner.StealthLiveLocalDiagnosticHasSignature;
			var changed = !first && owner.StealthLiveLocalDiagnosticSignature != signature;
			if (changed)
				owner.StealthLiveLocalDiagnosticChanges++;
			owner.StealthLiveLocalDiagnosticHasSignature = true;
			owner.StealthLiveLocalDiagnosticSignature = signature;
			if (owner.StealthLiveLocalDiagnosticNextSummaryTick < 0)
				owner.StealthLiveLocalDiagnosticNextSummaryTick = owner.World.WorldTick + 250;
			var periodic = owner.World.WorldTick >= owner.StealthLiveLocalDiagnosticNextSummaryTick;
			if (!first && !changed && !periodic)
				return;

			owner.StealthLiveLocalDiagnosticEmitted++;
			var diagnosticAttribution = SquadManagerBotModule.BeginStealthManagerAttributionPhase();
			try
			{
				Log.Write("debug", "Stealth live local package [{0}] tick={1}: center={2} " +
					"members=[{3}] actors=[{4}] membership=live-hostile-visible-spatial priority=live " +
					"diagnostic-trigger={5}.", owner.StealthProfile, owner.World.WorldTick, center,
					activeMembers.Select(actor => actor.Info.Name + "#" + actor.ActorID).JoinWith(","),
					actors.Select(actor => string.Format("{0}#{1}@{2}:priority={3}", actor.Info.Name,
						actor.ActorID, actor.Owner.InternalName, StealthPriority(owner, actor))).JoinWith(","),
					first ? "first" : changed ? "change" : "periodic");
				if (Game.Settings.Debug.BotDebug)
					owner.SquadManager.AddStealthManagerAttributionOperations(
						StealthManagerAttributionPhase.DiagnosticEmission, 1);
			}
			finally
			{
				owner.SquadManager.RecordStealthManagerAttributionPhase(
					StealthManagerAttributionPhase.DiagnosticEmission,
					diagnosticAttribution, 0);
			}
			if (!periodic)
				return;

			owner.StealthLiveLocalDiagnosticNextSummaryTick = owner.World.WorldTick + 250;
			EmitStealthLiveLocalDiagnosticSummary(owner, "periodic");
		}

		static List<Actor> DefenderPackage(Squad owner, StealthInfluenceCache cache, Actor wanted)
		{
			var coarseSize = StealthCoarseSize(owner);
			var center = new CPos(wanted.Location.X / coarseSize, wanted.Location.Y / coarseSize);
			return LiveDefenderPackage(owner, center);
		}

		static List<Actor> DefenderPackage(StealthInfluenceCache cache, CPos center)
		{
			var actors = new List<Actor>();
			for (var dy = -1; dy <= 1; dy++)
				for (var dx = -1; dx <= 1; dx++)
					if (cache.EnemyActorsByCell.TryGetValue(new CPos(center.X + dx, center.Y + dy), out var cellActors))
						actors.AddRange(cellActors.Where(a => !a.IsDead && a.IsInWorld));

			return actors.Distinct().OrderBy(a => a.ActorID).ToList();
		}

		static int PackageSignature(IEnumerable<Actor> ours, IEnumerable<Actor> theirs)
		{
			unchecked
			{
				var hash = 17;
				foreach (var actor in ours.Concat(theirs).OrderBy(a => a.ActorID))
					hash = hash * 31 + (int)actor.ActorID;
				return hash;
			}
		}

		static List<Actor> LiveLatchedDefenderPackage(Squad owner)
		{
			if (owner.StealthClearCenterCell == null)
				return new List<Actor>();

			// Package ids preserve the selected engagement's intent across ticks and saves, but
			// membership and priority always come from actors that are live in the local world now.
			return LiveDefenderPackage(owner, owner.StealthClearCenterCell.Value);
		}

		static GeneralizedCombatThreatCalculator.GroupTypeCount[] ThreatGroup(IEnumerable<Actor> actors)
		{
			return actors.GroupBy(a => a.Info.Name, StringComparer.OrdinalIgnoreCase)
				.Select(g => new GeneralizedCombatThreatCalculator.GroupTypeCount(g.Key, g.Count(),
					g.Select(a => a.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0).DefaultIfEmpty().Max()))
				.ToArray();
		}

		static double CrossoverOvermatch(Squad owner, IEnumerable<Actor> ours, IEnumerable<Actor> theirs)
		{
			var ourActors = ours.Where(actor => actor != null && !actor.IsDead && actor.IsInWorld)
				.OrderBy(actor => actor.ActorID).ToArray();
			var theirActors = theirs.Where(actor => actor != null && !actor.IsDead && actor.IsInWorld)
				.OrderBy(actor => actor.ActorID).ToArray();
			var ourGroup = ThreatGroup(ourActors);
			var theirGroup = ThreatGroup(theirActors);
			if (ourGroup.Length == 0 || theirGroup.Length == 0)
				return 0;
			var crossover = owner.SquadManager.CombatThreatCalculator
				.EstimateLiveMixedGroupCrossover(ourActors, theirActors,
					GroundTargetTypes, true);
			var ourCount = ourGroup.Sum(g => (long)g.Count);
			var theirCount = theirGroup.Sum(g => (long)g.Count);
			if (!double.IsFinite(crossover))
				return 0;
			return crossover > 0 ? ourCount / (theirCount * crossover) : -1;
		}

		static double ThreatValue(Squad owner, IReadOnlyList<Actor> ours, Actor enemy)
		{
			return ours.Where(actor => actor != null && !actor.IsDead && actor.IsInWorld)
				.Sum(actor => owner.SquadManager.CombatThreatCalculator.CalculateLive(
					actor, enemy, GroundTargetTypes, true).DefenderThreatInAttackerEquivalents);
		}

		static Actor HighestThreatActor(Squad owner, IReadOnlyList<Actor> ours, IEnumerable<Actor> theirs)
		{
			return theirs.Where(enemy => ours.Any(unit => CanAttackTarget(unit, enemy)))
				.Select(enemy => (Actor: enemy, Threat: ThreatValue(owner, ours, enemy)))
				.OrderByDescending(x => x.Threat)
				.ThenBy(x => x.Actor.ActorID)
				.Select(x => x.Actor).FirstOrDefault();
		}

		protected static int StealthCoarseSize(Squad owner)
		{
			return Math.Max(1, owner.StealthDefinition?.StrategicCellSize ??
				StealthAISpecialistPolicy.RequiredStrategicCellSize);
		}

		protected static int StealthSwitchImprovement(Squad owner)
		{
			return owner.StealthDefinition?.TargetSwitchImprovementPercent ??
				owner.SquadManager.Info.AirTargetSwitchImprovementPercent;
		}

		static void MarkStealthRange(Squad owner, float[] danger, int width, int height,
			int coarseSize, GroundThreat threat, int range, float influence,
			Dictionary<CPos, List<GroundThreat>> coverage)
		{
			if (range <= 0 || influence <= 0)
				return;

			var actor = threat.Actor;
			var conservativeRange = range + coarseSize;
			var minX = Math.Max(0, (actor.Location.X - conservativeRange) / coarseSize);
			var maxX = Math.Min(width - 1, (actor.Location.X + conservativeRange) / coarseSize);
			var minY = Math.Max(0, (actor.Location.Y - conservativeRange) / coarseSize);
			var maxY = Math.Min(height - 1, (actor.Location.Y + conservativeRange) / coarseSize);
			var rangeLength = conservativeRange * 1024;
			for (var y = minY; y <= maxY; y++)
				for (var x = minX; x <= maxX; x++)
				{
					var cell = owner.World.Map.Clamp(new CPos(x * coarseSize + coarseSize / 2,
						y * coarseSize + coarseSize / 2));
					if ((owner.World.Map.CenterOfCell(cell) - actor.CenterPosition).Length <= rangeLength)
					{
						danger[y * width + x] += influence;
						var coarse = new CPos(x, y);
						if (!coverage.TryGetValue(coarse, out var threats))
							coverage.Add(coarse, threats = new List<GroundThreat>());
						if (!threats.Contains(threat))
							threats.Add(threat);
					}
				}
		}

		protected static StealthInfluenceCache StealthInfluence(Squad owner, Actor unit)
		{
			var definition = owner.StealthDefinition;
			var mobile = unit.TraitOrDefault<Mobile>();
			if (definition == null || mobile == null)
				return null;

			var map = owner.World.Map;
			var coarseSize = StealthCoarseSize(owner);
			var width = (map.MapSize.X + coarseSize - 1) / coarseSize;
			var height = (map.MapSize.Y + coarseSize - 1) / coarseSize;
			if (!StealthInfluenceCaches.TryGetValue(owner.SquadManager, out var profileCaches))
			{
				profileCaches = new Dictionary<string, StealthInfluenceCache>();
				StealthInfluenceCaches.Add(owner.SquadManager, profileCaches);
			}

			var locomotor = unit.Info.TraitInfoOrDefault<MobileInfo>()?.Locomotor ?? "ground";
			var cacheKey = owner.StealthProfile + ":" + locomotor;
			var info = owner.SquadManager.Info;
			if (profileCaches.TryGetValue(cacheKey, out var cached) && cached.Width == width &&
				cached.Height == height && owner.World.WorldTick - cached.Tick < info.AirInfluenceCacheInterval)
			{
				RecordAirPhase(owner, "influence-cache-hit", Stopwatch.GetTimestamp());
				return cached;
			}

			var started = Stopwatch.GetTimestamp();
			var weaponDanger = new float[width * height];
			var detectorCoverage = new float[width * height];
			var mobilityDanger = new float[width * height];
			var candidates = new List<(Actor Actor, int Priority)>();
			var threatFacts = StealthThreats(owner);
			var threatCoverage = new Dictionary<CPos, List<GroundThreat>>();
			foreach (var threat in threatFacts)
			{
				var ignoreWeapon = threat.Actor.GetEnabledTargetTypes()
					.Overlaps(definition.IgnoredHarassmentWeaponThreatTypes);
				MarkStealthRange(owner, detectorCoverage, width, height, coarseSize, threat,
					StealthAISpecialistPolicy.BufferedRange(threat.DetectorRange,
						definition.DetectorRangeBufferCells),
					StealthAISpecialistPolicy.HardDetectorRouteInfluence, threatCoverage);
				MarkStealthRange(owner, weaponDanger, width, height, coarseSize, threat,
					StealthAISpecialistPolicy.BufferedRange(ignoreWeapon ? 0 : threat.WeaponRange,
						definition.ThreatRangeBufferCells),
					(float)threat.CanonicalThreat, threatCoverage);
			}

			foreach (var actor in owner.World.Actors)
			{
				if (!owner.SquadManager.IsPreferredEnemyUnit(actor) ||
					!owner.SquadManager.IsNotHiddenUnit(actor))
					continue;

				var priority = StealthPriority(owner, actor);
				if (priority > 0)
					candidates.Add((actor, priority));
			}

			var enemyActorsByCell = candidates.Select(c => c.Actor)
				.Concat(threatFacts.Select(t => t.Actor)).Distinct()
				.GroupBy(a => new CPos(a.Location.X / coarseSize, a.Location.Y / coarseSize))
				.ToDictionary(g => g.Key, g => g.OrderBy(a => a.ActorID).ToList());
			var pendingExplosionCells = new HashSet<CPos>();
			var resourceLayer = owner.World.WorldActor.TraitOrDefault<IResourceLayer>();
			for (var y = 0; y < height; y++)
				for (var x = 0; x < width; x++)
					{
						var cell = map.Clamp(new CPos(x * coarseSize + coarseSize / 2,
							y * coarseSize + coarseSize / 2));
						var index = y * width + x;
						if (!mobile.CanExistInCell(cell))
						{
							mobilityDanger[index] += StealthAISpecialistPolicy.HardRouteDangerThreshold;
						}
						if (resourceLayer == null)
							continue;

						var hasBlue = false;
						var hasRed = false;
						var hasPending = false;
						for (var oy = 0; oy < coarseSize; oy++)
							for (var ox = 0; ox < coarseSize; ox++)
							{
								var exact = new CPos(x * coarseSize + ox, y * coarseSize + oy);
								if (!map.Contains(exact))
									continue;
								var resourceType = resourceLayer.GetResource(exact).Type;
								hasBlue |= resourceType == "BlueTiberium";
								hasRed |= resourceType == "RedTiberium";
								hasPending |= resourceLayer.IsExplosionPending(exact);
							}

						if (hasRed || hasPending)
						{
							mobilityDanger[index] += StealthAISpecialistPolicy.HardRouteDangerThreshold;
						}
						else if (hasBlue)
						{
							mobilityDanger[index] += StealthAISpecialistPolicy.SoftResourceRouteCost;
						}
						if (hasPending)
							pendingExplosionCells.Add(new CPos(x, y));
					}

			var exposedDanger = new float[mobilityDanger.Length];
			var cloakedDanger = new float[mobilityDanger.Length];
			for (var i = 0; i < mobilityDanger.Length; i++)
			{
				exposedDanger[i] = StealthAISpecialistPolicy.CloakAwareRouteDanger(
					mobilityDanger[i], weaponDanger[i], detectorCoverage[i] > 0, false);
				cloakedDanger[i] = StealthAISpecialistPolicy.CloakAwareRouteDanger(
					mobilityDanger[i], weaponDanger[i], detectorCoverage[i] > 0, true);
			}

			cached = new StealthInfluenceCache
			{
				Tick = owner.World.WorldTick,
				Width = width,
				Height = height,
				Danger = exposedDanger,
				CloakedDanger = cloakedDanger,
				MobilityDanger = mobilityDanger,
				Candidates = candidates,
				Threats = threatFacts,
				ThreatByActor = threatFacts.ToDictionary(t => t.Actor),
				EnemyActorsByCell = enemyActorsByCell,
				ThreatCoverageByCell = threatCoverage,
				PendingExplosionCells = pendingExplosionCells,
			};
			profileCaches[cacheKey] = cached;
			RecordAirPhase(owner, "influence-build", started);
			if (info.AirTargetDebugLogging)
				Log.Write("debug", "Stealth influence [{0}] rebuilt: coarse={1} size={2}x{3} " +
					"candidates={4} threats={5}.", owner.StealthProfile, coarseSize,
					width, height, candidates.Count, threatFacts.Count);

			return cached;
		}

		static StealthInfluenceCache CachedStealthInfluence(Squad owner, Actor unit)
		{
			var mobile = unit.TraitOrDefault<Mobile>();
			if (owner.StealthDefinition == null || mobile == null ||
				!StealthInfluenceCaches.TryGetValue(owner.SquadManager, out var profileCaches))
				return null;

			var map = owner.World.Map;
			var coarseSize = StealthCoarseSize(owner);
			var width = (map.MapSize.X + coarseSize - 1) / coarseSize;
			var height = (map.MapSize.Y + coarseSize - 1) / coarseSize;
			var locomotor = unit.Info.TraitInfoOrDefault<MobileInfo>()?.Locomotor ?? "ground";
			var cacheKey = owner.StealthProfile + ":" + locomotor;
			return profileCaches.TryGetValue(cacheKey, out var cached) && cached.Width == width &&
				cached.Height == height ? cached : null;
		}

		static List<CPos> StealthRouteToCell(Squad owner, Actor unit,
			StealthInfluenceCache cache, CPos goalCell, Actor directTarget = null)
		{
			var cloaked = unit.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked);
			var danger = cloaked ? cache?.CloakedDanger : cache?.Danger;
			return StealthRouteToCell(owner, unit, cache, goalCell, danger, false, directTarget);
		}

		static List<CPos> StealthRouteToCell(Squad owner, Actor unit,
			StealthInfluenceCache cache, CPos goalCell, float[] danger, bool allowDangerousStart = false,
			Actor directTarget = null)
		{
			var definition = owner.StealthDefinition;
			if (definition == null || cache == null || danger == null)
				return null;

			var directThreat = directTarget != null &&
				cache.ThreatByActor.TryGetValue(directTarget, out var foundDirectThreat) &&
				StealthAISpecialistPolicy.CanOutrangeUndetectingTarget(
					foundDirectThreat.WeaponRange, foundDirectThreat.DetectorRange,
					GroundWeaponRange(unit, directTarget)) ? foundDirectThreat : null;
			if (directThreat != null)
			{
				danger = (float[])danger.Clone();
				var directCoarseSize = StealthCoarseSize(owner);
				for (var y = 0; y < cache.Height; y++)
					for (var x = 0; x < cache.Width; x++)
					{
						var waypoint = owner.World.Map.Clamp(new CPos(
							x * directCoarseSize + directCoarseSize / 2,
							y * directCoarseSize + directCoarseSize / 2));
						if (!StealthAIThreatGeometry.IsOutsideWeaponRange(waypoint,
							directTarget.Location, directThreat.WeaponRange))
							danger[y * cache.Width + x] = Math.Max(danger[y * cache.Width + x],
								StealthAISpecialistPolicy.HardRouteDangerThreshold);
					}
			}

			var coarseSize = StealthCoarseSize(owner);
			var startX = Math.Clamp(unit.Location.X / coarseSize, 0, cache.Width - 1);
			var startY = Math.Clamp(unit.Location.Y / coarseSize, 0, cache.Height - 1);
			var goalX = Math.Clamp(goalCell.X, 0, cache.Width - 1);
			var goalY = Math.Clamp(goalCell.Y, 0, cache.Height - 1);
			var route = FindAirRoute(owner, danger, cache.Width, cache.Height,
				startX, startY, goalX, goalY, definition.RouteThreatPenalty);
			if (route == null || route.Skip(allowDangerousStart ? 1 : 0).Any(cell => StealthAISpecialistPolicy.IsHardRouteDanger(
				danger[cell.Y * cache.Width + cell.X])))
				return null;

			return ThreatAwareRoutePlanner.SmoothRoute(danger, cache.Width, cache.Height,
				startX, startY, route).Select(cell => owner.World.Map.Clamp(new CPos(
					cell.X * coarseSize + coarseSize / 2,
					cell.Y * coarseSize + coarseSize / 2))).ToList();
		}

		static List<CPos> BuildValidatedFiringRoute(StealthInfluenceCache cache, Actor target,
			CPos firingCell, Func<IReadOnlyList<CPos>> coarseRouteBuilder)
		{
			if (cache.ThreatByActor.TryGetValue(target, out var threat) &&
				StealthAISpecialistPolicy.CanOutrangeUndetectingTarget(
					threat.WeaponRange, threat.DetectorRange, int.MaxValue))
				return StealthAIThreatGeometry.BuildDirectSafeFiringRoute(coarseRouteBuilder,
					firingCell, target.Location, threat.WeaponRange);

			var coarseRoute = coarseRouteBuilder?.Invoke();
			if (coarseRoute == null)
				return null;

			var route = new List<CPos>(coarseRoute);
			if (route.Count == 0 || route[route.Count - 1] != firingCell)
				route.Add(firingCell);
			return route;
		}

		protected static List<CPos> MassClearRoute(Squad owner, Actor unit, Actor target)
		{
			var mobile = unit?.TraitOrDefault<Mobile>();
			var range = unit == null || target == null ? 0 : GroundWeaponRange(unit, target);
			if (mobile == null || range <= 0)
				return null;

			foreach (var firingCell in owner.World.Map.FindTilesInAnnulus(target.Location, 1, range)
				.Where(cell => mobile.CanEnterCell(cell, null, BlockedByActor.Immovable))
				.OrderBy(cell => (cell - unit.Location).LengthSquared)
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X))
			{
				var reversed = unit.Location == firingCell ? new List<CPos> { firingCell } :
					mobile.Pathfinder.FindUnitPath(unit.Location, firingCell, unit,
						null, BlockedByActor.Immovable);
				if (reversed.Count == 0)
					continue;

				var route = StealthAISpecialistPolicy.ForwardExactGroundRoute(reversed);
				if (route.Count == 0 || route[route.Count - 1] != firingCell)
					route.Add(firingCell);
				return route;
			}

			return null;
		}

		static List<CPos> SafeRouteForStealth(Squad owner, Actor unit, Actor target)
		{
			if (target == null)
				return null;

			var cache = StealthInfluence(owner, unit);
			var coarseSize = StealthCoarseSize(owner);
			return StealthRouteToCell(owner, unit, cache,
				new CPos(target.Location.X / coarseSize, target.Location.Y / coarseSize));
		}

		static CPos? NearestSafeStealthNeighbor(Squad owner, Actor representative,
			StealthInfluenceCache cache, bool nearestCell = false, CPos? originCell = null,
			IReadOnlyList<CPos> approachTargets = null)
		{
			var map = owner.World.Map;
			var mobile = representative.TraitOrDefault<Mobile>();
			var resourceLayer = owner.World.WorldActor.TraitOrDefault<IResourceLayer>();
			if (mobile == null)
				return null;

			var coarseSize = StealthCoarseSize(owner);
			var current = originCell ?? new CPos(representative.Location.X / coarseSize,
				representative.Location.Y / coarseSize);
			CPos? best = null;
			var bestDanger = float.MaxValue;
			var bestThreatDistance = long.MinValue;
			var bestTargetDistance = long.MaxValue;
			var originTargetDistance = StealthAIThreatGeometry.MinimumCellSeparationSquared(
				current, approachTargets);
			var debugCandidates = owner.SquadManager.Info.AirTargetDebugLogging ?
				new List<(CPos Cell, CPos Destination, long Travel, float Danger, long Clearance)>() : null;
			for (var dy = -1; dy <= 1; dy++)
				for (var dx = -1; dx <= 1; dx++)
				{
					if (dx == 0 && dy == 0)
						continue;

					var coarse = new CPos(current.X + dx, current.Y + dy);
					if (coarse.X < 0 || coarse.Y < 0 || coarse.X >= cache.Width || coarse.Y >= cache.Height)
						continue;

					var destination = nearestCell ? Enumerable.Range(0, coarseSize)
						.SelectMany(y => Enumerable.Range(0, coarseSize).Select(x => new CPos(
							coarse.X * coarseSize + x, coarse.Y * coarseSize + y)))
						.Where(map.Contains).Where(c => mobile.CanEnterCell(c, null, BlockedByActor.Immovable))
						.OrderBy(c => (representative.Location - c).LengthSquared).FirstOrDefault() :
						map.Clamp(new CPos(coarse.X * coarseSize + coarseSize / 2,
							coarse.Y * coarseSize + coarseSize / 2));
					if (destination == default(CPos) || !map.Contains(destination) ||
						!StealthAISpecialistPolicy.DestinationBelongsToStrategicCell(
							destination.X, destination.Y, coarseSize, coarse.X, coarse.Y))
						continue;
					if (!mobile.CanEnterCell(destination, null, BlockedByActor.Immovable))
						continue;

					var resource = resourceLayer?.GetResource(destination).Type;
					if (resource == "BlueTiberium" || resource == "RedTiberium" ||
						CoarseCellHasForbiddenResource(owner, coarse, true))
						continue;

					var danger = cache.Danger[coarse.Y * cache.Width + coarse.X];
					if (StealthAISpecialistPolicy.IsHardRouteDanger(danger))
						continue;

					var nearestThreatDistance = cache.Threats.Count == 0 ? long.MaxValue :
						cache.Threats.Min(t => (t.Actor.Location - destination).LengthSquared);
					var targetDistance = StealthAIThreatGeometry.MinimumCellSeparationSquared(
						coarse, approachTargets);
					if (approachTargets != null && approachTargets.Count > 0 &&
						targetDistance >= originTargetDistance)
						continue;
					debugCandidates?.Add((coarse, destination,
						(representative.Location - destination).LengthSquared, danger, nearestThreatDistance));
					if (targetDistance < bestTargetDistance ||
						(targetDistance == bestTargetDistance && (danger < bestDanger ||
						(danger == bestDanger && nearestThreatDistance > bestThreatDistance))))
					{
						best = destination;
						bestTargetDistance = targetDistance;
						bestDanger = danger;
						bestThreatDistance = nearestThreatDistance;
					}
				}

			if (debugCandidates != null && best != null)
			{
				var ranked = approachTargets != null && approachTargets.Count > 0 ?
					debugCandidates.OrderBy(candidate =>
						StealthAIThreatGeometry.MinimumCellSeparationSquared(candidate.Cell, approachTargets))
						.ThenBy(candidate => candidate.Danger)
						.ThenByDescending(candidate => candidate.Clearance).ToArray() :
					debugCandidates.OrderBy(candidate => candidate.Danger)
					.ThenByDescending(candidate => candidate.Clearance).ToArray();
				var selectedRank = Array.FindIndex(ranked, candidate => candidate.Destination == best.Value);
				var candidates = debugCandidates.Select(candidate =>
					$"cell={candidate.Cell}|destination={candidate.Destination}|travel={candidate.Travel}|" +
					$"danger={candidate.Danger}|clearance={candidate.Clearance}").JoinWith(";");
				Log.Write("debug", "Stealth safety candidates [{0}] tick={1}: evaluated={2} " +
					"selected={3} selected-rank={4} selected-minimum={5} " +
					"selection={6}.",
					owner.StealthProfile, owner.World.WorldTick, candidates, best.Value, selectedRank,
					selectedRank == 0, approachTargets != null && approachTargets.Count > 0 ?
						"target-distance-ascending,danger-ascending,clearance-descending" :
						"danger-ascending,clearance-descending");
			}

			return best;
		}

		static bool TryLiveStealthMemberRoutes(Squad owner,
			IReadOnlyCollection<Actor> activeMembers, CPos destination,
			IReadOnlyCollection<GroundThreat> threats,
			out Dictionary<Actor, List<CPos>> memberRoutes,
			out int detectorSteps, out double aggregateDanger, out double maximumDanger,
			out string memberRouteSummary)
		{
			var map = owner.World.Map;
			memberRoutes = new Dictionary<Actor, List<CPos>>();
			detectorSteps = 0;
			aggregateDanger = 0;
			maximumDanger = 0;
			var summaries = new List<string>();
			foreach (var member in activeMembers.Where(unit => !unit.IsDead && unit.IsInWorld)
				.OrderBy(unit => unit.ActorID))
			{
				var mobile = member.TraitOrDefault<Mobile>();
				var destinationEnterable = mobile != null && mobile.CanEnterCell(
					destination, null, BlockedByActor.Immovable);
				if (!destinationEnterable)
				{
					memberRouteSummary = summaries.JoinWith(",");
					return false;
				}

				var reversed = member.Location == destination ? new List<CPos> { destination } :
					mobile.Pathfinder.FindUnitPath(member.Location, destination,
						member, null, BlockedByActor.Immovable);
				if (reversed.Count == 0)
				{
					memberRouteSummary = summaries.JoinWith(",");
					return false;
				}

				var route = StealthAISpecialistPolicy.ForwardExactGroundRoute(reversed);
				if (route.Count == 0 || route[route.Count - 1] != destination)
					route.Add(destination);
				var routeDetectorSteps = 0;
				var routeAggregateDanger = 0d;
				var routeMaximumDanger = 0d;
				foreach (var routeCell in route)
				{
					var position = map.CenterOfCell(routeCell);
					var cellDanger = LiveStealthMemberCellDanger(owner, member, position, threats,
						out var detectorCovered);
					if (detectorCovered)
						routeDetectorSteps++;
					routeAggregateDanger += cellDanger;
					routeMaximumDanger = Math.Max(routeMaximumDanger, cellDanger);
				}
				memberRoutes.Add(member, route);
				detectorSteps += routeDetectorSteps;
				aggregateDanger += routeAggregateDanger;
				maximumDanger = Math.Max(maximumDanger, routeMaximumDanger);
				summaries.Add(string.Format("{0}#{1}:start={2}:detector-steps={3}:aggregate={4:0.###}:max={5:0.###}:route={6}",
					member.Info.Name, member.ActorID, member.Location, routeDetectorSteps,
					routeAggregateDanger, routeMaximumDanger, route.Count));
			}

			memberRouteSummary = summaries.JoinWith(",");
			return memberRoutes.Count > 0;
		}

		static double LiveStealthMemberCellDanger(Squad owner, Actor member, WPos position,
			IReadOnlyCollection<GroundThreat> threats, out bool detectorCovered)
		{
			return LiveStealthMemberCellDanger(owner, member, position, threats,
				out detectorCovered, out _);
		}

		static double LiveStealthMemberCellDanger(Squad owner, Actor member, WPos position,
			IReadOnlyCollection<GroundThreat> threats, out bool detectorCovered,
			out int dependencySignature, ISet<uint> relevantActorIds = null)
		{
			detectorCovered = false;
			var danger = 0d;
			var signature = 17;
			foreach (var threat in threats.OrderBy(item => item.Actor.ActorID))
				AccumulateLiveStealthMemberCellThreat(owner, member, position, threat,
					ref detectorCovered, ref danger, ref signature, relevantActorIds);

			dependencySignature = signature;
			return danger;
		}

		static void AccumulateLiveStealthMemberCellThreat(Squad owner, Actor member,
			WPos position, GroundThreat threat, ref bool detectorCovered, ref double danger,
			ref int signature, ISet<uint> relevantActorIds)
		{
			var covered = ThreatCoversPosition(threat, position, false,
				owner.StealthDefinition.DetectorRangeBufferCells);
			detectorCovered |= covered;
			if (!covered && !ThreatCoversPosition(threat, position, true))
				return;

			var distance = (threat.Actor.CenterPosition - position).HorizontalLength / 1024d;
			var pair = owner.SquadManager.CombatThreatCalculator.CalculateLive(
				member, threat.Actor, GroundTargetTypes, true);
			var contribution = GeneralizedCombatThreatCalculator.DefenderThreatAtDistance(pair, distance);
			danger += contribution;
			if (!covered && contribution == 0)
				return;
			relevantActorIds?.Add(threat.Actor.ActorID);

			unchecked
			{
				var bits = BitConverter.DoubleToInt64Bits(contribution);
				signature = signature * 31 + (int)threat.Actor.ActorID;
				signature = signature * 31 + (covered ? 1 : 0);
				signature = signature * 31 + (int)bits;
				signature = signature * 31 + (int)(bits >> 32);
			}
		}

		static readonly CPos[] LiveStealthAdjacentOffsets =
		{
			new CPos(-1, -1), new CPos(0, -1), new CPos(1, -1), new CPos(-1, 0),
			new CPos(1, 0), new CPos(-1, 1), new CPos(0, 1), new CPos(1, 1)
		};

		static (CPos Coarse, CPos Destination, bool Admissible, int Signature)
			ResolveLiveStealthEscapeDestination(Squad owner, IReadOnlyCollection<Actor> members,
				Actor representative, CPos current, int candidateIndex, bool nearestCell)
		{
			var map = owner.World.Map;
			var mobile = representative.TraitOrDefault<Mobile>();
			if (mobile == null || members.Count == 0 || candidateIndex < 0 ||
				candidateIndex >= LiveStealthAdjacentOffsets.Length)
				return (default(CPos), default(CPos), false, 17);

			var coarseSize = StealthCoarseSize(owner);
			var offset = LiveStealthAdjacentOffsets[candidateIndex];
			var coarse = new CPos(current.X + offset.X, current.Y + offset.Y);
			var destination = nearestCell ? Enumerable.Range(0, coarseSize)
				.SelectMany(y => Enumerable.Range(0, coarseSize).Select(x => new CPos(
					coarse.X * coarseSize + x, coarse.Y * coarseSize + y)))
				.Where(map.Contains).Where(cell => mobile.CanEnterCell(
					cell, null, BlockedByActor.Immovable))
				.Where(cell => members.All(member => member.Location != cell))
				.OrderBy(cell => (representative.Location - cell).LengthSquared)
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).FirstOrDefault() :
				map.Clamp(new CPos(coarse.X * coarseSize + coarseSize / 2,
					coarse.Y * coarseSize + coarseSize / 2));
			var contains = destination != default(CPos) && map.Contains(destination);
			var belongs = contains && CoarseCell(owner, destination) == coarse;
			var enterable = belongs && mobile.CanEnterCell(
				destination, null, BlockedByActor.Immovable);
			var blue = false;
			var red = false;
			var coarseHazard = false;
			if (enterable)
			{
				var resource = owner.World.WorldActor.TraitOrDefault<IResourceLayer>()?
					.GetResource(destination).Type;
				blue = resource == "BlueTiberium";
				red = resource == "RedTiberium";
				coarseHazard = CoarseCellHasForbiddenResource(owner, coarse, true);
			}

			unchecked
			{
				var signature = 17;
				signature = signature * 31 + current.GetHashCode();
				signature = signature * 31 + coarseSize;
				signature = signature * 31 + candidateIndex;
				signature = signature * 31 + coarse.GetHashCode();
				signature = signature * 31 + destination.GetHashCode();
				signature = signature * 31 + (contains ? 1 : 0);
				signature = signature * 31 + (belongs ? 1 : 0);
				signature = signature * 31 + (enterable ? 1 : 0);
				signature = signature * 31 + (blue ? 1 : 0);
				signature = signature * 31 + (red ? 1 : 0);
				signature = signature * 31 + (coarseHazard ? 1 : 0);
				return (coarse, destination, enterable && !blue && !red && !coarseHazard,
					signature);
			}
		}

		static LiveStealthEscapeCandidate EvaluateLiveStealthEscapeCandidate(Squad owner,
			IReadOnlyCollection<Actor> members, Actor representative, CPos current,
			IReadOnlyCollection<GroundThreat> threats, int candidateIndex, bool nearestCell,
			IReadOnlyList<CPos> approachTargets, out string rejection)
		{
			rejection = null;
			if (candidateIndex < 0 || candidateIndex >= LiveStealthAdjacentOffsets.Length)
				return null;

			var destinationState = ResolveLiveStealthEscapeDestination(owner, members,
				representative, current, candidateIndex, nearestCell);
			if (!destinationState.Admissible)
				return null;
			var coarse = destinationState.Coarse;
			var destination = destinationState.Destination;

			if (!TryLiveStealthMemberRoutes(owner, members, destination, threats,
				out var memberRoutes, out var detectorSteps, out var aggregateDanger,
				out var maximumDanger, out var memberRouteSummary))
			{
				rejection = string.Format("{0}:destination={1}:reason=no-member-route:" +
					"member-routes=[{2}]", coarse, destination, memberRouteSummary);
				return null;
			}
			if (members.Count > 1 && (detectorSteps > 0 || aggregateDanger > 0))
			{
				rejection = string.Format("{0}:destination={1}:reason=member-exposure:" +
					"detector-steps={2}:aggregate={3:0.###}:max={4:0.###}:member-routes=[{5}]",
					coarse, destination, detectorSteps, aggregateDanger, maximumDanger,
					memberRouteSummary);
				return null;
			}

			var originTargetDistance = StealthAIThreatGeometry.MinimumCellSeparationSquared(
				current, approachTargets);
			var targetDistance = StealthAIThreatGeometry.MinimumCellSeparationSquared(
				coarse, approachTargets);
			if (approachTargets != null && approachTargets.Count > 0 &&
				(targetDistance >= originTargetDistance || detectorSteps > 0 || aggregateDanger > 0))
				return null;

			return new LiveStealthEscapeCandidate
			{
				Cell = coarse,
				Destination = destination,
				DetectorSteps = detectorSteps,
				AggregateDanger = aggregateDanger,
				MaximumDanger = maximumDanger,
				RouteLength = memberRoutes.Sum(route => route.Value.Count),
				TargetDistance = targetDistance,
				MemberRoutes = memberRouteSummary,
				Routes = memberRoutes.ToDictionary(route => route.Key.ActorID, route => route.Value)
			};
		}

		static LiveStealthEscapeCandidate[] RankLiveStealthEscapeCandidates(
			IEnumerable<LiveStealthEscapeCandidate> candidates, bool approaching)
		{
			return candidates.OrderBy(candidate => approaching ? candidate.TargetDistance : long.MinValue)
				.ThenBy(candidate =>
					candidate.DetectorSteps == 0 && candidate.AggregateDanger <= 0 ? 0 : 1)
				.ThenBy(candidate => candidate.DetectorSteps > 0 ? 1 : 0)
				.ThenBy(candidate => candidate.AggregateDanger)
				.ThenBy(candidate => candidate.MaximumDanger)
				.ThenBy(candidate => candidate.RouteLength)
				.ThenBy(candidate => candidate.Cell.Y).ThenBy(candidate => candidate.Cell.X)
				.ToArray();
		}

		static CPos? NearestLiveStealthEscape(Squad owner,
			IReadOnlyCollection<Actor> activeMembers, Actor representative,
			bool nearestCell = false, CPos? originCell = null,
			IReadOnlyList<CPos> approachTargets = null)
		{
			var members = activeMembers.Where(unit => !unit.IsDead && unit.IsInWorld).ToArray();
			if (representative.TraitOrDefault<Mobile>() == null || members.Length == 0)
				return null;

			var current = originCell ?? CoarseCell(owner, representative.Location);
			var threats = LiveHostileGroundThreats(owner);
			var originTargetDistance = StealthAIThreatGeometry.MinimumCellSeparationSquared(
				current, approachTargets);
			var candidates = new List<LiveStealthEscapeCandidate>();
			var rejectedCandidates = owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug ?
				new List<string>() : null;
			for (var candidateIndex = 0; candidateIndex < LiveStealthAdjacentOffsets.Length; candidateIndex++)
			{
				var candidate = EvaluateLiveStealthEscapeCandidate(owner, members, representative,
					current, threats, candidateIndex, nearestCell, approachTargets, out var rejection);
				if (candidate != null)
					candidates.Add(candidate);
				else if (rejection != null)
					rejectedCandidates?.Add(rejection);
			}

			var ranked = RankLiveStealthEscapeCandidates(candidates,
				approachTargets != null && approachTargets.Count > 0);
			if (ranked.Length == 0)
			{
				if (rejectedCandidates != null)
					Log.Write("debug", "Stealth live escape candidates [{0}] tick={1}: evaluated=[] " +
						"rejected=[{2}] result=no-common-live-safe-flank " +
						"authority=per-member-live-world-standard-calculator.", owner.StealthProfile,
						owner.World.WorldTick, rejectedCandidates.JoinWith(";"));
				return null;
			}
			var selected = ranked[0];

			if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
				Log.Write("debug", "Stealth live escape candidates [{0}] tick={1}: evaluated=[{2}] " +
					"selected={3} selected-aggregate={4:0.###} selected-max={5:0.###} " +
					"selected-rank=0 selected-minimum=True zero-exposure={6} target-distance={7} " +
					"origin-target-distance={8} strict-decrease={9} " +
					"selection={10},zero-exposure-first,detector-free,aggregate-danger," +
					"maximum-danger,route-length,cell-order " +
					"authority=live-world-standard-calculator.", owner.StealthProfile,
					owner.World.WorldTick, candidates.Select(candidate => string.Format(
						"{0}:destination={1}:detector-steps={2}:aggregate={3:0.###}:max={4:0.###}:route={5}:target-distance={6}:member-routes=[{7}]",
						candidate.Cell, candidate.Destination, candidate.DetectorSteps,
						candidate.AggregateDanger, candidate.MaximumDanger,
						candidate.RouteLength, candidate.TargetDistance, candidate.MemberRoutes)).JoinWith(";"), selected.Cell,
					selected.AggregateDanger, selected.MaximumDanger,
					selected.DetectorSteps == 0 && selected.AggregateDanger <= 0,
					selected.TargetDistance, originTargetDistance,
					approachTargets == null || approachTargets.Count == 0 ||
						selected.TargetDistance < originTargetDistance,
					approachTargets != null && approachTargets.Count > 0 ?
						"target-distance-ascending" : "danger-only");

			return selected.Destination;
		}

		static bool PendingBlueExplosionInSquadCell(Squad owner, IEnumerable<Actor> members)
		{
			var resourceLayer = owner.World.WorldActor.TraitOrDefault<IResourceLayer>();
			if (resourceLayer == null)
				return false;

			var coarseSize = StealthCoarseSize(owner);
			return members.Select(unit => new CPos(unit.Location.X / coarseSize, unit.Location.Y / coarseSize))
				.Distinct().Any(coarse =>
				{
					for (var y = 0; y < coarseSize; y++)
						for (var x = 0; x < coarseSize; x++)
						{
							var cell = new CPos(coarse.X * coarseSize + x, coarse.Y * coarseSize + y);
							if (owner.World.Map.Contains(cell) &&
								resourceLayer.GetResource(cell).Type == "BlueTiberium" &&
								resourceLayer.IsExplosionPending(cell))
								return true;
						}

					return false;
				});
		}

		static void FinishStealthEscape(Squad owner)
		{
			var resumeEngagement = owner.StealthEscapePreserveEngagement && owner.IsTargetValid &&
				owner.AirTargetStrategicCell != null;
			owner.AirEscapingLocalAa = false;
			owner.StealthEscapeIssuedTick = -1;
			owner.StealthEscapeSafetyChecks = 0;
			owner.StealthEscapeDestination = null;
			owner.StealthEscapeStartCell = null;
			owner.StealthEscapeDestinationCell = null;
			owner.StealthEscapePendingExplosion = false;
			owner.StealthEscapeLastProgressTick = -1;
			owner.StealthEscapeLastDistanceCells = int.MaxValue;
			owner.StealthEscapePreserveEngagement = false;
			owner.FuzzyStateMachine.ChangeState(owner,
				resumeEngagement ? (IState)new StealthAIAttackState() : new StealthAIIdleState(), true);
		}

		static CPos? ActiveStealthCenterCell(Squad owner)
		{
			var members = AirDecisionUnits(owner).Where(unit => !unit.IsDead && unit.IsInWorld).ToArray();
			return members.Length == 0 ? (CPos?)null :
				CoarseCell(owner, owner.World.Map.CellContaining(
					members.Select(unit => unit.CenterPosition).Average()));
		}

		static bool ReachedOrPassedStealthEscapeCell(CPos start, CPos destination, CPos current)
		{
			var dx = Math.Sign(destination.X - start.X);
			var dy = Math.Sign(destination.Y - start.Y);
			return (dx == 0 || (current.X - destination.X) * dx >= 0) &&
				(dy == 0 || (current.Y - destination.Y) * dy >= 0);
		}

		protected static bool AdvanceStealthEscape(Squad owner)
		{
			if (!owner.AirEscapingLocalAa)
				return false;

			var center = ActiveStealthCenterCell(owner);
			if (center == null || owner.StealthEscapeStartCell == null ||
				owner.StealthEscapeDestinationCell == null)
			{
				FinishStealthEscape(owner);
				return false;
			}

			var start = owner.StealthEscapeStartCell.Value;
			var destination = owner.StealthEscapeDestinationCell.Value;
			if (ReachedOrPassedStealthEscapeCell(start, destination, center.Value))
			{
				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Stealth safety [{0}] center reached/crossed adjacent cell: " +
						"tick={1} start={2} destination={3} center={4}; immediate replan.",
						owner.StealthProfile, owner.World.WorldTick, start, destination, center.Value);
				FinishStealthEscape(owner);
				return false;
			}

			var direction = destination - start;
			var progress = (center.Value.X - start.X) * direction.X +
				(center.Value.Y - start.Y) * direction.Y;
			if (progress > owner.StealthEscapeLastDistanceCells)
			{
				owner.StealthEscapeLastDistanceCells = progress;
				owner.StealthEscapeLastProgressTick = owner.World.WorldTick;
			}

			if (owner.World.WorldTick - owner.StealthEscapeLastProgressTick < 150)
			{
				if (owner.SquadManager.Info.AirTargetDebugLogging)
					owner.StealthEscapeSafetyChecks++;
				return true;
			}

			if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Stealth safety [{0}] center made no escape progress for 150 ticks: " +
					"tick={1} start={2} destination={3} center={4}; immediate replan.",
					owner.StealthProfile, owner.World.WorldTick, start, destination, center.Value);
			FinishStealthEscape(owner);
			return false;
		}

		static bool TryRestoreLoadedStealthEscape(Squad owner, StealthInfluenceCache cache,
			IReadOnlyCollection<Actor> members)
		{
			if (!owner.StealthEscapeNeedsActivityRestore)
				return false;

			owner.StealthEscapeNeedsActivityRestore = false;
			var definition = owner.StealthDefinition;
			var center = ActiveStealthCenterCell(owner);
			if (definition == null || cache == null || center == null || members.Count == 0)
				return false;

			var revealed = members.Any(unit =>
				unit.TraitsImplementing<Cloak>().Any(cloak => !cloak.Cloaked));
			var detectorExposure = false;
			var weaponExposure = false;
			var maximumCanonicalThreat = 0d;
			foreach (var unit in members)
			{
				detectorExposure |= cache.Threats.Any(threat => ThreatCoversPosition(
					threat, unit.CenterPosition, false, definition.DetectorRangeBufferCells));
				foreach (var threat in cache.Threats)
				{
					if (!ThreatCoversPosition(threat, unit.CenterPosition, true,
						definition.ThreatRangeBufferCells))
						continue;

					var distance = (threat.Actor.CenterPosition - unit.CenterPosition).HorizontalLength / 1024d;
					if (!owner.SquadManager.CombatThreatCalculator.TryGetDefenderThreat(
						unit, threat.Actor, out var canonicalThreat, distance))
						continue;

					maximumCanonicalThreat = StealthAISpecialistPolicy.AccumulateMaximumCanonicalThreat(
						maximumCanonicalThreat, canonicalThreat);
					weaponExposure |= canonicalThreat > 0;
				}
			}

			if (!revealed || (!detectorExposure && !weaponExposure))
				return false;

			foreach (var member in members.OrderBy(unit => unit.ActorID))
			{
				var activity = member.CurrentActivity;
				if (activity?.GetType().Name != "Move")
					continue;

				var target = activity.GetTargets(member).FirstOrDefault();
				if (target.Type == TargetType.Invalid)
					continue;

				var exactDestination = owner.World.Map.CellContaining(target.CenterPosition);
				var destination = CoarseCell(owner, exactDestination);
				if (Math.Abs(destination.X - center.Value.X) > 1 ||
					Math.Abs(destination.Y - center.Value.Y) > 1)
					continue;

				owner.AirEscapingLocalAa = true;
				owner.StealthEscapeIssuedTick = owner.World.WorldTick;
				owner.StealthEscapeLastProgressTick = owner.World.WorldTick;
				owner.StealthEscapeDestination = exactDestination;
				owner.StealthEscapeStartCell = center;
				owner.StealthEscapeDestinationCell = destination;
				owner.StealthEscapeLastDistanceCells = 0;
				owner.StealthEscapeSafetyChecks = 0;
				if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth safety [{0}] restored loaded local escape: tick={1} " +
						"actor={2}#{3} activity={4} start={5} destination={6} exact={7} " +
						"revealed={8} detector={9} weapon={10} canonical-threat={11:0.###}.",
						owner.StealthProfile, owner.World.WorldTick, member.Info.Name, member.ActorID,
						activity.GetType().Name, center.Value, destination, exactDestination,
						revealed, detectorExposure, weaponExposure, maximumCanonicalThreat);
				return true;
			}

			return false;
		}

		protected static bool KiteParticipantTookDamage(Squad owner)
		{
			if (owner.StealthClearMode != StealthClearMode.Kite)
				return false;

			foreach (var participant in owner.StealthKiteParticipantHealth)
			{
				var actor = owner.World.GetActorById(participant.Key);
				if (actor == null || actor.IsDead || !actor.IsInWorld ||
					(actor.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue) < participant.Value)
					return true;
			}

			return false;
		}

		static bool KiteFormationIsLocallySafe(Squad owner,
			IReadOnlyCollection<Actor> formation, Actor target)
		{
			var definition = owner.StealthDefinition;
			var selectedThreat = LiveGroundThreat(target);
			if (definition == null || !definition.EnableKiting ||
				formation.Count == 0 || !IsLiveLocalCombatActor(owner, formation, target))
				return false;

			var ownSpeed = formation.Min(CurrentGroundSpeed);
			var ownRange = formation.Min(unit => GroundWeaponRange(unit, target));
			if (!StealthAISpecialistPolicy.CanKite(ownSpeed, selectedThreat.Speed, ownRange,
				selectedThreat.WeaponRange, definition.KiteRangeMarginCells,
				definition.MinimumKiteSpeedPercent))
				return false;

			var minimumRange = selectedThreat.WeaponRange + definition.KiteRangeMarginCells;
			return formation.All(unit =>
			{
				var distance = (unit.CenterPosition - target.CenterPosition).HorizontalLength / 1024f;
				return distance >= minimumRange && distance <= ownRange &&
					!LiveKitePositionIsCovered(owner, unit, target, unit.CenterPosition) &&
					!ThreatCoversPosition(selectedThreat, unit.CenterPosition, false,
						definition.DetectorRangeBufferCells);
			});
		}

		static bool IssueStealthEscape(Squad owner,
			IReadOnlyCollection<Actor> activeMembers, CPos destination, bool pendingBlueExplosion)
		{
			var members = activeMembers.Where(unit => !unit.IsDead && unit.IsInWorld).ToArray();
			if (members.Length == 0)
				return false;
			var start = CoarseCell(owner, owner.World.Map.CellContaining(
				members.Select(unit => unit.CenterPosition).Average()));
			var destinationCell = CoarseCell(owner, destination);
			if (start == destinationCell || Math.Abs(destinationCell.X - start.X) > 1 ||
				Math.Abs(destinationCell.Y - start.Y) > 1)
				return false;

			if (members.Any(member => member.Location == destination) ||
				!TryLiveStealthMemberRoutes(owner, members, destination,
					LiveHostileGroundThreats(owner), out var memberRoutes,
					out var detectorSteps, out var aggregateDanger, out var maximumDanger,
					out var memberRouteSummary) ||
				(members.Length > 1 && (detectorSteps > 0 || aggregateDanger > 0)))
				return false;

			return IssueValidatedStealthEscape(owner, members, destination, pendingBlueExplosion,
				memberRoutes.ToDictionary(route => route.Key.ActorID, route => route.Value),
				detectorSteps, aggregateDanger, maximumDanger, memberRouteSummary);
		}

		static bool IssueCachedStealthStrategicStep(Squad owner,
			IReadOnlyCollection<Actor> activeMembers, CPos destination)
		{
			var members = activeMembers.Where(unit => !unit.IsDead && unit.IsInWorld)
				.OrderBy(unit => unit.ActorID).ToArray();
			if (members.Length == 0)
				return false;

			var start = CoarseCell(owner, owner.World.Map.CellContaining(
				members.Select(unit => unit.CenterPosition).Average()));
			var destinationCell = CoarseCell(owner, destination);
			if (start == destinationCell || Math.Abs(destinationCell.X - start.X) > 1 ||
				Math.Abs(destinationCell.Y - start.Y) > 1)
				return false;

			var occupied = members.Select(member => member.Location).ToHashSet();
			var exactCandidates = Enumerable.Range(-2, 5).SelectMany(dy => Enumerable.Range(-2, 5)
				.Select(dx => owner.World.Map.Clamp(new CPos(destination.X + dx, destination.Y + dy))))
				.Where(cell => CoarseCell(owner, cell) == destinationCell).Distinct()
				.OrderBy(cell => (cell - destination).LengthSquared)
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).ToList();
			var exactIndex = StealthAISpecialistPolicy.FirstUnoccupiedEnterableDestination(
				exactCandidates.Select(occupied.Contains).ToList(),
				exactCandidates.Select(cell => members.All(member =>
					member.TraitOrDefault<Mobile>()?.CanEnterCell(
						cell, null, BlockedByActor.Immovable) == true)).ToList());
			if (exactIndex < 0)
				return false;

			var exactDestination = exactCandidates[exactIndex];
			owner.AirRoute.Clear();
			owner.AirRouteQueued = true;
			owner.Bot.QueueOrder(new Order("Move", null,
				Target.FromCell(owner.World, exactDestination), false, groupedActors: members));
			if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Stealth strategic approach [{0}] issued cached adjacent-cell Move: " +
					"tick={1} members={2} start={3} destination={4} exact={5} " +
					"authority=cached-influence live-route-fold=False.", owner.StealthProfile,
					owner.World.WorldTick, members.Length, start, destinationCell, exactDestination);

			return true;
		}

		static bool IssueValidatedStealthEscape(Squad owner,
			IReadOnlyCollection<Actor> activeMembers, CPos destination, bool pendingBlueExplosion,
			IReadOnlyDictionary<uint, List<CPos>> memberRoutes, int detectorSteps,
			double aggregateDanger, double maximumDanger, string memberRouteSummary)
		{
			var members = activeMembers.Where(unit => !unit.IsDead && unit.IsInWorld)
				.OrderBy(unit => unit.ActorID).ToArray();
			if (members.Length == 0 || members.Any(member =>
				!memberRoutes.ContainsKey(member.ActorID) || member.Location == destination))
				return false;
			var start = CoarseCell(owner, owner.World.Map.CellContaining(
				members.Select(unit => unit.CenterPosition).Average()));
			var destinationCell = CoarseCell(owner, destination);
			if (start == destinationCell || Math.Abs(destinationCell.X - start.X) > 1 ||
				Math.Abs(destinationCell.Y - start.Y) > 1)
				return false;

			var exactDestination = destination;

			owner.StealthEscapePreserveEngagement = !pendingBlueExplosion && owner.IsTargetValid &&
				owner.AirTargetStrategicCell != null;
			if (!owner.StealthEscapePreserveEngagement)
			{
				owner.TargetActor = null;
				owner.AirTargetStrategicCell = null;
			}
			owner.AirRoute.Clear();
			owner.AirRouteQueued = false;
			owner.AirEscapingLocalAa = true;
			owner.StealthEscapePendingExplosion = pendingBlueExplosion;
			owner.StealthEscapeIssuedTick = owner.World.WorldTick;
			owner.StealthEscapeLastProgressTick = owner.World.WorldTick;
			owner.StealthEscapeDestination = exactDestination;
			owner.StealthEscapeStartCell = start;
			owner.StealthEscapeDestinationCell = destinationCell;
			owner.StealthEscapeLastDistanceCells = 0;
			owner.StealthEscapeSafetyChecks = 0;

			foreach (var member in members)
			{
				var queued = false;
				foreach (var waypoint in memberRoutes[member.ActorID])
				{
					owner.Bot.QueueOrder(new Order("Move", member,
						Target.FromCell(owner.World, waypoint), queued));
					queued = true;
				}
			}

			if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Stealth safety [{0}] issued per-member live-safe adjacent-cell Moves: " +
					"tick={1} members={2} start={3} destination={4} exact={5} detector-steps={6} " +
					"aggregate={7:0.###} max={8:0.###} member-routes=[{9}] order-batches={2}.",
					owner.StealthProfile, owner.World.WorldTick, members.Length,
					start, destinationCell, exactDestination, detectorSteps,
					aggregateDanger, maximumDanger, memberRouteSummary);

			return true;
		}

		protected static int StealthKillCadenceMaximumTicks(Squad owner)
		{
			return Math.Max(1, 45000 / Math.Max(1, owner.World.Timestep));
		}

		static void StealthDebugOrderTarget(Squad owner, OpenRA.Activities.Activity activity, Actor unit,
			out TargetType type, out string name, out uint actorId, out CPos cell)
		{
			var target = activity == null ? Target.Invalid : activity.GetTargets(unit).FirstOrDefault();
			type = target.Type;
			name = "none";
			actorId = 0;
			cell = CPos.Zero;
			if (target.Type == TargetType.Actor && target.Actor != null)
			{
				name = target.Actor.Info.Name;
				actorId = target.Actor.ActorID;
				cell = target.Actor.Location;
			}
			else if (target.Type != TargetType.Invalid)
				cell = owner.World.Map.CellContaining(target.CenterPosition);
		}

		static int StealthDebugLifecycleSignature(in StealthDebugLifecycleSnapshot snapshot)
		{
			unchecked
			{
				var hash = 17;
				hash = hash * 31 + snapshot.Role.GetHashCode();
				hash = hash * 31 + (int)snapshot.Mode;
				hash = hash * 31 + (int)snapshot.TargetId;
				hash = hash * 31 + snapshot.CurrentActivity.GetHashCode();
				hash = hash * 31 + snapshot.CurrentActivityState;
				hash = hash * 31 + (snapshot.CurrentActivityCanceling ? 1 : 0);
				hash = hash * 31 + snapshot.NextActivity.GetHashCode();
				hash = hash * 31 + snapshot.NextActivityState;
				hash = hash * 31 + (snapshot.NextActivityCanceling ? 1 : 0);
				hash = hash * 31 + snapshot.FinalActivity.GetHashCode();
				hash = hash * 31 + snapshot.FinalActivityState;
				hash = hash * 31 + (snapshot.FinalActivityCanceling ? 1 : 0);
				hash = hash * 31 + snapshot.ActivityDepth;
				hash = hash * 31 + (int)snapshot.OrderTargetType;
				hash = hash * 31 + (int)snapshot.OrderTargetId;
				hash = hash * 31 + snapshot.OrderTargetCell.GetHashCode();
				hash = hash * 31 + (snapshot.RouteQueued ? 1 : 0);
				hash = hash * 31 + snapshot.RoutePhase.GetHashCode();
				hash = hash * 31 + (snapshot.Safety ? 1 : 0);
				hash = hash * 31 + snapshot.EscapeDestination.GetHashCode();
				hash = hash * 31 + (snapshot.Firing ? 1 : 0);
				hash = hash * 31 + snapshot.HP;
				hash = hash * 31 + (snapshot.Repair ? 1 : 0);
				hash = hash * 31 + (int)snapshot.RepairTargetId;
				return hash;
			}
		}

		static void RecordStealthDebugLifecycle(Squad owner, bool force)
		{
			// Release-default-off observation only: this guard precedes allocation, member inspection,
			// target enumeration, and formatting. Snapshots never queue an order or scan the world.
			if (!owner.SquadManager.Info.AirTargetDebugLogging || owner.StealthProfile != "stealth-tank")
				return;

			if (owner.StealthDebugLifecycle == null)
			{
				owner.StealthDebugLifecycle = new Dictionary<uint, Queue<StealthDebugLifecycleSnapshot>>();
				owner.StealthDebugLifecycleState = new Dictionary<uint, (int Signature, int LastReportTick)>();
			}

			var cadenceReset = owner.StealthDebugLifecycleLastCadenceAge >= 0 &&
				owner.StealthKillCadenceAge < owner.StealthDebugLifecycleLastCadenceAge;
			if (cadenceReset)
			{
				owner.StealthDebugLifecycle.Clear();
				owner.StealthDebugLifecycleState.Clear();
			}

			owner.StealthDebugLifecycleLastCadenceAge = owner.StealthKillCadenceAge;
			var tick = owner.World.WorldTick;
			var maximumTicks = StealthKillCadenceMaximumTicks(owner);
			var dense = owner.StealthKillCadenceAge >= Math.Max(0, maximumTicks - 600);
			var live = owner.Units.Where(unit => !unit.IsDead && unit.IsInWorld && unit.Info.Name == "stnk")
				.OrderBy(unit => unit.ActorID).ToArray();
			var liveIds = live.Select(unit => unit.ActorID).ToHashSet();
			foreach (var removed in owner.StealthDebugLifecycle.Keys.Where(id => !liveIds.Contains(id)).ToArray())
			{
				owner.StealthDebugLifecycle.Remove(removed);
				owner.StealthDebugLifecycleState.Remove(removed);
			}

			var formation = owner.AirFormationUnits().Select(unit => unit.ActorID).ToHashSet();
			var ownedTarget = owner.IsTargetValid ? owner.TargetActor : null;
			foreach (var unit in live)
			{
				var current = unit.CurrentActivity;
				var next = current?.NextActivity;
				var final = current;
				var depth = 0;
				for (var activity = current; activity != null && depth < 64; activity = activity.NextActivity)
				{
					final = activity;
					depth++;
				}

				StealthDebugOrderTarget(owner, final, unit, out var orderTargetType,
					out var orderTargetName, out var orderTargetId, out var orderTargetCell);
				if (orderTargetType == TargetType.Invalid)
					StealthDebugOrderTarget(owner, current, unit, out orderTargetType,
						out orderTargetName, out orderTargetId, out orderTargetCell);
				var health = unit.TraitOrDefault<IHealth>();
				var maximumFireDelay = unit.TraitsImplementing<Armament>()
					.Where(armament => !armament.IsTraitDisabled).Select(armament => armament.FireDelay)
					.DefaultIfEmpty(0).Max();
				var hasRepairTarget = owner.AirRepairTargets.TryGetValue(unit.ActorID, out var repairTargetId);
				var firing = maximumFireDelay > 0;
				var snapshot = new StealthDebugLifecycleSnapshot
				{
					Tick = tick,
					CadenceAge = owner.StealthKillCadenceAge,
					ActorId = unit.ActorID,
					Role = formation.Contains(unit.ActorID) ? "formation" :
						owner.AirReinforcements.Contains(unit.ActorID) ? "reinforcement" : "owned-other",
					Mode = owner.StealthClearMode,
					TargetName = ownedTarget?.Info.Name ?? "none",
					TargetId = ownedTarget?.ActorID ?? 0,
					HasTarget = ownedTarget != null,
					TargetCell = ownedTarget?.Location ?? CPos.Zero,
					CanAttack = ownedTarget != null && CanAttackTarget(unit, ownedTarget),
					ActorCell = unit.Location,
					Distance = ownedTarget == null ? -1 :
						(ownedTarget.CenterPosition - unit.CenterPosition).HorizontalLength / 1024,
					RouteQueued = owner.AirRouteQueued,
					RouteBuffer = owner.AirRoute.Count,
					RoutePhase = owner.AirEscapingLocalAa ? "safety" : firing || BusyAttack(unit) ? "attack" :
						unit.IsIdle ? "idle" : "travel",
					ProgressAge = tick - owner.AirTargetLastProgressTick,
					Safety = owner.AirEscapingLocalAa,
					EscapeDestination = owner.StealthEscapeDestination ?? CPos.Zero,
					HasEscapeDestination = owner.StealthEscapeDestination != null,
					Firing = firing,
					MaximumFireDelay = maximumFireDelay,
					HP = health?.HP ?? int.MaxValue,
					MaxHP = health?.MaxHP ?? int.MaxValue,
					Repair = owner.AirUnitsRepairing.Contains(unit.ActorID),
					RepairTargetId = repairTargetId,
					HasRepairTarget = hasRepairTarget,
					Idle = unit.IsIdle,
					CurrentActivity = current?.GetType().Name ?? "none",
					CurrentActivityState = current == null ? -1 : (int)current.State,
					CurrentActivityCanceling = current?.IsCanceling ?? false,
					NextActivity = next?.GetType().Name ?? "none",
					NextActivityState = next == null ? -1 : (int)next.State,
					NextActivityCanceling = next?.IsCanceling ?? false,
					FinalActivity = final?.GetType().Name ?? "none",
					FinalActivityState = final == null ? -1 : (int)final.State,
					FinalActivityCanceling = final?.IsCanceling ?? false,
					ActivityDepth = depth,
					OrderTargetType = orderTargetType,
					OrderTargetName = orderTargetName,
					OrderTargetId = orderTargetId,
					OrderTargetCell = orderTargetCell
				};
				snapshot.Signature = StealthDebugLifecycleSignature(snapshot);
				var known = owner.StealthDebugLifecycleState.TryGetValue(unit.ActorID, out var previous);
				var changed = !known || previous.Signature != snapshot.Signature;
				var heartbeat = known && tick - previous.LastReportTick >= 75;
				if (!force && !changed && !heartbeat && !dense)
					continue;

				snapshot.Reason = force ? 4 : !known ? 0 : changed ? 1 : dense ? 3 : 2;
				if (!owner.StealthDebugLifecycle.TryGetValue(unit.ActorID, out var history))
				{
					history = new Queue<StealthDebugLifecycleSnapshot>(256);
					owner.StealthDebugLifecycle.Add(unit.ActorID, history);
				}

				if (history.Count == 256)
					history.Dequeue();
				history.Enqueue(snapshot);
				owner.StealthDebugLifecycleState[unit.ActorID] = (snapshot.Signature, tick);
			}
		}

		static void FlushStealthDebugLifecycle(Squad owner, IReadOnlyCollection<Actor> stnks)
		{
			if (!owner.SquadManager.Info.AirTargetDebugLogging || owner.StealthDebugLifecycle == null)
				return;

			var representative = owner.AirFormationUnits().Where(unit => !unit.IsDead && unit.IsInWorld &&
				unit.Info.Name == "stnk").OrderBy(unit => unit.ActorID).FirstOrDefault() ??
				stnks.OrderBy(unit => unit.ActorID).FirstOrDefault();
			if (representative == null || !owner.StealthDebugLifecycle.TryGetValue(
				representative.ActorID, out var history))
				return;

			Log.Write("debug", "Stealth cadence lifecycle [stealth-tank] failure-window: tick={0} " +
				"generation={1} squad={2}#{3} representative=stnk#{4} snapshots={5} ring-capacity=256 " +
				"flush=watchdog-failure one-unit-only=True.", owner.World.WorldTick,
				owner.StealthKillCadenceGeneration.GenerationId, owner.StealthSquadDefinition,
				owner.StealthSquadIndex, representative.ActorID, history.Count);
			foreach (var snapshot in history)
				Log.Write("debug", "Stealth cadence lifecycle [stealth-tank] buffered-member: tick={0} " +
					"generation={1} squad={2}#{3} actor=stnk#{4} reason={5} cadence-age={6}/{7} role={8} mode={9} " +
					"target={10} target-cell={11} can-attack={12} actor-cell={13} distance={14} " +
					"route-queued={15} route-buffer={16} route-phase={17} progress-age={18} " +
					"safety={19} escape={20} firing={21} max-fire-delay={22} hp={23}/{24} " +
					"repair={25} repair-target={26} idle={27} activity-current={28}:{29}:cancel={30} " +
					"activity-next={31}:{32}:cancel={33} activity-final={34}:{35}:cancel={36} " +
					"queue-depth={37} order-target={38}:{39}#{40}@{41} signature={42}.",
					snapshot.Tick, owner.StealthKillCadenceGeneration.GenerationId,
					owner.StealthSquadDefinition, owner.StealthSquadIndex,
					snapshot.ActorId, snapshot.Reason, snapshot.CadenceAge,
					StealthKillCadenceMaximumTicks(owner), snapshot.Role, snapshot.Mode,
					snapshot.HasTarget ? snapshot.TargetName + "#" + snapshot.TargetId : "none",
					snapshot.HasTarget ? snapshot.TargetCell.ToString() : "none", snapshot.CanAttack,
					snapshot.ActorCell, snapshot.Distance, snapshot.RouteQueued, snapshot.RouteBuffer,
					snapshot.RoutePhase, snapshot.ProgressAge, snapshot.Safety,
					snapshot.HasEscapeDestination ? snapshot.EscapeDestination.ToString() : "none",
					snapshot.Firing, snapshot.MaximumFireDelay, snapshot.HP, snapshot.MaxHP,
					snapshot.Repair, snapshot.HasRepairTarget ? snapshot.RepairTargetId.ToString() : "none",
					snapshot.Idle, snapshot.CurrentActivity, snapshot.CurrentActivityState,
					snapshot.CurrentActivityCanceling, snapshot.NextActivity, snapshot.NextActivityState,
					snapshot.NextActivityCanceling, snapshot.FinalActivity, snapshot.FinalActivityState,
					snapshot.FinalActivityCanceling, snapshot.ActivityDepth, snapshot.OrderTargetType,
					snapshot.OrderTargetName, snapshot.OrderTargetId, snapshot.OrderTargetCell,
					snapshot.Signature);
		}

		static void TickStealthDebugKillCadenceWatchdog(Squad owner)
		{
			if (owner.StealthProfile != "stealth-tank" || owner.StealthKillCadenceGeneration == null)
				return;

			var tick = owner.World.WorldTick;
			var maximumTicks = StealthKillCadenceMaximumTicks(owner);
			if (owner.StealthDebugKillCadenceNextReportTick < 0)
				owner.StealthDebugKillCadenceNextReportTick = tick + maximumTicks;

			// The squad owns the clock. Promotion, reinforcement, death, or ordinary membership
			// churn changes the observed members without resetting or replacing its accumulated age.
			var stnks = owner.Units.Where(unit => !unit.IsDead && unit.IsInWorld &&
				unit.Info.Name == "stnk").Distinct().ToArray();
			var noStnk = stnks.Length == 0;
			var firing = stnks.Any(unit => unit.TraitsImplementing<Armament>()
				.Any(armament => !armament.IsTraitDisabled && armament.FireDelay > 0));
			var repairing = stnks.Length > 0 && stnks.All(unit =>
				owner.AirUnitsRepairing.Contains(unit.ActorID));
			var activeTarget = owner.IsTargetValid && stnks.Any(unit => CanAttackTarget(unit, owner.TargetActor)) ?
				owner.TargetActor : null;
			var reachableEnemy = activeTarget != null;
			// An assigned STNK makes this an active squad. Firing, repair, target acquisition,
			// route planning, and temporary target absence cannot pause the literal 45-second kill clock.
			var exempt = noStnk;
			var members = stnks.OrderBy(unit => unit.ActorID)
				.Select(unit => unit.Info.Name + "#" + unit.ActorID).JoinWith(",");
			var mismatchDetected = owner.StealthKillCadenceGeneration.Observe(tick, !exempt);
			if (mismatchDetected)
			{
				Log.Write("debug", "Stealth kill watchdog [stealth-tank] permanent generation-age mismatch: " +
					"tick={0} generation={1} squad={2}#{3} generation-start={4} generation-elapsed={5} " +
					"cadence-age={6} window-start={7} status=permanent-failure.", tick,
					owner.StealthKillCadenceGeneration.GenerationId, owner.StealthSquadDefinition,
					owner.StealthSquadIndex, owner.StealthKillCadenceGeneration.GenerationStartTick,
					tick - owner.StealthKillCadenceGeneration.GenerationStartTick,
					owner.StealthKillCadenceAge, owner.StealthKillCadenceGeneration.WindowStartTick);
			}
			if (owner.SquadManager.Info.AirTargetDebugLogging && !owner.StealthDebugKillCadenceFailed &&
				StealthAISpecialistPolicy.KillCadenceFailed(owner.StealthKillCadenceAge, maximumTicks))
			{
				RecordStealthDebugLifecycle(owner, true);
				FlushStealthDebugLifecycle(owner, stnks);
				owner.StealthKillCadenceGeneration.MarkCadenceFailed();
				Log.Write("debug", "Stealth kill watchdog [stealth-tank] squad failure: tick={0} " +
					"generation={1} generation-start={2} window-start={3} squad={4}#{5} " +
					"cadence-age={6}/{7} stnks={8} formation={9} reinforcements={10} " +
					"members=[{11}] target={12} firing={13} repairing={14} reachable-enemy={15}.",
					tick, owner.StealthKillCadenceGeneration.GenerationId,
					owner.StealthKillCadenceGeneration.GenerationStartTick,
					owner.StealthKillCadenceGeneration.WindowStartTick,
					owner.StealthSquadDefinition, owner.StealthSquadIndex,
					owner.StealthKillCadenceAge, maximumTicks, stnks.Length,
					owner.AirFormationUnits().Count, owner.AirReinforcements.Count,
					members,
					activeTarget == null ? "none" : activeTarget.Info.Name + "#" + activeTarget.ActorID,
					firing, repairing, reachableEnemy);
			}
			if (owner.SquadManager.Info.AirTargetDebugLogging &&
				tick >= owner.StealthDebugKillCadenceNextReportTick)
			{
				Log.Write("debug", "Stealth kill watchdog [stealth-tank] squad acceptance: tick={0} " +
					"generation={1} generation-start={2} window-start={3} squad={4}#{5} " +
					"cadence-age={6}/{7} generation-kills={8} stnks={9} formation={10} " +
					"reinforcements={11} members=[{12}] target={13} firing={14} repairing={15} " +
					"reachable-enemy={16} exempt={17} status={18}.", tick,
					owner.StealthKillCadenceGeneration.GenerationId,
					owner.StealthKillCadenceGeneration.GenerationStartTick,
					owner.StealthKillCadenceGeneration.WindowStartTick, owner.StealthSquadDefinition,
					owner.StealthSquadIndex, owner.StealthKillCadenceAge, maximumTicks,
					owner.StealthDebugKillCadenceKills, stnks.Length, owner.AirFormationUnits().Count,
					owner.AirReinforcements.Count, members,
					activeTarget == null ? "none" : activeTarget.Info.Name + "#" + activeTarget.ActorID,
					firing, repairing, reachableEnemy, exempt,
					owner.StealthDebugKillCadenceFailed ? "failure" : exempt ? "exempt" : "pass");
				owner.StealthDebugKillCadenceNextReportTick = tick + maximumTicks;
			}
		}

		internal static void TickStealthSafety(Squad owner, bool pendingBlueOnly = false)
		{
			if (!owner.IsValid || owner.Type != SquadType.Stealth)
				return;

			if (!pendingBlueOnly)
			{
				TickStealthDebugKillCadenceWatchdog(owner);
				foreach (var unit in owner.Units)
					SendHomeToRepair(owner, unit);
				PromoteArrivedAirReinforcements(owner);
			}
			RoutePendingStealthReinforcements(owner);

			var representative = AirDecisionUnits(owner).OrderBy(a => a.ActorID).FirstOrDefault();
			if (representative == null)
				return;
			var definition = owner.StealthDefinition;
			var cache = StealthInfluence(owner, representative);
			if (definition == null || cache == null)
				return;
			var liveMembers = owner.Units.Where(unit => !unit.IsDead && unit.IsInWorld).ToArray();
			var activeMembers = liveMembers.Where(unit =>
				!owner.AirUnitsRepairing.Contains(unit.ActorID)).ToArray();
			var pendingBlueExplosion = PendingBlueExplosionInSquadCell(owner, activeMembers);
			if (TryRestoreLoadedStealthEscape(owner, cache, AirDecisionUnits(owner)))
				return;

			if (owner.AirEscapingLocalAa)
			{
				if (AdvanceStealthEscape(owner))
					return;
			}

			if (pendingBlueOnly && !pendingBlueExplosion)
				return;

			var decisionUnits = AirDecisionUnits(owner);
			var safeKite = owner.IsTargetValid && owner.StealthClearMode == StealthClearMode.Kite &&
				KiteFormationIsLocallySafe(owner, decisionUnits, owner.TargetActor);
			var kiteParticipantDamaged = KiteParticipantTookDamage(owner);

			var detectorExposure = false;
			var weaponExposure = false;
			var maximumCanonicalThreat = 0d;
			var plannedDecloak = owner.IsTargetValid && decisionUnits.Any(unit =>
				CanAttackTarget(unit, owner.TargetActor) &&
				(unit.CenterPosition - owner.TargetActor.CenterPosition).HorizontalLength <=
					WDist.FromCells(GroundWeaponRange(unit, owner.TargetActor)).Length);
			foreach (var unit in decisionUnits)
			{
				detectorExposure |= cache.Threats.Any(t => ThreatCoversPosition(t, unit.CenterPosition,
					false, definition.DetectorRangeBufferCells));
				foreach (var threat in cache.Threats)
				{
					if ((safeKite && threat.Actor == owner.TargetActor) ||
						!ThreatCoversPosition(threat, unit.CenterPosition, true, definition.ThreatRangeBufferCells))
						continue;

					var distance = (threat.Actor.CenterPosition - unit.CenterPosition).HorizontalLength / 1024d;
					if (!owner.SquadManager.CombatThreatCalculator.TryGetDefenderThreat(
						unit, threat.Actor, out var canonicalThreat, distance))
						continue;

					maximumCanonicalThreat = StealthAISpecialistPolicy.AccumulateMaximumCanonicalThreat(
						maximumCanonicalThreat, canonicalThreat);
					weaponExposure |= canonicalThreat > 0;
				}
			}
			weaponExposure |= kiteParticipantDamaged;

			var currentResource = owner.World.WorldActor.TraitOrDefault<IResourceLayer>()?
				.GetResource(representative.Location).Type;
			var resourceHazard = currentResource == "RedTiberium";
			var revealed = decisionUnits.Any(unit =>
				unit.TraitsImplementing<Cloak>().Any(cloak => !cloak.Cloaked));
			var engagedWeaponExposure = weaponExposure && (revealed ||
				StealthAISpecialistPolicy.IsHardPlannedDecloakThreat(plannedDecloak, maximumCanonicalThreat));
			var engagementThreat = StealthAISpecialistPolicy.IsEngagementThreat(
				detectorExposure, weaponExposure, engagedWeaponExposure);
			if (Game.Settings.Debug.BotDebug &&
				(pendingBlueExplosion || resourceHazard || engagementThreat ||
				owner.World.WorldTick >= owner.StealthLocalPolicyNextReportTick))
			{
				owner.StealthLocalPolicyNextReportTick = owner.World.WorldTick + 250;
				Log.Write("debug", "Stealth local safety watchdog [{0}] tick={1}: mode={2} " +
					"target={3}#{4} detector={5} weapon={6} engaged-weapon={7} revealed={8} " +
					"planned-decloak={9} canonical-current-range-max={10:0.###} safe-kite={11} " +
					"kite-damaged={12} red-tiberium={13} pending-blue={14} verdict={15}.",
					owner.StealthProfile, owner.World.WorldTick, owner.StealthClearMode,
					owner.TargetActor?.Info.Name ?? "none", owner.TargetActor?.ActorID ?? 0,
					detectorExposure, weaponExposure, engagedWeaponExposure, revealed,
					plannedDecloak, maximumCanonicalThreat, safeKite, kiteParticipantDamaged,
					resourceHazard, pendingBlueExplosion,
					owner.StealthClearMode == StealthClearMode.Mass && !pendingBlueExplosion ?
						"retain-explicit-mass-policy" :
					pendingBlueExplosion || resourceHazard || engagementThreat ?
						"ordinary-escape-required" : "retain-approved-engagement");
			}
			if (!pendingBlueExplosion && owner.StealthClearMode == StealthClearMode.Mass)
			{
				if (Game.Settings.Debug.BotDebug &&
					owner.World.WorldTick >= owner.StealthMassPolicyNextReportTick)
				{
					owner.StealthMassPolicyNextReportTick = owner.World.WorldTick + 250;
					var package = LiveLatchedDefenderPackage(owner);
					var overmatch = CrossoverOvermatch(owner, decisionUnits, package);
					var policyOvermatch = overmatch < 0 ? double.MaxValue : overmatch;
					var exitPending = StealthAISpecialistPolicy.ShouldAbortMassClear(
						policyOvermatch, definition.MassClearAbortCrossoverPercent);
					Log.Write("debug", "Stealth mass policy watchdog [{0}] tick={1}: mode=Mass " +
						"entry=explicit-crossover exit-threshold={2} measured-overmatch={3:0.###} " +
						"policy-overmatch={4:0.###} detector-exposure={5} weapon-exposure={6} " +
						"revealed={7} planned-decloak={8} canonical-current-range-max={9:0.###} " +
						"ordinary-flee=bypassed-by-policy decision={10} package={11} members={12}.",
						owner.StealthProfile,
						owner.World.WorldTick, definition.MassClearAbortCrossoverPercent / 100d,
						overmatch, policyOvermatch, detectorExposure, weaponExposure,
						revealed, plannedDecloak, maximumCanonicalThreat,
						exitPending ? "exit-on-state-update" : "continue-crossover-policy",
						package.Count, decisionUnits.Count);
				}

				return;
			}
			if (!pendingBlueExplosion && !engagementThreat &&
				!resourceHazard)
				return;

			var destination = NearestLiveStealthEscape(owner, decisionUnits, representative,
				pendingBlueExplosion, ActiveStealthCenterCell(owner));
			if (destination == null)
				return;

			if (owner.StealthClearMode == StealthClearMode.CrushBridge &&
				(owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug))
				Log.Write("debug", "Stealth crush bridge [{0}] outcome=safety-escape-replan: tick={1} " +
					"blocker={2}#{3} detector={4} weapon={5} revealed={6} planned-decloak={7} " +
					"canonical-threat={8:0.###} resource={9}.",
					owner.StealthProfile, owner.World.WorldTick, owner.TargetActor?.Info.Name ?? "none",
					owner.TargetActor?.ActorID ?? 0, detectorExposure, weaponExposure, revealed, plannedDecloak,
					maximumCanonicalThreat, resourceHazard);

			if (!IssueStealthEscape(owner, decisionUnits,
				destination.Value, pendingBlueExplosion))
				return;
			if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
			{
				var from = new CPos(representative.Location.X / StealthCoarseSize(owner),
					representative.Location.Y / StealthCoarseSize(owner));
				var to = new CPos(destination.Value.X / StealthCoarseSize(owner),
					destination.Value.Y / StealthCoarseSize(owner));
				var destinationResources = CoarseCellResourceHazards(owner, to);
				Log.Write("debug", "Stealth safety [{0}] escaping one 6x6 strategic cell: tick={1} " +
					"from={2} to={3} delta={4},{5} destination={6} detector={7} weapon={8} " +
					"revealed={9} planned-decloak={10} canonical-threat={11:0.###} red-tiberium={12} " +
					"pending-blue-explosion={13} destination-blue={14} destination-red={15} " +
					"destination-pending={16} destination-forbidden-resource={17} order-batches=1.",
					owner.StealthProfile, owner.World.WorldTick, from, to, to.X - from.X, to.Y - from.Y,
					destination.Value, detectorExposure, weaponExposure, revealed, plannedDecloak,
					maximumCanonicalThreat, resourceHazard, pendingBlueExplosion,
					destinationResources.Blue, destinationResources.Red,
					destinationResources.Pending, destinationResources.Blue || destinationResources.Red ||
						destinationResources.Pending);
			}
		}

		internal static bool TickStealthRevealedIdleSafety(Squad owner, out bool repositionIssued)
		{
			repositionIssued = false;
			var threatened = AirDecisionUnits(owner).Where(unit => unit != null && !unit.IsDead &&
				unit.IsInWorld && unit.Info.Name == "stnk" && unit.IsIdle &&
				!owner.AirUnitsRepairing.Contains(unit.ActorID) &&
				unit.TraitsImplementing<Cloak>().Any(cloak => !cloak.Cloaked) &&
				LivePlannedDecloakThreatCoversPosition(owner, unit, unit.CenterPosition, out _))
				.OrderBy(unit => unit.ActorID).ToArray();
			if (threatened.Length == 0)
				return true;

			foreach (var member in threatened)
				owner.Bot.QueueOrder(new Order("Stop", member, false));

			owner.AirRoute.Clear();
			owner.AirRouteQueued = false;
			owner.AirNextTargetReviewTick = Math.Min(
				owner.AirNextTargetReviewTick, owner.World.WorldTick);
			if (BeginStealthSafetyReposition(owner))
			{
				repositionIssued = true;
				return true;
			}

			ClearAaTargetContext(owner);
			owner.TargetActor = null;
			owner.FuzzyStateMachine.ChangeState(owner, new StealthAIIdleState(), true);
			return false;
		}

		protected static bool BeginStealthSafetyReposition(Squad owner)
		{
			var activeMembers = AirDecisionUnits(owner);
			var representative = activeMembers.OrderBy(a => a.ActorID).FirstOrDefault();
			var destination = representative == null ? null : NearestLiveStealthEscape(
				owner, activeMembers, representative, originCell: ActiveStealthCenterCell(owner));
			if (destination == null)
			{
				foreach (var member in activeMembers.Where(unit => !unit.IsDead && unit.IsInWorld))
					owner.Bot.QueueOrder(new Order("Stop", member, false));
				owner.AirRoute.Clear();
				owner.AirRouteQueued = false;
				if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth safety [{0}] rejected all common adjacent-cell flanks: " +
						"tick={1} members=[{2}] action=hold-and-replan " +
						"authority=per-member-live-world-standard-calculator.", owner.StealthProfile,
						owner.World.WorldTick, activeMembers.OrderBy(unit => unit.ActorID).Select(unit =>
							$"{unit.Info.Name}#{unit.ActorID}@{unit.Location}").JoinWith(","));
				return false;
			}

			return IssueStealthEscape(owner, activeMembers, destination.Value, false);
		}

		protected static bool BeginStealthEnemyApproach(Squad owner)
		{
			var representative = AirDecisionUnits(owner).OrderBy(a => a.ActorID).FirstOrDefault();
			var cache = representative == null ? null : StealthInfluence(owner, representative);
			if (cache == null)
				return false;

			var size = StealthCoarseSize(owner);
			var targets = cache.Candidates.Select(candidate => new CPos(
				candidate.Actor.Location.X / size, candidate.Actor.Location.Y / size))
				.Distinct().OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToList();
			var destination = targets.Count == 0 ? null : NearestSafeStealthNeighbor(
				owner, representative, cache, originCell: ActiveStealthCenterCell(owner),
				approachTargets: targets);
			if (destination == null)
				return false;

			if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Stealth target approach [{0}] tick={1}: frontier-cells={2}/10 " +
					"destination={3} target-cells={4} scope=adjacent-safe-cell.", owner.StealthProfile,
					owner.World.WorldTick, owner.StealthLastFrontierTargetCells, destination.Value,
					targets.Count);

			return IssueCachedStealthStrategicStep(owner, AirDecisionUnits(owner), destination.Value);
		}

		static bool CoarseCellHasForbiddenResource(Squad owner, CPos coarse, bool includePending)
		{
			var hazards = CoarseCellResourceHazards(owner, coarse);
			return hazards.Blue || hazards.Red || (includePending && hazards.Pending);
		}

		static (bool Blue, bool Red, bool Pending) CoarseCellResourceHazards(Squad owner, CPos coarse)
		{
			var resourceLayer = owner.World.WorldActor.TraitOrDefault<IResourceLayer>();
			if (resourceLayer == null)
				return (false, false, false);

			var size = StealthCoarseSize(owner);
			var blue = false;
			var red = false;
			var pending = false;
			for (var y = 0; y < size; y++)
				for (var x = 0; x < size; x++)
				{
					var cell = new CPos(coarse.X * size + x, coarse.Y * size + y);
					if (!owner.World.Map.Contains(cell))
						continue;
					var resource = resourceLayer.GetResource(cell).Type;
					blue |= resource == "BlueTiberium";
					red |= resource == "RedTiberium";
					pending |= resourceLayer.IsExplosionPending(cell);
				}

			return (blue, red, pending);
		}

		static CPos? SafeRetreatCellNearTarget(Squad owner, StealthInfluenceCache cache, Actor target)
		{
			var size = StealthCoarseSize(owner);
			var center = new CPos(target.Location.X / size, target.Location.Y / size);
			return Enumerable.Range(-1, 3).SelectMany(y => Enumerable.Range(-1, 3)
				.Select(x => new CPos(center.X + x, center.Y + y)))
				.Where(c => c.X >= 0 && c.Y >= 0 && c.X < cache.Width && c.Y < cache.Height &&
					!StealthAISpecialistPolicy.IsHardRouteDanger(cache.Danger[c.Y * cache.Width + c.X]) &&
					!CoarseCellHasForbiddenResource(owner, c, true))
				.OrderBy(c => cache.Danger[c.Y * cache.Width + c.X])
				.ThenByDescending(c => (c - center).LengthSquared)
				.Cast<CPos?>().FirstOrDefault();
		}

		static bool CanCrushTarget(Actor unit, Actor target)
		{
			var mobile = unit.TraitOrDefault<Mobile>();
			return mobile != null && target.TraitsImplementing<ICrushable>()
				.Any(c => c.CrushableBy(target, unit, mobile.Info.LocomotorInfo.Crushes));
		}

		protected static Actor StealthCrushLeader(Squad owner, IEnumerable<Actor> formation, Actor target)
		{
			var eligible = formation.Where(unit => !unit.IsDead && unit.IsInWorld &&
				!owner.AirUnitsRepairing.Contains(unit.ActorID) && CanCrushTarget(unit, target)).ToArray();
			var leader = eligible.FirstOrDefault(unit => unit.ActorID == owner.StealthCrushLeaderActorId) ??
				eligible.OrderBy(unit => (unit.CenterPosition - target.CenterPosition).LengthSquared)
					.ThenBy(unit => unit.ActorID).FirstOrDefault();
			owner.StealthCrushLeaderActorId = leader?.ActorID ?? 0;
			return leader;
		}

		static AirTargetPlan TryStealthClearPlan(Squad owner, StealthInfluenceCache cache,
			Actor representative, Actor wanted, int score, List<Actor> package)
		{
			var definition = owner.StealthDefinition;
			var clearCenter = new CPos(wanted.Location.X / StealthCoarseSize(owner),
				wanted.Location.Y / StealthCoarseSize(owner));
			var formation = owner.AirFormationUnits(bootstrapIfEmpty: true)
				.Where(a => !a.IsDead && a.IsInWorld).OrderBy(a => a.ActorID).ToList();
			if (definition == null || formation.Count == 0 || package.Count == 0)
				return null;
			var liveThreats = LiveHostileGroundThreats(owner);

			var retreatCell = SafeRetreatCellNearTarget(owner, cache, wanted);
			if (definition.EnableKiting)
			{
				foreach (var defender in package.Where(a => a.Info.HasTraitInfo<MobileInfo>() &&
					formation.Any(unit => CanAttackTarget(unit, a)) &&
					!(definition.CrushInfantryTargets &&
						a.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes) &&
						!a.TraitsImplementing<DetectCloaked>().Any(d => !d.IsTraitDisabled)))
					.OrderBy(a => StealthAISpecialistPolicy.CachedLocalKiteOrderKey(
						(a.CenterPosition - owner.AirFormationCenter).LengthSquared,
						(long)Math.Round(Math.Max(0, ThreatValue(owner, formation, a)) * 1000),
						StealthPriority(owner, a), a.ActorID)))
				{
					var enemyThreat = LiveGroundThreat(defender);

					var ownSpeed = formation.Min(CurrentGroundSpeed);
					var ownRange = formation.Min(unit => GroundWeaponRange(unit, defender));
					if (!StealthAISpecialistPolicy.CanKite(ownSpeed, enemyThreat.Speed, ownRange,
						enemyThreat.WeaponRange, definition.KiteRangeMarginCells,
						definition.MinimumKiteSpeedPercent))
					{
						if (Game.Settings.Debug.BotDebug)
							Log.Write("debug", "Stealth Kite decision [{0}] tick={1}: target={2}#{3} " +
								"verdict=reject reason=mobility-or-range own-speed={4} threat-speed={5} " +
								"own-range={6} threat-range={7}.", owner.StealthProfile,
								owner.World.WorldTick, defender.Info.Name, defender.ActorID, ownSpeed,
								enemyThreat.Speed, ownRange, enemyThreat.WeaponRange);
						continue;
					}

					var minimumRange = Math.Max(enemyThreat.WeaponRange + definition.KiteRangeMarginCells,
						StealthAISpecialistPolicy.BufferedRange(enemyThreat.DetectorRange,
							definition.DetectorRangeBufferCells));
					if (minimumRange >= ownRange)
					{
						if (Game.Settings.Debug.BotDebug)
							Log.Write("debug", "Stealth Kite decision [{0}] tick={1}: target={2}#{3} " +
								"verdict=reject reason=no-legal-range-band minimum-range={4} own-range={5} " +
								"detector-range={6}.", owner.StealthProfile, owner.World.WorldTick,
								defender.Info.Name, defender.ActorID, minimumRange, ownRange,
								enemyThreat.DetectorRange);
						continue;
					}

					var mobile = representative.TraitOrDefault<Mobile>();
					var firingCandidates = owner.World.Map.FindTilesInAnnulus(defender.Location,
						Math.Max(1, minimumRange), ownRange)
						.Where(c => mobile.CanEnterCell(c, null, BlockedByActor.Immovable))
						.OrderBy(c => (owner.World.Map.CenterOfCell(c) - owner.AirFormationCenter).LengthSquared)
						.ThenBy(c => c.Y).ThenBy(c => c.X).ToList();
					CPos? firingCell = null;
					List<CPos> route = null;
					foreach (var candidate in firingCandidates)
					{
						var candidatePosition = owner.World.Map.CenterOfCell(candidate);
						if (formation.Any(unit => LiveKitePositionIsCovered(
							owner, unit, defender, candidatePosition)))
						{
							if (Game.Settings.Debug.BotDebug)
								Log.Write("debug", "Stealth Kite firing candidate [{0}] tick={1}: " +
									"target={2}#{3}@{4}:cell={5} candidate={6} " +
									"live-guard-covered=True guards=[{7}] verdict=reject.",
									owner.StealthProfile, owner.World.WorldTick, defender.Info.Name,
									defender.ActorID, defender.Owner.InternalName, defender.Location,
									candidate, LiveKiteCoveringThreatSummary(
										owner, formation, defender, candidatePosition));
							continue;
						}

						var candidateRoute = LiveKiteFiringRoute(owner, formation, defender, candidate);
						if (candidateRoute == null)
							continue;

						firingCell = candidate;
						route = candidateRoute;
						break;
					}
					if (firingCell == null)
					{
						if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
							Log.Write("debug", "Stealth reveal safety [{0}] rejected Kite: tick={1} " +
								"target={2}#{3} reason=no-safe-firing-cell retreat={4} minimum-range={5} " +
								"own-range={6} cached-non-target-threats={7}.",
								owner.StealthProfile, owner.World.WorldTick, defender.Info.Name, defender.ActorID,
								retreatCell, minimumRange, ownRange,
								CachedRevealThreatSummary(liveThreats, defender));

						// A cached non-detecting infantry escort can cover the entire legal tank firing
						// annulus. Safely crush that blocker while cloaked, then keep the latched package
						// so the next clear tick backs out to a legal Kite cell for the same tank.
						var crushBlocker = definition.CrushInfantryTargets ? package
							.Select(LiveGroundThreat)
							.Where(threat => threat.Actor != defender &&
								threat.Actor.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes) &&
								!threat.Actor.TraitsImplementing<DetectCloaked>()
									.Any(detector => !detector.IsTraitDisabled) &&
								formation.All(unit => CanCrushTarget(unit, threat.Actor)))
							.OrderBy(threat => (threat.Actor.CenterPosition - defender.CenterPosition).LengthSquared)
							.ThenBy(threat => threat.Actor.ActorID).Select(threat => threat.Actor).FirstOrDefault() : null;
						var bridgeFormationCloaked = formation.All(unit =>
							unit.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked));
						var crushRoute = crushBlocker == null || !bridgeFormationCloaked ? null :
							SafeRouteForStealth(owner, representative, crushBlocker);
						if (crushRoute != null && CloakedCrushRouteIsSafe(owner, crushRoute) &&
							OrdinaryCrushExposureIsSafe(
							owner, crushBlocker, retreatCell))
						{
							if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
								Log.Write("debug", "Stealth crush bridge [{0}] selected live blocker: tick={1} " +
									"blocked-kite={2}#{3} blocker={4}#{5} detector=False route=safe " +
									"exposure=safe next=backoff-and-kite.",
									owner.StealthProfile, owner.World.WorldTick, defender.Info.Name,
									defender.ActorID, crushBlocker.Info.Name, crushBlocker.ActorID);
							var crushBridge = new AirTargetPlan(crushBlocker, score, false, crushRoute,
								stealthMode: StealthClearMode.CrushBridge,
								stealthPackage: package.Select(actor => actor.ActorID).ToArray(),
								stealthClearCenterCell: clearCenter);
							crushBridge.ServiceMilliseconds = StealthMissionServiceMilliseconds(
								owner, representative, formation, crushBridge);
							if (StealthAISpecialistPolicy.IsWithinUndefendedTravelPreference(
								crushBridge.ServiceMilliseconds,
								definition.MaximumUndefendedTargetTravelSeconds))
								return crushBridge;
						}
						continue;
					}

					var kite = new AirTargetPlan(defender, score, false, route,
						stealthMode: StealthClearMode.Kite,
						stealthPackage: package.Select(a => a.ActorID).ToArray(),
						stealthClearCenterCell: clearCenter);
					kite.ServiceMilliseconds = StealthMissionServiceMilliseconds(
						owner, representative, formation, kite);
					if (StealthAISpecialistPolicy.IsWithinUndefendedTravelPreference(
						kite.ServiceMilliseconds, definition.MaximumUndefendedTargetTravelSeconds))
					{
						if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
							Log.Write("debug", "Stealth Kite plan [{0}] selected: tick={1} " +
								"representative={2}#{3} formation={4} members=[{5}] target={6}#{7} " +
								"target-cell={8} firing-cell={9} retreat={10} minimum-range={11} own-range={12} " +
								"route-waypoints={13} shared-route=True focus-fire=True service-ms={14}.",
								owner.StealthProfile, owner.World.WorldTick, representative.Info.Name,
								representative.ActorID, formation.Count, formation.Select(unit =>
									unit.Info.Name + "#" + unit.ActorID).JoinWith(","), defender.Info.Name,
								defender.ActorID, defender.Location, firingCell.Value, retreatCell,
								minimumRange, ownRange, route.Count, kite.ServiceMilliseconds);
						return kite;
					}
					if (Game.Settings.Debug.BotDebug)
						Log.Write("debug", "Stealth Kite decision [{0}] tick={1}: target={2}#{3} " +
							"firing-cell={4} retreat={5} route-waypoints={6} service-ms={7} " +
							"verdict=reject reason=distance-window.", owner.StealthProfile,
							owner.World.WorldTick, defender.Info.Name, defender.ActorID, firingCell.Value,
							retreatCell, route.Count, kite.ServiceMilliseconds);
				}
			}

			var crushableInfantryRemain = definition.CrushInfantryTargets && package.Any(a =>
				a.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes) &&
				!a.TraitsImplementing<DetectCloaked>().Any(d => !d.IsTraitDisabled) &&
				formation.All(unit => CanCrushTarget(unit, a)));
			var armedVehicleRemains = package.Any(a =>
			{
				var threat = LiveGroundThreat(a);
				return !a.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes) &&
					(threat.WeaponRange > 0 || threat.DetectorRange > 0);
			});
			var formationCloaked = formation.All(unit =>
				unit.TraitsImplementing<Cloak>().Any(cloak => cloak.Cloaked));
			var crushableLiveCoverage = package.Where(actor =>
				actor.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes) &&
				formation.All(unit => CanCrushTarget(unit, actor)))
				.Select(actor => $"{actor.Info.Name}#{actor.ActorID}:detector-covered=" +
					liveThreats.Any(threat => ThreatCoversPosition(threat, actor.CenterPosition,
						false, definition.DetectorRangeBufferCells))).JoinWith(",");
			if (Game.Settings.Debug.BotDebug && (crushableInfantryRemain ||
				package.Any(actor => actor.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes))))
				Log.Write("debug", "Stealth crush decision [{0}] tick={1}: considered={2} " +
					"crushable-undetected={3} detecting-infantry={4} armed-vehicle={5} " +
					"formation-cloaked={6} crushable-live=[{7}] verdict={8}.", owner.StealthProfile,
					owner.World.WorldTick, definition.CrushInfantryTargets,
					package.Count(actor => actor.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes) &&
						!actor.TraitsImplementing<DetectCloaked>().Any(detector => !detector.IsTraitDisabled) &&
						formation.All(unit => CanCrushTarget(unit, actor))),
					package.Count(actor => actor.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes) &&
						actor.TraitsImplementing<DetectCloaked>().Any(detector => !detector.IsTraitDisabled)),
					armedVehicleRemains, formationCloaked, crushableLiveCoverage,
					!crushableInfantryRemain ? "reject-no-eligible-undetected-infantry" :
					armedVehicleRemains ? "reject-armed-vehicle-remains" :
					!formationCloaked ? "reject-formation-revealed" : "evaluate-safe-route");
			if (crushableInfantryRemain && !armedVehicleRemains && !formationCloaked)
				return null;

			if (crushableInfantryRemain && !armedVehicleRemains && formationCloaked)
			{
				var crush = package.Where(a => a.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes) &&
					!a.TraitsImplementing<DetectCloaked>().Any(d => !d.IsTraitDisabled) &&
					formation.All(unit => CanCrushTarget(unit, a)))
					.OrderBy(a => retreatCell == null ? 0 : (a.Location - new CPos(
						retreatCell.Value.X * StealthCoarseSize(owner),
						retreatCell.Value.Y * StealthCoarseSize(owner))).LengthSquared)
					.ThenBy(a => a.ActorID).FirstOrDefault();
				if (crush != null)
				{
					var route = SafeRouteForStealth(owner, representative, crush);
					var crushFormationCloaked = false;
					var targetDetectorCovered = false;
					var nextCellDetectorCovered = false;
					var routeDetectorSafe = route != null && CloakedCrushRouteIsSafe(owner, route);
					var exposureSafe = OrdinaryCrushExposureIsSafe(
						owner, crush, retreatCell, out crushFormationCloaked,
						out targetDetectorCovered, out nextCellDetectorCovered) && routeDetectorSafe;
					if (exposureSafe)
					{
						var crushPlan = new AirTargetPlan(crush, score, false, route,
							stealthMode: StealthClearMode.Crush,
							stealthPackage: package.Select(a => a.ActorID).ToArray(),
							stealthClearCenterCell: clearCenter);
						crushPlan.ServiceMilliseconds = StealthMissionServiceMilliseconds(
							owner, representative, formation, crushPlan);
						var bounded = StealthAISpecialistPolicy.ShouldUseBoundedCrush(
							crushPlan.ServiceMilliseconds,
							definition.MaximumUndefendedTargetTravelSeconds);
						if (Game.Settings.Debug.BotDebug)
							Log.Write("debug", "Stealth crush decision [{0}] tick={1}: target={2}#{3} " +
							"target-detector-covered=False next-cell-detector-covered=False " +
							"formation-cloaked=True route-detector-safe=True route=safe exposure=safe " +
							"service-ms={4} verdict={5}.",
								owner.StealthProfile, owner.World.WorldTick, crush.Info.Name, crush.ActorID,
								crushPlan.ServiceMilliseconds, bounded ? "selected" : "reject-distance");
						if (bounded)
							return crushPlan;

						if (owner.SquadManager.Info.AirTargetDebugLogging)
							Log.Write("debug", "Stealth crush [{0}] rejected distant pursuit: tick={1} " +
								"target={2}#{3} service-ms={4} local-limit-seconds={5}.",
								owner.StealthProfile, owner.World.WorldTick, crush.Info.Name, crush.ActorID,
								crushPlan.ServiceMilliseconds,
								definition.MaximumUndefendedTargetTravelSeconds);
					}
					else if (Game.Settings.Debug.BotDebug)
						Log.Write("debug", "Stealth crush decision [{0}] tick={1}: target={2}#{3} " +
							"route={4} route-detector-safe={5} exposure={6} formation-cloaked={7} " +
							"target-detector-covered={8} next-cell-detector-covered={9} retreat={10} " +
							"verdict=reject-revealed-detector-or-route.",
							owner.StealthProfile, owner.World.WorldTick, crush.Info.Name, crush.ActorID,
							route == null ? "unavailable" : "safe", routeDetectorSafe, exposureSafe,
							crushFormationCloaked, targetDetectorCovered, nextCellDetectorCovered,
							retreatCell?.ToString() ?? "none");
				}
			}

			var overmatch = CrossoverOvermatch(owner, formation, package);
			var massApproved = StealthAISpecialistPolicy.ShouldEnterMassClear(
				overmatch, definition.MassClearEntryCrossoverPercent);
			if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
				Log.Write("debug", "Stealth crossover approval [{0}] tick={1}: mode=mass " +
					"target={2}#{3} overmatch={4:0.###} entry-percent={5} detectors={6} " +
					"decloak-attack-approved={7}.", owner.StealthProfile, owner.World.WorldTick,
					wanted.Info.Name, wanted.ActorID, overmatch,
					definition.MassClearEntryCrossoverPercent,
					package.Count(actor => actor.TraitsImplementing<DetectCloaked>()
						.Any(detector => !detector.IsTraitDisabled)), massApproved);
			if (!massApproved)
				return null;

			var threatTarget = HighestThreatActor(owner, formation, package);
			var aggressiveMass = StealthAISpecialistPolicy.ShouldEnterAggressiveMass(overmatch);
			var massRoute = threatTarget == null ? null : MassClearRoute(owner, representative,
				threatTarget);
			return threatTarget == null || massRoute == null ? null : new AirTargetPlan(
				threatTarget, score, false, massRoute, stealthMode: StealthClearMode.Mass,
				stealthPackage: package.Select(a => a.ActorID).ToArray(),
				stealthClearCenterCell: clearCenter,
				stealthAggressiveMass: aggressiveMass);
		}

		protected static bool ContinueOrAbortMassClear(Squad owner, StealthInfluenceCache cache,
			IReadOnlyList<Actor> formation, bool victimInvalid)
		{
			if (owner.StealthClearMode != StealthClearMode.Mass)
				return false;

			var clearCenter = owner.StealthClearCenterCell;
			var package = LiveLatchedDefenderPackage(owner);
			var signature = PackageSignature(formation, package);
			var overmatch = CrossoverOvermatch(owner, formation, package);
			if (overmatch < 0)
				overmatch = double.MaxValue;
			if (package.Count == 0)
			{
				if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth mass [{0}] cleared empty cached package at tick={1}; " +
						"transition-reason=cell-clear/package-empty; same-tick mission reacquisition, no completion retreat.",
						owner.StealthProfile, owner.World.WorldTick);
				ClearAaTargetContext(owner);
				owner.TargetActor = null;
				owner.AirTargetStrategicCell = null;
				owner.AirRoute.Clear();
				owner.AirRouteQueued = false;
				return false;
			}

			if (StealthAISpecialistPolicy.ShouldAbortMassClear(
				overmatch, owner.StealthDefinition.MassClearAbortCrossoverPercent))
			{
				if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth mass [{0}] abort-flee: tick={1} overmatch={2:0.###} " +
						"package={3} squad={4} transition-reason=crossover-exit-threshold threshold={5}.",
						owner.StealthProfile, owner.World.WorldTick,
						overmatch, package.Count, formation.Count,
						owner.StealthDefinition.MassClearAbortCrossoverPercent / 100d);
				ClearAaTargetContext(owner);
				owner.TargetActor = null;
				BeginStealthSafetyReposition(owner);
				return true;
			}

			var wasAggressiveMass = owner.StealthAggressiveMass;
			owner.StealthAggressiveMass = StealthAISpecialistPolicy.ShouldEnterAggressiveMass(overmatch);
			if (wasAggressiveMass && !owner.StealthAggressiveMass)
			{
				if (Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth mass [{0}] transition: tick={1} " +
						"transition-reason=aggressive-threshold-exit-to-ordinary overmatch={2:0.###}.",
						owner.StealthProfile, owner.World.WorldTick, overmatch);
				// Leaving >5 immediately restores the full ordinary hierarchy instead of
				// retaining the previous Mass victim or inventing a special downgrade tier.
				ClearAaTargetContext(owner);
				owner.TargetActor = null;
				owner.AirTargetStrategicCell = null;
				owner.AirRoute.Clear();
				owner.AirRouteQueued = false;
				return false;
			}

			if (!victimInvalid && signature == owner.StealthClearMembershipSignature)
				return false;

			var target = HighestThreatActor(owner, formation, package);
			var route = target == null || clearCenter == null ? null :
				MassClearRoute(owner, formation[0], target);
			if (target == null || route == null)
			{
				if (Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth mass [{0}] transition: tick={1} " +
						"transition-reason=no-live-target-or-route target={2} route={3}.",
						owner.StealthProfile, owner.World.WorldTick,
						target == null ? "none" : target.Info.Name + "#" + target.ActorID,
						route == null ? "none" : "available");
				ClearAaTargetContext(owner);
				owner.TargetActor = null;
				BeginStealthSafetyReposition(owner);
				return true;
			}

			ApplyAirTargetPlan(owner, new AirTargetPlan(target, owner.AirTargetScore, false, route,
				stealthMode: StealthClearMode.Mass,
				stealthPackage: package.Select(a => a.ActorID).ToArray(),
				stealthClearCenterCell: clearCenter,
				stealthAggressiveMass: owner.StealthAggressiveMass));
			if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Stealth mass [{0}] recalc: tick={1} overmatch={2:0.###} " +
					"package={3} squad={4} victim={5}#{6} threat={7:0.###}.", owner.StealthProfile,
					owner.World.WorldTick, overmatch, package.Count, formation.Count,
					target.Info.Name, target.ActorID, ThreatValue(owner, formation, target));
			return !owner.StealthAggressiveMass;
		}

		protected static bool ContinueStealthClear(Squad owner, StealthInfluenceCache cache,
			IReadOnlyList<Actor> formation)
		{
			if (owner.StealthClearMode == StealthClearMode.None || formation.Count == 0)
				return false;
			var finishedKiteDefender = owner.StealthClearMode == StealthClearMode.Kite &&
				!owner.IsTargetValid;
			var finishedCrushBridge = owner.StealthClearMode == StealthClearMode.CrushBridge &&
				!owner.IsTargetValid;
			if (finishedCrushBridge && owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Stealth crush bridge [{0}] outcome=completed-backoff-kite: tick={1} " +
					"completed cached blocker: " +
					"blocker={2}#{3} next=legal-band-backoff.", owner.StealthProfile,
					owner.World.WorldTick, owner.TargetActor.Info.Name, owner.TargetActor.ActorID);
			if (owner.SquadManager.Info.AirTargetDebugLogging &&
				finishedKiteDefender && owner.TargetActor != null &&
				owner.TargetActor.IsDead && owner.TargetActor.Info.Name == "mtnk")
			{
				var participants = owner.StealthKiteParticipantHealth.OrderBy(entry => entry.Key)
					.Select(entry =>
					{
						var actor = owner.World.GetActorById(entry.Key);
						var unchanged = actor != null && !actor.IsDead && actor.IsInWorld &&
							(actor.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue) >= entry.Value;
						return $"{entry.Key}:{(unchanged ? "unchanged" : "damaged-or-lost")}";
					}).ToArray();
				var unchangedParticipants = participants.Count(entry =>
					entry.EndsWith(":unchanged", StringComparison.Ordinal));
				Log.Write("debug", "Stealth kite [{0}] completed owned MTNK lifecycle: tick={1} " +
					"target=mtnk#{2} participants={3} zero-damage-participants={4} zero-damage={5}.", owner.StealthProfile,
					owner.World.WorldTick, owner.TargetActor.ActorID, participants.JoinWith(","),
					unchangedParticipants, unchangedParticipants > 0);
			}
			if (finishedKiteDefender)
			{
				if (Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth owned engagement watchdog [{0}] tick={1}: " +
						"mode=Kite target={2}#{3} decision=abandon reason=target-dead-or-invalid " +
						"clear-cell={4} package={5} next=reacquire.", owner.StealthProfile,
						owner.World.WorldTick, owner.TargetActor?.Info.Name ?? "none",
						owner.TargetActor?.ActorID ?? 0, owner.StealthClearCenterCell?.ToString() ?? "none",
						owner.StealthClearPackage.Count);

				// Strategic value owns the mission cell; the selected package defender owns only
				// this Kite lifecycle. Once that defender is dead or otherwise invalid, release
				// the package latch so the existing cached planner can re-evaluate the mission.
				ClearAaTargetContext(owner);
				owner.TargetActor = null;
				return false;
			}
			if (owner.StealthClearMode == StealthClearMode.Crush && owner.IsTargetValid)
			{
				var distanceCells = (owner.TargetActor.CenterPosition - owner.AirFormationCenter).Length / 1024;
				var targetHP = owner.TargetActor.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue;
				if (owner.SquadManager.Info.AirTargetDebugLogging)
				{
					var leader = StealthCrushLeader(owner, formation, owner.TargetActor);
					Log.Write("debug", "Stealth crush trace [{0}] tick={1} leader={2}#{3} " +
						"leader-cell={4} activity={5} next={6} target={7}#{8} target-cell={9} " +
						"distance={10} route-queued={11} progress-age={12} cadence-age={13}.",
						owner.StealthProfile, owner.World.WorldTick, leader?.Info.Name ?? "none",
						leader?.ActorID ?? 0, leader?.Location.ToString() ?? "none",
						leader?.CurrentActivity?.GetType().Name ?? "none",
						leader?.CurrentActivity?.NextActivity?.GetType().Name ?? "none",
						owner.TargetActor.Info.Name, owner.TargetActor.ActorID, owner.TargetActor.Location,
						distanceCells, owner.AirRouteQueued,
						owner.World.WorldTick - owner.AirTargetLastProgressTick,
						owner.StealthKillCadenceAge);
				}
				if (distanceCells + 1 < owner.AirTargetLastDistanceCells || targetHP < owner.AirTargetLastHP)
				{
					owner.AirTargetLastProgressTick = owner.World.WorldTick;
					owner.AirTargetLastDistanceCells = distanceCells;
					owner.AirTargetLastHP = targetHP;
				}
				else if (StealthAIThreatGeometry.ShouldRescanStalledTarget(
					owner.World.WorldTick - owner.AirTargetLastProgressTick,
					owner.SquadManager.Info.AirTargetStallTicks, true))
				{
					var stalledTarget = owner.TargetActor;
					var firingCell = SafeOrdinaryFiringCell(owner, formation[0], cache, stalledTarget);
					var route = firingCell == null ? null : BuildValidatedFiringRoute(
						cache, stalledTarget, firingCell.Value, () =>
							StealthRouteToCell(owner, formation[0], cache,
								CoarseCell(owner, firingCell.Value), stalledTarget));
					if (route != null)
					{
						LogDirectSafeRouteEvidence(owner, cache, stalledTarget,
							firingCell.Value, route, "stalled-fallback");
						if (owner.SquadManager.Info.AirTargetDebugLogging)
							Log.Write("debug", "Stealth clear [{0}] bounded Crush fallback: tick={1} " +
								"target={2}#{3} stalled={4} next=safe-fire route-waypoints={5}.",
								owner.StealthProfile, owner.World.WorldTick, stalledTarget.Info.Name,
								stalledTarget.ActorID, owner.World.WorldTick - owner.AirTargetLastProgressTick,
								route.Count);
						ApplyAirTargetPlan(owner, new AirTargetPlan(stalledTarget,
							owner.AirTargetScore, true, route));
						return true;
					}

					if (owner.SquadManager.Info.AirTargetDebugLogging)
						Log.Write("debug", "Stealth clear [{0}] bounded Crush fallback: tick={1} " +
							"target={2}#{3} stalled={4} next=safety-replan.", owner.StealthProfile,
							owner.World.WorldTick, stalledTarget.Info.Name, stalledTarget.ActorID,
							owner.World.WorldTick - owner.AirTargetLastProgressTick);
					ClearAaTargetContext(owner);
					owner.TargetActor = null;
					BeginStealthSafetyReposition(owner);
					return true;
				}
			}
			if (owner.StealthClearMode == StealthClearMode.Mass)
				return ContinueOrAbortMassClear(owner, cache, formation, !owner.IsTargetValid);

			var package = LiveLatchedDefenderPackage(owner);
			if (package.Count == 0)
			{
				if (Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth owned engagement watchdog [{0}] tick={1}: " +
						"mode={2} target={3}#{4} decision=abandon reason=package-empty " +
						"clear-cell={5} next=reacquire.", owner.StealthProfile, owner.World.WorldTick,
						owner.StealthClearMode, owner.TargetActor?.Info.Name ?? "none",
						owner.TargetActor?.ActorID ?? 0,
						owner.StealthClearCenterCell?.ToString() ?? "none");
				ClearAaTargetContext(owner);
				owner.TargetActor = null;
				return false;
			}

			if (owner.IsTargetValid && owner.StealthClearPackage.Contains(owner.TargetActor.ActorID))
			{
				if (Game.Settings.Debug.BotDebug &&
					owner.World.WorldTick >= owner.StealthEngagementNextReportTick)
				{
					owner.StealthEngagementNextReportTick = owner.World.WorldTick + 250;
					Log.Write("debug", "Stealth owned engagement watchdog [{0}] tick={1}: " +
						"mode={2} target={3}#{4} target-cell={5} decision=retain " +
						"reason=approved-actor-in-live-package route-queued={6} activity={7} " +
						"clear-cell={8} package={9}.", owner.StealthProfile, owner.World.WorldTick,
						owner.StealthClearMode, owner.TargetActor.Info.Name, owner.TargetActor.ActorID,
						owner.TargetActor.Location, owner.AirRouteQueued,
						formation[0].CurrentActivity?.GetType().Name ?? "none",
						owner.StealthClearCenterCell?.ToString() ?? "none", package.Count);
				}
				// Successful damage is engagement progress, not target invalidation. The owned actor
				// remains latched here; the bounded live-target service separately revalidates its
				// current actor, Kite geometry, firing band, route, and covering threats.
				return false;
			}

			var wanted = package[0];
			var clearCenter = owner.StealthClearCenterCell;
			var plan = TryStealthClearPlan(owner, cache, formation[0], wanted,
				owner.AirTargetScore, package);
			if (plan == null)
			{
				if (Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth owned engagement watchdog [{0}] tick={1}: " +
						"mode={2} target={3}#{4} decision=abandon reason=no-safe-local-plan " +
						"clear-cell={5} package={6} next=safety-reposition.",
						owner.StealthProfile, owner.World.WorldTick, owner.StealthClearMode,
						owner.TargetActor?.Info.Name ?? "none", owner.TargetActor?.ActorID ?? 0,
						clearCenter?.ToString() ?? "none", package.Count);
				ClearAaTargetContext(owner);
				owner.TargetActor = null;
				BeginStealthSafetyReposition(owner);
				return true;
			}

			ApplyAirTargetPlan(owner, plan);
			owner.StealthClearCenterCell = clearCenter;
			if (finishedCrushBridge && plan.StealthMode == StealthClearMode.Kite &&
				owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Stealth crush bridge [{0}] legal-band backoff queued: tick={1} " +
					"kite-target={2}#{3} route-waypoints={4}.", owner.StealthProfile,
					owner.World.WorldTick, plan.Actor.Info.Name, plan.Actor.ActorID, plan.Route?.Count ?? 0);
			return true;
		}

		protected static bool RefreshLiveKiteRoute(Squad owner,
			IReadOnlyList<Actor> formation, Actor target, out bool routeChanged)
		{
			routeChanged = false;
			if (formation.Count == 0 || !IsLiveLocalCombatActor(owner, formation, target))
				return false;
			var threat = LiveGroundThreat(target);

			var definition = owner.StealthDefinition;
			var ownSpeed = formation.Min(CurrentGroundSpeed);
			var ownRange = formation.Min(a => GroundWeaponRange(a, target));
			var minimumRange = Math.Max(threat.WeaponRange + definition.KiteRangeMarginCells,
				StealthAISpecialistPolicy.BufferedRange(threat.DetectorRange,
					definition.DetectorRangeBufferCells));
			if (!definition.EnableKiting || !StealthAISpecialistPolicy.CanKite(
				ownSpeed, threat.Speed, ownRange, threat.WeaponRange,
				definition.KiteRangeMarginCells, definition.MinimumKiteSpeedPercent))
				return false;
			foreach (var unit in formation)
			{
				if (LiveKitePositionIsCovered(owner, unit, target, unit.CenterPosition))
				{
					if (owner.SquadManager.Info.AirTargetDebugLogging)
						Log.Write("debug", "Stealth reveal safety [{0}] aborted Kite: tick={1} " +
							"target={2}#{3} unit={4}#{5} reason=live-non-target-coverage.",
							owner.StealthProfile, owner.World.WorldTick, target.Info.Name, target.ActorID,
							unit.Info.Name, unit.ActorID);
					return false;
				}
			}

			var representative = formation[0];
			if (formation.All(unit =>
			{
				var distance = (unit.CenterPosition - target.CenterPosition).HorizontalLength / 1024f;
				return distance >= minimumRange && distance <= ownRange;
			}))
				return true;

			var targetMoved = owner.StealthKiteTargetCell == null ||
				owner.StealthKiteTargetCell.Value != target.Location;
			var routeTraveling = owner.AirRouteQueued && formation.Any(unit =>
				!unit.IsIdle && !BusyAttack(unit));
			if (!targetMoved && routeTraveling &&
				owner.World.WorldTick - owner.AirTargetLastProgressTick <
				owner.SquadManager.Info.AirTargetStallTicks)
				return true;

			var mobile = representative.TraitOrDefault<Mobile>();
			var firingCandidates = owner.World.Map.FindTilesInAnnulus(target.Location,
				Math.Max(1, minimumRange), ownRange)
				.Where(c => mobile.CanEnterCell(c, null, BlockedByActor.Immovable))
				.OrderBy(c => (owner.World.Map.CenterOfCell(c) - owner.AirFormationCenter).LengthSquared)
				.ThenBy(c => c.Y).ThenBy(c => c.X).ToList();
			CPos? firing = null;
			List<CPos> route = null;
			foreach (var candidate in firingCandidates)
			{
				var candidatePosition = owner.World.Map.CenterOfCell(candidate);
				if (formation.Any(unit => LiveKitePositionIsCovered(owner, unit, target, candidatePosition)))
					continue;

				var candidateRoute = LiveKiteFiringRoute(owner, formation, target, candidate);
				if (candidateRoute == null)
					continue;

				firing = candidate;
				route = candidateRoute;
				break;
			}
			if (firing == null)
				return false;
			owner.AirRoute.Clear();
			owner.AirRoute.AddRange(route);
			owner.AirRouteQueued = false;
			owner.StealthKiteTargetCell = target.Location;
			routeChanged = true;
			return true;
		}

		internal static void TickStealthLiveTarget(Squad owner)
		{
			RecordStealthDebugLifecycle(owner, false);
			if (!owner.IsValid || !owner.IsTargetValid || owner.AirEscapingLocalAa)
				return;

			var formation = owner.AirFormationUnits()
				.Where(actor => !actor.IsDead && actor.IsInWorld).OrderBy(actor => actor.ActorID).ToList();
			if (formation.Count == 0)
				return;

			if (owner.StealthClearMode == StealthClearMode.Crush ||
				owner.StealthClearMode == StealthClearMode.CrushBridge)
			{
				var leader = StealthCrushLeader(owner, formation, owner.TargetActor);
				var crushMove = leader?.TraitOrDefault<EconomyMammothCrushMove>();
				var trackedOrderIsCurrent = crushMove != null &&
					crushMove.IsCurrentOrder(leader, owner.TargetActor);
				var targetCell = owner.TargetActor.Location;
				var targetStrategicCell = new CPos(
					owner.TargetActor.Location.X / StealthCoarseSize(owner),
					owner.TargetActor.Location.Y / StealthCoarseSize(owner));
				var targetChangedCell = owner.StealthCrushTargetCell == null ||
					owner.StealthCrushTargetCell.Value != targetCell;
				var crushCache = leader == null ? null : CachedStealthInfluence(owner, leader);
				var crushExposureSafe = leader != null && crushCache != null &&
					OrdinaryCrushExposureIsSafe(owner, owner.TargetActor,
						SafeRetreatCellNearTarget(owner, crushCache, owner.TargetActor));
				var activeMembers = AirDecisionUnits(owner).Where(actor => !actor.IsDead && actor.IsInWorld).ToArray();
				var liveLocalTarget = IsLiveLocalCombatActor(owner, activeMembers, owner.TargetActor);
				var crushRouteChanged = false;
				if (crushExposureSafe && StealthAISpecialistPolicy.ShouldRefreshQueuedCrushRoute(
					owner.StealthClearMode == StealthClearMode.Crush, owner.AirRouteQueued,
					trackedOrderIsCurrent, liveLocalTarget, targetChangedCell))
				{
					// The tracked actor order can exist at the tail of a static safe route without
					// controlling current motion. Refresh at the existing bounded live cadence after
					// an exact-cell move for the live-revalidated owned target. Once tracking becomes
					// current, the engine owns interception and this hook does not reissue it.
					var route = crushCache == null ? null : StealthRouteToCell(
						owner, leader, crushCache, targetStrategicCell);
					owner.AirTargetStrategicCell = targetStrategicCell;
					owner.StealthCrushTargetCell = targetCell;
					if (route != null && route.Count > 0 &&
						CloakedCrushRouteIsSafe(owner, route))
					{
						if (route[route.Count - 1] != targetCell)
							route.Add(targetCell);
						foreach (var unit in formation)
						{
							var queued = false;
							foreach (var waypoint in route)
							{
								owner.Bot.QueueOrder(new Order("Move", unit,
									Target.FromCell(owner.World, waypoint), queued));
								queued = true;
							}

							if (unit == leader)
								owner.Bot.QueueOrder(new Order(EconomyMammothCrushMove.OrderId, unit,
									Target.FromActor(owner.TargetActor), true));
						}

						owner.AirRouteQueued = formation.Count > 0;
						owner.AirTargetLastProgressTick = owner.World.WorldTick;
						owner.StealthCoreRouteIssues++;
						crushRouteChanged = true;
					}

					if (owner.SquadManager.Info.AirTargetDebugLogging)
						Log.Write("debug", "Stealth live target [{0}] queued Crush intercept: " +
							"tick={1} leader={2}#{3} target={4}#{5} target-cell={6} " +
							"route-waypoints={7} order-changed={8} membership=live detector-safety=live.",
							owner.StealthProfile, owner.World.WorldTick, leader.Info.Name, leader.ActorID,
							owner.TargetActor.Info.Name, owner.TargetActor.ActorID,
							owner.TargetActor.Location, route?.Count ?? 0, crushRouteChanged);
				}

				var orderChanged = crushExposureSafe && !crushRouteChanged && crushMove != null && !owner.AirRouteQueued &&
					crushMove.ShouldIssueOrder(leader, owner.TargetActor);
				if (orderChanged)
					owner.Bot.QueueOrder(new Order(EconomyMammothCrushMove.OrderId, leader,
						Target.FromActor(owner.TargetActor), false));

				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Stealth live target [{0}] Crush check: tick={1} mode={2} " +
						"leader={3}#{4} leader-cell={5} target={6}#{7} target-cell={8} " +
						"route-queued={9} order-changed={10} tracked-order={11} " +
						"tracking-current={12} " +
						"scope=owned-target actor-checks=1 world-scans=0.", owner.StealthProfile,
						owner.World.WorldTick, owner.StealthClearMode, leader?.Info.Name ?? "none",
						leader?.ActorID ?? 0, leader?.Location.ToString() ?? "none",
						owner.TargetActor.Info.Name, owner.TargetActor.ActorID, owner.TargetActor.Location,
						owner.AirRouteQueued, orderChanged || crushRouteChanged, crushMove != null,
						trackedOrderIsCurrent);
				return;
			}

			if (owner.StealthClearMode != StealthClearMode.Kite)
				return;

			// The strategic cadence owns cache rebuilds, target admission, and route topology. Live
			// micro keeps the owned actor but must validate exact firing geometry against live actors.
			var valid = RefreshLiveKiteRoute(owner, formation, owner.TargetActor,
				out var routeChanged);
			if (!valid)
			{
				if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth live target [{0}] Kite check: tick={1} target={2}#{3} " +
						"result=unsafe order-changed=False scope=live-owned-target " +
						"actor-checks=live-hostiles world-scans=1.", owner.StealthProfile, owner.World.WorldTick,
						owner.TargetActor.Info.Name, owner.TargetActor.ActorID);
				BeginStealthSafetyReposition(owner);
				return;
			}

			if (routeChanged)
			{
				foreach (var unit in formation)
				{
					var validatedFiringCell = owner.AirRoute.Count > 0 ?
						owner.AirRoute[owner.AirRoute.Count - 1] : (CPos?)null;
					if (validatedFiringCell != null)
						owner.StealthValidatedFiringCells[unit.ActorID] = validatedFiringCell.Value;
					else
						owner.StealthValidatedFiringCells.Remove(unit.ActorID);
					var queued = false;
					foreach (var waypoint in owner.AirRoute)
					{
						owner.Bot.QueueOrder(new Order("Move", unit,
							Target.FromCell(owner.World, waypoint), queued));
						queued = true;
					}

					var canAttack = CanAttackTarget(unit, owner.TargetActor);
					Actor coveringThreat = null;
					var vetoReason = "not-attackable";
					var withholdAttack = canAttack && ShouldWithholdLivePlannedDecloakEngagement(
						owner, unit, validatedFiringCell, out coveringThreat, out vetoReason);
					if (canAttack && !withholdAttack)
						owner.Bot.QueueOrder(new Order("Attack", unit,
							Target.FromActor(owner.TargetActor), true));
					else if (withholdAttack &&
						(owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug))
						Log.Write("debug", "Stealth live planned-decloak engagement veto [{0}] tick={1}: " +
							"phase=live-kite-issue unit={2}#{3} unit-cell={4} target={5}#{6} " +
							"validated-firing-cell={7} covering-threat={8} reason={9} " +
							"combat-order=withhold safe-route=continue.", owner.StealthProfile,
							owner.World.WorldTick, unit.Info.Name, unit.ActorID, unit.Location,
							owner.TargetActor.Info.Name, owner.TargetActor.ActorID,
							validatedFiringCell?.ToString() ?? "current-live-approved",
							coveringThreat == null ? "none" : coveringThreat.Info.Name + "#" +
								coveringThreat.ActorID, vetoReason);
				}

				owner.AirRouteQueued = formation.Count > 0;
				owner.AirRoute.Clear();
			}

			if ((owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug) &&
				routeChanged)
				Log.Write("debug", "Stealth live target [{0}] Kite check: tick={1} target={2}#{3} " +
					"target-cell={4} order-changed={5} result={6} scope=live-owned-target " +
					"actor-checks=live-hostiles world-scans=1.", owner.StealthProfile,
					owner.World.WorldTick, owner.TargetActor.Info.Name, owner.TargetActor.ActorID,
					owner.TargetActor.Location, routeChanged, routeChanged ? "live-route" : "useful-order");
		}

		static void EmitStealthLiveLocalDiagnosticSummary(Squad owner, string summary)
		{
			if (owner.StealthLiveLocalDiagnosticSamples == 0)
				return;

			var diagnosticAttribution = SquadManagerBotModule.BeginStealthManagerAttributionPhase();
			try
			{
				Log.Write("debug", "Stealth live local package summary [{0}] tick={1}: squad={2}#{3} " +
					"summary={4} samples={5} changes={6} emitted={7} suppressed={8} final-aggregate={9}.",
					owner.StealthProfile, owner.World.WorldTick, owner.StealthSquadDefinition,
					owner.StealthSquadIndex, summary, owner.StealthLiveLocalDiagnosticSamples,
					owner.StealthLiveLocalDiagnosticChanges, owner.StealthLiveLocalDiagnosticEmitted,
					owner.StealthLiveLocalDiagnosticSamples - owner.StealthLiveLocalDiagnosticEmitted,
					summary == "terminal");
				if (Game.Settings.Debug.BotDebug)
					owner.SquadManager.AddStealthManagerAttributionOperations(
						StealthManagerAttributionPhase.DiagnosticEmission, 1);
			}
			finally
			{
				owner.SquadManager.RecordStealthManagerAttributionPhase(
					StealthManagerAttributionPhase.DiagnosticEmission,
					diagnosticAttribution, 0);
			}
		}

		internal static void EmitStealthRecurringDiagnosticSummary(Squad owner, string summary)
		{
			EmitStealthLiveLocalDiagnosticSummary(owner, summary);
		}

		static AirTargetPlan FindBestStealthTarget(Squad owner, Actor incumbent,
			out AirTargetPlan incumbentPlan, CPos? requiredStrategicCell)
		{
			incumbentPlan = null;
			owner.StealthLastFrontierTargetCells = 0;
			var formation = owner.AirFormationUnits(bootstrapIfEmpty: true)
				.Where(actor => !actor.IsDead && actor.IsInWorld).OrderBy(actor => actor.ActorID).ToList();
			var representative = formation.FirstOrDefault();
			if (representative == null)
				return null;

			var cache = StealthInfluence(owner, representative);
			if (cache == null)
				return null;

			var coarseSize = StealthCoarseSize(owner);
			var candidates = cache.Candidates.Where(c => !c.Actor.IsDead &&
				owner.Units.Any(unit => CanAttackTarget(unit, c.Actor))).ToList();
			if (incumbent != null && !incumbent.IsDead && !candidates.Any(c => c.Actor == incumbent) &&
				owner.SquadManager.IsPreferredEnemyUnit(incumbent) &&
				owner.Units.Any(unit => CanAttackTarget(unit, incumbent)))
			{
				var incumbentPriority = StealthPriority(owner, incumbent);
				if (incumbentPriority > 0)
					candidates.Add((incumbent, incumbentPriority));
			}

			if (incumbent != null && !incumbent.IsDead)
				requiredStrategicCell = new CPos(
					incumbent.Location.X / coarseSize, incumbent.Location.Y / coarseSize);

			var groupedCells = candidates.GroupBy(c => new CPos(
				c.Actor.Location.X / coarseSize, c.Actor.Location.Y / coarseSize)).ToList();
			// Lifecycle §3 discovers target cells without a danger or value eligibility gate.
			// Lifecycle §4A and §4B apply the exact comparative halves after discovery.
			var cells = groupedCells.OrderBy(g => g.Key.Y).ThenBy(g => g.Key.X).ToList();
			var requiredIndex = requiredStrategicCell == null ? -1 :
				cells.FindIndex(g => g.Key == requiredStrategicCell.Value);
			var cachedFrontierRoutes = new Dictionary<int, List<CPos>>();
			var cachedFrontierRouteCosts = new Dictionary<int, float>();
			List<int> selectedIndices;
			if (owner.StealthProfile == "stealth-tank")
			{
				var startX = Math.Clamp(representative.Location.X / coarseSize, 0, cache.Width - 1);
				var startY = Math.Clamp(representative.Location.Y / coarseSize, 0, cache.Height - 1);
				var started = Stopwatch.GetTimestamp();
				var frontier = StealthAIThreatGeometry.SelectReachableTargetCells(
					cache.Danger, cache.Width, cache.Height, startX, startY,
					cells.Select(group => group.Key).ToList(), owner.StealthDefinition.RouteThreatPenalty,
					owner.StealthDefinition.OutwardTargetCellLimit, requiredIndex);
				RecordAirPhase(owner, "target-cell-frontier", started);
				selectedIndices = frontier?.Targets.Select(target => target.TargetIndex).ToList() ?? new List<int>();
				owner.StealthLastFrontierTargetCells = selectedIndices.Count;
				if (frontier != null)
					foreach (var target in frontier.Targets)
					{
						var smoothed = ThreatAwareRoutePlanner.SmoothRoute(cache.Danger, cache.Width,
							cache.Height, startX, startY, target.Route);
						if (smoothed == null)
							continue;

						cachedFrontierRoutes[target.TargetIndex] = smoothed.Select(cell =>
							owner.World.Map.Clamp(new CPos(cell.X * coarseSize + coarseSize / 2,
								cell.Y * coarseSize + coarseSize / 2))).ToList();
						cachedFrontierRouteCosts[target.TargetIndex] = target.RouteCost;
					}

				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Stealth target frontier [{0}] tick={1}: start={2},{3} " +
						"target-cells={4}/{5} expanded={6}/{7} incumbent-extra={8} " +
						"scope=cached-6x6 frontier-world-scans=0 target-cell-a-star=0.", owner.StealthProfile,
						owner.World.WorldTick, startX, startY, selectedIndices.Count,
						owner.StealthDefinition.OutwardTargetCellLimit,
						frontier?.ExpandedCells ?? 0, cache.Width * cache.Height,
						frontier?.Targets.Any(target => target.IsRequired) ?? false);
			}
			else
			{
				selectedIndices = StealthAIThreatGeometry.SelectTargetCandidates(
					cells.Select(g => (owner.World.Map.CenterOfCell(owner.World.Map.Clamp(new CPos(
						g.Key.X * coarseSize + coarseSize / 2, g.Key.Y * coarseSize + coarseSize / 2))) -
						representative.CenterPosition).LengthSquared).ToList(),
					cells.Select(g => (int)Math.Min(int.MaxValue, g.Sum(c => (long)c.Priority))).ToList(),
					owner.SquadManager.Info.AirTargetClosestCandidates,
					owner.SquadManager.Info.AirTargetHighestValueCandidates, requiredIndex);
				var harvesterCells = Enumerable.Range(0, cells.Count)
					.Where(i => cells[i].Any(c => c.Actor.Info.HasTraitInfo<HarvesterInfo>()))
					.OrderBy(i => (cells[i].First().Actor.CenterPosition - representative.CenterPosition).LengthSquared)
					.Take(owner.SquadManager.Info.AirTargetHarvesterCandidates);
				selectedIndices = selectedIndices.Concat(harvesterCells).Distinct().OrderBy(i => i).ToList();
			}

			if (owner.StealthProfile == "stealth-tank" && selectedIndices.Count > 0)
			{
				var strategicValues = new List<long>();
				var threatValues = new List<double>();
				var crossoverValues = new List<double>();
				foreach (var selectedIndex in selectedIndices)
				{
					var cell = cells[selectedIndex];
					strategicValues.Add(cell.Sum(candidate =>
					{
						var health = candidate.Actor.TraitOrDefault<IHealth>();
						return StealthAISpecialistPolicy.StrategicTargetValueByRemainingHealth(
							candidate.Priority,
							candidate.Actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0,
							health?.HP ?? 0, health?.MaxHP ?? 0);
					}));
					var defenders = cell.SelectMany(candidate => DefenderPackage(owner, cache, candidate.Actor))
						.Distinct().OrderBy(actor => actor.ActorID).ToList();
					threatValues.Add(defenders.Sum(defender => ThreatValue(owner, formation, defender)));
					crossoverValues.Add(defenders.Count == 0 ? double.PositiveInfinity :
						CrossoverOvermatch(owner, formation, defenders));
				}

				var survivors = StealthAIThreatGeometry.SelectOrderedTargetCellHalf(
					strategicValues, threatValues, crossoverValues);
				var activeLocalCell = CoarseCell(owner, representative.Location);
				var locallyArrived = Enumerable.Range(0, selectedIndices.Count).Where(index =>
				{
					var targetCell = cells[selectedIndices[index]].Key;
					return Math.Abs(targetCell.X - activeLocalCell.X) <= 1 &&
						Math.Abs(targetCell.Y - activeLocalCell.Y) <= 1;
				}).ToList();
				if (owner.SquadManager.Info.AirTargetDebugLogging)
				{
					var strategicHalf = Enumerable.Range(0, selectedIndices.Count)
						.OrderByDescending(index => strategicValues[index]).ThenBy(index => index)
						.Take((selectedIndices.Count + 1) / 2).ToHashSet();
					var survivorSet = survivors.ToHashSet();
					var ranking = Enumerable.Range(0, selectedIndices.Count).Select(index =>
						$"{cells[selectedIndices[index]].Key}:value={strategicValues[index]}:" +
						$"threat={threatValues[index]:0.###}:crossover={crossoverValues[index]:0.###}:" +
						$"stage={(survivorSet.Contains(index) ? "survivor" : strategicHalf.Contains(index) ? "threat-rejected" : "value-rejected")}")
						.JoinWith(",");
					Log.Write("debug", "Stealth target cell filter [{0}] tick={1}: ordered=value-then-threat-then-separation " +
						"frontier={2} strategic-keep={3} threat-keep={4} cells={5}.",
						owner.StealthProfile, owner.World.WorldTick, selectedIndices.Count,
						strategicHalf.Count, survivors.Count, ranking);
				}
				selectedIndices = survivors.Concat(locallyArrived).Distinct()
					.Select(index => selectedIndices[index]).OrderBy(index => index).ToList();
			}

			var safePlans = new List<(AirTargetPlan Plan, long TravelMs, long ServiceMs)>();
			var clearPlans = new List<AirTargetPlan>();
			var strategicCellByPlan = new Dictionary<AirTargetPlan, CPos>();
			var debugPlans = owner.SquadManager.Info.AirTargetDebugLogging ? new List<AirTargetPlan>() : null;
			foreach (var selectedIndex in selectedIndices)
			{
				var cell = cells[selectedIndex];
				var cellSafePlans = new List<(AirTargetPlan Plan, long TravelMs, long ServiceMs, int Priority)>();
				var cellClearPlans = new List<(AirTargetPlan Plan, int Priority)>();
				var safeRoute = cachedFrontierRoutes.TryGetValue(selectedIndex, out var cachedRoute) ?
					cachedRoute : owner.StealthProfile == "stealth-tank" ? null :
					StealthRouteToCell(owner, representative, cache, cell.Key);
				var activeLocalCell = CoarseCell(owner, representative.Location);
				var locallyArrived = owner.StealthProfile == "stealth-tank" &&
					Math.Abs(cell.Key.X - activeLocalCell.X) <= 1 &&
					Math.Abs(cell.Key.Y - activeLocalCell.Y) <= 1;
				// A defended corridor can make the ordinary harassment route unavailable even
				// when the cached 3x3 package has enough crossover for a deliberate Mass clear.
				// Keep a mobility-only route solely to evaluate that existing clear policy; safe
				// targets and Kite/Crush plans still require their normal threat-safe routes.
				var evaluationRoute = safeRoute ?? (locallyArrived ? new List<CPos> { representative.Location } :
					owner.StealthProfile == "stealth-tank" ? null :
					StealthRouteToCell(owner, representative, cache, cell.Key, cache.MobilityDanger));
				if (evaluationRoute == null)
					continue;

				foreach (var candidate in cell.OrderBy(c => c.Actor.ActorID))
				{
					var actor = candidate.Actor;
					var package = DefenderPackage(owner, cache, actor);
					var distance = cachedFrontierRouteCosts.TryGetValue(selectedIndex, out var routeCost) ?
						StealthAISpecialistPolicy.WeightedRouteDistanceCells(routeCost, coarseSize) :
						Math.Max(1, evaluationRoute.Count * coarseSize);
					var baseScore = BoundedStealthTargetUtility(actor,
						BaseTargetUtility(actor, owner.SquadManager.Info, null, 0, candidate.Priority));
					var score = (int)Math.Max(1, baseScore * 1000L /
						(1000 + distance * Math.Max(1, owner.StealthDefinition.HarassmentDistancePenalty) * 10L));
					var firingCell = safeRoute == null ? null :
						SafeOrdinaryFiringCell(owner, representative, cache, actor);
					// A mobile target with any other live armed/detecting local guard needs the
					// live Kite lifecycle. If no live firing cell exists then do not fall back to
					// an ordinary cached decloak plan through that guard's coverage.
					var liveArmedGuards = package.Where(defender => defender != actor)
						.Select(LiveGroundThreat).Any(threat =>
							threat.WeaponRange > 0 || threat.DetectorRange > 0);
					var requiresDynamicKite = package.Contains(actor) &&
						actor.Info.HasTraitInfo<MobileInfo>() && liveArmedGuards;
					var dynamicClear = requiresDynamicKite && (formation.Count == 1 ||
						owner.StealthProfile == "stealth-tank") ? TryStealthClearPlan(
						owner, cache, representative, actor, score, package) : null;
					if (dynamicClear != null)
					{
						dynamicClear.ServiceMilliseconds = StealthMissionServiceMilliseconds(
							owner, representative, formation, dynamicClear);
						cellClearPlans.Add((dynamicClear, candidate.Priority));
						debugPlans?.Add(dynamicClear);
						continue;
					}
					if (requiresDynamicKite)
					{
						if (Game.Settings.Debug.BotDebug)
							Log.Write("debug", "Stealth Kite decision [{0}] tick={1}: target={2}#{3} " +
								"live-armed-guards=True verdict=reject reason=no-safe-live-kite-plan " +
								"ordinary-fallback=False.", owner.StealthProfile, owner.World.WorldTick,
								actor.Info.Name, actor.ActorID);
						continue;
					}

					if (firingCell != null)
					{
						var ignoredActionThreat = cache.ThreatByActor.TryGetValue(actor, out var actionThreat) &&
							StealthAISpecialistPolicy.CanOutrangeUndetectingTarget(
								actionThreat.WeaponRange, actionThreat.DetectorRange,
								GroundWeaponRange(representative, actor)) ? actor : null;
						var postAttackCell = SafePostAttackStrategicCell(
							owner, representative, cache, firingCell.Value);
						var firingCovered = CoveringWeaponAt(owner, cache,
							owner.World.Map.CenterOfCell(firingCell.Value), ignoredActionThreat);
						if (!StealthAISpecialistPolicy.PlannedExposureIsSafe(
							firingCovered, postAttackCell != null, false))
							continue;

						var firingRoute = BuildValidatedFiringRoute(
							cache, actor, firingCell.Value, () =>
								StealthRouteToCell(owner, representative, cache,
									CoarseCell(owner, firingCell.Value), actor));
						if (firingRoute == null)
							continue;
						LogDirectSafeRouteEvidence(owner, cache, actor,
							firingCell.Value, firingRoute, "ordinary-plan");
						var plan = new AirTargetPlan(actor, score, true, firingRoute,
							stealthPostAttackCell: postAttackCell);
						if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
							Log.Write("debug", "Stealth decloak approval [{0}] tick={1}: mode=ordinary " +
								"target={2}#{3} target-priority={4} live-package={5} live-armed-defenders={6} " +
								"firing-cell={7} post-attack-cell={8} route-waypoints={9} " +
								"detector-covered=False decloak-attack-approved=True.", owner.StealthProfile,
								owner.World.WorldTick, actor.Info.Name, actor.ActorID,
								StealthPriority(owner, actor), package.Count, package.Count(defender =>
								{
									var threat = LiveGroundThreat(defender);
									return threat.WeaponRange > 0 || threat.DetectorRange > 0;
								}), firingCell.Value, postAttackCell.Value, firingRoute.Count);
						var travel = RouteTravelMilliseconds(owner, representative, firingRoute, actor);
						var killTicks = EstimatedKillTicks(formation, new[] { actor });
						var rawService = killTicks == long.MaxValue || travel == long.MaxValue ? long.MaxValue :
							Math.Min(long.MaxValue, travel + killTicks * owner.World.Timestep);
						var service = StealthAISpecialistPolicy.CachedMobileServiceMilliseconds(rawService,
							owner.World.Timestep, StealthAISpecialistPolicy.KillCadenceFinishMarginTicks(
								owner.SquadManager.Info.AirInfluenceCacheInterval,
								owner.SquadManager.Info.AirTargetStallTicks),
							actor.Info.HasTraitInfo<MobileInfo>());
						plan.ServiceMilliseconds = service;
						cellSafePlans.Add((plan, travel, service, candidate.Priority));
						debugPlans?.Add(plan);
						continue;
					}

					if (owner.SquadManager.Info.AirTargetDebugLogging && safeRoute != null)
					{
						var local = CachedPackageThreats(cache, DefenderPackage(owner, cache, actor));
						Log.Write("debug", "Stealth reveal safety [{0}] rejected ordinary: tick={1} " +
							"target={2}#{3} reason={4} cached-threats={5}.", owner.StealthProfile,
							owner.World.WorldTick, actor.Info.Name, actor.ActorID, "no-safe-firing-cell",
							CachedRevealThreatSummary(local));
					}

					var clear = TryStealthClearPlan(owner, cache, representative, actor, score, package);
					if (clear == null)
						continue;
					clear.ServiceMilliseconds = StealthMissionServiceMilliseconds(
						owner, representative, formation, clear);
					cellClearPlans.Add((clear, candidate.Priority));
					debugPlans?.Add(clear);
				}

				// Strategic-cell selection chooses where to engage. Preserve an already-approved
				// dynamic Kite/Mass lifecycle through final actor arbitration; otherwise retain
				// configured target priority across the already safety-validated fallback plans.
				var preferred = StealthAISpecialistPolicy.HighestPriorityFinalEngagements(
					cellSafePlans.Select(entry => (entry.Plan, entry.Priority,
						ApprovedDynamicLocal: false))
						.Concat(cellClearPlans.Select(entry => (entry.Plan, entry.Priority,
							ApprovedDynamicLocal: entry.Plan.StealthMode == StealthClearMode.Kite ||
								entry.Plan.StealthMode == StealthClearMode.Mass))))
					.Select(plan => plan.Actor.ActorID).ToHashSet();
				safePlans.AddRange(cellSafePlans.Where(entry => preferred.Contains(entry.Plan.Actor.ActorID))
					.Select(entry => (entry.Plan, entry.TravelMs, entry.ServiceMs)));
				clearPlans.AddRange(cellClearPlans.Where(entry => preferred.Contains(entry.Plan.Actor.ActorID))
					.Select(entry => entry.Plan));
				foreach (var plan in cellSafePlans.Select(entry => entry.Plan)
					.Concat(cellClearPlans.Select(entry => entry.Plan))
					.Where(plan => preferred.Contains(plan.Actor.ActorID)))
					strategicCellByPlan[plan] = cell.Key;
				if (incumbent != null)
					incumbentPlan = cellSafePlans.Select(entry => entry.Plan)
						.Concat(cellClearPlans.Select(entry => entry.Plan))
						.FirstOrDefault(plan => preferred.Contains(plan.Actor.ActorID) && plan.Actor == incumbent)
						?? incumbentPlan;
			}

			var otherStealthTargetCells = owner.SquadManager.Squads.Where(squad => squad != owner &&
				squad.IsValid && squad.Type == SquadType.Stealth &&
				squad.StealthProfile == owner.StealthProfile && squad.IsTargetValid)
				.Select(squad => squad.AirTargetStrategicCell ?? new CPos(
					squad.TargetActor.Location.X / coarseSize, squad.TargetActor.Location.Y / coarseSize))
				.Distinct().OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToList();
			long Separation(AirTargetPlan plan) => StealthAIThreatGeometry.MinimumCellSeparationSquared(
				strategicCellByPlan.TryGetValue(plan, out var strategicCell) ? strategicCell :
					new CPos(plan.Actor.Location.X / coarseSize, plan.Actor.Location.Y / coarseSize),
				otherStealthTargetCells);
			// Kill-cadence watchdog data is deliberately absent from this ordering. It is
			// diagnostic output only and must never change target eligibility or routing.
			AirTargetPlan best = null;
			if (best == null)
				best = clearPlans.Where(p => p.StealthMode == StealthClearMode.CrushBridge)
					.OrderByDescending(Separation).ThenByDescending(p => p.Score)
					.ThenBy(p => p.Actor.ActorID).FirstOrDefault();
			if (best == null)
			{
				var activeLocalCell = CoarseCell(owner, representative.Location);
				best = clearPlans.Where(plan => plan.StealthMode == StealthClearMode.Kite &&
					strategicCellByPlan.TryGetValue(plan, out var planCell) &&
					Math.Abs(planCell.X - activeLocalCell.X) <= 1 &&
					Math.Abs(planCell.Y - activeLocalCell.Y) <= 1)
					.OrderByDescending(plan => StealthPriority(owner, plan.Actor))
					.ThenBy(plan => plan.ServiceMilliseconds).ThenBy(plan => plan.Actor.ActorID)
					.FirstOrDefault();
			}
			if (best == null)
			{
				var activeLocalCell = CoarseCell(owner, representative.Location);
				best = clearPlans.Where(plan => plan.StealthMode == StealthClearMode.Mass &&
					strategicCellByPlan.TryGetValue(plan, out var planCell) &&
					Math.Abs(planCell.X - activeLocalCell.X) <= 1 &&
					Math.Abs(planCell.Y - activeLocalCell.Y) <= 1)
					.OrderByDescending(plan => StealthPriority(owner, plan.Actor))
					.ThenBy(plan => plan.ServiceMilliseconds).ThenBy(plan => plan.Actor.ActorID)
					.FirstOrDefault();
			}
			if (best == null)
				best = safePlans.Where(p =>
					StealthAISpecialistPolicy.IsWithinUndefendedTravelPreference(
						p.ServiceMs, owner.StealthDefinition.MaximumUndefendedTargetTravelSeconds))
				.OrderByDescending(p => Separation(p.Plan)).ThenBy(p => p.ServiceMs)
					.ThenByDescending(p => p.Plan.Score).ThenBy(p => p.TravelMs)
					.ThenBy(p => p.Plan.Actor.ActorID).Select(p => p.Plan).FirstOrDefault();
			if (best == null)
				best = safePlans
					.OrderByDescending(p => Separation(p.Plan)).ThenBy(p => p.ServiceMs)
					.ThenByDescending(p => p.Plan.Score).ThenBy(p => p.TravelMs)
					.ThenBy(p => p.Plan.Actor.ActorID).Select(p => p.Plan).FirstOrDefault();
			if (best == null)
				best = clearPlans.Where(p => p.StealthMode == StealthClearMode.Kite ||
						p.StealthMode == StealthClearMode.CrushBridge)
					.OrderByDescending(Separation).ThenByDescending(p => p.Score)
					.ThenBy(p => p.Actor.ActorID).FirstOrDefault();
			if (best == null)
				best = clearPlans.Where(p => p.StealthMode == StealthClearMode.Mass)
					.OrderByDescending(Separation).ThenByDescending(p => p.Score)
					.ThenBy(p => p.Actor.ActorID).FirstOrDefault();
			if (best == null)
				best = safePlans.OrderByDescending(p => Separation(p.Plan)).ThenBy(p => p.ServiceMs)
					.ThenByDescending(p => p.Plan.Score)
					.ThenBy(p => p.TravelMs).ThenBy(p => p.Plan.Actor.ActorID)
					.Select(p => p.Plan).FirstOrDefault();
			if (best == null)
				best = clearPlans.OrderByDescending(Separation).ThenBy(p => p.ServiceMilliseconds)
					.ThenByDescending(p => p.Score)
					.ThenBy(p => p.Actor.ActorID).FirstOrDefault();

			// A wall is only a low-value fallback. Surface one already-planned, cached-local
			// moving-MTNK Kite challenger so the ownership review can require repeated
			// confirmation before replacing that fallback. This does not add candidates,
			// paths, scans, or a general dynamic-target priority.
			if (incumbent != null && incumbent.Info.HasTraitInfo<LineBuildNodeInfo>())
			{
				var movingMtnkKite = clearPlans.Where(plan =>
					plan.StealthMode == StealthClearMode.Kite &&
					plan.Actor.Info.Name.Equals("mtnk", StringComparison.OrdinalIgnoreCase) &&
					StealthAISpecialistPolicy.IsWithinUndefendedTravelPreference(
						plan.ServiceMilliseconds,
						owner.StealthDefinition.MaximumUndefendedTargetTravelSeconds))
					.OrderBy(plan => plan.ServiceMilliseconds).ThenByDescending(plan => plan.Score)
					.ThenBy(plan => plan.Actor.ActorID).FirstOrDefault();
				if (movingMtnkKite != null)
					best = movingMtnkKite;
			}

			if (debugPlans != null)
			{
				var ranked = debugPlans.OrderByDescending(plan => plan.Score)
					.ThenBy(plan => plan.Actor.ActorID).Take(2).ToArray();
				var ranking = ranked.Select(plan =>
				{
					var health = plan.Actor.TraitOrDefault<IHealth>();
					return $"{plan.Actor.Info.Name}#{plan.Actor.ActorID}:score={plan.Score}:mode={plan.StealthMode}:" +
						$"hp={health?.HP ?? 0}/{health?.MaxHP ?? 0}:service-ms={plan.ServiceMilliseconds}:" +
						$"separation={Separation(plan)}";
				}).JoinWith(",");
				Log.Write("debug", "Stealth target evidence [{0}] tick={1}: incumbent={2} top-two={3}.",
					owner.StealthProfile, owner.World.WorldTick,
					incumbent == null ? "none" : incumbent.Info.Name + "#" + incumbent.ActorID,
					ranking);
			}

			return best;
		}
		// END CNC96A GROUND EXTENSION

		static bool ThreatCovers((Actor Actor, float StoppingWeight, int RangeCells) threat, Actor target)
		{
			return !threat.Actor.IsDead &&
				(threat.Actor.CenterPosition - target.CenterPosition).Length / 1024 <= threat.RangeCells;
		}

		static List<Squad> NearbyAaSupportSquads(Squad owner, IEnumerable<Actor> aaTargets)
		{
			var radius = owner.SquadManager.Info.AirTargetAaClearSupportRadius;
			if (radius <= 0)
				return new List<Squad>();

			var targets = aaTargets.ToList();
			return owner.SquadManager.Squads
				.Where(s => s != owner && s.IsValid && s.Type == SquadType.Air &&
					(s.AirFormationCenter - owner.AirFormationCenter).Length / 1024 <= radius &&
					targets.Any(t =>
					{
						var unit = s.AirFormationUnits(bootstrapIfEmpty: true)
							.FirstOrDefault(u => CanAttackTarget(u, t));
						return unit != null && SafeRouteForAircraft(s, unit, t, requireInfluenceCache: true) != null;
					}))
				.OrderBy(s => (s.AirFormationCenter - owner.AirFormationCenter).LengthSquared)
				.ToList();
		}

		static DefendedCellPlan EvaluateDefendedCell(Squad owner, CPos cell,
			List<int> actorIndices, List<(Actor Actor, int Utility, float ConfiguredWeight)> liveCandidates,
			List<(Actor Actor, float StoppingWeight, int RangeCells)> threats, List<Actor> planningUnits)
		{
			var wantedInCell = actorIndices.Select(i => liveCandidates[i])
				.Where(c => c.ConfiguredWeight <= 0 && c.Utility > 0).Select(c => c.Actor).ToList();
			if (wantedInCell.Count == 0)
				return null;

			var coveringThreats = threats.Where(t => wantedInCell.Any(a => ThreatCovers(t, a)))
				.GroupBy(t => t.Actor.ActorID).Select(g => g.First()).OrderBy(t => t.Actor.ActorID).ToList();
			if (coveringThreats.Count == 0)
				return null;

			var aaTargets = coveringThreats.Select(t => t.Actor).ToList();

			// Choose the first AA victim with the initiating squad, then only count nearby squads that can
			// actually route to and attack that actor. This prevents paper strength from authorizing an
			// operation that the supposed support formation never joins.
			var clearTarget = aaTargets.OrderBy(a => EstimatedKillTicks(planningUnits, new[] { a }))
				.ThenBy(a => a.ActorID).First();
			var supportSquads = NearbyAaSupportSquads(owner, new[] { clearTarget });
			var combinedUnits = planningUnits.Concat(supportSquads.SelectMany(s =>
				s.AirFormationUnits(bootstrapIfEmpty: true))).Distinct().ToList();
			var protectedTargets = liveCandidates
				.Where(c => c.ConfiguredWeight <= 0 && c.Utility > 0 &&
					coveringThreats.Any(t => ThreatCovers(t, c.Actor)))
				.Select(c => c.Actor).Distinct().ToList();

			var cellKillTicks = EstimatedKillTicks(planningUnits, wantedInCell);
			var protectedKillTicks = EstimatedKillTicks(combinedUnits, protectedTargets);
			var aaClearTicks = EstimatedKillTicks(combinedUnits, aaTargets);
			var dangerValue = 0d;
			foreach (var threat in coveringThreats)
			{
				var value = EconomicValue(threat.Actor);
				dangerValue += value * threat.StoppingWeight;
			}

			var combinedValue = AmmoWeightedSquadValue(combinedUnits.Where(u =>
				aaTargets.Any(t => CanAttackTarget(u, t))));
			var info = owner.SquadManager.Info;
			var referenceThreatWeight = AaClearReferenceThreatWeight(owner);
			var clearEligible = StealthAIThreatGeometry.CanAttemptAaClear(
				owner.AirConsecutiveNoUndefendedScans, info.AirTargetAaClearFallbackScans,
				combinedValue, referenceThreatWeight, dangerValue, info.AirTargetAaClearValueRatio);
			var patientEnough = owner.AirConsecutiveNoUndefendedScans >= info.AirTargetAaClearFallbackScans;
			var action = patientEnough ? StealthAIThreatGeometry.ChooseDefendedAction(
				cellKillTicks, protectedKillTicks, aaClearTicks, clearEligible) : StealthAIDefendedAirAction.Reject;
			if (action != StealthAIDefendedAirAction.ClearAa)
				clearTarget = null;

			return new DefendedCellPlan
			{
				Action = action,
				ClearTarget = clearTarget,
				SupportSquads = supportSquads,
				AaThreatIds = aaTargets.Select(a => a.ActorID).ToList(),
				DangerValue = dangerValue,
				UnlockedValue = protectedTargets.Sum(a =>
					(long)liveCandidates.First(c => c.Actor == a).Utility),
				CellKillTicks = cellKillTicks,
				ProtectedKillTicks = protectedKillTicks,
				AaClearTicks = aaClearTicks,
				ClearAircraftValue = combinedValue,
				ClearReferenceWeight = referenceThreatWeight,
			};
		}

		/// <summary>
		/// Stock-style tactical fallback: the nearest enemy that at least one squad member can attack.
		/// Unlike the strategic scan this deliberately ignores route utility so an aircraft already in
		/// contact never retreats across the map merely because strategic planning found no good option.
		/// </summary>
		protected static Actor FindClosestAttackableEnemy(Squad owner)
		{
			var decisionUnits = AirDecisionUnits(owner);
			return owner.World.Actors
				.Where(a => owner.SquadManager.IsPreferredEnemyUnit(a) &&
					decisionUnits.Any(u => CanAttackTarget(u, a)))
				.ClosestTo(owner.AirFormationCenter);
		}

		/// <summary>
		/// Builds a deterministic coarse influence grid, then uses bounded A* route costs to compare the
		/// best targets. Unlike the old random sampler this considers every known actor, can value a safe
		/// detour, and keeps AA as a finite cost whose importance falls gradually as the squad grows.
		/// </summary>
		protected static AirTargetPlan FindBestAirTarget(Squad owner, bool relaxed = false, bool escapeFromAa = false)
		{
			return FindBestAirTarget(owner, null, out _, null, relaxed, escapeFromAa, null);
		}

		protected static AirTargetPlan FindBestAirTarget(Squad owner, Actor incumbent,
			out AirTargetPlan incumbentPlan, CPos? requiredStrategicCell = null, bool relaxed = false,
			bool escapeFromAa = false, CPos? requiredAaProtectedCell = null)
		{
			// BEGIN CNC96A GROUND EXTENSION
			if (owner.Type == SquadType.Stealth)
				return FindBestStealthTarget(owner, incumbent, out incumbentPlan, requiredStrategicCell);
			// END CNC96A GROUND EXTENSION

			incumbentPlan = null;
			var map = owner.World.Map;
			var info = owner.SquadManager.Info;
			var coarseSize = StealthCoarseSize(owner);
			var width = (map.MapSize.X + coarseSize - 1) / coarseSize;
			var height = (map.MapSize.Y + coarseSize - 1) / coarseSize;
			var archetypePriority = ArchetypePriority(owner);
			var planningUnits = AirDecisionUnits(owner);
			if (planningUnits.Count == 0)
			{
				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air target [{0}] scan stopped: no non-repairing aircraft in squad of {1}.",
						owner.AirProfile, owner.Units.Count);

				return null;
			}

			var armedUnits = planningUnits.Where(a => HasAmmo(a.TraitsImplementing<AmmoPool>())).ToList();
			var squadSpeed = planningUnits.Select(a => a.Info.TraitInfoOrDefault<AircraftInfo>())
				.Where(a => a != null).Min(a => a.Speed);
			if (!InfluenceCaches.TryGetValue(owner.SquadManager, out var profileCaches))
			{
				profileCaches = new Dictionary<string, AirInfluenceCache>();
				InfluenceCaches.Add(owner.SquadManager, profileCaches);
			}

			var cacheKey = owner.AirProfile + ":" + squadSpeed;
			var influenceStarted = Stopwatch.GetTimestamp();
			var rebuildInfluence = !profileCaches.TryGetValue(cacheKey, out var cache) ||
				cache.Width != width || cache.Height != height ||
				owner.World.WorldTick - cache.Tick >= info.AirInfluenceCacheInterval;
			if (rebuildInfluence)
			{
				var rebuiltDanger = new float[width * height];
				var rebuiltCandidates = new List<(Actor Actor, int Utility, float ConfiguredWeight)>();
				var rebuiltThreats = new List<(Actor Actor, float StoppingWeight, int RangeCells)>();
				foreach (var actor in owner.World.Actors)
				{
					if (!owner.SquadManager.IsPreferredEnemyUnit(actor))
						continue;

					var profile = AntiAirProfile(actor);
					var configuredWeight = ConfiguredThreatWeight(owner, actor, profile.Weight);
					var stoppingWeight = StoppingThreatWeight(owner, actor, profile.Weight);
					if (stoppingWeight > 0)
					{
						var range = Math.Max(1, (int)Math.Ceiling(profile.RangeCells * info.AirThreatRangeBuffer));
						if (info.AirTargetDebugLogging &&
							Math.Abs(profile.RangeCells - profile.BaseRangeCells) >= .01f)
							Log.Write("debug", "Air threat [{0}] {1}#{2}: base-range={3:0.##} modified-range={4:0.##} buffered-range={5}.",
								owner.AirProfile, actor.Info.Name, actor.ActorID,
								profile.BaseRangeCells, profile.RangeCells, range);

						var mobile = actor.Info.TraitInfoOrDefault<MobileInfo>();
						var movementBuffer = mobile == null ? 0 :
							StealthAIThreatGeometry.MobileThreatBufferCells(mobile.Speed, info.AirInfluenceCacheInterval);
						var influenceRange = range + movementBuffer;
						var minX = Math.Max(0, (actor.Location.X - influenceRange) / coarseSize);
						var maxX = Math.Min(width - 1, (actor.Location.X + influenceRange) / coarseSize);
						var minY = Math.Max(0, (actor.Location.Y - influenceRange) / coarseSize);
						var maxY = Math.Min(height - 1, (actor.Location.Y + influenceRange) / coarseSize);
						var transitWeight = TransitThreatWeight(owner, actor, profile, squadSpeed);

						rebuiltThreats.Add((actor, stoppingWeight, range));
						for (var y = minY; y <= maxY; y++)
							for (var x = minX; x <= maxX; x++)
							{
								var cell = new CPos(x * coarseSize + coarseSize / 2, y * coarseSize + coarseSize / 2);
								var distance = (map.CenterOfCell(map.Clamp(cell)) - actor.CenterPosition).Length / 1024;
								if (distance <= influenceRange)
									rebuiltDanger[y * width + x] += transitWeight;
							}
					}

					if (!owner.SquadManager.IsNotHiddenUnit(actor))
						continue;

					rebuiltCandidates.Add((actor,
						BaseTargetUtility(actor, info, archetypePriority,
							configuredWeight), configuredWeight));
				}

				cache = new AirInfluenceCache
				{
					Tick = owner.World.WorldTick,
					Width = width,
					Height = height,
					Danger = rebuiltDanger,
					Candidates = rebuiltCandidates,
					Threats = rebuiltThreats,
				};
				profileCaches[cacheKey] = cache;
			}

			RecordAirPhase(owner, rebuildInfluence ? "influence-build" : "influence-cache-hit", influenceStarted);

			var danger = cache.Danger;
			var candidates = cache.Candidates;
			var threats = cache.Threats;

			var planningCenter = owner.AirFormationCenter;
			var ammoReadiness = AmmoReadiness(planningUnits);
			var startCell = map.CellContaining(planningCenter);
			var startX = Math.Clamp(startCell.X / coarseSize, 0, width - 1);
			var startY = Math.Clamp(startCell.Y / coarseSize, 0, height - 1);
			var adaptiveRisk = owner.SquadManager.AirRiskMultiplier(owner.AirProfile);
			var attackRiskScale = Math.Max(1f, armedUnits.Count / 3f) * adaptiveRisk;
			var bestScore = int.MinValue;
			Actor best = null;
			List<CPos> bestRoute = null;
			var bestIsUndefended = false;
			var bestClearsAa = false;
			List<Squad> bestSupportSquads = null;
			CPos? bestAaProtectedCell = null;
			IReadOnlyCollection<uint> bestAaThreatIds = null;
			var bestEscapeExposure = float.MaxValue;

			var attackableCandidates = candidates.Where(c => !c.Actor.IsDead &&
				planningUnits.Any(u => CanAttackTarget(u, c.Actor))).ToList();
			var coverageTargets = attackableCandidates.Where(c => c.ConfiguredWeight <= 0).ToList();
			var coveredTargets = coverageTargets.Count(c => IsCoveredByAa(c.Actor, threats));
			if (info.AirTargetDebugLogging && info.AirTargetPowerActors.Count > 0 &&
				info.AirTargetPowerPriorityMaximum > 0)
				Log.Write("debug", "Air coverage [{0}]: covered={1}/{2} ({3:0.#}%) threshold={4}% power-maximum={5}.",
					owner.AirProfile, coveredTargets, coverageTargets.Count,
					coverageTargets.Count == 0 ? 0 : coveredTargets * 100f / coverageTargets.Count,
					info.AirTargetPowerCoverageThresholdPercent, info.AirTargetPowerPriorityMaximum);

			var liveCandidates = attackableCandidates.Select(c =>
				{
					var baseUtility = c.Utility;
					if (info.AirTargetPowerActors.Contains(c.Actor.Info.Name) &&
						info.AirTargetPowerPriorityMaximum > 0)
					{
						var authoredPriority = TargetValue(c.Actor, info, archetypePriority);
						var adjustedPriority = StealthAIThreatGeometry.CoverageAdjustedPriority(authoredPriority,
							coveredTargets, coverageTargets.Count, info.AirTargetPowerCoverageThresholdPercent,
							info.AirTargetPowerPriorityMaximum);
						baseUtility = BaseTargetUtility(c.Actor, info, archetypePriority,
							c.ConfiguredWeight, adjustedPriority);
					}

					return (c.Actor, Utility: CurrentTargetUtility(c.Actor, baseUtility), c.ConfiguredWeight);
				})
				.OrderBy(c => c.Actor.ActorID).ToList();
			var liveCandidateActors = new HashSet<Actor>(liveCandidates.Select(c => c.Actor));
			if (incumbent != null && !incumbent.IsDead &&
				!liveCandidateActors.Contains(incumbent) &&
				owner.SquadManager.IsPreferredEnemyUnit(incumbent) &&
				planningUnits.Any(u => CanAttackTarget(u, incumbent)))
			{
				var profile = AntiAirProfile(incumbent);
				var configuredWeight = ConfiguredThreatWeight(owner, incumbent, profile.Weight);
				var priority = TargetValue(incumbent, info, archetypePriority);
				if (info.AirTargetPowerActors.Contains(incumbent.Info.Name) && info.AirTargetPowerPriorityMaximum > 0)
					priority = StealthAIThreatGeometry.CoverageAdjustedPriority(priority, coveredTargets,
						coverageTargets.Count, info.AirTargetPowerCoverageThresholdPercent,
						info.AirTargetPowerPriorityMaximum);

				var baseUtility = BaseTargetUtility(incumbent, info, archetypePriority,
					configuredWeight, priority);
				liveCandidates.Add((incumbent, CurrentTargetUtility(incumbent, baseUtility), configuredWeight));
				liveCandidateActors.Add(incumbent);
				liveCandidates.Sort((a, b) => a.Actor.ActorID.CompareTo(b.Actor.ActorID));
			}

			if (incumbent != null && !incumbent.IsDead)
				requiredStrategicCell = new CPos(
					incumbent.Location.X / coarseSize, incumbent.Location.Y / coarseSize);

			// The cached bounded scan may not yet contain an actor that has just transformed (for example,
			// an MCV deploying into a Construction Yard). Refresh every attackable actor in the current
			// target cell directly from the world and require that cell in the candidate set. This both
			// preserves local opportunities and lets the remaining-health bonus finish transformed targets.
			if (requiredStrategicCell != null)
			{
				foreach (var actor in owner.World.Actors)
				{
					if (actor.IsDead || liveCandidateActors.Contains(actor) ||
						!owner.SquadManager.IsPreferredEnemyUnit(actor) ||
						!owner.SquadManager.IsNotHiddenUnit(actor) ||
						!planningUnits.Any(u => CanAttackTarget(u, actor)))
						continue;

					var actorCell = new CPos(actor.Location.X / coarseSize, actor.Location.Y / coarseSize);
					if (actorCell != requiredStrategicCell.Value)
						continue;

					var profile = AntiAirProfile(actor);
					var configuredWeight = ConfiguredThreatWeight(owner, actor, profile.Weight);
					var priority = TargetValue(actor, info, archetypePriority);
					if (info.AirTargetPowerActors.Contains(actor.Info.Name) && info.AirTargetPowerPriorityMaximum > 0)
						priority = StealthAIThreatGeometry.CoverageAdjustedPriority(priority, coveredTargets,
							coverageTargets.Count, info.AirTargetPowerCoverageThresholdPercent,
							info.AirTargetPowerPriorityMaximum);

					var baseUtility = BaseTargetUtility(actor, info, archetypePriority,
						configuredWeight, priority);
					liveCandidates.Add((actor, CurrentTargetUtility(actor, baseUtility), configuredWeight));
					liveCandidateActors.Add(actor);
				}

				liveCandidates.Sort((a, b) => a.Actor.ActorID.CompareTo(b.Actor.ActorID));
			}

			var cellUtility = new Dictionary<CPos, long>();
			var cellUnlockUtility = new Dictionary<CPos, long>();
			var cellActors = new Dictionary<CPos, List<int>>();
			for (var i = 0; i < liveCandidates.Count; i++)
			{
				var candidate = liveCandidates[i];
				var cell = new CPos(candidate.Actor.Location.X / coarseSize, candidate.Actor.Location.Y / coarseSize);
				cellUtility.TryGetValue(cell, out var total);
				cellUtility[cell] = total + candidate.Utility;
				if (candidate.ConfiguredWeight <= 0)
				{
					cellUnlockUtility.TryGetValue(cell, out var unlockTotal);
					cellUnlockUtility[cell] = unlockTotal + candidate.Utility;
				}

				if (!cellActors.TryGetValue(cell, out var actors))
				{
					actors = new List<int>();
					cellActors.Add(cell, actors);
				}

				actors.Add(i);
			}

			// Select unique strategic locations before individual actors. Otherwise a single crowded,
			// defended cell can consume every nearest/high-value slot and hide safe targets elsewhere.
			var candidateCells = cellActors.Keys.OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
			var currentCellHasTargets = requiredStrategicCell != null &&
				cellActors.ContainsKey(requiredStrategicCell.Value);
			var useClusterOpportunity = StealthAIThreatGeometry.UseClusterOpportunity(
				incumbent != null && !incumbent.IsDead, currentCellHasTargets);
			var requiredCellIndex = requiredStrategicCell == null ? -1 :
				candidateCells.FindIndex(c => c == requiredStrategicCell.Value);
			var selectedCellIndices = StealthAIThreatGeometry.SelectTargetCandidates(
				candidateCells.Select(c =>
				{
					var center = map.CenterOfCell(map.Clamp(new CPos(
						c.X * coarseSize + coarseSize / 2, c.Y * coarseSize + coarseSize / 2)));
					return (center - planningCenter).LengthSquared;
				}).ToList(),
				candidateCells.Select(c => (int)Math.Min(int.MaxValue, cellUtility[c])).ToList(),
				info.AirTargetClosestCandidates, info.AirTargetHighestValueCandidates, requiredCellIndex);

			if (info.AirTargetHarvesterCandidates > 0)
			{
				var selected = new HashSet<int>(selectedCellIndices);
				foreach (var index in Enumerable.Range(0, candidateCells.Count)
					.Where(i => cellActors[candidateCells[i]].Any(a =>
						liveCandidates[a].Actor.Info.HasTraitInfo<HarvesterInfo>()))
					.OrderBy(i =>
					{
						var c = candidateCells[i];
						var center = map.CenterOfCell(map.Clamp(new CPos(
							c.X * coarseSize + coarseSize / 2, c.Y * coarseSize + coarseSize / 2)));
						return (center - planningCenter).LengthSquared;
					})
					.ThenBy(i => i)
					.Take(info.AirTargetHarvesterCandidates))
					selected.Add(index);

				selectedCellIndices = selected.OrderBy(i => i).ToList();
			}

			if (requiredAaProtectedCell != null)
			{
				var protectedCellIndex = candidateCells.FindIndex(c => c == requiredAaProtectedCell.Value);
				if (protectedCellIndex >= 0 && !selectedCellIndices.Contains(protectedCellIndex))
					selectedCellIndices.Add(protectedCellIndex);
			}

			var defendedCellPlans = selectedCellIndices
				.Select(i => candidateCells[i]).Distinct()
				.Select(c => (Cell: c, Plan: EvaluateDefendedCell(
					owner, c, cellActors[c], liveCandidates, threats, planningUnits)))
				.Where(p => p.Plan != null)
				.ToDictionary(p => p.Cell, p => p.Plan);
			if (info.AirTargetDebugLogging)
				foreach (var defended in defendedCellPlans.OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X))
					Log.Write("debug", "Air defended [{0}] cell={1}: action={2} cell-kill={3} protected-kill={4} AA-clear={5} clear-target={6} support-squads={7} aircraft-value={8} reference-weight={9:0.##} aircraft-strength={10:0.##} danger-value={11:0.##} required-ratio={12} unlocked={13}.",
						owner.AirProfile, defended.Key, defended.Value.Action,
						defended.Value.CellKillTicks, defended.Value.ProtectedKillTicks,
						defended.Value.AaClearTicks, defended.Value.ClearTarget == null ? "none" :
							defended.Value.ClearTarget.Info.Name + "#" + defended.Value.ClearTarget.ActorID,
						defended.Value.SupportSquads.Count, defended.Value.ClearAircraftValue,
						defended.Value.ClearReferenceWeight,
						defended.Value.ClearAircraftValue * defended.Value.ClearReferenceWeight,
						defended.Value.DangerValue, info.AirTargetAaClearValueRatio,
						defended.Value.UnlockedValue);
			var foundUndefendedTarget = false;
			var aaClearPlans = new List<(Actor Actor, double DangerValue, long UnlockedValue,
				int Score, List<CPos> Route, List<Squad> SupportSquads, CPos ProtectedCell,
				List<uint> ThreatIds)>();
			foreach (var defended in defendedCellPlans.OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X))
			{
				var plan = defended.Value;
				if (plan.Action != StealthAIDefendedAirAction.ClearAa || plan.ClearTarget == null)
					continue;

				var clearCell = new CPos(plan.ClearTarget.Location.X / coarseSize,
					plan.ClearTarget.Location.Y / coarseSize);
				var route = FindAirRoute(owner, danger, width, height, startX, startY,
					Math.Clamp(clearCell.X, 0, width - 1), Math.Clamp(clearCell.Y, 0, height - 1),
					info.AirRouteThreatPenalty);
				if (route == null)
					continue;

				var exposureCost = route.Sum(p => danger[p.Y * width + p.X]) * info.AirRouteThreatPenalty;
				var speedScale = info.AirTargetReferenceSpeed / (float)Math.Max(1, squadSpeed);
				var ammoDistanceScale = 1f + ammoReadiness * info.AirTargetFullAmmoDistanceBonus / 100f * adaptiveRisk;
				var distanceCost = (int)(route.Count * coarseSize * info.AirTargetDistancePenalty *
					speedScale * ammoDistanceScale);
				var clearCandidate = liveCandidates.FirstOrDefault(c => c.Actor == plan.ClearTarget);
				var clearUtility = clearCandidate.Actor == null ? CurrentTargetUtility(plan.ClearTarget,
					BaseTargetUtility(plan.ClearTarget, info, archetypePriority,
						ConfiguredThreatWeight(owner, plan.ClearTarget, AntiAirProfile(plan.ClearTarget).Weight))) :
					clearCandidate.Utility;
				var opportunity = StealthAIThreatGeometry.AirTargetOpportunityValue(clearUtility,
					plan.UnlockedValue, false, false, true, info.AirTargetAaClearUnlockPercent);
				var score = opportunity * 1024 / Math.Max(1, 1024 + distanceCost);
				score = score * 1024 / Math.Max(1, 1024 + (int)exposureCost);
				var smoothedRoute = ThreatAwareRoutePlanner.SmoothRoute(
					danger, width, height, startX, startY, route)
					.Select(p => map.Clamp(new CPos(
						p.X * coarseSize + coarseSize / 2, p.Y * coarseSize + coarseSize / 2))).ToList();
				aaClearPlans.Add((plan.ClearTarget, plan.DangerValue, plan.UnlockedValue,
					(int)Math.Clamp(score, int.MinValue, int.MaxValue), smoothedRoute,
					plan.SupportSquads, defended.Key, plan.AaThreatIds));
			}

			foreach (var selectedCellIndex in selectedCellIndices)
			{
				var cell = candidateCells[selectedCellIndex];
				var goalX = Math.Clamp(cell.X, 0, width - 1);
				var goalY = Math.Clamp(cell.Y, 0, height - 1);
				var clusteredOpportunityValue = cellUtility[cell];
				var route = FindAirRoute(owner, danger, width, height, startX, startY, goalX, goalY,
					info.AirRouteThreatPenalty);
				if (route == null)
				{
					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air cell [{0}] {1} rejected: no coarse route.",
							owner.AirProfile, cell);

					continue;
				}

				var exposureCost = route.Sum(p => danger[p.Y * width + p.X]) * info.AirRouteThreatPenalty;

				// Compare travel time rather than raw cells. A full magazine increases the pressure to
				// spend that readiness on a nearby opportunity instead of crossing the whole map.
				var speedScale = info.AirTargetReferenceSpeed / (float)Math.Max(1, squadSpeed);
				var ammoDistanceScale = 1f + ammoReadiness * info.AirTargetFullAmmoDistanceBonus / 100f * adaptiveRisk;
				var distanceCost = (int)(route.Count * coarseSize * info.AirTargetDistancePenalty *
					speedScale * ammoDistanceScale);
				var smoothedRoute = ThreatAwareRoutePlanner.SmoothRoute(
					danger, width, height, startX, startY, route)
					.Select(p => map.Clamp(new CPos(
						p.X * coarseSize + coarseSize / 2, p.Y * coarseSize + coarseSize / 2))).ToList();
				Actor cellTarget = null;
				var cellTargetScore = int.MinValue;
				var cellTargetDanger = 0f;
				var cellTargetStoppingCost = 0;
				var cellTargetIsUndefended = false;
				var cellTargetClearsAa = false;
				long cellTargetOpportunityValue = 0;
				var hasIncumbent = false;
				var incumbentStoppingCost = 0;
				var incumbentIsUndefended = false;
				long incumbentOpportunityValue = 0;

				// The cell sum ranks the location. The actor's own utility and exact AA coverage choose
				// the victim inside that location, so a SAM or power plant cannot inherit a harvester
				// cluster's full value merely by sharing its coarse tile.
				foreach (var candidateIndex in cellActors[cell])
				{
					var candidate = liveCandidates[candidateIndex];
					var destinationDanger = 0f;
					var candidateAa = candidate.ConfiguredWeight > 0;
					foreach (var threat in threats)
					{
						if (threat.Actor.IsDead)
							continue;

						var distance = (threat.Actor.CenterPosition - candidate.Actor.CenterPosition).Length / 1024;
						if (distance > threat.RangeCells)
							continue;

						if (threat.Actor == candidate.Actor)
							continue;

						destinationDanger += threat.StoppingWeight;
					}

					var continuingAaClear = candidateAa && candidate.Actor == incumbent;
					var clearsAa = continuingAaClear;

					// AA actors are deliberate clearing targets, never incidental victims of cell value.
					if (candidateAa && !clearsAa)
						continue;

					var quickStrike = false;
					if (!candidateAa && destinationDanger > 0)
					{
						if (!defendedCellPlans.TryGetValue(cell, out var defendedPlan) ||
							defendedPlan.Action != StealthAIDefendedAirAction.Sneak)
							continue;

						quickStrike = true;
					}

					var stoppingCost = (int)(destinationDanger * info.AirTargetAntiAirPenalty / attackRiskScale);
					if (quickStrike)
						stoppingCost /= 2;

					var isUndefended = destinationDanger <= 0 && !clearsAa;
					cellUnlockUtility.TryGetValue(cell, out var unlockedValue);
					var candidateLocationValue = StealthAIThreatGeometry.AirTargetOpportunityValue(
						candidate.Utility, clusteredOpportunityValue, useClusterOpportunity,
						isUndefended, clearsAa, info.AirTargetAaClearUnlockPercent);
					var targetValue = clearsAa ? StealthAIThreatGeometry.AirTargetOpportunityValue(
						candidate.Utility, unlockedValue, false, false, true,
						info.AirTargetAaClearUnlockPercent) : candidate.Utility;
					if (clearsAa)
						candidateLocationValue = targetValue;

					var targetScore = targetValue * 1024 / Math.Max(1, 1024 + stoppingCost);
					var finalTargetScore = (int)Math.Clamp(targetScore, int.MinValue, int.MaxValue);
					var locationScore = candidateLocationValue;
					locationScore = locationScore * 1024 / Math.Max(1, 1024 + distanceCost);
					locationScore = locationScore * 1024 / Math.Max(1, 1024 + (int)exposureCost);
					locationScore = locationScore * 1024 / Math.Max(1, 1024 + stoppingCost);
					var finalLocationScore = (int)Math.Clamp(locationScore, int.MinValue, int.MaxValue);
					if (candidate.Actor == incumbent)
					{
						hasIncumbent = true;
						incumbentStoppingCost = stoppingCost;
						incumbentIsUndefended = isUndefended;
						incumbentOpportunityValue = candidateLocationValue;
					}

					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air target [{0}] {1}#{2}: cell={3} utility={4} cell-utility={5} destination-danger={6:0.##} target-score={7} clears-aa={8} quick-strike={9} relaxed={10}",
							owner.AirProfile, candidate.Actor.Info.Name, candidate.Actor.ActorID, cell,
							candidate.Utility, clusteredOpportunityValue, destinationDanger, finalTargetScore,
							clearsAa, quickStrike, relaxed);

					if (cellTarget == null || (isUndefended && !cellTargetIsUndefended) ||
						(isUndefended == cellTargetIsUndefended && finalTargetScore > cellTargetScore))
					{
						cellTarget = candidate.Actor;
						cellTargetScore = finalTargetScore;
						cellTargetDanger = destinationDanger;
						cellTargetStoppingCost = stoppingCost;
						cellTargetIsUndefended = isUndefended;
						cellTargetClearsAa = clearsAa;
						cellTargetOpportunityValue = candidateLocationValue;
					}
				}

				if (cellTarget == null)
					continue;

				// Each independent liability scales the location value down. Defended locations remain
				// finite choices, but only after every selected undefended location has been considered.
				var opportunityValue = cellTargetOpportunityValue;
				var score = opportunityValue;
				score = score * 1024 / Math.Max(1, 1024 + distanceCost);
				score = score * 1024 / Math.Max(1, 1024 + (int)exposureCost);
				score = score * 1024 / Math.Max(1, 1024 + cellTargetStoppingCost);
				var finalScore = (int)Math.Clamp(score, int.MinValue, int.MaxValue);
				if (cellTargetIsUndefended && (relaxed || finalScore >= info.AirTargetMinimumScore))
					foundUndefendedTarget = true;
				if (hasIncumbent)
				{
					var incumbentScore = incumbentOpportunityValue;
					incumbentScore = incumbentScore * 1024 / Math.Max(1, 1024 + distanceCost);
					incumbentScore = incumbentScore * 1024 / Math.Max(1, 1024 + (int)exposureCost);
					incumbentScore = incumbentScore * 1024 / Math.Max(1, 1024 + incumbentStoppingCost);
					incumbentPlan = new AirTargetPlan(incumbent,
						(int)Math.Clamp(incumbentScore, int.MinValue, int.MaxValue),
						incumbentIsUndefended, new List<CPos>(smoothedRoute));
				}

				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air cell [{0}] {1}: utility={2} clustered-utility={3} cluster-mode={4} route={5} exposure={6:0.##} target={7}#{8} destination-danger={9:0.##} score={10} undefended={11} clears-aa={12} ammo={13:0.##} relaxed={14}",
						owner.AirProfile, cell, opportunityValue, clusteredOpportunityValue, useClusterOpportunity,
						route.Count, exposureCost,
						cellTarget.Info.Name, cellTarget.ActorID, cellTargetDanger, finalScore,
						cellTargetIsUndefended, cellTargetClearsAa, ammoReadiness, relaxed);

				// Undefended targets form the first selection tier. A defended target remains eligible,
				// but only when this bounded strategic-cell pool contains no undefended destination at all.
				// Route and distance costs still rank candidates within each tier.
				var betterEscape = escapeFromAa && cellTargetIsUndefended &&
					(exposureCost < bestEscapeExposure ||
						(exposureCost == bestEscapeExposure && finalScore > bestScore));
				var betterNormal = !escapeFromAa && (best == null ||
					(cellTargetIsUndefended && !bestIsUndefended) ||
					(cellTargetIsUndefended == bestIsUndefended && finalScore > bestScore));
				if (betterEscape || betterNormal)
				{
					best = cellTarget;
					bestScore = finalScore;
					bestIsUndefended = cellTargetIsUndefended;
					bestRoute = smoothedRoute;
					bestClearsAa = false;
					bestSupportSquads = null;
					bestAaProtectedCell = null;
					bestAaThreatIds = null;
					bestEscapeExposure = exposureCost;
				}
			}

			var viableAaClearPlans = aaClearPlans
				.Where(p => relaxed || p.Score >= info.AirTargetMinimumScore)
				.GroupBy(p => p.Actor.ActorID)
				.Select(g => g.OrderByDescending(p => p.UnlockedValue)
					.ThenBy(p => p.DangerValue).ThenByDescending(p => p.Score).First())
				.OrderBy(p => p.Actor.ActorID).ToList();
			if (!escapeFromAa && incumbent != null)
			{
				var incumbentAaIndex = StealthAIThreatGeometry.SelectAaClearCandidateForTarget(
					viableAaClearPlans.Select(p => p.Actor.ActorID).ToList(), incumbent.ActorID,
					viableAaClearPlans.Select(p => p.DangerValue).ToList(),
					viableAaClearPlans.Select(p => p.UnlockedValue).ToList(),
					viableAaClearPlans.Select(p => p.Score).ToList());
				if (incumbentAaIndex >= 0)
				{
					var incumbentAa = viableAaClearPlans[incumbentAaIndex];
					incumbentPlan = new AirTargetPlan(incumbentAa.Actor, incumbentAa.Score, false,
						incumbentAa.Route, clearsAa: true, supportSquads: incumbentAa.SupportSquads,
						aaProtectedCell: incumbentAa.ProtectedCell, aaThreatIds: incumbentAa.ThreatIds);
					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air AA-clear [{0}] recalculated incumbent {1}#{2}: protected-cell={3} danger-value={4:0.##} unlocked-value={5} score={6} support-squads={7}.",
							owner.AirProfile, incumbent.Info.Name, incumbent.ActorID,
							incumbentAa.ProtectedCell, incumbentAa.DangerValue,
							incumbentAa.UnlockedValue, incumbentAa.Score,
							incumbentAa.SupportSquads.Count);
				}
			}

			if (!escapeFromAa && !foundUndefendedTarget && info.AirTargetAaClearWeakestCandidates > 0)
			{
				var aaClearIndex = StealthAIThreatGeometry.SelectAaClearCandidate(
					viableAaClearPlans.Select(p => p.DangerValue).ToList(),
					viableAaClearPlans.Select(p => p.UnlockedValue).ToList(),
					viableAaClearPlans.Select(p => p.Score).ToList(),
					info.AirTargetAaClearWeakestCandidates);
				if (aaClearIndex >= 0)
				{
					var aaClear = viableAaClearPlans[aaClearIndex];
					best = aaClear.Actor;
					bestScore = aaClear.Score;
					bestIsUndefended = false;
					bestRoute = aaClear.Route;
					bestClearsAa = true;
					bestSupportSquads = aaClear.SupportSquads;
					bestAaProtectedCell = aaClear.ProtectedCell;
					bestAaThreatIds = aaClear.ThreatIds;
					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air AA-clear [{0}] selected {1}#{2}: danger-value={3:0.##} unlocked-value={4} score={5} weakest-pool={6} eligible={7}.",
							owner.AirProfile, best.Info.Name, best.ActorID, aaClear.DangerValue,
							aaClear.UnlockedValue, bestScore, info.AirTargetAaClearWeakestCandidates,
							viableAaClearPlans.Count);
				}
			}

			if (foundUndefendedTarget)
				owner.AirConsecutiveNoUndefendedScans = 0;
			else if (owner.AirConsecutiveNoUndefendedScans < int.MaxValue)
				owner.AirConsecutiveNoUndefendedScans++;

			if (info.AirTargetDebugLogging)
				Log.Write("debug", "Air target [{0}] scan summary: undefended-found={1} no-undefended-scans={2} cluster-mode={3} incumbent={4} current-cell-targets={5}.",
					owner.AirProfile, foundUndefendedTarget, owner.AirConsecutiveNoUndefendedScans,
					useClusterOpportunity, incumbent == null ? "none" : incumbent.Info.Name + "#" + incumbent.ActorID,
					currentCellHasTargets);

			if (incumbentPlan != null && !relaxed && incumbentPlan.Score < info.AirTargetMinimumScore)
				incumbentPlan = null;

			if (best == null || (!relaxed && bestScore < info.AirTargetMinimumScore))
			{
				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air target [{0}] scan found no eligible target: cells={1} best-score={2} minimum={3} relaxed={4}.",
						owner.AirProfile, selectedCellIndices.Count, bestScore, info.AirTargetMinimumScore, relaxed);

				return null;
			}

			if (info.AirTargetDebugLogging)
				Log.Write("debug", "Air target [{0}] selected {1}#{2}: score={3} undefended={4} waypoints={5} relaxed={6}",
					owner.AirProfile, best.Info.Name, best.ActorID, bestScore, bestIsUndefended,
					bestRoute?.Count ?? 0, relaxed);

			return new AirTargetPlan(best, bestScore, bestIsUndefended, bestRoute,
				bestClearsAa, bestSupportSquads, bestAaProtectedCell, bestAaThreatIds);
		}

		protected static void ApplyAirTargetPlan(Squad owner, AirTargetPlan plan)
		{
			var info = owner.SquadManager.Info;
			var previousStealthMode = owner.StealthClearMode;
			var previousTarget = owner.TargetActor;
			owner.StealthKiteSupersessionActorId = 0;
			owner.StealthKiteSupersessionConfirmations = 0;
			if (owner.StealthTargetlessApproachCell != null &&
				(owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug))
				Log.Write("debug", "Stealth targetless intent cleared [{0}] tick={1}: intent-cell={2} " +
					"reason=normal-plan-acquired target={3}#{4} target-cell={5}.", owner.StealthProfile,
					owner.World.WorldTick, owner.StealthTargetlessApproachCell.Value,
					plan.Actor.Info.Name, plan.Actor.ActorID, CoarseCell(owner, plan.Actor.Location));
			owner.StealthTargetlessApproachCell = null;
			owner.StealthTargetlessApproachStartedTick = -1;
			owner.StealthTargetlessApproachSteps = 0;
			owner.StealthTargetlessRejectedCells.Clear();
			var preserveStealthRoute = StealthAISpecialistPolicy.ShouldPreserveOwnedMissionRoute(
				owner.StealthProfile == "stealth-tank", owner.TargetActor == plan.Actor,
				owner.AirRouteQueued, owner.AirFormationUnits().Any(unit => !unit.IsIdle),
				owner.StealthClearMode, plan.StealthMode);
			var enteringStealthMass = owner.Type == SquadType.Stealth &&
				plan.StealthMode == StealthClearMode.Mass && owner.StealthClearMode != StealthClearMode.Mass;
			var enteringStealthKite = owner.Type == SquadType.Stealth &&
				plan.StealthMode == StealthClearMode.Kite &&
				(owner.StealthClearMode != StealthClearMode.Kite || owner.TargetActor != plan.Actor);
			if (plan.ClearsAa)
				owner.SquadManager.MarkGroundTargetForAirSupport(plan.Actor);

			owner.TargetActor = plan.Actor;
			if (preserveStealthRoute)
			{
				owner.StealthCoreRoutePreserves++;
				if (info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Air route [{0}] preserved progressing route to same incumbent " +
						"{1}#{2}: decision=retain reason=approved-actor-route-progressing " +
						"mode={3} issues={4} preserves={5}.", owner.AirProfile,
						plan.Actor.Info.Name, plan.Actor.ActorID, owner.StealthClearMode,
						owner.StealthCoreRouteIssues, owner.StealthCoreRoutePreserves);
			}
			else
			{
				owner.AirRoute.Clear();
				owner.AirRouteQueued = false;
				owner.StealthValidatedFiringCells.Clear();
				if (owner.StealthProfile == "stealth-tank")
					owner.StealthCoreRouteIssues++;
			}

			// STNK reinforcement catch-up is formation-owned and must survive economic target reviews.
			if (owner.StealthProfile != "stealth-tank")
				owner.AirReinforcementTargets.Clear();
			owner.AirTargetStrategicCell = plan.StealthAggressiveMass && plan.StealthClearCenterCell != null ?
				plan.StealthClearCenterCell : new CPos(
					plan.Actor.Location.X / StealthCoarseSize(owner),
					plan.Actor.Location.Y / StealthCoarseSize(owner));
			owner.AirTargetLastProgressTick = owner.World.WorldTick;
			owner.AirTargetLastDistanceCells = (plan.Actor.CenterPosition - owner.AirFormationCenter).Length / 1024;
			owner.AirTargetLastHP = plan.Actor.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue;
			owner.AirTargetScore = plan.Score;
			owner.AirTargetIsUndefended = plan.IsUndefended;
			owner.AirTargetClearsAa = plan.ClearsAa;
			owner.AirAaClearProtectedCell = plan.ClearsAa ? plan.AaProtectedCell : null;
			owner.AirAaClearThreatIds.Clear();
			if (plan.ClearsAa && plan.AaThreatIds != null)
				owner.AirAaClearThreatIds.UnionWith(plan.AaThreatIds);
			owner.AirAaClearEngaged = false;
			if (owner.Type == SquadType.Stealth)
			{
				owner.StealthPostAttackCell = plan.StealthPostAttackCell;
				owner.StealthClearMode = plan.StealthMode;
				owner.StealthAggressiveMass = plan.StealthMode == StealthClearMode.Mass &&
					plan.StealthAggressiveMass;
				if (plan.StealthMode != StealthClearMode.Crush &&
					plan.StealthMode != StealthClearMode.CrushBridge)
				{
					owner.StealthCrushLeaderActorId = 0;
					owner.StealthCrushTargetCell = null;
				}
				else
					owner.StealthCrushTargetCell = plan.Actor.Location;
				owner.StealthClearCenterCell = plan.StealthClearCenterCell;
				owner.StealthClearPackage.Clear();
				if (plan.StealthPackage != null)
					owner.StealthClearPackage.UnionWith(plan.StealthPackage);
				var package = owner.StealthClearPackage.Select(owner.World.GetActorById)
					.Where(a => a != null && !a.IsDead && a.IsInWorld);
				owner.StealthClearMembershipSignature = PackageSignature(
					owner.AirFormationUnits(bootstrapIfEmpty: true), package);
				if (Game.Settings.Debug.BotDebug &&
					(previousStealthMode != plan.StealthMode || previousTarget != plan.Actor))
				{
					var transitionReason = enteringStealthMass ? "crossover-entry-approved" :
						plan.StealthMode == StealthClearMode.Mass ? "crossover-continuation-retarget" :
						"plan-selected";
					Log.Write("debug", "Stealth lifecycle watchdog transition [{0}] tick={1}: " +
						"from-mode={2} to-mode={3} from-target={4} to-target={5} " +
						"reason={6} mass-entry-approved={7} package={8} clear-cell={9}.",
						owner.StealthProfile, owner.World.WorldTick, previousStealthMode,
						plan.StealthMode, previousTarget == null ? "none" :
							previousTarget.Info.Name + "#" + previousTarget.ActorID,
						plan.Actor.Info.Name + "#" + plan.Actor.ActorID, transitionReason,
						enteringStealthMass, owner.StealthClearPackage.Count,
						owner.StealthClearCenterCell?.ToString() ?? "none");
				}
				if (enteringStealthKite)
				{
					owner.StealthKiteTargetCell = plan.Actor.Location;
					owner.StealthKiteParticipantHealth.Clear();
					foreach (var participant in owner.AirFormationUnits(bootstrapIfEmpty: true))
						owner.StealthKiteParticipantHealth[participant.ActorID] =
							participant.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue;
				}
				else if (plan.StealthMode != StealthClearMode.Kite)
				{
					owner.StealthKiteTargetCell = null;
					owner.StealthKiteParticipantHealth.Clear();
				}
				if (enteringStealthMass && info.AirTargetDebugLogging)
				{
					var formation = owner.AirFormationUnits(bootstrapIfEmpty: true);
					var defenders = package.ToList();
					var threatRanking = defenders.Select(defender =>
						(Actor: defender, Threat: ThreatValue(owner, formation, defender)))
						.OrderByDescending(entry => entry.Threat).ThenBy(entry => entry.Actor.ActorID)
						.Select(entry => $"{entry.Actor.Info.Name}#{entry.Actor.ActorID}@{entry.Actor.Owner.InternalName}:" +
							$"threat={entry.Threat:0.###}").JoinWith(",");
					Log.Write("debug", "Stealth mass live threat ranking [{0}] tick={1}: " +
						"selected={2}#{3} actors=[{4}] source=live-standard-calculator.",
						owner.StealthProfile, owner.World.WorldTick, plan.Actor.Info.Name,
						plan.Actor.ActorID, threatRanking);
					Log.Write("debug", "Stealth mass [{0}] entry: tick={1} overmatch={2:0.###} " +
						"package={3} victim={4}#{5} threat={6:0.###}.", owner.StealthProfile,
						owner.World.WorldTick, CrossoverOvermatch(owner, formation, defenders), defenders.Count,
						plan.Actor.Info.Name, plan.Actor.ActorID, ThreatValue(owner, formation, plan.Actor));
				}
			}

			var targetReviewInterval = owner.StealthProfile == "stealth-tank" ?
				StealthAISpecialistPolicy.StrategicTargetReviewIntervalTicks(
					owner.World.Timestep, info.AirInfluenceCacheInterval) : info.AirInfluenceCacheInterval;
			owner.AirNextTargetReviewTick = owner.World.WorldTick + targetReviewInterval;
			if (plan.Route != null)
				owner.AirRoute.AddRange(plan.Route);

			if (!plan.ClearsAa || plan.SupportSquads == null)
				return;

			foreach (var support in plan.SupportSquads)
			{
				if (!support.IsValid || support == owner)
					continue;

				var supportUnits = AirDecisionUnits(support);
				if (supportUnits.Count == 0 || !supportUnits.Any(a => CanAttackTarget(a, plan.Actor)))
					continue;

				var route = SafeRouteForAircraft(
					support, supportUnits[0], plan.Actor, requireInfluenceCache: true);
				if (route == null)
					continue;

				ApplyAirTargetPlan(support, new AirTargetPlan(
					plan.Actor, plan.Score, false, route, clearsAa: true,
					aaProtectedCell: plan.AaProtectedCell, aaThreatIds: plan.AaThreatIds));
				support.FuzzyStateMachine.ChangeState(support, new StealthAIAttackState(), true);
				if (info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Air AA-clear [{0}] coordinated support [{1}] with {2} aircraft against {3}#{4} via {5} waypoints.",
						owner.AirProfile, support.AirProfile, supportUnits.Count,
						plan.Actor.Info.Name, plan.Actor.ActorID, route.Count);
			}
		}

		protected static void ClearAaTargetContext(Squad owner)
		{
			owner.AirTargetClearsAa = false;
			owner.AirAaClearProtectedCell = null;
			owner.AirAaClearThreatIds.Clear();
			owner.AirAaClearEngaged = false;
			if (owner.Type == SquadType.Stealth)
			{
				owner.StealthClearMode = StealthClearMode.None;
				owner.StealthAggressiveMass = false;
				owner.StealthClearCenterCell = null;
				owner.StealthClearPackage.Clear();
				owner.StealthClearMembershipSignature = 0;
				owner.StealthKiteTargetCell = null;
				owner.StealthKiteParticipantHealth.Clear();
				owner.StealthCrushLeaderActorId = 0;
				owner.StealthCrushTargetCell = null;
				owner.StealthValidatedFiringCells.Clear();
			}
		}

		protected static List<CPos> SafeRouteForAircraft(Squad owner, Actor aircraft, Actor target,
			bool requireInfluenceCache = false)
		{
			// BEGIN CNC96A GROUND EXTENSION
			if (owner.Type == SquadType.Stealth)
				return SafeRouteForStealth(owner, aircraft, target);
			// END CNC96A GROUND EXTENSION

			return SafeRouteForAircraft(owner, aircraft, target.Location, requireInfluenceCache);
		}

		static List<CPos> SafeRouteForAircraft(Squad owner, Actor aircraft, CPos targetCell,
			bool requireInfluenceCache = false)
		{
			var info = owner.SquadManager.Info;
			var speed = aircraft.Info.TraitInfoOrDefault<AircraftInfo>()?.Speed ?? info.AirTargetReferenceSpeed;
			if (!InfluenceCaches.TryGetValue(owner.SquadManager, out var profileCaches) ||
				!profileCaches.TryGetValue(owner.AirProfile + ":" + speed, out var cache))
				return requireInfluenceCache ? null :
					(owner.AirRoute.Count > 0 ? owner.AirRoute.ToList() : null);

			var map = owner.World.Map;
			var coarseSize = StealthCoarseSize(owner);
			var start = map.CellContaining(aircraft.CenterPosition);
			var startX = Math.Clamp(start.X / coarseSize, 0, cache.Width - 1);
			var startY = Math.Clamp(start.Y / coarseSize, 0, cache.Height - 1);
			var goalX = Math.Clamp(targetCell.X / coarseSize, 0, cache.Width - 1);
			var goalY = Math.Clamp(targetCell.Y / coarseSize, 0, cache.Height - 1);
			var route = FindAirRoute(owner, cache.Danger, cache.Width, cache.Height,
				startX, startY, goalX, goalY, info.AirRouteThreatPenalty);
			if (route == null)
				return null;

			return ThreatAwareRoutePlanner.SmoothRoute(
				cache.Danger, cache.Width, cache.Height, startX, startY, route)
				.Select(p => map.Clamp(new CPos(
					p.X * coarseSize + coarseSize / 2, p.Y * coarseSize + coarseSize / 2))).ToList();
		}

		/// <summary>
		/// Builds a conservative live AA map for an independently controlled aircraft such as a transport.
		/// Unlike Orca combat routing this deliberately applies no fly-by discount: a carrier may stop to
		/// load or unload, so every live threat uses its full configured effectiveness. The route planner
		/// uses finite costs and therefore returns the least-dangerous route when no threat-free path exists.
		/// </summary>
		internal static List<CPos> SafeIndependentAirRoute(SquadManagerBotModule manager, Actor aircraft, CPos targetCell)
		{
			if (manager == null || aircraft == null || aircraft.IsDead || !aircraft.IsInWorld)
				return null;

			var info = manager.Info;
			var map = manager.World.Map;
			var coarseSize = info.AirInfluenceCellSize;
			var width = (map.MapSize.X + coarseSize - 1) / coarseSize;
			var height = (map.MapSize.Y + coarseSize - 1) / coarseSize;
			var danger = new float[width * height];
			foreach (var enemy in manager.World.Actors)
			{
				if (!manager.IsPreferredEnemyUnit(enemy))
					continue;

				var profile = AntiAirProfile(enemy);
				var weight = StealthAIThreatGeometry.ConfiguredThreatWeight(enemy.Info.Name, profile.Weight,
					info.AirThreatWeightOverrides);
				if (weight <= 0)
					continue;

				var range = Math.Max(1, (int)Math.Ceiling(profile.RangeCells * info.AirThreatRangeBuffer));
				var mobile = enemy.Info.TraitInfoOrDefault<MobileInfo>();
				if (mobile != null)
					range += StealthAIThreatGeometry.MobileThreatBufferCells(mobile.Speed, info.AirInfluenceCacheInterval);

				var minX = Math.Max(0, (enemy.Location.X - range) / coarseSize);
				var maxX = Math.Min(width - 1, (enemy.Location.X + range) / coarseSize);
				var minY = Math.Max(0, (enemy.Location.Y - range) / coarseSize);
				var maxY = Math.Min(height - 1, (enemy.Location.Y + range) / coarseSize);
				for (var y = minY; y <= maxY; y++)
					for (var x = minX; x <= maxX; x++)
					{
						var cell = map.Clamp(new CPos(x * coarseSize + coarseSize / 2, y * coarseSize + coarseSize / 2));
						if ((map.CenterOfCell(cell) - enemy.CenterPosition).Length / 1024f <= range)
							danger[y * width + x] += weight;
					}
			}

			var start = map.CellContaining(aircraft.CenterPosition);
			var startX = Math.Clamp(start.X / coarseSize, 0, width - 1);
			var startY = Math.Clamp(start.Y / coarseSize, 0, height - 1);
			var goalX = Math.Clamp(targetCell.X / coarseSize, 0, width - 1);
			var goalY = Math.Clamp(targetCell.Y / coarseSize, 0, height - 1);
			var route = ThreatAwareRoutePlanner.FindRoute(
				danger, width, height, startX, startY, goalX, goalY, info.AirRouteThreatPenalty);
			if (route == null)
				return null;

			return ThreatAwareRoutePlanner.SmoothRoute(danger, width, height, startX, startY, route)
				.Select(p => map.Clamp(new CPos(
					p.X * coarseSize + coarseSize / 2, p.Y * coarseSize + coarseSize / 2))).ToList();
		}

		/// <summary>
		/// Returns the conservative live stopping threat at a carrier destination. This shares the exact
		/// weapon, veterancy-range, configured-effectiveness, safety-margin, and mobile-AA assumptions used
		/// by independent carrier routing, but avoids constructing a full influence map for drop-site ranking.
		/// </summary>
		internal static float SafeIndependentAirThreatAt(SquadManagerBotModule manager, CPos targetCell)
		{
			if (manager == null)
				return float.MaxValue;

			var info = manager.Info;
			var destination = manager.World.Map.CenterOfCell(targetCell);
			var danger = 0f;
			foreach (var enemy in manager.World.Actors)
			{
				if (!manager.IsPreferredEnemyUnit(enemy))
					continue;

				var profile = AntiAirProfile(enemy);
				var weight = StealthAIThreatGeometry.ConfiguredThreatWeight(enemy.Info.Name, profile.Weight,
					info.AirThreatWeightOverrides);
				if (weight <= 0)
					continue;

				var range = Math.Max(1, (int)Math.Ceiling(profile.RangeCells * info.AirThreatRangeBuffer));
				var mobile = enemy.Info.TraitInfoOrDefault<MobileInfo>();
				if (mobile != null)
					range += StealthAIThreatGeometry.MobileThreatBufferCells(mobile.Speed, info.AirInfluenceCacheInterval);

				if ((destination - enemy.CenterPosition).Length / 1024f <= range)
					danger += weight;
			}

			return danger;
		}

		static List<CPos> NearestSafeRouteFromFormation(Squad owner)
		{
			var info = owner.SquadManager.Info;
			var units = AirDecisionUnits(owner);
			if (units.Count == 0)
				return null;

			var speed = units.Select(a => a.Info.TraitInfoOrDefault<AircraftInfo>())
				.Where(a => a != null).Min(a => a.Speed);
			if (!InfluenceCaches.TryGetValue(owner.SquadManager, out var profileCaches) ||
				!profileCaches.TryGetValue(owner.AirProfile + ":" + speed, out var cache))
				return null;

			var map = owner.World.Map;
			var coarseSize = StealthCoarseSize(owner);
			var start = map.CellContaining(owner.AirFormationCenter);
			var startX = Math.Clamp(start.X / coarseSize, 0, cache.Width - 1);
			var startY = Math.Clamp(start.Y / coarseSize, 0, cache.Height - 1);
			var route = ThreatAwareRoutePlanner.FindNearestSafeRoute(
				cache.Danger, cache.Width, cache.Height, startX, startY, info.AirRouteThreatPenalty);
			if (route == null || route.Count == 0)
				return route;

			return ThreatAwareRoutePlanner.SmoothRoute(
				cache.Danger, cache.Width, cache.Height, startX, startY, route)
				.Select(p => map.Clamp(new CPos(
					p.X * coarseSize + coarseSize / 2, p.Y * coarseSize + coarseSize / 2))).ToList();
		}

		static bool BeginRoutedLocalAaEscape(Squad owner)
		{
			var info = owner.SquadManager.Info;

			// Local safety runs more often than the strategic cache. Force this rare emergency replan to
			// include the live AA actor that triggered it instead of escaping on a map up to five seconds old.
			if (InfluenceCaches.TryGetValue(owner.SquadManager, out var profileCaches))
				foreach (var key in profileCaches.Keys
					.Where(k => k.StartsWith(owner.AirProfile + ":", StringComparison.OrdinalIgnoreCase)).ToList())
					profileCaches.Remove(key);

			var target = FindBestAirTarget(owner, escapeFromAa: true);
			if (target != null && target.IsUndefended && target.Route != null && target.Route.Count > 1)
			{
				ApplyAirTargetPlan(owner, target);
				owner.AirEscapingLocalAa = true;
				owner.FuzzyStateMachine.ChangeState(owner, new StealthAIAttackState(), true);
				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air escape [{0}] routed toward safe target {1}#{2}: waypoints={3} score={4}.",
						owner.AirProfile, target.Actor.Info.Name, target.Actor.ActorID,
						target.Route.Count, target.Score);

				return true;
			}

			var route = NearestSafeRouteFromFormation(owner);
			if (route == null || route.Count == 0)
				return false;

			owner.TargetActor = null;
			ClearAaTargetContext(owner);
			owner.AirRoute.Clear();
			owner.AirRouteQueued = false;
			var destination = route[route.Count - 1];
			foreach (var aircraft in owner.Units)
			{
				if (SendHomeToResupply(owner, aircraft) || SendHomeToRepair(owner, aircraft))
					continue;

				if (owner.AirReinforcements.Contains(aircraft.ActorID))
				{
					QueueSafeMoveForReinforcement(owner, aircraft, destination);
					continue;
				}

				var queued = false;
				foreach (var waypoint in route)
				{
					owner.Bot.QueueOrder(new Order("Move", aircraft,
						Target.FromCell(owner.World, waypoint), queued));
					queued = true;
				}
			}

			owner.AirEscapingLocalAa = true;
			owner.FuzzyStateMachine.ChangeState(owner, new StealthAIFleeState(), true);
			if (info.AirTargetDebugLogging)
				Log.Write("debug", "Air escape [{0}] routed to nearest safe cell {1}: waypoints={2}.",
					owner.AirProfile, destination, route.Count);

			return true;
		}

		// New and repaired aircraft remain reinforcements until they reach the target's coarse cell or one
		// of its neighbors. They always receive a route from their own position and never inherit the
		// formation's shared route while catching up.

		protected static void QueueStealthReinforcementsToFormation(Squad owner)
		{
			if (owner.StealthProfile != "stealth-tank")
				return;

			var formation = owner.AirFormationUnits();
			if (formation.Count == 0)
				return;
			if (!owner.SquadManager.TryConsumeStealthCatchUpRoutingAllowance(owner))
				return;

			var anchorCell = CoarseCell(owner, owner.World.Map.CellContaining(owner.AirFormationCenter));

			foreach (var reinforcement in owner.Units.Where(unit =>
				owner.AirReinforcements.Contains(unit.ActorID) &&
				!owner.AirUnitsRepairing.Contains(unit.ActorID)))
			{
				// Catch-up belongs to the active formation, not the volatile economic mission target.
				// A moving route remains useful even when the formation center advances several strategic
				// cells during its mission. Re-anchor only after the order finishes/stalls or its actor dies.
				var hasJoinCell = owner.AirReinforcementJoinCells.TryGetValue(
					reinforcement.ActorID, out var previousAnchorCell);
				var routedAnchorValid = owner.AirReinforcementTargets.TryGetValue(
					reinforcement.ActorID, out var routedAnchorId) &&
					owner.World.GetActorById(routedAnchorId) is Actor routedAnchor &&
					!routedAnchor.IsDead && routedAnchor.IsInWorld && routedAnchor.Owner == owner.Bot.Player;
				if (!reinforcement.IsIdle && routedAnchorValid)
				{
					owner.StealthReinforcementRoutePreserves++;
					if (owner.SquadManager.Info.AirTargetDebugLogging)
						Log.Write("debug", "Air reinforcement [{0}] {1}#{2}: preserved progressing " +
							"formation catch-up route; anchor-cell={3} current-center-cell={4} " +
							"issues={5} preserves={6}.",
							owner.AirProfile, reinforcement.Info.Name, reinforcement.ActorID,
							hasJoinCell ? previousAnchorCell.ToString() : "none", anchorCell,
							owner.StealthReinforcementRouteIssues, owner.StealthReinforcementRoutePreserves);
					continue;
				}

				var anchor = formation.OrderBy(unit =>
					(unit.Location - reinforcement.Location).LengthSquared)
					.ThenBy(unit => unit.ActorID).First();
				QueueSafeRouteForReinforcement(owner, reinforcement, anchor);
				owner.AirReinforcementJoinCells[reinforcement.ActorID] = anchorCell;
				owner.StealthReinforcementRouteIssues++;

				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Air reinforcement [{0}] {1}#{2}: catch-up anchor cell={3} " +
						"previous={4} anchor-valid={5} idle={6} issues={7} preserves={8}.",
						owner.AirProfile, reinforcement.Info.Name, reinforcement.ActorID, anchorCell,
						hasJoinCell ? previousAnchorCell.ToString() : "none", routedAnchorValid,
						reinforcement.IsIdle, owner.StealthReinforcementRouteIssues,
						owner.StealthReinforcementRoutePreserves);
			}
		}

		protected static void QueueSafeRouteForReinforcement(Squad owner, Actor aircraft, Actor target)
		{
			var route = SafeRouteForAircraft(owner, aircraft, target);
			if (route == null)
			{
				if (owner.Type == SquadType.Stealth)
				{
					var anchor = owner.AirFormationUnits().Where(unit => unit != aircraft)
						.OrderBy(unit => (unit.Location - aircraft.Location).LengthSquared)
						.ThenBy(unit => unit.ActorID).FirstOrDefault();
					var joinRoute = anchor == null ? null : SafeRouteForStealth(owner, aircraft, anchor);

					var queuedJoin = false;
					if (joinRoute != null)
						foreach (var waypoint in joinRoute.Where(waypoint => waypoint != aircraft.Location))
						{
							owner.Bot.QueueOrder(new Order("Move", aircraft,
								Target.FromCell(owner.World, waypoint), queuedJoin));
							queuedJoin = true;
						}

					if (queuedJoin)
					{
						owner.AirReinforcementTargets[aircraft.ActorID] = target.ActorID;
						owner.AirReinforcementFallbackCells.Remove(aircraft.ActorID);
						owner.AirReinforcementFallbackTicks.Remove(aircraft.ActorID);
						if (owner.SquadManager.Info.AirTargetDebugLogging)
							Log.Write("debug", "Air reinforcement [{0}] {1}#{2}: target route unavailable; queued {3}-waypoint safe formation join toward {4}.",
								owner.AirProfile, aircraft.Info.Name, aircraft.ActorID,
								joinRoute.Count, anchor.Info.Name + "#" + anchor.ActorID);

						return;
					}

					// A cached safe route remains the first choice. If neither the mission nor the
					// active formation has one from this unit's current position, use one ordinary
					// ground Move toward the current formation strategic cell. The engine owns the
					// detailed path, the normal local safety loop may still preempt genuine hazards,
					// and same/adjacent promotion prevents this catch-up order from stalling the core.
					var coarseSize = StealthCoarseSize(owner);
					var fallbackCoarse = CoarseCell(owner, anchor?.Location ?? target.Location);
					var fallback = owner.World.Map.Clamp(new CPos(
						fallbackCoarse.X * coarseSize + coarseSize / 2,
						fallbackCoarse.Y * coarseSize + coarseSize / 2));
					var retryTicks = Math.Max(1, owner.SquadManager.Info.AirInfluenceCacheInterval);
					var recentlyIssued = owner.AirReinforcementFallbackCells.TryGetValue(
						aircraft.ActorID, out var previousFallback) && previousFallback == fallback &&
						owner.AirReinforcementFallbackTicks.TryGetValue(aircraft.ActorID, out var previousTick) &&
						owner.World.WorldTick - previousTick < retryTicks;
					owner.AirReinforcementTargets[aircraft.ActorID] = target.ActorID;
					if (recentlyIssued)
						return;

					owner.Bot.QueueOrder(new Order("Move", aircraft,
						Target.FromCell(owner.World, fallback), false));
					owner.AirReinforcementFallbackCells[aircraft.ActorID] = fallback;
					owner.AirReinforcementFallbackTicks[aircraft.ActorID] = owner.World.WorldTick;
					if (owner.SquadManager.Info.AirTargetDebugLogging)
						Log.Write("debug", "Air reinforcement [{0}] {1}#{2}: no cached safe route; " +
							"issued rate-limited direct catch-up Move to active cell {3} at {4}.",
							owner.AirProfile, aircraft.Info.Name, aircraft.ActorID, fallbackCoarse, fallback);

					return;
				}

				// Air squads retain their existing no-route behavior.
				owner.AirReinforcementTargets.Remove(aircraft.ActorID);
				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Air route [{0}] {1}#{2}: preserving current order toward {3}#{4}; no current-position safe route is available.",
						owner.AirProfile, aircraft.Info.Name, aircraft.ActorID, target.Info.Name, target.ActorID);

				return;
			}

			var queued = false;
			foreach (var waypoint in route)
			{
				owner.Bot.QueueOrder(new Order("Move", aircraft, Target.FromCell(owner.World, waypoint), queued));
				queued = true;
			}

			if (CanAttackTarget(aircraft, target))
				owner.Bot.QueueOrder(new Order("Attack", aircraft, Target.FromActor(target), queued));

			owner.AirReinforcementTargets[aircraft.ActorID] = target.ActorID;
			owner.AirReinforcementFallbackCells.Remove(aircraft.ActorID);
			owner.AirReinforcementFallbackTicks.Remove(aircraft.ActorID);

			if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Air route [{0}] {1}#{2}: queued current-position safe route ({3} waypoints) to {4}#{5}.",
					owner.AirProfile, aircraft.Info.Name, aircraft.ActorID, route.Count, target.Info.Name, target.ActorID);
		}

		static void QueueSafeMoveForReinforcement(Squad owner, Actor aircraft, CPos destination)
		{
			var route = SafeRouteForAircraft(owner, aircraft, destination);
			var queued = false;
			if (route != null)
				foreach (var waypoint in route)
				{
					owner.Bot.QueueOrder(new Order("Move", aircraft,
						Target.FromCell(owner.World, waypoint), queued));
					queued = true;
				}

			if (!queued)
				owner.Bot.QueueOrder(new Order("Move", aircraft,
					Target.FromCell(owner.World, destination), false));

			owner.AirReinforcementTargets.Remove(aircraft.ActorID);
			if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Air reinforcement [{0}] {1}#{2}: individual {3}-waypoint move to {4}.",
					owner.AirProfile, aircraft.Info.Name, aircraft.ActorID, route?.Count ?? 0, destination);
		}

		/// <summary>
		/// The per-archetype target priority table for this squad's aircraft type (Orca vs Heli), or null
		/// when neither applies - either the squad's aircraft type is not one either table is configured
		/// for, or both tables are empty (the feature is off). Callers compute this once per scan, not
		/// once per candidate: it only depends on the squad's own composition, not on the target.
		/// </summary>
		static Dictionary<string, int> ArchetypePriority(Squad owner)
		{
			var info = owner.SquadManager.Info;
			if (info.AirTargetPriorityOrca.Count == 0 && info.AirTargetPriorityHeli.Count == 0)
				return null;

			if (owner.AirProfile.Equals("Orca", StringComparison.OrdinalIgnoreCase))
				return info.AirTargetPriorityOrca;

			if (owner.AirProfile.Equals("Apache", StringComparison.OrdinalIgnoreCase))
				return info.AirTargetPriorityHeli;

			// Legacy generic squads infer their profile from their members.
			foreach (var u in owner.Units)
			{
				if (info.AirTargetPriorityOrca.Count > 0 && u.Info.Name == info.OrcaArchetypeActor)
					return info.AirTargetPriorityOrca;

				if (info.AirTargetPriorityHeli.Count > 0 && u.Info.Name == info.HeliArchetypeActor)
					return info.AirTargetPriorityHeli;
			}

			return null;
		}

		static int TargetValue(Actor a, SquadManagerBotModuleInfo info, Dictionary<string, int> archetypePriority)
		{
			if (archetypePriority != null && archetypePriority.TryGetValue(a.Info.Name, out var overrideValue))
				return overrideValue;

			switch (Classify(a))
			{
				case AirTargetClass.Harvester: return info.AirTargetHarvesterValue;
				case AirTargetClass.Production: return info.AirTargetProductionValue;
				case AirTargetClass.Building: return info.AirTargetBuildingValue;
				case AirTargetClass.Wall: return info.AirTargetWallValue;
				default: return info.AirTargetUnitValue;
			}
		}

		static AirTargetClass Classify(Actor a)
		{
			if (a.Info.HasTraitInfo<HarvesterInfo>())
				return AirTargetClass.Harvester;

			if (a.Info.HasTraitInfo<LineBuildNodeInfo>())
				return AirTargetClass.Wall;

			if (!a.Info.HasTraitInfo<BuildingInfo>())
				return AirTargetClass.Unit;

			if (a.Info.HasTraitInfo<ProductionInfo>() || a.Info.HasTraitInfo<RefineryInfo>())
				return AirTargetClass.Production;

			return AirTargetClass.Building;
		}

		static bool TryContinueLiveAaClear(Squad owner, List<Actor> localThreats)
		{
			if (!owner.IsTargetValid || localThreats.Count == 0)
				return false;

			var selectedTargetInRange = localThreats.Any(a => a == owner.TargetActor);
			var allLocalThreatsPlanned = localThreats.All(a => owner.AirAaClearThreatIds.Contains(a.ActorID));
			var response = StealthAIThreatGeometry.PlannedAaClearResponse(
				owner.AirTargetClearsAa, selectedTargetInRange, allLocalThreatsPlanned);
			if (response == StealthAILocalAaClearResponse.Flee)
				return false;

			if (response == StealthAILocalAaClearResponse.Continue)
				return true;

			var targetId = owner.TargetActor.ActorID;
			var participants = owner.SquadManager.Squads.Where(s =>
				s.IsValid && s.Type == SquadType.Air && s.AirTargetClearsAa &&
				s.IsTargetValid && s.TargetActor.ActorID == targetId).ToList();
			if (!participants.Contains(owner))
				participants.Add(owner);

			var combinedUnits = participants.SelectMany(AirDecisionUnits).Distinct().ToList();
			var distinctThreats = localThreats.Distinct().OrderBy(a => a.ActorID).ToList();
			var attackUnits = combinedUnits.Where(u => distinctThreats.Any(t => CanAttackTarget(u, t))).ToList();
			var combinedValue = AmmoWeightedSquadValue(attackUnits);
			var dangerValue = distinctThreats.Sum(a =>
				EconomicValue(a) * (double)StoppingThreatWeight(owner, a, AntiAirProfile(a).Weight));
			var clearTicks = EstimatedKillTicks(combinedUnits, distinctThreats);
			var noUndefendedScans = participants.Max(s => s.AirConsecutiveNoUndefendedScans);
			var info = owner.SquadManager.Info;
			var referenceThreatWeight = AaClearReferenceThreatWeight(owner);
			var eligible = clearTicks != long.MaxValue && StealthAIThreatGeometry.CanAttemptAaClear(
				noUndefendedScans, info.AirTargetAaClearFallbackScans,
				combinedValue, referenceThreatWeight, dangerValue, info.AirTargetAaClearValueRatio);

			if (info.AirTargetDebugLogging)
				Log.Write("debug", "Air AA-clear [{0}] live recalculation near {1}: selected-in-range={2} planned-threats={3}/{4} participants={5} aircraft={6} ammo-value={7} reference-weight={8:0.##} aircraft-strength={9:0.##} danger-value={10:0.##} required-ratio={11} clear-ticks={12} eligible={13}.",
					owner.AirProfile, owner.TargetActor.Info.Name + "#" + owner.TargetActor.ActorID,
					selectedTargetInRange, distinctThreats.Count(a => owner.AirAaClearThreatIds.Contains(a.ActorID)),
					distinctThreats.Count, participants.Count, combinedUnits.Count,
					combinedValue, referenceThreatWeight, combinedValue * referenceThreatWeight,
					dangerValue, info.AirTargetAaClearValueRatio, clearTicks, eligible);

			if (!eligible)
				return false;

			var threatIds = new HashSet<uint>(participants.SelectMany(s => s.AirAaClearThreatIds));
			threatIds.UnionWith(distinctThreats.Select(a => a.ActorID));
			var focus = selectedTargetInRange ? owner.TargetActor : distinctThreats
				.OrderBy(a => EstimatedKillTicks(combinedUnits, new[] { a }))
				.ThenBy(a => a.ActorID).First();
			if (focus == owner.TargetActor)
			{
				foreach (var participant in participants)
					participant.AirAaClearThreatIds.UnionWith(threatIds);

				return true;
			}

			ApplyAirTargetPlan(owner, new AirTargetPlan(
				focus, owner.AirTargetScore, false, null, clearsAa: true,
				supportSquads: participants.Where(s => s != owner).ToList(),
				aaProtectedCell: owner.AirAaClearProtectedCell, aaThreatIds: threatIds));
			owner.FuzzyStateMachine.ChangeState(owner, new StealthAIAttackState(), true);
			if (info.AirTargetDebugLogging)
				Log.Write("debug", "Air AA-clear [{0}] live recalculation focused local threat {1}#{2} before resuming protected cell {3}.",
					owner.AirProfile, focus.Info.Name, focus.ActorID,
					owner.AirAaClearProtectedCell?.ToString() ?? "none");

			return true;
		}

		/// <summary>
		/// Squad-local anti-air awareness, driven by <see cref="SquadManagerBotModuleInfo.AirSafetyCheckInterval"/>
		/// rather than by the squad state machine. Because it looks around the squad's own position it covers
		/// the whole harassment run - approach, attack and the way out - instead of only the moment a
		/// target is chosen. Disabled (and behaviour unchanged) when the interval is zero.
		///
		/// It doubles as the squad's local target search. The actors worth shooting are the same actors we
		/// are already enumerating for anti-air, so scoring them here costs nothing extra and lets the squad
		/// strike again as soon as an evasion hop lands, instead of idling until the next state machine tick
		/// (AttackForceInterval, typically three times slower). This is the "dip in, hit something, slip out,
		/// come back" half of the harassment loop.
		///
		/// PERF: exactly one FindActorsInCircle per air squad per interval, bounded by AirThreatScanRadius.
		/// </summary>
		internal static void TickAirSafety(Squad owner)
		{
			var info = owner.SquadManager.Info;
			if (info.AirSafetyCheckInterval <= 0 || !owner.IsValid)
				return;

			// Repair is an individual lifecycle decision, not a reason to retreat the entire squad.
			// Running it here makes the configured safety cadence apply in every squad state.
			foreach (var unit in owner.Units)
				SendHomeToRepair(owner, unit);

			PromoteArrivedAirReinforcements(owner);
			RoutePendingStealthReinforcements(owner);

			var tick = owner.World.WorldTick;
			owner.ForgetExpiredAirThreats(tick);

			var mergeRadius = WDist.FromCells(info.AirThreatMemoryMergeRadius);
			var expiry = tick + info.AirThreatMemoryTicks;
			var squadCenter = owner.AirFormationCenter;
			var archetypePriority = ArchetypePriority(owner);

			var antiAirWeight = 0f;
			var localThreats = new List<Actor>();
			var ownBuildingNear = false;

			Actor localTarget = null;
			var localScore = int.MinValue;

			// Widened by AirThreatRangeBuffer so a long-range defender is not missed entirely by a scan
			// sized for the stock flat radius; the per-defender buffered-range check below still applies
			// on top, so a short-range defender near the edge of this wider circle is correctly ignored.
			var scanRadiusCells = (int)Math.Ceiling(info.AirThreatScanRadius * info.AirThreatRangeBuffer);

			// PERF: single bounded scan, no intermediate list, no LINQ.
			foreach (var a in owner.World.FindActorsInCircle(squadCenter, WDist.FromCells(scanRadiusCells)))
			{
				if (a.Owner == owner.Bot.Player)
				{
					if (!ownBuildingNear && a.Info.HasTraitInfo<BuildingInfo>())
						ownBuildingNear = true;

					continue;
				}

				if (!owner.SquadManager.IsPreferredEnemyUnit(a))
					continue;

				var profile = AntiAirProfile(a);
				var stoppingWeight = StoppingThreatWeight(owner, a, profile.Weight);
				if (stoppingWeight > 0)
				{
					// Local safety never receives the Orca transit discount. A fast aircraft may route
					// past a slower missile, but hovering or attacking inside its range remains lethal.
					owner.RememberAirThreat(a.CenterPosition, expiry, mergeRadius, info.AirThreatMemorySize);

					var distanceInCells = (a.CenterPosition - squadCenter).Length / 1024f;
					var withinRange = StealthAIThreatGeometry.IsWithinBufferedRange(distanceInCells, profile.RangeCells, info.AirThreatRangeBuffer);
					if (withinRange)
					{
						antiAirWeight += stoppingWeight;
						localThreats.Add(a);
					}

					continue;
				}

				if (!owner.SquadManager.IsNotHiddenUnit(a))
					continue;

				// Everything reaching here has zero configured AA weight, so the defenceless bonus applies.
				// No anti-air or route penalty: whether this candidate is safe is decided below, by the same
				// scan's anti-air count, rather than per candidate.
				var distanceInCellsToTarget = (a.CenterPosition - squadCenter).Length / 1024;
				var score = StealthAIThreatGeometry.TargetScore(
					TargetValue(a, info, archetypePriority), true, info.AirTargetDefencelessBonus,
					0, 0, 0, 0,
					distanceInCellsToTarget, info.AirTargetDistancePenalty);

				if (localTarget == null || score > localScore)
				{
					localTarget = a;
					localScore = score;
				}
			}

			// Empty self-reloading aircraft use this on their strategic tick to decide whether they can
			// safely hold position and finish reloading instead of automatically abandoning the mission.
			owner.AirLocalThreatWeight = ownBuildingNear ? 0 : antiAirWeight;
			if (antiAirWeight <= 0)
			{
				owner.AirEscapingLocalAa = false;
				owner.AirAaClearEngaged = false;
			}
			else if (owner.AirEscapingLocalAa && (owner.AirRoute.Count > 1 ||
				owner.Units.Any(a => !owner.AirUnitsRepairing.Contains(a.ActorID) &&
					!a.IsIdle && !BusyAttack(a))))
			{
				// The strategic escape route is already carrying the squad out of this pocket. Replacing it
				// with another local reverse hop is the bounce loop this state is designed to prevent.
				return;
			}

			// Adaptive aggression may help finish an attack after the squad has reached the selected
			// strategic cell. It must never make the approach, withdrawal, repair, or evasion less safe.
			var inTargetCell = false;
			if (owner.IsTargetValid)
			{
				var coarseSize = StealthCoarseSize(owner);
				var squadCell = owner.World.Map.CellContaining(owner.AirFormationCenter);
				var targetCell = owner.TargetActor.Location;
				inTargetCell = squadCell.X / coarseSize == targetCell.X / coarseSize &&
					squadCell.Y / coarseSize == targetCell.Y / coarseSize;
			}

			var localRisk = StealthAIThreatGeometry.LocalAirRiskMultiplier(inTargetCell,
				owner.SquadManager.AirRiskMultiplier(owner.AirProfile));
			var decisionUnits = AirDecisionUnits(owner);
			var effectiveSquadStrength = (int)Math.Min(int.MaxValue,
				Math.Ceiling(decisionUnits.Count * localRisk));
			var shouldFlee = !ownBuildingNear && StealthAIThreatGeometry.ShouldFleeAntiAir(
				antiAirWeight, info.AirThreatFleeMultiplier, effectiveSquadStrength);
			if (shouldFlee && TryContinueLiveAaClear(owner, localThreats))
			{
				if (!owner.AirAaClearEngaged && info.AirTargetDebugLogging)
					Log.Write("debug", "Air AA-clear [{0}] entered coordinated engagement against {1}#{2}: planned-threats={3} local-threats={4}.",
						owner.AirProfile, owner.TargetActor.Info.Name, owner.TargetActor.ActorID,
						owner.AirAaClearThreatIds.Count, localThreats.Count);

				owner.AirAaClearEngaged = true;
				return;
			}

			if (shouldFlee)
			{
				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air evade [{0}] local AA safety: threat={1:0.##} flee-multiplier={2} effective-strength={3} risk={4:0.00} in-target-cell={5} target={6}.",
						owner.AirProfile, antiAirWeight, info.AirThreatFleeMultiplier, effectiveSquadStrength,
						localRisk, inTargetCell, owner.IsTargetValid ?
						owner.TargetActor.Info.Name + "#" + owner.TargetActor.ActorID : "none");

				// Drop the unsafe target, then ask the strategic router for a lower-exposure target or at
				// least the nearest safe coarse cell. The old direct reverse hop remains only as a fallback
				// when the influence map has no usable route.
				owner.TargetActor = null;
				ClearAaTargetContext(owner);
				if (!BeginRoutedLocalAaEscape(owner))
				{
					Evade(owner, "local AA safety route unavailable");
					owner.FuzzyStateMachine.ChangeState(owner, new StealthAIFleeState(), true);
				}

				return;
			}

			// Only ever commit to a local target when this scan saw no anti-air at all within
			// AirThreatScanRadius, so the fast path can never walk the squad into cover it just measured.
			var hasFullyLoadedUnit = decisionUnits.Any(a =>
			{
				if (owner.AirUnitsRepairing.Contains(a.ActorID))
					return false;

				var pools = a.TraitsImplementing<AmmoPool>().ToArray();
				return pools.Length > 0 && FullAmmo(pools);
			});
			if (antiAirWeight > 0 || localTarget == null || localScore < info.AirTargetMinimumScore ||
				owner.IsTargetValid || !hasFullyLoadedUnit)
				return;

			if (info.AirTargetDebugLogging)
				Log.Write("debug", "Air target [{0}] local opportunity selected {1}#{2}: score={3} full-ammo=True local-AA=0.",
					owner.AirProfile, localTarget.Info.Name, localTarget.ActorID, localScore);

			ApplyAirTargetPlan(owner, new AirTargetPlan(localTarget, localScore, true, null));
			owner.FuzzyStateMachine.ChangeState(owner, new StealthAIAttackState(), true);
		}

		/// <summary>
		/// Breaks off a run. With <see cref="SquadManagerBotModuleInfo.AirEvadeDistance"/> set this is a short
		/// hop directly away from the nearest anti-air the squad remembers, plus a random lateral wander, so
		/// the squad stays next to the enemy base and can turn straight back in - rather than the flight all
		/// the way home that the players (rightly) called out as enormous and stupid. Going home is reserved
		/// for aircraft that actually need to rearm.
		/// With AirEvadeDistance at zero this falls back to the stock retreat to an own building, so other
		/// mods and bots are unaffected.
		/// Rate limited by <see cref="SquadManagerBotModuleInfo.AirRetreatOrderInterval"/>: within that window
		/// it does nothing and the squad keeps flying the hop it was already given.
		/// </summary>
		protected static void Evade(Squad owner, string reason)
		{
			var info = owner.SquadManager.Info;
			var tick = owner.World.WorldTick;

			// An air squad sitting in anti-air cover must not re-issue move orders on every safety check.
			if (tick < owner.NextAirRetreatTick)
			{
				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air evade [{0}] suppressed by rate limit for {1} ticks: reason={2}.",
						owner.AirProfile, owner.NextAirRetreatTick - tick, reason);

				return;
			}

			owner.NextAirRetreatTick = tick + info.AirRetreatOrderInterval;

			if (info.AirEvadeDistance <= 0)
			{
				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air evade [{0}] falling back to base retreat: reason={1}.", owner.AirProfile, reason);

				Retreat(owner);
				return;
			}

			var destination = EvadeDestination(owner);
			if (info.AirTargetDebugLogging)
				Log.Write("debug", "Air evade [{0}] moving to {1}: reason={2} remembered-threats={3}.",
					owner.AirProfile, destination, reason, owner.AirThreatPositions.Count);
			foreach (var a in owner.Units)
			{
				if (SendHomeToResupply(owner, a) || SendHomeToRepair(owner, a))
					continue;

				if (owner.AirReinforcements.Contains(a.ActorID))
					QueueSafeMoveForReinforcement(owner, a, destination);
				else
					owner.Bot.QueueOrder(new Order("Move", a, Target.FromCell(owner.World, destination), false));
			}
		}

		/// <summary>Picks the cell for a local evasion hop and keeps it on the map.</summary>
		protected static CPos EvadeDestination(Squad owner)
		{
			var info = owner.SquadManager.Info;
			var map = owner.World.Map;

			owner.ForgetExpiredAirThreats(owner.World.WorldTick);

			// NOTE: Bot code runs on the host only and must never touch World.SharedRandom.
			var jitter = WVec.Zero;
			if (info.AirEvadeJitter > 0)
			{
				var spread = info.AirEvadeJitter * 1024;
				jitter = new WVec(owner.Random.Next(-spread, spread + 1), owner.Random.Next(-spread, spread + 1), 0);
			}

			var destination = StealthAIThreatGeometry.EvadeDestination(
				owner.AirFormationCenter, owner.AirThreatPositions, WDist.FromCells(info.AirEvadeDistance), jitter);

			return map.Clamp(map.CellContaining(destination));
		}

		/// <summary>
		/// Sends one aircraft home when it has run dry and cannot reload in the field. True when it did,
		/// in which case the caller must not also give it a move order.
		/// </summary>
		static bool SendHomeToResupply(Squad owner, Actor a)
		{
			var ammoPools = a.TraitsImplementing<AmmoPool>().ToArray();
			if (ReloadsAutomatically(ammoPools, a.TraitOrDefault<Rearmable>()) || FullAmmo(ammoPools))
				return false;

			if (!IsRearming(a))
				owner.Bot.QueueOrder(new Order("ReturnToBase", a, false));

			return true;
		}

		/// <summary>
		/// Sends one aircraft to repair when it drops below <see cref="SquadManagerBotModuleInfo.HealthRetreatThreshold"/>.
		/// True when it did (or is already en route), in which case the caller must not also give it a
		/// move/attack order.
		/// Scores safe owned repair actors and configured allied passive-repair auras. Owned actors receive
		/// a direct "Repair" order; allied actors are approached but never entered. The literal ReturnToBase order string
		/// requires a Rearmable trait (see Aircraft.cs's own order handling) which neither Orca nor Apache
		/// have, but "Repair" (alongside "Enter"/"ForceEnter") accepts an explicit destination actor and is
		/// gated on Repairable instead, which both of them do have.
		/// </summary>
		protected static bool SendHomeToRepair(Squad owner, Actor a)
		{
			var threshold = owner.SquadManager.Info.HealthRetreatThreshold;
			if (threshold <= 0)
				return false;

			var health = a.TraitOrDefault<IHealth>();
			if (health == null)
				return false;

			var repairing = owner.AirUnitsRepairing.Contains(a.ActorID);
			if (repairing && health.HP >= health.MaxHP)
			{
				owner.AirUnitsRepairing.Remove(a.ActorID);
				owner.AirRepairTargets.Remove(a.ActorID);
				owner.AirRepairWaiting.Remove(a.ActorID);
				owner.AirRepairWaitingSince.Remove(a.ActorID);
				owner.AirRepairUnavailable.Remove(a.ActorID);
				if (owner.Units.Count == 1)
					owner.JoinAirFormation(a);

				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Air repair [{0}] {1}#{2}: recovery complete.",
						owner.AirProfile, a.Info.Name, a.ActorID);

				return false;
			}

			if (!repairing && health.HP >= health.MaxHP * threshold)
			{
				owner.AirRepairUnavailable.Remove(a.ActorID);
				return false;
			}

			var repairable = a.Info.TraitInfoOrDefault<RepairableInfo>();
			if (repairable == null || repairable.RepairActors.Count == 0)
				return false;

			var threats = LiveRepairThreats(owner);
			Actor previousRepairTarget = null;
			var previousTargetDanger = float.MaxValue;
			if (repairing && owner.AirRepairTargets.TryGetValue(a.ActorID, out var repairTargetId))
			{
				var repairTarget = owner.World.GetActorById(repairTargetId);
				var passiveRange = PassiveRepairRange(owner, repairTarget);
				var targetEligible = repairTarget != null && !repairTarget.IsDead && repairTarget.IsInWorld &&
					repairable.RepairActors.Contains(repairTarget.Info.Name) &&
					(repairTarget.Owner == owner.Bot.Player || passiveRange > WDist.Zero);
				var targetDanger = !targetEligible ?
					float.MaxValue : RepairDestinationDanger(repairTarget.CenterPosition, threats);
				if (!RepairDestinationIsUnsafe(owner, targetDanger))
				{
					var waitingForPad = owner.AirRepairWaiting.Contains(a.ActorID);
					if (waitingForPad && (!IsReadyRepairWaiter(owner, a, repairTarget) ||
						!Reservable.IsAvailableFor(repairTarget, a)))
						return true;

					// A different squad may have replaced this aircraft's engine reservation after
					// both selected the same apparently free pad in one bot tick. Do not preserve
					// that stale non-idle Repair order: requeue it through the shared claim owner.
					if (!waitingForPad && !a.IsIdle && Reservable.IsAvailableFor(repairTarget, a))
						return true;

					if (passiveRange > WDist.Zero &&
						(repairTarget.CenterPosition - a.CenterPosition).HorizontalLength <= passiveRange.Length)
						return true;
				}

				previousRepairTarget = targetEligible ? repairTarget : null;
				previousTargetDanger = targetDanger;
				if (!targetEligible)
				{
					owner.AirRepairTargets.Remove(a.ActorID);
					owner.AirRepairWaiting.Remove(a.ActorID);
					owner.AirRepairWaitingSince.Remove(a.ActorID);
				}

				if (owner.SquadManager.Info.AirTargetDebugLogging && !targetEligible)
					Log.Write("debug", "Air repair [{0}] {1}#{2}: previous destination {3} became {4}; replanning.",
						owner.AirProfile, a.Info.Name, a.ActorID,
						repairTarget == null ? "unavailable" : repairTarget.Info.Name + "#" + repairTarget.ActorID,
						"unavailable");
			}

			var recovery = FindSafestRepairBuilding(owner, a, repairable, threats, requireAvailable: true);
			if (recovery.Building != null && owner.AirRepairWaiting.Contains(a.ActorID) &&
				!IsOldestReadyRepairWaiter(owner, a, recovery.Building))
				recovery = new AirRepairPlan();

			if (recovery.Building != null)
			{
				var passiveRange = PassiveRepairRange(owner, recovery.Building);
				if (recovery.RepairAtEnd || passiveRange <= WDist.Zero ||
					(recovery.Building.CenterPosition - a.CenterPosition).HorizontalLength > passiveRange.Length)
					QueueRecoveryRoute(owner, a, recovery.Route, recovery.Building, recovery.RepairAtEnd);

				owner.MarkAirRepairing(a, recovery.Building);
				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Air repair [{0}] {1}#{2}: {3}/{4} HP, safe route ({5} waypoints) to {6} {7}#{8}.",
						owner.AirProfile, a.Info.Name, a.ActorID, health.HP, health.MaxHP,
						recovery.Route?.Count ?? 0, recovery.RepairAtEnd ? "owned active" : "allied passive",
						recovery.Building.Info.Name, recovery.Building.ActorID);

				return true;
			}

			// Commit to recovery even when every pad is occupied. Waiting at the nearest compatible
			// facility keeps the aircraft out of combat, lets repair auras help where present, and retries
			// reservation on every safety update once it becomes idle.
			var waitingAt = FindSafestRepairBuilding(owner, a, repairable, threats, requireAvailable: false);
			if (waitingAt.Building != null)
			{
				QueueRecoveryRoute(owner, a, waitingAt.Route, waitingAt.Building, repairAtEnd: false);

				// This is a holding destination, not a pad claim. Keeping it out of the claim set
				// lets exactly one waiter reserve the pad after the current repair completes.
				owner.MarkAirRepairWaiting(a, waitingAt.Building);
				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Air repair [{0}] {1}#{2}: {3}/{4} HP, all pads occupied; safe wait route ({5} waypoints) to {6}#{7}.",
						owner.AirProfile, a.Info.Name, a.ActorID, health.HP, health.MaxHP,
						waitingAt.Route?.Count ?? 0, waitingAt.Building.Info.Name, waitingAt.Building.ActorID);

				return true;
			}

			// A compatible facility still exists, but every option is currently inside AA cover. Keep the
			// aircraft out of the formation and move it to a safe holding point. Re-evaluate on each safety
			// update so it can claim the facility as soon as the AA disappears, or rejoin only after every
			// compatible facility has actually been destroyed or lost.
			if (waitingAt.CandidateCount > 0)
			{
				var compromisedTarget = waitingAt.FallbackBuilding ?? previousRepairTarget;
				var alreadyHoldingForTarget = compromisedTarget != null && previousRepairTarget == compromisedTarget &&
					previousTargetDanger > 0 && previousTargetDanger < float.MaxValue;
				var currentlySafe = !RepairDestinationIsUnsafe(owner,
					RepairDestinationDanger(a.CenterPosition, threats));
				if (!alreadyHoldingForTarget || (a.IsIdle && !currentlySafe))
				{
					var holding = FindSafeRepairHoldingLocation(owner, a, threats);
					QueueRepairHoldingRoute(owner, a, holding?.Route, holding?.Destination ?? a.Location);

					if (owner.SquadManager.Info.AirTargetDebugLogging)
						Log.Write("debug", "Air repair [{0}] {1}#{2}: {3}/{4} HP, {5} repair option(s) AA-covered; " +
							"holding safely at {6} while waiting for {7}#{8}.",
							owner.AirProfile, a.Info.Name, a.ActorID, health.HP, health.MaxHP,
							waitingAt.CandidateCount,
							holding?.Shelter == null ? "cell " + (holding?.Destination.ToString() ?? a.Location.ToString()) :
								holding.Shelter.Info.Name + "#" + holding.Shelter.ActorID,
							compromisedTarget?.Info.Name ?? "facility", compromisedTarget?.ActorID ?? 0);
				}

				owner.MarkAirRepairWaiting(a, compromisedTarget);
				return true;
			}

			owner.AirUnitsRepairing.Remove(a.ActorID);
			owner.AirRepairTargets.Remove(a.ActorID);
			owner.AirRepairWaiting.Remove(a.ActorID);
			owner.AirRepairWaitingSince.Remove(a.ActorID);
			if (owner.Units.Count == 1)
				owner.JoinAirFormation(a);

			var rejectedByAa = Math.Max(recovery.RejectedByAa, waitingAt.RejectedByAa);
			if (owner.SquadManager.Info.AirTargetDebugLogging && owner.AirRepairUnavailable.Add(a.ActorID))
				Log.Write("debug", "Air repair [{0}] {1}#{2}: {3}/{4} HP, no safe recovery destination ({5} AA-covered); staying with squad.",
					owner.AirProfile, a.Info.Name, a.ActorID, health.HP, health.MaxHP, rejectedByAa);

			return false;
		}

		static List<(Actor Actor, float Weight, int RangeCells)> LiveRepairThreats(Squad owner)
		{
			var threats = new List<(Actor Actor, float Weight, int RangeCells)>();
			if (owner.Type == SquadType.Stealth)
			{
				var definition = owner.StealthDefinition;
				if (definition == null)
					return threats;

				foreach (var threat in StealthThreats(owner))
				{
					if (threat.DetectorRange > 0)
						threats.Add((threat.Actor, StealthAISpecialistPolicy.HardDetectorRouteInfluence,
							StealthAISpecialistPolicy.BufferedRange(threat.DetectorRange,
								definition.DetectorRangeBufferCells)));
					if (threat.WeaponRange > 0 && !threat.Actor.GetEnabledTargetTypes()
						.Overlaps(definition.IgnoredHarassmentWeaponThreatTypes))
						threats.Add((threat.Actor, (float)threat.CanonicalThreat,
							StealthAISpecialistPolicy.BufferedRange(threat.WeaponRange,
								definition.ThreatRangeBufferCells)));
				}

				return threats;
			}

			var info = owner.SquadManager.Info;
			foreach (var enemy in owner.World.Actors)
			{
				if (!owner.SquadManager.IsPreferredEnemyUnit(enemy))
					continue;

				var profile = AntiAirProfile(enemy);
				var weight = StoppingThreatWeight(owner, enemy, profile.Weight);
				if (weight <= 0)
					continue;

				var range = Math.Max(1, (int)Math.Ceiling(profile.RangeCells * info.AirThreatRangeBuffer));
				var mobile = enemy.Info.TraitInfoOrDefault<MobileInfo>();
				var movementBuffer = mobile == null ? 0 :
					StealthAIThreatGeometry.MobileThreatBufferCells(mobile.Speed, info.AirInfluenceCacheInterval);
				threats.Add((enemy, weight, range + movementBuffer));
			}

			return threats;
		}

		static float RepairDestinationDanger(WPos destination,
			IEnumerable<(Actor Actor, float Weight, int RangeCells)> threats)
		{
			var danger = 0f;
			foreach (var threat in threats)
				if ((threat.Actor.CenterPosition - destination).Length / 1024f <= threat.RangeCells)
					danger += threat.Weight;

			return danger;
		}

		static bool RepairDestinationIsUnsafe(Squad owner, float danger)
		{
			return owner.Type == SquadType.Stealth ?
				StealthAISpecialistPolicy.IsHardRouteDanger(danger) : danger > 0;
		}

		static WDist PassiveRepairRange(Squad owner, Actor building)
		{
			if (building == null || building.Owner == owner.Bot.Player ||
				!building.Owner.IsAlliedWith(owner.Bot.Player) ||
				!owner.SquadManager.Info.AirPassiveRepairActors.Contains(building.Info.Name))
				return WDist.Zero;

			var range = WDist.Zero;
			foreach (var aura in building.TraitsImplementing<GrantConditionInRange>())
				if (!aura.IsTraitDisabled && aura.Info.Granter &&
					aura.Info.ValidRelationships.HasRelationship(PlayerRelationship.Ally) && aura.Info.Range > range)
					range = aura.Info.Range;

			return range;
		}

		static AirRepairPlan FindSafestRepairBuilding(
			Squad owner, Actor aircraft, RepairableInfo repairable,
			List<(Actor Actor, float Weight, int RangeCells)> threats, bool requireAvailable)
		{
			var candidates = new List<(Actor Building, bool RepairAtEnd)>();
			foreach (var b in owner.World.ActorsHavingTrait<RepairsUnits>())
			{
				if (!repairable.RepairActors.Contains(b.Info.Name))
					continue;

				var owned = b.Owner == owner.Bot.Player;
				var alliedPassive = !owned && PassiveRepairRange(owner, b) > WDist.Zero;
				if (!owned && !alliedPassive)
					continue;

				if (owned && requireAvailable)
				{
					var airSquads = owner.SquadManager.Squads.Where(s =>
						s.Type == SquadType.Air || s.Type == SquadType.Stealth).ToList();
					var assignments = airSquads.SelectMany(s => s.AirRepairTargets)
						.ToDictionary(a => a.Key, a => a.Value);
					var repairing = new HashSet<uint>(airSquads.SelectMany(s => s.AirUnitsRepairing));
					var waiting = new HashSet<uint>(airSquads.SelectMany(s => s.AirRepairWaiting));
					var assignedToOther = StealthAIThreatGeometry.HasOtherRepairAssignment(assignments,
						repairing, waiting, aircraft.ActorID, b.ActorID);
					if (assignedToOther || !Reservable.IsAvailableFor(b, aircraft))
						continue;
				}

				candidates.Add((b, owned && requireAvailable));
			}

			if (candidates.Count == 0)
				return new AirRepairPlan();

			var info = owner.SquadManager.Info;
			var map = owner.World.Map;
			var coarseSize = owner.Type == SquadType.Stealth ? StealthCoarseSize(owner) : info.AirInfluenceCellSize;
			var danger = BuildRepairDangerGrid(owner, threats, out var width, out var height);
			var aircraftSpeed = aircraft.Info.TraitInfoOrDefault<AircraftInfo>()?.Speed ??
				aircraft.Info.TraitInfoOrDefault<MobileInfo>()?.Speed ?? info.AirTargetReferenceSpeed;
			var start = map.CellContaining(aircraft.CenterPosition);
			var startX = Math.Clamp(start.X / coarseSize, 0, width - 1);
			var startY = Math.Clamp(start.Y / coarseSize, 0, height - 1);
			Actor best = null;
			List<CPos> bestRoute = null;
			var bestRepairAtEnd = false;
			var bestCost = float.MaxValue;
			var rejectedByAa = 0;
			foreach (var candidate in candidates.OrderBy(a => a.Building.ActorID))
			{
				var goalX = Math.Clamp(candidate.Building.Location.X / coarseSize, 0, width - 1);
				var goalY = Math.Clamp(candidate.Building.Location.Y / coarseSize, 0, height - 1);
				if (RepairDestinationIsUnsafe(owner,
					RepairDestinationDanger(candidate.Building.CenterPosition, threats)) ||
					RepairDestinationIsUnsafe(owner, danger[goalY * width + goalX]))
				{
					rejectedByAa++;
					continue;
				}

				var route = FindAirRoute(owner,
					danger, width, height, startX, startY, goalX, goalY, info.AirRouteThreatPenalty);
				if (route == null)
					continue;

				var exposure = route.Sum(p => danger[p.Y * width + p.X]) * info.AirRouteThreatPenalty;
				var travel = route.Count * coarseSize * info.AirTargetDistancePenalty *
					info.AirTargetReferenceSpeed / (float)Math.Max(1, aircraftSpeed);
				var cost = exposure + travel;
				if (cost >= bestCost)
					continue;

				best = candidate.Building;
				bestRepairAtEnd = candidate.RepairAtEnd;
				bestCost = cost;
				bestRoute = ThreatAwareRoutePlanner.SmoothRoute(
					danger, width, height, startX, startY, route)
					.Select(p => map.Clamp(new CPos(
						p.X * coarseSize + coarseSize / 2, p.Y * coarseSize + coarseSize / 2))).ToList();
			}

			return new AirRepairPlan
			{
				Building = best,
				FallbackBuilding = candidates
					.OrderBy(c => RepairDestinationDanger(c.Building.CenterPosition, threats))
					.ThenBy(c => (c.Building.CenterPosition - aircraft.CenterPosition).LengthSquared)
					.ThenBy(c => c.Building.ActorID)
					.First().Building,
				Route = bestRoute,
				RepairAtEnd = bestRepairAtEnd,
				CandidateCount = candidates.Count,
				RejectedByAa = rejectedByAa,
			};
		}

		static bool IsOldestReadyRepairWaiter(Squad owner, Actor aircraft, Actor facility)
		{
			var airSquads = owner.SquadManager.Squads.Where(s =>
				s.Type == SquadType.Air || s.Type == SquadType.Stealth).ToList();
			var waitingSince = airSquads.SelectMany(s => s.AirRepairWaitingSince)
				.ToDictionary(a => a.Key, a => a.Value);
			var ready = airSquads.SelectMany(s => s.Units)
				.Where(unit => waitingSince.ContainsKey(unit.ActorID) && IsReadyRepairWaiter(owner, unit, facility) &&
					unit.Info.TraitInfoOrDefault<RepairableInfo>()?.RepairActors.Contains(facility.Info.Name) == true)
				.Select(unit => unit.ActorID);
			return StealthAIThreatGeometry.IsOldestReadyRepairWaiter(waitingSince, ready, aircraft.ActorID);
		}

		static bool IsReadyRepairWaiter(Squad owner, Actor aircraft, Actor facility)
		{
			var readyRange = WDist.FromCells((owner.Type == SquadType.Stealth ?
				StealthCoarseSize(owner) : owner.SquadManager.Info.AirInfluenceCellSize) * 2);
			return (aircraft.CenterPosition - facility.CenterPosition).HorizontalLength <= readyRange.Length;
		}

		static float[] BuildRepairDangerGrid(Squad owner,
			IEnumerable<(Actor Actor, float Weight, int RangeCells)> threats, out int width, out int height)
		{
			if (owner.Type == SquadType.Stealth)
			{
				var representative = AirDecisionUnits(owner).OrderBy(a => a.ActorID).FirstOrDefault();
				var cache = representative == null ? null : StealthInfluence(owner, representative);
				if (cache != null)
				{
					width = cache.Width;
					height = cache.Height;
					return cache.Danger;
				}
			}

			var info = owner.SquadManager.Info;
			var map = owner.World.Map;
			var coarseSize = owner.Type == SquadType.Stealth ? StealthCoarseSize(owner) : info.AirInfluenceCellSize;
			width = (map.MapSize.X + coarseSize - 1) / coarseSize;
			height = (map.MapSize.Y + coarseSize - 1) / coarseSize;
			var danger = new float[width * height];
			foreach (var threat in threats)
			{
				var minX = Math.Max(0, (threat.Actor.Location.X - threat.RangeCells) / coarseSize);
				var maxX = Math.Min(width - 1, (threat.Actor.Location.X + threat.RangeCells) / coarseSize);
				var minY = Math.Max(0, (threat.Actor.Location.Y - threat.RangeCells) / coarseSize);
				var maxY = Math.Min(height - 1, (threat.Actor.Location.Y + threat.RangeCells) / coarseSize);
				for (var y = minY; y <= maxY; y++)
					for (var x = minX; x <= maxX; x++)
					{
						var cell = new CPos(x * coarseSize + coarseSize / 2, y * coarseSize + coarseSize / 2);
						var distance = (map.CenterOfCell(map.Clamp(cell)) - threat.Actor.CenterPosition).Length / 1024;
						if (distance <= threat.RangeCells)
							danger[y * width + x] += threat.Weight;
					}
			}

			return danger;
		}

		static AirRepairHoldingPlan FindSafeRepairHoldingLocation(Squad owner, Actor aircraft,
			List<(Actor Actor, float Weight, int RangeCells)> threats)
		{
			var info = owner.SquadManager.Info;
			var map = owner.World.Map;
			var coarseSize = owner.Type == SquadType.Stealth ? StealthCoarseSize(owner) : info.AirInfluenceCellSize;
			var danger = BuildRepairDangerGrid(owner, threats, out var width, out var height);
			var start = map.CellContaining(aircraft.CenterPosition);
			var startX = Math.Clamp(start.X / coarseSize, 0, width - 1);
			var startY = Math.Clamp(start.Y / coarseSize, 0, height - 1);
			var aircraftSpeed = aircraft.Info.TraitInfoOrDefault<AircraftInfo>()?.Speed ??
				aircraft.Info.TraitInfoOrDefault<MobileInfo>()?.Speed ?? info.AirTargetReferenceSpeed;
			Actor bestShelter = null;
			List<CPos> bestRoute = null;
			var bestCost = float.MaxValue;
			foreach (var shelter in owner.World.ActorsHavingTrait<Building>()
				.Where(b => b.Owner == owner.Bot.Player).OrderBy(b => b.ActorID))
			{
				var goalX = Math.Clamp(shelter.Location.X / coarseSize, 0, width - 1);
				var goalY = Math.Clamp(shelter.Location.Y / coarseSize, 0, height - 1);
				if (RepairDestinationIsUnsafe(owner,
					RepairDestinationDanger(shelter.CenterPosition, threats)) ||
					RepairDestinationIsUnsafe(owner, danger[goalY * width + goalX]))
					continue;

				var route = FindAirRoute(owner,
					danger, width, height, startX, startY, goalX, goalY, info.AirRouteThreatPenalty);
				if (route == null)
					continue;

				var exposure = route.Sum(p => danger[p.Y * width + p.X]) * info.AirRouteThreatPenalty;
				var travel = route.Count * coarseSize * info.AirTargetDistancePenalty *
					info.AirTargetReferenceSpeed / (float)Math.Max(1, aircraftSpeed);
				var cost = exposure + travel;
				if (cost >= bestCost)
					continue;

				bestShelter = shelter;
				bestRoute = route;
				bestCost = cost;
			}

			if (bestShelter == null)
			{
				bestRoute = ThreatAwareRoutePlanner.FindNearestSafeRoute(
					danger, width, height, startX, startY, info.AirRouteThreatPenalty);
				if (bestRoute == null)
					return null;
			}

			var smoothedRoute = ThreatAwareRoutePlanner.SmoothRoute(
				danger, width, height, startX, startY, bestRoute)
				.Select(p => map.Clamp(new CPos(
					p.X * coarseSize + coarseSize / 2, p.Y * coarseSize + coarseSize / 2))).ToList();
			var destination = bestShelter?.Location ??
				(smoothedRoute.Count > 0 ? smoothedRoute[smoothedRoute.Count - 1] : aircraft.Location);
			return new AirRepairHoldingPlan
			{
				Shelter = bestShelter,
				Destination = destination,
				Route = smoothedRoute,
			};
		}

		static void QueueRecoveryRoute(
			Squad owner, Actor aircraft, List<CPos> route, Actor destination, bool repairAtEnd)
		{
			var queued = false;
			if (route != null)
				foreach (var waypoint in route)
				{
					owner.Bot.QueueOrder(new Order(
						"Move", aircraft, Target.FromCell(owner.World, waypoint), queued));
					queued = true;
				}

			if (repairAtEnd)
				owner.Bot.QueueOrder(new Order(
					"Repair", aircraft, Target.FromActor(destination), queued));
			else if (!queued)
				owner.Bot.QueueOrder(new Order(
					"Move", aircraft, Target.FromCell(owner.World, destination.Location), false));
		}

		static void QueueRepairHoldingRoute(Squad owner, Actor aircraft, List<CPos> route, CPos destination)
		{
			var queued = false;
			if (route != null)
				foreach (var waypoint in route)
				{
					owner.Bot.QueueOrder(new Order(
						"Move", aircraft, Target.FromCell(owner.World, waypoint), queued));
					queued = true;
				}

			if (!queued)
				owner.Bot.QueueOrder(new Order(
					"Move", aircraft, Target.FromCell(owner.World, destination), false));
		}

		/// <summary>
		/// Sends the squad home: rearming whoever needs it, and moving everyone else to the one of our
		/// own buildings that sits furthest from the anti-air the squad remembers. Falls back to the
		/// stock random building when nothing is remembered. Only reached when AirEvadeDistance is zero.
		/// </summary>
		protected static void Retreat(Squad owner)
		{
			var destination = SafeRetreatLocation(owner);

			foreach (var a in owner.Units)
			{
				if (SendHomeToResupply(owner, a) || SendHomeToRepair(owner, a))
					continue;

				if (owner.AirReinforcements.Contains(a.ActorID))
					QueueSafeMoveForReinforcement(owner, a, destination);
				else
					owner.Bot.QueueOrder(new Order("Move", a, Target.FromCell(owner.World, destination), false));
			}
		}

		static CPos SafeRetreatLocation(Squad owner)
		{
			var threats = owner.AirThreatPositions;
			if (threats.Count == 0)
				return RandomBuildingLocation(owner);

			// PERF: no world queries beyond the trait lookup the stock RandomBuildingLocation already did,
			// and the inner comparison is bounded by AirThreatMemorySize.
			var found = false;
			var best = owner.SquadManager.GetRandomBaseCenter();
			var bestDistance = long.MinValue;

			foreach (var b in owner.World.ActorsHavingTrait<Building>())
			{
				if (b.Owner != owner.Bot.Player)
					continue;

				var distance = StealthAIThreatGeometry.NearestThreatDistanceSquared(b.CenterPosition, threats);
				if (!found || distance > bestDistance)
				{
					found = true;
					bestDistance = distance;
					best = b.Location;
				}
			}

			return best;
		}

		protected static bool NearToPosSafely(Squad owner, WPos loc)
		{
			return NearToPosSafely(owner, loc, out _);
		}

		protected static bool NearToPosSafely(Squad owner, WPos loc, out Actor detectedEnemyTarget)
		{
			detectedEnemyTarget = null;
			var dangerRadius = owner.SquadManager.Info.DangerScanRadius;
			var unitsAroundPos = owner.World.FindActorsInCircle(loc, WDist.FromCells(dangerRadius))
				.Where(owner.SquadManager.IsPreferredEnemyUnit).ToList();

			if (!unitsAroundPos.Any())
				return true;

			if (CountAntiAirUnits(owner, unitsAroundPos) * owner.SquadManager.Info.AirThreatFleeMultiplier <
				AirDecisionUnits(owner).Count)
			{
				detectedEnemyTarget = unitsAroundPos.Random(owner.Random);
				return true;
			}

			return false;
		}

		// Checks the number of anti air enemies around units
		protected virtual bool ShouldFlee(Squad owner)
		{
			// BEGIN CNC96A GROUND EXTENSION
			// Specialist ground squads retain the Air decision loop, but never enter its retreat state.
			if (owner.Type == SquadType.Stealth)
				return false;
			// END CNC96A GROUND EXTENSION

			return ShouldFlee(owner, enemies => CountAntiAirUnits(owner, enemies) * MissileUnitMultiplier >
				AirDecisionUnits(owner).Count);
		}
	}

	class StealthAIAttackState : StealthAIStateBase, IState
	{
		public void Activate(Squad owner)
		{
			// Target selection already owns a bounded cached route. Submit it at the state boundary instead
			// of spending one full attack interval waiting for the first Attack tick.
			if (StealthAISpecialistPolicy.ShouldDispatchOwnedMissionImmediately(
				owner.StealthProfile == "stealth-tank", owner.IsTargetValid,
				owner.AirRoute.Count, owner.AirRouteQueued))
				Tick(owner);
		}

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			if (owner.AirEscapingLocalAa)
			{
				if (owner.Type != SquadType.Stealth || AdvanceStealthEscape(owner))
					return;
			}

			var info = owner.SquadManager.Info;
			foreach (var unit in owner.Units)
				SendHomeToRepair(owner, unit);

			PromoteArrivedAirReinforcements(owner);
			RoutePendingStealthReinforcements(owner);

			var decisionUnits = AirDecisionUnits(owner);
			var formationUnits = owner.AirFormationUnits();
			var stealthCache = owner.Type == SquadType.Stealth && formationUnits.Count > 0 ?
				StealthInfluence(owner, formationUnits[0]) : null;
			if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Air state [{0}] attack tick: units={1} formation={2} reinforcements={3} target-valid={4} route-queued={5}.",
					owner.AirProfile, owner.Units.Count, formationUnits.Count, owner.AirReinforcements.Count,
					owner.IsTargetValid, owner.AirRouteQueued);

			if (owner.Type == SquadType.Stealth && owner.IsTargetValid &&
				CancelUnsafeLivePlannedDecloakContinuation(owner, formationUnits))
			{
				if (!BeginStealthSafetyReposition(owner))
				{
					ClearAaTargetContext(owner);
					owner.TargetActor = null;
					owner.FuzzyStateMachine.ChangeState(owner, new StealthAIIdleState(), true);
				}
				return;
			}

			var hasArmedUnit = decisionUnits.Any(a =>
				HasAmmo(a.TraitsImplementing<AmmoPool>()));
			HoldUnsafeClaimedStealthApproach(owner, formationUnits);
			var anyUnitBusy = decisionUnits.Any(a =>
				(BusyAttack(a) || !a.IsIdle));
			var routeTraveling = owner.AirRouteQueued && formationUnits.Any(a => !a.IsIdle &&
				(owner.StealthProfile == "stealth-tank" || !BusyAttack(a)));
			if (owner.StealthProfile == "stealth-tank" && routeTraveling)
			{
				var centerCell = owner.World.Map.CellContaining(owner.AirFormationCenter);
				if (owner.StealthRouteLastCenterCell == null || owner.StealthRouteLastCenterCell.Value != centerCell)
				{
					owner.StealthRouteLastCenterCell = centerCell;
					owner.StealthRouteLastCenterProgressTick = owner.World.WorldTick;
					owner.AirTargetLastProgressTick = owner.World.WorldTick;
				}
			}
			else if (owner.StealthProfile == "stealth-tank" && !owner.AirRouteQueued)
				owner.StealthRouteLastCenterCell = null;
			var ticksSinceProgress = owner.World.WorldTick - owner.AirTargetLastProgressTick;
			if (owner.Type == SquadType.Stealth && owner.StealthClearMode == StealthClearMode.Kite &&
				KiteParticipantTookDamage(owner))
			{
				if (info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Stealth kite [{0}] participant damage abort at tick={1}; local safety reposition.",
						owner.StealthProfile, owner.World.WorldTick);
				if (!BeginStealthSafetyReposition(owner))
				{
					ClearAaTargetContext(owner);
					owner.TargetActor = null;
					owner.FuzzyStateMachine.ChangeState(owner, new StealthAIIdleState(), true);
				}
				return;
			}

			if (owner.Type == SquadType.Stealth && stealthCache != null &&
				ContinueStealthClear(owner, stealthCache, formationUnits))
				return;
			var clearingStealthPackage = owner.Type == SquadType.Stealth &&
				owner.StealthClearMode != StealthClearMode.None;

			if (!clearingStealthPackage && owner.IsTargetValid &&
				owner.World.WorldTick >= owner.AirNextTargetReviewTick)
			{
				var targetReviewInterval = owner.StealthProfile == "stealth-tank" ?
					StealthAISpecialistPolicy.StrategicTargetReviewIntervalTicks(
						owner.World.Timestep, info.AirInfluenceCacheInterval) : info.AirInfluenceCacheInterval;
				owner.AirNextTargetReviewTick = owner.World.WorldTick + targetReviewInterval;
				if (owner.TargetActor.Info.HasTraitInfo<BuildingInfo>() && hasArmedUnit)
				{
					var incumbent = owner.TargetActor;
					var previousScore = owner.AirTargetScore;
					var challenger = FindBestAirTarget(owner, incumbent, out var freshIncumbent,
						requiredAaProtectedCell: owner.AirTargetClearsAa ? owner.AirAaClearProtectedCell : null);
					var recalculatedIncumbent = challenger != null && challenger.Actor == incumbent ?
						challenger : freshIncumbent;
					var repeatedMovingMtnkKite = owner.Type == SquadType.Stealth &&
						recalculatedIncumbent != null && challenger != null &&
						challenger.Actor != incumbent &&
						incumbent.Info.HasTraitInfo<LineBuildNodeInfo>() &&
						challenger.StealthMode == StealthClearMode.Kite &&
						challenger.Actor.Info.Name.Equals("mtnk", StringComparison.OrdinalIgnoreCase);
					if (repeatedMovingMtnkKite)
					{
						if (owner.StealthKiteSupersessionActorId == challenger.Actor.ActorID)
							owner.StealthKiteSupersessionConfirmations =
								Math.Min(2, owner.StealthKiteSupersessionConfirmations + 1);
						else
						{
							owner.StealthKiteSupersessionActorId = challenger.Actor.ActorID;
							owner.StealthKiteSupersessionConfirmations = 1;
						}
					}
					else
					{
						owner.StealthKiteSupersessionActorId = 0;
						owner.StealthKiteSupersessionConfirmations = 0;
					}

					var confirmedMovingMtnkKite =
						StealthAISpecialistPolicy.ShouldReplaceLowValueWallWithConfirmedMovingMtnkKite(
							owner.StealthProfile == "stealth-tank",
							incumbent.Info.HasTraitInfo<LineBuildNodeInfo>(),
							repeatedMovingMtnkKite, challenger?.StealthMode ?? StealthClearMode.None,
							owner.StealthKiteSupersessionConfirmations,
							challenger?.ServiceMilliseconds ?? long.MaxValue,
							owner.StealthDefinition.MaximumUndefendedTargetTravelSeconds);
					var formationRepresentative = owner.AirFormationUnits(bootstrapIfEmpty: true).FirstOrDefault();
					var approvedArrivedMass = owner.Type == SquadType.Stealth &&
						challenger?.StealthMode == StealthClearMode.Mass &&
						challenger.StealthPackage?.Count > 0 && challenger.StealthClearCenterCell != null &&
						formationRepresentative != null &&
						Math.Abs(challenger.StealthClearCenterCell.Value.X -
							formationRepresentative.Location.X / StealthCoarseSize(owner)) <= 1 &&
						Math.Abs(challenger.StealthClearCenterCell.Value.Y -
							formationRepresentative.Location.Y / StealthCoarseSize(owner)) <= 1;
					if (repeatedMovingMtnkKite && info.AirTargetDebugLogging)
						Log.Write("debug", "Stealth Kite supersession [{0}] confirmation: tick={1} " +
							"incumbent={2}#{3} challenger={4}#{5} confirmations={6}/2 " +
							"service-ms={7} action={8} scope=cached-local world-scans=0.",
							owner.StealthProfile, owner.World.WorldTick, incumbent.Info.Name,
							incumbent.ActorID, challenger.Actor.Info.Name, challenger.Actor.ActorID,
							owner.StealthKiteSupersessionConfirmations,
							challenger.ServiceMilliseconds,
							confirmedMovingMtnkKite ? "switch" : "retain");
					// A bounded ground mission owns its static victim until it becomes unsafe,
					// invalid, or stalls. Air can exploit a higher-value challenger immediately;
					// STNKs must not restart a safe route every influence refresh before firing.
					var switchTarget = recalculatedIncumbent == null || confirmedMovingMtnkKite ||
						approvedArrivedMass ||
						(owner.Type != SquadType.Stealth && challenger != null && challenger.Actor != incumbent &&
						StealthAIThreatGeometry.ShouldSwitchTarget(recalculatedIncumbent.IsUndefended,
							recalculatedIncumbent.Score, true, challenger.IsUndefended, challenger.Score,
							StealthSwitchImprovement(owner)));
					if (switchTarget)
					{
						if (info.AirTargetDebugLogging)
							Log.Write("debug", "Air target [{0}] switching building at tick={1}: {2}#{3} score={4} to {5} score={6}: improvement threshold={7}%.",
								owner.AirProfile, owner.World.WorldTick,
								incumbent.Info.Name, incumbent.ActorID, previousScore,
								challenger == null ? "none" : challenger.Actor.Info.Name + "#" + challenger.Actor.ActorID,
								challenger?.Score ?? int.MinValue,
								StealthSwitchImprovement(owner));

						if (challenger != null)
							ApplyAirTargetPlan(owner, challenger);
						else
						{
							owner.TargetActor = null;
							ClearAaTargetContext(owner);
							owner.AirTargetStrategicCell = null;
							owner.AirRoute.Clear();
							owner.AirRouteQueued = false;
						}
					}
					else
					{
						owner.AirTargetScore = recalculatedIncumbent.Score;
						owner.AirTargetIsUndefended = recalculatedIncumbent.IsUndefended;
						owner.AirTargetClearsAa = recalculatedIncumbent.ClearsAa;
						owner.AirAaClearProtectedCell = recalculatedIncumbent.ClearsAa ?
							recalculatedIncumbent.AaProtectedCell : null;
						if (info.AirTargetDebugLogging)
							Log.Write("debug", "Air target [{0}] retaining building at tick={1}: {2}#{3}: challenger={4} old-score={5} recalculated-score={6} challenger-score={7} clears-aa={8} protected-cell={9}.",
								owner.AirProfile, owner.World.WorldTick, incumbent.Info.Name, incumbent.ActorID,
								challenger == null ? "none" : challenger.Actor.Info.Name + "#" + challenger.Actor.ActorID,
								previousScore, recalculatedIncumbent.Score,
								challenger?.Score ?? int.MinValue, recalculatedIncumbent.ClearsAa,
								recalculatedIncumbent.AaProtectedCell?.ToString() ?? "none");
					}
				}
			}

			if (!clearingStealthPackage && owner.IsTargetValid)
			{
				var currentCell = new CPos(
					owner.TargetActor.Location.X / StealthCoarseSize(owner),
					owner.TargetActor.Location.Y / StealthCoarseSize(owner));
				if (owner.AirTargetStrategicCell == null)
				{
					owner.AirTargetStrategicCell = currentCell;
					owner.AirTargetLastProgressTick = owner.World.WorldTick;
					owner.AirTargetLastDistanceCells =
						(owner.TargetActor.CenterPosition - owner.AirFormationCenter).Length / 1024;
					owner.AirTargetLastHP = owner.TargetActor.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue;
				}
				else if (currentCell != owner.AirTargetStrategicCell.Value &&
					(owner.StealthProfile != "stealth-tank" ||
					owner.World.WorldTick >= owner.AirNextTargetReviewTick))
				{
					if (owner.StealthProfile == "stealth-tank")
						owner.AirNextTargetReviewTick = owner.World.WorldTick +
							StealthAISpecialistPolicy.StrategicTargetReviewIntervalTicks(
								owner.World.Timestep, info.AirInfluenceCacheInterval);
					var previousCell = owner.AirTargetStrategicCell.Value;
					var incumbent = owner.TargetActor;
					var oldScore = owner.AirTargetScore;
					var best = FindBestAirTarget(owner, incumbent, out var freshIncumbent,
						requiredAaProtectedCell: owner.AirTargetClearsAa ? owner.AirAaClearProtectedCell : null);
					var recalculatedIncumbent = best != null && best.Actor == incumbent ? best : freshIncumbent;
					AirTargetPlan selected;
					string decision;
					if (recalculatedIncumbent == null)
					{
						selected = best;
						decision = best == null ? "abandoned" : "switched-invalid-incumbent";
					}
					else if (best == null || best.Actor == incumbent || owner.Type == SquadType.Stealth ||
						(owner.Type != SquadType.Stealth &&
						!StealthAIThreatGeometry.ShouldSwitchTarget(recalculatedIncumbent.IsUndefended,
							recalculatedIncumbent.Score, true, best.IsUndefended, best.Score,
							StealthSwitchImprovement(owner))))
					{
						selected = recalculatedIncumbent;
						decision = "retained";
					}
					else
					{
						selected = best;
						decision = "switched";
					}

					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air target [{0}] {1}#{2} moved strategic cell {3}->{4}; fresh reassessment={5} old-score={6} incumbent-score={7} best={8} best-score={9} progress-age={10}.",
							owner.AirProfile, incumbent.Info.Name, incumbent.ActorID, previousCell, currentCell,
							decision, oldScore, recalculatedIncumbent?.Score ?? int.MinValue,
							best == null ? "none" : best.Actor.Info.Name + "#" + best.Actor.ActorID,
							best?.Score ?? int.MinValue, ticksSinceProgress);

					if (selected != null)
						ApplyAirTargetPlan(owner, selected);
					else
					{
						owner.TargetActor = null;
						ClearAaTargetContext(owner);
						owner.AirTargetStrategicCell = null;
						owner.AirRoute.Clear();
						owner.AirRouteQueued = false;
					}
				}
				else
				{
					var distanceCells = (owner.TargetActor.CenterPosition - owner.AirFormationCenter).Length / 1024;
					var targetHP = owner.TargetActor.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue;
					if (distanceCells + 1 < owner.AirTargetLastDistanceCells || targetHP < owner.AirTargetLastHP)
					{
						owner.AirTargetLastProgressTick = owner.World.WorldTick;
						owner.AirTargetLastDistanceCells = distanceCells;
						owner.AirTargetLastHP = targetHP;
					}
					else if (StealthAIThreatGeometry.ShouldRescanStalledTarget(
						owner.World.WorldTick - owner.AirTargetLastProgressTick, info.AirTargetStallTicks, hasArmedUnit))
					{
						var routeCenterProgressing = owner.StealthProfile == "stealth-tank" &&
							routeTraveling && owner.StealthRouteLastCenterCell != null &&
							owner.World.WorldTick - owner.StealthRouteLastCenterProgressTick < info.AirTargetStallTicks;
						if (routeCenterProgressing)
						{
							if (info.AirTargetDebugLogging)
							{
								var dispositions = owner.Units.OrderBy(unit => unit.ActorID).Select(unit =>
									$"{unit.Info.Name}#{unit.ActorID}@{unit.Location.X},{unit.Location.Y}:" +
									$"idle={unit.IsIdle}:busy={BusyAttack(unit)}:" +
									$"active={formationUnits.Contains(unit)}:" +
									$"reinforcement={owner.AirReinforcements.Contains(unit.ActorID)}:" +
									$"repair={owner.AirUnitsRepairing.Contains(unit.ActorID)}");
								Log.Write("debug", "Air target [{0}] suppressed stalled-target rescan at tick={1} for " +
									"{2}#{3}: shared route traveling and squad center progressed {4} ticks ago; " +
									"state=Attack route-queued={5} target-valid={6} members=[{7}].",
									owner.AirProfile, owner.World.WorldTick, owner.TargetActor.Info.Name,
									owner.TargetActor.ActorID,
									owner.World.WorldTick - owner.StealthRouteLastCenterProgressTick,
									owner.AirRouteQueued, owner.IsTargetValid, dispositions.JoinWith(";"));
							}
							return;
						}

						if (info.AirTargetDebugLogging)
							Log.Write("debug", "Air target [{0}] {1}#{2} stalled for {3} ticks at distance {4}; rescanning (route-queued={5}, route-traveling={6}, any-busy={7}, armed={8}).",
								owner.AirProfile, owner.TargetActor.Info.Name, owner.TargetActor.ActorID,
								owner.World.WorldTick - owner.AirTargetLastProgressTick, distanceCells,
								owner.AirRouteQueued, routeTraveling, anyUnitBusy, hasArmedUnit);

						owner.TargetActor = null;
						ClearAaTargetContext(owner);
						owner.AirTargetStrategicCell = null;
						owner.AirRoute.Clear();
						owner.AirRouteQueued = false;
					}
				}
			}

			if (!owner.IsTargetValid)
			{
				var rememberedTargetCell = owner.AirTargetClearsAa && owner.AirAaClearProtectedCell != null ?
					owner.AirAaClearProtectedCell : owner.AirTargetStrategicCell;
				ClearAaTargetContext(owner);
				owner.AirTargetStrategicCell = null;
				owner.AirRoute.Clear();
				owner.AirRouteQueued = false;
				var nextTarget = rememberedTargetCell == null
					? FindBestAirTarget(owner)
					: FindBestAirTarget(owner, null, out _, rememberedTargetCell);
				if (nextTarget == null && !decisionUnits.Any(a => HasAmmo(a.TraitsImplementing<AmmoPool>())))
					return;

				if (nextTarget == null)
				{
					// BEGIN CNC96A GROUND EXTENSION
					// Ground specialists never inherit Air's flee/retreat transition. When every
					// firing position is unsafe, move one neighboring strategic cell and rescan.
					if (owner.Type == SquadType.Stealth)
					{
						QueueStealthReinforcementsToFormation(owner);
						if (!BeginStealthEnemyApproach(owner))
							owner.FuzzyStateMachine.ChangeState(owner, new StealthAIIdleState(), true);
						return;
					}
					// END CNC96A GROUND EXTENSION

					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air evade [{0}] attack state found no eligible target with armed aircraft; entering flee state.",
							owner.AirProfile);

					owner.FuzzyStateMachine.ChangeState(owner, new StealthAIFleeState(), true);
					return;
				}

				ApplyAirTargetPlan(owner, nextTarget);
			}

			if (owner.Type == SquadType.Stealth && owner.IsTargetValid &&
				owner.StealthClearMode == StealthClearMode.Kite &&
				!RefreshLiveKiteRoute(owner, formationUnits, owner.TargetActor, out _))
			{
				if (!BeginStealthSafetyReposition(owner))
				{
					ClearAaTargetContext(owner);
					owner.TargetActor = null;
					owner.FuzzyStateMachine.ChangeState(owner, new StealthAIIdleState(), true);
				}
				return;
			}

			if (owner.AirProfile == "Generic" && !NearToPosSafely(owner, owner.TargetActor.CenterPosition))
			{
				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air evade [{0}] generic proximity safety rejected target {1}#{2}.",
						owner.AirProfile, owner.TargetActor.Info.Name, owner.TargetActor.ActorID);

				owner.FuzzyStateMachine.ChangeState(owner, new StealthAIFleeState(), true);
				return;
			}

			// Submit the selected shared route once for the current squad. This restores the stable bleed
			// lifecycle: the route is a transient order batch, not a target-lifetime per-aircraft latch.
			if (owner.AirRoute.Count > (owner.Type == SquadType.Stealth ? 0 : 1) && !owner.AirRouteQueued)
			{
				var routeWaypoints = owner.AirRoute.Count;
				var attackOrders = 0;
				var crushLeader = owner.Type == SquadType.Stealth &&
					(owner.StealthClearMode == StealthClearMode.Crush ||
					owner.StealthClearMode == StealthClearMode.CrushBridge) ?
					StealthCrushLeader(owner, formationUnits, owner.TargetActor) : null;
				foreach (var a in formationUnits)
				{
					if (SendHomeToRepair(owner, a))
						continue;

					var massFocus = owner.Type == SquadType.Stealth &&
						owner.StealthClearMode == StealthClearMode.Mass;
					var massRange = massFocus ? GroundWeaponRange(a, owner.TargetActor) : 0;
					var massDistance = massFocus ?
						(a.CenterPosition - owner.TargetActor.CenterPosition).HorizontalLength / 1024f : 0;
					var massAlreadyInRange = massFocus && massRange > 0 && massDistance <= massRange;
					IReadOnlyList<CPos> unitRoute = owner.AirRoute;
					if (massFocus)
						unitRoute = massAlreadyInRange ? (IReadOnlyList<CPos>)Array.Empty<CPos>() :
							MassClearRoute(owner, a, owner.TargetActor);
					if (unitRoute == null)
						continue;

					var validatedFiringCell = owner.Type == SquadType.Stealth && unitRoute.Count > 0 ?
						unitRoute[unitRoute.Count - 1] : (CPos?)null;
					if (owner.Type == SquadType.Stealth)
					{
						if (validatedFiringCell != null)
							owner.StealthValidatedFiringCells[a.ActorID] = validatedFiringCell.Value;
						else
							owner.StealthValidatedFiringCells.Remove(a.ActorID);
					}

					var queued = false;
					foreach (var waypoint in unitRoute)
					{
						owner.Bot.QueueOrder(new Order("Move", a,
							Target.FromCell(owner.World, waypoint), queued));
						queued = true;
					}
					if (massFocus && (info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug))
						Log.Write("debug", "Stealth mass focus dispatch [{0}] tick={1}: unit={2}#{3} " +
							"target={4}#{5} distance={6:0.###} range={7} already-in-range={8} " +
							"live-route-waypoints={9}.", owner.StealthProfile, owner.World.WorldTick,
							a.Info.Name, a.ActorID, owner.TargetActor.Info.Name, owner.TargetActor.ActorID,
							massDistance, massRange, massAlreadyInRange, unitRoute.Count);

					if (crushLeader != null)
					{
						if (a == crushLeader)
						{
							owner.Bot.QueueOrder(new Order(EconomyMammothCrushMove.OrderId, a,
								Target.FromActor(owner.TargetActor), true));
							if (info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
								Log.Write("debug", "Stealth crush trace [{0}] initial-dispatch tick={1} " +
								"leader={2}#{3} leader-cell={4} target={5}#{6} target-cell={7} " +
									"route-waypoints={8} queued=True.", owner.StealthProfile,
									owner.World.WorldTick, a.Info.Name, a.ActorID, a.Location,
									owner.TargetActor.Info.Name, owner.TargetActor.ActorID,
									owner.TargetActor.Location, routeWaypoints);
						}
					}
					else if (CanAttackTarget(a, owner.TargetActor) && owner.Type != SquadType.Stealth)
					{
						owner.Bot.QueueOrder(new Order("Attack", a, Target.FromActor(owner.TargetActor), queued));
						if (owner.StealthPostAttackCell != null)
							owner.Bot.QueueOrder(new Order("Move", a,
								Target.FromCell(owner.World, owner.StealthPostAttackCell.Value), true));
						attackOrders++;
					}
					else if (CanAttackTarget(a, owner.TargetActor) && owner.Type == SquadType.Stealth)
					{
						var withholdAttack = ShouldWithholdLivePlannedDecloakEngagement(
							owner, a, validatedFiringCell, out var coveringThreat, out var vetoReason);
						if (!withholdAttack)
						{
							owner.Bot.QueueOrder(new Order("Attack", a,
								Target.FromActor(owner.TargetActor), queued));
							if (owner.StealthPostAttackCell != null)
								owner.Bot.QueueOrder(new Order("Move", a,
									Target.FromCell(owner.World, owner.StealthPostAttackCell.Value), true));
							attackOrders++;
						}
						else if (info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
							Log.Write("debug", "Stealth live planned-decloak engagement veto [{0}] tick={1}: " +
							"phase=initial-issue unit={2}#{3} unit-cell={4} target={5}#{6} " +
							"validated-firing-cell={7} covering-threat={8} reason={9} " +
							"combat-order=withhold safe-route=continue.", owner.StealthProfile,
							owner.World.WorldTick, a.Info.Name, a.ActorID, a.Location,
							owner.TargetActor.Info.Name, owner.TargetActor.ActorID,
							validatedFiringCell?.ToString() ?? "current-live-approved",
							coveringThreat == null ? "none" : coveringThreat.Info.Name + "#" +
								coveringThreat.ActorID, vetoReason);
					}

					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air route [{0}] {1}#{2}: queued shared safe route ({3} waypoints) to {4}#{5}.",
							owner.AirProfile, a.Info.Name, a.ActorID, owner.AirRoute.Count,
							owner.TargetActor.Info.Name, owner.TargetActor.ActorID);
				}

				if (owner.StealthProfile == "stealth-tank")
					QueueStealthReinforcementsToFormation(owner);
				else
					foreach (var a in owner.Units)
						if (owner.AirReinforcements.Contains(a.ActorID) &&
							!owner.AirUnitsRepairing.Contains(a.ActorID))
							QueueSafeRouteForReinforcement(owner, a, owner.TargetActor);

				owner.AirRouteQueued = formationUnits.Count > 0;
				if (owner.StealthProfile == "stealth-tank" && info.AirTargetDebugLogging)
					Log.Write("debug", "Stealth target service [stealth-tank] destination owned: tick={0} " +
						"squad={1}#{2} target={3}#{4} target-cell={5} route-waypoints={6} " +
						"formation={7} reinforcements={8} queued-attacks={9}.", owner.World.WorldTick,
						owner.StealthSquadDefinition, owner.StealthSquadIndex, owner.TargetActor.Info.Name,
						owner.TargetActor.ActorID, owner.TargetActor.Location, routeWaypoints,
						formationUnits.Count, owner.AirReinforcements.Count, attackOrders);
				owner.AirRoute.Clear();
				return;
			}

			// Once no aircraft is still traveling the shared route, release the transient flag. Busy attack
			// orders do not keep it latched, and repaired/new idle aircraft are replanned below.
			if (owner.AirRouteQueued && !routeTraveling)
			{
				owner.AirRouteQueued = false;
				if (owner.StealthProfile == "stealth-tank")
					owner.AirNextTargetReviewTick = Math.Min(
						owner.AirNextTargetReviewTick, owner.World.WorldTick);
				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air route [{0}] shared route completed; idle joiners will replan " +
						"from their current position; strategic-event-review={1}.",
						owner.AirProfile, owner.StealthProfile == "stealth-tank" ? "arrival" : "ordinary");
			}

			// Lazily computed: only needed if a self-reloading aircraft actually turns out to be dry,
			// which is the uncommon case, and shared across every unit that needs it this tick rather
			// than recomputed (and drawing fresh jitter from World.LocalRandom) per unit.
			CPos? disengageDestination = null;
			if (owner.StealthProfile == "stealth-tank")
				QueueStealthReinforcementsToFormation(owner);
			var activeCrushLeader = owner.Type == SquadType.Stealth && owner.IsTargetValid &&
				(owner.StealthClearMode == StealthClearMode.Crush ||
				owner.StealthClearMode == StealthClearMode.CrushBridge) ?
				StealthCrushLeader(owner, formationUnits, owner.TargetActor) : null;

			foreach (var a in owner.Units)
			{
				if (SendHomeToRepair(owner, a))
					continue;

				if (owner.AirReinforcements.Contains(a.ActorID))
				{
					if (owner.StealthProfile == "stealth-tank")
						continue;

					var routedToCurrentTarget = owner.AirReinforcementTargets.TryGetValue(a.ActorID, out var targetId) &&
						targetId == owner.TargetActor.ActorID;
					if (!routedToCurrentTarget || a.IsIdle)
						QueueSafeRouteForReinforcement(owner, a, owner.TargetActor);

					continue;
				}

				if (owner.AirRouteQueued && !a.IsIdle && !BusyAttack(a))
					continue;

				if (BusyAttack(a))
					continue;

				var ammoPools = a.TraitsImplementing<AmmoPool>().ToArray();
				var reloadsAutomatically = ReloadsAutomatically(ammoPools, a.TraitOrDefault<Rearmable>());
				if (!reloadsAutomatically)
				{
					if (IsRearming(a))
						continue;

					if (!HasAmmo(ammoPools))
					{
						owner.Bot.QueueOrder(new Order("ReturnToBase", a, false));
						continue;
					}
				}
				else if (!HasAmmo(ammoPools))
				{
					// Self-reloading (e.g. Orca/Apache): no dock needed, so don't send it home - just
					// hold a safe position until the magazines refill. Only break off when the local
					// safety scan found AA exposure: abandoning an undefended victim after one volley
					// wastes the aircraft's fast passive reload.
					if (owner.AirLocalThreatWeight <= 0)
					{
						if (info.AirTargetDebugLogging)
							Log.Write("debug", "Air order [{0}] {1}#{2}: empty but locally safe; holding to reload.",
								owner.AirProfile, a.Info.Name, a.ActorID);

						continue;
					}

					if (disengageDestination == null)
						disengageDestination = EvadeDestination(owner);

					owner.Bot.QueueOrder(new Order("Move", a, Target.FromCell(owner.World, disengageDestination.Value), false));
					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air order [{0}] {1}#{2}: empty under AA threat {3:0.##}; disengaging to {4} to reload.",
							owner.AirProfile, a.Info.Name, a.ActorID, owner.AirLocalThreatWeight, disengageDestination.Value);

					continue;
				}

				if (activeCrushLeader != null)
				{
					if (a == activeCrushLeader)
					{
						owner.Bot.QueueOrder(new Order(EconomyMammothCrushMove.OrderId, a,
							Target.FromActor(owner.TargetActor), false));
						if (owner.SquadManager.Info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
							Log.Write("debug", "Stealth crush trace [{0}] live-reissue tick={1} " +
								"leader={2}#{3} leader-cell={4} target={5}#{6} target-cell={7}.",
								owner.StealthProfile, owner.World.WorldTick, a.Info.Name, a.ActorID,
								a.Location, owner.TargetActor.Info.Name, owner.TargetActor.ActorID,
								owner.TargetActor.Location);
					}
				}
				else if (CanAttackTarget(a, owner.TargetActor) &&
					(owner.Type != SquadType.Stealth || owner.StealthClearMode == StealthClearMode.Kite ||
					owner.StealthClearMode == StealthClearMode.Mass || OrdinaryAttackExposureIsSafe(
						owner, stealthCache, a, owner.TargetActor, owner.StealthPostAttackCell)))
				{
					owner.StealthValidatedFiringCells.TryGetValue(a.ActorID, out var firingCell);
					var hasFiringCell = owner.StealthValidatedFiringCells.ContainsKey(a.ActorID);
					if (owner.Type == SquadType.Stealth &&
						ShouldWithholdLivePlannedDecloakEngagement(owner, a,
							hasFiringCell ? firingCell : (CPos?)null,
							out var coveringThreat, out var vetoReason))
					{
						owner.Bot.QueueOrder(new Order("Stop", a, false));
						owner.AirNextTargetReviewTick = Math.Min(
							owner.AirNextTargetReviewTick, owner.World.WorldTick);
						if (info.AirTargetDebugLogging || Game.Settings.Debug.BotDebug)
							Log.Write("debug", "Stealth live planned-decloak engagement veto [{0}] tick={1}: " +
								"phase=immediate-issue unit={2}#{3} unit-cell={4} target={5}#{6} " +
								"validated-firing-cell={7} covering-threat={8} reason={9} " +
								"combat-order=withhold safe-recalculation=immediate.", owner.StealthProfile,
								owner.World.WorldTick, a.Info.Name, a.ActorID, a.Location,
								owner.TargetActor.Info.Name, owner.TargetActor.ActorID,
								hasFiringCell ? firingCell.ToString() : "current-live-approved",
								coveringThreat == null ? "none" : coveringThreat.Info.Name + "#" +
									coveringThreat.ActorID, vetoReason);
						if (!BeginStealthSafetyReposition(owner))
						{
							ClearAaTargetContext(owner);
							owner.TargetActor = null;
							owner.FuzzyStateMachine.ChangeState(owner, new StealthAIIdleState(), true);
						}
						return;
					}

					owner.Bot.QueueOrder(new Order("Attack", a, Target.FromActor(owner.TargetActor), false));
					if (owner.SquadManager.Info.AirTargetDebugLogging)
						Log.Write("debug", "Air order [{0}] {1}#{2}: attack {3}#{4}.",
							owner.AirProfile, a.Info.Name, a.ActorID, owner.TargetActor.Info.Name, owner.TargetActor.ActorID);
				}
				else if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Air order [{0}] {1}#{2}: cannot attack selected {3}#{4}.",
						owner.AirProfile, a.Info.Name, a.ActorID, owner.TargetActor.Info.Name, owner.TargetActor.ActorID);
			}
		}

		public void Deactivate(Squad owner) { }
	}
}
