# Integration: coordinated bug/polish RC2 candidate

- Round: `20260806-bug-polish-01`
- Status: complete five-task cumulative RC2 code candidate; combined worker adversarial testing and final release review remain pending
- Recorded common product base: `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
- Stable branch: `agent/cnc-20260806-bug-polish-01-release`
- Exact combined RC2 code candidate: `b456fd89fac88d71dfadd65c47cfb7b409d44122`
- Release target: `bleed`
- Original release PR: https://github.com/Realpra1/LibertyDawn/pull/82
- Successor draft release PR: https://github.com/Realpra1/LibertyDawn/pull/84

## Live release-PR state

The job envelope recorded PR #82 as the open release PR to update. Before RC2
integration began, GitHub reported that PR #82 had already been squash-merged at
`2026-08-07T00:54:15Z` as `b6dc87832c8db5cd8a7d9a28316784b5a39728f0`.
Its merged head was the partial RC1 receipt commit
`0057dd25868e1cc6f7a3ba406062caa05eca2406`. A merged PR cannot be updated in
place. The stable branch was therefore synchronized with that exact target commit
using a no-content merge before adding RC2, so a successor draft PR can contain
only the cumulative work not already present in `bleed`. Draft PR #84 now carries
the same stable branch forward for the remaining RC/test rounds.

## Source verification

All supplied feature heads descend from the recorded common product base and are
ancestors of the RC2 candidate. Each has a unique open source PR, a worker handoff
or review-response receipt, and the checks required by its assigned branch. Source
PRs were not merged or closed through GitHub.

| Task | Source PR | Included head | Review / worker disposition | Exact-head GitHub checks |
| --- | --- | --- | --- | --- |
| CNC-39A | #80 | `937ef0204870ff2eca39c413af7431adb279c082` | `REVIEW-2.md` reviewed `464dd7ad7b76a8833927eff4c415e046d43c9ef2`; `f3fbbb4da48a66739bfc7195a3f3b4f91e5e3d16` fixes deterministic assignment restoration; the supplied receipt head was explicitly accepted for the release as `First iteration - testing` | Linux .NET 6.0 passed; Windows .NET 6.0 passed |
| CNC-43 | #78 | `b229612791fe82f2c08e5225325e8c707d69f92f` | `REVIEW-3.md` reviewed `52250bb084ca804856d1bac0f0f59a73a4842ddd`; the supplied head records the required long-pressure response; worker status `Complete - testing` | no configured check rollup or required branch checks |
| CNC-43A | #79 | `ade3f9d3254d57de117a252b0d7537f306e5c3ae` | `REVIEW-4.md` reviewed `f584f56f12915d650bb3739cb39bfd31ee8a373a`; the supplied head records the required natural terminal state; worker status `Complete - testing` | no configured check rollup or required branch checks |
| CNC-51 | #81 | `72dad573af1cff637285187e541737c128e9499e` | independent PR review at `02397810fb993cc8263aa789a943308b2270391d`; reviewed product response `cb6a05d5a302b2f1db2f32d2f72f684005a18611` adds enabled-aircraft closing speed; supplied head records `Complete - testing` | Linux .NET 6.0 passed; Windows .NET 6.0 passed |
| CNC-39 | #83 | `0e9efa901ae35283d435b217b5498d402b3f9fa9` | `REVIEW-1.md` reviewed `53874e4328b8f00ff691d591625d5f548ed1b551`; the supplied response queues `Stop` for the exact released surplus Engineer | Linux .NET 6.0 passed; Windows .NET 6.0 passed |

## Merge order

The existing stable branch already contained the first three reviewed feature
heads and they were not re-merged or rewritten:

1. Original coordinator metadata `e0ec7f7c7b404d2c5caabdf7bde8466636ccdc35`
   -> `ecae89a7db`.
2. CNC-39A `937ef0204870ff2eca39c413af7431adb279c082`
   -> `402c808aeb`.
3. CNC-43 `b229612791fe82f2c08e5225325e8c707d69f92f`
   -> `cdaf8f214d`.
4. CNC-43A `ade3f9d3254d57de117a252b0d7537f306e5c3ae`
   -> partial RC1 code head `545b857af809450ecf4d21c76b4a6884d3a9297e`.

RC2 continued with merge commits in this order:

5. Synchronize the stable branch with PR #82's already-merged target commit
   `b6dc87832c8db5cd8a7d9a28316784b5a39728f0` -> no-content merge
   `d482b66413c8b9003909e20423c9ab25918ba9bb`.
6. Latest coordinator/task/skill metadata
   `21363184a502fe326d7550e9278f5cd1de220e69` ->
   `cb173527e8cee842024c4cf1e50d4f2e61f7b287`.
7. CNC-51 `72dad573af1cff637285187e541737c128e9499e` ->
   `b7df46abbf5e6d85a09b1701f6dd0116d14c0615`.
8. CNC-39 `0e9efa901ae35283d435b217b5498d402b3f9fa9` -> exact RC2
   code candidate `b456fd89fac88d71dfadd65c47cfb7b409d44122`.

## CNC-39 / CNC-39A conflict and semantic resolution

The CNC-39 merge conflicted in `CaptureTargeting.cs`,
`CaptureManagerBotModule.cs`, and `CaptureTargetingTest.cs`, as anticipated.
The resolution deliberately combined both tasks instead of selecting either file
set wholesale:

- CNC-39A remains the sole owner of shared Engineer/Commando purpose
  reservations, deterministic ActorID ordering, demolition safety, assignment
  progress/defer tracking, capture and demolition scan timers, and the complete
  save/restore schema.
- CNC-39 contributes the exact HP/MaxHP threshold comparison, CNC-owned threshold
  `80`, valuable transformed-husk scoring, strict replacement margin, worse-member
  pair scoring, distinct-solo allocation, bounded reachable-approach filtering,
  pair reassessment, and deterministic target/ActorID tie breaking.
- When a pair target becomes solo-capturable, the exact higher-ActorID surplus
  Engineer now receives `Stop`, is removed from the active assignment, and releases
  its shared reservation. The surviving lower-ActorID assignment is rewritten with
  claimant cardinality one while preserving its assigned/progress history, so a
  save at that transition restores a valid solo claim instead of dropping an
  incomplete pair.
- Pair invalidation, pair retarget, pair dissolve, and newly allocated pair paths
  now update shared reservations before queuing replacement capture orders and
  roll back reservations on allocation failure. No competing capture-only save or
  reservation model was retained.
- Both original test portfolios were preserved and a focused regression was added
  for shrinking a two-claimant capture reservation into a deterministically
  restorable one-claimant capture reservation.

`mods/cnc/rules/ai.yaml` combined automatically, retaining CNC-51 transport
configuration and CNC-39's exact `SoloBuildingCaptureHealth: 80`.

## Combined checks

All build-dependent commands ran under the round's canonical capacity-one
`large-build` slot.

- Focused reconciliation:
  `dotnet test OpenRA.Test/OpenRA.Test.csproj --configuration Debug --nologo --filter FullyQualifiedName~CaptureTargetingTest`
  passed 15/15 with 0 warnings and 0 errors.
- `make check`: passed; Debug build succeeded with 0 warnings and 0 errors;
  explicit-interface and conditional-trait-interface checks passed.
- `dotnet test OpenRA.Test/OpenRA.Test.csproj --configuration Debug --nologo`:
  passed 454/454, with 0 failed and 0 skipped.
- `make check-scripts`: passed CNC/common Lua syntax validation.
- `make test`: passed; Release build succeeded with 0 warnings and 0 errors, then
  CNC MiniYAML, default sequences, and every CNC map passed validation.
- `git diff --check 09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf..b456fd89fac88d71dfadd65c47cfb7b409d44122`:
  passed.

## Combined-diff inspection

- Product changes are confined to the five task areas: capture/demolition
  coordination and reassessment, transport unload planning/mission ownership,
  MCV locomotion, and the authorized Flame Tank values.
- The only combat-balance delta is CNC-43A's exact FTNK HP `30000 -> 36000` and
  seven BigFlamer-local Heavy modifiers `20 -> 22`. CNC-43's dedicated MCV
  locomotor changes only MCV/FACT references and adds the authorized crush classes.
- Capture and transport diagnostics remain behind their existing `DebugLogging`
  controls; repeated blocked transport diagnostics are scan-rate limited. No
  unconditional console/debug output was added.
- Candidate, target, specialist, passenger, cell, and restoration ordering is
  explicitly sorted where decisions depend on iteration order. Reservation-set
  release iterations are commutative. No new simulation RNG is used.
- Transport threat snapshots are bounded by the configured 75-tick replan
  interval, landing searches by 128 candidates, and route/handoff searches by
  configured radii. Capture reassessment remains scan-bounded at 125 ticks. No
  unbounded per-tick search or duplicated reservation/policy authority was found.

## Remaining combined-game work and release risk

This is RC2 assembly evidence, not final release readiness. The following still
must be completed on the cumulative branch (and repeated on any later repair RC):

- original-worker adversarial combined testing for all five included tasks,
  including each affected acceptance scenario and at least three clean
  task-relevant adversarial cases after the latest relevant repair;
- matched full-engine old-behavior comparisons for the strategic AI changes, with
  material improvement where the changed behavior is exercised;
- at least one fresh ordinary-AI full-engine MAX regression from a new process to
  its natural conclusion, plus the relevant save/load, pressure, contention, and
  cross-task interference cases;
- fresh factual Commenter and, where AI policy is judged, Policy Reviewer receipts
  for materially judged integrated matches;
- any task-scoped repair branches/PRs and affected reruns; and
- a fresh final cumulative release-PR review after the last candidate-changing
  repair.

No source task is excluded from RC2. The principal orchestration risk is that the
recorded persistent PR #82 was merged before this cumulative candidate; the
successor draft release PR must remain open for the remaining RC/test rounds and
must not be merged into `bleed` by the integrator.
