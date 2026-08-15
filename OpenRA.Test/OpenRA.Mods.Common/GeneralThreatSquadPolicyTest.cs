#region Copyright & License Information
/* Copyright 2007-2021 The OpenRA Developers (see AUTHORS) */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class GeneralThreatSquadPolicyTest
	{
		[Test]
		public void SelectsTopUnitTypeByEconomicMassWithStableTieBreak()
		{
			var types = new[]
			{
				new GeneralizedCombatThreatCalculator.GroupTypeCount("z", 2, 500),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("b", 5, 300),
				new GeneralizedCombatThreatCalculator.GroupTypeCount("a", 3, 500)
			};

			Assert.That(GeneralThreatSquadPolicy.SelectTopEconomicMassType(types), Is.EqualTo("a"));
		}

		[Test]
		public void SelectsHighestThreatBeforeEconomicMass()
		{
			var types = new[]
			{
				new GeneralThreatTypeScore("mass", 3, 10000),
				new GeneralThreatTypeScore("counter", 200, 600),
				new GeneralThreatTypeScore("other", 5, 5000)
			};

			Assert.That(GeneralThreatSquadPolicy.SelectHighestThreatType(types), Is.EqualTo("counter"));
		}

		[Test]
		public void ReconsidersOnDeadlineOrInvalidTarget()
		{
			Assert.That(GeneralThreatSquadPolicy.ShouldReconsider(24, 25, true), Is.False);
			Assert.That(GeneralThreatSquadPolicy.ShouldReconsider(25, 25, true), Is.True);
			Assert.That(GeneralThreatSquadPolicy.ShouldReconsider(1, 25, false), Is.True);
		}
	}
}
