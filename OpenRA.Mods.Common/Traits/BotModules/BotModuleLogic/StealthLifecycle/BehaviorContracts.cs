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

namespace OpenRA.Mods.Common.Traits
{
	public enum BehaviorId
	{
		Start,
		SquadConstruction,
		TargetAcquisition,
		TargetValueFilter,
		TargetThreatFilter,
		TargetDistanceChoice,
		Engagement,
		Damage
	}

	public enum StealthLifecycleObservationKind
	{
		Timer,
		UnitBuilt,
		RepairCompleted,
		Damage,
		WorldEvent
	}

	public readonly struct OwnershipEpoch : IEquatable<OwnershipEpoch>
	{
		public long Value { get; }

		public OwnershipEpoch(long value)
		{
			if (value <= 0)
				throw new ArgumentOutOfRangeException(nameof(value));

			Value = value;
		}

		public bool Equals(OwnershipEpoch other) { return Value == other.Value; }

		public override bool Equals(object obj) { return obj is OwnershipEpoch other && Equals(other); }

		public override int GetHashCode() { return Value.GetHashCode(); }

		public static bool operator ==(OwnershipEpoch left, OwnershipEpoch right) { return left.Equals(right); }

		public static bool operator !=(OwnershipEpoch left, OwnershipEpoch right) { return !left.Equals(right); }
	}

	public readonly struct StealthLifecycleObservation
	{
		public StealthLifecycleObservationKind Kind { get; }
		public uint SubjectActorId { get; }

		public StealthLifecycleObservation(StealthLifecycleObservationKind kind, uint subjectActorId = 0)
		{
			if (!Enum.IsDefined(typeof(StealthLifecycleObservationKind), kind))
				throw new ArgumentOutOfRangeException(nameof(kind));

			Kind = kind;
			SubjectActorId = subjectActorId;
		}
	}

	public sealed class StealthLifecycleObservationFrame
	{
		readonly ReadOnlyCollection<StealthLifecycleObservation> observations;

		public int Tick { get; }
		public IReadOnlyList<StealthLifecycleObservation> Observations => observations;

		public StealthLifecycleObservationFrame(int tick,
			IEnumerable<StealthLifecycleObservation> observations)
		{
			if (tick < 0)
				throw new ArgumentOutOfRangeException(nameof(tick));
			if (observations == null)
				throw new ArgumentNullException(nameof(observations));

			Tick = tick;
			this.observations = Array.AsReadOnly(new List<StealthLifecycleObservation>(observations).ToArray());
		}
	}

	public sealed class StealthBehaviorHandoff
	{
		public BehaviorId Owner { get; }
		public OwnershipEpoch Epoch { get; }

		internal StealthBehaviorHandoff(BehaviorId owner, OwnershipEpoch epoch)
		{
			Owner = owner;
			Epoch = epoch;
		}
	}

	public enum StealthStartDisposition
	{
		ObservationOnly,
		Transition,
		Terminated
	}

	public readonly struct StealthStartMemberSnapshot
	{
		public uint ActorId { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }

		public StealthStartMemberSnapshot(uint actorId, bool isInWorld = true, bool isDead = false)
		{
			ActorId = actorId;
			IsInWorld = isInWorld;
			IsDead = isDead;
		}
	}

	public sealed class StealthStartResult
	{
		readonly ReadOnlyCollection<uint> memberActorIds;

		internal StealthBehaviorHandoff Handoff { get; }
		public StealthLifecycleObservationKind Source { get; }
		public uint SubjectActorId { get; }
		public StealthStartDisposition Disposition { get; }
		public IReadOnlyList<uint> MemberActorIds => memberActorIds;
		public bool HasTransition => Disposition == StealthStartDisposition.Transition;
		public bool IsTerminated => Disposition == StealthStartDisposition.Terminated;

		internal StealthStartResult(StealthBehaviorHandoff handoff,
			StealthLifecycleObservationKind source, uint subjectActorId,
			StealthStartDisposition disposition, IEnumerable<uint> memberActorIds)
		{
			if (handoff == null)
				throw new ArgumentNullException(nameof(handoff));
			if (!Enum.IsDefined(typeof(StealthLifecycleObservationKind), source))
				throw new ArgumentOutOfRangeException(nameof(source));
			if (!Enum.IsDefined(typeof(StealthStartDisposition), disposition))
				throw new ArgumentOutOfRangeException(nameof(disposition));
			if (memberActorIds == null)
				throw new ArgumentNullException(nameof(memberActorIds));

			Handoff = handoff;
			Source = source;
			SubjectActorId = subjectActorId;
			Disposition = disposition;
			this.memberActorIds = Array.AsReadOnly(new List<uint>(memberActorIds).ToArray());
		}
	}
}
