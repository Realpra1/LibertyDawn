---
name: review-cnc-pr
description: Review one completed Liberty Dawn CNC task pull request against its isolated worker specification and evidence, focusing on correctness, regressions, code quality, technical debt, readability, determinism, and simulation CPU cost. Use as the single independent review gate before a task branch enters a coordinated release candidate.
---

# Review One CNC PR

Use a fresh Sol 5.6 high session. Do not modify Git, product code, task-sheet
state, coordinator state, or the PR; write only the requested review file.

1. Read applicable `AGENTS.md`, the assigned worker state/spec, PR diff and commits,
   relevant surrounding code, test evidence, required checks, and no other worker
   specs.
2. Verify observable requirements and forbidden behavior before style. Look for
   hidden queue/order contention, save/load or replay-state omissions, nondeterminism,
   unbounded per-tick work, excess allocations/scans/logging, duplicated policy,
   brittle test-only behavior, and unrelated changes.
3. Treat runtime game evidence and deterministic checks as evidence, not author
   confidence. A code review does not replace builds, tests, CI, profiling, or a
   real match.
4. Support performance blockers with a credible hot-path argument or measurement;
   do not block on speculative micro-optimization.
5. List findings by severity with file/line, failure mechanism, affected spec
   clause, and smallest safe correction. Avoid cosmetic preferences.
6. Nominate one `required_fix`: the highest-impact correction compatible with the
   task. Use `none` when no worthwhile issue exists. Critical compile, corruption,
   security, or deterministic-simulation failures remain release blockers even
   though the ordinary review-response budget is one code/test cycle.
7. The worker may reject a finding with concrete evidence. Record the disagreement
   rather than arguing indefinitely.

Write the requested review file and return only verdict (`ready`, `ready with one
fix`, or `blocked`), `required_fix`, and its path to the coordinator.
