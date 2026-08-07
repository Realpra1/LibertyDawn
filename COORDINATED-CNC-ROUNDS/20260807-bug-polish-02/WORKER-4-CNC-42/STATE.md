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
- Status: `Complete - testing`
- Common base branch/SHA: `agent/cnc-20260806-bug-polish-01-release` / `419bee2531d4802bf922c3597b42c6eeb75ab250`
- Task branch: `agent/round-20260807-cnc42-economy-field-defense`
- Intended PR base: `agent/cnc-20260806-bug-polish-01-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `20`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/workers/worker-4-cnc42/COORDINATED-CNC-ROUNDS/20260807-bug-polish-02/WORKER-4-CNC-42/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-4-cnc42`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `72` (66 isolated plus six integrated RC1 games: G5 discovery, G5 mid-build save, G5 exact load, combined G4, combined G7, and the stressed final; the tick-0 missing-map load attempt is excluded)
- Terra cycle code reviews: `cycle 5 advisory adopted: validated hazard-free paths were reduced to four-cell waypoints whose ordinary Move activities could re-path through forbidden cells; cycle 6 preserved every safe segment (`analysis/worker-4-cnc42/cycle-review-05/CYCLE-REVIEW.md`). Cycle 10 advisory adopted: the judged e1 occupancy proves owned safety must veto every ordinary movement source, not only exact routes/nudges, and needs a forced like-for-like regression (`analysis/worker-4-cnc42/cycle-review-10/CYCLE-REVIEW.md`). Cycle 15 advisory rejected after source verification: the synchronized SetMoveAlongPathSafety order is recorded in the save order stream and deterministically reconstructs both strictMovementSafety and strictAvoidCells during replay before IGameSaveTraitData resolution; SyncAttribute controls hash reporting rather than save serialization, and the cycle-15 load reproduced the live pre-boundary trace exactly (`analysis/worker-4-cnc42/cycle-review-15/CYCLE-REVIEW.md`). Cycle 16 nevertheless retained a post-load pre-scan forbidden-cell assertion. Cycle 20 advisory adopted as deferred: exact economy-SAM queue/type ownership is runtime-only and is lost when loading between reservation and placement, so that one build can fall back to ordinary placement (`analysis/worker-4-cnc42/cycle-review-20/CYCLE-REVIEW.md`); resolving and proving it requires forbidden cycle 21, therefore hand off First iteration.`
- Sol-xhigh policy escalation: `unused (requires at least 10 game tests; one maximum)`
- PR: `#89 https://github.com/Realpra1/LibertyDawn/pull/89 (draft and mergeable; base agent/cnc-20260806-bug-polish-01-release; tested product head 84dbf5013d8b6b3c696e8d6f80f24c7be00f1a23)`

## Integrated repair assignment

- Phase: `integrated testing`
- Current release branch/head: `agent/cnc-20260807-bug-polish-02-release` / `ffb841b48750cc54b1862fb93101d3dce3a87a3f`
- Integration notes: `COORDINATED-CNC-ROUNDS/20260807-bug-polish-02/INTEGRATION.md`
- Repair branch: `agent/round-20260807-cnc42-rc1-repair`
- Repair PR base: `agent/cnc-20260807-bug-polish-02-release`
- Integrated cycles used this RC: `1/3`
- Integrated cycles used total: `1/12`

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
- Worker pre-change dependency inspection on 2026-08-07 found local and remote
  branch `agent/round-20260807-cnc41-economy-tiberium-fields` at
  `ab7997c89b`; its scoped diff from the common base contains only CNC-41's worker
  contract and no product/config commits or reusable API. No matching GitHub PR
  was open. CNC-42 therefore keeps its field/traffic seams narrow and will inspect
  CNC-41 again before any shared `ai.yaml`/BaseBuilder change and publication.
- The required cycle-5 dependency recheck on 2026-08-07 found the same local and
  remote head `ab7997c89b8a2d545b894aef2a08e615e957032e`; the open-PR list still
  contained no CNC-41 PR. There remains no reviewed field/traffic/placement API
  to consume before CNC-42's future BaseBuilder work.
- The required pre-BaseBuilder cycle-10 dependency recheck on 2026-08-07 again
  found local, remote-tracking, and live remote head
  `ab7997c89b8a2d545b894aef2a08e615e957032e`; its only scoped commit/file is the
  CNC-41 worker contract, and the open-PR list still contains no CNC-41 PR. CNC-42
  therefore has no reviewed CNC-41 placement or traffic API to consume and must
  keep the economy-SAM seam independently narrow.
