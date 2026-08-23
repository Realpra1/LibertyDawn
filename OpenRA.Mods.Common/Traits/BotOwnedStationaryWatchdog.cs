#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Debug/runtime acceptance watchdog that fails when a bot-owned actor remains stationary too long.")]
	public sealed class BotOwnedStationaryWatchdogInfo : TraitInfo, IRulesetLoaded
	{
		[Desc("Maximum non-exempt stationary world ticks before failing.")]
		public readonly int MaximumStationaryTicks = 750;

		[Desc("Interval between diagnostic stationary-state samples.")]
		public readonly int SampleIntervalTicks = 25;

		[Desc("Additional ticks allowed beyond the weapon's declared firing cycle before a sustained firing episode ends.")]
		public readonly int FiringCadenceToleranceTicks = 2;

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (MaximumStationaryTicks <= 0 || SampleIntervalTicks <= 0 || FiringCadenceToleranceTicks < 0)
				throw new YamlException("Bot stationary watchdog intervals must be positive.");
		}

		public override object Create(ActorInitializer init)
		{
			return new BotOwnedStationaryWatchdog(init.Self, this);
		}
	}

	public sealed class BotOwnedStationaryWatchdog : ITick, INotifyAttack, INotifyBeingResupplied, INotifyKilled
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
			if (!self.Owner.IsBot)
			{
				stationaryAge = 0;
				lastCenterPosition = self.CenterPosition;
				UpdateExemption(self, BotStationaryWatchdogExemption.None);
				return;
			}

			var currentHealth = self.TraitOrDefault<IHealth>()?.HP ?? 0;
			var firingTargetValid = firingTarget != null && firingTarget.IsInWorld && !firingTarget.IsDead;
			var exactRootAttackActivity = IsExactRootAttackActivity(self.CurrentActivity);
			var sustainedFiring = StealthTankSquadPolicy.IsSustainedFiringEpisode(
				lastDischargeTick, self.World.WorldTick, firingCadenceTicks,
				firingTargetValid,
				exactRootAttackActivity && ReferenceEquals(self.CurrentActivity, firingActivity),
				firingTargetValid);
			if (!sustainedFiring && lastDischargeTick != int.MinValue)
			{
				var reason = !firingTargetValid ? "target-invalid" :
					!exactRootAttackActivity ? "non-attack-activity" :
					!ReferenceEquals(self.CurrentActivity, firingActivity) ? "attack-activity-changed" :
					"cadence-missed";
				Log.Write("debug", "AI stationary watchdog firing-episode-end owner={0} unit={1}#{2} tick={3}: target={4} last-discharge={5} cadence={6} reason={7} nonexempt-age={8}.",
					self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
					firingTarget == null ? "none" : firingTarget.Info.Name + "#" + firingTarget.ActorID,
					lastDischargeTick, firingCadenceTicks, reason, stationaryAge);
				lastDischargeTick = int.MinValue;
				firingTarget = null;
				firingActivity = null;
				firingArmament = null;
			}

			var observedRepairAmount = StealthTankSquadPolicy.ObservedRepairAmount(lastHealth, currentHealth);
			var currentExemption = StealthTankSquadPolicy.StationaryWatchdogExemption(
				sustainedFiring, observedRepairAmount > 0);
			if (observedRepairAmount > 0)
				Log.Write("debug", "AI stationary watchdog observed-healing owner={0} unit={1}#{2} tick={3}: hp-before={4} hp-after={5} delta={6} episode={7} nonexempt-age={8}.",
					self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
					lastHealth, currentHealth, observedRepairAmount,
					exemption == BotStationaryWatchdogExemption.Repairing ? "continued" : "started",
					stationaryAge);
			UpdateExemption(self, currentExemption);
			var moved = self.CenterPosition != lastCenterPosition;
			var previousAge = stationaryAge;
			stationaryAge = StealthTankSquadPolicy.NextStationaryWatchdogAge(
				stationaryAge, moved, currentExemption);

			// Periodic samples already prove continuous movement. Only log the transition
			// that ends a stationary interval to keep large specialist armies bounded.
			if (moved && previousAge > 0)
				Log.Write("debug", "AI stationary watchdog movement owner={0} unit={1}#{2} tick={3}: from={4} to={5} cell={6} previous-nonexempt-age={7} exemption={8} activity={9}.",
					self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
					lastCenterPosition, self.CenterPosition, self.Location, previousAge,
					currentExemption, ActivitySignature(self.CurrentActivity));

			lastCenterPosition = self.CenterPosition;
			lastHealth = currentHealth;
			if (self.World.WorldTick % info.SampleIntervalTicks == 0)
				Log.Write("debug", "AI stationary watchdog sample owner={0} unit={1}#{2} tick={3}: center={4} cell={5} nonexempt-age={6}/{7} exemption={8} activity={9}.",
					self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
					self.CenterPosition, self.Location, stationaryAge, info.MaximumStationaryTicks,
					currentExemption, ActivitySignature(self.CurrentActivity));

			if (!StealthTankSquadPolicy.StationaryWatchdogFailed(
				stationaryAge, info.MaximumStationaryTicks))
				return;

			var message = $"AI stationary watchdog failure owner={self.Owner.PlayerName} " +
				$"unit={self.Info.Name}#{self.ActorID} tick={self.World.WorldTick}: " +
				$"center={self.CenterPosition} cell={self.Location} " +
				$"nonexempt-age={stationaryAge}/{info.MaximumStationaryTicks} " +
				$"exemption={currentExemption} activity={ActivitySignature(self.CurrentActivity)}.";
			Log.Write("debug", message);
			throw new InvalidOperationException(message);
		}

		void UpdateExemption(Actor self, BotStationaryWatchdogExemption current)
		{
			if (current == exemption)
				return;

			if (exemption != BotStationaryWatchdogExemption.None)
				Log.Write("debug", "AI stationary watchdog exemption-end owner={0} unit={1}#{2} tick={3}: exemption={4} start={5} duration={6} nonexempt-age={7}.",
					self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
					exemption, exemptionStartTick, self.World.WorldTick - exemptionStartTick,
					stationaryAge);

			exemption = current;
			if (exemption == BotStationaryWatchdogExemption.None)
				return;

			exemptionStartTick = self.World.WorldTick;
			Log.Write("debug", "AI stationary watchdog exemption-start owner={0} unit={1}#{2} tick={3}: exemption={4} nonexempt-age={5} center={6} cell={7} activity={8}.",
				self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
				exemption, stationaryAge, self.CenterPosition, self.Location,
				ActivitySignature(self.CurrentActivity));
		}

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			var targetActor = target.Type == TargetType.Actor ? target.Actor : null;
			if (!IsExactRootAttackActivity(self.CurrentActivity))
			{
				if (lastDischargeTick != int.MinValue)
					Log.Write("debug", "AI stationary watchdog firing-episode-end owner={0} unit={1}#{2} tick={3}: target={4} last-discharge={5} cadence={6} reason=discharge-non-attack-activity nonexempt-age={7}.",
						self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
						firingTarget == null ? "none" : firingTarget.Info.Name + "#" + firingTarget.ActorID,
						lastDischargeTick, firingCadenceTicks, stationaryAge);

				lastDischargeTick = int.MinValue;
				firingTarget = null;
				firingActivity = null;
				firingArmament = null;
				Log.Write("debug", "AI stationary watchdog weapon-discharge owner={0} unit={1}#{2} tick={3}: target={4} weapon={5} episode=ignored-non-attack-activity activity={6}.",
					self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
					targetActor == null ? target.Type.ToString() : targetActor.Info.Name + "#" + targetActor.ActorID,
					a.Info.Weapon, ActivitySignature(self.CurrentActivity));
				return;
			}

			var sameEpisode = lastDischargeTick != int.MinValue && firingTarget == targetActor &&
				ReferenceEquals(firingActivity, self.CurrentActivity) && ReferenceEquals(firingArmament, a);
			if (!sameEpisode && lastDischargeTick != int.MinValue)
				Log.Write("debug", "AI stationary watchdog firing-episode-end owner={0} unit={1}#{2} tick={3}: target={4} last-discharge={5} cadence={6} reason=discharge-context-changed nonexempt-age={7}.",
					self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
					firingTarget == null ? "none" : firingTarget.Info.Name + "#" + firingTarget.ActorID,
					lastDischargeTick, firingCadenceTicks, stationaryAge);

			if (!sameEpisode)
				Log.Write("debug", "AI stationary watchdog firing-episode-start owner={0} unit={1}#{2} tick={3}: target={4} weapon={5} nonexempt-age={6} activity={7}.",
					self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
					targetActor == null ? target.Type.ToString() : targetActor.Info.Name + "#" + targetActor.ActorID,
					a.Info.Weapon, stationaryAge, ActivitySignature(self.CurrentActivity));

			lastDischargeTick = self.World.WorldTick;
			firingCadenceTicks = StealthTankSquadPolicy.FiringEpisodeCadenceTicks(
				a.Weapon.ReloadDelay, a.Weapon.BurstDelays, info.FiringCadenceToleranceTicks);
			firingTarget = targetActor;
			firingActivity = self.CurrentActivity;
			firingArmament = a;
			Log.Write("debug", "AI stationary watchdog weapon-discharge owner={0} unit={1}#{2} tick={3}: target={4} weapon={5} burst={6} fire-delay={7} cadence={8} episode={9}.",
				self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
				targetActor == null ? target.Type.ToString() : targetActor.Info.Name + "#" + targetActor.ActorID,
				a.Info.Weapon, a.Burst, a.FireDelay, firingCadenceTicks, sameEpisode ? "continued" : "started");
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel) { }

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			var reason = ActorKilledFiringEpisodeEndReason(lastDischargeTick);
			if (reason == null)
				return;

			Log.Write("debug", "AI stationary watchdog firing-episode-end owner={0} unit={1}#{2} tick={3}: target={4} last-discharge={5} cadence={6} reason={7} nonexempt-age={8}.",
				self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
				firingTarget == null ? "none" : firingTarget.Info.Name + "#" + firingTarget.ActorID,
				lastDischargeTick, firingCadenceTicks, reason, stationaryAge);
			lastDischargeTick = int.MinValue;
			firingTarget = null;
			firingActivity = null;
			firingArmament = null;
			UpdateExemption(self, BotStationaryWatchdogExemption.None);
		}

		public static string ActorKilledFiringEpisodeEndReason(int lastConfirmedDischargeTick)
		{
			return lastConfirmedDischargeTick == int.MinValue ? null : "actor-killed";
		}

		void INotifyBeingResupplied.StartingResupply(Actor self, Actor host)
		{
			Log.Write("debug", "AI stationary watchdog resupply-session-start owner={0} unit={1}#{2} tick={3}: host={4}#{5} health={6} exemption=none-until-health-restored.",
				self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
				host.Info.Name, host.ActorID, self.TraitOrDefault<IHealth>()?.HP ?? 0);
		}

		void INotifyBeingResupplied.StoppingResupply(Actor self, Actor host)
		{
			Log.Write("debug", "AI stationary watchdog resupply-session-end owner={0} unit={1}#{2} tick={3}: host={4} health={5}.",
				self.Owner.PlayerName, self.Info.Name, self.ActorID, self.World.WorldTick,
				host == null ? "none" : host.Info.Name + "#" + host.ActorID,
				self.TraitOrDefault<IHealth>()?.HP ?? 0);
		}

		public static bool IsExactRootAttackActivity(OpenRA.Activities.Activity activity)
		{
			return activity != null && activity.GetType() == typeof(OpenRA.Mods.Common.Activities.Attack);
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
