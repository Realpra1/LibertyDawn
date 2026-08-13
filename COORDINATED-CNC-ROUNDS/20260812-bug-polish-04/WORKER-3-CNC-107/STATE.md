# Worker State: CNC-107

Reread this file after compaction, before each cycle, after its two game analyses,
and before publication. It is the complete assignment. Do not read the task sheet,
coordinator state, other skills, or other worker specs. Read applicable
`AGENTS.md`; inspect another task PR only when named under Dependencies.

## Assignment

- Worker/task: `WORKER-3-CNC-107` / `CNC-107 — Complete blocked-cell wall repair around the Fact`
- Change category: `AI behavior — deterministic first-Fact enclosure repair/path legality; routing-adjacent and persisted`
- Balance authority: `Frozen; only the expressly requested wall-planner repair/path-legality behavior may change. Do not alter wall or unit cost, HP, damage, armor, speed, build/production time, prerequisites, probabilities, resources, MaximumWallSegments, access width, maintenance interval, cutoff, or other balance.`
- Status: `Complete - testing`
- Base branch/SHA: `origin/bleed` / `4e12088061ac277c51de2e658dc0209337b80968`
- Task branch / PR base: `agent/round-20260812-cnc107-wall-repair` / `bleed`
- Current cycle: `complete`; cycles used: `5/5 primary`, `0/10 optional Luna`
- Required model: cycle 1 `Sol high`; cycles 2-5 `Terra medium`; cycles 6-15
  `Luna medium` only when coordinator authorizes minor obvious work; at most two
  exceptional `Sol medium` escalation cycles may follow only a critical blocker
  that makes safe release or engine execution impossible
