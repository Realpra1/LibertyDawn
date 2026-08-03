#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	class BaseBuilderFirstTowerPlanner
	{
		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		CPos? plannedLocation;
		string plannedType;

		BaseBuilderBotModuleInfo Info => baseBuilder.Info;

		public bool Complete { get; set; }

		public BaseBuilderFirstTowerPlanner(BaseBuilderBotModule baseBuilder, Player player)
		{
			this.baseBuilder = baseBuilder;
			this.player = player;
			world = player.World;
		}

		public void Update()
		{
			if (Complete || Info.FirstTowerTypes.Count == 0)
				return;

			var tower = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead && Info.FirstTowerTypes.Contains(a.Info.Name))
				.OrderBy(a => a.ActorID).FirstOrDefault();
			if (tower == null)
				return;

			Complete = true;
			LogDecision("{0} completed first-tower placement: {1} at {2}; planned={3}.",
				player, tower.Info.Name, tower.Location, plannedLocation?.ToString() ?? "existing");
		}

		public bool AppliesTo(string actorType)
		{
			return !Complete && Info.FirstTowerTypes.Contains(actorType);
		}

		public CPos? ChooseLocation(ActorInfo towerInfo, BuildingInfo towerBuildingInfo)
		{
			if (!AppliesTo(towerInfo.Name))
				return null;

			var yard = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead && Info.ConstructionYardTypes.Contains(a.Info.Name))
				.OrderBy(a => a.ActorID).FirstOrDefault();
			var yardInfo = yard?.Info.TraitInfoOrDefault<BuildingInfo>();
			if (yardInfo == null)
				return null;

			var preferred = FirstTowerPlacementLogic.PreferredLocation(yard.Location, yardInfo.Dimensions, towerBuildingInfo.Dimensions);
			var selected = FirstTowerPlacementLogic.ClosestLegalLocation(preferred, Info.FirstTowerSearchRadius,
				c => world.Map.Contains(c) && world.CanPlaceBuilding(c, towerInfo, towerBuildingInfo, null) &&
					towerBuildingInfo.IsCloseEnoughToBase(world, player, towerInfo, c));

			if (selected == null)
			{
				LogDecision("{0} found no legal first-tower location for {1} within {2} cells of {3}.",
					player, towerInfo.Name, Info.FirstTowerSearchRadius, preferred);
				return null;
			}

			plannedType = towerInfo.Name;
			plannedLocation = selected.Value;
			LogDecision("{0} selected first-tower location for {1}: target={2}, preferred={3}, offset-distance={4}.",
				player, plannedType, selected.Value, preferred, (selected.Value - preferred).LengthSquared);
			return selected.Value;
		}

		void LogDecision(string format, params object[] args)
		{
			AIUtils.BotDebug(format, args);
			if (Info.FirstTowerDebugLogging)
				Log.Write("debug", "AI first tower: " + format, args);
		}
	}
}
