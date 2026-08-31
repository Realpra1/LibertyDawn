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

namespace OpenRA.Mods.Common.Traits
{
	public enum StealthAIDefendedAirAction { Reject, Sneak, ClearAa }
	public enum StealthAILocalAaClearResponse { Flee, Continue, Recalculate }

	/// <summary>
	/// The world-independent half of the bot's air threat avoidance: how far a remembered anti-air
	/// position is from a flight path, which retreat point sits furthest from danger, and how a
	/// candidate air target scores. Kept free of World and Actor so it can be unit tested.
	/// Everything here is deterministic - no randomness and no iteration over
	/// unordered collections. Ties always resolve to the lowest index.
	/// </summary>
	public static class StealthAIThreatGeometry
	{
		/// <summary>
		/// Returns the squared strategic-cell distance to the closest cell already owned by another
		/// squad. No other cells means there is no separation constraint.
		/// </summary>
		public static long MinimumCellSeparationSquared(CPos candidate, IReadOnlyList<CPos> otherCells)
		{
			if (otherCells == null || otherCells.Count == 0)
				return long.MaxValue;

			var minimum = long.MaxValue;
			foreach (var other in otherCells)
			{
				var dx = (long)candidate.X - other.X;
				var dy = (long)candidate.Y - other.Y;
				minimum = Math.Min(minimum, dx * dx + dy * dy);
			}

			return minimum;
		}

		/// <summary>
		/// Applies the lifecycle's ordered strategic-cell filters before any squad-separation tie-break.
		/// Both halves round up so a singleton, and the best member of an odd-sized set, remain eligible.
		/// </summary>
		internal static IReadOnlyList<int> SelectOrderedTargetCellHalf(
			IReadOnlyList<long> strategicValues, IReadOnlyList<double> threatValues,
			IReadOnlyList<double> crossoverValues)
		{
			if (strategicValues == null || threatValues == null || crossoverValues == null)
				throw new ArgumentNullException();
			if (strategicValues.Count != threatValues.Count || strategicValues.Count != crossoverValues.Count)
				throw new ArgumentException("Target-cell ranking inputs must have matching lengths.");

			var strategicCount = (strategicValues.Count + 1) / 2;
			var strategicHalf = Enumerable.Range(0, strategicValues.Count)
				.OrderByDescending(index => strategicValues[index]).ThenBy(index => index)
				.Take(strategicCount).ToList();
			var threatCount = (strategicHalf.Count + 1) / 2;
			return strategicHalf.OrderBy(index => threatValues[index])
				.ThenByDescending(index => crossoverValues[index]).ThenBy(index => index)
				.Take(threatCount).OrderBy(index => index).ToList();
		}

		public static bool IsOutsideWeaponRange(CPos waypoint, CPos target, int weaponRange)
		{
			return weaponRange <= 0 || (waypoint - target).LengthSquared > weaponRange * weaponRange;
		}

		public static List<CPos> BuildDirectSafeFiringRoute(Func<IReadOnlyList<CPos>> coarseRouteBuilder,
			CPos firingCell, CPos target, int weaponRange)
		{
			var coarseWaypoints = coarseRouteBuilder?.Invoke();
			if (coarseWaypoints == null || !IsOutsideWeaponRange(firingCell, target, weaponRange) ||
				coarseWaypoints.Any(cell => !IsOutsideWeaponRange(cell, target, weaponRange)))
				return null;

			var route = new List<CPos>(coarseWaypoints);
			if (route.Count == 0 || route[route.Count - 1] != firingCell)
				route.Add(firingCell);

			return route;
		}

		public sealed class ReachableTargetCell
		{
			public int TargetIndex { get; set; }
			public float RouteCost { get; set; }
			public List<CPos> Route { get; set; }
			public bool IsRequired { get; set; }
		}

		public sealed class ReachableTargetCells
		{
			public List<ReachableTargetCell> Targets { get; set; }
			public int ExpandedCells { get; set; }
		}

		public sealed class ReachableTargetCellSearch
		{
			enum SearchPhase
			{
				InitializeCosts,
				IndexTargets,
				InitializeStart,
				ExpandCell,
				InsertTarget,
				BuildReverseRoute,
				BuildForwardRoute,
				FinalizeTarget,
				ExpandEdge
			}