- Game/build capacity: `2` / `1`; lock: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260812-bug-polish-04/locks`
- Report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260812-bug-polish-04/WORKER-3-CNC-107/REPORT.md`
- Analysis directory: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260812-bug-polish-04/WORKER-3-CNC-107/ANALYSIS`
- Design: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Policy scratchpad/lock: `/root/github/LibertyDawn/.agents/references/LIBERTY-DAWN-POLICY-SCRATCHPAD.md` / `/root/github/LibertyDawn/.worktrees/coordinated-cnc/shared-locks`
- Games completed: `10`; cycle-3 review: `completed; accepted access-origin correction implemented and tested in cycle 4`; PR: `pending publication`

Each worker invocation performs only the current authorized cycle, updates this
file/report, and exits. Do not loop into another model tier in the same context.

## Integrated assignment

- Phase/release head: `isolated` / `not assigned`
- Repair branch/base: `not assigned`
- Release-wide integration cycle: `not assigned; maximum 5`
- Integrated role model: `Terra medium for integration cycles 1-5; Sol medium only
  for an explicitly authorized blocker escalation`

The Terra Integrator fills these fields before combined testing. Integrated work
uses this normal worker's same task boundary, canonical game launcher, installed
content staging, preflight, two-game contract, and minor-fix restraint. Prefer
`launch-ai-parallel.py --content <installed-runtime-content>` and verify the
isolated `SupportDir/Content` target before every game; an incomplete custom
launch command is not an integration setup.

## Why and predicted change

Temporary friendly units can occupy intended first-Fact enclosure cells while
the wall planner is repairing or rebuilding the perimeter. Current control at
the common base asks `ConstructionYardEnclosurePolicy.FirstLegalMissingRun` for
the first contiguous missing-and-placeable run in fixed segment order. That
policy neither expresses router-backed exhaustion of independent destinations
nor the literal corner-first/nearest order. A blocker, a route discontinuity, or
the current run choice can therefore leave a different intended cell empty even
though it is legal and reachable.

After this change, an ordinary enabled AI will use the best currently available
configured wall type and repair every missing intended enclosure cell that is
both currently legal and reachable under the normal router. It will select
corners first, then remaining cells closest to the bound first Fact with a stable
tie-break, and will continue through ordinary serialized queue/maintenance work
until no legal intended destination has a route. A temporarily occupied cell
remains part of the immutable plan and becomes eligible after it clears on a
later bounded early-game attempt. Permanent obstruction or no route is an
acceptable bounded partial result. The Fact footprint and the authored access
opening remain excluded, no general route/capture/transport behavior changes,
and ordinary construction and CNC-46 defense screens continue.

## Authoritative behavior

- Preserve literally: temporary friendly blockers must not strand other empty,
  legal, reachable intended wall cells. Repair every such cell.
- The enclosure plan remains the CNC-52 plan bound to the lowest-ActorID original
  Fact. Do not derive a new plan from occupancy, rebind to a later Fact, change
  its geometry, or close its deliberate three-cell access opening.
- Resolve the best available wall using the existing authored preference order
  `brik`, then `sbag`, then `cycl`, considering actual current buildability. If
  the preferred type is unavailable, fall back to the next available type; if no
  configured type is available, defer without consuming or redirecting a queue.
- Candidate destinations are missing cells in the immutable intended wall set,
  never access cells or any Fact-footprint cell. A cell must pass ordinary wall
  placement/build-radius legality at selection/placement time.
- Use the existing router's exact unblocked path cells to establish destination
  reachability from a deterministic Engineer-like origin near the Fact. The
  implementation may use a bounded virtual Engineer/capture-like route-probe
  context, but it must not create a live unit, spend cash, acquire CaptureManager
  or transport ownership, issue ordinary capture/movement work, or add persistent
  gameplay state.
- The Fact is never a routing destination and receives no special "ignore" or
  traversable/blocked exception. Exact path direction and endpoints must be
  verified; a path containing occupied/illegal cells, crossing the Fact, using
  transport, or reversed relative to the router contract is not acceptance.
- Select legal routed missing corners before all non-corners. Within each class,
  select closest to the bound Fact first and use explicit stable plan/coordinate
  tie-breaking. Unordered actor/hash iteration must not change the result.
- Serialize work through the existing single planner-owned pending destination,
  wall type, defense queue, production item, and placement. At most one current
  destination/request exists per ownership cycle; do not batch conflicting
  multi-Fact claims or issue duplicate orders.
- After each observed placement, continue on subsequent normal queue polls or
  maintenance work. Stop the attempt when the router cannot reach another legal
  intended destination. No-route is success for that bounded attempt, not proof
  the immutable plan is complete.
- Retain current periodic early-game deferral: configured maintenance cadence is
  250 ticks and the absolute cutoff is tick 7500. Do not change those values.
  Occupied/no-route cells may be retried only on that bounded lifecycle; no inner
  retry loop, recursive search churn, transport request, or post-cutoff repair.
- Preserve ordinary placement, cash, power/refinery recovery, wall-cap, queue,
  original-Fact-loss, save/load, and placement-time legality behavior. Critical
  survival recovery retains its current priority; a wall must not fake success
  or starve ordinary production.
- Preserve CNC-46 general defense-cluster ownership and open-screen behavior.
  Enclosure pending anchors/cells/reservations must not be overwritten or consumed
  by cluster work, and enclosure work must not claim cluster-owned cells.
- Preserve CNC-52 persistence: saved first-Fact identity and exact ordered plan,
  access cells, observed/issued cells, next scan tick, pending wall type/cell, and
  exact queue owner must validate deterministically. Stale or invalid ownership
  is released safely; a legacy/invalid plan does not bind a later Fact.

## Forbidden behavior and failure signals

- Any intended cell that is missing, legal, and routed when considered in the
  required corner-first/nearest sequence is skipped while a lower-priority cell
  is selected. A cell that becomes unroutable after a required earlier placement
  is an acceptable bounded terminal result.
- The planner repeatedly selects an earlier blocked cell while a different legal
  reachable candidate remains, or declares the plan complete because one cell is
  occupied/unreachable.
- Temporary occupancy deletes/reorders a cell in the plan or makes it permanently
  ineligible after the blocker clears.
- Corner-first, nearest-first, or stable tie order differs across identical seeds
  and state, or depends on dictionary/hash/actor enumeration.
- The deliberate access gap is walled; a Fact-footprint cell is considered a
  destination; the route crosses/ignores the Fact; the route contains a currently
  blocked cell; path order/endpoints are reversed or fabricated.
- A placement check, route request, queue reservation, issued order, or log is
  presented as acceptance without the intended wall actor's final presence.
- The virtual route probe becomes an in-world Engineer, spends/reserves resources,
  joins CaptureManager, issues capture/movement orders, requests transport, or is
  serialized independently.
- Concrete is selected when unavailable, fallback ordering is wrong, no available
  type causes queue churn, or a wall is silently substituted outside the authored
  preference list.
- Duplicate `StartProduction`, `PlaceBuilding`/`LineBuild`, capture-like, or Stop
  orders; multiple Facts/queues consume one destination; reservation ownership is
  leaked or stolen; placement failure enters an unbounded retry.
- Wall maintenance delays urgent refinery/power recovery, normal construction, or
  CNC-46 screen work enough to cause repeated material survival loss; ordinary
  capture, movement, production exits, or routing regress.
- Reordering, optimizing, or changing geometry/routing to preserve access to a
  farther intended cell after a required corner/near placement. Task-owner policy
  makes corner-first absolute; stop boundedly when no virtual-Engineer route
  remains.
- Work continues at/after cutoff, rebinds after original-Fact loss/capture, closes
  the access gap after load, restores stale owner/type/destination, duplicates an
  issued wall, or succeeds only in a reload run.
- Wall costs, HP, build speed, cap, access width, cadence, cutoff, prerequisites,
  unit stats, resources, probabilities, or any other balance change.
- Full-map/per-tick scans, unbounded path searches, nondeterministic ordering,
  uncontrolled allocations, swallowed errors, fake success, or published noisy
  per-candidate/per-path diagnostics.

## Current implementation and old-behavior control

Repository facts at `4e12088061ac277c51de2e658dc0209337b80968`:

- `BaseBuilderBotModuleInfo` owns `ConstructionYardEnclosureWallTypes`, margin,
  access width, cutoff tick, maintenance interval, debug flag, wall cap, and path
  locomotor. CNC AI personalities configure `brik, sbag, cycl`; the default
  cutoff/maintenance values are 7500/250 and normal per-AI caps are already
  authored. These are frozen policy/config.
- `BaseBuilderWallPlanner` owns enclosure identity/plan, observed/issued state,
  pending anchors/type/purpose, exact defense-queue reservation, periodic scan,
  placement legality, wall counting, diagnostics, save/load, and shared CNC-46
  cluster-screen planning. `TryPlanConstructionYardEnclosure` observes existing
  wall actors, checks remaining cap, calls `FirstLegalMissingRun`, truncates to
  remaining cap/line range, and produces line endpoints.
- `ConstructionYardEnclosurePolicy.CreatePlan` deterministically builds the first-
  Fact wall cells/segments and the authored access cells. Its current
  `FirstLegalMissingRun` walks each segment and returns the first contiguous
  missing legal run. Existing focused tests prove stable geometry, blocker
  splitting, destroyed-cell candidacy, plan retention under transient/fixed
  blockage, reservation overlap, cutoff/identity, save encoding/validation,
  exact queue ownership, stale restoration, and bounded polling. They do not
  prove CNC-107 route selection, corner/near ordering, or destination exhaustion.
- `BaseBuilderQueueManager` lets urgent power and serialized missing-refinery
  recovery precede enclosure selection; asks the wall planner before later normal
  choices; owns `StartProduction` and completed `LineBuild`/individual placement;
  and shortens defense-queue polling only while the enclosure is active. Normal
  placement avoids enclosure-reserved cells where a comparable legal alternative
  exists, with the existing bounded override when none exists.
- `ConstructionYardEnclosureBuildOwnership` serializes one exact queue/type from
  request to placement and validates restoration only when a matching build is
  queued. The state is serialized through the BaseBuilder save trait.
- `PathSearch`/the actor locomotor and pathfinder are the existing routing
  boundary. `MoveAlongPath` demonstrates exact bounded cell-path encoding/order;
  do not repurpose it as required gameplay movement. CaptureManager independently
  owns real Engineers, target reservations, capture orders, recovery, and
  transport exclusions; CNC-107 must not enter that ownership.
- History shows CNC-52 established bounded first-Fact enclosure repair and CNC-46
  established defense clusters; integration explicitly resolved their shared
  planner overlap before the common base. Treat those merged behaviors as current
  contracts, not code to revert or redesign.

Control protocol: prefer a same-build narrow feature-disabled toggle only if the
implementation naturally provides one without policy/config duplication.
Otherwise build the recorded common-base SHA in an isolated worktree. Match map
bytes, seed, factions, starts, options, initial actors/resources, bot types,
ordinary enemies, content and wall-type availability. Do not use the current
unrelated checkout as the control. The primary comparison is exact intended-cell
outcome/order and queue/routing side effects, not win/loss alone.

## Likely wrong approaches and challenges

- Merely changing `FirstLegalMissingRun` to return another contiguous segment
  cannot prove exact routed reachability or exhaustive legal destinations.
- Treating `world.CanPlaceBuilding` as route evidence conflates placement and
  movement; straight-line/Euclidean distance similarly fails on blocked/island
  topology.
- Removing occupied cells from the plan, caching them as permanently handled, or
  marking no-route as final violates periodic recovery after temporary traffic.
- Spawning/adding a real Engineer, borrowing CaptureManager assignments, issuing a
  synchronized capture order, or adding transport recovery expands scope and may
  contend with specialist ownership.
- Making the Fact ignored by the path graph can create a route through the
  footprint. Conversely, treating the Fact itself as the target violates the
  literal exclusion. Choose a deterministic legal origin beside it and validate
  the router's native blocker rules.
- Sorting only by distance ignores required corner priority; using distance
  without an explicit tie-break risks nondeterminism. Hard-coded coordinate
  partitions or a global optimizer are unnecessary.
- Planning all cells into a batch ignores world changes between production and
  placement, queue ownership, placement-time blockers, and the one-pending-result
  contract. Revalidate at the owning boundary.
- LineBuild across an occupied gap may have different final occupancy than its
  endpoints imply. Final evidence must inventory actual wall actors cell-by-cell;
  focused repair may need the existing individual-placement distinction without
  changing cluster behavior.
- The best wall type is current buildability in authored preference order, not
  strongest stats inferred in code. Do not copy build prerequisites or values
  into policy/tests.
- Reusing CNC-46 `KeepsBaseOpen`'s cheap flood fill as if it were the literal
  router exact-path requirement is insufficient; redesigning all routing to meet
  CNC-107 is also wrong.
- A selected corner or nearer cell can lie on the only route to a farther cell.
  This is now an explicitly accepted bounded terminal outcome: preserve literal
  ordering, do not invent a route optimizer or weaken corner-first, and stop when
  the virtual Engineer cannot route.
- Save schema changes may need a version bump and backwards-safe validation.
  Never restore a pending destination without its exact build owner/type or
  accept cells outside the configured plan.
- Diagnostic blind spots include confusing unavailable prerequisites, cash/cap,
  queue contention, lost Fact, cutoff, placement illegality, and no-route. Logs
  must distinguish them, but dumping every expanded path node per tick would
  distort MAX performance and is forbidden for publication.
- This runs in queue/maintenance paths, not a per-tick routing loop. Keep work
  proportional to the small intended perimeter, use a bounded router search, and
  compare MAX tick throughput/latency and allocations against control. Repeated
  map-wide scans or a path search per candidate per tick are regressions.

## Competing systems and ownership

- `BaseBuilderWallPlanner`: sole owner of first-Fact intended geometry, candidate
  selection, enclosure pending purpose/cell/type, observations, cadence/cutoff,
  and route-probe decision. Keep deterministic pure ordering/filter rules in a
  small world-independent policy helper; keep world actor/placement/path facts in
  the planner boundary.
- `ConstructionYardEnclosurePolicy`: owns pure deterministic plan/order/subset
  invariants and is the likely focused-test seam. Do not make it own World,
  queues, actors, pathfinder, or build prerequisites.
- `BaseBuilderQueueManager` and defense production queues: own actual production,
  cash consumption, completed placement orders, fail handling, and polling.
  Enclosure repair competes for the same defense queue and cash as CNC-46 walls
  and other defenses; urgent power/missing-refinery recovery remains ahead of it.
- `ConstructionYardEnclosureBuildOwnership`: owns the one exact queue/type claim.
  Multiple Facts and defense queues must not duplicate/steal a destination.
- CNC-46 `BaseBuilderDefenseClusterManager` plus cluster screen state: shares the
  wall planner, wall types, cap, queues, pending purpose, and some intended cells.
  Preserve cluster reservations, open-screen path safety, repair recovery,
  placement order, and progress.
- CNC-52 first-Fact enclosure: owns original-Fact identity, geometry/access,
  reservation avoidance by ordinary buildings, cutoff/cadence, observed/issued
  walls, and save/load. CNC-107 extends missing-cell selection only.
- Smart economy/opening/power and ordinary base construction: compete for cash,
  Facts, buildable area, and queue time. Exercise them enabled and verify recovery
  demand is not starved or canceled by wall work.
- Tiberium-field placement, first-tower/economy-SAM placement, and ordinary
  building placement pass through adjacent placement ownership in
  `BaseBuilderQueueManager`; they must neither consume enclosure wall work nor
  occupy reserved geometry when an existing comparable alternative is legal.
- `BuildingInfluence`, `ActorMap`, locomotor/pathfinder, and placement rules own
  current occupancy, terrain reachability, and build legality. Temporary unit
  presence is dynamic and must not be persisted as enclosure policy.
- CaptureManager owns real Engineer/capture targets, reservations, orders,
  recovery, and transport exclusions. A virtual Engineer-like path query cannot
  reserve or consume these actors/targets or emit gameplay orders.
- Wall actors, normal units, enemy pressure, and production exits consume/block
  map cells. The router must see ordinary blockers exactly as the full engine
  does; final cell actors and usable access/exit movement are acceptance.
- Save/load owns durable bound identity, plan/order, retry tick, observed/issued
  cells, pending destination/type, and queue owner. Route-probe ephemera should be
  recomputed rather than serialized unless a narrow invariant demonstrably
  requires otherwise.

## Dependencies

- CNC-46 general defense-cluster wall behavior and CNC-52 first-Fact enclosure
  ownership are already merged in common base `4e12088061ac277c51de2e658dc0209337b80968`.
  Preserve their contracts. Their source history may be inspected, but do not
  read their worker specs/reports.
- No open dependency PR is named for this packet. Monitor `origin/bleed` and tell
  the coordinator before rebasing if later commits touch
  `BaseBuilderWallPlanner`, `ConstructionYardEnclosurePolicy`,
  `BaseBuilderQueueManager`, related save state/tests, or CNC AI wall config.
- Material cross-worker warning: this task shares wall-planner pending state,
  defense queues, wall cap/types, placement cells and repair sequencing with
  CNC-46, while CNC-52 owns all first-Fact geometry/identity/persistence. A broad
  planner rewrite, changed access/cutoff/cadence, or shared queue behavior can
  regress both. Keep CNC-107 candidate/routing changes purpose-scoped.

## Spec policy consultation

- Partial spec: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260812-bug-polish-04/WORKER-3-CNC-107/ANALYSIS/SPEC-POLICY/PARTIAL-SPEC.md`
- Sol-high review/verdict: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260812-bug-polish-04/WORKER-3-CNC-107/ANALYSIS/SPEC-POLICY/POLICY-REVIEW.md` / `Mostly sensible; medium confidence; required follow-up`
- Adopted hypotheses: `Retain temporarily blocked cells in the immutable plan; continue to independent legal routed cells; preserve access gap, first-Fact identity, cadence/cutoff, single queue ownership, no-route deferral, and frozen balance. Treat the virtual Engineer/router result only as legality evidence with no gameplay/capture/transport state. Add the reviewer's highest-priority hostile-chokepoint game proving sequential corner/near placements do not seal the sole route to later cells or starve power/refinery recovery and CNC-46 screens. Require the control blocker to precede independent candidates and revise the harness if it does not reproduce the defect.`
- Rejected/deferred advice and why: `No design recommendation rejected. Do not preemptively change the literal corner/nearest order in response to the reviewer's caveat: the packet is authoritative, so a repeatable self-trapping conflict must be evidenced and escalated to the task owner. Win/loss-only strategic judgments remain deferred because the spec review had no match artifacts.`
- Scratchpad update: `Validated UTF-8 at 1,809 characters and atomically promoted under the cross-round one-slot lock. Added bounded authored-geometry repair guidance and the sequential self-trapping caveat to /root/github/LibertyDawn/.agents/references/LIBERTY-DAWN-POLICY-SCRATCHPAD.md.`
- Task-owner resolution (2026-08-13): `The speccer/reviewer caveat that an earlier
  corner must not strand farther cells is superseded. Fill non-blocked corners
  first, then cells closest to the Fact, until routing is impossible. If the
  virtual Engineer cannot route after a required placement, that is an acceptable
  terminal outcome. No optimizer, reordering, geometry/routing redesign, or
  balance change is authorized.`

