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
	public class AlliedSupplyProductionPolicyTest
	{
		[TestCase(3, 0, 3)]
		[TestCase(3, 2, 1)]
		[TestCase(3, 3, 0)]
		[TestCase(3, 5, 0)]
		public void GlobalQuotaIsSharedAcrossAllAllies(int queues, int requested, int expected)
		{
			Assert.That(AlliedSupplyProductionPolicy.RemainingGlobalQuota(queues, requested), Is.EqualTo(expected));
		}

		[Test]
		public void QuotaIsOneRequestPerAvailableQueuePerWindow()
		{
			var state = NewState();
			var observation = new AlliedSupplyProductionObservation(true, true, false, 3);

			var first = AlliedSupplyProductionPolicy.Evaluate(state, observation, 100, 7500);
			var repeated = AlliedSupplyProductionPolicy.Evaluate(state, observation, 200, 7500);

			Assert.That(first.Action, Is.EqualTo(AlliedSupplyProductionAction.Request));
			Assert.That(first.RequestCount, Is.EqualTo(3));
			Assert.That(repeated.Action, Is.EqualTo(AlliedSupplyProductionAction.None));
		}

		[Test]
		public void NewQueueAddsOnlyItsAdditionalQuota()
		{
			var state = NewState();
			AlliedSupplyProductionPolicy.Evaluate(state,
				new AlliedSupplyProductionObservation(true, true, false, 1), 100, 7500);

			var decision = AlliedSupplyProductionPolicy.Evaluate(state,
				new AlliedSupplyProductionObservation(true, true, false, 3), 200, 7500);

			Assert.That(decision.RequestCount, Is.EqualTo(2));
		}

		[Test]
		public void QuotaResetsAtIntervalBoundary()
		{
			var state = NewState();
			var observation = new AlliedSupplyProductionObservation(true, true, false, 2);
			AlliedSupplyProductionPolicy.Evaluate(state, observation, 100, 7500);

			var decision = AlliedSupplyProductionPolicy.Evaluate(state, observation, 7600, 7500);

			Assert.That(decision.RequestCount, Is.EqualTo(2));
		}

		[Test]
		public void RecoveryCancelsAndStartsANewEpisode()
		{
			var state = NewState();
			AlliedSupplyProductionPolicy.Evaluate(state,
				new AlliedSupplyProductionObservation(true, true, false, 1), 100, 7500);

			var recovered = AlliedSupplyProductionPolicy.Evaluate(state,
				new AlliedSupplyProductionObservation(false, true, false, 1), 200, 7500);
			var strandedAgain = AlliedSupplyProductionPolicy.Evaluate(state,
				new AlliedSupplyProductionObservation(true, true, false, 1), 300, 7500);

			Assert.That(recovered.Action, Is.EqualTo(AlliedSupplyProductionAction.Cancel));
			Assert.That(strandedAgain.RequestCount, Is.EqualTo(1));
		}

		[Test]
		public void OwnEmergencySuppressesAndCancelsAlliedRequests()
		{
			var decision = AlliedSupplyProductionPolicy.Evaluate(NewState(),
				new AlliedSupplyProductionObservation(true, true, true, 4), 100, 7500);

			Assert.That(decision.Action, Is.EqualTo(AlliedSupplyProductionAction.Cancel));
			Assert.That(decision.RequestCount, Is.Zero);
		}

		[Test]
		public void TerminalAllyStatePermanentlyGivesUpForEpisode()
		{
			var state = NewState();
			var terminal = new AlliedSupplyProductionObservation(true, false, false, 2);
			var first = AlliedSupplyProductionPolicy.Evaluate(state, terminal, 100, 7500);
			var later = AlliedSupplyProductionPolicy.Evaluate(state,
				new AlliedSupplyProductionObservation(true, true, false, 2), 8000, 7500);

			Assert.That(first.Action, Is.EqualTo(AlliedSupplyProductionAction.GiveUp));
			Assert.That(later.Action, Is.EqualTo(AlliedSupplyProductionAction.GiveUp));
			Assert.That(state.GaveUp, Is.True);
		}

		[Test]
		public void NoAvailableQueueDoesNotConsumeQuota()
		{
			var state = NewState();
			var unavailable = AlliedSupplyProductionPolicy.Evaluate(state,
				new AlliedSupplyProductionObservation(true, true, false, 0), 100, 7500);
			var available = AlliedSupplyProductionPolicy.Evaluate(state,
				new AlliedSupplyProductionObservation(true, true, false, 2), 200, 7500);

			Assert.That(unavailable.Action, Is.EqualTo(AlliedSupplyProductionAction.Cancel));
			Assert.That(available.RequestCount, Is.EqualTo(2));
		}

		static AlliedSupplyProductionState NewState()
		{
			return new AlliedSupplyProductionState { WindowStartTick = -1 };
		}
	}
}
