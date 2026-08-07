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
6. Launch one fresh release-PR reviewer when a candidate appears final. Treat
   cross-task responsibility leakage, duplicated policy, conflict damage,
   nondeterminism, unbounded CPU work, noisy diagnostics, and missing integrated
   evidence as release findings. If a finding causes another repair, repeat the
   release review on the new candidate within the four-round cap.

## Integrated test rounds

For each candidate, ask each relevant original worker to run up to three
code-change cycles against the combined branch. Each uses a separate worktree and
the existing task state plus current integration notes. A worker that passes may
stop; reactivate it if later fixes touch its behavior.

Begin every integrated round with full-engine ordinary-AI simulations from test 1;
do not spend a preliminary round on unit-only confidence. Treat those games as
cheap feedback and run focused build/unit gates alongside them where resources
permit. Make every integrated test try to break the combined code. Target cross-task
interference, merge-order assumptions, shared queues/resources/actors, changed
timing, state invalidation, save/load, longer duration, and heavier unit pressure.
Do not use three repetitions of the same passing match as three cycles. After a
candidate passes one scenario, make the next materially harder or different and
record its failure hypothesis and signal in integration state.

After every materially judged integrated match or paired batch, launch a fresh
Terra-medium Commenter on the assigned artifacts. For AI-policy rounds, pass only
its narrative and the Liberty Dawn design reference to a fresh Terra-medium Policy
Reviewer. Record factual narrative, policy verdict, advice adopted/rejected, and
the harder test or repair it inspired before judging the next candidate. These
reviews inform but never replace full-engine evidence.

Use no-history sessions and the coordinator launcher's strict JSON envelopes for
both roles: Commenter gets only artifact paths, optional design-reference path, and
`NARRATIVE.md` output; Policy Reviewer gets exactly design-reference, narrative,
and `POLICY-REVIEW.md` output paths. Copy only authorized evidence into the
Commenter's `inputs/` subtree and copy its narrative (never symlink either) to the
Policy Reviewer's `inputs/NARRATIVE.md` before launch.

For every included strategic AI change, compare the release head against its
recorded feature-disabled, base-SHA, or named older-behavior control under matched
full-AI conditions. Require material improvement in scenarios that exercise the
change. Treat repeated release parity, marginal gain, or loss as likely merge
damage, regression, or bad policy and return it for repair unless concrete
task-specific evidence proves an acceptable tradeoff.

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
- After the latest relevant repair, require each included task's affected
  acceptance scenario plus at least three clean task-relevant adversarial cases.
  Shared matches may satisfy multiple tasks only when every claimed scenario,
  competing module, expected failure signal, and final outcome is exercised and
  evidenced separately.
- Require at least one fresh real full-engine ordinary-AI MAX regression to its
  natural conclusion for AI/engine rounds, plus graphical/platform checks for any
  feature MAX cannot prove. Reject sole reliance on reloaded states.
- Use the global build/game slots and isolated MAX-game support directories.
- Record release heads, merge order, conflicts, tests, repairs, exclusions,
  old-control comparisons, and remaining risks in the integration state.
- Send final structured receipts to the Task Maker. Promote the draft release PR
  to the product release PR, but never merge it into `bleed`; the user does that.

If build/game concurrency proves unreliable, reduce it immediately and submit a
high-priority orchestration-fix task through the Task Maker.
