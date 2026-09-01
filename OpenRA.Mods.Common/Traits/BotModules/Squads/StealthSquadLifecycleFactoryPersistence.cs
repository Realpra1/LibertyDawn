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
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	static class StealthSquadLifecycleFactoryPersistence
	{
		public static MiniYamlNode PristineConstruction(string key, IEnumerable<uint> expectedActorIds)
		{
			var nodes = expectedActorIds.OrderBy(id => id)
				.Select(id => new MiniYamlNode("ExpectedMemberId", FieldSaver.FormatValue(id))).ToList();
			return new MiniYamlNode(key, "Pristine", nodes);
		}

		public static uint[] RestorePristineConstruction(MiniYamlNode node)
		{
			if (node == null || node.Value.Value != "Pristine" ||
				node.Value.Nodes.Any(child => child.Key != "ExpectedMemberId"))
				throw new InvalidOperationException("Invalid pristine SquadConstruction persistence shape.");
			var ids = node.Value.Nodes.Select(child =>
				FieldLoader.GetValue<uint>(child.Key, child.Value.Value)).ToArray();
			if (ids.Length == 0 || ids.Any(id => id == 0) ||
				!ids.SequenceEqual(ids.Distinct().OrderBy(id => id)))
				throw new InvalidOperationException("Invalid pristine SquadConstruction member identities.");
			return ids;
		}
	}
}
