# Worker State: CNC-45

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `WORKER-1-CNC-45`
- Task: `CNC-45 — Economy troop production/use`
- Change category: `AI production, unit-role ownership, bounded tactical behavior, and an opt-in Mammoth attack-approach rule`
- Balance authority: `Game balance is frozen. AI-only composition, readiness, role-size, cooldown, target-selection, and attack-approach policy/configuration is authorized only for this task's Economy behavior. Do not change unit/weapon cost, HP, damage, armor, speed, range, prerequisites, locomotor crush classes, economy values, or player-facing balance.`
- Status: `Specified`
- Common base branch/SHA: `agent/cnc-20260807-bug-polish-02-release` / `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`
- Task branch: `agent/round-20260807-cnc45-economy-troops`
- Intended PR base: `agent/cnc-20260807-bug-polish-02-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `0`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/shared-locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260807-bug-polish-03/WORKER-1-CNC-45/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/AUTONOMOUS-CNC-LOGS/20260807-bug-polish-03/WORKER-1-CNC-45`
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

The Economy army currently has one general cohesive ground squad and specialist
field-defense/artillery reservations, but its ordinary production configuration
does not reliably express the intended roles. On the recorded base, Brutalis and
Iron Reaper do not enable weighted unit selection, so their large
`UnitsToBuild` numbers do not by themselves enforce the apparent proportions.
The general ground squad can also use its whole slow formation against valuable
remote economy targets. Mammoths attack from the longest target-valid weapon
range, allowing their missiles to engage while the cannons remain idle.

The predicted result is a mature, ready Economy force whose committed frontline
vehicle value is primarily Mammoths, supported by Rifle Infantry and the existing
bounded Rocket Launcher artillery squad, while a small Medium Tank group owns
mobile harassment. A tiny, local subset of Mammoths may opportunistically crush
dangerous infantry and immediately return. For an ordinary supported attack, a
Mammoth closes to the shortest maximum range among currently usable weapons, so
both cannon and missiles contribute against ground targets. These policies must
yield to survival, economy continuity, anti-air, field defense, artillery support,
reservations, retreat, and the current squad objective.

## Authoritative behavior

- Scope the new strategic identity to Brutalis and to Iron Reaper only while its
  Economy specialization is active. Other AI personalities, easier difficulties,
  and Iron Reaper's non-Economy specializations retain their current behavior.
- Once Economy III is available and a cheap readiness check says the economy and
  force can sustain the role, Mammoths are the single largest type and the
  majority of the unreserved, committed direct-fire frontline vehicle value.
  This is a fuzzy readiness-driven priority, not an unconditional production
  quota. Necessary harvesters/technology, anti-air, Rifle Infantry screen,
  artillery operation, and urgent local defense take precedence.
- Preserve a live mixed frontline. Rifle Infantry must join and participate as a
  screen; the existing Economy artillery owner must retain its bounded Rocket
  Launcher battery and modest escorts; Mobile SAM and other counter-production
  remain available when required. Mammoth-led must never mean Mammoth-only.
- Create at most one independently owned Medium Tank harassment mission at a
  time. It may use a small configurable group capped at four eligible Medium
  Tanks, must leave a credible mobile response for threatened harvesters and
  artillery, and must select a reachable exposed harvester, economy target, or
  weak flank. It suspends replenishment or recalls promptly when readiness fails,
  the target becomes bad, the Economy specialization ends, or a higher-priority
  reservation/order takes ownership. Mammoths are never harassment members.
- Continue to use the established reservation interfaces. The new owner may claim
  only currently eligible Medium Tanks and must release them on completion,
  invalidation, timeout, no progress, tech/specialization loss, owner change,
  save/load recovery failure, or urgent readiness loss. Released units must
  promptly become useful to their normal owner instead of remaining idle.
- Mammoth crushing is an occasional tactical deviation by no more than two
  unreserved, healthy-enough Mammoths already close to the current formation
  route/objective. Require a visible hostile infantry actor that is actually
  crushable by that Mammoth, shares a reachable domain, has a cheap bounded route,
  and does not require a meaningful formation detour or threat-exposure increase.
  Prefer exposed anti-tank infantry obstructing or immediately threatening the
  formation. Reject allied/neutral, invalid, uncrushable, distant, blocked, dense
  volatile-infantry, dangerous-choke, or lure candidates.
