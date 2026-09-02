# CNC-96A stable stealth lifecycle hotfix

Authority: [.agents/references/STEALTH-TANK-LIFECYCLE.md](.agents/references/STEALTH-TANK-LIFECYCLE.md)

## Goal

Produce a stable, simple, modular stealth-squad release candidate. Every lifecycle
stage owns the squad until explicit completion/handoff. Strategic work uses the
cache; local engagement uses current live actor positions and standard threat
calculation. Preserve established balance and build-order behavior.

## Required behavior

- Visceroids have priority `-1` in every stealth profile.
- Target choosing prefers cells worth at least one Harvester
  (`5000 * 1100`); use lower-value cells only when no qualifying cell exists.
  This filter never affects routing.
- Use bounded cached A*-like acquisition, retain the incumbent, and prefer
  separation from other stealth squads without forcing exclusive targets.
- Approach follows the cached strategic route. Obelisks are passable while
  cloaked; detectors are real route danger according to actual coverage.
- Local engagement is live-only. Do not use the strategic cache for local safety,
  target positions, crush, kite, or mass attack decisions.
- Undefended attack uses configured priority/value and finishes its retained live
  target instead of firing once and forgetting it.
- Crush remains cloaked, rejects actual detector coverage, and orders the target's
  current live position. Rare allied crush splash reveal is out of scope.
- Kite uses one shared squad action: fire from a currently safe position, move to
  a safe firing position, or explicitly yield to mass attack/flee.
- Mass attack begins only above crossover 2, attacks the highest live threat,
  continues above 1, and flees at or below 1.
- After live combat chooses to flee, use the strategic cache for a simple safest
  route roughly two strategic cells outward. Damage uses the safest repair route;
  if none is safe, resume the fight.
- Tactical owner state, plans, timers, fingerprints, and orders are not saved.
  Save allocations only and load each squad into TargetAcquisition.
- Normal strategic replanning must not interrupt active local combat. Replan after
  arrival, kill, empty cell, mission completion, or explicit behavior yield.
- Keep behavior owners short and independently testable; under 400 lines per
  class where practical and never above 500.

## Permanent diagnostics

Program output, not task/state files, must report:

- no squad stalled for 30 seconds or longer;
- kill cadence per real squad, diagnostic target one kill per 45 seconds;
- no Obelisk-attributed stealth-tank death;
- average killed value per minute per stealth tank over that tank's lifetime;
- the permanent comparable damage-divided efficiency score using killed value,
  tank lifetime, and damage taken.

Cadence and efficiency are diagnostic review signals, not target filters. A large
regression requires code and policy review.

## Validation

1. Unit-test each lifecycle owner and its explicit handoff independently.
2. Pass the no-resource four-squad scenario: VIKI has four squads of two stealth
   tanks and an immortal off-map base; Brutalis has infantry, rockets, Mammoths,
   artillery, Obelisks, structures, and Harvesters. Bound stalls with existing
   watchdogs. Failure: all stealth tanks die without defeating Brutalis.
3. Run full natural games as VIKI versus two Brutalis players, never Skynet for
   normal covert validation. Compare permanent watchdog output and efficiency.
4. Use save/reload fixtures near a single opportunity or death for fast focused
   iteration, but validate the release candidate in full games.
5. Before accepting a behavioral change, run three full games and compare mean;
   use at least five games when variance or an outlier makes the result unclear.

## Current implementation status

- Release candidate implemented with a single explicit owner per lifecycle
  stage; behavior files are 43-251 lines.
- Tactical serialization, preplanned Kite/MassAttack machinery, and repeated
  in-flight Approach/Kite/MassAttack/Flee/Repair orders are removed.
- Approach uses cached A* only for strategic movement, but yields to the
  least-dangerous live flee owner when exact standard-range calculation finds
  an exposed current position unsafe.
- MassAttack now follows the authority: move once to a safe firing cell when
  one exists, otherwise commit against the highest live threat while crossover
  remains above one.
- Focused lifecycle/threat tests: 100/100. Full .NET: 865/865. Headless runner:
  14/14. Diff whitespace check passes.
- Final special scenario: VIKI win at tick 6187; all eight stealth tanks alive;
  no stall or Obelisk-death failure; primary efficiency 1101.91 and
  damage-adjusted efficiency 5.754. Cadence remained diagnostic (one pass,
  three quiet-terminal-tail failures).
- Final five natural VIKI-versus-two-Brutalis games: 5/5 wins; no stall or
  Obelisk-death failure; every active terminal squad passed cadence. Primary
  scores: 1808.48, 1985.24, 1707.70, 2161.47, 1955.57 (mean 1923.69,
  median 1955.57). Damage-adjusted median: 0.589.
- The prior reviewed human replay baselines were primary 869.88 / adjusted
  0.220 and primary 417.01 / adjusted 0.024; final natural median exceeds both.

## PR 143 follow-up evidence

- Lifecycle rule 4D now states that in-flight move orders are retained so engine
  pathing can work. On long-obstacle maps, Approach must retain its issued
  strategic waypoint while the formation is moving even when its cached route
  recalculates from an off-course strategic cell. Reissue only after movement
  finishes or active membership changes; do not add a replacement routing system.
- The five-run disabled-module baseline and five matched full-module runs are
  recorded in `CNC-AI-PERFORMANCE-BENCHMARK.md`.
- The generated special map has no resource-layer contents and gives VIKI four
  two-tank squads against the requested mixed Brutalis force.
- Three final `playtest-20260901` runs passed. The retained seed `9609314` replay
  is a VIKI win at tick 4218 with six tanks surviving, all four squads passing
  cadence, and no stall or Obelisk-death failure. Primary efficiency is 1979.05;
  damage-adjusted efficiency is 0.4362.
- Human-review artifacts are `tests/CNC-96A-Four-Squad-Lifecycle.oramap` and
  `tests/CNC-96A-Four-Squad-Passing-playtest-20260901.orarep`; replay and map UID
  are both `e5f1fe6b06ef14a1b921f0af69fa2073763af654`.

## Current PR 143 work order

Work and validate one item at a time:

Completed: the replay's first stank was already generic-fallback controlled at
tick 7643, so its later targeting/deaths are not lifecycle evidence. Its long-route
oscillation is fixed by retaining the active engine move. Flee detector coverage now
snapshots actors once per live evaluation (same candidates/threat math), cutting the
measured flee cost 86.6%. All stealth definitions now use one modular runtime;
chemical behavior remains configuration-driven.

The 17-by-17 live flee search is replaced by a simple cached strategic route
roughly two cells outward. Expensive strategic work now throttles through a
central 1/2/4 interval factor before the retained final failsafe; live local
combat remains responsive. MassAttack reuses one live calculator evaluation and
bounds safe-cell checks. Same-tick owner cycles wait for the normal scheduler.

The off-by-default F8 spectator/replay overlay records and displays squad
identity, center, phase, blue considered-target lines, and the purple chosen
target line with endpoint labels. Replay playback resolves the sparse orders
without desync; old replays simply contain no overlay metadata.

Remaining:

1. Publish the final evidence update to PR 143.

Final natural CPU evidence is recorded in `CNC-AI-PERFORMANCE-BENCHMARK.md`:
five control and five full-module games passed natural completion. Full modules
averaged `2.538x` wall-clock speed (slowest `2.160x`) with no advanced-module
disable. The release-candidate behavior commit is available on PR 143 for human
testing; complete .NET, benchmark-tool, Release build, and CNC rules/map
validation are green.
