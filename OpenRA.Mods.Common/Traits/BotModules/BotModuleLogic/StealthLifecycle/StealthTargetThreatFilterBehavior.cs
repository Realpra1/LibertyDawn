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
	/// <summary>Step 4B: retain the lower-threat half using the standard threat calculator.</summary>
	public sealed class StealthTargetThreatFilterBehavior
	{
		readonly StealthTargetThreatFilterHandoff handoff;
		readonly IStealthTargetThreatAdapter adapter;

		public StealthTargetThreatFilterBehavior(StealthTargetThreatFilterHandoff handoff,
			IStealthTargetThreatAdapter adapter)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetThreatFilter)
				throw new ArgumentException("TargetThreatFilter requires its ownership.", nameof(handoff));
			this.adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
		}

		public StealthTargetThreatFilterResult Execute()
		{
			var scored = handoff.Options.Select(option =>
				new StealthTargetThreatOption(option, adapter.Calculate(option.ThreatFacts))).ToArray();
			var retained = scored.OrderBy(option => option.ThreatRating)
				.ThenBy(option => option.Crossover).ThenBy(option => option.StableIdentity)
				.ThenBy(option => option.StrategicCell.Y).ThenBy(option => option.StrategicCell.X)
				.Take((scored.Length + 1) / 2).ToArray();
			return new StealthTargetThreatFilterResult(handoff.Handoff, retained, true);
		}
	}
}
