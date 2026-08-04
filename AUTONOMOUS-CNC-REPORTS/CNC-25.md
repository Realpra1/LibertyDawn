# CNC-25: Mammoth Drop

- Status: complete; GitHub Linux and Windows checks passed
- Cycles used: 28 of 30
- Branch: `agent/cnc25-mammoth-drop`
Pull request: https://github.com/Realpra1/LibertyDawn/pull/44

## Behavior

Brutalis and Iron Reaper can now launch a repeatable mid/late-game heavy drop when ten healthy Mammoths and ten empty Chinooks are available. The AI reserves the complete force as one mission, loads one Mammoth per carrier with bounded concurrent boarding, and launches a viable eight-to-ten-carrier wave after the gathering deadline.

The destination planner ranks valuable economy/base targets and searches behind them for locations with no configured stopping AA danger and bounded ground defenders. Each carrier receives a distinct, spaced, unloadable landing cell and follows the existing conservative independent-air route. Live destination checks replan newly defended drops or hold the wave when no safe replacement exists. A damaged carrier emergency-unloads, while a successful landing releases the Mammoths to normal ground AI control.

Existing route-failure rescue remains the general mechanism for slow units that cannot complete long ground journeys. Heavy drops share its carrier pool, production cap, safe staging/repair behavior, and reservation ledger instead of introducing competing ownership.

## Design choices

- Enabled the strategy for economy-oriented Brutalis and adaptive Iron Reaper, with all timing, actor types, formation size, safety limits, and cooldowns in CNC AI configuration.
- Reused the air AI's exact live AA profile, including true/veterancy range, configured effectiveness, safety margin, and mobile-AA movement buffer; no fly-by discount is applied to stopping locations.
- Required the full ten-unit force before creating a mission, but allowed eight loaded pairs to depart after a bounded gathering timeout so a small staging obstruction cannot stall the strategy forever.
- Removed unfinished boarding pairs before departure so late loaders cannot prolong or redirect an otherwise valid wave.
- Separated landing points by three cells and verified adjacent unload space, fixing simultaneous unload congestion and map-boundary stalls.
- Kept selection bounded to twelve target candidates and deterministic actor/cell ordering.

## Validation

- Full solution build: passed with zero warnings and zero errors.
- Unit tests: 264/264 passed, including heavy-drop eligibility, assembly threshold, safety, bounded boarding, and target-scoring policies.
- Explicit-interface, conditional-interface, and complete CNC YAML/sequence/rule/map validation: passed.
- Engine fixture assembled a full ten-pair mission and launched the viable loaded wave.
- Eight Mammoths followed the long cross-map carrier route, reached distinct cells behind the target base at the playable-map boundary, unloaded, and returned to ordinary ground control.
- Newly defended destination edge: the live replan rejected the changed landing zone and held instead of entering the new danger.
- Congested pickup edge: carrier staging was separated from ground-unit cells, recovered expired approaches, and bounded simultaneous boarding.
- Boundary/unload edge: near-cordon destinations exposed overlapping unload failures; explicit formation spacing, adjacent-space validation, and one-cell arrival tolerance corrected them.
- Straggler edge: unfinished pairs are stopped before the viable wave departs, preventing late boarding from turning a successful drop into a mission timeout.
- Inadequate asset and ineligible-bot cases are rejected by deterministic policy tests without partial reservations.
- GitHub CI: Linux passed in 3m05s; Windows passed in 4m26s.

## Failed cycles and corrections

- Dense initial fixtures placed aircraft over Mammoths and created circular boarding obstruction. Dedicated pickup staging cells and bounded boarding removed that deadlock.
- Reissuing enter orders cancelled valid approaches. Explicit pickup/boarding state plus a deterministic retry window fixed it.
- Aircraft repulsion meant a carrier could be safely adjacent without occupying its exact staging cell. Readiness now uses passenger distance and actual enterability.
- The first formation unloaded too close to the map boundary and into overlapping exit neighborhoods. Wider configurable formation spacing and unload-space checks fixed the placement.
- Two late-loading carriers kept the wave alive after the main force had already landed. Only assembled pairs now leave the gathering state.

