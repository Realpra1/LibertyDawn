# Worker State: {{TASK_ID}}

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `{{WORKER_ID}}`
- Task: `{{TASK_ID}} — {{TITLE}}`
- Status: `Specified`
- Common base branch/SHA: `{{BASE_BRANCH}}` / `{{BASE_SHA}}`
- Task branch: `{{TASK_BRANCH}}`
- Intended PR base: `{{PR_BASE}}`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `0`
- Game/build lock directory: `{{ABSOLUTE_LOCK_DIRECTORY}}`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `{{TASK_REPORT_PATH}}`
- PR: `none`

## Integrated repair assignment

- Phase: `isolated implementation`
- Current release branch/head: `not assigned`
- Integration notes: `not assigned`
- Repair branch: `not assigned`
- Repair PR base: `not assigned`
- Integrated cycles used this RC: `0/3`
- Integrated cycles used total: `0/12`

Before relaunching this worker for combined testing or repair, the integrator must
replace these fields with the exact release head, note path, branch, and counters.
During that phase, the repair branch replaces the original task branch as the
writable branch; the task scope and behavioral contract do not change.

## Why and predicted change

{{WHY_AND_PREDICTED_CHANGE}}

## Authoritative behavior

{{DESIRED_BEHAVIOR}}

## Forbidden behavior and failure signals

{{FORBIDDEN_BEHAVIOR}}

## Relevant current implementation and control behavior

{{CURRENT_IMPLEMENTATION}}

## Likely wrong approaches and challenges

{{WRONG_APPROACHES}}

## Competing systems and ownership

{{COMPETING_SYSTEMS}}

## Cross-worker dependencies

{{CROSS_WORKER_DEPENDENCIES}}

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Acceptance and tests

### Literal black-box acceptance

{{LITERAL_ACCEPTANCE}}

### Focused checks and instrumentation

{{FOCUSED_CHECKS}}

### Ordinary and differential games

{{GAME_TESTS}}

### Adversarial cases

{{ADVERSARIAL_TESTS}}

### Final regression

{{FINAL_REGRESSION}}

## Implementation rules

- Investigate and choose the smallest correct modular design. Preserve unrelated
  behavior and user changes.
- Put tunable policy in the owning rules/config/save/map layer and algorithmic
  invariants in code. Add proportionate tests and useful bounded diagnostics.
- Inventory and test ordinary modules that compete for the same units, queues,
  cash, reservations, targets, repair, or retargeting.
- Keep raw logs/replays/saves/profiles outside Git or under ignored
  `AUTONOMOUS-CNC-LOGS/`. Record concise paths, seeds, and conclusions here or in
  the task report.
- Never push directly to `bleed`, merge a GitHub PR, or edit the task sheet or
  coordinator state. Update this state and task report on the recorded task branch
  or, during integrated repair, the recorded repair branch.

## Evidence-driven loop

One cycle begins when a product-code/config change is made. A cycle may build,
run focused checks, and execute up to two materially useful games needed to judge
that change. Merely reading logs or correcting an invalid harness without a
product change does not begin another cycle; record it honestly.

For each cycle:

1. Reread this state, current diff, and previous evidence.
2. Implement or revise the smallest evidence-driven change.
3. Run focused unit/static checks and fix relevant errors or warnings.
4. Run the simplest scenario that proves the requested final observable outcome.
5. Diagnose results against desired and forbidden behavior. Add bounded
   instrumentation when evidence cannot distinguish request, reservation,
   movement, contention, state transition, and final outcome.
6. Update the cycle journal before making another code change.

Prefer the full engine and real bot types. On Linux use the explicit headless MAX
path when graphics/input are irrelevant. Prove the current run loaded the intended
map, bots, actors, options, advanced ticks, flushed evidence, and produced the
final outcome. A passive fixture or manager-only simulation is not sole proof.

Use ordinary full matches for emergent AI behavior. Include a matched differential
when the behavior can be toggled, at least one relevant resource/order contention
case, at least three distinct clean adversarial scenarios after fixes, and a final
rerun of literal acceptance with normal modules. Run at least one real full match
at MAX to a natural conclusion when relevant. Use long-distance starts for
progression and short-distance starts for rush/defense; do not waste concurrency
on near-copy spawn swaps unless position bias matters.

Wrap shared resources with:

```text
python3 .agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py \
  --lock-dir {{ABSOLUTE_LOCK_DIRECTORY}} --resource game --capacity 2 --slots 1 -- COMMAND...
```

Reserve two game slots when using a two-game `launch-ai-parallel.py` batch. Poll
background games within 60 seconds, normally cap them at 30 minutes, isolate every
runtime path, and stop them when evidence is sufficient or progress stalls.

After 20 unsuccessful code-change cycles, publish the safest useful result as
`First iteration - testing`. Do not pad cycle counts after evidence is sufficient.

When the phase is integrated testing, the isolated 20-cycle cap no longer blocks
the assigned release validation. Use at most three code-change cycles for the
current RC and at most twelve across four RCs, updating both integrated counters.
Test the exact recorded release head before changing code; put any change only on
the recorded repair branch and rerun the materially affected original acceptance,
adversarial, and combined scenarios.

## Completion and publication

Propose `Complete - testing` only after literal acceptance, all required clean
adversarial cases, final regression, task checks, report, PR, and required GitHub
checks pass. Otherwise propose `First iteration - testing` with exact failures and
risks. The reviewer and integrated release determine final status.

Push the task branch and open one individual PR. Do not merge it. When review
returns a correction, perform at most one review-response code/test cycle, applying
the highest-impact safe finding you agree with or recording evidence for rejection.

## Cycle journal

| Cycle | Commit/change | Checks/games | Observable evidence | Decision |
|---|---|---|---|---|

## Handoff receipt

- Proposed status:
- Final branch/head:
- PR and checks:
- Cycles used:
- Acceptance evidence:
- Adversarial evidence:
- Final regression:
- Known failures/risks:
- Relevant artifact paths:
