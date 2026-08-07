# Worker State: CNC-47

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `3`
- Task: `CNC-47 — Repeatable performance baseline`
- Change category: `performance measurement/tooling; bounded behavior-preserving engine instrumentation only if the existing seams cannot provide the required evidence`
- Balance authority: `Frozen. Do not change the adaptive 300-unit global/configured minimum, TotalUnitLimit, costs, HP, damage, armor, speed, production/build timing, prerequisites, probabilities, resources, AI personality policy, or any gameplay tuning. Test-map-only setup may create a repeatable workload but must use ordinary released CNC actor/rule behavior during the measured interval and must be identified as test scaffolding.`
- Status: `First iteration - testing`
- Common base branch/SHA: `agent/cnc-20260807-bug-polish-02-release` / `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`
- Task branch: `agent/round-20260807-cnc47-repeatable-performance-baseline`
- Intended PR base: `agent/cnc-20260807-bug-polish-02-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `17`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260807-bug-polish-03/WORKER-3-CNC-47/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/analysis/worker-3-cnc47`
- Persistent policy scratchpad: `/root/github/LibertyDawn/.agents/references/LIBERTY-DAWN-POLICY-SCRATCHPAD.md` (3,000
  characters maximum; one cross-round serialized writer)
- Policy scratchpad lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/shared-locks`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `26`
- Terra cycle code reviews: `cycles 5, 10, and 15 completed/adopted at analysis/worker-3-cnc47/cycle-review-{05,10,15}/CYCLE-REVIEW.md; all three concerns resolved`
- Sol-xhigh policy escalation: `unused and not applicable unless scope changes into AI/game policy (which this contract forbids)`
- PR: `https://github.com/Realpra1/LibertyDawn/pull/95` (draft)

## Integrated repair assignment

- Phase: `isolated implementation`
- Current release branch/head: `not assigned`
- Integration notes: `not assigned`
- Repair branch: `not assigned`
- Repair PR base: `not assigned`
- Integrated cycles used this RC: `0/3`
- Integrated cycles used total: `0/12`

Before relaunching this worker for combined testing or repair, the integrator must
replace these fields with the exact release head, note path, branch, and counters.
During that phase, the repair branch replaces the original task branch as the
writable branch; the task scope and behavioral contract do not change.

## Why and predicted change

The repository has a capable full-engine headless-MAX runner and per-tick
benchmark CSVs, but no checked-in, one-command late-game workload that measures
the requested **Normal and Fastest** speeds, proves five ordinary AIs each carry
at least 300 live mobile units, summarizes actor load and simulation timing, and
records enough host/configuration identity to compare later branches honestly.
The current parallel runner deliberately rejects every map speed except MAX and
its summary records wall duration and maximum tick, not the requested
real-time/game-time ratio, per-player actor counts, fixed measurement window, or
profiler hotspots. Earlier evidence reached roughly 500 live mobile units across
a five-bot match; it does not prove 300 or more for every AI.

After this task, a developer should be able to run a repository-owned CNC
baseline on a clean checkout and obtain self-describing machine-readable and
human-readable artifacts for matched Normal and Fastest workloads. Each accepted
measurement must prove the map, seed, speed/timestep, five-or-more ordinary bots,
300-live-mobile-per-AI floor, tick window, actor/effect load, benchmark streams,
host state, exit, replay, and absence of fatal/desync signals. The committed
workflow and workload identity—not one favorable result from this host—are the
deliverable used later by CNC-48 and CNC-49.

This is not an optimization mandate. First create and measure the baseline. Make
no gameplay change merely because a profiler shows a hotspot. A very small
behavior-preserving harness, cleanup, measurement, or instrumentation correction
may be made if adversarial evidence proves it is necessary for repeatability.

## Authoritative behavior

- Provide one documented repository-owned command, or one small command sequence,
  that builds/locates CNC and runs a version-controlled workload definition at
  both `Normal` (`Timestep: 40`) and `Fastest` (`Timestep: 20`). The result must
  state the internal speed key and timestep actually accepted by the engine.
  MAX is a separate compatibility/control run and never substitutes for either.
- Use at least five ordinary released CNC AIs with their normal bot traits,
  production, economy, squads, targeting, pathfinding, repairs, support powers,
  and other enabled modules active. A passive bot, manager-only fixture, or Lua
  replacement AI is invalid. The harness may deterministically prepare a
  late-game test workload, including a focused generated/copied map, but it must
  not replace ordinary AI operation during the measured window.
- For primary acceptance on this host, every participating AI must have at least
  **300 simultaneously live, in-world mobile actors** at the beginning of the
  measured interval and at every periodic count sample used for the result.
  Count actors whose definitions carry `MobileInfo` or `AircraftInfo`, matching
  the existing unit-cap classification, but exclude dead actors, actors not in
  the world, queued/pending production, buildings, effects, husks, and neutral or
  spectator ownership. Record live mobile counts separately from queued counts.
  A run that falls below 300 for any required AI is valid diagnostic evidence but
  is not accepted baseline evidence. “Where feasible” authorizes documenting a
  demonstrated environmental impossibility after serious attempts; it never
  authorizes lowering the configured floor or relabeling a smaller run as pass.
- Use a fixed warm-up boundary and a fixed measured world-tick interval, at least
  2,500 world ticks for the accepted primary window. Exclude build/load/map-start
  and warm-up wall time from the primary ratio but report them separately. Define
  and emit `real_game_time_ratio = measured_wall_milliseconds /
  ((end_world_tick - start_world_tick) * accepted_timestep_milliseconds)`.
  Also report measured ticks/second so later consumers do not have to reinterpret
  the ratio.
- At a bounded periodic cadence, record world/local tick, elapsed monotonic wall
  time, total live in-world actor count, total effect count if cheaply available,
  and per-AI live-mobile counts. Record min/median/max per AI across the measured
  window. The observation path must be unsynced/read-only and must not issue
  orders, alter RNG, serialize new gameplay state, or scan/log every actor every
  tick. A modest periodic O(total actors + players) snapshot is acceptable; an
  unbounded hot-path scan is not.
- Preserve raw benchmark CSVs and summarize at least `tick_time`, `tick_actors`,
  `bot_tick`, and every other stream actually emitted: sample count plus
  median/p95/p99/max milliseconds. Identify absent streams explicitly rather than
  synthesizing zeroes. Keep benchmark/profiler overhead out of the golden timing
  result when it is material.
- Produce bounded profiler evidence on the same fixed workload in a separately
  labeled profile-on run. Existing `PerfHistory` CSVs and the opt-in
  `EnableSimulationPerfLogging`/`LongTickThresholdMs` trait/activity diagnostics
  are acceptable if summarized into ranked hotspot counts/cumulative time and the
  raw profile size is bounded. An available sampling profiler may supplement
  this, but a tool absent from the host is not a reason to invent evidence. Never
  compare a heavily instrumented result against an uninstrumented control as a
  claimed speedup.
- Every result manifest must record commit and dirty-state status, task/control
  identity, CNC mod version, workload/map/manifest/settings hashes, seed, roster,
  factions/teams/spawns, speed/timestep, tick bounds, automation/render mode,
  build configuration, OS/kernel/architecture, CPU model/logical count, memory,
  runtime version, requested resource slots/jobs/affinity if any, start/end UTC,
  exit status, and paths/hashes for summaries, logs, replays, saves, benchmarks,
  and profiles. Never overwrite an existing result directory.
- Time golden repetitions serially with `--jobs 1` while holding both game slots,
  with no concurrent large build or unrelated OpenRA process. Run at least three
  valid repetitions per required speed, interleave speed/control order to reduce
  thermal/time bias, report every result, median and spread (including min/max or
  MAD/CV), and invalidate rather than hide contention or setup failures.
- The command must fail nonzero and explain which gate failed for a wrong speed,
  wrong/missing map or bot roster, fewer than five active AIs, any AI below the
  300 live-mobile floor, insufficient tick window, missing/corrupt benchmark,
  missing evidence, timeout/stall, nonzero child exit, fatal/crash/desync signal,
  or leaked child process. A batch aggregate cannot conceal a failed run.
- Keep outputs portable for CNC-48/CNC-49: their runner must be able to point the
  same workload at a different checkout/launcher while preserving the workload
  hash and fixed counts. Host-specific raw results belong in the ignored analysis
  or `AUTONOMOUS-CNC-LOGS/` area; checked-in code/config/docs/tests must remain
  small and deterministic.

## Forbidden behavior and failure signals

- Do not reduce or bypass the 300-unit floor, lower `AdaptiveUnitCapMinimum`, make
  queued/pending units count as live, drop an under-floor AI from the denominator,
  choose a shorter/easier interval after seeing results, or report aggregate
  1,500 units as proof that each of five AIs reached 300.
