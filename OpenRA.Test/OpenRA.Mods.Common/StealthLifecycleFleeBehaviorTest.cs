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

		sealed class Threat : IStealthRecalculateFleeThreatAdapter
		{
			public readonly Dictionary<CPos, StealthTargetThreatScore> Dangers =
				new Dictionary<CPos, StealthTargetThreatScore>();
			public StealthTargetThreatScore CalculateEntryCrossover(
				StealthRecalculateFleeEntryThreatFacts facts)
			{
				throw new InvalidOperationException("Flee must not revalidate stale entry evidence.");
			}

			public StealthTargetThreatScore CalculateRouteDanger(StealthRecalculateFleeThreatFacts facts)
			{
				return Dangers[facts.CandidateCell];
			}
		}

		sealed class Cache : IStealthRecalculateFleeStrategicCache
		{
			public int Reads;
			public StealthRecalculateFleeStrategicCacheSnapshot ReadLongRoute(
				StealthApproachMission mission, CPos liveDestination)
			{
				Reads++;
				return new StealthRecalculateFleeStrategicCacheSnapshot(3,
					new[] { new CPos(-1, 0), liveDestination });
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
		public void FleeChoosesLeastDangerousLiveCandidateAndUsesCacheOnlyForLongRoute()
		{
			var local = new CPos(2, 0);
			var longRoute = new CPos(-5, 0);
			var world = new World
			{
				Snapshot = Snapshot(new CPos(0, 0), new[]
				{
					new StealthRecalculateFleeCandidateSnapshot(local, true),
					new StealthRecalculateFleeCandidateSnapshot(longRoute, true, true)
				})
			};
			var threat = new Threat();
			threat.Dangers[local] = new StealthTargetThreatScore(8, 4);
			threat.Dangers[longRoute] = new StealthTargetThreatScore(1, 1);
			var cache = new Cache();
			var orders = new Orders();
			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(),
				world, threat, cache, orders);

			var result = behavior.Execute();

			Assert.That(result.SelectedDestinationCell, Is.EqualTo(longRoute));
			Assert.That(result.OrderedRoute, Is.EqualTo(new[] { new CPos(-1, 0), longRoute }));
			Assert.That(orders.Destinations.Single(), Is.EqualTo(new CPos(-1, 0)));
			Assert.That(cache.Reads, Is.EqualTo(1));
		}

		[Test]
		public void FleeAdvancesOneSharedWaypointFromCurrentLivePositions()
		{
			var destination = new CPos(-5, 0);
			var candidates = new[]
			{
				new StealthRecalculateFleeCandidateSnapshot(destination, true, true)
			};
			var world = new World { Snapshot = Snapshot(new CPos(0, 0), candidates) };
			var threat = new Threat();
			threat.Dangers[destination] = new StealthTargetThreatScore(1, 1);
			var orders = new Orders();
			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(),
				world, threat, new Cache(), orders);
			behavior.Execute();
			world.Snapshot = Snapshot(new CPos(-1, 0), candidates);

			var result = behavior.Execute();

			Assert.That(result.RouteProgress, Is.EqualTo(1));
			Assert.That(orders.Destinations,
				Is.EqualTo(new[] { new CPos(-1, 0), destination }));
		}

		[Test]
		public void FleeCompletesWhenTheFormationCenterReachesTheSafeCell()
		{
			var destination = new CPos(-5, 0);
			var candidates = new[]
			{
				new StealthRecalculateFleeCandidateSnapshot(destination, true)
			};
			var world = new World { Snapshot = Snapshot(new CPos(0, 0), candidates) };
			var threat = new Threat();
			threat.Dangers[destination] = new StealthTargetThreatScore(0, double.PositiveInfinity);
			var orders = new Orders();

			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(), world,
				threat, new Cache(), orders);
			behavior.Execute();
			world.Snapshot = Snapshot(new CPos(-6, 0), candidates);
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
			var candidates = new[]
			{
				new StealthRecalculateFleeCandidateSnapshot(destination, true)
			};
			var world = new World { Snapshot = Snapshot(new CPos(0, 0), candidates) };
			var threat = new Threat();
			threat.Dangers[destination] = new StealthTargetThreatScore(1, 1);
			var orders = new Orders();
			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(),
				world, threat, new Cache(), orders);

			behavior.Execute();
			behavior.Execute();
			world.Snapshot = new StealthRecalculateFleeLiveSnapshot(2,
				new[]
				{
					new StealthRecalculateFleeMemberSnapshot(1, new CPos(0, 0), 5,
						needsMovementOrder: true),
					new StealthRecalculateFleeMemberSnapshot(2, new CPos(0, 0), 5)
				},
				new[]
				{
					new StealthRecalculateFleeEnemySnapshot(71, "mtnk", new CPos(5, 0),
						100, 100, 4, false)
				}, candidates, true, "current-live");
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
					Array.Empty<StealthRecalculateFleeCandidateSnapshot>(), true, "current-live")
			};
			var behavior = new StealthRecalculateFleeBehavior(Handoff(), new Guard(), world,
				new Threat(), new Cache(), new Orders());

			var result = behavior.Execute();

			Assert.That(result.Disposition,
				Is.EqualTo(StealthRecalculateFleeDisposition.TargetAcquisition));
			Assert.That(result.LiveCause, Is.EqualTo(StealthRecalculateFleeLiveCause.NoTarget));
		}

		static StealthRecalculateFleeLiveSnapshot Snapshot(CPos memberCell,
			IEnumerable<StealthRecalculateFleeCandidateSnapshot> candidates)
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
				}, candidates, true, "current-live");
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
				new StealthTargetThreatScore(1, 2)), 0L, 0, 1000L);
		}

		static T Construct<T>(params object[] arguments)
		{
			return (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance |
				BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null);
		}
	}
}