- A crush deviation has an explicit finite leash, timeout, no-progress rule, and
  group cooldown. Cancel on target invalidation/destruction, reservation or owner
  change, squad retreat/new urgent order, threat escalation, blocked movement, or
  leash breach. After success or cancellation the Mammoths immediately return to
  normal squad ownership/order. A plain AttackMove without an observable valid
  crush attempt and result is not evidence. Walls are not an initial target.
- Only opted-in Mammoth attacks change their approach behavior. Against a target
  for which cannon and missiles are both enabled, unpaused, and target-valid, use
  the shortest of their effective maximum ranges, including live range modifiers,
  rather than a hard-coded cell distance. If only one weapon is usable, use that
  weapon's own range; an air-only target therefore remains a missile-range attack.
  If no weapon is valid, preserve normal rejection. Retreat, invalidation,
  unreachable movement, squad-objective cancellation, and higher-priority orders
  override closing. Do not turn this into unconditional pursuit.
- Save/load and replay must preserve or safely reconstruct production/readiness,
  harassment ownership, crush cooldown/mission state, and return-to-owner state
  without duplicate reservations/orders or desynchronization.
- Keep all evaluation deterministic and bounded: stable ActorID ordering where a
  tie needs resolution, capped candidate sets, configured scan/order intervals,
  domain/threat/readiness rejection before path queries, no per-Mammoth full-map
  searches, and no uncontrolled retries or allocations.

## Forbidden behavior and failure signals

- Any game-balance edit, including changing Mammoth/Medium Tank/weapon stats,
  range, cost, prerequisites, build timing, actor locomotors, or crush classes.
- Any change to the CNC-43 MCV baseline. The MCV must continue using its distinct
  heavy-wheeled crush capability exactly as it does at the common base; it must
  not receive this task's Mammoth orders or targeting policy.
- Mammoths assigned to harassment, the whole frontline chasing remote harvesters,
  pure or effectively pure Mammoth production, loss of the Rifle Infantry screen,
  a missing/stolen artillery battery, or starvation of harvesters, Mobile SAMs,
  field defense, transport, repair, or counter-production.
- Starting or maintaining a Medium Tank raid while harvesters/artillery face
  credible immediate pressure, when minimum screen/support is absent, or when
  another owner has reserved the actors. More than one raid, more than four raid
  tanks, leaked reservations, stale mission state, repeated order fighting, or
  released tanks remaining idle are failures.
- Whole-army crushing; more than two temporary crushers; attempts on allied,
  neutral, non-infantry, uncrushable, invalid, unreachable, distant, or dangerous
  bait; wall crushing; long detours; persistent separation; repeated no-progress
  orders; or failure to return after the attempt.
- A global change to `AttackFollow`/attack-range semantics, a fixed `5c` Mammoth
  stop distance, applying the rule to unrelated actors, closing on a target that
  only the long-ranged weapon can hit, ignoring effective range modifiers, or
  pursuit that overrides retreat/objective cancellation.
- Treating an activation log, selected target, issued order, weapon shot, or
  attempted crush as success without final observable damage/kill, useful role
  outcome, release/return, and matched control comparison.
- An avoidable crash, assertion, warning/error flood, save incompatibility,
  replay divergence/desync, nondeterministic result from unordered collections,
  or repeated matched MAX throughput regression. A repeatable median slowdown
  greater than 5% in the five-AI stress comparison, or a new dominant scan/path/
  allocation hotspot even below that threshold, requires diagnosis and repair.

## Relevant current implementation and control behavior

All facts in this section refer to common base
`468ee64f5a0f9a9e19e260e5c5943e6e878f4705`.

- `mods/cnc/rules/ai.yaml` enables the established
  `EconomyFieldDefenseBotModule` and `EconomyArtilleryBotModule`. Field defense
  reserves `mtnk`, `e1`, and `msam` around committed fields. Artillery reserves
  Rocket Launchers plus bounded `msam`, `mtnk`, and `e1` escorts and makes explicit
  production requests. These are incumbent owners, not code to replace.
