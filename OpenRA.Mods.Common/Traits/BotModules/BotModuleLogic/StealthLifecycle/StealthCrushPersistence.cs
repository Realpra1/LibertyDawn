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
using System.Globalization;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	sealed class StealthCrushPrivateState
	{
		public int LastObservedTick { get; set; }
		public uint? SelectedTargetActorId { get; set; }
		public CPos? SelectedTargetCurrentCell { get; set; }
		public int LastRefreshTick { get; set; }
		public int NextRefreshTick { get; set; }
		public StealthCrushThreatFacts ThreatFacts { get; set; }
		public StealthCrushSafetyResult? Safety { get; set; }
		public StealthCrushDisposition Disposition { get; set; }
		public uint[] LastIssuedActorIds { get; set; }
		public uint? LastIssuedTargetActorId { get; set; }
		public CPos? LastIssuedTargetCurrentCell { get; set; }
		public uint[] LiveDefenderActorIds { get; set; }
		public uint[] LiveObjectiveActorIds { get; set; }
	}

	static class StealthCrushPersistence
	{
		const int PrivateSaveVersion = 1;
		static readonly string[] ScalarKeys =
		{
			"Version", "Owner", "Epoch", "LastObservedTick", "HasSelectedTarget",
			"SelectedTargetActorId", "SelectedTargetCurrentCell", "LastRefreshTick",
			"NextRefreshTick", "HasSafety", "ThreatSelectedTargetActorId",
			"ThreatSelectedTargetCurrentCell", "ThreatFormationCloaked",
			"ThreatHasDetectorCoverage", "ThreatPlannedActionRevealsFormation",
			"ThreatRating", "Crossover", "SafetyApproved", "Disposition",
			"HasLastIssuedTarget", "LastIssuedTargetActorId", "LastIssuedTargetCurrentCell"
		};

		public static MiniYamlNode Serialize(string key, StealthCrushEvaluationHandoff handoff,
			StealthApproachMission mission, int lastObservedTick, uint? selectedTargetActorId,
			CPos? selectedTargetCurrentCell, int lastRefreshTick, int nextRefreshTick,
			StealthCrushThreatFacts threatFacts, StealthCrushSafetyResult? safety,
			StealthCrushDisposition disposition, IReadOnlyList<uint> lastIssuedActorIds,
			uint? lastIssuedTargetActorId, CPos? lastIssuedTargetCurrentCell,
			IReadOnlyList<uint> liveDefenderActorIds, IReadOnlyList<uint> liveObjectiveActorIds)
		{
			ValidateNormalized(lastObservedTick, selectedTargetActorId, selectedTargetCurrentCell,
				lastRefreshTick, nextRefreshTick, threatFacts, safety, disposition,
				lastIssuedActorIds, lastIssuedTargetActorId, lastIssuedTargetCurrentCell,
				liveDefenderActorIds, liveObjectiveActorIds);
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", PrivateSaveVersion.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Owner", handoff.Owner.ToString()),
				new MiniYamlNode("Epoch", handoff.Epoch.Value.ToString(CultureInfo.InvariantCulture)),
				StealthApproachPersistence.SerializeMission(mission),
				new MiniYamlNode("LastObservedTick", lastObservedTick.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("HasSelectedTarget", FieldSaver.FormatValue(selectedTargetActorId.HasValue)),
				new MiniYamlNode("SelectedTargetActorId", FieldSaver.FormatValue(selectedTargetActorId ?? 0)),
				new MiniYamlNode("SelectedTargetCurrentCell",
					FieldSaver.FormatValue(selectedTargetCurrentCell ?? default(CPos))),
				new MiniYamlNode("LastRefreshTick", lastRefreshTick.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("NextRefreshTick", nextRefreshTick.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("HasSafety", FieldSaver.FormatValue(safety.HasValue)),
				new MiniYamlNode("ThreatSelectedTargetActorId",
					FieldSaver.FormatValue(threatFacts?.SelectedTargetActorId ?? 0)),
				new MiniYamlNode("ThreatSelectedTargetCurrentCell",
					FieldSaver.FormatValue(threatFacts?.SelectedTargetCurrentCell ?? default(CPos))),
				new MiniYamlNode("ThreatFormationCloaked",
					FieldSaver.FormatValue(threatFacts?.FormationCloaked ?? false)),
				new MiniYamlNode("ThreatHasDetectorCoverage",
					FieldSaver.FormatValue(threatFacts?.HasDetectorCoverage ?? false)),
				new MiniYamlNode("ThreatPlannedActionRevealsFormation", "False"),
				new MiniYamlNode("ThreatRating", FormatDouble(safety?.Score.ThreatRating ?? 0)),
				new MiniYamlNode("Crossover", FormatDouble(safety?.Score.Crossover ?? 0)),
				new MiniYamlNode("SafetyApproved", FieldSaver.FormatValue(safety?.Approved ?? false)),
				new MiniYamlNode("Disposition", disposition.ToString()),
				new MiniYamlNode("HasLastIssuedTarget",
					FieldSaver.FormatValue(lastIssuedTargetActorId.HasValue)),
				new MiniYamlNode("LastIssuedTargetActorId",
					FieldSaver.FormatValue(lastIssuedTargetActorId ?? 0)),
				new MiniYamlNode("LastIssuedTargetCurrentCell",
					FieldSaver.FormatValue(lastIssuedTargetCurrentCell ?? default(CPos)))
			};
			foreach (var actorId in handoff.LiveDefenderActorIds)
				nodes.Add(new MiniYamlNode("IncomingDefenderActorId", FieldSaver.FormatValue(actorId)));
			if (threatFacts != null)
			{
				foreach (var actorId in threatFacts.FriendlyActorIds)
					nodes.Add(new MiniYamlNode("ThreatFriendlyActorId", FieldSaver.FormatValue(actorId)));
				foreach (var actorId in threatFacts.EnemyActorIds)
					nodes.Add(new MiniYamlNode("ThreatEnemyActorId", FieldSaver.FormatValue(actorId)));
			}

			foreach (var actorId in lastIssuedActorIds)
				nodes.Add(new MiniYamlNode("LastIssuedActorId", FieldSaver.FormatValue(actorId)));
			foreach (var actorId in liveDefenderActorIds)
				nodes.Add(new MiniYamlNode("LiveDefenderActorId", FieldSaver.FormatValue(actorId)));
			foreach (var actorId in liveObjectiveActorIds)
				nodes.Add(new MiniYamlNode("LiveObjectiveActorId", FieldSaver.FormatValue(actorId)));
			return new MiniYamlNode(key, "", nodes);
		}

		public static StealthCrushPrivateState Restore(MiniYamlNode node,
			StealthCrushEvaluationHandoff handoff, StealthApproachMission mission)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));
			var repeated = new HashSet<string>(StringComparer.Ordinal)
			{
				"IncomingDefenderActorId", "ThreatFriendlyActorId", "ThreatEnemyActorId",
				"LastIssuedActorId", "LiveDefenderActorId", "LiveObjectiveActorId"
			};
			var values = Unique(node.Value.Nodes.Where(child => child.Key != "Mission" &&
				!repeated.Contains(child.Key)));
			if (values.Count != ScalarKeys.Length || ScalarKeys.Any(key => !values.ContainsKey(key)))
				throw new InvalidOperationException("Unexpected Crush private state field set.");
			if (!ReadInt(values, "Version", out var version) || version != PrivateSaveVersion)
				throw new InvalidOperationException("Unsupported Crush private save schema.");
			if (!values.TryGetValue("Owner", out var ownerText) ||
				!Enum.TryParse(ownerText, out BehaviorId owner) || owner != BehaviorId.CrushEvaluation)
				throw new InvalidOperationException("Invalid Crush owner in private save state.");
			if (!ReadLong(values, "Epoch", out var epoch) || epoch <= 0 ||
				owner != handoff.Owner || epoch != handoff.Epoch.Value)
				throw new InvalidOperationException("Stale Crush ownership in private save state.");

			var missionNodes = node.Value.Nodes.Where(child => child.Key == "Mission").ToArray();
			if (missionNodes.Length != 1 || StealthApproachPersistence.Canonical(missionNodes[0]) !=
				StealthApproachPersistence.Canonical(StealthApproachPersistence.SerializeMission(mission)))
				throw new InvalidOperationException("Crush private state does not match its immutable mission.");
			var incomingIds = ReadIds(node, "IncomingDefenderActorId");
			if (!incomingIds.SequenceEqual(handoff.LiveDefenderActorIds))
				throw new InvalidOperationException("Crush private state does not match its immutable handoff.");

			var lastObservedTick = ReadRequiredInt(values, "LastObservedTick");
			var hasSelected = Read<bool>(values, "HasSelectedTarget");
			var selectedIdValue = Read<uint>(values, "SelectedTargetActorId");
			var selectedCellValue = Read<CPos>(values, "SelectedTargetCurrentCell");
			var selectedId = hasSelected ? selectedIdValue : (uint?)null;
			var selectedCell = hasSelected ? selectedCellValue : (CPos?)null;
			var lastRefreshTick = ReadRequiredInt(values, "LastRefreshTick");
			var nextRefreshTick = ReadRequiredInt(values, "NextRefreshTick");
			var hasSafety = Read<bool>(values, "HasSafety");
			var threatSelectedId = Read<uint>(values, "ThreatSelectedTargetActorId");
			var threatSelectedCell = Read<CPos>(values, "ThreatSelectedTargetCurrentCell");
			var formationCloaked = Read<bool>(values, "ThreatFormationCloaked");
			var detectorCoverage = Read<bool>(values, "ThreatHasDetectorCoverage");
			var plannedReveal = Read<bool>(values, "ThreatPlannedActionRevealsFormation");
			var friendlyIds = ReadIds(node, "ThreatFriendlyActorId");
			var enemyIds = ReadIds(node, "ThreatEnemyActorId");
			var threatRating = ReadDouble(values, "ThreatRating", false);
			var crossover = ReadDouble(values, "Crossover", true);
			var approved = Read<bool>(values, "SafetyApproved");
			StealthCrushThreatFacts facts = hasSafety ? new StealthCrushThreatFacts(
				threatSelectedId, threatSelectedCell, friendlyIds, enemyIds,
				formationCloaked, detectorCoverage) : null;
			StealthCrushSafetyResult? safety = hasSafety ? new StealthCrushSafetyResult(
				new StealthTargetThreatScore(threatRating, crossover), approved) :
				(StealthCrushSafetyResult?)null;
			if (!ReadEnum(values, "Disposition", out StealthCrushDisposition disposition))
				throw new InvalidOperationException("Invalid Crush disposition in private save state.");

			var hasOrderTarget = Read<bool>(values, "HasLastIssuedTarget");
			var orderTargetValue = Read<uint>(values, "LastIssuedTargetActorId");
			var orderCellValue = Read<CPos>(values, "LastIssuedTargetCurrentCell");
			var orderTarget = hasOrderTarget ? orderTargetValue : (uint?)null;
			var orderCell = hasOrderTarget ? orderCellValue : (CPos?)null;
			var orderActorIds = ReadIds(node, "LastIssuedActorId");
			var defenderIds = ReadIds(node, "LiveDefenderActorId");
			var objectiveIds = ReadIds(node, "LiveObjectiveActorId");

			if ((!hasSelected && (selectedIdValue != 0 || selectedCellValue != default(CPos))) ||
				(!hasSafety && (threatSelectedId != 0 || threatSelectedCell != default(CPos) ||
					formationCloaked || detectorCoverage || plannedReveal || friendlyIds.Length != 0 ||
					enemyIds.Length != 0 || threatRating != 0 || crossover != 0 || approved)) ||
				plannedReveal || (!hasOrderTarget &&
					(orderTargetValue != 0 || orderCellValue != default(CPos))))
				throw new InvalidOperationException("Noncanonical Crush private save state.");

			ValidateNormalized(lastObservedTick, selectedId, selectedCell, lastRefreshTick,
				nextRefreshTick, facts, safety, disposition, orderActorIds, orderTarget, orderCell,
				defenderIds, objectiveIds);
			return new StealthCrushPrivateState
			{
				LastObservedTick = lastObservedTick,
				SelectedTargetActorId = selectedId,
				SelectedTargetCurrentCell = selectedCell,
				LastRefreshTick = lastRefreshTick,
				NextRefreshTick = nextRefreshTick,
				ThreatFacts = facts,
				Safety = safety,
				Disposition = disposition,
				LastIssuedActorIds = orderActorIds,
				LastIssuedTargetActorId = orderTarget,
				LastIssuedTargetCurrentCell = orderCell,
				LiveDefenderActorIds = defenderIds,
				LiveObjectiveActorIds = objectiveIds
			};
		}

		static void ValidateNormalized(int lastObservedTick, uint? selectedTargetActorId,
			CPos? selectedTargetCurrentCell, int lastRefreshTick, int nextRefreshTick,
			StealthCrushThreatFacts threatFacts, StealthCrushSafetyResult? safety,
			StealthCrushDisposition disposition, IReadOnlyList<uint> lastIssuedActorIds,
			uint? lastIssuedTargetActorId, CPos? lastIssuedTargetCurrentCell,
			IReadOnlyList<uint> liveDefenderActorIds, IReadOnlyList<uint> liveObjectiveActorIds)
		{
			if (lastObservedTick < -1 || !Enum.IsDefined(typeof(StealthCrushDisposition), disposition) ||
				selectedTargetActorId == 0 || lastIssuedTargetActorId == 0 ||
				selectedTargetActorId.HasValue != selectedTargetCurrentCell.HasValue ||
				lastIssuedTargetActorId.HasValue != lastIssuedTargetCurrentCell.HasValue ||
				(threatFacts != null) != safety.HasValue ||
				selectedTargetActorId.HasValue != safety.HasValue)
				throw new InvalidOperationException("Invalid Crush selected or safety state.");
			if (selectedTargetActorId.HasValue ? lastRefreshTick < 0 ||
				lastRefreshTick > lastObservedTick || lastRefreshTick > int.MaxValue -
					StealthCrushBehavior.RefreshIntervalTicks ||
				nextRefreshTick != lastRefreshTick + StealthCrushBehavior.RefreshIntervalTicks :
				lastRefreshTick != -1 || nextRefreshTick != -1)
				throw new InvalidOperationException("Invalid Crush refresh state.");
			if (!OrderedUnique(lastIssuedActorIds, true) ||
				(lastIssuedTargetActorId.HasValue != (lastIssuedActorIds.Count != 0)) ||
				!OrderedUnique(liveDefenderActorIds, true) || !OrderedUnique(liveObjectiveActorIds, true))
				throw new InvalidOperationException("Invalid Crush actor identity state.");
			if (safety.HasValue && (threatFacts.SelectedTargetActorId != selectedTargetActorId ||
				threatFacts.SelectedTargetCurrentCell != selectedTargetCurrentCell ||
				!threatFacts.EnemyActorIds.SequenceEqual(liveDefenderActorIds)))
				throw new InvalidOperationException("Inconsistent Crush live threat context.");
			if (lastIssuedTargetActorId.HasValue && (disposition != StealthCrushDisposition.Retain ||
				lastIssuedTargetActorId != selectedTargetActorId ||
				lastIssuedTargetCurrentCell != selectedTargetCurrentCell ||
				!safety.HasValue || !safety.Value.Approved ||
				!lastIssuedActorIds.SequenceEqual(threatFacts.FriendlyActorIds)))
				throw new InvalidOperationException("Forged Crush order deduplication state.");
			if (disposition == StealthCrushDisposition.Retain &&
				(liveDefenderActorIds.Count == 0 || !safety.HasValue || !safety.Value.Approved ||
					!lastIssuedTargetActorId.HasValue))
				throw new InvalidOperationException("Inconsistent retained Crush state.");
			if (disposition == StealthCrushDisposition.Kite &&
				(liveDefenderActorIds.Count == 0 || lastIssuedTargetActorId.HasValue ||
					(safety.HasValue && safety.Value.Approved)))
				throw new InvalidOperationException("Inconsistent Crush-to-Kite state.");
			if (disposition == StealthCrushDisposition.UndefendedAttack &&
				(liveDefenderActorIds.Count != 0 || liveObjectiveActorIds.Count == 0 ||
					selectedTargetActorId.HasValue || safety.HasValue || lastIssuedTargetActorId.HasValue))
				throw new InvalidOperationException("Inconsistent Crush-to-UndefendedAttack state.");
			if (disposition == StealthCrushDisposition.Reacquire &&
				(liveDefenderActorIds.Count != 0 || liveObjectiveActorIds.Count != 0 ||
					selectedTargetActorId.HasValue || safety.HasValue || lastIssuedTargetActorId.HasValue))
				throw new InvalidOperationException("Inconsistent Crush reacquisition state.");
		}

		static uint[] ReadIds(MiniYamlNode node, string key)
		{
			return node.Value.Nodes.Where(child => child.Key == key)
				.Select(child => FieldLoader.GetValue<uint>(child.Key, child.Value.Value)).ToArray();
		}

		static bool OrderedUnique(IReadOnlyList<uint> ids, bool allowEmpty)
		{
			return (allowEmpty || ids.Count != 0) && ids.All(id => id != 0) &&
				ids.SequenceEqual(ids.OrderBy(id => id)) && ids.Distinct().Count() == ids.Count;
		}

		static Dictionary<string, string> Unique(IEnumerable<MiniYamlNode> nodes)
		{
			var values = new Dictionary<string, string>(StringComparer.Ordinal);
			try
			{
				foreach (var node in nodes)
					values.Add(node.Key, node.Value.Value);
			}
			catch (ArgumentException ex)
			{
				throw new InvalidOperationException("Duplicate Crush private state field.", ex);
			}

			return values;
		}

		static T Read<T>(Dictionary<string, string> values, string key)
		{
			if (!values.TryGetValue(key, out var value))
				throw new InvalidOperationException("Missing Crush private state field: " + key);
			return FieldLoader.GetValue<T>(key, value);
		}

		static int ReadRequiredInt(Dictionary<string, string> values, string key)
		{
			if (!ReadInt(values, key, out var value))
				throw new InvalidOperationException("Invalid Crush integer field: " + key);
			return value;
		}

		static bool ReadInt(Dictionary<string, string> values, string key, out int value)
		{
			value = 0;
			return values.TryGetValue(key, out var text) && int.TryParse(
				text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}

		static bool ReadLong(Dictionary<string, string> values, string key, out long value)
		{
			value = 0;
			return values.TryGetValue(key, out var text) && long.TryParse(
				text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}

		static double ReadDouble(Dictionary<string, string> values, string key, bool allowInfinity)
		{
			if (!values.TryGetValue(key, out var text) || !double.TryParse(text,
				NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
				double.IsNaN(value) || value < 0 || (!allowInfinity && double.IsInfinity(value)))
				throw new InvalidOperationException("Invalid Crush threat context.");
			return value;
		}

		static bool ReadEnum<T>(Dictionary<string, string> values, string key, out T value)
			where T : struct
		{
			value = default;
			return values.TryGetValue(key, out var text) && Enum.TryParse(text, out value) &&
				Enum.IsDefined(typeof(T), value);
		}

		static string FormatDouble(double value)
		{
			return value.ToString("R", CultureInfo.InvariantCulture);
		}
	}
}