- Do not change AI policy, unit composition weights, caps, costs, combat values,
  production timing, resources, game-speed timesteps, order latency, pathfinding,
  targeting, RNG/order iteration, or save serialization. Test setup must be
  isolated from ordinary maps and cannot leak rules into normal games.
- Do not use MAX, a save-only run, replay playback, passive fixtures, five idle
  bots, a pre-spawn screenshot, activation logs, or profiler samples without the
  complete player-visible full-engine interval as sole acceptance.
- Do not call a setup “late game” merely because many inert infantry were spawned.
  Accepted evidence must show all ordinary bots active and nontrivial movement,
  production/economy and combat/order activity during the interval. Preserve a
  mixed mobile workload adequate to exercise actors, activities, pathfinding,
  targeting, projectiles/effects, bot ticks, and production; the exact mix is
  test scaffolding, not a new balance recommendation.
- Do not include startup/load time in the measured simulation ratio, compare
  different maps/configs/hashes/seeds/count windows as a matched result, run timed
  golden samples concurrently, or accept a run during detectable CPU/memory/I/O
  contention. Do not publish only the fastest sample.
- Do not enable verbose per-actor/per-tick logging for golden timing. A profiler
  run that creates unbounded output, exhausts memory/disk, or changes throughput
  without being labeled separately is a failure.
- Do not retain orphan OpenRA/Xvfb children after timeout, interruption, invalid
  content, or sibling failure. The repository already records a deferred cleanup
  weakness through the `xvfb-run` wrapper; explicitly check the exact launched
  process groups before and after every adversarial teardown.
- Do not claim deterministic equality between independent AI matches. Require
  reproducible workload/setup identity and valid replay/no-desync health. If an
  optimization is attempted, do not claim improvement from one pair, divergent
  actor counts/actions, or ordinary run-to-run noise.
- A crash/exception/desync, incorrect speed, stalled tick, absent natural/bounded
  exit, missing replay/CSV, malformed summary, negative/zero timing interval,
  actor-count observer changing sync/order behavior, more than 5% sustained
  median overhead from always-on measurement, or behavior mismatch against the
  base control is a failure.

## Relevant current implementation and control behavior

- At the exact base SHA, `launch-ai-parallel.py` runs one to three isolated Linux
  full-engine games from JSON. It copies each map/save, creates a private support
  directory/settings/log/replay/save/benchmark prefix, uses Xvfb, requests
  `Launch.Headless=true`, requires `option gamespeed max`, tracks maximum tick and
  wall duration, and writes per-run plus batch JSON/TSV. It checks headless/MAX/
  roster-start/exit markers, expected artifacts, fatal signals and individual
  child status. Its example is a bounded tick-10,000 Empire Earth4 run with two
  Skynets and three Brutalis. `tests/test_launch_ai_parallel.py` covers manifest
  validation, serial/concurrent scheduling, sibling failure isolation and save
  staging.
- `LaunchArguments.HeadlessValidationError` rejects a map-side headless request
  unless lobby commands select MAX; `Launch.ExitAtTick` also requires headless.
  `Game` suppresses render ticks in explicit headless automation, forces only MAX
  to run unpaced, logs progress every 5,000 ticks, flushes benchmarks on bounded
  or natural exit, and disallows headless remote/replay use. Therefore current
  control cannot produce automated Normal/Fastest acceptance without either a
  minimal explicit local benchmark seam or a carefully bounded graphical/Xvfb
  path. Preserve all local-only/network/replay guards and existing MAX behavior.
- CNC speed config defines Normal as 40 ms/tick, Fastest as 20 ms/tick, and MAX as
  the same 20 ms simulation timestep with `RunAtMaximumSpeed: true`. MAX throughput
  is not a Fastest real/game-time measurement.
- `Benchmark` samples all `PerfHistory.Items` once per local logic tick and writes
  one CSV per stream at finish. Current streams include engine `tick_time`, world
  `tick_actors`, bot `bot_tick`/`bot_attack_response`, Lua, sync, locomotor cache,
  and render streams when relevant. It retains samples in memory until exit and
  has no actor counts, measurement-window metadata, percentile summary, host
  manifest, or stable workload identity.
- Opt-in simulation perf logging times individual trait/activity/effect work and
  writes threshold crossings to `perf.log`; it is off by default and has a 1 ms
  default threshold. It is useful for a bounded diagnostic slice but can distort
  timing and produce excessive output in a 1,500-unit workload.
- Adaptive unit caps are enabled in released CNC AI profiles. The code clamps the
  minimum to the global 300 floor and measures wall/game ratio periodically.
  `UnitBuilderBotModule` classifies `MobileInfo` or `AircraftInfo` actors as mobile
  and its optional debug snapshot counts live plus queued/pending units. That log
  is not sufficient for CNC-47's exact live in-world floor, but its classification
  is the authoritative compatibility definition.
- Empire Earth4 is a repository map with 202x202 dimensions and 36 playable slots;
  it is the current five-bot MAX example and a useful ordinary-control map. Prior
  headless evidence on it reached about 500 live mobile units total, not 300 per
  AI. Do not treat that old artifact as this task's baseline.
- Spec host at 2026-08-07: Linux 6.12.100 amd64, four logical CPUs and 7.8 GiB RAM
  with no swap. No `perf` or `dotnet-trace` executable was found on PATH during
  specification. Re-probe and record the worker/test host; do not hard-code these
  facts or install tooling as part of this task.

## Likely wrong approaches and challenges

- Merely checking in a shell command or the existing MAX example does not measure
  Normal/Fastest, exact live actors, steady-state time, or profiler hotspots.
- Natural production may never place all five opponents above 300 simultaneously,
  because they fight and the adaptive limiter is a floor on enforcement, not a
  promise to build 300. Use deterministic workload preparation if needed, then
  prove ordinary AIs actually operate it. Do not wait through repeated unforced
  games or change production/balance values to manufacture the count.
- Preplacing 1,500 actors without legal cells, mixed work, adequate late-game
  bases/resources, separation/warm-up, or activity proof can benchmark spawn
  congestion instead of a representative late game. Validate exact owners/types/
  cells and record the measured count trajectory.
- Normal is wall-paced at 40 ms/tick and Fastest at 20 ms/tick while the host can
  keep up; ratios may cluster near 1.0 and only rise under load. MAX ticks/second
  answers a different question. Use both fixed-speed ratio and MAX compatibility,
  label them precisely, and do not infer optimization headroom from the pacing
  ceiling alone.
- Wall-clock timing around the process includes map load, shader/content work and
  shutdown. Measure monotonic boundaries inside or from explicit tick markers;
  retain startup/teardown timing separately.
- Periodic actor enumeration, CSV retention, debug logging and profiler output
  can become the performance problem. Keep the observer cadence bounded, compare
  instrumentation-off/on controls, cap/profile only a short slice, and report
  allocation/file-size overhead.
- Independent AI games at the same seed are known not to guarantee bit-identical
  natural outcomes. Use a deterministic workload description and exact hashes,
  repeated distributions, actor-load alignment, replay no-desync, and matched
  base/changed execution; do not demand impossible cross-run replay equality.
- Running serial and concurrent jobs interchangeably corrupts host-level timing.
  The shared resource lock coordinates game count, but golden timing must reserve
  both slots and ensure no separate OpenRA/build remains. If noise remains high,
  investigate host load/thermal throttling and repeat the entire interleaved batch,
  not only the slow/fast outlier.
- Extending general headless semantics carelessly can weaken the existing MAX,
  remote, replay, save-load, input-pumping or exit guarantees. Prefer the narrowest
  explicit local automation surface and test all existing validation paths.
- Broad “fix the top hotspot” refactors are out of scope. Record hotspots for
  CNC-49. Only repair a proven measurement/harness defect or an obviously bounded
  behavior-preserving issue required for valid evidence.

## Competing systems and ownership

- Automation ownership: launch argument validation and benchmark lifecycle belong
  in `OpenRA.Game`/the common load screen; process isolation, workload manifests,
  host capture, aggregation and failure reporting belong in the repository Python
  runner; CNC-only workload setup and metric observation belong in isolated CNC
  test map/rules/script or a cohesive opt-in CNC/common observer. Do not hide
  configuration policy inside result parsing.
- Timing competitors: OS scheduling, CPU frequency/thermal state, GC, Xvfb/renderer,
  audio fallback, file I/O, benchmark sample retention, perf logging, replay/save
  writing, other builds/games, and launcher polling. Record or isolate them.
- Engine workload: actor/activity/trait ticking, effects/projectiles, locomotor and
  path caches, shroud/visibility, targeting/combat, order processing/sync reports,
  world frame-end actions, bot ticks, and rendering if the chosen automation mode
  renders. All relevant ordinary paths must remain enabled and visible in metrics.
