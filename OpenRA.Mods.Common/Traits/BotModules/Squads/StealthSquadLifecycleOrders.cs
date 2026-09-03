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
using System.Globalization;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Adapts every accepted owner order port to the single runtime sink.</summary>
	sealed class StealthSquadLifecycleOrders : IStealthApproachMovementOrders,
		IStealthUndefendedAttackOrders, IStealthCrushOrders, IStealthKiteOrders,
		IStealthMassAttackOrders, IStealthRecalculateFleeOrders, IStealthRepairOrders
	{
		readonly IStealthLifecycleRuntimeOrders orders;

		public StealthSquadLifecycleOrders(IStealthLifecycleRuntimeOrders orders)
		{
			this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
		}

		void IStealthApproachMovementOrders.IssueMove(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, CPos destinationStrategicCell, long orderRevision)
		{
			Issue(owner, epoch, StealthLifecycleRuntimeOrderKind.Move,
				"approach-" + orderRevision.ToString(CultureInfo.InvariantCulture), actorIds,
				targetCell: destinationStrategicCell);
		}

		void IStealthUndefendedAttackOrders.IssueAttack(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, uint targetActorId, long orderRevision)
		{
			Issue(owner, epoch, StealthLifecycleRuntimeOrderKind.Attack,
				"undefended-" + orderRevision.ToString(CultureInfo.InvariantCulture),
				actorIds, targetActorId);
		}

		void IStealthCrushOrders.IssueCrush(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, uint targetActorId, CPos targetCurrentCell,
			long attemptRevision)
		{
			Issue(owner, epoch, StealthLifecycleRuntimeOrderKind.Crush,
				"crush-" + attemptRevision.ToString(CultureInfo.InvariantCulture), actorIds,
				targetActorId, targetCurrentCell);
		}

		void IStealthKiteOrders.IssueMove(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, CPos cell, StealthKiteOrderToken token)
		{
			Issue(owner, epoch, StealthLifecycleRuntimeOrderKind.Move,
				"kite-" + Token(token.Action, token.PhaseRevision, token.ActivityRevision), actorIds,
				targetCell: cell);
		}

		void IStealthKiteOrders.IssueAttack(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, uint targetActorId, CPos targetCurrentCell,
			StealthKiteOrderToken token)
		{
			Issue(owner, epoch, StealthLifecycleRuntimeOrderKind.Attack,
				"kite-" + Token(token.Action, token.PhaseRevision, token.ActivityRevision), actorIds,
				targetActorId, targetCurrentCell);
		}

		void IStealthMassAttackOrders.IssueMove(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, uint targetActorId, CPos destinationCell,
			StealthMassAttackOrderToken token)
		{
			Issue(owner, epoch, StealthLifecycleRuntimeOrderKind.Move,
				"mass-" + Token(token.Phase, token.AttemptRevision, token.ActivityRevision), actorIds,
				targetActorId, destinationCell);
		}

		void IStealthMassAttackOrders.IssueAttack(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, uint targetActorId, CPos targetCurrentCell,
			StealthMassAttackOrderToken token)
		{
			Issue(owner, epoch, StealthLifecycleRuntimeOrderKind.Attack,
				"mass-" + Token(token.Phase, token.AttemptRevision, token.ActivityRevision), actorIds,
				targetActorId, targetCurrentCell);
		}

		void IStealthRecalculateFleeOrders.IssueMove(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, CPos destinationCell,
			IReadOnlyList<CPos> orderedRoute, int routeProgress,
			StealthRecalculateFleeOrderToken token)
		{
			Issue(owner, epoch, StealthLifecycleRuntimeOrderKind.Move,
				"flee-" + token.RouteRevision.ToString(CultureInfo.InvariantCulture) + "-" +
				token.ActivityRevision.ToString(CultureInfo.InvariantCulture) + "-" +
				routeProgress.ToString(CultureInfo.InvariantCulture), actorIds,
				targetCell: orderedRoute[routeProgress], route: orderedRoute);
		}

		void IStealthRepairOrders.IssueRepair(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, uint repairOptionActorId,
			IReadOnlyList<CPos> orderedRoute, int routeProgress, StealthRepairOrderKind kind,
			StealthRepairOrderToken token)
		{
			Issue(owner, epoch, StealthLifecycleRuntimeOrderKind.Repair,
				"repair-" + Token(kind, token.RouteRevision, token.ActivityRevision) + "-" +
				token.RouteIdentity.ToString(CultureInfo.InvariantCulture) + "-" +
				routeProgress.ToString(CultureInfo.InvariantCulture), actorIds,
				repairOptionActorId, kind == StealthRepairOrderKind.Repair ?
					orderedRoute[orderedRoute.Count - 1] : orderedRoute[routeProgress], orderedRoute);
		}

		void Issue(BehaviorId owner, OwnershipEpoch epoch, StealthLifecycleRuntimeOrderKind kind,
			string action, IEnumerable<uint> actorIds, uint? targetActorId = null,
			CPos? targetCell = null, IEnumerable<CPos> route = null)
		{
			orders.Issue(new StealthLifecycleRuntimeOrder(owner, epoch, kind, action,
				actorIds, targetActorId, targetCell, route));
		}

		static string Token<T>(T phase, long revision, long activity)
		{
			return phase + "-" + revision.ToString(CultureInfo.InvariantCulture) + "-" +
				activity.ToString(CultureInfo.InvariantCulture);
		}
	}

	/// <summary>Only sink that may translate runtime commands into synchronized bot orders.</summary>
	sealed class StealthSquadLifecycleOrderTarget : IStealthLifecycleRuntimeOrderTarget
	{
		readonly Squad squad;

		public StealthSquadLifecycleOrderTarget(Squad squad)
		{
			this.squad = squad ?? throw new ArgumentNullException(nameof(squad));
		}

		public Action Prepare(StealthLifecycleRuntimeOrder order)
		{
			var prepared = StealthSquadLifecycleOrderPreflight.Prepare(order.ActorIds,
				actorId =>
			{
				var actor = squad.World.GetActorById(actorId);
				if (actor == null || actor.IsDead || !actor.IsInWorld || actor.ActorID != actorId ||
					!squad.Units.Contains(actor))
					return null;
				return actor;
			}, actors =>
			{
				var targetCell = order.TargetCell.HasValue ? ResolveCell(order) : (CPos?)null;
				var target = order.TargetActorId.HasValue ? Resolve(order.TargetActorId) : null;
				if (order.Kind != StealthLifecycleRuntimeOrderKind.Attack && !targetCell.HasValue)
					throw new InvalidOperationException("The runtime order has no target cell.");
				if (order.Kind == StealthLifecycleRuntimeOrderKind.Attack && target == null)
					throw new InvalidOperationException("The runtime attack has no live target.");
				if (order.Route.Count != 0 &&
					(order.Route.Distinct().Count() != order.Route.Count ||
						!order.Route.Contains(order.TargetCell.Value)))
					throw new InvalidOperationException("The runtime waypoint route is not canonical.");
				if (order.Kind == StealthLifecycleRuntimeOrderKind.Repair &&
					order.Action.Contains("Repair-Repair", StringComparison.OrdinalIgnoreCase) &&
					(order.Route.Count == 0 || order.Route[order.Route.Count - 1] != order.TargetCell))
					throw new InvalidOperationException("The runtime Repair route is not canonical.");

				switch (order.Kind)
				{
					case StealthLifecycleRuntimeOrderKind.Attack:
						var attackOrder = order.Owner == BehaviorId.Kite ||
							order.Owner == BehaviorId.MassAttack ? "AttackWithoutMoving" : "Attack";
						return new Order(attackOrder, null, Target.FromActor(target), false,
							groupedActors: actors);
					case StealthLifecycleRuntimeOrderKind.Crush:
						return new Order("Move", null,
							Target.FromCell(squad.World, targetCell.Value), false, groupedActors: actors);
					case StealthLifecycleRuntimeOrderKind.Repair:
						if (order.Action.Contains("Repair-Repair", StringComparison.OrdinalIgnoreCase))
							return new Order("Repair", null, Target.FromActor(target), false,
								groupedActors: actors);
						return new Order("Move", null, Target.FromCell(squad.World, targetCell.Value),
							false, groupedActors: actors);
					default:
						return new Order("Move", null, Target.FromCell(squad.World, targetCell.Value),
							false, groupedActors: actors);
				}
			});
			return () =>
			{
				StealthSquadLifecycleTelemetry.RecordOrder(squad, order);
				squad.Bot.QueueOrder(prepared);
			};
		}

		CPos ResolveCell(StealthLifecycleRuntimeOrder order)
		{
			if (!order.Action.StartsWith("approach", StringComparison.Ordinal) &&
				!order.Action.StartsWith("acquisition", StringComparison.Ordinal) &&
				!order.Action.StartsWith("construction", StringComparison.Ordinal))
				return order.TargetCell.Value;
			var size = Math.Max(1, squad.StealthDefinition?.StrategicCellSize ??
				StealthAISpecialistPolicy.RequiredStrategicCellSize);
			var strategic = order.TargetCell.Value;
			var center = squad.World.Map.Clamp(new CPos(strategic.X * size + size / 2,
				strategic.Y * size + size / 2));
			var members = order.ActorIds.Select(squad.World.GetActorById)
				.Where(actor => actor != null && actor.IsInWorld && !actor.IsDead).ToArray();
			var representative = members.FirstOrDefault();
			var mobile = representative?.TraitOrDefault<Mobile>();
			if (mobile == null)
				return center;
			var occupiedFormationCells = new HashSet<CPos>(members.Select(actor => actor.Location));

			return Enumerable.Range(0, size).SelectMany(y => Enumerable.Range(0, size)
				.Select(x => new CPos(strategic.X * size + x, strategic.Y * size + y)))
				.Where(cell => squad.World.Map.Contains(cell) && !occupiedFormationCells.Contains(cell) &&
					mobile.CanEnterCell(cell, null, BlockedByActor.Immovable))
				.OrderBy(cell => (cell - representative.Location).LengthSquared)
				.ThenBy(cell => (cell - center).LengthSquared)
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X)
				.DefaultIfEmpty(center).First();
		}

		Actor Resolve(uint? actorId)
		{
			if (!actorId.HasValue)
				throw new InvalidOperationException("The runtime order has no target actor.");
			var actor = squad.World.GetActorById(actorId.Value);
			if (actor == null || actor.IsDead || !actor.IsInWorld || actor.ActorID != actorId.Value)
				throw new InvalidOperationException("The runtime order target is not live.");
			return actor;
		}
	}
}
