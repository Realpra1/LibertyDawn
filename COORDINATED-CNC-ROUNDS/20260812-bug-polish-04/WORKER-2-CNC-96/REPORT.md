# CNC-96 cycle 1 report

## Status

Proposed `First iteration - testing`. Cycle 1 added bounded opt-in diagnosis, not a gameplay fix. The evidence proves large separated startup tails and a severe completion boundary at tick 25 under the artificial one-CPU/600-mobile-actor setup, but it does not yet prove a repeating player-visible rendered freeze or one owning component. No balance, policy, cadence, feature, or AI workload was changed.

## Implementation

- Benchmark mode now writes a bounded `periodic-stall-v1` report: fixed 1ms histograms through 10s, at most 128 worst tail events, at most 512 runtime samples every 25 completed world ticks plus tick 1, and deterministic per-player/per-module aggregates.
- Correlated samples include completed logic/render/present tails, module calls/time/queued orders, process CPU/working set, managed and allocated bytes, GC generations, process disk bytes, log bytes, actors/effects, `/proc/loadavg`, CPU-frequency/thermal values when exposed, and explicit unavailable markers.
- Normal play takes the old path. Per-module stopwatch spans exist only while benchmark mode is active; no formatted per-tick/per-actor diagnostic stream was added.
- A public pure classifier distinguishes uniform slow, isolated, and periodic tails using nearest-rank quantiles and cadence tolerance. The report de-duplicates world ticks after the Game 1 narrator correctly identified repeated legacy benchmark rows.
- An initially recursive sysfs thermal probe was caught by the rerun and replaced with the single bounded `thermal_zone0/temp` file. Unavailable counters remain nonfatal.

## Equivalence gate (before any future conditional fix)

| Invariant | Old/configured | Cycle 1 | Disposition |
|---|---|---|---|
| AI cadence and long-run calls | Every enabled `IBotTick` remains invoked by `ModularBot` in existing order | Unchanged; instrumentation surrounds calls only in opt-in benchmark mode | Required for any later fix |
| Immediate/event-driven triggers | Attack response, unsafe exposure, destroyed assignments, defenders, economy recovery and unload/safety retain existing paths | Unchanged | Any later phase change must prove the existing response bound |
| Field locality/reservations/production | Existing per-player fields, hazards, routes, reservations, stance and queues | Unchanged | No guard cache/phase code selected |
| Save/load | Existing simulation state only | No diagnostic state is serialized or synced | Longer save/load game still required |
| Deterministic ordering | Existing module order and simulation decisions | Stopwatch/report values are unsynced output; module order unchanged | Replays differ by timestamp; decision parity measured by calls/orders |
| Workload/policy metrics | Exact base uses same map bytes, seed, bots, factions, teams/spawns, options, content and CPU affinity | Changed default-off: 1,008 module calls, 40 queued orders; logging-on identical | No gameplay optimization claimed |

## Setup and artifacts

- Scenario generator/artifacts: `/root/github/LibertyDawn/AUTONOMOUS-CNC-LOGS/20260812-bug-polish-04/WORKER-2-CNC-96/cycle-1/`; enabled map SHA-256 `e10c252a154dd9797ffef7c648aae3d794e93d51ddf807c169e53df670d7bc46`.
- Two ordinary `ironreaper` AIs, Nod/Nod, teams 1/2, spawns 1/2, seed 9601, `startingcash 20000`, MAX, 300 mobile actors plus six structures per player, mixed infantry/vehicles/AA/artillery/stealth/harvesters, living map resources, all current modules.
- Fixed old-host constraint: `taskset -c 0`; process allowed CPUs were 0-3, all golden legs reserved both game slots and ran serially. Runtime content: `/root/github/LibertyDawn/.build/cnc33a/runtime-content` with isolated `SupportDir/Content` symlink verified by successful content load.
- Package/source defaults verified: `PerfText`, `PerfGraph`, `BotDebug`, and `EnableSimulationPerfLogging` false. Game 1 explicitly used simulation logging false; Game 2 explicitly enabled it with 0.1ms threshold.
- The initial content-parent mistake and tick-1200/50/25 timeouts are retained under the analysis directory as invalid setup evidence and are not counted as games.

