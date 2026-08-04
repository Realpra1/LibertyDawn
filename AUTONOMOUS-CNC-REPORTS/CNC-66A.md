# CNC-66A: Empire Lars Resource Startup Repair

- Status: first iteration
- Cycles used: 21 of 30
- Branch: `agent/cnc66a-empire-lars`
- Pull request: https://github.com/Realpra1/LibertyDawn/pull/52
- Remaining gate: real-Mac startup verification

## Result

Empire Lars now reaches active graphical play in 4.772 seconds on the Windows test host, compared with 30.283 seconds before the fix. The reported apparent crash was a synchronous startup freeze: `ResourceLayer.WorldLoaded` queried modifiers for each resource cell while an uninitialized cache deliberately failed its freshness check, repeatedly enumerating 146 resonators and their coverage circles.

Resource-modifier coverage is now built once and invalidated by actor creation/removal, condition or power changes, and movement to another cell. A refresh compares the effective old and new modifier for each cell, deterministically reschedules only changed resource cells, and preserves their elapsed stage/spread time. Unique generation tokens ensure superseded callbacks cannot create duplicate update chains.

The map package itself is unchanged. Its tracked SHA-256 is `DF994DE48995FB2BCEC4816C7355F07D2FFFE377F76696F51A1E23FFE6F80614`.

## Preserved resource behavior

- The resonator's authored `SpreadModifier: 750` still reduces ordinary Tiberium spread from 563 to 75 ticks and blue/red spread from 1,125 to 150 ticks: approximately 7.5 times faster.
- Stage behavior remains controlled by the authored `StageModifier: 100`.
- Losing power or removing resonators restores ordinary resource evolution without restarting elapsed timers or stalling updates.
- Gaining protection cancels an already queued spontaneous red-Tiberium instability explosion if the effective evolution is no longer `Explode`.
- Damage-triggered explosions remain eligible under resonator protection.
- Removing or replacing a resource invalidates its prior stage, spread, blink, and delayed-explosion callbacks.
- Overlapping modifier changes select one deterministic effective actor and only reschedule cells whose effective modifier actually changed.

## Replay compatibility

Two same-seed autonomous controls on the same build already diverged, so independent-run checksum equality is not a valid gate in this repository. A replay recorded by the repaired build played to completion on that same build without desynchronizing.

An old-build replay diverges at frame 51 even with only the minimal cache initialization repair. Investigation showed that the old recursive first-cache rebuild scheduled the first covered resource cell twice with the same tick-time token, leaving both callback chains valid. Removing that accidental duplicate chain necessarily changes the first resource update and old replay checksum; retaining it would preserve the freeze and contradictory updates the task is intended to remove.

## Cycles and evidence

1. Reproduced the startup delay and preserved logs/map identity.
2. Reduced Skynet graphical startup from 30.283 to 4.766 seconds with a one-time cache.
3-6. Exercised modifier loss, enable/disable transitions, and scheduler continuity; a 4,443-resource-cell transition retained 4,443 active schedules.
7-13. Compared old/new and repeated seeded runs, replay playback, and the minimal-fix isolation; identified the old duplicate first-cell chain as the frame-51 incompatibility.
14-16. Forced queued red-Tiberium instability and modifier churn; protection cancelled the queued spontaneous explosion and coalesced refreshes without errors.
17. Ran Empire Lars graphically with a spectator/human client and live Skynet. Active play began in 4.772 seconds and the bounded run exited normally.
18. Removed mass resonator power on the current implementation: 14,851 coverage cells changed, all 4,443 resource cells retained active schedules, and the run exited normally.
19. Oscillated power repeatedly. Seven refreshes cancelled seven queued instabilities, with no stalled or duplicate resource schedule evidence.
20. Ran two allied Skynets against three allied Brutalis on Empire Lars to natural game over in 278.880 seconds. All five bots loaded and acted. Across 99 live cache refreshes, every changed resource cell requiring timed work retained a schedule; no fatal/Lua/unhandled error occurred.
21. Ran the same five-bot autonomous setup on official Empire Earth for the seven-minute bound. All bots remained active, capacity telemetry stayed near 1.0x, and no new fatal/Lua/unhandled error occurred; the exact test process was then stopped because the match had not naturally ended.

Ignored raw evidence is under `AUTONOMOUS-CNC-LOGS/CNC-66A/`.

## Validation

- Strict Debug build: passed with zero warnings and zero errors.
- Unit tests after the final focused addition: 300/300 passed.
- Focused resource cache/scheduler/growth subset: 14/14 passed.
- Explicit-interface and conditional-trait interface checks: passed without diagnostics.
- Full CNC YAML validation: passed, including Empire Lars and every supported CNC map.
- Graphical Empire Lars: passed startup and active-play gate on Windows.
- Full five-AI Empire Lars: natural completion, active real bots, no relevant exception.
- Official Empire Earth control: seven minutes of active real-bot play, no relevant exception.

## Scope and remaining risk

No map, balance value, resource type, resonator radius, growth setting, or AI behavior changed. The malformed user map `TibTest.oramap` still emits its pre-existing nonfatal map-cache warning.

The local host cannot reproduce the original report on macOS. The implementation repairs the platform-independent startup hotspot and Windows graphical evidence matches the user's later diagnosis, but CNC-66A remains a first iteration until a real Mac confirms Empire Lars enters play and provides timestamp-matched logs if it does not.
