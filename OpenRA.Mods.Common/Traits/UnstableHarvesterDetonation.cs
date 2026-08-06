#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. You can redistribute
 * it and/or modify it under the terms of the GNU General Public License as
 * published by the Free Software Foundation, either version 3 of the License,
 * or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public static class UnstableHarvesterDetonationPolicy
	{
		public static bool IsWarningActive(bool isUnstable, int unstableTicks, int detonationDelay,
			int warningDuration)
		{
			return isUnstable && unstableTicks >= detonationDelay - warningDuration;
		}

		public static bool CanDetonate(bool isUnstable, int unstableTicks, int detonationDelay,
			int warningDuration)
		{
			return IsWarningActive(isUnstable, unstableTicks, detonationDelay, warningDuration) &&
				unstableTicks >= detonationDelay;
		}

		public static bool ShouldEnableAutomaticDetonation(bool isUnstable, bool impactSuppressed)
		{
			return isUnstable && !impactSuppressed;
		}
	}

	[Desc("Times unstable harvester cargo, exposes its warning phase, and permits a mature deploy detonation.")]
	public class UnstableHarvesterDetonationInfo : TraitInfo, IRulesetLoaded,
		Requires<HarvesterInfo>, Requires<IHealthInfo>
	{
		[Desc("Ticks of continuous unstable cargo before detonation is mature.")]
		public readonly int DetonationDelay = 3000;

		[Desc("Ticks at the end of the detonation delay that grant the warning condition.")]
		public readonly int WarningDuration = 750;

		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("Condition granted throughout the warning duration.")]
		public readonly string WarningCondition = null;

		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("Condition granted while automatic unstable damage is allowed by lobby options.")]
		public readonly string AutoDetonationCondition = null;

		[FieldLoader.Require]
		[Desc("Semantic impact type bypassed only by a valid explicit deploy detonation.")]
		public readonly string ImpactType = null;

		[CursorReference]
		public readonly string DeployCursor = "deploy";

		[CursorReference]
		public readonly string DeployBlockedCursor = "deploy-blocked";

		[VoiceReference]
		public readonly string Voice = "Action";

		[Desc("Write unstable timer and deploy decisions to debug.log.")]
		public readonly bool DebugLogging = false;

		public override object Create(ActorInitializer init)
		{
			return new UnstableHarvesterDetonation(init.Self, this);
		}

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (DetonationDelay <= 0 || WarningDuration <= 0 || WarningDuration > DetonationDelay ||
				string.IsNullOrEmpty(WarningCondition) || string.IsNullOrEmpty(AutoDetonationCondition) ||
				string.IsNullOrEmpty(ImpactType))
				throw new YamlException("Unstable harvester detonation delay, warning, condition, and impact type must be valid.");
		}
	}

	public class UnstableHarvesterDetonation : ITick, ISync, IIssueOrder, IResolveOrder, IIssueDeployOrder,
		IOrderVoice, IImpactTypeSuppressionBypass
	{
		readonly UnstableHarvesterDetonationInfo info;
		readonly Harvester harvester;

		[Sync]
		int unstableTicks;
		[Sync]
		bool impactSuppressed;

		int warningConditionToken = Actor.InvalidConditionToken;
		int autoDetonationConditionToken = Actor.InvalidConditionToken;
		bool manualDetonation;

		public int UnstableTicks => unstableTicks;

		public bool CanDetonate => UnstableHarvesterDetonationPolicy.CanDetonate(harvester.IsUnstable,
			unstableTicks, info.DetonationDelay, info.WarningDuration);

		public UnstableHarvesterDetonation(Actor self, UnstableHarvesterDetonationInfo info)
		{
			this.info = info;
			harvester = self.Trait<Harvester>();
		}

		void ITick.Tick(Actor self)
		{
			if (self.IsDead || !harvester.IsUnstable)
			{
				Reset(self);
				return;
			}

			if (unstableTicks == 0)
				impactSuppressed = IsImpactSuppressed(self.World);

			if (unstableTicks < int.MaxValue)
				unstableTicks++;

			var enableAutomatic = UnstableHarvesterDetonationPolicy.ShouldEnableAutomaticDetonation(true,
				impactSuppressed);
			if (enableAutomatic && autoDetonationConditionToken == Actor.InvalidConditionToken)
				autoDetonationConditionToken = self.GrantCondition(info.AutoDetonationCondition);
			else if (!enableAutomatic && autoDetonationConditionToken != Actor.InvalidConditionToken)
				autoDetonationConditionToken = self.RevokeCondition(autoDetonationConditionToken);

			var warning = UnstableHarvesterDetonationPolicy.IsWarningActive(true, unstableTicks,
				info.DetonationDelay, info.WarningDuration);
			if (warning && warningConditionToken == Actor.InvalidConditionToken)
			{
				warningConditionToken = self.GrantCondition(info.WarningCondition);
				Debug(self, "warning started age={0}/{1}", unstableTicks, info.DetonationDelay);
			}

			if (CanDetonate && enableAutomatic)
				Debug(self, "automatic detonation age={0}/{1}", unstableTicks, info.DetonationDelay);
		}

		void Reset(Actor self)
		{
			if (unstableTicks > 0)
				Debug(self, "unstable cargo cleared; timer reset from {0}", unstableTicks);

			unstableTicks = 0;
			impactSuppressed = false;
			manualDetonation = false;
			if (warningConditionToken != Actor.InvalidConditionToken)
				warningConditionToken = self.RevokeCondition(warningConditionToken);

			if (autoDetonationConditionToken != Actor.InvalidConditionToken)
				autoDetonationConditionToken = self.RevokeCondition(autoDetonationConditionToken);
		}

		bool IsImpactSuppressed(World world)
		{
			return world.WorldActor.TraitsImplementing<IImpactTypeSuppressor>()
				.Any(s => s.SuppressImpact(info.ImpactType));
		}

		IEnumerable<IOrderTargeter> IIssueOrder.Orders
		{
			get
			{
				if (harvester.IsUnstable)
					yield return new DeployOrderTargeter("DetonateUnstableHarvester", 5,
						() => CanDetonate ? info.DeployCursor : info.DeployBlockedCursor);
			}
		}

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			return order.OrderID == "DetonateUnstableHarvester" ?
				new Order(order.OrderID, self, queued) : null;
		}

		Order IIssueDeployOrder.IssueDeployOrder(Actor self, bool queued)
		{
			return new Order("DetonateUnstableHarvester", self, queued);
		}

		bool IIssueDeployOrder.CanIssueDeployOrder(Actor self, bool queued) { return CanDetonate; }

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != "DetonateUnstableHarvester")
				return;

			if (!CanDetonate)
			{
				TryDetonate(self);
				return;
			}

			self.QueueActivity(order.Queued, new CallFunc(() => TryDetonate(self)));
		}

		public bool TryDetonate(Actor self)
		{
			if (!CanDetonate)
			{
				Debug(self, "rejected early deploy age={0}/{1} unstable={2}", unstableTicks,
					info.DetonationDelay, harvester.IsUnstable);
				return false;
			}

			manualDetonation = true;
			Debug(self, "accepted explicit deploy detonation age={0}/{1}", unstableTicks, info.DetonationDelay);
			self.Kill(self);
			return true;
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == "DetonateUnstableHarvester" && CanDetonate ? info.Voice : null;
		}

		bool IImpactTypeSuppressionBypass.BypassImpactSuppression(string impactType)
		{
			return manualDetonation && string.Equals(impactType, info.ImpactType, StringComparison.OrdinalIgnoreCase);
		}

		void Debug(Actor self, string format, params object[] args)
		{
			if (!info.DebugLogging)
				return;

			Log.Write("debug", "Unstable harvester {0}#{1} owner={2} tick={3}: {4}", self.Info.Name,
				self.ActorID, self.Owner.InternalName, self.World.WorldTick, string.Format(format, args));
		}
	}
}