- AI workload: every `IBotTick` module configured on the chosen ordinary profiles,
  including base construction, unit production/adaptive cap, economy/harvesters,
  squad formation/ground and air targeting, defense/repair, support powers,
  transport/special-unit work, scouting/exploration, and profile-specific modules.
  The baseline must not selectively disable a slow module. Record the actual bot
  profile roster and show movement, orders, production/economy and combat activity.
- Shared mutable resources: support/settings/content links, copied map/save,
  logs/replay/save/benchmark/profile prefixes, display, local endpoint, result
  directory, process group, CPU/memory/disk and the cross-worker game/build slots.
  All paths must be unique per run even though timed runs are serial.
- The existing `Benchmark` currently mixes data collection/storage/CSV writing and
  may not be the right owner for CNC actor semantics. Keep any change cohesive;
  do not make generic engine measurement depend on CNC actor names or AI profiles.

## Cross-worker dependencies

- CNC-45 and CNC-46 are same-round behavior tasks. CNC-47 has no implementation
  dependency on them and must be developed/measured first from exact common base
  `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`; do not cherry-pick their commits or
  let their behavior/config changes define the baseline. If the coordinator later
  asks for cumulative evidence, preserve the exact base baseline and add a clearly
  labeled second result rather than replacing it.
- CNC-48 is a downstream integration consumer. Hand off the committed command,
  workload/config hashes, metric schema, valid base distribution, host manifest
  and artifact interpretation so cumulative behavior/lag can be compared.
- CNC-49 is a downstream lag-reduction consumer. It must reuse the same workload,
  speed, seed, warm-up/window, 300-per-AI live floor, serial resource isolation and
  summary schema for before/after evidence. Keep the runner able to target another
  checkout/launcher without mutating that checkout.
- The repository records a deferred `launch-ai-parallel.py` orphan-grandchild risk
  through `xvfb-run`. This task may make a narrowly tested process-group cleanup
  repair if the baseline path exercises/proves the defect; otherwise preserve the
  failure check and route the issue to deferred work. Do not broaden into general
  launcher redesign.
- No active task PR branch was available during specification. Before publication,
  compare this scoped diff to the intended PR base and inspect named dependency
  commits only if the coordinator updates this section with an exact branch/PR.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `not applicable — this is performance/tooling with explicitly frozen AI/game policy and balance`
- Sol-high policy review: `not requested`
- Verdict and confidence: `skip; high confidence that no player-facing strategic judgment is authorized`
- Recommendations adopted as testable hypotheses: `none`
- Recommendations rejected or deferred, with reason: `all AI composition, priority, strategy and balance tuning is outside CNC-47; profiler findings route to CNC-49/deferred work`
- Persistent scratchpad update: `none`

## Acceptance and tests

### Literal black-box acceptance

From a clean task checkout, reserve both round game slots and run the documented
baseline command serially. It must create new isolated artifacts for at least
three valid Normal and three valid Fastest repetitions of the same checked-in
workload. Each accepted run must prove the exact base/task commit and clean/dirty
status; identical workload/map/settings hashes; chosen seed, factions, teams,
spawns and at least five ordinary active AIs; actual Normal/40 ms or Fastest/20 ms
engine speed; a post-warm-up measured interval of at least 2,500 world ticks; at
least 300 simultaneously live in-world `MobileInfo`/`AircraftInfo` actors owned by
every required AI at every counted interval sample; nontrivial ordinary movement,
orders, production/economy and combat; total/per-AI actor counts; wall and
simulated durations; computed ratio and ticks/second; benchmark timing summaries;
bounded ranked profiler evidence; replay/exit/artifact success; and no fatal,
exception, stall, orphan or desync signal.

The aggregate summary must include every attempt, invalidation reasons, per-speed
median/spread and host identity. Re-running the same command into a different new
output root must preserve workload identity/schema and independently satisfy the
same gates. A clean reader must be able to tell which data is paced timing, MAX
compatibility, profiled diagnostic, save/load, replay, or adversarial scaling.

### Focused checks and instrumentation

- Before code changes, capture control facts from base SHA: current launcher
  rejects Normal/Fastest headless manifests; existing five-bot MAX example passes
  only its existing gates; list existing benchmark streams and confirm actor-floor
  evidence is absent. This is diagnosis, not acceptance.
- Add focused parser/schema tests for speed/timestep validation, ratio arithmetic,
  warm-up exclusion, actor classification/count samples, five-AI and per-AI-300
  gates, missing/invalid CSVs, percentile edge cases, duplicate/unsafe paths,
  immutable output directories, host/revision/hash capture, nonzero child status,
  fatal/desync detection, and profiler labeling. Use fake launcher artifacts only
  for these interface/error tests; they never replace engine evidence.
- If launch arguments/headless automation change, extend `GameSpeedTest` or a
  focused launch-policy test for local Normal/Fastest acceptance and rejection of
  remote, replay, missing-map/save, invalid speed, non-headless bounded exit, and
  unchanged MAX behavior. Prove normal UI/lobby speed selection is unchanged.
- If engine/mod actor observation is added, test exact live/in-world/owner/mobile
  classification, dead/queued/building/effect exclusion, deterministic ordering,
  cadence/reset boundaries and save/load behavior. Keep observations unsynced and
  outside serialized game state unless persistence is demonstrably required.
- Validate the deterministic workload/generator: exact map package/hash, legal
  cells and owners, at least five active slots, 300 qualifying live actors per AI,
  ordinary bot profiles/modules, and no unintended rules/balance overrides.
- Run Python launcher tests, proportionate C# unit tests, a warnings-as-errors CNC/
  shared-engine build when touched, global CNC rules/sequences YAML validation,
  changed map/package validation, and a scoped diff check against the base SHA.
- Diagnostics must distinguish requested versus accepted speed, setup/warm-up/
  measured/exit state transitions, bot roster, each actor-count gate, benchmark/
  profiler start and flush, timeout/signal, child/process-group cleanup, and final
  pass/failure. Remove noisy per-actor/per-tick diagnostics; retain only bounded
  reusable summaries and actionable warnings.
- Performance expectation for the harness itself: bounded periodic observation,
  bounded output and memory, no new per-actor per-tick allocation/logging, and no
  sustained median `tick_time` or MAX-throughput regression greater than 5%
  versus the measurement-disabled base control at aligned actor counts. Report
  profile file size and peak RSS. Do not optimize gameplay to pass this gate.

### Ordinary and differential games

1. **Cycle-1 full-engine matched compatibility pair after the first product
   change.** Under both game slots, run exact base SHA and changed checkout
   serially on the existing ordinary five-bot Empire Earth4 headless-MAX example,
   same map hash/seed/roster/options/tick bound. Failure hypothesis: the new
   automation/measurement seam changes MAX simulation, bot startup, exit, replay,
   benchmark flush or process cleanup. Failure signal: missing/wrong markers,
   fatal/desync/orphan, count/roster mismatch, absent artifact, or >5% sustained
   median throughput/tick-time overhead after repetition. Pass: both complete the
   same required interval with ordinary active bots and clean artifacts, while the
   changed path adds evidence without a material regression. Immediately move to
   a required speed; do not repeat this smoke as acceptance.
2. **Fastest fixed-load pair.** Run base/current control where possible and changed
   harness with identical workload hash, five AIs, seed and actor floor at actual
   Fastest/20 ms, serially. Hypothesis: headless/local pacing or actor observation
   silently uses MAX, mismeasures wall/game time, or changes bot work. Failure:
   wrong speed marker/timestep, ratio arithmetic mismatch, any AI under 300,
   inactive modules, missing CSV/replay, or behavior/overhead regression. Pass:
   the complete accepted fixed window and self-consistent raw/summary evidence.
3. **Normal fixed-load pair.** Repeat the exact workload at actual Normal/40 ms.
   Hypothesis: the tool hard-codes Fastest/MAX assumptions or includes warm-up.
   Failure: wrong timestep/interval, implausible ratio not explained by raw times,
   count-floor loss, timeout/stall or evidence mismatch. Pass: complete accepted
   window with independently recomputable ratio and all normal modules active.
4. Accumulate at least three valid interleaved repetitions at each speed after the
   latest measurement-affecting fix. Record all runs and distributions; never
   discard a slow valid run. If CV/spread exceeds 10%, diagnose host/load/workload
   variance and rerun a complete interleaved batch before drawing conclusions.
5. Run at least one real ordinary five-AI match at headless MAX to a natural game
   conclusion, with replay/benchmark/no-desync evidence, as compatibility and
   endurance feedback. MAX remains outside Normal/Fastest baseline statistics.

### Old-behavior control and required improvement

The baseline is an observational/tooling deliverable, not an AI strategy. The old
control is exact SHA `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`
in an isolated worktree. Record the exact launcher/build/content/config/map hashes
for every pair. Where base lacks the new Normal/Fastest automation seam, compare
the closest valid explicit-speed run and state the control limitation; never
pretend current MAX is Fastest.

