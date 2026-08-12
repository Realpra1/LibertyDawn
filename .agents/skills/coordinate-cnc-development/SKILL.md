---
name: coordinate-cnc-development
description: Coordinate parallel autonomous Command & Conquer development through isolated task intake, specification, one-cycle workers, match narration, policy review, code review, integration, and a cumulative release PR. Use to run or resume a multi-task CNC round without loading task or implementation detail into the coordinator.
---

# Coordinate CNC Development

Run the coordinator as `gpt-5.6-luna` medium. It routes durable files and process
results; it does not solve tasks. If the current session uses another model,
record that fact and pin every launched role exactly.

## Boundaries

- Read only applicable `AGENTS.md`, this skill, and `COORDINATED-CNC-STATE.md`.
  Never read the task sheet, task packets, worker states/reports, diffs, or role
  skill bodies; fresh roles own those contexts.
- Only the coordinator edits coordinator state. Only Task Maker edits the task
  sheet. Workers edit only their branch, state, and report.
- Treat user messages as control input. Answer questions and continue unless the
  user explicitly pauses. A question, observation, worker idea, test anomaly, or
  reviewer suggestion is not permission to create a task.
- Create or amend a task only after the user explicitly asks. Put worthwhile
  unrequested ideas in `DEFERRED_WORK.md`; do not route them to Task Maker.
- Freeze balance unless the selected task expressly authorizes the exact surface.
- Never push or merge `bleed`. Preserve unrelated work and use isolated worktrees.

After compaction, interruption, or a test cycle, reread this skill and coordinator
state. Restart a fresh role from its durable input instead of reconstructing its
context.

## Roles and models

| Role | Model | Scope |
|---|---|---|
| Coordinator | Luna medium | Routing and state only |
| Task Reader | Luna medium | Select one task packet |
| Task Maker/updater | Luna medium | Sole task-sheet writer |
| Task Intake Reviewer | Terra medium | Ask exactly two pre-creation questions |
| Speccer | Sol high | Partial spec, consultation, final worker state |
| Spec Policy Reviewer | Sol high | Answer speccer questions |
| Worker cycle 1 | Sol high | Initial implementation and two games |
| Worker cycles 2-5 | Terra medium | Evidence-led correction and two games |
| Worker optional cycles 6-15 | Luna medium | Minor obvious fixes/testing only |
| Match Commenter | Luna medium | One factual narrative per game |
| Match Policy Reviewer | Luna medium | One policy review per game |
| Cycle-5 Reviewer | Terra medium | One advisory code concern |
| Final Reviewer | Sol high | Independent PR/release gate |
| Integrator/Merger | Terra medium | Merge and release coordination |
| Integration worker/tester | Luna medium | Combined testing and minor fixes |

Use exact model IDs `gpt-5.6-luna`, `gpt-5.6-terra`, and `gpt-5.6-sol` through
`scripts/launch_role.py`. Use fresh no-history agents where practical; use the
launcher when native slots are full. Never share a mutable worktree.

Analysis roles run in exclusive output directories under `workspace-write`.
Commenter jobs contain only `artifacts`, optional `design_reference`, and
`output`. Policy jobs contain exactly `design_reference`, `task_context`,
`narrative`, and `output`. Stage regular-file copies under `inputs/`; never pass
source, diffs, full specs, or preferred conclusions. Serialize policy calls with
the shared `policy-scratchpad` slot, stage the canonical scratchpad, and promote a
valid replacement of at most 3,000 Unicode characters atomically.

## Task intake

For a new task explicitly requested by the user:

1. Stage its short request as task context and launch a fresh Terra-medium
   `task-intake-reviewer`. It asks exactly two high-value questions about expected
   behavior, wrong outcomes, predicted change, why, scope, or bug facts.
2. Relay those questions. Do not create the task until the user answers them.
3. Launch a fresh Luna-medium Task Maker with the request, answers, and intake
   receipt. It validates and writes the smallest task-sheet change.

Status receipts and amendments to an existing authorized task do not require the
two-question new-task gate. Normal questions never enter this flow.

## Start or resume a five-task round

1. Fetch safely and record one exact common base for all task branches.
2. Launch fresh Task Readers sequentially. After each returned ID, atomically
   record its claim before selecting another. The coordinator reads only returned
   ID/title/path/blocker metadata.
