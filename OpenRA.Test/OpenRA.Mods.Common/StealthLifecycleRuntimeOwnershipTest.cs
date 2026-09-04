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
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class StealthLifecycleRuntimeOwnershipTest
	{
		sealed class Passive : IStealthLifecycleCacheService, IStealthLifecycleThreatService,
			IStealthLifecycleRouteService, IStealthLifecycleDiagnosticService,
			IStealthLifecycleRuntimeOrderTarget
		{
			public void Observe(StealthLifecycleObservationFrame frame) { }
			public void Record(StealthLifecycleDiagnostic diagnostic) { }
			public Action Prepare(StealthLifecycleRuntimeOrder order) { return () => { }; }
		}

		sealed class FakeOwner : IStealthLifecycleRuntimeOwner
		{
			readonly Func<object> execute;
			public BehaviorId OwnerId { get; }
			public BehaviorId Owner => OwnerId;
			public OwnershipEpoch Epoch { get; }
			public FakeOwner(BehaviorId owner, OwnershipEpoch epoch, Func<object> execute)
			{
				OwnerId = owner;
				Epoch = epoch;
				this.execute = execute;
			}

			public object Execute() { return execute(); }
		}

		sealed class Factory : IStealthLifecycleRuntimeOwnerFactory
		{
			public Func<StealthLifecycleRuntimeEntry, object> Execute = entry => new object();
			public StealthLifecycleRuntimeEntry Created;
			public IStealthLifecycleRuntimeOwner Create(StealthLifecycleRuntimeEntry entry,
				IStealthLifecycleOwnershipGuard ownershipGuard, IStealthLifecycleRuntimeOrders orders)
			{
				Created = entry;
				return new FakeOwner(entry.Owner, entry.Epoch, () => Execute(entry));
			}
		}

		[Test]
		public void LoadedRuntimeStartsFreshAtTargetAcquisition()
		{
			var services = new Passive();
			var factory = new Factory();
			var runtime = new StealthLifecycleRuntime(BehaviorId.TargetAcquisition,
				factory, services, services, services, services, services);

			Assert.That(runtime.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
			Assert.That(factory.Created.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
			Assert.That(factory.Created.Context, Is.Null);
		}

		[Test]
		public void ObservationsCannotInterruptTheExecutingOwner()
		{
			var services = new Passive();
			var factory = new Factory();
			StealthLifecycleRuntime runtime = null;
			factory.Execute = entry =>
			{
				runtime.Observe(new StealthLifecycleObservationFrame(1,
					Array.Empty<StealthLifecycleObservation>()));
				return null;
			};
			runtime = new StealthLifecycleRuntime(BehaviorId.TargetAcquisition,
				factory, services, services, services, services, services);

			Assert.That(() => runtime.Tick(), Throws.InvalidOperationException.With.Message
				.EqualTo("Runtime observations cannot interrupt an active owner."));
			Assert.That(runtime.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
		}

		[Test]
		public void TacticalRuntimeHasNoSerializationSurface()
		{
			var forbidden = new[] { "Serialize", "Restore", "ExportState", "RestorePrivateState" };
			var methods = typeof(StealthLifecycleRuntime).GetMethods()
				.Concat(typeof(IStealthLifecycleRuntimeOwner).GetMethods())
				.Select(method => method.Name).ToArray();

			Assert.That(methods.Intersect(forbidden), Is.Empty);
		}

		[Test]
		public void ASecondHitCannotEraseThePendingDamageYield()
		{
			var handoff = Construct<StealthBehaviorHandoff>(
				BehaviorId.Kite, new OwnershipEpoch(1));
			var resume = Construct<StealthRepairResumeContext>(BehaviorId.Kite,
				new OwnershipEpoch(1), Mission(), new uint[] { 1 }, new uint[] { 71 },
				(uint?)71, (CPos?)new CPos(5, 0), "pending-damage");
			var damaged = new StealthRepairDamagedMember(1, 50, 100);
			var pending = Construct<StealthLifecycleDamageYield>(handoff, 1L, 5, 900u, 25,
				new[] { damaged }, resume);
			var captures = 0;
			var ownerType = typeof(StealthLifecycleRuntime).Assembly.GetType(
				"OpenRA.Mods.Common.Traits.BotModules.Squads.StealthSquadLifecycleRuntimeOwner", true);
			Func<object> execute = () => new object();
			Func<object, StealthLifecycleDamageObservation, long,
				StealthLifecycleDamageYield> capture = (result, hit, eventId) =>
				{
					captures++;
					return pending;
				};
			var owner = (IStealthLifecycleRuntimeOwner)Activator.CreateInstance(ownerType,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
				new object[] { BehaviorId.Kite, new OwnershipEpoch(1), execute, capture }, null);
			var damageOwner = (IStealthLifecycleRuntimeDamageOwner)owner;
			var observation = new StealthLifecycleDamageObservation(
				5, 900, new CPos(4, 0), 25, damaged);

			Assert.That(damageOwner.TryCaptureDamage(observation, 1, out var first), Is.True);
			Assert.That(damageOwner.TryCaptureDamage(observation, 2, out var second), Is.False);
			Assert.That(first, Is.SameAs(pending));
			Assert.That(second, Is.Null);
			Assert.That(owner.Execute(), Is.SameAs(pending));
			Assert.That(captures, Is.EqualTo(1));
		}

		[Test]
		public void ApproachMayYieldDamageToRepairAndResumeItsMission()
		{
			var epoch = new OwnershipEpoch(1);
			var approachOwner = Construct<StealthBehaviorHandoff>(BehaviorId.Approach, epoch);
			var resume = Construct<StealthRepairResumeContext>(BehaviorId.Approach,
				epoch, Mission(), new uint[] { 1 }, new uint[] { 900 },
				(uint?)null, (CPos?)null, "approach-damage");
			var yielded = Construct<StealthLifecycleDamageYield>(approachOwner, 1L, 5, 900u, 25,
				new[] { new StealthRepairDamagedMember(1, 40, 100) }, resume);

			Assert.That(yielded.Resume.Owner, Is.EqualTo(BehaviorId.Approach));
			Assert.That(yielded.Resume.Mission, Is.SameAs(resume.Mission));
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

		static T Construct<T>(params object[] arguments)
		{
			return (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance |
				BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null);
		}
	}
}