## Acceptance plan

- Literal player-visible result: In a full-engine game, the bound original Fact's
  intended wall perimeter gains an actual own wall actor at each cell that is
  missing, wall-placement-legal, and exactly routed when considered in the
  required sequence before the cutoff. Actual corners appear before non-corners and
  remaining cells appear nearest-first with stable ties. Independent legal cells
  fill while another cell is occupied; after a temporary friendly blocker clears,
  that retained cell fills on a later bounded attempt. The access gap stays open,
  the Fact is never a route destination/path shortcut, permanent/no-route cells
  remain bounded, and economy/combat/cluster-screen behavior continues. Required
  earlier placements may make farther cells unroutable; bounded termination is
  acceptance, not a reason to reorder.
- Focused checks/instrumentation: Add/extend policy tests for corner classification,
  distance and tie ordering, blocker skipping without plan mutation, destination
  exhaustion, Fact/access exclusion, type fallback/no type, and bounded subsets.
  Add narrow integration seams/tests for exact path direction/endpoints, occupied
  path rejection, one queue owner, placement revalidation, and save/load version/
  owner/destination/type/cadence/cutoff. Temporary event-bounded diagnostics must
  identify tick, bound Fact, wall type candidates/selected type, intended cell,
  corner/distance/tie rank, placement status/reason, route origin/destination,
  exact returned path cells or no-route, reservation queue/type/owner, competing
  consumer, production request, placement order kind, observed wall and latency,
  retry tick, cutoff/stop reason. Remove noisy exact-path dumps before publication;
  retain only useful gated summaries at owning boundaries.
