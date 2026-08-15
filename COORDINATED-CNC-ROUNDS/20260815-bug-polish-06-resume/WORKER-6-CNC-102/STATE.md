# Worker State: CNC-102

## Assignment

- Worker: `WORKER-6`
- Task: `CNC-102 — Resonator fancy-placement fallback`
- Status: `Fourth iteration - testing handoff after cycle 4`
- Base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Task branch: `agent/round-20260815-cnc102-resonator-fallback`
- PR base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Cycle: `4` (completed; next authorized cycle is 5)
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
| 3 | Added staged scenario cash and map-local fixed ordinary construction queues so both normal bots can accept real Resonators before obstruction. Extended recovery behind the ready deadline and made retained ready field work outrank later entries in the same queue. | Cycle-2 never exercised the two-bot ready case. Once both items became ready, Iron Reaper exposed a second lifecycle failure: the old reservation deadline could reset a retained item before fallback, and later queue-front work could hide it. | Warnings-as-errors affected build passed. Focused `TiberiumFieldPolicyTest` 17/17, CNC YAML, Python syntax, and `git diff --check` passed. Ordinary-queue calibration passed with both production-accepted markers by tick 2350. The legal/save preload passed to tick 3500 with Brutalis fancy placement, Iron Reaper ready timer, a tick-3000 save, and no early fallback. Repeated blocked runs reached tick 5200 in 47-50 seconds. Evidence: `.worktrees/cnc102-cycle3/idle-facts-calibration-run/`, `final-fresh-run-fixed/`, `blocked-fixed-queue-run-2/`, and `blocked-final-pass-run/`. | Final blocked evidence consistently started Brutalis and Iron Reaper ready timers near ticks 2600 and 2940. Brutalis issued exactly one covered simple fallback just after its deadline. Iron Reaper no longer reset into extension after the recovery-deadline fix, but its completed queue entry disappeared after the first retained-placement poll and never issued fallback by tick 5200. The save/load leg was not continued because the same blocker remained. | Preserve testing status and do not open a PR. Cycle 4 should instrument or repair ownership transfer when a ready completed item ceases to appear in `AllQueued` after its first retained poll; prove whether another queue owns it or it is being discarded, then rerun the same blocked and saved legal controls. |
| 4 | Removed the custom enemy's starting covert upgrade from the ignored evidence-map generator; no engine, policy, balance, timing, SAM, or geometry code changed. | The missing Iron Reaper item was scenario preemption: the covert enemy signal matured Iron Reaper's technology-counter switch out of Economy, removing the ready Resonator's `upgrade.economy3` prerequisite. | Warnings-as-errors affected build passed; focused `TiberiumFieldPolicyTest` 17/17; global and both generated-map YAML; Python syntax; `git diff --check`. Exactly two full-engine games were run. Game 1 reached tick 5200: both ready timers started, Brutalis fell back once at 4097 after deadline 4067, and Iron was preempted. Game 2 saved at tick 3000, restored both deadlines on load, and reached tick 5200; Brutalis fell back once at 4070 after 4063 and Iron once at 4445 after 4431, with no pre-save fallback. Evidence: `.worktrees/cnc102-cycle4/game-1-run/`, `game-2-preload-run/`, and `analysis/game-{1,2}/`. | Ready-only timing, two-AI fallback, single-order behavior, and save/load persistence are now observed. The intended legal west control was invalid because both adjacent trees selected `43,160`; the east blocker covered both, so this cycle adds no fresh legal-fancy evidence. | Game-1 policy recommendation (guarantee one fallback per fully ready blocked project) was accepted and tested by game 2 for both AIs. Game-2 recommendation (deterministic legal fancy placement plus explicit non-blocking SAM evidence) is accepted as the next required follow-up; the exact two-game budget prevents another run. Keep testing status and do not open a PR. |

## Handoff receipt

- Proposed status: `Fourth iteration - testing`
- Product cycles used: `4 / 10`
- PR: none
- Balance/geometry: unchanged; cycle 4 changes only the ignored scenario generator's enemy starting upgrade.
- Evidence status: both ordinary bots now preserve ready-only deadlines across save/load and each issues one post-deadline fallback. Fresh legal-fancy and explicit SAM-nonblocking evidence remain incomplete because the game-2 control blocker covered both selected sites.
