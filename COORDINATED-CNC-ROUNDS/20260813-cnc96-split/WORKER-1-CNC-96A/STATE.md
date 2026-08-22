# Worker State: CNC-96A

Reread this file after compaction, before each cycle, after its game analyses,
and before publication. It is the complete assignment. Do not read the task
sheet, coordinator state, other skills, or other worker specs. Read applicable
`AGENTS.md`; inspect another task PR only when named under Dependencies.

## Assignment

- Worker/task: `WORKER-1` / `CNC-96A — Stealth squad performance using AirSquad as gold standard`
- Change category: `AI performance and bounded architecture, with player-visible tactical behavior preservation`
- Balance authority: `Frozen. Do not change cost, HP, damage, armor, speed, timing, power, prerequisites, probabilities, resources, production fractions, squad composition, target priorities, threat buffers, scan/order cadence, or candidate/group bounds.`
- Status: `cycle complete — merged PR128 order-churn correction ready for fresh Terra review and user manual gate`
- Base branch/SHA: `origin/bleed` / `24404606a28522a0a7e66bb5460abd718b5247e1`
- Task branch / PR base: `agent/20260822-cnc96a-order-churn` / `origin/bleed`; PR #128 is merged
- Current cycle: `authorized live-human CNC-96A correction after merged PR128`
- Required model: cycle 1 `Sol high`; cycles 2-5 `Terra medium`; cycles 6-15
  `Luna medium` only when coordinator authorizes minor obvious work; at most two
  exceptional `Sol medium` escalation cycles may follow only a critical blocker
  that makes safe release or engine execution impossible
- Game/build capacity: `2` / `1`; lock: `/root/github/LibertyDawn/.agents/locks`
- Report: `COORDINATED-CNC-ROUNDS/20260813-cnc96-split/WORKER-1-CNC-96A/REPORT.md`
- Analysis directory: `/root/github/LibertyDawn/.build/coordinated-cnc/20260813-cnc96-split/WORKER-1-CNC-96A/analysis`
- Design: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Policy scratchpad/lock: `/root/github/LibertyDawn/.agents/references/LIBERTY-DAWN-POLICY-SCRATCHPAD.md` /
  `/root/github/LibertyDawn/.agents/locks`
- Games completed: `cycle 1: 2 distinct changed-build scenarios plus matched exact-base controls; cycle 2: 2 distinct changed-build scenarios; cycle 3: 2 distinct final-source changed-build scenarios; cycle 4: 2 valid bounded changed-build scenarios plus 2 tick-0 fixture failures excluded from the game count; cycle 5: exactly 2 valid bounded changed-build scenarios plus 1 tick-0 fixture failure excluded from the game count; cycle 6: exactly 2 valid bounded changed-build scenarios plus 1 tick-0 Lua fixture failure excluded from the game count; cycle 7: exactly 2 valid bounded changed-build scenarios plus 1 tick-accounting-0 Lua telemetry failure excluded from the game count; cycle 8: exactly 2 valid bounded changed-build scenarios; cycle 9: exactly 2 valid bounded changed-build scenarios; cycle 10: exactly 2 valid bounded changed-build scenarios plus one pre-completion Lua telemetry failure excluded from the game count; cycle 11: exactly 2 valid bounded changed-build scenarios; cycle 12: exactly 2 valid bounded changed-build scenarios plus setup/observer calibration attempts explicitly excluded; merged-PR128 hotfix: exactly 2 valid bounded natural games plus matched baseline/diagnostic/far-route candidates explicitly excluded`;
  cycle-3 review: `one advisory, partly adopted`; PR: `none`

Each worker invocation performs only the current authorized cycle, updates this
file/report, and exits. Do not loop into another model tier in the same context.
For this task, the user's manual policy review is also a hard inter-cycle gate:
after the cycle's games are narrated, stop and return the narratives and factual
test summary. Do not make another code decision or start another cycle until the
coordinator records the user's review and authorization.

## Integrated assignment

- Phase/release head: `isolated` / `not assigned`
- Repair branch/base: `not assigned`
- Release-wide integration cycle: `not assigned; maximum 5`
- Integrated role model: `Terra medium for integration cycles 1-5; Sol medium only
  for an explicitly authorized blocker escalation`

The Terra Integrator fills these fields before combined testing. Integrated work
uses this normal worker's same task boundary, canonical game launcher, installed
content staging, preflight, two-scenario contract, and minor-fix restraint.
Prefer `launch-ai-parallel.py --content <installed-runtime-content>` and verify
the isolated `SupportDir/Content` target before every game; an incomplete custom
launch command is not an integration setup. Integrated cycles retain the no-
automated-match-policy-review and manual-review gate.

## Why and predicted change

CNC-96 repeatedly nominated the shared 75-tick Stealth/Chemical module boundary
as a major periodic simulation-tail owner. In a two-Iron-Reaper, 600-mobile-actor
control, enabled tick 76 contained about `365ms + 112ms` of the two players'
specialist spans; disabling both traits moved that boundary near `67ms` but also
removed ordinary AI work and was not a valid fix. Later pressured games measured
about `8.9–11.1s` aggregate specialist work over 2,200 ticks and individual spans
above `1.25s`. The old implementation repeatedly constructs full enemy/threat
views, evaluates candidates against all threats, performs exact per-unit ground
path searches, and refreshes orders. AirSquad instead shares a bounded strategic
influence cache, separates cheap live safety from strategic reconsideration,
maintains stable target/route progress, and submits route orders on meaningful
state changes.

The predicted observable change is materially lower Stealth/Chemical planning,
allocation, pathfinding, queued-order, and p95/p99/max tick cost, with fewer
player-visible periodic hitches, while ordinary AI specialists still make useful
safe progress and damage using their existing distinct harassment/attack roles.
The work is not complete if cost merely moves to another manager, if improvement
falls inside repeated-control variability, or if specialists idle, lose value,
react late, or violate their current safety/movement policy.

## Authoritative behavior

1. Compare Stealth/Chemical directly with the efficient AirSquad implementation.
   Attribute strategic world-view/cache construction, candidate evaluations,
   routes/path searches, local safety checks, plan transitions, queued order
   batches, CPU/allocations, tick tails, and player-visible outcomes. Aggregate
   `SquadManagerBotModule` timing is not direct AirSquad evidence.
2. Determine the evidenced reasons Stealth is more expensive, then implement the
   smallest AirSquad-shaped simplification that materially improves them. Prefer
   a local bounded lifecycle before extracting common machinery; extraction is
   allowed only if a clean common seam prevents material duplicated work.
3. Separate infrequent bounded strategic planning from cheap current local
   safety. A still-safe, useful plan may persist between reconsiderations, but a
   nearby detector/weapon/hazard or meaningful invalidation bypasses stale state.
4. Preserve all current actor eligibility, group assignment and roles; separate
   Stealth Tank and Chemical target restrictions/priorities; cloak/detector and
   armed-threat meaning; careful and weakest-defender clearing; target-switch
   semantics; resource/pending-explosion safety; ground locomotor/domain/blocker
   reachability; harvester-field waiting; reservations; deterministic ordering;
   scan/order cadence; and configured candidate/group bounds.
5. On reconsideration, the unchanged current eligibility, priority, threat,
   hazard, bound, and cadence rules apply. Bounded staleness may change an exact
   target/waypoint only when choices are policy-equivalent and the retained plan
   remains safe and useful; it must not become a hidden target-policy change.
6. Invalidate or cheaply repair a plan on target death/invalidity or meaningful
   movement, newly local danger, volatile resource hazard, route failure/blockage,
   lack of progress, membership loss/addition, transport or other reservation
   change, and required role recovery. State-change-driven ordering must resume
   promptly and deterministically after the hold/blocking reason disappears.
7. A safe hold is acceptable only with a narrated current reason and bounded
   reconsideration. A safe reachable opportunity plus no progress/damage and no
   valid hold reason is passive idling and a failure.
8. The behavior applies to every configured ordinary CNC bot that can receive
   `stnk` or `ctnk`: Cabal, Watson, HAL 9001, Brutalis, VIKI, SkyNet, IronReaper,
   WaveMaker, Easy, and Easiest. Rotate these bots across the difficulty ladder;
   do not special-case the diagnostic IronReaper example.

## Forbidden behavior and failure signals

- Never restore, rename, or disguise the discarded `UseScanLocalThreatSnapshot`
  optimization removed by `0c9a5c1`. It changed decisions/workload and is rejected
  evidence, not a starting implementation.
- Do not manufacture improvement by disabling Stealth/Chemical or another module;
  changing cadence, phase, candidates, group limits, balance, composition,
  priorities, threat buffers, or production; weakening route/safety checks; or
  reducing the required actors/workload.
- Do not transplant Air flight routing into ground specialists without locomotor,
  domain, blockers, detectors/cloak, Tiberium, pending explosion, and exact
  reachable-approach handling.
- Do not cache volatile actor state as current without bounded expiry,
  conservative moving-threat coverage, cheap live checks, and explicit
  invalidation. Do not rebuild an ostensibly shared cache per group/profile.
- Do not retain per-candidate/per-threat and per-unit exact path work or repeated
  equivalent order batches behind a cosmetic abstraction. Do not introduce a
  global optimizer, graph solver, rigid partition, or elaborate planner without
  game evidence that the smaller AirSquad-shaped lifecycle cannot meet the task.
- Fail on shifted cost, increased allocation/order storms, exceptions/desyncs,
  late danger response, unsafe hazard crossing, lost target-role identity,
  reservation conflict, unrecovered stalled/invalid plans, increased valuable
  losses, passive idling, or missing useful damage/progress.
- Requests, cache hits, selected targets, routes, reservations, movement, and log
  lines are not acceptance without the task-specific visible outcome and material
  performance improvement.
- Do not run automated match Policy Reviewers. This task uses one fresh Luna
  Commenter per game, then the user's manual policy review after the complete
  narrated round. An automated review is a process violation even if advisory.

## Current implementation and old-behavior control

- At base `0c9a5c1`, `StealthTankSquadBotModule` is instantiated twice from
  `mods/cnc/rules/ai.yaml`: `stnk` has up to two harassment groups plus an attack
  group; `ctnk` has one harassment group. Both use `ScanInterval: 75`,
  `OrderInterval: 75`, and `MaximumTargetCandidates: 48`; all ten bots enable both
  traits when their condition is active.
- Every active scan rebalances actor-ID-sorted eligible units; materializes and
  actor-ID-sorts every enemy; derives armament/detector facts for every threat;
  and updates each active group. Each group scores the enemy set before taking 48
  candidates, scans the threat list per candidate, may scan nearby infantry, and
  may make additional defender-clear reachability checks.
- Hazard-aware issue runs one exact ground `PathSearch` per eligible unit. Its
  cell predicate checks resource/pending-explosion hazards and can scan all threats
  for every visited cell; each route queues several moves plus an attack. Unchanged
  targets may be reconsidered/reordered on the 75-tick boundary. Only resource-
  hazard cells have a scan-local cache; there is no stable group plan/route lifecycle.
- AirSquad's comparison architecture is in `Squads/States/AirStates.cs`,
  `Squads/Squad.cs`, and `SquadManagerBotModule.cs`: a per-manager/profile coarse
  influence cache with 125-tick expiry and a moving-threat buffer; bounded strategic
  cell selection before route calculation; a separate 25-tick local safety check;
  explicit target/progress/route/invalidation state; and one-shot shared route
  batches with local handling for repair, ammo, reinforcements, and danger.
- The primary old-policy control is the exact base SHA `0c9a5c187d6bd3c354921855f19a4fb3590d6f06`.
  Prefer a same-build custom-map feature-disabled switch that executes the exact
  old path while retaining the new bounded diagnostics. Otherwise use an isolated
  base worktree. Match map bytes, Lua, seed, starts, factions, bot types, options,
  initial actors/resources, content, affinity, launcher, and enemy pressure.
- Establish repeated unchanged old-control variability before declaring a material
  gain. CNC-96 cycle 10 showed that superficially matched old-path runs could
  diverge in target/order traces and terminal workload; exact aggregate parity is
  not assumed. Use deterministic scenario acknowledgements and compare direct
  task events and distributions, not a single lucky maximum.

## Likely wrong approaches and challenges

- A scan-local position/target-type snapshot is already falsified. A few property
  reads were not the whole cost, and staleness altered decisions.
