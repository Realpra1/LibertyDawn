// Exact inactive specialist-only method removed from the mixed route-planner
// fixture at source head 609ebf92eeac565af51b2b6acd53de37bb3b39d2.
		[Test]
		public void SoftResourceCostPrefersCleanDetourButDoesNotBlockOnlyRoute()
		{
			var danger = new float[6];
			danger[1] = StealthTankSquadPolicy.SoftResourceRouteCost;
			var detour = ThreatAwareRoutePlanner.FindRoute(danger, 3, 2, 0, 0, 2, 0, 100);
			Assert.That(detour, Does.Not.Contain(new CPos(1, 0)));

			var required = ThreatAwareRoutePlanner.FindRoute(
				new[] { 0f, StealthTankSquadPolicy.SoftResourceRouteCost, 0f },
				3, 1, 0, 0, 2, 0, 100);
			Assert.That(required, Does.Contain(new CPos(1, 0)));
		}
