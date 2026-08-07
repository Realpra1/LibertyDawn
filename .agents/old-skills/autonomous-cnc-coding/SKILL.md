---
name: autonomous-cnc-coding
description: Autonomously select, implement, instrument, game-test, document, and publish the next eligible Command & Conquer feature from a repository task sheet. Use when Codex should run a no-questions CNC development loop, create one independent PR per task, or combine completed task PRs into an integration PR.
---

# Autonomous CNC Coding

Do not combine this skill with `coding-workflow` (the coding skill). This skill replaces it for the run.

## Autonomy

- Do not ask implementation or preference questions. Investigate, choose the best option, and record material assumptions.
- Do not exceed granted authority. If credentials, permission, required files, or a safe path are unavailable, record the blocker and stop without asking.
- Treat the task sheet and repository files as authoritative over conversational memory.
- From a repository clone, use `AUTONOMOUS-CNC-TASKS.md`, `AUTONOMOUS-CNC-STATE.md`, `DEFERRED_WORK.md`, and `AUTONOMOUS-CNC-REPORTS/` at the repository root as the default durable workflow files. Do not depend on files from another machine's Codex home directory.
- Do not stop for questions, status checks, minor corrections, small side requests, or task completion. Answer or apply them, then continue with the current cycle or next eligible task. Pause only when the user explicitly says stop/pause or an actual blocker prevents progress.
- For long unattended runs, use a reversible session-scoped mechanism to prevent inactivity standby when the environment permits it. Do not change the permanent power plan. Record the helper/process identity and restore normal sleep behavior whenever the autonomous run pauses or ends.

## Task selection and isolation

1. Resolve the repository root, read applicable `AGENTS.md`, then read this skill, `AUTONOMOUS-CNC-TASKS.md`, `AUTONOMOUS-CNC-STATE.md`, `DEFERRED_WORK.md`, relevant reports, and available current logs. On a fresh machine, fetch remotes before trusting local branch availability; do not discard local work.
2. Select the first sensible task not marked `complete` or `first iteration`. Record it in the state file so unmerged PRs are not selected again.
3. Use a cumulative branch series by default: follow the exact next-base branch recorded in `AUTONOMOUS-CNC-STATE.md`, create the task branch/worktree from its freshly fetched remote ref, then base each later task on the immediately preceding task PR branch. Preserve all preceding task work. Follow an explicit task-sheet/user override when tasks must be isolated.
4. Write a concise plan, including desired behavior, forbidden/regression behavior, implementation, instrumentation, tests, and publication.

Before coding, translate the task literally into the simplest black-box acceptance scenario a player could create. Record the final observable outcome, not only internal events. Inventory every normal AI module that can issue orders to, reserve, produce, consume, repair, or retarget the same actors/queues; these are mandatory contention tests.

If no eligible tasks remain, create one integration branch from the base, merge the task branches listed in the state/task sheet, run build/unit checks, and open an integration PR. Leave every source PR open. Do not require a full game test for this integration-only task.

## Implementation rules

- Keep responsibilities separate and code modular; split oversized classes/functions when it improves cohesion.
- Put tunable policy and content in the owning config, rules, save, or map layer; keep algorithmic invariants in code.
- Preserve existing project behavior and unrelated work.
- Add proportionate unit tests plus useful debug logs and handled warnings/errors.
- Record valuable out-of-scope fixes, refactors, and optimizations in the repository work file; do not expand scope silently.
- Store raw logs, saves, replays, profiler output, and generated test artifacts outside Git or under ignored `AUTONOMOUS-CNC-LOGS/`. Record concise evidence, seeds, paths, and conclusions in the task report.
- Never push directly to the base branch. Commit and push only the task branch, then open a PR targeting the base.

## Evidence-driven test loop

Repeat at most thirty times total, including the adversarial edge-case cycles below:

1. Build and run focused unit/static checks. Fix every relevant build error or warning.
2. Use focused setup maps to accelerate early cycles when useful, but run the full game engine with real bot types. Before normal acceptance passes, include a fully enabled scenario with all relevant ordinary AI modules and use an ordinary test match when the behavior depends on emergent game conditions. A passive/custom bot or isolated manager fixture cannot prove completion alone.
   On Linux and other unattended environments, prefer the repository's explicit headless MAX launch path for engine tests that do not require graphics or input. Prove from the current run that headless MAX activated, the intended ordinary bots and map loaded, simulation ticks advanced, the final observable outcome occurred, and logs/replay or benchmark evidence flushed. Headless MAX does not replace a task's required graphical, rendering, input, or platform-specific checks.
