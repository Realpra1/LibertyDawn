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

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Disabled Approach owner. Strategic cache data is used only for long A* routing; target
	/// validity, local safety, squad position, and arrival classification are read live by this owner.
	/// </summary>
	public sealed class StealthApproachBehavior
	{
		const float DangerCost = 1f;

		readonly StealthApproachHandoff handoff;
		readonly StealthApproachMission mission;
		readonly IStealthApproachStrategicCache cache;
		readonly IStealthApproachLiveWorld liveWorld;
		readonly IStealthTargetThreatAdapter threatAdapter;
		readonly IStealthApproachMovementOrders movementOrders;
		readonly List<CPos> route = new List<CPos>();
		readonly List<StealthApproachRouteThreat> routeThreats = new List<StealthApproachRouteThreat>();
		int routeIndex;
		uint[] lastIssuedActorIds = Array.Empty<uint>();
		CPos? lastIssuedDestination;
		StealthApproachArrivalClassification lastArrivalClassification;
		StealthApproachDisposition lastDisposition = StealthApproachDisposition.AwaitingSafeRoute;
		StealthTargetThreatScore? lastLocalThreatScore;
		uint[] lastLiveDefenderActorIds = Array.Empty<uint>();

		public StealthApproachBehavior(StealthApproachHandoff handoff,
			IStealthApproachStrategicCache cache, IStealthApproachLiveWorld liveWorld,
			IStealthTargetThreatAdapter threatAdapter, IStealthApproachMovementOrders movementOrders)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.Approach || handoff.Missions.Count != 1)
				throw new ArgumentException("Approach requires exactly one owned strategic mission.", nameof(handoff));

			mission = handoff.Missions[0];
			this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
			this.liveWorld = liveWorld ?? throw new ArgumentNullException(nameof(liveWorld));
			this.threatAdapter = threatAdapter ?? throw new ArgumentNullException(nameof(threatAdapter));
			this.movementOrders = movementOrders ?? throw new ArgumentNullException(nameof(movementOrders));
		}

		public StealthApproachResult Execute()
		{
			var live = liveWorld.Read(mission) ??
				throw new InvalidOperationException("The live Approach view returned no snapshot.");
			var activeMembers = ActiveMembers(live.Members);
			var activeIds = activeMembers.Select(member => member.ActorId).ToArray();
			var center = Center(activeMembers);

			if (!live.TargetIsValid)
				return Result(StealthApproachDisposition.Reacquire,
					StealthApproachArrivalClassification.None, center, activeIds, Array.Empty<uint>(), null);

			if (IsSameOrAdjacent(center, mission.StrategicCell))
			{
				var defended = live.LiveDefenderActorIds.Count != 0;
				return Result(defended ? StealthApproachDisposition.CrushEvaluation :
					StealthApproachDisposition.UndefendedAttack,
					defended ? StealthApproachArrivalClassification.Defended :
					StealthApproachArrivalClassification.Undefended, center, activeIds,
					live.LiveDefenderActorIds, null);
			}

			var localFacts = new StealthTargetThreatFacts(center,
				live.LocalFriendlyGroup,
				live.LocalEnemyGroup, live.FormationCloaked, live.HasDetectorCoverage,
				mission.TargetOption.ValueOption.ThreatFacts.PlannedActionRevealsFormation &&
					live.PlannedActionRevealsFormation);
			var localScore = threatAdapter.Calculate(localFacts);
			if (localScore.ThreatRating > 0)
				return Result(StealthApproachDisposition.Reacquire,
					StealthApproachArrivalClassification.None, center, activeIds, Array.Empty<uint>(), localScore);

			var strategic = cache.ReadSnapshot() ??
				throw new InvalidOperationException("The strategic Approach cache returned no snapshot.");
			if (!Inside(strategic, center) || !Inside(strategic, mission.StrategicCell))
				return Result(StealthApproachDisposition.AwaitingSafeRoute,
					StealthApproachArrivalClassification.None, center, activeIds, Array.Empty<uint>(), localScore);

			var cellThreats = CalculateStrategicThreats(strategic);
			AdvanceRoute(center);
			if (route.Count != 0 &&
				(routeIndex >= route.Count || !IsCardinalAdjacent(center, route[routeIndex])))
				ClearRouteOwnership();
			if (!RemainingRouteIsSafe(strategic, cellThreats) &&
				!TryBuildRoute(strategic, cellThreats, center))
				return Result(StealthApproachDisposition.AwaitingSafeRoute,
					StealthApproachArrivalClassification.None, center, activeIds, Array.Empty<uint>(), localScore);

			AdvanceRoute(center);
			if (routeIndex >= route.Count)
				return Result(StealthApproachDisposition.AwaitingSafeRoute,
					StealthApproachArrivalClassification.None, center, activeIds, Array.Empty<uint>(), localScore);

			var destination = route[routeIndex];
			if (lastIssuedDestination != destination || !lastIssuedActorIds.SequenceEqual(activeIds))
			{
				movementOrders.IssueMove(handoff.Owner, handoff.Epoch, activeIds, destination);
				lastIssuedDestination = destination;
				lastIssuedActorIds = activeIds;
			}

			return Result(StealthApproachDisposition.Moving,
				StealthApproachArrivalClassification.None, center, activeIds, Array.Empty<uint>(), localScore);
		}

		public MiniYamlNode SerializePrivateState(string key = "Approach")
		{
			return StealthApproachPersistence.Serialize(key, handoff, mission,
				route, routeThreats, routeIndex, lastArrivalClassification,
				lastDisposition, lastLocalThreatScore, lastIssuedActorIds, lastIssuedDestination,
				lastLiveDefenderActorIds);
		}

		public void RestorePrivateState(MiniYamlNode node)
		{
			var state = StealthApproachPersistence.Restore(node, handoff, mission);
			ValidateRestoredRoute(state);
			route.Clear();
			route.AddRange(state.Route);
			routeThreats.Clear();
			routeThreats.AddRange(state.RouteThreats);
			routeIndex = state.RouteIndex;
			lastArrivalClassification = state.ArrivalClassification;
			lastDisposition = state.Disposition;
			lastLocalThreatScore = state.LocalThreatScore;
			lastIssuedActorIds = state.LastIssuedActorIds;
			lastIssuedDestination = state.LastIssuedDestination;
			lastLiveDefenderActorIds = state.LiveDefenderActorIds;
		}

		void ValidateRestoredRoute(StealthApproachPrivateState state)
		{
			if (state.Route.Length == 0)
				return;
			if (state.Route.Distinct().Count() != state.Route.Length)
				throw new InvalidOperationException("Approach routes cannot contain duplicate or cyclic cells.");

			var strategic = cache.ReadSnapshot() ??
				throw new InvalidOperationException("The strategic Approach cache returned no snapshot.");
			var threats = CalculateStrategicThreats(strategic);
			for (var i = 0; i < state.Route.Length; i++)
			{
				var cell = state.Route[i];
				if (!Inside(strategic, cell) || (i != 0 && !IsCardinalAdjacent(state.Route[i - 1], cell)))
					throw new InvalidOperationException("Invalid normalized Approach route in private save state.");
				var cacheCell = strategic.Cells[cell.Y * strategic.Width + cell.X];
				var saved = state.RouteThreats[i];
				var expected = ThreatAt(strategic, threats, cell);
				if (!saved.Score.ThreatRating.Equals(expected.ThreatRating) ||
					!saved.Score.Crossover.Equals(expected.Crossover) ||
					saved.HasDetectorCoverage != cacheCell.HasDetectorCoverage ||
					saved.PlannedActionRevealsFormation != cacheCell.PlannedActionRevealsFormation)
					throw new InvalidOperationException(
						"Approach private state does not match the standard strategic threat context.");
			}

			if (!IsSameOrAdjacent(state.Route[state.Route.Length - 1], mission.StrategicCell) ||
				(state.LastIssuedDestination != null && !state.Route.Contains(state.LastIssuedDestination.Value)))
				throw new InvalidOperationException("Invalid normalized Approach route endpoint in private save state.");
			if (state.LastIssuedDestination != null)
			{
				var ownedIndex = Math.Min(state.RouteIndex, state.Route.Length - 1);
				if (state.LastIssuedDestination != state.Route[ownedIndex])
					throw new InvalidOperationException("Invalid normalized Approach route progress in private save state.");
			}
		}

		StealthApproachResult Result(StealthApproachDisposition disposition,
			StealthApproachArrivalClassification classification, CPos center,
			IEnumerable<uint> activeIds, IEnumerable<uint> liveDefenderActorIds,
			StealthTargetThreatScore? localScore)
		{
			lastDisposition = disposition;
			lastArrivalClassification = classification;
			lastLocalThreatScore = localScore;
			lastLiveDefenderActorIds = liveDefenderActorIds.ToArray();
			return new StealthApproachResult(handoff.Handoff, mission, disposition,
				classification, center, route, routeIndex, activeIds,
				lastLiveDefenderActorIds, localScore);
		}

		StealthTargetThreatScore[] CalculateStrategicThreats(StealthApproachStrategicCacheSnapshot strategic)
		{
			var missionFacts = mission.TargetOption.ValueOption.ThreatFacts;
			return strategic.Cells.Select(cell => threatAdapter.Calculate(new StealthTargetThreatFacts(
				cell.StrategicCell, missionFacts.FriendlyGroup, cell.EnemyGroup,
				missionFacts.FormationCloaked, cell.HasDetectorCoverage,
				missionFacts.PlannedActionRevealsFormation &&
					cell.PlannedActionRevealsFormation))).ToArray();
		}

		bool RemainingRouteIsSafe(StealthApproachStrategicCacheSnapshot strategic,
			IReadOnlyList<StealthTargetThreatScore> threats)
		{
			if (routeIndex >= route.Count || !IsSameOrAdjacent(route[route.Count - 1], mission.StrategicCell))
				return false;

			for (var i = routeIndex; i < route.Count; i++)
			{
				if (!Inside(strategic, route[i]))
					return false;
				var cell = strategic.Cells[route[i].Y * strategic.Width + route[i].X];
				var saved = routeThreats[i];
				var current = ThreatAt(strategic, threats, route[i]);
				if (!saved.Score.ThreatRating.Equals(current.ThreatRating) ||
					!saved.Score.Crossover.Equals(current.Crossover) ||
					saved.HasDetectorCoverage != cell.HasDetectorCoverage ||
					saved.PlannedActionRevealsFormation != cell.PlannedActionRevealsFormation)
					return false;
			}

			return true;
		}

		bool TryBuildRoute(StealthApproachStrategicCacheSnapshot strategic,
			IReadOnlyList<StealthTargetThreatScore> threats, CPos center)
		{
			var danger = threats.Select(score => (float)Math.Min(score.ThreatRating,
				float.MaxValue / 4)).ToArray();
			var endpoints = new[]
			{
				mission.StrategicCell,
				mission.StrategicCell + new CVec(-1, 0),
				mission.StrategicCell + new CVec(1, 0),
				mission.StrategicCell + new CVec(0, -1),
				mission.StrategicCell + new CVec(0, 1)
			}.Where(cell => Inside(strategic, cell))
				.Distinct().ToArray();
			var selected = endpoints.Select(endpoint => new
			{
				Endpoint = endpoint,
				Route = ThreatAwareRoutePlanner.FindRoute(danger, strategic.Width, strategic.Height,
					center.X, center.Y, endpoint.X, endpoint.Y, DangerCost)
			}).Where(candidate => candidate.Route != null)
				.OrderBy(candidate => RouteCost(candidate.Route, danger, strategic.Width))
				.ThenBy(candidate => candidate.Route.Count)
				.ThenBy(candidate => candidate.Endpoint.Y)
				.ThenBy(candidate => candidate.Endpoint.X)
				.FirstOrDefault();
			var candidates = selected?.Route;
			if (candidates == null)
				return false;

			route.Clear();
			route.AddRange(candidates);
			routeIndex = 0;
			routeThreats.Clear();
			routeThreats.AddRange(route.Select(cell =>
			{
				var cacheCell = strategic.Cells[cell.Y * strategic.Width + cell.X];
				return new StealthApproachRouteThreat(cell, ThreatAt(strategic, threats, cell),
					cacheCell.HasDetectorCoverage, cacheCell.PlannedActionRevealsFormation);
			}));
			return true;
		}

		void ClearRouteOwnership()
		{
			route.Clear();
			routeThreats.Clear();
			routeIndex = 0;
			lastIssuedActorIds = Array.Empty<uint>();
			lastIssuedDestination = null;
		}

		static double RouteCost(IEnumerable<CPos> candidate, IReadOnlyList<float> danger, int width)
		{
			return candidate.Sum(cell => 1d + danger[cell.Y * width + cell.X] * DangerCost);
		}

		void AdvanceRoute(CPos center)
		{
			while (routeIndex < route.Count && route[routeIndex] == center)
				routeIndex++;
		}

		static IReadOnlyList<StealthApproachMemberSnapshot> ActiveMembers(
			IReadOnlyList<StealthApproachMemberSnapshot> members)
		{
			var core = members.Where(member => !member.IsReinforcement).ToArray();
			var currentCell = Center(core);
			return core.Concat(members.Where(member => member.IsReinforcement &&
				IsSameOrAdjacent(member.StrategicCell, currentCell))).OrderBy(member => member.ActorId).ToArray();
		}

		static CPos Center(IReadOnlyList<StealthApproachMemberSnapshot> members)
		{
			return new CPos((int)(members.Sum(member => (long)member.StrategicCell.X) / members.Count),
				(int)(members.Sum(member => (long)member.StrategicCell.Y) / members.Count));
		}

		static bool Inside(StealthApproachStrategicCacheSnapshot snapshot, CPos cell)
		{
			return cell.X >= 0 && cell.Y >= 0 && cell.X < snapshot.Width && cell.Y < snapshot.Height;
		}

		static StealthTargetThreatScore ThreatAt(StealthApproachStrategicCacheSnapshot snapshot,
			IReadOnlyList<StealthTargetThreatScore> threats, CPos cell)
		{
			return threats[cell.Y * snapshot.Width + cell.X];
		}

		static bool IsSameOrAdjacent(CPos left, CPos right)
		{
			return Math.Abs(left.X - right.X) <= 1 && Math.Abs(left.Y - right.Y) <= 1;
		}

		static bool IsCardinalAdjacent(CPos left, CPos right)
		{
			return Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y) == 1;
		}
	}
}
