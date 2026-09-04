// Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
// This file is licensed under the GNU General Public License version 3 or later.

using System;
using System.Collections.Generic;

namespace OpenRA.Mods.Common.Traits
{
	static class StealthStrategicRouteGeometry
	{
		public static CPos EndOfFirstStraightLeg(CPos start, IReadOnlyList<CPos> route)
		{
			if (route == null || route.Count == 0)
				throw new ArgumentException("A strategic route must contain a destination.", nameof(route));

			var previous = start;
			var destination = route[0];
			var direction = Direction(start, destination);
			for (var i = 1; i < route.Count; i++)
			{
				var next = route[i];
				if (Direction(previous, next) != direction)
					break;
				destination = next;
				previous = next;
			}

			return destination;
		}

		static CVec Direction(CPos from, CPos to)
		{
			return new CVec(Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));
		}
	}
}
