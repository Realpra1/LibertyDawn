#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.BotModules;
using OpenRA.Mods.Common.Traits.BotModules.Squads;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class StealthAIFunctionMatrixTest
	{
		static string RepositoryRoot
		{
			get
			{
				var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
				while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Makefile")))
					directory = directory.Parent;

				return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
			}
		}

		static string Source(string relativePath) => File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

		[Test]
		public void OriginalManagerAndSquadRemainTheOnlyLiveOwners()
		{
			var assembly = typeof(SquadManagerBotModule).Assembly;
			Assert.That(assembly.GetType("OpenRA.Mods.Common.Traits.StealthAIModule"), Is.Null);
			Assert.That(assembly.GetType("OpenRA.Mods.Common.Traits.BotModules.Squads.StealthAISquad"), Is.Null);
			Assert.That(assembly.GetType("OpenRA.Mods.Common.Traits.StealthAISpecialistModule"), Is.Null);

			var squadLists = assembly.GetTypes()
				.SelectMany(type => type.GetFields(BindingFlags.Instance | BindingFlags.Public |
					BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
					.Where(field => field.FieldType == typeof(List<Squad>))
					.Select(field => (Type: type, Field: field)))
				.Where(owner => owner.Type == typeof(SquadManagerBotModule) ||
					owner.Type.Name.IndexOf("Stealth", StringComparison.OrdinalIgnoreCase) >= 0)
				.ToArray();

			Assert.That(squadLists.Select(owner => owner.Type), Is.EqualTo(new[] { typeof(SquadManagerBotModule) }));
			Assert.That(squadLists.Single().Field.Name, Is.EqualTo("Squads"));
			Assert.That(Enum.IsDefined(typeof(SquadType), "Stealth"), Is.True);
		}

		[Test]
		public void StateMachineTicksCopiedAirDecisionBodiesDirectly()
		{
			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(states, Does.Contain("class StealthAIIdleState : StealthAIStateBase, IState"));
			Assert.That(states, Does.Contain("var e = FindDefenselessTarget(owner);"));
			Assert.That(states, Does.Contain("class StealthAIAttackState : StealthAIStateBase, IState"));
			Assert.That(states, Does.Contain("owner.World.WorldTick >= owner.AirNextTargetReviewTick"));
			Assert.That(states, Does.Contain("StealthAIThreatGeometry.ShouldSwitchTarget"));
			Assert.That(states, Does.Contain("new Order(\"Attack\", a, Target.FromActor(owner.TargetActor), false)"));
			Assert.That(states, Does.Not.Contain("TickAirOwned"));
			Assert.That(states, Does.Not.Contain("StealthAISpecialistBehavior"));
		}

		[Test]
		public void AuthoritativeMatrixHasExactDispositionAndRetreatFreeIntegrationOrder()
		{
			using (var matrix = JsonDocument.Parse(Source(
				".agents/inspiration/stealth-ai-pre-air-copy/FINAL-MATRIX.json")))
			{
				var root = matrix.RootElement;
				var counts = root.GetProperty("counts");
				Assert.That(counts.GetProperty("all_functions").GetInt32(), Is.EqualTo(184));
				Assert.That(counts.GetProperty("RESTORE_NECESSARY").GetInt32(), Is.EqualTo(92));
				Assert.That(counts.GetProperty("RESTORE_BETTER").GetInt32(), Is.EqualTo(6));
				Assert.That(counts.GetProperty("KEEP_AIR").GetInt32(), Is.EqualTo(51));
				Assert.That(counts.GetProperty("LEAVE_OUT_RETREAT").GetInt32(), Is.EqualTo(35));

				var order = root.GetProperty("integration_order").EnumerateArray()
					.Select(id => id.GetString()).ToArray();
				var retreat = root.GetProperty("verdict_matrix").GetProperty("LEAVE_OUT_RETREAT")
					.EnumerateArray().Select(id => id.GetString()).ToArray();
				Assert.That(order, Has.Length.EqualTo(98));
				Assert.That(order, Is.Unique);
				Assert.That(retreat, Has.Length.EqualTo(35));
				Assert.That(order.Intersect(retreat), Is.Empty);
				Assert.That(root.GetProperty("retreat_free_extraction_ids").GetArrayLength(), Is.EqualTo(5));
			}
		}

		[Test]
		public void IndependentLiveProvenanceMapsEveryRestoreId()
		{
			using (var matrix = JsonDocument.Parse(Source(
				".agents/inspiration/stealth-ai-pre-air-copy/FINAL-MATRIX.json")))
			using (var provenance = JsonDocument.Parse(Source(
				".agents/inspiration/stealth-ai-pre-air-copy/live-provenance/LIVE-MAP.json")))
			{
				var expected = matrix.RootElement.GetProperty("integration_order").EnumerateArray()
					.Select(id => id.GetString()).ToArray();
				var root = provenance.RootElement;
				var counts = root.GetProperty("counts");
				Assert.That(root.GetProperty("status").GetString(), Is.EqualTo("PASS"));
				Assert.That(counts.GetProperty("EXACT_BODY").GetInt32(), Is.EqualTo(47));
				Assert.That(counts.GetProperty("RETREAT_FREE_EXTRACTION").GetInt32(), Is.EqualTo(5));
				Assert.That(counts.GetProperty("COMPOSED_INTO_AIR_BODY").GetInt32(), Is.EqualTo(46));
				Assert.That(counts.GetProperty("MISSING").GetInt32(), Is.Zero);
				Assert.That(counts.GetProperty("CONFLICTING_OLD_BODY").GetInt32(), Is.Zero);

				var records = root.GetProperty("records").EnumerateArray().ToArray();
				var mapped = records.Select(record => record.GetProperty("id").GetString()).ToArray();
				Assert.That(mapped, Has.Length.EqualTo(98));
				Assert.That(mapped, Is.Unique);
				Assert.That(mapped, Is.EquivalentTo(expected));
				Assert.That(records.All(record => record.GetProperty("reachable").GetBoolean()), Is.True);
				Assert.That(records.All(record => record.GetProperty("live").GetArrayLength() > 0), Is.True);
			}
		}

		[Test]
		public void CncYamlConfiguresTwoNamedProfilesWithinFourSquadCap()
		{
			var yaml = Source("mods/cnc/rules/ai.yaml");
			Assert.That(yaml.Split("stealth-tank:").Length - 1, Is.EqualTo(10));
			Assert.That(yaml.Split("chemical:").Length - 1, Is.EqualTo(10));
			Assert.That(yaml.Split("UnitTypes: stnk").Length - 1, Is.EqualTo(10));
			Assert.That(yaml.Split("UnitTypes: ctnk").Length - 1, Is.EqualTo(10));
			Assert.That(yaml.Split("StrategicCellSize: 6").Length - 1, Is.EqualTo(20));
			Assert.That(yaml, Does.Not.Contain("StealthAISpecialistModule@"));

			Assert.That(StealthAISpecialistPolicy.MaximumSquadCount, Is.EqualTo(4));
			var manager = Source("OpenRA.Mods.Common/Traits/BotModules/SquadManagerBotModule.cs");
			Assert.That(manager, Does.Contain("specialistSquadCount > StealthAISpecialistPolicy.MaximumSquadCount"));
			Assert.That(manager, Does.Contain("RebalanceStealthSquads();"));
			Assert.That(manager.IndexOf("RebalanceStealthSquads();", StringComparison.Ordinal),
				Is.LessThan(manager.IndexOf("RecruitUnassignedCombatUnits(bot);", StringComparison.Ordinal)));
			Assert.That(new StealthSquadDefinition(new MiniYaml("", new List<MiniYamlNode>())).StrategicCellSize,
				Is.EqualTo(6));
		}

		[Test]
		public void StealthPlanningUsesCachedSixCellRoutesAndIndependentSafety()
		{
			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			var yaml = Source("mods/cnc/rules/ai.yaml");
			Assert.That(states, Does.Contain("StealthInfluenceCaches"));
			Assert.That(states, Does.Contain("SelectTargetCandidates("));
			Assert.That(states, Does.Contain("foreach (var selectedIndex in selectedIndices)"));
			Assert.That(states, Does.Contain("TickStealthSafety(Squad owner, bool pendingBlueOnly = false)"));
			Assert.That(states, Does.Contain("NearestSafeStealthNeighbor"));
			Assert.That(states, Does.Contain("resourceType == \"BlueTiberium\""));
			Assert.That(states, Does.Contain("resourceType == \"RedTiberium\""));
			Assert.That(states, Does.Contain("PendingBlueExplosionInSquadCell(owner, activeMembers)"));
			Assert.That(states, Does.Contain("resourceLayer.GetResource(cell).Type == \"BlueTiberium\" &&"));
			Assert.That(states, Does.Contain("resourceLayer.IsExplosionPending(cell)"));
			Assert.That(states, Does.Not.Contain("memberCoarseCells.Any(cache.PendingExplosionCells.Contains)"));
			Assert.That(states, Does.Not.Contain("Target.FromCell(owner.World, aircraft.Location)"),
				"An unavailable reinforcement route must preserve useful movement instead of issuing a self-Move.");
			Assert.That(states, Does.Contain("cache, cell.Key, cache.MobilityDanger"),
				"Mass-clear crossover must remain reachable when ordinary harassment routing rejects a defended corridor.");
			Assert.That(states, Does.Contain("target route unavailable; queued {3}-waypoint safe formation join"),
				"A stranded reinforcement must prefer its cached safe formation route.");
			Assert.That(states, Does.Contain("issued rate-limited direct catch-up Move to active cell"),
				"When no cached route exists, a reinforcement must use ordinary ground pathing toward the active formation.");
			Assert.That(states, Does.Contain("AirReinforcementFallbackTicks.TryGetValue"));
			Assert.That(states, Does.Contain("QueueStealthReinforcementsToFormation(owner);"),
				"STNK squads must service formation-owned reinforcement catch-up with and without a mission target.");
			Assert.That(states, Does.Contain("AirReinforcementJoinCells.TryGetValue"));
			Assert.That(states, Does.Contain("if (!reinforcement.IsIdle && routedAnchorValid)"),
				"A progressing catch-up route must survive formation-center and mission-target movement.");
			Assert.That(states, Does.Contain("preserveStealthRoute"));
			Assert.That(states, Does.Contain("ShouldPreserveOwnedMissionRoute("));
			Assert.That(states, Does.Contain("owner.TargetActor == plan.Actor,"));
			Assert.That(states, Does.Contain("owner.StealthClearMode, plan.StealthMode"),
				"Target review may preserve a moving route only when actor and mission mode are unchanged.");
			Assert.That(states, Does.Contain("routeTraveling && owner.StealthRouteLastCenterCell != null"));
			Assert.That(states, Does.Contain("suppressed stalled-target rescan"),
				"The short target watchdog must not cancel a moving route whose squad center is progressing.");
			Assert.That(states, Does.Contain("owner.StealthProfile == \"stealth-tank\" || !BusyAttack(a)"),
				"A queued unchanged STNK destination remains owned while its final Move transitions into Attack.");
			Assert.That(states, Does.Contain("owner.AirTargetLastProgressTick = owner.World.WorldTick;"),
				"Any real engine route movement, including a detour away from the target, refreshes no-progress age.");
			Assert.That(states, Does.Contain("if (owner.StealthProfile != \"stealth-tank\")\n\t\t\t\towner.AirReinforcementTargets.Clear();"),
				"STNK formation-owned catch-up state must survive economic target-plan reviews.");
			var invalidTarget = states.IndexOf("if (!owner.IsTargetValid)", StringComparison.Ordinal);
			var staleRouteClear = states.IndexOf("owner.AirRouteQueued = false;", invalidTarget,
				StringComparison.Ordinal);
			var invalidTargetScan = states.IndexOf("var nextTarget = rememberedTargetCell == null", invalidTarget,
				StringComparison.Ordinal);
			Assert.That(staleRouteClear, Is.GreaterThan(invalidTarget));
			Assert.That(staleRouteClear, Is.LessThan(invalidTargetScan),
				"An invalid target must release its transient shared-route latch before reacquisition.");
			Assert.That(states, Does.Not.Contain("nearestCell: true"),
				"Unavailable reinforcement routes must not churn through neighboring-cell retries.");
			Assert.That(states, Does.Contain("groupedActors: members"),
				"Local escape must use one ordinary grouped Move and leave ground pathing to the engine.");
			Assert.That(states, Does.Contain("ReachedOrPassedStealthEscapeCell(start, destination, center.Value)"),
				"The escape latch must release when the squad center enters or crosses the adjacent strategic cell.");
			Assert.That(states, Does.Contain("pendingBlueExplosion, ActiveStealthCenterCell(owner)"),
				"The approved escape neighbor must be adjacent to the active squad center, not a dispersed representative.");
			Assert.That(states, Does.Contain("var members = AirDecisionUnits(owner).Where"),
				"Repairing units and reinforcements must not skew the active formation's local escape center.");
			Assert.That(states, Does.Contain("IssueStealthEscape(owner, decisionUnits"),
				"A local safety Move must preserve reinforcement ownership and move only the active formation.");
			Assert.That(states, Does.Contain("Math.Abs(destinationCell.X - start.X) > 1"),
				"The grouped escape path must reject any non-neighboring strategic destination.");
			Assert.That(states, Does.Not.Contain("activeMembers.Any(a => !a.IsIdle && !BusyAttack(a))"),
				"Escape completion must not wait for every member's movement state.");
			Assert.That(states, Does.Not.Contain("StealthRouteToCell(owner, representative, cache, goal, cache.Danger, true)"),
				"Local escape must not add an A* route or fan representative waypoints across the squad.");
			Assert.That(states, Does.Contain("KiteParticipantTookDamage(owner)"));
			Assert.That(states, Does.Contain("KiteFormationIsLocallySafe(owner, cache, decisionUnits, owner.TargetActor)"));
			Assert.That(states, Does.Contain("CachedThreatCoversReveal(owner, t, unit.CenterPosition, target)"));
			Assert.That(states, Does.Contain("SafeOrdinaryFiringCell(owner, representative, cache, actor)"),
				"Ordinary attacks must end at a cached all-threat-safe exact firing cell before revealing.");
			var attackState = states.Substring(states.IndexOf("class StealthAIAttackState", StringComparison.Ordinal));
			attackState = attackState.Substring(0,
				attackState.IndexOf("class StealthAIFleeState", StringComparison.Ordinal));
			Assert.That(attackState, Does.Not.Contain(
				"RevealedAttackPositionIsCovered(owner.TargetActor, liveStealthThreats)"),
				"A cached-safe exact firing-cell plan must not be discarded by the obsolete target-center reveal check.");
			Assert.That(states, Does.Contain("definition.DetectorRangeBufferCells"),
				"Kite and ordinary reveal safety must include cached non-target detection coverage.");
			Assert.That(states, Does.Contain("Stealth crush bridge [{0}] selected cached blocker:"),
				"An eligible cached infantry blocker must bridge a rejected MTNK Kite into crush then backoff/Kite progress.");
			Assert.That(states, Does.Contain("next=backoff-and-kite"));
			Assert.That(states, Does.Contain("StealthClearMode.CrushBridge"));
			Assert.That(states, Does.Contain("completed cached blocker:"));
			Assert.That(states, Does.Contain("legal-band backoff queued:"));
			Assert.That(states, Does.Contain("OrderBy(p => p.ServiceMs)"),
				"Comparable-value STNK targets should prefer bounded travel-plus-kill service time.");
			Assert.That(states, Does.Contain("p.ServiceMs, owner.StealthDefinition.MaximumUndefendedTargetTravelSeconds"),
				"The ordinary-target preference must bound total travel-plus-kill service, not travel alone.");
			Assert.That(states, Does.Contain("bounded Crush fallback:"),
				"A moving ordinary infantry crush must fall back to cached safe fire or a bounded safety replan.");
			Assert.That(states, Does.Contain("owner.SquadManager.Info.AirTargetStallTicks, true"),
				"Ordinary Crush must reuse the existing target progress budget instead of latching indefinitely.");
			Assert.That(states, Does.Contain("EconomyMammothCrushMove.OrderId"),
				"A cached safe Crush route must finish with the engine's live actor-tracking crush activity.");
			Assert.That(states, Does.Contain("Stealth live target [{0}] Crush check:"));
			Assert.That(states, Does.Contain("Stealth live target [{0}] Kite check:"));
			Assert.That(states, Does.Contain("routeChanged ? \"live-route\" : \"useful-order\""),
				"A 12-tick live check must expose useful-order preservation instead of unconditional churn.");
			Assert.That(states, Does.Contain("var cache = CachedStealthInfluence(owner, formation[0])"));
			Assert.That(states, Does.Contain("world-scans=0"),
				"Live micro telemetry must prove that owned-target checks do not trigger a world scan.");
			var vehicles = Source("mods/cnc/rules/vehicles.yaml");
			var stnk = vehicles.Substring(vehicles.IndexOf("STNK:", StringComparison.Ordinal));
			stnk = stnk.Substring(0, stnk.IndexOf("\nMHQ:", StringComparison.Ordinal));
			Assert.That(stnk, Does.Contain("EconomyMammothCrushMove:"));
			Assert.That(states, Does.Contain("owner.Type != SquadType.Stealth && challenger != null"),
				"A valid static STNK mission must reach service instead of restarting for every economic challenger.");
			Assert.That(states, Does.Contain("if (cache == null || !cache.Threats.Any(t => t.Actor == target)"),
				"An owned Kite must reject a transiently unavailable influence cache and use the existing safe replan path.");
			var watchdogGuard = states.IndexOf(
				"if (!owner.SquadManager.Info.AirTargetDebugLogging || owner.StealthProfile != \"stealth-tank\")",
				StringComparison.Ordinal);
			Assert.That(watchdogGuard, Is.GreaterThanOrEqualTo(0));
			Assert.That(watchdogGuard, Is.LessThan(states.IndexOf(
				"if (owner.StealthDebugMotion == null)", watchdogGuard, StringComparison.Ordinal)),
				"Debug-disabled games must return before allocating or enumerating per-member watchdog state.");
			Assert.That(states, Does.Contain("StealthKiteParticipantHealth[participant.ActorID]"));
			Assert.That(states, Does.Contain("completed owned MTNK lifecycle"),
				"BotDebug evidence must distinguish an owned Kite completion from mode-unaware observer range inference.");
			Assert.That(states, Does.Not.Contain("Stealth kite [{0}] Air-style target switch"),
				"Strategic mission value must not replace an already-owned cached-package Kite defender.");
			Assert.That(states, Does.Contain("owner.StealthClearPackage.Contains(owner.TargetActor.ActorID)"),
				"A valid owned Kite defender remains latched until it dies, invalidates, or safety aborts.");
			Assert.That(states, Does.Contain("var finishedKiteDefender = owner.StealthClearMode == StealthClearMode.Kite"));
			Assert.That(states, Does.Contain("if (finishedKiteDefender)"),
				"A completed defender lifecycle must release the package before mission-cell replanning.");
			Assert.That(states, Does.Contain("Stealth mass [{0}] cleared empty cached package"));
			Assert.That(states, Does.Contain("same-tick mission reacquisition, no completion retreat"),
				"An empty Mass package must release stale state without retreating or waiting on refreshed cache age.");
			Assert.That(states, Does.Not.Contain("package.Count == 0 && owner.World.WorldTick - cache.Tick"),
				"A freshly rebuilt empty package must not renew the stale Mass wait forever.");
			var pendingLatch = states.IndexOf("owner.StealthEscapePendingExplosion = pendingBlueExplosion;",
				StringComparison.Ordinal);
			Assert.That(pendingLatch, Is.GreaterThanOrEqualTo(0));
			Assert.That(pendingLatch, Is.LessThan(states.IndexOf(
				"if (owner.SquadManager.Info.AirTargetDebugLogging)", pendingLatch, StringComparison.Ordinal)));
			Assert.That(yaml, Does.Not.Contain("StealthResourceExplosionTestDriver"));
			Assert.That(yaml, Does.Not.Contain("StealthPendingBlueOrderObserver"));
			Assert.That(yaml, Does.Not.Contain("StealthCrushTestTelemetry"));
			Assert.That(StealthAISpecialistPolicy.IsEngagementThreat(true, true, false), Is.True);
			Assert.That(StealthAISpecialistPolicy.IsEngagementThreat(false, true, false), Is.False);
			Assert.That(StealthAISpecialistPolicy.NextKillCadenceAge(100, 25, false, false), Is.EqualTo(125));
			Assert.That(StealthAISpecialistPolicy.NextKillCadenceAge(100, 25, false, true), Is.EqualTo(100));
			Assert.That(StealthAISpecialistPolicy.NextKillCadenceAge(100, 25, true, false), Is.Zero);
			Assert.That(StealthAISpecialistPolicy.KillCadenceFailed(2249, 2250), Is.False);
			Assert.That(StealthAISpecialistPolicy.KillCadenceFailed(2250, 2250), Is.True);
			var manager = Source("OpenRA.Mods.Common/Traits/BotModules/SquadManagerBotModule.cs");
			var squad = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/Squad.cs");
			Assert.That(manager, Does.Contain("StealthLiveTargetCheckInterval = 12"));
			Assert.That(manager, Does.Contain("s.TickStealthLiveTarget()"),
				"Owned Crush/Kite live checks must run independently of the 75-tick strategy interval.");
			Assert.That(manager, Does.Contain("status=timed-out cadence-age={5}/{6}"),
				"Debug acceptance runs must account for an expired STNK squad without changing cadence state.");
			Assert.That(manager, Does.Contain("e.Attacker.Info.Name == \"stnk\""));
			Assert.That(manager, Does.Contain("stealthSquad.StealthKillCadenceAge = 0"),
				"Only an attributed STNK kill may reset its current squad's running cadence window.");
			Assert.That(manager, Does.Not.Contain("internal int StealthKillCadenceAge"),
				"Cadence state must not be owner-wide.");
			Assert.That(squad, Does.Contain("internal int StealthKillCadenceAge"),
				"Every persistent configured STNK squad must own an independent cadence clock.");
			var cadenceObserver = states.IndexOf("static void TickStealthDebugKillCadenceWatchdog",
				StringComparison.Ordinal);
			Assert.That(cadenceObserver, Is.GreaterThanOrEqualTo(0));
			Assert.That(states.IndexOf("if (owner.StealthProfile != \"stealth-tank\")",
				cadenceObserver, StringComparison.Ordinal), Is.GreaterThan(cadenceObserver),
				"Release-default cadence service state must not depend on debug telemetry.");
			Assert.That(manager, Does.Contain("stealthSquadAssignments"));
			Assert.That(manager, Does.Contain("assignment.Definition == configured.Key ? assignment.Index"),
				"Temporary control or reservation must not move a returning STNK to another persistent squad.");
			var rebalance = manager.Substring(manager.IndexOf("void RebalanceStealthSquads()",
				StringComparison.Ordinal));
			rebalance = rebalance.Substring(0, rebalance.IndexOf("bool IsManagerOwnedSpecialist",
				StringComparison.Ordinal));
			Assert.That(rebalance, Does.Not.Contain("World.Actors"),
				"The frequent specialist lifecycle must never enumerate or materialize the whole world.");
			Assert.That(rebalance, Does.Contain("activeUnits"));
			Assert.That(rebalance, Does.Contain("unassignedCombatUnits?.UnassignedActors"),
				"Newly admitted combat units must join the existing manager-owned cache without a world scan.");
			Assert.That(rebalance, Does.Contain("World.GetActorById(id) == null"),
				"Persistent squad affinity should be pruned by bounded assignment lookup only after the actor leaves the world.");
			Assert.That(states, Does.Contain("squad acceptance: tick={0}"));
			Assert.That(states, Does.Contain("members=[{9}]"),
				"Acceptance telemetry must identify bounded current membership for churn audits.");
			var lifecycleObserver = states.IndexOf("static void RecordStealthDebugLifecycle",
				StringComparison.Ordinal);
			Assert.That(lifecycleObserver, Is.GreaterThanOrEqualTo(0));
			Assert.That(states.IndexOf(
				"if (!owner.SquadManager.Info.AirTargetDebugLogging || owner.StealthProfile != \"stealth-tank\")",
				lifecycleObserver, StringComparison.Ordinal), Is.GreaterThan(lifecycleObserver));
			var lifecycleFlush = states.IndexOf("static void FlushStealthDebugLifecycle",
				lifecycleObserver, StringComparison.Ordinal);
			Assert.That(lifecycleFlush, Is.GreaterThan(lifecycleObserver));
			Assert.That(states.Substring(lifecycleObserver, lifecycleFlush - lifecycleObserver),
				Does.Not.Contain("Log.Write"),
				"Lifecycle capture must perform no pre-failure log I/O that can perturb wall-budgeted arbitration.");
			Assert.That(states.Substring(lifecycleObserver, lifecycleFlush - lifecycleObserver),
				Does.Not.Contain("QueueOrder"),
				"Lifecycle observation must not change simulation decisions.");
			Assert.That(states, Does.Contain("new Queue<StealthDebugLifecycleSnapshot>(256)"));
			Assert.That(states, Does.Contain("if (history.Count == 256)"));
			Assert.That(states, Does.Contain("buffered-member: tick={0}"));
			Assert.That(states, Does.Contain("activity-current={27}:{28}:cancel={29}"),
				"Failure flush must retain exact activity and cancellation state.");
			Assert.That(states, Does.Contain("tick - previous.LastReportTick >= 75"));
			Assert.That(states, Does.Contain("maximumTicks - 600"));
			Assert.That(states, Does.Contain("RecordStealthDebugLifecycle(owner, false);"));
			Assert.That(states, Does.Contain("RecordStealthDebugLifecycle(owner, true);"));
			Assert.That(states, Does.Contain("FlushStealthDebugLifecycle(owner, stnks);"));
			Assert.That(manager, Does.Contain("Stealth squad lifecycle [{0}] empty timeout:"),
				"A genuinely empty squad must expire instead of living indefinitely under a no-actor exemption.");
			Assert.That(manager, Does.Contain("Stealth squad lifecycle [{0}] remade:"),
				"Newly available members must remake an expired configured squad with a fresh lifecycle.");
			Assert.That(manager, Does.Contain("transient-affinity=False"),
				"Temporary control or reservation must retain affinity and not trigger an empty lifecycle reset.");
			Assert.That(states, Does.Contain("Stealth target service [stealth-tank] destination owned"),
				"Focused games need explicit destination ownership and queued target-service evidence.");
			Assert.That(states, Does.Contain("already-owned alternatives."),
				"Independent STNK squads should avoid competing for one final-kill credit when bounded alternatives exist.");
			Assert.That(states, Does.Contain("if (deconflicted.Count > 0)"),
				"Target deconfliction must remain a preference and fall back to shared service instead of idling.");
			Assert.That(states, Does.Contain("var stnks = owner.Units.Where"),
				"Formation promotion and reinforcement membership must share one squad clock.");
			Assert.That(states, Does.Not.Contain(
				"unit.Info.Name == \"stnk\" && !squad.AirReinforcements.Contains"),
				"Reinforcement accounting must not remove live squad members from cadence observation.");
		}

		[Test]
		public void SelectedGroundPoliciesRetainRequiredSemanticsWithoutRetreatController()
		{
			Assert.That(new SquadManagerBotModuleInfo().AirTargetDebugLogging, Is.False,
				"Ground route provenance must perform no logging work in ordinary release configuration.");
			Assert.That(StealthAISpecialistPolicy.AreAllCandidatesUnavailable(3, 1, 2), Is.True);
			Assert.That(StealthAISpecialistPolicy.ReassessTarget(true, true, 100,
				true, true, 200, 25), Is.EqualTo(StealthTankTargetReassessment.SwitchToChallenger));
			Assert.That(StealthAISpecialistPolicy.ShouldIssueSafeMobilityRoute(true, true, false), Is.True);
			Assert.That(StealthAISpecialistPolicy.ShouldIssueSafeMobilityRoute(false, true, false), Is.False);
			Assert.That(StealthAISpecialistPolicy.CanAdvanceReinforcement(true), Is.True);
			Assert.That(StealthAIThreatGeometry.RemainingHealthPriority(5000, 100, 1000), Is.EqualTo(50000));
			Assert.That(StealthAISpecialistPolicy.ShouldPreserveBusyReinforcement(true, false), Is.True);
		}

		[Test]
		public void StealthClearingPolicyUsesStrictKitingAndTwoToOneHysteresis()
		{
			Assert.That(StealthAISpecialistPolicy.CanKite(120, 100, 8, 5, 1, 120), Is.True);
			Assert.That(StealthAISpecialistPolicy.CanKite(119, 100, 8, 5, 1, 120), Is.False);
			Assert.That(StealthAISpecialistPolicy.CanKite(120, 100, 6, 5, 1, 120), Is.False);
			Assert.That(StealthAISpecialistPolicy.ShouldEnterMassClear(2, 200), Is.False);
			Assert.That(StealthAISpecialistPolicy.ShouldEnterMassClear(2.01, 200), Is.True);
			Assert.That(StealthAISpecialistPolicy.ShouldAbortMassClear(1.01, 100), Is.False);
			Assert.That(StealthAISpecialistPolicy.ShouldAbortMassClear(1, 100), Is.True);
			Assert.That(StealthAISpecialistPolicy.ShouldEnterAggressiveMass(5), Is.False);
			Assert.That(StealthAISpecialistPolicy.ShouldEnterAggressiveMass(5.01), Is.True);

			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(states, Does.Contain("owner.StealthAggressiveMass = " +
				"StealthAISpecialistPolicy.ShouldEnterAggressiveMass(overmatch)"),
				">5 enters aggressive Mass and <=5 downgrades to normal committed Mass.");
			Assert.That(states, Does.Contain("aggressiveMass ? \"AttackMove\" : \"Move\""));
			Assert.That(states, Does.Contain("aggressiveMass ? wanted : threatTarget"),
				"Aggressive Mass must route toward the selected high-value strategic destination.");
			Assert.That(states, Does.Contain("else if (!aggressiveMass && CanAttackTarget"),
				"Aggressive AttackMove must not divert into a targeted low-value scout hunt.");
			Assert.That(states, Does.Contain(
				"plan.StealthAggressiveMass && plan.StealthClearCenterCell != null"),
				"The latched high-value strategic destination must remain distinct from encountered threats.");
			Assert.That(states, Does.Contain("return !owner.StealthAggressiveMass"),
				"Aggressive invalid-target recalculation must continue into same-tick route submission.");
			Assert.That(states, Does.Contain("if (wasAggressiveMass && !owner.StealthAggressiveMass)"));
			Assert.That(states, Does.Contain("Leaving >5 immediately restores the full ordinary hierarchy"));
			var pendingBlue = states.IndexOf("if (pendingBlueOnly && !pendingBlueExplosion)",
				StringComparison.Ordinal);
			var massSafetyBypass = states.IndexOf(
				"if (!pendingBlueExplosion && owner.StealthClearMode == StealthClearMode.Mass)",
				StringComparison.Ordinal);
			Assert.That(pendingBlue, Is.GreaterThanOrEqualTo(0));
			Assert.That(pendingBlue, Is.LessThan(massSafetyBypass),
				"Pending Blue explosion escape must remain the mandatory override before Mass safety bypass.");
		}

		[Test]
		public void UndefendedTravelWindowIsPreferenceRatherThanEligibility()
		{
			Assert.That(StealthAISpecialistPolicy.IsWithinUndefendedTravelPreference(60000, 60), Is.True);
			Assert.That(StealthAISpecialistPolicy.IsWithinUndefendedTravelPreference(60001, 60), Is.False);
			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			var urgentUndefended = states.IndexOf("var urgentLocal = cadenceUrgent ? safePlans.Select",
				StringComparison.Ordinal);
			var urgentKite = states.IndexOf("urgentLocal = clearPlans.Where", urgentUndefended,
				StringComparison.Ordinal);
			var bridge = states.IndexOf("best = clearPlans.Where(p => p.StealthMode == StealthClearMode.CrushBridge",
				urgentKite, StringComparison.Ordinal);
			var shortSafe = states.IndexOf("best = (cadenceUrgent ? safePlans : preferredSafePlans).Where",
				bridge, StringComparison.Ordinal);
			var urgentSafe = states.IndexOf("best = safePlans.Where(p => FitsCadence", shortSafe,
				StringComparison.Ordinal);
			var kite = states.IndexOf("best = clearPlans.Where", shortSafe, StringComparison.Ordinal);
			var mass = states.IndexOf("best = clearPlans.Where", kite + 1, StringComparison.Ordinal);
			var farSafe = states.IndexOf("best = safePlans.OrderBy", mass, StringComparison.Ordinal);
			Assert.That(urgentUndefended, Is.GreaterThanOrEqualTo(0));
			Assert.That(urgentKite, Is.GreaterThan(urgentUndefended));
			Assert.That(bridge, Is.GreaterThan(urgentKite));
			Assert.That(shortSafe, Is.GreaterThan(bridge));
			Assert.That(urgentSafe, Is.GreaterThan(shortSafe));
			Assert.That(urgentSafe, Is.LessThan(kite));
			Assert.That(kite, Is.GreaterThan(shortSafe));
			Assert.That(mass, Is.GreaterThan(kite));
			Assert.That(farSafe, Is.GreaterThan(mass));
			Assert.That(states, Does.Contain("best = safePlans.Where(p => FitsCadence"),
				"Every bounded safe plan must become eligible only when comparable missions cannot finish.");
			Assert.That(states, Does.Contain("mobileArmedTarget && (formation.Count == 1 ||"));
			Assert.That(states, Does.Contain("owner.StealthProfile == \"stealth-tank\") ? TryStealthClearPlan("),
				"Every active STNK formation must share one legal Kite route and focus-fire target; CTNK stays singleton-only.");
			Assert.That(states, Does.Contain("StealthCrushLeader(owner, formationUnits, owner.TargetActor)"),
				"A multi-member squad must use one persistent crusher instead of congesting the exact target cell.");
			Assert.That(states, Does.Contain("outcome=safety-escape-replan"),
				"A locally-invalidated crush bridge must expose its bounded escape/replan outcome.");
		}

		[Test]
		public void CachedOutwardFrontierUsesSafeRouteCostAndRetainsIncumbentExtra()
		{
			const int Width = 5;
			const int Height = 3;
			var danger = new float[Width * Height];
			danger[1 * Width + 1] = StealthAISpecialistPolicy.HardRouteDangerThreshold;
			danger[1 * Width + 2] = StealthAISpecialistPolicy.HardRouteDangerThreshold;
			danger[1 * Width + 3] = StealthAISpecialistPolicy.HardRouteDangerThreshold;
			var targets = new[]
			{
				new CPos(4, 1),
				new CPos(0, 2),
				new CPos(2, 2),
				new CPos(4, 2),
			};

			var result = StealthAIThreatGeometry.SelectReachableTargetCells(
				danger, Width, Height, 0, 1, targets, 4, 2, requiredIndex: 0);

			Assert.That(result, Is.Not.Null);
			Assert.That(result.Targets.Select(target => target.TargetIndex), Is.EqualTo(new[] { 1, 2, 0 }),
				"The first two safe-cost cells are bounded normally and the incumbent remains an extra candidate.");
			Assert.That(result.Targets.Last().IsRequired, Is.True);
			Assert.That(result.Targets.Select(target => target.RouteCost), Is.Ordered);
			Assert.That(result.Targets.SelectMany(target => target.Route), Does.Not.Contain(new CPos(1, 1)));
			Assert.That(result.Targets.SelectMany(target => target.Route), Does.Not.Contain(new CPos(2, 1)));
			Assert.That(result.Targets.SelectMany(target => target.Route), Does.Not.Contain(new CPos(3, 1)));
			Assert.That(result.ExpandedCells, Is.LessThanOrEqualTo(Width * Height),
				"One frontier may expand each cached coarse cell at most once.");
		}

		[Test]
		public void StnkFrontierIsDefaultEightBoundedAndDoesNotRetuneChemicalSquads()
		{
			var definition = Source(
				"OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/StealthSquadDefinition.cs");
			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(definition, Does.Contain("public readonly int OutwardTargetCellLimit = 8;"));
			Assert.That(definition, Does.Contain("OutwardTargetCellLimit < 5 || OutwardTargetCellLimit > 10"));
			Assert.That(states, Does.Contain("if (owner.StealthProfile == \"stealth-tank\")"));
			Assert.That(states, Does.Contain("scope=cached-6x6 frontier-world-scans=0 target-cell-a-star=0"));
			Assert.That(states, Does.Contain("else\n\t\t\t{\n\t\t\t\tselectedIndices = StealthAIThreatGeometry.SelectTargetCandidates("),
				"Non-STNK specialist profiles must retain their previous candidate selection path.");
			Assert.That(states, Does.Contain("shared-route=True focus-fire=True"));
		}

		[Test]
		public void CadenceFinishMarginSelectsFinishableServiceWithoutCreatingAnExemption()
		{
			var margin = StealthAISpecialistPolicy.KillCadenceFinishMarginTicks(125, 150);
			Assert.That(margin, Is.EqualTo(275));
			Assert.That(StealthAISpecialistPolicy.CanFinishWithinKillCadence(
				10000, 20, 1000, 2250, margin), Is.True);
			Assert.That(StealthAISpecialistPolicy.CanFinishWithinKillCadence(
				20000, 20, 1000, 2250, margin), Is.False);
			Assert.That(StealthAISpecialistPolicy.IsKillCadenceUrgent(474, 2250, margin), Is.False);
			Assert.That(StealthAISpecialistPolicy.IsKillCadenceUrgent(475, 2250, margin), Is.True,
				"Finish-first service must reserve roughly three attempts for regroup and target churn.");
			Assert.That(StealthAISpecialistPolicy.ShouldReplaceNonFinishableMission(
				20000, 10000, 20, 1000, 2250, margin), Is.True);
			Assert.That(StealthAISpecialistPolicy.ShouldReplaceNonFinishableMission(
				10000, 5000, 20, 400, 2250, margin), Is.False,
				"A progressing mission that still fits its honest window keeps Air-consistent ownership.");
			Assert.That(StealthAISpecialistPolicy.ShouldReplaceNonFinishableMission(
				10000, 5000, 20, 1000, 2250, margin), Is.True,
				"A mobile incumbent may yield to any strictly shorter cached mission under urgency.");
			Assert.That(StealthAISpecialistPolicy.ShouldReplaceNonFinishableMission(
				10000, 5000, 20, 1000, 2250, margin, false, true), Is.False,
				"A stationary finish must not yield to marginal mobile target churn.");
			Assert.That(StealthAISpecialistPolicy.ShouldReplaceNonFinishableMission(
				10000, 4000, 20, 1000, 2250, margin, false, true), Is.True,
				"A stationary finish may yield when the mobile challenger saves the full retry margin.");
			Assert.That(StealthAISpecialistPolicy.ShouldReplaceNonFinishableMission(
				long.MaxValue, 20000, 20, 2000, 2250, margin), Is.True,
				"When every service is far, the bounded shortest reachable mission must still proceed.");
			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(states, Does.Contain("plan.ServiceMilliseconds"));
			Assert.That(states, Does.Contain("owner.StealthKillCadenceAge"));
			Assert.That(states, Does.Contain("best = safePlans.OrderBy(p => p.ServiceMs)"));
			Assert.That(states, Does.Not.Contain("FindClosestAttackableEnemy(owner)"),
				"Cadence urgency must not add a whole-world fallback scan.");
		}

		[Test]
		public void OwnedMissionRouteIdentityIncludesClearMode()
		{
			Assert.That(StealthAISpecialistPolicy.ShouldPreserveOwnedMissionRoute(
				true, true, true, true, StealthClearMode.None, StealthClearMode.None), Is.True);
			Assert.That(StealthAISpecialistPolicy.ShouldPreserveOwnedMissionRoute(
				true, true, true, true, StealthClearMode.Crush, StealthClearMode.Crush), Is.True);
			Assert.That(StealthAISpecialistPolicy.ShouldPreserveOwnedMissionRoute(
				true, true, true, true, StealthClearMode.None, StealthClearMode.Crush), Is.False,
				"An ordinary Move+Attack queue must not masquerade as tracked Crush ownership.");
			Assert.That(StealthAISpecialistPolicy.ShouldPreserveOwnedMissionRoute(
				true, true, true, true, StealthClearMode.Crush, StealthClearMode.Kite), Is.False);
			Assert.That(StealthAISpecialistPolicy.ShouldPreserveOwnedMissionRoute(
				true, false, true, true, StealthClearMode.Crush, StealthClearMode.Crush), Is.False);
		}

		[Test]
		public void MovingMtnkKiteNeedsRepeatedLocalConfirmationToReplaceWallFallback()
		{
			Assert.That(StealthAISpecialistPolicy.ShouldReplaceLowValueWallWithConfirmedMovingMtnkKite(
				true, true, true, StealthClearMode.Kite, 1, 15000, 20), Is.False,
				"One sighting must retain the useful wall route instead of creating target churn.");
			Assert.That(StealthAISpecialistPolicy.ShouldReplaceLowValueWallWithConfirmedMovingMtnkKite(
				true, true, true, StealthClearMode.Kite, 2, 15000, 20), Is.True,
				"The same legal local moving MTNK Kite may replace a wall after two reviews.");
			Assert.That(StealthAISpecialistPolicy.ShouldReplaceLowValueWallWithConfirmedMovingMtnkKite(
				true, false, true, StealthClearMode.Kite, 2, 15000, 20), Is.False,
				"Ordinary buildings retain their existing arbitration policy.");
			Assert.That(StealthAISpecialistPolicy.ShouldReplaceLowValueWallWithConfirmedMovingMtnkKite(
				true, true, false, StealthClearMode.Kite, 2, 15000, 20), Is.False,
				"CTNK and other dynamic actors are not admitted by the MTNK-only correction.");
			Assert.That(StealthAISpecialistPolicy.ShouldReplaceLowValueWallWithConfirmedMovingMtnkKite(
				true, true, true, StealthClearMode.Mass, 2, 15000, 20), Is.False,
				"The correction cannot bypass existing Mass or safety policy.");
			Assert.That(StealthAISpecialistPolicy.ShouldReplaceLowValueWallWithConfirmedMovingMtnkKite(
				true, true, true, StealthClearMode.Kite, 2, 20001, 20), Is.False,
				"Only the existing bounded local service window is eligible.");

			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(states, Does.Contain("confirmations={6}/2"));
			Assert.That(states, Does.Contain("owner.Type == SquadType.Stealth && !repeatedMovingMtnkKite"),
				"The established generic urgency path must not bypass the repeated-candidate gate.");
			Assert.That(states, Does.Contain("scope=cached-local world-scans=0"));
		}

		[Test]
		public void OrdinaryCrushUsesLocalServiceBoundWithoutResettingDistantOrdinaryRoute()
		{
			Assert.That(StealthAISpecialistPolicy.ShouldUseBoundedCrush(15000, 20), Is.True,
				"A nearby ordinary Crush remains eligible.");
			Assert.That(StealthAISpecialistPolicy.ShouldUseBoundedCrush(25000, 20), Is.False,
				"A distant ordinary Crush must fall through to the existing ordinary safe plan.");
			Assert.That(StealthAISpecialistPolicy.ShouldPreserveOwnedMissionRoute(
				true, true, true, true, StealthClearMode.None, StealthClearMode.None), Is.True,
				"The same distant actor's ordinary route remains owned when Crush is rejected.");
			Assert.That(StealthAISpecialistPolicy.ShouldPreserveOwnedMissionRoute(
				true, true, true, true, StealthClearMode.None, StealthClearMode.Crush), Is.False,
				"A genuinely local Crush transition still replaces the ordinary route by mode identity.");
		}

		[Test]
		public void QueuedCrushRefreshesOnlyForMaterialMovementBeforeTrackingOwnsMotion()
		{
			Assert.That(StealthAISpecialistPolicy.ShouldRefreshQueuedCrushRoute(
				true, true, false, true), Is.True,
				"A moved owned target invalidates the static route while tracked Crush is only queued.");
			Assert.That(StealthAISpecialistPolicy.ShouldRefreshQueuedCrushRoute(
				true, true, false, false), Is.False,
				"An unchanged target cell must preserve the useful queued route without churn.");
			Assert.That(StealthAISpecialistPolicy.ShouldRefreshQueuedCrushRoute(
				true, true, true, true), Is.False,
				"Once actor tracking controls current motion the engine follows the target without reissue.");
			Assert.That(StealthAISpecialistPolicy.ShouldRefreshQueuedCrushRoute(
				false, true, false, true), Is.False,
				"CrushBridge and other modes preserve their existing routing semantics.");

			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(states, Does.Contain("var crushCache = CachedStealthInfluence(owner, leader);"),
				"Live interception must reuse the bounded cache instead of scanning the world.");
			Assert.That(states, Does.Contain("tracking-current={12}"));
		}

		[Test]
		public void CadenceUrgencyPrefersOnlySafeCachedLocalQuickClears()
		{
			int Rank(StealthCadenceQuickClearMode mode, bool urgent = true, bool local = true,
				bool owned = true, bool safe = true, bool route = true, bool finishable = true,
				bool nearby = true, bool undefended = false, bool kite = false, bool stnk = true)
			{
				return StealthAISpecialistPolicy.CadenceUrgentLocalQuickClearRank(stnk, urgent,
					local, owned, safe, route, finishable, nearby, mode, undefended, kite);
			}

			Assert.That(Rank(StealthCadenceQuickClearMode.UndefendedValue, undefended: true), Is.Zero,
				"A nearby safe undefended value target is the first urgent service tier.");
			Assert.That(Rank(StealthCadenceQuickClearMode.Kite, kite: true), Is.EqualTo(1),
				"A nearby legal Kite is the second tier that can unlock valuable targets.");
			Assert.That(Rank(StealthCadenceQuickClearMode.UndefendedValue,
				safe: false, undefended: true), Is.EqualTo(int.MaxValue));
			Assert.That(Rank(StealthCadenceQuickClearMode.UndefendedValue,
				nearby: false, undefended: true), Is.EqualTo(int.MaxValue),
				"A cached mission outside the 20-second service window is not a local quick clear.");
			Assert.That(Rank(StealthCadenceQuickClearMode.Kite, owned: false, kite: true),
				Is.EqualTo(int.MaxValue), "A locally cached target owned by another squad remains rejected.");
			Assert.That(Rank(StealthCadenceQuickClearMode.Kite, route: false, kite: true),
				Is.EqualTo(int.MaxValue));
			Assert.That(Rank(StealthCadenceQuickClearMode.Kite, finishable: false, kite: true),
				Is.EqualTo(int.MaxValue));
			Assert.That(Rank(StealthCadenceQuickClearMode.Kite, local: false, kite: true),
				Is.EqualTo(int.MaxValue), "The preference must not expand beyond the cached local package.");
			Assert.That(Rank(StealthCadenceQuickClearMode.UndefendedValue,
				urgent: false, undefended: true),
				Is.EqualTo(int.MaxValue), "Urgency-off selection retains the existing strategic ordering.");
			Assert.That(Rank(StealthCadenceQuickClearMode.UndefendedValue,
				undefended: true, stnk: false),
				Is.EqualTo(int.MaxValue), "CTNK and other profiles remain unchanged.");

			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			var definition = Source("OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/StealthSquadDefinition.cs");
			Assert.That(definition, Does.Contain("MaximumUndefendedTargetTravelSeconds = 20"));
			Assert.That(states, Does.Contain("is a pure ordering step and performs no additional scan or path search"));
			Assert.That(states, Does.Contain("best = safePlans.OrderBy(p => p.ServiceMs)"),
				"No eligible local quick clear must preserve the bounded shortest-safe fallback.");
			Assert.That(states.IndexOf("if (definition.EnableKiting && retreatCell != null)",
				StringComparison.Ordinal), Is.LessThan(states.IndexOf("ShouldEnterMassClear(",
				StringComparison.Ordinal)), "A nearby legal Kite must be evaluated before crossover Mass.");
			Assert.That(states, Does.Contain("Stealth crush [{0}] rejected distant pursuit:"),
				"Ordinary Crush must share the bounded local service limit used by Kite and CrushBridge.");
		}

		[Test]
		public void CachedLocalKiteOrderingIsDistanceFirstWhileMassRemainsThreatFirst()
		{
			var nearLowThreat = StealthAISpecialistPolicy.CachedLocalKiteOrderKey(25, 1, 1, 9);
			var farHighThreat = StealthAISpecialistPolicy.CachedLocalKiteOrderKey(36, 100, 100, 1);
			Assert.That(nearLowThreat.CompareTo(farHighThreat), Is.LessThan(0),
				"A farther high-threat/high-value defender must not displace the closest eligible Kite target.");

			var tiedLowThreat = StealthAISpecialistPolicy.CachedLocalKiteOrderKey(25, 1, 100, 1);
			var tiedHighThreat = StealthAISpecialistPolicy.CachedLocalKiteOrderKey(25, 2, 1, 9);
			Assert.That(tiedHighThreat.CompareTo(tiedLowThreat), Is.LessThan(0),
				"Cached threat is a deterministic tie-breaker only after distance.");
			var tiedThreatLowValue = StealthAISpecialistPolicy.CachedLocalKiteOrderKey(25, 2, 1, 1);
			var tiedThreatHighValue = StealthAISpecialistPolicy.CachedLocalKiteOrderKey(25, 2, 2, 9);
			Assert.That(tiedThreatHighValue.CompareTo(tiedThreatLowValue), Is.LessThan(0),
				"Cached strategic value breaks only distance-and-threat ties.");

			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(states, Does.Contain("a.CenterPosition - owner.AirFormationCenter"));
			Assert.That(states, Does.Contain("var threatTarget = HighestThreatActor(owner, formation, package)"),
				"Existing Mass semantics must remain highest-threat-first.");
			Assert.That(states, Does.Contain("LiveKiteTargetAtCurrentSafeCell"));
			Assert.That(states, Does.Contain("targetHP < owner.AirTargetLastHP"),
				"A completed shot must repeat the bounded live nearest-enemy selection.");
			Assert.That(states, Does.Contain("livePlan.Actor != owner.TargetActor"),
				"An unchanged nearest target must preserve its destination and orders.");
			var liveLoop = states.IndexOf("static AirTargetPlan LiveKiteTargetAtCurrentSafeCell",
				StringComparison.Ordinal);
			Assert.That(liveLoop, Is.GreaterThanOrEqualTo(0));
			var liveLoopEnd = states.IndexOf("protected static bool ContinueOrAbortMassClear",
				liveLoop, StringComparison.Ordinal);
			Assert.That(liveLoopEnd, Is.GreaterThan(liveLoop));
			var liveLoopSource = states.Substring(liveLoop, liveLoopEnd - liveLoop);
			Assert.That(liveLoopSource, Does.Not.Contain("FindActorsInCircle"),
				"The live Kite loop must never broaden into a world or radius scan.");
			Assert.That(liveLoopSource, Does.Not.Contain("StealthRouteToCell("),
				"The after-shot live scan must not add A* or path search.");
		}

		[Test]
		public void OwnedStealthMissionDispatchesAtTheAttackActivationBoundary()
		{
			Assert.That(StealthAISpecialistPolicy.ShouldDispatchOwnedMissionImmediately(
				true, false, 5, false), Is.False,
				"A route without a valid owned mission must not dispatch.");
			Assert.That(StealthAISpecialistPolicy.ShouldDispatchOwnedMissionImmediately(
				true, true, 5, false), Is.True,
				"The first valid bounded STNK mission must dispatch at Attack activation.");
			Assert.That(StealthAISpecialistPolicy.ShouldDispatchOwnedMissionImmediately(
				true, true, 5, true), Is.False,
				"An already submitted route must retain its existing ownership without churn.");
			Assert.That(StealthAISpecialistPolicy.ShouldDispatchOwnedMissionImmediately(
				true, true, 0, false), Is.False,
				"Mission replacement without a cached route remains on the ordinary Attack path.");
			Assert.That(StealthAISpecialistPolicy.ShouldDispatchOwnedMissionImmediately(
				false, true, 5, false), Is.False,
				"The correction must not change other Air-style profiles.");

			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(states, Does.Contain("owner.AirRoute.Count, owner.AirRouteQueued))"));
			Assert.That(states, Does.Not.Contain("StealthKillCadenceAge = 0"),
				"Attack activation must not reset or game a continuing squad clock.");
		}

		[Test]
		public void StaleMobileSnapshotReservesOneBoundedReviewWindow()
		{
			const int timestep = 20;
			const int ageTicks = 1000;
			const int maximumTicks = 2250;
			var margin = StealthAISpecialistPolicy.KillCadenceFinishMarginTicks(125, 150);
			const long staleMobileSnapshot = 15000;
			const long finishableStationaryAlternative = 18000;

			Assert.That(StealthAISpecialistPolicy.CanFinishWithinKillCadence(
				staleMobileSnapshot, timestep, ageTicks, maximumTicks, margin), Is.True,
				"The stale snapshot intentionally appears finishable before movement/replan cost is reserved.");
			var reservedMobileService = StealthAISpecialistPolicy.CachedMobileServiceMilliseconds(
				staleMobileSnapshot, timestep, margin, true);
			Assert.That(reservedMobileService, Is.EqualTo(20500));
			Assert.That(StealthAISpecialistPolicy.CanFinishWithinKillCadence(
				reservedMobileService, timestep, ageTicks, maximumTicks, margin), Is.False);
			Assert.That(StealthAISpecialistPolicy.CachedMobileServiceMilliseconds(
				finishableStationaryAlternative, timestep, margin, false),
				Is.EqualTo(finishableStationaryAlternative));
			Assert.That(StealthAISpecialistPolicy.ShouldReplaceNonFinishableMission(
				reservedMobileService, finishableStationaryAlternative, timestep, ageTicks,
				maximumTicks, margin, true, false), Is.True,
				"A genuinely finishable cached alternative must replace the stale mobile mission.");
		}
	}
}
