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
using OpenRA.Mods.Common.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("This unit has access to build queues.")]
	public class ProductionInfo : PausableConditionalTraitInfo
	{
		[FieldLoader.Require]
		[Desc("e.g. Infantry, Vehicles, Aircraft, Buildings")]
		public readonly string[] Produces = Array.Empty<string>();

		[ActorReference]
		[Desc("Actor types whose transition from queued or in-flight production to live actors must share an owner-wide limit.")]
		public readonly HashSet<string> OwnerActorLimitTypes = new HashSet<string>();

		[Desc("Maximum live, queued, and in-flight OwnerActorLimitTypes. Zero disables the limit.")]
		public readonly int OwnerActorLimit = 0;

		[Desc("Apply OwnerActorLimit only when the producer owner is a bot.")]
		public readonly bool OwnerActorLimitBotsOnly = false;

		public override object Create(ActorInitializer init) { return new Production(init, this); }
	}

	public class Production : PausableConditionalTrait<ProductionInfo>
	{
		RallyPoint rp;

		public string Faction { get; private set; }

		public Production(ActorInitializer init, ProductionInfo info)
			: base(info)
		{
			Faction = init.GetValue<FactionInit, string>(init.Self.Owner.Faction.InternalName);
		}

		protected override void Created(Actor self)
		{
			rp = self.TraitOrDefault<RallyPoint>();
			base.Created(self);
		}

		public virtual void DoProduction(Actor self, ActorInfo producee, ExitInfo exitinfo, string productionType,
			TypeDictionary inits, int refundableValue = 0)
		{
			var exit = CPos.Zero;
			var exitLocations = new List<CPos>();

			// Clone the initializer dictionary for the new actor
			var td = new TypeDictionary();
			foreach (var init in inits)
				td.Add(init);

			if (exitinfo != null && self.OccupiesSpace != null && producee.HasTraitInfo<IOccupySpaceInfo>())
			{
				exit = self.Location + exitinfo.ExitCell;
				var spawn = self.CenterPosition + exitinfo.SpawnOffset;
				var to = self.World.Map.CenterOfCell(exit);

				WAngle initialFacing;
				if (!exitinfo.Facing.HasValue)
				{
					var delta = to - spawn;
					if (delta.HorizontalLengthSquared == 0)
					{
						var fi = producee.TraitInfoOrDefault<IFacingInfo>();
						initialFacing = fi != null ? fi.GetInitialFacing() : WAngle.Zero;
					}
					else
						initialFacing = delta.Yaw;
				}
				else
					initialFacing = exitinfo.Facing.Value;

				exitLocations = rp != null && rp.Path.Count > 0 ? rp.Path : new List<CPos> { exit };

				td.Add(new LocationInit(exit));
				td.Add(new CenterPositionInit(spawn));
				td.Add(new FacingInit(initialFacing));
				if (exitinfo != null)
					td.Add(new CreationActivityDelayInit(exitinfo.ExitDelay));
			}

			self.World.AddFrameEndTask(w =>
			{
				if (!CanCreateWithinOwnerActorLimit(w, self, producee))
				{
					if (refundableValue > 0)
						self.Owner.PlayerActor.Trait<PlayerResources>().GiveCash(refundableValue);

					Log.Write("debug", "Produced actor {0} from {1} suppressed and refunded for {2}: owner limit {3}.",
						producee.Name, self.Info.Name, self.Owner, Info.OwnerActorLimit);
					return;
				}

				var newUnit = self.World.CreateActor(producee.Name, td);

				var move = newUnit.TraitOrDefault<IMove>();
				if (exitinfo != null && move != null)
					foreach (var cell in exitLocations)
						newUnit.QueueActivity(new AttackMoveActivity(newUnit, () => move.MoveTo(cell, 1, evaluateNearestMovableCell: true, targetLineColor: Color.OrangeRed)));

				if (!self.IsDead)
					foreach (var t in self.TraitsImplementing<INotifyProduction>())
						t.UnitProduced(self, newUnit, exit);

				var notifyOthers = self.World.ActorsWithTrait<INotifyOtherProduction>();
				foreach (var notify in notifyOthers)
					notify.Trait.UnitProducedByOther(notify.Actor, self, newUnit, productionType, td);
			});
		}

		bool CanCreateWithinOwnerActorLimit(World world, Actor self, ActorInfo producee)
		{
			if (Info.OwnerActorLimit <= 0 || !Info.OwnerActorLimitTypes.Contains(producee.Name) ||
				(Info.OwnerActorLimitBotsOnly && !self.Owner.IsBot))
				return true;

			var live = world.Actors.Count(a => a.Owner == self.Owner && !a.IsDead &&
				Info.OwnerActorLimitTypes.Contains(a.Info.Name));
			var queued = world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == self.Owner && !q.Actor.IsDead && q.Actor.IsInWorld)
				.Sum(q => q.Trait.AllQueued().Count(i => Info.OwnerActorLimitTypes.Contains(i.Item)));
			var pending = world.ActorsWithTrait<IPendingProductionActors>()
				.Where(p => p.Actor.Owner == self.Owner && !p.Actor.IsDead && p.Actor.IsInWorld)
				.Sum(p => p.Trait.PendingActorTypes.Count(Info.OwnerActorLimitTypes.Contains));

			return SharedActorLimitPolicy.AllowedAmount(1, live + queued + pending,
				SharedActorLimitReservations.Reserved(world, self.Owner), Info.OwnerActorLimit) == 1;
		}

		protected virtual Exit SelectExit(Actor self, ActorInfo producee, string productionType, Func<Exit, bool> p)
		{
			if (rp == null || rp.Path.Count == 0)
				return self.RandomExitOrDefault(self.World, productionType, p);

			return self.NearestExitOrDefault(self.World.Map.CenterOfCell(rp.Path[0]), productionType, p);
		}

		protected Exit SelectExit(Actor self, ActorInfo producee, string productionType)
		{
			return SelectExit(self, producee, productionType, e => CanUseExit(self, producee, e.Info));
		}

		public virtual bool Produce(Actor self, ActorInfo producee, string productionType, TypeDictionary inits, int refundableValue)
		{
			if (IsTraitDisabled || IsTraitPaused || Reservable.IsReserved(self))
				return false;

			// Pick a spawn/exit point pair
			var exit = SelectExit(self, producee, productionType);
			if (exit != null || self.OccupiesSpace == null || !producee.HasTraitInfo<IOccupySpaceInfo>())
			{
				DoProduction(self, producee, exit?.Info, productionType, inits, refundableValue);
				return true;
			}

			return false;
		}

		static bool CanUseExit(Actor self, ActorInfo producee, ExitInfo s)
		{
			var mobileInfo = producee.TraitInfoOrDefault<MobileInfo>();

			self.NotifyBlocker(self.Location + s.ExitCell);

			return mobileInfo == null ||
				mobileInfo.CanEnterCell(self.World, self, self.Location + s.ExitCell, ignoreActor: self);
		}
	}

	public interface IPendingProductionActors
	{
		IEnumerable<string> PendingActorTypes { get; }
	}
}