- Simply stretching/rephasing the 75-tick boundary trades urgent response for a
  prettier graph and is outside authority.
- Coarse candidate cells can become a hidden target-policy reduction. Keep the
  configured 48-candidate bound and unchanged priority semantics; use coarse work
  only to avoid expensive routing of equivalent locations, and prove important
  target classes still enter reconsideration.
- The common strategic cache may safely share bounded world facts, not chemical-
  versus-stealth judgments. Profile convergence is a policy regression.
- Ground routes are dynamic: blockers, domains, moving units, resource changes,
  and pending explosions make long-lived exact path reuse unsafe. The stable
  lifecycle needs cheap validation and narrow repair, not blind replay.
- Full engine outcomes vary under combat. Direct phase counters, deterministic
  script acknowledgements, repeated controls, and comparable event timelines are
  needed to distinguish architecture benefit from changed downstream workload.
- Diagnostic formatting, per-actor logs, contention, or benchmark overhead can
  dominate small improvements. Keep counters aggregate/bounded and do not call a
  debug-heavy or contended run golden.
- `StealthTankSquadBotModule.cs` already mixes reservation, grouping, target
  policy, danger geometry, exact routing, orders, waiting, and diagnostics. A
  focused split into strategic facts/plan lifecycle/local safety/execution may be
  warranted, but broad framework churn or AirSquad changes are not.

## Competing systems and ownership

- `StealthTankSquadBotModule` owns specialist selection, reservation, roles,
  targets, threat/hazard safety, group plan lifecycle, and specialist orders.
  Tunable authored policy remains in `mods/cnc/rules/ai.yaml`; algorithmic cache,
  invalidation, route, and order invariants belong in code.
- The two configured Stealth/Chemical instances share the implementation but not
  eligible actors or target policy. They may share immutable bounded world facts;
  they must retain distinct interpretations and performance identities.
- `SquadManagerBotModule` owns ordinary ground/air squads. It removes actors
  reserved by any `IBotUnitReservations`; its attack response, role assignment,
  target work, and queued orders must not reclaim or double-order specialists.
- `TransportManagerBotModule` can rescue blocked `stnk` and `ctnk`, reserves both
  passenger and carrier, respects other unit reservations, and runs at a related
  75-tick cadence. Stealth currently refuses transport-reserved actors. Exercise
  both same-boundary orderings and require one clear reservation owner/handoff.
- `CrateCollectorBotModule` can use broad mobile collectors in an emergency. It
  respects Stealth reservations for new assignments, but a pre-existing committed
  assignment is a competing order/ownership edge that must be diagnosed and not
  silently overwritten.
- `EconomyTroopProductionBotModule` counts unreserved `stnk`/`ctnk` as direct-fire
  frontline value for Brutalis/IronReaper Economy III and can alter owned production
  requests. `UnitBuilderBotModule` and ordinary build fractions own queues/cash and
  produce the actors. Preserve reservation visibility and production accounting;
  do not tune queues or cash.
- Other reservation managers at this base use non-overlapping configured actor
  types for their ordinary roles (field defense `mtnk/e1/msam`, tank harassment
  `mtnk`, covert `bike/bggy/arty`, artillery `mlrs/msam/mtnk/e1`, early infantry,
  mammoth, harvester/red-bomb/capture roles), but all remain enabled in games and
  compete indirectly for cash, queues, targets, threats, orders, and simulation
  time. Confirm their calls/errors and unexpected reservation owners.
- Building repair does not repair these mobile specialists, and the Stealth module
  has no AirSquad-style mobile repair lifecycle. Adding one is outside scope.

## Dependencies

- Required base is diagnostic head `0c9a5c1`. Preserve its opt-in `periodic-stall-v1`
  reporting, paced rendered launcher support, logic/module attribution, and stable
  per-instance `IBotPerformanceIdentity` labels from the relevant CNC-96 commits
  `f5fc1d9057`, `1f3febbf00`, `57b02e8d97`, and `6fd71cf9e4`.
- `0c9a5c1` deliberately removes the unproven scan-local threat snapshot from
  `6fd71cf9e4`; never resolve or cherry-pick in a way that resurrects it.
- No worker PR is a code prerequisite. CNC-96 diagnostics recorded CNC-100 at
  `886519f69d` as an unmerged advanced-squad change that can alter CPU load/timing.
  Do not read its worker state. If that commit or a successor enters the eventual
  PR/integration base, inspect only the named commits/diff and rerun all controls at
  the combined head; old standalone timings are stale. Expect overlap in
  `SquadManagerBotModule.cs`, `Squads/Squad.cs`, `mods/cnc/rules/ai.yaml`, tests,
  or advanced-squad instrumentation and escalate semantic conflicts.

## Spec policy consultation

- Partial spec: `COORDINATED-CNC-ROUNDS/20260813-cnc96-split/WORKER-1-CNC-96A/SPEC-POLICY-NARRATIVE.md`
- Sol-high review/verdict: `COORDINATED-CNC-ROUNDS/20260813-cnc96-split/WORKER-1-CNC-96A/SPEC-POLICY-REVIEW/POLICY-REVIEW.md` /
  `mostly sensible (high confidence, advisory)`
- Adopted hypotheses: `AirSquad's infrequent bounded strategy + cheap live safety + stable plan + state-change order pattern fits the specialist purpose; preservation must be explicit; local danger and meaningful invalidations bypass cached strategy; accept exact target/waypoint differences only between policy-equivalent safe/useful choices; narrate target identity, holds, trigger-to-reaction timing, damage, losses, progress, and orders; measure repeated controls before materiality; test identity/live safety, blocked recovery, and matched saturation.`
- Rejected/deferred advice and why: `No substantive recommendation rejected. Shared extraction is deferred unless direct measurement shows the smallest local lifecycle still duplicates material work. Numeric materiality and response thresholds must be derived from repeated old controls, not invented as new gameplay policy.`
- Scratchpad update: `Validated the reviewer replacement (UTF-8, under 3,000 characters) and atomically promoted its bounded-staleness principle under the one-slot lock. A concurrent valid policy update was then merged under the same lock so neither general principle was lost.`

## Acceptance plan

- Literal player-visible result: In player-created full-engine scenarios, each
  ordinary AI's Stealth Tank and Chemical specialists retain distinct useful
  harassment/attack behavior: select eligible current targets, avoid or respond
  to current detectors/armed threats and Tiberium/pending hazards, route over
  reachable ground, wait only for a visible safety reason, recover from target/
  route/member/reservation changes, and produce useful progress/damage or a valid
  weakest-defender clear. Under matched load this occurs with materially less
  direct planning/path/order cost and fewer/lower periodic simulation lag spikes
  than old behavior, beyond repeated-control variability.
- Focused checks/instrumentation: Add only bounded aggregate attribution needed to
  distinguish cache build/hit/expiry, candidate shortlist/evaluation, threat/local
  safety checks, exact route searches/cells, invalidation reason, retained/changed
  plan, order batch/waypoints, request/rejection, reservation owner/handoff,
  target/progress/damage/loss, module CPU, allocation/GC, and completed tick tails.
  Separate AirSquad planning/order work from aggregate SquadManager and preserve
  `stealth-tank` versus `chemical` identities. Use deterministic script markers for
  map, options, seed, bots, factions, actors, threats/hazards/blockers, event ticks,
  damage/loss/arrival, world ticks, and exit. Remove noisy temporary diagnostics
  before publication; keep only low-overhead opt-in counters with clear ownership.
- Two-or-more distinct games per cycle: Every cycle runs at least two materially
  distinct custom scenarios, not two seeds of one setup. Scenario A is the matched
  saturation/performance matrix: two ordinary Iron Reapers, each with at least 300
  representative units plus structures and matched active Air, Stealth, and
  Chemical formations; pre-Codex, newest advanced squads disabled, and newest
  enabled legs, each <=120 seconds, serial/uncontended. Scenario B is a distinct
  identity/live-safety or blocked-recovery game with ordinary enemy AIs, all
  features/modules, changed-versus-old legs, and <=120 seconds per game. From test
  1 use full engine, normally headless MAX. The first changed test is a matched
  changed-versus-old pair. A paced rendered companion is required before final
  acceptance to verify the player-visible hitch reduction. Each individual game
  receives its own fresh Luna Commenter; no match Policy Reviewer is launched.
- Old-control comparison/metrics: Prefer a same-build custom-map feature-disabled
  exact old path; otherwise use isolated `0c9a5c1`. Keep map/Lua bytes, seed,
  starts, factions, bots, options, resources, actor IDs/counts, event schedule,
  content, launcher, affinity, and duration matched. Repeat unchanged controls to
  establish variability. Compare direct Air/Stealth/Chemical planning CPU and max
  spans, cache/local checks, exact paths/cells, plans/order batches, tick mean/
  p50/p95/p99/max/spike count, process CPU, allocations/GC, peak memory, actors/
  effects, errors/stalls, useful damage/kills/progress, survival/value losses,
  response timing, idle/hold time, and reservation ownership. Require a material
  direct specialist and tail improvement outside control variability with no
  shifted cost or meaningful gameplay regression; parity, marginal gain, or loss
  requires investigation/correction or a concrete task-specific explanation.
  Retain the template's pre-Codex historical leg at `8024fd2c6f377fc0744777b52daef3b7a8a4682f`
  if it can run the exact matrix. CNC-96 found it lacks the modern IronReaper/
  launcher contract; do not fake compatibility with product backports. Record that
  inherited incompatibility explicitly and never mislabel `0c9a5c1` as historical.
- Adversarial cases: Build the ladder across cycles: (1) matched saturated open
  connected workload; (2) connected identity/live-safety with both vehicle/
  structure opportunities and infantry clusters, moving detector/weapon screens,
  changing Blue Tiberium/pending explosion, target death/movement, and member loss;
  (3) Archipelago/island or hard-blocked topology with an unreachable high-value
  target and useful reachable work elsewhere, then opened route or released
  reservation; (4) transport rescue and committed/emergency crate competition;
  (5) all-defended patience followed by a safe opening and weakest-defender clear;
  (6) low/high specialist counts, reinforcement/loss, enemy pressure, duration,
  and save/load during a plan if state persists beyond a tick. Rotate all ten bot
  personalities before acceptance. For each game pre-record failure hypothesis,
  stressed condition, expected failure signal, and visible pass evidence.
- Final regression: Release build and focused tests pass; both Stealth/Chemical
  configs and all ten bots load; deterministic policy helper/invalidation/order
  tests pass; two final distinct all-module custom scenarios and the saturated
  matrix pass within bounds; paced rendered and headless MAX results agree on the
  task outcome; connected/blocked topology, reservations, target-role identity,
  hazards, live response, recovery, damage/survival, and performance all pass;
  save/load passes if applicable and is not sole acceptance; no exception/desync,
  noisy diagnostics, raw artifacts, or unrelated diff remains. Fresh Commenter
  narratives and the user's recorded final manual policy review agree that useful
  behavior is preserved. PR/CI pass; never merge the PR.

## Implementation rules

- Investigate code, history, configs, tests, and evidence; choose the smallest safe
  solution. Preserve unrelated behavior and user changes.
- Before coding, write the cycle's explicit preservation table: every current
  eligibility, priority, threat, hazard, group/candidate bound, cadence, response,
  and invalidation semantic remains unchanged. Derive response/materiality bounds
  from repeated old controls rather than inventing gameplay timing.
- Keep responsibilities separate and ownership explicit. Prefer short cohesive
  functions/classes; split mixed or oversized logic when it improves clarity,
  testability, or hot-path cost without unrelated churn.
- Prefer simple fuzzy thresholds and game-sensible rules of thumb. Avoid global
  optimizers, graph solvers, rigid partitions, and elaborate state unless tests
  prove a simpler priority, count, distance, threat-map, or cooldown insufficient.
- Put tunable policy in owning rules/config and invariants in code. Do not hide
  production policy in tests or duplicate it across AI personalities.
- Freeze balance exactly as stated above. Never alter a gameplay or strategy value
  to make behavior or performance pass; that invalidates evidence.
- Add proportionate focused tests. Log actionable handled errors at their owning
  boundary; never swallow failure, fake success, spam per tick/actor, or publish
  noisy temporary diagnostics.
