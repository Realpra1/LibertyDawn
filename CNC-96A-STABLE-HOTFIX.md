# CNC-96A stable stealth lifecycle hotfix

Authority: [.agents/references/STEALTH-TANK-LIFECYCLE.md](.agents/references/STEALTH-TANK-LIFECYCLE.md)

## Goal

Ship a stable, simple, modular stealth-squad release candidate. Each lifecycle
phase exclusively owns its activity until it explicitly yields. Strategic work
uses the cache; local combat uses current actor positions and the standard threat
calculator. Preserve established balance and build-order behavior.

## Required behavior

- Every AI that owns eligible stealth tanks uses the same configuration-driven
  lifecycle. Iron Reaper behaves like VIKI against Brutalis; Chem Tanks use the
  same code with their own configuration. There is no competing legacy owner.
- Unassigned tanks join a squad without a squad-size cap. Reinforcements do not
  affect the active center until they reach the formation.
- Target acquisition uses the bounded strategic-cache search, ten options when
  available, the incumbent, and stable `(3 * squad center + corner) / 4` scan
  origins. Corner bias affects discovery only. Targets need not be exclusive.
- Phase 4 applies the value half, threat half, then least-threat-closest choice.
  Prefer cells worth at least one Harvester (`5000 * 1100`), but fall back to
  lower-value cells if necessary. This target floor never affects routing.
- Approach uses cached A*. Retain an engine move while it is active instead of
  reissuing waypoints because the actor temporarily appears off the strategic
  line. Obelisks are passable while cloaked; detectors remain route dangers.
- Local combat is live-only. Do not use the strategic cache for local safety,
  positions, crush, Kite, or mass attack. Use current positions and the standard
  threat calculator, including planned-decloak/current-range overrides.
- Cache only the bounded local actor roster between spatial scans. Re-read live
  position, health, detection, weapons, threat, and reachability on every combat
  decision. Refresh the roster every 50 ticks normally and multiply that interval
  without a separate cap when the master increases its adaptive planning factor;
  refresh immediately for a new mission or substantial squad
  movement. The module throttles and recovers continuously; only the master
  failsafe may disable it and transfer ownership to aggressive fallback.
- UndefendedAttack uses configured priorities, retains and finishes its target,
  retries a completed engine activity, and yields to defended combat if a shared
  target dies while nearby defenders remain.
- Defended combat tries safe Kite actions before Crush. Kite actual threats or
  economic targets above the configured floor, from a safe current/live firing
  cell. Crush current live infantry only when detector-safe.
- Kite issues one shared squad action. A retained engine move must still carry a
  fresh approved live-safety result; an invalid result may not silently strand
  the active owner.
- MassAttack starts only above crossover 2, targets the highest live threat, and
  continues until completion or crossover reaches 1. Otherwise recalculate.
- Two or more squads that independently exhaust safe local actions in the same
  target province may commit simultaneously to one provincial MassAttack. Squad
  membership is never merged: each squad clears its temporary coordination when
  its MassAttack yields and resumes independently. Squads still making safe
  progress do not veto or get pulled into that commitment, while a later blocked
  squad may join an active commitment in that province.
- Flee is one simple cached safe route roughly two strategic cells outward and
  immediately reconsiders better lifecycle work. It is not a default phase.
- Damaged tanks use a safe repair route; if none exists, resume the fight.
- Save allocations only. After load, restart each squad at TargetAcquisition.
- Visceroids have priority `-1`; SAM sites have priority `1` and zero threat.
- Keep phase owners short and independently testable: under 400 lines where
  practical, and every supporting class under 500 lines.

## Permanent diagnostic output

- no squad stationary for 30 seconds;
- real per-squad kill cadence, diagnostic target one kill per 45 seconds;
- no Obelisk-attributed stealth-tank death;
- killed value per minute per tank over actual tank lifetime;
- comparable damage-adjusted efficiency: killed-value rate divided by average
  damage per tank.

Cadence and efficiency are review signals, never behavior inputs or target
filters. Explained terminal tails after finite-force exhaustion or kill stealing
are acceptable; active unexplained failures are not.

## Maintained validation

1. Unit-test every owner and explicit handoff.
2. Pass `mods/cnc/maps/StankChallenge.oramap`: four two-tank VIKI squads against
   a no-resource Brutalis force, including a dense compound behind a two-cell wall
   with a required central structure. Ordinary low-priority fallback must punch
   the wall after reachable Kite/Crush opportunities are exhausted.
3. Run three full natural Empire Earth games as VIKI versus two Brutalis players.
   Use at least five when variance makes a comparison unclear. Never use Skynet
   as the normal covert-behavior test.
4. Preserve the permanent watchdogs and keep raw logs/replays/build artifacts out
   of git except an explicitly requested reviewed replay.
5. Ship both maintained scenarios as visible CNC maps:
   `mods/cnc/maps/StankChallenge.oramap` and
   `mods/cnc/maps/StankChallenge2.oramap`.
6. A squad-manager failsafe transition must release every stealth tank to a
   working aggressive `AttackMove` fallback. Idle or completed orders are
   reconsidered, so a disabled module can never leave surviving tanks stopped.
   The F8 overlay may clear stale advanced-squad labels after the handoff, but
   loss of the overlay must not mean loss of unit ownership or orders.

