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
	/// Disabled TargetThreatFilter owner. It ranks only immutable Step 4A options using the
	/// standard combat adapter; routing, distance, live combat, and orders belong elsewhere.
	/// </summary>
	public sealed class StealthTargetThreatFilterBehavior
	{
		const int PrivateSaveVersion = 1;

		sealed class PersistedOption
		{
			public StealthTargetValueOption Option { get; }
			public StealthTargetThreatScore Score { get; }
			public bool Retained { get; }

			public PersistedOption(StealthTargetValueOption option,
				StealthTargetThreatScore score, bool retained)
			{
				Option = option;
				Score = score;
				Retained = retained;
			}
		}

		readonly StealthTargetThreatFilterHandoff handoff;
		readonly IStealthTargetThreatAdapter adapter;

		public StealthTargetThreatFilterBehavior(StealthTargetThreatFilterHandoff handoff,
			IStealthTargetThreatAdapter adapter)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetThreatFilter)
				throw new ArgumentException(
					"The TargetThreatFilter behavior requires TargetThreatFilter ownership.", nameof(handoff));

			this.adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
		}

		public StealthTargetThreatFilterResult Execute()
		{
			return BuildResult();
		}

		public MiniYamlNode SerializePrivateState(StealthTargetThreatFilterResult result,
			string key = "TargetThreatFilter")
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
				nodes.Add(SerializeOption(option, adapter.Calculate(option.ThreatFacts),
					retainedCells.Contains(option.StrategicCell)));

			return new MiniYamlNode(key, "", nodes);
		}

		public StealthTargetThreatFilterResult RestorePrivateState(MiniYamlNode node)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));

			var values = ReadUniqueValues(node.Value.Nodes.Where(child => child.Key != "Option"),
				"TargetThreatFilter private state");
			if (!TryReadInt(values, "Version", out var version) || version != PrivateSaveVersion)
				throw new InvalidOperationException("Unsupported stealth TargetThreatFilter private save schema.");
			if (!values.TryGetValue("Owner", out var ownerText) ||
				!Enum.TryParse(ownerText, out BehaviorId owner) || owner != BehaviorId.TargetThreatFilter)
				throw new InvalidOperationException("Invalid stealth TargetThreatFilter owner in private save state.");
			if (!TryReadLong(values, "Epoch", out var epoch) || epoch <= 0 ||
				owner != handoff.Owner || epoch != handoff.Epoch.Value)
				throw new InvalidOperationException("Stale stealth TargetThreatFilter ownership in private save state.");

			var persisted = node.Value.Nodes.Where(child => child.Key == "Option")
				.Select(RestoreOption).ToArray();
			if (persisted.Length != handoff.Options.Count ||
				!persisted.Select(option => option.Option).Zip(handoff.Options, SameValueOption).All(equal => equal))
				throw new InvalidOperationException(
					"TargetThreatFilter private state does not match its immutable input handoff.");

			var result = BuildResult();
			var expectedRetained = result.Options.Select(option => option.StrategicCell).ToHashSet();
			if (persisted.Any(option => !SameScore(option.Score, adapter.Calculate(option.Option.ThreatFacts)) ||
				option.Retained != expectedRetained.Contains(option.Option.StrategicCell)))
				throw new InvalidOperationException("Invalid normalized TargetThreatFilter private state.");

			return result;
		}

		internal static StealthTargetThreatFilterHandoff RestoreHandoff(
			StealthBehaviorHandoff handoff, MiniYamlNode node)
		{
			if (handoff == null || handoff.Owner != BehaviorId.TargetThreatFilter || node == null)
				throw new ArgumentException("TargetThreatFilter restore requires its exact active handoff.");
			var options = node.Value.Nodes.Where(child => child.Key == "Option")
				.Select(RestoreOption).Select(saved => saved.Option).ToArray();
			return new StealthTargetThreatFilterHandoff(handoff, options);
		}

		StealthTargetThreatFilterResult BuildResult()
		{
			var scored = handoff.Options.Select(option => new StealthTargetThreatOption(
				option, adapter.Calculate(option.ThreatFacts))).ToArray();
			var retainCount = (scored.Length + 1) / 2;
			var retained = scored.OrderBy(option => option.ThreatRating)
				.ThenBy(option => option.Crossover)
				.ThenBy(option => option.StableIdentity)
				.ThenBy(option => option.StrategicCell.Y)
				.ThenBy(option => option.StrategicCell.X)
				.Take(retainCount).ToArray();

			return new StealthTargetThreatFilterResult(handoff.Handoff, retained, true);
		}

		static MiniYamlNode SerializeOption(StealthTargetValueOption option,
			StealthTargetThreatScore score, bool retained)
		{
			var facts = option.ThreatFacts;
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("StrategicCell", FieldSaver.FormatValue(option.StrategicCell)),
				new MiniYamlNode("EstimatedTravelMilliseconds",
					FieldSaver.FormatValue(option.EstimatedTravelMilliseconds)),
				new MiniYamlNode("IsIncumbent", FieldSaver.FormatValue(option.IsIncumbent)),
				new MiniYamlNode("StrategicValue", option.StrategicValue.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("FormationCloaked", FieldSaver.FormatValue(facts.FormationCloaked)),
				new MiniYamlNode("HasDetectorCoverage", FieldSaver.FormatValue(facts.HasDetectorCoverage)),
				new MiniYamlNode("PlannedActionRevealsFormation",
					FieldSaver.FormatValue(facts.PlannedActionRevealsFormation)),
				new MiniYamlNode("ThreatRating", FormatDouble(score.ThreatRating)),
				new MiniYamlNode("Crossover", FormatDouble(score.Crossover)),
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

			foreach (var member in facts.FriendlyGroup)
				nodes.Add(SerializeGroupMember("Friendly", member));
			foreach (var member in facts.EnemyGroup)
				nodes.Add(SerializeGroupMember("Enemy", member));

			return new MiniYamlNode("Option", "", nodes);
		}

		static MiniYamlNode SerializeGroupMember(string key, StealthCombatGroupSnapshot member)
		{
			return new MiniYamlNode(key, "", new List<MiniYamlNode>
			{
				new MiniYamlNode("ActorType", member.ActorType),
				new MiniYamlNode("Count", FieldSaver.FormatValue(member.Count)),
				new MiniYamlNode("EconomicValue", FieldSaver.FormatValue(member.EconomicValue))
			});
		}

		static PersistedOption RestoreOption(MiniYamlNode node)
		{
			var values = ReadUniqueValues(node.Value.Nodes.Where(child =>
				child.Key != "Target" && child.Key != "Friendly" && child.Key != "Enemy"),
				"TargetThreatFilter option");
			var cell = Read<CPos>(values, "StrategicCell");
			var targets = node.Value.Nodes.Where(child => child.Key == "Target")
				.Select(child => RestoreTarget(child, cell));
			var facts = new StealthTargetThreatFacts(cell,
				node.Value.Nodes.Where(child => child.Key == "Friendly").Select(RestoreGroupMember),
				node.Value.Nodes.Where(child => child.Key == "Enemy").Select(RestoreGroupMember),
				Read<bool>(values, "FormationCloaked"), Read<bool>(values, "HasDetectorCoverage"),
				Read<bool>(values, "PlannedActionRevealsFormation"));
			var source = new StealthTargetOption(cell,
				Read<int?>(values, "EstimatedTravelMilliseconds"), Read<bool>(values, "IsIncumbent"),
				targets, facts);
			var option = new StealthTargetValueOption(source, ReadNonnegativeLong(values, "StrategicValue"));
			return new PersistedOption(option, new StealthTargetThreatScore(
				ReadNonnegativeDouble(values, "ThreatRating", false),
				ReadNonnegativeDouble(values, "Crossover", true)), Read<bool>(values, "Retained"));
		}

		static StealthStrategicTargetSnapshot RestoreTarget(MiniYamlNode node, CPos cell)
		{
			var values = ReadUniqueValues(node.Value.Nodes, "TargetThreatFilter target");
			return new StealthStrategicTargetSnapshot(Read<uint>(values, "StableActorId"), cell,
				Read<int>(values, "ConfiguredPriority"), Read<int>(values, "ActorValue"),
				Read<int>(values, "HitPoints"), Read<int>(values, "MaximumHitPoints"));
		}

		static StealthCombatGroupSnapshot RestoreGroupMember(MiniYamlNode node)
		{
			var values = ReadUniqueValues(node.Value.Nodes, "TargetThreatFilter combat group member");
			if (!values.TryGetValue("ActorType", out var actorType))
				throw new InvalidOperationException("Missing TargetThreatFilter private state field: ActorType");
			return new StealthCombatGroupSnapshot(actorType,
				Read<int>(values, "Count"), Read<int>(values, "EconomicValue"));
		}

		void ValidateOwnedResult(StealthTargetThreatFilterResult result)
		{
			if (result == null)
				throw new ArgumentNullException(nameof(result));
			if (result.Handoff.Owner != handoff.Owner || result.Handoff.Epoch != handoff.Epoch)
				throw new ArgumentException(
					"The TargetThreatFilter result belongs to another ownership epoch.", nameof(result));

			var expected = BuildResult();
			if (!result.IsReadyForDistanceChoice || result.Options.Count != expected.Options.Count ||
				!result.Options.Zip(expected.Options, SameThreatOption).All(equal => equal))
				throw new InvalidOperationException("Invalid normalized TargetThreatFilter private state.");
		}

		static bool SameThreatOption(StealthTargetThreatOption left, StealthTargetThreatOption right)
		{
			return SameValueOption(left.ValueOption, right.ValueOption) &&
				left.ThreatRating.Equals(right.ThreatRating) && left.Crossover.Equals(right.Crossover);
		}

		static bool SameScore(StealthTargetThreatScore left, StealthTargetThreatScore right)
		{
			return left.ThreatRating.Equals(right.ThreatRating) && left.Crossover.Equals(right.Crossover);
		}

		static bool SameValueOption(StealthTargetValueOption left, StealthTargetValueOption right)
		{
			return left.StrategicValue == right.StrategicValue && left.StableIdentity == right.StableIdentity &&
				left.StrategicCell == right.StrategicCell && left.IsIncumbent == right.IsIncumbent &&
				left.EstimatedTravelMilliseconds == right.EstimatedTravelMilliseconds &&
				SameTargets(left.StrategicTargets, right.StrategicTargets) &&
				SameFacts(left.ThreatFacts, right.ThreatFacts);
		}

		static bool SameTargets(IReadOnlyList<StealthStrategicTargetSnapshot> left,
			IReadOnlyList<StealthStrategicTargetSnapshot> right)
		{
			return left.Count == right.Count && left.Zip(right, (a, b) =>
				a.StableActorId == b.StableActorId && a.StrategicCell == b.StrategicCell &&
				a.ConfiguredPriority == b.ConfiguredPriority && a.ActorValue == b.ActorValue &&
				a.HitPoints == b.HitPoints && a.MaximumHitPoints == b.MaximumHitPoints).All(equal => equal);
		}

		static bool SameFacts(StealthTargetThreatFacts left, StealthTargetThreatFacts right)
		{
			return left.StrategicCell == right.StrategicCell && left.FormationCloaked == right.FormationCloaked &&
				left.HasDetectorCoverage == right.HasDetectorCoverage &&
				left.PlannedActionRevealsFormation == right.PlannedActionRevealsFormation &&
				SameGroup(left.FriendlyGroup, right.FriendlyGroup) && SameGroup(left.EnemyGroup, right.EnemyGroup);
		}

		static bool SameGroup(IReadOnlyList<StealthCombatGroupSnapshot> left,
			IReadOnlyList<StealthCombatGroupSnapshot> right)
		{
			return left.Count == right.Count && left.Zip(right, (a, b) =>
				a.ActorType == b.ActorType && a.Count == b.Count &&
				a.EconomicValue == b.EconomicValue).All(equal => equal);
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
				throw new InvalidOperationException("Missing TargetThreatFilter private state field: " + key);
			return FieldLoader.GetValue<T>(key, text);
		}

		static long ReadNonnegativeLong(Dictionary<string, string> values, string key)
		{
			if (!TryReadLong(values, key, out var value) || value < 0)
				throw new InvalidOperationException("Invalid TargetThreatFilter private state field: " + key);
			return value;
		}

		static double ReadNonnegativeDouble(Dictionary<string, string> values, string key, bool allowInfinity)
		{
			if (!values.TryGetValue(key, out var text) ||
				!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
				double.IsNaN(value) || value < 0 || (!allowInfinity && double.IsInfinity(value)))
				throw new InvalidOperationException("Invalid TargetThreatFilter private state field: " + key);
			return value;
		}

		static string FormatDouble(double value)
		{
			return value.ToString("R", CultureInfo.InvariantCulture);
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
