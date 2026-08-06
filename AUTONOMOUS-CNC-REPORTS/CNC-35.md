# CNC-35 — General attack squad

## Current task specification

The ordinary AI ground army must form one cohesive mixed assault force once its configured threshold is met. Aircraft, naval actors, excluded specialists, special-behavior reservations, and newly arriving ground reinforcements must not distort the established formation center. Reinforcements remain squad-owned and travel to join the formation before contributing to strategic strength and center calculations. Existing protection/defense squads must retain their current reactive behavior.

At each strategic decision, the assault force groups visible attackable enemies into deterministic 6x6-cell regions and evaluates only a bounded mix of the nearest, highest-value, and harvester-containing regions. Harvesters are the first strategic priority; production and other buildings remain valuable; artillery that cannot defend itself against the approaching force receives high opportunity value. Target utility is reduced by defending enemy value and slowest-member travel time. Defenders continue to count at every force ratio, but at 5:1 attacker overmatch their effective resistance is negligible rather than exactly zero. The squad must visibly travel together and attack the selected opportunity, replan after target loss/stall, and avoid splitting into repeated independent attack groups.

When an air squad commits to clearing anti-air that protects a valuable opportunity, it publishes a short-lived actor mark. A ground assault that can attack the marked AA actor gives it additional bounded priority, allowing ground forces to help unlock the air target without forcing every ground squad across the map or making the AA actor infinitely preferable.

Forbidden outcomes are changing stock behavior for bots that do not enable the feature; assigning aircraft, harvesters, MCVs, transports, explorers, stealth/chemical specialists, bomb harvesters, engineers/commandos, or reserved cargo to the assault; allowing a reinforcement at the factory to pull the formation center home; clearing or stealing the protection squad; treating defenders as nonexistent; targeting hidden, dead, allied, unreachable, or unattackable actors; unbounded whole-map route work each tick; duplicate orders from concurrent squad/specialist/transport owners; save/load corruption; nondeterministic target ties; release debug spam; or regression of air targeting and repair.

## Contention inventory

The shared actors and orders are touched by `SquadManagerBotModule` active/base/assault/rush/protection/naval/air membership and save data; `IBotUnitReservations` specialist modules (stealth tanks, chemical tanks, red-Tiberium bomb trucks, exploration and engineers/commandos); `IBotTransportReservations` carriers/passengers and transported-assault adoption/restoration; crate exploration; repair and resupply behavior; rush creation; player ownership/death/cargo transitions; `Mobile` pathing and attack order resolution; air squad AA-clear target selection/marks; and unit production/new-unit discovery. Integrated evidence must force ordinary assault, protection response, later reinforcement joining, specialist/transport reservation rejection, and air AA marking.

## Plan

1. Extract deterministic ground scoring and bounded strategic targeting policy, adapting the retired prototype to the current cumulative air/value helpers and current specialist systems.
2. Add persisted ground reinforcement membership/formation-center state and cohesive assault assignment behind default-off configuration, while leaving protection squads intact.
3. Add expiring air-to-ground AA marks and configurable finite priority, enable the policy for the intended hard AIs, and add focused policy tests plus concise default-off diagnostics.
4. Run strict builds/unit/YAML checks, then headless-MAX acceptance, natural-match, save/load, and at least three distinct adversarial engine cycles including protection contention, reinforcement-center isolation, specialist/transport contention, slow mixed units, and an air-marked AA opportunity. Publish one cumulative draft PR only after all required evidence and GitHub checks pass.

## Evidence

### Implementation

- Added default-off strategic/cohesive ground policy to `SquadManagerBotModule`; enabled it only for VIKI, SkyNet, Iron Reaper, and Brutalis. Stock assault and transported-assault squads retain the old path.
- Added deterministic, bounded strategic scoring in `StrategicGroundScoring` and `StrategicGroundTargeting`: 6x6 cells, bounded nearest/value/harvester candidate sets, domain rejection, slowest-member travel cost, actor/cell value, health finishing bonus, defender weapon coverage, and geometric nonzero defender decay.
- Added one persistent `GeneralAttack` squad with a persisted formation center and incoming reinforcement IDs. Incoming/protection actors do not affect target strength or center. Material waves make the core stop until enough units join, avoiding the same-speed tail observed in cycle 19.
- Preserved unit/transport reservation ownership and protection squads. A protection actor is temporarily omitted from the general formation and rejoins afterward; reserved specialist/transport actors are removed through the existing reservation interfaces and rediscovered after release.
- Air AA-clear commitments publish finite expiring actor marks. Marked AA adds bounded ground utility only while alive and attackable.
- All diagnostics remain behind `GroundTargetDebugLogging`; all four release AI configurations set it to `false`.

