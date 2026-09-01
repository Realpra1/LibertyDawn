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
	/// <summary>
	/// Disabled live-only owner for lifecycle Engagement/Crush. Strategic and actor caches are
	/// deliberately absent: target identity, position, safety, orders, and transitions come from
	/// one current live snapshot and remain bound to this owner's exact epoch.
	/// </summary>
	public sealed class StealthCrushBehavior
	{
		public const int RefreshIntervalTicks = 125;

		sealed class OwnerState
		{
			public int LastObservedTick { get; set; } = -1;
			public uint? SelectedTargetActorId { get; set; }
			public CPos? SelectedTargetCurrentCell { get; set; }
			public int LastRefreshTick { get; set; } = -1;
			public int NextRefreshTick { get; set; } = -1;
			public StealthCrushThreatFacts ThreatFacts { get; set; }
			public StealthCrushSafetyResult? Safety { get; set; }
			public StealthCrushDisposition Disposition { get; set; } = StealthCrushDisposition.Reacquire;
			public uint[] LastIssuedActorIds { get; set; } = Array.Empty<uint>();
			public uint? LastIssuedTargetActorId { get; set; }
			public CPos? LastIssuedTargetCurrentCell { get; set; }
			public uint[] LiveDefenderActorIds { get; set; } = Array.Empty<uint>();
			public uint[] LiveObjectiveActorIds { get; set; } = Array.Empty<uint>();

			public OwnerState Clone()
			{
				return new OwnerState
				{
					LastObservedTick = LastObservedTick,
					SelectedTargetActorId = SelectedTargetActorId,
					SelectedTargetCurrentCell = SelectedTargetCurrentCell,
					LastRefreshTick = LastRefreshTick,
					NextRefreshTick = NextRefreshTick,
					ThreatFacts = ThreatFacts,
					Safety = Safety,
					Disposition = Disposition,
					LastIssuedActorIds = LastIssuedActorIds.ToArray(),
					LastIssuedTargetActorId = LastIssuedTargetActorId,
					LastIssuedTargetCurrentCell = LastIssuedTargetCurrentCell,
					LiveDefenderActorIds = LiveDefenderActorIds.ToArray(),
					LiveObjectiveActorIds = LiveObjectiveActorIds.ToArray()
				};
			}
		}

		readonly StealthCrushEvaluationHandoff handoff;
		readonly StealthApproachMission mission;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthCrushLiveWorld liveWorld;
		readonly IStealthCrushThreatAdapter threatAdapter;
		readonly IStealthCrushOrders orders;
		readonly StealthBehaviorExecutionLease executionLease = new StealthBehaviorExecutionLease();
		OwnerState state = new OwnerState();

		public StealthCrushBehavior(StealthCrushEvaluationHandoff handoff,
			IStealthLifecycleOwnershipGuard ownershipGuard, IStealthCrushLiveWorld liveWorld,
			IStealthCrushThreatAdapter threatAdapter, IStealthCrushOrders orders)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.CrushEvaluation)
				throw new ArgumentException("Crush requires Crush ownership.", nameof(handoff));
			mission = handoff.Mission ?? throw new ArgumentException(
				"Crush requires one immutable mission.", nameof(handoff));
			this.ownershipGuard = ownershipGuard ?? throw new ArgumentNullException(nameof(ownershipGuard));
			this.liveWorld = liveWorld ?? throw new ArgumentNullException(nameof(liveWorld));
			this.threatAdapter = threatAdapter ?? throw new ArgumentNullException(nameof(threatAdapter));
			this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
		}

		public StealthCrushResult Execute()
		{
			var revision = executionLease.Acquire("Crush", EnsureActiveOwnership);
			try
			{
				return Execute(revision);
			}
			finally
			{
				executionLease.Release(revision);
			}
		}

		StealthCrushResult Execute(long revision)
		{
			var live = ReadLive("execute", revision);
			var prospective = state.Clone();
			if (live.Tick < prospective.LastObservedTick)
				throw new InvalidOperationException("Live Crush ticks must not move backwards.");
			prospective.LastObservedTick = live.Tick;

			var decision = StealthCrushLiveDecision.Create(live, mission);
			prospective.LiveDefenderActorIds = decision.DefenderActorIds.ToArray();
			prospective.LiveObjectiveActorIds = decision.ObjectiveActorIds.ToArray();
			if (decision.TargetlessDisposition.HasValue)
			{
				ClearSelection(prospective);
				prospective.Disposition = decision.TargetlessDisposition.Value;
				return CommitAndResult(prospective, decision.Members, revision);
			}

			var resolution = decision.ResolveTarget(prospective.SelectedTargetActorId,
				prospective.SelectedTargetCurrentCell, prospective.NextRefreshTick);
			var selected = resolution.Selected;
			if (resolution.RequiresRefresh)
				RefreshSelection(prospective, selected, live.Tick);

			var facts = decision.ThreatFacts(selected);
			var safety = CalculateSafety(facts, revision);
			prospective.ThreatFacts = facts;
			prospective.Safety = safety;
			if (!safety.Approved)
			{
				ClearOrderDeduplication(prospective);
				prospective.Disposition = StealthCrushDisposition.Kite;
				return CommitAndResult(prospective, decision.Members, revision);
			}

			var memberIds = decision.Members.Select(member => member.ActorId).ToArray();
			if (prospective.LastIssuedTargetActorId != selected.ActorId ||
				prospective.LastIssuedTargetCurrentCell != selected.CurrentCell ||
				!prospective.LastIssuedActorIds.SequenceEqual(memberIds))
			{
				IssueCrush(memberIds, selected.ActorId, selected.CurrentCell, revision);
				prospective.LastIssuedActorIds = memberIds.ToArray();
				prospective.LastIssuedTargetActorId = selected.ActorId;
				prospective.LastIssuedTargetCurrentCell = selected.CurrentCell;
			}

			prospective.Disposition = StealthCrushDisposition.Retain;
			return CommitAndResult(prospective, decision.Members, revision);
		}

		public MiniYamlNode SerializePrivateState(string key = "Crush")
		{
			return StealthCrushPersistence.Serialize(key, handoff, mission,
				state.LastObservedTick, state.SelectedTargetActorId, state.SelectedTargetCurrentCell,
				state.LastRefreshTick, state.NextRefreshTick, state.ThreatFacts, state.Safety,
				state.Disposition, state.LastIssuedActorIds, state.LastIssuedTargetActorId,
				state.LastIssuedTargetCurrentCell, state.LiveDefenderActorIds,
				state.LiveObjectiveActorIds);
		}

		public void RestorePrivateState(MiniYamlNode node)
		{
			var revision = executionLease.Acquire("Crush", EnsureActiveOwnership);
			try
			{
				var restored = StealthCrushPersistence.Restore(node, handoff, mission);
				var live = ReadLive("restore", revision);
				ValidateRestoredLiveState(restored, live, revision);
				var prospective = new OwnerState
				{
					LastObservedTick = restored.LastObservedTick,
					SelectedTargetActorId = restored.SelectedTargetActorId,
					SelectedTargetCurrentCell = restored.SelectedTargetCurrentCell,
					LastRefreshTick = restored.LastRefreshTick,
					NextRefreshTick = restored.NextRefreshTick,
					ThreatFacts = restored.ThreatFacts,
					Safety = restored.Safety,
					Disposition = restored.Disposition,
					LastIssuedActorIds = restored.LastIssuedActorIds.ToArray(),
					LastIssuedTargetActorId = restored.LastIssuedTargetActorId,
					LastIssuedTargetCurrentCell = restored.LastIssuedTargetCurrentCell,
					LiveDefenderActorIds = restored.LiveDefenderActorIds.ToArray(),
					LiveObjectiveActorIds = restored.LiveObjectiveActorIds.ToArray()
				};
				executionLease.Commit(revision, "Crush", EnsureActiveOwnership,
					() => state = prospective);
			}
			finally
			{
				executionLease.Release(revision);
			}
		}

		void ValidateRestoredLiveState(StealthCrushPrivateState restored,
			StealthCrushLiveSnapshot live, long revision)
		{
			if (live.Tick < restored.LastObservedTick ||
				(restored.SelectedTargetActorId.HasValue && restored.LastRefreshTick > live.Tick))
				throw new InvalidOperationException("Saved Crush timing is ahead of the live World.");
			var decision = StealthCrushLiveDecision.Create(live, mission);
			if (!restored.LiveDefenderActorIds.SequenceEqual(decision.DefenderActorIds) ||
				!restored.LiveObjectiveActorIds.SequenceEqual(decision.ObjectiveActorIds))
				throw new InvalidOperationException("Saved Crush classification is not current live state.");

			if (decision.TargetlessDisposition.HasValue)
			{
				if (restored.Disposition != decision.TargetlessDisposition.Value ||
					restored.SelectedTargetActorId.HasValue || restored.Safety.HasValue)
					throw new InvalidOperationException("Saved Crush disposition has no current live cause.");
				return;
			}

			var resolution = decision.ResolveTarget(restored.SelectedTargetActorId,
				restored.SelectedTargetCurrentCell, restored.NextRefreshTick);
			var selected = resolution.Selected;
			if (resolution.RequiresRefresh || restored.SelectedTargetActorId != selected.ActorId ||
				restored.SelectedTargetCurrentCell != selected.CurrentCell)
				throw new InvalidOperationException("Saved Crush target or refresh state is not current live state.");
			if (restored.LastIssuedTargetActorId.HasValue && !restored.LastIssuedActorIds.SequenceEqual(
				decision.Members.Select(member => member.ActorId)))
				throw new InvalidOperationException("Saved Crush order actors do not match the live squad.");

			if (!restored.Safety.HasValue || restored.ThreatFacts == null)
				throw new InvalidOperationException("Saved Crush target has no complete live safety context.");
			var currentFacts = decision.ThreatFacts(selected);
			if (!SameFacts(restored.ThreatFacts, currentFacts))
				throw new InvalidOperationException("Saved Crush threat context is not current live state.");
			var currentSafety = CalculateSafety(currentFacts, revision);
			if (!SameSafety(restored.Safety.Value, currentSafety))
				throw new InvalidOperationException("Saved Crush safety does not match the standard live result.");
			var expectedDisposition = currentSafety.Approved ?
				StealthCrushDisposition.Retain : StealthCrushDisposition.Kite;
			if (restored.Disposition != expectedDisposition)
				throw new InvalidOperationException("Saved Crush disposition has no current live safety cause.");
		}

		StealthCrushLiveSnapshot ReadLive(string operation, long revision)
		{
			executionLease.Verify(revision, "Crush", EnsureActiveOwnership);
			var live = liveWorld.Read(mission) ?? throw new InvalidOperationException(
				"The live Crush view returned no snapshot during " + operation + ".");
			executionLease.Verify(revision, "Crush", EnsureActiveOwnership);
			return live;
		}

		StealthCrushSafetyResult CalculateSafety(StealthCrushThreatFacts facts, long revision)
		{
			executionLease.Verify(revision, "Crush", EnsureActiveOwnership);
			var calculated = threatAdapter.Calculate(facts);
			executionLease.Verify(revision, "Crush", EnsureActiveOwnership);
			return facts.FormationCloaked && !facts.HasDetectorCoverage &&
				facts.RemainCloakedAction && !facts.PlannedActionRevealsFormation ? calculated :
				new StealthCrushSafetyResult(calculated.Score, false);
		}

		void IssueCrush(IReadOnlyList<uint> actorIds, uint targetActorId,
			CPos targetCurrentCell, long revision)
		{
			executionLease.Verify(revision, "Crush", EnsureActiveOwnership);
			orders.IssueCrush(handoff.Owner, handoff.Epoch,
				Array.AsReadOnly(actorIds.ToArray()), targetActorId, targetCurrentCell);
			executionLease.Verify(revision, "Crush", EnsureActiveOwnership);
		}

		StealthCrushResult CommitAndResult(OwnerState prospective,
			IEnumerable<StealthCrushMemberSnapshot> members, long revision)
		{
			var result = new StealthCrushResult(handoff.Handoff, mission, prospective.Disposition,
				prospective.SelectedTargetActorId, prospective.SelectedTargetCurrentCell,
				members.Select(member => member.ActorId), prospective.LiveDefenderActorIds,
				prospective.LiveObjectiveActorIds, prospective.Safety);
			executionLease.Commit(revision, "Crush", EnsureActiveOwnership,
				() => state = prospective);
			return result;
		}

		static bool SameFacts(StealthCrushThreatFacts saved, StealthCrushThreatFacts current)
		{
			return saved.SelectedTargetActorId == current.SelectedTargetActorId &&
				saved.SelectedTargetCurrentCell == current.SelectedTargetCurrentCell &&
				saved.FriendlyActorIds.SequenceEqual(current.FriendlyActorIds) &&
				saved.EnemyActorIds.SequenceEqual(current.EnemyActorIds) &&
				saved.FormationCloaked == current.FormationCloaked &&
				saved.HasDetectorCoverage == current.HasDetectorCoverage &&
				saved.RemainCloakedAction == current.RemainCloakedAction &&
				saved.PlannedActionRevealsFormation == current.PlannedActionRevealsFormation;
		}

		static bool SameSafety(StealthCrushSafetyResult saved, StealthCrushSafetyResult current)
		{
			return saved.Score.ThreatRating.Equals(current.Score.ThreatRating) &&
				saved.Score.Crossover.Equals(current.Score.Crossover) &&
				saved.Approved == current.Approved;
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException(
					"Stale Crush ownership cannot execute or restore state.");
		}

		static void RefreshSelection(OwnerState prospective,
			StealthCrushActorSnapshot selected, int liveTick)
		{
			prospective.SelectedTargetActorId = selected.ActorId;
			prospective.SelectedTargetCurrentCell = selected.CurrentCell;
			prospective.LastRefreshTick = liveTick;
			prospective.NextRefreshTick = checked(liveTick + RefreshIntervalTicks);
			prospective.ThreatFacts = null;
			prospective.Safety = null;
			ClearOrderDeduplication(prospective);
		}

		static void ClearSelection(OwnerState prospective)
		{
			prospective.SelectedTargetActorId = null;
			prospective.SelectedTargetCurrentCell = null;
			prospective.LastRefreshTick = -1;
			prospective.NextRefreshTick = -1;
			prospective.ThreatFacts = null;
			prospective.Safety = null;
			ClearOrderDeduplication(prospective);
		}

		static void ClearOrderDeduplication(OwnerState prospective)
		{
			prospective.LastIssuedActorIds = Array.Empty<uint>();
			prospective.LastIssuedTargetActorId = null;
			prospective.LastIssuedTargetCurrentCell = null;
		}
	}
}
