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

## Specialist behavior and testing

- Healthy-building engineer pairs previously stayed committed while the target remained healthy, which could suppress useful reassessment. The new engineer-correction task changes the capture threshold to 80% and should revalidate pair retargeting.
- A stale local user map named `TibTest.oramap` once lacked `map.yaml` and caused launcher startup failure. Keep autonomous launchers on validated packaged maps or validate custom-map packages before counting a game cycle.
- Map obstructions can force the opening barracks rally point away from the preferred cell beneath the construction yard. This is an accepted fallback unless it causes blocked production.
