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
	public sealed class StealthLifecycleScaffoldTest
	{
		sealed class PassiveServiceProbe : IStealthLifecycleCacheService,
			IStealthLifecycleThreatService, IStealthLifecycleRouteService
		{
			public int ObservationCount { get; private set; }

			void Observe(StealthLifecycleObservationFrame frame)
			{
				Assert.That(frame, Is.Not.Null);
				ObservationCount++;
			}

			void IStealthLifecycleCacheService.Observe(StealthLifecycleObservationFrame frame)
			{
				Observe(frame);
			}

			void IStealthLifecycleThreatService.Observe(StealthLifecycleObservationFrame frame)
			{
				Observe(frame);
			}

			void IStealthLifecycleRouteService.Observe(StealthLifecycleObservationFrame frame)
			{
				Observe(frame);
			}
		}

		sealed class DiagnosticProbe : IStealthLifecycleDiagnosticService
		{
			public int RecordCount { get; private set; }
			public StealthLifecycleDiagnostic LastRecord { get; private set; }

			public void Record(StealthLifecycleDiagnostic diagnostic)
			{
				RecordCount++;
				LastRecord = diagnostic;
			}
		}

		static StealthLifecycleObservationFrame Frame(int tick,
			params StealthLifecycleObservation[] observations)
		{
			return new StealthLifecycleObservationFrame(tick, observations);
		}

		static StealthStartBehavior Start(StealthLifecycleController controller)
		{
			return new StealthStartBehavior(controller.CurrentHandoff);
		}

		static StealthStartMemberSnapshot Member(uint actorId,
			bool isInWorld = true, bool isDead = false)
		{
			return new StealthStartMemberSnapshot(actorId, isInWorld, isDead);
		}

		static MiniYamlNode SaveNode(string version = "1", string enabled = "False",
			string owner = "Start", string epoch = "1", string lastObservedTick = "-1")
		{
			return new MiniYamlNode("StealthLifecycle", "", new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", version),
				new MiniYamlNode("Enabled", enabled),
				new MiniYamlNode("Owner", owner),
				new MiniYamlNode("Epoch", epoch),
				new MiniYamlNode("LastObservedTick", lastObservedTick)
			});
		}

		[Test]
		public void ObservationCannotTransitionWithoutTheActiveOwnerResult()
		{
			var controller = new StealthLifecycleController();
			var start = controller.CurrentHandoff;

			controller.Observe(Frame(20,
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Timer),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.UnitBuilt, 17)));

			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.Start));
			Assert.That(controller.Epoch, Is.EqualTo(new OwnershipEpoch(1)));
			Assert.That(controller.LastObservedTick, Is.EqualTo(20));

			var result = new StealthStartBehavior(start).Execute(
				new StealthLifecycleObservation(StealthLifecycleObservationKind.UnitBuilt, 17),
				new[] { Member(17) });
			var accepted = controller.TryAccept(result, out var handoff);

			Assert.That(accepted, Is.True);
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
			Assert.That(controller.Epoch, Is.EqualTo(new OwnershipEpoch(2)));
			Assert.That(handoff.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
			Assert.That(handoff.Epoch, Is.EqualTo(new OwnershipEpoch(2)));
		}

		[Test]
		public void StaleOwnershipEpochCannotStealTheController()
		{
			var controller = new StealthLifecycleController();
			var staleStartResult = Start(controller).Execute(
				new StealthLifecycleObservation(StealthLifecycleObservationKind.UnitBuilt, 14),
				new[] { Member(14) });
			Assert.That(controller.TryAccept(staleStartResult, out var currentHandoff), Is.True);

			Assert.That(controller.TryAccept(staleStartResult, out var rejectedHandoff), Is.False);
			Assert.That(rejectedHandoff, Is.Null);
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
			Assert.That(controller.Epoch, Is.EqualTo(new OwnershipEpoch(2)));
			Assert.That(currentHandoff.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
			Assert.That(currentHandoff.Epoch, Is.EqualTo(new OwnershipEpoch(2)));
		}

		[Test]
		public void TimerAndWorldEventsRemainImmutablePassiveObservations()
		{
			var observations = new List<StealthLifecycleObservation>
			{
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Timer),
				new StealthLifecycleObservation(StealthLifecycleObservationKind.WorldEvent, 42)
			};
			var frame = new StealthLifecycleObservationFrame(75, observations);
			observations.Clear();

			var controller = new StealthLifecycleController();
			controller.Observe(frame);

			Assert.That(frame.Observations.Count, Is.EqualTo(2));
			Assert.That(frame.Observations.Select(o => o.Kind), Is.EqualTo(new[]
			{
				StealthLifecycleObservationKind.Timer,
				StealthLifecycleObservationKind.WorldEvent
			}));
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.Start));
			Assert.That(controller.Epoch, Is.EqualTo(new OwnershipEpoch(1)));
		}

		[Test]
		public void PassiveServicesAndDiagnosticsHaveNoOrderOrTransitionReturnChannel()
		{
			var controller = new StealthLifecycleController();
			var services = new PassiveServiceProbe();
			var diagnostics = new DiagnosticProbe();
			var context = new StealthLifecycleContext(controller, services, services, services, diagnostics);

			context.Observe(Frame(90,
				new StealthLifecycleObservation(StealthLifecycleObservationKind.Damage, 9)));

			Assert.That(context.Enabled, Is.False);
			Assert.That(context.State.Owner, Is.EqualTo(BehaviorId.Start));
			Assert.That(context.State.Epoch, Is.EqualTo(new OwnershipEpoch(1)));
			Assert.That(services.ObservationCount, Is.EqualTo(3));
			Assert.That(diagnostics.RecordCount, Is.EqualTo(1));
			Assert.That(diagnostics.LastRecord.Tick, Is.EqualTo(90));
			Assert.That(diagnostics.LastRecord.Owner, Is.EqualTo(BehaviorId.Start));
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.Start));
			Assert.That(controller.Epoch, Is.EqualTo(new OwnershipEpoch(1)));

			var passiveContracts = new[]
			{
				typeof(IStealthLifecycleCacheService),
				typeof(IStealthLifecycleThreatService),
				typeof(IStealthLifecycleRouteService),
				typeof(IStealthLifecycleDiagnosticService)
			};
			Assert.That(passiveContracts.SelectMany(type => type.GetMethods())
				.All(method => method.ReturnType == typeof(void)), Is.True);
			Assert.That(passiveContracts.SelectMany(type => type.GetMethods())
				.SelectMany(method => method.GetParameters())
				.All(parameter => parameter.ParameterType != typeof(StealthLifecycleController) &&
					parameter.ParameterType != typeof(StealthBehaviorHandoff) &&
					parameter.ParameterType != typeof(StealthStartResult)), Is.True);
		}

		[Test]
		public void SavePayloadRoundTripsOwnerEpochAndObservationTickWhileDisabled()
		{
			var controller = new StealthLifecycleController();
			var startResult = Start(controller).Execute(
				new StealthLifecycleObservation(StealthLifecycleObservationKind.UnitBuilt, 31),
				new[] { Member(31) });
			Assert.That(controller.TryAccept(startResult, out _), Is.True);
			controller.Observe(Frame(123, new StealthLifecycleObservation(
				StealthLifecycleObservationKind.RepairCompleted, 31)));

			var saved = controller.ExportState();
			var serialized = new List<MiniYamlNode> { saved.Serialize() }.WriteToString();
			var roundTripped = StealthLifecycleSavePayload.Deserialize(MiniYaml.FromString(serialized).Single());
			var restored = StealthLifecycleController.Restore(roundTripped);

			Assert.That(restored.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
			Assert.That(restored.Epoch, Is.EqualTo(new OwnershipEpoch(2)));
			Assert.That(restored.LastObservedTick, Is.EqualTo(123));
			Assert.That(new List<MiniYamlNode> { roundTripped.Serialize() }.WriteToString(),
				Is.EqualTo(serialized));
		}

		[Test]
		public void SavePayloadRejectsEveryInvalidPersistentInvariant()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new StealthLifecycleSavePayload(
				BehaviorId.Start, default(OwnershipEpoch), -1));
			Assert.Throws<InvalidOperationException>(() =>
				StealthLifecycleSavePayload.Deserialize(SaveNode(version: "2")));
			Assert.Throws<InvalidOperationException>(() =>
				StealthLifecycleSavePayload.Deserialize(SaveNode(enabled: "True")));
			Assert.Throws<InvalidOperationException>(() =>
				StealthLifecycleSavePayload.Deserialize(SaveNode(owner: "Unknown")));
			Assert.Throws<InvalidOperationException>(() =>
				StealthLifecycleSavePayload.Deserialize(SaveNode(epoch: "0")));
			Assert.Throws<InvalidOperationException>(() =>
				StealthLifecycleSavePayload.Deserialize(SaveNode(lastObservedTick: "-2")));
		}

		[TestCase(StealthLifecycleObservationKind.UnitBuilt)]
		[TestCase(StealthLifecycleObservationKind.RepairCompleted)]
		public void StartHandsNewAndRepairedMembersOnlyToSquadConstruction(
			StealthLifecycleObservationKind source)
		{
			var controller = new StealthLifecycleController();
			var result = Start(controller).Execute(new StealthLifecycleObservation(source, 12),
				new[] { Member(12) });

			Assert.That(result.HasTransition, Is.True);
			Assert.That(result.IsTerminated, Is.False);
			Assert.That(result.Source, Is.EqualTo(source));
			Assert.That(controller.TryAccept(result, out var handoff), Is.True);
			Assert.That(handoff.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
		}

		[Test]
		public void StartDeterministicallyNormalizesLiveMemberIdentityWithoutOrders()
		{
			var result = Start(new StealthLifecycleController()).Execute(
				new StealthLifecycleObservation(StealthLifecycleObservationKind.UnitBuilt, 12),
				new[]
				{
					Member(12), Member(7), Member(12), Member(4, isInWorld: false),
					Member(5, isDead: true), Member(0), Member(9)
				});

			Assert.That(result.MemberActorIds, Is.EqualTo(new uint[] { 7, 9, 12 }));
			Assert.That(typeof(StealthStartResult).GetProperties()
				.All(property => !property.PropertyType.Name.Contains("Order", StringComparison.Ordinal)), Is.True);
			Assert.That(typeof(StealthStartBehavior).GetMethods()
				.Where(method => method.DeclaringType == typeof(StealthStartBehavior))
				.All(method => !method.ReturnType.Name.Contains("Order", StringComparison.Ordinal)), Is.True);
		}

		[Test]
		public void ExternalObservationsCannotProduceAStartTransition()
		{
			var controller = new StealthLifecycleController();
			var start = Start(controller);
			foreach (var source in new[]
			{
				StealthLifecycleObservationKind.Timer,
				StealthLifecycleObservationKind.Damage,
				StealthLifecycleObservationKind.WorldEvent
			})
			{
				var result = start.Execute(new StealthLifecycleObservation(source, 8), new[] { Member(8) });
				Assert.That(result.Disposition, Is.EqualTo(StealthStartDisposition.ObservationOnly));
				Assert.That(result.MemberActorIds, Is.Empty);
				Assert.That(controller.TryAccept(result, out var handoff), Is.False);
				Assert.That(handoff, Is.Null);
			}

			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.Start));
			Assert.That(controller.Epoch, Is.EqualTo(new OwnershipEpoch(1)));
		}

		[Test]
		public void InvalidOrDeletedStartInputTerminatesStructurally()
		{
			var controller = new StealthLifecycleController();
			var start = Start(controller);
			var invalidResults = new[]
			{
				start.Execute(new StealthLifecycleObservation(
					StealthLifecycleObservationKind.UnitBuilt), Array.Empty<StealthStartMemberSnapshot>()),
				start.Execute(new StealthLifecycleObservation(
					StealthLifecycleObservationKind.UnitBuilt, 4), new[] { Member(4, isDead: true) }),
				start.Execute(new StealthLifecycleObservation(
					StealthLifecycleObservationKind.RepairCompleted, 6), new[] { Member(7) })
			};

			foreach (var result in invalidResults)
			{
				Assert.That(result.IsTerminated, Is.True);
				Assert.That(result.HasTransition, Is.False);
				Assert.That(result.MemberActorIds, Is.Empty);
				Assert.That(controller.TryAccept(result, out var handoff), Is.False);
				Assert.That(handoff, Is.Null);
			}

			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.Start));
		}

		[Test]
		public void StartPrivateStateRoundTripsTheTypedPendingHandoff()
		{
			var controller = new StealthLifecycleController();
			var start = Start(controller);
			var result = start.Execute(new StealthLifecycleObservation(
				StealthLifecycleObservationKind.RepairCompleted, 17),
				new[] { Member(21), Member(17), Member(21) });

			var serialized = new List<MiniYamlNode> { start.SerializePrivateState(result) }.WriteToString();
			var restored = start.RestorePrivateState(MiniYaml.FromString(serialized).Single());

			Assert.That(restored.Source, Is.EqualTo(StealthLifecycleObservationKind.RepairCompleted));
			Assert.That(restored.SubjectActorId, Is.EqualTo(17));
			Assert.That(restored.MemberActorIds, Is.EqualTo(new uint[] { 17, 21 }));
			Assert.That(controller.TryAccept(restored, out var handoff), Is.True);
			Assert.That(handoff.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
		}

		[Test]
		public void StartPrivateStateCannotRebindAcrossOwnershipEpochs()
		{
			var original = new StealthLifecycleController();
			var originalStart = Start(original);
			var result = originalStart.Execute(new StealthLifecycleObservation(
				StealthLifecycleObservationKind.UnitBuilt, 17), new[] { Member(17) });
			var serialized = new List<MiniYamlNode> { originalStart.SerializePrivateState(result) }.WriteToString();
			var newer = StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
				BehaviorId.Start, new OwnershipEpoch(2), -1));

			Assert.Throws<InvalidOperationException>(() =>
				Start(newer).RestorePrivateState(MiniYaml.FromString(serialized).Single()));
			Assert.That(newer.Owner, Is.EqualTo(BehaviorId.Start));
			Assert.That(newer.Epoch, Is.EqualTo(new OwnershipEpoch(2)));
		}

		[Test]
		public void OnlyATypedStartResultCanEnterSquadConstruction()
		{
			var controller = new StealthLifecycleController();
			var tryAccept = typeof(StealthLifecycleController).GetMethods()
				.Single(method => method.Name == nameof(StealthLifecycleController.TryAccept) &&
					method.GetParameters()[0].ParameterType == typeof(StealthStartResult));

			Assert.That(tryAccept.GetParameters()[0].ParameterType, Is.EqualTo(typeof(StealthStartResult)));
			Assert.That(typeof(StealthStartResult).GetConstructors(), Is.Empty);
			Assert.Throws<ArgumentException>(() => new StealthStartBehavior(
				StealthLifecycleController.Restore(new StealthLifecycleSavePayload(
					BehaviorId.SquadConstruction, new OwnershipEpoch(2), -1)).CurrentHandoff));
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.Start));
		}
	}
}
