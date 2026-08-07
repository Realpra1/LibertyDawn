# Worker State: CNC-42

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `worker-4-cnc42`
- Task: `CNC-42 — Economy field defense`
- Change category: `AI strategy, production/reservation, persisted stationing/routing, and economy-defense placement policy`
- Balance authority: `Frozen except for the exact requested AI-policy surface: approximately one existing Medium Tank (mtnk), two Minigunners/riflemen (e1), and one Mobile SAM (msam) of field-defense demand per harvester/working-field context in mid/late Economy play; commit the saved station only after completed unloading; keep infantry out of every Tiberium type and all guards out of refinery traffic; and use normal SAM Site (sam) construction to defend economy structures. This permits bounded AI requests, reservations, assignment, safe routing/stationing, pursuit/re-form/release rules, and economy-oriented SAM demand/placement needed to realize that behavior. It does not permit changes to unit/weapon/structure stats, cost, HP, damage, armor, speed, range, ammunition, build time, power, prerequisites, Tiberium values/spread/damage, probabilities, resource values, general army composition, unrelated unit/building weights or fractions, or any other balance surface.`
- Status: `Specified`
- Common base branch/SHA: `agent/cnc-20260806-bug-polish-01-release` / `419bee2531d4802bf922c3597b42c6eeb75ab250`
- Task branch: `agent/round-20260807-cnc42-economy-field-defense`
- Intended PR base: `agent/cnc-20260806-bug-polish-01-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `0`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/workers/worker-4-cnc42/COORDINATED-CNC-ROUNDS/20260807-bug-polish-02/WORKER-4-CNC-42/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-4-cnc42`
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

Mid/late-game Economy harvesters often work outside the ordinary base-defense
center. The current AI does not remember a completed harvest/unload cycle as a
field station and does not own a local mixed screen, so general squads and other
specialists may carry all useful Medium Tanks, Minigunners, and Mobile SAMs away.
Refineries, Silos, and Resonators also receive only generic enemy-facing SAM
placement. Remote economy therefore remains exposed to light ground harassment,
infantry, and aircraft even after the AI has the intended Economy tools.

The player-visible change is that an Economy-focused advanced AI which has reached
the relevant mid/late technology forms a stable mixed field screen near a
harvester's last actually harvested area after that harvester completes unloading.
The screen stays clear of Tiberium and the refinery lane, intercepts local ground
and air raids without being kited away indefinitely, reforms after combat, and
releases or reassigns invalid actors. Powered SAM Sites preferentially cover
active economy structures and their exposed air approaches. Compared with the
recorded old control, exercised harassment should cause fewer economy losses,
more completed deliveries, and better local exchanges without unacceptable
frontline starvation, traffic loss, or simulation cost.

## Authoritative behavior

Literal user requirement:

> **Economy field defense.** Mid/late game, station roughly one medium tank, two
> riflemen, and one MSAM per harvester near the field from its saved last-harvest
> point. Update the station only after unloading; keep infantry out of Tiberium
> and guards out of refinery traffic. Also use SAM sites to defend economy
> structures.

Observable contract and bounded interpretation:

- Implement this as an Economy identity behavior for Brutalis and for Iron Reaper
  while it has Economy II capability. Do not make VIKI or Skynet adopt this
  Economy strategy merely because a mixed-tech edge case exposes the actors.
  Keep the actor lists, prerequisite/activation policy, role counts, radii,
  intervals, and debug switch in the owning CNC AI rules rather than hardcoding
  CNC names and numbers throughout engine algorithms.
- For an isolated usable harvester/field, the target screen is approximately one
  `mtnk`, two `e1`, and one `msam`. Separated working fields must receive distinct
  useful coverage and defender demand must grow with exposed economy value. Close,
  mutually reachable committed harvest points may be treated as one field cluster
  and share a formation so units do not issue duplicate orders; however clustering
  must not silently erase the literal per-harvester intent. Any reduction from a
  strict multiplied count must be supported by matched game evidence that shared
  coverage protects all harvesters and materially avoids main-army starvation.
  An isolated harvester never loses its base 1/2/1 target merely because clustering
  is convenient.
- Record the last cell at which the harvester actually took resource. Treat that
  as pending context while it harvests, returns, queues, docks, or unloads. Commit
  a new station only after a real unload completed and the harvester is empty.
  Dock entry is too early. An undock notification after cancellation, destruction,
  capture, or failed/full-storage unloading is not sufficient. Until successful
  completion, the old committed station remains unchanged.
- Persist committed field context, assignment identity, and other state necessary
  to avoid station jumps or duplicate ownership across save/load. On load, resolve
  actor IDs safely and discard dead, captured, missing, disabled, or incompatible
  records. A fresh match must independently prove acceptance; a reloaded match is
  supplementary persisted-state evidence.
- Assign only owned, live, usable, mutually reachable defenders which are not
  reserved by a transport or another `IBotUnitReservations` owner and are not
  already protecting the base through the ordinary squad manager. Preserve
  stable assignments when still valid. Use deterministic tie-breaking. Release
  promptly when a harvester/station/cluster disappears, technology or bot
  capability disables the policy, a unit dies or changes owner, the route becomes
  invalid, or another explicit higher-priority owner legitimately holds it.
- Scarce assignments should first give uncovered, exposed fields a useful mixed
  nucleus, then fill missing roles. Current credible threat, field exposure,
  delivered economic value, and travel time may raise priority. Do not leave all
  other fields naked while making one safe field perfect; do not wait for an
  unavailable Mobile SAM while ground raiders kill a harvester when an available
  Tank/Minigunner partial screen can already help.
