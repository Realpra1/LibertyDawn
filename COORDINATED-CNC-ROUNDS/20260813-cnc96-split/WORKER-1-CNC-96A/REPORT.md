# CNC-96A worker report

## Status

Cycle 1 implemented and checked; two distinct full-engine games and their matched
exact-base controls passed. Proposed status is `First iteration - testing`, stopped
at the required user manual-policy gate. The saturated case demonstrates a large
specialist-tail reduction without profile inactivity, but the transition case did
not demonstrate Stealth Tank recovery onto the scripted late safe target.

## Task boundary

Compare Stealth/Chemical specialist CPU, planning, and order behavior directly
with AirSquad; simplify it toward AirSquad's bounded architecture while preserving
Stealth-specific targets, threats, ground movement, and harassment purpose. Base
work on `0c9a5c187d6bd3c354921855f19a4fb3590d6f06`. Balance is frozen and the
discarded CNC-96 scan-local snapshot must not return.

## Pre-implementation evidence

- CNC-96's two-trait removal moved the repeated tick-76 boundary substantially,
  nominating the Stealth/Chemical module boundary but not proving a valid fix.
- Pressured 2,200-tick runs attributed 8.9–11.1 seconds of aggregate specialist
  work and individual spans above 1.25 seconds to that shared boundary.
- Source inspection finds repeated full enemy/threat enumeration, per-group
  candidate-by-threat work, per-unit exact hazard pathfinding, and 75-tick order
  refreshes. AirSquad instead uses a shared bounded strategic cache, a separate
  cheap local safety cadence, stable target/route state, and one-shot route batches.
- The prior `UseScanLocalThreatSnapshot` candidate changed decisions and workload;
  commit `0c9a5c1` removed it. It is rejected evidence, not an implementation seed.

## Cycle 1 design contract

Manual policy amendment adopted during cycle 1: `stealth-tank` and `chemical`
must use the exact same squad/control code. They remain two configured instances
of `StealthTankSquadBotModule`; there is no profile-specific planner, lifecycle,
routing, target-selection, or order implementation. All role differences flow
only through the existing `StealthTankSquadBotModuleInfo` configuration inputs.
Per-instance targets/plans remain isolated state so one profile cannot consume the
other profile's judgments; isolation is not a separate control path.

Measured-work hypothesis: both configured specialist profiles currently rebuild the
same enemy/threat facts on their aligned 75-tick scans, while every live group also
rebuilds and requeues the same hazard route whenever its target is unchanged. A
same-world-tick factual view should remove the duplicate cross-profile enumeration
without caching positions, target types, profile scores, or risk judgments. A
retained group plan should remove repeated path searches and equivalent orders while
the squad is visibly progressing, with the existing 75-tick scan/retry boundary
remaining the maximum response time.

| Event | Classification | Required response |
|---|---|---|
| Target dies, leaves the world, changes relationship, or materially moves cell | Plan invalidation | Drop/reselect immediately on the next existing 75-tick scan; a moved retained target gets a fresh route/order. |
| Newly local detector or active attacker intersects the retained route | Cheap local override, then plan invalidation | Validate retained route cells against the current same-tick factual threats and stop/replan on the current scan. |
| Blue Tiberium or pending explosion intersects the retained route | Cheap local override, then plan invalidation | Validate retained route cells with live resource authority and stop/replan on the current scan. |
| Route search fails or no safe endpoint exists | Bounded retry | Withhold/stop the affected unit and retry after the existing order interval; never direct-attack through the hazard. |
| Previously unsafe/blocked route reopens | Bounded retry | Lack of progress expires at the existing order interval and triggers a fresh path search. |
| Membership or reservation changes | Plan invalidation | Rebuild the affected group plan on the current scan; transport reservations retain priority. |
| Squad makes no positional or target-damage progress | Bounded retry | Replan once the existing order interval elapses; progress retains the plan without equivalent orders. |
| Target and membership remain stable, route stays safe, and movement/damage progresses | Retain | Keep the target/route and issue no equivalent order batch. |

The shared view owns only actor references plus ground-weapon range, detector range,
economic value, and current engagement fact for one simulation tick. Actor position,
enabled target types, profile filtering/scoring, ignored threat types, kite decisions,
and route/resource safety remain live and profile-local.

## Deferred observations

None. Any unrelated discovery belongs here rather than in the task contract.

## Specification policy consultation

The isolated Sol-high consultation at
`SPEC-POLICY-REVIEW/POLICY-REVIEW.md` returned `mostly sensible` with high
confidence. It endorsed the AirSquad-shaped separation of bounded strategy,
cheap live safety, stable plans, and state-change-driven orders. Its primary
constraint is adopted: caching may delay reconsideration only while a plan stays
safe and useful; it must not change eligibility, priorities, threat/hazard meaning,
cadence, or bounds when reconsideration occurs, and local safety plus meaningful
invalidations must bypass stale state. Identity/live-safety, blocked recovery, and
matched saturation tests were added to the durable assignment. No recommendation
was rejected; shared extraction is deferred unless measurement proves the local
lifecycle still duplicates material work. The bounded-staleness principle was
validated and atomically merged into the shared policy scratchpad without losing
a concurrent valid update.

