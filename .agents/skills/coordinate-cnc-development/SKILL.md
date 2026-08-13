---
name: coordinate-cnc-development
description: Coordinate parallel autonomous Command & Conquer development using fresh task-reader, task-maker, speccer, worker, reviewer, and integration agents with isolated context, Git worktrees, durable worker-state files, queued headless MAX testing, individual task PRs, and a final release PR. Use when Codex should run or resume a multi-task autonomous CNC development round without loading the task sheet or task implementation details into the parent coordinator.
---

# Coordinate CNC Development

Run this skill from a Terra 5.6 medium session. A skill cannot change its current
model; record a mismatch in coordinator state and use exact model selection for
new sessions.

## Boundaries

- Read applicable `AGENTS.md`, this skill, and `COORDINATED-CNC-STATE.md`.
- Do not read the task sheet, worker specs, task reports, code diffs, or role-skill
  bodies into coordinator context. Route those artifacts to fresh role sessions.
- Do not ask implementation or preference questions. Let the focused role inspect
  the repository, choose the strongest safe option, and record material assumptions.
- Do not exceed granted authority. Record credentials, permission, missing-file,
  unsafe-path, or unavailable-model blockers and stop only the affected workstream.
- Treat conversation as control input. Answer status questions and record notes,
  then continue unless the user explicitly pauses or a real blocker prevents work.
- Never push to or merge `bleed`. The user promotes the final release PR.
- Keep task-sheet writes single-owner: only a Task Maker session may edit it.
- Keep coordinator-state writes single-owner: only the coordinator may edit it.
- Let workers update only their own state/report and code branch.
- Treat balance as frozen throughout specification, implementation, review, and
  integration unless the selected task expressly authorizes the exact balance
  surface. Never permit tuning values to manufacture behavioral improvement.
- Preserve unrelated local work. Fetch before trusting remote branch state; never
  discard or rewrite an unknown change to manufacture a clean checkpoint.

For a long unattended round, use a reversible session-scoped keep-awake mechanism
when the host permits it. Record its process identity and restore normal sleep
behavior whenever the round pauses or ends; never change a permanent power plan.

## Durable layout

Use:

```text
COORDINATED-CNC-STATE.md
COORDINATED-CNC-ROUNDS/<round-id>/
  TASK-READER-<worker>.md
  TASK-PACKET-<worker>.md
  REVIEW-<worker>.md
  INTEGRATION.md
  WORKER-<worker>-<task>/STATE.md
  WORKER-<worker>-<task>/REPORT.md
```

Keep `.agents/references/LIBERTY-DAWN-DESIGN.md` as the shared strategic reference.
Store detailed per-match narratives and policy reviews under the ignored round
analysis area, for example `.worktrees/coordinated-cnc/<round>/analysis/<worker>/`;
commit concise conclusions and paths in worker state/report rather than flooding
Git with every generated analysis.

Commit local state during normal task/release pushes. A task branch contains only
its own worker state. After task and release PRs are merged or closed, remove
obsolete active-round files in an ordinary cleanup PR after preserving concise
results in task reports and coordinator state. Keep raw logs, saves, replays, and
build artifacts in ignored paths.

## Fresh role sessions

Use a fresh context whenever practical:

| Role | Model | Input |
|---|---|---|
| Task Reader | Terra 5.6 medium | Task sheet, exclusions, output packet path |
| Task Maker | Terra 5.6 medium | User request/status receipt, task sheet |
| Speccer | Sol 5.6 xhigh | One task packet, repository, worker-state output path |
| Worker | Sol 5.6 high | One worker `STATE.md` only |
| Commenter | Terra 5.6 medium | Assigned match/control logs and optional design doc |
| Policy Reviewer | Terra 5.6 medium | Design doc, short task context, match narrative |
| Spec Policy Reviewer | Sol 5.6 high | Design doc, short task context, proposed-policy narrative |
| Escalated Policy Reviewer | Sol 5.6 xhigh | Once after at least ten persistent-problem game tests |
| Cycle Reviewer | Terra 5.6 medium | One worker state, cumulative scoped diff, and evidence through cycle 5/10/15/20 |
| Reviewer | Sol 5.6 high | One task PR, its worker state, evidence |
| Integrator | Sol 5.6 high | Reviewed branch heads and integration state |

