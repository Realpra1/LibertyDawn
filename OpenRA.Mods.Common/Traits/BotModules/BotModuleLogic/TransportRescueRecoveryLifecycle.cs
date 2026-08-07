#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;

namespace OpenRA.Mods.Common.Traits
{
	public enum TransportRescueRecoveryPhase { Active, Returning, Terminal }

	/// <summary>
	/// One-way lifecycle for loaded route-failure rescue recovery. The safe-return deadline is
	/// assigned once and cannot be renewed by repeated plan failures.
	/// </summary>
	public sealed class TransportRescueRecoveryLifecycle
	{
		public TransportRescueRecoveryPhase Phase { get; private set; }
		public int DeadlineTick { get; private set; }

		public bool TryBeginReturn(int currentTick, int timeoutTicks)
		{
			if (Phase != TransportRescueRecoveryPhase.Active)
				return false;

			if (timeoutTicks <= 0)
				throw new ArgumentOutOfRangeException(nameof(timeoutTicks));

			Phase = TransportRescueRecoveryPhase.Returning;
			DeadlineTick = currentTick > int.MaxValue - timeoutTicks ? int.MaxValue : currentTick + timeoutTicks;
			return true;
		}

		public bool TryEnterTerminal(int currentTick)
		{
			if (Phase != TransportRescueRecoveryPhase.Returning || currentTick < DeadlineTick)
				return false;

			Phase = TransportRescueRecoveryPhase.Terminal;
			return true;
		}
	}
}
