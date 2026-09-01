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
	sealed class StealthUndefendedAttackPrivateState
	{
		public uint? SelectedTargetActorId { get; set; }
		public int LastRefreshTick { get; set; }
		public int NextRefreshTick { get; set; }
		public StealthUndefendedAttackSafetyResult? Safety { get; set; }
		public StealthUndefendedAttackDisposition Disposition { get; set; }
		public uint[] LastIssuedActorIds { get; set; }
		public uint? LastIssuedTargetActorId { get; set; }
		public uint[] LiveDefenderActorIds { get; set; }
	}

	static class StealthUndefendedAttackPersistence
	{
		const int PrivateSaveVersion = 1;

		public static MiniYamlNode Serialize(string key,
			StealthUndefendedAttackHandoff handoff, StealthApproachMission mission,
			uint? selectedTargetActorId, int lastRefreshTick, int nextRefreshTick,
			StealthUndefendedAttackSafetyResult? safety,
			StealthUndefendedAttackDisposition disposition,
			IReadOnlyList<uint> lastIssuedActorIds, uint? lastIssuedTargetActorId,
			IReadOnlyList<uint> liveDefenderActorIds)
		{
			ValidateNormalized(selectedTargetActorId, lastRefreshTick, nextRefreshTick,
				safety, disposition, lastIssuedActorIds, lastIssuedTargetActorId,
				liveDefenderActorIds);
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", PrivateSaveVersion.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Owner", handoff.Owner.ToString()),
				new MiniYamlNode("Epoch", handoff.Epoch.Value.ToString(CultureInfo.InvariantCulture)),
				StealthApproachPersistence.SerializeMission(mission),
				new MiniYamlNode("HasSelectedTarget", FieldSaver.FormatValue(selectedTargetActorId.HasValue)),
				new MiniYamlNode("SelectedTargetActorId", FieldSaver.FormatValue(selectedTargetActorId ?? 0)),
				new MiniYamlNode("LastRefreshTick", lastRefreshTick.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("NextRefreshTick", nextRefreshTick.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("HasSafety", FieldSaver.FormatValue(safety.HasValue)),
				new MiniYamlNode("ThreatRating", FormatDouble(safety?.Score.ThreatRating ?? 0)),
				new MiniYamlNode("Crossover", FormatDouble(safety?.Score.Crossover ?? 0)),
				new MiniYamlNode("SafetyApproved", FieldSaver.FormatValue(safety?.Approved ?? false)),
				new MiniYamlNode("SafetyRequiresReacquisition",
					FieldSaver.FormatValue(safety?.RequiresReacquisition ?? false)),
				new MiniYamlNode("Disposition", disposition.ToString()),
				new MiniYamlNode("HasLastIssuedTarget", FieldSaver.FormatValue(lastIssuedTargetActorId.HasValue)),
				new MiniYamlNode("LastIssuedTargetActorId", FieldSaver.FormatValue(lastIssuedTargetActorId ?? 0))
			};
			foreach (var actorId in lastIssuedActorIds)
				nodes.Add(new MiniYamlNode("LastIssuedActorId", FieldSaver.FormatValue(actorId)));
			foreach (var actorId in liveDefenderActorIds)
				nodes.Add(new MiniYamlNode("LiveDefenderActorId", FieldSaver.FormatValue(actorId)));
			return new MiniYamlNode(key, "", nodes);
		}

		public static StealthUndefendedAttackPrivateState Restore(MiniYamlNode node,
			StealthUndefendedAttackHandoff handoff, StealthApproachMission mission)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));

			var values = Unique(node.Value.Nodes.Where(child => child.Key != "Mission" &&
				child.Key != "LastIssuedActorId" && child.Key != "LiveDefenderActorId"));
			if (!ReadInt(values, "Version", out var version) || version != PrivateSaveVersion)
				throw new InvalidOperationException("Unsupported UndefendedAttack private save schema.");
			if (!values.TryGetValue("Owner", out var ownerText) ||
				!Enum.TryParse(ownerText, out BehaviorId owner) || owner != BehaviorId.UndefendedAttack)
				throw new InvalidOperationException("Invalid UndefendedAttack owner in private save state.");
			if (!ReadLong(values, "Epoch", out var epoch) || epoch <= 0 ||
				owner != handoff.Owner || epoch != handoff.Epoch.Value)
				throw new InvalidOperationException("Stale UndefendedAttack ownership in private save state.");

			var missionNodes = node.Value.Nodes.Where(child => child.Key == "Mission").ToArray();
			if (missionNodes.Length != 1 || StealthApproachPersistence.Canonical(missionNodes[0]) !=
				StealthApproachPersistence.Canonical(StealthApproachPersistence.SerializeMission(mission)))
				throw new InvalidOperationException(
					"UndefendedAttack private state does not match its immutable mission.");

			var hasSelected = Read<bool>(values, "HasSelectedTarget");
			var selectedId = Read<uint>(values, "SelectedTargetActorId");
			var selected = hasSelected ? selectedId : (uint?)null;
			var lastRefresh = ReadRequiredInt(values, "LastRefreshTick");
			var nextRefresh = ReadRequiredInt(values, "NextRefreshTick");
			var hasSafety = Read<bool>(values, "HasSafety");
			var threat = ReadDouble(values, "ThreatRating", false);
			var crossover = ReadDouble(values, "Crossover", true);
			var approved = Read<bool>(values, "SafetyApproved");
			var requiresReacquisition = Read<bool>(values, "SafetyRequiresReacquisition");
			StealthUndefendedAttackSafetyResult? safety = hasSafety ?
				new StealthUndefendedAttackSafetyResult(
					new StealthTargetThreatScore(threat, crossover), approved, requiresReacquisition) :
				(StealthUndefendedAttackSafetyResult?)null;
			if (!ReadEnum(values, "Disposition", out StealthUndefendedAttackDisposition disposition))
				throw new InvalidOperationException("Invalid UndefendedAttack disposition in private save state.");

			var hasOrderTarget = Read<bool>(values, "HasLastIssuedTarget");
			var orderTargetId = Read<uint>(values, "LastIssuedTargetActorId");
			var orderTarget = hasOrderTarget ? orderTargetId : (uint?)null;
			var orderActorIds = node.Value.Nodes.Where(child => child.Key == "LastIssuedActorId")
				.Select(child => FieldLoader.GetValue<uint>(child.Key, child.Value.Value)).ToArray();
			var defenderIds = node.Value.Nodes.Where(child => child.Key == "LiveDefenderActorId")
				.Select(child => FieldLoader.GetValue<uint>(child.Key, child.Value.Value)).ToArray();

			if ((!hasSelected && selectedId != 0) || (!hasSafety &&
				(threat != 0 || crossover != 0 || approved || requiresReacquisition)) ||
				(!hasOrderTarget && orderTargetId != 0))
				throw new InvalidOperationException("Noncanonical UndefendedAttack private save state.");
			ValidateNormalized(selected, lastRefresh, nextRefresh, safety, disposition,
				orderActorIds, orderTarget, defenderIds);

			return new StealthUndefendedAttackPrivateState
			{
				SelectedTargetActorId = selected,
				LastRefreshTick = lastRefresh,
				NextRefreshTick = nextRefresh,
				Safety = safety,
				Disposition = disposition,
				LastIssuedActorIds = orderActorIds,
				LastIssuedTargetActorId = orderTarget,
				LiveDefenderActorIds = defenderIds
			};
		}

		static void ValidateNormalized(uint? selectedTargetActorId,
			int lastRefreshTick, int nextRefreshTick,
			StealthUndefendedAttackSafetyResult? safety,
			StealthUndefendedAttackDisposition disposition,
			IReadOnlyList<uint> lastIssuedActorIds, uint? lastIssuedTargetActorId,
			IReadOnlyList<uint> liveDefenderActorIds)
		{
			if (!Enum.IsDefined(typeof(StealthUndefendedAttackDisposition), disposition) ||
				selectedTargetActorId == 0 || lastIssuedTargetActorId == 0)
				throw new InvalidOperationException("Invalid UndefendedAttack selected state.");
			if (selectedTargetActorId.HasValue ? lastRefreshTick > int.MaxValue -
				StealthUndefendedAttackBehavior.RefreshIntervalTicks ||
				lastRefreshTick < 0 || nextRefreshTick != lastRefreshTick +
					StealthUndefendedAttackBehavior.RefreshIntervalTicks :
				lastRefreshTick != -1 || nextRefreshTick != -1)
				throw new InvalidOperationException("Invalid UndefendedAttack refresh state.");
			if (!OrderedUnique(lastIssuedActorIds, !lastIssuedTargetActorId.HasValue) ||
				!OrderedUnique(liveDefenderActorIds, true))
				throw new InvalidOperationException("Invalid UndefendedAttack actor identity state.");
			if (safety.HasValue && !selectedTargetActorId.HasValue)
				throw new InvalidOperationException("UndefendedAttack safety requires a selected target.");
			if (lastIssuedTargetActorId.HasValue &&
				(lastIssuedTargetActorId != selectedTargetActorId || !safety.HasValue ||
				!safety.Value.Approved || disposition != StealthUndefendedAttackDisposition.Retain))
				throw new InvalidOperationException("Forged UndefendedAttack order deduplication state.");
			if ((disposition == StealthUndefendedAttackDisposition.CrushEvaluation) !=
				(liveDefenderActorIds.Count != 0))
				throw new InvalidOperationException("Inconsistent UndefendedAttack transition state.");
			if (disposition == StealthUndefendedAttackDisposition.Retain && selectedTargetActorId.HasValue &&
				(!safety.HasValue || safety.Value.RequiresReacquisition ||
				(safety.Value.Approved != lastIssuedTargetActorId.HasValue)))
				throw new InvalidOperationException("Inconsistent retained UndefendedAttack safety/order state.");
			if (disposition == StealthUndefendedAttackDisposition.Reacquire &&
				(selectedTargetActorId.HasValue != safety.HasValue ||
				(safety.HasValue && (!safety.Value.RequiresReacquisition || safety.Value.Approved))))
				throw new InvalidOperationException("Inconsistent UndefendedAttack safety transition.");
			if (disposition == StealthUndefendedAttackDisposition.CrushEvaluation && safety.HasValue)
				throw new InvalidOperationException("Crush handoff cannot retain UndefendedAttack safety.");
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
				throw new InvalidOperationException("Duplicate UndefendedAttack private state field.", ex);
			}

			return values;
		}

		static T Read<T>(Dictionary<string, string> values, string key)
		{
			if (!values.TryGetValue(key, out var value))
				throw new InvalidOperationException("Missing UndefendedAttack private state field: " + key);
			return FieldLoader.GetValue<T>(key, value);
		}

		static int ReadRequiredInt(Dictionary<string, string> values, string key)
		{
			if (!ReadInt(values, key, out var value))
				throw new InvalidOperationException("Invalid UndefendedAttack integer field: " + key);
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
				throw new InvalidOperationException("Invalid UndefendedAttack threat context.");
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