			readonly float[] danger;
			readonly int width;
			readonly int height;
			readonly IReadOnlyList<CPos> targetCells;
			readonly float dangerCost;
			readonly int maximumResults;
			readonly int requiredIndex;
			readonly Dictionary<int, List<int>> targetsByCell = new Dictionary<int, List<int>>();
			readonly HashSet<int> selectedIndices = new HashSet<int>();
			readonly List<ReachableTargetCell> selected = new List<ReachableTargetCell>();
			readonly List<CPos> reversedRoute = new List<CPos>();
			readonly List<CPos> forwardRoute = new List<CPos>();
			readonly SortedSet<(float Cost, int Index)> open;
			readonly float[] cost;
			readonly int[] previous;
			readonly int start;
			SearchPhase phase;
			int initializationIndex;
			int targetInputIndex;
			int current;
			List<int> currentTargetIndices;
			int currentTargetIndex;
			int pendingTargetIndex;
			bool pendingTargetRequired;
			int routeAt;
			int forwardRouteIndex;
			int direction;
			int regularResults;
			bool requiredFound;

			internal ReachableTargetCellSearch(float[] danger, int width, int height,
				int startX, int startY, IReadOnlyList<CPos> targetCells, float dangerCost,
				int maximumResults, int requiredIndex)
			{
				if (danger == null || width <= 0 || height <= 0 || danger.Length != width * height ||
					startX < 0 || startY < 0 || startX >= width || startY >= height ||
					targetCells == null || maximumResults <= 0)
				{
					Complete = true;
					return;
				}

				this.danger = danger;
				this.width = width;
				this.height = height;
				this.targetCells = targetCells;
				this.dangerCost = dangerCost;
				this.maximumResults = maximumResults;
				this.requiredIndex = requiredIndex;
				start = startY * width + startX;
				cost = new float[danger.Length];
				previous = new int[danger.Length];
				open = new SortedSet<(float Cost, int Index)>(
					Comparer<(float Cost, int Index)>.Create((a, b) =>
					{
						if (a.Cost < b.Cost)
							return -1;
						if (a.Cost > b.Cost)
							return 1;

						return a.Index.CompareTo(b.Index);
					}));
				requiredFound = requiredIndex < 0 || requiredIndex >= targetCells.Count;
				phase = SearchPhase.InitializeCosts;
			}

			public bool Complete { get; private set; }
			public int PrimitiveOperations { get; private set; }
			public int ExpandedCells { get; private set; }
			public ReachableTargetCells Result { get; private set; }

			public int Advance(int maximumOperations)
			{
				if (maximumOperations <= 0)
					throw new ArgumentOutOfRangeException(nameof(maximumOperations));

				var started = PrimitiveOperations;
				while (!Complete && PrimitiveOperations - started < maximumOperations)
					Step();

				return PrimitiveOperations - started;
			}

