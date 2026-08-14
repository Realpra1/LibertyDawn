# CNC-101 task report

## Current status

Cycle 4 is complete on `agent/round-20260815-cnc101-build-order-silo` at unchanged
product head `56984ae1933e4a953dd09b9fadb086ba5e0d326e`. The PR117 selective port compiles
and Scenario A passes again. Scenario B remains fixture-invalid because the
script-created `upgrade.recon2` prerequisite was not visible in the same Lua
callback, so the exact `gtwr` request was deliberately not issued. This remains
an interim handoff rather than acceptance. No product code changed and no PR was
opened or pushed.

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

## Cycle 4 checks

- `make -j2`: passed, 0 warnings/errors through the protected build entry.
- Focused Release tests: 42/42 passed for
  `OpeningGarrisonLogicTest|OpeningPolicyLogicTest|SmartEconomyPolicyTest`.
- `./utility.sh cnc --check-yaml`: passed across CNC rules and maps.
- Both packaged cycle-4 custom maps passed targeted `--check-yaml` validation;
  both Lua scripts and the manifest also passed syntax/JSON validation.
- `git diff --check`: passed.

## Scenario A: opening prefix

The custom Pressure-based map ran ordinary SkyNet/GDI and Brutalis/Nod with one
MCV/Fact each, normal modules, seed `101301`, headless MAX, and a tick-9000 cap.
SkyNet began at 1300 cash and received 4000 at tick 750; Brutalis began at 5000.
The game exited 0 in 56.181 seconds without fatal, crash, Lua, unhandled-exception,
or desync markers.

Both bots logged Power, then their faction infantry structure, then the first
Refinery. Both requested the preserved emergency infantry burst before the
Refinery became live and resumed optional rifle/rocket requests afterward. SkyNet
fell to 1 cash after the temporary constraint, then reserved its protected
Refinery after release. Later samples showed Harvester income and continued normal
modules; the constrained side did not remain in an opening idle/income dead zone.

## Scenario B: Silo queue boundary

The focused custom map ran two ordinary SkyNets, seed `101302`, headless MAX, and
a tick-7000 cap. It exited 0 in 43.145 seconds with benchmark/replay output and no
fatal, crash, Lua, unhandled-exception, or desync marker. Both newly created
Refineries reported live capacity 150. At tick 2 the harness proved the compatible
Defence queue idle with the base and 10000 cash live, then waited one tick. Its
script-created `upgrade.recon2` was not yet visible to `HasPrerequisites` in that
callback, so the explicit prerequisite-not-live failure fired and no `gtwr`
request or pressure was issued.

Normal SkyNet behavior later reserved and placed one `obli` per side, then natural
harvesting caused one Silo commitment and completion per side with capacity
relief. This does not prove the scripted free/busy boundary, tower preservation,
or preferred-tower resumption. No exact request existed and no product defect is
inferred. One earlier launcher preflight was rejected before output creation or a
game launch because it named a nonexistent remembered content path; the unchanged
two-game manifest then ran once with the resolved runtime-content parent.

## Artifacts and next step

Raw maps, manifest, logs, replay, benchmark CSVs, and summaries are outside Git at
`.worktrees/coordinated-cnc/20260815-bug-polish-06-resume/analysis/worker-2-cnc101/cycle-04/`.
Cycle 5 should keep the product unchanged, hold the busy side below tower
affordability while `upgrade.recon2` propagates across ticks, then restore cash
and issue the scripted `gtwr` in the first prerequisite-live callback. It must
retain the exact callback/live-actor and competing-reservation rejection gates,
then repeat the required two distinct games. Product changes require exact-item
engine evidence of a product defect.
