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
	public sealed class StealthRepairBehaviorTest
	{
		sealed class Input
		{
			public StealthLifecycleController Controller;
			public StealthRepairHandoff Handoff;
			public StealthRepairResumeContext Resume;
		}

		sealed class LiveProbe : IStealthRepairLiveWorld
		{
			public StealthRepairLiveSnapshot Snapshot;
			public Action OnRead;
			public int Reads;
			public StealthRepairLiveSnapshot Read(StealthApproachMission mission)
			{
				Reads++;
				OnRead?.Invoke();
				return Snapshot;
			}
		}

		sealed class ThreatProbe : IStealthRepairThreatAdapter
		{
			public readonly List<StealthRepairThreatFacts> Facts = new List<StealthRepairThreatFacts>();
			public Func<StealthRepairThreatFacts, StealthTargetThreatScore> Score = facts =>
				facts.RepairOptionActorId == 100 ? new StealthTargetThreatScore(0, 4) :
				new StealthTargetThreatScore(3, 1);
			public Action OnCalculate;
			public StealthTargetThreatScore CalculateRouteDanger(StealthRepairThreatFacts facts)
			{
				Facts.Add(facts);
				OnCalculate?.Invoke();
				return Score(facts);
			}
		}

		sealed class CacheProbe : IStealthRepairStrategicCache
		{
			public int Reads;
			public Action OnRead;
			public Func<uint, IReadOnlyList<CPos>, StealthRepairStrategicCacheSnapshot> Route =
				(option, live) => new StealthRepairStrategicCacheSnapshot(12, live);
			public StealthRepairStrategicCacheSnapshot ReadLongRoute(StealthApproachMission mission,
				uint repairOptionActorId, IReadOnlyList<CPos> liveRoute)
			{
				Reads++;
				OnRead?.Invoke();
				return Route(repairOptionActorId, liveRoute);
			}
		}

		sealed class OrderProbe : IStealthRepairOrders
		{
			public readonly List<StealthRepairOrderToken> Calls = new List<StealthRepairOrderToken>();
			public readonly List<StealthRepairOrderToken> Issued = new List<StealthRepairOrderToken>();
			public readonly List<(CPos[] Route, int Progress)> Routes =
				new List<(CPos[], int)>();
			readonly HashSet<StealthRepairOrderToken> accepted = new HashSet<StealthRepairOrderToken>();
			public Action OnIssue;
			public bool MutationSucceeded;
			public void IssueRepair(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint repairOptionActorId,
				IReadOnlyList<CPos> orderedRoute, int routeProgress, StealthRepairOrderKind kind,
				StealthRepairOrderToken token)
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
					Routes.Add((orderedRoute.ToArray(), routeProgress));
					OnIssue?.Invoke();
				}
			}
		}

		sealed class GuardProbe : IStealthLifecycleOwnershipGuard
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
			var cell = new CPos(8, 0);
			var target = new StealthStrategicTargetSnapshot(10, cell, 100, 1000, 100, 100);
			var facts = new StealthTargetThreatFacts(cell,
				new[] { new StealthCombatGroupSnapshot("stnk", 2, 900) },
				new[] { new StealthCombatGroupSnapshot("mtnk", 1, 800) }, true, true, true);
			var option = CreateInternal<StealthTargetOption>(cell, (int?)100, false,
				new[] { target }, facts);
			var value = CreateInternal<StealthTargetValueOption>(option, 1000L);
			var threat = CreateInternal<StealthTargetThreatOption>(value,
				new StealthTargetThreatScore(5, 3));
			return CreateInternal<StealthApproachMission>(threat, 100L, 20, 80L);
		}

		static Input CreateInput(IEnumerable<StealthRepairDamagedMember> damaged = null)
		{
			var mission = Mission();
			var resume = CreateInternal<StealthRepairResumeContext>(BehaviorId.Kite,
				new OwnershipEpoch(7), mission, new uint[] { 1, 2 }, new uint[] { 10, 20, 30, 40 },
				(uint?)10, (CPos?)new CPos(8, 0), "fight-context");
			var controller = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.Damage, new OwnershipEpoch(8), -1));
			var request = CreateInternal<StealthDamageRepairRequest>(controller.CurrentHandoff,
				77L, 5, 900u, 25, damaged ?? new[]
				{
					new StealthRepairDamagedMember(1, 40, 100),
					new StealthRepairDamagedMember(2, 60, 100)
				}, resume);
			Assert.That(controller.TryAccept(request, out var handoff), Is.True);
			return new Input { Controller = controller, Handoff = handoff, Resume = resume };
		}

		static StealthRepairMemberSnapshot Member(uint id, int x = 0, int hp = 40,
			bool inWorld = true, bool dead = false, int range = 4)
		{
			return new StealthRepairMemberSnapshot(id, new CPos(x, 0), range, hp, 100,
				inWorld, dead);
		}

		static StealthRepairEnemySnapshot Enemy(uint id = 10, string type = "guard",
			int x = 8, int range = 4, bool detector = false, bool targetable = true)
		{
			return new StealthRepairEnemySnapshot(id, type, new CPos(x, 0), 100, 100,
				range, detector, true, true, false, targetable);
		}

		static StealthRepairRouteSnapshot Route(uint id, uint option, int destination,
			bool passable = true, bool strategic = false, bool detector = false)
		{
			return new StealthRepairRouteSnapshot(id, option,
				Enumerable.Range(1, destination).Select(x => new CPos(x, 0)),
				passable, strategic, detector);
		}

		static StealthRepairLiveSnapshot Live(int tick,
			IEnumerable<StealthRepairMemberSnapshot> members = null,
			IEnumerable<StealthRepairOptionSnapshot> options = null,
			IEnumerable<StealthRepairEnemySnapshot> enemies = null,
			IEnumerable<StealthRepairStaticActorSnapshot> staticActors = null,
			IEnumerable<StealthRepairRouteSnapshot> routes = null, bool cloaked = true,
			bool observe = false, long activityRevision = 0, int routeProgress = 0,
			StealthRepairOrderToken active = null, StealthRepairOrderToken completed = null,
			string resumeFingerprint = "fight-context", int damageAmount = 25)
		{
			return new StealthRepairLiveSnapshot(tick, 77, 5, 900, damageAmount,
				resumeFingerprint, members ?? new[] { Member(1), Member(2, hp: 60) },
				options ?? new[]
				{
					new StealthRepairOptionSnapshot(100, new CPos(5, 0)),
					new StealthRepairOptionSnapshot(200, new CPos(6, 0))
				}, enemies ?? new[]
				{
					Enemy(), Enemy(20, "detector", 9, detector: true),
					Enemy(30, "wall", 3, range: 0), Enemy(40, "obelisk", 8, range: 6)
				}, staticActors ?? new[]
				{
					new StealthRepairStaticActorSnapshot(300, "wall", new CPos(3, 1), false),
					new StealthRepairStaticActorSnapshot(400, "obelisk", new CPos(8, 0), true)
				}, routes ?? new[] { Route(1000, 100, 5), Route(2000, 200, 6) }, cloaked,
				observe, activityRevision, routeProgress, active, completed);
		}

		static StealthRepairBehavior Behavior(Input input, StealthRepairLiveSnapshot snapshot,
			out LiveProbe live, out ThreatProbe threats, out CacheProbe cache, out OrderProbe orders,
			IStealthLifecycleOwnershipGuard guard = null)
		{
			live = new LiveProbe { Snapshot = snapshot };
			threats = new ThreatProbe();
			cache = new CacheProbe();
			orders = new OrderProbe();
			return new StealthRepairBehavior(input.Handoff, guard ?? input.Controller,
				live, threats, cache, orders);
		}

		static string ForgeLastOrderSubsetWithMatchingFingerprint(string text)
		{
			var lines = text.Split('\n').ToList();
			var fingerprint = lines.FindIndex(line =>
				line.TrimStart().StartsWith("LastTokenFingerprint:", StringComparison.Ordinal));
			var lastOrder = lines.FindIndex(line => line.Trim() == "LastOrder:");
			var actor = lines.FindIndex(lastOrder + 1, line => line.Trim() == "ActorId: 2");
			if (fingerprint < 0 || lastOrder < 0 || actor < 0 ||
				!lines[fingerprint].EndsWith(",2", StringComparison.Ordinal))
				throw new InvalidOperationException("Expected canonical two-member Repair token save.");

			lines[fingerprint] = lines[fingerprint].Substring(0, lines[fingerprint].Length - 2);
			lines.RemoveAt(actor);
			return string.Join("\n", lines);
		}

		static string ForgeLastOrderBroadeningWithMatchingFingerprint(string text)
		{
			var lines = text.Split('\n').ToList();
			var fingerprint = lines.FindIndex(line =>
				line.TrimStart().StartsWith("LastTokenFingerprint:", StringComparison.Ordinal));
			var lastOrder = lines.FindIndex(line => line.Trim() == "LastOrder:");
			var actor = lines.FindIndex(lastOrder + 1, line => line.Trim() == "ActorId: 1");
			if (fingerprint < 0 || lastOrder < 0 || actor < 0 ||
				!lines[fingerprint].EndsWith("|1", StringComparison.Ordinal))
				throw new InvalidOperationException("Expected canonical one-member Repair token save.");

			lines[fingerprint] += ",3";
			lines.Insert(actor + 1, lines[actor].Replace("ActorId: 1", "ActorId: 3"));
			return string.Join("\n", lines);
		}

		static string ForgeLastOrderSwapWithMatchingFingerprint(string text)
		{
			var lines = text.Split('\n').ToList();
			var fingerprint = lines.FindIndex(line =>
				line.TrimStart().StartsWith("LastTokenFingerprint:", StringComparison.Ordinal));
			var lastOrder = lines.FindIndex(line => line.Trim() == "LastOrder:");
			var actor = lines.FindIndex(lastOrder + 1, line => line.Trim() == "ActorId: 1");
			if (fingerprint < 0 || lastOrder < 0 || actor < 0 ||
				!lines[fingerprint].EndsWith("|1", StringComparison.Ordinal))
				throw new InvalidOperationException("Expected canonical one-member Repair token save.");

			lines[fingerprint] = lines[fingerprint].Substring(0, lines[fingerprint].Length - 1) + "2";
			lines[actor] = lines[actor].Replace("ActorId: 1", "ActorId: 2");
			return string.Join("\n", lines);
		}

		static string ForgeLastOrderRouteWithMatchingFingerprint(string text)
		{
			var lines = text.Split('\n').ToList();
			var fingerprint = lines.FindIndex(line =>
				line.TrimStart().StartsWith("LastTokenFingerprint:", StringComparison.Ordinal));
			var lastOrder = lines.FindIndex(line => line.Trim() == "LastOrder:");
			var route = lines.FindIndex(lastOrder + 1, line => line.Trim() == "RouteId: 1000");
			if (fingerprint < 0 || lastOrder < 0 || route < 0 ||
				!lines[fingerprint].Contains("|100|1000|"))
				throw new InvalidOperationException("Expected canonical Repair route history.");

			lines[fingerprint] = lines[fingerprint].Replace("|100|1000|", "|100|2000|");
			lines[route] = lines[route].Replace("RouteId: 1000", "RouteId: 2000");
			return string.Join("\n", lines);
		}

		[Test]
		public void DamageBoundaryIsExactSingleUseAndCopiesCollections()
		{
			var mission = Mission();
			var members = new uint[] { 1, 2 };
			var enemies = new uint[] { 10 };
			var resume = CreateInternal<StealthRepairResumeContext>(BehaviorId.Kite,
				new OwnershipEpoch(7), mission, members, enemies, (uint?)10,
				(CPos?)new CPos(8, 0), "fight-context");
			members[0] = 99;
			enemies[0] = 98;
			var damage = new[] { new StealthRepairDamagedMember(1, 40, 100) };
			var controller = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.Damage, new OwnershipEpoch(8), -1));
			var request = CreateInternal<StealthDamageRepairRequest>(controller.CurrentHandoff,
				77L, 5, 900u, 25, damage, resume);
			damage[0] = new StealthRepairDamagedMember(2, 60, 100);

			Assert.That(controller.TryAccept(request, out var handoff), Is.True);
			Assert.That(controller.TryAccept(request, out _), Is.False);
			Assert.That(handoff.DamagedMembers.Single().ActorId, Is.EqualTo(1));
			Assert.That(handoff.Resume.MemberActorIds, Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(handoff.Resume.EnemyActorIds, Is.EqualTo(new uint[] { 10 }));
			var wrong = new StealthLifecycleController();
			Assert.That(wrong.TryAccept(request, out _), Is.False);
		}

		[Test]
		public void DamagedSquadChoosesSafeRouteAndOrdersAreDeterministicIdempotent()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out _, out var threats, out var cache,
				out var orders);
			var first = behavior.Execute();
			var second = behavior.Execute();

			Assert.That(first.LiveCause, Is.EqualTo(StealthRepairLiveCause.Retreating));
			Assert.That(first.SelectedRepairOptionActorId, Is.EqualTo(100));
			Assert.That(first.SelectedRouteIdentity, Is.EqualTo(1000));
			Assert.That(first.LastOrderToken.Kind, Is.EqualTo(StealthRepairOrderKind.Retreat));
			Assert.That(second.LastOrderToken, Is.EqualTo(first.LastOrderToken));
			Assert.That(threats.Facts, Has.Count.EqualTo(4));
			Assert.That(threats.Facts.All(facts => !facts.PlannedDecloak && !facts.PlannedAttack), Is.True);
			Assert.That(threats.Facts.All(facts => facts.Enemies.Select(enemy => enemy.ActorType)
				.SequenceEqual(new[] { "guard", "detector", "wall", "obelisk" })), Is.True);
			Assert.That(orders.Calls, Has.Count.EqualTo(2));
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
			Assert.That(orders.MutationSucceeded, Is.False);
			Assert.That(cache.Reads, Is.Zero);
		}

		[Test]
		public void NoSafeRepairIssuesNoOrderAndResumesExactFightContext()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out _, out var threats, out var cache,
				out var orders);
			threats.Score = facts => new StealthTargetThreatScore(2, 1);

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthRepairDisposition.ResumeFight));
			Assert.That(orders.Calls, Is.Empty);
			Assert.That(cache.Reads, Is.Zero);
			Assert.That(input.Controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.ResumedFight.Owner, Is.EqualTo(BehaviorId.Kite));
			Assert.That(transition.ResumedFight.Context, Is.SameAs(input.Resume));
			Assert.That(transition.ResumedFight.Context.ContextFingerprint, Is.EqualTo("fight-context"));
			Assert.That(transition.ResumedFight.Context.SelectedTargetActorId, Is.EqualTo(10));
		}

		[Test]
		public void CompletionYieldsEachSurvivingDamagedTankOnceToStart()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out _, out _, out var orders);
			var retreat = behavior.Execute();
			live.Snapshot = Live(2, new[] { Member(1, 5, 100), Member(2, 5, 100) });

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthRepairDisposition.Start));
			Assert.That(result.Completion.Members.Select(member => member.ActorId),
				Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(result.LastOrderToken, Is.EqualTo(retreat.LastOrderToken));
			Assert.That(input.Controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.StartEntries.Select(entry => entry.ActorId),
				Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(transition.StartEntries.All(entry => entry.Owner == BehaviorId.Start &&
				entry.Epoch == input.Controller.Epoch), Is.True);
			Assert.That(transition.StartEntries.Select(entry => entry.ActorId).Distinct().Count(),
				Is.EqualTo(2));
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
		}

		[Test]
		public void ArrivalAndCompletionUseOnlyTheExactOrderedDamagedSubset()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out _, out _, out var orders);
			var initial = behavior.Execute();
			Assert.That(initial.LastOrderToken.ActorIds, Is.EqualTo(new uint[] { 1, 2 }));
			var beforeHealing = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var atOption = Live(2, new[] { Member(1, 5, 40), Member(2, 0, 100) });
			live.Snapshot = atOption;
			orders.OnIssue = () => throw new InvalidOperationException("healed membership token issued then throw");
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			var replacement = orders.Issued.Last();
			Assert.That(replacement.ActorIds, Is.EqualTo(new uint[] { 1 }));
			Assert.That(replacement.Kind, Is.EqualTo(StealthRepairOrderKind.Repair));
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(beforeHealing));

			orders.OnIssue = null;
			var healing = behavior.Execute();
			Assert.That(healing.LiveCause, Is.EqualTo(StealthRepairLiveCause.Healing));
			Assert.That(healing.LastOrderToken, Is.EqualTo(replacement));
			behavior.Execute();
			Assert.That(orders.Issued, Has.Count.EqualTo(2));
			var saved = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input, atOption, out _, out _, out _, out _);
			restored.RestorePrivateState(MiniYaml.FromString(saved).Single());
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(saved));

			live.Snapshot = Live(3, new[] { Member(1, 5, 100), Member(2, 0, 100) });
			var complete = behavior.Execute();
			Assert.That(complete.Disposition, Is.EqualTo(StealthRepairDisposition.Start));
			Assert.That(complete.Completion.Members.Select(member => member.ActorId),
				Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(complete.LastOrderToken.ActorIds, Is.EqualTo(new uint[] { 1 }));
			Assert.That(input.Controller.TryAccept(complete, out var transition), Is.True);
			Assert.That(transition.StartEntries.Select(entry => entry.ActorId),
				Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(transition.StartEntries.Select(entry => entry.Epoch).Distinct().Single(),
				Is.EqualTo(input.Controller.Epoch));
			Assert.That(transition.StartEntries.Select(entry => entry.ActorId).Distinct().Count(),
				Is.EqualTo(2));
			Assert.That(input.Controller.TryAccept(complete, out _), Is.False);

			var awayInput = CreateInput();
			var awayBehavior = Behavior(awayInput, Live(1), out var awayLive,
				out _, out _, out _);
			awayBehavior.Execute();
			awayLive.Snapshot = Live(2, new[] { Member(1, 0, 40), Member(2, 5, 100) });
			var away = awayBehavior.Execute();
			Assert.That(away.LiveCause, Is.EqualTo(StealthRepairLiveCause.Retreating));
			Assert.That(away.LastOrderToken.ActorIds, Is.EqualTo(new uint[] { 1 }));
		}

		[Test]
		public void TotalLiveMemberLossYieldsCanonicalSquadConstruction()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out _, out _, out var orders);
			behavior.Execute();
			live.Snapshot = Live(2, new[]
			{
				Member(1, hp: 0, dead: true), Member(2, hp: 0, dead: true)
			});

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthRepairDisposition.SquadConstruction));
			Assert.That(result.ActiveMemberActorIds, Is.Empty);
			Assert.That(input.Controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.SquadConstructionEntry.Owner,
				Is.EqualTo(BehaviorId.SquadConstruction));
			Assert.That(transition.SquadConstructionEntry.Mission, Is.SameAs(input.Handoff.Mission));
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
		}

		[Test]
		public void CloakCandidateOptionAndScoreChangesAlwaysReevaluateWithoutChurn()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out var threats, out var cache,
				out var orders);
			behavior.Execute();
			var first = orders.Issued.Single();
			live.Snapshot = Live(2, staticActors: new[]
			{
				new StealthRepairStaticActorSnapshot(300, "wall", new CPos(3, 2), false),
				new StealthRepairStaticActorSnapshot(400, "obelisk", new CPos(8, 0), true)
			});
			behavior.Execute();
			Assert.That(threats.Facts, Has.Count.EqualTo(4));
			Assert.That(orders.Issued, Has.Count.EqualTo(1));

			live.Snapshot = Live(3, routes: new[]
			{
				new StealthRepairRouteSnapshot(1000, 100,
					new[] { new CPos(1, 0), new CPos(2, 1), new CPos(5, 0) }, true),
				Route(2000, 200, 6)
			});
			var changed = behavior.Execute();
			Assert.That(changed.LastOrderToken, Is.Not.EqualTo(first));
			Assert.That(orders.Issued, Has.Count.EqualTo(2));
			Assert.That(cache.Reads, Is.Zero);
		}

		[TestCase("member")]
		[TestCase("enemy")]
		[TestCase("option")]
		[TestCase("cloak")]
		[TestCase("candidate")]
		[TestCase("score")]
		public void EverySafetyAffectingLiveChangeIsReevaluated(string change)
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out var threats, out _, out _);
			behavior.Execute();
			var prior = threats.Facts.Count;
			var members = new[] { Member(1), Member(2, hp: 60) };
			var enemies = new[]
			{
				Enemy(), Enemy(20, "detector", 9, detector: true),
				Enemy(30, "wall", 3, range: 0), Enemy(40, "obelisk", 8, range: 6)
			};
			var options = new[]
			{
				new StealthRepairOptionSnapshot(100, new CPos(5, 0)),
				new StealthRepairOptionSnapshot(200, new CPos(6, 0))
			};
			var routes = new[] { Route(1000, 100, 5), Route(2000, 200, 6) };
			var cloaked = true;
			if (change == "member") members[0] = Member(1, x: 1);
			else if (change == "enemy") enemies[3] = Enemy(40, "obelisk", 10, range: 6);
			else if (change == "option") options[0] = new StealthRepairOptionSnapshot(100, new CPos(5, 1));
			else if (change == "cloak") cloaked = false;
			else if (change == "candidate") routes = routes.Concat(new[]
			{
				new StealthRepairRouteSnapshot(3000, 100,
					new[] { new CPos(1, 1), new CPos(5, 0) }, true)
			}).ToArray();
			else threats.Score = facts => facts.RepairOptionActorId == 100 ?
				new StealthTargetThreatScore(0, 5) : new StealthTargetThreatScore(3, 1);
			live.Snapshot = Live(2, members, options, enemies, routes: routes, cloaked: cloaked);

			behavior.Execute();

			Assert.That(threats.Facts.Count, Is.GreaterThan(prior));
		}

		[Test]
		public void LongRouteCacheIsPassiveAndNeverSuppliesSafetyOrOrders()
		{
			var input = CreateInput();
			var routes = new[] { Route(1000, 100, 5, strategic: true), Route(2000, 200, 6) };
			var behavior = Behavior(input, Live(1, routes: routes), out _, out var threats,
				out var cache, out var orders);
			var result = behavior.Execute();
			Assert.That(result.LongRouteCacheRevision, Is.EqualTo(12));
			Assert.That(cache.Reads, Is.EqualTo(1));
			Assert.That(threats.Facts, Has.Count.EqualTo(2));
			Assert.That(orders.Issued.Single().RepairOptionActorId, Is.EqualTo(100));
			behavior.Execute();
			Assert.That(cache.Reads, Is.EqualTo(1));
		}

		[Test]
		public void StrategicRepairOrdersOnlyTheCachedWaypointWithoutLiveRouteConcatenation()
		{
			var input = CreateInput();
			var routes = new[] { Route(1000, 100, 5, strategic: true) };
			var behavior = Behavior(input, Live(1, routes: routes), out _, out _,
				out var cache, out var orders);
			cache.Route = (_, __) => new StealthRepairStrategicCacheSnapshot(21,
				new[] { new CPos(2, 1), new CPos(5, 0) });

			var result = behavior.Execute();

			Assert.That(result.LongRouteCacheRevision, Is.EqualTo(21));
			Assert.That(orders.Routes.Single().Route,
				Is.EqualTo(new[] { new CPos(2, 1), new CPos(5, 0) }));
			Assert.That(orders.Routes.Single().Progress, Is.Zero);
			Assert.That(orders.Routes.Single().Route, Does.Not.Contain(new CPos(1, 0)));
		}

		[TestCase(false, 200u, StealthRepairDisposition.Retain)]
		[TestCase(true, null, StealthRepairDisposition.ResumeFight)]
		public void EmptyStrategicRepairRouteTriesNextSafeOptionOrResumesFight(
			bool allEmpty, uint? expectedOption, StealthRepairDisposition expectedDisposition)
		{
			var input = CreateInput();
			var routes = new[]
			{
				Route(1000, 100, 5, strategic: true), Route(2000, 200, 6, strategic: true)
			};
			var behavior = Behavior(input, Live(1, routes: routes), out _, out var threats,
				out var cache, out var orders);
			threats.Score = facts => new StealthTargetThreatScore(0,
				facts.RepairOptionActorId == 100 ? 0 : 1);
			cache.Route = (option, _) => new StealthRepairStrategicCacheSnapshot(22,
				allEmpty || option == 100 ? Array.Empty<CPos>() : new[] { new CPos(6, 0) });

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(expectedDisposition));
			Assert.That(result.SelectedRepairOptionActorId, Is.EqualTo(expectedOption));
			Assert.That(cache.Reads, Is.EqualTo(allEmpty ? 2 : 2));
			Assert.That(orders.Issued.Count, Is.EqualTo(allEmpty ? 0 : 1));
		}

		[Test]
		public void StrategicRepairAdvancesOneWaypointPerArrivalAndRoundTripsMidRoute()
		{
			var input = CreateInput();
			var routes = new[] { Route(1000, 100, 5, strategic: true) };
			var behavior = Behavior(input, Live(1, routes: routes), out var live, out _,
				out var cache, out var orders);
			cache.Route = (_, __) => new StealthRepairStrategicCacheSnapshot(23,
				new[] { new CPos(1, 0), new CPos(5, 0) });
			behavior.Execute();
			live.Snapshot = Live(2, new[] { Member(1, 1), Member(2, 1, 60) }, routes: routes);

			behavior.Execute();

			Assert.That(orders.Routes.Select(route => route.Progress), Is.EqualTo(new[] { 0, 1 }));
			Assert.That(orders.Routes.All(route => route.Route.SequenceEqual(
				new[] { new CPos(1, 0), new CPos(5, 0) })), Is.True);
			var saved = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input, live.Snapshot, out _, out _, out var restoredCache, out _);
			restoredCache.Route = cache.Route;
			restored.RestorePrivateState(MiniYaml.FromString(saved).Single());
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(saved));
		}

		[Test]
		public void StrategicRepairCallbackFailureRetriesExactWaypointTokenWithoutSkipping()
		{
			var input = CreateInput();
			var routes = new[] { Route(1000, 100, 5, strategic: true) };
			var behavior = Behavior(input, Live(1, routes: routes), out _, out _,
				out var cache, out var orders);
			cache.Route = (_, __) => new StealthRepairStrategicCacheSnapshot(24,
				new[] { new CPos(2, 0), new CPos(5, 0) });
			orders.OnIssue = () => throw new InvalidOperationException("issued then throw");

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			orders.OnIssue = null;
			behavior.Execute();

			Assert.That(orders.Calls[1], Is.EqualTo(orders.Calls[0]));
			Assert.That(orders.Routes, Has.Count.EqualTo(1));
			Assert.That(orders.Routes.Single().Progress, Is.Zero);
		}

		[Test]
		public void ScheduledRestoreKeepsHistoricalTickWithoutCallbacksThenReplansLive()
		{
			var input = CreateInput();
			var original = Behavior(input, Live(10), out _, out _, out _, out _);
			original.Execute();
			var saved = new List<MiniYamlNode> { original.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input, Live(14), out var live, out var threats,
				out var cache, out var orders);
			threats.Score = facts => facts.RepairOptionActorId == 200 ?
				new StealthTargetThreatScore(0, 0) : new StealthTargetThreatScore(4, 0);

			typeof(StealthRepairBehavior).GetMethod("RestorePersistedState",
				BindingFlags.Instance | BindingFlags.NonPublic).Invoke(restored,
					new object[] { MiniYaml.FromString(saved).Single() });

			Assert.That(live.Reads, Is.Zero);
			Assert.That(threats.Facts, Is.Empty);
			Assert.That(cache.Reads, Is.Zero);
			Assert.That(orders.Issued, Is.Empty);
			Assert.That(new List<MiniYamlNode> { restored.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(saved));
			var replanned = restored.Execute();
			Assert.That(replanned.SelectedRepairOptionActorId, Is.EqualTo(200));
			Assert.That(live.Reads, Is.EqualTo(1));
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
		}

		[Test]
		public void CloakedObeliskRouteIsSafeOnlyWithoutLiveDetectorCoverage()
		{
			var input = CreateInput();
			var routes = new[]
			{
				Route(1000, 100, 5, detector: false), Route(2000, 200, 6, detector: true)
			};
			var behavior = Behavior(input, Live(1, routes: routes), out var live,
				out var threats, out _, out var orders);
			threats.Score = facts => facts.FormationCloaked && !facts.HasDetectorCoverage ?
				new StealthTargetThreatScore(0, 0) : new StealthTargetThreatScore(5, 2);

			var cloaked = behavior.Execute();
			Assert.That(cloaked.SelectedRouteIdentity, Is.EqualTo(1000));
			Assert.That(threats.Facts.All(facts => facts.Enemies.Any(enemy =>
				enemy.ActorType == "obelisk") && !facts.PlannedDecloak && !facts.PlannedAttack), Is.True);

			live.Snapshot = Live(2, routes: routes, cloaked: false);
			var visible = behavior.Execute();
			Assert.That(visible.Disposition, Is.EqualTo(StealthRepairDisposition.ResumeFight));
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
		}

		[Test]
		public void CallbackIssuedThenThrowAndActivityLossRemainTokenIdempotent()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out _, out _, out var orders);
			orders.OnIssue = () => throw new InvalidOperationException("issued then throw");
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			orders.OnIssue = null;
			var recovered = behavior.Execute();
			Assert.That(orders.Calls[1], Is.EqualTo(orders.Calls[0]));
			Assert.That(orders.Issued, Has.Count.EqualTo(1));

			live.Snapshot = Live(2, observe: true);
			behavior.Execute();
			Assert.That(orders.Issued, Has.Count.EqualTo(1));
			Assert.That(recovered.LastOrderToken, Is.EqualTo(orders.Issued.Single()));
		}

		[Test]
		public void MembershipChangeAndOptionInvalidationReplaceOnlyTheExactToken()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out var threats, out _, out var orders);
			behavior.Execute();
			var first = orders.Issued.Single();
			var beforeMembership = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			live.Snapshot = Live(2, new[]
			{
				Member(1), Member(2, hp: 0, dead: true)
			});
			orders.OnIssue = () => throw new InvalidOperationException("membership token issued then throw");
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			var replacement = orders.Issued.Last();
			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString(),
				Is.EqualTo(beforeMembership));
			orders.OnIssue = null;
			var membership = behavior.Execute();
			Assert.That(membership.LastOrderToken.ActorIds, Is.EqualTo(new uint[] { 1 }));
			Assert.That(membership.LastOrderToken, Is.Not.EqualTo(first));
			Assert.That(membership.LastOrderToken, Is.EqualTo(replacement));
			behavior.Execute();
			Assert.That(orders.Issued, Has.Count.EqualTo(2));

			threats.Score = facts => facts.RepairOptionActorId == 200 ?
				new StealthTargetThreatScore(0, 2) : new StealthTargetThreatScore(3, 1);
			live.Snapshot = Live(3, new[] { Member(1), Member(2, hp: 0, dead: true) },
				options: new[]
				{
					new StealthRepairOptionSnapshot(100, new CPos(5, 0), isAvailable: false),
					new StealthRepairOptionSnapshot(200, new CPos(6, 0))
				});
			var invalidated = behavior.Execute();
			Assert.That(invalidated.SelectedRepairOptionActorId, Is.EqualTo(200));
			Assert.That(invalidated.LastOrderToken, Is.Not.EqualTo(membership.LastOrderToken));
			Assert.That(orders.Issued, Has.Count.EqualTo(3));
		}

		[Test]
		public void StaleForgedAndReentrantLiveCallbacksCannotCommit()
		{
			var input = CreateInput();
			var guard = new GuardProbe();
			var behavior = Behavior(input, Live(1, damageAmount: 26), out _, out var threats,
				out var cache, out var orders, guard);
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(threats.Facts, Is.Empty);
			Assert.That(cache.Reads, Is.Zero);
			Assert.That(orders.Issued, Is.Empty);
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "LastObservedTick").Value.Value, Is.EqualTo("-1"));

			behavior = Behavior(input, Live(1), out var live, out _, out _, out _, guard);
			live.OnRead = () => behavior.Execute();
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "LastObservedTick").Value.Value, Is.EqualTo("-1"));
		}

		[TestCase("hp")]
		[TestCase("member")]
		[TestCase("resume")]
		[TestCase("damage")]
		public void EveryForgedOrStaleEntryFactFailsBeforeSafety(string change)
		{
			var input = CreateInput();
			var members = new[] { Member(1), Member(2, hp: 60) };
			var resume = "fight-context";
			var damage = 25;
			if (change == "hp") members[0] = Member(1, hp: 41);
			else if (change == "member") members[1] = Member(3, hp: 60);
			else if (change == "resume") resume = "forged";
			else damage = 26;
			var behavior = Behavior(input, Live(1, members, resumeFingerprint: resume,
				damageAmount: damage), out _, out var threats, out var cache, out var orders);

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(threats.Facts, Is.Empty);
			Assert.That(cache.Reads, Is.Zero);
			Assert.That(orders.Issued, Is.Empty);
		}

		[Test]
		public void OwnershipLossDuringOrderAndRecursiveRestoreRollBackTransactionally()
		{
			var input = CreateInput();
			var guard = new GuardProbe();
			var behavior = Behavior(input, Live(1), out _, out _, out _, out var orders, guard);
			orders.OnIssue = () => guard.Active = false;
			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "LastObservedTick").Value.Value, Is.EqualTo("-1"));

			guard.Active = true;
			orders.OnIssue = null;
			behavior.Execute();
			var saved = behavior.SerializePrivateState();
			var recursive = Behavior(input, Live(1), out var live, out _, out _, out _, guard);
			live.OnRead = () => recursive.RestorePrivateState(saved);
			Assert.Throws<InvalidOperationException>(() => recursive.RestorePrivateState(saved));
			Assert.That(recursive.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "LastObservedTick").Value.Value, Is.EqualTo("-1"));
		}

		[TestCase("threat")]
		[TestCase("cache")]
		public void RecursiveSafetyAndCacheCallbacksCannotCommit(string callback)
		{
			var input = CreateInput();
			StealthRepairRouteSnapshot[] routes = null;
			if (callback == "cache")
				routes = new[] { Route(1000, 100, 5, strategic: true), Route(2000, 200, 6) };
			var behavior = Behavior(input, Live(1, routes: routes), out _, out var threats,
				out var cache, out _, new GuardProbe());
			if (callback == "threat")
				threats.OnCalculate = () => behavior.Execute();
			else
				cache.OnRead = () => behavior.Execute();

			Assert.Throws<InvalidOperationException>(() => behavior.Execute());
			Assert.That(behavior.SerializePrivateState().Value.Nodes.Single(node =>
				node.Key == "LastObservedTick").Value.Value, Is.EqualTo("-1"));
		}

		[Test]
		public void PrivateStateRoundTripsAndRejectsExactFactTamperingTransactionally()
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
				text.Replace("Amount: 25", "Amount: 26"),
				text.Replace("HP: 40", "HP: 41"),
				text.Replace("OptionId: 100", "OptionId: 101"),
				text.Replace("RouteId: 1000", "RouteId: 1001"),
				text.Replace("DangerCrossover: 4", "DangerCrossover: 5"),
				text.Replace("Fingerprint: fight-context", "Fingerprint: forged-context"),
				text.Replace("ActivityRevision: 0", "ActivityRevision: 1"),
				ForgeLastOrderSubsetWithMatchingFingerprint(text),
				text.Replace("Owner: Repair", "Owner: Damage")
			})
			{
				var invalid = Behavior(input, snapshot, out _, out _, out _, out _);
				Assert.Throws<InvalidOperationException>(() => invalid.RestorePrivateState(
					MiniYaml.FromString(tampered).Single()));
				Assert.That(invalid.SerializePrivateState().Value.Nodes.Single(node =>
					node.Key == "LastObservedTick").Value.Value, Is.EqualTo("-1"));
			}
		}

		[Test]
		public void TerminalCompletionRoundTripsWithExactLastTokenAndCompletionEvidence()
		{
			var input = CreateInput();
			var behavior = Behavior(input, Live(1), out var live, out _, out _, out _);
			behavior.Execute();
			live.Snapshot = Live(2, new[] { Member(1, 5, 40), Member(2, 0, 100) });
			var repair = behavior.Execute();
			var completed = Live(3, new[] { Member(1, 5, 100), Member(2, 0, 100) });
			live.Snapshot = completed;
			var result = behavior.Execute();
			Assert.That(result.LastOrderToken.ActorIds, Is.EqualTo(new uint[] { 1 }));
			Assert.That(result.Completion.Members.Select(member => member.ActorId),
				Is.EqualTo(new uint[] { 1, 2 }));
			var text = new List<MiniYamlNode> { behavior.SerializePrivateState() }.WriteToString();
			var restored = Behavior(input, completed, out _, out _, out _, out _);
			restored.RestorePrivateState(MiniYaml.FromString(text).Single());
			Assert.That(restored.SerializePrivateState().Value.Nodes.Any(node =>
				node.Key == "LastOrder"), Is.True);
			Assert.That(text, Does.Contain("LastTokenFingerprint:"));
			Assert.That(text, Does.Contain("Completion:"));
			Assert.That(text, Does.Contain("RouteId: " + repair.LastOrderToken.RouteIdentity));

			foreach (var tampered in new[]
			{
				text.Replace("Kind: Repair", "Kind: Retreat"),
				ForgeLastOrderSwapWithMatchingFingerprint(text),
				ForgeLastOrderBroadeningWithMatchingFingerprint(text),
				ForgeLastOrderRouteWithMatchingFingerprint(text)
			})
			{
				var invalid = Behavior(input, completed, out _, out _, out _, out _);
				Assert.Throws<InvalidOperationException>(() => invalid.RestorePrivateState(
					MiniYaml.FromString(tampered).Single()));
				Assert.That(invalid.SerializePrivateState().Value.Nodes.Single(node =>
					node.Key == "LastObservedTick").Value.Value, Is.EqualTo("-1"));
			}
		}
	}
}
