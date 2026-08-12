---
name: integrate-cnc-release
description: Combine reviewed Liberty Dawn CNC task branches into one stable release branch, run bounded adversarial combined testing and task-scoped repairs, and prepare one cumulative PR to bleed. Use after individual coordinated task PRs finish implementation and review.
---

# Integrate a CNC Release

Use Terra 5.6 medium. Read applicable `AGENTS.md`, the integration job, reviewed heads,
PR/check summaries, and worker receipts. Read a worker state only for a conflict or
integrated failure that requires its contract.

## Build the candidate

1. Verify every included branch descends from the common base and has a unique task
   PR, status, final review, and required checks. Record exclusions.
2. Create `agent/cnc-<round>-release` from the common base and merge reviewed heads
   locally with merge commits. Resolve only mechanical conflicts; return behavior
   conflicts to the owning worker.
3. Run build/unit/static checks, inspect the combined diff, push the stable branch,
   and open one draft PR to `bleed`. Keep source PRs open; never merge them through
   GitHub or merge `bleed`.
4. Before promotion, run a fresh Terra-medium release review for cross-task leakage,
   duplicated policy, conflict damage, nondeterminism, unbounded CPU work, noisy
   diagnostics, missing evidence, and unauthorized balance changes.

## Combined testing and repair

Run at most five release-wide integration test/fix cycles. Launch fresh
Terra-medium integration workers or test-only agents from durable assignments;
use task-scoped repair branches based on the current release head. Integration
workers use the same worker-state game contract, helpers, and preflight as normal
workers. Prefer `launch-ai-parallel.py` with an explicit valid `--content` path;
verify the isolated `SupportDir/Content` target, checkout-local engine/mod paths,
map, timeout, cleanup, and artifact destinations before launch. Do not substitute
an incomplete hand-written command. Every cycle runs at least two distinct
adversarial custom scenarios; each game:

- uses the full engine, all features, all AI modules, and ordinary enemy
  AIs from test 1;
- normally runs headless MAX and ends within 120 seconds wall-clock;
- stresses a different cross-task interaction, timing, state invalidation,
  topology, resource/order contention, loss/recovery path, or old-control case;
- receives its own fresh Luna-medium Commenter narrative and its own fresh
  Luna-medium Policy Review before the next change.

The integrator and integration workers own game readiness as well as product
behavior. A run that never reaches world tick 1 is an invalid test, not a failed
game and not a consumed integration cycle. Repair the exact release checkout's
build/content/launcher/display/audio/process cleanup or scenario setup, then
rerun. Do not repeat an identical pre-map-start launch five times. If matched
base/release diagnosis proves a host/runtime blocker, preserve the evidence and
request environment help; do not promote the release or claim integrated testing.

Before diagnosing product startup, reproduce the newest preserved successful
worker launch with its exact launcher inputs. Missing installed content commonly
redirects CNC to `modcontent`; treat `Loading mod: modcontent` in an automated
headless run as a setup failure and check `SupportDir/Content` first.

Use strict analysis envelopes and the serialized persistent scratchpad contract
from the coordinator skill. Treat analyses as advice, not proof. Compare every
strategic change with its recorded feature-disabled/base/older control under
matched conditions. A repeated loss, tie, or marginal improvement is likely merge
damage or bad policy unless task-specific evidence proves an accepted tradeoff.

When repair is needed:

1. Record release head, integration notes, repair branch/base, and counters in the
   worker state.
2. Make only the owning task's smallest compatible fix; review its repair PR.
3. Merge reviewed repair heads locally into the stable branch, creating the next
   candidate, then rerun combined checks and affected game scenarios.

Stop after five total integration cycles even when several tasks remain active.
Publish the safest proven subset and mark unresolved work `First iteration -
testing`; do not churn.

For performance comparisons, use matched custom maps with many pre-spawned units
and structures, normally two Iron Reapers with at least 300 units plus structures
each. Cap every pre-Codex/newest-disabled/newest-enabled leg at 120 seconds and
compare tick progress/latency, CPU, peak memory, actor counts, and stalls.

## Completion

Require every included task's literal behavior, forbidden-behavior checks,
old-control comparison, affected adversarial scenarios, and CI to pass on the
combined head. Use isolated game support directories and shared resource slots;
await launcher completion rather than spending agent turns sleeping or polling.
Record heads, merge order, conflicts, tests, repairs, exclusions, and risks in
integration state. Send structured receipts to Luna Task Maker and promote the
draft release PR. The user decides whether to merge it.

If orchestration itself appears defective, record it in deferred work. Create a
task only when the user explicitly requests one and completes the intake gate.
