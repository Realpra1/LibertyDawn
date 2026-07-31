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

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Serializable snapshot of one adaptive-risk history checkpoint.
	/// Values are basis points so replay and save/load behavior never depends on floating-point rounding.
	/// </summary>
	public readonly struct AdaptiveAirRiskCheckpoint
	{
		public readonly int Tick;
		public readonly int BonusBasisPoints;

		public AdaptiveAirRiskCheckpoint(int tick, int bonusBasisPoints)
		{
			Tick = tick;
			BonusBasisPoints = bonusBasisPoints;
		}
	}

	/// <summary>Serializable state for <see cref="AdaptiveAirRiskController"/>.</summary>
	public sealed class AdaptiveAirRiskState
	{
		public int BonusBasisPoints { get; }
		public int PendingKillBonusBasisPoints { get; }
		public AdaptiveAirRiskCheckpoint[] History { get; }

		public AdaptiveAirRiskState(int bonusBasisPoints, int pendingKillBonusBasisPoints, AdaptiveAirRiskCheckpoint[] history)
		{
			BonusBasisPoints = bonusBasisPoints;
			PendingKillBonusBasisPoints = pendingKillBonusBasisPoints;
			History = history ?? Array.Empty<AdaptiveAirRiskCheckpoint>();
		}
	}

	/// <summary>
	/// World-independent adaptive aggression controller. The authored behavior is always the floor
	/// (bonus zero). Readiness and successful kills can grow the bonus without an ordinary gameplay
	/// ceiling; only a deliberately high safety clamp protects integer arithmetic. Enemy losses roll
	/// behavior back toward a historical checkpoint and apply an additional fixed decrement, allowing
	/// repeated losses in one window to continue reducing aggression to the authored floor.
	/// </summary>
	public sealed class AdaptiveAirRiskController
	{
		public const int BasisPointsPerMultiplier = 10000;
		public const int DefaultSafetyClampBasisPoints = 100000000;

		readonly int historyCapacity;
		readonly int safetyClampBasisPoints;
		readonly List<AdaptiveAirRiskCheckpoint> history = new List<AdaptiveAirRiskCheckpoint>();
		int pendingKillBonusBasisPoints;

		public int BonusBasisPoints { get; private set; }
		public int MultiplierBasisPoints => BasisPointsPerMultiplier + BonusBasisPoints;
		public decimal Multiplier => (decimal)MultiplierBasisPoints / BasisPointsPerMultiplier;

		public AdaptiveAirRiskController(int historyCapacity = 64, int safetyClampBasisPoints = DefaultSafetyClampBasisPoints)
		{
			if (historyCapacity <= 0)
				throw new ArgumentOutOfRangeException(nameof(historyCapacity));
			if (safetyClampBasisPoints <= 0 || safetyClampBasisPoints > int.MaxValue - BasisPointsPerMultiplier)
				throw new ArgumentOutOfRangeException(nameof(safetyClampBasisPoints));

			this.historyCapacity = historyCapacity;
			this.safetyClampBasisPoints = safetyClampBasisPoints;
		}

		/// <summary>Adds value-weighted kill credit for the next periodic update.</summary>
		public void RecordKill(int victimValue, int bonusBasisPointsPerValue)
		{
			if (victimValue <= 0 || bonusBasisPointsPerValue <= 0)
				return;

			pendingKillBonusBasisPoints = ClampBonus((long)pendingKillBonusBasisPoints +
				(long)victimValue * bonusBasisPointsPerValue);
		}

		/// <summary>
		/// Applies one periodic observation and stores the resulting timestamped checkpoint.
		/// Kill credit is consumed exactly once. A full magazine grows aggression; low force strength
		/// decays it. Both may apply in the same update and are combined deterministically.
		/// </summary>
		public void Update(int tick, int fullAmmoUnitCount, int unitCount, int minimumUnitCount,
			int fullAmmoGrowthBasisPoints, int lowUnitDecayBasisPoints)
		{
			ValidateTick(tick);
			var change = (long)pendingKillBonusBasisPoints;
			pendingKillBonusBasisPoints = 0;

			if (unitCount > 0)
			{
				var readyUnits = Math.Clamp(fullAmmoUnitCount, 0, unitCount);
				change += (long)Math.Max(0, fullAmmoGrowthBasisPoints) * readyUnits / unitCount;
			}

			if (unitCount < minimumUnitCount)
				change -= Math.Max(0, lowUnitDecayBasisPoints);

			BonusBasisPoints = ClampBonus((long)BonusBasisPoints + change);
			AddCheckpoint(tick);
		}

		/// <summary>
		/// Handles an enemy-caused loss. The result is the lower of the historical bonus at or before
		/// the rollback tick and the current bonus minus the configured decrement.
		/// </summary>
		public void RecordEnemyLoss(int tick, int rollbackDurationTicks, int lossDecrementBasisPoints)
		{
			ValidateTick(tick);
			var rollbackTick = (long)tick - Math.Max(0, rollbackDurationTicks);
			var historicalBonus = 0;
			for (var i = history.Count - 1; i >= 0; i--)
			{
				if (history[i].Tick > rollbackTick)
					continue;

				historicalBonus = history[i].BonusBasisPoints;
				break;
			}

			var decremented = ClampBonus((long)BonusBasisPoints - Math.Max(0, lossDecrementBasisPoints));
			BonusBasisPoints = Math.Min(historicalBonus, decremented);
			pendingKillBonusBasisPoints = 0;
		}

		public AdaptiveAirRiskState ExportState()
		{
			return new AdaptiveAirRiskState(BonusBasisPoints, pendingKillBonusBasisPoints, history.ToArray());
		}

		public void ImportState(AdaptiveAirRiskState state)
		{
			if (state == null)
				throw new ArgumentNullException(nameof(state));

			BonusBasisPoints = ClampBonus(state.BonusBasisPoints);
			pendingKillBonusBasisPoints = ClampBonus(state.PendingKillBonusBasisPoints);
			history.Clear();
			var start = Math.Max(0, state.History.Length - historyCapacity);
			var previousTick = -1;
			for (var i = start; i < state.History.Length; i++)
			{
				var checkpoint = state.History[i];
				if (checkpoint.Tick < 0 || checkpoint.Tick < previousTick)
					throw new ArgumentException("Adaptive air risk history must use non-negative, nondecreasing ticks.", nameof(state));

				history.Add(new AdaptiveAirRiskCheckpoint(checkpoint.Tick, ClampBonus(checkpoint.BonusBasisPoints)));
				previousTick = checkpoint.Tick;
			}
		}

		void AddCheckpoint(int tick)
		{
			if (history.Count > 0 && history[history.Count - 1].Tick == tick)
				history[history.Count - 1] = new AdaptiveAirRiskCheckpoint(tick, BonusBasisPoints);
			else
				history.Add(new AdaptiveAirRiskCheckpoint(tick, BonusBasisPoints));

			if (history.Count > historyCapacity)
				history.RemoveRange(0, history.Count - historyCapacity);
		}

		void ValidateTick(int tick)
		{
			if (tick < 0 || (history.Count > 0 && tick < history[history.Count - 1].Tick))
				throw new ArgumentOutOfRangeException(nameof(tick), "Ticks must be non-negative and nondecreasing.");
		}

		int ClampBonus(long bonus)
		{
			return (int)Math.Clamp(bonus, 0, safetyClampBasisPoints);
		}
	}
}
