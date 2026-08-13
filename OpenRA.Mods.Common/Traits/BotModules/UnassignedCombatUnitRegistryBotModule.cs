#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Tracks live mobile combat actors that are not currently claimed by a bot controller.")]
	public sealed class UnassignedCombatUnitRegistryBotModuleInfo : TraitInfo
	{
		[Desc("Combat-capable actor types that must never enter the unassigned registry.")]
		public readonly HashSet<string> ExcludedActorTypes = new HashSet<string>();

		[Desc("Write bounded registry lifecycle changes to debug.log.")]
		public readonly bool DebugLogging = false;

		public override object Create(ActorInitializer init)
		{
			return new UnassignedCombatUnitRegistryBotModule(init.Self, this);
		}
	}

	public sealed class UnassignedCombatUnitRegistryBotModule : IBotEnabled, IUnassignedCombatUnitRegistry
	{
		readonly UnassignedCombatUnitRegistryBotModuleInfo info;
		readonly World world;
		readonly Player player;
		readonly BotModules.UnassignedCombatUnitRegistry registry =
			new BotModules.UnassignedCombatUnitRegistry();
		IBotUnitReservations[] reservations;
		bool enabled;

		public UnassignedCombatUnitRegistryBotModule(Actor self, UnassignedCombatUnitRegistryBotModuleInfo info)
		{
			this.info = info;
			world = self.World;
			player = self.Owner;
			world.ActorAdded += ActorAdded;
			world.ActorRemoved += ActorRemoved;
		}

		void IBotEnabled.BotEnabled(IBot bot)
		{
			enabled = true;
			reservations = player.PlayerActor.TraitsImplementing<IBotUnitReservations>().ToArray();
			if (info.DebugLogging)
			{
				var owned = world.Actors.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead).ToArray();
				Log.Write("debug", "Unassigned combat registry [{0}]: action=enabled reservations={1} owned={2} " +
					"attack={3} mobile={4} reserved={5}.", player.PlayerName, reservations.Length, owned.Length,
					owned.Count(a => a.Info.HasTraitInfo<AttackBaseInfo>()), owned.Count(a =>
						a.Info.HasTraitInfo<MobileInfo>() || a.Info.HasTraitInfo<AircraftInfo>()), owned.Count(IsReserved));
			}
			foreach (var actor in world.Actors.OrderBy(a => a.ActorID))
				Reclassify(actor, "bot-enabled");
		}

		IEnumerable<Actor> IUnassignedCombatUnitRegistry.UnassignedActors
		{
			get
			{
				Prune();
				return registry.ActorIds.Select(world.GetActorById).Where(a => a != null).ToArray();
			}
		}

		bool IUnassignedCombatUnitRegistry.IsRegistered(Actor actor)
		{
			if (actor == null)
				return false;

			ReclassifyIfInvalid(actor, "classification-changed");
			return registry.Contains(actor.ActorID);
		}

		void IUnassignedCombatUnitRegistry.RegisterReleasedActors(IEnumerable<Actor> actors)
		{
			foreach (var actor in actors.Where(IsEligible).OrderBy(a => a.ActorID))
				Register(actor, "released");
		}

		void IUnassignedCombatUnitRegistry.ClaimActors(IEnumerable<Actor> actors)
		{
			foreach (var actor in actors.Where(a => a != null).OrderBy(a => a.ActorID))
				Remove(actor.ActorID, actor, "claimed");
		}

		void ActorAdded(Actor actor)
		{
			if (enabled)
				Reclassify(actor, "added");
		}

		void ActorRemoved(Actor actor)
		{
			if (enabled && actor != null)
				Remove(actor.ActorID, actor, "removed");
		}

		void Prune()
		{
			foreach (var id in registry.ActorIds)
			{
				var actor = world.GetActorById(id);
				if (!IsEligible(actor) || IsReserved(actor))
					Remove(id, actor, "classification-changed");
			}
		}

		void Reclassify(Actor actor, string reason)
		{
			if (IsEligible(actor) && !IsReserved(actor))
				Register(actor, reason);
			else if (actor != null)
				Remove(actor.ActorID, actor, reason);
		}

		void ReclassifyIfInvalid(Actor actor, string reason)
		{
			if (registry.Contains(actor.ActorID) && (!IsEligible(actor) || IsReserved(actor)))
				Remove(actor.ActorID, actor, reason);
		}

		bool IsEligible(Actor actor)
		{
			return actor != null && actor.Owner == player && actor.IsInWorld && !actor.IsDead &&
				!info.ExcludedActorTypes.Contains(actor.Info.Name) && actor.Info.HasTraitInfo<AttackBaseInfo>() &&
				(actor.Info.HasTraitInfo<MobileInfo>() || actor.Info.HasTraitInfo<AircraftInfo>());
		}

		bool IsReserved(Actor actor)
		{
			return reservations != null && reservations.Any(r => r.IsUnitReserved(actor));
		}

		void Register(Actor actor, string reason)
		{
			if (registry.Register(actor.ActorID) && info.DebugLogging)
				Log.Write("debug", "Unassigned combat registry [{0}]: action=register reason={1} actor={2}#{3} count={4}.",
					player.PlayerName, reason, actor.Info.Name, actor.ActorID, registry.ActorIds.Length);
		}

		void Remove(uint actorId, Actor actor, string reason)
		{
			if (registry.Remove(actorId) && info.DebugLogging)
				Log.Write("debug", "Unassigned combat registry [{0}]: action=remove reason={1} actor={2}#{3} count={4}.",
					player.PlayerName, reason, actor?.Info.Name ?? "missing", actorId, registry.ActorIds.Length);
		}
	}
}
