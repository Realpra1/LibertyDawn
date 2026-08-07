# Worker State: CNC-43

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `worker-3`
- Task: `CNC-43 — MCV crush flavor`
- Status: `Complete - testing`
- Common base branch/SHA: `agent/cnc38-early-viki-infantry-rush` / `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
- Task branch: `agent/round-20260806-cnc43-mcv-crush-flavor`
- Intended PR base: `agent/cnc38-early-viki-infantry-rush`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `1`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260806-bug-polish-01/WORKER-3-CNC-43/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-3-cnc-43`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `46`
- Sol-xhigh policy escalation: `unused (requires at least 10 game tests; one maximum)`
- PR: `#78 — https://github.com/Realpra1/LibertyDawn/pull/78 — mergeable CLEAN; no status checks reported/configured`

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

The MCV is an unarmed, slow-to-build strategic recovery and expansion asset, but
its current `heavywheeled` locomotor can crush only crates and infantry. The
Mammoth Tank's `heavytracked` locomotor additionally crushes ordinary walls and
`heavywall` actors, which include concrete walls, most vehicles, and selected
light defenses. The literal request is a flavor correction: give every normal
CNC `mcv` the Mammoth's complete crush-class capability without turning the MCV
into an AI combat unit or changing unrelated movement or balance.

After the change, a player who deliberately force-moves an MCV through an enemy
target crushable by a Mammoth should see that target destroyed and the MCV pass
through. Existing crate and infantry crushing must continue. The MCV should still
move like the present MCV on every terrain, remain unarmed and itself uncrushable,
deploy normally into a Construction Yard, and remain excluded from ordinary AI
combat squads. Mammoth, Stealth Tank, wall, vehicle, defense, and AI policy values
must not change.

## Authoritative behavior

- Implement the authoritative task text literally: **Config only: give MCVs the
  Mammoth tank's crush capabilities. Do not change AI behavior or unrelated
  balance.**
- The resolved crush-class set used by `mcv` must equal the pinned Mammoth
  Tank/`heavytracked` set: `wall, heavywall, crate, infantry`. Equality matters;
  do not add a fifth class or omit an existing class.
- Scope the new capability to the normal `mcv` actor and the Construction Yard's
  transform-into-mobile validation path. All normal instances of that actor must
  inherit it: starting MCVs, produced MCVs, campaign MCVs whose map rules merely
  disable production or alter rendering, and MCVs created by repacking `fact`.
- Preserve the current MCV's `heavywheeled` terrain speeds exactly: Clear 90,
  Rough 63, Road 190, Bridge 190, Tiberium/BlueTiberium/RedTiberium 63, Beach 50.
  Preserve `Mobile.Speed: 60`, inherited turn speed, health, armor, cost,
  production/deployment rules, cloak conditions, repairability, targetability,
  passenger behavior, and `-Crushable` state.
- Preserve the Mammoth Tank's existing `heavytracked` locomotor and crush values.
  The Mammoth remains the reference; this task must not tune it in anticipation
  of later Mammoth AI work.
- Preserve `heavywheeled` behavior for the Stealth Tank: it may crush crates and
  infantry, not `wall` or `heavywall`. Do not broaden any other actor's crush set.
- Crushing remains hostile-only under the existing `Crushable` contract:
  `CrushedByFriendlies` defaults to false. An MCV must not crush friendly or allied
  infantry, walls, vehicles, or defenses.
- Capability does not imply policy. Player or pre-existing movement orders may
  exercise it, but no AI module may be taught to seek, target, reserve, or issue
  crush orders to an MCV.

## Forbidden behavior and failure signals

- Any product-code, AI-code, AI-rules, weapon, cost, health, speed, turn-rate,
  path-cost, target-class, wall-class, or Mammoth-value edit is out of scope.
- Assigning `mcv` directly to `heavytracked` is a failure: that silently changes
  wheeled terrain/path-cost behavior to tracked behavior, materially buffing rough,
  Tiberium, and beach movement while nerfing road movement.
- Expanding the shared `heavywheeled` locomotor is a failure unless all non-MCV
  users are given an exactly equivalent replacement. On the pinned base,
  `heavywheeled` is also used by `stnk` and by `fact`'s
  `TransformsIntoMobile`; a global edit would incorrectly let Stealth Tanks crush
  walls, vehicles, and light defenses.
