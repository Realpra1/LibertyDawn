# Worker State: CNC-41

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `worker-3-cnc41`
- Task: `CNC-41 — Economy Tiberium fields`
- Change category: `AI economy, structure-placement, route-preservation, and persisted field-maintenance policy`
- Balance authority: `Frozen except for the exact requested AI-policy surface: one Resonator per configured live Tiberium tree; a four-cell red-tree enclosure; roughly six-cell Power Plant extension steps; and a 60-second (1500 game-tick) incomplete-enclosure maintenance cadence. The enabled field policy may supersede a generic Resonator fraction/cap only as required to satisfy one-per-tree cardinality, but may not tune costs, HP, armor, power, build time, prerequisites, resource values/growth/explosions, unit behavior, probabilities, or any other balance value.`
- Status: `First iteration - testing`
- Common base branch/SHA: `agent/cnc-20260806-bug-polish-01-release` / `419bee2531d4802bf922c3597b42c6eeb75ab250`
- Task branch: `agent/round-20260807-cnc41-economy-tiberium-fields`
- Intended PR base: `agent/cnc-20260806-bug-polish-01-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `20`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/workers/worker-3-cnc41/COORDINATED-CNC-ROUNDS/20260807-bug-polish-02/WORKER-3-CNC-41/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-3-cnc41`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `42`
- Terra cycle code reviews: `cycle 5: advisory concern adopted; red-tree lifecycle configured but unimplemented; cycle 10: advisory concern adopted; completed red enclosure identity, 1500-tick missing-only maintenance, and save/load reconstruction are absent; cycle 15: advisory concern adopted; terminal red segment state is rejected after completed enclosure and before Resonator placement; cycle 20: advisory concern adopted for handoff; real ordinary and reserved-stealth harvester route proof is absent before red activation eligibility`
- Sol-xhigh policy escalation: `unused (requires at least 10 game tests; one maximum)`
- PR: `https://github.com/Realpra1/LibertyDawn/pull/88` (draft; final-review CI-only response removes the single unused using; replacement checks pending)

## Integrated repair assignment

- Phase: `isolated implementation`
- Current release branch/head: `not assigned`
- Integration notes: `not assigned`
- Repair branch: `not assigned`
- Repair PR base: `not assigned`
- Integrated cycles used this RC: `0/3`
- Integrated cycles used total: `0/12`

Before relaunching this worker for combined testing or repair, the integrator must
replace these fields with the exact release head, note path, branch, and counters.
During that phase, the repair branch replaces the original task branch as the
writable branch; the task scope and behavioral contract do not change.

## Why and predicted change

The requested behavior is literal: **"Build one resonator per Tiberium tree. For
red trees, first enclose the tree/resonator at four cells using the best
walls/sandbags, leaving an entrance occupied by an accessible building edge or
repair pad; extend build radius with power at roughly six cells when needed.
Repair incomplete walls every 60 seconds. Prove harvest access and containment
on a validated red-Tiberium map such as Empire Earth/Empire Red."**

At the recorded base, Resonators are only ordinary entries in the Brutalis and
IronReaper building fractions. The queue manager classifies them as refineries
and places them near a shuffled resource cell; no code maps a Resonator to a
blossom tree, plans remote construction, contains red fields, leaves a deliberate
gate, or revisits missing field walls. The existing wall planner handles the first
Construction Yard and straight walls in front of towers only. This makes
Resonator construction accidental and permits duplicate/ineffective placement,
uncontained accelerated red growth, traffic obstruction, and no recovery after
an enclosure segment disappears.

The predicted player-visible change is an Economy-capable AI that serially and
deliberately develops fields: it extends build area only when required, completes
one correctly ranged Resonator for each configured live blossom tree, contains a
red tree and its Resonator before activation, preserves a working harvester gate,
and replaces genuinely missing containment every 60 seconds. The result must be
more usable delivered income and field persistence than the old control without
starving core economy, power recovery, combat production, or survival.

## Authoritative behavior

1. Treat configured blossom-tree actor types (`split2`, `split3`, `splitblue`,
   and `splitred` in CNC rules) as durable field identities. Treat `splitred` as
   the red-tree subset. Detect trees that appear after a transform as well as
   trees present at match start. Do not infer a red **tree** merely from a red
   resource cell.
2. For every live configured tree, eventually maintain exactly one owned, live,
   complete, powered Resonator whose six-cell effect covers that tree. A live,
   queued, produced-awaiting-placement, or explicit queue reservation belongs to
   exactly one tree; parallel Facts must neither duplicate one assignment nor
   let one Resonator satisfy two trees while another remains unserved.
3. Work only a bounded number of cash-consuming projects, normally one. Rank
   deterministically using observed useful field demand, route/safety
   feasibility, and remaining commitment (extension, perimeter, Resonator), not
   distance alone. The eventual one-per-tree requirement remains after temporary
   pauses.
4. Admit or continue discretionary field spending only while the existing owners
   of critical policy show that the AI has a functioning unloading refinery and
   usable storage, adequate power margin, a viable harvester round trip, no
   serialized missing-refinery or opening emergency, and protected access to
   combat/core-economy cash and production. Under immediate existential pressure,
   pause a new or early project and resume after recovery. A breached enclosure
   around an already active red-field Resonator has higher urgency, but never
   closes the gate or preempts missing refinery/power recovery.
5. Do not force a technology choice. Start a part only when it is currently
   buildable. Brutalis/WaveMaker share the Economy-oriented BaseBuilder block;
   IronReaper may switch branches. Losing Economy III pauses unbuildable work and
   later resumes it from live world state without upgrade spam or duplicate
   reservations.
6. If the planned site is outside legal construction area, extend toward it with
   an ordinary configured Power Plant only when necessary. Each successful plant
   must make roughly six cells of useful progress, remain legally connected under
   the engine's existing `RequiresBuildableArea` rules, avoid resource and traffic
   cells, and actually make the next project part placeable. The task does not
   authorize changing build radius, footprints, `Adjacent`, power output, or
   plant cost.
7. For a non-red tree, choose a legal Resonator footprint that covers the tree
   and keeps gatherable resource and refinery approaches traversable. No enclosure
   is required merely because nearby resource has evolved to red.
8. For a red tree, plan a perimeter at a four-cell standoff that contains both
   the tree and planned Resonator footprint. Complete containment before the
   Resonator becomes live/powered. Prefer concrete only when currently buildable,
   affordable for the remaining perimeter, geometrically suitable, and compatible
   with route preservation; otherwise use sandbags. Do not use mined chain-link
   (`cycl`) as automatic red-field containment.
9. Leave a deliberate entrance aligned with an accessible, stable owned
   build-radius building edge or Repair Facility. Target at least two contiguous
   harvester-passable cells where terrain permits. A narrower gate is acceptable
   only when geometry forces it and repeated live ordinary-harvester traffic in
   both directions proves it does not chronically jam. Prefer a Repair Facility
   where practical; do not build one solely as a ritual gate when an existing
   safe building edge satisfies construction and traffic.
10. Prove the route with the actual ordinary harvester locomotor from an owned
    refinery approach through the entrance to gatherable field cells and back.
    Preserve access for ordinary HarvesterBotModule actors and for a stealth
    harvester reserved by RedTiberiumBombBotModule. Empty-cell geometry or one
    successful path query is not enough.
11. Every 1500 game ticks (60 game seconds), rescan active red enclosures. Walls
    already present but damaged use their existing self-heal and are not duplicate
    work. Missing, destroyed, captured, or never-completed coverage is queued for
    replacement, with stale queue ownership released and the intended entrance
    preserved. Repeated impossible placements defer visibly with bounded retries.
12. Recover after destruction/capture of an extension plant, gate structure,
    wall, Resonator, queue actor, or tree. Re-evaluate affected downstream pieces;
    do not rebuild unrelated infrastructure or retain a reservation whose tree,
    queue, placement, or actor no longer exists.
13. Save/load must either persist and validate or deterministically reconstruct
    tree assignments, project phase, queue reservations, planned cells, entrance,
    maintenance due time, and retry/deferred state. A reload must not duplicate a
    Resonator, forget a breach, close a gate, or immediately burst retries.
14. Keep field policy configurable in the owning AI rules and generic engine code
    opt-in. Enable it only for intended Economy-capable CNC profiles; do not make
    VIKI, Skynet, easier bots, or another mod silently adopt Economy behavior.
15. CNC-42 guard assignment is out of scope. CNC-41 may expose a cohesive field
    identity/entrance/harvest-access observation that CNC-42 can consume later,
    but it must not reserve combat units, station guards, or add field-defense
    targeting.

## Forbidden behavior and failure signals

- More or fewer than one live/committed Resonator per live configured tree after
  the project has had a feasible opportunity to finish; a Resonator outside
  effective range; or an association that exists only in logs.
- Resonator placement on harvestable resource, across a refinery approach, in an
  infantry/vehicle corridor, or where accelerated growth predictably seals base
  movement.
- Completing or powering a red-tree Resonator before its real four-cell-standoff
  containment and usable entrance exist.
- A closed ring; maintenance that fills the intended gate; a gate that only a
  test locomotor can traverse; or accepting a one-off route while multiple live
  harvesters remain idle, trapped, or unable to unload.
- Treating red cells on Empire Earth as proof that the `splitred` actor branch was
  exercised. Acceptance must identify the intended actor type, location, and
  resulting enclosure.
- Using `cycl` merely because it is in the generic wall preference; increasing
  the generic `MaximumWallSegments` so unrelated tower/yard wall behavior changes;
  or changing wall/plant/repair-pad/Resonator/resource/harvester stats.
- Starting all tree projects concurrently, permanently reserving cash or queues,
  starving refinery/silo/power/opening/combat production, or continuing an early
  field project solely because money has already been spent.
- Counting a Resonator as a functioning unloading refinery in admission/recovery
  logic. It has no `Refinery`/`StoresResources` behavior even though current
  BaseBuilder `RefineryTypes` includes it; use the existing smart-economy refinery
  ownership or actual unloading capability.
- Conflicting with BaseBuilder opening, smart-economy, first-tower, construction-
  yard/tower-wall, low-power, air-repair, and ordinary fraction choices; consuming
  the wrong queue's wall intent; or duplicating requests across multiple Facts.
- Stealing or reordering ordinary or red-bomb harvesters, changing resource-claim
  policy, or implementing CNC-42 guards.
- Reissuing an order every tick, retrying impossible terrain forever, scanning
  all map cells/actors every tick, nondeterministic collection iteration, or
  unbounded per-tick allocations/logging that reduces headless MAX throughput.
- Swallowing placement/load errors, substituting a fallback placement and calling
  it success, leaving noisy temporary diagnostics, or accepting only unit tests,
  fixtures, requests, reservations, movement orders, save reloads, or activation
  messages without the final visible result.

## Relevant current implementation and control behavior

- Base and history: `419bee2531d4802bf922c3597b42c6eeb75ab250` is
  the exact task/PR base. It already contains the construction-yard enclosure
  work from `4e65c05fed`, smart-economy/refinery reservation work, red-Tiberium
  economy/raid work including `45b21055d4`, and the completed CNC-39/CNC-39A
  integration. No CNC-41 implementation or active CNC-41/CNC-42 branch/PR was
  found at specification time.
- `mods/cnc/rules/trees.yaml` defines `split2`/`split3` (green), `splitblue`,
  and `splitred`, all through `^TibTree`; transformable ordinary trees can create
  those actors when adjacent resource reaches the corresponding type.
- `mods/cnc/rules/structures.yaml` defines the two-cell `resonator`: cost 1500,
  Economy III/high-tech prerequisites, -100 power, six-cell effect, and resource
  modification. Its red-resource override suppresses spontaneous red instability
  while powered. These facts constrain behavior but none of their values may be
  changed.
- `BaseBuilderBotModule.cs` is already 1168 lines. It owns opening, smart economy,
  power/refinery common names, queue creation, rally points, first-tower state,
  save/load, and other policies. Its `HasAdequateRefineryCount` counts configured
  common names. Brutalis and IronReaper currently configure `RefineryTypes: proc,
  resonator`, even though a Resonator does not accept or store delivered
  resources; their `SmartEconomyRefineryTypes` correctly contains only `proc`.
- `BaseBuilderQueueManager.cs` is 679 lines and owns both `Building.*` and
  `Defence.*` queues. A finished ordinary actor is classified as defense,
  refinery, or building and passed to generic placement. Refinery placement scans
  shuffled resource cells around a random Construction Yard, so a Resonator has
  no tree identity or range guarantee. Choice priority is low power, missing
  refinery/smart economy, construction-yard walls, opening, first tower, refinery
  congestion, air repair, production, silo, then shuffled fractions. Field work
  must integrate with these owners rather than bypass them.
- `BaseBuilderQueueManager.TickQueue` can cancel an unplaceable finished actor;
  its current `failCount += failCount` does not increase a zero count. Do not rely
  on that global backoff for field correctness and do not broaden this task into
  an unrelated placement rewrite. Field reservations/retries need their own
  bounded, observable lifecycle; record the global issue as deferred if it
  remains relevant.
- `BaseBuilderWallPlanner.cs` (479 lines) and `BotWallGeometry.cs` implement two
  different ordinary wall purposes: a complete first-Construction-Yard perimeter
  and a straight line before a defensive tower. `LineBuild` consumes planned
  anchors and fills between them. The planner has no field identity, no deliberate
  gate, no 60-second rescan, and no save state. Its global wall count includes all
  configured wall types and the CNC AI cap is normally 24. A four-cell field
  perimeter can exceed that cap, so simply reusing or inflating this policy would
  either fail literal containment or alter unrelated walling.
