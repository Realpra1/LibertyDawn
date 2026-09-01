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
	public sealed class StealthSquadConstructionBehaviorTest
	{
		sealed class SafetyProbe : IStealthSquadConstructionSafetyService
		{
			readonly Dictionary<uint, CPos[]> routes = new Dictionary<uint, CPos[]>();

			public List<uint> Queries { get; } = new List<uint>();

			public void AddRoute(uint actorId, params CPos[] route)
			{
				routes.Add(actorId, route);
			}

			public bool TryFindSafeRoute(uint actorId, CPos originStrategicCell,
				CPos destinationStrategicCell, out IReadOnlyList<CPos> routeStrategicCells)
			{
				Queries.Add(actorId);
				if (routes.TryGetValue(actorId, out var route))
				{
					routeStrategicCells = route;
					return true;
				}

				routeStrategicCells = null;
				return false;
			}
		}

		static StealthSquadConstructionMemberSnapshot Member(uint actorId, int x, int y,
			int? squadId = null, bool isInWorld = true, bool isDead = false,
			bool isStealthTank = true)
		{
			return new StealthSquadConstructionMemberSnapshot(actorId, new CPos(x, y),
				squadId, isInWorld, isDead, isStealthTank);
		}

		static StealthSquadConstructionSquadSnapshot Squad(int squadId, int x, int y)
		{
			return new StealthSquadConstructionSquadSnapshot(squadId, new CPos(x, y));
		}

		static StealthSquadConstructionBehavior Construction(StealthLifecycleController controller,
			SafetyProbe safety, params uint[] memberActorIds)
		{
			var start = new StealthStartBehavior(controller.CurrentHandoff);
			var startResult = start.Execute(new StealthLifecycleObservation(
				StealthLifecycleObservationKind.UnitBuilt, memberActorIds[0]),
				memberActorIds.Select(actorId => new StealthStartMemberSnapshot(actorId)));
			Assert.That(controller.TryAccept(startResult, out var constructionHandoff), Is.True);
			return new StealthSquadConstructionBehavior(
				constructionHandoff, startResult.MemberActorIds, safety);
		}

		[Test]
		public void ActiveOwnerAloneCompletesConstructionAndHandsOffToTargetAcquisition()
		{
			var controller = new StealthLifecycleController();
			var result = Construction(controller, new SafetyProbe(), 7).Execute(
				new[] { Member(7, 3, 4) }, Array.Empty<StealthSquadConstructionSquadSnapshot>());

			controller.Observe(new StealthLifecycleObservationFrame(5, new[]
			{
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Timer),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.WorldEvent)
			}));
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
			Assert.That(controller.TryAccept(result, out var targetHandoff), Is.True);
			Assert.That(targetHandoff.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
			Assert.That(targetHandoff.Epoch, Is.EqualTo(new OwnershipEpoch(3)));
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
		}

		[Test]
		public void AssignsUnassignedMembersDeterministicallyAndAdmitsAdjacentArrivals()
		{
			var controller = new StealthLifecycleController();
			var safety = new SafetyProbe();
			var result = Construction(controller, safety, 9, 4, 7, 12).Execute(
				new[]
				{
					Member(12, 21, 21), Member(4, 2, 2, squadId: 1),
					Member(9, 10, 10), Member(7, 20, 20, squadId: 2)
				},
				new[] { Squad(2, 20, 20), Squad(1, 10, 10) });

			Assert.That(result.IsComplete, Is.True);
			Assert.That(result.Assignments.Select(assignment => assignment.ActorId),
				Is.EqualTo(new uint[] { 9, 12 }));
			Assert.That(result.Assignments.Select(assignment => assignment.SquadId),
				Is.EqualTo(new[] { 1, 2 }));
			Assert.That(result.Assignments.All(assignment =>
				assignment.Disposition == StealthSquadAssignmentDisposition.ActiveMember), Is.True);
			Assert.That(result.Centers.Single(center => center.SquadId == 1).MemberActorIds,
				Is.EqualTo(new uint[] { 4, 9 }));
			Assert.That(result.Centers.Single(center => center.SquadId == 2).MemberActorIds,
				Is.EqualTo(new uint[] { 7, 12 }));
			Assert.That(safety.Queries, Is.Empty);
		}

		[Test]
		public void RoutedReinforcementStaysOutsideTheCenterUntilItArrives()
		{
			var controller = new StealthLifecycleController();
			var safety = new SafetyProbe();
			safety.AddRoute(9, new CPos(4, 4), new CPos(8, 8), new CPos(10, 9));
			var construction = Construction(controller, safety, 4, 9);
			var result = construction.Execute(
				new[] { Member(4, 10, 10, squadId: 1), Member(9, 4, 4) },
				new[] { Squad(1, 10, 10) });

			var assignment = result.Assignments.Single();
			Assert.That(assignment.Disposition,
				Is.EqualTo(StealthSquadAssignmentDisposition.RoutedReinforcement));
			Assert.That(assignment.SafeRouteStrategicCells,
				Is.EqualTo(new[] { new CPos(4, 4), new CPos(8, 8), new CPos(10, 9) }));
			Assert.That(assignment.IsActiveCenterMember, Is.False);
			Assert.That(result.Centers.Single().MemberActorIds, Is.EqualTo(new uint[] { 4 }));

			var stillJoining = construction.Execute(
				new[] { Member(4, 10, 10, squadId: 1), Member(9, 6, 6, squadId: 1) },
				new[] { Squad(1, 10, 10) });
			Assert.That(stillJoining.Assignments.Single().Disposition,
				Is.EqualTo(StealthSquadAssignmentDisposition.RoutedReinforcement));
			Assert.That(stillJoining.Centers.Single().MemberActorIds, Is.EqualTo(new uint[] { 4 }));

			var arrived = construction.Execute(
				new[] { Member(4, 10, 10, squadId: 1), Member(9, 10, 9, squadId: 1) },
				new[] { Squad(1, 10, 10) });
			Assert.That(arrived.Assignments.Single().Disposition,
				Is.EqualTo(StealthSquadAssignmentDisposition.ActiveMember));
			Assert.That(arrived.Centers.Single().MemberActorIds, Is.EqualTo(new uint[] { 4, 9 }));
			Assert.That(safety.Queries, Is.EqualTo(new uint[] { 9, 9 }));
		}

		[Test]
		public void UnsafeOrIncompleteRouteBecomesSafeHoldAndCannotMoveTheCenter()
		{
			var controller = new StealthLifecycleController();
			var safety = new SafetyProbe();
			safety.AddRoute(9, new CPos(4, 4), new CPos(6, 6));
			var result = Construction(controller, safety, 4, 9, 12).Execute(
				new[]
				{
					Member(4, 10, 10, squadId: 1), Member(9, 4, 4),
					Member(12, 2, 2)
				},
				new[] { Squad(1, 10, 10) });

			Assert.That(result.Assignments.Select(assignment => assignment.Disposition),
				Is.EqualTo(new[]
				{
					StealthSquadAssignmentDisposition.SafeHoldReinforcement,
					StealthSquadAssignmentDisposition.SafeHoldReinforcement
				}));
			Assert.That(result.Assignments.SelectMany(assignment => assignment.SafeRouteStrategicCells),
				Is.Empty);
			Assert.That(result.Centers.Single().MemberActorIds, Is.EqualTo(new uint[] { 4 }));
		}

		[Test]
		public void LowestUnassignedStealthTankRemakesMissingSquadCenter()
		{
			var controller = new StealthLifecycleController();
			var safety = new SafetyProbe();
			safety.AddRoute(8, new CPos(20, 20), new CPos(3, 3));
			var result = Construction(controller, safety, 8, 3).Execute(
				new[] { Member(8, 20, 20), Member(3, 3, 3) },
				Array.Empty<StealthSquadConstructionSquadSnapshot>());

			Assert.That(result.Assignments[0].ActorId, Is.EqualTo(3));
			Assert.That(result.Assignments[0].Disposition,
				Is.EqualTo(StealthSquadAssignmentDisposition.NewCenter));
			Assert.That(result.Assignments[1].Disposition,
				Is.EqualTo(StealthSquadAssignmentDisposition.RoutedReinforcement));
			Assert.That(result.Centers.Single().StrategicCell, Is.EqualTo(new CPos(3, 3)));
			Assert.That(result.Centers.Single().MemberActorIds, Is.EqualTo(new uint[] { 3 }));
		}

		[Test]
		public void InvalidMembersAreNeverAssignedAndEmptyConstructionTerminates()
		{
			var controller = new StealthLifecycleController();
			var result = Construction(controller, new SafetyProbe(), 4, 7, 9).Execute(
				new[]
				{
					Member(4, 1, 1, isInWorld: false), Member(7, 1, 1, isDead: true),
					Member(9, 1, 1, isStealthTank: false)
				},
				Array.Empty<StealthSquadConstructionSquadSnapshot>());

			Assert.That(result.Disposition, Is.EqualTo(StealthSquadConstructionDisposition.Terminated));
			Assert.That(result.Assignments, Is.Empty);
			Assert.That(result.Centers, Is.Empty);
			Assert.That(controller.TryAccept(result, out var handoff), Is.False);
			Assert.That(handoff, Is.Null);
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
		}

		[Test]
		public void PrivateStateRoundTripsAssignmentsCentersAndOwnershipEpoch()
		{
			var controller = new StealthLifecycleController();
			var safety = new SafetyProbe();
			safety.AddRoute(9, new CPos(4, 4), new CPos(10, 9));
			var construction = Construction(controller, safety, 4, 9);
			var result = construction.Execute(
				new[] { Member(4, 10, 10, squadId: 1), Member(9, 4, 4) },
				new[] { Squad(1, 10, 10) });

			var serialized = new List<MiniYamlNode> { construction.SerializePrivateState(result) }
				.WriteToString();
			var restoredConstruction = new StealthSquadConstructionBehavior(
				controller.CurrentHandoff, new uint[] { 4, 9 }, safety);
			var restored = restoredConstruction.RestorePrivateState(
				MiniYaml.FromString(serialized).Single());

			Assert.That(restored.Assignments.Single().ActorId, Is.EqualTo(9));
			Assert.That(restored.Assignments.Single().Disposition,
				Is.EqualTo(StealthSquadAssignmentDisposition.RoutedReinforcement));
			Assert.That(restored.Centers.Single().MemberActorIds, Is.EqualTo(new uint[] { 4 }));
			Assert.That(new List<MiniYamlNode> { restoredConstruction.SerializePrivateState(restored) }
				.WriteToString(), Is.EqualTo(serialized));
			var resumed = restoredConstruction.Execute(
				new[] { Member(4, 10, 10, squadId: 1), Member(9, 5, 5, squadId: 1) },
				new[] { Squad(1, 10, 10) });
			Assert.That(resumed.Assignments.Single().Disposition,
				Is.EqualTo(StealthSquadAssignmentDisposition.RoutedReinforcement));
			Assert.That(resumed.Centers.Single().MemberActorIds, Is.EqualTo(new uint[] { 4 }));
			Assert.That(controller.TryAccept(restored, out var handoff), Is.True);
			Assert.That(handoff.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
		}

		[Test]
		public void PrivateStateCannotRebindAcrossConstructionEpochs()
		{
			var originalController = new StealthLifecycleController();
			var original = Construction(originalController, new SafetyProbe(), 4);
			var result = original.Execute(new[] { Member(4, 1, 1) },
				Array.Empty<StealthSquadConstructionSquadSnapshot>());
			var serialized = new List<MiniYamlNode> { original.SerializePrivateState(result) }
				.WriteToString();
			var newerController = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.SquadConstruction, new OwnershipEpoch(3), -1));
			var newer = new StealthSquadConstructionBehavior(newerController.CurrentHandoff,
				new uint[] { 4 }, new SafetyProbe());

			Assert.Throws<InvalidOperationException>(() =>
				newer.RestorePrivateState(MiniYaml.FromString(serialized).Single()));
			Assert.That(newerController.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
			Assert.That(newerController.Epoch, Is.EqualTo(new OwnershipEpoch(3)));
		}

		[Test]
		public void PrivateStateRejectsStagedActorSmuggledIntoAnotherCenter()
		{
			var controller = new StealthLifecycleController();
			var safety = new SafetyProbe();
			safety.AddRoute(9, new CPos(4, 4), new CPos(10, 9));
			var construction = Construction(controller, safety, 4, 7, 9);
			var result = construction.Execute(
				new[]
				{
					Member(4, 10, 10, squadId: 1), Member(7, 20, 20, squadId: 2),
					Member(9, 4, 4)
				},
				new[] { Squad(1, 10, 10), Squad(2, 20, 20) });
			var serialized = new List<MiniYamlNode> { construction.SerializePrivateState(result) }
				.WriteToString();
			var tampered = serialized.Replace("MemberActorIds: 7", "MemberActorIds: 7, 9");

			Assert.That(tampered, Is.Not.EqualTo(serialized));
			Assert.Throws<InvalidOperationException>(() =>
				construction.RestorePrivateState(MiniYaml.FromString(tampered).Single()));
		}

		[Test]
		public void OnlyTypedConstructionResultCanYieldToTargetAcquisition()
		{
			var accept = typeof(StealthLifecycleController).GetMethods()
				.Single(method => method.Name == nameof(StealthLifecycleController.TryAccept) &&
					method.GetParameters()[0].ParameterType == typeof(StealthSquadConstructionResult));

			Assert.That(accept.GetParameters()[0].ParameterType,
				Is.EqualTo(typeof(StealthSquadConstructionResult)));
			Assert.That(typeof(StealthSquadConstructionResult).GetConstructors(), Is.Empty);
			Assert.Throws<ArgumentException>(() => new StealthSquadConstructionBehavior(
				new StealthLifecycleController().CurrentHandoff,
				new uint[] { 4 }, new SafetyProbe()));
		}
	}
}
