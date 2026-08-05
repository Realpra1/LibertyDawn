# CNC-26B: Iron Reaper Branch-Config Parity

- Status: local implementation complete; GitHub green pending
- Cycles used: 8 of 30
- Branch: `agent/cnc26b-branch-config`
- Base: `origin/agent/cnc66a-empire-lars`
- Draft PR: https://github.com/Realpra1/LibertyDawn/pull/53

## Literal acceptance

While Iron Reaper owns Covert technology, its authored configuration must actually produce stealth tanks, stealth harvesters, and stealth generators using the relevant native VIKI values and shared placement behavior. Stealth harvesters must remain harvesters rather than joining combat squads. An available and sufficiently charged Temple must use the existing nuclear support-power targeter. Ordinary technology switching, Extra all-branch ownership, captured/off-branch buildability, and native bot configuration must remain unchanged.

Forbidden outcomes include importing VIKI's unrelated whole-bot strategy, running a second technology or support-power manager, requesting covert actors after their prerequisites disappear, assigning stealth harvesters to attack squads, granting covert technology to another bot, changing support-power scoring, or altering native VIKI/Skynet/Brutalis values.

## Plan

- Inventory Iron Reaper, VIKI, technology switching, production, base placement, squad exclusion, and support-power ownership.
- Apply only missing first-pass covert values while retaining Iron Reaper's existing cross-branch strategy.
- Add bounded support-power decision diagnostics without changing selection behavior.
- Validate exact config parity, strict build, CNC YAML, interfaces, and unit tests.
- Exercise natural production, equal-technology parity, nuclear use, ordinary branch switching, Extra mode, and full-match outcomes.
- Publish cumulatively and require green Linux/Windows GitHub checks.

## Implementation

- Added VIKI's exact `stealth: 2` building-fraction ceiling to Iron Reaper. Both bots already use the same `BaseBuilderBotModule` placement implementation, so no parallel generator-placement code was added.
- Added VIKI's exact `sharv: 100`, `sharv: 50` limit, and `stnk: 15` unit settings to Iron Reaper.
- Added `sharv` to Iron Reaper's squad exclusions, matching VIKI and preventing a newly configured economic unit from becoming an attacker.
- Kept the existing shared `SupportPowerBotModule@b` nuclear decision and scoring as the sole behavior owner. Added an opt-in `DebugLogging` field that mirrors existing bot-debug support decisions to `debug.log`; enabled it for the shared advanced-bot module.

Selecting VIKI's entire module at runtime would also replace unrelated infantry, artillery, air, defense, expansion, and timing strategy and would behave ambiguously under Extra's simultaneous branches. Exact first-pass values are therefore copied only at the existing configuration boundary. The deliberately broader branch-dependent strategy remains CNC-26C, which is pinned until every other task is complete and requires user design questions.

## Cycles

1. Invalid harness: the initial observer used unsupported Lua `table.concat`. Bots loaded, the script failed, and the cycle was rejected before product evaluation.
2. Supporting Extra-mode game: Iron Reaper completed every branch and naturally built stealth tanks, stealth harvesters, stealth generators, and one Temple. The Temple appeared too late for its normal charge to complete in the observation window, so nuclear use remained unproven.
3. Invalid harness: a malformed background-wrapper invocation never started a fresh game and copied stale logs. The evidence was rejected.
4. Supporting allied parity game: Iron Reaper built stealth harvesters and generators under Extra, but native VIKI's weight-1 upgrade was starved behind hundreds of ordinary unit requests. This identified unequal technology timing in the fixture rather than a product failure.
5. Supporting natural Extra-mode comparison (`seed 26201`): Iron Reaper reached maxima of nine stealth tanks, three stealth harvesters, and eight generators; native VIKI reached five stealth tanks and three stealth harvesters. Neither selected a Temple in this seed.
6. Clean focused adversarial cycle (`seed 26206`): both real AIs were granted identical Covert III prerequisites; Iron Reaper received an available test Temple with a shortened test-only charge. Both exercised covert economic structures, and Iron Reaper selected seven live VIKI-base nuclear targets with no Lua/fatal error.
7. Clean full-match adversarial cycle (`seed 26207`): ordinary-tech Nod Iron Reaper observed Economy Brutalis, naturally completed Covert III, and built every required asset plus a Temple. It then eliminated Brutalis decisively; final snapshots were Iron Reaper 180 mobile units/20 harvesters versus Brutalis 0/0. The match ended naturally and its replay is archived.
8. Clean full branch-churn adversarial cycle (`seed 26208`): GDI Iron Reaper briefly completed Covert while Skynet's branch was changing, built three generators, then observed Recon and fully downgraded to Economy III. It built no new covert actor afterward and continued normal production until Skynet won the expected air-power matchup 222 mobile units to 2. The natural result proves unavailable static entries do not stall queues.

Ignored raw evidence is under `AUTONOMOUS-CNC-LOGS/CNC-26B/`, including the two natural-result replays.

## Validation

- Exact static parity check: passed for generator fraction, stealth-harvester weight/limit, and stealth-tank weight; only Iron Reaper's intended section changed.
- Strict Debug solution build: passed with zero warnings and errors.
- CNC YAML validator: passed in 20.7 seconds.
- Explicit and conditional trait-interface checks: passed.
- Unit tests: 300/300 passed.
- Real-game errors: no new fatal, unhandled, or Lua error in clean cycles 6-8.
- GitHub Linux/Windows checks: pending.

## Deferred boundary

CNC-26B intentionally does not switch Iron Reaper's entire strategy table by current branch. That second-pass design is CNC-26C and remains pinned last so later branch-specific squads/economy features exist before strategy tuning begins.
