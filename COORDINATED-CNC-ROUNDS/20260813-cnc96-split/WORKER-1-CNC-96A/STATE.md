# Worker State: CNC-96A

Reread this file after compaction, before each cycle, after its game analyses,
and before publication. It is the complete assignment. Do not read the task
sheet, coordinator state, other skills, or other worker specs. Read applicable
`AGENTS.md`; inspect another task PR only when named under Dependencies.

## Assignment

- Worker/task: `WORKER-1` / `CNC-96A — Stealth squad performance using AirSquad as gold standard`
- Change category: `AI performance and bounded architecture, with player-visible tactical behavior preservation`
- Balance authority: `Frozen. Do not change cost, HP, damage, armor, speed, timing, power, prerequisites, probabilities, resources, production fractions, squad composition, target priorities, threat buffers, scan/order cadence, or candidate/group bounds.`
- Status: `First iteration - testing; awaiting user manual policy review`
- Base branch/SHA: `agent/round-20260812-cnc96-periodic-stalls` / `0c9a5c187d6bd3c354921855f19a4fb3590d6f06`
- Task branch / PR base: `agent/round-20260813-cnc96a-stealth-performance` / `bleed`
- Current cycle: `3`; cycles used: `3/5 primary`, `0/10 optional Luna`
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
- Games completed: `cycle 1: 2 distinct changed-build scenarios plus matched exact-base controls; cycle 2: 2 distinct changed-build scenarios; cycle 3: 2 distinct final-source changed-build scenarios`;
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

