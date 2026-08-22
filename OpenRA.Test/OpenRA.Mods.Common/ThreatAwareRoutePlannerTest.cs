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

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class ThreatAwareRoutePlannerTest
	{
		[TestCase(TestName = "Coarse A* detours around expensive anti-air cells")]
		public void RouteAvoidsThreat()
		{
			var danger = new float[15];
			danger[2] = 10;
			var route = ThreatAwareRoutePlanner.FindRoute(danger, 5, 3, 0, 0, 4, 0, 100);
			Assert.That(route, Is.Not.Null);
			Assert.That(route, Does.Not.Contain(new CPos(2, 0)));
		}

		[TestCase(TestName = "Coarse A* crosses finite danger when no detour exists")]
		public void RouteCanAcceptDanger()
		{
			var danger = new[] { 0f, 10f, 0f };
			var route = ThreatAwareRoutePlanner.FindRoute(danger, 3, 1, 0, 0, 2, 0, 100);
			Assert.That(route, Does.Contain(new CPos(1, 0)));
		}

		[Test]
		public void SoftResourceCostPrefersCleanDetourButDoesNotBlockOnlyRoute()
		{
			var danger = new float[6];
			danger[1] = StealthTankSquadPolicy.SoftResourceRouteCost;
			var detour = ThreatAwareRoutePlanner.FindRoute(danger, 3, 2, 0, 0, 2, 0, 100);
			Assert.That(detour, Does.Not.Contain(new CPos(1, 0)));

			var required = ThreatAwareRoutePlanner.FindRoute(
				new[] { 0f, StealthTankSquadPolicy.SoftResourceRouteCost, 0f },
				3, 1, 0, 0, 2, 0, 100);
			Assert.That(required, Does.Contain(new CPos(1, 0)));
		}

		[Test]
		public void NearestSafeRouteLeavesAaCoverAtLowestCost()
		{
			var danger = new[]
			{
				0f, 20f, 0f,
				0f, 10f, 5f,
				0f, 20f, 0f,
			};

			var route = ThreatAwareRoutePlanner.FindNearestSafeRoute(danger, 3, 3, 1, 1, 100);
			Assert.That(route, Is.EqualTo(new[] { new CPos(0, 1) }));
		}

		[Test]
		public void NearestSafeRouteStaysPutWhenAlreadySafe()
		{
			var route = ThreatAwareRoutePlanner.FindNearestSafeRoute(new[] { 0f, 10f }, 2, 1, 0, 0, 100);
			Assert.That(route, Is.Empty);
		}

		[Test]
		public void SmoothRouteCollapsesSafeGridPathToStraightFlight()
		{
			var danger = new float[25];
			var route = new[] { new CPos(1, 0), new CPos(2, 0), new CPos(2, 1), new CPos(2, 2), new CPos(3, 2), new CPos(4, 2) };

			var smoothed = ThreatAwareRoutePlanner.SmoothRoute(danger, 5, 5, 0, 0, route);

			Assert.That(smoothed, Is.EqualTo(new[] { new CPos(4, 2) }));
		}

		[Test]
		public void SmoothRouteKeepsDetourAroundDanger()
		{
			var danger = new float[25];
			danger[2 * 5 + 2] = 1;
			var route = new[] { new CPos(1, 1), new CPos(2, 1), new CPos(3, 1), new CPos(4, 2) };

			var smoothed = ThreatAwareRoutePlanner.SmoothRoute(danger, 5, 5, 0, 2, route);

			Assert.That(smoothed.Count, Is.GreaterThan(1));
			Assert.That(smoothed[smoothed.Count - 1], Is.EqualTo(new CPos(4, 2)));
		}
	}
}
