# CNC-96A worker state

## Assignment

- Task: amend and implement CNC-96A Stealth Tank ownership and stable tactics.
- Exact base: `d7ac2e346a0505b28d67587b25b28d9f33033ee2` (PR #124 reviewed head).
- Worker branch: `agent/20260820-cnc96a-stank-tactics` (literal worker payload/actual checkout).
- Worker worktree: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260820-cnc96a-stank-tactics/worker`.

## Literal acceptance

- Use exactly 6x6 map-cell strategic/coarse cells for Stealth Tank tactics,
  matching Air's coarse-map dimensions; do not change Air behavior.
- Eliminate repeated attack-order cancellation/churn before mission completion.
- Reduce slow reaction beside valid undefended targets; directly timestamp
  target acquisition and attack/reaction latency.
- After attacking or revealing, every Stealth Tank retreats one 6x6 coarse
  strategic cell before reassessment/continuation.
- Preserve every produced/captured Stealth Tank claim, at most four squads per
  AI, one-unit survivor behavior, repair/no-repair/rejoin behavior, and zero
  ordinary-squad leakage. Do not alter unrelated squads, balance, Air, or add
  broad micromanagement.

## Required evidence and checks

- Add focused tests for 6x6 cell geometry, order lifecycle/churn prevention,
  undefended-target reaction, one-cell retreat, ownership/reformation,
  survivor, repair/no-repair/rejoin, four-squad ceiling, and no leakage.
- Run exactly two distinct adversarial ordinary-AI/all-module custom games,
  each under 120 seconds, with separate native Luna factual narration and
  separate native Luna policy review. Each must directly timestamp attack
  order/cancel behavior or stable mission completion, nearby undefended-target
  acquisition/reaction, attack/reveal, and one-cell retreat before the next
  engagement, while recording ownership/reformation/no-leakage and repair/
  rejoin behavior.
- Preserve concise evidence paths; keep raw logs, replays, saves, and build
  output out of Git.

## Cycle-1 completion receipt

- Root causes: Stealth reused the 4-cell hazard waypoint spacing as its coarse
  tactical grid; target movement invalidated at map-cell granularity and the
  75-tick order interval retried stalled missions too quickly; nearby targets
  had no bounded local reaction path; firing/reveal had no retreat lifecycle.
- Product correction: Stealth-only production configuration now uses exact
  6x6 strategic cells, a 300-tick no-progress retry, a radius-12 local target
  reaction checked within 25 ticks, and a per-unit cloaked-to-revealed retreat
  to one adjacent strategic cell before reassessment. Retained nearby targets
  are observed without order reissue. Retreat debug evidence records every
  origin/destination coarse cell and delta. Chemical defaults remain legacy;
  Air was not changed.
- Preserved behavior: claim-all reservations, maximum-four grouping,
  reformation/survivor ownership, safe repair/no-repair/rejoin, and ordinary
  squad exclusion remain in the existing module and passed focused coverage.
- Focused test: `StealthTankSquadPolicyTest` PASS 93/93.
- Protected checks: final `make check` PASS with 0 warnings/0 errors; full CNC
  YAML PASS; both final custom-map YAML checks PASS.
- Final Game 1: `game1-final3`, ordinary Brutalis Nod vs VIKI GDI,
  tick 9000, exit 0, 32.054 s. Nearby retained reaction tick 25 with no churn;
  fire tick 67; cloak/reveal tick 3320; reveal retreat tick 5175 with
  strategic-size=6, all-one-cell=True, three delta=1 geometries; reassessment
  only after retreat completion tick 5350; owned=3/ordinary=0; no compatible
  repair path kept the damaged tank active. Fresh narrator PASS; fresh policy
  PASS/no blocker.
- Final Game 2: `game2-final5`, distinct repair/reformation pressure, ordinary
  Brutalis Nod vs VIKI GDI, tick 9000, exit 0, 27.036 s. Repair queued and fully
  rejoined; nearby retained reaction tick 25; fire tick 67; cloak/reveal tick
  4764; reveal retreat tick 4775 with all three delta=1; reassessment after
  completion tick 4875; attrition reached one retained specialist at tick
  6052; replacement was claimed at tick 6202; ordinary=0 and later missions
  continued. Fresh narrator PASS; fresh policy PASS/no blocker.
- Uncounted setup/calibration runs are disclosed in `REPORT.md`; only the two
  final strict passes above count toward the exactly-two contract.
- Status: ready for fresh Terra review after the single worker commit.
