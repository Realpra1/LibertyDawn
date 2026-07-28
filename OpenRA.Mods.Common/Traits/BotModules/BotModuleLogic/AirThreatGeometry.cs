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

using System.Collections.Generic;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// The world-independent half of the bot's air threat avoidance: how far a remembered anti-air
	/// position is from a flight path, which retreat point sits furthest from danger, and how a
	/// candidate air target scores. Kept free of World and Actor so it can be unit tested.
	/// Everything here is deterministic - no randomness, no floating point, no iteration over
	/// unordered collections. Ties always resolve to the lowest index.
	/// </summary>
	public static class AirThreatGeometry
	{
		/// <summary>
		/// Squared distance (in world units) from <paramref name="p"/> to the segment
		/// <paramref name="a"/>-<paramref name="b"/>, measured on the ground plane.
		/// Aircraft fly in a straight line, so the segment is the flight path.
		/// </summary>
		public static long DistanceSquaredToSegment(WPos p, WPos a, WPos b)
		{
			long apx = p.X - a.X;
			long apy = p.Y - a.Y;
			long abx = b.X - a.X;
			long aby = b.Y - a.Y;

			var abLengthSquared = abx * abx + aby * aby;
			if (abLengthSquared == 0)
				return apx * apx + apy * apy;

			var dot = apx * abx + apy * aby;
			if (dot <= 0)
				return apx * apx + apy * apy;

			if (dot >= abLengthSquared)
			{
				long bpx = p.X - b.X;
				long bpy = p.Y - b.Y;
				return bpx * bpx + bpy * bpy;
			}

			// Project onto the segment. abx/aby are bounded by the map diagonal and dot by its square,
			// so the products below stay well inside long range.
			var closestX = a.X + (int)(abx * dot / abLengthSquared);
			var closestY = a.Y + (int)(aby * dot / abLengthSquared);

			long dx = p.X - closestX;
			long dy = p.Y - closestY;
			return dx * dx + dy * dy;
		}

		/// <summary>
		/// Number of remembered threats that sit within <paramref name="corridorRadius"/> of the flight
		/// path from <paramref name="from"/> to <paramref name="to"/>. Threats within
		/// <paramref name="destinationExclusion"/> of the destination are skipped: the caller already
		/// prices those in through the target's own anti-air penalty, and counting them twice would
		/// make every defended target look doubly lethal.
		/// </summary>
		public static int CountThreatsNearRoute(IReadOnlyList<WPos> threats, WPos from, WPos to, WDist corridorRadius, WDist destinationExclusion)
		{
			if (threats == null || threats.Count == 0 || corridorRadius.Length <= 0)
				return 0;

			var corridorSquared = (long)corridorRadius.Length * corridorRadius.Length;
			var exclusionSquared = (long)destinationExclusion.Length * destinationExclusion.Length;

			var count = 0;
			for (var i = 0; i < threats.Count; i++)
			{
				var t = threats[i];

				long ex = t.X - to.X;
				long ey = t.Y - to.Y;
				if (ex * ex + ey * ey <= exclusionSquared)
					continue;

				if (DistanceSquaredToSegment(t, from, to) <= corridorSquared)
					count++;
			}

			return count;
		}

		/// <summary>
		/// Squared distance from <paramref name="p"/> to the nearest remembered threat, or
		/// <see cref="long.MaxValue"/> when nothing is remembered.
		/// </summary>
		public static long NearestThreatDistanceSquared(WPos p, IReadOnlyList<WPos> threats)
		{
			var nearest = long.MaxValue;
			if (threats == null)
				return nearest;

			for (var i = 0; i < threats.Count; i++)
			{
				long dx = p.X - threats[i].X;
				long dy = p.Y - threats[i].Y;
				var d = dx * dx + dy * dy;
				if (d < nearest)
					nearest = d;
			}

			return nearest;
		}

		/// <summary>
		/// Index of the candidate that sits furthest from the nearest remembered threat, or -1 when
		/// there are no candidates. With no threats remembered the first candidate wins.
		/// </summary>
		public static int SafestCandidate(IReadOnlyList<WPos> candidates, IReadOnlyList<WPos> threats)
		{
			if (candidates == null || candidates.Count == 0)
				return -1;

			var best = 0;
			var bestDistance = NearestThreatDistanceSquared(candidates[0], threats);
			for (var i = 1; i < candidates.Count; i++)
			{
				var d = NearestThreatDistanceSquared(candidates[i], threats);
				if (d > bestDistance)
				{
					bestDistance = d;
					best = i;
				}
			}

			return best;
		}

		/// <summary>
		/// Whether an air squad should break off, given the anti-air it can see around itself.
		/// Note this scales with squad *size*: a big squad tolerates anti-air that a small one runs from.
		/// That is why capping air squads matters for survival and not just for feel - at the stock
		/// multiplier of 8, a twenty-aircraft squad shrugged off two anti-air actors that would each
		/// individually delete an aircraft, while a squad of five breaks off for the first one.
		/// </summary>
		public static bool ShouldFleeAntiAir(int antiAirCount, int fleeMultiplier, int squadSize)
		{
			return antiAirCount > 0 && antiAirCount * fleeMultiplier > squadSize;
		}

		/// <summary>
		/// <paramref name="v"/> rescaled to <paramref name="length"/> world units on the ground plane,
		/// or zero when it has no direction to preserve. Integer only, so the result is within a unit or
		/// two of the requested length - which is fine for "hop roughly this far".
		/// </summary>
		public static WVec ScaleToLength(WVec v, int length)
		{
			var currentSquared = (long)v.X * v.X + (long)v.Y * v.Y;
			if (currentSquared == 0 || length <= 0)
				return WVec.Zero;

			var current = Exts.ISqrt(currentSquared);
			if (current == 0)
				return WVec.Zero;

			return new WVec((int)(v.X * (long)length / current), (int)(v.Y * (long)length / current), 0);
		}

		/// <summary>
		/// Where a threatened air squad should hop to: <paramref name="hopDistance"/> directly away from
		/// the nearest remembered threat, plus <paramref name="jitter"/>. The jitter is what turns a
		/// straight in-out shuttle into a squad that works its way around the outside of a base, and it
		/// is the whole move when nothing is remembered - a random nearby point is a good enough
		/// "try again from somewhere else".
		/// The caller supplies the jitter so this stays deterministic and testable; bot code must draw it
		/// from World.LocalRandom, never World.SharedRandom.
		/// </summary>
		public static WPos EvadeDestination(WPos from, IReadOnlyList<WPos> threats, WDist hopDistance, WVec jitter)
		{
			var away = WVec.Zero;
			var nearestSquared = long.MaxValue;

			if (threats != null)
			{
				for (var i = 0; i < threats.Count; i++)
				{
					long dx = from.X - threats[i].X;
					long dy = from.Y - threats[i].Y;
					var d = dx * dx + dy * dy;

					// d == 0 means we are exactly on top of it and there is no direction to run in.
					if (d == 0 || d >= nearestSquared)
						continue;

					nearestSquared = d;
					away = new WVec((int)dx, (int)dy, 0);
				}
			}

			return from + ScaleToLength(away, hopDistance.Length) + jitter;
		}

		/// <summary>
		/// Score of a candidate air target. Soft mobile targets are meant to win: the class value plus
		/// the defenceless bonus (awarded when the target itself cannot shoot back at aircraft) has to
		/// outweigh the anti-air cover on top of it and along the way there.
		/// </summary>
		public static int TargetScore(int classValue, bool defenceless, int defencelessBonus,
			int antiAirCount, int antiAirPenalty,
			int routeThreatCount, int routeThreatPenalty,
			int distanceInCells, int distancePenalty)
		{
			var score = classValue;

			if (defenceless)
				score += defencelessBonus;

			score -= antiAirCount * antiAirPenalty;
			score -= routeThreatCount * routeThreatPenalty;
			score -= distanceInCells * distancePenalty;

			return score;
		}
	}
}
