#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Reactive live UndefendedAttack owner: finish the best target unless defenders appear.</summary>
	public sealed class StealthUndefendedAttackBehavior
	{
		readonly StealthUndefendedAttackHandoff handoff;
		readonly StealthApproachMission mission;
		readonly IStealthLifecycleOwnershipGuard ownershipGuard;
		readonly IStealthUndefendedAttackLiveWorld liveWorld;
		readonly IStealthUndefendedAttackThreatAdapter threatAdapter;
		readonly IStealthUndefendedAttackOrders orders;
		uint? targetId;
		uint[] lastMembers = Array.Empty<uint>();
		long orderRevision;

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
			EnsureActiveOwnership();
			var live = liveWorld.Read(mission) ??
				throw new InvalidOperationException("The live UndefendedAttack view returned no snapshot.");
			EnsureActiveOwnership();
			var targets = live.Targets.Where(candidate => candidate.IsValid &&
				candidate.StrategicCell == mission.StrategicCell).ToArray();
			if (targets.Length == 0)
				return Result(live, live.LiveDefenderActorIds.Count == 0 ?
					StealthUndefendedAttackDisposition.Reacquire :
					StealthUndefendedAttackDisposition.CrushEvaluation, null, null);

			var target = targetId.HasValue ?
				targets.FirstOrDefault(candidate => candidate.ActorId == targetId.Value) : null;
			target = target ?? SelectTarget(targets);
			var safety = threatAdapter.Calculate(ThreatFacts(live, target, targets));
			EnsureActiveOwnership();
			if (safety.RequiresReacquisition)
				return Result(live, StealthUndefendedAttackDisposition.Reacquire, target, safety);
			if (!safety.Approved)
				return Result(live, StealthUndefendedAttackDisposition.CrushEvaluation, target, safety);

			var members = live.Members.Select(member => member.ActorId).ToArray();
			if (targetId != target.ActorId || !lastMembers.SequenceEqual(members) ||
				live.Members.All(member => member.NeedsAttackOrder))
			{
				orders.IssueAttack(handoff.Owner, handoff.Epoch, members, target.ActorId,
					++orderRevision);
				EnsureActiveOwnership();
			}

			return Result(live, StealthUndefendedAttackDisposition.Retain, target, safety);
		}

		StealthUndefendedAttackResult Result(StealthUndefendedAttackLiveSnapshot live,
			StealthUndefendedAttackDisposition disposition,
			StealthUndefendedAttackTargetSnapshot target,
			StealthUndefendedAttackSafetyResult? safety)
		{
			var members = live.Members.Select(member => member.ActorId).ToArray();
			var retaining = disposition == StealthUndefendedAttackDisposition.Retain;
			targetId = retaining ? target?.ActorId : null;
			lastMembers = retaining ? members : Array.Empty<uint>();
			return new StealthUndefendedAttackResult(handoff.Handoff, mission, disposition,
				target?.ActorId, retaining ? members : Array.Empty<uint>(),
				live.LiveDefenderActorIds, safety);
		}

		static StealthUndefendedAttackTargetSnapshot SelectTarget(
			IReadOnlyList<StealthUndefendedAttackTargetSnapshot> targets)
		{
			var priority = StealthAISpecialistPolicy.HighestPriorityEligibleEngagements(
				targets.Select(target => (target, target.ConfiguredPriority)));
			return priority.OrderByDescending(target =>
				StealthAISpecialistPolicy.StrategicTargetValueByRemainingHealth(
					target.ConfiguredPriority, target.ActorValue,
					target.HitPoints, target.MaximumHitPoints))
				.ThenBy(target => target.ActorId).First();
		}

		static StealthUndefendedAttackThreatFacts ThreatFacts(
			StealthUndefendedAttackLiveSnapshot live,
			StealthUndefendedAttackTargetSnapshot target,
			IReadOnlyList<StealthUndefendedAttackTargetSnapshot> targets)
		{
			return new StealthUndefendedAttackThreatFacts(target.ActorId,
				live.Members.Select(member => member.ActorId), targets.Select(actor => actor.ActorId)
					.Concat(live.LiveDefenderActorIds).Distinct(),
				live.FormationCloaked, live.HasDetectorCoverage,
				live.PlannedActionRevealsFormation,
				InCurrentRange(live.Members, target));
		}

		static bool InCurrentRange(IReadOnlyList<StealthUndefendedAttackMemberSnapshot> members,
			StealthUndefendedAttackTargetSnapshot target)
		{
			if (members.Count == 0)
				return false;
			var x = (int)Math.Round(members.Average(member => member.CurrentCell.X));
			var y = (int)Math.Round(members.Average(member => member.CurrentCell.Y));
			var dx = (long)x - target.CurrentCell.X;
			var dy = (long)y - target.CurrentCell.Y;
			var range = (long)members.Min(member => member.CurrentWeaponRangeCells);
			return dx * dx + dy * dy <= range * range;
		}

		void EnsureActiveOwnership()
		{
			if (!ownershipGuard.IsActive(handoff.Owner, handoff.Epoch))
				throw new InvalidOperationException("Stale UndefendedAttack ownership cannot execute.");
		}
	}
}
