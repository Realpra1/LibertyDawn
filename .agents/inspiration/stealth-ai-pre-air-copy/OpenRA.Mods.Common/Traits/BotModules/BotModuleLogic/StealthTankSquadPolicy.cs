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

namespace OpenRA.Mods.Common.Traits
{
	public enum StealthTankSquadRole { Harass, Attack }
	public enum SpecialistDefenderClearAction { None, CrushInfantry, SnipeTank, AttackUnarmedDetector }
	public enum SpecialistLostActivityRouteDecision { None, RetainShared, SameEndpointMemberRoute, AlternateEndpoint }
	public enum SpecialistRetreatRetryRouteDecision
	{
		None,
		SameEndpointExactRoute,
		SameAwayCellAlternate,
		DirectionalProgress
	}

	public enum SpecialistRetreatMaintenanceAction
	{
		Pending,
		Completed,
		RetryUnavailable,
		RetryQueued
	}

	public sealed class SpecialistRetreatRetryPlan
	{
		public readonly CPos Endpoint;
		public readonly List<CPos> Route;
		public readonly string Reason;

		public SpecialistRetreatRetryPlan(CPos endpoint, List<CPos> route, string reason)
		{
			Endpoint = endpoint;
			Route = route;
			Reason = reason;
		}
	}

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

	public enum StealthTankRetreatCompletion
	{
		ContinueRetreat,
		ReassessWithIncumbent,
		ReassessWithoutIncumbent
	}

	public enum BotStationaryWatchdogExemption
	{
		None,
		Firing,
		Repairing
	}

	public sealed class StealthTankRetreatSaveGroup
	{
		public int GroupIndex;
		public uint TargetId;
		public KeyValuePair<uint, CPos>[] Destinations;
	}

	public sealed class StealthTankReinforcementSaveGroup
	{
		public int GroupIndex;
		public uint[] Members;
		public KeyValuePair<uint, uint>[] PlanTargets = Array.Empty<KeyValuePair<uint, uint>>();
		public uint[] SafeHolds = Array.Empty<uint>();
	}

	public static class StealthTankSquadPolicy
	{
		public const int MaximumSquadCount = 4;
		public const int RequiredStrategicCellSize = 6;
		public const int NearbyReactionMaximumLatencyTicks = 25;
		public const float HardRouteDangerThreshold = 1f;
		public const float SoftResourceRouteCost = 0.05f;
		public const float OrdinaryWeaponRouteInfluence = 0.2f;
		public const float HardDetectorRouteInfluence = 1f;
		public const int RetreatSaveVersion = 1;
		public const int ReinforcementSaveVersion = 2;

		public static BotStationaryWatchdogExemption StationaryWatchdogExemption(
			bool weaponDischargedThisTick, bool activeRepair)
		{
			if (activeRepair)
				return BotStationaryWatchdogExemption.Repairing;

			return weaponDischargedThisTick ? BotStationaryWatchdogExemption.Firing :
				BotStationaryWatchdogExemption.None;
		}

		public static int ObservedRepairAmount(int previousHealth, int currentHealth)
		{
			return Math.Max(0, currentHealth - previousHealth);
		}

		public static int FiringExemptionTicks(bool burstContinues, int nextFireDelay)
		{
			return burstContinues ? Math.Max(1, nextFireDelay) : 1;
		}

		public static int FiringEpisodeCadenceTicks(int reloadDelay,
			IEnumerable<int> burstDelays, int toleranceTicks)
		{
			if (reloadDelay < 0)
				throw new ArgumentOutOfRangeException(nameof(reloadDelay));
			if (toleranceTicks < 0)
				throw new ArgumentOutOfRangeException(nameof(toleranceTicks));

			return Math.Max(1, reloadDelay + (burstDelays?.Sum() ?? 0) + toleranceTicks);
		}

		public static bool IsSustainedFiringEpisode(int lastDischargeTick, int currentTick,
			int cadenceTicks, bool sameTarget, bool sameAttackActivity, bool targetValid)
		{
			return lastDischargeTick != int.MinValue && cadenceTicks > 0 &&
				currentTick >= lastDischargeTick && currentTick - lastDischargeTick <= cadenceTicks &&
				sameTarget && sameAttackActivity && targetValid;
		}

