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
- When the current target crosses a 6x6 strategic-cell boundary, do not lose or
  automatically replace it. Perform Air-style fresh incumbent-aware
  reassessment.
- Retain the incumbent unless invalid or a challenger meets Air's same
  meaningful-improvement/switch threshold; if retained, refresh its route/order
  for the new cell without treating it as target loss.
- Air implementation and behavior are the golden reference and must not be
  modified; do not invent a separate weaker threshold.
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

## Cycle-3 Terra correction (recorded before code decisions)

- Exact blocker: restored destination validation currently checks only
  passability and adjacent-one-cell geometry. If a still-live retreat target
  moves between save and load, a formerly-away saved destination can now point
  toward the target.
- Required correction: pass the validated live target into restored-destination
  validation and require current away-direction geometry. If that validation
  fails, recompute with `FindStrategicRetreatDestination`.
- Required regression/evidence: add a focused moving-live-target save/load
  regression proving no restored member resumes toward it; run exactly two new
  reviewed ordinary-AI/all-module games. At least one must save an active
  retreat, move the target before load/continuation, and prove recomputed exact
  one-cell-away destinations, barrier/no churn, completion, and reengagement.
  The other must cover multi-unit/stale/repair/ownership behavior.
- Status: cycle 3 complete from clean head
  `dafc4de0fd37084ff409c9e60167cbbfe57e6b04`; ready for the required Luna
  cycle-3 advisory code review after the single cycle-3 commit, then Terra
  rereview.

## Cycle-3 completion receipt

- Product: restored destination validation now includes the current position of
  a validated live target and accepts only the exact current one-cell-away 6x6
  strategic direction. A changed direction recomputes through
  `FindStrategicRetreatDestination` and queues a replacement Move so the old
  serialized activity cannot continue toward the target or strand the barrier.
  Fallback diagnostics record origin/destination/target cells, delta, and away.
  Stale-target behavior remains compatible.
- Focused test: `StealthTankSquadPolicyTest` PASS 96/96, including versioned
  serialize/load with the live target crossing the unit and the saved direction
  being rejected/recomputed away.
- Protected checks: final `make check` PASS with 0 warnings/0 errors; full CNC
  YAML and both qualifying map checks PASS; diff/path audit PASS with no Air or
  Chemical path changed.
- Game 1: strict active-save/load PASS/PASS at ticks 1410/2800 in
  10.010/12.017 seconds. Target crossed after retreat began and before save.
  Restore recomputed both members (`fallback=2`) with direct size-6
  `delta=1:away=True` geometry, retained barrier/no churn until completion tick
  1467, then reengaged. Repair/rejoin and ordinary=0 continued. Fresh narrator
  and separate policy PASS/no blocker.
- Game 2: distinct strict stale-target save/load PASS/PASS at ticks 1140/3500
  in 10.008/14.014 seconds. Restore retained two destinations with barrier=True
  while rejecting the dead target; no order before completion tick 1169, then
  Harass, repair/rejoin, later three-member exact retreat, and ordinary=0.
  Fresh narrator and separate policy PASS/no blocker.
- Policy dispositions: Game 1's optional stale-target/lifecycle request is
  satisfied by Game 2. Additional invalid-target/map coverage is optional and
  requires no product change. Calibrations are disclosed in `REPORT.md`; no
  fixture or save-byte mutation occurred.

## Cycle-4 required Luna advisory (recorded before code decisions)

- Advisory blocker: `UpdateStrategicRetreat` currently removes a member's
  destination as soon as that actor enters `repairing`. A repairing tank has not
  necessarily reached its issued retreat destination, so this can empty the
  group barrier and resume strategic reassessment/Harass before every retreat
  responsibility completes.
- Required disposition: accept and correct the advisory with the smallest
  coherent lifecycle. A repairing member must not be silently counted complete;
  its responsibility must remain pending until explicitly completed, or be
  explicitly canceled/reissued/accounted while the group remains blocked until
  every responsibility resolves. Preserve repair safety/rejoin and all other
  behavior.
- Required regression/evidence: focused coverage plus exactly two fresh reviewed
  games directly exercising damage-to-repair during an active multi-member
  retreat, no premature barrier release, repair/rejoin and later reassessment,
  with distinct control/no-repair/ownership pressure.
- Status: cycle 4 complete from clean head
  `91ddc055c1ead4efcd0e2a3815299074038248ed`; ready for fresh Terra final
  rereview after the single cycle-4 commit.

## Cycle-4 completion receipt

- Advisory disposition: accepted and corrected. Repairing no longer resolves a
  retreat responsibility. Only ineligibility or physical strategic-cell arrival
  removes it; full repair/cancellation explicitly reissues any pending Move
  under the retained group barrier.
