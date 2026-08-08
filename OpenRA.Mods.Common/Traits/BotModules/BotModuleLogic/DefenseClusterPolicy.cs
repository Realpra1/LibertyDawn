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

namespace OpenRA.Mods.Common.Traits
{
	[Flags]
	public enum DefenseClusterRole
	{
		None = 0,
		AntiInfantry = 1,
		AntiGround = 2,
		AntiAir = 4
	}

	/// <summary>
	/// World-independent state transitions for one active strongpoint and one pending attacked tower.
	/// Keeping this separate makes the damage-spam and save/load invariants explicit and testable.
	/// </summary>
	public sealed class DefenseClusterPolicyState
	{
		public uint AnchorActorId { get; private set; }
		public int AnchorAttackTick { get; private set; }
		public int AnchorLeaseUntilTick { get; private set; }
		public uint PendingActorId { get; private set; }
		public int PendingAttackTick { get; private set; }
		public int PlacementFailures { get; private set; }

		public bool HasAnchor => AnchorActorId != 0;
		public bool HasPending => PendingActorId != 0;

		public void ObserveActiveClusterPressure(int tick)
		{
			if (HasAnchor)
				AnchorAttackTick = Math.Max(AnchorAttackTick, tick);
		}

		public bool ObserveTowerAttack(uint actorId, int tick, int leaseTicks, bool minimumClusterComplete)
		{
			if (actorId == 0)
				return false;

			if (!HasAnchor)
			{
				Activate(actorId, tick, leaseTicks);
				return true;
			}

			if (AnchorActorId == actorId)
			{
				AnchorAttackTick = Math.Max(AnchorAttackTick, tick);
				return false;
			}

			// One slot only. A newer hit replaces the pending candidate; ties keep the
			// lower ActorID so arrival order cannot make the result nondeterministic.
			if (!HasPending || tick > PendingAttackTick ||
				(tick == PendingAttackTick && actorId < PendingActorId))
			{
				PendingActorId = actorId;
				PendingAttackTick = tick;
			}

			return TryPromotePending(tick, leaseTicks, minimumClusterComplete);
		}

		public bool TryPromotePending(int tick, int leaseTicks, bool minimumClusterComplete)
		{
			if (!HasPending || (!minimumClusterComplete && tick < AnchorLeaseUntilTick) ||
				PendingAttackTick <= AnchorAttackTick)
				return false;

			Activate(PendingActorId, PendingAttackTick, leaseTicks);
			return true;
		}

		public bool InvalidateAnchor(int tick, int leaseTicks, Func<uint, bool> isValidPending)
		{
			AnchorActorId = 0;
			AnchorAttackTick = 0;
			AnchorLeaseUntilTick = 0;
			PlacementFailures = 0;

			if (HasPending && isValidPending(PendingActorId))
			{
				Activate(PendingActorId, PendingAttackTick, leaseTicks);
				return true;
			}

			ClearPending();
			return false;
		}

		public bool RecordPlacementFailure(int maximumFailures)
		{
			PlacementFailures++;
			return PlacementFailures >= Math.Max(1, maximumFailures);
		}

		public void RecordPlacementProgress()
		{
			PlacementFailures = 0;
		}

		public void Restore(uint anchorActorId, int anchorAttackTick, int anchorLeaseUntilTick,
			uint pendingActorId, int pendingAttackTick, int placementFailures)
		{
			AnchorActorId = anchorActorId;
			AnchorAttackTick = Math.Max(0, anchorAttackTick);
			AnchorLeaseUntilTick = Math.Max(0, anchorLeaseUntilTick);
			PendingActorId = pendingActorId == anchorActorId ? 0 : pendingActorId;
			PendingAttackTick = PendingActorId == 0 ? 0 : Math.Max(0, pendingAttackTick);
			PlacementFailures = Math.Max(0, placementFailures);
		}

		void Activate(uint actorId, int attackTick, int leaseTicks)
		{
			AnchorActorId = actorId;
			AnchorAttackTick = Math.Max(0, attackTick);
			AnchorLeaseUntilTick = Math.Max(0, attackTick) + Math.Max(1, leaseTicks);
			PlacementFailures = 0;
			ClearPending();
		}

		void ClearPending()
		{
			PendingActorId = 0;
			PendingAttackTick = 0;
		}
	}

	/// <summary>
	/// The one safer-side repair site protected for the active cluster. Its lifetime is tied to
	/// the anchor rather than current placement legality so a passing unit cannot let a tower
	/// consume a site that was already proven useful.
	/// </summary>
	public sealed class DefenseClusterRepairSiteState
	{
		public uint AnchorActorId { get; private set; }
		public string FacilityType { get; private set; }
		public CPos Site { get; private set; }
		public CPos ApproachCell { get; private set; }
		public CPos EnemyLocation { get; private set; }
		public bool HasSite => AnchorActorId != 0 && !string.IsNullOrEmpty(FacilityType);

