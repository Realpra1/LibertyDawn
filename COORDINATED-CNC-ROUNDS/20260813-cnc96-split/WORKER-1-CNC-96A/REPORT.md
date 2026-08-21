# CNC-96A worker report

## Status

Cycles 1-11 implemented and checked. Cycle 10 proves detector-plus-armed Stop
suppresses new fire after already-committed missiles, and proves a damaged member
with no repair remains active/reserved through scripted death. Proposed status
remains `First iteration - testing`. Cycle 11 fixes Terra's shared-view blocker,
proves non-owner Chemical full-health repair, and proves lone-survivor/replacement
specialist ownership. Explicit displaced route/order and combat rejoin remain
unproved at the exact two-game cap.

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
Cycle 1 did not have direct per-Air CPU identity, so its aggregate SquadManager CPU
is explicitly not treated as direct AirSquad performance evidence. Cycle 2 adds
benchmark-only direct identities and order deltas around each Air state-machine and
local-safety call; these do not change non-benchmark behavior.

## Cycle 1 manual decision

The user approved retaining a safe progressing specialist target/route until
target, membership, movement, route-safety, or no-progress invalidates it. The user
also ruled cycle 1's zero-order Stealth result inconclusive rather than acceptable
idling and required a guaranteed reachable opening with actual orders, useful
attacks, damage/survival/recovery, and direct Air CPU/order attribution. A later
amendment prohibited substantive per-tick specialist work: per-tick dispatch may
only do O(1) countdown/state work; local safety may run no faster than 25 ticks and
expensive planning must remain on the unchanged 75-tick cadence.

## Cycle 2 evidence

### Implementation and cadence check

- `IBotTick` still dispatches each specialist module instance every engine tick,
  but its only enabled hot-path operation before return is an O(1) integer
  decrement/comparison. A focused test observes substantive scans exactly at ticks
  1, 76, and 151 in a 225-tick sequence.
- Enemy enumeration, factual threat derivation, group rebalance, target scoring,
  local retained-route safety, pathfinding, and order issuance all remain below
  that gate. No separate 25-tick specialist safety scan was added; current safety
  remains part of the unchanged 75-tick strategic scan.
- Benchmark-only attribution now times Air state-machine strategy and local-safety
  calls separately and records their added queued-order counts. A bounded debug
  counter records observed target HP reduction between specialist scans.
- `stealth-tank` and `chemical` still run one shared implementation. Their distinct
  actor/target/threat priorities remain YAML configuration only; no gameplay YAML
  or cadence value changed.

Checks on final source: protected
`make test TESTS=OpenRA.Test/OpenRA.Mods.Common/StealthTankSquadPolicyTest.cs`
passed CNC compilation and map lint with zero warnings/errors. The focused policy
suite passed `43/43`; `git diff --check` passed. A direct filtered test run also
passed `43/43` and emitted one unrelated existing analyzer warning from
`AircraftHuskSpawnEligibilityTest.cs`.

### Scenario C — saturated direct Air comparison

The headless-MAX `cnc96a-scenario-a.oramap` run used seed `960202`, SkyNet/Nod
versus IronReaper/GDI, all modules, more than 300 mobile actors plus structures per
side, 20 Stealth Tanks, 20 Chemical Tanks, and 16 aircraft per side. It reached
tick 900 in `32.027s` without fatal, unhandled-exception, or desync patterns.
Tick mean/p50/p95/p99/max were `31.606/11/57/227/6357.828ms`, with 55 ticks at or
above 50ms.

The exact scan trace had 12 substantive calls per player/profile at ticks
`1,76,151,226,301,376,451,526,601,676,751,826`. On every player/tick the first
`stealth-tank` instance built the shared enemy/threat facts and the following
`chemical` instance hit that same-world-tick view. Stealth recorded 24 view builds,
17 retained plans, 5 invalidations, 20 exact path searches, and 245 queued orders;
Chemical recorded 24 hits, 9 retentions, 2 invalidations, no exact path search, and
2 orders. Both remained active before all 48 late candidates became dangerous.

| Direct identity | denominator | CPU total / average | queued orders |
|---|---:|---:|---:|
| SkyNet Stealth | 900 dispatches; 12 substantive scans; 3 group updates/scan | 2928.672ms; ~244.056ms/scan; ~3.254ms/game tick | 110 |
| IronReaper Stealth | 900 dispatches; 12 substantive scans; 3 group updates/scan | 4861.478ms; ~405.123ms/scan; ~5.402ms/game tick | 135 |
| both Chemical profiles | 1800 dispatches; 24 substantive scans; 1 group/profile | 67.161ms; ~2.798ms/scan; ~0.075ms/game tick | 2 |
| SkyNet Apache+Orca strategy | 4 squads, 72 state-machine calls | 1743.152ms; ~24.211ms/call; ~1.937ms/game tick | 49 |
| SkyNet Apache+Orca local safety | 4 squads, 144 calls | 16.383ms; ~0.114ms/call; ~0.018ms/game tick | 0 |
| IronReaper Generic Air strategy | 1 squad, 12 state-machine calls | 165.449ms; ~13.787ms/call; ~0.184ms/game tick | 121 |

These denominators are intentionally not presented as equal. The performance
framework wraps each specialist `IBotTick`, so it records 900 samples per module
even though 888 are trivial early returns. Air instrumentation exists inside the
squad manager and records only actual per-squad strategy/safety calls. SkyNet has
four air squads and a 50-tick strategy/25-tick safety configuration; IronReaper has
one Generic air squad and defaults to 75-tick strategy with local safety disabled.
For a closer cadence comparison, the two Stealth profiles together spent
`7790.150ms` across 24 substantive module scans (~324.590ms/scan), whereas all five
Air squads spent `1908.601ms` across 84 strategy state-machine calls
(~22.721ms/call). Each Stealth scan invokes three configured group updates in this
fixture (~108.197ms/group-update if shared scan/view cost is merely amortized), while the Air number is one
squad call, so even that normalized ratio is architectural evidence, not a claim
that the operations are behaviorally identical.

The opening/tick-76 spikes are explained by invalidation work. At tick 1 each
Stealth module builds the full factual enemy/threat view, scores up to 48 candidates
for each live group, checks candidates against threats, and runs one exact ground
path per assigned specialist unit. Each visited route cell checks live resource
hazards and relevant threats, then the module queues spaced waypoints plus the
attack. SkyNet tick 1 performed 8 exact paths/110 orders; IronReaper did 8/94.
IronReaper tick 76 invalidated one plan and added 4 paths/41 orders. Those boundaries
own the multi-second specialist tails. Retained scans do rebuild factual facts and
rescore candidates/check route lookahead, but avoid exact path searches and orders.
Air's state machine instead works from a bounded coarse influence cache, stable
formation/route state, and one-shot route batches; its separate local safety is a
small bounded nearby scan. Ground locomotion, Blue-Tiberium/pending-explosion cells,
and threat-per-visited-cell validation are the main necessary specialist differences,
but their current exact-path implementation remains disproportionately expensive.

- Game: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/cycle2-saturation/cycle2-saturation`
- Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle2-saturation/NARRATIVE.md`

### Scenario D — guaranteed reachable action and recovery

The distinct headless-MAX reachable scenario used seed `960201`, SkyNet/Nod versus
IronReaper/GDI, all modules, 12 Stealth Tanks plus 12 Chemical Tanks per side, and
nearby scripted Creeps-owned hostile harvesters. It removed the opening targets,
replaced one Stealth member at tick 250, and created late reachable targets at tick
300. The valid game reached tick 750 in `6.005s` without fatal, unhandled-exception,
or desync patterns. Tick mean/p50/p95/p99/max were `3.998/2/7/24/1119.557ms`, with
three ticks at or above 50ms.

Both sides acquired the opening hostile harvesters at tick 76 and late harvesters
at tick 376, issued hazard-routed orders, and retained the plans after damage. The
sampled HP counters recorded 68,000 damage for SkyNet at tick 151 and 34,000 for
both sides at tick 451. Script events recorded first damage and all opening/late
targets dead by tick 500; both players still had 12 Stealth and 12 Chemical Tanks
at tick 700. Combined Stealth cost was `211.994ms/101 orders`; combined Chemical
cost was `8.980ms/20 orders`. This resolves cycle 1's zero-order evidence gap: its
targets were not enemies of the bots, while this fixture made the scripted targets
genuinely hostile and actionable. The harmless crush selections at tick 1 occurred
before the scripted harvesters existed for the bot's first scan; tick 76 was the
first configured scan able to observe them.

Two pre-world harness attempts and one invalid-slot attempt were rejected and do
not count as games; the harness was repaired before the two authorized valid runs.

- Game: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/cycle2-reachable-final/cycle2-reachable`
- Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle2-reachable/NARRATIVE.md`

