#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Allows synchronized orders to move along an exact bounded cell path without replanning between waypoints.")]
	public class MoveAlongPathInfo : TraitInfo, IRulesetLoaded, Requires<MobileInfo>
	{
		public readonly HashSet<string> AvoidResourceTypes = new HashSet<string>();
		public readonly int MaximumPathCells = 96;
		public readonly int MaximumSafetyCells = 2048;
		public readonly int ResourceSafetyMarginCells = 2;

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (MaximumPathCells < 2 || MaximumSafetyCells <= 0 || ResourceSafetyMarginCells < 0)
				throw new YamlException("MoveAlongPath path, safety-cell, and resource-margin bounds must be valid.");

			var resourceTypes = rules.Actors[SystemActors.World].TraitInfo<ResourceLayerInfo>().ResourceTypes;
			foreach (var resourceType in AvoidResourceTypes)
				if (!resourceTypes.ContainsKey(resourceType))
					throw new YamlException($"MoveAlongPath resource '{resourceType}' does not exist.");
		}

		public override object Create(ActorInitializer init) { return new MoveAlongPath(init.Self, this); }
	}

	public sealed class MoveAlongPath : IResolveOrder, IMobileCellValidator, ISync
	{
		public const string OrderName = "MoveAlongPath";
		public const string SafetyOrderName = "SetMoveAlongPathSafety";

		readonly World world;
		readonly MoveAlongPathInfo info;
		readonly Mobile mobile;
		readonly IResourceLayer resourceLayer;
		readonly HashSet<CPos> strictAvoidCells = new HashSet<CPos>();
		[Sync]
		bool strictMovementSafety;

		public MoveAlongPath(Actor self, MoveAlongPathInfo info)
		{
			world = self.World;
			this.info = info;
			mobile = self.Trait<Mobile>();
			resourceLayer = world.WorldActor.Trait<IResourceLayer>();
		}

		void SetStrictMovementSafety(bool value, IEnumerable<CPos> additionalAvoidCells)
		{
			strictMovementSafety = value;
			strictAvoidCells.Clear();
			if (value && additionalAvoidCells != null)
				strictAvoidCells.UnionWith(additionalAvoidCells);
		}

		public static Order CreateSafetyOrder(Actor actor, bool enabled, IEnumerable<CPos> additionalAvoidCells = null)
		{
			return new Order(SafetyOrderName, actor, false)
			{
				TargetString = enabled ? EncodeSafetyCells(additionalAvoidCells ?? Array.Empty<CPos>()) : "",
				ExtraData = enabled ? 1u : 0u
			};
		}

		public static string EncodeSafetyCells(IEnumerable<CPos> cells)
		{
			return string.Join(";", cells.Distinct().OrderBy(c => c.Bits)
				.Select(c => c.Bits.ToString(CultureInfo.InvariantCulture)));
		}

		public static bool TryDecodeSafetyCells(string encoded, int maximumSafetyCells, out CPos[] cells)
		{
			cells = Array.Empty<CPos>();
			if (maximumSafetyCells <= 0)
				return false;

			if (string.IsNullOrEmpty(encoded))
				return true;

			var tokens = encoded.Split(';');
			if (tokens.Length > maximumSafetyCells)
				return false;

			var decoded = new CPos[tokens.Length];
			for (var i = 0; i < tokens.Length; i++)
			{
				if (!int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bits))
					return false;

				decoded[i] = new CPos(bits);
				if (i > 0 && decoded[i - 1].Bits >= decoded[i].Bits)
					return false;
			}

			cells = decoded;
			return true;
		}

		bool IMobileCellValidator.IsValidCell(Actor self, CPos cell)
		{
			return !strictMovementSafety || IsAllowed(cell);
		}

		public static Order CreateOrder(World world, Actor actor, IReadOnlyList<CPos> path,
			bool attackMove, bool queued = false)
		{
			if (path == null || path.Count < 2)
				throw new ArgumentException("An exact movement path must contain at least two cells.", nameof(path));

			return new Order(OrderName, actor, Target.FromCell(world, path[0]), queued)
			{
				TargetString = EncodePath(path),
				ExtraData = attackMove ? 1u : 0u
			};
		}

		public static string EncodePath(IEnumerable<CPos> path)
		{
			return string.Join(";", path.Select(c => c.Bits.ToString(CultureInfo.InvariantCulture)));
		}

		public static bool TryDecodePath(string encoded, int maximumPathCells, out CPos[] path)
		{
			path = Array.Empty<CPos>();
			if (string.IsNullOrEmpty(encoded) || maximumPathCells < 2)
				return false;

			var tokens = encoded.Split(';');
			if (tokens.Length < 2 || tokens.Length > maximumPathCells)
				return false;

			var decoded = new CPos[tokens.Length];
			for (var i = 0; i < tokens.Length; i++)
			{
				if (!int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bits))
					return false;

				decoded[i] = new CPos(bits);
				if (i > 0 && (decoded[i - 1] == decoded[i] || !Util.AreAdjacentCells(decoded[i - 1], decoded[i])))
					return false;
			}

			path = decoded;
			return true;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == SafetyOrderName)
			{
				if (order.ExtraData > 1 ||
					!TryDecodeSafetyCells(order.TargetString, info.MaximumSafetyCells, out var avoidCells))
					return;

				SetStrictMovementSafety(order.ExtraData == 1, avoidCells);
				return;
			}

			if (order.OrderString != OrderName || order.Target.Type != TargetType.Terrain ||
				!TryDecodePath(order.TargetString, info.MaximumPathCells, out var path) ||
				path[path.Length - 1] != mobile.ToCell ||
				path[0] != world.Map.CellContaining(order.Target.CenterPosition) ||
				path.Any(c => !IsAllowed(c)))
				return;

			mobile.RecordMoveOrderIntent(path[0]);
			Func<Activity> move = () => new MoveAlongPathActivity(self, path, IsAllowed);
			var activity = order.ExtraData == 1 ? new AttackMoveActivity(self, move) : move();
			self.QueueActivity(order.Queued, activity);
		}

		bool IsAllowed(CPos cell)
		{
			if (!world.Map.Contains(cell) || (strictMovementSafety && strictAvoidCells.Contains(cell)))
				return false;

			return world.Map.FindTilesInAnnulus(cell, 0, info.ResourceSafetyMarginCells).All(c =>
			{
				var resourceType = resourceLayer.GetResource(c).Type;
				return resourceType == null || !info.AvoidResourceTypes.Contains(resourceType);
			});
		}
	}

	sealed class MoveAlongPathActivity : Activity
	{
		readonly Mobile mobile;
		readonly CPos[] path;
		readonly Func<CPos, bool> isAllowed;

		public MoveAlongPathActivity(Actor self, CPos[] path, Func<CPos, bool> isAllowed)
		{
			mobile = self.Trait<Mobile>();
			this.path = path;
			this.isAllowed = isAllowed;
			ChildHasPriority = false;
		}

		protected override void OnFirstRun(Actor self) { QueueNextSegment(); }

		public override bool Tick(Actor self)
		{
			if (!TickChild(self))
				return false;

			if (IsCanceling || mobile.MoveResult != MoveResult.CompleteDestinationReached)
				return true;

			return !QueueNextSegment();
		}

		bool QueueNextSegment()
		{
			var current = mobile.ToCell;
			var index = Array.LastIndexOf(path, current);
			if (index <= 0)
				return false;

			var next = path[index - 1];
			if (!isAllowed(next))
				return false;

			QueueChild(mobile.MoveTo(check =>
			{
				if (mobile.ToCell == next)
					return (true, PathFinder.NoPath);

				return mobile.ToCell == current && isAllowed(next) ?
					(false, new List<CPos> { next }) : (false, PathFinder.NoPath);
			}));
			return true;
		}
	}
}
