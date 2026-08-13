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
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using OpenRA.Primitives;
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

		[Desc("Ticks that an otherwise unowned idle demolition specialist must remain eligible before recovery.")]
		public readonly int DemolitionIdleConfirmationTicks = 10;

		[Desc("Maximum distance for recovered demolition specialists to engage exposed infantry.")]
		public readonly int DemolitionFallbackCombatRadiusCells = 10;

		[Desc("Extra cells around hostile weapon range rejected by direct demolition approaches.")]
		public readonly int DemolitionThreatBufferCells = 2;

		[Desc("Maximum cells between queued waypoints on a threat-aware demolition approach.")]
		public readonly int DemolitionRouteWaypointSpacing = 8;

		[Desc("Radius searched for a safe owned hold when no demolition or favorable combat is viable.")]
		public readonly int DemolitionHoldSearchRadiusCells = 6;

		[Desc("Ticks between reconsidering an owned demolition-specialist hold.")]
		public readonly int DemolitionHoldReconsiderTicks = 125;

		[Desc("Write capture and demolition assignments to debug.log.")]
		public readonly bool DebugLogging = false;

		public override object Create(ActorInitializer init) { return new CaptureManagerBotModule(init.Self, this); }
	}

	public class CaptureManagerBotModule : ConditionalTrait<CaptureManagerBotModuleInfo>,
		IBotTick, IBotUnitReservations, IGameSaveTraitData
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

		sealed class SpecialistAssignment
		{
			public readonly Actor Target;
			public readonly int TargetHealth;
			public readonly int AssignedTick;
			public readonly int MaximumClaimants;
			WPos lastSpecialistPosition;
			int lastTargetHealth;
			public int LastProgressTick { get; private set; }
			public int MissingActivitySinceTick { get; private set; }
			public WPos LastSpecialistPosition { get { return lastSpecialistPosition; } }
			public int LastTargetHealth { get { return lastTargetHealth; } }

			public SpecialistAssignment(Actor specialist, Actor target, int assignedTick, int maximumClaimants)
			{
				Target = target;
				TargetHealth = target.TraitOrDefault<IHealth>()?.HP ?? 0;
				AssignedTick = assignedTick;
				MaximumClaimants = maximumClaimants;
				lastSpecialistPosition = specialist.CenterPosition;
				lastTargetHealth = TargetHealth;
				LastProgressTick = assignedTick;
				MissingActivitySinceTick = assignedTick;
			}

			public SpecialistAssignment(Actor target, int targetHealth, int assignedTick, int maximumClaimants,
				WPos lastSpecialistPosition, int lastTargetHealth, int lastProgressTick, int missingActivitySinceTick)
			{
				Target = target;
				TargetHealth = targetHealth;
				AssignedTick = assignedTick;
				MaximumClaimants = maximumClaimants;
				this.lastSpecialistPosition = lastSpecialistPosition;
				this.lastTargetHealth = lastTargetHealth;
				LastProgressTick = lastProgressTick;
				MissingActivitySinceTick = missingActivitySinceTick;
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

			public void ObserveActivity(bool hasExpectedActivity, int worldTick)
			{
				if (hasExpectedActivity)
				{
					MissingActivitySinceTick = -1;
					return;
				}

				if (MissingActivitySinceTick < 0)
					MissingActivitySinceTick = worldTick;
			}

			public SpecialistAssignment WithMaximumClaimants(int maximumClaimants)
			{
				return new SpecialistAssignment(Target, TargetHealth, AssignedTick, maximumClaimants,
					lastSpecialistPosition, lastTargetHealth, LastProgressTick, MissingActivitySinceTick);
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

		sealed class SavedAssignment
		{
			public uint SpecialistId;
			public uint TargetId;
			public SpecialistAssignmentPurpose Purpose;
			public int MaximumClaimants;
			public int TargetHealth;
			public int AssignedTick;
			public WPos LastSpecialistPosition;
			public int LastTargetHealth;
			public int LastProgressTick;
			public int MissingActivitySinceTick;
		}

		sealed class SavedCommandoConfirmation
		{
			public uint SpecialistId;
			public int IdleSinceTick;
		}

		sealed class SavedCommandoFallback
		{
			public uint SpecialistId;
			public CommandoFallbackPurpose Purpose;
			public uint TargetId;
			public CPos Destination;
			public int AssignedTick;
			public int ReconsiderTick;
		}

		enum CommandoFallbackPurpose
		{
			Combat,
			Hold
		}

		sealed class CommandoFallback
		{
			public readonly CommandoFallbackPurpose Purpose;
			public readonly Actor Target;
			public readonly CPos Destination;
			public readonly int AssignedTick;
			public int ReconsiderTick;

			public CommandoFallback(CommandoFallbackPurpose purpose, Actor target, CPos destination,
				int assignedTick, int reconsiderTick)
			{
				Purpose = purpose;
				Target = target;
				Destination = destination;
				AssignedTick = assignedTick;
				ReconsiderTick = reconsiderTick;
			}
		}

		readonly struct CommandoThreat
		{
			public readonly Actor Actor;
			public readonly int Range;
			public readonly int Value;

			public CommandoThreat(Actor actor, int range)
			{
				Actor = actor;
				Range = range;
				Value = Math.Max(1, actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1);
			}
		}

		readonly struct CommandoThreatRelevance
		{
			public readonly bool CoversOwnedHold;
			public readonly bool CoversLane;
			public readonly long DistanceSquared;

			public CommandoThreatRelevance(bool coversOwnedHold, bool coversLane, long distanceSquared)
			{
				CoversOwnedHold = coversOwnedHold;
				CoversLane = coversLane;
				DistanceSquared = distanceSquared;
			}
		}

		const int PendingOrderGraceTicks = 10;

		readonly World world;
		readonly Player player;
		readonly Func<Actor, bool> isEnemyUnit;
		readonly Predicate<Actor> unitCannotBeOrdered;
		readonly int maximumCaptureTargetOptions;
		IBotTransportReservations[] transportReservations;
		IBotUnitReservations[] unitReservations;
		IBotTemporaryUnitControl[] temporaryUnitControls;
		IBotTransportObjectiveService[] transportServices;
		DomainIndex domainIndex;
		int minCaptureDelayTicks;
		int minDemolitionDelayTicks;

		// Specialists with active orders and their targets. Remembering the target prevents duplicate assignments
		// and lets the debug log distinguish completed work from an interrupted order.
		readonly Dictionary<Actor, SpecialistAssignment> activeCapturers = new Dictionary<Actor, SpecialistAssignment>();
		readonly Dictionary<Actor, SpecialistAssignment> activeDemolitionUnits = new Dictionary<Actor, SpecialistAssignment>();
		readonly Dictionary<Actor, DeferredTarget> deferredTargets = new Dictionary<Actor, DeferredTarget>();
		readonly HashSet<Actor> pendingCaptureRecovery = new HashSet<Actor>();
		readonly HashSet<Actor> pendingDemolitionRecovery = new HashSet<Actor>();
		readonly Dictionary<Actor, int> demolitionIdleSince = new Dictionary<Actor, int>();
		readonly Dictionary<Actor, CommandoFallback> commandoFallbacks = new Dictionary<Actor, CommandoFallback>();
		readonly SpecialistTargetReservations targetReservations = new SpecialistTargetReservations();
		static readonly BitSet<TargetableType> InfantryTargetTypes = new BitSet<TargetableType>("Infantry");

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
			domainIndex = world.WorldActor.TraitOrDefault<DomainIndex>();
			transportReservations = self.Owner.PlayerActor.TraitsImplementing<IBotTransportReservations>().ToArray();
			unitReservations = self.Owner.PlayerActor.TraitsImplementing<IBotUnitReservations>()
				.Where(reservation => reservation != this).ToArray();
			temporaryUnitControls = self.Owner.PlayerActor.TraitsImplementing<IBotTemporaryUnitControl>().ToArray();
			transportServices = self.Owner.PlayerActor.TraitsImplementing<IBotTransportObjectiveService>().ToArray();
			base.Created(self);
		}

		bool IBotUnitReservations.IsUnitReserved(Actor actor)
		{
			return actor != null && (activeCapturers.ContainsKey(actor) || activeDemolitionUnits.ContainsKey(actor) ||
				commandoFallbacks.ContainsKey(actor) ||
				targetReservations.TryGetReservation(actor.ActorID, out _, out _));
		}

		protected override void TraitEnabled(Actor self)
		{
			// Avoid all AIs reevaluating assignments on the same tick, randomize their initial evaluation delay.
			minCaptureDelayTicks = world.LocalRandom.Next(0, Info.MinimumCaptureDelay);
			minDemolitionDelayTicks = world.LocalRandom.Next(0, Info.MinimumDemolitionDelay);
		}

		void IBotTick.BotTick(IBot bot)
		{
			// Audit only the small active-specialist sets each tick. Candidate/world scans stay on
			// their normal cadence unless a transition makes an owned specialist available again.
			pendingCaptureRecovery.Clear();
			pendingDemolitionRecovery.Clear();
			var reassessCapture = AuditAssignments(bot, activeCapturers, "capture", SpecialistAssignmentPurpose.Capture,
				pendingCaptureRecovery);
			var reassessDemolition = AuditAssignments(bot, activeDemolitionUnits, "demolition",
				SpecialistAssignmentPurpose.Demolition, pendingDemolitionRecovery);
			var reassessFallback = AuditCommandoFallbacks();

			var scanCapture = reassessCapture || pendingCaptureRecovery.Count != 0 || --minCaptureDelayTicks <= 0;
			var scanDemolition = reassessDemolition || reassessFallback || pendingDemolitionRecovery.Count != 0 ||
				--minDemolitionDelayTicks <= 0;

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
				.Where(a => a.Owner == player && (a.IsIdle || activeCapturers.ContainsKey(a) ||
					pendingCaptureRecovery.Contains(a)) &&
					Info.CapturingActorTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<CapturesInfo>() &&
					!IsReservedForTransport(a))
				.Select(a => new TraitPair<CaptureManager>(a, a.TraitOrDefault<CaptureManager>()))
				.Where(tp => tp.Trait != null)
				.OrderBy(tp => tp.Actor.ActorID)
				.ToArray();

			if (capturers.Length == 0)
				return;

			var relationshipTargets = world.Players.Where(p => !p.Spectating
					&& Info.CapturableRelationships.HasRelationship(player.RelationshipWith(p)))
				.SelectMany(p => Info.CheckCaptureTargetsForVisibility
					? GetVisibleActorsBelongingToPlayer(p) : GetActorsThatCanBeOrderedByPlayer(p));
			var ownedRestorableTargets = GetActorsThatCanBeOrderedByPlayer(player)
				.Where(target => (!Info.CheckCaptureTargetsForVisibility || target.CanBeViewedByPlayer(player)) &&
					capturers.Any(capturer => IsOwnedRestorableHuskTarget(target, capturer)));
			var targetOptions = relationshipTargets.Concat(ownedRestorableTargets)
				.GroupBy(target => target.ActorID).Select(group => group.First())
				.OrderBy(target => target.ActorID).ToArray();

			PreemptReversibleDemolitions(bot, capturers, targetOptions);

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
			{
				DebugEmptyCapturePlan(capturers, targetOptions);
				return;
			}

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
					DebugHighestPriorityRejection(capturer, candidates, unavailable, incumbentIndex);
					reserved.Add(incumbentIndex);
					continue;
				}

				if (targetIndex < 0 || scores[targetIndex] < 0)
				{
					DebugHighestPriorityRejection(capturer, candidates, unavailable, -1);
					if (activeCapturers.Remove(capturer.Actor))
					{
						targetReservations.Release(capturer.Actor.ActorID);
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

				if (!targetReservations.TryReserve(capturer.Actor.ActorID, target.Actor.ActorID,
					SpecialistAssignmentPurpose.Capture, 1))
				{
					Debug("capture {0}#{1} rejected: {2}#{3} is reserved for demolition",
						capturer.Actor.Info.Name, capturer.Actor.ActorID, target.Actor.Info.Name, target.Actor.ActorID);
					continue;
				}

				var transported = !HasReachableCaptureApproach(capturer.Actor, target.Actor);
				if (transported && !TryRequestCaptureTransport(capturer.Actor, target.Actor))
				{
					targetReservations.Release(capturer.Actor.ActorID);
					Debug("capture {0}#{1} -> {2}#{3} released: existing transport plan became unavailable",
						capturer.Actor.Info.Name, capturer.Actor.ActorID, target.Actor.Info.Name, target.Actor.ActorID);
					continue;
				}

				if (!transported)
					bot.QueueOrder(new Order("CaptureActor", capturer.Actor, Target.FromActor(target.Actor), false));
				AIUtils.BotDebug("AI ({0}): Ordered {1} to capture {2}", player.ClientIndex, capturer.Actor, target.Actor);
				Debug("{0} {1}#{2} -> {3}#{4}: value={5}, distance-cells={6:0.0}, score={7:0.0}, " +
					"building={8}, health={9}/{10}{11}", action,
					capturer.Actor.Info.Name, capturer.Actor.ActorID, target.Actor.Info.Name, target.Actor.ActorID,
					target.Value, DistanceCells(distances[targetIndex]), scores[targetIndex], target.IsBuilding,
					target.HitPoints, target.MaxHitPoints,
					incumbentIndex >= 0 ? string.Format(", previous={0}#{1}, previous-score={2:0.0}",
						oldTarget.Actor.Info.Name, oldTarget.Actor.ActorID, scores[incumbentIndex]) : string.Empty);
				activeCapturers[capturer.Actor] = new SpecialistAssignment(capturer.Actor, target.Actor, world.WorldTick, 1);
			}
		}

		void PreemptReversibleDemolitions(IBot bot, TraitPair<CaptureManager>[] capturers, Actor[] targetOptions)
		{
			foreach (var target in targetOptions.OrderBy(target => target.ActorID))
			{
				var captureCandidate = new CaptureCandidate(target, CaptureEconomicValue(target));
				if (RequiresPair(captureCandidate))
					continue;

				var demolition = activeDemolitionUnits.Where(pair => pair.Value.Target == target)
					.OrderBy(pair => pair.Key.ActorID).FirstOrDefault();
				if (demolition.Key == null)
					continue;

				var capturer = capturers.Where(candidate => candidate.Actor.IsIdle &&
					!activeCapturers.ContainsKey(candidate.Actor) &&
					!targetReservations.TryGetReservation(candidate.Actor.ActorID, out _, out _) &&
					(unitReservations == null || !unitReservations.Any(r => r.IsUnitReserved(candidate.Actor))) &&
					CanCapture(candidate, target) && HasReachableCaptureApproach(candidate.Actor, target))
					.OrderBy(candidate => DistanceSquared(candidate.Actor, target))
					.ThenBy(candidate => candidate.Actor.ActorID).FirstOrDefault();
				if (!CaptureTargeting.CanPreemptDemolition(capturer.Actor != null,
					HasPendingAutonomousDemolition(target, demolition.Key)))
					continue;

				RetireAssignment(bot, activeDemolitionUnits, demolition, "demolition",
					SpecialistAssignmentPurpose.Demolition, "capture-preempted", stopSpecialist: true);
				pendingDemolitionRecovery.Add(demolition.Key);
				if (!targetReservations.TryReserve(capturer.Actor.ActorID, target.ActorID,
					SpecialistAssignmentPurpose.Capture, 1))
					continue;

				bot.QueueOrder(new Order("CaptureActor", capturer.Actor, Target.FromActor(target), false));
				activeCapturers.Add(capturer.Actor,
					new SpecialistAssignment(capturer.Actor, target, world.WorldTick, 1));
				Debug("ownership target={0}#{1} transition=demolition-to-capture planted=false " +
					"released-specialist={2}#{3} capture-owner={4}#{5}", target.Info.Name, target.ActorID,
					demolition.Key.Info.Name, demolition.Key.ActorID, capturer.Actor.Info.Name,
					capturer.Actor.ActorID);
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
					var retained = assignments[0];
					foreach (var surplus in assignments.Skip(1))
					{
						bot.QueueOrder(new Order("Stop", surplus.Key, false));
						activeCapturers.Remove(surplus.Key);
						targetReservations.Release(surplus.Key.ActorID);
						Debug("capture pair surplus stopped and released {0}#{1}: target={2}#{3}, " +
							"health={4}/{5}, solo-eligible=true",
							surplus.Key.Info.Name, surplus.Key.ActorID, candidates[targetIndex].Actor.Info.Name,
							candidates[targetIndex].Actor.ActorID, candidates[targetIndex].HitPoints,
							candidates[targetIndex].MaxHitPoints);
					}

					// Persist the surviving incumbent as a one-claimant assignment. This keeps the
					// shared reservation and save/restore cardinality aligned after the pair shrinks.
					activeCapturers[retained.Key] = retained.Value.WithMaximumClaimants(1);

					continue;
				}

				var pair = assignments.Select(assignment => capturerByActor.TryGetValue(assignment.Key, out var capturer) ?
					capturer : default(TraitPair<CaptureManager>)).Where(capturer => capturer.Actor != null).Take(2).ToArray();
				if (pair.Length < 2)
					continue;

				foreach (var surplus in assignments.Where(assignment => pair.All(capturer => capturer.Actor != assignment.Key)))
				{
					bot.QueueOrder(new Order("Stop", surplus.Key, false));
					activeCapturers.Remove(surplus.Key);
					targetReservations.Release(surplus.Key.ActorID);
				}

				if (pair.Any(capturer => !CanCapture(capturer, candidates[targetIndex].Actor)))
				{
					foreach (var capturer in pair)
					{
						activeCapturers.Remove(capturer.Actor);
						targetReservations.Release(capturer.Actor.ActorID);
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
					var pairReserved = pair.All(capturer => targetReservations.TryReserve(capturer.Actor.ActorID,
						replacement.Actor.ActorID, SpecialistAssignmentPurpose.Capture, 2));
					if (!pairReserved)
					{
						foreach (var capturer in pair)
						{
							targetReservations.Release(capturer.Actor.ActorID);
							bot.QueueOrder(new Order("Stop", capturer.Actor, false));
						}

						continue;
					}

					foreach (var capturer in pair)
					{
						bot.QueueOrder(new Order("CaptureActor", capturer.Actor, Target.FromActor(replacement.Actor), false));
						activeCapturers[capturer.Actor] = new SpecialistAssignment(
							capturer.Actor, replacement.Actor, world.WorldTick, 2);
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
						targetReservations.Release(capturer.ActorID);
						bot.QueueOrder(new Order("Stop", capturer, false));
						retainedActors.Add(capturer);
						continue;
					}

					var replacement = candidates[replacementIndex];
					reserved.Add(replacementIndex);
					if (!targetReservations.TryReserve(capturer.ActorID, replacement.Actor.ActorID,
						SpecialistAssignmentPurpose.Capture, 1))
					{
						targetReservations.Release(capturer.ActorID);
						bot.QueueOrder(new Order("Stop", capturer, false));
						retainedActors.Add(capturer);
						continue;
					}

					bot.QueueOrder(new Order("CaptureActor", capturer, Target.FromActor(replacement.Actor), false));
					activeCapturers[capturer] = new SpecialistAssignment(capturer, replacement.Actor, world.WorldTick, 1);
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
			return candidates.Select(candidate => RequiresPair(candidate) ||
				IsTargetDeferred(capturer.Actor, candidate.Actor) || !CanCapture(capturer, candidate.Actor) ?
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

					var pair = idle.Where(capturer => !IsTargetDeferred(capturer.Actor, candidates[i].Actor) &&
						CanCapture(capturer, candidates[i].Actor))
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
				var pairReserved = bestPair.All(capturer => targetReservations.TryReserve(capturer.Actor.ActorID,
					target.Actor.ActorID, SpecialistAssignmentPurpose.Capture, 2));
				if (!pairReserved)
				{
					Debug("capture pair rejected: {0}#{1} is reserved for demolition",
						target.Actor.Info.Name, target.Actor.ActorID);
					foreach (var reservedCapturer in bestPair)
						targetReservations.Release(reservedCapturer.Actor.ActorID);

					return;
				}

				foreach (var capturer in bestPair)
				{
					bot.QueueOrder(new Order("CaptureActor", capturer.Actor, Target.FromActor(target.Actor), false));
					activeCapturers[capturer.Actor] = new SpecialistAssignment(capturer.Actor, target.Actor, world.WorldTick, 2);
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
			return CanCaptureByRules(capturer, target) && (HasReachableCaptureApproach(capturer.Actor, target) ||
				IsCaptureTransportActive(capturer.Actor) || CanUseExistingTransport(capturer, target));
		}

		static bool CanCaptureByRules(TraitPair<CaptureManager> capturer, Actor target)
		{
			var captureManager = target.TraitOrDefault<CaptureManager>();
			return captureManager != null && captureManager.CanBeTargetedBy(target, capturer.Actor, capturer.Trait);
		}

		bool IsOwnedRestorableHuskTarget(Actor target, TraitPair<CaptureManager> capturer)
		{
			if (target == null || target.IsDead || !target.IsInWorld)
				return false;

			var transformInfo = target.Info.TraitInfoOrDefault<TransformOnCaptureInfo>();
			var transform = target.TraitOrDefault<TransformOnCapture>();
			var targetManager = target.TraitOrDefault<CaptureManager>();
			var hasValidTransform = transformInfo != null && transform != null &&
				!string.IsNullOrEmpty(transformInfo.IntoActor) && world.Map.Rules.Actors.ContainsKey(transformInfo.IntoActor);
			var hasMatchingCapture = hasValidTransform && targetManager != null &&
				capturer.Actor.TraitsImplementing<Captures>().Any(captures =>
					targetManager.CanBeTargetedBy(target, capturer.Actor, captures) &&
					transform.HandlesCaptureTypes(captures.Info.CaptureTypes));

			return CaptureTargeting.IsCapabilityScopedOwnedRestorationCandidate(target.Owner == player,
				target.Info.HasTraitInfo<HuskInfo>(), target.Info.HasTraitInfo<BuildingInfo>(),
				hasValidTransform, hasMatchingCapture);
		}

		bool IsOwnedRestorableHuskTarget(Actor target, Actor capturer)
		{
			var manager = capturer?.TraitOrDefault<CaptureManager>();
			return manager != null && IsOwnedRestorableHuskTarget(target,
				new TraitPair<CaptureManager>(capturer, manager));
		}

		bool CanUseExistingTransport(TraitPair<CaptureManager> capturer, Actor target)
		{
			return IsOwnedRestorableHuskTarget(target, capturer) && transportServices != null &&
				transportServices.Any(service => service.CanTransportTo(capturer.Actor, target, this));
		}

		bool TryRequestCaptureTransport(Actor capturer, Actor target)
		{
			return transportServices != null && transportServices.Any(service =>
				service.TryRequestTransport(capturer, target, this));
		}

		bool IsCaptureTransportActive(Actor capturer)
		{
			return transportServices != null && transportServices.Any(service => service.IsTransporting(capturer));
		}

		bool ConsumeTimedOutCaptureTransport(Actor capturer, Actor target)
		{
			return transportServices != null && transportServices.Any(service =>
				service.TryConsumeTimedOutObjective(capturer, target));
		}

		void CancelCaptureTransport(Actor capturer)
		{
			if (transportServices == null)
				return;

			foreach (var service in transportServices)
				service.CancelTransport(capturer);
		}

		bool HasReachableCaptureApproach(Actor capturer, Actor target)
		{
			var mobile = capturer.TraitOrDefault<Mobile>();
			if (mobile == null || domainIndex == null)
				return true;

			foreach (var cell in Util.AdjacentCells(world, Target.FromActor(target)))
				if (mobile.CanStayInCell(cell) &&
					mobile.CanEnterCell(cell, check: BlockedByActor.Immovable) &&
					domainIndex.IsPassable(capturer.Location, cell, mobile.Locomotor))
					return true;

			return false;
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

		void DebugEmptyCapturePlan(TraitPair<CaptureManager>[] capturers, IEnumerable<Actor> targetOptions)
		{
			if (!Info.DebugLogging)
				return;

			Actor rejectedTarget = null;
			string rejectionReason = null;
			double rejectedScore = -1;
			var rejectedIsBuilding = false;
			foreach (var target in targetOptions)
			{
				if (target.IsDead || !target.IsInWorld || target.TraitOrDefault<IPositionable>() == null)
					continue;

				var reason = GlobalCaptureRejectionReason(capturers, target);
				if (reason == null)
					continue;

				var candidate = new CaptureCandidate(target, CaptureEconomicValue(target));
				var score = capturers.Max(capturer => CaptureScore(capturer.Actor, candidate));
				if (rejectedTarget != null && (score < rejectedScore ||
					(score == rejectedScore && candidate.IsBuilding == rejectedIsBuilding &&
						target.ActorID > rejectedTarget.ActorID) ||
					(score == rejectedScore && !candidate.IsBuilding && rejectedIsBuilding)))
					continue;

				rejectedTarget = target;
				rejectionReason = reason;
				rejectedScore = score;
				rejectedIsBuilding = candidate.IsBuilding;
			}

			foreach (var capturer in capturers)
			{
				var actor = capturer.Actor;
				var activity = actor.CurrentActivity == null ? "none" : actor.CurrentActivity.GetType().Name;
				var hasAssignment = activeCapturers.TryGetValue(actor, out var assignment);
				var hasReservation = targetReservations.TryGetReservation(actor.ActorID,
					out var reservedTarget, out var reservedPurpose);
				var assignmentState = hasAssignment ? string.Format("{0}#{1}", assignment.Target.Info.Name,
					assignment.Target.ActorID) : "none";
				var reservationState = hasReservation ? string.Format("{0}/{1}", reservedTarget, reservedPurpose) : "none";

				if (rejectedTarget == null)
				{
					Debug("capture {0}#{1} planning empty: idle={2}, activity={3}, assignment={4}, " +
						"reservation={5}, transport-owned={6}, rejected=no-candidates", actor.Info.Name,
						actor.ActorID, actor.IsIdle, activity, assignmentState, reservationState,
						IsReservedForTransport(actor));
					continue;
				}

				var candidate = new CaptureCandidate(rejectedTarget, CaptureEconomicValue(rejectedTarget));
				var relationship = player.RelationshipWith(rejectedTarget.Owner);
				var claimants = string.Join(",", targetReservations.Claimants(rejectedTarget.ActorID));
				Debug("capture {0}#{1} planning empty: idle={2}, activity={3}, assignment={4}, " +
					"reservation={5}, transport-owned={6}, candidate={7}#{8}, owner={9}, relationship={10}, " +
					"value={11}, distance-cells={12:0.0}, score={13:0.0}, target-claimants=[{14}], rejected={15}",
					actor.Info.Name, actor.ActorID, actor.IsIdle, activity, assignmentState, reservationState,
					IsReservedForTransport(actor), rejectedTarget.Info.Name, rejectedTarget.ActorID,
					rejectedTarget.Owner.InternalName, relationship, candidate.Value,
					DistanceCells(DistanceSquared(actor, rejectedTarget)), CaptureScore(actor, candidate),
					claimants, rejectionReason);
			}
		}

		string GlobalCaptureRejectionReason(TraitPair<CaptureManager>[] capturers, Actor target)
		{
			if (targetReservations.IsReservedForOtherPurpose(target.ActorID, SpecialistAssignmentPurpose.Capture))
				return "reserved-by-demolition";

			if (capturers.All(capturer => IsTargetDeferred(capturer.Actor, target)))
				return "deferred";

			var captureManager = target.TraitOrDefault<CaptureManager>();
			if (captureManager == null || !capturers.Any(capturer =>
				captureManager.CanBeTargetedBy(target, capturer.Actor, capturer.Trait)))
				return "capture-ineligible";

			if (Info.CapturableActorTypes.Any() && !Info.CapturableActorTypes.Contains(target.Info.Name.ToLowerInvariant()))
				return "actor-type-excluded";

			return null;
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

			var targets = world.Actors.Where(a => !a.IsDead && a.IsInWorld &&
				player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy &&
				a.Info.HasTraitInfo<BuildingInfo>() &&
				(!Info.CheckCaptureTargetsForVisibility || a.CanBeViewedByPlayer(player)) &&
				!targetReservations.IsReserved(a.ActorID))
				.OrderByDescending(a => a.GetSellValue()).ThenBy(a => a.ActorID)
				.Take(maximumCaptureTargetOptions).ToArray();
			var threats = CommandoThreats(targets);
			ReconsiderCommandoHolds(bot, targets, threats);

			var demolitionUnits = world.Actors.Where(IsUnownedIdleCommando)
				.OrderBy(unit => unit.ActorID).ToArray();
			var confirmedUnits = demolitionUnits.Where(ConfirmIdleCommando).ToArray();
			foreach (var stale in demolitionIdleSince.Keys.Where(actor => !demolitionUnits.Contains(actor)).ToArray())
				demolitionIdleSince.Remove(stale);

			if (confirmedUnits.Length == 0)
				return;

			var distances = new long[confirmedUnits.Length, targets.Length];
			var viable = new bool[confirmedUnits.Length, targets.Length];
			for (var unitIndex = 0; unitIndex < confirmedUnits.Length; unitIndex++)
			{
				var unit = confirmedUnits[unitIndex];
				for (var targetIndex = 0; targetIndex < targets.Length; targetIndex++)
				{
					var target = targets[targetIndex];
					distances[unitIndex, targetIndex] = DistanceSquared(unit, target);
					viable[unitIndex, targetIndex] = HasCheapDemolitionResponse(unit, target, threats);
				}
			}

			var assigned = new HashSet<Actor>();
			foreach (var allocation in CaptureTargeting.TargetFirstDemolitionAllocation(distances, viable))
			{
				var unit = confirmedUnits[allocation.Unit];
				var target = targets[allocation.Target];
				var routeThreats = RouteThreats(unit, target, threats);
				var favorableFight = IsFavorableDemolitionFight(unit, target, routeThreats);
				var safeRoute = routeThreats.Length > 0 && !favorableFight ?
					FindSafeDemolitionRoute(unit, target, threats) : null;
				var response = CaptureTargeting.DemolitionApproach(routeThreats.Length > 0,
					favorableFight, safeRoute != null);
				if (!IsUnownedIdleCommando(unit) ||
					target.IsDead || !target.IsInWorld || targetReservations.IsReserved(target.ActorID))
				{
					Debug("commando {0}#{1} transition=release reason=pre-order-revalidation target={2}#{3} " +
						"ownership={4} threat={5}", unit.Info.Name, unit.ActorID, target.Info.Name, target.ActorID,
						OwnershipState(unit), ThreatState(routeThreats));
					continue;
				}

				if (response == DemolitionApproachResponse.WithdrawOrHold)
				{
					Debug("commando {0}#{1} transition=withdraw-or-hold target={2}#{3} ownership=confirmed-ownerless " +
						"threat={4} route=no-safe-alternate", unit.Info.Name, unit.ActorID, target.Info.Name,
						target.ActorID, ThreatState(routeThreats));
					continue;
				}

				if (!targetReservations.TryReserve(unit.ActorID, target.ActorID,
					SpecialistAssignmentPurpose.Demolition, 1))
					continue;

				QueueDemolitionApproach(bot, unit, target, response, routeThreats, safeRoute);
				AIUtils.BotDebug("AI ({0}): Ordered {1} to demolish {2}", player.ClientIndex, unit, target);
				Debug("demolish {0}#{1} -> {2}#{3}: value={4}, distance-cells={5:0.0}, " +
					"transition=approach, ownership=confirmed-ownerless, route={6}, threat={7}",
					unit.Info.Name,
					unit.ActorID, target.Info.Name, target.ActorID, target.GetSellValue(),
					System.Math.Sqrt((target.CenterPosition - unit.CenterPosition).LengthSquared) / 1024d,
					response, ThreatState(routeThreats));
				activeDemolitionUnits.Add(unit, new SpecialistAssignment(unit, target, world.WorldTick, 1));
				demolitionIdleSince.Remove(unit);
				assigned.Add(unit);
			}

			foreach (var unit in confirmedUnits.Where(unit => !assigned.Contains(unit)))
				AssignCommandoFallback(bot, unit, threats, targets);
		}

		bool IsUnownedIdleCommando(Actor actor)
		{
			return actor.Owner == player && actor.IsIdle && !unitCannotBeOrdered(actor) &&
				Info.DemolitionActorTypes.Contains(actor.Info.Name) && actor.Info.HasTraitInfo<DemolitionInfo>() &&
				!activeDemolitionUnits.ContainsKey(actor) && !activeCapturers.ContainsKey(actor) &&
				!commandoFallbacks.ContainsKey(actor) && !IsReservedForTransport(actor) &&
				(unitReservations == null || !unitReservations.Any(r => r.IsUnitReserved(actor))) &&
				(temporaryUnitControls == null || !temporaryUnitControls.Any(c => c.IsUnitTemporarilyControlled(actor)));
		}

		bool ConfirmIdleCommando(Actor actor)
		{
			if (!demolitionIdleSince.TryGetValue(actor, out var idleSince))
			{
				demolitionIdleSince.Add(actor, world.WorldTick);
				minDemolitionDelayTicks = Math.Min(minDemolitionDelayTicks,
					Math.Max(1, Info.DemolitionIdleConfirmationTicks));
				Debug("commando {0}#{1} ownership=candidate-ownerless transition=confirming idle-since={2} " +
					"recheck={3}", actor.Info.Name, actor.ActorID, world.WorldTick,
					world.WorldTick + Info.DemolitionIdleConfirmationTicks);
				return false;
			}

			return CaptureTargeting.ConfirmedOwnerless(idleSince, world.WorldTick,
				Info.DemolitionIdleConfirmationTicks, actor.IsIdle, actor.CurrentActivity != null,
				(unitReservations != null && unitReservations.Any(r => r.IsUnitReserved(actor))) ||
				(temporaryUnitControls != null && temporaryUnitControls.Any(c => c.IsUnitTemporarilyControlled(actor))),
				IsReservedForTransport(actor));
		}

		CommandoThreat[] CommandoThreats(Actor[] targets)
		{
			var specialists = world.Actors.Where(actor => !actor.IsDead && actor.IsInWorld &&
				actor.Owner == player && Info.DemolitionActorTypes.Contains(actor.Info.Name))
				.OrderBy(actor => actor.ActorID).Take(maximumCaptureTargetOptions * 4).ToArray();
			var buffer = WDist.FromCells(Math.Max(0, Info.DemolitionThreatBufferCells)).Length;
			return world.Actors.Where(actor => !actor.IsDead && actor.IsInWorld &&
				player.RelationshipWith(actor.Owner) == PlayerRelationship.Enemy)
				.Select(actor => new CommandoThreat(actor, actor.TraitsImplementing<Armament>()
					.Where(armament => !armament.IsTraitDisabled)
					.Select(armament => armament.MaxRange().Length).DefaultIfEmpty(0).Max()))
				.Where(threat => threat.Range > 0)
				.Select(threat => new
				{
					Threat = threat,
					Relevance = CommandoThreatCoverage(threat, specialists, targets, buffer)
				})
				.OrderByDescending(candidate => candidate.Relevance.CoversOwnedHold)
				.ThenByDescending(candidate => candidate.Relevance.CoversLane)
				.ThenBy(candidate => candidate.Relevance.DistanceSquared)
				.ThenByDescending(candidate => candidate.Threat.Value)
				.ThenBy(candidate => candidate.Threat.Actor.ActorID)
				.Select(candidate => candidate.Threat)
				.Take(maximumCaptureTargetOptions * 4).ToArray();
		}

		CommandoThreatRelevance CommandoThreatCoverage(CommandoThreat threat, Actor[] specialists, Actor[] targets,
			int buffer)
		{
			var range = threat.Range + buffer;
			var distanceSquared = long.MaxValue;
			var coversLane = false;
			foreach (var specialist in specialists)
			{
				var specialistDistance = (long)(threat.Actor.CenterPosition - specialist.CenterPosition).LengthSquared;
				distanceSquared = Math.Min(distanceSquared, specialistDistance);
				coversLane |= CaptureTargeting.ThreatCoverageMargin(specialistDistance, range) <= 0;
				foreach (var target in targets)
				{
					var laneDistance = AirThreatGeometry.DistanceSquaredToSegment(threat.Actor.CenterPosition,
						specialist.CenterPosition, target.CenterPosition);
					distanceSquared = Math.Min(distanceSquared, laneDistance);
					coversLane |= CaptureTargeting.ThreatCoverageMargin(laneDistance, range) <= 0;
				}
			}

			var coversOwnedHold = false;
			foreach (var pair in commandoFallbacks.Where(pair => pair.Value.Purpose == CommandoFallbackPurpose.Hold))
			{
				var holdDistance = AirThreatGeometry.DistanceSquaredToSegment(threat.Actor.CenterPosition,
					pair.Key.CenterPosition, world.Map.CenterOfCell(pair.Value.Destination));
				distanceSquared = Math.Min(distanceSquared, holdDistance);
				coversOwnedHold |= CaptureTargeting.ThreatCoverageMargin(holdDistance, range) <= 0;
			}

			return new CommandoThreatRelevance(coversOwnedHold, coversLane, distanceSquared);
		}

		CommandoThreat[] RouteThreats(Actor specialist, Actor target, CommandoThreat[] threats)
		{
			var targetTypes = specialist.GetEnabledTargetTypes();
			return threats.Where(threat =>
				threat.Actor.TraitsImplementing<Armament>().Any(armament =>
					!armament.IsTraitDisabled && armament.Weapon.IsValidTarget(targetTypes)))
				.Where(threat =>
				{
				var range = threat.Range + WDist.FromCells(Math.Max(0, Info.DemolitionThreatBufferCells)).Length;
				return AirThreatGeometry.DistanceSquaredToSegment(threat.Actor.CenterPosition,
					specialist.CenterPosition, target.CenterPosition) <= (long)range * range;
				}).OrderBy(threat => threat.Actor.ActorID).ToArray();
		}

		bool IsFavorableDemolitionFight(Actor specialist, Actor target, CommandoThreat[] threats)
		{
			if (threats.Length == 0)
				return false;

			var specialistValue = Math.Max(1, specialist.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1);
			var threatValue = 0;
			foreach (var threat in threats)
			{
				var isDemolitionObjective = threat.Actor == target && target.GetSellValue() <= specialistValue;
				var isFightableInfantry = threat.Actor.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes) &&
					StateBase.CanAttackTarget(specialist, threat.Actor);
				if (!isDemolitionObjective && !isFightableInfantry)
					return false;

				threatValue += threat.Value;
			}

			return threatValue <= specialistValue;
		}

		bool HasCheapDemolitionResponse(Actor specialist, Actor target, CommandoThreat[] threats)
		{
			if (IsTargetDeferred(specialist, target) ||
				!target.TraitsImplementing<IDemolishable>().Any(d => d.IsValidTarget(target, specialist)) ||
				!HasReachableDemolitionApproach(specialist, target))
				return false;

			var routeThreats = RouteThreats(specialist, target, threats);
			if (routeThreats.Length == 0 || IsFavorableDemolitionFight(specialist, target, routeThreats))
				return true;

			var mobile = specialist.TraitOrDefault<Mobile>();
			return mobile != null && Util.AdjacentCells(world, Target.FromActor(target)).Any(cell =>
				mobile.CanStayInCell(cell) && mobile.CanEnterCell(cell, check: BlockedByActor.Immovable) &&
				!IsThreatenedCell(specialist, cell, threats));
		}

		List<CPos> FindSafeDemolitionRoute(Actor specialist, Actor target, CommandoThreat[] threats)
		{
			var mobile = specialist.TraitOrDefault<Mobile>();
			if (mobile == null)
				return null;

			var approachCells = Util.AdjacentCells(world, Target.FromActor(target))
				.Where(cell => mobile.CanStayInCell(cell) &&
					mobile.CanEnterCell(cell, check: BlockedByActor.Immovable) &&
					!IsThreatenedCell(specialist, cell, threats)).ToHashSet();
			if (approachCells.Count == 0)
				return null;

			List<CPos> path;
			using (var search = PathSearch.ToTargetCellByPredicate(world, mobile.Locomotor, specialist,
				new[] { specialist.Location }, approachCells.Contains, BlockedByActor.Immovable,
				cell => IsThreatenedCell(specialist, cell, threats) ? PathGraph.PathCostForInvalidPath : 0))
				path = mobile.Pathfinder.FindPath(search);

			if (path.Count == 0)
				return null;

			path.Reverse();
			return path;
		}

		void QueueDemolitionApproach(IBot bot, Actor specialist, Actor target,
			DemolitionApproachResponse response, CommandoThreat[] threats, List<CPos> safeRoute)
		{
			var queued = false;
			if (response == DemolitionApproachResponse.FightThrough)
			{
				foreach (var threat in threats.Where(threat => threat.Actor != target &&
					threat.Actor.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes) &&
					StateBase.CanAttackTarget(specialist, threat.Actor)).OrderBy(threat => threat.Actor.ActorID))
				{
					bot.QueueOrder(new Order("Attack", specialist, Target.FromActor(threat.Actor), queued));
					queued = true;
				}
			}
			else if (response == DemolitionApproachResponse.RouteAround && safeRoute != null)
			{
				var spacing = Math.Max(1, Info.DemolitionRouteWaypointSpacing);
				for (var i = Math.Min(spacing, safeRoute.Count - 1); i < safeRoute.Count; i += spacing)
				{
					bot.QueueOrder(new Order("Move", specialist, Target.FromCell(world, safeRoute[i]), queued));
					queued = true;
				}

				if (safeRoute.Count > 1 && (safeRoute.Count - 1) % spacing != 0)
				{
					bot.QueueOrder(new Order("Move", specialist,
						Target.FromCell(world, safeRoute[safeRoute.Count - 1]), queued));
					queued = true;
				}
			}

			bot.QueueOrder(new Order("C4", specialist, Target.FromActor(target), queued)
			{
				ExtraData = Demolition.AutonomousOrderMarker
			});
		}

		void AssignCommandoFallback(IBot bot, Actor unit, CommandoThreat[] threats, Actor[] targets)
		{
			if (!IsUnownedIdleCommando(unit))
				return;

			var combatTarget = world.FindActorsInCircle(unit.CenterPosition,
				WDist.FromCells(Math.Max(1, Info.DemolitionFallbackCombatRadiusCells)))
				.Where(actor => !actor.IsDead && actor.IsInWorld &&
					player.RelationshipWith(actor.Owner) == PlayerRelationship.Enemy &&
					actor.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes) &&
					StateBase.CanAttackTarget(unit, actor) &&
					IsFavorableDemolitionFight(unit, actor, FallbackCombatThreats(unit, actor, threats)))
				.OrderBy(actor => DistanceSquared(unit, actor)).ThenBy(actor => actor.ActorID).FirstOrDefault();
			if (combatTarget != null)
			{
				bot.QueueOrder(new Order("Attack", unit, Target.FromActor(combatTarget), false));
				commandoFallbacks.Add(unit, new CommandoFallback(CommandoFallbackPurpose.Combat,
					combatTarget, CPos.Zero, world.WorldTick, world.WorldTick + StalledAssignmentTicks(
						SpecialistAssignmentPurpose.Demolition)));
				demolitionIdleSince.Remove(unit);
				Debug("commando {0}#{1} transition=favorable-combat target={2}#{3} ownership=recovered " +
					"threat=fightable-infantry", unit.Info.Name, unit.ActorID, combatTarget.Info.Name,
					combatTarget.ActorID);
				return;
			}

			var holdCell = FindSafeCommandoHoldCell(unit, threats);
			if (holdCell == null)
			{
				Debug("commando {0}#{1} transition=release reason=no-safe-hold ownership={2} " +
					"targets={3}", unit.Info.Name, unit.ActorID, OwnershipState(unit), targets.Length);
				return;
			}

			bot.QueueOrder(new Order("Move", unit, Target.FromCell(world, holdCell.Value), false));
			commandoFallbacks.Add(unit, new CommandoFallback(CommandoFallbackPurpose.Hold, null,
				holdCell.Value, world.WorldTick, world.WorldTick + Math.Max(1, Info.DemolitionHoldReconsiderTicks)));
			demolitionIdleSince.Remove(unit);
			Debug("commando {0}#{1} transition=safe-hold destination={2} ownership=recovered " +
				"reason=no-viable-demolition threat={3} reconsider={4}", unit.Info.Name, unit.ActorID,
				holdCell.Value, ThreatState(threats),
				world.WorldTick + Math.Max(1, Info.DemolitionHoldReconsiderTicks));
		}

		CommandoThreat[] FallbackCombatThreats(Actor unit, Actor target, CommandoThreat[] threats)
		{
			var routeThreats = RouteThreats(unit, target, threats);
			if (routeThreats.Any(threat => threat.Actor == target))
				return routeThreats;

			var range = target.TraitsImplementing<Armament>().Where(armament => !armament.IsTraitDisabled)
				.Select(armament => armament.MaxRange().Length).DefaultIfEmpty(0).Max();
			return range == 0 ? routeThreats : routeThreats.Append(new CommandoThreat(target, range))
				.OrderBy(threat => threat.Actor.ActorID).ToArray();
		}

		CPos? FindSafeCommandoHoldCell(Actor unit, CommandoThreat[] threats)
		{
			var mobile = unit.TraitOrDefault<Mobile>();
			if (mobile == null)
				return null;

			return world.Map.FindTilesInAnnulus(unit.Location, 2,
				Math.Max(2, Info.DemolitionHoldSearchRadiusCells))
				.Where(cell => mobile.CanStayInCell(cell) &&
					mobile.CanEnterCell(cell, check: BlockedByActor.Immovable) &&
					(domainIndex == null || domainIndex.IsPassable(unit.Location, cell, mobile.Locomotor)) &&
					!IsThreatenedCell(unit, cell, threats))
				.OrderByDescending(cell => threats.Select(threat =>
					(long)(world.Map.CenterOfCell(cell) - threat.Actor.CenterPosition).LengthSquared)
					.DefaultIfEmpty(long.MaxValue).Min())
				.ThenBy(cell => (cell - unit.Location).LengthSquared)
				.ThenBy(cell => cell.X).ThenBy(cell => cell.Y).Select(cell => (CPos?)cell).FirstOrDefault();
		}

		bool IsThreatenedCell(Actor unit, CPos cell, CommandoThreat[] threats)
		{
			var targetTypes = unit.GetEnabledTargetTypes();
			var position = world.Map.CenterOfCell(cell);
			foreach (var threat in threats)
			{
				if (!threat.Actor.TraitsImplementing<Armament>().Any(armament =>
					!armament.IsTraitDisabled && armament.Weapon.IsValidTarget(targetTypes)))
					continue;

				var range = threat.Range + WDist.FromCells(Math.Max(0, Info.DemolitionThreatBufferCells)).Length;
				if ((position - threat.Actor.CenterPosition).LengthSquared <= (long)range * range)
					return true;
			}

			return false;
		}

		void ReconsiderCommandoHolds(IBot bot, Actor[] targets, CommandoThreat[] threats)
		{
			foreach (var pair in commandoFallbacks.Where(pair =>
				pair.Value.Purpose == CommandoFallbackPurpose.Hold &&
				IsThreatenedCell(pair.Key, pair.Value.Destination, threats))
				.OrderBy(pair => pair.Key.ActorID).ToArray())
			{
				var unit = pair.Key;
				var previous = pair.Value;
				var destination = FindSafeCommandoHoldCell(unit, threats);
				if (destination == null && !IsThreatenedCell(unit, unit.Location, threats))
					destination = unit.Location;

				if (!CaptureTargeting.ShouldRerouteHold(destinationThreatened: true,
					safeDestinationFound: destination != null))
					continue;

				var order = destination == unit.Location ? "Stop" : "Move";
				bot.QueueOrder(destination == unit.Location ? new Order(order, unit, false) :
					new Order(order, unit, Target.FromCell(world, destination.Value), false));
				commandoFallbacks[unit] = new CommandoFallback(CommandoFallbackPurpose.Hold, null,
					destination.Value, world.WorldTick,
					world.WorldTick + Math.Max(1, Info.DemolitionHoldReconsiderTicks));
				Debug("commando {0}#{1} transition=hold-reroute reason=destination-threatened " +
					"old-destination={2} destination={3} reconsider={4}", unit.Info.Name, unit.ActorID,
					previous.Destination, destination.Value,
					world.WorldTick + Math.Max(1, Info.DemolitionHoldReconsiderTicks));
			}

			var due = commandoFallbacks.Where(pair => pair.Value.Purpose == CommandoFallbackPurpose.Hold &&
				world.WorldTick >= pair.Value.ReconsiderTick && pair.Key.IsIdle)
				.OrderBy(pair => pair.Key.ActorID).ToArray();
			if (due.Length == 0)
				return;

			var distances = new long[due.Length, targets.Length];
			var viable = new bool[due.Length, targets.Length];
			for (var unitIndex = 0; unitIndex < due.Length; unitIndex++)
			{
				var unit = due[unitIndex].Key;
				for (var targetIndex = 0; targetIndex < targets.Length; targetIndex++)
				{
					distances[unitIndex, targetIndex] = DistanceSquared(unit, targets[targetIndex]);
					viable[unitIndex, targetIndex] = HasViableDemolitionResponse(unit,
						targets[targetIndex], threats);
				}
			}

			var resumed = CaptureTargeting.TargetFirstDemolitionAllocation(distances, viable)
				.Select(allocation => due[allocation.Unit].Key).ToHashSet();
			foreach (var pair in due)
			{
				var unit = pair.Key;
				if (resumed.Contains(unit))
				{
					commandoFallbacks.Remove(unit);
					demolitionIdleSince[unit] = world.WorldTick - Math.Max(1, Info.DemolitionIdleConfirmationTicks);
					Debug("commando {0}#{1} transition=hold-resume reason=viable-objective " +
						"ownership=released-for-demolition", unit.Info.Name, unit.ActorID);
					continue;
				}

				pair.Value.ReconsiderTick = world.WorldTick + Math.Max(1, Info.DemolitionHoldReconsiderTicks);
				Debug("commando {0}#{1} transition=hold-continue reason=no-unreserved-safe-objective " +
					"destination={2} reconsider={3}", unit.Info.Name, unit.ActorID,
					pair.Value.Destination, pair.Value.ReconsiderTick);
			}
		}

		bool HasViableDemolitionResponse(Actor unit, Actor target, CommandoThreat[] threats)
		{
			if (IsTargetDeferred(unit, target) ||
				!target.TraitsImplementing<IDemolishable>().Any(d => d.IsValidTarget(target, unit)) ||
				!HasReachableDemolitionApproach(unit, target))
				return false;

			var routeThreats = RouteThreats(unit, target, threats);
			if (routeThreats.Length == 0 || IsFavorableDemolitionFight(unit, target, routeThreats))
				return true;

			return FindSafeDemolitionRoute(unit, target, threats) != null;
		}

		bool AuditCommandoFallbacks()
		{
			var reassess = false;
			foreach (var pair in commandoFallbacks.OrderBy(pair => pair.Key.ActorID).ToArray())
			{
				var unit = pair.Key;
				var fallback = pair.Value;
				var invalid = unitCannotBeOrdered(unit) || IsReservedForTransport(unit) ||
					(unitReservations != null && unitReservations.Any(r => r.IsUnitReserved(unit))) ||
					(temporaryUnitControls != null && temporaryUnitControls.Any(c => c.IsUnitTemporarilyControlled(unit)));
				var combatComplete = fallback.Purpose == CommandoFallbackPurpose.Combat &&
					(fallback.Target == null || fallback.Target.IsDead || !fallback.Target.IsInWorld ||
					player.RelationshipWith(fallback.Target.Owner) != PlayerRelationship.Enemy ||
					(unit.IsIdle && world.WorldTick - fallback.AssignedTick >= PendingOrderGraceTicks));
				if (!invalid && !combatComplete)
					continue;

				commandoFallbacks.Remove(unit);
				demolitionIdleSince.Remove(unit);
				reassess = true;
				Debug("commando {0}#{1} transition=release reason={2} ownership={3} purpose={4}",
					unit.Info.Name, unit.ActorID, invalid ? "owner-conflict" : "combat-complete",
					OwnershipState(unit), fallback.Purpose);
			}

			return reassess;
		}

		string OwnershipState(Actor actor)
		{
			if (unitCannotBeOrdered(actor))
				return "not-orderable";
			if (IsReservedForTransport(actor))
				return "transport";
			if (unitReservations != null && unitReservations.Any(r => r.IsUnitReserved(actor)))
				return "unit-reservation";
			if (temporaryUnitControls != null && temporaryUnitControls.Any(c => c.IsUnitTemporarilyControlled(actor)))
				return "temporary-control";
			return actor.IsIdle ? "ownerless-idle" : "activity";
		}

		static string ThreatState(CommandoThreat[] threats)
		{
			return threats == null || threats.Length == 0 ? "clear" : string.Format("persistent:{0}#{1}",
				threats[0].Actor.Info.Name, threats[0].Actor.ActorID);
		}

		bool HasReachableDemolitionApproach(Actor specialist, Actor target)
		{
			var mobile = specialist.TraitOrDefault<Mobile>();
			if (mobile == null || domainIndex == null)
				return true;

			foreach (var cell in Util.AdjacentCells(world, Target.FromActor(target)))
				if (mobile.CanStayInCell(cell) && mobile.CanEnterCell(cell, check: BlockedByActor.Immovable) &&
					domainIndex.IsPassable(specialist.Location, cell, mobile.Locomotor))
					return true;

			return false;
		}

		bool IsReservedForTransport(Actor actor)
		{
			return transportReservations != null && transportReservations.Any(r => r.IsTransportReserved(actor));
		}

		bool AuditAssignments(IBot bot, Dictionary<Actor, SpecialistAssignment> assignments, string action,
			SpecialistAssignmentPurpose purpose, HashSet<Actor> pendingRecovery)
		{
			var requestReassessment = false;
			while (true)
			{
				var invalid = default(KeyValuePair<Actor, SpecialistAssignment>);
				string result = null;
				foreach (var pair in assignments)
				{
					var candidateResult = AssignmentInvalidationReason(pair.Key, pair.Value, purpose);
					if (candidateResult == "pair-state-changed")
					{
						requestReassessment = true;
						continue;
					}

					if (candidateResult != null && (invalid.Key == null || pair.Key.ActorID < invalid.Key.ActorID))
					{
						invalid = pair;
						result = candidateResult;
					}
				}

				if (invalid.Key == null)
					return requestReassessment;

				var transportOwned = result == "transport-owned";
				var specialistLost = result == "specialist-lost";
				var completed = result == "captured" || result == "sabotaged";
				var recoverableCompletion = completed && !unitCannotBeOrdered(invalid.Key) &&
					!IsReservedForTransport(invalid.Key);
				var recoverableInvalidation = !transportOwned && !specialistLost && !completed;
				var stopSpecialist = (recoverableCompletion || recoverableInvalidation) && !invalid.Key.IsIdle;
				RetireAssignment(bot, assignments, invalid, action, purpose, result, stopSpecialist);
				if (recoverableCompletion || recoverableInvalidation)
					pendingRecovery.Add(invalid.Key);
			}
		}

		string AssignmentInvalidationReason(Actor specialist, SpecialistAssignment assignment,
			SpecialistAssignmentPurpose purpose)
		{
			var target = assignment.Target;
			if (target.IsDead || !target.IsInWorld)
				return "target-removed";

			var captureTransportActive = purpose == SpecialistAssignmentPurpose.Capture &&
				IsCaptureTransportActive(specialist);
			if (unitCannotBeOrdered(specialist) && !captureTransportActive)
				return "specialist-lost";

			// Cargo is briefly removed from the world while an unload frame-end task completes.
			// The active objective transport remains the exact reservation owner during that handoff.
			if (!specialist.IsInWorld && captureTransportActive)
				return null;

			var ownedRestoration = purpose == SpecialistAssignmentPurpose.Capture &&
				IsOwnedRestorableHuskTarget(target, specialist);
			if (target.Owner == player && !ownedRestoration)
				return purpose == SpecialistAssignmentPurpose.Capture ? "captured" : "sabotaged";

			if (IsReservedForTransport(specialist) && !captureTransportActive)
				return "transport-owned";

			if (!HasValidRelationship(target, purpose, specialist))
				return "relationship-invalid";

			if (!targetReservations.Matches(specialist.ActorID, target.ActorID, purpose,
				assignment.MaximumClaimants))
				return "reservation-mismatch";

			if (purpose == SpecialistAssignmentPurpose.Capture)
			{
				if (ConsumeTimedOutCaptureTransport(specialist, target))
					return "transport-handoff-timeout";

				var captureManager = specialist.TraitOrDefault<CaptureManager>();
				if (captureManager == null || !CanCaptureByRules(
					new TraitPair<CaptureManager>(specialist, captureManager), target))
					return "capture-ineligible";

				var expectedClaimants = RequiresPair(new CaptureCandidate(target, CaptureEconomicValue(target))) ? 2 : 1;
				if (assignment.MaximumClaimants != expectedClaimants)
					return "pair-state-changed";

				if (!HasReachableCaptureApproach(specialist, target) && !captureTransportActive)
					return "unreachable-approach";

				if (captureTransportActive)
				{
					assignment.ObserveProgress(specialist, world.WorldTick);
					return null;
				}
			}

			var expectedActivity = HasExpectedActivity(specialist, purpose) ||
				(purpose == SpecialistAssignmentPurpose.Demolition &&
					HasPendingAutonomousDemolition(target, specialist));
			assignment.ObserveActivity(expectedActivity, world.WorldTick);
			if (!expectedActivity)
				return CaptureTargeting.ActivityGraceExpired(assignment.MissingActivitySinceTick,
					world.WorldTick, PendingOrderGraceTicks) ? "missing-activity" : null;

			assignment.ObserveProgress(specialist, world.WorldTick);
			return world.WorldTick - assignment.LastProgressTick > StalledAssignmentTicks(purpose) ?
				"non-progressing" : null;
		}

		static bool HasPendingAutonomousDemolition(Actor target, Actor specialist)
		{
			foreach (var demolishable in target.TraitsImplementing<Demolishable>())
				if (demolishable.HasPendingAutonomousDemolition(specialist))
					return true;

			return false;
		}

		void RetireAssignment(IBot bot, Dictionary<Actor, SpecialistAssignment> assignments,
			KeyValuePair<Actor, SpecialistAssignment> pair, string action, SpecialistAssignmentPurpose purpose,
			string result = null, bool stopSpecialist = false)
		{
			var target = pair.Value.Target;
			var targetRemoved = target.IsDead || !target.IsInWorld;
			var targetHealth = targetRemoved ? 0 : target.TraitOrDefault<IHealth>()?.HP ?? 0;
			var relationshipInvalid = !targetRemoved && !unitCannotBeOrdered(pair.Key) &&
				!HasValidRelationship(target, purpose, pair.Key);
			var nonProgressing = world.WorldTick - pair.Value.LastProgressTick > StalledAssignmentTicks(purpose);
			result = result ?? (targetRemoved ? "target-removed" :
				target.Owner == player ? "captured" :
				targetHealth < pair.Value.TargetHealth ? "sabotaged" :
				pair.Key.IsDead || !pair.Key.IsInWorld ? "specialist-lost" :
				relationshipInvalid ? "relationship-invalid" :
				nonProgressing ? "non-progressing" : "specialist-idle");
			DebugAssignmentRelease(action, pair, purpose, result);
			if (result == "non-progressing")
			{
				deferredTargets[pair.Key] = new DeferredTarget(target,
					world.WorldTick + StalledAssignmentTicks(purpose));
			}
			else if (result == "transport-handoff-timeout")
			{
				// The same unloaded Engineer has already spent its bounded objective handoff.
				// Keep it available for other work, but do not recreate an identical stalled order
				// until this exact live target disappears.
				deferredTargets[pair.Key] = new DeferredTarget(target, int.MaxValue);
			}

			if (purpose == SpecialistAssignmentPurpose.Capture)
				CancelCaptureTransport(pair.Key);

			if (stopSpecialist && !unitCannotBeOrdered(pair.Key))
				bot.QueueOrder(new Order("Stop", pair.Key, false));

			assignments.Remove(pair.Key);
			targetReservations.Release(pair.Key.ActorID);
		}

		void DebugAssignmentRelease(string action, KeyValuePair<Actor, SpecialistAssignment> pair,
			SpecialistAssignmentPurpose purpose, string result)
		{
			if (!Info.DebugLogging)
				return;

			var specialist = pair.Key;
			var assignment = pair.Value;
			var target = assignment.Target;
			var hasReservation = targetReservations.TryGetReservation(specialist.ActorID,
				out var reservedTarget, out var reservedPurpose);
			var claimants = string.Join(",", targetReservations.Claimants(target.ActorID));
			var activity = specialist.CurrentActivity == null ? "none" : specialist.CurrentActivity.GetType().Name;
			var relationship = target.IsDead || !target.IsInWorld ? "none" :
				player.RelationshipWith(target.Owner).ToString();
			Debug("{0} {1}#{2} released from {3}#{4}: result={5}, idle={6}, activity={7}, " +
				"target-live={8}, target-owner={9}, relationship={10}, last-progress={11}, " +
				"reservation={12}/{13}/{14}, expected-purpose={15}, expected-claimants={16}, " +
				"target-claimants=[{17}], transport-owned={18}", action, specialist.Info.Name,
				specialist.ActorID, target.Info.Name, target.ActorID, result, specialist.IsIdle, activity,
				!target.IsDead && target.IsInWorld, target.Owner.InternalName, relationship,
				assignment.LastProgressTick, hasReservation, reservedTarget,
				hasReservation ? reservedPurpose.ToString() : "none", purpose, assignment.MaximumClaimants,
				claimants, IsReservedForTransport(specialist));
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

			if (purpose == SpecialistAssignmentPurpose.Capture)
			{
				foreach (var activity in actor.CurrentActivity.ActivitiesImplementing<Activities.CaptureActor>())
					return true;
			}
			else
			{
				foreach (var activity in actor.CurrentActivity.ActivitiesImplementing<Activities.Demolish>())
					return true;
			}

			return false;
		}

		bool HasValidRelationship(Actor target, SpecialistAssignmentPurpose purpose, Actor specialist = null)
		{
			if (target.IsDead || !target.IsInWorld)
				return false;

			var relationship = player.RelationshipWith(target.Owner);
			return purpose == SpecialistAssignmentPurpose.Capture ?
				Info.CapturableRelationships.HasRelationship(relationship) ||
					IsOwnedRestorableHuskTarget(target, specialist) :
				relationship == PlayerRelationship.Enemy;
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			var assignments = activeCapturers
				.Select(pair => new { Pair = pair, Purpose = SpecialistAssignmentPurpose.Capture })
				.Concat(activeDemolitionUnits.Select(pair =>
					new { Pair = pair, Purpose = SpecialistAssignmentPurpose.Demolition }))
				.OrderBy(saved => saved.Pair.Value.Target.ActorID)
				.ThenBy(saved => saved.Pair.Key.ActorID)
				.Select(saved => SaveAssignment(saved.Pair, saved.Purpose))
				.ToList();

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("CaptureManagerCaptureScanTicks", FieldSaver.FormatValue(minCaptureDelayTicks)),
				new MiniYamlNode("CaptureManagerDemolitionScanTicks", FieldSaver.FormatValue(minDemolitionDelayTicks)),
				new MiniYamlNode("CaptureManagerAssignments", "", assignments),
				new MiniYamlNode("CaptureManagerCommandoConfirmations", "", demolitionIdleSince
					.OrderBy(pair => pair.Key.ActorID).Select(pair => SaveCommandoConfirmation(
						new SavedCommandoConfirmation { SpecialistId = pair.Key.ActorID, IdleSinceTick = pair.Value }))
					.ToList()),
				new MiniYamlNode("CaptureManagerCommandoFallbacks", "", commandoFallbacks
					.OrderBy(pair => pair.Key.ActorID).Select(pair => SaveCommandoFallback(
						new SavedCommandoFallback
						{
							SpecialistId = pair.Key.ActorID,
							Purpose = pair.Value.Purpose,
							TargetId = pair.Value.Target?.ActorID ?? 0,
							Destination = pair.Value.Destination,
							AssignedTick = pair.Value.AssignedTick,
							ReconsiderTick = pair.Value.ReconsiderTick
						})).ToList()),
				new MiniYamlNode("CaptureManagerDeferredTargets", "", deferredTargets.OrderBy(pair => pair.Key.ActorID).Select(pair =>
					new MiniYamlNode("DeferredTarget", "", new List<MiniYamlNode>
					{
						new MiniYamlNode("Specialist", FieldSaver.FormatValue(pair.Key.ActorID)),
						new MiniYamlNode("Target", FieldSaver.FormatValue(pair.Value.Target.ActorID)),
						new MiniYamlNode("RetryTick", FieldSaver.FormatValue(pair.Value.RetryTick))
					})).ToList())
			};
		}

		static MiniYamlNode SaveAssignment(KeyValuePair<Actor, SpecialistAssignment> pair,
			SpecialistAssignmentPurpose purpose)
		{
			return new MiniYamlNode("Assignment", "", new List<MiniYamlNode>
			{
				new MiniYamlNode("Specialist", FieldSaver.FormatValue(pair.Key.ActorID)),
				new MiniYamlNode("Target", FieldSaver.FormatValue(pair.Value.Target.ActorID)),
				new MiniYamlNode("Purpose", FieldSaver.FormatValue((int)purpose)),
				new MiniYamlNode("MaximumClaimants", FieldSaver.FormatValue(pair.Value.MaximumClaimants)),
				new MiniYamlNode("TargetHealth", FieldSaver.FormatValue(pair.Value.TargetHealth)),
				new MiniYamlNode("AssignedTick", FieldSaver.FormatValue(pair.Value.AssignedTick)),
				new MiniYamlNode("LastSpecialistPosition", FieldSaver.FormatValue(pair.Value.LastSpecialistPosition)),
				new MiniYamlNode("LastTargetHealth", FieldSaver.FormatValue(pair.Value.LastTargetHealth)),
				new MiniYamlNode("LastProgressTick", FieldSaver.FormatValue(pair.Value.LastProgressTick)),
				new MiniYamlNode("MissingActivitySinceTick", FieldSaver.FormatValue(pair.Value.MissingActivitySinceTick))
			});
		}

		static MiniYamlNode SaveCommandoConfirmation(SavedCommandoConfirmation saved)
		{
			return new MiniYamlNode("Confirmation", "", new List<MiniYamlNode>
			{
				new MiniYamlNode("Specialist", FieldSaver.FormatValue(saved.SpecialistId)),
				new MiniYamlNode("IdleSinceTick", FieldSaver.FormatValue(saved.IdleSinceTick))
			});
		}

		static MiniYamlNode SaveCommandoFallback(SavedCommandoFallback saved)
		{
			return new MiniYamlNode("Fallback", "", new List<MiniYamlNode>
			{
				new MiniYamlNode("Specialist", FieldSaver.FormatValue(saved.SpecialistId)),
				new MiniYamlNode("Purpose", FieldSaver.FormatValue((int)saved.Purpose)),
				new MiniYamlNode("Target", FieldSaver.FormatValue(saved.TargetId)),
				new MiniYamlNode("Destination", FieldSaver.FormatValue(saved.Destination)),
				new MiniYamlNode("AssignedTick", FieldSaver.FormatValue(saved.AssignedTick)),
				new MiniYamlNode("ReconsiderTick", FieldSaver.FormatValue(saved.ReconsiderTick))
			});
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var savedAssignments = new List<SavedAssignment>();
			var savedDeferredTargets = new List<MiniYamlNode>();
			var savedCommandoConfirmations = new List<SavedCommandoConfirmation>();
			var savedCommandoFallbacks = new List<SavedCommandoFallback>();
			foreach (var node in data)
				switch (node.Key)
				{
					case "CaptureManagerCaptureScanTicks":
						minCaptureDelayTicks = FieldLoader.GetValue<int>(node.Key, node.Value.Value);
						break;
					case "CaptureManagerDemolitionScanTicks":
						minDemolitionDelayTicks = FieldLoader.GetValue<int>(node.Key, node.Value.Value);
						break;
					case "CaptureManagerAssignments":
						savedAssignments.AddRange(node.Value.Nodes.Select(LoadAssignment));
						break;
					case "CaptureManagerCommandoConfirmations":
						savedCommandoConfirmations.AddRange(node.Value.Nodes.Select(LoadCommandoConfirmation));
						break;
					case "CaptureManagerCommandoFallbacks":
						savedCommandoFallbacks.AddRange(node.Value.Nodes.Select(LoadCommandoFallback));
						break;
					case "CaptureManagerDeferredTargets":
						savedDeferredTargets.AddRange(node.Value.Nodes);
						break;
				}

			RestoreAssignments(savedAssignments);
			RestoreDeferredTargets(savedDeferredTargets);
			RestoreCommandoOwnership(savedCommandoFallbacks, savedCommandoConfirmations);
		}

		static SavedCommandoConfirmation LoadCommandoConfirmation(MiniYamlNode node)
		{
			return new SavedCommandoConfirmation
			{
				SpecialistId = LoadId(node, "Specialist"),
				IdleSinceTick = LoadValue<int>(node, "IdleSinceTick")
			};
		}

		static SavedCommandoFallback LoadCommandoFallback(MiniYamlNode node)
		{
			return new SavedCommandoFallback
			{
				SpecialistId = LoadId(node, "Specialist"),
				Purpose = (CommandoFallbackPurpose)LoadValue<int>(node, "Purpose"),
				TargetId = LoadId(node, "Target"),
				Destination = LoadValue<CPos>(node, "Destination"),
				AssignedTick = LoadValue<int>(node, "AssignedTick"),
				ReconsiderTick = LoadValue<int>(node, "ReconsiderTick")
			};
		}

		static SavedAssignment LoadAssignment(MiniYamlNode node)
		{
			T Load<T>(string key)
			{
				var value = node.Value.Nodes.First(n => n.Key == key);
				return FieldLoader.GetValue<T>(key, value.Value.Value);
			}

			T LoadOrDefault<T>(string key, T fallback)
			{
				var value = node.Value.Nodes.FirstOrDefault(n => n.Key == key);
				return value == null ? fallback : FieldLoader.GetValue<T>(key, value.Value.Value);
			}

			var assignedTick = Load<int>("AssignedTick");

			return new SavedAssignment
			{
				SpecialistId = Load<uint>("Specialist"),
				TargetId = Load<uint>("Target"),
				Purpose = (SpecialistAssignmentPurpose)Load<int>("Purpose"),
				MaximumClaimants = Load<int>("MaximumClaimants"),
				TargetHealth = Load<int>("TargetHealth"),
				AssignedTick = assignedTick,
				LastSpecialistPosition = Load<WPos>("LastSpecialistPosition"),
				LastTargetHealth = Load<int>("LastTargetHealth"),
				LastProgressTick = Load<int>("LastProgressTick"),
				MissingActivitySinceTick = LoadOrDefault("MissingActivitySinceTick", assignedTick)
			};
		}

		void RestoreAssignments(IEnumerable<SavedAssignment> savedAssignments)
		{
			activeCapturers.Clear();
			activeDemolitionUnits.Clear();

			var candidates = savedAssignments
				.Where(saved => Enum.IsDefined(typeof(SpecialistAssignmentPurpose), saved.Purpose))
				.Select(saved => new { Saved = saved, Specialist = world.GetActorById(saved.SpecialistId), Target = world.GetActorById(saved.TargetId) })
				.Where(candidate => IsValidRestoredAssignment(candidate.Specialist, candidate.Target, candidate.Saved))
				.ToArray();
			var restoredReservations = targetReservations.Restore(candidates.Select(candidate =>
				new SpecialistTargetReservationState(candidate.Saved.SpecialistId, candidate.Saved.TargetId,
					candidate.Saved.Purpose, candidate.Saved.MaximumClaimants)));
			var restoredKeys = restoredReservations.Select(reservation =>
				(reservation.SpecialistId, reservation.TargetId, reservation.Purpose)).ToHashSet();

			foreach (var candidate in candidates.OrderBy(candidate => candidate.Saved.SpecialistId))
			{
				var saved = candidate.Saved;
				if (!restoredKeys.Contains((saved.SpecialistId, saved.TargetId, saved.Purpose)))
					continue;

				var assignment = new SpecialistAssignment(candidate.Target, saved.TargetHealth, saved.AssignedTick,
					saved.MaximumClaimants, saved.LastSpecialistPosition, saved.LastTargetHealth, saved.LastProgressTick,
					saved.MissingActivitySinceTick);
				var assignments = saved.Purpose == SpecialistAssignmentPurpose.Capture ?
					activeCapturers : activeDemolitionUnits;
				assignments.Add(candidate.Specialist, assignment);
				Debug("restored {0} {1}#{2} -> {3}#{4}: assigned-tick={5}, last-progress={6}, claimants={7}",
					saved.Purpose.ToString().ToLowerInvariant(), candidate.Specialist.Info.Name, saved.SpecialistId,
					candidate.Target.Info.Name, saved.TargetId, saved.AssignedTick, saved.LastProgressTick,
					saved.MaximumClaimants);
			}
		}

		bool IsValidRestoredAssignment(Actor specialist, Actor target, SavedAssignment saved)
		{
			if (specialist == null || target == null || unitCannotBeOrdered(specialist) ||
				!HasValidRelationship(target, saved.Purpose, specialist))
				return false;

			var expectedActivity = CaptureTargeting.ShouldRestoreAssignmentActivity(
				HasExpectedActivity(specialist, saved.Purpose), saved.MissingActivitySinceTick,
				world.WorldTick, PendingOrderGraceTicks);
			if (!expectedActivity)
				return false;

			return saved.Purpose == SpecialistAssignmentPurpose.Capture ?
				Info.CapturingActorTypes.Contains(specialist.Info.Name) && specialist.Info.HasTraitInfo<CapturesInfo>() :
				Info.DemolitionActorTypes.Contains(specialist.Info.Name) && specialist.Info.HasTraitInfo<DemolitionInfo>();
		}

		void RestoreDeferredTargets(IEnumerable<MiniYamlNode> nodes)
		{
			deferredTargets.Clear();
			foreach (var node in nodes.OrderBy(node => LoadId(node, "Specialist")))
			{
				var specialist = world.GetActorById(LoadId(node, "Specialist"));
				var target = world.GetActorById(LoadId(node, "Target"));
				var retryTick = LoadValue<int>(node, "RetryTick");
				if (specialist == null || target == null || unitCannotBeOrdered(specialist) ||
					target.IsDead || !target.IsInWorld || retryTick <= world.WorldTick)
					continue;

				deferredTargets[specialist] = new DeferredTarget(target, retryTick);
			}
		}

		void RestoreCommandoOwnership(IEnumerable<SavedCommandoFallback> savedFallbacks,
			IEnumerable<SavedCommandoConfirmation> savedConfirmations)
		{
			commandoFallbacks.Clear();
			demolitionIdleSince.Clear();

			foreach (var saved in savedFallbacks.OrderBy(saved => saved.SpecialistId)
				.GroupBy(saved => saved.SpecialistId).Where(group => group.Count() == 1).Select(group => group.Single()))
			{
				var specialist = world.GetActorById(saved.SpecialistId);
				var target = saved.TargetId == 0 ? null : world.GetActorById(saved.TargetId);
				if (!IsValidRestoredCommandoFallback(specialist, target, saved))
					continue;

				commandoFallbacks.Add(specialist, new CommandoFallback(saved.Purpose, target,
					saved.Destination, saved.AssignedTick, saved.ReconsiderTick));
				Debug("restored commando {0}#{1} ownership=fallback purpose={2} target={3} " +
					"destination={4} assigned={5} reconsider={6}", specialist.Info.Name, specialist.ActorID,
					saved.Purpose, saved.TargetId, saved.Destination, saved.AssignedTick, saved.ReconsiderTick);
			}

			foreach (var saved in savedConfirmations.OrderBy(saved => saved.SpecialistId)
				.GroupBy(saved => saved.SpecialistId).Where(group => group.Count() == 1).Select(group => group.Single()))
			{
				var specialist = world.GetActorById(saved.SpecialistId);
				if (specialist == null || saved.IdleSinceTick > world.WorldTick || !IsUnownedIdleCommando(specialist))
					continue;

				demolitionIdleSince.Add(specialist, saved.IdleSinceTick);
				Debug("restored commando {0}#{1} ownership=candidate-ownerless idle-since={2}",
					specialist.Info.Name, specialist.ActorID, saved.IdleSinceTick);
			}
		}

		bool IsValidRestoredCommandoFallback(Actor specialist, Actor target, SavedCommandoFallback saved)
		{
			if (specialist == null || saved.AssignedTick > world.WorldTick ||
				!Enum.IsDefined(typeof(CommandoFallbackPurpose), saved.Purpose) ||
				specialist.Owner != player || specialist.IsDead || !specialist.IsInWorld ||
				unitCannotBeOrdered(specialist) || !Info.DemolitionActorTypes.Contains(specialist.Info.Name) ||
				!specialist.Info.HasTraitInfo<DemolitionInfo>() || activeCapturers.ContainsKey(specialist) ||
				activeDemolitionUnits.ContainsKey(specialist) ||
				targetReservations.TryGetReservation(specialist.ActorID, out _, out _) ||
				IsReservedForTransport(specialist) ||
				(unitReservations != null && unitReservations.Any(r => r.IsUnitReserved(specialist))) ||
				(temporaryUnitControls != null && temporaryUnitControls.Any(c => c.IsUnitTemporarilyControlled(specialist))))
				return false;

			if (saved.Purpose == CommandoFallbackPurpose.Combat)
				return target != null && !target.IsDead && target.IsInWorld &&
					player.RelationshipWith(target.Owner) == PlayerRelationship.Enemy &&
					target.GetEnabledTargetTypes().Overlaps(InfantryTargetTypes) &&
					StateBase.CanAttackTarget(specialist, target);

			var mobile = specialist.TraitOrDefault<Mobile>();
			return saved.TargetId == 0 && world.Map.Contains(saved.Destination) && mobile != null &&
				mobile.CanStayInCell(saved.Destination);
		}

		static uint LoadId(MiniYamlNode node, string key)
		{
			return LoadValue<uint>(node, key);
		}

		static T LoadValue<T>(MiniYamlNode node, string key)
		{
			var value = node.Value.Nodes.First(n => n.Key == key);
			return FieldLoader.GetValue<T>(key, value.Value.Value);
		}

		void Debug(string format, params object[] args)
		{
			if (Info.DebugLogging)
				Log.Write("debug", "AI ({0}) capture manager at tick {1}: {2}", player.ClientIndex,
					world.WorldTick, string.Format(format, args));
		}
	}
}