## Cycle 3 evidence

### Air-shaped coarse routing

The user authorized the narrow design after cycle 2's phase attribution:

- Keep the existing 75-tick strategic scan and stable-plan invalidators. Ordinary
  per-tick dispatch performs only two O(1) countdown checks.
- Reuse `ThreatAwareRoutePlanner`, AirSquad's coarse A*/smoothing seam, but not
  AirSquad's private air-only target/influence cache. Each configured specialist
  trait owns a profile-isolated map; Air cache state and decisions remain separate.
- Build one 4-cell coarse grid per profile on demand and retain it for 125 ticks,
  matching Air's cache lifetime. The saturation grid is 51x51 (2,601 floats,
  ~10.4KB/profile); the reachable grid is 44x44 (1,936, ~7.7KB/profile).
- Select one reachable firing approach and one coarse route per formation; issue
  grouped movement/attack orders. `DomainIndex` rejects disconnected approaches,
  while the engine locomotor refines ordinary terrain, blockers, and Tiberium.
- Run engagement-only local safety every 25 ticks: stop/invalidate an actively
  fighting specialist exposed to a live detector or engaged-weapon envelope, and
  keep Stealth Tanks from attacking on/adjacent to configured Blue Tiberium.
- Keep the same Stealth/Chemical implementation and existing config-only actor,
  priority, ignored-threat, and capability differences.

No fine-cell threat raster, per-route threat/resource scan, or exact engine
`PathSearch` remains. `AirStateBase.AirInfluenceCache` was not reused because it is
private static state keyed by air profile/speed and contains air-only candidates,
ammo, anti-air, and utility policy. The shared route-planner seam provides safe
reuse without cross-profile contamination.

The cycle-3 Luna review found that a future nonzero pending-explosion avoidance
radius would otherwise lose its declared safety contract. Adopted correction:
when and only when that radius is configured, pending explosions conservatively
mark the bounded coarse grid and force one current-view build per strategic tick.
Current CNC specialist configs leave the radius at zero. The review's request to
restore Blue-Tiberium route-cell rejection was rejected because the user's explicit
cycle-3 policy delegates travel avoidance to locomotor cost and limits Blue safety
to engagement adjacency. This disposition preserves the user decision without
ignoring the review's valid compatibility edge.

### Final saturation game

`cycle3-final-saturation4` ran SkyNet versus IronReaper, all modules, 331 mobile
actors plus structures per side, Stealth/Chem formations, and active AirSquads to
tick 900 in `25.027s`. It passed without fatal error, exception, or desync.

| Direct identity | total / calls | average | worst | orders |
|---|---:|---:|---:|---:|
| Stealth strategy, both AIs | 222.997ms / 24 | 9.292ms | 48.911ms | 60 |
| Chemical strategy, both AIs | 76.725ms / 24 | 3.197ms | 5.705ms | 6 |
| all specialist local safety | 10.884ms / 144 | 0.076ms | 2.878ms | 29 stops |
| Air strategy, all profiles | 1316.446ms / 84 | 15.672ms | 102.650ms | 186 |
| Air local safety | 23.157ms / 144 | 0.161ms | 8.454ms | 0 |
| Air coarse route (nested) | 532.668ms / 1773 | 0.300ms | 1.915ms | 0 |
| Air influence build | 26.989ms / 13 | 2.076ms | 5.700ms | 0 |

The four specialist outer modules consumed `314.425ms` over 3,600 per-tick
dispatches (`0.349ms/game tick` across both AIs). Their explicitly measured
strategy plus safety was `310.606ms`; remaining wrapper/dispatch overhead was
`3.819ms`. Compared with cycle 2's two-AI Stealth strategy (`7790.150ms/24`),
cycle-3 Stealth strategy fell `97.1%`. Candidate-by-threat tests are now the
largest specialist phase (`202.413ms` across all profiles); coarse routing consumed
`19.823ms`. Air route/build timing is nested in Air strategy and is not double-counted.

Tick mean/p50/p95/p99/max were `23.513/12/49/203/1643.235ms`, with 42 ticks >=50ms.
The maximum was startup/world work, not the former specialist planning boundary.
Cycle 2's matched run recorded `31.606/11/57/227/6357.828ms` and 55 >=50ms ticks.

- Game: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/cycle3-final-saturation4/cycle2-saturation`
- Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle3-final-saturation4/NARRATIVE.md`

### Final reachable-action game

`cycle3-final-reachable4` reached tick 750 in `6.008s`. The opening targets took
first damage; opening-west died before tick 200 and opening-east was damaged. After
member replacement and two late openings at tick 300, both late targets took damage
and died before tick 500. Tick 700 still recorded 12 `stnk` plus 12 `ctnk` per side.
The game passed without fatal error, exception, or desync.

Specialist strategy consumed `72.294ms/40` heavy scans; local safety consumed
`2.996ms/120` calls; outer modules consumed `78.053ms/3000` dispatches. Tick
mean/p50/p95/p99/max were `3.589/2/4/19/1124.159ms`, with three startup events
>=50ms. Stealth issued 66 orders and Chemical 20 while the script proved useful
damage, target kills, turnover recovery, and survival.

- Game: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/cycle3-final-reachable4/cycle2-reachable`
- Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle3-final-reachable4/NARRATIVE.md`

### Checks

- Protected `make all`: passed, 0 warnings/errors on final source.
- Focused Stealth policy, Air target, and shared route tests: `52/52` passed.
- `git diff --check`: passed.
- Cycle-3 Luna review: one advisory concern, partially adopted as described above.

## Cycle 3 manual policy-review packet

No automated Match Policy Reviewer ran. The user must decide before cycle 4:

1. Retain the one 125-tick profile-isolated coarse map and shared group route, with
   exact ground refinement delegated to locomotion?
2. Accept the 25-tick engagement-only detector/engaged-weapon and Blue-adjacency
   stop/invalidation response?
3. Should cycle 4 test all-defended weakest-blocker clearing plus explicit local
   detector/Blue reactions, or optimize the remaining bounded candidate-by-threat
   phase first? The worker recommends behavior evidence first.

## Known risks / next evidence if authorized

- Saturation behavior telemetry is less decisive than the scripted reachable case;
  the all-dangerous hold is visible but not yet challenged by a safe weak blocker.
- Only SkyNet and IronReaper were exercised in cycle 3. Other bot personalities,
  explicit detector/Blue local response, pending explosion configuration, blocked
  topology, transport/crate handoff, save/load, repeated controls, paced agreement,
  final diagnostics cleanup, PR, and CI remain on the multi-cycle ladder.
- The historical `8024fd2` tree remains incompatible and was not relabelled or
  backported.

## Cycle 4 — capability-gated defender clearing and engagement safety

The user's manual review retained cycle 3's working 4-cell specialist map and
25-tick engagement check, and authorized a behavior-first cycle. Air uses an exact
configured 6-cell influence grid (`AirInfluenceCellSize: 6`); the specialist stays
at 4 because cycle 3 measured it working and there was no evidence that matching
Air's numeric resolution would improve ground behavior or cost.

### Implementation

- `StealthTankSquadPolicy.DefenderClearAction` makes the rare all-defended fallback
  explicit and testable. It returns `CrushInfantry` only for one isolated infantry
  blocker when the profile can crush and the blocker has no detector. It returns
  `SnipeTank` only for a non-detector tank whose weapon range plus the configured
  threat buffer and kite margin remains within specialist range. All other actors
  remain invalid clearing targets.
- Both Stealth and Chemical profiles still execute this one implementation.
  Chemical's existing `CrushInfantryTargets: false` prevents it from acquiring the
  crush action; no profile branch, target-priority, composition, cadence, balance,
  or YAML policy changed.
- A snipe plan now chooses only approach cells outside the same buffered threat
  envelope used by the 25-tick local safety check, avoiding a plan that would be
  immediately stopped. Crush routing deliberately validates terrain/domain while
  ignoring the crushable actor occupying the destination cell.
- The engagement check emits one bounded reason line when it acts, separating
  detector, engaged-weapon, and Blue-adjacency evidence. Detecting a newly local
  actor invalidates the shared factual view and that profile's coarse map so the
  next 75-tick strategy boundary cannot restore a stale unsafe route.
- Pending explosion cells remain conservatively represented in the coarse map,
  but the final source honors the required 125-tick cache lifetime. The cycle-3
  compatibility branch had rebuilt the full 4-cell grid every 75-tick scan whenever
  a pending radius was configured; removing that special case restores the Air-
  shaped bounded cache without weakening the declared hazard map.

### Game 1 — ambient-target fixture failure

`cycle4-defender` ran headless MAX with two ordinary IronReapers, all modules, two
reserved Stealth Tanks per side, seed `960401`, and a 2,000-tick bound. It completed
in `8.006s`, exit code 0, without exception/desync/fatal markers. The harness failed
only because the required weakest-defender selection was absent.

