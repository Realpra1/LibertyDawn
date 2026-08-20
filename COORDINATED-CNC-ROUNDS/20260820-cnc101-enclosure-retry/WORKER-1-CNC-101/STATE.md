# Worker State: CNC-101

## Assignment

- Worker: `WORKER-1`; task: `CNC-101`
- Worker branch supplied by the coordinator: `agent/20260820-cnc101-enclosure-retry`
- Worker worktree supplied by the coordinator: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260820-cnc101-enclosure-retry/worker`
- Exact base: `54f84f580461123dfca9a3cdfe8cf9a62d90188d` (`origin/bleed`)
- Balance is frozen; no architecture, unrelated behavior, or tuning changes are authorized.

## Literal acceptance

The initial MCV enclosure phase must retry missing or terrain-constrained wall placements up to eight times before allowing normal secondary-queue construction. There is no global or per-player wall-count cap; no cap may block initial enclosure closure or later wall construction. In exactly two distinct ordinary-AI/all-module custom games, each capped at 120 seconds, the enclosure must complete in about 30 seconds or less, including a terrain-constrained-hole game. Each game must directly timestamp enclosure completion and prove that Silo, first configured defense, and other normal secondary construction do not preempt the initial retry phase. After enclosure completion, the observable order must remain Silo, first configured defense, then normal construction. Retries must be bounded and must not create duplicate/unbounded orders.

## Required checks and evidence

- Focused enclosure/wall-planner tests covering missing placements, terrain holes, eight-retry bounds, no duplicate orders, no global/per-player wall cap in initial and later construction, and post-enclosure queue release.
- Protected repository checks, CNC YAML validation, scenario/map validation, and `git diff --check`.
- Exactly two fresh full-engine games only: ordinary AI with all modules enabled, one terrain-constrained-hole adversarial map and one distinct control/adversarial map. Preserve concise timestamps, retry/queue observations, crashes/desyncs, and paths in the task report; keep raw artifacts out of Git.

## Cycle-1 receipt

- Status: `Complete - testing`.
- Root cause: secondary-queue release was coupled to a global four-wall count instead of the bound first Construction Yard's complete 16-cell enclosure, and there was no persisted bounded initial-retry phase. Competing queue polls also needed to avoid consuming retries while a placement confirmation was still in flight.
- Change: the first-yard enclosure now remains the secondary-queue gate until every perimeter cell is closed by an owned wall or impassable terrain, or until a persisted retry counter saturates at eight. Issued-cell retries occur only at the maintenance interval. Save state is versioned compatibly, and the existing post-release Silo/configured-defense/normal order is retained.
- Focused tests: 45 passed, 0 failed. Protected `make check`: passed with 0 warnings and 0 errors. Full CNC YAML validation, both custom-map validations, and `git diff --check`: passed.
- Qualifying Game 1, terrain hole: Brutalis GDI versus VIKI Nod, 30.08 seconds, tick 9000, retries 5/8, product release tick 821, 15 walls plus one water-sealed perimeter cell complete tick 825 (20.625 seconds), then Silo/configured defense/normal secondary at ticks 4825/6270/6295. No failure, retry-limit release, desync, or fatal error.
- Qualifying Game 2, clear pressured control: Brutalis Nod versus VIKI GDI, 32.057 seconds, tick 9000, retries 7/8, product release tick 296, all 16 walls complete tick 300 (7.5 seconds), then Silo/configured defense/normal secondary at ticks 5520/6705/7575. No failure, retry-limit release, desync, or fatal error.
- Separate Luna narration and policy review completed for each qualifying game. Game 1's required follow-up for a materially different layout was exercised by Game 2 and resolved. Game 2's remaining recommendation is advisory only: keep conclusions bounded to the two tested geometries; no additional game or code change is required in this exact-two cycle.
- Detailed receipt and ignored artifact paths: `REPORT.md`.

## Cycle-2 reviewer correction

- Starting head: `47979e1dfac5d6900dc80e7d983a4e3e965970a6` (clean).
- Terra correction: unresolved initial-enclosure maintenance must not bypass the persisted bounded gate through the enclosure cutoff or wall-cap paths. `EnsureEnclosureState` currently stops at cutoff, `EnclosureActive` depends on `worldTick < cutoffTick`, and the wall-cap branch returns before retry accounting.
- Required outcome after user amendment: cutoff outcomes route through the persisted maximum-eight retry accounting; the global/per-player wall-cap policy is removed entirely. Ordinary secondary construction releases only after physical/terrain closure or explicit retry-limit exhaustion.
- Required evidence after user amendment: focused cutoff/no-cap regressions, protected checks, and exactly two new distinct ordinary-AI/all-module games at most 120 seconds with separate fresh Luna narration/policy. The games must prove cutoff cannot bypass the gate and wall construction can exceed the former cap without premature secondary construction.
- Cycle-2 status: `Complete - testing`.

### User amendment

- Remove the `MaximumWallSegments` wall-cap policy completely from production configuration, code, and tests; there must be no global/player wall cap.
- The interrupted cap-specific Game 2 is uncounted and may not be used as acceptance evidence.
- Preserve the cutoff-bypass correction and persisted maximum-eight initial-enclosure retry gate.
- Replacement Game 2 must be a distinct ordinary-AI/all-module terrain/blocker scenario with no global wall cap. It must prove wall construction can exceed the former configured cap where required, preserve bounded retries/no preemption, reach physical/terrain closure within 30 seconds or explicit 8/8 release, and then continue Silo/configured-defense/normal secondary construction.
- The worker will not edit the task sheet; any task-sheet amendment is routed by the coordinator through the authorized Task Maker.

## Cycle-2 receipt

- Authorized Task Maker amendment `d2c4640327` was incorporated without an intermediate worker commit. It changed only the CNC-101 task entry and this state acceptance to require no global/per-player wall cap.
- Production correction: unresolved initial enclosure work remains active across cutoff and consumes persisted retries to explicit 8/8 release. `MaximumWallSegments` was removed from `BaseBuilderBotModuleInfo`, all planner branches/signatures, and all nine CNC AI configurations.
- Focused tests: 46 passed, 0 failed. Protected `make check`: passed with 0 warnings and 0 errors. Full CNC YAML, scenario/map/Lua validations, and `git diff --check`: passed.
- Qualifying Game 1, final-code cutoff: no cap setting; all 16 cells blocked; cutoff tick 4 remained pending; explicit 8/8 release tick 8; no preemption; Silo/configured defense/normal at ticks 4577/6032/6059; exit 0 at tick 9000 in 40.035 seconds.
- Qualifying replacement Game 2, no cap: 24 prior walls, one terrain-sealed ring cell and 15 temporarily blocked cells; first new wall exceeded the former cap at tick 80; total 39 walls and physical closure tick 656 (16.4 seconds) with retries 6/8; no preemption; Silo/configured defense/normal at ticks 5600/6728/8114; exit 0 at tick 9000 in 35.081 seconds.
- Separate Luna narration and policy reviews passed for both qualifying games. Both highest-priority recommendations were advisory only; their bounded-scope wording is recorded in `REPORT.md` and no extra game/code change is required.
- The pre-amendment cutoff run, interrupted cap-specific run, and one-tick-maintenance replacement calibration are explicitly superseded/uncounted and excluded from final acceptance.

## Cycle-3 reviewer correction

- Starting head: `9ca432f4d06e8b83b7e5dfb915ad80e572984aee` (clean).
- Terra correction: unavailable-cell retries remain queue-poll-aged because `NextEnclosureScanTick` reduces pending scans to one tick and ordinary no-legal/cutoff-unavailable branches increment at every scan.
- Required correction: persist or deterministically derive a next-retry timestamp and increment unavailable/cutoff retries no more than once per configured `ConstructionYardEnclosureMaintenanceInterval`, including under competing queue polls.
- Preserve: no wall cap, per-Fact physical/terrain closure, maximum eight retries, cutoff gating, save/load determinism, and post-release Silo/configured-defense/normal construction order.
- Required evidence: interval-250 and competing-poll focused regressions, protected checks, and exactly two new distinct ordinary-AI/all-module games at most 120 seconds with separate fresh Luna narration/policy. Both games use the normal maintenance interval and directly timestamp maintenance-aged retries, no preemption, physical closure within 30 seconds where achievable or explicit bounded behavior, and later construction continuation/order.
- Cycle-3 status: `Complete - testing`.

## Cycle-3 receipt

- Production correction: the planner persists `NextInitialRetryTick` and funnels unavailable-cell, cutoff-unavailable, and due issued-cell outcomes through one maintenance-aged retry consumer. Competing one-tick queue polls cannot consume again before the configured interval. Save data advances compatibly to version 5; versions 2-4 default the new schedule to immediate eligibility, future scheduled ticks are valid, and negative state is rejected.
- Focused enclosure/opening tests: 50 passed, 0 failed, including interval-250/four-competing-polls coverage at ticks 1 and 251. Protected `make check` passed with 0 warnings and 0 errors. Full CNC YAML, both custom-map YAML/Lua validations, and `git diff --check` passed.
- Qualifying Game 1, recoverable terrain/no-cap pressure: ordinary all-module Brutalis/Nod versus VIKI/GDI; retry 1/8 tick 1 scheduled tick 251; blockers cleared tick 5 without retry 2; first plan tick 251; 25 walls tick 323; terrain-sealed physical closure at tick 902 (22.55 seconds) with 39 walls and release at 1/8; Silo/defense/normal ticks 5546/7265/7439; exit 0 at tick 9000 in 36.088 seconds.
- Qualifying Game 2, impossible geometry plus cutoff: ordinary all-module Brutalis/GDI versus VIKI/Nod; retries exactly ticks 1/251/501/751/1001/1251/1501/1751; explicit `retry limit reached` release at 8/8 tick 1751; Silo/defense/normal ticks 4898/6374/6407; exit 0 at tick 9000 in 34.127 seconds.
- Separate fresh Luna narration and policy reviews completed for each qualifying game. Both highest-priority recommendations were `KEEP`; accepted without further product or game changes, with claims bounded to the two tested scenarios.
- The first Game-2 launcher invocation failed before engine startup because of an invalid output option. It is disclosed as an uncounted setup failure; the corrected unchanged launch is the qualifying run.
- Detailed receipt and ignored artifact paths: `REPORT.md`.
