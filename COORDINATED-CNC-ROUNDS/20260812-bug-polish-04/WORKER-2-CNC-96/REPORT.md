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

# CNC-96 cycle 4 report

## Result

No product code or gameplay policy changed. The observation window was extended from 25 to 150 rendered world ticks using the exact existing map, seed, roster, content, normal pacing, and package diagnostics. The corrected serial affinity pair shows that the startup tail is separated, while a second aligned world/module stall occurs at tick 76. It is a causal candidate, not a proven fix boundary: both StealthTankSquad module spans and the enclosing world phase coincide, but no same-build intervention has yet moved it.

## Valid games and comparison

- Constrained (`taskset -c 0`): passed tick 150 in 17.023s. Completed tick p50/p95/p99/max was `17/47/502/1198.630ms` with six >=50ms samples; render `59/63/170/169.391ms` with 143 samples; present max `45.872ms` with none. Startup immediate was `1151.204ms`; at tick 76, world reached `496.674ms`, player-1 and player-2 StealthTankSquad reached `368.974ms` and `115.081ms`, and the following completed tick was `501.650ms`.
- Unrestricted affinity: passed tick 150 in 11.012s. Completed tick p50/p95/p99/max was `10/28/417/1058.724ms` with four >=50ms samples; render `20/27/31/104.551ms` with one sample; present max `20.117ms` with none. Startup immediate was `1019.671ms`; at tick 76, world reached `415.176ms`, the same two modules reached `309.353ms` and `98.712ms`, and tick 77 was `416.597ms`.
- Both runs retained 150 calls for ordinary modules (300 for each StealthTankSquad module), unchanged actors through the relevant window (748 until tick 100; 750 later), the required map/bot/exit markers, benchmark/replay artifacts, and no exception/desync. The valid artifacts are `cycle-4/game-{1-constrained-final,2-unconstrained-final}/`; fresh commentary/policy reviews are at the corresponding `game-{1,2}-analysis/` directories.
- The explicit `mods/cnc` content override was an invalid setup: it timed out loading content before world tick 1. It is retained at `cycle-4/game-1-constrained/` and is not counted. The final pair verified the previous successful isolated `SupportDir/Content` target `/root/github/LibertyDawn/.build/cnc33a/runtime-content` before launch.

## Interpretation and next gate

Removing one-CPU affinity materially improves ordinary rendered flow (constrained render p50 59ms versus 20ms and 143 versus one render tails), but does not remove the startup or tick-76 simulation tails. Host capacity is therefore a material amplifier, not an isolated complete diagnosis. The tick-76 alignment is the first repeated non-startup candidate and must receive a same-build, workload-equivalent diagnostic intervention before any behavior-preserving change. Preserve call counts, module order, urgent work, reservations, save/load state, deterministic replay, and all balance values; do not phase-shift or disable AI work from temporal proximity alone.

The fresh reviewers accepted the frozen-policy, valid-rendered evidence and require the exact affinity parity comparison; that comparison has now passed. Their next recommendation is to compare completed post-startup tick/render/present and phase tails under a narrowly controlled candidate test. This is adopted for the next cycle. No reviewer advice was rejected.

## Checks

- `dotnet test OpenRA.Test/OpenRA.Test.csproj --no-restore --filter 'FullyQualifiedName~LaunchArgumentsTest|FullyQualifiedName~PeriodicStallClassifierTest' --nologo`: 18/18 passed.
- `git diff --check`: passed.
- Two valid full-engine, serial paced rendered games passed at world tick 150; each process remained within the 120-second cap.

## Remaining risks

- GC-pause and scheduler-throttle metrics remain unavailable; the logger flush/backlog, save/load/replay continuation, and historical/newest matrix remain incomplete.
- The affinity contrast cannot identify whether tick-76 belongs to StealthTankSquad internals, the enclosing world work, allocation/GC, or another simultaneous owner. A causal same-build intervention is still required before a code fix.

# CNC-96 cycle 5 report

## Result

No product code or AI policy changed. A diagnostic-only, in-map control removed exactly `StealthTankSquadBotModule` and `StealthTankSquadBotModule@chemical`, retaining the same 600-actor placement, seed `9601`, IronReaper factions/roster, normal rendered pacing, package diagnostics, staged content, one-CPU constraint, and tick-150 exit. This intentionally reduces work, so it is causal evidence only and cannot be a product fix or acceptance claim.

