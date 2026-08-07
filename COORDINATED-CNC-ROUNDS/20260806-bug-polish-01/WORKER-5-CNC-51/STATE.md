# Worker State: CNC-51

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `worker-5-cnc-51`
- Task: `CNC-51 — Transport-helicopter unload recovery and threat-safe landing`
- Status: `Complete - testing`
- Common base branch/SHA: `agent/cnc38-early-viki-infantry-rush` / `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
- Task branch: `agent/round-20260806-cnc51-transport-unload`
- Intended PR base: `agent/cnc38-early-viki-infantry-rush`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `14`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260806-bug-polish-01/WORKER-5-CNC-51/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-5-cnc-51`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `51`
- Game-run summary: runs 02-06 invalid fixture; run 07 discovery; run 08 blocker
  correction; run 09 negative control; run 10 accepted pinned-base failure
  reproduction; run 11 cycle-1 changed smoke pass; runs 12-13 invalid stationary-
  threat boundary placements; run 14 direct-threat pass; runs 15-16 invalid
  assault fixtures; run 17 helicopter-assault pass; run 18 heavy-drop diagnostic-
  assertion failure with clean physical outcome; run 19 heavy-drop pass; run 20
  covered-transition failure; run 21 transition physical pass with diagnostic-
  assertion failure; run 22 covered-transition pass; run 23 invalid all-covered
  late-threat fixture; run 24 post-success fixture-observer failure; run 25 late-
  threat replan pass with post-handoff carrier loss; run 26 carrier-recovery and
  reuse pass; run 27 invalid grouped-intent literal fixture; run 28 two-of-three
  sequential fixture; run 29 three-of-three physical exit with mission-2 bounded
  safe recovery; run 30 invalid objective-dependent moving-threat placement; run
  31 invalid late-threat timing after useful unload; runs 32-33 live-replan
  physical passes with queued fixture retirement failure; run 34 clean literal
  three-rescue live-replan pass; runs 35-36 invalid narrow pickup placement, with
  run 36 proving rate-limited diagnostics; run 37 clean Archipelago mixed-vehicle
  pass; run 38 natural endurance pass without observable transport activation;
  run 39 clean observed natural match with two emergent rescues; run 40 pinned-
  base natural control; run 41 clean Release natural matched changed game; run 42
  clean final literal regression; runs 43-45 covered-assembly fallback exposed
  unsafe post-release disposition and an unrelated immediate rescue confounder;
  run 46 clean covered-assembly fallback recovery and idle survival; run 47 clean
  post-fix Archipelago mixed-vehicle regression; run 48 clean post-fix live-threat
  replan, carrier recovery, and ordinary reuse; run 49 clean fresh-process final
  literal three-rescue regression; run 50 clean post-fix Release natural match;
  run 51 clean aircraft-closing-envelope review regression; run 52 strict literal
  harness invalid because mission 3 selected a different real squad objective and
  completed after the tick-2000 assertion, although all three useful physical
  handoffs released safely by tick 3500.
- Sol-xhigh policy escalation: `unused (requires at least 10 game tests; one maximum)`
- PR: `https://github.com/Realpra1/LibertyDawn/pull/81`

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

Three preserved rescue missions timed out while still carrying cargo, and a later
Archipelago game showed Chinooks landing beside an MSAM and enemy Mammoths. The
current managers treat their requested objective or preselected formation cell as
the landing plan, then rely on generic `UnloadCargo`/`Land` fallback. That fallback
can repeatedly prefer a cell occupied by an idle ally and has no knowledge of
passenger exits, mission usefulness, combined landing threats, or other transport
claims.

After this task, rescue, helicopter infantry-assault, and heavy-drop missions will
deterministically choose and retain a complete nearby unload plan: an actually
landable carrier cell, enough non-conflicting passenger exits, a useful ground
handoff, a safe approach selected from the coarse strategic threat map, and a
threat-aware route. A site invalidated in flight will be replanned or explicitly
abandoned for safe holding/withdrawal. The player-visible change is that viable
cargo appears in the world near the objective and resumes its rescue, assault, or
Mammoth-squad purpose instead of remaining aboard until timeout or being delivered
inside known MSAM/Mammoth coverage.

## Authoritative behavior

- Treat the requested destination as an objective area, not a mandatory landing
  cell. Before ordinary unload, perform a bounded, stable-order search for a
  nearby exact cell where the carrier can land against current terrain and all
  actor blockers and where every intended passenger has sufficient current,
  non-conflicting adjacent exit capacity. This applies to rescue, helicopter
  infantry assault, heavy-drop attack, safe abort, and timeout recovery.
- Evaluate the union of the descent/airborne and landed `Ground, Vehicle` exposure
  profiles. Reject an ordinary landing if the carrier cell or any required exit
  cell is within buffered range of any enabled, live enemy weapon that can target
  and deal positive damage during either phase. Do not key this to an actor being
  labeled AA. The combined model must include powered SAMs, MSAMs, both applicable
  Mammoth weapon modes, enemy aircraft or other equivalent weapons, true modified
  (including veterancy) armament range, and a movement margin based on a mobile
  threat's ability to close before the next revalidation. Never apply an Orca
  fly-by/projectile-outrun discount to stopping, descent, landing, or exits.
- Use two planning levels for attack approaches: the bounded coarse live threat
  map selects a safe strategic sector/approach side and candidate landing area;
  `ThreatAwareRoutePlanner` (or the existing cohesive equivalent) selects the
  route to that selected cell. An empty, failed, or least-dangerous route must not
  cause an unconditional final direct move into an unsafe objective. A finite
  least-dangerous transit route remains valid when unavoidable only if the final
  landing/exit plan itself passes ordinary safety.
- Revalidate the plan on the configured bounded mission scan/replan cadence and
  immediately before committing descent/unload. Actor, structure, exit, power,
  weapon, mobile-threat, route, and competing-carrier changes can invalidate it.
  Cancel stale landing/unload intent, release obsolete cell/exit claims, and choose
  the next safe useful plan. If none exists, hold outside known danger, retarget,
  or threat-route to safe staging/assembly; do not hover forever or land beside a
  known threat.
- A rescue site is useful only when its passenger can resume the recorded original
  ground objective from the unload region within a bounded path/time. An infantry
  assault must remain cohesive and reach a valuable live target promptly. A heavy
  drop may spread farther but must remain ground-connected and preserve the
  explicit surviving-Mammoth assault-squad handoff. Safe aborts restore ordinary
  eligibility. Tune mission-specific search/usefulness/cohesion policy in the
  owning CNC AI rules rather than one hidden hardcoded distance.
- Allocate simultaneous carrier and exit neighborhoods deterministically and
  without overlap. A temporarily affected pair may safely wait while others
  proceed, but no pair or mission may starve, leak claims, or monopolize a shared
  carrier/passenger reservation.
- Ordinary descent uses a strict zero-known-applicable-threat rule. A narrow
  damage emergency may be evaluated separately only when carrier loss is imminent,
  no survivable withdrawal exists, a physically complete exit plan exists, and
  unloading gives passengers a demonstrably better outcome. Log it as an unsafe
  emergency, never as safe acceptance, and do not trigger it for incidental damage.
- Add bounded, rate-limited diagnostics for blocked/stalled/unsafe sites, candidate
  rejection, the rejecting threat/weapon and effective range/margin, reservation
  or landing-claim owner, route/strategic-map use, stage/order transitions,
  timeout/withdrawal, physical passenger exit, handoff, and terminal outcome.
- Preserve CNC-21/CNC-25 behavior: four normal rescue mission slots, the exact
  ten-Chinook live/queued/requested cap, shared atomic reservations, ordinary squad
  exclusion, repair/safe idle staging, concurrent distinct Mammoth pickup,
  viable-wave behavior, defended-destination abort, carrier emergency handling,
  post-drop assault adoption, and ordinary behavior once released.

## Forbidden behavior and failure signals

- Reissuing `Unload` at the same blocked current cell until timeout; a carrier
  remaining loaded with no changed plan/order across bounded retries is failure.
- Calling an unload successful because a request, reservation, route, waypoint,
  landing order, empty route, state transition, or log occurred. The intended
  passenger must physically appear in-world at a valid cell and resume the stated
  rescue/assault/handoff outcome.
- Landing or assigning an exit within current applicable coverage of an MSAM,
  Mammoth, SAM, ground cannon, enemy aircraft, or equivalent because it was not
  classified AA, was hidden by a fly-by discount, used only base range, or was
  checked only at the carrier center.
- Treating a least-dangerous route as permission for an unsafe ordinary landing,
  or appending a direct move to the requested target after routing/planning failed.
- Selecting a physically safe but strategically useless cell on the wrong island,
  behind impassable terrain, beyond bounded useful travel, or so dispersed that
  the delivered assault is defeated piecemeal.
- Letting two transports claim the same cell/exit neighborhood, trusting idle
  friendly mobiles to move, ignoring structures/map bounds, or using unstable
  world/RNG iteration as the landing-site tie-breaker.
- Hardcoding `msam`/`htnk` actor lists or weapon names as the threat model. Safety
  must follow live targetability, damage applicability, current armaments and
  conditions so future equivalent weapons work without code edits.
- Full-world/per-weapon scans for every candidate cell or every carrier order,
  per-tick replanning, uncontrolled allocations, route thrashing, and unchanged
  retry log spam. MAX throughput regression above the bound below is failure.
- Weakening shared manual/scripted `Cargo`/`Land` behavior or campaign transports
  as a shortcut unless a generic engine invariant is proven and covered; AI-owned
  planning is the primary responsibility boundary.
- Breaking cap/production cash accounting, repair/staging, route-failure rescue,
  general ground APC behavior, heavy-drop pickup/handoff, or ordinary squad
  ownership. Do not absorb CNC-65/CNC-65A APC composition, specialist-safety, or
  post-unload scope.
- Counting an emergency unload, a reload-only run, a passive/custom bot fixture,
  an untriggered mission, or a game that did not load the intended map/options as
  literal acceptance.

## Relevant current implementation and control behavior

- Pinned control is `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`.
  `TransportManagerBotModule` owns route-failure rescue, instantiates the infantry
  and heavy-drop managers, shares `TransportMissionCoordinator`, scans every 75
  ticks, exposes both carrier and passenger reservations, runs heavy-drop assembly
  before opportunistic rescue/assault, limits committed Chinooks to ten, repairs
  below 50%, and stages idle carriers near the base.
- Rescue `AdvanceTravel` changes to `Unloading` when the carrier is within
  `UnloadRangeCells` (configured/default 4), issues targetless `Unload`, and then
  retries targetless unload only when idle. A distance/speed deadline eventually
  calls `RecoverTimedOutCargo`, which routes toward a random base center and queues
  another targetless unload. The mission retains only its original readonly
  destination; it has no planned landing cell, exit allocation, rejection reason,
  safe hold site, or route-plan revision.
- `InfantryAssaultTransportManager` chooses a target actor and uses its exact cell
  for helicopters, routes there, then targetless-unloads at the carrier's current
  location. Damage immediately starts emergency unload. Its mission destination
  is readonly and it has no threat/site revalidation or explicit helicopter
  landing plan. Ground APC use shares this class and must remain behaviorally
  separate from the new helicopter landing policy.
- `HeavyDropTransportManager` is the strongest existing control. It ranks up to
  twelve target actors/directions, chooses distinct landing cells, checks one
  adjacent exit with `BlockedByActor.Immovable`, routes each carrier with a final
  `Land`, and reviews the destination every 150 ticks. It checks stopping danger
  at the center and individual carrier cell through `SafeIndependentAirThreatAt`,
  but does not evaluate all current mobile blockers, reserve complete exit
  neighborhoods, distinguish the descent/landed target profiles, or bind route
  approach safety and usefulness to each exact plan. Unloading still retries the
  generic targetless `Unload` until cargo disappears or the wave times out.
- `TransportManagerBotModule.IssueRoutedMove` calls
  `AirStateBase.SafeIndependentAirRoute`, logs only a waypoint count, queues the
  returned waypoints, and always queues a direct `Move` to the requested
  destination when the route is empty or does not end there. Heavy-drop adds a
  queued `Land`; rescue and assault do not.
