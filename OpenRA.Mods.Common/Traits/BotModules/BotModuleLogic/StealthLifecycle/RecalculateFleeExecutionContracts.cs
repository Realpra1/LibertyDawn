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
		internal StealthRecalculateFleeHandoff Source { get; }
		internal StealthBehaviorHandoff Handoff => Source.Handoff;
		public StealthApproachMission Mission { get; }
		public StealthRecalculateFleeEntryEvidence EntryCause => Source.Evidence;
		public StealthRecalculateFleeDisposition Disposition { get; }
		public StealthRecalculateFleeLiveCause LiveCause { get; }
		public IReadOnlyList<uint> ActiveMemberActorIds => memberIds;
		public IReadOnlyList<uint> LiveEnemyActorIds => enemyIds;
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
						LiveCause == StealthRecalculateFleeLiveCause.NoTarget ||
						LiveCause == StealthRecalculateFleeLiveCause.NoRoute ||
						LiveCause == StealthRecalculateFleeLiveCause.SafeToReconsider))
				throw new ArgumentException("Invalid RecalculateFlee result disposition.");
			var hasRoute = SelectedDestinationCell.HasValue && SelectedStandardDanger.HasValue &&
				LastOrderToken != null;
			var routeCause = LiveCause == StealthRecalculateFleeLiveCause.Traversing ||
				(LiveCause == StealthRecalculateFleeLiveCause.MemberLoss && memberIds.Count != 0);
			if (routeCause != hasRoute)
				throw new ArgumentException("Flee route shape does not match its live cause.");
			if (hasRoute && (memberIds.Count == 0 || enemyIds.Count == 0 ||
				LastOrderToken.Owner != BehaviorId.RecalculateFlee ||
				LastOrderToken.Epoch != Handoff.Epoch ||
				!LastOrderToken.ActorIds.SequenceEqual(memberIds) || OrderedRoute.Count != 1 ||
				RouteProgress != 0 ||
				LastOrderToken.DestinationCell != OrderedRoute[RouteProgress] ||
				OrderedRoute[0] != SelectedDestinationCell))
				throw new ArgumentException("Flee route token is not exact.");
			if (!hasRoute && (OrderedRoute.Count != 0 || RouteProgress != 0))
				throw new ArgumentException("Flee terminal result cannot retain route progress.");
			if ((LiveCause == StealthRecalculateFleeLiveCause.NoTarget && enemyIds.Count != 0) ||
				(LiveCause == StealthRecalculateFleeLiveCause.NoRoute && enemyIds.Count == 0) ||
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