For instrumentation/harness changes, required improvement is reliable collection
of previously absent acceptance evidence with behavior parity and no sustained
material overhead: median MAX throughput and aligned `tick_time` must remain
within 5% of measurement-disabled control after repeated matched runs, absent a
clearly isolated unavoidable cost that is disabled outside benchmark mode. Match
outcome need not improve. If an optional optimization is attempted, require at
least 10% repeated median improvement in the targeted profile/ratio metric at
aligned actor counts, no worse p95/p99 tick behavior, no actor/workload divergence,
and replay/no-desync parity. Otherwise remove/defer it rather than claiming a win.

### Adversarial cases

After normal acceptance first passes and after the latest relevant fix, complete
at least three distinct clean full-engine ordinary-AI adversarial scenarios:

1. **Scale/floor boundary:** five AIs at exactly the 300 live-mobile floor for the
   measured interval, then a distinct supported higher-load workload (preferably
   400 per AI or a sixth 300-unit AI if host/map feasibility permits). Hypothesis:
   count aggregation hides one under-floor AI, memory/CSV storage grows without
   bound, or timing scales nonlinearly/hangs. Failure: any false pass, OOM/stall,
   unbounded output, missing sample, or leaked process. Pass: exact per-AI counts,
   completed fixed interval and an honestly reported scaling curve/resource peak.
2. **Geometry and contention:** same five-by-300 floor with meaningfully different
   open versus congested/chokepoint geometry and active opposing teams so movement,
   pathfinding, targeting, combat, effects and bot managers contend. Hypothesis:
   a benign spread-out setup masks path/activity hotspots or observation becomes
   too expensive during churn. Failure: inactive bots, permanent spawn lock,
   under-floor false pass, tick stall, profiler explosion or fatal/desync. Pass:
   ordinary activity and bounded evidence identify the changed hotspot shape while
   preserving the measurement gate. If a water/island map is used, enable normal
   transport/naval behavior rather than treating unreachable idle armies as load.
3. **Lifecycle/teardown pressure:** force one invalid manifest/content child, one
   timeout or interrupt during a live high-load game, then a clean new run reusing
   no mutable directory. Hypothesis: `xvfb-run` grandchildren survive, sibling or
   later results are contaminated, or partial CSVs pass. Failure: orphan process,
   aggregate pass, overwritten artifact, ambiguous exit, or later port/display/
   settings collision. Pass: exact child failure, complete process cleanup and a
   subsequent independent valid run.
4. **Profiler perturbation:** run the same fixed workload once with golden minimal
   measurement and once with bounded profiler/long-tick logging. Hypothesis: the
   profiler distorts the result or emits unbounded data. Failure: profile artifact
   exceeds the declared bound, changes gameplay/count gates, or gets mixed into
   golden statistics. Pass: separate labels, bounded top-hotspot summary, raw size
   and measured overhead with the golden result untouched.
5. **Save/load and replay:** create a save after setup but before or during the
   measured phase, reload it in an isolated support directory, and prove bots/count
   metrics/timing resume without stale boundaries; separately play the fresh-run
   replay through the recorded final tick and check for OOS/desync/fatal signals.
   Failure: stale warm-up clock/count state, changed roster/speed, incompatible save,
   replay desync or relying on reload as sole acceptance. Pass: clean supplemental
   continuity plus the unchanged fresh-run primary evidence.

For each scenario, record the failure hypothesis, perturbation, exact failure
signal and player-visible pass evidence before launch. If a fix follows failure,
restart the three-clean-adversarial requirement for affected scenarios.

### Final regression

On the final task head and with both game slots reserved, start from a fresh map
(not a reload) and rerun the literal baseline command into a new output root:
three interleaved serial Normal and three serial Fastest repetitions of the exact
versioned five-AI workload, each with the 300-live-mobile-per-AI floor throughout
the measured >=2,500-tick interval, ordinary AI activity, complete ratio/actor/
benchmark artifacts, and clean exit/replay/no-desync/process teardown. Run the
bounded profiler replicate separately, then the existing ordinary five-bot
headless-MAX example to a natural conclusion. Recompute summaries from raw files
and verify hashes/schema. A missing speed, under-floor AI, stale/reused output,
contention, hidden invalid run, or only save-loaded proof fails final regression.

## Implementation rules

- Do not ask implementation or preference questions. Investigate code, history,
  controls, configs, tests, and evidence; choose the strongest safe option and
  record material assumptions. Stop only this task for a real authority,
  credential, missing-file, unsafe-path, or irreducible blocker.
- Keep responsibilities separate and dependencies explicit. Prefer short,
  cohesive classes and functions; split oversized responsibilities when that
  improves cohesion, testability, or hot-path clarity without unrelated churn.
  Preserve unrelated behavior and user changes.
- Prefer the simplest bounded solution supported by evidence. Use fuzzy
  thresholds and game-sensible rules of thumb; do not solve graph theory or add
  exact optimizers, rigid partitions, or elaborate state machinery unless the
  task and adversarial evidence show that simpler priority, count, distance,
  threat-map, or cooldown rules are insufficient.
- Put tunable policy in the owning rules/config/save/map layer and algorithmic
  invariants in code. Do not duplicate policy across AI personalities or hide a
  rules/config concern in test-only code.
- Treat balance as frozen unless `Balance authority` above expressly permits the
  specific surface. Never change cost, HP, damage, armor, speed, timing, power,
  prerequisites, probabilities, resource values, or comparable tuning to make a
  behavior test pass. Unauthorized balance changes invalidate the result because
  they can fake improvement. Record a needed balance change as deferred work.
- For an expressly authorized balance-only task, test its bounded local effect
  first: affected-unit survival, useful damage, exchange value, adaptive rating,
  and selection frequency as relevant. Treat whole-match outcome/composition as
  secondary regression evidence unless the task explicitly makes it primary.
- Add proportionate unit/interface/static tests. Add useful bounded debug logging
  and handled warnings/errors at the owning boundary: make failures actionable,
  never silently swallow exceptions or substitute success, avoid per-tick spam,
  and remove obsolete/noisy temporary instrumentation before publication.
- Keep deterministic simulation hot paths bounded. Avoid repeated full-map/unit
  scans, uncontrolled allocations, nondeterministic iteration/order, unbounded
  retry queues, or logging that materially reduces MAX throughput. Measure or
  explain performance-sensitive changes with current evidence.
- Inventory and test ordinary modules that compete for the same units, queues,
  cash, reservations, targets, repair, or retargeting.
- Record worthwhile out-of-scope fixes, refactors, and optimizations under
  `Deferred work` in the task report/handoff; never expand scope silently or make
  concurrent workers edit a shared deferred-work file.
- Keep raw logs/replays/saves/profiles outside Git or under ignored
  `AUTONOMOUS-CNC-LOGS/`. Record concise paths, seeds, and conclusions here or in
  the task report.
- Never push directly to `bleed`, merge a GitHub PR, or edit the task sheet or
  coordinator state. Update this state and task report on the recorded task branch
  or, during integrated repair, the recorded repair branch.

## Evidence-driven loop

One cycle begins when a product-code/config change is made. A cycle may build,
run focused checks, and execute up to two materially useful games needed to judge
that change. Merely reading logs or correcting an invalid harness without a
product change does not begin another cycle; record it honestly.

Treat full-engine simulations with ordinary AI as cheap primary feedback. The
first behavioral test after the first implementation change must be a full-engine
ordinary-AI game, normally headless MAX, with every relevant normal module enabled
from test 1. A focused custom map, pre-spawned actors, short distance, or obvious
cheese setup may make the event immediate, but it must not replace the real engine
or ordinary AI with a passive/custom bot or isolated manager fixture. Run focused
unit/static checks as useful baseline gates before or alongside it; do not delay
game evidence while accumulating unit-only confidence. Keep available game slots
working while other agents code or analyze because simulation is cheaper than
missing human feedback.

For this performance task, golden timing is the exception to general parallel
utilization: reserve both game slots and run it serially. Other non-timed
functional games may use available slots when their evidence is not contaminated.

When a required situation is rare, construct it deliberately in a full-engine
custom map while keeping ordinary AIs and every relevant normal module enabled.
For example, pre-place a damaged or healthy capturable building and enough
engineers to force the one-versus-two-engineer decision. Use the setup for direct
causal proof, then seek natural-match evidence when the event is reasonably
reachable. If natural occurrence depends on unfinished prerequisite behavior
(such as an APC/transport delivery task), record that dependency and required
future revalidation instead of wasting cycles waiting for an event the current
build seldom creates or treating its absence as failure of this task.

For every change to AI strategy, priorities, economy, production, targeting,
recovery, or tactics, compare against old behavior repeatedly throughout the loop.
This contract forbids such changes; if one appears in the diff, remove it or stop
for renewed authority. For allowed tooling/instrumentation, use the exact base SHA
control and the parity/overhead criteria above.

