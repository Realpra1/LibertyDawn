# CNC-107 Worker Report

## Status

- Proposed status: `Complete - testing`
- Cycle/model: `5 / Sol medium completed; 5/5 primary cycles used`
- Branch: `agent/round-20260812-cnc107-wall-repair`
- Head: `8b7b9c0b12` at PR publication; final receipt update follows
- Base: `4e12088061ac277c51de2e658dc0209337b80968`
- PR/CI: draft [#103](https://github.com/Realpra1/LibertyDawn/pull/103) open against `bleed`; GitHub CI started; local focused and prior full/build checks pass

## Cycle 5 — task-owner resolution and terminal no-route acceptance

- Task-owner policy supersedes the speccer/reviewer caveat: select non-blocked
  cells corners first, then closest to the Fact, until routing is impossible. A
  virtual-Engineer no-route after a required placement is an acceptable terminal
  outcome. No optimizer, reorder, geometry/routing redesign, or balance change is
  authorized.
- Product change: none. The existing implementation already follows the resolved
  literal order and bounded no-route behavior. Cycle 5 only reconciles the stale
  acceptance language in state/report and adds fresh acceptance evidence.
- Game 1 (`ANALYSIS/CYCLE-5/LIVE-GAMES/connected-final-inventory`) passed to tick
  7600 in 14.019 seconds. Both required corners routed from alternate origin
  `27,31`; after the cage/blockers cleared, `24,27`, `25,27`, and `24,31` were
  placed. Final inventory contains every missing intended cell and no authored
  access cell. The engine had no fatal/desync/unhandled error.
- Game 1 fresh analysis: `GAME-1-COMMENT/NARRATIVE.md` records the exact placement
  chronology and final inventory. `GAME-1-POLICY/POLICY-REVIEW.md` finds the wall
  behavior sensible and requests explicit bidirectional access-result telemetry.
  Disposition: reject that as a release blocker because the final inventory
  preserves all three access cells, the probe is logged outside the opening, and
  cycle-1 connected plus Archipelago evidence directly recorded traversal. Retain
  explicit access/terminal-result logging as advisory observability.
- Game 2 (`ANALYSIS/CYCLE-5/LIVE-GAMES/hostile-corner-first-terminal`) passed to
  tick 7600 in 19.025 seconds. Corner `28,19` was rank 1, had an exact route, and
  confirmed at tick 401. The farther legal cells `25,19`, `26,19`, and `27,19`
  then remained `routed=0`; work stopped boundedly at tick 7500 while three towers
  and ordinary defense activity continued. No optimizer/reorder occurred and the
  engine was clean.
- Game 2 fresh analysis: `GAME-2-COMMENT/NARRATIVE.md` records the exact bounded
  outcome. `GAME-2-POLICY/POLICY-REVIEW.md` judges corner-first followed by
  terminal no-route compliant with the resolved policy and recommends no behavior
  change; terminal/access-result logging is advisory.
- Checks: focused `ConstructionYardEnclosurePolicyTest` passed 25/25 and
  `git diff --check` passed. Cycle 4 full NUnit remained 574/574; prior Release
  `make all`, Debug `make check`, runner tests, and map validation remain passing.
- Decision: `Complete - testing`. The earlier sole-route result is no longer a
  blocker under the literal task-owner ruling.

## Cycle 4 — viable-origin fallback and sequential-order escalation

- Product change/commit: `9a587e9911` makes the pure enclosure policy deterministically try every currently viable authored access origin in distance/coordinate order, while the live planner still performs one bounded native exact-path search at a time. The first exact-valid route wins. No plan geometry, access width, queue ownership, save state, routing rules, cadence, cutoff, balance, or general movement behavior changed.
- Focused/full checks: the new disconnected-nearest/reachable-alternate test passed in `ConstructionYardEnclosurePolicyTest` (`25/25`); the full NUnit suite passed `574/574`; `git diff --check` passed. The first focused compile exposed only a test-local C# shadowed variable and was corrected before evidence.
- Game 1 (`ANALYSIS/CYCLE-4/LIVE-GAMES/connected-asymmetric-origin`): PASS for the cycle-4 access-origin correction, not complete literal acceptance. The full connected SkyNet-versus-Brutalis MAX game reached tick 7600 in 12.017 seconds. With nearest origin `25,31` isolated and `26,31` occupied, plans at ticks 1301/1655 used alternate origin `27,31` and actual walls at `28,27`/`28,31` confirmed at ticks 1655/2044. After the cage cleared, routing resumed from `25,31`; transient intended blockers remained eligible and both appeared in final inventory. The non-corner `25,27` remained absent, and no later exact route check established whether it was still reachable while serialized corner work occupied the queue, so this run does not prove every eligible cell completed. Final inventory preserved the authored access cells and the access probe exited. No fatal/desync/error occurred.
- Game 1 analysis: `GAME-1-COMMENT/NARRATIVE.md` and `GAME-1-POLICY/POLICY-REVIEW.md` pass with bounded confidence. Accepted recommendation: later final evidence should independently query every intended wall cell, verify all access cells remain enterable, and explicitly record the movement-probe result. No scratchpad update was proposed.
- Game 2 (`ANALYSIS/CYCLE-4/HOSTILE-R4/hostile-sole-route`): acceptance-critical conflict reproduced. The full hostile MAX game reached tick 7600 in 19.014 seconds and saved at tick 200. Before placement, the neutral probe traversed beyond the far top-row hole (`25,17`); the planner routed and confirmed required first corner `28,19` at tick 415. Immediately afterward `25,19`, `26,19`, and `27,19` remained explicitly placement-legal but `routed=0` through cutoff, while the post-corner probe remained inside at `27,22`. Power/refinery pressure, two Facts, defense work, access probing, enemies, and the tick-7500 stop continued without fatal/desync/error. The launcher summary's failure is only its over-specific expected pre-probe coordinate, not a startup or gameplay-evidence failure; earlier R1/R2 attempts are non-counting harness diagnosis and R3 first established the planner conflict before the neutral-probe repair.
- Game 2 analysis: `GAME-2-COMMENT/NARRATIVE.md` describes the observed behavior; `GAME-2-POLICY/POLICY-REVIEW.md` judges the sequential self-trapping conflict established with high confidence for this run and directs escalation. Accepted exactly: do not weaken corner-first ordering, add an optimizer, redesign geometry/access, or alter balance without a task-owner decision. No reviewer advice was rejected and the unrelated current CNC-98 scratchpad remains unchanged.
- Historical cycle-4 decision: `Needs help` pending a task-owner ruling. Cycle 5
  resolved it as described above without changing product behavior.

## 2026-08-12 environment-startup handoff

- This invocation performed the currently authorized handoff only. The durable cycle-3 evidence already shows both isolated launches timed out before world tick 1 while loading `cnc`/`modcontent`.
- No retry, implementation, build, game, commenter, policy review, or cycle advancement occurred. Request runtime-content/environment remediation against `ANALYSIS/CYCLE-3/LIVE-GAMES` before authorizing the next cycle-3 attempt.
- Follow-up contract check: the worktree remains clean at `2070518b8a`; the status and counters remain `Blocked - environment startup`, cycle `3`, and `2/5` primary cycles used. No further task action is authorized until that runtime remediation occurs.

## 2026-08-12 canonical runtime-content recovery

- Retried exactly the two cycle-3 adversarial manifests through this task worktree's launcher, reserving both game slots and passing `--content /root/github/LibertyDawn/.build/cnc33a/runtime-content`. Both reached tick 7600, so the earlier `cnc`/`modcontent` startup hang is resolved.
- `ANALYSIS/CYCLE-3/CANONICAL-RETRY/hostile-archipelago-regression` passed in 15.026 seconds with its tick-200 save and all required scenario/planner/confirmation patterns; no fatal, desync, or unhandled error appeared.
- `.../connected-valid-route-counterpart` reached tick 7600 in 12.02 seconds and had no fatal, desync, or unhandled error, but failed its contract because no `confirmed wall yard=` event occurred. Its planner logged candidate selection at ticks 3270 and 5794 without a matching confirmation, so it is not acceptance evidence.
- This is a consumed, mixed-evidence Terra cycle: status is now `First iteration - testing`, cycles used `3/5`, and the current cycle is 4. Obtain the mandatory Luna code review before any cycle-4 change; then investigate the connected no-confirmation evidence. No source, balance, or task scope change was made.

## 2026-08-13 mandatory cycle-3 Luna code-review handoff

- A fresh Luna-medium reviewer inspected the task diff at `2070518b8a` against `4e12088061ac277c51de2e658dc0209337b80968`; the durable review is `ANALYSIS/CYCLE-3/CODE-REVIEW.md`. No source, build, game, or cycle-counter action occurred in this invocation.
- Accepted major finding: `TryFindExactEnclosureRoute` probes only the nearest enterable authored access cell. Asymmetric terrain can disconnect that origin while another authored access cell still has an exact valid route, causing a false no-route result and indefinite bounded deferral.
- Cycle 4 must first deterministically try all viable access origins and accept the first exact-valid route, with a focused disconnected-nearest-origin fallback test. It must then investigate the connected no-confirmation evidence and run its two new adversarial games. No other blocking ordering, ownership, persistence, or CNC-46/CNC-52 defect was found.

Cycle 1 implements and validates the coherent initial solution. It is not proposed complete because save/load, exact cutoff/type-unavailable cases, an engineered sole-route sequential-placement trap, and final long connected/Archipelago regressions remain for later authorized cycles.

## Cycle 3 — blocked before world tick 1

- Product change: the debug-only stale pending-anchor event now says `deferred ... no production or placement issued`, making the bounded revalidation result unambiguous without changing any queue, route, geometry, cadence, cutoff, or balance behavior.
- Check: CNC Release compilation and map validation completed with zero warnings/errors; `git diff --check` passed.
- Invalid launches: both `ANALYSIS/CYCLE-3/LIVE-GAMES/connected-valid-route-counterpart` and `.../hostile-archipelago-regression` used the worktree launcher, installed `mods/modcontent`, unique support directories/displays, and the declared custom scenarios. Each remained at `Loading mod: cnc` / `Loading mod: modcontent`, then timed out after 120 seconds with world tick 0. The retained `batch-summary.json`, commands, console logs, and support logs are the complete startup evidence.
- Decision: no match narrative, policy review, control claim, save/load claim, or cycle advancement is valid. Environment/runtime-content help is required before another game attempt; the cycle-2 request for a deliberately injected post-selection blocker, valid-route counterpart, and matched old control remains outstanding.

## Cycle 2 — live pending-route revalidation

- Product change: pending first-Fact enclosure anchors now re-run the bounded exact native route check each time they are consumed for production or placement. A route that was valid at selection but becomes occupied/illegal is dropped and deferred to the ordinary bounded maintenance lifecycle; no routing, cadence, cutoff, geometry, access opening, queue ownership, wall type policy, or balance value changed.
- Focused test: `ConstructionYardEnclosurePolicyTest` passed `24/24`, including the new path-becomes-blocked-before-placement rejection. `git diff --check` passed.
- Game 1: `ANALYSIS/CYCLE-2/CYCLE-2-LIVE-GAMES/connected-live-route-revalidation` reached tick 7600 in 14.02 seconds with required CNC107 planner/confirmation patterns and no fatal/desync/error. The independent commenter and policy review found the log bundle insufficient to prove a blocker appeared specifically after selection and caused a defer; see `GAME-1-COMMENT/NARRATIVE.md` and `GAME-1-POLICY/POLICY-REVIEW.md`.
- Game 2: `ANALYSIS/CYCLE-2/CYCLE-2-LIVE-GAMES/hostile-pending-route-revalidation` reached tick 7600 in 17.025 seconds under hostile Archipelago pressure, emitted a valid tick-200 pending save, and had no fatal/desync/error. Its independent reviews reached the same insufficient-evidence verdict; see `GAME-2-COMMENT/NARRATIVE.md` and `GAME-2-POLICY/POLICY-REVIEW.md`.
- Decision: retain `First iteration - testing`. The next cycle must use a deliberately injected blocker between selection and issuance, preserve concise actor/route/revalidation/defer-or-issue evidence, pair it with a valid-route counterpart and an otherwise matched base control, and obtain the due cycle-3 Luna review before cycle 4. The reviewer recommendation is accepted; no policy recommendation is rejected.

## Cycle 2 launcher/evidence repair (not consumed)

- No product/config behavior changed, so cycle 2 is not consumed. The first two attempts were invalid because `launch-ai-parallel.py` defaulted to `/root/github/LibertyDawn/launch-game.sh`, executing the main checkout despite content staging. They remain diagnostic-only.
- `GAME-1-R3` explicitly used this task worktree's launcher plus the existing installed `cnc/` content. It reached tick 7600, emitted current `planned yard=` at tick 1 and wall confirmations at 410/809/1208, and produced the valid tick-200 save used below.
- Reload initially revealed a harness gap: the runner staged a save but not its custom map UID. The narrow task-local runner change adds manifest `support_maps`, copies declared maps to isolated `SupportDir/maps/cnc/{DEV_VERSION}`, and rejects missing paths. `tests/test_launch_ai_parallel.py` passed 4/4 and `py_compile` passed.
- `GAME-2-R4-RELOAD` then reached tick 7600. It restored the original `yard=210/fact@25,20` at tick 201, confirmed pending `28,19` at tick 463, then confirmed `25,19` and repaired `26,19`. It retained the permanent blocker `24,19`, preserved access, and released reservations at cutoff tick 7500. Both final batch summaries passed with no fatal/desync/error pattern.

## Behavior and design

- The immutable CNC-52 first-Fact plan, identity, access cells, cadence, cutoff, observed/issued state, pending wall type/cell, and exact queue ownership remain unchanged.
- Pure policy now returns missing legal destinations in deterministic corner-first order, then squared distance to the bound Fact location, then saved plan index and coordinates.
- The wall planner tests those ordered candidates with bounded native `PathSearch` work using the configured wall locomotor. It deterministically exhausts the small authored access-cell set until the first exact-valid route, uses strict current actor occupancy, `BlockedByActor.All`, a 3,000-expansion bound per origin, exact destination-to-source endpoint/order validation, contiguous path steps, and explicit Fact-footprint rejection. It creates no actor, capture/transport state, resource demand, or serialized route state.
- One routed destination is owned at a time. Enclosure placement now uses individual `PlaceBuilding`, preventing a one-anchor `LineBuild` from extending across the authored access opening. Existing placement-time legality and exact queue/type owner checks remain in force.
- The configured wall preference is resolved from current buildability in authored order (`brik`, `sbag`, `cycl`). An unavailable pending unowned type is safely replanned; no available configured type causes no queue request.
- No balance, prerequisites, geometry, access width, wall cap, cadence, cutoff, cash, stats, probabilities, or general routing behavior changed. Save schema stayed at version 3 because no durable state shape changed.

## Focused evidence and checks

- Established a failing focused compile/control before implementation for the new ordering and exact-route seams.
- Added policy checks for corner/nearest/stable plan order, blocker retention without plan mutation, reversed endpoint rejection, occupied/Fact-crossing path rejection, and wall-type fallback/no-type.
- Focused policy suite: `23/23` passed.
- Full NUnit suite: `572/572` passed.
- `make check`: passed under the shared large-build lock; Debug build had zero warnings/errors and interface checks passed.
- Release `make all`: passed under the shared large-build lock with zero warnings/errors.
- `git diff --check`: passed.

## Game 1 — connected matched blocker/control

- Hypothesis: blockers on early corners must not strand independent destinations; reachable corners must precede the non-corner; cleared cells must remain eligible; the access gap must stay open.
- Changed artifacts: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260812-bug-polish-04/WORKER-3-CNC-107/ANALYSIS/CYCLE-1/CHANGED-GAMES-R6/connected-changed`
- Matched base-control artifacts: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260812-bug-polish-04/WORKER-3-CNC-107/ANALYSIS/CYCLE-1/CONTROL-GAME/connected-control`
- Both exact-seed runs loaded the custom map with SkyNet GDI versus Brutalis Nod and reached tick 7600.
- Changed selection/confirmation order was reachable corners `28,27`, `28,31`, then non-corner `25,27`, followed by retained blockers `24,27` and `24,31` after their tick-2400/tick-4000 clears and later ordinary occupancy. All 13 intended cells had actual own sandbags by tick 6925; `missing=0` persisted to cutoff. The access probe moved out and all three access cells remained unwalled.
- Base control selected non-corner `25,27` before the corners. Its single-anchor `LineBuild` at `24,31` ultimately created walls in all three access cells `25,31`, `26,31`, and `27,31`, leaving 13 intended plus three forbidden access walls. The setup therefore materially reproduced both wrong order and access-seal failure.
- No duplicate confirmations, fatal errors, desyncs, timeouts, or post-cutoff work appeared. Ordinary construction, field/harvester activity, capture/combat, and the second Fact continued.
- Match commenter: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260812-bug-polish-04/WORKER-3-CNC-107/ANALYSIS/CYCLE-1/GAME-1/NARRATIVE.md` — changed run completed all intended cells in required order and preserved access; control did neither.
- Policy review: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260812-bug-polish-04/WORKER-3-CNC-107/ANALYSIS/CYCLE-1/GAME-1/POLICY-REVIEW.md` — PASS, high confidence; no scratchpad replacement.

## Game 2 — hostile Archipelago competition

- Hypothesis: corner-first routed placement must not self-trap a later legal hole, a permanent blocker must remain bounded, a destroyed wall must repair, and wall work must coexist with power loss, low cash, two Facts, defense towers, and enemy pressure.
- Artifacts: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260812-bug-polish-04/WORKER-3-CNC-107/ANALYSIS/CYCLE-1/CHANGED-GAMES-R6/hostile-changed`
- The custom Archipelago game loaded SkyNet GDI versus Brutalis Nod and reached tick 7600. The planner stayed bound to original `fact#210@25,20`, not the later Facts.
- Reachable corner `28,19` confirmed first, later legal hole `25,19` confirmed next, and scripted destroyed wall `26,19` was re-confirmed as a repair. Native routes remained available after the corner; this geometry did not reproduce sequential self-trapping.
- Final inventory contained every independently possible intended cell. Only `24,19` remained absent, repeatedly and correctly reported as occupied by the permanent friendly blocker with `missing=1 legal=0 routed=0`; no request churn followed.
- The three-cell access stayed open and a unit traversed it. Power destruction and zero-cash periods, three live towers, defense queues/centers, ordinary construction, two additional Facts, and enemy presence continued without duplicate ownership, crash, timeout, or post-cutoff work.
- Match commenter: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260812-bug-polish-04/WORKER-3-CNC-107/ANALYSIS/CYCLE-1/GAME-2/NARRATIVE.md` — all independently possible cells filled, destroyed wall repaired, permanent blocker bounded, first-Fact/access preserved.
- Policy review: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260812-bug-polish-04/WORKER-3-CNC-107/ANALYSIS/CYCLE-1/GAME-2/POLICY-REVIEW.md` — PASS, confidence 0.94; no scratchpad replacement.

## Diagnostics and performance

- Publication retains concise owning-boundary summaries: bind/stop, candidate rank, route endpoints/path-cell count, wall type, exact queue owner, individual placement, confirmation latency, missing/legal/routed counts, retry/cutoff reason. No expanded-node or exact-path dump remains.
- Matched connected benchmark, unique ticks 101–7600: changed mean tick time `0.829 ms`, control `0.779 ms`; maxima `214.17 ms` versus `218.13 ms`. Mean bot tick was `0.429 ms` versus `0.354 ms` (absolute `+0.075 ms`); maxima `38.21 ms` versus `33.91 ms`. Changed wall routing ran only on small-perimeter queue/maintenance decisions, not per tick. The absolute difference is small and no latency spike regression appeared, but later final regression should repeat a single-process matched benchmark because changed and control were launched under different ambient contention.
- Wall-clock to tick 7600 was 12.019 seconds for changed Game 1 and 20.03 seconds for control; this is recorded but not treated as a controlled CPU claim because the runs had different concurrent system load.

## Policy recommendation disposition

- Resolved: the hostile sole-route test confirmed that a required corner can end
  farther routing. The task owner accepts that bounded terminal result and forbids
  weakening corner-first or inventing an optimizer.
- Also carry save/load with a pending individual destination and blocker across reload into the next persisted test. This is compatible with, and strengthens, the reviewers' recommendation.
- The cycle-5 Game-1 request for explicit bidirectional access-result telemetry is
  retained as advisory but rejected as a release blocker based on preserved final
  access inventory and prior direct traversal evidence. The Game-2 recommendation
  to retain absolute corner-first and bounded no-route is accepted. A valid
  1,684-character CNC-107 scratchpad replacement was promoted under the one-slot
  lock; no later compatible task-scoped replacement was adopted.

## Deferred work and known risks

- Explicit terminal-reason and bidirectional access-result telemetry remain useful
  observability improvements, not behavior blockers.
- Additional no-type/cap/original-Fact-loss/stale-save permutations and a matched
  single-process allocation benchmark remain advisory. The pending save/reload,
  cutoff, fallback, ownership, stable order, and plan validation already have
  focused or full-engine evidence.
- Independent final review and integration remain release-process steps after task
  PR #103; GitHub CI started at publication.
- The nearest metric is squared distance to the saved bound Fact location. This is deterministic and matches the packet wording/test evidence, but later adversarial tests should retain equal-distance ties to guard the interpretation.
- The path probe is intentionally strict: any live actor in a path cell blocks acceptance. That satisfies the literal no-occupied-path rule but may defer routes that a movable ally could eventually vacate; cadence retries are the intended recovery.

## Cycle journal

| Cycle/model | Commit/change | Game 1 | Game 2 | Checks | Decision |
|---|---|---|---|---|---|
| 1 / Sol high | Ordered one-cell routed selection, individual enclosure placement, buildable wall fallback | PASS; changed corner-first/all 13/access open; control non-corner first/access sealed | PASS; 12 possible cells present, permanent blocker bounded, destroyed wall repaired, no self-trap in tested geometry | 23 focused, 572 full, Release build, `make check`, diff check | `First iteration - testing`; carry sole-route + save/load recommendation to cycle 2 |
| 2 / Terra medium (setup repair; not consumed) | Task-worktree engine override and declared isolated support-map staging for save reloads; no game product/config behavior change | PASS; R3 tick-200 save run loaded current planner and reached tick 7600 | PASS; R4 reload restored pending ownership/Fact and completed remaining eligible cells to tick 7600 | `py_compile`; launcher tests 4/4; both engine summaries passed | Runtime setup resolved; next invocation must begin the still-unconsumed product-change cycle 2 |
| 2 / Terra medium | Exact native route is revalidated before a pending enclosure endpoint is consumed | PASS harness; connected MAX to tick 7600, but reviewer evidence verdict insufficient | PASS harness; hostile MAX/pending save to tick 7600, but reviewer evidence verdict insufficient | Focused 24/24; diff check; two engine summaries | Record accepted evidence recommendation; cycle 3 must construct the stale-route trigger/control and complete its due review |
| 3 / Terra medium (not consumed) | Debug-only stale-anchor defer wording | INVALID; timed out loading mods, tick 0 | INVALID; timed out loading mods, tick 0 | Release compile/map validation; diff check | `Blocked - environment startup`; request runtime help and do not claim game evidence |
| 3 / Terra medium | Canonical runtime-content retry; no further source change | MIXED; connected reached tick 7600 but no wall confirmation | PASS; hostile Archipelago reached tick 7600 with save and confirmations | Both valid full-engine runs; mandatory Luna review completed afterward | Cycle consumed; review found nearest-origin false rejection and authorized cycle-4 correction |
| 4 / Terra medium | `9a587e9911`: deterministically exhaust viable access origins | PASS for origin fallback, incomplete literal acceptance; alternate `27,31` routed/confirmed while nearest was isolated, both transient blockers repaired, but `25,27` remained absent without a later exact reachability observation | FAIL/ESCALATE; corner `28,19` confirmed, then three farther legal cells became unrouted through cutoff | Focused 25/25; full 574/574; diff check; per-game Luna commenter/policy reviews | `Needs help`; task owner must resolve literal-order versus route-preservation conflict before cycle 5 |
| 5 / Sol medium | No product change; reconcile task-owner absolute corner-first/terminal-no-route ruling | PASS; all five missing cells present in final inventory, alternate-origin fallback and transient recovery confirmed, access cells unwalled | PASS; required corner confirmed first, three farther legal cells bounded at `routed=0` through cutoff | Focused 25/25; diff check; two fresh full-engine games and separate Luna commenter/policy reviews | `Complete - testing`; no optimizer/reorder/geometry/routing/balance change |
