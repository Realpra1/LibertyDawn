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
using System.Diagnostics;
using System.Linq;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	public enum SquadType { Assault, Air, Rush, Protection, Naval, GeneralAttack, Stealth }

	public struct StealthDebugLifecycleSnapshot
	{
		public int Tick;
		public int CadenceAge;
		public int Reason;
		public uint ActorId;
		public string Role;
		public StealthClearMode Mode;
		public string TargetName;
		public uint TargetId;
		public bool HasTarget;
		public CPos TargetCell;
		public bool CanAttack;
		public CPos ActorCell;
		public int Distance;
		public bool RouteQueued;
		public int RouteBuffer;
		public string RoutePhase;
		public int ProgressAge;
		public bool Safety;
		public CPos EscapeDestination;
		public bool HasEscapeDestination;
		public bool Firing;
		public int MaximumFireDelay;
		public int HP;
		public int MaxHP;
		public bool Repair;
		public uint RepairTargetId;
		public bool HasRepairTarget;
		public bool Idle;
		public string CurrentActivity;
		public int CurrentActivityState;
		public bool CurrentActivityCanceling;
		public string NextActivity;
		public int NextActivityState;
		public bool NextActivityCanceling;
		public string FinalActivity;
		public int FinalActivityState;
		public bool FinalActivityCanceling;
		public int ActivityDepth;
		public TargetType OrderTargetType;
		public string OrderTargetName;
		public uint OrderTargetId;
		public CPos OrderTargetCell;
		public int Signature;
	}

	public class Squad
	{
		public List<Actor> Units = new List<Actor>();
		public SquadType Type;
		public string AirSquadDefinition;
		public string StealthSquadDefinition;
		public int StealthSquadIndex;

		internal IBot Bot;
		internal World World;
		internal SquadManagerBotModule SquadManager;
		internal MersenneTwister Random;

		internal Target Target;
		internal StateMachine FuzzyStateMachine;
		StealthSquadLifecycleRuntimeHost stealthLifecycleRuntime;
		internal bool UsesModularStealthLifecycle => UsesModularStealthLifecycleFor(
			Type, StealthSquadDefinition, SquadManager.Info.UseModularStealthLifecycle);
		internal static bool UsesModularStealthLifecycleFor(SquadType type,
			string definition, bool managerEnabled)
		{
			return managerEnabled && type == SquadType.Stealth &&
				string.Equals(definition, "stealth-tank", StringComparison.Ordinal);
		}

		internal static bool LegacyStealthAuthorityAllowed(bool modularRuntimeEnabled)
		{
			return !modularRuntimeEnabled;
		}

		// Where this squad last saw enemy anti-air, and the tick each sighting is forgotten on.
		// Purely advisory bot state: it never touches the synced simulation, is not saved with the
		// game, and is only ever written from the host-only bot tick. Kept as two parallel lists so
		// the positions can be handed to AirThreatGeometry without copying.
		internal readonly List<WPos> AirThreatPositions = new List<WPos>();
		readonly List<int> airThreatExpiry = new List<int>();

		// Earliest tick at which the squad may issue another retreat. Stops an air squad sitting in
		// anti-air cover from re-issuing move orders on every safety check.
		internal int NextAirRetreatTick;

		// Consecutive AirIdleState scans that found no worthwhile target. This gradually increases the
		// willingness to accept a low score, but coarse route danger always remains part of the cost.
		internal int AirConsecutiveNoTargetScans;
		internal int AirConsecutiveNoUndefendedScans;
		internal readonly List<CPos> AirRoute = new List<CPos>();
		internal bool AirRouteQueued;
		internal readonly HashSet<uint> AirUnitsRepairing = new HashSet<uint>();
		internal readonly Dictionary<uint, uint> AirRepairTargets = new Dictionary<uint, uint>();
		internal readonly HashSet<uint> AirRepairWaiting = new HashSet<uint>();
		internal readonly Dictionary<uint, int> AirRepairWaitingSince = new Dictionary<uint, int>();
		internal readonly HashSet<uint> AirRepairUnavailable = new HashSet<uint>();
		internal readonly HashSet<uint> AirReinforcements = new HashSet<uint>();
		internal readonly Dictionary<uint, uint> AirReinforcementTargets = new Dictionary<uint, uint>();
		internal readonly Dictionary<uint, CPos> AirReinforcementJoinCells = new Dictionary<uint, CPos>();
		internal readonly Dictionary<uint, CPos> AirReinforcementFallbackCells = new Dictionary<uint, CPos>();
		internal readonly Dictionary<uint, int> AirReinforcementFallbackTicks = new Dictionary<uint, int>();
		internal int StealthReinforcementRouteIssues;
		internal int StealthReinforcementRoutePreserves;
		internal int StealthCatchUpWorkRequestedTick = -1;
		internal int StealthLocalPlanningWorkRequestedTick = -1;
		internal bool StealthLocalSafetyRequested;
		internal bool StealthLiveTargetRequested;
		internal bool StealthBlueSafetyRequested;
		internal bool StealthRevealedIdleSafetyRequested;
		internal readonly HashSet<uint> StealthRevealedIdleSafetyCloakArmed = new HashSet<uint>();
		internal readonly HashSet<uint> StealthRevealedIdleSafetyPending = new HashSet<uint>();

		internal long StealthLiveLocalDiagnosticSamples;
		internal long StealthLiveLocalDiagnosticEmitted;
		internal long StealthLiveLocalDiagnosticChanges;
		internal bool StealthLiveLocalDiagnosticHasSignature;
		internal int StealthLiveLocalDiagnosticSignature;
		internal int StealthLiveLocalDiagnosticNextSummaryTick = -1;
		internal int StealthCoreRouteIssues;
		internal int StealthCoreRoutePreserves;
		internal int StealthLastFrontierTargetCells;
		internal CPos? StealthTargetlessApproachCell;
		internal int StealthTargetlessApproachStartedTick = -1;
		internal int StealthTargetlessApproachSteps;
		internal readonly HashSet<CPos> StealthTargetlessRejectedCells = new HashSet<CPos>();
		internal CPos? StealthRouteLastCenterCell;
		internal int StealthRouteLastCenterProgressTick;

		internal Dictionary<uint, Queue<StealthDebugLifecycleSnapshot>> StealthDebugLifecycle;
		internal Dictionary<uint, (int Signature, int LastReportTick)> StealthDebugLifecycleState;
		internal int StealthDebugLifecycleLastCadenceAge = -1;
		internal StealthKillCadenceGeneration StealthKillCadenceGeneration;
		internal int StealthKillCadenceAge => StealthKillCadenceGeneration?.CadenceAge ?? 0;
		internal int StealthDebugKillCadenceNextReportTick = -1;
		internal int StealthDebugKillCadenceKills => StealthKillCadenceGeneration?.AttributedKills ?? 0;
		internal bool StealthDebugKillCadenceFailed =>
			StealthKillCadenceGeneration?.CadenceFailed == true ||
			StealthKillCadenceGeneration?.MismatchFailed == true;
		internal int StealthEmptySinceTick = -1;
		WPos airLastFormationCenter;
		bool hasAirFormationCenter;
		internal CPos? AirTargetStrategicCell;
		internal int AirTargetLastProgressTick;
		internal int AirTargetLastDistanceCells = int.MaxValue;
		internal int AirTargetLastHP = int.MaxValue;
		internal int AirTargetScore = int.MinValue;
		internal bool AirTargetIsUndefended;
		internal bool AirTargetClearsAa;
		internal CPos? AirAaClearProtectedCell;
		internal readonly HashSet<uint> AirAaClearThreatIds = new HashSet<uint>();
		internal bool AirAaClearEngaged;
		internal float AirLocalThreatWeight;
		internal int AirNextTargetReviewTick;
		internal bool AirEscapingLocalAa;
		internal int StealthEscapeIssuedTick = -1;
		internal int StealthEscapeSafetyChecks;
		internal CPos? StealthEscapeDestination;
		internal CPos? StealthEscapeStartCell;
		internal CPos? StealthEscapeDestinationCell;
		internal bool StealthEscapePendingExplosion;
		internal int StealthEscapeLastProgressTick = -1;
		internal int StealthEscapeLastDistanceCells = int.MaxValue;
		internal bool StealthEscapePreserveEngagement;
		internal bool StealthEscapeNeedsActivityRestore;
		internal CPos? StealthKiteTargetCell;
		internal CPos? StealthCrushTargetCell;
		internal CPos? StealthPostAttackCell;
		internal readonly Dictionary<uint, CPos> StealthValidatedFiringCells = new Dictionary<uint, CPos>();
		internal uint StealthKiteSupersessionActorId;
		internal int StealthKiteSupersessionConfirmations;
		internal readonly Dictionary<uint, int> StealthKiteParticipantHealth = new Dictionary<uint, int>();
		internal uint StealthCrushLeaderActorId;
		internal StealthClearMode StealthClearMode;
		internal bool StealthAggressiveMass;
		internal int StealthMassPolicyNextReportTick;
		internal int StealthLocalPolicyNextReportTick;
		internal int StealthEngagementNextReportTick;
		internal readonly HashSet<uint> StealthClearPackage = new HashSet<uint>();
		internal int StealthClearMembershipSignature;
		internal CPos? StealthClearCenterCell;

		// General-attack reinforcements remain squad-owned while traveling, but do not pull the
		// established formation center home or inflate its strategic strength before they arrive.
		internal readonly HashSet<uint> GroundReinforcements = new HashSet<uint>();
		internal int GroundNextTargetReviewTick;
		WPos groundLastFormationCenter;
		bool hasGroundFormationCenter;
		bool groundHoldingForReinforcements;

		public Squad(IBot bot, SquadManagerBotModule squadManager, SquadType type)
			: this(bot, squadManager, type, null) { }

		public Squad(IBot bot, SquadManagerBotModule squadManager, SquadType type, Actor target)
		{
			Bot = bot;
			SquadManager = squadManager;
			World = bot.Player.PlayerActor.World;
			Random = World.LocalRandom;
			Type = type;
			Target = Target.FromActor(target);
			FuzzyStateMachine = new StateMachine();

			switch (type)
			{
				case SquadType.Assault:
				case SquadType.Rush:
				case SquadType.GeneralAttack:
					FuzzyStateMachine.ChangeState(this, new GroundUnitsIdleState(), true);
					break;
				case SquadType.Air:
					FuzzyStateMachine.ChangeState(this, new AirIdleState(), true);
					break;
				case SquadType.Stealth:
					FuzzyStateMachine.ChangeState(this, new StealthAIIdleState(), true);
					break;
				case SquadType.Protection:
					FuzzyStateMachine.ChangeState(this, new UnitsForProtectionIdleState(), true);
					break;
				case SquadType.Naval:
					FuzzyStateMachine.ChangeState(this, new NavyUnitsIdleState(), true);
					break;
			}
		}

		public void Update()
		{
			if (IsValid)
			{
				if (UsesModularStealthLifecycle)
				{
					if (stealthLifecycleRuntime == null)
						stealthLifecycleRuntime = new StealthSquadLifecycleRuntimeHost(this);
					stealthLifecycleRuntime.Tick();
					return;
				}

				if (Type == SquadType.GeneralAttack)
				{
					UpdateGroundReinforcements();
					if (GroundFormationUnits(bootstrapIfEmpty: true).Count == 0)
						return;
				}

				if ((Type == SquadType.Air || Type == SquadType.Stealth) && Game.IsBenchmarking)
					BenchmarkAirWork("strategy", () => FuzzyStateMachine.Update(this));
				else
					FuzzyStateMachine.Update(this);
			}
		}

		/// <summary>
		/// Short-interval anti-air awareness for air squads, run independently of the squad state
		/// machine so danger is noticed on approach, mid-attack and on the way home alike.
		/// </summary>
		public void TickAirSafety()
		{
			if (!LegacyStealthAuthorityAllowed(UsesModularStealthLifecycle))
				return;

			if (IsValid && (Type == SquadType.Air || Type == SquadType.Stealth))
			{
				if (Game.IsBenchmarking)
					BenchmarkAirWork("local-safety", () =>
					{
						if (Type == SquadType.Stealth)
							StealthAIStateBase.TickStealthSafety(this);
						else
							AirStateBase.TickAirSafety(this);
					});
				else if (Type == SquadType.Stealth)
					StealthAIStateBase.TickStealthSafety(this);
				else
					AirStateBase.TickAirSafety(this);
			}
		}

		internal void TickModularStealthLocalSafety()
		{
			if (!UsesModularStealthLifecycle || !IsValid)
				return;
			if (stealthLifecycleRuntime == null)
				stealthLifecycleRuntime = new StealthSquadLifecycleRuntimeHost(this);
			stealthLifecycleRuntime.TickLocalSafety();
		}

		public void TickStealthBlueSafety()
		{
			if (!LegacyStealthAuthorityAllowed(UsesModularStealthLifecycle))
				return;

			if (!IsValid || Type != SquadType.Stealth)
				return;

			if (Game.IsBenchmarking)
				BenchmarkAirWork("blue-safety", () => StealthAIStateBase.TickStealthSafety(this, true));
			else
				StealthAIStateBase.TickStealthSafety(this, true);
		}

		public void TickStealthLiveTarget()
		{
			if (!LegacyStealthAuthorityAllowed(UsesModularStealthLifecycle))
				return;

			if (IsValid && Type == SquadType.Stealth)
				StealthAIStateBase.TickStealthLiveTarget(this);
		}

		public bool TickStealthRevealedIdleSafety(out bool repositionIssued)
		{
			repositionIssued = false;
			if (!LegacyStealthAuthorityAllowed(UsesModularStealthLifecycle))
				return true;

			return !IsValid || Type != SquadType.Stealth ||
				StealthAIStateBase.TickStealthRevealedIdleSafety(this, out repositionIssued);
		}

		internal bool ObserveModularStealthDamage(Actor damaged, AttackInfo attack)
		{
			if (!UsesModularStealthLifecycle)
				return false;
			if (stealthLifecycleRuntime == null)
				stealthLifecycleRuntime = new StealthSquadLifecycleRuntimeHost(this);
			stealthLifecycleRuntime.ObserveDamage(damaged, attack);
			return true;
		}

		void BenchmarkAirWork(string phase, Action work)
		{
			var start = Stopwatch.GetTimestamp();
			var modularBot = Bot as ModularBot;
			var queuedOrders = modularBot?.QueuedOrderCount ?? 0;
			try { work(); }
			finally
			{
				var elapsed = 1000.0 * Math.Max(0, Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency;
				var addedOrders = modularBot == null ? 0 : modularBot.QueuedOrderCount - queuedOrders;
				var category = Type == SquadType.Stealth ? "StealthSquad" : "AirSquad";
				Game.RecordBotModuleSample(Bot.Player.ClientIndex,
					$"{category}/{AirProfile}/{phase}", elapsed, Math.Max(0, addedOrders));
			}
		}

		/// <summary>Drops sightings that have aged out. Called before the memory is read or written.</summary>
		internal void ForgetExpiredAirThreats(int tick)
		{
			for (var i = AirThreatPositions.Count - 1; i >= 0; i--)
			{
				if (airThreatExpiry[i] > tick)
					continue;

				AirThreatPositions.RemoveAt(i);
				airThreatExpiry.RemoveAt(i);
			}
		}

		/// <summary>
		/// Records an anti-air sighting. Sightings closer together than <paramref name="mergeRadius"/>
		/// collapse into one entry so a cluster of SAMs cannot flood the (small, bounded) memory.
		/// </summary>
		internal void RememberAirThreat(WPos pos, int expiryTick, WDist mergeRadius, int maxCount)
		{
			if (maxCount <= 0)
				return;

			var mergeSquared = (long)mergeRadius.Length * mergeRadius.Length;
			for (var i = 0; i < AirThreatPositions.Count; i++)
			{
				long dx = AirThreatPositions[i].X - pos.X;
				long dy = AirThreatPositions[i].Y - pos.Y;
				if (dx * dx + dy * dy > mergeSquared)
					continue;

				// Refresh the existing sighting rather than adding a near-duplicate.
				if (airThreatExpiry[i] < expiryTick)
					airThreatExpiry[i] = expiryTick;

				return;
			}

			// Evict the oldest sighting when full.
			if (AirThreatPositions.Count >= maxCount)
			{
				var oldest = 0;
				for (var i = 1; i < airThreatExpiry.Count; i++)
					if (airThreatExpiry[i] < airThreatExpiry[oldest])
						oldest = i;

				AirThreatPositions.RemoveAt(oldest);
				airThreatExpiry.RemoveAt(oldest);
			}

			AirThreatPositions.Add(pos);
			airThreatExpiry.Add(expiryTick);
		}

		public bool IsValid => Units.Any();

		public Actor TargetActor
		{
			get => Target.Actor;
			set => Target = Target.FromActor(value);
		}

		public bool IsTargetValid => Target.IsValidFor(Units.FirstOrDefault()) && !Target.Actor.Info.HasTraitInfo<HuskInfo>();

		public bool IsTargetVisible => TargetActor.CanBeViewedByPlayer(Bot.Player);

		public WPos CenterPosition { get { return Units.Select(u => u.CenterPosition).Average(); } }

		/// <summary>
		/// Aircraft that have reached the formation. Repairing aircraft and reinforcements still traveling
		/// from a factory or repair pad remain squad-owned, but do not pull its tactical center away from
		/// the formation or increase the strength used for squad-level decisions.
		/// </summary>
		internal List<Actor> AirFormationUnits(bool bootstrapIfEmpty = false)
		{
			var formation = Units.Where(a => !AirUnitsRepairing.Contains(a.ActorID) &&
				!AirReinforcements.Contains(a.ActorID)).ToList();
			if (formation.Count == 0 && bootstrapIfEmpty)
				formation.AddRange(Units.Where(a => !AirUnitsRepairing.Contains(a.ActorID)));

			return formation;
		}

		internal WPos AirFormationCenter
		{
			get
			{
				var formation = AirFormationUnits();
				if (formation.Count > 0)
				{
					airLastFormationCenter = formation.Select(a => a.CenterPosition).Average();
					hasAirFormationCenter = true;
					return airLastFormationCenter;
				}

				// A new squad's first aircraft is immediately joined, so this fallback is only needed for
				// old saves or a formation that disappeared before its center was first observed.
				return hasAirFormationCenter ? airLastFormationCenter : CenterPosition;
			}
		}

		internal void MarkAirReinforcement(Actor actor)
		{
			AirReinforcements.Add(actor.ActorID);
			AirReinforcementTargets.Remove(actor.ActorID);
			AirReinforcementJoinCells.Remove(actor.ActorID);
			AirReinforcementFallbackCells.Remove(actor.ActorID);
			AirReinforcementFallbackTicks.Remove(actor.ActorID);
		}

		internal void MarkAirRepairing(Actor actor, Actor destination = null)
		{
			if (!hasAirFormationCenter && !AirReinforcements.Contains(actor.ActorID))
			{
				airLastFormationCenter = actor.CenterPosition;
				hasAirFormationCenter = true;
			}

			AirUnitsRepairing.Add(actor.ActorID);
			AirRepairWaiting.Remove(actor.ActorID);
			AirRepairWaitingSince.Remove(actor.ActorID);
			AirRepairUnavailable.Remove(actor.ActorID);
			if (destination == null)
				AirRepairTargets.Remove(actor.ActorID);
			else
				AirRepairTargets[actor.ActorID] = destination.ActorID;

			MarkAirReinforcement(actor);
		}

		internal void MarkAirRepairWaiting(Actor actor, Actor destination)
		{
			var firstWaitingTick = AirRepairWaitingSince.TryGetValue(actor.ActorID, out var tick) ?
				tick : World.WorldTick;
			MarkAirRepairing(actor, destination);
			AirRepairWaiting.Add(actor.ActorID);
			AirRepairWaitingSince.Add(actor.ActorID, firstWaitingTick);
		}

		internal void JoinAirFormation(Actor actor)
		{
			AirReinforcements.Remove(actor.ActorID);
			AirReinforcementTargets.Remove(actor.ActorID);
			AirReinforcementJoinCells.Remove(actor.ActorID);
			AirReinforcementFallbackCells.Remove(actor.ActorID);
			AirReinforcementFallbackTicks.Remove(actor.ActorID);
			airLastFormationCenter = actor.CenterPosition;
			hasAirFormationCenter = true;
		}

		internal void CleanAirMembership()
		{
			var live = new HashSet<uint>(Units.Select(a => a.ActorID));
			AirUnitsRepairing.RemoveWhere(id => !live.Contains(id));
			AirRepairWaiting.RemoveWhere(id => !live.Contains(id));
			foreach (var id in AirRepairWaitingSince.Keys.Where(id => !live.Contains(id)).ToList())
				AirRepairWaitingSince.Remove(id);
			AirRepairUnavailable.RemoveWhere(id => !live.Contains(id));
			foreach (var id in AirRepairTargets.Keys.Where(id => !live.Contains(id)).ToList())
				AirRepairTargets.Remove(id);

			AirReinforcements.RemoveWhere(id => !live.Contains(id));
			foreach (var id in AirReinforcementTargets.Keys.Where(id => !live.Contains(id)).ToList())
				AirReinforcementTargets.Remove(id);
			foreach (var id in AirReinforcementJoinCells.Keys.Where(id => !live.Contains(id)).ToList())
				AirReinforcementJoinCells.Remove(id);
			foreach (var id in AirReinforcementFallbackCells.Keys.Where(id => !live.Contains(id)).ToList())
				AirReinforcementFallbackCells.Remove(id);
			foreach (var id in AirReinforcementFallbackTicks.Keys.Where(id => !live.Contains(id)).ToList())
				AirReinforcementFallbackTicks.Remove(id);

			var formation = AirFormationUnits();
			if (formation.Count == 0)
			{
				var replacements = Units.Where(a => !AirUnitsRepairing.Contains(a.ActorID))
					.OrderBy(a => hasAirFormationCenter ?
						(a.CenterPosition - airLastFormationCenter).LengthSquared : 0)
					.ThenBy(a => a.ActorID).ToList();
				if (replacements.Count > 0)
				{
					var restored = IsTargetValid ? replacements.Take(1) : replacements;
					var restoredCount = 0;
					foreach (var replacement in restored)
					{
						JoinAirFormation(replacement);
						restoredCount++;
					}

					if (SquadManager.Info.AirTargetDebugLogging)
						Log.Write("debug", "Air formation [{0}] restored core with {1} aircraft; target={2} remaining-reinforcements={3}.",
							AirProfile, restoredCount, IsTargetValid ?
								TargetActor.Info.Name + "#" + TargetActor.ActorID : "none",
							AirReinforcements.Count);
				}
			}
		}

		internal string AirProfile => Type == SquadType.Stealth ? StealthProfile :
			AirSquadDefinition != null &&
			SquadManager.Info.AirSquadDefinitions.TryGetValue(AirSquadDefinition, out var definition) ?
			definition.Profile : "Generic";

		internal string StealthProfile => StealthSquadDefinition ?? "stealth-tank";

		internal StealthSquadDefinition StealthDefinition => StealthSquadDefinition != null &&
			SquadManager.Info.StealthSquadDefinitions.TryGetValue(StealthSquadDefinition, out var definition) ?
			definition : null;

		internal List<Actor> GroundFormationUnits(bool bootstrapIfEmpty = false)
		{
			var formation = Units.Where(a => !GroundReinforcements.Contains(a.ActorID) &&
				!SquadManager.IsUnitProtectingBase(a) && !SquadManager.IsUnitTemporarilyControlled(a)).ToList();
			if (formation.Count == 0 && bootstrapIfEmpty)
			{
				var replacement = Units.Where(a => !SquadManager.IsUnitProtectingBase(a) &&
					!SquadManager.IsUnitTemporarilyControlled(a))
					.OrderBy(a => hasGroundFormationCenter ?
					(a.CenterPosition - groundLastFormationCenter).LengthSquared : (long)a.ActorID)
					.ThenBy(a => a.ActorID).FirstOrDefault();
				if (replacement != null)
				{
					GroundReinforcements.Remove(replacement.ActorID);
					formation.Add(replacement);
				}
			}

			return formation;
		}

		internal WPos GroundFormationCenter
		{
			get
			{
				var formation = GroundFormationUnits(bootstrapIfEmpty: true);
				if (formation.Count > 0)
				{
					groundLastFormationCenter = formation.Select(a => a.CenterPosition).Average();
					hasGroundFormationCenter = true;
				}

				return groundLastFormationCenter;
			}
		}

		internal void MarkGroundReinforcement(Actor actor)
		{
			if (actor != null)
				GroundReinforcements.Add(actor.ActorID);
		}

		internal void CleanGroundMembership()
		{
			var live = new HashSet<uint>(Units.Select(a => a.ActorID));
			GroundReinforcements.RemoveWhere(id => !live.Contains(id));
			GroundFormationUnits(bootstrapIfEmpty: true);
		}

		internal bool ShouldHoldForGroundReinforcements()
		{
			var formationCount = GroundFormationUnits(bootstrapIfEmpty: true).Count;
			var reinforcementCount = Units.Count(a => GroundReinforcements.Contains(a.ActorID) &&
				StrategicGroundScoring.CanOrderGroundReinforcement(
					SquadManager.IsUnitProtectingBase(a), SquadManager.IsUnitTemporarilyControlled(a)));
			var minimum = SquadManager.Info.GroundReinforcementHoldMinimum > 0 ?
				SquadManager.Info.GroundReinforcementHoldMinimum : System.Math.Max(2, SquadManager.Info.SquadSize / 2);
			var hold = StrategicGroundScoring.ShouldHoldForReinforcements(formationCount,
				reinforcementCount, minimum, SquadManager.Info.GroundReinforcementHoldRatioPercent);

			if (hold != groundHoldingForReinforcements && SquadManager.Info.GroundTargetDebugLogging)
				Log.Write("debug", "Ground formation [{0}] regroup {1}: formation={2} reinforcements={3} minimum={4} ratio={5}%.",
					Bot.Player.PlayerName, hold ? "holding" : "resuming", formationCount,
					reinforcementCount, minimum, SquadManager.Info.GroundReinforcementHoldRatioPercent);

			groundHoldingForReinforcements = hold;
			return hold;
		}

		void UpdateGroundReinforcements()
		{
			CleanGroundMembership();
			var formation = GroundFormationUnits(bootstrapIfEmpty: true);
			if (formation.Count == 0 || GroundReinforcements.Count == 0)
				return;

			var center = GroundFormationCenter;
			var destination = World.Map.CellContaining(center);
			var joinDistance = WDist.FromCells(SquadManager.Info.GroundReinforcementJoinRadius).Length;
			var joinDistanceSquared = (long)joinDistance * joinDistance;
			foreach (var reinforcement in Units.Where(a => GroundReinforcements.Contains(a.ActorID) &&
				StrategicGroundScoring.CanOrderGroundReinforcement(
					SquadManager.IsUnitProtectingBase(a), SquadManager.IsUnitTemporarilyControlled(a)))
				.OrderBy(a => a.ActorID).ToList())
			{
				if ((reinforcement.CenterPosition - center).LengthSquared <= joinDistanceSquared)
				{
					GroundReinforcements.Remove(reinforcement.ActorID);
					if (SquadManager.Info.GroundTargetDebugLogging)
						Log.Write("debug", "Ground formation [{0}] reinforcement {1}#{2} joined: formation={3} remaining={4}.",
							Bot.Player.PlayerName, reinforcement.Info.Name, reinforcement.ActorID,
							GroundFormationUnits().Count, GroundReinforcements.Count);

					continue;
				}

				Bot.QueueOrder(new Order("AttackMove", reinforcement, Target.FromCell(World, destination), false));
			}
		}

		public MiniYaml Serialize()
		{
			var nodes = new MiniYaml("", new List<MiniYamlNode>()
			{
				new MiniYamlNode("Type", FieldSaver.FormatValue(Type)),
				new MiniYamlNode("Units", FieldSaver.FormatValue(Units.Select(a => a.ActorID).ToArray())),
			});

			if (Target.Type == TargetType.Actor)
				nodes.Nodes.Add(new MiniYamlNode("Target", FieldSaver.FormatValue(Target.Actor.ActorID)));

			if (AirSquadDefinition != null)
				nodes.Nodes.Add(new MiniYamlNode("AirSquadDefinition", AirSquadDefinition));

			if (StealthSquadDefinition != null)
			{
				nodes.Nodes.Add(new MiniYamlNode("StealthSquadDefinition", StealthSquadDefinition));
				nodes.Nodes.Add(new MiniYamlNode("StealthSquadIndex",
					FieldSaver.FormatValue(StealthSquadIndex)));
				if (UsesModularStealthLifecycle)
				{
					if (stealthLifecycleRuntime == null)
						stealthLifecycleRuntime = new StealthSquadLifecycleRuntimeHost(this);
				}
				else
				{
					if (StealthKillCadenceGeneration != null)
					{
						var generation = StealthKillCadenceGeneration;
						nodes.Nodes.Add(new MiniYamlNode("StealthCadenceGenerationId", FieldSaver.FormatValue(generation.GenerationId)));
						nodes.Nodes.Add(new MiniYamlNode("StealthCadenceGenerationStartTick", FieldSaver.FormatValue(generation.GenerationStartTick)));
						nodes.Nodes.Add(new MiniYamlNode("StealthCadenceWindowStartTick", FieldSaver.FormatValue(generation.WindowStartTick)));
						nodes.Nodes.Add(new MiniYamlNode("StealthCadenceLastObservedTick", FieldSaver.FormatValue(generation.LastObservedTick)));
						nodes.Nodes.Add(new MiniYamlNode("StealthCadenceAge", FieldSaver.FormatValue(generation.CadenceAge)));
						nodes.Nodes.Add(new MiniYamlNode("StealthCadenceAttributedKills", FieldSaver.FormatValue(generation.AttributedKills)));
						nodes.Nodes.Add(new MiniYamlNode("StealthCadenceFailed", FieldSaver.FormatValue(generation.CadenceFailed)));
						nodes.Nodes.Add(new MiniYamlNode("StealthCadenceMismatchFailed", FieldSaver.FormatValue(generation.MismatchFailed)));
					}

					SerializeStealthEscapeState(this, nodes);
				}
			}

			if (AirUnitsRepairing.Count > 0)
				nodes.Nodes.Add(new MiniYamlNode("AirUnitsRepairing",
					FieldSaver.FormatValue(AirUnitsRepairing.OrderBy(id => id).ToArray())));

			if (AirReinforcements.Count > 0)
				nodes.Nodes.Add(new MiniYamlNode("AirReinforcements",
					FieldSaver.FormatValue(AirReinforcements.OrderBy(id => id).ToArray())));

			if (hasAirFormationCenter)
				nodes.Nodes.Add(new MiniYamlNode("AirFormationCenter", FieldSaver.FormatValue(airLastFormationCenter)));

			if (GroundReinforcements.Count > 0)
				nodes.Nodes.Add(new MiniYamlNode("GroundReinforcements",
					FieldSaver.FormatValue(GroundReinforcements.OrderBy(id => id).ToArray())));

			if (hasGroundFormationCenter)
				nodes.Nodes.Add(new MiniYamlNode("GroundFormationCenter", FieldSaver.FormatValue(groundLastFormationCenter)));

			return nodes;
		}

		static void SerializeStealthEscapeState(Squad squad, MiniYaml yaml)
		{
			yaml.Nodes.Add(new MiniYamlNode("AirEscapingLocalAa", FieldSaver.FormatValue(squad.AirEscapingLocalAa)));
			yaml.Nodes.Add(new MiniYamlNode("StealthEscapeIssuedTick", FieldSaver.FormatValue(squad.StealthEscapeIssuedTick)));
			yaml.Nodes.Add(new MiniYamlNode("StealthEscapeSafetyChecks", FieldSaver.FormatValue(squad.StealthEscapeSafetyChecks)));
			yaml.Nodes.Add(new MiniYamlNode("StealthEscapeDestination", FieldSaver.FormatValue(squad.StealthEscapeDestination)));
			yaml.Nodes.Add(new MiniYamlNode("StealthEscapeStartCell", FieldSaver.FormatValue(squad.StealthEscapeStartCell)));
			yaml.Nodes.Add(new MiniYamlNode("StealthEscapeDestinationCell", FieldSaver.FormatValue(squad.StealthEscapeDestinationCell)));
			yaml.Nodes.Add(new MiniYamlNode("StealthEscapePendingExplosion", FieldSaver.FormatValue(squad.StealthEscapePendingExplosion)));
			yaml.Nodes.Add(new MiniYamlNode("StealthEscapeLastProgressTick", FieldSaver.FormatValue(squad.StealthEscapeLastProgressTick)));
			yaml.Nodes.Add(new MiniYamlNode("StealthEscapeLastDistanceCells", FieldSaver.FormatValue(squad.StealthEscapeLastDistanceCells)));
			yaml.Nodes.Add(new MiniYamlNode("StealthEscapePreserveEngagement", FieldSaver.FormatValue(squad.StealthEscapePreserveEngagement)));
			yaml.Nodes.Add(new MiniYamlNode("AirTargetStrategicCell", FieldSaver.FormatValue(squad.AirTargetStrategicCell)));
		}

		static void DeserializeStealthEscapeState(Squad squad, MiniYaml yaml)
		{
			var escapeNode = yaml.Nodes.FirstOrDefault(n => n.Key == "AirEscapingLocalAa");
			if (escapeNode != null)
			{
				T LoadEscape<T>(string key) => FieldLoader.GetValue<T>(key,
					yaml.Nodes.First(n => n.Key == key).Value.Value);
				squad.AirEscapingLocalAa = FieldLoader.GetValue<bool>("AirEscapingLocalAa", escapeNode.Value.Value);
				squad.StealthEscapeIssuedTick = LoadEscape<int>("StealthEscapeIssuedTick");
				squad.StealthEscapeSafetyChecks = LoadEscape<int>("StealthEscapeSafetyChecks");
				squad.StealthEscapeDestination = LoadEscape<CPos?>("StealthEscapeDestination");
				squad.StealthEscapeStartCell = LoadEscape<CPos?>("StealthEscapeStartCell");
				squad.StealthEscapeDestinationCell = LoadEscape<CPos?>("StealthEscapeDestinationCell");
				squad.StealthEscapePendingExplosion = LoadEscape<bool>("StealthEscapePendingExplosion");
				squad.StealthEscapeLastProgressTick = LoadEscape<int>("StealthEscapeLastProgressTick");
				squad.StealthEscapeLastDistanceCells = LoadEscape<int>("StealthEscapeLastDistanceCells");
				squad.StealthEscapePreserveEngagement = LoadEscape<bool>("StealthEscapePreserveEngagement");
				squad.AirTargetStrategicCell = LoadEscape<CPos?>("AirTargetStrategicCell");
				squad.StealthEscapeNeedsActivityRestore = false;
			}
			else
				squad.StealthEscapeNeedsActivityRestore = true;
		}

		public static Squad Deserialize(IBot bot, SquadManagerBotModule squadManager, MiniYaml yaml)
		{
			var type = SquadType.Rush;
			Actor targetActor = null;

			var typeNode = yaml.Nodes.FirstOrDefault(n => n.Key == "Type");
			if (typeNode != null)
				type = FieldLoader.GetValue<SquadType>("Type", typeNode.Value.Value);
			StealthSquadLifecycleAuthorityPersistence.Validate(yaml,
				type == SquadType.Stealth && squadManager.Info.UseModularStealthLifecycle);

			var targetNode = yaml.Nodes.FirstOrDefault(n => n.Key == "Target");
			if (targetNode != null)
				targetActor = squadManager.World.GetActorById(FieldLoader.GetValue<uint>("ActiveUnits", targetNode.Value.Value));

			var squad = new Squad(bot, squadManager, type, targetActor);
			var definitionNode = yaml.Nodes.FirstOrDefault(n => n.Key == "AirSquadDefinition");
			if (definitionNode != null)
				squad.AirSquadDefinition = definitionNode.Value.Value;

			var stealthDefinitionNode = yaml.Nodes.FirstOrDefault(n => n.Key == "StealthSquadDefinition");
			if (stealthDefinitionNode != null)
			{
				squad.StealthSquadDefinition = stealthDefinitionNode.Value.Value;
				var stealthIndexNode = yaml.Nodes.FirstOrDefault(n => n.Key == "StealthSquadIndex");
				if (stealthIndexNode != null)
					squad.StealthSquadIndex = FieldLoader.GetValue<int>(
						"StealthSquadIndex", stealthIndexNode.Value.Value);

				var generationNode = yaml.Nodes.FirstOrDefault(n => n.Key == "StealthCadenceGenerationId");
				if (generationNode != null)
				{
					T Load<T>(string key) => FieldLoader.GetValue<T>(key,
						yaml.Nodes.First(n => n.Key == key).Value.Value);
					squad.StealthKillCadenceGeneration = StealthKillCadenceGeneration.Restore(
						Load<int>("StealthCadenceGenerationId"),
						Load<int>("StealthCadenceGenerationStartTick"),
						Load<int>("StealthCadenceWindowStartTick"),
						Load<int>("StealthCadenceLastObservedTick"),
						Load<int>("StealthCadenceAge"),
						Load<int>("StealthCadenceAttributedKills"),
						Load<bool>("StealthCadenceFailed"),
						Load<bool>("StealthCadenceMismatchFailed"));
				}

				DeserializeStealthEscapeState(squad, yaml);
			}

			var unitsNode = yaml.Nodes.FirstOrDefault(n => n.Key == "Units");
			if (unitsNode != null)
				squad.Units.AddRange(FieldLoader.GetValue<uint[]>("Units", unitsNode.Value.Value)
					.Select(a => squadManager.World.GetActorById(a)));

			var repairingNode = yaml.Nodes.FirstOrDefault(n => n.Key == "AirUnitsRepairing");
			if (repairingNode != null)
				squad.AirUnitsRepairing.UnionWith(
					FieldLoader.GetValue<uint[]>("AirUnitsRepairing", repairingNode.Value.Value));

			var reinforcementsNode = yaml.Nodes.FirstOrDefault(n => n.Key == "AirReinforcements");
			if (reinforcementsNode != null)
				squad.AirReinforcements.UnionWith(
					FieldLoader.GetValue<uint[]>("AirReinforcements", reinforcementsNode.Value.Value));

			var formationCenterNode = yaml.Nodes.FirstOrDefault(n => n.Key == "AirFormationCenter");
			if (formationCenterNode != null)
			{
				squad.airLastFormationCenter =
					FieldLoader.GetValue<WPos>("AirFormationCenter", formationCenterNode.Value.Value);
				squad.hasAirFormationCenter = true;
			}

			var groundReinforcementsNode = yaml.Nodes.FirstOrDefault(n => n.Key == "GroundReinforcements");
			if (groundReinforcementsNode != null)
				squad.GroundReinforcements.UnionWith(
					FieldLoader.GetValue<uint[]>("GroundReinforcements", groundReinforcementsNode.Value.Value));

			var groundFormationCenterNode = yaml.Nodes.FirstOrDefault(n => n.Key == "GroundFormationCenter");
			if (groundFormationCenterNode != null)
			{
				squad.groundLastFormationCenter =
					FieldLoader.GetValue<WPos>("GroundFormationCenter", groundFormationCenterNode.Value.Value);
				squad.hasGroundFormationCenter = true;
			}

			squad.CleanAirMembership();
			if (squad.UsesModularStealthLifecycle)
				squad.stealthLifecycleRuntime = StealthSquadLifecycleRuntimeHost.ForLoadedSquad(squad);

			if (squad.Type == SquadType.GeneralAttack)
				squad.CleanGroundMembership();

			return squad;
		}
	}
}
