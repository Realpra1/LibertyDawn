#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. You can redistribute
 * it and/or modify it under the terms of the GNU General Public License as
 * published by the Free Software Foundation, either version 3 of the License,
 * or (at your option) any later version.
 */
#endregion

using System.Collections.Generic;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class TiberiumExplosionOptionsTest
	{
		const string Red = "RedTiberiumExplosion";
		const string Blue = "BlueTiberiumExplosion";

		[TestCase(false, false, Red, false)]
		[TestCase(false, false, Blue, false)]
		[TestCase(true, false, Red, true)]
		[TestCase(true, false, Blue, false)]
		[TestCase(false, true, Red, false)]
		[TestCase(false, true, Blue, true)]
		[TestCase(true, true, Red, true)]
		[TestCase(true, true, Blue, true)]
		[TestCase(true, true, "OrdinaryExplosion", false)]
		public void IndependentOptionsSuppressOnlyTheirSemanticImpactType(bool noRed, bool noBlue,
			string impactType, bool expected)
		{
			Assert.That(TiberiumExplosionOptions.IsImpactSuppressed(
				impactType, Red, Blue, noRed, noBlue), Is.EqualTo(expected));
		}

		[Test]
		public void LoadedExplosionRunsWhenAnyPayloadRemainsEnabled()
		{
			var impactTypes = new Dictionary<string, string>
			{
				{ "BlueTiberium", Blue },
				{ "RedTiberium", Red }
			};
			var blue = new Dictionary<string, int> { { "BlueTiberium", 10 } };
			var mixedColors = new Dictionary<string, int> { { "BlueTiberium", 5 }, { "RedTiberium", 5 } };
			var mixedGreen = new Dictionary<string, int> { { "BlueTiberium", 5 }, { "Tiberium", 5 } };

			Assert.That(Explodes.LoadedResourceExplosionIsSuppressed(blue, impactTypes, t => t == Blue), Is.True);
			Assert.That(Explodes.LoadedResourceExplosionIsSuppressed(mixedColors, impactTypes, t => t == Blue), Is.False);
			Assert.That(Explodes.LoadedResourceExplosionIsSuppressed(mixedColors, impactTypes, t => true), Is.True);
			Assert.That(Explodes.LoadedResourceExplosionIsSuppressed(mixedGreen, impactTypes, t => true), Is.False);
		}
	}
}
