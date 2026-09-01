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
		public int Tick => live.Tick;
		public bool FormationCloaked => live.FormationCloaked;
		public string SourceFingerprint => live.SourceFingerprint;
		public bool HasActivityObservation => live.HasActivityObservation;
		public long ActivityRevision => live.ActivityRevision;
		public StealthRecalculateFleeOrderToken ActiveOrderToken => live.ActiveOrderToken;
		public StealthRecalculateFleeOrderToken CompletedOrderToken => live.CompletedOrderToken;
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

		public StealthRecalculateFleeEntryThreatFacts EntryFacts(
			StealthRecalculateFleeEntryEvidence evidence)
		{
			if (evidence == null)
				throw new ArgumentNullException(nameof(evidence));
			return new StealthRecalculateFleeEntryThreatFacts(evidence.Source,
				evidence.SelectedTargetActorId, evidence.SelectedTargetCurrentCell,
				MemberActorIds, Enemies, FormationCloaked);
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
			return evaluations.OrderBy(route => route.StandardDanger.ThreatRating)
				.ThenBy(route => route.StandardDanger.Crossover)
				.ThenBy(route => route.Candidate.Cell.Y)
				.ThenBy(route => route.Candidate.Cell.X).FirstOrDefault();
		}

		public bool Arrived(CPos destination)
		{
			return Members.Length != 0 && Members.All(member => member.CurrentCell == destination);
		}
	}

	sealed class StealthRecalculateFleeOwnerState
	{
		public bool EntryValidated;
		public int LastObservedTick = -1;
		public int LastEvaluationTick = -1;
		public StealthRecalculateFleeDisposition Disposition = StealthRecalculateFleeDisposition.Retain;
		public StealthRecalculateFleeLiveCause LiveCause = StealthRecalculateFleeLiveCause.NoRoute;
		public string Fingerprint;
		public uint[] MemberIds = Array.Empty<uint>();
		public uint[] EnemyIds = Array.Empty<uint>();
		public StealthRecalculateFleeRouteEvaluation[] Evaluations =
			Array.Empty<StealthRecalculateFleeRouteEvaluation>();
		public CPos? Destination;
		public StealthTargetThreatScore? Danger;
		public long RouteRevision;
		public StealthRecalculateFleeOrderToken LastOrderToken;
		public long? LongRouteCacheRevision;

		public StealthRecalculateFleeOwnerState Clone()
		{
			var clone = (StealthRecalculateFleeOwnerState)MemberwiseClone();
			clone.MemberIds = MemberIds.ToArray();
			clone.EnemyIds = EnemyIds.ToArray();
			clone.Evaluations = Evaluations.ToArray();
			return clone;
		}
	}
}
