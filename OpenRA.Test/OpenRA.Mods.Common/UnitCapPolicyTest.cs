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
	public class UnitCapPolicyTest
	{
		[Test]
		public void OrdinaryUnitsUseOnlyTheSharedRemainingCapacity()
		{
			Assert.That(UnitCapPolicy.AllowedQueueAmount(5, 398, 400, false, 0, 75, true), Is.EqualTo(2));
			Assert.That(UnitCapPolicy.AllowedQueueAmount(1, 400, 400, false, 0, 75, true), Is.Zero);
		}

		[Test]
		public void HarvestersRespectBothSharedAndHarvesterCaps()
		{
			Assert.That(UnitCapPolicy.AllowedQueueAmount(5, 300, 400, true, 89, 90, true), Is.EqualTo(1));
			Assert.That(UnitCapPolicy.AllowedQueueAmount(1, 300, 400, true, 90, 90, true), Is.Zero);
			Assert.That(UnitCapPolicy.AllowedQueueAmount(5, 500, 0, true, 89, 90, true), Is.EqualTo(1),
				"The harvester cap must remain active while the adaptive mobile cap is unlimited.");
		}

		[Test]
		public void UpgradesDoNotConsumeMobileUnitCapacity()
		{
			Assert.That(UnitCapPolicy.AllowedQueueAmount(1, 400, 400, false, 75, 75, false), Is.EqualTo(1));
		}

		[Test]
		public void FreeActorsRespectTheSameLiveAndQueuedHarvesterCeiling()
		{
			Assert.That(SharedActorLimitPolicy.CanSpawn(89, 0, 90), Is.True);
			Assert.That(SharedActorLimitPolicy.CanSpawn(85, 5, 90), Is.False);
			Assert.That(SharedActorLimitPolicy.CanSpawn(90, 0, 90), Is.False);
			Assert.That(SharedActorLimitPolicy.CanSpawn(100, 100, 0), Is.True);
		}

		[Test]
		public void SameTickReservationsShareTheRemainingHarvesterSlots()
		{
			Assert.That(SharedActorLimitPolicy.AllowedAmount(3, 85, 0, 90), Is.EqualTo(3));
			Assert.That(SharedActorLimitPolicy.AllowedAmount(3, 85, 3, 90), Is.EqualTo(2));
			Assert.That(SharedActorLimitPolicy.AllowedAmount(1, 85, 5, 90), Is.Zero);
			Assert.That(SharedActorLimitPolicy.AllowedAmount(3, 100, 100, 0), Is.EqualTo(3));
		}
	}
}
