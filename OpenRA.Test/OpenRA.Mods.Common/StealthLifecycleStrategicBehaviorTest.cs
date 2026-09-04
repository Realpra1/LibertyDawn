#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class StealthLifecycleStrategicBehaviorTest
	{
		sealed class Cache : IStealthTargetAcquisitionCache
		{
			public StealthTargetAcquisitionCacheSnapshot Snapshot;
			public StealthTargetAcquisitionCacheSnapshot ReadSnapshot() { return Snapshot; }
		}

		sealed class Threat : IStealthTargetThreatAdapter
		{
			readonly Dictionary<CPos, StealthTargetThreatScore> scores;
			public Threat(Dictionary<CPos, StealthTargetThreatScore> scores) { this.scores = scores; }
			public StealthTargetThreatScore Calculate(StealthTargetThreatFacts facts)
			{
				return scores[facts.StrategicCell];
			}
		}

		sealed class SafeRoute : IStealthSquadConstructionSafetyService
		{
			public bool TryFindSafeRoute(uint actorId, CPos originStrategicCell,
				CPos destinationStrategicCell, out IReadOnlyList<CPos> routeStrategicCells)
			{
				routeStrategicCells = new[] { destinationStrategicCell };
				return true;
			}
		}

		sealed class ApproachWorld : IStealthApproachStrategicCache,
			IStealthApproachStrategicRouteCache, IStealthApproachLiveWorld,
			IStealthApproachMovementOrders
		{
			public StealthApproachLiveSnapshot Snapshot;
			public IReadOnlyList<CPos> Route;
			public int RouteReads;
			public int Moves;
			public CPos Destination;
			public StealthApproachStrategicCacheSnapshot ReadSnapshot() { throw new NotSupportedException(); }
			public IReadOnlyList<CPos> ReadRoute(CPos originStrategicCell, CPos destinationStrategicCell)
			{
				RouteReads++;
				return Route ?? new[]
				{
					originStrategicCell,
					new CPos(originStrategicCell.X + 1, originStrategicCell.Y),
					destinationStrategicCell
				};
			}

			public StealthApproachLiveSnapshot Read(StealthApproachMission mission) { return Snapshot; }
			public void IssueMove(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, CPos destinationStrategicCell, long orderRevision)
			{
				Moves++;
				Destination = destinationStrategicCell;
			}
		}

		[Test]
		public void StartOnlyHandsBuiltOrRepairedLiveTanksToConstruction()
		{
			var behavior = new StealthStartBehavior(Handoff(BehaviorId.Start));
			var members = new[] { new StealthStartMemberSnapshot(7), new StealthStartMemberSnapshot(8) };
			var timer = behavior.Execute(new StealthLifecycleObservation(
				StealthLifecycleObservationKind.Timer), members);
			var built = behavior.Execute(new StealthLifecycleObservation(
				StealthLifecycleObservationKind.UnitBuilt, 7), members);

			Assert.That(timer.Disposition, Is.EqualTo(StealthStartDisposition.ObservationOnly));
			Assert.That(built.Disposition, Is.EqualTo(StealthStartDisposition.Transition));
			Assert.That(built.MemberActorIds, Is.EqualTo(new uint[] { 7, 8 }));
		}

		[Test]
		public void ConstructionExcludesRoutedReinforcementUntilItArrives()
		{
			var behavior = new StealthSquadConstructionBehavior(
				Handoff(BehaviorId.SquadConstruction), new uint[] { 1, 2 }, new SafeRoute());
			var squads = new[] { new StealthSquadConstructionSquadSnapshot(3, new CPos(5, 5)) };
			var traveling = behavior.Execute(new[]
			{
				new StealthSquadConstructionMemberSnapshot(1, new CPos(5, 5), 3),
				new StealthSquadConstructionMemberSnapshot(2, new CPos(0, 0), 3)
			}, squads);
			var arrived = behavior.Execute(new[]
			{
				new StealthSquadConstructionMemberSnapshot(1, new CPos(5, 5), 3),
				new StealthSquadConstructionMemberSnapshot(2, new CPos(4, 5), 3)
			}, squads);

			Assert.That(traveling.Assignments.Single().Disposition,
				Is.EqualTo(StealthSquadAssignmentDisposition.RoutedReinforcement));
			Assert.That(traveling.Centers.Single().MemberActorIds, Is.EqualTo(new uint[] { 1 }));
			Assert.That(arrived.Assignments.Single().Disposition,
				Is.EqualTo(StealthSquadAssignmentDisposition.ActiveMember));
			Assert.That(arrived.Centers.Single().MemberActorIds, Is.EqualTo(new uint[] { 1, 2 }));
		}

		[Test]
		public void ConstructionAnchorsAssignedSquadWhenEveryMemberStartsFarFromItsCenter()
		{
			var behavior = new StealthSquadConstructionBehavior(
				Handoff(BehaviorId.SquadConstruction), new uint[] { 1, 2 }, new SafeRoute());
			var result = behavior.Execute(new[]
			{
				new StealthSquadConstructionMemberSnapshot(1, new CPos(2, 2), 3),
				new StealthSquadConstructionMemberSnapshot(2, new CPos(8, 8), 3)
			}, new[] { new StealthSquadConstructionSquadSnapshot(3, new CPos(5, 5)) });

			Assert.That(result.Centers.Single().MemberActorIds, Is.EqualTo(new uint[] { 1 }));
			Assert.That(result.Assignments.Single(assignment => assignment.ActorId == 1).Disposition,
				Is.EqualTo(StealthSquadAssignmentDisposition.NewCenter));
			Assert.That(result.Assignments.Single(assignment => assignment.ActorId == 2).Disposition,
				Is.EqualTo(StealthSquadAssignmentDisposition.RoutedReinforcement));
		}

		[Test]
		public void AcquisitionKeepsIncumbentAndUsesBoundedCachedSearch()
		{
			var enemies = Enumerable.Range(1, 10).Select(x => new CPos(x, 1)).ToArray();
			var cache = new Cache
			{
				Snapshot = new StealthTargetAcquisitionCacheSnapshot(12, 3, new float[36],
					enemies, .1f)
			};
			var behavior = new StealthTargetAcquisitionBehavior(
				Handoff(BehaviorId.TargetAcquisition), cache);
			var result = behavior.Execute(new CPos(0, 1), enemies[9]);

			Assert.That(result.Options, Has.Count.EqualTo(10));
			Assert.That(result.Options.Single(option => option.IsIncumbent).StrategicCell,
				Is.EqualTo(enemies[9]));
			Assert.That(result.PrimitiveOperations,
				Is.LessThanOrEqualTo(StealthTargetAcquisitionBehavior.MaximumPrimitiveOperations));
		}

		[Test]
		public void AcquisitionUsesAllKnownTargetsWhenFewerThanTenRemain()
		{
			var enemies = new[] { new CPos(1, 1), new CPos(2, 1) };
			var cache = new Cache
			{
				Snapshot = new StealthTargetAcquisitionCacheSnapshot(4, 3, new float[12],
					enemies, .1f)
			};
			var result = new StealthTargetAcquisitionBehavior(
				Handoff(BehaviorId.TargetAcquisition), cache).Execute(new CPos(0, 1));

			Assert.That(result.Disposition,
				Is.EqualTo(StealthTargetAcquisitionDisposition.ReadyForValueFilter));
			Assert.That(result.Options, Has.Count.EqualTo(2));
		}

		[Test]
		public void AcquisitionDoesNotLetNearbyLowValueCellsHideAHighValueTarget()
		{
			var lowCells = Enumerable.Range(1, 10).Select(x => new CPos(x, 0)).ToArray();
			var highCell = new CPos(11, 0);
			var enemies = lowCells.Append(highCell).ToArray();
			var targets = lowCells.Select((cell, index) =>
				new StealthStrategicTargetSnapshot((uint)index + 1, cell, 1, 100, 100, 100))
				.Append(new StealthStrategicTargetSnapshot(20, highCell, 6000, 1100, 100, 100));
			var cache = new Cache
			{
				Snapshot = new StealthTargetAcquisitionCacheSnapshot(12, 2, new float[24],
					enemies, .1f, targets)
			};

			var result = new StealthTargetAcquisitionBehavior(
				Handoff(BehaviorId.TargetAcquisition), cache).Execute(new CPos(0, 0));

			Assert.That(result.Options.Select(option => option.StrategicCell),
				Is.EqualTo(new[] { highCell }));
		}

		[Test]
		public void AcquisitionAwaitsAnEmptyCache()
		{
			var cache = new Cache
			{
				Snapshot = new StealthTargetAcquisitionCacheSnapshot(2, 2, new float[4],
					Array.Empty<CPos>(), .1f)
			};
			var result = new StealthTargetAcquisitionBehavior(
				Handoff(BehaviorId.TargetAcquisition), cache).Execute(new CPos(0, 0));

			Assert.That(result.Disposition,
				Is.EqualTo(StealthTargetAcquisitionDisposition.AwaitingCache));
			Assert.That(result.Options, Is.Empty);
		}

		[Test]
		public void AcquisitionWaitsForItsMoveCloserDestinationBeforeRescanning()
		{
			var cache = new Cache
			{
				Snapshot = new StealthTargetAcquisitionCacheSnapshot(12, 2, new float[24],
					new[] { new CPos(10, 0) }, 10f)
			};
			var behavior = new StealthTargetAcquisitionBehavior(
				Handoff(BehaviorId.TargetAcquisition), cache);
			var first = behavior.Execute(new CPos(0, 0));
			var retained = behavior.Execute(new CPos(1, 0));

			Assert.That(first.Disposition,
				Is.EqualTo(StealthTargetAcquisitionDisposition.MoveCloserAndRescan));
			Assert.That(first.MoveCloserStrategicCell, Is.EqualTo(new CPos(3, 0)),
				"The cached route should use the full 30-second safe travel prefix, not four fixed legs.");
			Assert.That(retained.MoveCloserStrategicCell,
				Is.EqualTo(first.MoveCloserStrategicCell));
			Assert.That(retained.PrimitiveOperations, Is.Zero);
		}

		[Test]
		public void AcquisitionRescansWhenEnginePathFinishesShortOfMoveCloserDestination()
		{
			var cache = new Cache
			{
				Snapshot = new StealthTargetAcquisitionCacheSnapshot(12, 2, new float[24],
					new[] { new CPos(10, 0) }, 10f)
			};
			var behavior = new StealthTargetAcquisitionBehavior(
				Handoff(BehaviorId.TargetAcquisition), cache);
			var first = behavior.Execute(new CPos(0, 0));
			var rescanned = behavior.Execute(new CPos(1, 0), movementFinished: true);

			Assert.That(first.MoveCloserStrategicCell, Is.EqualTo(new CPos(3, 0)));
			Assert.That(rescanned.MoveCloserStrategicCell, Is.EqualTo(new CPos(4, 0)));
			Assert.That(rescanned.PrimitiveOperations, Is.GreaterThan(0));
		}

		[Test]
		public void AcquisitionRescansWhenItsRouteExposureChanges()
		{
			var cache = new Cache
			{
				Snapshot = new StealthTargetAcquisitionCacheSnapshot(12, 2, new float[24],
					new[] { new CPos(10, 0) }, 10f, formationCloaked: true)
			};
			var behavior = new StealthTargetAcquisitionBehavior(
				Handoff(BehaviorId.TargetAcquisition), cache);
			var first = behavior.Execute(new CPos(0, 0));
			cache.Snapshot = new StealthTargetAcquisitionCacheSnapshot(12, 2, new float[24],
				new[] { new CPos(10, 0) }, 10f, formationCloaked: false);
			var rescanned = behavior.Execute(new CPos(1, 0));

			Assert.That(first.MoveCloserStrategicCell, Is.EqualTo(new CPos(3, 0)));
			Assert.That(rescanned.MoveCloserStrategicCell, Is.EqualTo(new CPos(4, 0)));
			Assert.That(rescanned.PrimitiveOperations, Is.GreaterThan(0));
		}

		[Test]
		public void AcquisitionPreservesTheFirstSafeTurnWhenMovingCloser()
		{
			var danger = new float[10];
			danger[1] = StealthAISpecialistPolicy.HardRouteDangerThreshold;
			var cache = new Cache
			{
				Snapshot = new StealthTargetAcquisitionCacheSnapshot(5, 2, danger,
					new[] { new CPos(4, 0) }, 10f, routeThreatPenalty: 10f)
			};

			var result = new StealthTargetAcquisitionBehavior(
				Handoff(BehaviorId.TargetAcquisition), cache).Execute(new CPos(0, 0));

			Assert.That(result.MoveCloserStrategicCell, Is.EqualTo(new CPos(0, 1)),
				"The engine order must stop at the cached route's first turn instead of cutting through danger.");
		}

		[Test]
		public void AcquisitionBudgetExhaustionStillMovesOneCachedCellTowardTheEnemy()
		{
			const int size = 256;
			var cache = new Cache
			{
				Snapshot = new StealthTargetAcquisitionCacheSnapshot(size, size,
					new float[size * size], new[] { new CPos(size - 1, size - 1) }, 10f)
			};
			var result = new StealthTargetAcquisitionBehavior(
				Handoff(BehaviorId.TargetAcquisition), cache).Execute(new CPos(0, 0));

			Assert.That(result.Disposition,
				Is.EqualTo(StealthTargetAcquisitionDisposition.MoveCloserAndRescan));
			Assert.That(result.MoveCloserStrategicCell, Is.EqualTo(new CPos(1, 0)));
		}

		[Test]
		public void AcquisitionPermanentlyBiasesDifferentSquadsTowardDifferentCorners()
		{
			var enemies = Enumerable.Range(0, 6).Select(x => new CPos(x, 0))
				.Concat(Enumerable.Range(6, 6).Select(x => new CPos(x, 11))).ToArray();
			var cache = new Cache
			{
				Snapshot = new StealthTargetAcquisitionCacheSnapshot(12, 12, new float[144],
					enemies, .1f)
			};
			var upperLeft = new StealthTargetAcquisitionBehavior(
				Handoff(BehaviorId.TargetAcquisition), cache, 0).Execute(new CPos(6, 6));
			var lowerRight = new StealthTargetAcquisitionBehavior(
				Handoff(BehaviorId.TargetAcquisition), cache, 3).Execute(new CPos(6, 6));

			Assert.That(upperLeft.Options.Select(option => option.StrategicCell),
				Does.Contain(new CPos(0, 0)));
			Assert.That(upperLeft.Options.Select(option => option.StrategicCell),
				Does.Not.Contain(new CPos(11, 11)));
			Assert.That(lowerRight.Options.Select(option => option.StrategicCell),
				Does.Contain(new CPos(11, 11)));
			Assert.That(lowerRight.Options.Select(option => option.StrategicCell),
				Does.Not.Contain(new CPos(0, 0)));
		}

		[Test]
		public void AcquisitionCornerBiasAlsoRanksSmallCandidateSets()
		{
			var cache = new Cache
			{
				Snapshot = new StealthTargetAcquisitionCacheSnapshot(12, 12, new float[144],
					new[] { new CPos(0, 0), new CPos(11, 11) }, .1f)
			};
			var upperLeft = new StealthTargetAcquisitionBehavior(
				Handoff(BehaviorId.TargetAcquisition), cache, 0).Execute(new CPos(6, 6));
			var lowerRight = new StealthTargetAcquisitionBehavior(
				Handoff(BehaviorId.TargetAcquisition), cache, 3).Execute(new CPos(6, 6));

			Assert.That(upperLeft.Options.First().StrategicCell, Is.EqualTo(new CPos(0, 0)));
			Assert.That(lowerRight.Options.First().StrategicCell, Is.EqualTo(new CPos(11, 11)));
			Assert.That(upperLeft.Options.Single(option => option.StrategicCell == new CPos(0, 0))
				.EstimatedTravelMilliseconds,
				Is.EqualTo(lowerRight.Options.Single(option => option.StrategicCell == new CPos(0, 0))
					.EstimatedTravelMilliseconds),
				"Corner bias may order discovery but travel cost must stay rooted at the live squad center.");
		}

		[Test]
		public void ValueFilterPrefersHighValueTierThenKeepsUpperHalf()
		{
			var options = new[]
			{
				Option(1, 1000, 1, 100),
				Option(2, 6000, 2, 100),
				Option(3, 10000, 3, 50),
				Option(4, 7000, 4, 100)
			};
			var behavior = new StealthTargetValueFilterBehavior(
				Construct<StealthTargetValueFilterHandoff>(
					Handoff(BehaviorId.TargetValueFilter), options));
			var result = behavior.Execute();

			Assert.That(result.Options.Select(option => option.StrategicCell.X),
				Is.EqualTo(new[] { 3, 4 }));
			Assert.That(result.Options, Has.None.Matches<StealthTargetValueOption>(
				option => option.StrategicCell.X == 1));
		}

		[Test]
		public void ValueFilterFallsBackBelowFloorWhenNoHighValueCellExists()
		{
			var options = new[] { Option(1, 100, 1, 100), Option(2, 200, 2, 100) };
			var behavior = new StealthTargetValueFilterBehavior(
				Construct<StealthTargetValueFilterHandoff>(
					Handoff(BehaviorId.TargetValueFilter), options));

			Assert.That(behavior.Execute().Options.Single().StrategicCell.X, Is.EqualTo(2));
		}

		[Test]
		public void ThreatFilterRanksEveryCellWithoutHardRejection()
		{
			var values = new[] { Value(1), Value(2), Value(3) };
			var scores = new Dictionary<CPos, StealthTargetThreatScore>
			{
				[new CPos(1, 0)] = new StealthTargetThreatScore(90, .1),
				[new CPos(2, 0)] = new StealthTargetThreatScore(1, 9),
				[new CPos(3, 0)] = new StealthTargetThreatScore(2, 2)
			};
			var behavior = new StealthTargetThreatFilterBehavior(
				Construct<StealthTargetThreatFilterHandoff>(
					Handoff(BehaviorId.TargetThreatFilter), values),
				new Threat(scores));

			Assert.That(behavior.Execute().Options.Select(option => option.StrategicCell.X),
				Is.EqualTo(new[] { 2, 3 }));
		}

		[Test]
		public void DistanceChoiceUsesLowestCachedRouteCost()
		{
			var nearCrowded = ThreatOption(1, 5000);
			var slightlyFarSeparated = ThreatOption(5, 5500);
			var behavior = new StealthTargetDistanceChoiceBehavior(
				Construct<StealthTargetDistanceChoiceHandoff>(Handoff(BehaviorId.TargetDistanceChoice),
					new[] { nearCrowded, slightlyFarSeparated }));

			Assert.That(behavior.Execute().Mission.StrategicCell, Is.EqualTo(new CPos(1, 0)));
		}

		[Test]
		public void ApproachUsesCachedRouteButClassifiesArrivalFromLiveActors()
		{
			var world = new ApproachWorld
			{
				Snapshot = ApproachSnapshot(new CPos(0, 0), Array.Empty<uint>())
			};
			var threat = new Threat(new Dictionary<CPos, StealthTargetThreatScore>
			{
				[new CPos(0, 0)] = new StealthTargetThreatScore(200, 9)
			});
			var handoff = Construct<StealthApproachHandoff>(Handoff(BehaviorId.Approach), MissionAt(5));
			var behavior = new StealthApproachBehavior(handoff, world, world, world);

			var moving = behavior.Execute();
			world.Snapshot = ApproachSnapshot(new CPos(4, 0), new uint[] { 71 });
			var arrived = behavior.Execute();

			Assert.That(moving.Disposition, Is.EqualTo(StealthApproachDisposition.Moving));
			Assert.That(world.Destination, Is.EqualTo(new CPos(5, 0)));
			Assert.That(arrived.Disposition, Is.EqualTo(StealthApproachDisposition.Kite));
			Assert.That(arrived.LiveDefenderActorIds, Is.EqualTo(new uint[] { 71 }));
		}

		[Test]
		public void ApproachReacquiresInsteadOfStallingWhenCachedRouteIsUnavailable()
		{
			var world = new ApproachWorld
			{
				Snapshot = ApproachSnapshot(new CPos(0, 0), Array.Empty<uint>()),
				Route = Array.Empty<CPos>()
			};
			var threat = new Threat(new Dictionary<CPos, StealthTargetThreatScore>
			{
				[new CPos(0, 0)] = new StealthTargetThreatScore(1, 1)
			});
			var handoff = Construct<StealthApproachHandoff>(Handoff(BehaviorId.Approach), MissionAt(5));

			var result = new StealthApproachBehavior(handoff, world, world, world).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthApproachDisposition.Reacquire));
			Assert.That(world.Moves, Is.Zero);
		}

		[Test]
		public void ApproachReacquiresWhenTheEngineCannotLeaveItsStrategicCell()
		{
			var world = new ApproachWorld
			{
				Snapshot = ApproachSnapshot(new CPos(0, 0), Array.Empty<uint>(), true)
			};
			var threat = new Threat(new Dictionary<CPos, StealthTargetThreatScore>
			{
				[new CPos(0, 0)] = new StealthTargetThreatScore(1, 1)
			});
			var behavior = new StealthApproachBehavior(
				Construct<StealthApproachHandoff>(Handoff(BehaviorId.Approach), MissionAt(5)),
				world, world, world);

			behavior.Execute();
			world.Snapshot = ApproachSnapshot(new CPos(0, 0), Array.Empty<uint>());
			behavior.Execute();
			world.Snapshot = ApproachSnapshot(new CPos(0, 0), Array.Empty<uint>(), true);
			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthApproachDisposition.Reacquire));
			Assert.That(world.Moves, Is.EqualTo(1));
		}

		[Test]
		public void BlockedApproachHandsNearbyDefendersToLiveCombat()
		{
			var world = new ApproachWorld
			{
				Snapshot = ApproachSnapshot(new CPos(0, 0), Array.Empty<uint>(), true)
			};
			var behavior = new StealthApproachBehavior(
				Construct<StealthApproachHandoff>(Handoff(BehaviorId.Approach), MissionAt(5)),
				world, world, world);

			behavior.Execute();
			world.Snapshot = ApproachSnapshot(new CPos(0, 0), new uint[] { 71 }, true);
			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthApproachDisposition.Kite));
			Assert.That(result.LiveDefenderActorIds, Is.EqualTo(new uint[] { 71 }));
			Assert.That(world.Moves, Is.EqualTo(1));
		}

		[Test]
		public void ApproachDoesNotReplaceAnActiveEngineRouteWhenTheStrategicRouteChanges()
		{
			var world = new ApproachWorld
			{
				Snapshot = ApproachSnapshot(new CPos(0, 0), Array.Empty<uint>())
			};
			var behavior = new StealthApproachBehavior(
				Construct<StealthApproachHandoff>(Handoff(BehaviorId.Approach), MissionAt(5)),
				world, world, world);

			behavior.Execute();
			Assert.That(world.Destination, Is.EqualTo(new CPos(5, 0)));

			world.Snapshot = ApproachSnapshot(new CPos(0, 1), Array.Empty<uint>());
			world.Route = new[] { new CPos(0, 1), new CPos(0, 2), new CPos(5, 0) };
			behavior.Execute();

			Assert.That(world.Moves, Is.EqualTo(1));
			Assert.That(world.RouteReads, Is.EqualTo(1),
				"A live safety tick must not recalculate an in-flight engine route.");
			Assert.That(world.Destination, Is.EqualTo(new CPos(5, 0)));

			world.Snapshot = ApproachSnapshot(new CPos(0, 1), Array.Empty<uint>(), true);
			behavior.Execute();

			Assert.That(world.Moves, Is.EqualTo(2));
			Assert.That(world.Destination, Is.EqualTo(new CPos(0, 2)));
		}

		[Test]
		public void ApproachPreservesCachedTurnsAroundStrategicDanger()
		{
			var world = new ApproachWorld
			{
				Snapshot = ApproachSnapshot(new CPos(0, 0), Array.Empty<uint>()),
				Route = new[]
				{
					new CPos(0, 0), new CPos(1, 0), new CPos(2, 0),
					new CPos(2, 1), new CPos(3, 1), new CPos(4, 1), new CPos(5, 0)
				}
			};
			var behavior = new StealthApproachBehavior(
				Construct<StealthApproachHandoff>(Handoff(BehaviorId.Approach), MissionAt(5)),
				world, world, world);

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthApproachDisposition.Moving));
			Assert.That(world.Destination, Is.EqualTo(new CPos(2, 0)));
			Assert.That(world.Moves, Is.EqualTo(1));
		}

		[Test]
		public void ApproachHandsAnUnsafeExposedPositionToLiveKiting()
		{
			var member = new StealthApproachMemberSnapshot(1, new CPos(0, 0));
			var world = new ApproachWorld
			{
				Snapshot = new StealthApproachLiveSnapshot(true, new[] { member },
					new[] { new StealthCombatGroupSnapshot("stnk", 1, 900) },
					new[] { new StealthCombatGroupSnapshot("obli", 1, 1500) },
					new uint[] { 71 }, false, false, currentPositionSafe: false,
					immediateThreatActorId: 71, immediateThreatCurrentCell: new CPos(1, 0),
					currentThreatScore: new StealthTargetThreatScore(4, 3))
			};
			var behavior = new StealthApproachBehavior(
				Construct<StealthApproachHandoff>(Handoff(BehaviorId.Approach), MissionAt(5)),
				world, world, world);

			var result = behavior.Execute();
			var controller = Construct<StealthLifecycleController>(BehaviorId.Approach);

			Assert.That(result.Disposition, Is.EqualTo(StealthApproachDisposition.Kite));
			Assert.That(world.Moves, Is.Zero);
			Assert.That(controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.Kite, Is.Not.Null);
		}

		[Test]
		public void ApproachStartsKitingWhenItFindsADefenderFromASafeCell()
		{
			var world = new ApproachWorld
			{
				Snapshot = ApproachSnapshot(new CPos(0, 0), new uint[] { 71 })
			};
			var behavior = new StealthApproachBehavior(
				Construct<StealthApproachHandoff>(Handoff(BehaviorId.Approach), MissionAt(5)),
				world, world, world);

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthApproachDisposition.Kite));
			Assert.That(result.CurrentPositionSafe, Is.True);
			Assert.That(result.LiveDefenderActorIds, Is.EqualTo(new uint[] { 71 }));
			Assert.That(world.Moves, Is.Zero);
		}

		[Test]
		public void ApproachUsesLiveKitingBeforeReacquiringAnInvalidTarget()
		{
			var member = new StealthApproachMemberSnapshot(1, new CPos(0, 0));
			var world = new ApproachWorld
			{
				Snapshot = new StealthApproachLiveSnapshot(false, new[] { member },
					new[] { new StealthCombatGroupSnapshot("stnk", 1, 900) },
					new[] { new StealthCombatGroupSnapshot("mtnk", 1, 800) },
					new uint[] { 71 }, false, false, currentPositionSafe: false,
					immediateThreatActorId: 71, immediateThreatCurrentCell: new CPos(1, 0),
					currentThreatScore: new StealthTargetThreatScore(2, 2))
			};
			var behavior = new StealthApproachBehavior(
				Construct<StealthApproachHandoff>(Handoff(BehaviorId.Approach), MissionAt(5)),
				world, world, world);

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthApproachDisposition.Kite));
			Assert.That(result.ImmediateThreatActorId, Is.EqualTo(71));
			Assert.That(world.Moves, Is.Zero);
		}

		static StealthBehaviorHandoff Handoff(BehaviorId owner)
		{
			return Construct<StealthBehaviorHandoff>(owner, new OwnershipEpoch(1));
		}

		static StealthTargetOption Option(int x, int priority, uint id, int hitPoints)
		{
			var cell = new CPos(x, 0);
			return Construct<StealthTargetOption>(cell, (int?)(x * 1000), false,
				new[] { new StealthStrategicTargetSnapshot(id, cell, priority, 1100, hitPoints, 100) }, null);
		}

		static StealthTargetValueOption Value(int x)
		{
			return Construct<StealthTargetValueOption>(Option(x, 6000, (uint)x, 100), 6000L);
		}

		static StealthTargetThreatOption ThreatOption(int x, int travel)
		{
			var cell = new CPos(x, 0);
			var option = Construct<StealthTargetOption>(cell, (int?)travel, false,
				new[] { new StealthStrategicTargetSnapshot((uint)x, cell, 6000, 1100, 100, 100) }, null);
			return Construct<StealthTargetThreatOption>(
				Construct<StealthTargetValueOption>(option, 6000L), new StealthTargetThreatScore(1, 1));
		}

		static StealthApproachMission MissionAt(int x)
		{
			return Construct<StealthApproachMission>(ThreatOption(x, x * 1000));
		}

		static StealthApproachLiveSnapshot ApproachSnapshot(CPos memberCell,
			IEnumerable<uint> defenders, bool needsMovementOrder = false)
		{
			var member = new StealthApproachMemberSnapshot(1, memberCell,
				needsMovementOrder: needsMovementOrder);
			return new StealthApproachLiveSnapshot(true,
				new[] { member },
				new[] { new StealthCombatGroupSnapshot("stnk", 1, 900) },
				Array.Empty<StealthCombatGroupSnapshot>(), defenders, true, false);
		}

		static T Construct<T>(params object[] arguments)
		{
			return (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance |
				BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null);
		}
	}
}
