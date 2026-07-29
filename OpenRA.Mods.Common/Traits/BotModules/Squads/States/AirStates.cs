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

		protected static int CountAntiAirUnits(IEnumerable<Actor> units)
		{
			var missileUnitsCount = 0;
			foreach (var unit in units)
				if (IsAntiAirCapable(unit))
					missileUnitsCount++;

			return missileUnitsCount;
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
		/// Returns null when nothing scores above <see cref="SquadManagerBotModuleInfo.AirTargetMinimumScore"/>.
		/// </summary>
		protected static Actor FindBestAirTarget(Squad owner)
		{
			var map = owner.World.Map;
			var info = owner.SquadManager.Info;
			var dangerRadius = info.DangerScanRadius;

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
				actorsAround.AddRange(owner.World.FindActorsInCircle(map.CenterOfCell(pos), scanRadius));

				// PERF: Avoid LINQ.
				var antiAirCount = 0;
				foreach (var a in actorsAround)
				{
					if (!owner.SquadManager.IsPreferredEnemyUnit(a))
						continue;

					var antiAir = IsAntiAirCapable(a);
					candidates.Add(a);
					candidateIsAntiAir.Add(antiAir);

					if (antiAir)
					{
						antiAirCount++;
						AddDistinctThreat(threats, a.CenterPosition, threatMergeSquared, threatLimit);
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
						TargetValue(a, info), !candidateIsAntiAir[c], info.AirTargetDefencelessBonus,
						antiAirCount, info.AirTargetAntiAirPenalty,
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
				if (info.AirRouteThreatPenalty != 0)
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

			if (bestTarget == null || bestScore < info.AirTargetMinimumScore)
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

		static int TargetValue(Actor a, SquadManagerBotModuleInfo info)
		{
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

			var antiAirCount = 0;
			var ownBuildingNear = false;

			Actor localTarget = null;
			var localScore = int.MinValue;

			// PERF: single bounded scan, no intermediate list, no LINQ.
			foreach (var a in owner.World.FindActorsInCircle(squadCenter, WDist.FromCells(info.AirThreatScanRadius)))
			{
				if (a.Owner == owner.Bot.Player)
				{
					if (!ownBuildingNear && a.Info.HasTraitInfo<BuildingInfo>())
						ownBuildingNear = true;

					continue;
				}

				if (!owner.SquadManager.IsPreferredEnemyUnit(a))
					continue;

				if (IsAntiAirCapable(a))
				{
					antiAirCount++;
					owner.RememberAirThreat(a.CenterPosition, expiry, mergeRadius, info.AirThreatMemorySize);
					continue;
				}

				if (!owner.SquadManager.IsNotHiddenUnit(a))
					continue;

				// Everything reaching here failed IsAntiAirCapable, so the defenceless bonus always applies.
				// No anti-air or route penalty: whether this candidate is safe is decided below, by the same
				// scan's anti-air count, rather than per candidate.
				var distanceInCells = (a.CenterPosition - squadCenter).Length / 1024;
				var score = AirThreatGeometry.TargetScore(
					TargetValue(a, info), true, info.AirTargetDefencelessBonus,
					0, 0, 0, 0,
					distanceInCells, info.AirTargetDistancePenalty);

				if (localTarget == null || score > localScore)
				{
					localTarget = a;
					localScore = score;
				}
			}

			// Over our own base there is nowhere safer to run to, and our own defences are the answer.
			if (!ownBuildingNear && AirThreatGeometry.ShouldFleeAntiAir(antiAirCount, info.AirThreatFleeMultiplier, owner.Units.Count))
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
			if (antiAirCount > 0 || localTarget == null || localScore < info.AirTargetMinimumScore || owner.IsTargetValid)
				return;

			owner.TargetActor = localTarget;
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
				if (SendHomeToResupply(owner, a))
					continue;

				owner.Bot.QueueOrder(new Order("Move", a, Target.FromCell(owner.World, destination), false));
			}
		}

		/// <summary>Picks the cell for a local evasion hop and keeps it on the map.</summary>
		static CPos EvadeDestination(Squad owner)
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
		/// Sends the squad home: rearming whoever needs it, and moving everyone else to the one of our
		/// own buildings that sits furthest from the anti-air the squad remembers. Falls back to the
		/// stock random building when nothing is remembered. Only reached when AirEvadeDistance is zero.
		/// </summary>
		protected static void Retreat(Squad owner)
		{
			var destination = SafeRetreatLocation(owner);

			foreach (var a in owner.Units)
			{
				if (SendHomeToResupply(owner, a))
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
				// Nothing worth hitting from where we are standing. If the squad remembers anti-air it is
				// loitering next to an enemy base, so shuffle to a nearby point and try the scan again from
				// there instead of hovering: this is the "if it cannot get there in a straight line, move
				// around the base and try again" half of the loop, done the cheap way.
				if (owner.SquadManager.Info.AirEvadeDistance > 0 && owner.AirThreatPositions.Count > 0)
					Evade(owner);

				return;
			}

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

			foreach (var a in owner.Units)
			{
				if (BusyAttack(a))
					continue;

				var ammoPools = a.TraitsImplementing<AmmoPool>().ToArray();
				if (!ReloadsAutomatically(ammoPools, a.TraitOrDefault<Rearmable>()))
				{
					if (IsRearming(a))
						continue;

					if (!HasAmmo(ammoPools))
					{
						owner.Bot.QueueOrder(new Order("ReturnToBase", a, false));
						continue;
					}
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
