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
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	static class StealthKitePlanBuilder
	{
		public static StealthKitePlan Build(StealthKiteLiveDecision decision,
			StealthKiteActorSnapshot target, StealthKiteLiveFingerprint fingerprint,
			Func<StealthKiteThreatFacts, StealthKiteSafetyResult> calculate)
		{
			if (decision == null || target == null || fingerprint == null || calculate == null)
				throw new ArgumentNullException();
			foreach (var fireCell in decision.OrderedFireCells(target))
			{
				var fireFacts = decision.ThreatFacts(StealthKiteAction.Fire, target, fireCell);
				var fireSafety = calculate(fireFacts);
				if (!fireSafety.Approved)
					continue;
				foreach (var withdrawCell in decision.OrderedWithdrawCells(target, fireCell))
				{
					var withdrawFacts = decision.ThreatFacts(
						StealthKiteAction.Withdraw, target, withdrawCell);
					var withdrawSafety = calculate(withdrawFacts);
					if (withdrawSafety.Approved)
						return new StealthKitePlan(fingerprint, fireCell, withdrawCell,
							fireFacts, fireSafety, withdrawFacts, withdrawSafety);
				}
			}

			return null;
		}

		public static StealthKiteFallbackEvidence NoSafePlan(StealthKiteLiveDecision decision,
			StealthKiteActorSnapshot target, StealthKiteLiveFingerprint fingerprint,
			Func<StealthKiteFallbackFacts, StealthTargetThreatScore> calculate)
		{
			var facts = decision.FallbackFacts(target);
			var score = calculate(facts);
			return new StealthKiteFallbackEvidence(StealthKiteFallbackReason.NoSafePlan,
				fingerprint.Canonical, decision.DefenderActorIds, facts, score);
		}

		public static StealthKiteFallbackEvidence NoLiveMembers(
			StealthKiteLiveDecision decision, StealthKiteLiveFingerprint fingerprint)
		{
			return new StealthKiteFallbackEvidence(StealthKiteFallbackReason.NoLiveMembers,
				fingerprint.Canonical, decision.DefenderActorIds, null, null);
		}

		public static bool SameFacts(StealthKiteThreatFacts left, StealthKiteThreatFacts right)
		{
			return left != null && right != null && left.Action == right.Action &&
				left.SelectedTargetActorId == right.SelectedTargetActorId &&
				left.SelectedTargetCurrentCell == right.SelectedTargetCurrentCell &&
				left.PlannedCell == right.PlannedCell &&
				left.FriendlyCurrentFiringRangeCells == right.FriendlyCurrentFiringRangeCells &&
				left.FriendlyActorIds.SequenceEqual(right.FriendlyActorIds) &&
				left.EnemyActorIds.SequenceEqual(right.EnemyActorIds) &&
				left.Enemies.Zip(right.Enemies, SameEnemy).All(equal => equal) &&
				left.FormationCloaked == right.FormationCloaked &&
				left.PlannedDecloak == right.PlannedDecloak && left.PlannedAttack == right.PlannedAttack;
		}

		public static bool SameSafety(StealthKiteSafetyResult left, StealthKiteSafetyResult right)
		{
			return left.Score.ThreatRating.Equals(right.Score.ThreatRating) &&
				left.Score.Crossover.Equals(right.Score.Crossover) && left.Approved == right.Approved;
		}

		static bool SameEnemy(StealthKiteActorSnapshot left, StealthKiteActorSnapshot right)
		{
			return left.ActorId == right.ActorId && left.ActorType == right.ActorType &&
				left.CurrentCell == right.CurrentCell && left.HitPoints == right.HitPoints &&
				left.MaximumHitPoints == right.MaximumHitPoints &&
				left.CurrentWeaponRangeCells == right.CurrentWeaponRangeCells &&
				left.IsDefender == right.IsDefender &&
				left.IsMissionObjective == right.IsMissionObjective &&
				left.IsInfantry == right.IsInfantry &&
				left.CanBeCrushedByFormation == right.CanBeCrushedByFormation &&
				left.HasDetectorCoverage == right.HasDetectorCoverage &&
				left.IsInLocalEngagementArea == right.IsInLocalEngagementArea;
		}
	}
}