			void Step()
			{
				PrimitiveOperations++;
				switch (phase)
				{
					case SearchPhase.InitializeCosts:
						cost[initializationIndex] = float.MaxValue;
						previous[initializationIndex] = -1;
						initializationIndex++;
						if (initializationIndex == danger.Length)
							phase = SearchPhase.IndexTargets;
						break;

					case SearchPhase.IndexTargets:
						if (targetInputIndex == targetCells.Count)
						{
							phase = SearchPhase.InitializeStart;
							break;
						}

						var targetCell = targetCells[targetInputIndex];
						if (targetCell.X >= 0 && targetCell.Y >= 0 &&
							targetCell.X < width && targetCell.Y < height)
						{
							var cellIndex = targetCell.Y * width + targetCell.X;
							if (!targetsByCell.TryGetValue(cellIndex, out var targetIndices))
								targetsByCell.Add(cellIndex, targetIndices = new List<int>());
							targetIndices.Add(targetInputIndex);
						}

						targetInputIndex++;
						break;

					case SearchPhase.InitializeStart:
						cost[start] = 0;
						open.Add((0, start));
						phase = SearchPhase.ExpandCell;
						break;

					case SearchPhase.ExpandCell:
						if (open.Count == 0 || (regularResults >= maximumResults && requiredFound))
						{
							CompleteSearch();
							break;
						}

						var nextCell = open.Min;
						open.Remove(nextCell);
						current = nextCell.Index;
						ExpandedCells++;
						targetsByCell.TryGetValue(current, out currentTargetIndices);
						currentTargetIndex = 0;
						phase = SearchPhase.InsertTarget;
						break;

					case SearchPhase.InsertTarget:
						if (currentTargetIndices == null || currentTargetIndex == currentTargetIndices.Count)
						{
							direction = 0;
							phase = SearchPhase.ExpandEdge;
							break;
						}

						pendingTargetIndex = currentTargetIndices[currentTargetIndex++];
						pendingTargetRequired = pendingTargetIndex == requiredIndex;
						if ((!pendingTargetRequired && regularResults >= maximumResults) ||
							!selectedIndices.Add(pendingTargetIndex))
							break;

						reversedRoute.Clear();
						forwardRoute.Clear();
						routeAt = current;
						phase = SearchPhase.BuildReverseRoute;
						break;

					case SearchPhase.BuildReverseRoute:
						if (routeAt == start || routeAt < 0)
						{
							forwardRouteIndex = reversedRoute.Count - 1;
							phase = SearchPhase.BuildForwardRoute;
							break;
						}

						reversedRoute.Add(new CPos(routeAt % width, routeAt / width));
						routeAt = previous[routeAt];
						break;

					case SearchPhase.BuildForwardRoute:
						if (forwardRouteIndex < 0)
						{
							phase = SearchPhase.FinalizeTarget;
							break;
						}

						var routeCell = reversedRoute[forwardRouteIndex--];
						if (StealthAISpecialistPolicy.IsHardRouteDanger(
							danger[routeCell.Y * width + routeCell.X]))
							forwardRouteIndex = -1;
						else
							forwardRoute.Add(routeCell);
						break;

					case SearchPhase.FinalizeTarget:
						selected.Add(new ReachableTargetCell
						{
							TargetIndex = pendingTargetIndex,
							RouteCost = cost[current],
							Route = new List<CPos>(forwardRoute),
							IsRequired = pendingTargetRequired
						});
						if (pendingTargetRequired)
							requiredFound = true;
						else
							regularResults++;
						phase = SearchPhase.InsertTarget;
						break;

					case SearchPhase.ExpandEdge:
						if (direction == 4)
						{
							phase = SearchPhase.ExpandCell;
							break;
						}

						var cx = current % width;
						var cy = current / width;
						var nx = cx + (direction == 0 ? -1 : direction == 1 ? 1 : 0);
						var ny = cy + (direction == 2 ? -1 : direction == 3 ? 1 : 0);
						direction++;
						if (nx < 0 || ny < 0 || nx >= width || ny >= height)
							break;

						var next = ny * width + nx;
						var nextCost = cost[current] + 1 +
							Math.Max(0, danger[next]) * Math.Max(0, dangerCost);
						if (nextCost >= cost[next])
							break;

						if (cost[next] != float.MaxValue)
							open.Remove((cost[next], next));
						cost[next] = nextCost;
						previous[next] = current;
						open.Add((nextCost, next));
						break;
				}
			}

			void CompleteSearch()
			{
				Result = new ReachableTargetCells
				{
					Targets = selected,
					ExpandedCells = ExpandedCells
				};
				Complete = true;
			}
		}

		/// <summary>
		/// True when another damaged aircraft has already selected a facility, including assignments made
		/// earlier in the current bot tick before the engine has processed its reservation order.
		/// </summary>
		public static bool HasOtherRepairAssignment(IReadOnlyDictionary<uint, uint> assignments,
			IReadOnlyCollection<uint> repairingAircraft, IReadOnlyCollection<uint> waitingAircraft,
			uint aircraftId, uint facilityId)
		{
			return assignments != null && repairingAircraft != null && assignments.Any(a =>
				a.Key != aircraftId && a.Value == facilityId && repairingAircraft.Contains(a.Key) &&
				(waitingAircraft == null || !waitingAircraft.Contains(a.Key)));
		}

		/// <summary>
		/// Returns true when <paramref name="aircraftId"/> is the longest-waiting ready aircraft.
		/// Actor id is a deterministic tie-breaker for aircraft discovered on the same tick.
		/// </summary>
		public static bool IsOldestReadyRepairWaiter(IReadOnlyDictionary<uint, int> waitingSince,
			IEnumerable<uint> readyAircraft, uint aircraftId)
		{
			if (waitingSince == null || readyAircraft == null || !waitingSince.ContainsKey(aircraftId))
				return false;

			return readyAircraft
				.Where(waitingSince.ContainsKey)
				.OrderBy(id => waitingSince[id])
				.ThenBy(id => id)
				.FirstOrDefault() == aircraftId;
		}

