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

namespace OpenRA.Mods.Common.Traits
{
	public sealed class StealthLifecycleSavePayload
	{
		const int SchemaVersion = 1;

		public BehaviorId Owner { get; }
		public OwnershipEpoch Epoch { get; }
		public int LastObservedTick { get; }

		public StealthLifecycleSavePayload(BehaviorId owner, OwnershipEpoch epoch, int lastObservedTick)
		{
			if (!Enum.IsDefined(typeof(BehaviorId), owner))
				throw new ArgumentOutOfRangeException(nameof(owner));
			if (epoch.Value <= 0)
				throw new ArgumentOutOfRangeException(nameof(epoch));
			if (lastObservedTick < -1)
				throw new ArgumentOutOfRangeException(nameof(lastObservedTick));

			Owner = owner;
			Epoch = epoch;
			LastObservedTick = lastObservedTick;
		}

		public MiniYamlNode Serialize(string key = "StealthLifecycle")
		{
			return new MiniYamlNode(key, "", new List<MiniYamlNode>
			{
				new MiniYamlNode("Version", SchemaVersion.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("Enabled", FieldSaver.FormatValue(false)),
				new MiniYamlNode("Owner", Owner.ToString()),
				new MiniYamlNode("Epoch", Epoch.Value.ToString(CultureInfo.InvariantCulture)),
				new MiniYamlNode("LastObservedTick", LastObservedTick.ToString(CultureInfo.InvariantCulture))
			});
		}

		public static StealthLifecycleSavePayload Deserialize(MiniYamlNode node)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));

			var values = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var child in node.Value.Nodes)
				values.Add(child.Key, child.Value.Value);

			if (!TryReadInt(values, "Version", out var version) || version != SchemaVersion)
				throw new InvalidOperationException("Unsupported stealth lifecycle save schema.");
			if (!values.TryGetValue("Enabled", out var enabledText) ||
				!bool.TryParse(enabledText, out var enabled) || enabled)
				throw new InvalidOperationException("The stealth lifecycle save payload must remain disabled.");
			if (!values.TryGetValue("Owner", out var ownerText) ||
				!Enum.TryParse(ownerText, out BehaviorId owner) || !Enum.IsDefined(typeof(BehaviorId), owner))
				throw new InvalidOperationException("Invalid stealth lifecycle owner in save payload.");
			if (!values.TryGetValue("Epoch", out var epochText) ||
				!long.TryParse(epochText, NumberStyles.None, CultureInfo.InvariantCulture, out var epoch) || epoch <= 0)
				throw new InvalidOperationException("Invalid stealth lifecycle epoch in save payload.");
			if (!TryReadInt(values, "LastObservedTick", out var lastObservedTick) || lastObservedTick < -1)
				throw new InvalidOperationException("Invalid stealth lifecycle observation tick in save payload.");

			return new StealthLifecycleSavePayload(owner, new OwnershipEpoch(epoch), lastObservedTick);
		}

		static bool TryReadInt(Dictionary<string, string> values, string key, out int value)
		{
			value = 0;
			return values.TryGetValue(key, out var text) &&
				int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}
	}
}