		public bool Matches(uint anchorActorId, string facilityType)
		{
			return HasSite && AnchorActorId == anchorActorId &&
				(string.IsNullOrEmpty(facilityType) || FacilityType == facilityType);
		}

		public bool Protect(uint anchorActorId, string facilityType, CPos site, CPos approachCell,
			CPos enemyLocation)
		{
			if (anchorActorId == 0 || string.IsNullOrEmpty(facilityType))
				return false;

			var changed = AnchorActorId != anchorActorId || FacilityType != facilityType ||
				Site != site || ApproachCell != approachCell || EnemyLocation != enemyLocation;
			AnchorActorId = anchorActorId;
			FacilityType = facilityType;
			Site = site;
			ApproachCell = approachCell;
			EnemyLocation = enemyLocation;
			return changed;
		}

		public bool Clear()
		{
			var changed = HasSite;
			AnchorActorId = 0;
			FacilityType = null;
			Site = default;
			ApproachCell = default;
			EnemyLocation = default;
			return changed;
		}

		public void Restore(uint anchorActorId, string facilityType, CPos site, CPos approachCell,
			CPos enemyLocation)
		{
			if (anchorActorId == 0 || string.IsNullOrEmpty(facilityType))
			{
				Clear();
				return;
			}

			AnchorActorId = anchorActorId;
			FacilityType = facilityType;
			Site = site;
			ApproachCell = approachCell;
			EnemyLocation = enemyLocation;
		}
	}

	public static class DefenseClusterPolicy
	{
		public static bool ReservationIsLost(bool producerValid, bool itemQueued, int age, int timeout)
		{
			return !itemQueued && (!producerValid || age >= Math.Max(1, timeout));
		}

		public static bool CanUseRepairProducer(bool recoveryPending, uint candidateActorId,
			uint stableActorId)
		{
			return !recoveryPending || stableActorId == 0 || candidateActorId == stableActorId;
		}

		public static bool CanQueueRepairRecovery(bool recoveryPending, bool handoffUsed,
			bool priorityRecoveryActive, int queuedItems)
		{
			return recoveryPending && !handoffUsed && !priorityRecoveryActive && queuedItems == 1;
		}

		public static bool IsWithinCluster(CPos anchor, CPos candidate, int radius)
		{
			return (candidate - anchor).LengthSquared <= radius * radius;
		}

		public static bool IsSellableWallPurpose(bool protectedEnclosure,
			bool legacyClusterWall, bool plannedClusterWall)
		{
			return !protectedEnclosure && (legacyClusterWall || plannedClusterWall);
		}

		public static DefenseClusterRole CoveredRoles(IEnumerable<DefenseClusterRole> liveTowerRoles)
		{
			var covered = DefenseClusterRole.None;
			foreach (var roles in liveTowerRoles)
				covered |= roles;

			return covered;
		}

		public static bool IsComplete(int distinctLiveTowers, int minimumTowers,
			DefenseClusterRole requiredRoles, IEnumerable<DefenseClusterRole> operationalTowerRoles,
			bool hasLocalRepairFacility)
		{
			var covered = CoveredRoles(operationalTowerRoles);
			return distinctLiveTowers >= Math.Max(1, minimumTowers) &&
				(covered & requiredRoles) == requiredRoles && hasLocalRepairFacility;
		}

		public static string ChooseMissingTower(IEnumerable<string> orderedTypes,
			Func<string, DefenseClusterRole> rolesForType, DefenseClusterRole requiredRoles,
			DefenseClusterRole coveredRoles, int liveTowerCount, int committedTowerCount, int minimumTowers,
			Func<string, bool> canCommit)
		{
			var missingRoles = requiredRoles & ~coveredRoles;
			var needAnotherActor = liveTowerCount + committedTowerCount < Math.Max(1, minimumTowers);
			if (missingRoles == DefenseClusterRole.None && !needAnotherActor)
				return null;

			var candidates = orderedTypes.Where(canCommit)
				.Select((type, index) => new { Type = type, Index = index, Roles = rolesForType(type) })
				.Where(c => needAnotherActor || (c.Roles & missingRoles) != 0)
				.OrderByDescending(c => CountBits(c.Roles & missingRoles))
				.ThenBy(c => c.Index);

			return candidates.Select(c => c.Type).FirstOrDefault();
		}

		static int CountBits(DefenseClusterRole roles)
		{
			var value = (int)roles;
			var count = 0;
			while (value != 0)
			{
				count += value & 1;
				value >>= 1;
			}

			return count;
		}
	}
}
