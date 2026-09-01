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

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	sealed class StealthSquadLifecycleRuntimeOwner : IStealthLifecycleRuntimeOwner,
		IStealthLifecycleRuntimeDamageOwner
	{
		readonly Func<object> execute;
		readonly Func<object, StealthLifecycleDamageObservation, long,
			StealthLifecycleDamageYield> captureDamage;
		object lastResult;
		StealthLifecycleDamageYield pendingDamage;

		public BehaviorId Owner { get; }
		public OwnershipEpoch Epoch { get; }

		public StealthSquadLifecycleRuntimeOwner(BehaviorId owner, OwnershipEpoch epoch,
			Func<object> execute,
			Func<object, StealthLifecycleDamageObservation, long,
				StealthLifecycleDamageYield> captureDamage = null)
		{
			Owner = owner;
			Epoch = epoch;
			this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
			this.captureDamage = captureDamage;
		}

		public object Execute()
		{
			if (pendingDamage != null)
				return pendingDamage;
			return lastResult = execute();
		}

		public bool TryCaptureDamage(StealthLifecycleDamageObservation observation, long eventId,
			out StealthLifecycleDamageYield yielded)
		{
			if (pendingDamage != null)
			{
				yielded = null;
				return false;
			}

			yielded = captureDamage?.Invoke(lastResult, observation, eventId);
			if (yielded != null)
				pendingDamage = yielded;
			return yielded != null;
		}
	}
}
