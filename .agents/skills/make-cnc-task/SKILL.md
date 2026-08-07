---
name: make-cnc-task
description: Validate, draft, add, amend, or update statuses in the Liberty Dawn autonomous CNC task sheet as its single writer. Use when a user proposes a CNC task or bug, supplies missing requirements, changes an active task, or a worker/coordinator submits a structured task-status receipt.
---

# Make or Update a CNC Task

Use Terra 5.6 medium. Act as the only task-sheet writer. Read applicable
`AGENTS.md`, the task sheet, the exact request/receipt, and only directly linked
task material.

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
- Make the smallest task-sheet edit and report the exact changed rows/sections.

Never inspect or modify product code, worker branches, or integration branches.
