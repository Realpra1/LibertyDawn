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
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class SmartEconomyPolicyTest
	{
		[Test]
		public void PressureRequiresSustainedEvidenceAndReleasesWithHysteresis()
		{
			var pressure = new SmartEconomyPressure(0, false);
			pressure = SmartEconomyPolicy.UpdatePressure(pressure, true, 25, 100, 25);
			Assert.That(pressure.Active, Is.False);

			pressure = SmartEconomyPolicy.UpdatePressure(pressure, true, 75, 100, 25);
			Assert.That(pressure.Active, Is.True);
			Assert.That(pressure.EvidenceTicks, Is.EqualTo(100));

			pressure = SmartEconomyPolicy.UpdatePressure(pressure, false, 50, 100, 25);
			Assert.That(pressure.Active, Is.True);

			pressure = SmartEconomyPolicy.UpdatePressure(pressure, false, 25, 100, 25);
			Assert.That(pressure.Active, Is.False);
		}

		[TestCase(0, 0)]
		[TestCase(1, 0)]
		[TestCase(2, 1)]
		[TestCase(4, 3)]
		public void WaitingHarvestersExcludeTheActiveServiceSlot(int linked, int expected)
		{
			Assert.That(SmartEconomyPolicy.WaitingHarvesters(linked), Is.EqualTo(expected));
		}

		[Test]
		public void CongestionRequiresEveryRefineryToBeOccupied()
		{
			Assert.That(SmartEconomyPolicy.WaitingHarvestersWhenAllRefineriesOccupied(Array.Empty<int>()), Is.Zero);
			Assert.That(SmartEconomyPolicy.WaitingHarvestersWhenAllRefineriesOccupied(new[] { 3, 0 }), Is.Zero);
			Assert.That(SmartEconomyPolicy.WaitingHarvestersWhenAllRefineriesOccupied(new[] { 1, 1 }), Is.Zero);
			Assert.That(SmartEconomyPolicy.WaitingHarvestersWhenAllRefineriesOccupied(new[] { 3, 1 }), Is.EqualTo(2));
			Assert.That(SmartEconomyPolicy.WaitingHarvestersWhenAllRefineriesOccupied(new[] { 2, 2 }), Is.EqualTo(2));
		}

		[Test]
		public void StoragePressureUsesCapacityAndHandlesMissingStorage()
		{
			Assert.That(SmartEconomyPolicy.StoragePressure(799, 1000, 80), Is.False);
			Assert.That(SmartEconomyPolicy.StoragePressure(800, 1000, 80), Is.True);
			Assert.That(SmartEconomyPolicy.StoragePressure(100, 0, 80), Is.False);
		}

		[Test]
		public void ExcessCashRaisesExpansionTargetToConfiguredCeiling()
		{
			Assert.That(SmartEconomyPolicy.DesiredExpansionAssets(34999, 35000, 8), Is.Zero);
			Assert.That(SmartEconomyPolicy.DesiredExpansionAssets(35000, 35000, 8), Is.EqualTo(2));
			Assert.That(SmartEconomyPolicy.DesiredExpansionAssets(70000, 35000, 8), Is.EqualTo(3));
			Assert.That(SmartEconomyPolicy.DesiredExpansionAssets(105000, 35000, 8), Is.EqualTo(4));
			Assert.That(SmartEconomyPolicy.DesiredExpansionAssets(210000, 35000, 8), Is.EqualTo(7));
			Assert.That(SmartEconomyPolicy.DesiredExpansionAssets(999999, 35000, 8), Is.EqualTo(8));
		}

		[Test]
		public void ExpansionWaitsForConfiguredArmySupport()
		{
			Assert.That(SmartEconomyPolicy.ExpansionArmyReady(3319, 34570, 20), Is.False);
			Assert.That(SmartEconomyPolicy.ExpansionArmyReady(6914, 34570, 20), Is.True);
			Assert.That(SmartEconomyPolicy.ExpansionArmyReady(0, 34570, 0), Is.True);
			Assert.That(SmartEconomyPolicy.ExpansionArmyReady(0, 0, 20), Is.True);
		}
	}
}
