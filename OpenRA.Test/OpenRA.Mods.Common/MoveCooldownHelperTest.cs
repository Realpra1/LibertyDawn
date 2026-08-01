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
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class MoveCooldownHelperTest
	{
		[Test]
		public void BlockedMoveCompletesWhenRetriesAreDisabled()
		{
			var helper = new MoveCooldownHelper((min, max) => min);
			helper.NotifyMoveQueued();

			Assert.That(helper.Tick(false, MoveResult.CompleteDestinationBlocked, false), Is.True);
		}

		[Test]
		public void BlockedMoveRetriesOnlyAfterCooldown()
		{
			var helper = new MoveCooldownHelper((min, max) => min)
			{
				RetryIfDestinationBlocked = true,
				Cooldown = (2, 3)
			};
			helper.NotifyMoveQueued();

			Assert.That(helper.Tick(false, MoveResult.CompleteDestinationBlocked, false), Is.False);
			Assert.That(helper.Tick(false, MoveResult.CompleteDestinationBlocked, false), Is.False);
			Assert.That(helper.Tick(false, MoveResult.CompleteDestinationBlocked, false), Is.False);
			Assert.That(helper.Tick(false, MoveResult.CompleteDestinationBlocked, false), Is.Null);
		}

		[Test]
		public void BlockingActorSkipsAnActiveCooldown()
		{
			var helper = new MoveCooldownHelper((min, max) => min)
			{
				RetryIfDestinationBlocked = true,
				Cooldown = (20, 21)
			};
			helper.NotifyMoveQueued();
			Assert.That(helper.Tick(false, MoveResult.CompleteDestinationBlocked, false), Is.False);

			Assert.That(helper.Tick(false, MoveResult.CompleteDestinationBlocked, true), Is.Null);
		}

		[TestCase(MoveResult.CompleteCanceled)]
		[TestCase(MoveResult.CompleteDestinationReached)]
		public void CompletedMovementResumesParentImmediately(MoveResult result)
		{
			var helper = new MoveCooldownHelper((min, max) => min);
			helper.NotifyMoveQueued();

			Assert.That(helper.Tick(false, result, false), Is.Null);
		}

		[Test]
		public void HiddenTargetStopsAfterQueuedMovementCompletes()
		{
			var helper = new MoveCooldownHelper((min, max) => min);
			helper.NotifyMoveQueued();

			Assert.That(helper.Tick(true, MoveResult.CompleteDestinationReached, false), Is.True);
		}
	}
}