		/// <summary>
		/// Deterministically selects the deduplicated union of the nearest and highest-utility candidates.
		/// A valid <paramref name="requiredIndex"/> is always included so a moving incumbent cannot fall
		/// outside both bounded sets during a mandatory reassessment.
		/// </summary>
		public static List<int> SelectTargetCandidates(
			IReadOnlyList<long> distances, IReadOnlyList<int> utilities, int closestCount, int highestValueCount,
			int requiredIndex = -1)
		{
			if (distances == null || utilities == null || distances.Count != utilities.Count)
				return null;

			var result = new List<int>();
			var selected = new HashSet<int>();
			foreach (var i in Enumerable.Range(0, distances.Count).OrderBy(i => distances[i]).ThenBy(i => i).Take(closestCount)
				.Concat(Enumerable.Range(0, utilities.Count).OrderByDescending(i => utilities[i]).ThenBy(i => i).Take(highestValueCount)))
				if (selected.Add(i))
					result.Add(i);

			if (requiredIndex >= 0 && requiredIndex < distances.Count && selected.Add(requiredIndex))
				result.Add(requiredIndex);

			return result;
		}

		/// <summary>
		/// Expands one deterministic lowest-cost discovery frontier from the active coarse cell and
		/// returns the first bounded target-bearing cells. Danger ranks discovery cost but never removes
		/// a target cell. Returned approach routes stop before their first hard-danger cell, so discovery
		/// cannot become movement authority. A valid required target remains an extra bounded option.
		/// </summary>
		public static ReachableTargetCells SelectReachableTargetCells(
			float[] danger, int width, int height, int startX, int startY,
			IReadOnlyList<CPos> targetCells, float dangerCost, int maximumResults,
			int requiredIndex = -1)
		{
			var search = StartReachableTargetCellSearch(danger, width, height, startX, startY,
				targetCells, dangerCost, maximumResults, requiredIndex);
			while (!search.Complete)
				search.Advance(int.MaxValue);

			return search.Result;
		}

		public static ReachableTargetCellSearch StartReachableTargetCellSearch(
			float[] danger, int width, int height, int startX, int startY,
			IReadOnlyList<CPos> targetCells, float dangerCost, int maximumResults,
			int requiredIndex = -1)
		{
			return new ReachableTargetCellSearch(danger, width, height, startX, startY,
				targetCells, dangerCost, maximumResults, requiredIndex);
		}

		/// <summary>
		/// Applies a finishing bonus based on the target's remaining health. Full health preserves the
		/// authored priority, while half health doubles it. Zero health is clamped to one hit point so
		/// transient alive-at-zero actors cannot overflow the score, and invalid health data is ignored.
		/// </summary>
		public static int RemainingHealthPriority(int priority, int hp, int maxHp)
		{
			if (priority <= 0 || maxHp <= 0)
				return Math.Max(0, priority);

			var remainingHp = Math.Clamp(hp, 1, maxHp);
			return (int)Math.Min(int.MaxValue, (long)priority * maxHp / remainingHp);
		}

		/// <summary>Maximum whole cells a mobile threat can traverse before an influence cache expires.</summary>
		public static int MobileThreatBufferCells(int speed, int cacheTicks)
		{
			if (speed <= 0 || cacheTicks <= 0)
				return 0;

			return (int)Math.Ceiling(speed * (long)cacheTicks / 1024d);
		}

		/// <summary>
		/// Squared distance (in world units) from <paramref name="p"/> to the segment
		/// <paramref name="a"/>-<paramref name="b"/>, measured on the ground plane.
		/// Aircraft fly in a straight line, so the segment is the flight path.
		/// </summary>
		public static long DistanceSquaredToSegment(WPos p, WPos a, WPos b)
		{
			long apx = p.X - a.X;
			long apy = p.Y - a.Y;
			long abx = b.X - a.X;
			long aby = b.Y - a.Y;

			var abLengthSquared = abx * abx + aby * aby;
			if (abLengthSquared == 0)
				return apx * apx + apy * apy;

			var dot = apx * abx + apy * aby;
			if (dot <= 0)
				return apx * apx + apy * apy;

			if (dot >= abLengthSquared)
			{
				long bpx = p.X - b.X;
				long bpy = p.Y - b.Y;
				return bpx * bpx + bpy * bpy;
			}

			// Project onto the segment. abx/aby are bounded by the map diagonal and dot by its square,
			// so the products below stay well inside long range.
			var closestX = a.X + (int)(abx * dot / abLengthSquared);
			var closestY = a.Y + (int)(aby * dot / abLengthSquared);

			long dx = p.X - closestX;
			long dy = p.Y - closestY;
			return dx * dx + dy * dy;
		}

