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
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Owns the concrete services and sole lifecycle runtime registered for one Squad.</summary>
	sealed class StealthSquadLifecycleRuntimeHost
	{
		readonly Squad squad;
		readonly StealthSquadLifecycleStrategicAdapter strategic;
		readonly StealthLifecycleRuntime runtime;

		public StealthSquadLifecycleRuntimeHost(Squad squad)
		{
			this.squad = squad ?? throw new ArgumentNullException(nameof(squad));
			strategic = new StealthSquadLifecycleStrategicAdapter(squad);
			var passive = new StealthLifecyclePassiveServices();
			runtime = new StealthLifecycleRuntime(
				new StealthSquadLifecycleOwnerFactory(squad, strategic),
				new StealthSquadLifecycleOrderTarget(squad), strategic, passive, passive, passive);
		}

		StealthSquadLifecycleRuntimeHost(Squad squad, MiniYamlNode saved)
		{
			this.squad = squad ?? throw new ArgumentNullException(nameof(squad));
			strategic = new StealthSquadLifecycleStrategicAdapter(squad);
			var passive = new StealthLifecyclePassiveServices();
			runtime = StealthLifecycleRuntime.Restore(saved,
				new StealthSquadLifecycleOwnerFactory(squad, strategic),
				new StealthSquadLifecycleOrderTarget(squad), strategic, passive, passive, passive);
		}

		public static StealthSquadLifecycleRuntimeHost Restore(Squad squad, MiniYamlNode saved)
		{
			return new StealthSquadLifecycleRuntimeHost(squad, saved);
		}

		public void Tick()
		{
			var observations = squad.Units.Where(actor => actor != null && actor.IsInWorld && !actor.IsDead)
				.Select(actor => new StealthLifecycleObservation(
					StealthLifecycleObservationKind.Timer, actor.ActorID)).ToArray();
			runtime.Observe(new StealthLifecycleObservationFrame(squad.World.WorldTick, observations));
			runtime.Tick();
		}

		public MiniYamlNode Serialize()
		{
			return runtime.Serialize();
		}

		public void ObserveDamage(Actor damaged, AttackInfo attack)
		{
			if (damaged == null || attack?.Attacker == null || attack.Damage.Value <= 0)
				return;
			var health = damaged.TraitOrDefault<IHealth>();
			if (health == null || health.HP <= 0 || health.MaxHP <= 0)
				return;
			runtime.ObserveDamage(new StealthLifecycleDamageObservation(squad.World.WorldTick,
				attack.Attacker.ActorID, attack.Attacker.Location, attack.Damage.Value,
				new StealthRepairDamagedMember(damaged.ActorID, health.HP, health.MaxHP)));
		}
	}
}
