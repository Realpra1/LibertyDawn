# CNC-96A successor: mandatory Covert III STNK

- Status: bounded successor implementation and exact-source validation complete;
  coordinator review/publication pending
- Base: `origin/bleed` / `4a1461f6c1497a70e48c8bf85078e0e7e0600deb`
- Branch/worktree: `agent/20260825-cnc96a-mandatory-stank` /
  `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260825-cnc96a-mandatory-stank`
- Carry-forward: reviewed amendment `5a3e36886819d5a7148cec9c6dec0a6bc3592fa6`
  cherry-picked clean as `e850013c62`; source trees are identical
- Planner: separate optional one-shot late-technology milestone in the existing
  `BaseBuilderBotModule`; excluded from `OpeningComplete`; configured only for
  VIKI/Iron Reaper as one `stnk` after latched `upgrade.covert3`
- Continuity: no request while unbuildable; requested/queued/pending dedupe;
  timeout/retry; lifetime-built completion; persisted baseline, unlock,
  completion, retry, outstanding request, and expiry
- Existing behavior: five Harvesters, one MCV, structure goals, OpeningActive,
  prerequisites, queue arbitration, weighted production, and fallback unchanged
- Gates: `make check` PASS zero warnings/errors; focused opening/Stealth tests 22/22;
  CNC YAML PASS; `git diff --check` PASS
- Mandatory game: PASS tick 3200; tick301 pre-STNK=0, one mandatory STNK at
  tick1481, completion 1/1, 14 later non-STNK productions
- Obelisk game: PASS tick3500; 750-tick watchdog stalls=0,
  Obelisk-attributed deaths=0, active firing observed
- Artifacts: `.build/cnc96a-mandatory-stank/{results4,obelisk-results3}`
- No push, publication, merge, task-sheet/coordinator-state edit, or bleed mutation

# CNC-96A PR132 Obelisk/repair follow-up

- Status: bounded implementation and exact-source validation complete; coordinator review/publication pending
- Base: `fee78bab9311bf15f4e2dd59f839b9331891d9d9`
- Repair: Air and Stealth now mutually participate in the same facility-claim and oldest-ready FIFO lookup; passive allied pad waiting remains intentional
- Uncloaking: lethal one-volley firing positions are excluded without making non-detector transit hard; no-target Idle/Attack paths issue one latched neighboring 6x6 safety move, wait for arrival, then rescan; no completion retreat
- Exact games: 2/2 reached tick 3500 under `.build/cnc96a-obelisk-followup/results-exact3/`; zero 750-tick stalls, zero attributed Obelisk kills, 62/86 intentional specialist firing events, 26/18 completed safety replans
- Gates: `make check` PASS 0 warnings/errors; CNC YAML PASS; focused Stealth matrix 7/7; `git diff --check` PASS
- Build order: no source/YAML change; existing VIKI/Iron STNK weights are probabilistic and no safe supported existing config/request surface guarantees one earliest-tech STNK for both, so the user-protected no-machinery-change scope leaves this blocked for coordinator disposition
- No push, publication, merge, task-sheet edit, or coordinator-state edit

# CNC-96A PR132 finite-safety policy evidence follow-up

- Status: telemetry-only implementation and exact-final evidence validation complete; coordinator policy routing pending
- Base: `dc26620de92934d0ffee25bfb0510869746e93f5`
- Product behavior: unchanged; candidate enumeration mirrors the existing danger-ascending/clearance-descending decision only when default-false `AirTargetDebugLogging` is enabled
- Debug Game A: six evaluated safe neighboring 6x6 candidates; selected `27,39` is externally recomputed rank-zero/minimum under the unchanged risk ordering; one issue at tick 208, one arrival at tick 308, three preserved checks, no later/in-flight escape at tick-800 horizon
- Release Game B: equivalent map/seed with logging field omitted; ordinary game reaches configured tick 800; external verifier observes zero safety-candidate, safety-state, or target-evidence markers
- Artifacts: `.build/cnc96a-safety-policy2/results-exact-final/`; verifier: `.build/cnc96a-safety-policy2/verify_evidence.py`
- Gates: `make check` PASS 0 warnings/errors; CNC YAML PASS; focused tests 7/7; `git diff --check` PASS
- No push, publication, merge, task-sheet edit, or coordinator-state edit

# CNC-96A PR132 policy evidence follow-up