- Two-or-more distinct games per cycle: Each cycle must run two materially
  different scenario families, each with the full CNC engine, all features and AI
  modules enabled, ordinary real enemy AIs, intended map/options/factions/actors,
  and a 120-second wall-clock cap, normally headless MAX. Cycle 1 test 1 is a
  matched changed-versus-base-control compact connected scenario: original Fact,
  several missing walls including the first traversal corner plus equal-distance
  near/far cells, and temporary friendly blockers that clear at staggered ticks.
  Test 2 is a hostile chokepoint/blocked or Archipelago scenario with a permanent
  blocker, a destroyed segment, the sole route to a farther hole passing beside
  an earlier corner/near candidate, enemy attack, urgent power/refinery recovery,
  normal construction, a second Fact/defense queue, and active CNC-46 screen work.
  Both scenarios must inventory actual final actors; a run that does not reach
  world tick 1 or load the exact custom setup does not count. Later cycles must
  raise difficulty rather than repeat the same happy path.
- Old-control comparison/metrics: Prefer a same-build feature-disabled control if
  natural; otherwise use base SHA `4e12088061ac277c51de2e658dc0209337b80968`
  in an isolated worktree. Match map bytes, seed, starts, factions, bots, enemies,
  options, initial state/resources, timing and content. Measure exact intended
  legal+routed cells remaining empty at end/cutoff; corner/near/tie violations;
  ticks from blocker-clear/wall destruction to observed repair; duplicate request/
  placement count by cell; no-route/retry count and spacing; chosen wall type;
  Fact/access violations; queue ownership/contention; wall/economy/army value,
  survival/objective state and CNC-46 screen completion; ticks advanced, tick
  latency/spikes, CPU/peak memory or credible allocation signal. Changed behavior
  must materially reduce the exact set of avoidable empty cells and ordering
  failures with no material survival/ordinary-construction or simulation-cost
  regression. If control does not reproduce the early-blocker defect, revise the
  setup. Repeated parity, marginal gain or loss requires correction or a concrete
  task-specific explanation; feature-fire logs never suffice.
- Adversarial cases: Difficulty ladder spans (1) minimal full-engine smoke with a
  blocker on first corner and an independent legal destination; immediately then
  (2) staggered blocker clearing before/while/after wall production; (3) permanent
  actor and unreachable terrain; (4) corner/nearest equal-distance ties under
  changed actor enumeration; (5) hostile geometry where an earlier placement may
  seal the only later route; (6) concrete unavailable with sandbag/chain-link
  fallback, then no type available; (7) low cash, near wall cap, urgent power and
  missing-refinery recovery; (8) two Facts/queues and concurrent CNC-46 screens;
  (9) original Fact destroyed/captured and later Fact alive; (10) exact cutoff
  boundaries; (11) save before blocker clear and with pending production, reload
  before repair, plus invalid/stale owner data; and (12) long complete matches on
  ordinary connected and Archipelago topology. Failure signals/pass evidence are
  stated below under game contracts.
- Final regression: Run focused tests/build/lint and at least two final full-engine
  complete real-AI games—one ordinary connected map, one Archipelago or equivalent
  blocked/island topology—with all modules/features and ordinary enemies. Include
  a fresh non-reloaded scenario in addition to save/load. Confirm intended maps,
  options, bots, actors, wall cells/types, route path facts, ticks, scenario events,
  final actor inventory, access/production-exit movement, original-Fact binding,
  ordinary capture/movement/construction/economy/combat, CNC-46 screen completion,
  no errors/order spam/unbounded retries, and bounded tick/CPU/allocation behavior.

### Game contracts and failure hypotheses

