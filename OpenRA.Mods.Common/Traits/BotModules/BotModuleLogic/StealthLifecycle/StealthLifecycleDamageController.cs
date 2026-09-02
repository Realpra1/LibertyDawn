#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is made available to you under the terms of
 * the GNU General Public License as published by the Free Software Foundation.
 */
#endregion

namespace OpenRA.Mods.Common.Traits
{
	public sealed partial class StealthLifecycleController
	{
		/// <summary>Accepts Damage yielded under the active fight owner's execution lease.</summary>
		internal bool TryAccept(StealthLifecycleDamageYield yielded,
			out StealthDamageRepairRequest request)
		{
			request = null;
			if (yielded == null || !StealthRepairResumeContext.IsFightOwner(owner) ||
				yielded.Handoff.Owner != owner || yielded.Handoff.Epoch != epoch ||
				yielded.Resume.Owner != owner || yielded.Resume.Epoch != epoch)
				return false;
			var damage = AdvanceTo(BehaviorId.Damage);
			request = new StealthDamageRepairRequest(damage, yielded.DamageEventId,
				yielded.DamageTick, yielded.DamageSourceActorId, yielded.DamageAmount,
				yielded.DamagedMembers, yielded.Resume);
			return true;
		}
	}
}
