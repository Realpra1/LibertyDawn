---
name: integrate-cnc-release
description: Combine reviewed Liberty Dawn CNC task branches from a common checkpoint into one stable release branch with successive tested candidate heads, coordinate merged-branch adversarial testing and repair PRs, and prepare one final product release PR to bleed. Use after the individual PRs in a coordinated autonomous CNC development round finish implementation and review.
---

# Integrate a CNC Release

Use Sol 5.6 high. Read applicable `AGENTS.md`, the integration job, reviewed branch
heads, PR/check summaries, and worker status receipts. Read a worker spec only when
a merge conflict or integrated failure requires it.

## Build the candidate

1. Verify every supplied feature branch descends from the recorded common base and
   has a unique task PR, worker status, review verdict, and required checks.
2. Include safe `Complete - testing` and explicitly accepted `First iteration -
   testing` branches. Record exclusions; never smuggle an unreviewed branch into
   the release.
3. Create the stable branch `agent/cnc-<round>-release` from the common base.
   Locally merge each reviewed feature head with a merge commit in recorded order;
   the resulting head is RC1. Do not call GitHub's merge action on source PRs.
4. Resolve mechanical conflicts. Return behavioral conflicts to the responsible
   worker or a focused repair agent; do not guess silently.
5. Run strict build/unit/static checks and inspect the combined diff. Push the
   stable release branch and open one draft release PR targeting `bleed`. Keep
   source PRs open. Every later RC updates this same branch and PR.

## Integrated test rounds

For each candidate, ask each relevant original worker to run up to three
code-change cycles against the combined branch. Each uses a separate worktree and
the existing task state plus current integration notes. A worker that passes may
stop; reactivate it if later fixes touch its behavior.

When fixes are needed:

1. Add the current release head, integration-note path, repair branch, repair PR
   base, and separate integrated-cycle counters to the worker's durable state.
2. Create one repair branch per responsible worker from the current release head,
   with its own PR targeting the stable release branch.
3. Keep repairs task-scoped. Run the worker's focused and adversarial regression.
4. Locally merge the reviewed repair heads into the stable release branch. The
   resulting release head is the next RC and updates the existing release PR.
5. Rerun combined build/check gates and all materially affected game scenarios.

Repeat at most four candidate rounds. This provides at most twelve combined-branch
cycles per task after its twenty isolated cycles. If the cap is reached, publish
the safest proven subset/result and report unresolved tasks as first iteration.

## Completion

- Require the combined candidate to preserve every included task's literal
  acceptance behavior, forbidden behavior, clean adversarial evidence, and
  required checks.
- Require a real full-engine ordinary-AI regression and natural conclusion where
  relevant. Use shared matches for multiple tasks only when each task's scenario
  and final outcome are actually exercised and evidenced.
- Use the global build/game slots and isolated MAX-game support directories.
- Record release heads, merge order, conflicts, tests, repairs, exclusions, and
  remaining risks in the integration state.
- Send final structured receipts to the Task Maker. Promote the draft release PR
  to the product release PR, but never merge it into `bleed`; the user does that.

If build/game concurrency proves unreliable, reduce it immediately and submit a
high-priority orchestration-fix task through the Task Maker.
