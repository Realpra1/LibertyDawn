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
	sealed class StealthRecalculateFleeLiveDecision
	{
		readonly StealthRecalculateFleeLiveSnapshot live;
		public bool FormationCloaked => live.FormationCloaked;
		public StealthRecalculateFleeMemberSnapshot[] Members { get; }
		public StealthRecalculateFleeEnemySnapshot[] Enemies { get; }
		public StealthRecalculateFleeCandidateSnapshot[] PassableCandidates { get; }
		public uint[] MemberActorIds { get; }
		public uint[] EnemyActorIds { get; }
		public string Fingerprint { get; }

		StealthRecalculateFleeLiveDecision(StealthRecalculateFleeLiveSnapshot live)
		{
			this.live = live;
			Members = live.Members.Where(member => member.IsValid)
				.OrderBy(member => member.ActorId).ToArray();
			Enemies = live.Enemies.Where(enemy => enemy.IsValid && enemy.IsInLocalEngagementArea)
				.OrderBy(enemy => enemy.ActorId).ToArray();
			PassableCandidates = live.Candidates.Where(candidate => candidate.IsPassable)
				.OrderBy(candidate => candidate.Cell.Y).ThenBy(candidate => candidate.Cell.X).ToArray();
			MemberActorIds = Members.Select(member => member.ActorId).ToArray();
			EnemyActorIds = Enemies.Select(enemy => enemy.ActorId).ToArray();
			Fingerprint = StealthRecalculateFleeFingerprint.Create(live);
		}

		public static StealthRecalculateFleeLiveDecision Create(
			StealthRecalculateFleeLiveSnapshot live)
		{
			return new StealthRecalculateFleeLiveDecision(live ??
				throw new ArgumentNullException(nameof(live)));
		}

		public StealthRecalculateFleeRouteEvaluation Evaluate(
			StealthRecalculateFleeCandidateSnapshot candidate,
			Func<StealthRecalculateFleeThreatFacts, StealthTargetThreatScore> calculate)
		{
			if (candidate == null || !PassableCandidates.Contains(candidate))
				throw new ArgumentException("Route evaluation requires a passable live candidate.", nameof(candidate));
			var facts = new StealthRecalculateFleeThreatFacts(candidate.Cell,
				Members, Enemies, FormationCloaked, candidate.HasDetectorCoverage);
			return new StealthRecalculateFleeRouteEvaluation(candidate, facts, calculate(facts));
		}

		public static StealthRecalculateFleeRouteEvaluation SelectLeastDanger(
			IEnumerable<StealthRecalculateFleeRouteEvaluation> evaluations)
		{
			return OrderedBySafety(evaluations).FirstOrDefault();
		}

		public static IEnumerable<StealthRecalculateFleeRouteEvaluation> OrderedBySafety(
			IEnumerable<StealthRecalculateFleeRouteEvaluation> evaluations)
		{
			return evaluations.OrderBy(route => route.StandardDanger.ThreatRating)
				.ThenBy(route => route.StandardDanger.Crossover)
				.ThenBy(route => route.Candidate.Cell.Y).ThenBy(route => route.Candidate.Cell.X);
		}

		public bool Arrived(CPos destination)
		{
			if (Members.Length == 0)
				return false;
			var center = new CPos(
				(int)Math.Round(Members.Average(member => member.CurrentCell.X)),
				(int)Math.Round(Members.Average(member => member.CurrentCell.Y)));
			return Math.Abs(center.X - destination.X) <= 1 &&
				Math.Abs(center.Y - destination.Y) <= 1;
		}
	}
}
