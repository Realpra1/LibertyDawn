# CNC-36 — Economy artillery squad

## Current task specification

An AI that currently owns the economy technology branch must coordinate one reserved GDI MRLS artillery cluster. All eligible MRLS belong to that one cluster; Nod `arty` remains available to ordinary/general squads. The cluster reserves a small value-based screen: at least one MSAM when available, with MSAM value otherwise capped near 5% of the MRLS value, plus medium tanks and rifle infantry each near 5%. Composition must be deterministic, must not steal protected, transported, specialist, repairing, or otherwise reserved actors, and must release every reservation when branch capability or ownership is lost.

The cluster stays outside the useful range of its selected MRLS weapons, bombards the nearest valuable turret or ground blocker whose removal opens progress, and keeps escorts between local threats and the launchers. When the impact area is shrouded, a safe eligible rifle scout may advance only far enough to reacquire vision; the main launchers must not blindly advance. Empty/reloading MRLS never advance toward a target and regroup behind the screen. If a guarded MRLS dies, the screen immediately re-evaluates the remaining launcher center and closest threat rather than continuing to defend the old position. Target loss, branch changes, captures, deaths, and save/load must deterministically retarget or release the cluster.

The player-visible acceptance scenario is an ordinary economy-branch bot with several granted or naturally produced MRLS, an MSAM, medium tank, and riflemen facing a screened turret/blocker beyond current vision: one cohesive cluster forms, stops at bombardment range, scouts only when vision is missing, fires until the blocker is destroyed, keeps empty launchers back, and recenters its defenders after an MRLS is killed. A control Nod artillery unit remains governed by the ordinary general squad.

Forbidden outcomes include creating multiple artillery clusters; reserving the wrong technology branch or Nod artillery; exceeding the value caps except the required first MSAM; stealing actors from protection, transport, exploration, stealth/chemical/bomb/capture specialists, repair, or another reservation owner; sending empty MRLS forward; forcing rifle scouts through lethal opposition; advancing the whole cluster merely to reveal an impact cell; firing at hidden/dead/allied/unreachable/unattackable targets; stale center/target state after death or capture; unbounded scans/pathing; nondeterministic ties; save/load corruption; or release debug spam.

## Contention inventory

`SquadManagerBotModule` general/protection/rush membership and target orders; `IBotUnitReservations` specialist/exploration/capture modules; `IBotTransportReservations` carriers and passengers; repair behavior; unit production/new-unit discovery; `AmmoPool` reload state; attack/auto-target activities; shroud/frozen-actor targeting; ownership/death/capture; technology upgrade/downgrade state; and save/load all touch the same actors or mission state. Integrated tests must force general-squad exclusion, a protection response, transport or exploration reservation, reload/empty state, lost MRLS recentering, branch loss/recovery, and ordinary Nod artillery coexistence.

## Plan

1. Inventory current prerequisite, reservation, ammo, vision, attack-range, repair, and squad APIs; define pure deterministic composition/standoff/retarget policy with focused tests.
2. Implement one default-off economy-artillery coordinator using live capability/prerequisite state and the shared reservation interfaces, with persisted bounded mission state and concise debug evidence.
3. Configure the applicable hard/economy AI path without changing Nod artillery or bots that lack the feature; add any narrowly required production request through the existing unit-production owner rather than a competing queue writer.
4. Run strict build/unit/YAML checks and headless-MAX focused plus ordinary matches, then at least three distinct adversarial cases and a fresh final regression before publishing one cumulative draft PR on CNC35.

## Evidence

Local implementation and engine evidence are complete at 21/30 cycles; publication and required GitHub checks remain.

## Implementation

- Added a dedicated conditional `EconomyArtilleryBotModule` plus a world-independent `EconomyArtilleryPolicy`. The module activates from the live `upgrade.economy2` prerequisite for hard AIs, reserves every claimable `mlrs` into one cluster, deliberately leaves Nod `arty` untouched, and requests the required first `msam` through the existing production-request interface.
- Escort counts use deterministic whole-unit nearest-value rounding: one MSAM is the explicit minimum exception, while additional MSAMs, medium tanks, and rifle infantry each track the configured 5% launcher-value share. Prior reservations are preferred to prevent churn.
- Target selection is bounded to 48 visible candidates every 25 ticks and only accepts structures: armed buildings outrank other ground blockers. Armed launchers use normal attack orders and therefore stop at weapon range; every empty/reloading launcher receives `Stop`. A retained hidden building stops the battery and may send one reserved rifle only to a safe reveal cell.
- The defender screen is recomputed from the live launcher center and nearest local ground threat every order cycle. Branch loss, ownership/death changes, target loss, and bot disablement release or rebuild state. Scan, order, center, target, request ownership, and role IDs persist across saves.
- `SquadManagerBotModule` protection recruitment now honors generic specialist reservations, closing the contention path that could otherwise steal artillery actors. Production configuration keeps `DebugLogging: false`; focused maps alone enable diagnostics.

## Verification

- Strict local gates pass: zero-warning Debug and Release solution builds, both interface audits, 421/421 tests (including nine focused policy cases), exhaustive `./utility.sh cnc --check-yaml`, and `git diff --check`.
- Cycles 2-5 developed the literal fixture and exposed expectation/setup mistakes around exact reservation counts. Cycles 6-8 passed three concurrent full-engine runs but revealed that unit targets such as infantry and viceroids made the battery chase rather than bombard; the policy was corrected to structure-only objectives.
- Cycles 9-11 are three clean post-fix concurrent passes. Two ordinary Empire games naturally produced MRLS, requested/assigned the first MSAM, and targeted only `weap`/`resonator` structures through tick 15,000. The literal run formed `10/9000` MRLS with one MSAM, one medium tank, and four rifles; held one empty launcher, destroyed the gun, changed center from `21,11` to `20,11` after a launcher loss, preserved protection/general-squad behavior, and later used an `e1` scout for a hidden SAM. Evidence: `.build/cnc36/evidence/cycles9-11-structure-targets/`.
- Clean adversarial passes cover live capability removal/restoration (cycle 12), staged save/load resuming at tick 601 with restored composition/targeting (cycle 15), and concurrent APC transport plus ten-unit exploration contention without stealing the transport passenger or other reserved infantry (cycle 17). Evidence: `.build/cnc36/evidence/cycle12-branch-transition/`, `.build/cnc36/evidence/cycle15-load-staged/`, and `.build/cnc36/evidence/cycle17-transport-contention-final/`.
- Cycle 19 reran the complete literal scenario after all fixes and passed, including protection, general squads, empty ammo, death/recentering, blocker destruction, and hidden-target scouting. The paired close-spawn cycle 18 ended naturally at tick 5,000 but was rejected as feature evidence because Brutalis died before MRLS production.
- Cycles 20-21 both passed concurrently. The focused ordinary-bot match exercised the full feature sequence and reached natural game over at tick 5,000. A separate long-distance ordinary Brutalis-versus-SkyNet Empire match naturally produced and grew an MRLS/MSAM cluster, remained error-free through late-game losses, and reached natural game over at tick 35,000 after 590 seconds. Evidence: `.build/cnc36/evidence/cycles20-21-natural-feature/`.

## Remaining risk

Safe scouting is deliberately conservative: if no reserved rifle has a reachable destination clear of nearby ground weapons, the battery waits rather than advancing blindly. This can leave a retained hidden building unbombarded until another rifle becomes available, but preserves the task's safety requirement and ordinary modules continue the wider battle.
