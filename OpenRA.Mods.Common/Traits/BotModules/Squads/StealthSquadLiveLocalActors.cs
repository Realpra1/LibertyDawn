#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. You can redistribute
 * it and/or modify it under the terms of the GNU General Public License.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Bounded, current-World actor lookup shared by the local stealth combat phases.</summary>
	sealed class StealthSquadLiveLocalActors
	{
		const int ThreatSearchPaddingCells = 4;
		readonly Squad squad;
		readonly int maximumWeaponRangeCells;
		Actor[] cachedRoster = Array.Empty<Actor>();
		CPos cachedMissionCell;
		CPos cachedCenterCell;
		int rosterRefreshTick;
		bool hasCachedRoster;

		public StealthSquadLiveLocalActors(Squad squad)
		{
			this.squad = squad ?? throw new ArgumentNullException(nameof(squad));
			maximumWeaponRangeCells = (int)Math.Ceiling(squad.World.Map.Rules.Actors.Values
				.SelectMany(actor => actor.TraitInfos<ArmamentInfo>())
				.Select(armament => armament.ModifiedRange.Length / 1024d).DefaultIfEmpty().Max());
		}

		public Actor[] Enemies(StealthApproachMission mission, IReadOnlyList<Actor> members)
		{
			if (mission == null || members == null)
				throw new ArgumentNullException(mission == null ? nameof(mission) : nameof(members));
			if (members.Count == 0)
				return Array.Empty<Actor>();

			var center = members.Select(actor => actor.CenterPosition).Average();
			var localRadius = LocalRadiusCells();
			var radius = StealthLocalActorCachePolicy.CoveringRadiusCells(
				localRadius, maximumWeaponRangeCells, ThreatSearchPaddingCells);
			var roster = CurrentRoster(mission, center, localRadius, radius);
			var localRadiusLength = WDist.FromCells(localRadius).Length;
			var radiusLength = WDist.FromCells(radius).Length;
			var nearby = roster.Where(actor =>
				(actor.CenterPosition - center).HorizontalLengthSquared <=
				(long)localRadiusLength * localRadiusLength);
			var coveringThreats = roster.Where(actor =>
				(actor.CenterPosition - center).HorizontalLengthSquared <=
				(long)radiusLength * radiusLength)
				.Where(actor => ThreatensFormation(actor, members));

			var (topLeft, bottomRight) = MissionBounds(mission);
			var missionArea = roster.Where(actor => InBounds(actor.Location, topLeft, bottomRight));

			return nearby.Concat(missionArea).Concat(coveringThreats).Where(actor => Live(actor) &&
				squad.SquadManager.IsPreferredEnemyUnit(actor))
				.Distinct().OrderBy(actor => actor.ActorID).ToArray();
		}

		Actor[] CurrentRoster(StealthApproachMission mission, WPos center,
			int localRadius, int coveringRadius)
		{
			var map = squad.World.Map;
			var centerCell = map.CellContaining(center);
			var movementBuffer = StealthLocalActorCachePolicy.MovementBufferCells(localRadius);
			var refresh = StealthLocalActorCachePolicy.RequiresRefresh(hasCachedRoster,
				squad.World.WorldTick, rosterRefreshTick, mission.StrategicCell, cachedMissionCell,
				centerCell, cachedCenterCell, movementBuffer);
			var started = Game.IsBenchmarking ? Stopwatch.GetTimestamp() : 0;
			if (refresh)
			{
				var queryRadius = coveringRadius + movementBuffer;
				var (topLeft, bottomRight) = MissionBounds(mission, movementBuffer);
				cachedRoster = squad.World.FindActorsInCircle(center, WDist.FromCells(queryRadius))
					.Concat(squad.World.ActorMap.ActorsInBox(
						map.CenterOfCell(topLeft), map.CenterOfCell(bottomRight)))
					.Where(actor => Live(actor) && squad.SquadManager.IsPreferredEnemyUnit(actor))
					.Distinct().OrderBy(actor => actor.ActorID).ToArray();
				cachedMissionCell = mission.StrategicCell;
				cachedCenterCell = centerCell;
				rosterRefreshTick = squad.World.WorldTick + StealthLocalActorCachePolicy.RefreshInterval(
					squad.SquadManager.Info.StealthLocalActorCacheInterval,
					squad.SquadManager.PlanningIntervalFactor);
				hasCachedRoster = true;
			}

			RecordCacheSample(refresh, started);
			return cachedRoster;
		}

		void RecordCacheSample(bool refresh, long started)
		{
			if (!Game.IsBenchmarking)
				return;

			var elapsed = 1000d * Math.Max(0, Stopwatch.GetTimestamp() - started) / Stopwatch.Frequency;
			Game.RecordBotModuleSample(squad.Bot.Player.ClientIndex,
				$"StealthSquad/{squad.AirProfile}/local-actor-cache-{(refresh ? "refresh" : "hit")}",
				elapsed, 0);
		}

		static bool ThreatensFormation(Actor enemy, IReadOnlyList<Actor> members)
		{
			if (!Live(enemy))
				return false;
			foreach (var armament in enemy.TraitsImplementing<Armament>())
			{
				if (armament.IsTraitDisabled || armament.IsTraitPaused)
					continue;
				var range = armament.MaxRange().Length;
				var rangeSquared = (long)range * range;
				if (members.Any(member => armament.Weapon.IsValidTarget(member.GetEnabledTargetTypes()) &&
					(enemy.CenterPosition - member.CenterPosition).HorizontalLengthSquared <= rangeSquared))
					return true;
			}

			return false;
		}

		public bool IsInEngagementArea(StealthApproachMission mission,
			IReadOnlyList<Actor> members, Actor actor)
		{
			if (!Live(actor) || members == null || members.Count == 0)
				return false;
			var center = members.Select(member => member.CenterPosition).Average();
			var radius = WDist.FromCells(LocalRadiusCells()).Length;
			if ((actor.CenterPosition - center).HorizontalLengthSquared <= (long)radius * radius)
				return true;

			var (topLeft, bottomRight) = MissionBounds(mission);
			return actor.Location.X >= topLeft.X && actor.Location.X <= bottomRight.X &&
				actor.Location.Y >= topLeft.Y && actor.Location.Y <= bottomRight.Y;
		}

		int LocalRadiusCells()
		{
			var size = Math.Max(1, squad.StealthDefinition?.StrategicCellSize ?? 1);
			return StealthLocalActorCachePolicy.LocalRadiusCells(
				squad.SquadManager.Info.DangerScanRadius, size, ThreatSearchPaddingCells);
		}

		(CPos TopLeft, CPos BottomRight) MissionBounds(StealthApproachMission mission, int padding = 0)
		{
			var size = Math.Max(1, squad.StealthDefinition?.StrategicCellSize ?? 1);
			var map = squad.World.Map;
			return (map.Clamp(new CPos((mission.StrategicCell.X - 1) * size - padding,
				(mission.StrategicCell.Y - 1) * size - padding)),
				map.Clamp(new CPos((mission.StrategicCell.X + 2) * size - 1 + padding,
					(mission.StrategicCell.Y + 2) * size - 1 + padding)));
		}

		static bool InBounds(CPos cell, CPos topLeft, CPos bottomRight)
		{
			return cell.X >= topLeft.X && cell.X <= bottomRight.X &&
				cell.Y >= topLeft.Y && cell.Y <= bottomRight.Y;
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
