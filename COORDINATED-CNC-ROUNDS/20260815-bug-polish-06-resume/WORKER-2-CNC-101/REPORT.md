# CNC-101 task report

## Current status

Cycle 5 is complete on `agent/round-20260815-cnc101-build-order-silo` at unchanged
product head `56984ae1933e4a953dd09b9fadb086ba5e0d326e`. The PR117 selective port compiles
and Scenario A passes again. Scenario B now proves delayed prerequisite propagation,
exact `gtwr` queue acceptance/occupancy, preferred live placement, and one Silo per
side, but its Lua completion callback did not fire and the callback-dependent monitor
stopped before the complete free/busy lifecycle was proven. Status is `First
iteration - testing`; an evidence-only cycle 6 is appropriate. No product code
changed and no PR was opened or pushed.

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

## Cycle 5 checks

- `make -j2`: passed, 0 warnings/errors through the protected build entry.
- Focused Release tests: 42/42 passed for
  `OpeningGarrisonLogicTest|OpeningPolicyLogicTest|SmartEconomyPolicyTest`.
- `./utility.sh cnc --check-yaml`: passed across CNC rules and maps.
- Both packaged cycle-5 custom maps passed targeted `--check-yaml` validation;
  both Lua scripts and the manifest also passed syntax/JSON validation.
- `git diff --check`: passed.

## Scenario A: opening prefix

The custom Pressure-based map ran ordinary SkyNet/GDI and Brutalis/Nod with one
MCV/Fact each, normal modules, seed `101301`, headless MAX, and a tick-9000 cap.
SkyNet began at 1300 cash and received 4000 at tick 750; Brutalis began at 5000.
The game exited 0 in 16.364 seconds without fatal, crash, Lua, unhandled-exception,
or desync markers.

Both bots logged Power, then their faction infantry structure, then the first
Refinery. Both requested the preserved emergency infantry burst before the
Refinery became live and resumed optional rifle/rocket requests afterward. SkyNet
reserved its protected Refinery after release. Later samples showed Harvester
income and continued normal modules; the constrained side did not remain in an
opening idle/income dead zone. Brutalis later lost its Harvester economy while a
tiberium-extension plan remained blocked; the factual narrator marked causality
unknown, so this is retained as an unrelated advisory rather than attributed to
the opening prefix.

## Scenario B: Silo queue boundary

The focused custom map ran two ordinary SkyNets, seed `101302`, headless MAX, and
a tick-7000 cap. It exited 0 in 15.661 seconds with benchmark output and no fatal,
crash, Lua, unhandled-exception, or desync marker. Both newly created Refineries
reported live capacity 150. At tick 2 the harness proved the compatible Defence
queue idle, set the busy side to zero cash, and created `upgrade.recon2`; the
prerequisite became visible at tick 3. Restoring 10000 cash then yielded an accepted
exact `gtwr` request, and the Defence queue was occupied at tick 4 before resources
were set to 120/150.

Independent SkyNet logs show that exact `gtwr` placed live at its preferred target
`(79,92)` before the busy side reserved and completed one Silo. The free side also
placed its preferred `obli`, and both sides ultimately completed exactly one Silo
with capacity 4150. The Lua `Build` callback never fired despite the live `gtwr`,
so the callback-dependent monitor emitted `CNC101 B FAIL busy silo appeared before
producing tower completed` and stopped before proving the full free/busy boundary,
tower resumption, and reservation lifecycle. This is harness/state-order uncertainty,
not evidence that the product cancelled the tower.

## Narration, policy review, and dispositions

- Scenario A's fresh Luna narrator confirmed both faction prefixes and flagged the
  later Brutalis no-Harvester/blocked-extension state with causality unknown. The
  fresh Luna policy review rated the run `insufficient evidence` and recommended a
  multi-cash matched changed/control matrix. That recommendation is rejected for
  CNC-101: literal acceptance asks for the bounded A/B custom scenarios, cycles
  1-5 repeatedly exercise the opening, and an old-policy matrix is not a release
  gate. The late Brutalis observation remains advisory and unrelated to this change.
- Scenario B's fresh Luna narrator separated the callback FAIL marker from exact
  queue occupancy and independently logged live tower placement. The fresh Luna
  policy review rated it `insufficient evidence / required follow-up` and requested
  callback-independent queue/structure instrumentation. That recommendation is
  accepted for an evidence-only cycle 6. It was not run as a third game because
  cycle 5 was explicitly bounded to exactly two full-engine games.

## Artifacts and next step

Raw maps, manifest, logs, benchmark CSVs, staged narrator/policy inputs, narratives,
and reviews are outside Git at `.worktrees/coordinated-cnc/20260815-bug-polish-06-resume/analysis/worker-2-cnc101/cycle-05/`.
Cycle 6 should keep the product unchanged and replace the callback gate with
deterministic exact-item queue/placement/cancellation observation. It should apply
resources above the existing pressure threshold only after busy occupancy is
independently established, hold the free side away from an optional tower until
pressure is active, and repeat Scenario A plus the normal checks. Product changes
require callback-independent evidence of a product defect.
