# Worker State: CNC-44

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `worker-5`
- Task: `CNC-44 — Aircraft husks`
- Change category: `Gameplay/engine crash-lifecycle invariant plus CNC rules/config integration; not AI policy or balance`
- Balance authority: `Frozen. Authorized only to add the requested capturable-husk behavior, the per-aircraft restoration mappings, the exact post-crash occupancy gate, and the explicit A10 exclusion. Do not change crash/weapon damage (owned by CNC-62), actor cost, HP, armor, speed, turn rate, altitude/fall velocity, production or repair timing, prerequisites, probability, standard husk durability/decay, TransformOnCapture health percentage, Engineer behavior, or any AI weight/priority. Reuse existing standard husk/capture values; record any proposed tuning as deferred work.`
- Status: `First iteration - testing`
- Common base branch/SHA: `agent/cnc-20260806-bug-polish-01-release` / `419bee2531d4802bf922c3597b42c6eeb75ab250`
- Task branch: `agent/round-20260807-cnc44-aircraft-husks`
- Intended PR base: `agent/cnc-20260806-bug-polish-01-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `1`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/workers/worker-5-cnc44/COORDINATED-CNC-ROUNDS/20260807-bug-polish-02/WORKER-5-CNC-44/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-5-cnc44`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `7` (2 invalid control harness runs with useful evidence, 1 valid control, 1 valid changed, 3 materially useful type/capture runs with invalid acceptance assertions)
- Terra cycle code reviews: `none yet; required after cycles 5/10/15/20 that occur`
- Sol-xhigh policy escalation: `unused (requires at least 10 game tests; one maximum)`
- PR: `#85 — https://github.com/Realpra1/LibertyDawn/pull/85`

## Integrated repair assignment

- Phase: `integrated testing`
- Current release branch/head: `agent/cnc-20260807-bug-polish-02-release` / `ffb841b48750cc54b1862fb93101d3dce3a87a3f`
- Integration notes: `COORDINATED-CNC-ROUNDS/20260807-bug-polish-02/INTEGRATION.md`
- Repair branch: `agent/round-20260807-cnc44-rc1-repair`
- Repair PR base: `agent/cnc-20260807-bug-polish-02-release`
- Integrated cycles used this RC: `0/3`
- Integrated cycles used total: `0/12`

Before relaunching this worker for combined testing or repair, the integrator must
replace these fields with the exact release head, note path, branch, and counters.
During that phase, the repair branch replaces the original task branch as the
writable branch; the task scope and behavioral contract do not change.

## Why and predicted change

At the recorded base, airborne `TRAN`, `HELI`, and `ORCA` deaths create temporary
aircraft actors named `TRAN.Husk`, `HELI.Husk`, and `ORCA.Husk`. Those actors fall,
apply `HeliCrash` at ground contact, and are then killed. They inherit
`^CommonHuskDefaults`, not the normal ground `^Husk`, so they are neither durable
ground occupants nor capturable and nothing recoverable remains. `A10` follows
the same temporary falling-husk path, but the task expressly excludes it from
leaving a capturable remain.

After this change, each Chinook, Apache, or Orca destroyed while airborne will
retain its existing falling/crash presentation and damage, then leave exactly one
aircraft-specific, standard capturable ground husk at the final impact cell only
when the crash has finished resolving and no live blocking actor remains there.
An Engineer that completes capture will be consumed by the normal capture flow
and the husk will become the matching usable aircraft owned by the capturer. A
live vehicle, building, surviving infantry, or other blocking occupant prevents
the husk; dead crash victims and their deferred-removal timing do not. Stacked
infantry all killed by the eventual CNC-62 exact-cell crash damage therefore do
not suppress the husk, while even one survivor does. A10 may retain its current
falling crash visual and CNC-62 damage, but it never leaves a permanent capturable
husk.

## Authoritative behavior

- Preserve the literal request: "Make aircraft create capturable husks when they
  crash (Orcas and helicopters already fall to ground), except A10s. Resolve
  CNC-62 crash damage before the occupancy decision and inspect every actor
  sharing the impact cell: create the husk only when no blocking actor survives.
  One occupant dying is insufficient if another survives. Prefer the crash
  squishing/killing all infantry in the exact cell so stacked infantry do not
  prevent the husk; a surviving vehicle, building, or other blocking actor still
  prevents it. Capturing the husk grants the aircraft. Test empty cells, one and
  multiple infantry, mixed infantry/vehicle occupants, partial survivors,
  crash-killed occupants, simultaneous removal timing, terrain/map edges, and
  every husk-capable aircraft type."
- Husk-capable aircraft for this task are the three current CNC actors that spawn
  helicopter husks: `TRAN` (Chinook Transport), `HELI` (Apache Longbow), and
  `ORCA`. `A10` is the explicit negative case. `C17` has no health/death-husk path
  at the base and is not silently brought into scope.
- Keep the existing airborne-only condition inherited by normal helicopters:
  grounded helicopter deaths must not gain a new falling or permanent husk path.
- The existing fall reaches its final impact position first. The configured crash
  weapon, including CNC-62's eventual exact-cell effects, resolves before the
  occupancy decision. The durable husk must not become visible, targetable,
  capturable, or blocking in midair.
- Evaluate the exact final impact cell, not the original death cell, an adjacent
  cell, only a selected subcell, or an area around the explosion. Inspect all
  actor influences sharing that cell. Exclude the falling husk itself and actors
  already killed/removed by the crash; do not exclude a live actor merely because
  it is infantry, crushable, moving, allied, neutral, cloaked, or scheduled for a
  later action. A live full-cell blocker or any live infantry subcell occupant is
  sufficient to suppress the full-cell husk.
