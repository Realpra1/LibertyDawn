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
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// The world-independent half of the adaptive build-weighting feature: turning a per-type
	/// kills/losses ledger into a build-probability share. Kept free of World, Actor and PlayerStatistics
	/// so it can be unit tested. Everything here is deterministic - no randomness (callers supply any
	/// roll explicitly, drawn from World.LocalRandom, never World.SharedRandom) and dictionary
	/// iteration is always ordered by key so results don't depend on insertion order.
	/// </summary>
	public static class AdaptiveWeighting
	{
		public static IReadOnlyList<int> SelectAffordableOffers(
			IReadOnlyList<double> scores, IReadOnlyList<int> costs, int budget)
		{
			if (scores == null || costs == null || scores.Count != costs.Count || scores.Count == 0)
				return Array.Empty<int>();

			var ranked = Enumerable.Range(0, scores.Count)
				.OrderByDescending(i => scores[i]).ThenBy(i => costs[i]).ThenBy(i => i).ToList();
			var selected = new List<int>();
			var remaining = Math.Max(0, budget);
			foreach (var index in ranked)
			{
				var cost = Math.Max(0, costs[index]);
				if (cost > remaining)
					continue;

				selected.Add(index);
				remaining -= cost;
			}

			// When nothing is currently affordable, reserve one queue for the most wanted option so income
			// can accumulate instead of filling a cheaper, lower-value queue first.
			if (selected.Count == 0)
				selected.Add(ranked[0]);

			return selected;
		}

		/// <summary>
		/// Value destroyed vs value lost for a single rollover window. Losses are floored at 1 (not 0)
		/// so a type that has killed something but lost nothing yet still gets a large, finite score -
		/// one lucky early kill should move the needle immediately rather than divide by zero.
		/// </summary>
		public static double MinuteScore(int minuteKillsValue, int minuteLossesValue)
		{
			return (double)minuteKillsValue / Math.Max(minuteLossesValue, 1);
		}

		/// <summary>
		/// Blends the previous decayed score with this window's observation. A window with no kills and
		/// no losses is not evidence of anything - it is left alone rather than dragging the score toward
		/// zero for a type that simply wasn't in play.
		/// </summary>
		public static double DecayScore(double previousScore, int minuteKillsValue, int minuteLossesValue, double minuteWeight)
		{
			if (minuteKillsValue == 0 && minuteLossesValue == 0)
				return previousScore;

			var observed = MinuteScore(minuteKillsValue, minuteLossesValue);
			return (previousScore * (1 - minuteWeight)) + (observed * minuteWeight);
		}

		/// <summary>
		/// How much a type's decayed score should be trusted, ramping from 0 to 1 as combat samples
		/// (kills + losses) accumulate. Below <paramref name="confidenceSamples"/> the authored weight
		/// dominates; this is what stops a single early loss from permanently tanking a type's weight.
		/// </summary>
		public static double Confidence(int samples, int confidenceSamples)
		{
			if (confidenceSamples <= 0)
				return 1;

			return Math.Min(1.0, (double)samples / confidenceSamples);
		}

		/// <summary>
		/// The authored weight nudged toward the decayed score, in proportion to confidence. Can never go
		/// negative: the worst case (score 0, full confidence) multiplies the authored weight by zero, not
		/// below it.
		/// </summary>
		public static double AdaptedWeight(double authoredWeight, double decayedScore, double confidence)
		{
			var multiplier = 1 + ((decayedScore - 1) * confidence);
			if (multiplier < 0)
				multiplier = 0;

			return authoredWeight * multiplier;
		}

		/// <summary>
		/// Normalizes a set of non-negative weights into a probability distribution, then clamps every
		/// entry into [<paramref name="floor"/>, <paramref name="ceiling"/>] and redistributes the
		/// clamped-away mass proportionally among the entries that are still free, iterating until nothing
		/// new gets pinned. This is what makes the floor/ceiling legible as "at least X% / at most Y% of
		/// the time this gets built" rather than a multiplier on an arbitrary, non-percentage weight scale.
		/// Assumes floor * count &lt;= 1 &lt;= ceiling * count (true for the shipped 1%/50% with any
		/// realistic type count); with an infeasible combination this still terminates and returns its
		/// best effort rather than looping forever.
		/// A single entry always gets a share of 1 - there is nothing to clamp against.
		/// </summary>
		public static Dictionary<string, double> ClampedShares(IReadOnlyDictionary<string, double> weights, double floor, double ceiling)
		{
			var result = new Dictionary<string, double>();
			if (weights == null || weights.Count == 0)
				return result;

			var keys = weights.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

			if (keys.Count == 1)
			{
				result[keys[0]] = 1.0;
				return result;
			}

			var totalWeight = keys.Sum(k => Math.Max(weights[k], 0));
			foreach (var k in keys)
				result[k] = totalWeight > 0 ? Math.Max(weights[k], 0) / totalWeight : 1.0 / keys.Count;

			var pinned = new HashSet<string>();
			for (var iteration = 0; iteration < keys.Count + 2; iteration++)
			{
				var changed = false;
				foreach (var k in keys)
				{
					if (pinned.Contains(k))
						continue;

					if (result[k] < floor)
					{
						result[k] = floor;
						pinned.Add(k);
						changed = true;
					}
					else if (result[k] > ceiling)
					{
						result[k] = ceiling;
						pinned.Add(k);
						changed = true;
					}
				}

				if (!changed)
					break;

				var freeKeys = keys.Where(k => !pinned.Contains(k)).ToList();
				if (freeKeys.Count == 0)
					break;

				var pinnedSum = keys.Where(pinned.Contains).Sum(k => result[k]);
				var freeSum = freeKeys.Sum(k => result[k]);
				var target = 1.0 - pinnedSum;

				if (freeSum > 0)
					foreach (var k in freeKeys)
						result[k] = result[k] / freeSum * target;
				else
					foreach (var k in freeKeys)
						result[k] = target / freeKeys.Count;
			}

			return result;
		}

		/// <summary>
		/// Picks a key from a probability distribution given a roll in [0, 1). Ties in the cumulative walk
		/// always resolve to the same key for the same inputs (keys are visited in a fixed, sorted order),
		/// so this is safe to unit test and safe to call from bot code seeded by World.LocalRandom.
		/// </summary>
		public static string WeightedPick(IReadOnlyDictionary<string, double> shares, float roll)
		{
			if (shares == null || shares.Count == 0)
				return null;

			var keys = shares.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
			var total = keys.Sum(k => shares[k]);
			if (total <= 0)
				return keys[0];

			var target = Math.Clamp(roll, 0f, 0.999999f) * total;
			var cumulative = 0.0;
			foreach (var k in keys)
			{
				cumulative += shares[k];
				if (target < cumulative)
					return k;
			}

			return keys[keys.Count - 1];
		}

		/// <summary>
		/// Whether the economy gate should override the combat/economy split and force an economy pick
		/// this tick: either spend is currently outpacing income, or there aren't enough live harvesters
		/// to justify skipping one. Player-level only - no attempt to rank one harvester against another.
		/// </summary>
		public static bool ShouldForceEconomy(int incomeThisWindow, int spendThisWindow, int liveHarvesterCount, int harvesterFloor)
		{
			if (liveHarvesterCount < harvesterFloor)
				return true;

			return spendThisWindow > incomeThisWindow;
		}

		/// <summary>Whether MCV production should be force-prioritized: too few deployed construction yards.</summary>
		public static bool ShouldForceMcv(int deployedConstructionYards, int threshold)
		{
			return deployedConstructionYards < threshold;
		}
	}
}