- `AirStateBase.AntiAirProfile` profiles only armaments whose weapon target set
  overlaps `Air`. The independent route/threat helpers scan preferred ground
  enemies, use true `Armament.MaxRange()`, the configured 1.5 range buffer,
  `MobileInfo.Speed` over the 125-tick influence interval, and full stopping
  weight (no Orca fly-by discount). They exclude enemy aircraft through
  `IsPreferredEnemyUnit`, do not test the Chinook's landed `Ground, Vehicle`/Light
  profile or positive damage warheads, do not price exit cells, and rebuild a
  world scan/grid per call. Heavy candidate loops can therefore multiply those
  scans.
- Generic `Aircraft.FindLandingLocation` first tests cells with
  `blockedByMobile: false`, allowing an idle allied mobile to appear acceptable;
  `Land` later checks the chosen cell with normal mobile blocking, enters
  `FlyIdle`, and repeats the same search. This is a concrete explanation for an
  occupied-cell landing hold. `Cargo.ResolveOrder` silently ignores a nonqueued
  unload when `CanUnload` is false. `UnloadCargo` otherwise lands within
  `CargoInfo.LoadRange` (default five cells), chooses a shuffled adjacent subcell,
  and waits ten ticks when exits are transiently blocked; none of these generic
  layers understands strategic threat or other AI mission claims.
- CNC rules place the tunable transport counts, ranges, retry/timeout/cooldown,
  heavy formation/safety, and debug switch under
  `mods/cnc/rules/ai.yaml:TransportManagerBotModule`. Chinook `Cargo` carries
  infantry/vehicles up to weight ten and has 40-tick post-unload delay. Helicopters
  are targetable as `Air` while airborne and `Ground, Vehicle` when landed and
  have Light armor, explaining why a combined phase profile is required.
- Existing automated coverage is supplementary: pure
  `TransportMissionCoordinatorTest`, `InfantryAssaultPolicyTest`,
  `HeavyDropPolicyTest`, `ThreatAwareRoutePlannerTest`, and air-geometry tests.
  There is no direct full-engine regression for occupied unload cells, landing
  threat applicability, plan invalidation, or simultaneous exit allocation.
- Relevant history: CNC-21 branch head `18c8bd360b2054335f430aac44d54b1c98efa69a`
  introduced rescue/cap/routing/repair; CNC-25 branch head
  `8bd9305ff18addb67ef23c9fa22c21993b2dfc89` contains the final concurrent
  Mammoth pickup and explicit assault handoff. The pinned base incorporates their
  observable behavior plus later generic reservation coordination; preserve the
  reports `AUTONOMOUS-CNC-REPORTS/CNC-21.md` and `CNC-25.md` as the control record.

## Likely wrong approaches and challenges

- Fixing only the `FindLandingLocation` idle-mobile mismatch may stop one hold loop
  but cannot choose safe approaches, prove exit capacity, replan live threats, or
  coordinate several carriers.
- Increasing `MissionTimeoutTicks`, `Cargo.LoadRange`, retry frequency, or landing
  radius merely moves the failure and can make a larger unsafe search area.
- Reusing the existing Air-only profile unchanged misses landed ground weapons;
  checking only `Ground` would miss MSAM fire during descent. Use the applicable
  union and positive damage, not either classification alone.
- Adding Mammoth/MSAM special cases, cost heuristics, or raw actor counts will
  drift from weapon/range/condition reality and miss equivalent future threats.
- Evaluating only the intended center, only the eventual carrier cell, or only one
  nominal exit lets formation edges/passengers land in coverage or contention.
- Choosing a safe cell after flying directly to the exact objective exposes the
  approach before local replanning. Strategic sector, route, and exact landing
  choice must be one coherent plan.
- A finite-cost route planner deliberately returns a least-bad path. Do not mistake
  that fallback, a zero-waypoint same-coarse-cell result, or a null squad manager
  for proof that direct final movement/landing is safe.
- Calling `SafeIndependentAirThreatAt` for every candidate/passenger/carrier is a
  scan/allocation multiplier. Build a bounded live threat snapshot/coarse grid per
  player/replan epoch and reuse it across candidate and route evaluation; invalidate
  deliberately rather than every tick.
- Extending the already large 2,959-line `AirStates.cs`, 750-line heavy manager,
  or 584-line transport manager with another monolithic search will worsen
  cohesion. Prefer focused shared carrier-threat and unload-plan responsibilities,
  leaving mission lifecycle/handoff in their current owners. Do not over-refactor
  unrelated air combat.
- A cell can be mechanically landable but useless. Euclidean closeness alone fails
  on Archipelago, cliffs, walls, map edges, and isolated exit regions; mission-
  specific connectivity/time/cohesion is part of candidate validity.
- Changing generic `Cargo` exit shuffling, campaign reinforcement landing, or
  manual deploy semantics would broaden risk across shared engine consumers. If a
  minimal generic invariant is unavoidable, isolate it and prove existing CNC
  scripted/manual behavior; do not build/test unsupported mods except shared
  compilation required by the normal solution.
- Replanning without stable tie-breaks, hysteresis/claims, and stale-order
  cancellation can oscillate or send all carriers to the same newly free cell.
- Treating any damage as emergency permission weakens the ordinary rule into the
  original blunder. Keep imminent-loss/no-withdrawal/better-passenger-outcome as a
  separately measured hypothesis.
- Save/load persistence of in-flight transport missions remains deferred from
  CNC-21 and is not authorized here. A reload may be supplementary diagnosis but
  cannot expand scope or count as sole acceptance.

## Competing systems and ownership

- `TransportMissionCoordinator` is the single owner of atomic carrier/passenger
  reservations across heavy drop, rescue, and infantry assault. Heavy drop gets
  first assembly opportunity; rescue can use up to four active mission slots;
  assault advances while rescue creation wins that scan. Landing/exit claims must
  extend this coordination without creating a second contradictory ledger.
- `UnitBuilderBotModule` owns production queues and cash requests. `tran` is
  `ExternallyManagedTypes`; the transport module alone requests it while counting
  live, queued, and already requested carriers against ten. Several missions may
  consume the same available fleet and cannot create parallel production policy.
- `SquadManagerBotModule` excludes `tran`, consults
  `IBotTransportReservations`/`IBotUnitReservations`, removes reserved passengers
  from ordinary squads, explicitly adopts successful Mammoths into one assault,
  and restores abort survivors. Its air manager also owns the existing coarse
  threat/routing configuration. The landing planner may consume a cohesive threat
  snapshot but must not transfer mission lifecycle or handoff ownership into air
  combat squad state.
- `CaptureManagerBotModule`, `CovertHarassmentBotModule`,
  `EarlyInfantryRushBotModule`, `CrateCollectorBotModule`,
  `StealthTankSquadBotModule`, `EconomyArtilleryBotModule`,
  `RedTiberiumBombBotModule`, and `HarvesterBotModule` issue orders to or reserve
  overlapping Engineers, Commandos, infantry, vehicles, harvesters, targets, or
  queues through the generic reservation seams. At least one contention game must
  make relevant ordinary modules active while a transport owns its actors and
  prove they neither steal nor strand them.
- `Passenger`/`Cargo` maintain their own boarding weight reservations and may
  cancel/lock carrier activities. `Land`, `Aircraft`, `ActorMap`, passenger
  locomotors, structures, and mobile blockers own mechanical cell legality. Query
  these owners; do not duplicate terrain/occupancy truth in policy code.
- Idle transport service competes with missions for `Move`, `Repair`, and staging
  orders. It must ignore mission/landing-plan-owned carriers. Damage response can
  compete with descent/unload and must cancel/release the old plan before recovery.
- Enemy movement, power, ownership, death/capture, disabled attack traits, weapon
  range modifiers, and new structures change threat truth. The threat snapshot
  owns only a bounded current planning view, never permanent truth.
- Manual orders, Lua/scripted reinforcements, campaign cargo, paradrops, and APC
  unloading share generic engine traits but are not AI transport-manager missions.
  Preserve their semantics. CNC-65/CNC-65A remain the future owners of APC-specific
  composition, specialist protection, and normal-squad post-unload behavior.
- Tunable search radii, scan/replan/hold/terminal timing, cohesion/usefulness bounds,
  and optional bounded debug policy belong in CNC `TransportManagerBotModule` YAML.
  Weapon applicability, deterministic candidate/claim invariants, threat snapshot,
  route/landing-plan validity, and state transitions belong in cohesive code. Do
  not duplicate knobs per bot personality.

## Cross-worker dependencies

- Common and intended PR base is pinned to
  `agent/cnc38-early-viki-infantry-rush` at
  `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`; do not silently rebase to the moving
  local checkout or `bleed`.
- Preserve completed CNC-21 (`18c8bd360b2054335f430aac44d54b1c98efa69a`)
  and CNC-25 (`8bd9305ff18addb67ef23c9fa22c21993b2dfc89`) behavior. Their remote task
  branch commits may be inspected as history; their reports and the CNC-25
  Archipelago evidence named there are the factual compatibility contract.
- CNC-39 and CNC-39A are active round tasks around Engineer/Commando behavior.
  Their branches were both still exactly at the common base at spec time, so no
  implementation dependency exists yet. Before publication inspect commits—not
  worker specs—on `agent/round-20260806-cnc39-engineer-correction` and
  `agent/round-20260806-cnc39a-engineer-commando` if they advance. Material overlap
  exists if they alter `CaptureManagerBotModule`, generic unit reservations,
  `SquadManagerBotModule`, Engineer/Commando eligibility, or
  `InfantryAssaultTransportManager`; reconcile ownership and rerun contention plus
  helicopter-assault handoff tests.
- CNC-43 and CNC-43A were also at the common base and have no stated functional
  dependency. Monitor their branches only if commits touch shared transport,
  weapon targetability/damage, `AirStates.cs`, the threat map, or common CNC AI
  rules. Do not absorb their MCV/flame-tank flavor/balance work.
- Later CNC-65/CNC-65A are explicitly out of scope and must not be implemented.
  Record any APC-specific issue discovered here in the task report only.
- Highest merge-conflict risk is shared
  `TransportManagerBotModule.cs`, `InfantryAssaultTransportManager.cs`,
  `HeavyDropTransportManager.cs`, `SquadManagerBotModule.cs`, `AirStates.cs`, and
  `mods/cnc/rules/ai.yaml`. Keep the new responsibility narrow to reduce release
  integration conflicts.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-5-cnc-51/spec-policy/inputs/NARRATIVE.md`
- Sol-high policy review: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-5-cnc-51/spec-policy/POLICY-REVIEW.md`
- Verdict and confidence: `mostly sensible; medium confidence`
- Recommendations adopted as testable hypotheses: strict zero-current-applicable-
  threat ordinary landing; separately identified imminent-loss emergency;
  mission-specific ground connectivity, post-unload travel time, cohesion, and
  target relevance; explicit bounded retry exhaustion; widest safe formation that
  remains useful; matched metrics for physical unload, survival, useful action,
  delay, and terminal outcome. The review directly inspired weak-threat,
  all-covered-then-depowered, wrong-island, late blocker/threat, dispersed infantry,
  and damaged-carrier withdrawal/emergency adversarial games below.
- Recommendations rejected or deferred, with reason: no material-damage threshold
  or weak-weapon exemption for ordinary landing—the literal task requires every
  live applicable weapon, and current live state avoids stale-memory overreach.
  A narrow emergency remains a hypothesis but cannot count as safe acceptance.
  Indefinite geometric search and unconditional wide dispersion are rejected as
  unbounded/useless; policy is bounded and mission-specific. Save/load persistence
  remains CNC-21 deferred scope and no reload-only proof is accepted.

## Acceptance and tests

### Literal black-box acceptance

Run a fresh full-engine CNC headless-MAX scenario, derived from a validated
packaged connected CNC map or the repository Archipelago map packaged with a
verified root `map.yaml`, with ordinary real AI players and all normal modules
enabled. Give one ordinary AI three independently reserved rescue missions whose
Chinooks each physically load a vehicle passenger. After departure, place a
friendly mobile on each recorded requested destination, use dense friendly actors
and a structure to block some nearby cells/exits, leave three distinct nearby
carrier-and-exit plans, and cover only the direct approach/requested side with a
live MSAM plus a live enemy Mammoth. At least one plan must be near the map edge.

