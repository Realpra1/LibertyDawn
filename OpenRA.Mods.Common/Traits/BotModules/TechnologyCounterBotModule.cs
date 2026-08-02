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
	[Desc("Changes an AI technology branch after observing enemy specialization for a configured delay.")]
	public class TechnologyCounterBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between enemy technology observations.")]
		public readonly int ScanInterval = 125;

		[Desc("Ticks an observed enemy branch must remain dominant before switching.")]
		public readonly int SwitchDelay = 3000;

		[Desc("Branch to establish before an enemy specialization is observed.")]
		public readonly string InitialBranch = null;

		[Desc("Counter branch for each observed enemy branch.")]
		public readonly Dictionary<string, string> CounterBranches = new Dictionary<string, string>();

		[Desc("Ordered upgrade actor types belonging to each technology branch.")]
		public readonly Dictionary<string, string[]> BranchUpgradeActors = new Dictionary<string, string[]>();

		[Desc("Downgrade actor type for each technology branch.")]
		public readonly Dictionary<string, string> BranchDowngradeActors = new Dictionary<string, string>();

		[Desc("Write counter-technology observations and requests to debug.log.")]
		public readonly bool DebugLogging = false;

		public override object Create(ActorInitializer init) { return new TechnologyCounterBotModule(init.Self, this); }
	}

	public class TechnologyCounterBotModule : ConditionalTrait<TechnologyCounterBotModuleInfo>, IBotTick, IGameSaveTraitData
	{
		readonly World world;
		readonly Player player;
		IBotRequestUnitProduction[] productionRequesters;
		int scanTicks;
		int observedSinceTick;
		string observedBranch;
		string desiredBranch;

		public TechnologyCounterBotModule(Actor self, TechnologyCounterBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			productionRequesters = self.Owner.PlayerActor.TraitsImplementing<IBotRequestUnitProduction>().ToArray();
		}

		protected override void TraitEnabled(Actor self)
		{
			scanTicks = world.LocalRandom.Next(0, Math.Max(1, Info.ScanInterval));
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (Info.ScanInterval <= 0 || player.WinState != WinState.Undefined || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			var enemyBranch = DominantEnemyBranch();
			if (enemyBranch != observedBranch)
			{
				observedBranch = enemyBranch;
				observedSinceTick = world.WorldTick;
				Debug("observed enemy branch changed to {0}; counter decision delayed until tick {1}",
					enemyBranch ?? "none", observedSinceTick + Info.SwitchDelay);
				return;
			}

			if (enemyBranch == null)
			{
				desiredBranch = Info.InitialBranch;
				if (desiredBranch != null)
					RequestNextTransition(bot);

				return;
			}

			if (!TechnologyCounterLogic.DelayElapsed(world.WorldTick, observedSinceTick, Info.SwitchDelay) ||
				!Info.CounterBranches.TryGetValue(enemyBranch, out desiredBranch))
				return;

			RequestNextTransition(bot);
		}

		string DominantEnemyBranch()
		{
			var counts = Info.BranchUpgradeActors.Keys.ToDictionary(k => k, _ => 0, StringComparer.OrdinalIgnoreCase);
			foreach (var actor in world.Actors)
			{
				if (actor.IsDead || !actor.IsInWorld || player.RelationshipWith(actor.Owner) != PlayerRelationship.Enemy)
					continue;

				foreach (var branch in Info.BranchUpgradeActors)
					if (branch.Value.Contains(actor.Info.Name))
						counts[branch.Key]++;
			}

			return TechnologyCounterLogic.DominantBranch(counts);
		}

		string OwnBranch()
		{
			foreach (var branch in Info.BranchUpgradeActors.OrderBy(kv => kv.Key, StringComparer.Ordinal))
				if (world.Actors.Any(a => a.Owner == player && !a.IsDead && a.IsInWorld && branch.Value.Contains(a.Info.Name)))
					return branch.Key;

			return null;
		}

		void RequestNextTransition(IBot bot)
		{
			var requester = productionRequesters.FirstOrDefault(Exts.IsTraitEnabled);
			if (requester == null || !Info.BranchUpgradeActors.TryGetValue(desiredBranch, out var upgrades))
				return;

			var ownBranch = OwnBranch();
			if (ownBranch != null && !ownBranch.Equals(desiredBranch, StringComparison.OrdinalIgnoreCase))
			{
				if (Info.BranchDowngradeActors.TryGetValue(ownBranch, out var downgrade))
					Request(bot, requester, downgrade, $"leave {ownBranch} for {desiredBranch}");

				return;
			}

			foreach (var upgrade in upgrades)
			{
				if (world.Actors.Any(a => a.Owner == player && !a.IsDead && a.IsInWorld && a.Info.Name == upgrade))
					continue;

				Request(bot, requester, upgrade, $"counter {observedBranch} with {desiredBranch}");
				return;
			}
		}

		void Request(IBot bot, IBotRequestUnitProduction requester, string actor, string reason)
		{
			if (requester.RequestedProductionCount(bot, actor) > 0)
				return;

			requester.RequestUnitProduction(bot, actor);
			Debug("requested {0}: {1}", actor, reason);
		}

		void Debug(string format, params object[] args)
		{
			if (Info.DebugLogging)
				AIUtils.BotDebug("AI ({0}) technology counter: {1}", player.ClientIndex, string.Format(format, args));
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
					case "TechnologyCounterObserved": observedBranch = FieldLoader.GetValue<string>(node.Key, node.Value.Value); break;
					case "TechnologyCounterDesired": desiredBranch = FieldLoader.GetValue<string>(node.Key, node.Value.Value); break;
				}
		}
	}
}
