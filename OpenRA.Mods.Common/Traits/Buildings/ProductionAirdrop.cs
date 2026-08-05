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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Deliver the unit in production via skylift.")]
	public class ProductionAirdropInfo : ProductionInfo
	{
		[NotificationReference("Speech")]
		public readonly string ReadyAudio = "Reinforce";

		[FieldLoader.Require]
		[ActorReference(typeof(AircraftInfo))]
		[Desc("Cargo aircraft used for delivery. Must have the `" + nameof(Aircraft) + "` trait.")]
		public readonly string ActorType = null;

		[Desc("The cargo aircraft will spawn at the player baseline (map edge closest to the player spawn)")]
		public readonly bool BaselineSpawn = false;

		[Desc("Direction the aircraft should face to land.")]
		public readonly WAngle Facing = new WAngle(256);

		public override object Create(ActorInitializer init) { return new ProductionAirdrop(init, this); }
	}

	class ProductionAirdrop : Production, IPendingProductionActors, IGameSaveTraitData
	{
		readonly List<string> pendingLaunches = new List<string>();
		readonly Dictionary<Actor, string> pendingDeliveries = new Dictionary<Actor, string>();

		IEnumerable<string> IPendingProductionActors.PendingActorTypes => pendingLaunches.Concat(
			pendingDeliveries.Where(kv => !kv.Key.IsDead && kv.Key.IsInWorld).Select(kv => kv.Value));

		public ProductionAirdrop(ActorInitializer init, ProductionAirdropInfo info)
			: base(init, info) { }

		public override bool Produce(Actor self, ActorInfo producee, string productionType, TypeDictionary inits, int refundableValue)
		{
			if (IsTraitDisabled || IsTraitPaused)
				return false;

			var info = (ProductionAirdropInfo)Info;
			var owner = self.Owner;
			var map = owner.World.Map;
			var aircraftInfo = self.World.Map.Rules.Actors[info.ActorType].TraitInfo<AircraftInfo>();

			CPos startPos;
			CPos endPos;
			WAngle spawnFacing;

			if (info.BaselineSpawn)
			{
				var bounds = map.Bounds;
				var center = new MPos(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2).ToCPos(map);
				var spawnVec = owner.HomeLocation - center;
				startPos = owner.HomeLocation + spawnVec * (Exts.ISqrt((bounds.Height * bounds.Height + bounds.Width * bounds.Width) / (4 * spawnVec.LengthSquared)));
				endPos = startPos;
				var spawnDirection = new WVec((self.Location - startPos).X, (self.Location - startPos).Y, 0);
				spawnFacing = spawnDirection.Yaw;
			}
			else
			{
				// Start a fixed distance away: the width of the map.
				// This makes the production timing independent of spawnpoint
				var loc = self.Location.ToMPos(map);
				startPos = new MPos(loc.U + map.Bounds.Width, loc.V).ToCPos(map);
				endPos = new MPos(map.Bounds.Left, loc.V).ToCPos(map);
				spawnFacing = info.Facing;
			}

			var exitObj = SelectExit(self, producee, productionType);
			var exit = exitObj != null ? exitObj.Info : self.Info.TraitInfos<ExitInfo>().First();

			foreach (var tower in self.TraitsImplementing<INotifyDelivery>())
				tower.IncomingDelivery(self);

			pendingLaunches.Add(producee.Name);
			owner.World.AddFrameEndTask(w =>
			{
				if (!self.IsInWorld || self.IsDead)
				{
					pendingLaunches.Remove(producee.Name);
					owner.PlayerActor.Trait<PlayerResources>().GiveCash(refundableValue);
					return;
				}

				var actor = w.CreateActor(info.ActorType, new TypeDictionary
				{
					new CenterPositionInit(w.Map.CenterOfCell(startPos) + new WVec(WDist.Zero, WDist.Zero, aircraftInfo.CruiseAltitude)),
					new OwnerInit(owner),
					new FacingInit(spawnFacing)
				});
				pendingLaunches.Remove(producee.Name);
				pendingDeliveries.Add(actor, producee.Name);

				var exitCell = self.Location + exit.ExitCell;
				actor.QueueActivity(new Land(actor, Target.FromActor(self), WDist.Zero, WVec.Zero, info.Facing, clearCells: new CPos[1] { exitCell }));
				actor.QueueActivity(new CallFunc(() =>
				{
					if (!self.IsInWorld || self.IsDead)
					{
						pendingDeliveries.Remove(actor);
						owner.PlayerActor.Trait<PlayerResources>().GiveCash(refundableValue);
						return;
					}

					foreach (var cargo in self.TraitsImplementing<INotifyDelivery>())
						cargo.Delivered(self);

					self.World.AddFrameEndTask(ww =>
					{
						pendingDeliveries.Remove(actor);
						DoProduction(self, producee, exit, productionType, inits, refundableValue);
					});
					Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", info.ReadyAudio, self.Owner.Faction.InternalName);
				}));

				actor.QueueActivity(new FlyOffMap(actor, Target.FromCell(w, endPos)));
				actor.QueueActivity(new RemoveSelf());
			});

			return true;
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			var pending = pendingDeliveries.Where(kv => !kv.Key.IsDead && kv.Key.IsInWorld)
				.OrderBy(kv => kv.Key.ActorID).ToArray();
			return new List<MiniYamlNode>()
			{
				new MiniYamlNode("PendingDeliveryActorIds", FieldSaver.FormatValue(pending.Select(kv => kv.Key.ActorID).ToArray())),
				new MiniYamlNode("PendingDeliveryTypes", FieldSaver.FormatValue(pending.Select(kv => kv.Value).ToArray()))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var idsNode = data.FirstOrDefault(n => n.Key == "PendingDeliveryActorIds");
			var typesNode = data.FirstOrDefault(n => n.Key == "PendingDeliveryTypes");
			if (idsNode == null || typesNode == null)
				return;

			var ids = FieldLoader.GetValue<uint[]>("PendingDeliveryActorIds", idsNode.Value.Value);
			var types = FieldLoader.GetValue<string[]>("PendingDeliveryTypes", typesNode.Value.Value);
			pendingDeliveries.Clear();
			for (var i = 0; i < ids.Length && i < types.Length; i++)
			{
				var actor = self.World.GetActorById(ids[i]);
				if (actor != null && !actor.IsDead && actor.IsInWorld)
					pendingDeliveries.Add(actor, types[i]);
			}
		}
	}
}
