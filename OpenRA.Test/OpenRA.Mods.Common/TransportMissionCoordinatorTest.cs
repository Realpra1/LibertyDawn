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

		[Test]
		public void InvalidTransportOrPassengerCanBeReleasedWithoutDroppingSurvivingPairs()
		{
			var coordinator = new TransportMissionCoordinator(1);
			var mission = coordinator.TryReserve(new uint[] { 10, 11, 20, 21, 30, 31 });

			coordinator.ReleaseActors(mission, new uint[] { 10, 11 });
			Assert.That(coordinator.IsReserved(10), Is.False, "Destroyed carrier reservation must be released.");
			Assert.That(coordinator.IsReserved(11), Is.False, "Its surviving passenger must return to normal AI ownership.");
			Assert.That(coordinator.IsReserved(20), Is.True);
			Assert.That(coordinator.IsReserved(21), Is.True);

			coordinator.ReleaseActors(mission, new uint[] { 30, 31 });
			Assert.That(coordinator.IsReserved(30), Is.False, "Mirrored passenger invalidation must release the pair.");
			Assert.That(coordinator.IsReserved(31), Is.False);
			Assert.That(coordinator.IsReserved(20), Is.True, "A valid surviving pair must keep its mission ownership.");
			Assert.That(coordinator.MissionCount, Is.EqualTo(1));
		}

		[Test]
		public void CarrierAndExitClaimsReplaceAtomicallyAndReleaseWithMission()
		{
			var coordinator = new TransportMissionCoordinator(2);
			var first = coordinator.TryReserve(new uint[] { 1, 2 });
			var second = coordinator.TryReserve(new uint[] { 3, 4 });
			var firstCells = new[] { new CPos(4, 4), new CPos(4, 5) };
			var replacement = new[] { new CPos(6, 6), new CPos(6, 7) };

			Assert.That(coordinator.TryClaimCells(first, firstCells, out var conflict), Is.True);
			Assert.That(conflict, Is.Zero);
			Assert.That(coordinator.TryClaimCells(second, new[] { new CPos(4, 5), new CPos(5, 5) }, out conflict), Is.False);
			Assert.That(conflict, Is.EqualTo(first));
			Assert.That(coordinator.ClaimOwner(new CPos(4, 5)), Is.EqualTo(first));

			Assert.That(coordinator.TryClaimCells(first, replacement, out conflict), Is.True);
			Assert.That(coordinator.ClaimOwner(new CPos(4, 4)), Is.Zero);
			Assert.That(coordinator.ClaimOwner(new CPos(6, 7)), Is.EqualTo(first));

			coordinator.Release(first);
			Assert.That(coordinator.ClaimOwner(new CPos(6, 7)), Is.Zero);
		}

		[Test]
		public void LoadedRecoveryDeadlineIsNonRenewableAndTerminalOwnershipIsParked()
		{
			var recovery = new TransportRescueRecoveryLifecycle();
			var coordinator = new TransportMissionCoordinator(1);
			var mission = coordinator.TryReserve(new uint[] { 10, 20 });
			var staleCells = new[] { new CPos(4, 4), new CPos(4, 5) };

			Assert.That(recovery.TryBeginReturn(1000, 3000), Is.True);
			Assert.That(recovery.DeadlineTick, Is.EqualTo(4000));
			Assert.That(recovery.TryBeginReturn(2500, 3000), Is.False);
			Assert.That(recovery.DeadlineTick, Is.EqualTo(4000));
			Assert.That(coordinator.TryClaimCells(mission, staleCells, out _), Is.True);
			Assert.That(recovery.TryEnterTerminal(3999), Is.False);
			Assert.That(recovery.TryEnterTerminal(4000), Is.True);
			Assert.That(recovery.TryEnterTerminal(7000), Is.False);

			Assert.That(coordinator.ParkLoadedMission(mission), Is.True);
			Assert.That(coordinator.ParkLoadedMission(mission), Is.False);
			Assert.That(coordinator.MissionCount, Is.Zero);
			Assert.That(coordinator.ClaimOwner(staleCells[0]), Is.Zero);
			Assert.That(coordinator.IsReserved(10), Is.True);
			Assert.That(coordinator.IsReserved(20), Is.True);
			Assert.That(coordinator.TryReserve(new uint[] { 10, 30 }), Is.Zero);

			// A safe site that opens later may be claimed deterministically by the parked owner.
			var openedCells = new[] { new CPos(8, 8), new CPos(8, 9) };
			Assert.That(coordinator.TryClaimCells(mission, openedCells, out _), Is.True);
			coordinator.Release(mission);
			Assert.That(coordinator.ClaimOwner(openedCells[0]), Is.Zero);
			Assert.That(coordinator.IsReserved(10), Is.False);
			Assert.That(coordinator.IsReserved(20), Is.False);
		}

		[Test]
		public void TimedOutObjectiveIsConsumedOnceByItsExactPassengerAndTarget()
		{
			var timeouts = new TransportObjectiveTimeoutLedger();
			timeouts.Record(10, 20);

			Assert.That(timeouts.TryConsume(10, 21), Is.False, "An unrelated objective must not release a capture claim.");
			Assert.That(timeouts.TryConsume(11, 20), Is.False, "An unrelated specialist must not consume the timeout.");
			Assert.That(timeouts.TryConsume(10, 20), Is.True);
			Assert.That(timeouts.TryConsume(10, 20), Is.False, "The timeout only defers one retry window.");

			timeouts.Record(10, 20);
			timeouts.Clear(10);
			Assert.That(timeouts.TryConsume(10, 20), Is.False, "A fresh transport request clears obsolete timeout state.");
		}
	}
}
