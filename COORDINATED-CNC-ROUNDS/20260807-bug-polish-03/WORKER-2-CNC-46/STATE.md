# Worker State: CNC-46

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `WORKER-2-CNC-46`
- Task: `CNC-46 — Defense clusters`
- Change category: `AI strategy, construction priority/placement, wall lifecycle, and persisted policy state`
- Balance authority: `Frozen. Authorized only SkyNet-specific AI policy/config needed for the literal active-cluster trigger, at-least-three nearby towers, unlocked-role coverage, local Repair Facility, bounded open wall screen, wall-sale/access checks, and their minimal radii/lease/retry/limit fields. Do not change actor costs, HP, damage, armor, range, speed, power, prerequisites, build times, resource values, or unrelated production/composition tuning. A SkyNet BuildingLimits adjustment for a required local repair facility or literal cluster actor is allowed only if repository evidence shows the existing limit makes the requested live result impossible; document it and leave all actor rules unchanged.`
- Status: `First iteration - testing`
- Common base branch/SHA: `agent/cnc-20260807-bug-polish-02-release` / `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`
- Task branch: `agent/round-20260807-cnc46-defense-clusters`
- Intended PR base: `agent/cnc-20260807-bug-polish-03-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `20`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260807-bug-polish-03/WORKER-2-CNC-46/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/AUTONOMOUS-CNC-LOGS/20260807-bug-polish-03/WORKER-2-CNC-46`
- Persistent policy scratchpad: `/root/github/LibertyDawn/.agents/references/LIBERTY-DAWN-POLICY-SCRATCHPAD.md` (3,000
  characters maximum; one cross-round serialized writer)
- Policy scratchpad lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/shared-locks`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `62` (cycle-1 invalid observer; cycle-2 changed full; cycle-3 changed interrupted at tick 5,000 plus exact-base control full at tick 11,000; cycle-4 initial changed full plus exact-base control natural end near tick 7,746; corrected-fixture changed/control both full at tick 11,000; cycle-5 changed reached tick 10,000 with late fixture Lua fault and control natural-ended after tick 6,001; cycle-6 initial/resource-corrected pairs reached tick 7,000 plus legal-screen and capped single-front changed/control pairs reached tick 5,700; cycle-7 changed/control reached tick 5,700 but changed took a low-power/contention trajectory that never built the Repair Facility; cycle-8 changed/control reached tick 5,700 and proved the full core cluster/ordinary-defense contrast, but the screen was rejected by a neutral fixture rock; cycle-8 valid-screen-fixture changed/control rerun reached tick 5,700 and exposed loss of the only repair footprint to the second cluster tower; cycle-9 initial and corrected-screen changed/control pairs reached tick 5,700, proved protected-site retention and ordinary placement, but did not yet produce one run with both local repair and screen; cycle-10 changed/control reached tick 5,700 cleanly, but the changed run never completed repair and therefore never exercised the smaller-screen fallback; cycle-11 initial and south-facing corrected changed/control pairs reached tick 5,700 cleanly, proved the core cluster twice, and isolated stable base-adjacency rejection of the smallest centered screen; cycle-12 changed/control reached tick 5,700 cleanly, but an incidental post-lease hit on the first added tower legitimately re-anchored before the original screen fallback could be exercised; cycle-12 single-front corrected changed/control reached tick 5,700 cleanly but the protected repair site became unusable after nearby tower placement, so repair and screen remained absent; cycle-13 changed/control reached tick 5,700 cleanly, but reserving every orthogonal approach cell over-constrained local tower placement and left the changed cluster stalled at two towers/no repair/no screen; cycle-14 changed/control reached tick 5,700 cleanly and produced the first strict literal acceptance pass with three towers, every role, local repair, seven-cell open screen, causal sale, and ordinary-defense placement only on changed; post-cycle-14 inward-pressure changed/control reached tick 5,700 cleanly, retained the exact open cluster, and showed a material fixed-window health advantage only on changed; initial separated-front changed/control reached tick 5,700 cleanly but was invalid for the pending case because the pre-lease Orca never hit, while the later direct switch reached three towers but not a live repair before final; corrected separated-front changed/control reached tick7,700 cleanly, proved pending/promotion, then exposed intra-cluster re-anchors and loss of local-repair ownership; cycle-15 corrected separated-front changed/control reached tick7,700 cleanly, retained the promoted anchor and placed a live local repair, but the manager rejected completion because that facility did not overlap the current potential screen; cycle-16 corrected R3 separated-front changed/control reached tick7,700 cleanly, but changed's queued local repair disappeared before placement and no screen was planned; cycle-17 producer-loss changed naturally ended near tick9,543 before the final observer and control reached tick10,200, while both invalidly destroyed two expansion Facts and changed never reacquired repair on the surviving stable Fact; cycle-18 producer-handoff changed reached tick9,200 and control naturally ended around tick7,783, but the fixture destroyed a different Fact than the repair owner, natural combat caused a valid post-lease distant re-anchor, and changed never completed a recovery handoff/local repair/screen; cycle-19 initial changed/control fixtures failed near tick2,150 on a Lua recursive-declaration bug before producer loss; corrected cycle-19 changed/control both reached tick7,000, proved the exact producer-loss handoff, but changed's recovery item disappeared behind a long-lived queue head and never produced local repair/screen; cycle-20 changed/control both reached tick8,500, but changed never queued repair before producer loss, later legitimate configured-tower hits escaped fixture isolation, and no repair/screen completed)
- Terra cycle code reviews: `cycle 5 advisory adopted: restrict causal sales to persisted cluster/legacy provenance while preserving the first-Fact enclosure; review at cycle-review-05/CYCLE-REVIEW.md. Cycle 10 advisory adopted: preserve explicit open-screen line sequencing because stale shared-corner anchors could otherwise pair the two rear flank endpoints into an unintended base-facing closure; review at cycle-review-10/CYCLE-REVIEW.md. Cycle 15 advisory adopted: make repair-site eligibility and live completion use one persisted screen orientation and the same bounded variant-overlap constraint so a placed facility cannot deadlock wall readiness; review at cycle-review-15/CYCLE-REVIEW.md. Cycle 20 advisory rejected: preventing promotion while a live anchor is incomplete contradicts the authoritative lease rule, and cycle20 had no repair reservation/recovery to strand before the valid newer post-lease configured-tower hit; review at cycle-review-20/CYCLE-REVIEW.md.`
- Sol-xhigh policy escalation: `unused (requires at least 10 game tests; one maximum)`
- PR: `#98 — https://github.com/Realpra1/LibertyDawn/pull/98 (draft; temporarily targets the common round-02 release because the intended round-03 release branch does not yet exist)`

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

Literal assigned behavior:

> Defense clusters. Mark an attacked tower as the active cluster and build
> defense near attacked structures. Wall active towers without blocking base
> access, sell walls that block movement/construction. Add at least three nearby
> towers and at least one of each available role, plus a repair pad behind the
> cluster where it can cover some towers/walls. Deprecate old non-MCV straight
> wall construction.

The base behavior reacts to damage by moving a general defense center to the
attacker, while SkyNet's separate wall policy chooses an arbitrary enemy-near
tower and lays one long straight line. The two policies do not create or finish a
coherent strongpoint around the structure under pressure, do not guarantee
counter-role coverage or local repair support, and never remove a wall that later
obstructs traffic or construction.

After this task, a qualifying attack on a SkyNet tower visibly creates one stable
primary cluster at that tower. The live cluster has at least three nearby tower
actors, every currently unlocked/legal anti-infantry, anti-vehicle/ground, and
anti-air role, a locally effective Repair Facility behind it, and a bounded open
wall screen with a working inward lane. Damage to another owned structure causes
the next relevant defense placement to be near that structure rather than near
the attacker. Causally obstructive local AI wall segments are sold minimally.
The old non-MCV long straight tower wall no longer appears; the separate first-
construction-yard enclosure remains unchanged.

Enable the policy for SkyNet only. This is the only profile that enables the old
non-MCV tower-wall behavior at the common base, and the linked later sparse-tower
work is explicitly SkyNet policy. Keep reusable shared fields default-off so the
other CNC bots and every unsupported mod retain control behavior.

## Authoritative behavior

- A qualifying trigger is positive damage from a living enemy to an owned, live,
  configured tower actor. The first qualifying hit establishes the active anchor.
  Repeated hits on it retain it. During a short configured anchor lease/debounce,
  at most one other attacked configured tower is remembered as the pending
  candidate instead of immediately oscillating. When the lease expires or the
  minimum cluster completes, switch only if that valid pending tower was attacked
  more recently; prefer the current anchor on ties. Immediately invalidate and
  re-evaluate on anchor death, capture, ownership loss, disappearance, permanent
  unreachability, or bounded repeated placement infeasibility. There is one active
  construction goal, not several half-built primary clusters.
- A positive enemy attack on any other owned building records that building, not
  the attacker, as a bounded/cooldown-controlled center for the next ordinary
  relevant defense placement. This must not overwrite the active tower anchor,
  duplicate an existing cluster reservation, or repeatedly starve the cluster.
  Preserve the existing combat-defense notifications that other modules consume.
- Nearby means a small configurable radius around the active anchor, bounded by
  legal base adjacency and validated in real games. Count distinct live owned
  configured tower actor IDs. Existing nearby towers count. Pending/queued work
  suppresses duplicates but never satisfies final completion. At least three live
  nearby tower actors are mandatory.
- Role coverage is based on actual target capability and policy configuration,
  not cosmetic type diversity. One multi-role tower may cover several roles, but
  it is still only one of the three actors. A role is required when a configured
  tower capable of that role is faction-legal, unlocked by live prerequisites,
  and not categorically prohibited by a hard actor limit. Temporary cash, low
  power, busy queues, or a temporarily blocked footprint delay readiness; they do
  not make the required role disappear or permit false completion. Final coverage
  requires the live towers to be operational; low power invokes ordinary power
  recovery first.
- A live owned `fix` Repair Facility satisfies the local goal only when its
  existing 7-cell repair aura reaches at least one cluster tower, some screen
  cells lie within that same visible coverage area, it is on the base-facing or
  otherwise safer side of the cluster, and repairable units retain a legal
  approach. A distant facility does not count or suppress the local goal.
  Reuse a qualifying local facility; otherwise serialize one live/queued/reserved
  request. If no safe site exists, keep one bounded retryable goal or re-anchor
  after persistent infeasibility; do not place it exposed merely to tick a box.
  Do not change the existing Repair Facility aura or make walls repairable.
- Replace the SkyNet legacy non-MCV line with a shallow local screen: enemy-facing
  and flank wall cells around the active cluster, deliberately non-collinear as a
  whole and with a base-facing/inward opening. It must not become a closed ring,
  a map-choke project, or a long straight line across arbitrary terrain. Before
  ordering, bounded checks must preserve the construction-yard exit, an inward
  cluster/repair approach, relevant harvester/MCV/squad traffic, and at least one
  useful nearby legal construction footprint. Revalidate after the world changes.