- Make the decision at a lifecycle boundary where synchronous crash damage and
  the relevant queued removals are observable. The result must be independent of
  ActorMap enumeration order: all crash victims dying permits the husk; any one
  survivor suppresses it. A dead actor still present in the current tick must not
  cause a false rejection.
- On valid in-map terrain supported by the standard CNC `^Husk` contract, a clear
  impact creates one durable ground actor with `HuskInfo`, standard `husk`
  capturability, normal target/decay behavior, matching facing/owner/effective
  owner semantics, and a `TransformOnCapture` mapping back to the exact source
  aircraft. Reuse the established standard husk health and restored-health
  percentage; this task grants no authority to tune either.
- Capturing a `TRAN` husk yields `TRAN`; capturing `HELI` yields `HELI`; capturing
  `ORCA` yields `ORCA`. The resulting actor must be alive, in world, owned by the
  capturer, able to accept its ordinary orders, and visible to normal AI/module
  discovery. Never map one aircraft to another or restore `A10`.
- Invalid/off-map or non-husk terrain must be handled deterministically without a
  crash, malformed actor, or overlap. Follow the existing `Husk.AllowedTerrain`
  contract: do not broaden allowed terrain as an implicit balance/policy change.
- If two husk-capable aircraft reach the same impact cell in the same frame, or a
  live blocker enters during deferred processing, create at most one non-
  overlapping ground husk. Validate the cell at the actual creation boundary, not
  only against an earlier snapshot.
- Preserve current crash animation, explosion/effect, ownership, statistics, fog,
  shroud, aircraft production/repair, and unrelated vehicle/building/tree death-
  spawn behavior except where an opt-in generic lifecycle primitive must be added
  to satisfy this exact contract.

## Forbidden behavior and failure signals

- No permanent husk, a noncapturable visual remain, or a husk that vanishes at
  the end of `FallToEarth` after a qualifying empty-land `TRAN`, `HELI`, or `ORCA`
  crash is a literal failure.
- Any durable/capturable `A10` husk, an A10 restored by capture, or bringing `C17`
  into the behavior is a literal failure. Do not remove A10 crash damage or its
  transient fall merely to enforce the exception.
- Applying the occupancy test before `ExplosionWeapon.Impact`, testing only the
  first actor, stopping after one victim dies, checking a radius instead of the
  exact cell, or relying on ActorMap iteration order is forbidden.
- A live infantry, vehicle, structure, ground husk, or other blocker overlapping
  a newly created full-cell aircraft husk is a failure. Conversely, rejecting a
  husk solely because all occupants are dead but awaiting frame-end removal is a
  failure.
- Do not classify every infantry actor as ignorable. Infantry may be ignored only
  because crash damage killed/removed that specific actor before the final
  decision. A surviving infantry actor must suppress the husk just like another
  survivor.
- Do not add capturability/`Husk` directly to the temporary falling actor if that
  makes it reserve/block ground, become an Engineer target in midair, decay while
  falling, or die at impact without leaving the durable form.
- Do not spawn the ground husk immediately inside `FallToEarth.Tick` before
  removals settle, and do not rely on one extra hard-coded tick whose result
  changes with actor/frame-end ordering. Simultaneous crashes must not produce
  duplicate overlapping husks.
- Do not globally change `SpawnActorOnDeath`, `FallsToEarth`, or common husk
  semantics without an opt-in/default-preserving boundary. A vehicle, building,
  tree, crate, or another mod's actor changing behavior is a regression.
- Do not implement or tune CNC-62 damage here. Changing `HeliCrash`, any actor HP
  or armor, a damage multiplier/type/falloff, infantry crushability, fall speed,
  or impact timing to make occupancy evidence pass violates frozen balance and
  the dependency boundary.
- Do not hard-code actor names in a generic engine algorithm when rules/config can
  own the source-to-ground-husk and capture mappings. Conversely, do not hide the
  lifecycle/order invariant in test Lua or repeated per-aircraft config.
- Wrong restored type, owner, faction, facing, health policy, or more than one
  restored actor is a failure. An Engineer request/order/reservation or a log that
  says capture started is not acceptance unless the final matching aircraft
  exists under the capturer and the husk/Engineer transition completed normally.
- Noisy per-tick/per-actor scans, full-world occupancy scans per crash, nondeterministic
  unordered winner selection, new unhandled warnings/exceptions, desyncs, or a
  material MAX-throughput regression are publication blockers.

## Relevant current implementation and control behavior

- Base and old-control SHA is `419bee2531d4802bf922c3597b42c6eeb75ab250`.
  At specification time the integration worktree is exactly at that SHA.
- `mods/cnc/rules/defaults.yaml` defines `^Helicopter` with conditional
  `SpawnActorOnDeath` requiring `airborne`; `mods/cnc/rules/aircraft.yaml` supplies
  `Actor: TRAN.Husk`, `HELI.Husk`, and `ORCA.Husk`. `A10` independently defines
  `SpawnActorOnDeath: Actor: A10.Husk`; `C17` defines neither `Health` nor a death
  spawn.
- `TRAN.Husk`, `HELI.Husk`, and `ORCA.Husk` inherit `^HelicopterHusk`; `A10.Husk`
  inherits `^PlaneHusk`. Both templates inherit `^CommonHuskDefaults`, carry an
  `Aircraft` plus `FallsToEarth`, and deliberately lack `Husk`, `CaptureManager`,
  `Capturable`, and `TransformOnCapture`. They are temporary aircraft actors,
  despite their names.
