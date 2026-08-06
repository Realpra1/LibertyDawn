# CNC-37 — Covert harassment

## Current task specification

An ordinary hard AI with the live covert branch and at least two claimable recon bikes/buggies must form one reserved fast harassment squad. Every reserved bike and buggy is a core unit and only those fast core units define the squad center. The manager may attach a bounded amount of Nod `arty` support, selected deterministically and kept reserved from ordinary squads, without allowing slow support to pull the core center backward.

The fast core immediately pursues configured harassment targets such as exposed harvesters and valuable light infrastructure while attached artillery follows toward the live fast center. Artillery is held for heavy tower attacks rather than ordered onto ordinary raid targets. When the selected target is a configured tower, the fast core waits and regroups until its attached artillery is within the configured join radius; only then does the combined squad attack. Target loss, branch loss, death, capture, transport/protection contention, and save/load must deterministically retarget, rebalance, or release reservations.

The literal player-visible scenario grants a covert VIKI several bikes/buggies plus slower artillery, places an exposed harvester before a tower, and enables all normal AI modules. The fast vehicles form one squad whose logged/observed center is unchanged by distant artillery, raid the harvester without waiting while the artillery follows, then stop and wait at the tower until the support arrives before launching the heavy attack. An ordinary unreserved unit and any transport- or protection-reserved candidate remain under their owning modules.

Forbidden outcomes include creating multiple fast squads; using artillery or unrelated actors to calculate the fast center; waiting for slow support before ordinary harassment; spending artillery on ordinary raid targets; attacking a tower before attached artillery arrives; reserving every artillery unit without a bounded support need; stealing protected, transported, repairing, exploring, specialist, or otherwise reserved actors; retaining actors after branch/ownership loss; selecting hidden, dead, allied, unreachable, or unattackable targets; unbounded world/path scans; nondeterministic ties; save/load corruption; production-queue competition; or release debug spam.

## Contention inventory

`SquadManagerBotModule` general, rush, protection, and cohesive-ground ownership; `TransportManagerBotModule` route-failure rescue; `CrateCollectorBotModule`; stealth/chemical/red-Tiberium/economy-artillery specialist `IBotUnitReservations`; unit production and new-unit discovery; repair activities; bike/buggy/artillery weapon ranges, movement speeds, targeting and husks; tower ownership/death; live covert prerequisites/technology switching; movement domains; and save/load all touch the same actors or mission state. Integrated tests must force ordinary-squad exclusion, protection and transport contention, a distant artillery reinforcement, ordinary-versus-tower target transitions, branch loss/recovery, death/capture, and save/load.

## Plan

1. Add a small pure policy for bounded support count, target ranking, join/wait decisions, and core-only center behavior.
2. Add one conditional reservation coordinator that discovers live core/support actors on a bounded interval, honors every existing reservation owner, follows normal movement/attack orders, and persists only deterministic mission state.
3. Configure hard AIs behind the live covert prerequisite with production debug disabled; retain existing unit-builder weights and queues so the feature coordinates assets rather than competing to buy them.
4. Run strict build/unit/YAML checks and headless-MAX focused plus ordinary matches, then three distinct adversarial cases, a natural full match, and a fresh literal final regression before publishing one cumulative draft PR on CNC36.

## Implementation

- Added one conditional `CovertHarassmentBotModule` and a small world-independent `CovertHarassmentPolicy`. Live `upgrade.covert1` capability activates it for the four hard AIs; losing that prerequisite releases every actor and regaining it reconstructs one squad.
- The module deterministically reserves two to twelve `bike`/`bggy` core actors and attaches one `arty` per three core actors, rounded up and capped at four. Previous actor IDs win ties, extra artillery remains available to ordinary managers, and only the core actors contribute to the live center.
- Visible configured targets are selected from at most 48 nearby same-domain candidates using configured mission priority, economic value, distance, and an incumbent bonus. Ordinary targets receive a grouped core attack while artillery follows behind the core; configured towers require attached artillery and stop the core until all support is within five cells. The join check is repeated every scan so a fast core that outruns artillery regroups again.
- Claims exclude transport reservations, every other specialist reservation, base-protection actors, and active repair/resupply activities. Ownership/death changes rebalance deterministically, and scan/order timing, target ID, and role IDs persist through saves. The coordinator never requests production or changes existing build weights.
- Production configuration keeps `DebugLogging: false`; diagnostics were enabled only by ignored test maps. They distinguish role composition, competing reservation owners, core and support centers, target transitions, waiting readiness, attacks, and release reasons.

