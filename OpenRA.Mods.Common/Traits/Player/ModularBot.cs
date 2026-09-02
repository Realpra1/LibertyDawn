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
using System.Diagnostics;
using System.Linq;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Bot that uses BotModules.")]
	[TraitLocation(SystemActors.Player)]
	public sealed class ModularBotInfo : TraitInfo, IBotInfo, IRulesetLoaded
	{
		[FieldLoader.Require]
		[Desc("Internal id for this bot.")]
		public readonly string Type = null;

		[Desc("Human-readable name this bot uses.")]
		public readonly string Name = "Unnamed Bot";

		[Desc("Minimum portion of pending orders to issue each tick (e.g. 5 issues at least 1/5th of all pending orders). Excess orders remain queued for subsequent ticks.")]
		public readonly int MinOrderQuotientPerTick = 5;

		[Desc("Enable bounded CPU accounting and degraded-mode recovery for explicitly listed advanced squad modules.")]
		public readonly bool AdvancedSquadCpuFailsafe = false;

		[Desc("Stable module ids participating in the advanced squad CPU failsafe, in recovery priority order.")]
		public readonly string[] AdvancedSquadModules = Array.Empty<string>();

		[Desc("World ticks in each aligned CPU/pacing sample window.")]
		public readonly int AdvancedSquadSampleInterval = 250;

		[Desc("Normal-speed real-time slowdown allowed before advanced squad work is shed.")]
		public readonly float AdvancedSquadLagTolerance = 0.1f;

		[Desc("Maximum advanced-squad share of measured total simulation time when normal-speed pacing is unreliable.")]
		public readonly float AdvancedSquadCpuShare = 0.5f;

		[Desc("Consecutive breached windows required before shedding an advanced module.")]
		public readonly int AdvancedSquadBreachSamples = 2;

		[Desc("Consecutive healthy windows required before probing one disabled module.")]
		public readonly int AdvancedSquadRecoverySamples = 3;

		[Desc("Extra healthy windows required before probing the dominant offender.")]
		public readonly int AdvancedSquadOffenderPenaltySamples = 2;

		[Desc("Write sparse advanced squad failsafe transitions to debug.log.")]
		public readonly bool AdvancedSquadFailsafeDebugLogging = true;

		string IBotInfo.Type => Type;

		string IBotInfo.Name => Name;

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (MinOrderQuotientPerTick <= 0)
				throw new YamlException("MinOrderQuotientPerTick must be greater than zero.");
			if (AdvancedSquadSampleInterval <= 0 || AdvancedSquadBreachSamples <= 0 ||
				AdvancedSquadRecoverySamples <= 0 || AdvancedSquadOffenderPenaltySamples < 0)
				throw new YamlException("Advanced squad failsafe intervals and sample counts are invalid.");
			if (AdvancedSquadLagTolerance < 0 || AdvancedSquadCpuShare <= 0 || AdvancedSquadCpuShare > 1)
				throw new YamlException("Advanced squad failsafe thresholds must use a non-negative lag tolerance and a CPU share in (0, 1].");
			if (AdvancedSquadCpuFailsafe && AdvancedSquadModules.Length == 0)
				throw new YamlException("AdvancedSquadModules must name at least one module when the failsafe is enabled.");
			if (AdvancedSquadModules.Any(string.IsNullOrEmpty) ||
				AdvancedSquadModules.Distinct(StringComparer.Ordinal).Count() != AdvancedSquadModules.Length)
				throw new YamlException("AdvancedSquadModules must contain unique, non-empty ids.");
		}

		public override object Create(ActorInitializer init) { return new ModularBot(this, init); }
	}

	public sealed class ModularBot : ITick, IBot, INotifyCreated, INotifyDamage, IGameSaveTraitData
	{
		public bool IsEnabled;

		readonly ModularBotInfo info;
		readonly World world;
		readonly Queue<Order> orders = new Queue<Order>();
		internal int QueuedOrderCount => orders.Count;

		Player player;

		IBotTick[] tickModules;
		IReplayBotPolicyTick[] replayPolicyModules = Array.Empty<IReplayBotPolicyTick>();
		IBotRespondToAttack[] attackResponseModules;
		Dictionary<string, List<IAdvancedBotTick>> advancedModules;
		readonly Dictionary<string, double> advancedElapsed = new Dictionary<string, double>(StringComparer.Ordinal);
		AdvancedBotCpuFailsafeController advancedFailsafe;
		SimulationPacingSampler pacingSampler;
		double totalSimulationElapsed;
		int lastAccountedWorldTick = -1;
		long lastSampleTimestamp;
		AdvancedBotFailsafeState? pendingFailsafeState;

		IBotInfo IBot.Info => info;
		Player IBot.Player => player;

		public ModularBot(ModularBotInfo info, ActorInitializer init)
		{
			this.info = info;
			world = init.World;
		}

		void INotifyCreated.Created(Actor self)
		{
			if (self.World.IsReplay && !OpenRA.Server.ProtocolVersion.HasRecordedBotPolicy(self.World.ReplayOrdersProtocol) &&
				self.Owner.IsBot && self.Owner.BotType == info.Type)
				replayPolicyModules = self.TraitsImplementing<IReplayBotPolicyTick>().ToArray();
		}

		// Called by the host's player creation code
		public void Activate(Player p)
		{
			// Bot logic is not allowed to affect world state, and can only act by issuing orders
			// These orders are recorded in the replay, so bots shouldn't be enabled during replays
			if (p.World.IsReplay)
				return;

			IsEnabled = true;
			player = p;
			tickModules = p.PlayerActor.TraitsImplementing<IBotTick>().ToArray();
			attackResponseModules = p.PlayerActor.TraitsImplementing<IBotRespondToAttack>().ToArray();
			if (info.AdvancedSquadCpuFailsafe)
			{
				var configured = new HashSet<string>(info.AdvancedSquadModules, StringComparer.Ordinal);
				advancedModules = tickModules.OfType<IAdvancedBotTick>()
					.Where(m => configured.Contains(m.FailsafeModuleId))
					.GroupBy(m => m.FailsafeModuleId, StringComparer.Ordinal)
					.ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
				foreach (var module in info.AdvancedSquadModules)
					advancedElapsed[module] = 0;

				advancedFailsafe = new AdvancedBotCpuFailsafeController(info.AdvancedSquadModules,
					info.AdvancedSquadBreachSamples, info.AdvancedSquadRecoverySamples,
					info.AdvancedSquadOffenderPenaltySamples, info.AdvancedSquadLagTolerance,
					info.AdvancedSquadCpuShare);
				pacingSampler = new SimulationPacingSampler(info.AdvancedSquadSampleInterval);
				if (pendingFailsafeState.HasValue)
					advancedFailsafe.ImportState(pendingFailsafeState.Value);
				ApplyAdvancedModuleStates();
				if (info.AdvancedSquadFailsafeDebugLogging)
					Log.Write("debug", "Advanced squad failsafe [{0}] active: configured={1} matched={2} " +
						"window={3} lag={4:P0} cpu-share={5:P0}.", p.PlayerName,
						string.Join(",", info.AdvancedSquadModules), string.Join(",", advancedModules.Keys),
						info.AdvancedSquadSampleInterval, info.AdvancedSquadLagTolerance, info.AdvancedSquadCpuShare);
			}

			foreach (var ibe in p.PlayerActor.TraitsImplementing<IBotEnabled>())
				ibe.BotEnabled(this);
		}

		void IBot.QueueOrder(Order order)
		{
			orders.Enqueue(order);
		}

		void ITick.Tick(Actor self)
		{
			if (self.World.IsReplay)
			{
				foreach (var policy in replayPolicyModules)
					policy.ReplayBotPolicyTick();

				return;
			}

			if (!IsEnabled || self.World.IsLoadingGameSave)
				return;

			using (new PerfSample("bot_tick"))
			{
				Sync.RunUnsynced(Game.Settings.Debug.SyncCheckBotModuleCode, world, () =>
				{
					UpdateAdvancedFailsafe();
					foreach (var t in tickModules)
						if (t.IsTraitEnabled())
						{
							var advanced = t as IAdvancedBotTick;
							var accountAdvanced = advancedFailsafe != null && advanced != null &&
								advancedElapsed.ContainsKey(advanced.FailsafeModuleId) &&
								advancedFailsafe.IsEnabled(advanced.FailsafeModuleId);
							if (!accountAdvanced && !Game.IsBenchmarking)
							{
								t.BotTick(this);
								continue;
							}

							var start = Stopwatch.GetTimestamp();
							var queuedOrders = orders.Count;
							try { t.BotTick(this); }
							finally
							{
								var elapsed = 1000.0 * Math.Max(0, Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency;
								if (accountAdvanced)
									advancedElapsed[advanced.FailsafeModuleId] += elapsed;

								if (Game.IsBenchmarking)
								{
									var identity = (t as IBotPerformanceIdentity)?.PerformanceIdentity ?? t.GetType().Name;
									Game.RecordBotModuleSample(player.ClientIndex, identity, elapsed, orders.Count - queuedOrders);
								}
							}
						}
				});
			}

			var ordersToIssueThisTick = Math.Min((orders.Count + info.MinOrderQuotientPerTick - 1) / info.MinOrderQuotientPerTick, orders.Count);
			for (var i = 0; i < ordersToIssueThisTick; i++)
			{
				var order = orders.Dequeue();
				if (IsOrderValidForIssue(order))
					world.IssueOrder(order);
			}
		}

		bool IsOrderValidForIssue(Order order)
		{
			if (order.Subject != null &&
				(order.Subject.Owner != player || !order.Subject.IsInWorld || order.Subject.IsDead))
				return false;

			return order.Target.Actor == null || order.Target.Type == TargetType.Actor;
		}

		void UpdateAdvancedFailsafe()
		{
			if (advancedFailsafe == null)
				return;

			// PerfHistory is read-only here. LastValue is the completed preceding world tick;
			// ignoring the first tick prevents a previous world's terminal sample leaking in.
			if (lastAccountedWorldTick >= 0 && world.WorldTick > lastAccountedWorldTick)
				totalSimulationElapsed += Math.Max(0, PerfHistory.Items["tick_time"].LastValue);
			lastAccountedWorldTick = world.WorldTick;
			var now = Stopwatch.GetTimestamp();
			var sampleElapsed = lastSampleTimestamp == 0 ? -1 :
				1000d * Math.Max(0, now - lastSampleTimestamp) / Stopwatch.Frequency;

			var pacing = pacingSampler.Update(world.WorldTick, Game.LocalTick, world.Timestep, Game.RunTime,
				world.Paused, world.IsLoadingGameSave, Game.IsHeadlessAutomation || world.GameSpeed.RunAtMaximumSpeed,
				sampleElapsed);
			if (!pacing.Sampled)
				return;
			lastSampleTimestamp = now;

			// The elapsed wall window already contains advanced work. Use it only as a
			// same-window floor when the completed tick samples have not caught up.
			var compatibleTotalElapsed = Math.Max(totalSimulationElapsed, Math.Max(0, sampleElapsed));
			var decision = advancedFailsafe.Update(pacing, compatibleTotalElapsed, advancedElapsed);
			var advancedBreakdown = string.Join(",", info.AdvancedSquadModules.Select(module =>
				string.Format("{0}:{1:0.000}", module, advancedElapsed[module])));
			totalSimulationElapsed = 0;
			foreach (var module in info.AdvancedSquadModules)
				advancedElapsed[module] = 0;

			if (decision.Module != null)
				ApplyAdvancedModuleStates();

			if (info.AdvancedSquadFailsafeDebugLogging && decision.Transition != "healthy" && decision.Transition != "cooldown")
			{
				Log.Write("debug", "Advanced squad failsafe [{0}]: tick={1} source={2} reliable={3} reason={4} ratio={5:0.000} " +
					"window={6} total-ms={7:0.000} advanced-ms={8:0.000} module-ms={9} share={10:P1} threshold={11:P0} " +
					"transition={12} module={13} offender={14} disabled={15}.", player.PlayerName, world.WorldTick,
					pacing.Source, pacing.Reliable, decision.Reason, pacing.RealTimeRatio, info.AdvancedSquadSampleInterval,
					decision.TotalMilliseconds, decision.AdvancedMilliseconds, advancedBreakdown, decision.Share,
					info.AdvancedSquadCpuShare, decision.Transition, decision.Module ?? "none",
					advancedFailsafe.Offender ?? "none", string.Join(",", advancedFailsafe.DisabledModules));
			}

			foreach (var diagnostics in advancedModules.Values.SelectMany(modules => modules)
				.OfType<IAdvancedBotFailsafeWindowDiagnostics>())
				diagnostics.EmitAdvancedFailsafeWindowDiagnostics(
					info.AdvancedSquadSampleInterval, decision.Transition);
		}

		void ApplyAdvancedModuleStates()
		{
			if (advancedModules == null)
				return;

			foreach (var entry in advancedModules)
				foreach (var module in entry.Value)
					module.SetAdvancedBehaviorEnabled(advancedFailsafe.IsEnabled(entry.Key));
		}

		void INotifyDamage.Damaged(Actor self, AttackInfo e)
		{
			if (!IsEnabled || self.World.IsLoadingGameSave)
				return;

			using (new PerfSample("bot_attack_response"))
			{
				Sync.RunUnsynced(Game.Settings.Debug.SyncCheckBotModuleCode, world, () =>
				{
					foreach (var t in attackResponseModules)
						if (t.IsTraitEnabled())
							t.RespondToAttack(this, self, e);
				});
			}
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (advancedFailsafe == null)
				return null;

			var state = advancedFailsafe.ExportState();
			return new List<MiniYamlNode>
			{
				new MiniYamlNode("AdvancedFailsafeDisabled", FieldSaver.FormatValue(state.DisabledModules)),
				new MiniYamlNode("AdvancedFailsafeOffender", FieldSaver.FormatValue(state.Offender ?? "")),
				new MiniYamlNode("AdvancedFailsafeBreachSamples", FieldSaver.FormatValue(state.BreachSamples)),
				new MiniYamlNode("AdvancedFailsafeHealthySamples", FieldSaver.FormatValue(state.HealthySamples)),
				new MiniYamlNode("AdvancedFailsafeRecoveryProbe", FieldSaver.FormatValue(state.RecoveryProbe ?? "")),
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (!info.AdvancedSquadCpuFailsafe || self.World.IsReplay)
				return;

			var fields = data.ToDictionary(n => n.Key);
			var state = new AdvancedBotFailsafeState(
				fields.TryGetValue("AdvancedFailsafeDisabled", out var disabledNode) ?
					FieldLoader.GetValue<string[]>("AdvancedFailsafeDisabled", disabledNode.Value.Value) : Array.Empty<string>(),
				fields.TryGetValue("AdvancedFailsafeOffender", out var offenderNode) ? offenderNode.Value.Value : null,
				fields.TryGetValue("AdvancedFailsafeBreachSamples", out var breachNode) ?
					FieldLoader.GetValue<int>("AdvancedFailsafeBreachSamples", breachNode.Value.Value) : 0,
				fields.TryGetValue("AdvancedFailsafeHealthySamples", out var healthyNode) ?
					FieldLoader.GetValue<int>("AdvancedFailsafeHealthySamples", healthyNode.Value.Value) : 0,
				fields.TryGetValue("AdvancedFailsafeRecoveryProbe", out var recoveryProbeNode) ? recoveryProbeNode.Value.Value : null);
			pendingFailsafeState = state;
			if (advancedFailsafe != null)
			{
				advancedFailsafe.ImportState(state);
				ApplyAdvancedModuleStates();
			}
		}
	}
}