- Changing targets' `Crushable.CrushClasses`, enabling friendly crushing, or
  making MCV/Mammoth actors crushable to manufacture a passing scenario is a
  failure.
- Editing `mods/cnc/rules/ai.yaml`, any `*BotModule` implementation, squad policy,
  target priority, production weight, or order logic is a failure. MCVs must not
  join attack squads or receive authored combat/crush orders.
- A configuration that works only in a custom test map, only for one faction, only
  for starting actors, or not after Construction Yard repacking is incomplete.
- An MCV destroying only infantry or collecting a crate does not prove the change;
  both already work at the base SHA. Reaching a request, route, collision, or
  movement log without the enemy actor's visible destruction is not acceptance.
- If a normal move/deploy is blocked, an allied actor is killed, a Stealth Tank
  gains wall/heavywall crushing, an MCV changes terrain timing, or a normal bot
  begins using an MCV offensively, treat it as a regression.
- Repeated identical cheese tests, passive/custom bots, isolated manager fixtures,
  static YAML alone, or logs that merely say an order was issued are not sufficient
  completion evidence.

## Relevant current implementation and control behavior

- At `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`,
  `mods/cnc/rules/vehicles.yaml` defines one shared `MCV` actor. It inherits
  `^Vehicle`, overrides `Mobile.Speed` to 60 and `Mobile.Locomotor` to
  `heavywheeled`, transforms into `fact`, and removes the inherited `Crushable`
  trait. There are no faction-specific MCV actors.
- `HTNK` inherits `^Tank` but overrides `Mobile.Locomotor` to `heavytracked`.
  `mods/cnc/rules/world.yaml` gives `heavytracked` the crush set
  `wall, heavywall, crate, infantry`. `HTNK` also removes inherited `Crushable`.
- `heavywheeled` has the crush set `crate, infantry` and wheeled terrain values.
  Repository-wide CNC references show exactly three uses: `MCV`, `STNK`, and
  `FACT.TransformsIntoMobile`. `heavytracked` is used only by `HTNK`.
- `FACT.Transforms` creates `mcv`; its `TransformsIntoMobile.Locomotor` is
  `heavywheeled`. That companion locomotor reference participates in whether a
  Construction Yard may transform into a mobile actor and must stay coherent with
  the MCV's actual movement family.
- The engine owns crush capability in `LocomotorInfo.Crushes`, not `Mobile`.
  `Locomotor` uses the set for path blocking; `Mobile.EnteringCell` warns eligible
  actors and `Mobile.FinishedMoving` calls `INotifyCrushed.OnCrush`. The default
  `Crushable` implementation rejects allied crushers and kills the target only
  when its `CrushClasses` overlap the locomotor's set.
- Current relevant target classes are: infantry defaults to `infantry`; `SBAG`
  is `wall`; `BRIK`, the inherited `^Vehicle` `Crushable`, and GUN/SAM/GTWR are
  `heavywall`; crates use `crate`. Thus Mammoth capability means more than walls:
  it includes the currently authored heavywall vehicles/light defenses.
- All campaign `MCV:` overrides at the base either disable production or, in one
  case, alter the player palette. None replaces `Mobile`, so the core actor config
  controls campaign crushing too.
- History confirms intentional ownership: `d6aba2e6af` first added MCV crate and
  infantry crushing; `7441badc96` migrated per-actor `Mobile.Crushes` into the
  shared `heavywheeled` locomotor while moving Mammoth to `heavytracked`;
  `d15c3739c8` made most vehicles/light defenses `heavywall` crushables and kept
  MCV/Mammoth themselves uncrushable.
- There is no committed CNC test asserting MCV/Mammoth crush parity. The normal
  mod gate is `make test` / `./utility.sh cnc --check-yaml`; resolved rule output
  is available through `./utility.sh cnc --resolved-rules ACTOR`.

## Likely wrong approaches and challenges

