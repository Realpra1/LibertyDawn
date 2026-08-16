# Worker State: CNC-33

## Assignment

- Worker: `WORKER-2`; task: `CNC-33`
- Role model: `gpt-5.6-sol`, medium reasoning
- Base: `fcfafc21a9a6c2aa24e06b3b7c771c94df918d50` (origin/bleed)
- Task branch: `agent/round-20260816-cnc33-playtest-hotfix`
- PR: none; do not create an independent PR
- Balance: frozen; no unrelated tuning or balance change is authorized
- Cycle: `1/1`

## Literal acceptance authority

This is audit-first. Audit whether economic scaling is disabled or actually receives/uses 50 percent of money spent; the reported symptom is that Brutalis scales extremely slowly. If the configured 50 percent is already active and correctly consumed, record proof and make no product change; otherwise fix only the concrete disablement/accounting/routing defect. Preserve the existing smart-economy behavior and do not retune unrelated balance.

Use matched full-engine evidence and direct accounting/routing diagnostics, not logs alone: establish whether the configured 50 percent reaches the intended adaptive economic scaling consumer and is consumed there, then test Brutalis scaling against an appropriate control. Preserve refineries, silos, storage, queue recovery, VIKI behavior, and all unrelated policy.

## Required one-cycle contract

- Run focused tests and protected repository checks, including build/test validation appropriate to the touched owner and `git diff --check`.
- Run exactly two distinct ordinary-AI/all-module custom games, each under 120 seconds; no third game counts.
- For each game obtain one fresh Luna-medium narrator and one fresh Luna-medium policy reviewer, with evidence paths recorded here/report. Reviews must address the 50-percent audit, accounting proof, Brutalis scaling, and no-change outcome when already correct.
- Obtain a fresh Terra-medium final review after the two games and checks.
- Do not edit another stream, create tasks, push, merge bleed, or open an independent PR. Update only this state, the task report, and the task branch.

## Evidence

- Analysis directory: `/root/github/.build/coordinated-cnc/20260816-playtest-hotfix/WORKER-2-CNC-33/analysis`
- Report: `COORDINATED-CNC-ROUNDS/20260816-playtest-hotfix/WORKER-2-CNC-33/REPORT.md`
- Product head: pending commit.
- Audit proof: Brutalis had no `WeightedUnitSelection` or `EconomyTypes` entry.
  `EconomyCombatSplit` defaults to 0.5 but is reached only by
  `ChooseWeightedUnitToBuild`; unweighted Brutalis therefore never consumes it.
  The local change enables weighted selection, defines `harv` as the economy
  bucket, and pins the intended split at `0.5` without changing weights, limits,
  refineries, silos, or queues.
- Checks: `make check` passed with 0 warnings and 0 errors; `git diff --check`
  passed.
- Interrupted legacy control: invalid and uncounted. It loaded `modcontent` and
  failed platform initialization before world tick 1. Its diagnostic-only YAML
  edit was audited and replaced by the narrow product configuration above.
- Game results, Luna analyses, and Terra final review: pending. Do not claim
  completion or consume a further cycle until two valid ordinary-AI custom games
  are run with an installed CNC `SupportDir/Content` parent.
