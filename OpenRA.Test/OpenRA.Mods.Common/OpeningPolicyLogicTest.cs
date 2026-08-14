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
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class OpeningPolicyLogicTest
	{
		static readonly IReadOnlyList<string[]> Goals = new[]
		{
			new[] { "advanced-power", "power" },
			new[] { "silo" },
			new[] { "guard-tower", "turret" },
			new[] { "barracks" },
			new[] { "helipad" }
		};

		[Test]
		public void AdvancedUnitMilestonesWaitForTheRequiredStructurePrefix()
		{
			Assert.That(OpeningPolicyLogic.RequiredPrefixComplete(new[] { 0, 1 }, 3), Is.False);
			Assert.That(OpeningPolicyLogic.RequiredPrefixComplete(new[] { 0, 1, 3 }, 3), Is.False);
			Assert.That(OpeningPolicyLogic.RequiredPrefixComplete(new[] { 0, 1, 2 }, 3), Is.True);
			Assert.That(OpeningPolicyLogic.RequiredPrefixComplete(new[] { 0, 1, 2, 4 }, 3), Is.True);
			Assert.That(OpeningPolicyLogic.RequiredPrefixComplete(null, 3), Is.False);
		}

		[Test]
		public void PicksPreferredAlternativeFromFirstBuildableGoal()
		{
			var goal = OpeningPolicyLogic.FirstBuildableGoal(
				Goals, new[] { 0, 1 }, Array.Empty<int>(), new[] { "turret", "guard-tower", "barracks" });

			Assert.That(goal, Is.EqualTo(2));
			Assert.That(OpeningPolicyLogic.FirstAvailable(Goals[goal], new[] { "turret", "guard-tower" }),
				Is.EqualTo("guard-tower"));
		}

		[Test]
		public void ReservedGoalKeepsLaterGoalsOrderedWhileOtherQueuesFallBack()
		{
			var goal = OpeningPolicyLogic.FirstBuildableGoal(
				Goals, new[] { 0 }, new[] { 1 }, new[] { "guard-tower", "turret" });

			Assert.That(goal, Is.EqualTo(-1));
		}

		[Test]
		public void UnbuildableCurrentGoalDoesNotSkipToLaterGoal()
		{
			var goal = OpeningPolicyLogic.FirstBuildableGoal(
				Goals, new[] { 0 }, Array.Empty<int>(), new[] { "guard-tower", "turret" });

			Assert.That(goal, Is.EqualTo(-1));
		}

		[Test]
		public void PicksFinalGoalAfterEarlierGoalsComplete()
		{
			var goal = OpeningPolicyLogic.FirstBuildableGoal(
				Goals, new[] { 0, 1, 2, 3 }, Array.Empty<int>(), new[] { "helipad", "power" });

			Assert.That(goal, Is.EqualTo(4));
			Assert.That(OpeningPolicyLogic.FirstAvailable(Goals[goal], new[] { "helipad" }), Is.EqualTo("helipad"));
		}

		[Test]
		public void ReservationRetriesOnlyWhenMissingFromQueueLongEnough()
		{
			Assert.That(OpeningPolicyLogic.RetryReservation(100, 400, 250, true), Is.False);
			Assert.That(OpeningPolicyLogic.RetryReservation(100, 349, 250, false), Is.False);
			Assert.That(OpeningPolicyLogic.RetryReservation(100, 350, 250, false), Is.True);
			Assert.That(OpeningPolicyLogic.RetryReservation(100, 101, 0, false), Is.True);
		}

		[Test]
		public void OptionalGoalIsSkippedOnlyWhenUnavailable()
		{
			var goals = new[] { new[] { "power" }, new[] { "silo" }, new[] { "barracks" } };
			Assert.That(OpeningPolicyLogic.CanSkipUnavailableGoal(
				1, goals, new[] { 0, 2 }, new[] { "silo" }, new[] { "silo" }), Is.False);
			Assert.That(OpeningPolicyLogic.CanSkipUnavailableGoal(
				1, goals, new[] { 0, 2 }, new[] { "silo" }, Array.Empty<string>()), Is.True);
		}
	}
}