- The required publication recheck on 2026-08-07 found CNC-41 PR #88 at live
  remote head `418786381f64b1cae4ff9a8d1d943c78d5666646`, with product commit
  `aa4e97972d8a0cb7f4780babcdffa4fa363c2299`. Its scoped product diff adds an
  internal `BaseBuilderTiberiumFieldManager`, pure `TiberiumFieldPolicy`, two
  additive BaseBuilder config blocks, and queue placement/selection branches in
  the same three files CNC-42 touches: `BaseBuilderBotModule.cs`,
  `BaseBuilderQueueManager.cs`, and `mods/cnc/rules/ai.yaml`. It exposes no shared
  live field-identity, entrance, or traffic interface for CNC-42 to consume.
  CNC-42 therefore does not stack the unreviewed dependency branch; integration
  must combine the two independent managers/config blocks and order their queue
  branches deliberately, then rerun toxic geometry, static-SAM, and Archipelago
  persistence evidence on the reviewed combined candidate.

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
| 1 | Uncommitted cycle diff: exact unload-complete event, persistent harvester field context, Economy field reservation/assignment, resource/traffic-safe formations | Early commit, ordinary-squad theft, unsafe route, or no changed/control difference; identical one-harvester cheese smoke with exact initial 1/2/1 and timed raid | Policy tests 6/6; Release build; `make test`; G1 changed/control seed 424201, both tick 3000 | G0 factual/policy reviews approved control premise; G1 paths pending | First commit only at tick 726 after delivery; changed eventually retained mtnk/msam/one tracked rifle locally while control dispersed them. Failed exact composition because PROC supplied a second harvester and protection delayed tank/AA. Repeated 25-tick e1 reform orders are a forbidden failure. | Cycle 2: suppress en-route churn while retaining pursuit break, improve bounded no-route state, correct G1 harness to one pre-placed harvester, and repeat paired smoke with no exact composition ambiguity. |
| 2 | Progress-sensitive route ownership, pursuit/leash distinction, bounded route-rejection retry, and explicit unload diagnostics; corrected PROC-free-actor smoke harness | En-route defenders still churn every scan or fail the exact one-field role target; identical paired seed 424201 with one scripted initial harvester and tick-1800 stop | Policy tests 6/6; `make test`; `make check`; changed/control both passed tick 1800 | `analysis/worker-4-cnc42/g1-cycle2-comment/NARRATIVE.md`; routine policy verdict `insufficient evidence`, high confidence | Completed empty unload at tick 787 preceded bot commit at 801; exact fields=1/tanks=1/infantry=2/aa=1 at 1001; no 25-tick same-defender churn. Changed tank/MSAM held field cells while control remained at base. Raid preceded the screen, so no combat/economy improvement is claimed. | Pass G1 execution smoke. Cycle 3: add owned missing-role production and persisted assignment state, then G2 two-separated-field paired raids only after screens form, measuring deliveries, exchanges, and base tradeoff. |
| 3 | Owner-keyed external production requests with save-compatible UnitBuilder serialization; field/assignment/destination save data; refinery/low-power request gates and stale-field cleanup | Missing roles duplicate/stale or two-field breadth fails under normal production; paired G2 seed 424302 with only one mtnk/one e1 preplaced and fixed post-commit bike/e3/heli raids | Policy tests 11/11; Release build/`make test`; G2 changed/control both clean through tick 5000 (initial tick-0 Lua parse pair excluded) | `analysis/worker-4-cnc42/g2-separated-comment/NARRATIVE.md`; routine policy `insufficient evidence`, high confidence | Both original fields committed correctly and changed formed partial local screens, delaying left loss 284 ticks and killing 2/8 bikes vs control 0/8, but both harvesters died. No owned request logged: provider was cached absent while condition-disabled during early trait creation. Normal expansion grew to six committed fields. | Fail. Cycle 4: resolve enabled owned-production provider at request time, preserve measured original-field cohort in evidence, and repeat the post-commit paired raid with verified pre-contact tanks/e1/MSAM. |
| 4 | Resolve active owned-production provider at each scan/cancel across all providers; per-field composition diagnostics pin the original cohort | Provider fix still fails or one field monopolizes sequential demand; same G2 seed/raids with measured original fields and attempted higher lobby cash | Policy tests 11/11; Release `make test`; changed/control both passed launch manifests through tick 5000 | `analysis/worker-4-cnc42/g2-cycle4-comment/NARRATIVE.md`; routine policy verdict `mixed`, high confidence | Owned requests worked. Left reached pre-contact 1/2/1; right stayed 1/2/0 until reassignment after left died. Changed killed heli+bike and preserved refineries, but left/right harvesters died at 4138/4886 vs control 4025/4945; no decisive survival gain. MSAM 369 then churned idle retries every 25 ticks from 3651-4126. | Fail G2 survival. Cycle 5: allow two bounded outstanding role requests to exploit parallel queues and suppress idle en-route retries until stall timeout; rerun with explicit simultaneous pre-contact assertions. |
| 5 | Allow two bounded owner-keyed outstanding requests per missing role and suppress idle route retry until the configured 250-tick stall timeout | Parallel demand may still monopolize one field or idle defenders may retain forbidden 25-tick churn; same two-field raid with a pre-contact spatial 1/2/1 assertion at both original fields | Policy tests 11/11; changed passed through tick 5000; concurrent current control advanced through tick 2001 but ended naturally before raids and is invalid for outcome comparison; prior identical-base cycle-4 control reused without recounting | `analysis/worker-4-cnc42/g2-cycle5-comment/NARRATIVE.md`; routine policy verdict `mostly sensible`, medium confidence; cycle review advisory adopted from `analysis/worker-4-cnc42/cycle-review-05/CYCLE-REVIEW.md` | Changed reached spatial 1/2/1 at both original fields by tick 3401, killed heli+bike, and kept the right harvester alive through tick 5000 where valid control lost it at 4945. Left harvester still died (4052 vs control 4025); refinery health tradeoff was mixed. Representative idle retries were about 250 ticks apart, eliminating the cycle-4 25-tick loop. Reviewer found that four-cell waypoint spacing did not guarantee the issued ordinary moves retained the validated hazard-free path. | Qualified first decisive G2 seed, not acceptance. Adopt review: cycle 6 must issue the validated route without unsafe segment re-pathing and prove it in G4 toxic geometry; static SAM/unload-traffic/invalidation boundaries remain unproven. |
| 6 | Synchronized bounded `MoveAlongPath` order/activity preserves every validated adjacent cell and rechecks resource safety per segment; configured only on field role actors; rate-limited actual resource/traffic occupancy diagnostics | Exact order may fail serialization or still re-path through the new blue/red barrier; five harvesters, green/blue/red geometry, congested refineries, attempted storage-full abort, later paired raids and bike kites | Exact-route/policy tests 12/12; Release build and `make test` passed with zero warnings; corrected G4 changed/control both reached tick 6000 (initial no-Xvfb tick-0 pair excluded) | `analysis/worker-4-cnc42/g4-cycle6-comment/NARRATIVE.md`; routine policy verdict `mixed`, high confidence | Exact orders executed and changed formed useful original-field screens (tick 4001 left 2/2/2, right 1/3/1 vs control 0/0/0 and 0/1/0), preserving the right harvester at 34,434 health vs control 11,000. Failed mandatory safety: multiple e1 resource occupancies from tick 1801 onward plus vehicle/MSAM cases, after station orders and during pursuit/resource evolution. Storage abort was unexercised because capacity was still 0 at tick 0. | Fail G4 zero-resource rule. Review confirms safety must be continuous across hold/pursuit/re-form/recovery. Cycle 7: own/restore defensive stance, exact non-attack routing, and a resource-growth margin; rerun corrected abort timing plus toxic route. |
| 7 | Own and restore each reserved actor's prior stance, clear inherited attack activity, use `Defend` plus exact non-attack routes, and require a one-cell resource margin; reject repeated serialized route cells | Prior pursuit may survive stance change or diagonal/evolving resource may overtake held/pathing actors; G4 retimed storage after capacity initialization and repeated paired raids/kites | Exact-route/policy tests 12/12; Release build and `make test` passed with zero warnings; G4 changed/control both reached tick 6000 | `analysis/worker-4-cnc42/g4-cycle7-comment/NARRATIVE.md`; routine policy verdict `Fail — exercised policy violation`, high confidence | Changed killed both bikes before the second kite and kept both original harvesters/refineries pristine with strong local screens, but control also kept the economy pristine and killed 3/8 raiders vs changed 2/8. Mandatory safety still failed: five e1 and three tank resource occupancies. Positive 8300/8300 storage was reached, yet AI spending reopened capacity and commits occurred before release, so the aborted/full-storage boundary remained unexercised. | Fail. Diagonal Tiberium spread is outside the one-cell Euclidean annulus and overlapping per-field destinations permit displacement. Cycle 8: two-cell spread buffer, globally unique formation cells, safe release fallback, and a continuously saturated plus explicitly cancelled unload harness. |
| 8 | Cover diagonal spread with a two-cell resource margin and use one globally unique formation-cell set across all active fields; harness continuously holds storage full and cancels one dock before release | Diagonal growth or cross-field destination overlap still pushes infantry into resources; full-storage cancellation may commit anyway | Policy tests 12/12; Release build and `make test` passed with zero warnings; changed reached tick 6000, control ended naturally around tick 3801 before raids | `analysis/worker-4-cnc42/g4-cycle8-comment/NARRATIVE.md`; routine policy verdict `Fail`, high confidence | Unload boundary passed: no commit before tick-751 cancel/tick-901 release; first commits tick 1001. Resource violations fell from eight to two e1-only late cases; no tank/MSAM/traffic violation. Both e1 appeared several cells from their last exact destination and were released. Changed killed 5/8 raiders and preserved both harvesters/refineries, but early control invalidates comparison. | Fail. Evidence points to ordinary `Mobile.Nudge` displacement, which queues an unconstrained move outside exact-route ownership. Cycle 9: activate a resource-safe nudge-cell validator only while the field module owns an actor, preserving normal movement after release; repeat G4 with a valid control. |
| 9 | Add an owner-activated `INudgeCellValidator`; exact field routes reject unsafe nudge destinations with their synchronized resource margin, and release restores ordinary nudge behavior | Ordinary collision displacement may still bypass the exact route and put an owned e1 on Tiberium; same toxic/full-storage scenario with raids advanced so the old control reaches them | Policy/exact-route tests 12/12; Release `make test` passed with zero warnings; changed naturally ended after observed tick 5000 and control reached tick 6000 | `analysis/worker-4-cnc42/g4-cycle9-comment/NARRATIVE.md`; routine policy verdict `Pass with duration-evidence limitation`, high confidence | Changed had no resource/traffic violation, no pre-release commit, both original harvesters alive (35,000/33,000) at tick 3751, and 8/8 raid callbacks before natural end. Control reacted faster at tick 3751 but lost the left harvester at tick 3915 and retained one raider through tick 6000. Throughput was effectively identical (~249.65 vs ~249.70 ticks/s). Changed launcher status failed only its configured tick-6000 minimum. | First clean adversarial after the latest safety fix, not full acceptance or duration proof. Cycle 10: add bounded economy-aware static-SAM preference after rechecking CNC-41, then exercise powered existing-coverage/non-duplication, legal placement, refinery access, and a changed arm that reaches the configured minimum. |
| 10 | Add Economy-II/Bot-type-gated SAM anchor policy and BaseBuilder planner: refinery/resonator/used-silo priority, powered existing coverage, four-site cap, narrow legal traffic-free placement, and bounded diagnostics | Existing coverage may duplicate, low power may spam, generic placement may miss the economy, or static placement may obstruct field/traffic safety; G5 starts with overlapping left coverage, holds low power, restores it, then applies paired raids through tick 6500 | Focused policy tests 18/18; Release `make test`/CNC MiniYAML passed with zero warnings; first paired harness attempt advanced to tick 801 then failed identically on unavailable Lua `table.concat`; corrected judged changed/control both reached tick 6500 | `analysis/worker-4-cnc42/g5-cycle10-comment/NARRATIVE.md`; routine policy verdict `FAIL`, high confidence; mandatory review `analysis/worker-4-cnc42/cycle-review-10/CYCLE-REVIEW.md` advisory adopted | Both arms retained exactly one SAM under low power. Changed placed economy sites at 67,16 and 76,16, preserved both harvesters and pristine refineries, and killed 7/8; control's generic site at 70,30 was outside right-refinery weapon range, it lost the right harvester, damaged the left refinery to 73,485, and killed 6/8. Changed failed mandatory safety when e1 364 occupied Tiberium at 76,17 on tick 3726; later release occurred at tick 3951. Changed also issued a second reservation while one SAM was pending, although only useful distinct coverage was ultimately placed. Throughput differed by <0.1%. Reviewer found no stronger determinism/save/contention/performance/SAM defect, but confirmed exact routes/nudges do not veto all ordinary movement sources. | Fail G5 safety despite decisive economy benefit. Adopt review: cycle 11 must enforce the owned predicate at every Mobile cell transition, project active resource-modifier exposure into routing/station cells, and strictly serialize SAM requests; rerun the same forced G5 differential with causal movement markers. |
| 11 | Generalize the owned movement validator to every `Mobile.CanEnterCell` transition; project active resource-modifier ranges plus one cell into owned safety; require one in-flight economy SAM request | A combat/ordinary transition may still put an owned e1 in growing resource, projected safety may strand screens, or SAM demand may overlap; unchanged G5 seed 424307 with active resonators, low-power recovery, growing resources, and paired raids | Focused policy tests 22/22; Release `make test`/CNC MiniYAML passed with zero warnings; changed logged through the tick-6001 duration snapshot but exited 1 on an exact-route exception; control passed tick 6500 | `analysis/worker-4-cnc42/g5-cycle11-comment/NARRATIVE.md`; routine policy verdict rejects runtime acceptance but retains strategy and recommends infantry-only strict resource exclusion, high confidence 0.94 (`analysis/worker-4-cnc42/g5-cycle11-policy/POLICY-REVIEW.md`) | No e1 resource occupancy and every economy-SAM reservation reported `pending=0`; changed placed sites at 67,16 and 76,18, kept both harvesters plus pristine refineries, and killed 4/8 by tick 6001. Control lost the right harvester, left the survivor at 7,000 and right refinery at 3,777, and killed 2/8. Changed twice claimed MSAM 395 while it was already on Tiberium and immediately released it for no safe route. At tick 6026 an actor already in its destination cell produced a one-cell path, and `MoveAlongPath.CreateOrder` threw; exit/benchmark acceptance failed. | Fail runtime. Cycle 12: treat a one-cell path as already arrived, reject initially unsafe claims, retain absolute resource safety only for e1 and absolute refinery-lane safety for all roles, and rerun G5 with class-specific occupancy assertions. |
| 12 | Treat same-cell subcell offsets as arrived and withhold one-cell exact orders; reject hard-unsafe claims; enforce current/projected resource vetoes only for infantry while vehicles prefer safe routes then fall back; retain all-role refinery traffic veto | The crash may recur or class-specific fallback may mask e1/traffic violations and weaken coverage; unchanged G5 seed 424307 through tick 6500, followed without code change by G4 dense green/blue/red geometry, full-storage cancellation, five fields, narrow traffic, and kites | Focused policy tests 23/23; Release `make test` and Debug `make check` passed with zero warnings/errors; G5 changed/control both passed tick 6500; post-cycle G4 changed passed tick 6000 and current control naturally ended after observed tick 3901, with prior valid same-seed tick-6000 control reused | G5 reviews: `analysis/worker-4-cnc42/g5-cycle12-{comment,policy}` PASS; G4 reviews: `analysis/worker-4-cnc42/g4-post-cycle12-{comment,policy}` bounded policy failure, high confidence 0.87 | G5 had no crash/e1/traffic/SAM-overlap signal; changed retained both harvesters/refineries and killed 7/8 while control lost both. G4 retained hard safety and delayed first loss 594 ticks, but logged 24 tank/MSAM preference misses across all resource types, lost one harvester at 4509, and killed 3/8 at outcome/4/8 late versus prior valid control's 5/8 and 7/8. The current natural-end control's simultaneous 8/8 disappearance is not duration evidence. | G5 literal acceptance is superseded as clean-streak evidence by the relevant G4 failure; clean-three resets to zero after cycle 13. Adopt fail-closed resource-clear vehicle station/reform. Defer new ingress-direction inference until isolating the permissive fallback against the earlier strict-current-resource G4 result. |
| 13 | Reject current resource cells and their configured margin for every owned role at claim, destination, exact path, nudge, and ordinary Mobile transition; retain projected modifier exclusion only for infantry | Fail-closed vehicles may become route-starved or still cross the long mixed-resource barrier through another movement source; repeat G4 seed 424306 with five harvesters, forced full-storage cancellation, narrow traffic, and paired kites | Focused policy tests 24/24; Release `make test`, Debug `make check`, and final Release `make test` passed with zero warnings/errors; changed passed tick 6000; current control advanced through the tick-3751 outcome then ended naturally/duration-invalid; prior valid control reused without recounting | `analysis/worker-4-cnc42/g4-cycle13-comment/NARRATIVE.md`; routine policy verdict approve, high confidence 0.86 (`analysis/worker-4-cnc42/g4-cycle13-policy/POLICY-REVIEW.md`) | Changed logged zero hard or preferred resource/traffic occupancy, committed only after storage release, formed five fields by tick 1326, retained both original harvesters through tick 6000, and kept them at 34,000/34,525 with pristine refineries at tick 3751. The valid old control left one harvester at 16,250 then lost it at tick 3915. Changed killed 2/8 early and 3/8 late versus control's 5/8 and 7/8, but preventing the control economy loss is decisive and the screen remained bounded/safe. | First clean adversarial after the latest relevant fix. Reviewer flags aggregate local unit count/opportunity cost as the next risk; rerun G5 to ensure strict vehicles retain literal/static acceptance, then force constrained changing-field release/invalidation evidence. |

