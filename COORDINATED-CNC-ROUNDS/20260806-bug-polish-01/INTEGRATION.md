# Integration: coordinated bug/polish RC4 candidate

- Round: `20260806-bug-polish-01`
- Status: RC4 final-review repair merged and independently verified; draft release PR ready for user review after push/CI
- Recorded common product base: `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
- Stable branch: `agent/cnc-20260806-bug-polish-01-release`
- Exact combined RC2 code candidate: `b456fd89fac88d71dfadd65c47cfb7b409d44122`
- Exact combined RC3 code candidate: `de855c42d39fc947c7d00b32b38c69e448ade6c4`
- Exact combined RC4 code candidate: `a7d29d08d83deebb7867076a141675326553dc3f`
- RC3 combined worker-receipt head before this integration record: `3e345413cf16dad746e88b7b1186b5b1e94c2312`
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

RC3 continued from exact RC2 head
`fd15540ffc98c70f085688fe0b38a4a6341fc6ed` with conflict-free merge commits in
the supplied order:

9. CNC-39 reviewed repair/receipt
   `8040ba74e963cb27945cb75af28df3baa9cfb578` -> exact RC3 code candidate
   `de855c42d39fc947c7d00b32b38c69e448ade6c4`. Its exact product repair is
   `bc3ab411f8235cfbec1a31ed7187f6e7971897a9`.
10. CNC-39A combined receipt
    `4c140dc37ae858c0eee03eb46ed2dd06d5cda581` -> `589f55f1d76c2cd9bf826a31c7643d9e541fbccf`.
11. CNC-43 combined receipt
    `10931c9f200ffe0e74b25d7856a9d05815d236f1` -> `95703a9185cfc4d148fe38c573b74ae4ac812b71`.
12. CNC-43A combined receipt
    `8947aa71f7761d0e632ce2e2e045d227b1ce7796` -> `dd6fc1625fac9682e6c1b1d3f20725bfcb69c380`.
13. CNC-51 combined receipt
    `b007a26c2b9343ee17bdb94de94e5908aa8ebcdb` -> combined receipt head
    `3e345413cf16dad746e88b7b1186b5b1e94c2312`.

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

## RC3 combined handoffs

All five exact handoff heads descend from RC2
`fd15540ffc98c70f085688fe0b38a4a6341fc6ed`. CNC-39A, CNC-43, CNC-43A, and CNC-51
change only their own durable combined-testing state/report receipts. They record
`combined pass/no repair` and introduce no product, balance, fixture, skill, task,
or unrelated metadata change. CNC-39 changes only its own state receipt and
`CaptureManagerBotModule.cs`.

The CNC-39 product tree at handoff `8040ba74e963cb27945cb75af28df3baa9cfb578`
is byte-identical outside its state receipt to reviewed repair
`bc3ab411f8235cfbec1a31ed7187f6e7971897a9`. Final repair review
`/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/reviews/worker-1-rc2/REVIEW.md`
returned `ready with one fix`: obtain one fresh
strict literal lone-Engineer pass on the exact allocation-free repair tree. The
handoff records that pass at fresh seed `39207`: the intended 79-percent target
was the first and only assignment, ownership changed at tick 525, prompt
`captured` retirement occurred at tick 526, final ownership and zero-Engineer
assertions held, both pressure/repair gates fired, all forbidden patterns were
absent, and headless MAX exited cleanly at tick 1250. Fresh factual and policy
receipts accepted this as focused evidence; no further product change was made.

## RC3 scope and invariant audit

- The RC2-to-RC3 product delta is exactly the reviewed CNC-39 repair in
  `CaptureManagerBotModule.cs`; the cumulative product tree is otherwise identical
  to RC2. There is no RC3 rules, weapon, balance, map, fixture, task, or skill
  delta.
- The base-to-RC3 product diff remains confined to the five recorded task areas.
  The combat-balance/config freeze is preserved: the only combat-balance delta
  remains CNC-43A's authorized FTNK HP `30000 -> 36000` and seven
  BigFlamer-local Heavy modifiers `20 -> 22`; the recorded CNC-39 threshold,
  CNC-43 MCV locomotor/crush rules, and CNC-51 transport configuration are
  unchanged from RC2.
- Capture and transport diagnostics remain gated by `DebugLogging`; no
  unconditional product diagnostic was introduced. Decision-sensitive actor,
  target, reservation, restoration, passenger, cell, and route ordering remains
  deterministic, with no new simulation RNG.
- CNC-39A remains the sole save/restore and shared capture/demolition reservation
  authority. The repair only retires consumed/unavailable specialists and releases
  their existing claims; it adds no competing schema or policy.
- Prompt retirement runs each bot tick but scans only the bounded active capture
  and demolition assignment dictionaries. Its mutation loop creates no temporary
  collection or `ToArray` allocation; planning, world/path scans, and full
  retirement stay on their existing bounded cadence.

## RC3 gates

Every build-dependent command ran on combined receipt head
`3e345413cf16dad746e88b7b1186b5b1e94c2312` under the canonical capacity-one
`large-build` lock.

- Focused `CaptureTargetingTest` command passed 15/15, with 0 failed and 0 skipped.
- `make check` passed; Debug build succeeded with 0 warnings and 0 errors, and both
  explicit-interface checks completed cleanly.
- Full Debug `OpenRA.Test` passed 454/454, with 0 failed and 0 skipped.
- `make check-scripts` passed Lua syntax validation.
- `make test` passed; Release build succeeded with 0 warnings and 0 errors, then
  CNC MiniYAML, all five default sequence sets, and every CNC map passed.
- `git diff --check
  09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf..3e345413cf16dad746e88b7b1186b5b1e94c2312`
  passed. `git diff --check` for the working tree also passed, and the worktree was
  clean after all gates.

## Game-resource conclusion

Set the ordinary/full-engine shared default to two games. CNC-43's representative
three-way batch completed only 1/3 at 6,447,160 KiB sampled peak aggregate RSS on
this host, while its two-way rerun completed 2/2 with about 2.24 GiB less peak RSS.
CNC-39 and CNC-39A also measured worse contention or lock starvation at three-way
capacity. Three-way batches were reliable for CNC-43A and CNC-51's short bounded
fixtures, so three slots may be reserved only for explicitly short, bounded,
isolated fixtures whose combined memory and completion bounds are known. Normal,
long-pressure, natural-conclusion, and other full games use at most two slots.

RC3 excludes no task. Draft PR #84 remains the sole successor release PR and must
remain open until the final cumulative release review is complete; the integrator
must not merge it into `bleed`.

## RC4 final-review repair handoff

The final RC3 combined review at
`/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/reviews/final-rc3/REVIEW.md`
returned `ready with one fix`. Its sole required finding was that a loaded CNC-51
rescue could renew its safe-recovery deadline indefinitely when every useful site
remained unsafe, retaining the active mission and its reservations forever.

The supplied repair branch
`agent/round-20260806-cnc51-rc3-final-repair` was fetched from `origin`. Exact
handoff `2e6fa14c56dceebb7dffadc8882ced0461383d9f` descends from exact RC3 head
`2343cf158bd378b913eeb9b3001f747be43abc0a` and contains exact product repair
`4be958ee073f6cce62ddeb965c6664a7e7087354`. The handoff was merged locally,
without using GitHub's merge action, as merge commit
`a7d29d08d83deebb7867076a141675326553dc3f`; this is the exact RC4 code
candidate. There was no merge conflict. Source task PRs remain open and no commit
was merged or pushed to `bleed`.

The RC3-to-RC4 tree delta is exactly the CNC-51 worker state receipt plus the
reviewed repair's four product/test paths:

- `TransportMissionCoordinator.cs` adds explicit parked loaded-mission ownership,
  releases obsolete cell claims on parking, excludes parked missions from active
  capacity, and releases both active and parked actor reservations on completion.
- `TransportRescueRecoveryLifecycle.cs` adds the one-way
  `Active -> Returning -> Terminal` lifecycle and assigns the recovery deadline
  only on the first return transition.
- `TransportManagerBotModule.cs` enters terminal recovery once, cancels the stale
  carrier order, parks loaded ownership, retries only on the existing bounded
  landing-hold cadence, and releases ownership only after a safe physical
  cargo-zero handoff.
- `TransportMissionCoordinatorTest.cs` adds the persistent-no-safe-site lifecycle
  regression for a non-renewable deadline, one terminal transition, stale-claim
  release, non-stealable parked actors, freed active capacity, and deterministic
  later safe-cell claim/release.

The RC4 product tree is byte-identical to product commit
`4be958ee073f6cce62ddeb965c6664a7e7087354`; the handoff adds only the CNC-51
state receipt after that product commit. No unrelated product, balance, task-sheet,
skill, map, or fixture change was merged for RC4.

## RC4 resolved finding and evidence

The final-review finding is resolved. `RecoverTimedOutCargo` can begin return only
from `Active`, so repeated plan failures cannot replace the preserved recovery
deadline. Deadline expiry can enter `Terminal` only once. Terminal entry queues one
stop, clears the obsolete plan/revision, releases exact landing/exit claims, and
moves carrier/passenger ownership out of active mission capacity without exposing
hidden cargo to another manager. With no plan, terminal recovery performs only the
constant-time cargo/cadence checks each tick; exact planning and any new orders run
only after `LandingHoldTicks`. A genuinely safe later plan may be claimed by the
parked owner and is released only after physical unload makes cargo zero.

The worker's exact full-engine evidence and fresh policy receipt were inspected
rather than accepted from the report alone:

- Run 53, ordinary Cabal/SkyNet on **Empire Earth4 Terminal Recovery**, seed
  `510063`, map SHA
  `6b739c128da2462d8d33f551f704a427cc48e45d126049408bf088bc02b075a8`,
  passed at tick 6000 in 17.018 seconds (352.484 valid ticks/s). Its summary marks
  every required pattern true and every forbidden pattern false. The debug log
  contains exactly one parked-reserved terminal transition at preserved deadline
  3975, cargo/reservations retained under six live threats through tick 4750, a
  fresh safe plan after the tick-5000 opening, physical exit at `12,8`, cargo zero,
  and exact release. It contains no deadline-renewal/order/log loop, premature
  cargo-one release, fatal error, or desync.
- Run 54, the unchanged three-rescue literal contention regression, seed `510057`,
  map SHA
  `34f96775d113714607fbcb97977fd7b586d4af002c6f119d29a0b906a685c8f9`,
  passed at tick 5200 in 18.019 seconds (288.489 valid ticks/s). All three rescues
  physically handed off with cargo zero and released; mission 2 retained its live
  Mammoth revision-2 route and mission 3 retained the map-edge exit. All required
  patterns are true and timeout, safe-recovery, fatal, and desync patterns are
  absent.
- The factual narrative at
  `analysis/worker-5-cnc-51/rc3-terminal-comment/NARRATIVE.md` agrees with the
  staged summary/debug artifacts. The fresh policy receipt at
  `analysis/worker-5-cnc-51/rc3-terminal-policy/POLICY-REVIEW.md` is `approved`
  with no requested policy change: retaining loaded ownership under known danger,
  resuming only when a safe physical recovery exists, and releasing only after
  cargo zero match the survival-first design.

The final Sol-high reviewer already used its one allowed response. RC4 confirms
that exact stated finding is resolved; no new or expanded review round was
invented.

## RC4 scope and invariant audit

- The RC3-to-RC4 product delta is byte-for-byte the exact CNC-51 repair. No
  `mods/cnc/rules`, weapon, balance, map, fixture, or unsupported-game product file
  changed. The cumulative balance/config freeze remains exactly as recorded for
  RC3: authorized CNC-43A Flame Tank values, CNC-43 MCV locomotion/crush rules,
  CNC-39 solo-capture threshold, and CNC-51 transport configuration only.
- New diagnostics use the existing `Debug` helper gated by `Info.DebugLogging`;
  production CNC configuration remains false. The terminal transition is logged
  once and unchanged plan failures are silent between bounded hold cadences.
- The lifecycle uses no RNG or unordered choice. Existing stable actor/cell/route
  ordering is unchanged; the only new state transition is monotonic and its
  deadline addition is overflow bounded.
- Reservation semantics are explicit: parking releases stale landing/exit claims,
  preserves the exact carrier/passenger actor reservations, frees active mission
  capacity, permits later exact cell claims by that same parked owner, and releases
  all claims/reservations only on physical completion. No competing save schema or
  transport policy owner was introduced; in-flight mission save persistence
  remains the previously recorded out-of-scope behavior.
- `AdvanceTerminalRecovery` adds no per-tick collection or LINQ allocation. While
  parked without a plan it performs only cargo and integer cadence checks; bounded
  planner/route work and order allocation occur only on the existing
  `LandingHoldTicks` cadence or a material plan transition.

## RC4 gates

Every build-dependent command ran on exact RC4 code candidate
`a7d29d08d83deebb7867076a141675326553dc3f` under the canonical capacity-one
`large-build` lock.

- Focused Debug transport, air-threat, threat-aware-route, and capture-targeting
  filter: passed 98/98, with 0 failed and 0 skipped. This includes the new
  persistent-no-safe-site recovery lifecycle regression.
- `make check`: passed; Debug build succeeded with 0 warnings and 0 errors, and
  both explicit-interface checks completed cleanly.
- Full Release `OpenRA.Test`: passed 455/455, with 0 failed and 0 skipped.
- `make check-scripts`: passed Lua syntax validation.
- CNC-only `make test`: passed; Release build succeeded with 0 warnings and 0
  errors, followed by CNC MiniYAML, all five default sequence sets, and every CNC
  map.
- `git diff --check
  09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf..a7d29d08d83deebb7867076a141675326553dc3f`:
  passed. Working-tree `git diff --check` also passed before this record update.

RC4 excludes no task. Draft PR #84 remains the sole successor product release PR
to `bleed`; the integrator leaves it open for the user and does not merge it.
