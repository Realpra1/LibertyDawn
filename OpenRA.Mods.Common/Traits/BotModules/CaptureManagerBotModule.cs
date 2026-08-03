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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Manages AI capturing logic.")]
	public class CaptureManagerBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Actor types that can capture other actors (via `Captures`).",
			"Leave this empty to disable capturing.")]
		public readonly HashSet<string> CapturingActorTypes = new HashSet<string>();

		[Desc("Actor types that can be targeted for capturing.",
			"Leave this empty to include all actors.")]
		public readonly HashSet<string> CapturableActorTypes = new HashSet<string>();

		[Desc("Minimum delay (in ticks) between trying to capture with CapturingActorTypes.")]
		public readonly int MinimumCaptureDelay = 375;

		[Desc("Maximum number of options to consider for capturing.",
			"If a value less than 1 is given 1 will be used instead.")]
		public readonly int MaximumCaptureTargetOptions = 10;

		[Desc("Should visibility (Shroud, Fog, Cloak, etc) be considered when searching for capturable targets?")]
		public readonly bool CheckCaptureTargetsForVisibility = true;

		[Desc("Player relationships that capturers should attempt to target.")]
		public readonly PlayerRelationship CapturableRelationships = PlayerRelationship.Enemy | PlayerRelationship.Neutral;

		[Desc("Actor types that may use the existing C4 order against enemy buildings.",
			"Leave this empty to disable demolition orders.")]
		public readonly HashSet<string> DemolitionActorTypes = new HashSet<string>();

		[Desc("Minimum delay (in ticks) between trying to demolish with DemolitionActorTypes.")]
		public readonly int MinimumDemolitionDelay = 375;

		[Desc("Write capture and demolition assignments to debug.log.")]
		public readonly bool DebugLogging = false;

		public override object Create(ActorInitializer init) { return new CaptureManagerBotModule(init.Self, this); }
	}

	public class CaptureManagerBotModule : ConditionalTrait<CaptureManagerBotModuleInfo>, IBotTick
	{
		readonly struct CaptureCandidate
		{
			public readonly Actor Actor;
			public readonly int Value;
			public readonly bool IsBuilding;

			public CaptureCandidate(Actor actor, int value)
			{
				Actor = actor;
				Value = value;
				IsBuilding = actor.Info.HasTraitInfo<BuildingInfo>();
			}
		}

		readonly struct SpecialistAssignment
		{
			public readonly Actor Target;
			public readonly int TargetHealth;

			public SpecialistAssignment(Actor target)
			{
				Target = target;
				TargetHealth = target.TraitOrDefault<IHealth>()?.HP ?? 0;
			}
		}

		readonly World world;
		readonly Player player;
		readonly Func<Actor, bool> isEnemyUnit;
		readonly Predicate<Actor> unitCannotBeOrderedOrIsIdle;
		readonly int maximumCaptureTargetOptions;
		int minCaptureDelayTicks;
		int minDemolitionDelayTicks;

		// Specialists with active orders and their targets. Remembering the target prevents duplicate assignments
		// and lets the debug log distinguish completed work from an interrupted order.
		readonly Dictionary<Actor, SpecialistAssignment> activeCapturers = new Dictionary<Actor, SpecialistAssignment>();
		readonly Dictionary<Actor, SpecialistAssignment> activeDemolitionUnits = new Dictionary<Actor, SpecialistAssignment>();

		public CaptureManagerBotModule(Actor self, CaptureManagerBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;

			if (world.Type == WorldType.Editor)
				return;

			isEnemyUnit = unit =>
				player.RelationshipWith(unit.Owner) == PlayerRelationship.Enemy
					&& !unit.Info.HasTraitInfo<HuskInfo>()
					&& unit.Info.HasTraitInfo<ITargetableInfo>();

			unitCannotBeOrderedOrIsIdle = a => a.Owner != player || a.IsDead || !a.IsInWorld || a.IsIdle;

			maximumCaptureTargetOptions = Math.Max(1, Info.MaximumCaptureTargetOptions);
		}

		protected override void TraitEnabled(Actor self)
		{
			// Avoid all AIs reevaluating assignments on the same tick, randomize their initial evaluation delay.
			minCaptureDelayTicks = world.LocalRandom.Next(0, Info.MinimumCaptureDelay);
			minDemolitionDelayTicks = world.LocalRandom.Next(0, Info.MinimumDemolitionDelay);
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (--minCaptureDelayTicks <= 0)
			{
				minCaptureDelayTicks = Info.MinimumCaptureDelay;
				QueueCaptureOrders(bot);
			}

			if (--minDemolitionDelayTicks <= 0)
			{
				minDemolitionDelayTicks = Info.MinimumDemolitionDelay;
				QueueDemolitionOrders(bot);
			}
		}

		internal Actor FindClosestEnemy(WPos pos)
		{
			return world.Actors.Where(isEnemyUnit).ClosestTo(pos);
		}

		internal Actor FindClosestEnemy(WPos pos, WDist radius)
		{
			return world.FindActorsInCircle(pos, radius).Where(isEnemyUnit).ClosestTo(pos);
		}

		IEnumerable<Actor> GetVisibleActorsBelongingToPlayer(Player owner)
		{
			foreach (var actor in GetActorsThatCanBeOrderedByPlayer(owner))
				if (actor.CanBeViewedByPlayer(player))
					yield return actor;
		}

		IEnumerable<Actor> GetActorsThatCanBeOrderedByPlayer(Player owner)
		{
			foreach (var actor in world.Actors)
				if (actor.Owner == owner && !actor.IsDead && actor.IsInWorld)
					yield return actor;
		}

		void QueueCaptureOrders(IBot bot)
		{
			if (!Info.CapturingActorTypes.Any() || player.WinState != WinState.Undefined)
				return;

			RetireFinishedAssignments(activeCapturers, "capture");

			var newUnits = world.ActorsHavingTrait<IPositionable>()
				.Where(a => a.Owner == player && !activeCapturers.ContainsKey(a));

			var capturers = newUnits
				.Where(a => a.IsIdle && Info.CapturingActorTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<CapturesInfo>())
				.Select(a => new TraitPair<CaptureManager>(a, a.TraitOrDefault<CaptureManager>()))
				.Where(tp => tp.Trait != null)
				.ToArray();

			if (capturers.Length == 0)
				return;

			var targetOptions = world.Players.Where(p => !p.Spectating
					&& Info.CapturableRelationships.HasRelationship(player.RelationshipWith(p)))
				.SelectMany(p => Info.CheckCaptureTargetsForVisibility
					? GetVisibleActorsBelongingToPlayer(p) : GetActorsThatCanBeOrderedByPlayer(p));

			var capturableTargetOptions = targetOptions
				.Where(target =>
				{
					var captureManager = target.TraitOrDefault<CaptureManager>();
					if (captureManager == null)
						return false;

					return capturers.Any(tp => captureManager.CanBeTargetedBy(target, tp.Actor, tp.Trait));
				});

			if (Info.CapturableActorTypes.Any())
				capturableTargetOptions = capturableTargetOptions.Where(target => Info.CapturableActorTypes.Contains(target.Info.Name.ToLowerInvariant()));

			var candidates = capturableTargetOptions.Select(target => new CaptureCandidate(target, CaptureEconomicValue(target)))
				.OrderByDescending(candidate => candidate.Value)
				.ThenByDescending(candidate => candidate.IsBuilding)
				.ThenBy(candidate => candidate.Actor.ActorID)
				.Take(maximumCaptureTargetOptions).ToArray();
			if (candidates.Length == 0)
				return;

			var activeTargetIds = activeCapturers.Values.Select(assignment => assignment.Target.ActorID).ToHashSet();
			var assigned = Enumerable.Range(0, candidates.Length)
				.Where(index => activeTargetIds.Contains(candidates[index].Actor.ActorID)).ToHashSet();
			foreach (var capturer in capturers.OrderBy(capturer => capturer.Actor.ActorID))
			{
				var distances = candidates.Select(candidate =>
					(long)(candidate.Actor.CenterPosition - capturer.Actor.CenterPosition).LengthSquared).ToArray();
				var targetIndex = CaptureTargeting.BestTargetIndex(candidates.Select(candidate => candidate.Value).ToArray(),
					candidates.Select(candidate => candidate.IsBuilding).ToArray(), distances, assigned);
				if (targetIndex < 0)
					continue;

				assigned.Add(targetIndex);
				var target = candidates[targetIndex];

				bot.QueueOrder(new Order("CaptureActor", capturer.Actor, Target.FromActor(target.Actor), true));
				AIUtils.BotDebug("AI ({0}): Ordered {1} to capture {2}", player.ClientIndex, capturer.Actor, target.Actor);
				Debug("capture {0}#{1} -> {2}#{3}: value={4}, distance-cells={5:0.0}, building={6}",
					capturer.Actor.Info.Name, capturer.Actor.ActorID, target.Actor.Info.Name, target.Actor.ActorID,
					target.Value, System.Math.Sqrt(distances[targetIndex]) / 1024d, target.IsBuilding);
				activeCapturers.Add(capturer.Actor, new SpecialistAssignment(target.Actor));
			}
		}

		int CaptureEconomicValue(Actor target)
		{
			var transformedValue = 0;
			var transform = target.Info.TraitInfoOrDefault<TransformOnCaptureInfo>();
			if (transform != null && world.Map.Rules.Actors.TryGetValue(transform.IntoActor, out var transformed))
			{
				var customValue = transformed.TraitInfoOrDefault<CustomSellValueInfo>();
				var valued = transformed.TraitInfoOrDefault<ValuedInfo>();
				transformedValue = customValue?.Value ?? valued?.Cost ?? 0;
			}

			return CaptureTargeting.EconomicValue(target.GetSellValue(), transformedValue);
		}

		void QueueDemolitionOrders(IBot bot)
		{
			if (Info.DemolitionActorTypes.Count == 0 || player.WinState != WinState.Undefined)
				return;

			RetireFinishedAssignments(activeDemolitionUnits, "demolition");
			var activeTargetIds = activeDemolitionUnits.Values.Select(assignment => assignment.Target.ActorID).ToHashSet();
			var demolitionUnits = world.Actors.Where(a => a.Owner == player && a.IsIdle &&
				Info.DemolitionActorTypes.Contains(a.Info.Name) && a.Info.HasTraitInfo<DemolitionInfo>() &&
				!activeDemolitionUnits.ContainsKey(a)).ToArray();

			foreach (var unit in demolitionUnits.OrderBy(unit => unit.ActorID))
			{
				var target = world.Actors.Where(a => !a.IsDead && a.IsInWorld &&
					player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy &&
					a.Info.HasTraitInfo<BuildingInfo>() &&
					a.TraitsImplementing<IDemolishable>().Any(d => d.IsValidTarget(a, unit)) &&
					(!Info.CheckCaptureTargetsForVisibility || a.CanBeViewedByPlayer(player)) &&
					!activeTargetIds.Contains(a.ActorID))
					.OrderByDescending(a => a.GetSellValue())
					.Take(maximumCaptureTargetOptions)
					.MinByOrDefault(a => (a.CenterPosition - unit.CenterPosition).LengthSquared);
				if (target == null)
					continue;

				bot.QueueOrder(new Order("C4", unit, Target.FromActor(target), false));
				AIUtils.BotDebug("AI ({0}): Ordered {1} to demolish {2}", player.ClientIndex, unit, target);
				Debug("demolish {0}#{1} -> {2}#{3}: value={4}, distance-cells={5:0.0}", unit.Info.Name,
					unit.ActorID, target.Info.Name, target.ActorID, target.GetSellValue(),
					System.Math.Sqrt((target.CenterPosition - unit.CenterPosition).LengthSquared) / 1024d);
				activeDemolitionUnits.Add(unit, new SpecialistAssignment(target));
				activeTargetIds.Add(target.ActorID);
			}
		}

		void RetireFinishedAssignments(Dictionary<Actor, SpecialistAssignment> assignments, string action)
		{
			foreach (var pair in assignments.Where(pair => unitCannotBeOrderedOrIsIdle(pair.Key)).ToArray())
			{
				var target = pair.Value.Target;
				var targetRemoved = target.IsDead || !target.IsInWorld;
				var targetHealth = targetRemoved ? 0 : target.TraitOrDefault<IHealth>()?.HP ?? 0;
				var result = targetRemoved ? "target-removed" :
					target.Owner == player ? "captured" :
					targetHealth < pair.Value.TargetHealth ? "sabotaged" :
					pair.Key.IsDead || !pair.Key.IsInWorld ? "specialist-lost" : "specialist-idle";
				Debug("{0} {1}#{2} released from {3}#{4}: result={5}", action, pair.Key.Info.Name,
					pair.Key.ActorID, target.Info.Name, target.ActorID, result);
				assignments.Remove(pair.Key);
			}
		}

		void Debug(string format, params object[] args)
		{
			if (Info.DebugLogging)
				Log.Write("debug", "AI ({0}) capture manager: {1}", player.ClientIndex, string.Format(format, args));
		}
	}
}