Treat all tests as attempts to break the implementation. Compilation, lint, and
static analysis are baseline gates; every unit, integration, save/load, replay, or
game test must exercise a regression risk, boundary, invalidation, contention,
failure/recovery path, or assumption under pressure. Before running it, record:

- Failure hypothesis: what plausible defect this test could expose.
- Perturbation: what is made harder or different from the last passing test.
- Failure signal: the exact log/state/player-visible outcome that proves breakage.
- Pass evidence: the final observable result needed to falsify the hypothesis.

The existing broad regression suite counts as an adversarial gate against breaking
unrelated behavior, but it does not replace targeted falsification of this task.

One initial full-engine cheese-in-front-of-the-mouse smoke setup may establish
that the harness and simplest behavior work. As soon as it passes, change at least
one meaningful dimension—timing, map geometry, resources, missing/destroyed assets,
unit count, pressure, competing orders, save/load boundary, or match duration—and
make every later test harder or materially different. Never spend cycles on
near-identical happy-path confirmations when a stronger falsification is possible.

For each cycle:

1. Reread this state, current diff, and previous evidence.
2. Implement or revise the smallest evidence-driven change.
3. Run focused unit/static checks and fix relevant errors or warnings without
   treating them as a substitute for the game.
4. From cycle 1, run the simplest not-yet-proven full-engine ordinary-AI
   adversarial scenario that can falsify the current implementation while proving
   the requested outcome if it survives. Cycle 1 uses the matched MAX compatibility
   pair; the next successful cycle must exercise Fastest or Normal.
5. Diagnose results against desired and forbidden behavior. Add bounded
   instrumentation when evidence cannot distinguish requested/accepted speed,
   setup/warm-up/window state, bot roster, count gate, benchmark/profile flush,
   process cleanup, and final outcome.
6. Remove or reduce obsolete/noisy diagnostics after they answer the question.
7. Update the cycle journal before making another code change.

## Interim code-review loop

After product-change cycles 5, 10, 15, and 20 that occur, and before the next
product change or publication, launch a fresh Terra 5.6 medium
`cycle-reviewer`. Give it a job declaring `cycle` mode and only this state path,
the recorded base SHA, current branch/head and cumulative scoped diff, relevant
evidence through that cycle, and a task-local output path such as
`/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/analysis/worker-3-cnc47/cycle-review-05/CYCLE-REVIEW.md`.

The reviewer writes only its review artifact and returns at most one
`advisory_concern`. Read it, verify its evidence, and record whether it is adopted
or rejected and why. An adopted product change begins the next ordinary cycle;
the review grants no extra cycles. At cycle 20, either reject the concern with
evidence or hand off `First iteration - testing` if resolving it would require
cycle 21. A clear review does not replace adversarial games, Commenter review,
CI, or the final Sol-high task-PR review and one-response gate.

## Match narrative and policy-feedback loop

After every materially judged full-engine match or paired control batch:

1. Increment `Full-engine game tests completed` for each game, including an
   invalid setup that still ran far enough to expose evidence; label invalid runs.
2. Copy (do not symlink) only the authorized current/control logs, manifests,
   summaries, benchmark/profile samples and metrics into the role output
   directory's `inputs/` subtree. Write a strict JSON Commenter job containing
   only absolute `artifacts` paths, optional `design_reference`, and an absolute
   `output` path ending in `NARRATIVE.md`. Launch a no-history fresh `commenter`
   role (Terra 5.6 medium). Do not stage source, this state, the task sheet,
   implementation notes, or inline job-file commentary.
3. Read its factual `NARRATIVE.md`. Verify cited artifacts/ticks/counts/timings and
   use it to identify setup, pacing, activity, floor, outcome and cleanup facts.
   Correct input/evidence rather than editing the narrative into a preferred story.
4. Policy Reviewer is not used because CNC-47 freezes AI/game policy. If evidence
   raises a strategic or balance question, record it as deferred work for another
   authorized task; do not ask a reviewer to expand this task.

Detailed narratives stay under the ignored analysis directory. Preserve paths
plus concise factual conclusions in the cycle journal and task report. A paired
two-game batch may share one Commenter.

Prefer the full engine and real bot types. On Linux use explicit headless
automation when graphics/input are irrelevant, but prove the accepted Normal or
Fastest speed rather than assuming headless means MAX. Prove the current run
loaded the intended map, bots, actors, options, advanced ticks, flushed logs,
replay/benchmark evidence, and produced the final bounded/natural outcome. A
passive fixture or manager-only simulation is not sole proof.

Use ordinary full matches for emergent AI behavior and focused generated/copied
maps for the fixed-load measurements. After normal acceptance first passes,
require at least three distinct clean adversarial scenarios after the latest
relevant fix. Keep the final regression fresh-map and literal.

Wrap shared resources with:

```text
python3 .agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py \
  --lock-dir /root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/locks \
  --resource game --capacity 2 --slots 2 -- COMMAND...
```

Golden performance runs always reserve both slots and use one game process. For a
non-timed two-game diagnostic batch, reserve two slots and use isolated support
directories, settings, logs, replay/save/benchmark/profile prefixes, map artifacts,
ports and displays. Poll background games within 60 seconds, normally cap them at
30 minutes, and judge each run separately. A required full match may exceed 30
minutes while it continues making useful progress; stop it when evidence is
sufficient or progress stalls.

For expensive setup, optionally save shortly before the measured phase and reload
after a tooling change. Record the save's commit, config, seed and tick; reject an
incompatible or stale save. Never use reload as sole acceptance, adversarial, or
final-regression evidence because it may retain stale initialization or AI state.
Confirm the result from a fresh match.

After 20 unsuccessful code-change cycles, publish the safest useful result as
`First iteration - testing`. Do not pad cycle counts after evidence is sufficient.

When the phase is integrated testing, the isolated 20-cycle cap no longer blocks
the assigned release validation. Use at most three code-change cycles for the
current RC and at most twelve across four RCs, updating both integrated counters.
Test the exact recorded release head before changing code; put any change only on
the recorded repair branch and rerun the materially affected original acceptance,
adversarial, and combined scenarios.

## Completion and publication

Propose `Complete - testing` only after literal acceptance, all required clean
adversarial cases, final regression, task checks, report, PR, and required GitHub
checks pass. Otherwise propose `First iteration - testing` with exact failures and
risks. The reviewer and integrated release determine final status.

The task report must cover behavior, design choices, assumptions, cycle count,
tests, seeds/artifact paths, diagnostics removed or retained, host/resource
isolation, raw and summarized Normal/Fastest ratios, per-AI actor-floor evidence,
simulation/profiler hotspots, workload/config hashes, performance overhead and
determinism/replay health, old-control configuration and comparative results,
PR/checks, downstream CNC-48/CNC-49 invocation, deferred work, and remaining risks.

Push the task branch and open one individual PR. Do not merge it. Wait for every
required GitHub check; diagnose and fix relevant failures within the isolated
cycle budget and rerun them. If required checks cannot become green, propose
`First iteration - testing` rather than completion.

When review returns a correction, perform at most one review-response code/test
cycle, applying the highest-impact safe finding you agree with or recording
evidence for rejection. This cycle counts within the 20 isolated cycles; never
silently exceed the budget.

## Cycle journal

