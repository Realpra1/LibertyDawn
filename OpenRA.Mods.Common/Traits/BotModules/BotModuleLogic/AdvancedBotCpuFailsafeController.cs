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
	public readonly struct AdvancedBotFailsafeDecision
	{
		public readonly string Transition;
		public readonly string Module;
		public readonly string Reason;
		public readonly double AdvancedMilliseconds;
		public readonly double TotalMilliseconds;
		public readonly double Share;
		public readonly int PlanningIntervalFactor;

		public AdvancedBotFailsafeDecision(string transition, string module, string reason,
			double advancedMilliseconds, double totalMilliseconds, int planningIntervalFactor = 1)
		{
			Transition = transition;
			Module = module;
			Reason = reason;
			AdvancedMilliseconds = advancedMilliseconds;
			TotalMilliseconds = totalMilliseconds;
			Share = totalMilliseconds > 0 ? advancedMilliseconds / totalMilliseconds : 0;
			PlanningIntervalFactor = planningIntervalFactor;
		}
	}

	public readonly struct AdvancedBotFailsafeState
	{
		public readonly string[] DisabledModules;
		public readonly string Offender;
		public readonly int BreachSamples;
		public readonly int HealthySamples;
		public readonly string RecoveryProbe;
		public readonly int PlanningIntervalFactor;

		public AdvancedBotFailsafeState(string[] disabledModules, string offender, int breachSamples, int healthySamples,
			string recoveryProbe, int planningIntervalFactor = 1)
		{
			DisabledModules = disabledModules;
			Offender = offender;
			BreachSamples = breachSamples;
			HealthySamples = healthySamples;
			RecoveryProbe = recoveryProbe;
			PlanningIntervalFactor = planningIntervalFactor;
		}
	}

	// Deterministic policy only: callers provide elapsed measurements and enact the
	// returned transition. No stopwatch value enters synced actor state or ordering.
	public sealed class AdvancedBotCpuFailsafeController
	{
		readonly string[] moduleOrder;
		readonly HashSet<string> disabled = new HashSet<string>(StringComparer.Ordinal);
		readonly int breachSamplesRequired;
		readonly int recoverySamplesRequired;
		readonly int offenderRecoveryPenaltySamples;
		readonly double lagTolerance;
		readonly double fallbackShare;
		readonly int maxPlanningIntervalFactor;

		int breachSamples;
		int healthySamples;
		string offender;
		string recoveryProbe;
		int planningIntervalFactor = 1;

		public IEnumerable<string> DisabledModules => moduleOrder.Where(disabled.Contains);
		public string Offender => offender;
		public int PlanningIntervalFactor => planningIntervalFactor;

		public AdvancedBotCpuFailsafeController(IEnumerable<string> moduleOrder, int breachSamplesRequired,
			int recoverySamplesRequired, int offenderRecoveryPenaltySamples, float lagTolerance, float fallbackShare,
			bool initiallyDisabled = false, int maxPlanningIntervalFactor = 1)
		{
			this.moduleOrder = moduleOrder.Distinct(StringComparer.Ordinal).ToArray();
			this.breachSamplesRequired = Math.Max(1, breachSamplesRequired);
			this.recoverySamplesRequired = Math.Max(1, recoverySamplesRequired);
			this.offenderRecoveryPenaltySamples = Math.Max(0, offenderRecoveryPenaltySamples);
			this.lagTolerance = Math.Max(0, lagTolerance);
			this.fallbackShare = Math.Max(0, Math.Min(1, fallbackShare));
			this.maxPlanningIntervalFactor = Math.Max(1, maxPlanningIntervalFactor);
			if (initiallyDisabled)
				disabled.UnionWith(this.moduleOrder);
		}

		public bool IsEnabled(string module) { return !disabled.Contains(module); }

		public AdvancedBotFailsafeDecision Update(SimulationPacingSample pacing, double totalMilliseconds,
			IReadOnlyDictionary<string, double> moduleMilliseconds)
		{
			var enabledTimes = moduleOrder.Where(IsEnabled).ToDictionary(m => m,
				m => moduleMilliseconds.TryGetValue(m, out var elapsed) ? Math.Max(0, elapsed) : 0,
				StringComparer.Ordinal);
			var advancedMilliseconds = enabledTimes.Values.Sum();
			var share = totalMilliseconds > 0 ? advancedMilliseconds / totalMilliseconds : 0;
			var realTimeShare = share * Math.Max(0, pacing.RealTimeRatio);

			// Normal-speed lag alone does not identify an advanced module as its cause:
			// rendering, the F8 overlay, or unrelated simulation work may dominate the
			// frame. Shed advanced behavior only when its measured work also exceeds the
			// configured share of one real-time simulation budget.
			var breached = totalMilliseconds > 0 && realTimeShare > fallbackShare &&
				(!pacing.Reliable || pacing.RealTimeRatio > 1d + lagTolerance);
			var reason = pacing.Reliable ? "normal-speed-keep-up-and-cpu-share" :
				pacing.Source + "-half-cpu-budget";

			if (breached)
			{
				healthySamples = 0;
				if (recoveryProbe != null)
				{
					var dominant = enabledTimes.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value)
						.ThenBy(kv => Array.IndexOf(moduleOrder, kv.Key)).Select(kv => kv.Key).FirstOrDefault();
					if (!string.Equals(dominant, recoveryProbe, StringComparison.Ordinal))
						return Decision("probe-pending", recoveryProbe, reason, advancedMilliseconds, totalMilliseconds);

					disabled.Add(recoveryProbe);
					var failedProbe = recoveryProbe;
					offender = failedProbe;
					recoveryProbe = null;
					breachSamples = 0;
					return Decision("re-shed", failedProbe, reason, advancedMilliseconds, totalMilliseconds);
				}

				if (++breachSamples < breachSamplesRequired || !enabledTimes.Any(kv => kv.Value > 0))
					return Decision("held", null, reason, advancedMilliseconds, totalMilliseconds);

				breachSamples = 0;
				if (planningIntervalFactor < maxPlanningIntervalFactor)
				{
					planningIntervalFactor = Math.Min(maxPlanningIntervalFactor, planningIntervalFactor * 2);
					return Decision("throttled", null, reason, advancedMilliseconds, totalMilliseconds);
				}

				var selected = enabledTimes.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value)
					.ThenBy(kv => Array.IndexOf(moduleOrder, kv.Key)).First().Key;
				disabled.Add(selected);
				offender = selected;
				return Decision("disabled", selected, reason, advancedMilliseconds, totalMilliseconds);
			}

			breachSamples = 0;
			if (recoveryProbe != null)
			{
				if (!pacing.Reliable)
				{
					healthySamples = 0;
					return Decision("probe-pending", recoveryProbe, reason, advancedMilliseconds, totalMilliseconds);
				}

				if (++healthySamples < recoverySamplesRequired)
					return Decision("probe-pending", recoveryProbe, reason, advancedMilliseconds, totalMilliseconds);

				var recovered = recoveryProbe;
				recoveryProbe = null;
				healthySamples = 0;
				return Decision("recovered", recovered, reason, advancedMilliseconds, totalMilliseconds);
			}

			if (disabled.Count == 0)
			{
				if (planningIntervalFactor > 1 && ++healthySamples >= recoverySamplesRequired)
				{
					planningIntervalFactor = Math.Max(1, planningIntervalFactor / 2);
					healthySamples = 0;
					return Decision("relaxed", null, reason, advancedMilliseconds, totalMilliseconds);
				}

				if (planningIntervalFactor == 1)
					healthySamples = 0;
				return Decision("healthy", null, reason, advancedMilliseconds, totalMilliseconds);
			}

			// An unreliable share window cannot prove recovery while work is disabled:
			// its missing cost alone can make the window appear healthy. Wait for a
			// reliable normal-speed window before probing, then evaluate the enabled
			// probe normally on the next sample.
			if (!pacing.Reliable)
			{
				healthySamples = 0;
				return Decision("cooldown", null, reason, advancedMilliseconds, totalMilliseconds);
			}

			healthySamples++;
			var recovery = moduleOrder.FirstOrDefault(m => disabled.Contains(m) && !string.Equals(m, offender, StringComparison.Ordinal));
			var required = recoverySamplesRequired;
			if (recovery == null)
			{
				recovery = moduleOrder.FirstOrDefault(disabled.Contains);
				required += offenderRecoveryPenaltySamples;
			}

			if (recovery == null || healthySamples < required)
				return Decision("cooldown", null, reason, advancedMilliseconds, totalMilliseconds);

			disabled.Remove(recovery);
			recoveryProbe = recovery;
			healthySamples = 0;
			return Decision("enabled-probe", recovery, reason, advancedMilliseconds, totalMilliseconds);
		}

		AdvancedBotFailsafeDecision Decision(string transition, string module, string reason,
			double advancedMilliseconds, double totalMilliseconds)
		{
			return new AdvancedBotFailsafeDecision(transition, module, reason,
				advancedMilliseconds, totalMilliseconds, planningIntervalFactor);
		}

		public AdvancedBotFailsafeState ExportState()
		{
			return new AdvancedBotFailsafeState(DisabledModules.ToArray(), offender, breachSamples,
				healthySamples, recoveryProbe, planningIntervalFactor);
		}

		public void ImportState(AdvancedBotFailsafeState state)
		{
			disabled.Clear();
			foreach (var module in state.DisabledModules ?? Array.Empty<string>())
				if (moduleOrder.Contains(module))
					disabled.Add(module);

			offender = moduleOrder.Contains(state.Offender) ? state.Offender : null;
			breachSamples = Math.Max(0, state.BreachSamples);
			healthySamples = Math.Max(0, state.HealthySamples);
			recoveryProbe = moduleOrder.Contains(state.RecoveryProbe) && !disabled.Contains(state.RecoveryProbe) ?
				state.RecoveryProbe : null;
			planningIntervalFactor = Math.Max(1,
				Math.Min(maxPlanningIntervalFactor, state.PlanningIntervalFactor));
		}
	}
}
