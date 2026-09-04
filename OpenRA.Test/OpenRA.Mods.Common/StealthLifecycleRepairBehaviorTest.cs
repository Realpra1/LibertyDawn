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
	public sealed class StealthLifecycleRepairBehaviorTest
	{
		sealed class Guard : IStealthLifecycleOwnershipGuard
		{
			public bool IsActive(BehaviorId owner, OwnershipEpoch epoch) { return true; }
		}

		sealed class World : IStealthRepairLiveWorld
		{
			public StealthRepairLiveSnapshot Snapshot;
			public StealthRepairLiveSnapshot Read(StealthApproachMission mission) { return Snapshot; }
		}

		sealed class Threat : IStealthRepairThreatAdapter
		{
			public bool AnySafe = true;
			public StealthTargetThreatScore CalculateRouteDanger(StealthRepairThreatFacts facts)
			{
				if (!AnySafe || facts.RepairOptionActorId == 200)
					return new StealthTargetThreatScore(2, 1);
				return new StealthTargetThreatScore(0, 4);
			}
		}

		sealed class Cache : IStealthRepairStrategicCache
		{
			public StealthRepairStrategicCacheSnapshot ReadLongRoute(StealthApproachMission mission,
				uint repairOptionActorId, IReadOnlyList<CPos> liveRoute)
			{
				throw new InvalidOperationException("Local Repair routes must not read the strategic cache.");
			}
		}

		sealed class Orders : IStealthRepairOrders
		{
			public readonly List<uint> Options = new List<uint>();
			public void IssueRepair(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint repairOptionActorId,
				IReadOnlyList<CPos> orderedRoute, int routeProgress, StealthRepairOrderKind kind,
				StealthRepairOrderToken token)
			{
				Options.Add(repairOptionActorId);
			}
		}

		[Test]
		public void RepairWaitsForRetreatMovementAndRetriesOnlyAfterItEnds()
		{
			var world = new World { Snapshot = Snapshot(40) };
			var orders = new Orders();
			var behavior = new StealthRepairBehavior(Handoff(), new Guard(), world,
				new Threat(), new Cache(), orders);

			var first = behavior.Execute();
			var second = behavior.Execute();
			world.Snapshot = Snapshot(40, true);
			var third = behavior.Execute();

			Assert.That(first.Disposition, Is.EqualTo(StealthRepairDisposition.Retain));
			Assert.That(first.SelectedRepairOptionActorId, Is.EqualTo(100));
			Assert.That(first.SelectedRouteIdentity, Is.EqualTo(1000));
			Assert.That(second.LastOrderToken, Is.EqualTo(first.LastOrderToken));
			Assert.That(third.LastOrderToken, Is.Not.EqualTo(second.LastOrderToken));
			Assert.That(orders.Options, Is.EqualTo(new uint[] { 100, 100 }));
		}

		[Test]
		public void RepairResumesFightWhenNoCurrentRouteIsSafe()
		{
			var world = new World { Snapshot = Snapshot(40) };
			var threat = new Threat { AnySafe = false };
			var orders = new Orders();
			var behavior = new StealthRepairBehavior(Handoff(), new Guard(), world,
				threat, new Cache(), orders);

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthRepairDisposition.ResumeFight));
			Assert.That(result.LiveCause, Is.EqualTo(StealthRepairLiveCause.NoSafeRepair));
			Assert.That(orders.Options, Is.Empty);
		}

		[Test]
		public void FullyRepairedMemberReturnsToStart()
		{
			var world = new World { Snapshot = Snapshot(100) };
			var behavior = new StealthRepairBehavior(Handoff(), new Guard(), world,
				new Threat(), new Cache(), new Orders());

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthRepairDisposition.Start));
			Assert.That(result.Completion.Members.Single().ActorId, Is.EqualTo(1));
		}

		static StealthRepairLiveSnapshot Snapshot(int hitPoints,
			bool needsMovementOrder = false)
		{
			return new StealthRepairLiveSnapshot(10, 77, 5, 900, 25, "fight-context",
				new[]
				{
					new StealthRepairMemberSnapshot(1, new CPos(0, 0), 5, hitPoints, 100,
						needsMovementOrder: needsMovementOrder)
				},
				new[]
				{
					new StealthRepairOptionSnapshot(100, new CPos(5, 0)),
					new StealthRepairOptionSnapshot(200, new CPos(6, 0))
				}, Array.Empty<StealthRepairEnemySnapshot>(),
				Array.Empty<StealthRepairStaticActorSnapshot>(),
				new[]
				{
					new StealthRepairRouteSnapshot(1000, 100,
						new[] { new CPos(1, 0), new CPos(5, 0) }, true),
					new StealthRepairRouteSnapshot(2000, 200,
						new[] { new CPos(1, 1), new CPos(6, 0) }, true)
				}, true);
		}

		static StealthRepairHandoff Handoff()
		{
			var mission = Mission();
			var resume = Construct<StealthRepairResumeContext>(BehaviorId.Kite,
				new OwnershipEpoch(1), mission, new uint[] { 1 }, new uint[] { 71 },
				(uint?)71, (CPos?)new CPos(5, 0), "fight-context");
			var damageOwner = Construct<StealthBehaviorHandoff>(BehaviorId.Damage, new OwnershipEpoch(2));
			var request = Construct<StealthDamageRepairRequest>(damageOwner, 77L, 5, 900u, 25,
				new[] { new StealthRepairDamagedMember(1, 40, 100) }, resume);
			var repairOwner = Construct<StealthBehaviorHandoff>(BehaviorId.Repair, new OwnershipEpoch(3));
			return Construct<StealthRepairHandoff>(repairOwner, request);
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
