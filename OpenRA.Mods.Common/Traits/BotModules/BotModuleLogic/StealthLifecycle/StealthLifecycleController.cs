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
	/// Disconnected lifecycle scaffold. Ownership changes only when the current handoff is returned
	/// in a behavior result; observing time or world events can never select another owner.
	/// </summary>
	public sealed class StealthLifecycleController
	{
		BehaviorId owner;
		OwnershipEpoch epoch;
		int lastObservedTick;

		public BehaviorId Owner => owner;
		public OwnershipEpoch Epoch => epoch;
		public int LastObservedTick => lastObservedTick;
		public StealthBehaviorHandoff CurrentHandoff => new StealthBehaviorHandoff(owner, epoch);

		public StealthLifecycleController()
			: this(BehaviorId.Start, new OwnershipEpoch(1), -1) { }

		StealthLifecycleController(BehaviorId owner, OwnershipEpoch epoch, int lastObservedTick)
		{
			if (!Enum.IsDefined(typeof(BehaviorId), owner))
				throw new ArgumentOutOfRangeException(nameof(owner));
			if (epoch.Value <= 0)
				throw new ArgumentOutOfRangeException(nameof(epoch));
			if (lastObservedTick < -1)
				throw new ArgumentOutOfRangeException(nameof(lastObservedTick));

			this.owner = owner;
			this.epoch = epoch;
			this.lastObservedTick = lastObservedTick;
		}

		public void Observe(StealthLifecycleObservationFrame frame)
		{
			if (frame == null)
				throw new ArgumentNullException(nameof(frame));
			if (frame.Tick < lastObservedTick)
				throw new ArgumentOutOfRangeException(nameof(frame),
					"Lifecycle observations must be supplied in tick order.");

			lastObservedTick = frame.Tick;
		}

		public bool TryAccept(StealthStartResult result, out StealthBehaviorHandoff nextHandoff)
		{
			nextHandoff = null;
			if (result == null || !result.HasTransition || owner != BehaviorId.Start ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			nextHandoff = AdvanceTo(BehaviorId.SquadConstruction);
			return true;
		}

		public bool TryAccept(StealthSquadConstructionResult result,
			out StealthBehaviorHandoff nextHandoff)
		{
			nextHandoff = null;
			if (result == null || !result.IsComplete || owner != BehaviorId.SquadConstruction ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			nextHandoff = AdvanceTo(BehaviorId.TargetAcquisition);
			return true;
		}

		public bool TryAccept(StealthTargetAcquisitionResult result,
			out StealthTargetValueFilterHandoff nextHandoff)
		{
			nextHandoff = null;
			if (result == null || !result.IsReadyForValueFilter || owner != BehaviorId.TargetAcquisition ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			nextHandoff = new StealthTargetValueFilterHandoff(
				AdvanceTo(BehaviorId.TargetValueFilter), result.Options);
			return true;
		}

		public bool TryAccept(StealthTargetValueFilterResult result,
			out StealthTargetThreatFilterHandoff nextHandoff)
		{
			nextHandoff = null;
			if (result == null || !result.IsReadyForThreatFilter || owner != BehaviorId.TargetValueFilter ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch)
				return false;

			nextHandoff = new StealthTargetThreatFilterHandoff(
				AdvanceTo(BehaviorId.TargetThreatFilter), result.Options);
			return true;
		}

		StealthBehaviorHandoff AdvanceTo(BehaviorId nextOwner)
		{
			if (epoch.Value == long.MaxValue)
				throw new InvalidOperationException("The stealth lifecycle ownership epoch is exhausted.");

			owner = nextOwner;
			epoch = new OwnershipEpoch(epoch.Value + 1);
			return CurrentHandoff;
		}

		public StealthLifecycleSavePayload ExportState()
		{
			return new StealthLifecycleSavePayload(owner, epoch, lastObservedTick);
		}

		public static StealthLifecycleController Restore(StealthLifecycleSavePayload payload)
		{
			if (payload == null)
				throw new ArgumentNullException(nameof(payload));

			return new StealthLifecycleController(payload.Owner, payload.Epoch, payload.LastObservedTick);
		}
	}
}
