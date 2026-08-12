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
using OpenRA.Mods.Common.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Coordinates an occasional, leashed Mammoth crush deviation from the current ground squad mission.")]
	public sealed class EconomyMammothCrushBotModuleInfo : ConditionalTraitInfo
	{
		public readonly string[] RequiredPrerequisites = Array.Empty<string>();
		public readonly HashSet<string> MammothTypes = new HashSet<string>();
		public readonly HashSet<string> InfantryTypes = new HashSet<string>();
		public readonly HashSet<string> PreferredInfantryTypes = new HashSet<string>();
		public readonly int MaximumMammoths = 2;
		public readonly int MinimumHealthPercent = 65;
		public readonly int MaximumTargetDistanceCells = 5;
		public readonly int MaximumRouteDetourCells = 3;
		public readonly int LeashCells = 7;
		public readonly int DenseInfantryRadiusCells = 3;
		public readonly int MaximumNearbyInfantry = 3;
		public readonly int ThreatRadiusCells = 5;
		public readonly int MaximumNearbyArmedThreats = 2;
		public readonly int ScanInterval = 25;
		public readonly int OrderInterval = 25;
		public readonly int MissionTimeout = 175;
		public readonly int NoProgressTimeout = 75;
		public readonly int GroupCooldown = 750;
		public readonly int MaximumMammothCandidates = 8;
		public readonly int MaximumTargetCandidates = 32;
		public readonly bool DebugLogging = false;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (RequiredPrerequisites.Length == 0 || MammothTypes.Count == 0 || InfantryTypes.Count == 0 ||
				MaximumMammoths <= 0 || MaximumMammoths > 2 || MinimumHealthPercent <= 0 || MinimumHealthPercent > 100 ||
				MaximumTargetDistanceCells <= 0 || MaximumRouteDetourCells < 0 || LeashCells < MaximumTargetDistanceCells ||
				DenseInfantryRadiusCells <= 0 || MaximumNearbyInfantry <= 0 || ThreatRadiusCells <= 0 ||
				MaximumNearbyArmedThreats < 0 || ScanInterval <= 0 || OrderInterval <= 0 || MissionTimeout <= 0 ||
				NoProgressTimeout <= 0 || NoProgressTimeout > MissionTimeout || GroupCooldown <= 0 ||
				MaximumMammothCandidates < MaximumMammoths ||
				MaximumTargetCandidates <= 0 || !PreferredInfantryTypes.IsSubsetOf(InfantryTypes))
				throw new YamlException("Economy Mammoth crush prerequisites, types, health, leash, threat, timeout, and cooldown bounds must be configured and valid.");

			foreach (var type in MammothTypes.Concat(InfantryTypes).Distinct())
				if (!rules.Actors.ContainsKey(type))
					throw new YamlException($"Economy Mammoth crush actor '{type}' does not exist.");
		}

		public override object Create(ActorInitializer init) { return new EconomyMammothCrushBotModule(init.Self, this); }
	}

	public sealed class EconomyMammothCrushBotModule : ConditionalTrait<EconomyMammothCrushBotModuleInfo>,
		IBotEnabled, IBotTick, IBotTemporaryUnitControl, INotifyAppliedDamage, IGameSaveTraitData
	{
		sealed class TargetScan
		{
			public int Visible;
			public int NearRoute;
			public int Local;
			public int Crushable;
			public int Reachable;
			public int Bounded;
			public int Dense;
			public int Dangerous;
			public int Safe;
		}

		readonly World world;
		readonly Player player;
		readonly HashSet<uint> mammoths = new HashSet<uint>();
		IBot bot;
		TechTree techTree;
		DomainIndex domainIndex;
		EconomyTroopProductionBotModule readiness;
		SquadManagerBotModule squadManager;
		IBotUnitReservations[] reservations;
		IBotTransportReservations[] transportReservations;
		Actor target;
		uint objectiveTargetId;
		CPos objective;
		int scanTicks;
		int lastOrderTick;
		int missionStartedTick;
		int lastProgressTick;
		int cooldownUntilTick;
		long bestDistanceSquared = long.MaxValue;
		int missionDamage;
		bool crushContactObserved;
		string lastState;

		public EconomyMammothCrushBotModule(Actor self, EconomyMammothCrushBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			techTree = player.PlayerActor.Trait<TechTree>();
			domainIndex = world.WorldActor.Trait<DomainIndex>();
			reservations = player.PlayerActor.TraitsImplementing<IBotUnitReservations>().ToArray();
			transportReservations = player.PlayerActor.TraitsImplementing<IBotTransportReservations>().ToArray();
			RefreshCollaborators();
			base.Created(self);
		}

		protected override void TraitEnabled(Actor self) { scanTicks = 1; }

		protected override void TraitDisabled(Actor self) { ReturnToSquad("bot condition disabled"); }

		void IBotEnabled.BotEnabled(IBot enabledBot) { bot = enabledBot; }

		bool IBotTemporaryUnitControl.IsUnitTemporarilyControlled(Actor actor)
		{
			return actor != null && mammoths.Contains(actor.ActorID);
		}

		void IBotTick.BotTick(IBot enabledBot)
		{
			if (IsTraitDisabled || player.WinState != WinState.Undefined || --scanTicks > 0)
				return;

			scanTicks = Info.ScanInterval;
			RefreshCollaborators();
			if (!techTree.HasPrerequisites(Info.RequiredPrerequisites) || readiness?.IsReadyForRaid != true)
			{
				ReturnToSquad("readiness or Economy authority lost");
				return;
			}

			if (mammoths.Count > 0)
			{
				UpdateMission();
				return;
			}

			if (world.WorldTick < cooldownUntilTick)
				return;

			TryStartMission();
		}

		void RefreshCollaborators()
		{
			if (readiness == null || readiness.IsTraitDisabled)
				readiness = player.PlayerActor.TraitsImplementing<EconomyTroopProductionBotModule>()
					.FirstOrDefault(m => !m.IsTraitDisabled);

			if (squadManager == null || squadManager.IsTraitDisabled)
				squadManager = player.PlayerActor.TraitsImplementing<SquadManagerBotModule>()
					.FirstOrDefault(m => !m.IsTraitDisabled);
		}

		void TryStartMission()
		{
			if (squadManager == null)
			{
				LogState("waiting: squad manager unavailable");
				return;
			}

			var ownedMammoths = world.Actors.Where(a => Info.MammothTypes.Contains(a.Info.Name) && IsOwnedUsable(a))
				.OrderBy(a => a.ActorID).ToList();
			var candidates = ownedMammoths.Where(IsEligibleMammoth).ToList();
			if (candidates.Count == 0)
			{
				var resupplying = ownedMammoths.Count(IsResupplying);
				var reserved = ownedMammoths.Count(a => reservations.Any(r => r.IsUnitReserved(a)));
				var transportReserved = ownedMammoths.Count(a => transportReservations.Any(r => r.IsTransportReserved(a)));
				var protecting = ownedMammoths.Count(a => squadManager.IsUnitProtectingBase(a));
				var lowHealth = ownedMammoths.Count(a =>
				{
					var health = a.TraitOrDefault<IHealth>();
					return health == null || health.HP * 100L < health.MaxHP * Info.MinimumHealthPercent;
				});
				LogState($"waiting: no eligible Mammoth total={ownedMammoths.Count} resupplying={resupplying} reserved={reserved} transport={transportReserved} protection={protecting} low-health={lowHealth}");
				return;
			}

			Actor anchor = null;
			Actor squadTarget = null;
			var formationCenter = WPos.Zero;
			var missionObjective = CPos.Zero;
			var missingSquad = new List<Actor>();
			var invalidTarget = new List<Actor>();
			var urgentMission = new List<Actor>();
			var readyMammoths = new List<Actor>();
			foreach (var candidate in candidates)
			{
				var status = squadManager.GetGeneralGroundMissionStatus(candidate, out var candidateTarget,
					out var candidateFormationCenter, out var candidateObjective);
				switch (status)
				{
					case GeneralGroundMissionStatus.MissingSquad:
						missingSquad.Add(candidate);
						break;
					case GeneralGroundMissionStatus.InvalidTarget:
						invalidTarget.Add(candidate);
						break;
					case GeneralGroundMissionStatus.Urgent:
						urgentMission.Add(candidate);
						break;
					case GeneralGroundMissionStatus.Ready:
						readyMammoths.Add(candidate);
						if (anchor == null)
						{
							anchor = candidate;
							squadTarget = candidateTarget;
							formationCenter = candidateFormationCenter;
							missionObjective = candidateObjective;
						}

						break;
				}
			}

			if (anchor == null)
			{
				LogState($"waiting: no non-urgent general mission missing-squad={DescribeActorIds(missingSquad)} invalid-target={DescribeActorIds(invalidTarget)} urgent={DescribeActorIds(urgentMission)}");
				return;
			}

			candidates = readyMammoths
				.OrderBy(a => (a.CenterPosition - formationCenter).HorizontalLengthSquared)
				.ThenBy(a => a.ActorID)
				.Take(Info.MaximumMammothCandidates).ToList();

			var selectedTarget = SelectTarget(candidates, formationCenter,
				world.Map.CenterOfCell(missionObjective), out var targetScan);
			if (selectedTarget == null)
			{
				LogState($"waiting: no valid infantry target objective={squadTarget.Info.Name}#{squadTarget.ActorID} cell={missionObjective} visible={targetScan.Visible} route={targetScan.NearRoute} local={targetScan.Local} crushable={targetScan.Crushable} reachable={targetScan.Reachable} bounded={targetScan.Bounded} dense={targetScan.Dense} dangerous={targetScan.Dangerous} safe={targetScan.Safe}");
				return;
			}

			var selectedMammoths = candidates.Where(a =>
				(a.CenterPosition - selectedTarget.CenterPosition).HorizontalLengthSquared <=
				(long)WDist.FromCells(Info.MaximumTargetDistanceCells).Length * WDist.FromCells(Info.MaximumTargetDistanceCells).Length &&
				CanCrush(a, selectedTarget) && IsReachable(a, selectedTarget.Location))
				.OrderBy(a => (a.CenterPosition - selectedTarget.CenterPosition).HorizontalLengthSquared)
				.ThenBy(a => a.ActorID).Take(Info.MaximumMammoths).ToList();
			if (selectedMammoths.Count == 0)
			{
				LogState($"waiting: target has no eligible crusher target={selectedTarget.Info.Name}#{selectedTarget.ActorID}");
				return;
			}

			mammoths.UnionWith(selectedMammoths.Select(a => a.ActorID));
			target = selectedTarget;
			objectiveTargetId = squadTarget.ActorID;
			objective = missionObjective;
			missionStartedTick = lastProgressTick = world.WorldTick;
			bestDistanceSquared = long.MaxValue;
			missionDamage = 0;
			crushContactObserved = false;
			IssueCrushOrders(selectedMammoths);
			LogState($"started mammoths={string.Join(",", selectedMammoths.Select(a => $"{a.ActorID}@{a.Location}"))} target={target.Info.Name}#{target.ActorID}@{target.Location} formation={world.Map.CellContaining(formationCenter)} objective={objectiveTargetId}@{objective}");
		}

		void INotifyAppliedDamage.AppliedDamage(Actor self, Actor damaged, AttackInfo e)
		{
			if (IsTraitDisabled || target == null || damaged != target || e.Attacker == null ||
				player.RelationshipWith(damaged.Owner) != PlayerRelationship.Enemy)
				return;

			var applied = Math.Max(0, e.Damage.Value);
			if (applied <= 0)
				return;

			var killed = e.DamageState == DamageState.Dead && e.PreviousDamageState != DamageState.Dead;
			var contactKill = killed && e.Damage.DamageTypes.IsEmpty;
			if (!mammoths.Contains(e.Attacker.ActorID))
			{
				if (killed)
					LogState($"target invalidated by external damage attacker={e.Attacker.Info.Name}#{e.Attacker.ActorID}@{e.Attacker.Location} target={damaged.Info.Name}#{damaged.ActorID}@{damaged.Location} damage={applied} contact-kill={contactKill} distance-squared={(e.Attacker.CenterPosition - damaged.CenterPosition).HorizontalLengthSquared}");

				return;
			}

			var firstDamage = missionDamage == 0;
			missionDamage += applied;
			crushContactObserved |= contactKill;
			if (firstDamage || killed)
				LogState($"outcome mammoth={e.Attacker.Info.Name}#{e.Attacker.ActorID} target={damaged.Info.Name}#{damaged.ActorID} damage={applied} total-damage={missionDamage} killed={killed} contact-kill={contactKill} distance-squared={(e.Attacker.CenterPosition - damaged.CenterPosition).HorizontalLengthSquared}");
		}

		void UpdateMission()
		{
			var active = mammoths.Select(world.GetActorById).Where(IsOwnedUsable).OrderBy(a => a.ActorID).ToList();
			if (active.Count == 0)
			{
				ReturnToSquad("crusher destroyed or ownership changed");
				return;
			}

			var targetInvalidation = TargetInvalidation(active);
			if (targetInvalidation != null)
			{
				ReturnToSquad($"target invalidated ({targetInvalidation})");
				return;
			}

			foreach (var mammoth in active)
			{
				if (!IsEligibleMammoth(mammoth))
				{
					ReturnToSquad($"crusher eligibility changed actor={mammoth.ActorID}@{mammoth.Location}");
					return;
				}

				var status = squadManager.GetGeneralGroundMissionStatus(mammoth, out var currentObjective,
					out var formationCenter, out _);
				if (status != GeneralGroundMissionStatus.Ready)
				{
					ReturnToSquad($"general mission changed actor={mammoth.ActorID}@{mammoth.Location} status={status}");
					return;
				}

				if (!EconomyTroopPolicy.IsSameCrushObjective(objectiveTargetId, currentObjective.ActorID))
				{
					ReturnToSquad($"objective actor changed actor={mammoth.ActorID}@{mammoth.Location} expected={objectiveTargetId} actual={currentObjective.Info.Name}#{currentObjective.ActorID}@{currentObjective.Location}");
					return;
				}

				if (!EconomyTroopPolicy.IsNearRoute(target.CenterPosition, formationCenter,
					world.Map.CenterOfCell(objective), WDist.FromCells(Info.MaximumRouteDetourCells)))
				{
					ReturnToSquad($"target left route target={target.Info.Name}#{target.ActorID}@{target.Location} formation={world.Map.CellContaining(formationCenter)} objective={objective}");
					return;
				}

				if (!EconomyTroopPolicy.IsNearRoute(mammoth.CenterPosition, formationCenter,
					world.Map.CenterOfCell(objective), WDist.FromCells(Info.LeashCells)))
				{
					ReturnToSquad($"leash breached actor={mammoth.ActorID}@{mammoth.Location} formation={world.Map.CellContaining(formationCenter)} objective={objective}");
					return;
				}
			}

			if (NearbyInfantryCount(target) > Info.MaximumNearbyInfantry ||
				NearbyArmedThreatCount(target) > Info.MaximumNearbyArmedThreats)
			{
				ReturnToSquad("local threat escalated");
				return;
			}

			var distanceSquared = active.Min(a =>
				(a.CenterPosition - target.CenterPosition).HorizontalLengthSquared);
			if (distanceSquared < bestDistanceSquared)
			{
				bestDistanceSquared = distanceSquared;
				lastProgressTick = world.WorldTick;
			}

			if (EconomyTroopPolicy.MissionExpired(world.WorldTick, missionStartedTick, lastProgressTick,
				Info.MissionTimeout, Info.NoProgressTimeout))
			{
				ReturnToSquad("timeout or no progress");
				return;
			}

			if (world.WorldTick >= lastOrderTick + Info.OrderInterval)
				IssueCrushOrders(active);
		}

		Actor SelectTarget(List<Actor> candidates, WPos formationCenter, WPos missionObjective,
			out TargetScan scan)
		{
			scan = new TargetScan();
			var maximumDistance = WDist.FromCells(Info.MaximumTargetDistanceCells).Length;
			var maximumDistanceSquared = (long)maximumDistance * maximumDistance;
			var cheapTargets = new List<Actor>();
			foreach (var infantry in world.Actors)
			{
				if (!IsEnemyVisibleInfantry(infantry))
					continue;

				scan.Visible++;
				if (!EconomyTroopPolicy.IsNearRoute(infantry.CenterPosition, formationCenter, missionObjective,
					WDist.FromCells(Info.MaximumRouteDetourCells)))
					continue;

				scan.NearRoute++;
				var local = false;
				var crushable = false;
				foreach (var mammoth in candidates)
				{
					if ((mammoth.CenterPosition - infantry.CenterPosition).HorizontalLengthSquared > maximumDistanceSquared)
						continue;

					local = true;
					if (!CanCrush(mammoth, infantry))
						continue;

					crushable = true;
					break;
				}

				if (local)
					scan.Local++;

				if (crushable)
					scan.Crushable++;

				if (crushable)
					cheapTargets.Add(infantry);
			}

			var boundedTargets = cheapTargets
				.OrderBy(a => (a.CenterPosition - formationCenter).HorizontalLengthSquared)
				.ThenBy(a => a.ActorID).Take(Info.MaximumTargetCandidates).ToList();
			scan.Bounded = boundedTargets.Count;
			var safeTargets = new List<Actor>();
			foreach (var infantry in boundedTargets)
			{
				if (!candidates.Any(mammoth => CanCrush(mammoth, infantry) &&
					IsReachable(mammoth, infantry.Location)))
					continue;

				scan.Reachable++;
				if (NearbyInfantryCount(infantry) > Info.MaximumNearbyInfantry)
				{
					scan.Dense++;
					continue;
				}

				if (NearbyArmedThreatCount(infantry) > Info.MaximumNearbyArmedThreats)
				{
					scan.Dangerous++;
					continue;
				}

				scan.Safe++;
				safeTargets.Add(infantry);
			}

			return safeTargets
				.OrderByDescending(a => Info.PreferredInfantryTypes.Contains(a.Info.Name))
				.ThenBy(a => (a.CenterPosition - formationCenter).HorizontalLengthSquared)
				.ThenBy(a => a.ActorID).FirstOrDefault();
		}

		static string DescribeActorIds(IReadOnlyCollection<Actor> actors)
		{
			if (actors.Count == 0)
				return "none";

			const int maximumIds = 8;
			var ids = string.Join(",", actors.Take(maximumIds).Select(a => a.ActorID));
			return actors.Count <= maximumIds ? ids : $"{ids}(+{actors.Count - maximumIds})";
		}

		string TargetInvalidation(IReadOnlyCollection<Actor> active)
		{
			if (target == null)
				return "missing";

			var description = $"{target.Info.Name}#{target.ActorID}@{target.Location}";
			if (target.IsDead)
				return $"dead {description}";

			if (!target.IsInWorld)
				return $"out-of-world {description}";

			if (!Info.InfantryTypes.Contains(target.Info.Name))
				return $"wrong-type {description}";

			if (player.RelationshipWith(target.Owner) != PlayerRelationship.Enemy)
				return $"not-enemy {description}";

			if (!player.Shroud.IsVisible(target.Location))
				return $"hidden {description}";

			if (!target.CanBeViewedByPlayer(player))
				return $"unviewable {description}";

			var unableToCrush = active.Where(a => !CanCrush(a, target)).OrderBy(a => a.ActorID).ToList();
			return unableToCrush.Count == 0 ? null :
				$"uncrushable {description} crushers={DescribeActorIds(unableToCrush)}";
		}

		bool IsEligibleMammoth(Actor actor)
		{
			if (!IsOwnedUsable(actor) || IsResupplying(actor) ||
				reservations.Any(r => r.IsUnitReserved(actor)) ||
				transportReservations.Any(r => r.IsTransportReserved(actor)) ||
				(squadManager?.IsUnitProtectingBase(actor) ?? false))
				return false;

			var health = actor.TraitOrDefault<IHealth>();
			return health != null && health.HP * 100L >= health.MaxHP * Info.MinimumHealthPercent;
		}

		bool IsOwnedUsable(Actor actor)
		{
			return actor != null && actor.Owner == player && actor.IsInWorld && !actor.IsDead;
		}

		static bool IsResupplying(Actor actor)
		{
			return actor.CurrentActivity is Resupply || actor.CurrentActivity?.NextActivity is Resupply;
		}

		bool IsEnemyVisibleInfantry(Actor actor)
		{
			return actor != null && actor.IsInWorld && !actor.IsDead && Info.InfantryTypes.Contains(actor.Info.Name) &&
				player.RelationshipWith(actor.Owner) == PlayerRelationship.Enemy &&
				player.Shroud.IsVisible(actor.Location) && actor.CanBeViewedByPlayer(player);
		}

		static bool CanCrush(Actor mammoth, Actor infantry)
		{
			var mobile = mammoth.TraitOrDefault<Mobile>();
			return mobile != null && infantry.TraitsImplementing<ICrushable>()
				.Any(c => c.CrushableBy(infantry, mammoth, mobile.Info.LocomotorInfo.Crushes));
		}

		bool IsReachable(Actor actor, CPos destination)
		{
			var mobile = actor.TraitOrDefault<Mobile>();
			return mobile != null && domainIndex.IsPassable(actor.Location, destination, mobile.Locomotor);
		}

		int NearbyInfantryCount(Actor center)
		{
			return world.FindActorsInCircle(center.CenterPosition, WDist.FromCells(Info.DenseInfantryRadiusCells))
				.Count(IsEnemyVisibleInfantry);
		}

		int NearbyArmedThreatCount(Actor center)
		{
			return world.FindActorsInCircle(center.CenterPosition, WDist.FromCells(Info.ThreatRadiusCells))
				.Count(a => a != center && a.IsInWorld && !a.IsDead &&
					player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy && a.Info.HasTraitInfo<AttackBaseInfo>());
		}

		void IssueCrushOrders(IEnumerable<Actor> active)
		{
			if (bot == null || target == null)
				return;

			lastOrderTick = world.WorldTick;
			foreach (var mammoth in active)
			{
				var crushMove = mammoth.TraitOrDefault<EconomyMammothCrushMove>();
				if (crushMove == null || crushMove.ShouldIssueOrder(mammoth, target))
					bot.QueueOrder(new Order(EconomyMammothCrushMove.OrderId, mammoth, Target.FromActor(target), false));
			}
		}

		void ReturnToSquad(string reason)
		{
			var returning = mammoths.Select(world.GetActorById).Where(IsOwnedUsable).OrderBy(a => a.ActorID).ToArray();
			var targetDescription = target == null ? "none" :
				$"{target.Info.Name}#{target.ActorID}@{target.Location}:{(target.IsDead ? "dead" : target.IsInWorld ? "alive" : "out-of-world")}";
			var duration = missionStartedTick > 0 ? world.WorldTick - missionStartedTick : 0;
			var returnCell = objective;
			if (returnCell == CPos.Zero && squadManager != null)
				returnCell = squadManager.GroundRecoveryDestination();

			mammoths.Clear();
			target = null;
			objectiveTargetId = 0;
			objective = CPos.Zero;
			missionStartedTick = lastProgressTick = lastOrderTick = 0;
			bestDistanceSquared = long.MaxValue;
			if (returning.Length > 0 && bot != null)
			{
				bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(world, returnCell), false,
					groupedActors: returning));
				cooldownUntilTick = Math.Max(cooldownUntilTick, world.WorldTick + Info.GroupCooldown);
				LogState($"returned reason={reason} actors={string.Join(",", returning.Select(a => $"{a.ActorID}@{a.Location}"))} target={targetDescription} duration={duration} damage={missionDamage} contact-kill={crushContactObserved} return={returnCell} cooldown={cooldownUntilTick}");
			}

			missionDamage = 0;
			crushContactObserved = false;
		}

		void LogState(string state)
		{
			if (state == lastState)
				return;

			lastState = state;
			if (!Info.DebugLogging)
				return;

			AIUtils.BotDebug("AI ({0}) economy Mammoth crush: {1}", player.ClientIndex, state);
			Log.Write("debug", "AI economy Mammoth crush: {0} (client {1}) at tick {2}: {3}",
				player, player.ClientIndex, world.WorldTick, state);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			return IsTraitDisabled ? null : new List<MiniYamlNode>
			{
				new MiniYamlNode("EconomyMammothCrushScanTicks", FieldSaver.FormatValue(scanTicks)),
				new MiniYamlNode("EconomyMammothCrushLastOrderTick", FieldSaver.FormatValue(lastOrderTick)),
				new MiniYamlNode("EconomyMammothCrushStartedTick", FieldSaver.FormatValue(missionStartedTick)),
				new MiniYamlNode("EconomyMammothCrushLastProgressTick", FieldSaver.FormatValue(lastProgressTick)),
				new MiniYamlNode("EconomyMammothCrushCooldownUntil", FieldSaver.FormatValue(cooldownUntilTick)),
				new MiniYamlNode("EconomyMammothCrushBestDistance", FieldSaver.FormatValue(bestDistanceSquared)),
				new MiniYamlNode("EconomyMammothCrushDamage", FieldSaver.FormatValue(missionDamage)),
				new MiniYamlNode("EconomyMammothCrushContact", FieldSaver.FormatValue(crushContactObserved)),
				new MiniYamlNode("EconomyMammothCrushTarget", FieldSaver.FormatValue(target?.ActorID ?? 0)),
				new MiniYamlNode("EconomyMammothCrushObjectiveTarget", FieldSaver.FormatValue(objectiveTargetId)),
				new MiniYamlNode("EconomyMammothCrushObjective", FieldSaver.FormatValue(objective)),
				new MiniYamlNode("EconomyMammothCrushMammoths", FieldSaver.FormatValue(mammoths.OrderBy(id => id).ToArray()))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			foreach (var node in data)
				switch (node.Key)
				{
					case "EconomyMammothCrushScanTicks": scanTicks = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyMammothCrushLastOrderTick": lastOrderTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyMammothCrushStartedTick": missionStartedTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyMammothCrushLastProgressTick": lastProgressTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyMammothCrushCooldownUntil": cooldownUntilTick = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyMammothCrushBestDistance": bestDistanceSquared = FieldLoader.GetValue<long>(node.Key, node.Value.Value); break;
					case "EconomyMammothCrushDamage": missionDamage = FieldLoader.GetValue<int>(node.Key, node.Value.Value); break;
					case "EconomyMammothCrushContact": crushContactObserved = FieldLoader.GetValue<bool>(node.Key, node.Value.Value); break;
					case "EconomyMammothCrushTarget":
						var targetId = FieldLoader.GetValue<uint>(node.Key, node.Value.Value);
						target = targetId == 0 ? null : world.GetActorById(targetId);
						break;
					case "EconomyMammothCrushObjectiveTarget": objectiveTargetId = FieldLoader.GetValue<uint>(node.Key, node.Value.Value); break;
					case "EconomyMammothCrushObjective": objective = FieldLoader.GetValue<CPos>(node.Key, node.Value.Value); break;
					case "EconomyMammothCrushMammoths":
						mammoths.Clear();
						mammoths.UnionWith(FieldLoader.GetValue<uint[]>(node.Key, node.Value.Value));
						break;
				}
		}
	}
}
