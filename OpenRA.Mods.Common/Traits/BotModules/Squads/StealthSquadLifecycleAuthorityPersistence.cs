#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is made available to you under the terms of
 * the GNU General Public License as published by the Free Software Foundation.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	/// <summary>Validates the serialized authority mode before Squad construction or live lookup.</summary>
	static class StealthSquadLifecycleAuthorityPersistence
	{
		static readonly IReadOnlyList<string> RequiredModularKeys = Array.AsReadOnly(new[]
		{
			"Type", "Units", "StealthSquadDefinition", "StealthSquadIndex"
		});
		static readonly IReadOnlyList<string> OptionalModularKeys = Array.AsReadOnly(new[]
		{
			"Target", "AirSquadDefinition", "AirUnitsRepairing", "AirReinforcements",
			"AirFormationCenter", "GroundReinforcements", "GroundFormationCenter",

			// Old modular saves are accepted, but their tactical runtime state is ignored.
			"StealthLifecycleRuntime"
		});
		internal static readonly IReadOnlyList<string> LegacyAuthorityKeys = Array.AsReadOnly(new[]
		{
			"StealthCadenceGenerationId", "StealthCadenceGenerationStartTick",
			"StealthCadenceWindowStartTick", "StealthCadenceLastObservedTick",
			"StealthCadenceAge", "StealthCadenceAttributedKills", "StealthCadenceFailed",
			"StealthCadenceMismatchFailed", "AirEscapingLocalAa", "StealthEscapeIssuedTick",
			"StealthEscapeSafetyChecks", "StealthEscapeDestination", "StealthEscapeStartCell",
			"StealthEscapeDestinationCell", "StealthEscapePendingExplosion",
			"StealthEscapeLastProgressTick", "StealthEscapeLastDistanceCells",
			"StealthEscapePreserveEngagement", "AirTargetStrategicCell"
		});

		internal static void Validate(MiniYaml yaml, bool modularAuthority)
		{
			if (yaml == null)
				throw new ArgumentNullException(nameof(yaml));
			if (!modularAuthority)
			{
				if (yaml.Nodes.Any(node => node.Key == "StealthLifecycleRuntime"))
					throw new InvalidOperationException(
						"Legacy authority saves cannot contain modular lifecycle state.");
				return;
			}

			var allowed = RequiredModularKeys.Concat(OptionalModularKeys).ToHashSet(StringComparer.Ordinal);
			if (RequiredModularKeys.Any(key =>
					yaml.Nodes.Count(node => node.Key == key) != 1) ||
				yaml.Nodes.Any(node => !allowed.Contains(node.Key)) ||
				OptionalModularKeys.Any(key => yaml.Nodes.Count(node => node.Key == key) > 1))
				throw new InvalidOperationException(
					"Modular stealth saves require one canonical top-level runtime authority shape.");
		}
	}
}
