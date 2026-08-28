#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software and licensed under the GNU General Public License version 3.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Development-test watchdog that fails when a bot-owned actor remains stationary for too long.")]
	public sealed class BotOwnedStationaryWatchdogInfo : TraitInfo, IRulesetLoaded
	{
		[Desc("Maximum non-exempt stationary in-game milliseconds before failing.")]
		public readonly int MaximumStationaryMilliseconds = 30000;

		[Desc("Interval between diagnostic stationary-state samples.")]
		public readonly int SampleIntervalTicks = 25;

		[Desc("Additional ticks allowed beyond the declared firing cycle before a sustained firing episode ends.")]
		public readonly int FiringCadenceToleranceTicks = 2;

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (MaximumStationaryMilliseconds <= 0 || SampleIntervalTicks <= 0 ||
				FiringCadenceToleranceTicks < 0)
				throw new YamlException("Bot stationary watchdog intervals must be positive.");
		}

		public override object Create(ActorInitializer init)
		{
			return new BotOwnedStationaryWatchdog(init.Self, this);
		}
	}

	public sealed class BotOwnedStationaryWatchdog : ITick, INotifyAttack, INotifyKilled
	{
		readonly BotOwnedStationaryWatchdogInfo info;
		WPos lastCenterPosition;
		int stationaryAge;
		int lastDischargeTick = int.MinValue;
		int firingCadenceTicks;
		Actor firingTarget;
		OpenRA.Activities.Activity firingActivity;
		Armament firingArmament;
		int lastHealth;
		BotStationaryWatchdogExemption exemption;
		int exemptionStartTick;

		public BotOwnedStationaryWatchdog(Actor self, BotOwnedStationaryWatchdogInfo info)
		{
			this.info = info;
			lastCenterPosition = self.CenterPosition;
			lastHealth = self.TraitOrDefault<IHealth>()?.HP ?? 0;
		}

		void ITick.Tick(Actor self)
		{
			// This is permanent acceptance instrumentation, but release-default play pays only this guard.
			if (!Game.Settings.Debug.BotDebug || !self.Owner.IsBot)
			{
				stationaryAge = 0;
				lastCenterPosition = self.CenterPosition;
				lastHealth = self.TraitOrDefault<IHealth>()?.HP ?? 0;
				UpdateExemption(self, BotStationaryWatchdogExemption.None);
				return;
			}

			var currentHealth = self.TraitOrDefault<IHealth>()?.HP ?? 0;
			var firingTargetValid = firingTarget != null && firingTarget.IsInWorld && !firingTarget.IsDead;
			var sameFiringActivity = ContinuesConfirmedFiringActivity(firingActivity, self.CurrentActivity);
			var sustainedFiring = StealthAISpecialistPolicy.IsSustainedFiringEpisode(
				lastDischargeTick, self.World.WorldTick, firingCadenceTicks,
				firingTargetValid, sameFiringActivity, firingTargetValid);
			if (!sustainedFiring && lastDischargeTick != int.MinValue)
			{
				var reason = !firingTargetValid ? "target-invalid" :
					!sameFiringActivity ? "firing-activity-changed" : "cadence-missed";
				EndFiringEpisode(self, reason);
			}

			var observedRepairAmount = StealthAISpecialistPolicy.ObservedRepairAmount(lastHealth, currentHealth);
			var currentExemption = StealthAISpecialistPolicy.StationaryWatchdogExemption(
				sustainedFiring, observedRepairAmount > 0);
			if (observedRepairAmount > 0)
				Log.Write("debug", "AI stationary watchdog observed-healing owner={0} unit={1}#{2} tick={3}: " +
					"hp-before={4} hp-after={5} delta={6} episode={7} nonexempt-age={8}.",
					self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
					lastHealth, currentHealth, observedRepairAmount,
					exemption == BotStationaryWatchdogExemption.Repairing ? "continued" : "started",
					stationaryAge);
			UpdateExemption(self, currentExemption);

			var moved = self.CenterPosition != lastCenterPosition;
			var previousAge = stationaryAge;
			stationaryAge = StealthAISpecialistPolicy.NextStationaryWatchdogAge(
				stationaryAge, moved, currentExemption);
			if (moved && previousAge > 0)
				Log.Write("debug", "AI stationary watchdog movement owner={0} unit={1}#{2} tick={3}: " +
					"from={4} to={5} cell={6} previous-nonexempt-age={7} exemption={8} activity={9}.",
					self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
					lastCenterPosition, self.CenterPosition, self.Location, previousAge,
					currentExemption, ActivitySignature(self.CurrentActivity));

			lastCenterPosition = self.CenterPosition;
			lastHealth = currentHealth;
			var maximumTicks = Math.Max(1, info.MaximumStationaryMilliseconds /
				Math.Max(1, self.World.Timestep));
			if (self.World.WorldTick % info.SampleIntervalTicks == 0)
				Log.Write("debug", "AI stationary watchdog sample owner={0} unit={1}#{2} tick={3}: " +
					"center={4} cell={5} nonexempt-age={6}/{7} maximum-ms={8} exemption={9} activity={10}.",
					self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
					self.CenterPosition, self.Location, stationaryAge, maximumTicks,
					info.MaximumStationaryMilliseconds, currentExemption,
					ActivitySignature(self.CurrentActivity));

			if (!StealthAISpecialistPolicy.StationaryWatchdogFailed(stationaryAge, maximumTicks))
				return;

			var message = $"AI stationary watchdog failure owner={self.Owner.PlayerName} " +
				$"unit={self.Info.Name}#{self.ActorID} tick={self.World.WorldTick}: " +
				$"center={self.CenterPosition} cell={self.Location} nonexempt-age={stationaryAge}/{maximumTicks} " +
				$"maximum-ms={info.MaximumStationaryMilliseconds} exemption={currentExemption} " +
				$"activity={ActivitySignature(self.CurrentActivity)}.";
			Log.Write("debug", message);
			throw new InvalidOperationException(message);
		}

		void UpdateExemption(Actor self, BotStationaryWatchdogExemption current)
		{
			if (current == exemption)
				return;

			if (exemption != BotStationaryWatchdogExemption.None)
				Log.Write("debug", "AI stationary watchdog exemption-end owner={0} unit={1}#{2} tick={3}: " +
					"exemption={4} start={5} duration={6} nonexempt-age={7}.",
					self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
					exemption, exemptionStartTick, self.World.WorldTick - exemptionStartTick, stationaryAge);

			exemption = current;
			if (exemption == BotStationaryWatchdogExemption.None)
				return;

			exemptionStartTick = self.World.WorldTick;
			Log.Write("debug", "AI stationary watchdog exemption-start owner={0} unit={1}#{2} tick={3}: " +
				"exemption={4} nonexempt-age={5} center={6} cell={7} activity={8}.",
				self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
				exemption, stationaryAge, self.CenterPosition, self.Location,
				ActivitySignature(self.CurrentActivity));
		}

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			if (!Game.Settings.Debug.BotDebug || !self.Owner.IsBot)
				return;

			var targetActor = target.Type == TargetType.Actor ? target.Actor : null;
			var sameEpisode = lastDischargeTick != int.MinValue && firingTarget == targetActor &&
				ContinuesConfirmedFiringActivity(firingActivity, self.CurrentActivity) &&
				ReferenceEquals(firingArmament, a);
			if (!sameEpisode && lastDischargeTick != int.MinValue)
				EndFiringEpisode(self, "discharge-context-changed");

			if (!sameEpisode)
				Log.Write("debug", "AI stationary watchdog firing-episode-start owner={0} unit={1}#{2} tick={3}: " +
					"target={4} weapon={5} nonexempt-age={6} activity={7}.",
					self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
					targetActor == null ? target.Type.ToString() : targetActor.Info.Name + "#" + targetActor.ActorID,
					a.Info.Weapon, stationaryAge, ActivitySignature(self.CurrentActivity));

			lastDischargeTick = self.World.WorldTick;
			firingCadenceTicks = StealthAISpecialistPolicy.FiringEpisodeCadenceTicks(
				a.Weapon.ReloadDelay, a.Weapon.BurstDelays, info.FiringCadenceToleranceTicks);
			firingTarget = targetActor;
			firingActivity = self.CurrentActivity;
			firingArmament = a;
			Log.Write("debug", "AI stationary watchdog weapon-discharge owner={0} unit={1}#{2} tick={3}: " +
				"target={4} weapon={5} burst={6} fire-delay={7} cadence={8} episode={9}.",
				self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
				targetActor == null ? target.Type.ToString() : targetActor.Info.Name + "#" + targetActor.ActorID,
				a.Info.Weapon, a.Burst, a.FireDelay, firingCadenceTicks, sameEpisode ? "continued" : "started");
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel) { }

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (lastDischargeTick != int.MinValue)
				EndFiringEpisode(self, "actor-killed");
			UpdateExemption(self, BotStationaryWatchdogExemption.None);
		}

		void EndFiringEpisode(Actor self, string reason)
		{
			Log.Write("debug", "AI stationary watchdog firing-episode-end owner={0} unit={1}#{2} tick={3}: " +
				"target={4} last-discharge={5} cadence={6} reason={7} nonexempt-age={8}.",
				self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
				firingTarget == null ? "none" : firingTarget.Info.Name + "#" + firingTarget.ActorID,
				lastDischargeTick, firingCadenceTicks, reason, stationaryAge);
			lastDischargeTick = int.MinValue;
			firingTarget = null;
			firingActivity = null;
			firingArmament = null;
		}

		public static bool ContinuesConfirmedFiringActivity(
			OpenRA.Activities.Activity recordedActivity, OpenRA.Activities.Activity currentActivity)
		{
			return recordedActivity != null && ReferenceEquals(recordedActivity, currentActivity);
		}

		static string ActivitySignature(OpenRA.Activities.Activity activity)
		{
			if (activity == null)
				return "idle";

			var chain = new List<string>();
			for (var current = activity; current != null && chain.Count < 12; current = current.NextActivity)
				chain.Add(current.GetType().Name + ":" + current.State);
			return string.Join(">", chain);
		}
	}
}