## Valid paired games

- Enabled control: `cycle-5/game-1-enabled/` passed tick 150 in `17.024s`, with no exception/desync. Completed tick p50/p95/p99/max was `16/46/497/1201ms`; the tick-76 world span was `494.492ms`, including player-1/player-2 StealthTank spans `365.131/112.318ms`, and tick 77 was `496.280ms`. The run settled to ordinary post-boundary tick times and kept the candidate modules at 300 calls per player.
- Candidate-off diagnostic: `cycle-5/game-2-candidate-off/` passed tick 150 in `17.020s`, with no exception/desync. Completed tick p50/p95/p99/max was `17/45/338/1329ms`; the same boundary was only `67.181ms` at tick 76. The two candidate module identities are absent; all listed remaining modules retained 150 calls per player. Startup immediate work persisted and increased to `1272.448ms`; paced render remained ~`59ms` p50 with 142/176 samples above the diagnostic 50ms threshold, while present stayed below threshold.

The paired movement strongly nominates the two-trait boundary as a contributor to the tick-76/77 simulation stall, but it does not distinguish the traits' intrinsic cost from downstream workload/order effects and it violates workload equivalence by disabling ordinary AI work. It therefore does not authorize a phase shift, cache, reduced scan, or other behavior change.

## Review disposition and handoff

Fresh Luna commenter and policy artifacts are under `cycle-5/game-{1,2}-analysis/{commenter/NARRATIVE.md,policy/POLICY-REVIEW.md}`. Both games were accepted as valid frozen-policy evidence. The Game 2 reviewer’s strongest recommendation—adopted—is a repeated like-for-like paired comparison with workload/outcome/equivalence metrics before assigning ownership. This primary tier is exhausted (five cycles), so the task is handed off as `Needs help` rather than making an unproven AI change.

Checks: both serial paced full-engine games passed the launcher’s map/bot/content/benchmark/exit validation within 120 seconds; `git diff --check` passed. Cycle 4's focused automated checks remained `18/18`; there was no code to rebuild in cycle 5.

# CNC-96 explicit Sol-high continuation cycle 6 report

## Result

No product code, balance value, cadence, or AI policy changed. This cycle tested
the original Economy-field guard hypothesis directly with two ordinary Nod
Brutalis bots on forced Economy II, twelve pre-spawned harvesters across three
living fields and 288 other mobile actors per player. Canonical headless MAX was
used so unmodified harvest/unload timing could commit real fields within the
120-second wall bound. The matched in-map control removed only
`EconomyFieldDefenseBotModule`; all actors, structures, fields, seed `9606`, bots,
factions, teams/spawns, cash, other modules, staged content, one-CPU affinity,
diagnostics, and tick-2000 exit were retained.

The enabled map SHA-256 was
`a0d273c7495bbf752e3415d8d2dfe8b8a6b6d845fbac2f77e717f33c3632694e`;
the disabled control was
`79909ae20d9a2ac8aebbff2a0b21ba69bec93b2fc96254c78952a3ef972277f4`.
Their sole intentional difference is the diagnostic trait override and enabled
guard logging. Invalid rendered setup attempts that reached tick 600/1000 without
a committed field are retained under `cycle-6/game-1-enabled{,-final}/` and are
not counted.

## Valid games and comparison

- Guard enabled: `cycle-6/game-1-enabled-max/` passed tick 2000 in `33.041s`
  with no exception/desync. Completed ticks were p50/p95/p99/max
  `9/17/160/1377.099ms`, mean `14.994ms`, with `39` >=50ms samples and reported
  cadence `74`. Three fields committed for Brutalis 2; the module recorded 11
  defender assignments, 14 reforms, and 45 queued orders. Its two player spans
  totaled `136.462ms` across the normal 2000 calls per player, with direct maxima
  `22.340/46.426ms`.
- Guard-disabled control: `cycle-6/game-2-control-max/` passed tick 2000 in
  `32.025s`. Completed ticks were `9/18/115/1369.828ms`, mean `14.514ms`, with
  `33` >=50ms samples and cadence `75`. Six actual unloads completed; no guard
  event or module span was present. Initial actors were equal at 760; terminal
  actors differed (`657` enabled versus `625` control), so later strategic work
  and outcomes are not workload-equivalent.
