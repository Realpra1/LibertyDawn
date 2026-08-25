// Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
// This file is licensed under the GNU General Public License version 3 or later.

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Test-map-only driver that schedules a resource explosion in the strategic cell of an actor type.")]
	public sealed class StealthResourceExplosionTestDriverInfo : TraitInfo
	{
		public readonly string ActorType = "stnk";
		public readonly string ResourceType = "BlueTiberium";
		public readonly int ActorIndex;
		public readonly bool RequireNonLeadSquadMember;
		public readonly int TriggerTick = 100;
		public readonly int PendingDelayAfterFill;
		public readonly bool DamageOnlyNonOccupiedCell;
		public readonly bool ForceSafetyCacheRefreshBeforeDamage;
		public readonly int StrategicCellSize = 6;
		public readonly int Damage = 1000000;

		public override object Create(ActorInitializer init)
		{
			return new StealthResourceExplosionTestDriver(init.Self, this);
		}
	}

	public sealed class StealthResourceExplosionTestDriver : ITick
	{
		static readonly Dictionary<World, (Player Owner, CPos Coarse, int Tick, HashSet<uint> Members)> PendingByWorld =
			new Dictionary<World, (Player Owner, CPos Coarse, int Tick, HashSet<uint> Members)>();
		readonly Actor self;
		readonly StealthResourceExplosionTestDriverInfo info;
		bool initialized;
		bool damageIssued;
		bool pendingRecorded;
		bool releaseBatchIssued;
		bool preservedExitRecorded;
		bool releaseArrivalRecorded;
		bool releaseFailureRecorded;
		int filledTick = -1;
		CPos coarseCell;
		CPos damageCell;
		Actor subject;
		Actor squadLeader;
		Squad subjectSquad;
		int squadMemberCount;
		uint[] squadMemberIds = System.Array.Empty<uint>();

		public StealthResourceExplosionTestDriver(Actor self, StealthResourceExplosionTestDriverInfo info)
		{
			this.self = self;
			this.info = info;
		}

		internal static bool TryGetPending(World world, Actor member, out CPos coarse, out int tick)
		{
			if (PendingByWorld.TryGetValue(world, out var pending) && pending.Owner == member.Owner &&
				pending.Members.Contains(member.ActorID))
			{
				coarse = pending.Coarse;
				tick = pending.Tick;
				return true;
			}

			coarse = default;
			tick = -1;
			return false;
		}

		void ObserveReleaseDefaultEscape()
		{
			if (!releaseBatchIssued || releaseArrivalRecorded || subjectSquad == null)
				return;
			var members = squadMemberIds.Select(self.World.GetActorById)
				.Where(a => a != null && !a.IsDead && a.IsInWorld).ToArray();
			var allExited = members.All(a => new CPos(a.Location.X / info.StrategicCellSize,
				a.Location.Y / info.StrategicCellSize) != coarseCell);
			if (!allExited && (!subjectSquad.AirEscapingLocalAa ||
				!subjectSquad.StealthEscapePendingExplosion) && !releaseFailureRecorded)
			{
				releaseFailureRecorded = true;
				Log.Write("debug", "CNC96A_RELEASE_BLUE_LATCH_FAILURE tick={0} escaping={1} latch={2}.",
					self.World.WorldTick, subjectSquad.AirEscapingLocalAa,
					subjectSquad.StealthEscapePendingExplosion);
			}

			if (allExited && !preservedExitRecorded)
			{
				preservedExitRecorded = true;
				Log.Write("debug", "CNC96A_RELEASE_BLUE_PRESERVED_EXIT tick={0} members={1} " +
					"failure={2}.", self.World.WorldTick, members.Length, releaseFailureRecorded);
			}

			if (preservedExitRecorded && !subjectSquad.AirEscapingLocalAa)
			{
				releaseArrivalRecorded = true;
				Log.Write("debug", "CNC96A_RELEASE_BLUE_ARRIVAL tick={0} members={1} failure={2}.",
					self.World.WorldTick, members.Length, releaseFailureRecorded);
			}
		}

		void PrepareReleaseDefaultEscapeFixture()
		{
			if (subjectSquad == null)
				return;
			subjectSquad.AirEscapingLocalAa = false;
			subjectSquad.StealthEscapePendingExplosion = false;
			subjectSquad.AirUnitsRepairing.Clear();
			foreach (var member in squadMemberIds.Select(self.World.GetActorById)
				.Where(a => a != null && !a.IsDead && a.IsInWorld))
				member.CancelActivity();
		}

		void ITick.Tick(Actor actor)
		{
			if (pendingRecorded)
			{
				ObserveReleaseDefaultEscape();
				return;
			}

			if (self.World.WorldTick < info.TriggerTick)
				return;

			var resourceLayer = self.World.WorldActor.TraitOrDefault<IResourceLayer>();
			if (resourceLayer == null)
				return;

			if (!initialized)
			{
				if (info.RequireNonLeadSquadMember)
				{
					var squad = self.World.Actors.SelectMany(a => a.TraitsImplementing<SquadManagerBotModule>())
						.SelectMany(manager => manager.Squads).Where(s => s.Type == SquadType.Stealth)
						.Select(s => new
						{
							Squad = s,
							Units = s.Units.Where(a => a.IsInWorld && !a.IsDead &&
								a.Info.Name == info.ActorType).OrderBy(a => a.ActorID).ToArray()
						})
						.FirstOrDefault(entry => entry.Units.Length > info.ActorIndex + 1);
					if (squad != null)
					{
						subjectSquad = squad.Squad;
						squadLeader = squad.Units[0];
						subject = squad.Units[info.ActorIndex + 1];
						squadMemberCount = squad.Units.Length;
						squadMemberIds = squad.Units.Select(a => a.ActorID).ToArray();
					}
				}
				else
					subject = self.World.Actors.Where(a => a.IsInWorld && !a.IsDead &&
						a.Info.Name == info.ActorType).OrderBy(a => a.ActorID).Skip(info.ActorIndex).FirstOrDefault();
				if (subject == null)
					return;

				coarseCell = new CPos(subject.Location.X / info.StrategicCellSize,
					subject.Location.Y / info.StrategicCellSize);
				if (!info.ForceSafetyCacheRefreshBeforeDamage)
				{
					var density = resourceLayer.GetMaxDensity(info.ResourceType);
					for (var y = 0; y < info.StrategicCellSize; y++)
						for (var x = 0; x < info.StrategicCellSize; x++)
						{
							var cell = new CPos(coarseCell.X * info.StrategicCellSize + x,
								coarseCell.Y * info.StrategicCellSize + y);
							if (self.World.Map.Contains(cell) &&
								resourceLayer.CanAddResource(info.ResourceType, cell, density))
								resourceLayer.AddResource(info.ResourceType, cell, density);
						}
				}

				initialized = true;
				filledTick = self.World.WorldTick;
				var occupied = self.World.Actors.Where(a => a.IsInWorld && !a.IsDead &&
					a.Owner == subject.Owner && a.Info.Name == info.ActorType &&
					a.Location.X / info.StrategicCellSize == coarseCell.X &&
					a.Location.Y / info.StrategicCellSize == coarseCell.Y)
					.Select(a => a.Location).ToHashSet();
				damageCell = Enumerable.Range(0, info.StrategicCellSize)
					.SelectMany(y => Enumerable.Range(0, info.StrategicCellSize).Select(x =>
						new CPos(coarseCell.X * info.StrategicCellSize + x,
							coarseCell.Y * info.StrategicCellSize + y)))
					.Where(cell => self.World.Map.Contains(cell) &&
						(!info.DamageOnlyNonOccupiedCell || !occupied.Contains(cell)))
					.OrderByDescending(cell => (cell - subject.Location).LengthSquared)
					.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).First();
				Log.Write("debug", "CNC96A_BLUE_FILLED tick={0} actor={1}#{2} coarse={3} " +
					"damage-cell={4} occupied={5} pending-delay={6}.", self.World.WorldTick,
					subject.Info.Name, subject.ActorID, coarseCell, damageCell,
					occupied.Contains(damageCell), info.PendingDelayAfterFill);
			}

			if (self.World.WorldTick < filledTick + info.PendingDelayAfterFill)
				return;
			if (!damageIssued && info.ForceSafetyCacheRefreshBeforeDamage)
				coarseCell = new CPos(subject.Location.X / info.StrategicCellSize,
					subject.Location.Y / info.StrategicCellSize);

			if (!damageIssued && info.DamageOnlyNonOccupiedCell)
			{
				var occupiedNow = self.World.Actors.Where(a => a.IsInWorld && !a.IsDead &&
					a.Owner == subject.Owner && a.Info.Name == info.ActorType)
					.Select(a => a.Location).ToHashSet();
				damageCell = Enumerable.Range(0, info.StrategicCellSize)
					.SelectMany(y => Enumerable.Range(0, info.StrategicCellSize).Select(x =>
						new CPos(coarseCell.X * info.StrategicCellSize + x,
							coarseCell.Y * info.StrategicCellSize + y)))
					.Where(cell => self.World.Map.Contains(cell) && !occupiedNow.Contains(cell))
					.OrderByDescending(cell => (cell - subject.Location).LengthSquared)
					.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).First();
			}

			if (!damageIssued && info.ForceSafetyCacheRefreshBeforeDamage)
			{
				var density = resourceLayer.GetMaxDensity(info.ResourceType);
				if (resourceLayer.CanAddResource(info.ResourceType, damageCell, density))
					resourceLayer.AddResource(info.ResourceType, damageCell, density);
				if (subjectSquad != null)
					StealthAIStateBase.PrimeStealthInfluenceForTest(subjectSquad);
				Log.Write("debug", "CNC96A_BLUE_CACHE_PRIMED tick={0} actor={1}#{2} coarse={3} cell={4}.",
					self.World.WorldTick, subject.Info.Name, subject.ActorID, coarseCell, damageCell);
			}

			if (resourceLayer.IsExplosionPending(damageCell))
			{
				var occupiedNow = self.World.Actors.Any(a => a.IsInWorld && !a.IsDead &&
					a.Owner == subject.Owner && a.Info.Name == info.ActorType && a.Location == damageCell);
				pendingRecorded = true;
				PendingByWorld[self.World] = (subject.Owner, coarseCell, self.World.WorldTick,
					new HashSet<uint>(squadMemberIds));
				PrepareReleaseDefaultEscapeFixture();
				var modularBot = subjectSquad?.Bot as ModularBot;
				var queuedBefore = modularBot?.QueuedOrderCount ?? 0;
				subjectSquad?.TickAirSafety();
				var queuedAfter = modularBot?.QueuedOrderCount ?? queuedBefore;
				releaseBatchIssued = true;
				Log.Write("debug", "CNC96A_RELEASE_BLUE_BATCH tick={0} queued={1} squad-size={2} " +
					"escaping={3} latch={4}.", self.World.WorldTick, queuedAfter - queuedBefore,
					squadMemberCount, subjectSquad?.AirEscapingLocalAa ?? false,
					subjectSquad?.StealthEscapePendingExplosion ?? false);
				Log.Write("debug", "CNC96A_BLUE_PENDING tick={0} actor={1}#{2} index={3} " +
					"leader={4} squad-size={5} coarse={6} cell={7} occupied={8} fill-delay={9}.",
					self.World.WorldTick, subject.Info.Name, subject.ActorID, info.ActorIndex,
					squadLeader == null ? "none" : squadLeader.Info.Name + "#" + squadLeader.ActorID,
					squadMemberCount, coarseCell, damageCell, occupiedNow,
					info.PendingDelayAfterFill);
				return;
			}

			resourceLayer.DamageResource(self, damageCell, info.Damage);
			damageIssued = true;
		}
	}

	[Desc("Test-map-only observer for release-default pending-Blue escape orders.")]
	public sealed class StealthPendingBlueOrderObserverInfo : TraitInfo
	{
		public readonly int StrategicCellSize = 6;

		public override object Create(ActorInitializer init)
		{
			return new StealthPendingBlueOrderObserver(init.Self, this);
		}
	}

	public sealed class StealthPendingBlueOrderObserver : ITick
	{
		readonly Actor self;
		readonly StealthPendingBlueOrderObserverInfo info;
		bool observed;
		bool exited;
		CPos sourceCoarse;

		public StealthPendingBlueOrderObserver(Actor self, StealthPendingBlueOrderObserverInfo info)
		{
			this.self = self;
			this.info = info;
		}

		void ITick.Tick(Actor actor)
		{
			if (exited || self.IsDead || !self.IsInWorld)
				return;
			if (!observed)
			{
				if (!StealthResourceExplosionTestDriver.TryGetPending(
					self.World, self, out sourceCoarse, out _))
					return;
				observed = true;
			}

			var current = new CPos(self.Location.X / info.StrategicCellSize,
				self.Location.Y / info.StrategicCellSize);
			if (current == sourceCoarse)
				return;

			exited = true;
			Log.Write("debug", "CNC96A_RELEASE_BLUE_EXIT tick={0} actor={1}#{2} " +
				"source={3} current={4}.", self.World.WorldTick,
				self.Info.Name, self.ActorID, sourceCoarse, current);
		}
	}

	[Desc("Test-map-only exact crush telemetry. Add only to actors in an evidence map.")]
	public sealed class StealthCrushTestTelemetryInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new StealthCrushTestTelemetry(); }
	}

	public sealed class StealthCrushTestTelemetry : INotifyCrushed
	{
		void INotifyCrushed.WarnCrush(Actor self, Actor crusher, BitSet<CrushClass> crushClasses) { }

		void INotifyCrushed.OnCrush(Actor self, Actor crusher, BitSet<CrushClass> crushClasses)
		{
			if (crusher != null && crusher.Info.Name == "stnk")
				Log.Write("debug", "CNC96A_STEALTH_CRUSH tick={0} crusher={1}#{2} victim={3}#{4}.",
					self.World.WorldTick, crusher.Info.Name, crusher.ActorID, self.Info.Name, self.ActorID);
		}
	}
}
