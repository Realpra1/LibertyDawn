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

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class BaseBuilderPowerPolicyTest
	{
		[Test]
		public void SecondsUseConfiguredGameClockTimestepWithCeiling()
		{
			Assert.That(BaseBuilderPowerPolicy.SecondsToTicks(300, 40), Is.EqualTo(7500));
			Assert.That(BaseBuilderPowerPolicy.SecondsToTicks(300, 35), Is.EqualTo(8572));
			Assert.That(BaseBuilderPowerPolicy.SecondsToTicks(300, 30), Is.EqualTo(10000));
		}

		[Test]
		public void OptionalBufferWaitsFiveMinutesAndYieldsToCriticalRecovery()
		{
			int Target(int tick, bool recovery) => BaseBuilderPowerPolicy.TargetExcessPower(
				tick, 40, recovery, 1, 700, 150, 300, 5, 1, 20);

			Assert.That(Target(7499, false), Is.EqualTo(1));
			Assert.That(Target(7500, false), Is.EqualTo(251));
			Assert.That(Target(9000, true), Is.EqualTo(1));
		}
	}
}
