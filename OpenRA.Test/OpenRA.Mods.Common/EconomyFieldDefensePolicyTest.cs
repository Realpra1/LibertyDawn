#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class EconomyFieldDefensePolicyTest
	{
		[Test]
		public void ExactRouteEncodingPreservesEveryAdjacentSafeCell()
		{
			// Pathfinders return target-to-source order. The bend represents the safe route
			// around a hazardous direct shortcut and must survive order serialization intact.
			var expected = new[]
			{
				new CPos(5, 1), new CPos(4, 1), new CPos(3, 1),
				new CPos(3, 2), new CPos(2, 2), new CPos(1, 2), new CPos(1, 1)
			};

			var encoded = MoveAlongPath.EncodePath(expected);
			Assert.That(MoveAlongPath.TryDecodePath(encoded, 16, out var actual), Is.True);
			Assert.That(actual, Is.EqualTo(expected));
			Assert.That(MoveAlongPath.TryDecodePath(
				MoveAlongPath.EncodePath(new[] { new CPos(5, 1), new CPos(1, 1) }), 16, out _), Is.False,
				"A direct multi-cell fallback must not be accepted as an exact safe route segment.");
			Assert.That(MoveAlongPath.TryDecodePath(encoded, expected.Length - 1, out _), Is.False,
				"Routes beyond the configured bound must be withheld instead of growing the order queue.");
			Assert.That(MoveAlongPath.TryDecodePath("not-a-cell;1", 16, out _), Is.False);
			Assert.That(MoveAlongPath.TryDecodePath(
				MoveAlongPath.EncodePath(new[] { new CPos(1, 1), new CPos(1, 1) }), 16, out _), Is.False,
				"A repeated cell must not create a zero-length movement segment.");
		}

		[Test]
		public void MovementSafetySnapshotEncodingIsDeterministicAndBounded()
		{
			var expected = new[] { new CPos(1, 1), new CPos(2, 1), new CPos(3, 1) };
			var encoded = MoveAlongPath.EncodeSafetyCells(new[]
			{
				expected[2], expected[0], expected[1], expected[0]
			});

			Assert.That(MoveAlongPath.TryDecodeSafetyCells(encoded, expected.Length, out var actual), Is.True);
			Assert.That(actual, Is.EqualTo(expected));
			Assert.That(MoveAlongPath.TryDecodeSafetyCells(encoded, expected.Length - 1, out _), Is.False,
				"Safety ownership must fail closed instead of emitting an unbounded synchronized order.");
			Assert.That(MoveAlongPath.TryDecodeSafetyCells("2;1", expected.Length, out _), Is.False,
				"Non-canonical snapshots must not reintroduce collection-order differences.");
			Assert.That(MoveAlongPath.TryDecodeSafetyCells("", expected.Length, out actual), Is.True);
			Assert.That(actual, Is.Empty);
		}

		[Test]
		public void SavedDestinationCellsRoundTripThroughIntegerBits()
		{
			var expected = new[] { new CPos(-3, 7, 1), new CPos(20, 30) };
			var serialized = FieldSaver.FormatValue(new[] { expected[0].Bits, expected[1].Bits });
			var bits = FieldLoader.GetValue<int[]>("DestinationCells", serialized);

			Assert.That(new[] { new CPos(bits[0]), new CPos(bits[1]) }, Is.EqualTo(expected),
				"CPos arrays cannot use generic comma-delimited save formatting because each cell also contains commas.");
		}

		[Test]
		public void SavedRouteStateRoundTripsThroughNestedScalars()
		{
			var serialized = new List<MiniYamlNode>
			{
				new MiniYamlNode("EconomyFieldDefenseRoutes", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("Route", "", new List<MiniYamlNode>
					{
						new MiniYamlNode("Actor", FieldSaver.FormatValue(42u)),
						new MiniYamlNode("LastOrder", FieldSaver.FormatValue(3101)),
						new MiniYamlNode("BestDistance", FieldSaver.FormatValue(9876543210L)),
						new MiniYamlNode("LastProgress", FieldSaver.FormatValue(3126)),
						new MiniYamlNode("EnRoute", FieldSaver.FormatValue(true))
					})
				})
			}.WriteToString();
			var route = MiniYaml.FromString(serialized).Single().Value.Nodes.Single();
			T Load<T>(string key)
			{
				var node = route.Value.Nodes.Single(n => n.Key == key);
				return FieldLoader.GetValue<T>(key, node.Value.Value);
			}

			Assert.That(Load<uint>("Actor"), Is.EqualTo(42u));
			Assert.That(Load<int>("LastOrder"), Is.EqualTo(3101));
			Assert.That(Load<long>("BestDistance"), Is.EqualTo(9876543210L));
			Assert.That(Load<int>("LastProgress"), Is.EqualTo(3126));
			Assert.That(Load<bool>("EnRoute"), Is.True);
		}

		[Test]
		public void ActualHarvestRemainsPendingUntilSuccessfulEmptyUnload()
		{
			var oldStation = new CPos(5, 5);
			var state = new HarvesterFieldContextState(false, CPos.Zero, true, oldStation);
			state = EconomyFieldDefensePolicy.Harvested(state, new CPos(20, 20));

			Assert.That(state.HasPending, Is.True);
			Assert.That(state.Pending, Is.EqualTo(new CPos(20, 20)));
			Assert.That(state.Committed, Is.EqualTo(oldStation));

			state = EconomyFieldDefensePolicy.UnloadCompleted(state, false);
			Assert.That(state.Committed, Is.EqualTo(oldStation));
			Assert.That(state.HasPending, Is.True);

			state = EconomyFieldDefensePolicy.UnloadCompleted(state, true);
			Assert.That(state.HasPending, Is.False);
			Assert.That(state.Committed, Is.EqualTo(new CPos(20, 20)));
		}

		[Test]
		public void AbortedUnloadPreservesPendingAndCommittedFields()
		{
			var state = new HarvesterFieldContextState(true, new CPos(20, 20), true, new CPos(5, 5));
			var aborted = EconomyFieldDefensePolicy.UnloadAborted(state);

			Assert.That(aborted.HasPending, Is.True);
			Assert.That(aborted.Pending, Is.EqualTo(state.Pending));
			Assert.That(aborted.Committed, Is.EqualTo(state.Committed));
		}

		[TestCase(0, 2, 0)]
		[TestCase(1, 2, 2)]
		[TestCase(3, 2, 6)]
		public void RoleDemandPreservesPerHarvesterIntent(int harvesters, int perHarvester, int expected)
		{
			Assert.That(EconomyFieldDefensePolicy.RoleDemand(harvesters, perHarvester), Is.EqualTo(expected));
		}

		[Test]
		public void ReformPolicyUsesConfiguredTolerance()
		{
			Assert.That(EconomyFieldDefensePolicy.ShouldReform(1024L * 1024, 1, 8), Is.False);
			Assert.That(EconomyFieldDefensePolicy.ShouldReform(8L * 1024 * 8 * 1024, 1, 8), Is.False);
			Assert.That(EconomyFieldDefensePolicy.ShouldReform(9L * 1024 * 9 * 1024, 1, 8), Is.True);
		}

		[Test]
		public void FormationTreatsCurrentDestinationCellAsArrivedDespiteSubcellOffset()
		{
			var destination = new CPos(20, 20);
			Assert.That(EconomyFieldDefensePolicy.IsWithinFormation(destination, destination,
				long.MaxValue, 1), Is.True,
				"A same-cell subcell offset must not create an invalid one-cell exact route.");
			Assert.That(EconomyFieldDefensePolicy.IsWithinFormation(new CPos(19, 20), destination,
				2L * 1024 * 2 * 1024, 1), Is.False);
		}

		[TestCase(4, 0, 0, 0, 1, 1)]
		[TestCase(4, 3, 1, 0, 1, 0)]
		[TestCase(4, 2, 0, 1, 1, 0)]
		[TestCase(4, 2, 0, 0, 2, 2)]
		[TestCase(2, 3, 0, 0, 1, 0)]
		public void OutstandingRequestsAreBoundedAndDeduplicated(int target, int assigned, int queued,
			int ownedRequests, int maximumOutstanding, int expected)
		{
			Assert.That(EconomyFieldDefensePolicy.OutstandingRequestDemand(target, assigned, queued,
				ownedRequests, maximumOutstanding), Is.EqualTo(expected));
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

			var uncovered = EconomyFieldDefensePolicy.FirstUncoveredSamAnchor(anchors, coverage);
			Assert.That(uncovered?.ActorId, Is.EqualTo(20),
				"One powered site should satisfy both nearby refineries before resonator or silo demand.");
		}

		[TestCase(true, true, 1, 0, 4, true, true)]
		[TestCase(true, false, 1, 0, 4, true, false)]
		[TestCase(true, true, 4, 0, 4, true, false)]
		[TestCase(true, true, 1, 1, 4, true, false)]
		[TestCase(true, true, 1, 1, 4, false, false)]
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
			Assert.That(ownership.Owns(ordinaryQueue, "sam"), Is.False,
				"An ordinary SAM from another queue must retain normal BaseBuilder placement.");
			Assert.That(ownership.Owns(economyQueue, "gtwr"), Is.False);

			ownership.Refresh(100, 5, _ => true, (queue, type) =>
				ReferenceEquals(queue, economyQueue) && type == "sam");
			Assert.That(ownership.Owns(economyQueue, "sam"), Is.True,
				"Ownership must survive while the reserved SAM is queued or awaiting placement.");

			ownership.Refresh(100, 5, _ => true, (_, __) => false);
			Assert.That(ownership.HasReservation, Is.False,
				"A completed or cancelled build must release ownership for later ordinary SAMs.");
		}

		[Test]
		public void EconomySamPlacementOwnershipRestoresOnlyToTheMatchingQueuedBuild()
		{
			var beforeSaveQueue = new object();
			var loadedQueue = new object();
			var ownership = new EconomyDefenseSamBuildOwnership<object>();

			Assert.That(ownership.TryReserve(beforeSaveQueue, "sam", 1200), Is.True);
			Assert.That(ownership.ReservedActorType, Is.EqualTo("sam"));
			Assert.That(ownership.ReservedTick, Is.EqualTo(1200));

			var restored = new EconomyDefenseSamBuildOwnership<object>();
			Assert.That(restored.TryRestore(loadedQueue, ownership.ReservedActorType, ownership.ReservedTick,
				queue => ReferenceEquals(queue, loadedQueue),
				(queue, type) => ReferenceEquals(queue, loadedQueue) && type == "sam"), Is.True);
			Assert.That(restored.Owns(loadedQueue, "sam"), Is.True,
				"A loaded matching build must retain economy placement ownership.");
			Assert.That(restored.Owns(beforeSaveQueue, "sam"), Is.False,
				"Ownership must use the reconstructed queue instance, not the stale pre-save reference.");

			var missingBuild = new EconomyDefenseSamBuildOwnership<object>();
			Assert.That(missingBuild.TryRestore(loadedQueue, "sam", 1200, _ => true, (_, __) => false), Is.False,
				"Stale save data must not redirect an ordinary build when the matching queued SAM is gone.");
		}

		[TestCase(6, 1, 7)]
		[TestCase(0, 2, 2)]
		[TestCase(-1, -1, 0)]
		public void ActiveResourceModifierRangeIncludesConfiguredSafetyMargin(int range, int margin, int expected)
		{
			Assert.That(EconomyFieldDefensePolicy.ProjectedResourceHazardRadius(range, margin),
				Is.EqualTo(expected));
		}

		[Test]
		public void ProjectedResourceSafetyIsMandatoryOnlyForInfantry()
		{
			Assert.That(EconomyFieldDefensePolicy.RequiresProjectedResourceSafety(true), Is.True);
			Assert.That(EconomyFieldDefensePolicy.RequiresProjectedResourceSafety(false), Is.False,
				"Vehicles must reject current resource cells without being stranded by every projected modifier zone.");
		}

		[TestCase(3201, 3200, 25, 1)]
		[TestCase(3201, 3201, 25, 25)]
		[TestCase(3201, 3202, 25, 24)]
		[TestCase(3176, 3201, 25, 25)]
		[TestCase(3202, 3201, 25, 1)]
		[TestCase(-1, 3201, 25, 1)]
		public void RestoredScanPhaseSkipsMissedBoundaryWithoutShiftingCadence(
			int nextScanTick, int currentWorldTick, int scanInterval, int expected)
		{
			Assert.That(EconomyFieldDefensePolicy.RestoredScanTicks(
				nextScanTick, currentWorldTick, scanInterval), Is.EqualTo(expected));
		}

		[Test]
		public void DirtyAssignmentsDeduplicateAndDrainDeterministically()
		{
			var dirty = new EconomyFieldDefenseDirtyAssignments();
			Assert.That(dirty.Enqueue(20, 8), Is.True);
			Assert.That(dirty.Enqueue(10, 9), Is.True);
			Assert.That(dirty.Enqueue(10, 7), Is.True);
			Assert.That(dirty.Enqueue(10, 7), Is.False,
				"Repeated harmless hits must not create repeated validity work.");

			var pending = dirty.Drain();
			Assert.That(pending.Select(item => (item.FieldId, item.ActorId)), Is.EqualTo(new[]
			{
				(10u, 7u), (10u, 9u), (20u, 8u)
			}));
			Assert.That(dirty.Count, Is.Zero);
		}

		[Test]
		public void DirtyAssignmentSnapshotsPreservePendingSaveWork()
		{
			var beforeSave = new EconomyFieldDefenseDirtyAssignments();
			beforeSave.Enqueue(10, 7);
			beforeSave.Enqueue(20, 8);

			var loaded = new EconomyFieldDefenseDirtyAssignments();
			foreach (var item in beforeSave.Snapshot())
				loaded.Enqueue(item.FieldId, item.ActorId);

			Assert.That(loaded.Drain().Select(item => (item.FieldId, item.ActorId)),
				Is.EqualTo(new[] { (10u, 7u), (20u, 8u) }));
		}

		[TestCase(10, 10, 10, 10, 2, false)]
		[TestCase(10, 10, 12, 10, 2, false)]
		[TestCase(10, 10, 13, 10, 2, true)]
		[TestCase(10, 10, 10, 13, 2, true)]
		public void UrgentAttackMovesRequireAMateriallyNewEnemyCell(
			int previousX, int previousY, int currentX, int currentY, int radius, bool expected)
		{
			Assert.That(EconomyFieldDefensePolicy.IsMateriallyNewUrgentTarget(
				new CPos(previousX, previousY), new CPos(currentX, currentY), radius), Is.EqualTo(expected));
		}
	}
}
