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

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class StealthTargetThreatFilterBehaviorTest
	{
		sealed class CacheProbe : IStealthTargetAcquisitionCache
		{
			readonly StealthTargetAcquisitionCacheSnapshot snapshot;

			public CacheProbe(IEnumerable<StealthStrategicTargetSnapshot> targets,
				IEnumerable<StealthTargetThreatFacts> facts)
			{
				snapshot = new StealthTargetAcquisitionCacheSnapshot(16, 1,
					Enumerable.Repeat(0f, 16), Cells(), 0.1f, targets, facts);
			}

			public StealthTargetAcquisitionCacheSnapshot ReadSnapshot() { return snapshot; }
		}

		sealed class AdapterProbe : IStealthTargetThreatAdapter
		{
			readonly Func<StealthTargetThreatFacts, StealthTargetThreatScore> calculate;

			public List<StealthTargetThreatFacts> Calls { get; } = new List<StealthTargetThreatFacts>();

			public AdapterProbe(Func<StealthTargetThreatFacts, StealthTargetThreatScore> calculate)
			{
				this.calculate = calculate;
			}

			public StealthTargetThreatScore Calculate(StealthTargetThreatFacts facts)
			{
				Calls.Add(facts);
				return calculate(facts);
			}
		}

		static StealthTargetThreatFacts Facts(CPos cell, int enemyCount = 1,
			bool cloaked = false, bool detector = false, bool plannedReveal = true)
		{
			return new StealthTargetThreatFacts(cell,
				new[] { new StealthCombatGroupSnapshot("stnk", 4, 900) },
				new[] { new StealthCombatGroupSnapshot("e3", enemyCount, 300) },
				cloaked, detector, plannedReveal);
		}

		static CPos[] Cells()
		{
			return Enumerable.Range(1, 10).Select(x => new CPos(x, 0)).ToArray();
		}

		static StealthTargetThreatFilterHandoff Handoff(int optionCount,
			out StealthLifecycleController controller, long acquisitionEpoch = 3,
			IReadOnlyList<uint> actorIds = null,
			Func<CPos, StealthTargetThreatFacts> factsFactory = null)
		{
			if (optionCount < 1 || optionCount > 5)
				throw new ArgumentOutOfRangeException(nameof(optionCount));

			var cells = Cells();
			var highCount = optionCount * 2 - 1;
			var targets = cells.Select((cell, index) => new StealthStrategicTargetSnapshot(
				actorIds?[index] ?? (uint)(index + 1), cell,
				index < highCount ? 5000 : 1, index < highCount ? 1100 : 100, 100, 100)).ToArray();
			var createFacts = factsFactory ?? (cell => Facts(cell));
			var facts = cells.Select(createFacts).ToArray();
			controller = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.TargetAcquisition, new OwnershipEpoch(acquisitionEpoch), -1));
			var acquisition = new StealthTargetAcquisitionBehavior(controller.CurrentHandoff,
				new CacheProbe(targets, facts)).Execute(new CPos(0, 0));
			Assert.That(controller.TryAccept(acquisition, out var valueHandoff), Is.True);
			var value = new StealthTargetValueFilterBehavior(valueHandoff).Execute();
			Assert.That(value.Options.Count, Is.EqualTo(optionCount));
			Assert.That(controller.TryAccept(value, out var threatHandoff), Is.True);
			return threatHandoff;
		}

		[Test]
		public void DelegatesEveryOptionAndRanksByLowestThreatThenCrossover()
		{
			var handoff = Handoff(4, out _);
			var scores = new Dictionary<int, StealthTargetThreatScore>
			{
				{ 1, new StealthTargetThreatScore(2, 1) },
				{ 2, new StealthTargetThreatScore(1, 4) },
				{ 3, new StealthTargetThreatScore(1, 2) },
				{ 4, new StealthTargetThreatScore(9, 1) }
			};
			var adapter = new AdapterProbe(facts => scores[facts.StrategicCell.X]);

			var result = new StealthTargetThreatFilterBehavior(
				handoff, adapter).Execute();

			Assert.That(adapter.Calls, Is.EqualTo(handoff.Options.Select(option => option.ThreatFacts)));
			Assert.That(result.Options.Select(option => option.StableIdentity),
				Is.EqualTo(new uint[] { 3, 2 }));
		}

		[Test]
		public void GeneralizedAdapterReturnsTheStandardAggregateAndCrossover()
		{
			var cell = new CPos(1, 2);
			var facts = new StealthTargetThreatFacts(cell,
				new[]
				{
					new StealthCombatGroupSnapshot("stnk", 4, 900),
					new StealthCombatGroupSnapshot("bike", 2, 500)
				},
				new[]
				{
					new StealthCombatGroupSnapshot("e3", 3, 300),
					new StealthCombatGroupSnapshot("ltnk", 1, 600)
				}, false, false);
			double Rating(string friendly, string enemy)
			{
				return friendly == "stnk" ? (enemy == "e3" ? 1d : 4d) :
					enemy == "e3" ? 2d : 8d;
			}

			var actual = GeneralizedCombatTargetThreatAdapter.CalculateStandard(facts, _ => true, Rating);
			var expected = GeneralizedCombatThreatCalculator.CalculateMixedGroupThreat(
				facts.FriendlyGroup.Select(member => new GeneralizedCombatThreatCalculator.GroupTypeCount(
					member.ActorType, member.Count, member.EconomicValue)),
				facts.EnemyGroup.Select(member => new GeneralizedCombatThreatCalculator.GroupTypeCount(
					member.ActorType, member.Count, member.EconomicValue)), Rating);

			Assert.That(actual.ThreatRating, Is.EqualTo(expected.ThreatRating));
			Assert.That(actual.Crossover, Is.EqualTo(expected.Crossover));
		}

		[Test]
		public void PlannedDecloakAndDetectorContextControlTargetabilityWithoutReplacingStandardMath()
		{
			var pairCalls = 0;
			StealthTargetThreatScore Calculate(StealthTargetThreatFacts facts)
			{
				return GeneralizedCombatTargetThreatAdapter.CalculateStandard(facts, _ => true, (_, __) =>
				{
					pairCalls++;
					return 9;
				});
			}

			var cell = new CPos(1, 0);
			var plannedDecloak = Calculate(Facts(cell, cloaked: true, plannedReveal: true));
			var remainsCloaked = Calculate(Facts(cell, cloaked: true, plannedReveal: false));
			var detectorCovered = Calculate(Facts(cell,
				cloaked: true, detector: true, plannedReveal: false));

			Assert.That(plannedDecloak.ThreatRating, Is.EqualTo(9));
			Assert.That(detectorCovered.ThreatRating, Is.EqualTo(plannedDecloak.ThreatRating));
			Assert.That(detectorCovered.Crossover, Is.EqualTo(plannedDecloak.Crossover));
			Assert.That(remainsCloaked.ThreatRating, Is.Zero);
			Assert.That(remainsCloaked.Crossover, Is.Zero);
			Assert.That(pairCalls, Is.EqualTo(2),
				"Only explicit untargetability may bypass a cached pair rating.");
		}

		[TestCase(1, 1)]
		[TestCase(2, 1)]
		[TestCase(3, 2)]
		[TestCase(5, 3)]
		public void RetainsExactCeilingHalfIncludingOneAndOddCounts(int count, int expected)
		{
			var adapter = new AdapterProbe(facts =>
				new StealthTargetThreatScore(facts.StrategicCell.X, facts.StrategicCell.X));

			var result = new StealthTargetThreatFilterBehavior(
				Handoff(count, out _), adapter).Execute();

			Assert.That(result.Options.Count, Is.EqualTo(expected));
		}

		[Test]
		public void ExactThreatTiesKeepTheStableHalfAndIgnoreTravelDistance()
		{
			var actorIds = new uint[]
			{
				50, 40, 30, 20, 10, 101, 102, 103, 104, 105
			};
			var adapter = new AdapterProbe(_ => new StealthTargetThreatScore(7, 3));

			var result = new StealthTargetThreatFilterBehavior(
				Handoff(5, out _, actorIds: actorIds), adapter).Execute();

			Assert.That(result.Options.Select(option => option.StableIdentity),
				Is.EqualTo(new uint[] { 10, 20, 30 }));
		}

		[Test]
		public void NeverHardRejectsDangerousOptions()
		{
			var adapter = new AdapterProbe(facts => new StealthTargetThreatScore(
				facts.StrategicCell.X == 1 ? 1 : GeneralizedCombatThreatCalculator.MaximumThreatRating,
				facts.StrategicCell.X));

			var result = new StealthTargetThreatFilterBehavior(
				Handoff(3, out _), adapter).Execute();

			Assert.That(result.Options.Select(option => option.StableIdentity),
				Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(result.Options[1].ThreatRating,
				Is.EqualTo(GeneralizedCombatThreatCalculator.MaximumThreatRating));
		}

		[Test]
		public void ObservationsCannotStealControlAndOnlyTypedResultHandsOffImmutableOptions()
		{
			var handoff = Handoff(3, out var controller);
			var behavior = new StealthTargetThreatFilterBehavior(handoff,
				new AdapterProbe(facts => new StealthTargetThreatScore(
					facts.StrategicCell.X, facts.StrategicCell.X)));
			controller.Observe(new StealthLifecycleObservationFrame(10, new[]
			{
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Timer),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.WorldEvent),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Damage, 7)
			}));

			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.TargetThreatFilter));
			Assert.That(controller.Epoch, Is.EqualTo(new OwnershipEpoch(5)));
			var result = behavior.Execute();
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.TargetThreatFilter));
			Assert.That(controller.TryAccept(result, out var distanceHandoff), Is.True);
			Assert.That(distanceHandoff.Owner, Is.EqualTo(BehaviorId.TargetDistanceChoice));
			Assert.That(distanceHandoff.Epoch, Is.EqualTo(new OwnershipEpoch(6)));
			Assert.Throws<NotSupportedException>(() =>
				((IList<StealthTargetThreatOption>)distanceHandoff.Options).Add(distanceHandoff.Options[0]));
		}

		[Test]
		public void PrivateStateRoundTripsAndRejectsChangedInputScoreOwnerOrEpoch()
		{
			var adapter = new AdapterProbe(facts => new StealthTargetThreatScore(
				facts.StrategicCell.X * 2, facts.StrategicCell.X));
			var behavior = new StealthTargetThreatFilterBehavior(Handoff(3, out _), adapter);
			var serialized = new List<MiniYamlNode> { behavior.SerializePrivateState(behavior.Execute()) }
				.WriteToString();
			var restored = behavior.RestorePrivateState(MiniYaml.FromString(serialized).Single());

			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState(restored) }.WriteToString(),
				Is.EqualTo(serialized));
			Assert.That(restored.Options.Select(option => option.StableIdentity),
				Is.EqualTo(new uint[] { 1, 2 }));

			var stale = new StealthTargetThreatFilterBehavior(
				Handoff(3, out _, acquisitionEpoch: 4), adapter);
			Assert.Throws<InvalidOperationException>(() =>
				stale.RestorePrivateState(MiniYaml.FromString(serialized).Single()));
			Assert.Throws<InvalidOperationException>(() => behavior.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("Owner: TargetThreatFilter", "Owner: TargetValueFilter")).Single()));
			Assert.Throws<InvalidOperationException>(() => behavior.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("ThreatRating: 2", "ThreatRating: 12")).Single()));
			Assert.Throws<InvalidOperationException>(() => behavior.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("PlannedActionRevealsFormation: True",
					"PlannedActionRevealsFormation: False")).Single()));
		}
	}
}
