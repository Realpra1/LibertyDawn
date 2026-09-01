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

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class StealthTargetValueFilterBehaviorTest
	{
		sealed class CacheProbe : IStealthTargetAcquisitionCache
		{
			readonly StealthTargetAcquisitionCacheSnapshot snapshot;

			public CacheProbe(IEnumerable<CPos> cells,
				IEnumerable<StealthStrategicTargetSnapshot> targets)
			{
				snapshot = new StealthTargetAcquisitionCacheSnapshot(16, 16,
					Enumerable.Repeat(0f, 16 * 16), cells, 0.1f, targets);
			}

			public StealthTargetAcquisitionCacheSnapshot ReadSnapshot() { return snapshot; }
		}

		static CPos[] Cells()
		{
			return new[]
			{
				new CPos(1, 0), new CPos(0, 1), new CPos(2, 0), new CPos(1, 1), new CPos(0, 2),
				new CPos(3, 0), new CPos(2, 1), new CPos(1, 2), new CPos(0, 3), new CPos(4, 0)
			};
		}

		static StealthStrategicTargetSnapshot Target(int index, uint actorId,
			int priority, int value, int hp = 100, int maxHp = 100)
		{
			return new StealthStrategicTargetSnapshot(actorId, Cells()[index], priority, value, hp, maxHp);
		}

		static StealthTargetValueFilterHandoff Handoff(
			IEnumerable<StealthStrategicTargetSnapshot> targets,
			out StealthLifecycleController controller, CPos? incumbent = null,
			long acquisitionEpoch = 3, IEnumerable<CPos> cells = null)
		{
			controller = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.TargetAcquisition, new OwnershipEpoch(acquisitionEpoch), -1));
			var acquisition = new StealthTargetAcquisitionBehavior(controller.CurrentHandoff,
				new CacheProbe(cells ?? Cells(), targets));
			var result = acquisition.Execute(new CPos(0, 0), incumbent);
			Assert.That(result.IsReadyForValueFilter, Is.True);
			Assert.That(controller.TryAccept(result, out var handoff), Is.True);
			return handoff;
		}

		static List<StealthStrategicTargetSnapshot> TargetsWithHighCount(int highCount)
		{
			return Enumerable.Range(0, Cells().Length).Select(index => Target(index,
				(uint)(100 + index), index < highCount ? 5000 : 1,
				index < highCount ? 1100 : 100)).ToList();
		}

		[Test]
		public void UsesConfiguredPriorityValueAndRemainingHpWithoutActorExceptions()
		{
			var targets = TargetsWithHighCount(0);
			targets[0] = Target(0, 10, 5000, 1100);
			targets[1] = Target(1, 20, 5000, 1100, 25, 100);
			targets[2] = Target(2, 30, 5000, 1100);
			targets[3] = Target(3, 40, -1, int.MaxValue);

			var result = new StealthTargetValueFilterBehavior(
				Handoff(targets, out _)).Execute();

			Assert.That(result.Options.Select(option => option.StableIdentity), Is.EqualTo(new uint[] { 20, 10 }));
			Assert.That(result.Options.Select(option => option.StrategicValue),
				Is.EqualTo(new[] { 22000000L, 5500000L }));
			Assert.That(result.Options.Any(option => option.StableIdentity == 40), Is.False,
				"Configured negative priority must score zero without an actor/type exception.");
		}

		[TestCase(1, 1)]
		[TestCase(2, 1)]
		[TestCase(3, 2)]
		[TestCase(5, 3)]
		public void RetainsExactCeilingHalfOfActiveTier(int highCount, int expected)
		{
			var result = new StealthTargetValueFilterBehavior(
				Handoff(TargetsWithHighCount(highCount), out _)).Execute();

			Assert.That(result.Options.Count, Is.EqualTo(expected));
		}

		[Test]
		public void DeterministicValueTiesUseStableIdentityThenCell()
		{
			var targets = Enumerable.Range(0, Cells().Length)
				.Select(index => Target(index, (uint)(200 - index), 5000, 1100)).ToArray();
			var first = new StealthTargetValueFilterBehavior(
				Handoff(targets, out _)).Execute();
			var second = new StealthTargetValueFilterBehavior(Handoff(
				targets.Reverse(), out _, cells: Cells().Reverse())).Execute();

			Assert.That(first.Options.Select(option => option.StableIdentity),
				Is.EqualTo(new uint[] { 191, 192, 193, 194, 195 }));
			Assert.That(second.Options.Select(option => option.StrategicCell),
				Is.EqualTo(first.Options.Select(option => option.StrategicCell)));
		}

		[Test]
		public void StrategicFloorUsesHighTierFirstAndBelowFloorOnlyAsFallback()
		{
			var highTier = TargetsWithHighCount(2);
			var preferred = new StealthTargetValueFilterBehavior(
				Handoff(highTier, out _)).Execute();
			Assert.That(preferred.Options.Count, Is.EqualTo(1));
			Assert.That(preferred.Options[0].StableIdentity, Is.EqualTo(100));

			var fallbackTargets = Enumerable.Range(0, Cells().Length)
				.Select(index => Target(index, (uint)(300 + index), 1, 1000 - index)).ToArray();
			var fallback = new StealthTargetValueFilterBehavior(
				Handoff(fallbackTargets, out _)).Execute();
			Assert.That(fallback.Options.Count, Is.EqualTo(5));
			Assert.That(fallback.Options.Select(option => option.StableIdentity),
				Is.EqualTo(new uint[] { 300, 301, 302, 303, 304 }));
		}

		[Test]
		public void IncumbentIsScoredByTheSamePolicyAndCanBeRetained()
		{
			var targets = TargetsWithHighCount(0);
			targets[9] = Target(9, 999, 5000, 1100, 25, 100);
			var incumbent = Cells()[9];
			var handoff = Handoff(targets, out _, incumbent);

			Assert.That(handoff.Options[0].StrategicCell, Is.EqualTo(incumbent));
			Assert.That(handoff.Options[0].IsIncumbent, Is.True);
			var result = new StealthTargetValueFilterBehavior(handoff).Execute();
			Assert.That(result.Options.Single().StableIdentity, Is.EqualTo(999));
			Assert.That(result.Options.Single().IsIncumbent, Is.True);
			Assert.That(result.Options.Single().StrategicValue, Is.EqualTo(22000000L));
		}

		[Test]
		public void ObservationsCannotStealControlAndOnlyTypedResultHandsOffImmutableOptions()
		{
			var handoff = Handoff(TargetsWithHighCount(3), out var controller);
			var behavior = new StealthTargetValueFilterBehavior(handoff);
			controller.Observe(new StealthLifecycleObservationFrame(10, new[]
			{
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Timer),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.WorldEvent),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Damage, 7)
			}));

			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.TargetValueFilter));
			Assert.That(controller.Epoch, Is.EqualTo(new OwnershipEpoch(4)));
			var result = behavior.Execute();
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.TargetValueFilter));
			Assert.That(controller.TryAccept(result, out var threatHandoff), Is.True);
			Assert.That(threatHandoff.Owner, Is.EqualTo(BehaviorId.TargetThreatFilter));
			Assert.That(threatHandoff.Epoch, Is.EqualTo(new OwnershipEpoch(5)));
			Assert.Throws<NotSupportedException>(() =>
				((IList<StealthTargetValueOption>)threatHandoff.Options).Add(threatHandoff.Options[0]));
			Assert.Throws<NotSupportedException>(() =>
				((IList<StealthStrategicTargetSnapshot>)threatHandoff.Options[0].StrategicTargets)
					.Add(threatHandoff.Options[0].StrategicTargets[0]));
		}

		[Test]
		public void PrivateStateRoundTripsAndRejectsStaleOrWrongOwnerEpochs()
		{
			var targets = TargetsWithHighCount(5);
			var behavior = new StealthTargetValueFilterBehavior(Handoff(targets, out _));
			var serialized = new List<MiniYamlNode> { behavior.SerializePrivateState(behavior.Execute()) }
				.WriteToString();
			var restored = behavior.RestorePrivateState(MiniYaml.FromString(serialized).Single());

			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState(restored) }.WriteToString(),
				Is.EqualTo(serialized));
			Assert.That(restored.Options.Select(option => option.StableIdentity),
				Is.EqualTo(new uint[] { 100, 101, 102 }));

			var stale = new StealthTargetValueFilterBehavior(
				Handoff(targets, out _, acquisitionEpoch: 4));
			Assert.Throws<InvalidOperationException>(() =>
				stale.RestorePrivateState(MiniYaml.FromString(serialized).Single()));
			var wrongOwner = serialized.Replace("Owner: TargetValueFilter", "Owner: TargetAcquisition");
			Assert.That(wrongOwner, Is.Not.EqualTo(serialized));
			Assert.Throws<InvalidOperationException>(() =>
				behavior.RestorePrivateState(MiniYaml.FromString(wrongOwner).Single()));
		}
	}
}
