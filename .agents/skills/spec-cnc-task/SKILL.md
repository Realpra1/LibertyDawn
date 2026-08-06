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
   algorithmic invariants belong in code. Avoid prescribing an implementation
   when several designs can meet the observable contract.
7. Define focused checks, current-control comparison, ordinary real-AI games,
   matched differential evidence where possible, contention tests, at least three
   distinct adversarial scenarios, and a final literal regression.
8. Require evidence that the intended map, options, bots, actors, ticks, scenario,
   and final outcome occurred. Requests, reservations, movement, or logs without
   the player-visible result are not acceptance.
9. Record the common base SHA, task branch, intended PR base, cycle budget of 20,
   global resource-lock path/capacity, and all durable output paths.

Keep the coordinator state concise; all task detail belongs in this worker state.
Return only the task ID, worker-state path, base SHA, and material cross-worker
warning to the coordinator.
