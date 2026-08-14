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
using OpenRA.Mods.Common.Traits.BotModules;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class AdvancedBotFallbackOwnershipTest
	{
		[TestCase(40, 250)]
		[TestCase(35, 286)]
		[TestCase(20, 500)]
		public void OrdinaryGroundOrderCadenceUsesSimulationSeconds(int timestep, int expectedTicks)
		{
			var info = new SquadManagerBotModuleInfo();
			Assert.That(info.GroundOrderIntervalTicks(timestep), Is.EqualTo(expectedTicks));
		}

		[Test]
		public void SimultaneousSpecialistReleaseRetainsEveryActorByCohesiveOwner()
		{
			var ownership = new AdvancedBotFallbackOwnership();
			ownership.Retain("StealthTankSquadBotModule/stealth-tank", new uint[] { 7, 5 });
			ownership.Retain("CovertHarassmentBotModule", new uint[] { 11, 12, 13 });
			ownership.Retain("EconomyFieldDefenseBotModule", new uint[] { 21, 22 });

			var groups = ownership.Groups.ToArray();
			Assert.That(groups.Select(g => g.Key), Is.Ordered);
			Assert.That(groups.SelectMany(g => g.Value), Is.EquivalentTo(new uint[] { 5, 7, 11, 12, 13, 21, 22 }));
			Assert.That(groups.Single(g => g.Key == "StealthTankSquadBotModule/stealth-tank").Value,
				Is.EqualTo(new uint[] { 5, 7 }));
		}

		[Test]
		public void RepeatedReleaseMergesActorsInsteadOfStrandingEarlierHandoffs()
		{
			var ownership = new AdvancedBotFallbackOwnership();
			ownership.Retain("specialist", new uint[] { 1, 2 });
			ownership.Retain("specialist", new uint[] { 2, 3 });

			Assert.That(ownership.Groups.Single().Value, Is.EqualTo(new uint[] { 1, 2, 3 }));
		}

		[Test]
		public void SaveRestorePreservesStableReleasedOwnership()
		{
			var ownership = new AdvancedBotFallbackOwnership();
			ownership.Retain("b", new uint[] { 8, 6 });
			ownership.Retain("a", new uint[] { 4 });
			var state = ownership.Export();

			var restored = new AdvancedBotFallbackOwnership();
			restored.Import(state.Sources, state.ActorIds);

			Assert.That(restored.Export(), Is.EqualTo(state));
		}

		[Test]
		public void GenericFallbackRequiresConfiguredDirectCombatTypeAndAttackCapability()
		{
			var directCombatTypes = new HashSet<string> { "e1", "mtnk" };

			Assert.That(AdvancedBotFallbackOwnership.IsEligibleForGenericFallback(
				directCombatTypes, "mtnk", true), Is.True);
			Assert.That(AdvancedBotFallbackOwnership.IsEligibleForGenericFallback(
				directCombatTypes, "mtnk", false), Is.False);
		}

		[TestCase("arty")]
		[TestCase("msam")]
		[TestCase("stnk")]
		[TestCase("ctnk")]
		public void ReleasedSpecialistsOutsideAllowlistNeverReceiveGenericFallback(string actorType)
		{
			var directCombatTypes = new HashSet<string> { "e1", "mtnk" };

			Assert.That(AdvancedBotFallbackOwnership.IsEligibleForGenericFallback(
				directCombatTypes, actorType, true), Is.False);
		}
	}
}
