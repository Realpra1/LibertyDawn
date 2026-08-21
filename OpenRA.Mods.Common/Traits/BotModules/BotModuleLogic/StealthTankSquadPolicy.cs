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
	public enum SpecialistRepairDisposition { Active, Repair, Rejoin }
	public enum StealthTankPlanInvalidation
	{
		None,
		TargetChanged,
		MembershipChanged,
		TargetMoved,
		RouteUnsafe,
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
	}

	public static class StealthTankSquadPolicy
	{
		public const int MaximumSquadCount = 4;
		public const int RequiredStrategicCellSize = 6;
		public const int NearbyReactionMaximumLatencyTicks = 25;
		public const int RetreatSaveVersion = 1;
		public const int ReinforcementSaveVersion = 1;

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
					new MiniYamlNode("Members", FieldSaver.FormatValue(group.Members.OrderBy(id => id).ToArray()))
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
				if (FieldLoader.GetValue<int>(version.Key, version.Value.Value) != ReinforcementSaveVersion)
					return false;

				var loaded = state.Value.Nodes.Where(n => n.Key == "Group").Select(groupNode =>
				{
					var indexNode = groupNode.Value.Nodes.Single(n => n.Key == "Index");
					var membersNode = groupNode.Value.Nodes.Single(n => n.Key == "Members");
					return new StealthTankReinforcementSaveGroup
					{
						GroupIndex = FieldLoader.GetValue<int>(indexNode.Key, indexNode.Value.Value),
						Members = FieldLoader.GetValue<uint[]>(membersNode.Key, membersNode.Value.Value)
					};
				}).ToArray();
				if (loaded.Any(g => g.GroupIndex < 0 || g.Members.Length == 0 ||
					g.Members.Distinct().Count() != g.Members.Length) ||
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
			int currentTick, int lastProgressTick, int retryInterval)
		{
			if (!hasPlan || targetChanged)
				return StealthTankPlanInvalidation.TargetChanged;
			if (membershipChanged)
				return StealthTankPlanInvalidation.MembershipChanged;
			if (targetMoved)
				return StealthTankPlanInvalidation.TargetMoved;
			if (routeUnsafe)
				return StealthTankPlanInvalidation.RouteUnsafe;
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

		public static StealthTankTargetReassessment ReassessMovedTarget(bool incumbentValid,
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

		public static List<T> BoundCandidatesWithIncumbent<T>(IEnumerable<T> rankedCandidates,
			int maximumCandidates, bool includeIncumbent, Func<T, bool> isIncumbent)
		{
			var bounded = new List<T>(Math.Max(0, maximumCandidates) + (includeIncumbent ? 1 : 0));
			var incumbentFound = false;
			var incumbent = default(T);
			foreach (var candidate in rankedCandidates)
			{
				if (bounded.Count < maximumCandidates)
					bounded.Add(candidate);

				if (includeIncumbent && isIncumbent(candidate))
				{
					incumbentFound = true;
					incumbent = candidate;
				}

				if (bounded.Count >= maximumCandidates && (!includeIncumbent || incumbentFound))
					break;
			}

			if (incumbentFound && !bounded.Any(isIncumbent))
				bounded.Add(incumbent);

			return bounded;
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
			if (isInfantry && canCrushInfantry && packageDefenderCount == 1 && defenderDetectorRangeCells <= 0)
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

		public static bool ShouldResumeSuspendedEngagement(bool wasAlreadySuspended, bool hasValidTarget,
			bool localThreatExposure, bool resourceHazard)
		{
			return wasAlreadySuspended && hasValidTarget && !localThreatExposure && !resourceHazard;
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
