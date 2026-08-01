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
	public enum AlliedSupplyProductionAction { None, Cancel, GiveUp, Request }

	public readonly struct AlliedSupplyProductionObservation
	{
		public readonly bool NeedsSupply;
		public readonly bool HasAnyRecoveryPath;
		public readonly bool OwnPlayerNeedsSupply;
		public readonly int AvailableProductionQueues;

		public AlliedSupplyProductionObservation(bool needsSupply, bool hasAnyRecoveryPath,
			bool ownPlayerNeedsSupply, int availableProductionQueues)
		{
			NeedsSupply = needsSupply;
			HasAnyRecoveryPath = hasAnyRecoveryPath;
			OwnPlayerNeedsSupply = ownPlayerNeedsSupply;
			AvailableProductionQueues = Math.Max(0, availableProductionQueues);
		}
	}

	public readonly struct AlliedSupplyProductionDecision
	{
		public readonly AlliedSupplyProductionAction Action;
		public readonly int RequestCount;

		public AlliedSupplyProductionDecision(AlliedSupplyProductionAction action, int requestCount = 0)
		{
			Action = action;
			RequestCount = Math.Max(0, requestCount);
		}
	}

	/// <summary>Serializable per-ally quota state.</summary>
	public sealed class AlliedSupplyProductionState
	{
		public int WindowStartTick;
		public int RequestsInWindow;
		public bool GaveUp;
	}

	/// <summary>Pure deterministic policy for allied emergency supply production.</summary>
	public static class AlliedSupplyProductionPolicy
	{
		public static int RemainingGlobalQuota(int availableProductionQueues, int requestsInWindow)
		{
			return Math.Max(0, Math.Max(0, availableProductionQueues) - Math.Max(0, requestsInWindow));
		}

		public static AlliedSupplyProductionDecision Evaluate(AlliedSupplyProductionState state,
			in AlliedSupplyProductionObservation observation, int tick, int quotaIntervalTicks)
		{
			if (state == null)
				throw new ArgumentNullException(nameof(state));
			if (tick < 0)
				throw new ArgumentOutOfRangeException(nameof(tick));
			if (quotaIntervalTicks <= 0)
				throw new ArgumentOutOfRangeException(nameof(quotaIntervalTicks));

			// Recovery ends this stranded episode. A future independent episode may be helped again.
			if (!observation.NeedsSupply)
			{
				state.WindowStartTick = tick;
				state.RequestsInWindow = 0;
				state.GaveUp = false;
				return new AlliedSupplyProductionDecision(AlliedSupplyProductionAction.Cancel);
			}

			if (state.GaveUp)
				return new AlliedSupplyProductionDecision(AlliedSupplyProductionAction.GiveUp);

			if (!observation.HasAnyRecoveryPath)
			{
				state.GaveUp = true;
				return new AlliedSupplyProductionDecision(AlliedSupplyProductionAction.GiveUp);
			}

			// Existing or newly completed trucks must remain available for our own emergency first.
			if (observation.OwnPlayerNeedsSupply || observation.AvailableProductionQueues == 0)
				return new AlliedSupplyProductionDecision(AlliedSupplyProductionAction.Cancel);

			if (state.WindowStartTick < 0 || tick < state.WindowStartTick ||
				tick - state.WindowStartTick >= quotaIntervalTicks)
			{
				state.WindowStartTick = tick;
				state.RequestsInWindow = 0;
			}

			var requestCount = Math.Max(0, observation.AvailableProductionQueues - state.RequestsInWindow);
			if (requestCount == 0)
				return new AlliedSupplyProductionDecision(AlliedSupplyProductionAction.None);

			state.RequestsInWindow += requestCount;
			return new AlliedSupplyProductionDecision(AlliedSupplyProductionAction.Request, requestCount);
		}
	}
}
