#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;

namespace OpenRA.Mods.Common.Traits
{
	public enum EarlyInfantryProductionType
	{
		None,
		Grenadier,
		Chemical
	}

	/// <summary>World-independent production, formation, and timing policy for VIKI's early infantry attacks.</summary>
	public static class EarlyInfantryRushPolicy
	{
		public static EarlyInfantryProductionType NextProduction(int grenadiers, int grenadierTarget,
			int chemicals, int chemicalTarget)
		{
			var needsGrenadiers = grenadiers < grenadierTarget;
			var needsChemicals = chemicals < chemicalTarget;
			if (!needsGrenadiers && !needsChemicals)
				return EarlyInfantryProductionType.None;

			if (!needsChemicals)
				return EarlyInfantryProductionType.Grenadier;

			if (!needsGrenadiers)
				return EarlyInfantryProductionType.Chemical;

			// Keep both missions progressing instead of building either whole force first.
			return (long)chemicals * grenadierTarget <= (long)grenadiers * chemicalTarget ?
				EarlyInfantryProductionType.Chemical : EarlyInfantryProductionType.Grenadier;
		}

		public static bool CanLaunchGroup(int pendingCount, int groupSize, int launchedGroups, int maximumGroups)
		{
			return groupSize > 0 && maximumGroups > 0 && pendingCount >= groupSize && launchedGroups < maximumGroups;
		}

		public static List<CPos> SelectSpacedCells(IEnumerable<CPos> orderedCandidates, int count, int spacingCells)
		{
			var result = new List<CPos>();
			if (orderedCandidates == null || count <= 0 || spacingCells <= 0)
				return result;

			var spacingSquared = spacingCells * spacingCells;
			foreach (var candidate in orderedCandidates)
			{
				var separated = true;
				foreach (var selected in result)
					if ((selected - candidate).LengthSquared < spacingSquared)
					{
						separated = false;
						break;
					}

				if (!separated)
					continue;

				result.Add(candidate);
				if (result.Count == count)
					break;
			}

			return result;
		}

		public static bool IsHolding(int worldTick, int holdUntilTick)
		{
			return worldTick < holdUntilTick;
		}

		public static long TargetScore(int priority, int value, long distanceSquared, bool incumbent)
		{
			if (priority <= 0)
				return 0;

			var distanceCellsSquared = Math.Max(0, distanceSquared / (1024L * 1024L));
			var score = (long)priority * 1000000 + Math.Max(1, value) * 1000L - Math.Min(999999, distanceCellsSquared);
			return incumbent ? score * 110 / 100 : score;
		}
	}
}
