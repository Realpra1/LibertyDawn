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

## Current release-candidate evidence

- Focused local-combat/flee/threat suite: `112/112` passing.
- Complete .NET suite: `925/925` passing. `make test MOD=cnc` passes with
  Release compilation, CNC rules, and every packaged CNC map including
  StankChallenge.
- Dense StankChallenge: VIKI win at tick `24967`, `600.34` ticks/second; no
  stationary, Obelisk-death, rejected-result, or failsafe-disable signal.
- Three concurrent full natural games: `3/3` completed at ticks `19107`, `30007`,
  and `40743`; no stationary, Obelisk-death, rejected-result, or failsafe-disable
  signal. Active terminal squads passed cadence.
- Natural damage-adjusted efficiency: `0.465`, `0.066`, `0.201`; median `0.201`,
  mean `0.244`. The low run was a long 91-stealth-tank war and remains a variance
  signal, not a hidden acceptance threshold.

## Remaining release work

- Complete the final diff review, update PR 144, and publish the exact evidence.
  Do not make further behavior changes unless review exposes a clear bug.
