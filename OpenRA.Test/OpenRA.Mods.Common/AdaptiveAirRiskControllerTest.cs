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

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class AdaptiveAirRiskControllerTest
	{
		[Test]
		public void AuthoredBehaviorIsTheFloor()
		{
			var controller = new AdaptiveAirRiskController();
			controller.Update(1, 0, 0, 3, 100, 500);

			Assert.That(controller.BonusBasisPoints, Is.Zero);
			Assert.That(controller.MultiplierBasisPoints, Is.EqualTo(10000));
			Assert.That(controller.Multiplier, Is.EqualTo(1m));
		}

		[Test]
		public void SustainedFullAmmoGrowthCanExceedOneHundredPercentBonus()
		{
			var controller = new AdaptiveAirRiskController();
			for (var tick = 1; tick <= 12; tick++)
				controller.Update(tick, 4, 4, 3, 1000, 100);

			Assert.That(controller.BonusBasisPoints, Is.EqualTo(12000));
			Assert.That(controller.Multiplier, Is.EqualTo(2.2m));
		}

		[Test]
		public void ReadinessGrowthScalesWithFullAmmoShare()
		{
			var controller = new AdaptiveAirRiskController();
			controller.Update(1, 3, 4, 3, 1000, 100);

			Assert.That(controller.BonusBasisPoints, Is.EqualTo(750));
		}

		[Test]
		public void KillValueAccumulatesUntilTheNextUpdate()
		{
			var controller = new AdaptiveAirRiskController();
			controller.RecordKill(300, 2);
			controller.RecordKill(700, 2);
			controller.Update(1, 0, 4, 3, 100, 100);

			Assert.That(controller.BonusBasisPoints, Is.EqualTo(2000));
			controller.Update(2, 0, 4, 3, 100, 100);
			Assert.That(controller.BonusBasisPoints, Is.EqualTo(2000));
		}

		[Test]
		public void LowUnitCountDecaysBonus()
		{
			var controller = new AdaptiveAirRiskController();
			controller.Update(1, 4, 4, 3, 1000, 250);
			controller.Update(2, 0, 1, 3, 1000, 250);

			Assert.That(controller.BonusBasisPoints, Is.EqualTo(750));
		}

		[Test]
		public void EnemyLossRollsBackToOneMinuteCheckpoint()
		{
			var controller = new AdaptiveAirRiskController();
			controller.Update(100, 4, 4, 3, 1000, 100);
			controller.Update(1600, 4, 4, 3, 1000, 100);
			controller.Update(3100, 4, 4, 3, 1000, 100);
			controller.RecordEnemyLoss(3100, 3000, 250);

			Assert.That(controller.BonusBasisPoints, Is.EqualTo(1000));
		}

		[Test]
		public void RepeatedLossesInOneWindowReachFloor()
		{
			var controller = new AdaptiveAirRiskController();
			controller.Update(100, 4, 4, 3, 4000, 100);
			controller.Update(3100, 4, 4, 3, 4000, 100);

			controller.RecordEnemyLoss(3100, 3000, 1500);
			Assert.That(controller.BonusBasisPoints, Is.EqualTo(4000));
			controller.RecordEnemyLoss(3100, 3000, 1500);
			controller.RecordEnemyLoss(3100, 3000, 1500);
			controller.RecordEnemyLoss(3100, 3000, 1500);

			Assert.That(controller.BonusBasisPoints, Is.Zero);
		}

		[Test]
		public void LossBeforeEnoughHistoryRollsBackToFloor()
		{
			var controller = new AdaptiveAirRiskController();
			controller.Update(1000, 4, 4, 3, 2000, 100);
			controller.RecordEnemyLoss(1500, 3000, 100);

			Assert.That(controller.BonusBasisPoints, Is.Zero);
		}

		[Test]
		public void GrowthStopsAtOverflowSafetyClamp()
		{
			var controller = new AdaptiveAirRiskController(safetyClampBasisPoints: 50000);
			controller.RecordKill(int.MaxValue, int.MaxValue);
			controller.Update(1, 4, 4, 3, int.MaxValue, 0);

			Assert.That(controller.BonusBasisPoints, Is.EqualTo(50000));
			Assert.That(controller.MultiplierBasisPoints, Is.EqualTo(60000));
		}

		[Test]
		public void EquivalentSequencesAreDeterministic()
		{
			var first = RunSequence();
			var second = RunSequence();

			Assert.That(second.BonusBasisPoints, Is.EqualTo(first.BonusBasisPoints));
			Assert.That(second.MultiplierBasisPoints, Is.EqualTo(first.MultiplierBasisPoints));
			Assert.That(second.ExportState().History, Has.Length.EqualTo(first.ExportState().History.Length));
		}

		[Test]
		public void ExportedStateRoundTripsPendingCreditAndBoundedHistory()
		{
			var original = new AdaptiveAirRiskController(historyCapacity: 3);
			for (var tick = 1; tick <= 5; tick++)
				original.Update(tick, 4, 4, 3, 100, 10);
			original.RecordKill(50, 2);

			var state = original.ExportState();
			Assert.That(state.History, Has.Length.EqualTo(3));

			var restored = new AdaptiveAirRiskController(historyCapacity: 3);
			restored.ImportState(state);
			restored.Update(6, 0, 4, 3, 100, 10);

			Assert.That(restored.BonusBasisPoints, Is.EqualTo(600));
			Assert.That(restored.ExportState().History, Has.Length.EqualTo(3));
			Assert.That(restored.ExportState().History[0].Tick, Is.EqualTo(4));
		}

		static AdaptiveAirRiskController RunSequence()
		{
			var controller = new AdaptiveAirRiskController();
			controller.RecordKill(250, 2);
			controller.Update(100, 4, 4, 3, 300, 100);
			controller.Update(1600, 2, 2, 3, 300, 100);
			controller.RecordKill(100, 3);
			controller.Update(3100, 0, 4, 3, 300, 100);
			controller.RecordEnemyLoss(3100, 3000, 200);
			return controller;
		}
	}
}