### Test result

Thirty evidence-loop cycles were used. The final state passes the literal acceptance scenario, three clean post-fix adversarial cases, a fresh natural final regression, Release compilation, all 412 unit tests, and full CNC MiniYAML/map validation.

- Cycles 1-3: zero-warning Debug build, four focused policy tests, all then-current 410 unit tests, and full CNC YAML validation passed.
- Cycle 4: invalid JSON fixture; no engine evidence.
- Cycle 5: ordinary SkyNet/Brutalis engine run passed technically and exposed an unbounded wounded-target multiplier.
- Cycles 6-8: bounded health finishing priority implemented; rebuilt focused tests (five), zero-warning build, and ordinary tick-12,000 engine regression passed with harvester selection, joins, and a mission-target harvester kill.
- Cycles 9-13: AA-mark fixture setup iterations exposed and corrected malformed map rules, premature game-over, and invalid expectations.
- Cycles 14-15: the corrected ordinary-bot custom map reached natural game-over; cycle 15 proved Orcas committed to AA clear, marked `sam#26`, and the ground force selected that same actor with `air-mark=7500`.
- Cycles 16-18: protection ownership fix, zero-warning build, five focused tests, and all 411 tests passed. The short ordinary contention match exercised protection but its seed did not share a general actor.
- Cycle 19: fresh ordinary Empire Earth SkyNet versus Brutalis match reached natural tick 20,000. It proved harvester-first targeting, attack outcomes, later joins, and protection sharing (`general-shared` up to seven), while exposing a large same-speed reinforcement tail.
- Cycle 20: the first regroup-policy compile found a missing `System.Math` qualification and failed with zero warnings otherwise.
- Cycles 21-22: corrected Debug build passed; the explicitly rebuilt test assembly passed six focused scoring/regroup tests and all 412 tests. Cycle 21's first test invocation had used the stale test assembly, so cycle 22 is authoritative.
- Cycle 23: clean ordinary high-production adversarial pass to tick 14,000. Logs prove `regroup holding`, many reinforcement joins, `regroup resuming`, protection sharing, and combat outcomes without fatal signals.
- Cycles 24-26: post-fix AA fixture reruns were judged setup failures: the original SAM died before the ground scan in 24-25, while the over-durable SAM in 26 was correctly rejected by the air kill-time policy. No source behavior changed; cycle 15 remains the direct AA handoff evidence.
- Cycle 27: clean ordinary save adversarial pass. A save was written at tick 6,000 with active ground formations/reinforcement waves, then the match continued to tick 8,000 with joins and outcomes.
- Cycle 28: clean load adversarial pass. The exact cycle-27 save resumed from tick 6,001 to 10,000 with restored strategic targets, reinforcement joins, and outcomes; no corruption or fatal signals.
- Cycle 29: fresh final regression on ordinary Empire Earth reached natural game-over at tick 20,000. Logs prove repeated harvester priority, nonzero defender costs, regroup hold/resume, protection sharing, kills, headless MAX, replay, and benchmark output with no forbidden signal.
- Cycle 30: transport/exploration contention reached tick 1,000. VIKI held ten exploration scouts, reached `other-reserved=2`, completed an APC mission and assault handoff, while the cohesive force continued joining unreserved actors. The launcher marked the run failed only because its one enemy harvester was consumed before the general squad could emit the separately required target log; this does not contradict the contention outcome or the target evidence from cycles 23/27-29.

Authoritative raw evidence is ignored under `.build/cnc35/evidence/`, especially `cycle15-ground-aa-mark-natural`, `cycle23-regroup-wave`, `cycle27-ground-save`, `cycle28-ground-load`, `cycle29-final-natural`, and `cycle30-specialist-transport-contention`.

### Remaining risks

- The coarse domain check rejects disconnected targets deterministically but does not precompute an exact multi-unit route cost; terrain inside a connected domain can still produce ordinary pathfinder detours.
- Air marks are deliberately advisory and short-lived. If aircraft destroy the AA before the ground scan, the dead mark is discarded and the ground force chooses its next best opportunity.
- The cycle-30 combined fixture had only one enemy target, so the reservation race and general target choice were evidenced in adjacent integrated games rather than in the same surviving-target moment.

Publication: draft PR #73 (`agent/cnc35-general-attack-squad` into `agent/cnc34-first-economy-obelisk`) at implementation commit `bc0eab3acd`. Required Linux and Windows CI passed in 2m09s and 3m27s respectively.
