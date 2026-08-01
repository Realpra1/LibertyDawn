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

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class HarvesterRaidLogicTest
	{
		[TestCase(0, 5, 0)]
		[TestCase(1, 5, 1)]
		[TestCase(19, 5, 1)]
		[TestCase(20, 5, 1)]
		[TestCase(21, 5, 2)]
		[TestCase(100, 5, 5)]
		[TestCase(10, 0, 0)]
		public void RaidLimitIsCeilingSafeAndBounded(int harvesters, int percent, int expected)
		{
			Assert.That(HarvesterRaidLogic.RaidLimit(harvesters, percent), Is.EqualTo(expected));
		}

		[TestCase(12, 2, 4, 3, 1)]
		[TestCase(20, 2, 4, 3, 3)]
		[TestCase(8, 2, 4, 3, 0)]
		[TestCase(20, 2, 0, 3, 0)]
		public void RefineryProxyReturnsOnlyTheBoundedShortfall(
			int harvesters, int refineries, int perRefinery, int maximumAdditional, int expected)
		{
			Assert.That(HarvesterRaidLogic.AdditionalRefineries(
				harvesters, refineries, perRefinery, maximumAdditional), Is.EqualTo(expected));
		}
	}
}
