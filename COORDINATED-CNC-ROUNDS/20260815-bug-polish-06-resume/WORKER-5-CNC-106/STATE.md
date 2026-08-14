# Worker State: CNC-106

## Assignment

- Task: `CNC-106 — General queue-stall prevention and smart-economy serialization`
- Base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13` (PR117 head)
- Branch: `agent/round-20260815-cnc106-queue-stall`
- Status: `Review handoff returned — one required fix awaits an authorized repair cycle`
- Cycle: `3`
- PR: `none`

## Smallest literal contract

Detect genuine shared-production stalls or unaffordable contention, not low cash
alone. Below five harvesters, prioritize economy/scaling and request one
harvester/refinery commitment at a time; otherwise preserve existing choices.
Cancel and normally refund displaced work, retain its valid owner/purpose, and
serialize useful work without losing requests or recreating them immediately.
Preserve completed exit-blocked work, healthy parallel queues, existing crate
collection, deterministic save/load behavior, and all balance/config values.
Acceptance is visible useful completion followed by ordinary parallel progress,
with prior build choices preserved and less persistent idle cash where useful work
exists. Requests/logs alone are not acceptance.

## Explicit exclusions

Do not require or implement “materially sooner than control,” an exactly-five-live-
working-harvester result, or any new near-complete threat/combat exception. Do not
invent a composition/priority policy, alter authored weights/timing/prerequisites,
change crate routing, add a global CPU policy, or suppress healthy parallel work.
Exclude the audited exhaustive ownership inventory, new architecture mandate,
large metric taxonomy, ten-step adversarial ladder, three-clean gate, five-AI
performance gate, and broad personality/endurance matrix.

## Checks and scenarios

Use custom full-engine scenarios only; never fixtures. Each cycle may run at most
two distinct games, each capped at 120 seconds, with all normal modules and
ordinary CNC AIs enabled. Exercise one low/zero-cash partial-queue contention
case and one distinct lifecycle/prerequisite or save/load case. Prove actual
paid-progress stall, exact one-time cancellation/refund, one below-five recovery
commitment with a viable unloading path, visible completion, and resumed ordinary
parallel work. Add small focused checks for deterministic candidate order,
below-five counting, zero-refinery prerequisite ordering, healthy streaming,
Done/exit handling, request suppression/release, invalidation, refund ownership,
and save-state normalization. Preserve and observe existing crate behavior.

## Dependencies and porting

Respect existing contracts of CNC-101, CNC-103, CNC-104, CNC-77, and CNC-99;
inspect only published commits when the coordinator supplies a dependency pointer.
Selectively port only task-faithful product/test commits from the old CNC-106
branch. Never wholesale-merge old state, report, journal, or unrelated cumulative
commits.

## Handoff

Report concise evidence and artifact paths. Keep raw logs/replays/saves outside
Git. Do not edit the task sheet or coordinator state, merge a PR, or push `bleed`.

## Cycle 1 durable outcome

- Product/test commit: `77d229b12d` (`Port CNC-106 queue stall recovery`).
- Selectively ported the task-faithful product and focused-test changes through
  old commit `4971cad0a7`. Did not port old state/report history or the later
  imminent-threat exception `ecbff69355`, which is excluded by this contract.
- `make test`: passed; Release build had 0 warnings/errors and CNC MiniYAML
  validation passed.
- Focused `SmartEconomyPolicyTest`: 45/45 passed, including sustained paid-
  progress detection, below-five gating, zero-refinery candidate ordering,
  eligibility transitions, invalidation, and Done/exit classification.
- Two custom full-engine CNC games reached bounded tick 3000 with ordinary
  `brutalis` modules and no fatal Lua error or desync. They did not prove
  recovery acceptance because the temporary starting base was underpowered:
  recovery correctly classified contention as `low-power`, and the scheduled
  funding then restored ordinary paid streaming before activation.
- Raw artifacts (outside Git):
  `/tmp/cnc106-cycle1.S6qnz3/results-xvfb/`.
  Existing-refinery evidence is in
  `existing-refinery-contention/support/Logs/debug.log`; zero-refinery evidence
  is in `zero-refinery-prerequisite/support/Logs/debug.log`.

## Cycle 2 durable outcome

- No product or focused-test code changed. The temporary maps received one
  additional `nuk2` each, and scheduled grants moved from 15/35 seconds to 25/45
  seconds. These harness artifacts remain outside Git.
- Both authorized ordinary-`brutalis` CNC games exited normally at bounded tick
  3000 in about 9 seconds, with no fatal Lua error or desync.
- The zero-refinery prerequisite case passed every required marker. It activated
  once at tick 601 on `proc`, resolved exactly two displaced cancellations once at
  tick 626 (`expected-refund=186`, `earned-delta=186`), completed and released the
  refinery at tick 1426 with its free harvester/unloading path, then logged paid
  progress across ordinary parallel queues from tick 1476 and two ordinary queue
  completions by tick 1601.
- The existing-refinery case correctly did not activate. Power stayed normal, but
  its pre-existing harvester supplied small paid deltas throughout the intended
  stall; evidence reached only 200 ticks at tick 551 before new income prevented
  the 250-tick threshold. It later made healthy ordinary progress to at least five
  harvesters. This is a remaining scenario-isolation defect, not a product failure.
- Raw artifacts (outside Git): `/tmp/cnc106-cycle2.qJgaoP/results/`. Relevant logs
  are `zero-refinery-prerequisite/support/Logs/debug.log` and
  `existing-refinery-contention/support/Logs/debug.log`; concise runner results are
  in `batch-summary.json` and `batch-summary.tsv`.

## Cycle 3 authorized action

Preserve the required two distinct custom full-engine scenarios, each capped at
120 seconds, with all normal modules and an ordinary CNC AI enabled. Do not change
product code unless a concrete literal defect is found; do not add the excluded
threat exception or alter authored balance/config values.

- Scenario A — existing Refinery, no income-producing starting Harvester: retain
  the Refinery, force genuine shared-queue stall/partial-queue contention under
  normal power, and prove bounded recovery, one protected harvester/refinery
  commitment, visible completion, and ordinary parallel progress after exit.
- Scenario B — existing Refinery with a starting Harvester: cause that Harvester
  to be lost before its first unload, prove the transition into the same bounded
  recovery path, then prove selected completion and ordinary parallel progress
  after recovery exit.

Record actual paid-progress stall, one-time cancellation/refund ownership, viable
unloading, recovery transition, completion, and resumed parallel work. The prior
zero-refinery case remains evidence but does not replace either required scenario.

## Cycle 3 durable outcome

- No product or focused-test source changed. Both temporary maps retained the
  existing Refinery and normal power while suppressing only the map-local free
  Refinery Harvester spawn. Scenario A had no starting Harvester; Scenario B kept
  the explicit starting Harvester through the tick-1 observation and destroyed it
  at the first scripted second, before any unload. The Refinery and dock remained
  available to the recovery-built Harvester in both cases.
- Both authorized ordinary-`brutalis` full-engine CNC games passed and exited
  normally at bounded tick 3000 in 21.054 and 18.046 seconds. There was no fatal
  Lua error or desync.
- Each scenario accumulated exactly 250 ticks of no-paid-progress evidence and
  activated exactly once at tick 601 on the partial `harv` front. Each had exactly
  one cancellation-resolution event at tick 626 with `entries=3`, `unresolved=0`,
  `expected-refund=306`, and `earned-delta=306`.
- Each protected Harvester made continuous selected progress, entered the
  completed/exit wait at tick 1201, and released at tick 1226 with
  `completed=True`, one live Harvester, and one live Refinery. Ordinary parallel
  paid progress resumed across infantry, building, defence, and vehicle queues by
  tick 1301; two ordinary queue completions were recorded by tick 1401.
- Scenario B explicitly recorded the lifecycle transition from one live Harvester
  at ticks 1/51 to zero at tick 301 before entering the same recovery path.
- Raw artifacts (outside Git): `/tmp/cnc106-cycle3.4tnUP0/results/`. Concise
  results are in `batch-summary.json` and `batch-summary.tsv`; detailed evidence
  is in each scenario's `support/Logs/debug.log`.

## Handoff status

The assigned Cycle 3 acceptance action is complete. No further development or
game cycle is authorized by this state; return to the coordinator for review/PR
handoff using product/test commit `77d229b12d` plus this durable evidence update.

The review handoff has now been returned. `REVIEW.md` reports one required fix:
preserve the recovery ownership/gate while the selected item is `Done` but still
exit-blocked, with a focused regression proving ordinary production remains
paused until the selected actor exits. This state does not authorize that product
or test change, so no repair or additional game was performed. The coordinator
must authorize a repair cycle before implementation resumes.
