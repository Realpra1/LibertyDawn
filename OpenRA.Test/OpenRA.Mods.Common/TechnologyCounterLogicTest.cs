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
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class TechnologyCounterLogicTest
	{
		static readonly IReadOnlyDictionary<string, string> Counters = new Dictionary<string, string>
		{
			{ "covert", "recon" },
			{ "recon", "economy" },
			{ "economy", "covert" }
		};

		[Test]
		public void DominantBranchUsesProgressThenStableNameTieBreak()
		{
			Assert.That(TechnologyCounterLogic.DominantBranch(new Dictionary<string, int>
			{
				{ "recon", 2 }, { "economy", 2 }, { "covert", 1 }
			}), Is.EqualTo("economy"));
			Assert.That(TechnologyCounterLogic.DominantBranch(new Dictionary<string, int>
			{
				{ "recon", 0 }, { "economy", 0 }
			}), Is.Null);
		}

		[TestCase("economy", "covert")]
		[TestCase("covert", "recon")]
		[TestCase("recon", "economy")]
		public void MatureObservationSelectsConfiguredCounter(string observed, string expected)
		{
			Assert.That(TechnologyCounterLogic.DesiredBranch("economy", "economy", observed,
				4000, 1000, 3000, Counters), Is.EqualTo(expected));
		}

		[Test]
		public void ImmatureOrMissingObservationKeepsCurrentPlan()
		{
			Assert.That(TechnologyCounterLogic.DesiredBranch("recon", "economy", "economy",
				3999, 1000, 3000, Counters), Is.EqualTo("recon"));
			Assert.That(TechnologyCounterLogic.DesiredBranch("recon", "economy", null,
				9999, 1000, 3000, Counters), Is.EqualTo("recon"));
			Assert.That(TechnologyCounterLogic.DesiredBranch(null, "economy", null,
				0, -1, 3000, Counters), Is.EqualTo("economy"));
		}

		[Test]
		public void SwitchDelayCannotMatureBeforeAnObservation()
		{
			Assert.That(TechnologyCounterLogic.DelayElapsed(10000, -1, 0), Is.False);
			Assert.That(TechnologyCounterLogic.DelayElapsed(3999, 1000, 3000), Is.False);
			Assert.That(TechnologyCounterLogic.DelayElapsed(4000, 1000, 3000), Is.True);
		}

		[Test]
		public void TransitionLeavesStrongestWrongBranchFirst()
		{
			Assert.That(TechnologyCounterLogic.BranchToDowngrade(new Dictionary<string, int>
			{
				{ "economy", 3 }, { "covert", 1 }, { "recon", 0 }
			}, "recon"), Is.EqualTo("economy"));
			Assert.That(TechnologyCounterLogic.BranchToDowngrade(new Dictionary<string, int>
			{
				{ "economy", 0 }, { "covert", 0 }, { "recon", 2 }
			}, "recon"), Is.Null);
		}

		[Test]
		public void NextUpgradeReturnsFirstMissingTier()
		{
			var upgrades = new[] { "recon1", "recon2", "recon3" };
			Assert.That(TechnologyCounterLogic.NextUpgrade(upgrades,
				new HashSet<string> { "recon1", "recon3" }), Is.EqualTo("recon2"));
			Assert.That(TechnologyCounterLogic.NextUpgrade(upgrades,
				new HashSet<string>(upgrades)), Is.Null);
		}

		[Test]
		public void AllBranchUpgradePrefersDesiredThenUsesStableBranchOrder()
		{
			var upgrades = new Dictionary<string, string[]>
			{
				{ "recon", new[] { "recon1", "recon2" } },
				{ "economy", new[] { "economy1", "economy2" } },
				{ "covert", new[] { "covert1", "covert2" } }
			};

			Assert.That(TechnologyCounterLogic.NextUpgradeAcrossBranches(upgrades, "recon",
				new HashSet<string> { "recon1", "economy1" }), Is.EqualTo("recon2"));
			Assert.That(TechnologyCounterLogic.NextUpgradeAcrossBranches(upgrades, "recon",
				new HashSet<string> { "recon1", "recon2", "covert1", "covert2", "economy1" }), Is.EqualTo("economy2"));
			Assert.That(TechnologyCounterLogic.NextUpgradeAcrossBranches(upgrades, "unknown",
				new HashSet<string>()), Is.EqualTo("covert1"));
			Assert.That(TechnologyCounterLogic.NextUpgradeAcrossBranches(upgrades, "recon",
				new HashSet<string>(upgrades.SelectMany(kv => kv.Value))), Is.Null);
		}
	}
}