- Reusing `heavytracked` looks like a one-line solution but imports Mammoth terrain
  speeds and costs. Reusing or mutating `heavywheeled` looks equally small but
  buffs `stnk`. The simplest safe design is actor-scoped locomotor configuration
  that copies current MCV terrain semantics and Mammoth crush classes; another
  config-only design is acceptable only if resolved rules prove the same ownership
  and zero spillover.
- Do not put a `Crushes` field back under `Mobile`; the engine migrated ownership
  to `LocomotorInfo`, and YAML lint should reject or ignore the wrong boundary.
- Keep the `FACT.TransformsIntoMobile` reference coherent. Updating only the live
  MCV can leave transformation validation using a locomotor with different
  blocking semantics.
- Do not solve actor-scoping by changing `Crushable` classes on every target. That
  changes what Mammoths, tanks, and all other locomotors can crush and creates a
  broad balance change.
- MCV and Mammoth parity must compare the resolved set, not textual ordering or a
  map-local override. Conversely, copying Mammoth's locomotor wholesale is not
  parity because terrain is not a crush capability.
- A target can evade (`WarnProbability`) or a pathfinder can route around an open
  obstacle. Use deterministic immobile wall/defense targets, a one-cell corridor,
  explicit force-move, and final actor-death/objective evidence rather than
  inferring capability from the route chosen.
- Friendly and allied `Crushable` actors intentionally remain blockers. Include
  a valid detour in safety tests; a closed allied wall that correctly blocks an MCV
  is not evidence of broken crushing.
- A dedicated locomotor adds one world locomotor/cache. Keep the definition static
  and allocation-free per tick; compare MAX benchmark/tick evidence so a surprising
  initialization or pathing regression is not dismissed as "config only."
- Avoid product logging. A focused test map may emit bounded one-shot setup,
  order, kill, arrival, transform, and result markers. Remove noisy temporary
  per-tick diagnostics before publication and keep generated maps/artifacts ignored.

## Competing systems and ownership

- `Locomotor` owns terrain movement, actor blocking, and the crusher's class set;
  `Crushable` owns target classes, evasion, allied safety, sounds, and final death.
  This task changes only the MCV-side capability configuration.
- `Transforms` owns MCV-to-Construction-Yard deployment; `FACT.Transforms` and
  `TransformsIntoMobile` own the reverse lifecycle. They compete for MCV movement
  state and must still transform successfully before and after crushing.
- `McvManagerBotModule` is the ordinary AI owner of idle MCVs. It requests a
  replacement when no yard exists, issues `Move`, and queues `DeployTransform`.
  It must retain priority over bot MCV lifecycle; this task must not add another
  order source or make it pursue crush targets.
- `BaseBuilderBotModule` opening and smart-economy managers request expansion MCVs.
  `UnitBuilderBotModule` services production requests and owns the shared
  Vehicle.GDI/Vehicle.Nod queues and cash; personality configs mark `mcv` externally
  managed so ordinary unit selection does not treat it as combat production.
  Exercise request, queue, production, spawn, move, and deployment in at least one
  full game, not just a pre-placed player MCV.
- Airstrip and Weapons Factory production, cash, and any concurrent harvester,
  vehicle, upgrade, or smart-economy requests contend with an MCV build. Evidence
  must distinguish an unbuilt MCV from insufficient cash, a busy queue, a rejected
  request, or a live MCV already satisfying the request.
- `SquadManagerBotModule` excludes `mcv` from squads and lists it as a protection
  asset. Early-infantry-rush and opening-garrison logic classify it as a
  construction/emergency anchor. Crate collection uses MCV presence only for its
  no-base emergency and excludes MCVs from collectors. Allied-aid logic counts an
  ally's MCV for recovery need. These systems may react to or defend an MCV but
  must not become MCV order owners.
- Enemy air/ground target selection can prioritize or attack MCVs while it moves.
  Include real enemy pressure so success is not confused with a passive target
  range. The MCV's existing Repairable/FIX and Passenger/Cargo contracts remain
  available, although current ordinary squad/transport configs do not enlist it.
- Human move/force-move and deploy orders remain the direct black-box control.
  Normal AI modules must be active in every full-engine scenario even when a
  scripted human-owned MCV lane makes the critical collision deterministic.

## Cross-worker dependencies