- Track enough local wall purpose/provenance to protect the first construction-
  yard enclosure and unrelated project walls. When a nearby owned wall is the
  causal blocker for a required route or cluster construction footprint, compare
  the bounded result with and without it, sell the minimum useful segment (at
  most one per maintenance decision), and re-evaluate before another sale. Never
  sell enemy/allied walls. On SkyNet there are no field-containment projects; do
  not generalize this task into editing or dismantling another profile's field
  project.
- Essential low-power and missing-refinery recovery may preempt discretionary
  cluster work. Existing opening, first-tower, economy-SAM, smart-economy,
  air-repair, Tiberium-field, and construction-yard-enclosure owners must retain
  their documented priority and must not receive duplicate requests.
- Persist or deterministically reconstruct the anchor, pending candidate,
  lease/cooldowns, current goals/reservations, completion facts, retry state, and
  local wall provenance needed to resume safely. Save/load must neither duplicate
  work nor declare queued/reserved work live. Replays issue no bot decisions and
  must replay the recorded orders without desync.
- Keep all observation, candidate selection, route/build-space checks, and sales
  bounded and deterministic. Policy/config belongs on the owning BaseBuilder
  trait; capability, lifecycle, serialization, geometry, and access invariants
  belong in cohesive code helpers. The existing wall planner is already a mixed
  construction-yard/tower responsibility; split a focused cluster policy/helper
  rather than expanding it into another global planner if that keeps ownership
  clear.

## Forbidden behavior and failure signals

- No active cluster after a qualifying attacked-tower event, anchoring on the
  attacker instead of the attacked structure, or silent anchor oscillation.
- Calling a cluster complete with fewer than three live nearby tower actors, a
  missing unlocked/legal role, a disabled low-power role with no recovery, only
  queued/reserved actors, or a distant Repair Facility.
- Building several partial primary clusters, duplicating tower/facility requests,
  or retaining stale reservations after cancellation, destruction, capture,
  ownership change, infeasible geometry, or reload.
- A closed wall ring, a globally selected choke wall, the legacy non-MCV long
  straight line, blocked Fact/production/harvester/MCV/squad access, a sealed
  repair approach, or consumption of the last useful cluster build footprint.
- Selling a wall merely because it is nearby or inconvenient; selling more than
  the minimum causal segment without rechecking; selling another owner’s wall,
  the protected first-Fact enclosure, or another profile's project wall.
- Redirecting combat squads or the general defense-center interface to the
  attacked structure when their existing contract expects attacker position.
  Cluster construction needs separate state.
- Starving emergency power/refinery recovery, bypassing prerequisites/hard
  limits, perturbing unrelated AI production, changing actor balance, enabling
  the feature for other bots/mods, adding global graph optimization, unbounded
  scans/queues/allocations, nondeterministic iteration, or per-damage/per-tick log
  spam.
- Treating requests, debug messages, wall plans, route estimates, or a won match
  as acceptance without the live player-visible cluster, sale, and usable routes.

## Relevant current implementation and control behavior

- `OpenRA.Mods.Common/Traits/BotModules/BaseBuilderBotModule.cs` owns the shared
  BaseBuilder configuration and implements `IBotRespondToAttack`. At the base,
  any attacked building broadcasts `e.Attacker.Location` through
  `IBotPositionsUpdated`; `DefenseCenter` is saved/loaded, but there is no attacked
  structure identity, cluster lifecycle, or cluster reservation state.
- `OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/BaseBuilderQueueManager.cs`
  owns both building and defense queue selection/placement. Ordinary defense
  placement searches an annulus around `lastUsedDefenseLocation ?? DefenseCenter`
  toward the closest enemy building, so it may chain from an old hotspot and is
  not anchored to the damaged structure. Completed wall actors are handed to the
  wall planner as `LineBuild`; ordinary power, recovery, opening, first-tower,
  economy-SAM, smart-economy, air-repair, field, production, and fraction choices
  all share these queues/cash.
