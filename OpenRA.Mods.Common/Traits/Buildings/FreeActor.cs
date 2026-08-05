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
using System.Runtime.CompilerServices;
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

		[ActorReference]
		[Desc("Actor types sharing an owner-wide spawn limit with this free actor.")]
		public readonly HashSet<string> OwnerActorLimitTypes = new HashSet<string>();

		[Desc("Maximum live and queued OwnerActorLimitTypes allowed before suppressing this free actor.",
			"Zero disables the limit.")]
		public readonly int OwnerActorLimit = 0;

		[Desc("Apply OwnerActorLimit only when the building owner is a bot.")]
		public readonly bool OwnerActorLimitBotsOnly = false;

		[Desc("Write a debug-log entry when OwnerActorLimit suppresses this free actor.")]
		public readonly bool OwnerActorLimitDebugLogging = false;

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
		protected bool onSpawnHappened = false;

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
			onSpawnHappened = true;
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

			if (info.AtSpawnOnly)
				if (!spawnEvent || onSpawnHappened)
					return;
				else
					onSpawnHappened = true;

			allowSpawn = Info.AllowRespawn;

			self.World.AddFrameEndTask(w =>
			{
				if (Info.OwnerActorLimit > 0 && (!Info.OwnerActorLimitBotsOnly || self.Owner.IsBot))
				{
					var live = w.Actors.Count(a => a.Owner == self.Owner && !a.IsDead &&
						Info.OwnerActorLimitTypes.Contains(a.Info.Name));
					var queued = w.ActorsWithTrait<ProductionQueue>()
						.Where(q => q.Actor.Owner == self.Owner && !q.Actor.IsDead && q.Actor.IsInWorld)
						.Sum(q => q.Trait.AllQueued().Count(i => Info.OwnerActorLimitTypes.Contains(i.Item)));
					var pending = w.ActorsWithTrait<IPendingProductionActors>()
						.Where(p => p.Actor.Owner == self.Owner && !p.Actor.IsDead && p.Actor.IsInWorld)
						.Sum(p => p.Trait.PendingActorTypes.Count(Info.OwnerActorLimitTypes.Contains));
					if (SharedActorLimitPolicy.AllowedAmount(1, live + queued + pending,
						SharedActorLimitReservations.Reserved(w, self.Owner), Info.OwnerActorLimit) == 0)
					{
						if (Info.OwnerActorLimitDebugLogging)
							Log.Write("debug", "Free actor {0} from {1} suppressed for {2}: shared actors={3} live+{4} queued/{5}.",
								Info.Actor, self.Info.Name, self.Owner, live, queued + pending, Info.OwnerActorLimit);

						return;
					}
				}

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

	public static class SharedActorLimitPolicy
	{
		public static bool CanSpawn(int liveActors, int queuedActors, int maximumActors)
		{
			return maximumActors <= 0 || (long)Math.Max(0, liveActors) + Math.Max(0, queuedActors) < maximumActors;
		}

		public static int AllowedAmount(int requestedActors, int committedActors, int reservedActors, int maximumActors)
		{
			if (requestedActors <= 0)
				return 0;

			if (maximumActors <= 0)
				return requestedActors;

			var available = (long)maximumActors - Math.Max(0, committedActors) - Math.Max(0, reservedActors);
			return (int)Math.Min(requestedActors, Math.Max(0L, available));
		}
	}

	// Production orders and free-actor creation are both deferred until frame end. Keep their
	// same-tick claims in one owner-wide ledger so several queues/refineries cannot all observe the
	// same final slot. This state is deliberately ephemeral: the live and queued actors are the
	// authoritative state again on the next simulation tick and across save/load.
	public static class SharedActorLimitReservations
	{
		sealed class WorldReservations
		{
			public int Tick = int.MinValue;
			public readonly Dictionary<Player, int> ByOwner = new Dictionary<Player, int>();
		}

		static readonly ConditionalWeakTable<World, WorldReservations> Reservations =
			new ConditionalWeakTable<World, WorldReservations>();

		static WorldReservations For(World world)
		{
			var reservations = Reservations.GetOrCreateValue(world);
			if (reservations.Tick != world.WorldTick)
			{
				reservations.Tick = world.WorldTick;
				reservations.ByOwner.Clear();
			}

			return reservations;
		}

		public static int Reserved(World world, Player owner)
		{
			return For(world).ByOwner.TryGetValue(owner, out var reserved) ? reserved : 0;
		}

		public static void Reserve(World world, Player owner, int amount)
		{
			if (amount <= 0)
				return;

			var reservations = For(world);
			reservations.ByOwner[owner] = Reserved(world, owner) + amount;
		}

		public static bool TryReserve(World world, Player owner, int committedActors,
			int maximumActors, int requestedActors)
		{
			var allowed = SharedActorLimitPolicy.AllowedAmount(requestedActors, committedActors,
				Reserved(world, owner), maximumActors);
			if (allowed < requestedActors)
				return false;

			Reserve(world, owner, allowed);
			return true;
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