- Focused test: `StealthTankSquadPolicyTest` PASS 97/97, including repairing,
  physical-arrival, and ineligible responsibility outcomes.
- Protected checks: final `make check` PASS with 0 warnings/0 errors; isolated
  full CNC YAML and both maps PASS after one concurrent host bus error; diff/path
  audit PASS with no Air or Chemical path changed.
- Game 1: strict repair-pressure PASS, tick3500, 13.011 seconds. Active
  three-member retreat, damage/displacement, real repair with pending=True, no
  reassessment while repairing, explicit repair-resume barrier at tick2650,
  physical completion2725, later Harass/retreat cycles, repair/rejoin, and
  ordinary=0. Fresh narrator/policy PASS/no blocker.
- Game 2: distinct strict slowed no-repair control PASS, tick3500, 13.010
  seconds. Damage occurred during active three-member retreat; no repair path
  kept the member active and no reassessment occurred before physical completion
  tick1300; later Harass/retreat cycles and ordinary=0 continued. Fresh
  narrator/policy PASS/no blocker.
- Policy dispositions: Game 2 satisfies Game 1's optional broader timing/map
  suggestion. Further repair-selection/timing combinations are optional and need
  no product change. Calibrations are disclosed in `REPORT.md`; no fixture/save
  mutation occurred.

## Amendment acceptance: Air-style target retention

- Direct focused tests and games must show an incumbent crossing multiple
  strategic cells, remaining selected against weaker/marginal challengers,
  switching for an invalid incumbent or sufficiently better challenger, and no
  cancellation/idle gap during retained reassessment.
- Preserve retreat, save/load, repair/rejoin, ownership, claim coverage, and
  zero ordinary leakage. Air implementation and behavior remain unchanged.

## Cycle-5 correction (recorded before code decisions)

- Exact correction: when a current target crosses a 6x6 strategic-cell boundary,
  perform fresh incumbent-aware reassessment using Air's same meaningful-
  improvement/switch threshold/helper. Retain a valid incumbent unless a
  challenger qualifies, and refresh the retained moved target's route/order
  without representing target loss, Stop/cancellation, or an idle gap.
- Direct evidence must cover one multi-cell moving incumbent retained against
  weaker/marginal challengers and one invalid-incumbent or clearly threshold-
  winning switch. Both preserve retreat/save/repair/ownership behavior and Air
  output remains unchanged.
- Starting head: `a5f0ed6c38b2d54ea49bf92bfb48751070246e77` plus authorized
  Task Maker amendment `1587cb3c8c` applied without a separate commit.

## Cycle-5 user amendment (recorded before amended code decisions)

- Air helper parity is switch-decision parity only. Stealth retains its distinct
  target priorities and scoring because it destroys buildings faster; do not
  copy or unify Air target priorities into Stealth.
- Add explicit configurable wall target priority `1` to both the Stealth and Air
  profiles, far below their existing valuable-target priorities such as
  harvesters (roughly thousands under existing configuration). This is the only
  authorized Air behavior/configuration change.
- The first strict retain/save and threshold-switch pair is pre-amendment and
  uncounted final evidence. Preserve its diagnostics, then run exactly two new
  post-amendment qualifying games with direct wall-versus-valuable-target
  discrimination plus the required multi-cell retain and threshold switch/no-gap
  behaviors, each with fresh Luna narration and policy review.
- Task Maker amendment `a1db6a4fe9` was incorporated into the staged task sheet
  and reconciled with this state without a separate commit.

## Cycle-5 completion receipt

- Product: a target crossing a 6x6 boundary now receives a fresh raw-score
  incumbent/challenger reassessment through Air's exact
  `ShouldSwitchTarget` decision helper. A retained incumbent reuses the existing
  `TargetMoved` route refresh without Stop, cancellation, target clearing, or
  idle gap. Stealth priorities and its value/distance formula remain distinct.
- Amendment: explicit Stealth `WallTargetPriority` and Air
  `AirTargetWallValue` are both configured to `1`. Wall classification uses
  `LineBuildNodeInfo` before generic building fallback. All ten ordinary Air
  profiles explicitly carry value 1; no Air priority table/formula changed.
- Focused tests PASS 173/173, including exact threshold/category parity,
  invalid-incumbent behavior, overflow-safe long scores, all ordinary Air
  configs wall=1/harvester>wall, and distinct Stealth-versus-Air harvester
  scoring. Final `make check` PASS 0 warnings/errors; full CNC YAML and both map
  YAML checks PASS; diff/path audit PASS.
