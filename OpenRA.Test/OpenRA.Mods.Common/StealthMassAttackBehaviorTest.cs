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
using System.Text;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class StealthMassAttackBehaviorTest
	{
		sealed class Input
		{
			public StealthLifecycleController Controller;
			public StealthMassAttackHandoff Handoff;
		}

		sealed class LiveProbe : IStealthMassAttackLiveWorld
		{
			public StealthMassAttackLiveSnapshot Snapshot;
			public int Reads;
			public Action OnRead;
			public StealthMassAttackLiveSnapshot Read(StealthApproachMission mission)
			{
				Reads++;
				OnRead?.Invoke();
				return Snapshot;
			}
		}

		sealed class ThreatProbe : IStealthMassAttackThreatAdapter
		{
			public readonly List<StealthMassAttackThreatFacts> Facts =
				new List<StealthMassAttackThreatFacts>();
			public readonly Dictionary<uint, double> TargetThreat = new Dictionary<uint, double>();
			public double Threat = 5;
			public double Crossover = 3;
			public Action OnCalculate;
			public StealthMassAttackThreatResult Calculate(StealthMassAttackThreatFacts facts)
			{
				Facts.Add(facts);
				OnCalculate?.Invoke();
				return new StealthMassAttackThreatResult(
					new StealthTargetThreatScore(Threat, Crossover),
					TargetThreat.TryGetValue(facts.SelectedTargetActorId, out var value) ? value : 1);
			}
		}

		sealed class OrderProbe : IStealthMassAttackOrders
		{
			public readonly List<StealthMassAttackOrderToken> Calls =
				new List<StealthMassAttackOrderToken>();
			public readonly List<StealthMassAttackOrderToken> Issued =
				new List<StealthMassAttackOrderToken>();
			readonly HashSet<StealthMassAttackOrderToken> accepted =
				new HashSet<StealthMassAttackOrderToken>();
			public Action OnIssue;
			public IReadOnlyList<uint> RetainedActors;
			public bool MutationSucceeded;

			public void IssueMove(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint targetActorId, CPos targetCurrentCell,
				StealthMassAttackOrderToken token)
			{
				Accept(actorIds, token);
			}

			public void IssueAttack(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint targetActorId, CPos targetCurrentCell,
				StealthMassAttackOrderToken token)
			{
				Accept(actorIds, token);
			}

			void Accept(IReadOnlyList<uint> actorIds, StealthMassAttackOrderToken token)
			{
				Calls.Add(token);
				if (!accepted.Add(token))
					return;
				RetainedActors = actorIds;
				if (actorIds is IList<uint> list)
				{
					try { list[0] = 999; MutationSucceeded = true; }
					catch (NotSupportedException) { }
				}

				Issued.Add(token);
				OnIssue?.Invoke();
			}
		}

		sealed class OwnershipProbe : IStealthLifecycleOwnershipGuard
		{
			public bool Active = true;
			public bool IsActive(BehaviorId owner, OwnershipEpoch epoch) { return Active; }
		}

		static T CreateInternal<T>(params object[] arguments)
		{
			return (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance | BindingFlags.NonPublic,
				null, arguments, null);
		}

		static StealthApproachMission Mission()
		{
			var cell = new CPos(4, 4);
			var target = new StealthStrategicTargetSnapshot(99, cell, 100, 1000, 100, 100);
			var facts = new StealthTargetThreatFacts(cell,
				new[] { new StealthCombatGroupSnapshot("stnk", 2, 900) },
				new[] { new StealthCombatGroupSnapshot("mtnk", 2, 800) }, true, true, true);
			var option = CreateInternal<StealthTargetOption>(cell, (int?)100, false,
				new[] { target }, facts);
			var value = CreateInternal<StealthTargetValueOption>(option, 1000L);
			var threat = CreateInternal<StealthTargetThreatOption>(value,
				new StealthTargetThreatScore(5, 3));
			return CreateInternal<StealthApproachMission>(threat, 100L, 20, 80L);
		}

		static Input CreateInput(StealthMassAttackLiveSnapshot snapshot,
			double crossover = 3, double threat = 5)
		{
			Assert.That(TryCreateInput(snapshot, crossover, threat, out var input), Is.True);
			return input;
		}

		static bool TryCreateInput(StealthMassAttackLiveSnapshot snapshot,
			double crossover, double threat, out Input input)
		{
			input = null;
			var mission = Mission();
			var members = snapshot.Members.Where(member => member.IsValid)
				.Select(member => member.ActorId).OrderBy(id => id).ToArray();
			var defenders = snapshot.Actors.Where(actor => actor.IsValid &&
				actor.IsInLocalEngagementArea && actor.IsDefender).OrderBy(actor => actor.ActorId).ToArray();
			var sourceTarget = defenders.First();
			var fallbackFacts = new StealthKiteFallbackFacts(sourceTarget.ActorId,
				sourceTarget.CurrentCell, members, defenders.Select(actor => actor.ActorId),
				snapshot.FormationCloaked);
			var fallback = CreateInternal<StealthKiteFallbackEvidence>(
				StealthKiteFallbackReason.NoSafePlan, Fingerprint(snapshot, sourceTarget),
				defenders.Select(actor => actor.ActorId), fallbackFacts,
				(StealthTargetThreatScore?)new StealthTargetThreatScore(threat, crossover));
			var controller = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.Kite, new OwnershipEpoch(7), -1));
			var result = CreateInternal<StealthKiteResult>(controller.CurrentHandoff, mission,
				StealthKiteDisposition.MassAttack, StealthKitePhase.Position,
				(uint?)sourceTarget.ActorId, (CPos?)sourceTarget.CurrentCell, (CPos?)null, (CPos?)null,
				members, defenders.Select(actor => actor.ActorId).ToArray(), Array.Empty<uint>(),
				(StealthKiteSafetyResult?)null, fallback);
			if (!controller.TryAccept(result, out var transition))
				return false;
			Assert.That(transition.MassAttack, Is.Not.Null);
			Assert.That(transition.MassAttackEntry, Is.Not.Null);
			input = new Input { Controller = controller, Handoff = transition.MassAttackEntry };
			return true;
		}

		static StealthMassAttackMemberSnapshot Member(uint id = 1, int x = 0, int range = 4,
			int hp = 100, int maxHp = 100, bool inWorld = true, bool dead = false)
		{
			return new StealthMassAttackMemberSnapshot(id, new CPos(x, 0), range,
				hp, maxHp, inWorld, dead);
		}

		static StealthMassAttackActorSnapshot Actor(uint id, int x = 8, int hp = 100,
			int range = 2, string type = "mtnk", bool defender = true,
			bool objective = false, bool detector = false, bool local = true,
			bool inWorld = true, bool dead = false, bool targetable = true)
		{
			return new StealthMassAttackActorSnapshot(id, type, new CPos(x, 0), hp, 100, range,
				defender, objective, detector, local, inWorld, dead, targetable);
		}

		static StealthMassAttackLiveSnapshot Live(int tick,
			IEnumerable<StealthMassAttackActorSnapshot> actors,
			IEnumerable<StealthMassAttackMemberSnapshot> members = null,
			IEnumerable<CPos> cells = null, bool cloaked = true,
			bool observeActivity = false, long activityRevision = 0,
			StealthMassAttackOrderToken active = null, StealthMassAttackOrderToken completed = null)
		{
			return new StealthMassAttackLiveSnapshot(tick, members ?? new[] { Member() }, actors,
				cells ?? Array.Empty<CPos>(), cloaked, observeActivity, activityRevision, active, completed);
		}

		static string Fingerprint(StealthMassAttackLiveSnapshot live,
			StealthMassAttackActorSnapshot target)
		{
			var defenders = live.Actors.Where(actor => actor.IsValid && actor.IsInLocalEngagementArea &&
				actor.IsDefender).OrderBy(actor => actor.ActorId);
			var text = new StringBuilder();
			text.Append("C=").Append(live.FormationCloaked ? 1 : 0).Append(";R=1;M=");
			foreach (var member in live.Members.OrderBy(member => member.ActorId))
				text.Append(member.ActorId).Append(',').Append(member.CurrentCell.Bits).Append(',')
					.Append(member.CurrentWeaponRangeCells).Append(',').Append(member.HitPoints).Append(',')
					.Append(member.MaximumHitPoints).Append(',').Append(member.IsInWorld ? 1 : 0).Append(',')
					.Append(member.IsDead ? 1 : 0).Append('|');
			text.Append(";T=").Append(target.ActorId).Append(',').Append(target.CurrentCell.Bits).Append(',')
				.Append(target.HitPoints).Append(',').Append(target.MaximumHitPoints).Append(',')
				.Append(target.CurrentWeaponRangeCells).Append(";E=");
			foreach (var enemy in defenders)
				text.Append(enemy.ActorId).Append(',').Append(enemy.CurrentCell.Bits).Append(',')
					.Append(enemy.HitPoints).Append(',').Append(enemy.MaximumHitPoints).Append(',')
					.Append(enemy.CurrentWeaponRangeCells).Append(',')
					.Append(enemy.HasDetectorCoverage ? 1 : 0).Append('|');
			text.Append(";P=");
			foreach (var cell in live.CandidateCells)
				text.Append(cell.Bits).Append('|');
			return text.ToString();
		}

		static StealthMassAttackBehavior Behavior(Input input,
			StealthMassAttackLiveSnapshot snapshot, out LiveProbe live, out ThreatProbe threats,
			out OrderProbe orders, IStealthLifecycleOwnershipGuard guard = null)
		{
			live = new LiveProbe { Snapshot = snapshot };
			threats = new ThreatProbe();
			orders = new OrderProbe();
			return new StealthMassAttackBehavior(input.Handoff, guard ?? input.Controller,
				live, threats, orders);
		}

		static void SetRevision(StealthMassAttackBehavior behavior, long revision)
		{
			var lease = typeof(StealthMassAttackBehavior).GetField("executionLease",
				BindingFlags.Instance | BindingFlags.NonPublic).GetValue(behavior);
			lease.GetType().GetField("revision", BindingFlags.Instance | BindingFlags.NonPublic)
				.SetValue(lease, revision);
		}

		static void ForgeAutoProperty<T>(object value, string property, T forged)
		{
			value.GetType().GetField("<" + property + ">k__BackingField",
				BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, forged);
		}

		[Test]
		public void ExactTypedEntryUsesHighestStandardThreatAndExplicitDetectorExposure()
		{
			var snapshot = Live(1, new[]
			{
				Actor(10, detector: true), Actor(20, x: 9), Actor(30, x: 3, local: false)
			}, new[] { Member(2), Member(1) });
			var input = CreateInput(snapshot);
			var behavior = Behavior(input, snapshot, out _, out var threats, out var orders);
			threats.TargetThreat[10] = 4;
			threats.TargetThreat[20] = 9;

			var result = behavior.Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(20));
			Assert.That(result.Disposition, Is.EqualTo(StealthMassAttackDisposition.Retain));
			Assert.That(threats.Facts, Has.Count.EqualTo(3));
			Assert.That(threats.Facts.All(facts => facts.PlannedReveal && facts.PlannedAttack &&
				facts.FullCurrentFiringRangeExposure && facts.HasDetectorCoverage), Is.True);
			Assert.That(orders.Issued.Single().Owner, Is.EqualTo(BehaviorId.MassAttack));
			Assert.That(orders.Issued.Single().ActorIds, Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(orders.MutationSucceeded, Is.False);
		}

		[Test]
		public void KiteEntryThresholdIsStrictlyGreaterThanTwo()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			Assert.That(TryCreateInput(snapshot, 2, 5, out _), Is.False);
			Assert.That(TryCreateInput(snapshot, 2.0001, 5, out var accepted), Is.True);
			Assert.That(accepted.Handoff.Evidence.StandardScore.Crossover, Is.EqualTo(2.0001));
		}

		[Test]
		public void DeterministicTieRetainsMovingDamagedTargetUntilKillThenSelectsHighest()
		{
			var snapshot = Live(1, new[] { Actor(10), Actor(20, x: 9), Actor(30, x: 10) });
			var input = CreateInput(snapshot);
			var behavior = Behavior(input, snapshot, out var live, out var threats, out var orders);
			threats.TargetThreat[10] = 8;
			threats.TargetThreat[20] = 8;
			threats.TargetThreat[30] = 2;
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(10));
			live.Snapshot = Live(2, new[] { Actor(10, x: 7, hp: 25), Actor(20, x: 9), Actor(30, x: 10) });
			threats.TargetThreat[20] = 99;
			var retained = behavior.Execute();
			Assert.That(retained.SelectedTargetActorId, Is.EqualTo(10));
			Assert.That(retained.SelectedTargetCurrentCell, Is.EqualTo(new CPos(7, 0)));
			Assert.That(retained.ThreatFacts.Enemies.Single(actor => actor.ActorId == 10).HitPoints,
				Is.EqualTo(25));
			live.Snapshot = Live(3, new[]
			{
				Actor(10, dead: true), Actor(20, x: 9), Actor(30, x: 10)
			});
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(20));
			Assert.That(orders.Issued.Select(token => token.TargetActorId),
				Is.EqualTo(new uint[] { 10, 10, 20 }));
		}

		[TestCase(false, false)]
		[TestCase(true, false)]
		[TestCase(false, true)]
		public void DeadOutOfWorldOrUntargetableTargetIsInvalidated(
			bool outOfWorld, bool untargetable)
		{
			var snapshot = Live(1, new[] { Actor(10), Actor(20) });
			var behavior = Behavior(CreateInput(snapshot), snapshot,
				out var live, out var threats, out _);
			threats.TargetThreat[10] = 9;
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(10));
			live.Snapshot = Live(2, new[]
			{
				Actor(10, inWorld: !outOfWorld, dead: !outOfWorld && !untargetable,
					targetable: !untargetable), Actor(20)
			});
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(20));
		}

		[Test]
		public void AdvanceAttackAndUnchangedUsefulOrderDoNotChurn()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var input = CreateInput(snapshot);
			var behavior = Behavior(input, snapshot, out var live, out _, out var orders);
			Assert.That(behavior.Execute().Phase, Is.EqualTo(StealthMassAttackPhase.Advance));
			var advance = orders.Issued.Single();
			live.Snapshot = Live(1, new[] { Actor(10) }, observeActivity: true, active: advance);
			behavior.Execute();
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
			live.Snapshot = Live(2, new[] { Actor(10, hp: 75) }, observeActivity: true, active: advance);
			behavior.Execute();
			Assert.That(orders.Issued, Has.Count.EqualTo(1), "Damage reevaluation must retain a useful token.");
			live.Snapshot = Live(3, new[] { Actor(10, hp: 75) }, new[] { Member(x: 5) },
				observeActivity: true, activityRevision: 1, completed: advance);
			Assert.That(behavior.Execute().Phase, Is.EqualTo(StealthMassAttackPhase.Attack));
			var attack = orders.Issued.Last();
			live.Snapshot = Live(3, new[] { Actor(10, hp: 75) }, new[] { Member(x: 5) },
				observeActivity: true, activityRevision: 1, active: attack);
			behavior.Execute();
			Assert.That(orders.Issued, Has.Count.EqualTo(2));
		}

		[Test]
		public void NonExclusiveSquadsTeamUpOnSameLiveActor()
		{
			var snapshot = Live(1, new[] { Actor(10), Actor(20) });
			var first = Behavior(CreateInput(snapshot), snapshot, out _, out var firstThreats, out _);
			var second = Behavior(CreateInput(snapshot), snapshot, out _, out var secondThreats, out _);
			firstThreats.TargetThreat[10] = secondThreats.TargetThreat[10] = 9;
			Assert.That(first.Execute().SelectedTargetActorId, Is.EqualTo(10));
			Assert.That(second.Execute().SelectedTargetActorId, Is.EqualTo(10));
		}

		[Test]
		public void LiveInputsEvidenceResultsAndOrderActorsAreImmutableCopies()
		{
			var members = new[] { Member(2), Member(1) };
			var actors = new[] { Actor(10) };
			var cells = new[] { new CPos(3, 0) };
			var snapshot = Live(1, actors, members, cells);
			members[0] = Member(99);
			actors[0] = Actor(99);
			cells[0] = new CPos(99, 0);
			var input = CreateInput(snapshot);
			var result = Behavior(input, snapshot, out _, out _, out var orders).Execute();
			Assert.That(result.ActiveMemberActorIds, Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(result.LiveDefenderActorIds, Is.EqualTo(new uint[] { 10 }));
			Assert.That(input.Handoff.Evidence.FriendlyActorIds, Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(result.ActiveMemberActorIds is uint[], Is.False);
			Assert.That(input.Handoff.Evidence.FriendlyActorIds is uint[], Is.False);
			Assert.That(orders.MutationSucceeded, Is.False);
		}

		[TestCase(2.0)]
		[TestCase(1.01)]
		public void RemainsCommittedThroughoutOneToTwoCrossover(double crossover)
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var input = CreateInput(snapshot);
			var behavior = Behavior(input, snapshot, out var live, out var threats, out _);
			behavior.Execute();
			live.Snapshot = Live(2, new[] { Actor(10, hp: 99) });
			threats.Crossover = crossover;
			var result = behavior.Execute();
			Assert.That(result.Disposition, Is.EqualTo(StealthMassAttackDisposition.Retain));
			Assert.That(input.Controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.Retained, Is.Not.Null);
			Assert.That(input.Controller.Owner, Is.EqualTo(BehaviorId.MassAttack));
		}

		[Test]
		public void UnchangedSnapshotStillRefreshesTheCurrentStandardLiveCrossover()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var behavior = Behavior(CreateInput(snapshot), snapshot, out _, out var threats, out _);
			behavior.Execute();
			var previousCalls = threats.Facts.Count;
			threats.Crossover = 1;
			var refreshed = behavior.Execute();
			Assert.That(threats.Facts.Count, Is.GreaterThan(previousCalls));
			Assert.That(refreshed.Disposition, Is.EqualTo(StealthMassAttackDisposition.RecalculateFlee));
		}

		[Test]
		public void ExitsAtOneOrZeroMembersAndUsesUnchangedMissionHandoffs()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var crossoverInput = CreateInput(snapshot);
			var crossover = Behavior(crossoverInput, snapshot, out var live, out var threats, out _);
			crossover.Execute();
			live.Snapshot = Live(2, new[] { Actor(10, hp: 99) });
			threats.Crossover = 1;
			var flee = crossover.Execute();
			Assert.That(flee.Disposition, Is.EqualTo(StealthMassAttackDisposition.RecalculateFlee));
			Assert.That(crossoverInput.Controller.TryAccept(flee, out var fleeTransition), Is.True);
			Assert.That(fleeTransition.RecalculateFlee, Is.Not.Null);
			Assert.That(fleeTransition.RecalculateFleeEntry, Is.Not.Null);
			Assert.That(fleeTransition.RecalculateFleeEntry.Mission, Is.SameAs(flee.Mission));

			var zeroInput = CreateInput(snapshot);
			var zero = Behavior(zeroInput, snapshot, out var zeroLive, out _, out _);
			zero.Execute();
			zeroLive.Snapshot = Live(2, new[] { Actor(10) }, new[] { Member(hp: 0) });
			var zeroResult = zero.Execute();
			Assert.That(zeroResult.Disposition, Is.EqualTo(StealthMassAttackDisposition.RecalculateFlee));
			Assert.That(zeroInput.Controller.TryAccept(zeroResult, out var zeroTransition), Is.True);
			Assert.That(zeroTransition.SquadConstruction, Is.Not.Null);
			Assert.That(zeroTransition.SquadConstructionEntry.Mission, Is.SameAs(zeroResult.Mission));
			Assert.That(zeroTransition.RecalculateFlee, Is.Null);

			foreach (var actors in new[]
			{
				new[] { Actor(90, defender: false, objective: true) },
				Array.Empty<StealthMassAttackActorSnapshot>()
			})
			{
				var noMembersInput = CreateInput(snapshot);
				var zeroMembers = Live(2, actors, new[] { Member(hp: 0) });
				var noMembers = Behavior(noMembersInput, zeroMembers, out _, out _, out _);
				var result = noMembers.Execute();
				Assert.That(result.Disposition, Is.EqualTo(StealthMassAttackDisposition.RecalculateFlee));
				Assert.That(result.Threat, Is.Null);
				Assert.That(result.LastOrderToken, Is.Null);
				Assert.That(noMembersInput.Controller.TryAccept(result, out var transition), Is.True);
				Assert.That(transition.SquadConstruction, Is.Not.Null);
				Assert.That(transition.SquadConstructionEntry.Mission, Is.SameAs(result.Mission));
				Assert.That(transition.RecalculateFlee, Is.Null);
			}

			var objectiveInput = CreateInput(snapshot);
			var objective = Behavior(objectiveInput, snapshot, out var objectiveLive, out _, out _);
			objective.Execute();
			objectiveLive.Snapshot = Live(2, new[] { Actor(90, defender: false, objective: true) });
			var undefended = objective.Execute();
			Assert.That(objectiveInput.Controller.TryAccept(undefended, out var objectiveTransition), Is.True);
			Assert.That(objectiveTransition.UndefendedAttack.Mission, Is.SameAs(objectiveInput.Handoff.Mission));

			var emptyInput = CreateInput(snapshot);
			var empty = Behavior(emptyInput, snapshot, out var emptyLive, out _, out _);
			empty.Execute();
			emptyLive.Snapshot = Live(2, Array.Empty<StealthMassAttackActorSnapshot>());
			var reacquire = empty.Execute();
			Assert.That(emptyInput.Controller.TryAccept(reacquire, out var emptyTransition), Is.True);
			Assert.That(emptyTransition.Reacquisition, Is.Not.Null);
			Assert.That(reacquire.Mission, Is.SameAs(emptyInput.Handoff.Mission));
		}

		[Test]
		public void SkippedZeroEntryClearsActivityRoundTripsAndRejectsMembersReappearing()
		{
			var entry = Live(1, new[] { Actor(10) });
			var input = CreateInput(entry);
			var active = CreateInternal<StealthMassAttackOrderToken>(BehaviorId.MassAttack,
				input.Handoff.Epoch, StealthMassAttackPhase.Advance, 5L, 2L,
				new uint[] { 1 }, 10u, new CPos(8, 0));
			var completed = CreateInternal<StealthMassAttackOrderToken>(BehaviorId.MassAttack,
				input.Handoff.Epoch, StealthMassAttackPhase.Attack, 4L, 1L,
				new uint[] { 1 }, 10u, new CPos(8, 0));
			var zeroLive = Live(2, new[] { Actor(10) }, new[] { Member(hp: 0) },
				observeActivity: true, activityRevision: 5, active: active, completed: completed);
			var zero = Behavior(input, zeroLive, out _, out var threats, out var orders);
			var result = zero.Execute();
			Assert.That(result.Disposition, Is.EqualTo(StealthMassAttackDisposition.RecalculateFlee));
			Assert.That(threats.Facts, Is.Empty);
			Assert.That(orders.Issued, Is.Empty);
			var text = new List<MiniYamlNode> { zero.SerializePrivateState() }.WriteToString();
			Assert.That(text, Does.Contain("EntryState: SkippedZeroMembers"));
			Assert.That(text, Does.Contain("HasActivityObservation: False"));
			Assert.That(text, Does.Not.Contain("ActiveOrder:"));
			Assert.That(text, Does.Not.Contain("CompletedOrder:"));

			var restored = Behavior(input, zeroLive, out _, out _, out _);
			restored.RestorePrivateState(MiniYaml.FromString(text).Single());
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(text));
			var membersReturned = Behavior(input, Live(2, new[] { Actor(10) }), out _, out _, out _);
			Assert.Throws<InvalidOperationException>(() => membersReturned.RestorePrivateState(
				MiniYaml.FromString(text).Single()));
		}

		[Test]
		public void TargetlessResultConstructionAndControllerRejectMalformedEvidence()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var input = CreateInput(snapshot);
			var behavior = Behavior(input, snapshot, out var live, out _, out _);
			var retained = behavior.Execute();
			live.Snapshot = Live(2, Array.Empty<StealthMassAttackActorSnapshot>());
			var result = behavior.Execute();
			Assert.Throws<TargetInvocationException>(() => CreateInternal<StealthMassAttackResult>(
				input.Handoff, result.Mission, result.Disposition, StealthMassAttackPhase.Attack,
				(uint?)null, (CPos?)null, result.ActiveMemberActorIds, result.LiveDefenderActorIds,
				result.LiveObjectiveActorIds, null, (StealthMassAttackThreatResult?)null, null));
			Assert.Throws<TargetInvocationException>(() => CreateInternal<StealthMassAttackResult>(
				input.Handoff, Mission(), result.Disposition, result.Phase,
				(uint?)null, (CPos?)null, result.ActiveMemberActorIds, result.LiveDefenderActorIds,
				result.LiveObjectiveActorIds, null, (StealthMassAttackThreatResult?)null, null));
			Assert.Throws<TargetInvocationException>(() => CreateInternal<StealthMassAttackResult>(
				input.Handoff, result.Mission, result.Disposition, result.Phase,
				(uint?)10, (CPos?)new CPos(8, 0), result.ActiveMemberActorIds,
				result.LiveDefenderActorIds, result.LiveObjectiveActorIds, retained.ThreatFacts,
				retained.Threat, retained.LastOrderToken));

			ForgeAutoProperty(result, "Phase", StealthMassAttackPhase.Attack);
			Assert.That(input.Controller.TryAccept(result, out _), Is.False);

			var secondInput = CreateInput(snapshot);
			var second = Behavior(secondInput, snapshot, out var secondLive, out _, out _);
			second.Execute();
			secondLive.Snapshot = Live(2, Array.Empty<StealthMassAttackActorSnapshot>());
			var forgedCell = second.Execute();
			ForgeAutoProperty(forgedCell, "SelectedTargetCurrentCell", (CPos?)new CPos(8, 0));
			Assert.That(secondInput.Controller.TryAccept(forgedCell, out _), Is.False);

			var thirdInput = CreateInput(snapshot);
			var third = Behavior(thirdInput, snapshot, out var thirdLive, out _, out _);
			var targeted = third.Execute();
			thirdLive.Snapshot = Live(2, Array.Empty<StealthMassAttackActorSnapshot>());
			var forgedThreat = third.Execute();
			ForgeAutoProperty(forgedThreat, "ThreatFacts", targeted.ThreatFacts);
			Assert.That(thirdInput.Controller.TryAccept(forgedThreat, out _), Is.False);

			var fourthInput = CreateInput(snapshot);
			var fourth = Behavior(fourthInput, snapshot, out var fourthLive, out _, out _);
			fourth.Execute();
			fourthLive.Snapshot = Live(2, Array.Empty<StealthMassAttackActorSnapshot>());
			var forgedMission = fourth.Execute();
			ForgeAutoProperty(forgedMission, "Mission", Mission());
			Assert.That(fourthInput.Controller.TryAccept(forgedMission, out _), Is.False);
		}

		[Test]
		public void EntryRejectsStaleFingerprintParticipantsCellAndStandardScoreTransactionally()
		{
			var snapshot = Live(1, new[] { Actor(10), Actor(20) });
			var variants = new[]
			{
				Live(1, new[] { Actor(10, hp: 99), Actor(20) }),
				Live(1, new[] { Actor(10, x: 7), Actor(20) }),
				Live(1, new[] { Actor(10), Actor(20) }, new[] { Member(2) })
			};
			foreach (var variant in variants)
			{
				var behavior = Behavior(CreateInput(snapshot), variant, out _, out _, out var orders);
				Assert.Throws<InvalidOperationException>(() => behavior.Execute());
				Assert.That(orders.Issued, Is.Empty);
			}

			var input = CreateInput(snapshot);
			var score = Behavior(input, snapshot, out _, out var threats, out var scoreOrders);
			threats.Threat = 6;
			Assert.Throws<InvalidOperationException>(() => score.Execute());
			Assert.That(scoreOrders.Issued, Is.Empty);
		}

		[Test]
		public void CallbackIssuedThenThrowRetriesIdenticalTokenWithoutExternalDuplicate()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var behavior = Behavior(CreateInput(snapshot), snapshot, out _, out _, out var orders);
			orders.OnIssue = () => throw new InvalidOperationException("issued then throw");
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			orders.OnIssue = null;
			behavior.Execute();
			Assert.That(orders.Calls, Has.Count.EqualTo(2));
			Assert.That(orders.Calls[1], Is.EqualTo(orders.Calls[0]));
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
		}

		[Test]
		public void ExactActiveSuppressesAndCompletedOrLostActivityCreatesDeterministicAttempts()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var behavior = Behavior(CreateInput(snapshot), snapshot, out var live, out _, out var orders);
			behavior.Execute();
			var first = orders.Issued.Single();
			live.Snapshot = Live(2, new[] { Actor(10) }, observeActivity: true, active: first);
			behavior.Execute();
			Assert.That(orders.Calls, Has.Count.EqualTo(1));

			live.Snapshot = Live(3, new[] { Actor(10) }, observeActivity: true,
				activityRevision: 1, completed: first);
			behavior.Execute();
			var completedRetry = orders.Issued.Last();
			Assert.That(completedRetry.ActivityRevision, Is.EqualTo(1));
			Assert.That(completedRetry.AttemptRevision, Is.EqualTo(1));
			var completedState = behavior.SerializePrivateState();
			var completedRoundTrip = Behavior(CreateInput(snapshot), live.Snapshot,
				out _, out _, out _);
			completedRoundTrip.RestorePrivateState(completedState);

			live.Snapshot = Live(4, new[] { Actor(10) }, observeActivity: true,
				activityRevision: 2);
			behavior.Execute();
			var lostCorrection = orders.Issued.Last();
			Assert.That(lostCorrection.ActivityRevision, Is.EqualTo(2));
			Assert.That(lostCorrection.AttemptRevision, Is.EqualTo(2));
			var lostRoundTrip = Behavior(CreateInput(snapshot), live.Snapshot, out _, out _, out _);
			lostRoundTrip.RestorePrivateState(behavior.SerializePrivateState());
		}

		[Test]
		public void ExactActiveValidatesCanonicalAndUnrelatedCompletedTokensBeforeSuppression()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var input = CreateInput(snapshot);
			var behavior = Behavior(input, snapshot, out var live, out _, out var orders);
			behavior.Execute();
			var first = orders.Issued.Single();
			live.Snapshot = Live(2, new[] { Actor(10) }, observeActivity: true,
				activityRevision: 1, completed: first);
			behavior.Execute();
			var active = orders.Issued.Last();

			live.Snapshot = Live(3, new[] { Actor(10) }, observeActivity: true,
				activityRevision: 1, active: active, completed: first);
			behavior.Execute();
			Assert.That(orders.Calls, Has.Count.EqualTo(2));
			var canonical = behavior.SerializePrivateState();
			var roundTrip = Behavior(CreateInput(snapshot), live.Snapshot, out _, out _, out _);
			roundTrip.RestorePrivateState(canonical);

			live.Snapshot = Live(4, new[] { Actor(10) }, observeActivity: true,
				activityRevision: 1, active: active, completed: active);
			behavior.Execute();
			Assert.That(orders.Calls, Has.Count.EqualTo(2));
			var before = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var unrelated = CreateInternal<StealthMassAttackOrderToken>(BehaviorId.MassAttack,
				input.Handoff.Epoch, StealthMassAttackPhase.Advance, 1L, 0L,
				new uint[] { 1 }, 99u, new CPos(9, 0));
			live.Snapshot = Live(5, new[] { Actor(10) }, observeActivity: true,
				activityRevision: 1, active: active, completed: unrelated);
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(orders.Calls, Has.Count.EqualTo(2));
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void PriorActivityAdvancesMovedTargetOnceAndRetriesExactSuccessor(bool completed)
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var behavior = Behavior(CreateInput(snapshot), snapshot,
				out var live, out _, out var orders);
			behavior.Execute();
			var prior = orders.Issued.Single();
			live.Snapshot = Live(2, new[] { Actor(10, x: 7) }, observeActivity: true,
				active: completed ? null : prior, completed: completed ? prior : null);
			orders.OnIssue = () => throw new InvalidOperationException("issued changed action then throw");
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			var attempted = orders.Calls.Last();
			Assert.That(attempted.TargetCurrentCell, Is.EqualTo(new CPos(7, 0)));
			Assert.That(attempted.AttemptRevision, Is.EqualTo(1));
			orders.OnIssue = null;
			behavior.Execute();
			Assert.That(orders.Calls.Last(), Is.EqualTo(attempted));
			Assert.That(orders.Issued, Has.Count.EqualTo(2));
			behavior.Execute();
			Assert.That(orders.Calls.Last(), Is.EqualTo(attempted));
			Assert.That(orders.Issued, Has.Count.EqualTo(2));

			var bridged = behavior.SerializePrivateState();
			Assert.That(bridged.Value.Nodes.Any(node => node.Key == "PriorOrder"), Is.True);
			var roundTrip = Behavior(CreateInput(snapshot), live.Snapshot,
				out _, out _, out var roundTripOrders);
			roundTrip.RestorePrivateState(bridged);
			roundTrip.Execute();
			Assert.That(roundTripOrders.Issued.Single(), Is.EqualTo(attempted));

			var forged = MiniYaml.FromString(new List<MiniYamlNode> { bridged }.WriteToString()).Single();
			forged.Value.Nodes.Single(node => node.Key == "PriorOrder").Value.Nodes
				.Single(node => node.Key == "AttemptRevision").Value.Value = "9";
			var rejected = Behavior(CreateInput(snapshot), live.Snapshot,
				out _, out _, out var rejectedOrders);
			var rejectedBefore = rejected.SerializePrivateState();
			Assert.Throws<InvalidOperationException>(() => rejected.RestorePrivateState(forged));
			Assert.That(rejectedOrders.Issued, Is.Empty);
			Assert.That(new List<MiniYamlNode> { rejected.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(new List<MiniYamlNode> { rejectedBefore }.WriteToString()));

			live.Snapshot = Live(3, new[] { Actor(10, x: 7) }, observeActivity: true,
				active: attempted, completed: prior);
			behavior.Execute();
			Assert.That(orders.Issued, Has.Count.EqualTo(2));
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Any(
				node => node.Key == "PriorOrder"), Is.False);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void PriorActivityAdvancesOnceAfterKilledTargetReselection(bool completed)
		{
			var snapshot = Live(1, new[] { Actor(10), Actor(20) });
			var behavior = Behavior(CreateInput(snapshot), snapshot,
				out var live, out _, out var orders);
			behavior.Execute();
			var prior = orders.Issued.Single();
			live.Snapshot = Live(2, new[] { Actor(10, dead: true), Actor(20) }, observeActivity: true,
				active: completed ? null : prior, completed: completed ? prior : null);
			var result = behavior.Execute();
			var successor = orders.Issued.Last();
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(20));
			Assert.That(successor.TargetActorId, Is.EqualTo(20));
			Assert.That(successor.AttemptRevision, Is.EqualTo(1));
			behavior.Execute();
			Assert.That(orders.Calls.Last(), Is.EqualTo(successor));
			Assert.That(orders.Issued, Has.Count.EqualTo(2));
		}

		[Test]
		public void ChangedDesiredActionRejectsUnrelatedPriorActivityTransactionally()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var input = CreateInput(snapshot);
			var behavior = Behavior(input, snapshot, out var live, out _, out var orders);
			behavior.Execute();
			var unrelated = CreateInternal<StealthMassAttackOrderToken>(BehaviorId.MassAttack,
				input.Handoff.Epoch, StealthMassAttackPhase.Advance, 0L, 0L,
				new uint[] { 1 }, 99u, new CPos(9, 0));
			live.Snapshot = Live(2, new[] { Actor(10, x: 7) }, observeActivity: true,
				active: unrelated);
			var before = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));
		}

		[Test]
		public void CrossoverExitValidatesActivityThenClearsAndRoundTripsTerminalState()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var input = CreateInput(snapshot);
			var behavior = Behavior(input, snapshot, out var live, out var threats, out var orders);
			behavior.Execute();
			var active = orders.Issued.Single();
			var unrelated = CreateInternal<StealthMassAttackOrderToken>(BehaviorId.MassAttack,
				input.Handoff.Epoch, StealthMassAttackPhase.Advance, 0L, 0L,
				new uint[] { 1 }, 99u, new CPos(9, 0));
			live.Snapshot = Live(2, new[] { Actor(10) }, observeActivity: true,
				active: active, completed: unrelated);
			threats.Crossover = 1;
			var before = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(orders.Calls, Has.Count.EqualTo(1));
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(before));

			live.Snapshot = Live(2, new[] { Actor(10) }, observeActivity: true, active: active);
			var result = behavior.Execute();
			Assert.That(result.Disposition, Is.EqualTo(StealthMassAttackDisposition.RecalculateFlee));
			Assert.That(result.LastOrderToken, Is.Null);
			var text = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			Assert.That(text, Does.Contain("EntryState: ExitedRecalculate"));
			Assert.That(text, Does.Contain("HasActivityObservation: False"));
			Assert.That(text, Does.Contain("LastOrder:"));

			var restoredInput = CreateInput(snapshot);
			var restored = Behavior(restoredInput, live.Snapshot,
				out _, out var restoredThreats, out _);
			restoredThreats.Crossover = 1;
			restored.RestorePrivateState(MiniYaml.FromString(text).Single());
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(text));
			foreach (var invalidLive in new[]
			{
				Live(2, new[] { Actor(10) }, observeActivity: true, active: unrelated),
				Live(2, new[] { Actor(10) }, observeActivity: true,
					active: active, completed: unrelated)
			})
			{
				var invalidInput = CreateInput(snapshot);
				var invalid = Behavior(invalidInput, invalidLive,
					out _, out var invalidThreats, out var invalidOrders);
				invalidThreats.Crossover = 1;
				var pristine = new List<MiniYamlNode> { invalid.SerializePrivateState() }.WriteToString();
				Assert.Throws<InvalidOperationException>(() => invalid.RestorePrivateState(
					MiniYaml.FromString(text).Single()));
				Assert.That(invalidOrders.Issued, Is.Empty);
				Assert.That(new List<MiniYamlNode> { invalid.SerializePrivateState() }.WriteToString(),
					Is.EqualTo(pristine));
			}

			Assert.That(input.Controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.RecalculateFlee, Is.Not.Null);
			Assert.That(transition.RecalculateFleeEntry, Is.Not.Null);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void TerminalExitPersistsAcceptedPriorAcrossMovementOrReselection(bool killed)
		{
			var snapshot = Live(1, new[] { Actor(10), Actor(20) });
			var input = CreateInput(snapshot);
			var behavior = Behavior(input, snapshot, out var live, out var threats, out var orders);
			behavior.Execute();
			var prior = orders.Issued.Single();
			var currentEnemies = killed ?
				new[] { Actor(10, dead: true), Actor(20) } :
				new[] { Actor(10, x: 7), Actor(20) };
			live.Snapshot = Live(2, currentEnemies, observeActivity: true, active: prior);
			behavior.Execute();
			var successor = orders.Issued.Last();
			Assert.That(successor.AttemptRevision, Is.EqualTo(1));
			live.Snapshot = Live(3, currentEnemies, observeActivity: true, active: prior);
			threats.Crossover = 1;
			var result = behavior.Execute();
			Assert.That(result.LastOrderToken, Is.Null);
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(killed ? 20u : 10u));
			var saved = behavior.SerializePrivateState();
			Assert.That(saved.Value.Nodes.Any(node => node.Key == "LastOrder"), Is.True);
			Assert.That(saved.Value.Nodes.Any(node => node.Key == "PriorOrder"), Is.True);

			var restored = Behavior(CreateInput(snapshot), live.Snapshot,
				out _, out var restoredThreats, out var restoredOrders);
			restoredThreats.Crossover = 1;
			restored.RestorePrivateState(saved);
			Assert.That(restoredOrders.Issued, Is.Empty);

			var forged = MiniYaml.FromString(new List<MiniYamlNode> { saved }.WriteToString()).Single();
			forged.Value.Nodes.Single(node => node.Key == "PriorOrder").Value.Nodes
				.Single(node => node.Key == "AttemptRevision").Value.Value = "9";
			var invalid = Behavior(CreateInput(snapshot), live.Snapshot,
				out var invalidLive, out var invalidThreats, out var invalidOrders);
			invalidThreats.Crossover = 1;
			var pristine = new List<MiniYamlNode> { invalid.SerializePrivateState() }.WriteToString();
			Assert.Throws<InvalidOperationException>(() => invalid.RestorePrivateState(forged));
			Assert.That(invalidOrders.Issued, Is.Empty);
			Assert.That(new List<MiniYamlNode> { invalid.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(pristine));

			var unrelated = CreateInternal<StealthMassAttackOrderToken>(BehaviorId.MassAttack,
				input.Handoff.Epoch, StealthMassAttackPhase.Advance, 0L, 0L,
				new uint[] { 1 }, 99u, new CPos(9, 0));
			invalidLive.Snapshot = Live(3, currentEnemies, observeActivity: true, active: unrelated);
			Assert.Throws<InvalidOperationException>(() => invalid.RestorePrivateState(saved));
			Assert.That(invalidOrders.Issued, Is.Empty);
			Assert.That(new List<MiniYamlNode> { invalid.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(pristine));
		}

		[Test]
		public void InfiniteSelectedTargetThreatRejectsTransactionally()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var behavior = Behavior(CreateInput(snapshot), snapshot,
				out _, out var threats, out var orders);
			threats.Threat = double.PositiveInfinity;
			Assert.Throws<ArgumentOutOfRangeException>(() => behavior.Execute());
			Assert.That(orders.Issued, Is.Empty);
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "EntryState").Value.Value, Is.EqualTo("Pristine"));
		}

		[Test]
		public void UnrelatedLiveActivityIsRejectedWithoutCommit()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var input = CreateInput(snapshot);
			var behavior = Behavior(input, snapshot, out var live, out _, out var orders);
			behavior.Execute();
			var unrelatedTarget = CreateInternal<StealthMassAttackOrderToken>(BehaviorId.MassAttack,
				input.Handoff.Epoch, StealthMassAttackPhase.Advance, 1L, 0L,
				new uint[] { 1 }, 99u, new CPos(9, 0));
			var unrelatedAttempt = CreateInternal<StealthMassAttackOrderToken>(BehaviorId.MassAttack,
				input.Handoff.Epoch, StealthMassAttackPhase.Advance, 1L, 9L,
				new uint[] { 1 }, 10u, new CPos(8, 0));
			foreach (var unrelated in new[] { unrelatedTarget, unrelatedAttempt })
			{
				live.Snapshot = Live(2, new[] { Actor(10) }, observeActivity: true,
					activityRevision: 1, active: unrelated);
				Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			}

			Assert.That(orders.Issued, Has.Count.EqualTo(1));
		}

		[Test]
		public void PristinePersistenceIsDistinctAndRejectsForgedValidationTimeAndPhase()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var input = CreateInput(snapshot);
			var pristine = Behavior(input, snapshot, out _, out var threats, out _);
			var text = new List<MiniYamlNode> { pristine.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input, snapshot, out _, out _, out _);
			restored.RestorePrivateState(MiniYaml.FromString(text).Single());
			Assert.That(threats.Facts, Is.Empty);
			foreach (var invalid in new[]
			{
				text.Replace("EntryState: Pristine", "EntryState: Validated"),
				text.Replace("LastObservedTick: -1", "LastObservedTick: 0"),
				text.Replace("Phase: Advance", "Phase: Attack")
			})
				Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(
					MiniYaml.FromString(invalid).Single()));
		}

		[Test]
		public void RestoreUsesExactSavedCurrentStateAfterMovementReselectionAndTargetlessExit()
		{
			var snapshot = Live(1, new[] { Actor(10), Actor(20) });
			var input = CreateInput(snapshot);
			var behavior = Behavior(input, snapshot, out var live, out _, out _);
			behavior.Execute();
			var moved = Live(2, new[] { Actor(10, x: 7, hp: 90), Actor(20) });
			live.Snapshot = moved;
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(10));
			var movedText = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var movedRestore = Behavior(input, moved, out _, out _, out _);
			movedRestore.RestorePrivateState(MiniYaml.FromString(movedText).Single());
			Assert.That(new List<MiniYamlNode> { movedRestore.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(movedText));

			var stale = Behavior(input, Live(2, new[] { Actor(10, x: 6, hp: 90), Actor(20) }),
				out _, out _, out var staleOrders);
			var pristine = new List<MiniYamlNode> { stale.SerializePrivateState() }.WriteToString();
			Assert.Throws<InvalidOperationException>(() => stale.RestorePrivateState(
				MiniYaml.FromString(movedText).Single()));
			Assert.That(staleOrders.Issued, Is.Empty);
			Assert.That(new List<MiniYamlNode> { stale.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(pristine));

			var reselectedLive = Live(3, new[] { Actor(10, dead: true), Actor(20) });
			live.Snapshot = reselectedLive;
			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(20));
			var reselectedText = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var reselected = Behavior(input, reselectedLive, out _, out _, out _);
			reselected.RestorePrivateState(MiniYaml.FromString(reselectedText).Single());
			Assert.That(new List<MiniYamlNode> { reselected.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(reselectedText));

			foreach (var actors in new[]
			{
				new[] { Actor(90, defender: false, objective: true) },
				Array.Empty<StealthMassAttackActorSnapshot>()
			})
			{
				var targetlessInput = CreateInput(snapshot);
				var targetless = Behavior(targetlessInput, snapshot, out var targetlessLive, out _, out _);
				targetless.Execute();
				targetlessLive.Snapshot = Live(2, actors);
				targetless.Execute();
				var text = new List<MiniYamlNode> { targetless.SerializePrivateState() }.WriteToString();
				var targetlessRestore = Behavior(targetlessInput, targetlessLive.Snapshot,
					out _, out _, out _);
				targetlessRestore.RestorePrivateState(MiniYaml.FromString(text).Single());
				Assert.That(new List<MiniYamlNode>
				{
					targetlessRestore.SerializePrivateState()
				}.WriteToString(), Is.EqualTo(text));
			}
		}

		[Test]
		public void RestoreThenContinueHandlesExactCompletedAndLostActivityDeterministically()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var input = CreateInput(snapshot);
			var source = Behavior(input, snapshot, out _, out _, out var sourceOrders);
			source.Execute();
			var first = sourceOrders.Issued.Single();
			var original = source.SerializePrivateState();

			var completedSave = MiniYaml.FromString(new List<MiniYamlNode> { original }.WriteToString()).Single();
			completedSave.Value.Nodes.Single(node => node.Key == "HasActivityObservation")
				.Value.Value = "True";
			var lastText = new List<MiniYamlNode>
			{
				completedSave.Value.Nodes.Single(node => node.Key == "LastOrder")
			}.WriteToString().Replace("LastOrder:", "CompletedOrder:");
			completedSave.Value.Nodes.Add(MiniYaml.FromString(lastText).Single());
			var completedLive = Live(1, new[] { Actor(10) }, observeActivity: true, completed: first);
			var completed = Behavior(input, completedLive, out _, out _, out var completedOrders);
			completed.RestorePrivateState(completedSave);
			completed.Execute();
			Assert.That(completedOrders.Issued.Single().AttemptRevision, Is.EqualTo(1));

			var lost = Behavior(input, snapshot, out _, out _, out var lostOrders);
			lost.RestorePrivateState(original);
			lost.Execute();
			Assert.That(lostOrders.Issued.Single().AttemptRevision, Is.EqualTo(1));
		}

		[TestCase("read")]
		[TestCase("threat")]
		[TestCase("order")]
		public void ReentrantExecuteCannotCommitOrExternallyDuplicate(string callback)
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var behavior = Behavior(CreateInput(snapshot), snapshot,
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
			var snapshot = Live(1, new[] { Actor(10) });
			var input = CreateInput(snapshot);
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
		}

		[Test]
		public void PersistenceRoundTripsAndRejectsMissionEvidenceFactsTokenAndDecisionTampering()
		{
			var snapshot = Live(1, new[] { Actor(10, detector: true), Actor(20) });
			var input = CreateInput(snapshot);
			var source = Behavior(input, snapshot, out _, out _, out _);
			source.Execute();
			var text = new List<MiniYamlNode> { source.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input, snapshot, out _, out _, out var orders);
			restored.RestorePrivateState(MiniYaml.FromString(text).Single());
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(text));
			Assert.That(orders.Issued, Is.Empty);
			foreach (var invalid in new[]
			{
				text.Replace("Owner: MassAttack", "Owner: Kite"),
				text.Replace("StrategicCell: 4,4", "StrategicCell: 5,4"),
				text.Replace("Reason: NoSafePlan", "Reason: NoLiveMembers"),
				text.Replace("PlannedReveal: True", "PlannedReveal: False"),
				text.Replace("Detector: True", "Detector: False"),
				text.Replace("SelectedTargetThreat: 1", "SelectedTargetThreat: 2"),
				text.Replace("ActivityRevision: 0", "ActivityRevision: 9"),
				text.Replace("LastEvaluationTick: 1", "LastEvaluationTick: 0"),
				text.Replace("Phase: Advance", "Phase: Attack"),
				text.Replace("Disposition: Retain", "Disposition: RecalculateFlee"),
				text.Replace("TargetCell: 8,0", "TargetCell: 7,0")
			})
				Assert.Throws<InvalidOperationException>(() => restored.RestorePrivateState(
					MiniYaml.FromString(invalid).Single()));
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(text));
			var future = Behavior(input, Live(2, new[] { Actor(10, detector: true), Actor(20) }),
				out _, out _, out _);
			Assert.Throws<InvalidOperationException>(() => future.RestorePrivateState(
				MiniYaml.FromString(text).Single()));
		}

		[Test]
		public void StaleOwnershipPassiveObservationsAndRevisionExhaustionAreBounded()
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var input = CreateInput(snapshot);
			var guard = new OwnershipProbe { Active = false };
			var behavior = Behavior(input, snapshot, out var live, out var threats, out var orders, guard);
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(live.Reads, Is.Zero);
			Assert.That(threats.Facts, Is.Empty);
			Assert.That(orders.Issued, Is.Empty);

			var owner = input.Controller.Owner;
			var epoch = input.Controller.Epoch;
			input.Controller.Observe(new StealthLifecycleObservationFrame(9, new[]
			{
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Timer),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.WorldEvent, 10)
			}));
			Assert.That(input.Controller.Owner, Is.EqualTo(owner));
			Assert.That(input.Controller.Epoch, Is.EqualTo(epoch));

			guard.Active = true;
			SetRevision(behavior, long.MaxValue);
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(live.Reads, Is.Zero);
		}

		[TestCase("read")]
		[TestCase("threat")]
		[TestCase("order")]
		public void OwnershipLossInsideCallbackRollsBackWithoutFurtherAuthority(string callback)
		{
			var snapshot = Live(1, new[] { Actor(10) });
			var input = CreateInput(snapshot);
			var guard = new OwnershipProbe();
			var behavior = Behavior(input, snapshot, out var live, out var threats, out var orders, guard);
			Action lose = () => guard.Active = false;
			if (callback == "read") live.OnRead = lose;
			else if (callback == "threat") threats.OnCalculate = lose;
			else orders.OnIssue = lose;
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "LastObservedTick").Value.Value, Is.EqualTo("-1"));
			Assert.That(orders.Issued, Has.Count.LessThanOrEqualTo(1));
		}

		[Test]
		public void OwnerHasNoCacheClaimOrExclusiveDependency()
		{
			var fields = typeof(StealthMassAttackBehavior).GetFields(
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			Assert.That(fields.Any(field => field.FieldType.Name.IndexOf(
				"Cache", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
			Assert.That(fields.Any(field => field.Name.IndexOf("claim", StringComparison.OrdinalIgnoreCase) >= 0 ||
				field.Name.IndexOf("exclusive", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
		}
	}
}