Prefer native fresh agents while slots exist for roles other than Commenter and
Policy Reviewer. Spawn those native delegated roles with no inherited conversation
history. Always launch `commenter`, `policy-reviewer`, `policy-speccer`, and
`policy-escalation` through `scripts/launch_role.py`, even when a native slot is
free. The caller selects only the role and strict job envelope; it must not
reconstruct or override model, reasoning, sandbox, output, or session settings.
Because the native four-agent limit includes the coordinator, use the same launcher
for additional independent sessions of other roles. Do not share a mutable
worktree. The launcher tells each session to read/work its role-instruction file
and job file without asking the coordinator to preload that role. Its worker
prompt points only at the worker state, which is the worker's complete contract.
Put launcher event/process output under the ignored `.worktrees/` coordination
area; keep only concise durable results in tracked state.

The external launcher pins `danger-full-access` with approval policy `never` so an
unattended worker can build, game-test, push its assigned branch, and open its PR.
Use it only with an exact authorized Liberty Dawn worktree and task-local job file;
never point it at a home directory, workspace root shared by another writer, or an
unrelated repository.

Commenter and Policy Reviewer sessions instead run from their dedicated output
directory under `workspace-write` with approval policy `never`; they cannot mutate
the repository worktree. Keep each such output directory exclusive to one running
role.

Use exact model IDs `gpt-5.6-terra` and `gpt-5.6-sol`. Use the role's reasoning
effort from the table. Do not silently substitute a weaker model; record the
blocker or receive a user override.

Commenter and Policy Reviewer launcher jobs must be strict JSON path envelopes
stored directly in their output directory. Commenter keys are `artifacts`, optional
`design_reference`, and `output`; its output is `NARRATIVE.md`. Policy-role keys
are exactly `design_reference`, `task_context`, `narrative`, and `output`; its output is
`POLICY-REVIEW.md`. The launcher rejects extra inline context, a different design
document, missing inputs, relative paths, or outputs outside the role directory.
Before launch, copy only authorized Commenter game evidence into that role
directory's `inputs/` subtree. Copy the one proposed or factual narrative to the
Policy Reviewer directory as `inputs/NARRATIVE.md`; do not use symlinks. Stage a
short `inputs/TASK-CONTEXT.md` containing task identity, why, change category,
in/out-of-scope behavior, and explicit balance authority. The
launcher rejects analysis inputs outside these staged roots.
Run the launcher with `--validate-cli` for a no-agent smoke against the installed
Codex parser. This retains every constructed option, substitutes parser help only
for the free-form prompt, and proves a legacy approval option is rejected. Use
`--background` or foreground execution for the real isolated role; there is no
native analysis-role bypass.

## Start a round

1. Fetch remotes without discarding local changes. Choose and record one exact
   base commit for all five task branches.
2. Create a round ID and coordinator entry from
   `assets/COORDINATOR-STATE.template.md`.
3. For worker slots 1 through 5, launch a fresh Task Reader one at a time. Give it
   the already-selected task IDs so it cannot duplicate them. Receive one task
   packet only; do not read the packet yourself. Immediately and atomically record
   the returned task ID, worker slot, packet path, selection time, and `claimed`
   phase in coordinator state before launching another reader. Treat every durable
   claim and active task PR as excluded after restart. Once the batch is claimed,
   have a Task Maker mark those rows `in progress`.
4. Launch a fresh Speccer for each packet, one at a time. It creates that worker's
   `STATE.md` using the dedicated spec skill, consults one Sol-high Policy Reviewer
   through a proposed-policy narrative for AI-policy work, and records useful
   policy and cross-task/PR concerns.
5. Create five branches and worktrees from the recorded base. Put only the
   assigned state file into each task branch. A fixed five-task round is allowed;
   record dependency or overlap concerns but do not silently reorder the user's
   task sheet.
6. Launch one Sol-high worker per state file. Use external sessions when native
   slots are exhausted. Workers may code while other workers' games run.

If fewer than five eligible tasks exist, run the available tasks rather than
inventing work. If none exist but coordinator state contains reviewed task heads
awaiting combination, launch the Integrator instead of creating an empty round. If
there is neither eligible nor pending integration work, record the empty queue and
remain ready without inventing a full-game proof requirement for an integration-
only bookkeeping pass.

## Task changes from the user

When the user adds or changes a task, launch a fresh Task Maker with the exact user
message. It either writes a ready task or preserves a draft and returns missing
questions. Relay those questions without loading the task sheet. Bug additions to
an existing task require a new or revised spec before selection.

## Resource scheduling

- Start with two total game slots and one large-build slot across all workers.
- Treat full-engine ordinary-AI simulations as cheap primary feedback, not a late
  acceptance expense. Queue them from each worker's first behavioral test and keep
  game slots busy while other workers inspect, code, build, or analyze evidence.
- After each materially judged match or paired batch, launch a fresh Terra-medium
  Commenter. For AI-policy work, pass its narrative to a fresh Terra-medium Policy
  Reviewer before the worker chooses the next change. These model sessions do not
  consume local game slots and can analyze while another simulation runs.
