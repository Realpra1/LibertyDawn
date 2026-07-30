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

using System.Collections.Generic;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class AirThreatGeometryTest
	{
		static WPos Cell(int x, int y)
		{
			return new WPos(x * 1024 + 512, y * 1024 + 512, 0);
		}

		static long CellsSquared(int cells)
		{
			return (long)(cells * 1024) * (cells * 1024);
		}

		[TestCase(TestName = "Coarse A* detours around expensive anti-air cells")]
		public void CoarseRouteAvoidsThreat()
		{
			var danger = new float[15];
			danger[2] = 10;
			var route = AirThreatGeometry.FindCoarseRoute(danger, 5, 3, 0, 0, 4, 0, 100);
			Assert.That(route, Is.Not.Null);
			Assert.That(route, Does.Not.Contain(new CPos(2, 0)));
		}

		[TestCase(TestName = "Coarse A* crosses finite danger when no detour exists")]
		public void CoarseRouteCanAcceptDanger()
		{
			var danger = new[] { 0f, 10f, 0f };
			var route = AirThreatGeometry.FindCoarseRoute(danger, 3, 1, 0, 0, 2, 0, 100);
			Assert.That(route, Does.Contain(new CPos(1, 0)));
		}

		[TestCase(TestName = "Distance to a segment uses the perpendicular when the foot is inside it")]
		public void PerpendicularDistance()
		{
			var a = Cell(0, 0);
			var b = Cell(20, 0);

			// Directly above the middle of the segment, five cells off.
			var d = AirThreatGeometry.DistanceSquaredToSegment(Cell(10, 5), a, b);
			Assert.That(d, Is.EqualTo(CellsSquared(5)));

			// On the segment.
			Assert.That(AirThreatGeometry.DistanceSquaredToSegment(Cell(7, 0), a, b), Is.EqualTo(0));
		}

		[TestCase(TestName = "Distance to a segment clamps to the endpoints")]
		public void EndpointDistance()
		{
			var a = Cell(0, 0);
			var b = Cell(20, 0);

			// Well behind the start: the nearest point is the start itself, not the infinite line.
			Assert.That(AirThreatGeometry.DistanceSquaredToSegment(Cell(-10, 0), a, b), Is.EqualTo(CellsSquared(10)));

			// Well beyond the end.
			Assert.That(AirThreatGeometry.DistanceSquaredToSegment(Cell(35, 0), a, b), Is.EqualTo(CellsSquared(15)));
		}

		[TestCase(TestName = "A degenerate segment is treated as a point")]
		public void DegenerateSegment()
		{
			var a = Cell(4, 4);
			Assert.That(AirThreatGeometry.DistanceSquaredToSegment(Cell(4, 7), a, a), Is.EqualTo(CellsSquared(3)));
		}

		[TestCase(TestName = "Only threats inside the flight corridor are counted")]
		public void ThreatsInCorridor()
		{
			var from = Cell(0, 0);
			var to = Cell(60, 0);
			var threats = new List<WPos>
			{
				Cell(20, 3),  // inside the corridor
				Cell(30, 20), // far off to the side
				Cell(40, -4), // inside, other side
			};

			var count = AirThreatGeometry.CountThreatsNearRoute(threats, from, to, WDist.FromCells(8), WDist.FromCells(10));
			Assert.That(count, Is.EqualTo(2));
		}

		[TestCase(TestName = "Threats sitting on the destination are excluded from the route penalty")]
		public void DestinationThreatsExcluded()
		{
			var from = Cell(0, 0);
			var to = Cell(60, 0);

			// A SAM right next to the target: already priced in by the target's own anti-air penalty.
			var threats = new List<WPos> { Cell(57, 2) };

			Assert.That(AirThreatGeometry.CountThreatsNearRoute(threats, from, to, WDist.FromCells(8), WDist.FromCells(10)),
				Is.EqualTo(0));

			// The same SAM does count once the exclusion no longer reaches it.
			Assert.That(AirThreatGeometry.CountThreatsNearRoute(threats, from, to, WDist.FromCells(8), WDist.FromCells(1)),
				Is.EqualTo(1));
		}

		[TestCase(TestName = "A threat behind the squad does not make the route look dangerous")]
		public void ThreatBehindSquadIgnored()
		{
			var from = Cell(20, 0);
			var to = Cell(60, 0);
			var threats = new List<WPos> { Cell(0, 0) };

			Assert.That(AirThreatGeometry.CountThreatsNearRoute(threats, from, to, WDist.FromCells(8), WDist.FromCells(10)),
				Is.EqualTo(0));
		}

		[TestCase(TestName = "An empty or disabled corridor never penalises")]
		public void NoCorridor()
		{
			var threats = new List<WPos> { Cell(10, 0) };
			Assert.That(AirThreatGeometry.CountThreatsNearRoute(threats, Cell(0, 0), Cell(20, 0), WDist.Zero, WDist.Zero),
				Is.EqualTo(0));
			Assert.That(AirThreatGeometry.CountThreatsNearRoute(null, Cell(0, 0), Cell(20, 0), WDist.FromCells(8), WDist.Zero),
				Is.EqualTo(0));
		}

		[TestCase(TestName = "Nearest threat distance ignores everything but the closest")]
		public void NearestThreat()
		{
			var threats = new List<WPos> { Cell(50, 0), Cell(3, 0), Cell(90, 90) };
			Assert.That(AirThreatGeometry.NearestThreatDistanceSquared(Cell(0, 0), threats), Is.EqualTo(CellsSquared(3)));
			Assert.That(AirThreatGeometry.NearestThreatDistanceSquared(Cell(0, 0), new List<WPos>()), Is.EqualTo(long.MaxValue));
		}

		[TestCase(TestName = "The retreat point is the one furthest from the nearest threat")]
		public void SafestRetreatPoint()
		{
			var candidates = new List<WPos> { Cell(10, 0), Cell(50, 0), Cell(30, 0) };
			var threats = new List<WPos> { Cell(0, 0) };

			Assert.That(AirThreatGeometry.SafestCandidate(candidates, threats), Is.EqualTo(1));

			// Two threats, one at each end: the middle candidate wins.
			threats.Add(Cell(60, 0));
			Assert.That(AirThreatGeometry.SafestCandidate(candidates, threats), Is.EqualTo(2));
		}

		[TestCase(TestName = "With nothing remembered the first retreat candidate is taken, and no candidates gives -1")]
		public void SafestRetreatPointDegenerate()
		{
			var candidates = new List<WPos> { Cell(10, 0), Cell(50, 0) };
			Assert.That(AirThreatGeometry.SafestCandidate(candidates, new List<WPos>()), Is.EqualTo(0));
			Assert.That(AirThreatGeometry.SafestCandidate(new List<WPos>(), new List<WPos>()), Is.EqualTo(-1));
		}

		[TestCase(TestName = "A squad of five breaks off for one anti-air actor; the uncapped squad did not")]
		public void SquadSizeDecidesWhoRunsAway()
		{
			// AirThreatFleeMultiplier as wired into SquadManagerBotModule@skynet.
			const int Multiplier = 8;

			// SkyNet builds heli: 8 + orca: 12, and before the cap they all shared one squad. A single
			// Sheep (SheepAA, Burst 2 x 6000 damage, enough to delete an 8500 HP Orca in one salvo) was
			// therefore not enough to make the squad leave - it took three.
			Assert.That(AirThreatGeometry.ShouldFleeAntiAir(1, Multiplier, 20), Is.False);
			Assert.That(AirThreatGeometry.ShouldFleeAntiAir(2, Multiplier, 20), Is.False);
			Assert.That(AirThreatGeometry.ShouldFleeAntiAir(3, Multiplier, 20), Is.True);

			// Capped at AirSquadSize: 5, the first one is enough.
			Assert.That(AirThreatGeometry.ShouldFleeAntiAir(1, Multiplier, 5), Is.True);

			// Nothing seen is never a reason to run.
			Assert.That(AirThreatGeometry.ShouldFleeAntiAir(0, Multiplier, 5), Is.False);
		}

		[TestCase(TestName = "A vector is rescaled to the requested length, keeping its direction")]
		public void ScaleToLength()
		{
			// 3-4-5 triangle: (3072, 4096) has length 5120, rescale to 10240 and it doubles.
			var scaled = AirThreatGeometry.ScaleToLength(new WVec(3072, 4096, 0), 10240);
			Assert.That(scaled, Is.EqualTo(new WVec(6144, 8192, 0)));

			// Shortening works the same way, and the sign of each component is preserved.
			Assert.That(AirThreatGeometry.ScaleToLength(new WVec(-3072, 4096, 0), 5120 / 2),
				Is.EqualTo(new WVec(-1536, 2048, 0)));

			// Degenerate inputs.
			Assert.That(AirThreatGeometry.ScaleToLength(WVec.Zero, 1024), Is.EqualTo(WVec.Zero));
			Assert.That(AirThreatGeometry.ScaleToLength(new WVec(1024, 0, 0), 0), Is.EqualTo(WVec.Zero));
		}

		[TestCase(TestName = "Evasion moves directly away from the nearest threat, by the hop distance")]
		public void EvadeMovesAwayFromNearestThreat()
		{
			var from = Cell(50, 50);

			// Threat due west: the hop is due east.
			var threats = new List<WPos> { Cell(40, 50) };
			var to = AirThreatGeometry.EvadeDestination(from, threats, WDist.FromCells(12), WVec.Zero);

			Assert.That(to.Y, Is.EqualTo(from.Y));
			Assert.That(to.X - from.X, Is.EqualTo(12 * 1024));

			// A second, closer threat to the north takes over and the hop turns south.
			threats.Add(Cell(50, 46));
			to = AirThreatGeometry.EvadeDestination(from, threats, WDist.FromCells(12), WVec.Zero);
			Assert.That(to.X, Is.EqualTo(from.X));
			Assert.That(to.Y - from.Y, Is.EqualTo(12 * 1024));
		}

		[TestCase(TestName = "Evasion hops are local: never further than the hop distance plus the jitter")]
		public void EvadeStaysLocal()
		{
			// The whole point of round 3: this must be a short hop, not a flight home. A threat 70 cells
			// away - the sort of distance the old retreat-to-a-building move covered - still only moves
			// the squad AirEvadeDistance cells.
			var from = Cell(100, 100);
			var threats = new List<WPos> { Cell(30, 100) };

			var hop = WDist.FromCells(12);
			var jitter = new WVec(5 * 1024, -5 * 1024, 0);
			var to = AirThreatGeometry.EvadeDestination(from, threats, hop, jitter);

			var moved = (to - from).Length;
			Assert.That(moved, Is.LessThanOrEqualTo(hop.Length + jitter.Length));
			Assert.That(moved, Is.LessThan(WDist.FromCells(20).Length));
		}

		[TestCase(TestName = "With nothing to run from, evasion is just a nearby random reposition")]
		public void EvadeWithoutThreatsIsPureJitter()
		{
			var from = Cell(50, 50);
			var jitter = new WVec(3 * 1024, 2 * 1024, 0);

			Assert.That(AirThreatGeometry.EvadeDestination(from, new List<WPos>(), WDist.FromCells(12), jitter),
				Is.EqualTo(from + jitter));
			Assert.That(AirThreatGeometry.EvadeDestination(from, null, WDist.FromCells(12), jitter),
				Is.EqualTo(from + jitter));

			// A threat we are sitting exactly on top of gives no direction to run in, so the jitter decides.
			Assert.That(AirThreatGeometry.EvadeDestination(from, new List<WPos> { from }, WDist.FromCells(12), jitter),
				Is.EqualTo(from + jitter));
		}

		[TestCase(TestName = "Evasion increases the distance to the threat it is running from")]
		public void EvadeIncreasesThreatDistance()
		{
			var from = Cell(50, 50);
			var threats = new List<WPos> { Cell(44, 43) };

			var before = AirThreatGeometry.NearestThreatDistanceSquared(from, threats);
			var to = AirThreatGeometry.EvadeDestination(from, threats, WDist.FromCells(12), WVec.Zero);
			var after = AirThreatGeometry.NearestThreatDistanceSquared(to, threats);

			Assert.That(after, Is.GreaterThan(before));
		}

		// The values below are the ones wired into SquadManagerBotModule@skynet in mods/cnc/rules/ai.yaml.
		const int Harvester = 1000;
		const int Unit = 450;
		const int Production = 300;
		const int Building = 100;
		const int DefencelessBonus = 250;
		const int AntiAirPenalty = 700;
		const int RoutePenalty = 200;
		const int DistancePenalty = 3;
		const int MinimumScore = 200;

		static int Score(int classValue, bool defenceless, int antiAir, int routeThreats, int distanceInCells)
		{
			return AirThreatGeometry.TargetScore(classValue, defenceless, DefencelessBonus,
				antiAir, AntiAirPenalty, routeThreats, RoutePenalty, distanceInCells, DistancePenalty);
		}

		[TestCase(TestName = "A defenceless unit outranks a production building at the same range")]
		public void SoftUnitsBeatStructures()
		{
			var tank = Score(Unit, true, 0, 0, 20);
			var refinery = Score(Production, true, 0, 0, 20);
			var powerPlant = Score(Building, true, 0, 0, 20);

			Assert.That(tank, Is.GreaterThan(refinery));
			Assert.That(refinery, Is.GreaterThan(powerPlant));
		}

		[TestCase(TestName = "A harvester outranks every other class")]
		public void HarvestersWin()
		{
			var harvester = Score(Harvester, true, 0, 0, 20);
			Assert.That(harvester, Is.GreaterThan(Score(Unit, true, 0, 0, 20)));
			Assert.That(harvester, Is.GreaterThan(Score(Production, true, 0, 0, 20)));

			// Even a distant harvester beats a nearby generic building.
			Assert.That(Score(Harvester, true, 0, 0, 120), Is.GreaterThan(Score(Building, true, 0, 0, 0)));
		}

		[TestCase(TestName = "Anti-air cover rejects a unit outright but a harvester is still worth one SAM")]
		public void AntiAirCoverIsRespected()
		{
			Assert.That(Score(Unit, true, 1, 0, 20), Is.LessThan(MinimumScore));
			Assert.That(Score(Harvester, true, 1, 0, 20), Is.GreaterThanOrEqualTo(MinimumScore));
			Assert.That(Score(Harvester, true, 2, 0, 20), Is.LessThan(MinimumScore));
		}

		[TestCase(TestName = "An anti-air capable target gets no defenceless bonus")]
		public void AntiAirTargetsGetNoBonus()
		{
			// A SAM site: a building that also counts itself as anti-air cover.
			Assert.That(Score(Building, false, 1, 0, 10), Is.LessThan(MinimumScore));
			Assert.That(Score(Unit, false, 0, 0, 10), Is.EqualTo(Score(Unit, true, 0, 0, 10) - DefencelessBonus));
		}

		[TestCase(TestName = "Two otherwise equal harvesters are separated by the anti-air on the way there")]
		public void RouteThreatsBreakTies()
		{
			var clearRoute = Score(Harvester, true, 0, 0, 40);
			var throughSams = Score(Harvester, true, 0, 3, 40);

			Assert.That(clearRoute, Is.GreaterThan(throughSams));
			Assert.That(clearRoute - throughSams, Is.EqualTo(3 * RoutePenalty));
		}

		[TestCase(TestName = "A weapon's range is respected further out once padded by the buffer, but no further")]
		public void BufferedRangeExtendsCoverage()
		{
			// A TowerAAMissile site: Range 10c0, AirThreatRangeBuffer 1.5 as wired into
			// SquadManagerBotModule@skynet.
			Assert.That(AirThreatGeometry.IsWithinBufferedRange(10, 10, 1.5f), Is.True);
			Assert.That(AirThreatGeometry.IsWithinBufferedRange(15, 10, 1.5f), Is.True);
			Assert.That(AirThreatGeometry.IsWithinBufferedRange(16, 10, 1.5f), Is.False);

			// No buffer at all is just the raw range.
			Assert.That(AirThreatGeometry.IsWithinBufferedRange(11, 10, 1f), Is.False);
		}

		[TestCase(TestName = "An Orca outruns everything except the two fastest AA missiles it can meet")]
		public void OrcaOutrunsMostAntiAir()
		{
			// Real speeds (WDist/tick) from mods/cnc/rules/aircraft.yaml and
			// mods/cnc/weapons/missiles.yaml.
			const int OrcaSpeed = 230;
			const int ApacheSpeed = 160;

			const int OrcaAaMissileSpeed = 341;
			const int AaRocketsSpeed = 350;
			const int TowerAaMissileSpeed = 180;
			const int BikeAaRocketsSpeed = 213;
			const int StealthTankAaSpeed = 213; // 227mm.stnkAA
			const int DragonSpeed = 210;

			// The two the feedback specifically called out as too fast to outrun.
			Assert.That(AirThreatGeometry.CanOutrun(OrcaSpeed, OrcaAaMissileSpeed), Is.False);
			Assert.That(AirThreatGeometry.CanOutrun(OrcaSpeed, AaRocketsSpeed), Is.False);

			// Everything else, the Orca can fly straight through without stopping.
			Assert.That(AirThreatGeometry.CanOutrun(OrcaSpeed, TowerAaMissileSpeed), Is.True);
			Assert.That(AirThreatGeometry.CanOutrun(OrcaSpeed, BikeAaRocketsSpeed), Is.True);
			Assert.That(AirThreatGeometry.CanOutrun(OrcaSpeed, StealthTankAaSpeed), Is.True);
			Assert.That(AirThreatGeometry.CanOutrun(OrcaSpeed, DragonSpeed), Is.True);

			// The slower Apache cannot outrun anything an Orca can - not even the Dragon.
			Assert.That(AirThreatGeometry.CanOutrun(ApacheSpeed, DragonSpeed), Is.False);

			// A tie is not an outrun: the projectile still catches up eventually.
			Assert.That(AirThreatGeometry.CanOutrun(200, 200), Is.False);
		}

		[TestCase(TestName = "Units that are bad at anti-air score well below a dedicated AA weapon")]
		public void AaEffectivenessReflectsHowBadTheUnitActuallyIs()
		{
			// Real Inaccuracy/Damage from mods/cnc/weapons/missiles.yaml. In each case the AA weapon
			// inherits its unit's primary ground weapon and overrides Inaccuracy sharply upward
			// alongside a damage cut - exactly the pattern AaEffectiveness is meant to discount.

			// E3 Rocket Soldier: Rockets (Inaccuracy 128, Damage 4000) vs AARockets (1800, 1500).
			var rocketSoldier = AirThreatGeometry.AaEffectiveness(1800, 1500, 128, 4000);
			Assert.That(rocketSoldier, Is.EqualTo(0.2231f).Within(0.001f));

			// Recon Bike: BikeRockets (128, 2500 - inherited from ^MissileWeapon) vs BikeAARockets (1800, 800).
			var bike = AirThreatGeometry.AaEffectiveness(1800, 800, 128, 2500);
			Assert.That(bike, Is.EqualTo(0.1956f).Within(0.001f));

			// Stealth Tank: 227mm.stnk (213, 10000) vs 227mm.stnkAA (1800, 4000).
			var stealthTank = AirThreatGeometry.AaEffectiveness(1800, 4000, 213, 10000);
			Assert.That(stealthTank, Is.EqualTo(0.2592f).Within(0.001f));

			// All three are clearly worse than a dedicated AA weapon, but never treated as harmless.
			Assert.That(rocketSoldier, Is.LessThan(0.5f));
			Assert.That(bike, Is.LessThan(0.5f));
			Assert.That(stealthTank, Is.LessThan(0.5f));

			// A dedicated single-purpose AA unit - nothing to compare against - is a full threat.
			Assert.That(AirThreatGeometry.AaEffectiveness(0, 5000, 0, 0), Is.EqualTo(1f));

			// Never entirely harmless, never overweighted past a genuine full threat.
			Assert.That(AirThreatGeometry.AaEffectiveness(100000, 1, 128, 4000), Is.EqualTo(0.05f));
			Assert.That(AirThreatGeometry.AaEffectiveness(1, 100000, 128, 4000), Is.EqualTo(1f));
		}

		[Test]
		public void SmoothRouteCollapsesSafeGridPathToStraightFlight()
		{
			var danger = new float[25];
			var route = new[] { new CPos(1, 0), new CPos(2, 0), new CPos(2, 1), new CPos(2, 2), new CPos(3, 2), new CPos(4, 2) };

			var smoothed = AirThreatGeometry.SmoothCoarseRoute(danger, 5, 5, 0, 0, route);

			Assert.That(smoothed, Is.EqualTo(new[] { new CPos(4, 2) }));
		}

		[Test]
		public void SmoothRouteKeepsDetourAroundDanger()
		{
			var danger = new float[25];
			danger[2 * 5 + 2] = 1;
			var route = new[] { new CPos(1, 1), new CPos(2, 1), new CPos(3, 1), new CPos(4, 2) };

			var smoothed = AirThreatGeometry.SmoothCoarseRoute(danger, 5, 5, 0, 2, route);

			Assert.That(smoothed.Count, Is.GreaterThan(1));
			Assert.That(smoothed[smoothed.Count - 1], Is.EqualTo(new CPos(4, 2)));
		}

		[Test]
		public void TargetCandidatesCombineNearestAndHighestValue()
		{
			var selected = AirThreatGeometry.SelectTargetCandidates(
				new long[] { 1, 2, 3, 100, 200 },
				new[] { 10, 20, 30, 1000, 900 },
				closestCount: 3, highestValueCount: 2);

			Assert.That(selected, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
		}
	}
}
