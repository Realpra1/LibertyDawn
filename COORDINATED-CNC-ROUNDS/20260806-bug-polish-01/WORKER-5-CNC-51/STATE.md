# Worker State: CNC-51

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `worker-5-cnc-51`
- Task: `CNC-51 — Transport-helicopter unload recovery and threat-safe landing`
- Status: `Specified`
- Common base branch/SHA: `agent/cnc38-early-viki-infantry-rush` / `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
- Task branch: `agent/round-20260806-cnc51-transport-unload`
- Intended PR base: `agent/cnc38-early-viki-infantry-rush`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `0`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260806-bug-polish-01/WORKER-5-CNC-51/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-5-cnc-51`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `0`
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

| Cycle | Commit/change | Failure hypothesis and perturbation | Checks/games | Narrative/policy review | Failure/pass evidence | Decision/next harder test |
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
- Sol-xhigh policy escalation (unused, or test count/path/conclusion):
- Final regression:
- Error/warning and diagnostic-cleanup result:
- Performance/determinism result:
- Deferred work:
- Known failures/risks:
- Relevant artifact paths:
