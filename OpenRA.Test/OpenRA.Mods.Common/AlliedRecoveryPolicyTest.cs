#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits.BotModules.BotModuleLogic;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class AlliedRecoveryPolicyTest
	{
		[Test]
		public void OnlyCrippledButRecoverableAlliesNeedAid()
		{
			var crippled = new AlliedRecoverySnapshot(0, 0, 0, 0, 1, 0);
			Assert.That(AlliedRecoveryPolicy.ShouldAid(crippled, 0), Is.True);

			Assert.That(AlliedRecoveryPolicy.ShouldAid(
				new AlliedRecoverySnapshot(1, 0, 0, 0, 1, 0), 0), Is.False, "cash recovery stops aid");
			Assert.That(AlliedRecoveryPolicy.ShouldAid(
				new AlliedRecoverySnapshot(0, 1, 0, 0, 1, 0), 0), Is.False, "harvester recovery stops aid");
			Assert.That(AlliedRecoveryPolicy.ShouldAid(
				new AlliedRecoverySnapshot(0, 0, 1, 0, 1, 0), 0), Is.False, "refinery recovery stops aid");
			Assert.That(AlliedRecoveryPolicy.ShouldAid(
				new AlliedRecoverySnapshot(0, 0, 0, 1, 1, 0), 0), Is.False, "MCV recovery stops aid");
		}

		[Test]
		public void EliminatedAlliesDoNotConsumeAid()
		{
			var eliminated = new AlliedRecoverySnapshot(0, 0, 0, 0, 0, 0);
			Assert.That(AlliedRecoveryPolicy.NeedsAid(eliminated, 0), Is.True);
			Assert.That(AlliedRecoveryPolicy.CanRecover(eliminated), Is.False);
			Assert.That(AlliedRecoveryPolicy.ShouldAid(eliminated, 0), Is.False);
		}

		[Test]
		public void DispatchCapacityIsPerAvailableFactory()
		{
			Assert.That(AlliedRecoveryPolicy.AvailableDispatches(2, 0, 0), Is.EqualTo(2));
			Assert.That(AlliedRecoveryPolicy.AvailableDispatches(2, 1, 0), Is.EqualTo(1));
			Assert.That(AlliedRecoveryPolicy.AvailableDispatches(2, 1, 1), Is.Zero);
			Assert.That(AlliedRecoveryPolicy.AvailableDispatches(1, 2, 0), Is.Zero);
		}
	}
}
