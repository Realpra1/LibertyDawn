#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Adds a readiness-gated Mammoth-led production priority for Economy AIs.")]
	public sealed class EconomyTroopProductionBotModuleInfo : ConditionalTraitInfo
	{
		public readonly string[] RequiredPrerequisites = Array.Empty<string>();
		public readonly HashSet<string> MammothTypes = new HashSet<string>();
		public readonly HashSet<string> DirectFireVehicleTypes = new HashSet<string>();
		public readonly HashSet<string> ScreenTypes = new HashSet<string>();
		public readonly HashSet<string> ArtilleryTypes = new HashSet<string>();
		public readonly HashSet<string> AntiAirTypes = new HashSet<string>();
		public readonly HashSet<string> HarvesterTypes = new HashSet<string>();
		public readonly string AttackApproachPolicy = "economy-mammoth";
		public readonly int MinimumHarvesters = 3;
		public readonly int MinimumScreen = 4;
		public readonly int MinimumArtillery = 1;
		public readonly int MinimumAntiAir = 1;
		public readonly int MinimumAvailableCash = 2500;
		public readonly int MinimumMaintainCash = 0;
		public readonly int MammothTargetValuePercent = 55;
		public readonly int CriticalThreatRadiusCells = 10;
		public readonly int MaximumCriticalActors = 16;
		public readonly int ScanInterval = 75;
		public readonly int ReadinessObservationTicks = 300;
		public readonly int DecisionLogInterval = 750;
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (RequiredPrerequisites.Length == 0 || MammothTypes.Count == 0 || DirectFireVehicleTypes.Count == 0 ||
				ScreenTypes.Count == 0 || ArtilleryTypes.Count == 0 || AntiAirTypes.Count == 0 ||
				HarvesterTypes.Count == 0 || string.IsNullOrEmpty(AttackApproachPolicy) || MinimumHarvesters <= 0 ||
				MinimumScreen <= 0 || MinimumArtillery <= 0 || MinimumAntiAir <= 0 || MinimumAvailableCash < 0 ||
				MinimumMaintainCash < 0 || MinimumMaintainCash > MinimumAvailableCash ||
				MammothTargetValuePercent <= 0 || MammothTargetValuePercent > 100 || CriticalThreatRadiusCells <= 0 ||
				MaximumCriticalActors <= 0 || ScanInterval <= 0 || ReadinessObservationTicks <= 0 ||
				DecisionLogInterval <= 0)
				throw new YamlException("Economy troop production types, prerequisites, readiness, priority, and bounds must be configured and valid.");

			foreach (var type in MammothTypes.Concat(DirectFireVehicleTypes).Concat(ScreenTypes)
				.Concat(ArtilleryTypes).Concat(AntiAirTypes).Concat(HarvesterTypes).Distinct())
				if (!rules.Actors.ContainsKey(type))
					throw new YamlException($"Economy troop production actor '{type}' does not exist.");
		}

		public override object Create(ActorInitializer init) { return new EconomyTroopProductionBotModule(init.Self, this); }
	}

	public sealed class EconomyTroopProductionBotModule : ConditionalTrait<EconomyTroopProductionBotModuleInfo>,
		IBotEnabled, IBotTick, IBotAttackApproachPolicy, IGameSaveTraitData
	{
		const string RequestOwner = "economy-troop-production";
		static readonly BitSet<TargetableType> GroundTargetTypes = new BitSet<TargetableType>("Ground");

		readonly World world;
		readonly Player player;
		IBot bot;
		TechTree techTree;
		PlayerResources playerResources;
		IBotRequestOwnedUnitProduction[] productionRequesters;
		IBotUnitReservations[] unitReservations;
		int scanTicks;
		int readinessObservationStartedTick = -1;
		string lastDecisionCategory;
		int nextDecisionLogTick;
		internal bool IsReadyForRaid { get; private set; }

		public EconomyTroopProductionBotModule(Actor self, EconomyTroopProductionBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			techTree = player.PlayerActor.Trait<TechTree>();
			playerResources = player.PlayerActor.Trait<PlayerResources>();
			productionRequesters = player.PlayerActor.TraitsImplementing<IBotRequestOwnedUnitProduction>().ToArray();
			unitReservations = player.PlayerActor.TraitsImplementing<IBotUnitReservations>().ToArray();
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self) { scanTicks = 1; }

		protected override void TraitDisabled(Actor self)
		{
			IsReadyForRaid = false;
			readinessObservationStartedTick = -1;
			CancelRequests();
			lastDecisionCategory = null;
		}

		void IBotEnabled.BotEnabled(IBot enabledBot) { bot = enabledBot; }

		bool IBotAttackApproachPolicy.IsAttackApproachPolicyActive(Actor attacker, string policy)
		{
			return bot != null && !IsTraitDisabled && IsReadyForRaid && attacker != null && attacker.Owner == player &&
				policy == Info.AttackApproachPolicy && Info.MammothTypes.Contains(attacker.Info.Name) &&
				techTree.HasPrerequisites(Info.RequiredPrerequisites);
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (IsTraitDisabled || player.WinState != WinState.Undefined || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			if (!techTree.HasPrerequisites(Info.RequiredPrerequisites))
			{
				IsReadyForRaid = false;
				readinessObservationStartedTick = -1;
				CancelRequests();
				LogDecision("inactive", "prerequisites");
				return;
			}

			var live = world.Actors.Where(IsOwnedUsable).ToList();
			var harvesters = live.Count(a => Info.HarvesterTypes.Contains(a.Info.Name));
			var screen = live.Count(a => Info.ScreenTypes.Contains(a.Info.Name) &&
				!unitReservations.Any(r => r.IsUnitReserved(a)));
			var artillery = live.Count(a => Info.ArtilleryTypes.Contains(a.Info.Name));
			var antiAir = live.Count(a => Info.AntiAirTypes.Contains(a.Info.Name));
			var cash = Math.Max(0, playerResources.Cash + playerResources.Resources);
			var criticalThreat = HasCriticalThreat(live);
			var entryReady = EconomyTroopPolicy.IsReady(harvesters, Info.MinimumHarvesters, screen,
				Info.MinimumScreen, artillery, Info.MinimumArtillery, antiAir, Info.MinimumAntiAir,
				cash, Info.MinimumAvailableCash, criticalThreat);
			var maintenanceReady = EconomyTroopPolicy.IsReady(harvesters, Info.MinimumHarvesters, screen,
				Info.MinimumScreen, artillery, Info.MinimumArtillery, antiAir, Info.MinimumAntiAir,
				cash, Info.MinimumMaintainCash, criticalThreat);
			var readinessDecision = EconomyTroopPolicy.ReadinessDecision(IsReadyForRaid, entryReady,
				maintenanceReady, world.WorldTick, readinessObservationStartedTick, Info.ReadinessObservationTicks);
			if (readinessDecision == EconomyReadinessDecision.Observing && readinessObservationStartedTick < 0)
				readinessObservationStartedTick = world.WorldTick;
			else if (readinessDecision != EconomyReadinessDecision.Observing)
				readinessObservationStartedTick = -1;

			var ready = readinessDecision == EconomyReadinessDecision.Ready;
			IsReadyForRaid = ready;
			var readinessCash = ready || readinessDecision == EconomyReadinessDecision.Observing ?
				Info.MinimumMaintainCash : Info.MinimumAvailableCash;

			var mammothCount = live.Count(a => Info.MammothTypes.Contains(a.Info.Name));
			var requiredScreen = Math.Max(Info.MinimumScreen, (mammothCount + 1) / 2);
			if (screen < requiredScreen && harvesters >= Info.MinimumHarvesters && artillery >= Info.MinimumArtillery &&
				antiAir >= Info.MinimumAntiAir && cash >= readinessCash / 2)
			{
				EnsureRequest(Info.ScreenTypes.OrderBy(t => t, StringComparer.Ordinal).First());
				LogDecision("screen-recovery", $"screen={screen}/{requiredScreen} ready={ready} cash={cash}/{readinessCash} threat={criticalThreat}");
				return;
			}

			if (!ready)
			{
				CancelRequests();
				var category = readinessDecision == EconomyReadinessDecision.Observing ? "observing" : "not-ready";
				var observation = readinessObservationStartedTick < 0 ? "none" :
					$"{world.WorldTick - readinessObservationStartedTick}/{Info.ReadinessObservationTicks}";
				LogDecision(category, $"harv={harvesters} screen={screen} artillery={artillery} aa={antiAir} cash={cash}/{readinessCash} threat={criticalThreat} observation={observation}");
				return;
			}

			var frontline = live.Where(a => Info.DirectFireVehicleTypes.Contains(a.Info.Name) &&
				!unitReservations.Any(r => r.IsUnitReserved(a))).ToList();
			var typeValues = frontline.GroupBy(a => a.Info.Name)
				.ToDictionary(g => g.Key, g => g.Sum(ActorValue), StringComparer.Ordinal);
			var mammothValue = Info.MammothTypes.Sum(t => typeValues.TryGetValue(t, out var value) ? value : 0L);
			var otherValue = typeValues.Where(kv => !Info.MammothTypes.Contains(kv.Key))
				.Select(kv => kv.Value).DefaultIfEmpty(0).Max();
			var totalValue = typeValues.Values.Sum();
			if (EconomyTroopPolicy.ShouldRequestMammoth(mammothValue, otherValue,
				totalValue, Info.MammothTargetValuePercent))
			{
				EnsureRequest(Info.MammothTypes.OrderBy(t => t, StringComparer.Ordinal).First());
				LogDecision("mammoth-priority", $"value={mammothValue}/{totalValue} largest-other={otherValue} cash={cash}/{readinessCash}");
			}
			else
			{
				CancelRequests();
				LogDecision("mixed-target-met", $"value={mammothValue}/{totalValue} largest-other={otherValue} cash={cash}/{readinessCash}");
			}
		}

		bool IsOwnedUsable(Actor actor)
		{
			return actor != null && actor.Owner == player && actor.IsInWorld && !actor.IsDead;
		}

		bool HasCriticalThreat(List<Actor> live)
		{
			var critical = live.Where(a => Info.HarvesterTypes.Contains(a.Info.Name) ||
				Info.ArtilleryTypes.Contains(a.Info.Name)).OrderBy(a => a.ActorID).Take(Info.MaximumCriticalActors).ToList();
			if (critical.Count == 0)
				return false;

			var radiusSquared = (long)WDist.FromCells(Info.CriticalThreatRadiusCells).Length;
			radiusSquared *= radiusSquared;
			return world.Actors.Where(a => a.IsInWorld && !a.IsDead &&
				player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy && a.CanBeViewedByPlayer(player) &&
				a.TraitsImplementing<Armament>().Any(arm => !arm.IsTraitDisabled && arm.Weapon.IsValidTarget(GroundTargetTypes)))
				.Any(enemy => critical.Any(c => (enemy.CenterPosition - c.CenterPosition).HorizontalLengthSquared <= radiusSquared));
		}

		static int ActorValue(Actor actor)
		{
			return Math.Max(1, actor.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 1);
		}

		void EnsureRequest(string actorType)
		{
			if (bot == null)
				return;

			var requester = productionRequesters.FirstOrDefault(Exts.IsTraitEnabled);
			if (requester == null)
				return;

			foreach (var type in Info.MammothTypes.Concat(Info.ScreenTypes).Distinct())
				if (type != actorType && requester.RequestedProductionCount(bot, RequestOwner, type) > 0)
					requester.CancelRequestedUnitProduction(bot, RequestOwner, type);

			if (requester.RequestedProductionCount(bot, RequestOwner, actorType) == 0 && !IsQueued(actorType))
				requester.RequestUnitProduction(bot, RequestOwner, actorType);
		}

		void CancelRequests()
		{
			if (bot == null)
				return;

			foreach (var requester in productionRequesters ?? Array.Empty<IBotRequestOwnedUnitProduction>())
				foreach (var type in Info.MammothTypes.Concat(Info.ScreenTypes).Distinct())
					if (requester.RequestedProductionCount(bot, RequestOwner, type) > 0)
						requester.CancelRequestedUnitProduction(bot, RequestOwner, type);
		}

		bool IsQueued(string actorType)
		{
			return world.ActorsWithTrait<ProductionQueue>().Any(q => q.Actor.Owner == player &&
				q.Trait.AllQueued().Any(item => item.Item == actorType));
		}

		void LogDecision(string category, string detail)
		{
			if (category == lastDecisionCategory && world.WorldTick < nextDecisionLogTick)
				return;

			lastDecisionCategory = category;
			nextDecisionLogTick = world.WorldTick + Info.DecisionLogInterval;
			if (!Info.DebugLogging)
				return;

			AIUtils.BotDebug("AI ({0}) economy troops: {1}: {2}", player.ClientIndex, category, detail);
			Log.Write("debug", "AI economy troops: {0} (client {1}) at tick {2}: {3}: {4}",
				player, player.ClientIndex, world.WorldTick, category, detail);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			return IsTraitDisabled ? null : new List<MiniYamlNode>
			{
				new MiniYamlNode("EconomyTroopProductionScanTicks", FieldSaver.FormatValue(scanTicks)),
				new MiniYamlNode("EconomyTroopProductionReady", FieldSaver.FormatValue(IsReadyForRaid)),
				new MiniYamlNode("EconomyTroopProductionObservationStarted", FieldSaver.FormatValue(readinessObservationStartedTick))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			foreach (var node in data)
				switch (node.Key)
				{
					case "EconomyTroopProductionScanTicks": scanTicks = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyTroopProductionReady": IsReadyForRaid = FieldLoader.GetValue<bool>(node.Key, node.Value.Value); break;
					case "EconomyTroopProductionObservationStarted": readinessObservationStartedTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
				}
		}
	}
}