- `OpenRA.Mods.Common/Activities/Air/FallToEarth.cs` checks ground contact,
  synchronously calls `info.ExplosionWeapon.Impact(Target.FromPos(...), self)`,
  then calls `self.Kill(self)`. `Health.InflictDamage` immediately marks killed
  victims at zero HP and queues `Actor.Dispose`; actual world/ActorMap removal is
  a frame-end task.
- `OpenRA.Mods.Common/Traits/SpawnActorOnDeath.cs` records a successful kill in
  `INotifyKilled`, waits for the dead actor's `INotifyRemovedFromWorld`, then
  queues creation at frame end. History (`a5df442499`) says this delay exists so
  all removed-from-world callbacks run before the replacement is added. This is a
  useful ordering primitive, but the trait currently has no occupancy/terrain
  gate.
- `ActorMap.GetActorsAt` enumerates all non-disposed influences in the exact cell;
  it does not itself filter `Actor.IsDead` or `WillDispose`. `Husk` occupies one
  full cell and its runtime `GetAvailableSubCell` rejects other cell influences,
  but `HuskInfo.CanEnterCell` intentionally returns true and raw `World.CreateActor`
  does not pre-validate placement. The new path therefore needs an explicit,
  final-time valid-cell/blocker decision.
- Current `HeliCrash` in `mods/cnc/weapons/explosions.yaml` is a 10,000-damage
  `SpreadDamage` inheriting a 426-unit spread. It is not the authoritative final
  exact-cell behavior; CNC-62 owns Apache/Orca/A10/transport-helicopter impact
  damage and application to every actor in the impact cell.
- Standard ground vehicle husks inherit `^Husk` in
  `mods/cnc/rules/defaults.yaml`: `Husk.AllowedTerrain`, `Targetable` types
  `Ground,Husk`, `CaptureManager`, `Capturable.Types: husk`,
  `TransformOnCapture.ForceHealthPercentage: 25`, decay, burn overlay, and normal
  force-fire/explosion behavior. `mods/cnc/rules/husks.yaml` owns each vehicle
  husk's exact `IntoActor` mapping. Aircraft ground husks should use this existing
  contract rather than invent a second capture protocol.
- CNC Engineers (`E6`) have `Captures@CAPTURES.CaptureTypes: building,husk`.
  `CaptureManagerBotModule` is enabled for all ordinary listed AI personalities,
  scans every eligible enemy/neutral capturable actor, understands `HuskInfo`,
  values a `TransformOnCapture` target using the restored actor's cost, reserves
  Engineers, and issues the normal capture order. Attack squads explicitly avoid
  `HuskInfo` targets. This is the control behavior the new ground actor must join.
- After capture, `HELI`/`ORCA` can be discovered by `SquadManagerBotModule` air
  squads and normal repair/base modules; `TRAN` is excluded from air squads and
  is externally/transport managed by the transport, heavy-drop, and infantry-
  assault transport paths. These consumers must not retain stale references to
  the removed husk or double-claim the restored actor.
- Relevant history: `5184cee3ca` (2013) introduced temporary aircraft husks and
  falling visuals; `7dddc7fc44` changed ground impact from dispose to kill;
  `a5df442499` moved death-spawn creation after removal callbacks; `395b34ebcc`
  (2024) made A10 killable and added its temporary plane husk. No existing focused
  `FallsToEarth`/aircraft-husk test was found in `OpenRA.Test`.
- At specification time `gh pr list`, local/remotes, and `git ls-remote` contain
  no CNC-44 or CNC-62 branch/PR. Open PRs likewise contain no crash-damage or
  aircraft-husk implementation to inspect. Recheck before code and publication.

## Likely wrong approaches and challenges

- Treating the current `*.Husk` actor as the durable result conflates an airborne
  physics actor with a ground `Husk` positionable. Static actor traits cannot be
  safely switched after impact; a distinct configured ground form or another
  cohesive opt-in transform boundary is likely needed.
- A generic `SpawnActorOnDeath.RequiresEmptyCell` bolted on without defining
  terrain, dead/deferred actors, the dying actor, offsets, and simultaneous
  creators risks changing every ordinary death spawn. If extending a common
  trait is the simplest design, make the behavior explicit opt-in and default-
  compatible, and test its ordering contract directly.
- Checking `GetActorsAt(cell).Any()` during impact will see the falling husk and
  may see crash-killed actors pending removal. Checking only after taking a stale
  snapshot can instead miss a blocker or competing husk inserted before creation.
  Separate “impact completed” from “creation still valid” and make the final
  outcome deterministic.
- `HeliCrash` currently uses radial hit-shape damage. Do not make occupancy logic
  compensate for its limitations by special-casing infantry or applying a second
  damage pass; CNC-62 owns exact-cell damage. Test dead-versus-surviving actors
  independently, then repeat on CNC-62's commits.
- `HuskInfo.CanEnterCell` is intentionally permissive for info-level callers, so
  invoking it alone is not proof that the runtime cell is clear. Conversely,
  counting decorative actors without ActorMap blocking influence would reject a
  valid cell. Define “blocking” at the standard full-cell occupancy boundary.
- The impact position may not equal the original aircraft location, especially
  for moving plane-style falls; use the final world position/cell. Guard map
  containment and standard allowed terrain before reading cell layers or creating
  actors. Do not clamp an invalid impact onto an unrelated edge cell.
- Same-frame crashes can both observe an empty cell and enqueue two creations.
  Test and preserve a deterministic single winner without a global reservation
  structure or unbounded retry queue.
- Copying standard husk fields into every aircraft definition invites mapping,
  decay, capture, and later CNC-40/CNC-50 drift. Use an owning aircraft-ground-
  husk template plus the smallest actor-specific visual and `IntoActor` mapping.
