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
using System.Linq;
using NUnit.Framework;
using OpenRA.Support;

namespace OpenRA.Test
{
	[TestFixture]
	public class PeriodicStallClassifierTest
	{
		[Test]
		public void DistinguishesUniformSlowWorkFromSeparatedPeriodicStalls()
		{
			var uniform = Enumerable.Repeat(60.0, 100).ToArray();
			var ticks = Enumerable.Range(1, 100).ToArray();
			var uniformResult = PeriodicStallClassifier.Classify(uniform, ticks, 50);
			Assert.That(uniformResult.HasSeparatedTail, Is.False);
			Assert.That(uniformResult.IsPeriodic, Is.False);

			var periodic = Enumerable.Repeat(10.0, 100).ToArray();
			foreach (var tick in new[] { 25, 50, 75, 100 })
				periodic[tick - 1] = 80;

			var periodicResult = PeriodicStallClassifier.Classify(periodic, ticks, 50);
			Assert.That(periodicResult.HasSeparatedTail, Is.True);
			Assert.That(periodicResult.IsPeriodic, Is.True);
			Assert.That(periodicResult.Cadence, Is.EqualTo(25));
		}

		[Test]
		public void ReportsNearestRankQuantilesAndRejectsIsolatedOrShortInputs()
		{
			var values = Enumerable.Range(1, 100).Select(x => (double)x).ToArray();
			var ticks = Enumerable.Range(1, 100).ToArray();
			var result = PeriodicStallClassifier.Classify(values, ticks, 99);
			Assert.That(result.Median, Is.EqualTo(50));
			Assert.That(result.P95, Is.EqualTo(95));
			Assert.That(result.P99, Is.EqualTo(99));
			Assert.That(result.Maximum, Is.EqualTo(100));
			Assert.That(result.IsPeriodic, Is.False);

			var shortResult = PeriodicStallClassifier.Classify(new[] { 10.0, 80.0 }, new[] { 1, 2 }, 50);
			Assert.That(shortResult.IsPeriodic, Is.False);
			Assert.That(PeriodicStallClassifier.Classify(Array.Empty<double>(), Array.Empty<int>(), 50).SampleCount, Is.Zero);
		}

		[Test]
		public void IgnoresDuplicateAndRegressingClockEdgesForCadence()
		{
			var values = Enumerable.Repeat(10.0, 100).ToArray();
			var ticks = Enumerable.Range(1, 100).ToArray();
			for (var i = 0; i < 5; i++)
				values[i] = 80;
			ticks[0] = 10;
			ticks[1] = 10;
			ticks[2] = 9;
			ticks[3] = 20;
			ticks[4] = 30;
			var result = PeriodicStallClassifier.Classify(values, ticks, 50);

			Assert.That(result.FreezeCount, Is.EqualTo(3));
			Assert.That(result.IsPeriodic, Is.True);
			Assert.That(result.Cadence, Is.EqualTo(10));
		}
	}
}