		/// <summary>
		/// Number of remembered threats that sit within <paramref name="corridorRadius"/> of the flight
		/// path from <paramref name="from"/> to <paramref name="to"/>. Threats within
		/// <paramref name="destinationExclusion"/> of the destination are skipped: the caller already
		/// prices those in through the target's own anti-air penalty, and counting them twice would
		/// make every defended target look doubly lethal.
		/// </summary>
		public static int CountThreatsNearRoute(IReadOnlyList<WPos> threats, WPos from, WPos to, WDist corridorRadius, WDist destinationExclusion)
		{
			if (threats == null || threats.Count == 0 || corridorRadius.Length <= 0)
				return 0;

			var corridorSquared = (long)corridorRadius.Length * corridorRadius.Length;
			var exclusionSquared = (long)destinationExclusion.Length * destinationExclusion.Length;

			var count = 0;
			for (var i = 0; i < threats.Count; i++)
			{
				var t = threats[i];

				long ex = t.X - to.X;
				long ey = t.Y - to.Y;
				if (ex * ex + ey * ey <= exclusionSquared)
					continue;

				if (DistanceSquaredToSegment(t, from, to) <= corridorSquared)
					count++;
			}

			return count;
		}

		/// <summary>
		/// Squared distance from <paramref name="p"/> to the nearest remembered threat, or
		/// <see cref="long.MaxValue"/> when nothing is remembered.
		/// </summary>
		public static long NearestThreatDistanceSquared(WPos p, IReadOnlyList<WPos> threats)
		{
			var nearest = long.MaxValue;
			if (threats == null)
				return nearest;

			for (var i = 0; i < threats.Count; i++)
			{
				long dx = p.X - threats[i].X;
				long dy = p.Y - threats[i].Y;
				var d = dx * dx + dy * dy;
				if (d < nearest)
					nearest = d;
			}

			return nearest;
		}

		/// <summary>
		/// Index of the candidate that sits furthest from the nearest remembered threat, or -1 when
		/// there are no candidates. With no threats remembered the first candidate wins.
		/// </summary>
		public static int SafestCandidate(IReadOnlyList<WPos> candidates, IReadOnlyList<WPos> threats)
		{
			if (candidates == null || candidates.Count == 0)
				return -1;

			var best = 0;
			var bestDistance = NearestThreatDistanceSquared(candidates[0], threats);
			for (var i = 1; i < candidates.Count; i++)
			{
				var d = NearestThreatDistanceSquared(candidates[i], threats);
				if (d > bestDistance)
				{
					bestDistance = d;
					best = i;
				}
			}

			return best;
		}

		/// <summary>
		/// Whether an air squad should break off, given the anti-air it can see around itself.
		/// Note this scales with squad *size*: a big squad tolerates anti-air that a small one runs from.
		/// That is why capping air squads matters for survival and not just for feel - at the stock
		/// multiplier of 8, a twenty-aircraft squad shrugged off two anti-air actors that would each
		/// individually delete an aircraft, while a squad of five breaks off for the first one.
		/// <paramref name="antiAirWeight"/> is a sum of <see cref="AaEffectiveness"/> weights, not a
		/// raw headcount - a Guard Tower and a Rocket Soldier both being "anti-air capable" no longer
		/// means they are equally dangerous.
		/// </summary>
		public static bool ShouldFleeAntiAir(float antiAirWeight, int fleeMultiplier, int squadSize)
		{
			return antiAirWeight > 0 && antiAirWeight * fleeMultiplier > squadSize;
		}

		public static StealthAILocalAaClearResponse PlannedAaClearResponse(
			bool clearsAa, bool selectedTargetInRange, bool allLocalThreatsPlanned)
		{
			if (!clearsAa)
				return StealthAILocalAaClearResponse.Flee;

			return selectedTargetInRange && allLocalThreatsPlanned ?
				StealthAILocalAaClearResponse.Continue : StealthAILocalAaClearResponse.Recalculate;
		}

		public static bool ShouldSwitchTarget(bool currentUndefended, long currentScore,
			bool challengerValid, bool challengerUndefended, long challengerScore, int minimumImprovementPercent)
		{
			if (!challengerValid)
				return false;

			if (currentUndefended != challengerUndefended)
				return challengerUndefended;

			var requiredScore = (decimal)Math.Max(0, currentScore) *
				(100L + Math.Max(0, minimumImprovementPercent));
			return (decimal)Math.Max(0, challengerScore) * 100 >= requiredScore;
		}

