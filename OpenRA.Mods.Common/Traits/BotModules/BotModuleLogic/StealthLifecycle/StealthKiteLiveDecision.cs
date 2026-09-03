#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>One current-World view for one reactive Kite decision.</summary>
	sealed class StealthKiteLiveDecision
	{
		readonly bool formationCloaked;
		public bool FormationExposed { get; }
		public bool KitingEnabled { get; }
		public StealthKiteMemberSnapshot[] Members { get; }
		public StealthKiteActorSnapshot[] Defenders { get; }
		public StealthKiteActorSnapshot[] Objectives { get; }
		public StealthKiteActorSnapshot[] KiteTargets { get; }
		public StealthKiteActorSnapshot[] FallbackObjectives { get; }
		public StealthKiteActorSnapshot[] CrushableInfantry { get; }
		public CPos[] CandidateCells { get; }
		public int FormationRadiusCells => RadiusFrom(CurrentFormationCell().Value);
		public uint[] DefenderActorIds { get; }
		public uint[] ObjectiveActorIds { get; }
		public CPos[] MemberCells => Members.Select(member => member.CurrentCell).Distinct().ToArray();
		public StealthKiteDisposition? TargetlessDisposition { get; }
		StealthKiteLiveDecision(StealthKiteLiveSnapshot live, uint? requiredKiteActorId)
		{
			formationCloaked = live.FormationCloaked;
			FormationExposed = !live.FormationCloaked || live.FormationDetected;
			KitingEnabled = live.KitingEnabled;
			Members = live.Members.Where(member => member.IsValid)
				.OrderBy(member => member.ActorId).ToArray();
			var actors = live.Actors.Where(actor => actor.IsValid && actor.IsInLocalEngagementArea)
				.OrderBy(actor => actor.ActorId).ToArray();
			Defenders = actors.Where(actor => actor.IsDefender).ToArray();
			Objectives = actors.Where(actor => actor.IsMissionObjective).ToArray();
			CrushableInfantry = actors.Where(actor => actor.IsDefender && actor.IsInfantry &&
				actor.CanBeCrushedByFormation && !actor.HasDetectorCoverage).ToArray();
			KiteTargets = actors.Where(actor =>
				(actor.IsDefender && (!actor.IsInfantry || !actor.CanBeCrushedByFormation ||
					actor.HasDetectorCoverage || actor.ActorId == requiredKiteActorId)) ||
				(actor.IsMissionObjective && actor.PriorityValue >= live.MinimumKitePriorityValue)).ToArray();
			FallbackObjectives = Objectives.Except(KiteTargets).ToArray();
			CandidateCells = live.CandidateCells.ToArray();
			DefenderActorIds = Defenders.Select(actor => actor.ActorId).ToArray();
			ObjectiveActorIds = Objectives.Select(actor => actor.ActorId).ToArray();
			if (Defenders.Length == 0)
				TargetlessDisposition = Objectives.Length == 0 ?
					StealthKiteDisposition.Reacquire : StealthKiteDisposition.UndefendedAttack;
			else if (Members.Length == 0)
				TargetlessDisposition = StealthKiteDisposition.RecalculateFlee;
			else if (KiteTargets.Length == 0 && CrushableInfantry.Length != 0)
				TargetlessDisposition = StealthKiteDisposition.CrushEvaluation;
		}

		public static StealthKiteLiveDecision Create(StealthKiteLiveSnapshot live,
			uint? requiredKiteActorId = null)
		{
			return new StealthKiteLiveDecision(live ?? throw new ArgumentNullException(nameof(live)),
				requiredKiteActorId);
		}

		public StealthKiteActorSnapshot ResolveTarget(uint? retainedActorId)
		{
			if (TargetlessDisposition.HasValue)
				throw new InvalidOperationException("A targetless Kite decision cannot resolve a target.");
			return OrderedTargets(retainedActorId).First();
		}

		public StealthKiteActorSnapshot[] OrderedTargets(uint? retainedActorId)
		{
			var center = CurrentFormationCell().Value;
			var retainedFallback = retainedActorId.HasValue ? FallbackObjectives.FirstOrDefault(
				actor => actor.ActorId == retainedActorId.Value) : null;
			var candidates = retainedFallback == null ? KiteTargets :
				KiteTargets.Append(retainedFallback).ToArray();
			return candidates.OrderBy(actor => actor.ActorId == retainedActorId ? 0 : 1)
				.ThenBy(actor => DistanceSquared(center, actor.CurrentCell))
				.ThenBy(actor => actor.ActorId).ToArray();
		}

		public CPos? CurrentFormationCell()
		{
			return Members.Length == 0 ? (CPos?)null : new CPos(
				(int)Math.Round(Members.Average(member => member.CurrentCell.X)),
				(int)Math.Round(Members.Average(member => member.CurrentCell.Y)));
		}

		public CPos RepresentativeCell(StealthKiteActorSnapshot target)
		{
			if (target == null || Members.Length == 0)
				throw new ArgumentException("A representative Kite cell requires a live target and squad.");
			return Members.OrderBy(member => DistanceSquared(member.CurrentCell, target.CurrentCell))
				.ThenBy(member => member.ActorId).First().CurrentCell;
		}

		public CPos[] OrderedCandidateCells(StealthKiteActorSnapshot target, CPos? currentCell)
		{
			var occupied = MemberCells.ToHashSet();
			var formationCell = currentCell ?? Members[0].CurrentCell;
			return CandidateCells.Where(cell => !occupied.Contains(cell))
				.OrderBy(cell => DistanceSquared(formationCell, cell))
				.ThenByDescending(cell => DistanceSquared(cell, target.CurrentCell))
				.ThenBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
		}

		public StealthKiteActorSnapshot[] OrderedFallbackObjectives()
		{
			var center = CurrentFormationCell().Value;
			return FallbackObjectives.OrderBy(actor => DistanceSquared(center, actor.CurrentCell))
				.ThenByDescending(actor => actor.PriorityValue)
				.ThenBy(actor => actor.ActorId).ToArray();
		}

		public StealthKiteThreatFacts ThreatFacts(StealthKiteActorSnapshot target, CPos cell)
		{
			if (target == null || (!KiteTargets.Contains(target) &&
				!FallbackObjectives.Contains(target)) || Members.Length == 0)
				throw new ArgumentException("Kite safety requires a live target and squad.", nameof(target));
			return new StealthKiteThreatFacts(StealthKiteAction.Fire, target.ActorId,
				target.CurrentCell, cell, Members.Min(member => member.CurrentWeaponRangeCells),
				Members.Select(member => member.ActorId), ThreatActors(target), formationCloaked, true, true,
				FormationRadiusCells);
		}

		public StealthKiteFallbackFacts FallbackFacts(StealthKiteActorSnapshot target)
		{
			return new StealthKiteFallbackFacts(target.ActorId, target.CurrentCell,
				Members.Select(member => member.ActorId), ThreatActors(target).Select(actor => actor.ActorId),
				formationCloaked);
		}

		public string LiveIdentity(StealthKiteActorSnapshot target)
		{
			return string.Join("|", target.ActorId, string.Join(",",
				Members.Select(member => member.ActorId)), string.Join(",", DefenderActorIds));
		}

		StealthKiteActorSnapshot[] ThreatActors(StealthKiteActorSnapshot target)
		{
			return Defenders.Append(target).Distinct().OrderBy(actor => actor.ActorId).ToArray();
		}

		static long DistanceSquared(CPos left, CPos right)
		{
			var dx = (long)left.X - right.X;
			var dy = (long)left.Y - right.Y;
			return dx * dx + dy * dy;
		}

		int RadiusFrom(CPos center)
		{
			return (int)Math.Ceiling(Math.Sqrt(Members.Max(member =>
				DistanceSquared(center, member.CurrentCell))));
		}
	}
}