## Verification

- Strict local gates pass: zero-warning Debug and Release builds, both interface audits, Lua syntax checks, 430/430 tests (including nine focused policy cases), exhaustive `./utility.sh cnc --check-yaml`, and `git diff --check`.
- Cycles 2-5 developed the focused map and rejected invalid evidence caused by immediate conquest, a mobile harvester crushing buggies, and a moved tower being outside the reachable test domain. Cycle 6 is the first clean literal pass: four core vehicles began at `19,10` while two artillery averaged `11,12`, killed the stationary harvester by tick 101 without waiting, waited at the turret with `0/2` then `1/2` support ready, attacked only at `2/2`, regrouped again after separation, and destroyed the turret by tick 501. Evidence: `.build/cnc37/evidence/cycle6-focused-literal/`.
- Three distinct clean adversarial cycles ran concurrently. Cycle 7 removed covert capability at tick 150, observed full release at tick 201, restored it at tick 400, and rebuilt the exact four/two formation at tick 451. Cycle 8 killed one reserved buggy and deterministically reduced four/two to three/one before completing the tower attack. Cycle 9 forced the existing economy-artillery manager to reserve all three artillery; covert diagnostics reported `other:3`, retained zero support, immediately completed the core-only harvester raid, and never selected or attacked the support-required tower. Evidence: `.build/cnc37/evidence/cycles7-9-adversarial/`.
- Cycle 10 created a save during tower regrouping. Cycles 12-13 rejected isolated-load setup and matcher errors; staged cycle 14 cleanly restored four core/two support plus the live turret target at tick 202, resumed waiting, launched the combined attack, and observed the turret dead at tick 502. Evidence: `.build/cnc37/evidence/cycles10-11-save-natural/covert-save/` and `.build/cnc37/evidence/cycle14-load-final/`.
- Cycle 11 was a fully ordinary long-distance VIKI-versus-Brutalis MAX match with all production and squad modules enabled. It ended naturally after about 48,750 simulation ticks and 1,760 seconds. VIKI repeatedly formed, lost, and rebuilt feature squads up to the configured 12-core/four-support caps; raided live harvesters, airfields, and power; waited at an obelisk near game-over; and naturally yielded candidates to protection and other reservation owners without fatal, desync, or state errors. Evidence: `.build/cnc37/evidence/cycles10-11-save-natural/ordinary-natural-viki-long-distance/`.
- Cycles 15-16 passed concurrently after all source changes. A real heavy-drop mission reserved one bike and Chinook before the covert scan; the coordinator reported `transport:1/other:1`, formed only the remaining three-core/one-support group, and still killed the harvester. The fresh literal regression independently repeated the exact separated centers, harvester death, `0/2` turret wait, combined attack, and turret death with no forbidden cross-role orders. Evidence: `.build/cnc37/evidence/cycles15-16-transport-final/`.
- Cycle 17 changed one reserved buggy to enemy ownership, observed the formation and support shrink from four/two to three/one, then changed the live enemy turret to friendly ownership. At the next scan the logged target was still alive but the squad abandoned it for an enemy structure, proving both actor-role and target relationship revalidation. Evidence: `.build/cnc37/evidence/cycle17-capture/`.

Total evidence-driven cycles: 17.

## Remaining risk

The coordinator deliberately uses visible, same-domain objectives and ordinary engine orders; it does not add a separate threat map or transport fallback for the fast squad. On very large or defended maps it may therefore wait for ordinary visibility, lose fragile bikes/buggies, or leave distant artillery catching up for a long time. The natural match demonstrated recovery rather than guaranteed squad survival. The unusual single-bike Chinook contention fixture also exposed repeated heavy-drop unload retries outside CNC-37; this is recorded in `DEFERRED_WORK.md`, and all production debug switches remain off.

## Publication

- Branch: `agent/cnc37-covert-harassment`
- Base: `agent/cnc36-economy-artillery`
- Draft PR: pending
- Required checks: pending
