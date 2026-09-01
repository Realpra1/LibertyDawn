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
	public sealed class StealthTargetDistanceChoiceBehaviorTest
	{
		sealed class CacheProbe : IStealthTargetAcquisitionCache
		{
			readonly StealthTargetAcquisitionCacheSnapshot snapshot;

			public CacheProbe(StealthTargetAcquisitionCacheSnapshot snapshot)
			{
				this.snapshot = snapshot;
			}

			public StealthTargetAcquisitionCacheSnapshot ReadSnapshot() { return snapshot; }
		}

		sealed class AdapterProbe : IStealthTargetThreatAdapter
		{
			readonly IReadOnlyDictionary<CPos, StealthTargetThreatScore> scores;

			public AdapterProbe(IReadOnlyDictionary<CPos, StealthTargetThreatScore> scores)
			{
				this.scores = scores;
			}

			public StealthTargetThreatScore Calculate(StealthTargetThreatFacts facts)
			{
				return scores[facts.StrategicCell];
			}
		}

		sealed class Candidate
		{
			public uint ActorId { get; }
			public CPos Cell { get; }
			public double Threat { get; }
			public double Crossover { get; }
			public bool Incumbent { get; }

			public Candidate(uint actorId, int x, int y, double threat = 1,
				double crossover = 1, bool incumbent = false)
			{
				ActorId = actorId;
				Cell = new CPos(x, y);
				Threat = threat;
				Crossover = crossover;
				Incumbent = incumbent;
			}
		}

		static StealthTargetDistanceChoicePolicy Policy(int perCell = 1000, int maximum = 3000)
		{
			return new StealthTargetDistanceChoicePolicy(perCell, maximum);
		}

		static StealthTargetThreatFacts Facts(CPos cell)
		{
			return new StealthTargetThreatFacts(cell,
				new[] { new StealthCombatGroupSnapshot("stnk", 4, 900) },
				new[] { new StealthCombatGroupSnapshot("e3", 2, 300) }, true, false, true);
		}

		static StealthTargetDistanceChoiceHandoff Handoff(Candidate[] candidates,
			out StealthLifecycleController controller, long acquisitionEpoch = 3)
		{
			if (candidates.Length != 3)
				throw new ArgumentException("The fixture requires exactly three Step 4B survivors.");

			var fillerCells = Enumerable.Range(1, 7).Select(x => new CPos(x, 10)).ToArray();
			var cells = candidates.Select(candidate => candidate.Cell).Concat(fillerCells).ToArray();
			var targetless = candidates.All(candidate => candidate.ActorId == 0);
			var fillerTargets = targetless ? Array.Empty<StealthStrategicTargetSnapshot>() :
				fillerCells.Select((cell, index) => new StealthStrategicTargetSnapshot(
					(uint)(100 + index), cell, 5000, 1000, 90, 100)).ToArray();
			var targets = candidates.Where(candidate => candidate.ActorId != 0).Select(candidate =>
				new StealthStrategicTargetSnapshot(candidate.ActorId, candidate.Cell, 5000, 1000, 90, 100))
				.Concat(fillerTargets).ToArray();
			var scores = candidates.ToDictionary(candidate => candidate.Cell,
				candidate => new StealthTargetThreatScore(candidate.Threat, candidate.Crossover));
			foreach (var cell in fillerCells)
				scores.Add(cell, new StealthTargetThreatScore(1000, 1000));

			controller = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.TargetAcquisition, new OwnershipEpoch(acquisitionEpoch), -1));
			var cache = new CacheProbe(new StealthTargetAcquisitionCacheSnapshot(40, 20,
				Enumerable.Repeat(0f, 800), cells, 1,
				targets, cells.Select(Facts)));
			var incumbent = candidates.Where(candidate => candidate.Incumbent)
				.Select(candidate => (CPos?)candidate.Cell).SingleOrDefault();
			var acquisition = new StealthTargetAcquisitionBehavior(controller.CurrentHandoff, cache)
				.Execute(new CPos(0, 0), incumbent);
			Assert.That(controller.TryAccept(acquisition, out var valueHandoff), Is.True);
			var value = new StealthTargetValueFilterBehavior(valueHandoff).Execute();
			Assert.That(controller.TryAccept(value, out var threatHandoff), Is.True);
			var threat = new StealthTargetThreatFilterBehavior(
				threatHandoff, new AdapterProbe(scores)).Execute();
			Assert.That(threat.Options.Select(option => option.StrategicCell),
				Is.EquivalentTo(candidates.Select(candidate => candidate.Cell)));
			Assert.That(controller.TryAccept(threat, out var distanceHandoff), Is.True);
			return distanceHandoff;
		}

		static StealthActiveSquadTargetSnapshot Peer(uint actorId, int x, int y)
		{
			return new StealthActiveSquadTargetSnapshot(actorId, new CPos(x, y));
		}

		[Test]
		public void NoOtherSquadPreservesOwnStrategicDistanceAndDoesNotRerankThreat()
		{
			var behavior = new StealthTargetDistanceChoiceBehavior(Handoff(new[]
			{
				new Candidate(90, 1, 0, threat: 100, crossover: 100),
				new Candidate(10, 2, 0, threat: 0, crossover: 0),
				new Candidate(30, 3, 0, threat: 50, crossover: 50)
			}, out _), Array.Empty<StealthActiveSquadTargetSnapshot>(), Policy());

			var mission = behavior.Execute().Mission;

			Assert.That(mission.StableTargetActorId, Is.EqualTo(90));
			Assert.That(mission.EstimatedTravelMilliseconds, Is.EqualTo(1000));
			Assert.That(mission.SeparationCreditMilliseconds, Is.Zero);
			Assert.That(mission.AdjustedTravelCostMilliseconds, Is.EqualTo(1000));
			Assert.That(mission.MinimumSquadSeparationSquared, Is.EqualTo(long.MaxValue));
		}

		[Test]
		public void BoundedSeparationTermCanChangeAComparableNonTiedDistanceChoice()
		{
			var behavior = new StealthTargetDistanceChoiceBehavior(Handoff(new[]
			{
				new Candidate(10, 5, 0),
				new Candidate(20, 7, 0),
				new Candidate(30, 20, 0)
			}, out _), new[] { Peer(700, 5, 0) }, Policy());

			var mission = behavior.Execute().Mission;

			Assert.That(mission.StableTargetActorId, Is.EqualTo(20));
			Assert.That(mission.MinimumSquadSeparationSquared, Is.EqualTo(4));
			Assert.That(mission.SeparationCreditMilliseconds, Is.EqualTo(3000));
			Assert.That(mission.AdjustedTravelCostMilliseconds, Is.EqualTo(4000));
		}

		[Test]
		public void SeparationCapCannotOverwhelmAMateriallyLongerOwnRoute()
		{
			var behavior = new StealthTargetDistanceChoiceBehavior(Handoff(new[]
			{
				new Candidate(10, 5, 0),
				new Candidate(20, 9, 0),
				new Candidate(30, 20, 0)
			}, out _), new[] { Peer(700, 5, 0) }, Policy());

			var mission = behavior.Execute().Mission;

			Assert.That(StealthTargetDistanceChoicePolicy.AbsoluteMaximumSeparationCreditMilliseconds,
				Is.EqualTo(3000));
			Assert.That(mission.StableTargetActorId, Is.EqualTo(10));
			Assert.That(mission.AdjustedTravelCostMilliseconds, Is.EqualTo(5000));
		}

		[Test]
		public void MultipleSquadsPreferTheFarthestMinimumSeparationWithoutExclusivity()
		{
			var peers = new[] { Peer(800, 9, 0), Peer(700, 5, 0) };
			var separated = new StealthTargetDistanceChoiceBehavior(Handoff(new[]
			{
				new Candidate(10, 5, 0),
				new Candidate(20, 7, 0),
				new Candidate(30, 9, 0)
			}, out _), peers, Policy()).Execute().Mission;
			var occupiedButNear = new StealthTargetDistanceChoiceBehavior(Handoff(new[]
			{
				new Candidate(10, 5, 0),
				new Candidate(20, 10, 0),
				new Candidate(30, 20, 0)
			}, out _), peers, Policy()).Execute().Mission;

			Assert.That(separated.StableTargetActorId, Is.EqualTo(20));
			Assert.That(separated.MinimumSquadSeparationSquared, Is.EqualTo(4));
			Assert.That(occupiedButNear.StableTargetActorId, Is.EqualTo(10),
				"A peer-owned cell remains eligible; separation is a preference, not exclusivity.");
		}

		[Test]
		public void DeterministicTiesUseStableActorThenCellCoordinates()
		{
			var actorTie = new StealthTargetDistanceChoiceBehavior(Handoff(new[]
			{
				new Candidate(20, 1, 0), new Candidate(10, 0, 1), new Candidate(30, 0, 0)
			}, out _), new[] { Peer(700, 0, 0) }, Policy()).Execute().Mission;
			var cellTie = new StealthTargetDistanceChoiceBehavior(Handoff(new[]
			{
				new Candidate(0, 1, 0), new Candidate(0, 0, 1), new Candidate(0, 3, 0)
			}, out _), Array.Empty<StealthActiveSquadTargetSnapshot>(), Policy()).Execute().Mission;

			Assert.That(actorTie.StableTargetActorId, Is.EqualTo(10));
			Assert.That(cellTie.StrategicCell, Is.EqualTo(new CPos(1, 0)));
		}

		[Test]
		public void IncumbentRemainsEligible()
		{
			var mission = new StealthTargetDistanceChoiceBehavior(Handoff(new[]
			{
				new Candidate(99, 1, 0, incumbent: true),
				new Candidate(10, 2, 0),
				new Candidate(20, 3, 0)
			}, out _), Array.Empty<StealthActiveSquadTargetSnapshot>(), Policy()).Execute().Mission;

			Assert.That(mission.StableTargetActorId, Is.EqualTo(99));
			Assert.That(mission.TargetOption.ValueOption.IsIncumbent, Is.True);
			Assert.That(mission.EstimatedTravelMilliseconds, Is.EqualTo(1000));
			Assert.That(mission.SeparationCreditMilliseconds, Is.Zero);
			Assert.That(mission.AdjustedTravelCostMilliseconds, Is.EqualTo(1000));
		}

		[Test]
		public void OnlyTypedResultTransfersExactlyOneImmutableMissionToApproach()
		{
			var handoff = Handoff(new[]
			{
				new Candidate(10, 5, 0), new Candidate(20, 7, 0), new Candidate(30, 20, 0)
			}, out var controller);
			var behavior = new StealthTargetDistanceChoiceBehavior(
				handoff, new[] { Peer(700, 5, 0) }, Policy());
			controller.Observe(new StealthLifecycleObservationFrame(10, new[]
			{
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Timer),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.WorldEvent),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Damage, 7)
			}));

			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.TargetDistanceChoice));
			Assert.That(controller.Epoch, Is.EqualTo(new OwnershipEpoch(6)));
			var result = behavior.Execute();
			Assert.That(controller.TryAccept(result, out var approach), Is.True);
			Assert.That(controller.TryAccept(result, out var duplicate), Is.False);
			Assert.That(duplicate, Is.Null);
			Assert.That(approach.Owner, Is.EqualTo(BehaviorId.Approach));
			Assert.That(approach.Epoch, Is.EqualTo(new OwnershipEpoch(7)));
			Assert.That(approach.Missions, Has.Count.EqualTo(1));
			Assert.That(approach.Missions.Single(), Is.SameAs(result.Mission));
			Assert.Throws<NotSupportedException>(() =>
				((IList<StealthApproachMission>)approach.Missions).Add(result.Mission));
			Assert.That(typeof(StealthApproachMission).GetConstructors(), Is.Empty);
			Assert.That(typeof(StealthTargetDistanceChoiceResult).GetConstructors(), Is.Empty);
		}

		[Test]
		public void PrivateStateRoundTripsFullInputsAndRejectsChangedFactsSelectionOwnerOrEpoch()
		{
			var behavior = new StealthTargetDistanceChoiceBehavior(Handoff(new[]
			{
				new Candidate(10, 5, 0, threat: 2, crossover: 3, incumbent: true),
				new Candidate(20, 7, 0, threat: 4, crossover: 5),
				new Candidate(30, 20, 0, threat: 6, crossover: 7)
			}, out _), new[] { Peer(700, 5, 0) }, Policy());
			var serialized = new List<MiniYamlNode> { behavior.SerializePrivateState(behavior.Execute()) }
				.WriteToString();
			var restored = behavior.RestorePrivateState(MiniYaml.FromString(serialized).Single());

			Assert.That(new List<MiniYamlNode> { behavior.SerializePrivateState(restored) }.WriteToString(),
				Is.EqualTo(serialized));
			Assert.That(serialized.Split(new[] { "Selected: True" }, StringSplitOptions.None).Length - 1,
				Is.EqualTo(1));
			Assert.Throws<InvalidOperationException>(() => behavior.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("Owner: TargetDistanceChoice", "Owner: TargetThreatFilter")).Single()));
			Assert.Throws<InvalidOperationException>(() => behavior.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("Epoch: 6", "Epoch: 7")).Single()));
			Assert.Throws<InvalidOperationException>(() => behavior.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("MaximumSeparationCreditMilliseconds: 3000",
					"MaximumSeparationCreditMilliseconds: 2999")).Single()));
			Assert.Throws<InvalidOperationException>(() => behavior.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("StableActorId: 700", "StableActorId: 701")).Single()));
			Assert.Throws<InvalidOperationException>(() => behavior.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("EstimatedTravelMilliseconds: 5000",
					"EstimatedTravelMilliseconds: 5001")).Single()));
			Assert.Throws<InvalidOperationException>(() => behavior.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("ThreatRating: 2", "ThreatRating: 12")).Single()));
			Assert.Throws<InvalidOperationException>(() => behavior.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("MinimumSquadSeparationSquared: 4",
					"MinimumSquadSeparationSquared: 5")).Single()));
			Assert.Throws<InvalidOperationException>(() => behavior.RestorePrivateState(MiniYaml.FromString(
				serialized.Replace("Selected: True", "Selected: False")).Single()));

			var stale = new StealthTargetDistanceChoiceBehavior(Handoff(new[]
			{
				new Candidate(10, 5, 0, threat: 2, crossover: 3, incumbent: true),
				new Candidate(20, 7, 0, threat: 4, crossover: 5),
				new Candidate(30, 20, 0, threat: 6, crossover: 7)
			}, out _, acquisitionEpoch: 4), new[] { Peer(700, 5, 0) }, Policy());
			Assert.Throws<InvalidOperationException>(() =>
				stale.RestorePrivateState(MiniYaml.FromString(serialized).Single()));
		}

		[Test]
		public void ValidatesBoundedPolicyAndImmutablePeerIdentities()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new StealthTargetDistanceChoicePolicy(0, 1000));
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new StealthTargetDistanceChoicePolicy(1000, 0));
			Assert.Throws<ArgumentOutOfRangeException>(() => new StealthTargetDistanceChoicePolicy(1000,
				StealthTargetDistanceChoicePolicy.AbsoluteMaximumSeparationCreditMilliseconds + 1));
			Assert.Throws<ArgumentException>(() => new StealthTargetDistanceChoiceBehavior(Handoff(new[]
			{
				new Candidate(10, 5, 0), new Candidate(20, 7, 0), new Candidate(30, 20, 0)
			}, out _), new[] { Peer(700, 0, 0), Peer(700, 1, 0) }, Policy()));
		}
	}
}