- Brutalis and Iron Reaper each have one cohesive general ground squad. Strategic
  scoring values harvesters highly enough that an entire slow formation can be
  pulled toward an economy target. Squad assignment already filters unit and
  transport reservations and persists its state.
- Their UnitBuilder entries list high apparent `e1`/`mlrs`/`htnk` counts and low
  `mtnk` counts, but weighted selection is not enabled. With the very high idle
  unit ceiling, those values do not reliably implement the intended proportional
  force. Existing weighted/adaptive selection includes broader learning,
  affordability, economy gates, and queue behavior; enabling it indiscriminately
  would change more than this Economy role.
- `EconomyArtilleryBotModule` claims all eligible Rocket Launchers and bounded
  value-proportional escorts, checks other reservations, queues its first Mobile
  SAM, persists target/timer/actor state, and releases on prerequisite loss.
  `EconomyFieldDefenseBotModule` similarly uses reservations and resource-safe
  routing. A new Medium Tank owner must coexist with both.
- `htnk` uses `AttackTurreted`/`AttackFollow`. Its dual cannon has a base range of
  `5c`; Mammoth missiles have `7c` and different target validity. The follow
  attack's target approach currently uses the longest valid maximum range, while
  individual armaments fire only when they can. This permits a ground Mammoth to
  stop where missiles fire but cannons do not. Generic frontal attack code has
  related shortest-range logic, but changing all follow attacks is out of scope.
- Mammoths use the existing `heavytracked` locomotor and can crush infantry.
  CNC-43 separately gave MCVs the `mcvheavywheeled` baseline capability. Preserve
  both locomotor definitions and actor assignments.
- Existing unit reservation competitors include Economy field defense/artillery,
  transport, protection/general squads, covert and stealth harassment, crate
  collection, early rush, and red-Tiberium-bomb logic. Auto-target, repair/
  resupply, production request queues, branch switching, and counter-production
  can also invalidate a proposed role.
- Relevant focused-test patterns exist in `OpenRA.Test/OpenRA.Mods.Common/` for
  adaptive weighting, strategic ground scoring, Economy artillery policy,
  harassment policy, stealth-tank policy, and movement cooldowns. They are useful
  supplementary checks, not substitutes for full-engine ordinary-AI games.

## Likely wrong approaches and challenges

- Merely editing `UnitsToBuild` values while their selection mode ignores relative
  weights. Conversely, globally enabling adaptive/weighted production or enabling
  it for all Iron Reaper phases would let unrelated learning and branch behavior
  erase the authored Economy role.
- Defining Mammoth dominance as a fixed quota or every spare credit. This can
  crowd out harvesters, anti-air, screens, artillery operation, field defense,
  counters, or recovery after losses. Use an explicit cheap readiness budget and
  calibrate fuzzy thresholds with matched games.
- Reusing Covert/Stealth harassment wholesale. Their unit purpose, target risks,
  cloak/mobility assumptions, ownership, and state transitions differ. Reuse
  small proven policy helpers or interfaces only when their contract truly fits.
- Folding all new behavior into the already large `SquadManagerBotModule` or
  duplicating reservation/save logic. Prefer focused policy collaborators and a
  single clear owner for each temporary role.
- Letting the harassment group steal Medium Tanks from the established field or
  artillery owners, or reserving all Medium Tanks so those modules can never act.
  Candidate filtering and release ordering must make contention auditable.
- Scanning every unit/target every tick or running pathfinding for a huge group
  before cheap domain, locality, readiness, threat, and cap checks. Disconnected
  large formations are a known worst case; do not solve a global route optimizer.
- Copying the stealth-tank `Move`-to-infantry pattern and assuming it proves safe
  Mammoth crushing. Mammoth cohesion, threat exposure, crushability, objective
  ownership, cooldown, and return all need explicit evidence.
- Changing weapon range, `RangeMargin`, or global follow-attack behavior; hard-
  coding `5c`; or ignoring air-only/paused/disabled/modified weapon cases. Derive
  the opted-in approach from currently target-valid effective weapon ranges.
- Testing only pure policy methods, passive bots, scripted units, logs, or a
  cheese map. Constructed full-engine fixtures are useful only with ordinary AIs
  and all relevant normal modules, followed by matched natural-game evidence.
