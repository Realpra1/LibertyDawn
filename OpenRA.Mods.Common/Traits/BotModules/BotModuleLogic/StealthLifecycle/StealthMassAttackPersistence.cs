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

namespace OpenRA.Mods.Common.Traits
{
	sealed class StealthMassAttackPrivateState
	{
		public StealthMassAttackEntryState EntryState { get; }
		public int LastObservedTick { get; }
		public int LastEvaluationTick { get; }
		public StealthMassAttackPhase Phase { get; }
		public StealthMassAttackDisposition Disposition { get; }
		public uint? TargetId { get; }
		public CPos? TargetCell { get; }
		public int TargetHitPoints { get; }
		public int TargetMaximumHitPoints { get; }
		public StealthMassAttackLiveFingerprint Fingerprint { get; }
		public StealthMassAttackEvaluation Evaluation { get; }
		public uint[] DefenderIds { get; }
		public uint[] ObjectiveIds { get; }
		public StealthMassAttackActivityContext Activity { get; }
		public StealthMassAttackOrderToken LastOrderToken { get; }
		public StealthMassAttackOrderToken PriorOrderToken { get; }

		public StealthMassAttackPrivateState(StealthMassAttackEntryState entryState, int lastObservedTick,
			int lastEvaluationTick, StealthMassAttackPhase phase,
			StealthMassAttackDisposition disposition, uint? targetId, CPos? targetCell,
			int targetHitPoints, int targetMaximumHitPoints,
			StealthMassAttackLiveFingerprint fingerprint, StealthMassAttackEvaluation evaluation,
			IEnumerable<uint> defenderIds, IEnumerable<uint> objectiveIds,
			StealthMassAttackActivityContext activity,
			StealthMassAttackOrderToken lastOrderToken,
			StealthMassAttackOrderToken priorOrderToken)
		{
			EntryState = entryState;
			LastObservedTick = lastObservedTick;
			LastEvaluationTick = lastEvaluationTick;
			Phase = phase;
			Disposition = disposition;
			TargetId = targetId;
			TargetCell = targetCell;
			TargetHitPoints = targetHitPoints;
			TargetMaximumHitPoints = targetMaximumHitPoints;
			Fingerprint = fingerprint;
			Evaluation = evaluation;
			DefenderIds = defenderIds?.ToArray();
			ObjectiveIds = objectiveIds?.ToArray();
			Activity = activity;
			LastOrderToken = lastOrderToken;
			PriorOrderToken = priorOrderToken;
			StealthMassAttackPersistence.Validate(this);
		}
	}

	static class StealthMassAttackPersistence
	{
		const int Version = 1;
		static readonly string[] RootScalars =
		{
			"Version", "Owner", "Epoch", "EntryState", "LastObservedTick",
			"LastEvaluationTick", "Phase", "Disposition", "HasTarget", "TargetId",
			"TargetCell", "TargetHitPoints", "TargetMaximumHitPoints", "Fingerprint",
			"HasActivityObservation", "ActivityRevision"
		};