| 14 | Replace host-only movement-safety mutation with a synchronized, canonical, bounded actor order carrying the refinery/projected-hazard snapshot | Post-commit save replay may diverge because bot modules do not run during replay and therefore did not reproduce the safety toggle; fresh G7 save at tick 3200 followed by exact current-code reload | Focused policy/exact-route tests 25/25; fresh G7 passed tick 6500 and wrote save SHA `039aeb44...`; exact reload reproduced the live trace through tick 3201, then exited 1 resolving saved field-defense data | `analysis/worker-4-cnc42/g7-cycle14-comment/NARRATIVE.md`; routine policy verdict `insufficient evidence`, high confidence (`analysis/worker-4-cnc42/g7-cycle14-policy/POLICY-REVIEW.md`) | Synchronization diagnosis passed: live/replay sample traces are byte-identical through the saved frame and the prior frame-1066 desync is gone. Persistence still fails: `FieldLoader.ParseCPos` throws on `DestinationCells` because generic `CPos[]` formatting flattens comma-delimited cells into an unparseable array. Fresh topology remained safe/unreachable-aware but lost the main harvester at tick 2137; this is not accepted policy evidence. | Fail persistence; clean-three remains reset. Cycle 15 changes only destination-cell save representation to integer `CPos.Bits`, repeats a fresh two-station save plus exact load/raid window, then completes the mandatory cycle-15 code review before any further product change. |