- The first active guard scan at logic tick 1576 led to completed tick 1577 of
  `182.698ms` versus `22.437ms` in control, but the enabled world span also
  contained a `110.649ms` StealthTankSquad span. At logic tick 1851, after two
  more fields committed, completed tick 1852 was `59.022ms` versus `17.563ms`.
  At logic tick 1951, completed tick 1952 was `472.821ms` versus `203.445ms`,
  while the enabled StealthTankSquad spans totaled about `460ms` versus the
  control's `181.308ms`. Guard reservations/orders therefore changed downstream
  squad work; proximity cannot assign the full tail to guard planning.

This is a causal nomination, not a correction gate. The direct guard span stayed
below 50ms, only three fields became active late, the disabled control necessarily
changed ordinary reservations/orders, and the largest aligned tails remained
dominated by simultaneous StealthTankSquad work. The modest p99/freeze-count and
wall-time movement is not enough to select a behavior-preserving cache, phase
shift, or reduced policy. No correction was made.

## Reviewer disposition and next authorization

Each valid game received its own fresh Luna-medium Commenter and Policy Reviewer
under `cycle-6/game-{1,2}-analysis/`. Both policy verdicts were `insufficient
evidence` with high confidence. Game 1 accepted the mixed Medium Tank/two-infantry/
MSAM screen as sensible Brutalis Economy behavior. Both strongest recommendations
were adopted: preserve exact field safety, reachability, reservations, production,
recovery, determinism, and urgent response, then repeat the matched test with a
workload-equivalent simplest idle/guard-near-owned-field replacement rather than
treating disabled guards as a product fix. Reviewer suggestions about extension
fallback, threat policy, or balance were not adopted because they are outside
CNC-96's frozen performance scope.

Checks: both serial full-engine games passed canonical map/bot/content/tick/
benchmark/exit/fatal validation while holding both game slots and remained below
120 seconds; staged content was
`/root/github/LibertyDawn/.build/cnc33a/runtime-content`; `git diff --check`
passed. No product source changed, so no new build was required. Cycle 7 is
authorized by the user's five-cycle Sol-high continuation (cycles 6-10) and must
be a fresh invocation; it should use the reviewer-requested workload-equivalent
replacement control and must not redesign or tune balance.

# CNC-96 explicit Sol-high continuation cycle 7 report

## Result

Added a default-off diagnostic `SimpleIdleGuardControl` to the existing
`EconomyFieldDefenseBotModule`. Normal play remains on the old path. The control
uses the same 25-tick scan, field detection, deterministic candidate gate, mixed
role counts, reservations, defensive stance, safety hazards, production demand,
loss replacement, and save-owned destination state. It plans one safe local
anchor per assigned defender, then issues a safe return only after displacement
instead of repeatedly managing the normal field formation. No balance value,
ordinary default, strategy composition, cadence, or other AI module changed.

The cycle started from cycle 6's forced-Economy-II Brutalis setup and retained
12 harvesters plus 288 other mobile actors per side, three living field clusters,
all modules/features, normal enemies and losses, seed `9607`, one-CPU affinity,
MAX speed, tick-2000 exit, and staged content
`/root/github/LibertyDawn/.build/cnc33a/runtime-content`. The enabled map SHA-256
was `1c30dbe204c6f917abf0af35a8f99406e04cce05b9e1c5187e0288048cb3a179`;
the diagnostic-control map was
`0271a911270b692c8f367f47a8ca3d4fbf83bfc4a352fc45db635ce7b78d031b`.

## Equivalence gate and valid games