## Current release-candidate evidence

- The reviewed `playtest-20260904` replay records all four F8 squad entries being
  cleared at ticks `1509-1512`. Surviving tanks later become idle and are not
  reclaimed. Retained automation independently records the same controller
  disabling `SquadManagerBotModule`, with `assign_roles` dominating its sample.
- Normal-speed lag now requires the advanced modules themselves to exceed their
  configured share of one real-time simulation budget before throttling/shedding.
  Rendering, overlay, or unrelated simulation lag cannot alone disable squads.
- A disabled squad manager releases all retained actors to aggressive fallback,
  and fallback renews a completed/abandoned order whenever an actor becomes idle.
  A forced runtime transition retained all eight tanks and renewed fallback orders
  through tick `1500` without a stationary failure.
- Exact final binary: StankChallenge won naturally at tick `15643`; Challenge 2
  won naturally at tick `23604`. Neither run disabled the manager or stalled.
  Challenge 2 diagnosed one Obelisk-attributed loss. A concurrent Challenge 2
  run also recorded one win at `18861`, while a second timing-variable run hit its
  `40000`-tick cap with two idle tanks; keep this as a lifecycle review signal.
- Five full Empire Earth games, VIKI versus two allied Brutalis players: `5/5`
  VIKI wins at ticks `22022`, `23285`, `28453`, `28603`, and `48261` (median
  `28453`). No manager disable, stationary, cadence, exception, or desync signal.
  Primary efficiency mean/median: `1137.39`/`668.57`; damage-adjusted mean/median:
  `0.13427`/`0.08982`. Five Obelisk-loss diagnostics occurred across the batch.
- CPU profile on the identical paced Challenge 1 fixture reduced Kite from
  `5386.164 ms / 755 calls` to `5302.569 ms / 735 calls`; candidate order and
  behavior are unchanged because duplicates are removed before passability checks.
- Adaptive local-actor roster caching reduced the same paced Kite profile from
  `5302.569` to `4055.701 ms` (`23.5%`) and the complete VIKI squad manager from
  `6516.470` to `5231.219 ms` (`19.7%`). Tick p95 fell from `35` to `26 ms` and
  50+ ms samples from `19` to `6`; 263 spatial refreshes served 628 cache hits.
- Cache behavior validation: both challenge maps completed naturally without a
  stall, disable, or Obelisk loss (`13506` and `23630`). Three full concurrent
  Empire Earth games were `3/3` VIKI wins at ticks `19992`, `20633`, and `22788`,
  with no permanent-watchdog failure. Primary efficiency mean/median were
  `1911.53`/`1949.18`; damage-adjusted mean/median were `0.29452`/`0.28301`.
- With the requested 50-tick base and uncapped adaptive multiplier, Challenge 2
  completed cleanly at tick `19077`. Three natural games were `3/3` VIKI wins at
  ticks `22951`, `23351`, and `25959`; no stall, cadence, disable, exception, or
  desync signal occurred, while two Obelisk losses remain diagnostic signals.
  Primary efficiency mean/median were `1939.43`/`1785.76`; damage-adjusted
  mean/median were `0.26720`/`0.21463`.
- The 50-tick paced profile made 163 spatial refreshes and 706 cache hits. Run
  variance increased Kite calls from 721 to 785, but total manager/Kite CPU
  remained `15.4%`/`14.2%` below uncached (`5510.537`/`4551.086 ms`).
- Validation: focused failsafe/ownership `29/29`; cache/throttle policy `5/5`;
  scenario packaging `5/5`; complete .NET `971/971`; `make check`, `make check-scripts`, and
  `make test MOD=cnc` pass. Both challenge maps are packaged and validated.

## Release state

- **Ready for human retest, not final acceptance.** The uploaded replay's
  failsafe/fallback defect is repaired and reproduced by a forced transition
  test. Human testing should confirm F8 ownership remains active normally and,
  if a genuine overload sheds the module, tanks continue aggressive fallback.
  Challenge 2 timing variance and Obelisk-loss diagnostics remain review signals;
  they do not invalidate this bounded crash/handoff hotfix.
- Kite now finishes a retained live target,
  yields to phases 3-4 when it dies, and cannot reuse that target's movement plan.
- A fresh Kite phase may try the next safe valid target. Once it selects one, it
  cannot silently abandon it for another target's move; an unsafe retained fight
  must explicitly use crossover and MassAttack/RecalculateFlee.
- Low-priority walls are excluded from normal Kite selection and remain reachable
  fallback only. UndefendedAttack keeps configured priority/value/remaining-health
  selection.
- Lone crushable Riflemen yield to Crush rather than Flee. Exposed unsafe squads
  seek a live threat-approved firing cell; Flee immediately yields when live combat
  is safe again and otherwise uses one cached least-danger route.
- Provincial coordination is a temporary synchronized commitment, not a squad
  merge. Participants retain their membership, MassAttack together, then resume
  independent lifecycle work as soon as their own MassAttack yields.
