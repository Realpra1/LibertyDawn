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
- Status: `Specified`
- Common base branch/SHA: `agent/cnc-20260807-bug-polish-02-release` / `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`
- Task branch: `agent/round-20260807-cnc46-defense-clusters`
- Intended PR base: `agent/cnc-20260807-bug-polish-03-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `0`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260807-bug-polish-03/WORKER-2-CNC-46/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/AUTONOMOUS-CNC-LOGS/20260807-bug-polish-03/WORKER-2-CNC-46`
- Persistent policy scratchpad: `/root/github/LibertyDawn/.agents/references/LIBERTY-DAWN-POLICY-SCRATCHPAD.md` (3,000
  characters maximum; one cross-round serialized writer)
- Policy scratchpad lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/shared-locks`
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
