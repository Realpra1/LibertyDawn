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

		// Consecutive AirIdleState scans that found no valid (safe-enough) target. Drives the massed-
		// attack fallback: past SquadManagerBotModuleInfo.AirMassedAttackIdleThreshold, the squad stops
		// waiting for something undefended to show up and instead forces an attack on whatever scores
		// best with the anti-air/route penalties relaxed. Reset to zero the moment a target is found.
		internal int AirConsecutiveNoTargetScans;

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

		public MiniYaml Serialize()
		{
			var nodes = new MiniYaml("", new List<MiniYamlNode>()
			{
				new MiniYamlNode("Type", FieldSaver.FormatValue(Type)),
				new MiniYamlNode("Units", FieldSaver.FormatValue(Units.Select(a => a.ActorID).ToArray())),
			});

			if (Target.Type == TargetType.Actor)
				nodes.Nodes.Add(new MiniYamlNode("Target", FieldSaver.FormatValue(Target.Actor.ActorID)));

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

			var unitsNode = yaml.Nodes.FirstOrDefault(n => n.Key == "Units");
			if (unitsNode != null)
				squad.Units.AddRange(FieldLoader.GetValue<uint[]>("Units", unitsNode.Value.Value)
					.Select(a => squadManager.World.GetActorById(a)));

			return squad;
		}
	}
}