1. **Connected matched blocker control, cycle-1 first behavior test.** Hypothesis:
   current control's first-run policy can leave independent legal routed holes or
   wrong order when the first corner is occupied. Stress: compact connected map,
   fixed seed, several missing corners/near/far cells, temporary friendly blockers
   on candidates that precede independent destinations, staggered clear events,
   normal AI/enemy/modules. Failure: changed AI leaves any concurrently legal
   routed cell, selects non-corner/greater-distance first, loses the plan cell,
   duplicates requests, or does not revisit cleared cells on cadence. Pass: exact
   route and selection evidence followed by actual walls in all eligible cells in
   order before cutoff; access gap/Fact excluded and materially fewer empty cells
   than matched base control. Run both builds/configs at <=120 seconds each.
2. **Hostile sequential-route/competition game.** Hypothesis: an early literal
   corner/near placement can seal the sole path to farther repair, which must end
   in bounded no-route without changing the required order, while wall demand
   can starve survival/cluster work. Stress: chokepoint/blocked-island geometry,
   one permanent blocker, destroyed segment, only later route beside an early
   candidate, enemy attack, urgent power/refinery recovery, low cash, normal
   construction, two Facts/defense queues and active CNC-46 screen. Failure:
   corner-first is weakened; the planner invents an optimizer/reorder; access/exit
   seals; duplicate ownership/spam; recovery or screen stalls materially; or a
   route crosses Fact/blocked cells. Pass: the required corner is selected and
   confirmed first, later routed cells continue nearest-first, and zero-route
   cells terminate boundedly while survival recovery plus screen progress visibly
   continue.
3. **Wall availability/cap/cutoff game.** Hypothesis: fallback or terminal causes
   can churn the queue or select an illegal/weaker-preference type. Stress concrete
   prerequisite unavailable then enabled, fallback type available, later no wall
   types, cash scarcity, cap boundary and tick 7499/7500. Failure: wrong type,
   post-cutoff request, repeated identical request/no-route per tick, or ordinary
   build stall. Pass: first actually available configured type, one bounded owner,
   correct defer/stop reason and continued ordinary construction.
4. **Persisted pending/blocker game.** Hypothesis: load can reorder destinations,
   lose a temporarily blocked cell, duplicate a wall, change type/Fact/queue owner,
   or resume after cutoff. Stress save with blockers and a pending build, then
   load before clear; include two Facts and later clear/destruction. Failure is any
   changed identity/order/state or duplicate/stale claim. Pass is exact validated
   restoration or safe release/replan, later actual repair on cadence, no later-
   Fact bind, and the same behavior in a separate fresh non-reloaded game.
5. **Complete topology regressions.** Hypothesis: route probing harms ordinary
   movement/capture/construction or MAX performance outside cheese setups. Stress
   full ordinary connected and Archipelago games with all modules, normal enemies,
   pressure and duration. Failure: errors/stalls, transport request, real virtual
   Engineer, path through Fact, sealed exit, CNC-46/CNC-52 regression, per-tick
   path/log churn, material tick throughput/latency/allocation loss. Pass: complete
   real-AI matches with correct wall outcomes, continued economy/army behavior and
   bounded performance.

## Implementation rules

- Investigate code, history, configs, tests, and evidence; choose the smallest safe
  solution. Preserve unrelated behavior and user changes.
- Keep responsibilities separate and ownership explicit. Prefer short cohesive
  functions/classes; split mixed or oversized logic when it improves clarity,
  testability, or hot-path cost without unrelated churn.
- Prefer simple fuzzy thresholds and game-sensible rules of thumb. Avoid global
  optimizers, graph solvers, rigid partitions, and elaborate state unless tests
  prove a simpler priority, count, distance, threat-map, or cooldown insufficient.
- Put tunable policy in owning rules/config and invariants in code. Do not hide
  production policy in tests or duplicate it across AI personalities.
- Freeze balance unless expressly authorized above. Never alter cost, HP, damage,
  armor, speed, timing, power, prerequisites, probabilities, or resources to make
  behavior pass; that invalidates evidence.
- Add proportionate focused tests. Log actionable handled errors at their owning
  boundary; never swallow failure, fake success, spam per tick, or publish noisy
  temporary diagnostics.
- Keep simulation work bounded: avoid repeated full-map scans, uncontrolled
  allocation, nondeterministic ordering, unbounded retries, and heavy logging.
- Inventory all modules competing for the same actors, queues, cash, reservations,
  repairs, targets, or orders, and exercise them with all modules enabled.
- Record out-of-scope ideas in the task report's deferred section. Do not create a
  task, edit shared deferred work, task sheet, coordinator state, or `bleed`.
- Desired implementation ownership: extend the pure enclosure policy only for
  deterministic candidate classification/order/state validation; keep router and
  live legality in the wall planner; keep production/placement in the queue
  manager; reuse exact queue ownership. Several designs may satisfy this; do not
  prescribe a new general router or capture abstraction without evidence.
- First implementation plan: establish a focused failing policy/control check;
  implement the smallest ordered candidate plus bounded exact-route selection;
  revalidate destination at production/placement; add save-state fields/version
  only if durable pending state truly changes; add gated event diagnostics; then
  run game 1 changed-versus-control before further code changes. Do not start with
  passive fixtures as behavioral acceptance.
- Publication plan: remove temporary path dumps, retain concise owning-boundary
  diagnostics, update REPORT with every game/control/reviewer conclusion and
  deferred risk, commit only task branch/state/report changes, open a PR to
  `bleed`, run focused/build/lint/CI and final full-AI regression, and never merge.

## One-cycle evidence loop

One cycle starts with a product/config change. Reading evidence or fixing an
invalid harness is not another cycle. For the current cycle:

1. Reread this state, diff, prior narratives/reviews, and unresolved evidence.
2. Make the smallest evidence-driven change and run relevant focused checks.
3. Run at least two materially different adversarial games. Every game must use
   the full engine, a custom scenario, all features, all AI modules, and
   ordinary enemy AIs from test 1. Normally use headless MAX and stop at 120
   seconds wall-clock; MAX may advance much farther in game time.
   Making this game launch and load the intended map is part of the worker's
   assignment. A process that dies, hangs, or remains before world tick 1 is not
   a game and does not count toward the cycle or its evidence. Repair task-local
   build/content/launcher/display/audio/process-cleanup/scenario problems and
   rerun; never repeat an identical broken launch as a nominal test.
4. Before each game record its failure hypothesis, changed pressure/assumption,
   exact failure signal, and player-visible pass evidence. Vary geometry, timing,
   resources, losses, counts, topology, competing managers, old-control setting,
   or save/load as relevant. Never spend both games on near copies.
