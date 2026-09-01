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

		public IStealthLifecycleRuntimeOwner Restore(StealthBehaviorHandoff handoff,
			IStealthLifecycleOwnershipGuard ownershipGuard,
			IStealthLifecycleRuntimeOrders orders, MiniYamlNode privateState)
		{
			if (handoff == null || privateState == null)
				throw new ArgumentNullException(handoff == null ? nameof(handoff) : nameof(privateState));
			if (privateState.Value.Value == "Pristine" &&
				(handoff.Owner == BehaviorId.Start || handoff.Owner == BehaviorId.TargetAcquisition))
				return Create(new StealthLifecycleRuntimeEntry(handoff), ownershipGuard, orders);
			var runtimeOrders = new StealthSquadLifecycleOrders(orders);
			switch (handoff.Owner)
			{
				case BehaviorId.Start:
					var start = new StealthStartBehavior(handoff);
					var startResult = start.RestorePrivateState(privateState);
					return Owner(handoff, () => startResult = start.Execute(
						new StealthLifecycleObservation(startResult.Source, startResult.SubjectActorId),
						combat.Members().Select(actor => new StealthStartMemberSnapshot(
							actor.ActorID, actor.IsInWorld, actor.IsDead))),
						key => start.SerializePrivateState(startResult, key));
				case BehaviorId.TargetAcquisition:
					var acquisition = new StealthTargetAcquisitionBehavior(handoff, strategic);
					var acquisitionResult = acquisition.RestorePrivateState(privateState);
					return Owner(handoff, () => acquisitionResult = ExecuteAcquisition(
						acquisition, orders),
						key => acquisition.SerializePrivateState(acquisitionResult, key));
				case BehaviorId.TargetValueFilter:
					return RestoreTargetValue(handoff, privateState);
				case BehaviorId.TargetThreatFilter:
					return RestoreTargetThreat(handoff, privateState);
				case BehaviorId.TargetDistanceChoice:
					return RestoreTargetDistance(handoff, privateState);
				default:
					return RestoreLiveOwner(handoff, ownershipGuard, runtimeOrders, privateState);
			}
		}

		IStealthLifecycleRuntimeOwner Start(StealthLifecycleRuntimeEntry entry)
		{
			var behavior = new StealthStartBehavior(entry.Handoff);
			StealthStartResult result = null;
			object Execute()
			{
				var members = combat.Members().Select(actor =>
					new StealthStartMemberSnapshot(actor.ActorID, actor.IsInWorld, actor.IsDead)).ToArray();
				var repair = entry.Context as StealthRepairTransition;
				var subject = repair?.StartEntries.Select(item => item.ActorId).FirstOrDefault() ??
					members.Select(member => member.ActorId).FirstOrDefault();
				var source = repair == null ? StealthLifecycleObservationKind.UnitBuilt :
					StealthLifecycleObservationKind.RepairCompleted;
				return result = behavior.Execute(new StealthLifecycleObservation(source, subject), members);
			}

			return Owner(entry, Execute,
				key => result == null ? Pristine(key) : behavior.SerializePrivateState(result, key));
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
				result = behavior.Execute(ConstructionMembers(), ConstructionSquads());
				foreach (var assignment in result.Assignments.Where(item =>
					item.Disposition == StealthSquadAssignmentDisposition.RoutedReinforcement))
					runtimeOrders.Issue(new StealthLifecycleRuntimeOrder(entry.Owner, entry.Epoch,
						StealthLifecycleRuntimeOrderKind.Move,
						"construction-route-" + assignment.ActorId, new[] { assignment.ActorId },
						targetCell: assignment.SafeRouteStrategicCells.Last(),
						route: assignment.SafeRouteStrategicCells));
				return result;
			}

			return Owner(entry, Execute,
				key => result == null ? StealthSquadLifecycleFactoryPersistence.PristineConstruction(
					key, expected) : behavior.SerializePrivateState(result, key));
		}

		IStealthLifecycleRuntimeOwner TargetAcquisition(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleRuntimeOrders runtimeOrders)
		{
			var behavior = new StealthTargetAcquisitionBehavior(entry.Handoff, strategic);
			StealthTargetAcquisitionResult result = null;
			object Execute() { return result = ExecuteAcquisition(behavior, runtimeOrders); }
			return Owner(entry, Execute,
				key => result == null ? Pristine(key) : behavior.SerializePrivateState(result, key));
		}

		StealthTargetAcquisitionResult ExecuteAcquisition(
			StealthTargetAcquisitionBehavior behavior, IStealthLifecycleRuntimeOrders runtimeOrders)
		{
			var result = behavior.Execute(combat.ActiveCenter(), squad.AirTargetStrategicCell);
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
			return Owner(entry, () => result = behavior.Execute(),
				key => behavior.SerializePrivateState(result ?? (result = behavior.Execute()), key));
		}

		IStealthLifecycleRuntimeOwner RestoreTargetValue(
			StealthBehaviorHandoff handoff, MiniYamlNode state)
		{
			var typed = StealthTargetValueFilterBehavior.RestoreHandoff(handoff, state);
			var behavior = new StealthTargetValueFilterBehavior(typed);
			var result = behavior.RestorePrivateState(state);
			return Owner(handoff, () => result = behavior.Execute(),
				key => behavior.SerializePrivateState(result, key));
		}

		IStealthLifecycleRuntimeOwner TargetThreat(StealthLifecycleRuntimeEntry entry)
		{
			var handoff = (StealthTargetThreatFilterHandoff)entry.Context;
			var behavior = new StealthTargetThreatFilterBehavior(handoff,
				new GeneralizedCombatTargetThreatAdapter(squad.SquadManager.CombatThreatCalculator));
			StealthTargetThreatFilterResult result = null;
			return Owner(entry, () => result = behavior.Execute(),
				key => behavior.SerializePrivateState(result ?? (result = behavior.Execute()), key));
		}

		IStealthLifecycleRuntimeOwner RestoreTargetThreat(
			StealthBehaviorHandoff handoff, MiniYamlNode state)
		{
			var typed = StealthTargetThreatFilterBehavior.RestoreHandoff(handoff, state);
			var behavior = new StealthTargetThreatFilterBehavior(typed,
				new GeneralizedCombatTargetThreatAdapter(squad.SquadManager.CombatThreatCalculator));
			var result = behavior.RestorePrivateState(state);
			return Owner(handoff, () => result = behavior.Execute(),
				key => behavior.SerializePrivateState(result, key));
		}

		IStealthLifecycleRuntimeOwner TargetDistance(StealthLifecycleRuntimeEntry entry)
		{
			var handoff = (StealthTargetDistanceChoiceHandoff)entry.Context;
			var policy = new StealthTargetDistanceChoicePolicy(1000, 3000);
			var behavior = new StealthTargetDistanceChoiceBehavior(
				handoff, combat.OtherActiveSquads(), policy);
			StealthTargetDistanceChoiceResult result = null;
			return Owner(entry, () => result = behavior.Execute(),
				key => behavior.SerializePrivateState(result ?? (result = behavior.Execute()), key));
		}

		IStealthLifecycleRuntimeOwner RestoreTargetDistance(
			StealthBehaviorHandoff handoff, MiniYamlNode state)
		{
			var typed = StealthTargetDistanceChoiceBehavior.RestoreHandoff(handoff, state);
			var behavior = new StealthTargetDistanceChoiceBehavior(typed,
				combat.OtherActiveSquads(), new StealthTargetDistanceChoicePolicy(1000, 3000));
			var result = behavior.RestorePrivateState(state);
			return Owner(handoff, () => result = behavior.Execute(),
				key => behavior.SerializePrivateState(result, key));
		}

		IStealthLifecycleRuntimeOwner RestoreLiveOwner(StealthBehaviorHandoff handoff,
			IStealthLifecycleOwnershipGuard guard, StealthSquadLifecycleOrders orders,
			MiniYamlNode state)
		{
			var missionNode = state.Value.Nodes.SingleOrDefault(child => child.Key == "Mission");
			var mission = missionNode == null ? null : StealthApproachPersistence.RestoreMission(missionNode);
			switch (handoff.Owner)
			{
				case BehaviorId.SquadConstruction:
					var pristineConstruction = state.Value.Value == "Pristine";
					var expected = pristineConstruction ?
						StealthSquadLifecycleFactoryPersistence.RestorePristineConstruction(state) :
						squad.Units.Where(actor => actor != null)
							.Select(actor => actor.ActorID).OrderBy(id => id).ToArray();
					var construction = new StealthSquadConstructionBehavior(handoff, expected, strategic);
					StealthSquadConstructionResult constructionResult = null;
					if (!pristineConstruction)
						constructionResult = construction.RestorePrivateState(state);
					return Owner(handoff, () => constructionResult = construction.Execute(
						ConstructionMembers(), ConstructionSquads()),
						key => constructionResult == null ?
							StealthSquadLifecycleFactoryPersistence.PristineConstruction(key, expected) :
							construction.SerializePrivateState(constructionResult, key));
				case BehaviorId.Approach:
					var approach = new StealthApproachBehavior(
						new StealthApproachHandoff(handoff, mission), strategic, combat,
						new GeneralizedCombatTargetThreatAdapter(
							squad.SquadManager.CombatThreatCalculator), orders);
					approach.RestorePrivateState(state);
					return Owner(handoff, approach.Execute, approach.SerializePrivateState);
				case BehaviorId.UndefendedAttack:
					var undefendedHandoff = new StealthUndefendedAttackHandoff(handoff, mission);
					var undefended = new StealthUndefendedAttackBehavior(
						undefendedHandoff, guard, combat,
						new GeneralizedCombatUndefendedAttackThreatAdapter(
							squad.SquadManager.CombatThreatCalculator, combat.Resolve), orders);
					undefended.RestorePrivateState(state);
					return FightOwner(new StealthLifecycleRuntimeEntry(handoff, undefendedHandoff),
						undefended.Execute, undefended.SerializePrivateState);
				case BehaviorId.CrushEvaluation:
					var crushHandoff = new StealthCrushEvaluationHandoff(
						handoff, mission, ReadIds(state, "IncomingDefenderActorId"));
					var crush = new StealthCrushBehavior(crushHandoff, guard, combat,
						new GeneralizedCombatCrushThreatAdapter(
							squad.SquadManager.CombatThreatCalculator, combat.Resolve), orders);
					crush.RestorePrivateState(state);
					return FightOwner(new StealthLifecycleRuntimeEntry(handoff, crushHandoff),
						crush.Execute, crush.SerializePrivateState);
				case BehaviorId.Kite:
					var kiteHandoff = new StealthKiteHandoff(
						handoff, mission, ReadIds(state, "IncomingDefenderId"));
					var kite = new StealthKiteBehavior(kiteHandoff, guard, combat,
						new GeneralizedCombatKiteThreatAdapter(squad.SquadManager.CombatThreatCalculator,
							combat.Resolve, GroundTargetTypes), orders);
					kite.RestorePrivateState(state);
					return FightOwner(new StealthLifecycleRuntimeEntry(handoff, kiteHandoff),
						kite.Execute, kite.SerializePrivateState);
				case BehaviorId.MassAttack:
					var massHandoff = new StealthMassAttackHandoff(handoff, mission,
						StealthMassAttackPersistenceNodes.RestoreEntry(Required(state, "Entry")));
					var mass = new StealthMassAttackBehavior(massHandoff,
						guard, combat, new GeneralizedCombatMassAttackThreatAdapter(
							squad.SquadManager.CombatThreatCalculator, combat.Resolve, GroundTargetTypes), orders);
					mass.RestorePersistedState(state);
					return FightOwner(new StealthLifecycleRuntimeEntry(handoff, massHandoff),
						mass.Execute, mass.SerializePrivateState);
				case BehaviorId.RecalculateFlee:
					var fleeHandoff = new StealthRecalculateFleeHandoff(handoff, mission,
						StealthRecalculateFleePersistence.RestoreEntry(Required(state, "Entry")));
					var flee = new StealthRecalculateFleeBehavior(fleeHandoff, guard,
						new StealthRecalculateFleeLiveWorld(recovery, fleeHandoff.Evidence.LiveFingerprint),
						new GeneralizedCombatRecalculateFleeThreatAdapter(
							squad.SquadManager.CombatThreatCalculator, combat.Resolve, GroundTargetTypes),
						strategic, orders);
					flee.RestorePersistedState(state);
					return Owner(handoff, flee.Execute, flee.SerializePrivateState);
				case BehaviorId.Repair:
					var repairHandoff = StealthRepairPersistence.RestoreHandoff(handoff, state);
					var repair = new StealthRepairBehavior(repairHandoff, guard,
						new StealthRepairLiveWorld(recovery, repairHandoff),
						new GeneralizedCombatRepairThreatAdapter(squad.SquadManager.CombatThreatCalculator,
							combat.Resolve, GroundTargetTypes), strategic, orders);
					repair.RestorePersistedState(state);
					return Owner(handoff, repair.Execute, repair.SerializePrivateState);
				default:
					throw new InvalidOperationException(
						"Live modular owner restore is not registered for " + handoff.Owner + ".");
			}
		}

		static MiniYamlNode Required(MiniYamlNode parent, string key)
		{
			var nodes = parent.Value.Nodes.Where(child => child.Key == key).ToArray();
			if (nodes.Length != 1)
				throw new InvalidOperationException("Expected exactly one " + key + " persistence node.");
			return nodes[0];
		}

		static uint[] ReadIds(MiniYamlNode parent, string key)
		{
			return parent.Value.Nodes.Where(child => child.Key == key)
				.Select(child => FieldLoader.GetValue<uint>(key, child.Value.Value)).ToArray();
		}

		IStealthLifecycleRuntimeOwner Approach(StealthLifecycleRuntimeEntry entry,
			StealthSquadLifecycleOrders orders)
		{
			var handoff = (StealthApproachHandoff)entry.Context;
			var behavior = new StealthApproachBehavior(handoff, strategic, combat,
				new GeneralizedCombatTargetThreatAdapter(squad.SquadManager.CombatThreatCalculator), orders);
			return Owner(entry, behavior.Execute, behavior.SerializePrivateState);
		}

		IStealthLifecycleRuntimeOwner Undefended(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleOwnershipGuard guard, StealthSquadLifecycleOrders orders)
		{
			var handoff = UndefendedHandoff(entry);
			var behavior = new StealthUndefendedAttackBehavior(handoff, guard, combat,
				new GeneralizedCombatUndefendedAttackThreatAdapter(
					squad.SquadManager.CombatThreatCalculator, combat.Resolve), orders);
			return FightOwner(entry, behavior.Execute, behavior.SerializePrivateState);
		}

		IStealthLifecycleRuntimeOwner Crush(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleOwnershipGuard guard, StealthSquadLifecycleOrders orders)
		{
			var handoff = CrushHandoff(entry);
			var behavior = new StealthCrushBehavior(handoff, guard, combat,
				new GeneralizedCombatCrushThreatAdapter(
					squad.SquadManager.CombatThreatCalculator, combat.Resolve), orders);
			return FightOwner(entry, behavior.Execute, behavior.SerializePrivateState);
		}

		IStealthLifecycleRuntimeOwner Kite(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleOwnershipGuard guard, StealthSquadLifecycleOrders orders)
		{
			var handoff = KiteHandoff(entry);
			var behavior = new StealthKiteBehavior(handoff, guard, combat,
				new GeneralizedCombatKiteThreatAdapter(squad.SquadManager.CombatThreatCalculator,
					combat.Resolve, GroundTargetTypes), orders);
			return FightOwner(entry, behavior.Execute, behavior.SerializePrivateState);
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
			return FightOwner(entry, behavior.Execute, behavior.SerializePrivateState);
		}

		IStealthLifecycleRuntimeOwner RecalculateFlee(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleOwnershipGuard guard, StealthSquadLifecycleOrders orders)
		{
			var handoff = (StealthRecalculateFleeHandoff)entry.Context;
			var behavior = new StealthRecalculateFleeBehavior(handoff, guard,
				new StealthRecalculateFleeLiveWorld(recovery, handoff.Evidence.LiveFingerprint),
				new GeneralizedCombatRecalculateFleeThreatAdapter(
					squad.SquadManager.CombatThreatCalculator, combat.Resolve, GroundTargetTypes),
				strategic, orders);
			return Owner(entry, behavior.Execute, behavior.SerializePrivateState);
		}

		IStealthLifecycleRuntimeOwner Repair(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleOwnershipGuard guard, StealthSquadLifecycleOrders orders)
		{
			var handoff = (StealthRepairHandoff)entry.Context;
			var behavior = new StealthRepairBehavior(handoff, guard,
				new StealthRepairLiveWorld(recovery, handoff),
				new GeneralizedCombatRepairThreatAdapter(squad.SquadManager.CombatThreatCalculator,
					combat.Resolve, GroundTargetTypes), strategic, orders);
			return Owner(entry, behavior.Execute, behavior.SerializePrivateState);
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

		static IStealthLifecycleRuntimeOwner Owner(StealthLifecycleRuntimeEntry entry, Func<object> execute,
			Func<string, MiniYamlNode> serialize)
		{
			return new StealthSquadLifecycleRuntimeOwner(entry.Owner, entry.Epoch, execute, serialize);
		}

		IStealthLifecycleRuntimeOwner FightOwner(StealthLifecycleRuntimeEntry entry,
			Func<object> execute, Func<string, MiniYamlNode> serialize)
		{
			var damage = new StealthSquadLifecycleDamageAdapter(entry, combat);
			return new StealthSquadLifecycleRuntimeOwner(entry.Owner, entry.Epoch, execute, serialize, damage.Capture);
		}

		static IStealthLifecycleRuntimeOwner Owner(StealthBehaviorHandoff handoff,
			Func<object> execute, Func<string, MiniYamlNode> serialize)
		{
			return new StealthSquadLifecycleRuntimeOwner(
				handoff.Owner, handoff.Epoch, execute, serialize);
		}

		static MiniYamlNode Pristine(string key)
		{
			return new MiniYamlNode(key, "Pristine", new List<MiniYamlNode>());
		}
	}
}