## Cycle evidence

### Cycle 1 implementation

- One `StealthTankSquadBotModule` implementation still owns both configured
  profiles. Reflection/parity tests fail if a profile-specific subclass appears;
  the lifecycle test is executed for both `stealth-tank` and `chemical` labels.
- The first configured instance builds one same-player, same-world-tick factual
  enemy/threat view and the second consumes it. Cached content is actor references
  plus weapon/detector/value/engagement facts only; target scoring, enabled target
  types, profile threat interpretation, positions, hazards, and path decisions stay
  live through the one shared control path.
- Each group retains its selected target and issued route while it makes positional
  or target-HP progress. Target change/death, membership change, target movement,
  newly unsafe retained route, or one unchanged order interval without progress
  invalidates the plan. A new plan reuses the existing scoring, danger, reachability,
  hazard, group, candidate, and cadence rules and issues one replacement order batch.
- Default-off bounded scan diagnostics report factual-view build/hit, plan
  retention/invalidation, exact path searches, and queued orders. There is no
  profile-specific branch in planning, lifecycle, routing, target selection, or
  order execution, and `mods/cnc/rules/ai.yaml` was not changed.

### Checks

- Protected `make all`: passed, `0` warnings and `0` errors.
- Focused `StealthTankSquadPolicyTest`: `42` passed, `0` failed. New cases cover
  same-tick factual-view freshness, invalidation priority/boundary behavior, absence
  of a profile-specific control subclass, and identical lifecycle calls for both
  configured profile labels.
- `git diff --check`: passed before publication.

### Scenario A — saturated open comparison

Hypothesis: a shared view could merely move work, while retained plans could remove
orders by making one profile inert. The custom map
`cnc96a-scenario-a.oramap` (`sha256 ac01a65eb060d78f41293f21a6081b9c3488e2be7370e4d50fe539f45d73036b`)
used seed `960101`, two ordinary IronReapers at opposite starts, all modules,
paced rendering, a 900-world-tick bound, and 331 mobile actors plus structures per
side, including 20 `stnk`, 20 `ctnk`, and 16 aircraft. Both exact-base and changed
runs passed without fatal, unhandled-exception, or desync patterns at tick 900;
wall times were `78.116s` and `57.121s`.

At tick 1, both builds made each Stealth group and each Chemical group select the
same opposing isolated `nuk2`. Stealth used Blue-Tiberium-aware routes; Chemical
issued direct attacks. Ordinary AirSquad also selected the `nuk2` and remained
active, later selecting ordinary mobile/harvester targets. In the changed run the
two Stealth plans and one Chemical plan per player were retained on ticks
76/151/226/301 with `0` replacement paths and `0` replacement orders. The opening
changed scan performed eight Stealth path searches per player and queued 107/82
Stealth orders plus one Chemical order per player. The exact-base log rebuilt
shrinking hazard routes at aligned scans and its module accounting recorded
483/295 Stealth orders; the changed run recorded 107/82. From tick 376 one enemy
view dropped from 333 to 332 and the specialists later reported all 48 candidates
dangerous; target destruction is plausible but not proved by the supplied
telemetry, so damage/kill preservation remains an evidence gap.

| Metric | exact base | changed | factual comparison |
|---|---:|---:|---:|
| combined Stealth CPU | 24177.159 ms | 5675.048 ms | -76.5% |
| combined Stealth queued orders | 778 | 189 | -75.7% |
| combined Chemical CPU | 94.288 ms | 78.507 ms | -16.7% |
| combined Chemical queued orders | 11 | 2 | -81.8% |
| tick mean / p50 / p95 / p99 | 55.810 / 19 / 58 / 414 ms | 32.839 / 18 / 50 / 196 ms | mean -41.2%; p99 -52.7% |
| tick maximum / >=50ms freezes | 5788.153 ms / 65 | 5808.107 ms / 45 | startup maximum unchanged; freezes -30.8% |
| render p50 / p95 / p99 / max | 26 / 31 / 37 / 101.142 ms | 25 / 31 / 36 / 104.019 ms | comparable |
| present p50 / p95 / p99 / max | 1 / 1 / 1 / 23.617 ms | 1 / 1 / 1 / 27.459 ms | no >=50ms event |

Ordinary `SquadManagerBotModule` is not direct AirSquad CPU attribution. Its
combined CPU changed from `1366.867ms` to `1459.393ms` (+6.8%) and its queued
orders from 1000 to 1921; player-level downstream battles diverged. This does not
erase the separately attributed specialist improvement, but it prevents a claim
that all ordinary-manager workload was unchanged and must be repeated in later
controls with direct Air attribution.

