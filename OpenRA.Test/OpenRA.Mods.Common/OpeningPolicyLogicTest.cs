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
		public void FirstRefineryHoldsOnlyActionableOrCommittedOptionalConstruction()
		{
			Assert.That(OpeningPolicyLogic.HoldOptionalConstructionForFirstRefinery(true, 0, false, true), Is.True,
				"An actionable initial Refinery must win the next idle construction commitment.");
			Assert.That(OpeningPolicyLogic.HoldOptionalConstructionForFirstRefinery(true, 0, true, false), Is.True,
				"A started Refinery must retain priority until it becomes live.");
			Assert.That(OpeningPolicyLogic.HoldOptionalConstructionForFirstRefinery(true, 0, false, false), Is.False,
				"An unavailable or unaffordable goal must not stall independent construction.");
			Assert.That(OpeningPolicyLogic.HoldOptionalConstructionForFirstRefinery(false, 0, true, true), Is.False,
				"The hold is scoped to the protected Refinery goal.");
			Assert.That(OpeningPolicyLogic.HoldOptionalConstructionForFirstRefinery(true, 1, true, true), Is.False,
				"Optional construction must resume as soon as the Refinery is live.");
		}

		[Test]
		public void SecondaryQueueProtectsSiloAndConfiguredDefenseAfterFourWalls()
		{
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 3, 4,
				true, false, true, false, true, false, true, false), Is.EqualTo(SecondaryQueueOpeningChoice.Wall));
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				true, false, true, false, true, false, true, false), Is.EqualTo(SecondaryQueueOpeningChoice.Silo));
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				false, true, false, false, true, false, true, false), Is.EqualTo(SecondaryQueueOpeningChoice.FirstDefense));
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				false, true, false, false, true, true, true, false), Is.EqualTo(SecondaryQueueOpeningChoice.None));
		}

		[Test]
		public void SecondaryQueueKeepsFourWallPhaseActiveDuringFirstRefineryRecovery()
		{
			Assert.That(OpeningPolicyLogic.HoldOptionalConstructionForFirstRefinery(true, 0, true, false), Is.True,
				"The primary construction queue may still be serializing its first Refinery.");
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 0, 4,
				true, false, true, false, true, false, true, false),
				Is.EqualTo(SecondaryQueueOpeningChoice.Wall),
				"The protected secondary queue must keep polling and completing its four-wall prefix independently.");
			Assert.That(OpeningPolicyLogic.KeepsEmptySecondaryQueueActive(SecondaryQueueOpeningChoice.Wall), Is.True,
				"A transiently empty wall planner must retain the short active poll delay.");
			Assert.That(OpeningPolicyLogic.KeepsEmptySecondaryQueueActive(SecondaryQueueOpeningChoice.Hold), Is.False,
				"A deliberate post-wall policy hold must not spin at the active poll rate.");
			Assert.That(OpeningPolicyLogic.SecondaryOpeningPollDelay(130, 30, true, 3, 4), Is.EqualTo(30),
				"The wall prefix must override an inactive empty-queue delay.");
			Assert.That(OpeningPolicyLogic.SecondaryOpeningPollDelay(130, 30, true, 4, 4), Is.EqualTo(130),
				"The short poll override must end at the four-wall boundary.");
		}

		[Test]
		public void SecondaryQueueHoldsBoundaryWithoutClaimingImpossibleWork()
		{
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				false, false, true, false, true, false, true, false), Is.EqualTo(SecondaryQueueOpeningChoice.Hold));
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				true, false, false, false, true, false, true, false), Is.EqualTo(SecondaryQueueOpeningChoice.Hold));
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				true, false, false, true, true, false, true, false), Is.EqualTo(SecondaryQueueOpeningChoice.Hold));
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				false, true, false, false, true, false, false, false), Is.EqualTo(SecondaryQueueOpeningChoice.Hold));
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				false, true, false, false, true, false, false, true), Is.EqualTo(SecondaryQueueOpeningChoice.Hold));
		}

		[Test]
		public void SecondaryQueueReleasesPermanentlyUnavailableConfiguredDefense()
		{
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				false, true, false, false, false, false, false, false), Is.EqualTo(SecondaryQueueOpeningChoice.None),
				"A defense goal skipped by the existing opening policy must not strand the secondary queue.");
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				false, true, false, false, true, false, false, false), Is.EqualTo(SecondaryQueueOpeningChoice.Hold),
				"A configured defense that is only temporarily unavailable must retain the boundary.");
		}

		[Test]
		public void FirstQueueSiloCommitmentCompletesSecondaryBoundaryAtItsCapturedTarget()
		{
			var target = OpeningPolicyLogic.ObserveSecondaryQueueSiloTarget(0, 1, true);
			Assert.That(target, Is.EqualTo(2),
				"The four-wall boundary must capture the next live Silo even when another queue owns it.");
			Assert.That(OpeningPolicyLogic.SecondaryQueueSiloTargetCompleted(target, 1), Is.False);
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				false, false, false, true, true, false, false, false), Is.EqualTo(SecondaryQueueOpeningChoice.Hold));

			Assert.That(OpeningPolicyLogic.SecondaryQueueSiloTargetCompleted(target, 0), Is.False,
				"Losing the baseline Silo must not weaken the boundary-time target.");
			Assert.That(OpeningPolicyLogic.SecondaryQueueSiloTargetCompleted(target, 1), Is.False,
				"Replacing only the lost baseline Silo is not completion of the committed target.");
			Assert.That(OpeningPolicyLogic.SecondaryQueueSiloTargetCompleted(target, 2), Is.True);
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				false, true, false, false, true, false, true, false),
				Is.EqualTo(SecondaryQueueOpeningChoice.FirstDefense),
				"The configured defense must release immediately after the first queue completes the Silo target.");
		}

		[Test]
		public void SecondaryBoundaryDoesNotClaimLowCashOrPowerWorkDuringRecovery()
		{
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				true, false, false, false, true, false, true, false), Is.EqualTo(SecondaryQueueOpeningChoice.Hold),
				"An unaffordable or low-power Silo is held without creating a production claim.");
			Assert.That(OpeningPolicyLogic.ChooseSecondaryQueueOpening(true, 4, 4,
				false, true, false, false, true, false, false, false), Is.EqualTo(SecondaryQueueOpeningChoice.Hold),
				"An unaffordable or low-power defense is held without creating a production claim.");
			Assert.That(OpeningPolicyLogic.HoldOptionalConstructionForFirstRefinery(true, 0, true, false), Is.True,
				"The existing committed-refinery recovery hold remains authoritative.");
		}

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
		public void ReservedGoalLetsAnotherFactCommitTheNextOrderedGoal()
		{
			var goal = OpeningPolicyLogic.FirstBuildableGoal(
				Goals, new[] { 0 }, new[] { 1 }, new[] { "guard-tower", "turret" });

			Assert.That(goal, Is.EqualTo(2));
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
