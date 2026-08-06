#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class AirRepairCapacityPolicyTest
	{
		[TestCase(0, 4, 5, 0)]
		[TestCase(1, 4, 5, 1)]
		[TestCase(4, 4, 5, 1)]
		[TestCase(5, 4, 5, 2)]
		[TestCase(20, 4, 5, 5)]
		[TestCase(40, 4, 5, 5)]
		[TestCase(8, 0, 5, 0)]
		public void ScalesRepairBuildingsWithFleetAndAuthoredLimit(
			int aircraft, int aircraftPerBuilding, int limit, int expected)
		{
			Assert.That(AirRepairCapacityPolicy.DesiredBuildings(aircraft, aircraftPerBuilding, limit),
				Is.EqualTo(expected));
		}
	}
}
