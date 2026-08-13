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

namespace OpenRA.Mods.Common.Traits
{
	public readonly struct AdaptiveUnitCapSample
	{
		public readonly bool Sampled;
		public readonly double RealTimeRatio;
		public readonly int EffectiveLimit;
		public readonly string Decision;

		public AdaptiveUnitCapSample(bool sampled, double realTimeRatio, int effectiveLimit, string decision)
		{
			Sampled = sampled;
			RealTimeRatio = realTimeRatio;
			EffectiveLimit = effectiveLimit;
			Decision = decision;
		}
	}

	public sealed class AdaptiveUnitCapController
	{
		public const int GlobalMinimumLimit = 300;

		readonly int sampleInterval;
		readonly double lagTolerance;
		readonly int minimumLimit;
		readonly int reductionStep;
		readonly int recoverySamples;
		readonly SimulationPacingSampler pacingSampler;
		int healthySamples;

		public int EffectiveLimit { get; private set; }

		public AdaptiveUnitCapController(int sampleInterval, float lagTolerance, int minimumLimit,
			int reductionStep, int recoverySamples)
		{
			this.sampleInterval = Math.Max(1, sampleInterval);
			this.lagTolerance = Math.Max(0, lagTolerance);
			this.minimumLimit = Math.Max(GlobalMinimumLimit, minimumLimit);
			this.reductionStep = Math.Max(1, reductionStep);
			this.recoverySamples = Math.Max(1, recoverySamples);
			pacingSampler = new SimulationPacingSampler(this.sampleInterval);
		}

		public AdaptiveUnitCapSample Update(int worldTick, int localTick, int timestep, long runTime,
			int committedUnits, int enforcementCeiling)
		{
			var pacing = pacingSampler.Update(worldTick, localTick, timestep, runTime);
			if (!pacing.Sampled)
				return new AdaptiveUnitCapSample(false, 0, EffectiveLimit, "waiting");

			var ratio = pacing.RealTimeRatio;

			if (enforcementCeiling <= 0)
				return new AdaptiveUnitCapSample(true, ratio, 0, "disabled-no-ceiling");

			// A stale or mistaken ceiling must never undermine the global per-AI safety floor.
			var floor = minimumLimit;
			var ceiling = Math.Max(floor, enforcementCeiling);
			if (ratio > 1d + lagTolerance)
			{
				healthySamples = 0;
				if (EffectiveLimit <= 0)
				{
					EffectiveLimit = Math.Min(ceiling, Math.Max(floor, committedUnits));
					return new AdaptiveUnitCapSample(true, ratio, EffectiveLimit, "enforced");
				}

				var reducedLimit = Math.Max(floor, EffectiveLimit - reductionStep);
				var decision = reducedLimit < EffectiveLimit ? "reduced" : "held";
				EffectiveLimit = reducedLimit;
				return new AdaptiveUnitCapSample(true, ratio, EffectiveLimit, decision);
			}

			if (EffectiveLimit <= 0)
				return new AdaptiveUnitCapSample(true, ratio, 0, "unlimited");

			if (++healthySamples >= recoverySamples)
			{
				healthySamples = 0;
				EffectiveLimit = 0;
				return new AdaptiveUnitCapSample(true, ratio, 0, "released");
			}

			return new AdaptiveUnitCapSample(true, ratio, EffectiveLimit, "recovering");
		}
	}
}
