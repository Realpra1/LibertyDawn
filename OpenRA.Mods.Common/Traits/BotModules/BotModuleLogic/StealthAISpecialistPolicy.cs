#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable SA1507

namespace OpenRA.Mods.Common.Traits
{
	public enum StealthTankSquadRole { Harass, Attack }
	public enum SpecialistDefenderClearAction { None, CrushInfantry, SnipeTank, AttackUnarmedDetector }
	public enum SpecialistLostActivityRouteDecision { None, RetainShared, SameEndpointMemberRoute, AlternateEndpoint }
	public enum SpecialistRepairDisposition { Active, Repair, Rejoin }
	public enum StealthTankPlanInvalidation
	{
		None,
		TargetChanged,
		MembershipChanged,
		TargetMoved,
		RouteUnsafe,
		LostActivity,
		NoProgress
	}

	public enum StealthTankTargetReassessment
	{
		RetainIncumbent,
		SwitchToChallenger,
		Abandon
	}

	public enum BotStationaryWatchdogExemption
	{
		None,
		Firing,
		Repairing
	}

	public sealed class StealthTankReinforcementSaveGroup
	{
		public int GroupIndex;
		public uint[] Members;
		public KeyValuePair<uint, uint>[] PlanTargets = Array.Empty<KeyValuePair<uint, uint>>();
		public uint[] SafeHolds = Array.Empty<uint>();
	}

	public static class StealthAISpecialistPolicy
	{
		public const int MaximumSquadCount = 4;
		public const int RequiredStrategicCellSize = 6;
		public const int NearbyReactionMaximumLatencyTicks = 25;
		public const float HardRouteDangerThreshold = 1f;
		public const float SoftResourceRouteCost = 0.05f;
		public const float OrdinaryWeaponRouteInfluence = 0.2f;
		public const float HardDetectorRouteInfluence = 1f;
		public const int ReinforcementSaveVersion = 2;









