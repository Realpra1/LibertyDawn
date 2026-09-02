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
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class AircraftHuskSpawnEligibilityTest
	{
		[Test]
		public void EmptyCellAndOnlyDeadActorsHaveNoLiveBlocker()
		{
			Assert.That(HuskSpawnCellEligibility.HasLiveBlocker(Array.Empty<bool>()), Is.False);
			Assert.That(HuskSpawnCellEligibility.HasLiveBlocker(new[] { true }), Is.False);
			Assert.That(HuskSpawnCellEligibility.HasLiveBlocker(new[] { true, true, true }), Is.False);
		}

		[TestCase(false, true, true)]
		[TestCase(true, false, true)]
		[TestCase(true, true, false)]
		[TestCase(false, false, true)]
		public void AnyLiveActorBlocksRegardlessOfEnumerationOrder(bool firstDead, bool secondDead, bool thirdDead)
		{
			Assert.That(HuskSpawnCellEligibility.HasLiveBlocker(new[] { firstDead, secondDead, thirdDead }), Is.True);
		}
	}
}