The map still contained ambient Creeps scenery. Both squads selected `arco#183`
at tick 1 and every later logged strategy boundary, issued a zero-waypoint hazard
route, and withheld both units. The intended west harvester/infantry remained
`35000/35000` and `5000/5000`; the east harvester/tank remained `35000/35000` and
`45000/45000` through tick 1950. This is invalid weakest-defender evidence, but it
factually demonstrates that the safety layer did not fall back to an unsafe direct
attack when no coarse route existed.

Direct two-AI Stealth totals were `267.277ms/4000` outer dispatches. Strategy was
`262.305ms/54` calls and local safety `1.740ms/160`. Tick mean/p50/p95/p99/max were
`2.054/1/4/12/1104.909ms`; the three >=50ms samples were startup, and the worst
tick after tick 3 was `44.358ms` at tick 748. No AirSquad existed in this narrow
fixture, so aggregate SquadManager timing is not presented as Air evidence.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/cycle4-defender/cycle4-defender`
- Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle4-defender/NARRATIVE.md`

### Game 2 — detector proof and failed defender fixture

`cycle4-combined-retry2` ran the corrected sequential fixture to tick 2200 in
`7.006s`, seed `960403`, exit code 0, without exception/desync/fatal markers. Two
tick-0 setup attempts were excluded: one destroyed the Creeps player actor, and one
had a Lua userdata formatting error. Neither advanced a valid test world.

Open harvesters first caused the west group to acquire a real route. At tick 100
the script injected `mhq#244` three cells from the group. At the next 25-tick local
safety boundary both `stnk#223` and `stnk#224` received Stop orders with
`detector=True`, `engaged-weapon=False`, `blue-adjacent=False`; the next strategy
record rebuilt current facts and issued no stale route. This is direct detector
reaction evidence.

The east group's open route was withheld before it engaged, so the injected Blue
seeder did not produce a `blue-adjacent=True` response. After the defended stage
began, west explicitly logged rejected `harv#246` with blocker `e1#247`; east
logged rejected `harv#248` with blocker `mtnk#249`. Both groups reached the 20-scan
patience boundary and attempted one route search, but no reachable clear was
selected and no actor took damage. Inspection found a concrete product defect:
crush endpoint validation rejected the cell precisely because the crushable actor
occupied it. Final source now ignores that actor for endpoint occupancy while
retaining terrain/domain validation. The east fixture also moved its target away
from the previously proven reachable coordinate, so it cannot distinguish policy
from map reachability. No third game was run under the two-valid-game cap.

Direct two-AI Stealth outer cost was `163.020ms/4400` dispatches, or
`0.074ms/game tick`; strategy was `154.950ms/60` (`2.583ms/call`) and local safety
was `4.666ms/176` (`0.027ms/call`, two Stop orders). Tick mean/p50/p95/p99/max were
`1.672/1/4/11/1058.881ms`; the three >=50ms events were startup, and the worst tick
after tick 3 was `40.774ms` at tick 1294.

For the requested correctly denominated Air comparison, the latest shared-load
cycle-3 saturation remains authoritative: Air strategy `1316.446ms/84`, Air local
safety `23.157ms/144`, specialist strategy `299.722ms/48`, and specialist local
safety `10.884ms/144`. Cycle-4's no-air fixtures are not cross-workload evidence.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/cycle4-combined-retry2/cycle4-combined`
- Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle4-combined/NARRATIVE.md`

### Checks and manual gate

- `make all`: passed, zero warnings/errors.
- Focused `StealthTankSquadPolicyTest`: `52/52` passed. Seven cases cover the
  defender capability boundaries in addition to existing cadence/cache/isolation
  coverage.
- `git diff --check`: passed.
- No automated Match Policy Reviewer ran. Each valid game received its own fresh
  Luna factual narrative.

The narrow code direction remains sensible, but visible weakest-defender clearing
and Blue engagement are not proved. Proposed status remains `First iteration -
testing`. A cycle 5, only if the user authorizes it, should use the already proven
reachable west/east coordinates, begin with engaged groups, and separately prove
isolated-infantry crush, outside-range Medium Tank snipe, subsequent Harvester
reassessment/damage, and Blue-adjacency Stop. It must not add a third cycle-4 game.

## Cycle 5 — focused fixture proof at durable product head

The user authorized one final primary Terra-medium cycle from durable head
`edefb98bd9282e2fea8636705fe78daee56f3557`. The cycle retained product code and
CNC authored configuration unchanged: the remaining question was literal runtime
proof of the cycle-4 endpoint/range repair, not a basis for redesign. Before either
game the cycle recorded the preservation/scenario table at
`analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle5/PRESERVATION-AND-SCENARIOS.md`.
It explicitly preserved actor eligibility/grouping, config-only profile identity,
target priorities/bounds, detector/weapon/resource meaning, 4-cell and 125-tick
specialist routing, 75-tick strategy, engagement-only 25-tick safety, stable-plan
invalidators, and Air's independent 6-cell output.

Exactly two valid games ran. One attempted combined game failed at tick 0 because
destroying inherited Neutral crates during `WorldLoaded` invoked a crate-removal
callback against a destroyed world actor. It was corrected by stripping the custom
map's actor block down to multiplayer spawns and is excluded from the count. No
third valid game ran.

### Game 1 — inherited ambient-target failure

`cycle5-defenders` used Watson/Nod versus VIKI/GDI, all modules, seed `960501`,
headless MAX, and the previously proved `38,21` / `138,151` target cells. It reached
tick 3200 in `9.008s`, exit code 0, with no fatal/exception/desync marker. The
harness result was a behavior failure: inherited Creeps-owned `arco#183` remained
on the source map, outranked the deliberately defended package, and both two-tank
groups selected it at every sampled scan. Each bot performed `43` route searches
and queued `43` Stop/order batches, always `routed=0`, `withheld=2`, and never
recorded target damage. Both scripted Harvesters, `e1`, and `mtnk` remained at full
health through tick 3151. The `avoided-resources=BlueTiberium` string is the route's
configured avoidance label; the artifact does not identify the actual zero-route
cause.

Direct performance stayed bounded despite the invalid fixture. Across 6,400
Stealth outer dispatches the two bots used `239.124ms`; strategy consumed
`231.692ms/86` calls and local safety `2.673ms/256`. The tick distribution was
mean/p50/p95/p99/max `1.773/1/3/9/1272.077ms`; only three startup ticks were at
least 50ms and the worst tick after tick 3 was `31.423ms`.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/cycle5-defenders/cycle5-defenders`
- Fresh Luna narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle5-defenders/NARRATIVE.md`

The narrative correctly marks the missing combat/kill/reassessment evidence. Its
causal wording that the configured Blue label was the cause of withholding is not
supported by the log; the factual worker conclusion remains that ambient target
selection plus an unknown zero-route cause invalidated this behavioral probe.

### Game 2 — partial combined proof

The corrected `cycle5-combined-retry1` map contained only multiplayer spawns before
Lua created west `harv@38,21` + `e1@37,21`, east `harv@138,151` +
`mtnk@137,151`, and two separate `vice` targets for Chemical. SkyNet/Nod versus
IronReaper/GDI, all modules, seed `960502`, headless MAX, reached tick 3200 in
`8.011s`, exit code 0, with no fatal/exception/desync marker.

The west sequence proves useful specialist target recovery and live detector
response, but not the named clearing action. The scripted `e1` first took damage at
tick 551 and died at 576; no `by CrushInfantry` log exists, so the exact attacker
and crush action remain unknown. SkyNet's two Stealth Tanks then routed to the
Creeps-owned Harvester. A Stealth-sourced damage callback fired at tick 817 when the
target had `378868/500000` HP and injected `mhq#64`; at the next recorded local
safety boundary (tick 826) both `stnk#32/#33` stopped with `detector=True`,
`engaged-weapon=False`, and `blue-adjacent=False`. The target later died at tick
1130. Chemical also stopped when physically inside the detector envelope. That is
shared-control threat safety, so the fixture's initial forbidden-Chemical assertion
was overstrict rather than evidence of cross-profile target/cache contamination.

The east sequence failed the requested proof. Through tick 2101 the `mtnk` and
Harvester stayed at full health. Late Air was injected at tick 2601. At tick 2817
both east actors first recorded damage, but the tank still survived at
`44727/45000` and the Harvester at `266080/500000` by tick 3151. There was no
`SnipeTank` selection/kill and no Stealth-sourced callback, so no Blue seeder was
injected and no `blue-adjacent=True` response could occur. Direct Air evidence is
present despite the harness expecting the wrong literal `AI air strategy` string:
Apache/Orca selected and attacked `vice#57`, Generic Air selected `harv#55`, and
the periodic attribution records independent profile identities.

