#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Diagnostic-only whole-control-group STNK efficiency scorer for human play and replay playback.")]
	public sealed class StealthEfficiencyControlWatchdogInfo : TraitInfo
	{
		public override object Create(ActorInitializer init)
		{
			return new StealthEfficiencyControlWatchdog(init.Self);
		}
	}

	public sealed class StealthEfficiencyControlWatchdog : ITick, INotifyCreated,
		INotifyDamage, INotifyAppliedDamage, INotifyActorDisposing
	{
		readonly Actor playerActor;
		readonly StealthEfficiencyWindow window;
		bool enabled;
		bool terminalReported;

		public StealthEfficiencyControlWatchdog(Actor self)
		{
			playerActor = self;
			window = new StealthEfficiencyWindow(self.World.WorldTick);
		}

		void INotifyCreated.Created(Actor self)
		{
			enabled = Game.Settings.Debug.BotDebug && !self.Owner.IsBot && self.Owner.Playable &&
				!self.Owner.NonCombatant;
			if (enabled)
				self.World.GameEnding += EmitTerminalSummary;
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (enabled)
				self.World.GameEnding -= EmitTerminalSummary;
		}

		void ITick.Tick(Actor self)
		{
			if (!enabled)
				return;

			window.Observe(self.World.Actors.Where(actor => actor.Owner == self.Owner &&
				actor.IsInWorld && !actor.IsDead && actor.Info.Name == "stnk").Select(actor => actor.ActorID));
		}

		void INotifyDamage.Damaged(Actor self, AttackInfo e)
		{
			if (enabled && self.Owner == playerActor.Owner && self.Info.Name == "stnk" && e.Damage.Value > 0)
				window.RecordDamage(self.ActorID, e.Damage.Value);
		}

		void INotifyAppliedDamage.AppliedDamage(Actor self, Actor damaged, AttackInfo e)
		{
			if (!enabled || self.Owner != playerActor.Owner || self.Info.Name != "stnk" ||
				e.DamageState != DamageState.Dead || e.PreviousDamageState == DamageState.Dead ||
				playerActor.Owner.RelationshipWith(damaged.Owner) != PlayerRelationship.Enemy)
				return;

			window.RecordKill(damaged.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0);
		}

		void EmitTerminalSummary()
		{
			if (!StealthAISpecialistPolicy.TryBeginStealthTerminalSummary(
				ref terminalReported, enabled, enabled))
				return;

			Log.Write("debug", "Stealth efficiency control membership owner={0} bot_id={1} " +
				"control=human generation=1 generation-start={2} generation-end={3} kills={4} members=[{5}] " +
				"actor-time-denominator=sum-live-member-ticks summary=terminal diagnostic_only=true.",
				playerActor.Owner.PlayerName, playerActor.ActorID, window.StartTick, playerActor.World.WorldTick,
				window.KillCount, window.Actors.Select(id => "stnk#" + id).JoinWith(","));
			Log.Write("debug", StealthAISpecialistPolicy.FormatStealthEfficiencySummary(
				"terminal", playerActor.ActorID, window.StartTick, playerActor.World.WorldTick, window.Summary()));
		}
	}
}
