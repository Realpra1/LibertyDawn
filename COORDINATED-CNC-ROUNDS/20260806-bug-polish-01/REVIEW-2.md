# Review: CNC-39A PR #80

- Verdict: **blocked**
- PR: `#80` (`agent/round-20260806-cnc39a-engineer-commando` -> `agent/cnc38-early-viki-infantry-rush`)
- Reviewed head: `464dd7ad7b76a8833927eff4c415e046d43c9ef2`
- Product commit: `0c6accf17aa89be8a6f0a910727a1b289e9b30b0`
- Contract base: `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
- GitHub state: open draft, mergeable/clean, no configured checks reported

## Required fix

Persist and deterministically restore `CaptureManagerBotModule`'s scan timers,
active capture/demolition assignments, deferred targets, progress timestamps/state,
and the shared purpose reservations across a game save. Add a focused restoration
check and a fresh full-engine save/load regression that saves while one purpose has
a travelling incumbent and the opposite specialist is idle, then proves after
reload that the incumbent still excludes the opposite purpose, choices remain
stable, and normal release/reselection still works.

## Findings

### High — Active purpose reservations are lost at every game-save load

- Locations: `OpenRA.Mods.Common/Traits/BotModules/CaptureManagerBotModule.cs:65`,
  `:129-145`, `:172-196`, `:228-235`, `:467-489`; surrounding engine boundary
  `OpenRA.Mods.Common/Traits/Player/ModularBot.cs:84-101`.
- Failure mechanism: the module owns all new reservation correctness in transient
  dictionaries (`activeCapturers`, `activeDemolitionUnits`, `deferredTargets`, and
  `targetReservations`) plus randomized scan timers, but it implements only
  `IBotTick`, not `IGameSaveTraitData`. During save restoration `ModularBot`
  explicitly does not tick, so replayed orders rebuild actor activities while this
  module remains at its newly constructed empty state. When play resumes, a moving
  Engineer is not in `activeCapturers` and is excluded by the capturer query, or a
  moving Commando is absent from `activeDemolitionUnits`; an idle opposite-purpose
  specialist can therefore see the incumbent target as unreserved and receive the
  conflicting order. Scan cadence, progress retirement, and deferred-target state
  also restart from unrelated randomized/default values.
- Affected clauses: one shared deterministic reservation authority; no concurrent
  capture/demolition purpose; incumbent retention; deterministic save/load and
  replay behavior; save/load differential must reject duplicate claims.
- Existing evidence does not exercise this. The travel save occurs after the first
  target was already captured and its C4 canceled, and the post-plant save tests the
  shared `DemolitionSafety` action against a different allied bot, not restoration
  of a same-bot cross-purpose reservation. Its factual narrative explicitly notes
  capture-manager lines present in the fresh run but absent after reload.
- Smallest safe correction: implement deterministic game-save trait data for the
  owning module and rebuild reservations by stable ActorID after validating live
  actors/targets and purpose/pair cardinality; restore timers and progress/defer
  state rather than inferring success from continued activities. Exercise both
  capture-incumbent/idle-Commando and demolition-incumbent/idle-Engineer directions.

### High — Mandatory strategic and contention acceptance remains unexercised

- Locations: `COORDINATED-CNC-ROUNDS/20260806-bug-polish-01/WORKER-2-CNC-39A/STATE.md:401-432`,
  `:473-520`; `REPORT.md:7`, `:116-122`, `:148-159`.
- Failure mechanism: the contract requires a repeated matched defended
  no-alternate comparison, ordinary connected and island transport contention,
  a natural-production match that actually exercises both specialists, three clean
  adversarial scenarios after the final fix, and a final combined run containing
  real defenders, repair, and a transport reservation. The report concedes that
  no ordinary transport takeover was exercised, the natural run used pre-staged
  specialists, and the combined run omitted defenders/repair/transport. The three
  scenarios labeled clean are ownership cancellation, scripted-scope compatibility,
  and another combined basic-path run; they do not substitute for the required
  frontline-under-fire or missing-assets/transport adversaries.
- Affected clauses: defended no-alternate pressure comparison; contention/recovery;
  natural ordinary match; three clean adversarial cases; final regression.
- Smallest safe correction: after the required product fix, run the materially
  affected acceptance plus the missing specified adversarial/final scenarios with
  fresh Terra narratives and policy reviews before claiming the PR is releasable.

### Medium — Relationship finality is not uniformly latched by every delayed demolition consumer

- Locations: `OpenRA.Mods.Common/Traits/Demolishable.cs:78-97`,
  `OpenRA.Mods.Common/Traits/Buildings/BridgeHut.cs:176-183`, and
  `OpenRA.Mods.Common/Traits/Buildings/LegacyBridgeHut.cs:57-64`.
- Failure mechanism: normal `Demolishable` polls safety every enabled tick, but it
  skips all safety checks while its conditional trait is disabled. Both bridge-hut
  implementations check safety only once when their delayed action finally fires.
  Consequently, a planted autonomous action can observe an allowed relationship at
  plant, become allied while polling is disabled/absent, return hostile before the
  final callback, and then detonate because `DemolitionSafety` never observed and
  latched the intervening invalid relationship. This contradicts the reported claim
  that later hostility cannot reauthorize an obsolete charge and the contract's
  explicit bridge-consumer/finality boundary.
- Affected clauses: relationship changes back to hostile must require a fresh
  assignment; target trait disablement invalidates work; all shared
  `IDemolishable` consumers must preserve autonomous safety through detonation.
- Smallest safe correction: ensure each pending autonomous action observes and
  permanently latches invalidation throughout its delay, including disabled-trait
  and bridge paths, then cover an ally-to-hostile flip after plant without changing
  script/manual semantics.

### Medium — Focused tests cover the data helpers, not several required lifecycle boundaries

- Location: `OpenRA.Test/OpenRA.Mods.Common/CaptureTargetingTest.cs:62-123`.
- Failure mechanism: the five new tests cover reservation cardinality/arrival
  order/release and pure `DemolitionSafety` relationship flags. They do not execute
  valid-incumbent retention, simultaneous selector precedence, deterministic
  alternate selection under enumeration perturbation, each assignment invalidation
  class, a queued C4 behind another activity, the actual plant/final-damage boundary,
  or manual/forced/script provenance. Passing 9/9 therefore cannot guard the
  material lifecycle failures requested by the contract.
- Affected clauses: focused checks and instrumentation requirements at worker-state
  lines 349-355; material failure/recovery coverage required by the reviewer role.
- Smallest safe correction: add focused activity/module/interface coverage at the
  real ownership boundaries, using the game regressions for behavior that cannot be
  tested faithfully without the full engine.

## Evidence assessment

The core fresh-game behavior is credible: the matched simultaneous fixture shows
the control collision and changed disjoint capture/demolition outcomes; the planted
race shows control destruction versus changed disarm/survival; the combined run
shows useful hostile follow-up. Required local build/static/unit commands are
reported green, `git diff --check` is clean, and no unrelated product change was
found in the task commit. Fresh Terra factual narratives and policy reviews exist
for the materially judged batches inspected, including the final combined,
ownership ladder, persistence, post-plant persistence, and scripted-scope runs.
Those strengths do not close the deterministic persistence defect or the explicitly
missing acceptance gates above.
