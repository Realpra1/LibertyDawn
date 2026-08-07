# CNC-44 Task Report — Aircraft husks

## Status

`First iteration - testing`. Cycle 1 is implemented and RC1 integrated validation found no product repair to make. Type/capture, all-actor occupancy/removal timing, same-frame contention, A10 exclusion, and forbidden Rock terrain pass on the combined release code. CNC-62 remains unavailable and completion-blocking, so this is not a complete handoff.

## Behavior and design

- `SpawnActorOnDeath` has an explicit default-off `RequiresValidHuskCell` mode.
- The mode validates the configured target as a `Husk`, checks the target actor's standard `Husk.AllowedTerrain`, and inventories every exact-cell ActorMap influence at the final frame-end creation boundary.
- Dead actors do not block; any live influence blocks. Simultaneous attempts revalidate at creation, so the first created full-cell husk blocks later attempts.
- CNC rules opt in only `TRAN.Husk`, `HELI.Husk`, and `ORCA.Husk`, each targeting a distinct standard `^AircraftGroundHusk` actor with exact `TransformOnCapture` mapping. A10 and C17 are unchanged.
- Diagnostics are one bounded debug line per opted-in transition and include source/created actor IDs, exact cell, result, and rejection reason/blockers.

No crash damage, HP, armor, speed, timing, cost, production, AI priority, standard husk durability/decay, or restored-health policy was changed.

## Cycle 1 evidence

Old-control product SHA: `419bee2531d4802bf922c3597b42c6eeb75ab250` (worker pre-product HEAD `abc1011771`). Map SHA-256: `72e5c7d3fad5bcc5aeda991a3a9dfc919d1418674e73d77cac9f4b2e723b43ac`. Seed: 44001. Bots: ordinary SkyNet GDI and Brutalis Nod at headless MAX.

- Valid control: `analysis/worker-5-cnc44/baseline-control/run-004/`; temporary ORCA and A10 fall actors cleared, with zero live/restored aircraft and no durable spawn through tick 90.
- Valid changed: `analysis/worker-5-cnc44/cycle-01/run-001/`; `orca.husk#147` produced exactly one `orca.groundhusk#150` at clear cell 38,34 after removal. A10 stayed transient-only. Clean tick-90 exit with replay and benchmark artifacts.
- Invalid but useful control runs 001/002 exposed and corrected an A10 `AttackBomber` targetless harness crash and a premature natural-game-over test window; neither is acceptance evidence.
- Factual narratives: `analysis/worker-5-cnc44/commentary/control-batch-01/NARRATIVE.md` and `analysis/worker-5-cnc44/commentary/cycle-01-pair/NARRATIVE.md`. Routine policy review is not applicable.

Checks: Release build passed with zero warnings; `make test` passed CNC MiniYAML and map validation; focused `dotnet vstest` ran 5/5 eligibility cases successfully; publication `make check` passed its Debug build and interface checks with zero warnings/errors.

### Type, terrain, capture, and ordinary-module evidence

Seed 44002 used ordinary SkyNet GDI and Brutalis Nod AIs at headless MAX. The final harness SHA-256 was `0f34e7e4048102fb72a70e06aa89259c58c6c5173451982bb17f9f9e6f5832a9`.

- Run 002 safely rejected an ORCA durable husk on Rock terrain while still creating TRAN and HELI ground husks. This run was invalid for capture because the ORCA setup contradicted the standard `Husk.AllowedTerrain` contract.
- Runs 003/004 created exactly one mapped `TRAN.GroundHusk`, `HELI.GroundHusk`, and `ORCA.GroundHusk`, with zero A10 ground/transient remainder after its fall.
- Three ordinary E6 capture orders consumed all three Engineers, removed all three ground husks, and restored exact `TRAN`, `HELI`, and `ORCA` actors under Multi0 at the inherited 25% health policy: 2250/9000, 3750/15000, and 2125/8500.
- All three restored aircraft remained alive and accepted movement; the Apache and Orca were redirected by normal air-squad modules before the delayed assertion, providing module-adoption evidence but making their capture-time facing unverifiable. TRAN facing was verified. This batch is therefore useful evidence, not literal acceptance.
- All three runs reached tick 425 with exit code 0 and no fatal Lua/desync signal. Factual narrative: `analysis/worker-5-cnc44/commentary/cycle-01-type-capture/NARRATIVE.md`.

## RC1 integrated validation

Validated release product/receipt: `394ae5eeadfffbf58a9db7c1fac91960f5158cb6` / `ffb841b48750cc54b1862fb93101d3dce3a87a3f`. Repair branch: `agent/round-20260807-cnc44-rc1-repair`. No tracked product, configuration, or test repair was made, so integrated cycle use remains `0/3` for RC1 and `0/12` total.

Fourteen full-engine games are now counted overall. RC1 added seven games: three formally passing corrected scenarios and four invalid/useful predecessors that exposed only harness setup, geometry, or expected-pattern defects.

