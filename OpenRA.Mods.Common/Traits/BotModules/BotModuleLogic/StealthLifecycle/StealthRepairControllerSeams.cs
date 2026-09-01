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

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Bounded transition validation only; it owns no state and registers no runtime loop.</summary>
	static class StealthRepairControllerSeams
	{
		public static bool TryAccept(StealthDamageRepairRequest request,
			BehaviorId owner, OwnershipEpoch epoch,
			Func<BehaviorId, StealthBehaviorHandoff> advance,
			out StealthRepairHandoff repairHandoff)
		{
			repairHandoff = null;
			if (request == null || owner != BehaviorId.Damage || request.Handoff.Owner != owner ||
				request.Handoff.Epoch != epoch || request.Resume == null ||
				request.Resume.Epoch.Value == long.MaxValue ||
				request.Handoff.Epoch.Value != request.Resume.Epoch.Value + 1)
				return false;

			repairHandoff = new StealthRepairHandoff(advance(BehaviorId.Repair), request);
			return true;
		}

		public static bool TryAccept(StealthRepairResult result,
			BehaviorId owner, OwnershipEpoch epoch, StealthBehaviorHandoff current,
			Func<BehaviorId, StealthBehaviorHandoff> advance,
			out StealthRepairTransition transition)
		{
			transition = null;
			if (result == null || owner != BehaviorId.Repair || result.Source == null ||
				result.Handoff.Owner != owner || result.Handoff.Epoch != epoch ||
				!ReferenceEquals(result.Mission, result.Source.Mission))
				return false;

			StealthBehaviorHandoff next;
			switch (result.Disposition)
			{
				case StealthRepairDisposition.Retain:
					if (result.LiveCause != StealthRepairLiveCause.Retreating &&
						result.LiveCause != StealthRepairLiveCause.Healing)
						return false;
					next = current;
					break;
				case StealthRepairDisposition.ResumeFight:
					if (result.LiveCause != StealthRepairLiveCause.NoSafeRepair ||
						!StealthRepairResumeContext.IsFightOwner(result.Resume.Owner))
						return false;
					next = advance(result.Resume.Owner);
					break;
				case StealthRepairDisposition.Start:
					if (result.LiveCause != StealthRepairLiveCause.RepairComplete ||
						result.Completion == null || result.Completion.Members.Count == 0)
						return false;
					next = advance(BehaviorId.Start);
					break;
				case StealthRepairDisposition.SquadConstruction:
					if (result.LiveCause != StealthRepairLiveCause.NoLiveMembers ||
						result.ActiveMemberActorIds.Count != 0)
						return false;
					next = advance(BehaviorId.SquadConstruction);
					break;
				default:
					return false;
			}

			transition = new StealthRepairTransition(next, result);
			return true;
		}
	}
}
