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

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class AdaptiveUnitCapControllerTest
	{
		[Test]
		public void HealthySimulationRemainsUnlimitedWithinTenPercentTolerance()
		{
			var controller = new AdaptiveUnitCapController(10, 0.1f, 100, 25, 3);
			controller.Update(0, 0, 20, 0, 250, 400);

			var sample = controller.Update(10, 10, 20, 219, 250, 400);

			Assert.That(sample.Sampled, Is.True);
			Assert.That(sample.Decision, Is.EqualTo("unlimited"));
			Assert.That(controller.EffectiveLimit, Is.Zero);
		}

		[Test]
		public void LagEnforcesCurrentCapacityAndContinuedLagLowersIt()
		{
			var controller = new AdaptiveUnitCapController(10, 0.1f, 100, 25, 3);
			controller.Update(0, 0, 20, 0, 500, 400);

			var enforced = controller.Update(10, 10, 20, 221, 500, 400);
			var reduced = controller.Update(20, 20, 20, 442, 500, 400);

			Assert.That(enforced.Decision, Is.EqualTo("enforced"));
			Assert.That(enforced.EffectiveLimit, Is.EqualTo(400));
			Assert.That(reduced.Decision, Is.EqualTo("reduced"));
			Assert.That(reduced.EffectiveLimit, Is.EqualTo(375));
		}

		[Test]
		public void ContinuedLagHoldsAtTheConfiguredFloor()
		{
			var controller = new AdaptiveUnitCapController(10, 0.1f, 100, 25, 3);
			controller.Update(0, 0, 20, 0, 50, 400);
			controller.Update(10, 10, 20, 221, 50, 400);

			var sample = controller.Update(20, 20, 20, 442, 50, 400);

			Assert.That(sample.Decision, Is.EqualTo("held"));
			Assert.That(sample.EffectiveLimit, Is.EqualTo(100));
		}

		[Test]
		public void ThreeHealthySamplesReleaseAnEnforcedCap()
		{
			var controller = new AdaptiveUnitCapController(10, 0.1f, 100, 25, 3);
			controller.Update(0, 0, 20, 0, 250, 400);
			controller.Update(10, 10, 20, 221, 250, 400);

			Assert.That(controller.Update(20, 20, 20, 421, 250, 400).Decision, Is.EqualTo("recovering"));
			Assert.That(controller.Update(30, 30, 20, 621, 250, 400).Decision, Is.EqualTo("recovering"));
			Assert.That(controller.Update(40, 40, 20, 821, 250, 400).Decision, Is.EqualTo("released"));
			Assert.That(controller.EffectiveLimit, Is.Zero);
		}

		[Test]
		public void PausedLocalFramesAreExcludedFromSlowdown()
		{
			var controller = new AdaptiveUnitCapController(10, 0.1f, 100, 25, 3);
			controller.Update(0, 0, 20, 0, 250, 400);

			var sample = controller.Update(10, 110, 20, 2200, 250, 400);

			Assert.That(sample.RealTimeRatio, Is.EqualTo(1d).Within(0.001));
			Assert.That(sample.Decision, Is.EqualTo("unlimited"));
		}
	}
}
