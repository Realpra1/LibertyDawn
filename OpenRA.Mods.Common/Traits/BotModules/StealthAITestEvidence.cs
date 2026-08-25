// Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
// This file is licensed under the GNU General Public License version 3 or later.

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
		public readonly int StrategicCellSize = 6;
		public readonly int Damage = 1000000;

		public override object Create(ActorInitializer init)
		{
			return new StealthResourceExplosionTestDriver(init.Self, this);
		}
	}

	public sealed class StealthResourceExplosionTestDriver : ITick
	{
		readonly Actor self;
		readonly StealthResourceExplosionTestDriverInfo info;
		bool initialized;
		bool pendingRecorded;
		CPos coarseCell;
		Actor subject;
		Actor squadLeader;

		public StealthResourceExplosionTestDriver(Actor self, StealthResourceExplosionTestDriverInfo info)
		{
			this.self = self;
			this.info = info;
		}

		void ITick.Tick(Actor actor)
		{
			if (pendingRecorded || self.World.WorldTick < info.TriggerTick)
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
							Units = s.Units.Where(a => a.IsInWorld && !a.IsDead &&
								a.Info.Name == info.ActorType).OrderBy(a => a.ActorID).ToArray()
						})
						.FirstOrDefault(entry => entry.Units.Length > info.ActorIndex + 1);
					if (squad != null)
					{
						squadLeader = squad.Units[0];
						subject = squad.Units[info.ActorIndex + 1];
					}
				}
				else
					subject = self.World.Actors.Where(a => a.IsInWorld && !a.IsDead &&
						a.Info.Name == info.ActorType).OrderBy(a => a.ActorID).Skip(info.ActorIndex).FirstOrDefault();
				if (subject == null)
					return;

				coarseCell = new CPos(subject.Location.X / info.StrategicCellSize,
					subject.Location.Y / info.StrategicCellSize);
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

				initialized = true;
			}

			for (var y = 0; y < info.StrategicCellSize; y++)
				for (var x = 0; x < info.StrategicCellSize; x++)
				{
					var cell = new CPos(coarseCell.X * info.StrategicCellSize + x,
						coarseCell.Y * info.StrategicCellSize + y);
					if (!self.World.Map.Contains(cell))
						continue;
					if (resourceLayer.IsExplosionPending(cell))
					{
						pendingRecorded = true;
						Log.Write("debug", "CNC96A_BLUE_PENDING tick={0} actor={1}#{2} index={3} " +
							"leader={4} coarse={5} cell={6}.", self.World.WorldTick, subject.Info.Name,
							subject.ActorID, info.ActorIndex, squadLeader == null ? "none" :
							squadLeader.Info.Name + "#" + squadLeader.ActorID, coarseCell, cell);
						return;
					}

					resourceLayer.DamageResource(self, cell, info.Damage);
				}
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
