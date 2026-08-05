# CNC-33A.2: Independent Tiberium explosion options

- Status: first iteration
- Cycles used: 30 of 30
- Branch: `agent/cnc33a2-tiberium-explosion-options`
- Base: green CNC-33A.1 PR #65 head `a302f673be`
- Pull request: draft PR #67, mergeable; implementation/publication head passed Linux and Windows CI

## Literal acceptance

In a full-engine match, the host can independently enable `No red Tiberium explosions` and `No blue Tiberium explosions`. With neither option enabled, existing behavior remains unchanged. With either option enabled, every explosion attributable to that color is suppressed for the match, including resource fields, loaded cargo, actor traits, weapons, scripts, and delayed warheads. The authoritative setting comes from the live lobby/world state and is independent of actor, map, owner, and order source. Ordinary non-Tiberium weapons and deaths must remain active.

The black-box gate uses otherwise identical red-tagged, blue-tagged, actor-trait-tagged, and ordinary explosions. Each disabled color must leave its target intact while the other color and ordinary explosion retain normal damage. Field damage must reach the semantic resource gate; loaded harvesters must reach the payload-aware gate. Red/blue suppression takes precedence over later no-mutants substitution, which belongs to CNC-33A.3.

Forbidden outcomes are actor/map-name special cases, visual-only suppression, green or ordinary explosion suppression, delayed warheads bypassing the option, behavior changing after capture, release debug spam, or claiming save/load and cargo coverage without observable engine evidence.

## Contention and source inventory

- Resource-layer damage and max-stage instability, including scheduling and delayed execution.
- Generic loaded-harvester death explosions, unstable red HARV/SHARV traits, red bomb-truck AI and player/script death or deploy sources.
- Direct/projectile/delayed weapon impacts, actor death traits, Lua weapon calls, ownership changes, and ordinary non-Tiberium deaths.
- Save/load, lobby/replay settings, normal harvesting, attack, repair, squad, reservation, and complete-match teardown.

## Implementation

- Added a reusable semantic `IImpactTypeSuppressor` boundary and two default-off world lobby options. Release `DebugLogging` defaults to false; ignored evidence maps opt in.
- Tagged dedicated red/blue Tiberium weapons. `WeaponInfo` rejects tagged impacts before immediate warheads and `DelayedImpact` rechecks at actual execution.
- Added `ExplosionImpactType` to resource types and gates at damage scheduling, delayed execution, and final explosion. Suppressed max-stage fields remain stable instead of continually rearming.
- Added actor-level and loaded-resource mappings to `Explodes`. Mixed cargo still explodes when any positive payload color remains enabled; it is suppressed only when every loaded mapped color is suppressed.
- HARV and SHARV map blue/red cargo semantically. Their unstable red traits are tagged separately; SHARV's shared ordinary `Atomic` weapon remains untagged so normal nuclear strikes are unaffected.
- Added focused independent-option and mixed-payload policy tests.

## Evidence cycles

- Cycles 1-2 found two strict-style precedence errors, corrected them, then passed the strict Debug solution build with zero warnings/errors.
- Cycle 3 passed 10 focused red/blue option-matrix and mixed-payload tests. Cycle 4 passed exhaustive CNC rules, sequences, and shipped-map validation. Cycle 5 passed all 368 tests plus explicit and conditional trait-interface checks. Cycle 6 repeated the strict build after adding optional instrumentation.
- Cycles 7-16 iterated an ignored full-engine fixture. They exposed invalid abstract/direct inheritance, missing Lua wiring, and over-stripped inherited JEEP traits; none are counted as acceptance.
- Cycle 17 passed an ordinary SkyNet-versus-Brutalis headless MAX default-on smoke through tick 500: red, blue, and red actor-trait damage remained active and ordinary `Atomic` damage was unchanged.
- Cycles 18-21 exercised all four lobby combinations in full-engine ordinary-bot games. Secondary effects made target health noisy, but semantic decisions were correct and independent.
- Cycles 22-25 replaced those weapons with isolated matched impacts. All four combinations passed exactly: default damaged all four targets from 1,000,000 to 940,000; no-red preserved red weapon/trait targets; no-blue preserved only blue; both preserved red, blue, and red-trait targets while ordinary damage remained 940,000. Each run loaded SkyNet and Brutalis, advanced to tick 500, and flushed a replay without fatal/desync errors. Evidence: `.build/cnc33a2/evidence/cycle22-25-option-matrix-isolated-weapons/`.
- Cycles 26-27 were invalid field/cargo attempts: the fixture's resource-damage warhead excluded ground and the selected cells had no resources. The harness incorrectly reported pass based only on log presence; manual health/log inspection rejected both.
- Cycle 28 created a clean tick-300 game save with both options enabled, reached tick 350, preserved the isolated red/blue targets, and retained ordinary damage. Its first field trigger still did not occur because a newly created actor was killed in the same Lua callback.
- Cycle 29 correctly exposed the isolated reload harness gap: the fresh support directory could not resolve ignored map UID `95966335bf49598efdebf58a1623916620021d80`, so it stopped at tick 0. The save exists, but load persistence is not claimed.
- Cycle 30 corrected actor timing and reached natural game over at tick 45,000 in about 70 seconds with ordinary SkyNet and Brutalis. Red and blue resource-field gates fired at tick 136; red carried-cargo suppression fired at tick 451; both colors were repeatedly suppressed during normal play; ordinary damage, benchmark output, and replay remained active with no fatal/desync error. The harness classified the cycle failed because its cargo target expected zero total damage even though an ordinary actor death explosion remained, and blue cargo did not produce the intended tick-451 semantic signal. Evidence: `.build/cnc33a2/evidence/cycle30-natural-final/`.

## Result and remaining risks

The independent lobby matrix, common weapon boundary, field boundary, red cargo path, ordinary-explosion regression, release-quiet default, real bots, replay output, and natural complete match have strong evidence. The 30-cycle cap was reached without a successful isolated reload or a clean blue loaded-cargo observable, so this is deliberately published as `first iteration`, not `complete`. A future correction should stage the exact ignored map UID before reload and remove every unrelated death trait from the cargo fixture (or assert semantic tick events rather than total target health), then revalidate both loaded colors and save/load without changing the implementation unless those tests expose a product defect.

Draft PR #67 is mergeable. Exact implementation/publication head `68160a187d` passed Linux CI in 2m08s and Windows CI in 3m29s.
