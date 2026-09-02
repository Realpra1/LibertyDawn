#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Concrete owner registration for the per-Squad modular lifecycle runtime.</summary>
	sealed class StealthSquadLifecycleOwnerFactory : IStealthLifecycleRuntimeOwnerFactory
	{
		static readonly BitSet<TargetableType> GroundTargetTypes = new BitSet<TargetableType>("Ground");

		readonly Squad squad;
		readonly StealthSquadLifecycleStrategicAdapter strategic;
		readonly StealthSquadLifecycleCombatLiveAdapter combat;
		readonly StealthSquadLifecycleRecoveryLiveAdapter recovery;
		StealthSquadConstructionMembershipPlan pendingConstructionMembership;

		public StealthSquadLifecycleOwnerFactory(Squad squad,
			StealthSquadLifecycleStrategicAdapter strategic)
		{
			this.squad = squad ?? throw new ArgumentNullException(nameof(squad));
			this.strategic = strategic ?? throw new ArgumentNullException(nameof(strategic));
			combat = new StealthSquadLifecycleCombatLiveAdapter(squad);
			recovery = new StealthSquadLifecycleRecoveryLiveAdapter(squad);
		}

		public IStealthLifecycleRuntimeOwner Create(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleOwnershipGuard ownershipGuard,
			IStealthLifecycleRuntimeOrders runtimeOrders)
		{
			if (entry == null)
				throw new ArgumentNullException(nameof(entry));
			var orders = new StealthSquadLifecycleOrders(runtimeOrders);
			switch (entry.Owner)
			{
				case BehaviorId.Start:
					return Start(entry);
				case BehaviorId.SquadConstruction:
					return SquadConstruction(entry, runtimeOrders);
				case BehaviorId.TargetAcquisition:
					return TargetAcquisition(entry, runtimeOrders);
				case BehaviorId.TargetValueFilter:
					return TargetValue(entry);
				case BehaviorId.TargetThreatFilter:
					return TargetThreat(entry);
				case BehaviorId.TargetDistanceChoice:
					return TargetDistance(entry);
				case BehaviorId.Approach:
					return Approach(entry, orders);
				case BehaviorId.UndefendedAttack:
					return Undefended(entry, ownershipGuard, orders);
				case BehaviorId.CrushEvaluation:
					return Crush(entry, ownershipGuard, orders);
				case BehaviorId.Kite:
					return Kite(entry, ownershipGuard, orders);
				case BehaviorId.MassAttack:
					return MassAttack(entry, ownershipGuard, orders);
				case BehaviorId.RecalculateFlee:
					return RecalculateFlee(entry, ownershipGuard, orders);
				case BehaviorId.Repair:
					return Repair(entry, ownershipGuard, orders);
				default:
					throw new InvalidOperationException("No modular runtime owner is registered for " + entry.Owner + ".");
			}
		}

		IStealthLifecycleRuntimeOwner Start(StealthLifecycleRuntimeEntry entry)
		{
			var behavior = new StealthStartBehavior(entry.Handoff);
			StealthStartResult result = null;
			object Execute()
			{
				var members = ConstructionMembers().Select(member =>
					new StealthStartMemberSnapshot(member.ActorId, member.IsInWorld, member.IsDead)).ToArray();
				var repair = entry.Context as StealthRepairTransition;
				var subject = repair?.StartEntries.Select(item => item.ActorId).FirstOrDefault() ??
					members.Select(member => member.ActorId).FirstOrDefault();
				var source = repair == null ? StealthLifecycleObservationKind.UnitBuilt :
					StealthLifecycleObservationKind.RepairCompleted;
				return result = behavior.Execute(new StealthLifecycleObservation(source, subject), members);
			}

			return Owner(entry, Execute);
		}

		IStealthLifecycleRuntimeOwner SquadConstruction(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleRuntimeOrders runtimeOrders)
		{
			var expected = entry.Context is StealthStartResult start ? start.MemberActorIds.ToArray() :
				combat.Members().Select(actor => actor.ActorID).ToArray();
			var behavior = new StealthSquadConstructionBehavior(entry.Handoff, expected, strategic);
			StealthSquadConstructionResult result = null;
			object Execute()
			{
				var members = ConstructionMembers().ToArray();
				result = behavior.Execute(members, ConstructionSquads());
				pendingConstructionMembership = result.IsComplete ?
					StealthSquadConstructionMembershipPlan.Create(result, squad.StealthSquadIndex,
						members.Where(member => member.IsInWorld && !member.IsDead && member.IsStealthTank)
							.Select(member => member.ActorId)) : null;
				foreach (var assignment in result.Assignments.Where(item =>
					item.Disposition == StealthSquadAssignmentDisposition.RoutedReinforcement))
					runtimeOrders.Issue(new StealthLifecycleRuntimeOrder(entry.Owner, entry.Epoch,
						StealthLifecycleRuntimeOrderKind.Move,
						"construction-route-" + assignment.ActorId, new[] { assignment.ActorId },
						targetCell: assignment.SafeRouteStrategicCells.Last(),
						route: assignment.SafeRouteStrategicCells));
				return result;
			}

			return Owner(entry, Execute);
		}

		internal void CommitConstructionMembership(OwnershipEpoch epoch)
		{
			var plan = pendingConstructionMembership;
			if (plan == null || plan.Epoch != epoch)
				throw new InvalidOperationException("Accepted construction membership plan is missing or stale.");
			var actorIds = plan.ActiveActorIds.Concat(plan.PendingActorIds).ToArray();
			var actors = actorIds.Select(actorId => squad.World.GetActorById(actorId)).ToArray();
			if (actors.Any(actor => actor == null || actor.IsDead || !actor.IsInWorld ||
				!squad.Units.Contains(actor)) ||
				!actors.Select(actor => actor.ActorID).SequenceEqual(actorIds))
				throw new InvalidOperationException("Accepted construction membership changed before commit.");

			var byId = actors.ToDictionary(actor => actor.ActorID);
			foreach (var actorId in plan.ActiveActorIds)
				squad.JoinAirFormation(byId[actorId]);
			foreach (var actorId in plan.PendingActorIds)
				squad.MarkAirReinforcement(byId[actorId]);
			pendingConstructionMembership = null;
		}

		IStealthLifecycleRuntimeOwner TargetAcquisition(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleRuntimeOrders runtimeOrders)
		{
			squad.StealthOverlayConsideredTargets.Clear();
			squad.StealthOverlayChosenTarget = null;
			var behavior = new StealthTargetAcquisitionBehavior(entry.Handoff, strategic);
			StealthTargetAcquisitionResult result = null;
			object Execute() { return result = ExecuteAcquisition(behavior, runtimeOrders); }
			return Owner(entry, Execute);
		}

		StealthTargetAcquisitionResult ExecuteAcquisition(
			StealthTargetAcquisitionBehavior behavior, IStealthLifecycleRuntimeOrders runtimeOrders)
		{
			var result = behavior.Execute(combat.ActiveCenter(), squad.AirTargetStrategicCell);
			squad.StealthOverlayConsideredTargets.Clear();
			squad.StealthOverlayConsideredTargets.AddRange(result.Options.Select(option => option.StrategicCell));
			if (result.Disposition == StealthTargetAcquisitionDisposition.MoveCloserAndRescan &&
				result.MoveCloserStrategicCell.HasValue)
			{
				var members = combat.Members().Select(actor => actor.ActorID).ToArray();
				if (members.Length != 0)
					runtimeOrders.Issue(new StealthLifecycleRuntimeOrder(result.Handoff.Owner,
						result.Handoff.Epoch, StealthLifecycleRuntimeOrderKind.Move,
						"acquisition-rescan", members, targetCell: result.MoveCloserStrategicCell));
			}

			return result;
		}

		IStealthLifecycleRuntimeOwner TargetValue(StealthLifecycleRuntimeEntry entry)
		{
			var handoff = (StealthTargetValueFilterHandoff)entry.Context;
			var behavior = new StealthTargetValueFilterBehavior(handoff);
			StealthTargetValueFilterResult result = null;
			return Owner(entry, () => result = behavior.Execute());
		}

		IStealthLifecycleRuntimeOwner TargetThreat(StealthLifecycleRuntimeEntry entry)
		{
			var handoff = (StealthTargetThreatFilterHandoff)entry.Context;
			var behavior = new StealthTargetThreatFilterBehavior(handoff,
				new GeneralizedCombatTargetThreatAdapter(squad.SquadManager.CombatThreatCalculator));
			StealthTargetThreatFilterResult result = null;
			return Owner(entry, () => result = behavior.Execute());
		}

		IStealthLifecycleRuntimeOwner TargetDistance(StealthLifecycleRuntimeEntry entry)
		{
			var handoff = (StealthTargetDistanceChoiceHandoff)entry.Context;
			var policy = new StealthTargetDistanceChoicePolicy(1000, 3000);
			var behavior = new StealthTargetDistanceChoiceBehavior(
				handoff, combat.OtherActiveSquads(), policy);
			StealthTargetDistanceChoiceResult result = null;
			return Owner(entry, () =>
			{
				result = behavior.Execute();
				if (result.Mission != null)
				{
					squad.AirTargetStrategicCell = result.Mission.StrategicCell;
					squad.StealthOverlayChosenTarget = result.Mission.StrategicCell;
				}

				return result;
			});
		}

		IStealthLifecycleRuntimeOwner Approach(StealthLifecycleRuntimeEntry entry,
			StealthSquadLifecycleOrders orders)
		{
			var handoff = (StealthApproachHandoff)entry.Context;
			var behavior = new StealthApproachBehavior(handoff, strategic, combat, orders);
			return Owner(entry, behavior.Execute);
		}

		IStealthLifecycleRuntimeOwner Undefended(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleOwnershipGuard guard, StealthSquadLifecycleOrders orders)
		{
			var handoff = UndefendedHandoff(entry);
			var behavior = new StealthUndefendedAttackBehavior(handoff, guard, combat,
				new GeneralizedCombatUndefendedAttackThreatAdapter(
					squad.SquadManager.CombatThreatCalculator, combat.Resolve), orders);
			return FightOwner(entry, behavior.Execute);
		}

		IStealthLifecycleRuntimeOwner Crush(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleOwnershipGuard guard, StealthSquadLifecycleOrders orders)
		{
			var handoff = CrushHandoff(entry);
			var behavior = new StealthCrushBehavior(handoff, guard, combat,
				new GeneralizedCombatCrushThreatAdapter(
					squad.SquadManager.CombatThreatCalculator, combat.Resolve), orders);
			return FightOwner(entry, behavior.Execute);
		}

		IStealthLifecycleRuntimeOwner Kite(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleOwnershipGuard guard, StealthSquadLifecycleOrders orders)
		{
			var handoff = KiteHandoff(entry);
			var behavior = new StealthKiteBehavior(handoff, guard, combat,
				new GeneralizedCombatKiteThreatAdapter(squad.SquadManager.CombatThreatCalculator,
					combat.Resolve, GroundTargetTypes), orders);
			return FightOwner(entry, behavior.Execute);
		}

		IStealthLifecycleRuntimeOwner MassAttack(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleOwnershipGuard guard, StealthSquadLifecycleOrders orders)
		{
			var handoff = entry.Context as StealthMassAttackHandoff;
			if (handoff == null)
			{
				var resume = ((StealthRepairFightResumeHandoff)entry.Context).Context;
				handoff = new StealthMassAttackHandoff(entry.Handoff, resume.Mission,
					resume.MassAttackEntryEvidence ?? throw new InvalidOperationException(
						"Repair cannot resume MassAttack without its immutable entry evidence."));
			}

			var behavior = new StealthMassAttackBehavior(handoff, guard, combat,
				new GeneralizedCombatMassAttackThreatAdapter(squad.SquadManager.CombatThreatCalculator,
					combat.Resolve, GroundTargetTypes), orders);
			return FightOwner(entry, behavior.Execute);
		}

		IStealthLifecycleRuntimeOwner RecalculateFlee(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleOwnershipGuard guard, StealthSquadLifecycleOrders orders)
		{
			var handoff = (StealthRecalculateFleeHandoff)entry.Context;
			var behavior = new StealthRecalculateFleeBehavior(handoff, guard,
				new StealthRecalculateFleeLiveWorld(recovery, handoff.Evidence.LiveFingerprint),
				strategic, orders);
			return Owner(entry, behavior.Execute);
		}

		IStealthLifecycleRuntimeOwner Repair(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleOwnershipGuard guard, StealthSquadLifecycleOrders orders)
		{
			var handoff = (StealthRepairHandoff)entry.Context;
			var behavior = new StealthRepairBehavior(handoff, guard,
				new StealthRepairLiveWorld(recovery, handoff),
				new GeneralizedCombatRepairThreatAdapter(squad.SquadManager.CombatThreatCalculator,
					combat.Resolve, GroundTargetTypes), strategic, orders);
			return Owner(entry, behavior.Execute);
		}

		StealthUndefendedAttackHandoff UndefendedHandoff(StealthLifecycleRuntimeEntry entry)
		{
			if (entry.Context is StealthUndefendedAttackHandoff handoff)
				return handoff;
			var resume = ((StealthRepairFightResumeHandoff)entry.Context).Context;
			return new StealthUndefendedAttackHandoff(entry.Handoff, resume.Mission);
		}

		StealthCrushEvaluationHandoff CrushHandoff(StealthLifecycleRuntimeEntry entry)
		{
			if (entry.Context is StealthCrushEvaluationHandoff handoff)
				return handoff;
			var resume = ((StealthRepairFightResumeHandoff)entry.Context).Context;
			return new StealthCrushEvaluationHandoff(entry.Handoff, resume.Mission, resume.EnemyActorIds);
		}

		StealthKiteHandoff KiteHandoff(StealthLifecycleRuntimeEntry entry)
		{
			if (entry.Context is StealthKiteHandoff handoff)
				return handoff;
			var resume = ((StealthRepairFightResumeHandoff)entry.Context).Context;
			return new StealthKiteHandoff(entry.Handoff, resume.Mission, resume.EnemyActorIds);
		}

		IEnumerable<StealthSquadConstructionMemberSnapshot> ConstructionMembers()
		{
			var size = Math.Max(1, squad.StealthDefinition.StrategicCellSize);
			return squad.Units.OrderBy(actor => actor.ActorID).Select(actor =>
				new StealthSquadConstructionMemberSnapshot(actor.ActorID,
					new CPos(actor.Location.X / size, actor.Location.Y / size), squad.StealthSquadIndex,
					actor.IsInWorld, actor.IsDead));
		}

		IEnumerable<StealthSquadConstructionSquadSnapshot> ConstructionSquads()
		{
			return new[]
			{
				new StealthSquadConstructionSquadSnapshot(
					squad.StealthSquadIndex, combat.ActiveCenter())
			};
		}

		static IStealthLifecycleRuntimeOwner Owner(StealthLifecycleRuntimeEntry entry, Func<object> execute)
		{
			return new StealthSquadLifecycleRuntimeOwner(entry.Owner, entry.Epoch, execute);
		}

		IStealthLifecycleRuntimeOwner FightOwner(StealthLifecycleRuntimeEntry entry,
			Func<object> execute)
		{
			var damage = new StealthSquadLifecycleDamageAdapter(entry, combat);
			return new StealthSquadLifecycleRuntimeOwner(
				entry.Owner, entry.Epoch, execute, damage.Capture);
		}
	}
}
