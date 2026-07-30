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

		protected static Actor FindDefenselessTarget(Squad owner)
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
		protected static Actor FindBestAirTarget(Squad owner, bool relaxed = false)
		{
			var map = owner.World.Map;
			var info = owner.SquadManager.Info;
			var coarseSize = info.AirInfluenceCellSize;
			var width = (map.MapSize.X + coarseSize - 1) / coarseSize;
			var height = (map.MapSize.Y + coarseSize - 1) / coarseSize;
			var archetypePriority = ArchetypePriority(owner);
			var apache = owner.AirProfile.Equals("Apache", StringComparison.OrdinalIgnoreCase);
			var orca = owner.AirProfile.Equals("Orca", StringComparison.OrdinalIgnoreCase);
			var combatUnits = owner.Units.Where(a => !owner.AirUnitsRepairing.Contains(a.ActorID) &&
				HasAmmo(a.TraitsImplementing<AmmoPool>())).ToList();
			if (combatUnits.Count == 0)
			{
				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air target [{0}] scan stopped: no armed non-repairing aircraft in squad of {1}.",
						owner.AirProfile, owner.Units.Count);

				return null;
			}

			var squadSpeed = combatUnits.Select(a => a.Info.TraitInfoOrDefault<AircraftInfo>())
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
						var minX = Math.Max(0, (actor.Location.X - range) / coarseSize);
						var maxX = Math.Min(width - 1, (actor.Location.X + range) / coarseSize);
						var minY = Math.Max(0, (actor.Location.Y - range) / coarseSize);
						var maxY = Math.Min(height - 1, (actor.Location.Y + range) / coarseSize);
						var weight = profile.Weight;
						if (apache && profile.Weight >= .75f)
							weight *= 4f;
						else if (orca && AirThreatGeometry.CanOutrun(squadSpeed, profile.FastestProjectileSpeed))
							weight *= .2f;

						rebuiltThreats.Add((actor, weight, range));
						for (var y = minY; y <= maxY; y++)
							for (var x = minX; x <= maxX; x++)
							{
								var cell = new CPos(x * coarseSize + coarseSize / 2, y * coarseSize + coarseSize / 2);
								var distance = (map.CenterOfCell(map.Clamp(cell)) - actor.CenterPosition).Length / 1024;
								if (distance <= range)
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

			var planningCenter = combatUnits.Select(a => a.CenterPosition).Average();
			var startCell = map.CellContaining(planningCenter);
			var startX = Math.Clamp(startCell.X / coarseSize, 0, width - 1);
			var startY = Math.Clamp(startCell.Y / coarseSize, 0, height - 1);
			var riskScale = Math.Max(1f, combatUnits.Count / 3f);
			var bestScore = int.MinValue;
			Actor best = null;
			List<CPos> bestRoute = null;

			var liveCandidates = candidates.Where(c => !c.Actor.IsDead).OrderBy(c => c.Actor.ActorID).ToList();
			var candidateIndices = AirThreatGeometry.SelectTargetCandidates(
				liveCandidates.Select(c => (c.Actor.CenterPosition - planningCenter).LengthSquared).ToList(),
				liveCandidates.Select(c => c.Utility).ToList(),
				info.AirTargetClosestCandidates, info.AirTargetHighestValueCandidates);

			foreach (var candidateIndex in candidateIndices)
			{
				var candidate = liveCandidates[candidateIndex];
				var goalX = Math.Clamp(candidate.Actor.Location.X / coarseSize, 0, width - 1);
				var goalY = Math.Clamp(candidate.Actor.Location.Y / coarseSize, 0, height - 1);
				var route = AirThreatGeometry.FindCoarseRoute(danger, width, height, startX, startY, goalX, goalY,
					info.AirRouteThreatPenalty / riskScale);
				if (route == null)
				{
					if (info.AirTargetDebugLogging)
						Log.Write("debug", "Air target [{0}] {1}#{2} rejected: no coarse route.",
							owner.AirProfile, candidate.Actor.Info.Name, candidate.Actor.ActorID);

					continue;
				}

				var exposureCost = route.Sum(p => danger[p.Y * width + p.X]) * info.AirRouteThreatPenalty / riskScale;
				var destinationDanger = 0f;
				foreach (var threat in threats)
				{
					if (threat.Actor.IsDead)
						continue;

					var distance = (threat.Actor.CenterPosition - candidate.Actor.CenterPosition).Length / 1024;
					if (distance <= threat.RangeCells)
						destinationDanger += threat.Weight;
				}

				var distanceCost = route.Count * coarseSize * info.AirTargetDistancePenalty;
				var stoppingCost = (int)(destinationDanger * info.AirTargetAntiAirPenalty / riskScale);

				// Each independent liability scales the target value down instead of participating in a
				// binary veto. This preserves meaningful tradeoffs: speed can justify a distant harvester,
				// while route and destination AA still compound to make defended targets unattractive.
				var score = (long)candidate.Utility;
				score = score * 1024 / Math.Max(1, 1024 + distanceCost);
				score = score * 1024 / Math.Max(1, 1024 + (int)exposureCost);
				score = score * 1024 / Math.Max(1, 1024 + stoppingCost);
				var finalScore = (int)Math.Clamp(score, int.MinValue, int.MaxValue);

				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air target [{0}] {1}#{2}: utility={3} route={4} exposure={5:0.##} destination-danger={6:0.##} score={7} relaxed={8}",
						owner.AirProfile, candidate.Actor.Info.Name, candidate.Actor.ActorID, candidate.Utility,
						route.Count, exposureCost, destinationDanger, finalScore, relaxed);

				if (best == null || finalScore > bestScore)
				{
					best = candidate.Actor;
					bestScore = finalScore;
					bestRoute = AirThreatGeometry.SmoothCoarseRoute(danger, width, height, startX, startY, route)
						.Select(p => map.Clamp(new CPos(p.X * coarseSize + coarseSize / 2, p.Y * coarseSize + coarseSize / 2))).ToList();
				}
			}

			if (best == null || (!relaxed && bestScore < info.AirTargetMinimumScore))
			{
				if (info.AirTargetDebugLogging)
					Log.Write("debug", "Air target [{0}] scan found no eligible target: candidates={1} best-score={2} minimum={3} relaxed={4}.",
						owner.AirProfile, candidateIndices.Count, bestScore, info.AirTargetMinimumScore, relaxed);

				return null;
			}

			if (info.AirTargetDebugLogging)
				Log.Write("debug", "Air target [{0}] selected {1}#{2}: score={3} waypoints={4} relaxed={5}",
					owner.AirProfile, best.Info.Name, best.ActorID, bestScore, bestRoute?.Count ?? 0, relaxed);

			owner.AirRoute.Clear();
			owner.AirRouteQueued = false;
			if (bestRoute != null)
				owner.AirRoute.AddRange(bestRoute);
			return best;
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

					if (withinRange && !outrunnable)
						antiAirWeight += profile.Weight;

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

			// Over our own base there is nowhere safer to run to, and our own defences are the answer.
			if (!ownBuildingNear && AirThreatGeometry.ShouldFleeAntiAir(antiAirWeight, info.AirThreatFleeMultiplier, owner.Units.Count))
			{
				// Drop the target so the squad re-evaluates from scratch once it is clear; the threat we
				// just remembered will make the route back through here look expensive.
				// The state change happens even when Evade declines to re-order (rate limit), so the squad
				// cannot keep pressing an attack run it has already decided to abandon.
				owner.TargetActor = null;
				Evade(owner);
				owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), true);
				return;
			}

			// Only ever commit to a local target when this scan saw no anti-air at all within
			// AirThreatScanRadius, so the fast path can never walk the squad into cover it just measured.
			if (antiAirWeight > 0 || localTarget == null || localScore < info.AirTargetMinimumScore || owner.IsTargetValid)
				return;

			owner.TargetActor = localTarget;
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
		protected static void Evade(Squad owner)
		{
			var info = owner.SquadManager.Info;
			var tick = owner.World.WorldTick;

			// An air squad sitting in anti-air cover must not re-issue move orders on every safety check.
			if (tick < owner.NextAirRetreatTick)
				return;

			owner.NextAirRetreatTick = tick + info.AirRetreatOrderInterval;

			if (info.AirEvadeDistance <= 0)
			{
				Retreat(owner);
				return;
			}

			var destination = EvadeDestination(owner);
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

			var nearest = FindNearestRepairBuilding(owner, a, repairable);
			if (nearest == null)
				return repairing;

			owner.Bot.QueueOrder(new Order("Repair", a, Target.FromActor(nearest), false));
			owner.AirUnitsRepairing.Add(a.ActorID);
			return true;
		}

		static Actor FindNearestRepairBuilding(Squad owner, Actor aircraft, RepairableInfo repairable)
		{
			Actor nearest = null;
			var nearestDistanceSquared = long.MaxValue;

			// PERF: bounded by the number of the bot's own repair-capable buildings, not a world query.
			foreach (var b in owner.World.ActorsHavingTrait<RepairsUnits>())
			{
				if (b.Owner != owner.Bot.Player || !repairable.RepairActors.Contains(b.Info.Name))
					continue;

				long dx = b.CenterPosition.X - aircraft.CenterPosition.X;
				long dy = b.CenterPosition.Y - aircraft.CenterPosition.Y;
				var d = dx * dx + dy * dy;
				if (nearest == null || d < nearestDistanceSquared)
				{
					nearest = b;
					nearestDistanceSquared = d;
				}
			}

			return nearest;
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

			if (CountAntiAirUnits(unitsAroundPos) * owner.SquadManager.Info.AirThreatFleeMultiplier < owner.Units.Count)
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
				var nearbyTarget = FindClosestAttackableEnemy(owner);
				if (nearbyTarget != null)
				{
					if (owner.SquadManager.Info.AirTargetDebugLogging)
						Log.Write("debug", "Air target [{0}] strategic scan failed; immediately using closest {1}#{2}.",
							owner.AirProfile, nearbyTarget.Info.Name, nearbyTarget.ActorID);

					owner.AirConsecutiveNoTargetScans = 0;
					owner.AirRoute.Clear();
					owner.AirRouteQueued = false;
					owner.TargetActor = nearbyTarget;
					owner.FuzzyStateMachine.ChangeState(owner, new AirAttackState(), true);
					return;
				}

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
							owner.TargetActor = massedTarget;
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
					Evade(owner);

				return;
			}

			owner.AirConsecutiveNoTargetScans = 0;
			owner.TargetActor = e;
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

			if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Air state [{0}] attack tick: units={1} target-valid={2} route-queued={3}.",
					owner.AirProfile, owner.Units.Count, owner.IsTargetValid, owner.AirRouteQueued);

			if (!owner.IsTargetValid)
			{
				var nextTarget = FindBestAirTarget(owner);
				if (nextTarget == null)
					nextTarget = FindClosestAttackableEnemy(owner);

				if (nextTarget == null)
				{
					owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), true);
					return;
				}

				owner.TargetActor = nextTarget;
			}

			if (owner.AirProfile == "Generic" && !NearToPosSafely(owner, owner.TargetActor.CenterPosition))
			{
				owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), true);
				return;
			}

			// The engine already supports shift-queued movement. Submit the complete planned route in one
			// pass instead of waiting for a strategic tick at each coarse cell, then queue the attack behind
			// it. This keeps the influence grid a planning detail rather than a series of visible pauses.
			if (owner.AirRoute.Count > 1 && !owner.AirRouteQueued)
			{
				foreach (var a in owner.Units)
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
					{
						owner.Bot.QueueOrder(new Order("Attack", a, Target.FromActor(owner.TargetActor), true));
						if (owner.SquadManager.Info.AirTargetDebugLogging)
							Log.Write("debug", "Air order [{0}] {1}#{2}: queued route attack on {3}#{4}.",
								owner.AirProfile, a.Info.Name, a.ActorID, owner.TargetActor.Info.Name, owner.TargetActor.ActorID);
					}
					else if (owner.SquadManager.Info.AirTargetDebugLogging)
						Log.Write("debug", "Air order [{0}] {1}#{2}: cannot attack selected {3}#{4}.",
							owner.AirProfile, a.Info.Name, a.ActorID, owner.TargetActor.Info.Name, owner.TargetActor.ActorID);
				}

				owner.AirRouteQueued = true;
				owner.AirRoute.Clear();
				return;
			}

			// A queued route belongs to each aircraft, not to the squad as a blocking transaction. One
			// aircraft may still be flying while another has finished or become idle; only the former waits.
			if (owner.AirRouteQueued &&
				!owner.Units.Any(a => !owner.AirUnitsRepairing.Contains(a.ActorID) && !a.IsIdle))
				owner.AirRouteQueued = false;

			// Lazily computed: only needed if a self-reloading aircraft actually turns out to be dry,
			// which is the uncommon case, and shared across every unit that needs it this tick rather
			// than recomputed (and drawing fresh jitter from World.LocalRandom) per unit.
			CPos? disengageDestination = null;

			foreach (var a in owner.Units)
			{
				if (owner.AirRouteQueued && !a.IsIdle)
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
					// break off from the target while ammo passively recharges in the field, instead of
					// continuing to issue Attack orders it cannot act on.
					if (disengageDestination == null)
						disengageDestination = EvadeDestination(owner);

					owner.Bot.QueueOrder(new Order("Move", a, Target.FromCell(owner.World, disengageDestination.Value), false));
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

			Evade(owner);

			// Straight back to idle: the next scan - whichever of the state machine or the much faster
			// safety check gets there first - re-targets from wherever the hop put us.
			owner.FuzzyStateMachine.ChangeState(owner, new AirIdleState(), true);
		}

		public void Deactivate(Squad owner) { }
	}
}
