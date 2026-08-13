#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Tracks live mobile combat actors that are not currently claimed by a bot controller.")]
	public sealed class UnassignedCombatUnitRegistryBotModuleInfo : TraitInfo, IRulesetLoaded
	{
		[Desc("Combat-capable actor types that must never enter the unassigned registry.")]
		public readonly HashSet<string> ExcludedActorTypes = new HashSet<string>();

		[Desc("Write bounded registry lifecycle changes to debug.log.")]
		public readonly bool DebugLogging = false;

		[Desc("Ticks between complete registry consistency audits. The CNC default is approximately 120 seconds.")]
		public readonly int AuditInterval = 3000;

		[Desc("Maximum actor ids examined by a registry audit on one bot tick.")]
		public readonly int AuditActorsPerTick = 32;

		[Desc("Testing only: remove one registered actor of this type when its first periodic audit begins.")]
		public readonly string AuditTestSkipActorType = null;

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (AuditInterval <= 0 || AuditActorsPerTick <= 0)
				throw new YamlException("Unassigned registry audit interval and per-tick actor budget must be greater than zero.");

			if (AuditTestSkipActorType != null && !rules.Actors.ContainsKey(AuditTestSkipActorType))
				throw new YamlException($"AuditTestSkipActorType actor '{AuditTestSkipActorType}' does not exist.");
		}

		public override object Create(ActorInitializer init)
		{
			return new UnassignedCombatUnitRegistryBotModule(init.Self, this);
		}
	}

	public sealed class UnassignedCombatUnitRegistryBotModule : IBotEnabled, IBotTick,
		IUnassignedCombatUnitRegistry, IGameSaveTraitData
	{
		readonly UnassignedCombatUnitRegistryBotModuleInfo info;
		readonly World world;
		readonly Player player;
		readonly BotModules.UnassignedCombatUnitRegistry registry =
			new BotModules.UnassignedCombatUnitRegistry();
		IBotUnitReservations[] reservations;
		bool enabled;
		uint maximumObservedActorId;
		bool initialReconstructionActive;
		uint initialReconstructionNextActorId;
		uint initialReconstructionEndActorId;
		int nextAuditTick;
		bool auditActive;
		int auditStartTick;
		uint auditNextActorId;
		uint auditEndActorId;
		int auditActors;
		int auditEligibleActors;
		int auditCorrections;
		uint auditTestSkippedActorId;

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
			initialReconstructionActive = true;
			initialReconstructionNextActorId = 1;
			initialReconstructionEndActorId = maximumObservedActorId;
			if (nextAuditTick == 0)
			{
				var playerIndex = player.ClientIndex;
				// Twenty deterministic buckets spread normal lobby clients without making the
				// first consistency check wait almost a full interval in two-player games.
				var playerCount = Math.Max(20, Math.Max(world.LobbyInfo.Clients.Count, playerIndex + 1));
				nextAuditTick = world.WorldTick + 1 + BotModules.UnassignedCombatUnitRegistry
					.StaggeredAuditStartOffset(info.AuditInterval, playerIndex, playerCount);
			}

			if (info.DebugLogging)
			{
				var owned = world.Actors.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead).ToArray();
				Log.Write("debug", "Unassigned combat registry [{0}]: action=enabled reservations={1} owned={2} " +
					"attack={3} mobile={4} reserved={5}.", player.PlayerName, reservations.Length, owned.Length,
					owned.Count(a => a.Info.HasTraitInfo<AttackBaseInfo>()), owned.Count(a =>
						a.Info.HasTraitInfo<MobileInfo>() || a.Info.HasTraitInfo<AircraftInfo>()), owned.Count(IsReserved));
			}
			foreach (var actor in world.Actors.OrderBy(a => a.ActorID))
			{
				maximumObservedActorId = Math.Max(maximumObservedActorId, actor.ActorID);
				Reclassify(actor, "bot-enabled");
			}
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (!enabled)
				return;

			if (initialReconstructionActive)
			{
				initialReconstructionEndActorId = Math.Max(initialReconstructionEndActorId, maximumObservedActorId);
				foreach (var actorId in BotModules.UnassignedCombatUnitRegistry.NextAuditActorIds(
					ref initialReconstructionNextActorId, initialReconstructionEndActorId, info.AuditActorsPerTick))
				{
					var actor = world.GetActorById(actorId);
					if (actor != null)
						Reclassify(actor, "initial-reconstruction");
				}

				initialReconstructionActive = initialReconstructionNextActorId <= initialReconstructionEndActorId;
			}

			if (!auditActive)
			{
				if (world.WorldTick < nextAuditTick)
					return;

				InjectAuditTestMiss();
				auditActive = true;
				auditStartTick = world.WorldTick;
				auditNextActorId = 1;
				auditEndActorId = maximumObservedActorId;
				auditActors = 0;
				auditEligibleActors = 0;
				auditCorrections = 0;
			}

			foreach (var actorId in BotModules.UnassignedCombatUnitRegistry.NextAuditActorIds(
				ref auditNextActorId, auditEndActorId, info.AuditActorsPerTick))
			{
				var actor = world.GetActorById(actorId);
				if (actor == null)
					continue;

				auditActors++;
				if (IsEligible(actor))
					auditEligibleActors++;
				if (Audit(actor))
					auditCorrections++;
			}

			if (auditNextActorId <= auditEndActorId)
				return;

			auditActive = false;
			nextAuditTick = Math.Max(world.WorldTick + 1, auditStartTick + info.AuditInterval);
			if (info.DebugLogging)
				Log.Write("debug", "Unassigned combat registry audit [{0}]: start={1} end={2} duration={3} " +
					"actors={4} eligible={5} corrections={6} actor-ids-per-tick={7} next={8}.",
					player.PlayerName, auditStartTick, world.WorldTick, world.WorldTick - auditStartTick + 1,
					auditActors, auditEligibleActors, auditCorrections, info.AuditActorsPerTick, nextAuditTick);
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
			{
				registry.Release(actor.ActorID);
				Register(actor, "released");
			}
		}

		void IUnassignedCombatUnitRegistry.ClaimActors(IEnumerable<Actor> actors)
		{
			foreach (var actor in actors.Where(a => a != null).OrderBy(a => a.ActorID))
			{
				var wasRegistered = registry.Contains(actor.ActorID);
				registry.Claim(actor.ActorID);
				if (wasRegistered && info.DebugLogging)
					Log.Write("debug", "Unassigned combat registry [{0}]: action=remove reason=claimed actor={1}#{2} count={3}.",
						player.PlayerName, actor.Info.Name, actor.ActorID, registry.ActorIds.Length);
			}
		}

		void ActorAdded(Actor actor)
		{
			if (actor != null)
				maximumObservedActorId = Math.Max(maximumObservedActorId, actor.ActorID);

			if (enabled)
			{
				registry.Forget(actor.ActorID);
				// ActorAdded can run before trait initialization has completed. Reclassify at
				// frame end so production and world additions enter the registry immediately
				// with their final owner and capability traits.
				world.AddFrameEndTask(w =>
				{
					if (enabled)
						Reclassify(actor, "added");
				});
			}
		}

		void ActorRemoved(Actor actor)
		{
			if (enabled && actor != null)
			{
				var wasRegistered = registry.Contains(actor.ActorID);
				registry.Forget(actor.ActorID);
				if (wasRegistered && info.DebugLogging)
					Log.Write("debug", "Unassigned combat registry [{0}]: action=remove reason=removed actor={1}#{2} count={3}.",
						player.PlayerName, actor.Info.Name, actor.ActorID, registry.ActorIds.Length);
			}
		}

		void Prune()
		{
			foreach (var id in registry.ActorIds)
			{
				var actor = world.GetActorById(id);
				if (!IsEligible(actor) || IsReserved(actor) || registry.IsClaimed(id))
					Remove(id, actor, "classification-changed");
			}
		}

		void Reclassify(Actor actor, string reason)
		{
			if (IsEligible(actor) && !IsReserved(actor) && !registry.IsClaimed(actor.ActorID))
				Register(actor, reason);
			else if (actor != null)
				Remove(actor.ActorID, actor, reason);
		}

		void ReclassifyIfInvalid(Actor actor, string reason)
		{
			if (registry.Contains(actor.ActorID) && (!IsEligible(actor) || IsReserved(actor) || registry.IsClaimed(actor.ActorID)))
				Remove(actor.ActorID, actor, reason);
		}

		bool Audit(Actor actor)
		{
			if (actor.Owner != player || !IsEligible(actor))
				return registry.Forget(actor.ActorID);

			if (IsReserved(actor) || registry.IsClaimed(actor.ActorID))
				return registry.Remove(actor.ActorID);

			return registry.Register(actor.ActorID);
		}

		void InjectAuditTestMiss()
		{
			if (info.AuditTestSkipActorType == null || auditTestSkippedActorId != 0)
				return;

			var actor = registry.ActorIds.Select(world.GetActorById).FirstOrDefault(a =>
				a != null && a.Info.Name == info.AuditTestSkipActorType && IsEligible(a) && !IsReserved(a));
			if (actor == null)
				return;

			auditTestSkippedActorId = actor.ActorID;
			registry.Forget(actor.ActorID);
			if (info.DebugLogging)
				Log.Write("debug", "Unassigned combat registry [{0}]: action=test-missed-entry actor={1}#{2} count={3}.",
					player.PlayerName, actor.Info.Name, actor.ActorID, registry.ActorIds.Length);
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

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			return new List<MiniYamlNode>
			{
				new MiniYamlNode("UnassignedActors", FieldSaver.FormatValue(registry.ActorIds)),
				new MiniYamlNode("ClaimedActors", FieldSaver.FormatValue(registry.ClaimedActorIds)),
				new MiniYamlNode("MaximumObservedActorId", FieldSaver.FormatValue(maximumObservedActorId)),
				new MiniYamlNode("InitialReconstructionActive", FieldSaver.FormatValue(initialReconstructionActive)),
				new MiniYamlNode("InitialReconstructionNextActorId", FieldSaver.FormatValue(initialReconstructionNextActorId)),
				new MiniYamlNode("InitialReconstructionEndActorId", FieldSaver.FormatValue(initialReconstructionEndActorId)),
				new MiniYamlNode("NextAuditTick", FieldSaver.FormatValue(nextAuditTick)),
				new MiniYamlNode("AuditActive", FieldSaver.FormatValue(auditActive)),
				new MiniYamlNode("AuditStartTick", FieldSaver.FormatValue(auditStartTick)),
				new MiniYamlNode("AuditNextActorId", FieldSaver.FormatValue(auditNextActorId)),
				new MiniYamlNode("AuditEndActorId", FieldSaver.FormatValue(auditEndActorId)),
				new MiniYamlNode("AuditActors", FieldSaver.FormatValue(auditActors)),
				new MiniYamlNode("AuditEligibleActors", FieldSaver.FormatValue(auditEligibleActors)),
				new MiniYamlNode("AuditCorrections", FieldSaver.FormatValue(auditCorrections)),
				new MiniYamlNode("AuditTestSkippedActorId", FieldSaver.FormatValue(auditTestSkippedActorId)),
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var fields = data.ToDictionary(n => n.Key);
			if (fields.TryGetValue("UnassignedActors", out var unassigned) && fields.TryGetValue("ClaimedActors", out var claimed))
				registry.Import(FieldLoader.GetValue<uint[]>("UnassignedActors", unassigned.Value.Value),
					FieldLoader.GetValue<uint[]>("ClaimedActors", claimed.Value.Value));

			maximumObservedActorId = Load(fields, "MaximumObservedActorId", maximumObservedActorId);
			initialReconstructionActive = Load(fields, "InitialReconstructionActive", initialReconstructionActive);
			initialReconstructionNextActorId = Load(fields, "InitialReconstructionNextActorId", initialReconstructionNextActorId);
			initialReconstructionEndActorId = Load(fields, "InitialReconstructionEndActorId", initialReconstructionEndActorId);
			nextAuditTick = Load(fields, "NextAuditTick", nextAuditTick);
			auditActive = Load(fields, "AuditActive", auditActive);
			auditStartTick = Load(fields, "AuditStartTick", auditStartTick);
			auditNextActorId = Load(fields, "AuditNextActorId", auditNextActorId);
			auditEndActorId = Load(fields, "AuditEndActorId", auditEndActorId);
			auditActors = Load(fields, "AuditActors", auditActors);
			auditEligibleActors = Load(fields, "AuditEligibleActors", auditEligibleActors);
			auditCorrections = Load(fields, "AuditCorrections", auditCorrections);
			auditTestSkippedActorId = Load(fields, "AuditTestSkippedActorId", auditTestSkippedActorId);
			if (info.DebugLogging)
			{
				var unassignedIds = registry.ActorIds;
				var claimedIds = registry.ClaimedActorIds;
				Log.Write("debug", "Unassigned combat registry [{0}]: action=loaded unassigned={1} claimed={2} " +
					"overlap={3} unassigned-digest={4:X8} claimed-digest={5:X8} audit-active={6} next={7}.",
					player.PlayerName, unassignedIds.Length, claimedIds.Length, unassignedIds.Intersect(claimedIds).Count(),
					BotModules.UnassignedCombatUnitRegistry.StableActorIdDigest(unassignedIds),
					BotModules.UnassignedCombatUnitRegistry.StableActorIdDigest(claimedIds), auditActive, nextAuditTick);
			}
		}

		static T Load<T>(Dictionary<string, MiniYamlNode> fields, string key, T fallback)
		{
			return fields.TryGetValue(key, out var node) ? FieldLoader.GetValue<T>(key, node.Value.Value) : fallback;
		}
	}
}
