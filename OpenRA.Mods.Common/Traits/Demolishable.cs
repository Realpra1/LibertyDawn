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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Handle demolitions from C4 explosives.")]
	public class DemolishableInfo : ConditionalTraitInfo, IDemolishableInfo
	{
		public bool IsValidTarget(ActorInfo actorInfo, Actor saboteur) { return true; }

		[GrantedConditionReference]
		[Desc("Condition to grant during demolition countdown.")]
		public readonly string Condition = null;

		public override object Create(ActorInitializer init) { return new Demolishable(this); }
	}

	public class Demolishable : ConditionalTrait<DemolishableInfo>, IDemolishable, IAdaptiveKillValue, ITick
	{
		class DemolishAction
		{
			public readonly Actor Saboteur;
			public readonly int Token;
			public int Delay;
			public readonly BitSet<DamageType> DamageTypes;
			public readonly DemolitionSafety Safety;

			public DemolishAction(Actor saboteur, int delay, int token, BitSet<DamageType> damageTypes,
				DemolitionSafety safety)
			{
				Saboteur = saboteur;
				Delay = delay;
				Token = token;
				DamageTypes = damageTypes;
				Safety = safety;
			}
		}

		readonly List<DemolishAction> actions = new List<DemolishAction>();
		readonly List<DemolishAction> removeActions = new List<DemolishAction>();
		DemolishAction resolvingAction;

		public Demolishable(DemolishableInfo info)
			: base(info) { }

		public bool HasPendingAutonomousDemolition(Actor saboteur)
		{
			return actions.Any(a => a.Saboteur == saboteur && a.Safety != null);
		}

		bool IDemolishable.IsValidTarget(Actor self, Actor saboteur)
		{
			return !IsTraitDisabled;
		}

		int? IAdaptiveKillValue.GetAdaptiveKillValue(Actor self, Actor attacker)
		{
			return resolvingAction?.Saboteur == attacker && self.Info.HasTraitInfo<BuildingInfo>() ?
				self.GetSellValue() : (int?)null;
		}

		void IDemolishable.Demolish(Actor self, Actor saboteur, int delay, BitSet<DamageType> damageTypes,
			DemolitionSafety safety)
		{
			if (IsTraitDisabled)
				return;

			var token = self.GrantCondition(Info.Condition);
			actions.Add(new DemolishAction(saboteur, delay, token, damageTypes, safety));
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled)
				return;

			foreach (var a in actions)
			{
				if (a.Safety != null && !a.Safety.IsValid(self, a.Saboteur))
				{
					DemolitionDebug.Write("AI autonomous C4 disarmed at tick {0}: {1}#{2}, target-owner={3}, " +
						"relationship={4}", self.World.WorldTick, self.Info.Name, self.ActorID,
						self.Owner.InternalName, a.Saboteur.Owner.RelationshipWith(self.Owner));
					if (a.Token != Actor.InvalidConditionToken)
						self.RevokeCondition(a.Token);

					removeActions.Add(a);
					continue;
				}

				if (a.Delay-- <= 0)
				{
					var modifiers = self.TraitsImplementing<IDamageModifier>()
						.Concat(self.Owner.PlayerActor.TraitsImplementing<IDamageModifier>())
						.Select(t => t.GetDamageModifier(self, null));

					if (Util.ApplyPercentageModifiers(100, modifiers) > 0)
					{
						if (a.Safety != null)
							DemolitionDebug.Write("AI autonomous C4 final action at tick {0}: {1}#{2}, target-owner={3}, " +
								"relationship={4}", self.World.WorldTick, self.Info.Name, self.ActorID,
								self.Owner.InternalName, a.Saboteur.Owner.RelationshipWith(self.Owner));

						resolvingAction = a;
						try
						{
							self.Kill(a.Saboteur, a.DamageTypes);
						}
						finally
						{
							resolvingAction = null;
						}
					}
					else if (a.Token != Actor.InvalidConditionToken)
					{
						self.RevokeCondition(a.Token);
						removeActions.Add(a);
					}
				}
			}

			// Remove expired actions to avoid double-revoking
			foreach (var a in removeActions)
				actions.Remove(a);

			removeActions.Clear();
		}
	}
}
