#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test.Mods.Common
{
	[TestFixture]
	public class CncAirSquadConfigurationTest
	{
		static readonly string[] OrdinarySquadManagers =
		{
			"SquadManagerBotModule@cabal",
			"SquadManagerBotModule@watson",
			"SquadManagerBotModule@hal9001",
			"SquadManagerBotModule@brutalis",
			"SquadManagerBotModule@wavemaker",
			"SquadManagerBotModule@viki",
			"SquadManagerBotModule@skynet",
			"SquadManagerBotModule@ironreaper",
			"SquadManagerBotModule@Easy",
			"SquadManagerBotModule@Easiest"
		};

		static Dictionary<string, MiniYaml> LoadSquadManagers()
		{
			var path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
				"..", "mods", "cnc", "rules", "ai.yaml"));
			var player = MiniYaml.FromFile(path).Single(n => n.Key == "Player");
			return player.Value.Nodes.Where(n => OrdinarySquadManagers.Contains(n.Key))
				.ToDictionary(n => n.Key, n => n.Value);
		}

		static MiniYamlNode LoadPlayer()
		{
			var path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
				"..", "mods", "cnc", "rules", "ai.yaml"));
			return MiniYaml.FromFile(path).Single(n => n.Key == "Player");
		}

		static bool IsAirPolicyNode(MiniYamlNode node)
		{
			return node.Key.StartsWith("Air", StringComparison.Ordinal) ||
				node.Key == "HealthRetreatThreshold" || node.Key == "MaximumAirSquads";
		}

		[Test]
		public void EveryOrdinaryAiUsesTheProvenAirSquadPolicy()
		{
			var managers = LoadSquadManagers();
			Assert.That(managers.Keys, Is.EquivalentTo(OrdinarySquadManagers));

			var expected = managers["SquadManagerBotModule@skynet"].Nodes
				.Where(IsAirPolicyNode).OrderBy(n => n.Key)
				.Select(n => n.Clone()).ToList().WriteToString();
			foreach (var manager in managers)
			{
				var actual = manager.Value.Nodes.Where(IsAirPolicyNode)
					.OrderBy(n => n.Key).Select(n => n.Clone()).ToList().WriteToString();
				Assert.That(actual, Is.EqualTo(expected), manager.Key);
			}
		}

		[Test]
		public void EveryOrdinaryAiHasCompatibleProfilesAndActiveRepairFallback()
		{
			foreach (var manager in LoadSquadManagers())
			{
				var info = FieldLoader.Load<SquadManagerBotModuleInfo>(manager.Value);
				Assert.That(info.AirUnitsTypes, Is.EquivalentTo(new[] { "heli", "orca" }), manager.Key);
				Assert.That(info.AirSquadDefinitions.Keys, Is.EquivalentTo(new[] { "Apache", "Orca" }), manager.Key);
				Assert.That(info.AirSquadDefinitions["Apache"].UnitTypes, Is.EquivalentTo(new[] { "heli" }), manager.Key);
				Assert.That(info.AirSquadDefinitions["Apache"].Profile, Is.EqualTo("Apache"), manager.Key);
				Assert.That(info.AirSquadDefinitions["Orca"].UnitTypes, Is.EquivalentTo(new[] { "orca" }), manager.Key);
				Assert.That(info.AirSquadDefinitions["Orca"].Profile, Is.EqualTo("Orca"), manager.Key);
				Assert.That(info.HealthRetreatThreshold, Is.EqualTo(.5f), manager.Key);
				Assert.That(info.AirPassiveRepairActors, Is.EquivalentTo(new[] { "fix" }), manager.Key);
				Assert.That(info.AirSafetyCheckInterval, Is.EqualTo(25), manager.Key);
				Assert.That(info.AirRouteThreatPenalty, Is.EqualTo(200), manager.Key);
			}
		}

		[Test]
		public void StealthAndAirKeepDistinctScoringWithExplicitLowestWallPriority()
		{
			var player = LoadPlayer();
			var stealth = FieldLoader.Load<StealthTankSquadBotModuleInfo>(
				player.Value.Nodes.Single(n => n.Key == "StealthTankSquadBotModule").Value);
			Assert.That(stealth.WallTargetPriority, Is.EqualTo(1));
			Assert.That(stealth.HarassmentTargetPriorities["harv"], Is.EqualTo(5000));
			Assert.That(stealth.HarassmentTargetPriorities["sharv"], Is.EqualTo(5000));
			Assert.That(stealth.HarassmentTargetPriorities["mcv"], Is.EqualTo(5000));
			Assert.That(stealth.HarassmentTargetPriorities["fact"], Is.EqualTo(2500));
			Assert.That(stealth.PostMissionRetreatDistanceCells, Is.EqualTo(6));
			Assert.That(stealth.PostMissionRetreatToleranceCells, Is.EqualTo(1));
			Assert.That(stealth.RouteThreatPenalty, Is.EqualTo(4));
			Assert.That(stealth.MaximumRouteStretchPercent, Is.EqualTo(150));

			foreach (var manager in LoadSquadManagers())
			{
				var air = FieldLoader.Load<SquadManagerBotModuleInfo>(manager.Value);
				Assert.That(air.AirTargetWallValue, Is.EqualTo(1), manager.Key);
				Assert.That(air.AirTargetHarvesterValue, Is.GreaterThan(air.AirTargetWallValue), manager.Key);
			}

			Assert.That(stealth.HarassmentTargetPriorities["harv"],
				Is.Not.EqualTo(FieldLoader.Load<SquadManagerBotModuleInfo>(
					LoadSquadManagers()["SquadManagerBotModule@brutalis"]).AirTargetHarvesterValue),
				"Stealth scoring must remain distinct from Air; only the switch decision is shared.");
		}
	}
}