- Current CNC walls require existing buildable area with `Adjacent: 5`, have
  `LineBuild.Range: 15`, and use `ChangesHealth` self-heal. They do not expose
  `RepairableBuilding`, so `BuildingRepairBotModule` cannot perform the requested
  missing-segment maintenance. The repair bot only reacts when an attacked
  repairable building crosses a damage-state threshold.
- Ordinary base buildings, including `nuke`, `nuk2`, `fix`, and `resonator`, use
  `RequiresBuildableArea: Adjacent: 4` and provide buildable area. `BuildingInfo.
  IsCloseEnoughToBase` checks real footprint adjacency (and optional
  `BaseProvider` range). Thus “roughly six cells” is an AI placement step, not
  authorization to change engine adjacency.
- `HarvesterBotModule` discovers owned harvesters every 50 ticks, respects every
  `IBotUnitReservations`, finds a claimed reachable resource cell using the real
  locomotor, and issues `Harvest`. `FindAndDeliverResources` remembers the last
  harvested cell and returns through a refinery delivery cell. This is the
  ordinary traffic path that containment must preserve.
- `RedTiberiumBombBotModule` is enabled for all CNC bots. Every 50 ticks it scans
  red resource cells, reserves eligible stealth harvesters through
  `IBotUnitReservations`, claims/reaches an actual red cell, and sends the armed
  harvester away from its refinery toward an enemy. A red-field entrance must
  allow this competing mission as well as ordinary harvesting.
- Only the shared Brutalis/WaveMaker BaseBuilder and IronReaper currently author
  Resonator building fractions/limits (Brutalis limit 75/fraction 5; IronReaper
  limit 80/fraction 20; both delay ordinary fraction selection until tick 7200).
  IronReaper's technology counter owns Economy upgrades/downgrades. Generic
  task code should be opt-in and current buildability-aware rather than changing
  other AI identities.
- `mods/cnc/maps/Empire-Earth.oramap` passes `utility.sh cnc --check-yaml`, has
  OpenRA map hash `7e1899fb3dc54edfaee043bcc3a2b89de1c82ecb`, package SHA-256
  `de517bb85418139fb1125888578c06516bea223d0c22652d038e48e70d1d64cc`,
  140 initial `splitblue` actors and 44 red resource cells, but **zero initial
  `splitred` actors**. It is useful for scale/transition evidence but is not by
  itself immediate proof of the red-tree branch.
- `mods/cnc/maps/Red Dawn.oramap` is the strongest packaged red-tree acceptance
  map found. It passes `--check-yaml`, has OpenRA map hash
  `9773a7c85dbaa2f7ca6471ad9938810982f76d8e`, package SHA-256
  `90703eb201b166c479c330d2dca98e67e367cf8846ca321b9a7b0ffdcdff7966`,
  36 initial `splitred` actors, and 645 red resource cells. Use it for the required
  packaged-map red-tree proof. `mods/cnc/maps/archipelago` also validates (OpenRA
  hash `5db1ecc4f09e91aad51f4b7adfbe2661d496d437`) and supplies blocked/island
  topology, but needs a validated test-only tree setup to exercise CNC-41.
- Existing focused tests include `BotWallGeometryTest`, `OpeningPolicyLogicTest`,
  and `SmartEconomyPolicyTest`; none tests tree association, field-project state,
  entrance traffic, extension planning, or field maintenance.

## Likely wrong approaches and challenges

- Adding more special cases to the already oversized queue manager or turning the
  tower/Construction-Yard wall planner into a multi-purpose field state machine.
  Prefer a cohesive field manager plus small world-independent policy/geometry
  helpers, with narrow queue/placement integration.
- Leaving policy literals in code. Actor type lists, enablement, wall preference,
  Resonator/power/repair-building alternatives, the authorized 4/6-cell geometry,
  and 1500-tick cadence belong in the owning BaseBuilder AI config; deterministic
  reservation, validation, and lifecycle invariants belong in code.
- Reusing generic `BuildingFractions`, generic refinery placement, or nearest
  resource cells and then guessing which tree a Resonator serves. Association
  must exist before production and be validated after placement.
- Treating every red resource cell as a red tree, or using Empire Earth's red
  cells without proving a live `splitred`. Actor transformation and resource
  evolution are different state transitions.
- Reusing `EnclosurePerimeter` unchanged. It intentionally creates a full ring,
  has no gate, becomes permanently “handled,” and does not revisit missing cells.
- Raising `MaximumWallSegments` globally or counting only paid LineBuild anchors.
  The player-visible enclosure consists of every spawned wall actor, and changing
  the generic cap would also change Construction-Yard/tower wall behavior.
- Hard-coding a six-cell delta without checking existing footprint edges,
  placement legality, buildable-area gain, terrain, resources, route, or whether
  the plant actually advances the project.
- Calling `RepairBuilding` on walls. They self-heal and are not repairable
  buildings; “incomplete” means missing containment coverage, not merely HP below
  maximum.
- Reserving one field globally without queue identity/expiry, or letting two
  Building/Defence queues consume the same planned actor/cell. Record request,
  accepted queue, production, placement, completion, and release separately.
- Using a single pathfinder success as gate acceptance. Turning, two-way traffic,
  resource claims, multiple harvesters, a reserved bomb harvester, and maintenance
  changes can still jam it.
- Optimizing only Resonator count or raw resource density. Charge the policy for
  Resonator, extension, wall anchors/segments, repair/replacement, power, and
  production opportunity; require delivered/spent income, army/economy value,
  survival, and outcome evidence.
- Building a perfect global optimizer or scanning all cells every tick. Cache
  configured tree identities, react to bounded events/intervals, plan one local
  project at a time, and impose candidate/path/retry bounds.
- Reusing the stale `TibTest.oramap`, editing a packaged product map merely for a
  harness, or counting a custom package before `map.yaml`, `map.bin`, actors,
  lobby slots, and `--check-yaml` are validated.

## Competing systems and ownership

- **Building/Defence production queues and cash:** every owned Fact can expose
  parallel `Building.GDI/Nod` and `Defence.GDI/Nod` queues. BaseBuilder low-power,
  opening, first-tower, smart-economy refinery/vehicle-factory, air-repair,
  production, silo, generic wall, and fraction choices all consume the same
  structures/cash. Field reservations must be queue-scoped, deduplicated across
  Facts, stale-safe, and subordinate to critical recovery.
- **Power and build area:** `PowerManager`, `MapBuildRadius`, `BaseProvider`,
  `GivesBuildableArea`, `RequiresBuildableArea`, `BuildingInfluence`, map terrain,
  existing structures, and placement orders determine whether extension,
  perimeter, and Resonator cells remain legal between plan and order.
- **Existing wall work:** `BaseBuilderWallPlanner` owns Construction-Yard and
  tower wall intents and the same Defence queues/wall actors. Field work needs a
  distinct purpose/owner so an actor or queued wall cannot consume another
  planner's anchor. Exercise both planners in one game.
- **Smart/opening economy:** `BaseBuilderSmartEconomyManager`, opening structure
  reservations, `IBotRequestPauseUnitProduction`, PlayerResources, storage, live
  refinery delivery capacity, MCV expansion, and emergency low-power logic can
  preempt field spending. Use their observable states rather than duplicating
  thresholds.
- **Harvester traffic and claims:** `HarvesterBotModule`, `FindAndDeliverResources`,
  `ResourceClaimLayer`, refinery delivery offsets, the wheeled locomotor, and live
  resource evolution issue/retarget ordinary harvest orders independently of
  field construction. Field policy observes route outcomes but does not own
  ordinary harvesters.
- **Red-bomb missions:** `RedTiberiumBombBotModule` reserves stealth harvesters,
  red cells, and targets, then issues its own HarvestUnstable/Move/Deploy orders.
  It must be forced to act in an integrated red-gate contention test.
- **Technology:** IronReaper `TechnologyCounterBotModule` owns branch transitions
  and Upgrade-queue actors; other UnitBuilder modules own their authored upgrades.
  A temporarily unavailable Resonator/wall/Repair Facility is a pause/replan, not
  permission for this feature to issue technology orders.
- **Repair/recovery/selling:** `BuildingRepairBotModule` may repair attacked
  extension buildings or Resonators; walls self-heal. Enemy destruction/capture,
  CrateCollector emergency sale of power, owner changes, Fact loss, and production
  cancellation invalidate pieces and reservations.
- **Movement and production exits:** rally-point managers, opening garrisons,
  squad movement, refinery traffic, Repair Facility service, and infantry's
  Tiberium vulnerability share corridors affected by Resonator growth and walls.
  At least one full game must make ordinary infantry and vehicle movement coexist
  with the field even though CNC-42 guard ownership is excluded.
- **Targets and opponents:** enemy assault, artillery, Mammoths, aircraft, capture/
  demolition, and support powers can attack field infrastructure. The enclosure
  remains counterable; this task does not retarget defensive forces to protect it.

## Cross-worker dependencies

- **CNC-42 (pending field defense) is the direct semantic follow-up.** It will
  station guards near each field using a harvester's saved last-harvest point and
  must keep infantry out of Tiberium and guards out of refinery traffic. CNC-41
  must leave stable, observable field identity/entrance/access behavior and must
  not implement, reserve, or position guards. No CNC-42 branch/PR was present at
  specification time. If one appears before publication, inspect its commits (not
  its worker spec), record the exact branch/PR/head here, and reconcile only
  shared interfaces/config; re-run gate/traffic tests after integration.
- CNC-39/CNC-39A placement/reservation work is already represented in the common
  base and is not a prerequisite. Preserve current capture/placement reservation
  semantics. Do not read their worker specs.
- CNC-87 and CNC-40 are claimed in this round but the task packet reports no
  expected product-code overlap; CNC-40 adaptive-specialist crediting is unrelated.
  `mods/cnc/rules/ai.yaml` is nevertheless a shared high-conflict file: rebase and
  preserve their disjoint edits rather than taking an entire-file version.
- The task packet found no active CNC-41 PR. Do not wait on one or copy historical
  `tiberium-spreading` branches; current base behavior and this contract are the
  authority.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-3-cnc41/spec-policy/inputs/NARRATIVE.md`
- Sol-high policy review: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-3-cnc41/spec-policy/POLICY-REVIEW.md`
- Verdict and confidence: `mostly sensible / medium`
- Recommendations adopted as testable hypotheses: `Protect core refinery/storage/power/combat cash and queues with a survival-first admission/pause gate and prove eventual resume; rank serial projects by active demand, safety/route feasibility, and remaining commitment/payback rather than distance; use a two-cell entrance where feasible and repeated bidirectional ordinary plus reserved-harvester round trips; prefer concrete only when buildable/affordable/suitable, otherwise sandbags, and exclude cycl; pause early projects under acute pressure but prioritize a breach around an already active red Resonator; measure net usable-income payback, production delay, army/economy value, losses, power/storage faults, survival, and outcome in matched games.`
- Recommendations rejected or deferred, with reason: `Do not implement an exact global expected-return optimizer: it would add unjustified complexity and map-wide cost; use a deterministic bounded rule-of-thumb and test the review's ranking hypothesis. A Repair Facility is preferred but not mandatory because the literal allows an accessible building edge and the review agrees an existing safe edge is sufficient. A two-cell gate is a target, not an unconditional geometry law; a forced one-cell gate may pass only with repeated real contention evidence. Field-defense/minefield suggestions remain deferred to CNC-42.`

## Acceptance and tests

### Literal black-box acceptance

Run a fresh full-engine headless-MAX game with an ordinary enabled Economy AI
(normally Brutalis) and an ordinary hostile AI, all normal modules enabled, on a
validated focused CNC map derived from current packaged content. Give the Economy
AI legal technology and enough ordinary assets to make the behavior prompt, but
do not replace it with a passive/custom bot. Place at least three configured live
trees (including one `splitred`), a near and remote site, a normal refinery,
storage, harvesters, and map geometry that requires one real extension and lets a
hostile AI apply pressure.

Acceptance is the final world outcome, not requests:

1. Every still-live configured tree has exactly one still-live, complete, powered
   owned Resonator covering it; no duplicate or unassigned Resonator is present.
2. The remote project used only necessary legal Power Plant steps making roughly
   six-cell progress and reached the field without occupying resource/traffic.
3. The red tree and its Resonator are inside a real four-cell-standoff concrete-
   or-sandbag perimeter completed before Resonator activation. The deliberate
   building-edge/Repair-Facility entrance remains open (two cells where feasible).
4. At least two ordinary harvesters repeatedly travel from the refinery through
   the entrance, harvest the intended field, return, unload successfully, and
   repeat without chronic idle/search failure. In a separate forced run, a
   RedTiberiumBombBotModule-reserved stealth harvester also crosses the gate and
   progresses through its mission.
5. A non-gate segment removed after completion is observed on the next 1500-tick
   maintenance pass and visibly restored; a damaged present wall is not
   redundantly rebuilt; the repair does not close or jam the entrance.
6. When immediate pressure clears, a paused remaining project resumes. Core
   refinery/storage/power and combat production remain functional throughout.