- Exact-base artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/scenario-a-base-final3/scenario-a-exact-base`
- Changed artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/scenario-a-new-final3/scenario-a-cycle1`
- Fresh Luna factual narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/scenario-a/NARRATIVE.md`

### Scenario B — scripted Archipelago transitions

Hypothesis: retained state or same-tick facts could become stale when targets,
danger, topology, and membership change. The custom map
`cnc96a-scenario-b.oramap` (`sha256 bfaf97b2c355c00418d85fe6d6f32470f71c81c0b1e536e5c11bce5ac3811ab8`)
used seed `960102`, two ordinary IronReapers, all modules, paced rendering, 104
mobile actors plus structures per side, and a 750-world-tick bound. Exact-base and
changed runs passed at tick 750 in `35.074s` and `35.078s` with no fatal,
unhandled-exception, or desync patterns.

Both runs acknowledged deterministic events: targets created at tick 0 and moved
at 100; detectors/attackers created and the route closed at 175; targets destroyed
at 275; danger removed and route reopened at 375; one Stealth member killed and
replaced at 450; late safe targets created at 550; and final tick-700 counts of
12 `stnk` plus 12 `ctnk` for each player. In the changed run the Stealth profile
made no target plan, path search, or order at any diagnostic scan, while Chemical
selected and attacked `vice` targets and its second profile access was always a
same-tick factual-view hit. This establishes shared-path/profile activity and a
safe absence of stale Stealth orders, but it does **not** establish Stealth recovery
after route reopening or late-target creation; no artifact proves whether the
late target was safely reachable under current Stealth policy. Pending-explosion
and transport-reservation recovery also remain untested.

Ordinary AirSquad remained active across the script: its 12-aircraft formations
attacked the scripted vehicle targets, invalidated targets after transitions, held
empty Orcas for local reload safety, and one later state recorded 11 aircraft after
a loss. Logs establish its state transitions and orders, not target kills.

| Metric | exact base | changed | factual comparison |
|---|---:|---:|---:|
| combined Stealth CPU / orders | 86.351 ms / 0 | 83.200 ms / 0 | CPU -3.6%; both held |
| combined Chemical CPU / orders | 27.893 ms / 18 | 23.266 ms / 17 | CPU -16.6%; active both |
| tick mean / p50 / p95 / p99 | 9.265 / 7 / 16 / 29 ms | 9.603 / 7 / 16 / 32 ms | mean +3.6%; p99 +3 ms |
| tick maximum / >=50ms freezes | 1221.706 ms / 4 | 1289.772 ms / 4 | comparable startup tail |
| render p50 / p95 / p99 / max | 25 / 33 / 41 / 100.646 ms | 25 / 32 / 38 / 108.053 ms | tail lower except max |
| present p50 / p95 / p99 / max | 1 / 1 / 1 / 24.446 ms | 1 / 1 / 1 / 32.715 ms | no >=50ms event |

- Exact-base artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/scenario-b-base-retry1/scenario-b-exact-base`
- Changed artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/scenario-b-new-retry1/scenario-b-cycle1`
- Fresh Luna factual narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/scenario-b/NARRATIVE.md`

### Direct AirSquad comparison finding

The useful architectural match is supported behaviorally: AirSquad selected a
strategic target, retained formation/route state, and issued bounded route/attack
batches, while the changed specialist path retained stable plans and eliminated
equivalent 75-tick route/order batches. The specialist implementation does not copy
air routing or safety; it retains ground locomotor, detector/armed-threat, Blue
Tiberium/pending-hazard, reachable-approach, and per-profile scoring decisions.
There is no direct per-Air CPU identity at this base, so aggregate SquadManager CPU
is explicitly not treated as direct AirSquad performance evidence.

## Manual policy-review packet

No automated Match Policy Reviewer was launched. The user must decide all three
items before cycle 2:

1. Is the shared lifecycle policy acceptable: retain a safe target/route while
   distance or target HP progresses, and replan only on target/member/movement/
   live-route-safety/no-progress invalidation at the unchanged 75-tick scan?
2. Does Scenario B's zero-order Stealth hold count as a valid safe result under
   uncertain reachability, or must cycle 2 treat it as passive idling and build a
   guaranteed reachable late opening with explicit target/damage/recovery markers?
3. Is Scenario A's large attributed CPU/order/tick-tail improvement sufficient for
   cycle-1 direction despite missing direct damage/loss telemetry and divergent
   ordinary SquadManager orders, or should cycle 2 prioritize explicit damage,
   survival, and direct Air planning/order attribution before any further change?

## Known risks / next evidence if authorized

- Explicit target HP/damage, specialist loss, arrival, and kill markers are absent;
  target disappearance cannot be promoted to a proved kill.
- Scenario B does not discriminate safe waiting from unreachable-target passivity.
- Only IronReaper was exercised; the other nine configured ordinary bot
  personalities, transport/crate reservation handoff, pending explosions, blocked
  recovery, save/load applicability, repeated-control variability, headless/paced
  agreement, and direct Air timing remain required by the multi-cycle acceptance
  ladder.
- The historical `8024fd2` tree was not mislabelled or backported; its known modern
  launcher/IronReaper incompatibility remains inherited evidence rather than a
  matched cycle-1 control.
