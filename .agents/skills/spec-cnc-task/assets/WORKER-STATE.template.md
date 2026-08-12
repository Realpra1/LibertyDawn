# Worker State: {{TASK_ID}}

Reread this file after compaction, before each cycle, after its two game analyses,
and before publication. It is the complete assignment. Do not read the task sheet,
coordinator state, other skills, or other worker specs. Read applicable
`AGENTS.md`; inspect another task PR only when named under Dependencies.

## Assignment

- Worker/task: `{{WORKER_ID}}` / `{{TASK_ID}} — {{TITLE}}`
- Change category: `{{CHANGE_CATEGORY}}`
- Balance authority: `{{EXPLICIT_BALANCE_AUTHORITY_OR_FROZEN}}`
- Status: `Specified`
- Base branch/SHA: `{{BASE_BRANCH}}` / `{{BASE_SHA}}`
- Task branch / PR base: `{{TASK_BRANCH}}` / `{{PR_BASE}}`
- Current cycle: `1`; cycles used: `0/5 primary`, `0/10 optional Luna`
- Required model: cycle 1 `Sol high`; cycles 2-5 `Terra medium`; cycles 6-15
  `Luna medium` only when coordinator authorizes minor obvious work
- Game/build capacity: `2` / `1`; lock: `{{ABSOLUTE_LOCK_DIRECTORY}}`
- Report: `{{TASK_REPORT_PATH}}`
- Analysis directory: `{{ABSOLUTE_ANALYSIS_DIRECTORY}}`
- Design: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Policy scratchpad/lock: `{{ABSOLUTE_POLICY_SCRATCHPAD_PATH}}` /
  `{{ABSOLUTE_SHARED_LOCK_DIRECTORY}}`
- Games completed: `0`; cycle-3 review: `not due`; PR: `none`

Each worker invocation performs only the current authorized cycle, updates this
file/report, and exits. Do not loop into another model tier in the same context.

## Integrated assignment

- Phase/release head: `isolated` / `not assigned`
- Repair branch/base: `not assigned`
- Release-wide integration cycle: `not assigned; maximum 5`
- Integrated role model: `Terra medium for integration cycles 1-5; Sol medium only
  for an explicitly authorized blocker escalation`

The Terra Integrator fills these fields before combined testing. Integrated work
uses this normal worker's same task boundary, canonical game launcher, installed
content staging, preflight, two-game contract, and minor-fix restraint. Prefer
`launch-ai-parallel.py --content <installed-runtime-content>` and verify the
isolated `SupportDir/Content` target before every game; an incomplete custom
launch command is not an integration setup.

## Why and predicted change

{{WHY_AND_PREDICTED_CHANGE}}

## Authoritative behavior

{{DESIRED_BEHAVIOR}}

## Forbidden behavior and failure signals

{{FORBIDDEN_BEHAVIOR}}

## Current implementation and old-behavior control

{{CURRENT_IMPLEMENTATION}}

## Likely wrong approaches and challenges

{{WRONG_APPROACHES}}

## Competing systems and ownership

{{COMPETING_SYSTEMS}}

## Dependencies

{{CROSS_WORKER_DEPENDENCIES}}

## Spec policy consultation

- Partial spec: `{{SPEC_POLICY_NARRATIVE_PATH_OR_NOT_APPLICABLE}}`
- Sol-high review/verdict: `{{SPEC_POLICY_REVIEW_PATH_OR_NOT_APPLICABLE}}` /
  `{{SPEC_POLICY_VERDICT}}`
- Adopted hypotheses: `{{SPEC_POLICY_ADOPTED}}`
- Rejected/deferred advice and why: `{{SPEC_POLICY_REJECTED}}`
- Scratchpad update: `{{SPEC_POLICY_SCRATCHPAD_UPDATE}}`

## Acceptance plan

- Literal player-visible result: {{LITERAL_ACCEPTANCE}}
- Focused checks/instrumentation: {{FOCUSED_CHECKS}}
- Two-or-more distinct games per cycle: {{GAME_TESTS}}
- Old-control comparison/metrics: {{CONTROL_BASELINE_AND_METRICS}}
- Adversarial cases: {{ADVERSARIAL_TESTS}}
- Final regression: {{FINAL_REGRESSION}}

## Implementation rules

- Investigate code, history, configs, tests, and evidence; choose the smallest safe
  solution. Preserve unrelated behavior and user changes.
- Keep responsibilities separate and ownership explicit. Prefer short cohesive
  functions/classes; split mixed or oversized logic when it improves clarity,
  testability, or hot-path cost without unrelated churn.
- Prefer simple fuzzy thresholds and game-sensible rules of thumb. Avoid global
  optimizers, graph solvers, rigid partitions, and elaborate state unless tests
  prove a simpler priority, count, distance, threat-map, or cooldown insufficient.
- Put tunable policy in owning rules/config and invariants in code. Do not hide
  production policy in tests or duplicate it across AI personalities.
- Freeze balance unless expressly authorized above. Never alter cost, HP, damage,
  armor, speed, timing, power, prerequisites, probabilities, or resources to make
  behavior pass; that invalidates evidence.
- Add proportionate focused tests. Log actionable handled errors at their owning
  boundary; never swallow failure, fake success, spam per tick, or publish noisy
  temporary diagnostics.
- Keep simulation work bounded: avoid repeated full-map scans, uncontrolled
  allocation, nondeterministic ordering, unbounded retries, and heavy logging.
- Inventory all modules competing for the same actors, queues, cash, reservations,
  repairs, targets, or orders, and exercise them with all modules enabled.
