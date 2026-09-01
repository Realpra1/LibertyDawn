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
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class StealthRecalculateFleeBehaviorTest
	{
		sealed class Input
		{
			public StealthLifecycleController Controller;
			public StealthRecalculateFleeHandoff Handoff;
		}

		sealed class LiveProbe : IStealthRecalculateFleeLiveWorld
		{
			public StealthRecalculateFleeLiveSnapshot Snapshot;
			public Action OnRead { get; set; }
			public int Reads;
			public StealthRecalculateFleeLiveSnapshot Read(StealthApproachMission mission)
			{
				Reads++;
				OnRead?.Invoke();
				return Snapshot;
			}
		}

		sealed class ThreatProbe : IStealthRecalculateFleeThreatAdapter
		{
			public StealthTargetThreatScore Entry = new StealthTargetThreatScore(4, 2);
			public readonly Dictionary<CPos, StealthTargetThreatScore> Dangers =
				new Dictionary<CPos, StealthTargetThreatScore>();
			public readonly List<StealthRecalculateFleeEntryThreatFacts> EntryFacts =
				new List<StealthRecalculateFleeEntryThreatFacts>();
			public readonly List<StealthRecalculateFleeThreatFacts> RouteFacts =
				new List<StealthRecalculateFleeThreatFacts>();
			public Func<StealthRecalculateFleeThreatFacts, StealthTargetThreatScore> EvaluateRoute =
				facts => new StealthTargetThreatScore(1, 1);
			public Action OnEntry { get; set; }
			public Action OnRoute { get; set; }
			public StealthTargetThreatScore CalculateEntryCrossover(
				StealthRecalculateFleeEntryThreatFacts facts)
			{
				EntryFacts.Add(facts);
				OnEntry?.Invoke();
				return Entry;
			}

			public StealthTargetThreatScore CalculateRouteDanger(
				StealthRecalculateFleeThreatFacts facts)
			{
				RouteFacts.Add(facts);
				OnRoute?.Invoke();
				return Dangers.TryGetValue(facts.CandidateCell, out var score) ? score :
					EvaluateRoute(facts);
			}
		}

		sealed class CacheProbe : IStealthRecalculateFleeStrategicCache
		{
			public int Reads;
			public Action OnRead { get; set; }
			public StealthRecalculateFleeStrategicCacheSnapshot ReadLongRoute(
				StealthApproachMission mission, CPos liveDestination)
			{
				Reads++;
				OnRead?.Invoke();
				return new StealthRecalculateFleeStrategicCacheSnapshot(7,
					new[] { liveDestination });
			}
		}

		sealed class OrderProbe : IStealthRecalculateFleeOrders
		{
			public readonly List<StealthRecalculateFleeOrderToken> Calls =
				new List<StealthRecalculateFleeOrderToken>();
			public readonly List<StealthRecalculateFleeOrderToken> Issued =
				new List<StealthRecalculateFleeOrderToken>();
			readonly HashSet<StealthRecalculateFleeOrderToken> accepted =
				new HashSet<StealthRecalculateFleeOrderToken>();
			public Action OnIssue;
			public bool MutationSucceeded;
			public void IssueMove(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, CPos destinationCell,
				StealthRecalculateFleeOrderToken token)
			{
				Calls.Add(token);
				if (actorIds is IList<uint> list)
				{
					try { list[0] = 999; MutationSucceeded = true; }
					catch (NotSupportedException) { }
				}

				if (accepted.Add(token))
				{
					Issued.Add(token);
					OnIssue?.Invoke();
				}
			}
		}

		static T CreateInternal<T>(params object[] arguments)
		{
			return (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance | BindingFlags.NonPublic,
				null, arguments, null);
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Makefile")))
				directory = directory.Parent;
			return directory?.FullName ?? throw new InvalidOperationException(
				"Could not locate repository root.");
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

		static StealthKiteFallbackEvidence KiteEvidence(IEnumerable<uint> members,
			double crossover, StealthKiteFallbackReason reason = StealthKiteFallbackReason.NoSafePlan)
		{
			var memberIds = members.ToArray();
			var facts = reason == StealthKiteFallbackReason.NoSafePlan ?
				new StealthKiteFallbackFacts(10, new CPos(8, 0), memberIds,
					new uint[] { 10, 20 }, true) : null;
			return CreateInternal<StealthKiteFallbackEvidence>(reason, "entry",
				new uint[] { 10, 20 }, facts,
				reason == StealthKiteFallbackReason.NoSafePlan ?
					(StealthTargetThreatScore?)new StealthTargetThreatScore(4, crossover) : null);
		}

		static Input CreateInput(double crossover = 2, IEnumerable<uint> members = null)
		{
			var memberIds = (members ?? new uint[] { 1, 2 }).OrderBy(id => id).ToArray();
			var mission = Mission();
			var controller = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.Kite, new OwnershipEpoch(7), -1));
			var evidence = KiteEvidence(memberIds, crossover);
			var result = CreateInternal<StealthKiteResult>(controller.CurrentHandoff, mission,
				StealthKiteDisposition.RecalculateFlee, StealthKitePhase.Position,
				(uint?)10, (CPos?)new CPos(8, 0), (CPos?)null, (CPos?)null,
				memberIds, new uint[] { 10, 20 }, Array.Empty<uint>(),
				(StealthKiteSafetyResult?)null, evidence);
			Assert.That(controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.RecalculateFleeEntry, Is.Not.Null);
			return new Input { Controller = controller, Handoff = transition.RecalculateFleeEntry };
		}

		static StealthRecalculateFleeMemberSnapshot Member(uint id, int x = 0,
			int hp = 100, bool dead = false, int range = 4)
		{
			return new StealthRecalculateFleeMemberSnapshot(id, new CPos(x, 0), range,
				hp, 100, true, dead);
		}

		static StealthRecalculateFleeEnemySnapshot Enemy(uint id, string type,
			int x, int hp = 100, int range = 4, bool detector = false,
			bool targetable = true)
		{
			return new StealthRecalculateFleeEnemySnapshot(id, type, new CPos(x, 0),
				hp, 100, range, detector, true, true, false, targetable);
		}

		static StealthRecalculateFleeLiveSnapshot Live(int tick,
			IEnumerable<StealthRecalculateFleeMemberSnapshot> members = null,
			IEnumerable<StealthRecalculateFleeEnemySnapshot> enemies = null,
			IEnumerable<StealthRecalculateFleeCandidateSnapshot> candidates = null,
			bool cloaked = true,
			bool observe = false, long activityRevision = 0,
			StealthRecalculateFleeOrderToken active = null,
			StealthRecalculateFleeOrderToken completed = null)
		{
			return new StealthRecalculateFleeLiveSnapshot(tick,
				members ?? new[] { Member(1), Member(2) },
				enemies ?? new[] { Enemy(10, "guard", 8), Enemy(20, "harv", 10) },
				candidates ?? new[]
				{
					new StealthRecalculateFleeCandidateSnapshot(new CPos(-2, 0), true),
					new StealthRecalculateFleeCandidateSnapshot(new CPos(0, 2), true)
				}, cloaked, "entry", observe, activityRevision, active, completed);
		}

		static StealthRecalculateFleeBehavior Behavior(Input input,
			StealthRecalculateFleeLiveSnapshot snapshot, out LiveProbe live,
			out ThreatProbe threats, out CacheProbe cache, out OrderProbe orders)
		{
			live = new LiveProbe { Snapshot = snapshot };
			threats = new ThreatProbe();
			cache = new CacheProbe();
			orders = new OrderProbe();
			return new StealthRecalculateFleeBehavior(input.Handoff, input.Controller,
				live, threats, cache, orders);
		}

		[Test]
		public void ChoosesLeastStandardDangerDeterministicallyAcrossAllLiveEnemyTypes()
		{
			var input = CreateInput();
			var enemies = new[]
			{
				Enemy(10, "guard", 8), Enemy(20, "harv", 10), Enemy(30, "wall", 7),
				Enemy(40, "obelisk", 12, detector: false), Enemy(50, "detector", 9, detector: true)
			};
			var cells = new[]
			{
				new StealthRecalculateFleeCandidateSnapshot(new CPos(2, 2), true),
				new StealthRecalculateFleeCandidateSnapshot(new CPos(-2, 0), true),
				new StealthRecalculateFleeCandidateSnapshot(new CPos(0, -2), true),
				new StealthRecalculateFleeCandidateSnapshot(new CPos(9, 9), false)
			};
			var behavior = Behavior(input, Live(1), out var live, out var threats,
				out var cache, out var orders);
			behavior.Execute();
			live.Snapshot = Live(2, enemies: enemies, candidates: cells);
			threats.Dangers[new CPos(2, 2)] = new StealthTargetThreatScore(3, 1);
			threats.Dangers[new CPos(-2, 0)] = new StealthTargetThreatScore(1, 1);
			threats.Dangers[new CPos(0, -2)] = new StealthTargetThreatScore(1, 1);
			var result = behavior.Execute();

			Assert.That(result.SelectedDestinationCell, Is.EqualTo(new CPos(0, -2)));
			Assert.That(threats.RouteFacts, Has.Count.EqualTo(5));
			Assert.That(threats.RouteFacts.Skip(2).All(facts => facts.Enemies.Select(enemy => enemy.ActorType)
				.SequenceEqual(new[] { "guard", "harv", "wall", "obelisk", "detector" })), Is.True);
			Assert.That(threats.RouteFacts.All(facts => !facts.PlannedAttack && !facts.PlannedDecloak), Is.True);
			Assert.That(cache.Reads, Is.Zero);
			Assert.That(orders.Issued, Has.Count.EqualTo(2));
			Assert.That(orders.MutationSucceeded, Is.False);
		}

		[Test]
		public void LongRouteCacheIsPassiveAndCannotChangeTheLiveWinner()
		{
			var input = CreateInput();
			var cells = new[]
			{
				new StealthRecalculateFleeCandidateSnapshot(new CPos(-5, 0), true, true),
				new StealthRecalculateFleeCandidateSnapshot(new CPos(1, 0), true)
			};
			var behavior = Behavior(input, Live(1, candidates: cells), out _, out var threats,
				out var cache, out var orders);
			threats.Dangers[new CPos(-5, 0)] = new StealthTargetThreatScore(0, 0);
			threats.Dangers[new CPos(1, 0)] = new StealthTargetThreatScore(9, 9);
			var result = behavior.Execute();
			Assert.That(result.SelectedDestinationCell, Is.EqualTo(new CPos(-5, 0)));
			Assert.That(result.LongRouteCacheRevision, Is.EqualTo(7));
			Assert.That(cache.Reads, Is.EqualTo(1));
			Assert.That(orders.Issued.Single().DestinationCell, Is.EqualTo(new CPos(-5, 0)));
			behavior.Execute();
			Assert.That(cache.Reads, Is.EqualTo(1));
		}

		[Test]
		public void CloakedRouteUsesCandidateSpecificDetectorCoverage()
		{
			var input = CreateInput();
			var enemies = new[] { Enemy(10, "guard", 8), Enemy(20, "obelisk", 8) };
			var safe = new StealthRecalculateFleeCandidateSnapshot(new CPos(6, 0), true,
				hasDetectorCoverage: false);
			var detected = new StealthRecalculateFleeCandidateSnapshot(new CPos(10, 0), true,
				hasDetectorCoverage: true);
			var behavior = Behavior(input, Live(1, enemies: enemies, candidates: new[] { safe, detected }),
				out _, out var threats, out var cache, out var orders);
			threats.EvaluateRoute = facts => facts.HasDetectorCoverage ?
				new StealthTargetThreatScore(5, 2) : new StealthTargetThreatScore(0, 0);

			var result = behavior.Execute();

			Assert.That(result.SelectedDestinationCell, Is.EqualTo(safe.Cell));
			Assert.That(threats.RouteFacts.Select(facts => facts.HasDetectorCoverage),
				Is.EqualTo(new[] { false, true }));
			Assert.That(threats.RouteFacts.All(facts => facts.FormationCloaked &&
				!facts.PlannedDecloak && !facts.PlannedAttack &&
				!facts.PlannedCurrentRangeEngagement), Is.True);
			Assert.That(cache.Reads, Is.Zero);
			Assert.That(orders.Issued.Single().DestinationCell, Is.EqualTo(safe.Cell));
		}

		[Test]
		public void CoveredCurrentRangeGuardAndObeliskUseCanonicalStandardThreatWiring()
		{
			var input = CreateInput();
			var enemies = new[]
			{
				Enemy(10, "guard", 8, range: 4), Enemy(20, "obelisk", 8, range: 4)
			};
			var safe = new StealthRecalculateFleeCandidateSnapshot(new CPos(0, 0), true,
				hasDetectorCoverage: false);
			var covered = new StealthRecalculateFleeCandidateSnapshot(new CPos(8, 0), true,
				hasDetectorCoverage: true);
			var behavior = Behavior(input, Live(1, enemies: enemies,
				candidates: new[] { safe, covered }), out _, out var threats, out _, out _);
			threats.EvaluateRoute = facts =>
			{
				var covering = facts.Enemies.Count(enemy =>
					(!facts.FormationCloaked || facts.HasDetectorCoverage) &&
					DistanceSquared(enemy.CurrentCell, facts.CandidateCell) <=
						(long)enemy.CurrentWeaponRangeCells * enemy.CurrentWeaponRangeCells);
				return new StealthTargetThreatScore(covering, covering);
			};

			var result = behavior.Execute();

			Assert.That(result.SelectedDestinationCell, Is.EqualTo(safe.Cell));
			Assert.That(result.RouteEvaluations.Single(route => route.Candidate.Cell == safe.Cell)
				.StandardDanger.ThreatRating, Is.Zero);
			Assert.That(result.RouteEvaluations.Single(route => route.Candidate.Cell == covered.Cell)
				.StandardDanger.ThreatRating, Is.EqualTo(2));
			Assert.That(threats.RouteFacts.Single(facts => facts.CandidateCell == covered.Cell)
				.Enemies.Select(enemy => enemy.ActorType), Is.EqualTo(new[] { "guard", "obelisk" }));

			var path = Path.Combine(RepositoryRoot(), "OpenRA.Mods.Common", "Traits", "BotModules",
				"BotModuleLogic", "StealthLifecycle", "GeneralizedCombatRecalculateFleeThreatAdapter.cs");
			var adapter = File.ReadAllText(path);
			Assert.That(adapter, Does.Contain("return StandardRouteScore(friendly, covering);"));
			var routeScore = adapter.Substring(adapter.IndexOf(
				"StealthTargetThreatScore StandardRouteScore", StringComparison.Ordinal));
			routeScore = routeScore.Substring(0, routeScore.IndexOf(
				"static double SumFinite", StringComparison.Ordinal));
			Assert.That(routeScore, Does.Contain("var cumulativeThreat = SumFinite("));
			Assert.That(routeScore, Does.Contain(
				"calculator.CalculateLive(attacker, defender, plannedTargetTypesOverride, true)"));
			Assert.That(routeScore, Does.Not.Contain(".Max()"));
			Assert.That(adapter, Does.Contain("!double.IsFinite(contribution)"));
			Assert.That(adapter, Does.Contain("contribution > double.MaxValue - sum"));
			Assert.That(adapter, Does.Not.Contain(
				"StandardScore(friendly, covering, default(BitSet<TargetableType>), false)"));
		}

		[Test]
		public void CumulativeCoveredThreatsOutweighOneLargerThreatAndTieIsStable()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out var threats, out _, out _);
			behavior.Execute();
			var left = new CPos(0, 0);
			var right = new CPos(10, 0);
			var enemies = new[]
			{
				Enemy(10, "guard", 0, range: 2), Enemy(20, "obelisk", 0, range: 2),
				Enemy(30, "turret", 10, range: 2)
			};
			var candidates = new[]
			{
				new StealthRecalculateFleeCandidateSnapshot(left, true,
					hasDetectorCoverage: true),
				new StealthRecalculateFleeCandidateSnapshot(right, true,
					hasDetectorCoverage: true)
			};
			var weights = new Dictionary<string, double>
			{
				{ "guard", 3 }, { "obelisk", 3 }, { "turret", 5 }
			};
			threats.EvaluateRoute = facts => CumulativeScore(facts, weights);
			live.Snapshot = Live(2, enemies: enemies, candidates: candidates);
			var cumulative = behavior.Execute();
			Assert.That(cumulative.RouteEvaluations.Single(route => route.Candidate.Cell == left)
				.StandardDanger.ThreatRating, Is.EqualTo(6));
			Assert.That(cumulative.RouteEvaluations.Single(route => route.Candidate.Cell == right)
				.StandardDanger.ThreatRating, Is.EqualTo(5));
			Assert.That(cumulative.SelectedDestinationCell, Is.EqualTo(right));

			weights["obelisk"] = 2;
			var tie = behavior.Execute();
			Assert.That(tie.RouteEvaluations.All(route =>
				route.StandardDanger.ThreatRating == 5), Is.True);
			Assert.That(tie.SelectedDestinationCell, Is.EqualTo(left));
		}

		[Test]
		public void LiveEnemyAddRemoveFlipsCumulativeRouteTransactionallyAndPersistsExactly()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out var threats,
				out var cache, out var orders);
			behavior.Execute();
			var left = new CPos(0, 0);
			var right = new CPos(10, 0);
			var guard = Enemy(10, "guard", 0, range: 2);
			var obelisk = Enemy(20, "obelisk", 0, range: 2);
			var turret = Enemy(30, "turret", 10, range: 2);
			var candidates = new[]
			{
				new StealthRecalculateFleeCandidateSnapshot(left, true,
					hasDetectorCoverage: true),
				new StealthRecalculateFleeCandidateSnapshot(right, true,
					hasDetectorCoverage: true)
			};
			var weights = new Dictionary<string, double>
			{
				{ "guard", 3 }, { "obelisk", 3 }, { "turret", 5 }
			};
			threats.EvaluateRoute = facts => CumulativeScore(facts, weights);
			live.Snapshot = Live(2, enemies: new[] { guard, turret }, candidates: candidates);
			Assert.That(behavior.Execute().SelectedDestinationCell, Is.EqualTo(left));

			live.Snapshot = Live(3, enemies: new[] { guard, obelisk, turret }, candidates: candidates);
			orders.OnIssue = () => throw new InvalidOperationException("issued then throw");
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			var replacement = orders.Issued.Last();
			Assert.That(replacement.DestinationCell, Is.EqualTo(right));
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "Destination").Value.Value, Is.EqualTo(FieldSaver.FormatValue(left)));
			orders.OnIssue = null;
			var added = behavior.Execute();
			Assert.That(added.SelectedDestinationCell, Is.EqualTo(right));
			Assert.That(orders.Calls.Last(), Is.EqualTo(replacement));

			var saved = behavior.SerializePrivateState();
			var restored = Behavior(input, live.Snapshot, out _, out var restoredThreats,
				out _, out _);
			restoredThreats.EvaluateRoute = facts => CumulativeScore(facts, weights);
			restored.RestorePrivateState(saved);
			var text = new List<MiniYamlNode> { saved }.WriteToString();
			var tampered = text.Replace("Threat: 6", "Threat: 5");
			var invalid = Behavior(input, live.Snapshot, out _, out var invalidThreats,
				out _, out _);
			invalidThreats.EvaluateRoute = facts => CumulativeScore(facts, weights);
			Assert.Throws<InvalidOperationException>(() => invalid.RestorePrivateState(
				MiniYaml.FromString(tampered).Single()));

			live.Snapshot = Live(4, enemies: new[] { guard, turret }, candidates: candidates);
			Assert.That(behavior.Execute().SelectedDestinationCell, Is.EqualTo(left));
			Assert.That(cache.Reads, Is.Zero);
		}

		[Test]
		public void DetectorCoverageMovementReplacesRouteTransactionallyWithOneToken()
		{
			var input = CreateInput();
			var enemies = new[] { Enemy(10, "guard", 8), Enemy(20, "obelisk", 8) };
			var left = new CPos(6, 0);
			var right = new CPos(10, 0);
			var behavior = Behavior(input, Live(1, enemies: enemies, candidates: new[]
			{
				new StealthRecalculateFleeCandidateSnapshot(left, true,
					hasDetectorCoverage: false),
				new StealthRecalculateFleeCandidateSnapshot(right, true,
					hasDetectorCoverage: true)
			}), out var live, out var threats, out var cache, out var orders);
			threats.EvaluateRoute = facts => facts.HasDetectorCoverage ?
				new StealthTargetThreatScore(5, 2) : new StealthTargetThreatScore(0, 0);
			Assert.That(behavior.Execute().SelectedDestinationCell, Is.EqualTo(left));
			var firstToken = orders.Issued.Single();

			live.Snapshot = Live(2, enemies: enemies, candidates: new[]
			{
				new StealthRecalculateFleeCandidateSnapshot(left, true,
					hasDetectorCoverage: true),
				new StealthRecalculateFleeCandidateSnapshot(right, true,
					hasDetectorCoverage: false)
			});
			orders.OnIssue = () => throw new InvalidOperationException("issued then throw");
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			var replacementToken = orders.Issued.Last();
			Assert.That(replacementToken, Is.Not.EqualTo(firstToken));
			Assert.That(replacementToken.DestinationCell, Is.EqualTo(right));
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "Destination").Value.Value, Is.EqualTo(FieldSaver.FormatValue(left)));

			orders.OnIssue = null;
			var replaced = behavior.Execute();
			Assert.That(replaced.SelectedDestinationCell, Is.EqualTo(right));
			Assert.That(orders.Calls.Last(), Is.EqualTo(replacementToken));
			Assert.That(orders.Issued, Has.Count.EqualTo(2));
			Assert.That(cache.Reads, Is.Zero);
		}

		[Test]
		public void CallbackThrowActivityLossAndMembershipChangeRemainTokenIdempotent()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out _, out _, out var orders);
			orders.OnIssue = () => throw new InvalidOperationException("issued then throw");
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			orders.OnIssue = null;
			behavior.Execute();
			Assert.That(orders.Calls[1], Is.EqualTo(orders.Calls[0]));
			Assert.That(orders.Issued, Has.Count.EqualTo(1));

			live.Snapshot = Live(2, observe: true, activityRevision: 1);
			behavior.Execute();
			Assert.That(orders.Calls.Last(), Is.EqualTo(orders.Calls[0]));
			Assert.That(orders.Issued, Has.Count.EqualTo(1));

			live.Snapshot = Live(3, new[] { Member(1), Member(2, hp: 0) }, observe: true,
				activityRevision: 2);
			var changed = behavior.Execute();
			Assert.That(changed.LiveCause, Is.EqualTo(StealthRecalculateFleeLiveCause.MemberLoss));
			Assert.That(orders.Issued, Has.Count.EqualTo(2));
			Assert.That(orders.Issued[1].ActorIds, Is.EqualTo(new uint[] { 1 }));
		}

		[TestCase("member-position")]
		[TestCase("member-hp")]
		[TestCase("member-range")]
		[TestCase("enemy-position")]
		[TestCase("enemy-hp")]
		[TestCase("enemy-range")]
		[TestCase("enemy-validity")]
		[TestCase("cloak")]
		[TestCase("detection")]
		[TestCase("candidate")]
		[TestCase("score")]
		public void EverySafetyAffectingLiveChangeReevaluatesWithoutOrderChurn(string change)
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out var threats,
				out var cache, out var orders);
			behavior.Execute();
			var priorThreatCalls = threats.RouteFacts.Count;
			var members = new[] { Member(1), Member(2) };
			var enemies = new[] { Enemy(10, "guard", 8), Enemy(20, "harv", 10) };
			var candidates = new[]
			{
				new StealthRecalculateFleeCandidateSnapshot(new CPos(-2, 0), true),
				new StealthRecalculateFleeCandidateSnapshot(new CPos(0, 2), true)
			};
			var cloaked = true;
			if (change == "member-position") members[0] = Member(1, 1);
			else if (change == "member-hp") members[0] = Member(1, hp: 99);
			else if (change == "member-range") members[0] = Member(1, range: 5);
			else if (change == "enemy-position") enemies[1] = Enemy(20, "harv", 11);
			else if (change == "enemy-hp") enemies[1] = Enemy(20, "harv", 10, hp: 99);
			else if (change == "enemy-range") enemies[1] = Enemy(20, "harv", 10, range: 5);
			else if (change == "enemy-validity") enemies[1] = Enemy(20, "harv", 10, targetable: false);
			else if (change == "cloak") cloaked = false;
			else if (change == "detection")
			{
				enemies[1] = Enemy(20, "harv", 10, detector: true);
				candidates[1] = new StealthRecalculateFleeCandidateSnapshot(
					candidates[1].Cell, true, hasDetectorCoverage: true);
			}
			else if (change == "candidate") candidates = candidates.Concat(new[]
			{
				new StealthRecalculateFleeCandidateSnapshot(new CPos(5, 5), true)
			}).ToArray();
			else threats.Dangers[new CPos(0, 2)] = new StealthTargetThreatScore(2, 2);
			live.Snapshot = Live(2, members, enemies, candidates, cloaked);

			behavior.Execute();
			Assert.That(threats.RouteFacts.Count, Is.GreaterThan(priorThreatCalls));
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
			Assert.That(cache.Reads, Is.Zero);
		}

		[Test]
		public void NoTargetNoRouteAndTotalMemberLossAreCanonicalRetainedOutcomes()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out var threats,
				out var cache, out var orders);
			behavior.Execute();
			var priorThreats = threats.RouteFacts.Count;
			live.Snapshot = Live(2, enemies: Array.Empty<StealthRecalculateFleeEnemySnapshot>());
			var noTarget = behavior.Execute();
			Assert.That(noTarget.LiveCause, Is.EqualTo(StealthRecalculateFleeLiveCause.NoTarget));
			Assert.That(input.Controller.TryAccept(noTarget, out var retained), Is.True);
			Assert.That(retained.Retained, Is.Not.Null);

			live.Snapshot = Live(3, candidates: new[]
			{
				new StealthRecalculateFleeCandidateSnapshot(new CPos(2, 2), false)
			});
			var noRoute = behavior.Execute();
			Assert.That(noRoute.LiveCause, Is.EqualTo(StealthRecalculateFleeLiveCause.NoRoute));
			live.Snapshot = Live(4, new[] { Member(1, hp: 0), Member(2, hp: 0) });
			var noMembers = behavior.Execute();
			Assert.That(noMembers.LiveCause, Is.EqualTo(StealthRecalculateFleeLiveCause.MemberLoss));
			Assert.That(threats.RouteFacts, Has.Count.EqualTo(priorThreats));
			Assert.That(cache.Reads, Is.Zero);
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
		}

		[TestCase("read")]
		[TestCase("entry")]
		[TestCase("route")]
		[TestCase("cache")]
		[TestCase("order")]
		public void ReentrantCallbacksCannotCommitOrDuplicateExternally(string callback)
		{
			var input = CreateInput();
			StealthRecalculateFleeCandidateSnapshot[] candidates = null;
			if (callback == "cache")
				candidates = new[]
				{
					new StealthRecalculateFleeCandidateSnapshot(new CPos(-2, 0), true, true)
				};
			var behavior = Behavior(input, Live(1, candidates: candidates), out var live,
				out var threats, out var cache, out var orders);
			Action recurse = () => behavior.Execute();
			if (callback == "read") live.OnRead = recurse;
			else if (callback == "entry") threats.OnEntry = recurse;
			else if (callback == "route") threats.OnRoute = recurse;
			else if (callback == "cache") cache.OnRead = recurse;
			else orders.OnIssue = recurse;
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(orders.Issued, Has.Count.LessThanOrEqualTo(1));
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "LastObservedTick").Value.Value, Is.EqualTo("-1"));
		}

		[Test]
		public void ForgedLiveEntryCauseFailsBeforeRouteCacheOrOrders()
		{
			var input = CreateInput();
			var forged = new StealthRecalculateFleeLiveSnapshot(1,
				new[] { Member(1), Member(2) },
				new[] { Enemy(10, "guard", 8), Enemy(20, "harv", 10) },
				new[] { new StealthRecalculateFleeCandidateSnapshot(new CPos(-4, 0), true, true) },
				true, "forged");
			var behavior = Behavior(input, forged, out _, out var threats,
				out var cache, out var orders);
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(threats.EntryFacts, Is.Empty);
			Assert.That(threats.RouteFacts, Is.Empty);
			Assert.That(cache.Reads, Is.Zero);
			Assert.That(orders.Issued, Is.Empty);
		}

		[Test]
		public void CompletionAloneYieldsOneUnchangedMissionTargetAcquisitionHandoff()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out _, out _, out var orders);
			var first = behavior.Execute();
			Assert.That(input.Controller.TryAccept(first, out var retained), Is.True);
			Assert.That(retained.Retained, Is.Not.Null);
			var token = orders.Issued.Single();
			live.Snapshot = Live(2, new[]
			{
				Member(1, token.DestinationCell.X), Member(2, token.DestinationCell.X)
			}, candidates: new[] { new StealthRecalculateFleeCandidateSnapshot(token.DestinationCell, true) },
				observe: true, activityRevision: 1, completed: token);
			var complete = behavior.Execute();
			Assert.That(complete.Disposition,
				Is.EqualTo(StealthRecalculateFleeDisposition.TargetAcquisition));
			Assert.That(input.Controller.TryAccept(complete, out var transition), Is.True);
			Assert.That(transition.TargetAcquisition.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
			Assert.That(complete.Mission, Is.SameAs(input.Handoff.Mission));
			Assert.That(input.Controller.TryAccept(complete, out _), Is.False);
		}

		[Test]
		public void PrivateStateRoundTripsAndRejectsCauseRouteScoreAndTokenTampering()
		{
			var input = CreateInput();
			var snapshot = Live(1);
			var behavior = Behavior(input, snapshot, out _, out _, out _, out _);
			behavior.Execute();
			var text = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input, snapshot, out _, out _, out _, out _);
			restored.RestorePrivateState(MiniYaml.FromString(text).Single());
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(text));

			foreach (var tampered in new[]
			{
				text.Replace("LiveCause: Traversing", "LiveCause: Completed"),
				text.Replace("DangerThreat: 1", "DangerThreat: 17"),
				text.Replace("Crossover: 1", "Crossover: 19"),
				text.Replace("HP: 100", "HP: 99"),
				text.Replace("Detector: False", "Detector: True"),
				text.Replace("Destination: -2,0", "Destination: 7,7"),
				text.Replace("Owner: RecalculateFlee", "Owner: Kite")
			})
			{
				var invalid = Behavior(input, snapshot, out _, out _, out _, out _);
				Assert.Throws<InvalidOperationException>(() => invalid.RestorePrivateState(
					MiniYaml.FromString(tampered).Single()));
			}
		}

		[TestCase("kite")]
		[TestCase("mass")]
		public void ZeroMemberCombatExitTransfersOnceToSquadConstruction(string source)
		{
			if (source == "kite")
			{
				var mission = Mission();
				var controller = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
					BehaviorId.Kite, new OwnershipEpoch(7), -1));
				var result = CreateInternal<StealthKiteResult>(controller.CurrentHandoff, mission,
					StealthKiteDisposition.RecalculateFlee, StealthKitePhase.Position,
					(uint?)null, (CPos?)null, (CPos?)null, (CPos?)null, Array.Empty<uint>(),
					new uint[] { 10, 20 }, Array.Empty<uint>(), (StealthKiteSafetyResult?)null,
					KiteEvidence(Array.Empty<uint>(), 0, StealthKiteFallbackReason.NoLiveMembers));
				Assert.That(controller.TryAccept(result, out var transition), Is.True);
				Assert.That(transition.SquadConstruction.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
				Assert.That(transition.SquadConstructionEntry.Mission, Is.SameAs(mission));
				Assert.That(transition.RecalculateFlee, Is.Null);
				Assert.That(transition.RecalculateFleeEntry, Is.Null);
				Assert.That(controller.Epoch.Value, Is.EqualTo(8));
				Assert.That(controller.TryAccept(result, out _), Is.False);
				return;
			}

			var massMission = Mission();
			var controllerForMass = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.Kite, new OwnershipEpoch(7), -1));
			var massEvidence = KiteEvidence(new uint[] { 1 }, 3);
			var kiteResult = CreateInternal<StealthKiteResult>(controllerForMass.CurrentHandoff,
				massMission, StealthKiteDisposition.MassAttack, StealthKitePhase.Position,
				(uint?)10, (CPos?)new CPos(8, 0), (CPos?)null, (CPos?)null, new uint[] { 1 },
				new uint[] { 10, 20 }, Array.Empty<uint>(), (StealthKiteSafetyResult?)null, massEvidence);
			Assert.That(controllerForMass.TryAccept(kiteResult, out var massEntry), Is.True);
			var massResult = CreateInternal<StealthMassAttackResult>(massEntry.MassAttackEntry,
				massMission, StealthMassAttackDisposition.RecalculateFlee,
				StealthMassAttackPhase.Advance, (uint?)null, (CPos?)null, Array.Empty<uint>(),
				new uint[] { 10, 20 }, Array.Empty<uint>(), null,
				(StealthMassAttackThreatResult?)null, null);
			Assert.That(controllerForMass.TryAccept(massResult, out var massTransition), Is.True);
			Assert.That(massTransition.SquadConstruction.Owner,
				Is.EqualTo(BehaviorId.SquadConstruction));
			Assert.That(massTransition.SquadConstructionEntry.Mission, Is.SameAs(massMission));
			Assert.That(massTransition.RecalculateFlee, Is.Null);
			Assert.That(massTransition.RecalculateFleeEntry, Is.Null);
			Assert.That(controllerForMass.Epoch.Value, Is.EqualTo(9));
			Assert.That(controllerForMass.TryAccept(massResult, out _), Is.False);
		}

		static long DistanceSquared(CPos left, CPos right)
		{
			var dx = (long)left.X - right.X;
			var dy = (long)left.Y - right.Y;
			return dx * dx + dy * dy;
		}

		static StealthTargetThreatScore CumulativeScore(StealthRecalculateFleeThreatFacts facts,
			IReadOnlyDictionary<string, double> weights)
		{
			var sum = facts.Enemies.Where(enemy =>
				(!facts.FormationCloaked || facts.HasDetectorCoverage) &&
				DistanceSquared(enemy.CurrentCell, facts.CandidateCell) <=
					(long)enemy.CurrentWeaponRangeCells * enemy.CurrentWeaponRangeCells)
				.Sum(enemy => weights[enemy.ActorType]);
			return new StealthTargetThreatScore(sum, 0);
		}
	}
}
