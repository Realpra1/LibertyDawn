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
			public StealthKiteLiveSnapshot Read(StealthApproachMission mission) { return Snapshot; }
		}

		sealed class KiteThreat : IStealthKiteThreatAdapter
		{
			public readonly List<StealthKiteThreatFacts> Facts = new List<StealthKiteThreatFacts>();
			public Func<StealthKiteThreatFacts, bool> Approved = facts => true;
			public double FallbackCrossover = 1;
			public StealthKiteSafetyResult Calculate(StealthKiteThreatFacts facts)
			{
				Facts.Add(facts);
				return new StealthKiteSafetyResult(new StealthTargetThreatScore(1, 1), Approved(facts));
			}

			public StealthTargetThreatScore CalculateAttackCrossover(StealthKiteFallbackFacts facts)
			{
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
			public void IssueAttack(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, uint targetActorId)
			{
				Targets.Add(targetActorId);
			}
		}

		sealed class MassWorld : IStealthMassAttackLiveWorld
		{
			public StealthMassAttackLiveSnapshot Snapshot;
			public StealthMassAttackLiveSnapshot Read(StealthApproachMission mission) { return Snapshot; }
		}

		sealed class MassThreat : IStealthMassAttackThreatAdapter
		{
			public double Crossover = 2;
			public Func<StealthMassAttackThreatFacts, bool> Approved = facts => true;
			public StealthMassAttackThreatResult Calculate(StealthMassAttackThreatFacts facts)
			{
				return new StealthMassAttackThreatResult(
					new StealthTargetThreatScore(1, Crossover), facts.SelectedTargetActorId,
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
		}

		[Test]
		public void KiteDoesNotDecloakTheGroupWhileOneLiveMemberIsUnsafe()
		{
			var safeCell = new CPos(0, 0);
			var unsafeCell = new CPos(0, 1);
			var fallbackCell = new CPos(-1, 0);
			var target = new StealthKiteActorSnapshot(71, "obli", new CPos(5, 0),
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
				Approved = facts => facts.PlannedCell == safeCell || facts.PlannedCell == fallbackCell
			};
			var orders = new KiteOrders();

			new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders).Execute();

			Assert.That(orders.Attacks, Is.Zero);
			Assert.That(orders.Moves, Is.EqualTo(1));
			Assert.That(orders.Cell, Is.EqualTo(fallbackCell));
		}

		[Test]
		public void KiteWaitsForMovementAndRetriesOnlyAfterItEnds()
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
			behavior.Execute();

			Assert.That(orders.Moves, Is.EqualTo(2));
		}

		[Test]
		public void KiteNeverChoosesACellOccupiedByAnotherSquadMember()
		{
			var occupied = new CPos(1, 0);
			var safeCell = new CPos(2, 0);
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
			var threat = new KiteThreat { Approved = facts => facts.PlannedCell != new CPos(0, 0) };
			var orders = new KiteOrders();

			new StealthKiteBehavior(KiteHandoff(), new Guard(), world, threat, orders).Execute();

			Assert.That(orders.Cell, Is.EqualTo(safeCell));
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
		public void KiteHandsAnUnsafeExposedPositionToLeastDangerousFlee()
		{
			var safeCell = new CPos(2, 0);
			var target = new StealthKiteActorSnapshot(71, "obli", new CPos(4, 0),
				100, 100, 4, true, true, false, false, false);
			var world = new KiteWorld
			{
				Snapshot = new StealthKiteLiveSnapshot(1,
					new[] { new StealthKiteMemberSnapshot(1, new CPos(0, 0), 5) },
					new[] { target }, new[] { safeCell }, false)
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

			var controller = Construct<StealthLifecycleController>(BehaviorId.Kite);
			Assert.That(controller.TryAccept(result, out var transition), Is.True);
			Assert.That(transition.RecalculateFleeEntry.Evidence.Source,
				Is.EqualTo(StealthRecalculateFleeSource.KiteUnsafeCurrentPosition));
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
		public void CrushUsesLiveTargetCellAndRejectsActualDetectorCoverage()
		{
			var world = new CrushWorld { Snapshot = CrushSnapshot(new CPos(5, 0), false) };
			var threat = new CrushThreat();
			var orders = new CrushOrders();
			var behavior = new StealthCrushBehavior(CrushHandoff(), new Guard(), world, threat, orders);

			Assert.That(behavior.Execute().Disposition, Is.EqualTo(StealthCrushDisposition.Retain));
			world.Snapshot = CrushSnapshot(new CPos(7, 0), false);
			behavior.Execute();
			world.Snapshot = CrushSnapshot(new CPos(8, 0), true);

			Assert.That(behavior.Execute().Disposition, Is.EqualTo(StealthCrushDisposition.Kite));
			Assert.That(orders.Cells, Is.EqualTo(new[] { new CPos(5, 0), new CPos(7, 0) }));
			Assert.That(threat.LastFacts.HasDetectorCoverage, Is.True);
		}

		[Test]
		public void CrushRetriesAStationaryLiveInfantryTarget()
		{
			var world = new CrushWorld { Snapshot = CrushSnapshot(new CPos(5, 0), false) };
			var orders = new CrushOrders();
			var behavior = new StealthCrushBehavior(CrushHandoff(), new Guard(), world,
				new CrushThreat(), orders);

			behavior.Execute();
			behavior.Execute();

			Assert.That(orders.Cells,
				Is.EqualTo(new[] { new CPos(5, 0), new CPos(5, 0) }));
		}

		[Test]
		public void CrushHandsAnUncaughtInfantryTargetToKite()
		{
			var world = new CrushWorld { Snapshot = CrushSnapshot(new CPos(5, 0), false) };
			var behavior = new StealthCrushBehavior(CrushHandoff(), new Guard(), world,
				new CrushThreat(), new CrushOrders());

			for (var i = 0; i < 4; i++)
				Assert.That(behavior.Execute().Disposition, Is.EqualTo(StealthCrushDisposition.Retain));

			Assert.That(behavior.Execute().Disposition, Is.EqualTo(StealthCrushDisposition.Kite));
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
		public void MassAttackCommitsWhenCrossoverApprovesNoSafeFiringCell()
		{
			var world = new MassWorld { Snapshot = MassSnapshot() };
			var threat = new MassThreat { Approved = facts => false };
			var orders = new MassOrders();

			var result = new StealthMassAttackBehavior(MassHandoff(), new Guard(), world,
				threat, orders).Execute();

			Assert.That(result.Phase, Is.EqualTo(StealthMassAttackPhase.Attack));
			Assert.That(orders.Attacks, Is.EqualTo(1));
			Assert.That(orders.Moves, Is.Zero);
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

		static StealthCrushLiveSnapshot CrushSnapshot(CPos targetCell, bool detected)
		{
			return new StealthCrushLiveSnapshot(1,
				new[]
				{
					new StealthCrushMemberSnapshot(1, new CPos(0, 0))
				},
				new[]
				{
					new StealthCrushActorSnapshot(71, "e1", new CPos(5, 5), targetCell,
						100, true, false, true, true, detected)
				}, true);
		}

		static StealthUndefendedAttackLiveSnapshot UndefendedSnapshot(bool addBetterTarget,
			IEnumerable<uint> defenders = null)
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
						new CPos(0, 0), 100, 100, 5)
				}, targets, defenders ?? Array.Empty<uint>(), true, false, true);
		}

		static StealthMassAttackLiveSnapshot MassSnapshot(IEnumerable<CPos> candidates = null)
		{
			return new StealthMassAttackLiveSnapshot(1,
				new[] { new StealthMassAttackMemberSnapshot(1, new CPos(0, 0), 5) },
				new[]
				{
					new StealthMassAttackActorSnapshot(71, "e1", new CPos(3, 0),
						100, 100, 1, true, false, false),
					new StealthMassAttackActorSnapshot(72, "e3", new CPos(4, 0),
						100, 100, 1, true, false, false)
				}, candidates ?? Array.Empty<CPos>(), true);
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

		static StealthMassAttackHandoff MassHandoff()
		{
			var evidence = Construct<StealthMassAttackEntryEvidence>("old-entry", 71u,
				new CPos(99, 99), new uint[] { 1 }, new uint[] { 71 }, true,
				new StealthTargetThreatScore(1, 3));
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
				new StealthTargetThreatScore(1, 2)), 0L, 0, 1000L);
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