- Improving headline wins through a balance edit, favorable map/seed, faster tech,
  opponent production failure, or ordinary targeting luck. Matched controls and
  task-specific outcomes must isolate this policy.

## Competing systems and ownership

- UnitBuilder owns production queues, cash/affordability decisions, prerequisite
  gates, explicit production requests, MCV/economy priority, and any adaptive or
  target-share selection. New composition policy must remain subordinate to
  economy continuity and existing explicit requests.
- Economy artillery is the sole owner of its Rocket Launchers and selected
  escorts. Economy field defense owns committed field defenders. Transport,
  protection/general squads, repair/resupply, crate collection, special squads,
  and other `IBotUnitReservations` implementations may own or invalidate actors.
- The Medium Tank harassment role needs one explicit reservation owner, clear
  priority relative to incumbent owners, bounded claims, and deterministic release.
  It must not double-order a reserved actor and must expose the rejecting owner in
  bounded diagnostics when contention determines behavior.
- General/protection squads remain the normal owner of frontline Mammoths and
  Rifle Infantry. Crush is only a temporary mission attached to the current
  objective, never a second permanent army owner. Normal attack, retreat,
  repair/resupply, transport, death/disposal, and urgent squad orders can cancel it.
- The opted-in Mammoth approach calculation belongs at the attack mechanism's
  policy boundary or a focused helper, with the default longest-range behavior
  intact for every actor not explicitly configured. YAML owns the HTNK opt-in;
  code owns target-valid effective-range invariants.
- Iron Reaper branch transitions can remove Economy authority mid-production or
  mid-mission. Save/load may restore actors after ownership changed. Both are
  explicit release/reconciliation paths, not exceptional cases.

## Cross-worker dependencies

- No prerequisite task or active CNC-45 PR was named in the assignment packet.
- Preserve the completed CNC-43 MCV crush baseline already present in the common
  base (commit ancestry includes `4f36851179`). This is a behavior-scoped
  dependency: do not alter the MCV locomotor, actor assignment, or order policy.
- The existing CNC-36 Economy artillery cluster is an incumbent role on the base;
  reuse its reservation contract and prove it remains active rather than
  absorbing or replacing it.
- CNC-40, CNC-41, CNC-42, and CNC-44 are explicitly outside this task. Do not
  incorporate or repair those prior-round changes.
- Likely shared integration hotspots are `mods/cnc/rules/ai.yaml`, vehicle actor
  rules, UnitBuilder/squad code, and follow-attack code. If the coordinator later
  names a concurrent PR touching one, inspect that PR's commits, rebase/sequence
  as directed, preserve both behavioral contracts, and report the overlap. Until
  then, make no assumptions from another worker's spec or branch.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `/root/github/LibertyDawn/AUTONOMOUS-CNC-LOGS/20260807-bug-polish-03/WORKER-1-CNC-45/spec-policy-review/inputs/NARRATIVE.md`
- Sol-high policy review: `/root/github/LibertyDawn/AUTONOMOUS-CNC-LOGS/20260807-bug-polish-03/WORKER-1-CNC-45/spec-policy-review/POLICY-REVIEW.md`
- Verdict and confidence: `mostly sensible; medium confidence`
- Recommendations adopted as testable hypotheses: `Use a fuzzy readiness budget before Mammoth-dominant production or Medium Tank raiding; define dominance by committed frontline vehicle value rather than all production; retain a local mobile-defense reserve; use at most a tiny threat-aware Mammoth crush subset with volatile-cluster/choke/leash/no-progress rejection; require the shortest-target-valid-range approach to preserve retreat and objective cancellation; compare useful cannon uptime/objective time, Medium Tank economy damage per loss, force survival, support continuity, ownership recovery, and MAX cost against the exact old control.`
- Recommendations rejected or deferred, with reason: `No strategic recommendation was rejected. Wall crushing is explicitly deferred because it expands the first acceptance target without evidence. Exact composition/readiness percentages, route distances, and threat thresholds are not declared proven at spec time; calibrate the smallest fuzzy values through matched full-engine games. Broader strategic target-selection and pathfinding redesign is out of scope.`
- Persistent scratchpad update: `The reviewer preserved the scratchpad unchanged because this was proposed-policy review without match evidence; the validated reviewer file was atomically promoted under the one-slot lock and matches the canonical 271-byte scratchpad.`