- Capturing only proves the contract if the Engineer is consumed, the husk is
  removed, exactly one correctly typed aircraft appears, and normal orders/modules
  can use it. Sprite presence, targetability, capture requests, or transform logs
  alone are diagnostic, not acceptance.
- Full natural matches may not produce controllable exact-cell aircraft crashes
  or timely Engineers. Force the edge cases in a small full-engine map with real
  ordinary bots and all normal modules, then use a fresh long natural match for
  regression/endurance; do not wait repeatedly for rare coincidences.

## Competing systems and ownership

- Crash lifecycle owner: `FallsToEarth`/its activity owns ground-contact ordering
  and invokes the configured crash weapon. CNC-62 owns what damage is applied and
  to which exact-cell actors. CNC-44 owns only the post-damage eligibility and
  durable-husk transition. Keep these responsibilities separable.
- Rules owner: CNC aircraft rules own which source aircraft leaves which temporary
  and ground husk and which actor capture restores. Common CNC husk defaults own
  targetability, capture type, durability, decay, allowed terrain, and restored-
  health policy. Do not duplicate those policies in engine code.
- Occupancy owner: `ActorMap` plus each actor's `IOccupySpace` represents infantry
  subcells and full-cell vehicles/buildings/husks. The decision must inventory
  every surviving exact-cell influence, including simultaneous removals and a
  competing same-frame ground husk, without a world scan.
- Death-spawn owner: the source aircraft's `SpawnActorOnDeath` creates only the
  temporary falling form; any second transition must not recursively or
  accidentally trigger on combat destruction of the permanent husk. Other
  `SpawnActorOnDeath` consumers (vehicles, trees, civilians, buildings) are
  regression surfaces, not candidates for this behavior.
- Capture owner: `CaptureManager`, `Capturable`, `Captures`, and
  `TransformOnCapture` own player capture and the consumed-Engineer transition.
  `CaptureManagerBotModule` owns AI candidate scoring, reservations, rejection,
  retargeting, and capture orders. CNC-44 must expose a normal `HuskInfo` actor; it
  must not add a parallel Engineer or AI workflow.
- Normal restored-aircraft consumers: `SquadManagerBotModule` can claim restored
  Apache/Orca actors for air squads; `UnitBuilderBotModule` and helipad queues
  produce the same types; air repair/base modules reserve helipads; crate and
  targeting systems can retarget aircraft. `TRAN` can be reserved/ordered by
  `TransportManagerBotModule`, heavy-drop, and infantry-assault transport logic.
  Exercise Engineer capture alongside these normal modules so ownership changes
  do not create stale or duplicate claims.
- Normal husk consumers/competitors: attack, covert, rush, artillery, and stealth
  squads filter `HuskInfo`; force-fire can still destroy a targetable husk;
  standard `ChangesHealth` decays it; another Engineer or the bot capture manager
  may race to capture it. Test destruction/decay and human-versus-AI Engineer
  contention at least once.
- Shared cash/queues are not changed, but captured aircraft adds army value
  without consuming an Aircraft queue. Observe that existing UnitBuilder and
  helipad production continue normally and that the restored actor is neither
  queued nor charged as a new purchase. Do not adjust adaptive value/credit here;
  CNC-40 owns restored-husk credit.

## Cross-worker dependencies

- **CNC-62 is directly coupled and publication-blocking.** It will set impact
  damage for Apache/helicopter, Orca, A10, and transport helicopter, apply it to
  every actor in the exact crash cell, and may change `FallToEarth.cs`,
  `FallsToEarth.cs`, crash weapon rules, exact-cell actor enumeration, or removal
  timing. Before the first product change and again before publication, query for
  its branch/PR. If it exists, inspect its commits (never its worker spec), record
  the exact SHA, rebase/integrate according to the coordinator's branch order,
  and rerun every occupancy/timing test on the combined code. Crash damage must
  complete before CNC-44 eligibility. Do not duplicate or tune CNC-62's damage.
  CNC-44 cannot be proposed `Complete - testing` until this combined ordering and
  all exact-cell occupant outcomes are proven; if CNC-62 remains unavailable,
  publish only the safest scoped result as `First iteration - testing` with the
  dependency and unverified combined behavior explicit.
- **CNC-40** (adaptive credit for restored husks) and **CNC-50** (Engineer recovery
  toward capturable husks/buildings) are downstream behavior consumers, not
  prerequisites. Preserve a conventional `HuskInfo` + `Capturable: husk` +
  `TransformOnCapture` contract and exact restored `Valued` actor so those tasks
  can discover and credit aircraft husks without aircraft-specific exceptions.
  At spec time no relevant PR commits existed; if a CNC-40 PR appears and touches
  `TransformOnCapture`, `CaptureManagerBotModule`, or restored-husk credit, inspect
  only its commits before publication and test the combined capture result.
- CNC-87, CNC-40, CNC-41, and CNC-42 are claimed in this coordinated round; the
  isolated task packet predicts no direct product-code overlap. Do not read their
  packets/specs. Recheck only their PR diffs if the coordinator reports overlap
  with the crash files, CNC aircraft/husk rules, capture traits, or test map.
- No active CNC-44 or CNC-62 branch/PR was found during specification. This is the
  material cross-worker warning: the worker must not mistake the current radial
  `HeliCrash` behavior for CNC-62's required exact-cell prerequisite.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `not applicable — no narrative staged`