- Request missing role actors only through normal available production interfaces
  and within the total-unit/cash/queue protections. The existing `msam` per-type
  limits of four on relevant personalities conflict with a literal per-harvester
  target once the economy exceeds four harvesters; resolve that conflict only in
  the field-defense demand path or its expressly owned cap/config. Do not inflate
  ordinary production weights/fractions, bypass the total cap, duplicate queued or
  pending requests, monopolize blocked queues, or keep stale requests after demand
  falls. Existing actors should satisfy demand before new requests.
- Choose a small formation of reachable station cells near the committed harvest
  point. All infantry destination cells and every cell of their deliberately
  planned path must be free of green, blue, and red Tiberium. A safe endpoint with
  a resource-crossing route is a failure. Reuse or extract bounded hazard-aware
  path logic instead of issuing a direct move that lets ordinary pathfinding cut
  across Tiberium. Tanks and Mobile SAMs should also avoid field cells and harvest
  access where practical, but the literal zero-resource rule is mandatory for
  infantry.
- Exclude refinery footprints, dock/delivery cells, drag/exit cells, automatic
  harvester unblock destinations, queued-harvester waiting space, and the practical
  approach/exit corridor from stationary destinations and re-form routes. Guards
  must never directly follow a harvester into a refinery. Verify uninterrupted
  docking, unloading, exit, and empty-lane recovery rather than relying on a
  distance constant alone.
- Use a station-centered engagement leash. Medium Tanks and Minigunners may
  intercept credible local ground threats and briefly finish a retreating target
  only while prompt mutual support and return remain possible. The Mobile SAM
  stays behind the ground screen and responds to aircraft, not ground targets.
  Break pursuit and reform when a target leaves the defended area, support splits,
  a second threat attacks the field, a refinery corridor would be crossed, or the
  station becomes unsafe. Repeated movement orders when the formation is already
  useful are forbidden.
- Existing powered SAM coverage that reaches the relevant likely air approach
  satisfies static demand before another site is selected. Otherwise normal SAM
  construction may prefer exposed active Refineries first, an active Resonator
  second, and materially used Silos third. One site may cover several structures.
  The site must be legally buildable, close enough to a base provider, reliably
  powered, and placed without blocking field/harvester/refinery access. This does
  not authorize unlimited SAM production, a per-structure hard quota, or changing
  SAM/building fractions merely to make evidence look good.
- Preserve normal harvesting, refinery selection, unload throughput, Tiberium
  behavior, repair, building placement outside the expressly owned SAM preference,
  general attacks, emergency base defense, other specialist squads, and CNC-41's
  field entrance/access/repair behavior.

## Forbidden behavior and failure signals

- Updating a committed station on `MovingToResources`, at each selected resource
  cell, on harvest without subsequent completed unload, at dock entry, or on an
  aborted/cancelled undock.
- Using the harvester's current refinery position, linked refinery, requested
  harvest target, or private activity search anchor as if it were the last actual
  harvest cell.
- Direct `Guard`/follow orders on the harvester, or any formation that enters,
  waits in, crosses unnecessarily, or blocks the refinery footprint, dock, drag,
  queue, exit, or unblock lane.
- Any `e1` occupancy or deliberately planned transit through green, blue, or red
  Tiberium. Granting infantry immunity, changing Tiberium damage, or testing only
  the final destination is a scope escape, not a fix.
- A screen that exists only as reservations or production requests, piles all
  roles onto one cell, remains unreachable, arrives after the economy is dead,
  chases a fast raider away indefinitely, sends the ground-unarmed Mobile SAM in
  front, or never reforms after combat.
- Actor/order tug-of-war with `EconomyArtilleryBotModule`, `SquadManagerBotModule`,
  `TransportManagerBotModule`, `CrateCollectorBotModule`, early VIKI behavior, or
  another reservation owner. A later scanner stealing from an earlier owner is a
  failure regardless of which order is visible last.
- Multiplying production requests every scan, ignoring queued/pending actors or
  existing units, bypassing total/per-role capacity without explicit field demand,
  starving harvester/refinery/power recovery, or retaining requests after a field
  disappears.
- Treating every co-located harvester as a separate formation without testing the
  opportunity cost, or collapsing many separated/isolated harvesters to one token
  group in contradiction of the literal approximate per-harvester behavior.
- SAM proximity without useful air coverage, a new SAM despite sufficient powered
  coverage, SAM construction while it cannot remain powered, generic placement at
  the combat hotspot after claiming economy-defense success, blocked harvesting
  access, or unbounded SAM demand.
- Losing or duplicating committed stations/assignments on save/load; accepting a
  reload alone as final proof; using stale actor IDs after death/capture.
- Per-tick full-world scans, full-map path searches for every actor, unbounded
  allocations/candidate lists/retry queues, nondeterministic collection traversal,
  or debug spam that materially slows headless MAX.
- Changing actor/weapon/structure/resource stats or unrelated AI weights, taking
  CNC-41/CNC-45/CNC-46/CNC-60 behavior into scope, modifying non-CNC mods, or
  declaring success from compilation, unit tests, logs, requests, reservations,
  orders, or feature activation without the final visible defense outcome.

## Relevant current implementation and control behavior

- `OpenRA.Mods.Common/Activities/FindAndDeliverResources.cs` keeps
  `lastHarvestedCell` private to one activity. It updates the value after choosing
  a harvest cell and may clear it when a search fails; no player bot module can
  currently use it as a durable, post-unload field station.
- `HarvestResource` notifies traits on the harvester through
  `INotifyHarvesterAction.MovingToResources` and `Harvested`; at `Harvested`, the
  actor's current location is the actual cell where resource was accepted.
  `SpriteHarvesterDockSequence` and CNC's `VoxelHarvesterDockSequence` notify
  `Docked` and `Undocked`, but these callbacks currently have no actor argument
  and only traits attached to that harvester receive them.