Then prove the red-tree behavior on the fresh, unmodified packaged
`mods/cnc/maps/Red Dawn.oramap`, not only the focused map. Evidence must identify
title `Red Dawn`, its validated current map hash, ordinary bots/options, a live
`splitred` actor and location, the associated Resonator, containment cells/gate,
world ticks, repeated harvest/unload outcome, and final game outcome. Empire Earth
may supplement scale/transition proof but cannot substitute for an exercised red
tree because it has no initial `splitred` actors.

### Focused checks and instrumentation

- Before product changes, record `git status`, exact base/head, `utility.sh cnc
  --check-yaml` and `--map-hash` for Red Dawn, Empire Earth, and any focused map,
  plus a feature-disabled current-control smoke to confirm old generic behavior.
- Add small world-independent tests for configured tree classification, one-to-one
  assignment/commitment counting, deterministic project ranking, survival gate,
  queue reservation/expiry, phase transitions, branch loss/resume, maintenance
  deadline arithmetic (including overflow/boundaries), and save-state validation.
- Extend or add focused geometry tests for a perimeter containing the 1x1 tree and
  2x1 Resonator at four-cell standoff, deliberate two-cell gate, building-edge/
  Repair-Facility alignment, LineBuild segment decomposition, blocked corner,
  one-cell forced fallback, extension progress, resource/footprint exclusion, and
  bounded failure. Do not mistake pure geometry for live placement/path proof.
- Run `dotnet test OpenRA.Test/OpenRA.Test.csproj` and `./utility.sh cnc
  --check-yaml` as baseline gates, along with repository formatting/style checks
  relevant to changed files. Run CNC only; do not build/test/package other mods
  except unavoidable shared-engine compilation.
- Instrument task-local state transitions, not per-tick state. Each useful event
  must identify player, tick, tree ActorID/type/location, project phase, planned
  Resonator/effect distance, extension/perimeter/gate cells, actor type, queue
  actor/category, reservation owner/expiry, cash/power/buildability, and reason.
  Distinguish candidate request, rejection, queue reservation, competing consumer,
  production acceptance, completion, placement/order, state transition, retry/
  release, route check, live traversal, maintenance observation, and final
  coverage. A single “feature fired” line is insufficient.
- Warnings/errors at configuration, load, or lifecycle boundaries must name the
  invalid actor/list/state and defer/recover safely; do not catch and silently
  continue as success. Keep bounded transition/maintenance summaries useful for
  later diagnosis, but remove noisy temporary path-cell dumps and debug toggles
  before publication.
- For every game retain launcher `command.json`, manifest, `summary.json`,
  console/debug logs, replay, benchmark CSV, exact map copy/hash, seed, lobby
  commands, starts/factions, build/commit/config checksum, bot identities, and
  outcome under the analysis directory. Verify `Headless MAX automation enabled`,
  `MAX game speed enabled`, intended map start marker, bot markers, progressing
  ticks, benchmark/replay production, exit/natural marker, and final result.
- Hot-path expectation: no full-map or full-actor scan every tick, no unbounded
  geometry/path work, and no new per-tick logging/allocation. On 202x202 Empire
  Earth (140 initial tree actors), compare matched benchmark CSVs and investigate
  a sustained median MAX simulation-throughput/CPU regression greater than 5% or
  visible allocation/GC growth. Fix the cause or record a concrete task-specific
  explanation; do not dismiss it as feature cost.
- Put builds behind one `large-build` slot and games behind the recorded `game`
  capacity. Reserve two game slots for a paired batch. Isolate support, settings,
  logs, replay, save, benchmark prefix, port, display, and map package per run.

### Ordinary and differential games

The first behavioral test after cycle 1's first implementation change is Game 1,
a same-build feature-enabled versus feature-disabled matched pair in the full
engine. A focused map may accelerate the event, but both sides/runs use ordinary
Brutalis and hostile ordinary AI with opening, smart economy, Harvester,
RedTiberiumBomb, wall, repair, production, squad, and other normal modules active.
If a same-build config toggle is not feasible, use an isolated worktree at the
recorded base SHA as control. Focused tests may run before/alongside it but may not
delay it.

1. **Matched cheese smoke, one near ordinary tree.** Failure hypothesis: the
   manager never receives/places the intended Resonator or duplicates it across
   Facts. Perturbation: enable the new policy only in the changed run while map,
   seed, faction, starts, cash, tech, opponents, and actors remain identical.
   Failure signal: no completed in-range one-to-one Resonator, duplicate request,
   generic resource placement, or core-economy stall. Pass evidence: changed run
   has the final association and a completed harvest/unload round trip; control
   retains old behavior. Do not repeat this happy path after it passes.
2. **Connected remote mixed-tree ladder.** Failure hypothesis: extension planning
   changes radius/balance, occupies resources, loops, or lets field work preempt
   opening/smart economy. Perturbation: three trees at different distances,
   ordinary starting cash, one site requiring multiple legal extension steps,
   cliffs/structures near alternative cells, at least two Facts, and early enemy
   pressure. Failure signal: non-progressing/extra plants, duplicate projects,
   blocked traffic, missing refinery/storage/power/combat production, or no resume
   after pressure. Pass evidence: serial deterministic completion, roughly
   six-cell useful steps, preserved core recovery, and literal one-per-tree result.
3. **Focused red containment and traffic.** Failure hypothesis: a full ring,
   wrong wall type, premature Resonator, or geometry-only gate fails under live
   traffic. Perturbation: `splitred`, representative obstacles, two-way traffic
   from at least two normal harvesters, and a separately reserved stealth
   harvester. Failure signal: `cycl`, less than four-cell standoff, live Resonator
   before enclosure, entrance closure/jam, Harvest search failures, missing unload,
   or reserved mission unable to reach red. Pass evidence: real perimeter/gate and
   repeated inbound/outbound harvest/unload plus reserved mission progress.
4. **Maintenance/destruction phases.** Failure hypothesis: the 1500-tick rescan
   rebuilds damaged-present walls, forgets missing cells, duplicates orders, or
   closes the gate. Perturbation: remove a non-gate segment just after a pass,
   remove a gate-adjacent segment, capture/destroy extension power, Repair
   Facility/building edge, Resonator, and queue actor in separate runs; attack
   before walls, near completion, and after activation. Failure signal: early
   spam, no next-pass request/completion, stale reservation, unrelated rebuild,
   gate loss, or existential recovery starvation. Pass evidence: bounded observed
   recovery at the right priority with route and production preserved.
5. **Save/load boundaries.** Failure hypothesis: persisted/reconstructed intent
   duplicates or loses a project. Perturbation: save immediately before queued
   Resonator placement, mid-enclosure, and just before maintenance; on reload
   destroy/capture a reserved asset. Failure signal: duplicate actor, skipped
   missing segment, stale queue/cell, cadence burst, closed gate, or invalid-state
   exception. Pass evidence: coherent resume and final outcome. Each reload run is
   supplementary and must be followed by a fresh-start confirmation.
6. **Blocked/island topology.** Failure hypothesis: a planner assumes connected
   land and searches/retries forever. Perturbation: validated Archipelago-style
   full-engine setup with one reachable tree and one tree across water/blocked
   terrain, then a variant with a legal narrow connection. Failure signal:
   unbounded scans/logs/queue churn, suicidal plant chain, false acceptance, or
   reachable project starvation. Pass evidence: reachable field completes,
   impossible one enters bounded deferred state, connected variant completes with
   real round trips.
7. **Packaged Red Dawn.** Failure hypothesis: focused assumptions fail on a real
   map containing many initial red trees. Perturbation: current unmodified Red
   Dawn, ordinary Brutalis versus tank/Mammoth, artillery, and air-capable ordinary
   opponents across useful matched seeds. Failure signal: no identified splitred
   project, runaway all-at-once investment, wall/queue collisions, trapped
   harvesters, no payback, loss/parity against control, or performance regression.
   Pass evidence: clean containment/access and materially better net usable
   economy without persistent army/survival loss.
8. **Long scale/transition and natural conclusion.** Failure hypothesis: actor
   transformation, 140-tree scale, long duration, or normal win/loss cleanup
   corrupts state/performance. Perturbation: fresh Empire Earth matched runs with
   different seeds and at least one full match at headless MAX to natural game
   over. Require actual actor-type evidence for any red-tree claim. Failure signal:
   duplicate/stale assignments, ever-growing state/logs, >5% sustained benchmark
   regression, or worse strategic result. Pass evidence: bounded stable behavior,
   valid transitions, useful project progress/payback, and natural outcome.

After every materially judged game or pair, stage only authorized artifacts for a
fresh Commenter and, because this is AI policy, a fresh routine Policy Reviewer as
required by the role loop. Record their factual/policy conclusions and worker
disposition before the next code change.

### Old-behavior control and required improvement

- Preferred control: the same compiled build/map rules with only field management
  disabled for the control run. Fallback: exact base
  `419bee2531d4802bf922c3597b42c6eeb75ab250` in an isolated worktree. Record the
  toggle/rules checksum or control commit for every pair.
- Match CNC content checksum, map/package hash, seed, lobby options, factions,
  slots/starts, initial actors/resources/tech/cash, bot types, opponents, exit
  tick/natural end, and launcher version. Different AI personalities are useful
  opponents, not substitutes for the old-behavior control.
- Primary causal metrics: configured/live trees; live/queued/reserved/completed
  Resonators by tree; time to project phases/coverage; exact project spend and
  replacement spend; cumulative resource collected, successfully unloaded, and
  spent; time to incremental usable-income payback; completed round trips and
  collection-to-unload travel time; idle/trapped/search-failed harvesters;
  storage overflow/failed unloads; red instability events; field/harvester losses;
  and maintenance observation-to-restoration time.
- Strategic/regression metrics: power-outage ticks, refinery/storage downtime,
  queue idle/block time and competing requests, unit/army/economic value at fixed
  checkpoints, useful damage/kills/losses, production/tech timing, survival,
  objective/match outcome, world ticks per wall second/benchmark throughput, and
  allocations/GC signal.
- In a scenario that exercises the field long enough to repay it, changed AI must
  materially outperform control: by default, across at least three matched seeds,
  reach net usable-income payback in at least two, show at least a 15% median gain
  in delivered-and-spent income or surviving army+economy value at the declared
  late checkpoint, and not lose more of the matched series or show a persistent
  core-production/survival regression. Predeclare the checkpoint/payback horizon.
  If map outcome makes that exact threshold unsuitable, declare a stricter
  task-relevant alternative before viewing results and justify it in the report.
- Repeated parity, marginal gain, later army production without payback, more
  trapped harvesters, or a match loss is strong evidence of a policy/implementation
  error. Investigate admission, ranking, route, queue contention, and project cost;
  fix or give a concrete evidence-backed task-specific explanation. Resonator
  count, wall completion, or activation logs alone never satisfy improvement.

### Adversarial cases

After the latest relevant product fix and after normal literal acceptance first
passes, obtain at least three distinct clean full-engine ordinary-AI adversarial
scenarios. A fix to a scenario resets the clean-three count for materially
affected scenarios.

1. **Survival/queue contention:** scarce cash, two+ Facts, opening and smart-
   economy demand, low-power/recovery transition, production requests, ordinary
   Construction-Yard/tower wall work, and an early rush. Force every inventoried
   structure/cash consumer to act. Failure is duplicate reservation, starvation,
   unfinished critical recovery, no project resume, or materially worse army/
   survival. Pass is protected core production plus later one-to-one field
   progress and comparative payback.
2. **Red gate under live contention and breach:** a completed active red field,
   multiple normal harvesters moving both ways, a RedTiberiumBomb-reserved stealth
   harvester, ordinary infantry/vehicle corridor traffic, and enemy destruction
   of non-gate and gate-adjacent wall/power/anchor pieces across maintenance
   boundaries. Failure is premature Resonator, `cycl`, closed/jammed gate, missed
   1500-tick observation, duplicate repair, stale owner, trapped actors, or failed
   unload/mission. Pass is contained growth, repeated real traffic, correct
   missing-only restoration, and continued ordinary modules.
3. **Connected versus blocked/island topology:** matched ordinary connected and
   Archipelago-style blocked setups with hostile terrain, narrow turns, an
   unreachable tree, and a later legal-route variant. Failure is false route
   proof, infinite retry/log/queue churn, wasteful power chain, or one impossible
   target starving reachable fields. Pass is bounded deferral, stable throughput,
   and final completion only after a real route exists.
4. **Persistence/invalidation:** save/load at three phases, tree appearance by
   resource transform, Fact/queue destruction, ownership change, branch loss and
   regain, and Resonator/extension destruction. Failure is a duplicate/ghost
   project, invalid load, lost maintenance time, immediate retry burst, or stale
   placement. Pass is coherent reconstruction and a later fresh-start repeat.
5. **Many-tree endurance/counters:** Red Dawn/Empire Earth with tank/Mammoth,
   artillery, and air pressure, different seeds, long duration/natural game-over,
   and benchmark comparison. Failure is runaway infrastructure investment,
   invulnerability, no net payback, strategic loss/parity, nondeterminism, memory/
   log growth, or >5% sustained MAX regression. Pass is counterable infrastructure
   that materially improves usable economy and does not erase army/survival.

Record before each run its failure hypothesis, new perturbation, exact failure
signal, and player-visible pass result. Force the intended contention/state; an
unexercised actor/module/path cannot pass.

### Final regression

