#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class StealthLifecycleFleeBehaviorTest
	{
		sealed class Guard : IStealthLifecycleOwnershipGuard
		{
			public bool IsActive(BehaviorId owner, OwnershipEpoch epoch) { return true; }
		}

		sealed class World : IStealthRecalculateFleeLiveWorld
		{
			public StealthRecalculateFleeLiveSnapshot Snapshot;
			public StealthRecalculateFleeLiveSnapshot Read(StealthApproachMission mission) { return Snapshot; }
		}

		sealed class Cache : IStealthRecalculateFleeStrategicCache
		{
			public int Reads;
			public CPos[] Route = { new CPos(-5, 0) };
			public StealthTargetThreatScore Danger = new StealthTargetThreatScore(1, double.PositiveInfinity);
			public StealthRecalculateFleeStrategicCacheSnapshot ReadEscapeRoute(
				StealthApproachMission mission)
			{
				Reads++;
				return new StealthRecalculateFleeStrategicCacheSnapshot(3, Danger, Route);
			}
		}

		sealed class Orders : IStealthRecalculateFleeOrders
		{
			public readonly List<CPos> Destinations = new List<CPos>();
			public void IssueMove(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, CPos destinationCell,
				IReadOnlyList<CPos> orderedRoute, int routeProgress,
				StealthRecalculateFleeOrderToken token)
			{
				Destinations.Add(destinationCell);
			}
		}

		[Test]
		public void FleeUsesOneCachedStrategicEscapeRoute()
		{
			var world = new World
			{
				Snapshot = Snapshot(new CPos(0, 0))
			};
			var cache = new Cache();
			var orders = new Orders();
			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(),
				world, cache, orders);

			var result = behavior.Execute();

			Assert.That(result.SelectedDestinationCell, Is.EqualTo(new CPos(-5, 0)));
			Assert.That(result.OrderedRoute, Is.EqualTo(cache.Route));
			Assert.That(orders.Destinations.Single(), Is.EqualTo(new CPos(-5, 0)));
			Assert.That(cache.Reads, Is.EqualTo(1));
		}

		[Test]
		public void FleeSkipsTheWholeFormationFootprintAtTheStartOfItsRoute()
		{
			var world = new World
			{
				Snapshot = new StealthRecalculateFleeLiveSnapshot(1,
					new[]
					{
						new StealthRecalculateFleeMemberSnapshot(1, new CPos(0, 0), 5),
						new StealthRecalculateFleeMemberSnapshot(2, new CPos(-1, 0), 5)
					},
					new[]
					{
						new StealthRecalculateFleeEnemySnapshot(71, "mtnk", new CPos(5, 0),
							100, 100, 4, false)
					}, true, "current-live")
			};
			var cache = new Cache { Route = new[] { new CPos(0, 0), new CPos(-1, 0), new CPos(-5, 0) } };
			var orders = new Orders();

			var result = new StealthRecalculateFleeBehavior(Handoff(), new Guard(),
				world, cache, orders).Execute();

			Assert.That(result.OrderedRoute, Is.EqualTo(new[] { new CPos(-5, 0) }));
			Assert.That(orders.Destinations, Is.EqualTo(new[] { new CPos(-5, 0) }));
		}

		[Test]
		public void FleeRebuildsItsCachedRouteWhenFormationExposureChanges()
		{
			var initial = Snapshot(new CPos(0, 0));
			var world = new World { Snapshot = initial };
			var cache = new Cache();
			var orders = new Orders();
			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(),
				world, cache, orders);
			behavior.Execute();
			world.Snapshot = new StealthRecalculateFleeLiveSnapshot(2,
				initial.Members, initial.Enemies, formationCloaked: false,
				initial.SourceFingerprint);

			var exposed = behavior.Execute();

			Assert.That(cache.Reads, Is.EqualTo(2));
			Assert.That(orders.Destinations, Has.Count.EqualTo(2));
			Assert.That(exposed.LastOrderToken.RouteRevision, Is.EqualTo(2));
		}

		[Test]
		public void FleeLeavesTheEngineToCompleteOneDirectMove()
		{
			var destination = new CPos(-1, -5);
			var world = new World { Snapshot = Snapshot(new CPos(0, 0)) };
			var firstStep = new CPos(-2, 0);
			var cache = new Cache { Route = new[] { firstStep, destination } };
			var orders = new Orders();
			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(),
				world, cache, orders);
			var first = behavior.Execute();

			var retained = behavior.Execute();

			Assert.That(first.OrderedRoute, Is.EqualTo(new[] { firstStep }));
			Assert.That(retained.OrderedRoute, Is.EqualTo(new[] { firstStep }));
			Assert.That(orders.Destinations, Is.EqualTo(new[] { firstStep }));
			Assert.That(cache.Reads, Is.EqualTo(1));
		}

		[Test]
		public void FleeCompletesWhenTheFormationCenterReachesTheSafeCell()
		{
			var destination = new CPos(-5, 0);
			var world = new World { Snapshot = Snapshot(new CPos(0, 0)) };
			var orders = new Orders();

			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(), world,
				new Cache { Route = new[] { destination } }, orders);
			behavior.Execute();
			world.Snapshot = Snapshot(new CPos(-6, 0));
			var result = behavior.Execute();

			Assert.That(result.Disposition,
				Is.EqualTo(StealthRecalculateFleeDisposition.TargetAcquisition));
			Assert.That(result.LiveCause, Is.EqualTo(StealthRecalculateFleeLiveCause.Completed));
			Assert.That(orders.Destinations, Is.EqualTo(new[] { destination }));
		}

		[Test]
		public void FleeWaitsForMovementAndRetriesOnlyAfterItEnds()
		{
			var destination = new CPos(-5, 0);
			var world = new World { Snapshot = Snapshot(new CPos(0, 0)) };
			var orders = new Orders();
			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(),
				world, new Cache { Route = new[] { destination } }, orders);

			behavior.Execute();
			behavior.Execute();
			world.Snapshot = new StealthRecalculateFleeLiveSnapshot(2,
				new[]
				{
					new StealthRecalculateFleeMemberSnapshot(1, new CPos(0, 0), 5,
						needsMovementOrder: true),
					new StealthRecalculateFleeMemberSnapshot(2, new CPos(0, 0), 5,
						needsMovementOrder: true)
				},
				new[]
				{
					new StealthRecalculateFleeEnemySnapshot(71, "mtnk", new CPos(5, 0),
						100, 100, 4, false)
				}, true, "current-live");
			behavior.Execute();

			Assert.That(orders.Destinations, Is.EqualTo(new[] { destination, destination }));
		}

		[Test]
		public void FleeReturnsToAcquisitionWhenTheLocalFightIsGone()
		{
			var world = new World
			{
				Snapshot = new StealthRecalculateFleeLiveSnapshot(1,
					new[] { new StealthRecalculateFleeMemberSnapshot(1, new CPos(0, 0), 5) },
					Array.Empty<StealthRecalculateFleeEnemySnapshot>(),
					true, "current-live")
			};
			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(), world,
				new Cache(), new Orders());

			var result = behavior.Execute();

			Assert.That(result.Disposition,
				Is.EqualTo(StealthRecalculateFleeDisposition.TargetAcquisition));
			Assert.That(result.LiveCause, Is.EqualTo(StealthRecalculateFleeLiveCause.NoTarget));
		}

		[Test]
		public void FleeReturnsToAcquisitionWhenTheStrategicCacheHasNoEscapeRoute()
		{
			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(),
				new World { Snapshot = Snapshot(new CPos(0, 0)) },
				new Cache { Route = Array.Empty<CPos>() }, new Orders());

			var result = behavior.Execute();

			Assert.That(result.Disposition,
				Is.EqualTo(StealthRecalculateFleeDisposition.TargetAcquisition));
			Assert.That(result.LiveCause, Is.EqualTo(StealthRecalculateFleeLiveCause.NoRoute));
			Assert.That(result.LastOrderToken, Is.Null);
			var controller = Construct<StealthLifecycleController>(BehaviorId.RecalculateFlee,
				new OwnershipEpoch(2), -1);
			Assert.That(controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.TargetAcquisition.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
		}

		[Test]
		public void FleeIssuesOneSafeStepThenReconsidersWhenLiveCombatIsSafe()
		{
			var initial = Snapshot(new CPos(0, 0));
			var world = new World
			{
				Snapshot = new StealthRecalculateFleeLiveSnapshot(initial.Tick,
					initial.Members, initial.Enemies, initial.FormationCloaked,
					initial.SourceFingerprint, currentPositionSafe: true)
			};
			var orders = new Orders();
			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(), world,
				new Cache(), orders);

			Assert.That(behavior.Execute().Disposition,
				Is.EqualTo(StealthRecalculateFleeDisposition.Retain));
			var reconsidered = behavior.Execute();
			world.Snapshot = new StealthRecalculateFleeLiveSnapshot(2,
				new[]
				{
					new StealthRecalculateFleeMemberSnapshot(1, new CPos(-5, 0), 5,
						needsMovementOrder: true),
					new StealthRecalculateFleeMemberSnapshot(2, new CPos(-5, 0), 5,
						needsMovementOrder: true)
				}, initial.Enemies, initial.FormationCloaked,
				initial.SourceFingerprint, currentPositionSafe: true);
			Assert.That(reconsidered.Disposition,
				Is.EqualTo(StealthRecalculateFleeDisposition.TargetAcquisition));
			Assert.That(reconsidered.LiveCause,
				Is.EqualTo(StealthRecalculateFleeLiveCause.SafeToReconsider));
			Assert.That(reconsidered.SelectedDestinationCell, Is.Null);
			Assert.That(reconsidered.LastOrderToken, Is.Null);
			Assert.That(orders.Destinations, Has.Count.EqualTo(1));
			var controller = Construct<StealthLifecycleController>(BehaviorId.RecalculateFlee,
				new OwnershipEpoch(2), -1);
			Assert.That(controller.TryAccept(reconsidered, out var transition), Is.True);
			Assert.That(transition.TargetAcquisition.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
		}

		[Test]
		public void SafeReconsiderationClearsAnOrderWhoseMembershipChanged()
		{
			var world = new World { Snapshot = Snapshot(new CPos(0, 0)) };
			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(), world,
				new Cache(), new Orders());
			behavior.Execute();
			world.Snapshot = new StealthRecalculateFleeLiveSnapshot(2,
				new[]
				{
					new StealthRecalculateFleeMemberSnapshot(1, new CPos(1, 0), 5,
						needsMovementOrder: true)
				},
				world.Snapshot.Enemies, true, "changed-members", currentPositionSafe: true);

			var result = behavior.Execute();

			Assert.That(result.Disposition,
				Is.EqualTo(StealthRecalculateFleeDisposition.TargetAcquisition));
			Assert.That(result.ActiveMemberActorIds, Is.EqualTo(new uint[] { 1 }));
			Assert.That(result.LastOrderToken, Is.Null);
		}

		static StealthRecalculateFleeLiveSnapshot Snapshot(CPos memberCell)
		{
			return new StealthRecalculateFleeLiveSnapshot(1,
				new[]
				{
					new StealthRecalculateFleeMemberSnapshot(1, memberCell, 5),
					new StealthRecalculateFleeMemberSnapshot(2, memberCell, 5)
				},
				new[]
				{
					new StealthRecalculateFleeEnemySnapshot(71, "mtnk", new CPos(5, 0),
						100, 100, 4, false)
				}, true, "current-live");
		}

		static StealthRecalculateFleeHandoff Handoff()
		{
			var evidence = Construct<StealthRecalculateFleeEntryEvidence>(
				StealthRecalculateFleeSource.KiteNoSafePlan, new OwnershipEpoch(1), "old-entry",
				71u, new CPos(99, 99), new uint[] { 1, 2 }, new uint[] { 71 }, true,
				new StealthTargetThreatScore(1, 2));
			var owner = Construct<StealthBehaviorHandoff>(
				BehaviorId.RecalculateFlee, new OwnershipEpoch(2));
			return Construct<StealthRecalculateFleeHandoff>(owner, Mission(), evidence);
		}

		static StealthApproachMission Mission()
		{
			var cell = new CPos(5, 5);
			var option = Construct<StealthTargetOption>(cell, (int?)1000, false,
				new[] { new StealthStrategicTargetSnapshot(71, cell, 5000, 1100, 100, 100) }, null);
			var value = Construct<StealthTargetValueOption>(option, 5500000L);
			return Construct<StealthApproachMission>(Construct<StealthTargetThreatOption>(value,
				new StealthTargetThreatScore(1, 2)));
		}

		static T Construct<T>(params object[] arguments)
		{
			return (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance |
				BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null);
		}
	}
}