- CNC-45 (Economy troop production/use) is the material future overlap. It may
  later add **bounded Mammoth** crush orders, but at spec time it is pending and no
  local branch or open PR matching CNC-45 exists. Do not implement any part of it.
  Preserve Mammoth crush values and all AI code/config. Before publication, check
  whether a CNC-45 branch/PR has appeared; if so, inspect only its commits and
  report any overlap, especially changes to `heavytracked`, Mammoth rules, crush
  targeting, or AI orders.
- Open PR #31 (`agent/cnc-mcv-wall-enclosure`, commit `4e65c05fed`) is already in
  this base through merge `20fbcb002f`. It edits wall-planning AI and `ai.yaml`, not
  MCV/locomotor rules. Its behavioral interaction is intentional: allied enclosure
  walls remain uncrushable by the MCV because friendly crushing is false. If that
  PR's head changes before publication, inspect its new commits; do not edit its
  files or absorb its policy into this task.
- No active CNC-43/MCV-crush branch or PR existed at spec time. The intended base
  branch had advanced from the pinned SHA by two coordinator/role-launcher commits
  (`109209131b`, `96ca6049b5`) with no CNC product delta. Implement from the
  recorded SHA and report any later product-file change on the intended PR base.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `not applicable — literal config capability only; no AI behavior or policy is permitted`
- Sol-high policy review: `not applicable — policy review would expand a non-policy task`
- Verdict and confidence: `Skipped with high confidence: the authoritative text fixes a crush-class parity fact and explicitly forbids AI behavior changes.`
- Recommendations adopted as testable hypotheses: `None from policy review. Repository evidence drives hypotheses: actor-scoped parity, unchanged terrain movement, hostile-only crushing, and unchanged MCV AI lifecycle.`
- Recommendations rejected or deferred, with reason: `Any MCV targeting, combat-order, production-priority, or strategic-use advice is rejected as CNC-45-adjacent/out of scope.`

## Acceptance and tests

### Literal black-box acceptance

Create or adapt an ignored focused CNC map that runs the full game engine with at
least two ordinary real AI players and all their normal modules active. Give a
scripted human/test player one normal `mcv` and an unobstructed one-cell-wide lane
containing hostile representatives of every Mammoth crush class: a crate,
infantry (`e1`), `wall` (`sbag`), and `heavywall` (at minimum `brik`; also include
one normal heavywall vehicle or light defense such as `apc`/`gun` if geometry
permits). Issue the same player-equivalent force-move through the lane that a
human can create from the UI. Do not override the `mcv`, Mammoth, target, locomotor,
or crush rules in the map.

Pass only when current-run evidence proves the intended map, seed, factions,
ordinary bot types, MAX speed, actors and owners loaded; the MCV received the
force-move; every hostile target in the lane was actually killed/removed by that
MCV; the MCV crossed the final cell alive; and it then successfully deployed into
`fact`. Preserve a replay/benchmark plus bounded setup, actor-death, arrival,
transform, and final objective/result evidence. A route/order log, target health
change without death, or destruction by an AI weapon does not pass.

Run the same scenario at the pinned base as the old control. The expected base
outcome is that its MCV still collects/crushes crate and infantry but cannot cross
the first wall/heavywall; the changed MCV must complete the lane. In a parallel
source-reference lane, a Mammoth must complete the crush matrix in both builds.

### Focused checks and instrumentation

- Before the first code/config edit, save base resolved-rule evidence for `World`,
  `MCV`, `HTNK`, `STNK`, and `FACT` under the ignored analysis directory. After
  each relevant edit, rerun the same queries and compare:
  `./utility.sh cnc --resolved-rules World`,
  `./utility.sh cnc --resolved-rules MCV`,
  `./utility.sh cnc --resolved-rules HTNK`,
  `./utility.sh cnc --resolved-rules STNK`, and
  `./utility.sh cnc --resolved-rules FACT`.
- Assert from resolved/runtime evidence that MCV and Mammoth crush sets are equal;
  MCV terrain speeds equal the old `heavywheeled` values; STNK remains
  `crate, infantry`; `FACT.TransformsIntoMobile` uses the coherent MCV locomotor;
  Mammoth values and target `Crushable` classes are unchanged.
