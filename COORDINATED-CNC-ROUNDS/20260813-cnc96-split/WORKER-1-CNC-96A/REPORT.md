# CNC-96A worker report

## Status

Cycles 1-3 implemented and checked. Cycle 3 ran two distinct adversarial full-engine
games and one fresh factual Luna narrative for each. Proposed status remains
`First iteration - testing`, stopped at the required user manual-policy gate. The
Air-shaped coarse routing removes the measured multi-second specialist hitch while
the reachable case preserves orders, target damage/destruction, survival, and recovery.

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
