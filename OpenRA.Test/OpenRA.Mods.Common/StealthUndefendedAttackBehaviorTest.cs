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
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class StealthUndefendedAttackBehaviorTest
	{
		sealed class Input
		{
			public StealthLifecycleController Controller { get; set; }
			public StealthUndefendedAttackHandoff Handoff { get; set; }
			public StealthApproachMission Mission => Handoff.Mission;
		}

		sealed class LiveProbe : IStealthUndefendedAttackLiveWorld
		{
			public StealthUndefendedAttackLiveSnapshot Snapshot { get; set; }
			public int Reads { get; private set; }
			public Action OnRead { get; set; }
			public LiveProbe(StealthUndefendedAttackLiveSnapshot snapshot) { Snapshot = snapshot; }
			public StealthUndefendedAttackLiveSnapshot Read(StealthApproachMission mission)
			{
				Reads++;
				OnRead?.Invoke();
				return Snapshot;
			}
		}

		sealed class ThreatProbe : IStealthUndefendedAttackThreatAdapter
		{
			public readonly List<StealthUndefendedAttackThreatFacts> Facts =
				new List<StealthUndefendedAttackThreatFacts>();
			public StealthUndefendedAttackSafetyResult Result { get; set; } = Safe();
			public Action OnCalculate { get; set; }
			public StealthUndefendedAttackSafetyResult Calculate(
				StealthUndefendedAttackThreatFacts facts)
			{
				Facts.Add(facts);
				OnCalculate?.Invoke();
				return Result;
			}
		}

		sealed class OrderProbe : IStealthUndefendedAttackOrders
		{
			public readonly List<(BehaviorId Owner, OwnershipEpoch Epoch,
				uint[] ActorIds, uint TargetActorId)> Orders =
				new List<(BehaviorId, OwnershipEpoch, uint[], uint)>();
			public Action OnIssue { get; set; }
			public bool AttemptActorMutation { get; set; }
			public bool MutationDuringIssueSucceeded { get; private set; }
			public IReadOnlyList<uint> RetainedActorIds { get; private set; }
			public void IssueAttack(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint targetActorId)
			{
				RetainedActorIds = actorIds;
				if (AttemptActorMutation)
					MutationDuringIssueSucceeded = TryMutateRetained(999);

				Orders.Add((owner, epoch, actorIds.ToArray(), targetActorId));
				OnIssue?.Invoke();
			}

			public bool TryMutateRetained(uint actorId)
			{
				if (!(RetainedActorIds is IList<uint> mutable) || mutable.Count == 0)
					return false;

				try
				{
					mutable[0] = actorId;
					return true;
				}
				catch (NotSupportedException)
				{
					return false;
				}
			}
		}

		sealed class AcquisitionCache : IStealthTargetAcquisitionCache
		{
			readonly StealthTargetAcquisitionCacheSnapshot snapshot;
			public AcquisitionCache(StealthTargetAcquisitionCacheSnapshot snapshot) { this.snapshot = snapshot; }
			public StealthTargetAcquisitionCacheSnapshot ReadSnapshot() { return snapshot; }
		}

		sealed class ApproachCache : IStealthApproachStrategicCache
		{
			public StealthApproachStrategicCacheSnapshot ReadSnapshot()
			{
				throw new InvalidOperationException("Arrival must not read the strategic cache.");
			}
		}

		sealed class ApproachLive : IStealthApproachLiveWorld
		{
			public StealthApproachLiveSnapshot Read(StealthApproachMission mission)
			{
				return new StealthApproachLiveSnapshot(true,
					new[] { new StealthApproachMemberSnapshot(1, mission.StrategicCell) },
					new[] { new StealthCombatGroupSnapshot("stnk", 1, 900) },
					Array.Empty<StealthCombatGroupSnapshot>(), Array.Empty<uint>(), true, false, true);
			}
		}

		sealed class StandardThreat : IStealthTargetThreatAdapter
		{
			public StealthTargetThreatScore Calculate(StealthTargetThreatFacts facts)
			{
				return new StealthTargetThreatScore(0, double.PositiveInfinity);
			}
		}

		sealed class NoMovement : IStealthApproachMovementOrders
		{
			public void IssueMove(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, CPos destinationStrategicCell)
			{
				throw new InvalidOperationException("Arrival must not issue movement.");
			}
		}

		static StealthUndefendedAttackSafetyResult Safe()
		{
			return new StealthUndefendedAttackSafetyResult(
				new StealthTargetThreatScore(0, double.PositiveInfinity), true, false);
		}

		static Input CreateInput(long epoch = 8)
		{
			var cell = new CPos(4, 4);
			var facts = new StealthTargetThreatFacts(cell,
				new[] { new StealthCombatGroupSnapshot("stnk", 2, 900) },
				Array.Empty<StealthCombatGroupSnapshot>(), true, false, true);
			var fillerCells = Enumerable.Range(0, 9).Select(index => new CPos(10 + index, 1)).ToArray();
			var cells = new[] { cell }.Concat(fillerCells).ToArray();
			var targets = new[] { new StealthStrategicTargetSnapshot(99, cell, 100, 1000, 100, 100) }
				.Concat(fillerCells.Select((filler, index) => new StealthStrategicTargetSnapshot(
					(uint)(100 + index), filler, 1, 1, 100, 100))).ToArray();
			var threatFacts = new[] { facts }.Concat(fillerCells.Select(filler =>
				new StealthTargetThreatFacts(filler, facts.FriendlyGroup,
					Array.Empty<StealthCombatGroupSnapshot>(), true, false, true))).ToArray();
			var controller = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.TargetAcquisition, new OwnershipEpoch(epoch - 5), -1));
			var acquisition = new StealthTargetAcquisitionBehavior(controller.CurrentHandoff,
				new AcquisitionCache(new StealthTargetAcquisitionCacheSnapshot(40, 20,
					Enumerable.Repeat(0f, 800), cells, 1, targets, threatFacts)))
				.Execute(new CPos(0, 0), null);
			Assert.That(controller.TryAccept(acquisition, out var valueHandoff), Is.True);
			var value = new StealthTargetValueFilterBehavior(valueHandoff).Execute();
			Assert.That(controller.TryAccept(value, out var threatHandoff), Is.True);
			var threat = new StealthTargetThreatFilterBehavior(threatHandoff,
				new StandardThreat()).Execute();
			Assert.That(controller.TryAccept(threat, out var distanceHandoff), Is.True);
			var distance = new StealthTargetDistanceChoiceBehavior(distanceHandoff,
				Array.Empty<StealthActiveSquadTargetSnapshot>(),
				new StealthTargetDistanceChoicePolicy(1000, 3000)).Execute();
			Assert.That(controller.TryAccept(distance, out var approachHandoff), Is.True);
			var approach = new StealthApproachBehavior(approachHandoff,
				new ApproachCache(), new ApproachLive(), new StandardThreat(), new NoMovement()).Execute();
			Assert.That(controller.TryAccept(approach, out var transition), Is.True);
			return new Input
			{
				Controller = controller,
				Handoff = transition.UndefendedAttack
			};
		}

		static StealthUndefendedAttackMemberSnapshot Member(uint id = 1,
			CPos? cell = null, int range = 3)
		{
			return new StealthUndefendedAttackMemberSnapshot(
				id, "stnk", 900, cell ?? new CPos(4, 4), 100, 100, range);
		}

		static StealthUndefendedAttackTargetSnapshot Target(uint id,
			int priority = 100, int value = 1000, int hp = 100, int maxHp = 100,
			CPos? current = null, bool inWorld = true, bool dead = false,
			bool targetable = true, CPos? strategic = null, string type = "fact")
		{
			return new StealthUndefendedAttackTargetSnapshot(id, type,
				strategic ?? new CPos(4, 4), current ?? new CPos(4, 4), priority,
				value, hp, maxHp, inWorld, dead, targetable);
		}

		static StealthUndefendedAttackLiveSnapshot Live(int tick,
			IEnumerable<StealthUndefendedAttackTargetSnapshot> targets,
			IEnumerable<StealthUndefendedAttackMemberSnapshot> members = null,
			IEnumerable<uint> defenders = null, bool cloaked = true,
			bool detector = false, bool plannedReveal = true)
		{
			return new StealthUndefendedAttackLiveSnapshot(tick,
				members ?? new[] { Member() }, targets, defenders ?? Array.Empty<uint>(),
				cloaked, detector, plannedReveal);
		}

		static StealthUndefendedAttackBehavior Behavior(Input input,
			StealthUndefendedAttackLiveSnapshot live, out LiveProbe liveProbe,
			out ThreatProbe threatProbe, out OrderProbe orderProbe)
		{
			liveProbe = new LiveProbe(live);
			threatProbe = new ThreatProbe();
			orderProbe = new OrderProbe();
			return new StealthUndefendedAttackBehavior(input.Handoff,
				input.Controller, liveProbe, threatProbe, orderProbe);
		}

		[Test]
		public void ConfiguredPriorityWinsBeforeEstablishedRemainingHpValueAndWalls()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[]
			{
				Target(10, priority: 1, value: 100000, hp: 1, type: "wall"),
				Target(20, priority: 100, value: 1000, hp: 100, type: "fact"),
				Target(30, priority: 100, value: 1000, hp: 25, type: "proc")
			}), out _, out _, out var orders);

			var result = behavior.Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(30),
				"Equal configured priorities use the established remaining-HP value policy.");
			Assert.That(orders.Orders.Single().TargetActorId, Is.EqualTo(30));
			Assert.That(orders.Orders.Single().TargetActorId, Is.Not.EqualTo(10),
				"A wall cannot outrank a configured objective merely by value or low HP.");
		}

		[Test]
		public void EqualConfiguredPriorityUsesLiveActorValue()
		{
			var input = CreateInput();
			var result = Behavior(input, Live(0, new[]
			{
				Target(10, priority: 100, value: 1000),
				Target(20, priority: 100, value: 2000)
			}), out _, out _, out _).Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(20));
		}

		[Test]
		public void RetainsTargetAcrossTicksAndRefreshesOnlyAtFiveSeconds()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Target(10), Target(20, priority: 50) }),
				out var live, out _, out var orders);

			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(10));
			live.Snapshot = Live(1, new[] { Target(10), Target(20, priority: 1000) });
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(10));
			live.Snapshot = Live(StealthUndefendedAttackBehavior.RefreshIntervalTicks,
				new[] { Target(10), Target(20, priority: 1000) });
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(20));
			Assert.That(orders.Orders.Select(order => order.TargetActorId), Is.EqualTo(new uint[] { 10, 20 }));
		}

		[TestCase(false, false, true)]
		[TestCase(true, true, true)]
		[TestCase(true, false, false)]
		public void KilledInvalidOrUntargetableTargetReevaluatesImmediately(
			bool inWorld, bool dead, bool targetable)
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Target(10), Target(20, priority: 50) }),
				out var live, out _, out var orders);
			behavior.Execute();
			live.Snapshot = Live(1, new[]
			{
				Target(10, inWorld: inWorld, dead: dead, targetable: targetable), Target(20, priority: 50)
			});

			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(20));
			Assert.That(orders.Orders.Last().TargetActorId, Is.EqualTo(20));
		}

		[Test]
		public void EmptyCellReacquiresAndLiveDefenderTransfersUnchangedMissionToCrush()
		{
			var emptyInput = CreateInput();
			var empty = Behavior(emptyInput, Live(0, Array.Empty<StealthUndefendedAttackTargetSnapshot>()),
				out _, out var emptyThreat, out var emptyOrders).Execute();
			Assert.That(empty.Disposition, Is.EqualTo(StealthUndefendedAttackDisposition.Reacquire));
			Assert.That(emptyThreat.Facts, Is.Empty);
			Assert.That(emptyOrders.Orders, Is.Empty);
			Assert.That(emptyInput.Controller.TryAccept(empty, out var reacquire), Is.True);
			Assert.That(reacquire.Reacquisition.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));

			var defendedInput = CreateInput();
			var defendedBehavior = Behavior(defendedInput,
				Live(0, new[] { Target(10) }, defenders: new uint[] { 77 }),
				out var defendedLive, out var defendedThreat, out var defendedOrders);
			var defended = defendedBehavior.Execute();
			Assert.That(defended.Disposition, Is.EqualTo(StealthUndefendedAttackDisposition.CrushEvaluation));
			Assert.That(defendedThreat.Facts, Is.Empty);
			Assert.That(defendedOrders.Orders, Is.Empty);
			Assert.That(defendedInput.Controller.TryAccept(defended, out var crush), Is.True);
			Assert.That(crush.CrushEvaluation.Mission, Is.SameAs(defendedInput.Mission));
			Assert.That(crush.CrushEvaluation.LiveDefenderActorIds, Is.EqualTo(new uint[] { 77 }));
			Assert.Throws<InvalidOperationException>(() => defendedBehavior.Execute());
			Assert.That(defendedLive.Reads, Is.EqualTo(1));
			Assert.That(defendedThreat.Facts, Is.Empty);
			Assert.That(defendedOrders.Orders, Is.Empty,
				"A stale pre-Crush owner cannot issue orders after the controller transition.");
		}

		[Test]
		public void ActiveOwnerUsesCurrentRangeCloakRevealAndSafelyClosesFromJustOutOfRange()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(10,
				new[] { Target(10, current: new CPos(4, 4)) },
				new[] { Member(1, new CPos(0, 4), range: 3) },
				cloaked: true, detector: false, plannedReveal: true),
				out _, out var threats, out var orders);

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthUndefendedAttackDisposition.Retain));
			Assert.That(threats.Facts.Single().PlannedCurrentRangeEngagement, Is.True);
			Assert.That(threats.Facts.Single().AnyMemberCurrentlyInRange, Is.False);
			Assert.That(threats.Facts.Single().FormationCloaked, Is.True);
			Assert.That(threats.Facts.Single().HasDetectorCoverage, Is.False);
			Assert.That(threats.Facts.Single().PlannedActionRevealsFormation, Is.True);
			Assert.That(orders.Orders.Single().TargetActorId, Is.EqualTo(10),
				"The owner-bound Attack order safely closes only to its retained firing target.");
		}

		[Test]
		public void ExplicitSafetyReacquisitionOwnsTheTransitionAndIssuesNoAttack()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Target(10) }),
				out var live, out var threats, out var orders);
			threats.Result = new StealthUndefendedAttackSafetyResult(
				new StealthTargetThreatScore(5, 0.5), false, true);
			input.Controller.Observe(new StealthLifecycleObservationFrame(50, new[]
			{
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Timer),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.WorldEvent)
			}));

			var result = behavior.Execute();

			Assert.That(input.Controller.Owner, Is.EqualTo(BehaviorId.UndefendedAttack),
				"Observations cannot interrupt the active owner.");
			Assert.That(result.Disposition, Is.EqualTo(StealthUndefendedAttackDisposition.Reacquire));
			Assert.That(orders.Orders, Is.Empty);
			Assert.That(input.Controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.Reacquisition.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
			Assert.That(input.Controller.TryAccept(result, out _), Is.False,
				"A result from the previous owner/epoch cannot transition twice.");
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(live.Reads, Is.EqualTo(1));
			Assert.That(threats.Facts, Has.Count.EqualTo(1));
			Assert.That(orders.Orders, Is.Empty,
				"A stale pre-reacquisition owner cannot issue orders after the controller transition.");
		}

		[Test]
		public void ReentrantLiveReadTransitionCannotReachSafetyOrderOrOwnerState()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Target(10) }),
				out var live, out var threats, out var orders);
			threats.Result = new StealthUndefendedAttackSafetyResult(
				new StealthTargetThreatScore(5, 0.5), false, true);
			var transitionResult = behavior.Execute();
			var before = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var accepted = false;
			live.OnRead = () => accepted = input.Controller.TryAccept(transitionResult, out _);

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());

			Assert.That(accepted, Is.True);
			Assert.That(input.Controller.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
			Assert.That(live.Reads, Is.EqualTo(2));
			Assert.That(threats.Facts, Has.Count.EqualTo(1),
				"An owner invalidated by its live callback cannot reach safety.");
			Assert.That(orders.Orders, Is.Empty);
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));
		}

		[Test]
		public void ReentrantThreatTransitionCannotReachOrderOrOwnerStateCommit()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Target(10) }),
				out var live, out var threats, out var orders);
			threats.Result = new StealthUndefendedAttackSafetyResult(
				new StealthTargetThreatScore(5, 0.5), false, true);
			var transitionResult = behavior.Execute();
			var before = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var accepted = false;
			threats.Result = Safe();
			threats.OnCalculate = () => accepted = input.Controller.TryAccept(transitionResult, out _);

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());

			Assert.That(accepted, Is.True);
			Assert.That(input.Controller.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
			Assert.That(live.Reads, Is.EqualTo(2));
			Assert.That(threats.Facts, Has.Count.EqualTo(2));
			Assert.That(orders.Orders, Is.Empty,
				"An owner invalidated by its threat callback cannot issue its attack.");
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));
		}

		[Test]
		public void ReentrantRefreshRetargetThreatTransitionDiscardsProspectiveState()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0,
				new[] { Target(10), Target(20, priority: 50) }),
				out var live, out var threats, out var orders);
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(10));
			var before = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();

			var transitionSource = Behavior(input, Live(0, new[] { Target(10) }),
				out _, out var sourceThreats, out _);
			sourceThreats.Result = new StealthUndefendedAttackSafetyResult(
				new StealthTargetThreatScore(5, 0.5), false, true);
			var transitionResult = transitionSource.Execute();
			var accepted = false;
			live.Snapshot = Live(StealthUndefendedAttackBehavior.RefreshIntervalTicks,
				new[] { Target(10), Target(20, priority: 1000) });
			threats.OnCalculate = () => accepted = input.Controller.TryAccept(transitionResult, out _);

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());

			Assert.That(accepted, Is.True);
			Assert.That(threats.Facts, Has.Count.EqualTo(2));
			Assert.That(orders.Orders.Select(order => order.TargetActorId), Is.EqualTo(new uint[] { 10 }));
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before), "A reentrant refresh/retarget must discard all prospective state.");
		}

		[Test]
		public void ReentrantAttackOrderTransitionInvokesOnceAndCommitsNoOwnerState()
		{
			var input = CreateInput();
			var transitionSource = Behavior(input, Live(0, new[] { Target(10) }),
				out _, out var sourceThreats, out _);
			sourceThreats.Result = new StealthUndefendedAttackSafetyResult(
				new StealthTargetThreatScore(5, 0.5), false, true);
			var transitionResult = transitionSource.Execute();
			var behavior = Behavior(input, Live(0, new[] { Target(10) }),
				out var live, out var threats, out var orders);
			var before = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var accepted = false;
			orders.OnIssue = () => accepted = input.Controller.TryAccept(transitionResult, out _);

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());

			Assert.That(accepted, Is.True);
			Assert.That(orders.Orders, Has.Count.EqualTo(1),
				"The already-entered order callback is unavoidable, but must occur only once.");
			Assert.That(orders.Orders[0].TargetActorId, Is.EqualTo(10));
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before), "An invalidated order callback cannot commit its deduplication latch.");

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(live.Reads, Is.EqualTo(1));
			Assert.That(threats.Facts, Has.Count.EqualTo(1));
			Assert.That(orders.Orders, Has.Count.EqualTo(1),
				"The stale owner cannot perform a subsequent action.");
		}

		[Test]
		public void UsefulAttackOrderIsDeduplicatedAndOwnerEpochBounded()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Target(10) },
				new[] { Member(2), Member(1) }), out var live, out _, out var orders);
			var first = behavior.Execute();
			live.Snapshot = Live(1, new[] { Target(10) }, new[] { Member(2), Member(1) });
			var second = behavior.Execute();

			Assert.That(orders.Orders, Has.Count.EqualTo(1));
			Assert.That(orders.Orders[0].Owner, Is.EqualTo(BehaviorId.UndefendedAttack));
			Assert.That(orders.Orders[0].Epoch, Is.EqualTo(new OwnershipEpoch(8)));
			Assert.That(orders.Orders[0].ActorIds, Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(input.Controller.TryAccept(first, out var retained), Is.True);
			Assert.That(retained.Retained.Owner, Is.EqualTo(BehaviorId.UndefendedAttack));
			Assert.That(input.Controller.Epoch, Is.EqualTo(new OwnershipEpoch(8)));
			Assert.That(input.Controller.TryAccept(second, out _), Is.True);
		}

		[Test]
		public void HostileOrderCallbackCannotAliasCommittedDeduplicationState()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Target(10) },
				new[] { Member(2), Member(1) }), out var live, out _, out var orders);
			orders.AttemptActorMutation = true;

			behavior.Execute();

			Assert.That(orders.MutationDuringIssueSucceeded, Is.False);
			Assert.That(orders.RetainedActorIds is uint[], Is.False,
				"The callback must not receive the mutable array owned by behavior state.");
			Assert.That(orders.Orders.Single().ActorIds, Is.EqualTo(new uint[] { 1, 2 }));
			var committed = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			Assert.That(orders.TryMutateRetained(999), Is.False,
				"A callback retaining its list must not mutate it after the owner commits.");

			live.Snapshot = Live(1, new[] { Target(10) }, new[] { Member(2), Member(1) });
			behavior.Execute();

			Assert.That(orders.Orders, Has.Count.EqualTo(1),
				"External mutation attempts must not corrupt the committed order-deduplication state.");
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(committed));
			Assert.That(committed, Does.Contain("LastIssuedActorId: 1"));
			Assert.That(committed, Does.Contain("LastIssuedActorId: 2"));
			Assert.That(committed, Does.Not.Contain("LastIssuedActorId: 999"));
		}

		[Test]
		public void PrivateStateRoundTripsAndRejectsOwnershipMissionTargetAndOrderTampering()
		{
			var input = CreateInput();
			var snapshot = Live(0, new[] { Target(10), Target(20, priority: 50) });
			var behavior = Behavior(input, snapshot, out _, out _, out _);
			behavior.Execute();
			var serialized = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input, snapshot, out _, out _, out var restoredOrders);

			restored.RestorePrivateState(MiniYaml.FromString(serialized).Single());
			restored.Execute();

			Assert.That(restoredOrders.Orders, Is.Empty);
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(serialized));
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("Owner: UndefendedAttack", "Owner: Approach")).Single()));
			Assert.Throws<InvalidOperationException>(() => Behavior(CreateInput(9), snapshot,
				out _, out _, out _).RestorePrivateState(MiniYaml.FromString(serialized).Single()));
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("StrategicCell: 4,4", "StrategicCell: 5,4")).Single()));
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("SelectedTargetActorId: 10", "SelectedTargetActorId: 999")).Single()));
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("SelectedTargetActorId: 10", "SelectedTargetActorId: 999")
					.Replace("LastIssuedTargetActorId: 10", "LastIssuedTargetActorId: 999")).Single()));
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("LastIssuedTargetActorId: 10", "LastIssuedTargetActorId: 20")).Single()));
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("LastIssuedActorId: 1", "LastIssuedActorId: 999")).Single()));
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("NextRefreshTick: 125", "NextRefreshTick: 124")).Single()));
		}

		[Test]
		public void RestoreRejectsFutureRefreshPairAndAcceptsCurrentOrOverdueCadence()
		{
			var input = CreateInput();
			var snapshot = Live(0, new[] { Target(10) });
			var source = Behavior(input, snapshot, out _, out _, out _);
			source.Execute();
			var serialized = new List<MiniYamlNode> { source.SerializePrivateState() }.WriteToString();
			var future = MiniYaml.FromString(serialized).Single();
			future.Value.Nodes.Single(child => child.Key == "LastRefreshTick").Value.Value = "1";
			future.Value.Nodes.Single(child => child.Key == "NextRefreshTick").Value.Value = "126";
			var rejected = Behavior(input, snapshot,
				out var rejectedLive, out var rejectedThreats, out var rejectedOrders);
			var before = new List<MiniYamlNode> { rejected.SerializePrivateState() }.WriteToString();

			Assert.Throws<InvalidOperationException>(() => rejected.RestorePrivateState(future),
				"A structurally paired refresh timestamp cannot begin after the current live tick.");
			Assert.That(rejectedLive.Reads, Is.EqualTo(1));
			Assert.That(rejectedThreats.Facts, Is.Empty);
			Assert.That(rejectedOrders.Orders, Is.Empty);
			Assert.That(new List<MiniYamlNode> { rejected.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));

			var current = Behavior(input, snapshot, out _, out _, out _);
			current.RestorePrivateState(MiniYaml.FromString(serialized).Single());
			Assert.That(new List<MiniYamlNode> { current.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(serialized), "A refresh captured at the current live tick remains valid.");

			var overdue = Behavior(input, Live(126, new[] { Target(10) }),
				out _, out var overdueThreats, out var overdueOrders);
			overdue.RestorePrivateState(MiniYaml.FromString(serialized).Single());
			Assert.That(new List<MiniYamlNode> { overdue.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(serialized), "An elapsed cadence is valid and remains due for owner refresh.");

			overdue.Execute();

			var refreshed = new List<MiniYamlNode> { overdue.SerializePrivateState() }.WriteToString();
			Assert.That(overdueThreats.Facts, Has.Count.EqualTo(2));
			Assert.That(overdueOrders.Orders, Is.Empty);
			Assert.That(refreshed, Does.Contain("LastRefreshTick: 126"));
			Assert.That(refreshed, Does.Contain("NextRefreshTick: 251"));
		}

		[Test]
		public void PrivateStateRejectsForgedThreatRatingAndDecisionContext()
		{
			var input = CreateInput();
			var snapshot = Live(0, new[] { Target(10) });
			var behavior = Behavior(input, snapshot, out _, out _, out _);
			behavior.Execute();
			var serialized = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input, snapshot, out _, out var restoredThreats, out _);
			var forgedRating = MiniYaml.FromString(serialized).Single();
			forgedRating.Value.Nodes.Single(child => child.Key == "ThreatRating").Value.Value = "1";

			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(forgedRating),
				"Saved threat must be recomputed from the current standard live facts.");
			Assert.That(restoredThreats.Facts, Has.Count.EqualTo(1));
			var forgedDecision = MiniYaml.FromString(serialized).Single();
			forgedDecision.Value.Nodes.Single(child => child.Key == "SafetyApproved").Value.Value = "False";
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(forgedDecision),
				"A saved approval decision cannot contradict its retained attack-order context.");
		}

		[Test]
		public void EmptyReacquisitionRestoreRequiresACurrentlyEmptyMissionCell()
		{
			var input = CreateInput();
			var empty = Behavior(input, Live(0, Array.Empty<StealthUndefendedAttackTargetSnapshot>()),
				out _, out _, out _);
			empty.Execute();
			var serialized = new List<MiniYamlNode> { empty.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input, Live(1, new[] { Target(10) }),
				out var restoredLive, out var restoredThreats, out var restoredOrders);

			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(
				MiniYaml.FromString(serialized).Single()));
			Assert.That(restoredLive.Reads, Is.EqualTo(1));
			Assert.That(restoredThreats.Facts, Is.Empty);
			Assert.That(restoredOrders.Orders, Is.Empty);
		}

		[Test]
		public void ReentrantRestoreThreatTransitionCannotCommitValidatedState()
		{
			var input = CreateInput();
			var snapshot = Live(0, new[] { Target(10) });
			var source = Behavior(input, snapshot, out _, out var sourceThreats, out _);
			var unsafeSafety = new StealthUndefendedAttackSafetyResult(
				new StealthTargetThreatScore(5, 0.5), false, true);
			sourceThreats.Result = unsafeSafety;
			var transitionResult = source.Execute();
			var saved = source.SerializePrivateState();
			var restored = Behavior(input, snapshot,
				out var restoredLive, out var restoredThreats, out var restoredOrders);
			restoredThreats.Result = unsafeSafety;
			var before = new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString();
			var accepted = false;
			restoredThreats.OnCalculate = () =>
				accepted = input.Controller.TryAccept(transitionResult, out _);

			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(saved));

			Assert.That(accepted, Is.True);
			Assert.That(restoredLive.Reads, Is.EqualTo(1));
			Assert.That(restoredThreats.Facts, Has.Count.EqualTo(1));
			Assert.That(restoredOrders.Orders, Is.Empty);
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before), "Restore must commit only after its final active-owner guard.");
		}

		[Test]
		public void OwnerHasNoStrategicOrLocalActorCacheDependency()
		{
			var fields = typeof(StealthUndefendedAttackBehavior).GetFields(
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			Assert.That(fields.Any(field => field.FieldType.Name.IndexOf(
				"Cache", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Target(10) }),
				out var live, out _, out _);
			behavior.Execute();
			Assert.That(live.Reads, Is.EqualTo(1));
		}
	}
}
