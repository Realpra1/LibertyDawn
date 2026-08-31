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

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	class StealthAIIdleState : StealthAIStateBase, IState
	{
		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			if (owner.StealthProfile == "stealth-tank")
			{
				foreach (var unit in owner.Units)
					SendHomeToRepair(owner, unit);
				PromoteArrivedAirReinforcements(owner);
				RoutePendingStealthReinforcements(owner);
			}

			if (owner.AirEscapingLocalAa)
			{
				if (owner.Type != SquadType.Stealth || AdvanceStealthEscape(owner))
					return;
			}

			if (owner.SquadManager.Info.AirTargetDebugLogging)
				Log.Write("debug", "Air state [{0}] idle tick: units={1} no-target-scans={2}.",
					owner.AirProfile, owner.Units.Count, owner.AirConsecutiveNoTargetScans);

			// The continuous safety check watches the squad's surroundings on its own, much shorter
			// interval, so this scan is pure duplicated work whenever that is switched on.
			if (owner.SquadManager.Info.AirSafetyCheckInterval <= 0 && ShouldFlee(owner))
			{
				owner.FuzzyStateMachine.ChangeState(owner, new StealthAIFleeState(), true);
				return;
			}

			var e = FindDefenselessTarget(owner);
			if (e == null)
			{
				QueueStealthReinforcementsToFormation(owner);

				// Given up waiting for a positive score: accept the best finite-cost route instead of idling
				// forever. Threat costs remain intact, and squad size already scales acceptable risk.
				var threshold = owner.SquadManager.Info.AirMassedAttackIdleThreshold;
				if (threshold > 0)
				{
					owner.AirConsecutiveNoTargetScans++;
					if (owner.AirConsecutiveNoTargetScans > threshold)
					{
						var massedTarget = FindBestAirTarget(owner, relaxed: true);
						if (massedTarget != null)
						{
							owner.AirConsecutiveNoTargetScans = 0;
							ApplyAirTargetPlan(owner, massedTarget);
							owner.FuzzyStateMachine.ChangeState(owner, new StealthAIAttackState(), true);
							return;
						}
					}
				}

				// Nothing worth hitting from where we are standing. If the squad remembers anti-air it is
				// loitering next to an enemy base, so shuffle to a nearby point and try the scan again from
				// there instead of hovering: this is the "if it cannot get there in a straight line, move
				// around the base and try again" half of the loop, done the cheap way.
				if (owner.Type == SquadType.Stealth)
				{
					if (owner.StealthProfile == "stealth-tank")
						BeginStealthEnemyApproach(owner);
					else
						BeginStealthSafetyReposition(owner);
				}
				else if (owner.SquadManager.Info.AirEvadeDistance > 0 && owner.AirThreatPositions.Count > 0)
					Evade(owner, "no eligible target near remembered AA");

				return;
			}

			owner.AirConsecutiveNoTargetScans = 0;
			ApplyAirTargetPlan(owner, e);
			owner.FuzzyStateMachine.ChangeState(owner, new StealthAIAttackState(), true);
		}

		public void Deactivate(Squad owner) { }
	}
}
