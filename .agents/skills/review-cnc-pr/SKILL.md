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
   brittle test-only behavior, and unrelated changes. Treat balance as frozen
   unless the worker contract expressly authorizes the exact surface; block cost,
   HP, damage, armor, speed, timing, power, prerequisite, probability, resource,
   or similar tuning used to make unrelated acceptance evidence pass.
3. Enforce separation of concerns and cohesion. Flag mixed responsibilities,
   oversized or deeply nested classes/functions, unclear ownership, duplicated
   policy, misleading names, hidden mutable state, and abstractions that make the
   behavior harder to test or reason about. Prefer short focused methods where
   practical, but do not demand churn or arbitrary line-count limits.
4. Inspect error handling and diagnostics. Require actionable handled errors and
   warnings at the correct boundary; reject swallowed exceptions, false-success
   fallbacks, unbounded/per-tick log spam, and temporary diagnostics left noisy.
   Confirm tests cover material failure/recovery paths.
5. Treat runtime game evidence and deterministic checks as evidence, not author
   confidence. A code review does not replace builds, tests, CI, profiling, or a
   real match.
6. Support performance blockers with a credible hot-path argument or measurement;
   do not block on speculative micro-optimization.
7. Verify that real-AI/MAX, matched differential, contention, three clean
   adversarial scenarios, final acceptance regression, save/load where relevant,
   diagnostic cleanup, and required checks satisfy the worker contract. Missing
   or unexercised evidence is a finding, not an assumption.
8. Require the first behavioral test after implementation to be a full-engine
   ordinary-AI simulation, normally headless MAX. Reject unit-test-only early
   cycles, passive/custom-bot substitution, or delayed game testing when the full
   simulation could have supplied cheap feedback from cycle 1.
9. For an AI strategy/policy change, require a valid old-behavior control: prefer
   the same build with the feature disabled, otherwise the recorded base or named
   older control commit. Verify matched content, map, faction, seed, starts,
   options, initial state, and opponents plus task-relevant outcome/quality
   metrics. Treat unexplained repeated loss, parity, or marginal improvement as a
   likely correctness or strategic-policy defect and block completion; feature
   activation logs do not prove benefit. When infrastructure allowed it, confirm
   the first behavioral test paired changed and old behavior.
10. Reject a test portfolio made of repeated happy paths. Except for one initial
   full-engine harness/basic-path smoke, require every unit/integration/game test
   to state a
   credible failure hypothesis, harder or different perturbation, expected failure
   signal, and observed pass/failure evidence. Confirm difficulty increased as
   soon as behavior first worked and that unexpected results changed the next
   test or implementation decision.
11. Verify a fresh Terra Commenter produced a factual narrative after every
   materially judged match/batch and that every AI-policy narrative received a
   fresh Terra Policy Review before the next worker decision. For AI-policy specs,
   require the recorded Sol-high spec consultation. Permit at most one Sol-xhigh
   policy escalation, only after the recorded tenth game test. Flag leaked source,
   logs, full task/spec context, or outcome-driven rewriting across the Policy
   Reviewer's design-document-plus-short-task-context-plus-narrative boundary.
12. Treat Commenter and Policy Reviewer output as interpretation, not completion
   evidence. Confirm the worker checked cited facts, documented adopted/rejected
   advice, and validated recommendations through later adversarial full-AI games.
13. List findings by severity with file/line, failure mechanism, affected spec
   clause, and smallest safe correction. Avoid cosmetic preferences.
14. Nominate one `required_fix`: the highest-impact correction compatible with the
   task. Use `none` when no worthwhile issue exists. Critical compile, corruption,
   security, or deterministic-simulation failures remain release blockers even
   though the ordinary review-response budget is one code/test cycle.
15. The worker may reject a finding with concrete evidence. Record the disagreement
   rather than arguing indefinitely.

Write the requested review file and return only verdict (`ready`, `ready with one
fix`, or `blocked`), `required_fix`, and its path to the coordinator.
