# Worker State: CNC-84

## Assignment

- Worker: `WORKER-7`
- Task: `CNC-84 — Repair-queue recovery for Skynet`
- Status: `cycle 1 complete; implementation and full-engine evidence ready for review`
- Base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Task branch: `agent/round-20260815-cnc84-repair-queue`
- PR base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Cycle: `1` (fresh)
- Balance: frozen
- PR: none

## Cycle 1 result

- Commit: `4ad929e441b34399f2bee11874f4e10c82bd2713`
- Concrete stall owner: repair claims were squad-local while `Reservable` pad
  capacity is player-wide. Separate Skynet Apache squads could both select the
  same apparently free helipad in one bot tick, overwrite its engine
  reservation, and leave the displaced non-idle `Repair` order stalled.
- Fix: distinguish holding waiters from active pad claimants, consult claims
  across all Skynet air squads, preserve first-wait ticks, promote only the
  oldest compatible waiter within two influence cells of the pad, and replan a
  displaced stale repair order. Existing repair eligibility, safety/routing,
  squad membership, reinforcement ownership, and post-repair behavior remain
  unchanged.
- Focused tests: `dotnet test OpenRA.Test/OpenRA.Test.csproj --filter
  'FullyQualifiedName~AirThreatGeometryTest' --no-restore` passed 68/68.
- Compile: `make -j2` passed with zero warnings/errors; the focused test rebuild
  after the final change also compiled the affected projects successfully.
- Syntax/whitespace: both custom Lua scripts passed `luac -p`; `git diff
  --check` passed.
- Full-engine control: `/tmp/cnc84-scenarios.Vl3v5D/cnc84-control.oramap`,
  ordinary Skynet versus Brutalis with normal modules, seed 8404, 1,100 ticks
  (44 seconds). One pad claim was active at a time. Apache `#352`, the oldest
  ready waiter, promoted and repaired before later waiter `#353`; far incoming
  `#354` remained waiting. `#352` then received a new routed order to `fact#363`.
  Healthy Apache `#355` remained full-health and attacked `fact#363`.
- Full-engine provider recovery: `/tmp/cnc84-scenarios.Vl3v5D/cnc84-provider.oramap`,
  ordinary Skynet versus Brutalis with normal modules, seed 8412, 1,200 ticks
  (48 seconds). Destroying helipad `#350` cleared all four stale targets; after
  helipad `#364` appeared, interrupted Apache `#351` reclaimed the sole pad,
  completed repair, and the FIFO advanced to `#352` then `#353`, without a
  duplicate active claim. Healthy Apache `#355` remained full-health and active.
- Raw scenario packages and the most recent engine logs are intentionally
  untracked under `/tmp/cnc84-scenarios.Vl3v5D/`.

## Literal scope

Treat this as a minor Skynet stalled repair-queue fix. Existing repair code is
already good, including its avoidance of dangerous repair options. Preserve all
existing repair eligibility, danger/safety, routing, target selection, squad
handoff, ownership, and post-repair reinforcement/rejoin behavior. The queue-
stall fix may not alter reinforcement ownership, recruitment, squad return, or
post-repair orders. Diagnose the concrete queue stall—such as
queue ownership/capacity, reservation, pad selection, stale order,
power/prerequisite, movement, or completion handoff—and fix only that owner.
Observed Skynet bases can fill with unrepaired units while repair facilities are
not fully occupied. A distant unit reserving a pad is only a hypothesis; trace
discovery, readiness, queue ownership, pad assignment, arrival, repair, and
release to identify the actual forgetting/starvation cause. Among eligible ready
units waiting in or near the base, dispatch FIFO by longest waiting time. A
far-off incoming unit must not jump ahead of those units or reserve capacity
over them.
Eligible damaged units must no longer remain stalled when existing repair
capacity is available, and repaired units must return to their prior valid
squad/mission. Any redesign or tuning of the working policies above is
prohibited. Preserve pad capacity, reservations, safe movement, healthy-unit
activity, and all balance.

Do not build a universal repair-dispatch redesign, continuous-dispatch
architecture, broad repair-policy rewrite, or unrelated air/ground repair
matrix. Destroyed/blocked repair providers are in scope only if directly shown
to cause the concrete stall.

## Minimal scenarios and acceptance

Use two distinct custom full-engine scenarios, never manager-only fixtures, each
<=120 seconds, with ordinary CNC AIs and all normal modules enabled:

1. Reproduce a Skynet force with multiple damaged eligible units and available
   repair capacity. Prove bounded diagnostics account for each unit, identify
   the stall owner, dispatch units without overfilling/thrashing pads, and return
   repaired units to their prior valid work.
2. Re-run the same causal queue path with the directly relevant provider/pad
   becoming blocked or destroyed and then available again. Prove recovery of the
   stalled queue, no duplicate reservations or unsafe orders, and no healthy
   force idling. If that perturbation is not causal, record it as out of scope.

Acceptance is a narrowly evidenced fix for the concrete Skynet stall, preserved
existing policy, no overfill/thrash, correct handoff, and no regression in the
ordinary control. Run focused repair-queue tests, syntax, and `git diff --check`;
do not require the task-row's broad natural-match/performance matrix or a
universal dispatch redesign.

## Ambiguity and selective port dependency

The task-sheet row requests broad multi-unit/pad and complete-match evidence,
while this resumed assignment explicitly narrows CNC-84 to a minor concrete
stall fix. The resumed clarification governs; escalate only if the concrete
owner cannot be identified without broader behavior. Inspect predecessor commits
selectively and port only task-faithful repair-queue changes. Never wholesale-
merge another worker's state, report, evidence, or process metadata.

## Handoff

Do not edit the task sheet or coordinator state, push `bleed`, or merge a PR.
