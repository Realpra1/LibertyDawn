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

using System.Linq;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public class GameSpeedTest
	{
		static GameSpeed Load(string extra = null)
		{
			var yaml = "Speed:\n\tName: Test\n\tTimestep: 20\n\tOrderLatency: 1";
			if (extra != null)
				yaml += "\n\t" + extra;

			return FieldLoader.Load<GameSpeed>(MiniYaml.FromString(yaml).Single().Value);
		}

		[Test]
		public void MaximumSpeedDefaultsToDisabled()
		{
			Assert.That(Load().RunAtMaximumSpeed, Is.False);
		}

		[Test]
		public void MaximumSpeedRequiresALocalLiveWorld()
		{
			var speed = Load("RunAtMaximumSpeed: true");

			Assert.That(speed.UsesMaximumSpeed(true, false, false), Is.True);
			Assert.That(speed.UsesMaximumSpeed(false, false, false), Is.False);
			Assert.That(speed.UsesMaximumSpeed(true, true, false), Is.False);
			Assert.That(speed.UsesMaximumSpeed(true, false, true), Is.False);
		}
	}
}
