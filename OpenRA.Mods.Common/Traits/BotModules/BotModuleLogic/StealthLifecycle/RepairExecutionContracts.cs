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

namespace OpenRA.Mods.Common.Traits
{
	public enum StealthRepairDisposition { Retain, ResumeFight, Start, SquadConstruction }
	public enum StealthRepairLiveCause { Retreating, Healing, NoSafeRepair, RepairComplete, NoLiveMembers }
	public enum StealthRepairOrderKind { Retreat, Repair }

	public sealed class StealthRepairThreatFacts
	{
		readonly ReadOnlyCollection<StealthRepairMemberSnapshot> members;
		readonly ReadOnlyCollection<StealthRepairEnemySnapshot> enemies;
		readonly ReadOnlyCollection<CPos> routeCells;
		public uint RepairOptionActorId { get; }
		public IReadOnlyList<StealthRepairMemberSnapshot> Members => members;
		public IReadOnlyList<StealthRepairEnemySnapshot> Enemies => enemies;
		public IReadOnlyList<CPos> RouteCells => routeCells;
		public bool FormationCloaked { get; }
		public bool HasDetectorCoverage { get; }
		public bool PlannedDecloak => false;
		public bool PlannedAttack => false;
		public bool PlannedCurrentRangeEngagement => false;

		internal StealthRepairThreatFacts(uint repairOptionActorId,
			IEnumerable<StealthRepairMemberSnapshot> members,
			IEnumerable<StealthRepairEnemySnapshot> enemies, IEnumerable<CPos> routeCells,
			bool formationCloaked, bool hasDetectorCoverage)
		{
			var memberCopy = members?.OrderBy(member => member?.ActorId).ToArray();
			var enemyCopy = enemies?.OrderBy(enemy => enemy?.ActorId).ToArray();
			var cellCopy = routeCells?.ToArray();
			if (repairOptionActorId == 0 || memberCopy == null || memberCopy.Length == 0 ||
				memberCopy.Any(member => member == null) ||
				memberCopy.Select(member => member.ActorId).Distinct().Count() != memberCopy.Length ||
				enemyCopy == null || enemyCopy.Any(enemy => enemy == null) ||
				enemyCopy.Select(enemy => enemy.ActorId).Distinct().Count() != enemyCopy.Length ||
				cellCopy == null || cellCopy.Length == 0)
				throw new ArgumentException("Repair threat facts require exact live participants and route.");
			RepairOptionActorId = repairOptionActorId;
			this.members = Array.AsReadOnly(memberCopy);
			this.enemies = Array.AsReadOnly(enemyCopy);
			this.routeCells = Array.AsReadOnly(cellCopy);
			FormationCloaked = formationCloaked;
			HasDetectorCoverage = hasDetectorCoverage;
		}
	}

	public interface IStealthRepairThreatAdapter
	{
		StealthTargetThreatScore CalculateRouteDanger(StealthRepairThreatFacts facts);
	}

	public sealed class StealthRepairRouteEvaluation
	{
		public StealthRepairOptionSnapshot Option { get; }
		public StealthRepairRouteSnapshot Route { get; }
		public StealthRepairThreatFacts Facts { get; }
		public StealthTargetThreatScore StandardDanger { get; }
		public bool IsSafe => StandardDanger.ThreatRating == 0;

		internal StealthRepairRouteEvaluation(StealthRepairOptionSnapshot option,
			StealthRepairRouteSnapshot route, StealthRepairThreatFacts facts,
			StealthTargetThreatScore standardDanger)
		{
			Option = option ?? throw new ArgumentNullException(nameof(option));
			Route = route ?? throw new ArgumentNullException(nameof(route));
			Facts = facts ?? throw new ArgumentNullException(nameof(facts));
			if (route.RepairOptionActorId != option.ActorId ||
				facts.RepairOptionActorId != option.ActorId ||
				!facts.RouteCells.SequenceEqual(route.Cells))
				throw new ArgumentException("Repair route evaluation does not match its live option.");
			StandardDanger = standardDanger;
		}
	}

