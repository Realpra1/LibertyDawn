#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. You can redistribute
 * it and/or modify it under the terms of the GNU General Public License as
 * published by the Free Software Foundation, either version 3 of the License,
 * or (at your option) any later version. For more details, see COPYING.
 */
#endregion

using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Marks this actor as a mutant for world creation policies.")]
	public class MutantInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new Mutant(init); }
	}

	public class Mutant : INotifyAddedToWorld
	{
		public bool SpawnedByMap { get; }
		public bool HasEnteredWorld { get; private set; }

		public Mutant(ActorInitializer init)
		{
			SpawnedByMap = init.GetOrDefault<SpawnedByMapInit>() != null;
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			HasEnteredWorld = true;
		}
	}
}
