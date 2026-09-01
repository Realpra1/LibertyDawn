#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is made available to you under the terms of
 * the GNU General Public License as published by the Free Software Foundation.
 */
#endregion

using System;

namespace OpenRA.Mods.Common.Traits
{
	#pragma warning disable SA1205
	static partial class StealthRepairPersistence
	#pragma warning restore SA1205
	{
		internal static MiniYamlNode SerializePendingDamage(StealthLifecycleDamageYield yielded)
		{
			if (yielded == null)
				throw new ArgumentNullException(nameof(yielded));
			var damage = new StealthBehaviorHandoff(BehaviorId.Damage,
				new OwnershipEpoch(yielded.Handoff.Epoch.Value + 1));
			var request = new StealthDamageRepairRequest(damage, yielded.DamageEventId,
				yielded.DamageTick, yielded.DamageSourceActorId, yielded.DamageAmount,
				yielded.DamagedMembers, yielded.Resume);
			var repair = new StealthRepairHandoff(new StealthBehaviorHandoff(BehaviorId.Repair,
				new OwnershipEpoch(yielded.Handoff.Epoch.Value + 2)), request);
			return Node("PendingDamage", new[]
			{
				StealthApproachPersistence.SerializeMission(repair.Mission), SerializeCause(repair),
				SerializeResume(repair.Resume)
			});
		}

		internal static StealthLifecycleDamageYield RestorePendingDamage(
			StealthBehaviorHandoff active, MiniYamlNode node)
		{
			if (active == null || !StealthRepairResumeContext.IsFightOwner(active.Owner) || node == null)
				throw new InvalidOperationException("Pending Damage requires one active fight owner.");
			RequireCount(node, "Mission", 1);
			RequireCount(node, "Cause", 1);
			RequireCount(node, "Resume", 1);
			if (node.Value.Nodes.Count != 3)
				throw new InvalidOperationException("Pending Damage has a noncanonical field set.");
			var mission = StealthApproachPersistence.RestoreMission(Required(node, "Mission"));
			var resume = RestoreResume(Required(node, "Resume"), mission);
			var cause = RestoreCause(Required(node, "Cause"));
			if (resume.Owner != active.Owner || resume.Epoch != active.Epoch)
				throw new InvalidOperationException("Pending Damage does not match active ownership.");
			return new StealthLifecycleDamageYield(active, cause.EventId, cause.Tick, cause.Source,
				cause.Amount, cause.Members, resume);
		}
	}
}