		public static MiniYamlNode Serialize(string key, StealthMassAttackHandoff handoff,
			StealthApproachMission mission, StealthMassAttackPrivateState state)
		{
			Validate(state);
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", FieldSaver.FormatValue(Version)),
				new MiniYamlNode("Owner", handoff.Owner.ToString()),
				new MiniYamlNode("Epoch", FieldSaver.FormatValue(handoff.Epoch.Value)),
				StealthApproachPersistence.SerializeMission(mission),
				StealthMassAttackPersistenceNodes.SerializeEntry(handoff.Evidence),
				new MiniYamlNode("EntryState", state.EntryState.ToString()),
				new MiniYamlNode("LastObservedTick", FieldSaver.FormatValue(state.LastObservedTick)),
				new MiniYamlNode("LastEvaluationTick", FieldSaver.FormatValue(state.LastEvaluationTick)),
				new MiniYamlNode("Phase", state.Phase.ToString()),
				new MiniYamlNode("Disposition", state.Disposition.ToString()),
				new MiniYamlNode("HasTarget", FieldSaver.FormatValue(state.TargetId.HasValue)),
				new MiniYamlNode("TargetId", FieldSaver.FormatValue(state.TargetId ?? 0)),
				new MiniYamlNode("TargetCell", FieldSaver.FormatValue(state.TargetCell ?? default(CPos))),
				new MiniYamlNode("TargetHitPoints", FieldSaver.FormatValue(state.TargetHitPoints)),
				new MiniYamlNode("TargetMaximumHitPoints", FieldSaver.FormatValue(state.TargetMaximumHitPoints)),
				new MiniYamlNode("Fingerprint", state.Fingerprint?.Canonical ?? ""),
				new MiniYamlNode("HasActivityObservation",
					FieldSaver.FormatValue(state.Activity.HasObservation)),
				new MiniYamlNode("ActivityRevision", FieldSaver.FormatValue(state.Activity.Revision))
			};
			AddIds(nodes, "DefenderId", state.DefenderIds);
			AddIds(nodes, "ObjectiveId", state.ObjectiveIds);
			if (state.Evaluation != null)
				nodes.Add(StealthMassAttackPersistenceNodes.SerializeEvaluation(state.Evaluation));
			if (state.LastOrderToken != null)
				nodes.Add(StealthMassAttackPersistenceNodes.SerializeOrder("LastOrder", state.LastOrderToken));
			if (state.PriorOrderToken != null)
				nodes.Add(StealthMassAttackPersistenceNodes.SerializeOrder("PriorOrder", state.PriorOrderToken));
			if (state.Activity.Active != null)
				nodes.Add(StealthMassAttackPersistenceNodes.SerializeOrder("ActiveOrder", state.Activity.Active));
			if (state.Activity.Completed != null)
				nodes.Add(StealthMassAttackPersistenceNodes.SerializeOrder(
					"CompletedOrder", state.Activity.Completed));
			return Node(key, nodes);
		}