	public sealed class StealthRepairOrderToken : IEquatable<StealthRepairOrderToken>
	{
		readonly ReadOnlyCollection<uint> actorIds;
		public BehaviorId Owner { get; }
		public OwnershipEpoch Epoch { get; }
		public IReadOnlyList<uint> ActorIds => actorIds;
		public uint RepairOptionActorId { get; }
		public uint RouteIdentity { get; }
		public StealthRepairOrderKind Kind { get; }
		public long RouteRevision { get; }
		public long ActivityRevision { get; }

		internal StealthRepairOrderToken(BehaviorId owner, OwnershipEpoch epoch,
			IEnumerable<uint> actorIds, uint repairOptionActorId, uint routeIdentity,
			StealthRepairOrderKind kind, long routeRevision, long activityRevision)
		{
			var actors = actorIds?.OrderBy(id => id).ToArray();
			if (owner != BehaviorId.Repair || actors == null || actors.Length == 0 ||
				actors.Any(id => id == 0) || actors.Distinct().Count() != actors.Length ||
				repairOptionActorId == 0 || routeIdentity == 0 ||
				!Enum.IsDefined(typeof(StealthRepairOrderKind), kind) ||
				routeRevision < 0 || activityRevision < 0)
				throw new ArgumentException("Invalid Repair order token.");
			Owner = owner;
			Epoch = epoch;
			this.actorIds = Array.AsReadOnly(actors);
			RepairOptionActorId = repairOptionActorId;
			RouteIdentity = routeIdentity;
			Kind = kind;
			RouteRevision = routeRevision;
			ActivityRevision = activityRevision;
		}

		public bool Equals(StealthRepairOrderToken other)
		{
			return other != null && Owner == other.Owner && Epoch == other.Epoch &&
				RepairOptionActorId == other.RepairOptionActorId && RouteIdentity == other.RouteIdentity &&
				Kind == other.Kind && RouteRevision == other.RouteRevision &&
				ActivityRevision == other.ActivityRevision && actorIds.SequenceEqual(other.actorIds);
		}

		public override bool Equals(object obj) { return Equals(obj as StealthRepairOrderToken); }
		public override int GetHashCode()
		{
			unchecked
			{
				var hash = ((int)Owner * 397) ^ Epoch.GetHashCode();
				hash = (hash * 397) ^ RepairOptionActorId.GetHashCode();
				hash = (hash * 397) ^ RouteIdentity.GetHashCode();
				hash = (hash * 397) ^ Kind.GetHashCode();
				hash = (hash * 397) ^ RouteRevision.GetHashCode();
				hash = (hash * 397) ^ ActivityRevision.GetHashCode();
				foreach (var actorId in actorIds)
					hash = (hash * 397) ^ actorId.GetHashCode();
				return hash;
			}
		}
	}

	public interface IStealthRepairOrders
	{
		void IssueRepair(BehaviorId owner, OwnershipEpoch epoch,
			IReadOnlyList<uint> actorIds, uint repairOptionActorId,
			IReadOnlyList<CPos> liveRoute, StealthRepairOrderKind kind,
			StealthRepairOrderToken token);
	}

	public sealed class StealthRepairCompletionEvidence
	{
		readonly ReadOnlyCollection<StealthRepairDamagedMember> members;
		public int Tick { get; }
		public IReadOnlyList<StealthRepairDamagedMember> Members => members;
		internal StealthRepairCompletionEvidence(int tick,
			IEnumerable<StealthRepairDamagedMember> members)
		{
			var copy = members?.OrderBy(member => member.ActorId).ToArray();
			if (tick < 0 || copy == null || copy.Length == 0 ||
				copy.Any(member => member.HitPoints != member.MaximumHitPoints) ||
				copy.Select(member => member.ActorId).Distinct().Count() != copy.Length)
				throw new ArgumentException("Repair completion must prove exact full health.");
			Tick = tick;
			this.members = Array.AsReadOnly(copy);
		}
	}