		public static int NextStationaryWatchdogAge(int currentAge, bool moved,
			BotStationaryWatchdogExemption exemption)
		{
			if (currentAge < 0)
				throw new ArgumentOutOfRangeException(nameof(currentAge));

			if (moved)
				return 0;

			return exemption == BotStationaryWatchdogExemption.None ? currentAge + 1 : currentAge;
		}

		public static bool StationaryWatchdogFailed(int stationaryAge, int maximumStationaryTicks)
		{
			return maximumStationaryTicks > 0 && stationaryAge >= maximumStationaryTicks;
		}

		public static bool ShouldBeginPostMissionRetreat(bool enabled, bool hasTarget,
			bool targetIsValid)
		{
			return enabled && hasTarget && !targetIsValid;
		}

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

		public static MiniYamlNode SaveRetreatState(IEnumerable<StealthTankRetreatSaveGroup> groups)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", FieldSaver.FormatValue(RetreatSaveVersion))
			};
			nodes.AddRange(groups.OrderBy(g => g.GroupIndex).Select(group =>
				new MiniYamlNode("Group", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("Index", FieldSaver.FormatValue(group.GroupIndex)),
					new MiniYamlNode("Target", FieldSaver.FormatValue(group.TargetId)),
					new MiniYamlNode("Destinations", "", group.Destinations.OrderBy(d => d.Key)
						.Select(destination => new MiniYamlNode("Destination", "", new List<MiniYamlNode>
						{
							new MiniYamlNode("Member", FieldSaver.FormatValue(destination.Key)),
							new MiniYamlNode("Cell", FieldSaver.FormatValue(destination.Value))
						})).ToList())
				})));
			return new MiniYamlNode("StealthTankRetreatState", "", nodes);
		}

		public static bool TryLoadRetreatState(MiniYamlNode state,
			out StealthTankRetreatSaveGroup[] groups)
		{
			groups = Array.Empty<StealthTankRetreatSaveGroup>();
			if (state == null)
				return false;

			try
			{
				var version = state.Value.Nodes.Single(n => n.Key == "Version");
				if (FieldLoader.GetValue<int>(version.Key, version.Value.Value) != RetreatSaveVersion)
					return false;

				var loaded = new List<StealthTankRetreatSaveGroup>();
				foreach (var groupNode in state.Value.Nodes.Where(n => n.Key == "Group"))
				{
					var indexNode = groupNode.Value.Nodes.Single(n => n.Key == "Index");
					var targetNode = groupNode.Value.Nodes.Single(n => n.Key == "Target");
					var destinationsNode = groupNode.Value.Nodes.Single(n => n.Key == "Destinations");
					var destinations = destinationsNode.Value.Nodes.Where(n => n.Key == "Destination")
						.Select(destinationNode =>
						{
							var memberNode = destinationNode.Value.Nodes.Single(n => n.Key == "Member");
							var cellNode = destinationNode.Value.Nodes.Single(n => n.Key == "Cell");
							return new KeyValuePair<uint, CPos>(
								FieldLoader.GetValue<uint>(memberNode.Key, memberNode.Value.Value),
								FieldLoader.GetValue<CPos>(cellNode.Key, cellNode.Value.Value));
						}).ToArray();
					var group = new StealthTankRetreatSaveGroup
					{
						GroupIndex = FieldLoader.GetValue<int>(indexNode.Key, indexNode.Value.Value),
						TargetId = FieldLoader.GetValue<uint>(targetNode.Key, targetNode.Value.Value),
						Destinations = destinations
					};
					if (group.GroupIndex < 0 || destinations.Length == 0 ||
						destinations.Select(d => d.Key).Distinct().Count() != destinations.Length)
						return false;

					loaded.Add(group);
				}

				if (loaded.Select(g => g.GroupIndex).Distinct().Count() != loaded.Count)
					return false;

				groups = loaded.ToArray();
				return true;
			}
			catch (InvalidOperationException)
			{
				return false;
			}
			catch (FormatException)
			{
				return false;
			}
			catch (OverflowException)
			{
				return false;
			}
			catch (YamlException)
			{
				return false;
			}
		}

		public static bool ShouldBlockReassessment(int retreatDestinationCount)
		{
			return retreatDestinationCount > 0;
		}

		public static StealthTankRetreatCompletion CompleteRetreat(int retreatDestinationCount,
			bool targetIsValid)
		{
			if (ShouldBlockReassessment(retreatDestinationCount))
				return StealthTankRetreatCompletion.ContinueRetreat;

			return targetIsValid ? StealthTankRetreatCompletion.ReassessWithIncumbent :
				StealthTankRetreatCompletion.ReassessWithoutIncumbent;
		}

		public static bool IsRetreatResponsibilityResolved(bool eligible, bool repairing,
			bool reachedDestination)
		{
			if (!eligible || reachedDestination)
				return true;

			// Repair supersedes the current Move activity, but not its group responsibility.
			if (repairing)
				return false;

			return false;
		}

		public static CPos StrategicCell(CPos cell, int strategicCellSize)
		{
			var size = Math.Max(1, strategicCellSize);
			return new CPos(cell.X / size, cell.Y / size);
		}

		public static bool IsSameStrategicCell(CPos a, CPos b, int strategicCellSize)
		{
			return StrategicCell(a, strategicCellSize) == StrategicCell(b, strategicCellSize);
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

		public static int ReinforcementGroup(int proposedGroup,
			IReadOnlyList<int> establishedCoreCounts)
		{
			if (proposedGroup >= 0 && proposedGroup < establishedCoreCounts.Count &&
				establishedCoreCounts[proposedGroup] > 0)
				return proposedGroup;

			return Enumerable.Range(0, establishedCoreCounts.Count)
				.Where(i => establishedCoreCounts[i] > 0)
				.OrderBy(i => establishedCoreCounts[i]).ThenBy(i => i)
				.DefaultIfEmpty(proposedGroup).First();
		}

		public static bool CanAdvanceReinforcement(bool active, bool retreatResponsibilityPending)
		{
			return active && !retreatResponsibilityPending;
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

		public static bool ShouldEvadeLocalDanger(bool localThreatExposure, bool blueAdjacent)
		{
			return localThreatExposure;
		}

		public static bool IsHardRouteDanger(float danger)
		{
			return danger >= HardRouteDangerThreshold;
		}

		public static bool IsRetreatDestinationSafe(bool passable, bool hasResource,
			bool hardDanger)
		{
			return passable && !hasResource && !hardDanger;
		}

		public static uint? RecoveryCore(IEnumerable<uint> members, ISet<uint> reinforcements)
		{
			var ordered = members.OrderBy(id => id).ToArray();
			return ordered.Length > 0 && ordered.All(reinforcements.Contains) ? ordered[0] : (uint?)null;
		}

		public static CPos OneStrategicCellRetreat(CPos unit, CPos target, int strategicCellSize,
			int mapWidth, int mapHeight)
		{
			var size = Math.Max(1, strategicCellSize);
			var width = Math.Max(1, (mapWidth + size - 1) / size);
			var height = Math.Max(1, (mapHeight + size - 1) / size);
			var start = StrategicCell(unit, size);
			var targetCell = StrategicCell(target, size);
			var rawDx = unit.X - target.X;
			var rawDy = unit.Y - target.Y;
			var preferredX = Math.Sign(start.X - targetCell.X);
			var preferredY = Math.Sign(start.Y - targetCell.Y);
			if (preferredX == 0 && preferredY == 0)
			{
				if (Math.Abs(rawDx) >= Math.Abs(rawDy))
					preferredX = rawDx < 0 ? -1 : 1;
				else
					preferredY = rawDy < 0 ? -1 : 1;
			}

			var candidates = Enumerable.Range(-1, 3)
				.SelectMany(y => Enumerable.Range(-1, 3).Select(x => new CPos(start.X + x, start.Y + y)))
				.Where(c => c != start && c.X >= 0 && c.Y >= 0 && c.X < width && c.Y < height)
				.OrderByDescending(c => (c.X - start.X) * preferredX + (c.Y - start.Y) * preferredY)
				.ThenByDescending(c => (c - targetCell).LengthSquared)
				.ThenBy(c => c.Y).ThenBy(c => c.X).ToArray();
			var destination = candidates.Length > 0 ? candidates[0] : start;
			return new CPos(Math.Min(mapWidth - 1, destination.X * size + size / 2),
				Math.Min(mapHeight - 1, destination.Y * size + size / 2));
		}

		public static bool IsRetreatDestinationAwayFromTarget(CPos unit, CPos destination, CPos target,
			int strategicCellSize, int mapWidth, int mapHeight)
		{
			var expected = OneStrategicCellRetreat(unit, target, strategicCellSize, mapWidth, mapHeight);
			return StrategicCell(destination, strategicCellSize) == StrategicCell(expected, strategicCellSize);
		}

		public static int SquadCount(int maximumHarassmentGroups, bool includeAttackGroup)
		{
			var harassmentGroups = Math.Max(0, maximumHarassmentGroups);
			return includeAttackGroup && harassmentGroups < int.MaxValue ? harassmentGroups + 1 : harassmentGroups;
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

		public static StealthTankPlanInvalidation ClassifyPlanInvalidation(bool hasPlan,
			bool targetChanged, bool membershipChanged, bool targetMoved, bool routeUnsafe,
			bool lostActivity, int currentTick, int lastProgressTick, int retryInterval)
		{
			if (!hasPlan || targetChanged)
				return StealthTankPlanInvalidation.TargetChanged;
			if (membershipChanged)
				return StealthTankPlanInvalidation.MembershipChanged;
			if (targetMoved)
				return StealthTankPlanInvalidation.TargetMoved;
			if (routeUnsafe)
				return StealthTankPlanInvalidation.RouteUnsafe;
			if (lostActivity)
				return StealthTankPlanInvalidation.LostActivity;
			if (currentTick >= lastProgressTick + Math.Max(1, retryInterval))
				return StealthTankPlanInvalidation.NoProgress;

			return StealthTankPlanInvalidation.None;
		}

		public static int SpecialistCount(int total, bool reserveOpeningPair = true)
		{
			return SpecialistCount(total, reserveOpeningPair, 0);
		}

		public static int SpecialistCount(int total, bool reserveOpeningPair, int retainedSpecialists)
		{
			return SpecialistCount(total, reserveOpeningPair, retainedSpecialists, false);
		}

		public static int SpecialistCount(int total, bool reserveOpeningPair, int retainedSpecialists,
			bool claimAllEligible)
		{
			if (claimAllEligible)
				return Math.Max(0, total);

			if (total < 2)
				return total > 0 && retainedSpecialists > 0 ? 1 : 0;
			if (!reserveOpeningPair)
				return (total + 1) / 2;
			if (total < 4)
				return 2;

			return total / 2;
		}

		public static uint[] SelectSpecialistIds(IEnumerable<uint> eligibleIds,
			IEnumerable<uint> previouslyOwnedIds, bool reserveOpeningPair = true,
			bool claimAllEligible = false)
		{
			var eligible = eligibleIds.Distinct().OrderBy(id => id).ToArray();
			var owned = new HashSet<uint>(previouslyOwnedIds);
			var retained = eligible.Count(owned.Contains);
			var desired = SpecialistCount(eligible.Length, reserveOpeningPair, retained, claimAllEligible);
			return eligible.Where(owned.Contains).Concat(eligible.Where(id => !owned.Contains(id)))
				.Take(desired).ToArray();
		}

		public static int GroupForIndex(int index, int specialistCount,
			int maximumHarassmentGroups = 2, bool includeAttackGroup = true)
		{
			if (index < 0 || index >= specialistCount || maximumHarassmentGroups <= 0)
				return -1;
			if (maximumHarassmentGroups == 1 && !includeAttackGroup)
				return 0;

			if (includeAttackGroup && maximumHarassmentGroups == 2 && specialistCount <= 3)
				return 0;
			if (includeAttackGroup && maximumHarassmentGroups == 2 && specialistCount == 4)
				return index < 2 ? 0 : 1;

			// Keep two tanks together for cooperative anti-tank work. The remaining tanks are
			// split between two harassment groups, which naturally grow in large late-game armies.
			var attackCount = includeAttackGroup && specialistCount >= 5 ? 2 : 0;
			var harassmentCount = specialistCount - attackCount;
			if (index >= harassmentCount)
				return maximumHarassmentGroups;

			return Math.Min(maximumHarassmentGroups - 1,
				index * maximumHarassmentGroups / Math.Max(1, harassmentCount));
		}

		public static StealthTankSquadRole RoleForGroup(int group,
			int maximumHarassmentGroups = 2, bool includeAttackGroup = true)
		{
			return includeAttackGroup && group == maximumHarassmentGroups ?
				StealthTankSquadRole.Attack : StealthTankSquadRole.Harass;
		}

		public static long TargetScore(int priority, int economicValue, int distanceCells,
			int currentTargetBonusPercent, int clusterMultiplierPercent = 100, int distancePenalty = 1)
		{
			var score = Math.Max(0, priority) * (long)Math.Max(1, economicValue) /
				Math.Max(1, Math.Max(0, distanceCells) * Math.Max(1, distancePenalty) + 6);
			return score * Math.Max(100, currentTargetBonusPercent) / 100 *
				Math.Max(100, clusterMultiplierPercent) / 100;
		}

		public static int RouteDistanceCells(CPos start, IEnumerable<CPos> route)
		{
			var distance = 0d;
			var previous = start;
			foreach (var cell in route)
			{
				distance += Math.Sqrt((cell - previous).LengthSquared);
				previous = cell;
			}

			return (int)Math.Ceiling(distance);
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

		public static bool IsPostMissionRetreatRouteDistance(int routeDistance,
			int requestedDistance, int tolerance)
		{
			return requestedDistance > 0 && tolerance >= 0 &&
				routeDistance >= Math.Max(1, requestedDistance - tolerance) &&
				routeDistance <= requestedDistance + tolerance;
		}

		public static bool ShouldRejectPostMissionRetreatRouteCell(bool isOrigin,
			bool hasResource, bool hasPendingResourceHazard, bool hasHardInfluence)
		{
			// A retreat commonly begins inside the danger it must escape. Air escape
			// routing permits that occupied origin, while every traversed cell after it
			// must satisfy the ground safety contract.
			return !isOrigin && (hasResource || hasPendingResourceHazard || hasHardInfluence);
		}

		public static bool ShouldBlockTargetReassessment(int retreatResponsibilities,
			bool postMissionRetreat)
		{
			return !postMissionRetreat && ShouldBlockReassessment(retreatResponsibilities);
		}

		public static bool ShouldInvalidateBoundedPostMissionRetreat(bool postMissionRetreat,
			bool eligible, bool idle, bool repairing, bool reachedExactEndpoint)
		{
			return postMissionRetreat && eligible && idle && !repairing && !reachedExactEndpoint;
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

		public static int SelectDefenderClearOpportunity(int[] defendingValues, long[] unlockedScores, int weakestCount)
		{
			if (defendingValues == null || unlockedScores == null || weakestCount <= 0 ||
				defendingValues.Length == 0 || defendingValues.Length != unlockedScores.Length)
				return -1;

			return Enumerable.Range(0, defendingValues.Length)
				.OrderBy(i => Math.Max(0, defendingValues[i]))
				.ThenBy(i => i)
				.Take(weakestCount)
				.OrderByDescending(i => unlockedScores[i])
				.ThenBy(i => i)
				.First();
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

		public static SpecialistRetreatRetryRouteDecision RetreatRetryRouteDecision(
			bool sameEndpointExactRouteUsable, bool sameAwayCellAlternateUsable,
			bool directionalProgressRouteUsable = false)
		{
			if (sameEndpointExactRouteUsable)
				return SpecialistRetreatRetryRouteDecision.SameEndpointExactRoute;
			if (sameAwayCellAlternateUsable)
				return SpecialistRetreatRetryRouteDecision.SameAwayCellAlternate;
			if (directionalProgressRouteUsable)
				return SpecialistRetreatRetryRouteDecision.DirectionalProgress;

			return SpecialistRetreatRetryRouteDecision.None;
		}

		public static int RetreatProgressProjection(CPos current, CPos requiredDestination,
			CPos candidate)
		{
			return (candidate.X - current.X) * Math.Sign(requiredDestination.X - current.X) +
				(candidate.Y - current.Y) * Math.Sign(requiredDestination.Y - current.Y);
		}

		public static bool ShouldRejectImmediateRetreatReverse(bool stagedRouteApplies,
			CPos candidate, CPos stagedOrigin)
		{
			return stagedRouteApplies && candidate == stagedOrigin;
		}

		public static CPos RetreatResponsibilityAfterRetry(CPos requiredDestination,
			CPos selectedEndpoint, int strategicCellSize)
		{
			// A ground-only directional fallback is an intermediate route. It may
			// advance around an unavailable away cell, but cannot replace the
			// original one-cell retreat responsibility until it actually reaches
			// that required strategic cell.
			return IsSameStrategicCell(requiredDestination, selectedEndpoint,
				strategicCellSize) ? selectedEndpoint : requiredDestination;
		}

		public static SpecialistRetreatMaintenanceAction MaintainRetreatResponsibility(
			IDictionary<uint, CPos> responsibilities, uint actorId, CPos? current,
			bool eligible, bool repairing, bool idle, int currentTick,
			int lastRetreatOrderTick, int retryInterval, int strategicCellSize,
			Func<CPos, SpecialistRetreatRetryPlan> findRetryPlan,
			Action<List<CPos>> queueRoute, Action cleanup,
			out CPos requiredDestination, out SpecialistRetreatRetryPlan retryPlan)
		{
			retryPlan = null;
			if (!responsibilities.TryGetValue(actorId, out requiredDestination))
				return SpecialistRetreatMaintenanceAction.Pending;

			if (!eligible)
			{
				responsibilities.Remove(actorId);
				cleanup?.Invoke();
				return SpecialistRetreatMaintenanceAction.Completed;
			}

			var reachedDestination = current.HasValue && IsSameStrategicCell(
				current.Value, requiredDestination, strategicCellSize);
			if (IsRetreatResponsibilityResolved(eligible, repairing, reachedDestination))
			{
				responsibilities.Remove(actorId);
				cleanup?.Invoke();
				return SpecialistRetreatMaintenanceAction.Completed;
			}

			if (!idle || repairing || findRetryPlan == null ||
				!CanRetryRetreat(currentTick, lastRetreatOrderTick, retryInterval))
				return SpecialistRetreatMaintenanceAction.Pending;

			retryPlan = findRetryPlan(requiredDestination);
			if (retryPlan?.Route == null || retryPlan.Route.Count == 0)
				return SpecialistRetreatMaintenanceAction.RetryUnavailable;

			responsibilities[actorId] = RetreatResponsibilityAfterRetry(
				requiredDestination, retryPlan.Endpoint, strategicCellSize);
			queueRoute?.Invoke(retryPlan.Route);
			return SpecialistRetreatMaintenanceAction.RetryQueued;
		}

		public static string RetreatIneligibleCleanupTelemetry(uint actorId, CPos destination)
		{
			return $"unit={actorId} current=none selected-endpoint={destination} " +
				"eligible=false responsibility=completed reason=ineligible-cleanup " +
				"stop=false cancel=false reassess=false";
		}

		public static bool ShouldRetryUnavailableRetreatSearch(bool sameTarget,
			bool sameTargetLocation, bool sameOrigin, bool sameDestination, bool sameRouteContext)
		{
			return !(sameTarget && sameTargetLocation && sameOrigin && sameDestination && sameRouteContext);
		}

		public static string RetreatRetryTelemetry(CPos start, CPos selectedEndpoint,
			CPos requiredDestination, int strategicCellSize, bool exactRoute,
			bool endpointHardThreat, bool endpointResource, bool endpointDetectorSafe,
			bool domainPassable, bool responsibilityRetained)
		{
			var requiredCell = StrategicCell(requiredDestination, strategicCellSize);
			var selectedCell = StrategicCell(selectedEndpoint, strategicCellSize);
			var startCell = StrategicCell(start, strategicCellSize);
			var strategicDisplacement = Math.Max(Math.Abs(selectedCell.X - startCell.X),
				Math.Abs(selectedCell.Y - startCell.Y));
			return string.Format("start={0} current={0} selected-endpoint={1} required-cell={2} " +
				"required-bounds={3}-{4},{5}-{6} selected-cell={7} exact-route={8} " +
				"endpoint-hard-threat={9} endpoint-resource={10} endpoint-detector-safe={11} " +
				"domain-passable={12} directional-projection={13} strategic-displacement={14} " +
				"responsibility={15} completed=false reason=route-issued stop=false cancel=false",
				start, selectedEndpoint, requiredCell,
				requiredCell.X * strategicCellSize,
				requiredCell.X * strategicCellSize + strategicCellSize - 1,
				requiredCell.Y * strategicCellSize,
				requiredCell.Y * strategicCellSize + strategicCellSize - 1,
				selectedCell, exactRoute, endpointHardThreat, endpointResource,
				endpointDetectorSafe, domainPassable,
				RetreatProgressProjection(start, requiredDestination, selectedEndpoint),
				strategicDisplacement, responsibilityRetained ? "retained-until-arrival" : "missing");
		}

		public static bool CanRetryRetreat(int currentTick, int lastRetreatOrderTick,
			int retryInterval)
		{
			return currentTick >= lastRetreatOrderTick + retryInterval;
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
