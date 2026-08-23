#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class StealthTankSquadPolicyTest
	{
		sealed class NonAttackDischargeActivity : OpenRA.Activities.Activity { }

		sealed class RepairInfluenceState
		{
			public readonly string Profile;
			public readonly int Weight;
			public object CachedFacts;

			public RepairInfluenceState(string profile, int weight)
			{
				Profile = profile;
				Weight = weight;
			}

			public string GetPrivateInfluence(object sharedFacts)
			{
				CachedFacts = sharedFacts;
				return $"{Profile}:{Weight}";
			}
		}

		[Test]
		public void BothProfilesUseOneSharedControlImplementation()
		{
			var profileSpecificImplementations = typeof(StealthTankSquadBotModule).Assembly.GetTypes()
				.Where(t => t != typeof(StealthTankSquadBotModule) &&
					typeof(StealthTankSquadBotModule).IsAssignableFrom(t)).ToArray();

			Assert.That(profileSpecificImplementations, Is.Empty,
				"Stealth and Chemical must remain configured instances of one control module.");
		}

		[TestCase("stealth-tank")]
		[TestCase("chemical")]
		public void BothProfilesUseTheSameLifecycleContract(string profile)
		{
			Assert.That(profile, Is.Not.Empty);
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(true, false,
				false, false, false, false, 149, 75, 75), Is.EqualTo(StealthTankPlanInvalidation.None));
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(true, false,
				false, false, true, false, 149, 75, 75), Is.EqualTo(StealthTankPlanInvalidation.RouteUnsafe));
		}

		[Test]
		public void StrategicWorkRunsOnlyAtTheConfiguredCadence()
		{
			var countdown = 1;
			var scanTicks = new System.Collections.Generic.List<int>();
			for (var tick = 1; tick <= 225; tick++)
				if (StealthTankSquadPolicy.ShouldRunStrategicScan(ref countdown, 75))
					scanTicks.Add(tick);

			Assert.That(scanTicks, Is.EqualTo(new[] { 1, 76, 151 }));
		}

		[Test]
		public void StrategicFactsAreSharedOnlyWithinOneWorldTick()
		{
			Assert.That(StealthTankSquadPolicy.ShouldRefreshStrategicView(75, 75), Is.False);
			Assert.That(StealthTankSquadPolicy.ShouldRefreshStrategicView(75, 76), Is.True);
		}

		[Test]
		public void SpecialistInfluenceCacheMatchesAirLifetimeBoundary()
		{
			Assert.That(StealthTankSquadPolicy.ShouldRefreshInfluenceMap(int.MinValue, 1, 125), Is.True);
			Assert.That(StealthTankSquadPolicy.ShouldRefreshInfluenceMap(1, 76, 125), Is.False);
			Assert.That(StealthTankSquadPolicy.ShouldRefreshInfluenceMap(1, 125, 125), Is.False);
			Assert.That(StealthTankSquadPolicy.ShouldRefreshInfluenceMap(1, 126, 125), Is.True);
		}

		[Test]
		public void SpecialistInfluenceCacheIsInstanceOwnedAndCannotContaminateAirCache()
		{
			var specialistCache = typeof(StealthTankSquadBotModule).GetField("influenceMap",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(specialistCache, Is.Not.Null);
			Assert.That(specialistCache.IsStatic, Is.False);

			var airState = typeof(StealthTankSquadBotModule).Assembly.GetType(
				"OpenRA.Mods.Common.Traits.BotModules.Squads.AirStateBase");
			var airCaches = airState.GetField("InfluenceCaches", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(airCaches, Is.Not.Null);
			Assert.That(specialistCache.FieldType, Is.Not.EqualTo(airCaches.FieldType));
		}

		[TestCase(false, false, false, false, false, false, 75, 75, StealthTankPlanInvalidation.TargetChanged)]
		[TestCase(true, true, false, false, false, false, 75, 75, StealthTankPlanInvalidation.TargetChanged)]
		[TestCase(true, false, true, false, false, false, 75, 75, StealthTankPlanInvalidation.MembershipChanged)]
		[TestCase(true, false, false, true, false, false, 75, 75, StealthTankPlanInvalidation.TargetMoved)]
		[TestCase(true, false, false, false, true, false, 75, 75, StealthTankPlanInvalidation.RouteUnsafe)]
		[TestCase(true, false, false, false, false, false, 149, 75, StealthTankPlanInvalidation.None)]
		[TestCase(true, false, false, false, false, false, 150, 75, StealthTankPlanInvalidation.NoProgress)]
		[TestCase(true, false, false, false, false, true, 76, 75, StealthTankPlanInvalidation.LostActivity)]
		public void StablePlansRetryOnlyOnExplicitInvalidation(bool hasPlan, bool targetChanged,
			bool membershipChanged, bool targetMoved, bool routeUnsafe, bool lostActivity, int currentTick,
			int lastProgressTick, StealthTankPlanInvalidation expected)
		{
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(hasPlan, targetChanged,
				membershipChanged, targetMoved, routeUnsafe, lostActivity, currentTick, lastProgressTick, 75),
				Is.EqualTo(expected));
		}

		[Test]
		public void StealthStrategicGeometryUsesExactSixBySixCells()
		{
			Assert.That(StealthTankSquadPolicy.RequiredStrategicCellSize, Is.EqualTo(6));
			Assert.That(StealthTankSquadPolicy.StrategicCell(new CPos(0, 0), 6), Is.EqualTo(new CPos(0, 0)));
			Assert.That(StealthTankSquadPolicy.StrategicCell(new CPos(5, 5), 6), Is.EqualTo(new CPos(0, 0)));
			Assert.That(StealthTankSquadPolicy.StrategicCell(new CPos(6, 6), 6), Is.EqualTo(new CPos(1, 1)));
			Assert.That(StealthTankSquadPolicy.IsSameStrategicCell(
				new CPos(6, 6), new CPos(11, 11), 6), Is.True);
			Assert.That(StealthTankSquadPolicy.IsSameStrategicCell(
				new CPos(5, 5), new CPos(6, 6), 6), Is.False);
		}

		[Test]
		public void ReinforcementsStageOnlyAfterAFormationExistsAndJoinAtAdjacentSixCells()
		{
			Assert.That(StealthTankSquadPolicy.ShouldStageReinforcement(true, false), Is.True);
			Assert.That(StealthTankSquadPolicy.ShouldStageReinforcement(false, false), Is.False);
			Assert.That(StealthTankSquadPolicy.ShouldStageReinforcement(true, true), Is.False);
			Assert.That(StealthTankSquadPolicy.ReinforcementGroup(1, new[] { 3, 0, 0 }), Is.Zero,
				"A topology slot without a core must not turn an incoming tank into a new mission.");
			Assert.That(StealthTankSquadPolicy.ReinforcementGroup(1, new[] { 3, 1, 0 }), Is.EqualTo(1));
			Assert.That(StealthTankSquadPolicy.ReinforcementGroup(2, new[] { 3, 1, 0 }), Is.EqualTo(1));
			Assert.That(StealthTankSquadPolicy.CanAdvanceReinforcement(true, false), Is.True);
			Assert.That(StealthTankSquadPolicy.CanAdvanceReinforcement(true, true), Is.False,
				"A repaired staged member must finish its retained retreat responsibility first.");
			Assert.That(StealthTankSquadPolicy.RecoveryCore(new uint[] { 14, 12 },
				new System.Collections.Generic.HashSet<uint> { 12, 14 }), Is.EqualTo(12),
				"A staged-only restored group must deterministically recover one core member.");
			Assert.That(StealthTankSquadPolicy.RecoveryCore(new uint[] { 12, 14 },
				new System.Collections.Generic.HashSet<uint> { 14 }), Is.Null);
			Assert.That(StealthTankSquadPolicy.IsSameOrAdjacentStrategicCell(
				new CPos(0, 0), new CPos(11, 11), 6), Is.True);
			Assert.That(StealthTankSquadPolicy.IsSameOrAdjacentStrategicCell(
				new CPos(0, 0), new CPos(12, 0), 6), Is.False);
		}

		[TestCase(true, false, false, true, false, false)]
		[TestCase(true, false, false, true, true, false)]
		[TestCase(true, false, true, true, false, true)]
		[TestCase(true, true, false, false, false, false)]
		[TestCase(true, true, true, false, false, false)]
		[TestCase(true, true, false, true, false, true)]
		[TestCase(false, false, false, true, false, true)]
		public void ReinforcementOrdersRetainAirStylePlanOwnership(bool retainedPlanMatches,
			bool retainedSafeHold, bool isIdle, bool routeAvailable, bool issuedThisTick, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.ShouldIssueReinforcementOrder(retainedPlanMatches,
				retainedSafeHold, isIdle, routeAvailable, issuedThisTick), Is.EqualTo(expected));
		}

		[TestCase(true, false, true)]
		[TestCase(true, true, true)]
		[TestCase(false, true, false,
			Description = "Blue Tiberium is route cost, not an emergency movement veto.")]
		[TestCase(false, false, false)]
		public void LocalSafetyEvadesThreatButNeverBlueAlone(bool localThreatExposure,
			bool blueAdjacent, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.ShouldEvadeLocalDanger(localThreatExposure,
				blueAdjacent), Is.EqualTo(expected));
		}

		[TestCase(0f, false)]
		[TestCase(StealthTankSquadPolicy.SoftResourceRouteCost, false)]
		[TestCase(StealthTankSquadPolicy.HardRouteDangerThreshold, true)]
		[TestCase(5f, true)]
		public void BlueCostRemainsBelowHardRouteDanger(float danger, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.IsHardRouteDanger(danger), Is.EqualTo(expected));
		}

		[TestCase(true, false, false, true)]
		[TestCase(true, true, false, false, Description = "Both Blue and Red endpoints are rejected.")]
		[TestCase(true, false, true, false)]
		[TestCase(false, false, false, false)]
		public void CompletionRetreatRequiresPassableResourceFreeSafeEndpoint(bool passable,
			bool hasResource, bool hardDanger, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.IsRetreatDestinationSafe(passable,
				hasResource, hardDanger), Is.EqualTo(expected));
		}

		[Test]
		public void StagedReinforcementOwnershipSurvivesSaveLoadWithoutDuplicates()
		{
			var saved = StealthTankSquadPolicy.SaveReinforcementState(new[]
			{
				new StealthTankReinforcementSaveGroup
				{
					GroupIndex = 0,
					Members = new uint[] { 14, 12 },
					PlanTargets = new[]
					{
						new KeyValuePair<uint, uint>(14, 701),
						new KeyValuePair<uint, uint>(12, 701)
					},
					SafeHolds = new uint[] { 14 }
				},
				new StealthTankReinforcementSaveGroup { GroupIndex = 2, Members = new uint[] { 18 } }
			});

			Assert.That(StealthTankSquadPolicy.TryLoadReinforcementState(saved, out var restored), Is.True);
			Assert.That(restored.Select(g => g.GroupIndex), Is.EqualTo(new[] { 0, 2 }));
			Assert.That(restored[0].Members, Is.EqualTo(new uint[] { 12, 14 }));
			Assert.That(restored[0].PlanTargets, Is.EqualTo(new[]
			{
				new KeyValuePair<uint, uint>(12, 701),
				new KeyValuePair<uint, uint>(14, 701)
			}));
			Assert.That(restored[0].SafeHolds, Is.EqualTo(new uint[] { 14 }));
			Assert.That(restored.SelectMany(g => g.Members), Is.Unique);
		}

		[Test]
		public void LegacyReinforcementSaveLoadsWithoutTransientPlanOwnership()
		{
			var legacy = new MiniYamlNode("StealthTankReinforcementState", "", new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", FieldSaver.FormatValue(1)),
				new MiniYamlNode("Group", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("Index", FieldSaver.FormatValue(0)),
					new MiniYamlNode("Members", FieldSaver.FormatValue(new uint[] { 12, 14 }))
				})
			});

			Assert.That(StealthTankSquadPolicy.TryLoadReinforcementState(legacy, out var restored), Is.True);
			Assert.That(restored[0].Members, Is.EqualTo(new uint[] { 12, 14 }));
			Assert.That(restored[0].PlanTargets, Is.Empty);
			Assert.That(restored[0].SafeHolds, Is.Empty);
		}

		[Test]
		public void ReinforcementSaveRejectsLatchOwnershipOutsideStagedMembers()
		{
			var malformed = StealthTankSquadPolicy.SaveReinforcementState(new[]
			{
				new StealthTankReinforcementSaveGroup
				{
					GroupIndex = 0,
					Members = new uint[] { 12 },
					PlanTargets = new[] { new KeyValuePair<uint, uint>(14, 701) },
					SafeHolds = new uint[] { 14 }
				}
			});

			Assert.That(StealthTankSquadPolicy.TryLoadReinforcementState(malformed, out _), Is.False);
		}

		[TestCase(true, true, true, true)]
		[TestCase(false, true, true, false)]
		[TestCase(true, false, true, false)]
		[TestCase(true, true, false, false)]
		public void RestoredReinforcementLatchRequiresLiveOwnership(bool validMember,
			bool validTarget, bool ownsActivity, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.ShouldRestoreReinforcementPlan(validMember,
				validTarget, ownsActivity), Is.EqualTo(expected));
		}

		[TestCase(true, true, true, true)]
		[TestCase(false, true, true, false)]
		[TestCase(true, false, true, false)]
		[TestCase(true, true, false, false)]
		public void SavedStagedMemberReattachesBeforeLatchValidation(bool eligible,
			bool reserved, bool selected, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.ShouldRestoreReinforcementMember(eligible,
				reserved, selected), Is.EqualTo(expected));
		}

		[Test]
		public void MobileTargetInsideMissionCellDoesNotCauseImmediateOrderChurn()
		{
			var movedInsideCell = !StealthTankSquadPolicy.IsSameStrategicCell(
				new CPos(7, 8), new CPos(10, 11), 6);
			Assert.That(movedInsideCell, Is.False);
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(true, false,
				false, movedInsideCell, false, false, 299, 0, 300), Is.EqualTo(StealthTankPlanInvalidation.None));
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(true, false,
				false, movedInsideCell, false, false, 300, 0, 300), Is.EqualTo(StealthTankPlanInvalidation.NoProgress));
		}

		[Test]
		public void RetainedMovingTargetAcrossStrategicCellKeepsAirStyleOneShotOrdersUntilStall()
		{
			Assert.That(StealthTankSquadPolicy.IsSameStrategicCell(
				new CPos(5, 5), new CPos(6, 6), 6), Is.False);

			// The caller has already reassessed and retained this live actor, so its
			// movement is not an order invalidation. Attack follows the actor and the
			// bounded no-progress path remains the route-refresh authority.
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(true, false,
				false, false, false, false, 299, 0, 300), Is.EqualTo(StealthTankPlanInvalidation.None));
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(true, false,
				false, false, false, false, 300, 0, 300), Is.EqualTo(StealthTankPlanInvalidation.NoProgress));
		}

		[TestCase(18, 18, 6, 6, 27, 27)]
		[TestCase(18, 18, 30, 18, 15, 15)]
		[TestCase(1, 1, 8, 1, 3, 9)]
		public void RevealRetreatMovesExactlyOneAdjacentSixCellStrategicCell(
			int unitX, int unitY, int targetX, int targetY, int expectedX, int expectedY)
		{
			var start = StealthTankSquadPolicy.StrategicCell(new CPos(unitX, unitY), 6);
			var destination = StealthTankSquadPolicy.OneStrategicCellRetreat(
				new CPos(unitX, unitY), new CPos(targetX, targetY), 6, 96, 96);
			var destinationCell = StealthTankSquadPolicy.StrategicCell(destination, 6);

			Assert.That(destination, Is.EqualTo(new CPos(expectedX, expectedY)));
			Assert.That(System.Math.Max(System.Math.Abs(destinationCell.X - start.X),
				System.Math.Abs(destinationCell.Y - start.Y)), Is.EqualTo(1));
		}

		[Test]
		public void PostMissionRetreatWaitsForSelectedTargetCompletion()
		{
			Assert.That(StealthTankSquadPolicy.ShouldBeginPostMissionRetreat(true, true, true), Is.False,
				"A firing reveal must retain the still-valid selected target until mission completion.");
			Assert.That(StealthTankSquadPolicy.ShouldBeginPostMissionRetreat(true, true, false), Is.True,
				"A completed, captured, or stale selected target must start the post-mission retreat.");
			Assert.That(StealthTankSquadPolicy.ShouldBeginPostMissionRetreat(true, false, false), Is.False,
				"An idle group has no completed mission to retreat from.");
			Assert.That(StealthTankSquadPolicy.ShouldBeginPostMissionRetreat(false, true, false), Is.False,
				"Profiles without configured reveal retreat retain their existing lifecycle.");
		}

		[Test]
		public void NearbyUndefendedReactionHasATwentyFiveTickBound()
		{
			Assert.That(StealthTankSquadPolicy.NearbyReactionMaximumLatencyTicks, Is.EqualTo(25));
		}

		[Test]
		public void NearbyReactionKeepsDistantIncumbentInFreshSwitchDecision()
		{
			var candidates = StealthTankSquadPolicy.NearbyReassessmentCandidates(
				new[] { 11, 12 }, 99, (a, b) => a == b);

			Assert.That(candidates, Is.EqualTo(new[] { 11, 12, 99 }),
				"A valid distant incumbent must remain available for the same thresholded switch decision as Air.");
			Assert.That(StealthTankSquadPolicy.ReassessTarget(true, true, 100,
				true, true, 124, 25), Is.EqualTo(StealthTankTargetReassessment.RetainIncumbent));
			Assert.That(StealthTankSquadPolicy.ReassessTarget(true, true, 100,
				true, true, 125, 25), Is.EqualTo(StealthTankTargetReassessment.SwitchToChallenger));
		}

		[Test]
		public void NearbyReactionPreservesRankFortyNineIncumbentBeyondChallengerCap()
		{
			var reassessment = StealthTankSquadPolicy.NearbyReassessmentCandidates(
				Enumerable.Range(0, 60), 99, (a, b) => a == b);
			Assert.That(reassessment.Last(), Is.EqualTo(99));
		}

		[Test]
		public void NearbyReactionDoesNotDuplicateAlreadyNearbyIncumbent()
		{
			var candidates = StealthTankSquadPolicy.NearbyReassessmentCandidates(
				new[] { 11, 12 }, 12, (a, b) => a == b);

			Assert.That(candidates, Is.EqualTo(new[] { 11, 12 }));
		}

		[Test]
		public void VersionedMultiUnitRetreatSaveRestoresBarrierUntilEveryMemberCompletes()
		{
			var saved = StealthTankSquadPolicy.SaveRetreatState(new[]
			{
				new StealthTankRetreatSaveGroup
				{
					GroupIndex = 0,
					TargetId = 91,
					Destinations = new[]
					{
						new System.Collections.Generic.KeyValuePair<uint, CPos>(11, new CPos(27, 21)),
						new System.Collections.Generic.KeyValuePair<uint, CPos>(12, new CPos(33, 27))
					}
				}
			});

			Assert.That(StealthTankSquadPolicy.TryLoadRetreatState(saved, out var restored), Is.True);
			Assert.That(restored, Has.Length.EqualTo(1));
			Assert.That(restored[0].TargetId, Is.EqualTo(91));
			Assert.That(restored[0].Destinations.Select(d => d.Key), Is.EqualTo(new uint[] { 11, 12 }));

			var remaining = restored[0].Destinations.ToDictionary(d => d.Key, d => d.Value);
			Assert.That(StealthTankSquadPolicy.ShouldBlockReassessment(remaining.Count), Is.True);
			remaining.Remove(11);
			Assert.That(StealthTankSquadPolicy.ShouldBlockReassessment(remaining.Count), Is.True,
				"One completed member must not release the restored multi-unit retreat barrier.");
			remaining.Remove(12);
			Assert.That(StealthTankSquadPolicy.ShouldBlockReassessment(remaining.Count), Is.False);
		}

		[Test]
		public void CompletedRevealRetreatReassessesWithTheLiveTargetAsIncumbent()
		{
			Assert.That(StealthTankSquadPolicy.CompleteRetreat(1, true),
				Is.EqualTo(StealthTankRetreatCompletion.ContinueRetreat),
				"No target decision may release a pending multi-unit retreat barrier.");
			Assert.That(StealthTankSquadPolicy.CompleteRetreat(0, true),
				Is.EqualTo(StealthTankRetreatCompletion.ReassessWithIncumbent),
				"A live attacked target must remain the incumbent after the safety move.");
			Assert.That(StealthTankSquadPolicy.CompleteRetreat(0, false),
				Is.EqualTo(StealthTankRetreatCompletion.ReassessWithoutIncumbent),
				"A dead, stale, or captured target must never be resumed.");
		}

		[Test]
		public void MalformedOrFutureRetreatSaveFallsBackWithoutState()
		{
			var malformed = new MiniYamlNode("StealthTankRetreatState", "", new[]
			{
				new MiniYamlNode("Version", FieldSaver.FormatValue(
					StealthTankSquadPolicy.RetreatSaveVersion + 1))
			}.ToList());

			Assert.That(StealthTankSquadPolicy.TryLoadRetreatState(malformed, out var restored), Is.False);
			Assert.That(restored, Is.Empty);
		}

		[Test]
		public void RestoredRetreatRecomputesAwayFromLiveTargetThatMovedAcrossUnit()
		{
			var unit = new CPos(18, 18);
			var originalTarget = new CPos(30, 18);
			var movedTarget = new CPos(6, 18);
			var savedDestination = StealthTankSquadPolicy.OneStrategicCellRetreat(
				unit, originalTarget, 6, 96, 96);
			var saved = StealthTankSquadPolicy.SaveRetreatState(new[]
			{
				new StealthTankRetreatSaveGroup
				{
					GroupIndex = 0,
					TargetId = 91,
					Destinations = new[]
					{
						new System.Collections.Generic.KeyValuePair<uint, CPos>(11, savedDestination)
					}
				}
			});

			Assert.That(StealthTankSquadPolicy.TryLoadRetreatState(saved, out var restored), Is.True);
			Assert.That(StealthTankSquadPolicy.IsRetreatDestinationAwayFromTarget(unit,
				restored[0].Destinations[0].Value, movedTarget, 6, 96, 96), Is.False,
				"The saved westward destination now points toward the moved live target.");

			var recomputed = StealthTankSquadPolicy.OneStrategicCellRetreat(unit, movedTarget, 6, 96, 96);
			Assert.That(StealthTankSquadPolicy.IsRetreatDestinationAwayFromTarget(
				unit, recomputed, movedTarget, 6, 96, 96), Is.True);
			Assert.That(recomputed.X, Is.GreaterThan(unit.X),
				"Restore must resume east, away from the target that moved west of the unit.");
		}

		[Test]
		public void RepairingMemberDoesNotResolveRetreatResponsibilityBeforeArrival()
		{
			Assert.That(StealthTankSquadPolicy.IsRetreatResponsibilityResolved(
				true, true, false), Is.False,
				"Entering repair must not silently release the group retreat barrier.");
			Assert.That(StealthTankSquadPolicy.IsRetreatResponsibilityResolved(
				true, true, true), Is.True,
				"Physical arrival completes the responsibility even if repair starts afterward.");
			Assert.That(StealthTankSquadPolicy.IsRetreatResponsibilityResolved(
				false, true, false), Is.True,
				"A dead or otherwise ineligible actor cannot retain an impossible responsibility.");
		}

		[TestCase(0, 0)]
		[TestCase(1, 0)]
		[TestCase(2, 2)]
		[TestCase(3, 2)]
		[TestCase(4, 2)]
		[TestCase(9, 4)]
		[TestCase(10, 5)]
		[TestCase(20, 10)]
		public void SpecialistReservationLeavesRoughlyHalfForOrdinaryArmies(int total, int expected)
		{
			Assert.That(StealthTankSquadPolicy.SpecialistCount(total), Is.EqualTo(expected));
		}

		[Test]
		public void ClaimAllPolicySelectsEveryDistinctEligibleTankAndReservesNewDiscoveries()
		{
			var selected = StealthTankSquadPolicy.SelectSpecialistIds(
				new uint[] { 14, 11, 13, 12, 14 }, new uint[] { 11, 99 }, true, true);

			Assert.That(selected, Is.EqualTo(new uint[] { 11, 12, 13, 14 }));
			Assert.That(selected, Is.Unique);
			Assert.That(StealthTankSquadPolicy.ShouldReserveUnit(false, true, true), Is.True,
				"A produced or captured eligible tank must be reserved before the next strategic scan.");
			Assert.That(StealthTankSquadPolicy.ShouldReserveUnit(false, true, false), Is.False);
		}

		[Test]
		public void SpecialistSquadTopologyHasAHardFourSquadCeiling()
		{
			Assert.That(StealthTankSquadPolicy.SquadCount(2, true), Is.EqualTo(3));
			Assert.That(StealthTankSquadPolicy.SquadCount(3, true),
				Is.EqualTo(StealthTankSquadPolicy.MaximumSquadCount));
			Assert.That(StealthTankSquadPolicy.SquadCount(4, true),
				Is.GreaterThan(StealthTankSquadPolicy.MaximumSquadCount));
			Assert.That(StealthTankSquadPolicy.SquadCount(int.MaxValue, true),
				Is.GreaterThan(StealthTankSquadPolicy.MaximumSquadCount),
				"Overflow must not let an invalid configuration bypass the four-squad ruleset guard.");
		}

		[TestCase(0, 0)]
		[TestCase(1, 0)]
		[TestCase(2, 1)]
		[TestCase(3, 2)]
		[TestCase(4, 2)]
		[TestCase(10, 5)]
		public void HalfPreservingReservationNeverConsumesTheOpeningPair(int total, int expected)
		{
			Assert.That(StealthTankSquadPolicy.SpecialistCount(total, false), Is.EqualTo(expected));
		}

		[Test]
		public void PartnerDeathRetainsLoneSpecialistOwnershipAndActiveGroup()
		{
			var selected = StealthTankSquadPolicy.SelectSpecialistIds(new uint[] { 11 },
				new uint[] { 10, 11 }, true);

			Assert.That(selected, Is.EqualTo(new uint[] { 11 }));
			Assert.That(StealthTankSquadPolicy.GroupForIndex(0, selected.Length), Is.Zero);
			Assert.That(StealthTankSquadPolicy.RepairDisposition(true, false, false, false),
				Is.EqualTo(SpecialistRepairDisposition.Active));
		}

		[Test]
		public void CompatibleReplacementReformsWithTheOwnedSurvivor()
		{
			var selected = StealthTankSquadPolicy.SelectSpecialistIds(new uint[] { 11, 12 },
				new uint[] { 11 }, true);

			Assert.That(selected, Is.EqualTo(new uint[] { 11, 12 }));
			Assert.That(selected.Select(id => StealthTankSquadPolicy.GroupForIndex(
				System.Array.IndexOf(selected, id), selected.Length)), Is.EqualTo(new[] { 0, 0 }));
		}

		[Test]
		public void RestoredLoneOwnershipSurvivesSaveLoadRebalance()
		{
			var savedReservedIds = new uint[] { 11 };
			var selected = StealthTankSquadPolicy.SelectSpecialistIds(new uint[] { 11 },
				savedReservedIds, true);

			Assert.That(selected, Is.EqualTo(savedReservedIds));
		}

		[Test]
		public void ReformationCannotCreateOwnerlessOrDuplicateReservations()
		{
			var selected = StealthTankSquadPolicy.SelectSpecialistIds(
				new uint[] { 12, 11, 12 }, new uint[] { 11, 11, 99 }, true);

			Assert.That(selected, Is.EqualTo(new uint[] { 11, 12 }));
			Assert.That(selected, Is.Unique);
			Assert.That(selected, Has.None.EqualTo(99));
		}

		[Test]
		public void LargeForceCreatesTwoHarassmentGroupsAndOneAttackGroup()
		{
			Assert.That(StealthTankSquadPolicy.GroupForIndex(0, 6), Is.EqualTo(0));
			Assert.That(StealthTankSquadPolicy.GroupForIndex(1, 6), Is.EqualTo(0));
			Assert.That(StealthTankSquadPolicy.GroupForIndex(2, 6), Is.EqualTo(1));
			Assert.That(StealthTankSquadPolicy.GroupForIndex(3, 6), Is.EqualTo(1));
			Assert.That(StealthTankSquadPolicy.GroupForIndex(4, 6), Is.EqualTo(2));
			Assert.That(StealthTankSquadPolicy.GroupForIndex(5, 6), Is.EqualTo(2));
			Assert.That(StealthTankSquadPolicy.RoleForGroup(2), Is.EqualTo(StealthTankSquadRole.Attack));
		}

		[Test]
		public void ChemicalConfigurationAlwaysCreatesOneHarassmentGroup()
		{
			for (var i = 0; i < 6; i++)
				Assert.That(StealthTankSquadPolicy.GroupForIndex(i, 6, 1, false), Is.Zero);

			Assert.That(StealthTankSquadPolicy.RoleForGroup(0, 1, false),
				Is.EqualTo(StealthTankSquadRole.Harass));
		}

		[Test]
		public void ScoringRewardsValueAndIncumbencyButPenalizesDistance()
		{
			var baseline = StealthTankSquadPolicy.TargetScore(1000, 1000, 20, 100);
			Assert.That(StealthTankSquadPolicy.TargetScore(1000, 2000, 20, 100), Is.GreaterThan(baseline));
			Assert.That(StealthTankSquadPolicy.TargetScore(1000, 1000, 40, 100), Is.LessThan(baseline));
			Assert.That(StealthTankSquadPolicy.TargetScore(1000, 1000, 20, 125), Is.GreaterThan(baseline));
			Assert.That(StealthTankSquadPolicy.TargetScore(1000, 1000, 20, 100, 100, 3),
				Is.LessThan(baseline));
		}

		[Test]
		public void GroundTargetScoreUsesRoutableTravelCostInsteadOfStraightLineDistance()
		{
			var direct = StealthTankSquadPolicy.RouteDistanceCells(new CPos(0, 0),
				new[] { new CPos(6, 0) });
			var detour = StealthTankSquadPolicy.RouteDistanceCells(new CPos(0, 0),
				new[] { new CPos(0, 6), new CPos(6, 6), new CPos(6, 0) });

			Assert.That(direct, Is.EqualTo(6));
			Assert.That(detour, Is.EqualTo(18));
			Assert.That(StealthTankSquadPolicy.OptimisticApproachDistance(20, 6), Is.EqualTo(14));
			Assert.That(StealthTankSquadPolicy.TargetScore(1000, 1000, detour, 100),
				Is.LessThan(StealthTankSquadPolicy.TargetScore(1000, 1000, direct, 100)));
		}

		[TestCase(true, 100, true, true, 124, 25, StealthTankTargetReassessment.RetainIncumbent)]
		[TestCase(true, 100, true, true, 125, 25, StealthTankTargetReassessment.SwitchToChallenger)]
		[TestCase(true, 10000, true, false, 100000, 0, StealthTankTargetReassessment.RetainIncumbent)]
		[TestCase(false, 10000, true, true, 1, 100, StealthTankTargetReassessment.SwitchToChallenger)]
		public void EveryTargetReassessmentUsesExactAirSwitchPolicy(bool incumbentUndefended,
			long incumbentScore, bool challengerValid, bool challengerUndefended, long challengerScore,
			int improvementPercent, StealthTankTargetReassessment expected)
		{
			Assert.That(StealthTankSquadPolicy.ReassessTarget(true, incumbentUndefended,
				incumbentScore, challengerValid, challengerUndefended, challengerScore, improvementPercent),
				Is.EqualTo(expected));
			Assert.That(expected == StealthTankTargetReassessment.SwitchToChallenger,
				Is.EqualTo(AirThreatGeometry.ShouldSwitchTarget(incumbentUndefended, incumbentScore,
					challengerValid, challengerUndefended, challengerScore, improvementPercent)));
		}

		[Test]
		public void InvalidTargetSwitchesOrAbandonsWithoutApplyingThreshold()
		{
			Assert.That(StealthTankSquadPolicy.ReassessTarget(false, false, 10000,
				true, false, 1, 100), Is.EqualTo(StealthTankTargetReassessment.SwitchToChallenger));
			Assert.That(StealthTankSquadPolicy.ReassessTarget(false, false, 10000,
				false, false, 0, 100), Is.EqualTo(StealthTankTargetReassessment.Abandon));
		}

		[Test]
		public void GlobalAndNearbyCellPoolsUseAirRequiredIncumbentBeyondCandidateCap()
		{
			var distances = Enumerable.Range(0, 60).Select(i => (long)i).ToArray();
			var utilities = Enumerable.Range(0, 60).Select(i => i >= 24 && i < 48 ? 1000 - i : 0).ToArray();
			var ordinary = AirThreatGeometry.SelectTargetCandidates(distances, utilities, 24, 24);
			var bounded = AirThreatGeometry.SelectTargetCandidates(distances, utilities, 24, 24, 55);

			Assert.That(ordinary, Has.No.Member(55));
			Assert.That(bounded.Take(ordinary.Count), Is.EqualTo(ordinary),
				"The Air closest/highest-value strategic-cell pool must remain unchanged.");
			Assert.That(bounded.Last(), Is.EqualTo(55));
			Assert.That(StealthTankSquadPolicy.ReassessTarget(
				bounded.Contains(55), true, 100, true, true, 124, 25),
				Is.EqualTo(StealthTankTargetReassessment.RetainIncumbent));
		}

		[Test]
		public void UnroutableIncumbentIsNotRetainedAndStalledRoutableMissionRetries()
		{
			Assert.That(StealthTankSquadPolicy.ReassessTarget(
				false, false, 0, true, true, 1, 25),
				Is.EqualTo(StealthTankTargetReassessment.SwitchToChallenger),
				"A no-route incumbent is invalid for this scan, so the next routable target must be tried.");
			Assert.That(StealthTankSquadPolicy.ReassessTarget(
				false, false, 0, false, false, 0, 25),
				Is.EqualTo(StealthTankTargetReassessment.Abandon),
				"No unroutable target may be installed as a retained plan.");
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(
				true, false, false, false, false, false, 299, 0, 300),
				Is.EqualTo(StealthTankPlanInvalidation.None));
			Assert.That(StealthTankSquadPolicy.ClassifyPlanInvalidation(
				true, false, false, false, false, false, 300, 0, 300),
				Is.EqualTo(StealthTankPlanInvalidation.NoProgress),
				"A routable plan with no distance or damage progress must retry at the configured bound.");
		}

		[Test]
		public void InfantryClusterBonusIsBoundedAndImprovesTargetScore()
		{
			Assert.That(StealthTankSquadPolicy.InfantryClusterMultiplier(0, 50, 300), Is.EqualTo(100));
			Assert.That(StealthTankSquadPolicy.InfantryClusterMultiplier(2, 50, 300), Is.EqualTo(200));
			Assert.That(StealthTankSquadPolicy.InfantryClusterMultiplier(20, 50, 300), Is.EqualTo(300));
			Assert.That(StealthTankSquadPolicy.TargetScore(1000, 100, 10, 100, 200),
				Is.GreaterThan(StealthTankSquadPolicy.TargetScore(1000, 100, 10, 100)));
		}

		[TestCase(4999, 1000, 5, false)]
		[TestCase(5000, 1000, 5, true)]
		[TestCase(10000, 0, 5, false)]
		public void DefendedAreasRequireConfiguredOvermatch(int squadValue, int defendingValue,
			int requiredRatio, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.CanCarefullyClear(squadValue, defendingValue, requiredRatio),
				Is.EqualTo(expected));
		}

		[TestCase(19, 20, 5000, 1000, 5, false)]
		[TestCase(20, 20, 4999, 1000, 5, false)]
		[TestCase(20, 20, 5000, 1000, 5, true)]
		public void DefenderClearingRequiresPatienceAndOvermatch(int scans, int requiredScans,
			int squadValue, int defendingValue, int requiredRatio, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.CanAttemptDefenderClear(scans, requiredScans,
				squadValue, defendingValue, requiredRatio), Is.EqualTo(expected));
		}

		[TestCase(39, 34, 5, true, Description = "Defended and no-route candidates together exhaust the bounded pool.")]
		[TestCase(39, 34, 4, false, Description = "One ordinary candidate remains available.")]
		[TestCase(3, 0, 3, true, Description = "Blue or terrain may make every target unroutable without inventing a defender.")]
		[TestCase(0, 0, 0, false)]
		public void DefenderFallbackAgesWhenDangerAndRouteFailureExhaustCandidatePool(
			int candidates, int dangerous, int unroutable, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.AreAllCandidatesUnavailable(
				candidates, dangerous, unroutable), Is.EqualTo(expected));
		}

		[Test]
		public void DefenderClearingChoosesBestUnlockFromWeakestPackages()
		{
			Assert.That(StealthTankSquadPolicy.SelectDefenderClearOpportunity(
				new[] { 300, 100, 200, 50 }, new long[] { 9000, 1000, 8000, 500 }, 3), Is.EqualTo(2));
			Assert.That(StealthTankSquadPolicy.SelectDefenderClearOpportunity(
				new[] { 300, 100, 200, 50 }, new long[] { 9000, 1000, 8000, 500 }, 2), Is.EqualTo(1));
		}

		[TestCase(true, false, true, 1, 8, 2, 0, 1, SpecialistDefenderClearAction.CrushInfantry)]
		[TestCase(true, false, true, 2, 8, 2, 0, 1, SpecialistDefenderClearAction.CrushInfantry)]
		[TestCase(true, false, false, 1, 8, 2, 0, 1, SpecialistDefenderClearAction.None)]
		[TestCase(true, false, true, 1, 8, 2, 2, 1, SpecialistDefenderClearAction.None)]
		[TestCase(false, true, true, 3, 8, 5, 0, 3, SpecialistDefenderClearAction.SnipeTank)]
		[TestCase(false, true, true, 3, 7, 5, 0, 3, SpecialistDefenderClearAction.None)]
		[TestCase(false, true, true, 3, 8, 5, 2, 3, SpecialistDefenderClearAction.None)]
		[TestCase(false, false, true, 1, 8, 0, 16, 3, SpecialistDefenderClearAction.AttackUnarmedDetector,
			Description = "MHQ is Ground/Vehicle, not Structure or Tank; detector capability owns this fallback.")]
		[TestCase(false, false, true, 2, 8, 0, 16, 3, SpecialistDefenderClearAction.None)]
		[TestCase(false, false, true, 1, 8, 5, 16, 3, SpecialistDefenderClearAction.None)]
		public void DefenderClearingRequiresAnExplicitSafeCapability(bool infantry, bool tank, bool canCrush,
			int packageCount, int ownRange, int weaponRange, int detectorRange, int kiteMargin,
			SpecialistDefenderClearAction expected)
		{
			Assert.That(StealthTankSquadPolicy.DefenderClearAction(infantry, tank, canCrush,
				packageCount, ownRange, weaponRange, detectorRange, kiteMargin), Is.EqualTo(expected));
		}

		[TestCase(SpecialistDefenderClearAction.CrushInfantry, true)]
		[TestCase(SpecialistDefenderClearAction.SnipeTank, true)]
		[TestCase(SpecialistDefenderClearAction.AttackUnarmedDetector, true)]
		[TestCase(SpecialistDefenderClearAction.None, false)]
		public void SelectedClearDefenderDoesNotBlockItsOwnOtherwiseSafeApproach(
			SpecialistDefenderClearAction action, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.ShouldIgnoreSelectedDefenderInfluence(action),
				Is.EqualTo(expected));
		}

		[TestCase(true, false, false, false, SpecialistRepairDisposition.Active,
			Description = "No repair path leaves a damaged member active until death.")]
		[TestCase(true, false, false, true, SpecialistRepairDisposition.Repair)]
		[TestCase(false, true, true, true, SpecialistRepairDisposition.Rejoin)]
		[TestCase(false, false, false, true, SpecialistRepairDisposition.Active)]
		public void RepairIsOpportunisticAndNeverAnIndefiniteWait(bool damaged,
			bool repairing, bool fullyRepaired, bool reachableRepair, SpecialistRepairDisposition expected)
		{
			Assert.That(StealthTankSquadPolicy.RepairDisposition(damaged, repairing,
				fullyRepaired, reachableRepair), Is.EqualTo(expected));
		}

		[Test]
		public void BothProfilesUseSharedRepairFactsWithPrivateInfluenceState()
		{
			var ownerThreatFacts = new object();
			var stealth = new RepairInfluenceState("stealth-tank", 7);
			var chemical = new RepairInfluenceState("chemical", 13);

			var stealthInfluence = StealthTankSquadPolicy.ResolveRepairInfluence(
				ownerThreatFacts, stealth.GetPrivateInfluence);
			var chemicalInfluence = StealthTankSquadPolicy.ResolveRepairInfluence(
				ownerThreatFacts, chemical.GetPrivateInfluence);

			Assert.That(stealthInfluence, Is.EqualTo("stealth-tank:7"));
			Assert.That(chemicalInfluence, Is.EqualTo("chemical:13"));
			Assert.That(stealth.CachedFacts, Is.SameAs(ownerThreatFacts));
			Assert.That(chemical.CachedFacts, Is.SameAs(ownerThreatFacts));
			Assert.That(stealthInfluence, Is.Not.EqualTo(chemicalInfluence),
				"Shared factual threats must not share profile weights or influence caches.");
		}

		[Test]
		public void MissingSharedRepairFactsRetainsActiveNoRepairFallback()
		{
			var influenceBuilds = 0;
			var influence = StealthTankSquadPolicy.ResolveRepairInfluence<object, string>(null, facts =>
			{
				influenceBuilds++;
				return "unexpected";
			});

			Assert.That(influence, Is.Null);
			Assert.That(influenceBuilds, Is.Zero);
			Assert.That(StealthTankSquadPolicy.RepairDisposition(true, false, false,
				influence != null), Is.EqualTo(SpecialistRepairDisposition.Active));
		}

		[Test]
		public void DamagedIdleMemberWithoutRepairOrAnchorRouteUsesNearestSafeMobility()
		{
			Assert.That(StealthTankSquadPolicy.RepairDisposition(true, false, false, false),
				Is.EqualTo(SpecialistRepairDisposition.Active));
			Assert.That(StealthTankSquadPolicy.ShouldUseNearestSafeMobilityFallback(
				true, false, true), Is.True,
				"Air's nearest-safe second stage must keep an idle active member moving.");
			Assert.That(StealthTankSquadPolicy.ShouldUseNearestSafeMobilityFallback(
				false, false, true), Is.False,
				"A busy matching activity retains ownership and must not be replaced.");
			Assert.That(StealthTankSquadPolicy.ShouldUseNearestSafeMobilityFallback(
				true, true, true), Is.False,
				"The anchor-directed safe route remains the preferred first stage.");
		}

		[Test]
		public void LostActivityContinuationAndTelemetryUseOnlyIdleAppliedMembers()
		{
			var active = new[] { 989, 1104, 1183, 1234, 1250 };
			var idle = StealthTankSquadPolicy.LostActivityPlanMembers(active, id => id == 1250);

			Assert.That(idle, Is.EqualTo(new[] { 1250 }),
				"The same applied set is passed to one-shot plan submission and diagnostic telemetry.");
			Assert.That(active.Except(idle), Is.EqualTo(new[] { 989, 1104, 1183, 1234 }),
				"Busy matching members remain outside both the order and continued-idle telemetry set.");
		}

		[Test]
		public void LostActivityUsesBoundedMemberRouteBeforeChangingFiringEndpoint()
		{
			Assert.That(StealthTankSquadPolicy.LostActivityRouteDecision(
				true, true, true), Is.EqualTo(SpecialistLostActivityRouteDecision.RetainShared),
				"A reachable representative route remains the one-shot plan.");
			Assert.That(StealthTankSquadPolicy.LostActivityRouteDecision(
				false, true, true), Is.EqualTo(SpecialistLostActivityRouteDecision.SameEndpointMemberRoute),
				"An invalid shared waypoint uses an exact member route to the same selected firing endpoint.");
			Assert.That(StealthTankSquadPolicy.LostActivityRouteDecision(
				false, false, true), Is.EqualTo(SpecialistLostActivityRouteDecision.AlternateEndpoint),
				"An unreachable or memoized zero-progress member route advances to a different bounded endpoint.");
			Assert.That(StealthTankSquadPolicy.LostActivityRouteDecision(
				false, false, false), Is.EqualTo(SpecialistLostActivityRouteDecision.None),
				"The planner never periodically reissues an identical failed route as a fallback.");
		}

		[Test]
		public void FailedLostActivityRouteSurvivesAnUnsuccessfulSecondScan()
		{
			Assert.That(StealthTankSquadPolicy.FailedMemberRouteRemainsApplicable(
				true, true, true), Is.True,
				"An unchanged actor, target and target location retain the failed signature after no fallback was queued.");
			Assert.That(StealthTankSquadPolicy.ShouldRecomputeSameEndpointMemberRoute(true), Is.False,
				"The next scan neither recomputes nor queues the identical failed shared/member route.");
			Assert.That(StealthTankSquadPolicy.LostActivityRouteDecision(
				false, false, false), Is.EqualTo(SpecialistLostActivityRouteDecision.None));

			Assert.That(StealthTankSquadPolicy.FailedMemberRouteRemainsApplicable(
				false, true, true), Is.False, "A changed target invalidates the signature.");
			Assert.That(StealthTankSquadPolicy.FailedMemberRouteRemainsApplicable(
				true, false, true), Is.False, "Target movement invalidates the signature.");
			Assert.That(StealthTankSquadPolicy.FailedMemberRouteRemainsApplicable(
				true, true, false), Is.False, "Member movement invalidates the signature.");
			Assert.That(StealthTankSquadPolicy.ShouldRecomputeSameEndpointMemberRoute(false), Is.True,
				"A distinct route revision remains eligible for one bounded exact-path calculation.");
			Assert.That(StealthTankSquadPolicy.LostActivityRouteDecision(
				false, false, true), Is.EqualTo(SpecialistLostActivityRouteDecision.AlternateEndpoint),
				"A distinct alternate endpoint remains eligible while the identical route stays memoized.");
		}

		[Test]
		public void IdleRetreatRetryUsesExactRouteOrSameAwayCellWithoutDuplicateReissue()
		{
			Assert.That(StealthTankSquadPolicy.RetreatRetryRouteDecision(
				true, true), Is.EqualTo(SpecialistRetreatRetryRouteDecision.SameEndpointExactRoute),
				"A reachable original resource-free retreat responsibility remains preferred.");
			Assert.That(StealthTankSquadPolicy.RetreatRetryRouteDecision(
				false, true), Is.EqualTo(SpecialistRetreatRetryRouteDecision.SameAwayCellAlternate),
				"An unreachable endpoint may change only within its required away strategic cell.");
			Assert.That(StealthTankSquadPolicy.RetreatRetryRouteDecision(
				false, false), Is.EqualTo(SpecialistRetreatRetryRouteDecision.None),
				"No identical failed direct Move is periodically reissued when no exact route exists.");
			Assert.That(StealthTankSquadPolicy.FailedMemberRouteRemainsApplicable(
				true, true, true), Is.True,
				"The #821/#983/#987 zero-progress signature survives unchanged retry scans.");
			Assert.That(StealthTankSquadPolicy.ShouldRecomputeSameEndpointMemberRoute(true), Is.False,
				"A literal identical failed retreat route is not recomputed or reissued.");
			Assert.That(StealthTankSquadPolicy.FailedMemberRouteRemainsApplicable(
				true, true, false), Is.False,
				"Movement changes the signature and permits one new exact retry.");
			Assert.That(StealthTankSquadPolicy.CanRetryRetreat(10425, 9650, 75), Is.True,
				"Unrelated group orders cannot postpone the pending member's retreat maintenance.");
			Assert.That(StealthTankSquadPolicy.CanRetryRetreat(9724, 9650, 75), Is.False);
			Assert.That(StealthTankSquadPolicy.CanRetryRetreat(9725, 9650, 75), Is.True,
				"A member becoming idle waits at most one dedicated retreat interval.");
		}

		[Test]
		public void UnavailableRetreatCellUsesBoundedDirectionalProgressWithoutSearchChurn()
		{
			var current = new CPos(91, 27);
			var requiredDestination = new CPos(87, 39);
			var staged = new CPos(90, 29);
			var crossedCell = new CPos(87, 36);
			var backward = new CPos(94, 25);

			Assert.That(StealthTankSquadPolicy.RetreatProgressProjection(
				current, requiredDestination, staged), Is.GreaterThan(0));
			Assert.That(StealthTankSquadPolicy.RetreatProgressProjection(
				current, requiredDestination, crossedCell), Is.GreaterThan(
					StealthTankSquadPolicy.RetreatProgressProjection(
						current, requiredDestination, staged)));
			Assert.That(StealthTankSquadPolicy.RetreatProgressProjection(
				current, requiredDestination, backward), Is.LessThan(0));
			Assert.That(StealthTankSquadPolicy.RetreatRetryRouteDecision(
				false, false, true), Is.EqualTo(
					SpecialistRetreatRetryRouteDecision.DirectionalProgress),
				"A hard-safe exact route makes staged progress when the original away cell is unavailable.");

			Assert.That(StealthTankSquadPolicy.ShouldRetryUnavailableRetreatSearch(
				true, true, true, true, true), Is.False,
				"An identical null candidate set is memoized instead of rerunning bounded A* or orders.");
			Assert.That(StealthTankSquadPolicy.ShouldRetryUnavailableRetreatSearch(
				true, true, false, true, true), Is.True, "Literal member movement permits retry.");
			Assert.That(StealthTankSquadPolicy.ShouldRetryUnavailableRetreatSearch(
				false, true, true, true, true), Is.True, "Target identity change permits retry.");
			Assert.That(StealthTankSquadPolicy.ShouldRetryUnavailableRetreatSearch(
				true, true, true, false, true), Is.True, "Responsibility change permits retry.");
			Assert.That(StealthTankSquadPolicy.ShouldRetryUnavailableRetreatSearch(
				true, true, true, true, false), Is.True, "Threat/resource context change permits retry.");

			var evidence = StealthTankSquadPolicy.RetreatRetryTelemetry(
				current, crossedCell, requiredDestination, 6, true,
				false, false, true, true, true);
			Assert.That(evidence, Does.Contain("start=91,27 current=91,27"));
			Assert.That(evidence, Does.Contain("required-cell=14,6"));
			Assert.That(evidence, Does.Contain("required-bounds=84-89,36-41"));
			Assert.That(evidence, Does.Contain("selected-cell=14,6 exact-route=True"));
			Assert.That(evidence, Does.Contain("endpoint-hard-threat=False endpoint-resource=False"));
			Assert.That(evidence, Does.Contain("endpoint-detector-safe=True domain-passable=True"));
			Assert.That(evidence, Does.Contain("directional-projection=13 strategic-displacement=2"));
			Assert.That(evidence, Does.Contain("responsibility=retained-until-arrival completed=false"));
		}

		[Test]
		public void ExactMemberRouteChecksSubmittedWaypointsWithoutInventingASecondThreatVeto()
		{
			Assert.That(StealthTankSquadPolicy.SubmittedGroundWaypointIsUsable(
				false, true, true), Is.False,
				"A hard-threat or pending-resource submitted waypoint remains rejected.");
			Assert.That(StealthTankSquadPolicy.SubmittedGroundWaypointIsUsable(
				true, false, true), Is.False,
				"A safe submitted waypoint still needs an exact locomotor path.");
			Assert.That(StealthTankSquadPolicy.SubmittedGroundWaypointIsUsable(
				true, true, false), Is.True,
				"Private engine refinement cells do not override the established coarse waypoint policy.");
		}

		[Test]
		public void TargetChangedPreservesBusyMoveUntilIdleHandoff()
		{
			var members = new[]
			{
				new KeyValuePair<int, bool>(872, false),
				new KeyValuePair<int, bool>(967, false)
			};
			var initiallyApplied = StealthTankSquadPolicy.TargetChangedPlanMembers(
				members, member => member.Value, true);

			Assert.That(initiallyApplied, Is.Empty,
				"TargetChanged records the new mission without replacing either busy Move root.");

			var afterCompletion = members.Select(member =>
				new KeyValuePair<int, bool>(member.Key, true));
			var handedOff = StealthTankSquadPolicy.LostActivityPlanMembers(
				afterCompletion, member => member.Value);

			Assert.That(handedOff.Select(member => member.Key), Is.EqualTo(new[] { 872, 967 }),
				"Each actor receives the pending mission exactly once after its old activity completes.");
			Assert.That(StealthTankSquadPolicy.CanApplyPendingTargetPlan(11250, 11250), Is.False,
				"A second scanner in the same tick cannot duplicate a just-queued handoff.");
			Assert.That(StealthTankSquadPolicy.CanApplyPendingTargetPlan(11251, 11250), Is.True);
			Assert.That(StealthTankSquadPolicy.ShouldRetainWholeGroupEngagement(true, true), Is.False,
				"Busy squadmates keep their Attack roots while the idle member receives its pending mission.");
			Assert.That(StealthTankSquadPolicy.ShouldRetainWholeGroupEngagement(true, false), Is.True);
		}

		[Test]
		public void NearbyAndStrategicTargetChangesSubmitAtMostOncePerWorldTick()
		{
			var idle = new[] { 799 };
			var pendingTarget = "nearby-harvester";
			var nearbyApplied = StealthTankSquadPolicy.TargetChangedPlanMembers(
				idle, _ => true, StealthTankSquadPolicy.CanApplyPendingTargetPlan(8375, 8300));
			pendingTarget = "strategic-refinery";
			var strategicApplied = StealthTankSquadPolicy.TargetChangedPlanMembers(
				idle, _ => true, StealthTankSquadPolicy.CanApplyPendingTargetPlan(8375, 8375));

			Assert.That(nearbyApplied, Is.EqualTo(new[] { 799 }),
				"The first producer submits the idle actor's selected target.");
			Assert.That(strategicApplied, Is.Empty,
				"A different target selected by the second producer remains pending instead of replacing the same-tick batch.");
			Assert.That(pendingTarget, Is.EqualTo("strategic-refinery"),
				"The latest selected target remains the retained plan for the next idle handoff.");
		}

		[Test]
		public void SafetyBufferDoesNotInventAThreatCapability()
		{
			Assert.That(StealthTankSquadPolicy.BufferedRange(0, 2), Is.Zero);
			Assert.That(StealthTankSquadPolicy.BufferedRange(5, 2), Is.EqualTo(7));
		}

		[TestCase(true, 0, 3, 8, true)]
		[TestCase(true, 0, 8, 8, false)]
		[TestCase(false, 0, 3, 8, false)]
		[TestCase(true, 5, 3, 8, false)]
		public void OnlyUnarmedPrimaryTargetDetectorMayBeOutranged(bool threatIsTarget,
			int weaponRange, int detectorRange, int ownRange, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.CanOutrangeTargetDetector(threatIsTarget,
				weaponRange, detectorRange, ownRange), Is.EqualTo(expected));
		}

		[TestCase(true, false, false, false, Description = "A lone unarmed detector cannot punish revealed fire.")]
		[TestCase(true, true, false, true, Description = "A separate detector and armed support cover the firing cell.")]
		[TestCase(true, true, true, true, Description = "One armed detector supplies both capabilities.")]
		[TestCase(true, false, false, false, Description = "Removing the shooter immediately leaves detector-only coverage.")]
		[TestCase(true, false, false, false, Description = "An ignored weapon is filtered before it can support a detector.")]
		[TestCase(false, true, true, true, Description = "An already-engaged weapon remains an immediate threat without a detector.")]
		public void EngagementSafetyRequiresArmedPunishmentForDetectorExposure(bool detectorExposure,
			bool armedCoverage, bool engagedWeaponExposure, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.IsEngagementThreat(detectorExposure,
				armedCoverage, engagedWeaponExposure), Is.EqualTo(expected));
		}

		[TestCase(true, true, false, false, true)]
		[TestCase(true, true, true, false, false)]
		[TestCase(true, true, false, true, false)]
		[TestCase(true, false, false, false, false)]
		[TestCase(false, true, false, false, false)]
		public void StrategicApproachScanCannotTurnDetectorAloneIntoAnActiveEngagementVeto(
			bool hasValidTarget, bool isEngaged, bool localThreatExposure, bool resourceHazard, bool expected)
		{
			Assert.That(StealthTankSquadPolicy.ShouldRetainActiveEngagement(hasValidTarget,
				isEngaged, localThreatExposure, resourceHazard), Is.EqualTo(expected));
		}

		[TestCase(0, 7, false, false, 0)]
		[TestCase(0, 7, true, false, 7)]
		[TestCase(5, 7, false, false, 5)]
		[TestCase(5, 7, true, false, 7)]
		[TestCase(5, 7, true, true, 5)]
		public void TransitOnlyTreatsEngagedWeaponsAsCrossfire(int detectorRange, int weaponRange,
			bool weaponIsEngaged, bool canKiteTarget, int expected)
		{
			Assert.That(StealthTankSquadPolicy.TransitThreatRange(detectorRange, weaponRange,
				weaponIsEngaged, canKiteTarget), Is.EqualTo(expected));
		}

		[Test]
		public void StationaryWatchdogDoesNotExemptAttackActivityWithoutWeaponDischarge()
		{
			var exemption = StealthTankSquadPolicy.StationaryWatchdogExemption(false, false);
			var age = 0;
			for (var tick = 0; tick < 750; tick++)
				age = StealthTankSquadPolicy.NextStationaryWatchdogAge(age, false, exemption);

			Assert.That(exemption, Is.EqualTo(BotStationaryWatchdogExemption.None));
			Assert.That(StealthTankSquadPolicy.StationaryWatchdogFailed(age, 750), Is.True,
				"Target assignment, aiming, Attack activity and weapon cooldown are not discharge evidence.");
		}

		[Test]
		public void StationaryWatchdogOnlyRecordsDischargeFromExactRootAttackActivity()
		{
			var attack = (OpenRA.Activities.Activity)System.Runtime.Serialization.FormatterServices
				.GetUninitializedObject(typeof(OpenRA.Mods.Common.Activities.Attack));

			Assert.That(BotOwnedStationaryWatchdog.IsExactRootAttackActivity(attack), Is.True);
			Assert.That(BotOwnedStationaryWatchdog.IsExactRootAttackActivity(
				new NonAttackDischargeActivity()), Is.False,
				"A real discharge from a non-Attack root activity must remain nonexempt.");
			Assert.That(BotOwnedStationaryWatchdog.IsExactRootAttackActivity(null), Is.False);
		}

		[Test]
		public void StationaryWatchdogDoesNotExemptRepairOrderOrTravelBeforeActiveResupply()
		{
			var exemption = StealthTankSquadPolicy.StationaryWatchdogExemption(false, false);
			Assert.That(StealthTankSquadPolicy.NextStationaryWatchdogAge(749, false, exemption),
				Is.EqualTo(750), "A queued repair order, travel or depot wait is not active repair.");
			Assert.That(StealthTankSquadPolicy.ObservedRepairAmount(400, 400), Is.Zero);
			Assert.That(StealthTankSquadPolicy.ObservedRepairAmount(400, 399), Is.Zero);
			Assert.That(StealthTankSquadPolicy.ObservedRepairAmount(400, 405), Is.EqualTo(5),
				"The repair exemption and its evidence require an observed positive HP delta.");
		}

		[Test]
		public void StationaryWatchdogPausesForSustainedShotConfirmedFiringAndActiveRepair()
		{
			var age = 400;
			var cadence = StealthTankSquadPolicy.FiringEpisodeCadenceTicks(70, new[] { 10 }, 2);
			var lastShot = 0;
			for (var tick = 0; tick < 800; tick++)
			{
				if (tick % 80 == 0)
					lastShot = tick;
				var sustained = StealthTankSquadPolicy.IsSustainedFiringEpisode(
					lastShot, tick, cadence, true, true, true);
				age = StealthTankSquadPolicy.NextStationaryWatchdogAge(age, false,
					StealthTankSquadPolicy.StationaryWatchdogExemption(sustained, false));
			}

			var repairing = StealthTankSquadPolicy.StationaryWatchdogExemption(false, true);
			for (var tick = 0; tick < 200; tick++)
				age = StealthTankSquadPolicy.NextStationaryWatchdogAge(age, false, repairing);

			Assert.That(age, Is.EqualTo(400));
			Assert.That(StealthTankSquadPolicy.NextStationaryWatchdogAge(age, true,
				BotStationaryWatchdogExemption.None), Is.Zero);
			Assert.That(cadence, Is.EqualTo(82),
				"The sustained episode tolerance is bounded by the declared weapon cycle.");
		}

		[Test]
		public void StationaryWatchdogStaleSingleShotResumesAndEventuallyFails()
		{
			var cadence = StealthTankSquadPolicy.FiringEpisodeCadenceTicks(70, new[] { 10 }, 2);
			var age = 0;
			for (var tick = 0; tick <= cadence + 750; tick++)
			{
				var sustained = StealthTankSquadPolicy.IsSustainedFiringEpisode(
					0, tick, cadence, true, true, true);
				age = StealthTankSquadPolicy.NextStationaryWatchdogAge(age, false,
					StealthTankSquadPolicy.StationaryWatchdogExemption(sustained, false));
			}

			Assert.That(StealthTankSquadPolicy.StationaryWatchdogFailed(age, 750), Is.True,
				"One old discharge must not exempt a stale Attack indefinitely.");
		}

		[TestCase(false, true, true)]
		[TestCase(true, false, true)]
		[TestCase(true, true, false)]
		public void StationaryWatchdogTargetActivityOrValidityChangeEndsFiringEpisode(
			bool sameTarget, bool sameActivity, bool targetValid)
		{
			Assert.That(StealthTankSquadPolicy.IsSustainedFiringEpisode(
				100, 101, 82, sameTarget, sameActivity, targetValid), Is.False);
		}
	}
}
