---
name: make-cnc-task
description: Validate, draft, add, amend, or update statuses in the Liberty Dawn autonomous CNC task sheet as its single writer. Use when a user proposes a CNC task or bug, supplies missing requirements, changes an active task, or a worker/coordinator submits a structured task-status receipt.
---

# Make or Update a CNC Task

Use Terra 5.6 medium. Act as the only task-sheet writer. Read applicable
`AGENTS.md`, the task sheet, the exact request/receipt, and only directly linked
task material.

The active task sheet is for executable and pending work. Keep completed-release
history in the append-only root file `AUTONOMOUS-CNC-TASK-HISTORY.md`. No agent
may read, search, summarize, or use that history for task selection or policy;
the Task Maker may append one new line when explicitly processing a release
handoff, without reading prior history. The coordinator records only the path
and routing outcome.

## Readiness gate

Do not place a new task in the executable queue until it states:

- Expected high-level player-visible behavior.
- Error signals or wrong implementations.
- Predicted change from current behavior.
- Why the task should be done.

Record explicit balance authority. If the user did not expressly request a
balance change, write that balance is frozen; never infer tuning permission from
a desired gameplay or AI outcome.

A bug report additionally requires:

- What happened.
- Why it is wrong.
- What should have happened instead.

Preserve an incomplete request as `draft`; do not discard it. Return concise,
specific questions for missing fields. Promote the draft to `pending` only when
the answers satisfy the gate.

## Existing tasks and statuses

- Append a related bug to its original history, but queue a new regression task
  or a revised version that must be respecced. Never silently change a worker's
  active contract.
- Accept worker proposals `Complete - testing` and `First iteration - testing` as
  pre-integration states when their receipt includes cycle count, branch, PR,
  evidence paths, passing/failing acceptance conditions, and known risks.
- Accept final `complete` or `first iteration` only from the coordinated release
  handoff after integrated testing and required checks.
- Preserve task IDs and history. Do not rewrite completed evidence to make a new
  result appear cleaner.
- When a coordinated release handoff confirms that a task is complete and the
  cumulative release PR has actually merged into `bleed`, append exactly one
  concise line to `AUTONOMOUS-CNC-TASK-HISTORY.md` containing its task ID, task
  name, and one-line description of the delivered behavior. Then remove that
  completed task entry from `AUTONOMOUS-CNC-TASKS.md`; retain detailed evidence
  in its worker/report/state artifacts. Never remove a task merely because it
  says `Complete - testing`, has an open PR, or is only in a release candidate.
- Perform the append and task-sheet removal as one intentional handoff update;
  do not migrate unfinished, rejected, superseded, or unmerged tasks.
- Make the smallest task-sheet edit and report the exact changed rows/sections.

Never inspect or modify product code, worker branches, or integration branches.
