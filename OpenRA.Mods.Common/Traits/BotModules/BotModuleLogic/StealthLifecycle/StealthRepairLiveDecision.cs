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
using System.Text;

namespace OpenRA.Mods.Common.Traits
{
	sealed class StealthRepairLiveDecision
	{
		readonly StealthRepairLiveSnapshot live;
		public int Tick => live.Tick;
		public long DamageEventId => live.DamageEventId;
		public int DamageTick => live.DamageTick;
		public uint DamageSourceActorId => live.DamageSourceActorId;
		public int DamageAmount => live.DamageAmount;
		public string ResumeFingerprint => live.ResumeFingerprint;
		public bool FormationCloaked => live.FormationCloaked;
		public bool HasActivityObservation => live.HasActivityObservation;
		public long ActivityRevision => live.ActivityRevision;
		public int RouteProgress => live.RouteProgress;
		public StealthRepairOrderToken ActiveOrderToken => live.ActiveOrderToken;
		public StealthRepairOrderToken CompletedOrderToken => live.CompletedOrderToken;
		public StealthRepairMemberSnapshot[] Members { get; }
		public StealthRepairOptionSnapshot[] Options { get; }
		public StealthRepairEnemySnapshot[] Enemies { get; }
		public StealthRepairStaticActorSnapshot[] StaticActors { get; }
		public StealthRepairRouteSnapshot[] PassableRoutes { get; }
		public uint[] MemberActorIds { get; }
		public uint[] EnemyActorIds { get; }
		public string Fingerprint { get; }

		StealthRepairLiveDecision(StealthRepairLiveSnapshot live)
		{
			this.live = live;
			Members = live.Members.Where(member => member.IsValid)
				.OrderBy(member => member.ActorId).ToArray();
			Options = live.RepairOptions.Where(option => option.IsValid)
				.OrderBy(option => option.ActorId).ToArray();
			Enemies = live.Enemies.Where(enemy => enemy.IsValid && enemy.IsInLocalArea)
				.OrderBy(enemy => enemy.ActorId).ToArray();
			StaticActors = live.StaticActors.Where(actor => actor.IsInWorld && !actor.IsDead)
				.OrderBy(actor => actor.ActorId).ToArray();
			PassableRoutes = live.Routes.Where(route => route.IsPassable &&
				Options.Any(option => option.ActorId == route.RepairOptionActorId))
				.OrderBy(route => route.RepairOptionActorId)
				.ThenBy(route => route.StableIdentity).ToArray();
			MemberActorIds = Members.Select(member => member.ActorId).ToArray();
			EnemyActorIds = Enemies.Select(enemy => enemy.ActorId).ToArray();
			Fingerprint = StealthRepairFingerprint.Create(live);
		}

		public static StealthRepairLiveDecision Create(StealthRepairLiveSnapshot live)
		{
			return new StealthRepairLiveDecision(live ?? throw new ArgumentNullException(nameof(live)));
		}

		public StealthRepairRouteEvaluation Evaluate(StealthRepairRouteSnapshot route,
			IEnumerable<StealthRepairMemberSnapshot> repairMembers,
			Func<StealthRepairThreatFacts, StealthTargetThreatScore> calculate)
		{
			if (route == null || !PassableRoutes.Contains(route) || calculate == null)
				throw new ArgumentException("Repair evaluation requires a current passable route.");
			var option = Options.Single(candidate => candidate.ActorId == route.RepairOptionActorId);
			var facts = new StealthRepairThreatFacts(option.ActorId, repairMembers, Enemies,
				route.Cells, FormationCloaked, route.HasDetectorCoverage);
			return new StealthRepairRouteEvaluation(option, route, facts, calculate(facts));
		}

		public static StealthRepairRouteEvaluation SelectSafest(
			IEnumerable<StealthRepairRouteEvaluation> evaluations)
		{
			return OrderedSafe(evaluations).FirstOrDefault();
		}

		public static IEnumerable<StealthRepairRouteEvaluation> OrderedSafe(
			IEnumerable<StealthRepairRouteEvaluation> evaluations)
		{
			return evaluations.Where(route => route.IsSafe)
				.OrderBy(route => route.StandardDanger.ThreatRating)
				.ThenBy(route => route.StandardDanger.Crossover)
				.ThenBy(route => route.Option.ActorId)
				.ThenBy(route => route.Route.StableIdentity);
		}

		public bool AtOption(StealthRepairOptionSnapshot option,
			IEnumerable<StealthRepairMemberSnapshot> repairMembers)
		{
			var exact = ExactOrderedRepairMembers(repairMembers);
			return option != null && exact.Length != 0 &&
				exact.All(member => member.CurrentCell == option.CurrentCell);
		}

		public StealthRepairCompletionEvidence Completion(
			IEnumerable<StealthRepairMemberSnapshot> repairMembers)
		{
			var exact = ExactOrderedRepairMembers(repairMembers);
			if (exact.Length == 0 || exact.Any(member => !member.IsRepaired))
				return null;
			return new StealthRepairCompletionEvidence(Tick, exact.Select(member =>
				new StealthRepairDamagedMember(member.ActorId, member.MaximumHitPoints,
					member.MaximumHitPoints)));
		}