## Counted games and causal matrix

### Game 1: all modules, package logging off

Hypothesis/failure/pass were recorded before launch in `GAME-PLAN.md`. Final artifact: `game-1-final2/`. It passed world tick 24 in 5.008s with the required map/bot/exit markers and no fatal/desync.

The corrected bounded report measured 24 distinct completed ticks: median 6ms, p95 329ms, p99/max 1226.703ms, three >=50ms tails at ticks 1-3, no rendered/present samples because this was headless. Player 1 Base Builder owned 124.115ms of tick 1, but the completed tick was 1226.703ms, so proximity does not assign the remaining cost. Tick-1 runtime sample: process CPU 3570ms, working set 477,229,056 bytes, managed 127,692,912, allocated 699,443,552, GC 81/32/14, write bytes 73,728, log bytes 4,096, actors 748, effects 4; CPU frequency and thermal were unavailable.

Attempts to complete tick 25 repeatedly entered a logged Harvester field-station boundary and exceeded 120 seconds. Because no tick-25 completion sample exists, this nominates matched module interventions but does not prove field defense/harvester/base planning causal.

Narrative: `cycle-1/game-1-analysis/commenter/NARRATIVE.md`. Policy: `cycle-1/game-1-analysis/policy/POLICY-REVIEW.md` (`insufficient evidence`, high confidence). Facts were verified; the narrator's missing seed/options concern is an artifact-isolation limitation—the launcher manifest records them.

### Game 2: controlled simulation logging enabled

Final artifact: `game-2-final2/`. Identical scenario/seed/content/affinity/workload, with simulation perf logging enabled at 0.1ms. It passed tick 24 in 5.004s. Corrected report: median 7ms, p95 329ms, p99/max 1200.431ms, three >=50ms startup tails. Module calls/orders were identical. `perf.log` grew from 7,046 to 13,631 bytes; tails remained parity. Tick-1 allocations differed by only 46,648 bytes and GC generations were 81/33/14 versus 81/32/14.

Conclusion: short-window formatting/output is not a material cause before tick 25. The run is too short to exercise repeated five-second flushes; logging remains unproven rather than exonerated.

Narrative: `cycle-1/game-2-analysis/commenter/NARRATIVE.md`. Policy: `cycle-1/game-2-analysis/policy/POLICY-REVIEW.md` (`insufficient evidence`, high confidence).

### Controls and required matrix

- Exact common-base `4e12088061`: `base-control/`, passed tick 24 in 5.004s. Legacy benchmark rows: median 6.519ms, p95 315.327ms, max 1171.021ms, three >=50ms. This is tail parity with the instrumented build, so profiler overhead was not materially visible in the bounded short run; it is not an improvement claim.
- Newest advanced modules enabled: Game 1 above. Newest advanced-off map: `matrix-newest-advanced-off-valid/`, passed tick 24; legacy rows median 4.025ms, p95 262.997ms, max 1224.377ms, three >=50ms. Lower median/p95 but identical freeze count/max means disabling features is neither acceptance nor an authorized fix. A tick-25 advanced-off attempt also failed, so the removed module group is not isolated as cause.
- Historical `8024fd2c6f377fc0744777b52daef3b7a8a4682f` built successfully with its period content, but predates canonical headless, lobby-command, deterministic-seed, and bounded-exit automation. It cannot run the identical ordinary-Iron-Reaper matrix without product/harness backports that would violate the clean named-control contract. This leg is explicitly incompatible/incomplete, not counted or compared.
- CNC-100 was initially unchanged at the common base, then advanced to `4e0ba78fd3` after the final games. This cycle did not inspect or merge it. Because it changes advanced squad CPU load/timing, all later integration/final controls are contaminated until rerun at the integration head.