| 15 | Persist destination cells as integer `CPos.Bits` arrays and reconstruct cells explicitly, with a direct generic save-loader round trip | Parse-safe representation may still lose/duplicate ownership or continue differently after load; fresh G7 save at tick 3200 and exact load through both raids | Focused policy/save tests 26/26; fresh passed tick 6500; exact save SHA `3f640bef...` loaded without crash/desync and reached tick 5200, but launcher failed one expected release marker | `analysis/worker-4-cnc42/g7-cycle15-comment/NARRATIVE.md`; routine policy verdict `mixed`, high confidence; cycle-15 advisory rejected after source verification (`analysis/worker-4-cnc42/cycle-review-15/CYCLE-REVIEW.md`) | Fresh and replay traces matched through tick 3201 and restored composition logged at tick 3202. Reload then diverged materially: uninterrupted fresh lost harvester 230 and released its field at tick 4076, while reload kept both measured harvesters alive (25,000/35,000) and grew to three fields. This proves parse repair but fails exact continuation; route cooldown/progress/rejection state is not persisted. | Fail persistence continuity; clean-three remains reset. Reviews require exact field/harvester lifecycle equivalence before policy judgment. Cycle 16 will serialize only bounded behavior-affecting route timing/progress/rejection state, then repeat the fresh save/load raid with actor-level restoration and pre-scan safety assertions. |

| 16 | Persist bounded actor-keyed last-order cooldowns, route progress, and future route-rejection deadlines; restore only live owned assigned actors/bounded rejected candidates; add actor-level restoration diagnostics | Cleared route state may emit an immediate duplicate route and alter post-load harvester lifecycle; fresh G7 save at tick 3200, exact load, actor-level route restoration, no tick-3202 idle retry, and matched health/release assertions | Focused persistence/policy tests 27/27; Release `make test` and Debug `make check` passed with zero warnings/errors; fresh passed tick 6500; exact save SHA `ca4e45e5...` loaded exit 0 through tick 5200, with launcher failed only on an overstrict exact raid-kill count | `analysis/worker-4-cnc42/g7-cycle16-comment/NARRATIVE.md`; routine policy verdict `mixed`, medium confidence (`analysis/worker-4-cnc42/g7-cycle16-policy/POLICY-REVIEW.md`) | Load restored seven route records at tick 3201, emitted no tick-3202 idle retry, logged no resource/traffic/desync/runtime failure, retained the unreachable MSAM, grew to four fields, and matched fresh original-harvester health exactly (34,888/34,902) with both alive and no field-230 release. Fresh killed 3/6 raids while load killed 2/6; a save-boundary station transition repeated one tick later as `new-destination`, not an unpersisted idle route. | Partial persistence pass only; no clean-three credit. Reviews treat the surviving E3 and loaded screen decline to 0 tanks/3 infantry/0 AA as strategically material. Cycle 17 adds bounded per-release ownership/role-vacancy and per-raider target/damage telemetry, then reruns fresh/exact load before any speculative policy change. |

