# CNC-96A worker report (cycles 1-2)

## Result

READY for fresh Terra cycle-2 review. Active Stealth Tank reveal-retreat state
now survives a normal save/load with a versioned, validated per-group payload.
Restored destinations remain the reassessment barrier; stale targets and
already-completed, ineligible, wrong-group, or impassable state are rejected or
recomputed safely. Existing ownership, repair, survivor, reformation, exact
6x6 geometry, stable mission, and nearby-reaction behavior remains intact. Old
saves remain compatible. Air and Chemical behavior are unchanged.

## Root cause and correction

The specialist influence map inherited the 4-cell hazard-route waypoint size,
map-cell target movement invalidated retained plans, and a 75-tick no-progress
retry encouraged order churn. Nearby target acquisition depended on the slow
strategic scan, and reveal transitions had no explicit mission phase.

The correction adds Stealth-profile-only values for a 6-cell strategic grid,
300-tick mission retry, radius-12 nearby scan at the existing 25-tick local
safety cadence, and reveal-triggered retreat state. Same-cell target movement
retains a plan. A retained nearby target is recognized without reissuing its
order. Every passable retreat destination stays within one adjacent 6x6 cell;
the group blocks strategic reassessment until all issued destinations complete.
Debug output records origin/destination coarse cells and `delta=1` per unit.
Legacy/off defaults keep the Chemical profile unchanged. No Air path changed.

## Tests and protected checks

- Focused policy tests: PASS 93/93 (6x6 boundaries, same-cell plan stability,
  300-tick retry, one-adjacent-cell retreat including map-edge fallback,
  bounded 25-tick reaction, plus existing ownership/repair/reformation suite).
- Final protected `make check`: PASS, build 0 warnings/0 errors and interface
  guards clean.
- Full `./utility.sh cnc --check-yaml`: PASS.
- Both final custom-map YAML checks: PASS.
- `git diff --check`: PASS; no Air/Aircraft path changed.

## Exactly two qualifying games

1. `game1-final3/cnc96a-no-repair`: strict PASS, tick 9000, exit 0,
   32.054 seconds. Ordinary all-module Brutalis Nod vs VIKI GDI. Tick 1
   total/reserved 3/3 and ordinary=0; one Harass mission. Tick 25 retained the
   nearby target at distance 9 within radius 12 with bounded-latency=25 and
   order-churn=false. Fire occurred tick 67; cloak/reveal tick 3320. Product
   retreat tick 5175 recorded strategic-size=6, all-one-cell=True and three
   direct origin→destination delta=1 entries; completion/reassessment followed
   at tick 5350. Damaged 3000/15000 tank repeatedly had no compatible safe
   repair path and stayed active. Tick 8500 owned=3. No FAIL, leakage, or
   desync. Narrator PASS; policy PASS/no blocker.
2. `game2-final5/cnc96a-repair-reform`: strict PASS, tick 9000, exit 0,
   27.036 seconds. Distinct ordinary all-module repair/attrition/replacement
   pressure. Damaged tank took a two-waypoint safe Repair order, fully repaired,
   and rejoined. Tick 25 retained the nearby target without churn; fire tick 67;
   cloak/reveal tick 4764. Product retreat tick 4775 recorded all three
   strategic-size=6 delta=1 moves; completion/reassessment tick 4875. Staged
   attrition produced owned=1 plus total/reserved 1/1 and ordinary=0 at tick
   6052. Replacement produced owned=2 plus total/reserved 2/2 and ordinary=0
   at tick 6202; later targets and reveal retreats continued. No FAIL, leakage,
   or desync. Narrator PASS; policy PASS/no blocker.

Concise review evidence:

- `.build/20260820-cnc96a-stank-tactics/reviews/game1-narrator/NARRATIVE.md`
- `.build/20260820-cnc96a-stank-tactics/reviews/game1-policy/POLICY-REVIEW.md`
- `.build/20260820-cnc96a-stank-tactics/reviews/game2-narrator/NARRATIVE.md`
- `.build/20260820-cnc96a-stank-tactics/reviews/game2-policy/POLICY-REVIEW.md`

Raw logs, maps, saves, replays, manifests, and build output remain ignored.

## Exclusions and calibration disclosure

Uncounted attempts: one tick-0 lobby setup failure (missing spectator command),
one tick-0 Lua property setup failure, one Game1 survival calibration, one
Game2 reveal-geometry calibration, and later strict-pass runs superseded when
manual audit found observer timing or a mislabeled survivor count. None count
toward acceptance. The two games listed above are the only qualifying games.

Optional reviewer suggestions for broader map/matchup and the complementary
repair outcome are already covered across the two final games where in scope;
no policy recommendation required further product change.

## Cycle-2 Terra correction (recorded before code decisions)

Terra found that the reveal-retreat lifecycle is unsaved:
`IssueTraitData`/`ResolveTraitData` only persist `ReservedSpecialists`, while
`RetreatTarget` and `RetreatDestinations` are the sole barrier preventing
strategic reassessment. Cycle 2 must add the smallest versioned per-group
save/restore with ownership/eligibility/passability/group validation and safe
fallback, preserve old saves and prior behavior, add a multi-unit restored
barrier regression, and obtain two new reviewed games including active-retreat
save/load plus stale/dead/malformed or safe-fallback pressure.

