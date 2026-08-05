# CNC-24.7: Parallel headless MAX simulations

- Status: complete
- Branch: `agent/cnc24-7-parallel-simulations`
- Base: created from `origin/agent/cnc33a-refinery-throughput`; publication targets `bleed` after merged integration PR #62
- Cycles used: 27 of 30
- Pull request: #64 (`agent/cnc24-7-parallel-simulations` -> `bleed`), required Linux/Windows checks green

## Literal acceptance

One repository-owned Linux command can run one, two, or three independent full-engine headless MAX games. With concurrency two or three on the four-vCPU test VM, every ordinary-AI game loads its intended map/bots/options, advances simulation ticks, exits or is bounded independently, and flushes unambiguous isolated logs, replay/save data, benchmark output, exit status, and a concise per-run/batch summary. A representative concurrent batch must complete more valid simulation work per wall-clock time than the same workloads run serially without materially starving individual games.

Forbidden outcomes are shared mutable support or map artifacts, colliding logs/replays/saves/benchmark prefixes/displays/ports, one child failure hiding or terminating healthy siblings, orphan OpenRA/Xvfb processes, indefinite waits, changed game simulation rules, treating aggregate success as proof for a failed child, or making the existing single-game path unreliable.

## Contention inventory

- CPU cores, memory, disk writes, process/file descriptors, Xvfb displays, renderer/software-GL initialization, audio fallback, and benchmark output.
- Support settings, content and map discovery, mod metadata, logs, replays, saves, screenshots, temporary maps, ports/endpoints, and cleanup signals.
- Natural game-over exit, bounded termination, launcher failure, engine fatal/exception/desync output, save/load, user interruption, repeated batches, and stale processes/artifacts.

## Plan

1. Audit the existing launch scripts, headless validation, artifact layout, and process lifecycle; benchmark one representative ordinary game serially.
2. Add the smallest portable Linux orchestrator and deterministic per-run manifest/summary format, retaining serial mode and avoiding gameplay changes.
3. Benchmark the same workloads one-wide, two-wide, and three-wide on this four-vCPU VM; choose a safe default from measured aggregate throughput and per-run progress.
4. Test natural completion, bounded stop, one invalid/failing child beside healthy games, save/load isolation, repeated batches, interruption cleanup, and the existing single-game launcher.
5. Run strict build/tests/interfaces/CNC validation as proportionate to changed code, complete three clean adversarial engine cycles plus final regression, then document and publish the cumulative task PR.

## Implementation

- `launch-ai-parallel.py` reads a JSON manifest, validates one map or save per named run, and schedules one to three independent processes. The default leaves one detected CPU free, selecting three jobs on this four-vCPU host; `--jobs 1`, `2`, or `3` remains explicit.
- Every child gets a new support directory and settings file, immutable Content link, copied map or versioned server-side save, benchmark prefix, console/log/replay/save paths, Xvfb starting display, and engine-assigned ephemeral loopback endpoint. Command and per-run JSON evidence are retained beside batch JSON/TSV summaries. One headless-only start record captures the actual loaded map and accepted bot/faction/team/spawn roster without restoring release diagnostic spam.
- A failed, timed-out, or invalid child is judged independently while healthy siblings continue. SIGINT/SIGTERM terminates each child process group with a bounded TERM/KILL cleanup, and a batch succeeds only when every child passes its own activation, tick, exit, pattern, artifact, crash, and benchmark gates.
- `Launch.ExitAtTick` provides a graceful bounded headless exit that logs the reached world tick and flushes benchmark/replay output. It is rejected outside headless automation. Existing natural-game and single-game launch behavior is unchanged.
- The example manifest defines three ordinary five-bot Empire Earth workloads, including one automated save. Four Python standard-library tests cover validation, serial/concurrent scheduling, sibling failure isolation, and server-side save staging.

## Engine evidence