## Acceptance and tests

### Literal black-box acceptance

In full-engine games with ordinary AI modules enabled, prove all of the following:

1. For both Brutalis and an Economy-phase Iron Reaper, create a mature ready-state
   observation window after Economy III with enough cash/time for at least twelve
   post-tech ground-combat completions or an equivalently documented established
   force. While no urgent readiness guard is active, Mammoths are the single
   largest direct-fire frontline vehicle type and at least half of unreserved
   committed frontline direct-fire vehicle value across multiple decision samples.
   Rifle Infantry visibly participates with that formation; the established
   Rocket Launcher battery remains owned, active, and supported; required Mobile
   SAM/economy/counter production is not starved. Under a deliberately forced
   readiness failure, the AI relaxes the Mammoth/raid priority and preserves the
   missing critical function.
2. A small independently owned Medium Tank group selects and reaches a deliberately
   exposed, reachable harvester/economy/weak-flank target, produces observable
   useful damage or a kill, and then releases/returns or retargets within its
   bounded mission rules. No Mammoth is ever recorded or observed as a harassment
   member. The old control must not show the same intended split merely by chance.
3. With one reachable hostile crushable infantry threat near the Mammoth formation
   route and misleading allied/neutral/uncrushable/dangerous candidates present,
   no more than two eligible Mammoths deviate, at least one valid target is visibly
   crushed by movement/contact rather than merely shot, and every survivor returns
   to the current normal squad objective before another group cooldown permits a
   new attempt. Invalidating/blocking the target must cancel cleanly.
4. Against a stationary ground target valid for both Mammoth weapons, the Mammoth
   closes to the dynamically derived shortest effective maximum range and both
   cannon and missile produce useful fire/damage. Against an air-only target it
   remains at the missile-valid range; with a live range modifier it uses the
   modified effective range; with one weapon paused/disabled/invalid it uses only
   the remaining usable weapon. Unrelated follow-attack actors behave like control.
5. Tech/specialization loss, target death, reservation change, actor death, retreat,
   and save/load during each temporary role leave no double ownership, stale
   target, repeated order fight, stranded actor, crash, replay divergence, or
   desync. The ordinary connected case still functions after each recovery.

### Focused checks and instrumentation

- Before or alongside cycle 1's game, add focused tests for the smallest pure
  policy boundaries: readiness/role allocation and incumbent reservations;
  production selection proving configured Mammoth priority actually affects the
  active selection path; raid group cap/recall/release; crush hostility,
  crushability, domain, volatile-cluster, leash, cooldown, no-progress and
  invalidation decisions; and shortest target-valid effective range for two, one,
  paused/disabled, modified, air-only, and no usable weapons.
- Extend existing adaptive-weighting, Economy-artillery, harassment, stealth-policy,
  movement-cooldown, or attack tests only where their existing contract fits.
  Prefer a focused new collaborator test over expanding a large unrelated fixture.
- Run the affected NUnit project/tests plus `make test`/YAML validation, `make
  check`, and supported CNC compile/package gates proportionate to touched code.
  Shared engine compilation is allowed; do not build, test, package, or modify
  Red Alert, Dune 2000, or Tiberian Sun content.
- Add bounded, task-owned diagnostics sufficient to correlate: branch/prerequisite
  and readiness decision; production candidates/selection/request/queue/spend;
  actor assignment and reservation owner/rejection; raid/crush target and state
  transition; reject/cancel reason; configured scan/cooldown/leash; movement/order;
  enabled target-valid weapon set and derived approach range; final hits, damage,
  kills, release, and return. Include ActorIDs and ticks where useful. Do not log
  per tick; remove temporary/noisy probes before publication.
- For every game record map/content checksum, commit/toggle, seed, factions, bot
  types/difficulties, starts, options, initial actors/resources/tech, duration,
  game ticks, headless-MAX activation markers, final outcome, and artifact paths.
  Record production completions/spend, sampled composition and owner/reservation,
  army/economy value, losses, idle queues/units, raid exchange/economy damage,
  crush attempts/results/detours/damage taken, weapon shots/useful damage,
  objective time, real/game-time ratio, actor counts, path calls, allocations, and
  profiler hotspots as available.