| Direct identity | total / calls | average | worst | orders |
|---|---:|---:|---:|---:|
| Stealth strategy, both AIs | 71.099ms / 86 | 0.827ms | 28.829ms | 4 |
| Stealth local safety | 2.650ms / 256 | 0.010ms | 1.380ms | 2 |
| Chemical strategy, both AIs | 27.635ms / 86 | 0.321ms | 8.987ms | 74 |
| Chemical local safety | 2.015ms / 256 | 0.008ms | 1.627ms | 3 |
| Air strategy, all profiles | 255.590ms / 51 | 5.012ms | 71.622ms | 24 |
| Air local safety | 12.770ms / 88 | 0.145ms | 11.789ms | 0 |

The four specialist outer modules consumed `111.154ms` across 12,800 per-tick
dispatches. The tick distribution was mean/p50/p95/p99/max
`1.432/1/3/10/1071.437ms`, with six ticks at least 50ms. Three were after startup;
the worst post-tick-3 tick was `72.910ms` at tick 2686. Runtime at tick 3200 was
about `618MB` working set, `1.511GB` cumulative managed allocation, and GC counts
`169/56/13`. Air attribution is correctly denominated and independent; its 51
strategy calls are not compared as if equal to 86 specialist scans.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/cycle5-combined-retry1/cycle5-engagement`
- Fresh Luna narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle5-combined/NARRATIVE.md`

### Checks, disposition, and manual gate

- Protected CNC compile/map lint: passed, zero warnings/errors.
- Focused `StealthTankSquadPolicyTest`: `52/52` passed.
- Both cycle-5 map archives passed CNC MiniYAML validation; Lua passed syntax
  checking before launch.
- No product/config file changed. No automated Match Policy Reviewer ran. Each
  valid game received its own fresh Luna-medium factual narrator.

Proposed status remains `First iteration - testing`. Cycle 5 materially extends
behavior evidence with Stealth-sourced Harvester damage/destruction, detector
trigger-to-Stop response, active distinct Chemical work, and isolated Air
strategy/local-safety attribution. It still does not prove explicit occupied
`CrushInfantry`, outside-range `SnipeTank` and subsequent east Stealth Harvester
reassessment, or Blue adjacency. The parent has separately authorized routing a
fresh Sol-medium cycle 6 after this durable commit; this worker stops here and does
not begin that cycle. The subsequent acceptance fixture must use ordinary VIKI
versus Brutalis with no Air units or Air injection, because Air counters Stealth
and confounds behavioral attribution.

## Cycle 6 — isolated no-Air acceptance and concrete detector defect

The authorized exceptional Sol-medium cycle retained product/config at durable
head `f30f976d4e6a4ba15c9fab1da60ec754997a0d04`. Both games used ordinary
VIKI/Nod versus Brutalis/GDI, all normal modules, headless MAX, purged map actors,
proven reachable cells, and no aircraft. The preservation/scenario contract is
`analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle6/PRESERVATION-AND-SCENARIOS.md`.
Exactly two valid games ran. One Game-B attempt failed at tick 0 because a Lua
damage callback referenced an unsupported actor property; it was corrected to
log attacker type, owner, and location and is excluded from the count.

### Game A — target-local detector classification defect

`cycle6-snipe` (seed `960601`) reached tick 2800 in `6.007s`, exit 0, without
fatal/exception/desync markers. Two VIKI Stealth Tanks faced Brutalis
`mtnk@37,21` and protected `harv@38,21`. Recon3 gave ordinary Brutalis vehicles
short detection. Across 38 scans VIKI repeatedly rejected `harv#50` with itself
as blocker and issued 0 routes/orders/damage; tank/Harvester remained
`45000/45000` and `500000/500000`. No SnipeTank or reassessment occurred.

Authoritative user review identifies a concrete product defect: an unarmed
primary target's own 1-cell `DetectCloaked` does not defend it when a reachable
Stealth firing approach lies outside detection. A next cycle must make
target-local detection capability/range-aware while preserving avoidance of
separate armed/dedicated detectors and detector coverage of the firing approach.
No code change was permitted after this cycle's game/review boundary.

VIKI Stealth outer/strategy/local cost was `47.056ms/2800`, `42.150ms/38`, and
`1.939ms/112`, with 0 orders. Unique tick mean/p50/p95/p99/max was
`1.008/0.313/1.184/4.387/1030.273ms`; post-tick-3 max was `44.109ms` at tick
2523. Runtime ended near `621MB`, `1.326GB` allocated, GC `147/51/13`.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/cycle6-snipe/cycle6-snipe`
- Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle6/reviews/game-a-narrator/NARRATIVE.md`
- Policy: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle6/reviews/game-a-policy/POLICY-REVIEW.md` (`insufficient evidence`, high)

The narrative correctly reports zero action but could not identify the rejection
rule from its isolated inputs. The review correctly blocks literal acceptance;
the user's concrete range/capability ruling resolves its remaining causal doubt.

### Game B — Blue pass, detector isolation gap

`cycle6-safety` retry 1 (seed `960602`) reached tick 2200 in `7.009s`, exit 0,
without fatal/exception/desync markers. At tick 121 exact callback attribution
proved `source=stnk source-owner=Multi0` damage to Brutalis `harv#43`
(`483000/500000`); Blue was injected adjacent to the attacker. By the next logged
25-tick local-safety boundary `stnk#29` stopped with `detector=False`,
`engaged-weapon=False`, `blue-adjacent=True`; both tanks continued showing Blue.
This proves the requested Blue-adjacent engagement safety without Air.

At tick 901 a fresh VIKI pair and separate Brutalis Harvester began the detector
phase. At tick 1168 exact VIKI Stealth damage reduced it to `483000/500000`, then
a Brutalis MHQ was injected three cells away. Destroying the `splitblue` actor had
not removed already-seeded resource, however: fresh `stnk#48/#49` were already
stopping for Blue and continued with that reason. `detector=True` never appeared,
so detector reaction is not independently proved. Final target HP was
`466000/500000`. Cycle 5 retains an uncontaminated detector regression.

VIKI Stealth outer/strategy/local was `92.587ms/2200`, `83.848ms/30` with 28
orders, and `5.862ms/88` with 27 orders. Unique tick mean/p50/p95/p99/max was
`1.498/0.311/3.093/14.325/1028.129ms`; post-tick-3 max was `32.882ms` at tick
1390. Runtime ended near `615MB`, `1.528GB` allocated, GC `170/66/13`. The
75-tick strategy, 25-tick engagement safety, 4-cell grid, 125-tick cache, shared
Stealth/Chem implementation, stable plans, and no-Air isolation remained intact.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/games/cycle6-safety-retry1/cycle6-safety`
- Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle6/reviews/game-b-narrator/NARRATIVE.md`
- Policy: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle6/reviews/game-b-policy/POLICY-REVIEW.md` (`insufficient evidence`, high)

The narrative correctly records engagement and Blue Stops, but its claim that the
detector outcome was proved is too strong: the boolean proves injection after
engagement, not detector-attributed reaction. The reviewer correctly requires
explicit detector identity. Its separate-Chemical suggestion is advisory only;
Chemical is explicitly not required to react to Blue and shared-path/config
isolation is already covered elsewhere.

### Checks and disposition

- Protected CNC Release build and full CNC MiniYAML/map lint: passed, 0 warnings/errors.
- Focused `StealthTankSquadPolicyTest`: 52/52; Lua syntax and `git diff --check`: passed.
- No product/config changed. Each valid game received a fresh Luna factual narrator and fresh isolated Luna policy reviewer as explicitly required.

Status remains `First iteration - testing`. Cycle 6 proves Blue-adjacent reaction
and identifies the exact range/capability detector defect. The next cycle must
narrowly correct it and prove outside-range SnipeTank, defender kill, and
Harvester reassessment while retaining dedicated-detector safety; it must also
isolate detector regression from residual Blue.

## Cycle 7 — capability/range-aware self-detector correction

The authorized Sol-medium cycle began from durable head `76b7406777b464d52370519e702aec2d9252ef87`.
The concrete defect was in preliminary candidate screening: a target's own
detector was treated as unavoidable before the existing route planner could test
an outside-coverage firing approach. The narrow correction permits screening only
when the threat actor is the primary target, has no ground weapon, has positive
detector range, and the specialist's range is strictly greater than the buffered
detector range. Separate detectors and armed targets never receive the exception.
The unchanged 4-cell/125-tick influence map still contains the target detector, so
the existing route search must find an approach outside its coverage.

No authored config, balance, target priority, cadence, candidate/group bound,
profile distinction, Air behavior, local engagement response, or stable-plan
invalidation changed. Four focused cases cover the positive unarmed target, equal
range rejection, separate detector rejection, and armed detector rejection. One
bounded debug line records actor/owner plus raw, buffered, and own ranges when the
candidate-only exception applies.

Exactly two valid VIKI/Nod versus Brutalis/GDI headless-MAX games ran with all
ordinary modules enabled and no aircraft. Both maps contained only multiplayer
spawn actors before Lua setup; they did not destroy Neutral crates at runtime.
One initial Game A process reached combat but a Lua callback used unsupported
`ActorID`; its summary recorded world tick 0 and it is excluded. The callback
was corrected to supported type/owner/location fields and both source and packaged
Lua were checked ActorID-free before the valid runs.

### Game A — unarmed self-detecting primary target

`cycle7-self-detector` (seed `960701`) reached tick 1800 in `6.008s`,
exit 0, without fatal/exception/desync or Air identity. VIKI formed the two-unit
specialist group, logged the Harvester's raw detector range 2, buffered range 4,
and own range 8, and found a two-waypoint route at tick 76. At tick 162 exact Lua
attribution recorded a Multi0 `stnk` at `30,21` damaging the Multi1 fixture
Harvester at `38,21`: cell-distance-squared 64, target health
`483000/500000`. The target was dead by tick 1501.

The launcher wrapper marked the valid run failed only because three manifest
regexes treated square brackets as character classes; the raw bracketed facts,
configured exit, and behavioral requirements are present. The fresh Luna factual
narrative correctly treats this as bookkeeping, does not call Brutalis an old
control, and does not infer why later zero-waypoint routes were withheld. Its
fresh Luna policy review returned `mostly sensible` / medium confidence with no
blocker; repeated later withholding is advisory and unrelated to the proved
fixture target.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle7/games/game-a-retry1/cycle7-self-detector`
- Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle7/reviews/game-a-luna-narrator/NARRATIVE.md`
- Policy: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle7/reviews/game-a-luna-policy/POLICY-REVIEW.md`
- Performance: tick mean/p50/p95/p99/max `1.571/1/3/9/1206.752ms`;
  three >=50ms events were startup and post-tick-3 max was `25.362ms`.
  VIKI Stealth used `117.521ms/1800` outer dispatches, worst `51.293ms`,
  and 18 orders. Final working set was about `573MB`, allocated bytes
  `1.064GB`, GC `117/41/13`.

