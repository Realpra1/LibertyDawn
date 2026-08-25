// Exact inactive specialist-only members removed from the mixed Air fixture at
// source head 609ebf92eeac565af51b2b6acd53de37bb3b39d2.
		static MiniYamlNode LoadPlayer()
		{
			var path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
				"..", "mods", "cnc", "rules", "ai.yaml"));
			return MiniYaml.FromFile(path).Single(n => n.Key == "Player");
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
