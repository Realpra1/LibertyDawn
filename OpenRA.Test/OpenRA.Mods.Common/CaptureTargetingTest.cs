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

using System.Collections.Generic;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class CaptureTargetingTest
	{
		[Test]
		public void RecoveredActorMakesHuskEconomicallyValuable()
		{
			Assert.That(CaptureTargeting.EconomicValue(0, 1500), Is.EqualTo(1500));
			Assert.That(CaptureTargeting.EconomicValue(2000, 1500), Is.EqualTo(2000));
		}

		[Test]
		public void SelectsValueThenBuildingThenDistanceAndDoesNotDuplicate()
		{
			var values = new[] { 1000, 1500, 1500, 1500 };
			var buildings = new[] { true, false, true, true };
			var distances = new long[] { 1, 1, 100, 25 };
			var assigned = new HashSet<int>();

			var first = CaptureTargeting.BestTargetIndex(values, buildings, distances, assigned);
			Assert.That(first, Is.EqualTo(3));
			assigned.Add(first);
			Assert.That(CaptureTargeting.BestTargetIndex(values, buildings, distances, assigned), Is.EqualTo(2));
		}
	}
}
