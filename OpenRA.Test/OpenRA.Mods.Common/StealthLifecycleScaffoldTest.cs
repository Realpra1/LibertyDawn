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

			var accepted = controller.TryAccept(
				StealthBehaviorResult.Complete(start, BehaviorId.SquadConstruction), out var handoff);

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
			var staleStartHandoff = controller.CurrentHandoff;
			Assert.That(controller.TryAccept(StealthBehaviorResult.Complete(
				staleStartHandoff, BehaviorId.SquadConstruction), out var currentHandoff), Is.True);

			Assert.That(controller.TryAccept(StealthBehaviorResult.Complete(
				staleStartHandoff, BehaviorId.Damage), out var rejectedHandoff), Is.False);
			Assert.That(rejectedHandoff, Is.Null);
			Assert.That(controller.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
			Assert.That(controller.Epoch, Is.EqualTo(new OwnershipEpoch(2)));

			Assert.That(controller.TryAccept(StealthBehaviorResult.Complete(
				currentHandoff, BehaviorId.TargetAcquisition), out var nextHandoff), Is.True);
			Assert.That(nextHandoff.Epoch, Is.EqualTo(new OwnershipEpoch(3)));
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
					parameter.ParameterType != typeof(StealthBehaviorResult)), Is.True);
		}

		[Test]
		public void SavePayloadRoundTripsOwnerEpochAndObservationTickWhileDisabled()
		{
			var controller = new StealthLifecycleController();
			Assert.That(controller.TryAccept(StealthBehaviorResult.Complete(
				controller.CurrentHandoff, BehaviorId.SquadConstruction), out var handoff), Is.True);
			Assert.That(controller.TryAccept(StealthBehaviorResult.Complete(
				handoff, BehaviorId.TargetAcquisition), out _), Is.True);
			controller.Observe(Frame(123, new StealthLifecycleObservation(
				StealthLifecycleObservationKind.RepairCompleted, 31)));

			var saved = controller.ExportState();
			var serialized = new List<MiniYamlNode> { saved.Serialize() }.WriteToString();
			var roundTripped = StealthLifecycleSavePayload.Deserialize(MiniYaml.FromString(serialized).Single());
			var restored = StealthLifecycleController.Restore(roundTripped);

			Assert.That(restored.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
			Assert.That(restored.Epoch, Is.EqualTo(new OwnershipEpoch(3)));
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
	}
}
