# CNC-19: OpenRA Routing Backport

- Status: complete; GitHub Linux and Windows checks passed
- Cycles used: 11 of 30
- Branch: `agent/cnc19-openra-rebase`
Pull request: https://github.com/Realpra1/LibertyDawn/pull/38

## Decision

A literal rebase onto current OpenRA was rejected because LibertyDawn and upstream diverged in 2022 and now differ by thousands of engine and content changes. Instead, this task selectively backported the relevant upstream routing fixes with commit provenance. This preserves LibertyDawn balance, resource growth, harvester rules, maps, and current AI work.

## Implementation

- Backported failed-path cooldowns from OpenRA PR #21391 so persistent activities retry after a deterministic 20-30 tick delay rather than every tick.
- Preserved immediate retries when a movable actor causes the obstruction and retained later deadlock-recovery fixes from OpenRA PR #22487.
- Backported the later zero-length-path correction so an actor already at an acceptable destination reports success rather than a blocked route.
- Added six focused movement-cooldown tests.
- Made `Media.Debug` write to `debug.log` when Lua debugging is enabled, providing durable custom-map evidence without production logging on movement hot paths.
- Added `UPSTREAM-PATHFINDING-PORT.md` with exact provenance, compatibility choices, and intentionally untouched systems.

## Validation

- Engine build and interface checks: passed with zero warnings and zero errors.
- Unit tests: 239/239 passed.
- MiniYAML, rules, sequences, and map validation: passed.
- Single unreachable attacker: stopped retrying continuously and the game stayed responsive.
- 150 simultaneous unreachable attackers: recovered after the initial search batch; all became idle and simulation continued.
- 40 persistent cross-island capture orders: initial batch was 40 searches; later cooldown retries were deterministically spread with no more than seven on one tick.
- Already-adjacent capture: completed successfully, proving the zero-length route is not treated as failure.
- Normal Empire Earth match with two SkyNet and three Brutalis AIs: all five AIs loaded, constructed bases, harvested, grew armies, and remained responsive.

## Failed cycles and corrections

- Corrected PowerShell execution-policy invocation for the build script.
- Delayed map-harness orders until actors entered the world and kept test players alive with the proper player traits.
- Replaced a factory capture harness that invoked the wrong capture type with a neutral hospital that supports ordinary capture.
- Added durable Lua logging after discovering UI-only debug output was insufficient for automated evidence.

## Remaining risks

- The first search for a very large disconnected group can still be expensive, although repeated retries are now bounded. This is deferred to performance work.
- Archipelago remains unsuitable for ordinary bots because its islands are genuinely disconnected. CNC-21 transport recovery is the appropriate place to enable bots and prove cross-island travel.
- Transport recovery and the initial disconnected-domain search cost remain deliberately deferred; all required GitHub checks are green.