| 17 | Split defender release diagnostics into concrete validity/reservation owners plus active role vacancy and bounded eligible replacement; task-local stable-label raider/defender damage timeline; no balance/tactical change | The cycle-16 one-kill divergence may be caused by an invalid release, uncompensated vacancy, or different post-save combat contact; same focused G7 save/load, with exact causal telemetry | Focused tests 27/27; Debug `make check` and Release `make test` passed with zero warnings/errors; one wrong-map tick-6500 setup invalid; corrected fresh passed tick 6500 and exact save SHA `44a2d9cd...` load passed tick 5200 | `analysis/worker-4-cnc42/g7-cycle17-comment/NARRATIVE.md`; routine Policy Review `conditional pass`, one bounded continuity follow-up (`analysis/worker-4-cnc42/g7-cycle17-policy/POLICY-REVIEW.md`) | Both corrected arms killed the main bike at 3321 and E3 at 3531 with identical damage events; the SAM killed the helicopter at 3350 fresh/3355 load, so both reached 3/6. Main harvester health matched exactly at 23,031; far health differed by 400. Fresh released MSAM 235 at tick 3401 for `resource` with no eligible replacement, while load retained it and scanned on the one-tick-later cadence. No generic other-owner release, safety, topology, lifecycle, runtime, or desync failure occurred. | Diagnostic pass, not yet clean credit. Adopt the bounded review concern: cycle 18 persists the absolute scan phase so the missed load boundary does not permanently shift the resource-safety decision; repeat the same fresh/exact-load checkpoint without tactical tuning. |
| 18 | Persist an absolute next Economy field-defense scan tick with legacy countdown fallback; resume at the next original cadence tick when load resolution has already passed the saved scan boundary | A relative countdown restored after the bot tick permanently phase-shifts scans by one tick and changes defender 235's resource release into a reform; same G7 fresh/save/load checkpoint must agree on defender 235's first resource-safety decision | Focused tests 33/33 after the legacy refinement; Debug/Release compile, interface checks, and CNC MiniYAML passed with zero warnings/errors. First fresh attempt reached tick 6500 but is invalid because a task-map player-trait override changed bot trait ordering. Corrected exact-rules-shape fresh reached tick 6500, saved absolute `NextScanTick: 3201`, and had no safety/traffic/runtime/desync failure. Exact legacy cycle-17 load passed through tick 5200. | `analysis/worker-4-cnc42/g7-cycle18-comment/NARRATIVE.md`; routine Policy Review `PASS, with bounded follow-up concern`, medium-high confidence (`analysis/worker-4-cnc42/g7-cycle18-policy/POLICY-REVIEW.md`) | Corrected fresh reached `raid=3/6`, zero harvester losses, and pristine processors. Natural assignment ordering did not repeat the cycle-17 tick-3401 resource release, so its launcher failed only that deliberately overstrict marker, but it remains the independent fresh/new-save baseline. The exact legacy load restored `saved=1` as `next=3201 current=3201 ticks=25`, released defender 235 at tick 3401 for `resource` with no eligible replacement and no tick-3402 re-form, reached `raid=3/6`, lost no harvester, and kept both processors pristine without safety/traffic/runtime/desync failure. | The bounded persistence defect and cycle-17 review concern pass their exact regression. Reject actor ID 235 as a required fresh-run oracle because actor identity and natural assignment order are save-specific; the new-format fresh save plus exact legacy event jointly prove the bounded phase behavior. Cycle 19 disables evidence logging and proves the publication configuration without tactical tuning. |
| 19 | Disable task-owned mobile field-defense and Brutalis/Iron Reaper economy-SAM debug switches; move the task G7 archive out of the product map tree without changing code or policy | Diagnostics cleanup could accidentally alter behavior or leave task debug output enabled; fresh exact-rules-shape Archipelago run keeps storage cancellation, later unload, topology, replacement, save boundary, and paired raids while explicitly forbidding both task debug prefixes | Focused policy/persistence tests 33/33; Debug and Release builds, interface checks, and CNC MiniYAML passed with zero warnings/errors. Fresh publication configuration passed through tick 6500 at 270.5 ticks/s and wrote save SHA `0ba298b2...`; post-cleanup CNC MiniYAML passed without seeing the task map; one no-content configuration stop and one wrong content-root tick-0 UI launch are excluded. | `analysis/worker-4-cnc42/g7-cycle19-comment/NARRATIVE.md`; routine Policy Review `policy-compatible`, high confidence, no bounded concern (`analysis/worker-4-cnc42/g7-cycle19-policy/POLICY-REVIEW.md`) | All required setup/state/raid markers were present; tick-4201 outcome was `raid=3/6 harvLosses=0`, main/far harvesters were 34,120/35,000 and 35,000/35,000, the shared processor was pristine, unreachable MSAM stayed at 91,96, task debug prefixes were absent, and no runtime/desync signal occurred. | Publication configuration and its independent reviews pass. Publish the safest useful result without further product change; propose `First iteration - testing` because the post-persistence clean-three and exact stressed final-regression bar were not completed within the isolated cycle budget. |
| 20 | Final-review response: retain economy SAM ownership on the exact production-queue object plus actor type until its queued build leaves that queue; route only that owned build through economy placement and restore ordinary SAM BuildingFractions/general placement | Final reviewer found Economy II globally suppressed ordinary SAM selection and redirected every completed SAM into economy-only placement; focused exact-queue lifetime regression plus fresh fully covered economy with ordinary authored fraction eligible and no BaseBuilder override | Focused tests 34/34; `make check test` passed Debug/Release compilation, interface checks, and CNC MiniYAML with zero warnings/errors. One invalid observer run reached tick 3401 and exposed the second SAM before unsupported Lua `ActorID` access; corrected run exited 0 at tick 5200 with replay/benchmarks and no runtime/desync signal, although two overstrict manifest regexes left its launcher label failed. | `analysis/worker-4-cnc42/g5-cycle20-comment/NARRATIVE.md`: no-control/decision-log evidence limitation, but direct additional-site observation; routine Policy Review `insufficient evidence`, high confidence (`analysis/worker-4-cnc42/g5-cycle20-policy/POLICY-REVIEW.md`) because no matched control, air threat, spending, or explicit decision log; cycle-20 code review adopted one deferred save/load advisory (`analysis/worker-4-cnc42/cycle-review-20/CYCLE-REVIEW.md`) | Initial powered SAM at 47,17 covered proc/resonator/used-silo anchors at squared distances 50/37/37 within radius-squared 64. One site remained through tick 3701; sites at 42,35 and 47,35 appeared by 3801 and 40,35 by 4801, all well outside economy-anchor annuli. Tick 5001 recorded four live SAMs and every anchor alive; neither economy reservation phrase nor runtime/desync failure appeared. Reviewer verified that a save/load between economy reservation and placement loses the runtime-only ownership and demotes that build to ordinary placement. | Required live-run suppression defect is repaired and directly regression-tested. Adopt save/load ownership as a real blocker, but resolution would require forbidden cycle 21; hand off `First iteration - testing`. Relevant cycle-20 correction also resets clean-three, no stressed fresh final regression was possible, causal control evidence is limited, and combined CNC-41 validation remains outstanding. |

Post-cycle-13 evidence without a product change: the fresh G5 changed/control pair
both passed tick 6500. Changed remained free of hard/preferred resource and traffic
signals, placed four distinct useful economy SAMs, retained both original
harvesters and pristine refineries, and achieved the same 6/8 outcome kills as
control; control lost its right harvester and left refinery. Changed throughput
was about 5% below control, within the required 10%. Fresh factual and policy
reviews accepted the literal/static result with a moderate 0.74 policy confidence
(`analysis/worker-4-cnc42/g5-post-cycle13-{comment,policy}`).