Acceptance requires all three carriers to use a demonstrably evaluated coarse
strategic sector and threat-aware routed approach, select stable distinct cells
outside combined MSAM/Mammoth descent/landing/exit coverage, revalidate before
descent, land, and physically unload their intended vehicles before their original
mission deadlines. Each passenger must appear in the world at a valid adjacent
cell, receive/resume movement toward its recorded original destination, and the
transport coordinator must release the exact carrier/passenger ownership. No
carrier may land at the occupied requested cell, enter known applicable range,
repeat unchanged unload until timeout, retain cargo, leak claims/reservations, or
be stolen by another ordinary module. Prove map checksum/title, CNC mod, factions,
bots, seed, starts, options, headless MAX markers, actor IDs/types/owners, mission
IDs, requested/selected cells, blocker/threat IDs and weapons, route/strategic-use
fields, advanced ticks, physical exit/handoff, cargo count, and terminal result.

### Focused checks and instrumentation

- Before code, preserve a control artifact for the occupied-cell scenario at the
  pinned base and record the precise observed branch: request rejected, landing
  held, unload silently rejected, exit blocked, route direct, or timeout. Do not
  assume the preserved anecdote is the only cause.
- Add narrow deterministic tests for candidate ordering and mission usefulness:
  occupied exact cell with one safe fallback; all cells blocked; structure/mobile
  blockers; map edge; infantry versus Mammoth exit locomotors; connected versus
  wrong-island cells; near/medium/deliberately-too-far candidates; simultaneous
  non-overlapping carrier/exit allocation; stale claim release; stable actor/cell
  tie-breaking independent of enumeration order.
- Add threat-applicability tests against the carrier's descent plus landed profile:
  powered SAM/MSAM; Mammoth missiles and `120mmDual`; an applicable enemy-aircraft
  ground weapon; an irrelevant/zero-damage/disabled/paused/dead/non-enemy weapon;
  Light-armor positive damage; weapon/warhead target restrictions; modified
  veterancy range; configured buffer; mobile actual movement margin; threat on an
  exit but not center; and proof that stopping weight receives no fly-by discount.
- Add route/terminal-policy tests: safe side selected when one approach is covered;
  empty/same-coarse-cell versus null/failed route distinguished; no direct unsafe
  append; late invalidation cancels stale landing; all sites unsafe chooses bounded
  hold/withdraw/retarget; claims released on death/capture/abort; ordinary versus
  imminent-loss emergency remains explicit and never reports `safe`.
- Keep tests at the narrowest public/internal seam; pure policy tests supplement
  but do not replace full-engine games. Run focused filters such as
  `dotnet test OpenRA.Test/OpenRA.Test.csproj --configuration Debug --nologo
  -p:TargetPlatform=linux-x64 --filter 'FullyQualifiedName~Transport|FullyQualifiedName~AirThreat|FullyQualifiedName~ThreatAwareRoute'`,
  then the full test project, `make check`, `make check-scripts` when a Lua fixture
  is used, and `make test`. Wrap large builds/checks with resource `large-build`,
  capacity one. Fix task-relevant warnings/errors; do not silently suppress them.
- Required retained bounded diagnostic fields: tick; player/bot; mission purpose
  and mission/wave ID; carrier and passenger IDs/types; lifecycle stage and
  previous/new state; requested objective, strategic cell/approach side, selected
  carrier and exit cells, plan revision/age; candidate rejection category; exact
  blocker or claim owner; exact threat actor/weapon, enabled phase, modified range,
  buffer/movement margin and measured distance; threat snapshot epoch; strategic
  map evaluated/used, route result/waypoint/exposure; queued/canceled order; retry
  and deadline; cargo before/after; physical exit; handoff/restore; terminal safe,
  emergency, timeout, withdraw, failure, or success outcome.
- Log the first rejection and material reason/owner/plan change immediately, then
  rate-limit identical unchanged retries per mission/plan to at most the bounded
  replan cadence. Keep actionable warnings for impossible/invalid configuration or
  missing planning owners; do not catch-and-substitute success. Remove candidate
  dumps, per-cell/per-tick traces, and temporary fixture instrumentation before PR;
  retain only the concise `DebugLogging`-gated lifecycle/rejection/terminal events
  needed to diagnose future reports.
- Instrument/measure snapshot builds, candidates evaluated, threat comparisons,
  routes, replans, and claims. One player/replan epoch should reuse a bounded
  threat/coarse-map snapshot across carriers and candidates; no candidate may
  trigger another full `World.Actors` scan. Compare 20k+ matched MAX ticks and GC/
  process metrics to control over repeated runs. Investigate and correct over 10%
  median ticks-per-second regression, unbounded allocation growth, or planning
  counts exceeding configured candidates × threats × replan epochs.

### Ordinary and differential games

Use `launch-ai-parallel.py` manifests and isolated support/log/replay/benchmark
paths. Create focused fixtures only by copying a validated CNC map: connected
tests should start from `mods/cnc/maps/Empire-Earth.oramap`; blocked/island tests
may package `mods/cnc/maps/archipelago/` only after proving the archive contains a
root `map.yaml`. Never use the stale local `TibTest.oramap`. Keep ordinary real AI
players and every normal module enabled from test 1; fixture actors/scripts may
accelerate the event but may not replace the AI or transport manager.

1. **Pre-change control reproduction (no product-code cycle).** Failure
   hypothesis: the anecdote is caused by a different gate than landing-cell
   occupancy. Perturbation: one real-AI rescue on a connected focused map, exact
   requested cell occupied after cargo loads, one obvious safe adjacent fallback,
   no enemy threat. Failure signal: event never creates/loads, or evidence shows a
   different blocker/reservation/order path. Pass evidence: the pinned control
   reaches the loaded landing/unload path and visibly holds/retries/times out while
   the fallback remains usable. Correct the fixture/evidence before implementation
   if it does not exercise the defect.
2. **Cycle-1 matched smoke pair—the first behavioral test after the first product
   change.** Use two game slots for changed versus same-build feature-disabled
   control when available, otherwise the pinned base build. Failure hypothesis:
   the new planner activates but generic landing/unload still targets the occupied
   cell or never exits cargo. Perturbation: exactly the control setup above with a
   short safe fallback and ordinary AI. Failure signal: no plan distinction,
   unchanged unload retry, cargo aboard at deadline, invalid map/bot/module, or no
   useful passenger movement. Pass evidence: changed carrier selects the fallback,
   lands, passenger physically exits and resumes the recorded objective, ownership
   releases; matched control reproduces the stall. Immediately move to harder
   geometry after one pass—do not repeat this cheese setup.
3. **Matched literal contention pair.** Failure hypothesis: several missions race
   to the same fallback or exit and one transient ally recreates the stall.
   Perturbation: the literal three-rescue dense-friendly/structure/map-edge setup,
   with normal squad, capture/harassment/production/repair modules active. Failure
   signal: overlapping claims, stolen orders, any retained cargo/timeout, invalid
   edge cell, or one transport's completion starving another. Pass evidence: all
   three distinct physical unloads, resumed passenger movement, exact releases,
   and materially better completion/time-to-action than control.
4. **Connected helicopter-assault approach pair.** Failure hypothesis: site safety
   is local-only and the final direct segment still crosses the covered approach.
   Perturbation: force the configured helicopter infantry strategy for an ordinary
   eligible bot; put a powered SAM/MSAM and Mammoth on only the direct approach,
   with a safe strategic sector and useful cohesive landing on the other side;
   block the original target cell in flight. Failure signal: direct flight, known
   coverage on carrier/exit, dispersed/idle passengers, route/strategic flag false
   despite the geometry, or control parity. Pass evidence: coarse safe side and
   threat-aware waypoints materially differ from control, every intended survivor
   exits, remains cohesive, and attacks/captures/demolishes or otherwise makes
   prompt useful progress against the recorded target.
5. **Archipelago heavy-drop differential.** Failure hypothesis: the shared fix
   breaks CNC-25 formation, Mammoth connectivity, or handoff and still ignores a
   threat at the edge/exit. Perturbation: eight-to-ten paired carriers/Mammoths,
   distinct concurrent pickup, target near island boundary, one direct approach
   covered, and a mobile Mammoth plus MSAM arriving after departure. Failure
   signal: pickup serialization/regression, wrong-island cell, overlapping exit,
   unsafe descent, obsolete order continuing, cargo retained, `adopted` mismatch,
   or dropped Mammoths idle. Pass evidence: live replan to connected distinct safe
   cells/approach (or safe assembly return if no plan), physical vehicle exits,
   viable survivors adopted into one assault and making useful target progress,
   with cap/reservations intact and a decisive safety/completion gain over control.
6. **All-covered transition/recovery.** Failure hypothesis: strict safety becomes
   infinite preservation or plan oscillation. Perturbation: cover every useful
   site with live mobile/static threats, later depower/remove exactly one; in a
   separate run destroy/block the only safe exit or recovery asset. Failure signal:
   unsafe ordinary unload, repeated plan churn, airborne holding beyond terminal
   bounds, leaked claim, or no response to the opening. Pass evidence: safe hold or
   routed restage/return, bounded explicit terminal state, prompt deterministic use
   of the newly safe useful site when it appears, physical unload/release, and
   rate-limited diagnostics. Test the damaged-carrier imminent-loss branch here;
   label any unsafe emergency separately and compare passenger/carrier outcome.
7. **Ordinary endurance/natural game.** Failure hypothesis: frequent snapshot or
   claim work degrades MAX, creates false rescue work, or displaces ordinary
   strategy. Perturbation: at least one real connected full match and one
   Archipelago/blocked match with normal resources/starts/bots to natural game
   over; no preloaded cheese for at least the connected run. Failure signal:
   crash/desync, false transport flood, cap/queue/cash regression, idle carriers or
   units, persistent claims, task-relevant strategic loss, or >10% median MAX
   throughput regression. Pass evidence: intended missions that arise complete or
   terminate honestly, ordinary openings/combat/economy progress, natural outcome,
   no task regression, and bounded performance/allocation counts.

For every run record the hypothesis, changed dimension, exact failure signal and
physical pass evidence before launch. Prove title/checksum, CNC mod/content, bot
types, factions, seed, starts, options including `gamespeed max`, fixture actors,
normal modules, headless/MAX markers, world-tick progress, flushed logs/replay/
benchmark, and natural/configured terminal marker. An unexercised transport path is
invalid; alter the seed/layout/assets rather than passing it.

### Old-behavior control and required improvement

- Preferred control is a same-build CNC rules/fixture override that disables only
  the new landing planner while leaving mission selection, map, content and all
  normal modules identical. If retaining such a product toggle harms ownership,
  use a clean isolated worktree/build at exact SHA
  `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`. Never compare unrelated bot
  personalities as the old AI.
- For every matched pair pin and record map artifact checksum, mod/content checksum,
  fixture revision, factions, bot types, seed, player slots/starts, lobby options,
  initial actors/resources/health/power, threat timing, exit blockers, game build
  SHA/toggle, and exit tick/timeout. Run control/changed concurrently when reliable;
  judge each artifact independently.
- Primary measures: missions created/loaded; ordinary safe plans selected;
  requested-cell versus selected-cell distance; route exposure/strategic-side use;
  plan invalidations/replans; time from loaded to physical final passenger exit;
  completed intended passengers / loaded passengers; carrier/cargo survival and
  HP; minimum effective range margin at carrier and exits; unchanged retry/hold
  ticks; reservation/claim duration/leaks; time to resumed objective/useful attack;
  heavy-drop `adopted`/abort `restored`; target damage/objective completion; and MAX
  ticks/sec/allocation/GC signal.
- In the deterministic occupied-cell fixture, changed must complete every intended
  unload before the original deadline while the reproduced control stalls or is
  materially slower; three-of-three changed versus preserved three timeouts is the
  literal target. In covered-approach scenarios, changed must have zero ordinary
  landings/exits inside current applicable coverage and materially higher useful
  passenger/carrier survival or completion. A planner log without physical outcome
  is zero success.