- Record out-of-scope ideas in the task report's deferred section. Do not create a
  task, edit shared deferred work, task sheet, coordinator state, or `bleed`.

## One-cycle evidence loop

One cycle starts with a product/config change. Reading evidence or fixing an
invalid harness is not another cycle. For the current cycle:

1. Reread this state, diff, prior narratives/reviews, and unresolved evidence.
2. Make the smallest evidence-driven change and run relevant focused checks.
3. Run at least two materially different adversarial games. Every game must use
   the full engine, a custom scenario, all features, all AI modules, and
   ordinary enemy AIs from test 1. Normally use headless MAX and stop at 120
   seconds wall-clock; MAX may advance much farther in game time.
   Making this game launch and load the intended map is part of the worker's
   assignment. A process that dies, hangs, or remains before world tick 1 is not
   a game and does not count toward the cycle or its evidence. Repair task-local
   build/content/launcher/display/audio/process-cleanup/scenario problems and
   rerun; never repeat an identical broken launch as a nominal test.
4. Before each game record its failure hypothesis, changed pressure/assumption,
   exact failure signal, and player-visible pass evidence. Vary geometry, timing,
   resources, losses, counts, topology, competing managers, old-control setting,
   or save/load as relevant. Never spend both games on near copies.
5. Give each game—not a batch—to its own fresh Luna Commenter and Luna Policy
   Reviewer. Read both before deciding the next change. Verify narrative facts.
   The worker must carry the strongest policy recommendation into the next
   focused test or code change, or record an explicit rejection with a concrete
   scope, evidence, or safety reason; silently ignoring correct reviewer advice
   is not an acceptable cycle decision.
6. Remove answered/noisy diagnostics, update the journal/report/state, commit, and
   exit so the coordinator can select the next model tier.

Use `with_resource_slots.py` around shared resources and the game
launcher/supervisor as the completion helper. Await the bounded process/result;
do not burn agent turns sleeping or repeatedly polling. Isolate every map, support
directory, port, log, replay, save, benchmark, and display.

If targeted setup diagnosis cannot make the full engine reach world tick 1, save
the exact startup logs, command, process tree, and checkout comparison, then mark
the cycle blocked and request environment help. Do not advance the cycle counter,
produce a narrative for a nonexistent match, or claim task acceptance.

Custom setups should force rare decisions while retaining real AIs/modules: for
example pre-place damaged/healthy capturable structures and engineers, destroy a
critical asset, constrain resources, or pre-spawn opposing forces. Absence of an
unfinished prerequisite behavior is a dependency, not proof this task failed.

For strategic AI changes, prefer a same-build feature-disabled control; otherwise
use the recorded base or named known-good older AI in an isolated worktree. Match
map bytes, seed, starts, options, initial actors/resources, factions, and enemies.
Require material task-relevant improvement, not merely an activation log. Treat
repeated loss, parity, or marginal gain as likely code/policy error unless evidence
supports a task-approved tradeoff.

For performance work, use a matched custom scenario with two ordinary Iron
Reapers, each given at least 300 representative units plus structures. Run each
leg for at most 120 seconds: pre-Codex, newest with advanced squad modules off,
and newest with them on. Compare ticks, tick latency/spikes, CPU, peak memory,
actor counts, and errors/stalls; do not call contended/debug-heavy runs golden.

## Model-tier limits

- Cycle 1/Sol high: implement the coherent initial solution.
- Cycles 2-5/Terra medium: correct evidenced bugs and wrong assumptions. Do not
  casually redesign. After cycle 3 obtain one Luna code review with at most one
  advisory concern, record adoption/rejection, then continue to cycle 4.
- If unresolved after cycle 5, mark `Needs help` or `First iteration - testing`
  unless all remaining work is minor and obvious.
- Cycles 6-15/Luna medium: require coordinator authorization. Only narrow guards,
  config mistakes, assertions, obvious local bugs, and testing are allowed. No new
  architecture, strategic policy, balance, or broad refactor. Stop when the next
  fix requires judgment.

## Analysis isolation

For each game, stage only authorized artifacts for the Commenter. Stage its
`NARRATIVE.md`, a short task context (ID/title, why, category, in/out of scope,
balance authority), design reference, and current scratchpad for the Policy
Reviewer. Use strict launcher JSON envelopes. Serialize policy calls, validate the
reviewer's replacement scratchpad as UTF-8 and <=3,000 characters, then promote
it atomically. Keep detailed analysis ignored; record concise conclusions/paths.

## Publication

Propose `Complete - testing` only when literal acceptance, required adversarial
evidence, final regression, checks, report, PR, and CI pass. Otherwise propose
`First iteration - testing` with exact failures and risks. A final Terra review
may return one compatible correction; it consumes an available cycle. Never merge
the PR.

The report records behavior, design/assumptions, cycle count, game scenarios and
artifacts, per-game narratives/policy advice, old-control results, diagnostics,
performance, checks/CI, deferred work, and risks.

## Cycle journal

| Cycle/model | Commit/change | Game 1 hypothesis/result/analysis | Game 2 hypothesis/result/analysis | Checks | Decision |
|---|---|---|---|---|---|

## Handoff receipt

- Proposed status:
- Branch/head and PR/checks:
- Cycles/models used:
- Acceptance/adversarial/final-regression evidence:
- Old-control comparative result:
- Per-game narrative and policy-review paths/conclusions:
- Cycle-3 code review/disposition:
- Policy recommendations/disposition: accepted recommendation -> next test/change;
  rejected recommendation -> concrete reason and replacement test:
- Diagnostic/performance result:
- Deferred work and known risks:
