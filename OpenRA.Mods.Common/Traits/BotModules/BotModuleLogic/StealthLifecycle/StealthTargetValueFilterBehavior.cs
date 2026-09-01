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
	/// <summary>
	/// Disabled TargetValueFilter owner. It evaluates only immutable configured value facts supplied
	/// by TargetAcquisition; travel, threat, detection, local combat, and orders are outside this owner.
	/// </summary>
	public sealed class StealthTargetValueFilterBehavior
	{
		const int PrivateSaveVersion = 1;

		sealed class PersistedOption
		{
			public StealthTargetOption Option { get; }
			public long StrategicValue { get; }
			public bool Retained { get; }

			public PersistedOption(StealthTargetOption option, long strategicValue, bool retained)
			{
				Option = option;
				StrategicValue = strategicValue;
				Retained = retained;
			}
		}

		readonly StealthTargetValueFilterHandoff handoff;

		public StealthTargetValueFilterBehavior(StealthTargetValueFilterHandoff handoff)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetValueFilter)
				throw new ArgumentException(
					"The TargetValueFilter behavior requires TargetValueFilter ownership.", nameof(handoff));
		}

		public StealthTargetValueFilterResult Execute()
		{
			return BuildResult(handoff.Options);
		}

		public MiniYamlNode SerializePrivateState(StealthTargetValueFilterResult result,
			string key = "TargetValueFilter")
		{
			ValidateOwnedResult(result);
			var retainedCells = result.Options.Select(option => option.StrategicCell).ToHashSet();
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", PrivateSaveVersion.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Owner", result.Handoff.Owner.ToString()),
				new MiniYamlNode("Epoch", result.Handoff.Epoch.Value.ToString(CultureInfo.InvariantCulture))
			};

			foreach (var option in handoff.Options)
				nodes.Add(SerializeOption(option, Score(option), retainedCells.Contains(option.StrategicCell)));

			return new MiniYamlNode(key, "", nodes);
		}

		public StealthTargetValueFilterResult RestorePrivateState(MiniYamlNode node)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));

			var values = ReadUniqueValues(node.Value.Nodes.Where(child => child.Key != "Option"),
				"TargetValueFilter private state");
			if (!TryReadInt(values, "Version", out var version) || version != PrivateSaveVersion)
				throw new InvalidOperationException("Unsupported stealth TargetValueFilter private save schema.");
			if (!values.TryGetValue("Owner", out var ownerText) ||
				!Enum.TryParse(ownerText, out BehaviorId owner) || owner != BehaviorId.TargetValueFilter)
				throw new InvalidOperationException("Invalid stealth TargetValueFilter owner in private save state.");
			if (!TryReadLong(values, "Epoch", out var epoch) || epoch <= 0 ||
				owner != handoff.Owner || epoch != handoff.Epoch.Value)
				throw new InvalidOperationException("Stale stealth TargetValueFilter ownership in private save state.");

			var persisted = node.Value.Nodes.Where(child => child.Key == "Option")
				.Select(RestoreOption).ToArray();
			if (persisted.Length != handoff.Options.Count ||
				!persisted.Select(option => option.Option).Zip(handoff.Options, SameOption).All(equal => equal))
				throw new InvalidOperationException(
					"TargetValueFilter private state does not match its immutable input handoff.");

			var result = BuildResult(handoff.Options);
			var retainedCells = result.Options.Select(option => option.StrategicCell).ToHashSet();
			if (persisted.Any(option => option.StrategicValue != Score(option.Option) ||
				option.Retained != retainedCells.Contains(option.Option.StrategicCell)))
				throw new InvalidOperationException("Invalid normalized TargetValueFilter private state.");

			return result;
		}

		StealthTargetValueFilterResult BuildResult(IReadOnlyList<StealthTargetOption> options)
		{
			var scored = options.Select(option => new StealthTargetValueOption(option, Score(option))).ToList();
			var highTier = scored.Where(option =>
				StealthAISpecialistPolicy.MeetsMinimumStrategicCellValue(option.StrategicValue)).ToList();
			var eligible = highTier.Count == 0 ? scored : highTier;
			var retainCount = (eligible.Count + 1) / 2;
			var retained = eligible.OrderByDescending(option => option.StrategicValue)
				.ThenBy(option => option.StableIdentity)
				.ThenBy(option => option.StrategicCell.Y)
				.ThenBy(option => option.StrategicCell.X)
				.Take(retainCount).ToArray();

			return new StealthTargetValueFilterResult(handoff.Handoff, retained, true);
		}

		static long Score(StealthTargetOption option)
		{
			long total = 0;
			foreach (var target in option.StrategicTargets)
			{
				var value = StealthAISpecialistPolicy.StrategicTargetValueByRemainingHealth(
					target.ConfiguredPriority, target.ActorValue, target.HitPoints, target.MaximumHitPoints);
				if (value <= 0)
					continue;
				if (long.MaxValue - total < value)
					return long.MaxValue;
				total += value;
			}

			return total;
		}

		static MiniYamlNode SerializeOption(StealthTargetOption option,
			long strategicValue, bool retained)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("StrategicCell", FieldSaver.FormatValue(option.StrategicCell)),
				new MiniYamlNode("EstimatedTravelMilliseconds",
					FieldSaver.FormatValue(option.EstimatedTravelMilliseconds)),
				new MiniYamlNode("IsIncumbent", FieldSaver.FormatValue(option.IsIncumbent)),
				new MiniYamlNode("StrategicValue", strategicValue.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Retained", FieldSaver.FormatValue(retained))
			};

			foreach (var target in option.StrategicTargets)
				nodes.Add(new MiniYamlNode("Target", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("StableActorId", FieldSaver.FormatValue(target.StableActorId)),
					new MiniYamlNode("ConfiguredPriority", FieldSaver.FormatValue(target.ConfiguredPriority)),
					new MiniYamlNode("ActorValue", FieldSaver.FormatValue(target.ActorValue)),
					new MiniYamlNode("HitPoints", FieldSaver.FormatValue(target.HitPoints)),
					new MiniYamlNode("MaximumHitPoints", FieldSaver.FormatValue(target.MaximumHitPoints))
				}));

			return new MiniYamlNode("Option", "", nodes);
		}

		static PersistedOption RestoreOption(MiniYamlNode node)
		{
			var values = ReadUniqueValues(node.Value.Nodes.Where(child => child.Key != "Target"),
				"TargetValueFilter option");
			var cell = Read<CPos>(values, "StrategicCell");
			var targets = node.Value.Nodes.Where(child => child.Key == "Target")
				.Select(child => RestoreTarget(child, cell));
			return new PersistedOption(new StealthTargetOption(cell,
				Read<int?>(values, "EstimatedTravelMilliseconds"),
				Read<bool>(values, "IsIncumbent"), targets),
				ReadNonnegativeLong(values, "StrategicValue"), Read<bool>(values, "Retained"));
		}

		static StealthStrategicTargetSnapshot RestoreTarget(MiniYamlNode node, CPos cell)
		{
			var values = ReadUniqueValues(node.Value.Nodes, "TargetValueFilter target");
			return new StealthStrategicTargetSnapshot(Read<uint>(values, "StableActorId"), cell,
				Read<int>(values, "ConfiguredPriority"), Read<int>(values, "ActorValue"),
				Read<int>(values, "HitPoints"), Read<int>(values, "MaximumHitPoints"));
		}

		void ValidateOwnedResult(StealthTargetValueFilterResult result)
		{
			if (result == null)
				throw new ArgumentNullException(nameof(result));
			if (result.Handoff.Owner != handoff.Owner || result.Handoff.Epoch != handoff.Epoch)
				throw new ArgumentException(
					"The TargetValueFilter result belongs to another ownership epoch.", nameof(result));

			var expected = BuildResult(handoff.Options);
			if (!result.IsReadyForThreatFilter || result.Options.Count != expected.Options.Count ||
				!result.Options.Zip(expected.Options, SameValueOption).All(equal => equal))
				throw new InvalidOperationException("Invalid normalized TargetValueFilter private state.");
		}

		static bool SameValueOption(StealthTargetValueOption left, StealthTargetValueOption right)
		{
			return left.StrategicValue == right.StrategicValue && left.StableIdentity == right.StableIdentity &&
				left.StrategicCell == right.StrategicCell && left.IsIncumbent == right.IsIncumbent &&
				left.EstimatedTravelMilliseconds == right.EstimatedTravelMilliseconds &&
				SameTargets(left.StrategicTargets, right.StrategicTargets);
		}

		static bool SameOption(StealthTargetOption left, StealthTargetOption right)
		{
			return left.StrategicCell == right.StrategicCell && left.IsIncumbent == right.IsIncumbent &&
				left.EstimatedTravelMilliseconds == right.EstimatedTravelMilliseconds &&
				SameTargets(left.StrategicTargets, right.StrategicTargets);
		}

		static bool SameTargets(IReadOnlyList<StealthStrategicTargetSnapshot> left,
			IReadOnlyList<StealthStrategicTargetSnapshot> right)
		{
			return left.Count == right.Count && left.Zip(right, (a, b) =>
				a.StableActorId == b.StableActorId && a.StrategicCell == b.StrategicCell &&
				a.ConfiguredPriority == b.ConfiguredPriority && a.ActorValue == b.ActorValue &&
				a.HitPoints == b.HitPoints && a.MaximumHitPoints == b.MaximumHitPoints).All(equal => equal);
		}

		static Dictionary<string, string> ReadUniqueValues(IEnumerable<MiniYamlNode> nodes, string context)
		{
			var values = new Dictionary<string, string>(StringComparer.Ordinal);
			try
			{
				foreach (var child in nodes)
					values.Add(child.Key, child.Value.Value);
			}
			catch (ArgumentException ex)
			{
				throw new InvalidOperationException("Duplicate " + context + " field.", ex);
			}

			return values;
		}

		static T Read<T>(Dictionary<string, string> values, string key)
		{
			if (!values.TryGetValue(key, out var text))
				throw new InvalidOperationException("Missing TargetValueFilter private state field: " + key);
			return FieldLoader.GetValue<T>(key, text);
		}

		static long ReadNonnegativeLong(Dictionary<string, string> values, string key)
		{
			if (!TryReadLong(values, key, out var value) || value < 0)
				throw new InvalidOperationException("Invalid TargetValueFilter private state field: " + key);
			return value;
		}

		static bool TryReadInt(Dictionary<string, string> values, string key, out int value)
		{
			value = 0;
			return values.TryGetValue(key, out var text) &&
				int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}

		static bool TryReadLong(Dictionary<string, string> values, string key, out long value)
		{
			value = 0;
			return values.TryGetValue(key, out var text) &&
				long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}
	}
}