- For clear/uncontested routes, changed should preserve control completion and
  handoff without material delay, loss, or match regression. For natural matches,
  no >10% repeat-median MAX throughput decline is accepted without correction and
  a concrete task-specific explanation. A non-strategic win difference alone is
  not decisive, but repeated parity/marginal gain/loss in deliberately exercised
  blocked/threat scenarios is strong evidence of a defect or bad policy and must
  be investigated, not explained away by activation logs.

### Adversarial cases

After the latest relevant fix, obtain at least three distinct clean full-engine,
ordinary-real-AI adversarial scenarios before final regression. A failure and fix
restart this count for affected scenarios. Use these stronger cases; each records
the four required evidence fields:

1. **Dense simultaneous occupancy and map edge.** Failure hypothesis: exact-cell
   recovery ignores current mobile blockers/structures or overlapping exits.
   Perturbation: three rescues plus a helicopter infantry assault, dense friendlies,
   structure footprint, map boundary, target occupied in flight, and ordinary
   managers competing for passengers/orders. Failure signal: same fallback claim,
   out-of-bounds/unenterable exit, unchanged retry/timeout, starvation or stolen
   actor. Pass: every viable pair gets a stable distinct plan/physical exit/useful
   handoff; any nonviable pair explicitly restages/releases without affecting the
   others.
2. **One-sided and newly arriving combined threat.** Failure hypothesis: only
   static Air-class threats or the initial route are represented. Perturbation:
   direct sector covered by powered SAM/MSAM; Mammoth moves into cannon/missile
   range after route issue; optionally an applicable enemy ground-attack aircraft;
   one alternate safe approach remains. Failure signal: stale landing continues,
   carrier/exit enters buffered true range, fly-by discount appears, or strategic
   and routed approach are indistinguishable from direct control. Pass: exact
   threat/weapon rejection, stale order canceled, safe-side strategic cell plus
   routed waypoints, useful unload and survival.
3. **Archipelago connectivity and vehicle unload.** Failure hypothesis: geometric
   nearest safety selects the wrong island or `BlockedByActor.Immovable` misses the
   transient vehicle-exit contention seen in CNC-37/CNC-25. Perturbation: Mammoth
   and lighter vehicle passengers, island/blocked topology, edge landing,
   simultaneous carriers, dense transient mobiles, safe cells with deliberately
   different ground connectivity. Failure signal: repeated `Unload` with passenger
   aboard, stranded/idle passenger, pickup/handoff regression, or wrong-island
   plan. Pass: correct connected useful cells, current exit allocation, physical
   vehicle exits, assault adoption/ordinary restoration and no unchanged retry
   spam.
4. **No-safe-site, power transition, and missing recovery asset.** Failure
   hypothesis: zero-threat policy deadlocks or emergency damage bypasses safety.
   Perturbation: all sites covered, threat later depowered/destroyed in one run;
   in another, repair/staging asset or selected exit is destroyed and a loaded
   carrier is damaged. Failure signal: ordinary unsafe unload, incidental-damage
   emergency, endless holding, oscillation, old claim/order retained, cargo death
   without honest terminal evidence. Pass: safe bounded withdrawal/restage/retarget
   or prompt use of the real opening; emergency only under the stated imminent-loss
   conditions and only if matched evidence improves passenger outcome.
5. **Weak-applicable-weapon counterexample.** Failure hypothesis from policy
   review: strict ordinary avoidance could make the AI strategically inert.
   Perturbation: a weak but genuinely applicable live weapon shadows an otherwise
   poor target while a farther zero-threat useful cell exists. Failure signal:
   indefinite hold or mission irrelevance. Pass: strict policy chooses the farther
   connected/cohesive useful cell within bounded time. If no such policy can
   outperform control, stop and document the literal-policy conflict rather than
   silently adding a damage threshold.

The difficulty ladder therefore changes event timing, post-departure state,
connected/island geometry, resources/available repair assets, one versus many
carriers, infantry versus heavy vehicles, enemy pressure/weapon type/power,
competing modules, and match duration. Save/load is not a required ladder rung
because these mission lifecycles are not persisted and persistence is explicitly
deferred; never use a reload as acceptance.

### Final regression

From a fresh process—not a save—rerun the literal three-rescue scenario after the
last fix with the strongest compatible stress retained: dense friendlies and a
structure, one map-edge candidate, exact targets blocked only after departure, an
MSAM on the direct approach, and a moving Mammoth that compromises one initially
selected exit so a live replan is mandatory. All normal AI modules remain active.
Require all three physical safe unloads, resumed recorded ground objectives,
released carrier/passenger/landing claims, no unchanged retries/timeouts, and exact
diagnostic proof that both strategic-map selection and threat-aware routing shaped
the approach/landing area.

Then, if no clean post-fix natural game already exists, run one connected ordinary
headless-MAX match to natural conclusion and one Archipelago blocked-topology run
long enough to exercise transport. Prove no regression to cap, repair/staging,
ordinary rescue, helicopter assault, heavy-drop concurrent pickup, defended-site
abort, Mammoth squad adoption, normal squad/production/economy progress, or MAX
performance. Re-run focused/full checks and required GitHub Linux/Windows CI on
the published head. Preserve only ignored artifact paths/checksums/seeds and
concise conclusions in this state/report; remove temporary maps, noisy diagnostics,
raw logs/replays/saves/build output from Git.

## Implementation rules

Implementation/publication plan: (1) reproduce and instrument the control failure;
(2) establish a focused shared current carrier-threat snapshot and deterministic
unload-plan/claim seam without broad air-combat or generic cargo churn; (3) make
each helicopter mission retain/revalidate/cancel a plan while preserving its own
lifecycle and handoff; (4) put mission policy bounds in CNC AI YAML and keep
diagnostics bounded; (5) run the matched cycle-1 engine pair, then climb through
contention, approach, Archipelago, transition and natural games; (6) remove noisy
instrumentation, write the complete report, commit only scoped source/tests/config,
push the task branch, open one PR to the recorded base, and wait for all checks.

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
   output directory as `inputs/NARRATIVE.md`. Write a strict JSON job there with
   exactly the absolute `design_reference`, staged `narrative`, and `output` paths;
   output must end in `POLICY-REVIEW.md`. Launch a no-history fresh
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
  --lock-dir /root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks --resource game --capacity 2 --slots 1 -- COMMAND...
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

Pre-cycle control evidence: pinned-base run 02 loaded the intended Empire Earth4
fixture, CNC, Cabal/SkyNet bots, MAX/headless automation, and advanced to tick
4200 with benchmark/replay artifacts. It was invalid because the original
friendly `mtnk` blocker was recruited and moved from `28,20` to `28,19`; the
passenger reached the requested cell, so the rescue path did not activate. The
fixture was corrected without product code by using the ordinary manager-excluded
mobile `truck`. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/pinned-control-run-02/`.

Pinned-base run 03 exposed that `truck` is crushable by the moving Medium Tank;
the blocker died by the first tick-500 status sample and the fixture's dead-actor
location probe caused a Lua error. No product code changed. The blocker was
restored to a non-crushable `mtnk`, with only that fixture blocker kept stopped so
ordinary squad recruitment cannot move it. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/pinned-control-run-03/`.

Pinned-base run 04 proved that a scripted `Stop` does not suppress the engine's
friendly-mobile nudge: the live blocker again moved to `28,19` and the passenger
occupied `28,20`. The corrected fixture grants only the blocker the engine-owned
`Mobile.ImmovableCondition`; passenger/carrier and all managers remain ordinary.
Evidence: `AUTONOMOUS-CNC-LOGS/cnc51-control/pinned-control-run-04/`.

Pinned-base run 05 held the condition-backed blocker at `28,20`, but the
passenger stopped at `27,20`; the manager correctly ignores blocked intent already
inside its four-cell destination tolerance. The corrected connected-map fixture
adds a small wall enclosure so `CompleteDestinationBlocked` occurs outside that
tolerance. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/pinned-control-run-05/`.

Pinned-base run 06 proved the enclosure (`23,20` versus `32,20`) but exposed that
Lua `Actor.Move` bypasses `Mobile.ResolveOrder` and therefore never records the
move intent rescue detection consumes. The corrected discovery fixture reduces
the real Cabal squad threshold to one so the normal squad manager issues the
grouped `AttackMove`; no product code changed. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/pinned-control-run-06/`.

Pinned-base discovery run 07 used Cabal's real grouped order and confirmed
destination `33,30`, mission 1 (`tran 554`/`mtnk 553`), physical load, blocker-free
unload, resumed movement, and release. The final control now creates an immovable-
condition friendly `harv` at exact `33,30` only after physical load. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/pinned-control-run-07/`.

Pinned-base run 08 proved `ImmovableCondition` blocks nudge but not a Harvester's
own manager-issued movement; the blocker left `33,30`. The condition now also
drives `Mobile.PauseOnCondition`, retaining a live mobile actor-map blocker while
preventing self movement. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/pinned-control-run-08/`.

Pinned-base negative control run 09 kept the requested blocker at `33,30`, yet
the old AI unloaded because it commits within four cells and its current unload
cell was `30,30`. The final reproduction preserves `33,30` occupancy and adds a
second paused mobile at actual `30,30`, leaving adjacent fallback cells clear.
Evidence: `AUTONOMOUS-CNC-LOGS/cnc51-control/pinned-control-run-09/`.

Pinned-base control run 10 reproduced the precise failure at SHA
`09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`, seed `510051`, and fixture checksum
`72931e7603be21ce21c09a3a7bcece5836e1cf6f0ffcc31214af4f96a6526854`.
Cabal confirmed bot-owned route failure to `33,30`, created mission 1 for
`tran 554`/`mtnk 553`, and physically loaded cargo. Paused friendly mobiles stayed
at current unload cell `30,30` and requested cell `33,30`; cargo remained aboard
at ticks 1000/2000 with the carrier at `33,30`. The mission logged timeout while
loaded and only physically exited during return-to-base recovery after the
original deadline. This identifies the control branch as permissive targetless
landing search selecting an occupied current cell, followed by strict `Land`
holding, rather than requested-cell occupancy alone. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/pinned-control-run-10/`.

Late-threat run 23 proved the revision-1 carrier was invalidated while the loaded
helicopter was in flight, but the fixture's Mammoth placed directly on that cell
covered the complete bounded assault search. The manager correctly held, then
returned all three passengers to the safe assembly region. This is not evidence
of alternate-route behavior, so the fixture was narrowed to a shorter-range bike
approaching from the north; no product code changed after the run. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-07-run-23/`.

Late-threat run 24 narrowed the new threat to a bike at `53,1`. Snapshot 525
rejected revision-1 carrier `53,7` for `BikeRockets`, selected revision-2 carrier
`55,8` with three distinct exits, immediately issued its two-waypoint replacement
route, and physically handed off all three passengers. The artifact is invalid as
a clean game because the bike then destroyed the carrier and the fixture's
tick-1000 observer read the dead actor's `Location`, causing a fatal Lua error.
The next cycle changes only the observer to represent a dead carrier safely.
Evidence: `AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-07-run-24/`.

Late-threat run 25 repeated seed `510056` with map SHA
`75cf809324dbecdabaa3382518b1816f6a008765eb5f04ebdb21fec2c802076d`.
It cleanly reached tick 2200 at 168.981 ticks/s: revision 1 at `53,7` was
invalidated in flight by the tick-500 bike, snapshot 525 selected revision 2 at
`55,8`, immediately routed two replacement waypoints, and handed off all three
passengers. All three remained in world at the planned cells through tick 2000.
The empty carrier died before tick 1000, however, after the assault mission had
already released it and generic staging routed it away; later rescue scans had no
healthy empty transport and emitted repeated diagnostics. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-08-run-25/`.

Carrier-recovery run 26 repeated the identical seed and map after retaining the
assault mission through an empty-carrier return. The handoff log recorded the
authoritative passenger IDs/cells, then the planner selected assembly carrier cell
`28,30` at snapshot 675 with five threats and routed four waypoints. The carrier
was alive at `31,30` with cargo zero at tick 1000, reached `28,30`, and only then
released its mission/claims. It was subsequently reused to load and safely recover
the blocked APC before tick 2000. The unavailable-transport diagnostic emitted
only the first rate-limited pair during contention. Run 26 passed at tick 2200,
156.933 ticks/s, with replay/benchmarks. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-09-run-26/`.

