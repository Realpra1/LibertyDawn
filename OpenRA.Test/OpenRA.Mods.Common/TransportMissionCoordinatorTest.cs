#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class TransportMissionCoordinatorTest
	{
		[Test]
		public void ReservationsAreExclusiveAndReleasedAtomically()
		{
			var coordinator = new TransportMissionCoordinator(2);
			var first = coordinator.TryReserve(new uint[] { 8, 2, 8 });

			Assert.That(first, Is.GreaterThan(0));
			Assert.That(coordinator.IsReserved(2), Is.True);
			Assert.That(coordinator.IsReserved(8), Is.True);
			Assert.That(coordinator.TryReserve(new uint[] { 8, 10 }), Is.Zero);

			coordinator.Release(first);
			Assert.That(coordinator.IsReserved(2), Is.False);
			Assert.That(coordinator.IsReserved(8), Is.False);
			Assert.That(coordinator.TryReserve(new uint[] { 8, 10 }), Is.GreaterThan(0));
		}

		[Test]
		public void MissionCountIsBounded()
		{
			var coordinator = new TransportMissionCoordinator(1);
			var first = coordinator.TryReserve(new uint[] { 1, 2 });

			Assert.That(first, Is.GreaterThan(0));
			Assert.That(coordinator.MissionCount, Is.EqualTo(1));
			Assert.That(coordinator.TryReserve(new uint[] { 3, 4 }), Is.Zero);
		}
	}
}