### Game B — completed under a superseded detector-alone premise

`cycle7-detector-recovery` (seed `960702`) reached tick 2300 in
`7.007s`, exit 0, without fatal/exception/desync or Air identity. With a
Brutalis MHQ at `31,21`, VIKI reported all candidates dangerous and named
`mhq#50` as the blocker; the fixture Harvester remained
`500000/500000` through tick 701. The MHQ was removed at tick 901 with no
prior damage. VIKI replanned at tick 976 and at tick 1113 a Multi0 `stnk`
at `30,21` damaged the Multi1 Harvester at `38,21`, again distance 8;
the target was dead by tick 2101.

The wrapper's only miss was a doubled space in the removal event. The fresh Luna
narrative accurately reconstructs the run. Its policy review returned
`sensible` / high confidence under the supplied premise. Immediately after the
game, however, the user authoritatively corrected that premise: firing reveals a
Stealth Tank anyway, so detector-only coverage must not veto or Stop an engagement.
A lone unarmed MHQ is defenseless and may itself be attacked. Detection remains
relevant to concealed routing/approach; engagement deterrence requires detector
coverage plus armed enemy support capable of punishing the intended firing or
escape area. Therefore Game B is a valid engine run and routing observation, but
its detector-alone hold is **not acceptance**, and its policy review is
superseded.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle7/games/game-b/cycle7-detector-recovery`
- Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle7/reviews/game-b-luna-narrator/NARRATIVE.md`
- Superseded policy: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle7/reviews/game-b-luna-policy/POLICY-REVIEW.md`
- Performance: tick mean/p50/p95/p99/max `1.335/1/2/8/1167.924ms`;
  three >=50ms events were startup and post-tick-3 max was `27.092ms`.
  VIKI Stealth used `96.482ms/2300` outer dispatches, worst `37.425ms`,
  and 6 orders. Final working set was about `575MB`, allocated bytes
  `1.157GB`, GC `129/44/13`.

### Checks and next boundary

- Protected Release build and full CNC MiniYAML/map lint passed with zero
  warnings/errors; both packaged cycle-7 maps also linted independently.
- Focused `StealthTankSquadPolicyTest` passed `56/56`; Lua syntax and
  `git diff --check` passed.
- The 75-tick strategy, engagement-only 25-tick safety, 4-cell specialist grid,
  125-tick cache, shared Stealth/Chemical implementation, and Air isolation
  remained unchanged.
- Exactly two valid games were consumed; no post-correction game or product churn
  occurred.

Proposed status remains `First iteration - testing`. The self-detector
candidate correction is implemented and literally proved. A separately authorized
cycle 8 must make engagement response armed-support-aware and test an MHQ plus
armed supporting defender, then remove or neutralize the shooter while the MHQ
remains and prove reassessment/attack. Explicit SnipeTank/defender kill, other
personalities, reservations/blocked routes, controls, paced agreement, save/load,
final review, PR/CI, and diagnostic cleanup remain open.

## Cycle 8 — armed-support-aware engagement safety

The authorized Sol-medium cycle began from durable head `491c1a35939becc4fa9668ae94993bcacf5afc6d`.
The narrow correction leaves the 75-tick strategic candidate/influence routing
unchanged and changes only an already-firing engagement. Detector coverage at a
specialist's current firing/escape cell is actionable only when non-ignored
enemy ground-weapon coverage overlaps it; an already-engaged enemy weapon remains
an immediate threat. A detector-plus-armed Stop retains the exact active target
for bounded 25-tick reassessment. The final source also prevents the slower
concealed-approach scan from discarding an already-active, valid, locally safe
engagement under detector-only coverage. Blue/resource hazards and ordinary
weapon Stops retain their prior semantics. Cycle 7's unarmed self-detecting
target exception is unchanged.

Focused tests cover lone detector false, detector plus non-engaged armed support
true, one armed detector true, shooter removed false, already-engaged weapon true,
ignored weapon false, no same-call resume, suspended-target release, and active
engagement retention. Shared Stealth/Chem control, 4-cell/125-tick influence,
75/25-tick cadences, stable plans, Air isolation, and all authored values remain.

### Game A — combined coverage exposed same-call resume

`cycle8-armed-recovery` (seed `960801`) reached tick 2100 in `6.008s`, exit 0,
without fatal/exception/desync or Air identity. At first VIKI STNK damage tick
227 the fixture placed Brutalis `mhq#52` and `mtnk#53` relative to the actual
firing cell. Local safety named MHQ owner Multi1/buffered detector range 18 and
MTNK owner Multi1/buffered ground range 7 and stopped both STNKs. The same
safety invocation then immediately resumed the Harvester target and repeated;
the tick-501 boundary recorded `damage-during-threat=true`. Removing only MTNK
left MHQ alive but no later damage occurred. This valid engine game failed its
behavior assertions and exposed the now-fixed requirement that an engagement
must already have been suspended at invocation start before it can resume.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle8/games/game-a/cycle8-armed-recovery`
- Narrative/review: `cycle8/reviews/game-a-luna-narrator/NARRATIVE.md` and
  `cycle8/reviews/game-a-luna-policy/POLICY-REVIEW.md`.
- Review verdict: sensible intent, high-priority observed failure; require an
  idempotent Stop and fresh fact transition before release. Adopted in the
  same-call guard; generation-token telemetry is deferred.

### Game B — core rule proved; strategic boundary exposed

`cycle8-detector-then-armed` (seed `960802`) reached tick 1700 in `6.010s`,
exit 0, without fatal/exception/desync or Air identity. VIKI STNK first damaged
the Harvester at tick 213; a lone Brutalis MHQ was injected at the actual firing
cell and a second attributable STNK hit followed at tick 224 while MHQ lived.
At tick 601 a Brutalis MTNK covering the firing cell was injected; local safety
stopped `stnk#32` with exact MHQ owner/range 18 and MTNK owner/range 7. No damage
occurred under armed coverage. MTNK was removed at tick 676 while MHQ remained,
but no resume or post-removal damage followed. Evidence showed that the slower
strategic scan had already cleared the active target under detector-only coverage,
so the armed Stop had no target to suspend. The final source narrowly retains an
already-active valid engagement when the same 25-tick predicate says it is safe;
pre-engagement approach routing still avoids MHQ coverage. The game cap prevented
literal proof of that final repair.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle8/games/game-b/cycle8-detector-then-armed`
- Narrative/review: `cycle8/reviews/game-b-luna-narrator/NARRATIVE.md` and
  `cycle8/reviews/game-b-luna-policy/POLICY-REVIEW.md`.
- Review verdict: core exposure policy sensible and demonstrated; recovery is
  a medium-high evidence gap. Adopted active-assignment preservation; explicit
  assignment-generation telemetry remains advisory/deferred.

### Target policy note and checks

After shooter removal, current source resumes the exact
`SuspendedEngagementTarget` (the intended Harvester), not the MHQ or a freshly
ranked target. Normal pre-engagement selection chooses the highest-score safe
candidate: STNK Harvester priority is 10000, with economic value, distance,
25-percent incumbent bonus, and deterministic actor-ID tie. Only after 20
all-defended scans does it consider the weakest three defender packages and
choose highest unlocked target score; eligible clears are isolated non-detector
infantry crushes or safely outrangeable non-detector tanks. Thus a lone MHQ is
currently neither a normal harassment target nor a clearable detector. The user
has directed cycle 9 to address that Air-AA-clearing mismatch; cycle 8 does not
broaden into it.

Protected strict Release solution build passed with zero warnings/errors; full
CNC MiniYAML passed; focused policy suite passed 67/67; both custom maps and Lua
passed preflight; `git diff --check` passed. Exactly two valid games were consumed.
Both launcher summaries say failed solely because behavior assertions were
missing; engine completion/integrity passed and the absent assertions are the
recorded product evidence. No third game ran.

Status remains `First iteration - testing`. The final active-engagement repair
requires fresh full-engine proof before acceptance or Terra final review. Cycle 9
must also implement the separately authorized safe lone-MHQ clearing policy,
without weakening armed detector/support avoidance. AirSquad remains the lifecycle
gold standard: that cycle must inspect and reuse safe-primary -> weakest-blocker ->
reassess, unfavorable-flee, damaged-to-repair, and repaired-rejoin/return seams
where they are safely common, while preserving Stealth capability distinctions,
caches, and cadences. Repair must be opportunistic exactly like Air: when no
compatible reachable repair exists, damaged Stealth/Chem remains active with
bounded conservative hit-run/flee behavior until death, never parking, idling,
leaving the squad, or waiting; it reevaluates if repair later appears. Cycle 9
needs no-repair-active and repair/rejoin tests. Those broader changes are not
part of cycle 8.

## Cycle 9 — Air lifecycle mapping and MHQ blocker clear

Cycle 9 began from clean durable head `eb152455fba93a4de521fa735eccba45fb6e271e`.
AirSquad's reusable seams are safe undefended targets before AA clearing; three
no-undefended scans in CNC before weakest relevant AA clearing; protected-primary
reassessment; 25-tick local flee; individual repair; repaired reinforcement
rejoin; and active fallback when no compatible facility exists. Air's private
caches and occupied/AA-covered-pad parking were not copied.

Specialists retain shared facts, profile-private 4-cell/125-tick influence, 75/25
cadences, ground domain/resource safety, and exact suspended targets. Safe primary
targets remain the strict first tier. After three all-defended scans, the weakest
fallback may attack an isolated unarmed detector using a one-operation influence
map that ignores only that target; separate armed coverage remains unsafe.
Per-member repair uses the existing 0.5 threshold and requires a compatible allied
`fix`, safe reachable cell, and threat-free coarse route. Full health rejoins the
same group. No route leaves the member reserved and combat-active, with 125-tick
reevaluation. Focused tests cover active/repair/rejoin; engine repair proof is
deferred to preserve the two required games.

### Game A — exact assignment recovery, ambiguous shot timing

`cycle9-armed-recovery` (seed `960901`) reached tick 1800 in `6.005s`, exit 0,
without Air, exception, fatal, or desync. Lone MHQ allowed attributable VIKI STNK
damage. With an added MTNK, safety stopped both STNKs and named `mhq#39` / Multi1 /
buffered 18 and `mtnk#42` / Multi1 / buffered 7. Shooter removal at tick 676 left
MHQ alive and the same safety boundary resumed exact `harv#38`. This proves the
cycle-8 assignment-lifecycle repair.

