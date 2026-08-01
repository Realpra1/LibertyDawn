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
	public class OpeningPolicyLogicTest
	{
		[Test]
		public void PicksStrongestCurrentlyAvailableAlternative()
		{
			Assert.That(OpeningPolicyLogic.FirstAvailable(
				new[] { "advanced-power", "basic-power" }, new[] { "basic-power" }), Is.EqualTo("basic-power"));
			Assert.That(OpeningPolicyLogic.FirstAvailable(
				new[] { "advanced-power", "basic-power" }, new[] { "basic-power", "advanced-power" }), Is.EqualTo("advanced-power"));
		}

		[Test]
		public void UnavailableStageUsesConfiguredBoundedRetries()
		{
			Assert.That(OpeningPolicyLogic.ShouldSkipUnavailable(7, 8), Is.False);
			Assert.That(OpeningPolicyLogic.ShouldSkipUnavailable(8, 8), Is.True);
			Assert.That(OpeningPolicyLogic.ShouldSkipUnavailable(1, 0), Is.True);
		}
	}
}
