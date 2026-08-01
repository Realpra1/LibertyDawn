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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	public enum SquadType { Assault, Air, Rush, Protection, Naval }

	public class Squad
	{
		public List<Actor> Units = new List<Actor>();
		public SquadType Type;
		public string AirSquadDefinition;

		internal IBot Bot;
		internal World World;
		internal SquadManagerBotModule SquadManager;
		internal MersenneTwister Random;

		internal Target Target;
		internal StateMachine FuzzyStateMachine;

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
		internal readonly HashSet<uint> AirRepairUnavailable = new HashSet<uint>();
		internal readonly HashSet<uint> AirReinforcements = new HashSet<uint>();
		internal readonly Dictionary<uint, uint> AirReinforcementTargets = new Dictionary<uint, uint>();
		WPos airLastFormationCenter;
		bool hasAirFormationCenter;
		internal CPos? AirTargetStrategicCell;
		internal int AirTargetLastProgressTick;
		internal int AirTargetLastDistanceCells = int.MaxValue;
		internal int AirTargetLastHP = int.MaxValue;
		internal int AirTargetScore = int.MinValue;
		internal bool AirTargetIsUndefended;
		internal float AirLocalThreatWeight;
		internal int AirNextTargetReviewTick;

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
					FuzzyStateMachine.ChangeState(this, new GroundUnitsIdleState(), true);
					break;
				case SquadType.Air:
					FuzzyStateMachine.ChangeState(this, new AirIdleState(), true);
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
				FuzzyStateMachine.Update(this);
		}

		/// <summary>
		/// Short-interval anti-air awareness for air squads, run independently of the squad state
		/// machine so danger is noticed on approach, mid-attack and on the way home alike.
		/// </summary>
		public void TickAirSafety()
		{
			if (IsValid && Type == SquadType.Air)
				AirStateBase.TickAirSafety(this);
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
		}

		internal void MarkAirRepairing(Actor actor, Actor destination = null)
		{
			if (!hasAirFormationCenter && !AirReinforcements.Contains(actor.ActorID))
			{
				airLastFormationCenter = actor.CenterPosition;
				hasAirFormationCenter = true;
			}

			AirUnitsRepairing.Add(actor.ActorID);
			AirRepairUnavailable.Remove(actor.ActorID);
			if (destination == null)
				AirRepairTargets.Remove(actor.ActorID);
			else
				AirRepairTargets[actor.ActorID] = destination.ActorID;

			MarkAirReinforcement(actor);
		}

		internal void JoinAirFormation(Actor actor)
		{
			AirReinforcements.Remove(actor.ActorID);
			AirReinforcementTargets.Remove(actor.ActorID);
			airLastFormationCenter = actor.CenterPosition;
			hasAirFormationCenter = true;
		}

		internal void CleanAirMembership()
		{
			var live = new HashSet<uint>(Units.Select(a => a.ActorID));
			AirUnitsRepairing.RemoveWhere(id => !live.Contains(id));
			AirRepairUnavailable.RemoveWhere(id => !live.Contains(id));
			foreach (var id in AirRepairTargets.Keys.Where(id => !live.Contains(id)).ToList())
				AirRepairTargets.Remove(id);

			AirReinforcements.RemoveWhere(id => !live.Contains(id));
			foreach (var id in AirReinforcementTargets.Keys.Where(id => !live.Contains(id)).ToList())
				AirReinforcementTargets.Remove(id);

			if (Units.Count == 1 && !AirUnitsRepairing.Contains(Units[0].ActorID))
				JoinAirFormation(Units[0]);
		}

		internal string AirProfile => AirSquadDefinition != null &&
			SquadManager.Info.AirSquadDefinitions.TryGetValue(AirSquadDefinition, out var definition) ?
			definition.Profile : "Generic";

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

			if (AirUnitsRepairing.Count > 0)
				nodes.Nodes.Add(new MiniYamlNode("AirUnitsRepairing",
					FieldSaver.FormatValue(AirUnitsRepairing.OrderBy(id => id).ToArray())));

			if (AirReinforcements.Count > 0)
				nodes.Nodes.Add(new MiniYamlNode("AirReinforcements",
					FieldSaver.FormatValue(AirReinforcements.OrderBy(id => id).ToArray())));

			if (hasAirFormationCenter)
				nodes.Nodes.Add(new MiniYamlNode("AirFormationCenter", FieldSaver.FormatValue(airLastFormationCenter)));

			return nodes;
		}

		public static Squad Deserialize(IBot bot, SquadManagerBotModule squadManager, MiniYaml yaml)
		{
			var type = SquadType.Rush;
			Actor targetActor = null;

			var typeNode = yaml.Nodes.FirstOrDefault(n => n.Key == "Type");
			if (typeNode != null)
				type = FieldLoader.GetValue<SquadType>("Type", typeNode.Value.Value);

			var targetNode = yaml.Nodes.FirstOrDefault(n => n.Key == "Target");
			if (targetNode != null)
				targetActor = squadManager.World.GetActorById(FieldLoader.GetValue<uint>("ActiveUnits", targetNode.Value.Value));

			var squad = new Squad(bot, squadManager, type, targetActor);
			var definitionNode = yaml.Nodes.FirstOrDefault(n => n.Key == "AirSquadDefinition");
			if (definitionNode != null)
				squad.AirSquadDefinition = definitionNode.Value.Value;

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

			squad.CleanAirMembership();

			return squad;
		}
	}
}
