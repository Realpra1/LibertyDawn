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

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class TechnologyCounterLogicTest
	{
		[Test]
		public void DominantBranchUsesCountThenStableNameTieBreak()
		{
			Assert.That(TechnologyCounterLogic.DominantBranch(new Dictionary<string, int>
			{
				["recon"] = 2,
				["economy"] = 2,
				["covert"] = 1
			}), Is.EqualTo("economy"));
		}

		[Test]
		public void DominantBranchIgnoresAbsentBranches()
		{
			Assert.That(TechnologyCounterLogic.DominantBranch(new Dictionary<string, int>
			{
				["recon"] = 0,
				["economy"] = 0
			}), Is.Null);
		}

		[TestCase(3999, 1000, 3000, false)]
		[TestCase(4000, 1000, 3000, true)]
		[TestCase(4001, 1000, 3000, true)]
		public void SwitchDelayUsesInclusiveBoundary(int tick, int observed, int delay, bool expected)
		{
			Assert.That(TechnologyCounterLogic.DelayElapsed(tick, observed, delay), Is.EqualTo(expected));
		}
	}
}