## Remaining risks

- The strategy intentionally waits for a large, expensive force and therefore appears only in longer economy games.
- A wave holds when all candidate landing zones become defended. This preserves the force but may delay the strategy until the battlefield changes.
- The shared ten-Chinook production cap means rescue and assault needs can delay assembly, by design.

## Resumed correction pass (2026-08-04)

- Status: complete; GitHub Linux and Windows checks passed
- Additional cycles used: 14 of a fresh 30-cycle allowance (the original 28-cycle history above is retained)
Commit: `8e4469df67`

The resumed pass reproduced and corrected several failures hidden by the original compact fixture. Heavy-drop assembly now runs before generic route-failure rescue can reserve individual carriers. Routed pickup and drop movement ends with an explicit landing order because ordinary aircraft movement may stop several cells short. Pickup validation also requires a carrier-landable cell with a genuinely ground-reachable adjacent Mammoth boarding cell.

When a chosen landing area becomes defended and no safe replacement exists, the wave now recalculates and follows threat-aware routes back to its original distinct assembly cells, unloads there, and releases every reservation. The previous hold state left obsolete movement orders active and could fly the formation into newly powered SAM defenses. Gathering-timeout diagnostics now include distance, reservation, landed state, and current activities.

Validation passed:

- Release build with zero warnings/errors; strict Debug/check build and interface checks.
- 268/268 complete unit tests and 15/15 focused heavy-drop policy tests.
- Complete CNC YAML, sequence, sprite, rule, and map checks, including Archipelago, Empire Earth4, and Empire Lars.
- Full natural 33,857-tick Fastest match: an underfilled 10-Mammoth/4-carrier force remained unreserved, requested carriers, and did not create a partial wave.
- Adversarial defended-destination cycle: a SAM ring activated after departure; all ten carriers cancelled, threat-routed home, unloaded at distinct assembly cells without damage, released reservations, and became available to rescue logic.
- Adversarial unreachable-pickup cycle: only eight valid boarding arrangements produced no partial heavy wave while ordinary rescue continued using available carriers.
- Adversarial damaged-carrier cycle: one carrier below the repair threshold consistently counted as unavailable, leaving 9/10 and producing no partial reservation.
- Final acceptance regression: one complete 10-pair wave assembled, replanned changing destinations, unloaded viable survivors, handed Mammoths to ground control, and released mission ownership.

Preserved evidence is under `AUTONOMOUS-CNC-LOGS/cnc25-*20260804-*`, including the failed protected-target cycle that exposed the obsolete-order hold bug and the three clean post-fix adversarial runs.

GitHub CI passed on the resumed head and PR #44 is cleanly mergeable.

## Reopened regression (2026-08-04)

Status: in progress at 16/30 resumed cycles.

In the user's `ArchipelagoTest` manual game, an AI granted ten transport helicopters and more than ten Mammoths did not form the expected coordinated drop and left both actor groups idle for long periods. CNC-25's launch is not random after strategy eligibility: the current implementation enables heavy drops only for configured bot types (`brutalis` and `ironreaper`) and then requires ten recognized Mammoths, ten healthy empty unreserved carriers, ten ground-reachable pickup arrangements, and a viable undefended landing plan. The resume must log and prove which exact gate changes or fails instead of silently returning.

The manual run did not use `Debug.BotDebug=true`, and its live debug log was subsequently replaced by the paused CNC-24.6 smoke test, so no nonexistent heavy-drop diagnostics are claimed. The timestamp-matching replay, exact `archipelagoTest.oramap`, and end-of-run trait report were preserved at `AUTONOMOUS-CNC-LOGS/cnc25-regression-archipelagoTest-20260804-1144/`. Resume by replaying or relaunching that map with full bot diagnostics and normal competing modules.

### Confirmed slow-loading and handoff defects

