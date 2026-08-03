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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Uses existing player orders for specialist AI units.")]
	public class SpecialOrderBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between special-order scans. Zero disables the module.")]
		public readonly int ScanInterval = 125;

		[Desc("Actor types that deliver cash to compatible buildings owned by the bot.")]
		public readonly HashSet<string> SupplyActorTypes = new HashSet<string>();

		[Desc("Write special-order assignments to debug.log.")]
		public readonly bool DebugLogging = false;

		public override object Create(ActorInitializer init) { return new SpecialOrderBotModule(init.Self, this); }
	}

	public class SpecialOrderBotModule : ConditionalTrait<SpecialOrderBotModuleInfo>, IBotTick
	{
		readonly World world;
		readonly Player player;
		readonly HashSet<uint> activeSupplyUnits = new HashSet<uint>();
		int scanTicks;

		public SpecialOrderBotModule(Actor self, SpecialOrderBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void TraitEnabled(Actor self)
		{
			scanTicks = world.LocalRandom.Next(0, Math.Max(1, Info.ScanInterval));
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (Info.ScanInterval <= 0 || player.WinState != WinState.Undefined || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			CleanupAssignments();
			AssignSupplyOrders(bot);
		}

		void CleanupAssignments()
		{
			var activeActors = world.Actors.Where(a => a.Owner == player && !a.IsDead && a.IsInWorld)
				.ToDictionary(a => a.ActorID);
			activeSupplyUnits.RemoveWhere(id => !activeActors.TryGetValue(id, out var unit) || unit.IsIdle);
		}

		void AssignSupplyOrders(IBot bot)
		{
			foreach (var truck in world.Actors.Where(a => a.Owner == player && a.IsIdle &&
				Info.SupplyActorTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<DeliversCashInfo>() &&
				!activeSupplyUnits.Contains(a.ActorID)))
			{
				var target = EligibleCashTargets(truck).ClosestTo(truck.CenterPosition);
				if (target == null)
					continue;

				bot.QueueOrder(new Order("DeliverCash", truck, Target.FromActor(target), false));
				activeSupplyUnits.Add(truck.ActorID);
				Debug("supply {0}#{1} -> {2}#{3}", truck.Info.Name, truck.ActorID, target.Info.Name, target.ActorID);
			}
		}

		IEnumerable<Actor> EligibleCashTargets(Actor truck)
		{
			var deliveryType = truck.Info.TraitInfo<DeliversCashInfo>().Type;
			return world.Actors.Where(a => a.Owner == player && !a.IsDead && a.IsInWorld)
				.Where(a =>
				{
					var accepts = a.Info.TraitInfoOrDefault<AcceptsDeliveredCashInfo>();
					return accepts != null && SpecialOrderTargeting.AcceptsDelivery(a.Owner.RelationshipWith(player),
						deliveryType, accepts.ValidTypes, accepts.ValidRelationships);
				});
		}

		void Debug(string format, params object[] args)
		{
			if (Info.DebugLogging)
				Log.Write("debug", "AI ({0}) special orders: {1}", player.ClientIndex, string.Format(format, args));
		}
	}
}
