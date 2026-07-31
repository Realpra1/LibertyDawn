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
			public List<(Actor Actor, int Utility)> Candidates;
			public List<(Actor Actor, float Weight, int RangeCells)> Threats;
		}

		// Bot logic is host-only. Sharing one cache per manager/profile prevents two same-type squads
		// rebuilding the same world influence map during the configured strategic cache interval.
		static readonly Dictionary<SquadManagerBotModule, Dictionary<string, AirInfluenceCache>> InfluenceCaches =
			new Dictionary<SquadManagerBotModule, Dictionary<string, AirInfluenceCache>>();

		protected const int MissileUnitMultiplier = 3;

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
		protected static float CountAntiAirUnits(IEnumerable<Actor> units)
		{
			var weight = 0f;
			foreach (var unit in units)
				weight += AntiAirProfile(unit).Weight;

			return weight;
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
		/// Conservative host-side estimate of whether fully loaded squad members can destroy a target
		/// with their current magazines. This intentionally uses rules data only: target HP and armor,
		/// weapon damage/versus/burst, and actual ammo state. It is used to recognize disposable AA
		/// such as rocket infantry without actor-name exceptions.
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
					return false;

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
		/// all, or an aircraft itself) short-circuits to (0, 0, int.MaxValue).
		/// </summary>
		protected static (float Weight, int RangeCells, int FastestProjectileSpeed) AntiAirProfile(Actor unit)
		{
			if (unit == null || unit.Info.HasTraitInfo<AircraftInfo>())
				return (0, 0, int.MaxValue);

			WeaponInfo bestAa = null;
			WeaponInfo bestPrimary = null;

			// PERF: Avoid LINQ.
			foreach (var ab in unit.TraitsImplementing<AttackBase>())
			{
				if (ab.IsTraitDisabled || ab.IsTraitPaused)
					continue;

				foreach (var a in ab.Armaments)
				{
					if (a.Weapon.IsValidTarget(AirTargetTypes))
					{
						if (bestAa == null || a.Weapon.Range > bestAa.Range)
							bestAa = a.Weapon;
					}
					else if (bestPrimary == null || a.Weapon.Range > bestPrimary.Range)
						bestPrimary = a.Weapon;
				}
			}

			if (bestAa == null)
				return (0, 0, int.MaxValue);

			var weight = bestPrimary == null
				? 1f
				: AirThreatGeometry.AaEffectiveness(
					WeaponInaccuracy(bestAa), WeaponDamage(bestAa),
					WeaponInaccuracy(bestPrimary), WeaponDamage(bestPrimary));

			return (weight, bestAa.Range.Length / 1024, WeaponProjectileSpeed(bestAa));
		}

		enum AirTargetClass { Unit, Building, Production, Harvester }

		protected sealed class AirTargetPlan
		{
			public readonly Actor Actor;
			public readonly int Score;
			public readonly bool IsUndefended;
			public readonly List<CPos> Route;

			public AirTargetPlan(Actor actor, int score, bool isUndefended, List<CPos> route)
			{
				Actor = actor;
				Score = score;
				IsUndefended = isUndefended;
				Route = route;
			}
		}

		protected static AirTargetPlan FindDefenselessTarget(Squad owner)
		{
			return FindBestAirTarget(owner);
		}

		/// <summary>
		/// Stock-style tactical fallback: the nearest enemy that at least one squad member can attack.
		/// Unlike the strategic scan this deliberately ignores route utility so an aircraft already in
		/// contact never retreats across the map merely because strategic planning found no good option.
		/// </summary>
		protected static Actor FindClosestAttackableEnemy(Squad owner)
		{
			return owner.World.Actors
				.Where(a => owner.SquadManager.IsPreferredEnemyUnit(a) &&
					owner.Units.Any(u => CanAttackTarget(u, a)))
				.ClosestTo(owner.CenterPosition);
		}

		/// <summary>
		/// Builds a deterministic coarse influence grid, then uses bounded A* route costs to compare the
		/// best targets. Unlike the old random sampler this considers every known actor, can value a safe
		/// detour, and keeps AA as a finite cost whose importance falls gradually as the squad grows.
		/// </summary>
		protected static AirTargetPlan FindBestAirTarget(Squad owner, bool relaxed = false)
		{
			var map = owner.World.Map;
			var info = owner.SquadManager.Info;
			var coarseSize = info.AirInfluenceCellSize;
			var width = (map.MapSize.X + coarseSize - 1) / coarseSize;
			var height = (map.MapSize.Y + coarseSize - 1) / coarseSize;
			var archetypePriority = ArchetypePriority(owner);
			var apache = owner.AirProfile.Equals("Apache", StringComparison.OrdinalIgnoreCase);
			var orca = owner.AirProfile.Equals("Orca", StringComparison.OrdinalIgnoreCase);
			var planningUnits = owner.Units.Where(a => !owner.AirUnitsRepairing.Contains(a.ActorID)).ToList();
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
			if (!profileCaches.TryGetValue(cacheKey, out var cache) || cache.Width != width || cache.Height != height ||
				owner.World.WorldTick - cache.Tick >= info.AirInfluenceCacheInterval)
			{
				var rebuiltDanger = new float[width * height];
				var rebuiltCandidates = new List<(Actor Actor, int Utility)>();
				var rebuiltThreats = new List<(Actor Actor, float Weight, int RangeCells)>();
				foreach (var actor in owner.World.Actors)
				{
					if (!owner.SquadManager.IsPreferredEnemyUnit(actor))
						continue;

					var profile = AntiAirProfile(actor);
					if (profile.Weight > 0)
					{
						var range = Math.Max(1, (int)Math.Ceiling(profile.RangeCells * info.AirThreatRangeBuffer));
						var mobile = actor.Info.TraitInfoOrDefault<MobileInfo>();
						var movementBuffer = mobile == null ? 0 :
							AirThreatGeometry.MobileThreatBufferCells(mobile.Speed, info.AirInfluenceCacheInterval);
						var influenceRange = range + movementBuffer;
						var minX = Math.Max(0, (actor.Location.X - influenceRange) / coarseSize);
						var maxX = Math.Min(width - 1, (actor.Location.X + influenceRange) / coarseSize);
						var minY = Math.Max(0, (actor.Location.Y - influenceRange) / coarseSize);
						var maxY = Math.Min(height - 1, (actor.Location.Y + influenceRange) / coarseSize);
						var weight = profile.Weight;
						if (apache && profile.Weight >= .75f)
							weight *= mobile == null ? 8f : 4f;
						else if (orca && AirThreatGeometry.CanOutrun(squadSpeed, profile.FastestProjectileSpeed))
							weight *= .5f;

						rebuiltThreats.Add((actor, weight, range));
						for (var y = minY; y <= maxY; y++)
							for (var x = minX; x <= maxX; x++)
							{
								var cell = new CPos(x * coarseSize + coarseSize / 2, y * coarseSize + coarseSize / 2);
								var distance = (map.CenterOfCell(map.Clamp(cell)) - actor.CenterPosition).Length / 1024;
								if (distance <= influenceRange)
									rebuiltDanger[y * width + x] += weight;
							}
					}

					if (!owner.SquadManager.IsNotHiddenUnit(actor))
						continue;

					var value = (long)Math.Max(1, TargetValue(actor, info, archetypePriority));
					var valued = actor.Info.TraitInfoOrDefault<ValuedInfo>();
					if (valued != null)
						value = value * (100 + Math.Min(valued.Cost / 100, 100)) / 100;

					var health = actor.TraitOrDefault<IHealth>();
					if (health != null && health.MaxHP > 0)
						value = value * 10000 / (10000 + health.HP);

					value = value * 100 / (100 + (int)(profile.Weight * info.AirTargetAntiAirPenalty));
					rebuiltCandidates.Add((actor, Math.Max(1, (int)Math.Min(int.MaxValue, value))));
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

			var danger = cache.Danger;
			var candidates = cache.Candidates;
			var threats = cache.Threats;

			var planningCenter = planningUnits.Select(a => a.CenterPosition).Average();
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

			var liveCandidates = candidates.Where(c => !c.Actor.IsDead &&
				planningUnits.Any(u => CanAttackTarget(u, c.Actor))).OrderBy(c => c.Actor.ActorID).ToList();
			var cellUtility = new Dictionary<CPos, long>();
			var cellActors = new Dictionary<CPos, List<int>>();
			for (var i = 0; i < liveCandidates.Count; i++)
			{
				var candidate = liveCandidates[i];
				var cell = new CPos(candidate.Actor.Location.X / coarseSize, candidate.Actor.Location.Y / coarseSize);
				cellUtility.TryGetValue(cell, out var total);
				cellUtility[cell] = total + candidate.Utility;
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
			var selectedCellIndices = AirThreatGeometry.SelectTargetCandidates(
				candidateCells.Select(c =>
				{
					var center = map.CenterOfCell(map.Clamp(new CPos(
						c.X * coarseSize + coarseSize / 2, c.Y * coarseSize + coarseSize / 2)));
					return (center - planningCenter).LengthSquared;
				}).ToList(),
				candidateCells.Select(c => (int)Math.Min(int.MaxValue, cellUtility[c])).ToList(),
				info.AirTargetClosestCandidates, info.AirTargetHighestValueCandidates);

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

			foreach (var selectedCellIndex in selectedCellIndices)
			{
				var cell = candidateCells[selectedCellIndex];
				var goalX = Math.Clamp(cell.X, 0, width - 1);
				var goalY = Math.Clamp(cell.Y, 0, height - 1);
				var opportunityValue = cellUtility[cell];
				var route = AirThreatGeometry.FindCoarseRoute(danger, width, height, startX, startY, goalX, goalY,
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
				Actor cellTarget = null;
				var cellTargetScore = int.MinValue;
				var cellTargetDanger = 0f;
				var cellTargetStoppingCost = 0;
				var cellTargetIsUndefended = false;
				var cellTargetClearsAa = false;

				// The cell sum ranks the location. The actor's own utility and exact AA coverage choose
				// the victim inside that location, so a SAM or power plant cannot inherit a harvester
				// cluster's full value merely by sharing its coarse tile.
				foreach (var candidateIndex in cellActors[cell])
				{
					var candidate = liveCandidates[candidateIndex];
					var destinationDanger = 0f;
					var clearsAa = false;
					foreach (var threat in threats)
					{
						if (threat.Actor.IsDead)
							continue;

						// A fully loaded squad may treat an AA actor it can eliminate in one magazine
						// as the target to clear, but every other defender around it remains in force.
						if (threat.Actor == candidate.Actor &&
							CanEliminateWithFullAmmo(planningUnits, candidate.Actor))
						{
							clearsAa = true;
							continue;
						}

						var distance = (threat.Actor.CenterPosition - candidate.Actor.CenterPosition).Length / 1024;
						if (distance <= threat.RangeCells)
							destinationDanger += threat.Weight;
					}

					var quickStrike = !clearsAa && destinationDanger > 0 &&
						CanEliminateWithFullAmmo(planningUnits, candidate.Actor);
					var stoppingCost = (int)(destinationDanger * info.AirTargetAntiAirPenalty / attackRiskScale);
					if (quickStrike)
						stoppingCost /= 2;

					// AA actors only receive unlock credit in the defended tier. The credit comes from
					// everything worth attacking in this cell and is divided by the other AA that would
					// still be shooting, so one mobile SAM screening harvesters becomes compelling while
					// a dense static SAM nest does not reward itself.
					var targetValue = (long)candidate.Utility;
					if (clearsAa)
						targetValue += opportunityValue * info.AirTargetAaClearUnlockPercent / 100;

					var isUndefended = destinationDanger <= 0 && !clearsAa;
					var targetScore = targetValue * 1024 / Math.Max(1, 1024 + stoppingCost);
					var finalTargetScore = (int)Math.Clamp(targetScore, int.MinValue, int.MaxValue);

					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air target [{0}] {1}#{2}: cell={3} utility={4} cell-utility={5} destination-danger={6:0.##} target-score={7} clears-aa={8} quick-strike={9} relaxed={10}",
							owner.AirProfile, candidate.Actor.Info.Name, candidate.Actor.ActorID, cell,
							candidate.Utility, opportunityValue, destinationDanger, finalTargetScore,
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
					}
				}

				if (cellTarget == null)
					continue;

				// Each independent liability scales the location value down. Defended locations remain
				// finite choices, but only after every selected undefended location has been considered.
				var score = opportunityValue;
				score = score * 1024 / Math.Max(1, 1024 + distanceCost);
				score = score * 1024 / Math.Max(1, 1024 + (int)exposureCost);
				score = score * 1024 / Math.Max(1, 1024 + cellTargetStoppingCost);
				var finalScore = (int)Math.Clamp(score, int.MinValue, int.MaxValue);

				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air cell [{0}] {1}: utility={2} route={3} exposure={4:0.##} target={5}#{6} destination-danger={7:0.##} score={8} undefended={9} clears-aa={10} ammo={11:0.##} relaxed={12}",
						owner.AirProfile, cell, opportunityValue, route.Count, exposureCost,
						cellTarget.Info.Name, cellTarget.ActorID, cellTargetDanger, finalScore,
						cellTargetIsUndefended, cellTargetClearsAa, ammoReadiness, relaxed);

				// Undefended targets form the first selection tier. A defended target remains eligible,
				// but only when this bounded strategic-cell pool contains no undefended destination at all.
				// Route and distance costs still rank candidates within each tier.
				if (best == null || (cellTargetIsUndefended && !bestIsUndefended) ||
					(cellTargetIsUndefended == bestIsUndefended && finalScore > bestScore))
				{
					best = cellTarget;
					bestScore = finalScore;
					bestIsUndefended = cellTargetIsUndefended;
					bestRoute = AirThreatGeometry.SmoothCoarseRoute(danger, width, height, startX, startY, route)
						.Select(p => map.Clamp(new CPos(p.X * coarseSize + coarseSize / 2, p.Y * coarseSize + coarseSize / 2))).ToList();
				}
			}

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

			return new AirTargetPlan(best, bestScore, bestIsUndefended, bestRoute);
		}

		protected static void ApplyAirTargetPlan(Squad owner, AirTargetPlan plan)
		{
			var info = owner.SquadManager.Info;
			owner.TargetActor = plan.Actor;
			owner.AirRoute.Clear();
			owner.AirRouteAssignedUnits.Clear();
			owner.AirRoutePlanTick = owner.World.WorldTick;
			owner.AirTargetStrategicCell = new CPos(
				plan.Actor.Location.X / info.AirInfluenceCellSize,
				plan.Actor.Location.Y / info.AirInfluenceCellSize);
			owner.AirTargetLastProgressTick = owner.World.WorldTick;
			owner.AirTargetLastDistanceCells = (plan.Actor.CenterPosition - owner.CenterPosition).Length / 1024;
			owner.AirTargetLastHP = plan.Actor.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue;
			owner.AirTargetScore = plan.Score;
			owner.AirTargetIsUndefended = plan.IsUndefended;
			owner.AirNextTargetReviewTick = owner.World.WorldTick + info.AirInfluenceCacheInterval;
			if (plan.Route != null)
				owner.AirRoute.AddRange(plan.Route);
		}

		protected static List<CPos> SafeRouteForAircraft(Squad owner, Actor aircraft, Actor target)
		{
			var info = owner.SquadManager.Info;
			var speed = aircraft.Info.TraitInfoOrDefault<AircraftInfo>()?.Speed ?? info.AirTargetReferenceSpeed;
			if (!InfluenceCaches.TryGetValue(owner.SquadManager, out var profileCaches) ||
				!profileCaches.TryGetValue(owner.AirProfile + ":" + speed, out var cache))
				return owner.AirRoute.ToList();

			var map = owner.World.Map;
			var coarseSize = info.AirInfluenceCellSize;
			var start = map.CellContaining(aircraft.CenterPosition);
			var startX = Math.Clamp(start.X / coarseSize, 0, cache.Width - 1);
			var startY = Math.Clamp(start.Y / coarseSize, 0, cache.Height - 1);
			var goalX = Math.Clamp(target.Location.X / coarseSize, 0, cache.Width - 1);
			var goalY = Math.Clamp(target.Location.Y / coarseSize, 0, cache.Height - 1);
			var route = AirThreatGeometry.FindCoarseRoute(cache.Danger, cache.Width, cache.Height,
				startX, startY, goalX, goalY, info.AirRouteThreatPenalty);
			if (route == null)
				return new List<CPos>();

			return AirThreatGeometry.SmoothCoarseRoute(
				cache.Danger, cache.Width, cache.Height, startX, startY, route)
				.Select(p => map.Clamp(new CPos(
					p.X * coarseSize + coarseSize / 2, p.Y * coarseSize + coarseSize / 2))).ToList();
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

			var tick = owner.World.WorldTick;
			owner.ForgetExpiredAirThreats(tick);

			var mergeRadius = WDist.FromCells(info.AirThreatMemoryMergeRadius);
			var expiry = tick + info.AirThreatMemoryTicks;
			var squadCenter = owner.CenterPosition;
			var archetypePriority = ArchetypePriority(owner);
			var apache = owner.AirProfile.Equals("Apache", StringComparison.OrdinalIgnoreCase);
			var orca = owner.AirProfile.Equals("Orca", StringComparison.OrdinalIgnoreCase);

			// Only while already committed to a target (i.e. actually flying somewhere, not idling and
			// scanning in place) may a threat every unit in the squad can outrun be flown through instead
			// of triggering a flee - this is "as long as they don't stop", not a standing immunity.
			var committed = owner.IsTargetValid;
			var squadSpeed = committed ? SquadSlowestAircraftSpeed(owner) : 0;

			var antiAirWeight = 0f;
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
				if (profile.Weight > 0)
				{
					// Remembered regardless of range/outrun status - route-scoring elsewhere still
					// benefits from knowing it is there even if it is not an immediate flee trigger.
					owner.RememberAirThreat(a.CenterPosition, expiry, mergeRadius, info.AirThreatMemorySize);

					var distanceInCells = (a.CenterPosition - squadCenter).Length / 1024;
					var withinRange = AirThreatGeometry.IsWithinBufferedRange(distanceInCells, profile.RangeCells, info.AirThreatRangeBuffer);
					var outrunnable = committed && AirThreatGeometry.CanOutrun(squadSpeed, profile.FastestProjectileSpeed);

					if (withinRange)
					{
						var localWeight = profile.Weight;
						var mobile = a.Info.TraitInfoOrDefault<MobileInfo>();
						if (apache)
							localWeight *= mobile == null ? 8f : 4f;
						else if (orca && outrunnable)
							localWeight *= .5f;

						antiAirWeight += localWeight;
					}

					continue;
				}

				if (!owner.SquadManager.IsNotHiddenUnit(a))
					continue;

				// Everything reaching here failed IsAntiAirCapable, so the defenceless bonus always applies.
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

			// Over our own base there is nowhere safer to run to, and our own defences are the answer.
			var adaptiveSquadStrength = (int)Math.Min(int.MaxValue,
				Math.Ceiling(owner.Units.Count * owner.SquadManager.AirRiskMultiplier(owner.AirProfile)));
			if (!ownBuildingNear && AirThreatGeometry.ShouldFleeAntiAir(
				antiAirWeight, info.AirThreatFleeMultiplier, adaptiveSquadStrength))
			{
				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air evade [{0}] local AA safety: threat={1:0.##} flee-multiplier={2} adaptive-strength={3} risk={4:0.00} target={5}.",
						owner.AirProfile, antiAirWeight, info.AirThreatFleeMultiplier, adaptiveSquadStrength,
						owner.SquadManager.AirRiskMultiplier(owner.AirProfile), owner.IsTargetValid ?
						owner.TargetActor.Info.Name + "#" + owner.TargetActor.ActorID : "none");

				// Drop the target so the squad re-evaluates from scratch once it is clear; the threat we
				// just remembered will make the route back through here look expensive.
				// The state change happens even when Evade declines to re-order (rate limit), so the squad
				// cannot keep pressing an attack run it has already decided to abandon.
				owner.TargetActor = null;
				Evade(owner, "local AA safety");
				owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), true);
				return;
			}

			// Only ever commit to a local target when this scan saw no anti-air at all within
			// AirThreatScanRadius, so the fast path can never walk the squad into cover it just measured.
			var hasFullyLoadedUnit = owner.Units.Any(a =>
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

		/// <summary>Slowest AircraftInfo.Speed among a squad's units, or int.MaxValue for an empty squad.</summary>
		static int SquadSlowestAircraftSpeed(Squad owner)
		{
			var slowest = int.MaxValue;

			// PERF: Avoid LINQ.
			foreach (var u in owner.Units)
			{
				var aircraft = u.Info.TraitInfoOrDefault<AircraftInfo>();
				if (aircraft != null && aircraft.Speed < slowest)
					slowest = aircraft.Speed;
			}

			return slowest;
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
				owner.CenterPosition, owner.AirThreatPositions, WDist.FromCells(info.AirEvadeDistance), jitter);

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
		/// Targets the nearest owned actor matching the unit's own <see cref="RepairableInfo.RepairActors"/>
		/// directly, via a "Repair" order rather than "ReturnToBase" - the literal ReturnToBase order string
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
				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Air repair [{0}] {1}#{2}: recovery complete.",
						owner.AirProfile, a.Info.Name, a.ActorID);

				return false;
			}

			if (!repairing && health.HP >= health.MaxHP * threshold)
				return false;

			if (repairing && !a.IsIdle)
				return true;

			if (IsRearming(a))
			{
				owner.AirUnitsRepairing.Add(a.ActorID);
				return true;
			}

			var repairable = a.Info.TraitInfoOrDefault<RepairableInfo>();
			if (repairable == null || repairable.RepairActors.Count == 0)
				return false;

			var recovery = FindSafestRepairBuilding(owner, a, repairable, requireAvailable: true);
			if (recovery.Building != null)
			{
				QueueRecoveryRoute(owner, a, recovery.Route, recovery.Building, repairAtEnd: true);
				owner.AirUnitsRepairing.Add(a.ActorID);
				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Air repair [{0}] {1}#{2}: {3}/{4} HP, safe route ({5} waypoints) to available {6}#{7}.",
						owner.AirProfile, a.Info.Name, a.ActorID, health.HP, health.MaxHP,
						recovery.Route?.Count ?? 0, recovery.Building.Info.Name, recovery.Building.ActorID);

				return true;
			}

			// Commit to recovery even when every pad is occupied. Waiting at the nearest compatible
			// facility keeps the aircraft out of combat, lets repair auras help where present, and retries
			// reservation on every safety update once it becomes idle.
			owner.AirUnitsRepairing.Add(a.ActorID);
			var waitingAt = FindSafestRepairBuilding(owner, a, repairable, requireAvailable: false);
			if (waitingAt.Building != null)
			{
				QueueRecoveryRoute(owner, a, waitingAt.Route, waitingAt.Building, repairAtEnd: false);
				if (owner.SquadManager.Info.AirTargetDebugLogging)
					Log.Write("debug", "Air repair [{0}] {1}#{2}: {3}/{4} HP, all pads occupied; safe wait route ({5} waypoints) to {6}#{7}.",
						owner.AirProfile, a.Info.Name, a.ActorID, health.HP, health.MaxHP,
						waitingAt.Route?.Count ?? 0, waitingAt.Building.Info.Name, waitingAt.Building.ActorID);
			}
			else if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Air repair [{0}] {1}#{2}: {3}/{4} HP, no compatible repair facility.",
					owner.AirProfile, a.Info.Name, a.ActorID, health.HP, health.MaxHP);

			return true;
		}

		static (Actor Building, List<CPos> Route) FindSafestRepairBuilding(
			Squad owner, Actor aircraft, RepairableInfo repairable, bool requireAvailable)
		{
			var candidates = new List<Actor>();
			foreach (var b in owner.World.ActorsHavingTrait<RepairsUnits>())
			{
				if (b.Owner != owner.Bot.Player || !repairable.RepairActors.Contains(b.Info.Name))
					continue;

				if (requireAvailable && !Reservable.IsAvailableFor(b, aircraft))
					continue;

				candidates.Add(b);
			}

			if (candidates.Count == 0)
				return (null, null);

			var info = owner.SquadManager.Info;
			var map = owner.World.Map;
			var coarseSize = info.AirInfluenceCellSize;
			var width = (map.MapSize.X + coarseSize - 1) / coarseSize;
			var height = (map.MapSize.Y + coarseSize - 1) / coarseSize;
			var danger = new float[width * height];
			var apache = owner.AirProfile.Equals("Apache", StringComparison.OrdinalIgnoreCase);
			var orca = owner.AirProfile.Equals("Orca", StringComparison.OrdinalIgnoreCase);
			var aircraftSpeed = aircraft.Info.TraitInfoOrDefault<AircraftInfo>()?.Speed ?? info.AirTargetReferenceSpeed;

			foreach (var enemy in owner.World.Actors)
			{
				if (!owner.SquadManager.IsPreferredEnemyUnit(enemy))
					continue;

				var profile = AntiAirProfile(enemy);
				if (profile.Weight <= 0)
					continue;

				var range = Math.Max(1, (int)Math.Ceiling(profile.RangeCells * info.AirThreatRangeBuffer));
				var mobile = enemy.Info.TraitInfoOrDefault<MobileInfo>();
				var movementBuffer = mobile == null ? 0 :
					AirThreatGeometry.MobileThreatBufferCells(mobile.Speed, info.AirInfluenceCacheInterval);
				var influenceRange = range + movementBuffer;
				var weight = profile.Weight;
				if (apache && profile.Weight >= .75f)
					weight *= mobile == null ? 8f : 4f;
				else if (orca && AirThreatGeometry.CanOutrun(aircraftSpeed, profile.FastestProjectileSpeed))
					weight *= .5f;

				var minX = Math.Max(0, (enemy.Location.X - influenceRange) / coarseSize);
				var maxX = Math.Min(width - 1, (enemy.Location.X + influenceRange) / coarseSize);
				var minY = Math.Max(0, (enemy.Location.Y - influenceRange) / coarseSize);
				var maxY = Math.Min(height - 1, (enemy.Location.Y + influenceRange) / coarseSize);
				for (var y = minY; y <= maxY; y++)
					for (var x = minX; x <= maxX; x++)
					{
						var cell = new CPos(x * coarseSize + coarseSize / 2, y * coarseSize + coarseSize / 2);
						var distance = (map.CenterOfCell(map.Clamp(cell)) - enemy.CenterPosition).Length / 1024;
						if (distance <= influenceRange)
							danger[y * width + x] += weight;
					}
			}

			var start = map.CellContaining(aircraft.CenterPosition);
			var startX = Math.Clamp(start.X / coarseSize, 0, width - 1);
			var startY = Math.Clamp(start.Y / coarseSize, 0, height - 1);
			Actor best = null;
			List<CPos> bestRoute = null;
			var bestCost = float.MaxValue;
			foreach (var candidate in candidates.OrderBy(a => a.ActorID))
			{
				var goalX = Math.Clamp(candidate.Location.X / coarseSize, 0, width - 1);
				var goalY = Math.Clamp(candidate.Location.Y / coarseSize, 0, height - 1);
				var route = AirThreatGeometry.FindCoarseRoute(
					danger, width, height, startX, startY, goalX, goalY, info.AirRouteThreatPenalty);
				if (route == null)
					continue;

				var exposure = route.Sum(p => danger[p.Y * width + p.X]) * info.AirRouteThreatPenalty;
				var travel = route.Count * coarseSize * info.AirTargetDistancePenalty *
					info.AirTargetReferenceSpeed / (float)Math.Max(1, aircraftSpeed);
				var cost = exposure + travel;
				if (cost >= bestCost)
					continue;

				best = candidate;
				bestCost = cost;
				bestRoute = AirThreatGeometry.SmoothCoarseRoute(
					danger, width, height, startX, startY, route)
					.Select(p => map.Clamp(new CPos(
						p.X * coarseSize + coarseSize / 2, p.Y * coarseSize + coarseSize / 2))).ToList();
			}

			return (best, bestRoute);
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

			var adaptiveSquadStrength = owner.Units.Count * owner.SquadManager.AirRiskMultiplier(owner.AirProfile);
			if (CountAntiAirUnits(unitsAroundPos) * owner.SquadManager.Info.AirThreatFleeMultiplier < adaptiveSquadStrength)
			{
				detectedEnemyTarget = unitsAroundPos.Random(owner.Random);
				return true;
			}

			return false;
		}

		// Checks the number of anti air enemies around units
		protected virtual bool ShouldFlee(Squad owner)
		{
			return ShouldFlee(owner, enemies => CountAntiAirUnits(enemies) * MissileUnitMultiplier > owner.Units.Count);
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
			if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Air state [{0}] attack tick: units={1} target-valid={2} routed-units={3}.",
					owner.AirProfile, owner.Units.Count, owner.IsTargetValid, owner.AirRouteAssignedUnits.Count);

			var hasArmedUnit = owner.Units.Any(a => !owner.AirUnitsRepairing.Contains(a.ActorID) &&
				HasAmmo(a.TraitsImplementing<AmmoPool>()));
			var anyUnitBusy = owner.Units.Any(a => !owner.AirUnitsRepairing.Contains(a.ActorID) &&
				(BusyAttack(a) || !a.IsIdle));
			var routeTraveling = owner.Units.Any(a => owner.AirRouteAssignedUnits.Contains(a.ActorID) &&
				!a.IsIdle && !BusyAttack(a));
			var routeExpired = owner.World.WorldTick - owner.AirRoutePlanTick >= info.AirTargetRouteTimeoutTicks;
			var ticksSinceProgress = owner.World.WorldTick - owner.AirTargetLastProgressTick;
			var makingProgress = ticksSinceProgress < info.AirTargetCommitmentTicks;

			if (owner.IsTargetValid && owner.World.WorldTick >= owner.AirNextTargetReviewTick)
			{
				owner.AirNextTargetReviewTick = owner.World.WorldTick + info.AirInfluenceCacheInterval;
				if (owner.TargetActor.Info.HasTraitInfo<BuildingInfo>() && hasArmedUnit && !makingProgress)
				{
					var incumbent = owner.TargetActor;
					var challenger = FindBestAirTarget(owner);
					var switchTarget = challenger != null && challenger.Actor != incumbent &&
						AirThreatGeometry.ShouldSwitchTarget(false, owner.AirTargetIsUndefended,
							owner.AirTargetScore, true, challenger.IsUndefended, challenger.Score,
							info.AirTargetSwitchImprovementPercent);
					if (switchTarget)
					{
						if (info.AirTargetDebugLogging)
							Log.Write("debug", "Air target [{0}] switching building {1}#{2} score={3} to {4}#{5} score={6}: improvement threshold={7}%.",
								owner.AirProfile, incumbent.Info.Name, incumbent.ActorID, owner.AirTargetScore,
								challenger.Actor.Info.Name, challenger.Actor.ActorID, challenger.Score,
								info.AirTargetSwitchImprovementPercent);

						ApplyAirTargetPlan(owner, challenger);
					}
					else if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air target [{0}] retaining building {1}#{2}: challenger={3} current-score={4} challenger-score={5}.",
							owner.AirProfile, incumbent.Info.Name, incumbent.ActorID,
							challenger == null ? "none" : challenger.Actor.Info.Name + "#" + challenger.Actor.ActorID,
							owner.AirTargetScore, challenger?.Score ?? int.MinValue);
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
						(owner.TargetActor.CenterPosition - owner.CenterPosition).Length / 1024;
					owner.AirTargetLastHP = owner.TargetActor.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue;
				}
				else if (currentCell != owner.AirTargetStrategicCell.Value)
				{
					var previousCell = owner.AirTargetStrategicCell.Value;
					owner.AirTargetStrategicCell = currentCell;
					var committed = makingProgress;
					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air target [{0}] {1}#{2} moved strategic cell {3}->{4}; committed={5} progress-age={6} routed-units={7}.",
							owner.AirProfile, owner.TargetActor.Info.Name, owner.TargetActor.ActorID,
							previousCell, currentCell, committed,
							ticksSinceProgress, owner.AirRouteAssignedUnits.Count);

					if (!committed)
					{
						var incumbent = owner.TargetActor;
						var challenger = FindBestAirTarget(owner);
						var accept = challenger != null && (challenger.Actor == incumbent ||
							AirThreatGeometry.ShouldSwitchTarget(false, owner.AirTargetIsUndefended,
								owner.AirTargetScore, true, challenger.IsUndefended, challenger.Score,
								info.AirTargetSwitchImprovementPercent));
						if (accept)
						{
							if (info.AirTargetDebugLogging)
								Log.Write("debug", "Air target [{0}] cell-change replan accepted: {1}#{2}->{3}#{4} score={5}.",
									owner.AirProfile, incumbent.Info.Name, incumbent.ActorID,
									challenger.Actor.Info.Name, challenger.Actor.ActorID, challenger.Score);

							ApplyAirTargetPlan(owner, challenger);
						}
						else if (info.AirTargetDebugLogging)
							Log.Write("debug", "Air target [{0}] cell-change replan rejected; retaining {1}#{2} score={3}, challenger={4} score={5}.",
								owner.AirProfile, incumbent.Info.Name, incumbent.ActorID, owner.AirTargetScore,
								challenger == null ? "none" : challenger.Actor.Info.Name + "#" + challenger.Actor.ActorID,
								challenger?.Score ?? int.MinValue);
					}
				}
				else
				{
					var distanceCells = (owner.TargetActor.CenterPosition - owner.CenterPosition).Length / 1024;
					var targetHP = owner.TargetActor.TraitOrDefault<IHealth>()?.HP ?? int.MaxValue;
					if (distanceCells + 1 < owner.AirTargetLastDistanceCells || targetHP < owner.AirTargetLastHP)
					{
						owner.AirTargetLastProgressTick = owner.World.WorldTick;
						owner.AirTargetLastDistanceCells = distanceCells;
						owner.AirTargetLastHP = targetHP;
					}
					else if (AirThreatGeometry.ShouldRescanStalledTarget(
						owner.World.WorldTick - owner.AirTargetLastProgressTick, info.AirTargetStallTicks,
						routeTraveling && !routeExpired, anyUnitBusy && !routeExpired, hasArmedUnit))
					{
						if (info.AirTargetDebugLogging)
							Log.Write("debug", "Air target [{0}] {1}#{2} genuinely stalled for {3} ticks at distance {4}; rescanning.",
								owner.AirProfile, owner.TargetActor.Info.Name, owner.TargetActor.ActorID,
								owner.World.WorldTick - owner.AirTargetLastProgressTick, distanceCells);

						owner.TargetActor = null;
						owner.AirTargetStrategicCell = null;
						owner.AirRoute.Clear();
						owner.AirRouteAssignedUnits.Clear();
					}
				}
			}

			if (!owner.IsTargetValid)
			{
				owner.AirTargetStrategicCell = null;
				var nextTarget = FindBestAirTarget(owner);
				if (nextTarget == null && !owner.Units.Any(a =>
					!owner.AirUnitsRepairing.Contains(a.ActorID) && HasAmmo(a.TraitsImplementing<AmmoPool>())))
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

			// Route assignment belongs to each aircraft. Existing and newly produced squad members each
			// receive a safe route from their own current position, so one aircraft that remains active can
			// never leave the rest of a large squad permanently latched behind a squad-wide queued flag.
			owner.AirRouteAssignedUnits.RemoveWhere(id => !owner.Units.Any(a => a.ActorID == id));
			foreach (var a in owner.Units)
			{
				if (owner.AirRouteAssignedUnits.Contains(a.ActorID) || SendHomeToRepair(owner, a))
					continue;

				var route = SafeRouteForAircraft(owner, a, owner.TargetActor);
				owner.AirRouteAssignedUnits.Add(a.ActorID);
				if (route.Count <= 1)
					continue;

				var queued = false;
				foreach (var waypoint in route)
				{
					owner.Bot.QueueOrder(new Order("Move", a, Target.FromCell(owner.World, waypoint), queued));
					queued = true;
				}

				if (CanAttackTarget(a, owner.TargetActor))
				{
					owner.Bot.QueueOrder(new Order("Attack", a, Target.FromActor(owner.TargetActor), true));
					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air order [{0}] {1}#{2}: queued individual safe route ({3} waypoints) to {4}#{5}.",
							owner.AirProfile, a.Info.Name, a.ActorID, route.Count,
							owner.TargetActor.Info.Name, owner.TargetActor.ActorID);
				}
			}

			// Lazily computed: only needed if a self-reloading aircraft actually turns out to be dry,
			// which is the uncommon case, and shared across every unit that needs it this tick rather
			// than recomputed (and drawing fresh jitter from World.LocalRandom) per unit.
			CPos? disengageDestination = null;

			foreach (var a in owner.Units)
			{
				if (owner.AirRouteAssignedUnits.Contains(a.ActorID) && !a.IsIdle && !BusyAttack(a))
					continue;

				if (BusyAttack(a))
					continue;

				if (SendHomeToRepair(owner, a))
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

			Evade(owner, "flee-state continuation");

			// Straight back to idle: the next scan - whichever of the state machine or the much faster
			// safety check gets there first - re-targets from wherever the hop put us.
			owner.FuzzyStateMachine.ChangeState(owner, new AirIdleState(), true);
		}

		public void Deactivate(Squad owner) { }
	}
}
