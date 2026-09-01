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
using System.Collections.ObjectModel;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Immutable bounded cost policy supplied to TargetDistanceChoice.</summary>
	public sealed class StealthTargetDistanceChoicePolicy
	{
		const int MillisecondsPerSecond = 1000;
		const int MaximumCreditFractionDenominator = 10;

		public const int AbsoluteMaximumSeparationCreditMilliseconds =
			StealthTargetAcquisitionBehavior.MaximumTravelSeconds * MillisecondsPerSecond /
			MaximumCreditFractionDenominator;

		public int SeparationCreditPerSquaredCellMilliseconds { get; }
		public int MaximumSeparationCreditMilliseconds { get; }

		public StealthTargetDistanceChoicePolicy(int separationCreditPerSquaredCellMilliseconds,
			int maximumSeparationCreditMilliseconds)
		{
			if (separationCreditPerSquaredCellMilliseconds <= 0)
				throw new ArgumentOutOfRangeException(nameof(separationCreditPerSquaredCellMilliseconds));
			if (maximumSeparationCreditMilliseconds <= 0 ||
				maximumSeparationCreditMilliseconds > AbsoluteMaximumSeparationCreditMilliseconds)
				throw new ArgumentOutOfRangeException(nameof(maximumSeparationCreditMilliseconds));

			SeparationCreditPerSquaredCellMilliseconds = separationCreditPerSquaredCellMilliseconds;
			MaximumSeparationCreditMilliseconds = maximumSeparationCreditMilliseconds;
		}
	}

	/// <summary>Immutable strategic-cache position for another active stealth squad.</summary>
	public sealed class StealthActiveSquadTargetSnapshot
	{
		public uint StableActorId { get; }
		public CPos StrategicCell { get; }

		public StealthActiveSquadTargetSnapshot(uint stableActorId, CPos strategicCell)
		{
			if (stableActorId == 0)
				throw new ArgumentOutOfRangeException(nameof(stableActorId));

			StableActorId = stableActorId;
			StrategicCell = strategicCell;
		}
	}

	/// <summary>One immutable strategic-cell mission selected by TargetDistanceChoice.</summary>
	public sealed class StealthApproachMission
	{
		public StealthTargetThreatOption TargetOption { get; }
		public CPos StrategicCell => TargetOption.StrategicCell;
		public uint StableTargetActorId => TargetOption.StableIdentity;
		public int? EstimatedTravelMilliseconds => TargetOption.ValueOption.EstimatedTravelMilliseconds;
		public long MinimumSquadSeparationSquared { get; }
		public int SeparationCreditMilliseconds { get; }
		public long AdjustedTravelCostMilliseconds { get; }

		internal StealthApproachMission(StealthTargetThreatOption targetOption,
			long minimumSquadSeparationSquared, int separationCreditMilliseconds,
			long adjustedTravelCostMilliseconds)
		{
			TargetOption = targetOption ?? throw new ArgumentNullException(nameof(targetOption));
			if (minimumSquadSeparationSquared < 0 || separationCreditMilliseconds < 0 ||
				adjustedTravelCostMilliseconds < 0)
				throw new ArgumentOutOfRangeException(nameof(minimumSquadSeparationSquared));

			MinimumSquadSeparationSquared = minimumSquadSeparationSquared;
			SeparationCreditMilliseconds = separationCreditMilliseconds;
			AdjustedTravelCostMilliseconds = adjustedTravelCostMilliseconds;
		}
	}

	public sealed class StealthTargetDistanceChoiceResult
	{
		internal StealthBehaviorHandoff Handoff { get; }
		public StealthApproachMission Mission { get; }
		public bool IsReadyForApproach => true;

		internal StealthTargetDistanceChoiceResult(StealthBehaviorHandoff handoff,
			StealthApproachMission mission)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetDistanceChoice)
				throw new ArgumentException(
					"The result must belong to TargetDistanceChoice.", nameof(handoff));
			Mission = mission ?? throw new ArgumentNullException(nameof(mission));
		}
	}

	/// <summary>Typed immutable boundary between lifecycle Steps 4C and Approach.</summary>
	public sealed class StealthApproachHandoff
	{
		readonly ReadOnlyCollection<StealthApproachMission> missions;

		internal StealthBehaviorHandoff Handoff { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public IReadOnlyList<StealthApproachMission> Missions => missions;

		internal StealthApproachHandoff(StealthBehaviorHandoff handoff,
			StealthApproachMission mission)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.Approach)
				throw new ArgumentException("The handoff must belong to Approach.", nameof(handoff));

			missions = Array.AsReadOnly(new[]
			{
				mission ?? throw new ArgumentNullException(nameof(mission))
			});
		}
	}
}