G6 then forced six committed contexts, ordinary Economy-artillery competition,
a destroyed reserved Mobile SAM, a destroyed committed extra harvester, and paired
remote raids. The first pair counted because both advanced materially, although
the control ended naturally/duration-invalid; a harness-only duration sentinel
produced a clean corrected pair through tick 6500. Changed released/replaced the
Mobile SAM within 100/150 ticks and released the dead harvester's field within 200
ticks, reduced demand from six to five fields, logged no resource/traffic signal,
kept both original harvesters and both refineries alive, and had 3/8 raid kills at
tick 4201 versus control's lost right harvester, damaged right refinery, and 0/8.
Changed ran about 11% faster. Fresh policy review supports the bounded release and
replacement result at moderate confidence (`analysis/worker-4-cnc42/g6-comment/NARRATIVE.md`,
`analysis/worker-4-cnc42/g6-policy/POLICY-REVIEW.md`). This is clean adversarial
two; topology, persistence, and field-exhaustion proportionality remain next.

G7 exercised stock Archipelago and a focused Archipelago-derived topology with
reachable economy contexts plus an unreachable-domain Mobile SAM. The valid
focused cycle-13 changed/control pair passed tick 6500 at essentially identical
throughput; changed kept both measured harvesters alive and the unreachable actor
fixed at 91,96 while control lost its far harvester. A pre-commit negative save
loaded cleanly, and an old-control post-commit save also loaded cleanly. The
cycle-13 changed post-commit save desynchronized while replaying its recorded
orders, isolating a product persistence defect and beginning cycle 14.

Cycle 14 synchronized the field-owned movement-safety state with a canonical,
bounded actor order. A fresh focused run passed tick 6500 and wrote a current save
at tick 3200. Its exact reload reproduced every sampled live position through
tick 3201, eliminating the prior replay desync, but then crashed before the loaded
world's first tick while parsing the field module's comma-flattened `CPos[]`
destination data. Fresh factual review classifies persistence as an evidence
blocker; routine Policy Review returns `insufficient evidence`, high confidence,
and requires serialization repair before combat-policy judgment. The latest
relevant fix therefore has no clean adversarial credit.

Cycle 15 made destination persistence parse-safe by storing integer `CPos.Bits`.
Its fresh run passed tick 6500 and exact save SHA `3f640bef...` loaded without a
crash or desync through tick 5200. The restored composition appeared at tick 3202
and the unreachable actor remained fixed. Continuation nevertheless diverged:
the uninterrupted run lost harvester 230 and released its field at tick 4076,
while reload kept both measured harvesters alive at 25,000/35,000 through outcome
and later grew from two to three fields. The missing release marker is therefore
a real state mismatch, not a harness false negative. Assignment/destination state
is persisted, but behavior-affecting route cooldown, progress, and rejection state
is cleared on restore. Required cycle-15 factual/policy and code reviews were
completed before the next product change. The fresh Commenter confirms that the missing
release is a substantive persistence-equivalence failure rather than a launcher
failure (`analysis/worker-4-cnc42/g7-cycle15-comment/NARRATIVE.md`). Routine
Policy Review returns `mixed`, high confidence, and requires lifecycle equivalence
plus bounded actor-level restoration/combat telemetry before judging the apparent
reload improvement (`analysis/worker-4-cnc42/g7-cycle15-policy/POLICY-REVIEW.md`).
The cycle-15 code reviewer alleged that replay would retain the synchronized
strict-safety boolean but lose its unsaved avoid-cell set. Source verification
rejects that premise: game saves replay the recorded synchronized safety order,
whose resolver reconstructs both values before trait-data restoration; `[Sync]`
only contributes to the sync hash. The byte-identical live/replay pre-boundary
trace supports that path. Cycle 16 still keeps a pre-first-scan forbidden-cell
assertion to guard the invariant (`analysis/worker-4-cnc42/cycle-review-15/CYCLE-REVIEW.md`).

Cycle 16 persisted the bounded behavior-affecting route state as nested primitive
scalars. A fresh run passed tick 6500 and wrote exact current save SHA-256
`ca4e45e5df0144ade825be7c88bfcf67b59384b85438517027277781d0c6d8cc`.
The load restored seven actor route records at tick 3201 and did not reproduce
cycle 15's immediate tick-3202 idle retry. It exited normally at tick 5200 with
no exception, desync, resource, traffic, topology, or harvester-release signal.
Both fresh and load retained the original harvesters at exactly 34,888 and 34,902
health through the tick-4201 outcome and both grew to four fields. The launcher
status is failed solely because its exact fresh `raid=3/6` regex observed `2/6`
after load. This is a real one-kill combat continuation variance, but it does not
violate the predeclared harvester lifecycle, assignment, safety, or field-release
oracle. A field station that transitioned during the save boundary was reprocessed
one tick later as `new-destination`; no restored defender received the forbidden
immediate `idle-retry`. The Commenter classifies exact-load equivalence as
limited/failed despite the correct lifecycle, because the main E3 survives and
the loaded composition later declines to zero tanks and zero AA
(`analysis/worker-4-cnc42/g7-cycle16-comment/NARRATIVE.md`). Routine Policy Review
returns `mixed`, medium confidence, and likewise withholds acceptance: every
post-load release must identify its concrete owner/invalidity, active-field role
vacancy, eligible replacement, and causal combat timeline before the one-kill
difference can be judged (`analysis/worker-4-cnc42/g7-cycle16-policy/POLICY-REVIEW.md`).
Cycle 17 therefore changes diagnostics, not balance or unproven policy.

Cycle 17's first launch reached tick 6500 but used the inherited cycle-16 ignored
map copy, so it is counted as an invalid setup and excluded from the useful pair.
The corrected fresh run used map SHA-256 `892fa9de...`, passed tick 6500, and
wrote save SHA-256 `44a2d9cdb4394647c9d3e342d556730927bb80203392b9a11529965d36926b15`.
Its exact load passed tick 5200. Both continuations killed the main bike at tick
3321 and the main E3 at tick 3531 through identical attributed damage chains;
the covering SAM killed the helicopter at tick 3350 fresh versus 3355 load, so
both satisfied the required `3/6` checkpoint. Main harvester health was exactly
23,031 in both and the far harvester differed by only 400 health. The diagnostic
separated releases into missing actors, refinery traffic, and resource
invalidation with active role/replacement facts; it found no generic competing
reservation owner behind the main result. One persistence variance remains:
fresh released MSAM 235 for current resource at tick 3401 while load retained it
and performed field scans one tick later. The fresh Commenter verifies the
corrected pair and invalid inherited-map exclusion
(`analysis/worker-4-cnc42/g7-cycle17-comment/NARRATIVE.md`). Routine Policy Review
returns a conditional pass with one bounded follow-up: make defender 235's first
post-save resource-safety release/re-form decision agree without tactical tuning
(`analysis/worker-4-cnc42/g7-cycle17-policy/POLICY-REVIEW.md`).

