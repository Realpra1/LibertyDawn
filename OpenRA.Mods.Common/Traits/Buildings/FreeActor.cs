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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Player receives a unit for free once the building is placed. This also works for structures.",
		"If you want more than one unit to appear copy this section and assign IDs like FreeActor@2, ...")]
	public class FreeActorInfo : ConditionalTraitInfo, IEditorActorOptions
	{
		[ActorReference]
		[FieldLoader.Require]
		[Desc("Name of the actor.")]
		public readonly string Actor = null;

		[Desc("Offset relative to the top-left cell of the building.")]
		public readonly CVec SpawnOffset = CVec.Zero;

		[Desc("Which direction the unit should face.")]
		public readonly WAngle Facing = WAngle.Zero;

		[Desc("Whether another actor should spawn upon re-enabling the trait.")]
		public readonly bool AllowRespawn = false;

		[Desc("Free actor can only spawn if trait enabled at create event, if not, then it never spawns.")]
		public readonly bool AtSpawnOnly = false;

		[Desc("Display order for the free actor checkbox in the map editor")]
		public readonly int EditorFreeActorDisplayOrder = 4;

		[Desc("List of required prerequisites.")]
		public readonly string[] Prerequisites = Array.Empty<string>();

		IEnumerable<EditorActorOption> IEditorActorOptions.ActorOptions(ActorInfo ai, World world)
		{
			yield return new EditorActorCheckbox("Spawn Child Actor", EditorFreeActorDisplayOrder,
				actor =>
				{
					var init = actor.GetInitOrDefault<FreeActorInit>(this);
					if (init != null)
						return init.Value;

					return true;
				},
				(actor, value) =>
				{
					actor.ReplaceInit(new FreeActorInit(this, value), this);
				});
		}

		public override object Create(ActorInitializer init) { return new FreeActor(init, this); }
	}

	public class FreeActor : ConditionalTrait<FreeActorInfo>, INotifyCreated, INotifyAddedToWorld, INotifyRemovedFromWorld, INotifyOwnerChanged, INotifyPrerequisitesUpdated
	{
		protected bool wasAvailable;
		protected GrantConditionOnPrerequisiteManager globalManager;

		protected FreeActorInfo info;

		protected bool allowSpawn;
		protected bool hasSpawned = false;

		public FreeActor(ActorInitializer init, FreeActorInfo info)
			: base(info)
		{
			allowSpawn = init.GetValue<FreeActorInit, bool>(info, true);
			this.info = info;
		}

		protected override void Created(Actor self)
		{
			globalManager = self.Owner.PlayerActor.Trait<GrantConditionOnPrerequisiteManager>();
			base.Created(self);
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			Register(self);
			SpawnLogic(self, true);
		}

		void Register(Actor self)
		{
			if (info.Prerequisites.Any())
				globalManager.Register(self, this, info.Prerequisites);
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			Unregister(self);
		}

		void Unregister(Actor self)
		{
			if (info.Prerequisites.Any())
				globalManager.Unregister(self, this, info.Prerequisites);
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			globalManager = newOwner.PlayerActor.Trait<GrantConditionOnPrerequisiteManager>();
		}

		void INotifyPrerequisitesUpdated.PrerequisitesUpdated(Actor self, bool available)
		{
			if (available == wasAvailable)
				return;

			wasAvailable = available;

			if (wasAvailable)
				SpawnLogic(self);
		}

		protected override void TraitEnabled(Actor self)
		{
			SpawnLogic(self);
		}

		void SpawnLogic(Actor self, bool spawnEvent = false)
		{
			if (!allowSpawn || (!wasAvailable && info.Prerequisites.Any()))
				return;

			if (info.AtSpawnOnly && !spawnEvent)
				return;

			allowSpawn = Info.AllowRespawn;

			self.World.AddFrameEndTask(w =>
			{
				w.CreateActor(Info.Actor, new TypeDictionary
				{
					new ParentActorInit(self),
					new LocationInit(self.Location + Info.SpawnOffset),
					new OwnerInit(self.Owner),
					new FacingInit(Info.Facing),
				});
			});
		}
	}

	public class FreeActorInit : ValueActorInit<bool>
	{
		public FreeActorInit(TraitInfo info, bool value)
			: base(info, value) { }
	}

	public class ParentActorInit : ValueActorInit<ActorInitActorReference>, ISingleInstanceInit
	{
		public ParentActorInit(Actor value)
			: base(value) { }
	}
}