- Sol-high policy review: `not applicable — no review launched`
- Verdict and confidence: `Not applicable, high confidence: this task specifies a deterministic crash lifecycle, collision/occupancy invariant, and standard capture integration; it does not choose an AI strategy, priority, economy rule, targeting policy, or balance tradeoff.`
- Recommendations adopted as testable hypotheses: `None from policy consultation. Repository evidence instead motivates post-damage/deferred-removal, exact-cell, same-frame contention, normal Engineer capture, and standard-husk integration tests.`
- Recommendations rejected or deferred, with reason: `Policy consultation skipped as genuinely irrelevant under the role instructions. Crash damage tuning, infantry lethality, restored-health tuning, and AI recovery/credit policy remain frozen or assigned to CNC-62/CNC-40/CNC-50.`

## Acceptance and tests

### Literal black-box acceptance

In a fresh full-engine CNC skirmish containing at least one ordinary real AI with
all normal modules enabled, a player-owned airborne `ORCA` is destroyed by normal
combat over an empty, valid clear land cell. The ordinary falling actor visibly
reaches the ground and applies its crash event first. After impact, exactly one
durable, ground-blocking, targetable/capturable Orca husk remains in that exact
cell. A player-owned `E6` receives and completes the ordinary capture order; the
Engineer and husk disappear and exactly one live, player-owned `ORCA` appears,
with the captured facing and standard restored-husk health policy, and can accept
a normal move/attack/resupply order. The same final crash-and-capture outcome is
demonstrated for `HELI` and `TRAN`. In the same current build an `A10` crash
completes its visual/damage path but leaves no durable/capturable husk and can
never be restored. Evidence must show map/title/checksum, seed, factions, spawn
slots, options, ordinary bot names/types, source actor IDs/types and airborne
state, death and final impact cell/tick, all exact-cell occupant IDs/types and
alive/dead outcome, durable-husk ID/type, capture completion, Engineer removal,
restored actor ID/type/owner/health/facing, successful normal order, advancing
world ticks, headless MAX activation, and clean exit/artifact flush.

### Focused checks and instrumentation

- Before code, record base control from the focused harness: current `ORCA.Husk`
  falls and disappears, no `HuskInfo`/capturable ground actor remains, and an
  Engineer cannot complete a restoration. Also record current A10 behavior.
- Add proportionate deterministic tests around the selected lifecycle/eligibility
  boundary. At minimum falsify: empty exact cell accepts; a list of two or more
  occupants is fully inspected; all dead/removing occupants accept; any live
  occupant rejects regardless of enumeration order; self is excluded; invalid
  terrain/out-of-map rejects; and a second same-frame creator cannot overlap.
  Keep pure helper tests supplementary to full-engine evidence.
- Add rules/YAML validation that every authorized ground aircraft husk resolves,
  has `HuskInfo`, normal `husk` capturability, and exact `TransformOnCapture`
  mapping (`TRAN`, `HELI`, `ORCA`), while A10 has no permanent mapping. Run
  `make test`, targeted `dotnet test OpenRA.Test/OpenRA.Test.csproj` tests if added,
  `make check`, and `make check-scripts` if Lua is added. Shared-engine compilation
  is allowed; do not build/test/package another mod except unavoidable shared
  compilation.
- Add bounded event diagnostics only where evidence is otherwise ambiguous. One
  line per crash transition may include falling actor type/ID, impact tick/cell,
  configured ground-husk type or `none`, terrain validity, and outcome
  `created/rejected`. A rejection line may list the bounded exact-cell surviving
  blocker type/ID/state; a creation line may give the new actor type/ID. Never log
  every fall tick, scan the world, or leave an always-on noisy debug stream.
- The focused full-engine test map/script should emit machine-searchable assertions
  for request, crash damage completion, each occupant's pre/post health/death,
  deferred removal, eligibility decision, rejection reason, durable creation,
  capture candidate/order, reservation owner/competing consumer when AI capture
  is exercised, transform, and final actor/owner/order outcome. A missing assertion
  is an invalid run, not a pass.
- Enable existing `CaptureManagerBotModule.DebugLogging` only through the focused
  map override when needed to distinguish candidate rejection, Engineer
  reservation, competing capture/demolition/transport consumer, order issue,
  progress, and completion. Remove temporary product diagnostics before
  publication or reduce a genuinely useful handled warning to bounded opt-in
  logging; record the cleanup result.
- Validate no new exception/warning on missing actor mapping, invalid terrain,
  edge coordinates, disposed parent/effective owner, simultaneous creation, or
  capture during decay/destruction. Do not silently substitute a success when a
  configured actor is invalid: rules loading should fail actionably; runtime
  terrain/occupancy rejection should be explicit and safe.
- Performance expectation: eligibility work is event-driven at impact/removal and
  O(k) in actors influencing one exact cell, with no per-tick full-world scan,
  unbounded retry, nondeterministic collection choice, or persistent allocation
  queue. Compare paired benchmark CPU/tick data for a crash-stress map and a
  natural match; investigate a repeatable regression above roughly 5% in MAX
  throughput/actor-tick cost or sustained new allocation/GC pressure.

### Ordinary and differential games

- **Test 1 after cycle 1 — matched changed/control pair, full engine:** use the
  exact same focused `.oramap`, content, factions, ordinary AI types, spawn points,
  seed, options, initial actors, and scripted timing on changed HEAD and the
  isolated old-control SHA `419bee2531d4802bf922c3597b42c6eeb75ab250`.
  Run headless MAX with normal AI/modules from tick 1. Force one airborne Orca to
  die over an empty valid cell. Failure hypothesis: the new transition fires too
  early/late or the harness merely sees the temporary fall. Changed pass evidence
  is a durable capturable ground husk after the fall; control evidence must show
  the same crash but zero durable husks. If one game slot cannot run the old build
  safely, run serially; do not compare against a different bot personality.
