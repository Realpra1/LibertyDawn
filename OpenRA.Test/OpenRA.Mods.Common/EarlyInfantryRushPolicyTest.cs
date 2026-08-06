#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System.Collections.Generic;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class EarlyInfantryRushPolicyTest
	{
		[Test]
		public void ProductionInterleavesAndStopsAtBothCaps()
		{
			Assert.That(EarlyInfantryRushPolicy.NextProduction(0, 20, 0, 4),
				Is.EqualTo(EarlyInfantryProductionType.Chemical));
			Assert.That(EarlyInfantryRushPolicy.NextProduction(0, 20, 1, 4),
				Is.EqualTo(EarlyInfantryProductionType.Grenadier));
			Assert.That(EarlyInfantryRushPolicy.NextProduction(5, 20, 1, 4),
				Is.EqualTo(EarlyInfantryProductionType.Chemical));
			Assert.That(EarlyInfantryRushPolicy.NextProduction(20, 20, 3, 4),
				Is.EqualTo(EarlyInfantryProductionType.Chemical));
			Assert.That(EarlyInfantryRushPolicy.NextProduction(20, 20, 4, 4),
				Is.EqualTo(EarlyInfantryProductionType.None));
		}

		[TestCase(9, 10, 0, 2, false)]
		[TestCase(10, 10, 0, 2, true)]
		[TestCase(25, 10, 1, 2, true)]
		[TestCase(10, 10, 2, 2, false)]
		public void GroupsLaunchOnlyWholeAndWithinLifetimeCap(int pending, int size, int launched, int maximum, bool expected)
		{
			Assert.That(EarlyInfantryRushPolicy.CanLaunchGroup(pending, size, launched, maximum), Is.EqualTo(expected));
		}

		[Test]
		public void FormationSelectionIsDeterministicDistinctAndSpaced()
		{
			var candidates = new List<CPos>
			{
				new CPos(0, 0), new CPos(1, 0), new CPos(2, 0), new CPos(3, 0),
				new CPos(0, 2), new CPos(2, 2)
			};
			var selected = EarlyInfantryRushPolicy.SelectSpacedCells(candidates, 4, 2);

			Assert.That(selected, Is.EqualTo(new[]
			{
				new CPos(0, 0), new CPos(2, 0), new CPos(0, 2), new CPos(2, 2)
			}));
		}

		[Test]
		public void HoldEndsExactlyAtConfiguredTick()
		{
			Assert.That(EarlyInfantryRushPolicy.IsHolding(174, 175), Is.True);
			Assert.That(EarlyInfantryRushPolicy.IsHolding(175, 175), Is.False);
		}

		[Test]
		public void TargetScorePrefersPriorityValueDistanceAndIncumbent()
		{
			var baseline = EarlyInfantryRushPolicy.TargetScore(5000, 500, 20L * 20 * 1024 * 1024, false);
			Assert.That(EarlyInfantryRushPolicy.TargetScore(6000, 1, 100L * 100 * 1024 * 1024, false), Is.GreaterThan(baseline));
			Assert.That(EarlyInfantryRushPolicy.TargetScore(5000, 1000, 20L * 20 * 1024 * 1024, false), Is.GreaterThan(baseline));
			Assert.That(EarlyInfantryRushPolicy.TargetScore(5000, 500, 10L * 10 * 1024 * 1024, false), Is.GreaterThan(baseline));
			Assert.That(EarlyInfantryRushPolicy.TargetScore(5000, 500, 20L * 20 * 1024 * 1024, true), Is.GreaterThan(baseline));
		}
	}
}
