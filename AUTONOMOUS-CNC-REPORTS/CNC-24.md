# CNC-24: APC Rush

- Status: complete; GitHub Linux and Windows checks passed
- Cycles used: 14 of 30
- Branch: `agent/cnc24-apc-rush`
Pull request: https://github.com/Realpra1/LibertyDawn/pull/43

## Behavior

VIKI and Iron Reaper now make one deterministic 50% strategy roll per game. Selected AIs occasionally reserve an APC and three to eight nearby infantry, favoring a two-engineer capture package or a commando package, then load fully before travelling toward a value-and-distance-ranked enemy economy/base target. The transport uses hold-fire stance until unloading within four cells. Actual damage triggers an immediate emergency unload.

After unloading, reservations are released to the existing squad, capture, and C4 systems. When APC technology or a ground route is unavailable, an existing or buildable Chinook can perform the mission using the shared threat-aware air router. Chinook production is buildability-gated, preventing rejected request spam while prerequisites are missing. Missions repeat no more than once per five minutes.

## Design choices

- Extended the current transport manager and shared reservation ledger instead of creating a competing transport owner.
- Isolated assault lifecycle/planning in `InfantryAssaultTransportManager` and pure timing/scoring rules in `InfantryAssaultPolicy`.
- Used `IBotRespondToAttack`, which receives real owned-unit damage events; the aged insane implementation's player-actor damage listener would not reliably observe APC damage.
- Capture and demolition managers now release and ignore transport-reserved specialists.
- Kept all tunable types, strategy chance, passenger counts, ranges, retry cadence, and cooldowns in CNC AI configuration.

## Validation

- Strict solution and test-project builds: passed with zero warnings and zero errors.
- Unit tests: 250/250 passed.
- Explicit-interface, conditional-interface, and complete CNC YAML/sequence/rule/map validation: passed.
- APC capture package: loaded four passengers, travelled before unloading, then two engineers captured the target.
- APC commando package: unloaded near the target and the existing demolition manager destroyed it with C4.
- Damage edge: a moving loaded APC immediately emergency-unloaded and released its passengers.
- Technology edge: an economy Iron Reaper used a Chinook and threat-aware route when APC technology was unavailable, then staged the empty helicopter at base.
- Strategy-off edge: eligible VIKI created no transport mission and continued ordinary base development.
- Normal Empire Earth regression: VIKI, Iron Reaper, Skynet, and two Brutalis AIs loaded and scaled; only eligible bot types participated in the 50% roll.
- Missing-prerequisite edge: selected Iron Reaper stopped repeatedly requesting an unbuildable Chinook while continuing to scale.

## Failed cycles and corrections

- The first emergency fixture let the capture manager claim engineers before mission creation; an immediate fixture scan proved shared reservations resolve the race.
- A one-tick fixture scan exposed repeated enter orders cancelling passenger loading. A separate configurable three-second order retry cadence fixed it.
- Initial evidence silently skipped unit tests because the fresh worktree had no test assembly. The test project was explicitly restored/built and all 250 tests rerun.
- A normal match exposed repeated Chinook requests before prerequisites were available. Production is now gated on a live queue offering the unit.

## Remaining risks

- The strategy uses current enemy information and ordinary movement; a destination can become more defended after launch. Actual damage still forces unloading rather than trapping passengers.
- Only one infantry-assault mission is active per AI at a time, intentionally limiting queue disruption and CPU cost.
