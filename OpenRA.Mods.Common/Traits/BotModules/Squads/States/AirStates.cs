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

		protected static int CountAntiAirUnits(IEnumerable<Actor> units)
		{
			if (!units.Any())
				return 0;

			var missileUnitsCount = 0;
			foreach (var unit in units)
			{
				if (unit == null || unit.Info.HasTraitInfo<AircraftInfo>())
					continue;

				foreach (var ab in unit.TraitsImplementing<AttackBase>())
				{
					if (ab.IsTraitDisabled || ab.IsTraitPaused)
						continue;

					foreach (var a in ab.Armaments)
					{
						if (a.Weapon.IsValidTarget(AirTargetTypes))
						{
							missileUnitsCount++;
							break;
						}
					}
				}
			}

			return missileUnitsCount;
		}

		enum AirTargetClass { Unit, Building, Production, Harvester }

		protected static Actor FindDefenselessTarget(Squad owner)
		{
			return FindBestAirTarget(owner);
		}

		/// <summary>
		/// Samples a bounded number of grid cells across the map, scores every enemy actor found in them
		/// and returns the most attractive one. Soft economic and production targets outrank generic units,
		/// while anything sitting under enemy anti-air cover is penalised heavily and usually rejected outright.
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

			Actor bestTarget = null;
			var bestScore = int.MinValue;

			// PERF: Reused across every sample so the scan allocates twice per call rather than twice per sample.
			var actorsAround = new List<Actor>();
			var candidates = new List<Actor>();

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
				actorsAround.AddRange(owner.World.FindActorsInCircle(map.CenterOfCell(pos), scanRadius));

				// PERF: Avoid LINQ.
				foreach (var a in actorsAround)
					if (owner.SquadManager.IsPreferredEnemyUnit(a))
						candidates.Add(a);

				if (candidates.Count == 0)
					continue;

				var antiAirPenalty = CountAntiAirUnits(candidates) * info.AirTargetAntiAirPenalty;

				foreach (var a in candidates)
				{
					if (!owner.SquadManager.IsNotHiddenUnit(a))
						continue;

					var distanceInCells = (a.CenterPosition - squadCenter).Length / 1024;
					var score = TargetValue(a, info) - antiAirPenalty - distanceInCells * info.AirTargetDistancePenalty;

					if (score > bestScore)
					{
						bestScore = score;
						bestTarget = a;
					}
				}
			}

			if (bestScore < info.AirTargetMinimumScore)
				return null;

			return bestTarget;
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

			if (CountAntiAirUnits(unitsAroundPos) * MissileUnitMultiplier < owner.Units.Count)
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

			if (ShouldFlee(owner))
			{
				owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), true);
				return;
			}

			var e = FindDefenselessTarget(owner);
			if (e == null)
				return;

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

			foreach (var a in owner.Units)
			{
				var ammoPools = a.TraitsImplementing<AmmoPool>().ToArray();
				if (!ReloadsAutomatically(ammoPools, a.TraitOrDefault<Rearmable>()) && !FullAmmo(ammoPools))
				{
					if (IsRearming(a))
						continue;

					owner.Bot.QueueOrder(new Order("ReturnToBase", a, false));
					continue;
				}

				owner.Bot.QueueOrder(new Order("Move", a, Target.FromCell(owner.World, RandomBuildingLocation(owner)), false));
			}

			owner.FuzzyStateMachine.ChangeState(owner, new AirIdleState(), true);
		}

		public void Deactivate(Squad owner) { }
	}
}
