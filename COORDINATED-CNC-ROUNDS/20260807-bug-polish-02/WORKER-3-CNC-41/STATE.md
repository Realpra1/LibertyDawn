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
- Status: `Specified`
- Common base branch/SHA: `agent/cnc-20260806-bug-polish-01-release` / `419bee2531d4802bf922c3597b42c6eeb75ab250`
- Task branch: `agent/round-20260807-cnc41-economy-tiberium-fields`
- Intended PR base: `agent/cnc-20260806-bug-polish-01-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `0`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/workers/worker-3-cnc41/COORDINATED-CNC-ROUNDS/20260807-bug-polish-02/WORKER-3-CNC-41/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-3-cnc41`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `0`
- Terra cycle code reviews: `none yet; required after cycles 5/10/15/20 that occur`
- Sol-xhigh policy escalation: `unused (requires at least 10 game tests; one maximum)`
- PR: `none`

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

## Handoff receipt

- Proposed status:
- Final branch/head:
- PR and checks:
- Cycles used:
- Acceptance evidence:
- Adversarial evidence:
- Old-behavior control and comparative result:
- Match narratives and routine policy-review conclusions:
- Terra cycle code reviews and dispositions:
- Sol-xhigh policy escalation (unused, or test count/path/conclusion):
- Final regression:
- Error/warning and diagnostic-cleanup result:
- Performance/determinism result:
- Deferred work:
- Known failures/risks:
- Relevant artifact paths:
