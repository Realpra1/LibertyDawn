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

		/// <summary>A finalist from one sampled grid cell: the best actor there and its pre-route score.</summary>
		struct AirTargetCandidate
		{
			public Actor Actor;
			public int PartialScore;
		}

		protected static Actor FindDefenselessTarget(Squad owner)
		{
			return FindBestAirTarget(owner);
		}

		/// <summary>
		/// Samples a bounded number of grid cells across the map, scores every enemy actor it finds and
		/// returns the most attractive one. Soft mobile targets are meant to beat structures outright:
		/// aircraft do poor damage to buildings, so harvesters and undefended units carry both a higher
		/// class value and a "cannot shoot back" bonus. Anti-air on top of a candidate is penalised, and
		/// so is anti-air anywhere along the straight line the squad would fly to reach it - the squad
		/// should not have to fly through a SAM belt to reach an undefended harvester.
		/// A defender only counts toward a cell's anti-air weight when this specific cell is within
		/// AirThreatRangeBuffer times its own AA weapon's range - two defenders found by the same flat scan
		/// are not equally dangerous just because the scan does not know their range.
		/// With <paramref name="relaxed"/> the anti-air and route-threat penalties are dropped entirely and
		/// the AirTargetMinimumScore floor is skipped: this is the massed-attack fallback (see
		/// SquadManagerBotModuleInfo.AirMassedAttackIdleThreshold) for when nothing scores acceptably and the
		/// squad has given up waiting for something undefended to show up.
		/// Returns null when nothing scores above <see cref="SquadManagerBotModuleInfo.AirTargetMinimumScore"/>
		/// (not relaxed), or when there is nothing to attack at all (relaxed).
		/// </summary>
		protected static Actor FindBestAirTarget(Squad owner, bool relaxed = false)
		{
			var map = owner.World.Map;
			var info = owner.SquadManager.Info;
			var dangerRadius = info.DangerScanRadius;
			var archetypePriority = ArchetypePriority(owner);

			var columnCount = (map.MapSize.X + dangerRadius - 1) / dangerRadius;
			var rowCount = (map.MapSize.Y + dangerRadius - 1) / dangerRadius;
			var cellCount = columnCount * rowCount;
			if (cellCount <= 0)
				return null;

			var squadCenter = owner.CenterPosition;
			var scanRadius = WDist.FromCells(dangerRadius);

			// PERF: Reused across every sample so the scan allocates a handful of lists per call
			// rather than a handful per sample.
			var actorsAround = new List<Actor>();
			var candidates = new List<Actor>();

			// PERF: parallel to candidates, so IsAntiAirCapable (a trait lookup) runs once per actor.
			var candidateIsAntiAir = new List<bool>();
			var finalists = new List<AirTargetCandidate>();

			// Anti-air we know about: whatever this scan happens to uncover, plus whatever the squad
			// has personally run into recently. Used to price the flight path, not the destination.
			// Sampled grid circles overlap, so sightings are merged by position - otherwise one SAM
			// found by three samples would be charged to the route three times.
			var threats = new List<WPos>();
			var threatMergeSquared = (long)WDist.FromCells(info.AirThreatMemoryMergeRadius).Length * WDist.FromCells(info.AirThreatMemoryMergeRadius).Length;
			var threatLimit = info.AirTargetScanSamples + info.AirThreatMemorySize;
			owner.ForgetExpiredAirThreats(owner.World.WorldTick);
			threats.AddRange(owner.AirThreatPositions);

			// PERF: Sampling a fixed number of grid cells keeps the cost of this scan independent of map size.
			// The scan repeats every AttackForceInterval ticks, so over time the whole map still gets covered.
			var samples = Math.Min(info.AirTargetScanSamples, cellCount);
			for (var s = 0; s < samples; s++)
			{
				// NOTE: Bot code runs on the host only and must never touch World.SharedRandom.
				var i = owner.Random.Next(cellCount);
				var pos = new MPos((i % columnCount) * dangerRadius + dangerRadius / 2, (i / columnCount) * dangerRadius + dangerRadius / 2).ToCPos(map);

				actorsAround.Clear();
				candidates.Clear();
				candidateIsAntiAir.Clear();
				var cellCenter = map.CenterOfCell(pos);
				actorsAround.AddRange(owner.World.FindActorsInCircle(cellCenter, scanRadius));

				// PERF: Avoid LINQ.
				var antiAirWeight = 0f;
				foreach (var a in actorsAround)
				{
					if (!owner.SquadManager.IsPreferredEnemyUnit(a))
						continue;

					var profile = AntiAirProfile(a);
					var antiAir = profile.Weight > 0;
					candidates.Add(a);
					candidateIsAntiAir.Add(antiAir);

					if (antiAir)
					{
						AddDistinctThreat(threats, a.CenterPosition, threatMergeSquared, threatLimit);

						// "Every actor in the cell shares the same anti-air cover" (see below) already
						// approximates position within the cell, so the buffered-range check is made
						// against the cell centre rather than the eventual winning candidate.
						var distanceToCellCenter = (a.CenterPosition - cellCenter).Length / 1024;
						if (!relaxed && AirThreatGeometry.IsWithinBufferedRange(distanceToCellCenter, profile.RangeCells, info.AirThreatRangeBuffer))
							antiAirWeight += profile.Weight;
					}
				}

				if (candidates.Count == 0)
					continue;

				// Only the best actor in this cell can win overall, and every actor in the cell shares
				// the same anti-air cover - so keep one finalist per cell and route-check those.
				// PERF: bounds the second pass to AirTargetScanSamples entries.
				Actor bestHere = null;
				var bestPartial = 0;
				for (var c = 0; c < candidates.Count; c++)
				{
					var a = candidates[c];
					if (!owner.SquadManager.IsNotHiddenUnit(a))
						continue;

					var distanceInCells = (a.CenterPosition - squadCenter).Length / 1024;
					var partial = AirThreatGeometry.TargetScore(
						TargetValue(a, info, archetypePriority), !candidateIsAntiAir[c], info.AirTargetDefencelessBonus,
						antiAirWeight, info.AirTargetAntiAirPenalty,
						0, 0,
						distanceInCells, info.AirTargetDistancePenalty);

					if (bestHere == null || partial > bestPartial)
					{
						bestHere = a;
						bestPartial = partial;
					}
				}

				if (bestHere != null)
					finalists.Add(new AirTargetCandidate { Actor = bestHere, PartialScore = bestPartial });
			}

			// Second pass: charge each finalist for the anti-air it would have to fly past on the way.
			// PERF: pure arithmetic over at most AirTargetScanSamples x (samples + memory) pairs, no world queries.
			var corridorRadius = WDist.FromCells(info.AirRouteThreatRadius);
			var destinationExclusion = scanRadius;

			Actor bestTarget = null;
			var bestScore = int.MinValue;
			foreach (var f in finalists)
			{
				var score = f.PartialScore;
				if (!relaxed && info.AirRouteThreatPenalty != 0)
				{
					var routeThreats = AirThreatGeometry.CountThreatsNearRoute(
						threats, squadCenter, f.Actor.CenterPosition, corridorRadius, destinationExclusion);
					score -= routeThreats * info.AirRouteThreatPenalty;
				}

				if (bestTarget == null || score > bestScore)
				{
					bestScore = score;
					bestTarget = f.Actor;
				}
			}

			if (bestTarget == null || (!relaxed && bestScore < info.AirTargetMinimumScore))
				return null;

			return bestTarget;
		}

		/// <summary>
		/// Appends a threat position unless an equivalent one is already listed, and never grows the list
		/// past <paramref name="limit"/> so the route check stays O(finalists x limit).
		/// </summary>
		static void AddDistinctThreat(List<WPos> threats, WPos pos, long mergeSquared, int limit)
		{
			if (threats.Count >= limit)
				return;

			for (var i = 0; i < threats.Count; i++)
			{
				long dx = threats[i].X - pos.X;
				long dy = threats[i].Y - pos.Y;
				if (dx * dx + dy * dy <= mergeSquared)
					return;
			}

			threats.Add(pos);
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

			// PERF: Avoid LINQ.
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
			if (health == null || health.HP > health.MaxHP * threshold)
				return false;

			if (IsRearming(a))
				return true;

			var repairable = a.Info.TraitInfoOrDefault<RepairableInfo>();
			if (repairable == null || repairable.RepairActors.Count == 0)
				return false;

			Actor nearest = null;
			var nearestDistanceSquared = long.MaxValue;

			// PERF: bounded by the number of the bot's own repair-capable buildings, not a world query.
			foreach (var b in owner.World.ActorsHavingTrait<RepairsUnits>())
			{
				if (b.Owner != owner.Bot.Player || !repairable.RepairActors.Contains(b.Info.Name))
					continue;

				long dx = b.CenterPosition.X - a.CenterPosition.X;
				long dy = b.CenterPosition.Y - a.CenterPosition.Y;
				var d = dx * dx + dy * dy;
				if (nearest == null || d < nearestDistanceSquared)
				{
					nearest = b;
					nearestDistanceSquared = d;
				}
			}

			if (nearest == null)
				return false;

			owner.Bot.QueueOrder(new Order("Repair", a, Target.FromActor(nearest), false));
			return true;
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
				// Given up waiting for something undefended to show up: force an attack on whatever
				// scores best with the anti-air/route penalties relaxed, rather than idling forever while
				// the whole enemy base sits defended. Disabled (threshold zero) restores stock behaviour.
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

			if (!owner.IsTargetValid)
			{
				// Re-run the scored scan rather than falling back to the closest enemy:
				// the closest enemy is usually the defended base we just flew past.
				var nextTarget = FindBestAirTarget(owner);
				if (nextTarget != null)
					owner.TargetActor = nextTarget;
				else
				{
					owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), true);
					return;
				}
			}

			if (!NearToPosSafely(owner, owner.TargetActor.CenterPosition))
			{
				owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), true);
				return;
			}

			// Lazily computed: only needed if a self-reloading aircraft actually turns out to be dry,
			// which is the uncommon case, and shared across every unit that needs it this tick rather
			// than recomputed (and drawing fresh jitter from World.LocalRandom) per unit.
			CPos? disengageDestination = null;

			foreach (var a in owner.Units)
			{
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
					owner.Bot.QueueOrder(new Order("Attack", a, Target.FromActor(owner.TargetActor), false));
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