	public sealed class StealthRepairResult
	{
		readonly ReadOnlyCollection<uint> memberIds;
		readonly ReadOnlyCollection<uint> repairMemberIds;
		readonly ReadOnlyCollection<uint> enemyIds;
		readonly ReadOnlyCollection<StealthRepairRouteEvaluation> evaluations;
		internal StealthRepairHandoff Source { get; }
		internal StealthBehaviorHandoff Handoff => Source.Handoff;
		public StealthApproachMission Mission => Source.Mission;
		public StealthRepairResumeContext Resume => Source.Resume;
		public StealthRepairDisposition Disposition { get; }
		public StealthRepairLiveCause LiveCause { get; }
		public IReadOnlyList<uint> ActiveMemberActorIds => memberIds;
		public IReadOnlyList<uint> ActiveRepairMemberActorIds => repairMemberIds;
		public IReadOnlyList<uint> LiveEnemyActorIds => enemyIds;
		public IReadOnlyList<StealthRepairRouteEvaluation> RouteEvaluations => evaluations;
		public uint? SelectedRepairOptionActorId { get; }
		public uint? SelectedRouteIdentity { get; }
		public int RouteProgress { get; }
		public StealthTargetThreatScore? SelectedStandardDanger { get; }
		public StealthRepairOrderToken LastOrderToken { get; }
		public StealthRepairCompletionEvidence Completion { get; }
		public string LiveFingerprint { get; }
		public long? LongRouteCacheRevision { get; }

		internal StealthRepairResult(StealthRepairHandoff source,
			StealthRepairDisposition disposition, StealthRepairLiveCause liveCause,
			IEnumerable<uint> members, IEnumerable<uint> repairMembers, IEnumerable<uint> enemies,
			IEnumerable<StealthRepairRouteEvaluation> evaluations,
			uint? optionId, uint? routeId, int routeProgress,
			StealthTargetThreatScore? danger, StealthRepairOrderToken token,
			StealthRepairCompletionEvidence completion, string fingerprint,
			long? longRouteCacheRevision)
		{
			Source = source ?? throw new ArgumentNullException(nameof(source));
			Disposition = disposition;
			LiveCause = liveCause;
			memberIds = Canonical(members, nameof(members));
			repairMemberIds = Canonical(repairMembers, nameof(repairMembers));
			if (repairMemberIds.Any(id => !memberIds.Contains(id)))
				throw new ArgumentException("Repair members must remain in the live squad.");
			enemyIds = Canonical(enemies, nameof(enemies));
			var routeCopy = evaluations?.ToArray() ?? throw new ArgumentNullException(nameof(evaluations));
			if (routeCopy.Any(route => route == null) ||
				routeCopy.Select(route => route.Route.StableIdentity).Distinct().Count() != routeCopy.Length)
				throw new ArgumentException("Repair evaluations must be unique.", nameof(evaluations));
			this.evaluations = Array.AsReadOnly(routeCopy);
			SelectedRepairOptionActorId = optionId;
			SelectedRouteIdentity = routeId;
			RouteProgress = routeProgress;
			SelectedStandardDanger = danger;
			LastOrderToken = token;
			Completion = completion;
			LiveFingerprint = !string.IsNullOrEmpty(fingerprint) ? fingerprint :
				throw new ArgumentException("Repair results require a live fingerprint.");
			LongRouteCacheRevision = longRouteCacheRevision;
			ValidateShape();
		}

		void ValidateShape()
		{
			if (!Enum.IsDefined(typeof(StealthRepairDisposition), Disposition) ||
				!Enum.IsDefined(typeof(StealthRepairLiveCause), LiveCause) || RouteProgress < 0)
				throw new ArgumentException("Invalid Repair result disposition.");
			var routed = SelectedRepairOptionActorId.HasValue && SelectedRouteIdentity.HasValue &&
				SelectedStandardDanger.HasValue && LastOrderToken != null;
			var retaining = LiveCause == StealthRepairLiveCause.Retreating ||
				LiveCause == StealthRepairLiveCause.Healing;
			if (routed != retaining || retaining != (Disposition == StealthRepairDisposition.Retain))
				throw new ArgumentException("Repair route does not match disposition.");
			if (routed && (!evaluations.Any(evaluation =>
					evaluation.Option.ActorId == SelectedRepairOptionActorId &&
					evaluation.Route.StableIdentity == SelectedRouteIdentity &&
					evaluation.IsSafe && SameScore(evaluation.StandardDanger, SelectedStandardDanger.Value)) ||
				LastOrderToken.Owner != BehaviorId.Repair || LastOrderToken.Epoch != Handoff.Epoch ||
				!LastOrderToken.ActorIds.SequenceEqual(repairMemberIds) ||
				LastOrderToken.RepairOptionActorId != SelectedRepairOptionActorId ||
				LastOrderToken.RouteIdentity != SelectedRouteIdentity))
				throw new ArgumentException("Repair route token is not exact.");
			if ((Disposition == StealthRepairDisposition.ResumeFight) !=
					(LiveCause == StealthRepairLiveCause.NoSafeRepair) ||
				(Disposition == StealthRepairDisposition.Start) !=
					(LiveCause == StealthRepairLiveCause.RepairComplete) ||
				(Disposition == StealthRepairDisposition.SquadConstruction) !=
					(LiveCause == StealthRepairLiveCause.NoLiveMembers) ||
				(Completion != null) != (Disposition == StealthRepairDisposition.Start) ||
				(Disposition == StealthRepairDisposition.SquadConstruction && memberIds.Count != 0))
				throw new ArgumentException("Repair terminal evidence is inconsistent.");
			if (Completion != null && (Completion.Members.Any(member =>
				!Source.DamagedMembers.Any(source => source.ActorId == member.ActorId)) ||
				!Completion.Members.Select(member => member.ActorId).SequenceEqual(repairMemberIds)))
				throw new ArgumentException("Repair completion must yield each surviving damaged member exactly once.");
		}

