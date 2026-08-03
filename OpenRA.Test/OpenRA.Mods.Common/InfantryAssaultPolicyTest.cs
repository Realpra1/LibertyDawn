#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class InfantryAssaultPolicyTest
	{
		[Test]
		public void StrategySelectionRespectsBotEligibilityAndConfiguredChance()
		{
			Assert.That(InfantryAssaultPolicy.SelectStrategy(true, 50, 49), Is.True);
			Assert.That(InfantryAssaultPolicy.SelectStrategy(true, 50, 50), Is.False);
			Assert.That(InfantryAssaultPolicy.SelectStrategy(false, 100, 0), Is.False);
		}

		[Test]
		public void FullTransportDepartsWithoutWaitingForTimeout()
		{
			Assert.That(InfantryAssaultPolicy.ReadyToTravel(8, 8, 3, 1, 750), Is.True);
			Assert.That(InfantryAssaultPolicy.ReadyToTravel(2, 8, 3, 750, 750), Is.False);
		}

		[Test]
		public void PartialViableTransportWaitsUntilGatheringTimeout()
		{
			Assert.That(InfantryAssaultPolicy.ReadyToTravel(3, 8, 3, 749, 750), Is.False);
			Assert.That(InfantryAssaultPolicy.ReadyToTravel(3, 8, 3, 750, 750), Is.True);
			Assert.That(InfantryAssaultPolicy.AbandonGathering(2, 3, 750, 750), Is.True);
		}

		[Test]
		public void TargetScoreBalancesEconomicValueAndTravelDistance()
		{
			var nearbyMediumTarget = InfantryAssaultPolicy.TargetScore(1000, 10);
			var distantMediumTarget = InfantryAssaultPolicy.TargetScore(1000, 50);
			var nearbyHighValueTarget = InfantryAssaultPolicy.TargetScore(3000, 10);

			Assert.That(nearbyMediumTarget, Is.GreaterThan(distantMediumTarget));
			Assert.That(nearbyHighValueTarget, Is.GreaterThan(nearbyMediumTarget));
		}
	}
}
