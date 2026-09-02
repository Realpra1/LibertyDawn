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

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>
	/// Passive lifecycle facade over the established shared stealth influence cache and route service.
	/// It has no transition or order authority.
	/// </summary>
	sealed class StealthSquadLifecycleStrategicAdapter : IStealthLifecycleCacheService,
		IStealthTargetAcquisitionCache, IStealthApproachStrategicCache,
		IStealthApproachStrategicRouteCache,
		IStealthRecalculateFleeStrategicCache, IStealthRepairStrategicCache,
		IStealthSquadConstructionSafetyService
	{
		readonly Squad squad;
		StealthLifecycleStrategicView view;

		public StealthSquadLifecycleStrategicAdapter(Squad squad)
		{
			this.squad = squad ?? throw new ArgumentNullException(nameof(squad));
		}

		public void Observe(StealthLifecycleObservationFrame frame)
		{
			if (frame == null)
				throw new ArgumentNullException(nameof(frame));
			if (StealthAIStateBase.TryReadLifecycleStrategicView(squad, out var refreshed))
				view = refreshed;
		}

		StealthTargetAcquisitionCacheSnapshot IStealthTargetAcquisitionCache.ReadSnapshot()
		{
			return Current().Acquisition;
		}

		StealthApproachStrategicCacheSnapshot IStealthApproachStrategicCache.ReadSnapshot()
		{
			return Current().Approach;
		}

		IReadOnlyList<CPos> IStealthApproachStrategicRouteCache.ReadRoute(
			CPos originStrategicCell, CPos destinationStrategicCell)
		{
			return StealthAIStateBase.ReadLifecycleApproachRoute(squad,
				originStrategicCell, destinationStrategicCell);
		}

		StealthRecalculateFleeStrategicCacheSnapshot
			IStealthRecalculateFleeStrategicCache.ReadEscapeRoute(
				StealthApproachMission mission)
		{
			if (StealthAIStateBase.TryReadLifecycleFleeRoute(squad, mission,
				out var revision, out var danger, out var route))
				return new StealthRecalculateFleeStrategicCacheSnapshot(revision,
					new StealthTargetThreatScore(danger, double.PositiveInfinity), route);
			return new StealthRecalculateFleeStrategicCacheSnapshot(Current().Revision,
				new StealthTargetThreatScore(0, double.PositiveInfinity), Array.Empty<CPos>());
		}

		StealthRepairStrategicCacheSnapshot IStealthRepairStrategicCache.ReadLongRoute(
			StealthApproachMission mission, uint repairOptionActorId, IReadOnlyList<CPos> liveRoute)
		{
			var repair = squad.World.GetActorById(repairOptionActorId);
			if (repair != null && repair.IsInWorld && !repair.IsDead &&
				StealthAIStateBase.TryReadLifecycleLongRoute(squad, repair.Location,
					out var revision, out var route))
				return new StealthRepairStrategicCacheSnapshot(revision, route);
			return new StealthRepairStrategicCacheSnapshot(Current().Revision, Array.Empty<CPos>());
		}

		public bool TryFindSafeRoute(uint actorId, CPos startStrategicCell,
			CPos destinationStrategicCell, out IReadOnlyList<CPos> route)
		{
			var approaches = new[]
			{
				new CVec(-1, -1), new CVec(0, -1), new CVec(1, -1), new CVec(-1, 0),
				new CVec(1, 0), new CVec(-1, 1), new CVec(0, 1), new CVec(1, 1)
			}.Select(offset => destinationStrategicCell + offset)
				.Where(cell => cell.X >= 0 && cell.Y >= 0)
				.OrderBy(cell => (cell - startStrategicCell).LengthSquared)
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X);
			foreach (var approach in approaches)
				if (StealthAIStateBase.TryReadLifecycleStrategicRoute(squad, actorId,
					startStrategicCell, approach, true, true, out _, out route))
					return true;
			route = Array.Empty<CPos>();
			return false;
		}

		StealthLifecycleStrategicView Current()
		{
			return view ?? (view = StealthAIStateBase.ReadLifecycleStrategicView(squad));
		}
	}

	sealed class StealthLifecyclePassiveServices : IStealthLifecycleThreatService,
		IStealthLifecycleRouteService, IStealthLifecycleDiagnosticService
	{
		public void Observe(StealthLifecycleObservationFrame frame) { }
		public void Record(StealthLifecycleDiagnostic diagnostic) { }
	}
}
