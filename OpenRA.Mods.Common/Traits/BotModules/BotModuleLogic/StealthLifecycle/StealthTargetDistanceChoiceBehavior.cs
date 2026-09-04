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

using System;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Step 4C: choose the survivor with the lowest cached route cost.</summary>
	public sealed class StealthTargetDistanceChoiceBehavior
	{
		readonly StealthTargetDistanceChoiceHandoff handoff;

		public StealthTargetDistanceChoiceBehavior(StealthTargetDistanceChoiceHandoff handoff)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetDistanceChoice || handoff.Options.Count == 0)
				throw new ArgumentException("TargetDistanceChoice requires candidate ownership.", nameof(handoff));
		}

		public StealthTargetDistanceChoiceResult Execute()
		{
			var selected = handoff.Options
				.OrderBy(option => option.ValueOption.EstimatedTravelMilliseconds ?? int.MaxValue)
				.ThenBy(option => option.StableIdentity)
				.ThenBy(option => option.StrategicCell.Y)
				.ThenBy(option => option.StrategicCell.X).First();
			return new StealthTargetDistanceChoiceResult(handoff.Handoff,
				new StealthApproachMission(selected));
		}
	}
}
