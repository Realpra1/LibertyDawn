# CNC-43 PR review — PR #78

## Verdict

`ready with one fix`

## Required fix

Run one fresh, distinct, instrumented changed-versus-pinned-base ordinary
long-pressure batch that actually exercises and records the normal MCV lifecycle,
then obtain its fresh factual Commenter narrative before making the next decision.
The batch must use a normal connected CNC map, exact matched controls, ordinary
real AIs and modules, contended/scarce cash, enemy pressure, and a missing or
destroyed Construction Yard so replacement or expansion logic acts. Record the
pre-launch failure hypothesis, perturbation, failure signal, and pass evidence;
initial deployment, MCV request/reservation/queue/production/deployment ticks,
MCV orders and owners, Construction Yard counts, cash/queue contention, MCV
survival, wall-enclosure behavior, final outcome, and MAX cost. Require no MCV
combat/crush order and no material changed-versus-base regression. Let at least
the changed run reach an evidenced natural conclusion. Update the worker report
and state with the resulting artifacts and exact conclusions.

This is test/evidence work only; no product-code change is indicated.

## Findings

### High — mandatory ordinary lifecycle and distinct adversarial evidence is unexercised

- Location: `COORDINATED-CNC-ROUNDS/20260806-bug-polish-01/WORKER-3-CNC-43/STATE.md:342`,
  `STATE.md:349`, `STATE.md:389`, `STATE.md:417`, and `STATE.md:430`; reported as
  complete in `REPORT.md:89`, `REPORT.md:97`, `REPORT.md:125`, and `REPORT.md:140`.
- Failure mechanism: the ordinary changed/base artifacts record only setup,
  progress, generic profiling, and configured exit. Their factual narrative
  explicitly says there are no unit/building events, production/economy data,
  MCV decisions, gameplay outcome, or final-state evidence
  (`commenters/ordinary-pair/NARRATIVE.md:7`, `:17`, `:61`). The natural-endurance
  narrative likewise records no production/deployment or player result and cannot
  identify the game-over cause (`commenters/natural-endurance/NARRATIVE.md:7`,
  `:11`, `:15`, `:48`). The focused class matrix and allied-safety checks that the
  report labels adversarial scenarios 1 and 2 are stages of the same map/run
  (`REPORT.md:89-102`), so together with the lifecycle map they establish only two
  distinct clean full-engine scenarios. The current evidence therefore cannot
  verify the contract's matched ordinary MCV request/queue/order/deploy metrics,
  absence of new MCV combat orders under pressure, production/deployment during
  natural endurance, or three distinct clean adversarial scenarios.
- Affected clauses: Ordinary and differential games 3-4; Adversarial cases
  preamble and case 4; Final regression's unchanged normal-AI lifecycle/order
  ownership requirement; review-role rules 7, 10, and 12.
- Smallest safe correction: the single matched long-pressure batch described in
  `Required fix` supplies a third distinct scenario while covering the missing
  ordinary differential, lifecycle/order, contention, pressure, natural-outcome,
  and factual-review evidence. No terrain test is required if adversarial case 4
  is completed cleanly.

## Verified evidence

- PR head `52250bb084ca804856d1bac0f0f59a73a4842ddd` is mergeable/CLEAN against
  `agent/cnc38-early-viki-infantry-rush`; GitHub reports no configured checks.
- The product diff is limited to `mods/cnc/rules/world.yaml`,
  `mods/cnc/rules/vehicles.yaml`, and `mods/cnc/rules/structures.yaml`.
  `git diff --check` passes.
- `mcvheavywheeled` resolves to exactly `wall, heavywall, crate, infantry` and
  copies every pinned `heavywheeled` terrain speed. Only normal `mcv` and
  `FACT.TransformsIntoMobile` reference it. HTNK and STNK resolved behavior is
  unchanged, and the pinned-to-current PR-base CNC/engine product delta is empty.
- The pinned control worktree is clean at
  `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`. Runtime map hashes match within
  both the literal and final changed/base pairs. Those pairs decisively prove the
  new hostile crush matrix, Mammoth parity, STNK/MTNK negative behavior, allied
  safety, and MCV transform behavior.
- The lifecycle v6 run proves one changed-build production and repack path. The
  recorded build/YAML/unit-test results are green, the final focused pair shows
  effectively identical throughput, the PR contains no diagnostics, and no
  product correctness, determinism, allocation, logging, or unrelated-scope
  defect was found.

## Required-fix identifier

`CNC-43-EVIDENCE-1`
