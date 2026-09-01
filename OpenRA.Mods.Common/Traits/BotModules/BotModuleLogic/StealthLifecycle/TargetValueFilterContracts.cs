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
	/// <summary>Immutable strategic-value result for one TargetAcquisition option.</summary>
	public sealed class StealthTargetValueOption
	{
		readonly ReadOnlyCollection<StealthStrategicTargetSnapshot> strategicTargets;

		public CPos StrategicCell { get; }
		public int? EstimatedTravelMilliseconds { get; }
		public bool IsIncumbent { get; }
		public IReadOnlyList<StealthStrategicTargetSnapshot> StrategicTargets => strategicTargets;
		public long StrategicValue { get; }
		public uint StableIdentity => strategicTargets.Count == 0 ? uint.MaxValue :
			strategicTargets[0].StableActorId;

		internal StealthTargetValueOption(StealthTargetOption option, long strategicValue)
		{
			if (option == null)
				throw new ArgumentNullException(nameof(option));
			if (strategicValue < 0)
				throw new ArgumentOutOfRangeException(nameof(strategicValue));

			StrategicCell = option.StrategicCell;
			EstimatedTravelMilliseconds = option.EstimatedTravelMilliseconds;
			IsIncumbent = option.IsIncumbent;
			strategicTargets = Array.AsReadOnly(option.StrategicTargets.ToArray());
			StrategicValue = strategicValue;
		}
	}

	public sealed class StealthTargetValueFilterResult
	{
		readonly ReadOnlyCollection<StealthTargetValueOption> options;

		internal StealthBehaviorHandoff Handoff { get; }
		public IReadOnlyList<StealthTargetValueOption> Options => options;
		public bool IsReadyForThreatFilter { get; }

		internal StealthTargetValueFilterResult(StealthBehaviorHandoff handoff,
			IEnumerable<StealthTargetValueOption> options, bool isReadyForThreatFilter)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetValueFilter)
				throw new ArgumentException(
					"The result must belong to TargetValueFilter.", nameof(handoff));
			if (options == null)
				throw new ArgumentNullException(nameof(options));

			this.options = Array.AsReadOnly(options.ToArray());
			IsReadyForThreatFilter = isReadyForThreatFilter;
		}
	}

	/// <summary>Typed immutable boundary between lifecycle Steps 4A and 4B.</summary>
	public sealed class StealthTargetThreatFilterHandoff
	{
		readonly ReadOnlyCollection<StealthTargetValueOption> options;

		internal StealthBehaviorHandoff Handoff { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public IReadOnlyList<StealthTargetValueOption> Options => options;

		internal StealthTargetThreatFilterHandoff(StealthBehaviorHandoff handoff,
			IEnumerable<StealthTargetValueOption> options)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetThreatFilter)
				throw new ArgumentException("The handoff must belong to TargetThreatFilter.", nameof(handoff));
			if (options == null)
				throw new ArgumentNullException(nameof(options));

			this.options = Array.AsReadOnly(options.ToArray());
		}
	}
}
