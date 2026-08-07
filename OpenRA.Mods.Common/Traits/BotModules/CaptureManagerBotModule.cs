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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Manages AI capturing logic.")]
	public class CaptureManagerBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Actor types that can capture other actors (via `Captures`).",
			"Leave this empty to disable capturing.")]
		public readonly HashSet<string> CapturingActorTypes = new HashSet<string>();

		[Desc("Actor types that can be targeted for capturing.",
			"Leave this empty to include all actors.")]
		public readonly HashSet<string> CapturableActorTypes = new HashSet<string>();

		[Desc("Minimum delay (in ticks) between trying to capture with CapturingActorTypes.")]
		public readonly int MinimumCaptureDelay = 375;

		[Desc("Maximum number of options to consider for capturing.",
			"If a value less than 1 is given 1 will be used instead.")]
		public readonly int MaximumCaptureTargetOptions = 10;

		[Desc("Distance in cells at which travel reduces a capture target's economic score by half.")]
		public readonly int CaptureDistanceBias = 10;

		[Desc("Percentage by which a replacement target must outscore an active target before retargeting.")]
		public readonly int CaptureRetargetImprovement = 25;

		[Desc("Maximum building health percentage that one engineer may attempt to capture alone.")]
		public readonly int SoloBuildingCaptureHealth = 50;

		[Desc("Should visibility (Shroud, Fog, Cloak, etc) be considered when searching for capturable targets?")]
		public readonly bool CheckCaptureTargetsForVisibility = true;

		[Desc("Player relationships that capturers should attempt to target.")]
		public readonly PlayerRelationship CapturableRelationships = PlayerRelationship.Enemy | PlayerRelationship.Neutral;

		[Desc("Actor types that may use the existing C4 order against enemy buildings.",
			"Leave this empty to disable demolition orders.")]
		public readonly HashSet<string> DemolitionActorTypes = new HashSet<string>();

		[Desc("Minimum delay (in ticks) between trying to demolish with DemolitionActorTypes.")]
		public readonly int MinimumDemolitionDelay = 375;

		[Desc("Write capture and demolition assignments to debug.log.")]
		public readonly bool DebugLogging = false;

		public override object Create(ActorInitializer init) { return new CaptureManagerBotModule(init.Self, this); }
	}

	public class CaptureManagerBotModule : ConditionalTrait<CaptureManagerBotModuleInfo>, IBotTick, IGameSaveTraitData
	{
		readonly struct CaptureCandidate
		{
			public readonly Actor Actor;
			public readonly int Value;
			public readonly bool IsBuilding;
			public readonly int HitPoints;
			public readonly int MaxHitPoints;

			public CaptureCandidate(Actor actor, int value)
			{
				Actor = actor;
				Value = value;
				IsBuilding = actor.Info.HasTraitInfo<BuildingInfo>();
				var health = actor.TraitOrDefault<IHealth>();
				HitPoints = health?.HP ?? 0;
				MaxHitPoints = health?.MaxHP ?? 0;
			}
		}

		readonly struct SpecialistAssignment
		{
			public readonly Actor Target;
			public readonly int TargetHealth;

			public SpecialistAssignment(Actor target)
			{
				Target = target;
				TargetHealth = target.TraitOrDefault<IHealth>()?.HP ?? 0;
			}

			public SpecialistAssignment(Actor target, int targetHealth)
			{
				Target = target;
				TargetHealth = targetHealth;
			}
		}

		readonly World world;
		readonly Player player;
		readonly Func<Actor, bool> isEnemyUnit;
		readonly Predicate<Actor> unitCannotBeOrderedOrIsIdle;
		readonly int maximumCaptureTargetOptions;
		IBotTransportReservations[] transportReservations;
		DomainIndex domainIndex;
		int minCaptureDelayTicks;
		int minDemolitionDelayTicks;

		// Specialists with active orders and their targets. Remembering the target prevents duplicate assignments
		// and lets the debug log distinguish completed work from an interrupted order.
		readonly Dictionary<Actor, SpecialistAssignment> activeCapturers = new Dictionary<Actor, SpecialistAssignment>();
		readonly Dictionary<Actor, SpecialistAssignment> activeDemolitionUnits = new Dictionary<Actor, SpecialistAssignment>();

		public CaptureManagerBotModule(Actor self, CaptureManagerBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;

			if (world.Type == WorldType.Editor)
				return;

			isEnemyUnit = unit =>
				player.RelationshipWith(unit.Owner) == PlayerRelationship.Enemy
					&& !unit.Info.HasTraitInfo<HuskInfo>()
					&& unit.Info.HasTraitInfo<ITargetableInfo>();

			unitCannotBeOrderedOrIsIdle = a => a.Owner != player || a.IsDead || !a.IsInWorld || a.IsIdle;

			maximumCaptureTargetOptions = Math.Max(1, Info.MaximumCaptureTargetOptions);
		}

		protected override void Created(Actor self)
		{
			domainIndex = world.WorldActor.TraitOrDefault<DomainIndex>();
			transportReservations = self.Owner.PlayerActor.TraitsImplementing<IBotTransportReservations>().ToArray();
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self)
		{
			// Avoid all AIs reevaluating assignments on the same tick, randomize their initial evaluation delay.
			minCaptureDelayTicks = world.LocalRandom.Next(0, Info.MinimumCaptureDelay);
			minDemolitionDelayTicks = world.LocalRandom.Next(0, Info.MinimumDemolitionDelay);
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (--minCaptureDelayTicks <= 0)
			{
				minCaptureDelayTicks = Info.MinimumCaptureDelay;
				QueueCaptureOrders(bot);
			}

			if (--minDemolitionDelayTicks <= 0)
			{
				minDemolitionDelayTicks = Info.MinimumDemolitionDelay;
				QueueDemolitionOrders(bot);
			}
		}

		internal Actor FindClosestEnemy(WPos pos)
		{
			return world.Actors.Where(isEnemyUnit).ClosestTo(pos);
		}

		internal Actor FindClosestEnemy(WPos pos, WDist radius)
		{
			return world.FindActorsInCircle(pos, radius).Where(isEnemyUnit).ClosestTo(pos);
		}

		IEnumerable<Actor> GetVisibleActorsBelongingToPlayer(Player owner)
		{
			foreach (var actor in GetActorsThatCanBeOrderedByPlayer(owner))
				if (actor.CanBeViewedByPlayer(player))
					yield return actor;
		}

		IEnumerable<Actor> GetActorsThatCanBeOrderedByPlayer(Player owner)
		{
			foreach (var actor in world.Actors)
				if (actor.Owner == owner && !actor.IsDead && actor.IsInWorld)
					yield return actor;
		}

		void QueueCaptureOrders(IBot bot)
		{
			if (!Info.CapturingActorTypes.Any() || player.WinState != WinState.Undefined)
				return;

			ReleaseTransportReservations(activeCapturers);
			RetireFinishedAssignments(activeCapturers, "capture");

			var capturers = world.ActorsHavingTrait<IPositionable>()
				.Where(a => a.Owner == player && (a.IsIdle || activeCapturers.ContainsKey(a)) &&
					Info.CapturingActorTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<CapturesInfo>() &&
					!IsReservedForTransport(a))
				.Select(a => new TraitPair<CaptureManager>(a, a.TraitOrDefault<CaptureManager>()))
				.Where(tp => tp.Trait != null)
				.OrderBy(tp => tp.Actor.ActorID)
				.ToArray();

			if (capturers.Length == 0)
				return;

			var targetOptions = world.Players.Where(p => !p.Spectating
					&& Info.CapturableRelationships.HasRelationship(player.RelationshipWith(p)))
				.SelectMany(p => Info.CheckCaptureTargetsForVisibility
					? GetVisibleActorsBelongingToPlayer(p) : GetActorsThatCanBeOrderedByPlayer(p));

			var capturableTargetOptions = targetOptions
				.Where(target =>
				{
					var captureManager = target.TraitOrDefault<CaptureManager>();
					if (captureManager == null)
						return false;

					return capturers.Any(tp => captureManager.CanBeTargetedBy(target, tp.Actor, tp.Trait));
				});

			if (Info.CapturableActorTypes.Any())
				capturableTargetOptions = capturableTargetOptions.Where(target => Info.CapturableActorTypes.Contains(target.Info.Name.ToLowerInvariant()));

			var allCandidates = capturableTargetOptions.Select(target => new CaptureCandidate(target, CaptureEconomicValue(target)))
				.ToArray();
			var candidates = allCandidates
				.OrderByDescending(candidate => capturers.Max(capturer => CaptureScore(capturer.Actor, candidate)))
				.ThenByDescending(candidate => candidate.IsBuilding)
				.ThenBy(candidate => candidate.Actor.ActorID)
				.Take(maximumCaptureTargetOptions).ToArray();

			// The incumbent must remain in the comparison even if a new global top-N list would otherwise omit it.
			candidates = candidates.Concat(allCandidates.Where(candidate => activeCapturers.Values.Any(assignment =>
				assignment.Target == candidate.Actor))).GroupBy(candidate => candidate.Actor.ActorID).Select(group => group.First()).ToArray();
			if (candidates.Length == 0)
				return;

			var reserved = new HashSet<int>();
			var retainedPairActors = new HashSet<Actor>();
			ReserveActiveSoloTargets(candidates, reserved);
			ReassessActivePairs(bot, candidates, capturers, reserved, retainedPairActors);
			ReserveActiveSoloTargets(candidates, reserved);

			var remaining = capturers.Where(capturer => !retainedPairActors.Contains(capturer.Actor)).ToList();
			AssignHealthyBuildingPairs(bot, candidates, remaining, reserved);

			foreach (var capturer in remaining.ToArray())
			{
				var distances = candidates.Select(candidate => DistanceSquared(capturer.Actor, candidate.Actor)).ToArray();
				var scores = candidates.Select(candidate => CanCapture(capturer, candidate.Actor) && !RequiresPair(candidate) ?
					CaptureScore(capturer.Actor, candidate) : -1d).ToArray();
				var unavailable = new HashSet<int>(reserved);
				var incumbentIndex = -1;
				if (activeCapturers.TryGetValue(capturer.Actor, out var incumbent))
				{
					incumbentIndex = Array.FindIndex(candidates, candidate => candidate.Actor == incumbent.Target);
					unavailable.Remove(incumbentIndex);
				}

				var targetIndex = CaptureTargeting.BestTargetIndex(scores,
					candidates.Select(candidate => candidate.IsBuilding).ToArray(), distances, unavailable);
				if (incumbentIndex >= 0 && scores[incumbentIndex] >= 0 &&
					(targetIndex == incumbentIndex || targetIndex < 0 || !CaptureTargeting.ShouldRetarget(
						scores[incumbentIndex], scores[targetIndex], Info.CaptureRetargetImprovement)))
				{
					DebugHighestPriorityRejection(capturer, candidates, unavailable, incumbentIndex);
					reserved.Add(incumbentIndex);
					continue;
				}

				if (targetIndex < 0 || scores[targetIndex] < 0)
				{
					DebugHighestPriorityRejection(capturer, candidates, unavailable, -1);
					if (activeCapturers.Remove(capturer.Actor))
					{
						bot.QueueOrder(new Order("Stop", capturer.Actor, false));
						Debug("capture {0}#{1} stopped: no eligible solo target or healthy-building partner",
							capturer.Actor.Info.Name, capturer.Actor.ActorID);
					}

					continue;
				}

				DebugHighestPriorityRejection(capturer, candidates, unavailable, targetIndex);
				reserved.Add(targetIndex);
				var target = candidates[targetIndex];
				var action = incumbentIndex >= 0 ? "retarget" : "capture";
				var oldTarget = incumbentIndex >= 0 ? candidates[incumbentIndex] : default(CaptureCandidate);

				bot.QueueOrder(new Order("CaptureActor", capturer.Actor, Target.FromActor(target.Actor), false));
				AIUtils.BotDebug("AI ({0}): Ordered {1} to capture {2}", player.ClientIndex, capturer.Actor, target.Actor);
				Debug("{0} {1}#{2} -> {3}#{4}: value={5}, distance-cells={6:0.0}, score={7:0.0}, " +
					"building={8}, health={9}/{10}{11}", action,
					capturer.Actor.Info.Name, capturer.Actor.ActorID, target.Actor.Info.Name, target.Actor.ActorID,
					target.Value, DistanceCells(distances[targetIndex]), scores[targetIndex], target.IsBuilding,
					target.HitPoints, target.MaxHitPoints,
					incumbentIndex >= 0 ? string.Format(", previous={0}#{1}, previous-score={2:0.0}",
						oldTarget.Actor.Info.Name, oldTarget.Actor.ActorID, scores[incumbentIndex]) : string.Empty);
				activeCapturers[capturer.Actor] = new SpecialistAssignment(target.Actor);
			}
		}

		void ReserveActiveSoloTargets(CaptureCandidate[] candidates, HashSet<int> reserved)
		{
			foreach (var group in activeCapturers.GroupBy(entry => entry.Value.Target))
			{
				if (group.Count() != 1)
					continue;

				var targetIndex = Array.FindIndex(candidates, candidate => candidate.Actor == group.Key);
				if (targetIndex >= 0 && !RequiresPair(candidates[targetIndex]))
					reserved.Add(targetIndex);
			}
		}

		void ReassessActivePairs(IBot bot, CaptureCandidate[] candidates,
			TraitPair<CaptureManager>[] capturers, HashSet<int> reserved, HashSet<Actor> retainedActors)
		{
			var capturerByActor = capturers.ToDictionary(capturer => capturer.Actor);
			var groups = activeCapturers.Where(entry => !entry.Key.IsDead && entry.Key.IsInWorld)
				.GroupBy(entry => entry.Value.Target).OrderBy(group => group.Key.ActorID).ToArray();

			foreach (var group in groups)
			{
				var assignments = group.OrderBy(entry => entry.Key.ActorID).ToArray();
				if (assignments.Length < 2)
					continue;

				var targetIndex = Array.FindIndex(candidates, candidate => candidate.Actor == group.Key);
				if (targetIndex < 0)
					continue;

				if (!RequiresPair(candidates[targetIndex]))
				{
					foreach (var surplus in assignments.Skip(1))
					{
						activeCapturers.Remove(surplus.Key);
						Debug("capture pair surplus released {0}#{1}: target={2}#{3}, health={4}/{5}, solo-eligible=true",
							surplus.Key.Info.Name, surplus.Key.ActorID, candidates[targetIndex].Actor.Info.Name,
							candidates[targetIndex].Actor.ActorID, candidates[targetIndex].HitPoints,
							candidates[targetIndex].MaxHitPoints);
					}

					continue;
				}

				var pair = assignments.Select(assignment => capturerByActor.TryGetValue(assignment.Key, out var capturer) ?
					capturer : default(TraitPair<CaptureManager>)).Where(capturer => capturer.Actor != null).Take(2).ToArray();
				if (pair.Length < 2)
					continue;

				foreach (var surplus in assignments.Where(assignment => pair.All(capturer => capturer.Actor != assignment.Key)))
					activeCapturers.Remove(surplus.Key);

				if (pair.Any(capturer => !CanCapture(capturer, candidates[targetIndex].Actor)))
				{
					foreach (var capturer in pair)
					{
						activeCapturers.Remove(capturer.Actor);
						bot.QueueOrder(new Order("Stop", capturer.Actor, false));
					}

					Debug("capture pair dissolved {0}#{1}+{2}#{3} -> {4}#{5}: target invalid or unreachable",
						pair[0].Actor.Info.Name, pair[0].Actor.ActorID, pair[1].Actor.Info.Name,
						pair[1].Actor.ActorID, candidates[targetIndex].Actor.Info.Name, candidates[targetIndex].Actor.ActorID);
					continue;
				}

				var currentScore = CaptureTargeting.PairScore(CaptureScore(pair[0].Actor, candidates[targetIndex]),
					CaptureScore(pair[1].Actor, candidates[targetIndex]));
				var unavailable = new HashSet<int>(reserved) { targetIndex };
				var distinct = CaptureTargeting.BestDistinctTargetAllocation(
					SoloScores(pair[0], candidates), SoloScores(pair[1], candidates), unavailable);
				var alternatePairIndex = BestPairTargetIndex(pair, candidates, unavailable, out var alternatePairScore);
				var replacementScore = Math.Max(distinct.Score, alternatePairScore);

				if (!CaptureTargeting.ShouldRetarget(currentScore, replacementScore, Info.CaptureRetargetImprovement))
				{
					reserved.Add(targetIndex);
					foreach (var capturer in pair)
						retainedActors.Add(capturer.Actor);

					Debug("capture pair retained {0}#{1}+{2}#{3} -> {4}#{5}: current={6:0.0}, " +
						"distinct-solos={7:0.0}, alternate-pair={8:0.0}, margin={9}%",
						pair[0].Actor.Info.Name, pair[0].Actor.ActorID, pair[1].Actor.Info.Name,
						pair[1].Actor.ActorID, candidates[targetIndex].Actor.Info.Name,
						candidates[targetIndex].Actor.ActorID, currentScore, distinct.Score, alternatePairScore,
						Info.CaptureRetargetImprovement);
					continue;
				}

				foreach (var capturer in pair)
					activeCapturers.Remove(capturer.Actor);

				if (alternatePairIndex >= 0 && alternatePairScore > distinct.Score)
				{
					var replacement = candidates[alternatePairIndex];
					reserved.Add(alternatePairIndex);
					foreach (var capturer in pair)
					{
						bot.QueueOrder(new Order("CaptureActor", capturer.Actor, Target.FromActor(replacement.Actor), false));
						activeCapturers[capturer.Actor] = new SpecialistAssignment(replacement.Actor);
						retainedActors.Add(capturer.Actor);
					}

					Debug("capture pair retargeted {0}#{1}+{2}#{3}: {4}#{5} score={6:0.0} -> " +
						"{7}#{8} score={9:0.0}, margin={10}%",
						pair[0].Actor.Info.Name, pair[0].Actor.ActorID, pair[1].Actor.Info.Name,
						pair[1].Actor.ActorID, candidates[targetIndex].Actor.Info.Name,
						candidates[targetIndex].Actor.ActorID, currentScore, replacement.Actor.Info.Name,
						replacement.Actor.ActorID, alternatePairScore, Info.CaptureRetargetImprovement);
					continue;
				}

				var targets = new[] { distinct.FirstTarget, distinct.SecondTarget };
				for (var i = 0; i < pair.Length; i++)
				{
					var capturer = pair[i].Actor;
					var replacementIndex = targets[i];
					if (replacementIndex < 0)
					{
						bot.QueueOrder(new Order("Stop", capturer, false));
						retainedActors.Add(capturer);
						continue;
					}

					var replacement = candidates[replacementIndex];
					reserved.Add(replacementIndex);
					bot.QueueOrder(new Order("CaptureActor", capturer, Target.FromActor(replacement.Actor), false));
					activeCapturers[capturer] = new SpecialistAssignment(replacement.Actor);
					retainedActors.Add(capturer);
				}

				Debug("capture pair dissolved {0}#{1}+{2}#{3} -> distinct solos {4},{5}: " +
					"current={6:0.0}, replacement={7:0.0}, margin={8}%",
					pair[0].Actor.Info.Name, pair[0].Actor.ActorID, pair[1].Actor.Info.Name,
					pair[1].Actor.ActorID, distinct.FirstTarget, distinct.SecondTarget, currentScore,
					distinct.Score, Info.CaptureRetargetImprovement);
			}
		}

		double[] SoloScores(TraitPair<CaptureManager> capturer, CaptureCandidate[] candidates)
		{
			return candidates.Select(candidate => RequiresPair(candidate) || !CanCapture(capturer, candidate.Actor) ?
				-1d : CaptureScore(capturer.Actor, candidate)).ToArray();
		}

		int BestPairTargetIndex(TraitPair<CaptureManager>[] pair, CaptureCandidate[] candidates,
			HashSet<int> unavailable, out double bestScore)
		{
			var best = -1;
			bestScore = -1;
			for (var i = 0; i < candidates.Length; i++)
			{
				if (unavailable.Contains(i) || !RequiresPair(candidates[i]) ||
					pair.Any(capturer => !CanCapture(capturer, candidates[i].Actor)))
					continue;

				var score = CaptureTargeting.PairScore(CaptureScore(pair[0].Actor, candidates[i]),
					CaptureScore(pair[1].Actor, candidates[i]));
				if (score > bestScore)
				{
					best = i;
					bestScore = score;
				}
			}

			return best;
		}

		void AssignHealthyBuildingPairs(IBot bot, CaptureCandidate[] candidates,
			List<TraitPair<CaptureManager>> remaining, HashSet<int> reserved)
		{
			while (true)
			{
				var idle = remaining.Where(capturer => capturer.Actor.IsIdle && !activeCapturers.ContainsKey(capturer.Actor)).ToArray();
				if (idle.Length < 2)
					return;

				var bestTargetIndex = -1;
				TraitPair<CaptureManager>[] bestPair = null;
				var bestPairScore = 0d;
				for (var i = 0; i < candidates.Length; i++)
				{
					if (reserved.Contains(i) || !RequiresPair(candidates[i]))
						continue;

					var pair = idle.Where(capturer => CanCapture(capturer, candidates[i].Actor))
						.OrderByDescending(capturer => CaptureScore(capturer.Actor, candidates[i]))
						.ThenBy(capturer => capturer.Actor.ActorID).Take(2).ToArray();
					if (pair.Length < 2)
						continue;

					var pairScore = CaptureTargeting.PairScore(CaptureScore(pair[0].Actor, candidates[i]),
						CaptureScore(pair[1].Actor, candidates[i]));
					var alternative = CaptureTargeting.BestDistinctTargetAllocation(
						SoloScores(pair[0], candidates), SoloScores(pair[1], candidates), reserved);
					if (pairScore <= alternative.Score || pairScore <= bestPairScore)
						continue;

					bestTargetIndex = i;
					bestPair = pair;
					bestPairScore = pairScore;
				}

				if (bestPair == null)
					return;

				var target = candidates[bestTargetIndex];
				reserved.Add(bestTargetIndex);
				foreach (var capturer in bestPair)
				{
					bot.QueueOrder(new Order("CaptureActor", capturer.Actor, Target.FromActor(target.Actor), false));
					activeCapturers[capturer.Actor] = new SpecialistAssignment(target.Actor);
					remaining.Remove(capturer);
				}

				Debug("capture pair {0}#{1}+{2}#{3} -> {4}#{5}: value={6}, health={7}/{8}, pair-score={9:0.0}",
					bestPair[0].Actor.Info.Name, bestPair[0].Actor.ActorID, bestPair[1].Actor.Info.Name,
					bestPair[1].Actor.ActorID, target.Actor.Info.Name, target.Actor.ActorID, target.Value,
					target.HitPoints, target.MaxHitPoints, bestPairScore);
			}
		}

		bool RequiresPair(CaptureCandidate candidate)
		{
			return CaptureTargeting.RequiresEngineerPair(candidate.IsBuilding, candidate.HitPoints,
				candidate.MaxHitPoints, Info.SoloBuildingCaptureHealth);
		}

		bool CanCapture(TraitPair<CaptureManager> capturer, Actor target)
		{
			return CanCaptureByRules(capturer, target) && HasReachableCaptureApproach(capturer.Actor, target);
		}

		static bool CanCaptureByRules(TraitPair<CaptureManager> capturer, Actor target)
		{
			var captureManager = target.TraitOrDefault<CaptureManager>();
			return captureManager != null && captureManager.CanBeTargetedBy(target, capturer.Actor, capturer.Trait);
		}

		bool HasReachableCaptureApproach(Actor capturer, Actor target)
		{
			var mobile = capturer.TraitOrDefault<Mobile>();
			return mobile == null || domainIndex == null ||
				Util.AdjacentCells(world, Target.FromActor(target)).Any(cell =>
					mobile.CanStayInCell(cell) &&
					mobile.CanEnterCell(cell, check: BlockedByActor.Immovable) &&
					domainIndex.IsPassable(capturer.Location, cell, mobile.Locomotor));
		}

		void DebugHighestPriorityRejection(TraitPair<CaptureManager> capturer,
			CaptureCandidate[] candidates, HashSet<int> unavailable, int selectedIndex)
		{
			if (!Info.DebugLogging)
				return;

			var selectedScore = selectedIndex < 0 ? -1 : CaptureScore(capturer.Actor, candidates[selectedIndex]);
			var rejected = candidates.Select((candidate, index) => new
				{
					Candidate = candidate,
					Index = index,
					Score = CaptureScore(capturer.Actor, candidate)
				})
				.Where(entry => entry.Index != selectedIndex && entry.Score > selectedScore)
				.OrderByDescending(entry => entry.Score).ThenBy(entry => entry.Candidate.Actor.ActorID)
				.Select(entry => new
					{
						entry.Candidate,
						entry.Score,
						Reason = CaptureRejectionReason(capturer, entry.Candidate, entry.Index, unavailable)
					})
				.FirstOrDefault(entry => entry.Reason != null);
			if (rejected == null)
				return;

			Debug("capture {0}#{1} candidate {2}#{3} rejected={4}: value={5}, distance-cells={6:0.0}, " +
				"score={7:0.0}, building={8}, health={9}/{10}", capturer.Actor.Info.Name,
				capturer.Actor.ActorID, rejected.Candidate.Actor.Info.Name, rejected.Candidate.Actor.ActorID,
				rejected.Reason, rejected.Candidate.Value,
				DistanceCells(DistanceSquared(capturer.Actor, rejected.Candidate.Actor)), rejected.Score,
				rejected.Candidate.IsBuilding, rejected.Candidate.HitPoints, rejected.Candidate.MaxHitPoints);
		}

		string CaptureRejectionReason(TraitPair<CaptureManager> capturer, CaptureCandidate candidate,
			int candidateIndex, HashSet<int> unavailable)
		{
			if (unavailable.Contains(candidateIndex))
			{
				var owner = activeCapturers.Where(entry => entry.Value.Target == candidate.Actor)
					.Select(entry => entry.Key).OrderBy(actor => actor.ActorID).FirstOrDefault();
				return owner == null ? "reserved-by-capture" :
					string.Format("reserved-by-capture:{0}#{1}", owner.Info.Name, owner.ActorID);
			}

			if (!CanCaptureByRules(capturer, candidate.Actor))
				return "capture-ineligible";

			if (!HasReachableCaptureApproach(capturer.Actor, candidate.Actor))
				return "unreachable-approach";

			return RequiresPair(candidate) ? "requires-pair" : null;
		}

		double CaptureScore(Actor capturer, CaptureCandidate candidate)
		{
			return CaptureTargeting.Score(candidate.Value, DistanceCells(DistanceSquared(capturer, candidate.Actor)),
				Info.CaptureDistanceBias);
		}

		static long DistanceSquared(Actor first, Actor second)
		{
			return (long)(second.CenterPosition - first.CenterPosition).LengthSquared;
		}

		static double DistanceCells(long distanceSquared)
		{
			return Math.Sqrt(distanceSquared) / 1024d;
		}

		int CaptureEconomicValue(Actor target)
		{
			var transformedValue = 0;
			var transform = target.Info.TraitInfoOrDefault<TransformOnCaptureInfo>();
			if (transform != null && world.Map.Rules.Actors.TryGetValue(transform.IntoActor, out var transformed))
			{
				var customValue = transformed.TraitInfoOrDefault<CustomSellValueInfo>();
				var valued = transformed.TraitInfoOrDefault<ValuedInfo>();
				transformedValue = customValue?.Value ?? valued?.Cost ?? 0;
			}

			return CaptureTargeting.EconomicValue(target.GetSellValue(), transformedValue);
		}

		void QueueDemolitionOrders(IBot bot)
		{
			if (Info.DemolitionActorTypes.Count == 0 || player.WinState != WinState.Undefined)
				return;

			RetireFinishedAssignments(activeDemolitionUnits, "demolition");
			ReleaseTransportReservations(activeDemolitionUnits);
			var activeTargetIds = activeDemolitionUnits.Values.Select(assignment => assignment.Target.ActorID).ToHashSet();
			var demolitionUnits = world.Actors.Where(a => a.Owner == player && a.IsIdle &&
				Info.DemolitionActorTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<DemolitionInfo>() &&
				!activeDemolitionUnits.ContainsKey(a) && !IsReservedForTransport(a)).ToArray();

			foreach (var unit in demolitionUnits.OrderBy(unit => unit.ActorID))
			{
				var target = world.Actors.Where(a => !a.IsDead && a.IsInWorld &&
					player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy &&
					a.Info.HasTraitInfo<BuildingInfo>() &&
					a.TraitsImplementing<IDemolishable>().Any(d => d.IsValidTarget(a, unit)) &&
					(!Info.CheckCaptureTargetsForVisibility || a.CanBeViewedByPlayer(player)) &&
					!activeTargetIds.Contains(a.ActorID))
					.OrderByDescending(a => a.GetSellValue())
					.Take(maximumCaptureTargetOptions)
					.MinByOrDefault(a => (a.CenterPosition - unit.CenterPosition).LengthSquared);
				if (target == null)
					continue;

				bot.QueueOrder(new Order("C4", unit, Target.FromActor(target), false));
				AIUtils.BotDebug("AI ({0}): Ordered {1} to demolish {2}", player.ClientIndex, unit, target);
				Debug("demolish {0}#{1} -> {2}#{3}: value={4}, distance-cells={5:0.0}", unit.Info.Name,
					unit.ActorID, target.Info.Name, target.ActorID, target.GetSellValue(),
					System.Math.Sqrt((target.CenterPosition - unit.CenterPosition).LengthSquared) / 1024d);
				activeDemolitionUnits.Add(unit, new SpecialistAssignment(target));
				activeTargetIds.Add(target.ActorID);
			}
		}

		void ReleaseTransportReservations(Dictionary<Actor, SpecialistAssignment> assignments)
		{
			foreach (var actor in assignments.Keys.Where(IsReservedForTransport).ToArray())
			{
				assignments.Remove(actor);
				Debug("released {0}#{1} to a transport mission", actor.Info.Name, actor.ActorID);
			}
		}

		bool IsReservedForTransport(Actor actor)
		{
			return transportReservations != null && transportReservations.Any(r => r.IsTransportReserved(actor));
		}

		void RetireFinishedAssignments(Dictionary<Actor, SpecialistAssignment> assignments, string action)
		{
			foreach (var pair in assignments.Where(pair => unitCannotBeOrderedOrIsIdle(pair.Key)).ToArray())
			{
				var target = pair.Value.Target;
				var targetRemoved = target.IsDead || !target.IsInWorld;
				var targetHealth = targetRemoved ? 0 : target.TraitOrDefault<IHealth>()?.HP ?? 0;
				var result = targetRemoved ? "target-removed" :
					target.Owner == player ? "captured" :
					targetHealth < pair.Value.TargetHealth ? "sabotaged" :
					pair.Key.IsDead || !pair.Key.IsInWorld ? "specialist-lost" : "specialist-idle";
				Debug("{0} {1}#{2} released from {3}#{4}: result={5}", action, pair.Key.Info.Name,
					pair.Key.ActorID, target.Info.Name, target.ActorID, result);
				assignments.Remove(pair.Key);
			}
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("CaptureScanTicks", FieldSaver.FormatValue(minCaptureDelayTicks)),
				new MiniYamlNode("CaptureAssignments", "", activeCapturers.OrderBy(pair => pair.Key.ActorID).Select(pair =>
					new MiniYamlNode("Assignment", "", new List<MiniYamlNode>
					{
						new MiniYamlNode("Unit", FieldSaver.FormatValue(pair.Key.ActorID)),
						new MiniYamlNode("Target", FieldSaver.FormatValue(pair.Value.Target.ActorID)),
						new MiniYamlNode("TargetHealth", FieldSaver.FormatValue(pair.Value.TargetHealth))
					})).ToList())
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			foreach (var node in data)
				switch (node.Key)
				{
					case "CaptureScanTicks":
						minCaptureDelayTicks = FieldLoader.GetValue<int>(node.Key, node.Value.Value);
						break;
					case "CaptureAssignments":
						activeCapturers.Clear();
						foreach (var assignment in node.Value.Nodes)
							LoadCaptureAssignment(assignment);
						break;
				}
		}

		void LoadCaptureAssignment(MiniYamlNode node)
		{
			T Load<T>(string key, T fallback = default(T))
			{
				var value = node.Value.Nodes.FirstOrDefault(n => n.Key == key);
				return value == null ? fallback : FieldLoader.GetValue<T>(key, value.Value.Value);
			}

			var capturer = world.GetActorById(Load<uint>("Unit"));
			var target = world.GetActorById(Load<uint>("Target"));
			if (capturer == null || target == null)
				return;

			var targetHealth = Load("TargetHealth", target.TraitOrDefault<IHealth>()?.HP ?? 0);
			activeCapturers[capturer] = new SpecialistAssignment(target, targetHealth);
			Debug("capture assignment restored {0}#{1} -> {2}#{3}: target-health={4}", capturer.Info.Name,
				capturer.ActorID, target.Info.Name, target.ActorID, targetHealth);
		}

		void Debug(string format, params object[] args)
		{
			if (Info.DebugLogging)
				Log.Write("debug", "AI ({0}) capture manager: {1}", player.ClientIndex, string.Format(format, args));
		}
	}
}
