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
   predicted observable change. Classify the change and fill the worker state's
   balance authority. Balance is frozen by default: authorize only the exact
   balance surface expressly requested by the user/task; never infer permission
   to tune values so behavioral evidence looks better.
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
11. Make the first behavioral test after the first implementation change a
    full-engine simulation with ordinary real AI and relevant normal modules,
    normally at headless MAX. A focused custom map and obvious cheese setup may
    accelerate the first event, but the full game engine and normal AI must be
    active from test 1. Treat unit tests and passive fixtures as supplementary;
    the repository already has them and they cannot replace simulation feedback.
    For an AI behavior change, make test 1 a matched changed-versus-old-control
    pair whenever the available toggle/build infrastructure permits it.
12. For routing or transport, include ordinary connected and island/blocked
    topology such as Archipelago. For persisted behavior, include save/load and
    reject a reloaded state as sole acceptance. For hot paths, define a bounded
    CPU/allocation expectation and measurement or credible regression signal.
13. Write a concise implementation/publication plan covering desired and forbidden
    behavior, ownership, instrumentation, tests, task report, PR, and checks.
14. Treat full-AI game simulations as cheap primary feedback that substitutes for
    expensive human playtesting. Make every planned test adversarial in purpose.
    For each unit, integration, or
    game test, name the failure hypothesis, condition being stressed or changed,
    expected failure signal, and player-visible pass evidence. Allow one minimal
    cheese-in-front-of-the-mouse smoke scenario to prove the harness/basic path,
    but run even that smoke inside the full engine with ordinary AI;
    after it first works, immediately increase difficulty instead of repeating the
    same happy path.
15. Build a difficulty ladder that varies timing, state transitions, geometry,
    resources, missing/destroyed assets, unit counts, enemy pressure, competing
    managers, save/load, and duration as relevant. Treat tests as the substitute
    for human playtest feedback: they must challenge assumptions and generate the
    evidence used for the next implementation decision.
16. For every AI strategy, priority, production, economy, targeting, recovery, or
    tactical change, define an old-behavior control and comparative success
    metrics. Prefer a feature-disabled control in the same build; otherwise use
    the recorded pre-change base SHA or a named known-good older AI commit in an
    isolated worktree. Keep map, factions, seed, starts, options, initial state,
    content, and opponents matched so the behavior is the intended difference.
17. Require the changed AI to materially outperform old behavior in scenarios
    that exercise the change, using outcome plus task-relevant measures such as
    survival, objective completion, tech timing, economy/army value, useful damage,
    losses, idle time, or simulation cost. Treat repeated parity, marginal gain,
    or a loss as strong evidence of an implementation error or bad strategic
    policy. Require investigation and correction or a concrete task-specific
    explanation; logs showing the feature fired are never enough.
18. Before finalizing any AI-behavior or policy spec, write a concise proposed-
    policy narrative containing current/control behavior, proposed rule of thumb,
    expected situations and counters, predicted benefit/tradeoffs, forbidden
    blunders, and focused questions. Copy it (not symlink it) to the review role's
    `inputs/NARRATIVE.md`. Write a separate short `inputs/TASK-CONTEXT.md` with
    task ID/title, expected change, why, change category, in/out-of-scope behavior,
    and exact balance authority; exclude source, full spec, and preferred verdict.
    Put a strict JSON job beside the output with exactly the absolute
    `design_reference`, staged `task_context`, staged `narrative`, and `output`
    paths. Launch
    `policy-speccer` through the coordinator role launcher so a no-history fresh Sol
    5.6 high Policy Reviewer reads only that narrative and
    `.agents/references/LIBERTY-DAWN-DESIGN.md`. Record its verdict, review path,
    useful recommendations, rejected recommendations with reasons, and the
    adversarial tests it inspired in the worker state. Skip only when policy is
    genuinely irrelevant, recording why.

Keep the coordinator state concise; all task detail belongs in this worker state.
Return only the task ID, worker-state path, base SHA, and material cross-worker
warning to the coordinator.