- `HarvesterDockSequence` enters `Undock` when unloading completes **or** when the
  activity is cancelled, the refinery is gone/dead, or the Harvester trait is
  disabled. Consequently `Undocked` alone does not prove completed unload; the
  worker must distinguish an empty successful harvester from an aborted cycle.
- `Harvester` exposes `IsEmpty`, `IsFull`, `LinkedProc`, and `LastLinkedProc` but
  does not expose committed last-harvest context. `IGameSaveTraitData` is supported
  on arbitrary actor traits as well as player traits, so cohesive tracker state or
  bot-module state can be persisted without changing save format globally.
- `HarvesterBotModule` scans idle harvesters, respects every
  `IBotUnitReservations`, reorders failed/idle harvesters, avoids nearby enemies,
  and requests replacements. `RedTiberiumBombBotModule` can reserve a harvester
  and redirect it to unstable resource/enemy targets. Field defense must observe
  these owners without changing harvesting or bomb-mission behavior.
- `SquadManagerBotModule` currently claims idle `mtnk`, `e1`, and `msam` for
  general/rush/protection squads unless a transport or `IBotUnitReservations`
  owner has reserved them. It can pull a large protection group when an actor in
  `ProtectionTypes` (including `proc`, `silo`, `sam`, and `harv`) is attacked.
  Its `IsUnitProtectingBase` check is used by the Economy artillery module to avoid
  stealing active emergency defenders.
- `EconomyArtilleryBotModule` activates for advanced bots with
  `upgrade.economy2`, reserves all `mlrs` plus small value-based `msam`/`mtnk`/`e1`
  escorts, requests a first Mobile SAM, and periodically positions those defenders
  around the artillery battery. It implements `IBotUnitReservations` and save
  data. Its current 537-line module plus pure `EconomyArtilleryPolicy` is the most
  relevant ownership/validation/persistence pattern; field defense must not merge
  into it or take its battery actors.
- `StealthTankSquadBotModule` already demonstrates a bounded `PathSearch` whose
  cost function rejects configured resource hazards and issues spaced waypoints.
  Reuse/extract the general safe-route responsibility where cohesive; do not copy
  a second large near-identical path algorithm or reuse stealth-specific threat
  policy that does not belong to field infantry.
- `UnitBuilderBotModule` owns normal and external unit production requests,
  queued/pending counts, total adaptive unit capacity, cash/queue contention, and
  per-type `UnitLimits`. Relevant advanced configurations normally weight `mtnk`
  very low and set `msam` limits to four (or zero for non-Economy VIKI), while
  opening policies seek five harvesters and smart economy may grow much larger.
  External requests still pass capacity/buildability checks; a request log does
  not mean a unit was produced or assigned.
- `BaseBuilderQueueManager.ChooseBuildLocation` treats every `AttackBase` SAM as a
  generic defense. It searches an annulus around the last defense location toward
  the closest enemy/current defense center; it has no economy-structure target or
  refinery-lane exclusion. `ChooseBuildingToBuild` selects `sam` through authored
  fractions/limits and checks available power. Structure placement is therefore
  owned by BaseBuilder/queue placement, not by a mobile-unit module.
- CNC rules define `PROC` dock offset `(0,2)`, drag geometry, a Harvester unblock
  offset `(0,4)`, `SILO` as resource storage, and `RESONATOR` as a resource-growth
  structure. `SAM` costs 650 and consumes 20 power; `MSAM` costs 600, has two
  missiles and no ground attack; `MTNK` costs 800; `E1` costs 120 and is vulnerable
  to Tiberium. Those values are evidence for opportunity cost only and are frozen.
- At base SHA `419bee2531d4802bf922c3597b42c6eeb75ab250`, no CNC-42 branch/PR exists and
  no CNC-41 branch/PR was visible during specification. The old-behavior control
  has no field-defense owner and generic SAM placement. PR #74/commit
  `3b9efc3a4135a1a8cdc273bc392d2ccc0edca093` is relevant history for specialist
  reservation/persistence patterns; it is already contained in the common base.

## Likely wrong approaches and challenges

- Adding a direct Guard order is superficially simple but follows harvesters back
  to the refinery and creates precisely the traffic failure the task forbids.
- Reading `FindAndDeliverResources.lastHarvestedCell` or making it public couples
  policy to a replaceable activity instance and still does not prove unload. A
  cohesive event/tracker boundary should distinguish pending actual harvest from
  committed successful delivery without altering normal harvester decisions.
- Committing on `Docked` or every `Undocked` misses storage-full/cancel/death cases.
  Tests must force an aborted dock and verify the old station remains.
- Checking only `resourceLayer.GetResource(destination)` permits the pathfinder to
  route infantry through Tiberium. The route itself needs a bounded resource-free
  guarantee and a safe no-route fallback (`Stop`/withhold), never direct fallback.
- A global nearest-unit/nearest-cell assignment can churn every scan, swap units
  between close fields, and allocate two modules the same actor. Prefer stable
  ownership, deterministic IDs, bounded candidates, and recomputation on material
  events/intervals.
- A strict independent 1/2/1 per co-located harvester may reserve more army value
  than the field can use; an unconditional one-screen-per-cluster shortcut may
  violate the user's literal behavior. Exercise isolated, separated, and shared
  fields against control and use evidence to choose the smallest approximation
  that protects the economy without surrendering the map.
