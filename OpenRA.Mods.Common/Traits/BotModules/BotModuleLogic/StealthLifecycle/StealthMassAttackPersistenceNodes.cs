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
	static class StealthMassAttackPersistenceNodes
	{
		public static MiniYamlNode SerializeEntry(StealthMassAttackEntryEvidence entry)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Reason", entry.Reason.ToString()),
				new MiniYamlNode("Fingerprint", entry.LiveFingerprint),
				new MiniYamlNode("TargetId", FieldSaver.FormatValue(entry.SelectedTargetActorId)),
				new MiniYamlNode("TargetCell", FieldSaver.FormatValue(entry.SelectedTargetCurrentCell)),
				new MiniYamlNode("FormationCloaked", FieldSaver.FormatValue(entry.FormationCloaked)),
				new MiniYamlNode("PlannedReveal", "True"),
				new MiniYamlNode("PlannedAttack", "True"),
				new MiniYamlNode("FullCurrentFiringRangeExposure", "True"),
				new MiniYamlNode("Threat", Format(entry.StandardScore.ThreatRating)),
				new MiniYamlNode("Crossover", Format(entry.StandardScore.Crossover))
			};
			AddIds(nodes, "FriendlyId", entry.FriendlyActorIds);
			AddIds(nodes, "EnemyId", entry.EnemyActorIds);
			return Node("Entry", nodes);
		}

		public static StealthMassAttackEntryEvidence RestoreEntry(MiniYamlNode node)
		{
			var values = UniqueScalars(node, "FriendlyId", "EnemyId");
			if (values.Count != 10 || Read<StealthKiteFallbackReason>(values, "Reason") !=
				StealthKiteFallbackReason.NoSafePlan || !Read<bool>(values, "PlannedReveal") ||
				!Read<bool>(values, "PlannedAttack") ||
				!Read<bool>(values, "FullCurrentFiringRangeExposure"))
				throw new InvalidOperationException("Invalid MassAttack entry evidence.");
			return new StealthMassAttackEntryEvidence(values["Fingerprint"], Read<uint>(values, "TargetId"),
				Read<CPos>(values, "TargetCell"), ReadIds(node, "FriendlyId"), ReadIds(node, "EnemyId"),
				Read<bool>(values, "FormationCloaked"), new StealthTargetThreatScore(
					ReadDouble(values, "Threat", false), ReadDouble(values, "Crossover", true)));
		}

		public static bool SameEntry(StealthMassAttackEntryEvidence left,
			StealthMassAttackEntryEvidence right)
		{
			return left.LiveFingerprint == right.LiveFingerprint &&
				left.SelectedTargetActorId == right.SelectedTargetActorId &&
				left.SelectedTargetCurrentCell == right.SelectedTargetCurrentCell &&
				left.FriendlyActorIds.SequenceEqual(right.FriendlyActorIds) &&
				left.EnemyActorIds.SequenceEqual(right.EnemyActorIds) &&
				left.FormationCloaked == right.FormationCloaked &&
				left.StandardScore.ThreatRating.Equals(right.StandardScore.ThreatRating) &&
				left.StandardScore.Crossover.Equals(right.StandardScore.Crossover);
		}

		public static MiniYamlNode SerializeEvaluation(StealthMassAttackEvaluation evaluation)
		{
			return Node("Evaluation", new[]
			{
				SerializeFacts(evaluation.Facts),
				new MiniYamlNode("Threat", Format(evaluation.Threat.StandardScore.ThreatRating)),
				new MiniYamlNode("Crossover", Format(evaluation.Threat.StandardScore.Crossover)),
				new MiniYamlNode("SelectedTargetThreat", Format(evaluation.Threat.SelectedTargetThreat))
			});
		}

		public static StealthMassAttackEvaluation RestoreEvaluation(MiniYamlNode node)
		{
			var values = UniqueScalars(node, "Facts");
			if (values.Count != 3 || node.Value.Nodes.Count(child => child.Key == "Facts") != 1)
				throw new InvalidOperationException("Invalid MassAttack evaluation field set.");
			return new StealthMassAttackEvaluation(RestoreFacts(Required(node, "Facts")),
				new StealthMassAttackThreatResult(new StealthTargetThreatScore(
					ReadDouble(values, "Threat", false), ReadDouble(values, "Crossover", true)),
					ReadDouble(values, "SelectedTargetThreat", false)));
		}

		public static MiniYamlNode SerializeOrder(string key, StealthMassAttackOrderToken token)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Owner", token.Owner.ToString()),
				new MiniYamlNode("Epoch", FieldSaver.FormatValue(token.Epoch.Value)),
				new MiniYamlNode("Phase", token.Phase.ToString()),
				new MiniYamlNode("ActivityRevision", FieldSaver.FormatValue(token.ActivityRevision)),
				new MiniYamlNode("AttemptRevision", FieldSaver.FormatValue(token.AttemptRevision)),
				new MiniYamlNode("TargetId", FieldSaver.FormatValue(token.TargetActorId)),
				new MiniYamlNode("TargetCell", FieldSaver.FormatValue(token.TargetCurrentCell))
			};
			AddIds(nodes, "ActorId", token.ActorIds);
			return Node(key, nodes);
		}

		public static StealthMassAttackOrderToken RestoreOrder(MiniYamlNode node)
		{
			var values = UniqueScalars(node, "ActorId");
			if (values.Count != 7 || Read<BehaviorId>(values, "Owner") != BehaviorId.MassAttack)
				throw new InvalidOperationException("Invalid MassAttack order token.");
			try
			{
				return new StealthMassAttackOrderToken(BehaviorId.MassAttack,
					new OwnershipEpoch(Read<long>(values, "Epoch")),
					Read<StealthMassAttackPhase>(values, "Phase"),
					Read<long>(values, "ActivityRevision"),
					Read<long>(values, "AttemptRevision"), ReadIds(node, "ActorId"),
					Read<uint>(values, "TargetId"), Read<CPos>(values, "TargetCell"));
			}
			catch (ArgumentException ex)
			{
				throw new InvalidOperationException("Invalid MassAttack order token.", ex);
			}
		}

		public static bool SameFacts(StealthMassAttackThreatFacts left,
			StealthMassAttackThreatFacts right)
		{
			return left != null && right != null &&
				left.SelectedTargetActorId == right.SelectedTargetActorId &&
				left.SelectedTargetCurrentCell == right.SelectedTargetCurrentCell &&
				left.FriendlyActorIds.SequenceEqual(right.FriendlyActorIds) &&
				left.FormationCloaked == right.FormationCloaked &&
				left.HasDetectorCoverage == right.HasDetectorCoverage &&
				left.Enemies.Count == right.Enemies.Count &&
				left.Enemies.Zip(right.Enemies, SameEnemy).All(equal => equal);
		}

		static MiniYamlNode SerializeFacts(StealthMassAttackThreatFacts facts)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("TargetId", FieldSaver.FormatValue(facts.SelectedTargetActorId)),
				new MiniYamlNode("TargetCell", FieldSaver.FormatValue(facts.SelectedTargetCurrentCell)),
				new MiniYamlNode("FormationCloaked", FieldSaver.FormatValue(facts.FormationCloaked)),
				new MiniYamlNode("HasDetectorCoverage", FieldSaver.FormatValue(facts.HasDetectorCoverage)),
				new MiniYamlNode("PlannedReveal", "True"),
				new MiniYamlNode("PlannedAttack", "True"),
				new MiniYamlNode("FullCurrentFiringRangeExposure", "True")
			};
			AddIds(nodes, "FriendlyId", facts.FriendlyActorIds);
			foreach (var enemy in facts.Enemies)
				nodes.Add(SerializeEnemy(enemy));
			return Node("Facts", nodes);
		}

		static StealthMassAttackThreatFacts RestoreFacts(MiniYamlNode node)
		{
			var values = UniqueScalars(node, "FriendlyId", "Enemy");
			if (values.Count != 7 || !Read<bool>(values, "PlannedReveal") ||
				!Read<bool>(values, "PlannedAttack") ||
				!Read<bool>(values, "FullCurrentFiringRangeExposure"))
				throw new InvalidOperationException("Invalid MassAttack threat facts.");
			var facts = new StealthMassAttackThreatFacts(Read<uint>(values, "TargetId"),
				Read<CPos>(values, "TargetCell"), ReadIds(node, "FriendlyId"),
				node.Value.Nodes.Where(child => child.Key == "Enemy").Select(RestoreEnemy),
				Read<bool>(values, "FormationCloaked"));
			if (facts.HasDetectorCoverage != Read<bool>(values, "HasDetectorCoverage"))
				throw new InvalidOperationException("MassAttack detector evidence is inconsistent.");
			return facts;
		}

		static MiniYamlNode SerializeEnemy(StealthMassAttackActorSnapshot enemy)
		{
			return Node("Enemy", new[]
			{
				new MiniYamlNode("Id", FieldSaver.FormatValue(enemy.ActorId)),
				new MiniYamlNode("Type", enemy.ActorType),
				new MiniYamlNode("Cell", FieldSaver.FormatValue(enemy.CurrentCell)),
				new MiniYamlNode("HP", FieldSaver.FormatValue(enemy.HitPoints)),
				new MiniYamlNode("MaxHP", FieldSaver.FormatValue(enemy.MaximumHitPoints)),
				new MiniYamlNode("Range", FieldSaver.FormatValue(enemy.CurrentWeaponRangeCells)),
				new MiniYamlNode("Defender", FieldSaver.FormatValue(enemy.IsDefender)),
				new MiniYamlNode("Objective", FieldSaver.FormatValue(enemy.IsMissionObjective)),
				new MiniYamlNode("Detector", FieldSaver.FormatValue(enemy.HasDetectorCoverage)),
				new MiniYamlNode("Local", FieldSaver.FormatValue(enemy.IsInLocalEngagementArea))
			});
		}

		static StealthMassAttackActorSnapshot RestoreEnemy(MiniYamlNode node)
		{
			var values = Unique(node.Value.Nodes);
			if (values.Count != 10)
				throw new InvalidOperationException("Invalid MassAttack enemy facts.");
			return new StealthMassAttackActorSnapshot(Read<uint>(values, "Id"), values["Type"],
				Read<CPos>(values, "Cell"), Read<int>(values, "HP"), Read<int>(values, "MaxHP"),
				Read<int>(values, "Range"), Read<bool>(values, "Defender"),
				Read<bool>(values, "Objective"), Read<bool>(values, "Detector"),
				Read<bool>(values, "Local"));
		}

		static bool SameEnemy(StealthMassAttackActorSnapshot left, StealthMassAttackActorSnapshot right)
		{
			return left.ActorId == right.ActorId && left.ActorType == right.ActorType &&
				left.CurrentCell == right.CurrentCell && left.HitPoints == right.HitPoints &&
				left.MaximumHitPoints == right.MaximumHitPoints &&
				left.CurrentWeaponRangeCells == right.CurrentWeaponRangeCells &&
				left.IsDefender == right.IsDefender && left.IsMissionObjective == right.IsMissionObjective &&
				left.HasDetectorCoverage == right.HasDetectorCoverage &&
				left.IsInLocalEngagementArea == right.IsInLocalEngagementArea;
		}

		static MiniYamlNode Node(string key, IEnumerable<MiniYamlNode> nodes)
		{
			return new MiniYamlNode(key, new MiniYaml("", nodes.ToList()));
		}

		static MiniYamlNode Required(MiniYamlNode node, string key)
		{
			var matches = node.Value.Nodes.Where(child => child.Key == key).ToArray();
			if (matches.Length != 1)
				throw new InvalidOperationException("Missing or duplicate MassAttack node: " + key);
			return matches[0];
		}

		static Dictionary<string, string> UniqueScalars(MiniYamlNode node, params string[] repeated)
		{
			var excluded = new HashSet<string>(repeated, StringComparer.Ordinal);
			return Unique(node.Value.Nodes.Where(child => !excluded.Contains(child.Key)));
		}

		static Dictionary<string, string> Unique(IEnumerable<MiniYamlNode> nodes)
		{
			var result = new Dictionary<string, string>(StringComparer.Ordinal);
			try { foreach (var node in nodes) result.Add(node.Key, node.Value.Value); }
			catch (ArgumentException ex) { throw new InvalidOperationException("Duplicate MassAttack field.", ex); }
			return result;
		}

		static void AddIds(List<MiniYamlNode> nodes, string key, IEnumerable<uint> ids)
		{
			foreach (var id in ids)
				nodes.Add(new MiniYamlNode(key, FieldSaver.FormatValue(id)));
		}

		static uint[] ReadIds(MiniYamlNode node, string key)
		{
			return node.Value.Nodes.Where(child => child.Key == key)
				.Select(child => FieldLoader.GetValue<uint>(key, child.Value.Value)).ToArray();
		}

		static T Read<T>(Dictionary<string, string> values, string key)
		{
			if (!values.TryGetValue(key, out var value))
				throw new InvalidOperationException("Missing MassAttack field: " + key);
			if (typeof(T).IsEnum)
			{
				if (!Enum.TryParse(typeof(T), value, out var parsed) || !Enum.IsDefined(typeof(T), parsed))
					throw new InvalidOperationException("Invalid MassAttack enum field: " + key);
				return (T)parsed;
			}

			return FieldLoader.GetValue<T>(key, value);
		}

		static double ReadDouble(Dictionary<string, string> values, string key, bool allowInfinity)
		{
			if (!values.TryGetValue(key, out var text) || !double.TryParse(text, NumberStyles.Float,
				CultureInfo.InvariantCulture, out var value) || double.IsNaN(value) || value < 0 ||
				(!allowInfinity && double.IsInfinity(value)))
				throw new InvalidOperationException("Invalid MassAttack threat value.");
			return value;
		}

		static string Format(double value) { return value.ToString("R", CultureInfo.InvariantCulture); }
	}
}
