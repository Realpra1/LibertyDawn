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
			Assert.That(UnitCapPolicy.AllowedQueueAmount(5, 300, 400, true, 74, 75, true), Is.EqualTo(1));
			Assert.That(UnitCapPolicy.AllowedQueueAmount(1, 300, 400, true, 75, 75, true), Is.Zero);
		}

		[Test]
		public void UpgradesDoNotConsumeMobileUnitCapacity()
		{
			Assert.That(UnitCapPolicy.AllowedQueueAmount(1, 400, 400, false, 75, 75, false), Is.EqualTo(1));
		}
	}
}
