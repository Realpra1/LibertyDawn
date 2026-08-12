---
name: review-cnc-pr
description: Review a Liberty Dawn CNC worker diff at its cycle-3 checkpoint or a completed task/release PR for correctness, regressions, code quality, technical debt, readability, determinism, and simulation CPU cost. Use for one advisory Luna review and independent Terra task/release gates.
---

# Review CNC Worker Code

The job must declare one mode:

- `cycle`: use a fresh Luna 5.6 medium session after isolated product-change
  cycle 3. Review the cumulative current diff and
  latest evidence while the worker can still react inside its normal budget.
- `final`: use a fresh Terra 5.6 medium session for a completed task or integrated
  release PR.

Do not modify Git, product code, worker state, task-sheet state, coordinator
state, or a PR; write only the requested review file. Never create tasks, expand
scope, recommend balance changes outside explicit authority, or optimize for a
preferred test result.

In `cycle` mode, read only the assigned worker state, cumulative diff from its
recorded base, relevant surrounding code, and evidence through the named cycle.
Return at most one high-value compatible concern. Do not penalize final evidence,
CI, publication, or later adversarial cases merely because they are not due yet.
The worker records whether it adopts or rejects the concern; an adopted product
change starts the next ordinary code-change cycle. This advisory review does not
replace the final gate or grant extra cycles.

In `final` mode, the job identifies `task` or `release`. For a task PR, read its
worker state/report, diff, commits, evidence, and checks. For the integrated PR,
read integration state, combined diff/commits, included task review receipts,
combined evidence, and checks. Read an individual worker spec during release
review only when a concrete conflict or failure requires its contract.

1. Read applicable `AGENTS.md` and only the artifacts allowed by the selected
   mode above.
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
   Treat any process that fails or hangs before world tick 1 as an invalid test
   setup, not a gameplay loss. Check that the worker attempted to repair the
   launch/content/runtime path and did not count repeated identical startup
   failures as completed game cycles.
6. Support performance blockers with a credible hot-path argument or measurement;
   do not block on speculative micro-optimization.
7. In `final` mode, verify that every cycle has at least two distinct custom
   full-engine games capped at 120 seconds wall-clock with all features, all AI
   modules, and ordinary enemy AIs enabled. Verify matched differential,
   contention, final acceptance regression, save/load where relevant,
   diagnostic cleanup, valid map-start/world-tick evidence, and required checks
   satisfy the worker contract. Missing
   or unexercised evidence is a finding, not an assumption.
8. In `final` mode, require the first behavioral test after implementation to be a full-engine
   ordinary-AI simulation, normally headless MAX. Reject unit-test-only early
   cycles, passive/custom-bot substitution, or delayed game testing when the full
   simulation could have supplied cheap feedback from cycle 1.
9. In `final` mode, for an AI strategy/policy change, require a valid old-behavior control: prefer
   the same build with the feature disabled, otherwise the recorded base or named
   older control commit. Verify matched content, map, faction, seed, starts,
   options, initial state, and opponents plus task-relevant outcome/quality
   metrics. Treat unexplained repeated loss, parity, or marginal improvement as a
   likely correctness or strategic-policy defect and block completion; feature
   activation logs do not prove benefit. When infrastructure allowed it, confirm
   the first behavioral test paired changed and old behavior.
10. In `final` mode, reject a test portfolio made of repeated happy paths. Except for one initial
   full-engine harness/basic-path smoke, require every unit/integration/game test
   to state a
   credible failure hypothesis, harder or different perturbation, expected failure
   signal, and observed pass/failure evidence. Confirm difficulty increased as
   soon as behavior first worked and that unexpected results changed the next
   test or implementation decision.
11. In `final` mode, verify every game received its own fresh Luna Commenter and
   Luna Policy Reviewer before the next worker decision. For AI-policy specs,
   require the recorded partial-spec Sol-high consultation. Permit at most one Sol-xhigh
   policy escalation, only after the recorded tenth game test. Flag leaked source,
   logs, full task/spec context, or outcome-driven rewriting across the Policy
   Reviewer's design-document-plus-short-task-context-plus-narrative boundary.
12. In `final` mode, treat Commenter and Policy Reviewer output as interpretation, not completion
   evidence. Confirm the worker checked cited facts, documented adopted/rejected
   advice, and validated recommendations through later adversarial full-AI games.
13. List findings by severity with file/line, failure mechanism, affected spec
   clause, and smallest safe correction. Avoid cosmetic preferences.
14. In `cycle` mode, nominate at most one `advisory_concern`, using `none` when no
   worthwhile issue exists. In `final` mode, nominate one `required_fix`: the
   highest-impact correction compatible with the task, or `none`. Critical
   compile, corruption, security, or deterministic-simulation failures remain
   release blockers even though the ordinary final review-response budget is one
   code/test cycle.
15. The worker may reject a finding with concrete evidence. Record the disagreement
   rather than arguing indefinitely.

Write the requested review file. For `cycle`, return only verdict (`clear` or
`advisory concern`), `advisory_concern`, and its path. For `final`, return only
verdict (`ready`, `ready with one fix`, or `blocked`), `required_fix`, and its path
to the coordinator.
