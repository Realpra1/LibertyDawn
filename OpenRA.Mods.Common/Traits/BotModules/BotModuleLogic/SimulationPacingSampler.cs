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
	public readonly struct SimulationPacingSample
	{
		public readonly bool Sampled;
		public readonly bool Reliable;
		public readonly double RealTimeRatio;
		public readonly string Source;

		public SimulationPacingSample(bool sampled, bool reliable, double realTimeRatio, string source)
		{
			Sampled = sampled;
			Reliable = reliable;
			RealTimeRatio = realTimeRatio;
			Source = source;
		}
	}

	// A bounded, per-owner sampler shared by the existing adaptive unit cap and
	// the squad failsafe. It deliberately does not consume process-global perf history.
	public sealed class SimulationPacingSampler
	{
		readonly int sampleInterval;
		int lastWorldTick = -1;
		int lastLocalTick;
		long lastRunTime;

		public SimulationPacingSampler(int sampleInterval)
		{
			this.sampleInterval = Math.Max(1, sampleInterval);
		}

		public SimulationPacingSample Update(int worldTick, int localTick, int timestep, long runTime,
			bool paused = false, bool loading = false, bool maximumSpeed = false, double elapsedMilliseconds = -1)
		{
			if (lastWorldTick < 0 || worldTick < lastWorldTick || localTick < lastLocalTick || runTime < lastRunTime)
			{
				Reset(worldTick, localTick, runTime);
				return new SimulationPacingSample(false, false, 0, "initializing-or-regressed");
			}

			var worldTicks = worldTick - lastWorldTick;
			if (worldTicks < sampleInterval)
				return new SimulationPacingSample(false, false, 0, "waiting");

			var localTicks = localTick - lastLocalTick;
			var expectedMilliseconds = Math.Max(1L, (long)worldTicks * Math.Max(1, timestep));
			var pausedTicks = Math.Max(0, localTicks - worldTicks);
			var activeMilliseconds = elapsedMilliseconds >= 0 ? elapsedMilliseconds :
				Math.Max(0L, runTime - lastRunTime - (long)pausedTicks * Math.Max(1, timestep));
			var ratio = activeMilliseconds / (double)expectedMilliseconds;
			Reset(worldTick, localTick, runTime);

			if (loading)
				return new SimulationPacingSample(true, false, ratio, "loading");
			if (paused)
				return new SimulationPacingSample(true, false, ratio, "paused");
			if (maximumSpeed)
				return new SimulationPacingSample(true, false, ratio, "maximum-speed");
			if (worldTicks <= 0 || localTicks < worldTicks || activeMilliseconds <= 0)
				return new SimulationPacingSample(true, false, ratio, "invalid-window");

			return new SimulationPacingSample(true, true, ratio, "normal-real-time");
		}

		void Reset(int worldTick, int localTick, long runTime)
		{
			lastWorldTick = worldTick;
			lastLocalTick = localTick;
			lastRunTime = runTime;
		}
	}
}
