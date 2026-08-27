#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Allows an AI-controlled Mammoth to follow a verified crushable actor into its occupied cell.")]
	public sealed class EconomyMammothCrushMoveInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new EconomyMammothCrushMove(); }
	}

	public sealed class EconomyMammothCrushMove : IResolveOrder
	{
		public const string OrderId = "EconomyMammothCrushMove";

		internal bool ShouldIssueOrder(Actor self, Actor target)
		{
			var activeTargetIds = self.CurrentActivity == null ? Enumerable.Empty<uint>() :
				self.CurrentActivity.ActivitiesImplementing<MoveToCrushTarget>()
					.Where(a => !a.IsCanceling).Select(a => a.TargetId);
			return EconomyTroopPolicy.ShouldIssueCrushOrder(target.ActorID, activeTargetIds);
		}

		internal bool IsCurrentOrder(Actor self, Actor target)
		{
			return self.CurrentActivity is MoveToCrushTarget current &&
				!current.IsCanceling && current.TargetId == target.ActorID;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderId)
				return;

			if (order.Target.Type != TargetType.Actor)
				return;

			var target = order.Target.Actor;
			var mobile = self.TraitOrDefault<Mobile>();
			var enemy = self.Owner.RelationshipWith(target.Owner) == PlayerRelationship.Enemy;
			var crushable = mobile != null && target.TraitsImplementing<ICrushable>()
				.Any(c => c.CrushableBy(target, self, mobile.Info.LocomotorInfo.Crushes));
			if (mobile == null || !enemy || !crushable)
				return;

			self.QueueActivity(order.Queued, new MoveToCrushTarget(self, order.Target));
		}

		sealed class MoveToCrushTarget : MoveAdjacentTo
		{
			readonly Actor targetActor;
			readonly uint targetId;

			public uint TargetId => targetId;

			public MoveToCrushTarget(Actor self, in Target target)
				: base(self, target)
			{
				targetActor = target.Actor;
				targetId = target.Actor.ActorID;
			}

			protected override Actor IgnoredActorForMovement(Actor self)
			{
				return targetActor;
			}

			protected override bool ShouldStop(Actor self)
			{
				if (targetActor.IsDead || !targetActor.IsInWorld ||
					self.Owner.RelationshipWith(targetActor.Owner) != PlayerRelationship.Enemy)
					return true;

				return !targetActor.TraitsImplementing<ICrushable>()
					.Any(c => c.CrushableBy(targetActor, self, Mobile.Info.LocomotorInfo.Crushes));
			}

			protected override IEnumerable<CPos> CandidateMovementCells(Actor self)
			{
				yield return lastVisibleTargetLocation;
			}
		}
	}
}
