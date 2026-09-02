#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Strategic Approach owner: follow the established cached route until local arrival.</summary>
	public sealed class StealthApproachBehavior
	{
		readonly StealthApproachHandoff handoff;
		readonly StealthApproachMission mission;
		readonly IStealthApproachStrategicRouteCache routeCache;
		readonly IStealthApproachLiveWorld liveWorld;
		readonly IStealthApproachMovementOrders movementOrders;
		uint[] lastMembers = Array.Empty<uint>();
		CPos? lastDestination;
		long orderRevision;

		public StealthApproachBehavior(StealthApproachHandoff handoff,
			IStealthApproachStrategicCache cache, IStealthApproachLiveWorld liveWorld,
			IStealthApproachMovementOrders movementOrders)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.Approach || handoff.Missions.Count != 1)
				throw new ArgumentException("Approach requires one owned strategic mission.", nameof(handoff));
			mission = handoff.Missions[0];
			routeCache = cache as IStealthApproachStrategicRouteCache ?? throw new ArgumentException(
				"Approach requires the established strategic route cache.", nameof(cache));
			this.liveWorld = liveWorld ?? throw new ArgumentNullException(nameof(liveWorld));
			this.movementOrders = movementOrders ?? throw new ArgumentNullException(nameof(movementOrders));
		}

		public StealthApproachResult Execute()
		{
			var live = liveWorld.Read(mission) ??
				throw new InvalidOperationException("The live Approach view returned no snapshot.");
			var members = ActiveMembers(live.Members);
			var memberIds = members.Select(member => member.ActorId).ToArray();
			var center = Center(members);
			if (!live.TargetIsValid)
				return Result(StealthApproachDisposition.Reacquire,
					StealthApproachArrivalClassification.None, center, memberIds,
					Array.Empty<uint>(), null, Array.Empty<CPos>());

			if (IsSameOrAdjacent(center, mission.StrategicCell))
			{
				var defended = live.LiveDefenderActorIds.Count != 0;
				return Result(defended ? StealthApproachDisposition.CrushEvaluation :
					StealthApproachDisposition.UndefendedAttack,
					defended ? StealthApproachArrivalClassification.Defended :
					StealthApproachArrivalClassification.Undefended,
					center, memberIds, live.LiveDefenderActorIds, null, Array.Empty<CPos>());
			}

			var localScore = live.CurrentThreatScore;
			if (!live.CurrentPositionSafe)
				return Result(StealthApproachDisposition.RecalculateFlee,
					StealthApproachArrivalClassification.None, center, memberIds,
					live.LiveDefenderActorIds, localScore, Array.Empty<CPos>(), live);
			var route = routeCache.ReadRoute(center, mission.StrategicCell)?
				.SkipWhile(cell => cell == center).ToArray() ?? Array.Empty<CPos>();
			if (route.Length == 0 || route.Distinct().Count() != route.Length ||
				!IsSameOrAdjacent(route[route.Length - 1], mission.StrategicCell))
				return Result(StealthApproachDisposition.Reacquire,
					StealthApproachArrivalClassification.None, center, memberIds,
					Array.Empty<uint>(), localScore, Array.Empty<CPos>());

			var destination = route[0];
			var membershipChanged = !lastMembers.SequenceEqual(memberIds);
			var movementFinished = lastDestination.HasValue &&
				members.All(member => member.NeedsMovementOrder);
			if (!lastDestination.HasValue || membershipChanged || movementFinished)
			{
				movementOrders.IssueMove(handoff.Owner, handoff.Epoch, memberIds,
					destination, ++orderRevision);
				lastDestination = destination;
				lastMembers = memberIds;
			}

			return Result(StealthApproachDisposition.Moving,
				StealthApproachArrivalClassification.None, center, memberIds,
				Array.Empty<uint>(), localScore, route);
		}

		StealthApproachResult Result(StealthApproachDisposition disposition,
			StealthApproachArrivalClassification classification, CPos center,
			IEnumerable<uint> memberIds, IEnumerable<uint> defenders,
			StealthTargetThreatScore? localScore, IEnumerable<CPos> route,
			StealthApproachLiveSnapshot live = null)
		{
			if (disposition != StealthApproachDisposition.Moving)
			{
				lastDestination = null;
				lastMembers = Array.Empty<uint>();
			}

			return new StealthApproachResult(handoff.Handoff, mission, disposition,
				classification, center, route, 0, memberIds, defenders, localScore,
				live?.CurrentPositionSafe ?? true, live?.ImmediateThreatActorId,
				live?.ImmediateThreatCurrentCell, live?.FormationCloaked ?? true);
		}

		static IReadOnlyList<StealthApproachMemberSnapshot> ActiveMembers(
			IReadOnlyList<StealthApproachMemberSnapshot> members)
		{
			var core = members.Where(member => !member.IsReinforcement).ToArray();
			var center = Center(core);
			return core.Concat(members.Where(member => member.IsReinforcement &&
				IsSameOrAdjacent(member.StrategicCell, center)))
				.OrderBy(member => member.ActorId).ToArray();
		}

		static CPos Center(IReadOnlyList<StealthApproachMemberSnapshot> members)
		{
			return new CPos((int)(members.Sum(member => (long)member.StrategicCell.X) / members.Count),
				(int)(members.Sum(member => (long)member.StrategicCell.Y) / members.Count));
		}

		static bool IsSameOrAdjacent(CPos left, CPos right)
		{
			return Math.Abs(left.X - right.X) <= 1 && Math.Abs(left.Y - right.Y) <= 1;
		}
	}
}
