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
| Reviewer | Sol 5.6 high | One task PR, its worker state, evidence |
| Integrator | Sol 5.6 high | Reviewed branch heads and integration state |

Prefer native fresh agents while slots exist. Because the native four-agent limit
includes the coordinator, use `scripts/launch_role.py` for additional independent
Codex sessions. Do not share a mutable worktree. The launcher tells each session
to read/work its role-instruction file and job file without asking the coordinator
to preload that role. Its worker prompt points only at the worker state, which is
the worker's complete contract.
Put launcher event/process output under the ignored `.worktrees/` coordination
area; keep only concise durable results in tracked state.

The external launcher pins `danger-full-access` with approval policy `never` so an
unattended worker can build, game-test, push its assigned branch, and open its PR.
Use it only with an exact authorized Liberty Dawn worktree and task-local job file;
never point it at a home directory, workspace root shared by another writer, or an
unrelated repository.

Use exact model IDs `gpt-5.6-terra` and `gpt-5.6-sol`. Use the role's reasoning
effort from the table. Do not silently substitute a weaker model; record the
blocker or receive a user override.

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
   `STATE.md` using the dedicated spec skill and records cross-task/PR concerns
   when the Task Reader identifies them.
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
- Run `scripts/with_resource_slots.py` around shared builds and game batches.
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
2. Launch one fresh Reviewer per PR after the worker finishes. Give the reviewer
   only that PR, state/spec, and evidence. Return its single highest-impact
   compatible correction to the worker for at most one review-response code/test
   cycle. Record disagreements; review never replaces CI or runtime evidence.
3. After reviews and required task-PR checks, launch the Integrator. It creates one
   stable `agent/cnc-<round>-release` branch from the common base and locally
   merges the reviewed feature heads with merge commits. Its initial head is RC1.
   Do not invoke GitHub's merge action on the individual PRs.
4. Open one draft release PR from that stable branch to `bleed`. Keep source PRs
   open and visible.
5. Ask the five workers to test the combined candidate for up to three
   code-change cycles each. Stop workers whose relevant combined tests pass.
6. Put required release fixes on individual repair branches based on the current
   release head. Merge reviewed repair heads back into the same stable release
   branch; each new tested head becomes RC2, RC3, or RC4 and automatically updates
   the existing release PR. Repeat at most four candidate rounds, providing at
   most twelve merged-branch cycles per task and 32 total cycles per task including
   its 20 isolated cycles.
7. Reactivate a stopped worker when a later repair touches its subsystem or
   invalidates its evidence.
8. When the final candidate passes, have the Task Maker finalize task-sheet
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
