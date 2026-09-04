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
	public sealed class StealthLifecycleLocalCombatBehaviorTest
	{
		sealed class Guard : IStealthLifecycleOwnershipGuard
		{
			public bool IsActive(BehaviorId owner, OwnershipEpoch epoch) { return true; }
		}

		sealed class KiteWorld : IStealthKiteLiveWorld
		{
			public StealthKiteLiveSnapshot Snapshot;
			public Func<uint, CPos, bool> Reachable = (target, cell) => true;
			public Func<uint, CPos, uint?> Blocker = (target, cell) => null;
			public StealthKiteLiveSnapshot Read(StealthApproachMission mission) { return Snapshot; }
			public bool CanReach(uint targetActorId, CPos cell) { return Reachable(targetActorId, cell); }
			public uint? BlockingActor(uint targetActorId, CPos firingCell)
			{
				return Blocker(targetActorId, firingCell);
			}
		}

		sealed class KiteThreat : IStealthKiteThreatAdapter
		{
			public readonly List<StealthKiteThreatFacts> Facts = new List<StealthKiteThreatFacts>();
			public Func<StealthKiteThreatFacts, bool> Approved = facts => true;
			public double FallbackCrossover = 1;
			public StealthKiteFallbackFacts FallbackFacts;
			public StealthKiteSafetyResult Calculate(StealthKiteThreatFacts facts)
			{
				Facts.Add(facts);
				return new StealthKiteSafetyResult(new StealthTargetThreatScore(1, 1), Approved(facts));
			}

			public StealthTargetThreatScore CalculateAttackCrossover(StealthKiteFallbackFacts facts)
			{
				FallbackFacts = facts;
				return new StealthTargetThreatScore(1, FallbackCrossover);
			}
		}

		sealed class KiteOrders : IStealthKiteOrders
		{
			public int Moves;
			public int Attacks;
			public uint[] Actors;
			public CPos Cell;
			public void IssueMove(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, CPos cell, StealthKiteOrderToken token)
			{
				Moves++;
				Actors = actorIds.ToArray();
				Cell = cell;
			}

			public void IssueAttack(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint targetActorId, CPos targetCurrentCell,
				StealthKiteOrderToken token)
			{
				Attacks++;
				Actors = actorIds.ToArray();
				Cell = targetCurrentCell;
			}
		}

		sealed class CrushWorld : IStealthCrushLiveWorld
		{
			public StealthCrushLiveSnapshot Snapshot;
			public StealthCrushLiveSnapshot Read(StealthApproachMission mission) { return Snapshot; }
		}

		sealed class CrushThreat : IStealthCrushThreatAdapter
		{
			public StealthCrushThreatFacts LastFacts;
			public StealthCrushSafetyResult Calculate(StealthCrushThreatFacts facts)
			{
				LastFacts = facts;
				return new StealthCrushSafetyResult(new StealthTargetThreatScore(1, 1),
					facts.FormationCloaked && !facts.HasDetectorCoverage);
			}
		}

		sealed class CrushOrders : IStealthCrushOrders
		{
			public readonly List<CPos> Cells = new List<CPos>();
			public void IssueCrush(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint targetActorId, CPos targetCurrentCell,
				long attemptRevision)
			{
				Cells.Add(targetCurrentCell);
			}
		}

		sealed class UndefendedWorld : IStealthUndefendedAttackLiveWorld
		{
			public StealthUndefendedAttackLiveSnapshot Snapshot;
			public StealthUndefendedAttackLiveSnapshot Read(StealthApproachMission mission) { return Snapshot; }
		}

		sealed class UndefendedThreat : IStealthUndefendedAttackThreatAdapter
		{
			public bool Approved = true;
			public StealthUndefendedAttackSafetyResult Calculate(StealthUndefendedAttackThreatFacts facts)
			{
				return new StealthUndefendedAttackSafetyResult(
					new StealthTargetThreatScore(0, double.PositiveInfinity), Approved, false);
			}
		}

		sealed class UndefendedOrders : IStealthUndefendedAttackOrders
		{
			public readonly List<uint> Targets = new List<uint>();
			public readonly List<long> Revisions = new List<long>();
			public void IssueAttack(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint targetActorId, long orderRevision)
			{
				Targets.Add(targetActorId);
				Revisions.Add(orderRevision);
			}
		}

		sealed class MassWorld : IStealthMassAttackLiveWorld
		{
			public StealthMassAttackLiveSnapshot Snapshot;
			public uint? BlockerId;
			public StealthMassAttackLiveSnapshot Read(StealthApproachMission mission, CPos attackCenter)
			{
				return Snapshot;
			}

			public uint? BlockingActor(uint targetActorId, CPos firingCell) { return BlockerId; }
		}

		sealed class MassThreat : IStealthMassAttackThreatAdapter, IStealthMassAttackThreatEvaluation
		{
			public double Crossover = 2;
			public Func<StealthMassAttackThreatFacts, bool> Approved = facts => true;
			public Func<StealthMassAttackThreatFacts, uint> SelectedThreat =
				facts => facts.SelectedTargetActorId;
			public int Beginnings;
			public int Calculations;
			public IStealthMassAttackThreatEvaluation Begin(StealthMassAttackThreatFacts facts)
			{
				Beginnings++;
				return this;
			}

			public StealthMassAttackThreatResult Calculate(StealthMassAttackThreatFacts facts)
			{
				Calculations++;
				return new StealthMassAttackThreatResult(
					new StealthTargetThreatScore(1, Crossover), SelectedThreat(facts),
					Approved(facts));
			}
		}

		sealed class MassOrders : IStealthMassAttackOrders
		{
			public uint Target;
			public int Attacks;
			public int Moves;
			public CPos Cell;
			public void IssueMove(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint targetActorId, CPos destinationCell,
				StealthMassAttackOrderToken token)
			{
				Target = targetActorId;
				Cell = destinationCell;
				Moves++;
			}

			public void IssueAttack(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint targetActorId, CPos targetCurrentCell,
				StealthMassAttackOrderToken token)
			{
				Target = targetActorId;
				Attacks++;
			}
		}

		[Test]
		public void KiteFiresNowWhenCurrentLivePositionIsSafe()
		{
			var world = new KiteWorld { Snapshot = KiteSnapshot(new CPos(0, 0), new CPos(4, 0)) };
			var threat = new KiteThreat { Approved = facts => facts.PlannedCell == new CPos(0, 0) };
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders);

			var result = behavior.Execute();

			Assert.That(result.Phase, Is.EqualTo(StealthKitePhase.Fire));
			Assert.That(orders.Attacks, Is.EqualTo(1));
			Assert.That(orders.Actors, Is.EqualTo(new uint[] { 1 }));
			Assert.That(threat.Facts.Single().PlannedAttack, Is.True);
		}

		[Test]
		public void KiteRechecksAMovingTargetWithoutReplacingTheSameActorAttack()
		{
			var world = new KiteWorld { Snapshot = KiteSnapshot(new CPos(0, 0), new CPos(4, 0)) };
			var threat = new KiteThreat { Approved = facts => true };
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders);

			behavior.Execute();
			world.Snapshot = KiteSnapshot(new CPos(0, 0), new CPos(3, 0));
			behavior.Execute();

			Assert.That(threat.Facts, Has.Count.EqualTo(2));
			Assert.That(orders.Attacks, Is.EqualTo(1),
				"the engine tracks an actor target without repeated attack orders");
		}

		[Test]
		public void KiteLetsTheEngineFinishAMoveWhenTheTargetMoves()
		{
			var firstCell = new CPos(2, 0);
			var replacementCell = new CPos(3, 0);
			var world = new KiteWorld
			{
				Snapshot = KiteSnapshot(new CPos(0, 0), new CPos(7, 0), new[] { firstCell })
			};
			var threat = new KiteThreat
			{
				Approved = facts => facts.PlannedCell == firstCell ||
					facts.PlannedCell == replacementCell
			};
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders);

			behavior.Execute();
			world.Snapshot = KiteSnapshot(new CPos(1, 0), new CPos(8, 0),
				new[] { replacementCell });
			var retained = behavior.Execute();

			Assert.That(retained.Phase, Is.EqualTo(StealthKitePhase.Position));
			Assert.That(retained.FireCell, Is.EqualTo(firstCell));
			Assert.That(retained.Safety.HasValue, Is.True);
			Assert.That(retained.Safety.Value.Approved, Is.True);
			Assert.That(orders.Moves, Is.EqualTo(1));
			Assert.That(orders.Cell, Is.EqualTo(firstCell));
		}

		[Test]
		public void KiteDoesNotRetainMovementPlannedForADeadTarget()
		{
			var oldCell = new CPos(2, 0);
			var memberCell = new CPos(0, 0);
			var world = new KiteWorld
			{
				Snapshot = KiteSnapshot(memberCell, new CPos(7, 0), new[] { oldCell })
			};
			var threat = new KiteThreat
			{
				Approved = facts => facts.SelectedTargetActorId == 71 ?
					facts.PlannedCell == oldCell : true
			};
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders);

			behavior.Execute();
			var replacement = new StealthKiteActorSnapshot(72, "htnk", new CPos(4, 0),
				100, 100, 4, true, false, false, false, false);
			world.Snapshot = new StealthKiteLiveSnapshot(2,
				new[] { new StealthKiteMemberSnapshot(1, memberCell, 5) },
				new[] { replacement }, new[] { oldCell }, true);
			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.Reacquire));
			Assert.That(result.SelectedTargetActorId, Is.Null);
			Assert.That(orders.Moves, Is.EqualTo(1));
			Assert.That(orders.Attacks, Is.Zero);
		}

		[Test]
		public void KiteFinishesASafeLiveTargetAfterTheAttackActivityEnds()
		{
			var safeCell = new CPos(1, 0);
			var world = new KiteWorld
			{
				Snapshot = KiteSnapshot(new CPos(0, 0), new CPos(4, 0), new[] { safeCell })
			};
			var threat = new KiteThreat { Approved = facts => true };
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders);

			behavior.Execute();
			world.Snapshot = new StealthKiteLiveSnapshot(2,
				new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5, needsMovementOrder: true) },
				KiteSnapshot(new CPos(0, 0), new CPos(4, 0)).Actors, new[] { safeCell }, true);
			behavior.Execute();

			Assert.That(orders.Attacks, Is.EqualTo(2));
			Assert.That(orders.Moves, Is.Zero);
			Assert.That(orders.Cell, Is.EqualTo(new CPos(4, 0)));
		}

		[Test]
		public void KiteUsesOneSharedMoveToCurrentSafeFiringCell()
		{
			var safeCell = new CPos(2, 0);
			var world = new KiteWorld
			{
				Snapshot = KiteSnapshot(new CPos(0, 0), new CPos(7, 0),
					new[] { safeCell }, new uint[] { 1, 2 })
			};
			var threat = new KiteThreat { Approved = facts => facts.PlannedCell == safeCell };
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders);

			var result = behavior.Execute();

			Assert.That(result.Phase, Is.EqualTo(StealthKitePhase.Position));
			Assert.That(orders.Moves, Is.EqualTo(1));
			Assert.That(orders.Actors, Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(orders.Cell, Is.EqualTo(safeCell));
			Assert.That(threat.Facts.All(facts => facts.FormationRadiusCells == 0), Is.True);
		}

		[Test]
		public void KiteUsesOneRepresentativeLivePositionForAGroupedAttack()
		{
			var target = new StealthKiteActorSnapshot(71, "harv", new CPos(5, 0),
				100, 100, 1, true, true, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1, new[]
				{
					new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5),
					new StealthKiteMemberSnapshot(2, new CPos(0, 1), 5)
				}, new[] { target }, new[] { new CPos(0, 0) }, true)
			};
			var threat = new KiteThreat { Approved = facts => facts.PlannedCell.X == 0 };
			var orders = new KiteOrders();

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				threat, orders).Execute();

			Assert.That(result.Phase, Is.EqualTo(StealthKitePhase.Fire));
			Assert.That(orders.Attacks, Is.EqualTo(1));
			Assert.That(orders.Actors, Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(threat.Facts.Single().FormationRadiusCells, Is.Zero);
			Assert.That(threat.Facts.Single().PlannedCell, Is.EqualTo(new CPos(0, 0)));
		}

		[Test]
		public void KiteUsesTheMemberClosestToTheTargetForSharedOrders()
		{
			var safeCell = new CPos(0, 0);
			var unsafeCell = new CPos(1, 0);
			var fallbackCell = new CPos(-1, 0);
			var target = new StealthKiteActorSnapshot(71, "obli", new CPos(4, 0),
				100, 100, 4, true, true, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1, new[]
				{
					new StealthKiteMemberSnapshot(1, safeCell, 5),
					new StealthKiteMemberSnapshot(2, unsafeCell, 5)
				}, new[] { target }, new[] { fallbackCell }, true)
			};
			var threat = new KiteThreat
			{
				Approved = facts => facts.PlannedCell == fallbackCell
			};
			var orders = new KiteOrders();

			new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders).Execute();

			Assert.That(orders.Attacks, Is.Zero);
			Assert.That(orders.Moves, Is.EqualTo(1));
			Assert.That(orders.Cell, Is.EqualTo(fallbackCell));
			Assert.That(threat.Facts.First().PlannedCell, Is.EqualTo(unsafeCell));
			Assert.That(threat.Facts.First().FormationRadiusCells, Is.Zero);
			Assert.That(threat.Facts.Last().FormationRadiusCells, Is.Zero,
				"one shared Kite order must be planned as one representative stank");
		}

		[Test]
		public void KiteUsesTheMemberClosestToAnyLiveThreatForSharedOrders()
		{
			var target = new StealthKiteActorSnapshot(71, "weap", new CPos(8, 0),
				100, 100, 0, false, true, false, false, false, priorityValue: 1000);
			var nearbyThreat = new StealthKiteActorSnapshot(72, "obli", new CPos(0, 2),
				100, 100, 7, true, false, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1, new[]
				{
					new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5),
					new StealthKiteMemberSnapshot(2, new CPos(6, 0), 5)
				}, new[] { target, nearbyThreat }, Array.Empty<CPos>(), true)
			};
			var threat = new KiteThreat { Approved = facts => false };

			new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				threat, new KiteOrders()).Execute();

			Assert.That(threat.Facts.First().PlannedCell, Is.EqualTo(new CPos(0, 0)));
		}

		[Test]
		public void KiteIncludesLongRangeThreatsWithoutSelectingThemAsLocalTargets()
		{
			var localTarget = new StealthKiteActorSnapshot(71, "mtnk", new CPos(4, 0),
				100, 100, 4, true, true, false, false, false);
			var distantArtillery = new StealthKiteActorSnapshot(72, "mlrs", new CPos(25, 0),
				100, 100, 35, true, false, false, false, false,
				isInLocalEngagementArea: false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { localTarget, distantArtillery }, Array.Empty<CPos>(), true)
			};
			var threat = new KiteThreat
			{
				Approved = facts => facts.SelectedTargetActorId == 71 &&
					facts.EnemyActorIds.SequenceEqual(new uint[] { 71, 72 })
			};
			var orders = new KiteOrders();

			new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders).Execute();

			Assert.That(orders.Attacks, Is.EqualTo(1));
			Assert.That(threat.Facts.Single().SelectedTargetActorId, Is.EqualTo(71));
			Assert.That(threat.Facts.Single().EnemyActorIds, Is.EqualTo(new uint[] { 71, 72 }));
		}

		[Test]
		public void KiteCrossoverOnlyRatesThreatsCoveringTheChosenAttackArea()
		{
			var target = new StealthKiteActorSnapshot(71, "htnk", new CPos(5, 0),
				100, 100, 4, true, true, false, false, false);
			var covering = new StealthKiteActorSnapshot(72, "mtnk", new CPos(10, 0),
				100, 100, 2, true, false, false, false, false);
			var unrelated = new StealthKiteActorSnapshot(73, "mlrs", new CPos(20, 0),
				100, 100, 2, true, false, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { target, covering, unrelated }, Array.Empty<CPos>(), true)
			};
			var threat = new KiteThreat { Approved = facts => false };

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				threat, new KiteOrders()).Execute();

			Assert.That(threat.FallbackFacts.EnemyActorIds, Is.EqualTo(new uint[] { 71, 72 }));
			var controller = Construct<StealthLifecycleController>(BehaviorId.Kite);
			Assert.That(controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.RecalculateFleeEntry.Evidence.EnemyActorIds,
				Is.EqualTo(new uint[] { 71, 72 }));
		}

		[Test]
		public void KiteMovesBeforeDecloakingInsideAMediumTanksLiveRange()
		{
			var currentCell = new CPos(0, 0);
			var safeCell = new CPos(-2, 0);
			var mediumTank = new StealthKiteActorSnapshot(71, "mtnk", new CPos(3, 0),
				100, 100, 4, true, true, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, currentCell, 5) },
					new[] { mediumTank }, new[] { safeCell }, true)
			};
			var threat = new KiteThreat { Approved = facts => facts.PlannedCell == safeCell };
			var orders = new KiteOrders();

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				threat, orders).Execute();

			Assert.That(result.Phase, Is.EqualTo(StealthKitePhase.Position));
			Assert.That(orders.Attacks, Is.Zero);
			Assert.That(orders.Moves, Is.EqualTo(1));
			Assert.That(orders.Cell, Is.EqualTo(safeCell));
		}

		[Test]
		public void KiteOnlyChecksCellsThatCanFireOnTheSelectedTarget()
		{
			var currentCell = new CPos(0, 0);
			var unrelatedNearbyCell = new CPos(1, 0);
			var targetFiringCell = new CPos(5, 0);
			var target = new StealthKiteActorSnapshot(71, "htnk", new CPos(10, 0),
				100, 100, 4, true, true, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, currentCell, 5) },
					new[] { target }, new[] { unrelatedNearbyCell, targetFiringCell }, true)
			};
			var threat = new KiteThreat { Approved = facts => facts.PlannedCell != currentCell };
			var orders = new KiteOrders();

			new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders).Execute();

			Assert.That(orders.Moves, Is.EqualTo(1));
			Assert.That(orders.Cell, Is.EqualTo(targetFiringCell));
			Assert.That(threat.Facts.Select(facts => facts.PlannedCell),
				Does.Not.Contain(unrelatedNearbyCell));
		}

		[Test]
		public void KiteDoesNotReissueAnUnreachableFiringCellAfterMovementEnds()
		{
			var safeCell = new CPos(2, 0);
			var world = new KiteWorld
			{
				Snapshot = KiteSnapshot(new CPos(0, 0), new CPos(7, 0), new[] { safeCell })
			};
			var threat = new KiteThreat { Approved = facts => facts.PlannedCell == safeCell };
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders);

			behavior.Execute();
			behavior.Execute();
			world.Snapshot = new StealthKiteLiveSnapshot(2,
				new[]
				{
					new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5,
						needsMovementOrder: true)
				},
				new[]
				{
					new StealthKiteActorSnapshot(71, "harv", new CPos(7, 0), 100, 100,
						1, true, true, false, false, false)
				}, new[] { safeCell }, true);
			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.RecalculateFlee));
			Assert.That(orders.Moves, Is.EqualTo(1));
		}

		[Test]
		public void KiteStopsRepositioningAfterOneCompassSearchWithoutFiring()
		{
			var candidates = Enumerable.Range(1, 9).Select(x => new CPos(x, 1)).ToArray();
			var target = new StealthKiteActorSnapshot(71, "mtnk", new CPos(6, 0),
				100, 100, 4, true, true, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[]
					{
						new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5,
							needsMovementOrder: true)
					}, new[] { target }, candidates, true)
			};
			var threat = new KiteThreat
			{
				Approved = facts => facts.PlannedCell.Y == 1,
				FallbackCrossover = 3
			};
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders);

			StealthKiteResult result = null;
			for (var i = 0; i < 9; i++)
				result = behavior.Execute();

			Assert.That(orders.Moves, Is.EqualTo(8));
			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.MassAttack));
		}

		[Test]
		public void KiteFiresWhenTheRepresentativeArrivesBeforeTheRestOfTheGroup()
		{
			var safeCell = new CPos(1, 0);
			var target = new StealthKiteActorSnapshot(71, "mtnk", new CPos(6, 0),
				100, 100, 4, true, true, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1, new[]
				{
					new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5),
					new StealthKiteMemberSnapshot(2, new CPos(0, 1), 5)
				}, new[] { target }, new[] { safeCell }, true)
			};
			var threat = new KiteThreat { Approved = facts => facts.PlannedCell == safeCell };
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders);

			behavior.Execute();
			world.Snapshot = new StealthKiteLiveSnapshot(2, new[]
			{
				new StealthKiteMemberSnapshot(1, safeCell, 5, needsMovementOrder: true),
				new StealthKiteMemberSnapshot(2, new CPos(1, 1), 5)
			}, new[] { target }, new[] { safeCell }, true);
			behavior.Execute();

			Assert.That(orders.Moves, Is.EqualTo(1));
			Assert.That(orders.Attacks, Is.EqualTo(1));
		}

		[Test]
		public void KiteNeverChoosesACellOccupiedByAnotherSquadMember()
		{
			var occupied = new CPos(1, 0);
			var safeCell = new CPos(12, 0);
			var target = new StealthKiteActorSnapshot(71, "harv", new CPos(7, 0), 100, 100,
				1, true, true, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1, new[]
				{
					new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5),
					new StealthKiteMemberSnapshot(2, occupied, 5)
				}, new[] { target }, new[] { occupied, safeCell }, true)
			};
			var threat = new KiteThreat
			{
				Approved = facts => facts.PlannedCell != new CPos(0, 0) && facts.PlannedCell != occupied
			};
			var orders = new KiteOrders();

			new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders).Execute();

			Assert.That(orders.Cell, Is.EqualTo(safeCell));
		}

		[Test]
		public void KiteRanksSharedMovementFromTheLiveSquadCenter()
		{
			var centerCandidate = new CPos(5, -5);
			var flankCandidate = new CPos(0, -5);
			var target = new StealthKiteActorSnapshot(71, "harv", new CPos(10, 5),
				100, 100, 1, true, true, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1, new[]
				{
					new StealthKiteMemberSnapshot(1, new CPos(0, 0), 15),
					new StealthKiteMemberSnapshot(2, new CPos(10, 0), 15)
				}, new[] { target }, new[] { flankCandidate, centerCandidate }, true)
			};
			var threat = new KiteThreat
			{
				Approved = facts => facts.PlannedCell == flankCandidate ||
					facts.PlannedCell == centerCandidate
			};
			var orders = new KiteOrders();

			new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders).Execute();

			Assert.That(orders.Cell, Is.EqualTo(centerCandidate));
			Assert.That(threat.Facts.Last().FormationRadiusCells, Is.Zero,
				"dispersed members must still use one representative-stank plan");
		}

		[Test]
		public void KiteRejectsASafeCellThatTheRepresentativeCannotReach()
		{
			var blocked = new CPos(1, 0);
			var reachable = new CPos(2, 0);
			var world = new KiteWorld
			{
				Snapshot = KiteSnapshot(new CPos(0, 0), new CPos(7, 0),
					new[] { blocked, reachable }),
				Reachable = (target, cell) => cell != blocked
			};
			var threat = new KiteThreat { Approved = facts => facts.PlannedCell != new CPos(0, 0) };
			var orders = new KiteOrders();

			new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders).Execute();

			Assert.That(orders.Moves, Is.EqualTo(1));
			Assert.That(orders.Cell, Is.EqualTo(reachable));
		}

		[Test]
		public void KiteChoosesTheTargetClosestToTheLiveSquadCenter()
		{
			var flankTarget = new StealthKiteActorSnapshot(71, "mtnk", new CPos(0, 4),
				100, 100, 4, true, false, false, false, false);
			var centerTarget = new StealthKiteActorSnapshot(72, "htnk", new CPos(6, 0),
				100, 100, 4, true, true, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1, new[]
				{
					new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5),
					new StealthKiteMemberSnapshot(2, new CPos(10, 0), 5)
				}, new[] { flankTarget, centerTarget }, Array.Empty<CPos>(), true)
			};

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				new KiteThreat(), new KiteOrders()).Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(72));
		}

		[Test]
		public void KiteYieldsCrushableInfantryAfterHigherThreatTargetsAreGone()
		{
			var infantry = new StealthKiteActorSnapshot(71, "e3", new CPos(2, 0),
				100, 100, 4, true, true, true, true, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { infantry }, Array.Empty<CPos>(), true,
					minimumKitePriorityValue: 1)
			};
			var orders = new KiteOrders();

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				new KiteThreat(), orders).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.CrushEvaluation));
			Assert.That(result.SelectedTargetActorId, Is.Null);
			Assert.That(orders.Attacks + orders.Moves, Is.Zero);
		}

		[Test]
		public void KiteHandlesInfantryRejectedByCrushSafetyWithoutCyclingBack()
		{
			var infantry = new StealthKiteActorSnapshot(71, "e3", new CPos(2, 0),
				100, 100, 4, true, false, true, true, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { infantry }, Array.Empty<CPos>(), true)
			};
			var handoff = Construct<StealthKiteHandoff>(Handoff(BehaviorId.Kite),
				Mission(), new uint[] { 71 }, (uint?)71);
			var orders = new KiteOrders();

			var result = new StealthKiteBehavior(handoff, new Guard(), world,
				new KiteThreat(), orders).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.Retain));
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(71));
			Assert.That(orders.Attacks, Is.EqualTo(1));
		}

		[Test]
		public void KiteDoesNotReturnARejectedCrushTargetToCrushAgain()
		{
			var infantry = new StealthKiteActorSnapshot(71, "e3", new CPos(7, 0),
				100, 100, 4, true, false, true, true, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { infantry }, Array.Empty<CPos>(), true)
			};
			var handoff = Construct<StealthKiteHandoff>(Handoff(BehaviorId.Kite),
				Mission(), new uint[] { 71 }, (uint?)71);
			var threat = new KiteThreat { Approved = facts => false, FallbackCrossover = 3 };

			var result = new StealthKiteBehavior(handoff, new Guard(), world,
				threat, new KiteOrders()).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.MassAttack));
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(71));
		}

		[Test]
		public void KiteAttacksAReachableMissionFallbackAfterCrushCannotReachInfantry()
		{
			var infantry = new StealthKiteActorSnapshot(71, "e3", new CPos(7, 0),
				100, 100, 4, true, true, true, true, false);
			var wall = new StealthKiteActorSnapshot(72, "brik", new CPos(3, 0),
				100, 100, 0, false, true, false, false, false,
				priorityValue: 150);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { infantry, wall }, Array.Empty<CPos>(), true,
					minimumKitePriorityValue: 250000)
			};
			var handoff = Construct<StealthKiteHandoff>(Handoff(BehaviorId.Kite),
				Mission(), new uint[] { 71 }, (uint?)71);
			var threat = new KiteThreat
			{
				Approved = facts => facts.SelectedTargetActorId == 72
			};
			var orders = new KiteOrders();

			var behavior = new StealthKiteBehavior(handoff, new Guard(), world, threat, orders);
			var result = behavior.Execute();
			var retained = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.Retain));
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(72));
			Assert.That(retained.SelectedTargetActorId, Is.EqualTo(72));
			Assert.That(orders.Attacks, Is.EqualTo(1));
		}

		[Test]
		public void KiteAttacksAReachableMissionWallWhenNoValuableTargetIsSafe()
		{
			var tank = new StealthKiteActorSnapshot(71, "mtnk", new CPos(7, 0),
				100, 100, 4, true, true, false, false, false);
			var wall = new StealthKiteActorSnapshot(72, "brik", new CPos(3, 0),
				100, 100, 0, false, false, false, false, false,
				priorityValue: 150);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { tank, wall }, Array.Empty<CPos>(), true,
					minimumKitePriorityValue: 250000)
			};
			var threat = new KiteThreat
			{
				Approved = facts => facts.SelectedTargetActorId == 72
			};
			var orders = new KiteOrders();

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				threat, orders).Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(72));
			Assert.That(orders.Attacks, Is.EqualTo(1));
		}

		[Test]
		public void KiteDropsARetainedWallWhenAValidEconomicTargetAppears()
		{
			var infantry = new StealthKiteActorSnapshot(71, "e3", new CPos(7, 0),
				100, 100, 4, true, true, true, true, false);
			var wall = new StealthKiteActorSnapshot(72, "brik", new CPos(3, 0),
				100, 100, 0, false, true, false, false, false,
				priorityValue: 150);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { infantry, wall }, Array.Empty<CPos>(), true,
					minimumKitePriorityValue: 250000)
			};
			var handoff = Construct<StealthKiteHandoff>(Handoff(BehaviorId.Kite),
				Mission(), new uint[] { 71 }, (uint?)71);
			var threat = new KiteThreat
			{
				Approved = facts => facts.SelectedTargetActorId != 71
			};
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(handoff, new Guard(), world, threat, orders);

			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(72));
			var advancedPower = new StealthKiteActorSnapshot(73, "apwr", new CPos(4, 0),
				100, 100, 0, false, true, false, false, false, priorityValue: 1000000);
			world.Snapshot = new StealthKiteLiveSnapshot(2,
				new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
				new[] { infantry, wall, advancedPower }, Array.Empty<CPos>(), true,
				minimumKitePriorityValue: 250000);
			var result = behavior.Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(73));
			Assert.That(orders.Attacks, Is.EqualTo(2));
		}

		[Test]
		public void KiteFinishesTheWallBlockingItsLiveTarget()
		{
			var target = new StealthKiteActorSnapshot(71, "mtnk", new CPos(6, 0),
				100, 100, 4, true, true, false, false, false);
			var wall = new StealthKiteActorSnapshot(72, "brik", new CPos(3, 0),
				100, 100, 0, false, true, false, false, false, priorityValue: 150);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { target, wall }, Array.Empty<CPos>(), true,
					minimumKitePriorityValue: 250000),
				Blocker = (actorId, cell) => actorId == 71 ? (uint?)72 : null
			};
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				new KiteThreat(), orders);

			var first = behavior.Execute();
			world.Blocker = (actorId, cell) => null;
			var retained = behavior.Execute();

			Assert.That(first.SelectedTargetActorId, Is.EqualTo(72));
			Assert.That(retained.SelectedTargetActorId, Is.EqualTo(72));
			Assert.That(retained.Disposition, Is.EqualTo(StealthKiteDisposition.Retain));
			Assert.That(orders.Attacks, Is.EqualTo(1));
		}

		[Test]
		public void KiteLeavesABlockingWallForANewCloserLiveThreat()
		{
			var target = new StealthKiteActorSnapshot(71, "mtnk", new CPos(6, 0),
				100, 100, 4, true, true, false, false, false);
			var wall = new StealthKiteActorSnapshot(72, "brik", new CPos(3, 0),
				100, 100, 0, false, true, false, false, false, priorityValue: 150);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { target, wall }, Array.Empty<CPos>(), true,
					minimumKitePriorityValue: 250000),
				Blocker = (actorId, cell) => actorId == target.ActorId ? (uint?)wall.ActorId : null
			};
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				new KiteThreat(), orders);

			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(wall.ActorId));
			var closer = new StealthKiteActorSnapshot(73, "htnk", new CPos(1, 0),
				100, 100, 4, true, false, false, false, false);
			world.Blocker = (actorId, cell) => null;
			world.Snapshot = new StealthKiteLiveSnapshot(2,
				new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
				new[] { target, wall, closer }, Array.Empty<CPos>(), true,
				minimumKitePriorityValue: 250000);

			var result = behavior.Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(closer.ActorId));
			Assert.That(orders.Cell, Is.EqualTo(closer.CurrentCell));
		}

		[Test]
		public void KiteTriesAnotherLiveKiteTargetBeforeALowPriorityFallback()
		{
			var retained = new StealthKiteActorSnapshot(71, "e3", new CPos(7, 0),
				100, 100, 4, true, true, true, true, false);
			var tank = new StealthKiteActorSnapshot(72, "mtnk", new CPos(6, 0),
				100, 100, 4, true, true, false, false, false);
			var wall = new StealthKiteActorSnapshot(73, "brik", new CPos(3, 0),
				100, 100, 0, false, true, false, false, false, priorityValue: 150);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { retained, tank, wall }, Array.Empty<CPos>(), true,
					minimumKitePriorityValue: 250000)
			};
			var handoff = Construct<StealthKiteHandoff>(Handoff(BehaviorId.Kite),
				Mission(), new uint[] { 71, 72 }, (uint?)71);
			var threat = new KiteThreat { Approved = facts => facts.SelectedTargetActorId != 71 };
			var orders = new KiteOrders();

			var result = new StealthKiteBehavior(handoff, new Guard(), world, threat, orders).Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(72));
			Assert.That(orders.Attacks, Is.EqualTo(1));
		}

		[Test]
		public void KiteReacquiresAfterItsRetainedTargetDies()
		{
			var first = new StealthKiteActorSnapshot(71, "mtnk", new CPos(4, 0),
				100, 100, 4, true, true, false, false, false);
			var second = new StealthKiteActorSnapshot(72, "htnk", new CPos(6, 0),
				100, 100, 4, true, false, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { first, second }, Array.Empty<CPos>(), true)
			};
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				new KiteThreat(), orders);

			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(71));
			world.Snapshot = new StealthKiteLiveSnapshot(2,
				new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
				new[] { second }, Array.Empty<CPos>(), true);
			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.Reacquire));
			Assert.That(result.SelectedTargetActorId, Is.Null);
			Assert.That(orders.Attacks, Is.EqualTo(1));
		}

		[Test]
		public void KiteReturnsToTheCloserLocalTargetWhenItsTargetLeavesTheEngagementArea()
		{
			var target = new StealthKiteActorSnapshot(71, "mtnk", new CPos(4, 0),
				100, 100, 4, true, true, false, false, false);
			var alternative = new StealthKiteActorSnapshot(72, "htnk", new CPos(6, 0),
				100, 100, 4, true, false, false, false, false);
			var member = new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1, new[] { member },
					new[] { target, alternative }, Array.Empty<CPos>(), true)
			};
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				new KiteThreat(), new KiteOrders());

			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(71));
			world.Snapshot = new StealthKiteLiveSnapshot(2, new[] { member },
				new[]
				{
					new StealthKiteActorSnapshot(71, "mtnk", new CPos(7, 0),
						100, 100, 4, true, false, false, false, false,
						isInLocalEngagementArea: false),
					alternative
				}, Array.Empty<CPos>(), true);

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.Retain));
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(72));
		}

		[Test]
		public void DetectedKiteContinuesLiveCombatWhenItsMissionTargetDies()
		{
			var first = new StealthKiteActorSnapshot(71, "mtnk", new CPos(4, 0),
				100, 100, 4, true, true, false, false, false);
			var second = new StealthKiteActorSnapshot(72, "obli", new CPos(6, 0),
				100, 100, 4, true, false, false, false, true);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { first, second }, Array.Empty<CPos>(), false)
			};
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				new KiteThreat(), orders);

			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(71));
			world.Snapshot = new StealthKiteLiveSnapshot(2,
				new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
				new[] { second }, Array.Empty<CPos>(), true, formationDetected: true);
			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.Retain));
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(72));
			Assert.That(orders.Attacks, Is.EqualTo(2));
		}

		[Test]
		public void KitePrefersAThreatVehicleBeforeCrushableInfantry()
		{
			var infantry = new StealthKiteActorSnapshot(71, "e3", new CPos(1, 0),
				100, 100, 4, true, false, true, true, false);
			var tank = new StealthKiteActorSnapshot(72, "mtnk", new CPos(4, 0),
				100, 100, 4, true, true, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { infantry, tank }, Array.Empty<CPos>(), true)
			};

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				new KiteThreat(), new KiteOrders()).Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(72));
		}

		[Test]
		public void KiteTriesTheNextClosestThreatBeforeGivingUp()
		{
			var blocked = new StealthKiteActorSnapshot(71, "mtnk", new CPos(2, 0),
				100, 100, 4, true, false, false, false, false);
			var kiteable = new StealthKiteActorSnapshot(72, "htnk", new CPos(4, 0),
				100, 100, 4, true, true, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { blocked, kiteable }, Array.Empty<CPos>(), true)
			};
			var threat = new KiteThreat { Approved = facts => facts.SelectedTargetActorId == 72 };

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				threat, new KiteOrders()).Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(72));
			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.Retain));
		}

		[Test]
		public void KiteDoesNotAbandonARetainedThreatForAnotherSafeTarget()
		{
			var retained = new StealthKiteActorSnapshot(71, "htnk", new CPos(4, 0),
				100, 100, 4, true, true, false, false, false);
			var alternative = new StealthKiteActorSnapshot(72, "mtnk", new CPos(6, 0),
				100, 100, 4, true, false, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { retained, alternative }, Array.Empty<CPos>(), true)
			};
			var threat = new KiteThreat
			{
				Approved = facts => facts.SelectedTargetActorId == 71,
				FallbackCrossover = 3
			};
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				threat, orders);

			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(71));
			threat.Approved = facts => facts.SelectedTargetActorId == 72;
			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.MassAttack));
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(71));
			Assert.That(orders.Attacks, Is.EqualTo(1));
		}

		[Test]
		public void KiteKeepsItsLiveTargetWhenAnotherThreatBecomesCloser()
		{
			var retained = new StealthKiteActorSnapshot(71, "htnk", new CPos(4, 0),
				100, 100, 4, true, true, false, false, false);
			var alternative = new StealthKiteActorSnapshot(72, "mtnk", new CPos(6, 0),
				100, 100, 4, true, false, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { retained, alternative }, Array.Empty<CPos>(), true)
			};
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				new KiteThreat(), orders);

			Assert.That(behavior.Execute().SelectedTargetActorId, Is.EqualTo(retained.ActorId));
			var retreating = new StealthKiteActorSnapshot(71, "htnk", new CPos(8, 0),
				100, 100, 4, true, true, false, false, false);
			var closer = new StealthKiteActorSnapshot(72, "mtnk", new CPos(2, 0),
				100, 100, 4, true, false, false, false, false);
			world.Snapshot = new StealthKiteLiveSnapshot(2,
				new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
				new[] { retreating, closer }, Array.Empty<CPos>(), true);

			var result = behavior.Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(retreating.ActorId));
			Assert.That(orders.Attacks, Is.EqualTo(1));
		}

		[Test]
		public void KiteKeepsAnUnfiredPositioningTargetUntilItYields()
		{
			var candidate = new CPos(2, 0);
			var first = new StealthKiteActorSnapshot(71, "htnk", new CPos(7, 0),
				100, 100, 4, true, true, false, false, false);
			var alternative = new StealthKiteActorSnapshot(72, "mtnk", new CPos(8, 0),
				100, 100, 4, true, false, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { first, alternative }, new[] { candidate }, true)
			};
			var threat = new KiteThreat
			{
				Approved = facts => facts.SelectedTargetActorId == 71 && facts.PlannedCell == candidate
			};
			var orders = new KiteOrders();
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders);

			Assert.That(behavior.Execute().Phase, Is.EqualTo(StealthKitePhase.Position));
			threat.Approved = facts => facts.SelectedTargetActorId == 72;
			var replacement = behavior.Execute();

			Assert.That(replacement.Disposition, Is.EqualTo(StealthKiteDisposition.RecalculateFlee));
			Assert.That(replacement.SelectedTargetActorId, Is.EqualTo(71));
			Assert.That(orders.Attacks, Is.EqualTo(0));
		}

		[Test]
		public void KiteHandsOffByLiveCrossoverWhenNoSafeActionExists()
		{
			var world = new KiteWorld { Snapshot = KiteSnapshot(new CPos(0, 0), new CPos(7, 0)) };
			var threat = new KiteThreat { Approved = facts => false, FallbackCrossover = 3 };
			var behavior = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				threat, new KiteOrders());

			Assert.That(behavior.Execute().Disposition, Is.EqualTo(StealthKiteDisposition.MassAttack));
			threat.FallbackCrossover = 2;
			Assert.That(behavior.Execute().Disposition,
				Is.EqualTo(StealthKiteDisposition.RecalculateFlee));
		}

		[Test]
		public void CoordinatedKiteHandsItsCanonicalNoSafePlanToMassAttack()
		{
			var world = new KiteWorld { Snapshot = KiteSnapshot(new CPos(0, 0), new CPos(7, 0)) };
			var threat = new KiteThreat { Approved = facts => false, FallbackCrossover = 1.5 };
			var controller = Construct<StealthLifecycleController>(BehaviorId.Kite);
			var handoff = Construct<StealthKiteHandoff>(
				controller.CurrentHandoff, Mission(), new uint[] { 71 });
			var behavior = new StealthKiteBehavior(handoff, controller,
				world, threat, new KiteOrders(), _ => true);

			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.MassAttack));
			Assert.That(result.FallbackEvidence.CoordinatedMassAttack, Is.True);
			Assert.That(controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.MassAttackEntry.Evidence.StandardScore.Crossover, Is.EqualTo(1.5));
			Assert.That(transition.MassAttackEntry.Evidence.CoordinatedMassAttack, Is.True);
		}

		[Test]
		public void CoordinatedKitePrefersMassAttackToALowPriorityFallbackObjective()
		{
			var threat = new StealthKiteActorSnapshot(71, "htnk", new CPos(7, 0),
				100, 100, 4, true, true, false, false, false);
			var wall = new StealthKiteActorSnapshot(72, "brik", new CPos(3, 0),
				100, 100, 0, false, false, false, false, false, priorityValue: 1);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { threat, wall }, Array.Empty<CPos>(), true)
			};
			var calculator = new KiteThreat
			{
				Approved = facts => facts.SelectedTargetActorId == wall.ActorId,
				FallbackCrossover = .5
			};

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				calculator, new KiteOrders(), _ => true).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.MassAttack));
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(threat.ActorId));
		}

		[Test]
		public void CoordinatedKiteDoesNotMassAttackAnIncidentalTargetOutsideItsProvince()
		{
			var remoteThreat = new StealthKiteActorSnapshot(71, "obli", new CPos(30, 30),
				100, 100, 7, true, true, false, false, false);
			var wall = new StealthKiteActorSnapshot(72, "brik", new CPos(29, 30),
				100, 100, 0, false, false, false, false, false, priorityValue: 1);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(25, 30), 5) },
					new[] { remoteThreat, wall }, Array.Empty<CPos>(), true)
			};
			var calculator = new KiteThreat
			{
				Approved = facts => facts.SelectedTargetActorId == wall.ActorId,
				FallbackCrossover = .5
			};

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				calculator, new KiteOrders(), cell => cell.X < 10).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.Retain));
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(wall.ActorId));
		}

		[Test]
		public void DisabledKitingUsesCrossoverHandoffWithoutIssuingOrders()
		{
			var snapshot = KiteSnapshot(new CPos(0, 0), new CPos(7, 0));
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(snapshot.Tick, snapshot.Members,
					snapshot.Actors, snapshot.CandidateCells, snapshot.FormationCloaked,
					formationDetected: snapshot.FormationDetected, kitingEnabled: false)
			};
			var threat = new KiteThreat { Approved = facts => true, FallbackCrossover = 3 };
			var orders = new KiteOrders();

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				threat, orders).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.MassAttack));
			Assert.That(orders.Attacks, Is.Zero);
			Assert.That(orders.Moves, Is.Zero);
			Assert.That(threat.Facts, Is.Empty);
		}

		[Test]
		public void KiteHandsAnUnsafeExposedPositionToLeastDangerousFlee()
		{
			var safeCell = new CPos(-1, 0);
			var target = new StealthKiteActorSnapshot(71, "obli", new CPos(4, 0),
				100, 100, 4, true, true, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { target }, new[] { safeCell }, false,
					currentPositionSafe: false)
			};
			var threat = new KiteThreat
			{
				Approved = facts => facts.PlannedCell == safeCell,
				FallbackCrossover = 4
			};
			var orders = new KiteOrders();

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				threat, orders).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.RecalculateFlee));
			Assert.That(result.FallbackEvidence.Reason,
				Is.EqualTo(StealthKiteFallbackReason.UnsafeCurrentPosition));
			Assert.That(result.FallbackEvidence.AttackScore.Value.Crossover, Is.EqualTo(4));
			Assert.That(orders.Moves, Is.Zero);
		}

		[Test]
		public void KiteOwnsAnInfantryTargetAfterCrushHandsItOff()
		{
			var target = new StealthKiteActorSnapshot(71, "e1", new CPos(4, 0),
				100, 100, 1, true, true, true, true, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { target }, Array.Empty<CPos>(), true)
			};
			var orders = new KiteOrders();

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				new KiteThreat(), orders).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.Retain));
			Assert.That(orders.Attacks, Is.EqualTo(1));
		}

		[Test]
		public void KiteMaySelectACloserHighValueEconomicObjectiveWhileRespectingItsGuard()
		{
			var harvester = new StealthKiteActorSnapshot(71, "harv", new CPos(4, 0),
				100, 100, 0, false, true, false, false, false, priorityValue: 5500000);
			var guard = new StealthKiteActorSnapshot(72, "mtnk", new CPos(6, 0),
				100, 100, 4, true, false, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { harvester, guard }, Array.Empty<CPos>(), true,
					minimumKitePriorityValue: 250000)
			};
			var threat = new KiteThreat();

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				threat, new KiteOrders()).Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(71));
			Assert.That(threat.Facts.Single().EnemyActorIds, Is.EqualTo(new uint[] { 71, 72 }),
				"The economic target's live guard must remain part of the safety calculation.");
		}

		[Test]
		public void UnsafeEconomicKiteTargetAttributesFleeToItsClosestLiveGuard()
		{
			var harvester = new StealthKiteActorSnapshot(71, "harv", new CPos(2, 0),
				100, 100, 0, false, true, false, false, false, priorityValue: 5500000);
			var guard = new StealthKiteActorSnapshot(72, "mtnk", new CPos(4, 0),
				100, 100, 4, true, false, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { harvester, guard }, Array.Empty<CPos>(), false,
					minimumKitePriorityValue: 250000, currentPositionSafe: false)
			};
			var threat = new KiteThreat { Approved = facts => false, FallbackCrossover = 1 };
			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				threat, new KiteOrders()).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.RecalculateFlee));
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(72));
			Assert.That(result.LiveDefenderActorIds, Is.EqualTo(new uint[] { 72 }));
			Assert.That(result.FallbackEvidence.AttackFacts.EnemyActorIds,
				Is.EqualTo(new uint[] { 72 }));
			var controller = Construct<StealthLifecycleController>(BehaviorId.Kite);
			Assert.That(controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.RecalculateFleeEntry.Evidence.EnemyActorIds,
				Is.EqualTo(new uint[] { 72 }));
		}

		[Test]
		public void UnsafeEconomicKiteTargetCanMassAttackItsLiveGuard()
		{
			var harvester = new StealthKiteActorSnapshot(71, "harv", new CPos(2, 0),
				100, 100, 0, false, true, false, false, false, priorityValue: 5500000);
			var guard = new StealthKiteActorSnapshot(72, "mtnk", new CPos(4, 0),
				100, 100, 4, true, false, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { harvester, guard }, Array.Empty<CPos>(), true,
					minimumKitePriorityValue: 250000)
			};
			var threat = new KiteThreat { Approved = facts => false, FallbackCrossover = 3 };
			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				threat, new KiteOrders()).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.MassAttack));
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(71));
			Assert.That(result.LiveDefenderActorIds, Is.EqualTo(new uint[] { 72 }));
			Assert.That(result.FallbackEvidence.AttackFacts.EnemyActorIds,
				Is.EqualTo(new uint[] { 71, 72 }));
			var controller = Construct<StealthLifecycleController>(BehaviorId.Kite);
			Assert.That(controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.MassAttackEntry, Is.Not.Null);
		}

		[Test]
		public void KiteReturnsAnUnguardedHighValueEconomicObjectiveToUndefendedAttack()
		{
			var harvester = new StealthKiteActorSnapshot(71, "harv", new CPos(4, 0),
				100, 100, 0, false, true, false, false, false, priorityValue: 5500000);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { harvester }, Array.Empty<CPos>(), true,
					minimumKitePriorityValue: 250000)
			};
			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				new KiteThreat(), new KiteOrders()).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthKiteDisposition.UndefendedAttack));
		}

		[Test]
		public void KiteDoesNotSelectAnEconomicObjectiveBelowTheConfiguredFloor()
		{
			var lowValueTarget = new StealthKiteActorSnapshot(71, "sam", new CPos(4, 0),
				100, 100, 0, false, true, false, false, false, priorityValue: 500);
			var guard = new StealthKiteActorSnapshot(72, "mtnk", new CPos(6, 0),
				100, 100, 4, true, false, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { lowValueTarget, guard }, Array.Empty<CPos>(), true,
					minimumKitePriorityValue: 250000)
			};

			var result = new StealthKiteBehavior(KiteHandoff(), new Guard(), world,
				new KiteThreat(), new KiteOrders()).Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(72));
		}

		[Test]
		public void CrushUsesLiveTargetCellAndRejectsActualDetectorCoverage()
		{
			var world = new CrushWorld { Snapshot = CrushSnapshot(new CPos(5, 0), false) };
			var threat = new CrushThreat();
			var orders = new CrushOrders();
			var behavior = new StealthCrushBehavior(CrushHandoff(), new Guard(), world, threat, orders);

			Assert.That(behavior.Execute().Disposition, Is.EqualTo(StealthCrushDisposition.Retain));
			world.Snapshot = CrushSnapshot(new CPos(7, 0), false);
			behavior.Execute();
			world.Snapshot = CrushSnapshot(new CPos(7, 0), true);

			Assert.That(behavior.Execute().Disposition, Is.EqualTo(StealthCrushDisposition.Kite));
			Assert.That(orders.Cells, Is.EqualTo(new[] { new CPos(5, 0), new CPos(7, 0) }));
			Assert.That(threat.LastFacts.HasDetectorCoverage, Is.True);
		}

		[Test]
		public void CrushPursuesLiveInfantryAcrossTheLocalBattle()
		{
			var world = new CrushWorld { Snapshot = CrushSnapshot(new CPos(8, 0), false) };
			var orders = new CrushOrders();

			var result = new StealthCrushBehavior(CrushHandoff(), new Guard(), world,
				new CrushThreat(), orders).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthCrushDisposition.Retain));
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(71));
			Assert.That(orders.Cells, Is.EqualTo(new[] { new CPos(8, 0) }));
		}

		[Test]
		public void CrushHandsAnUnreachableInfantryTargetToKiteWithoutReissuing()
		{
			var world = new CrushWorld { Snapshot = CrushSnapshot(new CPos(5, 0), false) };
			var orders = new CrushOrders();
			var behavior = new StealthCrushBehavior(CrushHandoff(), new Guard(), world,
				new CrushThreat(), orders);

			Assert.That(behavior.Execute().Disposition, Is.EqualTo(StealthCrushDisposition.Retain));
			world.Snapshot = CrushSnapshot(new CPos(5, 0), false, needsMovementOrder: true);
			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthCrushDisposition.Kite));
			Assert.That(result.SelectedTargetActorId, Is.EqualTo(71));
			Assert.That(result.Safety, Is.Null);
			Assert.That(orders.Cells, Is.EqualTo(new[] { new CPos(5, 0) }));
		}

		[Test]
		public void CrushRetainsAStationaryLiveInfantryTargetWithoutReissuingItsOrder()
		{
			var world = new CrushWorld { Snapshot = CrushSnapshot(new CPos(5, 0), false) };
			var orders = new CrushOrders();
			var behavior = new StealthCrushBehavior(CrushHandoff(), new Guard(), world,
				new CrushThreat(), orders);

			behavior.Execute();
			behavior.Execute();

			Assert.That(orders.Cells, Is.EqualTo(new[] { new CPos(5, 0) }));
		}

		[Test]
		public void CrushLetsTheMovingPartOfAGroupFinishWithoutReissuingItsOrder()
		{
			var target = new StealthCrushActorSnapshot(71, "e1", new CPos(1, 0),
				new CPos(5, 0), 100, true, false, true, true, false);
			var world = new CrushWorld
			{
				Snapshot = new StealthCrushLiveSnapshot(1, new[]
				{
					new StealthCrushMemberSnapshot(1, new CPos(0, 0)),
					new StealthCrushMemberSnapshot(2, new CPos(0, 1))
				}, new[] { target }, true)
			};
			var orders = new CrushOrders();
			var behavior = new StealthCrushBehavior(CrushHandoff(), new Guard(), world,
				new CrushThreat(), orders);

			behavior.Execute();
			world.Snapshot = new StealthCrushLiveSnapshot(2, new[]
			{
				new StealthCrushMemberSnapshot(1, new CPos(0, 0), needsMovementOrder: true),
				new StealthCrushMemberSnapshot(2, new CPos(1, 1))
			}, new[] { target }, true);
			var result = behavior.Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthCrushDisposition.Retain));
			Assert.That(orders.Cells, Is.EqualTo(new[] { new CPos(5, 0) }));
		}

		[Test]
		public void CrushDoesNotAbandonAnUncaughtSafeInfantryTargetOnATimer()
		{
			var world = new CrushWorld { Snapshot = CrushSnapshot(new CPos(5, 0), false) };
			var behavior = new StealthCrushBehavior(CrushHandoff(), new Guard(), world,
				new CrushThreat(), new CrushOrders());

			for (var i = 0; i < 20; i++)
				Assert.That(behavior.Execute().Disposition, Is.EqualTo(StealthCrushDisposition.Retain));

			Assert.That(behavior.Execute().Disposition, Is.EqualTo(StealthCrushDisposition.Retain));
		}

		[Test]
		public void UndefendedAttackFinishesRetainedLiveTargetBeforeSwitching()
		{
			var world = new UndefendedWorld { Snapshot = UndefendedSnapshot(false) };
			var orders = new UndefendedOrders();
			var behavior = new StealthUndefendedAttackBehavior(UndefendedHandoff(), new Guard(),
				world, new UndefendedThreat(), orders);

			var first = behavior.Execute();
			world.Snapshot = UndefendedSnapshot(true);
			var second = behavior.Execute();

			Assert.That(first.SelectedTargetActorId, Is.EqualTo(71));
			Assert.That(second.SelectedTargetActorId, Is.EqualTo(71));
			Assert.That(orders.Targets, Is.EqualTo(new uint[] { 71 }));
		}

		[Test]
		public void UndefendedAttackHandsArmedCurrentRangeThreatsToLocalCombat()
		{
			var world = new UndefendedWorld { Snapshot = UndefendedSnapshot(false, new uint[] { 90 }) };
			var threat = new UndefendedThreat { Approved = false };
			var orders = new UndefendedOrders();
			var result = new StealthUndefendedAttackBehavior(UndefendedHandoff(), new Guard(),
				world, threat, orders).Execute();

			Assert.That(result.Disposition,
				Is.EqualTo(StealthUndefendedAttackDisposition.CrushEvaluation));
			Assert.That(result.LiveDefenderActorIds, Is.EqualTo(new uint[] { 90 }));
			Assert.That(orders.Targets, Is.Empty);
		}

		[Test]
		public void UndefendedAttackHandsOffWhenItsObjectiveDiesButLiveDefendersRemain()
		{
			var world = new UndefendedWorld
			{
				Snapshot = new StealthUndefendedAttackLiveSnapshot(1,
					new[]
					{
						new StealthUndefendedAttackMemberSnapshot(1, "stnk", 900,
							new CPos(0, 0), 100, 100, 5, true)
					}, Array.Empty<StealthUndefendedAttackTargetSnapshot>(), new uint[] { 90 },
					true, false, true)
			};
			var result = new StealthUndefendedAttackBehavior(UndefendedHandoff(), new Guard(),
				world, new UndefendedThreat(), new UndefendedOrders()).Execute();

			Assert.That(result.Disposition,
				Is.EqualTo(StealthUndefendedAttackDisposition.CrushEvaluation));
			var controller = Construct<StealthLifecycleController>(BehaviorId.UndefendedAttack);
			Assert.That(controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.CrushEvaluation, Is.Not.Null);
		}

		[Test]
		public void UndefendedAttackRetriesItsRetainedTargetAfterTheEngineOrderCompletes()
		{
			var world = new UndefendedWorld { Snapshot = UndefendedSnapshot(false) };
			var orders = new UndefendedOrders();
			var behavior = new StealthUndefendedAttackBehavior(UndefendedHandoff(), new Guard(),
				world, new UndefendedThreat(), orders);

			behavior.Execute();
			world.Snapshot = UndefendedSnapshot(false, membersIdle: true);
			var retained = behavior.Execute();

			Assert.That(retained.Disposition, Is.EqualTo(StealthUndefendedAttackDisposition.Retain));
			Assert.That(orders.Targets, Is.EqualTo(new uint[] { 71, 71 }));
			Assert.That(orders.Revisions, Is.EqualTo(new long[] { 1, 2 }),
				"A completed engine activity must produce a distinct runtime order fingerprint.");
		}

		[Test]
		public void MassAttackIgnoresStaleEntryAndAttacksHighestLiveThreatUntilCrossoverOne()
		{
			var world = new MassWorld { Snapshot = MassSnapshot() };
			var threat = new MassThreat();
			var orders = new MassOrders();
			var behavior = new StealthMassAttackBehavior(MassHandoff(), new Guard(), world, threat, orders);

			var attack = behavior.Execute();
			threat.Crossover = 1;
			var flee = behavior.Execute();

			Assert.That(attack.SelectedTargetActorId, Is.EqualTo(72));
			Assert.That(orders.Target, Is.EqualTo(72));
			Assert.That(orders.Attacks, Is.EqualTo(1));
			Assert.That(flee.Disposition, Is.EqualTo(StealthMassAttackDisposition.RecalculateFlee));
		}

		[Test]
		public void MassAttackChoosesTheClosestOfEqualHighestThreats()
		{
			var world = new MassWorld { Snapshot = MassSnapshot() };
			var orders = new MassOrders();
			var behavior = new StealthMassAttackBehavior(MassHandoff(), new Guard(), world,
				new MassThreat { SelectedThreat = _ => 1 }, orders);

			var result = behavior.Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(71));
			Assert.That(orders.Target, Is.EqualTo(71));
		}

		[Test]
		public void CoordinatedMassAttackKeepsClearingLiveThreatsBelowCrossoverOne()
		{
			var world = new MassWorld { Snapshot = MassSnapshot() };
			var threat = new MassThreat { Crossover = .5 };
			var orders = new MassOrders();
			var behavior = new StealthMassAttackBehavior(MassHandoff(coordinated: true),
				new Guard(), world, threat, orders);

			var attack = behavior.Execute();
			Assert.That(behavior.Execute().Disposition, Is.EqualTo(StealthMassAttackDisposition.Retain));
			world.Snapshot = MassSnapshotWithActors(new[]
			{
				new StealthMassAttackActorSnapshot(71, "e1", new CPos(3, 0),
					100, 100, 1, true, false, false)
			}, tick: 2);
			var result = behavior.Execute();

			Assert.That(attack.Disposition, Is.EqualTo(StealthMassAttackDisposition.Retain));
			Assert.That(result.Disposition, Is.EqualTo(StealthMassAttackDisposition.Retain));
			Assert.That(orders.Target, Is.EqualTo(71));
			Assert.That(orders.Attacks, Is.EqualTo(2));
		}

		[Test]
		public void MassAttackFinishesItsSelectedThreatBeforeReranking()
		{
			var world = new MassWorld { Snapshot = MassSnapshot() };
			var threat = new MassThreat();
			var orders = new MassOrders();
			var behavior = new StealthMassAttackBehavior(MassHandoff(), new Guard(), world, threat, orders);

			var first = behavior.Execute();
			threat.SelectedThreat = facts => 1000 - facts.SelectedTargetActorId;
			var retained = behavior.Execute();
			world.Snapshot = MassSnapshotWithActors(new[]
			{
				new StealthMassAttackActorSnapshot(71, "e1", new CPos(3, 0),
					100, 100, 1, true, false, false)
			}, tick: 2);
			var replacement = behavior.Execute();

			Assert.That(first.SelectedTargetActorId, Is.EqualTo(72));
			Assert.That(retained.SelectedTargetActorId, Is.EqualTo(72));
			Assert.That(replacement.SelectedTargetActorId, Is.EqualTo(71));
			Assert.That(orders.Attacks, Is.EqualTo(2));
		}

		[Test]
		public void MassAttackLetsAnEngineAttackFollowItsMovingTarget()
		{
			var world = new MassWorld { Snapshot = MassSnapshot() };
			var orders = new MassOrders();
			var behavior = new StealthMassAttackBehavior(MassHandoff(), new Guard(), world,
				new MassThreat { Approved = facts => false }, orders);

			behavior.Execute();
			world.Snapshot = MassSnapshotWithActors(new[]
			{
				new StealthMassAttackActorSnapshot(71, "e1", new CPos(3, 0),
					100, 100, 1, true, false, false),
				new StealthMassAttackActorSnapshot(72, "e3", new CPos(5, 0),
					100, 100, 1, true, false, false)
			}, tick: 2);
			behavior.Execute();

			Assert.That(orders.Attacks, Is.EqualTo(1));
			Assert.That(orders.Target, Is.EqualTo(72));
		}

		[Test]
		public void MassAttackMovesToSafeLiveFireCellBeforeAttacking()
		{
			var safeCell = new CPos(2, 0);
			var world = new MassWorld { Snapshot = MassSnapshot(new[] { safeCell }) };
			var threat = new MassThreat
			{
				Approved = facts => facts.PlannedCell == safeCell
			};
			var orders = new MassOrders();
			var result = new StealthMassAttackBehavior(MassHandoff(), new Guard(),
				world, threat, orders).Execute();

			Assert.That(result.Phase, Is.EqualTo(StealthMassAttackPhase.Advance));
			Assert.That(result.ThreatFacts.PlannedCell, Is.EqualTo(safeCell));
			Assert.That(result.ThreatFacts.FormationRadiusCells, Is.Zero);
			Assert.That(orders.Moves, Is.EqualTo(1));
			Assert.That(orders.Cell, Is.EqualTo(safeCell));
			Assert.That(orders.Attacks, Is.Zero);
		}

		[Test]
		public void MassAttackDoesNotDecloakAFormationMateFromAnUnsafeLiveCell()
		{
			var safeCell = new CPos(2, 0);
			var world = new MassWorld
			{
				Snapshot = MassSnapshot(new[] { safeCell },
					memberCells: new[] { new CPos(0, 0), new CPos(1, 0) })
			};
			var threat = new MassThreat
			{
				Approved = facts => facts.PlannedCell != new CPos(1, 0)
			};
			var orders = new MassOrders();

			var result = new StealthMassAttackBehavior(MassHandoff(), new Guard(),
				world, threat, orders).Execute();

			Assert.That(result.Phase, Is.EqualTo(StealthMassAttackPhase.Advance));
			Assert.That(orders.Cell, Is.EqualTo(safeCell));
			Assert.That(orders.Moves, Is.EqualTo(1));
			Assert.That(orders.Attacks, Is.Zero);
		}

		[Test]
		public void MassAttackRanksSharedMovementFromTheLiveSquadCenter()
		{
			var centerCandidate = new CPos(5, 1);
			var flankCandidate = new CPos(1, 1);
			var world = new MassWorld
			{
				Snapshot = MassSnapshot(new[] { flankCandidate, centerCandidate },
					memberCells: new[] { new CPos(0, 0), new CPos(10, 0) })
			};
			var threat = new MassThreat
			{
				Approved = facts => facts.PlannedCell == flankCandidate ||
					facts.PlannedCell == centerCandidate
			};
			var orders = new MassOrders();

			new StealthMassAttackBehavior(MassHandoff(), new Guard(), world, threat, orders).Execute();

			Assert.That(orders.Cell, Is.EqualTo(centerCandidate));
		}

		[Test]
		public void MassAttackPreservesItsSafeInProgressAdvance()
		{
			var firstCell = new CPos(2, 0);
			var replacementCell = new CPos(1, 0);
			var world = new MassWorld { Snapshot = MassSnapshot(new[] { firstCell }) };
			var threat = new MassThreat
			{
				Approved = facts => facts.PlannedCell == firstCell || facts.PlannedCell == replacementCell
			};
			var orders = new MassOrders();
			var behavior = new StealthMassAttackBehavior(MassHandoff(), new Guard(), world, threat, orders);

			behavior.Execute();
			world.Snapshot = MassSnapshot(new[] { replacementCell }, tick: 2);
			var retained = behavior.Execute();

			Assert.That(retained.Phase, Is.EqualTo(StealthMassAttackPhase.Advance));
			Assert.That(retained.ThreatFacts.PlannedCell, Is.EqualTo(firstCell));
			Assert.That(orders.Moves, Is.EqualTo(1));
		}

		[Test]
		public void MassAttackRefreshesItsAdvanceWhenFormationMembershipChanges()
		{
			var safeCell = new CPos(2, 0);
			var world = new MassWorld { Snapshot = MassSnapshot(new[] { safeCell }) };
			var threat = new MassThreat { Approved = facts => facts.PlannedCell == safeCell };
			var orders = new MassOrders();
			var behavior = new StealthMassAttackBehavior(MassHandoff(), new Guard(), world, threat, orders);

			behavior.Execute();
			world.Snapshot = MassSnapshot(new[] { safeCell }, memberCells: new[]
			{
				new CPos(0, 0), new CPos(1, 0)
			}, tick: 2);
			var refreshed = behavior.Execute();

			Assert.That(refreshed.Phase, Is.EqualTo(StealthMassAttackPhase.Advance));
			Assert.That(refreshed.LastOrderToken.ActorIds, Is.EqualTo(new uint[] { 1, 2 }));
			Assert.That(orders.Moves, Is.EqualTo(2));
		}

		[Test]
		public void CrossoverFactorMeansCurrentForceOvermatch()
		{
			Assert.That(GeneralizedCombatCrossover.Overmatch(8, 4, 0.5), Is.EqualTo(4));
			Assert.That(GeneralizedCombatCrossover.Overmatch(2, 8, 2), Is.EqualTo(0.125));
			Assert.That(GeneralizedCombatCrossover.Overmatch(2, 8, double.PositiveInfinity), Is.Zero);
			Assert.That(GeneralizedCombatCrossover.Overmatch(2, 8, 0), Is.EqualTo(double.PositiveInfinity));
		}

		[Test]
		public void CrossoverApprovedMassAttackCommitsWhenNoSafeFiringCellExists()
		{
			var world = new MassWorld { Snapshot = MassSnapshot() };
			var threat = new MassThreat { Approved = facts => false };
			var orders = new MassOrders();

			var result = new StealthMassAttackBehavior(MassHandoff(), new Guard(), world,
				threat, orders).Execute();

			Assert.That(result.Disposition, Is.EqualTo(StealthMassAttackDisposition.Retain));
			Assert.That(result.Phase, Is.EqualTo(StealthMassAttackPhase.Attack));
			Assert.That(orders.Attacks, Is.EqualTo(1));
			Assert.That(orders.Moves, Is.Zero);
		}

		[Test]
		public void MassAttackClearsALiveObjectiveAfterItsThreatAttackCannotAdvance()
		{
			var members = new[]
			{
				new StealthMassAttackMemberSnapshot(1, new CPos(0, 0), 5,
					needsMovementOrder: true)
			};
			var actors = new[]
			{
				new StealthMassAttackActorSnapshot(71, "e1", new CPos(6, 0),
					100, 100, 1, true, false, false),
				new StealthMassAttackActorSnapshot(72, "e3", new CPos(7, 0),
					100, 100, 1, true, false, false),
				new StealthMassAttackActorSnapshot(73, "brik", new CPos(3, 0),
					100, 100, 0, false, true, false),
				new StealthMassAttackActorSnapshot(74, "brik", new CPos(2, 2),
					100, 100, 0, false, true, false)
			};
			var world = new MassWorld
			{
				BlockerId = 74,
				Snapshot = new StealthMassAttackLiveSnapshot(1, members, actors,
					Array.Empty<CPos>(), true)
			};
			var orders = new MassOrders();
			var behavior = new StealthMassAttackBehavior(MassHandoff(), new Guard(), world,
				new MassThreat { Approved = facts => false }, orders);

			behavior.Execute();
			world.Snapshot = new StealthMassAttackLiveSnapshot(76, members, actors,
				Array.Empty<CPos>(), true);
			var result = behavior.Execute();

			Assert.That(result.SelectedTargetActorId, Is.EqualTo(72));
			Assert.That(orders.Target, Is.EqualTo(74));
			Assert.That(orders.Attacks, Is.EqualTo(2));
			var controller = Construct<StealthLifecycleController>(BehaviorId.MassAttack);
			Assert.That(controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.Retained, Is.Not.Null);
		}

		[Test]
		public void MassAttackRetriesItsRetainedTargetOnlyAfterTheBoundedInterval()
		{
			var world = new MassWorld { Snapshot = MassSnapshot(membersIdle: true) };
			var orders = new MassOrders();
			var behavior = new StealthMassAttackBehavior(MassHandoff(), new Guard(), world,
				new MassThreat(), orders);

			behavior.Execute();
			behavior.Execute();
			world.Snapshot = MassSnapshot(membersIdle: true, tick: 76);
			behavior.Execute();

			Assert.That(orders.Attacks, Is.EqualTo(2));
		}

		[Test]
		public void MassAttackRetriesItsRetainedAdvanceOnlyAfterTheBoundedInterval()
		{
			var safeCell = new CPos(2, 0);
			var world = new MassWorld
			{
				Snapshot = MassSnapshot(new[] { safeCell }, membersIdle: true)
			};
			var orders = new MassOrders();
			var behavior = new StealthMassAttackBehavior(MassHandoff(), new Guard(), world,
				new MassThreat { Approved = facts => facts.PlannedCell == safeCell }, orders);

			behavior.Execute();
			behavior.Execute();
			world.Snapshot = MassSnapshot(new[] { safeCell }, membersIdle: true, tick: 76);
			behavior.Execute();

			Assert.That(orders.Moves, Is.EqualTo(2));
			Assert.That(orders.Attacks, Is.Zero);
		}

		[Test]
		public void MassAttackBoundsSafeCellChecksInsideOneLiveThreatEvaluation()
		{
			var candidates = Enumerable.Range(1, 20).Select(x => new CPos(x, 1));
			var world = new MassWorld { Snapshot = MassSnapshot(candidates) };
			var threat = new MassThreat { Approved = facts => false };

			new StealthMassAttackBehavior(MassHandoff(), new Guard(), world,
				threat, new MassOrders()).Execute();

			Assert.That(threat.Beginnings, Is.EqualTo(1));
			Assert.That(threat.Calculations, Is.EqualTo(10),
				"Two live targets plus at most eight candidate cells may be evaluated.");
		}

		static StealthKiteLiveSnapshot KiteSnapshot(CPos memberCell, CPos targetCell,
			IEnumerable<CPos> candidates = null, IEnumerable<uint> memberIds = null)
		{
			var members = (memberIds ?? new uint[] { 1 }).Select(id =>
				new StealthKiteMemberSnapshot(id, memberCell, 5));
			var target = new StealthKiteActorSnapshot(71, "harv", targetCell, 100, 100, 1,
				true, true, false, false, false);
			return new StealthKiteLiveSnapshot(1, members, new[] { target },
				candidates ?? Array.Empty<CPos>(), true);
		}

		static StealthCrushLiveSnapshot CrushSnapshot(CPos targetCell, bool detected,
			bool needsMovementOrder = false)
		{
			return new StealthCrushLiveSnapshot(1,
				new[]
				{
					new StealthCrushMemberSnapshot(1, new CPos(0, 0),
						needsMovementOrder: needsMovementOrder)
				},
				new[]
				{
					new StealthCrushActorSnapshot(71, "e1", new CPos(5, 5), targetCell,
						100, true, false, true, true, detected)
				}, true);
		}

		static StealthUndefendedAttackLiveSnapshot UndefendedSnapshot(bool addBetterTarget,
			IEnumerable<uint> defenders = null, bool membersIdle = false)
		{
			var targets = new List<StealthUndefendedAttackTargetSnapshot>
			{
				new StealthUndefendedAttackTargetSnapshot(71, "harv", new CPos(5, 5),
					new CPos(5, 5), 5000, 1100, 100, 100)
			};
			if (addBetterTarget)
				targets.Add(new StealthUndefendedAttackTargetSnapshot(72, "fact", new CPos(5, 5),
					new CPos(6, 5), 10000, 2000, 100, 100));
			return new StealthUndefendedAttackLiveSnapshot(1,
				new[]
				{
					new StealthUndefendedAttackMemberSnapshot(1, "stnk", 900,
						new CPos(0, 0), 100, 100, 5, membersIdle)
				}, targets, defenders ?? Array.Empty<uint>(), true, false, true);
		}

		static StealthMassAttackLiveSnapshot MassSnapshot(IEnumerable<CPos> candidates = null,
			bool membersIdle = false, IEnumerable<CPos> memberCells = null, int tick = 1)
		{
			return MassSnapshotWithActors(new[]
				{
					new StealthMassAttackActorSnapshot(71, "e1", new CPos(3, 0),
						100, 100, 1, true, false, false),
					new StealthMassAttackActorSnapshot(72, "e3", new CPos(4, 0),
						100, 100, 1, true, false, false)
				}, candidates, membersIdle, memberCells, tick);
		}

		static StealthMassAttackLiveSnapshot MassSnapshotWithActors(
			IEnumerable<StealthMassAttackActorSnapshot> actors, IEnumerable<CPos> candidates = null,
			bool membersIdle = false, IEnumerable<CPos> memberCells = null, int tick = 1)
		{
			var members = (memberCells ?? new[] { new CPos(0, 0) }).Select((cell, index) =>
				new StealthMassAttackMemberSnapshot((uint)index + 1, cell, 5,
					needsMovementOrder: membersIdle));
			return new StealthMassAttackLiveSnapshot(tick, members, actors,
				candidates ?? Array.Empty<CPos>(), true);
		}

		static StealthKiteHandoff KiteHandoff()
		{
			return Construct<StealthKiteHandoff>(
				Handoff(BehaviorId.Kite), Mission(), new uint[] { 71 });
		}

		static StealthCrushEvaluationHandoff CrushHandoff()
		{
			return Construct<StealthCrushEvaluationHandoff>(Handoff(BehaviorId.CrushEvaluation),
				Mission(), new uint[] { 71 });
		}

		static StealthUndefendedAttackHandoff UndefendedHandoff()
		{
			return Construct<StealthUndefendedAttackHandoff>(
				Handoff(BehaviorId.UndefendedAttack), Mission());
		}

		static StealthMassAttackHandoff MassHandoff(bool coordinated = false)
		{
			var arguments = new List<object>
			{
				"old-entry", 71u, new CPos(99, 99), new uint[] { 1 },
				new uint[] { 71 }, true, new StealthTargetThreatScore(1, 3)
			};
			if (coordinated)
				arguments.Add(true);
			var evidence = Construct<StealthMassAttackEntryEvidence>(arguments.ToArray());
			return Construct<StealthMassAttackHandoff>(
				Handoff(BehaviorId.MassAttack), Mission(), evidence);
		}

		static StealthApproachMission Mission()
		{
			var cell = new CPos(5, 5);
			var option = Construct<StealthTargetOption>(cell, (int?)1000, false,
				new[] { new StealthStrategicTargetSnapshot(71, cell, 5000, 1100, 100, 100) }, null);
			var value = Construct<StealthTargetValueOption>(option, 5500000L);
			return Construct<StealthApproachMission>(Construct<StealthTargetThreatOption>(value,
				new StealthTargetThreatScore(1, 2)));
		}

		static StealthBehaviorHandoff Handoff(BehaviorId owner)
		{
			return Construct<StealthBehaviorHandoff>(owner, new OwnershipEpoch(1));
		}

		static T Construct<T>(params object[] arguments)
		{
			return (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance |
				BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null);
		}
	}
}