- **Type/exclusion ladder:** in a harder fresh game vary altitude, facing, owner,
  and timing and exercise `TRAN`, `HELI`, `ORCA`, and `A10`. Require exactly one
  durable mapped husk for each authorized type and none for A10. Capture each
  authorized type and issue an ordinary post-capture order.
- **Occupant ladder:** progress from empty cell to one low-health infantry, stacked
  infantry in all available subcells, mixed infantry/vehicle, multiple occupants
  with only one killed, a crash-killed vehicle plus a surviving structure, dead
  actors still pending removal, and an adjacent-cell decoy. Record every actor,
  final health/death/removal state, and the one expected create/reject result.
- **Timing/geometry ladder:** vary crash tick relative to actor entry/exit and
  destruction, run two same-cell crashes in one frame, crash at a map border, and
  use allowed clear/rough/road/Tiberium/beach plus invalid water/cliff cells as the
  map permits. Require no overlap, out-of-bounds access, clamp-to-wrong-cell, or
  exception.
- **Capture/competition ladder:** one fresh game uses a human Engineer; another
  gives an ordinary AI one or more idle `E6`s and a nearby aircraft husk while
  normal capture, demolition, transport reservation, squad, production, and
  repair modules remain enabled. Require one reservation/order winner, completed
  transform, no duplicate claimant or purchase charge, and ordinary module use of
  the resulting aircraft. Add a second Engineer or expiring/attacked husk to force
  invalidation/recovery.
- **Save/load:** save after a valid ground husk exists and before capture, reload,
  and complete capture with exact type/owner. This catches persisted actor/mapping
  defects. Then repeat the same outcome from a fresh game; a reloaded state is not
  sole acceptance or final regression.
- **Natural endurance:** run at least one fresh ordinary full AI match at headless
  MAX to natural game over, on an aircraft-producing map/faction setup. Seek at
  least one natural authorized aircraft crash and judge any observed occupancy/
  capture outcome; regardless of rarity, prove no desync, exception, stuck MAX,
  broken aircraft production/repair/squads, or regression to ordinary vehicle
  husks. The forced full-engine cases remain causal evidence when natural capture
  is rare.
- After CNC-62 commits are available, repeat the matched empty-cell pair, full
  occupant matrix, type/exclusion case, and capture case on the combined code.
  Record exact CNC-62 SHA and prove its damage log/result precedes eligibility.

Every game or pair must record before launch: the failure hypothesis, the new
perturbation, exact failure signal, and player-visible pass evidence. Preserve the
manifest, map checksum, commit/content checksum, seed, lobby commands, bot/faction/
spawn configuration, console/debug logs, benchmark/replay/save paths, last tick,
and exit reason outside Git under the assigned analysis directory. Use the
global resource wrapper and isolated support directories.

### Old-behavior control and required improvement

- Old control is the exact base SHA `419bee2531d4802bf922c3597b42c6eeb75ab250`
  in an isolated worktree, because no same-build feature toggle exists at spec
  time. If the implementation adds a clean rules toggle for testing, it may be a
  secondary same-build control, but do not add a player-facing option solely for
  this task.
- Keep map bytes/checksum, scripts, factions, seed, spawn points, lobby options,
  ordinary bots, initial actor IDs/types/positions/health, kill timing, and runtime
  limit matched. A control run that fails to load the same harness or does not
  reach the same crash is invalid.
- Comparative metrics: qualifying crashes; exact-cell occupants and deaths;
  durable husks after fall/removal; invalid overlapping/duplicate husks; successful
  captures; correct restored type/owner; time from impact to durable husk and from
  husk to capture; post-capture useful order/module adoption; fatal/warning/desync
  count; world ticks per wall-clock second; CPU/GC benchmark signals.
- Required improvement in exercised scenarios is decisive functional completion:
  changed code produces and restores all three authorized aircraft (`3/3` minimum
  in the type batch, with `0` wrong/overlapping results), while old control
  produces `0/3` durable capturable aircraft husks. Changed and control must both
  preserve A10 at `0` permanent husks. In the occupancy matrix, changed code must
  exactly match every create/reject oracle; logs that only show the feature fired
  are insufficient.
- This is not an AI-strategy task, so changed AI need not win more matches. It
  must not materially degrade normal AI aircraft production, squads, Engineer
  capture behavior, match completion, or MAX cost. A loss/tie is investigated only
  when task-relevant metrics show restored aircraft or competing modules caused
  it; do not tune AI or balance to manufacture a win.

### Adversarial cases

After the latest relevant product fix and after normal acceptance first passes,
run at least these distinct clean full-engine scenarios with ordinary real AI and
normal modules enabled. A focused map may force the event; a passive/custom bot or
manager-only fixture does not count.

1. **Stacked and partial survivors:** crash an authorized helicopter into all
   infantry subcells and a matrix of mixed occupants. Failure hypothesis: the
   implementation accepts after the first death or rejects on a dead-but-present
   actor. Perturbation is reordered actor creation plus (a) all infantry killed,
   (b) one infantry survivor, and (c) killed infantry plus one live vehicle or
   building. Failure signal is any overlap/wrong create-reject result. Pass is one
   husk only for the all-dead case and none whenever any blocker survives, with
   each occupant's final state evidenced. Repeat on CNC-62's combined SHA.
2. **Same-frame timing and contention:** land two `TRAN`/`HELI`/`ORCA` falling
   actors on one empty cell in the same frame while a moving survivor exits or
   enters at the frame-end boundary. Failure hypothesis: stale snapshots create
   two husks or timing nondeterministically suppresses/overlaps. Perturb actor
   insertion order and repeat the same seed/setup. Failure is more than one
   full-cell husk, a live overlap, or divergent synchronized results; pass is one
   deterministic winner when clear and no creation when the blocker survives.