		public static StealthMassAttackPrivateState Restore(MiniYamlNode node,
			StealthMassAttackHandoff handoff, StealthApproachMission mission)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));
			var repeated = new HashSet<string>(StringComparer.Ordinal)
			{
				"Mission", "Entry", "DefenderId", "ObjectiveId", "Evaluation", "LastOrder",
				"PriorOrder", "ActiveOrder", "CompletedOrder"
			};
			var values = Unique(node.Value.Nodes.Where(child => !repeated.Contains(child.Key)));
			if (values.Count != RootScalars.Length || RootScalars.Any(key => !values.ContainsKey(key)) ||
				Read<int>(values, "Version") != Version ||
				Read<BehaviorId>(values, "Owner") != BehaviorId.MassAttack ||
				Read<long>(values, "Epoch") != handoff.Epoch.Value)
				throw new InvalidOperationException("Invalid MassAttack private save header or field set.");
			RequireCount(node, "Mission", 1);
			RequireCount(node, "Entry", 1);
			RequireOptional(node, "Evaluation");
			RequireOptional(node, "LastOrder");
			RequireOptional(node, "PriorOrder");
			RequireOptional(node, "ActiveOrder");
			RequireOptional(node, "CompletedOrder");
			if (StealthApproachPersistence.Canonical(Required(node, "Mission")) !=
				StealthApproachPersistence.Canonical(StealthApproachPersistence.SerializeMission(mission)) ||
				!StealthMassAttackPersistenceNodes.SameEntry(
					StealthMassAttackPersistenceNodes.RestoreEntry(Required(node, "Entry")), handoff.Evidence))
				throw new InvalidOperationException("MassAttack private state does not match its immutable handoff.");

			var hasTarget = Read<bool>(values, "HasTarget");
			var targetId = Read<uint>(values, "TargetId");
			var targetCell = Read<CPos>(values, "TargetCell");
			if (!hasTarget && (targetId != 0 || targetCell != default(CPos)))
				throw new InvalidOperationException("Noncanonical absent MassAttack target.");
			var fingerprint = values["Fingerprint"];
			StealthMassAttackActivityContext activity;
			try
			{
				activity = new StealthMassAttackActivityContext(
					Read<bool>(values, "HasActivityObservation"), Read<long>(values, "ActivityRevision"),
					ReadOptional(node, "ActiveOrder", StealthMassAttackPersistenceNodes.RestoreOrder),
					ReadOptional(node, "CompletedOrder", StealthMassAttackPersistenceNodes.RestoreOrder));
			}
			catch (ArgumentException ex)
			{
				throw new InvalidOperationException("Invalid saved MassAttack activity context.", ex);
			}

			var restored = new StealthMassAttackPrivateState(
				Read<StealthMassAttackEntryState>(values, "EntryState"),
				Read<int>(values, "LastObservedTick"), Read<int>(values, "LastEvaluationTick"),
				Read<StealthMassAttackPhase>(values, "Phase"),
				Read<StealthMassAttackDisposition>(values, "Disposition"),
				hasTarget ? targetId : (uint?)null, hasTarget ? targetCell : (CPos?)null,
				Read<int>(values, "TargetHitPoints"), Read<int>(values, "TargetMaximumHitPoints"),
				string.IsNullOrEmpty(fingerprint) ? null : new StealthMassAttackLiveFingerprint(fingerprint),
				ReadOptional(node, "Evaluation", StealthMassAttackPersistenceNodes.RestoreEvaluation),
				ReadIds(node, "DefenderId"),
				ReadIds(node, "ObjectiveId"), activity,
				ReadOptional(node, "LastOrder", StealthMassAttackPersistenceNodes.RestoreOrder),
				ReadOptional(node, "PriorOrder", StealthMassAttackPersistenceNodes.RestoreOrder));
			if (restored.LastOrderToken != null && restored.LastOrderToken.Epoch != handoff.Epoch)
				throw new InvalidOperationException("Saved MassAttack order token has a stale epoch.");
			if (restored.PriorOrderToken != null && restored.PriorOrderToken.Epoch != handoff.Epoch)
				throw new InvalidOperationException("Saved MassAttack prior token has a stale epoch.");
			return restored;
		}

		public static void Validate(StealthMassAttackPrivateState state)
		{
			if (state == null || state.Activity == null || state.LastObservedTick < -1 ||
				state.LastEvaluationTick < -1 ||
				state.LastEvaluationTick > state.LastObservedTick || state.TargetId == 0 ||
				state.TargetId.HasValue != state.TargetCell.HasValue || state.TargetHitPoints < 0 ||
				state.TargetMaximumHitPoints < 0 || !Ordered(state.DefenderIds) ||
				!Ordered(state.ObjectiveIds) || !Enum.IsDefined(typeof(StealthMassAttackPhase), state.Phase) ||
				!Enum.IsDefined(typeof(StealthMassAttackDisposition), state.Disposition))
				throw new InvalidOperationException("Invalid MassAttack private state.");
			if (state.EntryState == StealthMassAttackEntryState.Pristine)
			{
				if (state.LastObservedTick != -1 || state.LastEvaluationTick != -1 ||
					state.TargetId.HasValue || state.Fingerprint != null || state.Evaluation != null ||
					state.LastOrderToken != null || state.PriorOrderToken != null || state.DefenderIds.Length != 0 ||
					state.ObjectiveIds.Length != 0 || state.Disposition != StealthMassAttackDisposition.Reacquire ||
					state.Phase != StealthMassAttackPhase.Advance || state.Activity.HasObservation)
					throw new InvalidOperationException("Unvalidated MassAttack state must be pristine.");
				return;
			}

			if (state.EntryState == StealthMassAttackEntryState.SkippedZeroMembers)
			{
				if (state.LastObservedTick < 0 || state.LastEvaluationTick != -1 ||
					state.TargetId.HasValue || state.Fingerprint != null || state.Evaluation != null ||
					state.LastOrderToken != null || state.PriorOrderToken != null || state.TargetHitPoints != 0 ||
					state.TargetMaximumHitPoints != 0 ||
					state.Disposition != StealthMassAttackDisposition.RecalculateFlee ||
					state.Phase != StealthMassAttackPhase.Advance || state.Activity.HasObservation ||
					state.Activity.Revision != 0 || state.Activity.Active != null ||
					state.Activity.Completed != null)
					throw new InvalidOperationException("Skipped-zero MassAttack state is noncanonical.");
				return;
			}

			var exited = state.EntryState == StealthMassAttackEntryState.ExitedRecalculate;
			if ((state.EntryState != StealthMassAttackEntryState.Validated && !exited) ||
				state.LastObservedTick < 0)
				throw new InvalidOperationException("Validated MassAttack state requires an observed tick.");
			if (exited && (state.Disposition != StealthMassAttackDisposition.RecalculateFlee ||
				!state.TargetId.HasValue || state.Activity.HasObservation ||
				state.Activity.Revision != 0 || state.Activity.Active != null ||
				state.Activity.Completed != null))
				throw new InvalidOperationException("Exited MassAttack state is noncanonical.");
			var targetless = state.Disposition == StealthMassAttackDisposition.UndefendedAttack ||
				state.Disposition == StealthMassAttackDisposition.Reacquire ||
				(state.Disposition == StealthMassAttackDisposition.RecalculateFlee && !state.TargetId.HasValue);
			if (targetless != !state.TargetId.HasValue ||
				state.TargetId.HasValue != (state.Fingerprint != null && state.Evaluation != null) ||
				(state.TargetId.HasValue && (state.Evaluation.Facts.SelectedTargetActorId != state.TargetId ||
					state.Evaluation.Facts.SelectedTargetCurrentCell != state.TargetCell ||
					!state.Evaluation.Facts.EnemyActorIds.SequenceEqual(state.DefenderIds))) ||
				(!exited &&
					(state.Disposition == StealthMassAttackDisposition.Retain) != (state.LastOrderToken != null)) ||
				(!exited && state.LastOrderToken != null &&
					(state.LastOrderToken.TargetActorId != state.TargetId ||
					state.LastOrderToken.TargetCurrentCell != state.TargetCell ||
					state.LastOrderToken.Phase != state.Phase ||
					state.LastOrderToken.ActivityRevision != state.Activity.Revision)) ||
				(exited && state.LastOrderToken != null &&
					state.LastOrderToken.Owner != BehaviorId.MassAttack) ||
				(state.PriorOrderToken != null && (state.LastOrderToken == null ||
					state.PriorOrderToken.Owner != state.LastOrderToken.Owner ||
					state.PriorOrderToken.Epoch != state.LastOrderToken.Epoch ||
					state.PriorOrderToken.ActivityRevision > state.LastOrderToken.ActivityRevision ||
					state.PriorOrderToken.AttemptRevision == long.MaxValue ||
					state.LastOrderToken.AttemptRevision != state.PriorOrderToken.AttemptRevision + 1)))
				throw new InvalidOperationException("MassAttack target, evaluation, and order state are inconsistent.");
			if ((state.Disposition == StealthMassAttackDisposition.UndefendedAttack &&
				(state.DefenderIds.Length != 0 || state.ObjectiveIds.Length == 0)) ||
				(state.Disposition == StealthMassAttackDisposition.Reacquire &&
					(state.DefenderIds.Length != 0 || state.ObjectiveIds.Length != 0)) ||
				(state.TargetId.HasValue && state.DefenderIds.Length == 0) ||
				(!state.TargetId.HasValue && (state.TargetHitPoints != 0 ||
					state.TargetMaximumHitPoints != 0 || state.LastEvaluationTick != -1 ||
					state.Phase != StealthMassAttackPhase.Advance)) ||
				(state.TargetId.HasValue && state.LastEvaluationTick != state.LastObservedTick))
				throw new InvalidOperationException("MassAttack disposition has no canonical live cause.");
			if (state.Evaluation != null &&
				((state.Disposition == StealthMassAttackDisposition.Retain &&
					state.Evaluation.Threat.StandardScore.Crossover <= 1) ||
				(state.Disposition == StealthMassAttackDisposition.RecalculateFlee &&
					state.Evaluation.Threat.StandardScore.Crossover > 1)))
				throw new InvalidOperationException("MassAttack disposition contradicts its standard crossover.");
		}

		static bool SameEntry(StealthMassAttackEntryEvidence left,
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

		static T ReadOptional<T>(MiniYamlNode node, string key, Func<MiniYamlNode, T> read) where T : class
		{
			var matches = node.Value.Nodes.Where(child => child.Key == key).ToArray();
			return matches.Length == 0 ? null : read(matches[0]);
		}

		static void RequireCount(MiniYamlNode node, string key, int count)
		{
			if (node.Value.Nodes.Count(child => child.Key == key) != count)
				throw new InvalidOperationException("Invalid MassAttack node count: " + key);
		}

		static void RequireOptional(MiniYamlNode node, string key)
		{
			if (node.Value.Nodes.Count(child => child.Key == key) > 1)
				throw new InvalidOperationException("Duplicate MassAttack optional node: " + key);
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

		static bool Ordered(uint[] ids)
		{
			return ids != null && ids.All(id => id != 0) && ids.SequenceEqual(ids.OrderBy(id => id)) &&
				ids.Distinct().Count() == ids.Length;
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
	}
}
