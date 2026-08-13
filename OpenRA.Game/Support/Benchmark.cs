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
using System.IO;
using System.Runtime.ExceptionServices;

namespace OpenRA.Support
{
	class Benchmark
	{
		const int FlushInterval = 256;
		const int CreateFileMaxRetryCount = 128;

		readonly string prefix;
		readonly int flushInterval;
		readonly Func<string, TextWriter> writerFactory;
		readonly Dictionary<string, BenchmarkChannel> channels = new Dictionary<string, BenchmarkChannel>();
		readonly PeriodicStallReport periodicStallReport = new PeriodicStallReport();
		int? lastTick;
		bool finished;

		public Benchmark(string prefix)
			: this(prefix, CreateWriter, FlushInterval) { }

		internal Benchmark(string prefix, Func<string, TextWriter> writerFactory, int flushInterval)
		{
			this.prefix = prefix;
			this.writerFactory = writerFactory ?? throw new ArgumentNullException(nameof(writerFactory));
			this.flushInterval = flushInterval > 0 ? flushInterval : throw new ArgumentOutOfRangeException(nameof(flushInterval));
		}

		public void Tick(int localTick, World world)
		{
			if (finished || lastTick == localTick)
				return;

			lastTick = localTick;
			foreach (var item in PerfHistory.Items)
				channels.GetOrAdd(item.Key, CreateChannel).Write(localTick, item.Value.LastValue);

			periodicStallReport.RecordTick(localTick, world);
		}

		public void Render(int renderFrame)
		{
			periodicStallReport.RecordRender(renderFrame);
		}

		public void BotModule(int tick, int playerIndex, string module, double milliseconds, int queuedOrders)
		{
			periodicStallReport.RecordModule(tick, playerIndex, module, milliseconds, queuedOrders);
		}

		public void LogicPhase(int tick, string phase, double milliseconds)
		{
			periodicStallReport.RecordLogicPhase(tick, phase, milliseconds);
		}

		BenchmarkChannel CreateChannel(string name)
		{
			var writer = writerFactory($"{prefix}{name}.csv");
			try
			{
				return new BenchmarkChannel(writer, flushInterval);
			}
			catch (Exception error)
			{
				try
				{
					writer.Dispose();
				}
				catch (Exception disposeError)
				{
					throw new AggregateException(error, disposeError);
				}

				throw;
			}
		}

		static TextWriter CreateWriter(string filename)
		{
			var path = Path.Combine(Platform.SupportDir, "Logs");
			Directory.CreateDirectory(path);

			IOException error = null;
			for (var i = 0; i < CreateFileMaxRetryCount; i++)
			{
				var candidate = Path.Combine(path, i > 0 ? $"{filename}.{i}" : filename);
				try
				{
					return File.CreateText(candidate);
				}
				catch (IOException e)
				{
					error = e;
				}
			}

			throw new ApplicationException($"Error creating benchmark file \"{filename}\"", error);
		}

		public void Write()
		{
			if (finished)
				return;

			periodicStallReport.Write(prefix);
			finished = true;
			Exception error = null;
			foreach (var channel in channels.Values)
			{
				try
				{
					channel.Finish();
				}
				catch (Exception e)
				{
					if (error == null)
						error = e;
				}
			}

			if (error != null)
				ExceptionDispatchInfo.Capture(error).Throw();
		}

		public void Reset()
		{
			Write();
			channels.Clear();
			lastTick = null;
			finished = false;
		}

		sealed class BenchmarkChannel
		{
			readonly TextWriter writer;
			readonly int flushInterval;
			int bufferedRows;
			bool finished;

			public BenchmarkChannel(TextWriter writer, int flushInterval)
			{
				this.writer = writer;
				this.flushInterval = flushInterval;
				writer.WriteLine("tick,time [ms]");
				writer.Flush();
			}

			public void Write(int tick, double value)
			{
				writer.WriteLine($"{tick},{value}");
				if (++bufferedRows < flushInterval)
					return;

				writer.Flush();
				bufferedRows = 0;
			}

			public void Finish()
			{
				if (finished)
					return;

				finished = true;
				writer.Dispose();
			}
		}
	}
}
