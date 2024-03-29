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

using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Lets the actor spread resources around it in a circle.")]
	class SeedsResourceInfo : ConditionalTraitInfo
	{
		public readonly int Interval = 75;
		public readonly string ResourceType = "Ore";
		public readonly int MaxRange = 100;
		[Desc("Will only place resource of density of 1 around itself, never more or further.")]
		public readonly bool SeedOnly = false;

		public override object Create(ActorInitializer init) { return new SeedsResource(init.Self, this); }
	}

	class SeedsResource : ConditionalTrait<SeedsResourceInfo>, ITick, ISeedableResource
	{
		readonly SeedsResourceInfo info;
		readonly IResourceLayer resourceLayer;

		public SeedsResource(Actor self, SeedsResourceInfo info)
			: base(info)
		{
			this.info = info;
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
				ticks = info.Interval;
			}
		}

		public void Seed(Actor self)
		{
			CPos? cell;

			if (info.SeedOnly)
			{
				cell = Util.CircularRandomNeighbors(self.Location, self.World.SharedRandom)
					.Take(8)
					.SkipWhile(p => resourceLayer.GetResource(p).Type == info.ResourceType
						&& !resourceLayer.CanAddResource(info.ResourceType, p, resourceLayer.GetMaxDensity(info.ResourceType)))
					.Cast<CPos?>().FirstOrDefault();
			}
			else
			{
				cell = Util.RandomWalk(self.Location, self.World.SharedRandom)
					.Take(info.MaxRange)
					.SkipWhile(p => resourceLayer.GetResource(p).Type == info.ResourceType && !resourceLayer.CanAddResource(info.ResourceType, p))
					.Cast<CPos?>().FirstOrDefault();
			}

			if (cell != null && resourceLayer.CanAddResource(info.ResourceType, cell.Value))
				resourceLayer.AddResource(info.ResourceType, cell.Value);
		}
	}
}