### Ordinary and differential games

- The first behavioral evidence after cycle 1's product change is a matched pair:
  changed behavior versus a feature-disabled same-build control when possible,
  otherwise an isolated worktree at exact base
  `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`. Use the same CNC map artifact/hash,
  seed, factions, starts, bots, difficulty, options, resources, tech, and initial
  state. Run ordinary Brutalis with all normal production, squad, artillery,
  field-defense, transport, protection, repair, and targeting modules enabled.
- A focused custom full-engine map may pre-place/pre-tech enough production,
  Mammoths, Rifle Infantry, Rocket Launchers, Medium Tanks, an exposed harvester,
  a nearby crushable threat, and a stationary ground target so the four requested
  effects happen quickly. Both sides must still use ordinary game AIs and normal
  modules. Passive/custom bots or isolated manager simulations do not count.
- After the forced causal case, run repeated matched connected-map games from a
  fresh start for both Brutalis and Economy-phase Iron Reaper against representative
  Economy, Covert, and Recon pressure. Vary map/seed/starts only between matched
  pairs. At least one final fresh natural match must run headless MAX to a natural
  conclusion and materially exercise production/role behavior.
- Run an ordinary connected map and Archipelago/blocked topology. The connected
  case must produce useful raid and return behavior. The blocked case must show
  cheap rejection of disconnected targets without disabling the connected case.
- Every materially judged game or paired batch gets a fresh factual Commenter and,
  because this is AI policy, a fresh Terra-medium Policy Reviewer using only the
  staged authorized artifacts/context and the one-slot scratchpad workflow defined
  below. Review recommendations are hypotheses, not substitutes for evidence.

### Old-behavior control and required improvement

- Golden control is exact common base
  `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`, or a verified same-build feature-
  disabled mode with unchanged hashes other than the toggle. Never use a different
  personality as the old-behavior control. Record exact control/changed manifests.
- In exercised ready-state scenarios, changed behavior must show a stable mixed
  Mammoth-led frontline, zero Mammoth harassment membership, a useful bounded
  Medium Tank raid, retained artillery/screen/anti-air/economy continuity, more
  useful Mammoth cannon participation, and faster completion or materially more
  useful damage against ordinary ground objectives than control.
- Judge improvement with army/economic value and survival, objective time, useful
  damage/kills, raid economy damage and exchange value, Mammoth remote-travel time,
  artillery/harvester losses, production/queue starvation, idle units, recovery
  latency, and match outcome. A higher Mammoth count, a trigger log, one crush, or
  extra cannon shots alone is not material improvement.
- Extra incoming damage from closing and Medium Tanks unavailable for local defense
  are real costs. Repeated loss, parity, marginal gain, slower objectives, material
  force/economy regression, or a win explained by unrelated opponent/tech luck is
  a failed policy until diagnosed. Preserve an accepted exposure tradeoff only
  when repeated objective value clearly offsets it and the task report states it.
- In a matched stress batch with at least five active ordinary AIs, compare median
  real/game-time ratio, actor counts, path/search counts, allocations, and profile
  hotspots. Target at least 300 mobile actors per AI where the harness/map can
  sustain it and record the achieved count. A repeatable greater-than-5% median
  throughput regression or a new dominant hotspot is not acceptable without a
  scoped fix and rerun.

### Adversarial cases

After the latest behavior-affecting fix, obtain at least three distinct clean
full-engine ordinary-AI adversarial scenarios, each with an explicit hypothesis,
perturbation, failure signal, and final observable pass evidence. Cover all of
these risks, combining compatible cases when the event is forced and auditable:

1. **Ownership/readiness/transition contention.** Force field defense, artillery,
   transport/protection/repair, and the new raid to want the same small Medium
   Tank pool; attack a harvester or artillery position; destroy the raid target;
   then remove Economy authority or switch Iron Reaper specialization. Fail on a
   stolen/double-ordered actor, missing critical defense/support, stale reservation,
   duplicate replenishment, or idle released unit. Pass only when every incumbent
   acts, readiness recalls/suspends the raid, and actors return usefully.
