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
	/// Disabled TargetDistanceChoice owner. It chooses one Step 4B survivor using only cached
	/// strategic travel and peer-cell facts; detailed routes, live combat, and orders belong elsewhere.
	/// </summary>
	public sealed class StealthTargetDistanceChoiceBehavior
	{
		const int PrivateSaveVersion = 1;
		sealed class DistanceFact
		{
			public StealthTargetThreatOption Option { get; }
			public long MinimumSeparationSquared { get; }
			public int SeparationCreditMilliseconds { get; }
			public long AdjustedTravelCostMilliseconds { get; }

			public DistanceFact(StealthTargetThreatOption option, long minimumSeparationSquared,
				int separationCreditMilliseconds, long adjustedTravelCostMilliseconds)
			{
				Option = option;
				MinimumSeparationSquared = minimumSeparationSquared;
				SeparationCreditMilliseconds = separationCreditMilliseconds;
				AdjustedTravelCostMilliseconds = adjustedTravelCostMilliseconds;
			}
		}

		sealed class PersistedFact
		{
			public DistanceFact Fact { get; }
			public bool Selected { get; }

			public PersistedFact(DistanceFact fact, bool selected)
			{
				Fact = fact;
				Selected = selected;
			}
		}

		readonly StealthTargetDistanceChoiceHandoff handoff;
		readonly StealthActiveSquadTargetSnapshot[] otherActiveSquads;
		readonly StealthTargetDistanceChoicePolicy policy;
		public StealthTargetDistanceChoiceBehavior(StealthTargetDistanceChoiceHandoff handoff,
			IEnumerable<StealthActiveSquadTargetSnapshot> otherActiveSquads,
			StealthTargetDistanceChoicePolicy policy)
		{
			this.handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetDistanceChoice)
				throw new ArgumentException(
					"The TargetDistanceChoice behavior requires TargetDistanceChoice ownership.", nameof(handoff));
			if (handoff.Options.Count == 0)
				throw new ArgumentException("TargetDistanceChoice requires at least one Step 4B option.", nameof(handoff));
			if (otherActiveSquads == null)
				throw new ArgumentNullException(nameof(otherActiveSquads));
			this.policy = policy ?? throw new ArgumentNullException(nameof(policy));

			this.otherActiveSquads = otherActiveSquads.OrderBy(squad => squad?.StableActorId)
				.ThenBy(squad => squad?.StrategicCell.Y).ThenBy(squad => squad?.StrategicCell.X).ToArray();
			if (this.otherActiveSquads.Any(squad => squad == null) || this.otherActiveSquads
				.Select(squad => squad.StableActorId).Distinct().Count() != this.otherActiveSquads.Length)
				throw new ArgumentException(
					"Active stealth squad snapshots must have unique stable actor identities.",
					nameof(otherActiveSquads));
		}

		public StealthTargetDistanceChoiceResult Execute()
		{
			return BuildResult();
		}

		public MiniYamlNode SerializePrivateState(StealthTargetDistanceChoiceResult result,
			string key = "TargetDistanceChoice")
		{
			ValidateOwnedResult(result);
			var facts = BuildFacts();
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", PrivateSaveVersion.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Owner", result.Handoff.Owner.ToString()),
				new MiniYamlNode("Epoch", result.Handoff.Epoch.Value.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("SeparationCreditPerSquaredCellMilliseconds",
					policy.SeparationCreditPerSquaredCellMilliseconds.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("MaximumSeparationCreditMilliseconds",
					policy.MaximumSeparationCreditMilliseconds.ToString(CultureInfo.InvariantCulture))
			};

			foreach (var squad in otherActiveSquads)
				nodes.Add(new MiniYamlNode("OtherActiveSquad", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("StableActorId", FieldSaver.FormatValue(squad.StableActorId)),
					new MiniYamlNode("StrategicCell", FieldSaver.FormatValue(squad.StrategicCell))
				}));
			foreach (var fact in facts)
				nodes.Add(SerializeFact(fact,
					fact.Option.StrategicCell == result.Mission.StrategicCell &&
					fact.Option.StableIdentity == result.Mission.StableTargetActorId));

			return new MiniYamlNode(key, "", nodes);
		}

		public StealthTargetDistanceChoiceResult RestorePrivateState(MiniYamlNode node)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));
			var values = ReadUniqueValues(node.Value.Nodes.Where(child =>
				child.Key != "OtherActiveSquad" && child.Key != "Option"),
				"TargetDistanceChoice private state");
			if (!TryReadInt(values, "Version", out var version) || version != PrivateSaveVersion)
				throw new InvalidOperationException("Unsupported stealth TargetDistanceChoice private save schema.");
			if (!values.TryGetValue("Owner", out var ownerText) ||
				!Enum.TryParse(ownerText, out BehaviorId owner) || owner != BehaviorId.TargetDistanceChoice)
				throw new InvalidOperationException("Invalid stealth TargetDistanceChoice owner in private save state.");
			if (!TryReadLong(values, "Epoch", out var epoch) || epoch <= 0 ||
				owner != handoff.Owner || epoch != handoff.Epoch.Value)
				throw new InvalidOperationException("Stale stealth TargetDistanceChoice ownership in private save state.");
			if (!TryReadInt(values, "SeparationCreditPerSquaredCellMilliseconds", out var perCellCredit) ||
				!TryReadInt(values, "MaximumSeparationCreditMilliseconds", out var maximumCredit) ||
				perCellCredit != policy.SeparationCreditPerSquaredCellMilliseconds ||
				maximumCredit != policy.MaximumSeparationCreditMilliseconds)
				throw new InvalidOperationException(
					"TargetDistanceChoice private state does not match its immutable distance policy.");

			var restoredSquads = node.Value.Nodes.Where(child => child.Key == "OtherActiveSquad")
				.Select(RestoreSquad).OrderBy(squad => squad.StableActorId)
				.ThenBy(squad => squad.StrategicCell.Y).ThenBy(squad => squad.StrategicCell.X).ToArray();
			if (restoredSquads.Length != otherActiveSquads.Length ||
				!restoredSquads.Zip(otherActiveSquads, SameSquad).All(equal => equal))
				throw new InvalidOperationException(
					"TargetDistanceChoice private state does not match its immutable squad inputs.");

			var persisted = node.Value.Nodes.Where(child => child.Key == "Option")
				.Select(RestoreFact).ToArray();
			var expected = BuildFacts();
			var selected = Select(expected);
			if (persisted.Length != expected.Length || persisted.Zip(expected, (saved, fact) =>
				SameThreatOption(saved.Fact.Option, fact.Option) &&
				saved.Fact.MinimumSeparationSquared == fact.MinimumSeparationSquared &&
				saved.Fact.SeparationCreditMilliseconds == fact.SeparationCreditMilliseconds &&
				saved.Fact.AdjustedTravelCostMilliseconds == fact.AdjustedTravelCostMilliseconds &&
				saved.Selected == ReferenceEquals(fact, selected)).Any(equal => !equal))
				throw new InvalidOperationException("Invalid normalized TargetDistanceChoice private state.");

			return BuildResult(expected, selected);
		}

		internal static StealthTargetDistanceChoiceHandoff RestoreHandoff(
			StealthBehaviorHandoff handoff, MiniYamlNode node)
		{
			if (handoff == null || handoff.Owner != BehaviorId.TargetDistanceChoice || node == null)
				throw new ArgumentException("TargetDistanceChoice restore requires its exact active handoff.");
			var options = node.Value.Nodes.Where(child => child.Key == "Option")
				.Select(RestoreFact).Select(saved => saved.Fact.Option).ToArray();
			return new StealthTargetDistanceChoiceHandoff(handoff, options);
		}

		DistanceFact[] BuildFacts()
		{
			var peerCells = otherActiveSquads.Select(squad => squad.StrategicCell).ToArray();
			return handoff.Options.Select(option =>
			{
				var separation = StealthAIThreatGeometry.MinimumCellSeparationSquared(
					option.StrategicCell, peerCells);
				var travel = option.ValueOption.EstimatedTravelMilliseconds;
				var credit = separation == long.MaxValue || travel == null ? 0 : (int)Math.Min(
					policy.MaximumSeparationCreditMilliseconds,
					Math.Min(separation, int.MaxValue) * policy.SeparationCreditPerSquaredCellMilliseconds);
				var adjusted = travel == null ? long.MaxValue : Math.Max(0L, travel.Value - (long)credit);
				return new DistanceFact(option, separation, credit, adjusted);
			}).ToArray();
		}

		StealthTargetDistanceChoiceResult BuildResult()
		{
			var facts = BuildFacts();
			return BuildResult(facts, Select(facts));
		}

		StealthTargetDistanceChoiceResult BuildResult(DistanceFact[] facts, DistanceFact selected)
		{
			return new StealthTargetDistanceChoiceResult(handoff.Handoff,
				new StealthApproachMission(selected.Option, selected.MinimumSeparationSquared,
					selected.SeparationCreditMilliseconds, selected.AdjustedTravelCostMilliseconds));
		}

		static DistanceFact Select(IEnumerable<DistanceFact> facts)
		{
			return facts.OrderBy(fact => fact.AdjustedTravelCostMilliseconds)
				.ThenByDescending(fact => fact.MinimumSeparationSquared)
				.ThenBy(fact => fact.Option.ValueOption.EstimatedTravelMilliseconds ?? int.MaxValue)
				.ThenBy(fact => fact.Option.StableIdentity)
				.ThenBy(fact => fact.Option.StrategicCell.Y)
				.ThenBy(fact => fact.Option.StrategicCell.X).First();
		}

		static MiniYamlNode SerializeFact(DistanceFact fact, bool selected)
		{
			var option = fact.Option;
			var value = option.ValueOption;
			var facts = value.ThreatFacts;
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("StrategicCell", FieldSaver.FormatValue(option.StrategicCell)),
				new MiniYamlNode("EstimatedTravelMilliseconds", FieldSaver.FormatValue(value.EstimatedTravelMilliseconds)),
				new MiniYamlNode("IsIncumbent", FieldSaver.FormatValue(value.IsIncumbent)),
				new MiniYamlNode("StrategicValue", value.StrategicValue.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("ThreatRating", FormatDouble(option.ThreatRating)),
				new MiniYamlNode("Crossover", FormatDouble(option.Crossover)),
				new MiniYamlNode("FormationCloaked", FieldSaver.FormatValue(facts.FormationCloaked)),
				new MiniYamlNode("HasDetectorCoverage", FieldSaver.FormatValue(facts.HasDetectorCoverage)),
				new MiniYamlNode("PlannedActionRevealsFormation", FieldSaver.FormatValue(facts.PlannedActionRevealsFormation)),
				new MiniYamlNode("MinimumSquadSeparationSquared",
					fact.MinimumSeparationSquared.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("SeparationCreditMilliseconds",
					fact.SeparationCreditMilliseconds.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("AdjustedTravelCostMilliseconds",
					fact.AdjustedTravelCostMilliseconds.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Selected", FieldSaver.FormatValue(selected))
			};

			foreach (var target in value.StrategicTargets)
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

		static PersistedFact RestoreFact(MiniYamlNode node)
		{
			var values = ReadUniqueValues(node.Value.Nodes.Where(child =>
				child.Key != "Target" && child.Key != "Friendly" && child.Key != "Enemy"),
				"TargetDistanceChoice option");
			var cell = Read<CPos>(values, "StrategicCell");
			var threatFacts = new StealthTargetThreatFacts(cell,
				node.Value.Nodes.Where(child => child.Key == "Friendly").Select(RestoreGroupMember),
				node.Value.Nodes.Where(child => child.Key == "Enemy").Select(RestoreGroupMember),
				Read<bool>(values, "FormationCloaked"), Read<bool>(values, "HasDetectorCoverage"),
				Read<bool>(values, "PlannedActionRevealsFormation"));
			var source = new StealthTargetOption(cell, Read<int?>(values, "EstimatedTravelMilliseconds"),
				Read<bool>(values, "IsIncumbent"), node.Value.Nodes.Where(child => child.Key == "Target")
					.Select(child => RestoreTarget(child, cell)), threatFacts);
			var value = new StealthTargetValueOption(source, ReadNonnegativeLong(values, "StrategicValue"));
			var option = new StealthTargetThreatOption(value, new StealthTargetThreatScore(
				ReadNonnegativeDouble(values, "ThreatRating", false),
				ReadNonnegativeDouble(values, "Crossover", true)));
			return new PersistedFact(new DistanceFact(option,
				ReadNonnegativeLong(values, "MinimumSquadSeparationSquared"),
				ReadNonnegativeInt(values, "SeparationCreditMilliseconds"),
				ReadNonnegativeLong(values, "AdjustedTravelCostMilliseconds")),
				Read<bool>(values, "Selected"));
		}

		static StealthActiveSquadTargetSnapshot RestoreSquad(MiniYamlNode node)
		{
			var values = ReadUniqueValues(node.Value.Nodes, "TargetDistanceChoice active squad");
			return new StealthActiveSquadTargetSnapshot(
				Read<uint>(values, "StableActorId"), Read<CPos>(values, "StrategicCell"));
		}

		static StealthStrategicTargetSnapshot RestoreTarget(MiniYamlNode node, CPos cell)
		{
			var values = ReadUniqueValues(node.Value.Nodes, "TargetDistanceChoice target");
			return new StealthStrategicTargetSnapshot(Read<uint>(values, "StableActorId"), cell,
				Read<int>(values, "ConfiguredPriority"), Read<int>(values, "ActorValue"),
				Read<int>(values, "HitPoints"), Read<int>(values, "MaximumHitPoints"));
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

		static StealthCombatGroupSnapshot RestoreGroupMember(MiniYamlNode node)
		{
			var values = ReadUniqueValues(node.Value.Nodes, "TargetDistanceChoice combat group member");
			if (!values.TryGetValue("ActorType", out var actorType))
				throw new InvalidOperationException("Missing TargetDistanceChoice private state field: ActorType");
			return new StealthCombatGroupSnapshot(actorType,
				Read<int>(values, "Count"), Read<int>(values, "EconomicValue"));
		}

		void ValidateOwnedResult(StealthTargetDistanceChoiceResult result)
		{
			if (result == null)
				throw new ArgumentNullException(nameof(result));
			if (result.Handoff.Owner != handoff.Owner || result.Handoff.Epoch != handoff.Epoch)
				throw new ArgumentException(
					"The TargetDistanceChoice result belongs to another ownership epoch.", nameof(result));

			var expected = BuildResult();
			if (!SameThreatOption(result.Mission.TargetOption, expected.Mission.TargetOption) ||
				result.Mission.MinimumSquadSeparationSquared != expected.Mission.MinimumSquadSeparationSquared ||
				result.Mission.SeparationCreditMilliseconds != expected.Mission.SeparationCreditMilliseconds ||
				result.Mission.AdjustedTravelCostMilliseconds != expected.Mission.AdjustedTravelCostMilliseconds)
				throw new InvalidOperationException("Invalid normalized TargetDistanceChoice private state.");
		}

		static bool SameThreatOption(StealthTargetThreatOption left, StealthTargetThreatOption right)
		{
			return left.ThreatRating.Equals(right.ThreatRating) && left.Crossover.Equals(right.Crossover) &&
				SameValueOption(left.ValueOption, right.ValueOption);
		}

		static bool SameValueOption(StealthTargetValueOption left, StealthTargetValueOption right)
		{
			return left.StrategicValue == right.StrategicValue && left.StableIdentity == right.StableIdentity &&
				left.StrategicCell == right.StrategicCell && left.IsIncumbent == right.IsIncumbent &&
				left.EstimatedTravelMilliseconds == right.EstimatedTravelMilliseconds &&
				left.StrategicTargets.Count == right.StrategicTargets.Count &&
				left.StrategicTargets.Zip(right.StrategicTargets, (a, b) =>
					a.StableActorId == b.StableActorId && a.StrategicCell == b.StrategicCell &&
					a.ConfiguredPriority == b.ConfiguredPriority && a.ActorValue == b.ActorValue &&
					a.HitPoints == b.HitPoints && a.MaximumHitPoints == b.MaximumHitPoints).All(equal => equal) &&
				SameFacts(left.ThreatFacts, right.ThreatFacts);
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
			return left.Count == right.Count && left.Zip(right, (a, b) => a.ActorType == b.ActorType &&
				a.Count == b.Count && a.EconomicValue == b.EconomicValue).All(equal => equal);
		}

		static bool SameSquad(StealthActiveSquadTargetSnapshot left,
			StealthActiveSquadTargetSnapshot right)
		{
			return left.StableActorId == right.StableActorId && left.StrategicCell == right.StrategicCell;
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
				throw new InvalidOperationException("Missing TargetDistanceChoice private state field: " + key);
			return FieldLoader.GetValue<T>(key, text);
		}

		static long ReadNonnegativeLong(Dictionary<string, string> values, string key)
		{
			if (!TryReadLong(values, key, out var value) || value < 0)
				throw new InvalidOperationException("Invalid TargetDistanceChoice private state field: " + key);
			return value;
		}

		static int ReadNonnegativeInt(Dictionary<string, string> values, string key)
		{
			if (!TryReadInt(values, key, out var value) || value < 0)
				throw new InvalidOperationException("Invalid TargetDistanceChoice private state field: " + key);
			return value;
		}

		static double ReadNonnegativeDouble(Dictionary<string, string> values, string key, bool allowInfinity)
		{
			if (!values.TryGetValue(key, out var text) ||
				!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
				double.IsNaN(value) || value < 0 || (!allowInfinity && double.IsInfinity(value)))
				throw new InvalidOperationException("Invalid TargetDistanceChoice private state field: " + key);
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