		public static MiniYamlNode SaveReinforcementState(
			IEnumerable<StealthTankReinforcementSaveGroup> groups)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", FieldSaver.FormatValue(ReinforcementSaveVersion))
			};
			nodes.AddRange(groups.OrderBy(g => g.GroupIndex).Select(group =>
				new MiniYamlNode("Group", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("Index", FieldSaver.FormatValue(group.GroupIndex)),
					new MiniYamlNode("Members", FieldSaver.FormatValue(group.Members.OrderBy(id => id).ToArray())),
					new MiniYamlNode("PlanTargets", "", group.PlanTargets.OrderBy(pair => pair.Key)
						.Select(pair => new MiniYamlNode("Plan", "", new List<MiniYamlNode>
						{
							new MiniYamlNode("Member", FieldSaver.FormatValue(pair.Key)),
							new MiniYamlNode("Target", FieldSaver.FormatValue(pair.Value))
						})).ToList()),
					new MiniYamlNode("SafeHolds", FieldSaver.FormatValue(group.SafeHolds.OrderBy(id => id).ToArray()))
				})));
			return new MiniYamlNode("StealthTankReinforcementState", "", nodes);
		}

		public static bool TryLoadReinforcementState(MiniYamlNode state,
			out StealthTankReinforcementSaveGroup[] groups)
		{
			groups = Array.Empty<StealthTankReinforcementSaveGroup>();
			if (state == null)
				return false;

			try
			{
				var version = state.Value.Nodes.Single(n => n.Key == "Version");
				var loadedVersion = FieldLoader.GetValue<int>(version.Key, version.Value.Value);
				if (loadedVersion != 1 && loadedVersion != ReinforcementSaveVersion)
					return false;

				var loaded = state.Value.Nodes.Where(n => n.Key == "Group").Select(groupNode =>
				{
					var indexNode = groupNode.Value.Nodes.Single(n => n.Key == "Index");
					var membersNode = groupNode.Value.Nodes.Single(n => n.Key == "Members");
					var targetsNode = groupNode.Value.Nodes.FirstOrDefault(n => n.Key == "PlanTargets");
					var holdsNode = groupNode.Value.Nodes.FirstOrDefault(n => n.Key == "SafeHolds");
					if (loadedVersion >= 2 && (targetsNode == null || holdsNode == null))
						throw new InvalidOperationException();

					return new StealthTankReinforcementSaveGroup
					{
						GroupIndex = FieldLoader.GetValue<int>(indexNode.Key, indexNode.Value.Value),
						Members = FieldLoader.GetValue<uint[]>(membersNode.Key, membersNode.Value.Value),
						PlanTargets = loadedVersion >= 2 && targetsNode != null ?
							targetsNode.Value.Nodes.Where(n => n.Key == "Plan").Select(plan =>
							{
								var memberNode = plan.Value.Nodes.Single(n => n.Key == "Member");
								var targetNode = plan.Value.Nodes.Single(n => n.Key == "Target");
								return new KeyValuePair<uint, uint>(
									FieldLoader.GetValue<uint>(memberNode.Key, memberNode.Value.Value),
									FieldLoader.GetValue<uint>(targetNode.Key, targetNode.Value.Value));
							}).ToArray() :
							Array.Empty<KeyValuePair<uint, uint>>(),
						SafeHolds = loadedVersion >= 2 && holdsNode != null ?
							FieldLoader.GetValue<uint[]>(holdsNode.Key, holdsNode.Value.Value) : Array.Empty<uint>()
					};
				}).ToArray();
				if (loaded.Any(g => g.GroupIndex < 0 || g.Members.Length == 0 ||
					g.Members.Distinct().Count() != g.Members.Length ||
					g.PlanTargets.Select(pair => pair.Key).Distinct().Count() != g.PlanTargets.Length ||
					g.PlanTargets.Any(pair => !g.Members.Contains(pair.Key)) ||
					g.SafeHolds.Distinct().Count() != g.SafeHolds.Length ||
					g.SafeHolds.Any(id => !g.PlanTargets.Any(pair => pair.Key == id))) ||
					loaded.Select(g => g.GroupIndex).Distinct().Count() != loaded.Length ||
					loaded.SelectMany(g => g.Members).Distinct().Count() != loaded.Sum(g => g.Members.Length))
					return false;

				groups = loaded;
				return true;
			}
			catch (InvalidOperationException) { return false; }
			catch (FormatException) { return false; }
			catch (OverflowException) { return false; }
			catch (YamlException) { return false; }
		}







		public static bool IsSameStrategicCell(CPos a, CPos b, int strategicCellSize)
		{
			return StrategicCell(a, strategicCellSize) == StrategicCell(b, strategicCellSize);
		}

		public static CPos StrategicCell(CPos cell, int strategicCellSize)
		{
			var size = Math.Max(1, strategicCellSize);
			return new CPos(cell.X / size, cell.Y / size);
		}

		public static bool IsSameOrAdjacentStrategicCell(CPos a, CPos b, int strategicCellSize)
		{
			var ac = StrategicCell(a, strategicCellSize);
			var bc = StrategicCell(b, strategicCellSize);
			return Math.Max(Math.Abs(ac.X - bc.X), Math.Abs(ac.Y - bc.Y)) <= 1;
		}

		public static bool ShouldStageReinforcement(bool hasEstablishedCore,
			bool wasPreviouslyAssigned)
		{
			return hasEstablishedCore && !wasPreviouslyAssigned;
		}


		public static bool CanAdvanceReinforcement(bool active)
		{
			return active;
		}

		public static bool ShouldIssueReinforcementOrder(bool retainedPlanMatches,
			bool retainedSafeHold, bool isIdle, bool routeAvailable, bool issuedThisTick)
		{
			if (issuedThisTick)
				return false;

			if (!routeAvailable)
				return !retainedPlanMatches || !retainedSafeHold;

			return !retainedPlanMatches || retainedSafeHold || isIdle;
		}

		public static bool ShouldPreserveBusyReinforcement(bool retainedPlanMatches, bool isIdle)
		{
			return retainedPlanMatches && !isIdle;
		}

		public static bool ShouldRetryFailedReinforcementSearch(bool sameTarget,
			bool sameOrigin, bool sameAnchor, bool sameRouteContext)
		{
			return !(sameTarget && sameOrigin && sameAnchor && sameRouteContext);
		}

		public static bool ShouldRetryFailedMobilitySearch(bool sameOrigin,
			bool sameAnchor, bool sameRouteContext)
		{
			return !(sameOrigin && sameAnchor && sameRouteContext);
		}

		public static bool ShouldIssueSafeMobilityRoute(bool isIdle,
			bool exactSegmentsUsable, bool identicalFailedRoute)
		{
			return isIdle && exactSegmentsUsable && !identicalFailedRoute;
		}

		public static bool ShouldRestoreReinforcementPlan(bool validMember,
			bool validTarget, bool ownsActivity)
		{
			return validMember && validTarget && ownsActivity;
		}

		public static bool ShouldRestoreReinforcementMember(bool eligible,
			bool reserved, bool selected)
		{
			return eligible && reserved && selected;
		}


		public static bool IsHardRouteDanger(float danger)
		{
			return danger >= HardRouteDangerThreshold;
		}


		public static uint? RecoveryCore(IEnumerable<uint> members, ISet<uint> reinforcements)
		{
			var ordered = members.OrderBy(id => id).ToArray();
			return ordered.Length > 0 && ordered.All(reinforcements.Contains) ? ordered[0] : (uint?)null;
		}




		public static bool ShouldReserveUnit(bool alreadyReserved, bool claimAllEligible, bool eligible)
		{
			return alreadyReserved || (claimAllEligible && eligible);
		}

		public static bool ShouldRunStrategicScan(ref int countdown, int interval)
		{
			if (--countdown > 0)
				return false;

			countdown = Math.Max(1, interval);
			return true;
		}

		public static bool ShouldRefreshStrategicView(int cachedTick, int currentTick)
		{
			return cachedTick != currentTick;
		}

		public static bool ShouldRefreshInfluenceMap(int cachedTick, int currentTick, int interval)
		{
			return cachedTick == int.MinValue || currentTick - cachedTick >= Math.Max(1, interval);
		}










		public static List<CPos> ForwardExactGroundRoute(IEnumerable<CPos> reversedPathfinderRoute)
		{
			// IPathFinder.FindUnitPath returns target-to-source. Plans and submitted
			// waypoints use source-to-target, matching the coarse/Air route contract.
			return reversedPathfinderRoute.Reverse().ToList();
		}

		public static bool RouteStretchIsDisproportionate(int selectedDistance,
			int directDistance, int maximumStretchPercent)
		{
			return directDistance > 0 && selectedDistance > 0 && maximumStretchPercent >= 100 &&
				selectedDistance * 100L > directDistance * (long)maximumStretchPercent;
		}





		public static int OptimisticApproachDistance(int targetDistanceCells, int weaponRangeCells)
		{
			return Math.Max(0, targetDistanceCells - Math.Max(0, weaponRangeCells));
		}

		public static StealthTankTargetReassessment ReassessTarget(bool incumbentValid,
			bool incumbentUndefended, long incumbentScore, bool challengerValid,
			bool challengerUndefended, long challengerScore, int minimumImprovementPercent)
		{
			if (!incumbentValid)
				return challengerValid ? StealthTankTargetReassessment.SwitchToChallenger :
					StealthTankTargetReassessment.Abandon;

			return AirThreatGeometry.ShouldSwitchTarget(incumbentUndefended, incumbentScore,
				challengerValid, challengerUndefended, challengerScore, minimumImprovementPercent) ?
				StealthTankTargetReassessment.SwitchToChallenger :
				StealthTankTargetReassessment.RetainIncumbent;
		}

		public static StealthTankTargetReassessment ReassessTargetWithWallFallback(bool incumbentValid,
			bool incumbentUndefended, long incumbentScore, bool challengerValid,
			bool challengerUndefended, long challengerScore, int minimumImprovementPercent,
			bool incumbentIsWall, bool challengerIsWall)
		{
			if (!incumbentValid)
				return challengerValid ? StealthTankTargetReassessment.SwitchToChallenger :
					StealthTankTargetReassessment.Abandon;

			if (!challengerValid)
				return StealthTankTargetReassessment.RetainIncumbent;
			if (incumbentIsWall != challengerIsWall)
				return incumbentIsWall ? StealthTankTargetReassessment.SwitchToChallenger :
					StealthTankTargetReassessment.RetainIncumbent;

			return ReassessTarget(true, incumbentUndefended, incumbentScore,
				true, challengerUndefended, challengerScore, minimumImprovementPercent);
		}

		public static List<T> NearbyReassessmentCandidates<T>(IEnumerable<T> nearbyCandidates,
			T incumbent, Func<T, T, bool> sameCandidate)
		{
			var candidates = nearbyCandidates.ToList();
			if (incumbent != null && !candidates.Any(candidate => sameCandidate(candidate, incumbent)))
				candidates.Add(incumbent);

			return candidates;
		}

		public static int InfantryClusterMultiplier(int nearbyInfantry, int bonusPercentPerActor,
			int maximumMultiplierPercent)
		{
			var multiplier = 100L + Math.Max(0, nearbyInfantry) * (long)Math.Max(0, bonusPercentPerActor);
			return (int)Math.Min(Math.Max(100, maximumMultiplierPercent), multiplier);
		}

		public static bool CanCarefullyClear(int squadValue, int defendingValue, int requiredValueRatio)
		{
			return squadValue > 0 && defendingValue > 0 && requiredValueRatio > 0 &&
				squadValue >= (long)defendingValue * requiredValueRatio;
		}

		public static bool CanAttemptDefenderClear(int consecutiveNoSafeTargetScans, int requiredScans,
			int squadValue, int defendingValue, int requiredValueRatio)
		{
			return consecutiveNoSafeTargetScans >= Math.Max(0, requiredScans) &&
				CanCarefullyClear(squadValue, defendingValue, requiredValueRatio);
		}

		public static bool AreAllCandidatesUnavailable(int candidateCount, int dangerousCandidates,
			int unroutableCandidates)
		{
			return candidateCount > 0 && Math.Max(0, dangerousCandidates) +
				Math.Max(0, unroutableCandidates) >= candidateCount;
		}


		public static SpecialistDefenderClearAction DefenderClearAction(bool isInfantry, bool isTank,
			bool canCrushInfantry, int packageDefenderCount, int ownRangeCells,
			int defenderWeaponRangeCells, int defenderDetectorRangeCells, int safetyMarginCells)
		{
			// The selected infantry is removed from the route influence map, while every
			// other package defender remains authoritative. This permits a patient
			// overmatching squad to crush a reachable edge defender without pretending
			// that the rest of a multi-defender package is safe.
			if (isInfantry && canCrushInfantry && packageDefenderCount > 0 && defenderDetectorRangeCells <= 0)
				return SpecialistDefenderClearAction.CrushInfantry;

			if (isTank && defenderWeaponRangeCells > 0 && defenderDetectorRangeCells <= 0 &&
				ownRangeCells >= defenderWeaponRangeCells + Math.Max(0, safetyMarginCells))
				return SpecialistDefenderClearAction.SnipeTank;

			// A lone detector cannot punish revealed fire. Keep this deliberately narrower
			// than ordinary structure targeting: it is only a fallback blocker capability,
			// and any overlapping armed defender makes packageDefenderCount greater than one.
			if (packageDefenderCount == 1 && defenderWeaponRangeCells <= 0 &&
				defenderDetectorRangeCells > 0 && ownRangeCells > 0)
				return SpecialistDefenderClearAction.AttackUnarmedDetector;

			return SpecialistDefenderClearAction.None;
		}

		public static bool ShouldIgnoreSelectedDefenderInfluence(SpecialistDefenderClearAction action)
		{
			return action != SpecialistDefenderClearAction.None;
		}

		public static SpecialistRepairDisposition RepairDisposition(bool damagedBelowThreshold,
			bool isRepairing, bool fullyRepaired, bool hasCompatibleReachableRepair)
		{
			if (isRepairing && fullyRepaired)
				return SpecialistRepairDisposition.Rejoin;
			if (damagedBelowThreshold && hasCompatibleReachableRepair)
				return SpecialistRepairDisposition.Repair;

			// No compatible reachable facility is never a parking state. A damaged
			// specialist remains owned by, and active in, its combat squad.
			return SpecialistRepairDisposition.Active;
		}

		public static bool ShouldUseNearestSafeMobilityFallback(bool isIdle,
			bool hasAnchorDirectedRoute, bool hasNearestSafeRoute)
		{
			// Match Air's second-stage escape without replacing a busy order. Damage and
			// an unavailable repair facility do not turn an active squad member into a hold.
			return isIdle && !hasAnchorDirectedRoute && hasNearestSafeRoute;
		}

		public static SpecialistLostActivityRouteDecision LostActivityRouteDecision(
			bool sharedRouteUsable, bool sameEndpointMemberRouteUsable,
			bool alternateEndpointRouteUsable)
		{
			if (sharedRouteUsable)
				return SpecialistLostActivityRouteDecision.RetainShared;
			if (sameEndpointMemberRouteUsable)
				return SpecialistLostActivityRouteDecision.SameEndpointMemberRoute;
			if (alternateEndpointRouteUsable)
				return SpecialistLostActivityRouteDecision.AlternateEndpoint;

			return SpecialistLostActivityRouteDecision.None;
		}

		public static bool FailedMemberRouteRemainsApplicable(bool sameTarget,
			bool sameTargetLocation, bool sameOrigin)
		{
			// A physically stuck ground member must not forget a collapsed route merely
			// because the strategic scanner cycles between live targets. Literal actor
			// movement is the authoritative retry boundary; target-specific alternate
			// endpoints remain independently eligible.
			return sameOrigin;
		}

		public static bool ShouldValidateIdleMemberRoute(StealthTankPlanInvalidation invalidation)
		{
			return invalidation == StealthTankPlanInvalidation.TargetChanged ||
				invalidation == StealthTankPlanInvalidation.LostActivity;
		}

		public static bool ShouldRecomputeSameEndpointMemberRoute(
			bool failedRouteMatchesSharedRoute)
		{
			return !failedRouteMatchesSharedRoute;
		}










		public static bool SubmittedGroundWaypointIsUsable(bool waypointIsHardSafe,
			bool exactSegmentReachable, bool internalEngineRefinementIsHardSafe)
		{
			// The cached coarse route owns threat and soft-resource costs at submitted
			// waypoints. Ground pathfinding only proves locomotor reachability between
			// them; re-vetoing its private refinement cells would invent a second route
			// policy and can reject every otherwise valid coarse plan.
			return waypointIsHardSafe && exactSegmentReachable;
		}

		public static T[] LostActivityPlanMembers<T>(IEnumerable<T> activeMembers,
			Func<T, bool> isIdle)
		{
			return activeMembers.Where(isIdle).ToArray();
		}

		public static T[] TargetChangedPlanMembers<T>(IEnumerable<T> activeMembers,
			Func<T, bool> isIdle, bool canSubmitThisTick)
		{
			// Air records the new target immediately but does not replace a formation
			// member's busy activity. LostActivity submits the pending mission when
			// the old activity completes and that member becomes idle. The submission
			// latch is shared by nearby and strategic producers in the same world tick.
			return canSubmitThisTick ? activeMembers.Where(isIdle).ToArray() : Array.Empty<T>();
		}

		public static bool CanApplyPendingTargetPlan(int currentTick, int lastOrderTick)
		{
			return currentTick > lastOrderTick;
		}

		public static bool ShouldRetainWholeGroupEngagement(bool retainActiveEngagement,
			bool hasPendingIdleMember, bool incumbentIsWallFallback = false)
		{
			// Air preserves each busy attacker independently, but still services an
			// idle joiner. A group-wide early return is only valid when no member is
			// waiting for its deferred target-change handoff. A last-resort wall
			// incumbent must also rescan so that a newly available strategic target
			// can replace the mission without replacing any busy actor activity.
			return retainActiveEngagement && !hasPendingIdleMember && !incumbentIsWallFallback;
		}

		public static TInfluence ResolveRepairInfluence<TFacts, TInfluence>(TFacts sharedThreatFacts,
			Func<TFacts, TInfluence> getPrivateInfluence)
			where TFacts : class
			where TInfluence : class
		{
			// Threat facts belong to the elected shared-view owner. Their interpretation and
			// cache belong to the profile that is currently evaluating its repair route.
			return sharedThreatFacts == null ? null : getPrivateInfluence(sharedThreatFacts);
		}

		public static int BufferedRange(int rangeCells, int bufferCells)
		{
			return rangeCells > 0 ? rangeCells + Math.Max(0, bufferCells) : 0;
		}

		public static bool CanOutrangeTargetDetector(bool threatIsTarget, int weaponRangeCells,
			int detectorRangeCells, int ownRangeCells)
		{
			return threatIsTarget && weaponRangeCells <= 0 && detectorRangeCells > 0 &&
				ownRangeCells > detectorRangeCells;
		}

		public static bool IsEngagementThreat(bool detectorExposure, bool armedCoverage,
			bool engagedWeaponExposure)
		{
			// Firing reveals a Stealth Tank, so detection alone cannot punish an engagement.
			// Keep the existing immediate response to a weapon that is already engaged, and
			// otherwise require detector and ground-weapon coverage to overlap the firing cell.
			return engagedWeaponExposure || (detectorExposure && armedCoverage);
		}

		public static bool ShouldRetainActiveEngagement(bool hasValidTarget, bool isEngaged,
			bool localThreatExposure, bool resourceHazard)
		{
			return hasValidTarget && isEngaged && !localThreatExposure && !resourceHazard;
		}

		public static int TransitThreatRange(int detectorRangeCells, int weaponRangeCells,
			bool weaponIsEngaged, bool canKiteTarget)
		{
			var weaponRange = weaponIsEngaged && !canKiteTarget ? weaponRangeCells : 0;
			return Math.Max(detectorRangeCells, weaponRange);
		}
	}
}
