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
	static class StealthRepairPersistence
	{
		const int Version = 1;
		static readonly string[] RootScalars =
		{
			"Version", "Owner", "Epoch", "EntryValidated", "LastObservedTick",
			"Disposition", "LiveCause", "Fingerprint", "HasOption", "OptionId",
			"HasRoute", "RouteId", "RouteProgress", "HasDanger", "DangerThreat",
			"DangerCrossover", "RouteRevision", "LastTokenFingerprint",
			"HasLongRouteCache", "LongRouteCacheRevision"
		};

		public static MiniYamlNode Serialize(string key, StealthRepairHandoff handoff,
			StealthRepairOwnerState state)
		{
			if (string.IsNullOrEmpty(key) || handoff == null || state == null)
				throw new ArgumentException("Repair persistence requires complete state.");
			Validate(state, handoff);
			var nodes = new List<MiniYamlNode>
			{
				Field("Version", Version), new MiniYamlNode("Owner", handoff.Owner.ToString()),
				Field("Epoch", handoff.Epoch.Value), Field("EntryValidated", state.EntryValidated),
				Field("LastObservedTick", state.LastObservedTick),
				new MiniYamlNode("Disposition", state.Disposition.ToString()),
				new MiniYamlNode("LiveCause", state.LiveCause.ToString()),
				new MiniYamlNode("Fingerprint", state.Fingerprint ?? ""),
				Field("HasOption", state.OptionId.HasValue), Field("OptionId", state.OptionId ?? 0),
				Field("HasRoute", state.RouteId.HasValue), Field("RouteId", state.RouteId ?? 0),
				Field("RouteProgress", state.RouteProgress), Field("HasDanger", state.Danger.HasValue),
				new MiniYamlNode("DangerThreat", Format(state.Danger?.ThreatRating ?? 0)),
				new MiniYamlNode("DangerCrossover", Format(state.Danger?.Crossover ?? 0)),
				Field("RouteRevision", state.RouteRevision),
				new MiniYamlNode("LastTokenFingerprint", TokenFingerprint(state.LastOrderToken)),
				Field("HasLongRouteCache", state.LongRouteCacheRevision.HasValue),
				Field("LongRouteCacheRevision", state.LongRouteCacheRevision ?? 0),
				StealthApproachPersistence.SerializeMission(handoff.Mission),
				SerializeCause(handoff), SerializeResume(handoff.Resume)
			};
			AddIds(nodes, "MemberId", state.MemberIds);
			AddIds(nodes, "EnemyId", state.EnemyIds);
			foreach (var evaluation in state.Evaluations)
				nodes.Add(SerializeEvaluation(evaluation));
			if (state.LastOrderToken != null)
				nodes.Add(SerializeOrder(state.LastOrderToken));
			if (state.Completion != null)
				nodes.Add(SerializeCompletion(state.Completion));
			return Node(key, nodes);
		}

		public static StealthRepairOwnerState Restore(MiniYamlNode node,
			StealthRepairHandoff handoff)
		{
			if (node == null || handoff == null)
				throw new ArgumentNullException(node == null ? nameof(node) : nameof(handoff));
			var repeated = new HashSet<string>(StringComparer.Ordinal)
			{
				"Mission", "Cause", "Resume", "MemberId", "EnemyId", "Evaluation",
				"LastOrder", "Completion"
			};
			var values = Unique(node.Value.Nodes.Where(child => !repeated.Contains(child.Key)));
			if (values.Count != RootScalars.Length || RootScalars.Any(key => !values.ContainsKey(key)) ||
				Read<int>(values, "Version") != Version ||
				Read<BehaviorId>(values, "Owner") != BehaviorId.Repair ||
				Read<long>(values, "Epoch") != handoff.Epoch.Value)
				throw new InvalidOperationException("Invalid Repair private save header or field set.");
			RequireCount(node, "Mission", 1);
			RequireCount(node, "Cause", 1);
			RequireCount(node, "Resume", 1);
			RequireOptional(node, "LastOrder");
			RequireOptional(node, "Completion");
			if (StealthApproachPersistence.Canonical(Required(node, "Mission")) !=
				StealthApproachPersistence.Canonical(StealthApproachPersistence.SerializeMission(handoff.Mission)) ||
				!SameCause(RestoreCause(Required(node, "Cause")), handoff) ||
				!SameResume(RestoreResume(Required(node, "Resume"), handoff.Mission), handoff.Resume))
				throw new InvalidOperationException("Saved Repair immutable Damage handoff was altered.");

			var hasOption = Read<bool>(values, "HasOption");
			var optionId = Read<uint>(values, "OptionId");
			var hasRoute = Read<bool>(values, "HasRoute");
			var routeId = Read<uint>(values, "RouteId");
			var hasDanger = Read<bool>(values, "HasDanger");
			var threat = ReadDouble(values, "DangerThreat");
			var crossover = ReadDouble(values, "DangerCrossover");
			var hasCache = Read<bool>(values, "HasLongRouteCache");
			var cacheRevision = Read<long>(values, "LongRouteCacheRevision");
			if ((!hasOption && optionId != 0) || (!hasRoute && routeId != 0) ||
				(!hasDanger && (threat != 0 || crossover != 0)) || (!hasCache && cacheRevision != 0))
				throw new InvalidOperationException("Noncanonical absent Repair value.");
			var restored = new StealthRepairOwnerState
			{
				EntryValidated = Read<bool>(values, "EntryValidated"),
				LastObservedTick = Read<int>(values, "LastObservedTick"),
				Disposition = Read<StealthRepairDisposition>(values, "Disposition"),
				LiveCause = Read<StealthRepairLiveCause>(values, "LiveCause"),
				Fingerprint = string.IsNullOrEmpty(values["Fingerprint"]) ? null : values["Fingerprint"],
				MemberIds = ReadIds(node, "MemberId"), EnemyIds = ReadIds(node, "EnemyId"),
				Evaluations = node.Value.Nodes.Where(child => child.Key == "Evaluation")
					.Select(RestoreEvaluation).ToArray(),
				OptionId = hasOption ? optionId : (uint?)null,
				RouteId = hasRoute ? routeId : (uint?)null,
				RouteProgress = Read<int>(values, "RouteProgress"),
				Danger = hasDanger ? new StealthTargetThreatScore(threat, crossover) :
					(StealthTargetThreatScore?)null,
				RouteRevision = Read<long>(values, "RouteRevision"),
				LastOrderToken = ReadOptional(node, "LastOrder", RestoreOrder),
				Completion = ReadOptional(node, "Completion", RestoreCompletion),
				LongRouteCacheRevision = hasCache ? cacheRevision : (long?)null
			};
			if (values["LastTokenFingerprint"] != TokenFingerprint(restored.LastOrderToken))
				throw new InvalidOperationException("Saved Repair token fingerprint was altered.");
			Validate(restored, handoff);
			return restored;
		}

		static void Validate(StealthRepairOwnerState state, StealthRepairHandoff handoff)
		{
			if (state.LastObservedTick < -1 || state.RouteProgress < 0 || state.RouteRevision < 0 ||
				state.LongRouteCacheRevision < 0 || !Ordered(state.MemberIds) || !Ordered(state.EnemyIds) ||
				state.Evaluations == null || state.Evaluations.Any(evaluation => evaluation == null) ||
				state.Evaluations.Select(evaluation => evaluation.Route.StableIdentity).Distinct().Count() !=
					state.Evaluations.Length || !Enum.IsDefined(typeof(StealthRepairDisposition), state.Disposition) ||
				!Enum.IsDefined(typeof(StealthRepairLiveCause), state.LiveCause))
				throw new InvalidOperationException("Invalid Repair private state.");
			if (!state.EntryValidated)
			{
				if (state.LastObservedTick != -1 || state.Fingerprint != null || state.MemberIds.Length != 0 ||
					state.EnemyIds.Length != 0 || state.Evaluations.Length != 0 || state.OptionId.HasValue ||
					state.RouteId.HasValue || state.RouteProgress != 0 || state.Danger.HasValue ||
					state.RouteRevision != 0 || state.LastOrderToken != null || state.Completion != null ||
					state.LongRouteCacheRevision.HasValue || state.Disposition != StealthRepairDisposition.Retain ||
					state.LiveCause != StealthRepairLiveCause.NoSafeRepair)
					throw new InvalidOperationException("Unvalidated Repair state must be pristine.");
				return;
			}

			if (state.LastObservedTick < 0 || string.IsNullOrEmpty(state.Fingerprint))
				throw new InvalidOperationException("Validated Repair state has no canonical live facts.");
			var routed = state.OptionId.HasValue && state.RouteId.HasValue && state.Danger.HasValue &&
				state.LastOrderToken != null;
			var retaining = state.LiveCause == StealthRepairLiveCause.Retreating ||
				state.LiveCause == StealthRepairLiveCause.Healing;
			if (routed != retaining || retaining != (state.Disposition == StealthRepairDisposition.Retain) ||
				(routed && !state.Evaluations.Any(evaluation =>
					evaluation.Option.ActorId == state.OptionId && evaluation.Route.StableIdentity == state.RouteId &&
					evaluation.IsSafe && StealthRepairResult.SameScore(evaluation.StandardDanger,
						state.Danger.Value))))
				throw new InvalidOperationException("Saved Repair route is inconsistent.");
			if ((state.Disposition == StealthRepairDisposition.ResumeFight) !=
					(state.LiveCause == StealthRepairLiveCause.NoSafeRepair) ||
				(state.Disposition == StealthRepairDisposition.Start) !=
					(state.LiveCause == StealthRepairLiveCause.RepairComplete) ||
				(state.Disposition == StealthRepairDisposition.SquadConstruction) !=
					(state.LiveCause == StealthRepairLiveCause.NoLiveMembers) ||
				(state.Completion != null) != (state.Disposition == StealthRepairDisposition.Start) ||
				(state.Disposition == StealthRepairDisposition.SquadConstruction && state.MemberIds.Length != 0))
				throw new InvalidOperationException("Saved Repair terminal evidence is forged.");
			if (state.LastOrderToken != null)
			{
				var token = state.LastOrderToken;
				var selected = routed ? state.Evaluations.Single(evaluation =>
					evaluation.Route.StableIdentity == state.RouteId) :
					state.Completion != null ? StealthRepairLiveDecision.SelectSafest(state.Evaluations) : null;
				var expectedActorIds = selected?.Facts.Members.Select(member => member.ActorId).ToArray();
				var completionActorIds = state.Completion?.Members.Select(member => member.ActorId)
					.ToArray();
				var terminalSubset = completionActorIds == null ||
					token.ActorIds.All(completionActorIds.Contains);
				if (token.Owner != handoff.Owner || token.Epoch != handoff.Epoch ||
					token.RouteRevision != state.RouteRevision || token.ActorIds.Any(id =>
						!handoff.DamagedMembers.Any(member => member.ActorId == id)) ||
					expectedActorIds == null || !token.ActorIds.SequenceEqual(expectedActorIds) ||
					!terminalSubset || token.RepairOptionActorId != selected.Option.ActorId ||
					token.RouteIdentity != selected.Route.StableIdentity)
					throw new InvalidOperationException("Saved Repair token is forged.");
			}
			else if (state.Completion != null && state.Evaluations.Length != 0)
				throw new InvalidOperationException("Tokenless Repair completion cannot claim order history.");

			if (state.LongRouteCacheRevision.HasValue && !state.Evaluations.Any(evaluation =>
				evaluation.Route.StableIdentity == state.RouteId &&
				evaluation.Route.RequiresStrategicRouting))
				throw new InvalidOperationException("Saved Repair cache metadata is not bound to a long route.");
		}

		static MiniYamlNode SerializeCause(StealthRepairHandoff handoff)
		{
			var nodes = new List<MiniYamlNode>
			{
				Field("EventId", handoff.DamageEventId), Field("Tick", handoff.DamageTick),
				Field("SourceActorId", handoff.DamageSourceActorId), Field("Amount", handoff.DamageAmount)
			};
			foreach (var member in handoff.DamagedMembers)
				nodes.Add(Node("DamagedMember", new[]
				{
					Field("Id", member.ActorId), Field("HP", member.HitPoints),
					Field("MaxHP", member.MaximumHitPoints)
				}));
			return Node("Cause", nodes);
		}

		static (long EventId, int Tick, uint Source, int Amount, StealthRepairDamagedMember[] Members)
			RestoreCause(MiniYamlNode node)
		{
			var values = UniqueExcept(node, "DamagedMember");
			if (values.Count != 4)
				throw new InvalidOperationException("Invalid Repair Damage cause field set.");
			var members = node.Value.Nodes.Where(child => child.Key == "DamagedMember")
				.Select(RestoreDamagedMember).ToArray();
			return (Read<long>(values, "EventId"), Read<int>(values, "Tick"),
				Read<uint>(values, "SourceActorId"), Read<int>(values, "Amount"), members);
		}

		static bool SameCause((long EventId, int Tick, uint Source, int Amount,
			StealthRepairDamagedMember[] Members) cause, StealthRepairHandoff handoff)
		{
			return cause.EventId == handoff.DamageEventId && cause.Tick == handoff.DamageTick &&
				cause.Source == handoff.DamageSourceActorId && cause.Amount == handoff.DamageAmount &&
				cause.Members.Select(member => (member.ActorId, member.HitPoints, member.MaximumHitPoints))
				.SequenceEqual(handoff.DamagedMembers.Select(member =>
					(member.ActorId, member.HitPoints, member.MaximumHitPoints)));
		}

		static MiniYamlNode SerializeResume(StealthRepairResumeContext resume)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Owner", resume.Owner.ToString()), Field("Epoch", resume.Epoch.Value),
				new MiniYamlNode("Fingerprint", resume.ContextFingerprint),
				Field("HasTarget", resume.SelectedTargetActorId.HasValue),
				Field("TargetId", resume.SelectedTargetActorId ?? 0),
				Field("TargetCell", resume.SelectedTargetCurrentCell ?? default(CPos))
			};
			AddIds(nodes, "MemberId", resume.MemberActorIds);
			AddIds(nodes, "EnemyId", resume.EnemyActorIds);
			return Node("Resume", nodes);
		}

		static StealthRepairResumeContext RestoreResume(MiniYamlNode node, StealthApproachMission mission)
		{
			var values = UniqueExcept(node, "MemberId", "EnemyId");
			if (values.Count != 6)
				throw new InvalidOperationException("Invalid Repair resume field set.");
			var hasTarget = Read<bool>(values, "HasTarget");
			var target = Read<uint>(values, "TargetId");
			var cell = Read<CPos>(values, "TargetCell");
			if (!hasTarget && (target != 0 || cell != default(CPos)))
				throw new InvalidOperationException("Noncanonical absent Repair resume target.");
			return new StealthRepairResumeContext(Read<BehaviorId>(values, "Owner"),
				new OwnershipEpoch(Read<long>(values, "Epoch")), mission,
				ReadIds(node, "MemberId"), ReadIds(node, "EnemyId"),
				hasTarget ? target : (uint?)null, hasTarget ? cell : (CPos?)null, values["Fingerprint"]);
		}

		static bool SameResume(StealthRepairResumeContext left, StealthRepairResumeContext right)
		{
			return left.Owner == right.Owner && left.Epoch == right.Epoch &&
				left.ContextFingerprint == right.ContextFingerprint &&
				left.MemberActorIds.SequenceEqual(right.MemberActorIds) &&
				left.EnemyActorIds.SequenceEqual(right.EnemyActorIds) &&
				left.SelectedTargetActorId == right.SelectedTargetActorId &&
				left.SelectedTargetCurrentCell == right.SelectedTargetCurrentCell;
		}

		static MiniYamlNode SerializeEvaluation(StealthRepairRouteEvaluation evaluation)
		{
			var nodes = new List<MiniYamlNode>
			{
				Field("OptionId", evaluation.Option.ActorId), Field("OptionCell", evaluation.Option.CurrentCell),
				Field("RouteId", evaluation.Route.StableIdentity),
				Field("Passable", evaluation.Route.IsPassable),
				Field("Long", evaluation.Route.RequiresStrategicRouting),
				Field("Detector", evaluation.Route.HasDetectorCoverage),
				Field("FormationCloaked", evaluation.Facts.FormationCloaked),
				new MiniYamlNode("Threat", Format(evaluation.StandardDanger.ThreatRating)),
				new MiniYamlNode("Crossover", Format(evaluation.StandardDanger.Crossover))
			};
			foreach (var cell in evaluation.Route.Cells)
				nodes.Add(Field("Cell", cell));
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
					Field("Id", enemy.ActorId),
					new MiniYamlNode("Type", enemy.ActorType), Field("Cell", enemy.CurrentCell),
					Field("HP", enemy.HitPoints), Field("MaxHP", enemy.MaximumHitPoints),
					Field("Range", enemy.CurrentWeaponRangeCells), Field("Detector", enemy.IsDetector)
				}));
			return Node("Evaluation", nodes);
		}

		static StealthRepairRouteEvaluation RestoreEvaluation(MiniYamlNode node)
		{
			var values = UniqueExcept(node, "Cell", "Member", "Enemy");
			if (values.Count != 9 || !Read<bool>(values, "Passable"))
				throw new InvalidOperationException("Invalid Repair route evaluation.");
			var option = new StealthRepairOptionSnapshot(Read<uint>(values, "OptionId"),
				Read<CPos>(values, "OptionCell"));
			var cells = node.Value.Nodes.Where(child => child.Key == "Cell")
				.Select(child => FieldLoader.GetValue<CPos>("Cell", child.Value.Value)).ToArray();
			var route = new StealthRepairRouteSnapshot(Read<uint>(values, "RouteId"), option.ActorId,
				cells, true, Read<bool>(values, "Long"), Read<bool>(values, "Detector"));
			var facts = new StealthRepairThreatFacts(option.ActorId,
				node.Value.Nodes.Where(child => child.Key == "Member").Select(RestoreMember),
				node.Value.Nodes.Where(child => child.Key == "Enemy").Select(RestoreEnemy), cells,
				Read<bool>(values, "FormationCloaked"), Read<bool>(values, "Detector"));
			return new StealthRepairRouteEvaluation(option, route, facts,
				new StealthTargetThreatScore(ReadDouble(values, "Threat"),
					ReadDouble(values, "Crossover")));
		}

		static StealthRepairMemberSnapshot RestoreMember(MiniYamlNode node)
		{
			var values = Unique(node.Value.Nodes);
			if (values.Count != 5)
				throw new InvalidOperationException("Invalid saved Repair member.");
			return new StealthRepairMemberSnapshot(Read<uint>(values, "Id"), Read<CPos>(values, "Cell"),
				Read<int>(values, "Range"), Read<int>(values, "HP"), Read<int>(values, "MaxHP"));
		}

		static StealthRepairEnemySnapshot RestoreEnemy(MiniYamlNode node)
		{
			var values = Unique(node.Value.Nodes);
			if (values.Count != 7)
				throw new InvalidOperationException("Invalid saved Repair enemy.");
			return new StealthRepairEnemySnapshot(Read<uint>(values, "Id"), values["Type"],
				Read<CPos>(values, "Cell"), Read<int>(values, "HP"), Read<int>(values, "MaxHP"),
				Read<int>(values, "Range"), Read<bool>(values, "Detector"));
		}

		static MiniYamlNode SerializeOrder(StealthRepairOrderToken token)
		{
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Owner", token.Owner.ToString()),
				Field("Epoch", token.Epoch.Value), Field("OptionId", token.RepairOptionActorId),
				Field("RouteId", token.RouteIdentity), new MiniYamlNode("Kind", token.Kind.ToString()),
				Field("RouteRevision", token.RouteRevision), Field("ActivityRevision", token.ActivityRevision)
			};
			AddIds(nodes, "ActorId", token.ActorIds);
			return Node("LastOrder", nodes);
		}

		static StealthRepairOrderToken RestoreOrder(MiniYamlNode node)
		{
			var values = UniqueExcept(node, "ActorId");
			if (values.Count != 7 || Read<BehaviorId>(values, "Owner") != BehaviorId.Repair)
				throw new InvalidOperationException("Invalid saved Repair order.");
			return new StealthRepairOrderToken(BehaviorId.Repair,
				new OwnershipEpoch(Read<long>(values, "Epoch")), ReadIds(node, "ActorId"),
				Read<uint>(values, "OptionId"), Read<uint>(values, "RouteId"),
				Read<StealthRepairOrderKind>(values, "Kind"), Read<long>(values, "RouteRevision"),
				Read<long>(values, "ActivityRevision"));
		}

		static MiniYamlNode SerializeCompletion(StealthRepairCompletionEvidence completion)
		{
			var nodes = new List<MiniYamlNode> { Field("Tick", completion.Tick) };
			foreach (var member in completion.Members)
				nodes.Add(Node("Member", new[]
				{
					Field("Id", member.ActorId), Field("HP", member.HitPoints),
					Field("MaxHP", member.MaximumHitPoints)
				}));
			return Node("Completion", nodes);
		}

		static StealthRepairCompletionEvidence RestoreCompletion(MiniYamlNode node)
		{
			var values = UniqueExcept(node, "Member");
			if (values.Count != 1)
				throw new InvalidOperationException("Invalid Repair completion.");
			return new StealthRepairCompletionEvidence(Read<int>(values, "Tick"),
				node.Value.Nodes.Where(child => child.Key == "Member").Select(RestoreDamagedMember));
		}

		static StealthRepairDamagedMember RestoreDamagedMember(MiniYamlNode node)
		{
			var values = Unique(node.Value.Nodes);
			if (values.Count != 3)
				throw new InvalidOperationException("Invalid damaged Repair member.");
			return new StealthRepairDamagedMember(Read<uint>(values, "Id"),
				Read<int>(values, "HP"), Read<int>(values, "MaxHP"));
		}

		static MiniYamlNode Field<T>(string key, T value) { return new MiniYamlNode(key, FieldSaver.FormatValue(value)); }
		static MiniYamlNode Node(string key, IEnumerable<MiniYamlNode> nodes) { return new MiniYamlNode(key, new MiniYaml("", nodes.ToList())); }
		static string Format(double value) { return value.ToString("R", CultureInfo.InvariantCulture); }
		static string TokenFingerprint(StealthRepairOrderToken token)
		{
			if (token == null)
				return "";

			return string.Join("|", token.Owner.ToString(),
				token.Epoch.Value.ToString(CultureInfo.InvariantCulture),
				token.RepairOptionActorId.ToString(CultureInfo.InvariantCulture),
				token.RouteIdentity.ToString(CultureInfo.InvariantCulture), token.Kind.ToString(),
				token.RouteRevision.ToString(CultureInfo.InvariantCulture),
				token.ActivityRevision.ToString(CultureInfo.InvariantCulture),
				string.Join(",", token.ActorIds.Select(id => id.ToString(CultureInfo.InvariantCulture))));
		}

		static void AddIds(List<MiniYamlNode> nodes, string key, IEnumerable<uint> ids) { foreach (var id in ids) nodes.Add(Field(key, id)); }
		static uint[] ReadIds(MiniYamlNode node, string key) { return node.Value.Nodes.Where(child => child.Key == key).Select(child => FieldLoader.GetValue<uint>(key, child.Value.Value)).ToArray(); }
		static MiniYamlNode Required(MiniYamlNode node, string key) { var matches = node.Value.Nodes.Where(child => child.Key == key).ToArray(); if (matches.Length != 1) throw new InvalidOperationException("Missing or duplicate Repair node: " + key); return matches[0]; }
		static T ReadOptional<T>(MiniYamlNode node, string key, Func<MiniYamlNode, T> read) where T : class { var matches = node.Value.Nodes.Where(child => child.Key == key).ToArray(); return matches.Length == 0 ? null : read(matches[0]); }
		static void RequireCount(MiniYamlNode node, string key, int count) { if (node.Value.Nodes.Count(child => child.Key == key) != count) throw new InvalidOperationException("Invalid Repair node count: " + key); }
		static void RequireOptional(MiniYamlNode node, string key) { if (node.Value.Nodes.Count(child => child.Key == key) > 1) throw new InvalidOperationException("Duplicate Repair node: " + key); }
		static Dictionary<string, string> UniqueExcept(MiniYamlNode node, params string[] repeated) { var excluded = new HashSet<string>(repeated, StringComparer.Ordinal); return Unique(node.Value.Nodes.Where(child => !excluded.Contains(child.Key))); }
		static Dictionary<string, string> Unique(IEnumerable<MiniYamlNode> nodes) { var result = new Dictionary<string, string>(StringComparer.Ordinal); try { foreach (var node in nodes) result.Add(node.Key, node.Value.Value); } catch (ArgumentException ex) { throw new InvalidOperationException("Duplicate Repair field.", ex); } return result; }
		static bool Ordered(uint[] ids) { return ids != null && ids.All(id => id != 0) && ids.SequenceEqual(ids.OrderBy(id => id)) && ids.Distinct().Count() == ids.Length; }
		static double ReadDouble(Dictionary<string, string> values, string key) { if (!values.TryGetValue(key, out var value) || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed) || parsed < 0) throw new InvalidOperationException("Invalid Repair score: " + key); return parsed; }
		static T Read<T>(Dictionary<string, string> values, string key)
		{
			if (!values.TryGetValue(key, out var value)) throw new InvalidOperationException("Missing Repair field: " + key);
			if (typeof(T).IsEnum) { if (!Enum.TryParse(typeof(T), value, out var parsed) || !Enum.IsDefined(typeof(T), parsed)) throw new InvalidOperationException("Invalid Repair enum: " + key); return (T)parsed; }
			return FieldLoader.GetValue<T>(key, value);
		}
	}
}
