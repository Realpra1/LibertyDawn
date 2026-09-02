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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace OpenRA.Mods.Common.Traits
{
	public sealed class StealthRecalculateFleeEntryThreatFacts
	{
		readonly ReadOnlyCollection<uint> memberIds;
		readonly ReadOnlyCollection<StealthRecalculateFleeEnemySnapshot> enemies;
		public StealthRecalculateFleeSource Source { get; }
		public uint SelectedTargetActorId { get; }
		public CPos SelectedTargetCurrentCell { get; }
		public IReadOnlyList<uint> MemberActorIds => memberIds;
		public IReadOnlyList<StealthRecalculateFleeEnemySnapshot> Enemies => enemies;
		public bool FormationCloaked { get; }
		public bool PlannedDecloak => true;
		public bool PlannedAttack => true;
		public bool PlannedCurrentRangeEngagement => true;

		internal StealthRecalculateFleeEntryThreatFacts(StealthRecalculateFleeSource source,
			uint targetId, CPos targetCell, IEnumerable<uint> memberIds,
			IEnumerable<StealthRecalculateFleeEnemySnapshot> enemies, bool formationCloaked)
		{
			var members = memberIds?.OrderBy(id => id).ToArray();
			var enemyCopy = enemies?.OrderBy(enemy => enemy?.ActorId).ToArray();
			if (!Enum.IsDefined(typeof(StealthRecalculateFleeSource), source) || targetId == 0 ||
				members == null || members.Length == 0 || members.Any(id => id == 0) ||
				members.Distinct().Count() != members.Length || enemyCopy == null || enemyCopy.Length == 0 ||
				enemyCopy.Any(enemy => enemy == null) ||
				enemyCopy.Select(enemy => enemy.ActorId).Distinct().Count() != enemyCopy.Length ||
				!enemyCopy.Any(enemy => enemy.ActorId == targetId && enemy.CurrentCell == targetCell))
				throw new ArgumentException("Entry threat facts require exact current participants and target.");
			Source = source;
			SelectedTargetActorId = targetId;
			SelectedTargetCurrentCell = targetCell;
			this.memberIds = Array.AsReadOnly(members);
			this.enemies = Array.AsReadOnly(enemyCopy);
			FormationCloaked = formationCloaked;
		}
	}

	public sealed class StealthRecalculateFleeThreatFacts
	{
		readonly ReadOnlyCollection<StealthRecalculateFleeMemberSnapshot> members;
		readonly ReadOnlyCollection<StealthRecalculateFleeEnemySnapshot> enemies;
		public CPos CandidateCell { get; }
		public IReadOnlyList<StealthRecalculateFleeMemberSnapshot> Members => members;
		public IReadOnlyList<StealthRecalculateFleeEnemySnapshot> Enemies => enemies;
		public bool FormationCloaked { get; }
		public bool HasDetectorCoverage { get; }
		public bool PlannedDecloak => false;
		public bool PlannedAttack => false;
		public bool PlannedCurrentRangeEngagement => false;

		internal StealthRecalculateFleeThreatFacts(CPos candidateCell,
			IEnumerable<StealthRecalculateFleeMemberSnapshot> members,
			IEnumerable<StealthRecalculateFleeEnemySnapshot> enemies, bool formationCloaked,
			bool hasDetectorCoverage)
		{
			var memberCopy = members?.OrderBy(member => member?.ActorId).ToArray();
			var enemyCopy = enemies?.OrderBy(enemy => enemy?.ActorId).ToArray();
			if (memberCopy == null || memberCopy.Length == 0 || memberCopy.Any(member => member == null) ||
				memberCopy.Select(member => member.ActorId).Distinct().Count() != memberCopy.Length ||
				enemyCopy == null || enemyCopy.Length == 0 || enemyCopy.Any(enemy => enemy == null) ||
				enemyCopy.Select(enemy => enemy.ActorId).Distinct().Count() != enemyCopy.Length)
				throw new ArgumentException("Flee threat facts require unique current participants.");
			CandidateCell = candidateCell;
			this.members = Array.AsReadOnly(memberCopy);
			this.enemies = Array.AsReadOnly(enemyCopy);
			FormationCloaked = formationCloaked;
			HasDetectorCoverage = hasDetectorCoverage;
		}
	}

	public interface IStealthRecalculateFleeThreatAdapter
	{
		StealthTargetThreatScore CalculateEntryCrossover(StealthRecalculateFleeEntryThreatFacts facts);
		StealthTargetThreatScore CalculateRouteDanger(StealthRecalculateFleeThreatFacts facts);
	}

	public sealed class StealthRecalculateFleeRouteEvaluation
	{
		public StealthRecalculateFleeCandidateSnapshot Candidate { get; }
		public StealthRecalculateFleeThreatFacts Facts { get; }
		public StealthTargetThreatScore StandardDanger { get; }
		internal StealthRecalculateFleeRouteEvaluation(StealthRecalculateFleeCandidateSnapshot candidate,
			StealthRecalculateFleeThreatFacts facts, StealthTargetThreatScore standardDanger)
		{
			Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
			Facts = facts ?? throw new ArgumentNullException(nameof(facts));
			if (facts.CandidateCell != candidate.Cell)
				throw new ArgumentException("Route evaluation does not match its live candidate.");
			StandardDanger = standardDanger;
		}
	}

	public sealed class StealthRecalculateFleeOrderToken : IEquatable<StealthRecalculateFleeOrderToken>
	{
		readonly ReadOnlyCollection<uint> actorIds;
		public BehaviorId Owner { get; }
		public OwnershipEpoch Epoch { get; }
		public IReadOnlyList<uint> ActorIds => actorIds;
		public CPos DestinationCell { get; }
		public long RouteRevision { get; }
		public long ActivityRevision { get; }

		internal StealthRecalculateFleeOrderToken(BehaviorId owner, OwnershipEpoch epoch,
			IEnumerable<uint> actorIds, CPos destinationCell, long routeRevision,
			long activityRevision)
		{
			var actors = actorIds?.OrderBy(id => id).ToArray();
			if (owner != BehaviorId.RecalculateFlee || actors == null || actors.Length == 0 ||
				actors.Any(id => id == 0) || actors.Distinct().Count() != actors.Length ||
				routeRevision < 0 || activityRevision < 0)
				throw new ArgumentException("Invalid RecalculateFlee order token.");
			Owner = owner;
			Epoch = epoch;
			this.actorIds = Array.AsReadOnly(actors);
			DestinationCell = destinationCell;
			RouteRevision = routeRevision;
			ActivityRevision = activityRevision;
		}

		public bool Equals(StealthRecalculateFleeOrderToken other)
		{
			return other != null && Owner == other.Owner && Epoch == other.Epoch &&
				DestinationCell == other.DestinationCell && RouteRevision == other.RouteRevision &&
				ActivityRevision == other.ActivityRevision && actorIds.SequenceEqual(other.actorIds);
		}

		public override bool Equals(object obj) { return Equals(obj as StealthRecalculateFleeOrderToken); }
		public override int GetHashCode()
		{
			unchecked
			{
				var hash = ((int)Owner * 397) ^ Epoch.GetHashCode();
				hash = (hash * 397) ^ DestinationCell.GetHashCode();
				hash = (hash * 397) ^ RouteRevision.GetHashCode();
				hash = (hash * 397) ^ ActivityRevision.GetHashCode();
				foreach (var actorId in actorIds)
					hash = (hash * 397) ^ actorId.GetHashCode();
				return hash;
			}
		}
	}

	/// <summary>Applies one owner-bound move using the token as an external idempotency key.</summary>
	public interface IStealthRecalculateFleeOrders
	{
		void IssueMove(BehaviorId owner, OwnershipEpoch epoch, IReadOnlyList<uint> actorIds,
			CPos destinationCell, IReadOnlyList<CPos> orderedRoute, int routeProgress,
			StealthRecalculateFleeOrderToken token);
	}

	public sealed class StealthRecalculateFleeResult
	{
		readonly ReadOnlyCollection<uint> memberIds;
		readonly ReadOnlyCollection<uint> enemyIds;
		readonly ReadOnlyCollection<StealthRecalculateFleeRouteEvaluation> evaluations;
		internal StealthRecalculateFleeHandoff Source { get; }
		internal StealthBehaviorHandoff Handoff => Source.Handoff;
		public StealthApproachMission Mission { get; }
		public StealthRecalculateFleeEntryEvidence EntryCause => Source.Evidence;
		public StealthRecalculateFleeDisposition Disposition { get; }
		public StealthRecalculateFleeLiveCause LiveCause { get; }
		public IReadOnlyList<uint> ActiveMemberActorIds => memberIds;
		public IReadOnlyList<uint> LiveEnemyActorIds => enemyIds;
		public IReadOnlyList<StealthRecalculateFleeRouteEvaluation> RouteEvaluations => evaluations;
		public CPos? SelectedDestinationCell { get; }
		public StealthTargetThreatScore? SelectedStandardDanger { get; }
		public IReadOnlyList<CPos> OrderedRoute { get; }
		public int RouteProgress { get; }
		public StealthRecalculateFleeOrderToken LastOrderToken { get; }
		public string LiveFingerprint { get; }
		public long? LongRouteCacheRevision { get; }

		internal StealthRecalculateFleeResult(StealthRecalculateFleeHandoff source,
			StealthRecalculateFleeDisposition disposition, StealthRecalculateFleeLiveCause liveCause,
			IEnumerable<uint> members, IEnumerable<uint> enemies,
			IEnumerable<StealthRecalculateFleeRouteEvaluation> evaluations,
			CPos? destination, StealthTargetThreatScore? danger,
			IEnumerable<CPos> orderedRoute, int routeProgress,
			StealthRecalculateFleeOrderToken lastOrderToken, string fingerprint,
			long? longRouteCacheRevision)
		{
			Source = source ?? throw new ArgumentNullException(nameof(source));
			Mission = source.Mission;
			Disposition = disposition;
			LiveCause = liveCause;
			memberIds = CanonicalIds(members, nameof(members));
			enemyIds = CanonicalIds(enemies, nameof(enemies));
			var routes = evaluations?.ToArray() ?? throw new ArgumentNullException(nameof(evaluations));
			if (routes.Any(route => route == null) || routes.Select(route => route.Candidate.Cell).Distinct().Count() != routes.Length)
				throw new ArgumentException("Route evaluations must be unique.", nameof(evaluations));
			this.evaluations = Array.AsReadOnly(routes);
			SelectedDestinationCell = destination;
			SelectedStandardDanger = danger;
			OrderedRoute = Array.AsReadOnly(orderedRoute?.ToArray() ??
				throw new ArgumentNullException(nameof(orderedRoute)));
			RouteProgress = routeProgress;
			LastOrderToken = lastOrderToken;
			LiveFingerprint = !string.IsNullOrEmpty(fingerprint) ? fingerprint :
				throw new ArgumentException("Results require a live fingerprint.", nameof(fingerprint));
			LongRouteCacheRevision = longRouteCacheRevision;
			ValidateShape();
		}

		void ValidateShape()
		{
			if (!Enum.IsDefined(typeof(StealthRecalculateFleeDisposition), Disposition) ||
				!Enum.IsDefined(typeof(StealthRecalculateFleeLiveCause), LiveCause) ||
				Disposition == StealthRecalculateFleeDisposition.TargetAcquisition !=
					(LiveCause == StealthRecalculateFleeLiveCause.Completed ||
						LiveCause == StealthRecalculateFleeLiveCause.NoTarget))
				throw new ArgumentException("Invalid RecalculateFlee result disposition.");
			var hasRoute = SelectedDestinationCell.HasValue && SelectedStandardDanger.HasValue &&
				LastOrderToken != null && evaluations.Any(route => route.Candidate.Cell == SelectedDestinationCell &&
					SameScore(route.StandardDanger, SelectedStandardDanger.Value));
			var routeCause = LiveCause == StealthRecalculateFleeLiveCause.Traversing ||
				LiveCause == StealthRecalculateFleeLiveCause.Completed ||
				(LiveCause == StealthRecalculateFleeLiveCause.MemberLoss && memberIds.Count != 0);
			if (routeCause != hasRoute)
				throw new ArgumentException("Flee route shape does not match its live cause.");
			if (hasRoute && (memberIds.Count == 0 || enemyIds.Count == 0 ||
				LastOrderToken.Owner != BehaviorId.RecalculateFlee ||
				LastOrderToken.Epoch != Handoff.Epoch ||
				!LastOrderToken.ActorIds.SequenceEqual(memberIds) || OrderedRoute.Count == 0 ||
				RouteProgress < 0 || RouteProgress >= OrderedRoute.Count ||
				LastOrderToken.DestinationCell != OrderedRoute[RouteProgress] ||
				(LongRouteCacheRevision == null &&
					(OrderedRoute.Count != 1 || OrderedRoute[0] != SelectedDestinationCell))))
				throw new ArgumentException("Flee route token is not exact.");
			if (!hasRoute && (OrderedRoute.Count != 0 || RouteProgress != 0))
				throw new ArgumentException("Flee terminal result cannot retain route progress.");
			if ((LiveCause == StealthRecalculateFleeLiveCause.NoTarget && enemyIds.Count != 0) ||
				(LiveCause == StealthRecalculateFleeLiveCause.NoRoute &&
					(enemyIds.Count == 0 || evaluations.Count != 0)) ||
				(LiveCause == StealthRecalculateFleeLiveCause.MemberLoss &&
					memberIds.SequenceEqual(EntryCause.MemberActorIds)))
				throw new ArgumentException("Flee outcome has no exact live cause.");
		}

		static ReadOnlyCollection<uint> CanonicalIds(IEnumerable<uint> ids, string name)
		{
			var copy = ids?.ToArray();
			if (copy == null || copy.Any(id => id == 0) || !copy.SequenceEqual(copy.OrderBy(id => id)) ||
				copy.Distinct().Count() != copy.Length)
				throw new ArgumentException("Result identities must be canonical.", name);
			return Array.AsReadOnly(copy);
		}

		static bool SameScore(StealthTargetThreatScore left, StealthTargetThreatScore right)
		{
			return left.ThreatRating.Equals(right.ThreatRating) && left.Crossover.Equals(right.Crossover);
		}
	}

	public sealed class StealthRecalculateFleeTransition
	{
		public StealthBehaviorHandoff Retained { get; }
		public StealthBehaviorHandoff TargetAcquisition { get; }
		internal StealthRecalculateFleeTransition(StealthBehaviorHandoff handoff,
			StealthRecalculateFleeResult result)
		{
			if (result.Disposition == StealthRecalculateFleeDisposition.Retain)
				Retained = handoff;
			else
				TargetAcquisition = handoff;
		}
	}

	static class StealthRecalculateFleeFingerprint
	{
		public static string Create(StealthRecalculateFleeLiveSnapshot live)
		{
			var text = new StringBuilder();
			text.Append("C=").Append(live.FormationCloaked ? 1 : 0).Append(";S=")
				.Append(live.SourceFingerprint).Append(";M=");
			foreach (var member in live.Members)
				text.Append(member.ActorId).Append(',').Append(member.CurrentCell.Bits).Append(',')
					.Append(member.CurrentWeaponRangeCells).Append(',').Append(member.HitPoints).Append(',')
					.Append(member.MaximumHitPoints).Append(',').Append(member.IsInWorld ? 1 : 0).Append(',')
					.Append(member.IsDead ? 1 : 0).Append('|');
			text.Append(";E=");
			foreach (var enemy in live.Enemies)
				text.Append(Enemy(enemy));
			text.Append(";P=");
			foreach (var candidate in live.Candidates)
				text.Append(candidate.Cell.Bits).Append(',').Append(candidate.IsPassable ? 1 : 0).Append(',')
					.Append(candidate.RequiresStrategicRouting ? 1 : 0).Append(',')
					.Append(candidate.HasDetectorCoverage ? 1 : 0).Append('|');
			return text.ToString();
		}

		public static string FromMassAttack(StealthMassAttackThreatFacts facts)
		{
			var text = new StringBuilder("MASS;C=").Append(facts.FormationCloaked ? 1 : 0)
				.Append(";T=").Append(facts.SelectedTargetActorId).Append(',')
				.Append(facts.SelectedTargetCurrentCell.Bits).Append(";M=");
			foreach (var id in facts.FriendlyActorIds)
				text.Append(id).Append('|');
			text.Append(";E=");
			foreach (var enemy in facts.Enemies)
				text.Append(enemy.ActorId).Append(',').Append(enemy.ActorType).Append(',')
					.Append(enemy.CurrentCell.Bits).Append(',').Append(enemy.HitPoints).Append(',')
					.Append(enemy.MaximumHitPoints).Append(',').Append(enemy.CurrentWeaponRangeCells).Append(',')
					.Append(enemy.HasDetectorCoverage ? 1 : 0).Append('|');
			return text.ToString();
		}

		static string Enemy(StealthRecalculateFleeEnemySnapshot enemy)
		{
			return new StringBuilder().Append(enemy.ActorId).Append(',').Append(enemy.ActorType).Append(',')
				.Append(enemy.CurrentCell.Bits).Append(',').Append(enemy.HitPoints).Append(',')
				.Append(enemy.MaximumHitPoints).Append(',').Append(enemy.CurrentWeaponRangeCells).Append(',')
				.Append(enemy.HasDetectorCoverage ? 1 : 0).Append(',')
				.Append(enemy.IsInLocalEngagementArea ? 1 : 0).Append(',')
				.Append(enemy.IsInWorld ? 1 : 0).Append(',').Append(enemy.IsDead ? 1 : 0).Append(',')
				.Append(enemy.IsTargetable ? 1 : 0).Append('|').ToString();
		}
	}
}