Raw save inspection identifies the bounded cause. The tick-3200 save stores
`EconomyFieldDefenseScanTicks: 1`, and uninterrupted play consumes that countdown
at tick 3201. Load resolution occurs after bot processing at tick 3201, so
restoring the same relative value first scans at tick 3202 and permanently moves
the 25-tick phase to 3402. The save contains field 230's AA assignment and actor
235's `LastOrder: 2926`; missing assignment/cooldown is not the cause. Cycle 18
will persist the absolute next scan tick and, when its boundary scan has already
been missed, resume at the next original cadence tick (3226, then 3401). It will
retain legacy countdown loading and make no balance or tactical change.

Cycle 18 implements that bounded phase repair. A first fresh run reached tick
6500 but is invalid because its task-map `Player` trait override altered bot-trait
processing order. The corrected exact-rules-shape fresh run reached tick 6500,
wrote the new absolute `EconomyFieldDefenseNextScanTick: 3201` save field, retained
both harvesters and pristine processors, reached `raid=3/6`, and logged no safety,
traffic, runtime, or desync failure. Its naturally different assignment ordering
did not reproduce actor 235's old resource event, so the overstrict marker alone
failed. Loading cycle 17's exact legacy save under the final fallback then restored
`saved=1 next=3201 current=3201 ticks=25`, released MSAM 235 at tick 3401 for
current resource with no eligible replacement, emitted no forbidden tick-3402
re-form, and retained the defined `raid=3/6`, zero-harvester-loss, pristine-
processor outcome through tick 5200. This directly closes the routine review's
one bounded persistence concern without tactical or balance tuning.

Cycle 19 disables all three task-owned evidence switches in the published CNC
rules and moves the untracked G7 archive out of `mods/cnc/maps` into the ignored
analysis area with its SHA unchanged. All 33 focused tests, Debug/Release builds,
interface checks, and CNC MiniYAML passed with zero warnings/errors. A fresh
debug-disabled Archipelago run then passed tick 6500 at 270.5 ticks/s, wrote save
SHA-256 `0ba298b2dd31ac8e918d75f0baf277c98a55d4cfdc4b39f597235b7411b8efce`,
emitted neither task debug prefix, retained both harvesters and a pristine
processor at the tick-4201 `raid=3/6 harvLosses=0` outcome, kept the unreachable
MSAM at 91,96, and logged no runtime or desync failure. The initial no-content
configuration stop and a wrong content-root mod-content UI launch advanced no
game ticks and remain excluded.

The one allowed final-review response became cycle 20. The final Sol-high review
correctly found that actor-type-only economy SAM control skipped every configured
SAM from ordinary `BuildingFractions` and sent every completed SAM through
economy-only placement. Cycle 20 now retains ownership on the exact runtime
`ProductionQueue` object plus actor type from economy selection through placement;
only that matching build takes the economy placement path. Ordinary SAMs remain
eligible for their unchanged authored fraction and use normal general-defense
placement. The focused ownership/lifetime regression raised the suite to 34/34,
and full Debug/Release, interface, and CNC MiniYAML gates passed cleanly.

The fresh covered-anchor engine case used one powered SAM at 47,17 to cover its
active refinery, Resonator, and materially used Silo, with squared distances
50/37/37 inside effective radius-squared 64. It retained the real Brutalis trait,
unchanged one-percent SAM fraction, and all normal modules; 100 pre-placed normal
wall buildings made that authored fraction eligible without a rules override.
After remaining at one site through tick 3701, general-defense sites appeared at
42,35 and 47,35 by tick 3801 and at 40,35 by tick 4801, all outside the economy
annuli. Tick 5001 retained four SAMs and all three anchors; the run exited 0 at
tick 5200 with replay/benchmarks and no economy-reservation, runtime, or desync
signal. Its launcher label failed only an unavailable BotDebug sentence and an
overstrict exact-two-site regex. The prior observer attempt is counted invalid:
it exposed a second site by tick 3401 but then used unsupported Lua `ActorID`.
Fresh factual review accepts the direct site observation but flags the missing
matched control/decision trace. Routine policy review therefore returns
`insufficient evidence`, high confidence, for strategic causality rather than a
policy failure. The exact source-level queue ownership and focused test close the
reviewed suppression defect; stronger causal instrumentation/control, the reset
clean-three, and the stressed final regression remain deferred because cycle 20
is the hard isolated cap. The mandatory cycle-20 Terra reviewer also found a
separate real save/load boundary: the exact queue/type ownership is runtime-only,
so loading after economy reservation but before placement loses it and routes that
one completed site through ordinary placement. This is adopted rather than
disputed. Persisting/reconstructing the ownership and proving an exact mid-build
load would require forbidden cycle 21, so it is an explicit First-iteration
handoff blocker.

## Handoff receipt

- Proposed status: `First iteration - testing`. The safest useful result is
  published, but cycle 20 reset the clean-three requirement and exposed one
  adopted persistence blocker; the hard cap forbids cycle 21.
- Final branch/head: `agent/round-20260807-cnc42-economy-field-defense` at tested
  product head `84dbf5013d8b6b3c696e8d6f80f24c7be00f1a23`; the handoff-receipt-only commit
  follows this product head and changes no code, rules, tests, or game content.
- PR and checks: draft PR #89, https://github.com/Realpra1/LibertyDawn/pull/89,
  targets exact base `agent/cnc-20260806-bug-polish-01-release` and is mergeable.
  CI run 31181233145 passed Linux .NET 6.0 in 2m33s and Windows .NET 6.0 in
  3m34s on the tested product head.
- Cycles used: `20/20`; cycle 20 was the one allowed final-review response.
- Acceptance evidence: G2 showed two separated-field demand and a changed-side
  harvester survival absent in control; G4 proved post-success unload commit,
  zero owned resource/traffic violations, and preservation of the control-lost
  harvester; G5 proved useful powered economy coverage and economy survival; G6
  proved contention/invalidation/replacement; G7 proved reachable-only topology,
  restored bounded state, and publication diagnostics-off behavior. Cycle 20
  directly proved ordinary authored-fraction SAM construction still proceeds
  when every economy anchor is already covered.
- Adversarial evidence: clean G4/G6 and later G5/G7 evidence exists for earlier
  product heads, but the relevant cycle-20 SAM ownership correction resets the
  contractual clean-three counter. Only the fresh cycle-20 covered-anchor/general-
  defense case follows that correction, so no post-fix clean-three is claimed.
- Old-behavior control and comparative result: exact base
  `419bee2531d4802bf922c3597b42c6eeb75ab250` was used for matched controls.
  Changed behavior decisively preserved at least one economy asset in valid G2,
  G4, G5, G6, and G7 comparisons; the accepted matched G5 pair was about 5%
  slower, inside the 10% limit. Cycle 20 lacks an identical matched control and
  explicit queue-decision trace, so it proves removal of suppression directly but
  not broader strategic causality.