3. **Terrain and map boundary:** crash each authorized type at the edge and across
   standard allowed and invalid terrain. Failure hypothesis: unchecked cell/
   terrain access crashes, clamps to an adjacent cell, or creates an unusable
   husk. Failure is exception/desync/out-of-map or a husk outside the standard
   allowed-terrain contract; pass is correct exact-cell creation on allowed
   terrain and safe rejection elsewhere, with A10 still negative.
4. **Capture, destruction, and competing managers:** place a fresh valid aircraft
   husk between human/AI Engineers, an enemy force-fire threat, normal Engineer
   capture manager, demolition manager, and transport reservations; separately
   approach standard decay. Failure hypothesis: duplicate reservations, stale
   capture after husk destruction, wrong transform owner/type, or restored
   aircraft double-claimed. Pass is exactly one valid capture or clean
   invalidation/reassignment, Engineer consumption only on completed capture, and
   normal HELI/ORCA squad or TRAN transport ownership afterward.
5. **Crash-stress/endurance:** force a bounded burst of many crashes spread over
   distinct cells during a real-AI MAX match, mixing blocked/clear outcomes, then
   continue to natural conclusion. Failure hypothesis: per-crash allocations,
   unbounded deferred tasks, or leaked invalid actors reduce throughput or desync.
   Pass is exact counts, no duplicates/leaks/fatals, stable deterministic replay,
   and no repeatable material benchmark regression versus matched control.

If any adversarial failure causes a product fix, restart the three-clean-scenario
minimum for all materially affected cases, then rerun literal acceptance.

### Final regression

After the last product change, required Terra checkpoint, and CNC-62 integration,
run a fresh (not reloaded) headless MAX full-engine CNC game with an ordinary real
AI and every normal module enabled. Use the literal clear-land Orca crash/capture
as the central outcome, but in the same scenario also force: stacked infantry all
killed before eligibility, a mixed cell with one surviving vehicle that suppresses
another husk, clear crashes for `HELI` and `TRAN`, and an A10 negative crash. Require
exactly three correctly mapped durable husks before capture, zero blocked/A10
husks, successful normal Engineer transforms into `ORCA`, `HELI`, and `TRAN`, and
a successful ordinary post-capture action for each. Evidence must prove the exact
branch/head and CNC-62 SHA, map/options/bots/actors, damage-before-decision order,
occupant outcomes, capture transitions, advancing ticks, MAX activation, no
fatal/desync, and flushed artifacts. Then run `make test`, relevant focused tests,
`make check`, and applicable required GitHub checks on the published head. This
final literal regression cannot be replaced by a save reload, passive fixture,
unit test, log-only assertion, or an earlier pre-fix game.

## Implementation and publication plan

1. Recheck the recorded base, applicable `AGENTS.md`, and CNC-62 branch/PR status;
   capture an old-control harness result before changing product code.
2. Establish the smallest opt-in lifecycle boundary that resolves crash effects
   and removals before an exact-cell, valid-terrain, all-survivors occupancy
   decision. Keep the algorithm generic/default-preserving and event-bounded.
3. Define cohesive CNC ground-aircraft-husk rules that reuse standard `^Husk`
   behavior and map `TRAN`, `HELI`, and `ORCA` back to themselves. Leave A10 and
   unrelated actors/mods outside the opt-in surface.
4. Add focused invariant/rules tests and only the bounded diagnostics needed to
   distinguish impact, victim death/removal, rejection/blocker, creation,
   capture/reservation/order, and final transform. Do not tune crash damage or
   other frozen values.
5. Make the first post-change behavioral evidence the matched full-engine
   changed/base Orca pair, then climb through types, occupants, frame-end
   contention, terrain/edges, capture/normal-module contention, save/load, and a
   natural MAX match. Use factual Commenters and required Terra cycle reviews.
6. Integrate the eventual CNC-62 commits at the assigned branch boundary and
   rerun every damage/occupancy case. Do not claim completion on base `HeliCrash`
   evidence alone.
7. Remove noisy temporary diagnostics, run the final fresh literal regression and
   static/build/check gates, and write the task report with exact control/current
   SHAs, maps/seeds/artifacts, performance/determinism, dependency, and risks.
8. Commit/push only the task branch, open one PR against the recorded cumulative
   base, wait for required checks and the final Sol-high review/one-response gate,
   and propose `Complete - testing` only when CNC-62 and every stated outcome are
   proven; otherwise hand off `First iteration - testing` with exact gaps.

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
`/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-5-cnc44/cycle-review-05/CYCLE-REVIEW.md`.

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
4. For AI-policy work, copy that narrative (do not symlink it) to the Policy Reviewer
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

For CNC-44, step 4 remains not applicable unless the worker materially expands
the task into AI policy (which is not authorized). Continue factual Commenter
reviews after materially judged games; do not launch routine Policy Reviewers for
this deterministic engine/config task.

If a policy problem persists after at least ten completed full-engine game tests,
the worker may ask exactly one Sol 5.6 xhigh `policy-escalation` instance. First
write a new narrative stating the game-test count, repeated failure pattern,
attempted policies, evidence for/against each, and focused questions. The
escalated reviewer still reads only the design document and narrative. Record use
in the assignment field. Never invoke it before test 10 or invoke it twice for one
task. This escalation is expected to remain unused for CNC-44 because AI-policy
changes are out of scope.

Prefer the full engine and real bot types. On Linux use the explicit headless MAX
path when graphics/input are irrelevant. Prove the current run loaded the intended
map, bots, actors, options, activated headless MAX, advanced ticks, flushed logs,
replay/benchmark evidence where configured, and produced the final outcome. A
passive fixture or manager-only simulation is not sole proof.
Use focused setup maps to accelerate reproduction, but before acceptance run a
fully enabled scenario containing every relevant ordinary module. Headless MAX
never replaces required graphical, rendering, input, lobby, or platform checks.

