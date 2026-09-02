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
	sealed class StealthMassAttackLiveDecision
	{
		readonly bool formationCloaked;
		public bool FormationCloaked => formationCloaked;
		public StealthMassAttackMemberSnapshot[] Members { get; }
		public StealthMassAttackActorSnapshot[] Defenders { get; }
		public StealthMassAttackActorSnapshot[] Objectives { get; }
		public CPos[] CandidateCells { get; }
		public uint[] MemberActorIds { get; }
		public CPos[] MemberCells => Members.Select(member => member.CurrentCell).Distinct().ToArray();
		public uint[] DefenderActorIds { get; }
		public uint[] ObjectiveActorIds { get; }
		public StealthMassAttackDisposition? TargetlessDisposition { get; }

		StealthMassAttackLiveDecision(StealthMassAttackLiveSnapshot live)
		{
			formationCloaked = live.FormationCloaked;
			Members = live.Members.Where(member => member.IsValid)
				.OrderBy(member => member.ActorId).ToArray();
			var local = live.Actors.Where(actor => actor.IsValid && actor.IsInLocalEngagementArea)
				.OrderBy(actor => actor.ActorId).ToArray();
			Defenders = local.Where(actor => actor.IsDefender).ToArray();
			Objectives = local.Where(actor => actor.IsMissionObjective).ToArray();
			CandidateCells = live.CandidateCells.ToArray();
			MemberActorIds = Members.Select(member => member.ActorId).ToArray();
			DefenderActorIds = Defenders.Select(actor => actor.ActorId).ToArray();
			ObjectiveActorIds = Objectives.Select(actor => actor.ActorId).ToArray();
			if (Members.Length == 0)
				TargetlessDisposition = StealthMassAttackDisposition.RecalculateFlee;
			else if (Defenders.Length == 0)
				TargetlessDisposition = Objectives.Length == 0 ?
					StealthMassAttackDisposition.Reacquire :
					StealthMassAttackDisposition.UndefendedAttack;
		}

		public static StealthMassAttackLiveDecision Create(StealthMassAttackLiveSnapshot live)
		{
			return new StealthMassAttackLiveDecision(live ?? throw new ArgumentNullException(nameof(live)));
		}

		public StealthMassAttackActorSnapshot FindTarget(uint targetId)
		{
			return Defenders.SingleOrDefault(actor => actor.ActorId == targetId);
		}

		public StealthMassAttackThreatFacts Facts(StealthMassAttackActorSnapshot target, CPos plannedCell)
		{
			if (target == null || !Defenders.Contains(target) || Members.Length == 0)
				throw new ArgumentException("MassAttack evaluation requires a current live target.", nameof(target));
			return new StealthMassAttackThreatFacts(target.ActorId, target.CurrentCell, plannedCell,
				MemberActorIds, Defenders, FormationCloaked, 0);
		}

		public CPos CurrentFormationCell()
		{
			if (Members.Length == 0)
				throw new InvalidOperationException("MassAttack has no current formation cell.");
			return Members[0].CurrentCell;
		}

		public CPos[] OrderedCandidateCells(StealthMassAttackActorSnapshot target, CPos currentCell)
		{
			var occupied = MemberCells.ToHashSet();
			return CandidateCells.Where(cell => !occupied.Contains(cell))
				.OrderBy(cell => DistanceSquared(cell, currentCell))
				.ThenByDescending(cell => DistanceSquared(cell, target.CurrentCell))
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
		}

		static long DistanceSquared(CPos left, CPos right)
		{
			var dx = (long)left.X - right.X;
			var dy = (long)left.Y - right.Y;
			return dx * dx + dy * dy;
		}
	}
}