## Reviewer recommendations and dispositions

- Game 1 highest priority: matched longer old-control run with completed module/output/GC/render/host attribution before any scheduling change. **Accepted** for cycle 2; cycle 1 added exact-base evidence but the extreme tick-25 boundary prevents a longer valid leg. Replacement next test: reduce geometric/pathological overlap without reducing the required actors, use a paced rendered launcher, and run matched tick-25 module intervention.
- Game 1: do not shorten Base Builder from one early spike. **Accepted**; no gameplay fix made. Next test will measure exclusive owned stages/player asymmetry.
- Game 2 highest priority: logging on/off long enough to cross five-second flush boundaries while preserving calls/orders. **Accepted**; short pair crossed five wall seconds only at shutdown, so a lower-perturbation longer scenario is required.
- Safety/extension-policy advice was **rejected as out of scope** for CNC-96: negative safety scores are contextual planner diagnostics and this task has frozen policy. Replacement is performance-only attribution with unchanged decisions.
- No scratchpad promotion: reviews proposed no new durable general policy beyond the already canonical behavior-preserving performance rule.

## Checks

- `dotnet test OpenRA.Test/OpenRA.Test.csproj --filter FullyQualifiedName~PeriodicStallClassifierTest`: 3/3 passed.
- `make all`: passed, zero warnings/errors in final Release build.
- `git diff --check`: passed.
- Full-engine launcher validation: both final counted games and exact-base/newest-off controls passed required world-tick/map/bot/content/benchmark/exit/fatal checks.

## Deferred work / risks

- Build a paced rendered companion that preserves canonical content/preflight/cleanup/120s bounds; headless MAX has no present samples and cannot meet literal acceptance.
- Rework the adversary so 600 actors remain but tick 25 completes, then match field-heavy versus equal-load no-committed-field, module/phase toggles, and unconstrained host.
- Add explicit benchmark overhead counters, thread CPU/scheduling/throttle where exposed, GC pause duration where supported, logger queue/backlog/flush markers, save/load/replay continuation, and gameplay outcome counters.
- The current runtime sample is intentionally sparse and performs one bounded log-directory enumeration every 25 ticks in benchmark mode. Its overhead needs a longer profiler-on/off control.
- No PR was opened in this first-iteration cycle; coordinator publication can follow after reviewing the commit.

# CNC-96 cycle 2 report

## Result

Added bounded paced rendered automation without changing simulation or AI policy. It preserves isolated content, benchmark, replay, world-tick, timeout, and configured-exit behavior while keeping the normal render/present path active. Two serial one-CPU, 600-mobile-actor IronReaper runs completed tick 25 and captured presented timing. This proves a player-visible startup/render tail exists in the constrained setup; it does not identify an owning cause or justify an AI scheduling fix.

## Evidence

- Exact map SHA-256: `e10c252a154dd9797ffef7c648aae3d794e93d51ddf807c169e53df670d7bc46`; seed 9601, normal speed, two NOD IronReapers, same teams/spawns/cash/content, `taskset -c 0`, serial Xvfb display 90, 748 actors.
- Package-default paced game: tick 25 in 7.007s. Tick p50/p95/p99/max 16/547/1259/1258.279ms (4 >=50ms); render 49/68/215/214.435ms (23); present 1/47/199/198.517ms (1). Player-1 BaseBuilder max 186.848ms and StealthTankSquad max 58.056ms at tick 1, but the 1.258s tick remains unattributed.
- Logging-on paced game: tick 25 in 7.011s. Tick 18/537/1221/1220.599ms (5); render 48/65/179/178.227ms (23); present 1/46/48/47.288ms (0). Calls/orders and actors remained equal; log bytes rose 8,192 to 13,926. The short interior window does not exercise the five-second flush, so logging remains unproven.

## Review disposition and checks

