#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is made available under the GNU General Public License
 * version 3 or later. See COPYING for details.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Pure deterministic rules shared by the live transport unload planner and its tests.</summary>
	public static class TransportLandingPolicy
	{
		public static IEnumerable<CPos> OrderedCandidates(IEnumerable<CPos> candidates, CPos objective)
		{
			return candidates.Distinct().OrderBy(c => (c - objective).LengthSquared)
				.ThenBy(c => c.X).ThenBy(c => c.Y).ThenBy(c => c.Layer);
		}

		public static bool DealsPositiveDamage(WeaponInfo weapon, BitSet<TargetableType> targetTypes,
			IEnumerable<string> armorTypes)
		{
			if (weapon == null || !weapon.IsValidTarget(targetTypes))
				return false;

			var armors = armorTypes?.Where(a => !string.IsNullOrEmpty(a)).Distinct().ToArray() ?? Array.Empty<string>();
			foreach (var warhead in weapon.Warheads)
			{
				if (!(warhead is DamageWarhead damage) || damage.Damage <= 0 ||
					!damage.ValidTargets.Overlaps(targetTypes) || damage.InvalidTargets.Overlaps(targetTypes) ||
					!damage.ValidRelationships.HasRelationship(PlayerRelationship.Enemy))
					continue;

				if (DealsPositiveDamage(damage.Damage, damage.Versus, armors))
					return true;
			}

			return false;
		}

		public static bool DealsPositiveDamage(int damage, IReadOnlyDictionary<string, int> versus,
			IEnumerable<string> armorTypes)
		{
			if (damage <= 0)
				return false;

			var armors = armorTypes?.Where(a => !string.IsNullOrEmpty(a)).Distinct().ToArray() ?? Array.Empty<string>();
			return armors.Length == 0 || armors.Any(armor => !versus.TryGetValue(armor, out var modifier) || modifier > 0);
		}

		public static string EncodeExactExits(IEnumerable<KeyValuePair<uint, CPos>> exits)
		{
			return string.Join(";", exits.OrderBy(e => e.Key).Select(e =>
				e.Key.ToString(CultureInfo.InvariantCulture) + ":" + e.Value.Bits.ToString(CultureInfo.InvariantCulture)));
		}

		public static bool TryDecodeExactExits(string encoded, out Dictionary<uint, CPos> exits)
		{
			exits = new Dictionary<uint, CPos>();
			if (string.IsNullOrEmpty(encoded))
				return false;

			foreach (var pair in encoded.Split(';'))
			{
				var separator = pair.IndexOf(':');
				if (separator <= 0 || separator == pair.Length - 1 ||
					!uint.TryParse(pair.Substring(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var actorId) ||
					!int.TryParse(pair.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bits) ||
					exits.ContainsKey(actorId))
				{
					exits.Clear();
					return false;
				}

				exits.Add(actorId, new CPos(bits));
			}

			return exits.Count > 0;
		}
	}

	public sealed class TransportUnloadPlan
	{
		public readonly CPos Objective;
		public readonly CPos CarrierCell;
		public readonly uint[] PassengerIds;
		public readonly CPos[] ExitCells;
		public readonly int Revision;
		public readonly int SnapshotTick;
		public readonly int CandidatesEvaluated;
		public readonly string FirstRejection;
		public readonly string FirstThreatRejection;

		public TransportUnloadPlan(CPos objective, CPos carrierCell, uint[] passengerIds, CPos[] exitCells,
			int revision, int snapshotTick, int candidatesEvaluated, string firstRejection,
			string firstThreatRejection)
		{
			Objective = objective;
			CarrierCell = carrierCell;
			PassengerIds = passengerIds;
			ExitCells = exitCells;
			Revision = revision;
			SnapshotTick = snapshotTick;
			CandidatesEvaluated = candidatesEvaluated;
			FirstRejection = firstRejection;
			FirstThreatRejection = firstThreatRejection;
		}
	}

	sealed class TransportThreatSnapshot
	{
		static readonly BitSet<TargetableType> CarrierTargetTypes =
			new BitSet<TargetableType>("Air", "Ground", "Vehicle");

		sealed class Threat
		{
			public Actor Actor;
			public Armament Armament;
			public WDist Range;
			public WDist Buffer;
			public WDist MobileMargin;
		}

		readonly World world;
		readonly TransportManagerBotModuleInfo info;
		readonly List<Threat> threats = new List<Threat>();
		readonly float[] coarseDanger;
		readonly int coarseWidth;
		readonly int coarseHeight;

		public readonly int Tick;
		public int ThreatCount => threats.Count;

		public TransportThreatSnapshot(World world, Player player, TransportManagerBotModuleInfo info)
		{
			this.world = world;
			this.info = info;
			Tick = world.WorldTick;

			foreach (var actor in world.Actors.Where(a => a != null && !a.IsDead && a.IsInWorld &&
				player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy).OrderBy(a => a.ActorID))
			{
				var seen = new HashSet<Armament>();
				foreach (var attack in actor.TraitsImplementing<AttackBase>())
				{
					if (attack.IsTraitDisabled || attack.IsTraitPaused)
						continue;

					foreach (var armament in attack.Armaments)
					{
						if (!seen.Add(armament) || armament.IsTraitDisabled || armament.IsTraitPaused ||
							!TransportLandingPolicy.DealsPositiveDamage(armament.Weapon, CarrierTargetTypes, Array.Empty<string>()))
							continue;

						var mobile = actor.TraitOrDefault<Mobile>();
						var movement = mobile == null || mobile.IsTraitDisabled || mobile.IsTraitPaused ? 0 :
							Math.Max(0, mobile.MovementSpeedForCell(actor, actor.Location)) * info.LandingReplanInterval;
						threats.Add(new Threat
						{
							Actor = actor,
							Armament = armament,
							Range = armament.MaxRange(),
							Buffer = WDist.FromCells(info.LandingThreatRangeBufferCells),
							MobileMargin = new WDist(movement),
						});
					}
				}
			}

			var coarseSize = info.LandingCoarseCellSize;
			coarseWidth = (world.Map.MapSize.X + coarseSize - 1) / coarseSize;
			coarseHeight = (world.Map.MapSize.Y + coarseSize - 1) / coarseSize;
			coarseDanger = new float[coarseWidth * coarseHeight];
			foreach (var threat in threats)
			{
				if (!TransportLandingPolicy.DealsPositiveDamage(
					threat.Armament.Weapon, CarrierTargetTypes, new[] { "Light" }))
					continue;

				var rangeCells = (threat.Range.Length + threat.Buffer.Length + threat.MobileMargin.Length + 1023) / 1024;
				var minX = Math.Max(0, (threat.Actor.Location.X - rangeCells) / coarseSize);
				var maxX = Math.Min(coarseWidth - 1, (threat.Actor.Location.X + rangeCells) / coarseSize);
				var minY = Math.Max(0, (threat.Actor.Location.Y - rangeCells) / coarseSize);
				var maxY = Math.Min(coarseHeight - 1, (threat.Actor.Location.Y + rangeCells) / coarseSize);
				for (var y = minY; y <= maxY; y++)
					for (var x = minX; x <= maxX; x++)
					{
						var cell = world.Map.Clamp(new CPos(x * coarseSize + coarseSize / 2,
							y * coarseSize + coarseSize / 2));
						if (Covers(threat, cell))
							coarseDanger[y * coarseWidth + x] += 1f;
					}
			}
		}

		public bool IsSafe(CPos cell, BitSet<TargetableType> targetTypes, string[] armorTypes,
			out Actor rejectingActor, out Armament rejectingArmament, out WDist effectiveRange)
		{
			foreach (var threat in threats)
			{
				if (!TransportLandingPolicy.DealsPositiveDamage(threat.Armament.Weapon, targetTypes, armorTypes) ||
					!Covers(threat, cell))
					continue;

				rejectingActor = threat.Actor;
				rejectingArmament = threat.Armament;
				effectiveRange = new WDist(threat.Range.Length + threat.Buffer.Length + threat.MobileMargin.Length);
				return false;
			}

			rejectingActor = null;
			rejectingArmament = null;
			effectiveRange = WDist.Zero;
			return true;
		}

		public bool IsCarrierSafe(CPos cell, Actor carrier, out Actor threatActor,
			out Armament threatArmament, out WDist effectiveRange)
		{
			return IsSafe(cell, CarrierTargetTypes, ArmorTypes(carrier), out threatActor, out threatArmament, out effectiveRange);
		}

		public List<CPos> Route(Actor carrier, CPos destination)
		{
			var coarseSize = info.LandingCoarseCellSize;
			var start = world.Map.CellContaining(carrier.CenterPosition);
			var startX = Math.Clamp(start.X / coarseSize, 0, coarseWidth - 1);
			var startY = Math.Clamp(start.Y / coarseSize, 0, coarseHeight - 1);
			var goalX = Math.Clamp(destination.X / coarseSize, 0, coarseWidth - 1);
			var goalY = Math.Clamp(destination.Y / coarseSize, 0, coarseHeight - 1);
			var route = ThreatAwareRoutePlanner.FindRoute(coarseDanger, coarseWidth, coarseHeight,
				startX, startY, goalX, goalY, info.LandingRouteThreatPenalty);
			if (route == null)
				return null;

			var result = ThreatAwareRoutePlanner.SmoothRoute(coarseDanger, coarseWidth, coarseHeight,
				startX, startY, route).Select(p => world.Map.Clamp(new CPos(
				p.X * coarseSize + coarseSize / 2, p.Y * coarseSize + coarseSize / 2))).ToList();
			if (result.Count == 0 || result[result.Count - 1] != destination)
				result.Add(destination);

			return result;
		}

		static string[] ArmorTypes(Actor actor)
		{
			var types = actor.TraitsImplementing<Armor>().Where(a => !a.IsTraitDisabled && !string.IsNullOrEmpty(a.Info.Type))
				.Select(a => a.Info.Type).Distinct().ToArray();
			if (types.Length > 0)
				return types;

			var type = actor.Info.TraitInfoOrDefault<ArmorInfo>()?.Type;
			return string.IsNullOrEmpty(type) ? Array.Empty<string>() : new[] { type };
		}

		bool Covers(Threat threat, CPos cell)
		{
			var range = (long)threat.Range.Length + threat.Buffer.Length + threat.MobileMargin.Length;
			return (world.Map.CenterOfCell(cell) - threat.Actor.CenterPosition).HorizontalLengthSquared <= range * range;
		}

		public static string[] TargetArmorTypes(Actor actor) { return ArmorTypes(actor); }
	}

	/// <summary>
	/// Builds one live threat snapshot per bounded bot planning epoch, then uses actor-owned landing and
	/// locomotor checks to allocate exact carrier/exit plans without rescanning the world per candidate.
	/// </summary>
	public sealed class TransportUnloadPlanner
	{
		readonly World world;
		readonly Player player;
		readonly TransportManagerBotModuleInfo info;
		readonly TransportMissionCoordinator coordinator;
		TransportThreatSnapshot snapshot;

		public int SnapshotTick => snapshot?.Tick ?? -1;
		public int SnapshotThreatCount => snapshot?.ThreatCount ?? 0;

		public TransportUnloadPlanner(World world, Player player, TransportManagerBotModuleInfo info,
			TransportMissionCoordinator coordinator)
		{
			this.world = world;
			this.player = player;
			this.info = info;
			this.coordinator = coordinator;
		}

		public void RefreshSnapshot()
		{
			if (snapshot == null || world.WorldTick - snapshot.Tick >= info.LandingReplanInterval)
				snapshot = new TransportThreatSnapshot(world, player, info);
		}

		public bool TryPlan(int missionId, Actor carrier, IEnumerable<Actor> passengers, CPos objective,
			int searchRadius, int usefulnessRadius, int revision, out TransportUnloadPlan plan, out string rejection)
		{
			if (!TryPlanWithoutClaim(missionId, carrier, passengers, objective, searchRadius, usefulnessRadius,
				revision, Array.Empty<CPos>(), out plan, out rejection))
			{
				coordinator.ReleaseCells(missionId);
				return false;
			}

			if (!TryClaimPlans(missionId, new[] { plan }, out rejection))
			{
				plan = null;
				coordinator.ReleaseCells(missionId);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Selects and claims a threat-screened landing cell for an empty carrier returning after handoff.
		/// Carrier recovery has no passenger exits, but shares the live snapshot, deterministic candidate
		/// order, actor blocking, and claim ledger used by unload plans.
		/// </summary>
		public bool TryPlanCarrierRecovery(int missionId, Actor carrier, CPos objective,
			int searchRadius, int revision, out TransportUnloadPlan plan, out string rejection)
		{
			return TryPlanCarrierRecovery(missionId, carrier, objective, objective, searchRadius,
				revision, out plan, out rejection);
		}

		public bool TryPlanCarrierRecovery(int missionId, Actor carrier, CPos searchCenter,
			CPos recoveryObjective, int searchRadius, int revision,
			out TransportUnloadPlan plan, out string rejection)
		{
			RefreshSnapshot();
			plan = null;
			rejection = "no bounded candidate has a safe landable carrier recovery cell";
			var aircraft = carrier?.TraitOrDefault<Aircraft>();
			if (aircraft == null || carrier.IsDead || !carrier.IsInWorld)
			{
				rejection = "carrier is unavailable or lacks Aircraft";
				return false;
			}

			var candidatesEvaluated = 0;
			string firstRejection = null;
			string firstThreatRejection = null;
			foreach (var carrierCell in TransportLandingPolicy.OrderedCandidates(
				world.Map.FindTilesInCircle(searchCenter, searchRadius), searchCenter).Take(info.LandingMaximumCandidates))
			{
				candidatesEvaluated++;
				var claimOwner = coordinator.ClaimOwner(carrierCell);
				if (claimOwner != 0 && claimOwner != missionId)
				{
					rejection = $"carrier recovery cell {carrierCell} is claimed by mission {claimOwner}";
					firstRejection = firstRejection ?? rejection;
					continue;
				}

				if (!aircraft.CanLand(carrierCell, blockedByMobile: true))
				{
					rejection = $"carrier recovery cell {carrierCell} blocked by terrain or actor";
					firstRejection = firstRejection ?? rejection;
					continue;
				}

				if (!snapshot.IsCarrierSafe(carrierCell, carrier, out var threatActor,
					out var threatArmament, out var effectiveRange))
				{
					rejection = $"carrier recovery cell {carrierCell} covered by {threatActor} weapon " +
						$"{threatArmament.Info.Weapon} effectiveRange={effectiveRange.Length}";
					firstRejection = firstRejection ?? rejection;
					firstThreatRejection = firstThreatRejection ?? rejection;
					continue;
				}

				plan = new TransportUnloadPlan(recoveryObjective, carrierCell, Array.Empty<uint>(), Array.Empty<CPos>(),
					revision, snapshot.Tick, candidatesEvaluated, firstRejection, firstThreatRejection);
				if (TryClaimPlans(missionId, new[] { plan }, out rejection))
					return true;

				plan = null;
				return false;
			}

			rejection = firstThreatRejection ?? firstRejection ?? rejection;
			coordinator.ReleaseCells(missionId);
			return false;
		}

		/// <summary>
		/// Builds one plan without changing the claim ledger. Multi-carrier missions use this to
		/// construct a complete non-overlapping set before atomically replacing their shared claims.
		/// Existing claims owned by the same mission are ignored until the complete set is committed.
		/// </summary>
		public bool TryPlanWithoutClaim(int missionId, Actor carrier, IEnumerable<Actor> passengers, CPos objective,
			int searchRadius, int usefulnessRadius, int revision, IEnumerable<CPos> unavailableCells,
			out TransportUnloadPlan plan, out string rejection)
		{
			return TryPlanWithoutClaim(missionId, carrier, passengers, objective, objective, searchRadius,
				usefulnessRadius, revision, unavailableCells, out plan, out rejection);
		}

		public bool TryPlanWithoutClaim(int missionId, Actor carrier, IEnumerable<Actor> passengers,
			CPos searchCenter, CPos handoffObjective, int searchRadius, int usefulnessRadius, int revision,
			IEnumerable<CPos> unavailableCells, out TransportUnloadPlan plan, out string rejection)
		{
			RefreshSnapshot();
			plan = null;
			rejection = "no bounded candidate has a landable carrier cell and complete useful exits";
			var unavailable = new HashSet<CPos>(unavailableCells ?? Array.Empty<CPos>());
			var aircraft = carrier?.TraitOrDefault<Aircraft>();
			if (aircraft == null || carrier.IsDead || !carrier.IsInWorld)
			{
				rejection = "carrier is unavailable or lacks Aircraft";
				return false;
			}

			var orderedPassengers = passengers?.Where(a => a != null && !a.IsDead).OrderBy(a => a.ActorID).ToArray() ??
				Array.Empty<Actor>();
			if (orderedPassengers.Length == 0)
			{
				rejection = "no live intended passengers";
				return false;
			}

			var candidates = TransportLandingPolicy.OrderedCandidates(
				world.Map.FindTilesInCircle(searchCenter, searchRadius), searchCenter).Take(info.LandingMaximumCandidates);
			var candidatesEvaluated = 0;
			string firstRejection = null;
			string firstThreatRejection = null;
			foreach (var carrierCell in candidates)
			{
				candidatesEvaluated++;
				var claimOwner = coordinator.ClaimOwner(carrierCell);
				if (unavailable.Contains(carrierCell) || (claimOwner != 0 && claimOwner != missionId))
				{
					rejection = claimOwner == 0 ? $"carrier cell {carrierCell} conflicts with this plan set" :
						$"carrier cell {carrierCell} is claimed by mission {claimOwner}";
					firstRejection = firstRejection ?? rejection;
					continue;
				}

				if (!aircraft.CanLand(carrierCell, blockedByMobile: true))
				{
					rejection = $"carrier cell {carrierCell} blocked by terrain or actor";
					firstRejection = firstRejection ?? rejection;
					continue;
				}

				if (!snapshot.IsCarrierSafe(carrierCell, carrier, out var threatActor,
					out var threatArmament, out var effectiveRange))
				{
					rejection = $"carrier cell {carrierCell} covered by {threatActor} weapon " +
						$"{threatArmament.Info.Weapon} effectiveRange={effectiveRange.Length}";
					firstRejection = firstRejection ?? rejection;
					firstThreatRejection = firstThreatRejection ?? rejection;
					continue;
				}

				if (!TryAllocateExits(missionId, carrierCell, orderedPassengers, handoffObjective, usefulnessRadius, unavailable,
					out var exits, out rejection))
				{
					firstRejection = firstRejection ?? rejection;
					if (rejection.Contains(" weapon "))
						firstThreatRejection = firstThreatRejection ?? rejection;

					continue;
				}

				plan = new TransportUnloadPlan(handoffObjective, carrierCell,
					orderedPassengers.Select(a => a.ActorID).ToArray(), exits, revision, snapshot.Tick,
					candidatesEvaluated, firstRejection, firstThreatRejection);
				return true;
			}

			// A failed bounded search may encounter both a blocked objective cell and covered
			// alternatives. Prefer the first actionable threat identity without fabricating one
			// when the failure was purely terrain, occupancy, or connectivity.
			rejection = firstThreatRejection ?? firstRejection ?? rejection;
			return false;
		}

		public bool TryClaimPlans(int missionId, IEnumerable<TransportUnloadPlan> plans, out string rejection)
		{
			var cells = plans?.Where(p => p != null).SelectMany(p =>
				new[] { p.CarrierCell }.Concat(p.ExitCells)).ToArray() ?? Array.Empty<CPos>();
			if (cells.Length == 0 || cells.Distinct().Count() != cells.Length)
			{
				rejection = "plan set has no cells or contains overlapping carrier/exit claims";
				return false;
			}

			if (!coordinator.TryClaimCells(missionId, cells, out var owner))
			{
				rejection = $"plan set conflicts with mission {owner}";
				return false;
			}

			rejection = null;
			return true;
		}

		public bool Revalidate(int missionId, Actor carrier, IEnumerable<Actor> passengers,
			TransportUnloadPlan plan, int usefulnessRadius, out string rejection)
		{
			if (!RevalidateWithoutClaim(missionId, carrier, passengers, plan, usefulnessRadius,
				Array.Empty<CPos>(), out rejection) || !TryClaimPlans(missionId, new[] { plan }, out rejection))
			{
				coordinator.ReleaseCells(missionId);
				return false;
			}

			return true;
		}

		public bool RevalidateWithoutClaim(int missionId, Actor carrier, IEnumerable<Actor> passengers,
			TransportUnloadPlan plan, int usefulnessRadius, IEnumerable<CPos> unavailableCells,
			out string rejection)
		{
			RefreshSnapshot();
			var unavailable = new HashSet<CPos>(unavailableCells ?? Array.Empty<CPos>());
			var aircraft = carrier?.TraitOrDefault<Aircraft>();
			if (plan == null || aircraft == null ||
				unavailable.Contains(plan.CarrierCell) ||
				!aircraft.CanLand(plan.CarrierCell, blockedByMobile: true) ||
				(coordinator.ClaimOwner(plan.CarrierCell) != 0 && coordinator.ClaimOwner(plan.CarrierCell) != missionId))
			{
				rejection = "planned carrier cell is no longer landable";
				return false;
			}

			if (!snapshot.IsCarrierSafe(plan.CarrierCell, carrier, out var threatActor,
				out var threatArmament, out var effectiveRange))
			{
				rejection = $"planned carrier cell covered by {threatActor} weapon " +
					$"{threatArmament.Info.Weapon} effectiveRange={effectiveRange.Length}";
				return false;
			}

			var orderedPassengers = passengers.Where(a => a != null && !a.IsDead).OrderBy(a => a.ActorID).ToArray();
			if (orderedPassengers.Length != plan.ExitCells.Length ||
				!orderedPassengers.Select(a => a.ActorID).SequenceEqual(plan.PassengerIds))
			{
				rejection = "planned passenger/exit count changed";
				return false;
			}

			rejection = null;
			for (var i = 0; i < orderedPassengers.Length; i++)
				if (unavailable.Contains(plan.ExitCells[i]) ||
					(coordinator.ClaimOwner(plan.ExitCells[i]) != 0 &&
						coordinator.ClaimOwner(plan.ExitCells[i]) != missionId) ||
					!IsExitValid(orderedPassengers[i], plan.ExitCells[i], plan.Objective, usefulnessRadius, out rejection))
				{
					rejection = rejection ?? $"planned exit {plan.ExitCells[i]} conflicts with this plan set";
					return false;
				}

			rejection = null;
			return true;
		}

		public List<CPos> Route(Actor carrier, TransportUnloadPlan plan)
		{
			RefreshSnapshot();
			return plan == null ? null : snapshot.Route(carrier, plan.CarrierCell);
		}

		bool TryAllocateExits(int missionId, CPos carrierCell, Actor[] passengers, CPos objective,
			int usefulnessRadius, HashSet<CPos> unavailable, out CPos[] exits, out string rejection)
		{
			var allocated = new List<CPos>();
			foreach (var passenger in passengers)
			{
				var exit = TransportLandingPolicy.OrderedCandidates(
					Util.AdjacentCells(world, Target.FromCell(world, carrierCell)).Where(c => c != carrierCell), objective)
					.Cast<CPos?>().FirstOrDefault(c => c.HasValue && !allocated.Contains(c.Value) &&
						!unavailable.Contains(c.Value) &&
						(coordinator.ClaimOwner(c.Value) == 0 || coordinator.ClaimOwner(c.Value) == missionId) &&
						IsExitValid(passenger, c.Value, objective, usefulnessRadius, out _));
				if (!exit.HasValue)
				{
					exits = Array.Empty<CPos>();
					rejection = $"carrier cell {carrierCell} lacks a safe useful exit for {passenger}";
					return false;
				}

				allocated.Add(exit.Value);
			}

			exits = allocated.ToArray();
			rejection = null;
			return true;
		}

		bool IsExitValid(Actor passenger, CPos exit, CPos objective, int usefulnessRadius, out string rejection)
		{
			if (!world.Map.Contains(exit))
			{
				rejection = $"exit {exit} is outside the map";
				return false;
			}

			var positionable = passenger.TraitOrDefault<IPositionable>();
			if (positionable == null || !positionable.CanEnterCell(exit, null, BlockedByActor.All))
			{
				rejection = $"exit {exit} is not currently enterable by {passenger}";
				return false;
			}

			var targetTypes = passenger.GetEnabledTargetTypes();
			if (!snapshot.IsSafe(exit, targetTypes, TransportThreatSnapshot.TargetArmorTypes(passenger),
				out var threatActor, out var threatArmament, out var effectiveRange))
			{
				rejection = $"exit {exit} covered by {threatActor} weapon {threatArmament.Info.Weapon} " +
					$"effectiveRange={effectiveRange.Length}";
				return false;
			}

			var mobile = passenger.TraitOrDefault<Mobile>();
			if (mobile == null || !HasUsefulGroundHandoff(passenger, mobile, exit, objective, usefulnessRadius))
			{
				rejection = $"exit {exit} has no bounded ground handoff toward {objective}";
				return false;
			}

			rejection = null;
			return true;
		}

		bool HasUsefulGroundHandoff(Actor passenger, Mobile mobile, CPos exit, CPos objective, int maximumPathCells)
		{
			foreach (var goal in TransportLandingPolicy.OrderedCandidates(
				world.Map.FindTilesInCircle(objective, Math.Max(1, info.UnloadRangeCells)), objective)
				.Where(c => mobile.CanEnterCell(c, check: BlockedByActor.Stationary)))
			{
				if (exit == goal)
					return true;

				var path = mobile.Pathfinder.FindUnitPath(exit, goal, passenger, null, BlockedByActor.Stationary);
				if (path.Count > 0 && path.Count <= maximumPathCells)
					return true;
			}

			return false;
		}
	}

	public static class TransportUnloadOrder
	{
		public static Order Create(World world, Actor carrier, TransportUnloadPlan plan, bool queued = false)
		{
			var exits = plan.PassengerIds.Zip(plan.ExitCells,
				(id, cell) => new KeyValuePair<uint, CPos>(id, cell));
			return new Order("Unload", carrier, Target.FromCell(world, plan.CarrierCell), queued)
			{
				ExtraData = 1,
				ExtraLocation = plan.ExitCells[0],
				TargetString = TransportLandingPolicy.EncodeExactExits(exits),
			};
		}
	}
}
