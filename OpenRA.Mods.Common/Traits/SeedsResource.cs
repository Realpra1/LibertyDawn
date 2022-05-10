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
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Lets the actor spread resources around it in a circle. Added support for resource cells to do the same.")]
	public class SeedsResourceInfo : ConditionalTraitInfo
	{
		public readonly int Interval = 75;
		public readonly int IntervalInSeconds = 0;
		public readonly int	GrowthRateInSeconds = 60;
		public readonly int PercentageChance = 75;
		public readonly string ResourceType = "Ore";
		public readonly int MaxRange = 100;
		public readonly int Stage = 1;

		public override object Create(ActorInitializer init) { return new SeedsResource(init.Self, this); }
	}

	public class SeedsResource : ConditionalTrait<SeedsResourceInfo>, ITick, ISeedableResource
	{
		readonly SeedsResourceInfo info;
		readonly IResourceLayer resourceLayer;

		public SeedsResource(Actor self, SeedsResourceInfo info)
			: base(info)
		{
			this.info = info;

			if (self.Info.Name != "world")
				resourceLayer = self.World.WorldActor.Trait<IResourceLayer>();
		}

		int ticks;

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled)
				return;

			if (--ticks <= 0)
			{
				Seed(self);
				if (info.IntervalInSeconds > 0)
					ticks = info.IntervalInSeconds * 25; // TODO: Find default tick value.
				else
					ticks = info.Interval;
			}
		}

		private static Random RNG = new Random();

		public void Seed(Actor self)
		{
			// Seeding from an actor in the world, or if this actor is the world, seed from resource cells instead.
			if (self.Info.Name != "world")
			{
				var cell = Util.RandomWalk(self.Location, self.World.SharedRandom)
					.Take(info.MaxRange)
					.SkipWhile(p => resourceLayer.GetResource(p).Type == info.ResourceType && !resourceLayer.CanAddResource(info.ResourceType, p))
					.Cast<CPos?>().FirstOrDefault();

				if (cell != null && resourceLayer.CanAddResource(info.ResourceType, cell.Value))
				{
					resourceLayer.AddResource(info.ResourceType, cell.Value);
				}
			}
			else
			{
				var worldResourceLayer = ResourceLayerInfo.WorldResourceLayer;
				var worldMap = self.World.Map;
				var worldCells = worldMap.AllCells;

				foreach (var cell in worldCells)
				{
					var resource = worldMap.Resources[cell];

					if (!worldResourceLayer.ResourceTypesByIndex.TryGetValue(resource.Type, out var resourceType))
						continue;

					if (!worldResourceLayer.CanSpreadResource(resourceType, cell))
						continue;

					var candidateCell = Util.RandomWalk(cell, self.World.SharedRandom)

					.Take(info.MaxRange)
					.SkipWhile(p => worldResourceLayer.GetResource(p).Type == info.ResourceType && !worldResourceLayer.CanAddResource(info.ResourceType, p))
					.Cast<CPos?>().FirstOrDefault();

					if (worldResourceLayer.CanAddResource(info.ResourceType, candidateCell.GetValueOrDefault()))
					{
						var canSeed = false;
						for (int i = 0; i < 100; i++)
						{
							var randomValue = RNG.Next(100);
							if (randomValue < info.PercentageChance)
								canSeed = true;
						}

						if (canSeed)
							worldResourceLayer.AddResource(info.ResourceType, candidateCell.GetValueOrDefault());
					}
				}
			}
		}
	}
}
