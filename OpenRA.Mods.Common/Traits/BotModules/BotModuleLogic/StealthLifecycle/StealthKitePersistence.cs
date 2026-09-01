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
	sealed class StealthKitePrivateState
	{
		public int LastObservedTick { get; }
		public int LastPlanTick { get; }
		public StealthKitePhase Phase { get; }
		public StealthKiteDisposition Disposition { get; }
		public uint? TargetId { get; }
		public CPos? TargetCell { get; }
		public int TargetHitPoints { get; }
		public int TargetMaximumHitPoints { get; }
		public int FireBaselineTargetHitPoints { get; }
		public StealthKiteLiveFingerprint Fingerprint { get; }
		public StealthKitePlan Plan { get; }
		public StealthKiteFallbackEvidence FallbackEvidence { get; }
		public uint[] DefenderIds { get; }
		public uint[] ObjectiveIds { get; }
		public StealthKiteOrderToken LastOrderToken { get; }

		public StealthKitePrivateState(int lastObservedTick, int lastPlanTick,
			StealthKitePhase phase, StealthKiteDisposition disposition, uint? targetId,
			CPos? targetCell, int targetHitPoints, int targetMaximumHitPoints,
			int fireBaselineTargetHitPoints, StealthKiteLiveFingerprint fingerprint,
			StealthKitePlan plan, StealthKiteFallbackEvidence fallbackEvidence,
			IEnumerable<uint> defenderIds, IEnumerable<uint> objectiveIds,
			StealthKiteOrderToken lastOrderToken)
		{
			LastObservedTick = lastObservedTick;
			LastPlanTick = lastPlanTick;
			Phase = phase;
			Disposition = disposition;
			TargetId = targetId;
			TargetCell = targetCell;
			TargetHitPoints = targetHitPoints;
			TargetMaximumHitPoints = targetMaximumHitPoints;
			FireBaselineTargetHitPoints = fireBaselineTargetHitPoints;
			Fingerprint = fingerprint;
			Plan = plan;
			FallbackEvidence = fallbackEvidence;
			DefenderIds = defenderIds?.ToArray();
			ObjectiveIds = objectiveIds?.ToArray();
			LastOrderToken = lastOrderToken;
			StealthKitePersistence.Validate(this);
		}
	}

	static class StealthKitePersistence
	{
		const int Version = 2;
		static readonly string[] RootScalars =
		{
			"Version", "Owner", "Epoch", "LastObservedTick", "LastPlanTick", "Phase",
			"Disposition", "HasTarget", "TargetId", "TargetCell", "TargetHitPoints",
			"TargetMaximumHitPoints", "FireBaselineTargetHitPoints", "Fingerprint"
		};

		public static MiniYamlNode Serialize(string key, StealthKiteHandoff handoff,
			StealthApproachMission mission, StealthKitePrivateState state)
		{
			Validate(state);
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", Version.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Owner", handoff.Owner.ToString()),
				new MiniYamlNode("Epoch", handoff.Epoch.Value.ToString(CultureInfo.InvariantCulture)),
				StealthApproachPersistence.SerializeMission(mission),
				new MiniYamlNode("LastObservedTick", FieldSaver.FormatValue(state.LastObservedTick)),
				new MiniYamlNode("LastPlanTick", FieldSaver.FormatValue(state.LastPlanTick)),
				new MiniYamlNode("Phase", state.Phase.ToString()),
				new MiniYamlNode("Disposition", state.Disposition.ToString()),
				new MiniYamlNode("HasTarget", FieldSaver.FormatValue(state.TargetId.HasValue)),
				new MiniYamlNode("TargetId", FieldSaver.FormatValue(state.TargetId ?? 0)),
				new MiniYamlNode("TargetCell", FieldSaver.FormatValue(state.TargetCell ?? default(CPos))),
				new MiniYamlNode("TargetHitPoints", FieldSaver.FormatValue(state.TargetHitPoints)),
				new MiniYamlNode("TargetMaximumHitPoints", FieldSaver.FormatValue(state.TargetMaximumHitPoints)),
				new MiniYamlNode("FireBaselineTargetHitPoints",
					FieldSaver.FormatValue(state.FireBaselineTargetHitPoints)),
				new MiniYamlNode("Fingerprint", state.Fingerprint?.Canonical ?? "")
			};
			AddIds(nodes, "IncomingDefenderId", handoff.LiveDefenderActorIds);
			AddIds(nodes, "DefenderId", state.DefenderIds);
			AddIds(nodes, "ObjectiveId", state.ObjectiveIds);
			if (state.Plan != null)
				nodes.Add(SerializePlan(state.Plan));
			if (state.FallbackEvidence != null)
				nodes.Add(SerializeFallback(state.FallbackEvidence));
			if (state.LastOrderToken != null)
				nodes.Add(StealthKitePersistenceNodes.SerializeOrder(state.LastOrderToken));
			return new MiniYamlNode(key, "", nodes);
		}

		public static StealthKitePrivateState Restore(MiniYamlNode node,
			StealthKiteHandoff handoff, StealthApproachMission mission)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));
			var repeated = new HashSet<string>(StringComparer.Ordinal)
			{
				"IncomingDefenderId", "DefenderId", "ObjectiveId", "Mission", "Plan", "Fallback", "LastOrder"
			};
			var values = Unique(node.Value.Nodes.Where(child => !repeated.Contains(child.Key)));
			if (values.Count != RootScalars.Length || RootScalars.Any(key => !values.ContainsKey(key)) ||
				Read<int>(values, "Version") != Version || Read<BehaviorId>(values, "Owner") != BehaviorId.Kite ||
				Read<long>(values, "Epoch") != handoff.Epoch.Value)
				throw new InvalidOperationException("Invalid Kite private save header or field set.");
			RequireOptionalCount(node, "Plan");
			RequireOptionalCount(node, "Fallback");
			RequireOptionalCount(node, "LastOrder");
			var missionNodes = node.Value.Nodes.Where(child => child.Key == "Mission").ToArray();
			if (missionNodes.Length != 1 || StealthApproachPersistence.Canonical(missionNodes[0]) !=
				StealthApproachPersistence.Canonical(StealthApproachPersistence.SerializeMission(mission)) ||
				!ReadIds(node, "IncomingDefenderId").SequenceEqual(handoff.LiveDefenderActorIds))
				throw new InvalidOperationException("Kite private state does not match its immutable handoff.");

			var hasTarget = Read<bool>(values, "HasTarget");
			var targetIdValue = Read<uint>(values, "TargetId");
			var targetCellValue = Read<CPos>(values, "TargetCell");
			var fingerprintText = values["Fingerprint"];
			if (!hasTarget && (targetIdValue != 0 || targetCellValue != default(CPos)))
				throw new InvalidOperationException("Noncanonical absent Kite target.");
			var restored = new StealthKitePrivateState(
				Read<int>(values, "LastObservedTick"), Read<int>(values, "LastPlanTick"),
				Read<StealthKitePhase>(values, "Phase"), Read<StealthKiteDisposition>(values, "Disposition"),
				hasTarget ? targetIdValue : (uint?)null, hasTarget ? targetCellValue : (CPos?)null,
				Read<int>(values, "TargetHitPoints"), Read<int>(values, "TargetMaximumHitPoints"),
				Read<int>(values, "FireBaselineTargetHitPoints"),
				fingerprintText.Length == 0 ? null : new StealthKiteLiveFingerprint(fingerprintText),
				ReadOptional(node, "Plan", RestorePlan), ReadOptional(node, "Fallback", RestoreFallback),
				ReadIds(node, "DefenderId"), ReadIds(node, "ObjectiveId"),
				ReadOptional(node, "LastOrder", StealthKitePersistenceNodes.RestoreOrder));
			if (restored.LastOrderToken != null && restored.LastOrderToken.Epoch != handoff.Epoch)
				throw new InvalidOperationException("Saved Kite order token has a stale ownership epoch.");
			return restored;
		}

		static MiniYamlNode SerializePlan(StealthKitePlan plan)
		{
			return Node("Plan", new[]
			{
				new MiniYamlNode("Fingerprint", plan.Fingerprint.Canonical),
				new MiniYamlNode("FireCell", FieldSaver.FormatValue(plan.FireCell)),
				new MiniYamlNode("WithdrawCell", FieldSaver.FormatValue(plan.WithdrawCell)),
				new MiniYamlNode("PlannedDecloak", "True"),
				StealthKitePersistenceNodes.SerializeSafety("FireSafety", plan.FireSafety),
				StealthKitePersistenceNodes.SerializeSafety("WithdrawSafety", plan.WithdrawSafety),
				StealthKitePersistenceNodes.SerializeFacts("FireFacts", plan.FireFacts),
				StealthKitePersistenceNodes.SerializeFacts("WithdrawFacts", plan.WithdrawFacts)
			});
		}

		static StealthKitePlan RestorePlan(MiniYamlNode node)
		{
			var values = UniqueScalars(node, "FireSafety", "WithdrawSafety", "FireFacts", "WithdrawFacts");
			if (values.Count != 4 || Read<bool>(values, "PlannedDecloak") != true)
				throw new InvalidOperationException("Invalid Kite plan field set.");
			return new StealthKitePlan(new StealthKiteLiveFingerprint(values["Fingerprint"]),
				Read<CPos>(values, "FireCell"), Read<CPos>(values, "WithdrawCell"),
				StealthKitePersistenceNodes.RestoreFacts(Required(node, "FireFacts")),
				StealthKitePersistenceNodes.RestoreSafety(Required(node, "FireSafety")),
				StealthKitePersistenceNodes.RestoreFacts(Required(node, "WithdrawFacts")),
				StealthKitePersistenceNodes.RestoreSafety(Required(node, "WithdrawSafety")));
		}

		static MiniYamlNode SerializeFallback(StealthKiteFallbackEvidence evidence)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Reason", evidence.Reason.ToString()),
				new MiniYamlNode("Fingerprint", evidence.LiveFingerprint)
			};
			AddIds(nodes, "DefenderId", evidence.DefenderActorIds);
			if (evidence.Reason == StealthKiteFallbackReason.NoSafePlan)
			{
				nodes.Add(StealthKitePersistenceNodes.SerializeFallbackFacts(evidence.AttackFacts));
				nodes.Add(new MiniYamlNode("Threat", Format(evidence.AttackScore.Value.ThreatRating)));
				nodes.Add(new MiniYamlNode("Crossover", Format(evidence.AttackScore.Value.Crossover)));
			}

			return Node("Fallback", nodes);
		}

		static StealthKiteFallbackEvidence RestoreFallback(MiniYamlNode node)
		{
			RequireOptionalCount(node, "AttackFacts");
			var values = UniqueScalars(node, "DefenderId", "AttackFacts");
			var reason = Read<StealthKiteFallbackReason>(values, "Reason");
			if ((reason == StealthKiteFallbackReason.NoSafePlan) !=
				(node.Value.Nodes.Count(child => child.Key == "AttackFacts") == 1))
				throw new InvalidOperationException("Kite fallback facts do not match the fallback reason.");
			var facts = reason == StealthKiteFallbackReason.NoSafePlan ?
				StealthKitePersistenceNodes.RestoreFallbackFacts(Required(node, "AttackFacts")) : null;
			var score = reason == StealthKiteFallbackReason.NoSafePlan ?
				new StealthTargetThreatScore(ReadDouble(values, "Threat", false),
					ReadDouble(values, "Crossover", true)) : (StealthTargetThreatScore?)null;
			if (values.Count != (reason == StealthKiteFallbackReason.NoSafePlan ? 4 : 2))
				throw new InvalidOperationException("Invalid Kite fallback field set.");
			return new StealthKiteFallbackEvidence(reason, values["Fingerprint"],
				ReadIds(node, "DefenderId"), facts, score);
		}

		public static bool SamePlan(StealthKitePlan left, StealthKitePlan right)
		{
			return left != null && right != null && left.Fingerprint.Equals(right.Fingerprint) &&
				left.FireCell == right.FireCell && left.WithdrawCell == right.WithdrawCell &&
				left.PlannedDecloak == right.PlannedDecloak &&
				StealthKitePlanBuilder.SameFacts(left.FireFacts, right.FireFacts) &&
				StealthKitePlanBuilder.SameSafety(left.FireSafety, right.FireSafety) &&
				StealthKitePlanBuilder.SameFacts(left.WithdrawFacts, right.WithdrawFacts) &&
				StealthKitePlanBuilder.SameSafety(left.WithdrawSafety, right.WithdrawSafety);
		}

		public static bool SameFallback(StealthKiteFallbackEvidence left,
			StealthKiteFallbackEvidence right)
		{
			return left != null && right != null && left.Reason == right.Reason &&
				left.LiveFingerprint == right.LiveFingerprint &&
				left.DefenderActorIds.SequenceEqual(right.DefenderActorIds) &&
				(left.Reason == StealthKiteFallbackReason.NoLiveMembers ||
					(SameFallbackFacts(left.AttackFacts, right.AttackFacts) &&
					left.AttackScore.Value.ThreatRating.Equals(right.AttackScore.Value.ThreatRating) &&
					left.AttackScore.Value.Crossover.Equals(right.AttackScore.Value.Crossover)));
		}

		static bool SameFallbackFacts(StealthKiteFallbackFacts left, StealthKiteFallbackFacts right)
		{
			return left.SelectedTargetActorId == right.SelectedTargetActorId &&
				left.SelectedTargetCurrentCell == right.SelectedTargetCurrentCell &&
				left.FriendlyActorIds.SequenceEqual(right.FriendlyActorIds) &&
				left.EnemyActorIds.SequenceEqual(right.EnemyActorIds) &&
				left.FormationCloaked == right.FormationCloaked;
		}

		public static void Validate(StealthKitePrivateState state)
		{
			if (state == null || state.LastObservedTick < -1 || state.LastPlanTick < -1 ||
				state.TargetId == 0 || state.TargetId.HasValue != state.TargetCell.HasValue ||
				state.TargetHitPoints < 0 || state.TargetMaximumHitPoints < 0 ||
				!Ordered(state.DefenderIds) || !Ordered(state.ObjectiveIds) ||
				!Enum.IsDefined(typeof(StealthKitePhase), state.Phase) ||
				!Enum.IsDefined(typeof(StealthKiteDisposition), state.Disposition))
				throw new InvalidOperationException("Invalid Kite private state.");
			var retained = state.Disposition == StealthKiteDisposition.Retain;
			var fallback = state.Disposition == StealthKiteDisposition.MassAttack ||
				state.Disposition == StealthKiteDisposition.RecalculateFlee;
			var targetless = state.Disposition == StealthKiteDisposition.UndefendedAttack ||
				state.Disposition == StealthKiteDisposition.Reacquire;
			if (retained != (state.Plan != null) || retained != (state.LastOrderToken != null) ||
				fallback != (state.FallbackEvidence != null) || (retained && state.Fingerprint == null) ||
				(state.Plan != null && !state.Plan.Fingerprint.Equals(state.Fingerprint)) ||
				(state.FallbackEvidence != null && state.FallbackEvidence.LiveFingerprint !=
					state.Fingerprint?.Canonical))
				throw new InvalidOperationException("Kite plan, fallback, and fingerprint are inconsistent.");
			if (state.FallbackEvidence?.Reason == StealthKiteFallbackReason.NoLiveMembers ?
				state.TargetId.HasValue || state.Disposition != StealthKiteDisposition.RecalculateFlee :
				fallback && !state.TargetId.HasValue)
				throw new InvalidOperationException("Kite fallback target shape is inconsistent.");
			if ((!state.TargetId.HasValue && (state.TargetHitPoints != 0 ||
					state.TargetMaximumHitPoints != 0)) ||
				(targetless && (state.TargetId.HasValue || state.Fingerprint != null ||
					state.LastPlanTick != -1)) ||
				(state.Disposition == StealthKiteDisposition.CrushEvaluation &&
					(!state.TargetId.HasValue || state.Fingerprint == null ||
						state.DefenderIds.Length == 0 || state.LastPlanTick != -1)))
				throw new InvalidOperationException("Kite target state is not canonical for its disposition.");
			if ((retained && (state.LastPlanTick < 0 || state.FireBaselineTargetHitPoints < -1)) ||
				(!retained && (state.FireBaselineTargetHitPoints != -1 ||
					state.LastOrderToken != null || state.Phase != StealthKitePhase.Position)) ||
				(state.Disposition == StealthKiteDisposition.UndefendedAttack &&
					(state.DefenderIds.Length != 0 || state.ObjectiveIds.Length == 0)) ||
				(state.Disposition == StealthKiteDisposition.Reacquire &&
					(state.DefenderIds.Length != 0 || state.ObjectiveIds.Length != 0)))
				throw new InvalidOperationException("Kite disposition has no canonical live cause.");
			ValidatePlanState(state, retained);
			ValidateFallbackState(state, fallback);
		}

		static void ValidatePlanState(StealthKitePrivateState state, bool retained)
		{
			if (!retained)
				return;
			var plan = state.Plan;
			var order = state.LastOrderToken;
			var fireTarget = plan.FireFacts.Enemies.SingleOrDefault(enemy =>
				enemy.ActorId == state.TargetId);
			if (!state.TargetId.HasValue || state.DefenderIds.Length == 0 ||
				fireTarget == null || fireTarget.CurrentCell != state.TargetCell ||
				fireTarget.HitPoints != state.TargetHitPoints ||
				fireTarget.MaximumHitPoints != state.TargetMaximumHitPoints ||
				!plan.FireFacts.PlannedDecloak || !plan.FireFacts.PlannedAttack ||
				plan.WithdrawFacts.PlannedDecloak || plan.WithdrawFacts.PlannedAttack ||
				plan.FireFacts.SelectedTargetActorId != state.TargetId ||
				plan.FireFacts.SelectedTargetCurrentCell != state.TargetCell ||
				plan.WithdrawFacts.SelectedTargetActorId != state.TargetId ||
				plan.WithdrawFacts.SelectedTargetCurrentCell != state.TargetCell ||
				!plan.FireFacts.FriendlyActorIds.SequenceEqual(order.ActorIds) ||
				!plan.WithdrawFacts.FriendlyActorIds.SequenceEqual(order.ActorIds) ||
				!plan.FireFacts.EnemyActorIds.SequenceEqual(state.DefenderIds) ||
				!plan.WithdrawFacts.EnemyActorIds.SequenceEqual(state.DefenderIds) ||
				order.Action != (StealthKiteAction)state.Phase ||
				(state.Phase == StealthKitePhase.Fire ?
					order.TargetActorId != state.TargetId || order.Cell != state.TargetCell :
					order.TargetActorId.HasValue || order.Cell != (state.Phase == StealthKitePhase.Position ?
						plan.FireCell : plan.WithdrawCell)))
				throw new InvalidOperationException("Saved Kite plan and phase order are not exactly bound.");
		}

		static void ValidateFallbackState(StealthKitePrivateState state, bool fallback)
		{
			if (!fallback)
				return;
			var evidence = state.FallbackEvidence;
			if (!evidence.DefenderActorIds.SequenceEqual(state.DefenderIds))
				throw new InvalidOperationException("Saved Kite fallback defenders are not current evidence.");
			if (evidence.Reason == StealthKiteFallbackReason.NoLiveMembers)
			{
				if (state.TargetId.HasValue || state.LastPlanTick != -1 ||
					state.Disposition != StealthKiteDisposition.RecalculateFlee)
					throw new InvalidOperationException("Saved zero-member fallback is noncanonical.");
				return;
			}

			var facts = evidence.AttackFacts;
			if (!state.TargetId.HasValue || state.LastPlanTick < 0 ||
				facts.SelectedTargetActorId != state.TargetId ||
				facts.SelectedTargetCurrentCell != state.TargetCell ||
				!facts.EnemyActorIds.SequenceEqual(state.DefenderIds) ||
				(state.Disposition == StealthKiteDisposition.MassAttack) !=
					(evidence.AttackScore.Value.Crossover > 2))
				throw new InvalidOperationException("Saved no-plan fallback evidence is noncanonical.");
		}

		static MiniYamlNode Node(string key, IEnumerable<MiniYamlNode> nodes)
		{
			return new MiniYamlNode(key, new MiniYaml("", nodes.ToList()));
		}

		static MiniYamlNode Required(MiniYamlNode node, string key)
		{
			var matches = node.Value.Nodes.Where(child => child.Key == key).ToArray();
			if (matches.Length != 1)
				throw new InvalidOperationException("Missing or duplicate Kite node: " + key);
			return matches[0];
		}

		static T ReadOptional<T>(MiniYamlNode node, string key, Func<MiniYamlNode, T> read) where T : class
		{
			var matches = node.Value.Nodes.Where(child => child.Key == key).ToArray();
			return matches.Length == 0 ? null : read(matches[0]);
		}

		static void RequireOptionalCount(MiniYamlNode node, string key)
		{
			if (node.Value.Nodes.Count(child => child.Key == key) > 1)
				throw new InvalidOperationException("Duplicate Kite optional node: " + key);
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