- Keep simulation work bounded: avoid repeated full-map scans, uncontrolled
  allocation, nondeterministic ordering, unbounded retries, and heavy logging.
- Treat AirSquad as Liberty Dawn's gold standard. Reuse or extract its shared/
  cached bounded strategic planning, cheap responsive local checks, stable plan
  lifecycle, and state-change-driven orders before inventing a separate planner.
  Stealth/Chemical should differ mainly in eligible actors, target/threat/detection
  policy, and unavoidable ground/resource movement constraints; require measured
  value and CPU evidence for broader divergence.
- Inventory and exercise all modules competing for actors, queues, cash,
  reservations, repairs, targets, orders, or simulation time as listed above.
- Record out-of-scope ideas in the report's deferred section. Do not create a task,
  edit shared deferred work, task sheet, coordinator state, or `bleed`.

## One-cycle evidence loop

One cycle starts with a product/config change. Reading evidence, adding bounded
diagnostic setup before the implementation, or repairing an invalid harness is
not another cycle. For the current cycle:

1. Reread this state, diff, prior narratives/manual reviews, and unresolved evidence.
2. Make the smallest evidence-driven change and run relevant focused checks.
3. Run at least two materially different adversarial custom scenarios. Every game
   uses the full engine, all features/modules, and ordinary enemy AIs from test 1.
   Normally use headless MAX and stop each game at 120 seconds wall-clock; MAX may
   advance much farther in game time. Run the required performance-matrix legs as
   specified. These are focused games; winner/natural game-over is unnecessary.
   Making the intended map load is part of the task. A process that dies, hangs,
   or remains before world tick 1 is not a game and does not count.
4. Before each game record its failure hypothesis, changed pressure/assumption,
   exact failure signal, and player-visible pass evidence. Vary geometry, timing,
   resources, losses, counts, topology, competing managers, control setting,
   save/load, or duration. Never spend both scenarios on near copies.
5. Give each game—not a batch—to its own fresh Luna Commenter. Stage only the
   game's authorized artifacts and short task context. Require factual narration
   of target/role identity, progress/damage/losses, deliberate holds and reasons,
   trigger-to-reaction timing, plans/path searches/order batches, module CPU and
   lag spikes. Verify the narrative facts. **Do not launch an automated match
   Policy Reviewer.** After all games are narrated, write the factual comparison,
   update report/state, and stop for the user's manual policy review. The next
   change/test cycle may incorporate only that recorded user direction.
6. Remove answered/noisy diagnostics, update journal/report/state, commit, and exit
   so the coordinator can present the manual-review packet and later choose the
   next authorized model tier.

Use `with_resource_slots.py` around shared resources and the game launcher/
supervisor as the completion helper. Await bounded results; do not burn turns
sleeping/polling. Isolate every map, support directory, port, log, replay, save,
benchmark, and display.

If setup diagnosis cannot make the full engine reach world tick 1, save exact
startup logs, command, process tree, and checkout comparison, then repair and
retry within this cycle. Do not give up early, repeat an identical broken launch
as a nominal test, narrate a nonexistent match, or claim acceptance. Only
exhausted authorized cycles may be handed off for help.

Custom setups force rare decisions while retaining real AIs/modules: pre-place
eligible specialists and Air formations, target classes, current/moving threats,
resource hazards, blockers, reservations, and scripted state changes. Absence of
an unfinished prerequisite behavior is a dependency, not proof this task failed.

For controls, prefer the same-build feature-disabled exact old path; otherwise
use the recorded base in an isolated worktree. Require material task-relevant
improvement, not activation logs. Treat repeated loss, parity, marginal gain, or
cost migration as likely code/policy error unless evidence supports a task-approved
tradeoff. Debug-heavy/contended runs are diagnostic only.

For the performance matrix, use two ordinary Iron Reapers with at least 300
representative units plus structures each. Run each pre-Codex/newest-off/newest-on
leg at most 120 seconds. Compare ticks/tails, direct planning/order work, CPU,
allocations/GC, peak memory, actors/effects, outcomes, and errors/stalls. Preserve
the known historical incompatibility honestly; never backport product behavior to
make the old tree appear matched.

## Model-tier limits

- Cycle 1/Sol high: implement the coherent initial bounded solution and make its
  first behavioral test the matched full-engine changed-versus-old pair.
- Cycles 2-5/Terra medium: correct evidenced bugs and wrong assumptions. Do not
  casually redesign. After cycle 3 obtain one fresh Luna code review with at most
  one advisory concern, record adoption/rejection, then continue to cycle 4 only
  after the user's manual match-policy review authorizes it.
- If unresolved after cycle 5, mark `Needs help` or `First iteration - testing`
  unless all remaining work is minor and obvious. Before cycle 5, workers may not
  surrender an authorized cycle; use it for diagnosis, repair, and valid reruns.
- Cycles 6-15/Luna medium require coordinator authorization. Only narrow guards,
  config mistakes, assertions, obvious local bugs, and testing are allowed. No new
  architecture, strategic policy, balance, or broad refactor. Stop when the next
  fix requires judgment. The manual policy gate remains mandatory each cycle.

## Analysis isolation

For each game, stage only authorized artifacts for its fresh Commenter, plus a
short task context (ID/title, literal behavior, why, category, in/out of scope,
balance authority). Use a strict launcher JSON envelope. Do not give a Commenter
implementation context or other games. Keep detailed analysis ignored; record
concise conclusions/paths in the report/state.

The spec-time Sol-high policy consultation recorded above is complete. It does
not authorize automated match Policy Reviewers. After each cycle, stage the two-
or-more verified Luna narratives and worker comparison for the user's manual
policy review. Record the user's exact decision, adopted recommendation and next
test/change, or rejection with concrete reason before another worker launch.

## Implementation and publication plan

1. Record the preservation table and repeated `0c9a5c1`/same-build old-control
   variability with bounded direct Air-versus-Stealth instrumentation.
2. Implement the smallest local AirSquad-shaped split between shared bounded
   strategic facts, per-group stable plans, cheap live safety/invalidation, and
   state-change-driven order execution. Do not revive the snapshot or tune policy.
3. Add focused pure tests for bounded selection/cache expiry, moving-threat safety,
   invalidation, deterministic ties, stable order decisions, and no-safe-route/
   reservation transitions; build CNC/shared engine only as required.
4. Run the full-engine matched pair from test 1, the required saturation matrix,
   and a distinct adversarial identity/recovery scenario. Get one Luna narrative
   per game, verify facts, update report/state, commit, and stop for manual review.
5. Across authorized cycles climb through connected/blocked geometry, hazards,
   losses/membership, reservations, all-defended/opening, bot personalities,
   save/load if applicable, longer duration, and paced rendering. Use user review
   and evidence to correct only the current task.
6. Remove temporary diagnostics, complete final regression and cycle-3/final code
   reviews, publish the task branch/PR to `bleed`, run CI/checks, record all evidence
   and the final user policy decision, and never merge.

## Publication

Propose `Complete - testing` only when literal focused acceptance, direct Air-
versus-Stealth comparison, repeated old controls, performance matrix, required
adversarial evidence, all-bot coverage, paced final regression, checks, report,
PR/CI, Commenter narratives, and user's final manual policy review pass. Otherwise
propose `First iteration - testing` with exact failures/risks. A final Terra code
review may return one compatible correction and consumes an available cycle.
Never merge the PR.

The report records behavior, design/assumptions, cycle count, game scenarios and
artifacts, per-game Luna narratives, user's manual policy reviews and dispositions,
old-control and direct Air comparisons, diagnostics, performance, checks/CI,
deferred work, and risks.

## Cycle journal

### Exceptional finish-target correction cycle (2026-08-21)

- Authority: native Sol-medium second/final exceptional release-blocking cycle
  from clean head `334189f2c4`. Diagnose before editing, make only the smallest
  correction, run exactly two distinct final full-engine games under 120 seconds,
  and give each counted game its own fresh native Luna factual narrative and
  separate serialized policy review.
- Required correction: specialist squads should generally finish the selected
  target before the one-strategic-cell post-mission retreat instead of retreating
  after each revealed shot. The calibrated natural 1-VIKI-versus-2-allied-Brutalis
  acceptance requires many unique Harvester kills, repeated target-completion then
  retreat cycles, continued operations, and veterancy where the engine/profile
  exposes it. Victory, 40+ kills, and zero losses are quality signals rather than
  unconditional gates. A materially distinct second game must exercise the same
  corrected lifecycle under different pressure/topology/timing.
- Diagnostic plan: reproduce the inherited reveal-to-retreat sequence on the
  unmodified head with direct specialist attack/damage/kill and decision timestamps;
  identify the exact trigger and target lifecycle before product edits. Movement
  and order telemetry remain diagnostic, not combat acceptance.
- Preservation table: actor eligibility, deterministic assignment/groups/roles,
  Stealth/Chemical shared lifecycle and configuration-only target differences,
  wall/value priorities, Air switch threshold, target-invalid and superior-target
  switching, detector/armed/resource/pending-explosion/local-safety vetoes, ground
  passability/routes, candidate/group bounds, 75/25-tick cadences, reinforcement
  staging/joining/save state, retreat geometry/barrier/persistence, repair/rejoin,
  reservation ownership/ordinary-army exclusion, CNC-101 behavior, Air output, and
  every balance/configuration value remain unchanged.
- Publication boundary: update only this STATE and REPORT, commit once, and return
  for Terra. No push, PR, merge, external agent, unrelated task, or task-sheet/
  coordinator edit. A human replay supplied during this cycle is calibration only
  and does not block current work.
- Game 1 pre-record: ordinary VIKI versus two allied Brutalis, all modules, six
  uncommanded STNK at meaningful distance from a 36-Harvester moving target field.
  Hypothesis: finish-target should replace per-shot oscillation with repeated real
  target-kill then one-cell retreat cycles. Failure is any retreat before a selected
  target's kill, fewer than ten unique attributed Harvester kills, no survivor/
  continued operation, or no veterancy progression where exposed. Pass evidence is
  direct hit/kill/retreat ordering, unique kills, survivor and Level/Experience logs.
- Game 2 pre-record: materially distinct two-sided crossfire geometry and different
  spawns/timing, with uncommanded STNK between separate Harvester waves owned by the
  two allied Brutalis players. Hypothesis/failure is the same lifecycle invariant
  under opposing target directions; require attributed combat, repeated completion-
  retreat cycles, ten unique kills, continued operation, and no safety/ownership/
  integrity fault. A preceding CTNK calibration was excluded because its frozen
  profile intentionally disables reveal retreat; its 31 kills/Level 3 preserve the
  shared Chemical lifecycle but cannot directly prove this correction.

## Fresh authoritative amendment: natural nearby target acquisition (2026-08-21)

PR #127 (`Finish CNC specialist targets before retreat`) merged into `bleed`.
Its successor must target `bleed` at exact head
`17835e8da1a45b80e9e0675d28df8dbdb22ddf29`; do not reopen PR #127 or create a
new task or round. The human reports ordinary enemy harvesters harvesting beside
Stealth Tanks while tanks stand idle. Prior preplaced groups/timed waves proved
firing and retreat mechanics but not spontaneous natural acquisition.

Diagnose on current `bleed` before editing, tracing every rejection layer:
owned/eligible/claimed/grouped state, nearby scan, category, hostility/
visibility/range, threat/safety, route, order issuance, and weapon viability.
Use Air squad AI target acquisition as the golden reference for scan cadence,
nearby acquisition, visibility/hostility/category filtering, safety rejection,
route availability, retention, and order issuance. Align where justified, but
preserve ground/weapon differences and Stealth-specific priorities; never copy
Air scoring/config blindly.

Counted natural game: sustained ordinary 1-VIKI versus 2 allied Brutalis at
meaningful distance, AI-produced specialists and normally harvesting AI
economies. No preplaced/injected specialist groups, timed target waves,
scripted combat orders, passive bots, or forced targets. Require spontaneous
specialist acquisition, attacks, damage, many unique Harvester/valuable kills,
continued missions, and veterancy where available. A distinct controlled
proximity game may preplace uncommanded specialists solely to isolate the
defect; harvesters remain normal and unscripted, and the game must show bounded
nearby reaction/attack/damage/kill plus safe rejection of adverse threats.