- The current `msam` caps and low `mtnk` weights mean merely reserving existing
  actors will often leave the target unformed. Conversely globally raising unit
  weights/limits can distort every army. Missing-role demand must be owned,
  deduplicated, cancellable, capacity-aware, and limited to relevant Economy bots.
- Separate infantry and vehicle production queues make requests concurrent but
  cash and total-unit slots shared. Requests must yield to missing refinery/power
  recovery and must not infer that an idle queue can afford every desired actor.
- Generic SAM build selection and generic SAM placement are separate facts.
  Changing only placement may never produce a site in the exercised window;
  changing only demand can still put it at an unrelated frontline. Keep demand,
  existing-coverage satisfaction, power, and BaseBuilder placement responsibility
  explicit without turning the already-large BaseBuilder into the mobile manager.
- `BaseBuilderBotModule.cs`, `BaseBuilderQueueManager.cs`, and the 537-line Economy
  artillery module are already large. Do not append all field tracking, assignment,
  hazard routing, combat leash, production, and SAM geometry to one class. Keep a
  cohesive player module, small pure deterministic policy/geometry helpers, and a
  narrow placement seam owned by BaseBuilder. Avoid unrelated refactors.
- Resource growth can invalidate a previously safe cell/path after commitment;
  CNC-41 may change field entrances and walls. Revalidate safety before movement,
  reform nearby without changing the committed source station, and avoid
  uncontrolled repeated pathfinding when no safe solution exists.
- Grouped orders, actor-ID save arrays, and world scans are deterministic only if
  collection traversal and tie breaks are explicit. Save/load must not preserve
  stale requests or double-reserve actors already restored by another module.
- A stationary defense may save harvesters yet lose the match by withholding the
  attack army on quiet maps. Forced harassment and quiet matched controls are both
  required; wins or activation logs alone cannot settle the policy.

## Competing systems and ownership

- **Harvester actors and field context:** `Harvester`, `FindAndDeliverResources`,
  `HarvestResource`, both dock-sequence implementations, `HarvesterBotModule`,
  `RedTiberiumBombBotModule`, resource claims, refineries, and smart-economy
  refinery congestion all touch harvest location, reservations, delivery, or
  traffic. Field defense observes completed context; it must not issue harvester
  orders, claim resources, select refineries, or alter unload state.
- **Mobile defenders:** `SquadManagerBotModule` owns ordinary general/rush/base
  protection; `EconomyArtilleryBotModule` owns Economy artillery escorts;
  `TransportManagerBotModule` can reserve all three role types for rescue/cargo
  and e1 for assaults; `CrateCollectorBotModule` can reserve e1 scouts;
  `EarlyInfantryRushBotModule` can reserve VIKI infantry; opening-garrison rally
  management and repair/resupply paths can also issue orders. The new field owner
  must implement `IBotUnitReservations`, snapshot all other unit and transport
  reservation providers excluding itself, preserve emergency-protection actors,
  and force every relevant contender in integrated games.
- **Production/cash/capacity:** `UnitBuilderBotModule` is the only normal
  `IBotRequestUnitProduction` owner and shares vehicle/infantry queues, cash,
  queued/pending items, total unit cap, adaptive cap, per-type limits, ordinary
  weights, harvester demand, MCV/upgrade demand, and external requests from
  Economy artillery, transports, opening garrison, smart economy, and technology
  counters. Field demand requests through this interface and owns/cancels only its
  own outstanding demand; it does not rewrite unrelated queues or report success
  until requested actors are live, reserved, moved, and useful.
- **SAM construction and placement:** `BaseBuilderBotModule`,
  `BaseBuilderQueueManager`, power management, `BuildingFractions`,
  `BuildingLimits`, defense queues, FirstTower/Wall planners, `BaseProvider`
  adjacency, and building repair compete for build choice, cash, power, cells,
  walls, and repair. Economy-coverage preference belongs at this placement/build
  boundary; mobile assignment should expose only the minimal factual demand or
  preferred anchors needed. SAMs must not override first-refinery/power recovery,
  opening goals, walls owned by CNC-41, or legal adjacency.
- **Targets and reactions:** normal AutoTarget/AttackMove, squad emergency
  protection, support powers, air and ground squads, repair behavior, and the new
  station leash can retarget the same defenders/threats. The field owner is
  responsible for reforming its reserved units after AutoTarget combat and for
  not repeatedly cancelling useful shots; it must release actors it cannot manage
  safely.
- **Persistent state:** actor traits and bot modules implementing
  `IGameSaveTraitData`, squad serialization, production request lists, and world
  actor IDs all restore related ownership. Field state needs its own version-safe
  keys and invalidation without depending on another module's private save shape.

## Cross-worker dependencies

- **CNC-41 is a live material dependency and the only expected code overlap.** It
  owns field identity, Tiberium-tree/Resonator construction, red-field containment,
  field entrances, harvest access, wall repair, and traffic behavior, and may edit
  `mods/cnc/rules/ai.yaml` plus BaseBuilder/placement helpers. At spec time no
  local branch, remote branch, or GitHub PR matching CNC-41 was visible, so there
  are no commits to inspect yet.
- Before the first implementation change, before changing any shared `ai.yaml` or
  BaseBuilder surface, after every CNC-41 update reported by the coordinator, and
  before publication, search local/remote branches and open PRs for `CNC-41`.
  Inspect that PR's commits and scoped diff only; do not read its worker state,
  packet, report, or private spec. Record exact branch/PR/head in this state/report.
- If CNC-41 supplies a stable field identifier, entrance/traffic exclusion API,
  or placement seam, consume it rather than creating a rival concept. If its
  branch is not yet mergeable, keep CNC-42's interface narrow and record the exact
  integration assumption. Resolve `ai.yaml` config by ownership rather than
  overwriting CNC-41 entries. Rebase/merge dependency commits only as directed by
  the coordinator/PR base; do not silently stack CNC-42 on an unreviewed private
  branch.
