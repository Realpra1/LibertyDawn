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
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	sealed class StealthKiteLiveDecision
	{
		readonly bool formationCloaked;
		readonly StealthKiteLiveSnapshot live;
		public int Tick { get; }
		public StealthKiteMemberSnapshot[] Members { get; }
		public StealthKiteActorSnapshot[] Defenders { get; }
		public StealthKiteActorSnapshot[] Objectives { get; }
		public CPos[] CandidateCells { get; }
		public uint[] DefenderActorIds { get; }
		public uint[] ObjectiveActorIds { get; }
		public StealthKiteDisposition? TargetlessDisposition { get; }
		public bool HasActivityObservation => live.HasActivityObservation;
		public long ActivityRevision => live.ActivityRevision;
		public StealthKiteOrderToken ActiveOrderToken => live.ActiveOrderToken;
		public StealthKiteOrderToken CompletedOrderToken => live.CompletedOrderToken;

		StealthKiteLiveDecision(StealthKiteLiveSnapshot live)
		{
			this.live = live;
			Tick = live.Tick;
			formationCloaked = live.FormationCloaked;
			Members = live.Members.Where(member => member.IsValid)
				.OrderBy(member => member.ActorId).ToArray();

			// The live-world boundary derives this local membership from current positions.
			// It is deliberately not the strategic mission-cell or an actor-cache lookup.
			var actors = live.Actors.Where(actor => actor.IsValid && actor.IsInLocalEngagementArea)
				.OrderBy(actor => actor.ActorId).ToArray();
			Defenders = actors.Where(actor => actor.IsDefender).ToArray();
			Objectives = actors.Where(actor => actor.IsMissionObjective).ToArray();
			CandidateCells = live.CandidateCells.ToArray();
			DefenderActorIds = Defenders.Select(actor => actor.ActorId).ToArray();
			ObjectiveActorIds = Objectives.Select(actor => actor.ActorId).ToArray();
			if (Defenders.Length == 0)
				TargetlessDisposition = Objectives.Length == 0 ?
					StealthKiteDisposition.Reacquire : StealthKiteDisposition.UndefendedAttack;
			else if (Members.Length == 0)
				TargetlessDisposition = StealthKiteDisposition.RecalculateFlee;
		}

		public StealthKiteLiveFingerprint Fingerprint(StealthKiteActorSnapshot target)
		{
			return StealthKiteLiveFingerprint.Create(live, this, target);
		}

		public static StealthKiteLiveDecision Create(StealthKiteLiveSnapshot live)
		{
			return new StealthKiteLiveDecision(live ?? throw new ArgumentNullException(nameof(live)));
		}

		public StealthKiteActorSnapshot ResolveTarget(uint? retainedActorId)
		{
			if (TargetlessDisposition.HasValue)
				throw new InvalidOperationException("A targetless Kite decision cannot resolve a target.");
			var retained = retainedActorId.HasValue ?
				Defenders.FirstOrDefault(actor => actor.ActorId == retainedActorId.Value) : null;
			if (retained != null)
				return retained;

			return Defenders.OrderBy(actor => Members.Min(member =>
				DistanceSquared(member.CurrentCell, actor.CurrentCell)))
				.ThenBy(actor => actor.ActorId).First();
		}

		public bool CanReturnToCrush(StealthKiteActorSnapshot target)
		{
			return target != null && target.IsInfantry && target.CanBeCrushedByFormation &&
				formationCloaked && !target.HasDetectorCoverage;
		}

		public StealthKiteThreatFacts ThreatFacts(StealthKiteAction action,
			StealthKiteActorSnapshot target, CPos cell)
		{
			if (target == null || !Defenders.Contains(target))
				throw new ArgumentException("Kite safety requires a current live defender.", nameof(target));
			var plannedAttack = action == StealthKiteAction.Fire;
			return new StealthKiteThreatFacts(action, target.ActorId, target.CurrentCell, cell,
				Members.Min(member => member.CurrentWeaponRangeCells),
				Members.Select(member => member.ActorId), Defenders, formationCloaked,
				plannedAttack, plannedAttack);
		}

		public StealthKiteFallbackFacts FallbackFacts(StealthKiteActorSnapshot target)
		{
			if (target == null || !Defenders.Contains(target) || Members.Length == 0)
				throw new ArgumentException("Kite fallback requires a target and live members.", nameof(target));
			return new StealthKiteFallbackFacts(target.ActorId, target.CurrentCell,
				Members.Select(member => member.ActorId), DefenderActorIds, formationCloaked);
		}

		public CPos[] OrderedFireCells(StealthKiteActorSnapshot target)
		{
			var range = Members.Min(member => member.CurrentWeaponRangeCells);
			return CandidateCells.Where(cell => DistanceSquared(cell, target.CurrentCell) <=
				(long)range * range).OrderBy(cell => Members.Min(member =>
					DistanceSquared(member.CurrentCell, cell)))
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
		}

		public CPos[] OrderedWithdrawCells(StealthKiteActorSnapshot target, CPos fireCell)
		{
			return CandidateCells.Where(cell => cell != fireCell)
				.OrderByDescending(cell => DistanceSquared(cell, target.CurrentCell))
				.ThenBy(cell => Members.Min(member => DistanceSquared(member.CurrentCell, cell)))
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
		}

		public bool MembersAt(CPos cell)
		{
			return Members.All(member => member.CurrentCell == cell);
		}

		static long DistanceSquared(CPos left, CPos right)
		{
			var dx = (long)left.X - right.X;
			var dy = (long)left.Y - right.Y;
			return dx * dx + dy * dy;
		}
	}
}
