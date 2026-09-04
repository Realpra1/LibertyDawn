#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits.BotModules.Squads;

namespace OpenRA.Test
{
	[TestFixture]
	public class StealthLocalActorCachePolicyTest
	{
		[Test]
		public void CncStealthRadiiCoverTheLocalProvinceAndLongestWeapon()
		{
			var local = StealthLocalActorCachePolicy.LocalRadiusCells(10, 6, 4);

			Assert.That(local, Is.EqualTo(22));
			Assert.That(StealthLocalActorCachePolicy.CoveringRadiusCells(local, 35, 4),
				Is.EqualTo(39));
			Assert.That(StealthLocalActorCachePolicy.MovementBufferCells(local), Is.EqualTo(5));
		}

		[TestCase(1, 25)]
		[TestCase(2, 50)]
		[TestCase(4, 50)]
		public void RefreshIntervalTracksPlanningThrottleAndCapsAtTwoSeconds(
			int planningFactor, int expected)
		{
			Assert.That(StealthLocalActorCachePolicy.RefreshInterval(25, 50, planningFactor),
				Is.EqualTo(expected));
		}

		[Test]
		public void RosterRefreshesForExpiryMissionChangeAndLargeSquadMovement()
		{
			var mission = new CPos(10, 20);
			var center = new CPos(30, 40);
			Assert.That(Refresh(false, 10, 20, mission, mission, center, center), Is.True);
			Assert.That(Refresh(true, 20, 20, mission, mission, center, center), Is.True);
			Assert.That(Refresh(true, 10, 20, mission, new CPos(11, 20), center, center), Is.True);
			Assert.That(Refresh(true, 10, 20, mission, mission, center, new CPos(36, 40)), Is.True);
			Assert.That(Refresh(true, 10, 20, mission, mission, center, new CPos(35, 40)), Is.False);
		}

		static bool Refresh(bool hasRoster, int tick, int refreshTick,
			CPos mission, CPos cachedMission, CPos center, CPos cachedCenter)
		{
			return StealthLocalActorCachePolicy.RequiresRefresh(hasRoster, tick, refreshTick,
				mission, cachedMission, center, cachedCenter, 5);
		}
	}
}