- Run a matched validation both without CNC-41 (the recorded common-base control)
  and on the first reviewed integration candidate that contains CNC-41, because
  new walls/resource growth can invalidate safe station cells, paths, SAM sites,
  and refinery throughput. An absent CNC-41 natural event is a dependency note,
  not permission to fake acceptance.
- CNC-45 (economy troop use), CNC-46 (defense clusters), and CNC-60 (last-patch
  conservation) are later non-prerequisite tasks. Preserve a clean reservation
  and field-context seam for them, but do not implement their offensive troop use,
  generic base clusters, or conservation policy.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-4-cnc42/proposed-policy/NARRATIVE.md` (copied verbatim to the review role's `spec-policy/inputs/NARRATIVE.md`)
- Sol-high policy review: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-4-cnc42/spec-policy/POLICY-REVIEW.md`
- Verdict and confidence: `mostly sensible; medium confidence (proposal-time review without match evidence)`
- Recommendations adopted as testable hypotheses: `Use a reachable field cluster rather than duplicate formations for nearby harvesters while retaining the literal isolated/separated-field target; allocate scarce useful nuclei breadth-first but raise credible active threats; limit the Economy identity behavior to Brutalis and Economy-capable Iron Reaper; use a station-centered pursuit leash and prompt re-form/release; keep the Mobile SAM behind the ground screen; treat existing powered approach coverage as satisfying SAM demand and prioritize exposed active Refineries, then active Resonators, then materially used Silos; test quiet-map opportunity cost, clustered versus separated fields, kiting, low power, topology, contention, save/load, traffic, and MAX cost.`
- Recommendations rejected or deferred, with reason: `A blanket one-1/2/1-screen-per-cluster cap is not accepted as authority to erase the user's approximate per-harvester requirement; close-field sharing must earn any count reduction in matched games and an isolated harvester retains the target. Undefined “emergency survival outranks field duty” is not a license for order stealing; only explicit reservation/invalidity/release rules may transfer actors. No new unit/building stats, global production weights, general attack policy, or unconditional SAM fraction increase is adopted; those are frozen or later-task surfaces. The review supplied no match outcome, so all strategic advice remains a hypothesis until full-engine differential evidence.`

## Acceptance and tests

### Literal black-box acceptance

Create a task-local full-engine CNC map under the ignored analysis directory with
ordinary real Brutalis AI, every normal bot module enabled, Economy II available,
a legal refinery/production base, two harvesters, and two separated reachable
Tiberium working areas. Give the bot enough ordinary or pre-placed role actors to
make the first event prompt, but do not replace the bot or disable its squad,
transport, harvester, production, BaseBuilder, repair, or AutoTarget modules.
Include live `PROC`, materially used `SILO`, and active `RESONATOR` anchors plus a
legal powered SAM opportunity. The opponent is an ordinary hostile AI; a scripted
map trigger may time a mixed light-ground/infantry/air raid, but may not command
the defender or substitute a passive attacker for sole acceptance.

From a player/spectator perspective, observe each harvester actually harvest at a
field, return, and complete unloading. Before unload completion, its field station
must not jump to the newly visited/pending cell. After completion, a useful mixed
screen approximating one `mtnk`, two `e1`, and one `msam` per isolated/separated
harvester must visibly form near that committed last-harvest area. No Minigunner
may enter any Tiberium cell in transit or at rest; no defender may occupy or delay
the refinery dock/approach/exit/unblock lane. During the raid the ground screen
engages locally, the Mobile SAM attacks aircraft from behind it, the group breaks
an attempted kite and reforms, and both harvesters survive to complete another
delivery. A powered SAM already covering, or newly placed to cover, the economy
approach must engage the air raid while the Refinery/Silo/Resonator remain usable.

Acceptance is the final visible outcome: both post-unload screens/coverage are
present and useful, both harvesters finish another delivery, the economy structures
survive and remain accessible/powered, the mixed raid is repelled or trades
decisively worse, and zero infantry-resource and defender-refinery-traffic
violations occurred. Logs prove setup and causality but cannot replace this result.

### Focused checks and instrumentation

- Add narrow pure-policy/unit tests for: role demand and bounded cluster sharing;
  isolated versus close versus unreachable harvesters; deterministic stable
  assignment; other-reservation rejection; pending actual-harvest versus committed
  post-unload transition; dock/undock cancellation and non-empty rejection; stale
  actor cleanup; request deduplication/cancellation; engagement leash/re-form;
  resource-free station/path selection and no-safe-route withholding; refinery
  traffic exclusion; existing powered SAM coverage, low-power rejection, target
  priority, buildable placement fallback; save-data round trips and invalid IDs.
- Run targeted `dotnet test OpenRA.Test/OpenRA.Test.csproj` filters for new policy
  tests, then `make test` (CNC MiniYAML validation) and `make check` under the
  large-build resource lock. Do not build/test/package RA, D2K, or TS. Existing
  broad tests are regression gates, not behavioral acceptance.
- Validate configured prerequisites, actor types, positive counts/intervals,
  bounded radii/candidates, resource type names, and structure/role relationships
  during rules load. Invalid config must fail with actionable YAML errors rather
  than silently disabling one role.
