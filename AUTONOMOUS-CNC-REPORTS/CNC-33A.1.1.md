# CNC-33A.1.1: Hard-AI deterministic openings and matchup progression

- Status: in progress
- Cycles used: 0 of 30
- Branch: `agent/cnc33a1-1-hard-ai-openings`
- Base: green CNC-33A.2 PR #67 head `7d7bb3b1c3`
- Pull request: pending

## Literal acceptance

In ordinary full-engine games, SkyNet, VIKI, Iron Reaper, and Brutalis follow deterministic compatible opening goals without serializing unrelated idle construction or unit queues. VIKI and Iron Reaper use SkyNet's core structure order but omit a helipad from the ordered opening. Brutalis uses the same core order but completes a second airfield, inserting enough power to keep construction viable, before its first radar. Faction-compatible alternatives satisfy the same semantic goal rather than stalling on an unavailable actor type.

Each hard AI rapidly reaches five total live-plus-queued harvesters and then requests exactly one opening MCV. Losses before completion are replaced toward the goal without duplicate MCV requests. After those milestones, ordinary configured construction, production, economy recovery, and idle queues take over. The literal starvation regression starts VIKI with scarce cash and observes that it cannot buy three barracks while remaining at only one harvester.

The matchup gate is also player-visible. VIKI must reliably tech into available covert/stealth production, form the configured stealth-tank harassment groups, and visibly kill exposed harvesters; at long distance against two Brutalis opponents lacking adequate detection, VIKI should exploit stealth and decisively dominate. Iron Reaper's opening helipad omission is opening-only: against covert/VIKI it must later build helipads and effective aircraft using SkyNet's proven matchup response. SkyNet must scale beyond one helipad when its air-fleet production, rearm, and repair demand requires it, and a helipad that is producing aircraft must still accept concurrent aircraft repair so the army does not wait behind one facility. The deterministic planner must meet or beat old random/control planners' stable scaling and combat outcome rather than passing only internal order logs.

Forbidden outcomes are three-barracks/one-harvester starvation; ordered goals monopolizing every Fact or unit queue; a missing optional structure stalling all progress; more than one opening MCV request; counting destroyed/captured enemy harvesters; free refinery harvesters bypassing or overshooting the shared target materially; emergency zero-refinery/zero-harvester/power recovery being blocked; VIKI remaining below covert tech or keeping stealth tanks idle; Iron Reaper never building air after its ordered opening; SkyNet remaining at one pad while a larger air fleet waits to repair; treating a pad's production activity as if its repair function were unavailable; planner behavior keyed only to AI names when capabilities or current technology are the real requirement; or a deterministic opening materially weaker than legacy random/control baselines.

## Contention inventory

- Opening-policy structure goals, random/authored building fractions, defense construction, smart-economy refinery/factory reservations, minimum/zero-refinery recovery, low-power recovery, placement, repair, and every live Fact queue.
- Direct unit selection, external unit requests, refinery-granted harvesters, the shared 90-harvester cap, five-harvester opening target, MCV request/build/deploy lifecycle, cash reservation, and every vehicle/aircraft queue.
- Iron Reaper technology switching/all-technologies configuration, current-branch config selection, upgrade prerequisites, captured/lost structures, and save/load.
- SkyNet air production, helipad-capacity demand, anti-covert configuration, pad production queues versus independent repair/rearm activities, Iron Reaper air demand, aircraft squads, VIKI stealth production and specialist reservation, ordinary squads, transport/crate/red-bomb reservations, enemy detectors, target loss/capture, and harvester hunting.

## Plan

1. Audit the opening-policy history plus current SkyNet, VIKI, Brutalis, Iron Reaper, original Iron Reaper, air-response, and stealth-squad configurations; reproduce the reported failures in ordinary games and measure the legacy random-planner baseline.
2. Express each opening as semantic, capability-aware structure/unit goals. Keep the ordered coordinator responsible only for its current compatible queues and preserve emergency recovery plus unrelated idle-queue work.
3. Add a deterministic five-live-plus-queued-harvester milestone followed by one serialized opening MCV request, with loss/retry and save-state handling shared by all four hard AIs.
4. Correct the smallest actual VIKI tech/stealth-harassment, Iron Reaper anti-covert-air, and SkyNet helipad-capacity/repair-contention regressions, reusing native capability configuration and keeping pad repair independent from its production queue.
5. Add focused policy/config tests and quiet-by-default lifecycle/spend instrumentation; pass strict build, all tests/interfaces, exhaustive CNC validation, and release-quiet checks.
6. Run ordinary and focused full-engine headless MAX games covering four AIs, GDI/Nod alternatives, low cash, blocked prerequisites, parallel queues, losses, save/load, random-planner differentials, VIKI versus two Brutalis, Iron Reaper versus covert VIKI, three clean adversarial cases, and final natural-match regression. Publish one cumulative draft PR to PR #67's branch and wait for Linux/Windows checks.
