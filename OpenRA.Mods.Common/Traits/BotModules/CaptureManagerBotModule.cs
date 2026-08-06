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

	public class CaptureManagerBotModule : ConditionalTrait<CaptureManagerBotModuleInfo>, IBotTick
	{
		readonly struct CaptureCandidate
		{
			public readonly Actor Actor;
			public readonly int Value;
			public readonly bool IsBuilding;
			public readonly int HealthPercent;

			public CaptureCandidate(Actor actor, int value)
			{
				Actor = actor;
				Value = value;
				IsBuilding = actor.Info.HasTraitInfo<BuildingInfo>();
				var health = actor.TraitOrDefault<IHealth>();
				HealthPercent = health == null || health.MaxHP <= 0 ? 100 :
					(int)(100L * health.HP / health.MaxHP);
			}
		}

		sealed class SpecialistAssignment
		{
			public readonly Actor Target;
			public readonly int TargetHealth;
			public readonly int AssignedTick;
			WPos lastSpecialistPosition;
			int lastTargetHealth;
			public int LastProgressTick { get; private set; }

			public SpecialistAssignment(Actor specialist, Actor target, int assignedTick)
			{
				Target = target;
				TargetHealth = target.TraitOrDefault<IHealth>()?.HP ?? 0;
				AssignedTick = assignedTick;
				lastSpecialistPosition = specialist.CenterPosition;
				lastTargetHealth = TargetHealth;
				LastProgressTick = assignedTick;
			}

			public void ObserveProgress(Actor specialist, int worldTick)
			{
				var specialistPosition = specialist.CenterPosition;
				var targetHealth = Target.IsDead || !Target.IsInWorld ? 0 : Target.TraitOrDefault<IHealth>()?.HP ?? 0;
				if (specialistPosition == lastSpecialistPosition && targetHealth == lastTargetHealth)
					return;

				lastSpecialistPosition = specialistPosition;
				lastTargetHealth = targetHealth;
				LastProgressTick = worldTick;
			}
		}

		readonly struct DeferredTarget
		{
			public readonly Actor Target;
			public readonly int RetryTick;

			public DeferredTarget(Actor target, int retryTick)
			{
				Target = target;
				RetryTick = retryTick;
			}
		}

		const int PendingOrderGraceTicks = 10;

		readonly World world;
		readonly Player player;
		readonly Func<Actor, bool> isEnemyUnit;
		readonly Predicate<Actor> unitCannotBeOrdered;
		readonly int maximumCaptureTargetOptions;
		IBotTransportReservations[] transportReservations;
		int minCaptureDelayTicks;
		int minDemolitionDelayTicks;

		// Specialists with active orders and their targets. Remembering the target prevents duplicate assignments
		// and lets the debug log distinguish completed work from an interrupted order.
		readonly Dictionary<Actor, SpecialistAssignment> activeCapturers = new Dictionary<Actor, SpecialistAssignment>();
		readonly Dictionary<Actor, SpecialistAssignment> activeDemolitionUnits = new Dictionary<Actor, SpecialistAssignment>();
		readonly Dictionary<Actor, DeferredTarget> deferredTargets = new Dictionary<Actor, DeferredTarget>();
		readonly SpecialistTargetReservations targetReservations = new SpecialistTargetReservations();

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

			unitCannotBeOrdered = a => a.Owner != player || a.IsDead || !a.IsInWorld;

			maximumCaptureTargetOptions = Math.Max(1, Info.MaximumCaptureTargetOptions);
		}

		protected override void Created(Actor self)
		{
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
			var scanCapture = --minCaptureDelayTicks <= 0;
			var scanDemolition = --minDemolitionDelayTicks <= 0;
			if (scanCapture || scanDemolition)
				RefreshAssignments(bot);

			if (scanCapture)
			{
				minCaptureDelayTicks = Info.MinimumCaptureDelay;
				QueueCaptureOrders(bot);
			}

			if (scanDemolition)
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
				.Where(target => !targetReservations.IsReservedForOtherPurpose(
					target.ActorID, SpecialistAssignmentPurpose.Capture))
				.Where(target => capturers.Any(capturer => !IsTargetDeferred(capturer.Actor, target)))
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
			foreach (var group in activeCapturers.Where(pair => !pair.Key.IsDead && pair.Key.IsInWorld)
				.GroupBy(pair => pair.Value.Target))
			{
				var targetIndex = Array.FindIndex(candidates, candidate => candidate.Actor == group.Key);
				if (targetIndex < 0 || group.Count() < 2 || !RequiresPair(candidates[targetIndex]))
					continue;

				reserved.Add(targetIndex);
				foreach (var pair in group)
					retainedPairActors.Add(pair.Key);
			}

			var remaining = capturers.Where(capturer => !retainedPairActors.Contains(capturer.Actor)).ToList();
			AssignHealthyBuildingPairs(bot, candidates, remaining, reserved);

			foreach (var capturer in remaining.ToArray())
			{
				var distances = candidates.Select(candidate => DistanceSquared(capturer.Actor, candidate.Actor)).ToArray();
				var scores = candidates.Select(candidate => !IsTargetDeferred(capturer.Actor, candidate.Actor) &&
					CanCapture(capturer, candidate.Actor) && !RequiresPair(candidate) ?
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
					reserved.Add(incumbentIndex);
					continue;
				}

				if (targetIndex < 0 || scores[targetIndex] < 0)
				{
					if (activeCapturers.Remove(capturer.Actor))
					{
						targetReservations.Release(capturer.Actor.ActorID);
						bot.QueueOrder(new Order("Stop", capturer.Actor, false));
						Debug("capture {0}#{1} stopped: no eligible solo target or healthy-building partner",
							capturer.Actor.Info.Name, capturer.Actor.ActorID);
					}

					continue;
				}

				reserved.Add(targetIndex);
				var target = candidates[targetIndex];
				var action = incumbentIndex >= 0 ? "retarget" : "capture";
				var oldTarget = incumbentIndex >= 0 ? candidates[incumbentIndex] : default(CaptureCandidate);

				if (!targetReservations.TryReserve(capturer.Actor.ActorID, target.Actor.ActorID,
					SpecialistAssignmentPurpose.Capture, 1))
				{
					Debug("capture {0}#{1} rejected: {2}#{3} is reserved for demolition",
						capturer.Actor.Info.Name, capturer.Actor.ActorID, target.Actor.Info.Name, target.Actor.ActorID);
					continue;
				}

				bot.QueueOrder(new Order("CaptureActor", capturer.Actor, Target.FromActor(target.Actor), false));
				AIUtils.BotDebug("AI ({0}): Ordered {1} to capture {2}", player.ClientIndex, capturer.Actor, target.Actor);
				Debug("{0} {1}#{2} -> {3}#{4}: value={5}, distance-cells={6:0.0}, score={7:0.0}, " +
					"building={8}, health={9}%{10}", action,
					capturer.Actor.Info.Name, capturer.Actor.ActorID, target.Actor.Info.Name, target.Actor.ActorID,
					target.Value, DistanceCells(distances[targetIndex]), scores[targetIndex], target.IsBuilding, target.HealthPercent,
					incumbentIndex >= 0 ? string.Format(", previous={0}#{1}, previous-score={2:0.0}",
						oldTarget.Actor.Info.Name, oldTarget.Actor.ActorID, scores[incumbentIndex]) : string.Empty);
				activeCapturers[capturer.Actor] = new SpecialistAssignment(capturer.Actor, target.Actor, world.WorldTick);
			}
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

					var pair = idle.Where(capturer => !IsTargetDeferred(capturer.Actor, candidates[i].Actor) &&
						CanCapture(capturer, candidates[i].Actor))
						.OrderByDescending(capturer => CaptureScore(capturer.Actor, candidates[i]))
						.ThenBy(capturer => capturer.Actor.ActorID).Take(2).ToArray();
					if (pair.Length < 2)
						continue;

					var pairScore = pair.Min(capturer => CaptureScore(capturer.Actor, candidates[i]));
					var alternativeScore = pair.Max(capturer => BestSoloScore(capturer, candidates, reserved));
					if (pairScore <= alternativeScore || pairScore <= bestPairScore)
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
					if (!targetReservations.TryReserve(capturer.Actor.ActorID, target.Actor.ActorID,
						SpecialistAssignmentPurpose.Capture, 2))
					{
						Debug("capture pair rejected: {0}#{1} is reserved for demolition",
							target.Actor.Info.Name, target.Actor.ActorID);
						foreach (var reservedCapturer in bestPair)
							targetReservations.Release(reservedCapturer.Actor.ActorID);

						return;
					}

					bot.QueueOrder(new Order("CaptureActor", capturer.Actor, Target.FromActor(target.Actor), false));
					activeCapturers[capturer.Actor] = new SpecialistAssignment(capturer.Actor, target.Actor, world.WorldTick);
					remaining.Remove(capturer);
				}

				Debug("capture pair {0}#{1}+{2}#{3} -> {4}#{5}: value={6}, health={7}%, pair-score={8:0.0}",
					bestPair[0].Actor.Info.Name, bestPair[0].Actor.ActorID, bestPair[1].Actor.Info.Name,
					bestPair[1].Actor.ActorID, target.Actor.Info.Name, target.Actor.ActorID, target.Value,
					target.HealthPercent, bestPairScore);
			}
		}

		double BestSoloScore(TraitPair<CaptureManager> capturer, CaptureCandidate[] candidates, HashSet<int> reserved)
		{
			return candidates.Select((candidate, index) => reserved.Contains(index) || RequiresPair(candidate) ||
				IsTargetDeferred(capturer.Actor, candidate.Actor) || !CanCapture(capturer, candidate.Actor) ?
				-1d : CaptureScore(capturer.Actor, candidate)).Max();
		}

		bool RequiresPair(CaptureCandidate candidate)
		{
			return CaptureTargeting.RequiresEngineerPair(candidate.IsBuilding, candidate.HealthPercent,
				Info.SoloBuildingCaptureHealth);
		}

		static bool CanCapture(TraitPair<CaptureManager> capturer, Actor target)
		{
			var captureManager = target.TraitOrDefault<CaptureManager>();
			return captureManager != null && captureManager.CanBeTargetedBy(target, capturer.Actor, capturer.Trait);
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
					!IsTargetDeferred(unit, a) &&
					!targetReservations.IsReserved(a.ActorID))
					.OrderByDescending(a => a.GetSellValue())
					.Take(maximumCaptureTargetOptions)
					.MinByOrDefault(a => (a.CenterPosition - unit.CenterPosition).LengthSquared);
				if (target == null)
					continue;

				if (!targetReservations.TryReserve(unit.ActorID, target.ActorID,
					SpecialistAssignmentPurpose.Demolition, 1))
					continue;

				bot.QueueOrder(new Order("C4", unit, Target.FromActor(target), false)
				{
					ExtraData = Demolition.AutonomousOrderMarker
				});
				AIUtils.BotDebug("AI ({0}): Ordered {1} to demolish {2}", player.ClientIndex, unit, target);
				Debug("demolish {0}#{1} -> {2}#{3}: value={4}, distance-cells={5:0.0}", unit.Info.Name,
					unit.ActorID, target.Info.Name, target.ActorID, target.GetSellValue(),
					System.Math.Sqrt((target.CenterPosition - unit.CenterPosition).LengthSquared) / 1024d);
				activeDemolitionUnits.Add(unit, new SpecialistAssignment(unit, target, world.WorldTick));
			}
		}

		void RefreshAssignments(IBot bot)
		{
			RetireFinishedAssignments(bot, activeCapturers, "capture", SpecialistAssignmentPurpose.Capture);
			RetireFinishedAssignments(bot, activeDemolitionUnits, "demolition", SpecialistAssignmentPurpose.Demolition);
			ReleaseTransportReservations(activeCapturers);
			ReleaseTransportReservations(activeDemolitionUnits);
		}

		void ReleaseTransportReservations(Dictionary<Actor, SpecialistAssignment> assignments)
		{
			foreach (var actor in assignments.Keys.Where(IsReservedForTransport).ToArray())
			{
				assignments.Remove(actor);
				targetReservations.Release(actor.ActorID);
				Debug("released {0}#{1} to a transport mission", actor.Info.Name, actor.ActorID);
			}
		}

		bool IsReservedForTransport(Actor actor)
		{
			return transportReservations != null && transportReservations.Any(r => r.IsTransportReserved(actor));
		}

		void RetireFinishedAssignments(IBot bot, Dictionary<Actor, SpecialistAssignment> assignments, string action,
			SpecialistAssignmentPurpose purpose)
		{
			foreach (var pair in assignments.Where(pair => ShouldRetireAssignment(pair.Key, pair.Value, purpose)).ToArray())
			{
				var target = pair.Value.Target;
				var targetRemoved = target.IsDead || !target.IsInWorld;
				var targetHealth = targetRemoved ? 0 : target.TraitOrDefault<IHealth>()?.HP ?? 0;
				var relationshipInvalid = !targetRemoved && !HasValidRelationship(target, purpose);
				var nonProgressing = world.WorldTick - pair.Value.LastProgressTick > StalledAssignmentTicks(purpose);
				var result = targetRemoved ? "target-removed" :
					target.Owner == player ? "captured" :
					targetHealth < pair.Value.TargetHealth ? "sabotaged" :
					pair.Key.IsDead || !pair.Key.IsInWorld ? "specialist-lost" :
					relationshipInvalid ? "relationship-invalid" :
					nonProgressing ? "non-progressing" : "specialist-idle";
				Debug("{0} {1}#{2} released from {3}#{4}: result={5}", action, pair.Key.Info.Name,
					pair.Key.ActorID, target.Info.Name, target.ActorID, result);
				if (result == "non-progressing")
				{
					deferredTargets[pair.Key] = new DeferredTarget(target,
						world.WorldTick + StalledAssignmentTicks(purpose));
					bot.QueueOrder(new Order("Stop", pair.Key, false));
				}

				assignments.Remove(pair.Key);
				targetReservations.Release(pair.Key.ActorID);
			}
		}

		bool ShouldRetireAssignment(Actor specialist, SpecialistAssignment assignment,
			SpecialistAssignmentPurpose purpose)
		{
			if (unitCannotBeOrdered(specialist) || !HasValidRelationship(assignment.Target, purpose))
				return true;

			if (world.WorldTick - assignment.AssignedTick <= PendingOrderGraceTicks)
				return false;

			if (purpose == SpecialistAssignmentPurpose.Demolition &&
				assignment.Target.TraitsImplementing<Demolishable>()
					.Any(d => d.HasPendingAutonomousDemolition(specialist)))
				return false;

			assignment.ObserveProgress(specialist, world.WorldTick);
			if (world.WorldTick - assignment.LastProgressTick > StalledAssignmentTicks(purpose))
				return true;

			return specialist.IsIdle || !HasExpectedActivity(specialist, purpose);
		}

		int StalledAssignmentTicks(SpecialistAssignmentPurpose purpose)
		{
			var reassessmentTicks = purpose == SpecialistAssignmentPurpose.Capture ?
				Info.MinimumCaptureDelay : Info.MinimumDemolitionDelay;
			return Math.Max(250, Math.Max(1, reassessmentTicks) * 2);
		}

		bool IsTargetDeferred(Actor specialist, Actor target)
		{
			if (!deferredTargets.TryGetValue(specialist, out var deferred))
				return false;

			if (world.WorldTick >= deferred.RetryTick || deferred.Target.IsDead || !deferred.Target.IsInWorld)
			{
				deferredTargets.Remove(specialist);
				return false;
			}

			return deferred.Target == target;
		}

		static bool HasExpectedActivity(Actor actor, SpecialistAssignmentPurpose purpose)
		{
			if (actor.IsIdle || actor.CurrentActivity == null)
				return false;

			return purpose == SpecialistAssignmentPurpose.Capture ?
				actor.CurrentActivity.ActivitiesImplementing<Activities.CaptureActor>().Any() :
				actor.CurrentActivity.ActivitiesImplementing<Activities.Demolish>().Any();
		}

		bool HasValidRelationship(Actor target, SpecialistAssignmentPurpose purpose)
		{
			if (target.IsDead || !target.IsInWorld)
				return false;

			var relationship = player.RelationshipWith(target.Owner);
			return purpose == SpecialistAssignmentPurpose.Capture ?
				Info.CapturableRelationships.HasRelationship(relationship) :
				relationship == PlayerRelationship.Enemy;
		}

		void Debug(string format, params object[] args)
		{
			if (Info.DebugLogging)
				Log.Write("debug", "AI ({0}) capture manager at tick {1}: {2}", player.ClientIndex,
					world.WorldTick, string.Format(format, args));
		}
	}
}
