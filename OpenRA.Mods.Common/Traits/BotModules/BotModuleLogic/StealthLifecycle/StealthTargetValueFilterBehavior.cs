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
	/// <summary>Step 4A: retain the higher-value half of the strategic target cells.</summary>
	public sealed class StealthTargetValueFilterBehavior
	{
		readonly StealthTargetValueFilterHandoff handoff;

		public StealthTargetValueFilterBehavior(StealthTargetValueFilterHandoff handoff)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetValueFilter)
				throw new ArgumentException("TargetValueFilter requires its ownership.", nameof(handoff));
		}

		public StealthTargetValueFilterResult Execute()
		{
			var scored = handoff.Options.Select(option =>
				new StealthTargetValueOption(option, Score(option))).ToArray();
			var highValue = scored.Where(option =>
				StealthAISpecialistPolicy.MeetsMinimumStrategicCellValue(option.StrategicValue)).ToArray();
			var eligible = highValue.Length == 0 ? scored : highValue;
			var retained = eligible.OrderByDescending(option => option.StrategicValue)
				.ThenBy(option => option.StableIdentity)
				.ThenBy(option => option.StrategicCell.Y).ThenBy(option => option.StrategicCell.X)
				.Take((eligible.Length + 1) / 2).ToArray();
			return new StealthTargetValueFilterResult(handoff.Handoff, retained, true);
		}

		static long Score(StealthTargetOption option)
		{
			long total = 0;
			foreach (var target in option.StrategicTargets)
			{
				var value = StealthAISpecialistPolicy.StrategicTargetValueByRemainingHealth(
					target.ConfiguredPriority, target.ActorValue, target.HitPoints, target.MaximumHitPoints);
				if (value <= 0)
					continue;
				if (long.MaxValue - total < value)
					return long.MaxValue;
				total += value;
			}

			return total;
		}
	}
}
