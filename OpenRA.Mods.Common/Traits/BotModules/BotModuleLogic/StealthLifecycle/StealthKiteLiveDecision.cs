#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>One current-World view for one reactive Kite decision.</summary>
	sealed class StealthKiteLiveDecision
	{
		readonly bool formationCloaked;
		public bool FormationExposed { get; }
		public bool KitingEnabled { get; }
		public StealthKiteMemberSnapshot[] Members { get; }
		public StealthKiteActorSnapshot[] Defenders { get; }
		public StealthKiteActorSnapshot[] Objectives { get; }
		public CPos[] CandidateCells { get; }
		public uint[] DefenderActorIds { get; }
		public uint[] ObjectiveActorIds { get; }
		public CPos[] MemberCells => Members.Select(member => member.CurrentCell).Distinct().ToArray();
		public StealthKiteDisposition? TargetlessDisposition { get; }
		StealthKiteLiveDecision(StealthKiteLiveSnapshot live)
		{
			formationCloaked = live.FormationCloaked;
			FormationExposed = !live.FormationCloaked || live.FormationDetected;
			KitingEnabled = live.KitingEnabled;
			Members = live.Members.Where(member => member.IsValid)
				.OrderBy(member => member.ActorId).ToArray();
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
			return retained ?? Defenders.OrderBy(actor => Members.Min(member =>
				DistanceSquared(member.CurrentCell, actor.CurrentCell)))
				.ThenBy(actor => actor.ActorId).First();
		}

		public CPos? CurrentFormationCell()
		{
			return Members.Length == 0 ? (CPos?)null : Members[0].CurrentCell;
		}

		public CPos[] OrderedCandidateCells(StealthKiteActorSnapshot target, CPos? currentCell)
		{
			var occupied = MemberCells.ToHashSet();
			return CandidateCells.Where(cell => !occupied.Contains(cell))
				.OrderBy(cell => DistanceSquared(Members[0].CurrentCell, cell))
				.ThenByDescending(cell => DistanceSquared(cell, target.CurrentCell))
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
		}

		public StealthKiteThreatFacts ThreatFacts(StealthKiteActorSnapshot target, CPos cell)
		{
			if (target == null || !Defenders.Contains(target) || Members.Length == 0)
				throw new ArgumentException("Kite safety requires a live target and squad.", nameof(target));
			return new StealthKiteThreatFacts(StealthKiteAction.Fire, target.ActorId,
				target.CurrentCell, cell, Members.Min(member => member.CurrentWeaponRangeCells),
				Members.Select(member => member.ActorId), Defenders, formationCloaked, true, true,
				0);
		}

		public StealthKiteFallbackFacts FallbackFacts(StealthKiteActorSnapshot target)
		{
			return new StealthKiteFallbackFacts(target.ActorId, target.CurrentCell,
				Members.Select(member => member.ActorId), DefenderActorIds, formationCloaked);
		}

		public string LiveIdentity(StealthKiteActorSnapshot target)
		{
			return string.Join("|", target.ActorId, string.Join(",",
				Members.Select(member => member.ActorId)), string.Join(",", DefenderActorIds));
		}

		static long DistanceSquared(CPos left, CPos right)
		{
			var dx = (long)left.X - right.X;
			var dy = (long)left.Y - right.Y;
			return dx * dx + dy * dy;
		}
	}
}