- After each isolated product-change cycle 5, 10, 15, and 20 that occurs, launch
  a fresh Terra-medium `cycle-reviewer` before the next product change or
  publication. Give it only that worker's state, cumulative diff from the common
  base, relevant evidence through the checkpoint, and a task-local output path.
  It returns at most one advisory concern. Require the worker to record an
  evidence-based adoption or rejection; an adopted code change consumes the next
  normal cycle. Do not grant extra cycles or replace the final Sol-high PR review.
- Run every large build or full test through the protected entry mode; callers
  select their ownership role but cannot select a filename or capacity:

  ```text
  python3 scripts/with_resource_slots.py --lock-dir <repository-root>/.agents/locks \
    --large-build-entry worker -- COMMAND...
  ```

  Reviewers use `--large-build-entry reviewer` and Integrators use
  `--large-build-entry integrator`. This covers `make`, `make all`, `make test`,
  `make check`, equivalent full `dotnet`/`msbuild` suites, packaging, and other
  comparably expensive shared-engine checks. The helper owns the canonical
  capacity-one lock and complete command-tree lifetime. After the foreground
  command exits it allows short-lived assigned descendants a bounded grace period,
  then terminates and reaps persistent build servers before releasing. Do not use
  direct `flock` or generic `--resource large-build`.
- Continue to use generic `scripts/with_resource_slots.py` reservations for game
  batches; games retain independent capacity two. `game`, `large-build`, and
  `policy-scratchpad` are registered repository-global resources. Every round and
  worktree must pass the main repository's canonical `.agents/locks` directory;
  the helper rejects alternate namespaces and caller-selected capacities before
  opening a lock. Retained JSON is last-known metadata only; use `--status` for an
  authoritative nonblocking flock snapshot of a registered resource.
- Serialize every Policy Reviewer scratchpad staging, foreground role completion,
  validation, and promotion with `--resource policy-scratchpad --capacity 1`
  against that same canonical namespace. Do not background the protected role or
  release the slot before promotion finishes.
- A worker may reserve two game slots for a two-game comparison. Use the existing
  `launch-ai-parallel.py` inside the reservation.
- Keep every game's support directory, logs, saves, replay, map artifact, port,
  benchmark prefix, and display isolated.
- If measured contention makes tests unreliable, reduce concurrency. If the
  orchestration itself needs code changes, send a high-priority task request to a
  Task Maker; do not let every worker patch the coordinator ad hoc.

## Review and release flow

1. Each worker opens one task PR from its isolated branch and proposes `Complete -
   testing` or `First iteration - testing` in its state.
2. Confirm every due cycle-5/10/15/20 Terra review and worker disposition is
   recorded. Missing an advisory checkpoint does not authorize a retroactive
   code change outside the cycle budget; complete it before publication when
   possible or record the gap for the final reviewer.
3. Launch one fresh Reviewer per PR after the worker finishes. Give the reviewer
   only that PR, state/spec, and evidence. Return its single highest-impact
   compatible correction to the worker for at most one review-response code/test
   cycle. Record disagreements; review never replaces CI or runtime evidence.
4. After reviews and required task-PR checks, launch the Integrator. It creates one
   stable `agent/cnc-<round>-release` branch from the common base and locally
   merges the reviewed feature heads with merge commits. Its initial head is RC1.
   Do not invoke GitHub's merge action on the individual PRs.
5. Open one draft release PR from that stable branch to `bleed`. Keep source PRs
   open and visible.
6. Ask the five workers to test the combined candidate for up to three
   code-change cycles each. Stop workers whose relevant combined tests pass.
7. Put required release fixes on individual repair branches based on the current
   release head. Merge reviewed repair heads back into the same stable release
   branch; each new tested head becomes RC2, RC3, or RC4 and automatically updates
   the existing release PR. Repeat at most four candidate rounds, providing at
   most twelve merged-branch cycles per task and 32 total cycles per task including
   its 20 isolated cycles.
8. Reactivate a stopped worker when a later repair touches its subsystem or
   invalidates its evidence.
9. When the final candidate passes, have the Task Maker finalize task-sheet
   statuses and promote the draft release PR to the product release PR. The user
   decides whether to merge it.

## Recovery

After compaction, interruption, or a role crash, reread this skill and coordinator
state. Inspect process/result files without loading worker specs. Restart a fresh
role from its durable input rather than reconstructing its context. Never select a
new task merely because a worker temporarily stopped responding.

To stop an external role, signal the exact child `pid` in its `process.json`, wait
for the supervisor to record the final exit status, and then verify no assigned
game/build process remains. Never use a broad process-name kill.
