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
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public sealed class StealthLifecycleRuntimeTest
	{
		sealed class PassiveServices : IStealthLifecycleCacheService,
			IStealthLifecycleThreatService, IStealthLifecycleRouteService,
			IStealthLifecycleDiagnosticService
		{
			public void Observe(StealthLifecycleObservationFrame frame) { }
			public void Record(StealthLifecycleDiagnostic diagnostic) { }
		}

		sealed class MutableGuard : IStealthLifecycleOwnershipGuard
		{
			public BehaviorId Owner;
			public OwnershipEpoch Epoch;

			public bool IsActive(BehaviorId owner, OwnershipEpoch epoch)
			{
				return owner == Owner && epoch == Epoch;
			}
		}

		sealed class OrderTarget : IStealthLifecycleRuntimeOrderTarget
		{
			public int Count;
			public readonly List<StealthLifecycleRuntimeOrder> Prepared =
				new List<StealthLifecycleRuntimeOrder>();
			public bool Throw;
			public bool ThrowDuringPrepare;
			public Action OnApply;

			public Action Prepare(StealthLifecycleRuntimeOrder order)
			{
				if (ThrowDuringPrepare)
					throw new InvalidOperationException("preflight failure");
				Prepared.Add(order);
				return () =>
				{
					Count++;
					OnApply?.Invoke();
					if (Throw)
						throw new InvalidOperationException("ambiguous callback failure");
				};
			}
		}

		sealed class Cache : IStealthTargetAcquisitionCache,
			IStealthSquadConstructionSafetyService
		{
			public StealthTargetAcquisitionCacheSnapshot ReadSnapshot()
			{
				var cells = new List<CPos>();
				var targets = new List<StealthStrategicTargetSnapshot>();
				var facts = new List<StealthTargetThreatFacts>();
				for (var i = 1; i <= 10; i++)
				{
					var cell = new CPos(i, 1);
					cells.Add(cell);
					targets.Add(new StealthStrategicTargetSnapshot((uint)(100 + i), cell,
						100 + i, 1000, 100, 100));
					facts.Add(new StealthTargetThreatFacts(cell,
						new[] { new StealthCombatGroupSnapshot("stnk", 4, 3600) },
						new[] { new StealthCombatGroupSnapshot("e3", 1, 100) }, true, false, true));
				}

				return new StealthTargetAcquisitionCacheSnapshot(12, 3, new float[36],
					cells, .1f, targets, facts);
			}

			public bool TryFindSafeRoute(uint actorId, CPos originStrategicCell,
				CPos destinationStrategicCell, out IReadOnlyList<CPos> routeStrategicCells)
			{
				routeStrategicCells = new[] { destinationStrategicCell };
				return true;
			}
		}

		sealed class FakeOwner : IStealthLifecycleRuntimeOwner,
			IStealthLifecycleRuntimeDamageOwner
		{
			readonly Func<object> execute;
			readonly Func<StealthLifecycleDamageObservation, long,
				StealthLifecycleDamageYield> captureDamage;
			public BehaviorId OwnerId { get; }
			public BehaviorId Owner => OwnerId;
			public OwnershipEpoch Epoch { get; }

			public FakeOwner(BehaviorId owner, OwnershipEpoch epoch, Func<object> execute,
				Func<StealthLifecycleDamageObservation, long,
					StealthLifecycleDamageYield> captureDamage = null)
			{
				OwnerId = owner;
				Epoch = epoch;
				this.execute = execute;
				this.captureDamage = captureDamage;
			}

			public object Execute() { return execute(); }
			public bool TryCaptureDamage(StealthLifecycleDamageObservation observation, long eventId,
				out StealthLifecycleDamageYield yielded)
			{
				yielded = captureDamage?.Invoke(observation, eventId);
				return yielded != null;
			}

			public MiniYamlNode Serialize(string key = "ActiveOwner")
			{
				return new MiniYamlNode(key, Owner.ToString(), new List<MiniYamlNode>());
			}
		}

		sealed class Factory : IStealthLifecycleRuntimeOwnerFactory
		{
			readonly Cache cache = new Cache();
			readonly bool defendedApproach;

			public Factory(IStealthLifecycleRuntimeOrderTarget target, bool defendedApproach = false)
			{
				this.defendedApproach = defendedApproach;
			}

			public IStealthLifecycleRuntimeOwner Create(StealthLifecycleRuntimeEntry entry,
				IStealthLifecycleOwnershipGuard guard, IStealthLifecycleRuntimeOrders orders)
			{
				switch (entry.Owner)
				{
					case BehaviorId.Start:
						var start = new StealthStartBehavior(entry.Handoff);
						return new FakeOwner(entry.Owner, entry.Epoch, () => start.Execute(
							new StealthLifecycleObservation(StealthLifecycleObservationKind.UnitBuilt, 7),
							new[] { new StealthStartMemberSnapshot(7) }));
					case BehaviorId.SquadConstruction:
						Assert.That(entry.Context, Is.TypeOf<StealthStartResult>());
						var construction = new StealthSquadConstructionBehavior(entry.Handoff,
							new[] { 7u }, cache);
						return new FakeOwner(entry.Owner, entry.Epoch, () => construction.Execute(
							new[] { new StealthSquadConstructionMemberSnapshot(7, new CPos(1, 1), 0) },
							new[] { new StealthSquadConstructionSquadSnapshot(0, new CPos(1, 1)) }));
					case BehaviorId.TargetAcquisition:
						return Acquisition(entry.Handoff, orders);
					case BehaviorId.TargetValueFilter:
						var value = new StealthTargetValueFilterBehavior(
							(StealthTargetValueFilterHandoff)entry.Context);
						return new FakeOwner(entry.Owner, entry.Epoch, value.Execute);
					case BehaviorId.TargetThreatFilter:
						var threat = new StealthTargetThreatFilterBehavior(
							(StealthTargetThreatFilterHandoff)entry.Context,
							new FixedThreat());
						return new FakeOwner(entry.Owner, entry.Epoch, threat.Execute);
					case BehaviorId.TargetDistanceChoice:
						var distance = new StealthTargetDistanceChoiceBehavior(
							(StealthTargetDistanceChoiceHandoff)entry.Context,
							Array.Empty<StealthActiveSquadTargetSnapshot>(),
							new StealthTargetDistanceChoicePolicy(1000, 3000));
						return new FakeOwner(entry.Owner, entry.Epoch, distance.Execute);
					case BehaviorId.Approach:
						var approachWorld = new ApproachWorld(defendedApproach);
						var approach = new StealthApproachBehavior((StealthApproachHandoff)entry.Context,
							approachWorld, approachWorld, new FixedThreat(), approachWorld);
						return new FakeOwner(entry.Owner, entry.Epoch, approach.Execute);
					case BehaviorId.UndefendedAttack:
					case BehaviorId.CrushEvaluation:
						return new FakeOwner(entry.Owner, entry.Epoch, () =>
							throw new InvalidOperationException("Combat execution is outside this branch fixture."));
					default:
						throw new InvalidOperationException();
				}
			}

			public IStealthLifecycleRuntimeOwner Restore(StealthBehaviorHandoff handoff,
				IStealthLifecycleOwnershipGuard guard, IStealthLifecycleRuntimeOrders orders,
				MiniYamlNode privateState)
			{
				if (handoff.Owner == BehaviorId.Start)
				{
					var start = new StealthStartBehavior(handoff);
					return new FakeOwner(handoff.Owner, handoff.Epoch, () => start.Execute(
						new StealthLifecycleObservation(StealthLifecycleObservationKind.UnitBuilt, 7),
						new[] { new StealthStartMemberSnapshot(7) }));
				}

				Assert.That(handoff.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
				return Acquisition(handoff, orders);
			}

			IStealthLifecycleRuntimeOwner Acquisition(StealthBehaviorHandoff handoff,
				IStealthLifecycleRuntimeOrders orders)
			{
				var acquisition = new StealthTargetAcquisitionBehavior(handoff, cache);
				return new FakeOwner(handoff.Owner, handoff.Epoch, () =>
				{
					orders.Issue(Order(handoff.Owner, handoff.Epoch, "scan"));
					return acquisition.Execute(new CPos(1, 1));
				});
			}
		}

		sealed class ScriptedFactory : IStealthLifecycleRuntimeOwnerFactory
		{
			readonly Func<StealthBehaviorHandoff, object> restoredExecution;
			readonly Func<StealthBehaviorHandoff, StealthLifecycleDamageObservation, long,
				StealthLifecycleDamageYield> captureDamage;
			public StealthLifecycleRuntimeEntry LastCreated { get; private set; }

			public ScriptedFactory(Func<StealthBehaviorHandoff, object> restoredExecution,
				Func<StealthBehaviorHandoff, StealthLifecycleDamageObservation, long,
					StealthLifecycleDamageYield> captureDamage = null)
			{
				this.restoredExecution = restoredExecution;
				this.captureDamage = captureDamage;
			}

			public IStealthLifecycleRuntimeOwner Create(StealthLifecycleRuntimeEntry entry,
				IStealthLifecycleOwnershipGuard guard, IStealthLifecycleRuntimeOrders orders)
			{
				LastCreated = entry;
				return new FakeOwner(entry.Owner, entry.Epoch, () =>
					throw new InvalidOperationException("The installed owner is not executed by this handoff fixture."));
			}

			public IStealthLifecycleRuntimeOwner Restore(StealthBehaviorHandoff handoff,
				IStealthLifecycleOwnershipGuard guard, IStealthLifecycleRuntimeOrders orders,
				MiniYamlNode privateState)
			{
				return new FakeOwner(handoff.Owner, handoff.Epoch, () => restoredExecution(handoff),
					(observation, eventId) => captureDamage?.Invoke(handoff, observation, eventId));
			}
		}

		sealed class FixedThreat : IStealthTargetThreatAdapter
		{
			public StealthTargetThreatScore Calculate(StealthTargetThreatFacts facts)
			{
				return new StealthTargetThreatScore(1, 2);
			}
		}

		sealed class ApproachWorld : IStealthApproachLiveWorld,
			IStealthApproachStrategicCache, IStealthApproachStrategicRouteCache,
			IStealthApproachMovementOrders
		{
			readonly bool defended;

			public ApproachWorld(bool defended)
			{
				this.defended = defended;
			}

			public StealthApproachLiveSnapshot Read(StealthApproachMission mission)
			{
				return new StealthApproachLiveSnapshot(true,
					new[] { new StealthApproachMemberSnapshot(7, mission.StrategicCell) },
					new[] { new StealthCombatGroupSnapshot("stnk", 1, 900) },
					new[] { new StealthCombatGroupSnapshot("e3", 1, 100) },
					defended ? new[] { 201u } : Array.Empty<uint>(), true, false, true);
			}

			public StealthApproachStrategicCacheSnapshot ReadSnapshot()
			{
				throw new InvalidOperationException("An arrived Approach must not read strategic routing.");
			}

			public IReadOnlyList<CPos> ReadRoute(CPos origin, CPos destination)
			{
				throw new InvalidOperationException("An arrived Approach must not read strategic routing.");
			}

			public void IssueMove(BehaviorId owner, OwnershipEpoch epoch,
				IReadOnlyList<uint> actorIds, CPos destinationStrategicCell)
			{
				throw new InvalidOperationException("An arrived Approach must not issue movement.");
			}
		}

		static StealthLifecycleRuntimeOrder Order(BehaviorId owner, OwnershipEpoch epoch,
			string action)
		{
			return new StealthLifecycleRuntimeOrder(owner, epoch,
				StealthLifecycleRuntimeOrderKind.Move, action, new[] { 7u },
				targetCell: new CPos(2, 2));
		}

		static T CreateInternal<T>(params object[] arguments)
		{
			return (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance | BindingFlags.NonPublic,
				null, arguments, null);
		}

		static StealthApproachMission Mission()
		{
			var cell = new CPos(8, 0);
			var target = new StealthStrategicTargetSnapshot(201, cell, 100, 1000, 100, 100);
			var facts = new StealthTargetThreatFacts(cell,
				new[] { new StealthCombatGroupSnapshot("stnk", 2, 900) },
				new[] { new StealthCombatGroupSnapshot("mtnk", 1, 800) }, true, true, true);
			var option = CreateInternal<StealthTargetOption>(cell, (int?)100, false,
				new[] { target }, facts);
			var value = CreateInternal<StealthTargetValueOption>(option, 1000L);
			var threat = CreateInternal<StealthTargetThreatOption>(value,
				new StealthTargetThreatScore(5, 3));
			return CreateInternal<StealthApproachMission>(threat, 100L, 20, 80L);
		}

		static StealthRepairResumeContext Resume(BehaviorId owner, OwnershipEpoch epoch)
		{
			var mission = Mission();
			if (owner != BehaviorId.MassAttack)
				return CreateInternal<StealthRepairResumeContext>(owner, epoch, mission,
					new[] { 7u }, new[] { 201u }, (uint?)201, (CPos?)mission.StrategicCell,
					"fight-context");

			var evidence = CreateInternal<StealthMassAttackEntryEvidence>("mass-entry", 201u,
				mission.StrategicCell, new[] { 7u }, new[] { 201u }, true,
				new StealthTargetThreatScore(3, 3));
			return CreateInternal<StealthRepairResumeContext>(owner, epoch, mission,
				new[] { 7u }, new[] { 201u }, (uint?)201, (CPos?)mission.StrategicCell,
				"fight-context", evidence);
		}

		static MiniYamlNode RuntimeState(BehaviorId owner, OwnershipEpoch epoch)
		{
			return new MiniYamlNode("StealthLifecycleRuntime", "", new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", "1"), new MiniYamlNode("Enabled", "True"),
				new MiniYamlNode("Owner", owner.ToString()),
				new MiniYamlNode("Epoch", epoch.Value.ToString()),
				new MiniYamlNode("LastObservedTick", "-1"),
				new MiniYamlNode("NextDamageEventId", "1"),
				new MiniYamlNode("ActiveOwner", owner.ToString()),
				new MiniYamlNode("OrderSink", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("Version", "1"), new MiniYamlNode("Owner", owner.ToString()),
					new MiniYamlNode("Epoch", epoch.Value.ToString()),
					new MiniYamlNode("AcceptedFingerprint", "")
				})
			});
		}

		static StealthLifecycleRuntime RestoreScripted(BehaviorId owner,
			Func<StealthBehaviorHandoff, object> execution, out ScriptedFactory factory)
		{
			var epoch = new OwnershipEpoch(11);
			factory = new ScriptedFactory(execution);
			var target = new OrderTarget();
			var passive = new PassiveServices();
			return StealthLifecycleRuntime.Restore(RuntimeState(owner, epoch), factory,
				target, passive, passive, passive, passive);
		}

		static StealthKiteResult KiteFallback(StealthBehaviorHandoff handoff, bool mass)
		{
			var mission = Mission();
			var facts = new StealthKiteFallbackFacts(201, mission.StrategicCell,
				new[] { 7u }, new[] { 201u }, true);
			var evidence = CreateInternal<StealthKiteFallbackEvidence>(
				StealthKiteFallbackReason.NoSafePlan, "kite-fallback", new[] { 201u }, facts,
				(StealthTargetThreatScore?)new StealthTargetThreatScore(3, mass ? 3 : 2));
			return CreateInternal<StealthKiteResult>(handoff, mission,
				mass ? StealthKiteDisposition.MassAttack : StealthKiteDisposition.RecalculateFlee,
				StealthKitePhase.Position, (uint?)201, (CPos?)mission.StrategicCell,
				(CPos?)null, (CPos?)null, new[] { 7u }, new[] { 201u }, Array.Empty<uint>(),
				(StealthKiteSafetyResult?)null, evidence);
		}

		static StealthMassAttackHandoff MassSource(StealthBehaviorHandoff handoff)
		{
			var mission = Mission();
			var evidence = CreateInternal<StealthMassAttackEntryEvidence>("mass-entry", 201u,
				mission.StrategicCell, new[] { 7u }, new[] { 201u }, true,
				new StealthTargetThreatScore(3, 3));
			return CreateInternal<StealthMassAttackHandoff>(handoff, mission, evidence);
		}

		static StealthMassAttackResult MassResult(StealthBehaviorHandoff handoff,
			StealthMassAttackDisposition disposition)
		{
			var source = MassSource(handoff);
			if (disposition != StealthMassAttackDisposition.RecalculateFlee)
				return CreateInternal<StealthMassAttackResult>(source, source.Mission, disposition,
					StealthMassAttackPhase.Advance, (uint?)null, (CPos?)null, new[] { 7u },
					Array.Empty<uint>(), disposition == StealthMassAttackDisposition.UndefendedAttack ?
						new[] { 201u } : Array.Empty<uint>(), null,
					(StealthMassAttackThreatResult?)null, null);

			var enemy = new StealthMassAttackActorSnapshot(201, "mtnk",
				source.Mission.StrategicCell, 100, 100, 4, true, false, true);
			var facts = new StealthMassAttackThreatFacts(201, source.Mission.StrategicCell,
				new[] { 7u }, new[] { enemy }, true);
			return CreateInternal<StealthMassAttackResult>(source, source.Mission, disposition,
				StealthMassAttackPhase.Attack, (uint?)201, (CPos?)source.Mission.StrategicCell,
				new[] { 7u }, new[] { 201u }, Array.Empty<uint>(), facts,
				(StealthMassAttackThreatResult?)new StealthMassAttackThreatResult(
					new StealthTargetThreatScore(3, 1), 3), null);
		}

		static StealthRecalculateFleeResult CompletedFlee(StealthBehaviorHandoff handoff)
		{
			var mission = Mission();
			var evidence = CreateInternal<StealthRecalculateFleeEntryEvidence>(
				StealthRecalculateFleeSource.KiteNoSafePlan,
				new OwnershipEpoch(handoff.Epoch.Value - 1), "flee-entry", 201u,
				mission.StrategicCell, new[] { 7u }, new[] { 201u }, true,
				new StealthTargetThreatScore(3, 2));
			var source = CreateInternal<StealthRecalculateFleeHandoff>(handoff, mission, evidence);
			var cell = new CPos(2, 2);
			var candidate = new StealthRecalculateFleeCandidateSnapshot(cell, true);
			var member = new StealthRecalculateFleeMemberSnapshot(7, cell, 4);
			var enemy = new StealthRecalculateFleeEnemySnapshot(201, "mtnk",
				mission.StrategicCell, 100, 100, 4, false);
			var facts = CreateInternal<StealthRecalculateFleeThreatFacts>(cell,
				new[] { member }, new[] { enemy }, true, false);
			var danger = new StealthTargetThreatScore(1, 1);
			var evaluation = CreateInternal<StealthRecalculateFleeRouteEvaluation>(candidate,
				facts, danger);
			var token = CreateInternal<StealthRecalculateFleeOrderToken>(BehaviorId.RecalculateFlee,
				handoff.Epoch, new[] { 7u }, cell, 1L, 1L);
			return CreateInternal<StealthRecalculateFleeResult>(source,
				StealthRecalculateFleeDisposition.TargetAcquisition,
				StealthRecalculateFleeLiveCause.Completed, new[] { 7u }, new[] { 201u },
				new[] { evaluation }, (CPos?)cell, (StealthTargetThreatScore?)danger,
				new[] { cell }, 0, token,
				"flee-live", (long?)1);
		}

		static StealthRepairHandoff RepairSource(StealthBehaviorHandoff repairHandoff,
			BehaviorId fightOwner)
		{
			var mission = Mission();
			var resumeEpoch = new OwnershipEpoch(repairHandoff.Epoch.Value - 2);
			StealthRepairResumeContext resume;
			if (fightOwner == BehaviorId.MassAttack)
			{
				var evidence = CreateInternal<StealthMassAttackEntryEvidence>("mass-entry", 201u,
					mission.StrategicCell, new[] { 7u, 8u }, new[] { 201u }, true,
					new StealthTargetThreatScore(3, 3));
				resume = CreateInternal<StealthRepairResumeContext>(fightOwner, resumeEpoch, mission,
					new[] { 7u, 8u }, new[] { 201u }, (uint?)201,
					(CPos?)mission.StrategicCell, "fight-context", evidence);
			}
			else
				resume = CreateInternal<StealthRepairResumeContext>(fightOwner, resumeEpoch, mission,
					new[] { 7u, 8u }, new[] { 201u }, (uint?)201,
					(CPos?)mission.StrategicCell, "fight-context");

			var damageHandoff = CreateInternal<StealthBehaviorHandoff>(BehaviorId.Damage,
				new OwnershipEpoch(resumeEpoch.Value + 1));
			var request = CreateInternal<StealthDamageRepairRequest>(damageHandoff, 77L, 5,
				201u, 25, new[]
				{
					new StealthRepairDamagedMember(7, 40, 100),
					new StealthRepairDamagedMember(8, 60, 100)
				}, resume);
			return CreateInternal<StealthRepairHandoff>(repairHandoff, request);
		}

		static StealthRepairResult RepairResult(StealthBehaviorHandoff handoff,
			BehaviorId fightOwner, StealthRepairDisposition disposition)
		{
			var source = RepairSource(handoff, fightOwner);
			var members = disposition == StealthRepairDisposition.SquadConstruction ?
				Array.Empty<uint>() : new[] { 7u, 8u };
			var completion = disposition == StealthRepairDisposition.Start ?
				CreateInternal<StealthRepairCompletionEvidence>(6, new[]
				{
					new StealthRepairDamagedMember(7, 100, 100),
					new StealthRepairDamagedMember(8, 100, 100)
				}) : null;
			var cause = disposition == StealthRepairDisposition.ResumeFight ?
				StealthRepairLiveCause.NoSafeRepair :
				disposition == StealthRepairDisposition.Start ?
					StealthRepairLiveCause.RepairComplete : StealthRepairLiveCause.NoLiveMembers;
			return CreateInternal<StealthRepairResult>(source, disposition, cause, members, members,
				new[] { 201u }, Array.Empty<StealthRepairRouteEvaluation>(), (uint?)null,
				(uint?)null, 0, (StealthTargetThreatScore?)null, null, completion,
				"repair-live", (long?)null);
		}

		[Test]
		public void RuntimeAdvancesExactlyOneAcceptedHandoffPerTick()
		{
			var target = new OrderTarget();
			var passive = new PassiveServices();
			var runtime = new StealthLifecycleRuntime(new Factory(target), target,
				passive, passive, passive, passive);

			Assert.That(runtime.Owner, Is.EqualTo(BehaviorId.Start));
			Assert.That(runtime.Tick(), Is.True);
			Assert.That(runtime.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
			Assert.That(runtime.Epoch.Value, Is.EqualTo(2));
			Assert.That(runtime.Tick(), Is.True);
			Assert.That(runtime.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
			Assert.That(runtime.Epoch.Value, Is.EqualTo(3));
		}

		[Test]
		public void StartCanOnlyBeSavedPristineAndTransitionsBeforeTickReturns()
		{
			var target = new OrderTarget();
			var passive = new PassiveServices();
			var factory = new Factory(target);
			var runtime = new StealthLifecycleRuntime(factory, target,
				passive, passive, passive, passive);
			var saved = runtime.Serialize();
			var restored = StealthLifecycleRuntime.Restore(saved, factory, target,
				passive, passive, passive, passive);

			Assert.That(restored.Owner, Is.EqualTo(BehaviorId.Start));
			Assert.That(restored.Tick(), Is.True);
			Assert.That(restored.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
		}

		[Test]
		public void RestoredOwnerUsesTheRestoredOrderSink()
		{
			var target = new OrderTarget();
			var passive = new PassiveServices();
			var factory = new Factory(target);
			var runtime = new StealthLifecycleRuntime(factory, target,
				passive, passive, passive, passive);
			runtime.Tick();
			runtime.Tick();
			var saved = runtime.Serialize();

			var restored = StealthLifecycleRuntime.Restore(saved, factory, target,
				passive, passive, passive, passive);
			Assert.That(restored.Tick(), Is.True);
			Assert.That(target.Count, Is.EqualTo(1));
		}

		[Test]
		public void StrategicOwnersAdvanceAcquisitionThroughApproachOneEpochAtATime()
		{
			var target = new OrderTarget();
			var passive = new PassiveServices();
			var runtime = new StealthLifecycleRuntime(new Factory(target), target,
				passive, passive, passive, passive);
			var owners = new[]
			{
				BehaviorId.SquadConstruction, BehaviorId.TargetAcquisition,
				BehaviorId.TargetValueFilter, BehaviorId.TargetThreatFilter,
				BehaviorId.TargetDistanceChoice, BehaviorId.Approach
			};
			for (var i = 0; i < owners.Length; i++)
			{
				Assert.That(runtime.Tick(), Is.True);
				Assert.That(runtime.Owner, Is.EqualTo(owners[i]));
				Assert.That(runtime.Epoch.Value, Is.EqualTo(i + 2));
			}
		}

		[TestCase(false, BehaviorId.UndefendedAttack)]
		[TestCase(true, BehaviorId.CrushEvaluation)]
		public void ApproachRoutesOnlyItsAcceptedLiveArrivalBranch(
			bool defended, BehaviorId expectedOwner)
		{
			var target = new OrderTarget();
			var passive = new PassiveServices();
			var runtime = new StealthLifecycleRuntime(new Factory(target, defended), target,
				passive, passive, passive, passive);
			for (var i = 0; i < 6; i++)
				Assert.That(runtime.Tick(), Is.True);

			Assert.That(runtime.Owner, Is.EqualTo(BehaviorId.Approach));
			Assert.That(runtime.Tick(), Is.True);
			Assert.That(runtime.Owner, Is.EqualTo(expectedOwner));
			Assert.That(runtime.Epoch.Value, Is.EqualTo(8));
		}

		[TestCase(BehaviorId.UndefendedAttack)]
		[TestCase(BehaviorId.CrushEvaluation)]
		[TestCase(BehaviorId.Kite)]
		[TestCase(BehaviorId.MassAttack)]
		public void EveryFightOwnerAloneYieldsDamageThroughRepair(BehaviorId fightOwner)
		{
			var epoch = new OwnershipEpoch(11);
			var resume = Resume(fightOwner, epoch);
			var factory = new ScriptedFactory(handoff =>
				CreateInternal<StealthLifecycleDamageYield>(handoff, 77L, 5, 201u, 25,
					new[] { new StealthRepairDamagedMember(7, 40, 100) }, resume));
			var target = new OrderTarget();
			var passive = new PassiveServices();
			var runtime = StealthLifecycleRuntime.Restore(RuntimeState(fightOwner, epoch),
				factory, target, passive, passive, passive, passive);

			Assert.That(runtime.Tick(), Is.True);
			Assert.That(runtime.Owner, Is.EqualTo(BehaviorId.Repair));
			Assert.That(runtime.Epoch.Value, Is.EqualTo(13));
			Assert.That(factory.LastCreated.Context, Is.TypeOf<StealthRepairHandoff>());
			var repair = (StealthRepairHandoff)factory.LastCreated.Context;
			Assert.That(repair.Resume, Is.SameAs(resume));
			Assert.That(repair.Resume.MassAttackEntryEvidence,
				fightOwner == BehaviorId.MassAttack ? Is.Not.Null : Is.Null);
		}

		[Test]
		public void UndefendedHandsCurrentDefendersToCrushThenCrushHandsThemToKite()
		{
			var runtime = RestoreScripted(BehaviorId.UndefendedAttack, handoff =>
				CreateInternal<StealthUndefendedAttackResult>(handoff, Mission(),
					StealthUndefendedAttackDisposition.CrushEvaluation, (uint?)201,
					new[] { 7u }, new[] { 201u }, (StealthUndefendedAttackSafetyResult?)null),
				out var factory);
			Assert.That(runtime.Tick(), Is.True);
			Assert.That(runtime.Owner, Is.EqualTo(BehaviorId.CrushEvaluation));
			Assert.That(factory.LastCreated.Context, Is.TypeOf<StealthCrushEvaluationHandoff>());

			runtime = RestoreScripted(BehaviorId.CrushEvaluation, handoff =>
				CreateInternal<StealthCrushResult>(handoff, Mission(), StealthCrushDisposition.Kite,
					(uint?)null, (CPos?)null, new[] { 7u }, new[] { 201u }, Array.Empty<uint>(),
					(StealthCrushSafetyResult?)null), out factory);
			Assert.That(runtime.Tick(), Is.True);
			Assert.That(runtime.Owner, Is.EqualTo(BehaviorId.Kite));
			Assert.That(factory.LastCreated.Context, Is.TypeOf<StealthKiteHandoff>());
		}

		[TestCase(true, BehaviorId.MassAttack)]
		[TestCase(false, BehaviorId.RecalculateFlee)]
		public void KiteRoutesCrossoverToMassOrFlee(bool mass, BehaviorId expectedOwner)
		{
			var runtime = RestoreScripted(BehaviorId.Kite,
				handoff => KiteFallback(handoff, mass), out var factory);
			Assert.That(runtime.Tick(), Is.True);
			Assert.That(runtime.Owner, Is.EqualTo(expectedOwner));
			Assert.That(factory.LastCreated.Context, mass ?
				Is.TypeOf<StealthMassAttackHandoff>() : Is.TypeOf<StealthRecalculateFleeHandoff>());
		}

		[TestCase(StealthMassAttackDisposition.UndefendedAttack, BehaviorId.UndefendedAttack)]
		[TestCase(StealthMassAttackDisposition.Reacquire, BehaviorId.TargetAcquisition)]
		[TestCase(StealthMassAttackDisposition.RecalculateFlee, BehaviorId.RecalculateFlee)]
		public void MassRoutesEachAcceptedTerminalResult(StealthMassAttackDisposition disposition,
			BehaviorId expectedOwner)
		{
			var runtime = RestoreScripted(BehaviorId.MassAttack,
				handoff => MassResult(handoff, disposition), out _);
			Assert.That(runtime.Tick(), Is.True);
			Assert.That(runtime.Owner, Is.EqualTo(expectedOwner));
		}

		[Test]
		public void CompletedFleeReturnsToTargetAcquisition()
		{
			var runtime = RestoreScripted(BehaviorId.RecalculateFlee, CompletedFlee, out _);
			Assert.That(runtime.Tick(), Is.True);
			Assert.That(runtime.Owner, Is.EqualTo(BehaviorId.TargetAcquisition));
		}

		[TestCase(BehaviorId.UndefendedAttack)]
		[TestCase(BehaviorId.CrushEvaluation)]
		[TestCase(BehaviorId.Kite)]
		[TestCase(BehaviorId.MassAttack)]
		public void RepairResumesTheExactFightOwnerAndContext(BehaviorId fightOwner)
		{
			var runtime = RestoreScripted(BehaviorId.Repair,
				handoff => RepairResult(handoff, fightOwner, StealthRepairDisposition.ResumeFight),
				out var factory);
			Assert.That(runtime.Tick(), Is.True);
			Assert.That(runtime.Owner, Is.EqualTo(fightOwner));
			var resumed = (StealthRepairFightResumeHandoff)factory.LastCreated.Context;
			Assert.That(resumed.Context.Owner, Is.EqualTo(fightOwner));
			Assert.That(resumed.Context.Epoch.Value, Is.EqualTo(9));
			Assert.That(resumed.Context.MemberActorIds, Is.EqualTo(new[] { 7u, 8u }));
			Assert.That(resumed.Context.MassAttackEntryEvidence,
				fightOwner == BehaviorId.MassAttack ? Is.Not.Null : Is.Null);
		}

		[Test]
		public void RepairCompletionBatchesAllRepairedMembersIntoStart()
		{
			var runtime = RestoreScripted(BehaviorId.Repair,
				handoff => RepairResult(handoff, BehaviorId.Kite, StealthRepairDisposition.Start),
				out var factory);
			Assert.That(runtime.Tick(), Is.True);
			Assert.That(runtime.Owner, Is.EqualTo(BehaviorId.Start));
			var transition = (StealthRepairTransition)factory.LastCreated.Context;
			Assert.That(transition.StartEntries.Count, Is.EqualTo(2));
			Assert.That(transition.StartEntries[0].ActorId, Is.EqualTo(7));
			Assert.That(transition.StartEntries[1].ActorId, Is.EqualTo(8));
		}

		[Test]
		public void RepairWithNoLiveMembersReturnsToSquadConstruction()
		{
			var runtime = RestoreScripted(BehaviorId.Repair,
				handoff => RepairResult(handoff, BehaviorId.Kite,
					StealthRepairDisposition.SquadConstruction), out var factory);
			Assert.That(runtime.Tick(), Is.True);
			Assert.That(runtime.Owner, Is.EqualTo(BehaviorId.SquadConstruction));
			Assert.That(factory.LastCreated.Context,
				Is.TypeOf<StealthSquadConstructionRecoveryHandoff>());
		}

		[TestCase(BehaviorId.TargetAcquisition)]
		[TestCase(BehaviorId.Approach)]
		[TestCase(BehaviorId.Kite)]
		[TestCase(BehaviorId.Repair)]
		public void RepresentativeOwnerRuntimeEnvelopeRoundTrips(BehaviorId owner)
		{
			var target = new OrderTarget();
			var passive = new PassiveServices();
			var factory = new ScriptedFactory(_ => new object());
			var first = StealthLifecycleRuntime.Restore(RuntimeState(owner, new OwnershipEpoch(11)),
				factory, target, passive, passive, passive, passive);
			var saved = first.Serialize();
			var second = StealthLifecycleRuntime.Restore(saved, factory, target,
				passive, passive, passive, passive);
			Assert.That(second.Owner, Is.EqualTo(owner));
			Assert.That(second.Epoch.Value, Is.EqualTo(11));
		}

		[Test]
		public void GateDefaultsOffAndHasOneAuthorityForAllLegacyCallbacks()
		{
			Assert.That(new SquadManagerBotModuleInfo().UseModularStealthLifecycle, Is.False);
			var gate = typeof(OpenRA.Mods.Common.Traits.BotModules.Squads.Squad).GetMethod(
				"LegacyStealthAuthorityAllowed",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(gate, Is.Not.Null);
			Assert.That(gate.Invoke(null, new object[] { false }), Is.True,
				"Disabled runtime must remain behavior-neutral for legacy authority.");
			Assert.That(gate.Invoke(null, new object[] { true }), Is.False,
				"Enabled runtime must reject all four callbacks sharing this gate.");
		}

		[Test]
		public void PassiveDamageRoundTripsThenTheMatchingFightOwnerConsumesIt()
		{
			var epoch = new OwnershipEpoch(11);
			var resume = Resume(BehaviorId.Kite, epoch);
			var factory = new ScriptedFactory(_ =>
				throw new InvalidOperationException("Pending Damage must precede normal execution."),
				(handoff, observed, eventId) => CreateInternal<StealthLifecycleDamageYield>(
					handoff, eventId, observed.Tick, observed.SourceActorId,
					observed.Amount, new[] { observed.DamagedMember }, resume));
			var target = new OrderTarget();
			var passive = new PassiveServices();
			var runtime = StealthLifecycleRuntime.Restore(RuntimeState(BehaviorId.Kite, epoch),
				factory, target, passive, passive, passive, passive);
			var observation = new StealthLifecycleDamageObservation(5, 201,
				resume.Mission.StrategicCell, 25, new StealthRepairDamagedMember(7, 40, 100));

			Assert.That(runtime.ObserveDamage(observation), Is.True);
			Assert.That(runtime.Owner, Is.EqualTo(BehaviorId.Kite),
				"Passive observation cannot select or transition an owner.");
			Assert.That(runtime.ObserveDamage(observation), Is.False,
				"A pending observation cannot queue a second transition.");
			var saved = runtime.Serialize();
			var restored = StealthLifecycleRuntime.Restore(saved, factory, target,
				passive, passive, passive, passive);
			Assert.That(restored.Owner, Is.EqualTo(BehaviorId.Kite));
			Assert.That(restored.Tick(), Is.True);
			Assert.That(restored.Owner, Is.EqualTo(BehaviorId.Repair));
			Assert.That(((StealthRepairHandoff)factory.LastCreated.Context).DamageEventId,
				Is.EqualTo(1));
		}

		[Test]
		public void OrderSinkReplacesCommittedTokenAndSuppressesDuplicateAfterRestore()
		{
			var epoch = new OwnershipEpoch(5);
			var guard = new MutableGuard { Owner = BehaviorId.Kite, Epoch = epoch };
			var target = new OrderTarget();
			var sink = new StealthLifecycleRuntimeOrders(guard, target, guard.Owner, epoch);
			var first = Order(guard.Owner, epoch, "first");
			var second = Order(guard.Owner, epoch, "second");

			sink.Issue(first);
			sink.Issue(first);
			sink.Issue(second);
			Assert.That(target.Count, Is.EqualTo(2));
			var saved = sink.Serialize();
			var restored = new StealthLifecycleRuntimeOrders(guard, target, guard.Owner, epoch);
			restored.Restore(saved, guard.Owner, epoch);
			restored.Issue(second);
			Assert.That(target.Count, Is.EqualTo(2));
		}

		[Test]
		public void RuntimeAdapterSelectsExactlyTheCurrentPersistedWaypoint()
		{
			var assembly = typeof(OpenRA.Mods.Common.Traits.BotModules.Squads.Squad).Assembly;
			var adapterType = assembly.GetType(
				"OpenRA.Mods.Common.Traits.BotModules.Squads.StealthSquadLifecycleOrders", true);
			var route = new[] { new CPos(1, 0), new CPos(3, 1), new CPos(5, 0) };

			var fleeEpoch = new OwnershipEpoch(5);
			var fleeGuard = new MutableGuard
			{
				Owner = BehaviorId.RecalculateFlee, Epoch = fleeEpoch
			};
			var fleeTarget = new OrderTarget();
			var fleeSink = new StealthLifecycleRuntimeOrders(fleeGuard, fleeTarget,
				fleeGuard.Owner, fleeEpoch);
			var fleeAdapter = (IStealthRecalculateFleeOrders)Activator.CreateInstance(adapterType,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				null, new object[] { fleeSink }, null);
			var fleeToken = CreateInternal<StealthRecalculateFleeOrderToken>(
				BehaviorId.RecalculateFlee, fleeEpoch, new[] { 7u }, route[1], 2L, 0L);
			fleeAdapter.IssueMove(BehaviorId.RecalculateFlee, fleeEpoch, new[] { 7u },
				route[2], route, 1, fleeToken);

			Assert.That(fleeTarget.Prepared.Single().TargetCell, Is.EqualTo(route[1]));
			Assert.That(fleeTarget.Prepared.Single().Route, Is.EqualTo(route));

			var repairEpoch = new OwnershipEpoch(6);
			var repairGuard = new MutableGuard { Owner = BehaviorId.Repair, Epoch = repairEpoch };
			var repairTarget = new OrderTarget();
			var repairSink = new StealthLifecycleRuntimeOrders(repairGuard, repairTarget,
				repairGuard.Owner, repairEpoch);
			var repairAdapter = (IStealthRepairOrders)Activator.CreateInstance(adapterType,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				null, new object[] { repairSink }, null);
			var repairToken = CreateInternal<StealthRepairOrderToken>(BehaviorId.Repair,
				repairEpoch, new[] { 7u }, 100u, 1000u,
				StealthRepairOrderKind.Retreat, 3L, 0L);
			repairAdapter.IssueRepair(BehaviorId.Repair, repairEpoch, new[] { 7u }, 100u,
				route, 1, StealthRepairOrderKind.Retreat, repairToken);

			Assert.That(repairTarget.Prepared.Single().TargetCell, Is.EqualTo(route[1]));
			Assert.That(repairTarget.Prepared.Single().Route, Is.EqualTo(route));
			var concrete = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/" +
				"StealthSquadLifecycleOrders.cs");
			Assert.That(concrete.Split("QueueOrder(").Length - 1, Is.EqualTo(1),
				"IBot exposes no atomic batch callback, so each token must queue one grouped order.");
		}

		[Test]
		public void SerializedAuthorityShapeMatrixIsGateIndependentAndExhaustive()
		{
			var authority = typeof(OpenRA.Mods.Common.Traits.BotModules.Squads.Squad).Assembly.GetType(
				"OpenRA.Mods.Common.Traits.BotModules.Squads." +
				"StealthSquadLifecycleAuthorityPersistence", true);
			var validate = authority.GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic);
			var legacy = ((IEnumerable<string>)authority.GetField("LegacyAuthorityKeys",
				BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).ToArray();
			var required = new[]
			{
				"Type", "Units", "StealthSquadDefinition", "StealthSquadIndex",
				"StealthLifecycleRuntime"
			};
			var optional = new[]
			{
				"Target", "AirSquadDefinition", "AirUnitsRepairing", "AirReinforcements",
				"AirFormationCenter", "GroundReinforcements", "GroundFormationCenter"
			};
			MiniYaml Shape(params string[] keys) => new MiniYaml("", keys.Select(key =>
				new MiniYamlNode(key, key == "StealthLifecycleRuntime" ? "" : "0")).ToList());
			MiniYaml Modular(params string[] extras) => Shape(required.Concat(extras).ToArray());
			void Accept(MiniYaml yaml, bool modular) => validate.Invoke(null, new object[] { yaml, modular });
			void Reject(MiniYaml yaml, bool modular)
			{
				var shape = new MiniYamlNode("Shape", yaml);
				var before = new List<MiniYamlNode> { shape }.WriteToString();
				var error = Assert.Throws<TargetInvocationException>(() => Accept(yaml, modular));
				Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
				Assert.That(new List<MiniYamlNode> { shape }.WriteToString(), Is.EqualTo(before));
			}

			Assert.DoesNotThrow(() => Accept(Shape(legacy), false));
			Reject(Shape("StealthLifecycleRuntime"), false);
			Assert.DoesNotThrow(() => Accept(Modular(), true));
			Assert.DoesNotThrow(() => Accept(Modular(optional), true));
			Assert.DoesNotThrow(() => Accept(Shape(required.Reverse().ToArray()), true));
			Reject(Shape(), true);
			foreach (var missing in required)
				Reject(Shape(required.Where(key => key != missing).ToArray()), true);
			foreach (var key in legacy)
			{
				Reject(Modular(key), true);
				Reject(Shape(key), true);
			}

			Reject(Modular(legacy), true);
			Reject(Modular(legacy.Where(key => key != "AirEscapingLocalAa").ToArray()), true);
			Reject(Modular("FutureRuntimeAuthority"), true);
			Reject(Modular("StealthEscapeFutureOrderToken"), true);
			Reject(Modular("Units"), true);
			Reject(Modular("Target", "Target"), true);
			Reject(Modular("StealthLifecycleRuntime"), true);
		}

		[Test]
		public void AuthorityShapeValidationPrecedesSquadConstructionAndLiveLookups()
		{
			var source = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/Squad.cs");
			var squad = source.Substring(source.IndexOf("public static Squad Deserialize",
				StringComparison.Ordinal));
			var validate = squad.IndexOf("StealthSquadLifecycleAuthorityPersistence.Validate(yaml",
				StringComparison.Ordinal);
			var construct = squad.IndexOf("var squad = new Squad", StringComparison.Ordinal);
			var lookup = squad.IndexOf("squadManager.World.GetActorById", StringComparison.Ordinal);
			Assert.That(validate, Is.GreaterThan(0));
			Assert.That(validate, Is.LessThan(construct));
			Assert.That(validate, Is.LessThan(lookup));
		}

		[Test]
		public void CallbackFailureReservesTokenBeforeExternalApplication()
		{
			var epoch = new OwnershipEpoch(8);
			var guard = new MutableGuard { Owner = BehaviorId.Repair, Epoch = epoch };
			var target = new OrderTarget { Throw = true };
			var sink = new StealthLifecycleRuntimeOrders(guard, target, guard.Owner, epoch);
			var order = Order(guard.Owner, epoch, "repair");

			Assert.Throws<InvalidOperationException>(() => sink.Issue(order));
			target.Throw = false;
			sink.Issue(order);
			Assert.That(target.Count, Is.EqualTo(1));
		}

		[Test]
		public void PreflightFailureDoesNotReserveAndExactRetryCanApply()
		{
			var epoch = new OwnershipEpoch(8);
			var guard = new MutableGuard { Owner = BehaviorId.Repair, Epoch = epoch };
			var target = new OrderTarget { ThrowDuringPrepare = true };
			var sink = new StealthLifecycleRuntimeOrders(guard, target, guard.Owner, epoch);
			var order = Order(guard.Owner, epoch, "preflight");
			var before = new List<MiniYamlNode> { sink.Serialize() }.WriteToString();

			Assert.Throws<InvalidOperationException>(() => sink.Issue(order));
			Assert.That(target.Count, Is.Zero);
			Assert.That(new List<MiniYamlNode> { sink.Serialize() }.WriteToString(), Is.EqualTo(before));
			target.ThrowDuringPrepare = false;
			sink.Issue(order);
			Assert.That(target.Count, Is.EqualTo(1));
		}

		[Test]
		public void StaleEpochCannotReserveOrApplyAnOrder()
		{
			var sinkEpoch = new OwnershipEpoch(8);
			var guard = new MutableGuard
			{
				Owner = BehaviorId.Kite,
				Epoch = new OwnershipEpoch(9)
			};
			var target = new OrderTarget();
			var sink = new StealthLifecycleRuntimeOrders(guard, target, guard.Owner, sinkEpoch);
			var before = new List<MiniYamlNode> { sink.Serialize() }.WriteToString();

			Assert.Throws<InvalidOperationException>(() =>
				sink.Issue(Order(guard.Owner, sinkEpoch, "stale")));
			Assert.That(target.Count, Is.Zero);
			Assert.That(new List<MiniYamlNode> { sink.Serialize() }.WriteToString(), Is.EqualTo(before));
		}

		[Test]
		public void OwnershipLossDuringCallbackCannotDuplicateTheLogicalOrder()
		{
			var epoch = new OwnershipEpoch(8);
			var guard = new MutableGuard { Owner = BehaviorId.Kite, Epoch = epoch };
			var target = new OrderTarget();
			target.OnApply = () => guard.Epoch = new OwnershipEpoch(9);
			var sink = new StealthLifecycleRuntimeOrders(guard, target, guard.Owner, epoch);
			var order = Order(guard.Owner, epoch, "ownership-loss");

			Assert.Throws<InvalidOperationException>(() => sink.Issue(order));
			Assert.That(target.Count, Is.EqualTo(1));
			guard.Epoch = epoch;
			target.OnApply = null;
			sink.Issue(order);
			Assert.That(target.Count, Is.EqualTo(1));
		}

		[Test]
		public void ConcretePreflightRejectsLaterInvalidActorBeforePreparingOrQueueing()
		{
			var prepare = Preflight(typeof(string), typeof(string));
			var resolved = new List<uint>();
			var prepared = 0;
			Func<uint, string> resolver = id =>
			{
				resolved.Add(id);
				return id == 2 ? null : id.ToString();
			};
			Func<string[], string> builder = _ => { prepared++; return "order"; };

			var error = Assert.Throws<TargetInvocationException>(() =>
				prepare.Invoke(null, new object[] { new[] { 1u, 2u }, resolver, builder }));
			Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
			Assert.That(resolved, Is.EqualTo(new[] { 1u, 2u }));
			Assert.That(prepared, Is.Zero);
		}

		[Test]
		public void ConcretePreflightRejectsInvalidTargetPayloadBeforeQueueing()
		{
			var prepare = Preflight(typeof(string), typeof(string));
			var queued = 0;
			Func<uint, string> resolver = id => id.ToString();
			Func<string[], string> invalidTarget = _ =>
				throw new InvalidOperationException("target is not live");

			var error = Assert.Throws<TargetInvocationException>(() =>
				prepare.Invoke(null, new object[] { new[] { 1u, 2u }, resolver, invalidTarget }));
			Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
			Assert.That(queued, Is.Zero);
		}

		[Test]
		public void ConcreteStrategicFacadePreservesSharedCacheFactsAndSafeDetourRouting()
		{
			var adapter = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/" +
				"StealthSquadLifecycleStrategicAdapter.cs");
			var facade = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/" +
				"StealthAILifecycleStrategicFacade.cs");
			var established = Source("OpenRA.Mods.Common/Traits/BotModules/Squads/States/" +
				"StealthAIStates.cs");
			var approach = Source("OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/" +
				"StealthLifecycle/StealthApproachBehavior.cs");
			Assert.That(adapter, Does.Not.Contain("Rebuild("));
			Assert.That(adapter, Does.Not.Contain("CardinalRoute"));
			Assert.That(adapter, Does.Not.Contain(" Priority("));
			Assert.That(adapter, Does.Not.Contain(" Group("));
			Assert.That(adapter, Does.Contain("TryReadLifecycleStrategicView"),
				"Enabled observation must passively refresh the established shared cache.");
			Assert.That(facade, Does.Contain("StealthInfluence(owner, representative)"));
			Assert.That(facade, Does.Contain("candidate.Priority"));
			Assert.That(facade, Does.Contain("EconomicValue(actor)"));
			Assert.That(facade, Does.Contain("cache.CloakedDanger : cache.Danger"));
			Assert.That(facade, Does.Contain("cache.ThreatCoverageByCell"));
			Assert.That(facade, Does.Contain("owner.StealthDefinition.RouteThreatPenalty"));
			Assert.That(facade, Does.Contain("StealthRouteToCell(owner, actor, cache, destination)"));
			Assert.That(facade, Does.Contain("ReadLifecycleApproachRoute"));
			Assert.That(approach, Does.Not.Contain("ThreatAwareRoutePlanner.FindRoute"));
			Assert.That(approach, Does.Contain("routeCache.ReadRoute"));
			Assert.That(established, Does.Contain("IsHardRouteDanger"));
			Assert.That(established, Does.Contain("ThreatAwareRoutePlanner.SmoothRoute"));

			var danger = new float[15];
			danger[2] = 10;
			var route = ThreatAwareRoutePlanner.FindRoute(danger, 5, 3, 0, 0, 4, 0, 100);
			Assert.That(route, Is.Not.Null);
			Assert.That(route, Does.Not.Contain(new CPos(2, 0)),
				"The shared A* operation must select the safe detour, not X-then-Y routing.");
		}

		static string Source(string relativePath)
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Makefile")))
				directory = directory.Parent;
			if (directory == null)
				throw new InvalidOperationException("Could not locate repository root.");
			return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
		}

		static MethodInfo Preflight(Type actorType, Type orderType)
		{
			var type = typeof(OpenRA.Mods.Common.Traits.BotModules.Squads.Squad).Assembly.GetType(
				"OpenRA.Mods.Common.Traits.BotModules.Squads.StealthSquadLifecycleOrderPreflight", true);
			return type.GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)
				.MakeGenericMethod(actorType, orderType);
		}
	}
}
