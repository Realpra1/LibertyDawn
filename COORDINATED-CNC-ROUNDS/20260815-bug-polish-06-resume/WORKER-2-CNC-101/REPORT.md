# CNC-101 task report

## Current status

Cycle 2 is complete on `agent/round-20260815-cnc101-build-order-silo` at unchanged
product head `56984ae1933e4a953dd09b9fadb086ba5e0d326e`. The PR117 selective port compiles
and Scenario A passes again. Scenario B remains fixture-invalid because its
repaired setup checked queued production one tick too early, so this is an interim
handoff rather than acceptance. No PR was opened or pushed.

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

## Cycle 2 checks

- `make -j2`: passed, 0 warnings/errors.
- Focused Release tests: 42/42 passed for
  `OpeningGarrisonLogicTest|OpeningPolicyLogicTest|SmartEconomyPolicyTest`.
- `./utility.sh cnc --check-yaml`: passed across CNC rules and maps.
- Both packaged cycle-2 custom maps passed targeted `--check-yaml` validation.
- `git diff --check`: passed.

## Scenario A: opening prefix

The custom Pressure-based map ran ordinary SkyNet/GDI and Brutalis/Nod with one
MCV/Fact each, normal modules, seed `101201`, headless MAX, and a tick-9000 cap.
SkyNet began at 1300 cash and received 4000 at tick 750; Brutalis began at 5000.
The game exited 0 in 23.053 seconds without fatal, crash, Lua, unhandled-exception,
or desync markers.

Both bots logged Power, then their faction infantry structure, then the first
Refinery. Both requested the preserved two-unit emergency burst before the
Refinery became live and resumed optional rifle/rocket requests afterward. SkyNet
fell to 2 cash during the temporary constraint, then reserved its protected
Refinery after release. Later status samples showed live Harvesters, positive
earned income, useful army value, and continued production for both bots; the
constrained side did not remain in an idle/income dead zone.

## Scenario B: Silo queue boundary

The focused custom map ran two ordinary GDI SkyNets, seed `101202`, headless MAX,
and a tick-7000 cap. It exited 0 in 22.048 seconds with benchmark/replay output and
no fatal, crash, Lua, unhandled-exception, or desync marker. The setup correctly
waited until both newly created Refineries reported live capacity 150.

The launcher result is nevertheless failed and the scenario is not acceptance
evidence. At tick 2 the scripted busy-side `gtwr` request returned true, but the
harness immediately queried `IsProducing` in the same tick. The accepted order was
not observable yet, so the query returned false and the harness emitted its
explicit failure before applying scripted pressure. Natural harvesting later
caused exactly one Silo reservation and completion per side, but that does not
prove that an already-producing tower survives, nor that a free pressured queue
chooses one Silo before resuming SkyNet's preferred tower policy. No product defect
is inferred from this fixture timing error.

## Artifacts and next step

Raw maps, manifest, logs, replay, benchmark CSVs, and summaries are outside Git at
`.worktrees/coordinated-cnc/20260815-bug-polish-06-resume/analysis/worker-2-cnc101/cycle-02/`.
Cycle 3 should keep the product unchanged, issue the busy tower after live storage
initialization, and poll starting on a later tick until production becomes
observable before applying pressure. It must then repeat the required two distinct
games. Product changes require new engine evidence of a product defect.