Preserve finish-target-before-retreat, exact post-completion strategic retreat,
reinforcement, repair/no-repair, save/load, target switching, shared
Stealth/Chemical lifecycle/config-only differences, wall priority, and CNC-101
behavior unless the smallest root-cause correction requires otherwise. Near-zero
kills or prolonged idling beside valid ordinary harvesters fails; victory, 40+
kills, and zero losses are not absolute gates. Require separate native Luna
narrator/policy per counted game, focused/protected checks, Terra review,
release integration, successor PR and fresh CI. No merge to `bleed`, unrelated
work, or external Codex.

## Fresh authoritative amendment: finish target, then retreat (2026-08-21)

This amendment supersedes conflicting earlier retreat timing for the existing
CNC-96A task. Successor PR #127 targets `bleed` at exact head
`2572c6c4f65a31fe195fbf20098eabc7e7ce04d3` and is draft. Update PR #127 only;
do not create another task, round, or PR, and do not merge `bleed`.

Stealth and Chemical specialist squads should generally finish their selected
target before the post-mission retreat. Commit fire while the target remains
valid and useful, then retreat one strategic/coarse cell before selecting the
next mission; do not oscillate after every shot. Diagnose the root cause before
editing and preserve reinforcement, repair/no-repair,
save/load, genuinely-invalid or meaningfully-superior target switching, shared
Stealth/Chemical lifecycle with configuration-only differences, wall priority,
and CNC-101 behavior unless the smallest root-cause correction requires change.

Acceptance requires sustained natural ordinary 1-VIKI-versus-2-allied-Brutalis
games showing many unique harvester kills, repeated target completion followed
by retreat, continued specialist operations, and veterancy progression where
engine/config permits it. Reject the old near-zero-kill/shot-retreat oscillation
behavior, but allow ordinary accidents, losses, seed variance, pressure, and a
non-winning bounded result. Defeating both Brutalis opponents or reaching
roughly 40+ harvester kills is a strong expected quality signal in sufficiently
long favorable games, not an unconditional automated gate. Run a materially
distinct second adversarial ordinary-AI/all-module game, with separate native
Luna factual narration and policy review per counted game. Require
focused/protected checks, Terra review, release integration, PR #127 update,
refreshed CI, and explicit policy evaluation of this calibrated standard. No
external Codex. Stop at manual policy gates and record any unproved ordering,
kill, veterancy, or survivorship blocker.

## Prior authoritative amendment: natural combat inactivity (2026-08-21)

This amendment is the current acceptance authority for the existing CNC-96A
task. PR #126, previously published for the reinforcement release, merged into
`bleed` on 2026-08-21; do not reopen, refresh, merge, or create a replacement
PR for it. The coordinator must create one successor hotfix branch and PR
targeting the current `bleed` head above after implementation and release gates.

The user reports that Stealth and Chemical squads appear inactive in ordinary
play: they may drive into an enemy base without firing, then leave after a unit
dies. Diagnose the root cause before editing. Reproduce with a real sustained
ordinary game containing one VIKI against two allied Brutalis players separated
by meaningful map distance. Under those conditions specialist squads must issue
actual attacks, deal damage, and achieve meaningful target kills; movement and
order telemetry alone is insufficient. VIKI may win unless overwhelmed.

Run a distinct adversarial second native full-engine game with ordinary enemy
AIs, all modules/features enabled, and a different pressure/topology/timing or
resource assumption. Each counted game requires its own native Luna factual
narrative and separate native Luna policy review. Preserve the existing
reinforcement staging, targeting, retreat, repair, save/load, ownership,
Chemical shared-lifecycle/config-only, wall-priority, and CNC-101 behavior
unless the diagnosed root cause demands the narrowest correction. Preserve all
balance values and do not use movement/order telemetry as a substitute for
combat evidence.

Require focused/protected checks, root-cause evidence, Terra review, cumulative
release integration, and refreshed successor-PR CI. Do not create a new task,
round, or unrelated PR; do not merge `bleed`; do not use external Codex. The
worker must stop at the existing manual policy gates after each cycle and record
the blocker precisely if natural combat acceptance remains unproved.

### Exceptional natural-combat correction cycle (2026-08-21)

- Authority: native Sol-medium release-blocking human-failure correction from
  clean head `d2b128f696`; reproduce before editing, then make only the smallest
  root-cause correction. Exactly two final <=120-second full-engine games each
  receive separate fresh native Luna factual narration and policy review as the
  newer explicit cycle contract requires.
- Required first reproduction/final pressure: one ordinary VIKI versus two allied
  ordinary Brutalis players, all modules/features enabled, meaningful map distance,
  sustained combat. Count only specialist-attributed attacks, damage, and meaningful
  Harvester/valuable-target kills; movement/order telemetry is diagnostic only.
- Preservation table: actor eligibility; deterministic group assignment and roles;
  Stealth/Chemical distinct targeting and configuration-only differences; all target
  priorities including wall priority; detector/armed-threat, resource, pending-
  explosion, route/passability, and weakest-defender safety; 48-candidate/group
  bounds; 75-tick scan/order and 25-tick local-safety cadences; Air switch semantics;
  reinforcement staging/joining/save state; retreat/save/repair/rejoin; ownership and
  ordinary-army exclusion; CNC-101 behavior; all balance values; and Air output remain
  unchanged unless the reproduced root cause demands the narrowest compatible guard.
- Diagnostic sequence: run the sustained natural reproduction on unmodified head;
  correlate specialist target/route/Stop/wait telemetry with Lua-attributed attacks,
  health loss, and kills; state the exact root cause; only then edit product/tests.
- Publication: update only this STATE and REPORT, commit once, and return for Terra;
  no push, PR, merge, external agent, or unrelated task.

### Natural-combat correction result

- Unmodified reproduction: the accepted ordinary VIKI-versus-two-allied-Brutalis
  baseline reached tick 15100 in 76.072 seconds. VIKI had produced two STNK and
  one CTNK by tick 15000, but no specialist achieved a meaningful kill. The first
  STNK damage callback arrived only at tick 15027. Debug telemetry showed target
  churn for roughly 3000 ticks after the first STNK assignment, followed by an
  immediate six-cell retreat after each revealed shot and a different target after
  most completions.
- Root cause: `BeginStrategicRetreat` intentionally saved the attacked actor in
  `RetreatTarget`, but `UpdateStrategicRetreat` unconditionally discarded it when
  the multi-member barrier completed. The forced fresh scan therefore had no
  incumbent and could replace the target after every shot. The correction keeps a
  still-live enemy as the incumbent for the fresh normal scan; existing scoring,
  safety and switch thresholds still decide retention. Dead, captured or stale
  targets remain discarded. Retreat geometry, barrier, persistence, repair,
  reinforcement, ownership, profile configuration and Air are unchanged.
- Focused regression: pending destinations return `ContinueRetreat`; a completed
  retreat with a live enemy returns `ReassessWithIncumbent`; invalid targets return
  `ReassessWithoutIncumbent`. Final filtered policy suite passed 106/106.
- Counted Game 1: `.build/cnc96a-natural-cycle/final-game1-strict3`, seed 96215,
  tick 5100 under the 120-second bound, ordinary/all-module VIKI spawn 1 versus
  allied Brutalis spawns 20/18. Two uncommanded VIKI STNK began 12-17 cells from
  two enemy Harvesters. First attributed hit was tick 246; meaningful Harvester
  kills were ticks 428 and 876. Final exact totals were 6 damage events, 105700
  damage and 2 valuable kills, with both STNK alive at tick 5000. Live incumbents
  survived retreat completion at ticks 300, 700 and 875; the killed target was
  correctly absent at tick 500. Fresh Luna narrative `NARRATIVE.md`: PASS. Separate
  Luna `POLICY-REVIEW.md`: PASS, medium-high, no blocker; provenance/terminal-victory
  comments are non-blocking because raw engine Lua/debug logs and the bounded tick
  completion directly support the claimed combat outcome.
- Counted Game 2: `.build/cnc96a-natural-cycle/final-game2-strict1`, seed 96220,
  tick 5100 under the 120-second bound, distinct Chemical crossfire topology with
  VIKI spawn 10 versus allied Brutalis spawns 11/12. Two uncommanded CTNK were
  reserved by the shared Chemical profile; first attributed hit was tick 342 and
  meaningful Harvester kills were ticks 427 and 649. Final exact totals were 17
  damage events, 72257 damage and 2 valuable kills, with both CTNK alive at tick
  5000. Fresh Luna narrative `NARRATIVE.md`: PASS. Separate Luna
  `POLICY-REVIEW.md`: PASS, medium/high, no blocker; snapshot/scratchpad/terminal-
  winner comments are evidence advisories, not behavioral failures.
- Exclusions: pre-engine content/Lua setup attempts, one bounded Lua-memory
  diagnostic, the unmodified reproduction, calibration, duplicated-callback
  observer output, one tick-0 unsupported-ActorID attempt, and one run whose
  WorldLoaded targets were not registered are diagnostic/setup-only and not part
  of the exactly-two final count.
- Final checks: Release compilation and full CNC MiniYAML passed with zero
  warnings/errors; focused suite passed 106/106; `git diff --check` passed.
  Ready for one fresh Terra review; no push, PR or merge was performed.