2. **Disconnected large-group routing and MAX cost.** On Archipelago or an
   equivalent blocked topology, advertise a high-value unreachable economy target
   while at least 150 formation members receive hostile objectives and the game
   has high actor counts. Fail on an expensive initial path burst, full-group
   per-unit search, retry storm, stalled ordinary objective, or throughput hotspot.
   Pass on cheap domain/locality rejection and successful behavior toward a later
   connected target. Include the five-AI, preferably 300-mobile-actors-per-AI,
   Normal/Fastest or headless-MAX performance comparison here if practical.
3. **Crush bait and invalidation.** Present an exposed hostile Rocket Soldier on
   the formation route together with allied/neutral/uncrushable infantry, a dense
   volatile cluster, a target beyond a danger/choke leash, and a target that moves,
   dies, or becomes blocked mid-attempt. Fail on false eligibility, more than two
   crushers, shooting mistaken for crushing, repeated no-progress, dangerous
   pursuit, cohesion loss, cooldown violation, or failure to return. Pass only on
   one observable valid crush plus clean rejection/cancellation and reassignment.
4. **Weapon/target/exposure matrix.** Exercise a supported stationary ground
   target valid for both weapons, an air-only target, a paused/disabled or
   target-invalid weapon, a live range modifier, a fast retreating lure, and a
   fortified bait involving Turrets/Obelisk/anti-tank infantry or a choke. Fail on
   hard-coded/global range behavior, idle valid cannon, pursuit beyond the squad
   objective, disproportionate losses, or failure to disengage. Pass on derived
   target-valid range, useful dual-weapon damage in the safe case, and priority of
   retreat/objective/survival in the counterexamples.
5. **Persistence/recovery.** Save/load mid-raid, mid-crush, during cooldown, and
   across target destruction or tech/specialization loss. Also inspect a replay
   from a fresh start. Fail on duplicate ownership/orders, resurrected targets,
   reset cooldown abuse, stranded units, divergent decisions, fatal error, or
   desync. A reload may accelerate this case but cannot be sole final proof.
6. **Mixed-force counterpressure.** Against Covert and Recon pressure, remove or
   threaten screens, Mobile SAM, harvesters, and artillery in turn. Fail if a
   rigid Mammoth quota or raid continues while a critical readiness component is
   missing. Pass when support/counter production and local defense preempt the new
   roles, after which ready-state behavior resumes without permanent starvation.

Any relevant fix resets the three-clean-scenarios count. A near-identical happy-
path rerun is not a distinct adversarial scenario.

### Final regression

- Rerun the literal four-effect acceptance from a fresh game with every normal
  competing module enabled, adding the strongest compatible contention/topology/
  threat stress that does not invalidate the requested behavior. Require final
  observable role results, recovery, and no warning/fatal/desync; do not pass on
  activation logs.
- Rerun the materially affected focused tests, CNC YAML/static/check/build gates,
  the strongest matched old-control pair, and the five-AI MAX performance case.
  Inspect current artifacts rather than relying on an older passing save/replay.
- Run at least one fresh real full match at headless MAX to a natural conclusion,
  with ordinary AIs and relevant modules, and confirm the replay/no-desync result.
  Record final outcome plus composition, support continuity, raid/crush/weapon
  evidence if exercised, objective/survival metrics, real/game ratio, actor counts,
  and hotspots. Save/load evidence is supplementary and must be confirmed fresh.

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
For example, pre-place enough Economy actors and targets to force the production,
harassment, crush, and multi-weapon decisions. Use the setup for direct causal
proof, then seek natural-match evidence when the event is reasonably reachable.
If natural occurrence depends on unfinished prerequisite behavior, record that
dependency and required future revalidation instead of wasting cycles waiting for
an event the current build seldom creates or treating its absence as failure.

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
that the harness and simplest behavior work. As soon as it passes, change at least
one meaningful dimension—timing, map geometry, resources, missing/destroyed assets,
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
`/root/github/LibertyDawn/AUTONOMOUS-CNC-LOGS/20260807-bug-polish-03/WORKER-1-CNC-45/cycle-review-05/CYCLE-REVIEW.md`.

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
  --lock-dir /root/github/LibertyDawn/.worktrees/coordinated-cnc/shared-locks --resource game --capacity 2 --slots 1 -- COMMAND...
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
