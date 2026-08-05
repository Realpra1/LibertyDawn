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

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class ResourceLayerCacheTest
	{
		[TestCase(false, false, false)]
		[TestCase(false, true, false)]
		[TestCase(true, false, true)]
		[TestCase(true, true, false)]
		public void ModifierCacheRebuildsOnlyWhenUninitializedOrInvalidated(
			bool initialized, bool dirty, bool expected)
		{
			Assert.That(ResourceLayer.IsModifierCacheFresh(initialized, dirty), Is.EqualTo(expected));
		}

		[TestCase(1, 2, false, "RedTiberium", "RedTiberium", 20, 20, "Explode", false)]
		[TestCase(1, 1, false, "RedTiberium", "BlueTiberium", 20, 20, "Explode", false)]
		[TestCase(1, 1, false, "RedTiberium", "RedTiberium", 5, 20, null, true)]
		[TestCase(1, 1, true, "RedTiberium", "RedTiberium", 20, 20, "Explode", true)]
		[TestCase(1, 1, true, "RedTiberium", "RedTiberium", 19, 20, "Explode", false)]
		[TestCase(1, 1, true, "RedTiberium", "RedTiberium", 20, 20, null, false)]
		[TestCase(1, 1, true, "RedTiberium", "RedTiberium", 20, 20, "Nothing", false)]
		public void DelayedExplosionsRunOnlyForTheirCurrentResourceAndValidTrigger(
			long expectedToken, long activeToken, bool isInstability, string scheduledResourceType,
			string currentResourceType, int currentDensity, int maxDensity, string maxStageEvolvesTo, bool expected)
		{
			Assert.That(ResourceLayer.CanRunDelayedExplosion(expectedToken, activeToken, isInstability,
				scheduledResourceType, currentResourceType, currentDensity, maxDensity, maxStageEvolvesTo), Is.EqualTo(expected));
		}

		[TestCase(42, 42, true)]
		[TestCase(41, 42, false)]
		public void SupersededResourceTickChainsCannotRunTwice(long expectedToken, long activeToken, bool expected)
		{
			Assert.That(ResourceLayer.IsCurrentResourceTick(expectedToken, activeToken), Is.EqualTo(expected));
		}

		[Test]
		public void ResonatorSpreadModifierPreservesConfiguredGrowthAcceleration()
		{
			Assert.Multiple(() =>
			{
				Assert.That(ResourceLayer.ApplyTimeModifier(563, 100), Is.EqualTo(563));
				Assert.That(ResourceLayer.ApplyTimeModifier(563, 750), Is.EqualTo(75));
				Assert.That(ResourceLayer.ApplyTimeModifier(1125, 100), Is.EqualTo(1125));
				Assert.That(ResourceLayer.ApplyTimeModifier(1125, 750), Is.EqualTo(150));
			});
		}
	}
}
