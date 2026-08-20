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

## Cycle-2 Terra correction (recorded before code decisions)

- Exact blocker: the new reveal-retreat lifecycle is unsaved;
  `IssueTraitData`/`ResolveTraitData` only persist `ReservedSpecialists` while
  `RetreatTarget`/`RetreatDestinations` are the sole reassessment barrier.
- Required correction: implement the smallest versioned per-group save/restore
  for retreat target/destinations with ownership, eligibility, passability, and
  group validation plus safe fallback; preserve existing save compatibility and
  all cycle-1 behavior.
- Required regression/evidence: prove restored multi-unit retreat blocks
  reassessment until completion; run exactly two fresh reviewed games, including
  an active-retreat save/load and a stale/dead/malformed or safe-fallback case.
- Status: cycle 2 complete from clean head
  `4369b912bc60ad42d27bc1adfff896bd7dbcc852`; ready for fresh Terra rereview
  after the single cycle-2 worker commit.

## Cycle-2 completion receipt

- Product: added version-1 per-group retreat target/destination serialization
  alongside the unchanged reservation node. Old or malformed/future saves
  restore safely with no retreat state. Restore is applied after deterministic
  regrouping and validates ownership, eligibility, reservation, group
  membership, passability/domain, exact adjacent 6x6 geometry, and enemy target
  lifetime; invalid cells recompute only from a valid target or safely drop.
- Lifecycle: any validated destination clears retained/normal targeting and
  blocks every strategic/nearby reassessment path until all remaining members
  arrive. Already-arrived members and stale targets are safely discarded.
- Focused test: `StealthTankSquadPolicyTest` PASS 95/95, including the versioned
  multi-member barrier and malformed/future fallback regressions.
- Protected checks: final `make check` PASS with 0 warnings/0 errors; full CNC
  YAML PASS; both qualifying custom maps YAML PASS; diff check/path audit PASS;
  no Air or Chemical path changed.
- Game 1: strict normal-save/load PASS/PASS at ticks 4225/5000 in
  20.020/15.018 seconds. Three-member restore at tick 4177 reported version=1,
  members=3, dropped=0, fallback=0, barrier=True; no reassessment/order before
  completion tick 4226, then normal Harass resumed; ordinary=0. Fresh narrator
  and separate policy both PASS/no blocker.
- Game 2: distinct genuine controlled custom scenario strict PASS/PASS at ticks
  1150/3500 in 9.007/16.020 seconds. Real target died during active retreat and
  normal engine save occurred at tick 1125. Restore tick 1126 rejected the stale
  target and one already-arrived member but retained one valid destination as
  `barrier=True`; no reassessment/order before completion tick 1175. Repair/
  rejoin, later engagement, total/reserved=3/3, and ordinary=0 continued. Fresh
  narrator and separate policy both PASS/no blocker.
- Game 2 policy's optional all-pending save suggestion is already covered by
  Game 1's three-member restore. Uncounted calibrations are disclosed in
  `REPORT.md`. No fixture/save-byte mutation occurred.