3. Have Task Maker mark the claimed rows `in progress`.
4. Launch one fresh Sol-high Speccer per task. The Speccer investigates, writes a
   partial spec, consults a fresh Sol-high Policy Reviewer with the task plus
   partial spec and focused questions, then completes the worker state.
5. Create one branch/worktree per task from the common base and launch cycle 1 as
   `worker-sol`. Relaunch each later cycle from its durable state: `worker-terra`
   for cycles 2-5 and, only when allowed below, `worker-luna` for cycles 6-15.

Run fewer than five tasks when fewer are eligible; never invent work. Workers may
code while other workers' simulations or analysis roles run.

## Development-cycle contract

A worker invocation performs exactly one code-change/test cycle, updates durable
state, and exits. A cycle includes at least two distinct, adversarial full-engine
games. Each game:

- uses a deliberately constructed custom scenario;
- runs at most 120 seconds wall-clock, normally headless MAX;
- enables all game features and all AI modules;
- includes ordinary enemy AIs, not passive test bots;
- tests a different assumption, pressure, topology, timing, resource state,
  control, or failure mode from the other game; and
- gets its own fresh Luna Commenter and its own fresh Luna Policy Reviewer before
  the next code decision. Never combine two games into one analysis call.

Cycle 1 is the substantial Sol-high implementation. Cycles 2-5 are Terra-medium
corrections to observed bugs or wrong assumptions, not redesign invitations. At
cycle 5, run one Terra-medium cycle code review. If the task is still not close to
the spec, report `Needs help`/`First iteration - testing`; five failed rounds are
evidence that the approach or spec needs human or stronger-agent help.

The coordinator may authorize up to ten extra cycles (6-15) only when remaining
work is minor and obvious. Luna workers, narrators, and policy reviewers handle
this tail. Luna workers may fix a narrow defect, assertion, guard, configuration
mistake, or test setup; they must not introduce new architecture, policy, balance,
or broad refactors. Stop the tail and request help as soon as the fix is no longer
obvious. Never exceed 15 isolated cycles.

For old/new performance comparisons, use matched custom scenarios with many
pre-spawned units and structures (normally two Iron Reapers with at least 300
units plus structures each). Cap every leg at 120 seconds and compare pre-Codex,
newest with advanced squad modules disabled, and newest with them enabled.

## Resource use

- Start with two game slots and one large-build slot; try three game slots only
  after measured contention shows evidence remains valid.
- Reserve slots with `with_resource_slots.py`; isolate support directories, ports,
  maps, logs, saves, replays, benchmarks, and displays.
- Use the game launcher/supervisor as the completion helper. Start the bounded
  foreground job and wait for its process completion/result file; do not spend
  agent turns sleeping or repeatedly polling. If the execution tool yields, use
  one blocking wait/resume mechanism rather than reasoning between polls.
- Reduce concurrency when timing or evidence is contaminated. Record an
  orchestration problem in deferred work unless the user explicitly requests a
  task for it.

## Review and release

1. Each worker opens one task PR and proposes `Complete - testing` or `First
   iteration - testing` with evidence and remaining risks.
2. Launch one fresh Sol-high final Reviewer per PR. Return its single highest
   compatible correction for at most one response cycle using the model tier
   appropriate to the current phase.
3. Launch the Terra-medium Integrator after reviewed PRs and checks are ready. It
   merges feature heads locally into one stable release branch and opens one draft
   PR to `bleed`; source PRs stay open.
4. Use fresh Luna-medium integration workers/testers for the combined candidate,
   with the same bounded two-games-per-cycle contract and Luna narration/policy
   review. Put fixes on task-scoped repair branches and merge reviewed fixes into
   the stable branch. Stop after five release-wide integration test/fix cycles.
5. When the candidate passes, send structured receipts to Task Maker and promote
   the draft release PR. The user decides whether to merge it.

Keep raw artifacts ignored. Store detailed narratives/reviews under the round's
ignored analysis directory and concise conclusions/paths in worker reports. The
passive task history is never read; only Task Maker appends a one-line release
record after confirmed merge to `bleed`.

To stop an external role, signal only the exact child PID in its `process.json`,
wait for its supervisor result, and verify its assigned game/build children ended.
