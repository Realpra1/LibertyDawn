# CNC-101 task report

## Current status

Cycle 3 is complete on `agent/round-20260815-cnc101-build-order-silo` at unchanged
product head `56984ae1933e4a953dd09b9fadb086ba5e0d326e`. The PR117 selective port compiles
and Scenario A passes again. Scenario B remains fixture-invalid because Lua queue
occupancy cannot identify the queued actor, so this is an interim handoff rather
than acceptance. No product code changed and no PR was opened or pushed.

## Ported behavior

- All nine CNC BaseBuilder configurations covering the ten configured AIs opt
  into a common Power -> faction-compatible Barracks/Hand -> Refinery prefix.
  Advanced opening goals remain enabled only for profiles that already owned them.
- A fresh first Refinery is coordinated by the prefix, while serialization after
  loss applies only once a usable Refinery has existed. That fact and one global
  Silo reservation survive save/load.
- The existing storage-pressure threshold now owns exactly one actionable Silo
  on a free compatible Defence queue before optional tower selection. Busy queues
  are not inspected or cancelled; unaffordable, unpowered, unavailable, or already
  committed candidates cannot create a reservation.
- Advanced opening unit requests wait for the common structure prefix. Optional
  construction and discretionary opening-garrison requests yield while the first
  Refinery is protected; the existing emergency infantry burst remains available.
- No costs, prerequisites, power, capacity, thresholds, delays, limits, or other
  balance values changed.

## Cycle 3 checks

- `make -j2`: passed, 0 warnings/errors through the protected build entry.
- Focused Release tests: 42/42 passed for
  `OpeningGarrisonLogicTest|OpeningPolicyLogicTest|SmartEconomyPolicyTest`.
- `./utility.sh cnc --check-yaml`: passed across CNC rules and maps.
- Both packaged cycle-3 custom maps passed targeted `--check-yaml` validation;
  both Lua scripts and the manifest also passed syntax/JSON validation.
- `git diff --check`: passed.

## Scenario A: opening prefix

The custom Pressure-based map ran ordinary SkyNet/GDI and Brutalis/Nod with one
MCV/Fact each, normal modules, seed `101301`, headless MAX, and a tick-9000 cap.
SkyNet began at 1300 cash and received 4000 at tick 750; Brutalis began at 5000.
The game exited 0 in 13.015 seconds without fatal, crash, Lua, unhandled-exception,
or desync markers.

Both bots logged Power, then their faction infantry structure, then the first
Refinery. Both requested the preserved emergency infantry burst before the
Refinery became live and resumed optional rifle/rocket requests afterward. SkyNet
fell to 17 cash during the temporary constraint, then reserved its protected
Refinery after release. Later samples showed Harvester income and continued normal
modules; the constrained side did not remain in an opening idle/income dead zone.

## Scenario B: Silo queue boundary

The focused custom map ran two ordinary SkyNets, seed `101302`, headless MAX, and
a tick-7000 cap. It exited 0 in 14.019 seconds with benchmark/replay output and no
fatal, crash, Lua, unhandled-exception, or desync marker. Both newly created
Refineries reported live capacity 150. The script issued `gtwr` at tick 2, first
observed a busy compatible Defence queue at tick 139, then applied pressure. One
Silo completed per side and relieved capacity, but the `gtwr` callback/actor never
appeared, so the harness emitted its explicit busy-tower failure.

A normal-module diagnostic with first-tower logging proved this was still fixture
ambiguity, not product evidence. The scripting API documents and demonstrated
that `IsProducing("gtwr")` reports any compatible queue item. SkyNet independently
reserved and placed `obli` at its preferred location, which made the poll true.
The diagnostic reached tick 7000/exit 0 in 14.012 seconds. No cancellation of an
exact observed `gtwr` was established, so no product defect is inferred.

Two earlier launcher attempts reached world tick 0 because the content argument
pointed to the CNC child rather than the required `Support/Content` parent layout.
They are infrastructure-invalid, were isolated from the valid artifacts, and do
not count as game evidence.

## Artifacts and next step

Raw maps, manifests, logs, replay, benchmark CSVs, summaries, and the diagnostic
are outside Git at
`.worktrees/coordinated-cnc/20260815-bug-polish-06-resume/analysis/worker-2-cnc101/cycle-03/`.
Cycle 4 should keep the product unchanged and make Scenario B establish an idle,
live Defence queue before issuing `gtwr`, while treating the occupancy poll as
non-identifying and rejecting any competing SkyNet tower reservation. It must
require the scripted callback and live `gtwr` before accepting the busy boundary,
then repeat the required two distinct games. Product changes require exact-item
engine evidence of a product defect.
