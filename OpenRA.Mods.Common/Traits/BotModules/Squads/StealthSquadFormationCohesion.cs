// Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
// This file is licensed under the GNU General Public License version 3 or later.

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Keeps the largest local cluster active while separated members safely rejoin.</summary>
	public static class StealthSquadFormationCohesion
	{
		public static uint[] SelectCore(IEnumerable<(uint ActorId, CPos StrategicCell)> members)
		{
			if (members == null)
				throw new ArgumentNullException(nameof(members));
			var live = members.OrderBy(member => member.ActorId).ToArray();
			if (live.Length == 0 || live.Any(member => member.ActorId == 0) ||
				live.Select(member => member.ActorId).Distinct().Count() != live.Length)
				throw new ArgumentException("Formation members must be unique and nonzero.", nameof(members));
			var anchor = live.OrderByDescending(candidate => live.Count(member =>
				Adjacent(candidate.StrategicCell, member.StrategicCell)))
				.ThenBy(candidate => candidate.ActorId).First();
			return live.Where(member => Adjacent(anchor.StrategicCell, member.StrategicCell))
				.Select(member => member.ActorId).ToArray();
		}

		static bool Adjacent(CPos left, CPos right)
		{
			return Math.Abs(left.X - right.X) <= 1 && Math.Abs(left.Y - right.Y) <= 1;
		}
	}
}
