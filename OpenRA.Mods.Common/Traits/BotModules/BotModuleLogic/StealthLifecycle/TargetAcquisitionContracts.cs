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
	public enum StealthTargetAcquisitionDisposition
	{
		ReadyForValueFilter,
		MoveCloserAndRescan,
		AwaitingCache
	}

	public sealed class StealthCombatGroupSnapshot
	{
		public string ActorType { get; }
		public int Count { get; }
		public int EconomicValue { get; }

		public StealthCombatGroupSnapshot(string actorType, int count, int economicValue)
		{
			if (string.IsNullOrWhiteSpace(actorType))
				throw new ArgumentException("Actor types must be non-empty.", nameof(actorType));
			if (count <= 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if (economicValue < 0)
				throw new ArgumentOutOfRangeException(nameof(economicValue));

			ActorType = actorType.ToLowerInvariant();
			Count = count;
			EconomicValue = economicValue;
		}
	}

	/// <summary>Immutable strategic-cache combat facts for one candidate cell.</summary>
	public sealed class StealthTargetThreatFacts
	{
		readonly ReadOnlyCollection<StealthCombatGroupSnapshot> friendlyGroup;
		readonly ReadOnlyCollection<StealthCombatGroupSnapshot> enemyGroup;

		public CPos StrategicCell { get; }
		public IReadOnlyList<StealthCombatGroupSnapshot> FriendlyGroup => friendlyGroup;
		public IReadOnlyList<StealthCombatGroupSnapshot> EnemyGroup => enemyGroup;
		public bool FormationCloaked { get; }
		public bool HasDetectorCoverage { get; }
		public bool PlannedActionRevealsFormation { get; }

		public StealthTargetThreatFacts(CPos strategicCell,
			IEnumerable<StealthCombatGroupSnapshot> friendlyGroup,
			IEnumerable<StealthCombatGroupSnapshot> enemyGroup,
			bool formationCloaked, bool hasDetectorCoverage,
			bool plannedActionRevealsFormation = true)
		{
			StrategicCell = strategicCell;
			this.friendlyGroup = NormalizeGroup(friendlyGroup, nameof(friendlyGroup));
			this.enemyGroup = NormalizeGroup(enemyGroup, nameof(enemyGroup));
			FormationCloaked = formationCloaked;
			HasDetectorCoverage = hasDetectorCoverage;
			PlannedActionRevealsFormation = plannedActionRevealsFormation;
		}

		static ReadOnlyCollection<StealthCombatGroupSnapshot> NormalizeGroup(
			IEnumerable<StealthCombatGroupSnapshot> group, string parameterName)
		{
			if (group == null)
				throw new ArgumentNullException(parameterName);

			var snapshots = group.ToArray();
			if (snapshots.Any(snapshot => snapshot == null) || snapshots
				.Select(snapshot => snapshot.ActorType).Distinct(StringComparer.Ordinal).Count() != snapshots.Length)
				throw new ArgumentException("Combat group actor types must be unique.", parameterName);

			return Array.AsReadOnly(snapshots.OrderBy(snapshot => snapshot.ActorType,
				StringComparer.Ordinal).ToArray());
		}
	}

	/// <summary>
	/// Immutable strategic-cache view. No live actor or combat state is exposed to TargetAcquisition.
	/// Route-cost units are converted to estimated movement time by SecondsPerCostUnit.
	/// </summary>
	public sealed class StealthTargetAcquisitionCacheSnapshot
	{
		readonly ReadOnlyCollection<float> danger;
		readonly ReadOnlyCollection<CPos> enemyStrategicCells;
		readonly ReadOnlyCollection<StealthStrategicTargetSnapshot> strategicTargets;
		readonly ReadOnlyCollection<StealthTargetThreatFacts> threatFacts;

		public int Width { get; }
		public int Height { get; }
		public float SecondsPerCostUnit { get; }
		public IReadOnlyList<float> Danger => danger;
		public IReadOnlyList<CPos> EnemyStrategicCells => enemyStrategicCells;
		public IReadOnlyList<StealthStrategicTargetSnapshot> StrategicTargets => strategicTargets;
		public IReadOnlyList<StealthTargetThreatFacts> ThreatFacts => threatFacts;

		public StealthTargetAcquisitionCacheSnapshot(int width, int height,
			IEnumerable<float> danger, IEnumerable<CPos> enemyStrategicCells,
			float secondsPerCostUnit,
			IEnumerable<StealthStrategicTargetSnapshot> strategicTargets = null,
			IEnumerable<StealthTargetThreatFacts> threatFacts = null)
		{
			if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(width));
			if (danger == null)
				throw new ArgumentNullException(nameof(danger));
			if (enemyStrategicCells == null)
				throw new ArgumentNullException(nameof(enemyStrategicCells));
			if (!float.IsFinite(secondsPerCostUnit) || secondsPerCostUnit <= 0)
				throw new ArgumentOutOfRangeException(nameof(secondsPerCostUnit));

			var dangerCells = danger.ToArray();
			if (dangerCells.Length != width * height || dangerCells.Any(value => !float.IsFinite(value)))
				throw new ArgumentException("The strategic danger cache must exactly match its dimensions.",
					nameof(danger));

			var enemies = enemyStrategicCells.ToArray();
			if (enemies.Any(cell => cell.X < 0 || cell.Y < 0 || cell.X >= width || cell.Y >= height))
				throw new ArgumentException("Enemy strategic cells must be inside the cached grid.",
					nameof(enemyStrategicCells));
			var targets = (strategicTargets ?? Array.Empty<StealthStrategicTargetSnapshot>()).ToArray();
			if (targets.Any(target => target == null || target.StrategicCell.X < 0 ||
				target.StrategicCell.Y < 0 || target.StrategicCell.X >= width || target.StrategicCell.Y >= height))
				throw new ArgumentException("Strategic target snapshots must be inside the cached grid.",
					nameof(strategicTargets));
			if (targets.Select(target => target.StableActorId).Distinct().Count() != targets.Length)
				throw new ArgumentException("Strategic target snapshots must have unique stable identities.",
					nameof(strategicTargets));
			var facts = (threatFacts ?? Array.Empty<StealthTargetThreatFacts>()).ToArray();
			if (facts.Any(fact => fact == null || fact.StrategicCell.X < 0 || fact.StrategicCell.Y < 0 ||
				fact.StrategicCell.X >= width || fact.StrategicCell.Y >= height) ||
				facts.Select(fact => fact.StrategicCell).Distinct().Count() != facts.Length)
				throw new ArgumentException("Threat facts must have unique cells inside the cached grid.",
					nameof(threatFacts));

			Width = width;
			Height = height;
			SecondsPerCostUnit = secondsPerCostUnit;
			this.danger = Array.AsReadOnly(dangerCells);
			this.enemyStrategicCells = Array.AsReadOnly(enemies);
			this.strategicTargets = Array.AsReadOnly(targets.OrderBy(target => target.StrategicCell.Y)
				.ThenBy(target => target.StrategicCell.X).ThenBy(target => target.StableActorId).ToArray());
			this.threatFacts = Array.AsReadOnly(facts.OrderBy(fact => fact.StrategicCell.Y)
				.ThenBy(fact => fact.StrategicCell.X).ToArray());
		}
	}

	public interface IStealthTargetAcquisitionCache
	{
		StealthTargetAcquisitionCacheSnapshot ReadSnapshot();
	}

	/// <summary>
	/// Immutable value facts captured by the strategic cache. ConfiguredPriority has already been
	/// resolved by the established squad policy; no actor/type policy is duplicated in the lifecycle.
	/// </summary>
	public sealed class StealthStrategicTargetSnapshot
	{
		public uint StableActorId { get; }
		public CPos StrategicCell { get; }
		public int ConfiguredPriority { get; }
		public int ActorValue { get; }
		public int HitPoints { get; }
		public int MaximumHitPoints { get; }

		public StealthStrategicTargetSnapshot(uint stableActorId, CPos strategicCell,
			int configuredPriority, int actorValue, int hitPoints, int maximumHitPoints)
		{
			if (stableActorId == 0)
				throw new ArgumentOutOfRangeException(nameof(stableActorId));
			if (actorValue < 0)
				throw new ArgumentOutOfRangeException(nameof(actorValue));
			if (hitPoints < 0 || maximumHitPoints < 0)
				throw new ArgumentOutOfRangeException(nameof(hitPoints));

			StableActorId = stableActorId;
			StrategicCell = strategicCell;
			ConfiguredPriority = configuredPriority;
			ActorValue = actorValue;
			HitPoints = hitPoints;
			MaximumHitPoints = maximumHitPoints;
		}
	}

	public sealed class StealthTargetOption
	{
		readonly ReadOnlyCollection<StealthStrategicTargetSnapshot> strategicTargets;

		public CPos StrategicCell { get; }
		public int? EstimatedTravelMilliseconds { get; }
		public bool IsIncumbent { get; }
		public IReadOnlyList<StealthStrategicTargetSnapshot> StrategicTargets => strategicTargets;
		public StealthTargetThreatFacts ThreatFacts { get; }

		internal StealthTargetOption(CPos strategicCell,
			int? estimatedTravelMilliseconds, bool isIncumbent,
			IEnumerable<StealthStrategicTargetSnapshot> strategicTargets = null,
			StealthTargetThreatFacts threatFacts = null)
		{
			if (estimatedTravelMilliseconds < 0)
				throw new ArgumentOutOfRangeException(nameof(estimatedTravelMilliseconds));
			var targets = (strategicTargets ?? Array.Empty<StealthStrategicTargetSnapshot>()).ToArray();
			if (targets.Any(target => target == null || target.StrategicCell != strategicCell) ||
				targets.Select(target => target.StableActorId).Distinct().Count() != targets.Length)
				throw new ArgumentException(
					"Strategic target snapshots must be unique and belong to the option cell.",
					nameof(strategicTargets));
			if (threatFacts != null && threatFacts.StrategicCell != strategicCell)
				throw new ArgumentException("Threat facts must belong to the option cell.", nameof(threatFacts));

			StrategicCell = strategicCell;
			EstimatedTravelMilliseconds = estimatedTravelMilliseconds;
			IsIncumbent = isIncumbent;
			this.strategicTargets = Array.AsReadOnly(
				targets.OrderBy(target => target.StableActorId).ToArray());
			ThreatFacts = threatFacts ?? new StealthTargetThreatFacts(strategicCell,
				Array.Empty<StealthCombatGroupSnapshot>(), Array.Empty<StealthCombatGroupSnapshot>(),
				false, false);
		}
	}

	public sealed class StealthTargetAcquisitionResult
	{
		readonly ReadOnlyCollection<StealthTargetOption> options;

		internal StealthBehaviorHandoff Handoff { get; }
		public CPos ActiveSquadCenter { get; }
		public CPos? IncumbentStrategicCell { get; }
		public StealthTargetAcquisitionDisposition Disposition { get; }
		public IReadOnlyList<StealthTargetOption> Options => options;
		public CPos? MoveCloserStrategicCell { get; }
		public int PrimitiveOperations { get; }
		public int ExpandedCells { get; }
		public bool IsReadyForValueFilter =>
			Disposition == StealthTargetAcquisitionDisposition.ReadyForValueFilter;

		internal StealthTargetAcquisitionResult(StealthBehaviorHandoff handoff,
			CPos activeSquadCenter, CPos? incumbentStrategicCell,
			StealthTargetAcquisitionDisposition disposition,
			IEnumerable<StealthTargetOption> options, CPos? moveCloserStrategicCell,
			int primitiveOperations, int expandedCells)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (!Enum.IsDefined(typeof(StealthTargetAcquisitionDisposition), disposition))
				throw new ArgumentOutOfRangeException(nameof(disposition));
			if (options == null)
				throw new ArgumentNullException(nameof(options));
			if (primitiveOperations < 0 || expandedCells < 0)
				throw new ArgumentOutOfRangeException(nameof(primitiveOperations));

			ActiveSquadCenter = activeSquadCenter;
			IncumbentStrategicCell = incumbentStrategicCell;
			Disposition = disposition;
			this.options = Array.AsReadOnly(options.ToArray());
			MoveCloserStrategicCell = moveCloserStrategicCell;
			PrimitiveOperations = primitiveOperations;
			ExpandedCells = expandedCells;
		}
	}

	/// <summary>Typed immutable boundary between lifecycle Steps 3 and 4A.</summary>
	public sealed class StealthTargetValueFilterHandoff
	{
		readonly ReadOnlyCollection<StealthTargetOption> options;

		internal StealthBehaviorHandoff Handoff { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public IReadOnlyList<StealthTargetOption> Options => options;

		internal StealthTargetValueFilterHandoff(StealthBehaviorHandoff handoff,
			IEnumerable<StealthTargetOption> options)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetValueFilter)
				throw new ArgumentException("The handoff must belong to TargetValueFilter.", nameof(handoff));
			if (options == null)
				throw new ArgumentNullException(nameof(options));

			this.options = Array.AsReadOnly(options.ToArray());
		}
	}
}
