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

		[Test]
		public void ClaimedAndUnassignedTruthRoundTripsWithoutOverlap()
		{
			var registry = new UnassignedCombatUnitRegistry();
			registry.Import(new uint[] { 7, 3, 9 }, new uint[] { 9, 11 });

			Assert.That(registry.ActorIds, Is.EqualTo(new uint[] { 3, 7 }));
			Assert.That(registry.ClaimedActorIds, Is.EqualTo(new uint[] { 9, 11 }));
			Assert.That(registry.Register(9), Is.False, "A claimed actor must not also become unassigned.");
			Assert.That(registry.Release(9), Is.True);
			Assert.That(registry.Register(9), Is.True);
			Assert.That(registry.Claim(9), Is.True);
			Assert.That(registry.ActorIds, Is.EqualTo(new uint[] { 3, 7 }));
			Assert.That(registry.IsClaimed(9), Is.True);
		}

		[Test]
		public void AuditStartsAreDeterministicallyStaggeredPerPlayer()
		{
			Assert.That(UnassignedCombatUnitRegistry.StaggeredAuditStartOffset(3000, 0, 4), Is.EqualTo(0));
			Assert.That(UnassignedCombatUnitRegistry.StaggeredAuditStartOffset(3000, 1, 4), Is.EqualTo(750));
			Assert.That(UnassignedCombatUnitRegistry.StaggeredAuditStartOffset(3000, 2, 4), Is.EqualTo(1500));
			Assert.That(UnassignedCombatUnitRegistry.StaggeredAuditStartOffset(3000, 3, 4), Is.EqualTo(2250));
		}

		[Test]
		public void AuditSlicesActorIdsWithinThePerTickBudget()
		{
			uint nextActorId = 1;
			Assert.That(UnassignedCombatUnitRegistry.NextAuditActorIds(ref nextActorId, 10, 4),
				Is.EqualTo(new uint[] { 1, 2, 3, 4 }));
			Assert.That(UnassignedCombatUnitRegistry.NextAuditActorIds(ref nextActorId, 10, 4),
				Is.EqualTo(new uint[] { 5, 6, 7, 8 }));
			Assert.That(UnassignedCombatUnitRegistry.NextAuditActorIds(ref nextActorId, 10, 4),
				Is.EqualTo(new uint[] { 9, 10 }));
			Assert.That(UnassignedCombatUnitRegistry.NextAuditActorIds(ref nextActorId, 10, 4), Is.Empty,
				"A completed audit must do no actor lookup work on ordinary bot ticks.");
		}

		[Test]
		public void ActorIdDigestIsStableAcrossInputOrder()
		{
			Assert.That(UnassignedCombatUnitRegistry.StableActorIdDigest(new uint[] { 9, 3, 7 }),
				Is.EqualTo(UnassignedCombatUnitRegistry.StableActorIdDigest(new uint[] { 7, 9, 3 })));
			Assert.That(UnassignedCombatUnitRegistry.StableActorIdDigest(new uint[] { 3, 7 }),
				Is.Not.EqualTo(UnassignedCombatUnitRegistry.StableActorIdDigest(new uint[] { 3, 7, 9 })));
		}

		[Test]
		public void DisabledAdvancedSquadPrefersPreCodexAssaultOverGenericFallback()
		{
			Assert.That(UnassignedCombatUnitRecruitmentPolicy.SelectFallback(false, true, true),
				Is.EqualTo(UnassignedCombatFallbackDisposition.PreCodexAssault));
		}

		[Test]
		public void MissingCompatibleOwnerUsesGenericFallbackOnlyForAllowlistedDirectCombat()
		{
			Assert.That(UnassignedCombatUnitRecruitmentPolicy.SelectFallback(true, false, true),
				Is.EqualTo(UnassignedCombatFallbackDisposition.GenericFallback));
			Assert.That(UnassignedCombatUnitRecruitmentPolicy.SelectFallback(true, false, false),
				Is.EqualTo(UnassignedCombatFallbackDisposition.Unclaimed),
				"Unsupported artillery and specialist roles must remain available for a safe owner instead of charging.");
		}
	}
}
