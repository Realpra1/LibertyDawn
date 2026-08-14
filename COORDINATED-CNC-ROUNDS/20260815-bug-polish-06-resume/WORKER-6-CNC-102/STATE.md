# Worker State: CNC-102

## Assignment

- Worker: `WORKER-6`
- Task: `CNC-102 — Resonator fancy-placement fallback`
- Status: `Second iteration - testing handoff after cycle 2`
- Base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Task branch: `agent/round-20260815-cnc102-resonator-fallback`
- PR base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Cycle: `2` (completed; next authorized cycle is 3)
- Balance: frozen
- PR: none

## Literal scope

Keep CNC-41's fancy enclosed per-tree economy-field geometry and behavior
unchanged. Continue trying fancy placement. Start the fallback timer only when
the Resonator is fully built and ready to place. If 60 in-game seconds pass
without a legal fancy placement, place that waiting Resonator using the old
refinery-near-any-Tiberium fallback. Friendly SAM placement/coverage is
optional and low priority: it may happen whenever legal, but must never delay
fancy placement, the 60-second timer, or the old-behavior fallback. Do not
redesign fancy enclosure/geometry,
construction policy, balance, or Tiberium rules. The primary failure is that
Brutalis and Economy-branch Iron Reaper never build/place Resonators.

## Minimal scenarios and acceptance

Use two distinct custom full-engine scenarios, never manager-only fixtures, each
<=120 seconds, with ordinary CNC AIs and all normal modules enabled:

1. Block fancy placement for Brutalis and Economy Iron Reaper with a ready
   Resonator, proving the timer does not begin before readiness and that after
   60 in-game seconds the old simple placement occurs near any available blue,
   green, or red Tiberium, preferably in SAM coverage.
2. Provide a legal fancy opportunity plus a save/load boundary and a distinct
   blocked field control. Prove fancy attempts continue before timeout, the
   simple fallback does not fire early or duplicate, and a post-timeout
   Resonator is placed without changing enclosure geometry.

Acceptance is successful ready-only timeout fallback, continued fancy behavior
before timeout, no duplicate/stalled ready Resonator, and no balance or geometry
change. Run focused placement/timer tests, YAML validation, syntax, and
`git diff --check`; do not add broad natural-match, performance, or policy-review
gates.

## Selective port dependency

The active predecessor is CNC-41's existing contract; inspect only task-faithful
commits supplied by the coordinator. Do not wholesale-merge another worker's
state, report, evidence, or process metadata. Keep changes on this task branch.

## Handoff

Do not edit the task sheet or coordinator state, push `bleed`, or merge a PR.

## Cycle journal

| Cycle | Commit/change | Failure hypothesis and perturbation | Checks/games | Failure/pass evidence | Decision/next harder test |
|---|---|---|---|---|---|
| 1 | Selectively ported the CNC-41 field manager and added a persisted ready-only 1500-tick placement deadline. Fancy placement remains first; after the deadline only, the waiting Resonator uses the existing refinery/resource-field locator, with non-blocking SAM preference. Added focused policy coverage and a reproducible two-map generator. | A completed Resonator can retain an obstructed fancy site forever. Custom Empire Earth scenarios used ordinary Brutalis, Economy Iron Reaper, and distant Skynet with normal modules; ordinary map actors preserved the selected fancy footprint and a delayed ordinary wall was the intended obstruction. | Warnings-as-errors affected-module build passed. Focused `TiberiumFieldPolicyTest`: 17/17. Generated-map and global CNC YAML, Python syntax, and `git diff --check` passed. Fresh two-map save batch passed 2/2 to tick 2000. Blocked reload passed to tick 4500; fancy reload passed serially to tick 4500 after one excluded concurrent Lua-wrapper file-contention failure. Calibration run reached tick 6500. Artifacts: `.worktrees/cnc102-cycle1/fresh-scenarios-run/`, `.worktrees/cnc102-cycle1/reload-scenarios-run-2/blocked-ready-reload/`, `.worktrees/cnc102-cycle1/fancy-reload-serial-run/`, and `.worktrees/cnc102-cycle1/discovery-run-9/`. | Both projects restored at tick 1500 with `ready-placement-deadline=0`; no ready-timer or fallback marker fired before readiness. However neither AI reserved or produced a Resonator. Ordinary opening/queue activity persisted until opening completion, then admission repeatedly failed on protected cash (Brutalis showed cash 0 at tick 3274) or remained queue-preempted. The intended ready obstruction, 60-second fallback, legal fancy placement, no duplicate, and post-timeout placement were therefore not exercised. | Preserve the scoped ready-only implementation but do not claim acceptance. Next cycle should create a production-faithful scenario in which the normal AI reaches a ready Resonator without overriding policy timings or state, then exercise delayed obstruction and the distinct legal/save-load control. |
| 2 | Supplied a sustained scenario economy, separated the ordinary bots onto bot-specific non-red field actors, and moved optional economy-SAM reservation below waiting field-project ownership. | Cycle-1 production starvation came from exhausted cash and queue preemption; optional SAM ownership also preceded the field project despite the literal low-priority requirement. Iterated delayed obstruction and build-area anchors without changing manager timings or injecting state. | Warnings-as-errors affected build passed. Focused `TiberiumFieldPolicyTest` 17/17, CNC YAML, Python syntax, and `git diff --check` passed. Calibration naturally reserved Brutalis at tick 1649 and started readiness at 2578. One evidence-only blocked run started readiness at 3193 and issued simple fallback at 4696 after deadline 4693, but exceeded the 120-second ceiling by 0.4 seconds. Multiple final two-map attempts and three seed probes are retained under `.worktrees/cnc102-cycle2/`. | The real ready item retained until deadline and fell back once at 37,170 with SAM coverage; no early or duplicate fallback marker appeared. The accepted two-AI pair still failed: dynamic build-area/enclosure changes or continuously occupied ordinary queues prevented both bots from reaching readiness before obstruction. Save files were produced, but the required combined ready/save/control evidence was absent. | Keep status testing and do not open a PR. Next cycle should retain the same-site bot-specific layout, establish an ordinary idle queue for both AIs without policy-timing or manager-state injection, schedule obstruction after both production-accepted markers, and complete the distinct save/load control. |

## Handoff receipt

- Proposed status: `Second iteration - testing`
- Product cycles used: `2 / 20`
- PR: none
- Balance/geometry: unchanged; cycle 2 only lowers optional SAM queue ownership beneath a waiting field project and improves ignored scenario setup.
- Blocking evidence: one ordinary ready fallback succeeded as evidence, but the required two-AI blocked pair and distinct legal/save-load control did not both reach readiness within accepted scenarios.