A second `ArchipelagoTest` manual cycle provided full heavy-drop diagnostics and raised the resumed count to 16/30. Brutalis found a safe destination and created a complete ten-pair wave. Every pair received a distinct pickup cell, but `HeavyDropConcurrentBoarding: 3` allowed only three active boarding approaches at a time. All ten eventually logged staging, yet the gathering timeout discarded two still-unassembled pairs and launched only eight. The intended correction is a two-phase concurrent pickup, not a longer timeout: route all ten carriers simultaneously to distinct free ground cells beside their assigned Mammoths, then queue each passenger's boarding order only after its transport independently confirms it is landed. Boarding must not target a hovering or moving carrier. This uses ground-cell separation to prevent aircraft collisions while removing artificial serialization.

All eight carriers reached their assigned drop cells and unloaded. The log then claimed `ground-force handoff complete`, but `FinishWave` only released the transport coordinator reservation. During reservation, squad maintenance removed the Mammoths from `unitsHangingAroundTheBase`; they remained present in `activeUnits`, so later `FindNewUnits` treated them as already known and never recruited them. This matches the observed force killing one building and then idling. A successful drop needs an explicit cohesive assault-squad registration using the surviving passengers and retained/re-evaluated target. A safe abort must instead restore ordinary assignment eligibility.

Complete logs, replay, map, performance data, and trait report are preserved at `AUTONOMOUS-CNC-LOGS/cnc25-regression-archipelagoTest-20260804-1200-slow-load-orphaned-handoff/`.

## Concurrent pickup and assault-handoff correction (2026-08-04)

- Status: complete; GitHub Linux and Windows checks passed
Resumed cycles used: 22 of 30 (original 28-cycle history retained)
Final commit: `8bd9305ff1`

All selected carriers now receive their threat-routed landing orders concurrently. Pickup cells must be free of mobile blockers and separated by the configured formation spacing. A passenger is not ordered aboard until its assigned carrier occupies the exact pickup cell, reports land altitude, and is enterable. CNC configuration and the code default now allow all ten pickup approaches concurrently; timeout/retry recovery remains bounded.

Successful unloads now explicitly create one active assault squad from every surviving dropped Mammoth, retain the live drop target or re-evaluate the nearest valid enemy, and immediately issue a grouped attack-move. Safe returns and aborts instead restore the passengers to ordinary ground-squad eligibility. Reservations are released before either handoff. The refined root cause was not stale `activeUnits` bookkeeping—the reservation cleanup already removes that state—but the lack of an explicit handoff combined with the ordinary randomized assault threshold, which can leave an eight-unit drop waiting at the base pool boundary.

Local validation:

- Strict Debug build passed with warnings as errors; Release build passed with zero warnings/errors.
- 272/272 unit tests passed, including four landed-pickup gate cases and 19 focused heavy-drop policy tests.
- Explicit-interface, conditional-interface, and complete CNC YAML/map validation passed.
- Cycle 18 normal acceptance (`MAX`, original `ArchipelagoTest` layout): eight concurrent carriers, two emergency unloads, `adopted=8/8`, target Fact removed, Brutalis won naturally at tick 4,120.
- Adversarial cycle 19 (ordinary `Fastest` full match): repeated carrier damage and emergency unload retries, `adopted=8/8`, Brutalis won naturally at tick 4,165.
- Adversarial cycle 20 (defended destination plus rescue contention): the wave recalculated, routed home, unloaded at assembly, and logged `restored=8/8` without creating an enemy assault.
- Adversarial cycle 21 (central-island exact capacity): all ten carriers received concurrent distinct pickup routes, all ten loaded and unloaded, `adopted=10/10`, and Brutalis won naturally at tick 9,451.
- Final cycle 22 (`MAX`, original layout): eight carriers assembled without the former three-slot bottleneck, `adopted=8/8`, and Brutalis won naturally at tick 3,071.

Evidence is preserved in `AUTONOMOUS-CNC-LOGS/cnc25-cycle17-abort-contention-20260804/` through `cnc25-cycle22-final-regression-20260804/`.
