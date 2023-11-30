#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Any actor with this trait enabled will grant a condition to other actors with this trait within range.")]
	public class GrantConditionInRangeInfo : ConditionalTraitInfo
	{
		[Desc("How far away the condition will be granted.")]
		public readonly WDist Range = WDist.FromCells(5);

		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("The condition to grant or receive.")]
		public readonly string Condition = null;

		[Desc("Will grant the condition if this is true. Otherwise only receive.")]
		public readonly bool Granter = false;

		[Desc("Will get the condition if this is true. Otherwise not.")]
		public readonly bool Receiver = true;

		[Desc("Player relationships who can receive the condition.")]
		public readonly PlayerRelationship ValidRelationships = PlayerRelationship.Ally;

		[Desc("Time in ticks to wait between updating (higher is better for performance).")]
		public readonly int Delay = 50;

		public override object Create(ActorInitializer init) { return new GrantConditionInRange(this); }
	}

	public class GrantConditionInRange : ConditionalTrait<GrantConditionInRangeInfo>, INotifyRemovedFromWorld, ITick
	{
		readonly GrantConditionInRangeInfo info;

		int delay;

		/// <summary>
		/// ActorId as key and RevokeToken as value.
		/// String used because I'm bad at C# generics.
		/// </summary>
		readonly FastUniqueQueue<uint, string> actorRevokeTokenMap = new FastUniqueQueue<uint, string>();

		public GrantConditionInRange(GrantConditionInRangeInfo info)
			: base(info)
		{
			this.info = info;
			delay = info.Delay;
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
				RevokeAll(self);
				return;
		}

		void RevokeAll(Actor self)
		{
			if (actorRevokeTokenMap.Size() == 0)
				return;

			foreach (var actorId in actorRevokeTokenMap.Keys())
			{
				var actor = self.World.GetActorById(actorId);
				if (actor != null)
					actor.RevokeCondition(int.Parse(actorRevokeTokenMap.GetEntry(actorId).Value));
			}

			actorRevokeTokenMap.Clear();
		}

		void ITick.Tick(Actor self)
		{
			if (!info.Granter)
				return;

			if (!self.IsInWorld || self.IsDead || IsTraitDisabled)
			{
				RevokeAll(self);
				return;
			}

			if (delay-- > 0)
				return;

			delay = info.Delay;

			int initCount = actorRevokeTokenMap.Size();
			int pinged = 0;

			var actorsInRange = self.World.FindActorsInCircle(self.CenterPosition, info.Range)
				.Where(a => info.ValidRelationships.HasRelationship(a.Owner.RelationshipWith(self.Owner)));

			// Using FastUniqueQueue in this way keeps performance at O(N) instead of O(N^2) despite having to compare two lists.
			foreach (var actor in actorsInRange)
			{
				var grantTraits = actor.TraitsImplementing<GrantConditionInRange>();
				bool abort = true;
				if (grantTraits != null)
				{
					foreach (var grantTrait in grantTraits)
					{
						if (grantTrait.Info.Condition == Info.Condition && grantTrait.Info.Receiver)
						{
							abort = false;
							break;
						}
					}
				}

				if (abort)
					continue;

				if (!actorRevokeTokenMap.ContainsKey(actor.ActorID))
				{
					var revokeToken = actor.GrantCondition(info.Condition);
					actorRevokeTokenMap.PutIfAbsent(actor.ActorID, revokeToken.ToString());
				}
				else
				{
					pinged++;
					actorRevokeTokenMap.AddLast(actor.ActorID, actorRevokeTokenMap.GetEntry(actor.ActorID).Value);
				}
			}

			for (int i = 0; i < initCount - pinged; i++)
			{
				var leftRange = actorRevokeTokenMap.HeadEntry();
				actorRevokeTokenMap.Poll();
				if (leftRange != null)
				{
					var actor = self.World.GetActorById(leftRange.Key);
					if (actor != null)
						actor.RevokeCondition(int.Parse(leftRange.Value));
				}
			}
		}
	}
}
