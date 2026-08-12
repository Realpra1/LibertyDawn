# Deferred Work

This file preserves unresolved findings from the retired autonomous task reports and the aged insane-AI experiment. Items that now have a dedicated task remain here as historical implementation guidance until that task resolves them.

## Transport and strategic AI

- Persist and restore in-progress transport missions across save/load. The old insane branch had transport reservations and persistent-failed-order pickup, but did not prove save restoration.
- Coordinate full multi-carrier drops, including the requested roughly ten-Mammoth attack, instead of relying only on incremental opportunistic lifts.
- Improve terrain-aware ground strategic routing and formation staging, including multiple independently coordinated ground armies.
- Improve detector prediction and escape routing for revealed stealth harassment units.

## Economy and performance


- Profile or cheaply reject the initial path search for very large groups targeting a disconnected domain. CNC-19 bounds subsequent retries, but a deliberately hostile 150-unit cross-island order still has an expensive one-time search batch.
- Add exact refinery unload-queue/wait telemetry. The old economy-pressure implementation used harvester-to-refinery ratio as a conservative proxy because reliable wait duration was unavailable.
- Profile long late-game matches on slower hardware. The adaptive 300-unit-per-AI floor intentionally favors strength and can still be expensive when several AIs are active.
- Keep a repeatable Normal/Fastest performance baseline with at least five active AIs and, where feasible, 300 or more mobile units per AI. Record real-time/game-time ratio, actor counts and profiler hotspots before accepting optimization claims.
- CNC-47 was withdrawn as poorly defined and its closed draft PR #95 (`agent/round-20260807-cnc47-repeatable-performance-baseline`, head `e9a70b7adb8c`) must not be merged or cherry-picked wholesale. It nevertheless contains potentially reusable benchmarking code: paced headless Normal/Fastest speed acceptance and requested-versus-accepted logging; fail-closed measurement-window, per-player-floor and benchmark-artifact validation; benchmark percentile/profile summaries; immutable result directories; artifact hashing; and measured-checkout/workload-source provenance. Extract or reimplement these as small reviewed seams only under a better-defined task, adding finite/monotonic clock validation first.
- Preserve CNC-47 Cycle 13/16 results only as diagnostic reference, not a baseline: the six-bot, 550-mobile-per-bot workload was infeasible on the tested 4-vCPU host and repeatedly stalled near tick 1400. Long-event profiling pointed mainly at `ModularBot`, but did not establish root cause, observer overhead, a complete Normal/Fastest comparison, or any runtime improvement.

## Specialist behavior and testing

- A CNC-37 contention fixture that deliberately transported a recon bike in a Chinook reached its drop cell but repeatedly retried and debug-logged `Unload` while the passenger remained inside. Production transport logging is disabled, and ordinary Mammoth-drop evidence was not implicated; revalidate vehicle unloading and rate-limit unchanged retry diagnostics in a future transport pass.
- Healthy-building engineer pairs previously stayed committed while the target remained healthy, which could suppress useful reassessment. The new engineer-correction task changes the capture threshold to 80% and should revalidate pair retargeting.
- A stale local user map named `TibTest.oramap` once lacked `map.yaml` and caused launcher startup failure. Keep autonomous launchers on validated packaged maps or validate custom-map packages before counting a game cycle.
- Map obstructions can force the opening barracks rally point away from the preferred cell beneath the construction yard. This is an accepted fallback unless it causes blocked production.
- Harden `launch-ai-parallel.py` interruption cleanup through the `xvfb-run` wrapper. Interrupting an invalid-content batch left two CPU-bound `OpenRA.dll` grandchildren orphaned even though the wrapper processes were finalized; explicit PID cleanup was required. Also clarify that `--content` expects the parent content root containing `cnc/`, not the `cnc/` directory itself.
