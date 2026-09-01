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
	public sealed class StealthApproachBehaviorTest
	{
		sealed class CacheProbe : IStealthApproachStrategicCache,
			IStealthApproachStrategicRouteCache
		{
			readonly StealthApproachStrategicCacheSnapshot snapshot;
			public int Reads { get; private set; }
			public bool ThrowOnRead { get; set; }

			public CacheProbe(StealthApproachStrategicCacheSnapshot snapshot) { this.snapshot = snapshot; }

			public StealthApproachStrategicCacheSnapshot ReadSnapshot()
			{
				Reads++;
				if (ThrowOnRead)
					throw new InvalidOperationException("Strategic cache was read during a live-only check.");
				return snapshot;
			}

			public IReadOnlyList<CPos> ReadRoute(CPos origin, CPos destination)
			{
				var danger = snapshot.Cells.Select(cell =>
					cell.HasDetectorCoverage || cell.PlannedActionRevealsFormation ?
					(float)cell.EnemyGroup.Sum(enemy =>
						enemy.ActorType == "obelisk" ? 50 * enemy.Count : 10 * enemy.Count) : 0f).ToArray();
				var endpoints = new[]
				{
					destination, destination + new CVec(-1, 0), destination + new CVec(1, 0),
					destination + new CVec(0, -1), destination + new CVec(0, 1)
				}.Where(cell => cell.X >= 0 && cell.Y >= 0 &&
					cell.X < snapshot.Width && cell.Y < snapshot.Height).Distinct();
				return endpoints.Select(endpoint => ThreatAwareRoutePlanner.FindRoute(danger,
					snapshot.Width, snapshot.Height, origin.X, origin.Y, endpoint.X, endpoint.Y,
					snapshot.RouteThreatPenalty)).Where(route => route != null)
					.OrderBy(route => route.Sum(cell => 1d +
						danger[cell.Y * snapshot.Width + cell.X] * snapshot.RouteThreatPenalty))
					.ThenBy(route => route.Count).FirstOrDefault() ?? new List<CPos>();
			}
		}

		sealed class AcquisitionCacheProbe : IStealthTargetAcquisitionCache
		{
			readonly StealthTargetAcquisitionCacheSnapshot snapshot;
			public AcquisitionCacheProbe(StealthTargetAcquisitionCacheSnapshot snapshot) { this.snapshot = snapshot; }
			public StealthTargetAcquisitionCacheSnapshot ReadSnapshot() { return snapshot; }
		}

		sealed class ApproachInput
		{
			public StealthLifecycleController Controller { get; set; }
			public StealthApproachHandoff Handoff { get; set; }
			public StealthApproachMission Mission => Handoff.Missions.Single();
		}

		sealed class LiveProbe : IStealthApproachLiveWorld
		{
			public StealthApproachLiveSnapshot Snapshot { get; set; }
			public int Reads { get; private set; }
			public StealthApproachMission LastMission { get; private set; }

			public LiveProbe(StealthApproachLiveSnapshot snapshot) { Snapshot = snapshot; }

			public StealthApproachLiveSnapshot Read(StealthApproachMission mission)
			{
				Reads++;
				LastMission = mission;
				return Snapshot;
			}
		}

		sealed class StandardThreatProbe : IStealthTargetThreatAdapter
		{
			public readonly List<StealthTargetThreatFacts> Facts = new List<StealthTargetThreatFacts>();

			public StealthTargetThreatScore Calculate(StealthTargetThreatFacts facts)
			{
				Facts.Add(facts);
				var targetable = !facts.FormationCloaked || facts.HasDetectorCoverage ||
					facts.PlannedActionRevealsFormation;
				var threat = targetable ? facts.EnemyGroup.Sum(enemy =>
					enemy.ActorType == "obelisk" ? 50d * enemy.Count : 10d * enemy.Count) : 0;
				return new StealthTargetThreatScore(threat, threat + 1);
			}
		}

		sealed class OrderProbe : IStealthApproachMovementOrders
		{
			public readonly List<(BehaviorId Owner, OwnershipEpoch Epoch,
				uint[] ActorIds, CPos Destination)> Orders = new List<(BehaviorId, OwnershipEpoch, uint[], CPos)>();

			public void IssueMove(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, CPos destinationStrategicCell)
			{
				Orders.Add((owner, epoch, actorIds.ToArray(), destinationStrategicCell));
			}
		}

		static readonly StealthCombatGroupSnapshot[] NoEnemies = Array.Empty<StealthCombatGroupSnapshot>();

		static StealthCombatGroupSnapshot Enemy(string type = "e3")
		{
			return new StealthCombatGroupSnapshot(type, 1, 500);
		}

		static ApproachInput Input(CPos target, bool cloaked = true, bool plannedReveal = true,
			uint targetActorId = 99, long approachEpoch = 7)
		{
			var selectedFacts = new StealthTargetThreatFacts(target,
				new[] { new StealthCombatGroupSnapshot("stnk", 4, 900) }, new[] { Enemy() },
				cloaked, false, plannedReveal);
			var fillerCells = Enumerable.Range(0, 9).Select(index => new CPos(10 + index, 1)).ToArray();
			var cells = new[] { target }.Concat(fillerCells).ToArray();
			var targets = targetActorId == 0 ? Array.Empty<StealthStrategicTargetSnapshot>() :
				new[] { new StealthStrategicTargetSnapshot(targetActorId, target, 10000, 1000, 100, 100) }
					.Concat(fillerCells.Select((cell, index) => new StealthStrategicTargetSnapshot(
						(uint)(100 + index), cell, 1, 1000, 100, 100))).ToArray();
			var facts = new[] { selectedFacts }.Concat(fillerCells.Select(cell =>
				new StealthTargetThreatFacts(cell, selectedFacts.FriendlyGroup, new[] { Enemy() },
					cloaked, true, plannedReveal))).ToArray();
			var controller = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.TargetAcquisition, new OwnershipEpoch(approachEpoch - 4), -1));
			var cache = new AcquisitionCacheProbe(new StealthTargetAcquisitionCacheSnapshot(40, 20,
				Enumerable.Repeat(0f, 800), cells, 1, targets, facts));
			var acquisition = new StealthTargetAcquisitionBehavior(controller.CurrentHandoff, cache)
				.Execute(new CPos(0, 0), null);
			Assert.That(controller.TryAccept(acquisition, out var valueHandoff), Is.True);
			var value = new StealthTargetValueFilterBehavior(valueHandoff).Execute();
			Assert.That(controller.TryAccept(value, out var threatHandoff), Is.True);
			var threat = new StealthTargetThreatFilterBehavior(
				threatHandoff, new StandardThreatProbe()).Execute();
			Assert.That(controller.TryAccept(threat, out var distanceHandoff), Is.True);
			var distance = new StealthTargetDistanceChoiceBehavior(distanceHandoff,
				Array.Empty<StealthActiveSquadTargetSnapshot>(),
				new StealthTargetDistanceChoicePolicy(1000, 3000)).Execute();
			Assert.That(controller.TryAccept(distance, out var approachHandoff), Is.True);
			Assert.That(approachHandoff.Missions.Single().StrategicCell, Is.EqualTo(target));
			return new ApproachInput { Controller = controller, Handoff = approachHandoff };
		}

		static StealthApproachStrategicCacheSnapshot Cache(int width, int height,
			Func<CPos, StealthApproachStrategicCellSnapshot> cellFactory = null)
		{
			return new StealthApproachStrategicCacheSnapshot(width, height,
				Enumerable.Range(0, width * height).Select(index =>
				{
					var cell = new CPos(index % width, index / width);
					return cellFactory?.Invoke(cell) ??
						new StealthApproachStrategicCellSnapshot(cell, NoEnemies, false);
				}));
		}

		static StealthApproachLiveSnapshot Live(CPos core, bool valid = true,
			IEnumerable<StealthApproachMemberSnapshot> others = null,
			IEnumerable<StealthCombatGroupSnapshot> localEnemies = null,
			IEnumerable<uint> defenders = null, bool cloaked = true, bool detector = false,
			bool plannedReveal = false)
		{
			return new StealthApproachLiveSnapshot(valid,
				new[] { new StealthApproachMemberSnapshot(1, core) }
					.Concat(others ?? Array.Empty<StealthApproachMemberSnapshot>()),
				new[] { new StealthCombatGroupSnapshot("stnk", 1, 900) },
				localEnemies ?? NoEnemies, defenders ?? Array.Empty<uint>(), cloaked, detector,
				plannedReveal);
		}

		static StealthApproachBehavior Behavior(StealthApproachHandoff handoff,
			StealthApproachStrategicCacheSnapshot cache, StealthApproachLiveSnapshot live,
			out CacheProbe cacheProbe, out LiveProbe liveProbe,
			out StandardThreatProbe threatProbe, out OrderProbe orderProbe)
		{
			cacheProbe = new CacheProbe(cache);
			liveProbe = new LiveProbe(live);
			threatProbe = new StandardThreatProbe();
			orderProbe = new OrderProbe();
			return new StealthApproachBehavior(handoff, cacheProbe, liveProbe,
				threatProbe, orderProbe);
		}

		[Test]
		public void GraduatedStandardThreatMakesTheSaferLongerRouteWin()
		{
			var input = Input(new CPos(4, 1));
			var behavior = Behavior(input.Handoff, Cache(5, 3, cell => cell == new CPos(1, 1) ?
				new StealthApproachStrategicCellSnapshot(cell, new[] { Enemy() }, true) :
				new StealthApproachStrategicCellSnapshot(cell, NoEnemies, false)), Live(new CPos(0, 1)),
				out _, out _, out var threats, out _);

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthApproachDisposition.Moving));
			Assert.That(result.Route, Does.Not.Contain(new CPos(1, 1)));
			Assert.That(result.Route.Any(cell => cell.Y != 1), Is.True);
			Assert.That(threats.Facts.Single(fact => fact.StrategicCell == new CPos(1, 1)).HasDetectorCoverage,
				Is.True);
		}

		[Test]
		public void DetectorCoveredThreatRemainsTraversableAsTheOnlyRoute()
		{
			var input = Input(new CPos(3, 0));
			var behavior = Behavior(input.Handoff, Cache(4, 1, cell => cell == new CPos(1, 0) ?
				new StealthApproachStrategicCellSnapshot(cell, new[] { Enemy() }, true) :
				new StealthApproachStrategicCellSnapshot(cell, NoEnemies, false)), Live(new CPos(0, 0)),
				out _, out _, out _, out _);

			var result = behavior.Execute();

			Assert.That(result.Route, Does.Contain(new CPos(1, 0)));
			Assert.That(result.Disposition, Is.EqualTo(StealthApproachDisposition.Moving));
		}

		[Test]
		public void NonDetectorObeliskDoesNotBlockCloakedTransit()
		{
			var input = Input(new CPos(3, 0), cloaked: true, plannedReveal: true);
			var behavior = Behavior(input.Handoff, Cache(4, 1, cell => cell == new CPos(1, 0) ?
				new StealthApproachStrategicCellSnapshot(cell, new[] { Enemy("obelisk") }, false) :
				new StealthApproachStrategicCellSnapshot(cell, NoEnemies, false)), Live(new CPos(0, 0)),
				out _, out _, out var threats, out _);

			var result = behavior.Execute();

			Assert.That(result.Route, Does.Contain(new CPos(1, 0)));
			var obelisk = threats.Facts.Single(fact => fact.StrategicCell == new CPos(1, 0));
			Assert.That(obelisk.FormationCloaked, Is.True);
			Assert.That(obelisk.HasDetectorCoverage, Is.False);
			Assert.That(obelisk.PlannedActionRevealsFormation, Is.False,
				"Ordinary long transit remains cloaked despite the later mission action.");
		}

		[Test]
		public void PlannedRevealSegmentUsesFullThreatWithoutDetectorCoverage()
		{
			var input = Input(new CPos(3, 0), cloaked: true, plannedReveal: true);
			var behavior = Behavior(input.Handoff, Cache(4, 1, cell => cell == new CPos(1, 0) ?
				new StealthApproachStrategicCellSnapshot(cell, new[] { Enemy() }, false, true) :
				new StealthApproachStrategicCellSnapshot(cell, NoEnemies, false)), Live(new CPos(0, 0)),
				out _, out _, out var threats, out _);

			behavior.Execute();

			var reveal = threats.Facts.Single(fact => fact.StrategicCell == new CPos(1, 0));
			Assert.That(reveal.FormationCloaked, Is.True);
			Assert.That(reveal.HasDetectorCoverage, Is.False);
			Assert.That(reveal.PlannedActionRevealsFormation, Is.True);
		}

		[Test]
		public void MovementIsOwnerEpochBoundedDeduplicatedAndExcludesDistantReinforcementsFromCenter()
		{
			var input = Input(new CPos(5, 0));
			var behavior = Behavior(input.Handoff, Cache(6, 2), Live(new CPos(0, 0), others: new[]
			{
				new StealthApproachMemberSnapshot(2, new CPos(1, 0), true),
				new StealthApproachMemberSnapshot(3, new CPos(5, 1), true)
			}), out var cache, out var live, out _, out var orders);

			var first = behavior.Execute();
			var second = behavior.Execute();

			Assert.That(first.ActiveSquadCenter, Is.EqualTo(new CPos(0, 0)));
			Assert.That(first.ActiveMemberActorIds, Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(first.ActiveMemberActorIds, Does.Not.Contain(3));
			Assert.That(orders.Orders, Has.Count.EqualTo(1));
			Assert.That(orders.Orders[0].Owner, Is.EqualTo(BehaviorId.Approach));
			Assert.That(orders.Orders[0].Epoch, Is.EqualTo(new OwnershipEpoch(7)));
			Assert.That(orders.Orders[0].ActorIds, Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(first.Route[first.RouteIndex], Is.EqualTo(orders.Orders[0].Destination));
			Assert.That(second.Route[second.RouteIndex], Is.EqualTo(orders.Orders[0].Destination));
			Assert.That(cache.Reads, Is.EqualTo(2));
			Assert.That(live.Reads, Is.EqualTo(2));

			live.Snapshot = Live(orders.Orders[0].Destination, others: new[]
			{
				new StealthApproachMemberSnapshot(2, orders.Orders[0].Destination, true),
				new StealthApproachMemberSnapshot(3, new CPos(5, 1), true)
			});
			var advanced = behavior.Execute();
			Assert.That(advanced.RouteIndex, Is.GreaterThan(first.RouteIndex));
			Assert.That(orders.Orders, Has.Count.EqualTo(2));
			Assert.That(orders.Orders[1].Destination, Is.EqualTo(advanced.Route[advanced.RouteIndex]));
		}

		[Test]
		public void LiveLocalSafetyCanReacquireWithoutReadingTheStrategicCache()
		{
			var input = Input(new CPos(5, 0));
			var behavior = Behavior(input.Handoff, Cache(6, 1),
				Live(new CPos(0, 0), localEnemies: new[] { Enemy() }, detector: true),
				out var cache, out _, out var threats, out var orders);
			cache.ThrowOnRead = true;

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthApproachDisposition.Reacquire));
			Assert.That(result.LocalThreatScore.Value.ThreatRating, Is.EqualTo(10));
			Assert.That(threats.Facts.Single().StrategicCell, Is.EqualTo(new CPos(0, 0)));
			Assert.That(cache.Reads, Is.Zero);
			Assert.That(orders.Orders, Is.Empty);
			Assert.That(input.Controller.TryAccept(result, out var transition), Is.True);
			Assert.That(input.Controller.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
			Assert.That(transition.Reacquisition.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
		}

		[Test]
		public void EventualMissionRevealDoesNotExposeCloakedLocalObeliskTransit()
		{
			var input = Input(new CPos(5, 0), cloaked: true, plannedReveal: true);
			var behavior = Behavior(input.Handoff, Cache(6, 1), Live(new CPos(0, 0),
				localEnemies: new[] { Enemy("obelisk") }, cloaked: true, detector: false,
				plannedReveal: false), out var cache, out _, out var threats, out var orders);

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthApproachDisposition.Moving));
			Assert.That(result.LocalThreatScore.Value.ThreatRating, Is.Zero);
			Assert.That(threats.Facts.First().PlannedActionRevealsFormation, Is.False);
			Assert.That(cache.Reads, Is.EqualTo(1));
			Assert.That(orders.Orders, Has.Count.EqualTo(1));
		}

		[TestCase(false, BehaviorId.UndefendedAttack)]
		[TestCase(true, BehaviorId.CrushEvaluation)]
		public void ArrivalUsesOnlyLiveDefendersAndTransfersExactlyOneTypedMission(
			bool defended, BehaviorId expectedOwner)
		{
			var input = Input(new CPos(3, 0));
			var mission = input.Mission;
			var controller = input.Controller;
			var cache = new CacheProbe(Cache(4, 1)) { ThrowOnRead = true };
			var live = new LiveProbe(Live(new CPos(2, 0), defenders: defended ? new uint[] { 44 } : null));
			var threats = new StandardThreatProbe();
			var behavior = new StealthApproachBehavior(
				input.Handoff, cache, live, threats, new OrderProbe());
			controller.Observe(new StealthLifecycleObservationFrame(10, new[]
			{
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Timer),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.WorldEvent)
			}));

			var result = behavior.Execute();

			Assert.That(cache.Reads, Is.Zero);
			Assert.That(threats.Facts, Is.Empty);
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.Approach),
				"Passive observations cannot steal ownership.");
			Assert.That(controller.TryAccept(result, out var transition), Is.True);
			Assert.That(controller.Owner, Is.EqualTo(expectedOwner));
			Assert.That(controller.Epoch, Is.EqualTo(new OwnershipEpoch(8)));
			if (defended)
			{
				Assert.That(transition.CrushEvaluation.Mission, Is.SameAs(mission));
				Assert.That(transition.CrushEvaluation.Owner, Is.EqualTo(BehaviorId.CrushEvaluation));
				Assert.That(transition.CrushEvaluation.Epoch, Is.EqualTo(new OwnershipEpoch(8)));
				Assert.That(transition.CrushEvaluation.LiveDefenderActorIds, Is.EqualTo(new uint[] { 44 }));
				Assert.That(transition.UndefendedAttack, Is.Null);
			}
			else
			{
				Assert.That(transition.UndefendedAttack.Mission, Is.SameAs(mission));
				Assert.That(transition.UndefendedAttack.Owner, Is.EqualTo(BehaviorId.UndefendedAttack));
				Assert.That(transition.UndefendedAttack.Epoch, Is.EqualTo(new OwnershipEpoch(8)));
				Assert.That(transition.CrushEvaluation, Is.Null);
			}

			var saved = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			behavior.RestorePrivateState(MiniYaml.FromString(saved).Single());
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(), Is.EqualTo(saved));
			Assert.That(controller.TryAccept(result, out var duplicate), Is.False);
			Assert.That(duplicate, Is.Null);
		}

		[Test]
		public void PrivateStateRoundTripsAndRejectsStaleWrongOwnerAlteredMissionRouteAndHandoff()
		{
			var input = Input(new CPos(5, 0));
			var mission = input.Mission;
			var cache = Cache(6, 1, cell => cell == new CPos(1, 0) ?
				new StealthApproachStrategicCellSnapshot(cell, new[] { Enemy() }, true) :
				new StealthApproachStrategicCellSnapshot(cell, NoEnemies, false));
			var behavior = Behavior(input.Handoff, cache, Live(new CPos(0, 0)),
				out _, out _, out _, out _);
			behavior.Execute();
			var serialized = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input.Handoff, cache, Live(new CPos(0, 0)),
				out _, out _, out _, out var restoredOrders);

			restored.RestorePrivateState(MiniYaml.FromString(serialized).Single());
			restored.Execute();

			Assert.That(restoredOrders.Orders, Is.Empty,
				"Restored movement deduplication must preserve the already issued useful order.");
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(serialized));
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("Owner: Approach", "Owner: TargetDistanceChoice")).Single()));
			Assert.Throws<InvalidOperationException>(() => Behavior(
				Input(new CPos(5, 0), approachEpoch: 8).Handoff, cache, Live(new CPos(0, 0)),
				out _, out _, out _, out _).RestorePrivateState(MiniYaml.FromString(serialized).Single()));
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("EstimatedTravelMilliseconds: 5000",
					"EstimatedTravelMilliseconds: 5001")).Single()));
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("RouteIndex: 0", "RouteIndex: 1")).Single()));
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("ArrivalClassification: None", "ArrivalClassification: Defended")).Single()));
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("HasDetectorCoverage: True", "HasDetectorCoverage: False")).Single()));

			var dedupWithoutRoute = MiniYaml.FromString(serialized).Single();
			dedupWithoutRoute.Value.Nodes.RemoveAll(child => child.Key == "RouteCell");
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(dedupWithoutRoute));

			var truncatedRoute = MiniYaml.FromString(serialized).Single();
			var routeCells = truncatedRoute.Value.Nodes.Where(child => child.Key == "RouteCell").ToArray();
			Assert.That(routeCells.Select(child => FieldLoader.GetValue<CPos>("Cell",
				child.Value.Nodes.Single(field => field.Key == "Cell").Value.Value)),
				Is.EqualTo(new[] { new CPos(1, 0), new CPos(2, 0), new CPos(3, 0), new CPos(4, 0) }));
			truncatedRoute.Value.Nodes.RemoveAll(child => child.Key == "RouteCell" && child != routeCells.Last());
			truncatedRoute.Value.Nodes.Single(child => child.Key == "LastIssuedDestination")
				.Value.Value = FieldSaver.FormatValue(new CPos(4, 0));
			var rebuilt = Behavior(input.Handoff, cache, Live(new CPos(0, 0)),
				out _, out _, out _, out var rebuiltOrders);
			rebuilt.RestorePrivateState(truncatedRoute);
			var rebuiltResult = rebuilt.Execute();
			Assert.That(rebuiltOrders.Orders, Has.Count.EqualTo(1));
			Assert.That(rebuiltOrders.Orders[0].Destination, Is.EqualTo(new CPos(1, 0)));
			Assert.That(rebuiltResult.Route, Is.EqualTo(new[]
			{
				new CPos(1, 0), new CPos(2, 0), new CPos(3, 0), new CPos(4, 0)
			}));

			var absentDestination = MiniYaml.FromString(new List<MiniYamlNode>
			{
				Behavior(input.Handoff, cache, Live(new CPos(0, 0)),
					out _, out _, out _, out _).SerializePrivateState()
			}.WriteToString()).Single();
			absentDestination.Value.Nodes.Single(child => child.Key == "LastIssuedDestination")
				.Value.Value = FieldSaver.FormatValue(new CPos(1, 0));
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(absentDestination));

			var duplicateRoute = MiniYaml.FromString(serialized).Single();
			var duplicateCells = duplicateRoute.Value.Nodes.Where(child => child.Key == "RouteCell").ToArray();
			var duplicateAt = duplicateRoute.Value.Nodes.IndexOf(duplicateCells[2]);
			duplicateRoute.Value.Nodes.Insert(duplicateAt, duplicateCells[1].Clone());
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(duplicateRoute));

			var cyclicRoute = MiniYaml.FromString(serialized).Single();
			var cyclicCells = cyclicRoute.Value.Nodes.Where(child => child.Key == "RouteCell").ToArray();
			var firstRouteAt = cyclicRoute.Value.Nodes.IndexOf(cyclicCells[0]);
			cyclicRoute.Value.Nodes.RemoveAll(child => child.Key == "RouteCell");
			cyclicRoute.Value.Nodes.InsertRange(firstRouteAt, new[]
			{
				cyclicCells[0].Clone(), cyclicCells[1].Clone(), cyclicCells[0].Clone(),
				cyclicCells[1].Clone(), cyclicCells[2].Clone(), cyclicCells[3].Clone()
			});
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(cyclicRoute));

			var detourCache = Cache(6, 2, cell => cell == new CPos(1, 0) ?
				new StealthApproachStrategicCellSnapshot(cell, new[] { Enemy() }, true) :
				new StealthApproachStrategicCellSnapshot(cell, NoEnemies, false));
			var detour = Behavior(input.Handoff, detourCache, Live(new CPos(0, 0)),
				out _, out _, out _, out _);
			detour.Execute();
			var diagonalTruncation = MiniYaml.FromString(new List<MiniYamlNode>
			{
				detour.SerializePrivateState()
			}.WriteToString()).Single();
			var detourCells = diagonalTruncation.Value.Nodes.Where(child => child.Key == "RouteCell").ToArray();
			Assert.That(FieldLoader.GetValue<CPos>("Cell", detourCells[0].Value.Nodes
				.Single(field => field.Key == "Cell").Value.Value), Is.EqualTo(new CPos(0, 1)));
			Assert.That(FieldLoader.GetValue<CPos>("Cell", detourCells[1].Value.Nodes
				.Single(field => field.Key == "Cell").Value.Value), Is.EqualTo(new CPos(1, 1)));
			diagonalTruncation.Value.Nodes.Remove(detourCells[0]);
			diagonalTruncation.Value.Nodes.Single(child => child.Key == "LastIssuedDestination")
				.Value.Value = FieldSaver.FormatValue(new CPos(1, 1));
			var diagonalRebuilt = Behavior(input.Handoff, detourCache, Live(new CPos(0, 0)),
				out _, out _, out _, out var diagonalOrders);
			diagonalRebuilt.RestorePrivateState(diagonalTruncation);
			diagonalRebuilt.Execute();
			Assert.That(diagonalOrders.Orders, Has.Count.EqualTo(1));
			Assert.That(diagonalOrders.Orders[0].Destination, Is.EqualTo(new CPos(0, 1)));
		}
	}
}
