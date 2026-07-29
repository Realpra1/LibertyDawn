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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Maintains a running count and reports whether it is *exactly* equal to a target value.
	/// Deliberately an equality test, not a threshold: one below and one above are both unsatisfied.
	/// Kept free of engine types so the boundary behaviour can be unit tested in isolation.
	/// </summary>
	public sealed class ExactCountTracker
	{
		public readonly int Target;

		public int Count { get; private set; }

		public bool IsSatisfied => Count == Target;

		public ExactCountTracker(int target)
			: this(target, 0) { }

		public ExactCountTracker(int target, int initialCount)
		{
			Target = target;
			Count = initialCount;
		}

		/// <summary>
		/// Applies a change to the count and returns true if that flipped the satisfied state.
		/// </summary>
		public bool Adjust(int delta)
		{
			if (delta == 0)
				return false;

			var wasSatisfied = IsSatisfied;
			Count += delta;
			return IsSatisfied != wasSatisfied;
		}

		/// <summary>
		/// Overwrites the count outright and returns true if that flipped the satisfied state.
		/// </summary>
		public bool Set(int count)
		{
			var wasSatisfied = IsSatisfied;
			Count = count;
			return IsSatisfied != wasSatisfied;
		}
	}

	[TraitLocation(SystemActors.Player)]
	[Desc("Grants a condition to the player actor while the number of matching actors owned by this",
		"player is *exactly* equal to `Count`. This is an equality test, not a threshold: one fewer",
		"or one more actor revokes the condition again, and returning to the exact number re-grants it.",
		"The count is maintained incrementally from the world's actor added/removed events (which also",
		"cover ownership changes, since those re-add the actor), so no per-tick scan of the world",
		"is ever performed.")]
	public class GrantConditionOnActorCountInfo : TraitInfo
	{
		[FieldLoader.Require]
		[ActorReference]
		[Desc("Actor type names to count. Only actors owned by this player and currently in the world count.")]
		public readonly HashSet<string> ActorTypes = new HashSet<string>();

		[FieldLoader.Require]
		[Desc("The exact number of matching actors that grants the condition.")]
		public readonly int Count = 0;

		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("The condition to grant while the count matches exactly.")]
		public readonly string Condition = null;

		public override object Create(ActorInitializer init) { return new GrantConditionOnActorCount(init.Self, this); }
	}

	public class GrantConditionOnActorCount : INotifyCreated, INotifyActorDisposing, ISync
	{
		readonly GrantConditionOnActorCountInfo info;
		readonly HashSet<string> actorTypes;
		readonly ExactCountTracker tracker;
		readonly World world;

		Actor playerActor;
		Player owner;
		bool subscribed;
		int conditionToken = Actor.InvalidConditionToken;

		[Sync]
		public int Count => tracker.Count;

		public GrantConditionOnActorCount(Actor self, GrantConditionOnActorCountInfo info)
		{
			this.info = info;
			world = self.World;
			tracker = new ExactCountTracker(info.Count);

			// The ruleset lower-cases actor type names, but yaml authors are not obliged to.
			actorTypes = new HashSet<string>(info.ActorTypes, StringComparer.OrdinalIgnoreCase);
		}

		void INotifyCreated.Created(Actor self)
		{
			playerActor = self;
			owner = self.Owner;

			// Player actors are created before the map actors are spawned, so this normally finds
			// nothing. It exists so the trait is still correct if that ordering ever changes.
			// world.Actors is ordered by ActorID, so this is deterministic.
			var initial = 0;
			foreach (var a in world.Actors)
				if (Matches(a))
					initial++;

			tracker.Set(initial);

			world.ActorAdded += ActorAdded;
			world.ActorRemoved += ActorRemoved;
			subscribed = true;

			UpdateCondition();
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (!subscribed)
				return;

			world.ActorAdded -= ActorAdded;
			world.ActorRemoved -= ActorRemoved;
			subscribed = false;
		}

		// PERF: called for every actor entering or leaving the world, once per player.
		// The owner reference check rejects the overwhelming majority before any hashing happens.
		bool Matches(Actor a)
		{
			return a.Owner == owner && actorTypes.Contains(a.Info.Name);
		}

		void ActorAdded(Actor a)
		{
			if (Matches(a) && tracker.Adjust(1))
				UpdateCondition();
		}

		void ActorRemoved(Actor a)
		{
			if (Matches(a) && tracker.Adjust(-1))
				UpdateCondition();
		}

		void UpdateCondition()
		{
			if (tracker.IsSatisfied)
			{
				if (conditionToken == Actor.InvalidConditionToken)
					conditionToken = playerActor.GrantCondition(info.Condition);
			}
			else if (conditionToken != Actor.InvalidConditionToken)
				conditionToken = playerActor.RevokeCondition(conditionToken);
		}
	}
}