- Type/capture seed 44002, corrected map SHA-256 `bbd9f969c1a2b7b0550e55340cef20674c036baf74f1e3f5444fe8ffc08bfb59`: run 001 reproduced the late-facing harness defect while still proving all three aircraft moved under normal modules. Run 002 captured facing at the transform boundary and passed at tick 425. It created exactly one `TRAN.GroundHusk`, `HELI.GroundHusk`, and `ORCA.GroundHusk`, zero A10 result; consumed all three Engineers and husks; restored exact Multi0-owned types at 25% health with captured facing; and moved all three aircraft. Narrative: `analysis/worker-5-cnc44/commentary/integrated-type-capture/NARRATIVE.md`.
- Occupancy/contention seed 44003, final map SHA-256 `6caac82273ab4a8b9a3910070b848dd93cdcdd0c156eef00f92e5a5910372d4b`: run 001 stopped at tick 0 because the external map lacked `ScriptTriggers`; run 002 reached tick 140 and exposed a pre-existing `t17` in the intended adjacent cell. Corrected run 003 passed the exact `1/0/1/0/1/1/0` empty/live-vehicle/dead-stack/mixed/adjacent/simultaneous/A10 oracle. Four killed infantry were still `IsInWorld=true` at impact yet permitted one husk; a killed infantry plus live 35,000-HP tank rejected; the adjacent tank did not block; and same-frame TRAN created first while ORCA revalidated and rejected on that ground husk. Narrative: `analysis/worker-5-cnc44/commentary/integrated-occupancy/NARRATIVE.md`.
- Terrain seed 44004, map SHA-256 `23b74c5583bd067b25e4018bf05f5216c5a924f3dca29f35f600f24d64ab582b`: run 001 contained stale expected cell/assertion text but logged correct behavior. Corrected run 002 formally passed at tick 150: Orca rejected at Rock cell 50,30, TRAN and HELI created on valid terrain, and A10 remained absent. Narrative: `analysis/worker-5-cnc44/commentary/integrated-terrain/NARRATIVE.md`.

All valid RC1 games used ordinary SkyNet GDI and Brutalis Nod with normal modules enabled from tick 1 at headless MAX, exited cleanly, and produced replay/benchmark artifacts. No valid run logged a fatal or desync. Short focused throughput was 42.471, 27.966, and 14.991 ticks/s for materially different workloads, so no matched stress-regression claim is made.

Integrated gates passed under the shared large-build lock: `make test`; focused `AircraftHuskSpawnEligibilityTest` 5/5; `make check`; and `make check-scripts`. The final Debug build had zero warnings/errors. The focused test invocation repeated the already-recorded nonblocking CA1825 style warning in the test source.

## Dependency and remaining work

CNC-62 still has no local branch, remote branch, or GitHub PR as of the RC1 pre-test and pre-publication checks on 2026-08-07. It is not included in release RC1. Exact-cell crash damage and combined damage-before-eligibility evidence are therefore unavailable. Capture-time HELI/ORCA facing, all-actor dead/live occupancy, same-frame contention, and Rock terrain are now proven on RC1. Literal normal-combat source-aircraft crashes, CNC-62 ordering, map-edge/off-map handling, save/load, adversarial capture invalidation/manager competition, stress/natural endurance, and the fresh final regression remain. The result must remain `First iteration - testing`.

## Publication and review

- Product commit: `396c8106d9cec1c84ed0c2e44cd34ce0d0ef4772` on `agent/round-20260807-cnc44-aircraft-husks`.
- PR: `#85`, https://github.com/Realpra1/LibertyDawn/pull/85, targeting `agent/cnc-20260806-bug-polish-01-release`; it remains open and unmerged.
- RC1 validation receipt: draft repair PR `#91`, https://github.com/Realpra1/LibertyDawn/pull/91, targeting `agent/cnc-20260807-bug-polish-02-release`; it contains no product repair.
- GitHub checks: Linux and Windows .NET 6.0 passed at the product head. The final handoff-metadata-only successor is rechecked before return.
- Independent Sol-high final review: `analysis/worker-5-cnc44/final-review/REVIEW.md`; verdict `ready`, required fix `none`. The reviewer found no scoped correctness, regression, determinism, performance, scope, or diagnostic defect, while retaining the same CNC-62 and acceptance blockers.

The implementation is event-driven and bounded by actors influencing one exact cell. Ordered blocker diagnostics and all-actor evaluation make the decision deterministic; creation at the actual frame-end boundary makes an earlier same-frame created husk visible to a later attempt. Short matched evidence does not establish a stress/endurance performance conclusion, which remains open with the final portfolio.

## Deferred work

None. The targetless scripted A10 `AttackBomber` exception was a harness misuse and was avoided by instantiating its actual transient actor; no product change is proposed.