The fixture marked `damage-under-armed=true`, but supplies neither the shot tick
nor whether an already-fired missile landed after Stop, so it is not enough to
assign a product cause. First attributable post-removal damage was tick 743 (+67),
not the requested <=25, although the resume order itself was immediate. The next
cycle must distinguish pending projectile/order latency from local-safety cadence
before changing product code. The fresh Luna policy verdict is `FAIL` / high under
the literal fixture contract and recommends per-shot/order/assignment timing.
Tick mean/p50/p95/p99/max was `0.678/0.251/1.381/5.996/1025.134ms`; only three
>=50ms events were startup.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle9/games/game-a/cycle9-armed-recovery`
- Narrative/review: `cycle9/reviews/game-a-luna-narrator/NARRATIVE.md` and
  `cycle9/reviews/game-a-luna-policy/POLICY-REVIEW.md`

### Game B — isolated unarmed MHQ clear

`cycle9-mhq-clear` (seed `960902`) reached tick 1900 in `5.004s`, exit 0, with
all assertions true. After exactly three all-defended scans, VIKI selected
`mhq#33` by `AttackUnarmedDetector` while retaining protected `harv#32`. STNKs
damaged the MHQ at tick 254 and killed it at 278. The next strategic scan at 301
reassessed and routed to exact `harv#32`; damage occurred at tick 376 and the
Harvester ultimately died. Luna returns bounded `PASS` / moderate-high confidence.
Tick mean/p50/p95/p99/max was `0.514/0.212/1.227/5.329/1034.717ms`; only three
>=50ms events were startup.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle9/games/game-b/cycle9-mhq-clear`
- Narrative/review: `cycle9/reviews/game-b-luna-narrator/NARRATIVE.md` and
  `cycle9/reviews/game-b-luna-policy/POLICY-REVIEW.md`

Protected Release build passed with zero warnings/errors; full CNC MiniYAML lint
passed; focused suite passed `79/79`; Lua syntax, ActorID absence, JSON, launcher
preflight, and `git diff --check` passed. Exactly two valid games ran. Proposed
status remains `First iteration - testing`: blocker ordering passes and exact
assignment release is proved, but shot/damage timing needs one narrow diagnostic
cycle before Terra final review; repair behavior still lacks engine evidence.
## Cycle 10 evidence — causal Stop telemetry and repair lifecycle

Cycle 10 started exactly at durable head `94c12dce8892ff437763ea5508f752973c25a3bd`.
The only product correction is the missing terminal `Repair` order after a safe
repair route: the prior lifecycle queued Move waypoint(s), marked the member as
repairing, and excluded it from combat, but never invoked `Repairable.ResolveOrder`
to enter `Resupply`. It now queues the compatible facility actor as a queued
`Repair` target. Opt-in specialist logs additionally record exact safety ticks,
pre-Stop activity, armament reload/FireDelay/Burst, and exact resume identity.
No cadence, threshold, priority, balance, route, cache, Air, or config value changed.

### Game A — detector plus armed Stop and committed missile diagnosis

The full-engine VIKI/Nod versus Brutalis/GDI all-module, no-Air game reached tick
1900 in 6.006s with exit 0 and no exception/fatal/desync. Armed MTNK support was
injected at tick 601. At exact local boundary 625 both VIKI STNK activities were
`Attack`; one ground armament was reloading with delay 68/burst 2 and the other
with delay 3/burst 1. Both received Stop with exact MHQ owner/range18 and MTNK
owner/range7. Four already-committed missiles impacted at ticks 650/651/660/661;
no later Harvester impact or newly queued specialist Attack was logged through
the extended armed window. This resolves cycle 9's armed boolean: it mixed the
24 ticks before Stop and unavoidable launched missiles with unsafe continued fire.

The extended window exposed a separate fixture failure: assignment changed from
two specialists to one and then zero before shooter removal at tick 801, proving
both held STNKs died. Therefore no actor remained to satisfy the exact <=25-tick
resume requirement. This is an engine-valid behavioral failure, not proof of a
resume-code defect. Raw tick mean/p50/p95/p99/max were
`0.723/0.253/1.520/8.208/1053.159ms`, four >=50ms, startup-dominated.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle10/games/game-a-valid/cycle10-armed-recovery`
- Fresh Luna narrative/review: `cycle10/reviews/game-a-narrator/NARRATIVE.md`; `cycle10/reviews/game-a-policy/POLICY-REVIEW.md` (Stop/in-flight supported; resume failed).

### Game B — no-repair active pass, repair fixture conflict

The distinct full-engine VIKI/Brutalis all-module, no-Air game reached tick 2200
in 6.006s with exit 0 and no integrity fault. Two named STNKs began at
6000/15000 with no facility; specialist telemetry recorded `total=2 reserved=2`.
The Lua object-identity callback proves the exact no-repair actor repeatedly dealt
Harvester damage from tick 200 while remaining at 6000 HP. It was alive and had
damaged the target at tick 451, when the fixture deliberately destroyed it; one
committed missile landed at tick 465. This literally proves active/reserved
no-repair behavior through death rather than parking or waiting.

