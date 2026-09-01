#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Step 4C: choose a nearby survivor while rewarding separation from other squads.</summary>
	public sealed class StealthTargetDistanceChoiceBehavior
	{
		sealed class DistanceFact
		{
			public StealthTargetThreatOption Option { get; }
			public long SeparationSquared { get; }
			public int SeparationCreditMilliseconds { get; }
			public long AdjustedTravelMilliseconds { get; }

			public DistanceFact(StealthTargetThreatOption option, long separationSquared,
				int separationCreditMilliseconds, long adjustedTravelMilliseconds)
			{
				Option = option;
				SeparationSquared = separationSquared;
				SeparationCreditMilliseconds = separationCreditMilliseconds;
				AdjustedTravelMilliseconds = adjustedTravelMilliseconds;
			}
		}

		readonly StealthTargetDistanceChoiceHandoff handoff;
		readonly CPos[] otherSquadCells;
		readonly StealthTargetDistanceChoicePolicy policy;

		public StealthTargetDistanceChoiceBehavior(StealthTargetDistanceChoiceHandoff handoff,
			IEnumerable<StealthActiveSquadTargetSnapshot> otherActiveSquads,
			StealthTargetDistanceChoicePolicy policy)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetDistanceChoice || handoff.Options.Count == 0)
				throw new ArgumentException("TargetDistanceChoice requires candidate ownership.", nameof(handoff));
			if (otherActiveSquads == null)
				throw new ArgumentNullException(nameof(otherActiveSquads));
			this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
			var squads = otherActiveSquads.OrderBy(squad => squad?.StableActorId).ToArray();
			if (squads.Any(squad => squad == null) ||
				squads.Select(squad => squad.StableActorId).Distinct().Count() != squads.Length)
				throw new ArgumentException("Other squads require unique identities.", nameof(otherActiveSquads));
			otherSquadCells = squads.Select(squad => squad.StrategicCell).ToArray();
		}

		public StealthTargetDistanceChoiceResult Execute()
		{
			var selected = handoff.Options.Select(Fact)
				.OrderBy(fact => fact.AdjustedTravelMilliseconds)
				.ThenByDescending(fact => fact.SeparationSquared)
				.ThenBy(fact => fact.Option.ValueOption.EstimatedTravelMilliseconds ?? int.MaxValue)
				.ThenBy(fact => fact.Option.StableIdentity)
				.ThenBy(fact => fact.Option.StrategicCell.Y)
				.ThenBy(fact => fact.Option.StrategicCell.X).First();
			return new StealthTargetDistanceChoiceResult(handoff.Handoff,
				new StealthApproachMission(selected.Option, selected.SeparationSquared,
					selected.SeparationCreditMilliseconds, selected.AdjustedTravelMilliseconds));
		}

		DistanceFact Fact(StealthTargetThreatOption option)
		{
			var separation = StealthAIThreatGeometry.MinimumCellSeparationSquared(
				option.StrategicCell, otherSquadCells);
			var travel = option.ValueOption.EstimatedTravelMilliseconds;
			var credit = separation == long.MaxValue || !travel.HasValue ? 0 : (int)Math.Min(
				policy.MaximumSeparationCreditMilliseconds,
				Math.Min(separation, int.MaxValue) * policy.SeparationCreditPerSquaredCellMilliseconds);
			var adjusted = travel.HasValue ? Math.Max(0L, travel.Value - (long)credit) : long.MaxValue;
			return new DistanceFact(option, separation, credit, adjusted);
		}
	}
}