| Gate | Guard enabled | Simple idle control | Disposition |
|---|---|---|---|
| Cadence/module calls | 2,000 calls/player; 25-tick configured scan | Identical calls/cadence | Pass |
| Initial actors/content/seed | 760 actors; same geometry/roster/content/seed | 760; matched | Pass |
| Field safety/reachability | Safe routed formations; no forbidden occupancy | Safe local anchors/returns; no forbidden occupancy | Bounded scenario pass |
| Mixed reservations/recovery | Four fields by tick 2000; 20 assignment events including four missing-role replacements | Seven fields; 30 assignment events including two replacements | Fail: different committed workload |
| Guard work/orders | `190.017ms` total, `37.979ms` max, 92 queued orders | `270.095ms` total, `43.444ms` max, 289 queued orders | Fail: control did more direct work/orders |
| Harvesting/production | Four unloads; Harvester orders 14; UnitBuilder orders 41 | Seven unloads; Harvester orders 12; UnitBuilder orders 39 | Active in both; not equivalent |
| Ordinary squads | SquadManager 5,272 orders; StealthTank 2,108 | SquadManager 5,288; StealthTank 2,418 | General squads near parity, advanced squad work diverged |
| Loss/load outcome | 615 terminal actors | 492 terminal actors | Fail: materially different losses/downstream load |
| Completed tick tails | p50/p95/p99/max `8/17/112/2868.549ms`; 29 freezes; `35.042s` wall | `8/18/136/2868.109ms`; 34 freezes; `36.052s` wall | No material improvement; control is worse at p99/count/mean |
| Determinism/save-load/urgent threat | One fixed-seed run; losses/replacement occurred | One fixed-seed run; losses/replacement occurred | Repeat replay/save and explicit threat bounds remain unproven |

- Game 1, `cycle-7/game-1-enabled/`, passed canonical validation at tick
  2000 in `35.042s`, with mean tick `15.885ms`, p99 `112ms`, 29 >=50ms
  samples, four total committed fields, no exception/desync, continued unloads,
  complete mixed screens, and missing-role replacement. Its largest tails were
  again dominated by the enclosing world and StealthTankSquad spans, not direct
  guard work.
- The first simple-control launch, `cycle-7/game-2-simple/`, passed the engine
  bound but exposed an invalid test assumption: requiring already-near candidates
  left only one MSAM assigned. It is retained as harness evidence and is not the
  counted comparison. The corrected control reused the normal candidate gate and
  one-time safe station placement.
- Game 2, `cycle-7/game-2-simple-final/`, passed tick 2000 in `36.052s`, with
  mean tick `16.514ms`, p99 `136ms`, 34 >=50ms samples, seven committed fields,
  no forbidden occupancy/exception/desync, continuing unloads, full mixed role
  counts, and replacement after losses. It did not reduce tails and was not
  workload-equivalent: direct guard orders tripled, terminal actors fell by 123,
  and downstream StealthTank work increased.

The replacement therefore fails the task's causal and equivalence gates. It is
not a product fix, and it supplies no basis for a cache, phase shift, cadence
change, reduced coverage, or strategic policy change. The default-off diagnostic
is retained only so the next explicitly authorized cycle can falsify its threat,
blocked-anchor, and loss behavior without rebuilding a second control mechanism.

## Reviewer disposition, checks, and next test

Each valid game received its own fresh Luna Commenter and Luna Policy Reviewer
under `cycle-7/game-{1,2}-analysis/`. Game 1 found the unload-before-commit mixed
screen and role replacement sensible but causal evidence insufficient. Game 2's
single-game review called the observed idle guard conditionally acceptable while
explicitly requiring a changed/control threat test; the worker rejects a broader
behavior-preserving claim because the paired workload, losses, fields, orders,
and tails diverged. One commenter misread the effects column as actor count; the
narrative and downstream review were corrected to the verified `760 -> 492`
actor result.

Adopted strongest recommendation: cycle 8 should use one shared deterministic
scenario with a reachable scripted attack on a guarded field, a blocked or
invalidated anchor, and a defender-loss/replacement case. It must assert threat
departure/return bounds, continued unloads, field-local reservations and mixed
coverage, and compare per-tick planning/path/update tails. Extension fallback,
commit-age, threat-policy, balance, and composition suggestions remain rejected
as outside CNC-96's frozen performance scope. No durable general scratchpad entry
was proposed or promoted.

Checks: protected `make all` passed twice after the diagnostic/harness correction
with zero warnings/errors; `python3 -m unittest tests/test_launch_ai_parallel.py`
passed 5/5; `git diff --check` passed. Both final serial full-engine games held
both game slots, used isolated support/content/runtime artifacts, and stayed
within 120 seconds. CNC-100 is now at `886519f69d`; no dependency code was merged,
so prior controls remain stale for final integration.

# CNC-96 explicit Sol-high continuation cycle 8 report

## Result