| Cycle | Commit/change | Failure hypothesis and perturbation | Checks/games | Narrative/cycle-code review | Failure/pass evidence | Decision/next harder test |
|---|---|---|---|---|---|---|
| 1 | Uncommitted initial paced-headless seam, map-scoped fixed workload/observer, benchmark parser, and baseline orchestrator | The new automation/measurement path could change existing MAX startup, bot roster, bounded exit, replay/save/benchmark flush, or throughput. Exact-base and changed Release builds ran the same three seeded five-bot Empire Earth4 MAX manifests serially to tick 10,000. | Python launcher suite 4/4; focused LaunchArguments tests; Common/engine Release builds warning-free; base 3/3 and changed 3/3 full-engine runs passed. | `analysis/worker-3-cnc47/cycle-01-max-pair/commenter/NARRATIVE.md`; no cycle review due until cycle 5. | Both sides: five ordinary bots, MAX/20 ms accepted, tick 10,000, exit 0, replay/CSV and scheduled save present, no fatal/desync, no launched descendants. Batch throughput 79.209 base vs 91.585 changed ticks/s; median-of-run `tick_time` 5.696451 vs 5.778586 ms (+1.442%). An unrelated worker game visible afterward makes this non-golden overhead evidence. | Existing MAX behavior is compatible. Next cycle must harden schema/identity and falsify actual Fastest or Normal pacing with the fixed five-by-300 workload. |
| 2 | Truthful incomplete-window summaries, warm-up duration/progress evidence, graceful orchestrator interrupt, exact median tests, and v2 separated-team geometry | A paced run could hide progress, invent a configured end tick after interruption, or lose one AI below 300 despite geographically separated teams. Perturbation: actual Fastest/20 ms with five ordinary bots and 340 mobiles each, north-versus-south teams. | Python launcher suite 7/7; Common build 0 warnings/errors; one full-engine Fastest run intentionally interrupted after the floor failed. | `analysis/worker-3-cnc47/cycle-02-fastest/commenter/NARRATIVE.md`; no cycle review due. | Speed/timestep accepted and progress observed through tick 1,100. Multi3 fell 325@900 -> 294@1000 -> 275@1100 while all bots moved/worked and combat/effects occurred. Commenter also proved fractional `warmup_elapsed_ms` caused a parser rejection. Signal cleanup terminated the remaining launched descendant. | Geometry alone is insufficient. Next cycle increases only the deterministic test-map reserve, retains the immutable 300 acceptance floor, fixes fractional warm-up parsing, and reruns Fastest through the complete 2,500-tick window. |
| 3 | Fractional warm-up parser repair and equal 600-mobile test-map reserve per bot | The observed v2 loss curve projected below 300 before tick 3,000; perturbation raised only pre-measurement scaffolding from 340 to 600 while preserving the same units, ordinary AI, rules, seed, speed, geometry, and exact 300 gate. | Python launcher suite 7/7; one full-engine Fastest run intentionally interrupted at the first floor failure. | `analysis/worker-3-cnc47/cycle-03-fastest/commenter/NARRATIVE.md`; no cycle review due. | Parser correctly retained decimal warm-up duration and truthfully summarized ticks 500-2,000. All bots had movement, orders, spending/earnings and combat; Multi3 fell from 600 to 308@1900 then 292@2000. Peak observed effects 755; exact process group cleanup succeeded. | A larger unequal-army reserve is inefficient. Next cycle balances the authorized workload at six ordinary bots, 3v3, still 600 each and floor 300, then seeks a complete Fastest interval. |
| 4 | Six-bot 3v3 workload and exact six-bot roster gate | The 3v2 workload focused attrition on one side; perturbation added a sixth ordinary SkyNet and balanced three north versus three south spawns without changing actors, AI profiles, rules, seed, speed, reserve or floor. | Python launcher suite 7/7; one full-engine Fastest run intentionally interrupted after a player army disappeared. | `analysis/worker-3-cnc47/cycle-04-fastest/commenter/NARRATIVE.md`; no cycle review due. | Six bots and 3,600 setup mobiles reached tick 1,700 with sustained movement/economy/combat. Multi5 declined from 600@500 to 427@1300 and then 0@1400, remaining inert through 1700; the artifacts are defeat-consistent but contain no explicit defeat line. Exact process-group cleanup succeeded. | Balanced teams alone do not retain an army after base loss. Next cycle uses the ordinary released `shortgame false` lobby option plus a 700-unit reserve, preserving all-unit victory semantics and the exact 300 gate. |
| 5 | Ordinary `shortgame=False` map option and equal 700-mobile test-map reserve per bot | Base destruction appeared to dispose Multi5 despite a remaining army; perturbation disabled short-game victory and raised only pre-measurement scaffolding from 600 to 700, preserving six released bots, 3v3 geometry, actor mix, rules, seed, Fastest speed and the immutable 300 gate. | Python launcher suite 7/7; one invalid capitalization probe interrupted at tick 500 and one corrected full-engine Fastest run intentionally interrupted after the first floor breach was established. | `analysis/worker-3-cnc47/cycle-05-fastest/invalid-false-commenter/NARRATIVE.md`; `analysis/worker-3-cnc47/cycle-05-fastest/commenter/NARRATIVE.md`; `analysis/worker-3-cnc47/cycle-review-05/CYCLE-REVIEW.md`. The reviewer concern is adopted: engine evidence proves the corrected run used `shortgame=False`, but non-speed effective lobby identity is not yet summarized or gated. | Lowercase `false` was parsed as the default `shortgame=True`, so that probe was rejected. Correct `False` was accepted and all six armies persisted through tick 2,600. Multi5 reached exactly 300@2200, then 262@2300, 244@2400, 221@2500 and 204@2600; the other five remained at or above 300. The incomplete run correctly withheld ratio/benchmark acceptance, preserved a replay/no-desync record, and exact process-group cleanup succeeded. | Player disposal is fixed, but combat attrition still invalidates the window. Cycle 6 must first record/reject effective lobby-setting divergence, and can combine that gate with the next safer workload geometry falsification. |
| 6 | Declarative/effective lobby-identity gate and deeper bottom-row 3v3 geometry | A workload hash could describe requested options that the engine did not accept, while the y=155 bottom row still permitted focused attrition before tick 3,000. Perturbation generates lobby commands and the expected cash/short-game/bot/faction/team/spawn identity from one declaration, rejects engine divergence, and moves only team 2 to spawns 23/25/27; reserve, actor mix, ordinary AI, rules, seed, speed and floor are unchanged. | Python launcher suite 8/8; warning-as-error shared/CNC build and interface checks passed with 0 warnings/errors; one full-engine Fastest run intentionally interrupted at the first floor breach. | `analysis/worker-3-cnc47/cycle-06-fastest/commenter/NARRATIVE.md`; next cycle review due at cycle 10. | Requested/effective Fastest/20 ms, `shortgame=False`, cash 20,000 and every bot/faction/team/spawn matched and were recorded in the summary. All bots moved/worked, with 773 effects at tick 800. Multi5 fell 467@500 to 358@700 and 281@800; Multi1 was already 397@800, while the other four remained 639-700. Incomplete acceptance and exact cleanup were correct. | Identity validation is proven. Greater ground separation did not control early losses; debug artifacts are dominated by aircraft/husk combat and the 1-in-7 setup aircraft mix is not a representative reserve. Next cycle reduces only preplaced aircraft density while retaining a bounded mixed mobile workload and all ordinary AI modules. |
| 7 | Representative fixed setup mix with one aircraft per 25 preplaced mobiles | The prior 1-in-7 aircraft setup created 100 aircraft per bot and drove hundreds of aircraft/husk deaths during warm-up despite deeper ground geometry. Perturbation retains the same released ground/aircraft types and ordinary AI behavior but lowers only deterministic test scaffolding to 28 aircraft plus 672 mixed ground mobiles per bot; roster, rules, seed, speed, geometry, reserve and floor are unchanged. | Python suite 8/8; warning-as-error shared/CNC build and interface checks 0 warnings/errors; CNC YAML validation passed; one invalid-YAML full-engine attempt and one corrected Fastest run interrupted at the first floor breach. | `analysis/worker-3-cnc47/cycle-07-fastest/invalid-yaml-commenter/NARRATIVE.md`; `analysis/worker-3-cnc47/cycle-07-fastest/commenter/NARRATIVE.md`; next cycle review due at cycle 10. | The first generated map misindented three trait fields; the runner rejected its missing identity/measurement despite clean tick-3000 exit and benchmark flush, then corrected indentation was verified. In the valid workload, all bots moved, spent/earned and fought; Multi5 fell 506@500 to 315@1000 and 283@1100, while Multi1 remained 317 and the other four 624-699. Exact cleanup succeeded. | Aircraft density was not the root cause. Engine/map inspection proved spawn numbers follow actor-definition order: spawn 27 is `(109,36)`, about 34 cells from enemy spawn 3 `(84,13)`, rather than the assumed bottom edge. Spawns 23/24/25 are the contiguous bottom row `(58,185)/(96,185)/(135,185)`. Next cycle changes only Multi5 spawn 27 to 24. |
| 8 | Corrected contiguous bottom-row team geometry | Cycle 7 unintentionally placed Multi5 only about 34 cells from enemy Multi1 because map-definition order makes spawn 27 `(109,36)`. Perturbation changes only Multi5 from spawn 27 to actual bottom-row spawn 24 `(96,185)`, yielding team-2 spawns 23/24/25 while preserving identity gates, actor mix, ordinary bots, rules, seed, speed, reserve, warm-up/window and floor. | Python suite 8/8; one full-engine Fastest run under both slots intentionally interrupted after sustained progress loss. | `analysis/worker-3-cnc47/cycle-08-fastest/commenter/NARRATIVE.md`; next cycle review due at cycle 10. | Exact effective spawns 23/24/25 produced the first clean boundary: all six had 700@500 with zero deaths. At tick 1500 all remained 623-673 with movement, orders, spending, kills/deaths and 481 effects. After the tick-1500 sample, debug events advanced only to tick 1572 and no further sample appeared before interruption at 1,127.1 s; one task process used ~100% of one CPU and no competing OpenRA process existed. Exact cleanup succeeded. | Correct geometry solves focused attrition, but 4,200 surviving mobiles become operationally impractical on this four-core host. Cycle 9 lowers only the deterministic reserve to 550 per bot (3,300 total), still 250 above the immutable per-bot floor; Cycle-8 loss trajectories project sufficient retention while reducing actor/pathfinding load by 21%. |
| 9 | Equal 550-mobile per-bot setup on verified 23/24/25 geometry | Cycle 8 preserved a 623 minimum but lost repeatable progress with 4,200 surviving mobiles. Perturbation lowers only pre-measurement scaffolding to 550 per bot (3,300 total, still 250 above the exact 300 gate), retaining six released bots, the same mixed types/aircraft interval, rules, seed, geometry, speed, warm-up/window and all ordinary AI work. | Python suite 8/8; one full-engine Fastest run under both slots intentionally interrupted after sustained progress loss. | `analysis/worker-3-cnc47/cycle-09-fastest/commenter/NARRATIVE.md`; next cycle review due at cycle 10. | Setup reached tick 500 in 165 s; all six stayed above floor through tick 1300, ending 470-526 with substantial movement/busy work, spending, kills/deaths and 599 effects. No later sample or completion marker appeared before interruption at 668.577 s; the artifacts prove incompletion but not the stall cause. Exact cleanup succeeded. | Total load reduction alone did not remove the central mass-collision shape. Map definition order also proves wide bottom spawns 34 `(15,182)`, 24 `(96,185)`, and 35 `(176,183)` mirror top spawns 1/3/5 across three lanes. Cycle 10 spreads the unchanged 550-unit teams across those lanes to test whether distributed contention completes without weakening activity or floor gates. |
| 10 | Wide mirrored three-lane team geometry at unchanged 550 reserve | The clustered bottom row funneled 3,300 mobiles into a central progress bottleneck. Perturbation changes only team-2 endpoints from spawns 23/25 to 34/35, yielding bottom `(15,182)/(96,185)/(176,183)` against top `(14,9)/(84,13)/(154,12)` while retaining all identity gates, actors, ordinary bots, rules, seed, speed, reserve, warm-up/window and floor. | Python suite 8/8; one full-engine Fastest run under both slots intentionally interrupted after sustained progress loss. | `analysis/worker-3-cnc47/cycle-10-fastest/commenter/NARRATIVE.md`; `analysis/worker-3-cnc47/cycle-review-10/CYCLE-REVIEW.md`. The concern is adopted: current sampling repeatedly allocates/filter-scans actors and re-enumerates queues per bot, with no observer-disabled control. | Wide lanes passed the prior tick-1300 bottleneck and sampled regularly through tick 1500. All bots remained active with 436-521 mobiles, 375-510 moving, 383-512 busy, 1-2 queued, combat/economy and up to 620 effects. Later log activity reached tick 1572 but no tick-1600 sample appeared before interruption at 855.793 s. Exact cleanup succeeded. | Before any further workload reshaping, Cycle 11 will make snapshots single-pass/allocation-reusing and compare otherwise matched bounded observer-on/off Fastest diagnostics; neither partial diagnostic is golden acceptance. |
| 11 | Single-pass allocation-reusing observer plus explicit measurement toggle | Repeated per-bot actor/queue enumeration and per-sample arrays/dictionaries could dominate the apparent tick-boundary collapse. Perturbation performs one live-actor pass, one queue pass, cached mobile-type classification, reusable position dictionaries and per-bot counters; a map-only toggle disables only sampling for an otherwise matched control. Workload, AI, actors, seed, geometry, speed and gameplay are unchanged. | Python suite 8/8; warning-as-error build and interface checks 0 warnings/errors; observer-on Fastest run and one observer-off retry started under both slots; an earlier off attempt failed before game startup with a transient framebuffer error and is not counted as a game. | `analysis/worker-3-cnc47/cycle-11-observer-pair/commenter/NARRATIVE.md`; next cycle review due at cycle 15. | The enabled run sampled every 100 ticks through 1500, retained 448-514 mobiles per bot at tick 1500, then produced no later sample before interruption at 847.792 s. The disabled retry proved matching setup/identity and activity beyond tick 751 but emitted no independent later tick marker and was interrupted earlier at 407.378 s. Both cleaned up exactly; neither reached tick 1700 or flushed benchmarks. | The single-pass observer is retained as a bounded improvement, but the requested overhead/root-cause comparison is inconclusive because the off run had neither an aligned exit nor a final-tick trace. Cycle 12 adds only a cheap disabled-control heartbeat and runs both variants to a common tick-1500 bound so each flushes aligned benchmarks; it does not reshape gameplay. |
| 12 | Bounded 100-tick disabled-control heartbeat | The Cycle-11 control could have completed or stalled without an observable final tick. Perturbation adds one allocation-free debug heartbeat at the existing cadence only when full actor/queue measurements are disabled, then runs otherwise identical off/on Fastest variants toward a common tick-1500 bound. | Python suite 8/8; warning-as-error build and interface checks 0 warnings/errors; two full-engine runs under both game slots. | `analysis/worker-3-cnc47/cycle-12-observer-aligned/commenter/NARRATIVE.md`; next cycle review due at cycle 15. | Both variants accepted the same Fastest/20 ms lobby identity and emitted exact markers through tick 1400, then saturated one core without reaching 1500. Off timed out at 1,200.082 s; on was interrupted after the shared stall was established at 620.496 s. The on run remained active and above floor through tick 1400 (474-509 mobiles, 2,684 moving, 2,726 busy, 480 effects). Neither flushed accepted benchmarks, so numerical overhead remains unavailable, but measurement overhead alone cannot explain the shared boundary failure. Exact process-tree cleanup succeeded. | The optimized observer and cheap control heartbeat are retained. Further observer tuning is not justified. Cycle 13 must capture bounded simulation hotspot evidence on the same workload and harden the runner defect observed here: a measurement CSV must never satisfy the engine-benchmark presence gate. |
| 13 | Explicit bounded simulation-profile schema/ranking and strict engine-benchmark identity | A partial measurement CSV could falsely satisfy the generic CSV benchmark gate, while the shared tick-1400 collapse lacked causal hotspot evidence. Perturbation accepts a separately labeled simulation-perf profile with runner-owned threshold/byte/top-N settings, aggregates threshold events by count/total/max, and recognizes only run-prefixed engine benchmark streams. | Python compile; launcher suite 10/10; one full-engine profile-on Fastest diagnostic under both slots. | `analysis/worker-3-cnc47/cycle-13-profile/commenter/NARRATIVE.md`; next cycle review due at cycle 15. | The shortened profile diagnostic exited cleanly at tick 1400 in 550.509 s, flushed 11 benchmark streams and replay, kept all six bots active and at 478-521 mobiles, and produced a 12,159-byte profile under the declared 16 MiB cap. Of 126 >=100 ms events, `Trait: ModularBot` accounted for 121/77,267 ms (max 1,521 ms); movement activities accounted for the other five. `tick_time` median/p95/p99/max was 249.740/1,410.879/2,314.680/4,191.323 ms. No fatal/desync or descendant leak. | The evidence attributes the operational limit primarily to ordinary bot work and is routed to CNC-49; no gameplay optimization is authorized here. The profile is intentionally not golden timing and not a 2,500-tick acceptance result. Cycle 14 makes every future output self-describing (revision/dirty/build/runtime/manifest/settings/artifact hashes) and documents the portable command before one final feasible workload attempt. |
| 14 | Self-describing orchestrator output, portable golden/profile commands, and checked-in operator documentation | A failed or interrupted command could lose provenance, confuse profile output with golden timing, or omit settings/artifact hashes. Perturbation captures revision/dirty paths, task/result kind, build/runtime/host/affinity/load, fixed workload identity, copied inputs, hashes and a per-run artifact inventory; `--profile-only` remains separate from interleaved golden distributions. | Python compile; launcher/metadata suites 12/12; JSON/diff checks; one real orchestrated full-window profile attempt under both slots with an explicit 600 s cap. | `analysis/worker-3-cnc47/cycle-14-orchestrator-profile/commenter/NARRATIVE.md`; next cycle review due after cycle 15. | The command honestly returned nonzero at tick 1400/600.506 s with timeout, incomplete 500-1400 versus 500-3000 window, null ratio/ticks-per-second, and missing benchmark streams. It retained active six-bot floor evidence (minima 449-539), a bounded 11,865-byte profile (118 `ModularBot` events/94,232 ms, max 17,894 ms), replay/no-desync, exact cleanup, commit/dirty/build/runtime/host/timestamps, copied manifest/config/rules/map and a 16-file SHA-256 inventory including settings. | The one-command failure and provenance paths are proven. Cycle 15 makes the last workload-only feasibility perturbation: 500 rather than 550 setup mobiles per bot, preserving six bots, all actors/modules, wide mirrored geometry, fixed 300 floor and full 2,500-tick window. Review is required after the cycle before publication or another change. |
| 15 | Six-bot setup reduced from 550 to 500 mobiles per bot | The 550 setup remained safely above floor at tick 1400 but became operationally impractical; perturbation removes 300 total test-scaffold actors while preserving the exact six bots, released mixed units/modules, wide geometry, seed, Fastest/20 ms, floor and 500-3000 window. | Launcher/metadata suites 12/12; JSON/diff checks; one full-engine Fastest attempt under both slots. | `analysis/worker-3-cnc47/cycle-15-fastest-500/commenter/NARRATIVE.md`; `analysis/worker-3-cnc47/cycle-review-15/CYCLE-REVIEW.md`. Reviewer concern adopted. | Runtime proved 500 created for each bot and the exact accepted lobby/speed, but after 424.488 s the only direct tick logs remained tick 1 and the measurement CSV was header-only. The interrupted result correctly reported no exact final tick, no exit/benchmark/window/ratio evidence, and exact process cleanup. It neither supports nor falsifies full-window feasibility; it provides materially weaker evidence than the 550 profile that cleanly reached tick 1400. | Restore 550 because it is the only workload with repeated sampled floor/activity trajectories and a clean bounded profile/benchmark flush. Cycle 16 is the single review-response correction and will run one fresh bounded tick-1400 Fastest regression before First-iteration publication. |
| 16 | Review-response correction restoring the six-bot 550-mobile workload | The Cycle-15 reviewer found no sampled evidence for the 500 setup and required restoring the only repeatedly evidenced workload. Perturbation restores only setup count/identity to 550, preserving released actors/modules, wide geometry, seed, Fastest/20 ms, warm-up/window declaration and immutable 300 floor. A first launch was deliberately interrupted at tick 0 after an unrelated OpenRA process was detected and is excluded as contention-invalid, then the clean retry reserved both slots after that process exited. | Launcher/metadata suites 12/12; JSON/compile/diff checks; warning-as-error Debug build and interface checks 0 warnings/errors; Release build plus all CNC MiniYAML/default-sequence/map validation passed; focused LaunchArguments 14/14; one clean full-engine Fastest bounded retry under both slots. | `analysis/worker-3-cnc47/cycle-16-final-bounded/commenter/NARRATIVE.md`; Cycle-15 review concern adopted; no cycle-20 review due. | The clean retry accepted Fastest/20 ms, exact six-bot lobby and 550 setup, and exited 0 at tick 1400 in 575.464 s with replay, 11 benchmark streams, no fatal/desync, and exact cleanup. Ten samples at ticks 500-1400 kept every bot above floor (per-bot minima 447-522; total live actors 3,566-3,840), with movement/busy work and kills/deaths for all six. `tick_time` median/p95/p99/max was 253.214/1,398.204/3,194.818/5,472.596 ms. | The reviewer correction is proven at the strongest feasible bounded horizon. It is useful regression evidence but only 900 post-warm-up ticks, not the required 2,500-tick window or Normal/Fastest repetitions. Stop product changes and publish honestly as `First iteration - testing`. |
| 17 | Final Sol-high review response: cross-checkout measured-target provenance | The task-PR reviewer proved `--launcher` hashed the alternate script while attributing the harness checkout's revision/version/build label to the measured engine. Perturbation requires the standard owning checkout `launch-game.sh`, resolves and gates its Git/CNC identity, records measured and workload-source revisions separately, hashes the actual `bin/OpenRA.dll`, and relabels build configuration as requested rather than verified. No engine/gameplay path changes. | Focused provenance suite 3/3; combined Python launcher/orchestrator suites 13/13; Python compile/diff checks; direct current-checkout identity/hash probe. No game added because the correction executes and can fail before any child launch; the two-repository test directly falsifies the defect. | `analysis/worker-3-cnc47/task-pr-review/TASK-PR-REVIEW.md`; sole `review_response` adopted. No second response cycle is permitted or needed. | A test creates distinct workload and measured Git repositories, leaves only the target dirty, and proves the returned source commit stays clean/distinct while the measured commit/dirty state/CNC version and engine hash come from the target. Unowned/nonstandard/missing-runtime launchers now fail before output/game creation instead of substituting harness identity. | Commit/push the sole response, wait for both required checks again, refresh receipt/report, then hand off `First iteration - testing`. The review's non-blocking finite-clock hardening remains deferred until a Complete iteration. |

