# CNC-30: Crates and Exploration

- Status: complete
- Cycles used: 19 of 30
- Branch: `agent/cnc30-crates-exploration`
- Base: `origin/agent/cnc29-stealth-chem-squad`
- Draft PR: https://github.com/Realpra1/LibertyDawn/pull/57

## Behavior

Ordinary CNC bots now collect only crates whose exact cells are currently visible. A distinct reachable ground collector uses an ordinary move order; an eligible helicopter uses the conservative independent-air route model and an explicit landing order only when the crate cell has zero live stopping threat. Ground candidates cannot cross locomotor domains.

At zero spendable cash or without an owned MCV/construction yard, all otherwise suitable units may be reserved: visible crates come first, then each remaining unit receives a distinct accessible 6x6 region that is unseen or least recently visible. Assignments release on collection, loss, ownership/cargo changes, arrival, recovery, or bounded lack of progress. If no potential collector exists and multiple buildings remain, one configured sellable nonessential building may be sold on cooldown to create a scout.

## Design choices

- Added a dedicated `CrateCollectorBotModule` plus pure deterministic `CrateExplorationPolicy`; crate rewards and player controls are unchanged.
- Bounded work to one scan per 250 ticks, 6x6 regions built once, and at most 32 region candidates per assignment.
- Used current shroud visibility for crates and explored-age only for scout destinations; hidden crates are never read as targets.
- Used `DomainIndex` for cheap ground reachability and existing conservative live AA routing for aircraft, with no fly-by discount at landing.
- Added generic unit reservations to transport coordination so crate collection cannot steal pending passengers, heavy-drop units, carriers, specialists, repair actors, supply actors, harvesters, MCVs, engineers, or commandos.
- Deferred the first scan until initial shroud state is current and provisionally reserved eligible collectors for that single initialization tick.
- Re-scanned immediately after an emergency sale so ordinary squads cannot claim the spawned scout first; a merely busy/reserved collector prevents unnecessary sale.
- Persisted scan timing, initialization state, region ages, sale cooldown, and assignments through game saves.
- Added structured logs for assignment, release, rejection reason, scan state, and emergency sale.

## Cycles

1. Implemented the manager, policy, transport reservation seam, configuration, and tests; fixed initial compile/style issues.
2. Passed 328 tests and full CNC YAML validation.
3. Invalid Lua harness destroyed player actors and therefore the bot modules; rejected as evidence.
4. Corrected harness still ended under conquest before the timed checks.
5. A preplaced anchor did not prevent that conquest timing failure.
6. Mission-objective harness stayed active; ground pickup passed, while air crates began outside live vision.
7. Closer air crates were preempted before the next scan by ordinary air behavior.
8. Preplaced actors exposed the real initial-shroud race: tick-one scanning saw zero visible crates.
9. Delayed initialization exposed an unexplained safe-air candidate rejection.
10. Added bounded rejection logging, which identified an unlandable water cell; one retry was blocked by a stale utility process holding settings.
11. Focused acceptance passed: visible ground and safe-island crates collected; guarded and hidden crates remained.
12. Emergency sale created a rifleman inside the separate MCV-wall enclosure; retained as an invalid boundary fixture.
13. Sale on a fully explored isolated island correctly found no useful accessible stale region; inconclusive for movement.
14. First clean adversarial pass: connected-map sale recovery created and moved one scout through two unique stale regions.
15. Created a real tick-100 save with live ground-crate, air-crate, and scout assignments.
16. Second clean adversarial pass: loaded assignments resumed and completed without duplication or state loss.
17. Five-bot Empire Earth stress run remained active through tick 38,503 and bounded memory, but hit the 15-minute launcher timeout.
18. Third clean adversarial pass: a full ordinary two-bot match ended naturally at tick 35,466 with ground and air collections, emergency exploration, and sales.
19. Final regression repeated literal acceptance and active eight-pair Mammoth transport contention.

## Validation

- Focused final outcome: one visible reachable ground crate and one safe island crate consumed; one MSAM-covered crate and one hidden crate preserved.
- One-to-one assignment: distinct collectors/crates and distinct coarse scout regions in focused and full matches.
- Transport contention: eight reserved Mammoths remained with eight heavy-drop carriers; the crate collector used an unreserved medium tank.
- Save/load: live assignments restored and released/replanned normally.
- Full natural match: tick 35,466; 40 visible-crate assignments, nine safe aircraft landings, four emergency sales, no fatal/Lua/unhandled error.
- Strict Debug solution build: zero warnings/errors.
- Unit tests: 328/328 passed, including six crate exploration policy tests.
- Full CNC YAML/map validator: passed.
- GitHub implementation/report head `17e15f5c53`: Linux passed in 3m19s and Windows passed in 4m46s; PR #57 is mergeable.

## Boundaries and remaining risk

The known MCV-wall enclosure can trap a unit spawned by selling a structure placed inside that enclosure; wall self-block recovery belongs to the queued wall-repair task. On large maps, the literal emergency rule can prolong defeat because surviving units explore widely; the five-bot stress run showed healthy ticks and bounded memory but did not end before the test timeout. The recurring `TibTest.oramap` map-cache warning is a pre-existing invalid user map and unrelated to this change.
