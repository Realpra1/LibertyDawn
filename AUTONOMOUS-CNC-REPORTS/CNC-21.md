# CNC-21: Transport Recovery

- Status: complete; GitHub Linux and Windows checks passed
- Cycles used: 12 of 30
- Branch: `agent/cnc21-transport-recovery`
Pull request: https://github.com/Realpra1/LibertyDawn/pull/40

## Desired and forbidden behavior

After the same real movement destination is reported blocked on three bounded scans, eligible combat ground units should be reserved, loaded into a Chinook, moved along the least-dangerous live-AA route, unloaded near the destination, and returned to ordinary AI control. Carriers must not be stolen by squads, exceed ten live/queued/requested aircraft, ignore repairs, or trigger repeated expensive full-map path probes. Ordinary reachable matches must not manufacture rescue work.

## Implementation

- Added deterministic move-intent metadata to normal Move and AttackMove orders and used CNC-19's `CompleteDestinationBlocked` result. This avoids the aged experiment's repeated `FindUnitPath` probes.
- Added a bounded transport manager and atomic reservation coordinator. It supports four simultaneous missions, scans at most 32 eligible actors every 75 ticks, and uses normal Move, EnterTransport, Unload, and Repair orders.
- Reused CNC-20's route planner through a conservative live AA grid using true modified weapon range, configured buffer, mobile-AA movement radius, and finite danger costs. Chinooks receive no Orca fly-by discount, so unavoidable danger produces a least-bad route rather than failure.
- Added distance/speed-derived mission deadlines and safe return-to-base unloading if a loaded mission genuinely stalls.
- Excluded reserved passengers and all Chinooks from ordinary squad control. Added `ExternallyManagedTypes` so adaptive production/human sampling cannot bypass the exact ten-carrier cap.
- Idle hovering (`FlyIdle`) carriers stage near the construction yard. Damaged carriers below 50% route to compatible owned/allied repair facilities.
- Re-enabled bots on Archipelago after successful disconnected-island transport evidence.

## Validation

- Strict debug/style build and explicit/conditional interface checks: passed with zero warnings/errors.
- Unit tests: 241/241 passed, including two new atomic reservation/cap tests.
- CNC-only MiniYAML, sequences, and all packaged CNC maps: passed.
- Focused disconnected-path run: ten infantry produced real blocked results; one Chinook loaded, transported, unloaded, and released one passenger, then reused itself for the next.
- Damaged-carrier adversarial run: a 40%-health Chinook took a threat-aware route to a helipad and received a repair order. This exposed and fixed the engine's continuous `FlyIdle` activity not counting as `Actor.IsIdle`.
- Ten-carrier adversarial run: four simultaneous cross-island missions started, three completed during observation, and zero extra Chinooks were requested while ten were live.
- Hostile-AA adversarial run: a powered SAM on the direct diagonal changed the cross-map route from one smoothed waypoint to two, demonstrating a detour; unavailable mission actors were released without freezes or leaked reservations.
- Normal Empire Earth regression: two SkyNet and three Brutalis bots loaded and progressed through normal opening construction for 75 seconds; zero false persistent failures and zero transport missions were created.

## Failed/setup cycles and corrections

- The first repair test showed no order because hovering aircraft execute `FlyIdle` and are never technically idle; service/retry availability now recognizes it explicitly.
- A first combined utility command invoked the interactive Windows wrapper and left a process holding the binary. The process was closed and all checks rerun directly.
- A fixed 3,000-tick deadline could expire during very long cross-map routes. Mission allowance now scales with route distance and aircraft speed, with bounded safe cargo recovery.
- Temporary map fixtures for blocked units, damaged carriers, cap saturation, and AA corridors were removed after each test.

## Remaining risks

- Save/load persistence for in-flight transport missions remains deferred.
- This task rescues one unit per mission. Coordinated APC and ten-Mammoth drops remain separate CNC-24/CNC-25 work.
