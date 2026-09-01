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

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Instance-local guard for behavior calls that may invoke reentrant external callbacks.
	/// It does not change lifecycle ownership and is currently used only by the Crush owner.
	/// </summary>
	sealed class StealthBehaviorExecutionLease
	{
		bool held;
		long revision;
		long reservedRevision;

		public long Acquire(string behaviorName, Action ensureActiveOwnership)
		{
			if (ensureActiveOwnership == null)
				throw new ArgumentNullException(nameof(ensureActiveOwnership));
			if (held)
				throw new InvalidOperationException(
					"Recursive " + behaviorName + " execution or restore is not allowed.");
			if (revision == long.MaxValue)
				throw new InvalidOperationException(behaviorName + " state revision is exhausted.");

			held = true;
			reservedRevision = revision + 1;
			try
			{
				ensureActiveOwnership();
				return revision;
			}
			catch
			{
				held = false;
				reservedRevision = 0;
				throw;
			}
		}

		public void Verify(long acquiredRevision, string behaviorName,
			Action ensureActiveOwnership)
		{
			if (!held || revision != acquiredRevision)
				throw new InvalidOperationException(
					behaviorName + " execution lease is stale.");
			ensureActiveOwnership();
			if (!held || revision != acquiredRevision)
				throw new InvalidOperationException(
					behaviorName + " execution lease changed during ownership validation.");
		}

		public void Commit(long acquiredRevision, string behaviorName,
			Action ensureActiveOwnership, Action commit)
		{
			if (commit == null)
				throw new ArgumentNullException(nameof(commit));
			Verify(acquiredRevision, behaviorName, ensureActiveOwnership);

			commit();
			revision = reservedRevision;
		}

		public void Release(long acquiredRevision)
		{
			if (!held || (revision != acquiredRevision && revision != reservedRevision))
				throw new InvalidOperationException("Invalid behavior execution lease release.");
			held = false;
			reservedRevision = 0;
		}
	}
}
