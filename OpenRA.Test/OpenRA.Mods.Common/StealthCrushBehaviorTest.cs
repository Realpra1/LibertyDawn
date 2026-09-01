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
	public sealed class StealthCrushBehaviorTest
	{
		sealed class Input
		{
			public StealthLifecycleController Controller { get; set; }
			public StealthCrushEvaluationHandoff Handoff { get; set; }
			public StealthApproachMission Mission => Handoff.Mission;
		}

		sealed class LiveProbe : IStealthCrushLiveWorld
		{
			public StealthCrushLiveSnapshot Snapshot { get; set; }
			public int Reads { get; private set; }
			public Action OnRead { get; set; }

			public LiveProbe(StealthCrushLiveSnapshot snapshot) { Snapshot = snapshot; }

			public StealthCrushLiveSnapshot Read(StealthApproachMission mission)
			{
				Reads++;
				OnRead?.Invoke();
				return Snapshot;
			}
		}

		sealed class ThreatProbe : IStealthCrushThreatAdapter
		{
			public readonly List<StealthCrushThreatFacts> Facts = new List<StealthCrushThreatFacts>();
			public StealthCrushSafetyResult Result { get; set; } = Safe();
			public Action OnCalculate { get; set; }

			public StealthCrushSafetyResult Calculate(StealthCrushThreatFacts facts)
			{
				Facts.Add(facts);
				OnCalculate?.Invoke();
				return Result;
			}
		}

		sealed class OrderProbe : IStealthCrushOrders
		{
			public readonly List<(BehaviorId Owner, OwnershipEpoch Epoch, uint[] ActorIds,
				uint TargetActorId, CPos TargetCurrentCell)> Orders =
				new List<(BehaviorId, OwnershipEpoch, uint[], uint, CPos)>();
			public Action OnIssue { get; set; }
			public IReadOnlyList<uint> RetainedActorIds { get; private set; }
			public bool MutationDuringIssueSucceeded { get; private set; }
			public bool AttemptMutation { get; set; }

			public void IssueCrush(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint targetActorId, CPos targetCurrentCell)
			{
				RetainedActorIds = actorIds;
				if (AttemptMutation)
					MutationDuringIssueSucceeded = TryMutate(999);
				Orders.Add((owner, epoch, actorIds.ToArray(), targetActorId, targetCurrentCell));
				OnIssue?.Invoke();
			}

			public bool TryMutate(uint actorId)
			{
				if (!(RetainedActorIds is IList<uint> list) || list.Count == 0)
					return false;
				try
				{
					list[0] = actorId;
					return true;
				}
				catch (NotSupportedException)
				{
					return false;
				}
			}
		}

		sealed class OwnershipProbe : IStealthLifecycleOwnershipGuard
		{
			public bool Active { get; set; } = true;
			public bool Throw { get; set; }
			public int Checks { get; private set; }
			public Action OnCheck { get; set; }

			public bool IsActive(BehaviorId owner, OwnershipEpoch epoch)
			{
				Checks++;
				OnCheck?.Invoke();
				if (Throw)
					throw new InvalidOperationException("Injected ownership failure.");
				return Active;
			}
		}

		sealed class AcquisitionCache : IStealthTargetAcquisitionCache
		{
			readonly StealthTargetAcquisitionCacheSnapshot snapshot;
			public AcquisitionCache(StealthTargetAcquisitionCacheSnapshot snapshot) { this.snapshot = snapshot; }
			public StealthTargetAcquisitionCacheSnapshot ReadSnapshot() { return snapshot; }
		}

		sealed class ApproachCache : IStealthApproachStrategicCache,
			IStealthApproachStrategicRouteCache
		{
			public StealthApproachStrategicCacheSnapshot ReadSnapshot()
			{
				throw new InvalidOperationException("Arrival must not read the strategic cache.");
			}

			public IReadOnlyList<CPos> ReadRoute(CPos origin, CPos destination)
			{
				throw new InvalidOperationException("Arrival must not read the strategic cache.");
			}
		}

		sealed class ApproachLive : IStealthApproachLiveWorld
		{
			readonly bool defended;
			public ApproachLive(bool defended) { this.defended = defended; }

			public StealthApproachLiveSnapshot Read(StealthApproachMission mission)
			{
				return new StealthApproachLiveSnapshot(true,
					new[] { new StealthApproachMemberSnapshot(1, mission.StrategicCell) },
					new[] { new StealthCombatGroupSnapshot("stnk", 1, 900) },
					Array.Empty<StealthCombatGroupSnapshot>(),
					defended ? new uint[] { 71 } : Array.Empty<uint>(), true, false, false);
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
				throw new InvalidOperationException("Arrival must not move.");
			}
		}

		sealed class UndefLive : IStealthUndefendedAttackLiveWorld
		{
			public StealthUndefendedAttackLiveSnapshot Read(StealthApproachMission mission)
			{
				return new StealthUndefendedAttackLiveSnapshot(0,
					new[]
					{
						new StealthUndefendedAttackMemberSnapshot(
							1, "stnk", 900, mission.StrategicCell, 100, 100, 3)
					},
					new[]
					{
						new StealthUndefendedAttackTargetSnapshot(
							99, "fact", mission.StrategicCell, mission.StrategicCell,
							100, 1000, 100, 100)
					}, new uint[] { 71 }, true, false, true);
			}
		}

		sealed class UndefThreat : IStealthUndefendedAttackThreatAdapter
		{
			public StealthUndefendedAttackSafetyResult Calculate(StealthUndefendedAttackThreatFacts facts)
			{
				return new StealthUndefendedAttackSafetyResult(
					new StealthTargetThreatScore(0, double.PositiveInfinity), true, false);
			}
		}

		sealed class NoAttack : IStealthUndefendedAttackOrders
		{
			public void IssueAttack(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint targetActorId)
			{
				throw new InvalidOperationException("Defended classification must transfer before attacking.");
			}
		}

		static StealthCrushSafetyResult Safe()
		{
			return new StealthCrushSafetyResult(
				new StealthTargetThreatScore(0, double.PositiveInfinity), true);
		}

		static StealthCrushSafetyResult Unsafe()
		{
			return new StealthCrushSafetyResult(new StealthTargetThreatScore(2, 0.5), false);
		}

		static Input CreateInput(bool throughUndefended = false, long startingEpoch = 2)
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
				BehaviorId.TargetAcquisition, new OwnershipEpoch(startingEpoch), -1));
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
			var approach = new StealthApproachBehavior(approachHandoff, new ApproachCache(),
				new ApproachLive(!throughUndefended), new StandardThreat(), new NoMovement()).Execute();
			Assert.That(controller.TryAccept(approach, out var approachTransition), Is.True);
			if (!throughUndefended)
				return new Input { Controller = controller, Handoff = approachTransition.CrushEvaluation };

			var undefended = new StealthUndefendedAttackBehavior(approachTransition.UndefendedAttack,
				controller, new UndefLive(), new UndefThreat(), new NoAttack()).Execute();
			Assert.That(controller.TryAccept(undefended, out var undefendedTransition), Is.True);
			return new Input { Controller = controller, Handoff = undefendedTransition.CrushEvaluation };
		}

		static StealthCrushMemberSnapshot Member(uint id = 1, CPos? cell = null,
			bool inWorld = true, bool dead = false)
		{
			return new StealthCrushMemberSnapshot(id, cell ?? new CPos(4, 4), inWorld, dead);
		}

		static StealthCrushActorSnapshot Actor(uint id, int priority = 100,
			CPos? current = null, bool defender = true, bool objective = false,
			bool infantry = true, bool crushable = true, bool detector = false,
			bool inWorld = true, bool dead = false, bool targetable = true,
			CPos? strategic = null, string type = "e1")
		{
			return new StealthCrushActorSnapshot(id, type, strategic ?? new CPos(4, 4),
				current ?? new CPos(4, 4), priority, defender, objective, infantry,
				crushable, detector, inWorld, dead, targetable);
		}

		static StealthCrushLiveSnapshot Live(int tick,
			IEnumerable<StealthCrushActorSnapshot> actors,
			IEnumerable<StealthCrushMemberSnapshot> members = null, bool cloaked = true)
		{
			return new StealthCrushLiveSnapshot(tick,
				members ?? new[] { Member() }, actors, cloaked);
		}

		static StealthCrushBehavior Behavior(Input input, StealthCrushLiveSnapshot snapshot,
			out LiveProbe live, out ThreatProbe threats, out OrderProbe orders,
			IStealthLifecycleOwnershipGuard ownershipGuard = null)
		{
			live = new LiveProbe(snapshot);
			threats = new ThreatProbe();
			orders = new OrderProbe();
			return new StealthCrushBehavior(input.Handoff,
				ownershipGuard ?? input.Controller, live, threats, orders);
		}

		static void SetExecutionRevision(StealthCrushBehavior behavior, long revision)
		{
			var leaseField = typeof(StealthCrushBehavior).GetField("executionLease",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(leaseField, Is.Not.Null);
			var lease = leaseField.GetValue(behavior);
			var revisionField = lease.GetType().GetField("revision",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(revisionField, Is.Not.Null);
			revisionField.SetValue(lease, revision);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void AcceptsImmutableMissionFromBothDefendedPredecessors(bool throughUndefended)
		{
			var input = CreateInput(throughUndefended);
			var mission = input.Mission;
			var behavior = Behavior(input, Live(0, new[] { Actor(10) }),
				out _, out _, out _);

			var result = behavior.Execute();

			Assert.That(result.Mission, Is.SameAs(mission));
			Assert.That(input.Handoff.Owner, Is.EqualTo(BehaviorId.CrushEvaluation));
			Assert.That(input.Handoff.LiveDefenderActorIds, Is.EqualTo(new uint[] { 71 }));
			Assert.That(input.Handoff.LiveDefenderActorIds is uint[], Is.False);
		}

		[Test]
		public void CrushUsesCurrentLiveRemainCloakedThreatAndOwnerBoundOrder()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(7, new[]
			{
				Actor(10, current: new CPos(6, 4)),
				Actor(40, defender: true, infantry: false, crushable: false, type: "mtnk")
			}, new[] { Member(2, new CPos(4, 4)), Member(1, new CPos(5, 4)) }),
				out _, out var threats, out var orders);

			var result = behavior.Execute();

			var facts = threats.Facts.Single();
			Assert.That(facts.FriendlyActorIds, Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(facts.EnemyActorIds, Is.EqualTo(new uint[] { 10, 40 }));
			Assert.That(facts.SelectedTargetCurrentCell, Is.EqualTo(new CPos(6, 4)));
			Assert.That(facts.FormationCloaked, Is.True);
			Assert.That(facts.RemainCloakedAction, Is.True);
			Assert.That(facts.PlannedActionRevealsFormation, Is.False);
			Assert.That(result.Disposition, Is.EqualTo(StealthCrushDisposition.Retain));
			Assert.That(orders.Orders.Single().Owner, Is.EqualTo(BehaviorId.CrushEvaluation));
			Assert.That(orders.Orders.Single().Epoch, Is.EqualTo(input.Handoff.Epoch));
			Assert.That(orders.Orders.Single().TargetCurrentCell, Is.EqualTo(new CPos(6, 4)));
		}

		[TestCase("e1", false)]
		[TestCase("hq", true)]
		public void DetectorCoverageRejectsCrushWithoutActorOrHqException(
			string objectiveType, bool includeHqObjective)
		{
			var input = CreateInput();
			var actors = new List<StealthCrushActorSnapshot>
			{
				Actor(10, detector: true)
			};
			if (includeHqObjective)
				actors.Add(Actor(99, defender: false, objective: true, infantry: false,
					crushable: false, type: objectiveType));
			var behavior = Behavior(input, Live(0, actors), out _, out var threats, out var orders);

			var result = behavior.Execute();

			Assert.That(threats.Facts, Has.Count.EqualTo(1),
				"Detection rejection still evaluates the standard live threat context.");
			Assert.That(threats.Facts[0].HasDetectorCoverage, Is.True);
			Assert.That(result.Safety.Value.Approved, Is.False);
			Assert.That(result.Disposition, Is.EqualTo(StealthCrushDisposition.Kite));
			Assert.That(orders.Orders, Is.Empty);
			Assert.That(input.Controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.Kite.Owner, Is.EqualTo(BehaviorId.Kite));
		}

		[Test]
		public void RevealedFormationCannotEnterCrush()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Actor(10) }, cloaked: false),
				out _, out var threats, out var orders);

			var result = behavior.Execute();

			Assert.That(threats.Facts.Single().FormationCloaked, Is.False);
			Assert.That(result.Disposition, Is.EqualTo(StealthCrushDisposition.Kite));
			Assert.That(result.Safety.Value.Approved, Is.False);
			Assert.That(orders.Orders, Is.Empty);
		}

		[Test]
		public void MovingTargetRefreshUsesNewLivePositionWithoutOrderChurn()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Actor(10, current: new CPos(5, 4)) }),
				out var live, out _, out var orders);

			behavior.Execute();
			live.Snapshot = Live(1, new[] { Actor(10, current: new CPos(5, 4)) });
			behavior.Execute();
			live.Snapshot = Live(2, new[] { Actor(10, current: new CPos(7, 4)) });
			behavior.Execute();
			live.Snapshot = Live(3, new[] { Actor(10, current: new CPos(7, 4)) });
			behavior.Execute();

			Assert.That(orders.Orders.Select(order => order.TargetCurrentCell),
				Is.EqualTo(new[] { new CPos(5, 4), new CPos(7, 4) }));
			Assert.That(orders.Orders.All(order => order.TargetActorId == 10), Is.True);
			var saved = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			Assert.That(saved, Does.Contain("SelectedTargetCurrentCell: 7,4"));
			Assert.That(saved, Does.Contain("LastRefreshTick: 2"));
		}

		[Test]
		public void RetainsTargetUntilInvalidOrBoundedRefreshAndThenUsesLivePriorityProximity()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[]
			{
				Actor(10, priority: 100, current: new CPos(8, 4)),
				Actor(20, priority: 100, current: new CPos(5, 4)),
				Actor(30, priority: 50, current: new CPos(4, 4))
			}), out var live, out _, out var orders);

			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(20),
				"Highest live priority is selected before nearest-member proximity.");
			live.Snapshot = Live(1, new[]
			{
				Actor(10, priority: 1000, current: new CPos(4, 4)), Actor(20, current: new CPos(5, 4))
			});
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(20),
				"A still-valid incumbent is retained before the bounded refresh.");
			live.Snapshot = Live(2, new[]
			{
				Actor(10, priority: 1000), Actor(20, inWorld: false)
			});
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(10),
				"Death or invalidation refreshes immediately from current live candidates.");

			live.Snapshot = Live(2 + StealthCrushBehavior.RefreshIntervalTicks, new[]
			{
				Actor(10, priority: 1), Actor(30, priority: 1000)
			});
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(30));
			Assert.That(orders.Orders.Select(order => order.TargetActorId),
				Is.EqualTo(new uint[] { 20, 10, 30 }));
		}

		[TestCase(false, false, true)]
		[TestCase(true, true, true)]
		[TestCase(true, false, false)]
		public void KilledDeadOrUntargetableInfantryRefreshesImmediately(
			bool inWorld, bool dead, bool targetable)
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Actor(10), Actor(20, priority: 50) }),
				out var live, out _, out var orders);
			behavior.Execute();
			live.Snapshot = Live(1, new[]
			{
				Actor(10, inWorld: inWorld, dead: dead, targetable: targetable),
				Actor(20, priority: 50)
			});

			var result = behavior.Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(20));
			Assert.That(orders.Orders.Last().TargetActorId, Is.EqualTo(20));
		}

		[Test]
		public void IndependentSquadsMayTeamUpOnTheSameLiveInfantry()
		{
			var firstInput = CreateInput();
			var secondInput = CreateInput(startingEpoch: 20);
			var first = Behavior(firstInput, Live(0, new[] { Actor(10) }), out _, out _, out var firstOrders);
			var second = Behavior(secondInput, Live(0, new[] { Actor(10) }), out _, out _, out var secondOrders);

			Assert.That(first.Execute().SelectedTargetActorId, Is.EqualTo(10));
			Assert.That(second.Execute().SelectedTargetActorId, Is.EqualTo(10));
			Assert.That(firstOrders.Orders.Single().TargetActorId, Is.EqualTo(10));
			Assert.That(secondOrders.Orders.Single().TargetActorId, Is.EqualTo(10));
		}

		[Test]
		public void AllThreeOutgoingHandoffsUseOnlyCurrentLiveClassification()
		{
			var kiteInput = CreateInput();
			var kite = Behavior(kiteInput, Live(0, new[]
			{
				Actor(40, infantry: false, crushable: false, type: "mtnk")
			}), out _, out var kiteThreats, out var kiteOrders).Execute();
			Assert.That(kite.Disposition, Is.EqualTo(StealthCrushDisposition.Kite));
			Assert.That(kiteThreats.Facts, Is.Empty);
			Assert.That(kiteOrders.Orders, Is.Empty);
			Assert.That(kiteInput.Controller.TryAccept(kite, out var kiteTransition), Is.True);
			Assert.That(kiteTransition.Kite.Mission, Is.SameAs(kiteInput.Mission));

			var undefendedInput = CreateInput();
			var undefended = Behavior(undefendedInput, Live(0, new[]
			{
				Actor(99, defender: false, objective: true, infantry: false,
					crushable: false, type: "hq")
			}), out _, out _, out _).Execute();
			Assert.That(undefended.Disposition, Is.EqualTo(StealthCrushDisposition.UndefendedAttack));
			Assert.That(undefendedInput.Controller.TryAccept(undefended, out var undefendedTransition), Is.True);
			Assert.That(undefendedTransition.UndefendedAttack.Mission, Is.SameAs(undefendedInput.Mission));

			var emptyInput = CreateInput();
			var empty = Behavior(emptyInput, Live(0, Array.Empty<StealthCrushActorSnapshot>()),
				out _, out _, out _).Execute();
			Assert.That(empty.Disposition, Is.EqualTo(StealthCrushDisposition.Reacquire));
			Assert.That(emptyInput.Controller.TryAccept(empty, out var emptyTransition), Is.True);
			Assert.That(emptyTransition.Reacquisition.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
		}

		[Test]
		public void PassiveObservationsCannotStealCrushOwnership()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(50, new[] { Actor(10) }), out _, out _, out var orders);
			input.Controller.Observe(new StealthLifecycleObservationFrame(50, new[]
			{
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Timer),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.WorldEvent, 10)
			}));

			var result = behavior.Execute();

			Assert.That(input.Controller.Owner, Is.EqualTo(BehaviorId.CrushEvaluation));
			Assert.That(result.Disposition, Is.EqualTo(StealthCrushDisposition.Retain));
			Assert.That(orders.Orders, Has.Count.EqualTo(1));
		}

		[TestCase("live")]
		[TestCase("threat")]
		[TestCase("order")]
		public void ReentrantOwnershipLossCannotCommitProspectiveState(string callback)
		{
			var input = CreateInput();
			var transitionSource = Behavior(input, Live(0, new[] { Actor(10) }),
				out _, out var sourceThreats, out _);
			sourceThreats.Result = Unsafe();
			var transitionResult = transitionSource.Execute();
			var behavior = Behavior(input, Live(1, new[] { Actor(10) }),
				out var live, out var threats, out var orders);
			var before = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var accepted = false;
			Action invalidate = () => accepted = input.Controller.TryAccept(transitionResult, out _);
			if (callback == "live")
				live.OnRead = invalidate;
			else if (callback == "threat")
				threats.OnCalculate = invalidate;
			else
				orders.OnIssue = invalidate;

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());

			Assert.That(accepted, Is.True);
			Assert.That(input.Controller.Owner, Is.EqualTo(BehaviorId.Kite));
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));
			Assert.That(orders.Orders.Count, Is.EqualTo(callback == "order" ? 1 : 0));
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
		}

		[TestCase("live")]
		[TestCase("threat")]
		[TestCase("order")]
		public void RecursiveExecuteIsRejectedBeforeNestedExternalWorkOrLostUpdate(string callback)
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Actor(10) }),
				out var live, out var threats, out var orders);
			Exception recursiveFailure = null;
			Action recurse = () => recursiveFailure = Assert.Throws<InvalidOperationException>(
				() => behavior.Execute());
			if (callback == "live")
				live.OnRead = recurse;
			else if (callback == "threat")
				threats.OnCalculate = recurse;
			else
				orders.OnIssue = recurse;

			var result = behavior.Execute();

			Assert.That(recursiveFailure, Is.Not.Null);
			Assert.That(result.Disposition, Is.EqualTo(StealthCrushDisposition.Retain));
			Assert.That(live.Reads, Is.EqualTo(1), "The recursive call cannot enter the live callback.");
			Assert.That(threats.Facts, Has.Count.EqualTo(1));
			Assert.That(orders.Orders, Has.Count.EqualTo(1));
			var committed = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			live.OnRead = null;
			threats.OnCalculate = null;
			orders.OnIssue = null;

			behavior.Execute();

			Assert.That(orders.Orders, Has.Count.EqualTo(1),
				"The outer commit retains its order latch without a recursive lost update.");
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(committed));
		}

		[Test]
		public void RecursiveExceptionReleasesLeaseAndLeavesOuterStateUnchanged()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Actor(10) }),
				out var live, out var threats, out var orders);
			var before = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			live.OnRead = () => behavior.Execute();

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());

			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));
			Assert.That(threats.Facts, Is.Empty);
			Assert.That(orders.Orders, Is.Empty);
			live.OnRead = null;

			Assert.DoesNotThrow(() => behavior.Execute());
			Assert.That(live.Reads, Is.EqualTo(2));
			Assert.That(orders.Orders, Has.Count.EqualTo(1));
		}

		[TestCase("live")]
		[TestCase("threat")]
		public void RecursiveRestoreIsRejectedAndOuterRestoreCommitsExactlyOnce(string callback)
		{
			var input = CreateInput();
			var snapshot = Live(0, new[] { Actor(10) });
			var source = Behavior(input, snapshot, out _, out _, out _);
			source.Execute();
			var serialized = new List<MiniYamlNode> { source.SerializePrivateState() }.WriteToString();
			var saved = MiniYaml.FromString(serialized).Single();
			var restored = Behavior(input, snapshot, out var live, out var threats, out var orders);
			Exception recursiveFailure = null;
			Action recurse = () => recursiveFailure = Assert.Throws<InvalidOperationException>(
				() => restored.RestorePrivateState(saved));
			if (callback == "live")
				live.OnRead = recurse;
			else
				threats.OnCalculate = recurse;

			restored.RestorePrivateState(saved);

			Assert.That(recursiveFailure, Is.Not.Null);
			Assert.That(live.Reads, Is.EqualTo(1));
			Assert.That(threats.Facts, Has.Count.EqualTo(1));
			Assert.That(orders.Orders, Is.Empty);
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(serialized));
			live.OnRead = null;
			threats.OnCalculate = null;

			restored.Execute();

			Assert.That(orders.Orders, Is.Empty,
				"The one outer restore commit preserves the dedup latch without a recursive overwrite.");
		}

		[TestCase("execute", "execute")]
		[TestCase("restore", "restore")]
		[TestCase("execute", "restore")]
		[TestCase("restore", "execute")]
		public void OwnershipGuardCannotReenterSameOrCrossMethod(
			string outerOperation, string nestedOperation)
		{
			var input = CreateInput();
			var snapshot = Live(0, new[] { Actor(10) });
			var source = Behavior(input, snapshot, out _, out _, out _);
			source.Execute();
			var serialized = new List<MiniYamlNode> { source.SerializePrivateState() }.WriteToString();
			var saved = MiniYaml.FromString(serialized).Single();
			var ownership = new OwnershipProbe();
			var behavior = Behavior(input, snapshot, out var live, out var threats, out var orders,
				ownership);
			Exception recursiveFailure = null;
			var checksAtNestedReturn = -1;
			ownership.OnCheck = () =>
			{
				ownership.OnCheck = null;
				var checksAtNestedEntry = ownership.Checks;
				recursiveFailure = Assert.Throws<InvalidOperationException>(() =>
				{
					if (nestedOperation == "execute")
						behavior.Execute();
					else
						behavior.RestorePrivateState(saved);
				});
				checksAtNestedReturn = ownership.Checks;
				Assert.That(checksAtNestedReturn, Is.EqualTo(checksAtNestedEntry),
					"Nested acquisition must fail before another ownership callback.");
			};

			if (outerOperation == "execute")
				behavior.Execute();
			else
				behavior.RestorePrivateState(saved);

			Assert.That(recursiveFailure, Is.Not.Null);
			Assert.That(checksAtNestedReturn, Is.GreaterThan(0));
			Assert.That(live.Reads, Is.EqualTo(1));
			Assert.That(threats.Facts, Has.Count.EqualTo(1));
			Assert.That(orders.Orders.Count, Is.EqualTo(outerOperation == "execute" ? 1 : 0));
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(serialized));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void OwnershipFailureRollsBackLeaseForLaterCleanExecution(bool throws)
		{
			var input = CreateInput();
			var ownership = new OwnershipProbe { Active = throws, Throw = throws };
			var behavior = Behavior(input, Live(0, new[] { Actor(10) }),
				out var live, out var threats, out var orders, ownership);
			var before = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());

			Assert.That(live.Reads, Is.Zero);
			Assert.That(threats.Facts, Is.Empty);
			Assert.That(orders.Orders, Is.Empty);
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));
			ownership.Active = true;
			ownership.Throw = false;

			Assert.DoesNotThrow(() => behavior.Execute());
			Assert.That(live.Reads, Is.EqualTo(1));
			Assert.That(orders.Orders, Has.Count.EqualTo(1));
		}

		[Test]
		public void RevisionExhaustionRejectsBeforeOwnershipAndExternalWork()
		{
			var input = CreateInput();
			var ownership = new OwnershipProbe();
			var behavior = Behavior(input, Live(0, new[] { Actor(10) }),
				out var live, out var threats, out var orders, ownership);
			var before = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			SetExecutionRevision(behavior, long.MaxValue);

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.Throws<InvalidOperationException>(() => behavior.Execute(),
				"A retry at exhausted revision remains side-effect free.");

			Assert.That(ownership.Checks, Is.Zero);
			Assert.That(live.Reads, Is.Zero);
			Assert.That(threats.Facts, Is.Empty);
			Assert.That(orders.Orders, Is.Empty);
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));

			SetExecutionRevision(behavior, long.MaxValue - 1);
			Assert.DoesNotThrow(() => behavior.Execute(),
				"The last reservable revision can commit its one permitted action.");
			var checksAfterCommit = ownership.Checks;
			var readsAfterCommit = live.Reads;
			Assert.That(orders.Orders, Has.Count.EqualTo(1));

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(ownership.Checks, Is.EqualTo(checksAfterCommit));
			Assert.That(live.Reads, Is.EqualTo(readsAfterCommit));
			Assert.That(threats.Facts, Has.Count.EqualTo(1));
			Assert.That(orders.Orders, Has.Count.EqualTo(1),
				"Exhaustion after the final commit cannot issue and then fail another order.");
		}

		[Test]
		public void RetainedTargetWithAllMembersLostYieldsKiteWithoutThreatOrOrder()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Actor(10) }),
				out var live, out var threats, out var orders);
			behavior.Execute();
			live.Snapshot = Live(1, new[] { Actor(10) },
				new[] { Member(1, inWorld: false) });

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthCrushDisposition.Kite));
			Assert.That(result.Mission, Is.SameAs(input.Mission));
			Assert.That(result.ActiveMemberActorIds, Is.Empty);
			Assert.That(result.SelectedTargetActorId, Is.Null);
			Assert.That(result.Safety, Is.Null);
			Assert.That(threats.Facts, Has.Count.EqualTo(1),
				"The squad-loss tick must not construct empty-friendly threat facts.");
			Assert.That(orders.Orders, Has.Count.EqualTo(1),
				"The squad-loss tick must not issue another order.");
			Assert.That(input.Controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.Kite.Mission, Is.SameAs(input.Mission));
		}

		[Test]
		public void PrivateStateRoundTripsAndRejectsTamperingWithoutPartialMutation()
		{
			var input = CreateInput();
			var snapshot = Live(10, new[] { Actor(10, current: new CPos(6, 4)) },
				new[] { Member(2), Member(1) });
			var source = Behavior(input, snapshot, out _, out _, out _);
			source.Execute();
			var serialized = new List<MiniYamlNode> { source.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input, snapshot, out _, out var restoredThreats, out var restoredOrders);

			restored.RestorePrivateState(MiniYaml.FromString(serialized).Single());
			restored.Execute();

			Assert.That(restoredOrders.Orders, Is.Empty);
			Assert.That(restoredThreats.Facts, Has.Count.EqualTo(2));
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(serialized));
			var before = new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString();
			var tampered = new[]
			{
				serialized.Replace("Owner: CrushEvaluation", "Owner: Approach"),
				serialized.Replace("IncomingDefenderActorId: 71", "IncomingDefenderActorId: 72"),
				serialized.Replace("SelectedTargetCurrentCell: 6,4", "SelectedTargetCurrentCell: 7,4"),
				serialized.Replace("ThreatRating: 0", "ThreatRating: 1"),
				serialized.Replace("LastIssuedTargetCurrentCell: 6,4", "LastIssuedTargetCurrentCell: 7,4"),
				serialized.Replace("StrategicCell: 4,4", "StrategicCell: 5,4")
			};
			foreach (var invalid in tampered)
				Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(
					MiniYaml.FromString(invalid).Single()));
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));
		}

		[Test]
		public void ImmutableCopiesRejectCallbackMutationAndIdentityAliases()
		{
			var input = CreateInput();
			var memberSource = new[] { Member(2), Member(1) };
			var actorSource = new[] { Actor(10) };
			var snapshot = Live(0, actorSource, memberSource);
			memberSource[0] = Member(99);
			actorSource[0] = Actor(99);
			var behavior = Behavior(input, snapshot, out _, out _, out var orders);
			orders.AttemptMutation = true;

			var result = behavior.Execute();

			Assert.That(result.ActiveMemberActorIds, Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(10));
			Assert.That(orders.MutationDuringIssueSucceeded, Is.False);
			Assert.That(orders.TryMutate(999), Is.False);
			Assert.That(result.ActiveMemberActorIds is uint[], Is.False);
			Assert.Throws<ArgumentException>(() => Live(0, new[] { Actor(10), Actor(10) }));
			Assert.Throws<ArgumentException>(() => Live(0, new[] { Actor(10) },
				new[] { Member(1), Member(1) }));

			var duplicate = behavior.SerializePrivateState();
			duplicate.Value.Nodes.Add(new MiniYamlNode("Owner", "CrushEvaluation"));
			Assert.Throws<InvalidOperationException>(() => behavior.RestorePrivateState(duplicate));
		}

		[Test]
		public void RestoreCanonicalizesAbsentOrderTargetActorsAndPosition()
		{
			var input = CreateInput();
			var snapshot = Live(0, new[]
			{
				Actor(40, infantry: false, crushable: false, type: "mtnk")
			});
			var source = Behavior(input, snapshot, out _, out _, out _);
			source.Execute();
			var forged = source.SerializePrivateState();
			Assert.That(forged.Value.Nodes.Single(node => node.Key == "HasLastIssuedTarget")
				.Value.Value, Is.EqualTo("False"));
			forged.Value.Nodes.Add(new MiniYamlNode("LastIssuedActorId", "1"));
			var restored = Behavior(input, snapshot, out var live, out var threats, out var orders);
			var before = new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString();

			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(forged));
			var forgedPosition = source.SerializePrivateState();
			forgedPosition.Value.Nodes.Single(node => node.Key == "LastIssuedTargetCurrentCell")
				.Value.Value = "1,1";
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(forgedPosition));

			Assert.That(live.Reads, Is.Zero, "Canonical persistence rejects before external work.");
			Assert.That(threats.Facts, Is.Empty);
			Assert.That(orders.Orders, Is.Empty);
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));
		}

		[Test]
		public void SelectedKiteRequiresCompleteUnsafeSafetyState()
		{
			var input = CreateInput();
			var snapshot = Live(0, new[] { Actor(10) });
			var source = Behavior(input, snapshot, out _, out _, out _);
			Assert.That(source.Execute().Disposition, Is.EqualTo(StealthCrushDisposition.Retain));
			var forged = source.SerializePrivateState();
			foreach (var field in new[]
			{
				("Disposition", "Kite"), ("HasSafety", "False"),
				("ThreatSelectedTargetActorId", "0"),
				("ThreatSelectedTargetCurrentCell", "0,0"),
				("ThreatFormationCloaked", "False"),
				("ThreatHasDetectorCoverage", "False"),
				("ThreatRating", "0"), ("Crossover", "0"),
				("SafetyApproved", "False"), ("HasLastIssuedTarget", "False"),
				("LastIssuedTargetActorId", "0"),
				("LastIssuedTargetCurrentCell", "0,0")
			})
				forged.Value.Nodes.Single(node => node.Key == field.Item1).Value.Value = field.Item2;
			forged.Value.Nodes.RemoveAll(node => node.Key == "ThreatFriendlyActorId" ||
				node.Key == "ThreatEnemyActorId" || node.Key == "LastIssuedActorId");
			var restored = Behavior(input, snapshot, out var live, out var threats, out var orders);
			var before = new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString();

			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(forged));

			Assert.That(live.Reads, Is.Zero,
				"Selected Kite without complete safety is rejected as noncanonical before live work.");
			Assert.That(threats.Facts, Is.Empty);
			Assert.That(orders.Orders, Is.Empty);
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));
		}

		[TestCase("member")]
		[TestCase("candidate")]
		public void TargetlessKiteRejectsRestoreWhenItsLiveCauseDisappears(string liveChange)
		{
			var input = CreateInput();
			var sourceSnapshot = liveChange == "member" ?
				Live(0, new[] { Actor(10) }, new[] { Member(1, inWorld: false) }) :
				Live(0, new[] { Actor(10, infantry: false, crushable: false, type: "mtnk") });
			var source = Behavior(input, sourceSnapshot, out _, out _, out _);
			var sourceResult = source.Execute();
			Assert.That(sourceResult.Disposition, Is.EqualTo(StealthCrushDisposition.Kite));
			Assert.That(sourceResult.SelectedTargetActorId, Is.Null);
			var saved = source.SerializePrivateState();
			var restored = Behavior(input, Live(1, new[] { Actor(10) }),
				out var live, out var threats, out var orders);
			var before = new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString();

			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(saved));

			Assert.That(live.Reads, Is.EqualTo(1));
			Assert.That(threats.Facts, Is.Empty,
				"A stale targetless cause is rejected before calculating candidate safety.");
			Assert.That(orders.Orders, Is.Empty);
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));
		}

		[TestCase("zero-member")]
		[TestCase("no-candidate")]
		[TestCase("detector")]
		public void ReachableKiteStateRestoresWhileExactLiveCauseRemains(string cause)
		{
			var input = CreateInput();
			var snapshot = cause == "zero-member" ?
				Live(0, new[] { Actor(10) }, new[] { Member(1, inWorld: false) }) :
				cause == "no-candidate" ?
				Live(0, new[] { Actor(10, infantry: false, crushable: false, type: "mtnk") }) :
				Live(0, new[] { Actor(10, detector: true) });
			var source = Behavior(input, snapshot, out _, out _, out _);
			var sourceResult = source.Execute();
			Assert.That(sourceResult.Disposition, Is.EqualTo(StealthCrushDisposition.Kite));
			Assert.That(sourceResult.SelectedTargetActorId.HasValue,
				Is.EqualTo(cause == "detector"));
			Assert.That(sourceResult.Safety.HasValue, Is.EqualTo(cause == "detector"));
			var serialized = new List<MiniYamlNode> { source.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input, snapshot, out var live, out var threats, out var orders);

			restored.RestorePrivateState(MiniYaml.FromString(serialized).Single());

			Assert.That(live.Reads, Is.EqualTo(1));
			Assert.That(threats.Facts.Count, Is.EqualTo(cause == "detector" ? 1 : 0));
			Assert.That(orders.Orders, Is.Empty);
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(serialized));
		}

		[Test]
		public void OwnerHasNoCacheOrTargetExclusivityDependency()
		{
			var fields = typeof(StealthCrushBehavior).GetFields(
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			Assert.That(fields.Any(field => field.FieldType.Name.IndexOf(
				"Cache", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
			Assert.That(fields.Any(field => field.Name.IndexOf(
				"claim", StringComparison.OrdinalIgnoreCase) >= 0 || field.Name.IndexOf(
				"exclusive", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
			var input = CreateInput();
			var behavior = Behavior(input, Live(0, new[] { Actor(10) }),
				out var live, out _, out _);

			behavior.Execute();

			Assert.That(live.Reads, Is.EqualTo(1));
		}
	}
}