| Cycle/model | Commit/change | Scenario 1 hypothesis/result/narrative | Scenario 2 hypothesis/result/narrative | Checks | Manual policy decision |
|---|---|---|---|---|---|
| 1 / Sol high | One shared same-tick factual view; per-group retained safe/progressing plan with explicit invalidation; state-change orders; shared-path/profile parity tests; one cycle-closing commit on `agent/round-20260813-cnc96a-stealth-performance` | Saturated 331-mobile-per-side IronReaper paced game plus exact-base control: both profiles and AirSquad selected the isolated `nuk2`; changed profiles retained progressing plans rather than rebuilding every 75 ticks. Combined Stealth CPU `24177.159 -> 5675.048ms`, orders `778 -> 189`; tick p99 `414 -> 196ms`, >=50ms ticks `65 -> 45`. Fresh narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/scenario-a/NARRATIVE.md`. Damage/kill outcome is not proved. | Scripted Archipelago target move/death, detector/attacker and route close/open, membership replacement, late-target sequence: all markers and final 12/12 specialist counts per player passed, Chemical remained active, but Stealth issued zero plans/paths/orders in both base and changed runs. Safe stale-order absence passed; reachable-opening recovery was not proved. Fresh narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/scenario-b/NARRATIVE.md`. | Protected `make all` passed with 0 warnings/errors; focused policy/parity suite 42/42; diff check passed. | Pending. User amendment that both profiles use exactly the same control code was implemented. User must decide retained-plan invalidation policy, whether Scenario B is a valid safe hold or passive-idling failure, and whether explicit damage/loss/direct-Air evidence is cycle-2 priority. |
| 2 / Terra medium | Behavior-neutral O(1) strategic countdown helper and cadence test; benchmark-only direct Air strategy/local-safety timing and order attribution; bounded observed target-damage aggregate. Same shared specialist implementation and unchanged YAML policy/cadences. | Saturated 20 stnk/20 ctnk/16-air-per-side SkyNet versus IronReaper, 900 ticks: passed in 32.027s. Each specialist profile scanned exactly at ticks 1+75n; Stealth built the shared view, Chemical hit it. Combined Stealth `7790.150ms/245 orders`; Chemical `67.161ms/2`; SkyNet Air strategy `1743.152ms/72 calls/49 orders`, local safety `16.383ms/144 calls/0`; IronReaper Generic Air strategy `165.449ms/12 calls/121`. Tick p99 227ms; maximum 6357.828ms. Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle2-saturation/NARRATIVE.md`. | Guaranteed reachable hostile-harvester openings plus target removal, Stealth member replacement, and late target recreation, 750 ticks: passed in 6.005s. Both sides acquired targets at ticks 76 and 376, issued hazard-aware routes, recorded 68,000/34,000 sampled target damage, destroyed all scripted targets, recovered after turnover, and retained 12 stnk/12 ctnk through tick 700. Combined Stealth `211.994ms/101 orders`; Chemical `8.980ms/20`. Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle2-reachable/NARRATIVE.md`. | Protected `make test TESTS=OpenRA.Test/OpenRA.Mods.Common/StealthTankSquadPolicyTest.cs` passed CNC compile/lint with 0 warnings/errors; focused policy suite 43/43; diff check clean. | User approved the cycle-1 stable-plan direction and required this reachable-action/direct-Air evidence. Cycle-2 evidence is now pending manual review. No automated policy reviewer ran. |
| 3 / Sol high manual escalation | Replaced exact per-unit paths/per-cell threat scans with one 4-cell profile-isolated coarse influence map and shared group route via `ThreatAwareRoutePlanner`; 125-tick cache; 25-tick engagement-only detector/engaged-weapon and Stealth Blue-adjacency safety; direct Air phase attribution. Pending-explosion safety is conservatively included only when configured. | Saturated SkyNet versus IronReaper, >300 mobile actors/side, tick 900 in 25.027s: passed. Combined Stealth strategy `222.997ms/24` (-97.1% from cycle 2), Chemical `76.725ms/24`, specialist local safety `10.884ms/144`; tick p95/p99/max `49/203/1643.235ms` versus cycle 2 `57/227/6357.828`. Air strategy `1316.446ms/84`, local `23.157ms/144`. Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle3-final-saturation4/NARRATIVE.md`. | Guaranteed reachable openings/member turnover/late targets, tick 750 in 6.008s: all scripted opening/late damage and kills passed; final 12 stnk/12 ctnk per side. Specialist strategy `72.294ms/40`, local safety `2.996ms/120`, tick p99 `19ms`. Narrative: `analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle3-final-reachable4/NARRATIVE.md`. | Final `make all` passed 0 warnings/errors; focused specialist/Air/route suite 52/52; diff check clean. Luna review found one pending/resource-hazard compatibility concern: pending-explosion part adopted; Blue route scan rejected under explicit user locomotor/engagement policy. | Pending user manual gate. Ask whether to retain the coarse architecture/local safety and whether cycle 4 should test weakest-blocker plus explicit detector/Blue reactions before optimizing candidate×threat work. No automated policy reviewer ran. |

## Handoff receipt

- Proposed status: `First iteration - testing; hard stop at user manual policy-review gate`
- Branch/head and PR/checks: `agent/round-20260813-cnc96a-stealth-performance` / cycle-3 receipt commit; PR `none`; protected `make all` 0 warnings/errors; focused specialist/Air/route suite 52/52; `git diff --check` clean.
- Cycles/models used: `cycle 1 Sol high, cycle 2 Terra medium, cycle 3 explicitly authorized Sol high; no optional Luna cycles`
- Acceptance/adversarial/final-regression evidence: `The reachable scenario again proved real routes, attacks, damage/destruction, turnover recovery, survival, and both profile identities. Saturation removed the directly attributed multi-second specialist boundary. Other personalities, weakest-blocker, explicit local detector/Blue reaction, blocked/reservation/pending edges, repeated controls, paced final regression, and PR/CI remain.`
- Old-control and direct AirSquad comparative result: `Cycle 1 exact-base remains old-policy evidence. Cycle 3 versus cycle 2 changed-head direct Stealth strategy fell 7790.150 -> 222.997ms over the same 24 heavy scans (-97.1%); tick max fell 6357.828 -> 1643.235ms. Cycle-3 Air strategy was 1316.446ms/84 and local safety 23.157ms/144; specialist strategy was 299.722ms/48 and local safety 10.884ms/144. Denominators and nested route/build timing are explicit in the report.`
- Per-game Luna narrative paths/conclusions: `Saturation: analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle3-final-saturation4/NARRATIVE.md — early grouped routes, later all-dangerous holds, and direct CPU/tick facts. Reachable: analysis/20260813-cnc96-split/worker-1-cnc-96a/commenters/cycle3-final-reachable4/NARRATIVE.md — opening/late targets damaged and killed, member replacement recovered, all supplied specialists remained.`
- User manual policy-review decision/path and next authorization: `User explicitly authorized the Air-shaped coarse cache/route and engagement-only <=25-tick safety design. Cycle-3 implementation/evidence now awaits the manual gate: retain it, choose any safety correction, and prioritize either weakest-blocker/local-safety behavior tests (recommended) or remaining candidate×threat optimization.`
- Cycle-3 code review/disposition: `One advisory concern: resource/pending hazard checks were absent from coarse routes. Adopted bounded pending-explosion marking when configured; rejected Blue travel-cell restoration because the user's explicit policy delegates travel to locomotor and limits Blue safety to engagement adjacency.`
- Spec-policy recommendations/disposition: `Adopted the AirSquad-shaped bounded-strategy/live-safety/stable-plan/state-change-order direction and its preservation constraints. Deferred shared framework extraction; this cycle instead uses one existing specialist control implementation for both configured profiles. No recommendation rejected.`
- Diagnostic/performance result: `Per-tick dispatch is O(1); local safety runs at 25 ticks only for engaged actors; heavy scans remain 75 ticks. One 4-cell map/profile refreshes at 125 ticks unless nonzero pending-explosion safety requires current strategic-view freshness. No exact PathSearch or fine route-cell threat scan remains. Candidate×threat work is now the largest bounded phase.`
- Deferred work and known risks: `Other personalities, all-defended weakest blocker, explicit detector/Blue local reaction, blocked topology, nonzero pending explosion, transport/crate handoff, repeated controls, paced/headless agreement, save/load, final regression, PR/CI, and diagnostic cleanup remain. Historical 8024fd2 remains incompatible and was not relabelled or backported.`
