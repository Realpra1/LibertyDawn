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
	static class StealthRecalculateFleePersistence
	{
		const int Version = 2;
		static readonly string[] RootScalars =
		{
			"Version", "Owner", "Epoch", "EntryValidated", "LastObservedTick",
			"LastEvaluationTick", "Disposition", "LiveCause", "Fingerprint",
			"HasDestination", "Destination", "HasDanger", "DangerThreat", "DangerCrossover",
			"RouteRevision", "RouteProgress", "HasLongRouteCache", "LongRouteCacheRevision"
		};

		public static MiniYamlNode Serialize(string key, StealthRecalculateFleeHandoff handoff,
			StealthRecalculateFleeOwnerState state)
		{
			if (string.IsNullOrEmpty(key) || handoff == null || state == null)
				throw new ArgumentException("RecalculateFlee persistence requires complete state.");
			Validate(state, handoff);
			var nodes = new List<MiniYamlNode>
			{
				Field("Version", Version),
				new MiniYamlNode("Owner", handoff.Owner.ToString()),
				Field("Epoch", handoff.Epoch.Value),
				Field("EntryValidated", state.EntryValidated),
				Field("LastObservedTick", state.LastObservedTick),
				Field("LastEvaluationTick", state.LastEvaluationTick),
				new MiniYamlNode("Disposition", state.Disposition.ToString()),
				new MiniYamlNode("LiveCause", state.LiveCause.ToString()),
				new MiniYamlNode("Fingerprint", state.Fingerprint ?? ""),
				Field("HasDestination", state.Destination.HasValue),
				Field("Destination", state.Destination ?? default(CPos)),
				Field("HasDanger", state.Danger.HasValue),
				new MiniYamlNode("DangerThreat", Format(state.Danger?.ThreatRating ?? 0)),
				new MiniYamlNode("DangerCrossover", Format(state.Danger?.Crossover ?? 0)),
				Field("RouteRevision", state.RouteRevision),
				Field("RouteProgress", state.RouteProgress),
				Field("HasLongRouteCache", state.LongRouteCacheRevision.HasValue),
				Field("LongRouteCacheRevision", state.LongRouteCacheRevision ?? 0),
				StealthApproachPersistence.SerializeMission(handoff.Mission),
				SerializeEntry(handoff.Evidence)
			};
			AddIds(nodes, "MemberId", state.MemberIds);
			AddIds(nodes, "EnemyId", state.EnemyIds);
			foreach (var waypoint in state.OrderedRoute)
				nodes.Add(Field("RouteWaypoint", waypoint));
			foreach (var evaluation in state.Evaluations)
				nodes.Add(SerializeEvaluation(evaluation));
			if (state.LastOrderToken != null)
				nodes.Add(SerializeOrder(state.LastOrderToken));
			return Node(key, nodes);
		}

		public static StealthRecalculateFleeOwnerState Restore(MiniYamlNode node,
			StealthRecalculateFleeHandoff handoff)
		{
			if (node == null || handoff == null)
				throw new ArgumentNullException(node == null ? nameof(node) : nameof(handoff));
			var repeated = new HashSet<string>(StringComparer.Ordinal)
			{
				"Mission", "Entry", "MemberId", "EnemyId", "Evaluation", "LastOrder", "RouteWaypoint"
			};
			var values = Unique(node.Value.Nodes.Where(child => !repeated.Contains(child.Key)));
			if (values.Count != RootScalars.Length || RootScalars.Any(key => !values.ContainsKey(key)) ||
				Read<int>(values, "Version") != Version ||
				Read<BehaviorId>(values, "Owner") != BehaviorId.RecalculateFlee ||
				Read<long>(values, "Epoch") != handoff.Epoch.Value)
				throw new InvalidOperationException("Invalid RecalculateFlee private save header or field set.");
			RequireCount(node, "Mission", 1);
			RequireCount(node, "Entry", 1);
			RequireOptional(node, "LastOrder");
			if (StealthApproachPersistence.Canonical(Required(node, "Mission")) !=
				StealthApproachPersistence.Canonical(StealthApproachPersistence.SerializeMission(handoff.Mission)) ||
				!SameEntry(RestoreEntry(Required(node, "Entry")), handoff.Evidence))
				throw new InvalidOperationException("Saved RecalculateFlee immutable handoff was altered.");

			var hasDestination = Read<bool>(values, "HasDestination");
			var destination = Read<CPos>(values, "Destination");
			var hasDanger = Read<bool>(values, "HasDanger");
			var dangerThreat = ReadDouble(values, "DangerThreat");
			var dangerCrossover = ReadDouble(values, "DangerCrossover");
			var hasCache = Read<bool>(values, "HasLongRouteCache");
			var cacheRevision = Read<long>(values, "LongRouteCacheRevision");
			if ((!hasDestination && destination != default(CPos)) ||
				(!hasDanger && (dangerThreat != 0 || dangerCrossover != 0)) ||
				(!hasCache && cacheRevision != 0))
				throw new InvalidOperationException("Noncanonical absent RecalculateFlee value.");
			var restored = new StealthRecalculateFleeOwnerState
			{
				EntryValidated = Read<bool>(values, "EntryValidated"),
				LastObservedTick = Read<int>(values, "LastObservedTick"),
				LastEvaluationTick = Read<int>(values, "LastEvaluationTick"),
				Disposition = Read<StealthRecalculateFleeDisposition>(values, "Disposition"),
				LiveCause = Read<StealthRecalculateFleeLiveCause>(values, "LiveCause"),
				Fingerprint = string.IsNullOrEmpty(values["Fingerprint"]) ? null : values["Fingerprint"],
				MemberIds = ReadIds(node, "MemberId"),
				EnemyIds = ReadIds(node, "EnemyId"),
				Evaluations = node.Value.Nodes.Where(child => child.Key == "Evaluation")
					.Select(RestoreEvaluation).ToArray(),
				Destination = hasDestination ? destination : (CPos?)null,
				Danger = hasDanger ? new StealthTargetThreatScore(dangerThreat, dangerCrossover) :
					(StealthTargetThreatScore?)null,
				RouteRevision = Read<long>(values, "RouteRevision"),
				RouteProgress = Read<int>(values, "RouteProgress"),
				OrderedRoute = node.Value.Nodes.Where(child => child.Key == "RouteWaypoint")
					.Select(child => FieldLoader.GetValue<CPos>("RouteWaypoint", child.Value.Value)).ToArray(),
				LastOrderToken = ReadOptional(node, "LastOrder", RestoreOrder),
				LongRouteCacheRevision = hasCache ? cacheRevision : (long?)null
			};
			Validate(restored, handoff);
			return restored;
		}

		static void Validate(StealthRecalculateFleeOwnerState state,
			StealthRecalculateFleeHandoff handoff)
		{
			if (state.LastObservedTick < -1 || state.LastEvaluationTick < -1 || state.RouteProgress < 0 ||
				state.LastEvaluationTick > state.LastObservedTick || state.RouteRevision < 0 ||
				state.LongRouteCacheRevision < 0 || !Ordered(state.MemberIds) || !Ordered(state.EnemyIds) ||
				state.Evaluations == null || state.Evaluations.Any(evaluation => evaluation == null) ||
				state.Evaluations.Select(evaluation => evaluation.Candidate.Cell).Distinct().Count() !=
					state.Evaluations.Length ||
				!Enum.IsDefined(typeof(StealthRecalculateFleeDisposition), state.Disposition) ||
				!Enum.IsDefined(typeof(StealthRecalculateFleeLiveCause), state.LiveCause) ||
				state.OrderedRoute == null || state.OrderedRoute.Distinct().Count() != state.OrderedRoute.Length)
				throw new InvalidOperationException("Invalid RecalculateFlee private state.");
			if (!state.EntryValidated)
			{
				if (state.LastObservedTick != -1 || state.LastEvaluationTick != -1 || state.Fingerprint != null ||
					state.MemberIds.Length != 0 || state.EnemyIds.Length != 0 || state.Evaluations.Length != 0 ||
					state.Destination.HasValue || state.Danger.HasValue || state.LastOrderToken != null ||
					state.RouteRevision != 0 || state.LongRouteCacheRevision.HasValue ||
					state.OrderedRoute.Length != 0 || state.RouteProgress != 0 ||
					state.Disposition != StealthRecalculateFleeDisposition.Retain ||
					state.LiveCause != StealthRecalculateFleeLiveCause.NoRoute)
					throw new InvalidOperationException("Unvalidated RecalculateFlee state must be pristine.");
				return;
			}

			if (state.LastObservedTick < 0 || state.LastEvaluationTick != state.LastObservedTick ||
				string.IsNullOrEmpty(state.Fingerprint) ||
				(state.Disposition == StealthRecalculateFleeDisposition.TargetAcquisition) !=
					(state.LiveCause == StealthRecalculateFleeLiveCause.Completed))
				throw new InvalidOperationException("Validated RecalculateFlee state has no canonical progress.");
			var hasRoute = state.Destination.HasValue && state.Danger.HasValue && state.LastOrderToken != null &&
				state.OrderedRoute.Length != 0 && state.RouteProgress < state.OrderedRoute.Length;
			var routeCause = state.LiveCause == StealthRecalculateFleeLiveCause.Traversing ||
				state.LiveCause == StealthRecalculateFleeLiveCause.Completed ||
				(state.LiveCause == StealthRecalculateFleeLiveCause.MemberLoss && state.MemberIds.Length != 0);
			if (hasRoute != routeCause || (hasRoute &&
				!state.Evaluations.Any(evaluation => evaluation.Candidate.Cell == state.Destination &&
					SameScore(evaluation.StandardDanger, state.Danger.Value))))
				throw new InvalidOperationException("Saved RecalculateFlee route is inconsistent.");
			if ((state.LiveCause == StealthRecalculateFleeLiveCause.NoTarget && state.EnemyIds.Length != 0) ||
				(state.LiveCause == StealthRecalculateFleeLiveCause.NoRoute &&
					(state.EnemyIds.Length == 0 || state.Evaluations.Length != 0)) ||
				(state.LiveCause == StealthRecalculateFleeLiveCause.MemberLoss &&
					state.MemberIds.SequenceEqual(handoff.Evidence.MemberActorIds)))
				throw new InvalidOperationException("Saved RecalculateFlee live cause is forged.");
			if (state.LastOrderToken != null &&
				(state.LastOrderToken.Owner != handoff.Owner || state.LastOrderToken.Epoch != handoff.Epoch ||
				state.LastOrderToken.RouteRevision != state.RouteRevision ||
				!state.LastOrderToken.ActorIds.SequenceEqual(state.MemberIds) ||
				state.LastOrderToken.DestinationCell != state.OrderedRoute[state.RouteProgress]))
				throw new InvalidOperationException("Saved RecalculateFlee token is forged.");
			if (state.LongRouteCacheRevision.HasValue && !state.Evaluations.Any(evaluation =>
				evaluation.Candidate.Cell == state.Destination &&
				evaluation.Candidate.RequiresStrategicRouting))
				throw new InvalidOperationException("Saved cache metadata is not bound to a live long route.");
			if (!state.LongRouteCacheRevision.HasValue && hasRoute &&
				(state.OrderedRoute.Length != 1 || state.OrderedRoute[0] != state.Destination))
				throw new InvalidOperationException("Saved direct Flee route is not canonical.");
		}

		static MiniYamlNode SerializeEntry(StealthRecalculateFleeEntryEvidence entry)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Source", entry.Source.ToString()),
				Field("SourceEpoch", entry.SourceEpoch.Value),
				new MiniYamlNode("Fingerprint", entry.LiveFingerprint),
				Field("TargetId", entry.SelectedTargetActorId),
				Field("TargetCell", entry.SelectedTargetCurrentCell),
				Field("FormationCloaked", entry.FormationCloaked),
				new MiniYamlNode("Threat", Format(entry.StandardScore.ThreatRating)),
				new MiniYamlNode("Crossover", Format(entry.StandardScore.Crossover))
			};
			AddIds(nodes, "MemberId", entry.MemberActorIds);
			AddIds(nodes, "EnemyId", entry.EnemyActorIds);
			return Node("Entry", nodes);
		}

		internal static StealthRecalculateFleeEntryEvidence RestoreEntry(MiniYamlNode node)
		{
			var values = UniqueExcept(node, "MemberId", "EnemyId");
			if (values.Count != 8)
				throw new InvalidOperationException("Invalid RecalculateFlee entry field set.");
			return new StealthRecalculateFleeEntryEvidence(
				Read<StealthRecalculateFleeSource>(values, "Source"),
				new OwnershipEpoch(Read<long>(values, "SourceEpoch")), values["Fingerprint"],
				Read<uint>(values, "TargetId"), Read<CPos>(values, "TargetCell"),
				ReadIds(node, "MemberId"), ReadIds(node, "EnemyId"),
				Read<bool>(values, "FormationCloaked"), new StealthTargetThreatScore(
					ReadDouble(values, "Threat"), ReadDouble(values, "Crossover")));
		}

		static bool SameEntry(StealthRecalculateFleeEntryEvidence left,
			StealthRecalculateFleeEntryEvidence right)
		{
			return left.Source == right.Source && left.SourceEpoch == right.SourceEpoch &&
				left.LiveFingerprint == right.LiveFingerprint &&
				left.SelectedTargetActorId == right.SelectedTargetActorId &&
				left.SelectedTargetCurrentCell == right.SelectedTargetCurrentCell &&
				left.MemberActorIds.SequenceEqual(right.MemberActorIds) &&
				left.EnemyActorIds.SequenceEqual(right.EnemyActorIds) &&
				left.FormationCloaked == right.FormationCloaked &&
				SameScore(left.StandardScore, right.StandardScore);
		}

		static MiniYamlNode SerializeEvaluation(StealthRecalculateFleeRouteEvaluation evaluation)
		{
			var nodes = new List<MiniYamlNode>
			{
				Field("Cell", evaluation.Candidate.Cell),
				Field("Passable", evaluation.Candidate.IsPassable),
				Field("Long", evaluation.Candidate.RequiresStrategicRouting),
				Field("Detector", evaluation.Candidate.HasDetectorCoverage),
				new MiniYamlNode("Threat", Format(evaluation.StandardDanger.ThreatRating)),
				new MiniYamlNode("Crossover", Format(evaluation.StandardDanger.Crossover)),
				Field("FormationCloaked", evaluation.Facts.FormationCloaked)
			};
			foreach (var member in evaluation.Facts.Members)
				nodes.Add(Node("Member", new[]
				{
					Field("Id", member.ActorId), Field("Cell", member.CurrentCell),
					Field("Range", member.CurrentWeaponRangeCells), Field("HP", member.HitPoints),
					Field("MaxHP", member.MaximumHitPoints)
				}));
			foreach (var enemy in evaluation.Facts.Enemies)
				nodes.Add(Node("Enemy", new[]
				{
					Field("Id", enemy.ActorId), new MiniYamlNode("Type", enemy.ActorType),
					Field("Cell", enemy.CurrentCell), Field("HP", enemy.HitPoints),
					Field("MaxHP", enemy.MaximumHitPoints), Field("Range", enemy.CurrentWeaponRangeCells),
					Field("Detector", enemy.HasDetectorCoverage)
				}));
			return Node("Evaluation", nodes);
		}

		static StealthRecalculateFleeRouteEvaluation RestoreEvaluation(MiniYamlNode node)
		{
			var values = UniqueExcept(node, "Member", "Enemy");
			if (values.Count != 7 || !Read<bool>(values, "Passable"))
				throw new InvalidOperationException("Invalid RecalculateFlee route evaluation.");
			var candidate = new StealthRecalculateFleeCandidateSnapshot(Read<CPos>(values, "Cell"), true,
				Read<bool>(values, "Long"), Read<bool>(values, "Detector"));
			var members = node.Value.Nodes.Where(child => child.Key == "Member").Select(RestoreMember).ToArray();
			var enemies = node.Value.Nodes.Where(child => child.Key == "Enemy").Select(RestoreEnemy).ToArray();
			var facts = new StealthRecalculateFleeThreatFacts(candidate.Cell, members, enemies,
				Read<bool>(values, "FormationCloaked"), candidate.HasDetectorCoverage);
			return new StealthRecalculateFleeRouteEvaluation(candidate, facts,
				new StealthTargetThreatScore(ReadDouble(values, "Threat"),
					ReadDouble(values, "Crossover")));
		}

		static StealthRecalculateFleeMemberSnapshot RestoreMember(MiniYamlNode node)
		{
			var values = Unique(node.Value.Nodes);
			if (values.Count != 5)
				throw new InvalidOperationException("Invalid saved flee member.");
			return new StealthRecalculateFleeMemberSnapshot(Read<uint>(values, "Id"),
				Read<CPos>(values, "Cell"), Read<int>(values, "Range"),
				Read<int>(values, "HP"), Read<int>(values, "MaxHP"));
		}

		static StealthRecalculateFleeEnemySnapshot RestoreEnemy(MiniYamlNode node)
		{
			var values = Unique(node.Value.Nodes);
			if (values.Count != 7)
				throw new InvalidOperationException("Invalid saved flee enemy.");
			return new StealthRecalculateFleeEnemySnapshot(Read<uint>(values, "Id"), values["Type"],
				Read<CPos>(values, "Cell"), Read<int>(values, "HP"), Read<int>(values, "MaxHP"),
				Read<int>(values, "Range"), Read<bool>(values, "Detector"));
		}

		static MiniYamlNode SerializeOrder(StealthRecalculateFleeOrderToken token)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Owner", token.Owner.ToString()),
				Field("Epoch", token.Epoch.Value), Field("Destination", token.DestinationCell),
				Field("RouteRevision", token.RouteRevision), Field("ActivityRevision", token.ActivityRevision)
			};
			AddIds(nodes, "ActorId", token.ActorIds);
			return Node("LastOrder", nodes);
		}

		static StealthRecalculateFleeOrderToken RestoreOrder(MiniYamlNode node)
		{
			var values = UniqueExcept(node, "ActorId");
			if (values.Count != 5 || Read<BehaviorId>(values, "Owner") != BehaviorId.RecalculateFlee)
				throw new InvalidOperationException("Invalid saved RecalculateFlee order token.");
			return new StealthRecalculateFleeOrderToken(BehaviorId.RecalculateFlee,
				new OwnershipEpoch(Read<long>(values, "Epoch")), ReadIds(node, "ActorId"),
				Read<CPos>(values, "Destination"), Read<long>(values, "RouteRevision"),
				Read<long>(values, "ActivityRevision"));
		}

		static MiniYamlNode Field<T>(string key, T value) { return new MiniYamlNode(key, FieldSaver.FormatValue(value)); }
		static MiniYamlNode Node(string key, IEnumerable<MiniYamlNode> nodes) { return new MiniYamlNode(key, new MiniYaml("", nodes.ToList())); }
		static string Format(double value) { return value.ToString("R", CultureInfo.InvariantCulture); }
		static void AddIds(List<MiniYamlNode> nodes, string key, IEnumerable<uint> ids) { foreach (var id in ids) nodes.Add(Field(key, id)); }
		static uint[] ReadIds(MiniYamlNode node, string key) { return node.Value.Nodes.Where(child => child.Key == key).Select(child => FieldLoader.GetValue<uint>(key, child.Value.Value)).ToArray(); }
		static MiniYamlNode Required(MiniYamlNode node, string key) { var matches = node.Value.Nodes.Where(child => child.Key == key).ToArray(); if (matches.Length != 1) throw new InvalidOperationException("Missing or duplicate RecalculateFlee node: " + key); return matches[0]; }
		static T ReadOptional<T>(MiniYamlNode node, string key, Func<MiniYamlNode, T> read) where T : class { var matches = node.Value.Nodes.Where(child => child.Key == key).ToArray(); return matches.Length == 0 ? null : read(matches[0]); }
		static void RequireCount(MiniYamlNode node, string key, int count) { if (node.Value.Nodes.Count(child => child.Key == key) != count) throw new InvalidOperationException("Invalid RecalculateFlee node count: " + key); }
		static void RequireOptional(MiniYamlNode node, string key) { if (node.Value.Nodes.Count(child => child.Key == key) > 1) throw new InvalidOperationException("Duplicate RecalculateFlee node: " + key); }
		static Dictionary<string, string> UniqueExcept(MiniYamlNode node, params string[] repeated) { var excluded = new HashSet<string>(repeated, StringComparer.Ordinal); return Unique(node.Value.Nodes.Where(child => !excluded.Contains(child.Key))); }
		static Dictionary<string, string> Unique(IEnumerable<MiniYamlNode> nodes) { var result = new Dictionary<string, string>(StringComparer.Ordinal); try { foreach (var node in nodes) result.Add(node.Key, node.Value.Value); } catch (ArgumentException ex) { throw new InvalidOperationException("Duplicate RecalculateFlee field.", ex); } return result; }
		static bool Ordered(uint[] ids) { return ids != null && ids.All(id => id != 0) && ids.SequenceEqual(ids.OrderBy(id => id)) && ids.Distinct().Count() == ids.Length; }
		static double ReadDouble(Dictionary<string, string> values, string key) { if (!values.TryGetValue(key, out var value) || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed) || parsed < 0) throw new InvalidOperationException("Invalid RecalculateFlee score: " + key); return parsed; }
		static bool SameScore(StealthTargetThreatScore left, StealthTargetThreatScore right) { return left.ThreatRating.Equals(right.ThreatRating) && left.Crossover.Equals(right.Crossover); }
		static T Read<T>(Dictionary<string, string> values, string key)
		{
			if (!values.TryGetValue(key, out var value)) throw new InvalidOperationException("Missing RecalculateFlee field: " + key);
			if (typeof(T).IsEnum) { if (!Enum.TryParse(typeof(T), value, out var parsed) || !Enum.IsDefined(typeof(T), parsed)) throw new InvalidOperationException("Invalid RecalculateFlee enum: " + key); return (T)parsed; }
			return FieldLoader.GetValue<T>(key, value);
		}
	}
}
