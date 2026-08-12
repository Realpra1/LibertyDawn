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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenRA.Support
{
	public readonly struct StallClassification
	{
		public readonly int SampleCount;
		public readonly int FreezeCount;
		public readonly double Median;
		public readonly double P95;
		public readonly double P99;
		public readonly double Maximum;
		public readonly bool HasSeparatedTail;
		public readonly bool IsPeriodic;
		public readonly double Cadence;

		public StallClassification(int sampleCount, int freezeCount, double median, double p95,
			double p99, double maximum, bool hasSeparatedTail, bool isPeriodic, double cadence)
		{
			SampleCount = sampleCount;
			FreezeCount = freezeCount;
			Median = median;
			P95 = p95;
			P99 = p99;
			Maximum = maximum;
			HasSeparatedTail = hasSeparatedTail;
			IsPeriodic = isPeriodic;
			Cadence = cadence;
		}
	}

	public static class PeriodicStallClassifier
	{
		public static StallClassification Classify(IReadOnlyList<double> samples,
			IReadOnlyList<int> sampleTicks, double threshold)
		{
			if (samples == null || sampleTicks == null || samples.Count == 0 || samples.Count != sampleTicks.Count)
				return new StallClassification(0, 0, 0, 0, 0, 0, false, false, 0);

			var sorted = samples.OrderBy(x => x).ToArray();
			var freezeTicks = new List<int>();
			for (var i = 0; i < samples.Count; i++)
				if (samples[i] >= threshold && (freezeTicks.Count == 0 || sampleTicks[i] > freezeTicks[freezeTicks.Count - 1]))
					freezeTicks.Add(sampleTicks[i]);

			var median = Quantile(sorted, .5);
			var p95 = Quantile(sorted, .95);
			var p99 = Quantile(sorted, .99);
			var separated = freezeTicks.Count > 0 && freezeTicks.Count * 4 <= samples.Count && p95 < threshold;
			var periodic = false;
			double cadence = 0;
			if (separated && freezeTicks.Count >= 3)
			{
				var intervals = new int[freezeTicks.Count - 1];
				for (var i = 1; i < freezeTicks.Count; i++)
					intervals[i - 1] = freezeTicks[i] - freezeTicks[i - 1];

				Array.Sort(intervals);
				cadence = Quantile(intervals.Select(x => (double)x).ToArray(), .5);
				var tolerance = Math.Max(1, cadence * .2);
				periodic = cadence > 0 && intervals.Count(x => Math.Abs(x - cadence) <= tolerance) * 4 >= intervals.Length * 3;
			}

			return new StallClassification(samples.Count, freezeTicks.Count, median,
				Quantile(sorted, .95), Quantile(sorted, .99), sorted[sorted.Length - 1], separated, periodic, cadence);
		}

		static double Quantile(IReadOnlyList<double> sorted, double quantile)
		{
			if (sorted.Count == 0)
				return 0;

			var rank = Math.Max(1, (int)Math.Ceiling(quantile * sorted.Count));
			return sorted[Math.Min(sorted.Count, rank) - 1];
		}
	}

	sealed class FixedLatencyHistogram
	{
		// One millisecond buckets through ten seconds. The last bucket also counts larger values.
		const int MaximumBucket = 10000;
		readonly int[] buckets = new int[MaximumBucket + 1];
		readonly int[] tailTicks = new int[256];
		readonly double threshold;
		int tailHead;
		int tailCount;

		public long Count { get; private set; }
		public double Sum { get; private set; }
		public double Maximum { get; private set; }
		public int FreezeCount { get; private set; }

		public FixedLatencyHistogram(double threshold)
		{
			this.threshold = threshold;
		}

		public void Record(int tick, double milliseconds)
		{
			if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0)
				return;

			Count++;
			Sum += milliseconds;
			Maximum = Math.Max(Maximum, milliseconds);
			buckets[Math.Min(MaximumBucket, (int)Math.Ceiling(milliseconds))]++;
			if (milliseconds < threshold)
				return;

			FreezeCount++;
			tailTicks[tailHead] = tick;
			tailHead = (tailHead + 1) % tailTicks.Length;
			tailCount = Math.Min(tailCount + 1, tailTicks.Length);
		}

		public double Quantile(double quantile)
		{
			if (Count == 0)
				return 0;

			var target = Math.Max(1L, (long)Math.Ceiling(quantile * Count));
			long cumulative = 0;
			for (var i = 0; i < buckets.Length; i++)
			{
				cumulative += buckets[i];
				if (cumulative >= target)
					return i;
			}

			return MaximumBucket;
		}

		public double Cadence()
		{
			if (tailCount < 3)
				return 0;

			var ticks = new int[tailCount];
			var start = (tailHead - tailCount + tailTicks.Length) % tailTicks.Length;
			for (var i = 0; i < tailCount; i++)
				ticks[i] = tailTicks[(start + i) % tailTicks.Length];

			var intervals = new List<int>();
			for (var i = 1; i < ticks.Length; i++)
				if (ticks[i] > ticks[i - 1])
					intervals.Add(ticks[i] - ticks[i - 1]);

			if (intervals.Count < 2)
				return 0;

			intervals.Sort();
			return intervals[intervals.Count / 2];
		}
	}

	sealed class PeriodicStallReport
	{
		const int RuntimeSampleInterval = 25;
		const int MaximumRuntimeSamples = 512;
		const int MaximumTailEvents = 128;
		const double FreezeThresholdMs = 50;

		sealed class ModuleAggregate
		{
			public long Calls;
			public long Orders;
			public double TotalMilliseconds;
			public double MaximumMilliseconds;
		}

		readonly struct TailEvent
		{
			public readonly int Tick;
			public readonly string Source;
			public readonly double Milliseconds;

			public TailEvent(int tick, string source, double milliseconds)
			{
				Tick = tick;
				Source = source;
				Milliseconds = milliseconds;
			}
		}

		readonly struct RuntimeSample
		{
			public readonly int Tick;
			public readonly double CpuMilliseconds;
			public readonly long WorkingSet;
			public readonly long ManagedBytes;
			public readonly long AllocatedBytes;
			public readonly int Gc0;
			public readonly int Gc1;
			public readonly int Gc2;
			public readonly long ReadBytes;
			public readonly long WriteBytes;
			public readonly long LogBytes;
			public readonly int Actors;
			public readonly int Effects;
			public readonly string HostLoad;
			public readonly string CpuFrequency;
			public readonly string Thermal;

			public RuntimeSample(int tick, double cpuMilliseconds, long workingSet, long managedBytes,
				long allocatedBytes, int gc0, int gc1, int gc2, long readBytes, long writeBytes,
				long logBytes, int actors, int effects, string hostLoad, string cpuFrequency, string thermal)
			{
				Tick = tick;
				CpuMilliseconds = cpuMilliseconds;
				WorkingSet = workingSet;
				ManagedBytes = managedBytes;
				AllocatedBytes = allocatedBytes;
				Gc0 = gc0;
				Gc1 = gc1;
				Gc2 = gc2;
				ReadBytes = readBytes;
				WriteBytes = writeBytes;
				LogBytes = logBytes;
				Actors = actors;
				Effects = effects;
				HostLoad = hostLoad;
				CpuFrequency = cpuFrequency;
				Thermal = thermal;
			}
		}

		readonly FixedLatencyHistogram ticks = new FixedLatencyHistogram(FreezeThresholdMs);
		readonly FixedLatencyHistogram renders = new FixedLatencyHistogram(FreezeThresholdMs);
		readonly FixedLatencyHistogram presents = new FixedLatencyHistogram(FreezeThresholdMs);
		readonly Dictionary<string, ModuleAggregate> modules = new Dictionary<string, ModuleAggregate>();
		readonly Dictionary<string, ModuleAggregate> logicPhases = new Dictionary<string, ModuleAggregate>();
		readonly List<RuntimeSample> runtimeSamples = new List<RuntimeSample>(MaximumRuntimeSamples);
		readonly List<TailEvent> tailEvents = new List<TailEvent>(MaximumTailEvents);
		readonly MethodInfo totalAllocatedBytes = typeof(GC).GetMethod("GetTotalAllocatedBytes", new[] { typeof(bool) });
		int lastRecordedTick = -1;

		public void RecordTick(int tick, World world)
		{
			if (tick <= lastRecordedTick)
				return;

			lastRecordedTick = tick;
			var value = PerfHistory.Items["tick_time"].LastValue;
			ticks.Record(tick, value);
			RecordTail(tick, "tick", value);
			if ((tick == 1 || tick % RuntimeSampleInterval == 0) && runtimeSamples.Count < MaximumRuntimeSamples)
				runtimeSamples.Add(ReadRuntimeSample(tick, world));
		}

		public void RecordRender(int frame)
		{
			var render = PerfHistory.Items["render"].LastValue;
			var present = PerfHistory.Items["render_flip"].LastValue;
			renders.Record(frame, render);
			presents.Record(frame, present);
			RecordTail(frame, "render", render);
			RecordTail(frame, "present", present);
		}

		public void RecordModule(int tick, int playerIndex, string module, double milliseconds, int queuedOrders)
		{
			var key = string.Format(CultureInfo.InvariantCulture, "player-{0}/{1}", playerIndex, module);
			var aggregate = modules.GetOrAdd(key);
			aggregate.Calls++;
			aggregate.Orders += Math.Max(0, queuedOrders);
			aggregate.TotalMilliseconds += milliseconds;
			aggregate.MaximumMilliseconds = Math.Max(aggregate.MaximumMilliseconds, milliseconds);
			RecordTail(tick, key, milliseconds);
		}

		public void RecordLogicPhase(int tick, string phase, double milliseconds)
		{
			var aggregate = logicPhases.GetOrAdd(phase);
			aggregate.Calls++;
			aggregate.TotalMilliseconds += milliseconds;
			aggregate.MaximumMilliseconds = Math.Max(aggregate.MaximumMilliseconds, milliseconds);
			RecordTail(tick, "logic/" + phase, milliseconds);
		}

		void RecordTail(int tick, string source, double milliseconds)
		{
			if (milliseconds < FreezeThresholdMs)
				return;

			var item = new TailEvent(tick, source, milliseconds);
			if (tailEvents.Count < MaximumTailEvents)
			{
				tailEvents.Add(item);
				return;
			}

			var minimum = 0;
			for (var i = 1; i < tailEvents.Count; i++)
				if (tailEvents[i].Milliseconds < tailEvents[minimum].Milliseconds)
					minimum = i;

			if (milliseconds > tailEvents[minimum].Milliseconds)
				tailEvents[minimum] = item;
		}

		RuntimeSample ReadRuntimeSample(int tick, World world)
		{
			using (var process = Process.GetCurrentProcess())
			{
				var io = ReadProcIo();
				return new RuntimeSample(tick, process.TotalProcessorTime.TotalMilliseconds, process.WorkingSet64,
					GC.GetTotalMemory(false), ReadAllocatedBytes(), GC.CollectionCount(0), GC.CollectionCount(1),
					GC.CollectionCount(2), io.Item1, io.Item2, LogBytes(), world.Actors.Count(), world.Effects.Count(),
					ReadText("/proc/loadavg"), ReadText("/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq"),
					ReadFirstThermal());
			}
		}

		long ReadAllocatedBytes()
		{
			if (totalAllocatedBytes == null)
				return -1;

			try { return (long)totalAllocatedBytes.Invoke(null, new object[] { false }); }
			catch { return -1; }
		}

		static Tuple<long, long> ReadProcIo()
		{
			long read = -1;
			long write = -1;
			try
			{
				foreach (var line in File.ReadLines("/proc/self/io"))
				{
					var parts = line.Split(new[] { ':' }, 2);
					if (parts.Length != 2 || !long.TryParse(parts[1], NumberStyles.Integer,
						CultureInfo.InvariantCulture, out var value))
						continue;

					if (parts[0] == "read_bytes") read = value;
					else if (parts[0] == "write_bytes") write = value;
				}
			}
			catch { }

			return Tuple.Create(read, write);
		}

		static long LogBytes()
		{
			try
			{
				var directory = Path.Combine(Platform.SupportDir, "Logs");
				return Directory.Exists(directory) ? Directory.EnumerateFiles(directory).Sum(path => new FileInfo(path).Length) : 0;
			}
			catch { return -1; }
		}

		static string ReadText(string path)
		{
			try { return File.Exists(path) ? File.ReadAllText(path).Trim().Replace('\t', ' ') : "unavailable"; }
			catch { return "unavailable"; }
		}

		static string ReadFirstThermal()
		{
			return ReadText("/sys/class/thermal/thermal_zone0/temp");
		}

		public void Write(string prefix)
		{
			var channel = "periodic-stall-report";
			Log.AddChannel(channel, $"{prefix}periodic-stall.tsv");
			Log.Write(channel, "format\tperiodic-stall-v1");
			Log.Write(channel, "bounds\thistogram_bucket_ms=1\thistogram_max_ms=10000\ttail_events=128\truntime_samples=512\truntime_interval_ticks=25");
			WriteDistribution(channel, "tick", ticks);
			WriteDistribution(channel, "render", renders);
			WriteDistribution(channel, "present", presents);
			Log.Write(channel, "host-metrics\tgc_pause_time=unavailable\tscheduler_throttle=unavailable-unless-exposed-by-host");
			Log.Write(channel, "runtime\ttick\tcpu_ms\tworking_set\tmanaged_bytes\tallocated_bytes\tgc0\tgc1\tgc2\tread_bytes\twrite_bytes\tlog_bytes\tactors\teffects\thost_load\tcpu_frequency_khz\tthermal_millidegrees");
			foreach (var sample in runtimeSamples)
				Log.Write(channel, string.Format(CultureInfo.InvariantCulture,
					"runtime\t{0}\t{1:F3}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12}\t{13}\t{14}\t{15}",
					sample.Tick, sample.CpuMilliseconds, sample.WorkingSet, sample.ManagedBytes,
					sample.AllocatedBytes, sample.Gc0, sample.Gc1, sample.Gc2, sample.ReadBytes,
					sample.WriteBytes, sample.LogBytes, sample.Actors, sample.Effects, sample.HostLoad,
					sample.CpuFrequency, sample.Thermal));

			Log.Write(channel, "module\tidentity\tcalls\ttotal_ms\tmax_ms\tqueued_orders");
			foreach (var item in modules.OrderBy(x => x.Key, StringComparer.Ordinal))
				Log.Write(channel, string.Format(CultureInfo.InvariantCulture, "module\t{0}\t{1}\t{2:F3}\t{3:F3}\t{4}",
					item.Key, item.Value.Calls, item.Value.TotalMilliseconds, item.Value.MaximumMilliseconds, item.Value.Orders));

			Log.Write(channel, "logic-phase\tidentity\tcalls\ttotal_ms\tmax_ms");
			foreach (var item in logicPhases.OrderBy(x => x.Key, StringComparer.Ordinal))
				Log.Write(channel, string.Format(CultureInfo.InvariantCulture, "logic-phase\t{0}\t{1}\t{2:F3}\t{3:F3}",
					item.Key, item.Value.Calls, item.Value.TotalMilliseconds, item.Value.MaximumMilliseconds));

			Log.Write(channel, "tail\ttick_or_frame\tsource\tmilliseconds");
			foreach (var item in tailEvents.OrderBy(x => x.Tick).ThenBy(x => x.Source, StringComparer.Ordinal))
				Log.Write(channel, string.Format(CultureInfo.InvariantCulture, "tail\t{0}\t{1}\t{2:F3}", item.Tick, item.Source, item.Milliseconds));
		}

		static void WriteDistribution(string channel, string name, FixedLatencyHistogram histogram)
		{
			Log.Write(channel, string.Format(CultureInfo.InvariantCulture,
				"distribution\t{0}\tcount={1}\tmean_ms={2:F3}\tp50_ms={3:F0}\tp95_ms={4:F0}\tp99_ms={5:F0}\tmax_ms={6:F3}\tfreeze_threshold_ms={7:F0}\tfreeze_count={8}\tcadence={9:F1}",
				name, histogram.Count, histogram.Count == 0 ? 0 : histogram.Sum / histogram.Count,
				histogram.Quantile(.5), histogram.Quantile(.95), histogram.Quantile(.99), histogram.Maximum,
				FreezeThresholdMs, histogram.FreezeCount, histogram.Cadence()));
		}
	}
}