Literal-contention discovery run 27 spawned all three enclosed tanks together.
The real Cabal squad manager grouped them and only the leader retained the move
intent consumed by rescue detection: mission 1 loaded, selected occupied-cell
fallback `32,20`/exit `33,19`, physically handed off, and released, while the
other two tanks reached their enclosure walls without producing independent
rescue missions. This is invalid as a three-rescue scenario. The fixture will
spawn each subsequent rescue only after the prior passenger loads so each receives
an independent normal squad order; no product code changed. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-10-run-27/`.

Literal-contention run 28 sequenced the second tank after the first physical load,
which produced independent normal route failure and mission 2. Missions 1 and 2
both planned distinct useful exits, physically handed off, and released without
timeout. The transport manager legitimately reused carrier 1; because the load
callback was attached to the carrier rather than resolved from the entered
passenger, it attributed passenger 2 to rescue 1, did not spawn rescue 3, and did
not create the second requested-cell blocker. This remains a fixture failure, not
a product failure. Cycle 11 will resolve load identity from the passenger, delay
idle staging so each nearby carrier participates, and spawn the moving Mammoth
after mission 2 has an initial plan. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-10-run-28/`.

Literal-contention run 29 corrected passenger identity and exercised all three
ordinary rescue missions. Missions 1 and 3 made useful exact handoffs, including
the map-edge exit at `1,80`; mission 2 selected revision 1 at `32,50`, then the
late moving Mammoth invalidated it and caused immediate replacement routes for
revisions 2 and 3. The fixture left that Mammoth permanently covering the entire
bounded useful region, so the manager truthfully held and then completed its
bounded safe-recovery unload at `13,7`. All three passengers physically exited,
but this is not literal acceptance because mission 2 did not resume its original
objective. The next fixture-only iteration retains the mandatory live replan and
then removes the crossing threat, leaving a useful zero-threat alternative.
Evidence: `AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-11-run-29/`.

Literal-contention run 30 physically completed three useful rescues, but the real
squad manager selected `51,8` rather than the fixture's nearby `33,50` objective
for mission 2. The fixed Mammoth crossed `33,50`, retired, and never invalidated
mission 2's revision-1 plan at `51,8`. The batch regex was therefore too weak and
the apparent pass is rejected. The next fixture-only iteration places the Mammoth
relative to the loaded carrier late in its route and explicitly requires a
Mammoth-named plan revision before useful handoff. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-11-run-30-valid/`.

Literal-contention run 31 restored mission 2's nearby objective and all three
passengers physically exited usefully, but the carrier completed its exact unload
before the relative Mammoth appeared. The late spawn then killed the already
handed-off passenger, so the tick-3500 survival assertion also failed. This is a
fixture timing failure rather than a stale-unload product failure. The next
fixture-only run returns to the proven post-plan 100-tick timing and covers both
ordinary objective clusters observed in runs 29-31, then retires both mobile
threats after revalidation. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-11-run-31/`.

Literal-contention run 32 finally forced the objective-independent live replan:
mission 2 revision 1 at `51,8` was rejected for Mammoth `638` weapon
`120mmDual`, revision 2 selected `49,4`/exit `48,4`, and a five-waypoint route
immediately replaced the stale route. After further moving-threat pressure the
mission used another safe connected exact plan at `53,2`/exit `54,2`; missions
1-3 all physically exited, reported useful handoffs, and their passengers were
alive in world at tick 2000. This is not yet a clean artifact because Lua
`Destroy()` queued behind the alternate Mammoth's bot movement, so the asserted
retirement was not immediate, and the tick-3500 assertion rejected mission 1's
ordinary post-handoff combat death. The fixture will stop both threats before
destroying them and will assert survival at the completed tick-2000 handoff
horizon. Evidence: `AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-11-run-32/`.

Literal-contention run 33 repeated the nearby-objective branch with stronger
revision evidence: mission 2 routed revisions 2 and 3 away from both Mammoth
weapon profiles, held rather than using a subsequently covered plan, then
reacquired the original useful `32,50`/`33,49` plan after the threat window. All
three physical useful handoffs completed. `Stop()` still did not make queued
`Destroy()` authoritative against new bot orders; the alternate Mammoth survived,
moved into rescue 1's post-handoff area, and killed its carrier/passenger before
tick 2000. The final fixture repair uses immediate health `Kill()` for both
temporary threats. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-11-run-33/`.

Literal-contention run 34 passed at tick 5200 in 28.049 seconds
(185.323 valid ticks/s), seed `510057`, map SHA
`34f96775d113714607fbcb97977fd7b586d4af002c6f119d29a0b906a685c8f9`.
All three normal rescue missions loaded independently after persistent route
failure and physically handed off at useful exact exits, including map-edge
`1,80`. Mission 2 revision 1 (`32,50`/`33,49`) was invalidated by Mammoth
`638`/`120mmDual`; revisions 2 and 3 immediately issued replacement routes,
then the carrier held while the region remained covered. After both moving
threats were authoritatively dead, it reacquired the connected original region
and completed. At tick 2000 all three passengers and carriers were alive, cargo
was zero, and passengers had resumed movement. No timeout, safe-recovery, fatal,
or desync pattern occurred. The isolated factual narrative confirms the narrow
behavioral pass while correctly withholding a winner/control comparison. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-11-run-34/` and
`analysis/worker-5-cnc-51/cycle-11-literal-comment/NARRATIVE.md`.

Archipelago run 35 used the real map and ordinary IronReaper/Skynet bots, but the
selected lower-left pickup island exposed only one of four distinct
Mammoth-passable pickup cells. No wave was created, so this is invalid vehicle
unload evidence. It also exposed a real diagnostic defect: unchanged
`rejected wave assembly` and later `no undefended drop site` messages repeated on
every review interval. Cycle 12 rate-limits unchanged pre-wave heavy-drop
diagnostics with the configured eight-scan bound and moves the fixture to a wider
spawn landmass plus a unique undefended enemy helipad on the opposite edge.
Evidence: `AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-11-run-35-archipelago/`.

Post-fix Archipelago run 36 moved the pickup near spawn 1 but still found only
one of four distinct Mammoth-passable pickup cells, so it remains invalid unload
evidence. The product fix behaved as intended: the identical assembly diagnostic
appeared only 12 times across tick 7000 instead of on every 75-tick review, with
new occurrences separated by the configured eight-scan window. The next
fixture-only iteration uses four guaranteed multiplayer-spawn land areas as
independent pickup regions and adds transient friendly harvester occupancy around
the unique edge-island helipad target. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-12-run-36-archipelago/`.

Archipelago run 37 passed at tick 7000 in 25.024 seconds (279.672 valid
ticks/s), seed `510058`, map SHA
`cf425f8cdb77e1df9f7a21982e65210af82adbd4841c25e552c5518d876e9e8a`.
Four simultaneous mixed passengers (`htnk,mtnk,htnk,mtnk`) on four guaranteed
land regions received distinct pickup cells, loaded 4/4, and received distinct
edge-island carrier/exit plans at `159,159`/`158,158`, `156,159`/`155,158`,
`159,156`/`158,155`, and `156,156`/`155,155`. Routes used 4/4/7/5
threat-aware waypoints. All four physically exited, the heavy handoff adopted
4/4, and the isolated helipad fell from 60000 to 26600 HP by tick 2000 and was
dead by tick 3500. All four carriers/passengers remained alive with zero cargo.
The released carriers then performed three ordinary follow-on rescue load/exits,
showing reservation release and competing-manager reuse. No abort, safe return,
hold, travel failure, fatal, or desync occurred. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-12-run-37-archipelago/`.

Ordinary-endurance run 38 is predeclared on the stock connected two-player
Badland Ridges map with default resources, unmodified starts, and ordinary
IronReaper/Skynet bots. Hypothesis: shared plan snapshots or released claims can
create false transport work, idle assets, or material MAX slowdown during a full
economy/combat progression. The only changed dimension from an ordinary match is
diagnostic collection. Failure is crash/desync, transport diagnostic flood,
stalled ordinary production/combat, leaked mission state, timeout, or no natural
outcome; task paths that arise must complete or terminate honestly. Pass requires
a natural result, normal opening/economy/combat progress, bounded transport work,
and throughput within the accepted control bound. Seed `510059`, opposing distant
starts, normal cash, normal modules, and `gamespeed max` are pinned before launch.

Run 38 reached natural game over at tick 35000 in 183.169 seconds (191.077
ticks/s), with stock-map SHA
`7311ef9aa55fe0c5968ec6f411590a33c6665783c816c1f9fe79c73dd49246d8`,
normal IronReaper/Skynet progression, and no crash/desync/fatal pattern. It is a
clean ordinary endurance result but not task acceptance because default-disabled
manager diagnostics made transport activation unobservable. Run 39 preserves the
same stock actors, terrain, resources, starts, bots, and normal modules, changing
only `TransportManagerBotModule.DebugLogging` through a packaged rules override.
It pins seed `510060`, package SHA
`a131db97ce1f5471baeed9bc7d323e41e6b51eceb39f431b85a57d36d8656190`,
and requires an enabled heavy-drop diagnostic plus at least one transport-manager
diagnostic before accepting the natural outcome. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-12-run-38-natural/`.

Run 39 passed naturally with SkyNet defeating IronReaper (replay metadata final
tick 39350) in 145.147 seconds at 241.125 reported ticks/s. It produced two
emergent ordinary rescues without preloaded actors: `e1` mission 1 chose
`94,21`/exit `94,22` at snapshot 19050 and physically handed off usefully;
`ftnk` mission 2 chose `96,96`/exit `95,96` at snapshot 32700 and did likewise.
Both released promptly, and the carriers returned to ordinary threat-routed
staging. There was no timeout, claim leak, unsafe hold, crash, fatal, or desync.
Run 40 is the exact-SHA `09ccdac3...` isolated control using the identical
package, seed, bots, starts, options, and diagnostic-only override. Its failure
signals are control launch drift, missing natural result, or an inconclusive
throughput comparison; its behavioral output will be compared without requiring
the old controller to emit changed-only exact-plan diagnostics. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-12-run-39-natural-observed/`.

Pinned-base run 40 loaded the identical package/seed/options from detached SHA
`09ccdac3...`, naturally produced the same SkyNet-over-IronReaper outcome at replay
tick 31340, and created one late light-tank rescue before game over. The old
controller did not record a physical completion. Its 516.642 reported ticks/s is
not a valid comparison to run 39's 241.125 because run 39 used a Debug build while
the isolated control used Release; differing final ticks also make whole-run
throughput secondary. Run 41 will rebuild the changed tree in Release and repeat
the identical seed/package. Acceptance requires no greater than 10% Release
throughput regression after accounting for its longer natural simulation, plus
the changed exact physical outcomes. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/pinned-control-run-40-natural-observed/`.

Release run 41 passed naturally with the same SkyNet-over-IronReaper outcome at
replay tick 77614 and 550.807 reported ticks/s, 6.6% above the pinned control's
516.642 ticks/s rather than regressing. Two late emergent SkyNet rescues loaded
under a live SAM-covered objective. Each held safely, then used the bounded
timeout branch to select a threat-routed safe recovery plan, physically exited
with cargo zero, and released; neither falsely claimed useful objective recovery.
IronReaper separately made bounded transport requests for stranded units. There
was no crash, desync, claim leak, unchanged retry, or unsafe unload. The different
natural duration is ordinary bot nondeterminism, so strategic parity is the shared
winner while throughput is judged by the reported Release rate and benchmark
artifacts. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-12-run-41-natural-release/`.

