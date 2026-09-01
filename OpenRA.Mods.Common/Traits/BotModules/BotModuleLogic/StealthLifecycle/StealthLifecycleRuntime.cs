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

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// The sole runtime transition authority for one squad. It validates one accepted typed handoff
	/// at a time and never exposes controller mutation to timers, events, services, or legacy states.
	/// </summary>
	public sealed class StealthLifecycleRuntime
	{
		const int SaveVersion = 1;
		readonly IStealthLifecycleRuntimeOwnerFactory factory;
		readonly StealthLifecycleContext observations;
		readonly StealthLifecycleRuntimeOrders orders;
		readonly StealthLifecycleController controller;
		IStealthLifecycleRuntimeOwner active;
		StealthLifecycleDamageYield pendingDamage;
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
				diagnostics, null, null, null, 1) { }

		StealthLifecycleRuntime(StealthLifecycleController controller,
			IStealthLifecycleRuntimeOwnerFactory factory,
			IStealthLifecycleRuntimeOrderTarget orderTarget,
			IStealthLifecycleCacheService cache, IStealthLifecycleThreatService threats,
			IStealthLifecycleRouteService routes, IStealthLifecycleDiagnosticService diagnostics,
			IStealthLifecycleRuntimeOwner restoredOwner, StealthLifecycleRuntimeOrders restoredOrders,
			StealthLifecycleDamageYield restoredDamage, long nextDamageEventId)
		{
			this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
			this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
			observations = new StealthLifecycleContext(controller, cache, threats, routes, diagnostics, true);
			orders = restoredOrders ?? new StealthLifecycleRuntimeOrders(controller,
				orderTarget ?? throw new ArgumentNullException(nameof(orderTarget)),
				controller.Owner, controller.Epoch);
			active = restoredOwner ?? Create(new StealthLifecycleRuntimeEntry(controller.CurrentHandoff));
			pendingDamage = restoredDamage;
			this.nextDamageEventId = nextDamageEventId;
			ValidateActive();
		}

		public bool ObserveDamage(StealthLifecycleDamageObservation observation)
		{
			if (observation == null)
				throw new ArgumentNullException(nameof(observation));
			if (executing)
				throw new InvalidOperationException("Damage cannot interrupt an active owner execution.");
			if (pendingDamage != null || Epoch.Value > long.MaxValue - 2 ||
				nextDamageEventId == long.MaxValue ||
				!(active is IStealthLifecycleRuntimeDamageOwner fight) ||
				!fight.TryCaptureDamage(observation, nextDamageEventId, out var yielded))
				return false;
			pendingDamage = yielded;
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
				var consumingDamage = pendingDamage != null;
				var result = pendingDamage ?? active.Execute();
				ValidateActive();
				var accepted = Accept(result);
				if (consumingDamage && accepted)
					pendingDamage = null;
				return accepted;
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
					accepted = controller.TryAccept(Require<StealthKiteResult>(result), out var kiteTransition);
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
			var before = controller.ExportState();
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
			var previousController = controller.ExportState();
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
			if (value.UndefendedAttack != null) return new StealthLifecycleRuntimeEntry(value.UndefendedAttack.Handoff, value.UndefendedAttack);
			return new StealthLifecycleRuntimeEntry(value.CrushEvaluation.Handoff, value.CrushEvaluation);
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

		public MiniYamlNode Serialize(string key = "StealthLifecycleRuntime")
		{
			if (executing)
				throw new InvalidOperationException("Cannot save during stealth owner execution.");
			ValidateActive();
			var node = new MiniYamlNode(key, "", new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", SaveVersion.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Enabled", FieldSaver.FormatValue(true)),
				new MiniYamlNode("Owner", Owner.ToString()),
				new MiniYamlNode("Epoch", Epoch.Value.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("LastObservedTick", LastObservedTick.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("NextDamageEventId", nextDamageEventId.ToString(CultureInfo.InvariantCulture)),
				active.Serialize(), orders.Serialize()
			});
			if (pendingDamage != null)
				node.Value.Nodes.Add(StealthRepairPersistence.SerializePendingDamage(pendingDamage));
			return node;
		}

		public static StealthLifecycleRuntime Restore(MiniYamlNode node,
			IStealthLifecycleRuntimeOwnerFactory factory,
			IStealthLifecycleRuntimeOrderTarget orderTarget,
			IStealthLifecycleCacheService cache, IStealthLifecycleThreatService threats,
			IStealthLifecycleRouteService routes, IStealthLifecycleDiagnosticService diagnostics)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));
			var children = node.Value.Nodes;
			var scalars = children.Where(child => child.Key != "ActiveOwner" &&
				child.Key != "OrderSink" && child.Key != "PendingDamage")
				.ToDictionary(child => child.Key, child => child.Value.Value, StringComparer.Ordinal);
			if (scalars.Count != 6 || children.Count(child => child.Key == "ActiveOwner") != 1 ||
				children.Count(child => child.Key == "OrderSink") != 1 ||
				children.Count(child => child.Key == "PendingDamage") > 1 ||
				ReadInt(scalars, "Version") != SaveVersion || !ReadBool(scalars, "Enabled") ||
				!Enum.TryParse(scalars["Owner"], out BehaviorId owner) || !Enum.IsDefined(typeof(BehaviorId), owner) ||
				!long.TryParse(scalars["Epoch"], NumberStyles.None, CultureInfo.InvariantCulture, out var epoch) || epoch <= 0)
				throw new InvalidOperationException("Invalid canonical stealth runtime save shape.");
			var tick = ReadInt(scalars, "LastObservedTick");
			if (tick < -1 || !long.TryParse(scalars["NextDamageEventId"], NumberStyles.None,
				CultureInfo.InvariantCulture, out var nextDamageEventId) || nextDamageEventId <= 0)
				throw new InvalidOperationException("Invalid stealth runtime observation tick.");

			var controller = StealthLifecycleController.Restore(
				new StealthLifecycleSavePayload(owner, new OwnershipEpoch(epoch), tick));
			var orderState = children.Single(child => child.Key == "OrderSink");
			var temporaryOrders = new StealthLifecycleRuntimeOrders(controller, orderTarget, owner, controller.Epoch);
			temporaryOrders.Restore(orderState, owner, controller.Epoch);
			var restoredOwner = factory.Restore(controller.CurrentHandoff, controller, temporaryOrders,
				children.Single(child => child.Key == "ActiveOwner"));
			if (restoredOwner == null || restoredOwner.Owner != owner || restoredOwner.Epoch != controller.Epoch)
				throw new InvalidOperationException("Restored stealth owner does not match the saved controller.");
			var pendingNode = children.SingleOrDefault(child => child.Key == "PendingDamage");
			var pending = pendingNode == null ? null :
				StealthRepairPersistence.RestorePendingDamage(controller.CurrentHandoff, pendingNode);
			if (pending != null && pending.DamageEventId >= nextDamageEventId)
				throw new InvalidOperationException("Pending Damage event sequence is not canonical.");
			return new StealthLifecycleRuntime(controller, factory, orderTarget, cache, threats, routes,
				diagnostics, restoredOwner, temporaryOrders, pending, nextDamageEventId);
		}

		static int ReadInt(Dictionary<string, string> values, string key)
		{
			if (!values.TryGetValue(key, out var text) ||
				!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
				throw new InvalidOperationException("Invalid stealth runtime integer field " + key + ".");
			return value;
		}

		static bool ReadBool(Dictionary<string, string> values, string key)
		{
			if (!values.TryGetValue(key, out var text) || !bool.TryParse(text, out var value))
				throw new InvalidOperationException("Invalid stealth runtime Boolean field " + key + ".");
			return value;
		}
	}
}
