#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is made available to you under the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>Bounded economy-anchor demand and placement for normal BaseBuilder SAM construction.</summary>
	sealed class BaseBuilderEconomyDefenseSamPlanner
	{
		readonly BaseBuilderBotModule baseBuilder;
		readonly Player player;
		readonly World world;
		readonly PowerManager playerPower;
		readonly PlayerResources playerResources;
		readonly TechTree techTree;
		readonly EconomyDefenseSamBuildOwnership<ProductionQueue> buildOwnership =
			new EconomyDefenseSamBuildOwnership<ProductionQueue>();

		BaseBuilderBotModuleInfo Info => baseBuilder.Info;
		bool Enabled => Info.EconomyDefenseBotTypes.Contains(player.BotType) &&
			techTree.HasPrerequisites(Info.EconomyDefensePrerequisites);

		public BaseBuilderEconomyDefenseSamPlanner(BaseBuilderBotModule baseBuilder, Player player,
			PowerManager playerPower, PlayerResources playerResources, TechTree techTree)
		{
			this.baseBuilder = baseBuilder;
			this.player = player;
			world = player.World;
			this.playerPower = playerPower;
			this.playerResources = playerResources;
			this.techTree = techTree;
		}

		public ActorInfo ChooseBuilding(ProductionQueue queue, IEnumerable<ActorInfo> buildables)
		{
			if (!Enabled || queue == null || queue.AllQueued().Any())
				return null;

			RefreshBuildOwnership();
			if (buildOwnership.HasReservation)
				return null;

			var live = baseBuilder.CountActors(Info.EconomyDefenseSamTypes);
			var pending = baseBuilder.CountQueuedOrPendingActors(Info.EconomyDefenseSamTypes);
			var uncovered = FirstUncoveredAnchor();
			foreach (var sam in buildables.Where(a => Info.EconomyDefenseSamTypes.Contains(a.Name))
				.OrderBy(a => a.Name, StringComparer.Ordinal))
			{
				var hasPower = HasSufficientPower(sam);
				if (!EconomyFieldDefensePolicy.ShouldRequestSam(true, hasPower, live, pending,
					Info.EconomyDefenseMaximumSamSites, uncovered != null))
					continue;

				if (!buildOwnership.TryReserve(queue, sam.Name, world.WorldTick))
					continue;

				Debug("reserved build type={0} anchor={1} priority={2} live={3} pending={4} power={5}",
					sam.Name, uncovered.Value.ActorId, uncovered.Value.Priority, live, pending,
					playerPower?.ExcessPower ?? int.MaxValue);
				return sam;
			}

			return null;
		}

		public bool OwnsPlacement(ProductionQueue queue, string actorType)
		{
			return Enabled && buildOwnership.Owns(queue, actorType);
		}

		public CPos? ChooseLocation(ProductionQueue queue, string actorType, ActorInfo actorInfo,
			BuildingInfo buildingInfo,
			bool distanceToBaseIsImportant)
		{
			if (!OwnsPlacement(queue, actorType))
				return null;

			var anchor = FirstUncoveredAnchor();
			if (anchor == null)
			{
				Debug("withheld placement type={0}: all economy anchors already covered", actorType);
				return null;
			}

			var coverageRadius = CoverageRadiusCells(actorInfo);
			var traffic = RefineryTrafficCells();
			var cells = world.Map.FindTilesInAnnulus(anchor.Value.Cell,
				Info.EconomyDefenseSamMinimumRadius, Info.EconomyDefenseSamMaximumRadius)
				.OrderBy(c => (c - anchor.Value.Cell).LengthSquared)
				.ThenBy(c => c.X).ThenBy(c => c.Y);

			foreach (var cell in cells)
			{
				if ((cell - anchor.Value.Cell).LengthSquared > coverageRadius * coverageRadius ||
					buildingInfo.Tiles(cell).Any(traffic.Contains) ||
					!world.CanPlaceBuilding(cell, actorInfo, buildingInfo, null) ||
					(distanceToBaseIsImportant && !buildingInfo.IsCloseEnoughToBase(world, player, actorInfo, cell)))
					continue;

				Debug("placement type={0} anchor={1} priority={2} cell={3} coverage={4}", actorType,
					anchor.Value.ActorId, anchor.Value.Priority, cell, coverageRadius);
				return cell;
			}

			Debug("withheld placement type={0} anchor={1}: no legal powered coverage cell", actorType,
				anchor.Value.ActorId);
			return null;
		}

		EconomyDefenseSamAnchor? FirstUncoveredAnchor()
		{
			return EconomyFieldDefensePolicy.FirstUncoveredSamAnchor(Anchors(), ExistingCoverage());
		}

		IEnumerable<EconomyDefenseSamAnchor> Anchors()
		{
			var siloUsed = playerResources.ResourceCapacity > 0 &&
				playerResources.Resources * 100L >= playerResources.ResourceCapacity *
				Info.EconomyDefenseUsedSiloThresholdPercent;
			foreach (var actor in world.Actors.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead)
				.OrderBy(a => a.ActorID))
			{
				var priority = Info.EconomyDefenseRefineryTypes.Contains(actor.Info.Name) ? 0 :
					Info.EconomyDefenseResonatorTypes.Contains(actor.Info.Name) ? 1 :
					Info.EconomyDefenseSiloTypes.Contains(actor.Info.Name) && siloUsed ? 2 : -1;
				if (priority >= 0)
					yield return new EconomyDefenseSamAnchor(actor.ActorID, priority, actor.Location);
			}
		}

		IEnumerable<EconomyDefenseSamCoverage> ExistingCoverage()
		{
			foreach (var actor in world.Actors.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead &&
				Info.EconomyDefenseSamTypes.Contains(a.Info.Name)).OrderBy(a => a.ActorID))
			{
				var range = actor.TraitsImplementing<Armament>()
					.Where(a => !a.IsTraitDisabled && !a.IsTraitPaused)
					.Select(a => a.MaxRange().Length).DefaultIfEmpty(0).Max();
				var radius = Math.Max(0, range / 1024 - Info.EconomyDefenseSamCoverageMarginCells);
				if (radius > 0)
					yield return new EconomyDefenseSamCoverage(actor.Location, radius);
			}
		}

		int CoverageRadiusCells(ActorInfo actorInfo)
		{
			var range = actorInfo.TraitInfos<ArmamentInfo>()
				.Select(a => a.ModifiedRange.Length).DefaultIfEmpty(0).Max();
			return Math.Max(0, range / 1024 - Info.EconomyDefenseSamCoverageMarginCells);
		}

		bool HasSufficientPower(ActorInfo actorInfo)
		{
			if (playerPower == null || playerPower.PowerOutageRemainingTicks > 0)
				return playerPower == null;

			var power = actorInfo.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(i => i.Amount);
			return playerPower.ExcessPower >= Info.MinimumExcessPower &&
				playerPower.ExcessPower + power >= Info.MinimumExcessPower;
		}

		HashSet<CPos> RefineryTrafficCells()
		{
			var cells = new HashSet<CPos>();
			foreach (var pair in world.ActorsWithTrait<IAcceptResources>()
				.Where(p => p.Actor.Owner == player && p.Actor.IsInWorld && !p.Actor.IsDead)
				.OrderBy(p => p.Actor.ActorID))
			{
				var building = pair.Actor.Info.TraitInfoOrDefault<BuildingInfo>();
				if (building != null)
					cells.UnionWith(building.Tiles(pair.Actor.Location));

				var delivery = pair.Actor.Location + pair.Trait.DeliveryOffset;
				var dx = Math.Sign(pair.Trait.DeliveryOffset.X);
				var dy = Math.Sign(pair.Trait.DeliveryOffset.Y);
				if (dx == 0 && dy == 0)
					dy = 1;

				for (var distance = 0; distance <= Info.EconomyDefenseRefineryLaneLengthCells; distance++)
					for (var width = -Info.EconomyDefenseRefineryLaneHalfWidthCells;
						width <= Info.EconomyDefenseRefineryLaneHalfWidthCells; width++)
						cells.Add(delivery + new CVec(dx * distance - dy * width,
							dy * distance + dx * width));
			}

			return cells;
		}

		void RefreshBuildOwnership()
		{
			buildOwnership.Refresh(world.WorldTick, Info.StructureProductionActiveDelay,
				queue => queue.Actor != null && queue.Actor.IsInWorld && !queue.Actor.IsDead,
				(queue, actorType) => queue.AllQueued().Any(i => i.Item == actorType));
		}

		void Debug(string format, params object[] args)
		{
			if (Info.EconomyDefenseSamDebugLogging)
				Log.Write("debug", "AI economy SAM: " + format, args);
		}
	}
}