The same death invalidated the intended repair phase: with one eligible STNK,
`ReserveOpeningPair` correctly changed assignment to `total=1 reserved=0 groups=0
ordinary=1`. The compatible `fix` created at tick 451 stayed alive, but the
specialist module no longer owned the survivor; health stayed 6000/15000 and no
specialist repair/rejoin could occur. This is fixture-conflicted missing coverage,
not evidence against the Repair order. Raw tick mean/p50/p95/p99/max were
`0.615/0.212/1.358/11.842/1125.856ms`, three >=50ms, startup-dominated.

- Artifact: `analysis/20260813-cnc96-split/worker-1-cnc-96a/cycle10/games/game-b-valid/cycle10-repair`
- Fresh Luna narrative/review: `cycle10/reviews/game-b-narrator/NARRATIVE.md`; `cycle10/reviews/game-b-policy/POLICY-REVIEW.md` (no-repair pass; repair/rejoin unproved).

One pre-completion Game A attempt failed at tick 649 because Lua used an unsupported
`ActorID` property; it was repaired before the two valid games and is excluded.
Protected Release CNC compilation passed with zero warnings/errors, full CNC
MiniYAML lint passed, focused policy tests remain 79/79, Lua syntax and diff checks
pass. At the exact two-game cap the result remains `First iteration - testing`:
not ready for Terra final review/publication until a surviving <=25 resume and a
reserved compatible repair/full-rejoin are both proved.

## Cycle 11 — final-review fix

`FindRepairRoute` now passes `strategicViewOwner.strategicView.Threats` through an
explicit `ResolveRepairInfluence` seam, then calls the current profile's private
`GetInfluenceMap`. Shared facts therefore enable both profiles without sharing
Stealth/Chemical weights or the 4-cell/125-tick instance cache. Air code/output,
75/25 cadences, 125-tick repair reevaluation, policy values, and balance are
unchanged. Tests cover both profile labels with one shared fact object and distinct
private cache/weights, plus null-fact active fallback.

The pre-game user correction maps Air's `Squad.IsValid`,
`AirFormationUnits(bootstrapIfEmpty:true)`, `MarkAirReinforcement` /
`PromoteArrivedAirReinforcements`, and `Squad.Serialize/Deserialize`: an eligible
owned survivor remains specialist-owned; deterministic rebalance retains owned
IDs first and recruits compatible replacements; membership change still
invalidates routes; `ReservedSpecialists` persists through game save. Focused
tests cover partner death/active ownership, replacement reform, restored lone
ownership, and no duplicate/ownerless reservation.

Game A (`cycle11-chemical-repair`, seed 961101) reached tick1800 in 5.004s,
exit0, no Air/exception/fatal/desync. The exact non-owner CTNK rose from 9000/25000
to 10250 at tick126 and 25000 at tick1001 beside a live fix. No displaced route,
queued Repair line, or post-full damage appeared, so the accurate result is
full-health repair with combat rejoin unproved. Luna narrative/policy:
`cycle11/reviews/game-a-narrator/NARRATIVE.md` and
`cycle11/reviews/game-a-policy/POLICY-REVIEW.md` (`mixed/high`). Tick
mean/p50/p95/p99/max was `0.496/0.223/0.691/5.206/1065.649ms`; two >=50ms events
were startup.

Game B (`cycle11-stealth-repair`, seed 961102) reached tick2200 in 6.005s,
exit0, no Air/exception/fatal/desync. The exact 6000/15000 STNK damaged targets,
survived partner death, and continued exact-object damage through tick740.
Telemetry proved `total=1 reserved=1 groups=1/0/0 ordinary=0`, then after replacement
`total=2 reserved=2 groups=2/0/0 ordinary=0`. Neither repair probe activated because
VIKI's authored active SquadManager retains default zero `HealthRetreatThreshold`.
Luna narrative/policy: `cycle11/reviews/game-b-narrator/NARRATIVE.md` and
`cycle11/reviews/game-b-policy/POLICY-REVIEW.md` (ownership accepted; repair
`insufficient/high`). Tick p50/p95/p99/max was `1/2/7/1054.289ms`; three >=50ms
events were startup.

Strict Release compilation/full CNC MiniYAML passed with zero warnings/errors;
focused tests passed 85/85; both maps and Lua/JSON/ActorID/diff checks passed.
Exactly two valid games ran. The final-review blocker is fixed; explicit route/order
and combat rejoin remain evidence limits for fresh Terra rereview.

Cumulative Sol-medium integration handoff: pre-spawn a reachable compatible Repair
Facility for VIKI, place a Stealth specialist below an explicitly active authored
retreat threshold, and prove Repair health increase, full repair, rejoin, and
continued same-object action. Use a distinct no-repair active-fallback leg if the
integration game budget permits; do not tune product balance to activate repair.

## Exceptional natural-combat correction

The release-blocking ordinary-play report was reproduced before editing. An
unmodified one-VIKI-versus-two-allied-Brutalis Empire Earth game reached tick
15100 in 76.072 seconds. VIKI had two STNK and one CTNK by tick 15000, yet no
specialist achieved a meaningful kill; the first STNK damage callback arrived
only at tick 15027. The trace showed roughly 3000 ticks of target churn after
the first STNK assignment. Once it began firing, every reveal correctly caused
one six-cell strategic retreat, but completion discarded the attacked target and
the next scan frequently chose a different Harvester, SAM, infantry or structure.

The exact defect was a broken handoff between two already-existing lifecycle
fields. `BeginStrategicRetreat` stored the attacked actor in `RetreatTarget`, but
`UpdateStrategicRetreat` unconditionally nulled it when the last member reached
its destination. The correction classifies retreat completion and places a
still-live enemy back into `group.Target` as the incumbent for the forced fresh
scan. That scan still owns route safety, scoring and the existing switch threshold.
Dead, captured and stale actors are not restored. Six-cell geometry, multi-member
barrier, save/load, repair, reinforcement, ownership, Stealth/Chemical configuration,
target priorities, Air and all balance values are unchanged.

The focused regression covers all three outcomes: a pending member continues the
barrier, a completed live target is reassessed as incumbent, and an invalid target
is omitted. Final filtered `StealthTankSquadPolicyTest` passed 106/106. Final
Release compilation and full CNC MiniYAML lint passed with zero warnings/errors;
`git diff --check` passed.

### Final Game 1 — Stealth sustained combat

Artifact: `.build/cnc96a-natural-cycle/final-game1-strict3` (seed 96215). The
ordinary/all-module game used VIKI spawn 1 against allied Brutalis spawns 20 and
18. Two uncommanded VIKI STNK and two enemy Harvesters began 12-17 cells apart;
all subsequent specialist decisions/orders came from the ordinary bot modules.
The engine reached tick 5100 under the 120-second launcher bound with exit 0 and
no exception, fatal Lua error or desync.

The first attributed STNK hit occurred at tick 246. The exact injected Harvesters
died at ticks 428 and 876. Totals at tick 5000 were 6 attributed damage events,
105700 damage and 2 meaningful/valuable kills; both STNK remained alive. The
first attacked Harvester survived retreat completion at tick 300 as the incumbent,
then was correctly absent after its death at tick 500. The second survived
completions at ticks 700 and 875 before dying. Later natural targets also remained
incumbents across repeated safety retreats, demonstrating sustained engagement
rather than movement telemetry alone.

- Luna narrative: `.build/cnc96a-natural-cycle/final-game1-strict3/NARRATIVE.md`
  — PASS.
- Separate Luna policy review:
  `.build/cnc96a-natural-cycle/final-game1-strict3/POLICY-REVIEW.md` — PASS,
  medium-high confidence, no blocker. Provenance and lack of a terminal winner are
  non-blocking evidence limits: raw engine Lua/debug logs and bounded completion
  directly support the claimed combat outcome.

### Final Game 2 — distinct Chemical crossfire

Artifact: `.build/cnc96a-natural-cycle/final-game2-strict1` (seed 96220). This
distinct ordinary/all-module topology placed VIKI at spawn 10 against allied
Brutalis at spawns 11 and 12. Two uncommanded CTNK and two enemy Harvesters used
the opposite central corridor. The shared Chemical-profile module reserved one
specialist while leaving one for the ordinary army, preserving the ownership
split. The engine reached tick 5100 under the 120-second bound with exit 0 and no
integrity fault.

The first attributed CTNK hit occurred at tick 342; the two meaningful Harvesters
died at ticks 427 and 649. Exact tick-5000 totals were 17 damage events, 72257
damage and 2 meaningful/valuable kills, with both CTNK alive. The module continued
ordinary missions afterward. This independently proves that the shared Chemical
lifecycle remains active and its configuration-only distinction was not regressed.

