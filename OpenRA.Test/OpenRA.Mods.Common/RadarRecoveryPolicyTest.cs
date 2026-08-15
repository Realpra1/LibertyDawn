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
	public class RadarRecoveryPolicyTest
	{
		[Test]
		public void RecoveryStartsOnlyAfterAnEstablishedProviderIsLost()
		{
			Assert.That(RadarRecoveryPolicy.NeedsRecovery(true, false, false, false), Is.False,
				"Initial radar construction remains owned by authored policy.");
			Assert.That(RadarRecoveryPolicy.NeedsRecovery(true, true, false, false), Is.True);
		}

		[Test]
		public void ProviderAddedAndLostBetweenScansStillStartsRecovery()
		{
			var everEstablished = RadarRecoveryPolicy.RecordProviderEstablishment(false, false);
			Assert.That(everEstablished, Is.False, "An unrelated actor must not establish radar.");

			everEstablished = RadarRecoveryPolicy.RecordProviderEstablishment(everEstablished, true);
			Assert.That(RadarRecoveryPolicy.NeedsRecovery(true, everEstablished, false, false), Is.True,
				"The provider lifecycle event must survive a loss before the next provider scan.");
		}

		[Test]
		public void LiveProviderOrGlobalCommitmentSuppressesDuplicates()
		{
			Assert.That(RadarRecoveryPolicy.NeedsRecovery(true, true, true, false), Is.False,
				"A live provider remains present even when its conditional radar trait is disabled by low power.");
			Assert.That(RadarRecoveryPolicy.NeedsRecovery(true, true, false, true), Is.False,
				"A reservation, queued item, or pending placement globally owns the obligation.");
		}

		[Test]
		public void RecoveryCanBeDisabledByOmittingConfiguration()
		{
			Assert.That(RadarRecoveryPolicy.NeedsRecovery(false, true, false, false), Is.False);
		}

		[Test]
		public void ReservationTimeoutIsPositiveAndDeterministic()
		{
			Assert.That(RadarRecoveryPolicy.ReservationExpired(100, 349, 250), Is.False);
			Assert.That(RadarRecoveryPolicy.ReservationExpired(100, 350, 250), Is.True);
			Assert.That(RadarRecoveryPolicy.ReservationExpired(100, 101, 0), Is.True);
		}

		[Test]
		public void ReservationOwnershipIncludesTheExactQueueType()
		{
			Assert.That(RadarRecoveryPolicy.ReservationMatchesQueue(7, "Building", 7, "Building"), Is.True);
			Assert.That(RadarRecoveryPolicy.ReservationMatchesQueue(7, "Building", 7, "Defence.GDI"), Is.False,
				"A different queue on the same actor must not inherit the radar reservation.");
			Assert.That(RadarRecoveryPolicy.ReservationMatchesQueue(7, "Building", 8, "Building"), Is.False,
				"A replacement Construction Yard must explicitly retry the obligation.");
		}

		[Test]
		public void ActiveReservationReleasesOnExactQueueLossOrCommitmentDisappearance()
		{
			Assert.That(RadarRecoveryPolicy.ReservationMustRelease(true, false, true, true), Is.False,
				"An active production commitment retains its exact queue identity beyond the reservation timeout.");
			Assert.That(RadarRecoveryPolicy.ReservationMustRelease(false, true, false, false), Is.True,
				"Capturing the reserved queue releases ownership immediately.");
			Assert.That(RadarRecoveryPolicy.ReservationMustRelease(true, true, false, false), Is.True,
				"Cancellation releases a commitment without waiting for the reservation timeout.");
			Assert.That(RadarRecoveryPolicy.ReservationMustRelease(true, false, false, true), Is.True);
		}

		[Test]
		public void ActiveCommitmentObservationSurvivesSaveLoadAndCanBeReconstructed()
		{
			Assert.That(RadarRecoveryPolicy.RestoreCommitmentObservation(true, false), Is.True,
				"A loaded active-production marker must survive until the exact queue is validated.");
			Assert.That(RadarRecoveryPolicy.RestoreCommitmentObservation(false, true), Is.True,
				"Older saves safely reconstruct the phase from a still-visible commitment.");
			Assert.That(RadarRecoveryPolicy.RestoreCommitmentObservation(false, false), Is.False,
				"A pre-production reservation must not be promoted to an active commitment.");
		}

		[Test]
		public void StoragePressurePreservesTheLegacyStrictThreshold()
		{
			Assert.That(RadarRecoveryPolicy.HasActionableStoragePressure(800, 1000), Is.False);
			Assert.That(RadarRecoveryPolicy.HasActionableStoragePressure(801, 1000), Is.True);
			Assert.That(RadarRecoveryPolicy.HasActionableStoragePressure(100, 0), Is.False);
		}

		[Test]
		public void RadarWaitsForTheFirstIrreversibleStorageCommitment()
		{
			Assert.That(RadarRecoveryPolicy.StorageCommitmentBlocksRadar(true, false, false), Is.True,
				"A reservation on another idle queue must stop radar from spending first.");
			Assert.That(RadarRecoveryPolicy.StorageCommitmentBlocksRadar(true, false, true), Is.False,
				"The silo StartProduction order already precedes radar in the same deterministic order batch.");
			Assert.That(RadarRecoveryPolicy.StorageCommitmentBlocksRadar(true, true, false), Is.False,
				"A visible queued silo is already an irreversible commitment.");
		}

		[Test]
		public void QueueChoicesRefreshProviderStateAtMostOncePerTick()
		{
			Assert.That(RadarRecoveryPolicy.ShouldRefreshObservation(false, -1, 25, 0, false), Is.True);
			Assert.That(RadarRecoveryPolicy.ShouldRefreshObservation(true, 100, 125, 101, false), Is.False,
				"Periodic observation remains cached between scan intervals.");
			Assert.That(RadarRecoveryPolicy.ShouldRefreshObservation(true, 100, 125, 101, true), Is.True,
				"A queue choice must see a provider acquired after the periodic scan.");
			Assert.That(RadarRecoveryPolicy.ShouldRefreshObservation(true, 101, 126, 101, true), Is.False,
				"Multiple Facts choosing in the same tick share one fresh observation.");
		}

		[TestCase(false, false, false, false, false, false, RadarRecoveryPolicy.ProviderTransition.None)]
		[TestCase(false, false, false, false, true, false, RadarRecoveryPolicy.ProviderTransition.Established)]
		[TestCase(true, true, true, true, false, false, RadarRecoveryPolicy.ProviderTransition.Lost)]
		[TestCase(true, true, false, false, true, false, RadarRecoveryPolicy.ProviderTransition.Restored)]
		[TestCase(true, true, true, false, true, true, RadarRecoveryPolicy.ProviderTransition.BecameOperational)]
		[TestCase(true, true, true, true, true, false, RadarRecoveryPolicy.ProviderTransition.BecameUnavailable)]
		public void ProviderLifecycleTransitionsAreExplicit(bool initialized, bool everEstablished,
			bool wasLive, bool wasOperational, bool isLive, bool isOperational,
			RadarRecoveryPolicy.ProviderTransition expected)
		{
			Assert.That(RadarRecoveryPolicy.ObserveProviderTransition(initialized, everEstablished,
				wasLive, wasOperational, isLive, isOperational), Is.EqualTo(expected));
		}
	}
}
