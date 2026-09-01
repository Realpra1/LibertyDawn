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
	sealed class StealthCrushLiveDecision
	{
		public readonly struct TargetResolution
		{
			public StealthCrushActorSnapshot Selected { get; }
			public bool RequiresRefresh { get; }

			public TargetResolution(StealthCrushActorSnapshot selected, bool requiresRefresh)
			{
				Selected = selected;
				RequiresRefresh = requiresRefresh;
			}
		}

		readonly int liveTick;
		readonly bool formationCloaked;

		public StealthCrushMemberSnapshot[] Members { get; }
		public StealthCrushActorSnapshot[] Defenders { get; }
		public StealthCrushActorSnapshot[] Candidates { get; }
		public uint[] DefenderActorIds { get; }
		public uint[] ObjectiveActorIds { get; }
		public StealthCrushDisposition? TargetlessDisposition { get; }

		StealthCrushLiveDecision(StealthCrushLiveSnapshot live,
			StealthApproachMission mission)
		{
			liveTick = live.Tick;
			formationCloaked = live.FormationCloaked;
			Members = live.Members.Where(member => member.IsValid)
				.OrderBy(member => member.ActorId).ToArray();
			var actors = live.Actors.Where(actor => actor.IsValid &&
				actor.StrategicCell == mission.StrategicCell).OrderBy(actor => actor.ActorId).ToArray();
			Defenders = actors.Where(actor => actor.IsDefender).ToArray();
			Candidates = Defenders.Where(actor => actor.IsInfantry &&
				actor.CanBeCrushedByFormation).ToArray();
			DefenderActorIds = Defenders.Select(actor => actor.ActorId).ToArray();
			ObjectiveActorIds = actors.Where(actor => actor.IsMissionObjective)
				.Select(actor => actor.ActorId).ToArray();

			if (Defenders.Length == 0)
				TargetlessDisposition = ObjectiveActorIds.Length == 0 ?
					StealthCrushDisposition.Reacquire : StealthCrushDisposition.UndefendedAttack;
			else if (Members.Length == 0 || Candidates.Length == 0)
				TargetlessDisposition = StealthCrushDisposition.Kite;
		}

		public static StealthCrushLiveDecision Create(StealthCrushLiveSnapshot live,
			StealthApproachMission mission)
		{
			if (live == null || mission == null)
				throw new ArgumentNullException(live == null ? nameof(live) : nameof(mission));
			return new StealthCrushLiveDecision(live, mission);
		}

		public TargetResolution ResolveTarget(uint? selectedActorId,
			CPos? selectedCurrentCell, int nextRefreshTick)
		{
			if (TargetlessDisposition.HasValue)
				throw new InvalidOperationException("A targetless Crush decision cannot select infantry.");
			var selected = selectedActorId.HasValue ? Candidates.FirstOrDefault(
				candidate => candidate.ActorId == selectedActorId.Value) : null;
			if (selected == null)
				return new TargetResolution(SelectTarget(), true);
			if (selectedCurrentCell != selected.CurrentCell)
				return new TargetResolution(selected, true);
			if (nextRefreshTick < 0 || liveTick >= nextRefreshTick)
				return new TargetResolution(SelectTarget(), true);
			return new TargetResolution(selected, false);
		}

		public StealthCrushThreatFacts ThreatFacts(StealthCrushActorSnapshot selected)
		{
			if (selected == null || !Candidates.Contains(selected))
				throw new ArgumentException("Crush threat facts require selected live infantry.", nameof(selected));
			return new StealthCrushThreatFacts(selected.ActorId, selected.CurrentCell,
				Members.Select(member => member.ActorId), DefenderActorIds,
				formationCloaked, selected.HasDetectorCoverage);
		}

		StealthCrushActorSnapshot SelectTarget()
		{
			var priority = StealthAISpecialistPolicy.HighestPriorityEligibleEngagements(
				Candidates.Select(candidate => (candidate, candidate.ConfiguredPriority)));
			return priority.OrderBy(candidate => Members.Min(member =>
				DistanceSquared(member.CurrentCell, candidate.CurrentCell)))
				.ThenBy(candidate => candidate.ActorId).First();
		}

		static long DistanceSquared(CPos left, CPos right)
		{
			var dx = (long)left.X - right.X;
			var dy = (long)left.Y - right.Y;
			return dx * dx + dy * dy;
		}
	}
}
