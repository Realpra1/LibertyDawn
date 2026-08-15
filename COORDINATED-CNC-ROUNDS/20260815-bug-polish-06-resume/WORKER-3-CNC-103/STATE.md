# Worker State: CNC-103

## Assignment

- Task: `CNC-103 — Universal radar rebuild recovery`
- Base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13` (PR117 head)
- Branch: `agent/round-20260815-cnc103-radar-recovery`
- Status: `Cycle 2 complete — lifecycle gap fixed and tested; publication/reviewer handoff pending`
- Cycle: `2`
- PR: `none`

## Smallest literal contract

After an ordinary AI has established radar and subsequently loses it, rebuild one
radar-capable provider when prerequisites, power, and a viable queue permit. Basic
economy or power recovery may go first, but radar recovery may not be omitted
indefinitely. Preserve existing priorities and all balance values. A live powered
EYE satisfies the capability; TMPL does not. Do not duplicate commitments across
construction queues. Release/retry ownership after queue loss, cancellation,
placement failure, capture, recapture, and save/load. Acceptance is one completed,
owned, powered radar capability and one applicable downstream path, not a request
or log line.

## Explicit exclusions

Do not redesign openings, first-tower behavior, silo/build-order policy, balance,
or unrelated AI policy. Do not require an exact personality/configuration matrix,
new dedicated architecture, exact `viability_tick`, exhaustive ownership or
diagnostic taxonomies, universal matrices, repeated pair ladders, three-adversary
gates, endurance/performance thresholds, or publication/cycle bureaucracy beyond
the current coordinator requirements. Do not preempt arbitrary active work or
poll with per-tick full-world scans.

## Checks and scenarios

Use custom full-engine scenarios only; never fixtures. Each cycle may run at most
two distinct games, each capped at 120 seconds, with all normal modules and
ordinary CNC AIs enabled. Include a real established-radar loss (prefer an actual
support-power strike), essential power/economy contention, and a save/load or
capture/queue-loss lifecycle case when applicable. Prove final live powered radar,
single commitment, preserved essential work, and one downstream result. Add small
focused policy checks for never-established/lost radar, EYE versus TMPL, power and
refinery ordering, two-queue deduplication, lifecycle release, and persistence.

## Dependencies and porting

Respect the existing contracts of CNC-101, CNC-76, and CNC-77; inspect only their
published commits when the coordinator supplies a dependency pointer. Selectively
port only task-faithful product/test commits from the old CNC-103 branch. Never
wholesale-merge old state, report, journal, or unrelated cumulative commits.

## Handoff

Report concise evidence and artifact paths. Keep raw logs/replays/saves outside
Git. Do not edit the task sheet or coordinator state, merge a PR, or push `bleed`.

## Final-review disposition and cycle-2 authorization

- Verdict: `advisory concern`.
- Accepted literal gap: established radar is inferred only by periodic queue-choice
  snapshots; a provider that completes and is destroyed between snapshots is not
  durably recorded as previously established, so recovery can be skipped.
- Authorized cycle-2 fix: record establishment from the provider lifecycle or an
  equivalent bounded event, and add one focused regression covering loss between
  queue-choice snapshots. Make no broader radar redesign.
- If product code changes, run at most two simple custom full-engine scenarios,
  each capped at 120 seconds, in addition to the focused regression. Otherwise
  do not add scenarios.
- Advisory path: `OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/BaseBuilderRadarRecoveryManager.cs:152`.

## Cycle 1 outcome

- Selectively ported the ten CNC-103 product/test refinements from the prior task
  branch without its state, report, or unrelated cumulative history.
- Resolved the sole PR117 source conflict by retaining `WallPlanner.Tick(bot)` and
  adding the radar observation call; removed two imports made redundant by PR117.
- Added established-provider loss tracking, global exact-queue commitment
  ownership, power/refinery/storage ordering, placement/capture/cancellation
  release, save/load persistence, CNC EYE-only recovery configuration, and focused
  policy coverage without changing balance values.
- `make check`: pass, zero warnings/errors.
- `dotnet test ... --filter FullyQualifiedName~RadarRecoveryPolicyTest`: pass,
  16/16.
- `./utility.sh cnc --check-yaml`: pass.
- Full-engine game 1, storage/economy ordering: product behavior passed through
  tick 6000. It established and lost HQ radar, committed a silo first, committed
  exactly one replacement HQ, restored operational radar, and completed an
  HQ-dependent actor. The batch summary is a harness false-negative because the
  required regex `recovery entered production` did not match the actual stronger
  line `radar recovery hq entered production`; all product and forbidden markers
  passed. No rerun was made because cycle 1 had reached its two-game cap.
- Full-engine game 2, critical-power active-queue capture: pass through tick 8000.
  It released queue 135 after capture, retried once on queue 136, restored powered
  radar, and completed a downstream HQ-dependent actor with no duplicate/fatal/
  desync marker.
- Raw artifacts:
  `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260815-bug-polish-06-resume/outputs/worker-3-cnc103/cycle-01/game/`.
- Proposed disposition is superseded by the authorized cycle-2 lifecycle fix;
  after that narrow fix and evidence, return to publication/reviewer handoff.

## Cycle 2 outcome

- Accepted and fixed the final-review lifecycle gap without changing radar queue,
  priority, power, configuration, or balance policy. The recovery manager now
  records an owned `ProvidesRadar` actor from the bounded `World.ActorAdded`
  lifecycle event, so establishment remains durable if that provider is destroyed
  before the next periodic or queue-choice scan.
- Added a focused regression that models an unrelated actor, provider addition,
  and provider loss entirely between scans; recovery becomes required only after
  the provider lifecycle event.
- Locked `make check`: pass, zero warnings/errors.
- Locked focused NUnit run: pass, 17/17 `RadarRecoveryPolicyTest` cases. The build
  emitted four pre-existing analyzer warnings in unrelated test files.
- Full-engine `active-radar-queue-capture`: pass through tick 8000. The AI lost
  established radar under critical power, released the first exact queue after
  capture, retried exactly once on the second queue, restored powered radar, and
  completed a downstream HQ-dependent actor. All required markers matched; no
  duplicate, lifecycle, setup, fatal, nuclear-strike, or desync marker appeared.
- Raw artifacts:
  `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260815-bug-polish-06-resume/outputs/worker-3-cnc103/cycle-02/game/`.
- Proposed disposition: `Complete - testing`. Next authorized action is the
  publication/reviewer handoff; do not start another isolated product cycle from
  this state without coordinator direction.
