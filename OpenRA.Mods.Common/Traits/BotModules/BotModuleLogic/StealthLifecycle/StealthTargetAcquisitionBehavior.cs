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
	/// Disabled TargetAcquisition owner. It searches only an immutable strategic-cache snapshot;
	/// it has no World, Actor, order, timer, event, or live-combat dependency.
	/// </summary>
	public sealed class StealthTargetAcquisitionBehavior
	{
		const int PrivateSaveVersion = 2;
		public const int MaximumOptions = 10;
		public const int MaximumTravelSeconds = 30;
		public const int MaximumPrimitiveOperations = 65536;
		public const int MaximumFallbackSteps = 4;

		readonly StealthBehaviorHandoff handoff;
		readonly IStealthTargetAcquisitionCache cache;

		public StealthTargetAcquisitionBehavior(StealthBehaviorHandoff handoff,
			IStealthTargetAcquisitionCache cache)
		{
			if (handoff == null)
				throw new ArgumentNullException(nameof(handoff));
			if (handoff.Owner != BehaviorId.TargetAcquisition)
				throw new ArgumentException(
					"The TargetAcquisition behavior requires TargetAcquisition ownership.", nameof(handoff));

			this.handoff = handoff;
			this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
		}

		public StealthTargetAcquisitionResult Execute(CPos activeSquadCenter,
			CPos? incumbentStrategicCell = null)
		{
			var snapshot = cache.ReadSnapshot() ??
				throw new InvalidOperationException("The TargetAcquisition cache returned no snapshot.");
			if (!Contains(snapshot, activeSquadCenter))
				throw new ArgumentOutOfRangeException(nameof(activeSquadCenter));
			if (incumbentStrategicCell != null && !Contains(snapshot, incumbentStrategicCell.Value))
				throw new ArgumentOutOfRangeException(nameof(incumbentStrategicCell));

			var enemyCells = snapshot.EnemyStrategicCells.Distinct()
				.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
			var requiredIndex = incumbentStrategicCell == null ? -1 :
				Array.IndexOf(enemyCells, incumbentStrategicCell.Value);
			var regularLimit = incumbentStrategicCell == null ? MaximumOptions : MaximumOptions - 1;
			var search = StealthAIThreatGeometry.StartReachableTargetCellSearch(
				snapshot.Danger.ToArray(), snapshot.Width, snapshot.Height,
				activeSquadCenter.X, activeSquadCenter.Y, enemyCells, snapshot.RouteThreatPenalty,
				regularLimit, requiredIndex,
				MaximumTravelSeconds / snapshot.SecondsPerCostUnit);
			search.Advance(MaximumPrimitiveOperations);

			var reachable = search.Complete ? search.Result.Targets :
				new List<StealthAIThreatGeometry.ReachableTargetCell>();
			var candidates = reachable.Select(target => new
				{
					Cell = enemyCells[target.TargetIndex],
					TravelMilliseconds = ToTravelMilliseconds(
						target.RouteCost, snapshot.SecondsPerCostUnit),
					Target = target
				})
				.Where(candidate => candidate.TravelMilliseconds <= MaximumTravelSeconds * 1000)
				.OrderBy(candidate => candidate.TravelMilliseconds)
				.ThenBy(candidate => candidate.Cell.Y).ThenBy(candidate => candidate.Cell.X)
				.ToArray();

			var options = new List<StealthTargetOption>(MaximumOptions);
			if (incumbentStrategicCell != null)
			{
				var incumbent = candidates.FirstOrDefault(candidate =>
					candidate.Cell == incumbentStrategicCell.Value);
				options.Add(new StealthTargetOption(incumbentStrategicCell.Value,
					incumbent?.TravelMilliseconds, true,
					TargetsAt(snapshot, incumbentStrategicCell.Value),
					ThreatFactsAt(snapshot, incumbentStrategicCell.Value)));
			}

			foreach (var candidate in candidates)
			{
				if (options.Count == MaximumOptions)
					break;
				if (options.Any(option => option.StrategicCell == candidate.Cell))
					continue;

				options.Add(new StealthTargetOption(candidate.Cell,
					candidate.TravelMilliseconds, false, TargetsAt(snapshot, candidate.Cell),
					ThreatFactsAt(snapshot, candidate.Cell)));
			}

			var needsRescan = options.Count < MaximumOptions;
			CPos? moveCloser = needsRescan && enemyCells.Length != 0 ?
				MoveCloser(activeSquadCenter, enemyCells, snapshot, reachable) : (CPos?)null;
			var disposition = !needsRescan ?
				StealthTargetAcquisitionDisposition.ReadyForValueFilter :
				enemyCells.Length != 0 ? StealthTargetAcquisitionDisposition.MoveCloserAndRescan :
				StealthTargetAcquisitionDisposition.AwaitingCache;
			var result = new StealthTargetAcquisitionResult(handoff, activeSquadCenter,
				incumbentStrategicCell, disposition, options, moveCloser,
				search.PrimitiveOperations, search.ExpandedCells);
			ValidatePersistentResult(result);
			return result;
		}

		public MiniYamlNode SerializePrivateState(StealthTargetAcquisitionResult result,
			string key = "TargetAcquisition")
		{
			ValidateOwnedResult(result);
			var nodes = new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", PrivateSaveVersion.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Owner", result.Handoff.Owner.ToString()),
				new MiniYamlNode("Epoch", result.Handoff.Epoch.Value.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("ActiveSquadCenter", FieldSaver.FormatValue(result.ActiveSquadCenter)),
				new MiniYamlNode("IncumbentStrategicCell", FieldSaver.FormatValue(result.IncumbentStrategicCell)),
				new MiniYamlNode("Disposition", result.Disposition.ToString()),
				new MiniYamlNode("MoveCloserStrategicCell", FieldSaver.FormatValue(result.MoveCloserStrategicCell)),
				new MiniYamlNode("PrimitiveOperations", result.PrimitiveOperations.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("ExpandedCells", result.ExpandedCells.ToString(CultureInfo.InvariantCulture))
			};

			foreach (var option in result.Options)
			{
				var optionNodes = new List<MiniYamlNode>
				{
					new MiniYamlNode("StrategicCell", FieldSaver.FormatValue(option.StrategicCell)),
					new MiniYamlNode("EstimatedTravelMilliseconds",
						FieldSaver.FormatValue(option.EstimatedTravelMilliseconds)),
					new MiniYamlNode("IsIncumbent", FieldSaver.FormatValue(option.IsIncumbent)),
					new MiniYamlNode("FormationCloaked",
						FieldSaver.FormatValue(option.ThreatFacts.FormationCloaked)),
					new MiniYamlNode("HasDetectorCoverage",
						FieldSaver.FormatValue(option.ThreatFacts.HasDetectorCoverage)),
					new MiniYamlNode("PlannedActionRevealsFormation",
						FieldSaver.FormatValue(option.ThreatFacts.PlannedActionRevealsFormation))
				};
				foreach (var target in option.StrategicTargets)
					optionNodes.Add(SerializeTarget(target));
				foreach (var member in option.ThreatFacts.FriendlyGroup)
					optionNodes.Add(SerializeGroupMember("Friendly", member));
				foreach (var member in option.ThreatFacts.EnemyGroup)
					optionNodes.Add(SerializeGroupMember("Enemy", member));
				nodes.Add(new MiniYamlNode("Option", "", optionNodes));
			}

			return new MiniYamlNode(key, "", nodes);
		}

		public StealthTargetAcquisitionResult RestorePrivateState(MiniYamlNode node)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));

			var values = ReadUniqueValues(node.Value.Nodes.Where(child => child.Key != "Option"),
				"TargetAcquisition private state");
			if (!TryReadInt(values, "Version", out var version) || version != PrivateSaveVersion)
				throw new InvalidOperationException("Unsupported stealth TargetAcquisition private save schema.");
			if (!values.TryGetValue("Owner", out var ownerText) ||
				!Enum.TryParse(ownerText, out BehaviorId owner) || owner != BehaviorId.TargetAcquisition)
				throw new InvalidOperationException("Invalid stealth TargetAcquisition owner in private save state.");
			if (!TryReadLong(values, "Epoch", out var epoch) || epoch <= 0 ||
				owner != handoff.Owner || epoch != handoff.Epoch.Value)
				throw new InvalidOperationException("Stale stealth TargetAcquisition ownership in private save state.");
			if (!values.TryGetValue("Disposition", out var dispositionText) ||
				!Enum.TryParse(dispositionText, out StealthTargetAcquisitionDisposition disposition) ||
				!Enum.IsDefined(typeof(StealthTargetAcquisitionDisposition), disposition))
				throw new InvalidOperationException("Invalid stealth TargetAcquisition disposition in private save state.");

			var result = new StealthTargetAcquisitionResult(handoff,
				Read<CPos>(values, "ActiveSquadCenter"),
				Read<CPos?>(values, "IncumbentStrategicCell"), disposition,
				node.Value.Nodes.Where(child => child.Key == "Option").Select(RestoreOption),
				Read<CPos?>(values, "MoveCloserStrategicCell"),
				ReadNonnegativeInt(values, "PrimitiveOperations"),
				ReadNonnegativeInt(values, "ExpandedCells"));
			ValidateOwnedResult(result);
			return result;
		}

		static StealthTargetOption RestoreOption(MiniYamlNode node)
		{
			var values = ReadUniqueValues(node.Value.Nodes.Where(child => child.Key != "Target" &&
				child.Key != "Friendly" && child.Key != "Enemy"),
				"TargetAcquisition option");
			var cell = Read<CPos>(values, "StrategicCell");
			var facts = new StealthTargetThreatFacts(cell,
				node.Value.Nodes.Where(child => child.Key == "Friendly").Select(RestoreGroupMember),
				node.Value.Nodes.Where(child => child.Key == "Enemy").Select(RestoreGroupMember),
				Read<bool>(values, "FormationCloaked"), Read<bool>(values, "HasDetectorCoverage"),
				Read<bool>(values, "PlannedActionRevealsFormation"));
			return new StealthTargetOption(cell,
				Read<int?>(values, "EstimatedTravelMilliseconds"),
				Read<bool>(values, "IsIncumbent"),
				node.Value.Nodes.Where(child => child.Key == "Target").Select(RestoreTarget), facts);
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
			var values = ReadUniqueValues(node.Value.Nodes, "TargetAcquisition combat group member");
			if (!values.TryGetValue("ActorType", out var actorType))
				throw new InvalidOperationException("Missing TargetAcquisition private state field: ActorType");
			return new StealthCombatGroupSnapshot(actorType,
				Read<int>(values, "Count"), Read<int>(values, "EconomicValue"));
		}

		static MiniYamlNode SerializeTarget(StealthStrategicTargetSnapshot target)
		{
			return new MiniYamlNode("Target", "", new List<MiniYamlNode>
			{
				new MiniYamlNode("StableActorId", FieldSaver.FormatValue(target.StableActorId)),
				new MiniYamlNode("StrategicCell", FieldSaver.FormatValue(target.StrategicCell)),
				new MiniYamlNode("ConfiguredPriority", FieldSaver.FormatValue(target.ConfiguredPriority)),
				new MiniYamlNode("ActorValue", FieldSaver.FormatValue(target.ActorValue)),
				new MiniYamlNode("HitPoints", FieldSaver.FormatValue(target.HitPoints)),
				new MiniYamlNode("MaximumHitPoints", FieldSaver.FormatValue(target.MaximumHitPoints))
			});
		}

		static StealthStrategicTargetSnapshot RestoreTarget(MiniYamlNode node)
		{
			var values = ReadUniqueValues(node.Value.Nodes, "TargetAcquisition target");
			return new StealthStrategicTargetSnapshot(
				Read<uint>(values, "StableActorId"), Read<CPos>(values, "StrategicCell"),
				Read<int>(values, "ConfiguredPriority"), Read<int>(values, "ActorValue"),
				Read<int>(values, "HitPoints"), Read<int>(values, "MaximumHitPoints"));
		}

		static IEnumerable<StealthStrategicTargetSnapshot> TargetsAt(
			StealthTargetAcquisitionCacheSnapshot snapshot, CPos cell)
		{
			return snapshot.StrategicTargets.Where(target => target.StrategicCell == cell);
		}

		static StealthTargetThreatFacts ThreatFactsAt(
			StealthTargetAcquisitionCacheSnapshot snapshot, CPos cell)
		{
			return snapshot.ThreatFacts.FirstOrDefault(facts => facts.StrategicCell == cell);
		}

		static CPos? MoveCloser(CPos start, IReadOnlyList<CPos> enemies,
			StealthTargetAcquisitionCacheSnapshot snapshot,
			IReadOnlyList<StealthAIThreatGeometry.ReachableTargetCell> reachable)
		{
			var routed = reachable.Where(target => target.Route.Count != 0)
				.OrderBy(target => target.RouteCost)
				.ThenBy(target => enemies[target.TargetIndex].Y)
				.ThenBy(target => enemies[target.TargetIndex].X).FirstOrDefault();
			if (routed != null)
				return routed.Route[Math.Min(MaximumFallbackSteps, routed.Route.Count) - 1];

			var destination = enemies.OrderBy(cell => DistanceSquared(start, cell))
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).First();
			var current = start;
			for (var i = 0; i < MaximumFallbackSteps; i++)
			{
				var currentDistance = DistanceSquared(current, destination);
				var next = Neighbors(current, snapshot.Width, snapshot.Height)
					.Where(cell => DistanceSquared(cell, destination) < currentDistance)
					.OrderBy(cell => DistanceSquared(cell, destination))
					.ThenBy(cell => snapshot.Danger[cell.Y * snapshot.Width + cell.X])
					.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).FirstOrDefault(current);
				if (next == current)
					break;
				current = next;
			}

			return current == start ? (CPos?)null : current;
		}

		static IEnumerable<CPos> Neighbors(CPos cell, int width, int height)
		{
			if (cell.X > 0)
				yield return new CPos(cell.X - 1, cell.Y);
			if (cell.X + 1 < width)
				yield return new CPos(cell.X + 1, cell.Y);
			if (cell.Y > 0)
				yield return new CPos(cell.X, cell.Y - 1);
			if (cell.Y + 1 < height)
				yield return new CPos(cell.X, cell.Y + 1);
		}

		static long DistanceSquared(CPos a, CPos b)
		{
			var dx = (long)a.X - b.X;
			var dy = (long)a.Y - b.Y;
			return dx * dx + dy * dy;
		}

		static bool Contains(StealthTargetAcquisitionCacheSnapshot snapshot, CPos cell)
		{
			return cell.X >= 0 && cell.Y >= 0 && cell.X < snapshot.Width && cell.Y < snapshot.Height;
		}

		static int ToTravelMilliseconds(float routeCost, float secondsPerCostUnit)
		{
			return (int)Math.Min(int.MaxValue,
				Math.Round(routeCost * secondsPerCostUnit * 1000d, MidpointRounding.AwayFromZero));
		}

		void ValidateOwnedResult(StealthTargetAcquisitionResult result)
		{
			if (result == null)
				throw new ArgumentNullException(nameof(result));
			if (result.Handoff.Owner != handoff.Owner || result.Handoff.Epoch != handoff.Epoch)
				throw new ArgumentException(
					"The TargetAcquisition result belongs to another ownership epoch.", nameof(result));

			ValidatePersistentResult(result);
		}

		static void ValidatePersistentResult(StealthTargetAcquisitionResult result)
		{
			var options = result.Options.ToArray();
			if (options.Length > MaximumOptions ||
				options.Select(option => option.StrategicCell).Distinct().Count() != options.Length ||
				options.SelectMany(option => option.StrategicTargets)
					.Select(target => target.StableActorId).Distinct().Count() !=
					options.Sum(option => option.StrategicTargets.Count) ||
				options.Any(option => option.EstimatedTravelMilliseconds > MaximumTravelSeconds * 1000 &&
					!option.IsIncumbent) || options.Count(option => option.IsIncumbent) > 1 ||
				(result.IncumbentStrategicCell == null && options.Any(option => option.IsIncumbent)) ||
				(result.IncumbentStrategicCell != null && (options.Length == 0 ||
					!options[0].IsIncumbent || options[0].StrategicCell != result.IncumbentStrategicCell.Value)) ||
				!options.Skip(result.IncumbentStrategicCell == null ? 0 : 1).SequenceEqual(
					options.Skip(result.IncumbentStrategicCell == null ? 0 : 1)
						.OrderBy(option => option.EstimatedTravelMilliseconds)
						.ThenBy(option => option.StrategicCell.Y).ThenBy(option => option.StrategicCell.X)) ||
				result.PrimitiveOperations > MaximumPrimitiveOperations ||
				(result.IsReadyForValueFilter != (options.Length == MaximumOptions)) ||
				(result.Disposition == StealthTargetAcquisitionDisposition.MoveCloserAndRescan &&
					options.Length == MaximumOptions))
				throw new InvalidOperationException("Invalid normalized TargetAcquisition private state.");
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
				throw new InvalidOperationException("Missing TargetAcquisition private state field: " + key);
			return FieldLoader.GetValue<T>(key, text);
		}

		static int ReadNonnegativeInt(Dictionary<string, string> values, string key)
		{
			if (!TryReadInt(values, key, out var value) || value < 0)
				throw new InvalidOperationException("Invalid TargetAcquisition private state field: " + key);
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
