// Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
// This file is licensed under the GNU General Public License version 3 or later.

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	public readonly struct StealthProvincialMassRequest
	{
		public string Definition { get; }
		public int SquadIndex { get; }
		public CPos StrategicCell { get; }
		public int Tick { get; }
		public int MemberCount { get; }

		public StealthProvincialMassRequest(string definition, int squadIndex,
			CPos strategicCell, int tick, int memberCount)
		{
			Definition = definition ?? throw new ArgumentNullException(nameof(definition));
			SquadIndex = squadIndex;
			StrategicCell = strategicCell;
			Tick = tick;
			MemberCount = memberCount;
		}
	}

	public sealed class StealthProvincialMassPlan
	{
		public string Definition { get; }
		public CPos StrategicCell { get; }
		public int LeaderIndex { get; }
		public int[] JoiningIndices { get; }

		public StealthProvincialMassPlan(string definition, CPos strategicCell,
			int leaderIndex, IEnumerable<int> joiningIndices)
		{
			Definition = definition;
			StrategicCell = strategicCell;
			LeaderIndex = leaderIndex;
			JoiningIndices = joiningIndices.OrderBy(index => index).ToArray();
		}
	}

	public static class StealthProvincialMassCoordinator
	{
		public static bool SameProvince(CPos left, CPos right)
		{
			return Math.Abs(left.X - right.X) <= 1 && Math.Abs(left.Y - right.Y) <= 1;
		}

		public static StealthProvincialMassPlan[] Plan(
			IEnumerable<StealthProvincialMassRequest> requests, int tick, int minimumAge)
		{
			if (requests == null)
				throw new ArgumentNullException(nameof(requests));
			if (tick < 0 || minimumAge < 0)
				throw new ArgumentOutOfRangeException(tick < 0 ? nameof(tick) : nameof(minimumAge));

			var candidates = requests.Where(request => request.MemberCount > 0).ToArray();
			var eligible = candidates.Where(request =>
				request.Tick >= 0 && tick - request.Tick >= minimumAge)
				.OrderBy(request => request.Definition, StringComparer.Ordinal)
				.ThenBy(request => request.Tick).ThenBy(request => request.SquadIndex).ToArray();
			var consumed = new HashSet<(string Definition, int SquadIndex)>();
			var plans = new List<StealthProvincialMassPlan>();
			foreach (var anchor in eligible)
			{
				if (consumed.Contains((anchor.Definition, anchor.SquadIndex)))
					continue;
				var province = eligible.Where(request =>
					request.Definition == anchor.Definition &&
					!consumed.Contains((request.Definition, request.SquadIndex)) &&
					SameProvince(anchor.StrategicCell, request.StrategicCell))
					.GroupBy(request => request.SquadIndex).Select(group => group.First()).ToArray();
				if (province.Length < 2)
					continue;

				var ordered = province.OrderByDescending(request => request.MemberCount)
					.ThenBy(request => request.SquadIndex).ToArray();
				plans.Add(new StealthProvincialMassPlan(anchor.Definition,
					anchor.StrategicCell, ordered[0].SquadIndex,
					ordered.Skip(1).Select(request => request.SquadIndex)));
				foreach (var request in province)
					consumed.Add((request.Definition, request.SquadIndex));
			}

			return plans.OrderBy(plan => plan.Definition, StringComparer.Ordinal)
				.ThenBy(plan => plan.StrategicCell.Y).ThenBy(plan => plan.StrategicCell.X).ToArray();
		}
	}
}