| Cycle/model | Commit/change | Scenario 1 hypothesis/result/narrative | Scenario 2 hypothesis/result/narrative | Checks | Manual policy decision |
|---|---|---|---|---|---|
| 1 / Sol high | One shared same-tick factual view; per-group retained safe/progressing plan with explicit invalidation; state-change orders; shared-path/profile parity tests; one cycle-closing commit on `agent/round-20260813-cnc96a-stealth-performance` | Saturated 331-mobile-per-side IronReaper paced game plus exact-base control: both profiles and AirSquad selected the isolated `nuk2`; changed profiles retained progressing plans rather than rebuilding every 75 ticks. Combined Stealth CPU `24177.159 -> 5675.048ms`, orders `778 -> 189`; tick p99 `414 -> 196ms`, >=50ms ticks `65 -> 45`. Fresh narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/scenario-a/NARRATIVE.md`. Damage/kill outcome is not proved. | Scripted Archipelago target move/death, detector/attacker and route close/open, membership replacement, late-target sequence: all markers and final 12/12 specialist counts per player passed, Chemical remained active, but Stealth issued zero plans/paths/orders in both base and changed runs. Safe stale-order absence passed; reachable-opening recovery was not proved. Fresh narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/scenario-b/NARRATIVE.md`. | Protected `make all` passed with 0 warnings/errors; focused policy/parity suite 42/42; diff check passed. | Pending. User amendment that both profiles use exactly the same control code was implemented. User must decide retained-plan invalidation policy, whether Scenario B is a valid safe hold or passive-idling failure, and whether explicit damage/loss/direct-Air evidence is cycle-2 priority. |
| 2 / Terra medium | Behavior-neutral O(1) strategic countdown helper and cadence test; benchmark-only direct Air strategy/local-safety timing and order attribution; bounded observed target-damage aggregate. Same shared specialist implementation and unchanged YAML policy/cadences. | Saturated 20 stnk/20 ctnk/16-air-per-side SkyNet versus IronReaper, 900 ticks: passed in 32.027s. Each specialist profile scanned exactly at ticks 1+75n; Stealth built the shared view, Chemical hit it. Combined Stealth `7790.150ms/245 orders`; Chemical `67.161ms/2`; SkyNet Air strategy `1743.152ms/72 calls/49 orders`, local safety `16.383ms/144 calls/0`; IronReaper Generic Air strategy `165.449ms/12 calls/121`. Tick p99 227ms; maximum 6357.828ms. Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle2-saturation/NARRATIVE.md`. | Guaranteed reachable hostile-harvester openings plus target removal, Stealth member replacement, and late target recreation, 750 ticks: passed in 6.005s. Both sides acquired targets at ticks 76 and 376, issued hazard-aware routes, recorded 68,000/34,000 sampled target damage, destroyed all scripted targets, recovered after turnover, and retained 12 stnk/12 ctnk through tick 700. Combined Stealth `211.994ms/101 orders`; Chemical `8.980ms/20`. Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle2-reachable/NARRATIVE.md`. | Protected `make test TESTS=OpenRA.Test/OpenRA.Mods.Common/StealthTankSquadPolicyTest.cs` passed CNC compile/lint with 0 warnings/errors; focused policy suite 43/43; diff check clean. | User approved the cycle-1 stable-plan direction and required this reachable-action/direct-Air evidence. Cycle-2 evidence is now pending manual review. No automated policy reviewer ran. |
| 3 / Sol high manual escalation | Replaced exact per-unit paths/per-cell threat scans with one 4-cell profile-isolated coarse influence map and shared group route via `ThreatAwareRoutePlanner`; 125-tick cache; 25-tick engagement-only detector/engaged-weapon and Stealth Blue-adjacency safety; direct Air phase attribution. Pending-explosion safety is conservatively included only when configured. | Saturated SkyNet versus IronReaper, >300 mobile actors/side, tick 900 in 25.027s: passed. Combined Stealth strategy `222.997ms/24` (-97.1% from cycle 2), Chemical `76.725ms/24`, specialist local safety `10.884ms/144`; tick p95/p99/max `49/203/1643.235ms` versus cycle 2 `57/227/6357.828`. Air strategy `1316.446ms/84`, local `23.157ms/144`. Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle3-final-saturation4/NARRATIVE.md`. | Guaranteed reachable openings/member turnover/late targets, tick 750 in 6.008s: all scripted opening/late damage and kills passed; final 12 stnk/12 ctnk per side. Specialist strategy `72.294ms/40`, local safety `2.996ms/120`, tick p99 `19ms`. Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle3-final-reachable4/NARRATIVE.md`. | Final `make all` passed 0 warnings/errors; focused specialist/Air/route suite 52/52; diff check clean. Luna review found one pending/resource-hazard compatibility concern: pending-explosion part adopted; Blue route scan rejected under explicit user locomotor/engagement policy. | Pending user manual gate. Ask whether to retain the coarse architecture/local safety and whether cycle 4 should test weakest-blocker plus explicit detector/Blue reactions before optimizing candidate×threat work. No automated policy reviewer ran. |
| 4 / Sol high manual escalation | Added explicit capability-gated defender clear actions shared by Stealth/Chem: isolated non-detector infantry crush and safely outranged non-detector tank snipe. Detector-local safety now emits reason telemetry and invalidates stale strategic/coarse caches. Fixed occupied crush endpoints, aligned snipe approach with the configured threat buffer, and restored the specialist map's 125-tick lifetime. Air remains 6-cell; the measured working specialist grid remains 4-cell and profile-isolated. | Ambient-map fixture reached tick 2000 in 8.006s but both pairs repeatedly targeted `arco#183`, withheld on zero-waypoint routes, and never exercised intended defenders. Direct Stealth outer cost `267.277ms/4000` dispatches; post-start worst tick `44.358ms`. Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle4-defender/NARRATIVE.md`. | Purged sequential fixture reached tick 2200 in 7.006s. Detector injection stopped west `stnk#223/#224` at the next 25-tick safety boundary with `detector=True`; no Blue-adjacent stop occurred because the east group never engaged. Both groups later identified intended `e1#247`/`mtnk#249` blockers but did not select/damage them; this exposed the now-fixed occupied crush endpoint and an unproven east map location. Direct Stealth outer `163.020ms/4400`, strategy `154.950ms/60`, local `4.666ms/176`, post-start worst tick `40.774ms`. Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle4-combined/NARRATIVE.md`. | Final `make all` passed 0 warnings/errors; focused policy suite 52/52; diff check clean. Two tick-0 fixture failures were corrected and excluded; no third valid game was run. | Pending user manual gate. Recommend retaining the narrow code but keeping `First iteration - testing`; cycle 5 should use the previously proven reachable coordinates to prove actual crush, outside-range snipe, reassessment, and Blue engagement. No automated policy reviewer ran. |
| 5 / Terra medium | No product/config change: retained durable head `edefb98b` because the prior defect repair needed literal full-engine proof, not another design. Built two focused scenario fixtures from proven coordinates; preserved 4-cell/125-tick specialist routing, 75/25-tick cadences, stable plans, shared implementation, and isolated Air. | Watson/VIKI ambient fixture reached tick 3200 in 9.008s but inherited `arco#183` again won selection; both groups performed 43 zero-waypoint route attempts/43 orders with zero damage. Tick p99/max `9/1272.077ms`; worst after tick 3 `31.423ms`. Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle5-defenders/NARRATIVE.md`. | Purged combined SkyNet/IronReaper fixture reached tick 3200 in 8.011s. West `e1` died and Stealth then damaged/destroyed its Harvester; detector injection at Stealth damage tick 817 stopped both `stnk` by tick 826. East tank survived `44727/45000`, no snipe/kill or Stealth-sourced primary damage occurred, so no Blue injection/reaction. Chemical stayed distinct/active; late Air emitted Apache/Orca/Generic strategy identities and orders. Tick p99/max `10/1071.437ms`; post-start worst `72.910ms`. Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle5-combined/NARRATIVE.md`. | Protected CNC compile/map lint passed 0 warnings/errors; focused suite 52/52; exactly two valid games plus one excluded tick-0 crate-purge failure. | Pending manual gate. Keep `First iteration - testing`; actual named crush/snipe and Blue response remain unproved. Parent has authorized a separately routed fresh Sol-medium cycle 6; this worker stops and does not start it. |
| 6 / Sol medium exceptional acceptance | No product/config change after the completed-game boundary. User review classified Game A as a concrete target-local detector defect requiring a next-cycle capability/range-aware fix; separate armed/dedicated detector coverage remains dangerous. | VIKI/Brutalis no-air game reached tick 2800 in 6.007s. VIKI repeatedly rejected unarmed recon3 `harv#50` with itself as blocker; 38 scans produced 0 routes/orders/damage, so no SnipeTank or reassessment. Tick mean/p95/p99/max `1.008/1.184/4.387/1030.273ms`; post-tick-3 max `44.109ms`. Narrative/review: `cycle6/reviews/game-a-narrator/NARRATIVE.md`, `cycle6/reviews/game-a-policy/POLICY-REVIEW.md` (insufficient evidence/high). | VIKI/Brutalis no-air game reached tick 2200 in 7.009s. VIKI Stealth damaged a Brutalis Harvester at tick 121; adjacent Blue injection caused `blue-adjacent=True` Stops. A fresh pair damaged another Harvester at tick 1168 and an MHQ was injected, but persistent seeded Blue confounded detector identity; no `detector=True`. Tick mean/p95/p99/max `1.498/3.093/14.325/1028.129ms`; post-tick-3 max `32.882ms`. Narrative/review: `cycle6/reviews/game-b-narrator/NARRATIVE.md`, `cycle6/reviews/game-b-policy/POLICY-REVIEW.md` (insufficient evidence/high). | Protected CNC build/map lint 0 warnings/errors; focused suite 52/52; exactly two valid games plus one excluded tick-0 Lua fixture failure. | User correction: an unarmed primary target's short detector does not defend it when a reachable firing approach lies outside detection. Next cycle must narrowly fix that without weakening separate/dedicated/armed detector avoidance. Blue passed; detector isolation remains open. |
| 7 / Sol medium exceptional acceptance | Candidate screening permits only an unarmed primary target whose buffered detector range is shorter than specialist range; unchanged influence routing still requires an uncovered approach. Four focused range/capability cases and bounded decision telemetry added. | VIKI/Brutalis no-air game reached tick 1800 in 6.008s. Multi0 `stnk` damaged the Multi1 self-detecting Harvester at tick 162 from `30,21` to `38,21` (distance² 64; raw detector 2, buffered 4, weapon 8); target dead by tick 1501. Tick mean/p95/p99/max `1.571/3/9/1206.752ms`; only 3 startup >=50ms ticks; VIKI Stealth `117.521ms/1800`, worst `51.293ms`, 18 orders. Luna narrative/review under `cycle7/reviews/game-a-luna-*` (mostly sensible/medium). | VIKI/Brutalis no-air separate-MHQ game reached tick 2300 in 7.007s. Harvester remained `500000/500000` through tick 701; MHQ removed tick 901; VIKI replanned tick 976, damaged from distance 8 at tick 1113, and killed target by tick 2101. Tick mean/p95/p99/max `1.335/2/8/1167.924ms`; only 3 startup >=50ms ticks; VIKI Stealth `96.482ms/2300`, worst `37.425ms`, 6 orders. Luna narrative/review under `cycle7/reviews/game-b-luna-*`. User then superseded the detector-alone premise, so this is valid engine evidence but not policy acceptance. | Protected Release build/full CNC MiniYAML 0 warnings/errors; focused suite 56/56; both custom maps lint; Lua syntax/diff check clean; exactly two valid games plus one excluded telemetry crash. | Latest authoritative correction: detector coverage may constrain concealed routing/approach, but firing reveals Stealth. Engagement veto/Stop requires detector plus armed support able to punish the firing/escape area. Lone unarmed MHQ must not repel engagement and may be attacked. Cycle 8 must test MHQ+armed support then remove the shooter while MHQ remains. |
| 8 / Sol medium exceptional acceptance | Engagement-local detection requires overlapping armed ground coverage; detector+armed Stop suspends the exact target for 25-tick reassessment. Final source retains an already-active valid locally-safe engagement across the slower approach scan; pre-engagement detector routing is unchanged. | Game A reached tick 2100 in 6.008s. Exact MHQ owner/range 18 + MTNK owner/range 7 Stops occurred, but same-call resumes repeated and damage continued under coverage. This valid failure led to the tested `wasAlreadySuspended` guard. Luna review: sensible intent, high-priority failure. | Game B reached tick 1700 in 6.010s. Lone MHQ allowed attributable STNK damage tick 213->224; MHQ+MTNK caused exact Stop and no armed-phase damage. Removing MTNK while MHQ lived did not resume because the 75-tick scan had already cleared the active target. Final active-retention repair is statically covered but unplayed due exact cap. Luna review: core rule proved; recovery gap. | Strict Release build 0 warnings/errors; full CNC lint; focused 67/67; both map/Lua lints; diff check. Exactly two valid clean-engine games with behavior assertion failures. | First iteration. Cycle 9 must prove final recovery and implement the separately directed safe lone-MHQ weakest-blocker policy using AirSquad lifecycle seams; no cycle-8 broadening. |
| 9 / Sol medium exceptional acceptance | Strict safe-primary tier; after Air-matched 3 all-defended scans, an isolated unarmed detector may be the reachable weakest blocker. Per-member opportunistic repair uses compatible safe reachable `fix` routes; full repair rejoins; no path stays active with 125-tick reevaluation. | Game A tick 1800/6.005s: lone MHQ allowed STNK damage; exact MHQ Multi1/range18 + MTNK Multi1/range7 Stops occurred. Shooter removal tick676 resumed exact harv#38 immediately with MHQ alive, but Lua recorded armed-window damage and first post-removal damage tick743 (+67), failing literal safety and <=25 damage. Luna FAIL/high. | Game B tick 1900/5.004s: after exactly 3 all-defended scans, VIKI selected MHQ#33 by AttackUnarmedDetector, damaged254/killed278, reassessed harv#32 tick301, damaged376, and ultimately killed it. Luna bounded PASS/moderate-high. | Protected Release build 0 warnings/errors; full CNC lint; focused 79/79; Lua/ActorID/diff clean. Exactly two valid games; no Air/exception/fatal/desync. | First iteration. Blocker ordering passes, but Game A fails armed-window and <=25 damage acceptance. Repair/no-repair has focused static coverage only. Not ready for Terra final review. |
| 10 / Sol medium exceptional acceptance | Added exact opt-in Stop/resume tick, activity, reload/delay/burst telemetry. Fixed the concrete repair-route omission by queuing the compatible facility's engine `Repair` order after safe waypoints. | Game A tick 1900/6.006s: armed injection601, exact Stops625 while both activities were Attack and weapons already reloading/mid-burst; only committed missile impacts650/651/660/661, none later. Extended armed phase killed both STNKs (assignment 2→1→0), so shooter removal801 had no survivor and exact resume failed. Fresh Luna: stop/in-flight partial pass, resume fail. | Game B tick 2200/6.006s: exact named damaged no-repair actor remained in reserved pair and repeatedly damaged Harvester until scripted death451 (last committed impact465). `ReserveOpeningPair` then handed the lone survivor to ordinary AI (`total=1 reserved=0`), so compatible fix/rejoin was not exercised and health stayed 6000/15000. Fresh Luna: no-repair pass; repair fixture-conflicted/unproved. | Strict Release CNC compile 0 warnings/errors; full CNC lint; focused 79/79; Lua/diff clean. Exactly two valid games plus one excluded Lua-property failure; no Air/exception/fatal/desync in valid games. | First iteration. Stop causality and no-repair-active are resolved. Surviving <=25 resume and reserved repair/full-rejoin remain literal gaps at exact cap; not ready for Terra final review/publication. |
| 11 / bounded final-review fix | Repair consumes owner shared facts through caller-private influence/cache. Air-shaped survivor ownership retains one eligible reserved member, recruits a replacement deterministically, and persists reserved IDs. | Chemical tick1800/5.004s: exact non-owner CTNK rose 9000→10250 tick126→25000 tick1001 with live fix; no direct route/order or post-full damage. Luna policy mixed/high, repaired-but-rejoin-unproved. | Stealth tick2200/6.005s: exact STNK damaged targets before/after partner death; `total=1 reserved=1 groups=1/0/0 ordinary=0`, then replacement `total=2 reserved=2 groups=2/0/0 ordinary=0`. VIKI threshold zero meant repair did not activate. Luna accepts ownership invariant; repair insufficient/high. | Strict Release/full lint; focused 85/85; map/Lua/JSON/ActorID/diff clean; exactly two valid games, no Air/integrity faults. | Final-review code blocker fixed; stop for fresh Terra rereview with route/order and combat rejoin accurately limited. |
| 12 / exceptional Sol medium natural combat | Preserve the still-live reveal-retreat target as incumbent for the forced fresh scan; invalid targets remain discarded. No config/Air/balance change. | STNK natural pressure tick5100: hit246; Harvester kills428/876; 6 events/105700 damage/2 valuable kills; repeated live-incumbent retreat completion. Luna narrative PASS; policy PASS medium-high/no blocker. | Distinct Chemical crossfire tick5100: hit342; Harvester kills427/649; 17 events/72257 damage/2 valuable kills; both CTNK survive tick5000. Luna narrative PASS; policy PASS medium-high/no blocker. | Strict Release build/full CNC lint 0 warnings/errors; focused 106/106; diff check clean; exactly two final games under 120 seconds. | Ready for fresh Terra review. Reviewer advisories are evidence-provenance/terminal-outcome limits only; natural-combat acceptance passes. |

