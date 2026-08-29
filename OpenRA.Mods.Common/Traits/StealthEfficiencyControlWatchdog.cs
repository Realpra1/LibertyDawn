#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
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
		string control;
		int lastKillTick;
		bool terminalReported;

		public StealthEfficiencyControlWatchdog(Actor self)
		{
			playerActor = self;
			window = new StealthEfficiencyWindow(self.World.WorldTick);
			lastKillTick = self.World.WorldTick;
		}

		void INotifyCreated.Created(Actor self)
		{
			enabled = Game.Settings.Debug.BotDebug && (!self.Owner.IsBot || self.World.IsReplay) && self.Owner.Playable &&
				!self.Owner.NonCombatant;
			control = self.World.IsReplay ? "replay-owner" : "human";
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
			lastKillTick = playerActor.World.WorldTick;
		}

		void EmitTerminalSummary()
		{
			if (!StealthAISpecialistPolicy.TryBeginStealthTerminalSummary(
				ref terminalReported, enabled, enabled))
				return;

			Log.Write("debug", "Stealth efficiency control membership owner={0} bot_id={1} " +
				"control={2} generation=1 generation-start={3} generation-end={4} kills={5} members=[{6}] " +
				"actor-time-denominator=sum-live-member-ticks summary=terminal diagnostic_only=true.",
				playerActor.Owner.PlayerName, playerActor.ActorID, control, window.StartTick, playerActor.World.WorldTick,
				window.KillCount, window.Actors.Select(id => "stnk#" + id).JoinWith(","));
			Log.Write("debug", StealthAISpecialistPolicy.FormatStealthEfficiencySummary(
				"terminal", playerActor.ActorID, window.StartTick, playerActor.World.WorldTick, window.Summary()));
			if (!playerActor.World.IsReplay)
				return;

			var maximumTicks = Math.Max(1, 45000 / Math.Max(1, playerActor.World.Timestep));
			var cadenceAge = Math.Max(0, playerActor.World.WorldTick - lastKillTick);
			var failed = window.Actors.Length > 0 &&
				StealthAISpecialistPolicy.KillCadenceFailed(cadenceAge, maximumTicks);
			Log.Write("debug", "Stealth kill watchdog [stealth-tank] owner aggregate: owner={0} tick={1} " +
				"generation=1 generation-start={2} window-start={2} scope=replay-owner " +
				"cadence-age={3}/{4} generation-kills={5} stnks={6} formation={6} reinforcements=0 " +
				"members=[{7}] cadence-failed={8} status={9} summary=terminal retained-generations=1 " +
				"comparable=false per-squad=unavailable.",
				playerActor.Owner.PlayerName, playerActor.World.WorldTick, window.StartTick,
				cadenceAge, maximumTicks, window.KillCount, window.Actors.Length,
				window.Actors.Select(id => "stnk#" + id).JoinWith(","), failed,
				window.Actors.Length == 0 ? "exempt" : failed ? "failure" : "pass");
		}
	}
}
