# Worker State: CNC-101

## Assignment

- Worker: `WORKER-2`
- Task: `CNC-101 — Build-order protection and silo timing`
- Status: `cycle 6 complete; First iteration - testing, evidence-only cycle 7 authorized`
- Base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Task branch: `agent/round-20260815-cnc101-build-order-silo`
- PR base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Balance: frozen; no costs, production values, prerequisites, power, storage,
  thresholds, delays, or other tuning changes
- Cycle: `6/20`
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

## Cycle 4 durable result

- Product head remains `56984ae1933e4a953dd09b9fadb086ba5e0d326e`;
  no product or balance file changed.
- Release build passed with zero warnings/errors. Focused
  `OpeningGarrisonLogicTest|OpeningPolicyLogicTest|SmartEconomyPolicyTest` passed
  42/42. CNC and both cycle-4 custom-map YAML validation, Lua syntax, manifest
  JSON validation, and `git diff --check` passed.
- Scenario A passed at tick 9000/exit 0 in 56.181 seconds. Ordinary SkyNet/GDI
  and Brutalis/Nod again completed Power -> Barracks/Hand -> Refinery, produced
  emergency and optional useful infantry, and established Harvester income.
  SkyNet fell to 1 cash after the scripted tick-750 release and still completed
  its protected Refinery without an opening idle/income dead zone.
- Scenario B reached tick 7000/exit 0 in 43.145 seconds without crash, fatal Lua,
  unhandled exception, or desync. It proved the funded live Defence queue idle at
  tick 2 and waited one more tick, but creating `upgrade.recon2` did not make the
  prerequisite visible to `HasPrerequisites` in that same callback. The harness
  emitted its explicit prerequisite-not-live failure before issuing the scripted
  `gtwr`; therefore no exact busy-tower boundary was exercised.
- Post-failure normal SkyNet behavior later reserved and placed one `obli` per
  side, followed by one naturally pressured Silo per side and capacity relief.
  This is not evidence for the scripted free/busy boundary or a product defect.
- One launcher preflight was rejected before creating output or launching a game
  because a nonexistent remembered content path was supplied. The unchanged
  two-game manifest then ran once with the resolved runtime-content parent. The
  preflight is infrastructure-invalid and not game evidence.
- Raw maps, manifest, logs, replay, benchmarks, launcher summaries, and the
  rejected preflight context remain at
  `.worktrees/coordinated-cnc/20260815-bug-polish-06-resume/analysis/worker-2-cnc101/cycle-04/`
  outside Git.

## Cycle 5 durable result

- Product head remains `56984ae1933e4a953dd09b9fadb086ba5e0d326e`;
  no product or balance file changed. The interrupted predecessor had prepared
  the cycle-5 custom maps and manifest but launched no game; that partial setup
  was audited and reused without duplicating a run.
- Release build passed with zero warnings/errors. Focused
  `OpeningGarrisonLogicTest|OpeningPolicyLogicTest|SmartEconomyPolicyTest` passed
  42/42. CNC and both cycle-5 custom-map YAML validation, Lua syntax, manifest
  JSON validation, and `git diff --check` passed.
- Scenario A passed at tick 9000/exit 0 in 16.364 seconds. Ordinary SkyNet/GDI
  and Brutalis/Nod completed Power -> Barracks/Hand -> Refinery, created useful
  infantry, and established initial Harvester income. SkyNet began at 1300 cash,
  received the scripted release at tick 750, and completed its protected Refinery.
  The later Brutalis zero-Harvester/blocked-extension state occurred after the
  required prefix and is recorded as an unrelated advisory, not attributed to
  this task.
- Scenario B reached tick 7000/exit 0 in 15.661 seconds without crash, fatal Lua,
  unhandled exception, or desync. The scripted `upgrade.recon2` became live one
  tick after creation while Busy had zero cash; after cash restoration the exact
  `gtwr` request was accepted at tick 3 and occupied Defence at tick 4. SkyNet
  independently placed that `gtwr` at its preferred target `(79,92)` before its
  Silo, and both sides later completed exactly one Silo with capacity 4150.
- The Lua `Build` completion callback never fired despite the independently logged
  live `gtwr` placement. The callback-dependent monitor therefore emitted its
  explicit failure and stopped before proving the complete free/busy boundary,
  preferred-tower resumption, and duplicate/phantom lifecycle. This is harness
  state-order uncertainty, not exact evidence of a product defect.