- Run `git diff --check`, inspect `git diff -- mods/cnc`, and explicitly prove no
  changes under `OpenRA.*`, `mods/cnc/rules/ai.yaml`, other mods, maps, target
  `Crushable` rules, or generated/build output. Config-only means the publication
  diff should remain in the smallest owning CNC rules files.
- Run the CNC YAML/build gate under the single large-build slot, for example:
  `python3 .agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py --lock-dir /root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks --resource large-build --capacity 1 --slots 1 -- make test`.
  Finish with `make check`, `make check-scripts`, and
  `dotnet test OpenRA.Test/OpenRA.Test.csproj --configuration Debug --nologo`
  as applicable to the PR checks. Shared engine compilation is allowed; do not
  build or test unsupported game mods.
- Use only bounded map/test diagnostics. Record actor ID/type/owner and starting
  cell, issued order/destination, each target ID/class and actual killer, MCV
  arrival, transform result, AI MCV request/queue/production/deploy milestones,
  and final objective. If evidence is ambiguous, add one-shot diagnostics that
  distinguish order issuance, path rejection, blocker identity, collision,
  target death, competing order owner, and final outcome. Remove temporary noisy
  instrumentation before publication.
- Run every game through the global lock. One game uses one slot; a paired batch
  uses both:
  `python3 .agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py --lock-dir /root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks --resource game --capacity 2 --slots 2 -- python3 launch-ai-parallel.py ...`.
  Keep maps, support dirs, settings, logs, replays, benchmarks, saves, displays,
  and ports isolated; poll within 60 seconds and normally cap at 30 minutes.

### Ordinary and differential games

1. **Cycle-1 matched full-engine pair — capability delta.** Immediately after
   the first config change (static lint may run alongside it), run changed and
   pinned-base builds against identical copies of the focused literal map with
   the same content except the rules build, seed, factions, starts, options,
   actors, scripted force-move, and ordinary VIKI/Brutalis-class bots. Failure
   hypothesis: the change still lacks a class, spills into terrain behavior, or
   the harness credits another killer. Base should stop at wall/heavywall;
   changed must destroy the full matrix, arrive, and deploy; Mammoth reference
   behavior must match between builds.
2. **Lifecycle/production full-engine game.** In a harder focused or ordinary
   connected map, make normal BaseBuilder/UnitBuilder/McvManager systems request,
   queue, pay for, produce, move, and deploy an MCV while other vehicle/economy
   requests contend. Also exercise a `fact` repack to `mcv` where enabled. Failure
   hypothesis: the actor-scoped locomotor breaks transform validation, queue
   lifecycle, or grants offensive AI behavior. Pass evidence is a produced or
   repacked normal MCV using the expected locomotor, retaining ordinary movement,
   then deploying under real enemy pressure without an authored crush target/order.
3. **Ordinary matched control games.** Run at least one changed/base matched pair
   on a normal connected multiplayer map with all normal modules and fixed seed.
   Track initial deployment, expansion-MCV request/queue/production/deploy ticks,
   MCV orders, construction-yard count, cash/queue contention, MCV survival, and
   benchmark timing. Failure hypothesis: actor-scoped config displaced normal AI
   lifecycle or movement. The changed build need not win more, but it must not
   add MCV combat orders or materially regress these measures.
4. **Natural-conclusion endurance.** Run at least one real ordinary-AI match at
   headless MAX to natural game over after the latest relevant fix. Include an AI
   personality that builds Construction Yard enclosure walls and one that requests
   expansion MCVs. Verify map/bots/options/ticks, normal production and deployment,
   absence of fatal/desync/error logs, and a natural final outcome.

Each materially judged match or paired batch requires the template's isolated
Commenter workflow. Because this is not AI-policy work, use the factual narrative
to verify causal events and control differences but do not launch a Policy
Reviewer. A focused map can accelerate the collision; it may not replace the
ordinary-AI game, lifecycle test, or natural full match.

### Old-behavior control and required improvement