5. Give each game—not a batch—to its own fresh Luna Commenter and Luna Policy
   Reviewer. Read both before deciding the next change. Verify narrative facts.
   The worker must carry the strongest policy recommendation into the next
   focused test or code change, or record an explicit rejection with a concrete
   scope, evidence, or safety reason; silently ignoring correct reviewer advice
   is not an acceptable cycle decision.
6. Remove answered/noisy diagnostics, update the journal/report/state, commit, and
   exit so the coordinator can select the next model tier.

Use `with_resource_slots.py` around shared resources and the game
launcher/supervisor as the completion helper. Await the bounded process/result;
do not burn agent turns sleeping or repeatedly polling. Isolate every map,
support directory, port, log, replay, save, benchmark, and display.

If targeted setup diagnosis cannot make the full engine reach world tick 1, save
the exact startup logs, command, process tree, and checkout comparison, then mark
the cycle blocked and request environment help. Do not advance the cycle counter,
produce a narrative for a nonexistent match, or claim task acceptance.

Custom setups should force rare decisions while retaining real AIs/modules: for
example pre-place damaged/healthy capturable structures and engineers, destroy a
critical asset, constrain resources, or pre-spawn opposing forces. Absence of an
unfinished prerequisite behavior is a dependency, not proof this task failed.

For strategic AI changes, prefer a same-build feature-disabled control; otherwise
use the recorded base or named known-good older AI in an isolated worktree. Match
map bytes, seed, starts, options, initial actors/resources, factions, and enemies.
Require material task-relevant improvement, not merely an activation log. Treat
repeated loss, parity, or marginal gain as likely code/policy error unless evidence
supports a task-approved tradeoff.

This task is routing-adjacent and persisted: include ordinary connected plus
Archipelago/blocked topology, save/load, and a fresh non-reloaded acceptance run.
For the maintenance hot path, require bounded perimeter work and bounded path
search at queue/maintenance cadence, not per tick; compare MAX ticks, latency/
spikes, CPU and peak memory or a credible allocation signal against control.

## Model-tier limits

- Cycle 1/Sol high: implement the coherent initial solution.
- Cycles 2-5/Terra medium: correct evidenced bugs and wrong assumptions. Do not
  casually redesign. After cycle 3 obtain one Luna code review with at most one
  advisory concern, record adoption/rejection, then continue to cycle 4.
- If unresolved after cycle 5, mark `Needs help` or `First iteration - testing`
  unless all remaining work is minor and obvious.
- Cycles 6-15/Luna medium: require coordinator authorization. Only narrow guards,
  config mistakes, assertions, obvious local bugs, and testing are allowed. No new
  architecture, strategic policy, balance, or broad refactor. Stop when the next
  fix requires judgment.

## Analysis isolation

For each game, stage only authorized artifacts for the Commenter. Stage its
`NARRATIVE.md`, a short current task context (ID/title, literal behavior, why,
category, in/out of scope, balance authority), design reference, and current
scratchpad for the Policy Reviewer. Use strict launcher JSON envelopes. Serialize
policy calls, validate the reviewer's replacement scratchpad as UTF-8 and <=3,000
characters, then promote it atomically. Keep detailed analysis ignored; record
concise conclusions/paths.

## Publication

Propose `Complete - testing` only when literal acceptance, required adversarial
evidence, final regression, checks, report, PR, and CI pass. Otherwise propose
`First iteration - testing` with exact failures and risks. A final Terra review
may return one compatible correction; it consumes an available cycle. Never merge
the PR.

The report records behavior, design/assumptions, cycle count, game scenarios and
artifacts, per-game narratives/policy advice, old-control results, diagnostics,
performance, checks/CI, deferred work, and risks.

## Cycle journal

| Cycle/model | Commit/change | Game 1 hypothesis/result/analysis | Game 2 hypothesis/result/analysis | Checks | Decision |
|---|---|---|---|---|---|
| 1 / Sol high | `057ee6fea8c059f171a57d8814c4385aacd96322`: deterministic corner-first/nearest one-cell selection, exact bounded native route validation, individual enclosure placement, current-buildability wall fallback | PASS: connected blocker run reached tick 7600, completed all 13 intended cells after blockers cleared, preserved access; matched base selected a non-corner first and sealed all three access cells. Commenter and reviewer PASS. | PASS: hostile Archipelago run reached tick 7600, completed all 12 independently possible cells, repaired a destroyed wall, bounded a permanent blocker, preserved access and first-Fact binding under low cash/power/defense pressure. Commenter and reviewer PASS. | Focused 23/23; full 572/572; Release `make all`; Debug `make check`; `git diff --check` | `First iteration - testing`; cycle 2 must carry engineered sole-route and save/load evidence plus longer-lived occupancy/interference. |
| 2 / Terra medium (setup repair; not consumed) | No product behavior change. Fixed the task-local batch setup to use this worktree's `launch-game.sh` and stage a declared custom map beside a save in isolated `SupportDir/maps/cnc/{DEV_VERSION}`. | PASS: fresh hostile save-at-tick-200 run (`GAME-1-R3`) reached tick 7600 with current `planned yard=`/`confirmed wall yard=` diagnostics and produced a valid save. | PASS: isolated reload (`GAME-2-R4-RELOAD`) reached tick 7600; restored original Fact `210`, confirmed the pending corner, then the next two cells, retained the permanent blocker, and stopped at cutoff. | `py_compile`; `tests/test_launch_ai_parallel.py` 4/4 passed; both full-engine summaries passed. | Runtime-evidence blocker resolved. Do not advance the cycle counter: this was harness repair/evidence only. Next authorized cycle must make a product/config change before its two new adversarial games. |
| 2 / Terra medium | Revalidated a pending enclosure destination with the exact bounded native route immediately before it can be consumed for production/placement; a newly blocked route now drops the pending endpoint for bounded later replanning. Added one focused route-blocked-before-placement policy check. | PASS (harness): connected transient-blocker MAX run reached tick 7600 in 14.02 s; required planner/confirmation patterns passed, with no fatal/desync/error. Commenter/policy verdict: evidence insufficient to prove a post-selection route block caused a defer. | PASS (harness): hostile Archipelago pending-save MAX run reached tick 7600 in 17.025 s; required planner/confirmation patterns passed, saved at tick 200, with no fatal/desync/error. Commenter/policy verdict: evidence insufficient to prove the constructed stale-route trigger or comparative benefit. | Focused `ConstructionYardEnclosurePolicyTest` 24/24; `git diff --check`; two full-engine summaries passed. | `First iteration - testing`; cycle 3 must provide bounded actor/route/issue-or-defer evidence for an injected post-selection blocker, a valid-route counterpart, and matched old-control. Obtain the required cycle-3 Luna review before cycle 4. |
| 3 / Terra medium | Debug-only stale-anchor wording now states the observed action precisely: deferred, with no production or placement issued. The canonical recovery changed only the task-local invocation to stage `--content /root/github/LibertyDawn/.build/cnc33a/runtime-content`. | MIXED: connected valid-route counterpart reached tick 7600 in 12.02 s, loaded and ran normally with no fatal/desync/error, but emitted no `confirmed wall yard=` event and therefore failed its required-pattern contract. | PASS: hostile Archipelago regression reached tick 7600 in 15.026 s, including its tick-200 save, all required planner/confirmation patterns, and no fatal/desync/error. | Shared two-game reservation; canonical launcher batch summary records 1 passed / 1 failed. | Startup blocker resolved and cycle consumed. Retain `First iteration - testing`; obtain the due Luna code review before any cycle-4 product change, then address the connected no-confirmation evidence. |
| 4 / Terra medium | `9a587e9911`: deterministically exhaust all viable authored access origins and accept the first exact-valid bounded native route; added disconnected-nearest/reachable-alternate focused coverage. | PASS for origin fallback, incomplete literal acceptance: connected game reached tick 7600; alternate `27,31` routed and confirmed both corners while nearest was isolated, then recovery resumed after cage clear. Both transient blockers repaired, but non-corner `25,27` remained absent without a later exact reachability observation. Commenter/policy pass the scoped behavior and request independent final inventory/access evidence. | FAIL/ESCALATE: hostile sole-route game reached tick 7600; pre-corner neutral probe traversed beyond the far hole, corner `28,19` confirmed, then `25,19`, `26,19`, and `27,19` remained legal but unrouted through cutoff. Commenter recorded the behavior; policy reviewer judged the sequential self-trap established and required task-owner escalation. | Focused 25/25; full NUnit 574/574; `git diff --check`; two materially different full-engine games plus fresh per-game Luna commenter/policy reviews. | `Needs help`; do not weaken literal corner-first order, add an optimizer, or redesign geometry/access. Task owner must resolve order versus route preservation before cycle 5 product work. |
| 5 / Sol medium | No product algorithm change; reconciled the stale speccer acceptance caveat with the task owner's absolute corner-first/terminal-no-route ruling. | PASS: connected asymmetric-origin game reached tick 7600; both corners used alternate origin `27,31`, transient blockers recovered, all five missing cells appeared in final inventory, and authored access cells remained unwalled. Fresh Luna policy review found the wall behavior sensible and requested stronger explicit traversal telemetry. | PASS: hostile sole-route game reached tick 7600; corner `28,19` was rank 1, routed and confirmed, then three farther legal cells remained `routed=0` through bounded cutoff. Fresh Luna policy review judged this compliant with the resolved policy. | Focused 25/25; `git diff --check`; two distinct full-engine games passed; separate fresh Luna commenter/policy reviews completed. | `Complete - testing`; no optimizer/reorder/geometry/routing/balance change. Explicit bidirectional access telemetry is rejected as a release blocker because current inventory preserves the opening and prior cycle-1 games directly traversed it; retain as advisory observability. |

