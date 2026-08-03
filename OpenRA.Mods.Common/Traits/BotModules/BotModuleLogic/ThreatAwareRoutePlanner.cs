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
	/// Deterministic bounded-grid routing over graduated threat costs. This is independent of aircraft
	/// target policy so other bot systems, such as transports, can share the same routing behavior.
	/// </summary>
	public static class ThreatAwareRoutePlanner
	{
		/// <summary>Finds the lowest-cost route to a specific coarse-grid cell.</summary>
		public static List<CPos> FindRoute(
			float[] danger, int width, int height, int startX, int startY, int goalX, int goalY, float dangerCost)
		{
			if (danger == null || width <= 0 || height <= 0 || danger.Length != width * height)
				return null;

			var cost = Enumerable.Repeat(float.MaxValue, danger.Length).ToArray();
			var previous = Enumerable.Repeat(-1, danger.Length).ToArray();
			var open = new List<int>();
			var start = startY * width + startX;
			var goal = goalY * width + goalX;
			cost[start] = 0;
			open.Add(start);

			while (open.Count > 0)
			{
				var bestOpen = 0;
				var bestEstimate = float.MaxValue;
				for (var i = 0; i < open.Count; i++)
				{
					var x = open[i] % width;
					var y = open[i] / width;
					var estimate = cost[open[i]] + Math.Abs(goalX - x) + Math.Abs(goalY - y);
					if (estimate < bestEstimate)
					{
						bestEstimate = estimate;
						bestOpen = i;
					}
				}

				var current = open[bestOpen];
				open.RemoveAt(bestOpen);
				if (current == goal)
					break;

				var cx = current % width;
				var cy = current / width;
				for (var d = 0; d < 4; d++)
				{
					var nx = cx + (d == 0 ? -1 : d == 1 ? 1 : 0);
					var ny = cy + (d == 2 ? -1 : d == 3 ? 1 : 0);
					if (nx < 0 || ny < 0 || nx >= width || ny >= height)
						continue;

					var next = ny * width + nx;
					var nextCost = cost[current] + 1 + danger[next] * dangerCost;
					if (nextCost >= cost[next])
						continue;

					cost[next] = nextCost;
					previous[next] = current;
					if (!open.Contains(next))
						open.Add(next);
				}
			}

			if (cost[goal] == float.MaxValue)
				return null;

			var result = new List<CPos>();
			for (var at = goal; at != start && at >= 0; at = previous[at])
				result.Add(new CPos(at % width, at / width));
			result.Reverse();
			return result;
		}

		/// <summary>
		/// Finds the lowest-cost route from a threatened coarse cell to any safe cell. The returned route
		/// excludes the start and is empty when the start is already safe.
		/// </summary>
		public static List<CPos> FindNearestSafeRoute(
			float[] danger, int width, int height, int startX, int startY, float dangerCost)
		{
			if (danger == null || width <= 0 || height <= 0 || danger.Length != width * height ||
				startX < 0 || startY < 0 || startX >= width || startY >= height)
				return null;

			var start = startY * width + startX;
			if (danger[start] <= 0)
				return new List<CPos>();

			var cost = Enumerable.Repeat(float.MaxValue, danger.Length).ToArray();
			var previous = Enumerable.Repeat(-1, danger.Length).ToArray();
			var open = new List<int> { start };
			cost[start] = 0;
			var goal = -1;
			while (open.Count > 0)
			{
				var bestOpen = 0;
				for (var i = 1; i < open.Count; i++)
					if (cost[open[i]] < cost[open[bestOpen]] ||
						(cost[open[i]] == cost[open[bestOpen]] && open[i] < open[bestOpen]))
						bestOpen = i;

				var current = open[bestOpen];
				open.RemoveAt(bestOpen);
				if (danger[current] <= 0)
				{
					goal = current;
					break;
				}

				var cx = current % width;
				var cy = current / width;
				for (var d = 0; d < 4; d++)
				{
					var nx = cx + (d == 0 ? -1 : d == 1 ? 1 : 0);
					var ny = cy + (d == 2 ? -1 : d == 3 ? 1 : 0);
					if (nx < 0 || ny < 0 || nx >= width || ny >= height)
						continue;

					var next = ny * width + nx;
					var nextCost = cost[current] + 1 + Math.Max(0, danger[next]) * Math.Max(0, dangerCost);
					if (nextCost >= cost[next])
						continue;

					cost[next] = nextCost;
					previous[next] = current;
					if (!open.Contains(next))
						open.Add(next);
				}
			}

			if (goal < 0)
				return null;

			var result = new List<CPos>();
			for (var at = goal; at != start && at >= 0; at = previous[at])
				result.Add(new CPos(at % width, at / width));
			result.Reverse();
			return result;
		}

		/// <summary>
		/// Removes unnecessary coarse-grid turns without cutting across threatened cells. The returned
		/// route excludes the start and includes the original destination.
		/// </summary>
		public static List<CPos> SmoothRoute(
			float[] danger, int width, int height, int startX, int startY, IReadOnlyList<CPos> route)
		{
			if (danger == null || width <= 0 || height <= 0 || danger.Length != width * height || route == null)
				return null;

			if (route.Count <= 1)
				return route.ToList();

			var points = new List<CPos>(route.Count + 1) { new CPos(startX, startY) };
			points.AddRange(route);
			var result = new List<CPos>();
			var anchor = 0;
			while (anchor < points.Count - 1)
			{
				var next = points.Count - 1;
				while (next > anchor + 1 && !ClearSegment(danger, width, height, points[anchor], points[next]))
					next--;

				result.Add(points[next]);
				anchor = next;
			}

			return result;
		}

		static bool ClearSegment(float[] danger, int width, int height, CPos from, CPos to)
		{
			var dx = to.X - from.X;
			var dy = to.Y - from.Y;
			var samples = Math.Max(Math.Abs(dx), Math.Abs(dy)) * 2;
			if (samples == 0)
				return true;

			// The endpoints may be occupied by the squad or target. Only crossed cells decide whether
			// a shortcut would cut through danger.
			for (var i = 1; i < samples; i++)
			{
				var x = from.X + (int)Math.Round(dx * i / (double)samples);
				var y = from.Y + (int)Math.Round(dy * i / (double)samples);
				if (x < 0 || y < 0 || x >= width || y >= height || danger[y * width + x] > 0)
					return false;
			}

			return true;
		}
	}
}