- Golden control: commit `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
  in an isolated worktree/build. Prefer a same-build map toggle only if it changes
  no actor/locomotor rules and cleanly selects separately installed base versus
  changed content; otherwise use the recorded commit.
- Match map checksum, seed, factions, bot types, teams, start slots, starting cash,
  tech/options, actor placements, order tick/destination, exit tick, and content
  except for the task diff. Record exact commands, commits, resolved-rule outputs,
  and artifact paths.
- On the literal forced lane, improvement must be decisive: base MCV fails to
  destroy/cross `wall` and `heavywall`; changed MCV destroys every Mammoth-class
  target, crosses, and deploys. Mammoth completes the same source-reference lane
  in both builds. Activation logs alone are not improvement.
- Existing behavior is a preservation metric: both builds must still crush crate
  and hostile infantry, reject allied targets, retain identical terrain travel
  timing within deterministic equality for identical obstacle-free routes, and
  deploy normally. STNK must fail the wall/heavywall negative lane in the changed
  build just as it does at base.
- This is a non-strategic flavor change, so normal-game win rate need not improve.
  Require no material degradation in deployment timing, MCV production/queue
  lifecycle, game outcome plausibility, or MAX cost. Investigate a loss, repeated
  parity on the forced new-capability lane, changed AI MCV order mix, or benchmark
  regression greater than 5% across matched/repeated samples; correct it or give
  a concrete task-specific noise explanation.

### Adversarial cases

After normal acceptance first passes and after the latest relevant fix, complete
at least three distinct clean full-engine scenarios with ordinary real AIs and
normal modules. For each run, record the failure hypothesis, perturbation, exact
failure signal, and player-visible pass evidence before launch.

1. **Mixed class matrix plus negative crushers.** Stress class completeness and
   spillover with hostile crate, mobile infantry, sandbag, concrete wall, a normal
   heavywall vehicle, and a heavywall light defense in constrained geometry. Run
   MCV and Mammoth reference lanes plus STNK and an ordinary non-heavy tank
   negative lane. Failure signal: any intended target survives the MCV/Mammoth,
   another unit gains heavywall parity, evasion/weapon fire gets miscredited, or
   the MCV fails to finish/deploy. Pass: only MCV and Mammoth visibly clear the
   complete matrix and reach their outcomes.
2. **Allied safety and order contention.** Add friendly and allied infantry,
   walls, vehicles, and defenses beside hostile crushables, with a valid detour,
   normal defenders, enemy pressure, and competing move/deploy ownership. Failure
   signal: any allied death from the MCV, a stuck/no-progress order loop, combat
   retargeting, or missed deployment. Pass: hostile targets in the deliberate
   route die, allied actors survive, the MCV detours or waits correctly, and the
   pre-existing lifecycle owner completes deployment.
3. **Terrain and transform lifecycle.** Vary Clear, Road, Rough, Bridge, Beach,
   and all three Tiberium terrain types; use both a newly produced MCV and a
   repacked Construction Yard, then deploy again. Compare obstacle-free travel
   ticks to base and place one hostile wall/heavywall only on the changed-capability
   segment. Failure signal: timing/pathability differs away from the intentional
   blocker, repack/deploy is rejected, or only pre-placed MCVs work. Pass: exact
   old terrain semantics, successful lifecycle transitions, and new crushing at
   the intended collision.
4. **Ordinary long-pressure match.** Use a normal connected map, scarce/contended
   cash, wall-enclosure AI, destroyed or missing initial Construction Yard, and
   enemy attacks so replacement/expansion logic acts. Failure signal: no MCV
   request because the wrong consumer/reservation owns the queue, repeated MCV
   combat movement, failure to deploy, enclosure regression, desync/fatal error,
   or material MAX slowdown. Pass: normal request-to-deployment recovery and
   natural match outcome with no new MCV AI policy.

Save/load is optional because crush capability is static rules data, not persisted
task state. If a save is used to accelerate lifecycle pressure, record commit,
config, seed, and tick and confirm the same outcome from a fresh match; a reload
never counts as sole acceptance or final regression.

### Final regression

From a fresh process and fresh map state after all fixes, rerun the literal forced
MCV lane with the strongest compatible stress: ordinary real AIs active, both
factions represented through the same normal `mcv` actor, mobile infantry,
crate/wall/heavywall targets, nearby allied actors requiring a safe detour, and
real enemy pressure. Require current artifacts proving the MCV—not weapon fire or
another actor—destroyed every hostile Mammoth-class target, crossed alive, and
deployed into `fact`; allied actors survived; Mammoth reference and STNK negative
controls remained correct; normal AI MCV lifecycle/order ownership was unchanged;
MAX advanced to the intended outcome without fatal/desync errors; and final
resolved rules/build checks are green. Rerun any materially affected ordinary
matched game and the natural-conclusion game after the last relevant fix.

## Implementation rules

Task-specific implementation/publication plan: preserve base resolved-rule and
control evidence; make the smallest actor-scoped CNC config change at the
locomotor/transform ownership boundary; verify no non-MCV spillover; run the
cycle-1 matched full-engine pair immediately; climb through lifecycle, safety,
terrain, contention, and natural-match tests; remove temporary diagnostics; write
the task report with exact artifacts/control metrics; publish one task PR to the
recorded base; and wait for all required checks. Product code, AI behavior, map-
local production fixes, and unrelated cleanup are forbidden.

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
| 1 | Dedicated `mcvheavywheeled` locomotor; MCV and FACT references updated | Actor scoping could spill into STNK/Mammoth, miss a class, alter terrain semantics, break transform/lifecycle, enable friendly crushing, or regress normal AI/performance. Progressed from literal changed/base lane through mixed corridor matrix, negative crushers, allied safety, ordinary production/repack, matched ordinary control, and natural endurance. | Base/changed resolved rules; `make test` before and after; 36 full-engine games including invalid harness diagnostics; final `make check`, `make check-scripts`, 438 unit tests. Seeds 43001–43005. | Fresh factual narratives under `analysis/worker-3-cnc-43/commenters/`; no policy review because this is literal config-only work. Final matched pair Commenter: no integrity blocker. | Changed MCV alone killed wall/heavywall targets and retained crate/infantry, terrain table, deployment, allied safety; base MCV did not. Mammoth reference and STNK/MTNK negatives matched. Normal VIKI produced/deployed a replacement MCV; Brutalis repacked/redeployed via McvManager. Natural match completed. | Product change accepted after one cycle. Ignored harness timing/path issues were corrected without another product cycle. Publish the minimal config diff and wait for required PR checks. |
| Review response (test-only) | No product/config change; added ignored `CNC-43 Review Long Pressure` matched evidence harness and summaries | CNC-43-EVIDENCE-1: a short lifecycle fixture might miss recovery failure under scarce cash, a busy vehicle queue, an enclosed base, and sustained ordinary enemy pressure; the dedicated locomotor might also cause MCV combat/crush ordering or a repeated MAX regression. | 10 additional full-engine games including invalid harness iterations; final matched changed/base seed 43043 on normal connected Empire Earth geometry, 5,000 cash, four ordinary VIKI/Brutalis/SkyNet bots, natural completion beyond tick 20,000. | Fresh no-history factual Commenter: `analysis/worker-3-cnc-43/commenters/review-long-pressure-v5/NARRATIVE.md`; no policy review because product/AI policy did not change. | Both sides lost the target Yard to Multi2 pressure with cash near zero and one live factory, then VIKI spent 4,000 through its only vehicle queue, produced an MCV normally, moved it under McvManager ownership with zero scripted/combat/crush orders, and deployed it alive while its 16-wall enclosure remained intact. Both ended naturally without fatal/desync. | Required evidence fix satisfied. No product defect was exposed, so the product diff and isolated cycle count remain unchanged. Winner and wall-clock differences reversed/varied across paired v4/v5 repetitions and are not attributed to the locomotor. Publish the test-only review response. |

## Handoff receipt

- Proposed status: `Complete - testing`.
- Final branch/head: `agent/round-20260806-cnc43-mcv-crush-flavor` / final receipt commit on PR #78 (exact SHA reported in the handoff response).
- PR and checks: #78, https://github.com/Realpra1/LibertyDawn/pull/78; mergeable `CLEAN`; GitHub reports no status checks on the branch and no required checks.
- Cycles used: 1/20 isolated config cycles.
- Acceptance evidence: Seed 43001 literal pair and seed 43003 final matched pair. Changed MCV killed crate/infantry/SBAG/BRIK/APC/GUN with MCV attribution and deployed; pinned base retained crate/infantry only and deployed.
- Adversarial evidence: Corridor class matrix plus Mammoth/STNK/MTNK controls and allied safety; seed 43004 ordinary production/repack lifecycle; seed 43005 natural Empire Earth endurance; seed 43043 matched long-pressure recovery with scarce cash, a contended vehicle queue, intact wall enclosure, enemy Yard destruction, normal MCV production/McvManager deployment, and natural completion.
- Old-behavior control and comparative result: Pinned `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`; final changed/base both tick 10,000 at 499.852/499.82 ticks/sec. Only changed MCV killed wall/heavywall; both Mammoths completed the full reference matrix.
- Match narratives and routine policy-review conclusions: Fresh Commenters verified each materially judged sequence. Final matched-pair and review-response narratives have no integrity blocker. Policy review intentionally skipped because AI policy is forbidden and no AI policy changed.
- Sol-xhigh policy escalation (unused, or test count/path/conclusion): unused; no policy problem.
- Final regression: `games/final-regression-v4b` changed pass plus `games/final-regression-base-v2` pinned-base expected-behavior pass, seed 43003.
- Error/warning and diagnostic-cleanup result: `make test`, `make check`, `make check-scripts`, and 438/438 Debug unit tests pass; builds report 0 warnings/errors. No product diagnostics; ignored Lua is bounded.
- Performance/determinism result: Final focused pair effectively identical (499.852 vs 499.82 ticks/sec). Earlier ordinary matched wall time +2.38%, below 5%; mean tick time slightly favored changed. In long-pressure v4/v5, outcome direction and runner throughput varied, while the two-run arithmetic mean of aggregate per-world-tick telemetry favored changed by about 5.3%; this is workload divergence, not a stable regression or benefit. Every final run completed naturally without fatal/desync.
- Deferred work: Optional generic headless winner/per-player telemetry; no CNC-43 product work deferred.
- Known failures/risks: No known task defect. The long-pressure logs identify McvManager ownership and zero scripted/combat/crush counters but do not expose every engine order packet; exact per-item queue blocking is also unknown. Ordinary AI outcomes and wall-clock measurements diverge between repetitions. Terrain equality uses exact resolved-rule comparison rather than per-terrain timed gameplay.
- Relevant artifact paths: `analysis/worker-3-cnc-43/{resolved,games,commenters,maps,manifests}` and this worker's `REPORT.md`.

## Integrated RC2 assignment

- Test the exact cumulative candidate fd15540ffc98c70f085688fe0b38a4a6341fc6ed
  (code candidate b456fd89fac88d71dfadd65c47cfb7b409d44122, draft PR #84)
  from repair branch agent/round-20260806-cnc43-rc2-repair.
- This is the combined release-testing phase. Preserve the original task contract
  and judge whether that task still works when all five reviewed changes coexist.
- Use full-engine headless MAX simulations from the first behavioral test. Make
  every test adversarial, use materially different scenarios, and compare against
  old/control behavior whenever that is informative. Isolate every support dir,
  log, replay, port, display, and map artifact.
- The shared game resource has a trial capacity of three. Use
  with_resource_slots.py with resource game, capacity 3, and the common round lock
  directory. Record elapsed time, peak RSS, completion reliability, and whether
  three-way concurrency should be retained or reduced to two.
- Run at most three integrated code-change cycles. A cycle boundary is a product
  code change; one cycle may include up to two games when needed. If relevant
  combined evidence passes, stop without inventing a change. If a failure requires
  repair, keep it strictly within this task, commit and push this repair branch,
  rerun affected evidence, and record the exact repair head for the integrator.
- The original task's balance authority is unchanged. Do not alter balance outside
  an exact authorization already present in this state, and never tune values to
  manufacture a better result.
- Continue using the existing Commenter and Policy Reviewer workflow for materially
  judged AI matches. Finish by updating this state/report with the exact candidate,
  scenarios, controls, artifacts, results, cycle count, resource measurements, and
  one of: combined pass/no repair, or reviewed repair head ready to merge.