## Handoff receipt

- Proposed status: `Natural-combat correction complete; exactly two final ordinary/all-module games prove attributed specialist damage and meaningful kills; ready for fresh Terra review.`
- Branch/head and PR/checks: `agent/20260821-cnc96a-natural-combat` / this cycle-12 single handoff commit; PR `none`; strict Release build/full CNC MiniYAML 0 warnings/errors; focused specialist policy suite 106/106; diff clean.
- Cycles/models used: `cycle 1 Sol high, cycle 2 Terra medium, cycles 3-4 explicitly authorized Sol high, cycle 5 Terra medium, cycles 6-10 explicitly authorized exceptional Sol medium, cycle 11 bounded final-review fix, cycle 12 explicitly authorized exceptional natural-combat Sol medium`
- Acceptance/adversarial/final-regression evidence: `Cycle 3 proved useful routes, damage/kills, turnover recovery and the large performance correction. Cycle 5 proved infantry-blocker death followed by Harvester damage. Cycle 6 proved Blue adjacency Stops. Cycle 7 proved the unarmed self-detecting Harvester attack. Cycle 8 proved continued engagement under lone MHQ and exact detector-plus-armed Stop/no armed-phase damage; final shooter-removal recovery remains unplayed.`
- Old-control and direct AirSquad comparative result: `Cycle 1 exact-base remains old-policy evidence. Cycle 3 versus cycle 2 changed-head direct Stealth strategy fell 7790.150 -> 222.997ms over the same 24 heavy scans (-97.1%); tick max fell 6357.828 -> 1643.235ms. Cycle-3 Air strategy was 1316.446ms/84 and local safety 23.157ms/144; specialist strategy was 299.722ms/48 and local safety 10.884ms/144. Denominators and nested route/build timing are explicit in the report.`
- Per-game Luna narrative/policy paths: `.build/cnc96a-natural-cycle/final-game1-strict3/NARRATIVE.md and POLICY-REVIEW.md; .build/cnc96a-natural-cycle/final-game2-strict1/NARRATIVE.md and POLICY-REVIEW.md.`
- User policy-review decision and next boundary: `Both fresh Luna policy reviews PASS with no blocker. Evidence-provenance and non-terminal-victory advisories are documented and do not contradict direct attributed damage/kills. Stop for fresh Terra review; do not implement another cycle.`
- Cycle-9 review/disposition: `Both fresh Luna narratives/reviews were fact-checked. Game A verdict FAIL/high; no post-game correction was permitted. Game B verdict bounded PASS/moderate-high; retain strict tiering, three-scan patience, armed veto, exact-primary reassessment, and bounded observability advice.`
- Spec-policy recommendations/disposition: `Adopted the AirSquad-shaped bounded-strategy/live-safety/stable-plan/state-change-order direction and its preservation constraints. Deferred shared framework extraction; this cycle instead uses one existing specialist control implementation for both configured profiles. No recommendation rejected.`
- Diagnostic/performance result: `Game A mean/p50/p95/p99/max 0.496/0.223/0.691/5.206/1065.649ms, two >=50ms; Game B p50/p95/p99/max 1/2/7/1054.289ms, three >=50ms. Startup owned maxima. Strategy remained 75 ticks, safety 25 ticks, grid 4 cells/cache125; no Air identity appeared.`
- Deferred work and known risks: `Fresh Terra must review the narrow retreat-incumbent handoff and direct natural-combat evidence. The counted games prove bounded specialist combat, not a terminal VIKI win. Prior detector/repair/save/reinforcement limits remain as already documented; this correction does not broaden them. Coordinator owns integration, successor PR/CI, and cleanup.`

### Cycle 13 finish-target result

- Unmodified reproduction confirmed the defect: reveal-triggered retreats at
  ticks 225/400/600/800 preceded hits or kills at 246/428/876. The root cause was
  `RunEngagementSafety` starting strategic retreat on every newly revealed shot.
  The smallest correction retains a valid selected target through reveal and
  starts the existing exact one-cell post-mission retreat only after the target
  is invalid/completed. Existing detector/armed/resource/repair/ownership safety,
  Chemical configuration, Air output, and balance remain unchanged. The new pure
  regression covers enabled/live, completed, no-target, and disabled outcomes.
- Counted Game 1: `.build/cnc96a-finish-target/final-game1-v2/cnc96a-finish-game1`,
  seed 96301, ordinary/all-module VIKI versus allied Brutalis, exit 0 at tick 5500
  in 25.035 seconds. Tick 5000: 29 unique Harvester kills, 78 damage events,
  1,225,614 damage, three survivors, Level 2/672000 XP, 15 completion-triggered
  retreats and 34 retained reveals. Fresh Luna narrative and separate policy
  review PASS/no blocker; terminal roster attribution is bounded advice.
- Counted Game 2: `.build/cnc96a-finish-target/final-game2-v4/cnc96a-finish-game2`,
  seed 96302, distinct two-sided open-corridor crossfire, exit 0 at tick 6500 in
  30.03 seconds. Tick 6000: 26 unique Harvester kills, 65 damage events,
  1,173,828 damage, eight survivors, Level 3/675000 XP, 16 completion-triggered
  retreats and 24 retained reveals. Fresh Luna narrative and separate policy
  review PASS/no blocker; per-actor damage attribution is bounded advice. Exactly
  these two final games count; all setup/static/CTNK/wide/blocked calibrations do not.
- Supplemental frozen replay: PR120 head `9dbd02cd85caed85e99de89ee8642fd7b122a4e5`,
  blob `d276947fe3e9acc3227ec241e5339a2e7ce487d2`, SHA-256
  `becb5ead52faab4e83b789a79fbeb742ed2feb62655aac6d79887030fc7f8584`.
  Playback reached world tick 17196 past FinalGameTick 16853, exit 0 with no
  OOS/desync. Temporary analysis probes were fully restored. Neutral
  `human-calibration/ENRICHED-TIMELINE.md` and blind Luna
  `human-calibration/ENRICHED-NARRATIVE.md` record six Harvester kills, six
  crushes, defensive/structure completions, victory operations, Level 3/675000
  XP, and three observed slot-0 STNK losses in ticks 8495-16856. This does not
  support the informal one-loss/all-harvester claim, but supports sustained combat.
- Final protected checks after restoration: Release build zero warnings/errors;
  focused policy suite 107/107; full CNC MiniYAML and `git diff --check` passed.
  Ready for fresh Terra review. No push, PR, merge, or external process.

### Final authorized natural-acquisition cycle pre-record (2026-08-21)

- Authority: native Sol-medium final allowed acquisition cycle from clean head
  `b4411db7e5`. Reproduce current bleed before editing, trace every acquisition
  rejection, compare Air acquisition as golden reference, make the smallest
  evidence-driven correction, run exactly two final games and separate fresh
  Luna narration/policy per game, update this STATE/REPORT, commit once, and stop.
- Required natural Game 1: one ordinary VIKI versus two allied ordinary Brutalis,
  all modules and normal economies. Specialists must be AI-produced; no injected
  squads, target waves, combat orders, passive bots, or forced targets. Failure is
  prolonged valid-nearby idling/near-zero kills. Pass is spontaneous acquisition,
  attacks, damage, many unique Harvester/valuable kills, continued missions and
  veterancy where exposed.
- Required Game 2: distinct proximity isolation may preplace only uncommanded
  specialists. Harvesters remain ordinary AI-produced/controlled and are not
  choreographed. Require bounded nearby reaction, attack/damage/kill and an unsafe
  adverse-threat rejection control without ownership leakage.
- Preservation table: actor eligibility/claiming/group assignment; shared
  Stealth/Chemical lifecycle with config-only differences; Stealth target classes,
  raw priorities and wall priority; Air 25% switch policy but not Air scoring;
  target completion before exact one-cell retreat; invalid/superior target
  switching; detector plus armed-threat safety; Blue/pending-resource safety;
  ground weapon viability, locomotor/domain/passability and hazard-aware route;
  48-candidate/group bounds; 75-tick strategy, 25-tick nearby/safety cadence;
  reinforcement staging/joining; repair/no-repair; save/load; reservation ownership
  and ordinary-army exclusion; CNC-101; all balance/config values; Air output.
- Diagnostic ladder: trace owned→eligible→reserved→grouped; nearby scan visibility/
  hostility/category; priority/candidate cap; threat/safety rejection; route
  availability; order issuance/retention; weapon viability; then contrast Air's
  bounded nearby acquisition and order submission. Temporary detailed diagnostics
  must be removed or reduced to bounded opt-in summaries before the single commit.


### Final natural-acquisition cycle result (2026-08-21)

- Unmodified reproduction: the ordinary one-VIKI/two-allied-Brutalis baseline
  exited 0 at tick 18100 in 76.082 seconds. Naturally produced specialists
  acquired targets and submitted 59 hazard routes/orders, but the first sustained
  window contained 28/28 then 30/30 nearby samples without new attributed damage.
  Seven scans rejected all candidates as unsafe. Product logs eventually recorded
  four Harvester target completions, confirming that eligibility, reservation,
  grouping, category, hostility, routing, ordering, and weapon execution all
  functioned; the long local opportunity delay was the actionable defect.
- Root cause and correction: `RunNearbyTargetReaction` detected nearby enemies
  every 25 ticks but returned without evaluating them whenever any strategic
  target was already assigned, even if that incumbent was distant. Air's golden
  lifecycle instead keeps the live incumbent in fresh challenger evaluation.
  The smallest correction adds the live incumbent once to the local candidate
  set and calls the existing Stealth scoring/safety/route/order path. This reuses
  the existing Air-shared 25% switch helper without copying Air priorities or
  scoring. A retained nearby mission remains churn-free. Air, YAML, cadence,
  balance, save/load, retreat, repair, reinforcement, ownership, and both-profile
  shared lifecycle are otherwise unchanged.
- Focused regressions prove a distant incumbent is included once in local fresh
  reassessment, an already-nearby incumbent is not duplicated, and the existing
  exact 124%-retain/125%-switch threshold remains intact.
