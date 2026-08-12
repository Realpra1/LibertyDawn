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
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class AdvancedBotCpuFailsafeControllerTest
	{
		static readonly SimulationPacingSample Healthy = new SimulationPacingSample(true, true, 1, "normal-real-time");
		static readonly SimulationPacingSample Slow = new SimulationPacingSample(true, true, 1.2, "normal-real-time");
		static readonly SimulationPacingSample Maximum = new SimulationPacingSample(true, false, 0.1, "maximum-speed");

		static AdvancedBotCpuFailsafeController Create(params string[] modules)
		{
			return new AdvancedBotCpuFailsafeController(modules, 2, 2, 1, 0.1f, 0.5f);
		}

		[Test]
		public void SustainedNormalSpeedLagDisablesDominantModuleWithStableTieBreak()
		{
			var controller = Create("cheap", "expensive");
			var times = new Dictionary<string, double> { { "cheap", 20 }, { "expensive", 70 } };

			Assert.That(controller.Update(Slow, 200, times).Transition, Is.EqualTo("held"));
			var decision = controller.Update(Slow, 200, times);

			Assert.That(decision.Transition, Is.EqualTo("disabled"));
			Assert.That(decision.Module, Is.EqualTo("expensive"));
			Assert.That(controller.IsEnabled("expensive"), Is.False);
		}

		[Test]
		public void IsolatedSpikeDoesNotDisableAndExactlyHalfBudgetIsHealthy()
		{
			var controller = Create("advanced");
			var half = new Dictionary<string, double> { { "advanced", 50 } };

			Assert.That(controller.Update(Slow, 100, half).Transition, Is.EqualTo("held"));
			Assert.That(controller.Update(Healthy, 100, half).Transition, Is.EqualTo("healthy"));
			Assert.That(controller.Update(Maximum, 100, half).Transition, Is.EqualTo("healthy"));
			Assert.That(controller.IsEnabled("advanced"), Is.True);
		}

		[Test]
		public void UnreliablePacingUsesCollectiveHalfCpuBudget()
		{
			var controller = Create("first", "second");
			var collective = new Dictionary<string, double> { { "first", 30 }, { "second", 25 } };

			controller.Update(Maximum, 100, collective);
			var decision = controller.Update(Maximum, 100, collective);

			Assert.That(decision.Transition, Is.EqualTo("disabled"));
			Assert.That(decision.Module, Is.EqualTo("first"));
			Assert.That(decision.Share, Is.EqualTo(0.55).Within(0.001));
		}

		[Test]
		public void RecoveryIsSerializedAndOffenderWaitsLonger()
		{
			var controller = Create("cheap", "offender");
			var times = new Dictionary<string, double> { { "cheap", 20 }, { "offender", 80 } };
			controller.Update(Slow, 100, times);
			controller.Update(Slow, 100, times);

			Assert.That(controller.Update(Healthy, 100, times).Transition, Is.EqualTo("cooldown"));
			Assert.That(controller.Update(Healthy, 100, times).Transition, Is.EqualTo("cooldown"));
			Assert.That(controller.Update(Healthy, 100, times).Transition, Is.EqualTo("enabled-probe"));
			Assert.That(controller.IsEnabled("offender"), Is.True);
		}

		[Test]
		public void FailedRecoveryProbeIsImmediatelyReshed()
		{
			var controller = Create("advanced");
			var times = new Dictionary<string, double> { { "advanced", 75 } };
			controller.Update(Slow, 100, times);
			controller.Update(Slow, 100, times);
			controller.Update(Healthy, 100, times);
			controller.Update(Healthy, 100, times);
			controller.Update(Healthy, 100, times);

			var decision = controller.Update(Slow, 100, times);
			Assert.That(decision.Transition, Is.EqualTo("re-shed"));
			Assert.That(controller.IsEnabled("advanced"), Is.False);
		}

		[Test]
		public void ExportImportPreservesDegradedStateAndCooldownProgress()
		{
			var first = Create("advanced");
			var times = new Dictionary<string, double> { { "advanced", 75 } };
			first.Update(Slow, 100, times);
			first.Update(Slow, 100, times);
			first.Update(Healthy, 100, times);

			var restored = Create("advanced");
			restored.ImportState(first.ExportState());

			Assert.That(restored.IsEnabled("advanced"), Is.False);
			Assert.That(restored.Update(Healthy, 100, times).Transition, Is.EqualTo("cooldown"));
			Assert.That(restored.Update(Healthy, 100, times).Transition, Is.EqualTo("enabled-probe"));
		}

		[Test]
		public void PacingSamplerRejectsMaximumSpeedAndRegression()
		{
			var sampler = new SimulationPacingSampler(10);
			Assert.That(sampler.Update(0, 0, 20, 0).Sampled, Is.False);
			var maximum = sampler.Update(10, 10, 20, 20, maximumSpeed: true);
			Assert.That(maximum.Sampled, Is.True);
			Assert.That(maximum.Reliable, Is.False);
			Assert.That(maximum.Source, Is.EqualTo("maximum-speed"));
			Assert.That(sampler.Update(5, 5, 20, 10).Sampled, Is.False);
		}
	}
}
