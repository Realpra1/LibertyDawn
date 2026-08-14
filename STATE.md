# CNC-110 worker assignment

- Task ID: CNC-110
- Title: Prevent destroyed-actor crashes throughout HeavyDrop transport lifecycle.
- Status: development/test cycle complete — implementation committed and ready for review; balance frozen
- Common base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Task branch: `agent/round-20260815-cnc110-heavydrop-lifecycle-hotfix`
- Worker worktree: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260815-bug-polish-06-resume/workers/worker-8-cnc110`
- Scenario constraint: use deliberately constructed custom scenarios, never fixtures.

## Authoritative task text

**Prevent destroyed-actor crashes throughout HeavyDrop transport lifecycle.** What happened: playtest `playtest-20260814-4` crashed during a long, well-separated Empire Earth4 game with two Brutalis AIs against one VIKI because `HeavyDropTransportManager` retained a destroyed, out-of-world transport pair and `IsLoaded` called `TraitOrDefault` on it. Primary bug fact and acceptance anchor: `System.InvalidOperationException: Attempted to get trait from destroyed object (tran 3340 (not in world))`; `at OpenRA.Mods.Common.Traits.HeavyDropTransportManager.IsLoaded(Pair pair) in .../HeavyDropTransportManager.cs:line 887`; `at ...HeavyDropTransportManager.<>c.<DiscardUnloadedPairs>b__26_0(Pair p) ...:line 316`; `at ...HeavyDropTransportManager.DiscardUnloadedPairs(IBot bot) ...:line 316`; `at ...HeavyDropTransportManager.AdvanceGathering(IBot bot) ...:line 262`; `at ...HeavyDropTransportManager.Tick(IBot bot) ...:line 140`. The diagnosed unsafe paths include `DiscardUnloadedPairs` using the original `wave.Pairs` after filtering `livePairs` (approximately lines 314–323), both `IsLoaded` selection/removal paths (approximately lines 885–887), and the phase guard near line 265. This is wrong because no HeavyDrop phase may dereference a destroyed or out-of-world transport/passenger or issue an order to a destroyed actor. Validate actor lifecycle before every phase use and order. Discard every invalid pair; release surviving unboarded cargo and its reservations to normal AI ownership. Existing HeavyDrop policy may continue the mission with fewer passengers or find a new transport, but mission policy and balance must otherwise remain unchanged. The predicted result is absolute absence of null/lifecycle crashes and destroyed-actor orders while valid surviving missions continue or release cleanly. This is needed because one stale pair currently terminates an otherwise valid long match. Add a focused lifecycle regression plus small full-engine custom scenarios only—no fixtures—covering carrier destruction during gathering with the remaining nine passengers loaded, timeout with eight loaded passengers, mirrored passenger invalidation, and a bounded longer two-Brutalis-versus-one-VIKI run.

## Supplied exact playtest failure

`System.InvalidOperationException: Attempted to get trait from destroyed object (tran 3340 (not in world))`

Stack anchor: `HeavyDropTransportManager.IsLoaded` line 887 -> `DiscardUnloadedPairs` line 316 -> `AdvanceGathering` line 262 -> `Tick` line 140.

Reproduction: playtest-20260814-4, Empire Earth4, long well-separated match, two Brutalis versus one VIKI.

## User decisions

Discard an invalid pair and release surviving unboarded cargo and reservations to normal AI. Cover every HeavyDrop lifecycle phase. Never dereference or issue an order to a destroyed/out-of-world transport or passenger. Existing HeavyDrop policy may load fewer units or find another transport. Freeze balance and mission policy.

## Supplied read-only diagnosis

`AdvanceGathering` filters a live-pair list but `DiscardUnloadedPairs` later enumerates the original pair list; two unguarded `IsLoaded` calls request Cargo from the disposed transport and Stop orders may also target disposed actors. No removal notification clears the stale pair. The smallest indicated correction guards loading/usability, classifies stale/unloaded pairs once, orders only individually usable actors, removes the cached invalid set, and safely releases survivors. Treat this as evidence, not permission to broaden the task.

## Scope constraints

- Do not read the full task sheet, coordinator state, another worker state, or task history.
- Do not add architecture, behavior, policy, acceptance rules, or tests beyond the task and supplied facts.
- Do not modify Red Alert, Dune 2000, or Tiberian Sun.
- Keep raw game logs, replays, saves, build output, and local worktrees out of Git; preserve concise evidence and paths in the task report instead.

## Completed cycle handoff

- Implemented lifecycle pruning before every HeavyDrop phase, independently safe loading/boarding checks, and usable-actor order guards.
- Invalid or unloaded pairs now release their individual mission ledger entries; surviving ground passengers are unreserved, stopped, and restored to ordinary squad ownership.
- Added focused continuation/reservation regressions and a generator for ignored full-engine lifecycle scenarios.
- Verification: solution build passed; all 715 `OpenRA.Test` tests passed.
- Full-engine evidence: carrier invalidation passed to tick 2500 and continued with nine carriers; mirrored passenger invalidation passed to tick 1800 and continued with nine; eight-loaded timeout passed to tick 1800 after releasing two unassembled pairs; shipped Empire Earth4 two-Brutalis-versus-one-VIKI passed to tick 12000.
- Evidence and exact paths are recorded in `REPORT.md`. No raw artifacts were added to Git.