After the last relevant fix and three clean adversarial scenarios, rerun from a
fresh process/new game (never solely a save) the literal focused three-tree
acceptance with the strongest compatible stress: two+ Facts, ordinary opening and
smart economy, two-way multi-harvester traffic, a reserved red-bomb harvester,
early pressure, a remote extension, and one destroyed non-gate segment crossing a
1500-tick maintenance boundary. Require every final world outcome in the literal
acceptance and the predeclared matched-control improvement; a request/reservation/
path/log without final actors, enclosure, round trips, restoration, and outcome
fails.

Also run a clean fresh packaged `Red Dawn` regression on its then-current validated
hash with ordinary Brutalis and ordinary opponent(s), headless MAX, all relevant
normal modules, and no test-only passive bot. Prove intended title/map/options,
bot slots/factions/starts, splitred actor/location, ticks, actual associated
Resonator, four-cell containment/gate, repeated harvest/unload access, benchmark/
replay, and final result. Run one fastest-speed full match to natural conclusion.
If the packaged map or content hash changed intentionally, revalidate and record
the new hash and reason; otherwise a mismatch invalidates the comparison.

## Implementation rules

- Do not ask implementation or preference questions. Investigate code, history,
  controls, configs, tests, and evidence; choose the strongest safe option and
  record material assumptions. Stop only this task for a real authority,
  credential, missing-file, unsafe-path, or irreducible blocker.
- Keep responsibilities separate and dependencies explicit. Prefer short,
  cohesive classes and functions; split oversized responsibilities when that
  improves cohesion, testability, or hot-path clarity without unrelated churn.
  Preserve unrelated behavior and user changes.
- Put tunable policy in the owning rules/config/save/map layer and algorithmic
  invariants in code. Do not duplicate policy across AI personalities or hide a
  rules/config concern in test-only code.
- Treat balance as frozen unless `Balance authority` above expressly permits the
  specific surface. Never change cost, HP, damage, armor, speed, timing, power,
  prerequisites, probabilities, resource values, or comparable tuning to make a
  behavior test pass. Unauthorized balance changes invalidate the result because
  they can fake improvement. Record a needed balance change as deferred work.
- For an expressly authorized balance-only task, test its bounded local effect
  first: affected-unit survival, useful damage, exchange value, adaptive rating,
  and selection frequency as relevant. Treat whole-match outcome/composition as
  secondary regression evidence unless the task explicitly makes it primary.
- Add proportionate unit/interface/static tests. Add useful bounded debug logging
  and handled warnings/errors at the owning boundary: make failures actionable,
  never silently swallow exceptions or substitute success, avoid per-tick spam,
  and remove obsolete/noisy temporary instrumentation before publication.
- Keep deterministic simulation hot paths bounded. Avoid repeated full-map/unit
  scans, uncontrolled allocations, nondeterministic iteration/order, unbounded
  retry queues, or logging that materially reduces MAX throughput. Measure or
  explain performance-sensitive changes with current evidence.
- Inventory and test ordinary modules that compete for the same units, queues,
  cash, reservations, targets, repair, or retargeting.
- Record worthwhile out-of-scope fixes, refactors, and optimizations under
  `Deferred work` in the task report/handoff; never expand scope silently or make
  concurrent workers edit a shared deferred-work file.
- Keep raw logs/replays/saves/profiles outside Git or under ignored
  `AUTONOMOUS-CNC-LOGS/`. Record concise paths, seeds, and conclusions here or in
  the task report.
- Never push directly to `bleed`, merge a GitHub PR, or edit the task sheet or
  coordinator state. Update this state and task report on the recorded task branch
  or, during integrated repair, the recorded repair branch.

### Task-specific implementation and publication plan

1. Reconfirm the task worktree is on the exact branch/base, inventory user changes,
   record control/map hashes, and create the report plus ignored analysis layout.
2. Establish narrow modular ownership: configurable opt-in/tree/actor/geometry/
   cadence policy in BaseBuilder info and CNC `ai.yaml`; a cohesive field-project
   manager and small deterministic policy/geometry helpers; only narrow queue
   selection/finished-placement hooks. Avoid adding the state machine to the
   1168-line BaseBuilder, 679-line queue manager, or unrelated wall purposes.
3. Implement one-to-one identity, project admission/ranking, queue reservation and
   completion validation first. Keep a same-build disable control. Make the first
   post-change behavioral evidence the required matched ordinary-AI game pair.
4. Add extension, red perimeter/gate sequencing, missing-only 1500-tick upkeep,
   invalidation, and save/load in the smallest evidence-driven cycles. After each
   change, run focused checks and the next harder full-engine scenario; let results
   select the next correction.
5. Add only bounded transition diagnostics necessary to separate request,
   rejection, owner, competing consumer, state/order, route traversal, maintenance,
   and final world result. Remove temporary cell/path dumps before publication.
6. At cycles 5, 10, 15, and 20 that occur, launch fresh Terra-medium cycle reviews
   to `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-3-cnc41/cycle-review-XX/CYCLE-REVIEW.md`
   and record adoption/rejection before another product change.
7. Complete matched controls, routine Commenter/Policy Reviewer loops, three clean
   post-fix adversarial scenarios, fresh literal regression, packaged Red Dawn,
   natural full match, YAML/unit/style checks, MAX benchmark comparison, and
   diagnostic cleanup. Keep raw artifacts outside Git and concise evidence in
   state/report.
8. Update the task report with behavior, ownership, assumptions, cycle journal,
   seeds/paths, comparative payback/outcome, performance/determinism, reviews,
   risks, and deferred work. Commit/push only the task branch, open one PR against
   the recorded release branch, wait for all required checks, and perform at most
   one in-budget final-review response cycle. Never merge the PR.

## Evidence-driven loop

One cycle begins when a product-code/config change is made. A cycle may build,
run focused checks, and execute up to two materially useful games needed to judge
that change. Merely reading logs or correcting an invalid harness without a
product change does not begin another cycle; record it honestly.

Treat full-engine simulations with ordinary AI as cheap primary feedback. The
first behavioral test after the first implementation change must be a full-engine
ordinary-AI game, normally headless MAX, with every relevant normal module enabled
from test 1. A focused custom map, pre-spawned actors, short distance, or obvious
cheese setup may make the event immediate, but it must not replace the real engine
or ordinary AI with a passive/custom bot or isolated manager fixture. Run focused
unit/static checks as useful baseline gates before or alongside it; do not delay
game evidence while accumulating unit-only confidence. Keep available game slots
working while other agents code or analyze because simulation is cheaper than
missing human feedback.

When a required situation is rare, construct it deliberately in a full-engine
custom map while keeping ordinary AIs and every relevant normal module enabled.
For example, pre-place a damaged or healthy capturable building and enough
engineers to force the one-versus-two-engineer decision. Use the setup for direct
causal proof, then seek natural-match evidence when the event is reasonably
reachable. If natural occurrence depends on unfinished prerequisite behavior
(such as an APC/transport delivery task), record that dependency and required
future revalidation instead of wasting cycles waiting for an event the current
build seldom creates or treating its absence as failure of this task.

For every change to AI strategy, priorities, economy, production, targeting,
recovery, or tactics, compare against old behavior repeatedly throughout the loop.
Prefer a same-build feature-disabled control. If unavailable, run the recorded
base SHA or named known-good older AI commit from an isolated worktree. Record the
exact control commit/toggle, content/config checksum, map, factions, seed, starts,
options, initial state, opponents, and metrics. Keep these matched so the intended
behavior is the meaningful difference. Use both game slots for paired control and
changed-AI runs when practical; make the first behavioral test such a pair when
the feature toggle or recorded control build is ready.

The changed AI must materially outperform old behavior in scenarios that actually
exercise the change. Judge match outcome together with task-relevant measures such
as survival, objective completion, tech timing, income/spending, army/economic
value, useful damage/kills, losses, idle queues/units, recovery time, and CPU cost.
If it loses, ties, or gains only marginally, assume a likely implementation error,
bad strategic policy, or displaced regression until evidence rules those out.
Inspect code and logs, vary adversarial scenarios, and fix the cause; do not call
feature-activation logs a success. Because matches can vary, repeat materially
useful comparisons before blaming noise. A non-strategic change need not win more,
but it must not degrade the relevant old-AI behavior without an explicit accepted
tradeoff in the spec.

Treat all tests as attempts to break the implementation. Compilation, lint, and
static analysis are baseline gates; every unit, integration, save/load, replay, or
game test must exercise a regression risk, boundary, invalidation, contention,
failure/recovery path, or assumption under pressure. Before running it, record:

- Failure hypothesis: what plausible defect this test could expose.
- Perturbation: what is made harder or different from the last passing test.
- Failure signal: the exact log/state/player-visible outcome that proves breakage.
- Pass evidence: the final observable result needed to falsify the hypothesis.

The existing broad regression suite counts as an adversarial gate against breaking
unrelated behavior, but it does not replace targeted falsification of this task.

One initial full-engine cheese-in-front-of-the-mouse smoke setup may establish
that the harness and simplest behavior work. As soon as it passes, change at least one
meaningful dimension—timing, map geometry, resources, missing/destroyed assets,
unit count, pressure, competing orders, save/load boundary, or match duration—and
make every later test harder or materially different. Never spend cycles on
near-identical happy-path confirmations when a stronger falsification is possible.
These tests replace much human feedback: use surprising results to challenge the
spec's assumptions, inspect the repository/evidence, and choose the next change
without asking the user an implementation question.

For each cycle:

1. Reread this state, current diff, and previous evidence.
2. Implement or revise the smallest evidence-driven change.
3. Run focused unit/static checks and fix relevant errors or warnings without
   treating them as a substitute for the game.
4. From cycle 1, run the simplest not-yet-proven full-engine ordinary-AI
   adversarial scenario that can falsify the current implementation while proving
   the requested outcome if it survives.
5. Diagnose results against desired and forbidden behavior. Add bounded
   instrumentation when evidence cannot distinguish mission purpose, candidate
   rejection, reservation owner, competing consumer, movement/order, contention,
   state transition, and final outcome.
6. Remove or reduce obsolete/noisy diagnostics after they answer the question.
7. Update the cycle journal before making another code change.

## Interim code-review loop

After product-change cycles 5, 10, 15, and 20 that occur, and before the next
product change or publication, launch a fresh Terra 5.6 medium
`cycle-reviewer`. Give it a job declaring `cycle` mode and only this state path,
the recorded base SHA, current branch/head and cumulative scoped diff, relevant
evidence through that cycle, and a task-local output path such as
`/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-3-cnc41/cycle-review-05/CYCLE-REVIEW.md`.

The reviewer writes only its review artifact and returns at most one
`advisory_concern`. Read it, verify its evidence, and record whether it is adopted
or rejected and why. An adopted product change begins the next ordinary cycle;
the review grants no extra cycles. At cycle 20, either reject the concern with
evidence or hand off `First iteration - testing` if resolving it would require
cycle 21. A clear review does not replace adversarial games, Commenter/Policy
Review, CI, or the final Sol-high task-PR review and one-response gate.

## Match narrative and policy-feedback loop

After every materially judged full-engine match or paired control batch:

1. Increment `Full-engine game tests completed` for each game, including an
   invalid setup that still ran far enough to expose evidence; label invalid runs.
2. Copy (do not symlink) only the authorized current/control logs, manifests,
   summaries, and metrics into the role output directory's `inputs/` subtree. In
   that directory, write a strict JSON Commenter job containing only their absolute
   `artifacts` paths, optional `design_reference`, and the absolute `output` path
   ending in `NARRATIVE.md`. Launch a no-history fresh `commenter` role (Terra 5.6
   medium). Do not stage source code, this worker state, the task sheet,
   implementation notes, or inline job-file commentary.
3. Read its factual `NARRATIVE.md`. Verify cited artifacts/ticks and use it to
   understand exact control differences, causal win/loss sequence, and what the
   losing AI did well. Correct the input/evidence rather than editing the narrative
   into a preferred story.
4. For AI-policy work, copy that narrative (do not symlink) to the Policy Reviewer
   output directory as `inputs/NARRATIVE.md`. Also write
   `inputs/TASK-CONTEXT.md`: a short factual description containing task ID/title,
   expected change, why, change category, explicit in-scope/out-of-scope behavior,
   and the exact `Balance authority` above. Do not include source, implementation
   preferences, the full spec, or desired review conclusions. Write a strict JSON
   job there with exactly the absolute `design_reference`, staged `task_context`,
   staged `narrative`, and `output` paths; output must end in `POLICY-REVIEW.md`.
   Launch a no-history fresh
   `policy-reviewer` role (Terra 5.6 medium). Questions embedded in the narrative
   are the worker's questions to this playtester; the job contains no inline
   context.
5. Read the `POLICY-REVIEW.md` before choosing the next code change. Treat advice
   as hypotheses: record what inspired the next test/change and what was rejected
   with reasons. Never substitute the review for adversarial game evidence.

Detailed narratives/reviews stay under the ignored analysis directory. Preserve
their paths plus concise factual and policy conclusions in the cycle journal and
task report. A paired two-game batch may share one Commenter and Policy Reviewer.

If a policy problem persists after at least ten completed full-engine game tests,
the worker may ask exactly one Sol 5.6 xhigh `policy-escalation` instance. First
write a new narrative stating the game-test count, repeated failure pattern,
attempted policies, evidence for/against each, and focused questions. The escalated
reviewer still reads only the design document and narrative. Record use in the
assignment field. Never invoke it before test 10 or invoke it twice for one task.