Fresh commenter/policy artifacts exist per game under `cycle-2/game-{1-paced-default,2-paced-logging}-final-analysis/`; both high-confidence verdicts are `insufficient evidence`. Adopted: preserve all AI work/decisions, repeat comparable paced runs, and separate configured-stop/natural-game-over/winner reporting. Rejected: deferring or changing BaseBuilder/StealthTankSquad work, which would alter frozen AI work without causal proof.

- `python3 -m unittest tests/test_launch_ai_parallel.py`: 5/5 passed.
- `dotnet test OpenRA.Test/OpenRA.Test.csproj --filter FullyQualifiedName~LaunchArgumentsTest`: 14/14 passed.
- `git diff --check`: passed.
- Protected full `make all` helper is absent from this worktree; focused .NET compilation occurred through the test project. The next worker must run the protected full build once its canonical helper is restored/located.

## Next evidence-driven step

Do not change AI cadence or workload. First add bounded attribution that separates tick-1 engine/init, module, render/present, GC, and host-scheduler cost; repeat the exact paced control, then make a same-build intervention only if it materially moves that measured boundary.

# CNC-96 cycle 3 report

## Result

Added bounded, benchmark-only attribution for the existing `immediate`, `order-generator`, `world`, and `tick-render` logic phases. The samples are restricted to the active simulation world, preserving the benchmark's existing primary-world identity. No simulation decision, order, cadence, AI workload, policy, or balance setting changed.

## Final valid games

- Constrained (`taskset -c 0`): tick 25 in 7.011s; tick p50/p95/p99/max `16/351/1226/1225.233ms`, five >=50ms; render `36/69/192/191.159ms`, 24/50 >=50ms; present max `48.186ms`. Attribution: `immediate` max `1168.771ms` at initial local tick 0, `world` max `299.140ms` at tick 1, player-1 BaseBuilder max `118.070ms` at tick 1.
- Unconstrained affinity: tick 25 in 5.006s; tick `11/294/1083/1082.219ms`, three >=50ms; render `7/28/100/99.937ms`, 1/95 >=50ms; present max `21.104ms`. Attribution remains startup-dominated: `immediate` max `1049.766ms`, `world` max `260.618ms`, BaseBuilder max `102.391ms`.

Both serial paced rendered legs used the exact cycle-2 map SHA, seed 9601, two Nod IronReapers, 748 actors, normal speed, package diagnostics, staged runtime content, and configured tick-25 exit. They passed required map/bot/exit markers with no exception or desync. Final artifacts are under `cycle-3/game-{1-constrained,2-unconstrained}-final/`; fresh analysis accompanies each final leg.

## Interpretation and disposition

The new evidence attributes the dominant tail to startup `immediate` work, with secondary early `world` work; the named bot module is substantially smaller than the unexplained immediate maximum. The unrestricted control reduces observed tails and recurring rendered-widget cost, but it does not eliminate the startup stall. At 25 ticks neither leg establishes a periodic simulation cadence or a causal owner for a behavior-changing fix. Logging remains unproven and no AI schedule/field-defense intervention is authorized.

The required Luna code review found that phase samples could include a secondary shellmap world. The final code confines phase recording to `Game.OrderManager`; the two final legs were rerun after that reporting-integrity repair. The concern is adopted and closed.

## Checks

- `dotnet test OpenRA.Test/OpenRA.Test.csproj --filter 'FullyQualifiedName~PeriodicStallClassifierTest|FullyQualifiedName~LaunchArgumentsTest'`: 18/18 passed.
- `python3 -m unittest tests/test_launch_ai_parallel.py`: 5/5 passed.
- `git diff --check`: passed.

## Next evidence-driven step

Keep all AI work unchanged. Extend the same matched affinity pair beyond startup and add bounded per-tick phase joins plus available scheduler/GC-pause attribution before testing any owning-boundary intervention. The existing 25-tick evidence supports diagnosis only, not acceptance or a gameplay fix.
