#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 or
 * later. For more information, see COPYING.
 */
#endregion

using System;
using System.Linq;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Permanent owner, handoff, observation, and sole-order-sink evidence.</summary>
	sealed class StealthSquadLifecycleTelemetry : IStealthLifecycleDiagnosticService
	{
		public StealthSquadLifecycleTelemetry(Squad squad)
		{
			if (squad == null)
				throw new ArgumentNullException(nameof(squad));
		}

		public void Record(StealthLifecycleDiagnostic diagnostic)
		{
			// Observation is deliberately passive. Accepted handoffs are recorded after owner execution.
		}

		internal static void RecordActivation(Squad squad, StealthLifecycleRuntime runtime, bool restored)
		{
			Log.Write("debug", "Stealth modular lifecycle [{0}] squad={1}#{2} tick={3} " +
				"activation={4} owner={5} epoch={6} typed-handoff={7} members=[{8}] " +
				"authority=modular legacy-authority=False order-sinks=1.",
				squad.StealthProfile, squad.StealthSquadDefinition, squad.StealthSquadIndex,
				squad.World.WorldTick, restored ? "restored" : "new", runtime.Owner,
				runtime.Epoch.Value, Handoff(runtime.Owner), Members(squad));
		}

		internal static void RecordHandoff(Squad squad, BehaviorId previousOwner,
			OwnershipEpoch previousEpoch, BehaviorId owner, OwnershipEpoch epoch)
		{
			if (previousOwner == owner && previousEpoch == epoch)
				return;
			Log.Write("debug", "Stealth modular handoff [{0}] squad={1}#{2} tick={3} " +
				"from-owner={4} from-epoch={5} owner={6} epoch={7} typed-handoff={8} " +
				"trigger=active-owner-tick timer-transition=False event-transition=False members=[{9}].",
				squad.StealthProfile, squad.StealthSquadDefinition, squad.StealthSquadIndex,
				squad.World.WorldTick, previousOwner, previousEpoch.Value, owner, epoch.Value,
				Handoff(owner), Members(squad));
		}

		internal static void RecordDamageObservation(Squad squad, BehaviorId owner,
			OwnershipEpoch epoch, bool accepted)
		{
			if (!accepted)
				return;
			Log.Write("debug", "Stealth modular observation [{0}] squad={1}#{2} tick={3} " +
				"kind=damage captured={4} owner={5} epoch={6} transition=False.",
				squad.StealthProfile, squad.StealthSquadDefinition, squad.StealthSquadIndex,
				squad.World.WorldTick, accepted, owner, epoch.Value);
		}

		internal static void RecordOrder(Squad squad, StealthLifecycleRuntimeOrder order)
		{
			Log.Write("debug", "Stealth modular order [{0}] squad={1}#{2} tick={3} owner={4} " +
				"epoch={5} sink=1 kind={6} action={7} fingerprint={8} actors=[{9}] target={10}.",
				squad.StealthProfile, squad.StealthSquadDefinition, squad.StealthSquadIndex,
				squad.World.WorldTick, order.Owner, order.Epoch.Value, order.Kind, order.Action,
				order.Fingerprint, order.ActorIds.JoinWith(","),
				order.TargetActorId?.ToString() ?? order.TargetCell?.ToString() ?? "none");
		}

		static string Members(Squad squad)
		{
			return squad.Units.Where(actor => actor != null && actor.IsInWorld && !actor.IsDead)
				.OrderBy(actor => actor.ActorID).Select(actor => actor.ActorID).JoinWith(",");
		}

		static string Handoff(BehaviorId owner)
		{
			return owner == BehaviorId.TargetValueFilter ? nameof(StealthTargetValueFilterHandoff) :
				owner == BehaviorId.TargetThreatFilter ? nameof(StealthTargetThreatFilterHandoff) :
				owner == BehaviorId.TargetDistanceChoice ? nameof(StealthTargetDistanceChoiceHandoff) :
				owner == BehaviorId.Approach ? nameof(StealthApproachHandoff) :
				owner == BehaviorId.UndefendedAttack ? nameof(StealthUndefendedAttackHandoff) :
				owner == BehaviorId.CrushEvaluation ? nameof(StealthCrushEvaluationHandoff) :
				owner == BehaviorId.Kite ? nameof(StealthKiteHandoff) :
				owner == BehaviorId.MassAttack ? nameof(StealthMassAttackHandoff) :
				owner == BehaviorId.RecalculateFlee ? nameof(StealthRecalculateFleeHandoff) :
				owner == BehaviorId.Repair ? nameof(StealthRepairHandoff) : nameof(StealthBehaviorHandoff);
		}
	}
}