## Cycle-2 correction

- `IssueTraitData` now retains the unchanged `ReservedSpecialists` node and
  adds `StealthTankRetreatState` version 1. Each active group records its stable
  group index, optional target actor ID, and ordered member-ID/destination-cell
  pairs. Saves without this node restore with empty retreat state.
- `ResolveTraitData` parses only the supported version and fails closed to an
  empty restore on malformed/future payloads. Restore is deferred until the
  reserved actors have been deterministically regrouped.
- Restore accepts only owned, eligible, still-reserved members in the saved
  group. It rejects already-completed destinations, stale/dead/non-enemy
  targets, cells outside the map, impassable/domain-invalid cells, and geometry
  other than exactly one adjacent 6x6 strategic cell. An invalid cell is
  recomputed from a still-valid target; otherwise it is safely dropped.
- Any surviving destination clears the normal target/retained plan and remains
  the sole reassessment barrier until completion. Diagnostic output records
  restored/dropped/fallback counts, validated targets, and destinations.

## Cycle-2 tests and protected checks

- Focused `StealthTankSquadPolicyTest`: PASS 95/95. New regression round-trips
  a versioned two-member retreat and proves one completed member cannot release
  the barrier; malformed/future state safely restores empty.
- Final protected `make check`: PASS, 0 warnings and 0 errors; interface guards
  clean.
- Full `./utility.sh cnc --check-yaml`: PASS.
- Final Game 1 and Game 2 custom-map YAML checks: PASS.
- `git diff --check`: PASS; changed-path audit has no Air or Chemical path.

## Exactly two qualifying cycle-2 games

1. `cycle2-game1-final-leg` + `cycle2-game1-final-load`: ordinary all-module
   Brutalis Nod versus VIKI GDI, seed 9601, strict PASS/PASS, exit 0, initial
   tick 4225 in 20.020 seconds and loaded continuation tick 5000 in 15.018
   seconds. Product began a three-member reveal retreat at tick 4150 with
   strategic-size=6, all-one-cell=True, and delta=1 for every destination; the
   engine saved at tick 4175. Restore at tick 4177 reported version=1, groups=1,
   members=3, dropped=0, fallback=0, barrier=True. No target/order/replacement
   retreat appeared before completion at tick 4226; normal Harass resumed only
   afterward. Ownership remained total/reserved=3/3, ordinary=0. Fresh narrator
   PASS; separate fresh policy PASS/no blocker.
2. `cycle2-game2-controlled-final-leg` +
   `cycle2-game2-controlled-final-load2`: distinct genuine full-engine Empire
   Earth custom scenario, ordinary all-module Brutalis Nod versus VIKI GDI,
   seed 9622, strict PASS/PASS, exit 0, initial tick 1150 in 9.007 seconds and
   loaded continuation tick 3500 in 16.020 seconds. Three naturally cloaked or
   damaged STNKs transferred to ordinary Brutalis at tick 1000; ownership at
   tick 1001 was owned=3/ordinary-leakage=0 and product total/reserved=3/3,
   ordinary=0. A real repair order was queued. Two combat-ready members began
   a size-6/delta-1 retreat at tick 1100; their real PROC target died at tick
   1115 and the engine saved normally at tick 1125. Restore tick 1126 rejected
   the stale target and safely recognized one already-arrived member, reporting
   members=1, dropped=1, fallback=0, barrier=True, target=none. No Harass/order
   appeared before completion tick 1175; normal Harass resumed afterward. The
   damaged member fully repaired/rejoined and later three-member retreats and
   ownership/no-leakage evidence continued. Fresh narrator PASS; separate fresh
   policy PASS/no blocker.

Cycle-2 reviews:

- `.build/20260820-cnc96a-stank-tactics/cycle2-reviews/game1-narrator/NARRATIVE.md`
- `.build/20260820-cnc96a-stank-tactics/cycle2-reviews/game1-policy/POLICY-REVIEW.md`
- `.build/20260820-cnc96a-stank-tactics/cycle2-reviews/game2-narrator/NARRATIVE.md`
- `.build/20260820-cnc96a-stank-tactics/cycle2-reviews/game2-policy/POLICY-REVIEW.md`

Game 2's optional policy suggestion to cover a save where no member had already
arrived is satisfied by Game 1's three-of-three restored destination evidence;
no product change is required.

## Cycle-2 exclusions

Uncounted setup/calibration runs are retained in ignored `.build`: Game 1's
first save occurred after retreat completion; the first stale-map Game 2 leg
did not deterministically enter retreat before its save; the first controlled
calibration used a Lua cloak-edge trigger that did not fire; and the first
controlled load had a strict expected-count mismatch while the product safely
reported one already-arrived destination. The two strict save/load pairs above
are the only qualifying cycle-2 games. No synthetic test fixture was created or
used, and no save bytes were mutated.