- Cycles 1-3, serial baseline: three five-bot Empire Earth runs reached tick 10,000 and passed in 386.131 seconds total (77.694 valid ticks/s). Individual durations were 132.317, 128.265, and 123.460 seconds. Evidence: `.build/cnc24.7/cycle1-example-single/`.
- Cycles 4-6, two-wide: the unchanged workloads all passed in 235.919 seconds (127.162 ticks/s), a 1.64x aggregate speedup. Individual durations were 117.599, 124.679, and 117.264 seconds. Evidence: `.build/cnc24.7/cycle2-example-two-wide/`.
- Cycles 7-9, three-wide: the unchanged workloads all passed in 146.230 seconds (205.156 ticks/s), a 2.64x aggregate speedup. Individual durations were 117.846, 125.983, and 146.196 seconds; the worst per-workload slowdown versus serial was 18.4%, with no tick starvation. Three games used about 3.2 CPU cores and 2.0 GiB RSS while 5.4 GiB remained available. Displays `:90`, `:91`, and `:92`, support data, saves, replays, and benchmarks were distinct. Evidence: `.build/cnc24.7/cycle3-example-three-wide/`.
- Cycles 10-12, failing sibling: a corrupt map failed explicitly at tick zero and made the batch fail, while both ordinary VIKI/SkyNet siblings independently reached tick 5,000 and flushed their evidence. No process remained. Evidence: `.build/cnc24.7/cycle4-failing-sibling/`.
- Cycles 13-14 exposed that the local server reopens a save by filename under its versioned support directory even when the client receives an absolute path. Both attempts failed visibly and cleaned up. The runner now stages a private copy in that required directory. Evidence: `.build/cnc24.7/cycle5-save-load-two-wide/`.
- Cycles 15-16 passed the corrected two-wide load: both children independently resumed the same tick-5,000 save to tick 7,500, and one created a new isolated save before exit. Evidence: `.build/cnc24.7/cycle6-save-load-fixed-two-wide/`.
- Cycles 17-19 sent SIGINT to a live three-wide batch. Every run was marked interrupted/failed, all three process groups exited, and no OpenRA or Xvfb orphan remained. Evidence: `.build/cnc24.7/cycle7-interruption-cleanup/`.
- Cycles 20-21 were ordinary two-wide VIKI-versus-SkyNet Chokepoint matches with no configured bound. Both reached natural game-over, one beyond tick 5,000 and the other beyond tick 25,000, with valid replays/benchmarks and no fatal/desync signal. Evidence: `.build/cnc24.7/cycle8-natural-two-wide/`.
- Cycles 22-24 were the final default-width regression after all fixes. The three ordinary five-bot workloads each reached tick 10,000 and passed in 147.985 seconds total (202.723 ticks/s), including the isolated save, eleven benchmark streams per run, ordinary bot/map proof, and clean process teardown. Evidence: `.build/cnc24.7/cycle9-final-three-wide/`.
- Cycles 25-27 rebased the task onto merged PRs #62/#63 and repeated the default three-wide regression with every release AI debug switch disabled. Actual map/bot roster markers proved all five bots in each game, all runs reached tick 10,000, and the batch passed in 136.211 seconds (220.247 ticks/s). At 30 seconds each debug log was only 4 KiB, versus roughly 794 KiB by tick 10,000 with verbose diagnostics enabled, confirming the concurrent harness does not depend on playtest spam. Evidence: `.build/cnc24.7/cycle10-post-rebase-final-three-wide/`.

## Local gates

- Strict Debug build: zero warnings and errors; both explicit-interface checks passed.
- `OpenRA.Test`: 355 passed, zero failed or skipped.
- Python launcher tests: four passed.
- Exhaustive CNC rules, sequences, and map YAML validation: passed with no diagnostics.
- Skill validation: repository autonomous skill remains valid after adding concurrent-test guidance.
- GitHub: PR #64 is mergeable; both Linux checks passed in 1m50s/2m22s and both Windows checks passed in 3m31s/4m33s at implementation head `cd38beb876`.

## Remaining risk

Three five-bot games fit this 8 GiB host comfortably, but unusually large games or smaller-memory machines should select `--jobs 2` or `--jobs 1`. The shared Content link is intentionally read-only by convention; all mutable runtime data is isolated.
