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
	public readonly struct StealthTargetThreatScore
	{
		public double ThreatRating { get; }
		public double Crossover { get; }

		public StealthTargetThreatScore(double threatRating, double crossover)
		{
			if (!double.IsFinite(threatRating) || threatRating < 0)
				throw new ArgumentOutOfRangeException(nameof(threatRating));
			if (double.IsNaN(crossover) || crossover < 0)
				throw new ArgumentOutOfRangeException(nameof(crossover));

			ThreatRating = threatRating;
			Crossover = crossover;
		}
	}

	public interface IStealthTargetThreatAdapter
	{
		StealthTargetThreatScore Calculate(StealthTargetThreatFacts facts);
	}

	/// <summary>
	/// Adapts immutable strategic-cache facts to the standard combat threat calculation.
	/// Cloak and detector facts affect targetability only; all rating math remains standard.
	/// </summary>
	public sealed class GeneralizedCombatTargetThreatAdapter : IStealthTargetThreatAdapter
	{
		readonly Func<string, bool> isCachedCombatType;
		readonly Func<string, string, double> cachedThreatRating;

		public GeneralizedCombatTargetThreatAdapter(GeneralizedCombatThreatCalculator calculator)
		{
			if (calculator == null)
				throw new ArgumentNullException(nameof(calculator));

			isCachedCombatType = actorType => calculator.Cache.ContainsKey(
				GeneralizedCombatThreatCalculator.CanonicalKey(actorType, actorType));
			cachedThreatRating = (friendlyType, enemyType) =>
			{
				if (!calculator.TryGet(friendlyType, enemyType, out var pair))
					throw new InvalidOperationException(
						$"No cached matchup exists for {friendlyType}/{enemyType}.");
				return pair.DefenderThreatInAttackerEquivalents;
			};
		}

		internal GeneralizedCombatTargetThreatAdapter(Func<string, bool> isCachedCombatType,
			Func<string, string, double> cachedThreatRating)
		{
			this.isCachedCombatType = isCachedCombatType ?? throw new ArgumentNullException(nameof(isCachedCombatType));
			this.cachedThreatRating = cachedThreatRating ?? throw new ArgumentNullException(nameof(cachedThreatRating));
		}

		public StealthTargetThreatScore Calculate(StealthTargetThreatFacts facts)
		{
			return CalculateStandard(facts, isCachedCombatType, cachedThreatRating);
		}

		public static StealthTargetThreatScore CalculateStandard(StealthTargetThreatFacts facts,
			Func<string, bool> isCachedCombatType,
			Func<string, string, double> cachedThreatRating)
		{
			if (facts == null)
				throw new ArgumentNullException(nameof(facts));
			if (isCachedCombatType == null)
				throw new ArgumentNullException(nameof(isCachedCombatType));
			if (cachedThreatRating == null)
				throw new ArgumentNullException(nameof(cachedThreatRating));

			var friendly = facts.FriendlyGroup.Where(snapshot => isCachedCombatType(snapshot.ActorType))
				.Select(ToGroupType).ToArray();
			var enemy = facts.EnemyGroup.Where(snapshot => isCachedCombatType(snapshot.ActorType))
				.Select(ToGroupType).ToArray();
			var canBeTargeted = !facts.FormationCloaked || facts.HasDetectorCoverage ||
				facts.PlannedActionRevealsFormation;
			var result = GeneralizedCombatThreatCalculator.CalculateMixedGroupThreat(
				friendly, enemy, (friendlyType, enemyType) =>
				{
					if (!canBeTargeted)
						return 0;
					return cachedThreatRating(friendlyType, enemyType);
				});

			return new StealthTargetThreatScore(result.ThreatRating, result.Crossover);
		}

		static GeneralizedCombatThreatCalculator.GroupTypeCount ToGroupType(
			StealthCombatGroupSnapshot snapshot)
		{
			return new GeneralizedCombatThreatCalculator.GroupTypeCount(
				snapshot.ActorType, snapshot.Count, snapshot.EconomicValue);
		}
	}

	public sealed class StealthTargetThreatOption
	{
		public StealthTargetValueOption ValueOption { get; }
		public CPos StrategicCell => ValueOption.StrategicCell;
		public uint StableIdentity => ValueOption.StableIdentity;
		public double ThreatRating { get; }
		public double Crossover { get; }

		internal StealthTargetThreatOption(StealthTargetValueOption valueOption,
			StealthTargetThreatScore score)
		{
			ValueOption = valueOption ?? throw new ArgumentNullException(nameof(valueOption));
			ThreatRating = score.ThreatRating;
			Crossover = score.Crossover;
		}
	}

	public sealed class StealthTargetThreatFilterResult
	{
		readonly ReadOnlyCollection<StealthTargetThreatOption> options;

		internal StealthBehaviorHandoff Handoff { get; }
		public IReadOnlyList<StealthTargetThreatOption> Options => options;
		public bool IsReadyForDistanceChoice { get; }

		internal StealthTargetThreatFilterResult(StealthBehaviorHandoff handoff,
			IEnumerable<StealthTargetThreatOption> options, bool isReadyForDistanceChoice)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetThreatFilter)
				throw new ArgumentException(
					"The result must belong to TargetThreatFilter.", nameof(handoff));
			if (options == null)
				throw new ArgumentNullException(nameof(options));

			this.options = Array.AsReadOnly(options.ToArray());
			IsReadyForDistanceChoice = isReadyForDistanceChoice;
		}
	}

	/// <summary>Typed immutable boundary between lifecycle Steps 4B and 4C.</summary>
	public sealed class StealthTargetDistanceChoiceHandoff
	{
		readonly ReadOnlyCollection<StealthTargetThreatOption> options;

		internal StealthBehaviorHandoff Handoff { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public IReadOnlyList<StealthTargetThreatOption> Options => options;

		internal StealthTargetDistanceChoiceHandoff(StealthBehaviorHandoff handoff,
			IEnumerable<StealthTargetThreatOption> options)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetDistanceChoice)
				throw new ArgumentException("The handoff must belong to TargetDistanceChoice.", nameof(handoff));
			if (options == null)
				throw new ArgumentNullException(nameof(options));

			this.options = Array.AsReadOnly(options.ToArray());
		}
	}
}
