# CNC-31: Red-Tiberium Bomb Harvesters

- Status: local implementation, engine acceptance, and validation passed; publication gate pending
- Cycles used: 28 of 30 so far
- Branch: `agent/cnc31-red-tiberium-bomb-trucks`
- Base: `origin/agent/cnc30-crates-exploration`
- Pull request: pending

## Behavior

Ordinary CNC bots now reserve selected stealth harvesters for deliberate red-Tiberium bomb missions. Launch credit accrues fractionally at 5% of all live harvesters per game minute, only while an eligible stealth harvester, real red resource, and a distinct viable enemy target exist. Credit is capped at one stored launch and spent only after a mission is successfully created.

A selected stealth harvester receives a replay-safe explicit unstable-resource harvest order. The ordinary harvester manager respects the mission reservation, and ordinary stealth-harvester searches avoid accidental red collection. The coordinator waits until the harvester actually contains `RedTiberium`, then sends it to a real path-reachable, unreserved approach cell beside a configured high-value enemy structure. Targets are ranked by configured priority multiplied by economic value. Dead, captured, vanished, or unreachable resources and targets are released or deterministically re-evaluated.

## Design choices

- Added a dedicated configurable `RedTiberiumBombBotModule` and a small pure `RedTiberiumBombPolicy` rather than embedding the strategy in generic harvesting or squads.
- Used fractional integer launch credit, avoiding an unconditional one-unit-per-minute rounding floor for small economies.
- Reused the generic `IBotUnitReservations` seam so ordinary harvesting and other managers cannot steal an active bomber.
- Added a bot resource-policy seam plus an internal `HarvestUnstable` order. The explicit order carries its permission through save replay; ordinary player `Harvest` behavior is unchanged.
- Required an actual ground path around immovable blockers when selecting resource and target approach cells, while retaining cheap domain rejection and avoiding repeated A* work on every review.
- Kept existing harvester health, stealth, cargo, unstable timer, and explosion configuration unchanged.
- Persisted scan timing, fractional budget, and all live mission state through game saves.
- Logged bounded summaries and immediate launch, arm, route, arrival, retarget, rejection, and mission-end decisions.

## Cycles

1. Initial implementation; the fresh worktree required asset restore before compilation.
2. Strict build, six focused tests, and full CNC YAML validation passed.
3. Natural VIKI-versus-Brutalis match ran past tick 29,000 but produced no red cells or stealth harvesters; retained as unexercised regression evidence.
4. Focused full-engine fixture exposed ordinary stealth harvesters collecting red outside the 5% coordinator.
5. Resource-policy correction produced explicit real-red launches, distinct targets, and target destruction.
6. Created a mid-mission save with two live missions.
7. Save load failed because the ignored fixture map was not registered in the versioned user-map directory.
8. Registered map load reached the world but diverged at the first sync checkpoint.
9. Regenerated the save with the then-current binary and reproduced the feature normally.
10. Exact-current load still diverged, proving a real replay defect rather than stale evidence.
11. Created a control save with only the red-bomb coordinator removed.
12. The control loaded and completed, isolating the defect to dynamic resource permission.
13. Replaced dynamic permission with replayed explicit unstable-harvest semantics and created a fresh mid-mission save.
14. Save load resumed two active missions at tick 3,503 and completed naturally without desync.
15. Focused match passed launch/arming/detonation but exposed repeated retries toward a fact behind an immovable enclosure.
16. First real-path patch had a compile-scoping error.
17. Corrected patch passed strict compilation and both trait-interface checks.
18. First clean post-fix adversarial pass: multiple distinct real-red missions reached and destroyed facts, an eye, airfield, and resonator with zero stalled/idle retries.
19. Second clean pass used only four initial stealth harvesters. Fractional credit did not round up, first launch waited until tick 6,002 as the economy grew, and disappearing targets triggered successful re-evaluation before detonation.
20. Created a final exact-build save at tick 3,500 with two live missions.
21. Third clean pass loaded at tick 3,503, restored both missions and fractional budget, continued launching, and ended naturally without out-of-sync or fatal errors.
22. Five-bot Empire Lars stress match with two VIKIs and three Brutalis bots remained active and responsive beyond tick 30,000 after the seven-minute command wrapper expired; retained as stress evidence, not a natural-completion gate.
23. Limited accidental-red avoidance to stealth harvesters only; strict build, focused tests, and both interface checks passed.
24. First clean post-scope-fix pass: six launches used distinct active targets, destroyed multiple structures, re-evaluated one vanished target, and reached natural game over.
25. Second clean pass repeated the low-count fractional gate: the first launch waited until tick 6,002, a field casualty released normally, and a later bomber destroyed its fact.
26. Created an exact-final-build save at tick 3,500 with two live missions.
27. Third clean pass loaded at tick 3,503, restored both missions and budget, completed target destruction, launched further missions, and ended naturally without desync.
28. Final local gate passed: strict zero-warning Debug build, 334/334 tests, both interface checks, `git diff --check`, and exhaustive CNC YAML/map validation.

## Validation so far

- Strict Debug solution build passes with zero warnings/errors.
- All 334 unit tests pass, including six focused policy tests.
- Both explicit-interface validators and `git diff --check` pass.
- Focused maps use fully enabled ordinary VIKI and Brutalis stacks and reach natural game over.
- Literal missions harvested actual `RedTiberium`, armed only after cargo existed, used distinct targets/destinations, and visibly destroyed or severely damaged their targets.
- Low-count evidence proves fractional rather than rounded-up launch pacing.
- Real-path selection removed repeated retries against immovable enclosures.
- Final save/load restored active missions and budget without desync.
- Exhaustive CNC YAML/map validation passes, including Empire Lars and Archipelago.
- Commit/PR and green GitHub checks remain before completion.

## Boundaries

The map fixtures and generated logs remain under ignored `AUTONOMOUS-CNC-LOGS/CNC-31/`. The recurring invalid user `TibTest.oramap` cache warning predates this task and is unrelated. A large Empire Lars match can outlive the bounded automation wrapper; it demonstrated continued tick progress and bounded bot operation but is not counted as a natural full-match pass.