		public static bool ShouldRescanStalledTarget(int ticksSinceProgress, int stallTicks, bool hasArmedUnit)
		{
			return hasArmedUnit && ticksSinceProgress >= stallTicks;
		}

		public static bool UseClusterOpportunity(bool hasIncumbent, bool currentCellHasTargets)
		{
			return !hasIncumbent && !currentCellHasTargets;
		}

		public static bool CanAttemptAaClear(int consecutiveNoUndefendedScans, int requiredScans,
			long ammoWeightedSquadValue, float referenceAaThreatWeight,
			double targetCellAaDangerValue, int requiredValueRatio)
		{
			if (consecutiveNoUndefendedScans < Math.Max(0, requiredScans))
				return false;

			if (requiredValueRatio <= 0)
				return true;

			return referenceAaThreatWeight > 0 && targetCellAaDangerValue > 0 &&
				ammoWeightedSquadValue * (double)referenceAaThreatWeight >=
				targetCellAaDangerValue * requiredValueRatio;
		}

		/// <summary>
		/// Chooses the tactical response to a defended opportunity. The comparison deliberately uses
		/// estimated time instead of target score: a quick pass may take a small exposed cell, while an AA
		/// package is worth clearing when doing so is faster than destroying everything it protects.
		/// An AA package that cannot be cleared, and tied estimates, are rejected rather than turning an
		/// uncertain calculation into a charge. An unfinishable victim cell may still justify clearing AA.
		/// </summary>
		public static StealthAIDefendedAirAction ChooseDefendedAction(long cellKillTicks, long protectedKillTicks,
			long aaClearTicks, bool aaClearValueEligible)
		{
			if (cellKillTicks <= 0 || aaClearTicks <= 0 || aaClearTicks == long.MaxValue)
				return StealthAIDefendedAirAction.Reject;

			if (cellKillTicks < aaClearTicks)
				return StealthAIDefendedAirAction.Sneak;
			if (cellKillTicks == aaClearTicks)
				return StealthAIDefendedAirAction.Reject;

			if (aaClearValueEligible && protectedKillTicks > aaClearTicks)
				return StealthAIDefendedAirAction.ClearAa;

			return StealthAIDefendedAirAction.Reject;
		}

		/// <summary>
		/// Value used for the final location comparison. A target-rich cell may attract a fresh scan, but
		/// an ordinary defended victim must not inherit the value of every other actor in that cell. An
		/// eligible AA-clearing target is the exception: destroying it genuinely unlocks the surrounding
		/// opportunity, so that credit remains part of its score even during incumbent reassessments.
		/// </summary>
		public static long AirTargetOpportunityValue(int individualValue, long clusteredValue,
			bool useClusterOpportunity, bool isUndefended, bool clearsAa, int aaUnlockPercent)
		{
			var individual = Math.Max(0, individualValue);
			var clustered = Math.Max(0, clusteredValue);
			if (clearsAa)
				return individual + clustered * Math.Max(0, aaUnlockPercent) / 100;

			return useClusterOpportunity && isUndefended ? clustered : individual;
		}

		/// <summary>
		/// Raises a target priority smoothly from its authored value at the coverage threshold to the
		/// configured maximum at complete AA coverage. Dropping to or below the threshold restores the
		/// authored value immediately.
		/// </summary>
		public static int CoverageAdjustedPriority(int authoredPriority, int coveredTargets, int totalTargets,
			int thresholdPercent, int maximumPriority)
		{
			var authored = Math.Max(0, authoredPriority);
			var maximum = Math.Max(authored, maximumPriority);
			if (totalTargets <= 0 || maximum == authored)
				return authored;

			var threshold = Math.Clamp(thresholdPercent, 0, 100);
			var covered = Math.Clamp(coveredTargets, 0, totalTargets);
			var coveragePoints = (long)covered * 100;
			var thresholdPoints = (long)totalTargets * threshold;
			if (threshold >= 100 || coveragePoints <= thresholdPoints)
				return authored;

			var boostRange = (long)totalTargets * (100 - threshold);
			var boostProgress = coveragePoints - thresholdPoints;
			return (int)Math.Min(int.MaxValue,
				authored + (long)(maximum - authored) * boostProgress / boostRange);
		}

