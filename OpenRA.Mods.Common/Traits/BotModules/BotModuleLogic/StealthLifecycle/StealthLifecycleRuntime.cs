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

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// The sole runtime transition authority for one squad. It validates one accepted typed handoff
	/// at a time and never exposes controller mutation to timers, events, services, or legacy states.
	/// </summary>
	public sealed class StealthLifecycleRuntime
	{
		readonly IStealthLifecycleRuntimeOwnerFactory factory;
		readonly StealthLifecycleContext observations;
		readonly StealthLifecycleRuntimeOrders orders;
		readonly StealthLifecycleController controller;
		IStealthLifecycleRuntimeOwner active;
		long nextDamageEventId;
		bool executing;

		public BehaviorId Owner => controller.Owner;
		public OwnershipEpoch Epoch => controller.Epoch;
		public int LastObservedTick => controller.LastObservedTick;
		public bool Enabled => true;

		public StealthLifecycleRuntime(IStealthLifecycleRuntimeOwnerFactory factory,
			IStealthLifecycleRuntimeOrderTarget orderTarget,
			IStealthLifecycleCacheService cache, IStealthLifecycleThreatService threats,
			IStealthLifecycleRouteService routes, IStealthLifecycleDiagnosticService diagnostics)
			: this(new StealthLifecycleController(), factory, orderTarget, cache, threats, routes,
				diagnostics) { }

		public StealthLifecycleRuntime(BehaviorId initialOwner,
			IStealthLifecycleRuntimeOwnerFactory factory,
			IStealthLifecycleRuntimeOrderTarget orderTarget,
			IStealthLifecycleCacheService cache, IStealthLifecycleThreatService threats,
			IStealthLifecycleRouteService routes, IStealthLifecycleDiagnosticService diagnostics)
			: this(new StealthLifecycleController(initialOwner), factory, orderTarget, cache, threats,
				routes, diagnostics) { }

		StealthLifecycleRuntime(StealthLifecycleController controller,
			IStealthLifecycleRuntimeOwnerFactory factory,
			IStealthLifecycleRuntimeOrderTarget orderTarget,
			IStealthLifecycleCacheService cache, IStealthLifecycleThreatService threats,
			IStealthLifecycleRouteService routes, IStealthLifecycleDiagnosticService diagnostics)
		{
			this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
			this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
			observations = new StealthLifecycleContext(controller, cache, threats, routes, diagnostics, true);
			orders = new StealthLifecycleRuntimeOrders(controller,
				orderTarget ?? throw new ArgumentNullException(nameof(orderTarget)),
				controller.Owner, controller.Epoch);
			active = Create(new StealthLifecycleRuntimeEntry(controller.CurrentHandoff));
			nextDamageEventId = 1;
			ValidateActive();
		}

		public bool ObserveDamage(StealthLifecycleDamageObservation observation)
		{
			if (observation == null)
				throw new ArgumentNullException(nameof(observation));
			if (executing)
				throw new InvalidOperationException("Damage cannot interrupt an active owner execution.");
			if (Epoch.Value > long.MaxValue - 2 ||
				nextDamageEventId == long.MaxValue ||
				!(active is IStealthLifecycleRuntimeDamageOwner fight) ||
				!fight.TryCaptureDamage(observation, nextDamageEventId, out _))
				return false;
			nextDamageEventId++;
			return true;
		}

		public void Observe(StealthLifecycleObservationFrame frame)
		{
			if (executing)
				throw new InvalidOperationException("Runtime observations cannot interrupt an active owner.");
			observations.Observe(frame);
		}

		public bool Tick()
		{
			if (executing)
				throw new InvalidOperationException("Recursive stealth lifecycle execution is not allowed.");
			ValidateActive();
			executing = true;
			try
			{
				var result = active.Execute();
				ValidateActive();
				return Accept(result);
			}
			finally
			{
				executing = false;
			}
		}

		bool Accept(object result)
		{
			if (result == null)
				throw new InvalidOperationException("The active stealth owner returned no result.");
			if (result is StealthLifecycleDamageYield damage)
				return AcceptDamage(damage);

			StealthLifecycleRuntimeEntry next = null;
			var accepted = false;
			switch (controller.Owner)
			{
				case BehaviorId.Start:
					var start = Require<StealthStartResult>(result);
					accepted = controller.TryAccept(start, out var construction);
					if (accepted)
						next = new StealthLifecycleRuntimeEntry(construction, start);
					break;
				case BehaviorId.SquadConstruction:
					accepted = controller.TryAccept(Require<StealthSquadConstructionResult>(result),
						out var acquisition);
					if (accepted)
						next = new StealthLifecycleRuntimeEntry(acquisition);
					break;
				case BehaviorId.TargetAcquisition:
					accepted = controller.TryAccept(Require<StealthTargetAcquisitionResult>(result), out var value);
					if (accepted)
						next = new StealthLifecycleRuntimeEntry(value.Handoff, value);
					break;
				case BehaviorId.TargetValueFilter:
					accepted = controller.TryAccept(Require<StealthTargetValueFilterResult>(result), out var threat);
					if (accepted)
						next = new StealthLifecycleRuntimeEntry(threat.Handoff, threat);
					break;
				case BehaviorId.TargetThreatFilter:
					accepted = controller.TryAccept(Require<StealthTargetThreatFilterResult>(result), out var distance);
					if (accepted)
						next = new StealthLifecycleRuntimeEntry(distance.Handoff, distance);
					break;
				case BehaviorId.TargetDistanceChoice:
					accepted = controller.TryAccept(Require<StealthTargetDistanceChoiceResult>(result), out var approach);
					if (accepted)
						next = new StealthLifecycleRuntimeEntry(approach.Handoff, approach);
					break;
				case BehaviorId.Approach:
					accepted = controller.TryAccept(Require<StealthApproachResult>(result), out var approachTransition);
					if (accepted)
						next = Entry(approachTransition);
					break;
				case BehaviorId.UndefendedAttack:
					accepted = controller.TryAccept(Require<StealthUndefendedAttackResult>(result), out var attackTransition);
					if (accepted)
						next = Entry(attackTransition);
					break;
				case BehaviorId.CrushEvaluation:
					accepted = controller.TryAccept(Require<StealthCrushResult>(result), out var crushTransition);
					if (accepted)
						next = Entry(crushTransition);
					break;
				case BehaviorId.Kite:
					var kite = Require<StealthKiteResult>(result);
					accepted = controller.TryAccept(kite, out var kiteTransition);
					if (!accepted)
						Log.Write("debug", "Stealth lifecycle rejected Kite result: disposition={0} " +
							"members={1} defenders={2} objectives={3} target={4} cell={5} " +
							"safety={6}/{7} fallback={8}.", kite.Disposition,
							kite.ActiveMemberActorIds.Count, kite.LiveDefenderActorIds.Count,
							kite.LiveObjectiveActorIds.Count, kite.SelectedTargetActorId?.ToString() ?? "none",
							kite.FireCell?.ToString() ?? "none", kite.Safety.HasValue,
							kite.Safety?.Approved ?? false, kite.FallbackEvidence?.Reason.ToString() ?? "none");
					if (accepted)
						next = Entry(kiteTransition);
					break;
				case BehaviorId.MassAttack:
					accepted = controller.TryAccept(Require<StealthMassAttackResult>(result), out var massTransition);
					if (accepted)
						next = Entry(massTransition);
					break;
				case BehaviorId.RecalculateFlee:
					accepted = controller.TryAccept(Require<StealthRecalculateFleeResult>(result), out var fleeTransition);
					if (accepted && fleeTransition.TargetAcquisition != null)
						next = new StealthLifecycleRuntimeEntry(fleeTransition.TargetAcquisition);
					break;
				case BehaviorId.Repair:
					accepted = controller.TryAccept(Require<StealthRepairResult>(result), out var repairTransition);
					if (accepted)
						next = Entry(repairTransition);
					break;
				default:
					throw new InvalidOperationException("The runtime has no executable owner for " + controller.Owner + ".");
			}

			if (!accepted)
				return false;
			if (next == null)
				return true; // A validated retaining result keeps the same owner instance and private state.
			Install(next);
			return true;
		}

		bool AcceptDamage(StealthLifecycleDamageYield yielded)
		{
			var before = controller.CaptureState();
			try
			{
				if (!controller.TryAccept(yielded, out var request) ||
					!controller.TryAccept(request, out var repair))
					return false;
				Install(new StealthLifecycleRuntimeEntry(repair.Handoff, repair));
				return true;
			}
			catch
			{
				controller.RestoreState(before);
				throw;
			}
		}

		void Install(StealthLifecycleRuntimeEntry entry)
		{
			var previousController = controller.CaptureState();
			var previousOwner = active;
			try
			{
				var created = Create(entry);
				orders.Reset(entry.Owner, entry.Epoch);
				active = created;
				ValidateActive();
			}
			catch
			{
				controller.RestoreState(previousController);
				active = previousOwner;
				throw;
			}
		}

		IStealthLifecycleRuntimeOwner Create(StealthLifecycleRuntimeEntry entry)
		{
			var created = factory.Create(entry, controller, orders);
			if (created == null || created.Owner != entry.Owner || created.Epoch != entry.Epoch)
				throw new InvalidOperationException("The runtime factory returned a mismatched owner.");
			return created;
		}

		void ValidateActive()
		{
			if (active == null || active.Owner != controller.Owner || active.Epoch != controller.Epoch ||
				!controller.IsActive(active.Owner, active.Epoch))
				throw new InvalidOperationException("The stealth runtime owner and controller are inconsistent.");
		}

		static T Require<T>(object result) where T : class
		{
			return result as T ?? throw new InvalidOperationException(
				"The active stealth owner returned an invalid result type.");
		}

		static StealthLifecycleRuntimeEntry Entry(StealthApproachTransition value)
		{
			if (value.Reacquisition != null) return new StealthLifecycleRuntimeEntry(value.Reacquisition);
			if (value.RecalculateFlee != null) return new StealthLifecycleRuntimeEntry(
				value.RecalculateFlee.Handoff, value.RecalculateFlee);
			if (value.UndefendedAttack != null) return new StealthLifecycleRuntimeEntry(value.UndefendedAttack.Handoff, value.UndefendedAttack);
			return new StealthLifecycleRuntimeEntry(value.Kite.Handoff, value.Kite);
		}

		static StealthLifecycleRuntimeEntry Entry(StealthUndefendedAttackTransition value)
		{
			if (value.Retained != null) return null;
			if (value.Reacquisition != null) return new StealthLifecycleRuntimeEntry(value.Reacquisition);
			return new StealthLifecycleRuntimeEntry(value.CrushEvaluation.Handoff, value.CrushEvaluation);
		}

		static StealthLifecycleRuntimeEntry Entry(StealthCrushTransition value)
		{
			if (value.Retained != null) return null;
			if (value.Kite != null) return new StealthLifecycleRuntimeEntry(value.Kite.Handoff, value.Kite);
			if (value.UndefendedAttack != null) return new StealthLifecycleRuntimeEntry(value.UndefendedAttack.Handoff, value.UndefendedAttack);
			return new StealthLifecycleRuntimeEntry(value.Reacquisition);
		}

		static StealthLifecycleRuntimeEntry Entry(StealthKiteTransition value)
		{
			if (value.Retained != null) return null;
			if (value.CrushEvaluation != null) return new StealthLifecycleRuntimeEntry(value.CrushEvaluation.Handoff, value.CrushEvaluation);
			if (value.UndefendedAttack != null) return new StealthLifecycleRuntimeEntry(value.UndefendedAttack.Handoff, value.UndefendedAttack);
			if (value.Reacquisition != null) return new StealthLifecycleRuntimeEntry(value.Reacquisition);
			if (value.MassAttackEntry != null) return new StealthLifecycleRuntimeEntry(value.MassAttackEntry.Handoff, value.MassAttackEntry);
			if (value.RecalculateFleeEntry != null) return new StealthLifecycleRuntimeEntry(value.RecalculateFleeEntry.Handoff, value.RecalculateFleeEntry);
			return new StealthLifecycleRuntimeEntry(value.SquadConstructionEntry.Handoff, value.SquadConstructionEntry);
		}

		static StealthLifecycleRuntimeEntry Entry(StealthMassAttackTransition value)
		{
			if (value.Retained != null) return null;
			if (value.UndefendedAttack != null) return new StealthLifecycleRuntimeEntry(value.UndefendedAttack.Handoff, value.UndefendedAttack);
			if (value.Reacquisition != null) return new StealthLifecycleRuntimeEntry(value.Reacquisition);
			if (value.RecalculateFleeEntry != null) return new StealthLifecycleRuntimeEntry(value.RecalculateFleeEntry.Handoff, value.RecalculateFleeEntry);
			return new StealthLifecycleRuntimeEntry(value.SquadConstructionEntry.Handoff, value.SquadConstructionEntry);
		}

		static StealthLifecycleRuntimeEntry Entry(StealthRepairTransition value)
		{
			if (value.Retained != null) return null;
			if (value.ResumedFight != null) return new StealthLifecycleRuntimeEntry(value.ResumedFight.Handoff, value.ResumedFight);
			if (value.StartEntries.Count != 0) return new StealthLifecycleRuntimeEntry(value.StartEntries[0].Handoff, value);
			return new StealthLifecycleRuntimeEntry(value.SquadConstructionEntry.Handoff, value.SquadConstructionEntry);
		}
	}
}
