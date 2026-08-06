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
	public class StrategicGroundScoringTest
	{
		[Test]
		public void DefenderResistanceDecaysButDoesNotDisappearAtFiveToOne()
		{
			Assert.That(StrategicGroundScoring.EffectiveDefenderValue(0, 1000, 50), Is.EqualTo(1000));
			Assert.That(StrategicGroundScoring.EffectiveDefenderValue(1000, 1000, 50), Is.EqualTo(500));
			Assert.That(StrategicGroundScoring.EffectiveDefenderValue(3000, 1000, 50), Is.EqualTo(125));
			Assert.That(StrategicGroundScoring.EffectiveDefenderValue(5000, 1000, 50), Is.EqualTo(31));
		}

		[Test]
		public void FiveToOneOvermatchIsEffectivelyUndefendedOnlyAtBoundary()
		{
			Assert.That(StrategicGroundScoring.IsEffectivelyUndefended(4999, 1000, 5), Is.False);
			Assert.That(StrategicGroundScoring.IsEffectivelyUndefended(5000, 1000, 5), Is.True);
			Assert.That(StrategicGroundScoring.IsEffectivelyUndefended(1, 0, 5), Is.True);
		}

		[Test]
		public void FormationOnlyWaitsForMaterialReinforcementWaves()
		{
			Assert.That(StrategicGroundScoring.ShouldHoldForReinforcements(0, 20, 4, 100), Is.False);
			Assert.That(StrategicGroundScoring.ShouldHoldForReinforcements(8, 3, 4, 100), Is.False);
			Assert.That(StrategicGroundScoring.ShouldHoldForReinforcements(8, 7, 4, 100), Is.False);
			Assert.That(StrategicGroundScoring.ShouldHoldForReinforcements(8, 8, 4, 100), Is.True);
			Assert.That(StrategicGroundScoring.ShouldHoldForReinforcements(12, 6, 4, 50), Is.True);
		}

		[Test]
		public void SlowestMemberMakesTheSameJourneyMoreExpensive()
		{
			var slow = StrategicGroundScoring.ScoreCell(5000, 5000, 0, 60, 50, 100, 8, 50);
			var fast = StrategicGroundScoring.ScoreCell(5000, 5000, 0, 60, 100, 100, 8, 50);
			Assert.That(fast, Is.GreaterThan(slow));
		}

		[Test]
		public void FinishingPriorityCannotOvertakeTheAuthoredPriorityTier()
		{
			Assert.That(StrategicGroundScoring.RemainingHealthPriority(1000, 100, 100), Is.EqualTo(1000));
			Assert.That(StrategicGroundScoring.RemainingHealthPriority(1000, 1, 100), Is.EqualTo(1990));
			Assert.That(StrategicGroundScoring.RemainingHealthPriority(1000, 0, 100), Is.LessThan(2000));
		}

		[Test]
		public void RichExposedCellOutranksDefendedOrDistantAlternatives()
		{
			var exposed = StrategicGroundScoring.ScoreCell(6000, 5000, 0, 20, 100, 100, 8, 50);
			var defended = StrategicGroundScoring.ScoreCell(6000, 1000, 5000, 20, 100, 100, 8, 50);
			var distant = StrategicGroundScoring.ScoreCell(6000, 5000, 0, 100, 100, 100, 8, 50);
			Assert.That(exposed, Is.GreaterThan(defended));
			Assert.That(exposed, Is.GreaterThan(distant));
		}
	}
}
