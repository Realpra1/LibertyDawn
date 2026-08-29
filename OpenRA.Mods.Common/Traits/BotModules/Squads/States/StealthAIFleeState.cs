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

using System.Linq;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	class StealthAIFleeState : StealthAIStateBase, IState
	{
		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			// A loaded legacy state cannot turn a specialist plan into a retreat order.
			if (owner.Type == SquadType.Stealth)
			{
				owner.FuzzyStateMachine.ChangeState(owner, new StealthAIIdleState(), true);
				return;
			}

			if (owner.AirEscapingLocalAa)
			{
				if (owner.Units.Any(a => !owner.AirUnitsRepairing.Contains(a.ActorID) && !a.IsIdle))
					return;

				owner.AirEscapingLocalAa = false;
				owner.FuzzyStateMachine.ChangeState(owner, new StealthAIIdleState(), true);
				return;
			}

			Evade(owner, "flee-state continuation");

			// Straight back to idle: the next scan - whichever of the state machine or the much faster
			// safety check gets there first - re-targets from wherever the hop put us.
			owner.FuzzyStateMachine.ChangeState(owner, new StealthAIIdleState(), true);
		}

		public void Deactivate(Squad owner) { }
	}
}
