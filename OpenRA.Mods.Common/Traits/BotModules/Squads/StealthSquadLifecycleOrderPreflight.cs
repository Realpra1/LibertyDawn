#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is made available to you under the terms of
 * the GNU General Public License as published by the Free Software Foundation.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Resolves a complete canonical batch before its one external queue callback.</summary>
	static class StealthSquadLifecycleOrderPreflight
	{
		internal static TOrder Prepare<TActor, TOrder>(IEnumerable<uint> actorIds,
			Func<uint, TActor> resolveActor, Func<TActor[], TOrder> prepareOrder)
			where TActor : class
			where TOrder : class
		{
			if (actorIds == null || resolveActor == null || prepareOrder == null)
				throw new ArgumentNullException(actorIds == null ? nameof(actorIds) :
					resolveActor == null ? nameof(resolveActor) : nameof(prepareOrder));
			var actors = actorIds.Select(resolveActor).ToArray();
			if (actors.Length == 0 || actors.Any(actor => actor == null))
				throw new InvalidOperationException(
					"A runtime order referenced a non-live squad member.");
			return prepareOrder(actors) ??
				throw new InvalidOperationException("The runtime order payload was not prepared.");
		}
	}
}