## Handoff receipt

- Proposed status: `Complete - testing`
- Branch/head and PR/checks: `agent/round-20260812-cnc107-wall-repair` / `9a587e9911` before the cycle-5 state/report commit; PR pending publication. Cycle-5 focused policy tests passed 25/25 and `git diff --check` passed. Cycle-4 full NUnit passed 574/574; prior Release `make all`, Debug `make check`, map validation, and runner tests remain passing evidence.
- Cycles/models used: `1 / Sol high`, `2-4 / Terra medium`, `5 / Sol medium`; `5/5` primary cycles used. The earlier Terra launcher repair remains non-consumed because it made no product/config change.
- Cycle-4 correction/evidence: commit `9a587e9911` replaces single-nearest-origin probing with deterministic bounded exhaustion of every viable authored access origin. `ANALYSIS/CYCLE-4/LIVE-GAMES/connected-asymmetric-origin` reached tick 7600 and directly confirmed routes from alternate `27,31` while nearest `25,31` was isolated, followed by recovery from `25,31` after clearing. Both transient blocker cells appeared, but `25,27` remained absent with no later exact reachability observation; therefore the fresh Luna pass is scoped to origin fallback and requests independent post-run wall/access assertions before literal acceptance.
- Resolved prior blocker: the task owner made corner-first absolute and accepted bounded no-route after required placements. Cycle 5 reproduced the sole-route result cleanly: corner `28,19` was rank 1, routed and confirmed, then `25,19`, `26,19`, and `27,19` remained placement-legal with `routed=0` until cutoff. This is now an accepted terminal outcome; no optimizer, reorder, geometry/routing redesign, or balance change was made.
- Acceptance/adversarial/final-regression evidence: Cycle-1 changed artifacts are under `ANALYSIS/CYCLE-1/CHANGED-GAMES-R6`; connected and hostile Archipelago games both reached tick 7600. Connected completed all 13 intended cells after transient/ordinary blockers and kept the access open. Archipelago completed all 12 independently possible cells, repaired a destroyed wall, bounded one permanent blocker, retained original-Fact binding amid low cash/power, defense demand, enemies, and later Facts, and kept access traversable. This is adversarial first-iteration evidence, not final regression.
- Old-control comparative result: Matched base `4e12088061ac277c51de2e658dc0209337b80968` control under `ANALYSIS/CYCLE-1/CONTROL-GAME` selected a non-corner before reachable corners and its single-anchor `LineBuild` created walls in all three authored access cells, materially reproducing the defect.
- Per-game narrative and policy-review paths/conclusions: Game 1 `ANALYSIS/CYCLE-1/GAME-1/NARRATIVE.md` and `POLICY-REVIEW.md`: changed run completed all intended cells in required order and preserved access; PASS/high confidence. Game 2 equivalents: every independently possible cell filled, repair/permanent-blocker/first-Fact/access behavior held; PASS/0.94 confidence. No scratchpad replacement.
- Cycle-3 code review/disposition: fresh Luna-medium review recorded at `ANALYSIS/CYCLE-3/CODE-REVIEW.md`. Its accepted access-origin defect is corrected by `9a587e9911` and covered by the cycle-4 focused test/full-engine connected game. No other blocking ordering, ownership, persistence, or CNC-46/CNC-52 defect was found in that review.
- Policy recommendations/disposition: Cycle-5 Game 1 `ANALYSIS/CYCLE-5/GAME-1-COMMENT/NARRATIVE.md` and `GAME-1-POLICY/POLICY-REVIEW.md` find the corner-first fallback/final inventory sensible and request explicit bidirectional access-result telemetry. The release-blocker interpretation is rejected on scope/evidence grounds: final inventory excludes every authored access cell, the probe is outside the opening, and cycle-1 connected plus Archipelago evidence directly recorded traversal; retain better terminal/access logging as advisory observability. Game 2 equivalents judge required corner-first followed by bounded no-route compliant and recommend no policy change. The valid 1,684-character Game-1 scratchpad replacement was promoted under the one-slot lock; Game 2 proposed no compatible task-scoped addition.
- Diagnostic/performance result: concise boundary diagnostics retained; no route-cell/expanded-node dump. Matched connected ticks 101-7600: mean tick `0.829 ms` changed vs `0.779 ms` control, maxima `214.17` vs `218.13`; mean bot tick `0.429 ms` vs `0.354 ms` (`+0.075 ms`), maxima `38.21` vs `33.91`. No material spike, but ambient contention differed, so later final evidence needs a single-process matched performance/allocation run.
- Deferred work and known risks: explicit terminal-reason and bidirectional access-result telemetry, additional no-type/cap/original-Fact-loss/stale-save permutations, and a matched single-process allocation benchmark remain advisory. Persisted pending owner/type/destination plus blocker already passed the cycle-2 reload evidence; cutoff, type fallback, stable order, ownership, and plan validation have focused coverage. Strict live-actor path occupancy may defer routes until cadence retry; that is intentional.
- Runtime-evidence resolution: `ANALYSIS/CYCLE-2/GAME-1-R3` passed the unchanged hostile map/save-at-tick-200 manifest using this task worktree's launcher, not `/root/github/LibertyDawn/launch-game.sh`; it emitted `planned yard=` at tick 1 and confirmations at 410/809/1208. `GAME-2-R4-RELOAD` staged the same custom map in its isolated support map directory, restored `yard=210/fact@25,20` at tick 201, confirmed the pending `28,19` wall at tick 463, completed the next two legal cells, bounded `24,19` as occupied, and stopped at tick 7500. Both reached tick 7600 with passing summaries. The earlier `GAME-1`/`GAME-1-R2` artifacts remain non-counting diagnostics.
- Cycle-2 product/evidence result: `ANALYSIS/CYCLE-2/CYCLE-2-LIVE-GAMES` contains isolated connected and hostile full-engine MAX runs. Both reached tick 7600 and passed their pattern/error contracts; the hostile run saved a pending state at tick 200. Fresh Commenter and Policy Reviewer results under `GAME-{1,2}-COMMENT` and `GAME-{1,2}-POLICY` judged the behavior directionally sensible but the staged summary/console evidence insufficient: it lacked the exact post-selection blocker, actor/route/revalidation/defer trace, and matched control. The next cycle must construct and retain that evidence rather than infer acceptance from harness patterns.
- Cycle-3 startup blocker: `ANALYSIS/CYCLE-3/LIVE-GAMES` retains the exact manifest, commands, console/support logs, and `batch-summary.json`. Both isolated launches used this worktree's launcher, `mods/modcontent`, distinct support directories/displays, and the shared two-game resource lock; both hung after `Loading mod: cnc` / `Loading mod: modcontent`, timed out at 120 seconds, and never activated headless/MAX or reached world tick 1. This is not a game or reviewer input. Request environment/runtime-content help before retrying; do not advance the cycle counter or treat the diagnostic wording change as accepted behavior.
- 2026-08-12 worker handoff: reread the contract and confirmed no new runtime/content remediation or retry authorization is present. Preserved `Blocked - environment startup`, current cycle `3`, and cycles used `2/5`; no build, code, game, or reviewer action was performed. Coordinator/environment owner must resolve the `cnc`/`modcontent` startup hang using the retained cycle-3 artifacts before authorizing another attempt.
- 2026-08-12 follow-up handoff: reread this complete contract and reconfirmed the worktree remains clean at `2070518b8a`. The only currently authorized action remains the environment-startup handoff; no retry, build, source edit, game, reviewer invocation, or cycle-counter change was performed. Preserve `Blocked - environment startup` until the coordinator/environment owner resolves the retained `ANALYSIS/CYCLE-3/LIVE-GAMES` `cnc`/`modcontent` launch hang and explicitly authorizes a new attempt.
- 2026-08-12 canonical recovery: ran the two authorized Terra games under the shared two-game reservation with this worktree's `launch-game.sh` and canonical `--content /root/github/LibertyDawn/.build/cnc33a/runtime-content`. Both reached world tick 7600, resolving the prior `cnc`/`modcontent` startup hang. `ANALYSIS/CYCLE-3/CANONICAL-RETRY/hostile-archipelago-regression` passed its complete contract in 15.026 s, including save-at-tick-200 and planner/confirmation evidence. `.../connected-valid-route-counterpart` reached tick 7600 in 12.02 s without fatal/desync/error but failed only because `confirmed wall yard=` was absent; its log records plans at ticks 3270 and 5794 but no confirmation. This is a real mixed-evidence cycle, not an environment block. Cycle 3 is consumed (`3/5`), status is `First iteration - testing`, and the mandatory Luna code review remains due before cycle 4. No source or balance change was made.
- 2026-08-13 cycle-3 review handoff: completed the mandatory fresh Luna-medium code review without editing source or running builds/games. The review found one major defect and no other blockers: route selection tests only the nearest enterable access origin and can falsely defer a destination reachable from another authored access cell on asymmetric terrain. Accepted the finding. The next authorized cycle-4 product change is deterministic fallback across all viable access origins plus focused coverage; cycle 4 games remain blocked until that correction passes. Current cycle and counters remain `4` and `3/5`.
- 2026-08-13 cycle-4 handoff: committed deterministic viable-origin exhaustion as `9a587e9911`; focused 25/25 and full NUnit 574/574 passed. Connected asymmetric-origin evidence passed at tick 7600 and resolves the prior no-confirmation/origin-fallback concern. Hostile sole-route evidence reached tick 7600 and showed corner `28,19` convert three farther placement-legal cells from reachable-before to `routed=0` after confirmation. Fresh Luna policy review requires task-owner escalation. Status is `Needs help`; current cycle `5`; cycles used `4/5`; no PR. Do not begin another product cycle until the policy conflict is explicitly resolved.
- 2026-08-13 cycle-5 handoff: task-owner policy resolved corner-first as absolute and virtual-Engineer no-route as an acceptable terminal outcome. No product algorithm changed. Two distinct full-engine MAX games passed to tick 7600 in 14.019/19.025 seconds with separate fresh Luna narratives and policy reviews. Connected final inventory contained all five repaired cells and preserved the access opening; hostile sole-route confirmed the required corner then stopped boundedly with three legal cells at `routed=0`. Focused tests passed 25/25 and `git diff --check` passed. Proposed status is `Complete - testing`; publish the task PR after the state/report commit.
