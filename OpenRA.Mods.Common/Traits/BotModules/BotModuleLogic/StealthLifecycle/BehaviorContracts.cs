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
		TargetChoosing,
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

	public sealed class StealthBehaviorResult
	{
		internal StealthBehaviorHandoff Handoff { get; }
		internal BehaviorId NextOwner { get; }

		StealthBehaviorResult(StealthBehaviorHandoff handoff, BehaviorId nextOwner)
		{
			Handoff = handoff;
			NextOwner = nextOwner;
		}

		public static StealthBehaviorResult Complete(StealthBehaviorHandoff handoff, BehaviorId nextOwner)
		{
			if (handoff == null)
				throw new ArgumentNullException(nameof(handoff));
			if (!Enum.IsDefined(typeof(BehaviorId), nextOwner))
				throw new ArgumentOutOfRangeException(nameof(nextOwner));

			return new StealthBehaviorResult(handoff, nextOwner);
		}
	}
}
