#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is made available to you under the terms of
 * the GNU General Public License as published by the Free Software Foundation.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Lets an active mission owner turn one passive engine fact into Damage.</summary>
	sealed class StealthSquadLifecycleDamageAdapter
	{
		readonly StealthLifecycleRuntimeEntry entry;
		readonly StealthSquadLifecycleCombatLiveAdapter live;

		public StealthSquadLifecycleDamageAdapter(StealthLifecycleRuntimeEntry entry,
			StealthSquadLifecycleCombatLiveAdapter live)
		{
			this.entry = entry ?? throw new ArgumentNullException(nameof(entry));
			this.live = live ?? throw new ArgumentNullException(nameof(live));
		}

		public StealthLifecycleDamageYield Capture(object lastResult,
			StealthLifecycleDamageObservation observation, long eventId)
		{
			if (observation == null ||
				observation.DamagedMember.HitPoints >= observation.DamagedMember.MaximumHitPoints ||
				!StealthRepairResumeContext.IsFightOwner(entry.Owner))
				return null;
			var members = Members(lastResult);
			if (!members.Contains(observation.DamagedMember.ActorId))
				return null;
			var mission = Mission();
			var enemies = Enemies(lastResult).Append(observation.SourceActorId)
				.Distinct().OrderBy(id => id).ToArray();
			var target = Target(lastResult);
			if (target.Id.HasValue && !enemies.Contains(target.Id.Value))
				enemies = enemies.Append(target.Id.Value).OrderBy(id => id).ToArray();
			var fingerprint = string.Join("|", entry.Owner, entry.Epoch.Value,
				mission.StrategicCell.Bits, string.Join(",", members), string.Join(",", enemies),
				target.Id?.ToString(CultureInfo.InvariantCulture) ?? "-");
			var resume = new StealthRepairResumeContext(entry.Owner, entry.Epoch, mission,
				members, enemies, target.Id, target.Cell, fingerprint, MassEvidence());
			return new StealthLifecycleDamageYield(entry.Handoff, eventId, observation.Tick,
				observation.SourceActorId, observation.Amount,
				new[] { observation.DamagedMember }, resume);
		}

		uint[] Members(object result)
		{
			IEnumerable<uint> ids = result is StealthApproachResult approach ?
				approach.ActiveMemberActorIds :
				result is StealthUndefendedAttackResult undefended ?
				undefended.AttackMemberActorIds :
				result is StealthCrushResult crush ? crush.ActiveMemberActorIds :
				result is StealthKiteResult kite ? kite.ActiveMemberActorIds :
				result is StealthMassAttackResult mass ? mass.ActiveMemberActorIds : null;
			return (ids ?? live.Members().Select(actor => actor.ActorID))
				.Distinct().OrderBy(id => id).ToArray();
		}

		IEnumerable<uint> Enemies(object result)
		{
			if (result is StealthApproachResult approach) return approach.LiveDefenderActorIds;
			if (result is StealthUndefendedAttackResult undefended) return undefended.LiveDefenderActorIds;
			if (result is StealthCrushResult crush) return crush.LiveDefenderActorIds;
			if (result is StealthKiteResult kite) return kite.LiveDefenderActorIds;
			if (result is StealthMassAttackResult mass) return mass.LiveDefenderActorIds;
			if (entry.Context is StealthCrushEvaluationHandoff crushEntry) return crushEntry.LiveDefenderActorIds;
			if (entry.Context is StealthKiteHandoff kiteEntry) return kiteEntry.LiveDefenderActorIds;
			if (entry.Context is StealthMassAttackHandoff massEntry) return massEntry.Evidence.EnemyActorIds;
			if (entry.Context is StealthRepairFightResumeHandoff resumed) return resumed.Context.EnemyActorIds;
			return Array.Empty<uint>();
		}

		(uint? Id, CPos? Cell) Target(object result)
		{
			if (result is StealthCrushResult crush)
				return (crush.SelectedTargetActorId, crush.SelectedTargetCurrentCell);
			if (result is StealthKiteResult kite)
				return (kite.SelectedTargetActorId, kite.SelectedTargetCurrentCell);
			if (result is StealthMassAttackResult mass)
				return (mass.SelectedTargetActorId, mass.SelectedTargetCurrentCell);
			var evidence = MassEvidence();
			if (evidence != null)
				return (evidence.SelectedTargetActorId, evidence.SelectedTargetCurrentCell);
			if (entry.Context is StealthRepairFightResumeHandoff resumed)
				return (resumed.Context.SelectedTargetActorId, resumed.Context.SelectedTargetCurrentCell);
			return (null, null);
		}

		StealthApproachMission Mission()
		{
			if (entry.Context is StealthApproachHandoff approach) return approach.Missions[0];
			if (entry.Context is StealthUndefendedAttackHandoff undefended) return undefended.Mission;
			if (entry.Context is StealthCrushEvaluationHandoff crush) return crush.Mission;
			if (entry.Context is StealthKiteHandoff kite) return kite.Mission;
			if (entry.Context is StealthMassAttackHandoff mass) return mass.Mission;
			return ((StealthRepairFightResumeHandoff)entry.Context).Context.Mission;
		}

		StealthMassAttackEntryEvidence MassEvidence()
		{
			if (entry.Owner != BehaviorId.MassAttack)
				return null;
			if (entry.Context is StealthMassAttackHandoff mass)
				return mass.Evidence;
			return ((StealthRepairFightResumeHandoff)entry.Context).Context.MassAttackEntryEvidence;
		}
	}
}
