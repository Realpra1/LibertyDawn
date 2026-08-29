#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
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

		static string StealthStateSources(params string[] names) => string.Join("\n", names.Select(name => Source(
			$"OpenRA.Mods.Common/Traits/BotModules/Squads/States/{name}.cs")));

		static T InvokeInternal<T>(Type type, string method, params object[] arguments)
		{
			var target = type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(target, Is.Not.Null, $"Missing internal method {type.Name}.{method}.");
			return (T)target.Invoke(null, arguments);
		}

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
			var states = StealthStateSources("StealthAIStates", "StealthAIIdleState");
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
		public void StealthBehaviorStagesAndPlanningRecordsAreSeparateModules()
		{
			var baseAndAttack = StealthStateSources("StealthAIStates");
			var idle = StealthStateSources("StealthAIIdleState");
			var flee = StealthStateSources("StealthAIFleeState");
			var plans = StealthStateSources("StealthAIPlans");

			Assert.That(baseAndAttack, Does.Not.Contain("class StealthAIIdleState"));
			Assert.That(baseAndAttack, Does.Not.Contain("class StealthAIFleeState"));
			Assert.That(idle, Does.Contain("class StealthAIIdleState : StealthAIStateBase, IState"));
			Assert.That(flee, Does.Contain("class StealthAIFleeState : StealthAIStateBase, IState"));
			Assert.That(plans, Does.Contain("sealed class StealthInfluenceCache"));
			Assert.That(plans, Does.Contain("sealed class AirTargetPlan"));
			Assert.That(baseAndAttack, Does.Not.Contain("sealed class StealthInfluenceCache"));
			Assert.That(baseAndAttack, Does.Not.Contain("sealed class AirTargetPlan"));
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
			Assert.That(states, Does.Contain("OrderByDescending(p => Separation(p.Plan)).ThenBy(p => p.ServiceMs)"),
				"Comparable-value STNK targets should prefer multi-angle separation, then bounded service time.");
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
			var watchdog = Source("OpenRA.Mods.Common/Traits/BotOwnedStationaryWatchdog.cs");
			var watchdogGuard = watchdog.IndexOf(
				"if (!Game.Settings.Debug.BotDebug || !self.Owner.IsBot)", StringComparison.Ordinal);
			Assert.That(watchdogGuard, Is.GreaterThanOrEqualTo(0));
			Assert.That(watchdogGuard, Is.LessThan(watchdog.IndexOf(
				"var currentHealth", watchdogGuard, StringComparison.Ordinal)),
				"Debug-disabled games must return before firing, healing, or movement diagnostics.");
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
			Assert.That(StealthAISpecialistPolicy.IsEngagementThreat(false, true, true), Is.True,
				"planned decloak under canonical armed coverage must trigger local safety");
			Assert.That(StealthAISpecialistPolicy.IsHardPlannedDecloakThreat(true, 0.999), Is.False,
				"low canonical threat must not make a planned shot abandon its approved engagement");
			Assert.That(StealthAISpecialistPolicy.IsHardPlannedDecloakThreat(true, 1), Is.True,
				"hard canonical threat at the actual decloak distance must remain responsive");
			Assert.That(StealthAISpecialistPolicy.IsHardPlannedDecloakThreat(false, 100), Is.False,
				"canonical magnitude alone is not planned-decloak context");
			Assert.That(StealthAISpecialistPolicy.StrategicTargetReviewIntervalTicks(40, 25), Is.EqualTo(125));
			Assert.That(StealthAISpecialistPolicy.StrategicTargetReviewIntervalTicks(20, 125), Is.EqualTo(250));
			Assert.That(states, Does.Contain("TryGetDefenderThreat"),
				"stealth route and local safety must consume the standard calculator");
			var calculator = Source("OpenRA.Mods.Common/Traits/BotModules/GeneralizedCombatThreat.cs");
			Assert.That(calculator, Does.Contain(
				"engagementDistanceCells == null ? pair.DefenderThreatInAttackerEquivalents"),
				"the optional distance must leave the canonical default result unchanged");
			Assert.That(states, Does.Contain("owner.StealthEscapePreserveEngagement"),
				"a brief safety reposition must retain an approved live actor/cell engagement");
			Assert.That(states, Does.Contain("strategic-event-review={1}"),
				"route arrival must remain an event-driven exception to the five-second ordinary review floor");
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
			Assert.That(manager, Does.Contain("status=timed-out cadence-age={6}/{7}"),
				"Debug acceptance runs must account for an expired STNK squad without changing cadence state.");
			Assert.That(manager, Does.Contain("e.Attacker.Info.Name == \"stnk\""));
			Assert.That(manager, Does.Contain("StealthKillCadenceGeneration.AttributeKill(World.WorldTick)"),
				"Only an attributed STNK kill may reset its current squad's running cadence window.");
			Assert.That(manager, Does.Not.Contain("internal int StealthKillCadenceAge"),
				"Cadence state must not be owner-wide.");
			Assert.That(squad, Does.Contain("internal StealthKillCadenceGeneration StealthKillCadenceGeneration"),
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
			Assert.That(states, Does.Contain("members=[{12}]"),
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
			Assert.That(states, Does.Contain("activity-current={28}:{29}:cancel={30}"),
				"Failure flush must retain exact activity and cancellation state.");
			Assert.That(states, Does.Contain("tick - previous.LastReportTick >= 75"));
			Assert.That(states, Does.Contain("maximumTicks - 600"));
			Assert.That(states, Does.Contain("RecordStealthDebugLifecycle(owner, false);"));
			Assert.That(states, Does.Contain("RecordStealthDebugLifecycle(owner, true);"));
			Assert.That(states, Does.Contain("FlushStealthDebugLifecycle(owner, stnks);"));
			Assert.That(manager, Does.Contain("Stealth squad lifecycle [{0}] empty timeout:"),
				"A genuinely empty squad must expire instead of living indefinitely under a no-actor exemption.");
			Assert.That(manager, Does.Contain("lifecycle={6}"),
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
		public void RealSquadCadenceGenerationsRemainIndependentAcrossSlotReuse()
		{
			var first = new StealthKillCadenceGeneration(41, 1000);
			first.Observe(1200, true);
			Assert.That(first.AttributeKill(1300), Is.True);
			first.Observe(1400, true);

			var remake = new StealthKillCadenceGeneration(42, 1400);
			remake.Observe(1500, true);

			Assert.Multiple(() =>
			{
				Assert.That(first.GenerationId, Is.EqualTo(41));
				Assert.That(first.AttributedKills, Is.EqualTo(1));
				Assert.That(first.CadenceAge, Is.EqualTo(100));
				Assert.That(remake.GenerationId, Is.EqualTo(42));
				Assert.That(remake.AttributedKills, Is.Zero);
				Assert.That(remake.CadenceAge, Is.EqualTo(100));
				Assert.That(remake.WindowStartTick, Is.EqualTo(1400));
			});
		}

		[Test]
		public void CadenceAgeUsesActiveGenerationTicksAndMismatchFailureIsPermanent()
		{
			var generation = new StealthKillCadenceGeneration(7, 500);
			generation.Observe(600, false);
			generation.Observe(700, true);
			Assert.That(generation.CadenceAge, Is.EqualTo(100),
				"An empty interval must not be charged retroactively when membership becomes active.");
			Assert.That(generation.GenerationStartTick, Is.EqualTo(500));

			var corrupt = StealthKillCadenceGeneration.Restore(8, 1000, 1000, 1100,
				101, 0, false, false);
			Assert.That(corrupt.MismatchFailed, Is.True);
			Assert.That(corrupt.AttributeKill(1101), Is.False,
				"A later kill cannot erase or retroactively repair a generation-age mismatch.");
			Assert.That(corrupt.MismatchFailed, Is.True);
			Assert.That(corrupt.AttributedKills, Is.Zero);
		}

		[Test]
		public void KillTimeMembershipSelectsOnlyTheCurrentGeneration()
		{
			var beforeTransfer = new[]
			{
				new KeyValuePair<uint, int>(100, 1),
				new KeyValuePair<uint, int>(200, 2)
			};
			var afterTransfer = new[]
			{
				new KeyValuePair<uint, int>(100, 2),
				new KeyValuePair<uint, int>(200, 2)
			};

			Assert.That(StealthAISpecialistPolicy.KillTimeOwnerGeneration(100, beforeTransfer), Is.EqualTo(1));
			Assert.That(StealthAISpecialistPolicy.KillTimeOwnerGeneration(100, afterTransfer), Is.EqualTo(2));
			Assert.That(StealthAISpecialistPolicy.KillTimeOwnerGeneration(300, afterTransfer), Is.Zero);
		}

		[Test]
		public void CadenceGenerationStateRoundTripsWithoutAWindowReset()
		{
			var original = new StealthKillCadenceGeneration(19, 2000);
			original.Observe(2100, true);
			original.AttributeKill(2125);
			original.Observe(2200, true);
			var restored = StealthKillCadenceGeneration.Restore(original.GenerationId,
				original.GenerationStartTick, original.WindowStartTick, original.LastObservedTick,
				original.CadenceAge, original.AttributedKills, original.CadenceFailed,
				original.MismatchFailed);

			Assert.Multiple(() =>
			{
				Assert.That(restored.GenerationId, Is.EqualTo(19));
				Assert.That(restored.GenerationStartTick, Is.EqualTo(2000));
				Assert.That(restored.WindowStartTick, Is.EqualTo(2125));
				Assert.That(restored.LastObservedTick, Is.EqualTo(2200));
				Assert.That(restored.CadenceAge, Is.EqualTo(75));
				Assert.That(restored.AttributedKills, Is.EqualTo(1));
			});

			var squad = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/Squad.cs");
			var manager = Source("OpenRA.Mods.Common/Traits/BotModules/SquadManagerBotModule.cs");
			Assert.That(squad, Does.Contain("StealthCadenceGenerationStartTick"));
			Assert.That(squad, Does.Contain("StealthCadenceMismatchFailed"));
			Assert.That(manager, Does.Contain("NextStealthSquadGenerationId"));
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
			var bridge = states.IndexOf("best = clearPlans.Where(p => p.StealthMode == StealthClearMode.CrushBridge",
				StringComparison.Ordinal);
			var shortSafe = states.IndexOf("best = safePlans.Where",
				bridge, StringComparison.Ordinal);
			var safe = states.IndexOf("best = safePlans\n", shortSafe + 1,
				StringComparison.Ordinal);
			var kite = states.IndexOf("best = clearPlans.Where", safe, StringComparison.Ordinal);
			var mass = states.IndexOf("best = clearPlans.Where", kite + 1, StringComparison.Ordinal);
			var farSafe = states.IndexOf("best = safePlans.OrderBy", mass, StringComparison.Ordinal);
			Assert.That(bridge, Is.GreaterThanOrEqualTo(0));
			Assert.That(shortSafe, Is.GreaterThan(bridge));
			Assert.That(safe, Is.GreaterThan(shortSafe));
			Assert.That(kite, Is.GreaterThan(shortSafe));
			Assert.That(mass, Is.GreaterThan(kite));
			Assert.That(farSafe, Is.GreaterThan(mass));
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
		public void StnkFrontierIsDefaultTenBoundedAndDoesNotRetuneChemicalSquads()
		{
			var definition = Source(
				"OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/StealthSquadDefinition.cs");
			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(definition, Does.Contain("public readonly int OutwardTargetCellLimit = 10;"));
			Assert.That(definition, Does.Contain("OutwardTargetCellLimit < 5 || OutwardTargetCellLimit > 10"));
			Assert.That(states, Does.Contain("if (owner.StealthProfile == \"stealth-tank\")"));
			Assert.That(states, Does.Contain("scope=cached-6x6 frontier-world-scans=0 target-cell-a-star=0"));
			Assert.That(states, Does.Contain("else\n\t\t\t{\n\t\t\t\tselectedIndices = StealthAIThreatGeometry.SelectTargetCandidates("),
				"Non-STNK specialist profiles must retain their previous candidate selection path.");
			Assert.That(states, Does.Contain("shared-route=True focus-fire=True"));
		}

		[Test]
		public void CadenceWatchdogIsDiagnosticOnlyForTargetPlanning()
		{
			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			var planner = states.Substring(states.IndexOf("static AirTargetPlan FindBestStealthTarget",
				StringComparison.Ordinal));
			planner = planner.Substring(0, planner.IndexOf("// END CNC96A GROUND EXTENSION",
				StringComparison.Ordinal));
			Assert.That(planner, Does.Not.Contain("StealthKillCadenceAge"));
			Assert.That(planner, Does.Not.Contain("IsKillCadenceUrgent"));
			Assert.That(planner, Does.Not.Contain("CanFinishWithinKillCadence"));
			var attack = states.Substring(states.IndexOf("class StealthAIAttackState",
				StringComparison.Ordinal));
			Assert.That(attack, Does.Not.Contain("ShouldReplaceNonFinishableMission"),
				"Watchdog cadence must not replace or retain a gameplay target.");
			Assert.That(states, Does.Not.Contain("FindClosestAttackableEnemy(owner)"),
				"The bounded fallback must not add a whole-world scan.");
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
			Assert.That(states, Does.Not.Contain("ShouldReplaceNonFinishableMission("),
				"Diagnostic cadence must not bypass the repeated-candidate gate.");
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
				true, true, false, true, true), Is.True,
				"A moved owned target invalidates the static route while tracked Crush is only queued.");
			Assert.That(StealthAISpecialistPolicy.ShouldRefreshQueuedCrushRoute(
				true, true, false, true, false), Is.False,
				"An unchanged target cell must preserve the useful queued route without churn.");
			Assert.That(StealthAISpecialistPolicy.ShouldRefreshQueuedCrushRoute(
				true, true, true, true, true), Is.False,
				"Once actor tracking controls current motion the engine follows the target without reissue.");
			Assert.That(StealthAISpecialistPolicy.ShouldRefreshQueuedCrushRoute(
				false, true, false, true, true), Is.False,
				"CrushBridge and other modes preserve their existing routing semantics.");

			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(states, Does.Contain(
				"var crushCache = leader == null ? null : CachedStealthInfluence(owner, leader);"),
				"Live interception must reuse the bounded cache instead of scanning the world.");
			Assert.That(states, Does.Contain("tracking-current={12}"));
		}

		[Test]
		public void PostPr137StealthAcceptanceGatesAreLiteralAndProtected()
		{
			var yaml = Source("mods/cnc/rules/ai.yaml");
			Assert.That(yaml.Split("stealth-tank:").Length - 1, Is.EqualTo(10));
			Assert.That(yaml.Split('\n').Count(line => line.Trim() == "vice: -1"), Is.EqualTo(20),
				"Every harassment and attack STNK profile must reject both Visceroid actor types.");
			Assert.That(yaml.Split('\n').Count(line => line.Trim() == "pvice: -1"), Is.EqualTo(20));

			Assert.That(StealthAISpecialistPolicy.MinimumStrategicCellValue,
				Is.EqualTo(5000L * 1100L));
			Assert.That(StealthAISpecialistPolicy.StrategicTargetValue(5000, 1100),
				Is.EqualTo(StealthAISpecialistPolicy.MinimumStrategicCellValue));
			Assert.That(StealthAISpecialistPolicy.MeetsMinimumStrategicCellValue(
				5000L * 1100L - 1), Is.False);
			Assert.That(StealthAISpecialistPolicy.MeetsMinimumStrategicCellValue(
				5000L * 1100L), Is.True);

			Assert.That(StealthAISpecialistPolicy.WeightedRouteDistanceCells(4.5f, 6), Is.EqualTo(27));
			Assert.That(StealthAISpecialistPolicy.CanOutrangeUndetectingTarget(7, 0, 8), Is.True,
				"An STNK must use its exact one-cell Obelisk range advantage.");
			Assert.That(StealthAISpecialistPolicy.CanOutrangeUndetectingTarget(7, 1, 8), Is.False);
			Assert.That(StealthAISpecialistPolicy.IsObeliskAttributedStealthTankDeath("stnk", "obli"),
				Is.True);

			Assert.That(StealthAISpecialistPolicy.NextKillCadenceAge(2249, 1, false, false),
				Is.EqualTo(2250));
			Assert.That(StealthAISpecialistPolicy.KillCadenceFailed(2250, 2250), Is.True);
			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(states, Does.Contain("var exempt = noStnk;"),
				"Firing, repair, planning, and target absence cannot pause an active squad kill clock.");
			Assert.That(states, Does.Contain("Stealth kill watchdog [stealth-tank] squad failure:"));
			Assert.That(states, Does.Contain("cachedFrontierRouteCosts[target.TargetIndex] = target.RouteCost"));
			Assert.That(states, Does.Contain("Stealth crossover approval"));
			Assert.That(states, Does.Contain("Stealth decloak approval"));
			Assert.That(states, Does.Contain("owner.StealthCrushTargetCell.Value != targetCell"));
			Assert.That(states, Does.Contain("route.Add(targetCell)"));

			var vehicles = Source("mods/cnc/rules/vehicles.yaml");
			Assert.That(vehicles, Does.Contain("BotOwnedStationaryWatchdog:"));
			Assert.That(vehicles, Does.Contain("MaximumStationaryMilliseconds: 30000"));
			var manager = Source("OpenRA.Mods.Common/Traits/BotModules/SquadManagerBotModule.cs");
			Assert.That(manager, Does.Contain("Stealth Obelisk death watchdog failure"));
			var stationary = Source("OpenRA.Mods.Common/Traits/BotOwnedStationaryWatchdog.cs");
			Assert.That(stationary, Does.Contain("stationaryFailureReported"));
			Assert.That(stationary, Does.Not.Contain("throw new InvalidOperationException"));
			Assert.That(states, Does.Not.Contain("throw new InvalidOperationException"));
			Assert.That(manager, Does.Not.Contain("throw new InvalidOperationException"),
				"Premade stealth watchdogs are diagnostic-only and must never terminate gameplay.");
		}

		[Test]
		public void StealthLifecycleUsesTenCellFrontierAndMultiAngleSeparation()
		{
			var definition = new StealthSquadDefinition(new MiniYaml("", new List<MiniYamlNode>()));
			Assert.That(definition.OutwardTargetCellLimit, Is.EqualTo(10));

			var occupied = new[] { new CPos(4, 4), new CPos(10, 10) };
			Assert.That(StealthAIThreatGeometry.MinimumCellSeparationSquared(
				new CPos(6, 4), occupied), Is.EqualTo(4));
			Assert.That(StealthAIThreatGeometry.MinimumCellSeparationSquared(
				new CPos(0, 0), occupied), Is.EqualTo(32));
			Assert.That(StealthAIThreatGeometry.MinimumCellSeparationSquared(
				new CPos(0, 0), Array.Empty<CPos>()), Is.EqualTo(long.MaxValue));

			var states = StealthStateSources("StealthAIStates", "StealthAIIdleState");
			Assert.That(states, Does.Contain("OrderByDescending(p => Separation(p.Plan))"),
				"Surviving opportunities must prefer the cell least close to another STNK squad target.");
			Assert.That(states, Does.Contain("owner.StealthLastFrontierTargetCells >= 10"));
			Assert.That(states, Does.Contain("BeginStealthEnemyApproach(owner)"),
				"A short frontier with no viable mission must take one bounded safe step toward enemies.");
		}

		[Test]
		public void StealthTargetCellsFilterStrategicValueBeforeThreat()
		{
			Assert.That(InvokeInternal<long>(typeof(StealthAISpecialistPolicy),
				"StrategicTargetValueByRemainingHealth", 5000, 1000, 100, 100), Is.EqualTo(5000000));
			Assert.That(InvokeInternal<long>(typeof(StealthAISpecialistPolicy),
				"StrategicTargetValueByRemainingHealth", 5000, 1000, 25, 100), Is.EqualTo(20000000),
				"Remaining HP keeps the existing bounded finish-target boost.");
			var selected = InvokeInternal<IReadOnlyList<int>>(typeof(StealthAIThreatGeometry),
				"SelectOrderedTargetCellHalf",
				new long[] { 100, 90, 80, 70, 60 },
				new double[] { 10, 30, 20, 0, 0 },
				new double[] { 1, 1, 1, 100, 100 });

			Assert.That(selected, Is.EqualTo(new[] { 0, 2 }),
				"Only the strategic top three may enter the lower-threat half; threat cannot rescue a low-value cell.");
		}

		[Test]
		public void StealthTargetCellHalvesRoundUpAndRetainSingleton()
		{
			Assert.That(InvokeInternal<IReadOnlyList<int>>(typeof(StealthAIThreatGeometry),
				"SelectOrderedTargetCellHalf", new long[] { 10 }, new double[] { 5 }, new double[] { 2 }),
				Is.EqualTo(new[] { 0 }));
			Assert.That(InvokeInternal<IReadOnlyList<int>>(typeof(StealthAIThreatGeometry),
				"SelectOrderedTargetCellHalf",
				new long[] { 50, 40, 30 }, new double[] { 3, 1, 0 }, new double[] { 1, 1, 100 }),
				Is.EqualTo(new[] { 1 }),
				"Three cells retain two by strategic value, then one by threat; the discarded third cell stays out.");
		}

		[Test]
		public void StealthSeparationCannotResurrectFilteredTargetCell()
		{
			var selected = InvokeInternal<IReadOnlyList<int>>(typeof(StealthAIThreatGeometry),
				"SelectOrderedTargetCellHalf",
				new long[] { 100, 90, 80, 70 },
				new double[] { 20, 10, 0, 0 },
				new double[] { 1, 2, 100, 100 });
			var separation = new long[] { 1, 2, 1000, 10000 };
			var final = selected.OrderByDescending(index => separation[index]).First();

			Assert.That(selected, Is.EqualTo(new[] { 1 }));
			Assert.That(final, Is.EqualTo(1),
				"The separation stage sees only the ordered-filter survivors.");
			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(states.IndexOf("SelectOrderedTargetCellHalf", StringComparison.Ordinal),
				Is.LessThan(states.IndexOf("long Separation(AirTargetPlan plan)", StringComparison.Ordinal)));
			Assert.That(states, Does.Not.Contain("bestSafe.Plan.Score / 4"));
		}

		[Test]
		public void CloakAwareProductionPolicyDistinguishesTransitDetectionAndExposure()
		{
			Assert.That(StealthAISpecialistPolicy.CloakAwareRouteDanger(0, .2f, false, true), Is.Zero,
				"An undetected cloaked transit must discount a weapon that cannot acquire it.");
			Assert.That(StealthAISpecialistPolicy.CloakAwareRouteDanger(0, .2f, true, true),
				Is.EqualTo(StealthAISpecialistPolicy.HardRouteDangerThreshold),
				"Detector and live weapon overlap is hard route danger before exposure.");
			Assert.That(StealthAISpecialistPolicy.CloakAwareRouteDanger(0, 0, true, true), Is.Zero,
				"An unguarded detector is not an invented weapon threat.");
			Assert.That(StealthAISpecialistPolicy.CloakAwareRouteDanger(0, .2f, false, false),
				Is.EqualTo(.2f), "A currently exposed unit must pay actual weapon influence.");
			Assert.That(StealthAISpecialistPolicy.CloakAwareRouteDanger(1, 0, false, true), Is.EqualTo(1),
				"Terrain and resource danger is independent of cloak state.");

			Assert.That(StealthAISpecialistPolicy.PlannedExposureIsSafe(false, true, false), Is.True);
			Assert.That(StealthAISpecialistPolicy.PlannedExposureIsSafe(true, true, false), Is.False,
				"Ordinary attack or detector-exposed Crush must reject covering fire.");
			Assert.That(StealthAISpecialistPolicy.PlannedExposureIsSafe(false, false, false), Is.False,
				"An approved ordinary attack requires the immediate recloak-window cell.");
			Assert.That(StealthAISpecialistPolicy.PlannedExposureIsSafe(true, false, true), Is.True,
				"The existing Kite/Mass crossover exception remains an explicit approval path.");

			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(states, Does.Contain("cache?.CloakedDanger"));
			Assert.That(states, Does.Contain("OrdinaryCrushExposureIsSafe("));
			Assert.That(states, Does.Contain("SafePostAttackStrategicCell("));
			var routeOwner = states.Substring(states.IndexOf(
				"static List<CPos> StealthRouteToCell", StringComparison.Ordinal));
			routeOwner = routeOwner.Substring(0, routeOwner.IndexOf(
				"static List<CPos> BuildValidatedFiringRoute", StringComparison.Ordinal));
			Assert.That(routeOwner, Does.Not.Contain("Info.Name"),
				"Routing must be capability-based, without target type/name exceptions.");
			Assert.That(routeOwner, Does.Not.Contain("MinimumStrategicCellValue"),
				"The target-selection floor must never enter route cost or traversal.");
		}

		[Test]
		public void StrategicTargetFloorIsStrictTierThenFallback()
		{
			var high = StealthAISpecialistPolicy.MinimumStrategicCellValue;
			Assert.That(StealthAISpecialistPolicy.TargetCellIsInActiveTier(high, true), Is.True);
			Assert.That(StealthAISpecialistPolicy.TargetCellIsInActiveTier(high - 1, true), Is.False,
				"Low targets cannot compete while a high target tier is being tried.");
			Assert.That(StealthAISpecialistPolicy.TargetCellIsInActiveTier(high, false), Is.False);
			Assert.That(StealthAISpecialistPolicy.TargetCellIsInActiveTier(high - 1, false), Is.True,
				"Low targets become eligible only when no eligible high target exists.");
		}

		[Test]
		public void StrategicCellEngagementUsesConfiguredPriorityAfterEligibility()
		{
			var withPreferredActor = StealthAISpecialistPolicy.HighestPriorityEligibleEngagements(new[]
			{
				(Item: "wall-a", Priority: 1),
				(Item: "factory", Priority: 15),
				(Item: "wall-b", Priority: 1)
			});
			Assert.That(withPreferredActor, Is.EqualTo(new[] { "factory" }),
				"A strategic cell containing an eligible configured-priority target must not engage its walls first.");

			var preferredUnsafe = StealthAISpecialistPolicy.HighestPriorityEligibleEngagements(new[]
			{
				(Item: "wall-a", Priority: 1),
				(Item: "wall-b", Priority: 1)
			});
			Assert.That(preferredUnsafe, Is.EqualTo(new[] { "wall-a", "wall-b" }),
				"Eligibility is resolved before priority, preserving fallback when the preferred actor is invalid or unsafe.");

			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(states, Does.Contain("HighestPriorityEligibleEngagements("));
			Assert.That(states, Does.Contain("cellSafePlans.Select(entry => (entry.Plan, entry.Priority))"),
				"The production selected-cell engagement path must pass its already safety-validated plans to the priority gate.");
		}

		[Test]
		public void GroupedEscapeChoosesAnUnoccupiedEnterableExactCell()
		{
			Assert.That(StealthAISpecialistPolicy.FirstUnoccupiedEnterableDestination(
				new[] { true, false, false }, new[] { true, false, true }), Is.EqualTo(2),
				"An occupied approved-cell center must advance to a passable unoccupied exact cell.");
			Assert.That(StealthAISpecialistPolicy.FirstUnoccupiedEnterableDestination(
				new[] { true, false }, new[] { true, false }), Is.EqualTo(-1));

			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			var escape = states.Substring(states.IndexOf(
				"static bool IssueStealthEscape", StringComparison.Ordinal));
			escape = escape.Substring(0, escape.IndexOf(
				"protected static int StealthKillCadenceMaximumTicks", StringComparison.Ordinal));
			Assert.That(escape, Does.Contain("FirstUnoccupiedEnterableDestination("));
			Assert.That(escape, Does.Contain("groupedActors: members"),
				"The correction must preserve one bounded grouped escape order.");
		}

		[Test]
		public void SafetyRepositionRejectsAClampedDestinationOutsideItsEvaluatedStrategicCell()
		{
			Assert.That(StealthAISpecialistPolicy.DestinationBelongsToStrategicCell(
				21, 8, 6, 3, 0), Is.False,
				"A playable-map clamp must not turn an off-map neighbor into a same-cell no-op escape.");
			Assert.That(StealthAISpecialistPolicy.DestinationBelongsToStrategicCell(
				27, 9, 6, 4, 1), Is.True,
				"A genuine adjacent strategic-cell destination must remain available.");

			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			var neighbor = states.Substring(states.IndexOf(
				"static CPos? NearestSafeStealthNeighbor", StringComparison.Ordinal));
			neighbor = neighbor.Substring(0, neighbor.IndexOf(
				"static bool PendingBlueExplosionInSquadCell", StringComparison.Ordinal));
			Assert.That(neighbor, Does.Contain("DestinationBelongsToStrategicCell("),
				"The production safety selector must validate the strategic cell after clamping.");
		}

		[Test]
		public void StealthEfficiencyWatchdogUsesExactRawTickFormulasAndUndefinedSemantics()
		{
			long actorTicks = 0;
			foreach (var liveActors in new[] { 1, 2, 1, 0 })
				actorTicks = StealthAISpecialistPolicy.AccumulateActorTicks(actorTicks, liveActors);
			Assert.That(actorTicks, Is.EqualTo(4),
				"Actor joins and deaths contribute only their live actor-ticks.");

			var rated = StealthAISpecialistPolicy.StealthEfficiency(3000, 3000, 600, 2);
			Assert.That(rated.RawKilledValue, Is.EqualTo(3000));
			Assert.That(rated.ActorTicks, Is.EqualTo(3000));
			Assert.That(rated.ActorMinutes, Is.EqualTo(1));
			Assert.That(rated.UniqueStnks, Is.EqualTo(2));
			Assert.That(rated.TotalDamage, Is.EqualTo(600));
			Assert.That(rated.AverageDamage, Is.EqualTo(300));
			Assert.That(rated.Primary, Is.EqualTo(3000));
			Assert.That(rated.DamageAdjusted, Is.EqualTo(10));
			Assert.That(rated.InfiniteDamageAdjusted, Is.False);

			var zeroActorTime = StealthAISpecialistPolicy.StealthEfficiency(3000, 0, 600, 2);
			Assert.That(zeroActorTime.ActorMinutes, Is.Zero);
			Assert.That(zeroActorTime.Primary, Is.Null);
			Assert.That(zeroActorTime.DamageAdjusted, Is.Null);
			var zeroActors = StealthAISpecialistPolicy.StealthEfficiency(3000, 3000, 0, 0);
			Assert.That(zeroActors.AverageDamage, Is.Null);
			Assert.That(zeroActors.DamageAdjusted, Is.Null);
			Assert.That(zeroActors.InfiniteDamageAdjusted, Is.False);
			var noDamage = StealthAISpecialistPolicy.StealthEfficiency(3000, 3000, 0, 2);
			Assert.That(noDamage.AverageDamage, Is.Zero);
			Assert.That(noDamage.DamageAdjusted, Is.Null);
			Assert.That(noDamage.InfiniteDamageAdjusted, Is.True,
				"Positive primary divided by exact zero average damage is explicitly infinite.");
			var zeroOverZero = StealthAISpecialistPolicy.StealthEfficiency(0, 3000, 0, 2);
			Assert.That(zeroOverZero.DamageAdjusted, Is.Null);
			Assert.That(zeroOverZero.InfiniteDamageAdjusted, Is.False);
		}

		[Test]
		public void StealthEfficiencyWatchdogOutputIsStableInvariantAndPeriodicTerminalEquivalent()
		{
			var metric = StealthAISpecialistPolicy.StealthEfficiency(1000, 1000, 3, 2);
			var expectedPeriodic = "stealth_efficiency_watchdog|summary=periodic|bot_id=7|scope=stnk|" +
				"window_start_tick=11|window_end_tick=99|raw_killed_value=1000|actor_ticks=1000|" +
				"actor_minutes=0.3333333333333333|unique_stnks=2|total_damage=3|average_damage=1.5|" +
				"primary=3000|damage_adjusted=2000|diagnostic_only=true";
			var expectedTerminal = expectedPeriodic.Replace("summary=periodic", "summary=terminal");

			var previousCulture = CultureInfo.CurrentCulture;
			var previousUiCulture = CultureInfo.CurrentUICulture;
			try
			{
				CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
				CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
				Assert.That(StealthAISpecialistPolicy.FormatStealthEfficiencySummary(
					"periodic", 7, 11, 99, metric), Is.EqualTo(expectedPeriodic));
				Assert.That(StealthAISpecialistPolicy.FormatStealthEfficiencySummary(
					"terminal", 7, 11, 99, metric), Is.EqualTo(expectedTerminal));
			}
			finally
			{
				CultureInfo.CurrentCulture = previousCulture;
				CultureInfo.CurrentUICulture = previousUiCulture;
			}

			var zeroTime = StealthAISpecialistPolicy.StealthEfficiency(1000, 0, 3, 2);
			Assert.That(StealthAISpecialistPolicy.FormatStealthEfficiencySummary(
				"terminal", 7, 11, 99, zeroTime), Does.Contain(
				"|actor_ticks=0|actor_minutes=0|unique_stnks=2|total_damage=3|average_damage=1.5|" +
				"primary=unavailable|damage_adjusted=unavailable|"));
			var infinite = StealthAISpecialistPolicy.StealthEfficiency(1000, 3000, 0, 1);
			Assert.That(StealthAISpecialistPolicy.FormatStealthEfficiencySummary(
				"periodic", 7, 11, 99, infinite), Does.Contain(
				"|unique_stnks=1|total_damage=0|average_damage=0|primary=1000|damage_adjusted=infinite|"));
		}

		[Test]
		public void StealthEfficiencyWindowUsesTheProgramFormulaAndStableMemberDenominator()
		{
			var window = new StealthEfficiencyWindow(17);
			window.Observe(new uint[] { 9, 4, 9 });
			window.Observe(new uint[] { 4 });
			window.RecordKill(3000);
			window.RecordKill(-10);
			window.RecordDamage(9, 400);
			window.RecordDamage(12, 200);

			Assert.That(window.StartTick, Is.EqualTo(17));
			Assert.That(window.Actors, Is.EqualTo(new uint[] { 4, 9, 12 }));
			var metric = window.Summary();
			Assert.That(metric.RawKilledValue, Is.EqualTo(3000));
			Assert.That(window.KillCount, Is.EqualTo(2),
				"Kill count is factual even when a killed actor has zero configured value.");
			Assert.That(metric.ActorTicks, Is.EqualTo(3));
			Assert.That(metric.UniqueStnks, Is.EqualTo(3));
			Assert.That(metric.TotalDamage, Is.EqualTo(600));
			Assert.That(metric.ActorMinutes, Is.EqualTo(0.001));
			Assert.That(metric.AverageDamage, Is.EqualTo(200));
			Assert.That(metric.Primary, Is.EqualTo(3000000));
			Assert.That(metric.DamageAdjusted, Is.EqualTo(15000));
		}

		[Test]
		public void StealthGenerationEfficiencySaveLoadPreservesCompleteTerminalWindowsExactlyOnce()
		{
			var generation1 = new StealthEfficiencyWindow(17);
			generation1.Observe(new uint[] { 9, 4 });
			generation1.Observe(new uint[] { 4 });
			generation1.RecordKill(3000);
			generation1.RecordDamage(9, 400);
			var generation3 = new StealthEfficiencyWindow(41);
			generation3.Observe(new uint[] { 12 });
			generation3.RecordKill(0);
			generation3.RecordDamage(12, 200);

			var saved = StealthAISpecialistPolicy.SaveStealthGenerationEfficiency(
				new Dictionary<int, StealthEfficiencyWindow>
				{
					{ 3, generation3 },
					{ 1, generation1 }
				});
			Assert.That(saved.Key, Is.EqualTo("StealthGenerationEfficiency"));
			Assert.That(saved.Value.Nodes.Count(n => n.Key == "Generation"), Is.EqualTo(2));
			Assert.That(StealthAISpecialistPolicy.TryLoadStealthGenerationEfficiency(
				saved, out var loaded), Is.True);
			Assert.That(loaded.Select(pair => pair.Key), Is.EqualTo(new[] { 1, 3 }));

			var restored = loaded.Single(pair => pair.Key == 1).Value;
			var restoredState = restored.ExportState();
			Assert.That(restoredState.StartTick, Is.EqualTo(17));
			Assert.That(restoredState.RawKilledValue, Is.EqualTo(3000));
			Assert.That(restoredState.KillCount, Is.EqualTo(1));
			Assert.That(restoredState.ActorTicks, Is.EqualTo(3));
			Assert.That(restoredState.TotalDamage, Is.EqualTo(400));
			Assert.That(restoredState.Actors, Is.EqualTo(new uint[] { 4, 9 }));

			generation1.Observe(new uint[] { 9, 15 });
			restored.Observe(new uint[] { 9, 15 });
			generation1.RecordKill(900);
			restored.RecordKill(900);
			generation1.RecordDamage(15, 250);
			restored.RecordDamage(15, 250);
			Assert.That(restored.KillCount, Is.EqualTo(generation1.KillCount));
			Assert.That(restored.Actors, Is.EqualTo(generation1.Actors));
			Assert.That(StealthAISpecialistPolicy.FormatStealthEfficiencySummary(
				"terminal-generation-1", 7, restored.StartTick, 99, restored.Summary()), Is.EqualTo(
				StealthAISpecialistPolicy.FormatStealthEfficiencySummary(
					"terminal-generation-1", 7, generation1.StartTick, 99, generation1.Summary())));

			Assert.That(StealthAISpecialistPolicy.TryLoadStealthGenerationEfficiency(
				null, out var absent), Is.False, "Old saves without the new section remain loadable.");
			Assert.That(absent, Is.Empty);
			var terminalReported = false;
			Assert.That(StealthAISpecialistPolicy.TryBeginStealthTerminalSummary(
				ref terminalReported, true, true), Is.True);
			Assert.That(StealthAISpecialistPolicy.TryBeginStealthTerminalSummary(
				ref terminalReported, true, true), Is.False,
				"Restored generation state must still use the shared exactly-once terminal guard.");
		}

		[Test]
		public void HumanReplayScorerGroupsControlAndFlushesThroughTheSharedExactlyOnceGuard()
		{
			var scorer = Source("OpenRA.Mods.Common/Traits/StealthEfficiencyControlWatchdog.cs");
			Assert.That(scorer, Does.Contain("!self.Owner.IsBot || self.World.IsReplay"),
				"Replay playback must score recorded owner actions even though replay metadata retains bot ownership.");
			Assert.That(scorer, Does.Contain("control = self.World.IsReplay ? \"replay-owner\" : \"human\""));
			Assert.That(scorer, Does.Contain("control={2} generation=1"));
			Assert.That(scorer, Does.Contain("45000 / Math.Max(1, playerActor.World.Timestep)"));
			Assert.That(scorer, Does.Contain("KillCadenceFailed(cadenceAge, maximumTicks)"));
			Assert.That(scorer, Does.Contain("scope=replay-owner"));
			Assert.That(scorer, Does.Contain("if (!playerActor.World.IsReplay)"),
				"Non-comparable owner cadence context must remain replay-only.");
			Assert.That(scorer, Does.Contain("comparable=false per-squad=unavailable"),
				"Owner aggregation must not masquerade as live per-squad cadence.");
			Assert.That(scorer, Does.Contain("actor-time-denominator=sum-live-member-ticks"));
			Assert.That(scorer, Does.Contain("World.GameEnding += EmitTerminalSummary"));
			Assert.That(scorer, Does.Contain("World.GameEnding -= EmitTerminalSummary"));
			Assert.That(scorer, Does.Contain("TryBeginStealthTerminalSummary"));
			Assert.That(scorer.Split("FormatStealthEfficiencySummary(").Length - 1, Is.EqualTo(1),
				"One human control group owns one terminal metric emission site.");

			var reported = false;
			Assert.That(StealthAISpecialistPolicy.TryBeginStealthTerminalSummary(
				ref reported, true, true), Is.True);
			Assert.That(StealthAISpecialistPolicy.TryBeginStealthTerminalSummary(
				ref reported, true, true), Is.False);

			var world = Source("OpenRA.Game/World.cs");
			var dispose = world.Substring(world.IndexOf("public void Dispose()", StringComparison.Ordinal));
			dispose = dispose.Substring(0, dispose.IndexOf("OrderGenerator?.Deactivate();", StringComparison.Ordinal));
			Assert.That(dispose, Does.Contain("if (IsReplay)"));
			Assert.That(dispose, Does.Contain("EndGame();"),
				"Replay world disposal must deliver the ordinary terminal callback before actors disappear.");
		}

		[Test]
		public void StealthEfficiencyWatchdogOwnsOneBotScopeWindowAndSurvivesSaveLoad()
		{
			Assert.That(StealthAISpecialistPolicy.ShouldOwnStealthEfficiencyTerminal(true, true), Is.True);
			Assert.That(StealthAISpecialistPolicy.ShouldOwnStealthEfficiencyTerminal(false, true), Is.False,
				"A trait whose bot was never enabled must never subscribe or emit.");
			Assert.That(StealthAISpecialistPolicy.ShouldOwnStealthEfficiencyTerminal(true, false), Is.False,
				"A disabled bot-specific module must never subscribe or emit.");
			Assert.That(StealthAISpecialistPolicy.ShouldOwnStealthEfficiencyTerminal(false, false), Is.False);

			var beforeSave = StealthAISpecialistPolicy.StealthEfficiency(1234, 5678, 90, 4);
			var afterLoad = StealthAISpecialistPolicy.StealthEfficiency(
				beforeSave.RawKilledValue, beforeSave.ActorTicks, beforeSave.TotalDamage, beforeSave.UniqueStnks);
			Assert.That(StealthAISpecialistPolicy.FormatStealthEfficiencySummary(
				"periodic", 42, 17, 6000, afterLoad), Is.EqualTo(
				StealthAISpecialistPolicy.FormatStealthEfficiencySummary(
					"periodic", 42, 17, 6000, beforeSave)));

			var manager = Source("OpenRA.Mods.Common/Traits/BotModules/SquadManagerBotModule.cs");
			Assert.That(manager, Does.Contain("actor.Owner == Player"));
			Assert.That(manager, Does.Contain("e.Attacker.Owner != Player"));
			Assert.That(manager, Does.Contain("self.Owner == Player && self.Info.Name == \"stnk\""));
			Assert.That(manager, Does.Contain("StealthEfficiencyWindowStartTick"));
			Assert.That(manager, Does.Contain("SaveStealthGenerationEfficiency(stealthGenerationEfficiency)"));
			Assert.That(manager, Does.Contain("TryLoadStealthGenerationEfficiency("));
			Assert.That(manager.Split("World.GameEnding += EmitTerminalStealthWatchdogSummaries").Length - 1,
				Is.EqualTo(1));
			Assert.That(manager, Does.Contain("World.GameEnding -= EmitTerminalStealthWatchdogSummaries"),
				"Only the enabled bot-specific module may own the terminal summary subscription.");
			var traitEnabled = manager.Substring(manager.IndexOf(
				"protected override void TraitEnabled", StringComparison.Ordinal));
			traitEnabled = traitEnabled.Substring(0, traitEnabled.IndexOf(
				"protected override void TraitDisabled", StringComparison.Ordinal));
			Assert.That(traitEnabled, Does.Not.Contain("World.GameOver +="),
				"Trait enablement alone must never subscribe the terminal handler.");
			var subscription = manager.Substring(manager.IndexOf(
				"void UpdateStealthEfficiencyTerminalSubscription", StringComparison.Ordinal));
			subscription = subscription.Substring(0, subscription.IndexOf(
				"void IBotTick.BotTick", StringComparison.Ordinal));
			Assert.That(subscription, Does.Contain("bot != null, !IsTraitDisabled"));
			Assert.That(manager, Does.Not.Contain("World.Timestep,\n\t\t\t\tstealthEfficiencyDamageTaken"),
				"Whole-game time or wall-clock timestep must not replace accumulated STNK actor ticks.");

			var output = manager.Substring(manager.IndexOf(
				"void EmitStealthEfficiencySummary", StringComparison.Ordinal));
			output = output.Substring(0, output.IndexOf("void RunFailsafeTestPressure", StringComparison.Ordinal));
			Assert.That(output, Does.Not.Contain("Target"));
			Assert.That(output, Does.Not.Contain("Route"));
			Assert.That(output, Does.Not.Contain("Priority"));
		}

		[Test]
		public void AutomatedAndOrdinaryShutdownShareExactlyOnceTerminalNotification()
		{
			var game = Source("OpenRA.Game/Game.cs");
			var automatedExit = game.Substring(game.IndexOf(
				"static void TryAutomatedExit", StringComparison.Ordinal));
			automatedExit = automatedExit.Substring(0, automatedExit.IndexOf(
				"static void TryAutomatedSave", StringComparison.Ordinal));
			Assert.That(automatedExit, Does.Contain("world.EndGame();"),
				"A non-periodic configured boundary such as tick 3000 must use the ordinary terminal callback.");
			Assert.That(automatedExit, Does.Not.Contain("FinishBenchmark(false)"));
			Assert.That(automatedExit, Does.Contain("automatedExitTick = -1;"),
				"The configured exit remains armed for one delivery only.");

			var world = Source("OpenRA.Game/World.cs");
			var endGame = world.Substring(world.IndexOf("public void EndGame()", StringComparison.Ordinal));
			endGame = endGame.Substring(0, endGame.IndexOf("Player renderPlayer", StringComparison.Ordinal));
			Assert.That(endGame, Does.Contain("if (!IsGameOver)"));
			Assert.That(endGame.Split("GameEnding();").Length - 1, Is.EqualTo(1),
				"Natural and configured termination must each deliver the terminal callback once.");
			Assert.That(endGame.IndexOf("GameEnding();", StringComparison.Ordinal), Is.LessThan(
				endGame.IndexOf("GameOver();", StringComparison.Ordinal)),
				"Terminal diagnostics must flush before benchmark/game-over subscribers close output.");

			var reported = false;
			Assert.That(StealthAISpecialistPolicy.TryBeginStealthTerminalSummary(
				ref reported, true, true), Is.True);
			Assert.That(reported, Is.True);
			Assert.That(StealthAISpecialistPolicy.TryBeginStealthTerminalSummary(
				ref reported, true, true), Is.False,
				"A second lifecycle hook must not duplicate terminal cadence or efficiency output.");
		}

		[Test]
		public void TerminalWatchdogsIncludeEveryRealGenerationAndExcludeEmptySlots()
		{
			var first = new StealthKillCadenceGeneration(3, 100);
			var second = new StealthKillCadenceGeneration(1, 200);
			Assert.That(StealthAISpecialistPolicy.TerminalStealthGenerationIds(new[]
			{
				first, null, second, first
			}), Is.EqualTo(new[] { 1, 3 }),
				"Terminal output is ordered, includes active real generations, and excludes generation-0 slots.");

			var manager = Source("OpenRA.Mods.Common/Traits/BotModules/SquadManagerBotModule.cs");
			var terminal = manager.Substring(manager.IndexOf(
				"void EmitTerminalStealthWatchdogSummaries", StringComparison.Ordinal));
			terminal = terminal.Substring(0, terminal.IndexOf(
				"void EmitStealthEfficiencySummary", StringComparison.Ordinal));
			Assert.That(terminal, Does.Contain("EmitTerminalStealthCadenceSummaries();"));
			Assert.That(terminal, Does.Contain("EmitStealthEfficiencySummary(\"terminal\");"));
			Assert.That(terminal, Does.Contain("EmitStealthCadenceSummary(record, squad, \"terminal\")"));
			Assert.That(terminal, Does.Contain("owner={0}"));
			Assert.That(terminal, Does.Contain("generation-kills={9}"));
			Assert.That(terminal, Does.Contain("KillCadenceFailed(generation.CadenceAge, maximumTicks)"),
				"A retired generation must retain a factual threshold violation after its live squad disappears.");
			Assert.That(terminal, Does.Contain("stealthCadenceGenerations.Values"),
				"Terminal output must include retired generations after their reusable squad slots are removed.");
			Assert.That(terminal, Does.Not.Contain("TerminalStealthGenerationIds("),
				"Terminal output must not be limited to squads that remain active at game-over.");
		}

		[Test]
		public void RepeatedStealthGenerationRetirementEmitsOnceAndLeavesBoundedActiveState()
		{
			var activeCadence = new Dictionary<int, StealthCadenceGenerationRecord>();
			var activeEfficiency = new Dictionary<int, StealthEfficiencyWindow>();
			var retiredCadence = new List<int>();
			var retiredEfficiency = new List<int>();
			for (var id = 1; id <= 100; id++)
			{
				activeCadence.Add(id, new StealthCadenceGenerationRecord("stealth-tank", id % 3,
					new StealthKillCadenceGeneration(id, id * 10)));
				activeEfficiency.Add(id, new StealthEfficiencyWindow(id * 10));
				Assert.That(StealthAISpecialistPolicy.TryTakeStealthGeneration(
					activeCadence, id, out var cadence), Is.True);
				retiredCadence.Add(cadence.Generation.GenerationId);
				Assert.That(StealthAISpecialistPolicy.TryTakeStealthGeneration(
					activeEfficiency, id, out _), Is.True);
				retiredEfficiency.Add(id);
				Assert.That(StealthAISpecialistPolicy.TryTakeStealthGeneration(
					activeCadence, id, out _), Is.False, "A retired generation cannot emit twice.");
				Assert.That(activeCadence, Is.Empty);
				Assert.That(activeEfficiency, Is.Empty);
			}

			Assert.That(retiredCadence, Is.EqualTo(Enumerable.Range(1, 100)),
				"Every retired cadence diagnostic must be delivered before pruning.");
			Assert.That(retiredEfficiency, Is.EqualTo(Enumerable.Range(1, 100)),
				"Every paired efficiency diagnostic must be delivered before pruning.");

			var manager = Source("OpenRA.Mods.Common/Traits/BotModules/SquadManagerBotModule.cs");
			var retirement = manager.IndexOf("FinalizeStealthGenerationDiagnostics(squad, \"retired\")",
				StringComparison.Ordinal);
			Assert.That(retirement, Is.GreaterThanOrEqualTo(0));
			Assert.That(retirement, Is.LessThan(manager.IndexOf("Squads.Remove(squad)", retirement,
				StringComparison.Ordinal)), "Diagnostics must emit and prune before the reusable slot retires.");
			Assert.That(manager, Does.Contain("summary, stealthCadenceGenerations.Count"),
				"Full-engine evidence must expose the retained active-generation bound.");
			Assert.That(manager, Does.Contain("FinalizeOrphanedLoadedStealthGenerationDiagnostics();"),
				"Old saves must not reinstall historical generations into active retained state.");
		}

		[Test]
		public void ActiveStealthCadenceGenerationsRoundTripForTerminalDiagnostics()
		{
			var generation = new StealthKillCadenceGeneration(7, 100);
			generation.Observe(400, true);
			Assert.That(generation.AttributeKill(400), Is.True);
			generation.Observe(2800, true);
			generation.MarkCadenceFailed();
			var saved = StealthAISpecialistPolicy.SaveStealthCadenceGenerations(new[]
			{
				new StealthCadenceGenerationRecord("stealth-tank", 2, generation)
			});

			Assert.That(saved.Key, Is.EqualTo("StealthCadenceGenerations"));
			Assert.That(saved.Value.Nodes.Count(node => node.Key == "Generation"), Is.EqualTo(1),
				"Only the active generation is persisted after retired generations are emitted and pruned.");
			Assert.That(StealthAISpecialistPolicy.TryLoadStealthCadenceGenerations(
				saved, out var loaded), Is.True);
			Assert.That(loaded, Has.Length.EqualTo(1));
			Assert.That(loaded[0].SquadDefinition, Is.EqualTo("stealth-tank"));
			Assert.That(loaded[0].SquadIndex, Is.EqualTo(2));
			Assert.That(loaded[0].Generation.GenerationId, Is.EqualTo(7));
			Assert.That(loaded[0].Generation.GenerationStartTick, Is.EqualTo(100));
			Assert.That(loaded[0].Generation.WindowStartTick, Is.EqualTo(400));
			Assert.That(loaded[0].Generation.LastObservedTick, Is.EqualTo(2800));
			Assert.That(loaded[0].Generation.CadenceAge, Is.EqualTo(2400));
			Assert.That(loaded[0].Generation.AttributedKills, Is.EqualTo(1));
			Assert.That(loaded[0].Generation.CadenceFailed, Is.True);
			Assert.That(StealthAISpecialistPolicy.TryLoadStealthCadenceGenerations(
				null, out var absent), Is.False, "Old saves remain backward compatible.");
			Assert.That(absent, Is.Empty);

			var duplicate = StealthAISpecialistPolicy.SaveStealthCadenceGenerations(new[]
			{
				loaded[0], loaded[0]
			});
			Assert.That(StealthAISpecialistPolicy.TryLoadStealthCadenceGenerations(
				duplicate, out _), Is.False, "One terminal record per generation is required.");
		}

		[Test]
		public void OrdinaryAndStalledFireRoutesShareValidatedFinalizer()
		{
			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			Assert.That(states.Split("BuildValidatedFiringRoute(").Length - 1, Is.EqualTo(3),
				"The shared invariant must be defined once and used by both ordinary and stalled routes.");

			var stalledFallback = states.Substring(states.IndexOf(
				"var stalledTarget = owner.TargetActor;", StringComparison.Ordinal));
			stalledFallback = stalledFallback.Substring(0, stalledFallback.IndexOf(
				"BeginStealthSafetyReposition(owner);", StringComparison.Ordinal));
			Assert.That(stalledFallback, Does.Not.Contain("stalledTarget.Location.X / StealthCoarseSize(owner)"));

			var ordinaryPlan = states.Substring(states.IndexOf(
				"if (firingCell != null)", StringComparison.Ordinal));
			ordinaryPlan = ordinaryPlan.Substring(0, ordinaryPlan.IndexOf(
				"var plan = new AirTargetPlan(actor", StringComparison.Ordinal));
			Assert.That(ordinaryPlan, Does.Not.Contain("new List<CPos>(safeRoute)"),
				"The target strategic-cell route must never be submitted ahead of the safe annulus endpoint.");
			Assert.That(states, Does.Contain("LogDirectSafeRouteEvidence(owner, cache, actor,"));
			Assert.That(states, Does.Contain("minimum-distance-squared={12} all-outside={13}"),
				"Debug evidence must classify every submitted Obelisk waypoint without changing release behavior.");
			Assert.That(states, Does.Contain("CanOutrangeUndetectingTarget("));
			Assert.That(states, Does.Contain("cache?.CloakedDanger"),
				"Cached cloak/detector capability must own transit safety for every threat type.");
			var watchdog = Source("OpenRA.Mods.Common/Traits/BotOwnedStationaryWatchdog.cs");
			Assert.That(watchdog, Does.Contain("AI stationary watchdog cloak-transition"));
			Assert.That(watchdog, Does.Contain("AI stationary watchdog damage"));
			Assert.That(watchdog, Does.Contain("AI stationary watchdog death"));
		}

		[TestCase(29, 30, 37, 30)]
		[TestCase(30, 31, 38, 31)]
		[TestCase(31, 32, 39, 32)]
		public void OrdinaryObeliskProductionRouteAppendsOnlyOutsideOffsetRange(
			int targetX, int targetY, int firingX, int firingY)
		{
			var target = new CPos(targetX, targetY);
			var firingCell = new CPos(firingX, firingY);
			var coarseWaypoints = new[]
			{
				new CPos(21, 21), new CPos(27, 21), new CPos(33, 21),
				new CPos(39, 21), new CPos(39, 27), new CPos(39, 33)
			};

			var constructionCalls = 0;
			var route = StealthAIThreatGeometry.BuildDirectSafeFiringRoute(() =>
			{
				constructionCalls++;
				return coarseWaypoints;
			}, firingCell, target, 7);
			Assert.That(constructionCalls, Is.EqualTo(1),
				"The ordinary regression must drive the shared production route construction.");
			Assert.That(route, Is.Not.Null);
			Assert.That(route.Last(), Is.EqualTo(firingCell),
				"The ordinary production finalizer must append the exact safe firing cell.");
			Assert.That(route.All(cell => StealthAIThreatGeometry.IsOutsideWeaponRange(
				cell, target, 7)), Is.True,
				"Every submitted ordinary coarse waypoint and exact endpoint must be outside range.");
		}

		[TestCase(28, 33, 21, 33, 39, 33)]
		[TestCase(33, 28, 33, 21, 33, 39)]
		[TestCase(32, 32, 33, 33, 40, 32)]
		public void StalledObeliskProductionRouteRejectsOffsetBoundaryIngress(
			int targetX, int targetY, int unsafeX, int unsafeY, int firingX, int firingY)
		{
			var target = new CPos(targetX, targetY);
			var unsafeCoarseWaypoint = new CPos(unsafeX, unsafeY);
			var firingCell = new CPos(firingX, firingY);
			Assert.That(StealthAIThreatGeometry.IsOutsideWeaponRange(
				unsafeCoarseWaypoint, target, 7), Is.False);

			var constructionCalls = 0;
			var route = StealthAIThreatGeometry.BuildDirectSafeFiringRoute(() =>
			{
				constructionCalls++;
				return new[] { new CPos(21, 21) };
			}, firingCell, target, 7);
			Assert.That(constructionCalls, Is.EqualTo(1),
				"The stalled regression must drive the shared production route construction.");
			Assert.That(route, Is.Not.Null);
			Assert.That(route.Last(), Is.EqualTo(firingCell),
				"The stalled production builder must append the exact safe firing cell.");
			Assert.That(route.All(cell => StealthAIThreatGeometry.IsOutsideWeaponRange(
				cell, target, 7)), Is.True,
				"Every submitted stalled coarse waypoint and exact endpoint must be outside range.");

			var rejectedRoute = StealthAIThreatGeometry.BuildDirectSafeFiringRoute(() =>
			{
				constructionCalls++;
				return new[] { new CPos(21, 21), unsafeCoarseWaypoint };
			}, firingCell, target, 7);
			Assert.That(constructionCalls, Is.EqualTo(2));
			Assert.That(rejectedRoute, Is.Null,
				"The stalled production finalizer must reject inside and exact-boundary coarse centers.");
		}

		[Test]
		public void StationaryWatchdogExemptsOnlyObservedHealingAndShotConfirmedCadence()
		{
			var none = StealthAISpecialistPolicy.StationaryWatchdogExemption(false, false);
			Assert.That(StealthAISpecialistPolicy.NextStationaryWatchdogAge(1499, false, none),
				Is.EqualTo(1500), "Attack, approach, staging, and repair travel remain non-exempt.");
			Assert.That(StealthAISpecialistPolicy.ObservedRepairAmount(400, 400), Is.Zero);
			Assert.That(StealthAISpecialistPolicy.ObservedRepairAmount(400, 405), Is.EqualTo(5));
			Assert.That(StealthAISpecialistPolicy.StationaryWatchdogExemption(false, true),
				Is.EqualTo(BotStationaryWatchdogExemption.Repairing));

			var cadence = StealthAISpecialistPolicy.FiringEpisodeCadenceTicks(70, new[] { 10 }, 2);
			Assert.That(cadence, Is.EqualTo(82));
			Assert.That(StealthAISpecialistPolicy.IsSustainedFiringEpisode(
				100, 182, cadence, true, true, true), Is.True);
			Assert.That(StealthAISpecialistPolicy.IsSustainedFiringEpisode(
				100, 183, cadence, true, true, true), Is.False);
			Assert.That(StealthAISpecialistPolicy.IsSustainedFiringEpisode(
				100, 101, cadence, false, true, true), Is.False);
			Assert.That(StealthAISpecialistPolicy.StationaryWatchdogFailed(1499, 1500), Is.False);
			Assert.That(StealthAISpecialistPolicy.StationaryWatchdogFailed(1500, 1500), Is.True);
		}

		[Test]
		public void LegacyCadenceRankingHelperDoesNotEnterGameplayPlanning()
		{
			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			var definition = Source("OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/StealthSquadDefinition.cs");
			Assert.That(definition, Does.Contain("MaximumUndefendedTargetTravelSeconds = 20"));
			Assert.That(states, Does.Contain("diagnostic output only and must never change target eligibility"));
			Assert.That(states, Does.Not.Contain("CadenceUrgentLocalQuickClearRank("));
			Assert.That(states, Does.Not.Contain("ShouldReplaceNonFinishableMission("));
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