Prefer the full engine and real bot types. On Linux use the explicit headless MAX
path when graphics/input are irrelevant. Prove the current run loaded the intended
map, bots, actors, options, activated headless MAX, advanced ticks, flushed logs,
replay/benchmark evidence where configured, and produced the final outcome. A
passive fixture or manager-only simulation is not sole proof.
Use focused setup maps to accelerate reproduction, but before acceptance run a
fully enabled scenario containing every relevant ordinary module. Headless MAX
never replaces required graphical, rendering, input, lobby, or platform checks.

Force every inventoried competing system to act in at least one integrated test.
For routing or transport, test both an ordinary connected map and an island or
blocked topology such as Archipelago. If the event does not occur, change the
seed, map, duration, starting actors/resources, bots, or focused setup; do not pass
an unexercised path. Judge every unexpected behavior explicitly as acceptable or
defective.

Use ordinary full matches for emergent AI behavior. Full-engine real-AI testing
starts in cycle 1 and remains the main feedback loop; increase difficulty as soon
as the first behavior works rather than postponing games until late acceptance.

After normal acceptance first passes, require at least three distinct clean
adversarial scenarios after the latest relevant fix. Every adversarial scenario
must use the full engine, ordinary game AIs, and relevant normal modules. A focused
map may force the edge case, but passive/custom bots or isolated simulations do
not count. Define its expected failure signal, force it to occur, and inspect
current logs/replays; a happy-path rerun is not adversarial evidence.

Include hostile geometry, timing/state transitions, unusual unit counts, missing
critical assets, destruction/capture, save/load where state persists, and shared
resource/order contention as relevant. If a fix follows an adversarial failure,
restart the requirement for three clean adversarial scenarios affected by that
fix, then rerun the original literal acceptance with all normal modules. Keep that
final regression literal, but add the strongest compatible stress dimension that
does not invalidate the acceptance scenario; it must also try to break the code.

Prefer a matched differential as the golden adversarial test when the behavior
can be toggled: keep faction, map, seed, starts, options, and initial state aligned
and enable the behavior for only one side. When the scenario materially exercises
the feature, require a decisive advantage over the old-behavior control;
investigate a loss, tie, or marginal gain rather than calling it proof, and
document unavoidable nondeterminism. Do not substitute unrelated different AI
personalities for the old-behavior control unless the spec explicitly needs that
secondary benchmark.

Run at least one real full match at the fastest applicable speed to a natural
conclusion. For AI/engine behavior use headless MAX; use graphical modes when the
feature concerns rendering, lobby, input, or platform behavior. Use long-distance
starts for progression/endurance and short-distance starts for rush/defense. Do
not waste concurrency on near-copy spawn swaps unless position bias matters.

Wrap shared resources with:

```text
python3 .agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py \
  --lock-dir /root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/locks --resource game --capacity 2 --slots 1 -- COMMAND...
```

Reserve two game slots when using a two-game `launch-ai-parallel.py` batch. Poll
background games within 60 seconds, normally cap them at 30 minutes, isolate every
support directory, settings, log, replay, save, benchmark prefix, map artifact,
port, and display, and judge each run separately. Use concurrent slots for
materially different scenarios. Return to serial tests if contention corrupts
timing or evidence. A required full match may exceed 30 minutes while it continues
making useful progress; stop it when evidence is sufficient or progress stalls.

For expensive setup, optionally save shortly before the critical event and reload
after a logic change. Record the save's commit, config, seed, and tick; reject an
incompatible or stale save. Never use reload as the sole acceptance, adversarial,
or final-regression evidence because it may retain stale initialization or AI
state. Confirm the result again from a fresh match.

After 20 unsuccessful code-change cycles, publish the safest useful result as
`First iteration - testing`. Do not pad cycle counts after evidence is sufficient.

When the phase is integrated testing, the isolated 20-cycle cap no longer blocks
the assigned release validation. Use at most three code-change cycles for the
current RC and at most twelve across four RCs, updating both integrated counters.
Test the exact recorded release head before changing code; put any change only on
the recorded repair branch and rerun the materially affected original acceptance,
adversarial, and combined scenarios.

## Completion and publication

Propose `Complete - testing` only after literal acceptance, all required clean
adversarial cases, final regression, task checks, report, PR, and required GitHub
checks pass. Otherwise propose `First iteration - testing` with exact failures and
risks. The reviewer and integrated release determine final status.

The task report must cover behavior, design choices, assumptions, cycle count,
tests, seeds/artifact paths, diagnostics removed or retained, performance and
determinism, old-control configuration and comparative results, PR/checks,
deferred work, and remaining risks.

Push the task branch and open one individual PR. Do not merge it. Wait for every
required GitHub check; diagnose and fix relevant failures within the isolated
cycle budget and rerun them. If required checks cannot become green, propose
`First iteration - testing` rather than completion.

When review returns a correction, perform at most one review-response code/test
cycle, applying the highest-impact safe finding you agree with or recording
evidence for rejection. This cycle counts within the 20 isolated cycles; never
silently exceed the budget.

## Cycle journal