- With task debug enabled only in evidence builds, write state-transition/rate-
  limited diagnostics that distinguish: harvester ID and pending harvested cell;
  unload start, completion/abort reason, old/new committed station; field/cluster
  ID and members; role demand/live/queued/requested/assigned counts; candidate ID
  and rejection reason (`other-reservation`, `transport`, `base-protection`,
  `wrong-owner/dead`, `unreachable/domain`, `resource destination/path`, `traffic`,
  `occupied`, `unsafe`); reservation owner category; request accepted/waiting/
  unavailable/cancelled; chosen cells/path waypoint count; movement/order and
  observed arrival; threat/leash/re-form/release transition; SAM anchor, existing
  coverage, power/build queue/placement rejection; and final observed composition,
  distances, traffic/resource occupancy, raid result, and deliveries.
- Never catch and suppress path/config/save exceptions as success. Handled missing
  actors, invalid stations, unavailable queues, and no-safe-route outcomes get one
  bounded warning/transition and safe release/withhold behavior. No per-tick or
  unchanged-composition spam. Remove temporary actor/cell dumps and disable debug
  flags before publication; retain only low-volume diagnostics that are useful in
  future games.
- Every game evidence manifest must prove exact map path/checksum, branch/head and
  base/control SHA, rules/content checksum, seed, starts, factions, bot names,
  teams, lobby options including `gamespeed max`, starting cash/tech overrides,
  expected initial actors/structures/resources, normal modules, headless and MAX
  markers, first/last relevant ticks, exit/natural outcome, logs/replay/benchmark,
  and the final economy/raid result. Invalid setup still counts as a game test if
  it advanced far enough to expose evidence, but label and fix it.
- Treat assignment and safe-path work as a hot path. Cache one bounded owner/world
  snapshot per scan, cap station/SAM candidate cells and path attempts, recompute
  only on interval/material invalidation, sort deterministic ties, and avoid LINQ
  churn/full-world scans inside per-unit inner loops. Compare wall-clock time,
  MAX ticks/second, GC/allocation evidence if available, and benchmark output on a
  many-harvester Archipelago pair. The changed median must stay within 10% of the
  matched control absent a documented machine-noise explanation, with no growing
  retry queue or scan-time spikes as harvesters increase.

### Ordinary and differential games

Use `launch-ai-parallel.py` with isolated support/settings/log/replay/save/
benchmark/map paths and the global game lock. For each game or paired batch, write
the failure hypothesis, perturbation, exact failure signal, and player-visible
pass evidence **before** launch. After every materially judged batch, stage only
authorized evidence for Commenter and routine Policy Reviewer roles as required
by this state.

1. **G0 current-control characterization (no product cycle):** At base SHA, run
   the smallest ordinary-AI focused scenario long enough for one harvester to
   harvest/unload and for a raid to reach it. Hypothesis: generic squads/SAM
   placement provide no stable post-unload field screen. Perturbation: one exposed
   field and timed ground+air threat. Failure signal for the hypothesis: control
   independently forms and retains equivalent safe coverage. Evidence: exact old
   positions/orders, harvester outcome, SAM location/coverage, income/losses.
2. **G1 first post-change behavioral test — mandatory paired smoke:** Use both
   slots for changed branch versus detached base SHA on the identical focused map,
   seed, Brutalis/opponent, starts, content, actors, cash, options, and timing.
   Pre-place one harvester plus exactly 1/2/1 available defenders to accelerate
   the path while all normal modules remain enabled. Move the harvester to a
   second resource cell before unload. Hypothesis: the new owner commits too early
   or loses actors to ordinary squads. Failure: station changes before completed
   empty unload, composition never arrives, resource/traffic violation, or no
   changed/control difference. Pass: only changed commits after unload and visibly
   forms the safe useful screen. This is the one permitted cheese smoke; do not
   repeat it unchanged.
3. **G2 separated-field mixed-raid differential:** Two separated harvesters/fields,
   normal production rather than a complete pre-placed screen, one light-vehicle+
   infantry raid followed by aircraft. Hypothesis: requests/caps/assignment leave
   one field uncovered or opportunity cost exceeds protection. Perturbation:
   scarce starting defenders, simultaneous queue demand, ordinary specialists.
   Failure: control parity, a lost economy with live idle defenders, role starvation,
   or frontline collapse. Pass: changed coverage forms at both isolated fields,
   preserves materially more deliveries/assets, and trades locally better.
4. **G3 clustered-versus-separated/quiet policy pair:** In one run put three or
   more harvesters on one reachable field; in another put the same harvesters on
   distinct fields, with no early economy raid in the quiet variant. Hypothesis:
   clustering either multiplies waste or erases literal coverage, and reservations
   surrender map pressure when no threat exists. Failure: redundant formations,
   naked separated fields, repeated loss of attack timing/army value, or no
   release/reallocation. Pass: shared close-field formation remains sufficient,
   separated demand grows, and quiet-map attack/economy remains comparable to
   control.
5. **G4 toxic geometry plus refinery contention:** Dense green/blue/red resource
   around likely stations, at least five harvesters and one narrow refinery lane,
   a storage-full/aborted unload followed by successful unload, resource growth or
   CNC-41 field-wall entrances, and a kiting raider. Hypothesis: endpoint-only
   checks cross Tiberium, aborted undock commits, or reform blocks throughput.
   Failure: any e1 resource-cell tick, committed station on aborted unload, longer
   persistent queue caused by guards, blocked exit/unblock, or screen kited away.
   Pass: zero violations, old station persists until success, normal throughput
   continues, threat is handled and group reforms.
6. **G5 static SAM coverage/power differential:** Exposed active `proc`, then
   `resonator`, then used `silo`; an existing powered SAM covering two anchors;
   a low-power variant; a narrow legal build area; air and then ground counter.
   Hypothesis: duplicate/useless SAM demand or placement blocks economy and ground
   attackers exploit it. Failure: new site despite sufficient coverage, low-power
   spam, generic unrelated placement, blocked lane/access, or claim of ground
   protection. Pass: existing coverage satisfies demand, an uncovered powered
   anchor receives legal useful coverage when possible, aircraft pay/abort, and
   ground counter remains valid.