		/// <summary>
		/// Chooses an AA-clearing opportunity by first retaining only the configured number with the
		/// lowest total effectiveness-times-value danger, then selecting the one that unlocks the most
		/// target value. Location score and stable input order break ties.
		/// </summary>
		public static int SelectAaClearCandidate(IReadOnlyList<double> dangerValues,
			IReadOnlyList<long> unlockedValues, IReadOnlyList<int> locationScores, int weakestCount)
		{
			if (dangerValues == null || unlockedValues == null || locationScores == null || weakestCount <= 0 ||
				dangerValues.Count == 0 || dangerValues.Count != unlockedValues.Count ||
				dangerValues.Count != locationScores.Count)
				return -1;

			return Enumerable.Range(0, dangerValues.Count)
				.OrderBy(i => double.IsNaN(dangerValues[i]) ? double.MaxValue : Math.Max(0, dangerValues[i]))
				.ThenBy(i => i)
				.Take(weakestCount)
				.OrderByDescending(i => unlockedValues[i])
				.ThenByDescending(i => locationScores[i])
				.ThenBy(i => i)
				.First();
		}

		/// <summary>
		/// Recovers the strongest complete AA-clearing plan for an incumbent target. This keeps periodic
		/// reassessment comparable with the original selection: the incumbent retains the protected value
		/// that justified clearing it instead of being rescored only from the AA actor's own coarse cell.
		/// </summary>
		public static int SelectAaClearCandidateForTarget(IReadOnlyList<uint> targetIds, uint targetId,
			IReadOnlyList<double> dangerValues, IReadOnlyList<long> unlockedValues,
			IReadOnlyList<int> locationScores)
		{
			if (targetIds == null || dangerValues == null || unlockedValues == null || locationScores == null ||
				targetIds.Count == 0 || targetIds.Count != dangerValues.Count ||
				targetIds.Count != unlockedValues.Count || targetIds.Count != locationScores.Count)
				return -1;

			return Enumerable.Range(0, targetIds.Count)
				.Where(i => targetIds[i] == targetId)
				.OrderByDescending(i => unlockedValues[i])
				.ThenBy(i => double.IsNaN(dangerValues[i]) ? double.MaxValue : Math.Max(0, dangerValues[i]))
				.ThenByDescending(i => locationScores[i])
				.ThenBy(i => i)
				.DefaultIfEmpty(-1)
				.First();
		}

		public static float LocalAirRiskMultiplier(bool inTargetCell, float adaptiveRiskMultiplier)
		{
			return inTargetCell ? Math.Max(1f, adaptiveRiskMultiplier) : 1f;
		}

		/// <summary>
		/// Whether a weapon's real target range should be treated as reaching <paramref name="distanceInCells"/>
		/// away, once padded by <paramref name="buffer"/>. The bot's own scans are flat radii unrelated to
		/// any specific weapon's range; this is the per-defender check applied to candidates the scan already
		/// found, so a long-range SAM is respected further out than a short-range machine gun even though both
		/// were discovered by the same circular scan.
		/// </summary>
		public static bool IsWithinBufferedRange(float distanceInCells, float weaponRangeCells, float buffer)
		{
			return distanceInCells <= weaponRangeCells * buffer;
		}

		/// <summary>Whether two strategic cells are the same or touch along an edge or corner.</summary>
		public static bool IsSameOrAdjacentCoarseCell(CPos a, CPos b)
		{
			return Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1;
		}

		/// <summary>
		/// Whether an aircraft moving at <paramref name="aircraftSpeed"/> can simply outrun a projectile
		/// moving at <paramref name="projectileSpeed"/> (both in WDist/tick, the same scale
		/// <c>AircraftInfo.Speed</c> and <c>MissileInfo.Speed</c>/<c>BulletInfo.Speed</c> already use) - so it
		/// never has to stop to avoid this specific threat, only keep moving through it. A tie is not an
		/// outrun: the projectile catches up eventually.
		/// </summary>
		public static bool CanOutrun(int aircraftSpeed, int projectileSpeed)
		{
			return aircraftSpeed > projectileSpeed;
		}