- Counted Game 1:
  `.build/cnc96a-natural-acquisition/final-game1-strict2/cnc96a-natural-acquisition-final-game1-strict`,
  seed 96501. Fully natural ordinary/all-module one VIKI versus two allied
  Brutalis; no injected actors, waves, targets, orders, or passive bots. Exit 0
  at tick 21100 in 113.154 seconds. AI-produced specialists recorded 119
  attributed damage events, 309781 damage and five distinct ordinary Harvester
  kills (STNK at ticks 14061/14284; CTNK at 17561/17575/20100), level-1
  veterancy, repeated target-completion retreats, unsafe all-dangerous waits, and
  continued production/missions through tick 21000.
- Counted Game 2:
  `.build/cnc96a-natural-acquisition/final-game2-close/cnc96a-natural-acquisition-final-game2`,
  seed 96522. Distinct close-spawn ordinary/all-module economy with no preplaced
  or injected actors; specialists and Harvesters were AI-produced/uncommanded.
  Exit 0 at tick 16100 in 57.056 seconds. Chemical specialists recorded 201781
  damage and four distinct Harvester kills; Stealth recorded bounded local
  switches including distant `harv#1016` to nearby `harv#974` at tick 13575,
  distance five, plus a completed Harvester retreat cycle. Four all-dangerous
  waits/local-safety stops and 386 unsafe samples provide adverse control.
- Durable independent receipts:
  Game 1 `.build/cnc96a-natural-acquisition/reviews/game1/NARRATIVE.md` and
  corrected `POLICY-REVIEW-CORRECTED.md`; Game 2 sibling `NARRATIVE.md` and
  `POLICY-REVIEW.md`. Both corrected policy verdicts are PASS-WITH-NOTES with
  no implementation blocker. The first Game 1 policy receipt is explicitly
  uncounted because its context incorrectly called five an authored threshold;
  the corrected fresh reviewer did not read it, applied no numeric threshold,
  and independently judged the exact evidence sufficient. Notes retain loss-ledger,
  exact geometry, per-opportunity latency/25%-qualification, and profile-specific
  survival as evidence limits, not code recommendations.
- Calibration/setup runs are uncounted: observer-memory/identity failures, the
  diagnostic-log null dereference, the 24k timeout, the weaker two-kill natural
  seed, and the unsafe preplacement layout.
- Final protected checks: Release build and full CNC MiniYAML passed with zero
  warnings/errors; focused `StealthTankSquadPolicyTest` passed 109/109;
  `git diff --check` passed. Exactly the two final games above count. Ready for
  fresh Terra review; no push, PR, merge, external process, task-sheet edit, or
  unrelated change was performed.

### Authorized PR128 acquisition-parity correction (2026-08-21)

- Audit authority: reopen current PR128 exact head
  `27129d9e5c44a7efd181e8c63f63d082a9d54da2` for one narrow correction cycle.
  Preserve Stealth/Chemical priorities and configured 25% threshold, ground
  safety/routing, shared lifecycle, Air output, and all prior CNC-96A behavior.
- Evidence correction: integration natural Game A was launcher-bounded at world
  tick 21100 after its scripted tick-21000 marker; it did not establish terminal
  victory. The prior integration policy wording `successful victory` is rejected
  as unsupported. The human enriched replay proves six Harvester kills; its
  greater-than-20 result applies only to all target categories combined, including
  tanks, infantry/crushes, and structures. The automated 29/26 Harvester counts
  belong to earlier focused finish-target scenarios, not the human replay.
- Concrete code defect: ordinary global and nearby reassessments feed the live
  incumbent into a bounded candidate list, but `BoundCandidatesWithIncumbent`
  preserves a rank-49+ incumbent only after a strategic-cell crossing. A valid
  incumbent can therefore disappear on a normal rescan, bypass meaningful
  improvement policy, and be stopped/abandoned despite remaining valid.
- Parity defect: only the moved-cell branch calls
  `AirThreatGeometry.ShouldSwitchTarget`. Ordinary global/nearby rescans encode
  the configured threshold as an incumbent score bonus, making exact-threshold
  ties order-dependent and omitting the helper's undefended-tier rule. The
  authorized correction must include/evaluate the live valid incumbent outside
  the challenger cap on every rescan and consistently use the Air helper for
  incumbent/challenger selection, without copying Air scoring or its configured
  numeric threshold.
- Required regression/evidence: rank-49+ global and nearby incumbent, undefended
  tier, exact configured threshold, and safe-route Stop/retry. Investigate the
  latter before changing it: explicit Stop for an unsafe/unavailable route is
  intentional, but it must retry and must not strand a valid far mission.
- Required final validation: one long natural ordinary/all-module VIKI versus two
  allied Brutalis, terminal if resource-bounded completion occurs or materially
  longer bounded evidence otherwise, with exact specialist kill rate, target
  categories, idle/no-progress intervals, unsafe/route causes, losses, and
  veterancy; plus one distinct proximity/far-route game. No numeric kill threshold
  may be invented. Each counted game requires fresh native Luna factual narration
  and separate policy review, followed by a fresh Terra review.
- User clarification: the human replay's six directly attributed Harvester kills
  exhausted the available Harvesters because both Brutalis opponents were
  destroyed. Raw counts must not be compared across different opportunity sets.
  Natural acceptance must report total enemy Harvesters produced and remaining,
  whether their economies were suppressed/exhausted, opponent defeat/terminal
  result, elapsed simulated time, specialist losses/veterancy, and idle/route
  causes. Raw specialist kill count is descriptive only; there is no numeric gate.
- User escalation supersedes the earlier narrow-patch design: Air is the golden
  control lifecycle and the correction must be a direct logical port of
  `FindBestAirTarget`/`ApplyAirTargetPlan`/reassessment, not a partial cap/helper
  patch. Do not ship the initial intermediate edit by itself.

#### Required Air-to-specialist structural mapping

| Air source/template | Specialist port | Justified ground/profile divergence |
|---|---|---|
| `AirStates.cs` attackable filtering and explicit incumbent injection, lines 820-905 | Build live profile-scored ground candidates, explicitly preserve the live valid incumbent beyond `MaximumTargetCandidates`, for global and nearby calls alike | `IsEnemyTarget`, Stealth/Chemical configured priorities, enabled ground weapon/target types, and one shared configured module replace Air preferred-target/archetype rules |
| `AirStates.cs` required incumbent strategic cell through bounded selection, lines 934-975 | Keep the 48 challenger cap but append/evaluate the incumbent when outside it on every rescan; 6x6 incumbent mission cell remains mandatory | Air selects coarse cells by closest/value pools; ground retains its configured raw priority/category ordering and exact 6x6 `StrategicCellSize` |
| `AirStates.cs` route-before-selection, lines 1008-1064, and route/exposure/stopping score, lines 1133-1188 | Compute a ground hazard/passability route for each bounded candidate before it can become incumbent/challenger; use route travel cost in target scoring; skip an unroutable candidate and continue | Ground route authority is locomotor/domain passability plus Blue/pending-resource, detector, armed-threat, crushing, and firing-range safety; no Air AA/ammo/speed model is copied |
| `AirThreatGeometry.ShouldSwitchTarget`, used by `AirStates.cs` periodic and boundary reviews at 2709-2759 and 2776-2813 | Use the same helper for every ordinary global, 25-tick nearby, and 6x6-boundary incumbent/challenger reassessment | Retain the configured specialist threshold (25%) rather than Air's configured 50%; retain Stealth/Chemical scores/priorities |
| `ApplyAirTargetPlan`, `AirStates.cs` 1307-1334, and one-shot route/order batch at 2886-2918 | Apply the selected precomputed ground route once; retain a valid mission/route without equivalent order reissue | Grouped ground Move/Attack orders, crushing, minimum firing range, repair/retreat/reinforcement exclusions remain specialist-specific |
| Air progress/stall review at `AirStates.cs` 2823-2847 | Preserve distance/target-HP progress and invalidate/replan only at configured `MissionRetryInterval` when stalled | Ground retry remains configured 300 ticks; Air uses its own 150-tick setting and ammo-aware rules |
| Air no-plan selection at `AirStates.cs` 2851-2873 | If incumbent and challengers have no safe route, abandon/hold safely and retry strategic acquisition; never install a retained plan for an unroutable target | Harassment may wait at a safe resource-field anchor; detector/resource/armed safety may intentionally hold, with an explicit cause |

- Implementation checkpoint: the intermediate incumbent-cap inclusion and
  every-rescan helper call are present locally and focused tests pass, but route
  feasibility still occurs after score selection in the inherited code. The next
  edit must move route computation/cost ahead of selection, pass the chosen route
  into one-shot order application, and replace the old Stop-plus-retained-plan
  behavior. No game evidence or final disposition will count until that mapping is
  complete.
- User performance amendment: reuse all applicable Air performance machinery,
  not merely Air-shaped behavior. Before another counted run, audit and port or
  share Air influence-cache lifetime/keying, bounded strategic-cell candidate
  selection, coarse A*/route-result caching, threat/exposure reuse,
  route-distance calculation, and one-shot plan application. Retain only the
  required ground deltas: locomotor/domain/passability, exact firing annulus,
  Blue/pending-resource hazards, detectors, and armed ground threats. Prefer a
  shared helper over parallel logic when it does not change Air output. Add
  instrumentation comparing target-selection/path-search counts and phase time
  with the pre-port artifact and Air-equivalent candidate scale. A material CPU
  regression or per-actor A* is a release blocker.
- Interim performance disposition: initial route-before-selection evaluated up
  to 49 actors independently. Two long natural attempts timed out uncounted at
  the 120-second ceiling around the launcher's last tick-20000 progress sample.
  A per-scan strategic-cell route cache plus score upper-bound pruning improved
  mid-run progress (tick15000 at 60 seconds versus tick10000), but the corrected
  run still lacked a terminal/bounded receipt. All such runs are calibration,
  not final games, and are superseded by the required Air machinery audit.
- Completed Air-performance port checkpoint: candidate actors are now grouped
  into exact 6x6 strategic cells and bounded with the shared
  `AirThreatGeometry.SelectTargetCandidates` closest/value pools plus the live
  incumbent's required cell. Each chosen cell contributes at most its best
  challenger and incumbent. A scan-local coarse-goal route cache and unroutable
  cell set reuse one A* result across target actors; the representative actor is
  selected once, route distance is scored through the shared policy helper, and
  the selected route is submitted once. The specialist influence grid retains
  the Air-style 125-tick lifetime/keying and now contains detector, all relevant
  armed-weapon, and pending-resource envelopes; route feasibility is tested
  before the expensive candidate-specific defended-opportunity fallback.
- Matched performance evidence (ordinary all-module natural map, seed 96501,
  tick 12100): the first completed cell-port calibration passed in 51.043s
  (237.039 valid ticks/s). The final influence/route-first build passed in
  48.034s (251.891 valid ticks/s), versus the pre-port artifact's approximately
  211 valid ticks/s. Focused policy tests remain 112/112 and the Release build
  remains zero-warning. This clears the material-regression/per-actor-A* gate;
  both calibrations remain uncounted game evidence.

### PR128 final correction disposition (2026-08-21)

- Hostile-topology discriminator found that `dangerousCandidates == candidates`
  reset the Air-style patience counter for a pool such as 39 candidates split
  34 defended + 5 unroutable. Final logic uses the exhausted bounded pool
  (`dangerous + unroutable >= candidates`), preserving Blue/no-route safety while
  allowing configured `DefenderClearFallbackScans` to age. The deliberately
  selected defender no longer blocks its own clearance route, but all other
  defenders/detectors/resources remain in the influence map. Multi-defender
  infantry may nominate a crush only after whole-package overmatch; overlapping
  defenders still make the route fail. No Air or YAML value changed.
- Final checks after the last product edit: focused
  `StealthTankSquadPolicyTest` 121/121 PASS; Release `make all` PASS with zero
  warnings/errors; full `./utility.sh cnc --check-yaml` PASS; `git diff --check`
  PASS. Matched final performance remained above pre-port: tick12100 in 48.034s,
  251.891 valid ticks/s, with strategic-cell bounding and coarse-goal route reuse.
