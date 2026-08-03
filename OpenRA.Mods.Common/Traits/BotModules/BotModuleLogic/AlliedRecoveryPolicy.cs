#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;

namespace OpenRA.Mods.Common.Traits.BotModules.BotModuleLogic
{
	public readonly struct AlliedRecoverySnapshot
	{
		public readonly int SpendableCash;
		public readonly int Harvesters;
		public readonly int Refineries;
		public readonly int Mcvs;
		public readonly int ProductionBuildings;
		public readonly int MobileUnits;

		public AlliedRecoverySnapshot(int spendableCash, int harvesters, int refineries,
			int mcvs, int productionBuildings, int mobileUnits)
		{
			SpendableCash = spendableCash;
			Harvesters = harvesters;
			Refineries = refineries;
			Mcvs = mcvs;
			ProductionBuildings = productionBuildings;
			MobileUnits = mobileUnits;
		}
	}

	public static class AlliedRecoveryPolicy
	{
		public static bool NeedsAid(in AlliedRecoverySnapshot snapshot, int maximumCash)
		{
			return snapshot.SpendableCash <= maximumCash && snapshot.Harvesters == 0 &&
				snapshot.Refineries == 0 && snapshot.Mcvs == 0;
		}

		public static bool CanRecover(in AlliedRecoverySnapshot snapshot)
		{
			return snapshot.ProductionBuildings > 0 || snapshot.Mcvs > 0 || snapshot.MobileUnits > 0;
		}

		public static bool ShouldAid(in AlliedRecoverySnapshot snapshot, int maximumCash)
		{
			return NeedsAid(snapshot, maximumCash) && CanRecover(snapshot);
		}

		public static int AvailableDispatches(int availableFactories, int recentDispatches, int pendingRequests)
		{
			return Math.Max(0, availableFactories - recentDispatches - pendingRequests);
		}
	}
}
