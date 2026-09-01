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
	public sealed class StealthTargetAcquisitionBehaviorTest
	{
		sealed class CacheProbe : IStealthTargetAcquisitionCache
		{
			readonly StealthTargetAcquisitionCacheSnapshot snapshot;

			public int Reads { get; private set; }

			public CacheProbe(StealthTargetAcquisitionCacheSnapshot snapshot)
			{
				this.snapshot = snapshot;
			}

			public StealthTargetAcquisitionCacheSnapshot ReadSnapshot()
			{
				Reads++;
				return snapshot;
			}
		}

		sealed class SafetyProbe : IStealthSquadConstructionSafetyService
		{
			public bool TryFindSafeRoute(uint actorId, CPos originStrategicCell,
				CPos destinationStrategicCell, out IReadOnlyList<CPos> routeStrategicCells)
			{
				routeStrategicCells = null;
				return false;
			}
		}

		static StealthLifecycleController Controller(long epoch = 3)
		{
			return StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.TargetAcquisition, new OwnershipEpoch(epoch), -1));
		}

		static CacheProbe Cache(int width, int height, float secondsPerCostUnit,
			params CPos[] enemies)
		{
			return new CacheProbe(new StealthTargetAcquisitionCacheSnapshot(width, height,
				Enumerable.Repeat(0f, width * height), enemies, secondsPerCostUnit));
		}

		static CPos[] TenNearbyEnemies()
		{
			return new[]
			{
				new CPos(1, 0), new CPos(0, 1), new CPos(2, 0), new CPos(1, 1), new CPos(0, 2),
				new CPos(3, 0), new CPos(2, 1), new CPos(1, 2), new CPos(0, 3), new CPos(4, 0)
			};
		}

		[Test]
		public void ConstructionIsTheOnlyImplementedTypedEntryToTargetAcquisition()
		{
			var controller = new StealthLifecycleController();
			var start = new StealthStartBehavior(controller.CurrentHandoff);
			var startResult = start.Execute(new StealthLifecycleObservation(
				StealthLifecycleObservationKind.UnitBuilt, 7),
				new[] { new StealthStartMemberSnapshot(7) });
			Assert.That(controller.TryAccept(startResult, out var constructionHandoff), Is.True);
			var construction = new StealthSquadConstructionBehavior(constructionHandoff,
				new uint[] { 7 }, new SafetyProbe());
			var constructionResult = construction.Execute(new[]
			{
				new StealthSquadConstructionMemberSnapshot(7, new CPos(0, 0))
			}, Array.Empty<StealthSquadConstructionSquadSnapshot>());

			Assert.That(controller.TryAccept(constructionResult, out var targetHandoff), Is.True);
			Assert.That(targetHandoff.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
			Assert.Throws<ArgumentException>(() => new StealthTargetAcquisitionBehavior(
				new StealthLifecycleController().CurrentHandoff, Cache(8, 8, 1, TenNearbyEnemies())));
			Assert.That(typeof(StealthTargetAcquisitionResult).GetConstructors(), Is.Empty);
		}

		[Test]
		public void CapsDeduplicatesAndAlwaysRetainsIncumbentFirst()
		{
			var controller = Controller();
			var enemies = TenNearbyEnemies().Concat(new[]
			{
				new CPos(1, 0), new CPos(5, 0), new CPos(15, 15), new CPos(6, 0)
			}).Reverse().ToArray();
			var cache = Cache(20, 20, 1, enemies);
			var result = new StealthTargetAcquisitionBehavior(
				controller.CurrentHandoff, cache).Execute(new CPos(0, 0), new CPos(15, 15));

			Assert.That(result.Options.Count, Is.EqualTo(StealthTargetAcquisitionBehavior.MaximumOptions));
			Assert.That(result.Options.Select(option => option.StrategicCell).Distinct().Count(),
				Is.EqualTo(result.Options.Count));
			Assert.That(result.Options[0].StrategicCell, Is.EqualTo(new CPos(15, 15)));
			Assert.That(result.Options[0].IsIncumbent, Is.True);
			Assert.That(result.Options.Count(option => option.IsIncumbent), Is.EqualTo(1));
			Assert.That(cache.Reads, Is.EqualTo(1));
		}

		[Test]
		public void DeterministicOrderIgnoresCacheInputOrderAndDuplicateCells()
		{
			var firstController = Controller();
			var secondController = Controller();
			var enemies = TenNearbyEnemies();
			var first = new StealthTargetAcquisitionBehavior(firstController.CurrentHandoff,
				Cache(8, 8, 1, enemies.Concat(new[] { enemies[0] }).ToArray()))
				.Execute(new CPos(0, 0));
			var second = new StealthTargetAcquisitionBehavior(secondController.CurrentHandoff,
				Cache(8, 8, 1, enemies.Reverse().Concat(new[] { enemies[4] }).ToArray()))
				.Execute(new CPos(0, 0));

			Assert.That(second.Options.Select(option => option.StrategicCell),
				Is.EqualTo(first.Options.Select(option => option.StrategicCell)));
			Assert.That(second.Options.Select(option => option.EstimatedTravelMilliseconds),
				Is.EqualTo(first.Options.Select(option => option.EstimatedTravelMilliseconds)));
		}

		[Test]
		public void ThirtySecondBoundProducesBoundedMoveCloserAndRescan()
		{
			var controller = Controller();
			var result = new StealthTargetAcquisitionBehavior(controller.CurrentHandoff,
				Cache(80, 1, 1, new CPos(60, 0))).Execute(new CPos(0, 0));

			Assert.That(result.Options, Is.Empty);
			Assert.That(result.ExpandedCells, Is.EqualTo(31),
				"The cache frontier must stop at the inclusive 30-second route-cost horizon.");
			Assert.That(result.ExpandedCells, Is.LessThan(80),
				"A far-edge target must not force expansion across the whole cached row.");
			Assert.That(result.Disposition,
				Is.EqualTo(StealthTargetAcquisitionDisposition.MoveCloserAndRescan));
			Assert.That(result.MoveCloserStrategicCell, Is.EqualTo(new CPos(4, 0)));
			Assert.That(result.MoveCloserStrategicCell.Value.X,
				Is.LessThanOrEqualTo(StealthTargetAcquisitionBehavior.MaximumFallbackSteps));
			Assert.That(controller.TryAccept(result, out var handoff), Is.False);
			Assert.That(handoff, Is.Null);
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
		}

		[Test]
		public void SearchWorkIsCpuBoundedWithoutAWholeMapExpansion()
		{
			var controller = Controller();
			var result = new StealthTargetAcquisitionBehavior(controller.CurrentHandoff,
				Cache(300, 300, 0.001f, new CPos(299, 299))).Execute(new CPos(0, 0));

			Assert.That(result.PrimitiveOperations,
				Is.EqualTo(StealthTargetAcquisitionBehavior.MaximumPrimitiveOperations));
			Assert.That(result.ExpandedCells, Is.LessThan(300 * 300));
			Assert.That(result.MoveCloserStrategicCell, Is.EqualTo(new CPos(2, 2)));
			Assert.That(result.Disposition,
				Is.EqualTo(StealthTargetAcquisitionDisposition.MoveCloserAndRescan));
		}

		[Test]
		public void ObservationsCannotStealOwnershipOrInvokeTheCache()
		{
			var controller = Controller();
			var cache = Cache(8, 8, 1, TenNearbyEnemies());
			var acquisition = new StealthTargetAcquisitionBehavior(controller.CurrentHandoff, cache);

			controller.Observe(new StealthLifecycleObservationFrame(10, new[]
			{
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Timer),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.WorldEvent),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Damage, 7)
			}));

			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
			Assert.That(controller.Epoch, Is.EqualTo(new OwnershipEpoch(3)));
			Assert.That(cache.Reads, Is.Zero);
			acquisition.Execute(new CPos(0, 0));
			Assert.That(cache.Reads, Is.EqualTo(1));
		}

		[Test]
		public void ReadyResultHandsImmutableOptionsToTypedValueFilterBoundary()
		{
			var controller = Controller();
			var result = new StealthTargetAcquisitionBehavior(controller.CurrentHandoff,
				Cache(8, 8, 1, TenNearbyEnemies())).Execute(new CPos(0, 0));

			Assert.That(result.IsReadyForValueFilter, Is.True);
			Assert.That(controller.TryAccept(result, out var valueFilter), Is.True);
			Assert.That(valueFilter.Owner, Is.EqualTo(BehaviorId.TargetValueFilter));
			Assert.That(valueFilter.Epoch, Is.EqualTo(new OwnershipEpoch(4)));
			Assert.That(valueFilter.Options.Select(option => option.StrategicCell),
				Is.EqualTo(result.Options.Select(option => option.StrategicCell)));
			Assert.Throws<NotSupportedException>(() =>
				((IList<StealthTargetOption>)valueFilter.Options).Add(valueFilter.Options[0]));
		}

		[Test]
		public void PrivateStateRoundTripsAndRejectsStaleOrWrongOwnerEpochs()
		{
			var controller = Controller();
			var acquisition = new StealthTargetAcquisitionBehavior(controller.CurrentHandoff,
				Cache(8, 8, 1, TenNearbyEnemies()));
			var result = acquisition.Execute(new CPos(0, 0), new CPos(4, 0));
			var serialized = new List<MiniYamlNode> { acquisition.SerializePrivateState(result) }
				.WriteToString();
			var restored = acquisition.RestorePrivateState(MiniYaml.FromString(serialized).Single());

			Assert.That(new List<MiniYamlNode> { acquisition.SerializePrivateState(restored) }
				.WriteToString(), Is.EqualTo(serialized));
			Assert.That(restored.Options.Select(option => option.StrategicCell),
				Is.EqualTo(result.Options.Select(option => option.StrategicCell)));

			var stale = new StealthTargetAcquisitionBehavior(Controller(4).CurrentHandoff,
				Cache(8, 8, 1, TenNearbyEnemies()));
			Assert.Throws<InvalidOperationException>(() =>
				stale.RestorePrivateState(MiniYaml.FromString(serialized).Single()));
			var wrongOwner = serialized.Replace("Owner: TargetAcquisition", "Owner: Start");
			Assert.That(wrongOwner, Is.Not.EqualTo(serialized));
			Assert.Throws<InvalidOperationException>(() =>
				acquisition.RestorePrivateState(MiniYaml.FromString(wrongOwner).Single()));
		}
	}
}
