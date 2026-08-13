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

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenRA.Primitives;
using OpenRA.Support;

namespace OpenRA.Test
{
	[TestFixture]
	[NonParallelizable]
	class BenchmarkTest
	{
		Cache<string, PerfItem> originalItems;

		[SetUp]
		public void SetUp()
		{
			originalItems = PerfHistory.Items;
			PerfHistory.Items = new Cache<string, PerfItem>(name => new PerfItem(name, Color.White));
		}

		[TearDown]
		public void TearDown()
		{
			PerfHistory.Items = originalItems;
		}

		[TestCase(TestName = "Benchmark records each tick once and starts late channels at discovery")]
		public void RecordsCompletedTicksOnce()
		{
			var writers = new Dictionary<string, RecordingWriter>();
			var benchmark = new Benchmark("run-", filename => writers[filename] = new RecordingWriter(), 2);

			RecordValue("tick_time", 1.5);
			benchmark.Tick(1);
			RecordValue("tick_time", 99);
			benchmark.Tick(1);
			PerfHistory.Increment("tick_time", 2.5);
			PerfHistory.Increment("render", 3.5);
			PerfHistory.Tick();
			benchmark.Tick(2);

			Assert.That(writers["run-tick_time.csv"].Text, Is.EqualTo("tick,time [ms]\n1,1.5\n2,2.5\n"));
			Assert.That(writers["run-render.csv"].Text, Is.EqualTo("tick,time [ms]\n2,3.5\n"));
			Assert.That(writers["run-tick_time.csv"].FlushCount, Is.EqualTo(2), "header and full batch should be visible");

			benchmark.Write();
			benchmark.Write();
			benchmark.Tick(3);

			Assert.That(writers["run-tick_time.csv"].DisposeCount, Is.EqualTo(1));
			Assert.That(writers["run-render.csv"].DisposeCount, Is.EqualTo(1));
			Assert.That(writers["run-tick_time.csv"].Text, Does.Not.Contain("3,"));
		}

		[TestCase(TestName = "Benchmark periodically flushes a bounded batch during long runs")]
		public void FlushesBoundedBatchesIncrementally()
		{
			var writers = new Dictionary<string, RecordingWriter>();
			var benchmark = new Benchmark("run-", filename => writers[filename] = new RecordingWriter(), 4);

			for (var tick = 1; tick <= 12; tick++)
			{
				RecordValue("tick_time", tick);
				benchmark.Tick(tick);
				for (var attempt = 0; attempt < 100; attempt++)
					benchmark.Tick(tick);
			}

			Assert.That(writers["run-tick_time.csv"].FlushCount, Is.EqualTo(4), "header plus three fixed batches");
			Assert.That(writers["run-tick_time.csv"].Text.Split('\n').Length, Is.EqualTo(14));
			benchmark.Write();
			Assert.That(writers["run-tick_time.csv"].DisposeCount, Is.EqualTo(1));
		}

		[TestCase(TestName = "Benchmark finalizes every channel once when one writer fails")]
		public void FinalizesEveryChannelAfterWriterFailure()
		{
			var writers = new Dictionary<string, RecordingWriter>();
			var benchmark = new Benchmark("run-", filename =>
			{
				var writer = new RecordingWriter();
				writers.Add(filename, writer);
				return writer;
			}, 4);

			RecordValue("tick_time", 1);
			RecordValue("render", 2);
			benchmark.Tick(1);
			writers["run-tick_time.csv"].ThrowOnDispose = true;

			Assert.Throws<IOException>(() => benchmark.Write());
			Assert.That(writers["run-tick_time.csv"].DisposeCount, Is.EqualTo(1));
			Assert.That(writers["run-render.csv"].DisposeCount, Is.EqualTo(1));
			Assert.DoesNotThrow(() => benchmark.Write());
		}

		static void RecordValue(string channel, double value)
		{
			PerfHistory.Increment(channel, value);
			PerfHistory.Tick();
		}

		sealed class RecordingWriter : StringWriter
		{
			public bool ThrowOnDispose { get; set; }
			public int FlushCount { get; private set; }
			public int DisposeCount { get; private set; }
			public string Text => ToString().Replace("\r\n", "\n");

			public override void Flush()
			{
				FlushCount++;
				base.Flush();
			}

			protected override void Dispose(bool disposing)
			{
				DisposeCount++;
				if (ThrowOnDispose)
					throw new IOException("Expected test failure.");

				base.Dispose(disposing);
			}
		}
	}
}
