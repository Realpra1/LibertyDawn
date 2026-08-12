#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class ConstructionYardEnclosurePolicyTest
	{
		static ConstructionYardEnclosurePlan FactPlan()
		{
			return ConstructionYardEnclosurePolicy.CreatePlan(new CPos(50, 50), new CVec(3, 3), 1, 3);
		}

		[Test]
		public void FactPlanHasStableThreeCellSouthAccess()
		{
			var plan = FactPlan();
			Assert.That(plan.AccessCells, Is.EqualTo(new[]
			{
				new CPos(50, 53), new CPos(51, 53), new CPos(52, 53)
			}));
			Assert.That(plan.WallCells, Has.Length.EqualTo(13));
			Assert.That(plan.WallCells.Intersect(plan.AccessCells), Is.Empty);
			Assert.That(plan.WallCells.Distinct().Count(), Is.EqualTo(plan.WallCells.Length));
			Assert.That(ConstructionYardEnclosurePolicy.CreatePlan(
				new CPos(50, 50), new CVec(3, 3), 1, 3).WallCells, Is.EqualTo(plan.WallCells));
		}

		[Test]
		public void BlockedCellDoesNotVetoIndependentLegalRun()
		{
			var plan = FactPlan();
			var blocked = new CPos(51, 49);
			var run = ConstructionYardEnclosurePolicy.FirstLegalMissingRun(plan,
				c => false, c => c != blocked);
			Assert.That(run, Is.EqualTo(new[] { new CPos(49, 49), new CPos(50, 49) }));

			var alreadyBuilt = new HashSet<CPos>(run);
			run = ConstructionYardEnclosurePolicy.FirstLegalMissingRun(plan,
				alreadyBuilt.Contains, c => c != blocked);
			Assert.That(run, Is.EqualTo(new[] { new CPos(52, 49), new CPos(53, 49) }));
		}

		[Test]
		public void InteriorDestroyedCellBecomesRepairCandidate()
		{
			var plan = FactPlan();
			var hole = new CPos(49, 51);
			var present = new HashSet<CPos>(plan.WallCells.Where(c => c != hole));
			Assert.That(ConstructionYardEnclosurePolicy.FirstLegalMissingRun(plan,
				present.Contains, c => true), Is.EqualTo(new[] { hole }));
		}

		[Test]
		public void TransientAndFixedBlockageRemainPendingWithoutChangingPlan()
		{
			var plan = FactPlan();
			var present = new HashSet<CPos>(plan.WallCells.Take(5));
			var fixedCell = plan.WallCells[5];
			var transientCell = plan.WallCells[6];
			var first = ConstructionYardEnclosurePolicy.FirstLegalMissingRun(plan,
				present.Contains, c => c != fixedCell && c != transientCell);
			Assert.That(first, Is.Not.Empty);
			Assert.That(first, Does.Not.Contain(fixedCell));
			Assert.That(first, Does.Not.Contain(transientCell));
			Assert.That(plan.WallCells, Does.Contain(fixedCell));
			Assert.That(plan.WallCells, Does.Contain(transientCell));
		}

		[Test]
		public void ReservationOverlapUsesActualBuildingFootprintCells()
		{
			var plan = FactPlan();
			Assert.That(ConstructionYardEnclosurePolicy.Overlaps(plan,
				new[] { new CPos(48, 48), new CPos(49, 49) }), Is.True);
			Assert.That(ConstructionYardEnclosurePolicy.Overlaps(plan,
				new[] { new CPos(50, 50), new CPos(51, 50) }), Is.False);
		}

		[Test]
		public void RandomPlacementFallbackFindsUnreservedCellBeyondBoundedSample()
		{
			var candidates = Enumerable.Range(0, 9).Select(x => new CPos(x, 0)).ToArray();
			var reserved = new HashSet<CPos>(candidates.Take(8));
			Assert.That(ConstructionYardEnclosurePolicy.FirstLegalUnreservedCell(candidates,
				_ => true, reserved.Contains), Is.EqualTo(candidates[8]));
			Assert.That(ConstructionYardEnclosurePolicy.FirstLegalUnreservedCell(candidates,
				_ => true, _ => true), Is.Null);
		}

		[Test]
		public void CutoffAndIdentitySelectionAreLiteralBoundaries()
		{
			Assert.That(ConstructionYardEnclosurePolicy.IsActive(7499, 7500, true, false), Is.True);
			Assert.That(ConstructionYardEnclosurePolicy.IsActive(7500, 7500, true, false), Is.False);
			Assert.That(ConstructionYardEnclosurePolicy.IsActive(1, 7500, true, true), Is.False);
			Assert.That(ConstructionYardEnclosurePolicy.SelectInitialYardActorId(
				new uint[] { 19, 4, 12 }, true), Is.EqualTo(4));
			Assert.That(ConstructionYardEnclosurePolicy.SelectInitialYardActorId(
				new uint[] { 4, 12 }, false), Is.Null);
		}

		[Test]
		public void ExactCutoffTickDeactivatesWithoutWaitingForAnotherMaintenanceInterval()
		{
			const int cutoff = 7500;
			Assert.That(ConstructionYardEnclosurePolicy.IsActive(cutoff - 1, cutoff, true, false), Is.True);
			Assert.That(ConstructionYardEnclosurePolicy.IsActive(cutoff, cutoff, true, false), Is.False);
			Assert.That(ConstructionYardEnclosurePolicy.IsActive(cutoff + 250, cutoff, true, false), Is.False);
		}

		[Test]
		public void SavedPlanRequiresExactOrderedGeometryAndAccess()
		{
			var plan = FactPlan();
			Assert.That(ConstructionYardEnclosurePolicy.MatchesSavedPlan(plan,
				plan.WallCells, plan.AccessCells), Is.True);
			Assert.That(ConstructionYardEnclosurePolicy.MatchesSavedPlan(plan,
				plan.WallCells.Reverse(), plan.AccessCells), Is.False);
			Assert.That(ConstructionYardEnclosurePolicy.MatchesSavedPlan(plan,
				plan.WallCells, plan.AccessCells.Take(2)), Is.False);
		}

		[Test]
		public void SavedCellsRoundTripThroughBoundedIntegerBits()
		{
			var plan = FactPlan();
			var serialized = FieldSaver.FormatValue(ConstructionYardEnclosurePolicy.EncodeCells(plan.WallCells));
			var restored = ConstructionYardEnclosurePolicy.DecodeCells(
				FieldLoader.GetValue<int[]>("WallCellBits", serialized));
			Assert.That(restored, Is.EqualTo(plan.WallCells));
			Assert.That(ConstructionYardEnclosurePolicy.MatchesSavedPlan(
				plan, restored, plan.AccessCells), Is.True);
		}

		[Test]
		public void SavedPendingAndObservedCellsMustBeDistinctBoundedPlanSubsets()
		{
			var plan = FactPlan();
			Assert.That(ConstructionYardEnclosurePolicy.IsValidWallCellSubset(
				plan, plan.WallCells.Take(2), 2), Is.True);
			Assert.That(ConstructionYardEnclosurePolicy.IsValidWallCellSubset(
				plan, new[] { plan.WallCells[0], plan.WallCells[0] }, 2), Is.False);
			Assert.That(ConstructionYardEnclosurePolicy.IsValidWallCellSubset(
				plan, plan.WallCells.Take(3), 2), Is.False);
			Assert.That(ConstructionYardEnclosurePolicy.IsValidWallCellSubset(
				plan, new[] { plan.AccessCells[0] }, 2), Is.False);
		}

		[Test]
		public void PendingEndpointOwnershipSerializesTheExactQueue()
		{
			var firstFactQueue = new object();
			var laterFactQueue = new object();
			var ownership = new ConstructionYardEnclosureBuildOwnership<object>();

			Assert.That(ownership.TryReserve(firstFactQueue, "sbag", 100), Is.True);
			Assert.That(ownership.Owns(firstFactQueue, "sbag"), Is.True);
			Assert.That(ownership.Owns(laterFactQueue, "sbag"), Is.False);
			Assert.That(ownership.TryReserve(laterFactQueue, "sbag", 100), Is.False,
				"A second Fact queue must not request the same pending endpoint.");

			Assert.That(ownership.Refresh(100, 25, _ => true, (_, __) => false), Is.False,
				"The reservation must survive the tick before StartProduction is resolved.");
			Assert.That(ownership.Refresh(200, 25, _ => true,
				(queue, type) => ReferenceEquals(queue, firstFactQueue) && type == "sbag"), Is.False);

			ownership.Release();
			Assert.That(ownership.TryReserve(laterFactQueue, "sbag", 201), Is.True,
				"The next independent endpoint may move to an available queue after placement.");
		}

		[Test]
		public void PendingEndpointOwnershipRestoresOnlyAQueuedMatchingBuild()
		{
			var loadedQueue = new object();
			var ownership = new ConstructionYardEnclosureBuildOwnership<object>();
			Assert.That(ownership.TryRestore(loadedQueue, "sbag", 5900, _ => true,
				(queue, type) => ReferenceEquals(queue, loadedQueue) && type == "sbag"), Is.True);
			Assert.That(ownership.Owns(loadedQueue, "sbag"), Is.True);

			var stale = new ConstructionYardEnclosureBuildOwnership<object>();
			Assert.That(stale.TryRestore(loadedQueue, "sbag", 5900, _ => true, (_, __) => false), Is.False,
				"Stale save data must not redirect an unrelated wall build to the saved endpoint.");
			Assert.That(stale.HasReservation, Is.False);
		}

		[Test]
		public void PendingEndpointOwnershipSaveLoadKeepsTheOriginalQueueReservation()
		{
			var savedQueueId = FieldLoader.GetValue<uint>("PendingQueueActorId", FieldSaver.FormatValue(41u));
			var savedQueueType = FieldLoader.GetValue<string>("PendingQueueType", FieldSaver.FormatValue("Building"));
			var savedWallType = FieldLoader.GetValue<string>("PendingWallType", FieldSaver.FormatValue("sbag"));
			var savedReservedTick = FieldLoader.GetValue<int>("PendingQueueReservedTick", FieldSaver.FormatValue(5900));
			var restoredQueue = new object();
			var competingQueue = new object();
			var ownership = new ConstructionYardEnclosureBuildOwnership<object>();

			Assert.That(ownership.TryRestore(restoredQueue, savedWallType, savedReservedTick,
				queue => ReferenceEquals(queue, restoredQueue) && savedQueueId == 41u && savedQueueType == "Building",
				(queue, type) => ReferenceEquals(queue, restoredQueue) && type == savedWallType), Is.True);
			Assert.That(ownership.Owns(restoredQueue, savedWallType), Is.True,
				"Loading a pending enclosure must retain its original queue reservation.");
			Assert.That(ownership.Owns(competingQueue, savedWallType), Is.False);
			Assert.That(ownership.Refresh(5925, 100, _ => true,
				(queue, type) => ReferenceEquals(queue, restoredQueue) && type == savedWallType), Is.False,
				"The restored queue must keep ownership while its saved wall is still queued.");
		}

		[Test]
		public void BaseBuilderSaveLoadRestoresPendingEnclosureReservationOnce()
		{
			var resolve = typeof(BaseBuilderBotModule).GetInterfaceMap(typeof(IGameSaveTraitData)).TargetMethods
				.Single(m => m.Name.EndsWith(".ResolveTraitData", StringComparison.Ordinal));
			var calls = CalledMethods(resolve).Count(m => m.Name == "ResolveTraitData" &&
				m.DeclaringType?.Name == "BaseBuilderWallPlanner");

			Assert.That(calls, Is.EqualTo(1),
				"The module must restore the pending enclosure queue reservation only once; a second restore clears it after the production queue has consumed the saved build.");
		}

		static IEnumerable<MethodBase> CalledMethods(MethodInfo method)
		{
			var instructions = method.GetMethodBody().GetILAsByteArray();
			for (var offset = 0; offset < instructions.Length;)
			{
				var opcode = (int)instructions[offset++];
				if (opcode == 0xfe)
					opcode = 0xfe00 | instructions[offset++];

				var operation = OpCodeFor(opcode);
				if (operation.OperandType == OperandType.InlineMethod)
				{
					var token = BitConverter.ToInt32(instructions, offset);
					offset += 4;
					yield return method.Module.ResolveMethod(token);
					continue;
				}

				offset += OperandSize(operation.OperandType, instructions, offset);
			}
		}

		static OpCode OpCodeFor(int value)
		{
			return typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
				.Select(f => (OpCode)f.GetValue(null)).Single(opcode => (ushort)opcode.Value == value);
		}

		static int OperandSize(OperandType operandType, byte[] instructions, int offset)
		{
			switch (operandType)
			{
				case OperandType.InlineNone: return 0;
				case OperandType.ShortInlineBrTarget:
				case OperandType.ShortInlineI:
				case OperandType.ShortInlineVar: return 1;
				case OperandType.InlineVar: return 2;
				case OperandType.InlineI:
				case OperandType.InlineBrTarget:
				case OperandType.InlineField:
				case OperandType.InlineSig:
				case OperandType.InlineString:
				case OperandType.InlineTok:
				case OperandType.InlineType: return 4;
				case OperandType.InlineI8:
				case OperandType.InlineR: return 8;
				case OperandType.ShortInlineR: return 4;
				case OperandType.InlineSwitch: return 4 + 4 * BitConverter.ToInt32(instructions, offset);
				default: throw new ArgumentOutOfRangeException(nameof(operandType));
			}
		}

		[TestCase(0, 0, true)]
		[TestCase(6199, 6200, true)]
		[TestCase(-1, 6200, false)]
		[TestCase(6201, 6200, false)]
		public void SavedOwnershipTicksCannotBeNegativeOrFromTheFuture(
			int savedTick, int currentWorldTick, bool expected)
		{
			Assert.That(ConstructionYardEnclosurePolicy.IsValidSavedTick(savedTick, currentWorldTick),
				Is.EqualTo(expected));
		}

		[TestCase(600, 250, true, 250)]
		[TestCase(40, 250, true, 40)]
		[TestCase(600, 250, false, 600)]
		public void ActiveEnclosureBoundsOnlyLongQueuePollDelays(
			int normalDelay, int maintenanceInterval, bool enclosureActive, int expected)
		{
			Assert.That(ConstructionYardEnclosurePolicy.QueuePollDelay(
				normalDelay, maintenanceInterval, enclosureActive), Is.EqualTo(expected));
		}
	}
}