The cycle-7 `SimpleIdleGuardControl` failed its pressured behavior and performance
comparison and has been removed. This is cleanup of a default-off diagnostic,
not a gameplay fix: the published Economy field-defense source is again identical
to pre-cycle-7 commit `c16b212ecd`, and normal behavior never enabled the control.
No balance, cadence, composition, threat policy, field coverage, production,
reservation, or ordinary module changed.

The final pair used two ordinary Nod Brutalis bots, forced Economy II, 12
harvesters plus 288 other mobile actors and structures per player, all normal
modules/features, seed `9608`, unrestricted host affinity while holding both game
slots for serial isolation, MAX speed, tick-2200 exit, and
the staged CNC content root `/root/github/LibertyDawn/.build/cnc33a/runtime-content`.
Both maps used identical terrain (`map.bin` SHA-256
`98bc62a4dfe6f7f6eb00ebff9a4dc4b1c11b030fa75053e185d1d524473b09f3`)
and identical scripted pressure (`f1635b370dcc2ec243d7187653e496fe8ee29990585581cae4ce69d484babc32`):
six hostile Medium Tanks attacked the reachable 73,42 field at tick 1500,
deterministic infantry were removed at tick 1750, and prior anchors were blocked
at tick 1751. The enabled archive SHA-256 was
`98f6fa9cbb7deee7d480f2647627af1c0b5579c48e7af9ad3fb82d2bc65b40e3`;
the simple-control archive was
`34b726a5960b46ea3cf4cba54b102597fecb1538c67e7328c8732630fa4f11a4`.
Their intentional map differences are title/rules filename and the one
`SimpleIdleGuardControl: true` rules override; a lobby toggle was not added
because it would broaden a diagnostic already under falsification.

## Equivalence and hot-path comparison

| Gate | Current formation | Simple idle diagnostic | Disposition |
|---|---|---|---|
| Calls/cadence | 2,200 Economy calls/player; 4,400 aggregated Stealth/Chemical calls/player; 25/75-tick cadence | Identical | Pass |
| Fields/unloads | 5 commitments, 5 unload completions | 5 commitments, 5 unload completions | Count parity only; identities/timing diverged |
| Mixed replacement/reachability | 22 assignments, 2 releases, 1 missing-role replacement, 29 bounded reforms; alternate destinations after blocking | 24 assignments, 5 releases, 2 missing-role replacements; repeated `simple-anchor-unavailable` for the same MSAM at ticks 2126/2151/2176 | Fail: retry/release churn and incomplete local recovery |
| Direct field-defense work/orders | `157.031ms` combined, `26.797ms` max, 108 orders | `269.362ms`, `28.403ms` max, 246 orders; 169 logged simple returns | Fail: 71% more direct time and 2.28x orders |
| Stealth/Chemical work/orders | `8,911.929ms`, `1,251.254ms` max, 2,237 orders | `11,130.614ms`, `1,262.557ms` max, 2,374 orders | Fail: downstream specialist work rose 25% |
| Completed ticks | mean/p50/p95/p99/max `11.370/6/14/94/2406.207ms`; 32 freezes; 75-tick cadence; `28.024s` wall | `12.417/6/13/167/2412.929ms`; 36 freezes; same cadence; `31.029s` | Fail: worse p99, count, mean and wall time; maximum unchanged |
| Terminal load/allocation | 475 actors; allocated `5,745,000,200`; GC `684/214/13`; WS `550,457,344` | 489 actors; `5,236,352,768`; GC `623/194/13`; WS `551,002,112` | Workload diverged; lower allocation did not improve tails |

The current game passed canonical validation at
`cycle-8/game-1-enabled-final2/` and the simple control at
`cycle-8/game-2-simple-final/`; each reached tick 2200 in under 32 seconds with
no exception, desync, forbidden occupancy, or missing required field/replacement
markers. Earlier runs that used unlogged display-message gates or removed actors
before commitment are retained as harness evidence and are not counted.

