#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits.BotModules;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class UnassignedCombatUnitRegistryTest
	{
		[Test]
		public void RegistrationIsImmediateStableAndDuplicateSafe()
		{
			var registry = new UnassignedCombatUnitRegistry();

			Assert.That(registry.Register(7), Is.True);
			Assert.That(registry.Register(3), Is.True);
			Assert.That(registry.Register(7), Is.False);
			Assert.That(registry.Register(0), Is.False);
			Assert.That(registry.ActorIds, Is.EqualTo(new uint[] { 3, 7 }));
		}

		[Test]
		public void ClaimAndLifecycleRemovalLeaveNoStaleEntry()
		{
			var registry = new UnassignedCombatUnitRegistry();
			registry.Register(11);
			registry.Register(12);
			registry.Register(13);

			Assert.That(registry.Remove(12), Is.True, "Squad or dedicated-controller claim must consume the entry.");
			Assert.That(registry.Remove(11), Is.True, "Death, capture, transform, or cargo removal must clear the entry.");
			Assert.That(registry.Remove(11), Is.False, "Repeated lifecycle notifications must be idempotent.");
			Assert.That(registry.ActorIds, Is.EqualTo(new uint[] { 13 }));
		}

		[Test]
		public void ReleasedCohortCanEnterBeforePreviousOwnershipClears()
		{
			var registry = new UnassignedCombatUnitRegistry();

			Assert.That(registry.Register(21), Is.True);
			Assert.That(registry.Contains(21), Is.True,
				"The handoff registry must accept the actor before the former controller clears its reservation.");
			Assert.That(registry.Remove(21), Is.True, "A new valid controller must atomically consume the handoff.");
		}
	}
}