- Status: debug-gated evidence implementation and exact-final worker validation complete; coordinator policy routing pending
- Base: `15d0bee25ddf5f008bbf01e515fe18de38b9e05a` (clean corrective-cycle commit)
- Branch/worktree: unchanged `agent/20260825-cnc96a-air-cache-safety` in `repair4`
- Product behavior: unchanged; release-default `AirTargetDebugLogging: false` incurs no evidence scan/list/counter work
- Target evidence: at tick 119 equal-score/full-HP processors score 500000 and incumbent `proc#59` is retained over comparable `proc#50`; at tick 309 damaged `proc#61` (5000/100000 HP) scores 25714280 and replaces the incumbent
- Safety evidence: tick 258 selects adjacent coarse delta `-1,1`, issues one order batch, preserves it across five 25-tick safety checks, and at tick 408 arrival transitions directly to Idle for immediate replan
- Exact-final games: 2/2 PASS at tick 1600 under `.build/cnc96a-policy-followup/results-exact-final/`
- Exact-final gates: `make check` PASS 0 warnings/errors; CNC YAML PASS; focused tests 7/7; `git diff --check` PASS
- No push, publication, merge, task-sheet edit, or coordinator-state edit

# CNC-96A PR132 corrective cycle: 6x6 cache and local safety

- Status: implementation and worker validation complete; fresh Luna narration/policy and Terra review pending coordinator routing
- Base: `71fcbb102cf5ef09e548be50995bcf285a88c427` (published PR132 head)
- Branch: `agent/20260825-cnc96a-air-cache-safety`
- Worktree: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260824-cnc96a-air-verbatim/repair4`
- Correction: restored explicit and default 6x6 Stealth strategic cells; cached one manager/profile/locomotor influence map; Air-style bounded cell shortlist and incumbent retention; one A* per selected cell
- Safety: independent 25-tick live bounded ground/detector scan; blue tiberium finite cost, red avoided; sparse non-detecting weapons finite; one neighboring 6x6-cell escape retained until arrival, then immediate replan; no completion retreat
- Repair: ground/detector influence, terrain/resource grid, Mobile speed, and shared Air+Stealth facility claims
- Validation: `make check` PASS 0 warnings/errors; CNC YAML PASS; focused tests 7/7; `git diff --check` PASS
- Final games: 2/2 PASS at tick 3000 under `.build/cnc96a-air-cache-safety/results-final/`; summaries prove 6x6 influence, live safety escape, both profiles, and arriving reinforcement joins
- Performance evidence: final benchmark reports cache hits exceeding rebuilds for STNK (812/268 and 857/261); separate Stealth strategy/local-safety/coarse-route samples emitted
- No push, publication, merge, task-sheet edit, or coordinator-state edit

# Prior CNC-96A Air-verbatim integration cycle 4

- Status: complete; committed CI correction pending fresh Terra rereview
- Base: `0f807a81cf8e9be1b8f6b4c3abd7ad4314223fea`
- Branch: `agent/20260824-cnc96a-air-verbatim-cycle3`
- Worktree: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260824-cnc96a-air-verbatim/integration3`
- Ownership: original `SquadManagerBotModule` is the sole `List<Squad>` owner; specialist groups are ordinary `SquadType.Stealth` squads
- Profiles: `stealth-tank`/STNK and `chemical`/CTNK, shared implementation, aggregate maximum four squads
- Matrix: 98/98 restored live mappings, 51 copied-Air authorities, 35 retreat exclusions, five retreat-free extractions
- Cycle 4 correction: removed only the unnecessary `using System;` reported by Linux `make check`; no behavior, configuration, or refactor change
- Product validation: exact Linux `make check` and solution build both 0 warnings/0 errors; CNC YAML pass; focused provenance tests 6/6; Air-copy checker pass; diff check pass
- Final games: same two adversarial scenarios passed 2/2 at tick 3000 on final source under `/tmp/cnc96a-cycle4-games-final-rerun`
- Final reviews: fresh separate Luna narratives and serialized fresh Luna policy reviews PASS for each game
- Policy disposition: retain the existing HP/detector regression; record the no-valid-target/route suggestion as advisory and reject scenario expansion for this nonbehavioral CI-only correction
- Raw logs, maps, benchmarks, and replays remain ignored/outside Git
- Draft PR 132 already exists at the prior head; cycle 4 did not push or update it pending Terra READY
