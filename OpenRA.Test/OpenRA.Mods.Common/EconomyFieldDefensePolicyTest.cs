#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the GNU General Public License version 3 or later.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class EconomyFieldDefensePolicyTest
	{
		[Test]
		public void AttackCallbackRejectsIncompleteOrHarmlessPayloads()
		{
			Assert.That(EconomyFieldDefensePolicy.HasActionableAttack(null), Is.False);
			Assert.That(EconomyFieldDefensePolicy.HasActionableAttack(new AttackInfo()), Is.False);
			Assert.That(EconomyFieldDefensePolicy.HasActionableAttack(new AttackInfo
			{
				Damage = new Damage(1)
			}), Is.False);
			Assert.That(EconomyFieldDefensePolicy.HasActionableAttack(new AttackInfo
			{
				Damage = new Damage(0),
				Attacker = null
			}), Is.False);
		}

		[TestCase(20, 0, 0, 0, 20)]
		[TestCase(20, 11, 3, 2, 4)]
		[TestCase(5, 5, 2, 1, 0)]
		[TestCase(2, 3, 0, 0, 0)]
		public void ProductionDemandRequestsOnlyTheExactResidual(int target, int assigned,
			int queued, int owned, int expected)
		{
			Assert.That(EconomyFieldDefensePolicy.MissingProductionDemand(target, assigned, queued, owned),
				Is.EqualTo(expected));
		}

		[TestCase(10, 40, 250)]
		[TestCase(10, 35, 286)]
		[TestCase(10, 30, 334)]
		[TestCase(10, 20, 500)]
		public void ReinforcementIntervalUsesTenGameSecondsWithCeiling(int seconds, int timestep, int expected)
		{
			var ticks = EconomyFieldDefensePolicy.ReinforcementIntervalTicks(seconds, timestep);
			Assert.That(ticks, Is.EqualTo(expected));
			Assert.That(ticks * timestep, Is.GreaterThanOrEqualTo(seconds * 1000));
		}

		[Test]
		public void SamCoverageReusesSitesAndPrioritizesEconomyAnchorsDeterministically()
		{
			var anchors = new[]
			{
				new EconomyDefenseSamAnchor(30, 2, new CPos(30, 30)),
				new EconomyDefenseSamAnchor(20, 1, new CPos(20, 20)),
				new EconomyDefenseSamAnchor(11, 0, new CPos(12, 10)),
				new EconomyDefenseSamAnchor(10, 0, new CPos(10, 10))
			};
			var coverage = new[] { new EconomyDefenseSamCoverage(new CPos(11, 10), 3) };

			Assert.That(EconomyFieldDefensePolicy.FirstUncoveredSamAnchor(anchors, coverage)?.ActorId,
				Is.EqualTo(20));
		}

		[TestCase(true, true, 1, 0, 4, true, true)]
		[TestCase(true, false, 1, 0, 4, true, false)]
		[TestCase(true, true, 4, 0, 4, true, false)]
		[TestCase(true, true, 1, 1, 4, true, false)]
		[TestCase(false, true, 1, 0, 4, true, false)]
		public void SamDemandRequiresPowerCoverageNeedAndBoundedCapacity(bool enabled, bool power,
			int live, int pending, int maximum, bool uncovered, bool expected)
		{
			Assert.That(EconomyFieldDefensePolicy.ShouldRequestSam(enabled, power, live, pending,
				maximum, uncovered), Is.EqualTo(expected));
		}

		[Test]
		public void EconomySamPlacementOwnershipFollowsTheExactQueueAndBuild()
		{
			var economyQueue = new object();
			var ordinaryQueue = new object();
			var ownership = new EconomyDefenseSamBuildOwnership<object>();

			Assert.That(ownership.TryReserve(economyQueue, "sam", 10), Is.True);
			Assert.That(ownership.Owns(economyQueue, "sam"), Is.True);
			Assert.That(ownership.Owns(ordinaryQueue, "sam"), Is.False);
			ownership.Refresh(100, 5, _ => true, (_, __) => false);
			Assert.That(ownership.HasReservation, Is.False);
		}

		[Test]
		public void EconomySamPlacementOwnershipRestoresOnlyToTheMatchingQueuedBuild()
		{
			var queue = new object();
			var ownership = new EconomyDefenseSamBuildOwnership<object>();
			Assert.That(ownership.TryRestore(queue, "sam", 1200, q => ReferenceEquals(q, queue),
				(q, type) => ReferenceEquals(q, queue) && type == "sam"), Is.True);
			Assert.That(ownership.Owns(queue, "sam"), Is.True);
		}
	}
}
