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
	/// Disabled live-only owner for lifecycle Engagement/UndefendedAttack. It owns target
	/// selection, local safety, attack orders, and transitions until it explicitly yields.
	/// </summary>
	public sealed class StealthUndefendedAttackBehavior
	{
		public const int RefreshIntervalTicks = 125;

		sealed class OwnerState
		{
			public uint? SelectedTargetActorId { get; set; }
			public int LastRefreshTick { get; set; } = -1;
			public int NextRefreshTick { get; set; } = -1;
			public StealthUndefendedAttackSafetyResult? Safety { get; set; }
			public StealthUndefendedAttackDisposition Disposition { get; set; } =
				StealthUndefendedAttackDisposition.Retain;
			public uint[] LastIssuedActorIds { get; set; } = Array.Empty<uint>();
			public uint? LastIssuedTargetActorId { get; set; }
			public uint[] LiveDefenderActorIds { get; set; } = Array.Empty<uint>();

			public OwnerState Clone()
			{
				return new OwnerState
				{
					SelectedTargetActorId = SelectedTargetActorId,
					LastRefreshTick = LastRefreshTick,
					NextRefreshTick = NextRefreshTick,
					Safety = Safety,
					Disposition = Disposition,
					LastIssuedActorIds = LastIssuedActorIds.ToArray(),
					LastIssuedTargetActorId = LastIssuedTargetActorId,
					LiveDefenderActorIds = LiveDefenderActorIds.ToArray()
				};
			}
		}

		readonly StealthUndefendedAttackHandoff handoff;
		readonly StealthApproachMission mission;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthUndefendedAttackLiveWorld liveWorld;
		readonly IStealthUndefendedAttackThreatAdapter threatAdapter;
		readonly IStealthUndefendedAttackOrders orders;
		OwnerState state = new OwnerState();

		public StealthUndefendedAttackBehavior(StealthUndefendedAttackHandoff handoff,
			IStealthLifecycleOwnershipGuard ownershipGuard,
			IStealthUndefendedAttackLiveWorld liveWorld,
			IStealthUndefendedAttackThreatAdapter threatAdapter,
			IStealthUndefendedAttackOrders orders)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.UndefendedAttack)
				throw new ArgumentException(
					"UndefendedAttack requires UndefendedAttack ownership.", nameof(handoff));

			mission = handoff.Mission ?? throw new ArgumentException(
				"UndefendedAttack requires one immutable mission.", nameof(handoff));
			this.ownershipGuard = ownershipGuard ?? throw new ArgumentNullException(nameof(ownershipGuard));
			this.liveWorld = liveWorld ?? throw new ArgumentNullException(nameof(liveWorld));
			this.threatAdapter = threatAdapter ?? throw new ArgumentNullException(nameof(threatAdapter));
			this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
		}

		public StealthUndefendedAttackResult Execute()
		{
			var live = ReadLive("execute");
			var prospective = state.Clone();
			if (prospective.NextRefreshTick >= 0 && live.Tick < prospective.LastRefreshTick)
				throw new InvalidOperationException("Live UndefendedAttack ticks must not move backwards.");

			prospective.LiveDefenderActorIds = live.LiveDefenderActorIds.ToArray();
			if (prospective.LiveDefenderActorIds.Length != 0)
			{
				ClearOrderDeduplication(prospective);
				prospective.Disposition = StealthUndefendedAttackDisposition.CrushEvaluation;
				prospective.Safety = null;
				return CommitAndResult(prospective, Array.Empty<uint>());
			}

			var validTargets = ValidTargets(live);
			if (validTargets.Length == 0)
			{
				ClearSelectedTarget(prospective);
				prospective.Disposition = StealthUndefendedAttackDisposition.Reacquire;
				prospective.Safety = null;
				return CommitAndResult(prospective, Array.Empty<uint>());
			}

			var selected = prospective.SelectedTargetActorId.HasValue ? validTargets.FirstOrDefault(
				target => target.ActorId == prospective.SelectedTargetActorId.Value) : null;
			if (selected == null || prospective.NextRefreshTick < 0 ||
				live.Tick >= prospective.NextRefreshTick)
			{
				selected = SelectTarget(validTargets);
				prospective.SelectedTargetActorId = selected.ActorId;
				prospective.LastRefreshTick = live.Tick;
				prospective.NextRefreshTick = checked(live.Tick + RefreshIntervalTicks);
				if (prospective.LastIssuedTargetActorId != prospective.SelectedTargetActorId)
					ClearOrderDeduplication(prospective);
			}

			var attackMembers = live.Members.Select(member => member.ActorId).ToArray();
			var facts = ThreatFacts(live, selected, validTargets);
			var safety = CalculateSafety(facts);
			prospective.Safety = safety;
			if (safety.RequiresReacquisition)
			{
				ClearOrderDeduplication(prospective);
				prospective.Disposition = StealthUndefendedAttackDisposition.Reacquire;
				return CommitAndResult(prospective, Array.Empty<uint>());
			}

			if (safety.Approved &&
				(prospective.LastIssuedTargetActorId != prospective.SelectedTargetActorId ||
				!prospective.LastIssuedActorIds.SequenceEqual(attackMembers)))
			{
				IssueAttack(attackMembers, selected.ActorId);
				prospective.LastIssuedTargetActorId = selected.ActorId;
				prospective.LastIssuedActorIds = attackMembers.ToArray();
			}

			prospective.Disposition = StealthUndefendedAttackDisposition.Retain;
			return CommitAndResult(prospective, attackMembers);
		}

		public MiniYamlNode SerializePrivateState(string key = "UndefendedAttack")
		{
			return StealthUndefendedAttackPersistence.Serialize(key, handoff, mission,
				state.SelectedTargetActorId, state.LastRefreshTick, state.NextRefreshTick, state.Safety,
				state.Disposition, state.LastIssuedActorIds, state.LastIssuedTargetActorId,
				state.LiveDefenderActorIds);
		}

		public void RestorePrivateState(MiniYamlNode node)
		{
			EnsureActiveOwnership();
			var state = StealthUndefendedAttackPersistence.Restore(node, handoff, mission);
			var live = ReadLive("restore");
			ValidateRestoredLiveState(state, live);
			var restored = new OwnerState
			{
				SelectedTargetActorId = state.SelectedTargetActorId,
				LastRefreshTick = state.LastRefreshTick,
				NextRefreshTick = state.NextRefreshTick,
				Safety = state.Safety,
				Disposition = state.Disposition,
				LastIssuedActorIds = state.LastIssuedActorIds.ToArray(),
				LastIssuedTargetActorId = state.LastIssuedTargetActorId,
				LiveDefenderActorIds = state.LiveDefenderActorIds.ToArray()
			};
			EnsureActiveOwnership();
			this.state = restored;
		}

		void ValidateRestoredLiveState(StealthUndefendedAttackPrivateState state,
			StealthUndefendedAttackLiveSnapshot live)
		{
			if (!RestoredRefreshMatchesLive(state, live.Tick))
				throw new InvalidOperationException(
					"Saved UndefendedAttack refresh state is ahead of the live world.");

			var validTargets = ValidTargets(live);
			var selected = state.SelectedTargetActorId.HasValue ? validTargets.FirstOrDefault(
				target => target.ActorId == state.SelectedTargetActorId.Value) : null;
			if (state.SelectedTargetActorId.HasValue && selected == null)
				throw new InvalidOperationException(
					"Saved UndefendedAttack target is not a valid live mission-cell target.");
			if (state.Disposition == StealthUndefendedAttackDisposition.Reacquire &&
				!state.SelectedTargetActorId.HasValue && validTargets.Length != 0)
				throw new InvalidOperationException(
					"Saved empty-cell reacquisition does not match the live mission cell.");

			if (!state.LiveDefenderActorIds.SequenceEqual(live.LiveDefenderActorIds))
				throw new InvalidOperationException(
					"Saved UndefendedAttack transition does not match live defenders.");
			if (state.LastIssuedTargetActorId.HasValue && !state.LastIssuedActorIds.SequenceEqual(
				live.Members.Select(member => member.ActorId)))
				throw new InvalidOperationException(
					"Saved UndefendedAttack order actors do not match the live squad.");

			if (state.Safety.HasValue)
			{
				if (selected == null)
					throw new InvalidOperationException(
						"Saved UndefendedAttack safety has no valid selected target.");
				var current = CalculateSafety(ThreatFacts(live, selected, validTargets));
				if (!SameSafety(state.Safety.Value, current))
					throw new InvalidOperationException(
						"Saved UndefendedAttack safety does not match current standard live facts.");
			}
		}

		StealthUndefendedAttackLiveSnapshot ReadLive(string operation)
		{
			EnsureActiveOwnership();
			var live = liveWorld.Read(mission) ?? throw new InvalidOperationException(
				"The live UndefendedAttack view returned no snapshot during " + operation + ".");
			EnsureActiveOwnership();
			return live;
		}

		StealthUndefendedAttackSafetyResult CalculateSafety(
			StealthUndefendedAttackThreatFacts facts)
		{
			EnsureActiveOwnership();
			var safety = threatAdapter.Calculate(facts);
			EnsureActiveOwnership();
			return safety;
		}

		void IssueAttack(IReadOnlyList<uint> actorIds, uint targetActorId)
		{
			EnsureActiveOwnership();
			var callbackActorIds = Array.AsReadOnly(actorIds.ToArray());
			orders.IssueAttack(handoff.Owner, handoff.Epoch, callbackActorIds, targetActorId);
			EnsureActiveOwnership();
		}

		StealthUndefendedAttackResult CommitAndResult(OwnerState prospective,
			IEnumerable<uint> attackMemberActorIds)
		{
			EnsureActiveOwnership();
			state = prospective;
			return new StealthUndefendedAttackResult(handoff.Handoff, mission,
				prospective.Disposition, prospective.SelectedTargetActorId,
				attackMemberActorIds, prospective.LiveDefenderActorIds, prospective.Safety);
		}

		static StealthUndefendedAttackTargetSnapshot SelectTarget(
			IReadOnlyList<StealthUndefendedAttackTargetSnapshot> targets)
		{
			var highestPriority = StealthAISpecialistPolicy.HighestPriorityEligibleEngagements(
				targets.Select(target => (target, target.ConfiguredPriority)));
			return highestPriority.OrderByDescending(target =>
				StealthAISpecialistPolicy.StrategicTargetValueByRemainingHealth(
					target.ConfiguredPriority, target.ActorValue,
					target.HitPoints, target.MaximumHitPoints))
				.ThenBy(target => target.ActorId).First();
		}

		static bool InCurrentRange(StealthUndefendedAttackMemberSnapshot member,
			StealthUndefendedAttackTargetSnapshot target)
		{
			var dx = (long)member.CurrentCell.X - target.CurrentCell.X;
			var dy = (long)member.CurrentCell.Y - target.CurrentCell.Y;
			var range = (long)member.CurrentWeaponRangeCells;
			return dx * dx + dy * dy <= range * range;
		}

		StealthUndefendedAttackThreatFacts ThreatFacts(
			StealthUndefendedAttackLiveSnapshot live,
			StealthUndefendedAttackTargetSnapshot selected,
			IReadOnlyList<StealthUndefendedAttackTargetSnapshot> validTargets)
		{
			return new StealthUndefendedAttackThreatFacts(selected.ActorId,
				live.Members.Select(member => member.ActorId),
				validTargets.Select(target => target.ActorId),
				live.FormationCloaked, live.HasDetectorCoverage,
				live.PlannedActionRevealsFormation,
				live.Members.Any(member => InCurrentRange(member, selected)));
		}

		StealthUndefendedAttackTargetSnapshot[] ValidTargets(
			StealthUndefendedAttackLiveSnapshot live)
		{
			return live.Targets.Where(target => target.IsValid &&
				target.StrategicCell == mission.StrategicCell).ToArray();
		}

		static bool SameSafety(StealthUndefendedAttackSafetyResult saved,
			StealthUndefendedAttackSafetyResult current)
		{
			return saved.Score.ThreatRating.Equals(current.Score.ThreatRating) &&
				saved.Score.Crossover.Equals(current.Score.Crossover) &&
				saved.Approved == current.Approved &&
				saved.RequiresReacquisition == current.RequiresReacquisition;
		}

		static bool RestoredRefreshMatchesLive(
			StealthUndefendedAttackPrivateState restored, int liveTick)
		{
			if (!restored.SelectedTargetActorId.HasValue)
				return restored.LastRefreshTick == -1 && restored.NextRefreshTick == -1;

			return restored.LastRefreshTick >= 0 && restored.LastRefreshTick <= liveTick &&
				restored.LastRefreshTick <= int.MaxValue - RefreshIntervalTicks &&
				restored.NextRefreshTick == restored.LastRefreshTick + RefreshIntervalTicks;
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException(
					"Stale UndefendedAttack ownership cannot execute or restore state.");
		}

		static void ClearSelectedTarget(OwnerState prospective)
		{
			prospective.SelectedTargetActorId = null;
			prospective.LastRefreshTick = -1;
			prospective.NextRefreshTick = -1;
			ClearOrderDeduplication(prospective);
		}

		static void ClearOrderDeduplication(OwnerState prospective)
		{
			prospective.LastIssuedActorIds = Array.Empty<uint>();
			prospective.LastIssuedTargetActorId = null;
		}
	}
}