Final-regression run 42 is predeclared as a fresh-process rerun of the strongest
literal three-rescue artifact, seed `510057`, map SHA
`34f96775d113714607fbcb97977fd7b586d4af002c6f119d29a0b906a685c8f9`.
It retains three independent normal rescues, dense mobile and structure exit
contention, map-edge objective `3,80`, blockers arriving only after departure, an
MSAM on the direct approach, and moving Mammoths that must invalidate mission 2's
live route. Failure is fewer than 3/3 physical useful exits, an unchanged plan,
unsafe recovery/timeout, stale route continuation, leaked cargo/claims, dead
passenger at the tick-2000 outcome horizon, or missing normal-module evidence.
Pass requires the configured strategic/threat route logs, live revision 2 or
later, all three recorded objectives resumed, physical cargo-zero handoffs, and
clean tick-5200 termination.

Final-regression run 42 passed at tick 5200 in 17.019 seconds (305.456 valid
ticks/s) from a fresh process with the pinned map checksum. Missions 1-3 loaded
independently and physically handed off 3/3 with useful-rescue outcomes and cargo
zero. Mission 2 revision 1 at `32,50`/`33,49` was invalidated by live Mammoth
`120mmDual`; revisions 2 and 3 issued replacement threat-aware routes, then the
carrier held when no plan remained safe. After both temporary threats died it
reacquired the original connected objective region and completed. Mission 3 used
the bounded map-edge carrier/exit pair `2,80`/`1,80`. At tick 2000 all three
passengers were in world and moving from their handoffs; all three reservations
had released. No task timeout, safe recovery, fatal, or desync occurred. Ordinary
modules later reused carriers for additional emergent rescues, confirming release.
Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-12-run-42-final-literal/`.

Carrier-recovery run 43 is predeclared on the proven late-threat helicopter
assault, with one new post-handoff perturbation: a fixed MSAM covers the complete
radius around the original `28,30` assembly cell only after all three passengers
physically exit. Map SHA
`55ea4a45ad22500c20dc59f269c97668b486f202bde6c929a345e4f45c412707`,
seed `510061`, normal VIKI/SkyNet modules. Failure is an assembly-directed stale
route, unsafe landing in coverage, carrier death, reservation retained through
tick 2000, or the hard terminal timeout. Pass requires an explicit safe hold, a
current-position threat-screened fallback after the 300-tick bound, carrier
arrival/release, and cargo-zero survival.

Run 43 exercised the intended bounded branch but failed the survival signal. The
post-handoff MSAM rejected the entire assembly search, the carrier held for 300
ticks, and the current-position fallback selected the exact handoff cell `55,8`.
It released at tick ~1000 but was dead by tick 2000. Although snapshot safety was
true at selection time, releasing in the active enemy objective region was not an
operational recovery. Cycle 13 therefore changes fallback search from the handoff
cell to a bounded staging center beyond the assembly region, opposite the hostile
arrival vector; run 44 must retain the same blocker and prove survival/release.
Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-13-run-43-carrier-fallback/`.

Run 44 routed six threat-aware waypoints to the far-side fallback `20,38` and
released there, proving the revised fallback geometry. It still failed survival:
the newly released carrier was immediately claimed by the fixture's already-
blocked APC rescue, damaged en route to that pickup, released, and dead by tick
2000. The initial attribution to ordinary base restaging was incorrect. The next
repair still records the released fallback as the carrier's valid idle staging
cell: idle service leaves an undamaged carrier there while it remains at that
cell, while it remains unreserved and available to any real rescue/assault, and
the marker clears after another mission moves it. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-13-run-44-carrier-fallback/`.

Run 45 proved the safe-idle marker did suppress ordinary base restaging, but the
same fixture-created blocked APC immediately claimed the carrier for rescue
mission 2. That mission routed toward pickup, observed damage before boarding,
released, and the carrier died before tick 2000. This is not evidence that the
fallback marker failed, and excluding marked carriers would violate required
ordinary reuse. The fixture will retire or unblock that unrelated passenger after
the assault handoff so the next identical fallback run can observe idle survival
without disabling normal AI modules. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-13-run-45-carrier-fallback/`.

Run 46 is predeclared with fixture SHA
`561079bace9ffacf6fd2cce889e62c041cb43710e99ef3fcf979daa7eae7d30c`,
the same seed `510061`, threats, recovery blocker, normal VIKI/SkyNet modules, and
fallback survival assertions. The fixture-only correction destroys the enclosed
APC after it has already forced the real assault selection and after all assault
passengers exit, preventing that artificial trigger asset from starting an
unrelated second rescue during the post-release observation. Pass requires the
far-side fallback, honest release, no base restaging, and the idle unreserved
carrier alive with cargo zero at tick 2000.

Run 46 passed at tick 2600 in 13.013 seconds (199.742 valid ticks/s). The covered
assembly search named live MSAM `Patriot` coverage, held for the configured 300
ticks, selected the far-side `20,38` fallback, routed six threat-aware waypoints,
arrived at `21,38`, and released. Idle service left the unreserved carrier at its
recorded safe cell: it was alive at `20,38` with cargo zero at tick 2000. All 3/3
passengers remained in world at their useful handoff cells, and there was no
terminal timeout, base-restaging order, fatal, or desync. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-13-run-46-carrier-fallback/`.

Post-fix adversarial run 47 is predeclared as the unchanged Archipelago mixed-
vehicle package SHA
`cf425f8cdb77e1df9f7a21982e65210af82adbd4841c25e552c5518d876e9e8a`,
seed `510058`, normal IronReaper/SkyNet modules. It must load, route, physically
exit, and adopt all four simultaneous Mammoth/Medium-Tank passengers at distinct
connected plans, damage the edge-island target, retain cargo zero through tick
5000, and produce no hold, safe return, abort, fatal, or desync.

Run 47 passed at tick 7000 in 20.022 seconds (349.544 valid ticks/s). Wave 1
loaded all four mixed Mammoth/Medium-Tank pairs concurrently, claimed four
distinct carrier/exit plans on the connected target island, routed all four,
physically exited 4/4, adopted 4/4 into the assault, damaged and then killed the
target, and retained cargo zero through tick 5000. There was no wave hold, safe
return, abort, fatal, or desync; later ordinary rescue missions also reused the
released carriers. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-13-run-47-archipelago/`.

Post-fix adversarial run 48 is predeclared as the unchanged late-threat package
SHA `75cf809324dbecdabaa3382518b1816f6a008765eb5f04ebdb21fec2c802076d`,
seed `510056`, normal VIKI/SkyNet modules. It must invalidate revision 1 after the
tick-500 bike arrives, route and physically complete revision 2 for all 3/3
passengers, recover the empty carrier to the still-safe assembly region, release
it for ordinary reuse, and retain carrier/passenger survival at tick 1000 without
withdrawal, timeout, fatal, or desync.

Run 48 passed at tick 2200 in 12.013 seconds (183.075 valid ticks/s). The tick-500
bike invalidated revision 1 at `53,7`; revision 2 replaced the active route with
two threat-aware waypoints to `55,8` and physically handed off 3/3 passengers.
The empty carrier then routed four waypoints to the safe assembly `28,30`,
released, and was reused by ordinary rescue mission 2. Carrier and all passengers
were alive with cargo zero at tick 1000; there was no assault withdrawal, timeout,
fatal, or desync. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-13-run-48-late-threat/`.

Final-regression run 49 is predeclared from a fresh process using the unchanged
literal package SHA
`34f96775d113714607fbcb97977fd7b586d4af002c6f119d29a0b906a685c8f9`,
seed `510057`, and normal CABAL/SkyNet modules. It retains three independent
rescues, dense mobile/structure contention, map-edge objective `3,80`, blockers
arriving only after departure, an MSAM direct-approach screen, and moving Mammoths
that must invalidate mission 2. Pass requires 3/3 useful physical handoffs,
revision 2 or later after exact weapon rejection, all claims released, cargo zero
and all passengers alive at tick 2000, and no task timeout, safe recovery, fatal,
or desync through tick 5200.

Final-regression run 49 passed at tick 5200 in 18.016 seconds (288.576 valid
ticks/s). Missions 1-3 independently loaded and physically completed 3/3 useful
handoffs with cargo zero. Mission 2 revision 1 at `32,50` was invalidated by live
Mammoth `120mmDual`; revisions 2 and 3 replaced the active route, then the mission
reacquired its useful objective region after both threats retired. Mission 3 used
the bounded map-edge carrier/exit `2,80`/`1,80`. At tick 2000 all three passengers
were alive in world and all mission reservations had released. There was no task
timeout, safe recovery, fatal, or desync. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-13-run-49-final-literal/`.

Post-fix natural run 50 is predeclared on the unchanged connected Badland Ridges
diagnostic package SHA
`a131db97ce1f5471baeed9bc7d323e41e6b51eceb39f431b85a57d36d8656190`,
seed `510060`, normal IronReaper/SkyNet modules, and the clean Release build. It
must reach a natural outcome after observable ordinary transport activation with
no crash/desync/unchanged hold spam. Strategic parity is judged against the prior
SkyNet win; reported MAX rate must not regress more than 10% from the exact-SHA
control's 516.642 ticks/s, with transport outcomes assessed from physical exits,
honest recovery/terminal logs, and released claims rather than activation alone.

Post-fix natural run 50 passed naturally in 80.075 seconds at 561.949 reported
ticks/s, 8.8% above the pinned exact-SHA control's 516.642 rather than regressing.
Replay metadata records final tick 48198 and the same strategic outcome as the
control/prior changed match: SkyNet won and IronReaper lost. Normal production,
ground infantry-assault missions, idle helicopter staging, and both bots' economy
and combat continued. IronReaper's ordinary rescue mission 3 selected `73,96`/
`74,95`, routed seven threat-aware waypoints, physically handed off its passenger
at `74,95` with cargo zero and a useful-rescue outcome, then released all claims.
There was no crash, desync, unchanged hold spam, or dishonest transport success.
Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-13-run-50-natural-release/`.

Review-response run 51 is predeclared on the connected occupied-cell fixture with
one enabled enemy Apache at `47,30`. Its `HeliAGGun` has range 4096 and the
configured buffer is 1024; speed 160 over the 75-tick revalidation interval makes
the corrected effective envelope 17120. The first fallback is 15 cells away,
outside static range-plus-buffer but inside that closing envelope. Failure is a
static-only acceptance, missing exact aircraft/weapon/range rejection, unsafe
carrier or exit, retained cargo, withdrawal, timeout, fatal, or desync. Pass
requires an alternate exact plan, physical useful handoff, and release under
ordinary Cabal/SkyNet modules.

Run 51 passed to tick 4200 in 22.046 seconds (190.468 valid ticks/s), seed
`510062`, fixture SHA
`ae6cc567ccc07c321b02e6ef79d99c9eb82e7f53e729620bd1d68602bb33889a`.
The snapshot named `heli 559`/`HeliAGGun` at effective range 17120, rejected the
first fallback, then replaced two later cells as the aircraft moved. Mission 1
committed carrier `29,30`/exit `29,29`, physically handed off the Medium Tank
usefully, and released. There was no withdrawal, timeout, fatal, or desync.
Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-14-run-51-aircraft-threat-final/`,
`analysis/worker-5-cnc-51/cycle-14-aircraft-comment/NARRATIVE.md`, and
`analysis/worker-5-cnc-51/cycle-14-aircraft-policy/POLICY-REVIEW.md`.

Supplementary exact-head run 52 repeated the literal fixture. It reached tick
5200 cleanly and all original three missions physically completed useful handoffs
and released by tick 3500, including mission 2's live Mammoth revision. It is
invalid as a replacement literal acceptance artifact: mission 3's real squad
objective was `51,8` rather than the fixture-declared edge `4,80`, and its unload
finished after the fixture's tick-2000 assertion. No timeout or safe-recovery
outcome occurred. The mismatch is retained as an evidence-integrity limitation;
run 49 remains the clean literal acceptance artifact. The Policy Reviewer's
fixed-cutoff admission proposal is not adopted because production missions own
real mission deadlines, not a test exit horizon. Evidence:
`AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-14-run-52-final-literal/`,
`analysis/worker-5-cnc-51/cycle-14-literal-comment/NARRATIVE.md`, and
`analysis/worker-5-cnc-51/cycle-14-literal-policy/POLICY-REVIEW.md`.

