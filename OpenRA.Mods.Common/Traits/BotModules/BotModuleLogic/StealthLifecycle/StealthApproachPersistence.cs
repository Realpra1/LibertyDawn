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
	sealed class StealthApproachRouteThreat
	{
		public CPos Cell { get; }
		public StealthTargetThreatScore Score { get; }
		public bool HasDetectorCoverage { get; }
		public bool PlannedActionRevealsFormation { get; }

		public StealthApproachRouteThreat(CPos cell, StealthTargetThreatScore score,
			bool hasDetectorCoverage, bool plannedActionRevealsFormation)
		{
			Cell = cell;
			Score = score;
			HasDetectorCoverage = hasDetectorCoverage;
			PlannedActionRevealsFormation = plannedActionRevealsFormation;
		}
	}

	sealed class StealthApproachPrivateState
	{
		public CPos[] Route { get; set; }
		public StealthApproachRouteThreat[] RouteThreats { get; set; }
		public int RouteIndex { get; set; }
		public StealthApproachArrivalClassification ArrivalClassification { get; set; }
		public StealthApproachDisposition Disposition { get; set; }
		public StealthTargetThreatScore? LocalThreatScore { get; set; }
		public uint[] LastIssuedActorIds { get; set; }
		public CPos? LastIssuedDestination { get; set; }
		public uint[] LiveDefenderActorIds { get; set; }
	}

	static class StealthApproachPersistence
	{
		const int PrivateSaveVersion = 1;

		public static MiniYamlNode Serialize(string key, StealthApproachHandoff handoff,
			StealthApproachMission mission, IReadOnlyList<CPos> route,
			IReadOnlyList<StealthApproachRouteThreat> routeThreats, int routeIndex,
			StealthApproachArrivalClassification classification, StealthApproachDisposition disposition,
			StealthTargetThreatScore? localScore, IReadOnlyList<uint> lastActorIds,
			CPos? lastDestination, IReadOnlyList<uint> liveDefenderActorIds)
		{
			if (route.Count != routeThreats.Count || routeIndex < 0 || routeIndex > route.Count ||
				route.Where((cell, index) => cell != routeThreats[index].Cell).Any())
				throw new InvalidOperationException("Approach route progress and threat state are inconsistent.");

			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", PrivateSaveVersion.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Owner", handoff.Owner.ToString()),
				new MiniYamlNode("Epoch", handoff.Epoch.Value.ToString(CultureInfo.InvariantCulture)),
				SerializeMission(mission),
				new MiniYamlNode("RouteIndex", routeIndex.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("ArrivalClassification", classification.ToString()),
				new MiniYamlNode("Disposition", disposition.ToString()),
				new MiniYamlNode("HasLocalThreatScore", FieldSaver.FormatValue(localScore.HasValue)),
				new MiniYamlNode("LocalThreatRating", FormatDouble(localScore?.ThreatRating ?? 0)),
				new MiniYamlNode("LocalCrossover", FormatDouble(localScore?.Crossover ?? 0)),
				new MiniYamlNode("HasLastIssuedDestination", FieldSaver.FormatValue(lastDestination.HasValue)),
				new MiniYamlNode("LastIssuedDestination", FieldSaver.FormatValue(lastDestination ?? default))
			};
			for (var i = 0; i < route.Count; i++)
				nodes.Add(new MiniYamlNode("RouteCell", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("Cell", FieldSaver.FormatValue(route[i])),
					new MiniYamlNode("ThreatRating", FormatDouble(routeThreats[i].Score.ThreatRating)),
					new MiniYamlNode("Crossover", FormatDouble(routeThreats[i].Score.Crossover)),
					new MiniYamlNode("HasDetectorCoverage",
						FieldSaver.FormatValue(routeThreats[i].HasDetectorCoverage)),
					new MiniYamlNode("PlannedActionRevealsFormation",
						FieldSaver.FormatValue(routeThreats[i].PlannedActionRevealsFormation))
				}));
			foreach (var actorId in lastActorIds)
				nodes.Add(new MiniYamlNode("LastIssuedActorId", FieldSaver.FormatValue(actorId)));
			foreach (var actorId in liveDefenderActorIds)
				nodes.Add(new MiniYamlNode("LiveDefenderActorId", FieldSaver.FormatValue(actorId)));

			return new MiniYamlNode(key, "", nodes);
		}

		public static StealthApproachPrivateState Restore(MiniYamlNode node,
			StealthApproachHandoff handoff, StealthApproachMission mission)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));

			var values = Unique(node.Value.Nodes.Where(child => child.Key != "Mission" &&
				child.Key != "RouteCell" && child.Key != "LastIssuedActorId" &&
				child.Key != "LiveDefenderActorId"), "Approach private state");
			if (!ReadInt(values, "Version", out var version) || version != PrivateSaveVersion)
				throw new InvalidOperationException("Unsupported stealth Approach private save schema.");
			if (!values.TryGetValue("Owner", out var ownerText) ||
				!Enum.TryParse(ownerText, out BehaviorId owner) || owner != BehaviorId.Approach)
				throw new InvalidOperationException("Invalid stealth Approach owner in private save state.");
			if (!ReadLong(values, "Epoch", out var epoch) || epoch <= 0 ||
				owner != handoff.Owner || epoch != handoff.Epoch.Value)
				throw new InvalidOperationException("Stale stealth Approach ownership in private save state.");

			var missionNodes = node.Value.Nodes.Where(child => child.Key == "Mission").ToArray();
			if (missionNodes.Length != 1 || Canonical(missionNodes[0]) != Canonical(SerializeMission(mission)))
				throw new InvalidOperationException("Approach private state does not match its immutable mission.");

			var routeThreats = node.Value.Nodes.Where(child => child.Key == "RouteCell")
				.Select(RestoreRouteThreat).ToArray();
			if (!ReadInt(values, "RouteIndex", out var routeIndex) || routeIndex < 0 ||
				routeIndex > routeThreats.Length)
				throw new InvalidOperationException("Invalid Approach route progress in private save state.");
			if (!ReadEnum(values, "ArrivalClassification", out StealthApproachArrivalClassification classification) ||
				!ReadEnum(values, "Disposition", out StealthApproachDisposition disposition) ||
				!ConsistentOutcome(classification, disposition))
				throw new InvalidOperationException("Invalid Approach arrival outcome in private save state.");

			var hasLocal = Read<bool>(values, "HasLocalThreatScore");
			var localThreat = ReadScore(values, "LocalThreatRating", "LocalCrossover");
			if (!hasLocal && (localThreat.ThreatRating != 0 || localThreat.Crossover != 0))
				throw new InvalidOperationException("Invalid normalized Approach local threat state.");
			var hasDestination = Read<bool>(values, "HasLastIssuedDestination");
			var destination = Read<CPos>(values, "LastIssuedDestination");
			var actorIds = node.Value.Nodes.Where(child => child.Key == "LastIssuedActorId")
				.Select(child => FieldLoader.GetValue<uint>(child.Key, child.Value.Value)).ToArray();
			if (actorIds.Any(id => id == 0) || !actorIds.SequenceEqual(actorIds.OrderBy(id => id)) ||
				actorIds.Distinct().Count() != actorIds.Length ||
				(actorIds.Length == 0) != !hasDestination ||
				(!hasDestination && destination != default) ||
				(routeThreats.Length == 0 && hasDestination))
				throw new InvalidOperationException("Invalid Approach movement deduplication state.");
			var defenderIds = node.Value.Nodes.Where(child => child.Key == "LiveDefenderActorId")
				.Select(child => FieldLoader.GetValue<uint>(child.Key, child.Value.Value)).ToArray();
			if (defenderIds.Any(id => id == 0) || !defenderIds.SequenceEqual(defenderIds.OrderBy(id => id)) ||
				defenderIds.Distinct().Count() != defenderIds.Length ||
				(disposition == StealthApproachDisposition.CrushEvaluation) != (defenderIds.Length != 0))
				throw new InvalidOperationException("Invalid Approach selected handoff state.");

			return new StealthApproachPrivateState
			{
				Route = routeThreats.Select(item => item.Cell).ToArray(),
				RouteThreats = routeThreats,
				RouteIndex = routeIndex,
				ArrivalClassification = classification,
				Disposition = disposition,
				LocalThreatScore = hasLocal ? localThreat : (StealthTargetThreatScore?)null,
				LastIssuedActorIds = actorIds,
				LastIssuedDestination = hasDestination ? destination : (CPos?)null,
				LiveDefenderActorIds = defenderIds
			};
		}

		internal static MiniYamlNode SerializeMission(StealthApproachMission mission)
		{
			var option = mission.TargetOption;
			var value = option.ValueOption;
			var facts = value.ThreatFacts;
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("StrategicCell", FieldSaver.FormatValue(mission.StrategicCell)),
				new MiniYamlNode("StableTargetActorId", FieldSaver.FormatValue(mission.StableTargetActorId)),
				new MiniYamlNode("EstimatedTravelMilliseconds", FieldSaver.FormatValue(mission.EstimatedTravelMilliseconds)),
				new MiniYamlNode("MinimumSquadSeparationSquared",
					mission.MinimumSquadSeparationSquared.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("SeparationCreditMilliseconds",
					mission.SeparationCreditMilliseconds.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("AdjustedTravelCostMilliseconds",
					mission.AdjustedTravelCostMilliseconds.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("StrategicValue", value.StrategicValue.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("IsIncumbent", FieldSaver.FormatValue(value.IsIncumbent)),
				new MiniYamlNode("ThreatRating", FormatDouble(option.ThreatRating)),
				new MiniYamlNode("Crossover", FormatDouble(option.Crossover)),
				new MiniYamlNode("FormationCloaked", FieldSaver.FormatValue(facts.FormationCloaked)),
				new MiniYamlNode("HasDetectorCoverage", FieldSaver.FormatValue(facts.HasDetectorCoverage)),
				new MiniYamlNode("PlannedActionRevealsFormation",
					FieldSaver.FormatValue(facts.PlannedActionRevealsFormation))
			};
			foreach (var target in value.StrategicTargets)
				nodes.Add(new MiniYamlNode("Target", "", new List<MiniYamlNode>
				{
					new MiniYamlNode("StableActorId", FieldSaver.FormatValue(target.StableActorId)),
					new MiniYamlNode("StrategicCell", FieldSaver.FormatValue(target.StrategicCell)),
					new MiniYamlNode("ConfiguredPriority", FieldSaver.FormatValue(target.ConfiguredPriority)),
					new MiniYamlNode("ActorValue", FieldSaver.FormatValue(target.ActorValue)),
					new MiniYamlNode("HitPoints", FieldSaver.FormatValue(target.HitPoints)),
					new MiniYamlNode("MaximumHitPoints", FieldSaver.FormatValue(target.MaximumHitPoints))
				}));
			foreach (var member in facts.FriendlyGroup)
				nodes.Add(SerializeGroup("Friendly", member));
			foreach (var member in facts.EnemyGroup)
				nodes.Add(SerializeGroup("Enemy", member));
			return new MiniYamlNode("Mission", "", nodes);
		}

		static MiniYamlNode SerializeGroup(string key, StealthCombatGroupSnapshot member)
		{
			return new MiniYamlNode(key, "", new List<MiniYamlNode>
			{
				new MiniYamlNode("ActorType", member.ActorType),
				new MiniYamlNode("Count", FieldSaver.FormatValue(member.Count)),
				new MiniYamlNode("EconomicValue", FieldSaver.FormatValue(member.EconomicValue))
			});
		}

		static StealthApproachRouteThreat RestoreRouteThreat(MiniYamlNode node)
		{
			var values = Unique(node.Value.Nodes, "Approach route threat");
			return new StealthApproachRouteThreat(Read<CPos>(values, "Cell"),
				ReadScore(values, "ThreatRating", "Crossover"),
				Read<bool>(values, "HasDetectorCoverage"),
				Read<bool>(values, "PlannedActionRevealsFormation"));
		}

		static StealthTargetThreatScore ReadScore(Dictionary<string, string> values,
			string threatKey, string crossoverKey)
		{
			if (!ReadDouble(values, threatKey, false, out var threat) ||
				!ReadDouble(values, crossoverKey, true, out var crossover))
				throw new InvalidOperationException("Invalid Approach threat context in private save state.");
			return new StealthTargetThreatScore(threat, crossover);
		}

		static bool ConsistentOutcome(StealthApproachArrivalClassification classification,
			StealthApproachDisposition disposition)
		{
			return disposition == StealthApproachDisposition.UndefendedAttack ?
				classification == StealthApproachArrivalClassification.Undefended :
				disposition == StealthApproachDisposition.CrushEvaluation ?
				classification == StealthApproachArrivalClassification.Defended :
				classification == StealthApproachArrivalClassification.None;
		}

		internal static string Canonical(MiniYamlNode node)
		{
			return new List<MiniYamlNode> { node }.WriteToString();
		}

		static Dictionary<string, string> Unique(IEnumerable<MiniYamlNode> nodes, string context)
		{
			var values = new Dictionary<string, string>(StringComparer.Ordinal);
			try
			{
				foreach (var node in nodes)
					values.Add(node.Key, node.Value.Value);
			}
			catch (ArgumentException ex)
			{
				throw new InvalidOperationException("Duplicate " + context + " field.", ex);
			}

			return values;
		}

		static T Read<T>(Dictionary<string, string> values, string key)
		{
			if (!values.TryGetValue(key, out var value))
				throw new InvalidOperationException("Missing Approach private state field: " + key);
			return FieldLoader.GetValue<T>(key, value);
		}

		static bool ReadInt(Dictionary<string, string> values, string key, out int value)
		{
			value = 0;
			return values.TryGetValue(key, out var text) &&
				int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}

		static bool ReadLong(Dictionary<string, string> values, string key, out long value)
		{
			value = 0;
			return values.TryGetValue(key, out var text) &&
				long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}

		static bool ReadDouble(Dictionary<string, string> values, string key,
			bool allowInfinity, out double value)
		{
			value = 0;
			return values.TryGetValue(key, out var text) &&
				double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
				!double.IsNaN(value) && value >= 0 && (allowInfinity || !double.IsInfinity(value));
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