- Fresh Luna factual narrators and policy reviewers ran independently for both
  games. Scenario A review was `insufficient evidence`; its highest recommendation
  for a matched multi-cash changed/control matrix is rejected for this task because
  literal acceptance requires the bounded A/B scenarios, not a mandatory old-policy
  matrix, and cycles 1-5 repeatedly prove the opening prefix. Its late Brutalis
  economy observation is retained as unrelated advisory evidence. Scenario B
  review was `insufficient evidence / required follow-up`; its recommendation for
  callback-independent queue/structure instrumentation is accepted for the next
  evidence-only cycle. Exactly two full-engine games were run in cycle 5, so that
  follow-up was not silently added as a third game.
- Raw maps, logs, benchmark CSVs, staged role inputs, narratives, and reviews remain
  at `.worktrees/coordinated-cnc/20260815-bug-polish-06-resume/analysis/worker-2-cnc101/cycle-05/`
  outside Git.

## Cycle 6 durable result

- Product head remains `56984ae1933e4a953dd09b9fadb086ba5e0d326e`;
  no product, balance, or shipped map changed. The branch began clean at
  `4f071b6a935e4bf466f2f34ae88a68176722dd6b`.
- Release build passed with zero warnings/errors. Focused
  `OpeningGarrisonLogicTest|OpeningPolicyLogicTest|SmartEconomyPolicyTest` passed
  42/42. CNC and both final custom-map YAML validation, Lua and manifest syntax,
  and `git diff --check` passed.
- Exactly two acceptance-valid full-engine games ran in the final batch. Both
  ordinary-AI games reached tick 9000/exit 0 without fatal Lua, crash, unhandled
  exception, desync, or build stall. Scenario A took 21.031 seconds and Scenario B
  took 27.039 seconds; the batch passed 2/2.
- Scenario A re-proved the constrained common opening and normal continuation.
  Exact first tens were SkyNet/GDI
  `fact,sbag,nuk2,sbag,pyle,obli,proc,sbag,sbag,sbag` and Brutalis/Nod
  `fact,cycl,nuke,cycl,hand,proc,cycl,cycl,cycl,cycl`. Both therefore completed
  Power -> faction infantry structure -> Refinery while normal modules and wall
  construction continued.
- Scenario B used sustained real storage pressure only after a live Refinery,
  first Power, and at least four live walls. It recorded first Power at tick 460,
  the four-wall boundary (nine live by the observation tick) at tick 2014,
  pressure at tick 2014, and exactly one first Silo at tick 2131. Capacity rose
  from 150 to 4150; no duplicate occurred and play continued normally to tick
  9000. Its exact first-ten sequences matched Scenario A.
- Preliminary launches were acceptance-invalid and do not count toward the
  two-game cap: callback-dependent harnesses ended at tick 4, one one-shot
  pressure attempt reached tick 9000 without retaining the pressure precondition,
  and early first-ten loggers used unsupported Lua behavior. They identified
  harness defects only; the final direct observer uses live actor insertion order.
- Fresh Luna narrators and separate fresh Luna policy reviewers completed for
  each valid game. Scenario B policy was PASS/high confidence and recommended no
  product change. Scenario A policy's recommendation for an unconditional Silo
  in its no-pressure run is rejected as out of scope: the durable requirement
  keeps Silo conditional on actual storage pressure, which Scenario B directly
  proves. No callback or queue-internal follow-up remains required.
- Fresh Terra-medium final review returned FAIL/high confidence on one bounded
  evidence gap: the final direct Scenario B did not apply pressure while the
  relevant preferred tower was already producing, so it did not prove no
  cancellation and resumed preferred-tower production. All other checks and
  dispositions passed review. No product defect is inferred.

## Next authorized cycle

Cycle 7 is authorized solely to prove the remaining busy-tower boundary with
ordinary CNC AIs: establish the relevant preferred tower in production, apply
real storage pressure while that item remains in progress, prove it is not
cancelled, observe the conditional Silo ordering and capacity relief, and prove
preferred-tower production resumes afterward. Keep the product unchanged unless
that direct evidence identifies a concrete narrow defect. Do not revisit callback
semantics, add an unconditional Silo requirement, or broaden policy.

## Handoff

Keep this file and the task report current on the task branch. Do not edit the
task sheet or coordinator state, push `bleed`, or merge a PR.