- `OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/BaseBuilderWallPlanner.cs`
  first plans configured construction-yard enclosures, then considers up to four
  unhandled towers and lays the longest placeable enemy-facing straight run (up
  to the wall actor's 15-cell range) using two anchors. It remembers handled
  towers only in memory, performs one 3,000-cell escape flood from near a stable
  Fact, sleeps 500 ticks after failure, and has no wall sale or persisted state.
  A missing path-check start accepts the line by structural fallback. That
  fallback is not strong enough for multi-sided cluster walls.
- `OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/BotWallGeometry.cs` and
  `OpenRA.Test/OpenRA.Mods.Common/BotWallGeometryTest.cs` contain the current pure
  line/facing/escape helpers and focused tests. Extend or replace them only with
  bounded cluster-local invariants; do not revive removed global choke machinery.
- `mods/cnc/rules/ai.yaml` enables `WallTypes: brik`, the five walled defense
  types, and `MaximumWallSegments: 150` only on `BaseBuilderBotModule@skynet`.
  Other profiles only use the separate first-Fact enclosure. SkyNet already has
  high tower limits, adaptive defense types, `fix` in ordinary building policy,
  and a single global `fix` limit; confirm whether that limit prevents the literal
  local facility before changing only that AI limit.
- `mods/cnc/rules/structures.yaml` defines `gtwr` as anti-infantry, `gun` as
  anti-vehicle, `sam` as anti-air, `obli` as ground, and `atwr` as ground+air.
  Their prerequisites span recon/economy tiers. `fix` is a 3x3 Building-queue
  structure requiring `upgrade.economy1`; its existing RepairGen radius is 7
  cells and units need a physical approach. Base buildings/towers receive the
  aura; walls already self-repair and are not RepairGen receivers. `sbag`, `cycl`,
  and `brik` are `Sellable` line-build walls; custom sell value is zero.
- `BuildingRepairBotModule` repairs damaged buildings, `SquadManagerBotModule`
  protects configured assets and also broadcasts attacker position, and
  `CrateCollectorBotModule` demonstrates the ordinary bot `Sell` order. Do not
  conflate those responsibilities with cluster construction.
- History is a warning: commits `2c04c4b036` and `ce671230e9` tried rings,
  turret reservations, choke discovery, and large reachability policy; commit
  `66baf421b3` removed that complexity after poor playtest results and retained the
  current simple straight line. CNC-46 expressly supersedes that non-MCV line,
  but it does not authorize restoring global choke scans or an exact optimizer.

## Likely wrong approaches and challenges

- Reusing `DefenseCenter` for the anchor: other modules intentionally consume the
  attacker location, and the queue manager's cached last placement can keep
  building at the wrong hotspot. Store cluster and attacked-structure placement
  state separately with explicit ownership.
- Resurrecting the historical closed/three-sided ring, choke cache, global terrain
  scan, multi-target graph solver, or exact best fortification. The requested
  primary cluster is local and can be satisfied by a small open screen plus
  bounded route/footprint checks.
- Treating `world.CanPlaceBuilding` as access proof. A legal wall footprint may
  still block a Fact exit, refinery lane, rally path, repair approach, or the only
  future structure site. Conversely, accepting when no flood start is found is
  unsafe for a multi-sided screen.
- Using `LineBuild` for another longest run and merely renaming it a cluster. The
  final wall arrangement must be locally non-collinear, open inward, and visibly
  associated with the active anchor.
- Defining roles only by actor name or forcing one actor type per role. Validate
  configured actors and actual target capability; allow `atwr` to cover ground
  and air while still requiring three actors. Do not infer availability from cash,
  power, idle queues, or a transient footprint.
- Counting globally owned towers/facilities, queued items, reservations, or stale
  actor IDs as completion. Final evidence is live, nearby, owned, operational
  structures after invalidation and reload.
- Letting every damage event enqueue work or switch anchors. Damage callbacks can
  be frequent; debounce/lease, one pending candidate, one cluster reservation,
  and bounded logging are mandatory.
- Solving obstruction by indiscriminate wall sales or resetting all wall state.
  Establish causal before/after improvement, protect the first-Fact perimeter,
  sell one, and reassess. Do not make a wall-sale refund strategy; these walls
  have zero custom sell value.
- Building the Repair Facility on the enemy side, covering only empty wall cells,
  blocking its own entrance, or modifying the aura/repair traits. Site it using
  the existing mechanics and prove units can reach it.
- Raising tower/fix fractions, limits, cash, starting conditions, or actor stats
  until a test passes. Fixtures may accelerate state, but product balance and
  unrelated composition remain frozen.
- Unit-test-only or passive-bot proof, identical happy-path reruns, activation
  logs without final world state, or a reload-only pass.

## Competing systems and ownership

- `BaseBuilderQueueManager` instances for `Building.GDI/Nod` and
  `Defence.GDI/Nod` own the same Facts, queues, placement orders, cash, power
  checks, limits, and failure handling. Cluster requests need explicit one-owner
  serialization across every Fact/queue.
- Opening policy and `BaseBuilderFirstTowerPlanner` can reserve the first defense;
  a nearby first tower may count once live, but cluster logic must not steal or
  duplicate its pending build.
- Low-power and missing-refinery recovery are higher priority. Smart-economy
  refinery/vehicle-factory work, generic production scaling, silo recovery, and
  `TiberiumFieldManager` consume the Building queue and cash. `fix` also has
  ordinary fraction/limit ownership and air-repair capacity may interact with
  repair-building requests.
- `BaseBuilderEconomyDefenseSamPlanner` owns normal SAM production/placement for
  configured Iron Reaper economy coverage. SkyNet does not enable it at base, but
  shared code must preserve its reservation boundary and a future role check must
  not hijack its site.
- `BaseBuilderWallPlanner` owns first-Fact enclosures for all bots and the legacy
  SkyNet tower line. `TiberiumFieldManager` owns field wall placement for Iron
  Reaper before the wall planner hook. Cluster wall provenance and sale rules must
  remain separate from both.
- `BuildingRepairBotModule` orders repairs on damaged structures. `SquadManager`
  orders defenders and updates attacker-centered combat state. `McvManager`,
  rally points, harvesters, production exits, ground squads, transport missions,
  and units seeking `fix` all depend on unobstructed movement.
- Building limits/fractions, unlocked prerequisites, `TechTree`, powered state,
  building adjacency, `BuildingInfluence`, wall `LineBuild`, `Sellable`, and
  locomotor reachability jointly determine whether a request, placement, role,
  or sale is real. Instrument each boundary instead of guessing.

## Cross-worker dependencies

There is no explicit prerequisite and no active CNC-46 branch/PR at selection.
Work from the exact common base and do not absorb unrelated work.

- CNC-52 is a later pending first-Fact-only wall-hole follow-up. CNC-46 owns
  general cluster wall self-blocking/selling, but must preserve the common-base
  first-Fact enclosure behavior and must not implement CNC-52's follow-up. If a
  CNC-52 branch/PR becomes active before publication, inspect only its commits,
  report overlap in `BaseBuilderWallPlanner`/wall geometry/config, and rebase or
  coordinate through the integrator.
- CNC-91 is a later pending SkyNet sparse advanced-guard-tower policy. It is
  secondary map control and must never reserve towers/queues/cash ahead of the
  CNC-46 primary attacked cluster. Do not implement it. If its branch/PR appears,
  inspect only its commits and make the priority boundary explicit.
- CNC-40, CNC-41, CNC-42, and CNC-44 are excluded prior-round work already
  represented by the common base/release selection. Preserve their current code,
  especially adaptive defense accounting, field-project ownership, economy-SAM
  ownership, and aircraft behavior; do not edit their contracts or reopen them.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `/root/github/LibertyDawn/AUTONOMOUS-CNC-LOGS/20260807-bug-polish-03/WORKER-2-CNC-46/spec-policy-review/inputs/NARRATIVE.md`
- Sol-high policy review: `/root/github/LibertyDawn/AUTONOMOUS-CNC-LOGS/20260807-bug-polish-03/WORKER-2-CNC-46/spec-policy-review/POLICY-REVIEW.md`
- Verdict and confidence: `mostly sensible / medium`
- Recommendations adopted as testable hypotheses: `Separate unlocked/legal role eligibility from temporary order readiness; use one short anchor lease plus one pending candidate and immediate invalidation; allow a multi-role tower to cover multiple roles but still require three actors; require local rather than global Repair Facility de-duplication; use an open enemy/flank screen; sell only one causally obstructive segment after a before/after check; keep power/refinery recovery ahead; test two fronts, cramped traffic, recovery contention, counter rotation, destruction/capture, and persistence.`
- Recommendations rejected or deferred, with reason: `The review questioned categorical field-wall protection. SkyNet has no field project on the common base and CNC-41 ownership is out of scope, so this worker must not generalize sales into Iron Reaper field walls; record that future interaction for integration rather than expanding CNC-46. Exact lease/radius numbers and combat thresholds are not adopted from prose alone: choose bounded defaults with code evidence, then validate and revise through matched games without changing balance.`
- Persistent scratchpad update: `Validated regular UTF-8 replacement, 1,251 characters, atomically promoted under the cross-round one-slot policy-scratchpad lock. It records the unvalidated hypotheses for role-complete pressured strongpoints, role eligibility versus readiness, local repair support, and an open wall screen with causal minimal sales.`

## Acceptance and tests

### Literal black-box acceptance

Run a fresh full-engine headless-MAX CNC scenario from an ignored task-local
`.oramap` with an ordinary SkyNet player and an ordinary enemy AI, all normal bot
modules enabled. The focused map may pre-place a normal SkyNet Fact, economy,
power, production, one attackable tower, and unlocked role prerequisites, and may
give the enemy a close mixed force so the event occurs promptly. It must not use a
passive/custom test bot or an isolated manager. Include a damaged non-tower owned
structure and one owned non-enclosure wall segment that is the sole blocker of a
required route or the only safe local `fix` footprint.

With current product code, a positive enemy hit on the tower must make that exact
actor the active anchor. By the recorded judgment tick and before scenario end:

- at least three distinct live owned configured tower actors are within the
  declared cluster radius;
- live operational nearby towers cover every role unlocked/legal in the fixture,
  including anti-infantry, anti-vehicle/ground, and anti-air; any multi-role tower
  is credited only for capabilities it actually has;
- one live owned `fix` is on the safer/base-facing side, its unchanged 7-cell aura
  reaches at least one cluster tower and overlaps part of the screen, and a normal
  repairable unit can approach/use it;
- the wall arrangement is visibly cluster-local, enemy/flank facing, non-
  collinear as a whole, open inward, and not the deprecated arbitrary long line;
- the deliberately causal obstructing wall segment is sold, the named route or
  footprint becomes usable, no protected first-Fact enclosure segment is sold,
  and the Fact exit, harvester/MCV/squad lane, repair approach, and one useful
  nearby build site remain traversable/legal;
- the attacked non-tower structure causes a defense to be placed near that
  structure without changing the active tower anchor; and
- final world-state evidence, replay/benchmark artifacts, and a concise observer
  check show the structures, geometry, sale, access, ticks, and outcome. Requests,
  reservations, or logs without the live outcome do not pass.

Run the exact map/seed/starts/factions/options/actors against the old behavior at
the recorded base SHA as the cycle-1 matched control. The changed side must show
the complete visible cluster/access outcome while the control demonstrably
retains its attacker-centered/legacy-line behavior or fails the cluster contract.

### Focused checks and instrumentation

- Add small pure policy/geometry tests for: qualifying enemy tower damage; one
  active anchor/one pending candidate and lease/tie behavior; death/capture/
  ownership/infeasibility invalidation; attacked-structure placement center;
  nearby distinct actor counting; actual role capability; eligibility versus
  temporary readiness; multi-role credit; local facility/aura/approach checks;
  open-screen/non-collinearity/inward-gap invariants; protected wall cells; causal
  before/after route and footprint improvement; sell-one-and-recheck; reservation
  de-duplication; and save-data round trip/reconstruction. Extend
  `BotWallGeometryTest` only for cohesive geometry; create a focused cluster
  policy test rather than turning that class into a stateful manager test.
- Validate every configured actor/role/wall/facility at rules load: actor exists,
  tower has `Building` plus usable armament/target capability, wall is a sellable
  building/line actor as required, facility has the existing repair-range trait,
  all radii/caps/leases/retries are positive and mutually sane, and default-off
  profiles remain unaffected. Fail actionable bad YAML; do not silently drop a
  malformed role and call the cluster complete.
- Add BotDebug-gated, transition-only diagnostics sufficient to distinguish:
  attack self/attacker/damage; anchor activation/retention/pending/re-anchor and
  reason; eligible roles versus readiness blockers; live/queued/reserved counts;
  chosen actor and queue/reservation owner; candidate placement rejection reason;
  screen cells and protected provenance; route/footprint before/after result;
  sale actor/reason/restored requirement; placement order; live completion; and
  invalidation/reload reconstruction. Include tick, player, actor ID/type, cell,
  goal/role, queue actor/type, and bounded counters. Rate-limit repeated deferrals.
  Remove noisy candidate/per-tick logging before publication; retain only useful
  disabled-by-default transition diagnostics.
- Before tests, capture resolved relevant SkyNet BaseBuilder config from the base
  and changed heads so no hidden actor-stat or unrelated AI-policy delta is
  present. Run `git diff --check`, focused `dotnet test` filters, full
  `dotnet test OpenRA.Test/OpenRA.Test.csproj`, `make check`, `make check-scripts`,
  and `make test`/`./utility.sh cnc --check-yaml`. Reserve `large-build` capacity 1
  for the expensive build/test gate; do not build unsupported mods directly.
- CPU/allocation contract: no per-damage global scan, no per-tick full-world or
  full-map scan, no uncontrolled LINQ/materialization in the bot hot path, no
  unbounded pending candidates/retries/sales, and no RNG or hash iteration that
  changes order. Record MAX benchmark/bot-tick evidence for matched control and
  changed runs. A local maintenance pass must have explicit candidate and flood/
  cell caps and a cooldown; changed throughput must remain within ordinary run
  noise and show no sustained bot-tick spike, GC/allocation growth, stall, or log
  volume growth. Diagnose any repeatable regression rather than raising caps.

### Ordinary and differential games

All games use the full CNC engine, normal production/economy/repair/squad/MCV/
transport modules, explicit headless MAX, isolated support/log/replay/save/
benchmark paths, the shared game lock, and evidence that the intended map, bot
types, factions, teams, starts, options, seed, actors, headless/MAX markers, world
ticks, and final outcome actually occurred.

1. **Cycle-1 matched forced trigger (changed versus base control):** immediately
   after the first product change and focused compile checks, run the literal
   custom map as a paired batch against `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`.
   Failure hypothesis: the
   trigger/queue/placement path does not work in the real engine or normal modules
   steal it. Perturbation: close mixed pressure, all roles unlocked, causal wall
   obstruction. Failure signal: wrong anchor, no live clustered tower, duplicate
   owner, legacy line, stuck queue, no sale, or route loss. Pass evidence: a live
   partial-to-complete changed cluster and final outcomes above, with the control
   behavior clearly different. The first smoke may be easy; do not rerun it
   unchanged after it works.
2. **Recovery/contention pair:** remove/damage power or refinery and keep defense
   and Building queues busy. Failure hypothesis: the cluster starves essential
   recovery or false-completes by declaring roles unavailable. Pass evidence:
   recovery preempts, the role stays pending/required, and the live cluster resumes
   without duplicates after economy/power returns.
3. **Two-front pair:** attack two distant towers alternately, then sustain the
   second front. Failure hypothesis: anchor thrash, tunnel vision, or two partial
   primary goals. Pass evidence: one stable anchor, one bounded pending candidate,
   deterministic switch at the specified boundary, and no duplicate reservations.
4. **Cramped connected and blocked/island topology:** use a narrow CNC map/custom
   fixture and `mods/cnc/maps/island-duel.oramap` or a derived CNC-only focused
   topology. Failure hypothesis: a locally legal wall/pad blocks the only lane or
   path assumptions cross disconnected land. Pass evidence: the minimum causal
   sale/restored lane, no protected enclosure damage, viable cluster on its land
   mass, and no unreachable global search/stall.
5. **Lifecycle/persistence:** destroy or capture the anchor and cancel or destroy
   one queued/placed cluster actor; save mid-goal, reload, then complete from the
   reload and independently from a fresh run. Failure hypothesis: stale IDs,
   duplicate request, false completion, lost provenance, or divergent orders.
   Pass evidence: immediate invalidation/re-anchor, one rebuilt goal, identical
   policy transitions after the boundary, clean replay/no desync. Reload is
   supplementary, never sole acceptance.
6. **Natural ordinary matches:** at least one connected short-start pressure match
   and one long-distance progression/endurance match (Empire Earth is suitable)
   through natural conclusion. Vary seeds/factions/pressure rather than spawn-swap
   copies. Force an attacked-tower event if it is otherwise absent; an unexercised
   trigger does not pass. Show the cluster survives ordinary managers and does not
   materially harm economy/off-front defense.

After each materially judged pair/batch, stage only the authorized logs/manifests/
metrics, run a fresh Commenter, then a fresh routine Policy Reviewer under the
serialized scratchpad workflow exactly as this state requires. Record accepted
and rejected hypotheses before the next code change.

### Old-behavior control and required improvement

Use common base `468ee64f5a0f9a9e19e260e5c5943e6e878f4705` in an isolated control
worktree. Do not retain the deprecated product behavior behind a production
toggle merely for tests. Keep map bytes/checksum, rules/content, factions, bot
personalities, teams, starts, seed, cash/options, preplaced actors, unlocks,
attack timing/composition, exit tick, and measurement window matched; record both
heads and manifests.

For every exercised pair record: qualifying attacks and anchor actor/tick;
activation/re-anchor latency; live/queued/reserved cluster actor counts; eligible,
ready, and live operational roles; time to three towers/role completeness/local
`fix`/screen completion; wall cells built/sold and causal route/footprint result;
anchor/facility survival and damage state; enemy useful damage/kills versus
defense useful damage/kills; breach timing; lost defense/economy/army value;
power/refinery recovery timing; cash spent and queue idle/block time; harvester,
MCV, squad, rally, and repair approach failures; whole-match outcome; bot-tick,
MAX throughput, allocation/GC, fatal, and desync evidence.

The change must decisively satisfy the binary cluster/access contract where the
control does not. In combat pairs it must also show a material local advantage:
for at least two independently stressed matched pairs, either the changed anchor/
protected assets survive the fixed pressure window when the control loses them,
or the changed defense produces a clearly larger useful-damage/breach-delay result
outside observed run noise. It must not repeatedly lose whole-match outcome,
essential recovery, economy/army value, off-front survival, route access, or MAX
throughput. A tie, marginal gain, or activation-only log is presumed a policy or
implementation defect until varied evidence explains it; do not tune balance to
force a win.

### Adversarial cases

After the latest relevant fix and after normal acceptance first passes, obtain at
least three distinct clean full-engine scenarios from this list; rerun affected
ones and restart the three-clean count after a fix:

- **Separated alternating fronts.** Hypothesis: damage spam thrashes the anchor or
  leaves two half-clusters. Perturbation: alternating distant tower attacks then a
  sustained pending-front attack. Failure: repeated switches, two active owners,
  starvation, or ignored valid pending anchor. Pass: one active/one pending,
  deterministic boundary switch, visible completion.
- **Cramped refinery/Fact/production traffic with obstructing wall.** Hypothesis:
  the screen or `fix` consumes the only lane/site, or sale is indiscriminate.
  Perturbation: scarce legal cells and a non-enclosure owned wall that is the sole
  causal blocker. Failure: stuck harvesters/MCV/squads, no `fix`, protected wall
  sale, or multiple needless sales. Pass: one minimum sale restores the named
  route/site, all traffic traverses it, cluster completes, enclosure stays live.
- **Low-power/refinery-loss queue contention.** Hypothesis: temporary unreadiness
  erases a role or cluster work starves survival recovery. Perturbation: destroy
  economy/power during construction and occupy both queues. Failure: false role
  completion, duplicate requests, continued discretionary spend, or no resume.
  Pass: essential recovery first, role remains required, one resumed goal.
- **Counter rotation after unlock.** Hypothesis: three towers exist but actual
  infantry/vehicle/air coverage is absent or powerless. Perturbation: infantry,
  then armor, then aircraft after all corresponding prerequisites unlock. Failure:
  missing/non-operational role or irrelevant cosmetic diversity. Pass: actual
  live capability for every role and useful counter-specific damage.
- **Destruction/capture/cancel/save-load/replay.** Hypothesis: persisted IDs and
  reservations become stale or nondeterministic. Perturbation: invalidate anchor
  and goal around the save boundary. Failure: stale completion, duplicate build,
  wrong owner, lost wall provenance, replay divergence/desync. Pass: clean
  reconstruction/re-anchor, single goals, matching post-boundary behavior, clean
  replay; confirm again from fresh start.
- **CNC island/blocked topology and long natural match.** Hypothesis: local search
  scans unreachable land, cannot find an enemy relation, or blocks the only land-
  mass route; concentrated spend harms progression. Perturbation: Island Duel or
  derived blocked map, then long Empire Earth. Failure: stall/unbounded CPU,
  impossible cross-island plan, route loss, or repeated economy/off-front loss.
  Pass: bounded local cluster, natural outcome, no route/performance regression.

### Final regression

After all fixes, rerun the literal full-engine scenario from a fresh start with
the strongest compatible stress that previously failed (normally mixed-role
pressure plus cramped traffic and a pending second front), using the exact final
commit and a new recorded seed. Require the exact live three-tower/eligible-role/
local-`fix`/open-screen result, a witnessed causal one-wall sale and restored
route/footprint, nearby defense at the attacked non-tower, protected first-Fact
enclosure, no legacy line, normal module contention, clean natural or configured
exit, replay/benchmark/log flush, no fatal/desync, and acceptable MAX performance.
This must be a fresh run, not a reload. Then run at least one real ordinary full
match at headless MAX to natural conclusion and the final focused/full static
gates. Record exact artifact paths, checksums, seed, ticks, actor IDs/cells, final
outcome, and control comparison in the report and handoff.

## Implementation rules

- Do not ask implementation or preference questions. Investigate code, history,
  controls, configs, tests, and evidence; choose the strongest safe option and
  record material assumptions. Stop only this task for a real authority,
  credential, missing-file, unsafe-path, or irreducible blocker.
- Keep responsibilities separate and dependencies explicit. Prefer short,
  cohesive classes and functions; split oversized responsibilities when that
  improves cohesion, testability, or hot-path clarity without unrelated churn.
  Preserve unrelated behavior and user changes.
- Prefer the simplest bounded solution supported by evidence. Use fuzzy
  thresholds and game-sensible rules of thumb; do not solve graph theory or add
  exact optimizers, rigid partitions, or elaborate state machinery unless the
  task and adversarial evidence show that simpler priority, count, distance,
  threat-map, or cooldown rules are insufficient.
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
`/root/github/LibertyDawn/AUTONOMOUS-CNC-LOGS/20260807-bug-polish-03/WORKER-2-CNC-46/cycle-review-05/CYCLE-REVIEW.md`.

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
   Launch a no-history fresh `policy-reviewer` role (Terra 5.6 medium) in the
   foreground while holding the cross-round one-slot `policy-scratchpad` lock.
   Before launch copy the current canonical scratchpad to
   `inputs/POLICY-SCRATCHPAD.md`. Questions embedded in the narrative are the
   worker's questions to this playtester; the job contains no inline context.
5. Read the `POLICY-REVIEW.md` before choosing the next code change. Treat advice
   as hypotheses: record what inspired the next test/change and what was rejected
   with reasons. Never substitute the review for adversarial game evidence.
   Require the role's `POLICY-SCRATCHPAD.md` to be a regular UTF-8 file no longer
   than 3,000 characters, then atomically replace the canonical scratchpad before
   releasing the lock. If validation fails, retain the previous scratchpad.

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
For routing or transport, test both an ordinary connected CNC map and a CNC island
or blocked topology using `mods/cnc/maps/island-duel.oramap` or a CNC-only derived
fixture. Do not build or test Red Alert's Archipelago. If the event does not occur,
change the seed, map, duration, starting actors/resources, bots, or focused setup;
do not pass an unexercised path. Judge every unexpected behavior explicitly as
acceptable or defective.

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
  --lock-dir /root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/locks --resource game --capacity 2 --slots 1 -- COMMAND...
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
| 1 | Initial bounded cluster manager, pure anchor/role policy, local open-screen geometry/wall provenance, queue placement integration, SkyNet-only config, and persistence (uncommitted candidate) | Shared queues may lose cluster ownership, role readiness may false-complete, or the causal wall may prevent local `fix`; mixed close pressure, all role unlocks, protected Fact enclosure, and a deliberately obstructed safe-side footprint | `git diff --check`; focused 22 tests passed; `./utility.sh cnc --check-yaml` passed; changed full-engine fixture seed 46001 reached tick 2,500 then observer-only Lua failure (invalid run, game test 1) | Narrative/policy deferred until a materially judgeable full run | Exact attacked `gtwr@40,31` was hit from tick 103; product activated and requested `fix`, then placement failed at tick 452 without selling the sole wall blocker; later requested/placed `atwr`; diagnostics substituted prefix fields into transition placeholders; benchmark/replay produced, no desync before observer failure | Correct observer without charging a cycle; cycle 2 fixes actionable transition formatting and reruns the literal changed scenario |
| 2 | Correct transition diagnostic formatting; fixture-only safe Lua joining and exact footprint/screen/ordinary-pressure refinements | Actionable logs may expose queue starvation, wall-sale failure, or incomplete final live state under the same mixed-pressure fixture | `git diff --check`; focused 22 tests passed; changed full-engine seed 46001 cleanly reached configured tick 11,000 (game test 2), benchmark/replay present, no fatal/desync | Factual `cycle-02-commenter/NARRATIVE.md`: limited single-run evidence, repair reservation substantively absent, exact final assertion off by one tick. Routine `cycle-02-policy/POLICY-REVIEW.md`: mixed/high; adopted mandatory safer-side local repair reservation/placement with bounded retry/diagnostics and exact harness timing; deferred its Brutalis extension concern as out of CNC-46 scope | Anchor activation is now exact at tick 103; three local towers live by tick 2,501 and four by 6,001, but zero local `fix`, only the original wall at 35,31, no screen, and no completion; later natural combat activated another attacked `gun#302`, showing lifecycle path but invalidating the literal anchor observation after tick 5,747 | Treat as product failure: protect a deterministic repair footprint from cluster towers, let one queued repair goal trigger a causal wall sale, promote the local repair goal above repeatable discretionary contention after essential recovery/opening, rate-limit ordinary damage diagnostics, and run exact matched changed/control artifacts |
| 3 | Repair-footprint protection, post-minimum local-repair queue promotion, queued-goal causal sale, rate-limited ordinary center, persisted diagnostics, strict no-start screen rejection, and byte-identical base-compatible BotDebug attempt | The repair site may still be consumed or unreachable through the protected closed first-Fact enclosure; exact matched base may equal changed; ordinary pressure may rotate cluster facing | Focused 22 tests and CNC YAML passed; exact map SHA `a2905214...9ede`, seed 46001. Changed was externally interrupted at tick 5,000 (game 3); exact-base `468ee64f5a` control cleanly reached 11,000 (game 4), both with benchmark/replay and no fatal/desync | `cycle-03-commenter/NARRATIVE.md` restricted comparison to aligned ticks 0–5,000 and found only a promising 3-vs-2 tower/1-vs-0 ordinary-defense difference. `cycle-03-policy/POLICY-REVIEW.md`: insufficient evidence/high; adopted full common-endpoint rerun plus explicit roles/repair/screen/access/anchor/wall state, rejected unrelated specialist and Brutalis-extension outcomes as CNC-46 evidence | At tick 2,501 changed had three local towers versus control two, but both retained wall 35,31 and had zero `fix`; changed tower at 35,32 still consumed the intended repair footprint. The new AIUtils BotDebug route produced no file logs because it only emits in-game text notifications. Control ended with two local towers, zero fix, one wall. Exact map checksums match. | Cycle 4: log to debug file only when global BotDebug is enabled; select a deterministic path start outside the protected closed enclosure so a real base-to-facility approach can be proven; keep non-tower damage from changing cluster threat orientation; rerun full changed/control pair |
| 4 | Global-BotDebug-gated file diagnostics, deterministic outside-enclosure approach start, and separation of ordinary non-tower damage from cluster threat orientation | The outside path start may still reject the only safe `fix` site, while ordinary structure damage or match termination may prevent an aligned literal observation | Focused 22 tests passed. Initial exact map SHA `a2905214...9ede`, seed 46001: changed full (game 5), control natural end near 7,746 (game 6). Corrected fixture removed a coincident camera blocker, directly attacked `nuk2`, and made each Fact non-demolishable/high-HP; exact changed/control map SHA `5dc24726...f268`, same seed, both cleanly reached 11,000 (games 7–8), benchmark/replay present, no fatal/desync | Initial `cycle-04-commenter/NARRATIVE.md` declared early control invalid; `cycle-04-policy/POLICY-REVIEW.md` mixed/high. Corrected `cycle-04-r2-commenter/NARRATIVE.md` verified equal endpoints but feature failure; `cycle-04-r2-policy/POLICY-REVIEW.md` mixed/high. Adopted one proven causal sale, repair-before-surplus-tower priority, bounded diagnostics/retries, and the no-safe-site retention counter-test. Rejected policy expansion: code already suppresses cluster tower goals at three actors/role coverage; observed extra queues are ordinary owners and must not be cancelled. Validated 988-byte scratchpad promoted under lock | Corrected changed: exact `gtwr#45@40,31` activation tick 103; three towers by 2,501 versus control one, three versus two at 6,001/final; all roles operational. Direct `nuk2#11@25,27` damage recorded tick 803, consumed tick 1,095, and changed had one nearby ordinary defense by 2,501/three by final versus control zero. Both retained wall 35,31 and zero `fix`; changed reserved `fix` tick 1,742 then failed tick 2,068, so removing the extra blocker did not restore the causal site. Source inspection now identifies the likely cause: `World.CanPlaceBuilding(..., toIgnore)` skips the wall in `ActorMap` but still rejects its unchanged `BuildingInfluence`, making the causal after-removal comparison always false for a building blocker. | Cycle 5: use a conservative bounded local footprint predicate only for the hypothetical after-one-wall comparison, retain normal engine placement after actual sale, add transition-only rejection/success diagnostics, rerun corrected literal pair, then obtain required Terra cycle review before any cycle-6 change |
| 5 | Conservative hypothetical after-one-wall footprint legality, normal post-sale engine placement, and rate-limited causal/rejection diagnostics | The generic ignored-actor helper's unchanged building influence may be the sole reason no sale occurred; perturbation kept the corrected sole-blocker geometry and required explicit before/after/approach, exact one-wall removal, live local `fix`, and planned screen. Failure was any rejected causal check, retained blocker, consumed footprint, missing live repair/screen, route loss, or control feature activation | `git diff --check`; focused 22 tests passed with only pre-existing `AircraftHuskSpawnEligibilityTest.cs` CA1825 warning; CNC YAML passed. Exact map SHA `5dc24726...f268`, seed 46001: changed reached recorded tick 10,000 after a tick-6,334 fixture Lua fault (game 9); control naturally ended after the tick-6,001 observation (game 10). Both produced benchmark/replay; no engine exception/desync | `cycle-05-commenter/NARRATIVE.md` found an invalid paired endpoint but verified changed-only local tower growth. `cycle-05-policy/POLICY-REVIEW.md`: mixed/medium; adopted guarded observer plus valid matched rerun and retained causal single-sale proof, deferred reservation-policy redesign until legal terrain distinguishes an unavailable site from queue policy, and rejected unrelated Brutalis/factory-enclosure work. Validated 975-byte scratchpad promoted under lock. Mandatory `cycle-review-05/CYCLE-REVIEW.md` found that any owned non-enclosure wall was sellable; adopted persisted cluster/legacy provenance plus a focused ownership-boundary test. | At tick 126, bounded diagnostic considered 88 cells: 61 adjacency rejects, four coverage rejects, 18 with no candidate wall, and five wall candidates still rejected after hypothetical removal; first reason `terrain@34,30`, zero approach rejects. This disproves building influence as the sole blocker and proves the fixture did not supply a legal safe footprint. No sale/fix/screen occurred; changed had three towers by 2,501 and five by 6,001 versus control one. Direct `nuk2` damage/ordinary-center still worked, but `nuk2` later died and the final observer dereferenced its dead Location, causing the fixture-only fatal Lua. | Cycle 6: snapshot only cluster-local legacy walls outside the protected first-Fact perimeter, accept sales only from that persisted provenance or the wall planner's persisted cluster cells, cover the boundary, correct fixture terrain/observer without actor-rule changes, and rerun a valid sole-blocker pair |
| 6 | Adopted cycle-review wall-provenance boundary: bounded activation snapshot of local legacy walls, persisted cells, wall-planner cluster-cell query, first-Fact override, and pure ownership test | An unrelated owned wall could otherwise become sellable solely by occupying a candidate repair footprint; perturbation uses the corrected causal legacy wall while requiring the protected enclosure and unrelated future walls to stay outside sale ownership | `git diff --check`; focused 23 tests passed with only the pre-existing CA1825 warning; after making constructor-only fields readonly, `make check` passed 0 warnings/errors plus interface checks; `make check-scripts`, Release `make test`, product CNC YAML, and fixture YAML passed. Initial SHA `ad7e9743...d011` games 11–12 tick 7,000; resource-corrected SHA `47ac62ba...6d281` games 13–14 tick 7,000; legal-screen SHA `f99ec8b4...32196` games 15–16 and capped single-front SHA `e41da15c...e8d6` games 17–18 tick 5,700, all seed 46001 with benchmark/replay and no fatal/desync | Initial and corrected review paths as recorded above. `cycle-06-r3-commenter/NARRATIVE.md` and `cycle-06-r3-policy/POLICY-REVIEW.md` drove the stable single-front isolation while preserving required lease promotion for the later explicit two-front test. Capped-pair `cycle-06-r4-commenter/NARRATIVE.md` verified aligned clean execution and a decisive visible three-tower/repair/sale contrast, but correctly rejected acceptance because no open screen was planned. `cycle-06-r4-policy/POLICY-REVIEW.md`: mixed/medium; adopted mandatory concrete screen/access evidence, suppression of a duplicate repair request while the existing local facility remains valid, and a later post-sale assault comparison; rejected waiving the missing screen as instrumentation. Validated 1,174-byte scratchpad promoted under lock. | Resource-corrected run proved exact sale/fix/three-role cluster and ordinary defense, but screen terrain was invalid. The 15-cell-clear rerun again sold the wall and built three role-covering towers, but ordinary expansion produced `gun#293@63,23`; its tick-3,869 hit legally switched the anchor after the 750-tick lease before the queued `fix` completed. The capped rerun held the original anchor through tick 5,501, sold exactly `brik#187@35,31`, built `atwr@35,33`, `atwr@42,36`, and live `fix#269@34,30`, and reported complete at tick 2,126 versus control's one tower/no fix/live wall. However, from tick 2,376 the manager reported `repair-live=0` even while every observer still saw `fix@34,30`, reserved a duplicate `fix` at 5,022, and never exposed the existing facility to wall-screen readiness. | Cycle 7 tests the concrete self-occupancy hypothesis: an already-built Repair Facility must be ignored as the queried facility's own blocker during its approach check. Rerun the exact capped pair; failure is loss of the live facility, duplicate repair reservation, missing screen, or blocked route. |
| 7 | Existing live Repair Facility ignores only its own actor during approach validation; generic ignored-actor naming makes the boundary explicit | The manager's own BuildingInfluence made a newly live `fix` disappear from local completion and caused a duplicate; exact capped rerun should preserve that actor and expose the open-screen planner | `git diff --check`; focused 23 tests passed with only pre-existing CA1825; CNC YAML passed. Exact SHA `e41da15c...e8d6`, seed 46001, changed/control games 19–20 cleanly reached tick 5,700 with replay/benchmark and no fatal/desync, but changed did not build any `fix`, so the self-ignore path was not exercised | `cycle-07-commenter/NARRATIVE.md`: aligned but invalid for acceptance; it found changed sold the wall, expanded from one to four towers, then remained Critical/Low with no repair/screen. `cycle-07-policy/POLICY-REVIEW.md`: unsound/high; adopted suppression of discretionary cluster-tower queues during Low/Critical power and retained repair/screen as mandatory after recovery. Rejected putting repair ahead of emergency power. Rejected its anchor prescription because `gun#305` is a configured tower positively hit at tick 3,760 after the lease, so promotion is explicitly required. Validated 1,133-byte scratchpad promoted under lock. | Changed sold `brik#187` at tick 126, placed `atwr` at 743 and 1,516, reached Critical power by 1,626, nevertheless reserved `gun` at 1,962 and placed it at 2,531, and finished with four towers/no fix/no wall. An incidental `fact#10` hit at 270 occupied the ordinary-center lease, so the scripted `nuk2` hit at 801 was correctly ignored and never exercised the intended ordinary center. Control remained deterministic and passed its narrower contract. | Cycle 8: gate cluster repair/tower requests while power is not Normal, add bounded screen rejection reasons, move scripted `nuk2` damage ahead of incidental base contact, and add normal preplaced power rather than balance overrides. Require power recovery, three operational roles, live repair, no duplicate, and either a planned screen or an actionable exact rejection. |
| 8 | Suppress cluster tower/repair queues unless power is Normal; report exact bounded open-screen rejection reason/cell/facing; fixture preplaced ordinary power and moved the scripted `nuk2` hit to tick 201 | Low-power discretionary requests may starve recovery; with normal power the existing-facility self-ignore should retain one live repair and expose either a real screen or the exact blocker | `git diff --check`; focused 23 tests and CNC/fixture YAML passed. Exact initial SHA `fcfa0277...5c3e`, seed 46001, games 21–22 clean tick 5,700. Valid-screen fixture SHA `25934215...dda5` also passed YAML; changed/control games 23–24 cleanly reached 5,700 in 17.02/17.03 seconds with replay/benchmark and no fatal/desync. Both changed runs failed their full required patterns; both controls passed their narrower contracts. | Initial `cycle-08-commenter/NARRATIVE.md` and `cycle-08-policy/POLICY-REVIEW.md` found a decisive core contrast but required the missing screen; mixed/high. Corrected `cycle-08-r2-commenter/NARRATIVE.md` verified changed kept the exact anchor and three roles but failed repair/screen/final state. `cycle-08-r2-policy/POLICY-REVIEW.md`: mixed/high; adopted bounded repair recovery and mandatory screen, but rejected widening outside safer-side local scope. Source evidence refines its generic release advice: retain the already-proven repair site across transient legality changes and let existing bounded placement failures re-anchor if it becomes permanently impossible. Rejected shared factory-enclosure changes. Latest validated 509-byte scratchpad promoted under lock. | Initial changed sold exactly one causal wall, built three role-covering towers, live `fix`, and two ordinary defenses, while an invalid neutral rock blocked the screen. After exempting all 15 screen cells, changed sold `brik#184` at 126 and placed `atwr@35,33`, but a transiently unavailable repair scan returned no protected footprint before the second tower placement; `atwr@35,31` then consumed the proved `fix@34,30` footprint. Repair placement failed at 1,797 and 4,696; final was three towers/no fix/no screen/no measured ordinary defense versus control two towers/no fix/live blocker. | Cycle 9 persists the active anchor's proved safer-side repair-site/type across transient occupancy and save/load, protects its footprint from tower placement, prefers it when legal, clears it only on live local repair or anchor change, and retains existing bounded infeasibility recovery. Add direct ordinary-placement observation in the ignored fixture, rerun the valid-screen pair, and require repair plus screen. |
| 9 | Persist one proved repair-site/type per active anchor through transient illegality and save/load; reserve its footprint from towers, prefer it once legal, and clear it on live local repair or anchor lifecycle change; fixture directly reports each ordinary `gun` and distance | The only proved `fix@34,30` footprint may still be consumed during a transient block, or repair completion may never expose a concrete screen/ordinary-defense final state | `git diff --check`; focused 24 tests and product/fixture CNC YAML passed. Seed 46001: exact map SHA `3e256f3f...bec5f4` games 25–26 clean tick 5,700; corrected east-screen SHA `5c3686b4...c74ae` games 27–28 clean tick 5,700. Both controls passed; changed runs exited cleanly but missed full assertions. | Corrected factual `cycle-09-commenter/NARRATIVE.md` verifies both pairs and limits strategic claims without economy/loss telemetry. `cycle-09-policy/POLICY-REVIEW.md`: mixed/medium; adopted bounded productive repair retry/deadline, alternate-screen falsification, and stronger outcome instrumentation. Rejected its no-re-anchor prescription because `gun#276` is a configured tower positively hit at 3,161 after the lease, so promotion is authoritative. Validated 1,370-byte scratchpad promoted under lock. | First changed run proved the fix: protected at 126, second tower stayed off the footprint, `fix@34,30` ordered 4,310/live 4,376, three roles complete, two ordinary defenses; screen alone rejected adjacency at 43,27 because the fixture cleared only its prior south facing. After clearing the observed east screen, run two again protected the site but its first placement failed at 1,739; a naturally expanded `gun@64,20` was hit at 3,161 and correctly became the newer anchor before retry completion. Final original vicinity remained three towers/no fix/no screen. Both controls retained one tower/no fix/live blocker; ordinary defenses were 0/1 versus changed 2/3. | Source inspection rejects endpoint-only adjacency because the `LineBuild` resolver would bypass normal rule validation. Cycle 10 retains per-cell placement/adjacency/access checks and tries only a bounded deterministic sequence of smaller front-plus-two-flank open screens down to existing safe minima. Add focused variant-order/non-collinearity coverage and rerun a stable single-front fixture with repair/site proof plus a blocked preferred variant. Then obtain mandatory cycle-10 Terra review. |
| 10 | Add three bounded deterministic open-screen variants (configured 15 cells, then 11 and 7) that retain one front and both flanks; preserve every normal per-cell placement/adjacency/access check and log the accepted variant | The preferred 15-cell screen may be blocked while a smaller non-collinear open screen is legal; exact prior r9 geometry was reused so the former rejection at 43,27 should force `variant=2/3` without weakening normal rules | `git diff --check`; focused policy/geometry tests passed 25/25. Exact map SHA `3e256f3f...bec5f4`, seed 46001; changed/control games 29–30 both exited cleanly at tick 5,700 in 15.02/14.02 seconds with replay/benchmark and no fatal/desync, but both strict final patterns failed. | Corrected factual `cycle-10-commenter/NARRATIVE.md` treats the tick-4,679 switch to configured `gun#287@56,32` as a legitimate post-lease re-anchor and keeps original/new vicinities distinct. `cycle-10-policy/POLICY-REVIEW.md`: mixed/medium; retain the single causal sale, but hypothesize event-sensitive suppression after repeated unchanged repair infeasibility. Validated 1,275-byte scratchpad promoted atomically under the held one-slot lock. Mandatory `cycle-review-10/CYCLE-REVIEW.md` found a valid anchor-sequencing safety defect: after the front line occupies both shared corners, stale-anchor cleanup can pair the two rear flank endpoints into an unintended base-facing closure. Adopted for cycle 11. | Changed anchored exact `gtwr#47` at 101, proved/protected `fix@34,30`, sold only `brik#184@35,31`, reached three live role-covering towers by 1,626, and recorded the `nuk2` ordinary center. The repair reservation failed at 1,731 and remained transiently illegal; no repair or screen completed before a positively hit distant configured gun correctly re-anchored at 4,679. Control ended the fixed vicinity with two towers, no fix, two ordinary guns, and the blocker live. Because wall readiness was never reached, the smaller-screen fallback was not exercised. | Cycle 11 adopts the review: track/issue each accepted screen line explicitly so an occupied shared corner remains a valid LineBuild connector instead of being discarded or cross-paired, and directly test the emitted sequence. Then make repair completion deterministic enough to reach the blocked-preferred/valid-smaller fallback; keep authoritative re-anchor coverage for the separate two-front adversarial case. |
| 11 | Adopt cycle-review-10 sequencing: place both inward flank ends individually, then LineBuild the two front corners so each flank and the front connect without ever pairing the inward ends; persist per-anchor placement mode and place partial/reloaded screen cells conservatively | An occupied shared corner could be discarded as stale and cause the remaining rear endpoints to auto-connect across the inward opening; the exact emitted sequence is simulated, then the prior repair-success fixture should reach wall planning | `git diff --check`; focused policy/geometry tests passed 26/26 with only the pre-existing CA1825 warning. Initial exact SHA `fcfa0277...15c3e` games 31–32 and corrected south-pressure SHA `ef7c761f...a941c` games 33–34 reached tick 5,700 cleanly with replay/benchmark and no fatal/desync; controls passed and changed runs missed only screen/final-wall patterns. | Initial `cycle-11-commenter/NARRATIVE.md` / `cycle-11-policy/POLICY-REVIEW.md` and corrected `cycle-11-r2-commenter/NARRATIVE.md` / `cycle-11-r2-policy/POLICY-REVIEW.md` all verified the decisive core contrast and retained the screen as mandatory; latest policy mixed/high recommends a bounded distinct legal fallback. Both policy roles omitted replacement scratchpads, so canonical policy memory was safely retained unchanged under each serialized lock. | Initial changed completed four towers/local fix/sale but faced east into invalid fixture adjacency. Corrected pressure forced south: exact anchor tick115, sale tick126, two `atwr`s, `fix@34,30`, complete tick1751, final three towers/one fix/three ordinary guns/no blocker versus control one/no fix/two guns/live blocker. Temporary attackers caused early placement rejection, then the smallest centered 7-cell screen stably failed adjacency at `42,34`; nearby defenses do not provide build area and the local `fix` ends at x36, leaving that far endpoint one cell outside wall adjacency. | Cycle 12 adds only two deterministic one-cell lateral translations of the existing smallest open screen after the three centered variants. Require the centered screens to fail, the base-side translation to pass, the four-anchor safe sequence to build exactly seven non-collinear cells with an open inward row, and no access regression. |
| 12 | Append two deterministic one-cell lateral translations of the smallest seven-cell open screen after the three centered variants; retain all normal placement/adjacency/access checks and the safe four-anchor sequence | The centered minimum screen is exactly one cell outside local wall adjacency, while one bounded translation should connect without relaxing construction rules; failure is no `variant=4/5`, an inward closure, route loss, or anything other than seven final walls | Focused policy/geometry tests passed 26/26 and `git diff --check` passed. Exact map SHA `eaceeadd...fdf7`, seed 46001; changed/control games 35–36 reached tick 5,700 cleanly in 16.03/15.02 seconds with replay/benchmark and no fatal/desync. Control passed; changed strict assertions failed before the intended original-anchor fallback boundary. Fixture-only r13 SHA `3999baa0...eabe` retired the forced wave before reinforcement towers existed; games 37–38 reached tick 5,700 cleanly under a serial one-slot fallback after two-slot scheduler starvation. Control passed; changed held the original anchor but still failed repair/screen/final assertions. | `cycle-12-commenter/NARRATIVE.md` classified the changed run as invalid acceptance but verified causal sale, three towers, a temporary local repair, and ordinary defenses. `cycle-12-policy/POLICY-REVIEW.md`: mixed/high; adopted the missing repair/screen blocker and explicit outcome telemetry, rejected immutable-original-anchor advice because authoritative policy requires promotion of a newer valid attacked tower after lease/minimum completion. Reviewer emitted no replacement scratchpad, so canonical memory remained unchanged. Corrected `cycle-12-r2-commenter/NARRATIVE.md` isolated the formerly legal protected repair site becoming unusable after two nearby placements; `cycle-12-r2-policy/POLICY-REVIEW.md` was mixed/high, retained repair/screen as mandatory, and emitted no replacement scratchpad. | Changed anchored `gtwr#47@40,31` at tick115, sold only `brik#185@35,31` at126, then a jeep hit the first placed `atwr#225@35,33` and legitimately re-anchored at1109. It completed three towers plus `fix@28,30` at1751 for the new anchor, then repeatedly rejected that anchor's west-facing screen at32,32; final had three towers/two ordinary guns/no fix/no walls. Control retained one tower/live blocker/two ordinary guns. The pair therefore exercised re-anchor lifecycle, not the lateral fallback. In r13 changed retained `gtwr#47`, sold the wall, placed `atwr@35,33` and `atwr@35,29`, then failed `fix` placement at1759 and repeated bounded site rejection/reservation expiry; final had three towers/two ordinary guns/no fix/no walls. | Cycle 13 reserves the small orthogonal approach perimeter as well as the protected facility footprint from cluster towers, and logs the exact protected-site usability failure. Reject wall restoration and generic reservation renewal; rerun r13 and require the full lateral-screen outcome. |
| 13 | Reserve the protected Repair Facility footprint plus all immediate orthogonal approach cells from cluster-tower placement; report exact protected-site unusability reason | A tower footprint overlapping any immediate approach cell may make the previously proved facility unusable; preserving the entire local perimeter should retain one approach while still allowing the mandatory third tower | `git diff --check`; focused policy/geometry tests passed 27/27 with only the pre-existing CA1825 warning. Exact r13 SHA `3999baa0...eabe`, seed 46001; changed/control games 39–40 reached tick 5,700 cleanly in 13.02/13.01 seconds with replay/benchmark and no fatal/desync. Control passed; changed strict assertions failed. | `cycle-13-commenter/NARRATIVE.md` verified the causal sale and early response but classified changed as failed acceptance. `cycle-13-policy/POLICY-REVIEW.md`: mixed/high; adopted repair as the causal payoff before optional tower work. Rejected restoring a contingency wall because the task requires the sold blocker to remain absent and a bounded open screen; rejected immutable-anchor language because valid post-lease promotion remains authoritative. The reviewer emitted no replacement scratchpad, so canonical memory remained unchanged. Prescribed CLI roles were quota-blocked, so fresh no-history Terra/medium role slots produced the same strict artifacts while the policy lock remained held. | Changed activated `gtwr#47@40,31` at115, protected `fix@34,30`, sold only `brik#185@35,31`, and placed one `atwr@32,31`; the next cluster tower failed at1375 because the 13-cell reservation eliminated every safe local tower candidate. It stayed at two towers/no fix/no screen through the fixture judgment. A distant ordinary `atwr#308@66,29` later became the valid newer anchor at4174 and was captured at4560, exercising invalidation at4626. Control retained its blocker and finished with two towers/no fix/no screen. | Cycle 14 replaces the all-sides perimeter exclusion with one deterministic reachable approach cell/lane and commits the protected local repair as soon as two live towers already cover all required roles. The third tower follows, preserving both the repair payoff and mandatory three-actor completion. |
| 14 | Persist and reserve only one deterministic reachable base-facing repair approach cell with the facility footprint; allow a role-complete two-tower core to qualify for priority local repair while retaining three actors as the completion minimum | Reserving one approach may still let tower placement seal the facility, or earlier repair eligibility may interfere with established queues; the exact r13 pair requires the full final screen/access outcome, not transition logs | `git diff --check`; focused policy/geometry tests passed 27/27 with only pre-existing CA1825. Exact r13 SHA `3999baa0...eabe`, seed 46001; changed/control games 41–42 both passed strict manifests and reached tick 5,700 cleanly in 16.02/15.02 seconds with replay/benchmark and no fatal/desync. Post-fix adversarial 1 used inward-pressure SHA `4de0d237...ea7`, seed 46141; games 43–44 passed at tick 5,700. Initial separated-front SHA `770c028f...e350`, seed46221; games45–46 reached tick5,700 cleanly but changed failed strict behavior. Corrected separated-front R2 SHA `f8189094...0ae`, seed46222; games47–48 reached tick7,700 cleanly, proved pending/promotion, then failed local repair. | Acceptance/inward reviews are recorded above. `cycle-14-two-front-commenter/NARRATIVE.md` correctly found missing pending/promotion/live-repair evidence but called the direct switch a regression. `cycle-14-two-front-policy/POLICY-REVIEW.md` returned revise/high and proposed retaining the anchor through its lease plus persistent repair retry. Rejected the anchor product change: activation tick114 plus lease750 expired at864, while the first qualifying second-front hit was tick1027, so the observed direct switch is authoritative and the fixture never exercised pending. Retained repair persistence as a falsification hypothesis for a longer corrected run. Corrected `cycle-14-two-front-r2-commenter/NARRATIVE.md` verified the controlled distant transfer, four-tower coverage, and missing local repair; `cycle-14-two-front-r2-policy/POLICY-REVIEW.md` returned not approved and required reliable local-repair completion while preserving the causal sale. Neither policy role emitted a replacement scratchpad; canonical memory remained unchanged. Prescribed CLI roles remained quota-blocked; fresh isolated Terra/medium roles produced strict artifacts under the policy lock. | Acceptance changed anchored exact `gtwr#47@40,31` at115, selected `fix@34,30` with persisted approach `33,31`, sold only `brik#185`, placed `atwr@35,33` and `atwr@35,29`, placed live `fix#253` by2126, and planned seven-cell variant4/5 at2949. Final tick5501 had three towers, all roles, one fix, exact seven non-collinear cells, inward opening, two ordinary guns, and no blocker. Control had two towers, no fix/screen, one ordinary gun, and the blocker live. Adversarial1 produced the fixed-window health advantage recorded above. Initial two-front changed switched directly at1027 after lease expiry, then reached three second-front towers by5501 but no live `fix`; a repair goal reserved at4481 and expired at5251. Corrected R2 recorded pending at775/855 and promoted at876, then reserved repair at2798; hits on towers already within that strongpoint caused local re-anchors at2989/3751, cleared cluster ownership of the queued repair, and left an exposed ordinary `fix#327@68,33` while the final active vicinity had four towers/no local repair. | Corrected evidence shows a product lifecycle failure. Cycle15 keeps hits on configured towers already within the active cluster radius as pressure on that same strongpoint instead of oscillating its anchor, and preserves the single serialized reservation across a valid anchor transition so queued cluster work migrates rather than becoming exposed ordinary placement. Rerun corrected separated fronts and require one active/one pending, deterministic distant promotion, stable local anchor, one live local repair, and completion; post-fix adversarial count restarts. |
| 15 | Treat hits on configured towers already inside the active radius as pressure on that strongpoint; preserve one serialized reservation across valid re-anchor/promotion and clear it only when invalidation leaves no anchor | Sustained pressure may still churn the anchor or orphan an in-flight repair; the corrected separated-front pair requires the exact distant pending/promotion boundary, no local oscillation, migrated reservation, live local repair, and completion | `git diff --check`; focused policy/geometry tests passed 28/28 with only pre-existing CA1825. Exact R2 SHA `f8189094...0ae`, seed46223; changed/control games49–50 reached tick7,700 cleanly with replay/benchmark and no fatal/desync. Control passed; changed missed only the completion-log pattern. | `cycle-15-two-front-commenter/NARRATIVE.md` verified the stable promotion, live facility, aligned inputs, and exact completion/status mismatch. `cycle-15-two-front-policy/POLICY-REVIEW.md` found the policy aligned but validation incomplete and required reconciliation plus the broader screen/access gates; no scratchpad was emitted, so canonical memory remained unchanged. Mandatory `cycle-review-15/CYCLE-REVIEW.md` found the exact lifecycle deadlock: placement accepts tower coverage/approach without the screen-overlap constraint that live completion applies. Advisory adopted for cycle16. | Changed kept `gtwr#49@58,31` after promotion at876 with no later anchor transitions, migrated queued work, placed `atwr@60,33`, `atwr@62,30`, and live `fix@51,30` by tick4001, and ended with four towers/one fix/full anchor versus control three towers/no fix/full anchor and six walls. Manager status nevertheless remained `repair-live=0`: the base-nearest site covered towers but not the east-facing potential screen, so no completion/screen occurred. | Cycle16 persists the bounded screen orientation selected with the repair site, makes site search/placement/live validation use the same unchanged-aura overlap across every bounded screen variant, and makes the wall planner consume that orientation. Rerun separated fronts; require completion plus open screen/access before resetting the post-fix adversarial count to 1/3. |
| 16 | Persist the repair site's threat orientation; require candidate, placement, and live-facility validation to share tower coverage, approach, and unchanged-aura overlap for every bounded screen variant; retain the protected site after it becomes live; make wall planning consume the persisted orientation | A site can cover towers yet never overlap the eventual screen, permanently deadlocking completion; the corrected separated-front pair must now choose only a site whose aura overlaps every bounded fallback and then complete a legal open screen after deterministic promotion | `git diff --check`; focused policy/geometry tests passed 28/28 with only pre-existing CA1825. Fixture-only R3 observer correction passed CNC YAML; exact map SHA `bcc24572...50b8d6`, seed46224; changed/control games51–52 reached tick7,700 cleanly with replay/benchmark and no fatal/desync. Control passed; changed missed completion, screen, and final patterns. | `cycle-16-two-front-commenter/NARRATIVE.md` verified stable promotion, the early role-complete advantage, queued repair expiry, and absent final repair/screen while leaving the queue-loss cause unknown. `cycle-16-two-front-policy/POLICY-REVIEW.md` returned mixed/high and prioritized bounded deterministic repair recovery. No replacement scratchpad was emitted, so canonical memory remained unchanged under the serialized lock. Prescribed launchers were quota-blocked; fresh no-history Terra/medium roles produced both strict artifacts. | Changed activated `gtwr#47` at115, recorded pending at775/855, promoted exact `gtwr#49@58,31` at876, never switched again, selected screen-compatible `fix@53,28`, built three role-covering towers, and reserved one `fix` at2783. That item remained queued through4626, then queue ownership disappeared and the reservation expired at5051 without placement; final had four towers/no fix/no screen and the causal wall remained sold. Control completed normally under byte-identical inputs, but also lacked repair/a complete fallback. | Failure; post-fix clean count remains 0/3. Adopt bounded deterministic recovery after a repair queue owner/item disappears and exact queue-loss diagnostics for cycle17. Do not broadly cancel ordinary Defense-queue towers: the fourth tower used a separate queue and did not itself displace the Building-queue repair. Rerun R3 longer with producer/global-fix observation and require recovered repair plus screen. |
| 17 | Diagnose reservation loss by producer/item lifecycle, immediately retain one persisted repair-recovery intent, and restrict its next idle-queue retry to the deterministic stable construction yard | A disappeared queue owner may leave a stale goal or a recovery intent that never reaches an idle eligible producer; R4 destroyed the in-flight producer under a longer endpoint while preserving the original Fact | `git diff --check`; focused policy/geometry tests passed 29/29 with only pre-existing CA1825 after correcting one new style warning; R4 YAML passed, SHA `a32870ee...a3916e`, seed46224. Changed game53 naturally ended near benchmark tick9,543 before the final observer; control game54 reached tick10,200. Both had replay/benchmark and no fatal/desync, but strict manifests failed because the fixture destroyed two Facts rather than one. | `cycle-17-producer-loss-commenter/NARRATIVE.md` verified exact recovery failure and the invalid terminal comparison. `cycle-17-producer-loss-policy/POLICY-REVIEW.md` returned mixed/high: immediately reselect a surviving eligible producer, otherwise persist bounded intent through ordinary producer recovery; correct exact producer targeting/final observation. No scratchpad was emitted; canonical memory was retained. Prescribed launchers were quota-blocked; fresh no-history Terra/medium roles produced strict artifacts under the policy lock. | Changed reserved `fix` on `fact#292@42,16` at2201, the fixture destroyed both expansion Facts at3601, and the manager logged `producer-missing` plus `repair-recovery-pending` at3626. It never reserved on surviving `fact#10@20,30`; status stayed three operational towers/no queued or live repair through9126. Control completed the horizon with no cluster policy. Early changed front had three towers and 312094 health versus control two and257380 at2501, but no final outcome is valid. | Failure; post-fix clean count remains0/3. Stable-producer affinity cannot progress while that Building queue stays continuously occupied. Cycle18 may free it only through one bounded recovery handoff after a delay, with hard exclusions for low-power, missing-refinery, opening, completed items, and any cluster-owned work. Correct the fixture to destroy exactly the reserved producer and emit a synchronized terminal observation. |
| 18 | Permit one persisted repair-recovery handoff behind exactly one unfinished ordinary item on the deterministic stable Building producer; never cancel the head item and exclude opening, low/excess-power, missing-refinery, repeat handoff, and larger-queue states | A continuously occupied stable queue may otherwise never service the retained local-repair goal; R5 attempted to destroy one expansion Fact and required a one-item handoff plus recovered placement/completion | `git diff --check`; focused policy/geometry tests passed 29/29 with only pre-existing CA1825. R5 YAML passed, SHA `ea21482c...ad4c`, seed46224. Changed game55 reached tick9,200; control game56 naturally ended around benchmark tick7,783 although its launcher summary reported5,000. Replay/benchmark existed and neither had fatal/desync, but both strict manifests failed. | `cycle-18-producer-handoff-commenter/NARRATIVE.md` found the pair invalid: the targeted producer and terminal endpoints did not align, while changed's extra towers/health were only progress. `cycle-18-producer-handoff-policy/POLICY-REVIEW.md` returned mixed/high and retained deterministic producer-loss recovery plus a verified common endpoint. Its permanent-anchor recommendation is rejected because the authoritative contract requires a newer valid post-lease attacked tower to replace the current anchor; fixture isolation must prevent unrelated tower hits when testing handoff. No replacement scratchpad was emitted, so canonical memory was retained. Prescribed launchers were quota-blocked; fresh no-history Terra/medium roles produced strict artifacts under the serialized lock. | R5 destroyed `fact@45,21`, not the `fact@23,24` repair owner. Natural combat legitimately switched promoted `gtwr#49` to distant attacked `gun#283` at2545 and invalidated it at3126; its already-ordered `fix@68,28` survived only as a global facility. A later repair on stable `fact#10@20,30` disappeared and timed out at5626, setting recovery pending, but no handoff/reservation/placement followed. Final second-front observation had six towers, full anchor health, zero local fixes, one global fix, zero screen cells, two Facts, and the blocker absent. | Failure; post-fix clean count remains0/3. Cycle19 must make the one-item handoff work for a completed as well as unfinished queue head, add a rate-limited exact blocker diagnostic, and use an isolated fixture that destroys the actual low-ID expansion producer before placement while suppressing unrelated later tower hits. Keep legitimate post-lease promotion behavior unchanged. |
| 19 | Allow the single guarded handoff behind either an unfinished or completed queue head; persist a BotDebug-only cooldown diagnostic naming stable-producer queue depth, head/type/done state, and exact blocker | A completed head may have prevented cycle18 recovery; R6 destroys the exact closest expansion repair owner at2401, then retires emergent combat to isolate the handoff and requires live recovery/screen by6801 | `git diff --check`; focused policy/geometry tests passed29/29 with only pre-existing CA1825; corrected R6 YAML passed, SHA `f883f0ef...182e6`, seed46225. Initial games57–58 failed around tick2,150 on a fixture-only Lua self-reference before producer loss. Corrected games59–60 both reached tick7,000 in about16.0s; control passed, changed missed recovery/completion/screen and hit an unrelated later valid re-anchor. | `cycle-19-producer-handoff-commenter/NARRATIVE.md` verified a real one-tick alternate-producer handoff and the terminal stall. `cycle-19-producer-handoff-policy/POLICY-REVIEW.md` returned mixed/high: make handed-off repair a bounded persistent obligation and try an eligible alternate producer after timeout. Adopt alternate-producer selection; reject permanent-anchor freeze because the authoritative contract permits newer valid post-lease attacked towers. No replacement scratchpad was emitted; canonical memory was retained. Prescribed launchers were quota-blocked; fresh no-history Terra/medium roles produced strict artifacts under the serialized lock. | Changed reserved repair on `fact#202@23,24` at2387; fixture destroyed that exact producer at2401. Loss was detected2501 and recovery handed to `fact#10@20,30` at2502 behind one item. It remained behind `atwr`, then `afld`, disappeared by5143, and the one-shot guard blocked further handoff. A new `fact@64,16` was idle/available but fixed lowest-ID affinity never selected it. Final original second front had three role-covering towers versus control one, but zero fix/screen; changed mean bot tick0.357ms versus control0.371ms. | Failure; post-fix clean count remains0/3. Cycle20 will deterministically choose the eligible construction yard with the shortest matching queue, then actor ID, allowing an idle newly recovered Fact to own the normal retry without canceling or preempting the stuck head. Fixture isolation sweeps must be frequent enough to prevent an unrelated post-lease tower hit. After cycle20 games/reviews, run the mandatory cycle20 code review and publish `First iteration - testing` unless all remaining gates unexpectedly complete. |
| 20 | Select the eligible construction yard with the shortest matching queue, then ActorID, for normal repair recovery and the one bounded handoff; never cancel or preempt queued work | The fixed lowest-ID producer could strand recovery behind unrelated work while an idle recovered Fact exists; R7 tightened combat retirement to 25 ticks and extended the synchronized endpoint to8301 | `git diff --check`; focused29/29 and fixture YAML passed; full530/530, `make check`, `make check-scripts`, Release `make test`, and product CNC YAML passed. R7 SHA `630ca2ab...bcf424`, seed46226; games61–62 both reached tick8500, control passed, changed strict recovery/screen patterns failed. | `cycle-20-producer-handoff-commenter/NARRATIVE.md` classified changed invalid because recovery/screen events were absent and later configured-tower promotion violated the fixture's isolation assertion. `cycle-20-producer-handoff-policy/POLICY-REVIEW.md` returned unsound/high. Adopt its conclusion that local recovery remained incomplete; reject permanent-anchor continuity because the authoritative contract requires promotion after a valid newer post-lease configured-tower hit. No replacement scratchpad was emitted; canonical memory retained. Prescribed launchers were quota-blocked; fresh no-history Terra/medium roles produced strict artifacts under the policy lock. Mandatory `cycle-review-20/CYCLE-REVIEW.md` advised suppressing lease-expiry promotion while an incomplete anchor remains; rejected because that contradicts the authoritative lease rule and no cycle20 repair reservation existed to strand. | Changed reached three role-covering towers at the promoted front by2376, but never reserved repair before the fixture destroyed `fact@23,24` at2401, so the new alternate-producer path was not exercised. An `e3` repeatedly escaped even 25-tick sweeps, causing valid `gtwr#49->gun#336` at4122 and later `atwr#348`; final8301 retained three original-front towers but zero fix/screen. Control had two second-front towers, no fix, and persistent walls. Changed bot/tick means were0.428/0.867ms versus control0.512/1.403ms; no fatal/desync. | Failure; post-fix clean count remains0/3 and the 20-cycle cap is exhausted. Publish safest useful result as `First iteration - testing`, and defer a deterministic producer-loss fixture plus required adversarial/natural/final-regression gates to integration. |

## Handoff receipt

- Proposed status: `First iteration - testing`
- Final branch/head: `agent/round-20260807-cnc46-defense-clusters` / product head `fe63905ced7b11a5e1acbdbba0c6eb31c1e82fdb` (followed only by this handoff-receipt update)
- PR and checks: `Draft #98, https://github.com/Realpra1/LibertyDawn/pull/98; Linux (.NET 6.0) and Windows (.NET 6.0) passed. PR temporarily targets agent/cnc-20260807-bug-polish-02-release because the intended agent/cnc-20260807-bug-polish-03-release branch does not yet exist; retarget/revalidate during integration.`
- Cycles used: `20/20 isolated; 62 full-engine games`
- Acceptance evidence: `Cycle14 exact r13 SHA 3999baa099b6353057d716d27009da7f697a3c8cd5baf5514f0cbcf7fa92eabe, seed46001, games41–42: changed-only exact attacked anchor, three operational role-covering towers, live local fix@34,30 with approach33,31, seven-cell non-collinear inward-open screen, one causal wall sale, and two ordinary defenses; strict changed/control manifests passed.`
- Adversarial evidence: `Cycle14 inward-pressure SHA 4de0d237b9f9b3d0154c5ae18d57886893ac9a3a3563ccaa0b65468281d90ea7, seed46141, games43–44 showed changed anchor 400000/400000 versus control313830/400000 at tick3201 and both strict manifests passed. Later repair-lifecycle changes reset the required clean count; latest count is0/3.`
- Old-behavior control and comparative result: `Exact base468ee64f5a controls used byte-matched maps/seeds/options. Cycle14 control ended with two towers/no fix/no screen/one ordinary defense/blocker live versus changed three towers/live fix/seven-cell screen/two ordinary defenses/blocker sold. Cycle20 control passed at tick8500 with two second-front towers/no fix/persistent walls; changed had three towers but no fix/screen and failed.`
- Match narratives and routine policy-review conclusions: `Fresh factual/policy roles followed every materially judged batch. Latest cycle20 narrative classified changed invalid; policy review unsound/high. Adopted the incomplete producer-loss recovery finding; rejected permanent anchor freeze because valid newer post-lease configured-tower hits must promote. No cycle20 scratchpad replacement emitted.`
- Terra cycle code reviews and dispositions: `Cycle5 provenance advisory adopted; cycle10 safe screen sequencing adopted; cycle15 consistent repair/screen overlap adopted; cycle20 anchor-freeze advisory rejected as conflicting with the authoritative lease rule and unsupported by cycle20 reservation state.`
- Sol-xhigh policy escalation (unused, or test count/path/conclusion): `unused`
- Final regression: `not passed. Cycle20 R7 SHA630ca2abab7a58902560952849420e225847f7d95db67ac9804663192fbcf424 seed46226 reached tick8500 cleanly but changed never reserved/recovered local repair and ended without fix/screen.`
- Error/warning and diagnostic-cleanup result: `git diff --check; focused29/29; full530/530; make check zero warnings/errors; make check-scripts; Release make test; product/fixture CNC YAML all passed. Only the pre-existing focused CA1825 warning appeared. Diagnostics are BotDebug-gated, transition/rate-limited; no fatal/desync/rules/Lua/unhandled error in final pair.`
- Performance/determinism result: `Cycle20 mean bot/tick times0.428/0.867ms changed versus0.512/1.403ms control. Selection is deterministic shortest matching queue then ActorID; bounded local scans/cooldowns retained; no sustained MAX regression observed.`
- Deferred work: `Final Sol-high review blocked on persisting local-repair intent before the first queue reservation, carrying it through contention/producer loss, and proving live fix/open-screen completion. Then run three clean post-fix adversarial cases, low-power/refinery contention, lifecycle/save-load/replay, blocked/island topology, natural short/long matches, and a fresh final regression. Resolve the material CNC-52 BaseBuilderWallPlanner overlap during integration without absorbing its first-Fact-only policy.`
- Known failures/risks: `Cycle20 shortest-queue recovery selector is compile/focused-tested but unexercised because no repair was reserved before producer loss. Post-fix adversarial count0/3; no final regression/natural match/persistence/blocked-topology proof. PR must be retargeted to the eventual round-03 release base. Final Sol-high review verdict blocked.`
- Relevant artifact paths: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260807-bug-polish-03/WORKER-2-CNC-46/REPORT.md; /root/github/LibertyDawn/AUTONOMOUS-CNC-LOGS/20260807-bug-polish-03/WORKER-2-CNC-46/cycle-14-commenter/NARRATIVE.md; cycle-14-inward-commenter/NARRATIVE.md; cycle-20-producer-handoff-commenter/NARRATIVE.md; cycle-20-producer-handoff-policy/POLICY-REVIEW.md; cycle-review-20/CYCLE-REVIEW.md; final-review/FINAL-REVIEW.md; cycle-20-producer-handoff-results/`
