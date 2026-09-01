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
using System.Globalization;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Reserves an action before invoking the external callback. An ambiguous callback failure is
	/// therefore at-most-once externally and can never be retried as a duplicate after reentrancy,
	/// invalidation, or save/load.
	/// </summary>
	public sealed class StealthLifecycleRuntimeOrders : IStealthLifecycleRuntimeOrders
	{
		const int Version = 1;
		readonly IStealthLifecycleOwnershipGuard guard;
		readonly IStealthLifecycleRuntimeOrderTarget target;
		BehaviorId owner;
		OwnershipEpoch epoch;
		string acceptedFingerprint;
		bool issuing;

		public StealthLifecycleRuntimeOrders(IStealthLifecycleOwnershipGuard guard,
			IStealthLifecycleRuntimeOrderTarget target, BehaviorId owner, OwnershipEpoch epoch)
		{
			this.guard = guard ?? throw new ArgumentNullException(nameof(guard));
			this.target = target ?? throw new ArgumentNullException(nameof(target));
			Reset(owner, epoch);
		}

		public void Issue(StealthLifecycleRuntimeOrder order)
		{
			if (order == null)
				throw new ArgumentNullException(nameof(order));
			EnsureActive(order.Owner, order.Epoch);
			if (issuing)
				throw new InvalidOperationException("Recursive stealth runtime order callbacks are not allowed.");
			if (acceptedFingerprint == order.Fingerprint)
				return;
			var apply = target.Prepare(order) ?? throw new InvalidOperationException(
				"The runtime order target did not prepare an external callback.");
			EnsureActive(order.Owner, order.Epoch);

			// Preparation is side-effect free. Reservation intentionally survives callback exceptions:
			// whether the target applied an
			// order before throwing is unknowable, so retrying that same action would violate external
			// idempotence. A distinct later token replaces the committed active action.
			acceptedFingerprint = order.Fingerprint;
			issuing = true;
			try
			{
				apply();
				EnsureActive(order.Owner, order.Epoch);
			}
			finally
			{
				issuing = false;
			}
		}

		public void Reset(BehaviorId nextOwner, OwnershipEpoch nextEpoch)
		{
			if (issuing)
				throw new InvalidOperationException("An order callback cannot reset runtime ownership.");
			if (!Enum.IsDefined(typeof(BehaviorId), nextOwner))
				throw new ArgumentOutOfRangeException(nameof(nextOwner));
			owner = nextOwner;
			epoch = nextEpoch;
			acceptedFingerprint = null;
		}

		public MiniYamlNode Serialize(string key = "OrderSink")
		{
			if (issuing)
				throw new InvalidOperationException("Cannot save during an external order callback.");
			return new MiniYamlNode(key, "", new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", Version.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Owner", owner.ToString()),
				new MiniYamlNode("Epoch", epoch.Value.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("AcceptedFingerprint", acceptedFingerprint ?? "")
			});
		}

		public void Restore(MiniYamlNode node, BehaviorId expectedOwner, OwnershipEpoch expectedEpoch)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));
			if (issuing)
				throw new InvalidOperationException("Cannot restore during an external order callback.");
			var values = node.Value.Nodes.ToDictionary(child => child.Key, child => child.Value.Value,
				StringComparer.Ordinal);
			if (values.Count != 4 || !values.TryGetValue("Version", out var versionText) ||
				!int.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture, out var version) ||
				version != Version || !values.TryGetValue("Owner", out var ownerText) ||
				!Enum.TryParse(ownerText, out BehaviorId savedOwner) || savedOwner != expectedOwner ||
				!values.TryGetValue("Epoch", out var epochText) ||
				!long.TryParse(epochText, NumberStyles.None, CultureInfo.InvariantCulture, out var savedEpoch) ||
				savedEpoch != expectedEpoch.Value || !values.TryGetValue("AcceptedFingerprint", out var fingerprint))
				throw new InvalidOperationException("Invalid stealth runtime order-sink state.");

			owner = expectedOwner;
			epoch = expectedEpoch;
			acceptedFingerprint = string.IsNullOrEmpty(fingerprint) ? null : fingerprint;
		}

		void EnsureActive(BehaviorId candidateOwner, OwnershipEpoch candidateEpoch)
		{
			if (candidateOwner != owner || candidateEpoch != epoch || !guard.IsActive(owner, epoch))
				throw new InvalidOperationException("Only the exact active stealth owner may issue runtime orders.");
		}
	}
}
