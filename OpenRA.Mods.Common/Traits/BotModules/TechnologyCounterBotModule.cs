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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Changes a bot's technology branch after an enemy specialization remains observed for a configured delay.")]
	public class TechnologyCounterBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between enemy technology observations.")]
		public readonly int ScanInterval = 125;

		[Desc("Game ticks an observed enemy branch must remain dominant before switching.")]
		public readonly int SwitchDelay = 3000;

		[Desc("Fallback branch pursued before the first mature enemy observation when no branch is already owned.")]
		public readonly string InitialBranch = null;

		[Desc("Counter branch for each observed enemy branch.")]
		public readonly Dictionary<string, string> CounterBranches = new Dictionary<string, string>();

		[Desc("Ordered upgrade actor types belonging to each technology branch.")]
		public readonly Dictionary<string, string[]> BranchUpgradeActors = new Dictionary<string, string[]>();

		[Desc("Downgrade actor type for each technology branch.")]
		public readonly Dictionary<string, string> BranchDowngradeActors = new Dictionary<string, string>();

		[Desc("When these prerequisites are owned, retain existing branches while adding the desired branch.")]
		public readonly string[] PreserveExistingBranchesPrerequisites = { "techlevel.extra" };

		[Desc("Minimum ticks between repeated blocked-state diagnostics.")]
		public readonly int StatusLogInterval = 750;

		[Desc("Write observations, transition decisions, requests, and completion to debug.log.")]
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (ScanInterval <= 0 || SwitchDelay < 0 || StatusLogInterval <= 0)
				throw new YamlException("Technology counter scan/log intervals must be positive and switch delay cannot be negative.");

			if (BranchUpgradeActors.Count == 0 || string.IsNullOrEmpty(InitialBranch) ||
				!BranchUpgradeActors.ContainsKey(InitialBranch))
				throw new YamlException("Technology counter InitialBranch must name a configured branch.");

			foreach (var counter in CounterBranches)
				if (!BranchUpgradeActors.ContainsKey(counter.Key) || !BranchUpgradeActors.ContainsKey(counter.Value))
					throw new YamlException($"Technology counter mapping '{counter.Key}: {counter.Value}' references an unknown branch.");

			var actors = new HashSet<string>(StringComparer.Ordinal);
			foreach (var branch in BranchUpgradeActors)
			{
				if (branch.Value == null || branch.Value.Length == 0 || !BranchDowngradeActors.TryGetValue(branch.Key, out var downgrade))
					throw new YamlException($"Technology counter branch '{branch.Key}' needs ordered upgrades and a downgrade actor.");

				foreach (var actor in branch.Value.Concat(new[] { downgrade }))
				{
					if (!rules.Actors.ContainsKey(actor))
						throw new YamlException($"Technology counter actor '{actor}' does not exist.");

					if (!actors.Add(actor))
						throw new YamlException($"Technology counter actor '{actor}' is configured more than once.");
				}
			}
		}

		public override object Create(ActorInitializer init) { return new TechnologyCounterBotModule(init.Self, this); }
	}

	public class TechnologyCounterBotModule : ConditionalTrait<TechnologyCounterBotModuleInfo>, IBotTick, IGameSaveTraitData
	{
		readonly World world;
		readonly Player player;
		readonly Dictionary<string, string> actorBranches = new Dictionary<string, string>(StringComparer.Ordinal);
		readonly HashSet<string> managedTransitionActors = new HashSet<string>(StringComparer.Ordinal);
		IBotRequestUnitProduction[] productionRequesters;
		IBotRequestPauseUnitProduction[] productionPauses;
		TechTree techTree;
		int scanTicks;
		int observedSinceTick = -1;
		int nextBlockedLogTick;
		string observedBranch;
		string desiredBranch;
		string completedDesiredBranch;
		string lastBlockedReason;
		string lastProgressSignature;

		public TechnologyCounterBotModule(Actor self, TechnologyCounterBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			base.Created(self);
			productionRequesters = self.Owner.PlayerActor.TraitsImplementing<IBotRequestUnitProduction>().ToArray();
			productionPauses = self.Owner.PlayerActor.TraitsImplementing<IBotRequestPauseUnitProduction>().ToArray();
			techTree = self.Owner.PlayerActor.Trait<TechTree>();
			foreach (var branch in Info.BranchUpgradeActors)
				foreach (var actor in branch.Value)
				{
					actorBranches.Add(actor, branch.Key);
					managedTransitionActors.Add(actor);
				}

			foreach (var downgrade in Info.BranchDowngradeActors.Values)
				managedTransitionActors.Add(downgrade);
		}

		protected override void TraitEnabled(Actor self)
		{
			scanTicks = 1 + Math.Abs(player.ClientIndex) % Math.Max(1, Info.ScanInterval);
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (player.WinState != WinState.Undefined || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			if (string.IsNullOrEmpty(desiredBranch))
			{
				var ownProgress = Info.BranchUpgradeActors.Keys.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);
				foreach (var actor in world.Actors)
					if (actor.Owner == player && !actor.IsDead && actor.IsInWorld &&
						actorBranches.TryGetValue(actor.Info.Name, out var branch))
						ownProgress[branch]++;

				desiredBranch = TechnologyCounterLogic.DominantBranch(ownProgress) ?? Info.InitialBranch;
				Debug("initialized desired branch {0} from owned technology", desiredBranch);
			}

			var enemyBranch = DominantEnemyBranch();
			if (!string.Equals(enemyBranch, observedBranch, StringComparison.Ordinal))
			{
				observedBranch = enemyBranch;
				observedSinceTick = world.WorldTick;
				Debug("observed enemy branch {0}; counter decision matures at tick {1}",
					enemyBranch ?? "none", world.WorldTick + Info.SwitchDelay);
			}

			var nextDesired = TechnologyCounterLogic.DesiredBranch(desiredBranch, Info.InitialBranch,
				observedBranch, world.WorldTick, observedSinceTick, Info.SwitchDelay, Info.CounterBranches);
			if (!string.Equals(nextDesired, desiredBranch, StringComparison.Ordinal))
			{
				CancelManagedTransitions(bot);
				Debug("mature counter decision changed desired branch {0} -> {1} against {2}",
					desiredBranch ?? "none", nextDesired ?? "none", observedBranch ?? "none");
				desiredBranch = nextDesired;
				completedDesiredBranch = null;
			}

			RequestNextTransition(bot);
		}

		void CancelManagedTransitions(IBot bot)
		{
			foreach (var actorType in managedTransitionActors)
				foreach (var requester in productionRequesters)
					requester.CancelRequestedUnitProduction(bot, actorType);

			var canceled = 0;
			foreach (var queue in world.ActorsWithTrait<ProductionQueue>().Where(q => q.Actor.Owner == player))
				foreach (var group in queue.Trait.AllQueued().Where(i => managedTransitionActors.Contains(i.Item))
					.GroupBy(i => i.Item, StringComparer.Ordinal))
				{
					bot.QueueOrder(Order.CancelProduction(queue.Actor, group.Key, group.Count()));
					canceled += group.Count();
				}

			if (canceled > 0)
				Debug("canceled {0} obsolete queued technology transition(s) before replanning", canceled);
		}

		string DominantEnemyBranch()
		{
			var progress = Info.BranchUpgradeActors.Keys.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);
			foreach (var actor in world.Actors)
				if (!actor.IsDead && actor.IsInWorld && player.RelationshipWith(actor.Owner) == PlayerRelationship.Enemy &&
					actorBranches.TryGetValue(actor.Info.Name, out var branch))
					progress[branch]++;

			return TechnologyCounterLogic.DominantBranch(progress);
		}

		Dictionary<string, int> OwnProgress(out HashSet<string> ownedActorTypes)
		{
			var progress = Info.BranchUpgradeActors.Keys.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);
			ownedActorTypes = new HashSet<string>(StringComparer.Ordinal);
			foreach (var actor in world.Actors)
				if (actor.Owner == player && !actor.IsDead && actor.IsInWorld &&
					actorBranches.TryGetValue(actor.Info.Name, out var branch))
				{
					progress[branch]++;
					ownedActorTypes.Add(actor.Info.Name);
				}

			var signature = string.Join(",", progress.OrderBy(kv => kv.Key, StringComparer.Ordinal)
				.Select(kv => $"{kv.Key}:{kv.Value}"));
			if (signature != lastProgressSignature)
			{
				lastProgressSignature = signature;
				Debug("owned branch progress {0}; desired={1}", signature, desiredBranch ?? "none");
			}

			return progress;
		}

		void RequestNextTransition(IBot bot)
		{
			if (string.IsNullOrEmpty(desiredBranch) || !Info.BranchUpgradeActors.TryGetValue(desiredBranch, out var upgrades))
			{
				Blocked("desired branch is not configured");
				return;
			}

			if (productionPauses.Any(p => p.PauseUnitProduction))
			{
				Blocked("ordinary production is paused for economy recovery/opening refinery");
				return;
			}

			var progress = OwnProgress(out var ownedActorTypes);
			var preserveExisting = Info.PreserveExistingBranchesPrerequisites.Length > 0 &&
				techTree.HasPrerequisites(Info.PreserveExistingBranchesPrerequisites);
			var branchToDowngrade = preserveExisting ? null :
				TechnologyCounterLogic.BranchToDowngrade(progress, desiredBranch);
			if (branchToDowngrade != null)
			{
				if (!Info.BranchDowngradeActors.TryGetValue(branchToDowngrade, out var downgrade))
				{
					Blocked($"missing downgrade actor for {branchToDowngrade}");
					return;
				}

				Request(bot, downgrade, $"leave {branchToDowngrade} for {desiredBranch}");
				return;
			}

			var nextUpgrade = TechnologyCounterLogic.NextUpgrade(upgrades, ownedActorTypes);
			if (nextUpgrade == null)
			{
				if (!string.Equals(completedDesiredBranch, desiredBranch, StringComparison.Ordinal))
				{
					completedDesiredBranch = desiredBranch;
					Debug("completed desired branch {0} against {1}", desiredBranch, observedBranch ?? "unobserved enemy technology");
				}

				ClearBlocked();
				return;
			}

			Request(bot, nextUpgrade, $"pursue {desiredBranch} against {observedBranch ?? "unobserved enemy technology"}");
		}

		void Request(IBot bot, string actorType, string reason)
		{
			var requester = productionRequesters.FirstOrDefault(Exts.IsTraitEnabled);
			if (requester == null)
			{
				Blocked($"no enabled production requester for {actorType}");
				return;
			}

			if (requester.RequestedProductionCount(bot, actorType) > 0 || IsQueued(actorType))
			{
				ClearBlocked();
				return;
			}

			if (!HasFreeBuildableQueue(actorType))
			{
				Blocked($"{actorType} has no free buildable queue");
				return;
			}

			requester.RequestUnitProduction(bot, actorType);
			ClearBlocked();
			Debug("requested {0}: {1}", actorType, reason);
		}

		bool IsQueued(string actorType)
		{
			return world.ActorsWithTrait<ProductionQueue>().Any(q => q.Actor.Owner == player &&
				q.Trait.AllQueued().Any(item => item.Item == actorType));
		}

		bool HasFreeBuildableQueue(string actorType)
		{
			if (!world.Map.Rules.Actors.TryGetValue(actorType, out var actorInfo))
				return false;

			var buildable = actorInfo.TraitInfoOrDefault<BuildableInfo>();
			return buildable != null && buildable.Queue.Any(queueType => AIUtils.FindQueues(player, queueType)
				.Any(queue => !queue.AllQueued().Any() && queue.BuildableItems().Any(item => item.Name == actorType)));
		}

		void Blocked(string reason)
		{
			if (reason == lastBlockedReason && world.WorldTick < nextBlockedLogTick)
				return;

			lastBlockedReason = reason;
			nextBlockedLogTick = world.WorldTick + Info.StatusLogInterval;
			Debug("blocked: {0}; desired={1}, observed={2}", reason, desiredBranch ?? "none", observedBranch ?? "none");
		}

		void ClearBlocked()
		{
			lastBlockedReason = null;
		}

		void Debug(string format, params object[] args)
		{
			if (!Info.DebugLogging)
				return;

			var message = string.Format(format, args);
			AIUtils.BotDebug("AI ({0}) technology counter: {1}", player.ClientIndex, message);
			Log.Write("debug", "AI technology counter: {0} (client {1}) at tick {2}: {3}",
				player, player.ClientIndex, world.WorldTick, message);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("TechnologyCounterScanTicks", FieldSaver.FormatValue(scanTicks)),
				new MiniYamlNode("TechnologyCounterObservedSince", FieldSaver.FormatValue(observedSinceTick)),
				new MiniYamlNode("TechnologyCounterObserved", FieldSaver.FormatValue(observedBranch ?? string.Empty)),
				new MiniYamlNode("TechnologyCounterDesired", FieldSaver.FormatValue(desiredBranch ?? string.Empty))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			foreach (var node in data)
				switch (node.Key)
				{
					case "TechnologyCounterScanTicks": scanTicks = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "TechnologyCounterObservedSince": observedSinceTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "TechnologyCounterObserved": observedBranch = EmptyToNull(FieldLoader.GetValue<string>(node.Key, node.Value.Value)); break;
					case "TechnologyCounterDesired": desiredBranch = EmptyToNull(FieldLoader.GetValue<string>(node.Key, node.Value.Value)); break;
				}
		}

		static string EmptyToNull(string value) { return string.IsNullOrEmpty(value) ? null : value; }
	}
}
