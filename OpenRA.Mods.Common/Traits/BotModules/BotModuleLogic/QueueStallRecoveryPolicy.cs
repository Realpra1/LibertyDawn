#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System;

namespace OpenRA.Mods.Common.Traits
{
	public enum QueueStallRecoverySelectedFrontState
	{
		Active,
		CompletedAwaitingExit,
		Invalidated
	}

	public enum QueueStallRecoveryEligibility
	{
		Eligible,
		LowPower,
		HarvesterTargetMet,
		MissingCriticalCandidate,
		SufficientFunds,
		InsufficientContention
	}

	public enum QueueStallRecoveryConstructionChoice
	{
		None,
		ConstructionYardEnclosure,
		NeedBasedSilo
	}

	public static class QueueStallRecoveryPolicy
	{
		public static QueueStallRecoveryEligibility ClassifyEconomyObservation(bool normalPower,
			int liveHarvesters, int harvesterTarget, bool hasCriticalEconomyCandidate,
			bool cashConstrained, int competingFronts)
		{
			if (!normalPower)
				return QueueStallRecoveryEligibility.LowPower;

			if (liveHarvesters >= Math.Max(1, harvesterTarget))
				return QueueStallRecoveryEligibility.HarvesterTargetMet;

			if (!hasCriticalEconomyCandidate)
				return QueueStallRecoveryEligibility.MissingCriticalCandidate;
			if (!cashConstrained)
				return QueueStallRecoveryEligibility.SufficientFunds;

			return competingFronts >= 2 ? QueueStallRecoveryEligibility.Eligible :
				QueueStallRecoveryEligibility.InsufficientContention;
		}

		public static int UpdateNoProgressEvidence(int evidenceTicks, bool eligible,
			bool madePaidProgress, int elapsedTicks)
		{
			if (!eligible || madePaidProgress)
				return 0;

			return (int)Math.Min(int.MaxValue, (long)Math.Max(0, evidenceTicks) + Math.Max(1, elapsedTicks));
		}

		public static bool ShouldRecoverEconomy(int liveHarvesters, int harvesterTarget,
			bool hasCriticalEconomyCandidate, bool cashConstrained, int competingFronts,
			int evidenceTicks, int activationTicks)
		{
			return liveHarvesters < Math.Max(1, harvesterTarget) && hasCriticalEconomyCandidate && cashConstrained &&
				competingFronts >= 2 && evidenceTicks >= Math.Max(1, activationTicks);
		}

		public static bool HasCriticalEconomyCandidate(bool hasUsableRefinery,
			bool hasHarvesterCandidate, bool hasRefineryCandidate)
		{
			return hasUsableRefinery ? hasHarvesterCandidate : hasRefineryCandidate;
		}

		public static QueueStallRecoverySelectedFrontState ClassifySelectedFront(
			bool producerAvailable, bool selectedItemIsCurrent, bool selectedItemDone)
		{
			if (!producerAvailable || !selectedItemIsCurrent)
				return QueueStallRecoverySelectedFrontState.Invalidated;

			return selectedItemDone ? QueueStallRecoverySelectedFrontState.CompletedAwaitingExit :
				QueueStallRecoverySelectedFrontState.Active;
		}

		public static bool ShouldAwaitSelectedFrontOutcome(QueueStallRecoverySelectedFrontState state,
			bool outcomeActorCompleted)
		{
			return !outcomeActorCompleted && state == QueueStallRecoverySelectedFrontState.CompletedAwaitingExit;
		}

		public static bool ShouldPauseOrdinaryProduction(bool active, bool awaitingSelectedExit)
		{
			return active || awaitingSelectedExit;
		}

		public static QueueStallRecoveryConstructionChoice ChooseProtectedConstruction(
			bool recoveryActive, bool enclosureAvailable, bool needBasedSiloAvailable)
		{
			if (!recoveryActive)
				return QueueStallRecoveryConstructionChoice.None;
			if (enclosureAvailable)
				return QueueStallRecoveryConstructionChoice.ConstructionYardEnclosure;

			return needBasedSiloAvailable ? QueueStallRecoveryConstructionChoice.NeedBasedSilo :
				QueueStallRecoveryConstructionChoice.None;
		}

		public static bool ShouldProtectOpeningResearch(bool isNextOpeningResearch,
			bool currentlyBuildable, int availableFunds, int remainingCost)
		{
			return isNextOpeningResearch && currentlyBuildable &&
				Math.Max(0, availableFunds) >= Math.Max(0, remainingCost);
		}
	}
}
