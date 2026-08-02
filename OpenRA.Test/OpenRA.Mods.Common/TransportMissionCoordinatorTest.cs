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
	public class TransportMissionCoordinatorTest
	{
		[Test]
		public void ReservationIsAtomicWhenAnyActorIsAlreadyClaimed()
		{
			var coordinator = new TransportMissionCoordinator(4);
			var first = coordinator.TryReserve(new uint[] { 3, 1, 2 });

			Assert.That(first, Is.Not.Zero);
			Assert.That(coordinator.TryReserve(new uint[] { 4, 2 }), Is.Zero);
			Assert.That(coordinator.IsReserved(4), Is.False);
			Assert.That(coordinator.MissionCount, Is.EqualTo(1));
		}

		[Test]
		public void ReleaseMakesEveryMissionActorAvailable()
		{
			var coordinator = new TransportMissionCoordinator(2);
			var mission = coordinator.TryReserve(new uint[] { 10, 11, 12 });
			coordinator.Release(mission);

			Assert.That(coordinator.IsReserved(10), Is.False);
			Assert.That(coordinator.IsReserved(11), Is.False);
			Assert.That(coordinator.IsReserved(12), Is.False);
			Assert.That(coordinator.MissionCount, Is.Zero);
		}

		[Test]
		public void MissionLimitBoundsOutstandingWork()
		{
			var coordinator = new TransportMissionCoordinator(1);
			var first = coordinator.TryReserve(new uint[] { 1 });

			Assert.That(coordinator.TryReserve(new uint[] { 2 }), Is.Zero);
			coordinator.Release(first);
			Assert.That(coordinator.TryReserve(new uint[] { 2 }), Is.Not.Zero);
		}

		[Test]
		public void DuplicateActorIdsAreReservedOnce()
		{
			var coordinator = new TransportMissionCoordinator(1);
			var mission = coordinator.TryReserve(new uint[] { 5, 5, 5 });

			Assert.That(mission, Is.Not.Zero);
			Assert.That(coordinator.IsReservedBy(mission, 5), Is.True);
		}
	}
}
