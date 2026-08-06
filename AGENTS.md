# LibertyDawn Codex guidance

- Command & Conquer (`cnc`) is the supported mod. Do not build, test, package, or change Red Alert, Dune 2000, or Tiberian Sun unless shared engine compilation requires it.
- For coordinated autonomous CNC development, use `.agents/skills/coordinate-cnc-development/SKILL.md`. The coordinator reads only its coordinator state and delegates task-sheet, spec, worker, review, and integration context to focused agents.
- A delegated worker launched with `COORDINATED-CNC-ROUNDS/<round>/WORKER-*/STATE.md` must read and work that state file as its complete assignment. It must not select another task or read the full task sheet, coordinator state, or another worker spec. Reading another task PR's commits is allowed when its own state identifies a dependency. During integrated testing it may work on the task-scoped repair branch recorded in its updated state; this does not authorize unrelated release-branch edits.
- Only a `make-cnc-task` role edits `AUTONOMOUS-CNC-TASKS.md`. Only the coordinator edits `COORDINATED-CNC-STATE.md`. Workers edit only their task branch, own worker state, and task report.
- The previous single-agent autonomous skill is retained passively under `.agents/old-skills/autonomous-cnc-coding/` for historical reference; do not invoke it for new work.
- For substantial non-autonomous coding work outside a coordinated worker assignment, use the portable repository skill at `.agents/skills/coding-workflow/SKILL.md`. Do not combine it with a coordinated autonomous round.
- Resume coordinated work with: `$coordinate-cnc-development resume the active round from COORDINATED-CNC-STATE.md`.
- Never push directly to `bleed`. Use cumulative task branches and pull requests as recorded in the state file.
- Keep raw game logs, replays, saves, build output, and local worktrees out of Git. Preserve concise evidence and paths in the task report instead.