| Cycle | Commit/change | Failure hypothesis and perturbation | Checks/games | Narrative/policy/cycle-code review | Failure/pass evidence | Decision/next harder test |
|---|---|---|---|---|---|---|
| 1 | Opt-in tree scan, one-to-one powered coverage, serial queue reservation, legal non-resource in-range placement, admission/cash gate, generic Resonator suppression, deterministic policy tests | Hypothesis: parallel Facts duplicate a tree or generic placement misses it. Perturbation: exact-base versus changed Empire Earth, seed 410141, Brutalis GDI spawn 4 versus Skynet Nod spawn 1, 20k cash, declared 30k horizon. | 4/4 focused tests; zero-warning build; CNC YAML and diff checks pass. Full-engine changed ended naturally at tick 20000; exact-base control ended at tick 25000, so both failed the declared 30000 horizon. Artifacts: `analysis/worker-3-cnc41/cycle-01-game-01/`. | Factual narrative: `analysis/worker-3-cnc41/cycle-01-commenter/NARRATIVE.md`; it retained the summary/debug tick discrepancy and found only planning, not completion. Policy review: `analysis/worker-3-cnc41/cycle-01-policy/POLICY-REVIEW.md`; no demonstrated authority violation, balance impact inconclusive, direct final construction/access/maintenance evidence required. Adopted. | Changed manager discovered 140 configured trees and planned splitblue#503 at 124,22 on tick 1451, but opening ownership blocked every reservation; no Resonator coverage or duplicate was exercised. Initial discovery emitted 140 bounded but unacceptably noisy lines. Changed ended 5000 summary ticks earlier than control without field spending; logs exceed summary ticks, so causality is unresolved and cannot pass. | Reduce discovery logging to bounded summaries; construct the required focused ordinary-tree setup with opening/technology made prompt; instrument admission rejection transitions; rerun a matched pair that reaches a final covered actor and harvest/unload outcome. |
| 2 | Collapsed initial tree discovery to one deterministic type/count summary; added explicit, rate-limited admission-rejection reasons and tests. | Hypothesis: the cycle-1 project was silently blocked by opening/core admission; perturbation: validated same-build Tiberium Garden derivative with 18 `split2` trees, two harvesters, preplaced core/opening/tech assets, ordinary Brutalis versus ordinary Skynet, and a control map differing only by `TiberiumFieldExcludedBotTypes: brutalis`. | 4/4 focused tests, zero-warning build, CNC/global and both focused-map YAML checks pass. Enabled/control seed 410241 both reached tick 12000; control passed at 428.048 valid ticks/s, enabled failed only missing reservation/placement/coverage markers. Artifacts: `analysis/worker-3-cnc41/cycle-02-game-02/`. Enabled map hash `c323531d3a3d97ee5c44792e71d398af041412ba`; control `32e939c6f75c889545573395039ad49d6683dc6a`. | Factual narrative: `analysis/worker-3-cnc41/cycle-02-commenter/NARRATIVE.md`; scan/plan only, enabled 1.002 s (3.6%) slower at equal ticks with insufficient timing resolution for a cost conclusion. Policy review: `analysis/worker-3-cnc41/cycle-02-policy/POLICY-REVIEW.md`; not accepted, no unauthorized balance evidence, requires actual reservation/placement/powered coverage. Adopted. | Enabled emitted one initial summary and planned `split2#38@77,58` with Resonator site `82,58` at tick 1, then no further field transition. Because no admission-deferred line appeared, the configured Resonator was absent from queue buildables before admission rather than blocked by cash/opening/storage/power. No spend, actor, duplicate, or coverage occurred. | Add a bounded unbuildable-technology transition; grant `upgrade.economy3` only in the ignored focused harness through a test-only Player prerequisite (not product balance); prove queue-to-powered coverage, then instrument actual harvest/unload outcomes. |
| 3 | Added a bounded queue-buildability deferred/resumed transition without bypassing normal prerequisites; the ignored focused harness alone now grants `upgrade.economy3`. | Hypothesis: the prior focused actor upgrades did not provide the prerequisite consumed by production; perturbation: same ordinary-AI matched setup, but both validated map packages provide the test-only Economy III prerequisite and the feature remains disabled only in control. Failure signal was no queue reservation; pass required reservation, production, placement, powered coverage, and completed project. | 4/4 focused tests, zero-warning build, global CNC YAML and both focused packages pass. Enabled/control seed 410341 both reached tick 12000; control passed, enabled failed only missing lifecycle markers. Pair throughput 443.721 valid control ticks/s. Artifacts: `analysis/worker-3-cnc41/cycle-03-game-03/run/`. Enabled package hash `3976f339b0b74f6f3a67c4ad74b26d6ae22e1345`; control `402734a16e32b6dc676d440dafbc576d0c2f6ec3`. | Factual narrative: `analysis/worker-3-cnc41/cycle-03-commenter/NARRATIVE.md`; identical stated lobby/seed/horizon but distinct map package identity, scan/plan only, no evidenced outcome. Policy review: `analysis/worker-3-cnc41/cycle-03-policy/POLICY-REVIEW.md`; insufficient evidence, explicitly treats this as failed feature validation and keeps unrelated SkyNet MCV cancellation out of scope. Adopted lifecycle/evidence recommendation; threshold tuning rejected absent evidence. | Enabled deterministically scanned 18 `split2` trees and planned `split2#38@77,58` at tick 1, but still produced no reservation, admission, placement, actor, or coverage transition. Control emitted no field-policy log. Economy III alone therefore does not satisfy the Resonator's complete production prerequisite chain/build-limit state; no balance or product prerequisite was changed. | Inspect the real prerequisite expression and focused ownership/build-limit state; keep product prerequisite-aware, repair only the ignored harness if incomplete, and prove the full non-red queue-to-powered lifecycle before red work. |
| 4 | Scoped unloading-refinery ownership to smart-economy profiles while preserving legacy `RefineryTypes` behavior elsewhere; powered coverage now requires normal owner power. The ignored pair adds two ordinary preplaced `nuk2` plants to prevent core power recovery from monopolizing the queue. | Hypothesis: low-power core recovery, not technology, preempted the discretionary project; perturbation: same ordinary bots and modules with positive starting margin, plus stricter coverage loss/recovery observation. Failure signal was missing lifecycle or false coverage during low power; pass required the full non-red lifecycle and matched control silence. | 4/4 focused tests, zero-warning build, global CNC YAML, both package checks, and diff check pass. Seed 410441 enabled/control both reached tick 12000 and passed; pair throughput 704.580 valid ticks/s. Artifacts: `analysis/worker-3-cnc41/cycle-04-game-04/run/`. Enabled hash `f6d50f2a0a2ea8c2b3b7fb4b0ede4c5c50408490`; control `04cca3671bf569cf6203474d6af36a28cbd75625`. | Factual narrative: `analysis/worker-3-cnc41/cycle-04-commenter/NARRATIVE.md`; one complete actor-92 lifecycle and two recoveries, no outcome/access evidence. Policy review: `analysis/worker-3-cnc41/cycle-04-policy/POLICY-REVIEW.md`; insufficient evidence, but recognizes genuine task-directed execution and requires causes for deferral/coverage gaps plus harvest/economy/red evidence. Adopted access/state evidence; rejected strategic claims. | Enabled reserved tree 38 at tick 1 with cash 20000/power 305, production accepted tick 51, placement ordered tick 915, and live Resonator 92 covered/completed the tree tick 951 at exact planned cell 82,58. One next project was planned serially. Coverage was released at ticks 4251 and 10301 during non-normal power and restored at 4851/10951, so the stricter powered criterion exercised rather than logging false continuity. Control emitted no field-policy log; no desync/fatal error. Actual harvester traversal/unload and economic outcome remain unobserved. | Add bounded real ordinary-harvester traversal/unload and project-block reason evidence; begin red-tree geometry only after access is falsifiable. Cycle-5 review is mandatory after its product/game loop. |
| 5 | Added read-only live ordinary-harvester cargo-increase/unload observation keyed to an assigned tree, explicit planned-project queue-wait summaries, and power-state cause on coverage loss. No harvester order or reservation is changed. | Hypothesis: a legal-looking Resonator blocks or fails to serve actual harvesting, and the next serialized project is silently stuck. Perturbation: extend the matched focused pair to 15000 declared ticks and require actual locomotor cargo gain near the assigned field followed by cargo decrease at an owned unloading refinery. | 4/4 focused tests, zero-warning build, global CNC YAML, both package checks, and diff check pass. Seed 410541 enabled exercised every required new marker but ended naturally with launcher maximum tick 10000 (debug evidence through 11651), below the declared 15000 horizon; control reached 15000 and passed. Batch therefore failed 1/2. Artifacts: `analysis/worker-3-cnc41/cycle-05-game-05/run/`. | Factual narrative: `analysis/worker-3-cnc41/cycle-05-commenter/NARRATIVE.md`; terminal debug/benchmark bound 11652-11814, actual harvester stories, tree-42 stall. Policy review: `analysis/worker-3-cnc41/cycle-05-policy/POLICY-REVIEW.md`; mixed, accepts first project/access but rejects cardinality/effectiveness and keeps SkyNet production out of scope. Cycle review: `analysis/worker-3-cnc41/cycle-review-05/CYCLE-REVIEW.md`; one advisory concern that configured red trees are permanently deferred. Adopted. | Enabled completed tree 38/resonator 97 at tick 951. Harvester 122 gained cargo near tree 38 at tick 2901 and unloaded at proc 49 by tick 4551; multiple additional ordinary harvesters repeated real deliveries. The next tree-42 plan emitted bounded `QueueOrAdmissionPreempted` summaries. Coverage loss at tick 6801 explicitly reported `power-state=Low` and restored at 6901. However harvest transition logging expanded to dozens of lines, and no result/economy comparison or red behavior exists. | Collapse access logging to one proof per tree. Implement a distinct serialized red lifecycle beginning with deterministic four-cell perimeter/gate planning and wall-before-Resonator sequencing; validate on a real `splitred` focused full-engine setup. |
| 6 | Added deterministic red perimeter geometry around the tree plus Resonator footprint, a two-cell gate toward the nearest stable configured owned building, and a distinct `PlanningEnclosure` phase with activation-blocked planning telemetry. | Hypothesis: a generic ring closes access or contains only the tree; perturbation: same focused ordinary AIs but selected actor 38 is a live `splitred`, requiring a four-cell perimeter and two-cell gate while forbidding red reservation. | 5/5 focused tests after correcting side selection to use distance to the full side segment; zero-warning build; global/focused YAML and diff checks pass. Seed 410641 control reached tick 12000; enabled exercised all red geometry markers but ended naturally after debug tick 9001 (launcher maximum 5000), failing the horizon. Artifacts: `analysis/worker-3-cnc41/cycle-06-game-06/run/`. Enabled map hash `cc65f4945760ff613c692c69835c839f90fbc9c8`; control `b2c0b65d18b7fab13b7cfe6c64eb98f2b032a753`. | Factual narrative: `analysis/worker-3-cnc41/cycle-06-commenter/NARRATIVE.md`; valid red selection/geometry but an unexecuted long stall and contradictory terminal evidence. Policy review: `analysis/worker-3-cnc41/cycle-06-policy/POLICY-REVIEW.md`; insufficient evidence, accepts containment-before-activation ordering but requires explicit bounded wall/gate/activation stages. Adopted. | At tick 1, actor 38 was identified specifically as `splitred@77,58`; the plan contained tree plus Resonator at standoff 4 with 42 wall cells and gate `87,58;87,59`, width 2, activation blocked. No red reservation occurred. Defect: `RefreshProjectState` treated `PlanningEnclosure` as an active queue phase at tick 51, expired its zero deadline, and reset it to ordinary `Planned`; premature activation became possible even though it did not occur in this run. The geometry proof therefore passes, but the lifecycle fails safely only by circumstance. | Keep `PlanningEnclosure` non-queue-active and implement explicit field-wall queue ownership; do not admit the Resonator until owned non-gate coverage validates complete. Collapse access proof to one round trip per tree. |
| 7 | Added explicit field-owned wall reservations on the defense queue, five deterministic LineBuild segments, real owned-wall completion checks, gate-empty validation, enclosure-before-Resonator transition, and one access proof per tree. | Hypothesis: red wall work collides with ordinary wall intent or activates after merely ordering rather than world completion; perturbation: positive build-radius support around the same red geometry and required full wall-to-coverage lifecycle. | 5/5 focused tests, zero-warning build, global/focused YAML and diff checks pass. Seed 410741 enabled/control both reached tick 12000; control passed, enabled failed missing second-anchor/segment/enclosure/coverage markers. Artifacts: `analysis/worker-3-cnc41/cycle-07-game-07/run/`. Enabled hash `79771e625775303e3c5590dc6760a29c5880cfe5`; control `7030fa363ac81e8ef681557f7155c33e169247b5`. | Factual narrative: `analysis/worker-3-cnc41/cycle-07-commenter/NARRATIVE.md`; clean matched horizon, no winner evidence, and segment 1 never completed. Policy review: `analysis/worker-3-cnc41/cycle-07-policy/POLICY-REVIEW.md`; unsound/medium because retained work made no progress. Adopted bounded stateful recovery and endpoint telemetry. Rejected immediate whole-geometry recomputation: engine evidence identifies intentional first-anchor occupancy plus erased anchor state, not invalid perimeter geometry. | Enabled selected `sbag`, reserved segment 1/5 at tick 1, production accepted tick 51, and ordered the first endpoint at `73,54` tick 115 while activation remained blocked. Engine LineBuild consumed that wall item after the first endpoint; the manager incorrectly stayed `Producing` awaiting a second callback from the same item, timed out at tick 751, erased anchor state, and retried the now-occupied first cell as illegal. No enclosure or Resonator occurred, so premature activation was prevented. | Model each LineBuild endpoint as its own serialized wall production item; after endpoint 1 release its reservation and return to `PlanningEnclosure` with anchor index retained, after endpoint 2 validate the complete world segment. Count remaining endpoint items for affordability and retain anchor progress across bounded retries. |
| 8 | Modeled each LineBuild endpoint as an independent serialized wall item, retained first-anchor state across reservations/retries, released endpoint reservations explicitly, and counted all remaining endpoint orders for affordability. Added pure endpoint-budget coverage. | Hypothesis: treating one wall item as two placements caused the cycle-7 retry loop. Perturbation: the same red geometry now requires two distinct accepted productions and placement orders per segment; failure is a repeated first anchor, pass is five world-complete segments before Resonator admission. | 6/6 focused tests, zero-warning build, global CNC YAML and diff checks pass. Seed 410841 enabled/control both ended naturally after summary tick 10000, below the 12000 horizon, so both are invalid horizon runs. Enabled exercised both endpoint reservations and orders but failed segment/enclosure/coverage markers. Artifacts: `analysis/worker-3-cnc41/cycle-08-game-08/run/`. | Factual narrative: `analysis/worker-3-cnc41/cycle-08-commenter/NARRATIVE.md`; both horizons invalid, endpoint/segment orders verified, world completion absent. Policy review: `analysis/worker-3-cnc41/cycle-08-policy/POLICY-REVIEW.md`; mixed/medium, adopts actual world coverage as truth, bounded missing-wall recovery, and later identical-map traffic evidence. Its SkyNet production suggestion is explicitly out of scope. | Endpoint semantics are repaired: segment 1 endpoint 1 reserved tick 1/ordered 118; endpoint 2 reserved tick 118/ordered 240; no illegal first-cell retry occurred. World completion still failed by tick 3251. Direct map-bin inspection showed resource type 1 occupying planned top-edge cells `75,54` through `80,54`; engine LineBuild stops at the first non-buildable resource cell before reaching the existing connector. No enclosure or Resonator occurred. | Keep the exact four-cell boundary and authoritative world-state checks. After the two anchors, fill only currently legal missing wall cells as harvesters clear them; resource-obstructed cells visibly wait without queue ownership. Reuse this missing-only path for the 1500-tick active-enclosure maintenance cadence, never filling the gate. |
| 9 | Added world-state-driven missing-cell recovery after LineBuild encounters resource: endpoint orders release immediately, legal gaps are filled opportunistically, resource-obstructed cells retain no queue owner, and bounded waiting names the obstruction count while the gate remains excluded. | Hypothesis: a fixed segment timeout cannot recover as living Tiberium clears/regrows. Perturbation: keep the exact resource-overlapping four-cell boundary and require legal gap orders plus decreasing obstruction without duplicate/illegal placements. | 6/6 focused tests and zero-warning build pass. Seed 410941 enabled reached configured tick 12000; control ended naturally at tick 10000 and is horizon-invalid. Both feature and control are materially judged games. Enabled exercised gap and obstruction markers but failed segment/enclosure/coverage completion. Artifacts: `analysis/worker-3-cnc41/cycle-09-game-09/run/`. | Factual narrative: `analysis/worker-3-cnc41/cycle-09-commenter/NARRATIVE.md`; enabled reached horizon with four wall admissions and obstruction-to-preemption stall, while control evidence is horizon-inconsistent. Policy review: `analysis/worker-3-cnc41/cycle-09-policy/POLICY-REVIEW.md`; mixed/high. Adopted falsifiable liveness, bounded production budget, competing-admission telemetry, and unchanged survival priority. | Enabled built both endpoints by tick 230, then legal gap cells `74,54`, `81,54`, and `86,54` by tick 654. It waited without queue ownership while resource obstruction remained six cells; ordinary harvesting reduced this to four at tick 5251, three at 6751, and zero by 8251. No site-illegal retry or premature Resonator occurred. Defect: affordability pessimistically reserved one paid item for every missing perimeter actor rather than remaining LineBuild/gap orders; after other spending tightened cash, no wall reservation followed the cleared obstruction and segment 1 remained incomplete. | Charge the remaining current-segment missing cells plus two anchors for each later segment, not every actor that LineBuild can spawn for free. Add explicit wall admission/buildability reasons. Re-run through completion; cycle-10 cumulative review is mandatory afterward. |
| 10 | Corrected remaining-wall commitment to current-segment missing cells plus two anchors per later segment, retained exact affordability protection, and added rate-limited wall admission reasons with remaining orders/cash/power/recovery. | Hypothesis: all-actor affordability falsely blocks liveness after resource clearance. Perturbation: same ordinary-AI red pair; require precise admission reason after obstruction reaches zero and completion if protected cash remains. | 6/6 focused tests pass; focused compilation clean. Seed 411041 enabled reached configured tick 12000; control ended naturally with summary tick 5000 and is invalid. Enabled again failed segment/enclosure/coverage markers. Artifacts: `analysis/worker-3-cnc41/cycle-10-game-10/run/`. | Narrative: `analysis/worker-3-cnc41/cycle-10-commenter/NARRATIVE.md`; valid enabled horizon and bounded red attempt, invalid/inconsistent control horizon, no outcome comparison. Policy: `analysis/worker-3-cnc41/cycle-10-policy/POLICY-REVIEW.md`; mixed/medium. Adopted route-invalid replan as a required later falsification; rejected lowering the protected reserve from this intentionally under-provisioned run. Cycle review: `analysis/worker-3-cnc41/cycle-review-10/CYCLE-REVIEW.md`; advisory concern adopted: completed red identities, 1500-tick missing-only maintenance, and save/load reconstruction are absent. | Accounting/telemetry worked: bounded commitment was 18 paid orders, obstruction fell from six to zero by tick 6801, and admission named `InsufficientCash` with spendable cash 171, 319, 80, 43, 0, and 1077 against protected 5000; one transient `MissingHarvesterRoute` was also visible. No queue ownership, illegal placement, or premature Resonator persisted. The focused 20k-pressure harness exhausted cash on ordinary AI play before the field cleared, so it cannot prove completion without violating the survival gate. | Keep the protected reserve unchanged. Run a materially provisioned completion harness after mandatory reviews; if it completes, move directly to retained active-enclosure maintenance and save/load/reconstruction rather than further happy-path tuning. |
| 11 | Revalidate a produced gap wall at placement and deterministically retarget only to another legal missing cell on the same planned segment; retain anchor, standoff, gate, and bounded retry behavior. | Hypothesis: a transient actor on one cleared gap cell causes repeated cancellation/backoff even though another exact boundary cell is legal. Perturbation: exact supported-100k seed/setup that previously forced three illegal reservations; require retarget and real segment/enclosure completion. | 7/7 focused tests, clean CNC YAML/diff check. Seed 411243 enabled/control full-engine pair both ended naturally early (summary ticks 5000/10000); enabled debug transitions reached tick 8251. Artifacts: `analysis/worker-3-cnc41/cycle-11-game-13-retarget/run/`. | Narrative: `analysis/worker-3-cnc41/cycle-11-commenter/NARRATIVE.md`; it confirms the unexercised retarget and stalled first segment while retaining terminal-tick/outcome uncertainty. Policy: `analysis/worker-3-cnc41/cycle-11-policy/POLICY-REVIEW.md`; insufficient evidence/high. Adopted bounded recovery and active-maintenance validation; urgent admission preemption remains. Unrelated VIKI MCV behavior rejected as out of scope. | The rerun did not exercise retarget: no reserved site became illegal. Resource obstruction reached zero by tick 5251 and exact gaps were ordered through `77,54`; later queue preemption persisted and segment 1 never completed before elimination. No premature Resonator, illegal placement, or desync occurred, but completion and matched horizon failed. | Retain the deterministic same-boundary unit behavior as a guarded recovery path, but do not claim behavioral proof. Add active-enclosure ownership/maintenance next, then save/load reconstruction; use a less lethal ordinary-AI harness for lifecycle evidence. |
| 12 | Retain completed red enclosure identity/gate/segments and exact Resonator identity; scan each active enclosure every configured 1500 ticks, queue only actually missing non-gate cells, ignore present/damaged walls, and release invalid tree/Resonator ownership. | Hypothesis: completed projects are discarded, so later breaches cannot be observed or repaired and a changed Resonator can inherit unsafe containment. Perturbation: less-lethal ordinary Easiest opponent, supported 100k cash, 20000-tick target on the resource-obstructed red map. | 8/8 focused tests and diff check pass. Seed 412341 enabled/control both ended naturally before target (summary ticks 10000/5000); enabled debug transitions reached tick 10601. Artifacts: `analysis/worker-3-cnc41/cycle-12-game-14-active-maintenance/run/`. | Narrative: `analysis/worker-3-cnc41/cycle-12-commenter/NARRATIVE.md`; invalid unequal horizons and illegal-site stall, no outcome. Policy: `analysis/worker-3-cnc41/cycle-12-policy/POLICY-REVIEW.md`; insufficient evidence/high. Adopted explicit 1500-tick no-progress defer/reconstruct after save ownership is stable; unconditional queue priority rejected. | The opponent still lacked symmetric starting assets and was eliminated early. Enabled remained on segment 1; one reserved cell became illegal with no legal same-boundary alternative, then later obstruction cleared but queue/no-legal-cell waits persisted. No enclosure became active, so maintenance was not exercised; no premature Resonator or desync occurred. | Keep focused active-state tests as implementation evidence only. Repair the ignored harness with symmetric hostile assets and/or a resource-clear red completion variant, then exercise active scan/breach. Persist exact ownership now, then add explicit no-progress deferral/reconstruction. |
| 13 | Persist and validate the exact current project, queue/phase/retry/defer state, planned wall/segment/gate cells, active red enclosure identities, next scan, and maintenance deadlines through BaseBuilder's existing trait save data. Invalid actor/type/perimeter state is named and discarded for safe deterministic reconstruction. | Hypothesis: reload forgets the gate/project or duplicates a reservation and immediately bursts maintenance/retries. Perturbation: fresh ordinary-AI save at tick 4000 during the resource-obstructed red enclosure, then a separate process reload through tick 7000 with the exact focused package available by hash. | 9/9 focused tests passed after save-state perimeter validation was added; focused compilation succeeded. Fresh seed 413341 saved at tick 4000 and reached configured tick 6000. The reload restored at tick 4002 and reached configured tick 7000. Both launcher summaries are assertion-failed only because their expected automation-message wording differs from the actual save/exit lines. Artifacts: `analysis/worker-3-cnc41/cycle-13-game-15-save-mid-enclosure/`. | Narrative: `analysis/worker-3-cnc41/cycle-13-commenter/NARRATIVE.md`; same project resumed and both reported failures are exact-string assertions. Policy: `analysis/worker-3-cnc41/cycle-13-policy/POLICY-REVIEW.md`; sound/medium-high. Adopted deterministic unresolved-cell re-evaluation with one project and continued activation blocking. | Fresh evidence identifies `splitred#38@77,58`, its exact 42-cell perimeter/gate `87,58;87,59`, and a mid-segment save. Reload emitted `load-restored project=38 active-enclosures=0 next-scan=4001`, resumed exact-boundary gap orders, and emitted no second plan, duplicate Resonator reservation, `load-invalid`, desync, or fatal signal. The preliminary tick-0 load attempt failed only because the isolated support lacked the focused map; the valid rerun staged the byte-identical package and is the counted load game. Active-enclosure persistence remains unexercised. | Treat mid-project persistence as passed but do not claim active-maintenance save/load. Add an explicit 1500-tick no-progress defer/re-evaluation that retains one exact project/gate and resumes only newly legal unresolved cells; do not weaken containment or survival admission. |
| 14 | Added a persisted 1500-tick no-progress deadline/count for enclosure work. When no configured buildable wall has a legal unresolved exact-boundary cell, the manager releases queue identity, retains the single perimeter/gate, defers to the authorized cadence, and re-evaluates after expiry. Admission failures retain their owning reason and are not mislabeled as geometry stalls. | Hypothesis: resource obstruction or another illegal exact cell causes per-queue-tick churn, stale ownership, duplicate replanning, or permanent activation-blocked inertia. Perturbation: fresh ordinary Brutalis versus Easiest on the same resource-overlapping red boundary through tick 8000. | 10/10 focused tests and focused compilation pass. Seed 414341 passed every full-engine assertion through configured tick 8000 at 443.872 valid ticks/s. Artifacts: `analysis/worker-3-cnc41/cycle-14-game-16-no-progress/`. | Narrative: `analysis/worker-3-cnc41/cycle-14-commenter/NARRATIVE.md`; exact gate/perimeter persisted through two deferrals, partial progress resumed, completion absent. Policy: `analysis/worker-3-cnc41/cycle-14-policy/POLICY-REVIEW.md`; revise/high, hypothesizes post-resource queue starvation. Priority escalation is rejected for now: the owning boundary at tick 7919 explicitly found `NoLegalEnclosureCell`, and field work already precedes discretionary production while remaining subordinate only to critical owners. Retain as an adversarial hypothesis with better legal-cell evidence. | One project deferred at tick 2428 until 3928 with six resource-obstructed cells, queue owner released, exact perimeter/gate retained, and activation blocked. It resumed planned segment-1 gap orders at ticks 4451, 4895, 5052, and 5994; the tick-4895 placement retarget stayed on the same boundary. With four unresolved but zero resource-obstructed cells later, it entered a second bounded defer at tick 7919 because no remaining cell was legally placeable, instead of tight-looping. No Resonator reservation, duplicate plan, desync, or fatal signal occurred; enclosure completion remains absent. | Do not override power/opening/refinery/repair recovery without evidence. Cycle 15 addresses the largest wholly absent literal behavior: necessary remote configured Power Plant extension with bounded candidate search, useful-progress proof, exact queue ownership, and save state. Mandatory cumulative review follows before cycle 16. |
| 15 | Added remote physical Resonator-site planning and a serialized `PlanningExtension` phase. A configured ordinary Power Plant is selected only on a compatible queue, placed at a legal resource-free cell chosen by deterministic roughly-six-cell useful-progress ranking across eight nearest base anchors, persisted with the project, and re-evaluated after the live plant expands real engine build area. Build-radius loss returns retained work to extension. | Hypothesis: remote trees are ignored or a generic/non-progressing plant chain violates real build-radius/resource rules. Perturbation: validated one-remote-`split2` focused package with no west support, ordinary Brutalis versus Easiest, 100k cash, two fresh tick-4000 runs; the second initially required two placed steps. | 11/11 focused tests pass. Compilation found one SA1513 blank-line warning after the queue-owner correction; it was fixed before evidence handoff. Focused package validates, hash `d108bac7aea00cf5010bb2e23837110268b84aba`, SHA-256 `9886a8502511badd9370f82ed830df84f0e83def8fde7c4b5b5c1774052e34b3`. Both seed-415341 games reached configured tick 4000 but failed stronger assertions: run 1 because a Defence queue falsely emitted extension no-progress after step 1; run 2 because no step-2 completion occurred. Artifacts: `analysis/worker-3-cnc41/cycle-15-game-17-extension/`. | Narrative: `analysis/worker-3-cnc41/cycle-15-commenter/NARRATIVE.md`; one real useful extension in both runs and no second step. Policy: `analysis/worker-3-cnc41/cycle-15-policy/POLICY-REVIEW.md`; concern/high. Adopted its evidence boundary: no fixed second-step or tick-4000 rule; another step is required only while actual build-area checks show it remains necessary, legal, and survival-safe. Cycle review: `analysis/worker-3-cnc41/cycle-review-15/CYCLE-REVIEW.md`; revise. Its persisted terminal-segment defect is verified and adopted for cycle 16. | Both games selected only tree 26/split2@49,58, planned Resonator@52,54, reserved/ordered `nuk2@79,63`, and observed the live plant at tick 351 with four useful cells and `next=extension`. Run 1 exposed and fixed wrong-queue deferral: Defence queues now cannot defer Power work. Run 2 had no false defer, illegal placement, desync, or crash, but the compatible Building queue was not offered after tick 269. The trace says extension remained necessary, but it does not prove a legal/safe second cell at a compatible queue opportunity. Separately, code review found that save/load rejects a valid red project after the final segment completes and before Resonator placement. | Cycle 16 is the single review-response cycle: permit the terminal segment cursor only for a non-maintenance activation-eligible project whose exact owned perimeter is complete and gate remains open; retain rejection for all incomplete/maintenance/other-phase states and add focused validation. Then exercise that save boundary if a full-engine completed enclosure can be forced safely. |
| 16 | Adopted the cycle-15 review finding. Persisted terminal segment cursors are accepted only for a non-maintenance `Planned` project whose exact owned wall coverage is complete and whose gate contains no owned configured wall; incomplete, maintenance, wrong-phase, negative, and beyond-terminal cursors remain invalid with an actionable diagnostic. | Hypothesis: save after enclosure completion but before Resonator placement discards valid intent. Perturbation: focused map starts with 41/42 exact walls, leaves one legal top-edge gap, and withholds Resonator buildability so an ordinary Brutalis can save a stable terminal project. Failure signal: `load-invalid` or replanning; pass requires terminal project restore. | 12/12 focused tests pass; zero-warning CNC module build, global CNC YAML, focused package YAML, and diff check pass. Two seed-416341 fresh games reached tick 2000 and saved at tick 1000, but both failed the required `red-enclosure-complete` marker. Artifacts: `analysis/worker-3-cnc41/cycle-16-game-18-terminal-save/`. Focused package final hash `14c231b815840a27fd3ee6e421bfe09cb0f4758a`, SHA-256 `c88366e1e042dc30cc8a2b8b12c6c58424e4bd5e5ba14ab0264abdc39f3b8896`. | Narrative: `analysis/worker-3-cnc41/cycle-16-commenter/NARRATIVE.md`; both clean tick-2000 save runs lacked any enclosure/completed-project transition. Policy: `analysis/worker-3-cnc41/cycle-16-policy/POLICY-REVIEW.md`; inconclusive/high. Adopted its requirement that the next evidence explicitly identify a live configured red tree, enclosure-before-activation progression, and save/load continuity; no balance-authority breach is evidenced. | The pure boundary test exercises valid in-progress, valid completed activation-eligible, wrong-phase, incomplete-enclosure, and out-of-range persisted cursors. Both full-engine attempts loaded the intended map and ordinary bots, advanced, saved, benchmarked, replayed, and exited cleanly with no desync/fatal signal, but emitted no field-manager transition at all. The first map variant lacked the test prerequisite provider; the second restored it and used a YAML-valid `~disabled` Resonator override, yet the prebuilt-wall harness still did not create a project. No reload was attempted because the saves contained no evidenced terminal field intent. | Retain the review fix with focused evidence only; do not claim runtime persistence proof. Cycle 17 should address the more fundamental inability to recognize already-complete leading enclosure segments, then rerun a fresh red-tree progression/save boundary with explicit identity and activation blocking. |
| 17 | `PlanningEnclosure` now deterministically advances over exact owned live-world segments that are already complete, clears stale queue/target state, and makes a non-maintenance project activation-eligible only when every wall cell is owned and the gate remains open. Added pure first-incomplete-segment coverage. | Hypothesis: deterministic reconstruction stalls forever on pre-existing complete leading segments. Perturbation: validated map with the exact full 42-cell red perimeter/open gate already present and Resonator disabled, ordinary Brutalis versus Easiest through tick 1000. | 13/13 focused tests and zero-warning module build pass. Seed 417341 run 1 used the launcher script's unintended repository-root binary and is invalid-binary evidence. Run 2 used the explicit task-worktree launcher, reached tick 1000, and failed because ranking selected remote `split2#39@62,51`, not the pre-enclosed `splitred`. Artifacts: `analysis/worker-3-cnc41/cycle-17-game-19-existing-enclosure/`. Map hash `8106efae7715f4effd7c8496b329c590f132107c`, SHA-256 `f0fcb5db9187b3e5476ddfcc1d76ff650504c563114d78868c7b2bd6b54a44d1`. | Narrative: `analysis/worker-3-cnc41/cycle-17-commenter/NARRATIVE.md`; only the worktree launcher exercised current code, which selected/extended split2 and never entered splitred recovery. Policy: `analysis/worker-3-cnc41/cycle-17-policy/POLICY-REVIEW.md`; inconclusive/high. Its serial-starvation hypothesis is adopted as a longer-test target, not yet as a priority change: the run ended at tick 1000 before the configured 1500-tick bounded defer could act. | The explicit-launcher run proved the current binary/config was active: it scanned 18 configured trees, selected one serial project, placed `nuk2@70,52` at tick 351, and emitted no desync/fatal/forbidden red activation. It did not exercise the new recovery branch because project ranking preferred a high-demand nearby ordinary tree. The temporary one-shot constructor diagnostic showed why repository-root runs lacked current transitions and was removed immediately after diagnosis. | Retain the bounded reconstruction helper with focused evidence only. Cycle 18 makes remaining commitment truthful by charging red candidates for only actually missing wall orders; a fully existing enclosure then has zero wall commitment and can be selected on its real demand/safety. Test beyond 1500 ticks before changing serial priority. |
| 18 | Project ranking now includes only the paid wall endpoint/gap orders actually missing from the live world; fully present segments have zero wall commitment. Red build-area re-evaluation likewise checks only missing wall cells, while the Resonator still requires real legal build area. | Hypothesis: omitted perimeter commitment misranks projects, and validating build radius for already-owned walls falsely sends reconstructed containment into extension. Perturbation: one splitred with an exact pre-existing 42-cell perimeter/open gate, disabled Resonator, fresh save at tick 500. | 14/14 focused tests, zero-warning build, focused/global YAML and diff checks pass. First seed-418341 run selected splitred but exposed false extension from already-present wall validation. After narrowing that check, the second fresh run passed every assertion through tick 1000 at 166.41 valid ticks/s. Artifacts: `analysis/worker-3-cnc41/cycle-18-game-20-red-recovery-save/`. Final map hash `30bfa84d27f7e18a50f81142940536b8eca51935`, SHA-256 `8c544092f8d2859deba38578f8ada559cd2af36835e6bc89eb5e1d7dc8b273fc`. | Narrative: `analysis/worker-3-cnc41/cycle-18-commenter/NARRATIVE.md`; run 1 falsely extended, while run 2 reconstructed the same enclosure directly and became activation-eligible. Policy: `analysis/worker-3-cnc41/cycle-18-policy/POLICY-REVIEW.md`; APPROVE/high. Adopted its confirmation that a safe already-buildable enclosure must not be blocked by extension planning. Its residual requirement for safe live coverage remains a later-test obligation, not evidence to enable the disabled test Resonator. | Fresh run 2 identified only `splitred#26@77,58`, planned exact 42-cell/five-segment containment with gate `87,58;87,59`, observed all segments complete at tick 51, emitted activation eligibility once, and saved at tick 500. No live/powered Resonator, coverage, project completion, gate closure, desync, or fatal signal occurred because the test-only Resonator remained disabled. Run 1 safely retained activation blocking while false extension was present. | Treat deterministic live-world enclosure reconstruction as passed from a fresh game. Cycle 19 should validate the saved spatial identity and use the terminal save to prove the cycle-16 persisted-cursor fix through a separate reload. |
| 19 | Saved projects and active enclosures now validate the live tree's exact cell, the complete Resonator footprint as in-map, and the planned effect range; active enclosure restore additionally requires the live Resonator actor at its saved cell. | Hypothesis: a stale/tampered spatial identity or valid terminal segment cursor is accepted or discarded incorrectly across reload. Perturbation: separate current-code process loaded the cycle-18 tick-500 terminal red project and continued with the Resonator deliberately unbuildable. | 15/15 focused tests, zero-warning module build, and diff check pass. The seed-418341 reload passed all assertions through tick 1500 at 187.312 valid ticks/s. Artifacts: `analysis/worker-3-cnc41/cycle-19-game-21-terminal-reload/`. | Narrative: `analysis/worker-3-cnc41/cycle-19-commenter/NARRATIVE.md`; exact restore at tick 501, scan at 502, retained Planned wait at 752, and clean exit at 1500. Policy: `analysis/worker-3-cnc41/cycle-19-policy/POLICY-REVIEW.md`; PASS/medium. Adopted its confirmation that persisted intent plus survival-first deferral is policy-consistent. Its residual later-resume/completion gap is retained as a functional failure, not treated as a policy contradiction. | Reload emitted `load-restored project=26 active-enclosures=0 next-scan=501`, rescanned only `splitred:1`, retained `phase=Planned`, and exited at tick 1500. It emitted no invalid-load, second enclosure plan, coverage, completion, desync, or fatal marker. This proves the valid terminal cursor and exact saved gate/project survive a separate-process load under stricter spatial validation; active-enclosure persistence remains unexercised. | Retain strict spatial validation. Cycle 20 validates that saved red wall/gate/segment cells are the exact configured perimeter around the persisted identity, then reruns the compatible terminal save before mandatory cumulative review and publication as `First iteration - testing`. |
| 20 | Persisted red projects and active enclosures now require their saved wall cells, ordered two-cell gate, and ordered segments to reproduce the exact configured four-cell perimeter around the saved tree and Resonator footprint; an arbitrary internally consistent ring is rejected. | Hypothesis: persisted state can substitute a coherent but wrong enclosure or strict geometry validation rejects a legitimate save. Perturbation: pure shifted/reordered geometry cases plus a separate current-code reload of the cycle-18 terminal save. | 16/16 focused tests, zero-warning module build, and diff check pass. Seed-418341 reload passed all assertions through tick 1500 at 187.293 valid ticks/s. Artifacts: `analysis/worker-3-cnc41/cycle-20-game-22-exact-perimeter-reload/`. Final strict CNC build passed with 0 warnings/errors; full tests 471/471, exhaustive CNC YAML, and diff check pass. | Narrative: `analysis/worker-3-cnc41/cycle-20-commenter/NARRATIVE.md`; exact restored wait and clean exit, with the manifest source copied to isolated `input.orasav` by the harness. Policy: `analysis/worker-3-cnc41/cycle-20-policy/POLICY-REVIEW.md`; PASS limited-evidence/medium. Its completion/traffic/maintenance limits are adopted; the path-name concern is resolved by the recorded isolated-copy launch convention. Mandatory cycle review: `analysis/worker-3-cnc41/cycle-review-20/CYCLE-REVIEW.md`; REQUEST_CHANGES. Adopted for handoff: real ordinary-harvester and reserved-stealth route proof is absent before red activation eligibility. | The valid save restored project 26/zero active enclosures at tick 501, retained `splitred#26@77,58` in `Planned`, and exited cleanly at tick 1500. No invalid-load, second enclosure plan, coverage, completion, desync, or fatal marker occurred. Focused tests reject shifted tree geometry, reversed gate order, and reversed segment order. Active-enclosure load remains runtime-unexercised. | The cycle-20 concern is correct and resolving it requires cycle 21. The budget is exhausted; publish the safest useful result as `First iteration - testing`, explicitly retaining missing route gating, literal acceptance, active maintenance/traffic, packaged-map, comparative payoff, and natural-full-match evidence. |