7. **G6 invalidation/contention/save-load:** Destroy and capture harvesters; kill
   one defender; reserve candidates through artillery, transport, crate, and base
   protection; block a station; save once before unload and once after a committed
   station, then reload each. Hypothesis: stale IDs, duplicated ownership, early
   commit, or permanent missing-role requests. Failure: order tug, reservation
   theft, stale group/request, load mismatch, or reload-only pass. Pass: correct
   owners win, state commits only in the right save, invalid actors release, and a
   fresh run reproduces behavior.
8. **G7 connected and island/blocked topology:** Run an ordinary connected map and
   CNC Archipelago with harvesters, fields, defenders, and economy anchors on
   reachable and unreachable domains. Hypothesis: global nearest assignment causes
   unreachable order/path retries or cross-island screens. Failure: repeated
   no-path orders/logs, stranded ownership, spike/growth in scan cost, or ignored
   local candidates. Pass: only reachable groups form, impossible assignments are
   withheld/released once, local economy remains defended, and cost is bounded.
9. **G8 long natural ordinary match:** At least one real headless MAX match on a
   stock connected multiplayer map and one materially useful Archipelago match,
   with ordinary Brutalis or Economy-capable Iron Reaper and hostile advanced AI,
   to natural game over. Hypothesis: focused-map success hides production,
   progression, repair, offensive-pressure, or duration regressions. Failure:
   feature never naturally exercises, repeated parity/loss from self-garrison,
   traffic/Tiberium violations, stalled progression, exception/desync, or >10%
   credible slowdown. Pass: natural post-unload field coverage and SAM use occur,
   economy survives exercised raids better, normal army still acts, and match
   ends cleanly.

Immediately increase difficulty after G1. Do not spend cycles rerunning an
unchanged happy path. If CNC-41 is not yet available, run G4 first against current
geometry and reserve the exact CNC-41 integration variant for dependency
revalidation rather than inventing its event.

### Old-behavior control and required improvement

- Preferred control is the recorded pre-change base
  `419bee2531d4802bf922c3597b42c6eeb75ab250` in an isolated detached control
  worktree/build. If implementation adds a safe same-build feature-disabled
  control that cannot perturb unrelated policy, prefer it after proving parity
  with the base control. Do not compare different personalities as the primary
  control.
- For every pair keep map checksum, CNC rules/content, factions, bot types, teams,
  seed, starts, lobby options, cash/tech, initial actors/resources, opponent,
  trigger timing, tick limit, and analysis windows matched. Record unavoidable
  nondeterminism and repeat with at least three materially useful seeds before
  judging noise.
- Primary outcome measures: live/lost harvesters and active economy structures;
  completed unloads and full-load deliveries; delivered income; time from unload
  completion to useful screen; assigned/arrived role counts per isolated and
  clustered field; threat interceptions, useful damage/kills and defender losses;
  raid exchange value; station/leash/re-form time; SAM coverage/aircraft losses;
  infantry-in-resource ticks (must be zero); defender refinery-lane occupancy and
  unload wait/throughput regression (must be zero attributable violation).
- Opportunity/performance measures: main-army and economy value over time, attack
  formation/departure timing, idle queues/units, outstanding requests, field-
  reserved value, match outcome, MAX ticks/second, wall time, allocation/scan
  evidence, exceptions/warnings/desyncs.
- In feature-exercising raid comparisons, changed behavior must materially beat
  control in at least two of three matched seeds and must not lose the third
  through its own forbidden behavior. A decisive improvement is preventing at
  least one harvester/economy-structure loss suffered by control, completing at
  least one additional full-load delivery while economy losses do not worsen, or
  a clearly superior local value exchange plus uninterrupted income. Repeated
  parity, marginal activation-only gain, worse economy, or a comparable frontline
  collapse is strong evidence of a bad implementation/policy and requires
  diagnosis/correction or a concrete task-specific explanation.
- Quiet controls need not win more, but must show that shared clustering/release
  keeps attack pressure and army/economy value broadly comparable. A repeated
  changed loss while control attacks successfully, with no exercised economy
  threat/value saved, fails the policy even if every requested guard exists.
- A valid concentrated armor/artillery counter may defeat the screen, but changed
  behavior should still avoid forbidden movement/traffic, trade coherently, and
  outperform undefended control on task-relevant survival/exchange. Do not tune
  stats or spawn overwhelming defenders to force a win.

### Adversarial cases

After the latest relevant product fix and after normal acceptance first passes,
obtain at least three distinct clean full-engine ordinary-AI adversarial scenarios.
Any relevant fix restarts this clean-three requirement:

1. **Tiberium/traffic/state-transition adversary:** dense evolving green/blue/red
   geometry, a narrow busy refinery, aborted then completed unload, changed field,
   kiting plus a second threat. Failure hypothesis/signal: resource transit,
   premature commit, congestion, chase abandonment. Pass evidence: zero hazard and
   lane occupancy, exact post-success transition, uninterrupted later delivery,
   local engagement and re-form.
2. **Scale/contention/invalidation adversary:** co-located and separated
   harvesters, scarce mixed units, artillery/transport/crate/base-protection
   reservations, destroyed/captured actors, blocked station. Failure hypothesis/
   signal: army starvation, erased coverage, tug-of-war, stale requests/IDs. Pass:
   stable rightful ownership, useful breadth, deterministic release/replacement,
   materially better defended economy than control.
