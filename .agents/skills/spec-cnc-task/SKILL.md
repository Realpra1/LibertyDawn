---
name: spec-cnc-task
description: Convert one isolated Liberty Dawn CNC task packet into a complete worker-local state and implementation contract by investigating relevant code, history, configs, tests, maps, logs, and active PR relationships. Use in a fresh Sol-xhigh session before assigning one CNC task to an implementation worker.
---

# Spec One CNC Task

Use Sol 5.6 xhigh. Read applicable `AGENTS.md`, exactly one supplied task packet,
and the repository evidence relevant to that task. Do not read the full task
sheet, other worker specs, or unrelated reports. Do not modify product code.

Copy `assets/WORKER-STATE.template.md` to the requested task-specific path and
replace every placeholder. The resulting file is the worker's complete durable
contract, not a summary.

## Specification method

1. Preserve the literal user requirements and explain why the task exists and the
   predicted observable change.
2. Inspect current implementation, configuration ownership, history, tests,
   current control behavior, and relevant open PR commits. Never spec from task
   prose alone when repository inspection can settle a fact.
3. Translate the request into the simplest player-created black-box acceptance
   scenario and its final observable outcome.
4. Inventory every ordinary AI/module that can issue orders to, reserve, produce,
   consume, repair, or retarget the same actors, queues, cash, or targets.
5. List likely wrong approaches, hidden assumptions, regressions, performance
   traps, and diagnostic blind spots. State explicitly when another worker's PR
   may alter the solution and tell this worker which branch/PR commits to monitor.
6. Specify modular ownership: policy/config belongs in the owning rules/config;
   algorithmic invariants belong in code. Identify responsibility boundaries,
   likely cohesion problems, and oversized classes/functions that may need a
   focused split. Avoid prescribing an implementation when several designs can
   meet the observable contract.
7. Define focused checks, current-control comparison, ordinary real-AI games,
   matched differential evidence where possible, contention tests, at least three
   distinct adversarial scenarios, and a final literal regression.
8. Require evidence that the intended map, options, bots, actors, ticks, scenario,
   and final outcome occurred. Requests, reservations, movement, or logs without
   the player-visible result are not acceptance.
9. Record the common base SHA, task branch, intended PR base, cycle budget of 20,
   global resource-lock path/capacity, and all durable output paths.
10. Specify useful bounded diagnostics, handled error/warning boundaries, and the
    exact evidence needed to distinguish request, rejection, reservation owner,
    competing consumer, state transition, order, and final outcome. Require noisy
    temporary diagnostics to be removed before publication.
11. Require early engine evidence: for AI or emergent behavior, schedule the first
    real-AI game by cycle 10 and begin adversarial work by cycle 12 at the latest.
12. For routing or transport, include ordinary connected and island/blocked
    topology such as Archipelago. For persisted behavior, include save/load and
    reject a reloaded state as sole acceptance. For hot paths, define a bounded
    CPU/allocation expectation and measurement or credible regression signal.
13. Write a concise implementation/publication plan covering desired and forbidden
    behavior, ownership, instrumentation, tests, task report, PR, and checks.
14. Make every planned test adversarial in purpose. For each unit, integration, or
    game test, name the failure hypothesis, condition being stressed or changed,
    expected failure signal, and player-visible pass evidence. Allow one minimal
    cheese-in-front-of-the-mouse smoke scenario to prove the harness/basic path;
    after it first works, immediately increase difficulty instead of repeating the
    same happy path.
15. Build a difficulty ladder that varies timing, state transitions, geometry,
    resources, missing/destroyed assets, unit counts, enemy pressure, competing
    managers, save/load, and duration as relevant. Treat tests as the substitute
    for human playtest feedback: they must challenge assumptions and generate the
    evidence used for the next implementation decision.

Keep the coordinator state concise; all task detail belongs in this worker state.
Return only the task ID, worker-state path, base SHA, and material cross-worker
warning to the coordinator.
