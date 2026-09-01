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
	public sealed class StealthKiteBehaviorTest
	{
		sealed class Input
		{
			public StealthLifecycleController Controller;
			public StealthKiteHandoff Handoff;
		}

		sealed class LiveProbe : IStealthKiteLiveWorld
		{
			public StealthKiteLiveSnapshot Snapshot;
			public int Reads;
			public Action OnRead;
			public StealthKiteLiveSnapshot Read(StealthApproachMission mission)
			{
				Reads++;
				OnRead?.Invoke();
				return Snapshot;
			}
		}

		sealed class ThreatProbe : IStealthKiteThreatAdapter
		{
			public readonly List<StealthKiteThreatFacts> Facts = new List<StealthKiteThreatFacts>();
			public readonly List<StealthKiteFallbackFacts> FallbackFacts =
				new List<StealthKiteFallbackFacts>();
			public Func<StealthKiteThreatFacts, StealthKiteSafetyResult> Evaluate = facts =>
				new StealthKiteSafetyResult(new StealthTargetThreatScore(1, 3), true);
			public Func<StealthKiteFallbackFacts, StealthTargetThreatScore> EvaluateFallback = facts =>
				new StealthTargetThreatScore(1, 3);
			public Action OnCalculate;
			public StealthKiteSafetyResult Calculate(StealthKiteThreatFacts facts)
			{
				Facts.Add(facts);
				OnCalculate?.Invoke();
				return Evaluate(facts);
			}

			public StealthTargetThreatScore CalculateAttackCrossover(StealthKiteFallbackFacts facts)
			{
				FallbackFacts.Add(facts);
				OnCalculate?.Invoke();
				return EvaluateFallback(facts);
			}
		}

		sealed class OrderProbe : IStealthKiteOrders
		{
			public readonly List<(StealthKiteAction Action, BehaviorId Owner, OwnershipEpoch Epoch,
				uint[] Actors, uint? Target, CPos Cell)> Issued =
				new List<(StealthKiteAction, BehaviorId, OwnershipEpoch, uint[], uint?, CPos)>();
			public readonly List<StealthKiteOrderToken> Calls = new List<StealthKiteOrderToken>();
			readonly HashSet<StealthKiteOrderToken> accepted = new HashSet<StealthKiteOrderToken>();
			public Action OnIssue;
			public IReadOnlyList<uint> RetainedActors;
			public bool MutationSucceeded;
			public StealthKiteOrderToken LastToken => Calls.LastOrDefault();
			public void IssueMove(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, CPos cell, StealthKiteOrderToken token)
			{
				Calls.Add(token);
				if (!accepted.Add(token))
					return;
				RetainedActors = actorIds;
				MutationSucceeded = TryMutate();
				Issued.Add((cell == new CPos(5, 0) ? StealthKiteAction.Position :
					StealthKiteAction.Withdraw, owner, epoch, actorIds.ToArray(), null, cell));
				OnIssue?.Invoke();
			}

			public void IssueAttack(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint targetActorId, CPos targetCurrentCell,
				StealthKiteOrderToken token)
			{
				Calls.Add(token);
				if (!accepted.Add(token))
					return;
				RetainedActors = actorIds;
				MutationSucceeded = TryMutate();
				Issued.Add((StealthKiteAction.Fire, owner, epoch, actorIds.ToArray(),
					targetActorId, targetCurrentCell));
				OnIssue?.Invoke();
			}

			bool TryMutate()
			{
				if (!(RetainedActors is IList<uint> list) || list.Count == 0)
					return false;
				try
				{
					list[0] = 999;
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
			public bool Active = true;
			public bool IsActive(BehaviorId owner, OwnershipEpoch epoch)
			{
				return Active;
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
				throw new InvalidOperationException("Arrival must remain live-only.");
			}
		}

		sealed class ApproachLive : IStealthApproachLiveWorld
		{
			public StealthApproachLiveSnapshot Read(StealthApproachMission mission)
			{
				return new StealthApproachLiveSnapshot(true,
					new[] { new StealthApproachMemberSnapshot(1, mission.StrategicCell) },
					new[] { new StealthCombatGroupSnapshot("stnk", 1, 900) },
					Array.Empty<StealthCombatGroupSnapshot>(), new uint[] { 71 }, true, false, false);
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

		sealed class CrushLive : IStealthCrushLiveWorld
		{
			public StealthCrushLiveSnapshot Read(StealthApproachMission mission)
			{
				return new StealthCrushLiveSnapshot(0,
					new[]
					{
						new StealthCrushMemberSnapshot(1, new CPos(0, 0))
					},
					new[]
					{
						new StealthCrushActorSnapshot(71, "e1", mission.StrategicCell,
							new CPos(8, 0), 100, true, false, true, true, true)
					}, true);
			}
		}

		sealed class UnsafeCrush : IStealthCrushThreatAdapter
		{
			public StealthCrushSafetyResult Calculate(StealthCrushThreatFacts facts)
			{
				return new StealthCrushSafetyResult(new StealthTargetThreatScore(2, 0.5), false);
			}
		}

		sealed class NoCrush : IStealthCrushOrders
		{
			public void IssueCrush(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint targetActorId, CPos targetCurrentCell)
			{
				throw new InvalidOperationException("Unsafe Crush must transfer without ordering.");
			}
		}

		static Input CreateInput()
		{
			var cell = new CPos(4, 4);
			var facts = new StealthTargetThreatFacts(cell,
				new[] { new StealthCombatGroupSnapshot("stnk", 2, 900) },
				Array.Empty<StealthCombatGroupSnapshot>(), true, false, true);
			var filler = Enumerable.Range(0, 9).Select(index => new CPos(10 + index, 1)).ToArray();
			var cells = new[] { cell }.Concat(filler).ToArray();
			var targets = new[] { new StealthStrategicTargetSnapshot(99, cell, 100, 1000, 100, 100) }
				.Concat(filler.Select((candidate, index) => new StealthStrategicTargetSnapshot(
					(uint)(100 + index), candidate, 1, 1, 100, 100))).ToArray();
			var threatFacts = new[] { facts }.Concat(filler.Select(candidate =>
				new StealthTargetThreatFacts(candidate, facts.FriendlyGroup,
					Array.Empty<StealthCombatGroupSnapshot>(), true, false, true))).ToArray();
			var controller = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.TargetAcquisition, new OwnershipEpoch(2), -1));
			var acquisition = new StealthTargetAcquisitionBehavior(controller.CurrentHandoff,
				new AcquisitionCache(new StealthTargetAcquisitionCacheSnapshot(40, 20,
					Enumerable.Repeat(0f, 800), cells, 1, targets, threatFacts)))
				.Execute(new CPos(0, 0), null);
			Assert.That(controller.TryAccept(acquisition, out var valueHandoff), Is.True);
			var value = new StealthTargetValueFilterBehavior(valueHandoff).Execute();
			Assert.That(controller.TryAccept(value, out var threatHandoff), Is.True);
			var threat = new StealthTargetThreatFilterBehavior(threatHandoff, new StandardThreat()).Execute();
			Assert.That(controller.TryAccept(threat, out var distanceHandoff), Is.True);
			var distance = new StealthTargetDistanceChoiceBehavior(distanceHandoff,
				Array.Empty<StealthActiveSquadTargetSnapshot>(),
				new StealthTargetDistanceChoicePolicy(1000, 3000)).Execute();
			Assert.That(controller.TryAccept(distance, out var approachHandoff), Is.True);
			var approach = new StealthApproachBehavior(approachHandoff, new ApproachCache(),
				new ApproachLive(), new StandardThreat(), new NoMovement()).Execute();
			Assert.That(controller.TryAccept(approach, out var approachTransition), Is.True);
			var crush = new StealthCrushBehavior(approachTransition.CrushEvaluation,
				controller, new CrushLive(), new UnsafeCrush(), new NoCrush()).Execute();
			Assert.That(controller.TryAccept(crush, out var crushTransition), Is.True);
			return new Input { Controller = controller, Handoff = crushTransition.Kite };
		}

		static StealthKiteMemberSnapshot Member(uint id = 1, int x = 0, int range = 4,
			bool inWorld = true, bool dead = false, int hp = 100, int maxHp = 100)
		{
			return new StealthKiteMemberSnapshot(id, new CPos(x, 0), range,
				inWorld, dead, hp, maxHp);
		}

		static StealthKiteActorSnapshot Actor(uint id, int x = 8, int hp = 100,
			int range = 2, string type = "mtnk", bool defender = true,
			bool objective = false, bool infantry = false, bool crushable = false,
			bool detector = false, bool local = true, bool inWorld = true,
			bool dead = false, bool targetable = true)
		{
			return new StealthKiteActorSnapshot(id, type, new CPos(x, 0), hp, 100, range,
				defender, objective, infantry, crushable, detector, local, inWorld, dead, targetable);
		}

		static StealthKiteLiveSnapshot Live(int tick, IEnumerable<StealthKiteActorSnapshot> actors,
			IEnumerable<StealthKiteMemberSnapshot> members = null, IEnumerable<CPos> cells = null,
			bool cloaked = true, bool observeActivity = false, long activityRevision = 0,
			StealthKiteOrderToken activeOrder = null, StealthKiteOrderToken completedOrder = null)
		{
			return new StealthKiteLiveSnapshot(tick, members ?? new[] { Member() }, actors,
				cells ?? new[] { new CPos(5, 0), new CPos(0, 0) }, cloaked,
				observeActivity, activityRevision, activeOrder, completedOrder);
		}

		static StealthKiteBehavior Behavior(Input input, StealthKiteLiveSnapshot snapshot,
			out LiveProbe live, out ThreatProbe threats, out OrderProbe orders,
			IStealthLifecycleOwnershipGuard guard = null)
		{
			live = new LiveProbe { Snapshot = snapshot };
			threats = new ThreatProbe();
			orders = new OrderProbe();
			return new StealthKiteBehavior(input.Handoff, guard ?? input.Controller,
				live, threats, orders);
		}

		static void SetRevision(StealthKiteBehavior behavior, long revision)
		{
			var lease = typeof(StealthKiteBehavior).GetField("executionLease",
				BindingFlags.Instance | BindingFlags.NonPublic).GetValue(behavior);
			lease.GetType().GetField("revision", BindingFlags.Instance | BindingFlags.NonPublic)
				.SetValue(lease, revision);
		}

		[Test]
		public void ChoosesNearestLocalLiveTargetWithDeterministicIdentityTie()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1, new[]
			{
				Actor(30, x: 2, type: "harv", local: false),
				Actor(12, x: 8, type: "harv"),
				Actor(11, x: 8, type: "mtnk"),
				Actor(20, x: 10, type: "obli")
			}), out _, out _, out _);

			var result = behavior.Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(11));
			Assert.That(result.LiveDefenderActorIds, Is.EqualTo(new uint[] { 11, 12, 20 }));
		}

		[Test]
		public void RetainsMovingDamagedTargetAndUsesCurrentCellHpAndRange()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1, new[] { Actor(10) }),
				out var live, out var threats, out var orders);
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(10));
			live.Snapshot = Live(2, new[] { Actor(10, x: 7, hp: 25, range: 3), Actor(9, x: 6) },
				cells: new[] { new CPos(4, 0), new CPos(0, 0) });

			var result = behavior.Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(10));
			Assert.That(result.SelectedTargetCurrentCell, Is.EqualTo(new CPos(7, 0)));
			Assert.That(threats.Facts.Last().Enemies.Single(actor => actor.ActorId == 10).HitPoints,
				Is.EqualTo(25));
			Assert.That(threats.Facts.Last().Enemies.Single(actor => actor.ActorId == 10)
				.CurrentWeaponRangeCells, Is.EqualTo(3));
			Assert.That(orders.Issued.Last().Cell, Is.EqualTo(new CPos(4, 0)));
		}

		[Test]
		public void OwnsPositionFireWithdrawAndContinueWithoutOrderChurn()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1, new[] { Actor(10) }),
				out var live, out _, out var orders);
			var position = behavior.Execute();
			behavior.Execute();
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
			live.Snapshot = Live(2, new[] { Actor(10) }, new[] { Member(x: 5) });
			var fire = behavior.Execute();
			var waitingForShot = behavior.Execute();
			live.Snapshot = Live(3, new[] { Actor(10, hp: 75) }, new[] { Member(x: 5) });
			var withdraw = behavior.Execute();
			live.Snapshot = Live(4, new[] { Actor(10, hp: 75) });
			var continued = behavior.Execute();

			Assert.That(position.Phase, Is.EqualTo(StealthKitePhase.Position));
			Assert.That(fire.Phase, Is.EqualTo(StealthKitePhase.Fire));
			Assert.That(waitingForShot.Phase, Is.EqualTo(StealthKitePhase.Fire));
			Assert.That(withdraw.Phase, Is.EqualTo(StealthKitePhase.Withdraw));
			Assert.That(continued.Phase, Is.EqualTo(StealthKitePhase.Position));
			Assert.That(orders.Issued.Select(order => order.Action), Is.EqualTo(new[]
			{
				StealthKiteAction.Position, StealthKiteAction.Fire,
				StealthKiteAction.Withdraw, StealthKiteAction.Position
			}));
			Assert.That(orders.Issued.All(order => order.Owner == BehaviorId.Kite &&
				order.Epoch == input.Handoff.Epoch), Is.True);
		}

		[Test]
		public void LiveRangeReplanResetsPhaseBeforeUsingChangedCells()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1, new[] { Actor(10) }),
				out var live, out _, out var orders);
			behavior.Execute();
			live.Snapshot = Live(2, new[] { Actor(10) }, new[] { Member(x: 5) });
			Assert.That(behavior.Execute().Phase, Is.EqualTo(StealthKitePhase.Fire));
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "LastPlanTick").Value.Value, Is.EqualTo("2"));
			live.Snapshot = Live(3, new[] { Actor(10) }, new[] { Member(x: 5, range: 2) },
				new[] { new CPos(6, 0), new CPos(0, 0) });

			var replanned = behavior.Execute();

			Assert.That(replanned.FireCell, Is.EqualTo(new CPos(6, 0)));
			Assert.That(replanned.Phase, Is.EqualTo(StealthKitePhase.Position));
			Assert.That(orders.Issued.Last().Target, Is.Null);
			Assert.That(orders.Issued.Last().Cell, Is.EqualTo(new CPos(6, 0)));
		}

		[TestCase(3.01, StealthKiteDisposition.MassAttack)]
		[TestCase(2.0, StealthKiteDisposition.RecalculateFlee)]
		public void NoSafePlanUsesExactStandardCrossover(double crossover,
			StealthKiteDisposition expected)
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1, new[] { Actor(10) }),
				out _, out var threats, out var orders);
			threats.Evaluate = facts => new StealthKiteSafetyResult(
				new StealthTargetThreatScore(5, crossover), false);
			threats.EvaluateFallback = facts => new StealthTargetThreatScore(5, crossover);

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(expected));
			Assert.That(result.Mission, Is.SameAs(input.Handoff.Mission));
			Assert.That(orders.Issued, Is.Empty);
			Assert.That(input.Controller.TryAccept(result, out var transition), Is.True);
			Assert.That(input.Controller.Owner, Is.EqualTo(expected == StealthKiteDisposition.MassAttack ?
				BehaviorId.MassAttack : BehaviorId.RecalculateFlee));
			Assert.That(transition.MassAttack != null, Is.EqualTo(expected == StealthKiteDisposition.MassAttack));
		}

		[Test]
		public void TargetKillInvalidationCrushAndTargetlessHandoffsUseLiveState()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1, new[] { Actor(10), Actor(20, x: 10) }),
				out var live, out _, out _);
			behavior.Execute();
			live.Snapshot = Live(2, new[] { Actor(10, dead: true), Actor(20, x: 10) });
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(20));
			live.Snapshot = Live(3, new[] { Actor(40, x: 9, type: "e1", infantry: true, crushable: true) });
			Assert.That(behavior.Execute().Disposition, Is.EqualTo(StealthKiteDisposition.CrushEvaluation));

			var objective = Behavior(CreateInput(), Live(4, new[]
			{
				Actor(90, defender: false, objective: true, type: "fact")
			}), out _, out _, out _).Execute();
			var empty = Behavior(CreateInput(), Live(4, Array.Empty<StealthKiteActorSnapshot>()),
				out _, out _, out _).Execute();
			Assert.That(objective.Disposition, Is.EqualTo(StealthKiteDisposition.UndefendedAttack));
			Assert.That(empty.Disposition, Is.EqualTo(StealthKiteDisposition.Reacquire));
		}

		[Test]
		public void CloakDecloakObeliskAndMixedGuardsAreExplicitInEverySafetyFact()
		{
			var input = CreateInput();
			var actors = new[]
			{
				Actor(10, type: "harv", range: 0),
				Actor(20, x: 12, type: "obli", range: 6),
				Actor(30, x: 10, type: "mtnk", range: 4)
			};
			var behavior = Behavior(input, Live(1, actors), out _, out var threats, out _);
			threats.Evaluate = facts => new StealthKiteSafetyResult(
				new StealthTargetThreatScore(2, 2.5), true);

			behavior.Execute();

			Assert.That(threats.Facts.Any(facts => facts.Action == StealthKiteAction.Fire &&
				facts.PlannedDecloak && facts.PlannedAttack), Is.True);
			Assert.That(threats.Facts.All(facts => facts.EnemyActorIds.SequenceEqual(
				new uint[] { 10, 20, 30 })), Is.True);
			Assert.That(threats.Facts.Any(facts => !facts.PlannedDecloak &&
				facts.FormationCloaked), Is.True);
		}

		[Test]
		public void TargetsAreNonExclusiveAndCollectionsAndOrdersAreImmutableCopies()
		{
			var inputA = CreateInput();
			var inputB = CreateInput();
			var members = new[] { Member(2), Member(1) };
			var actors = new[] { Actor(10) };
			var snapshot = Live(1, actors, members);
			members[0] = Member(99);
			actors[0] = Actor(99);
			var first = Behavior(inputA, snapshot, out _, out _, out var ordersA).Execute();
			var second = Behavior(inputB, snapshot, out _, out _, out _).Execute();

			Assert.That(first.SelectedTargetActorId, Is.EqualTo(10));
			Assert.That(second.SelectedTargetActorId, Is.EqualTo(10));
			Assert.That(first.ActiveMemberActorIds, Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(first.ActiveMemberActorIds is uint[], Is.False);
			Assert.That(ordersA.MutationSucceeded, Is.False);
			Assert.Throws<ArgumentException>(() => Live(1, new[] { Actor(10), Actor(10) }));
		}

		[Test]
		public void PrivateStateRoundTripsAndRejectsTamperingTransactionally()
		{
			var input = CreateInput();
			var snapshot = Live(1, new[] { Actor(10) });
			var source = Behavior(input, snapshot, out _, out _, out _);
			source.Execute();
			var text = new List<MiniYamlNode> { source.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input, snapshot, out _, out _, out var orders);
			restored.RestorePrivateState(MiniYaml.FromString(text).Single());
			var canonical = new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString();
			Assert.That(canonical, Is.EqualTo(text));
			Assert.That(orders.Issued, Is.Empty);

			foreach (var invalid in new[]
			{
				text.Replace("Owner: Kite", "Owner: Approach"),
				text.Replace("Phase: Position", "Phase: Fire"),
				text.Replace("Action: Position", "Action: Fire"),
				text.Replace("ActivityRevision: 0", "ActivityRevision: 9"),
				text.Replace("PlannedDecloak: True", "PlannedDecloak: False"),
				text.Replace("Infantry: False", "Infantry: True"),
				text.Replace("TargetHitPoints: 100", "TargetHitPoints: 99"),
				text.Replace("Crossover: 3", "Crossover: 4"),
				text.Replace("IncomingDefenderId: 71", "IncomingDefenderId: 72"),
				text.Replace("StrategicCell: 4,4", "StrategicCell: 5,4")
			})
				Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(
					MiniYaml.FromString(invalid).Single()));
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(canonical));
		}

		[Test]
		public void FallbackRestoreRequiresSameNoSafePlanAndCanonicalDisposition()
		{
			var input = CreateInput();
			var snapshot = Live(1, new[] { Actor(10) });
			var source = Behavior(input, snapshot, out _, out var sourceThreats, out _);
			sourceThreats.Evaluate = facts => new StealthKiteSafetyResult(
				new StealthTargetThreatScore(5, 3), false);
			Assert.That(source.Execute().Disposition, Is.EqualTo(StealthKiteDisposition.MassAttack));
			var saved = source.SerializePrivateState();
			var restored = Behavior(input, snapshot, out _, out var restoredThreats, out _);
			restoredThreats.Evaluate = sourceThreats.Evaluate;

			restored.RestorePrivateState(saved);
			Assert.That(restored.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "Disposition").Value.Value, Is.EqualTo("MassAttack"));

			var nowSafe = Behavior(input, snapshot, out _, out _, out _);
			Assert.Throws<InvalidOperationException>(() => nowSafe.RestorePrivateState(saved));
			var forged = MiniYaml.FromString(new List<MiniYamlNode> { saved }.WriteToString()).Single();
			forged.Value.Nodes.Single(node => node.Key == "Disposition").Value.Value = "Retain";
			var beforeReads = restoredThreats.Facts.Count;
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(forged));
			Assert.That(restoredThreats.Facts, Has.Count.EqualTo(beforeReads));
		}

		[Test]
		public void EverySafetyAffectingLiveChangeReplansButUnchangedActionStaysDeduped()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1, new[] { Actor(10), Actor(20, x: 11) }),
				out var live, out var threats, out var orders);
			behavior.Execute();
			var previousFacts = threats.Facts.Count;
			behavior.Execute();
			Assert.That(threats.Facts, Has.Count.EqualTo(previousFacts));

			var changes = new[]
			{
				Live(2, new[] { Actor(10), Actor(20, x: 11) }, new[] { Member(hp: 90) }),
				Live(3, new[] { Actor(10), Actor(20, x: 11) }, new[] { Member(x: 1, hp: 90) }),
				Live(4, new[] { Actor(10), Actor(20, x: 11) }, new[] { Member(x: 1, range: 5, hp: 90) }),
				Live(5, new[] { Actor(10, hp: 90), Actor(20, x: 11) },
					new[] { Member(x: 1, range: 5, hp: 90) }),
				Live(6, new[] { Actor(10, x: 9, hp: 90), Actor(20, x: 11) },
					new[] { Member(x: 1, range: 5, hp: 90) }),
				Live(7, new[] { Actor(10, x: 9, hp: 90, range: 3), Actor(20, x: 11) },
					new[] { Member(x: 1, range: 5, hp: 90) }),
				Live(8, new[] { Actor(10, x: 9, hp: 90, range: 3), Actor(20, x: 12) },
					new[] { Member(x: 1, range: 5, hp: 90) }),
				Live(9, new[]
				{
					Actor(10, x: 9, hp: 90, range: 3),
					Actor(20, x: 12, range: 4, detector: true)
				},
					new[] { Member(x: 1, range: 5, hp: 90) }),
				Live(10, new[]
				{
					Actor(10, x: 9, hp: 90, range: 3),
					Actor(20, x: 12, range: 4, detector: true)
				},
					new[] { Member(x: 1, range: 5, hp: 90) }, cloaked: false),
				Live(11, new[]
				{
					Actor(10, x: 9, hp: 90, range: 3),
					Actor(20, x: 12, range: 4, detector: true)
				},
					new[] { Member(x: 1, range: 5, hp: 90) },
					new[] { new CPos(5, 0), new CPos(0, 0), new CPos(7, 0) }, false)
			};
			foreach (var change in changes)
			{
				live.Snapshot = change;
				behavior.Execute();
				Assert.That(threats.Facts.Count, Is.GreaterThan(previousFacts));
				previousFacts = threats.Facts.Count;
			}

			Assert.That(orders.Issued, Has.Count.EqualTo(1),
				"The derived Position order is unchanged across these replans.");
			live.Snapshot = Live(12, new[]
			{
				Actor(10, x: 9, hp: 90, range: 3),
				Actor(20, x: 12, range: 4, detector: true)
			},
				new[] { Member(2, x: 1, range: 5, hp: 90) },
				new[] { new CPos(5, 0), new CPos(0, 0), new CPos(7, 0) }, false);
			behavior.Execute();
			Assert.That(threats.Facts.Count, Is.GreaterThan(previousFacts));
			Assert.That(orders.Issued, Has.Count.EqualTo(2),
				"Changed live membership requires a new tokenized order.");
		}

		[Test]
		public void FireCompletionAndWithdrawActivityUseLiveEvidenceWithoutStalling()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1, new[] { Actor(10) },
				observeActivity: true, activityRevision: 1),
				out var live, out _, out var orders);
			behavior.Execute();
			var positionToken = orders.LastToken;
			live.Snapshot = Live(2, new[] { Actor(10) }, observeActivity: true,
				activityRevision: 1, activeOrder: positionToken);
			behavior.Execute();
			Assert.That(orders.Issued, Has.Count.EqualTo(1));

			live.Snapshot = Live(3, new[] { Actor(10) }, new[] { Member(x: 5) },
				observeActivity: true, activityRevision: 1, activeOrder: positionToken);
			Assert.That(behavior.Execute().Phase, Is.EqualTo(StealthKitePhase.Fire));
			var fireToken = orders.LastToken;
			live.Snapshot = Live(4, new[] { Actor(10) }, new[] { Member(x: 5) },
				observeActivity: true, activityRevision: 1, activeOrder: fireToken);
			Assert.That(behavior.Execute().Phase, Is.EqualTo(StealthKitePhase.Fire));
			Assert.That(orders.Issued, Has.Count.EqualTo(2));

			live.Snapshot = Live(5, new[] { Actor(10) }, new[] { Member(x: 5) },
				observeActivity: true, activityRevision: 2, completedOrder: fireToken);
			Assert.That(behavior.Execute().Phase, Is.EqualTo(StealthKitePhase.Withdraw));
			var withdrawToken = orders.LastToken;
			live.Snapshot = Live(6, new[] { Actor(10) }, new[] { Member(x: 5) },
				observeActivity: true, activityRevision: 2, activeOrder: withdrawToken);
			behavior.Execute();
			Assert.That(orders.Issued, Has.Count.EqualTo(3));

			live.Snapshot = Live(7, new[] { Actor(10) }, new[] { Member(x: 5) },
				observeActivity: true, activityRevision: 3);
			Assert.That(behavior.Execute().Phase, Is.EqualTo(StealthKitePhase.Withdraw));
			Assert.That(orders.Issued, Has.Count.EqualTo(4));
			live.Snapshot = Live(8, new[] { Actor(10) }, new[] { Member(x: 5), Member(2, x: 5) },
				observeActivity: true, activityRevision: 4);
			Assert.That(behavior.Execute().Phase, Is.EqualTo(StealthKitePhase.Withdraw));
			Assert.That(orders.Issued, Has.Count.EqualTo(5));
		}

		[Test]
		public void CallbackIssuedThenThrowRetriesSameTokenWithoutExternalDuplicate()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1, new[] { Actor(10) }),
				out _, out _, out var orders);
			orders.OnIssue = () => throw new InvalidOperationException("issued then throw");
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
			Assert.That(orders.Calls, Has.Count.EqualTo(1));
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "LastObservedTick").Value.Value, Is.EqualTo("-1"));

			orders.OnIssue = null;
			behavior.Execute();
			Assert.That(orders.Calls, Has.Count.EqualTo(2));
			Assert.That(orders.Calls[1], Is.EqualTo(orders.Calls[0]));
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "LastObservedTick").Value.Value, Is.EqualTo("1"));
		}

		[Test]
		public void ZeroLiveMemberFallbackRoundTripsAndRevalidatesCurrentCause()
		{
			var input = CreateInput();
			var snapshot = Live(1, new[] { Actor(10) }, new[] { Member(hp: 0) });
			var source = Behavior(input, snapshot, out _, out _, out _);
			var result = source.Execute();
			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.RecalculateFlee));
			Assert.That(result.ActiveMemberActorIds, Is.Empty);
			Assert.That(result.FallbackEvidence.Reason,
				Is.EqualTo(StealthKiteFallbackReason.NoLiveMembers));
			var saved = source.SerializePrivateState();

			var restored = Behavior(input, snapshot, out var live, out _, out _);
			restored.RestorePrivateState(saved);
			live.Snapshot = Live(2, new[] { Actor(10) });
			Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(saved));
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(new List<MiniYamlNode> { saved }.WriteToString()));

			var transitionInput = CreateInput();
			var transitionResult = Behavior(transitionInput, snapshot, out _, out _, out _).Execute();
			Assert.That(transitionInput.Controller.TryAccept(transitionResult, out var transition), Is.True);
			Assert.That(transition.RecalculateFlee, Is.Not.Null);
		}

		[Test]
		public void CanonicalFallbackIsIndependentOfCandidateEnumerationAndControllerChecksThreshold()
		{
			var firstInput = CreateInput();
			var first = Behavior(firstInput, Live(1, new[] { Actor(10), Actor(20) },
				cells: Array.Empty<CPos>()), out _, out var firstThreats, out _);
			firstThreats.EvaluateFallback = facts => new StealthTargetThreatScore(4, 3);
			var firstResult = first.Execute();
			var second = Behavior(CreateInput(), Live(1, new[] { Actor(10), Actor(20) }),
				out _, out var secondThreats, out _);
			secondThreats.Evaluate = facts => new StealthKiteSafetyResult(
				new StealthTargetThreatScore(99, 99), false);
			secondThreats.EvaluateFallback = firstThreats.EvaluateFallback;
			second.Execute();

			Assert.That(firstThreats.FallbackFacts, Has.Count.EqualTo(1));
			Assert.That(secondThreats.FallbackFacts, Has.Count.EqualTo(1));
			Assert.That(secondThreats.FallbackFacts[0].FriendlyActorIds,
				Is.EqualTo(firstThreats.FallbackFacts[0].FriendlyActorIds));
			Assert.That(secondThreats.FallbackFacts[0].EnemyActorIds,
				Is.EqualTo(firstThreats.FallbackFacts[0].EnemyActorIds));
			var forgedEvidence = (StealthKiteFallbackEvidence)Activator.CreateInstance(
				typeof(StealthKiteFallbackEvidence), BindingFlags.Instance | BindingFlags.NonPublic,
				null, new object[]
				{
					StealthKiteFallbackReason.NoSafePlan,
					firstResult.FallbackEvidence.LiveFingerprint,
					firstResult.LiveDefenderActorIds, firstResult.FallbackEvidence.AttackFacts,
					(StealthTargetThreatScore?)new StealthTargetThreatScore(4, 2)
				}, null);
			var rawHandoff = typeof(StealthKiteHandoff).GetProperty("Handoff",
				BindingFlags.Instance | BindingFlags.NonPublic).GetValue(firstInput.Handoff);
			var forged = (StealthKiteResult)Activator.CreateInstance(typeof(StealthKiteResult),
				BindingFlags.Instance | BindingFlags.NonPublic, null, new object[]
				{
					rawHandoff, firstResult.Mission, StealthKiteDisposition.MassAttack,
					firstResult.Phase, firstResult.SelectedTargetActorId,
					firstResult.SelectedTargetCurrentCell, firstResult.FireCell,
					firstResult.WithdrawCell, firstResult.ActiveMemberActorIds,
					firstResult.LiveDefenderActorIds, firstResult.LiveObjectiveActorIds,
					firstResult.Safety, forgedEvidence
				}, null);
			Assert.That(firstInput.Controller.TryAccept(forged, out _), Is.False);
			Assert.That(firstInput.Controller.Owner, Is.EqualTo(BehaviorId.Kite));
		}

		[TestCase("read")]
		[TestCase("threat")]
		[TestCase("order")]
		public void ReentrantExecuteCannotCommitOrDuplicateOrders(string callback)
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1, new[] { Actor(10) }),
				out var live, out var threats, out var orders);
			Action recurse = () => behavior.Execute();
			if (callback == "read") live.OnRead = recurse;
			else if (callback == "threat") threats.OnCalculate = recurse;
			else orders.OnIssue = recurse;

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(orders.Issued, Has.Count.LessThanOrEqualTo(1));
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "LastObservedTick").Value.Value, Is.EqualTo("-1"));
		}

		[TestCase("execute-restore")]
		[TestCase("restore-execute")]
		[TestCase("restore-restore")]
		public void ExecuteAndRestoreRejectAllRecursiveDirections(string direction)
		{
			var input = CreateInput();
			var snapshot = Live(1, new[] { Actor(10) });
			var source = Behavior(input, snapshot, out _, out _, out _);
			source.Execute();
			var saved = source.SerializePrivateState();
			var behavior = Behavior(input, snapshot, out var live, out _, out var orders);
			live.OnRead = direction == "execute-restore" ?
				(Action)(() => behavior.RestorePrivateState(saved)) :
				direction == "restore-execute" ? (Action)(() => behavior.Execute()) :
				() => behavior.RestorePrivateState(saved);

			if (direction == "execute-restore")
				Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			else
				Assert.Throws<InvalidOperationException>(() => behavior.RestorePrivateState(saved));

			Assert.That(orders.Issued, Is.Empty);
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "LastObservedTick").Value.Value, Is.EqualTo("-1"));
		}

		[Test]
		public void StaleOwnershipCallbackFailureAndRevisionExhaustionRollBack()
		{
			var input = CreateInput();
			var guard = new OwnershipProbe();
			var behavior = Behavior(input, Live(1, new[] { Actor(10) }),
				out _, out _, out var orders, guard);
			guard.Active = false;
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			guard.Active = true;
			orders.OnIssue = () => throw new InvalidOperationException("injected order failure");
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "LastObservedTick").Value.Value, Is.EqualTo("-1"));
			orders.OnIssue = null;
			SetRevision(behavior, long.MaxValue);
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
		}

		[Test]
		public void OwnerHasNoCacheClaimOrExclusiveDependencyAndObservationsStayPassive()
		{
			var fields = typeof(StealthKiteBehavior).GetFields(
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			Assert.That(fields.Any(field => field.FieldType.Name.IndexOf(
				"Cache", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
			Assert.That(fields.Any(field => field.Name.IndexOf("claim", StringComparison.OrdinalIgnoreCase) >= 0 ||
				field.Name.IndexOf("exclusive", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
			var input = CreateInput();
			var owner = input.Controller.Owner;
			var epoch = input.Controller.Epoch;
			input.Controller.Observe(new StealthLifecycleObservationFrame(9, new[]
			{
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Timer),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.WorldEvent, 10)
			}));
			Assert.That(input.Controller.Owner, Is.EqualTo(owner));
			Assert.That(input.Controller.Epoch, Is.EqualTo(epoch));
		}
	}
}
