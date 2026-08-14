# Worker State: CNC-101

## Assignment

- Worker: `WORKER-2`
- Task: `CNC-101 — Build-order protection and silo timing`
- Status: `cycle 3 complete; Scenario B fixture-invalid, ready for cycle 4`
- Base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Task branch: `agent/round-20260815-cnc101-build-order-silo`
- PR base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Balance: frozen; no costs, production values, prerequisites, power, storage,
  thresholds, delays, or other tuning changes
- Cycle: `4/20`
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

## Cycle 3 durable result

- Product head remains `56984ae1933e4a953dd09b9fadb086ba5e0d326e`;
  no product or balance file changed.
- Release build passed with zero warnings/errors. Focused
  `OpeningGarrisonLogicTest|OpeningPolicyLogicTest|SmartEconomyPolicyTest` passed
  42/42. CNC and both cycle-3 custom-map YAML validation, Lua syntax, manifest
  JSON validation, and `git diff --check` passed.
- Scenario A passed at tick 9000/exit 0 in 13.015 seconds. Ordinary SkyNet/GDI
  and Brutalis/Nod again completed Power -> Barracks/Hand -> Refinery, produced
  emergency and optional useful infantry, and established Harvester income.
  SkyNet fell to 17 cash before the scripted tick-750 release, then completed its
  protected Refinery without an opening dead zone.
- Scenario B reached tick 7000/exit 0 in 14.019 seconds without crash, fatal Lua,
  unhandled exception, or desync. Live storage reached 150, the scripted `gtwr`
  request returned true at tick 2, and the later queue poll first reported busy at
  tick 139 before pressure was applied. One Silo then completed per side with
  capacity relief, but the scripted `gtwr` callback and actor were never observed,
  so the explicit busy-tower failure fired before acceptance.
- A one-game normal-module diagnostic with first-tower logging reached tick 7000
  in 14.012 seconds and proved the poll was not exact-item evidence:
  `IsProducing("gtwr")` means any compatible Defence queue is busy. SkyNet
  independently reserved and placed `obli` at its preferred location, satisfying
  that poll even though the requested `gtwr` was not the observed commitment. The
  later Silo therefore does not demonstrate tower cancellation or a product
  defect. The product remains unchanged.
- Two preliminary launcher attempts reached no world tick because the first used
  the CNC content child instead of the required `Support/Content` parent layout;
  one timed out and the exact second assigned process was stopped after diagnosis.
  They are infrastructure-invalid and are not game evidence.
- Raw maps, manifests, logs, replay, benchmarks, launcher summaries, and the
  diagnostic remain at
  `.worktrees/coordinated-cnc/20260815-bug-polish-06-resume/analysis/worker-2-cnc101/cycle-03/`
  outside Git.

## Next authorized cycle

Cycle 4 must retain the current product unless new exact-item evidence identifies
a product defect. Repair Scenario B so a compatible Defence queue is confirmed
idle after the created base and cash are live, wait at least one additional tick,
then issue the scripted busy-side `gtwr`. Treat `IsProducing("gtwr")` only as queue
occupancy, not item identity: enable first-tower diagnostics and reject the
fixture if SkyNet independently reserves another tower before the scripted request
is established. Require the scripted callback and a live `gtwr` actor before
calling the busy boundary proved; do not infer cancellation from a compatible
`obli` commitment. Exercise one free side and one genuinely busy side, requiring
tower completion, one later Silo only while pressure remains, capacity relief,
preferred-tower resumption, and no duplicate/phantom commitment. Run the distinct
Scenario A control in the same two-game cycle and repeat the focused/build/YAML/
Lua/diff checks. Do not change product code for Lua queue-observation ambiguity.

## Handoff

Keep this file and the task report current on the task branch. Do not edit the
task sheet or coordinator state, push `bleed`, or merge a PR.