| Cycle | Commit/change | Failure hypothesis and perturbation | Checks/games | Narrative/policy review | Failure/pass evidence | Decision/next harder test |
|---|---|---|---|---|---|---|
| 1 | Shared live threat snapshot, deterministic exact carrier/exit plan, atomic cell claims, targeted exact unload, rescue integration | Old targetless unload selects the occupied current cell; the changed bot must choose a distinct landable/useful exit plan and unload before timeout in the identical fixture. | Release build 0 warnings/errors; 11 focused transport/route tests pass; `make check check-scripts` pass; run 11 passed at tick 4200 with replay/benchmark. Changed throughput 299.652 versus control 322.7 ticks/s (about -7.1%, below 10% threshold). | `cycle-01-pair-comment/NARRATIVE.md`; `cycle-01-pair-policy/POLICY-REVIEW.md`: mostly sensible/medium, materially better in this scenario. Adopt explicit useful-region success and threat/exit hardening; reviewer hash uncertainty is resolved by worker SHA evidence, not by editing the isolated narrative. | Identical seed `510051` and map SHA `72931e7603be21ce21c09a3a7bcece5836e1cf6f0ffcc31214af4f96a6526854`: changed selected carrier `32,30`, exit `33,29`, physically exited and released before tick 1000 with both blockers fixed; control retained cargo through tick 3500 and timed out. Evidence: `AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-01-run-11/`. | Harden multi-passenger exact exits and all-armor threat capture, then test a direct-side live threat before manager adoption. |
| 2 | Stable multi-passenger exact-exit order encoding, all-armor snapshot capture, replanned-descent routing, explicit useful-rescue versus safe-recovery handoff, bounded first-threat diagnostic | A weapon irrelevant to Light armor may still threaten a Heavy passenger exit; stale descent must route to a replacement, and exact multi-passenger exits must survive order serialization. Direct-side live weapons must reject covered cells without forcing withdrawal. | 12 focused tests pass; `make check check-scripts` and Release build pass with 0 warnings/errors. Runs 12-13 reached tick 4200 but were invalid because paused threats were just outside stationary effective range. Run 14 passed at tick 4200, 299.609 ticks/s, with replay/benchmarks. | `cycle-02-batch-comment/NARRATIVE.md`; `cycle-02-batch-policy/POLICY-REVIEW.md`: mostly sensible/medium. Adopt full-sequence and all-threat boundary tests; retain threat-specific assertion. No escalation: no persistent unresolved policy problem. | Threat fixture SHA `b43cfce150dc50c401ec4a5c0aa3f32aa16f6481c967bd7e60f0a489c8a3c05c`: snapshot found 3 weapons; Mammoth `561`/`MammothMissiles` rejected carrier `32,30` at effective range 8192; planner chose carrier `32,29`/exit `31,30`, physically handed off and released before tick 1000 without withdrawal. Evidence: `AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-02-run-14/`. | Adopt the proven exact multi-exit seam in helicopter infantry assault, keep ground APC unchanged, then exercise cohesion/handoff under a live threat. |
| 3 | Helicopter-only infantry assault adopts shared multi-exit planner, exact unload, grouped attack handoff, and safe damage/timeout return; ground APC path unchanged. Multi-stage handoff retains all physically delivered survivors after a partial-plan revalidation. | Old helicopter assault unloads targetlessly and treats any damage as permission to unload in place. A loaded wave must instead select complete safe exits, route/revalidate, hand off as a grouped attack, or return to its safe assembly region. | 14 focused transport/route tests pass; `make check check-scripts` and Release build pass with 0 warnings/errors. Runs 15-16 invalid fixture iterations; run 17 passed at tick 2200 with replay/benchmarks, 183.106 ticks/s. | `cycle-03-assault-comment/NARRATIVE.md`; `cycle-03-assault-policy/POLICY-REVIEW.md`: insufficient strategic-outcome evidence/high confidence, while calling the survival-first route/drop decision sensible. Adopt post-drop effect evidence in later games; the suggested APC retry-policy expansion is not adopted because it is fixture-created rescue behavior outside this helicopter-assault change. | Seed `510053`, map SHA `d60536d6fa921b1f3517456fa45e53af8260a52b07afb44aaa824e1aad152ea3`: Mammoth missiles rejected `52,7`; plan chose carrier `53,7`, exits `52,6;53,6;54,7`, used four waypoints, physically exited 3/3, issued grouped target handoff, and released with cargo zero. Evidence: `AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-03-run-17/`. | Adopt the shared planner in heavy-drop aircraft while preserving concurrent pickup and explicit Mammoth squad handoff; instrument post-drop target effect in its Archipelago adversarial game. |
| 4 | Heavy drop uses snapshot-screened strategic selection, an atomically claimed non-overlapping exact plan set, threat-aware routes, exact exits, safe return, and the preserved Mammoth squad-adoption handoff. | Ten-style concurrent carriers sharing one wave ID must not overwrite claims, select a wrong-island/covered formation, targetlessly unload, or lose the CNC-25 handoff. The first eight-pair connected stress adds combined threats and measured post-drop target effect. | 33 focused transport/route/heavy tests pass; `make check check-scripts` and Release build pass with 0 warnings/errors after removing one obsolete import. Run 18 reached tick 7000 and failed only the diagnostic assertion; run 19 passed at tick 7000 with replay/benchmarks, 303.955 ticks/s. | `cycle-04-heavy-comment/NARRATIVE.md`; `cycle-04-heavy-policy/POLICY-REVIEW.md`: insufficient full-match strategy evidence/high confidence, but disciplined 8/8 delivery, threat rejection, target kill, and carrier recovery made tactical sense. Adopt separate truthful strategic-versus-exact rejection telemetry and future matched/natural outcome evidence; reject any requirement to fabricate a local rejection after strategic screening already chose a clear sector. | Seed `510054`, map SHA `838cd383bffb7b1ec56f04a7eef200ce4241455b30404562d63fa2d33db265a6`: strategic screening rejected `44,8` for MSAM `Patriot` effective range 10240 and selected `60,1`; 8/8 loaded concurrently, received eight distinct carrier/exit plans and routes, physically exited, and were adopted 8/8. The target fell from 210000 to 153800 HP by tick 1000 and was destroyed by tick 2000. Evidence: `AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-04-run-18/` and `changed-cycle-04-run-19/`. | Use a no-safe-site transition to test invalidation, bounded hold/recovery, and claim release; later add matched control/natural outcome rather than treating a factory kill as the strategic verdict. |
| 5 | Retain rescue recovery objectives across failed plans, add bounded safe current-position fallback planning for rescue/assault return, and treat stopped `FlyIdle` helicopters as retry-idle. | A covered site that later opens must not remain stuck in a stopped aircraft activity; failed recovery must not forget its assembly/base objective and drift back toward the unsafe original destination. | 33 focused tests and `make check check-scripts`/Release build pass. Run 20 exposed `FlyIdle` route starvation; run 21 reached tick 2200 and passed every physical transition assertion but failed the initial hold-reason regex. | `cycle-05-transition-comment/NARRATIVE.md`; `cycle-05-transition-policy/POLICY-REVIEW.md`: insufficient broad evidence/high confidence, but run-21's safe transition made sense and bounded route/commit progress is required. The implemented `FlyIdle` fix converts selected plans into immediate routes; retain mission timeout/current-position fallback as the broader bound. Adopt separate truthful blocker-versus-threat diagnostic categories. | Run 21 held 3 cargo while covered, observed the tick-400 MSAM removal, selected carrier `53,7`/three exits at snapshot 450, routed four waypoints, physically exited 3/3, handed off, and released before tick 1000. The only failed assertion was that the hold's single reason named a weapon: it truthfully named the first blocked target cell even though later candidates were weapon-covered. Evidence: `AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-05-run-20/` and `changed-cycle-05-run-21/`. | Prefer the first threat rejection over a non-threat first candidate when a complete plan fails, then rerun for a clean transition artifact. |
| 6 | Failed bounded searches report the first concrete threat rejection when present, otherwise the truthful terrain/blocker/connectivity reason. | The all-covered hold must identify the applicable threat/weapon rather than only the occupied objective cell, while the newly opened route must still execute rather than merely log a plan. | 33 focused tests and Release build pass with 0 warnings/errors. Run 22 passed at tick 2200, 183.071 ticks/s, with replay/benchmarks. | `cycle-06-transition-comment/NARRATIVE.md`; `cycle-06-transition-policy/POLICY-REVIEW.md`: mostly sensible/medium. Survival-first hold and prompt exploitation of the opening fit VIKI; adopt post-handoff target/casualty evidence and a late re-cover test, but do not infer match-level improvement from the focused run. | Seed `510055`, map SHA `495c0addb4aabb97bda4875a587578aa93af1dd6ead98179df7d679b6b73e066`: hold named MSAM `559`/`Patriot` effective range 10240; after tick-400 removal snapshot 450 selected carrier `53,7`/three exits, routed four waypoints, physically exited 3/3, handed off, and released before tick 1000. Clean post-fix adversarial scenario 1. Evidence: `AUTONOMOUS-CNC-LOGS/cnc51-control/changed-cycle-06-run-22/`. | Exercise moving/late threat invalidation with a stale plan canceled and a safe alternate route. |
| 7 | Track the last routed plan revision for rescue and helicopter assault, immediately replace stale in-flight routes, reject empty routes while away from the carrier, and diagnose the exact revalidation reason. | A threat arriving after departure must invalidate revision 1 and cause revision 2 to cancel/replace the old route while the aircraft is still moving; a failed replacement must hold or recover rather than continue stale. | 33 focused tests pass. Run 23 was invalid because a late Mammoth covered the complete search. Run 24 exercised revision 1 to revision 2 and physical 3/3 handoff, but a post-success dead-carrier observer caused fatal Lua. | Pending a clean artifact. | Run 24 selected revision 1 at `53,7`; bike arrival at tick 500 caused snapshot 525 to report `replannedBecause=planned carrier cell covered by bike 565 weapon BikeRockets`, select revision 2 at `55,8`, and immediately log a two-waypoint revision-2 route before exact commit and 3/3 handoff. Evidence: `changed-cycle-07-run-23/` and `changed-cycle-07-run-24/`. | Begin cycle 8 with an observer-only repair, rerun the identical late-threat fixture, and require post-handoff survival/casualty state without Lua failure. |
| 8 | Observer-only dead-actor handling produced a clean late-threat artifact without changing the revision behavior. | The identical seed and geometry must prove the in-flight cancellation/handoff and reveal post-handoff passenger/carrier state instead of terminating in fixture Lua. | Run 25 passed at tick 2200, 168.981 ticks/s; 33 focused tests pass. | `cycle-08-late-threat-comment/NARRATIVE.md`; `cycle-08-late-threat-policy/POLICY-REVIEW.md`: conditional tactical pass/medium. The replan and 3/3 handoff are sound, but carrier disposition is a separate operational outcome and match-level success remains unknown. | Revision 2 used `55,8`/three exits and all passengers remained in world through tick 2000. The empty carrier died before tick 1000, and a later blocked APC had no healthy empty carrier. Evidence: `changed-cycle-08-run-25/`. | Keep the assault reservation through a carrier-only threat-screened return, record authoritative post-handoff cells, rate-limit unavailable-transport diagnostics, and repeat the same seed. |
| 9 | Add carrier-only threat-screened recovery plans, retain the assault reservation through safe return, record authoritative handoff cells, and rate-limit persistent blocked/no-carrier diagnostics. | The same late-threat handoff must preserve and release the empty carrier at a verified safe assembly cell, then make it available to the waiting APC rescue instead of losing fleet capacity. | 33 focused tests pass. Run 26 passed at tick 2200, 156.933 ticks/s, with replay/benchmarks. | `cycle-09-carrier-recovery-comment/NARRATIVE.md`; `cycle-09-carrier-recovery-policy/POLICY-REVIEW.md`: conditional tactical pass/medium. Carrier recovery and reuse are sound; the later APC safe recovery preserved life but did not complete its original objective, so match-level success remains unproven. | Seed `510056`, map SHA `75cf809324dbecdabaa3382518b1816f6a008765eb5f04ebdb21fec2c802076d`: exact 3/3 handoff at revision 2; carrier recovery chose `28,30`, routed four waypoints, was alive at tick 1000, released at assembly, and was reused for the APC recovery. Evidence: `changed-cycle-09-run-26/`. | Exercise literal multi-rescue contention with a moving Mammoth and map-edge/structure/mobile exit pressure. |
| 10 | No product change; construct the literal three-rescue fixture with normal squad-owned movement intent, dense occupancy, a structure, map edge, post-load blockers, MSAM, and moving Mammoth. | Simultaneous passengers may share only one squad intent; sequential passengers must independently activate without losing blocker/threat timing or starving a carrier. | Run 27 completed only mission 1; run 28 completed missions 1 and 2. Both were invalid as three-rescue evidence. | Not material for isolated review. | Run 27 exposed grouped leader-only intent. Run 28 proved sequential spawning produces a second normal mission but exposed carrier-bound callback attribution and idle staging. Evidence: `changed-cycle-10-run-27/`, `changed-cycle-10-run-28/`. | Cycle 11 uses passenger-resolved load identity, sequential third spawn, delayed staging, and post-plan moving Mammoth timing; require 3/3 before further hardening. |
| 11 | No product change; correct passenger-bound fixture callbacks and stage moving Mammoths after mission 2's initial plan. | All three real squads must trigger independently; a late Mammoth must invalidate an in-flight plan without permitting stale unload, while map-edge/blocker contention remains live. | Run 34 passed at tick 5200, 185.323 ticks/s, after runs 29-33 established fixture timing/objective/retirement bounds. | `cycle-11-literal-comment/NARRATIVE.md`; `cycle-11-literal-policy/POLICY-REVIEW.md`: insufficient match-level evidence/high confidence, but the wait/replan/complete sequence is locally survival-first and no transport blunder is proven. Retain it; test persistent threat bounds and paired strategic outcome before broader claims. | Seed `510057`, SHA `34f96775...`: 3/3 useful physical handoffs; mission 2 routed revisions 2/3 for `120mmDual`/`MammothMissiles`, held under continuing coverage, then reacquired its useful exact region after both threats died. All passengers/carriers alive with zero cargo at tick 2000. Evidence: `changed-cycle-11-run-34/`. | Exercise Archipelago connected-island vehicle unload and an ordinary natural match; capture winner/economy/end-state telemetry and retain the already-proven bounded persistent-threat recovery rather than changing policy from this narrow review. |
| 12 | Rate-limit unchanged heavy-drop pre-wave blockage diagnostics using the shared configured scan bound; relocate the Archipelago fixture to guaranteed spawn land areas and a unique undefended edge-island target. | A geometrically plausible island setup can fail before boarding, and an unchanged failure can spam every 75 ticks; mixed Mammoth/Medium-Tank pairs still need distinct pickup/exit cells and connected target-island handoff. | Release build 0 warnings/errors; 35 focused tests pass. Runs 35-36 invalid with only 1/4 pickup cells; run 36 proved eight-scan diagnostic limiting. Run 37 passed at tick 7000, 279.672 ticks/s. Natural runs 39/41 physically completed ordinary rescues; run 41 reached natural outcome at 550.807 ticks/s versus pinned control 516.642. Final literal run 42 passed 3/3 at tick 5200, 305.456 ticks/s. | Archipelago: `cycle-12-archipelago-comment/NARRATIVE.md` and `cycle-12-archipelago-policy/POLICY-REVIEW.md`, mostly sensible/medium. Natural pair: `cycle-12-natural-pair-comment/NARRATIVE.md` and `cycle-12-natural-pair-policy/POLICY-REVIEW.md`, mixed/medium; it judges the two SAM-avoiding physical recoveries locally effective but correctly withholds match-level causality because opening rolls diverged. Final literal: `cycle-12-final-literal-comment/NARRATIVE.md` and `cycle-12-final-literal-policy/POLICY-REVIEW.md`, mostly sensible/medium, with no demonstrated transport-policy blunder. Production retry/APC advice is unrelated scope. A fixed-horizon admission rule is rejected because the fixed exit belongs to the test harness, mission 5 had no cargo before cutoff, and ordinary missions already have tested timeout/recovery bounds. | Run 37 loaded/exited/adopted 4/4 and killed the connected target. Release run 41 safely terminated two SAM-covered emergent rescues, retained the control's SkyNet win, and exceeded control throughput by 6.6%. Run 42 completed 3/3 useful handoffs after live replans. Evidence: `changed-cycle-12-run-37-archipelago/`, `pinned-control-run-40-natural-observed/`, `changed-cycle-12-run-41-natural-release/`, `changed-cycle-12-run-42-final-literal/`. | Release/full verification and publication; do not expand into fixture-horizon, production-request, or APC-composition policy. |
| 13 | Empty assault-carrier recovery gains bounded far-side safe fallback, a hard reservation deadline, and released-fallback idle staging; heavy safe return re-enters timeout-governed travel and attempts an atomic far-side plan set when assembly cells disappear. | A permanently covered assembly could reserve an empty carrier forever; releasing at the handoff cell or immediately restaging into the covered base could kill it. Heavy return invoked from unloading could also remain outside timeout-governed travel. | Release build 0 warnings/errors; 35/35 focused and 442/442 full tests pass; `make check check-scripts` passes. Runs 43-45 exposed fallback/disposition and fixture-reuse confounders. Post-fix runs 46-48 passed covered recovery, Archipelago mixed vehicles, and late-threat recovery/reuse; final literal run 49 passed; Release natural run 50 passed. | `cycle-13-final-batch-comment/NARRATIVE.md`; `cycle-13-final-batch-policy/POLICY-REVIEW.md`: mixed/medium. It endorses safe replan/recovery as sound survival-first policy but does not infer match-level improvement. Harvester/MCV production recovery and undersized ground-APC assault advice are deferred as separate ownership/scope. | Run 46 held under MSAM coverage, routed six waypoints to `20,38`, released, and remained alive/cargo-zero at tick 2000. Run 47 exited/adopted 4/4 mixed vehicles and killed the target. Run 48 replanned 3/3 then recovered/reused the carrier. Run 49 completed 3/3 useful rescues with live Mammoth revisions and edge exit `1,80`. Run 50 naturally completed a useful rescue, retained the SkyNet win, and ran at 561.949 ticks/s (+8.8% versus pinned control). | Publish scoped branch/PR, wait Linux/Windows checks, obtain isolated code review, and respond at most once to material findings. |
| 14 | Review response commit `cb6a05d5a3`: include enabled `Aircraft.MovementSpeed` alongside ground-mobile speed in the one-replan closing margin; add overflow-bounded pure envelope logic and a carrier/exit boundary regression. | The isolated reviewer found that a live enemy aircraft just outside static weapon-plus-buffer range could close before the next snapshot while receiving zero movement margin. | 82/82 focused transport/air-threat/route tests, 443/443 full Release tests, `make check check-scripts`, `make test`, and Release builds all pass with 0 warnings/errors. Run 51 cleanly exercises the aircraft branch. Run 52 is a strict harness invalid with three eventual safe handoffs. Linux and Windows CI pass on exact product head. | PR review `pr-81-review/PR-REVIEW.md`: `ready with one fix`; applied exactly. Run 51 comment/policy: mostly sensible/medium. Run 52 comment/policy: strict assertion invalid and mixed/medium. Another aircraft old-control pair and fixture-cutoff admission are rejected as unnecessary/broader scope because the task already retains pinned/matched controls and production mission deadlines are not test horizons. | Run 51 proved `HeliAGGun` static 5120 versus corrected dynamic 17120, named the aircraft rejection, replanned twice, physically unloaded usefully, and released. Run 52 preserved safe bounded behavior but missed the artificial tick-2000 pattern after mission 3 selected a different objective; it is not promoted over clean run 49. | Handoff `Complete - testing`: publish report/state only, retain run-52 fixture limitation and no additional product cycle. |