		StealthRepairMemberSnapshot[] ExactOrderedRepairMembers(
			IEnumerable<StealthRepairMemberSnapshot> repairMembers)
		{
			var exact = repairMembers?.ToArray();
			if (exact == null || exact.Any(member => member == null || !Members.Contains(member)) ||
				!exact.Select(member => member.ActorId).SequenceEqual(
					exact.Select(member => member.ActorId).OrderBy(id => id)) ||
				exact.Select(member => member.ActorId).Distinct().Count() != exact.Length)
				throw new ArgumentException("Repair arrival requires the exact ordered damaged-member subset.",
					nameof(repairMembers));
			return exact;
		}
	}

	sealed class StealthRepairOwnerState
	{
		public bool EntryValidated;
		public int LastObservedTick = -1;
		public StealthRepairDisposition Disposition = StealthRepairDisposition.Retain;
		public StealthRepairLiveCause LiveCause = StealthRepairLiveCause.NoSafeRepair;
		public string Fingerprint;
		public uint[] MemberIds = Array.Empty<uint>();
		public uint[] EnemyIds = Array.Empty<uint>();
		public StealthRepairRouteEvaluation[] Evaluations = Array.Empty<StealthRepairRouteEvaluation>();
		public uint? OptionId;
		public uint? RouteId;
		public int RouteProgress;
		public StealthTargetThreatScore? Danger;
		public long RouteRevision;
		public StealthRepairOrderToken LastOrderToken;
		public StealthRepairCompletionEvidence Completion;
		public long? LongRouteCacheRevision;
		public CPos[] OrderedRoute = Array.Empty<CPos>();

		public StealthRepairOwnerState Clone()
		{
			var clone = (StealthRepairOwnerState)MemberwiseClone();
			clone.MemberIds = MemberIds.ToArray();
			clone.EnemyIds = EnemyIds.ToArray();
			clone.Evaluations = Evaluations.ToArray();
			clone.OrderedRoute = OrderedRoute.ToArray();
			return clone;
		}
	}

	static class StealthRepairFingerprint
	{
		public static string Create(StealthRepairLiveSnapshot live)
		{
			var text = new StringBuilder("D=").Append(live.DamageEventId).Append(',')
				.Append(live.DamageTick).Append(',').Append(live.DamageSourceActorId).Append(',')
				.Append(live.DamageAmount).Append(";X=").Append(live.ResumeFingerprint)
				.Append(";C=").Append(live.FormationCloaked ? 1 : 0).Append(";M=");
			foreach (var member in live.Members)
				text.Append(member.ActorId).Append(',').Append(member.CurrentCell.Bits).Append(',')
					.Append(member.CurrentWeaponRangeCells).Append(',').Append(member.HitPoints).Append(',')
					.Append(member.MaximumHitPoints).Append(',').Append(member.IsInWorld ? 1 : 0).Append(',')
					.Append(member.IsDead ? 1 : 0).Append('|');
			text.Append(";O=");
			foreach (var option in live.RepairOptions)
				text.Append(option.ActorId).Append(',').Append(option.CurrentCell.Bits).Append(',')
					.Append(option.IsAvailable ? 1 : 0).Append(',').Append(option.IsInWorld ? 1 : 0)
					.Append(',').Append(option.IsDead ? 1 : 0).Append('|');
			text.Append(";E=");
			foreach (var enemy in live.Enemies)
				text.Append(enemy.ActorId).Append(',').Append(enemy.ActorType).Append(',')
					.Append(enemy.CurrentCell.Bits).Append(',').Append(enemy.HitPoints).Append(',')
					.Append(enemy.MaximumHitPoints).Append(',').Append(enemy.CurrentWeaponRangeCells)
					.Append(',').Append(enemy.IsDetector ? 1 : 0).Append(',')
					.Append(enemy.IsInLocalArea ? 1 : 0).Append(',').Append(enemy.IsInWorld ? 1 : 0)
					.Append(',').Append(enemy.IsDead ? 1 : 0).Append(',')
					.Append(enemy.IsTargetable ? 1 : 0).Append('|');
			text.Append(";S=");
			foreach (var actor in live.StaticActors)
				text.Append(actor.ActorId).Append(',').Append(actor.ActorType).Append(',')
					.Append(actor.CurrentCell.Bits).Append(',').Append(actor.IsPassable ? 1 : 0).Append(',')
					.Append(actor.IsInWorld ? 1 : 0).Append(',').Append(actor.IsDead ? 1 : 0).Append('|');
			text.Append(";R=");
			foreach (var route in live.Routes)
			{
				text.Append(route.StableIdentity).Append(',').Append(route.RepairOptionActorId).Append(',')
					.Append(route.IsPassable ? 1 : 0).Append(',')
					.Append(route.RequiresStrategicRouting ? 1 : 0).Append(',')
					.Append(route.HasDetectorCoverage ? 1 : 0).Append(':');
				foreach (var cell in route.Cells)
					text.Append(cell.Bits).Append(',');
				text.Append('|');
			}

			return text.ToString();
		}
	}
}
