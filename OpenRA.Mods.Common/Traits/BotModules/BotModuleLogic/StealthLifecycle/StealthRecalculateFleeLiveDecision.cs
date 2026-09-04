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
	sealed class StealthRecalculateFleeLiveDecision
	{
		public StealthRecalculateFleeMemberSnapshot[] Members { get; }
		public StealthRecalculateFleeEnemySnapshot[] Enemies { get; }
		public uint[] MemberActorIds { get; }
		public uint[] EnemyActorIds { get; }
		public string Fingerprint { get; }
		public bool CurrentPositionSafe { get; }
		public bool FormationCloaked { get; }

		StealthRecalculateFleeLiveDecision(StealthRecalculateFleeLiveSnapshot live)
		{
			Members = live.Members.Where(member => member.IsValid)
				.OrderBy(member => member.ActorId).ToArray();
			Enemies = live.Enemies.Where(enemy => enemy.IsValid && enemy.IsInLocalEngagementArea)
				.OrderBy(enemy => enemy.ActorId).ToArray();
			MemberActorIds = Members.Select(member => member.ActorId).ToArray();
			EnemyActorIds = Enemies.Select(enemy => enemy.ActorId).ToArray();
			Fingerprint = StealthRecalculateFleeFingerprint.Create(live);
			CurrentPositionSafe = live.CurrentPositionSafe;
			FormationCloaked = live.FormationCloaked;
		}

		public static StealthRecalculateFleeLiveDecision Create(
			StealthRecalculateFleeLiveSnapshot live)
		{
			return new StealthRecalculateFleeLiveDecision(live ??
				throw new ArgumentNullException(nameof(live)));
		}

		public bool Arrived(CPos destination)
		{
			if (Members.Length == 0)
				return false;
			var center = FormationCenter();
			return Math.Abs(center.X - destination.X) <= 1 &&
				Math.Abs(center.Y - destination.Y) <= 1;
		}

		public CPos FormationCenter()
		{
			return new CPos(
				(int)Math.Round(Members.Average(member => member.CurrentCell.X)),
				(int)Math.Round(Members.Average(member => member.CurrentCell.Y)));
		}
	}
}
