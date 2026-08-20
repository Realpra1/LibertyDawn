# CNC-96A cycle-1 report

## Result

READY for fresh Terra review. The Stealth Tank specialist now uses exact 6x6
strategic cells, retains stable missions, performs bounded nearby reactions,
and retreats every active group member one adjacent strategic cell after a
cloaked-to-revealed transition before reassessment. Existing ownership,
repair, survivor, and reformation policy remains intact. Air is unchanged.

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
