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

namespace OpenRA.Mods.Common.Traits.BotModules
{
	public sealed class AdvancedBotFallbackOwnership
	{
		readonly SortedDictionary<string, SortedSet<uint>> released =
			new SortedDictionary<string, SortedSet<uint>>(StringComparer.Ordinal);

		public IEnumerable<KeyValuePair<string, uint[]>> Groups => released.Select(g =>
			new KeyValuePair<string, uint[]>(g.Key, g.Value.ToArray()));

		public static bool IsEligibleForGenericFallback(ISet<string> directCombatTypes, string actorType,
			bool hasAttackTrait)
		{
			return hasAttackTrait && directCombatTypes.Contains(actorType);
		}

		public static bool RequiresAttackMove(bool previouslyOrdered, bool targetChanged, bool isIdle)
		{
			// Engine pathing may complete or abandon an AttackMove without reaching a viable
			// target. A failsafe owner must reclaim that idle actor regardless of which
			// ordinary squad policy was configured before advanced behavior was shed.
			return !previouslyOrdered || targetChanged || isIdle;
		}

		public void Retain(string source, IEnumerable<uint> actorIds)
		{
			if (string.IsNullOrEmpty(source))
				throw new ArgumentException("A released specialist group must identify its source.", nameof(source));

			if (!released.TryGetValue(source, out var actors))
				released.Add(source, actors = new SortedSet<uint>());

			foreach (var actorId in actorIds)
				if (actorId != 0)
					actors.Add(actorId);
		}

		public (string[] Sources, uint[] ActorIds) Export()
		{
			var entries = released.SelectMany(g => g.Value.Select(id => (Source: g.Key, ActorId: id))).ToArray();
			return (entries.Select(e => e.Source).ToArray(), entries.Select(e => e.ActorId).ToArray());
		}

		public void Import(IEnumerable<string> sources, IEnumerable<uint> actorIds)
		{
			released.Clear();
			foreach (var entry in sources.Zip(actorIds, (source, actorId) => (source, actorId)))
				Retain(entry.source, new[] { entry.actorId });
		}
	}
}
