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
	public interface IStealthLifecycleCacheService
	{
		void Observe(StealthLifecycleObservationFrame frame);
	}

	public interface IStealthLifecycleThreatService
	{
		void Observe(StealthLifecycleObservationFrame frame);
	}

	public interface IStealthLifecycleRouteService
	{
		void Observe(StealthLifecycleObservationFrame frame);
	}

	public interface IStealthLifecycleDiagnosticService
	{
		void Record(StealthLifecycleDiagnostic diagnostic);
	}

	public readonly struct StealthLifecycleDiagnostic
	{
		public int Tick { get; }
		public BehaviorId Owner { get; }
		public OwnershipEpoch Epoch { get; }

		public StealthLifecycleDiagnostic(int tick, BehaviorId owner, OwnershipEpoch epoch)
		{
			Tick = tick;
			Owner = owner;
			Epoch = epoch;
		}
	}

	public readonly struct StealthLifecycleState
	{
		public BehaviorId Owner { get; }
		public OwnershipEpoch Epoch { get; }
		public int LastObservedTick { get; }

		public StealthLifecycleState(BehaviorId owner, OwnershipEpoch epoch, int lastObservedTick)
		{
			Owner = owner;
			Epoch = epoch;
			LastObservedTick = lastObservedTick;
		}
	}

	/// <summary>
	/// Disabled integration shell. It has no rollout switch and is not registered with a bot module.
	/// Services receive immutable observations and cannot return orders, results, or transition requests.
	/// </summary>
	public sealed class StealthLifecycleContext
	{
		readonly StealthLifecycleController controller;
		readonly IStealthLifecycleCacheService cache;
		readonly IStealthLifecycleThreatService threats;
		readonly IStealthLifecycleRouteService routes;
		readonly IStealthLifecycleDiagnosticService diagnostics;
		readonly bool enabled;

		public bool Enabled => enabled;
		public StealthLifecycleState State =>
			new StealthLifecycleState(controller.Owner, controller.Epoch, controller.LastObservedTick);

		public StealthLifecycleContext(
			StealthLifecycleController controller,
			IStealthLifecycleCacheService cache,
			IStealthLifecycleThreatService threats,
			IStealthLifecycleRouteService routes,
			IStealthLifecycleDiagnosticService diagnostics,
			bool enabled = false)
		{
			this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
			this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
			this.threats = threats ?? throw new ArgumentNullException(nameof(threats));
			this.routes = routes ?? throw new ArgumentNullException(nameof(routes));
			this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
			this.enabled = enabled;
		}

		public void Observe(StealthLifecycleObservationFrame frame)
		{
			controller.Observe(frame);
			cache.Observe(frame);
			threats.Observe(frame);
			routes.Observe(frame);
			diagnostics.Record(new StealthLifecycleDiagnostic(frame.Tick, controller.Owner, controller.Epoch));
		}
	}
}
