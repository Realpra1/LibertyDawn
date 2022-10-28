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

using OpenRA.Mods.Common.Effects;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Blinks the actor.")]
	class BlinkInfo : ConditionalTraitInfo
	{
		[Desc("Number of ticks to wait between repeating blinks.")]
		public readonly int Interval = 25;

		[Desc("Delay before starting to blink.")]
		public readonly int StartDelay = 0;

		[Desc("Blink color.")]
		public readonly string BlinkColor = "FFFFFF";

		public override object Create(ActorInitializer init) { return new Blink(this); }
	}

	class Blink : ConditionalTrait<BlinkInfo>, ITick
	{
		int tick = -1;
		readonly Primitives.Color color = Primitives.Color.White;

		public Blink(BlinkInfo info)
			: base(info)
		{
			if (!Primitives.Color.TryParse(info.BlinkColor, out var confColor))
				color = confColor;
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled)
			{
				tick = -1;
				return;
			}

			if (tick == -1)
				tick = Info.StartDelay;

			if (--tick <= 0)
			{
				tick = Info.Interval;
				self.World.Add(new FlashTarget(self, color));
			}
		}
	}
}