Post-cycle-10 no-product-change evidence: the first provisioned completion pair,
seed 411142, requested unsupported `startingcash 50000`; the engine fell back to
10,000. Enabled ended naturally with summary tick 5000 (debug activity through
tick 9001) and control with summary tick 10000, both below the declared 20000
horizon. Enabled safely attempted the red enclosure but remained cash/resource
blocked and never completed segment 1. Artifacts:
`analysis/worker-3-cnc41/cycle-10-game-11-completion/run/`. This is two materially
judged invalid-harness full-engine tests, not another product cycle or completion
evidence. Narrative: `analysis/worker-3-cnc41/cycle-10b-commenter/NARRATIVE.md`;
it retained the unequal early termination and enabled summary/debug tick conflict.
Policy: `analysis/worker-3-cnc41/cycle-10b-policy/POLICY-REVIEW.md`;
insufficient evidence/high. It supports preserving the 5000 reserve and a bounded
long-stall suspension. Resource-obstructed cells already release queue ownership;
an explicit slower deferred cadence is adopted for cycle 11 after current
completion is proven. The corrected harness must use a supported cash option and
a survivable matched horizon.

Post-cycle-10 corrected completion evidence: seed 411243 used supported
`startingcash 100000`, ordinary Brutalis versus VIKI, and the same enabled/control
focused packages. Enabled reached the configured tick 12000; control ended
naturally at summary tick 10000 and is invalid for the matched horizon. Resource
obstruction cleared by tick 7601 with ample cash, then exact planned gap cell
`78,54` became illegal at placement three times, triggered the bounded retry/
1500-tick backoff, and was finally ordered at tick 10651. Segment 1 still did not
complete by exit; enclosure and Resonator activation remained blocked. Artifacts:
`analysis/worker-3-cnc41/cycle-10-game-12-completion-corrected/run/`. This is two
materially judged games and a concrete cycle-11 liveness defect: revalidate and
select another exact planned missing cell when a reserved gap is transiently
occupied, with explicit retarget telemetry, rather than canceling the finished
wall item against the same cell repeatedly. Narrative:
`analysis/worker-3-cnc41/cycle-10c-commenter/NARRATIVE.md`; it verifies the
tick-12000 enabled lifecycle and three invalid reservations while retaining the
invalid control horizon and absent strategic result. Policy:
`analysis/worker-3-cnc41/cycle-10c-policy/POLICY-REVIEW.md`; mixed/high. Its
highest-priority exact-boundary geometry revalidation/replan with bounded defer is
adopted as cycle 11; balance changes and premature tree abandonment are rejected.