3. **Topology/static-defense/persistence adversary:** Archipelago or equivalent
   unreachable domains, save/load before and after commit, existing overlapping
   SAM, low power and narrow build geometry, air then ground raid. Failure
   hypothesis/signal: no-path spam, load duplication, useless SAM, blocked access,
   unbounded cost. Pass: reachable-only screens, correct restored state plus fresh
   confirmation, powered useful coverage without duplication, valid ground
   counterplay, bounded MAX cost.

Also force unusual unit counts (zero of one role and surplus of another), missing
factory/barracks/refinery/power, defender/harvester destruction, capture, resource
growth over a prior station, long duration, enemy pressure from two directions,
and repair/recovery as relevant. Every case must state its hypothesis,
perturbation, failure signal, and visible pass outcome before launch. An event that
did not occur is not a pass.

### Final regression

After three clean adversarial scenarios, rerun the literal two-separated-field
acceptance from a fresh full-engine start with all normal modules, ordinary real
AIs, and the strongest compatible stress: scarce initial defenders built through
normal queues, a storage-full aborted unload before a successful unload, one
defender casualty/replacement, evolving Tiberium near (not invalidating) the safe
route, simultaneous ground and air harassment, and an existing powered SAM that
covers only one of two exposed economy approaches.

Require the exact literal final outcome again: post-success committed field
stations only, roughly 1/2/1 useful coverage for both isolated harvesters, zero
infantry Tiberium transit/occupancy, zero defender-caused refinery traffic delay,
local interception/leash/re-form, both harvesters complete another delivery,
missing role recovers, powered SAM coverage is reused/extended without blocking
the economy, structures survive and remain operational, and changed evidence
materially beats the matched old control. Record map/options/bots/actors/ticks,
artifact paths, final outcome, and diagnostics cleanup. Reloaded state is not this
final regression.

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

Modular ownership for CNC-42:

- A cohesive field-context boundary owns pending actual harvest and successful
  post-unload commit semantics; it reports facts but does not decide harvesting.
- A dedicated player bot module owns clustering, defender demand, reservations,
  stable assignments, requests, station orders, leash/re-form/release, and its
  save data. Keep world-independent demand/geometry/state-transition rules in
  small pure helpers with focused tests.
- BaseBuilder/its queue placement owner remains responsible for SAM selection,
  existing-coverage/power checks, legal placement, and construction order. Use a
  narrow factual interface or cohesive helper between owners rather than calling
  another module's private state or moving all logic into the queue manager.
- Reuse/extract general resource-safe path primitives only when that avoids real
  duplication; do not merge stealth-threat targeting, field defense, Economy
  artillery, and base construction into a universal oversized manager.

Concise implementation/publication plan:

1. Record baseline/differential manifests, inspect any new CNC-41 PR commits, and
   add config validation plus pure transition/demand/geometry tests.
2. Implement the smallest durable post-unload field-context seam and dedicated
   reservation/assignment module; prove G1 paired full-engine behavior immediately
   after the first product change.
3. Add evidence-driven missing-role production, safe routes/stations,
   leash/re-form/invalidation/persistence, and resolve only observed contention.
4. Add bounded economy-SAM coverage/placement at BaseBuilder's ownership boundary;
   validate power, traffic, existing coverage, and CNC-41 integration.
5. Climb the differential/adversarial ladder, remove noisy diagnostics, run CNC
   tests/checks and performance evidence, write the report, inspect dependency
   commits, commit/push only the task branch, open one PR to the recorded base,
   wait for checks, and perform the allowed final review response.

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
bad strategic policy, or displaced regression until evidence rules that out.
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
that the harness and simplest behavior work. As soon as it passes, change at least
one meaningful dimension—timing, map geometry, resources, missing/destroyed
assets, unit count, pressure, competing orders, save/load boundary, or match
duration—and make every later test harder or materially different. Never spend
cycles on near-identical happy-path confirmations when a stronger falsification
is possible. These tests replace much human feedback: use surprising results to
challenge the spec's assumptions, inspect the repository/evidence, and choose the
next change without asking the user an implementation question.

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
`/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-4-cnc42/cycle-review-05/CYCLE-REVIEW.md`.

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
   Launch a no-history fresh `policy-reviewer` role (Terra 5.6 medium). Questions
   embedded in the narrative are the worker's questions to this playtester; the
   job contains no inline context.
5. Read the `POLICY-REVIEW.md` before choosing the next code change. Treat advice
   as hypotheses: record what inspired the next test/change and what was rejected
   with reasons. Never substitute the review for adversarial game evidence.

Detailed narratives/reviews stay under the ignored analysis directory. Preserve
their paths plus concise factual and policy conclusions in the cycle journal and
task report. A paired two-game batch may share one Commenter and Policy Reviewer.

If a policy problem persists after at least ten completed full-engine game tests,
the worker may ask exactly one Sol 5.6 xhigh `policy-escalation` instance. First
write a new narrative stating the game-test count, repeated failure pattern,
attempted policies, evidence for/against each, and focused questions. The
escalated reviewer still reads only the design document and narrative. Record use
in the assignment field. Never invoke it before test 10 or invoke it twice for one
task.

Prefer the full engine and real bot types. On Linux use the explicit headless MAX
path when graphics/input are irrelevant. Prove the current run loaded the intended
map, bots, actors, options, activated headless MAX, advanced ticks, flushed logs,
replay/benchmark evidence where configured, and produced the final outcome. A
passive fixture or manager-only simulation is not sole proof. Use focused setup
maps to accelerate reproduction, but before acceptance run a fully enabled
scenario containing every relevant ordinary module. Headless MAX never replaces
required graphical, rendering, input, lobby, or platform checks.

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