Force every inventoried competing system to act in at least one integrated test.
Routing/island topology is not intrinsically changed by CNC-44, but the AI
Engineer/captured-aircraft competition test must still prove a reachable approach
and ordinary module adoption on connected terrain; do not infer routing changes
or expand into transport routing. If the event does not occur, change the seed,
map, duration, starting actors/resources, bots, or focused setup; do not pass an
unexercised path. Judge every unexpected behavior explicitly as acceptable or
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
deferred work, and remaining risks. It must also record the exact CNC-62 commit/
PR used, or explicitly state that the direct dependency prevented completion.

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
| 1 | Commit `396c8106d9`: opt-in `SpawnActorOnDeath.RequiresValidHuskCell`; standard `TRAN/HELI/ORCA.GroundHusk` rules and mappings; A10 unchanged | A post-impact spawn could see its own transient/deferred victims, miss terrain, include A10, mis-map a type, or produce an unusable restoration. Reused exact control map SHA-256 `72e5c7d3fad5bcc5aeda991a3a9dfc919d1418674e73d77cac9f4b2e723b43ac`, seed 44001; type ladder seed 44002 varied types, altitude, facing, terrain, capture, and normal AI adoption. | Release build pass; `make test` pass; focused NUnit 5/5 pass; `make check` pass; control run 004 and changed run 001 passed at tick 90. Type runs 002–004 reached tick 425 and were materially useful but invalid as full acceptance: run 002 proved Rock rejection; runs 003/004 proved 3/3 mapped captures but used late facing assertions after AI orders. Linux/Windows GitHub CI passed at product head `396c8106d9`. | Control narrative: `analysis/worker-5-cnc44/commentary/control-batch-01/NARRATIVE.md`. Matched-pair narrative: `analysis/worker-5-cnc44/commentary/cycle-01-pair/NARRATIVE.md`. Type/capture narrative: `analysis/worker-5-cnc44/commentary/cycle-01-type-capture/NARRATIVE.md`. Final Sol-high review: `analysis/worker-5-cnc44/final-review/REVIEW.md`, verdict `ready`, required fix `none`. Policy review not applicable. | Control: zero durable ORCA/A10. Changed empty Clear: one ORCA ground husk. Type run 004: exactly one TRAN/HELI/ORCA durable husk, zero A10, 3 Engineers consumed, exact restored type/owner/25% health, no ground husks left, and all 3 aircraft alive/moved. TRAN retained captured facing; HELI/ORCA facing remained unproven because normal air modules redirected them before the delayed check. No fatal/desync; run 002 safely rejected ORCA on Rock. | Keep cycle 1 implementation. Final review found no safe scoped product correction. Handoff `First iteration - testing`: capture-time HELI/ORCA facing, occupant/timing/contention/boundary, save/load, natural endurance, final regression, and CNC-62 combined ordering remain open. |

## Handoff receipt

- Proposed status: `First iteration - testing`
- Final branch/head: `agent/round-20260807-cnc44-aircraft-husks`; product head `396c8106d9cec1c84ed0c2e44cd34ce0d0ef4772`, followed only by this handoff-metadata commit
- PR and checks: `#85` (`https://github.com/Realpra1/LibertyDawn/pull/85`); Linux and Windows .NET 6.0 CI passed at product head; final metadata-only head rechecked before return
- Cycles used: `1/20`
- Acceptance evidence: matched clear-land ORCA control/current result; current type batch created/captured/restored 3/3 exact types at 25% health with all Engineers/husks consumed and successful movement, with A10 excluded
- Adversarial evidence: safe Rock rejection and distinct altitude/facing/type/module-adoption perturbations; not complete acceptance because HELI/ORCA capture-time facing and the required occupant/contention/boundary portfolios remain open
- Old-behavior control and comparative result: exact base `419bee2531`, map SHA-256 `72e5c7d...`, seed 44001; control `0` durable ORCA versus current `1`, both `0` durable A10
- Match narratives and routine policy-review conclusions: three fresh factual Commenter narratives under `analysis/worker-5-cnc44/commentary/`; policy review not applicable for deterministic engine/config work
- Terra cycle code reviews and dispositions: none required; only cycle 1 occurred
- Sol-xhigh policy escalation (unused, or test count/path/conclusion): unused; no AI-policy issue and only 7 counted full-engine tests
- Final regression: not run; CNC-62 is unavailable and directly completion-blocking
- Error/warning and diagnostic-cleanup result: Release/Debug builds and checks passed with zero warnings/errors; no fatal/desync in judged current runs; retained diagnostics are one bounded debug record per opted-in transition
- Performance/determinism result: event-driven O(k) exact-cell work, deterministic all-blocker decision and frame-end revalidation; short matched evidence showed no claimed regression, but stress/endurance measurement remains open
- Deferred work: none proposed outside the explicit completion evidence/dependency portfolio
- Known failures/risks: CNC-62 damage-before-eligibility unverified; full occupant/removal, same-frame contention, map-edge/terrain, save/load, capture invalidation/manager contention, natural/stress endurance, three clean adversarial scenarios, HELI/ORCA capture-time facing, and fresh final regression remain
- Relevant artifact paths: `analysis/worker-5-cnc44/baseline-control/`, `analysis/worker-5-cnc44/cycle-01/`, `analysis/worker-5-cnc44/commentary/`, and `analysis/worker-5-cnc44/final-review/REVIEW.md`
