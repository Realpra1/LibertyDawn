#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. You can redistribute
 * it and/or modify it under the terms of the GNU General Public License as
 * published by the Free Software Foundation, either version 3 of the License,
 * or (at your option) any later version.
 */
#endregion

using OpenRA.Mods.Common.Traits;
using OpenRA.Scripting;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Scripting
{
	[ScriptPropertyGroup("Harvester")]
	public class UnstableHarvesterDetonationProperties : ScriptActorProperties,
		Requires<UnstableHarvesterDetonationInfo>
	{
		readonly UnstableHarvesterDetonation detonation;

		public UnstableHarvesterDetonationProperties(ScriptContext context, Actor self)
			: base(context, self)
		{
			detonation = self.Trait<UnstableHarvesterDetonation>();
		}

		[Desc("Returns true when continuous unstable cargo has reached its deploy detonation age.")]
		public bool CanDetonateUnstable => detonation.CanDetonate;

		[Desc("Returns the continuous unstable cargo age in game ticks.")]
		public int UnstableCargoAge => detonation.UnstableTicks;

		[Desc("Attempts the same unstable-cargo detonation used by the deploy order.")]
		public bool DetonateUnstable()
		{
			return detonation.TryDetonate(Self);
		}
	}
}
