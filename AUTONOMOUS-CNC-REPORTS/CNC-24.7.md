# CNC-24.7: Parallel headless MAX simulations

- Status: in progress
- Branch: `agent/cnc24-7-parallel-simulations`
- Base: `origin/agent/cnc33a-refinery-throughput` (draft PR #61)
- Cycles used: 0 of 30
- Pull request: pending

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