- Final Game 1: ordinary all-module Brutalis Nod vs VIKI GDI, seed9653,
  initial tick1275/10.012s and active-retreat load tick3500/15.026s, both exit0.
  Apache scored HARV5550 versus BRIK1 and selected HARV. Stealth retained HARV
  across two boundary crossings with distinct scores458333/183333, recorded
  BRIK score3/priority1, refreshed with no gap, restored barrier=True on load,
  then repaired/rejoined with ownership3 and ordinary0. Fresh narrator and
  separate policy PASS; policy classification none.
- Final Game 2: distinct ordinary all-module Brutalis Nod vs VIKI GDI,
  seed9654, tick3500/16.031s, exit0. Apache repeatedly selected HARV5550 over
  active BRIK1. At HARV boundary crossing, Stealth switched from raw282051 to
  SHARV547619 over the 25% threshold while BRIK remained score3/priority1; the
  routed order was immediate with no loss/Stop/cancel/idle gap. Retreat,
  repair/rejoin, ownership3, and ordinary0 continued. Fresh narrator and
  separate policy PASS; policy classification none.
- Pre-amendment strict games and wall calibration are explicitly uncounted;
  only the two post-amendment reviewed games above satisfy the cycle contract.
  Status: ready for Terra review after the single cycle-5 commit.

## Cycle-6 Terra correction (recorded before code decisions)

- Exact blocker: boundary reassessment currently populates `freshIncumbent`
  only from the list already capped by `MaximumTargetCandidates`. A valid live
  incumbent ranked 49th or later is therefore misclassified invalid and can be
  switched or abandoned without Air's configured 25% decision rule.
- Smallest correction: explicitly include/evaluate the live incumbent outside
  `MaximumTargetCandidates` only for boundary reassessment; keep the cap for
  challengers and preserve raw scoring, defended-category, threshold, wall1,
  route-refresh, retreat/save/repair/ownership, and all other behavior.
- Required proof: focused over-cap regression plus exactly two new reviewed
  ordinary-AI/all-module games: more than 48 viable candidates with a moving
  incumbent retained against a non-qualifying capped challenger/no gap, and an
  invalid or threshold-qualified switch control.
- Starting head: `0427aaa0f1f930948767979fc7c1c251c8b2a5f0`.

## Cycle-6 completion receipt

- Product: boundary scans still cap and preserve the first 48 ranked
  challengers, but now append the live incumbent when it lies outside that cap.
  Non-boundary scans remain exactly capped and an incumbent already inside the
  cap is not duplicated. Raw Stealth scoring, defended-category precedence,
  Air's 25% switch helper, wall priority 1, and Air behavior are unchanged.
- Focused Stealth/Air/config tests PASS 174/174, including rank-55 incumbent,
  unchanged first-48 challenger ordering, no-boundary cap, no duplication, and
  retained 100-versus-124 threshold behavior. Protected `make check` PASS with
  0 warnings/errors; full CNC YAML, both final custom-map YAML checks,
  `git diff --check`, and changed-path audit PASS.
- Game 1: ordinary all-module Brutalis Nod versus VIKI GDI, seed9663. Strict
  initial leg PASS/exit0 tick1500 in 12.013s and active-retreat save/load
  continuation PASS/exit0 tick3500 in 16.028s. Fifty-five viable HTNKs put the
  live HARV incumbent outside the first 48. Tick1125 logged cap48/count49,
  `incumbent-outside-cap=True`, valid undefended incumbent score2500000 versus
  defended challenger45454545, `RetainIncumbent`, refreshed routed Attack, and
  no loss/Stop/cancel/idle gap. Save/load restored barrier=True before exact
  retreat completion; repair/rejoin, ownership5/reserved5/ordinary0, and Air
  valuable1550 versus wall1 continued. Fresh Luna narrator completed; corrected
  bounded policy review PASS/classification none.
- Game 2: distinct ordinary all-module threshold-switch control, seed9664,
  PASS/exit0 tick3500 in 17.027s. Valid HARV282051 switched to qualifying
  SHARV547619 at the 25% rule with cap48/count24/outsideFalse, immediate routed
  order and no loss/Stop/cancel/idle gap. Exact multi-unit retreat, repair/rejoin,
  ownership3/reserved3/ordinary0, and Air HARV5550 versus wall1 continued.
  Fresh Luna narrator and policy PASS/classification none.
- Setup/calibration runs are uncounted: the initial scenario lacked an Attack
  incumbent; a custom inherited actor lacked its render alias; a 25-tick exit
  produced an incomplete save. Each was corrected before the two strict games;
  no fixture or save-byte mutation was used. Status: ready for fresh Terra
  review after the single cycle-6 commit.
