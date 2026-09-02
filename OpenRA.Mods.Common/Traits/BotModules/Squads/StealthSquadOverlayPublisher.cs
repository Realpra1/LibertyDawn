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
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Publishes sparse squad-state changes into the replay order stream.</summary>
	sealed class StealthSquadOverlayPublisher
	{
		const int PublishInterval = 5;
		readonly Dictionary<(string Profile, int Index), string> published =
			new Dictionary<(string Profile, int Index), string>();
		int nextPublishTick;

		public void Publish(SquadManagerBotModule manager, IBot bot)
		{
			if (manager.World.WorldTick < nextPublishTick ||
				manager.Player.PlayerActor.TraitOrDefault<StealthSquadOverlay>() == null)
				return;
			nextPublishTick = manager.World.WorldTick + PublishInterval;

			var current = manager.Squads.Where(squad => squad.UsesModularStealthLifecycle && squad.IsValid)
				.OrderBy(squad => squad.StealthSquadDefinition, StringComparer.Ordinal)
				.ThenBy(squad => squad.StealthSquadIndex).Select(squad =>
				{
					var key = (squad.StealthProfile, squad.StealthSquadIndex);
					var actorIds = squad.AirFormationUnits(bootstrapIfEmpty: false).Where(actor => actor != null &&
						actor.IsInWorld && !actor.IsDead).Select(actor => actor.ActorID).OrderBy(id => id);
					return (Key: key, Payload: StealthSquadOverlay.Encode(
						key.Item1, key.Item2, squad.StealthLifecyclePhase.ToString(), actorIds,
						Math.Max(1, squad.StealthDefinition.StrategicCellSize),
						squad.StealthOverlayConsideredTargets, squad.StealthOverlayChosenTarget));
				}).ToArray();
			var currentKeys = current.Select(item => item.Key).ToHashSet();
			foreach (var stale in published.Keys.Where(key => !currentKeys.Contains(key)).ToArray())
			{
				Queue(bot, StealthSquadOverlay.Encode(stale.Profile, stale.Index, null,
					Array.Empty<uint>(), 1, Array.Empty<CPos>(), null));
				published.Remove(stale);
			}

			foreach (var item in current)
			{
				if (published.TryGetValue(item.Key, out var prior) && prior == item.Payload)
					continue;
				Queue(bot, item.Payload);
				published[item.Key] = item.Payload;
			}
		}

		void Queue(IBot bot, string payload)
		{
			bot.QueueOrder(new Order(StealthSquadOverlay.OrderName,
				bot.Player.PlayerActor, false) { TargetString = payload });
		}
	}
}