- Luna narrative: `.build/cnc96a-natural-cycle/final-game2-strict1/NARRATIVE.md`
  — PASS.
- Separate Luna policy review:
  `.build/cnc96a-natural-cycle/final-game2-strict1/POLICY-REVIEW.md` — PASS,
  medium/high confidence, no blocker. The unticked first ownership snapshot,
  scratchpad omission note and absence of a winner claim are non-blocking; the
  direct damage/kills and ordinary/all-module distinction satisfy this cycle.

Calibration/setup artifacts were not counted: pre-engine content/Lua misses, a
bounded observer-memory diagnostic, the unmodified reproduction, a duplicated-
callback observer run, one tick-0 unsupported-ActorID run, and one run that did
not register WorldLoaded targets. Exactly the two final artifacts above form the
acceptance count. Proposed status: natural-combat correction complete and ready
for fresh Terra review. No push, PR or merge was performed; the coordinator owns
the successor hotfix publication.

## Exceptional finish-target correction

The unmodified reproduction showed reveal-triggered retreat before completion:
retreat tick 225 preceded hit 246, retreat 400 preceded kill 428, and retreats
600/800 preceded kill 876. `RunEngagementSafety` treated each reveal as an
immediate strategic-retreat trigger. The narrow correction retains a valid
selected target through reveal and invokes the existing exact one-strategic-cell
retreat after target invalidation/completion. All earlier safety, repair,
ownership, reinforcement, persistence, configuration, Air, and balance behavior
is preserved. The new pure regression covers live/completed/absent/disabled cases.

Game 1 (`.build/cnc96a-finish-target/final-game1-v2/cnc96a-finish-game1`, seed
96301) reached tick 5500 in 25.035 seconds, exit 0. At tick 5000 it recorded 29
unique Harvester kills, 78 damage events, 1,225,614 damage, three survivors,
Level 2/672000 XP, 15 completion retreats, 34 retained reveals, exact one-cell
geometry, and no stop/cancel/idle gap. Its fresh Luna `NARRATIVE.md` and separate
`POLICY-REVIEW.md` both PASS/no blocker.

Game 2 (`.build/cnc96a-finish-target/final-game2-v4/cnc96a-finish-game2`, seed
96302) used a distinct two-sided open corridor and reached tick 6500 in 30.03
seconds, exit 0. At tick 6000 it recorded 26 unique Harvester kills, 65 damage
events, 1,173,828 damage, eight survivors, Level 3/675000 XP, 16 completion
retreats, 24 retained reveals, exact geometry and continued operations. Fresh
Luna `NARRATIVE.md` and separate `POLICY-REVIEW.md` both PASS/no blocker.
Per-actor attribution and terminal roster detail are bounded advice. Exactly
these two ordinary/all-module games count; all calibration attempts are excluded.

The supplemental replay was frozen at PR120 head
`9dbd02cd85caed85e99de89ee8642fd7b122a4e5`, blob
`d276947fe3e9acc3227ec241e5339a2e7ce487d2`, SHA-256
`becb5ead52faab4e83b789a79fbeb742ed2feb62655aac6d79887030fc7f8584`.
Deterministic playback passed FinalGameTick 16853 to world tick 17196, exit 0,
without OOS/desync. Temporary instrumentation was fully restored. Durable neutral
evidence is `.build/cnc96a-finish-target/human-calibration/ENRICHED-TIMELINE.md`;
the superseding blind Luna narration is sibling `ENRICHED-NARRATIVE.md`. It
records six directly credited Harvester kills, six crushes, tank/structure kills,
continued victory operations, Level 3/675000 XP, and three observed slot-0 STNK
losses in ticks 8495-16856. It therefore does not support the informal one-loss/
all-harvester claim while strongly supporting sustained finish-target combat.

After probe restoration, Release build passed with zero warnings/errors, focused
policy tests passed 107/107, full CNC MiniYAML passed, and `git diff --check`
passed. Proposed status: complete and ready for fresh Terra review. No push, PR,
merge, or external process was used.


## Final natural-acquisition correction

The unmodified ordinary economy reproduced the reported acquisition gap before
editing. It reached tick 18100 in 76.082 seconds and naturally produced both
specialist profiles. Eligibility, ownership, grouping, target categories,
hostility, routes and orders were all active: the Stealth module submitted 59
Harass targets/hazard routes and logged seven all-dangerous rejection scans.
Nevertheless, the first sustained proximity window recorded 28/28 and then
30/30 samples without new attributed damage.

The exact defect was in the 25-tick local scanner. It found nearby candidates,
but if the group already held any strategic target it skipped fresh evaluation
unless that same target was nearby. A far incumbent could therefore suppress a
strong local Harvester opportunity. Air's target lifecycle keeps the incumbent
in challenger evaluation and applies the configured meaningful-improvement
threshold. The correction does exactly that for the local Stealth/Chemical
scanner: nearby candidates plus one live incumbent pass through the existing
Stealth priority, safety, route, order and Air-shared 25% switch policy. It does
not copy Air target priorities/scoring, change Air output, or change configuration,
cadence, balance, weapon/ground rules, save/load, retreat, repair, reinforcement
or ownership behavior.

Two focused regressions cover distant-incumbent inclusion, no duplication, and
the exact existing 124%-retain/125%-switch boundary.

### Counted final Game 1 — sustained natural economy

Artifact:
`.build/cnc96a-natural-acquisition/final-game1-strict2/cnc96a-natural-acquisition-final-game1-strict`
(seed 96501). This was one ordinary VIKI versus two allied ordinary Brutalis on
normal economies with all modules. Specialists and Harvesters were AI-produced;
there were no injected actors, waves, combat orders, passive bots, forced targets
or scripted combat. It exited 0 at tick 21100 in 113.154 seconds without
exception, Lua failure or desync.

At tick 21000 the identity-stable observer recorded 119 attributed specialist
damage events, 309781 damage and five distinct ordinary Harvester kills. STNK
kills occurred at ticks 14061 and 14284; CTNK kills occurred at 17561, 17575 and
20100. Product logs independently record the two STNK Harvester
target-completion→exact-retreat cycles, 89 nearby observations, 17 retained
reveals, eight all-dangerous waits, later missions/production, and level-1
veterancy. Five is reported as the exact observation, not an authored minimum.

Fresh blind Luna narrative:
`.build/cnc96a-natural-acquisition/reviews/game1/NARRATIVE.md`.
The usable fresh policy receipt is
`.build/cnc96a-natural-acquisition/reviews/game1/POLICY-REVIEW-CORRECTED.md`:
PASS-WITH-NOTES, no implementation change required. An earlier policy receipt is
uncounted because worker-authored context incorrectly declared five a threshold;
the corrected reviewer did not read it and explicitly applied no numeric gate.

### Counted final Game 2 — distinct close-economy isolation/control

Artifact:
`.build/cnc96a-natural-acquisition/final-game2-close/cnc96a-natural-acquisition-final-game2`
(seed 96522). VIKI spawn 1 faced allied Brutalis close spawns 2/4. The ordinary
all-module economies produced and controlled all specialists and Harvesters; no
actor was preplaced or injected and no target/combat choreography ran. It exited
0 at tick 16100 in 57.056 seconds with clean runtime integrity.

Chemical specialists recorded first Harvester damage at tick 10918, 201781 total
damage and four distinct Harvester kills. Stealth completed one Harvester mission
and retreat, issued 29 nearby reactions, and switched a distant
`harv#1016` incumbent to nearby `harv#974` at tick 13575 (distance five)
without Stop/cancel/idle-gap telemetry. Four all-dangerous waits, repeated local
safety Stops, 78 safe-nearby and 386 unsafe samples provide the defended adverse
control. The end roster had no surviving STNK and no attributed STNK damage, so
the profile-specific combat/survival limit is retained explicitly.

Fresh blind Luna narrative and separate policy receipt:
`.build/cnc96a-natural-acquisition/reviews/game2/NARRATIVE.md` and
`POLICY-REVIEW.md`. Policy verdict PASS-WITH-NOTES/no blocker. It recommends
future stronger per-opportunity latency/threshold, exact incumbent-distance and
Stealth loss-ledger evidence; these are evidence improvements and were not used
to broaden product code.

All observer/setup failures, the diagnostic-log NRE, timed-out extension,
weaker natural seed, and unsafe preplacement calibration are excluded. Exactly
the two artifacts above count.

Final protected checks passed: Release compilation and full CNC MiniYAML with
zero warnings/errors; focused `StealthTankSquadPolicyTest` 109/109; clean
`git diff --check`. Proposed status: correction complete and ready for fresh
Terra review. No push, PR, merge, external agent/process, task-sheet edit, or
unrelated change was performed.
