# LibertyDawn Codex guidance

- Command & Conquer (`cnc`) is the supported mod. Do not build, test, package, or change Red Alert, Dune 2000, or Tiberian Sun unless shared engine compilation requires it.
- For autonomous CNC development, use the repository skill at `.agents/skills/autonomous-cnc-coding/SKILL.md` and treat `AUTONOMOUS-CNC-TASKS.md`, `AUTONOMOUS-CNC-STATE.md`, `DEFERRED_WORK.md`, and `AUTONOMOUS-CNC-REPORTS/` as the durable source of truth.
- Resume autonomous work with: `$autonomous-cnc-coding resume the next eligible task from AUTONOMOUS-CNC-TASKS.md`.
- Never push directly to `bleed`. Use cumulative task branches and pull requests as recorded in the state file.
- Keep raw game logs, replays, saves, build output, and local worktrees out of Git. Preserve concise evidence and paths in the task report instead.
