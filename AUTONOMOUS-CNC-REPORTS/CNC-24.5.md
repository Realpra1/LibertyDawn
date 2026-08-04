# CNC-24.5: MAX Automated-Test Game Speed

- Status: complete; GitHub Linux and Windows checks passed
- Cycles used: 20 of 30
- Branch: `agent/cnc24-5-max-speed`
Pull request: https://github.com/Realpra1/LibertyDawn/pull/45

## Behavior

Local skirmish and debug games now offer a `MAX` speed that preserves the normal fixed 20 ms simulation timestep while removing wall-clock pacing. Simulation ticks, orders, RNG, AI update intervals, and game rules remain deterministic; rendering keeps an independent bounded cadence and the engine yields when the local order server has not supplied the next frame.

MAX is explicitly local-only. Network servers reject it both when changing the lobby option and at game start. Replay playback and game-save restoration never use MAX acceleration. The debug launcher accepts `max`, optional benchmark prefixes, and optional automated save checkpoints; direct command-line save loading bypasses the normal human-facing options-menu pause without changing ordinary UI loads.

## Design choices

- Added a declarative `RunAtMaximumSpeed` flag to game-speed configuration instead of identifying MAX by name.
- Kept `Timestep: 20` and `OrderLatency: 6`, matching Fastest, so only wall-clock pacing changes.
- Limited activation to a local live world; replays, loading worlds, and remote servers remain on their established timing paths.
- Preserved normal input/window event polling and a minimum render cadence while avoiding a forced render after every simulation tick.
- Added bounded activation, 5000-tick progress, and five-second no-progress diagnostics.
- Added automated save/load launch arguments so future autonomous tasks can reproduce late-game behavior from checkpoints.

## Validation

- Strict Debug build: passed with zero warnings and zero errors.
- Unit tests: 253/253 passed, including default-off and local/live-only MAX policy plus automated save arguments.
- Explicit-interface and conditional-interface checks: passed.
- Complete CNC YAML/sequence/rule/map validation and missing-sprite validation: passed.
- Identical five-bot Empire Earth setup completed naturally at both speeds. MAX processed about 101 world ticks/s versus about 43.2 ticks/s at Fastest, approximately 2.34 times the simulation throughput on this machine.
- Full MAX AI match: two Skynets versus three Brutalis loaded, built, fought, and completed naturally at world tick about 28,404; no idle-bot test fixture was accepted.
- High-load/window edge: a second five-bot match continued through minimize/restore and completed naturally at about world tick 16,780, with individual AIs producing more than 400 units and exceeding 200 concurrent mobile units.
- Save/load edge: a human-plus-Brutalis MAX game saved automatically, restored from the checkpoint, advanced from world tick 502 to 11,452, and completed naturally in about 24 seconds after loading.
- Deterministic replay: the saved-state match replayed through the normal replay timing path with no MAX activation, OOS, desync, fatal error, or exception.
- Normal-speed regression: Fastest remained wall-clock paced and completed the same fully enabled five-bot scenario without MAX activation.
- GitHub CI: Linux passed in 3m13s; Windows passed in 4m19s.

## Failed cycles and corrections

- The first benchmark launch omitted its prefix because of batch argument handling; the launcher now supports explicit and shorthand MAX forms.
- Direct save loading initially appeared stalled because the normal load workflow deliberately opened and paused behind the options menu. Only automated command-line loads now skip that human UI pause.
- Early progress telemetry watched attempted order ticks rather than actual world advancement. It now records real `WorldTick` progress and includes local/net frame and queued-order context.
- A short Chokepoint window test ended before the window could be manipulated, so it was rejected and replaced with the full five-bot Empire Earth match.
- A final local validation command mistakenly invoked the interactive utility wrapper, causing a prompt loop. The exact process was stopped and the real utility executable completed validation in 19 seconds.

## Remaining risks

- MAX throughput is CPU- and match-complexity-dependent; it is intentionally not a promise of a fixed speed multiplier.
- Rendering remains available for observability, so headless benchmark-specific optimization could make later autonomous runs faster without changing simulation behavior.
