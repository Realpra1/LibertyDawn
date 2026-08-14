# Worker State: CNC-101

## Assignment

- Worker: `WORKER-2`
- Task: `CNC-101 — Build-order protection and silo timing`
- Status: `cycle 2 complete; Scenario B fixture-invalid, ready for cycle 3`
- Base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Task branch: `agent/round-20260815-cnc101-build-order-silo`
- PR base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Balance: frozen; no costs, production values, prerequisites, power, storage,
  thresholds, delays, or other tuning changes
- Cycle: `3/20`
- PR: none

## Literal scope

For every configured CNC AI, preserve the existing build policy while making
the permitted opening sequence Power -> faction-compatible Barracks/Hand ->
Refinery the reliable default when prerequisites, queue, power, and affordability
permit. Do not duplicate qualifying existing structures or strand a queue.

Silo remains conditional: use the existing storage-pressure predicate and
threshold. When pressure is true and one Silo is actionable, exactly one free
compatible Defence-queue commitment must choose it before a new optional tower.
Never cancel a tower already producing. Do not reserve unaffordable cash or a
queue, create phantom/duplicate commitments, or suppress economy recovery.
Preserve Skynet's existing preferred tower identity/location/timing, existing
Refinery/harvester recovery, parallel queues, save/load, determinism, complete
matches, and all unrelated AI policy. No balance change is authorized.

## Minimal acceptance and checks

- Use ordinary CNC AIs with all normal modules in two distinct custom full-engine
  scenarios per cycle, each <=120 seconds; never use manager-only fixtures.
- Scenario A: a capable one-Fact fresh start covering both Barracks and Hand
  factions, proving Power -> infantry structure -> Refinery, useful infantry,
  and no idle/income dead zone; include a low-cash or temporarily unavailable
  candidate control that later resumes.
- Scenario B: force storage pressure on a free Defence queue and separately
  after a tower is already producing. Prove one actionable Silo before a new
  tower only in the first case, no tower cancellation in the second, capacity
  relief, resumed tower policy, and no duplicate/phantom reservation.
- Add focused selector/predicate tests, YAML validation, save/load or loss
  coverage when directly affected, syntax, and `git diff --check`. Do not add
  exhaustive planner inventories, mandatory policy-review loops, arbitrary
  performance thresholds, or unrelated full-regression gates.

## Selective port dependency

The prior task branch is `agent/round-20260814-cnc101-build-order-silo`.
Inspect its commits selectively and port only task-faithful build-order/Silo
changes that apply cleanly to PR117. Never wholesale-merge its STATE.md,
process metadata, report, evidence, or branch history. Recheck shared queue
ownership against the PR117 base before retaining a commit.

## Cycle 1 durable result

- Product head: `56984ae1933e4a953dd09b9fadb086ba5e0d326e`.
- Selectively ported the functional opening-prefix, post-establishment Refinery
  recovery, explicit need-based Silo ownership, prefix unit gate, protected first
  Refinery, and opening-garrison pause chain. Prior diagnostic-only commits and
  all prior process/report artifacts were excluded.
- Release build passed with zero warnings/errors. Focused
  `OpeningGarrisonLogicTest|OpeningPolicyLogicTest|SmartEconomyPolicyTest` passed
  42/42. CNC YAML validation and `git diff --check` passed.
- Scenario A passed at tick 9000/exit 0 in 13.018 seconds. Ordinary SkyNet/GDI
  and Brutalis/Nod each completed Power -> Barracks/Hand -> Refinery, produced
  emergency and optional useful infantry, established Harvester income, and
  continued production. SkyNet began with only 1300 cash, reached 72 cash while
  its next candidate was constrained, received the scripted release at tick 750,
  then reserved and completed its first Refinery without an idle-income dead zone.
- Scenario B reached tick 4500/exit 0 in 11.013 seconds with no crash, fatal Lua,
  or desync. Both ordinary SkyNets made one pressured Silo commitment and completed
  capacity relief from 150 to 4150. The scripted busy-side `gtwr` request returned
  true, but its completion callback never fired and initialized pressure clamped
  to `0/0` before the created Refineries became live. The game therefore did not
  prove the already-producing-tower boundary or resumed preferred-tower policy and
  is fixture-invalid for acceptance. No product defect is inferred from it.
- Raw maps, manifest, logs, replay, benchmarks, and launcher summaries remain at
  `.worktrees/coordinated-cnc/20260815-bug-polish-06-resume/analysis/worker-2-cnc101/cycle-01/`
  outside Git.

## Cycle 2 durable result

- Product head remains `56984ae1933e4a953dd09b9fadb086ba5e0d326e`;
  no product or balance file changed.
- Release build passed with zero warnings/errors. Focused
  `OpeningGarrisonLogicTest|OpeningPolicyLogicTest|SmartEconomyPolicyTest` passed
  42/42. CNC and both custom-map YAML validation plus `git diff --check` passed.
- Scenario A passed at tick 9000/exit 0 in 23.053 seconds. Ordinary SkyNet/GDI
  and Brutalis/Nod again completed Power -> Barracks/Hand -> Refinery, produced
  emergency and optional useful infantry, established Harvester income, and
  continued production. SkyNet fell to 2 cash before the scripted tick-750 cash
  release, then completed its protected Refinery without an idle-income dead zone.
- Scenario B reached tick 7000/exit 0 in 22.048 seconds without crash, fatal Lua,
  unhandled exception, or desync. Live storage correctly reached 150 before setup.
  At tick 2 the scripted `gtwr` request returned true, but the immediate same-tick
  `IsProducing` check returned false because the accepted production order had not
  become observable yet. The harness emitted its explicit failure and stopped
  before applying scripted pressure. Later natural harvesting produced exactly
  one Silo reservation and completion per side, but this is not evidence for the
  free/busy boundary, tower preservation, or preferred-tower resumption. No
  product defect is inferred.
- Raw maps, manifest, logs, replay, benchmarks, and launcher summaries remain at
  `.worktrees/coordinated-cnc/20260815-bug-polish-06-resume/analysis/worker-2-cnc101/cycle-02/`
  outside Git.

## Next authorized cycle

Cycle 3 must retain the current product unless new evidence identifies a product
defect. Repair Scenario B by separating request acceptance from observation:
after live storage reports capacity 150, issue the scripted busy-side `gtwr`, then
poll from a later tick until the Defence queue reports producing before applying
pressure to either side. Keep a bounded explicit failure if production never
becomes visible. Exercise one free side and one busy side, requiring tower
completion, one later Silo only while pressure remains, capacity relief,
preferred-tower resumption, and no duplicate/phantom commitment. Run the required
distinct Scenario A control in the same two-game cycle and repeat the
focused/build/YAML/diff checks. Do not change product code for the same-tick Lua
observation issue.

## Handoff

Keep this file and the task report current on the task branch. Do not edit the
task sheet or coordinator state, push `bleed`, or merge a PR.
