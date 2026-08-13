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
using OpenRA.Mods.Common.Projectiles;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	abstract class AirStateBase : StateBase
	{
		static readonly BitSet<TargetableType> AirTargetTypes = new BitSet<TargetableType>("Air");

		sealed class AirInfluenceCache
		{
			public int Tick;
			public int Width;
			public int Height;
			public float[] Danger;
			public List<(Actor Actor, int Utility, float ConfiguredWeight)> Candidates;
			public List<(Actor Actor, float StoppingWeight, int RangeCells)> Threats;
		}

		sealed class AirRepairPlan
		{
			public Actor Building;
			public Actor FallbackBuilding;
			public List<CPos> Route;
			public bool RepairAtEnd;
			public int CandidateCount;
			public int RejectedByAa;
		}

		sealed class AirRepairHoldingPlan
		{
			public Actor Shelter;
			public CPos Destination;
			public List<CPos> Route;
		}

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

		/// <summary>Sum of <see cref="AirThreatGeometry.AaEffectiveness"/> weights, not a raw headcount.</summary>
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
			return AirThreatGeometry.ConfiguredThreatWeight(actor.Info.Name, derivedWeight,
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
			var samWeight = AirThreatGeometry.ConfiguredThreatWeight("sam", 1f,
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
				weight = AirThreatGeometry.OrcaTransitThreatWeight(weight,
					AirThreatGeometry.CanOutrun(aircraftSpeed, profile.FastestProjectileSpeed));

			return weight;
		}

		protected static List<Actor> AirDecisionUnits(Squad owner)
		{
			return owner.AirFormationUnits(bootstrapIfEmpty: true);
		}

		static CPos CoarseCell(Squad owner, CPos cell)
		{
			var coarseSize = owner.SquadManager.Info.AirInfluenceCellSize;
			return new CPos(cell.X / coarseSize, cell.Y / coarseSize);
		}

		static void RecordAirPhase(Squad owner, string phase, long started)
		{
			if (!Game.IsBenchmarking)
				return;

			var elapsed = 1000.0 * Math.Max(0, Stopwatch.GetTimestamp() - started) / Stopwatch.Frequency;
			Game.RecordBotModuleSample(owner.Bot.Player.ClientIndex,
				$"AirSquad/{owner.AirProfile}/{phase}", elapsed, 0);
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

			foreach (var aircraft in owner.Units)
			{
				if (!owner.AirReinforcements.Contains(aircraft.ActorID) ||
					owner.AirUnitsRepairing.Contains(aircraft.ActorID))
					continue;

				var aircraftCell = CoarseCell(owner, aircraft.Location);
				var nearFormation = formationCell != null &&
					AirThreatGeometry.IsSameOrAdjacentCoarseCell(aircraftCell, formationCell.Value);
				var nearDestination = destinationCell != null &&
					AirThreatGeometry.IsSameOrAdjacentCoarseCell(aircraftCell, destinationCell.Value);
				if (!nearFormation && !nearDestination)
					continue;

				owner.JoinAirFormation(aircraft);
				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Air reinforcement [{0}] {1}#{2}: joined formation near {3}; aircraft-cell={4} formation-cell={5} destination-cell={6}.",
						owner.AirProfile, aircraft.Info.Name, aircraft.ActorID,
						nearFormation ? "squad" : "destination", aircraftCell,
						formationCell?.ToString() ?? "none", destinationCell?.ToString() ?? "none");
			}
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
		/// <see cref="AirThreatGeometry.AaEffectiveness"/>), the range of its AA weapon in cells, and the
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
				: AirThreatGeometry.AaEffectiveness(
					WeaponInaccuracy(bestAa.Weapon), WeaponDamage(bestAa.Weapon),
					WeaponInaccuracy(bestPrimary.Weapon), WeaponDamage(bestPrimary.Weapon));

			return (weight, bestAa.MaxRange().Length / 1024f, bestAa.Weapon.Range.Length / 1024f,
				WeaponProjectileSpeed(bestAa.Weapon));
		}

		enum AirTargetClass { Unit, Building, Production, Harvester }

		protected sealed class AirTargetPlan
		{
			public readonly Actor Actor;
			public readonly int Score;
			public readonly bool IsUndefended;
			public readonly List<CPos> Route;
			public readonly bool ClearsAa;
			public readonly List<Squad> SupportSquads;
			public readonly CPos? AaProtectedCell;
			public readonly IReadOnlyCollection<uint> AaThreatIds;

			public AirTargetPlan(Actor actor, int score, bool isUndefended, List<CPos> route,
				bool clearsAa = false, List<Squad> supportSquads = null, CPos? aaProtectedCell = null,
				IReadOnlyCollection<uint> aaThreatIds = null)
			{
				Actor = actor;
				Score = score;
				IsUndefended = isUndefended;
				Route = route;
				ClearsAa = clearsAa;
				SupportSquads = supportSquads;
				AaProtectedCell = aaProtectedCell;
				AaThreatIds = aaThreatIds;
			}
		}

		sealed class DefendedCellPlan
		{
			public DefendedAirAction Action;
			public Actor ClearTarget;
			public List<Squad> SupportSquads;
			public List<uint> AaThreatIds;
			public double DangerValue;
			public long UnlockedValue;
			public long CellKillTicks;
			public long ProtectedKillTicks;
			public long AaClearTicks;
			public long ClearAircraftValue;
			public float ClearReferenceWeight;
		}

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
				: AirThreatGeometry.RemainingHealthPriority(baseUtility, health.HP, health.MaxHP);
		}

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
			var clearEligible = AirThreatGeometry.CanAttemptAaClear(
				owner.AirConsecutiveNoUndefendedScans, info.AirTargetAaClearFallbackScans,
				combinedValue, referenceThreatWeight, dangerValue, info.AirTargetAaClearValueRatio);
			var patientEnough = owner.AirConsecutiveNoUndefendedScans >= info.AirTargetAaClearFallbackScans;
			var action = patientEnough ? AirThreatGeometry.ChooseDefendedAction(
				cellKillTicks, protectedKillTicks, aaClearTicks, clearEligible) : DefendedAirAction.Reject;
			if (action != DefendedAirAction.ClearAa)
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
			incumbentPlan = null;
			var map = owner.World.Map;
			var info = owner.SquadManager.Info;
			var coarseSize = info.AirInfluenceCellSize;
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
							AirThreatGeometry.MobileThreatBufferCells(mobile.Speed, info.AirInfluenceCacheInterval);
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
						var adjustedPriority = AirThreatGeometry.CoverageAdjustedPriority(authoredPriority,
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
					priority = AirThreatGeometry.CoverageAdjustedPriority(priority, coveredTargets,
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
						priority = AirThreatGeometry.CoverageAdjustedPriority(priority, coveredTargets,
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
			var useClusterOpportunity = AirThreatGeometry.UseClusterOpportunity(
				incumbent != null && !incumbent.IsDead, currentCellHasTargets);
			var requiredCellIndex = requiredStrategicCell == null ? -1 :
				candidateCells.FindIndex(c => c == requiredStrategicCell.Value);
			var selectedCellIndices = AirThreatGeometry.SelectTargetCandidates(
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
				if (plan.Action != DefendedAirAction.ClearAa || plan.ClearTarget == null)
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
				var opportunity = AirThreatGeometry.AirTargetOpportunityValue(clearUtility,
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
							defendedPlan.Action != DefendedAirAction.Sneak)
							continue;

						quickStrike = true;
					}

					var stoppingCost = (int)(destinationDanger * info.AirTargetAntiAirPenalty / attackRiskScale);
					if (quickStrike)
						stoppingCost /= 2;

					var isUndefended = destinationDanger <= 0 && !clearsAa;
					cellUnlockUtility.TryGetValue(cell, out var unlockedValue);
					var candidateLocationValue = AirThreatGeometry.AirTargetOpportunityValue(
						candidate.Utility, clusteredOpportunityValue, useClusterOpportunity,
						isUndefended, clearsAa, info.AirTargetAaClearUnlockPercent);
					var targetValue = clearsAa ? AirThreatGeometry.AirTargetOpportunityValue(
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
				var incumbentAaIndex = AirThreatGeometry.SelectAaClearCandidateForTarget(
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
				var aaClearIndex = AirThreatGeometry.SelectAaClearCandidate(
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
			if (plan.ClearsAa)
				owner.SquadManager.MarkGroundTargetForAirSupport(plan.Actor);

			owner.TargetActor = plan.Actor;
			owner.AirRoute.Clear();
			owner.AirRouteQueued = false;
			owner.AirReinforcementTargets.Clear();
			owner.AirTargetStrategicCell = new CPos(
				plan.Actor.Location.X / info.AirInfluenceCellSize,
				plan.Actor.Location.Y / info.AirInfluenceCellSize);
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

			owner.AirNextTargetReviewTick = owner.World.WorldTick + info.AirInfluenceCacheInterval;
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
				support.FuzzyStateMachine.ChangeState(support, new AirAttackState(), true);
				if (info.AirTargetDebugLogging)
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
		}

		protected static List<CPos> SafeRouteForAircraft(Squad owner, Actor aircraft, Actor target,
			bool requireInfluenceCache = false)
		{
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
			var coarseSize = info.AirInfluenceCellSize;
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
				var weight = AirThreatGeometry.ConfiguredThreatWeight(enemy.Info.Name, profile.Weight,
					info.AirThreatWeightOverrides);
				if (weight <= 0)
					continue;

				var range = Math.Max(1, (int)Math.Ceiling(profile.RangeCells * info.AirThreatRangeBuffer));
				var mobile = enemy.Info.TraitInfoOrDefault<MobileInfo>();
				if (mobile != null)
					range += AirThreatGeometry.MobileThreatBufferCells(mobile.Speed, info.AirInfluenceCacheInterval);

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
				var weight = AirThreatGeometry.ConfiguredThreatWeight(enemy.Info.Name, profile.Weight,
					info.AirThreatWeightOverrides);
				if (weight <= 0)
					continue;

				var range = Math.Max(1, (int)Math.Ceiling(profile.RangeCells * info.AirThreatRangeBuffer));
				var mobile = enemy.Info.TraitInfoOrDefault<MobileInfo>();
				if (mobile != null)
					range += AirThreatGeometry.MobileThreatBufferCells(mobile.Speed, info.AirInfluenceCacheInterval);

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
			var coarseSize = info.AirInfluenceCellSize;
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
				owner.FuzzyStateMachine.ChangeState(owner, new AirAttackState(), true);
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
			owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), true);
			if (info.AirTargetDebugLogging)
				Log.Write("debug", "Air escape [{0}] routed to nearest safe cell {1}: waypoints={2}.",
					owner.AirProfile, destination, route.Count);

			return true;
		}

		// New and repaired aircraft remain reinforcements until they reach the target's coarse cell or one
		// of its neighbors. They always receive a route from their own position and never inherit the
		// formation's shared route while catching up.
		protected static void QueueSafeRouteForReinforcement(Squad owner, Actor aircraft, Actor target)
		{
			var route = SafeRouteForAircraft(owner, aircraft, target);
			if (route == null)
			{
				owner.Bot.QueueOrder(new Order("Move", aircraft,
					Target.FromCell(owner.World, aircraft.Location), false));
				owner.AirReinforcementTargets.Remove(aircraft.ActorID);
				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Air route [{0}] {1}#{2}: withholding direct attack on {3}#{4}; no current-position safe route is available.",
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
				default: return info.AirTargetUnitValue;
			}
		}

		static AirTargetClass Classify(Actor a)
		{
			if (a.Info.HasTraitInfo<HarvesterInfo>())
				return AirTargetClass.Harvester;

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
			var response = AirThreatGeometry.PlannedAaClearResponse(
				owner.AirTargetClearsAa, selectedTargetInRange, allLocalThreatsPlanned);
			if (response == LocalAaClearResponse.Flee)
				return false;

			if (response == LocalAaClearResponse.Continue)
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
			var eligible = clearTicks != long.MaxValue && AirThreatGeometry.CanAttemptAaClear(
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
			owner.FuzzyStateMachine.ChangeState(owner, new AirAttackState(), true);
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
					var withinRange = AirThreatGeometry.IsWithinBufferedRange(distanceInCells, profile.RangeCells, info.AirThreatRangeBuffer);
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
				var score = AirThreatGeometry.TargetScore(
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
				var coarseSize = info.AirInfluenceCellSize;
				var squadCell = owner.World.Map.CellContaining(owner.AirFormationCenter);
				var targetCell = owner.TargetActor.Location;
				inTargetCell = squadCell.X / coarseSize == targetCell.X / coarseSize &&
					squadCell.Y / coarseSize == targetCell.Y / coarseSize;
			}

			var localRisk = AirThreatGeometry.LocalAirRiskMultiplier(inTargetCell,
				owner.SquadManager.AirRiskMultiplier(owner.AirProfile));
			var decisionUnits = AirDecisionUnits(owner);
			var effectiveSquadStrength = (int)Math.Min(int.MaxValue,
				Math.Ceiling(decisionUnits.Count * localRisk));
			var shouldFlee = !ownBuildingNear && AirThreatGeometry.ShouldFleeAntiAir(
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
					owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), true);
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
			owner.FuzzyStateMachine.ChangeState(owner, new AirAttackState(), true);
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

			var destination = AirThreatGeometry.EvadeDestination(
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
				if (targetDanger <= 0)
				{
					if (!a.IsIdle)
						return true;

					if (passiveRange > WDist.Zero &&
						(repairTarget.CenterPosition - a.CenterPosition).HorizontalLength <= passiveRange.Length)
						return true;
				}

				previousRepairTarget = targetEligible ? repairTarget : null;
				previousTargetDanger = targetDanger;
				if (!targetEligible)
					owner.AirRepairTargets.Remove(a.ActorID);

				if (owner.SquadManager.Info.AirTargetDebugLogging && !targetEligible)
					Log.Write("debug", "Air repair [{0}] {1}#{2}: previous destination {3} became {4}; replanning.",
						owner.AirProfile, a.Info.Name, a.ActorID,
						repairTarget == null ? "unavailable" : repairTarget.Info.Name + "#" + repairTarget.ActorID,
						"unavailable");
			}

			var recovery = FindSafestRepairBuilding(owner, a, repairable, threats, requireAvailable: true);
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
				owner.MarkAirRepairing(a, waitingAt.Building);
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
				var currentlySafe = RepairDestinationDanger(a.CenterPosition, threats) <= 0;
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

				owner.MarkAirRepairing(a, compromisedTarget);
				return true;
			}

			owner.AirUnitsRepairing.Remove(a.ActorID);
			owner.AirRepairTargets.Remove(a.ActorID);
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
					AirThreatGeometry.MobileThreatBufferCells(mobile.Speed, info.AirInfluenceCacheInterval);
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
					var assignedToOther = AirThreatGeometry.HasOtherRepairAssignment(owner.AirRepairTargets,
						owner.AirUnitsRepairing, aircraft.ActorID, b.ActorID);
					if (assignedToOther || !Reservable.IsAvailableFor(b, aircraft))
						continue;
				}

				candidates.Add((b, owned && requireAvailable));
			}

			if (candidates.Count == 0)
				return new AirRepairPlan();

			var info = owner.SquadManager.Info;
			var map = owner.World.Map;
			var coarseSize = info.AirInfluenceCellSize;
			var danger = BuildRepairDangerGrid(owner, threats, out var width, out var height);
			var aircraftSpeed = aircraft.Info.TraitInfoOrDefault<AircraftInfo>()?.Speed ?? info.AirTargetReferenceSpeed;
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
				if (RepairDestinationDanger(candidate.Building.CenterPosition, threats) > 0 ||
					danger[goalY * width + goalX] > 0)
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

		static float[] BuildRepairDangerGrid(Squad owner,
			IEnumerable<(Actor Actor, float Weight, int RangeCells)> threats, out int width, out int height)
		{
			var info = owner.SquadManager.Info;
			var map = owner.World.Map;
			var coarseSize = info.AirInfluenceCellSize;
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
			var coarseSize = info.AirInfluenceCellSize;
			var danger = BuildRepairDangerGrid(owner, threats, out var width, out var height);
			var start = map.CellContaining(aircraft.CenterPosition);
			var startX = Math.Clamp(start.X / coarseSize, 0, width - 1);
			var startY = Math.Clamp(start.Y / coarseSize, 0, height - 1);
			var aircraftSpeed = aircraft.Info.TraitInfoOrDefault<AircraftInfo>()?.Speed ?? info.AirTargetReferenceSpeed;
			Actor bestShelter = null;
			List<CPos> bestRoute = null;
			var bestCost = float.MaxValue;
			foreach (var shelter in owner.World.ActorsHavingTrait<Building>()
				.Where(b => b.Owner == owner.Bot.Player).OrderBy(b => b.ActorID))
			{
				var goalX = Math.Clamp(shelter.Location.X / coarseSize, 0, width - 1);
				var goalY = Math.Clamp(shelter.Location.Y / coarseSize, 0, height - 1);
				if (RepairDestinationDanger(shelter.CenterPosition, threats) > 0 ||
					danger[goalY * width + goalX] > 0)
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

				var distance = AirThreatGeometry.NearestThreatDistanceSquared(b.CenterPosition, threats);
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
			return ShouldFlee(owner, enemies => CountAntiAirUnits(owner, enemies) * MissileUnitMultiplier >
				AirDecisionUnits(owner).Count);
		}
	}

	class AirIdleState : AirStateBase, IState
	{
		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Air state [{0}] idle tick: units={1} no-target-scans={2}.",
					owner.AirProfile, owner.Units.Count, owner.AirConsecutiveNoTargetScans);

			// The continuous safety check watches the squad's surroundings on its own, much shorter
			// interval, so this scan is pure duplicated work whenever that is switched on.
			if (owner.SquadManager.Info.AirSafetyCheckInterval <= 0 && ShouldFlee(owner))
			{
				owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), true);
				return;
			}

			var e = FindDefenselessTarget(owner);
			if (e == null)
			{
				// Given up waiting for a positive score: accept the best finite-cost route instead of idling
				// forever. Threat costs remain intact, and squad size already scales acceptable risk.
				var threshold = owner.SquadManager.Info.AirMassedAttackIdleThreshold;
				if (threshold > 0)
				{
					owner.AirConsecutiveNoTargetScans++;
					if (owner.AirConsecutiveNoTargetScans > threshold)
					{
						var massedTarget = FindBestAirTarget(owner, relaxed: true);
						if (massedTarget != null)
						{
							owner.AirConsecutiveNoTargetScans = 0;
							ApplyAirTargetPlan(owner, massedTarget);
							owner.FuzzyStateMachine.ChangeState(owner, new AirAttackState(), true);
							return;
						}
					}
				}

				// Nothing worth hitting from where we are standing. If the squad remembers anti-air it is
				// loitering next to an enemy base, so shuffle to a nearby point and try the scan again from
				// there instead of hovering: this is the "if it cannot get there in a straight line, move
				// around the base and try again" half of the loop, done the cheap way.
				if (owner.SquadManager.Info.AirEvadeDistance > 0 && owner.AirThreatPositions.Count > 0)
					Evade(owner, "no eligible target near remembered AA");

				return;
			}

			owner.AirConsecutiveNoTargetScans = 0;
			ApplyAirTargetPlan(owner, e);
			owner.FuzzyStateMachine.ChangeState(owner, new AirAttackState(), true);
		}

		public void Deactivate(Squad owner) { }
	}

	class AirAttackState : AirStateBase, IState
	{
		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			var info = owner.SquadManager.Info;
			foreach (var unit in owner.Units)
				SendHomeToRepair(owner, unit);

			PromoteArrivedAirReinforcements(owner);
			var decisionUnits = AirDecisionUnits(owner);
			var formationUnits = owner.AirFormationUnits();
			if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Air state [{0}] attack tick: units={1} formation={2} reinforcements={3} target-valid={4} route-queued={5}.",
					owner.AirProfile, owner.Units.Count, formationUnits.Count, owner.AirReinforcements.Count,
					owner.IsTargetValid, owner.AirRouteQueued);

			var hasArmedUnit = decisionUnits.Any(a =>
				HasAmmo(a.TraitsImplementing<AmmoPool>()));
			var anyUnitBusy = decisionUnits.Any(a =>
				(BusyAttack(a) || !a.IsIdle));
			var routeTraveling = owner.AirRouteQueued && formationUnits.Any(a => !a.IsIdle && !BusyAttack(a));
			var ticksSinceProgress = owner.World.WorldTick - owner.AirTargetLastProgressTick;

			if (owner.IsTargetValid && owner.World.WorldTick >= owner.AirNextTargetReviewTick)
			{
				owner.AirNextTargetReviewTick = owner.World.WorldTick + info.AirInfluenceCacheInterval;
				if (owner.TargetActor.Info.HasTraitInfo<BuildingInfo>() && hasArmedUnit)
				{
					var incumbent = owner.TargetActor;
					var previousScore = owner.AirTargetScore;
					var challenger = FindBestAirTarget(owner, incumbent, out var freshIncumbent,
						requiredAaProtectedCell: owner.AirTargetClearsAa ? owner.AirAaClearProtectedCell : null);
					var recalculatedIncumbent = challenger != null && challenger.Actor == incumbent ?
						challenger : freshIncumbent;
					var switchTarget = recalculatedIncumbent == null ||
						(challenger != null && challenger.Actor != incumbent &&
						AirThreatGeometry.ShouldSwitchTarget(recalculatedIncumbent.IsUndefended,
							recalculatedIncumbent.Score, true, challenger.IsUndefended, challenger.Score,
							info.AirTargetSwitchImprovementPercent));
					if (switchTarget)
					{
						if (info.AirTargetDebugLogging)
							Log.Write("debug", "Air target [{0}] switching building {1}#{2} score={3} to {4} score={5}: improvement threshold={6}%.",
								owner.AirProfile, incumbent.Info.Name, incumbent.ActorID, previousScore,
								challenger == null ? "none" : challenger.Actor.Info.Name + "#" + challenger.Actor.ActorID,
								challenger?.Score ?? int.MinValue,
								info.AirTargetSwitchImprovementPercent);

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
							Log.Write("debug", "Air target [{0}] retaining building {1}#{2}: challenger={3} old-score={4} recalculated-score={5} challenger-score={6} clears-aa={7} protected-cell={8}.",
								owner.AirProfile, incumbent.Info.Name, incumbent.ActorID,
								challenger == null ? "none" : challenger.Actor.Info.Name + "#" + challenger.Actor.ActorID,
								previousScore, recalculatedIncumbent.Score,
								challenger?.Score ?? int.MinValue, recalculatedIncumbent.ClearsAa,
								recalculatedIncumbent.AaProtectedCell?.ToString() ?? "none");
					}
				}
			}

			if (owner.IsTargetValid)
			{
				var currentCell = new CPos(
					owner.TargetActor.Location.X / info.AirInfluenceCellSize,
					owner.TargetActor.Location.Y / info.AirInfluenceCellSize);
				if (owner.AirTargetStrategicCell == null)
				{
					owner.AirTargetStrategicCell = currentCell;
					owner.AirTargetLastProgressTick = owner.World.WorldTick;
					owner.AirTargetLastDistanceCells =
						(owner.TargetActor.CenterPosition - owner.AirFormationCenter).Length / 1024;
					owner.AirTargetLastHP = owner.TargetActor.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue;
				}
				else if (currentCell != owner.AirTargetStrategicCell.Value)
				{
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
					else if (best == null || best.Actor == incumbent ||
						!AirThreatGeometry.ShouldSwitchTarget(recalculatedIncumbent.IsUndefended,
							recalculatedIncumbent.Score, true, best.IsUndefended, best.Score,
							info.AirTargetSwitchImprovementPercent))
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
					else if (AirThreatGeometry.ShouldRescanStalledTarget(
						owner.World.WorldTick - owner.AirTargetLastProgressTick, info.AirTargetStallTicks, hasArmedUnit))
					{
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
				var nextTarget = rememberedTargetCell == null
					? FindBestAirTarget(owner)
					: FindBestAirTarget(owner, null, out _, rememberedTargetCell);
				if (nextTarget == null && !decisionUnits.Any(a => HasAmmo(a.TraitsImplementing<AmmoPool>())))
					return;

				if (nextTarget == null)
				{
					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air evade [{0}] attack state found no eligible target with armed aircraft; entering flee state.",
							owner.AirProfile);

					owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), true);
					return;
				}

				ApplyAirTargetPlan(owner, nextTarget);
			}

			if (owner.AirProfile == "Generic" && !NearToPosSafely(owner, owner.TargetActor.CenterPosition))
			{
				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air evade [{0}] generic proximity safety rejected target {1}#{2}.",
						owner.AirProfile, owner.TargetActor.Info.Name, owner.TargetActor.ActorID);

				owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), true);
				return;
			}

			// Submit the selected shared route once for the current squad. This restores the stable bleed
			// lifecycle: the route is a transient order batch, not a target-lifetime per-aircraft latch.
			if (owner.AirRoute.Count > 1 && !owner.AirRouteQueued)
			{
				foreach (var a in formationUnits)
				{
					if (SendHomeToRepair(owner, a))
						continue;

					var queued = false;
					foreach (var waypoint in owner.AirRoute)
					{
						owner.Bot.QueueOrder(new Order("Move", a, Target.FromCell(owner.World, waypoint), queued));
						queued = true;
					}

					if (CanAttackTarget(a, owner.TargetActor))
						owner.Bot.QueueOrder(new Order("Attack", a, Target.FromActor(owner.TargetActor), true));

					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air route [{0}] {1}#{2}: queued shared safe route ({3} waypoints) to {4}#{5}.",
							owner.AirProfile, a.Info.Name, a.ActorID, owner.AirRoute.Count,
							owner.TargetActor.Info.Name, owner.TargetActor.ActorID);
				}

				foreach (var a in owner.Units)
					if (owner.AirReinforcements.Contains(a.ActorID) &&
						!owner.AirUnitsRepairing.Contains(a.ActorID))
						QueueSafeRouteForReinforcement(owner, a, owner.TargetActor);

				owner.AirRouteQueued = formationUnits.Count > 0;
				owner.AirRoute.Clear();
				return;
			}

			// Once no aircraft is still traveling the shared route, release the transient flag. Busy attack
			// orders do not keep it latched, and repaired/new idle aircraft are replanned below.
			if (owner.AirRouteQueued && !routeTraveling)
			{
				owner.AirRouteQueued = false;
				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air route [{0}] shared route completed; idle joiners will replan from their current position.",
						owner.AirProfile);
			}

			// Lazily computed: only needed if a self-reloading aircraft actually turns out to be dry,
			// which is the uncommon case, and shared across every unit that needs it this tick rather
			// than recomputed (and drawing fresh jitter from World.LocalRandom) per unit.
			CPos? disengageDestination = null;

			foreach (var a in owner.Units)
			{
				if (SendHomeToRepair(owner, a))
					continue;

				if (owner.AirReinforcements.Contains(a.ActorID))
				{
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

				if (CanAttackTarget(a, owner.TargetActor))
				{
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

	class AirFleeState : AirStateBase, IState
	{
		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			if (owner.AirEscapingLocalAa)
			{
				if (owner.Units.Any(a => !owner.AirUnitsRepairing.Contains(a.ActorID) && !a.IsIdle))
					return;

				owner.AirEscapingLocalAa = false;
				owner.FuzzyStateMachine.ChangeState(owner, new AirIdleState(), true);
				return;
			}

			Evade(owner, "flee-state continuation");

			// Straight back to idle: the next scan - whichever of the state machine or the much faster
			// safety check gets there first - re-targets from wherever the hop put us.
			owner.FuzzyStateMachine.ChangeState(owner, new AirIdleState(), true);
		}

		public void Deactivate(Squad owner) { }
	}
}
