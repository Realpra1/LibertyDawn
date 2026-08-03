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

using System.Collections.Generic;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class SpecialOrderTargetingTest
	{
		[TestCase(PlayerRelationship.Ally, null, true)]
		[TestCase(PlayerRelationship.Enemy, null, false)]
		public void EmptyTypeFilterUsesConfiguredRelationship(PlayerRelationship relationship,
			string deliveryType, bool expected)
		{
			Assert.That(SpecialOrderTargeting.AcceptsDelivery(relationship, deliveryType,
				new HashSet<string>(), PlayerRelationship.Ally), Is.EqualTo(expected));
		}

		[TestCase("supply", true)]
		[TestCase("other", false)]
		[TestCase(null, false)]
		public void RequiredDeliveryTypeMustMatch(string deliveryType, bool expected)
		{
			Assert.That(SpecialOrderTargeting.AcceptsDelivery(PlayerRelationship.Ally, deliveryType,
				new HashSet<string> { "supply" }, PlayerRelationship.Ally), Is.EqualTo(expected));
		}
	}
}