Source inspection explains the stable 75-tick nomination without yet authorizing
a fix. Each configured `StealthTankSquadBotModule` instance scans eligible owned
actors, materializes and sorts the full enemy set, rebuilds a threat snapshot,
scores all useful targets before taking 48, compares each retained candidate
against every threat for each active group, may count nearby infantry, and may
perform hazard-aware path searches per ordered specialist. CNC config runs both
StealthTank and Chemical instances at the same 75-tick cadence. The largest
post-startup tails in both games align with those spans (for example the simple
control's tick-1276 world `1019.187ms` contains `639.582ms + 370.686ms` of the
two players' aggregated specialist work). Economy field defense separately
enumerates committed harvesters, refineries, resource modifiers and bounded
role candidates every 25 ticks, then performs safe path searches on new,
invalidated, stalled, or pursuit-break destinations. Its direct maxima remained
below 29ms and never owned the multi-hundred-millisecond tail.

The simplest behavior-preserving Stealth candidate for a later matched test is a
scan-local immutable feature/threat snapshot or equally local memoization that
removes repeated trait/property enumeration while retaining exact enemy order,
candidate scores, danger results, route searches, orders, and 75-tick cadence.
This cycle does not implement it: the pair proves module ownership and
same-tick alignment, but downstream loss/order divergence does not yet prove
which repeated scoring/threat/path component is causal. A cache, phase change,
candidate reduction, cadence change, or weaker route check is not authorized.

## Reviewer disposition, checks, and next authorization

Each final valid game received its own fresh Luna Commenter and Luna Policy
Reviewer at `cycle-8/game-1-final-analysis/` and `cycle-8/game-2-analysis/`.
Both policy verdicts were `insufficient evidence`, high confidence. They accept
mixed screens, same-tick role replacement, safe route rejection, reform and
stance restoration as provisional behavior. The control reviewer identifies
the repeated `simple-anchor-unavailable` release/retry as its clearest weakness.
The worker adopts their strongest recommendation to keep exact bounded
completed-tail/module/order attribution and run a matched behavior-preserving
Stealth hot-path intervention next. Suggestions to widen anchors, hold missing
fields, change extension policy, or tune composition are rejected for cycle 8
because they alter strategy policy outside CNC-96's frozen scope. No durable
general scratchpad entry was warranted.

Checks: protected Release `make all` passed with zero warnings/errors;
`python3 -m unittest tests/test_launch_ai_parallel.py` passed 5/5; both serial
canonical games held both game slots and stayed below 120 seconds;
`git diff --check` passed. Cycle 9 is authorized by the user's explicit Sol-high
continuation covering cycles 6-10 and must be a fresh invocation performing
exactly one cycle. It should retain the restored normal field path and test only
an exact-decision, scan-local Stealth attribution or bounded snapshot candidate;
do not revive simple idle guards or change cadence, candidate bounds, policy,
balance, coverage, modules, or actor workload.

# CNC-96 explicit Sol-high continuation cycle 9 report

## Result

Cycle 9 tested the authorized scan-local Stealth threat optimization and did
not retain it as normal behavior. A first combined immutable-feature plus
cell-danger memoization candidate materially reduced specialist time but changed
orders and terminal workload; the cell cache was removed immediately. The final
candidate captures only target types and positions once inside the same
synchronous 75-tick scan. It also failed the exact-decision/workload gate, so
`UseScanLocalThreatSnapshot` is diagnostic-only and defaults false. The false
path avoids even the extra snapshot reads. Normal targeting, scoring, danger,
routing, order, candidate, cadence, strategy, and balance behavior remains on
the old path.

Benchmark attribution now gives duplicate module instances stable identities,
separating `StealthTankSquadBotModule/stealth-tank` from `/chemical`. This is
opt-in benchmark output only and does not affect normal order execution. No
Economy field-defense behavior changed.

## Equivalence gate and final games

The pre-change gate is recorded in ignored artifact `cycle-9/GAME-PLAN.md`:
unchanged 75-tick cadence and 2,200 calls per player/instance, actor-ID enemy
ordering, all threats and 48 candidate bounds, exact scores/danger/routes/orders,
no new urgent path or save state, deterministic scan-local lifetime, and full
ordinary AI workload. Both final games used the exact cycle-8 pressured terrain
and Lua, two ordinary Nod Brutalis bots, 12 harvesters plus 288 other mobile
actors and structures per side, all modules/features, seed `9609`, MAX speed,
tick 2200, staged content `/root/github/LibertyDawn/.build/cnc33a/runtime-content`,
`taskset -c 0` from allowed CPUs `0-3`, and both game slots held serially. The
snapshot archive SHA-256 is
`30a2641d03e90479138a3befe9687df722d5c78db57e3edbddae5302eabe3465`;
the old-path control is
`18bda695a0ae92452b5f5d5c47d444e87b530a226a226d063cb81dd8036853a3`.
Only title and the explicit true/false overrides for both specialist instances
differ.

| Gate | Snapshot enabled | Old threat-read control | Disposition |
|---|---|---|---|
| Calls/cadence | 2,200 calls per player per labeled instance; 75-tick scans | Identical | Pass |
| Chemical work/orders | P1 `5.811ms/0`; P2 `6.860ms/0` | `6.367ms/0`; `6.727ms/0` | Parity |
| Stealth work/orders | P1 `4681.447ms/1163`; P2 `8692.744ms/1657` | `5032.091ms/1081`; `5203.383ms/1097` | Fail: decisions/workload diverged |
| Field work/orders | P1 `58.111ms/46`; P2 `162.644ms/62` | `80.289ms/40`; `109.145ms/57` | Fail: downstream fields diverged |
| Squad Manager orders | P1/P2 `3645/3291` | `3247/3125` | Fail: downstream combat diverged |
| Completed ticks | mean/p50/p95/p99/max `15.886/9/17/131/1540.074ms`; 37 freezes | `13.894/8/16/115/3088.200ms`; 36 freezes | Fail: lower max only; mean/p99/count worse |
| Terminal load | 574 actors, 128 effects; allocated `5,477,846,328`; GC `654/134/14` | 495 actors, 102 effects; `5,141,815,360`; GC `614/113/14` | Fail: not equivalent |

Both canonical runs passed all required map/bot/field/replacement/world-tick
markers at tick 2200 without exception, desync, simple-idle, or forbidden-
occupancy markers and stayed below 39 wall seconds. Final artifacts are
`cycle-9/game-1-final/` and `cycle-9/game-2-final/`. The earlier combined-cache
pair is retained as rejected diagnostic evidence only. Repeating the old path
also varied materially across attempts (including specialist orders and
terminal actors), so the scenario cannot establish exact decision equivalence
from aggregate totals alone. No tail improvement or product fix is claimed.

## Review disposition and checks

Each final game received its own isolated fresh Luna Commenter and Policy
Reviewer at `cycle-9/game-{1,2}-final-analysis/`. Reviewers found the logged
mixed 1-tank/2-infantry/1-AA screens, same-role replacement, stance restoration,
and no-safe-route release sensible. They also found the intended scripted
attack/loss/block events unverified because the scenario emits no captured
runtime acknowledgement or combat/loss trace; headless artifacts cannot prove
player-visible continuity. Their highest-priority recommendation is adopted for
cycle 10: add bounded deterministic scenario acknowledgements and an exact
target/route/order decision trace before retesting a candidate. Suggestions to
change cadence, policy, composition, coverage, or balance remain rejected as
out of scope. No scratchpad promotion was warranted.

Protected Release `make all` passed after the final default-off cleanup with
zero warnings/errors; `python3 -m unittest tests/test_launch_ai_parallel.py`
passed 5/5; `git diff --check` passed. CNC-100 remains at `886519f69d` and was
not merged. Proposed status remains `First iteration - testing`: cycle 9 adds
better per-instance attribution and a default-off diagnostic but neither literal
paced acceptance nor a behavior-preserving causal fix.

# CNC-96 explicit Sol-high continuation cycle 10 report

## Result

The authorized snapshot candidate has been removed. Normal Economy field-defense
and Stealth behavior are restored exactly; no gameplay, policy, cadence, balance,
candidate, coverage, module, or actor-workload change remains. Benchmark-only
per-instance Stealth/Chemical identities remain because they distinguish the two
configured modules without affecting simulation decisions.

Cycle 10 added test-only bounded Lua acknowledgements for all six scripted attack
orders, route arrival/progress, four named terminal losses, and four blockers. It
also enabled the existing bounded specialist target/route/order diagnostics in the
ignored test map. The corrected map archive is SHA-256
`5bab991977b2d2e84586e66399af8ed96d76f2c1e7a75b752b3542a6020135e8`;
its terrain is `98bc62a4dfe6f7f6eb00ebff9a4dc4b1c11b030fa75053e185d1d524473b09f3`
and script is `e3e48b93542d4d2b2749154d26adf788d152b9e6bf2dec81abfa7a6213d36af7`.
Both final controls used seed 9610, two ordinary Nod Brutalis bots, forced Economy
II, at least 300 mobile starting actors per player, all modules, MAX speed,
`taskset -c 0`, serial ownership of both game slots, tick-2200 exit, and staged
content `/root/github/LibertyDawn/.build/cnc33a/runtime-content`.

## Equivalence gate and games

| Gate | Corrected old path A | Identical old path B | Disposition |
|---|---|---|---|
| Scenario execution | 6 attack orders; 4/4 named losses terminal; 4/4 blockers live; route result 0 arrivals/4 alive | Same requests/losses/blockers; route result 3 arrivals/5 alive | Fail: exact scenario outcome diverged |
| Cadence/calls | 2,200 calls per player/module; 75-tick specialist cadence | Identical calls/cadence | Pass |
| Exact specialist trace | 105 target and 105 hazard-route lines | 111 target and 111 hazard-route lines; targets/scores/waypoints differ | Fail |
| Stealth work/orders | P1 `3273.780ms/676`; P2 `7535.705ms/1433` | P1 `4582.265ms/962`; P2 `5298.514ms/1101` | Fail |
| Field work/orders | P1 `59.550ms/15`; P2 `83.605ms/52` | P1 `92.302ms/36`; P2 `161.210ms/62` | Fail |
| Field decisions | 3 commitments; 15 assignments | 4 commitments; 22 assignments | Fail |
| Completed ticks | mean/p50/p95/p99/max `14.972/8/19/106/3206.237ms`; 36 freezes | `15.589/9/20/138/3110.874ms`; 35 freezes | Fail: repeat variability exceeds a safe candidate comparison |
| Terminal load | 471 actors/122 effects; allocated `5,118,680,736`; GC `611/119/14` | 498 actors/109 effects; `5,268,776,152`; GC `629/118/14` | Fail |

The first valid acknowledgement game used queued `Destroy()` and showed only two
of four victims terminal at the immediate check; its fresh reviews correctly
identified the missing terminal contract. The test was repaired to immediate
`Kill()` and the two final controls above each passed canonical validation in
37.032s and 38.033s. Two earlier launches are invalid setup evidence: one Lua
property query failed before completion, and one passed tick 2200 but failed the
overly strict `dead=true` marker because queued removal is not immediate.

The final controls prove that the old path is not repeatable enough to establish
the exact decision/workload baseline required by the contract. The Lua event
hashes, exact specialist traces, downstream work/orders, actor/effect load, field
commitments, and terminal outcomes differ under otherwise matched conditions.
Accordingly the default-off snapshot was not retested and was removed, as cycle
10 explicitly required when exact equivalence could not be demonstrated. The
repeated 75-tick tails remain diagnostic evidence, not authorization for a fix.

## Review disposition, checks, and handoff

Every valid game received its own isolated fresh Luna Commenter and Policy
Reviewer. The preliminary acknowledgement review is under
`cycle-10/game-old-a-analysis/`; corrected final controls are under
`cycle-10/game-{1,2}-final-analysis/`. Reviewers conditionally accepted hazard
routing, mixed field screens, terminal named losses, blockers, missing-role
replacement and stance/reform behavior, but rejected performance acceptance due
to 35-36 freeze ticks and maxima above three seconds. Their relevant highest-
priority recommendation—explicit terminal and route evidence plus an identical
unchanged-workload control—was adopted. Proposed forced re-target/retreat and
unrelated VIKI/crate tests were rejected because they change or leave CNC-96's
frozen performance scope. No durable scratchpad promotion was warranted.

Checks passed: protected Release `make all` with zero warnings/errors; launcher
tests 5/5; focused classifier/launch tests 18/18 (one pre-existing analyzer
warning); `git diff --check`; and all three valid full-engine games under 120
seconds while holding both game slots. Proposed status remains
`First iteration - testing`. Literal paced acceptance, stable old-control
decisions, save/load/replay, GC/scheduler attribution, the historical matrix,
and a behavior-preserving causal fix remain incomplete. Cycle 10 exhausts the
user-authorized Sol-high continuation; no further cycle is authorized.
