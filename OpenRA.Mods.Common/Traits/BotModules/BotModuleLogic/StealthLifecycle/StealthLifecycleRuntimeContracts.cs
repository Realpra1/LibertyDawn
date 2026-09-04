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
using System.Globalization;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>The only object allowed to execute one lifecycle owner.</summary>
	public interface IStealthLifecycleRuntimeOwner
	{
		BehaviorId Owner { get; }
		OwnershipEpoch Epoch { get; }
		object Execute();
	}

	public sealed class StealthLifecycleDamageObservation
	{
		public int Tick { get; }
		public uint SourceActorId { get; }
		public CPos SourceCurrentCell { get; }
		public int Amount { get; }
		public StealthRepairDamagedMember DamagedMember { get; }

		public StealthLifecycleDamageObservation(int tick, uint sourceActorId,
			CPos sourceCurrentCell, int amount, StealthRepairDamagedMember damagedMember)
		{
			if (tick < 0 || sourceActorId == 0 || amount <= 0)
				throw new ArgumentException("Invalid passive damage observation.");
			Tick = tick;
			SourceActorId = sourceActorId;
			SourceCurrentCell = sourceCurrentCell;
			Amount = amount;
			DamagedMember = damagedMember;
		}
	}

	public interface IStealthLifecycleRuntimeDamageOwner
	{
		bool TryCaptureDamage(StealthLifecycleDamageObservation observation, long eventId,
			out StealthLifecycleDamageYield yielded);
	}

	/// <summary>
	/// Constructs an owner from the exact typed handoff accepted by the router. Construction must
	/// be passive: live reads, safety calculations, orders, and transitions belong to Execute.
	/// </summary>
	public interface IStealthLifecycleRuntimeOwnerFactory
	{
		IStealthLifecycleRuntimeOwner Create(StealthLifecycleRuntimeEntry entry,
			IStealthLifecycleOwnershipGuard ownershipGuard,
			IStealthLifecycleRuntimeOrders orders);
	}

	/// <summary>Exact accepted handoff plus its immutable typed entry context.</summary>
	public sealed class StealthLifecycleRuntimeEntry
	{
		public StealthBehaviorHandoff Handoff { get; }
		public object Context { get; }
		public BehaviorId Owner => Handoff.Owner;
		public OwnershipEpoch Epoch => Handoff.Epoch;

		internal StealthLifecycleRuntimeEntry(StealthBehaviorHandoff handoff, object context = null)
		{
			Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			Context = context;
			ValidateContext(handoff.Owner, context);
		}

		static void ValidateContext(BehaviorId owner, object context)
		{
			var valid = owner == BehaviorId.Start ? context == null || context is StealthRepairTransition :
				owner == BehaviorId.SquadConstruction ? context is StealthStartResult ||
					context is StealthSquadConstructionRecoveryHandoff :
				owner == BehaviorId.TargetAcquisition ? context == null :
				owner == BehaviorId.TargetValueFilter ? context is StealthTargetValueFilterHandoff :
				owner == BehaviorId.TargetThreatFilter ? context is StealthTargetThreatFilterHandoff :
				owner == BehaviorId.TargetDistanceChoice ? context is StealthTargetDistanceChoiceHandoff :
				owner == BehaviorId.Approach ? context is StealthApproachHandoff ||
					context is StealthRepairFightResumeHandoff :
				owner == BehaviorId.UndefendedAttack ? context is StealthUndefendedAttackHandoff ||
					context is StealthRepairFightResumeHandoff :
				owner == BehaviorId.CrushEvaluation ? context is StealthCrushEvaluationHandoff ||
					context is StealthRepairFightResumeHandoff :
				owner == BehaviorId.Kite ? context is StealthKiteHandoff ||
					context is StealthRepairFightResumeHandoff :
				owner == BehaviorId.MassAttack ? context is StealthMassAttackHandoff ||
					context is StealthRepairFightResumeHandoff :
				owner == BehaviorId.Engagement ? context is StealthRepairFightResumeHandoff :
				owner == BehaviorId.RecalculateFlee ? context is StealthRecalculateFleeHandoff :
				owner == BehaviorId.Repair ? context is StealthRepairHandoff : false;
			if (!valid)
				throw new ArgumentException("The runtime entry does not match its typed owner handoff.", nameof(context));
		}
	}

	/// <summary>
	/// An active fight owner may yield this immutable damage fact. Events cannot construct or submit
	/// it directly to the router, so Damage never becomes a cross-trigger interruption.
	/// </summary>
	public sealed class StealthLifecycleDamageYield
	{
		readonly ReadOnlyCollection<StealthRepairDamagedMember> damagedMembers;
		internal StealthBehaviorHandoff Handoff { get; }
		public long DamageEventId { get; }
		public int DamageTick { get; }
		public uint DamageSourceActorId { get; }
		public int DamageAmount { get; }
		public IReadOnlyList<StealthRepairDamagedMember> DamagedMembers => damagedMembers;
		public StealthRepairResumeContext Resume { get; }

		internal StealthLifecycleDamageYield(StealthBehaviorHandoff handoff, long damageEventId,
			int damageTick, uint damageSourceActorId, int damageAmount,
			IEnumerable<StealthRepairDamagedMember> damagedMembers,
			StealthRepairResumeContext resume)
		{
			if (handoff == null || !StealthRepairResumeContext.IsFightOwner(handoff.Owner) ||
				damageEventId <= 0 || damageTick < 0 || damageSourceActorId == 0 || damageAmount <= 0 ||
				resume == null || resume.Owner != handoff.Owner || resume.Epoch != handoff.Epoch)
				throw new ArgumentException("Damage must be yielded by the exact active fight owner.");
			var members = damagedMembers?.OrderBy(member => member.ActorId).ToArray();
			if (members == null || members.Length == 0 ||
				members.Select(member => member.ActorId).Distinct().Count() != members.Length ||
				members.Any(member => !resume.MemberActorIds.Contains(member.ActorId)))
				throw new ArgumentException("Damage members must be a canonical active-fight subset.",
					nameof(damagedMembers));

			Handoff = handoff;
			DamageEventId = damageEventId;
			DamageTick = damageTick;
			DamageSourceActorId = damageSourceActorId;
			DamageAmount = damageAmount;
			this.damagedMembers = Array.AsReadOnly(members);
			Resume = resume;
		}
	}

	public enum StealthLifecycleRuntimeOrderKind
	{
		Move,
		Attack,
		Crush,
		Repair
	}

	/// <summary>Owner/epoch/action-bound command presented to the one external runtime sink.</summary>
	public sealed class StealthLifecycleRuntimeOrder
	{
		readonly ReadOnlyCollection<uint> actorIds;
		readonly ReadOnlyCollection<CPos> route;
		public BehaviorId Owner { get; }
		public OwnershipEpoch Epoch { get; }
		public StealthLifecycleRuntimeOrderKind Kind { get; }
		public string Action { get; }
		public IReadOnlyList<uint> ActorIds => actorIds;
		public uint? TargetActorId { get; }
		public CPos? TargetCell { get; }
		public IReadOnlyList<CPos> Route => route;
		public string Fingerprint { get; }

		public StealthLifecycleRuntimeOrder(BehaviorId owner, OwnershipEpoch epoch,
			StealthLifecycleRuntimeOrderKind kind, string action, IEnumerable<uint> actorIds,
			uint? targetActorId = null, CPos? targetCell = null, IEnumerable<CPos> route = null)
		{
			if (!Enum.IsDefined(typeof(BehaviorId), owner) ||
				!Enum.IsDefined(typeof(StealthLifecycleRuntimeOrderKind), kind) ||
				string.IsNullOrWhiteSpace(action))
				throw new ArgumentException("Runtime orders require a canonical owner action.");
			var actors = actorIds?.OrderBy(id => id).ToArray();
			if (actors == null || actors.Length == 0 || actors.Any(id => id == 0) ||
				actors.Distinct().Count() != actors.Length)
				throw new ArgumentException("Runtime order actors must be unique and nonzero.", nameof(actorIds));
			Owner = owner;
			Epoch = epoch;
			Kind = kind;
			Action = action;
			this.actorIds = Array.AsReadOnly(actors);
			TargetActorId = targetActorId;
			TargetCell = targetCell;
			this.route = Array.AsReadOnly((route ?? Array.Empty<CPos>()).ToArray());
			Fingerprint = CanonicalFingerprint();
		}

		string CanonicalFingerprint()
		{
			return string.Join("|", Owner, Epoch.Value.ToString(CultureInfo.InvariantCulture), Kind,
				Action, string.Join(",", actorIds), TargetActorId?.ToString(CultureInfo.InvariantCulture) ?? "-",
				TargetCell?.Bits.ToString(CultureInfo.InvariantCulture) ?? "-",
				string.Join(",", route.Select(cell => cell.Bits.ToString(CultureInfo.InvariantCulture))));
		}
	}

	public interface IStealthLifecycleRuntimeOrderTarget
	{
		Action Prepare(StealthLifecycleRuntimeOrder order);
	}

	public interface IStealthLifecycleRuntimeOrders
	{
		void Issue(StealthLifecycleRuntimeOrder order);
		void Reset(BehaviorId owner, OwnershipEpoch epoch);
	}
}
