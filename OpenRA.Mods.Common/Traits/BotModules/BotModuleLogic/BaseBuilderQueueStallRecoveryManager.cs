#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	sealed class BaseBuilderQueueStallRecoveryManager
	{
		sealed class FrontSnapshot
		{
			public ProductionQueue Queue;
			public ProductionItem Item;
		}

		sealed class PreviousFront
		{
			public string Item;
			public int RemainingCost;
		}

		sealed class CancellationSnapshot
		{
			public uint QueueActorId;
			public string Item;
			public int RetainedCount;
		}

		sealed class PostReleaseFront
		{
			public uint QueueActorId;
			public string QueueType;
			public string Item;
			public readonly HashSet<uint> ExistingActorIds = new HashSet<uint>();
			public bool LeftQueue;
		}

		const int HarvesterTarget = 5;

		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		readonly PowerManager playerPower;
		readonly PlayerResources playerResources;
		readonly HashSet<string> harvesterTypes;
		readonly HashSet<string> refineryTypes;
		readonly bool enabled;
		readonly Dictionary<string, PreviousFront> previousFronts =
			new Dictionary<string, PreviousFront>(StringComparer.Ordinal);
		readonly Dictionary<string, PreviousFront> diagnosticFronts =
			new Dictionary<string, PreviousFront>(StringComparer.Ordinal);
		readonly List<CancellationSnapshot> cancellationSnapshots = new List<CancellationSnapshot>();
		readonly Dictionary<string, PostReleaseFront> postReleaseFronts =
			new Dictionary<string, PostReleaseFront>(StringComparer.Ordinal);
		readonly HashSet<string> postReleaseCompletedQueueKeys = new HashSet<string>(StringComparer.Ordinal);

		int nextScanTick;
		int nextObservationDebugTick;
		int noProgressEvidenceTicks;
		string lastEligibilityReason;
		bool paidProgressSinceDebug;
		uint selectedQueueActorId;
		string selectedQueueType;
		string selectedItem;
		ProductionItem selectedProductionItem;
		int selectedRemainingCost;
		int selectedActorsAtSelection;
		bool selectedRefinery;
		int expectedCancellationRefund;
		int cancellationFundsAtSelection;
		int cancellationEarnedAtSelection;
		int cancellationSpentAtSelection;
		bool cancellationResolutionLogged;
		int postReleaseDiagnosticUntilTick;
		int postReleaseCompletedQueues;
		bool awaitingSelectedExit;

		public bool Active { get; private set; }

		public BaseBuilderQueueStallRecoveryManager(BaseBuilderBotModule baseBuilder, Player player,
			PowerManager playerPower)
		{
			this.baseBuilder = baseBuilder;
			world = player.World;
			this.player = player;
			this.playerPower = playerPower;
			playerResources = player.PlayerActor.Trait<PlayerResources>();
			harvesterTypes = baseBuilder.SmartEconomyHarvesterTypes;
			refineryTypes = baseBuilder.SmartEconomyRefineryTypes;
			enabled = !baseBuilder.Info.QueueStallRecoveryExcludedBotTypes.Contains(player.BotType);
		}

		public void Tick(IBot bot)
		{
			if (!enabled || world.WorldTick < nextScanTick)
				return;

			var elapsed = Math.Max(1, baseBuilder.Info.QueueStallRecoveryScanInterval);
			nextScanTick = world.WorldTick + elapsed;

			if (Active || awaitingSelectedExit)
			{
				UpdateActiveRecovery();
				return;
			}

			ObserveAndMaybeRecover(bot, elapsed);
		}

		public MiniYamlNode IssueTraitData()
		{
			if (!enabled)
				return null;

			var nodes = new List<MiniYamlNode>
			{
				Save("Version", 2),
				Save("Active", Active),
				Save("AwaitingSelectedExit", awaitingSelectedExit),
				Save("NextScanTick", nextScanTick),
				Save("NoProgressEvidenceTicks", noProgressEvidenceTicks)
			};

			if (Active || awaitingSelectedExit)
			{
				nodes.Add(Save("SelectedQueueActorId", selectedQueueActorId));
				nodes.Add(Save("SelectedQueueType", selectedQueueType ?? ""));
				nodes.Add(Save("SelectedItem", selectedItem ?? ""));
				nodes.Add(Save("SelectedRemainingCost", selectedRemainingCost));
				nodes.Add(Save("SelectedActorsAtSelection", selectedActorsAtSelection));
				nodes.Add(Save("SelectedRefinery", selectedRefinery));
			}
			else if (noProgressEvidenceTicks > 0 && previousFronts.Count > 0)
			{
				var fronts = previousFronts.OrderBy(f => f.Key, StringComparer.Ordinal).ToArray();
				nodes.Add(Save("PreviousFrontKeys", fronts.Select(f => f.Key).ToArray()));
				nodes.Add(Save("PreviousFrontItems", fronts.Select(f => f.Value.Item).ToArray()));
				nodes.Add(Save("PreviousFrontRemainingCosts", fronts.Select(f => f.Value.RemainingCost).ToArray()));
			}

			return new MiniYamlNode("QueueStallRecoveryState", new MiniYaml("", nodes));
		}

		public void ResolveTraitData(List<MiniYamlNode> data)
		{
			if (!enabled)
				return;

			var state = data.FirstOrDefault(n => n.Key == "QueueStallRecoveryState");
			if (state == null)
				return;

			ResetRecoveryState();
			try
			{
				var nodes = state.Value.Nodes;
				var version = Read(nodes, "Version", 0);
				if (version != 1 && version != 2)
					throw new InvalidOperationException("unsupported save-state version");

				nextScanTick = Math.Max(world.WorldTick, Read(nodes, "NextScanTick", world.WorldTick));
				noProgressEvidenceTicks = Math.Max(0, Read(nodes, "NoProgressEvidenceTicks", 0));
				var restoredActive = Read(nodes, "Active", false);
				var restoredAwaitingExit = version >= 2 && Read(nodes, "AwaitingSelectedExit", false);
				if (!restoredActive && !restoredAwaitingExit)
				{
					RestorePreviousFronts(nodes);
					if (previousFronts.Count == 0)
						noProgressEvidenceTicks = 0;
					return;
				}

				var queueActorId = Read<uint>(nodes, "SelectedQueueActorId", 0);
				var queueType = Read(nodes, "SelectedQueueType", "");
				var itemType = Read(nodes, "SelectedItem", "");
				var refinery = Read(nodes, "SelectedRefinery", false);
				var queue = world.ActorsWithTrait<ProductionQueue>().FirstOrDefault(q =>
					q.Actor.Owner == player && !q.Actor.IsDead && q.Actor.IsInWorld && q.Trait.Enabled &&
					q.Actor.ActorID == queueActorId && q.Trait.Info.Type == queueType).Trait;
				var item = queue?.CurrentItem();
				var validKind = refinery ? refineryTypes.Contains(itemType) : harvesterTypes.Contains(itemType);
				if (item == null || item.Paused || !item.Started || item.Item != itemType || !validKind ||
					(restoredActive && item.Done) || (restoredAwaitingExit && !item.Done))
					throw new InvalidOperationException("selected production front is no longer valid");

				Active = restoredActive;
				awaitingSelectedExit = restoredAwaitingExit;
				selectedQueueActorId = queueActorId;
				selectedQueueType = queueType;
				selectedItem = itemType;
				selectedProductionItem = item;
				selectedRemainingCost = item.RemainingCost;
				selectedActorsAtSelection = Math.Max(0, Read(nodes, "SelectedActorsAtSelection", 0));
				selectedRefinery = refinery;
				cancellationResolutionLogged = true;
				Debug("{0} load-restored tick={1} selected={2}:{3}:{4} remaining={5}/{6} outcome={7} state={8}",
					player, world.WorldTick, selectedQueueActorId, selectedQueueType, selectedItem,
					item.RemainingCost, item.TotalCost, selectedRefinery ? "refinery" : "harvester",
					awaitingSelectedExit ? "completed-awaiting-exit" : "active");
			}
			catch (Exception ex)
			{
				ResetRecoveryState();
				nextScanTick = world.WorldTick + Math.Max(1, baseBuilder.Info.QueueStallRecoveryScanInterval);
				Debug("{0} load-released tick={1} reason={2}; restarting bounded observation",
					player, world.WorldTick, ex.Message);
			}
		}

		void RestorePreviousFronts(List<MiniYamlNode> nodes)
		{
			var keys = Read(nodes, "PreviousFrontKeys", Array.Empty<string>());
			var items = Read(nodes, "PreviousFrontItems", Array.Empty<string>());
			var costs = Read(nodes, "PreviousFrontRemainingCosts", Array.Empty<int>());
			if (keys.Length != items.Length || keys.Length != costs.Length)
				return;

			for (var i = 0; i < keys.Length; i++)
				if (!string.IsNullOrEmpty(keys[i]) && !string.IsNullOrEmpty(items[i]) && costs[i] >= 0 &&
					!previousFronts.ContainsKey(keys[i]))
					previousFronts.Add(keys[i], new PreviousFront { Item = items[i], RemainingCost = costs[i] });
		}

		void ObserveAndMaybeRecover(IBot bot, int elapsed)
		{
			var fronts = EligibleFronts();
			var liveHarvesters = baseBuilder.CountActors(harvesterTypes);
			var hasUsableRefinery = baseBuilder.CountActors(refineryTypes) > 0;
			var normalPower = playerPower == null || playerPower.PowerState == PowerState.Normal;
			var hasHarvesterCandidate = fronts.Any(f => harvesterTypes.Contains(f.Item.Item));
			var hasRefineryCandidate = fronts.Any(f => refineryTypes.Contains(f.Item.Item));
			var hasEconomyCandidate = QueueStallRecoveryPolicy.HasCriticalEconomyCandidate(
				hasUsableRefinery, hasHarvesterCandidate, hasRefineryCandidate);
			var eligibility = QueueStallRecoveryPolicy.ClassifyEconomyObservation(normalPower,
				liveHarvesters, HarvesterTarget, hasEconomyCandidate, fronts.Count);
			var eligible = eligibility == QueueStallRecoveryEligibility.Eligible;

			var madePaidProgress = false;
			foreach (var front in fronts)
				if (previousFronts.TryGetValue(FrontKey(front), out var previous) &&
					previous.Item == front.Item.Item && front.Item.RemainingCost < previous.RemainingCost)
				{
					madePaidProgress = true;
					break;
				}

			paidProgressSinceDebug |= madePaidProgress;
			UpdatePostReleaseDiagnostics(fronts);
			noProgressEvidenceTicks = QueueStallRecoveryPolicy.UpdateNoProgressEvidence(
				noProgressEvidenceTicks, eligible, madePaidProgress, elapsed);
			MaybeDebugObservation(fronts, liveHarvesters, hasUsableRefinery, normalPower,
				hasEconomyCandidate, eligibility);
			RememberFronts(fronts);
			if (!QueueStallRecoveryPolicy.ShouldRecoverEconomy(liveHarvesters, HarvesterTarget,
				hasEconomyCandidate, fronts.Count, noProgressEvidenceTicks,
				baseBuilder.Info.QueueStallRecoveryActivationTicks))
				return;

			var criticalTypes = hasUsableRefinery ? harvesterTypes : refineryTypes;
			var selected = fronts.Where(f => criticalTypes.Contains(f.Item.Item))
				.OrderBy(f => f.Item.RemainingCost)
				.ThenBy(f => f.Queue.Actor.ActorID)
				.ThenBy(f => f.Queue.Info.Type, StringComparer.Ordinal)
				.First();
			Activate(bot, selected, fronts, liveHarvesters);
		}

		void MaybeDebugObservation(IReadOnlyCollection<FrontSnapshot> fronts, int liveHarvesters,
			bool hasUsableRefinery, bool normalPower, bool hasEconomyCandidate,
			QueueStallRecoveryEligibility eligibility)
		{
			if (!baseBuilder.Info.QueueStallRecoveryDebugLogging)
				return;

			var reason = EligibilityReason(eligibility, hasUsableRefinery);
			if (reason == lastEligibilityReason && world.WorldTick < nextObservationDebugTick)
				return;

			var frontDetails = string.Join(",", fronts.Select(f =>
			{
				var paidDelta = diagnosticFronts.TryGetValue(FrontKey(f), out var previous) &&
					previous.Item == f.Item.Item ? Math.Max(0, previous.RemainingCost - f.Item.RemainingCost) : 0;
				return $"{f.Queue.Actor.ActorID}:{f.Queue.Info.Type}:{f.Item.Item}:" +
					$"remaining={f.Item.RemainingCost}/{f.Item.TotalCost}:paid-delta={paidDelta}";
			}));
			Debug("{0} observation tick={1} eligibility={2} normal-power={3} usable-refinery={4} " +
				"live-harvesters={5}/5 economy-candidate={6} fronts=[{7}] " +
				"paid-progress-since-heartbeat={8} evidence={9}", player, world.WorldTick, reason,
				normalPower, hasUsableRefinery, liveHarvesters, hasEconomyCandidate, frontDetails,
				paidProgressSinceDebug, noProgressEvidenceTicks);

			diagnosticFronts.Clear();
			foreach (var front in fronts)
				diagnosticFronts.Add(FrontKey(front), new PreviousFront
				{
					Item = front.Item.Item,
					RemainingCost = front.Item.RemainingCost
				});

			paidProgressSinceDebug = false;
			lastEligibilityReason = reason;
			nextObservationDebugTick = world.WorldTick + Math.Max(250,
				baseBuilder.Info.QueueStallRecoveryActivationTicks);
		}

		static string EligibilityReason(QueueStallRecoveryEligibility eligibility, bool hasUsableRefinery)
		{
			switch (eligibility)
			{
				case QueueStallRecoveryEligibility.Eligible:
					return "eligible";
				case QueueStallRecoveryEligibility.LowPower:
					return "low-power";
				case QueueStallRecoveryEligibility.HarvesterTargetMet:
					return "harvester-target-met";
				case QueueStallRecoveryEligibility.MissingCriticalCandidate:
					return hasUsableRefinery ? "no-harvester-candidate" : "no-refinery-candidate";
				default:
					return "insufficient-contention";
			}
		}

		List<FrontSnapshot> EligibleFronts()
		{
			return world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Actor.IsInWorld && q.Trait.Enabled)
				.OrderBy(q => q.Actor.ActorID)
				.ThenBy(q => q.Trait.Info.Type, StringComparer.Ordinal)
				.Select(q => new FrontSnapshot { Queue = q.Trait, Item = q.Trait.CurrentItem() })
				.Where(f => f.Item != null && f.Item.Started && !f.Item.Done && !f.Item.Paused &&
					f.Queue.BuildableItems().Any(a => a.Name == f.Item.Item))
				.ToList();
		}

		void RememberFronts(IEnumerable<FrontSnapshot> fronts)
		{
			previousFronts.Clear();
			foreach (var front in fronts)
				previousFronts.Add(FrontKey(front), new PreviousFront
				{
					Item = front.Item.Item,
					RemainingCost = front.Item.RemainingCost
				});
		}

		void Activate(IBot bot, FrontSnapshot selected, IReadOnlyCollection<FrontSnapshot> fronts,
			int liveHarvesters)
		{
			Active = true;
			selectedQueueActorId = selected.Queue.Actor.ActorID;
			selectedQueueType = selected.Queue.Info.Type;
			selectedItem = selected.Item.Item;
			selectedProductionItem = selected.Item;
			selectedRemainingCost = selected.Item.RemainingCost;
			selectedRefinery = refineryTypes.Contains(selectedItem);
			selectedActorsAtSelection = selectedRefinery ? baseBuilder.CountActors(refineryTypes) : liveHarvesters;

			var canceled = 0;
			var expectedRefund = 0;
			cancellationSnapshots.Clear();
			foreach (var queue in world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Actor.IsInWorld)
				.OrderBy(q => q.Actor.ActorID).ThenBy(q => q.Trait.Info.Type, StringComparer.Ordinal))
			{
				var items = queue.Trait.AllQueued().ToArray();
				if (items.Any(i => i.Done))
					continue;

				var displaced = queue.Actor.ActorID == selectedQueueActorId ? items.Skip(1) : items.AsEnumerable();
				foreach (var group in displaced.Where(i => !i.Done)
					.GroupBy(i => i.Item, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
				{
					var count = group.Count();
					expectedRefund += group.Sum(i => Math.Max(0, i.TotalCost - i.RemainingCost));
					canceled += count;
					cancellationSnapshots.Add(new CancellationSnapshot
					{
						QueueActorId = queue.Actor.ActorID,
						Item = group.Key,
						RetainedCount = items.Count(i => i.Item == group.Key) - count
					});
					bot.QueueOrder(Order.CancelProduction(queue.Actor, group.Key, count));
				}
			}

			expectedCancellationRefund = expectedRefund;
			cancellationFundsAtSelection = playerResources.Cash + playerResources.Resources;
			cancellationEarnedAtSelection = playerResources.Earned;
			cancellationSpentAtSelection = playerResources.Spent;
			cancellationResolutionLogged = cancellationSnapshots.Count == 0;

			Debug("{0} activated tick={1} reason=no-paid-progress evidence={2} fronts={3} " +
				"live-harvesters={4}/5 selected={5}:{6}:{7} remaining={8}/{9} canceled={10} expected-refund={11}",
				player, world.WorldTick, noProgressEvidenceTicks, fronts.Count, liveHarvesters,
				selectedQueueActorId, selected.Queue.Info.Type, selectedItem,
				selected.Item.RemainingCost, selected.Item.TotalCost, canceled, expectedRefund);
		}

		void UpdateActiveRecovery()
		{
			MaybeDebugCancellationResolution();
			var queueEntry = world.ActorsWithTrait<ProductionQueue>().FirstOrDefault(q =>
				q.Actor.Owner == player && q.Actor.ActorID == selectedQueueActorId &&
				q.Trait.Info.Type == selectedQueueType);
			var producerAvailable = queueEntry.Actor != null && !queueEntry.Actor.IsDead &&
				queueEntry.Actor.IsInWorld && queueEntry.Trait.Enabled;
			var queue = producerAvailable ? queueEntry.Trait : null;
			var item = queue?.CurrentItem();
			var frontState = QueueStallRecoveryPolicy.ClassifySelectedFront(producerAvailable,
				ReferenceEquals(item, selectedProductionItem), item != null && item.Done);
			if (frontState == QueueStallRecoverySelectedFrontState.Active)
			{
				if (item.RemainingCost < selectedRemainingCost)
				{
					Debug("{0} selected progress tick={1} queue={2} item={3} remaining={4}/{5}",
						player, world.WorldTick, selectedQueueActorId, selectedItem,
						item.RemainingCost, item.TotalCost);
					selectedRemainingCost = item.RemainingCost;
				}

				return;
			}

			var liveHarvesters = baseBuilder.CountActors(harvesterTypes);
			var liveRefineries = baseBuilder.CountActors(refineryTypes);
			var completed = (selectedRefinery ? liveRefineries : liveHarvesters) > selectedActorsAtSelection;
			if (QueueStallRecoveryPolicy.ShouldAwaitSelectedFrontOutcome(frontState, completed))
			{
				if (!awaitingSelectedExit)
					Debug("{0} awaiting-exit tick={1} selected={2}:{3} live-harvesters={4}/5 " +
						"live-refineries={5} outcome={6}", player, world.WorldTick, selectedQueueActorId,
						selectedItem, liveHarvesters, liveRefineries, selectedRefinery ? "refinery" : "harvester");

				Active = false;
				awaitingSelectedExit = true;
				return;
			}

			var reason = completed ? "completed" : "selected-front-invalidated";
			Debug("{0} released tick={1} selected={2}:{3} completed={4} reason={5} live-harvesters={6}/5 " +
				"live-refineries={7} outcome={8}", player, world.WorldTick, selectedQueueActorId, selectedItem,
				completed, reason, liveHarvesters, liveRefineries, selectedRefinery ? "refinery" : "harvester");
			if (completed && baseBuilder.Info.QueueStallRecoveryDebugLogging)
			{
				postReleaseFronts.Clear();
				postReleaseCompletedQueueKeys.Clear();
				postReleaseCompletedQueues = 0;
				postReleaseDiagnosticUntilTick = world.WorldTick + Math.Max(1000,
					baseBuilder.Info.QueueStallRecoveryActivationTicks * 8);
				Debug("{0} post-release observation started tick={1} deadline={2}",
					player, world.WorldTick, postReleaseDiagnosticUntilTick);
			}

			ResetRecoveryState();
		}

		void UpdatePostReleaseDiagnostics(IReadOnlyCollection<FrontSnapshot> fronts)
		{
			if (!baseBuilder.Info.QueueStallRecoveryDebugLogging || postReleaseDiagnosticUntilTick == 0)
				return;

			if (world.WorldTick > postReleaseDiagnosticUntilTick || postReleaseCompletedQueues >= 2)
			{
				Debug("{0} post-release observation ended tick={1} completed-queues={2}/2 tracked={3}",
					player, world.WorldTick, postReleaseCompletedQueues, postReleaseFronts.Count);
				postReleaseDiagnosticUntilTick = 0;
				postReleaseFronts.Clear();
				return;
			}

			foreach (var front in fronts)
			{
				var key = PostReleaseKey(front.Queue.Actor.ActorID, front.Queue.Info.Type);
				if (postReleaseFronts.ContainsKey(key) || postReleaseCompletedQueueKeys.Contains(key) ||
					!previousFronts.TryGetValue(key, out var previous) ||
					previous.Item != front.Item.Item || front.Item.RemainingCost >= previous.RemainingCost)
					continue;

				var tracked = new PostReleaseFront
				{
					QueueActorId = front.Queue.Actor.ActorID,
					QueueType = front.Queue.Info.Type,
					Item = front.Item.Item
				};
				foreach (var actor in OwnedActorsByType(tracked.Item))
					tracked.ExistingActorIds.Add(actor.ActorID);

				postReleaseFronts.Add(key, tracked);
				Debug("{0} post-release paid-progress tick={1} queue={2}:{3} item={4} remaining={5}/{6}",
					player, world.WorldTick, tracked.QueueActorId, tracked.QueueType, tracked.Item,
					front.Item.RemainingCost, front.Item.TotalCost);
			}

			foreach (var entry in postReleaseFronts.ToArray())
			{
				var tracked = entry.Value;
				if (!tracked.LeftQueue)
				{
					var current = fronts.FirstOrDefault(f => f.Queue.Actor.ActorID == tracked.QueueActorId &&
						f.Queue.Info.Type == tracked.QueueType);
					tracked.LeftQueue = current == null || current.Item.Item != tracked.Item;
				}

				if (!tracked.LeftQueue)
					continue;

				var completedActor = OwnedActorsByType(tracked.Item)
					.Where(a => !tracked.ExistingActorIds.Contains(a.ActorID))
					.OrderBy(a => a.ActorID)
					.FirstOrDefault();
				if (completedActor == null)
					continue;

				postReleaseCompletedQueueKeys.Add(entry.Key);
				postReleaseCompletedQueues = postReleaseCompletedQueueKeys.Count;
				Debug("{0} post-release completed tick={1} queue={2}:{3} item={4} actor={5} completed-queues={6}/2",
					player, world.WorldTick, tracked.QueueActorId, tracked.QueueType, tracked.Item,
					completedActor.ActorID, postReleaseCompletedQueues);
				postReleaseFronts.Remove(entry.Key);
			}
		}

		static string PostReleaseKey(uint queueActorId, string queueType)
		{
			return queueActorId + ":" + queueType;
		}

		static string FrontKey(FrontSnapshot front)
		{
			return PostReleaseKey(front.Queue.Actor.ActorID, front.Queue.Info.Type);
		}

		IEnumerable<Actor> OwnedActorsByType(string type)
		{
			return world.Actors.Where(a => a.Owner == player && !a.IsDead && a.IsInWorld && a.Info.Name == type);
		}

		void MaybeDebugCancellationResolution()
		{
			if (cancellationResolutionLogged)
				return;

			var unresolved = 0;
			foreach (var cancellation in cancellationSnapshots)
			{
				var queue = world.ActorsWithTrait<ProductionQueue>().FirstOrDefault(q =>
					q.Actor.Owner == player && q.Actor.ActorID == cancellation.QueueActorId).Trait;
				var currentCount = queue?.AllQueued().Count(i => i.Item == cancellation.Item) ?? 0;
				unresolved += Math.Max(0, currentCount - cancellation.RetainedCount);
			}

			if (unresolved != 0)
				return;

			Debug("{0} cancellations resolved tick={1} entries={2} unresolved=0 expected-refund={3} " +
				"earned-delta={4} funds-delta={5} spent-delta={6}", player, world.WorldTick,
				cancellationSnapshots.Count, expectedCancellationRefund,
				playerResources.Earned - cancellationEarnedAtSelection,
				playerResources.Cash + playerResources.Resources - cancellationFundsAtSelection,
				playerResources.Spent - cancellationSpentAtSelection);
			cancellationResolutionLogged = true;
		}

		void Debug(string format, params object[] args)
		{
			if (baseBuilder.Info.QueueStallRecoveryDebugLogging)
				Log.Write("debug", "AI queue stall recovery: " + format, args);
		}

		void ResetRecoveryState()
		{
			Active = false;
			awaitingSelectedExit = false;
			selectedQueueActorId = 0;
			selectedQueueType = null;
			selectedItem = null;
			selectedProductionItem = null;
			selectedRemainingCost = 0;
			selectedActorsAtSelection = 0;
			selectedRefinery = false;
			noProgressEvidenceTicks = 0;
			previousFronts.Clear();
			cancellationSnapshots.Clear();
			cancellationResolutionLogged = false;
		}

		static MiniYamlNode Save<T>(string key, T value) =>
			new MiniYamlNode(key, FieldSaver.FormatValue(value));

		static T Read<T>(List<MiniYamlNode> nodes, string key, T fallback)
		{
			var node = nodes.FirstOrDefault(n => n.Key == key);
			return node == null ? fallback : FieldLoader.GetValue<T>(key, node.Value.Value);
		}
	}
}
