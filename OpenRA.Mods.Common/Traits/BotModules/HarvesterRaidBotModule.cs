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
	[Desc("Sends a bounded share of unstable-resource stealth harvesters toward distinct enemy structures.")]
	public class HarvesterRaidBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Harvester actor types eligible for unstable-resource raids.")]
		public readonly HashSet<string> RaiderTypes = new HashSet<string>();

		[Desc("Resource type that must actually be present in the harvester cargo.")]
		public readonly string RaidResourceType = "RedTiberium";

		[Desc("Maximum percentage of all live harvesters assigned at once. Rounded up to preserve one raid slot for small economies.")]
		public readonly int MaximumRaidPercent = 5;

		[Desc("Ticks between raid assignment scans.")]
		public readonly int RaidInterval = 1500;

		[Desc("Maximum high-value enemy destinations considered each scan.")]
		public readonly int MaximumTargetOptions = 15;

		[Desc("Whether destinations must be visible instead of using the bot's complete knowledge.")]
		public readonly bool CheckTargetVisibility = false;

		[Desc("Write raid assignments and cleanup to debug.log.")]
		public readonly bool DebugLogging = false;

		public override object Create(ActorInitializer init) { return new HarvesterRaidBotModule(init.Self, this); }
	}

	public class HarvesterRaidBotModule : ConditionalTrait<HarvesterRaidBotModuleInfo>, IBotTick, IGameSaveTraitData
	{
		readonly World world;
		readonly Player player;
		readonly Dictionary<uint, uint> assignments = new Dictionary<uint, uint>();
		int raidTicks;

		public HarvesterRaidBotModule(Actor self, HarvesterRaidBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void TraitEnabled(Actor self)
		{
			raidTicks = world.LocalRandom.Next(0, Math.Max(1, Info.RaidInterval));
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (Info.RaidInterval <= 0 || player.WinState != WinState.Undefined || --raidTicks > 0)
				return;

			raidTicks = Info.RaidInterval;
			CleanupAssignments();
			AssignRaids(bot);
		}

		void CleanupAssignments()
		{
			var actors = world.Actors.ToDictionary(a => a.ActorID);
			foreach (var raiderId in assignments.Keys.ToArray())
			{
				if (!actors.TryGetValue(raiderId, out var raider) || raider.IsDead || !raider.IsInWorld ||
					!actors.TryGetValue(assignments[raiderId], out var target) || target.IsDead || !target.IsInWorld)
				{
					Debug("released assignment {0} -> {1}", raiderId, assignments[raiderId]);
					assignments.Remove(raiderId);
				}
			}
		}

		void AssignRaids(IBot bot)
		{
			var harvesters = world.Actors.Where(a => a.Owner == player && !a.IsDead && a.IsInWorld &&
				a.TraitOrDefault<Harvester>() != null).OrderBy(a => a.ActorID).ToArray();
			var limit = HarvesterRaidLogic.RaidLimit(harvesters.Length, Info.MaximumRaidPercent);
			var available = Math.Max(0, limit - assignments.Count);
			if (available == 0)
				return;

			var usedTargets = new HashSet<uint>(assignments.Values);
			var usedCells = new HashSet<CPos>(world.Actors.Where(a => usedTargets.Contains(a.ActorID)).Select(a => a.Location));
			var targets = world.Actors.Where(a => !a.IsDead && a.IsInWorld &&
				player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy && a.Info.HasTraitInfo<BuildingInfo>() &&
				(!Info.CheckTargetVisibility || a.CanBeViewedByPlayer(player)) && !usedTargets.Contains(a.ActorID) &&
				!usedCells.Contains(a.Location)).OrderByDescending(a => a.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0)
				.ThenBy(a => a.ActorID).Take(Math.Max(1, Info.MaximumTargetOptions)).ToList();

			foreach (var raider in harvesters.Where(a =>
			{
				if (!Info.RaiderTypes.Contains(a.Info.Name) || assignments.ContainsKey(a.ActorID))
					return false;

				var cargo = a.Trait<Harvester>().Contents;
				return cargo.TryGetValue(Info.RaidResourceType, out var amount) && amount > 0;
			}).Take(available))
			{
				var harvester = raider.Trait<Harvester>();
				if (!harvester.Contents.TryGetValue(Info.RaidResourceType, out var carried) || carried <= 0)
					continue;

				var target = targets.Where(a => !usedCells.Contains(a.Location)).MinByOrDefault(a =>
					(a.CenterPosition - raider.CenterPosition).LengthSquared);
				if (target == null)
					break;

				bot.QueueOrder(new Order("Move", raider, Target.FromCell(world, target.Location), false));
				assignments[raider.ActorID] = target.ActorID;
				usedTargets.Add(target.ActorID);
				usedCells.Add(target.Location);
				targets.Remove(target);
				Debug("assigned {0}#{1} carrying {2} {3} to {4}#{5} at {6}", raider.Info.Name,
					raider.ActorID, carried, Info.RaidResourceType, target.Info.Name, target.ActorID, target.Location);
			}
		}

		void Debug(string format, params object[] args)
		{
			if (Info.DebugLogging)
				AIUtils.BotDebug("AI ({0}) harvester raid: {1}", player.ClientIndex, string.Format(format, args));
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>
			{
				new MiniYamlNode("HarvesterRaidTicks", FieldSaver.FormatValue(raidTicks)),
				new MiniYamlNode("HarvesterRaidAssignments", FieldSaver.FormatValue(assignments.Select(kv => $"{kv.Key}:{kv.Value}").ToArray()))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var ticks = data.FirstOrDefault(n => n.Key == "HarvesterRaidTicks");
			if (ticks != null)
				raidTicks = FieldLoader.GetValue<int>(ticks.Key, ticks.Value.Value);

			assignments.Clear();
			var saved = data.FirstOrDefault(n => n.Key == "HarvesterRaidAssignments");
			if (saved == null)
				return;

			foreach (var pair in FieldLoader.GetValue<string[]>(saved.Key, saved.Value.Value))
			{
				var parts = pair.Split(':');
				if (parts.Length == 2 && uint.TryParse(parts[0], out var raider) && uint.TryParse(parts[1], out var target))
					assignments[raider] = target;
			}
		}
	}
}