3. Prefer real full-match evidence, especially for adversarial and final testing. Launch debug games in the background, poll in intervals no longer than 60 seconds, and normally cap a test at 30 minutes. When scenarios are independent and the host has spare CPU and memory, optionally run two or three games concurrently; use those slots for materially different scenarios instead of near-duplicate spawn swaps unless position bias is under test. Use long-distance starts for progression/endurance and short-distance starts for rush/early-defense pressure. Isolate every support directory, log/replay/save path, benchmark prefix, port, and display, count and judge each cycle separately, and return to serial testing if contention makes evidence unreliable. A required full-match test may run longer within reason while it is still making useful progress.
4. Prove from current logs that the intended map, bots, actors, options, and scenario loaded and that the final player-visible outcome occurred. Intermediate states such as request, reservation, loading, movement, or target selection are not a pass.
5. Compare logs/replay evidence with every desired and forbidden behavior. Instrument mission purpose, reservation owner, competing consumer, candidate rejection, state transition, and final outcome when those cannot otherwise be distinguished.
6. Force each inventoried competing AI system to act during at least one integrated test. For routing/transport work, test both a normal connected map and an island/blocked topology such as Archipelago.
7. On failure, diagnose and fix the implementation or test setup. Add targeted instrumentation when evidence is missing; remove obsolete/noisy instrumentation when finished.
8. For expensive setup, optionally save shortly before the critical event and reload that state after logic changes for rapid comparisons. Record the save's commit/config/tick, reject incompatible saves, and never use a reloaded state as the sole acceptance, adversarial, or final-regression evidence because it may preserve stale initialization or AI state.
9. If the scenario never occurs, adjust starting conditions, duration, bots, or create a focused test map. Judge unexpected behavior explicitly as acceptable or defective.
10. Stop the test process when evidence is sufficient or the time limit is reached.

After normal acceptance passes, run at least three distinct adversarial tests. Begin immediately, including before cycle 20; thirty cycles is a maximum, not a target or minimum:

- Prefer a matched differential game as the golden adversarial test when the behavior can be toggled: run otherwise identical AIs with the same faction, starting state, map conditions, and explicit seed, enabling the new behavior for only one side. When the scenario materially exercises the feature, the updated AI should win decisively. Treat a tie, marginal advantage, or loss as evidence to investigate rather than proof of completion; document when map symmetry or unavoidable nondeterminism makes the comparison inconclusive.
- Actively find or create edge cases likely to break the implementation, including hostile map geometry, timing/state transitions, unusual unit counts, missing assets, and competing AI systems when relevant.
- Run every adversarial cycle in the full game engine with ordinary game AIs and relevant normal modules. Focused setup maps may force an edge case, but passive/custom bots and isolated simulations do not count.
- Run at least one real full match at the fastest game-speed setting and allow it to reach its natural conclusion. Perform the first real-AI game no later than cycle 20; no task may reach cycle 30 without real-match evidence.
- Include at least one shared-resource contention/race case whenever another module can reserve or order the same actors, queues, targets, or cash.
- Define the expected failure signal, force the scenario to occur, and inspect logs/replay evidence; a normal happy-path rerun is not an adversarial cycle.
- If all three adversarial tests expose no broken, unforeseen, or reasonably incorrect behavior, run the final regression and allow completion without padding the cycle count.
- If an edge case breaks the code, diagnose, fix, and retest it, then complete at least three clean adversarial tests. Continue only as evidence requires, up to the thirty-cycle maximum; count every attempt.
- After adversarial fixes, rerun the original literal acceptance scenario with all normal modules enabled, preferably as another real match. Completion requires that final regression plus at least three clean adversarial tests.

After thirty unsuccessful loops, deliver the safest useful result as `first iteration`; never label an unproven result `complete`.

## Handoff and continuation

1. Mark the task `complete` only when evidence meets the plan; otherwise mark it `first iteration`. Record how many evidence-driven test-loop cycles the task used.
2. Update `AUTONOMOUS-CNC-STATE.md` and save a concise report under `AUTONOMOUS-CNC-REPORTS/<task>.md` covering behavior, design choices, assumptions, test-cycle count, tests, logs, PR, and remaining risks. Commit these coordinator updates with the cumulative task branch so another machine can resume from its remote PR head.
3. Push the feature branch and open the PR; never merge it or close other PRs.
4. After local and game evidence is complete, wait for the PR's required GitHub checks. Fix failures and rerun checks within the task's test-loop budget. Mark the task `complete` only when required checks are green; use `first iteration` when checks cannot be made green.
5. Between evidence cycles, after any context compaction, and before selecting another task, reread this skill, the task sheet, and the state file. Keep only the current task's detailed specification in its report or a dedicated current-task file; keep stable coordinator files concise. Continue from those sources and disregard stale memory.

Codex cannot deliberately clear its own context or start a new turn after yielding. Continue within the active autonomous run; durable state makes automatic context compaction safe.