## Handoff receipt

- Proposed status: `Complete - testing`
- Final branch/head: `agent/round-20260806-cnc51-transport-unload` / reviewed
  product head `cb6a05d5a302b2f1db2f32d2f72f684005a18611`; the handoff-state
  commit is documentation-only.
- PR and checks: `https://github.com/Realpra1/LibertyDawn/pull/81`, mergeable to
  `agent/cnc38-early-viki-infantry-rush`; Linux (.NET 6.0) passed in 2m06s and
  Windows (.NET 6.0) passed in 3m35s on exact product head `cb6a05d5a3`.
- Cycles used: `14/20`, including the single permitted review-response cycle.
- Acceptance evidence: clean run 49 completed the literal three-rescue scenario
  3/3 with dense contention, edge exit `1,80`, live Mammoth replans, useful
  physical handoffs, and exact claim release; run 11 decisively fixed the pinned
  occupied-cell failure.
- Adversarial evidence: post-recovery-fix runs 46-48 cover no-safe assembly/far-
  side recovery, Archipelago mixed-vehicle connectivity/adoption, and live-threat
  replan plus carrier reuse. Review-response run 51 adds the enabled enemy-aircraft
  ground-weapon closing envelope.
- Old-behavior control and comparative result: exact pinned SHA `09ccdac3...`
  retained cargo through tick 3500 and timed out where changed run 11 completed
  before tick 1000; changed was -7.1%, inside the performance bound. Exact-base
  natural run 40 and changed Release run 50 both produced a SkyNet win; changed
  ran at 561.949 versus 516.642 ticks/s (+8.8%).
- Match narratives and routine policy-review conclusions: cycle 13 `mixed` /
  `medium`, cycle-14 aircraft `mostly sensible` / `medium`, and cycle-14 literal
  `mixed` / `medium`. Reviews endorse survival-first threat replan/recovery but do
  not infer match-level win causality. Fixed-horizon admission, unavailable-
  harvester recovery, and ground-APC package policy are rejected/deferred to their
  owning scopes.
- Sol-xhigh policy escalation (unused, or test count/path/conclusion): unused.
- Final regression: clean fresh run 49 remains literal acceptance. Supplementary
  exact-head run 52 is explicitly invalid as a replacement artifact because the
  real squad selected objective `51,8` instead of fixture edge `4,80` and mission
  3 completed after tick 2000; all three still handed off usefully and released by
  tick 3500 with no timeout/safe recovery. The reviewer-required affected-path
  regression is clean run 51.
- Error/warning and diagnostic-cleanup result: 82/82 focused and 443/443 full
  Release tests pass; `make check`, `make check-scripts`, `make test`, and Release
  compilation pass with zero warnings/errors. Debug diagnostics remain gated and
  unchanged failures are rate-limited; no temporary per-tick product tracing is
  committed.
- Performance/determinism result: stable actor/passenger/cell ordering, atomic
  claims, bounded 75-tick snapshot reuse and 128 candidates. Matched performance
  is within bound; run 51 reached 190.468 ticks/s.
- Deferred work: save/load persistence; adaptive unavailable harvester/MCV
  production recovery; ground-APC assault composition; richer replay economy/
  casualty causality; deterministic literal-fixture objective binding.
- Known failures/risks: low-frequency heavy multi-carrier far-side terminal
  fallback lacks a dedicated physical branch game but is atomic/bounded and the
  normal Archipelago path passes. Run 52 documents literal fixture objective/
  cutoff instability; do not convert the artificial test horizon into production
  admission policy. No known unsafe aircraft-margin, cargo-retention, claim-leak,
  compilation, or CI failure remains.
- Relevant artifact paths: task report
  `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260806-bug-polish-01/WORKER-5-CNC-51/REPORT.md`;
  isolated PR review `analysis/worker-5-cnc-51/pr-81-review/PR-REVIEW.md`;
  run roots `changed-cycle-13-run-46-carrier-fallback`,
  `changed-cycle-13-run-47-archipelago`, `changed-cycle-13-run-48-late-threat`,
  `changed-cycle-13-run-49-final-literal`,
  `changed-cycle-13-run-50-natural-release`,
  `changed-cycle-14-run-51-aircraft-threat-final`, and
  `changed-cycle-14-run-52-final-literal` under
  `AUTONOMOUS-CNC-LOGS/cnc51-control/`.