		static ReadOnlyCollection<uint> Canonical(IEnumerable<uint> ids, string parameter)
		{
			var copy = ids?.ToArray();
			if (copy == null || copy.Any(id => id == 0) ||
				!copy.SequenceEqual(copy.OrderBy(id => id)) || copy.Distinct().Count() != copy.Length)
				throw new ArgumentException("Repair result identities must be canonical.", parameter);
			return Array.AsReadOnly(copy);
		}

		internal static bool SameScore(StealthTargetThreatScore left, StealthTargetThreatScore right)
		{
			return left.ThreatRating.Equals(right.ThreatRating) && left.Crossover.Equals(right.Crossover);
		}
	}

	public sealed class StealthRepairFightResumeHandoff
	{
		internal StealthBehaviorHandoff Handoff { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public StealthRepairResumeContext Context { get; }
		internal StealthRepairFightResumeHandoff(StealthBehaviorHandoff handoff,
			StealthRepairResumeContext context)
		{
			if (handoff == null || context == null || handoff.Owner != context.Owner)
				throw new ArgumentException("Repair must resume the exact recorded fight owner.");
			Handoff = handoff;
			Context = context;
		}
	}

	public sealed class StealthRepairStartEntry
	{
		internal StealthBehaviorHandoff Handoff { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public uint ActorId { get; }
		internal StealthRepairStartEntry(StealthBehaviorHandoff handoff, uint actorId)
		{
			if (handoff == null || handoff.Owner != BehaviorId.Start || actorId == 0)
				throw new ArgumentException("Repair completion must enter Start with one actor.");
			Handoff = handoff;
			ActorId = actorId;
		}
	}

	public sealed class StealthRepairTransition
	{
		readonly ReadOnlyCollection<StealthRepairStartEntry> startEntries;
		public StealthBehaviorHandoff Retained { get; }
		public StealthRepairFightResumeHandoff ResumedFight { get; }
		public IReadOnlyList<StealthRepairStartEntry> StartEntries => startEntries;
		public StealthSquadConstructionRecoveryHandoff SquadConstructionEntry { get; }

		internal StealthRepairTransition(StealthBehaviorHandoff handoff, StealthRepairResult result)
		{
			if (result.Disposition == StealthRepairDisposition.Retain)
				Retained = handoff;
			else if (result.Disposition == StealthRepairDisposition.ResumeFight)
				ResumedFight = new StealthRepairFightResumeHandoff(handoff, result.Resume);
			else if (result.Disposition == StealthRepairDisposition.Start)
				startEntries = Array.AsReadOnly(result.Completion.Members.Select(member =>
					new StealthRepairStartEntry(handoff, member.ActorId)).ToArray());
			else
				SquadConstructionEntry = new StealthSquadConstructionRecoveryHandoff(
					handoff, result.Mission);
			if (startEntries == null)
				startEntries = Array.AsReadOnly(Array.Empty<StealthRepairStartEntry>());
		}
	}
}
