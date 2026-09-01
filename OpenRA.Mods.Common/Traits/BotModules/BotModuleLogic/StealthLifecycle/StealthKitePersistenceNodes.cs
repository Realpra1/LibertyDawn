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
	static class StealthKitePersistenceNodes
	{
		public static MiniYamlNode SerializeFacts(string key, StealthKiteThreatFacts facts)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Action", facts.Action.ToString()),
				new MiniYamlNode("TargetId", FieldSaver.FormatValue(facts.SelectedTargetActorId)),
				new MiniYamlNode("TargetCell", FieldSaver.FormatValue(facts.SelectedTargetCurrentCell)),
				new MiniYamlNode("PlannedCell", FieldSaver.FormatValue(facts.PlannedCell)),
				new MiniYamlNode("FriendlyRange", FieldSaver.FormatValue(facts.FriendlyCurrentFiringRangeCells)),
				new MiniYamlNode("FormationCloaked", FieldSaver.FormatValue(facts.FormationCloaked)),
				new MiniYamlNode("PlannedDecloak", FieldSaver.FormatValue(facts.PlannedDecloak)),
				new MiniYamlNode("PlannedAttack", FieldSaver.FormatValue(facts.PlannedAttack))
			};
			AddIds(nodes, "FriendlyId", facts.FriendlyActorIds);
			foreach (var enemy in facts.Enemies)
				nodes.Add(SerializeEnemy(enemy));
			return Node(key, nodes);
		}

		public static StealthKiteThreatFacts RestoreFacts(MiniYamlNode node)
		{
			var values = UniqueScalars(node, "FriendlyId", "Enemy");
			if (values.Count != 8)
				throw new InvalidOperationException("Invalid Kite threat facts field set.");
			return new StealthKiteThreatFacts(Read<StealthKiteAction>(values, "Action"),
				Read<uint>(values, "TargetId"), Read<CPos>(values, "TargetCell"),
				Read<CPos>(values, "PlannedCell"), Read<int>(values, "FriendlyRange"),
				ReadIds(node, "FriendlyId"), node.Value.Nodes.Where(child => child.Key == "Enemy")
					.Select(RestoreEnemy), Read<bool>(values, "FormationCloaked"),
				Read<bool>(values, "PlannedDecloak"), Read<bool>(values, "PlannedAttack"));
		}

		public static MiniYamlNode SerializeSafety(string key, StealthKiteSafetyResult safety)
		{
			return Node(key, new[]
			{
				new MiniYamlNode("Threat", Format(safety.Score.ThreatRating)),
				new MiniYamlNode("Crossover", Format(safety.Score.Crossover)),
				new MiniYamlNode("Approved", FieldSaver.FormatValue(safety.Approved))
			});
		}

		public static StealthKiteSafetyResult RestoreSafety(MiniYamlNode node)
		{
			var values = Unique(node.Value.Nodes);
			if (values.Count != 3)
				throw new InvalidOperationException("Invalid Kite safety field set.");
			return new StealthKiteSafetyResult(new StealthTargetThreatScore(
				ReadDouble(values, "Threat", false), ReadDouble(values, "Crossover", true)),
				Read<bool>(values, "Approved"));
		}

		public static MiniYamlNode SerializeFallbackFacts(StealthKiteFallbackFacts facts)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("TargetId", FieldSaver.FormatValue(facts.SelectedTargetActorId)),
				new MiniYamlNode("TargetCell", FieldSaver.FormatValue(facts.SelectedTargetCurrentCell)),
				new MiniYamlNode("FormationCloaked", FieldSaver.FormatValue(facts.FormationCloaked)),
				new MiniYamlNode("PlannedDecloak", "True"),
				new MiniYamlNode("PlannedAttack", "True")
			};
			AddIds(nodes, "FriendlyId", facts.FriendlyActorIds);
			AddIds(nodes, "EnemyId", facts.EnemyActorIds);
			return Node("AttackFacts", nodes);
		}

		public static StealthKiteFallbackFacts RestoreFallbackFacts(MiniYamlNode node)
		{
			var values = UniqueScalars(node, "FriendlyId", "EnemyId");
			if (values.Count != 5 || !Read<bool>(values, "PlannedDecloak") ||
				!Read<bool>(values, "PlannedAttack"))
				throw new InvalidOperationException("Invalid Kite fallback attack facts.");
			return new StealthKiteFallbackFacts(Read<uint>(values, "TargetId"),
				Read<CPos>(values, "TargetCell"), ReadIds(node, "FriendlyId"),
				ReadIds(node, "EnemyId"), Read<bool>(values, "FormationCloaked"));
		}

		public static MiniYamlNode SerializeOrder(StealthKiteOrderToken token)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Owner", token.Owner.ToString()),
				new MiniYamlNode("Epoch", FieldSaver.FormatValue(token.Epoch.Value)),
				new MiniYamlNode("Action", token.Action.ToString()),
				new MiniYamlNode("HasTarget", FieldSaver.FormatValue(token.TargetActorId.HasValue)),
				new MiniYamlNode("TargetId", FieldSaver.FormatValue(token.TargetActorId ?? 0)),
				new MiniYamlNode("Cell", FieldSaver.FormatValue(token.Cell)),
				new MiniYamlNode("PhaseRevision", FieldSaver.FormatValue(token.PhaseRevision)),
				new MiniYamlNode("ActivityRevision", FieldSaver.FormatValue(token.ActivityRevision))
			};
			AddIds(nodes, "ActorId", token.ActorIds);
			return Node("LastOrder", nodes);
		}

		public static StealthKiteOrderToken RestoreOrder(MiniYamlNode node)
		{
			var values = UniqueScalars(node, "ActorId");
			if (values.Count != 8 || Read<BehaviorId>(values, "Owner") != BehaviorId.Kite)
				throw new InvalidOperationException("Invalid Kite order token field set.");
			var hasTarget = Read<bool>(values, "HasTarget");
			var target = Read<uint>(values, "TargetId");
			if (!hasTarget && target != 0)
				throw new InvalidOperationException("Noncanonical Kite order target.");
			try
			{
				return new StealthKiteOrderToken(BehaviorId.Kite,
					new OwnershipEpoch(Read<long>(values, "Epoch")),
					Read<StealthKiteAction>(values, "Action"), ReadIds(node, "ActorId"),
					hasTarget ? target : (uint?)null, Read<CPos>(values, "Cell"),
					Read<long>(values, "PhaseRevision"), Read<long>(values, "ActivityRevision"));
			}
			catch (ArgumentException ex)
			{
				throw new InvalidOperationException("Invalid Kite order token.", ex);
			}
		}

		static MiniYamlNode SerializeEnemy(StealthKiteActorSnapshot actor)
		{
			return Node("Enemy", new[]
			{
				new MiniYamlNode("Id", FieldSaver.FormatValue(actor.ActorId)),
				new MiniYamlNode("Type", actor.ActorType),
				new MiniYamlNode("Cell", FieldSaver.FormatValue(actor.CurrentCell)),
				new MiniYamlNode("HP", FieldSaver.FormatValue(actor.HitPoints)),
				new MiniYamlNode("MaxHP", FieldSaver.FormatValue(actor.MaximumHitPoints)),
				new MiniYamlNode("Range", FieldSaver.FormatValue(actor.CurrentWeaponRangeCells)),
				new MiniYamlNode("Defender", FieldSaver.FormatValue(actor.IsDefender)),
				new MiniYamlNode("Objective", FieldSaver.FormatValue(actor.IsMissionObjective)),
				new MiniYamlNode("Infantry", FieldSaver.FormatValue(actor.IsInfantry)),
				new MiniYamlNode("Crushable", FieldSaver.FormatValue(actor.CanBeCrushedByFormation)),
				new MiniYamlNode("Detector", FieldSaver.FormatValue(actor.HasDetectorCoverage)),
				new MiniYamlNode("Local", FieldSaver.FormatValue(actor.IsInLocalEngagementArea))
			});
		}

		static StealthKiteActorSnapshot RestoreEnemy(MiniYamlNode node)
		{
			var values = Unique(node.Value.Nodes);
			if (values.Count != 12)
				throw new InvalidOperationException("Invalid Kite enemy facts field set.");
			return new StealthKiteActorSnapshot(Read<uint>(values, "Id"), values["Type"],
				Read<CPos>(values, "Cell"), Read<int>(values, "HP"), Read<int>(values, "MaxHP"),
				Read<int>(values, "Range"), Read<bool>(values, "Defender"),
				Read<bool>(values, "Objective"), Read<bool>(values, "Infantry"),
				Read<bool>(values, "Crushable"), Read<bool>(values, "Detector"),
				Read<bool>(values, "Local"));
		}

		static MiniYamlNode Node(string key, IEnumerable<MiniYamlNode> nodes)
		{
			return new MiniYamlNode(key, new MiniYaml("", nodes.ToList()));
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

		static Dictionary<string, string> UniqueScalars(MiniYamlNode node, params string[] repeated)
		{
			var excluded = new HashSet<string>(repeated, StringComparer.Ordinal);
			return Unique(node.Value.Nodes.Where(child => !excluded.Contains(child.Key)));
		}

		static Dictionary<string, string> Unique(IEnumerable<MiniYamlNode> nodes)
		{
			var result = new Dictionary<string, string>(StringComparer.Ordinal);
			try { foreach (var node in nodes) result.Add(node.Key, node.Value.Value); }
			catch (ArgumentException ex) { throw new InvalidOperationException("Duplicate Kite state field.", ex); }
			return result;
		}

		static T Read<T>(Dictionary<string, string> values, string key)
		{
			if (!values.TryGetValue(key, out var value))
				throw new InvalidOperationException("Missing Kite state field: " + key);
			if (typeof(T).IsEnum)
			{
				if (!Enum.TryParse(typeof(T), value, out var parsed) || !Enum.IsDefined(typeof(T), parsed))
					throw new InvalidOperationException("Invalid Kite enum field: " + key);
				return (T)parsed;
			}

			return FieldLoader.GetValue<T>(key, value);
		}

		static double ReadDouble(Dictionary<string, string> values, string key, bool allowInfinity)
		{
			if (!values.TryGetValue(key, out var text) || !double.TryParse(text, NumberStyles.Float,
				CultureInfo.InvariantCulture, out var value) || double.IsNaN(value) || value < 0 ||
				(!allowInfinity && double.IsInfinity(value)))
				throw new InvalidOperationException("Invalid Kite threat value.");
			return value;
		}

		static string Format(double value) { return value.ToString("R", CultureInfo.InvariantCulture); }
	}
}
