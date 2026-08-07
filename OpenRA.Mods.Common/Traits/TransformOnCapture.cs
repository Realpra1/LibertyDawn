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

using OpenRA.Mods.Common.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Replaces the captured actor with a new one.")]
	public class TransformOnCaptureInfo : TraitInfo
	{
		[ActorReference]
		[FieldLoader.Require]
		public readonly string IntoActor = null;

		public readonly int ForceHealthPercentage = 0;

		public readonly bool SkipMakeAnims = true;

		[Desc("Transform only if the capturer's CaptureTypes overlap with these types. Leave empty to allow all types.")]
		public readonly BitSet<CaptureType> CaptureTypes = default(BitSet<CaptureType>);

		public override object Create(ActorInitializer init) { return new TransformOnCapture(init, this); }
	}

	public class TransformOnCapture : INotifyCapture, INotifyTransform
	{
		readonly TransformOnCaptureInfo info;
		readonly string faction;
		Player pendingSpecialistPlayer;
		string pendingSpecialistType;
		uint pendingSpecialistId;
		string pendingTargetType;
		uint pendingTargetId;
		string pendingTargetOldOwner;
		int pendingDirectValue;

		public TransformOnCapture(ActorInitializer init, TransformOnCaptureInfo info)
		{
			this.info = info;
			faction = init.GetValue<FactionInit, string>(init.Self.Owner.Faction.InternalName);
		}

		public bool HandlesCaptureTypes(BitSet<CaptureType> captureTypes)
		{
			return info.CaptureTypes.IsEmpty || info.CaptureTypes.Overlaps(captureTypes);
		}

		void INotifyCapture.OnCapture(Actor self, Actor captor, Player oldOwner, Player newOwner, BitSet<CaptureType> captureTypes)
		{
			if (!HandlesCaptureTypes(captureTypes))
				return;

			pendingSpecialistPlayer = captor.Owner;
			pendingSpecialistType = captor.Info.Name;
			pendingSpecialistId = captor.ActorID;
			pendingTargetType = self.Info.Name;
			pendingTargetId = self.ActorID;
			pendingTargetOldOwner = oldOwner.InternalName;
			pendingDirectValue = self.GetSellValue();

			var facing = self.TraitOrDefault<IFacing>();
			var transform = new Transform(info.IntoActor) { ForceHealthPercentage = info.ForceHealthPercentage, Faction = faction };
			if (facing != null) transform.Facing = facing.Facing;
			transform.SkipMakeAnims = info.SkipMakeAnims;
			self.QueueActivity(false, transform);
		}

		void INotifyTransform.BeforeTransform(Actor self) { }

		void INotifyTransform.OnTransform(Actor self) { }

		void INotifyTransform.AfterTransform(Actor toActor)
		{
			if (pendingSpecialistPlayer == null)
				return;

			var specialistPlayer = pendingSpecialistPlayer;
			pendingSpecialistPlayer = null;
			if (toActor.Info.Name != info.IntoActor || toActor.Owner != specialistPlayer ||
				toActor.IsDead || !toActor.IsInWorld)
			{
				if (Game.Settings.Debug.BotDebug)
					Log.Write("debug", "Adaptive specialist outcome warning at tick {0}: " +
						"kind=husk-restoration, target={1}#{2}, expected-replacement={3}, actual-replacement={4}#{5}, " +
						"expected-player={6}, actual-player={7}", toActor.World.WorldTick, pendingTargetType,
						pendingTargetId, info.IntoActor, toActor.Info.Name, toActor.ActorID,
						specialistPlayer.InternalName, toActor.Owner.InternalName);

				return;
			}

			var economicValue = SpecialistAdaptiveEvidence.EconomicValue(true, pendingDirectValue, toActor.GetSellValue());
			var delta = CompletedSpecialistOutcome.Record(specialistPlayer, pendingSpecialistType, economicValue);
			CompletedSpecialistOutcome.WriteLog(toActor.World, "husk-restoration", pendingSpecialistType,
				pendingSpecialistId, specialistPlayer, pendingTargetType, pendingTargetId, pendingTargetOldOwner,
				"replacement-sell-value", toActor.Info.Name, false, delta);
		}
	}
}
