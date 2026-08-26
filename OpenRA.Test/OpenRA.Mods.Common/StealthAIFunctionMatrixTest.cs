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
			Assert.That(states, Does.Contain("owner.TargetActor == plan.Actor && owner.AirRouteQueued"),
				"Target review must not restart the active core's moving route to the same incumbent.");
			Assert.That(states, Does.Contain("routeTraveling && owner.StealthRouteLastCenterCell != null"));
			Assert.That(states, Does.Contain("suppressed stalled-target rescan"),
				"The short target watchdog must not cancel a moving route whose squad center is progressing.");
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
		}

		[Test]
		public void UndefendedTravelWindowIsPreferenceRatherThanEligibility()
		{
			Assert.That(StealthAISpecialistPolicy.IsWithinUndefendedTravelPreference(60000, 60), Is.True);
			Assert.That(StealthAISpecialistPolicy.IsWithinUndefendedTravelPreference(60001, 60), Is.False);
			var states = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs");
			var shortSafe = states.IndexOf("best = safePlans.Where", StringComparison.Ordinal);
			var kite = states.IndexOf("best = clearPlans.Where", shortSafe, StringComparison.Ordinal);
			var mass = states.IndexOf("best = clearPlans.Where", kite + 1, StringComparison.Ordinal);
			var farSafe = states.IndexOf("best = bestSafe.Plan", mass, StringComparison.Ordinal);
			Assert.That(shortSafe, Is.GreaterThanOrEqualTo(0));
			Assert.That(kite, Is.GreaterThan(shortSafe));
			Assert.That(mass, Is.GreaterThan(kite));
			Assert.That(farSafe, Is.GreaterThan(mass));
		}
	}
}