- Match narratives and routine policy-review conclusions: factual Commenters and
  routine Policy Reviewers were completed for every judged batch. The cycle-19
  publication run was policy-compatible at high confidence. Cycle 20's factual
  review confirms additional ordinary sites, while its policy review returns
  `insufficient evidence`, high confidence, because no identical control, air
  threat, spending trace, or explicit ownership decision log was present; this is
  an evidence limitation, not a policy-failure finding.
- Terra cycle code reviews and dispositions: cycle 5 exact-route advisory adopted
  and fixed in cycle 6; cycle 10 all-movement safety advisory adopted and fixed in
  cycles 11-13; cycle 15 save-order premise rejected after source inspection and
  byte-identical replay evidence, while the extra pre-scan assertion was retained;
  cycle 20 runtime-only exact SAM queue/type ownership advisory adopted as deferred.
- Sol-xhigh policy escalation (unused, or test count/path/conclusion): unused
  after 66 counted full-engine tests.
- Final regression: not completed after the cycle-20 relevant correction. Earlier
  literal/static, toxic-geometry, invalidation, topology, and persistence runs are
  retained as supporting evidence but cannot satisfy the reset final gate.
- Error/warning and diagnostic-cleanup result: focused tests passed 34/34;
  `make check test` passed Debug/Release compilation, interface checks, and CNC
  MiniYAML with zero warnings/errors. Task diagnostics are published disabled,
  and no task map, raw game log, replay, save, benchmark, or build output is tracked.
- Performance/determinism result: accepted paired MAX evidence ranged from faster
  or effectively equal to about 5% slower, within the 10% bound. Synchronized
  movement safety, bounded actor/route persistence, and absolute scan phase passed
  focused and full-engine replay/load checks without runtime/desync signals.
- Deferred work: persist or deterministically reconstruct exact economy-SAM
  queue/type ownership across a save made after reservation but before placement;
  add bounded ownership observability and an identical cycle-20 control with air
  harassment; obtain three clean post-fix adversaries and the stressed fresh final
  regression; combine CNC-41 PR #88 and rerun G4/G5/G7 on the reviewed candidate.
- Known failures/risks: a mid-build load currently loses runtime-only economy-SAM
  ownership and may demote that one completed build to ordinary placement. Cycle
  20 has limited causal evidence, no post-fix clean-three/final regression, and
  overlapping CNC-41 BaseBuilder/queue/config edits remain unintegrated.
- Relevant artifact paths: task report
  `COORDINATED-CNC-ROUNDS/20260807-bug-polish-02/WORKER-4-CNC-42/REPORT.md`;
  final review `analysis/worker-4-cnc42/final-review/FINAL-REVIEW.md`; cycle-20
  game `analysis/worker-4-cnc42/games/g5-cycle20-covered-general`; cycle-20
  factual/policy reviews `analysis/worker-4-cnc42/g5-cycle20-comment/NARRATIVE.md`
  and `analysis/worker-4-cnc42/g5-cycle20-policy/POLICY-REVIEW.md`; cycle-20 code
  review `analysis/worker-4-cnc42/cycle-review-20/CYCLE-REVIEW.md`; publication
  run `analysis/worker-4-cnc42/games/g7-archipelago/focused-cycle19-run2`.

## Integrated RC1 handoff receipt

- Proposed status: `Complete - testing`. One integrated repair cycle closes the
  adopted mid-build save/load blocker and passes the reset clean-three, combined
  CNC-41 validation, stressed final, repository gates, and publication checks.
- Tested product head: `b6e7eecf15a6993a2349b1595ffb2c350582d976` on
  `agent/round-20260807-cnc42-rc1-repair`, based on exact integrated release head
  `ffb841b48750cc54b1862fb93101d3dce3a87a3f`.
- Repair: save the economy-SAM reservation's queue actor ID, exact queue type,
  actor type, and reservation tick. Load resolves the original owned/live queue
  and restores ownership only when its matching build remains queued; missing,
  stale, dead, captured, disabled, and mismatched records fail closed. Legacy
  saves safely omit the new state.
- Focused and build gates: economy policy tests passed `35/35`; `make check &&
  make test` passed Debug/Release compilation, interface checks, and CNC
  MiniYAML with zero warnings/errors; full `OpenRA.Test` passed `513/513`.
- G5 persistence: uninterrupted play saved at tick 300 after reservation and
  before placement. The exact load restored queue `328/Defence.GDI`, retained
  actor type `sam`, and selected the same economy anchor/cell `332 / 41,17` at
  the same tick as uninterrupted play. The invalid tick-0 missing-map attempt is
  excluded.
- Reset clean-three and combination: the exact G5 load, combined CNC-41 G4 toxic
  geometry, and combined CNC-41 G7 topology/save scenarios passed. G4 retained
  both original harvesters and processors after all eight raiders; G7 retained
  both harvesters and processors after all six raiders without path spam,
  resource/traffic violations, runtime error, or desync.
- G7 policy-review disposition: the reviewer condition asked whether the fixed
  unreachable MSAM at `91,96` was ineffectively reserved. Source inspection
  confirms `unreachable-domain` rejection occurs in `ClaimRejectionReason`
  before `Fill` calls `assignment.Add` or `reserved.Add`; the actor was correctly
  withheld and never owned by the economy-defense module. No repair is needed.
- Stressed final: reached tick 9000 with both original harvesters and pristine
  refineries, literal local screens before contact, defender recovery, three SAM
  sites, later post-contact unload completions by both measured harvesters, and
  seven of eight raiders destroyed. No safety, traffic, runtime, Lua, or desync
  failure occurred. Its launcher label is rejected as an assertion-harness false
  negative: two unload expressions required `tick=` although the engine logged
  `at tick`, and the recovery expression sampled before the later literal screens.
  The factual narrative records the raw outcomes; the routine policy review's
  evidence condition is resolved by those same post-contact unload and literal-
  screen lines rather than an unchanged rerun.
- Evidence count and budgets: six new counted games bring the total to `72`;
  integrated cycles are `1/3` this RC and `1/12` total. No further product cycle
  was needed. Task-owned debug remains disabled and no raw game artifact is
  tracked.
- Publication: draft PR #92,
  https://github.com/Realpra1/LibertyDawn/pull/92, targets
  `agent/cnc-20260807-bug-polish-02-release`. CI run `31185660304` passed Linux
  .NET 6.0 in 2m14s and Windows .NET 6.0 in 4m29s on the tested product head.
  The final receipt-only commit follows that head and changes no product code.
- Integrated artifacts:
  `analysis/worker-4-cnc42/integrated-rc1/{g5-discovery-run,g5-save-run,g5-load-run2,g4-g7-combined-run,final-stress-run}`
  and factual/policy narratives under
  `analysis/worker-4-cnc42/integrated-rc1/reviews/`.