Baseline (pre-cycle): packaged Red Dawn feature-disabled control, seed 410041,
Brutalis GDI spawn 1 versus Skynet Nod spawn 2, headless MAX through tick 12000.
The run passed at 315.429 valid ticks/s with replay and benchmarks. The resource
modifier cache initialized with zero enabled actors, and no tree assignment,
Resonator lifecycle, containment, gate, or maintenance evidence appeared. Factual
narrative: `analysis/worker-3-cnc41/baseline-commenter/NARRATIVE.md`. Routine
policy review: `analysis/worker-3-cnc41/baseline-policy/POLICY-REVIEW.md`; verdict
was a clean control with insufficient evidence for CNC-41 conformance. Decision:
adopt its requirement that the first changed run explicitly log tree identity and
final Resonator coverage; do not infer policy success from a clean engine exit.

## Handoff receipt

- Proposed status: `First iteration - testing`
- Final branch/head: `agent/round-20260807-cnc41-economy-tiberium-fields` / product head `aa4e97972d8a0cb7f4780babcdffa4fa363c2299`; handoff metadata successor follows
- PR and checks: draft PR `https://github.com/Realpra1/LibertyDawn/pull/88`; Windows (.NET 6.0) passed in 3m39s; Linux (.NET 6.0) failed `make check` with IDE0005 at `BaseBuilderTiberiumFieldManager.cs:15` for unused `using OpenRA.Traits`. The one-line source correction would require forbidden cycle 21, so checks are explicitly not green.
- Cycles used: `20/20`; `42` materially judged full-engine games
- Acceptance evidence: non-red one-to-one powered placement and observed ordinary cargo/unload passed in cycle 4/5; exact red geometry, pre-existing enclosure reconstruction, and terminal project save/reload passed in cycles 18-20. Literal three-tree acceptance did not pass.
- Adversarial evidence: bounded resource obstruction, illegal-cell retarget, 1500-tick no-progress defer/resume, one necessary live Power Plant extension, mid-project reload, exact saved spatial/geometry validation, and terminal reload were exercised. Three clean post-fix adversarial scenarios were not completed.
- Old-behavior control and comparative result: feature-disabled Red Dawn baseline passed at 315.429 valid ticks/s; cycle-4 same-build enabled completed one non-red assignment while control stayed silent. No valid matched red payoff, winner, income, survival, or full-match improvement result exists.
- Match narratives and routine policy-review conclusions: factual and routine policy artifacts exist for every materially judged batch. Latest cycle-20 policy result is PASS limited-evidence/medium and retains completion, entrance-traffic, and maintenance gaps.
- Terra cycle code reviews and dispositions: cycles 5, 10, and 15 concerns were adopted in later cycles. Cycle 20 REQUEST_CHANGES is adopted for handoff: activation eligibility lacks actual ordinary and reserved-stealth harvester route proof. Final Sol-high task-PR review at `analysis/worker-3-cnc41/final-pr-review/FINAL-PR-REVIEW.md` is CLEAR only for `First iteration - testing`, not completion/release.
- Sol-xhigh policy escalation (unused, or test count/path/conclusion): unused
- Final regression: not passed; no literal stressed fresh regression, packaged Red Dawn changed-AI regression, or natural full match
- Error/warning and diagnostic-cleanup result: local incremental strict CNC build reported 0 warnings/errors; full tests 471/471, exhaustive CNC YAML, and diff check pass. GitHub Windows passed, but Linux clean `make check` found IDE0005 for unused `using OpenRA.Traits`; no out-of-budget correction was made. Initial 140-line scan and temporary constructor diagnostic were removed; bounded/rate-limited ownership transitions remain.
- Performance/determinism result: deterministic sorting and bounded scan/search/state; cycle-20 reload 187.293 valid ticks/s without desync. No matched <=5% sustained performance proof.
- Deferred work: implement pre-activation ordinary/refinery/gate round-trip and reserved-stealth route gates; then complete active breach/maintenance/save-load, remote extension, topology, three-tree, Red Dawn, matched payoff, and natural-match validation.
- Known failures/risks: red activation can become eligible from wall/open-gate presence without required real route proof; Linux CI is red for one unused using; fresh red construction never reached complete active coverage; active maintenance and active-enclosure reload are runtime-unproven; extension stopped after one step; serial infeasible-project starvation remains a hypothesis.
- Relevant artifact paths: `analysis/worker-3-cnc41/cycle-18-game-20-red-recovery-save/`, `cycle-19-game-21-terminal-reload/`, `cycle-20-game-22-exact-perimeter-reload/`, and `cycle-review-20/CYCLE-REVIEW.md`

## Final-review response receipt

- Scope: authorized single CI-only response for PR #88. Removed the unused
  `using OpenRA.Traits` reported as IDE0005 in
  `BaseBuilderTiberiumFieldManager.cs`; no behavior, balance, configuration, or
  route-gating code changed.
- Clean Linux reproduction: `make clean`, then locked `make check`, passed with
  0 warnings and 0 errors, including both explicit-interface validation steps.
- Proportionate verification: filtered `TiberiumFieldPolicyTest` passed 16/16 on
  `TargetPlatform=unix-generic`.
- GitHub receipt: pending push and replacement PR checks.
- The ordinary/stealth harvester gate-route blocker and every other deferred
  acceptance gap remain unchanged and explicitly out of this response scope.
