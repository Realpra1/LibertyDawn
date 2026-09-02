#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Transient rollback snapshot. This is not serialized AI behavior state.</summary>
	sealed class StealthLifecycleControllerState
	{
		public BehaviorId Owner { get; }
		public OwnershipEpoch Epoch { get; }
		public int LastObservedTick { get; }

		public StealthLifecycleControllerState(
			BehaviorId owner, OwnershipEpoch epoch, int lastObservedTick)
		{
			Owner = owner;
			Epoch = epoch;
			LastObservedTick = lastObservedTick;
		}
	}
}
