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
using System.Collections.ObjectModel;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Exact immutable combat context that Damage records and Repair may only return.</summary>
	public sealed class StealthRepairResumeContext
	{
		readonly ReadOnlyCollection<uint> memberIds;
		readonly ReadOnlyCollection<uint> enemyIds;
		public BehaviorId Owner { get; }
		public OwnershipEpoch Epoch { get; }
		public StealthApproachMission Mission { get; }
		public IReadOnlyList<uint> MemberActorIds => memberIds;
		public IReadOnlyList<uint> EnemyActorIds => enemyIds;
		public uint? SelectedTargetActorId { get; }
		public CPos? SelectedTargetCurrentCell { get; }
		public string ContextFingerprint { get; }
		public StealthMassAttackEntryEvidence MassAttackEntryEvidence { get; }

		internal StealthRepairResumeContext(BehaviorId owner, OwnershipEpoch epoch,
			StealthApproachMission mission, IEnumerable<uint> memberActorIds,
			IEnumerable<uint> enemyActorIds, uint? selectedTargetActorId,
			CPos? selectedTargetCurrentCell, string contextFingerprint)
			: this(owner, epoch, mission, memberActorIds, enemyActorIds, selectedTargetActorId,
				selectedTargetCurrentCell, contextFingerprint, null) { }

		internal StealthRepairResumeContext(BehaviorId owner, OwnershipEpoch epoch,
			StealthApproachMission mission, IEnumerable<uint> memberActorIds,
			IEnumerable<uint> enemyActorIds, uint? selectedTargetActorId,
			CPos? selectedTargetCurrentCell, string contextFingerprint,
			StealthMassAttackEntryEvidence massAttackEntryEvidence)
		{
			if (!IsFightOwner(owner) || mission == null ||
				selectedTargetActorId.HasValue != selectedTargetCurrentCell.HasValue ||
				string.IsNullOrEmpty(contextFingerprint) ||
				(owner == BehaviorId.MassAttack) != (massAttackEntryEvidence != null))
				throw new ArgumentException("Repair requires one exact active-fight context.");
			Owner = owner;
			Epoch = epoch;
			Mission = mission;
			memberIds = Canonical(memberActorIds, false, nameof(memberActorIds));
			enemyIds = Canonical(enemyActorIds, true, nameof(enemyActorIds));
			if (selectedTargetActorId.HasValue && !enemyIds.Contains(selectedTargetActorId.Value))
				throw new ArgumentException("The resume target must be one of the recorded enemies.");
			SelectedTargetActorId = selectedTargetActorId;
			SelectedTargetCurrentCell = selectedTargetCurrentCell;
			ContextFingerprint = contextFingerprint;
			MassAttackEntryEvidence = massAttackEntryEvidence;
		}

		internal static bool IsFightOwner(BehaviorId owner)
		{
			return owner == BehaviorId.Engagement || owner == BehaviorId.UndefendedAttack ||
				owner == BehaviorId.CrushEvaluation || owner == BehaviorId.Kite ||
				owner == BehaviorId.MassAttack;
		}

		internal static ReadOnlyCollection<uint> Canonical(IEnumerable<uint> ids,
			bool allowEmpty, string parameter)
		{
			var copy = ids?.OrderBy(id => id).ToArray();
			if (copy == null || (!allowEmpty && copy.Length == 0) || copy.Any(id => id == 0) ||
				copy.Distinct().Count() != copy.Length)
				throw new ArgumentException("Actor identities must be unique and nonzero.", parameter);
			return Array.AsReadOnly(copy);
		}
	}

	public readonly struct StealthRepairDamagedMember
	{
		public uint ActorId { get; }
		public int HitPoints { get; }
		public int MaximumHitPoints { get; }
		public StealthRepairDamagedMember(uint actorId, int hitPoints, int maximumHitPoints)
		{
			if (actorId == 0 || maximumHitPoints <= 0 || hitPoints <= 0 || hitPoints > maximumHitPoints)
				throw new ArgumentException("Repair member health evidence is invalid.");
			ActorId = actorId;
			HitPoints = hitPoints;
			MaximumHitPoints = maximumHitPoints;
		}
	}

	/// <summary>Typed Damage output. This is a boundary only; it does not implement Damage.</summary>
	public sealed class StealthDamageRepairRequest
	{
		readonly ReadOnlyCollection<StealthRepairDamagedMember> damagedMembers;
		internal StealthBehaviorHandoff Handoff { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public long DamageEventId { get; }
		public int DamageTick { get; }
		public uint DamageSourceActorId { get; }
		public int DamageAmount { get; }
		public IReadOnlyList<StealthRepairDamagedMember> DamagedMembers => damagedMembers;
		public StealthRepairResumeContext Resume { get; }

		internal StealthDamageRepairRequest(StealthBehaviorHandoff handoff, long damageEventId,
			int damageTick, uint damageSourceActorId, int damageAmount,
			IEnumerable<StealthRepairDamagedMember> damagedMembers,
			StealthRepairResumeContext resume)
		{
			if (handoff == null || handoff.Owner != BehaviorId.Damage || damageEventId <= 0 ||
				damageTick < 0 || damageSourceActorId == 0 || damageAmount <= 0 || resume == null ||
				resume.Epoch.Value == long.MaxValue || handoff.Epoch.Value != resume.Epoch.Value + 1)
				throw new ArgumentException("Repair requires one canonical Damage output.");
			var copy = damagedMembers?.OrderBy(member => member.ActorId).ToArray();
			if (copy == null || copy.Length == 0 ||
				copy.Select(member => member.ActorId).Distinct().Count() != copy.Length ||
				copy.Any(member => member.HitPoints >= member.MaximumHitPoints ||
					!resume.MemberActorIds.Contains(member.ActorId)))
				throw new ArgumentException("Damaged members must be unique resume members.", nameof(damagedMembers));
			Handoff = handoff;
			DamageEventId = damageEventId;
			DamageTick = damageTick;
			DamageSourceActorId = damageSourceActorId;
			DamageAmount = damageAmount;
			this.damagedMembers = Array.AsReadOnly(copy);
			Resume = resume;
		}
	}

	/// <summary>Typed immutable input into the disabled Repair owner.</summary>
	public sealed class StealthRepairHandoff
	{
		internal StealthBehaviorHandoff Handoff { get; }
		internal StealthDamageRepairRequest Source { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;
		public StealthApproachMission Mission => Source.Resume.Mission;
		public StealthRepairResumeContext Resume => Source.Resume;
		public long DamageEventId => Source.DamageEventId;
		public int DamageTick => Source.DamageTick;
		public uint DamageSourceActorId => Source.DamageSourceActorId;
		public int DamageAmount => Source.DamageAmount;
		public IReadOnlyList<StealthRepairDamagedMember> DamagedMembers => Source.DamagedMembers;

		internal StealthRepairHandoff(StealthBehaviorHandoff handoff,
			StealthDamageRepairRequest source)
		{
			if (handoff == null || handoff.Owner != BehaviorId.Repair || source == null ||
				source.Handoff.Epoch.Value == long.MaxValue ||
				handoff.Epoch.Value != source.Handoff.Epoch.Value + 1)
				throw new ArgumentException("Repair requires the immediately preceding Damage epoch.");
			Handoff = handoff;
			Source = source;
		}
	}

	public sealed class StealthRepairMemberSnapshot
	{
		public uint ActorId { get; }
		public CPos CurrentCell { get; }
		public int CurrentWeaponRangeCells { get; }
		public int HitPoints { get; }
		public int MaximumHitPoints { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }
		public bool IsValid => IsInWorld && !IsDead && HitPoints > 0 && MaximumHitPoints > 0;
		public bool IsRepaired => IsValid && HitPoints >= MaximumHitPoints;

		public StealthRepairMemberSnapshot(uint actorId, CPos currentCell,
			int currentWeaponRangeCells, int hitPoints, int maximumHitPoints,
			bool isInWorld = true, bool isDead = false)
		{
			if (actorId == 0 || currentWeaponRangeCells < 0 || hitPoints < 0 || maximumHitPoints < 0)
				throw new ArgumentException("Invalid live Repair member.");
			ActorId = actorId;
			CurrentCell = currentCell;
			CurrentWeaponRangeCells = currentWeaponRangeCells;
			HitPoints = hitPoints;
			MaximumHitPoints = maximumHitPoints;
			IsInWorld = isInWorld;
			IsDead = isDead;
		}
	}

	public sealed class StealthRepairOptionSnapshot
	{
		public uint ActorId { get; }
		public CPos CurrentCell { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }
		public bool IsAvailable { get; }
		public bool IsValid => IsInWorld && !IsDead && IsAvailable;

		public StealthRepairOptionSnapshot(uint actorId, CPos currentCell,
			bool isAvailable = true, bool isInWorld = true, bool isDead = false)
		{
			if (actorId == 0)
				throw new ArgumentOutOfRangeException(nameof(actorId));
			ActorId = actorId;
			CurrentCell = currentCell;
			IsAvailable = isAvailable;
			IsInWorld = isInWorld;
			IsDead = isDead;
		}
	}

	public sealed class StealthRepairEnemySnapshot
	{
		public uint ActorId { get; }
		public string ActorType { get; }
		public CPos CurrentCell { get; }
		public int HitPoints { get; }
		public int MaximumHitPoints { get; }
		public int CurrentWeaponRangeCells { get; }
		public bool IsDetector { get; }
		public bool IsInLocalArea { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }
		public bool IsTargetable { get; }
		public bool IsValid => IsInWorld && !IsDead && IsTargetable &&
			(MaximumHitPoints <= 0 || HitPoints > 0);

		public StealthRepairEnemySnapshot(uint actorId, string actorType, CPos currentCell,
			int hitPoints, int maximumHitPoints, int currentWeaponRangeCells, bool isDetector,
			bool isInLocalArea = true, bool isInWorld = true, bool isDead = false,
			bool isTargetable = true)
		{
			if (actorId == 0 || string.IsNullOrWhiteSpace(actorType) || hitPoints < 0 ||
				maximumHitPoints < 0 || currentWeaponRangeCells < 0)
				throw new ArgumentException("Invalid live Repair enemy.");
			ActorId = actorId;
			ActorType = actorType.ToLowerInvariant();
			CurrentCell = currentCell;
			HitPoints = hitPoints;
			MaximumHitPoints = maximumHitPoints;
			CurrentWeaponRangeCells = currentWeaponRangeCells;
			IsDetector = isDetector;
			IsInLocalArea = isInLocalArea;
			IsInWorld = isInWorld;
			IsDead = isDead;
			IsTargetable = isTargetable;
		}
	}

	public sealed class StealthRepairStaticActorSnapshot
	{
		public uint ActorId { get; }
		public string ActorType { get; }
		public CPos CurrentCell { get; }
		public bool IsInWorld { get; }
		public bool IsDead { get; }
		public bool IsPassable { get; }
		public StealthRepairStaticActorSnapshot(uint actorId, string actorType, CPos currentCell,
			bool isPassable, bool isInWorld = true, bool isDead = false)
		{
			if (actorId == 0 || string.IsNullOrWhiteSpace(actorType))
				throw new ArgumentException("Invalid live Repair static actor.");
			ActorId = actorId;
			ActorType = actorType.ToLowerInvariant();
			CurrentCell = currentCell;
			IsPassable = isPassable;
			IsInWorld = isInWorld;
			IsDead = isDead;
		}
	}

	public sealed class StealthRepairRouteSnapshot
	{
		readonly ReadOnlyCollection<CPos> cells;
		public uint StableIdentity { get; }
		public uint RepairOptionActorId { get; }
		public IReadOnlyList<CPos> Cells => cells;
		public bool IsPassable { get; }
		public bool RequiresStrategicRouting { get; }
		public bool HasDetectorCoverage { get; }

		public StealthRepairRouteSnapshot(uint stableIdentity, uint repairOptionActorId,
			IEnumerable<CPos> cells, bool isPassable, bool requiresStrategicRouting = false,
			bool hasDetectorCoverage = false)
		{
			var copy = cells?.ToArray();
			if (stableIdentity == 0 || repairOptionActorId == 0 || copy == null || copy.Length == 0)
				throw new ArgumentException("A Repair route requires stable identities and cells.");
			StableIdentity = stableIdentity;
			RepairOptionActorId = repairOptionActorId;
			this.cells = Array.AsReadOnly(copy);
			IsPassable = isPassable;
			RequiresStrategicRouting = requiresStrategicRouting;
			HasDetectorCoverage = hasDetectorCoverage;
		}
	}

	public sealed class StealthRepairLiveSnapshot
	{
		readonly ReadOnlyCollection<StealthRepairMemberSnapshot> members;
		readonly ReadOnlyCollection<StealthRepairOptionSnapshot> options;
		readonly ReadOnlyCollection<StealthRepairEnemySnapshot> enemies;
		readonly ReadOnlyCollection<StealthRepairStaticActorSnapshot> staticActors;
		readonly ReadOnlyCollection<StealthRepairRouteSnapshot> routes;
		public int Tick { get; }
		public long DamageEventId { get; }
		public int DamageTick { get; }
		public uint DamageSourceActorId { get; }
		public int DamageAmount { get; }
		public string ResumeFingerprint { get; }
		public IReadOnlyList<StealthRepairMemberSnapshot> Members => members;
		public IReadOnlyList<StealthRepairOptionSnapshot> RepairOptions => options;
		public IReadOnlyList<StealthRepairEnemySnapshot> Enemies => enemies;
		public IReadOnlyList<StealthRepairStaticActorSnapshot> StaticActors => staticActors;
		public IReadOnlyList<StealthRepairRouteSnapshot> Routes => routes;
		public bool FormationCloaked { get; }
		public bool HasActivityObservation { get; }
		public long ActivityRevision { get; }
		public int RouteProgress { get; }
		public StealthRepairOrderToken ActiveOrderToken { get; }
		public StealthRepairOrderToken CompletedOrderToken { get; }

		public StealthRepairLiveSnapshot(int tick, long damageEventId, int damageTick,
			uint damageSourceActorId, int damageAmount, string resumeFingerprint,
			IEnumerable<StealthRepairMemberSnapshot> members,
			IEnumerable<StealthRepairOptionSnapshot> repairOptions,
			IEnumerable<StealthRepairEnemySnapshot> enemies,
			IEnumerable<StealthRepairStaticActorSnapshot> staticActors,
			IEnumerable<StealthRepairRouteSnapshot> routes, bool formationCloaked,
			bool hasActivityObservation = false, long activityRevision = 0,
			int routeProgress = 0, StealthRepairOrderToken activeOrderToken = null,
			StealthRepairOrderToken completedOrderToken = null)
		{
			if (tick < 0 || damageEventId <= 0 || damageTick < 0 || damageSourceActorId == 0 ||
				damageAmount <= 0 || string.IsNullOrEmpty(resumeFingerprint) || members == null ||
				repairOptions == null || enemies == null || staticActors == null || routes == null ||
				activityRevision < 0 || routeProgress < 0 ||
				(!hasActivityObservation && (activityRevision != 0 || routeProgress != 0 ||
					activeOrderToken != null || completedOrderToken != null)))
				throw new ArgumentException("Invalid live Repair snapshot.");
			this.members = Copy(members, member => member?.ActorId, "members");
			options = Copy(repairOptions, option => option?.ActorId, "repair options");
			this.enemies = Copy(enemies, enemy => enemy?.ActorId, "enemies");
			this.staticActors = Copy(staticActors, actor => actor?.ActorId, "static actors");
			this.routes = Copy(routes, route => route?.StableIdentity, "routes");
			if (this.routes.Any(route => !options.Any(option => option.ActorId == route.RepairOptionActorId)) ||
				routeProgress > this.routes.Select(route => route.Cells.Count).DefaultIfEmpty().Max() ||
				(activeOrderToken != null && activeOrderToken.ActivityRevision != activityRevision) ||
				(completedOrderToken != null && completedOrderToken.ActivityRevision > activityRevision))
				throw new ArgumentException("Live Repair routes or activity are inconsistent.");
			Tick = tick;
			DamageEventId = damageEventId;
			DamageTick = damageTick;
			DamageSourceActorId = damageSourceActorId;
			DamageAmount = damageAmount;
			ResumeFingerprint = resumeFingerprint;
			FormationCloaked = formationCloaked;
			HasActivityObservation = hasActivityObservation;
			ActivityRevision = activityRevision;
			RouteProgress = routeProgress;
			ActiveOrderToken = activeOrderToken;
			CompletedOrderToken = completedOrderToken;
		}

		static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values, Func<T, uint?> identity,
			string name) where T : class
		{
			var copy = values.OrderBy(value => identity(value)).ToArray();
			if (copy.Any(value => value == null) || copy.Select(identity).Distinct().Count() != copy.Length)
				throw new ArgumentException("Live Repair " + name + " must be unique and non-null.");
			return Array.AsReadOnly(copy);
		}
	}

	public interface IStealthRepairLiveWorld
	{
		StealthRepairLiveSnapshot Read(StealthApproachMission mission);
	}

	public sealed class StealthRepairStrategicCacheSnapshot
	{
		readonly ReadOnlyCollection<CPos> waypoints;
		public long Revision { get; }
		public IReadOnlyList<CPos> Waypoints => waypoints;
		public StealthRepairStrategicCacheSnapshot(long revision, IEnumerable<CPos> waypoints)
		{
			if (revision < 0 || waypoints == null)
				throw new ArgumentException("Invalid passive Repair route cache.");
			Revision = revision;
			this.waypoints = Array.AsReadOnly(waypoints.ToArray());
		}
	}

	public interface IStealthRepairStrategicCache
	{
		StealthRepairStrategicCacheSnapshot ReadLongRoute(StealthApproachMission mission,
			uint repairOptionActorId, IReadOnlyList<CPos> liveRoute);
	}
}