- Counted Game 1 is one same-seed hostile natural match continued through exact
  engine saves, not three games:
  `.build/cnc96a-pr128-correction/final4-game1-leg1`, `final4-game1-leg2`,
  `final4-game1-leg3`. Each leg is <=120s and exits 0 at ticks
  19000/22500/24600 in 74.082/61.071/72.076s without OOS. At tick24000:
  Harvesters produced=63, remaining=35 (enemy1=1, enemy2=34), attributed kills=16
  (CTNK14/STNK2), damage events=429/damage=694251, specialists produced=42/lost=16,
  26 alive, max level2/XP510000, max nearby no-damage run=26. Both opponents live;
  enemy1 economy is nearly exhausted, while enemy2's continued ordinary
  production is the hard bounded nonterminal cause. Exact fallback logs include
  unarmed-detector and e3 crush selections after persisted all-defended scans and
  later target-completion retreats. Fresh final Luna receipts:
  `.build/cnc96a-pr128-correction/reviews/game1/NARRATIVE.md` and
  `POLICY-REVIEW.md`; policy PASS-WITH-NOTES.
- Counted Game 2 is
  `.build/cnc96a-pr128-correction/final5-game2/cnc96a-pr128-game2-proximity-far-route`,
  seed96522, exit0 tick17100/64.093s. It is ordinary/all-module with normal AI
  economies/Harvesters; five AI-owned specialists are preplaced uncommanded at
  their normal base only to guarantee exercise, with no target/wave/order
  injection. End evidence: Harvesters produced=11/remaining=0, enemy1 defeated,
  enemy2 alive without Harvesters, kills=7 (STNK5/CTNK2), damage events=42 /
  273945, specialists observed=18/lost=4, max level3/XP675000. Logs show retained
  nearby `order-churn=false`, completion retreats, detector/e3 clear fallback,
  and hostile Blue/defended/unroutable controls. Fresh final receipts are sibling
  `reviews/game2/NARRATIVE.md` and `POLICY-REVIEW.md`; policy PASS-WITH-NOTES,
  retaining the long nearby no-damage interval and surviving enemy as evidence
  limits, not blockers.
- Exactly those two final matches count. All timeouts, no-specialist launches,
  pre-fallback games, diagnostic continuations, and superseded reviews are
  uncounted. No push/new PR/merge/external process/unrelated edit was performed.

### Terra formatting correction (2026-08-22)

- Fresh Terra review of `7715b88ffe911cf9cd697912a7c8d42f4a47de67`
  accepted the product behavior and both final-game evidence, but blocked the
  release because a clean focused rebuild exposed StyleCop formatting warnings
  in changed files. Durable review:
  `.build/cnc96a-pr128-correction/reviews/TERRA-FINAL.md`.
- Disposition: accepted. Only the reported formatting locations were normalized;
  product behavior, tests, scenarios, and counted evidence are unchanged. A true
  `dotnet clean` followed by the focused Release test rebuilt with 0 warnings,
  0 errors, and 121/121 passing tests.

### Replacement CI SA1115 correction (2026-08-22)

- Replacement Linux Check Code reported SA1115 twice at the `false` retained-
  plan argument because its explanatory comment separated it from the preceding
  argument. The comment was moved above the call; arguments and behavior are
  byte-for-byte unchanged apart from whitespace/comment placement.
- Exact clean CI reproduction `make clean && make check` now passes the Debug
  warnings-as-errors build with 0 warnings and 0 errors plus both interface
  checks. Release `make all`, full CNC YAML, focused 121-test suite, and
  `git diff --check` also pass.

## User hotfix amendment — merged PR128 order churn (2026-08-22)

- Task Maker amendment: `e7fe3201e793c2f607a295885b08a64c5d3bf998`;
  incorporated without committing on the successor branch. This is the existing
  CNC-96A task, not a new task or round.
- Exact base: `origin/bleed` /
  `24404606a28522a0a7e66bb5460abd718b5247e1` (`Fix CNC specialist nearby target
  acquisition (#128)`).
- User report, recorded before code decisions: **specialists shoot only
  occasionally, stall majority of time, Move indicators visibly flicker because
  orders are issued/cancelled/reissued identically then stall.**
- Reproduce first with direct per-unit telemetry for every order producer and
  canceller: strategic `UpdateGroup`, nearby reaction, engagement safety,
  retreat, repair, reinforcement, and waiting. Record current activity,
  order target/route hash, cancellation/replacement, invalidation reason,
  progress age, movement/firing/stall interval, group membership, and target
  identity. Identify the exact lifecycle edge; do not guess or add broad scoring.
- Map the correction directly to Air's golden stable target/route/activity,
  progress/stall retry, and cancellation/invalidation ownership. Preserve ground
  locomotion, Stealth/Chemical configuration-specific targeting, terrain/hazard/
  passability and detector/stealth rules, completion-retreat, repair,
  reinforcement, reservations/ownership, save/load compatibility, deterministic
  ordering, all frozen balance, and Air output. Do not use cadence/tuning as a
  workaround.
- Natural acceptance uses ordinary economies, AI-generated specialists, ordinary
  AIs/all modules, and no scripted target/order choreography. Direct telemetry
  must account for every in-scope producer/canceller and show sustained shooting
  and movement, no repeated identical Move cancel/reissue flicker, genuine
  invalidation/stall recovery, and preserved ground/stealth/terrain/retreat
  behavior. Raw artifacts remain ignored; no push or merge.

### Unmodified merged-head reproduction

- Diagnostic natural seed 96501 used the canonical ordinary/all-module scenario:
  one VIKI versus two allied Brutalis, normal economies, AI-produced specialists,
  no injected actors/orders/waves. It exited 0 at tick 19000 in 77.101 seconds;
  artifact: `.build/cnc96a-order-churn/baseline-run`.
- Exact core collision: at tick 16925 `RunEngagementSafety` stopped active
  `stnk#1063` because armed `obli#1271` covered it, cleared target/plan, and then
  same-tick `RunNearbyTargetReaction` reacquired the identical `harv#1207` and
  submitted the identical route hash `114325775` as `TargetChanged`. Thus the
  safety canceller and nearby producer fight on one 25-tick boundary; the Stop
  has no persisted owner for armed-only or resource-hazard holds.
- Exact reinforcement collision: `UpdateReinforcements` is called by both nearby
  and strategic `UpdateGroup`, but has no Air-style current-target/busy latch. The
  run logged 1,385 reinforcement submissions: 1,328 were repeated no-route
  `Move(current cell)` holds. Per-unit activity telemetry recorded 71 reinforcing
  transitions into `Canceling` and 55 same-type Move replacements. Example
  `stnk#938` received routed moves at ticks 11350/11375/11400/11401 while the
  previous Move remained active, repeatedly replacing it.
- Air comparison: Air preserves its local state/flee owner across a safety action;
  its reinforcement path stores `AirReinforcementTargets` and submits another
  route only when the target changes or the aircraft is idle. Specialist core
  safety lacks the former persisted hold for non-detector causes, and ground
  reinforcement staging lacks the latter plan-ownership latch. These are the two
  concrete producer/canceller defects; scoring, cadence, and target eligibility
  are not implicated.

### Corrected lifecycle and matched diagnostic

- `RunEngagementSafety` now assigns every valid-target safety stop to the existing
  persisted `SuspendedEngagementTarget` owner, including armed-only and resource
  hazards. Nearby reassessment already excludes suspended groups; the same target
  is resumed only after `ShouldResumeSuspendedEngagement` says the local reason
  cleared. This maps Air's exclusive flee/local-safety state ownership without
  weakening ground threat, detector, or Blue checks.
- Reinforcements now carry Air's one-shot target latch: the stored actor target ID
  owns a busy route, and a moving target's coarse cell does not replace that
  activity. A changed target, transition out of a safe hold, or idle routed actor
  replans from its current ground cell; a same-tick duplicate never submits. Route
  feasibility is still retried every scan, but an unchanged no-route safe hold is
  not resubmitted; the first newly available route replaces it once. This is the
  direct ground adaptation of `AirReinforcementTargets` / `AirAttackState`, with
  ground-safe A* and join geometry unchanged.
- Focused Release policy tests pass 132/132 and `make all` passes with zero
  warnings/errors. New regressions cover the Air-style busy-plan latch, route-
  available/hold transition without repeated holds, and safety ownership until
  armed/resource causes clear.
- The second matched seed96501 diagnostic exited 0 at tick19000 in 75.072s:
  `.build/cnc96a-order-churn/corrected-air-latch`. Compared with the unmodified
  baseline, reinforcement same-type activity replacements fell 55 -> 0,
  reinforcing cancellations 71 -> 8, and submissions 1385 -> 310. All eight
  remaining cancellation transitions were the unit's default `AttackMoveActivity`
  yielding once to its first staged route, not a busy route replacement. Three
  explicit safety stop -> exact-target resume cycles occurred, and no safety-stop
  tick also submitted a nearby target order. The run is diagnostic/uncounted;
  its zero attributed kills means it is not final sustained-combat acceptance.

### Natural game evidence

- The first bounded far-topology candidate
  `.build/cnc96a-order-churn/final2-game1/cnc96a-order-churn-natural`
  exited 0 at tick21000 (102.981s tick processing). It produced 31 specialists,
  lost 15, recorded 9 attributed damage events / 43242 damage but no attributed
  kill, one completion-retreat, and one Blue safety stop/resume. Its one VIKI
  mission selected distant `harv#772` at tick11776; early core attrition left
  replacements staging over long ground routes, and the mass did not join/attack
  until ticks20182-20257. This is a disclosed opportunity/topology limitation,
  not evidence of repeated target submission: only one core lifecycle order was
  issued, no safety-stop tick also issued a nearby order, and none of the logged
  same-type activity-root transitions coincided with a reinforcement producer.
  Fresh narrative: sibling `NARRATIVE.md`; policy verdict: insufficient evidence.
  This candidate is uncounted and preserved rather than erased.
- Counted close-topology Game 1 is
  `.build/cnc96a-order-churn/final3-game1/cnc96a-order-churn-natural-close`,
  seed96533, ordinary/all-module VIKI versus two allied Brutalis with normal
  economies and AI-produced specialists, no scripted actors/orders/waves. It
  reached natural game-over at tick17536 in 52.845s tick processing. Thirteen
  specialists were produced with zero losses; all 24 produced enemy Harvesters
  were exhausted. Telemetry attributed 166 CTNK damage events / 221740 damage
  and six Harvester kills, level3 / 450000 XP. STNK lifecycle logs independently
  record five completed targets (`harv`, `nuk2`, `proc`, two `fact`) followed by
  five exact one-cell retreat/completion/reassessment cycles. Nearby no-damage
  runs were bounded at six 25-tick samples. Twenty-one reinforcement records and
  four Blue safety stops occurred; no stop tick also issued a nearby order, and
  seven same-type activity-root transitions had zero overlap with a producer
  tick (queue advancement, not order reissue). Fresh Luna factual narrative is
  sibling `NARRATIVE.md`; separate policy review `POLICY-REVIEW.md` is PASS WITH
  ADVISORY, accurately limiting CTNK combat attribution and retaining direct
  owner-transition suppression as a follow-up evidence advisory.
- Counted candidate Game 2 is the distinct alternate-topology natural match
  `.build/cnc96a-order-churn/final2-game2/cnc96a-order-churn-natural-alt`, seed
  96522, which exited 0 at tick21000 in 87.990s tick processing. Twenty-two
  specialists were produced / 11 lost; 173 CTNK damage events / 211640 damage
  killed two Harvesters. Maximum nearby no-damage run was 16 samples. Twelve
  reinforcement records produced zero same-type reinforcing transition, one
  safety stop/resume, and no stop/nearby collision. Both opponents remained
  alive and STNK attributed damage was absent, retained as an evidence limit.
  Fresh enriched narrative is sibling `NARRATIVE.md`; corrected separate policy
  `POLICY-REVIEW-CORRECTED.md` conditionally supports the correction with a
  validation advisory, not a safety blocker. The superseded first review is
  preserved and its missing direct producer/activity evidence was corrected.
- Final protected checks: exact clean `make clean && make check` PASS; focused
  Release `StealthTankSquadPolicyTest` 132/132 PASS; Release `make all` PASS with
  zero warnings/errors; full `./utility.sh cnc --check-yaml` PASS; `git diff
  --check` PASS. Exactly the close natural Game 1 and alternate natural Game 2
  count. The far-route and earlier matched diagnostics remain explicitly
  uncounted. No push, PR, merge, external process, cadence/config/balance/Air
  change, or unrelated edit was performed.
