#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. You can redistribute
 * it and/or modify it under the terms of the GNU General Public License.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Bounded, current-World actor lookup shared by the local stealth combat phases.</summary>
	sealed class StealthSquadLiveLocalActors
	{
		const int ThreatSearchPaddingCells = 4;
		readonly Squad squad;

		public StealthSquadLiveLocalActors(Squad squad)
		{
			this.squad = squad ?? throw new ArgumentNullException(nameof(squad));
		}

		public Actor[] Enemies(StealthApproachMission mission, IReadOnlyList<Actor> members)
		{
			if (mission == null || members == null)
				throw new ArgumentNullException(mission == null ? nameof(mission) : nameof(members));
			if (members.Count == 0)
				return Array.Empty<Actor>();

			var size = Math.Max(1, squad.StealthDefinition?.StrategicCellSize ?? 1);
			var center = members.Select(actor => actor.CenterPosition).Average();
			var radius = Math.Max(squad.SquadManager.Info.DangerScanRadius + ThreatSearchPaddingCells,
				size * 3 + ThreatSearchPaddingCells);
			var nearby = squad.World.FindActorsInCircle(center, WDist.FromCells(radius));

			var map = squad.World.Map;
			var topLeft = map.Clamp(new CPos((mission.StrategicCell.X - 1) * size,
				(mission.StrategicCell.Y - 1) * size));
			var bottomRight = map.Clamp(new CPos((mission.StrategicCell.X + 2) * size - 1,
				(mission.StrategicCell.Y + 2) * size - 1));
			var missionArea = squad.World.ActorMap.ActorsInBox(
				map.CenterOfCell(topLeft), map.CenterOfCell(bottomRight));

			return nearby.Concat(missionArea).Where(actor => Live(actor) &&
				squad.SquadManager.IsPreferredEnemyUnit(actor))
				.Distinct().OrderBy(actor => actor.ActorID).ToArray();
		}

		public static Actor Representative(IReadOnlyList<Actor> members,
			IReadOnlyList<Actor> enemies)
		{
			if (members == null || members.Count == 0)
				return null;
			if (enemies != null && enemies.Count != 0)
				return members.OrderBy(member => enemies.Min(enemy =>
					(member.CenterPosition - enemy.CenterPosition).HorizontalLengthSquared))
					.ThenBy(member => member.ActorID).First();
			var center = members.Select(actor => actor.CenterPosition).Average();
			return members.OrderBy(actor => (actor.CenterPosition - center).HorizontalLengthSquared)
				.ThenBy(actor => actor.ActorID).First();
		}

		public static Actor Representative(IReadOnlyList<Actor> members, Actor enemy)
		{
			return enemy == null ? Representative(members, (IReadOnlyList<Actor>)null) :
				members.OrderBy(member =>
					(member.CenterPosition - enemy.CenterPosition).HorizontalLengthSquared)
					.ThenBy(member => member.ActorID).FirstOrDefault();
		}

		static bool Live(Actor actor)
		{
			return actor != null && actor.IsInWorld && !actor.IsDead;
		}
	}
}