## Handoff receipt

- Proposed status: `First iteration - testing`
- Final branch/head: `agent/round-20260807-cnc47-repeatable-performance-baseline` / product-and-review-response head `e7eb19df239b249e0a99aa485f420d29132d4d4c`; the following state-only receipt commit does not change product code or evidence
- PR and checks: `https://github.com/Realpra1/LibertyDawn/pull/95` (draft) targets `agent/cnc-20260807-bug-polish-02-release`; required Linux and Windows checks passed on the review-response head in 2m14s and 3m30s, respectively; receipt-tip checks must also be green before worker exit
- Cycles used: `17/20`; Cycle 17 is the sole final-review response
- Acceptance evidence: `literal acceptance not achieved or claimed`; clean Cycle-16 Fastest/20 ms bounded run reached tick 1400 in 575.464 s, kept all six ordinary bots above the 300 floor at every tick-500..1400 sample (per-bot minima 447-522), flushed replay plus 11 benchmark streams, and exited without fatal/desync/descendant leak; no complete 2,500-tick Normal/Fastest window or golden ratio/distribution exists
- Adversarial evidence: `26 materially judged games` cover exact-base/current MAX compatibility, actual Fastest pacing, per-player floor failure, short-game lobby divergence, roster/geometry/scale/aircraft-mix pressure, observer on/off, profile perturbation, timeout/interruption/invalid content, exact cleanup, and a clean post-contention retry; clean three-case post-acceptance/final regression remains inapplicable because normal acceptance never passed
- Old-behavior control and comparative result: exact base `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`; three base plus three changed Empire Earth4 five-bot MAX games passed to tick 10,000. Throughput was 79.209 vs 91.585 ticks/s and median-of-run `tick_time` 5.696451 vs 5.778586 ms (+1.442%), but later-observed unrelated contention makes this compatibility-only, non-golden overhead evidence
- Match narratives and routine policy-review conclusions: `policy review not applicable unless contract authority changes`
- Terra cycle code reviews and dispositions: `cycles 5/10/15 all adopted and resolved`; effective lobby identity gated, observer made single-pass/allocation-reusing with a disabled control, and weakly evidenced 500 setup reverted to 550. Final Sol-high task-PR review at `analysis/worker-3-cnc47/task-pr-review/TASK-PR-REVIEW.md` requested one provenance correction; Cycle 17 adopted it and no second response cycle occurred
- Sol-xhigh policy escalation: `not applicable/unused`
- Final regression: `not complete`; the strongest feasible fresh bounded Fastest rerun passed, but the literal three-Normal/three-Fastest tick-3000 batch, separate full-window profile, natural-conclusion MAX, save/load, and replay-playback gates were not feasible on this four-logical-CPU host
- Error/warning and diagnostic-cleanup result: final `make check` passed 0 warnings/errors plus interface checks; `make test` passed Release build and all CNC MiniYAML/default-sequence/map validation; focused C# launch tests 14/14 and Python suites 13/13 passed; Python compile/JSON/diff checks passed; generated `__pycache__` removed. One unrelated existing CA1825 warning appeared only while compiling the broad test project
- Performance/determinism result: Cycle-16 `tick_time` median/p95/p99/max 253.214/1,398.204/3,194.818/5,472.596 ms; Cycle-13 profile attributed 121/126 >=100 ms events and 77,267 cumulative ms to `ModularBot`; workload/hash/seed/roster are reproducible and clean replays show no desync, but independent AI matches are not claimed bit-identical and observer overhead lacks a complete numerical distribution
- Deferred work: CNC-49 should investigate ordinary bot/actor/activity/path contention with unchanged workload gates; before `Complete - testing`, explicitly reject NaN/infinity/negative/non-monotonic measurement clocks and complete ratio/overhead/adversarial/save-load/replay evidence. Preserve the known general `xvfb-run` orphan-grandchild issue unless separately authorized
- Known failures/risks: host becomes CPU-bound around ticks 1400-1572; no accepted Normal/Fastest ratio, three-run distribution, full-window floor trajectory, or verified <5% observer-overhead distribution; partial results correctly remain null/failed. A Cycle-16 tick-0 launch was excluded after detecting contention
- Relevant artifact paths: task report `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260807-bug-polish-03/WORKER-3-CNC-47/REPORT.md`; final bounded result `analysis/worker-3-cnc47/cycle-16-final-bounded/output-02`; final narrative `analysis/worker-3-cnc47/cycle-16-final-bounded/commenter/NARRATIVE.md`; profiles `analysis/worker-3-cnc47/cycle-{13-profile,14-orchestrator-profile}`; reviews `analysis/worker-3-cnc47/cycle-review-{05,10,15}` and `analysis/worker-3-cnc47/task-pr-review/TASK-PR-REVIEW.md`