		/// <summary>
		/// How dangerous a unit's anti-air weapon really is, from 0 (harmless) to 1 (as dangerous as its
		/// own primary/ground weapon, or a dedicated single-purpose AA weapon with nothing to compare
		/// against). Derived from the AA weapon's <c>Inaccuracy</c> and <c>Damage</c> relative to the same
		/// unit's primary weapon, rather than a hardcoded list of "units that are bad at AA" - the recorded
		/// pattern in this mod's own weapon yaml is that a weak secondary AA weapon inherits its unit's
		/// primary weapon and overrides Inaccuracy sharply upward (e.g. 128 to 1800) alongside a damage cut,
		/// so both ratios genuinely carry signal. Floored above zero so a weak AA unit is never treated as
		/// entirely harmless (and never silently vanishes from every threat calculation), capped at 1 so a
		/// weapon that turns out to be more accurate/damaging than its own "primary" doesn't overweight.
		/// </summary>
		public static float AaEffectiveness(int aaInaccuracy, int aaDamage, int primaryInaccuracy, int primaryDamage)
		{
			// No baseline to compare against - either a dedicated single-purpose AA unit (nothing else to
			// judge it by) or malformed data. Either way, treat it as a full, undiscounted threat.
			if (primaryInaccuracy <= 0 || primaryDamage <= 0)
				return 1f;

			// Larger Inaccuracy is worse, so the ratio is baseline-over-actual: 1 when equally accurate,
			// shrinking as the AA weapon scatters more than its own primary weapon does.
			var accuracyRatio = (float)primaryInaccuracy / Math.Max(aaInaccuracy, 1);
			var damageRatio = (float)aaDamage / primaryDamage;

			var effectiveness = (accuracyRatio + damageRatio) / 2f;
			return Math.Clamp(effectiveness, 0.05f, 1f);
		}

		public static float ConfiguredThreatWeight(string actorName, float derivedWeight,
			IReadOnlyDictionary<string, float> overrides)
		{
			return overrides != null && actorName != null && overrides.TryGetValue(actorName, out var configured) ?
				configured : derivedWeight;
		}

		public static float OrcaTransitThreatWeight(float stoppingWeight, bool canOutrun)
		{
			return canOutrun ? stoppingWeight * .5f : stoppingWeight;
		}

		/// <summary>
		/// <paramref name="v"/> rescaled to <paramref name="length"/> world units on the ground plane,
		/// or zero when it has no direction to preserve. Integer only, so the result is within a unit or
		/// two of the requested length - which is fine for "hop roughly this far".
		/// </summary>
		public static WVec ScaleToLength(WVec v, int length)
		{
			var currentSquared = (long)v.X * v.X + (long)v.Y * v.Y;
			if (currentSquared == 0 || length <= 0)
				return WVec.Zero;

			var current = Exts.ISqrt(currentSquared);
			if (current == 0)
				return WVec.Zero;

			return new WVec((int)(v.X * (long)length / current), (int)(v.Y * (long)length / current), 0);
		}

		/// <summary>
		/// Where a threatened air squad should hop to: <paramref name="hopDistance"/> directly away from
		/// the nearest remembered threat, plus <paramref name="jitter"/>. The jitter is what turns a
		/// straight in-out shuttle into a squad that works its way around the outside of a base, and it
		/// is the whole move when nothing is remembered - a random nearby point is a good enough
		/// "try again from somewhere else".
		/// The caller supplies the jitter so this stays deterministic and testable; bot code must draw it
		/// from World.LocalRandom, never World.SharedRandom.
		/// </summary>
		public static WPos EvadeDestination(WPos from, IReadOnlyList<WPos> threats, WDist hopDistance, WVec jitter)
		{
			var away = WVec.Zero;
			var nearestSquared = long.MaxValue;

			if (threats != null)
			{
				for (var i = 0; i < threats.Count; i++)
				{
					long dx = from.X - threats[i].X;
					long dy = from.Y - threats[i].Y;
					var d = dx * dx + dy * dy;

					// d == 0 means we are exactly on top of it and there is no direction to run in.
					if (d == 0 || d >= nearestSquared)
						continue;

					nearestSquared = d;
					away = new WVec((int)dx, (int)dy, 0);
				}
			}

			return from + ScaleToLength(away, hopDistance.Length) + jitter;
		}

		/// <summary>
		/// Score of a candidate air target. Soft mobile targets are meant to win: the class value plus
		/// the defenceless bonus (awarded when the target itself cannot shoot back at aircraft) has to
		/// outweigh the anti-air cover on top of it and along the way there.
		/// </summary>
		public static int TargetScore(int classValue, bool defenceless, int defencelessBonus,
			float antiAirWeight, int antiAirPenalty,
			int routeThreatCount, int routeThreatPenalty,
			int distanceInCells, int distancePenalty)
		{
			var score = classValue;

			if (defenceless)
				score += defencelessBonus;

			score -= (int)Math.Round(antiAirWeight * antiAirPenalty);
			score -= routeThreatCount * routeThreatPenalty;
			score -= distanceInCells * distancePenalty;

			return score;
		}
	}
}
