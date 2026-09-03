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
using System.Diagnostics;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Owns the concrete services and sole lifecycle runtime registered for one Squad.</summary>
	sealed class StealthSquadLifecycleRuntimeHost
	{
		const int MaxImmediateHandoffs = 16;

		readonly Squad squad;
		readonly StealthSquadLifecycleStrategicAdapter strategic;
		readonly StealthSquadLifecycleOwnerFactory factory;
		readonly StealthLifecycleRuntime runtime;
		public BehaviorId Phase => runtime.Owner;

		public StealthSquadLifecycleRuntimeHost(Squad squad)
		{
			this.squad = squad ?? throw new ArgumentNullException(nameof(squad));
			strategic = new StealthSquadLifecycleStrategicAdapter(squad);
			factory = new StealthSquadLifecycleOwnerFactory(squad, strategic);
			var passive = new StealthLifecyclePassiveServices();
			runtime = new StealthLifecycleRuntime(
				factory,
				new StealthSquadLifecycleOrderTarget(squad), strategic, passive, passive,
				new StealthSquadLifecycleTelemetry(squad));
			StealthSquadLifecycleTelemetry.RecordActivation(squad, runtime, false);
		}

		StealthSquadLifecycleRuntimeHost(Squad squad, bool loaded)
		{
			this.squad = squad ?? throw new ArgumentNullException(nameof(squad));
			strategic = new StealthSquadLifecycleStrategicAdapter(squad);
			factory = new StealthSquadLifecycleOwnerFactory(squad, strategic);
			var passive = new StealthLifecyclePassiveServices();
			runtime = new StealthLifecycleRuntime(BehaviorId.TargetAcquisition, factory,
				new StealthSquadLifecycleOrderTarget(squad), strategic, passive, passive,
				new StealthSquadLifecycleTelemetry(squad));
			StealthSquadLifecycleTelemetry.RecordActivation(squad, runtime, loaded);
		}

		public static StealthSquadLifecycleRuntimeHost ForLoadedSquad(Squad squad)
		{
			return new StealthSquadLifecycleRuntimeHost(squad, true);
		}

		public void Tick()
		{
			EnforceLifecycleStance();
			if ((runtime.Owner == BehaviorId.Start || runtime.Owner == BehaviorId.SquadConstruction) &&
				StealthAIStateBase.MaintainInitialStealthRepairsForModularLifecycle(squad))
				return;
			PromoteArrivedReinforcements();
			StealthAIStateBase.RoutePendingStealthReinforcementsForModularLifecycle(squad);
			var observations = squad.Units.Where(actor => actor != null && actor.IsInWorld && !actor.IsDead)
				.Select(actor => new StealthLifecycleObservation(
					StealthLifecycleObservationKind.Timer, actor.ActorID)).ToArray();
			runtime.Observe(new StealthLifecycleObservationFrame(squad.World.WorldTick, observations));
			AdvanceUntilRetained();
		}

		public void TickLocalSafety()
		{
			EnforceLifecycleStance();
			PromoteArrivedReinforcements();
			if (runtime.Owner == BehaviorId.Approach)
				AdvanceApproachSafety();
			else if (StealthRepairResumeContext.IsFightOwner(runtime.Owner) ||
				runtime.Owner == BehaviorId.RecalculateFlee)
				AdvanceUntilRetained();
		}

		void AdvanceApproachSafety()
		{
			// Approach owns its live safety check. Permit only its immediate escape order here:
			// strategic reacquisition remains capped by the normal scheduler.
			if (!AdvanceOwnerOnce() || runtime.Owner != BehaviorId.RecalculateFlee)
				return;

			AdvanceOwnerOnce();
		}

		void AdvanceUntilRetained()
		{
			// Decision-only owners hand control on immediately. Stop when an owner retains control
			// (usually after issuing an order) or when a handoff cycle revisits an owner. The latter
			// defers stale strategic-cache retries to the normal scheduler interval.
			var visitedOwners = new HashSet<BehaviorId>();
			for (var i = 0; i < MaxImmediateHandoffs; i++)
			{
				var previousOwner = runtime.Owner;
				if (!visitedOwners.Add(previousOwner))
					break;
				if (!AdvanceOwnerOnce())
					break;
			}
		}

		bool AdvanceOwnerOnce()
		{
			var previousOwner = runtime.Owner;
			var previousEpoch = runtime.Epoch;
			var accepted = BenchmarkOwnerTick(previousOwner, runtime.Tick);
			var handedOff = accepted && (runtime.Owner != previousOwner || runtime.Epoch != previousEpoch);
			if (handedOff && previousOwner == BehaviorId.SquadConstruction)
				factory.CommitConstructionMembership(previousEpoch);
			StealthSquadLifecycleTelemetry.RecordHandoff(squad, previousOwner, previousEpoch,
				runtime.Owner, runtime.Epoch);
			return handedOff;
		}

		T BenchmarkOwnerTick<T>(BehaviorId owner, Func<T> work)
		{
			if (!Game.IsBenchmarking)
				return work();

			var started = Stopwatch.GetTimestamp();
			var modularBot = squad.Bot as ModularBot;
			var queuedOrders = modularBot?.QueuedOrderCount ?? 0;
			try
			{
				return work();
			}
			finally
			{
				var elapsed = 1000d * Math.Max(0, Stopwatch.GetTimestamp() - started) / Stopwatch.Frequency;
				var addedOrders = modularBot == null ? 0 : modularBot.QueuedOrderCount - queuedOrders;
				Game.RecordBotModuleSample(squad.Bot.Player.ClientIndex,
					$"StealthSquad/{squad.AirProfile}/lifecycle-{owner}", elapsed, Math.Max(0, addedOrders));
			}
		}

		void EnforceLifecycleStance()
		{
			foreach (var actor in squad.Units.Where(actor => actor != null && actor.IsInWorld &&
				!actor.IsDead).OrderBy(actor => actor.ActorID))
				if (actor.TraitOrDefault<AutoTarget>() is AutoTarget autoTarget &&
					autoTarget.Stance != UnitStance.HoldFire)
					squad.Bot.QueueOrder(new Order("SetUnitStance", actor, false)
					{
						ExtraData = (uint)UnitStance.HoldFire
					});
		}

		void PromoteArrivedReinforcements()
		{
			var formation = squad.AirFormationUnits();
			if (formation.Count == 0 || squad.AirReinforcements.Count == 0)
				return;
			var size = Math.Max(1, squad.StealthDefinition?.StrategicCellSize ?? 1);
			var center = squad.World.Map.CellContaining(
				formation.Select(actor => actor.CenterPosition).Average());
			var strategicCenter = new CPos(center.X / size, center.Y / size);
			foreach (var actor in squad.Units.Where(actor => actor != null && actor.IsInWorld &&
				!actor.IsDead && squad.AirReinforcements.Contains(actor.ActorID) &&
				!squad.AirUnitsRepairing.Contains(actor.ActorID)).ToArray())
			{
				var cell = new CPos(actor.Location.X / size, actor.Location.Y / size);
				if (Math.Abs(cell.X - strategicCenter.X) <= 1 &&
					Math.Abs(cell.Y - strategicCenter.Y) <= 1)
					squad.JoinAirFormation(actor);
			}
		}

		public void ObserveDamage(Actor damaged, AttackInfo attack)
		{
			if (damaged == null || attack?.Damage == null ||
				attack.Attacker == null || attack.Damage.Value <= 0)
				return;
			var health = damaged.TraitOrDefault<IHealth>();
			if (health == null || health.HP <= 0 || health.MaxHP <= 0)
				return;
			var threshold = squad.SquadManager.Info.HealthRetreatThreshold;
			if (threshold <= 0 || health.HP >= health.MaxHP * threshold)
				return;
			var owner = runtime.Owner;
			var epoch = runtime.Epoch;
			var accepted = runtime.ObserveDamage(new StealthLifecycleDamageObservation(squad.World.WorldTick,
				attack.Attacker.ActorID, attack.Attacker.Location, attack.Damage.Value,
				new StealthRepairDamagedMember(damaged.ActorID, health.HP, health.MaxHP)));
			StealthSquadLifecycleTelemetry.RecordDamageObservation(squad, owner, epoch, accepted);
		}
	}
}
