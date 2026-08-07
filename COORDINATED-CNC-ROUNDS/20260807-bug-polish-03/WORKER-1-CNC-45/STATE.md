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
- Status: `First iteration - testing`
- Common base branch/SHA: `agent/cnc-20260807-bug-polish-02-release` / `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`
- Task branch: `agent/round-20260807-cnc45-economy-troop-use`
- Intended PR base: `agent/cnc-20260807-bug-polish-02-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `20`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/shared-locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260807-bug-polish-03/WORKER-1-CNC-45/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/AUTONOMOUS-CNC-LOGS/20260807-bug-polish-03/WORKER-1-CNC-45`
- Persistent policy scratchpad: `/root/github/LibertyDawn/.agents/references/LIBERTY-DAWN-POLICY-SCRATCHPAD.md` (3,000
  characters maximum; one cross-round serialized writer)
- Policy scratchpad lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/shared-locks`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `31` (cycle-3 is the first valid bounded run; cycle-4 reached its tick-8,000 bound with exit 0 but the harness labeled it failed because required substrings present in debug.log were not found by its pattern routing; cycle-5 runs 6 and 7 are clean bounded constructed games but are not an old-behavior comparison; cycle-6 run 8 is invalid because the harness used the unrelated root checkout after the explicit worktree launcher was omitted, while corrected run 9 is valid; cycle-7 runs 10 and 11 are clean bounded diagnostic/contact attempts that identify the mission-overlap race but do not prove a crush; cycle-8 runs 12 and 13 prove the two-Mammoth cap and return but fail contact; cycle-9 runs 14 and 15 prove ranged-fire preemption but not a crush; cycle-10 runs 16 and 17 classify external kill and safe cancellation; cycle-11 runs 18 and 19 bound selection and exact aborts; cycle-12 run 20 exercises the custom order without contact; cycle-13 run 21 identifies the occupied-cell prefilter rejection; cycle-14 runs 22 and 23 are clean fixture races; cycle-15 run 24 rejects an off-route target and run 25 is the first assignment-bound contact kill with prompt return; cycle-16 runs 26 and 27 reveal same-map/seed trajectory variance and run 27 proves one resolver per Mammoth plus safe objective-change return; cycle-17 run 28 preserves same-objective cell motion but loses its selected infantry to ordinary fire before order resolution, while run 29 cleanly rejects an on-route candidate outside the locality bound; cycle-19 run 30 is a clean final-source 60,000-tick endurance run with replay/benchmarks and no fatal/desync/Lua error, but readiness never opens and the safety-bound exit is not natural-conclusion proof; cycle-20 run 31 is a clean 3,000-tick reviewer-response fixture in which exactly two Mammoths start, one contact-kills the selected infantry, and both return immediately, but no competing-order trace or control proves the exclusion causally; prior three attempts remain invalid as recorded below)
- Terra cycle code reviews: `cycle-05 advisory adopted; initial observation could complete after entry cash lapsed; corrected in cycle 6. cycle-10 advisory adopted; target cap is applied after unbounded infantry/reachability scans; deterministic cheap shortlist fixed in cycle 11. cycle-15 advisory adopted; stationary-target crush orders refresh over a still-active activity and cause observable order fighting; preserve the active same-target activity in cycle 16. cycle-20 advisory accepted but cannot be repaired without cycle 21: the Medium Tank raid cap is applied after reachability/attack checks and full sorting; hand off First iteration - testing.`
- Sol-xhigh policy escalation: `unused (requires at least 10 game tests; one maximum)`
- PR: `https://github.com/Realpra1/LibertyDawn/pull/94` (draft; base `agent/cnc-20260807-bug-polish-02-release`)

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
| 1 | Uncommitted initial Economy production/readiness, Medium Tank raid, Mammoth crush, and opted-in approach implementation | Hypothesis: Economy III may not activate the mixed force safely or may regress progress versus exact base. Perturbation: matched seed 45001, Empire Earth4, Brutalis GDI versus SkyNet Nod, $20,000, all ordinary modules, headless MAX. | Changed and exact-base control both launched. Changed advanced to tick 19,506 then stopped advancing; control reached natural game over at tick 23,960. | No Commenter/policy review: batch invalid and changed summary absent. | No crash/desync marker. Control harness failed only because natural conclusion preceded the configured minimum tick. Changed produced no task-owned diagnostics and no final outcome. Audit found `ApproachRangePolicy` attached to APC instead of HTNK, so literal effect 4 was not implemented. | Correct the actor opt-in, tighten task diagnostics and focused boundaries, then rerun a valid ordinary-AI changed/control pair with natural-game-over accepted. |
| 2 | Correct HTNK-only attack opt-in; require an unreserved Rifle Infantry screen; add bounded task-owned production, raid, crush, and effective-range diagnostics; remove hot-path range-selection LINQ. | Hypothesis: the cumulative implementation may fail compile/YAML validation or the HTNK opt-in may leak; serial ordinary-AI perturbation separates prior paired contention from a product stall. Failure signals: compile/YAML error, non-HTNK opt-in, fatal/desync/progress stall, no Economy III transitions, or missing mixed-force continuity. | First compile caught and corrected an `in Target` diagnostic capture. Focused NUnit then passed 9/9. Serial seed 45001 Empire Earth4 Brutalis-vs-SkyNet advanced through tick 55,000 without fatal/desync, then was intentionally interrupted because the behavior under test remained closed. | Commenter: `cycle-02-commentary/NARRATIVE.md`, no reliable winner/comparison, changed world did not stall, target breadth lacks damage/lifecycle proof. Policy: `cycle-02-policy-review/POLICY-REVIEW.md`, insufficient evidence/high confidence; require valid matched natural pair and unit outcomes. Reviewer scratchpad output was missing, so canonical scratchpad was retained unchanged. | 697 readiness logs, zero raid logs, zero crush logs. The simultaneous support/screen/no-threat/cash>=2500 entry state never occurred. 195 HTNK-only approach logs showed two-weapon ground range selecting 5120 over 7168, live-modified 6400 over 8960, and missile-only air range; no damage/kill result yet. The run exceeded the old control's observed natural tick but was externally stopped and is invalid outcome evidence. | Replace the brittle snapshot-cash gate with explicit entry/maintenance hysteresis that still yields to critical threats/support loss; reduce repetitive readiness logging; run a bounded full-engine scenario that can actually open readiness, then obtain a valid same-content toggle/control strategy before outcome claims. |
| 3 | Add readiness cash hysteresis (1000 entry/0 maintenance) while preserving immediate support/threat failure; throttle production diagnostics to state changes or a 750-tick heartbeat. | Hypothesis: hysteresis may still never activate, or may flap/continue through critical pressure/support loss. Perturbation: fresh seed 45003 connected ordinary Brutalis-vs-SkyNet to tick 30,000. | Focused NUnit passed 10/10. Full engine passed its configured bound at tick 30,000 in 224.709s (133.497 ticks/s), all required markers, benchmark/replay present, no fatal/desync. | Commenter: `cycle-03-commentary/NARRATIVE.md`, valid bounded run but no winner/control/lifecycle evidence; ready signals were transient. Policy: `cycle-03-policy-review/POLICY-REVIEW.md`, insufficient evidence/medium confidence; require a continuous stable observation gate. Proposed field-extension fallback rejected as out of CNC-45 scope. Reviewer scratchpad output again missing; canonical retained. | Ready opened at tick 24,826 then correctly dropped when unreserved screen fell below four; it opened again at 28,726 and issued Mammoth priority, then critical threat preempted it. Support reached 13 harvesters/12 Rifle screens/7 Rocket Launchers/15 Mobile SAMs. Zero raid/crush opportunities occurred. Mean tick sample 4.048ms; mean actor-time sample 0.930ms. | Require continuous maintenance-ready observation after an entry-budget sample before enabling production/raid/crush/approach. Keep approach baseline while unready. Then construct ordinary-AI role opportunities rather than waiting for rare natural contention. |
| 4 | Add saved 300-tick readiness observation state after entry cash; reset on maintenance/threat failure; gate HTNK approach policy on stable readiness. | Hypothesis: the observation window may activate early, never complete with a mature mixed force, or survive invalidation. Perturbation: focused state tests plus a constructed Empire Earth4 ordinary Brutalis-vs-SkyNet game with preplaced mixed force, exposed harvester, and nearby hostile Rocket Soldier. Failure: activation before 300 ticks, no later activation, oversized roles, stale role, fatal/desync. | Focused NUnit passed 11/11; only the pre-existing unrelated AircraftHusk CA1825 warning. Game seed 45004 reached tick 8,000 in 32.035s with exit 0, replay/benchmarks, no fatal/desync. Harness status failed only because two required substrings present in debug.log were not matched by its pattern source. | Commenter: `cycle-04-commentary/NARRATIVE.md`, exact gate timing and capped release but no outcome/control. Policy: `cycle-04-policy-review/POLICY-REVIEW.md`, insufficient evidence/medium; instrument eligibility/assignment/damage/return and run matched control. Extension/Minigunner observations explicitly retained as out of scope. Reviewer scratchpad output missing; canonical retained. | Brutalis remained screen-recovery through tick 5,251, began observing at 5,851, and first reached mixed-target-met exactly 300 ticks later at 6,151. Four Medium Tanks 532-535 raided harvester 541 at 7,251 and released at 7,351 when no exposed reachable target remained. Mammoths 528/530 received opted-in approach decisions after readiness. No premature activation, over-cap role, crash, or desync; no crush, useful raid damage/kill, forced readiness loss, or control comparison. | Adopt bounded first-eligible/assignment/rejection plus damage/kill/release/return and approach contribution diagnostics; keep unrelated extension/request symptoms out of scope. Then make the constructed opportunity durable enough for contact and run a matched changed/control pair. |
| 5 | Add bounded, saved mission outcome telemetry for raid/crush and per-target/armament shot plus damage outcome telemetry for opted-in Mammoth approach; no policy/order/balance change. | Hypothesis: existing activation logs may hide zero contact, weapon-only infantry kills mistaken for crushing, or stranded releases. Perturbation: instrument exact attacking ActorIDs, first eligible/assignment delay, applied damage/kill, contact signal/distance, duration, return cell, and first fire by each usable Mammoth armament. Failure: compile/save error, log flood, false role attribution, no final outcome distinction, or behavior/timing regression. Pass: bounded attributed outcomes and unchanged policy behavior in two differently timed constructed contact games. | Focused compile and NUnit passed 11/11; only the pre-existing unrelated AircraftHusk CA1825 warning. Seed 45005 reached tick 3,000 in 27.021s (111.011 ticks/s); seed 45006 reached tick 3,000 in 22.019s (136.223 ticks/s). Both passed required markers, produced replay/benchmarks, and had no fatal/desync/Lua error. | Commenter: `cycle-05-commentary/NARRATIVE.md`, clean bounded runs and attributable weapon kills but no control, raid contact, crush, or explicit Light-Tank preemption. Policy: `cycle-05-policy-review/POLICY-REVIEW.md`, insufficient evidence/high confidence; require explicit threat transition/reassignment/outcome, exact matched control, completed no-contact timeout, and separate true crush. Persistent extension retained as out of scope. Reviewer scratchpad output missing; canonical retained. Terra checkpoint: `cycle-review-05/CYCLE-REVIEW.md`, advisory adopted; initial observation could finish after entry cash lapsed because maintenance cash alone was rechecked. | Run 1: Mammoths 529/530 damaged and killed exposed harvester 541 at ticks 1164-1181; raid tanks 532-535 activated at 1151 and released at 1201 after 50 ticks with zero attributed damage/kills and return 102,27. Run 2: five Mammoths fired both primary 5120 and secondary 7168 armaments against silos; Mammoth 531 killed silo 574 at tick 404, Mammoth 528 killed silo 573 at 409, and Mammoth 526 killed e3 572 by weapon damage at 407. The four-tank silo raid released at 451 with zero damage/kills and return 128,23; a later refinery raid remained active through a 200-tick eligible delay at the bound. No crush mission logged. Ready opened exactly 300 ticks after observation in both runs and later dropped/recovered, but threat identity/preemption was not attributable. Telemetry stayed bounded and distinguished role outcomes from coincident Mammoth kills. | Instrumentation proved that the apparent raid/crush opportunities were false positives: Mammoths preempted raid contact and shot the infantry. Adopt the Terra concern in cycle 6 by requiring entry readiness throughout first observation while retaining lower maintenance cash only after established readiness. Then separate role targets in time/space, prove an actual contact crush plus return, make forced critical-threat transition explicit, and obtain an exact matched old-behavior control. Do not expand into resource extension. |
| 6 | Require entry readiness throughout the initial 300-tick observation; retain lower cash hysteresis only after readiness is established; add cash-lapse transition coverage. | Hypothesis: the cycle-05 reviewer found that cash could fall below the 1,000 entry floor during observation while the 0 maintenance floor allowed later activation. Perturbation: entry readiness false with maintenance readiness true immediately before and at the completion boundary; full-engine cash/resources drop at tick 150 and restore at 450, followed by timed economy, infantry, and Light-Tank pressure. Failure: Observing/Ready before continuous entry eligibility, loss of established maintenance hysteresis, compile/test error, role activation from the interrupted window, false crush attribution, stale role under threat, or engine integrity failure. | Focused compile/NUnit passed 12/12; only the pre-existing unrelated AircraftHusk CA1825 warning. Run 8 used the unrelated root checkout because the explicit worktree launcher was omitted; it reached tick 3,000/exit 0 but is invalid product evidence. Corrected seed 45007 task-worktree run passed tick 3,000 in 34.069s (88.045 ticks/s), required markers, replay/benchmarks, and no fatal/desync/Lua error. | Commenter attempt 1 misattributed the tick-1380 kill to HTNK 529; it was preserved as `cycle-06-commentary/NARRATIVE-ATTEMPT-1.md`, then a fresh factual rerun produced verified `cycle-06-commentary/NARRATIVE.md`. Policy: `cycle-06-policy-review/POLICY-REVIEW.md`, insufficient evidence/high confidence; cash reset and bounded releases make sense, but require matched control, bounded role/position/threat identity, and true crush/recovery. Extension observations remain out of scope. Reviewer scratchpad output missing; canonical retained. | Observing began tick 76; cash zero produced `not-ready` and `observation=none` at 226; restored cash began a new 0/300 window at 526; mixed-target-met first appeared exactly 300 ticks later at 826. No early role activation. Four Medium Tanks 532-535 raided silo 574 at 851 and released at 1001 with zero damage after Mammoths killed it. The same capped group raided harvester 577 at 1301, Medium Tank 534 dealt 7,040 attributed damage at 1364, Mammoth 527 killed it at 1380, and the raid released at 1401 with return 108,28. Threat=true caused not-ready at 1876 and 2851 but did not overlap an active raid, and the scripted threat identity/detection latency was not logged. No crush mission or false crush claim occurred. | The Terra review fix is proven in policy and full engine. Keep it. Adopt bounded rejection/ownership/threat diagnostics as the smallest next evidence change because two timed adjacent infantry opportunities still produced no crush state; distinguish absent general ground mission, ineligible/reserved Mammoths, route/target rejection, and threat presentation before changing tactics. Then force actual contact/recovery and run the exact matched control. |
| 7 | Add state-change-only crush rejection diagnostics for collaborator absence, Mammoth eligibility/reservation categories, non-urgent general-mission absence, route/target rejection, and final crusher selection; no policy/order/balance change. | Hypothesis: two adjacent Rocket Soldier opportunities produced no crush because a hidden ownership/mission/route guard rejected them; changing tactics without identifying that boundary could weaken safety. Perturbation: run 10 retained the cycle-06 cash/objective/infantry/raid/threat sequence; run 11 kept code and seed fixed but injected a second adjacent Rocket Soldier immediately before the later expected scan. Failure: repeated diagnostic flood, ambiguous first rejection, false crush credit, more than two crushers, stale order, fatal/desync/Lua failure, or progress stall. | Focused compile/NUnit passed 12/12; only the pre-existing unrelated AircraftHusk CA1825 warning. Run 10 passed tick 3,000 in 19.019s (157.709 ticks/s). Run 11 passed tick 3,000 in 19.023s (157.68 ticks/s). Both had required markers, replay/benchmarks, and no fatal/desync/Lua error. | Run 10 Commenter attempt 1 misnamed `mtnk` as Light Tanks and is preserved as `cycle-07-commentary/NARRATIVE-ATTEMPT-1.md`; verified rerun: `cycle-07-commentary/NARRATIVE.md`. Policy: `cycle-07-policy-review/POLICY-REVIEW.md`, insufficient/high; retain the outer mission safety gate and expose the exact category before relaxing it. Run 11: `cycle-07-contact-commentary/NARRATIVE.md`; policy `cycle-07-contact-policy-review/POLICY-REVIEW.md`, insufficient/high; ordinary weapon damage is not crush evidence and a sustained known-objective contact fixture is required. Both reviewer scratchpad outputs were missing, so canonical scratchpad was retained. | Run 10 first identified `no non-urgent general mission` at tick 826 for eligible Mammoths 526-531; later live structure objectives had no valid infantry. Its capped Medium Tank raid reached a refinery, logged 70,400 damage, and released at tick 1601. Run 11's four-tank raid killed harvester 577 for 28,160 attributed damage and released at tick 1501. The injected `e3#586` was acquired by five ordinary Mammoth attacks at ticks 1528-1536, took weapon damage, and never produced a crush start/contact/return; the crush owner later reported no valid target. Both runs stayed bounded and clean, but the positive crush path failed. | Keep the safety gate and do not claim contact success. In cycle 8 distinguish missing GeneralAttack membership, invalid squad target, and urgent mission per candidate, plus bounded infantry rejection reasons. Then sustain exactly one nearby candidate until a known compatible objective/scan overlap, require at most two temporary owners, contact attribution, release, and objective rejoin; follow with exact old-behavior control rather than changing tactical thresholds speculatively. |
| 8 | Classify each eligible Mammoth's GeneralAttack state as missing squad, invalid target, urgent, or ready; replace the equivalent infantry selection pipeline with stage counts for visible/route/local/crushable/reachable/bounded/dense/dangerous/safe targets. No threshold, order, ownership, balance, or selected-target policy change. | Hypothesis: the positive crush path is losing a short-lived infantry candidate because the general squad lacks a target or becomes urgent at the scan, but the current aggregate log cannot distinguish that from route, crushability, reachability, or threat rejection. Perturbation: run 12 sustained one adjacent hostile Rocket Soldier through bounded replacement while preserving six Mammoths; run 13 reduced the force to exactly two Mammoths and three Medium Tanks. Both retained a live GeneralAttack structure objective and all normal modules. Failure: selection behavior changes, compile/test error, unbounded log/scan cost, no exact rejection category, more than two crushers, weapon kill mistaken for contact, no release/rejoin, fatal/desync/Lua failure, or stall. Pass: state-change logs identify the precise mission/target stage and, when overlap exists, a capped temporary Move mission produces explicit contact attribution and bounded return. | `git diff --check` passed. Focused compile/NUnit passed 12/12; only the pre-existing unrelated AircraftHusk CA1825 warning. Run 12 reached tick 3,000/exit 0 in 22.024s; run 13 reached tick 3,000/exit 0 in 18.019s. Both had required engine markers, replay/benchmarks, and no fatal/desync/Lua error. The harness correctly failed each only because required `contact-kill=True` was absent. | Run 12 Commenter: `cycle-08-commentary/NARRATIVE.md`; policy: `cycle-08-policy-review/POLICY-REVIEW.md`, mixed/medium. Run 13 Commenter: `cycle-08-two-mammoth-commentary/NARRATIVE.md`; policy: `cycle-08-two-mammoth-policy-review/POLICY-REVIEW.md`, mixed/medium. Both reviews preserve the safety gate/cap/return and require target-lifecycle, killer, position, and contact evidence before admission/retry changes. Both reviewer scratchpad outputs were missing, so the canonical scratchpad was retained. | At tick 826 both runs identified every eligible Mammoth as having an invalid GeneralAttack target. In run 12 a later scan had one visible/route/local/crushable candidate but zero reachable; at tick 1476 exactly Mammoths 526/529 started against `e3#585` with silo objective 574 and returned at 1501 on target invalidation, duration 25, zero damage, `contact-kill=False`, return 101,25, cooldown 2251. Its four-Medium-Tank raid killed harvester 577 for 21,120 logged damage and released. Run 13 reached ready value 3,400/5,800, then exactly its only Mammoths 526/527 started at tick 1526 against `e3#588` with factory objective 561 and returned at 1551 on target invalidation, duration 25, zero damage, `contact-kill=False`, return 13,8, cooldown 2301. Both resumed ordinary fire; neither proves what invalidated the target. | Keep the safety gate, cap, and immediate release; reject the speculative policy-review suggestion to shorten no-contact cooldown or add retry until causal evidence exists. In cycle 9 add bounded target lifecycle/external-killer and start-position geometry telemetry, then move the required Rifle screen outside immediate weapon-preemption range while retaining readiness, exactly two Mammoths, a stable threatening candidate, and the ordinary objective. Require attributed contact plus return; if target still invalidates, use the new cause/geometry evidence before changing candidate or order policy. |
| 9 | Add state-change-bounded mission-start geometry and selected-target external-killer attribution. No admission, target ranking, movement, cooldown, ownership, weapon, or balance change. | Hypothesis: the 25-tick target invalidations are caused by an unobserved friendly unit killing the selected infantry, or the selected Mammoths begin too far from the target for contact; changing tactical policy before distinguishing those causes could weaken the safety gate. Run 14 replaced the Rifle screen and presented an adjacent candidate one tick before the expected scan. Run 15 separated screen replacement by 149 ticks and held one Rocket Soldier stationary two cells off route. Both retained exactly two Mammoths, ordinary AIs/modules, readiness, and a live structure objective. Failure: compile/save error, log flood, ambiguous killer/geometry, more than two crushers, ordinary damage mistaken for contact, no return, fatal/desync/Lua error, or stall. Pass: one bounded event chain reports start cells and either attributed external/selected-Mammoth damage or true empty-damage-type contact, followed by return. | `git diff --check` passed. Focused compile/NUnit passed 12/12; only the pre-existing unrelated AircraftHusk CA1825 warning. Run 14 reached tick 3,000/exit 0 in 35.082s; run 15 reached tick 3,000/exit 0 in 18.018s. Both had Headless MAX/bot markers, replay/benchmarks, empty Lua logs, and no fatal/desync; the harness correctly failed missing start/contact/return patterns. | Paired Commenter: `cycle-09-commentary/NARRATIVE.md`, verified as a failed diagnostic batch with no control or false contact claim. Policy: `cycle-09-policy-review/POLICY-REVIEW.md`, insufficient evidence/high; reject as validation evidence, preserve ordinary ranged fire in live policy, and use fixture-only held fire only if needed for attribution. The reviewer scratchpad output was missing, so the canonical scratchpad was retained. | Run 14 never exposed a visible candidate at a compatible scan; tick 1551 reported objective `fact#561`, visible/route/local/reachable all zero. Run 15 exposed stationary `e3#579`, but both ordinary Mammoth secondary weapons fired at tick 1550 and Mammoth 526 killed it at tick 1562 for 6,500 weapon damage; no crush mission started, so this is causal pre-mission ranged-fire preemption, not an external mission killer or contact. Its next compatible scans at 1601/1751 found zero infantry. Both runs stayed clean and bounded, but neither exercised the new start/external-killer telemetry or literal crush acceptance. | Keep ordinary ranged fire and reject the review's held-fire suggestion as live policy; it is acceptable only as a fixture control. Cycle 10 should classify target invalidation state/cell and rerun the original sustained-candidate/ordinary-screen scenario that already produced an actual capped mission. Require the new external-killer or invalidation cause plus start geometry, then decide whether fixture-only fire control is needed. Do not weaken the mission safety gate, add retries, or change cooldown yet. |
| 10 | Classify active crush-target invalidation as missing, dead, out-of-world, wrong type/owner, hidden/unviewable, or no longer crushable, with last cell and affected crusher IDs. No policy, order, ownership, weapon, cooldown, or balance change. | Hypothesis: the Cycle 8 mission target may be destroyed by an ordinary screen actor, leave the world through another lifecycle path, or become merely hidden; the generic invalidation reason cannot distinguish these causes. Perturbation: run 16 restored the original sustained candidate and ordinary Rifle screen. Run 17 retained the same seed/ordinary AIs/modules but used a map-local ten-second fire pause for GDI Minigunners and Mammoths across the known mission window. Failure: compile/save error, behavior change, log flood, no mission, ambiguous cause, false contact credit, more than two crushers, no return, fatal/desync/Lua error, or stall. Pass: exact lifecycle/killer classification in run 16 and true capped contact/return in the held-fire causal control. | `git diff --check` passed. Focused compile/NUnit passed 12/12; only the pre-existing unrelated AircraftHusk CA1825 warning. Run 16 reached tick 3,000/exit 0 in 17.018s and passed all strict diagnostic patterns. Run 17 reached tick 3,000/exit 0 in 20.007s with replay/benchmarks, empty Lua log, and no fatal/desync; its harness correctly failed because contact/dead-return patterns were absent. | Corrected Commenter: `cycle-10-commentary/NARRATIVE.md` (first attempt preserved after it mislabeled `e3`); Policy: `cycle-10-policy-review/POLICY-REVIEW.md`, insufficient/high. The policy review endorses dead-target return, rejects weakening safety overrides, and requests exact abort/movement state before policy changes. Reviewer scratchpad output was missing, so canonical scratchpad was retained. Cycle checkpoint: `cycle-review-10/CYCLE-REVIEW.md`; advisory adopted because `SelectTarget` performs reachability checks over the full visible infantry set before applying its configured cap. | Run 16: at tick 1526 one Mammoth `526@111,26` started against `e3#585@110,26`; Minigunner `e1#552@106,26` killed it three ticks later for 825 external damage, and the Mammoth returned at 1551 with classified `(dead e3#585@110,26)`, zero mission damage, and `contact-kill=False`. Run 17: exactly Mammoths 526/527 started adjacent at tick 1476 under fixture-only held fire, but after 50 ticks both returned on grouped owner/objective/threat/leash change with zero damage and no contact. Both runs remained bounded and resumed useful ordinary attacks. Literal crush contact remains unproven. | Keep exact invalidation attribution and the safe dead-target return. Adopt the checkpoint concern in cycle 11 with a deterministic cheap shortlist before reachability/path checks. Firing suppression alone did not produce contact, so do not change target admission, cooldown, or override precedence. Also distinguish the exact abort predicate and bounded movement/target survival state; then determine whether the current `Move` order physically permits contact before altering live policy. Obtain a true ordinary-AI contact plus return and later an exact old-behavior control. |
| 11 | Deterministically cap ready Mammoth candidates at eight and cheap route-local/crushable infantry at 32 before reachability checks; split grouped mission aborts into exact eligibility/squad/objective/route/leash reasons and add target state plus participant cells at return. No order, admission threshold, cooldown, ownership, weapon, or balance change. | Hypothesis: the cycle-10 reviewer found an unbounded pre-cap reachability scan, and the held-fire mission's grouped abort reason concealed whether movement or a legitimate safety transition prevented contact. Run 18 used the same held-fire map/seed; run 19 additionally paused map-local Medium Tank fire. Failure: compile/YAML error, selection nondeterminism, more than the configured reachability budget, changed safety precedence, ambiguous abort, false contact, missing return, fatal/desync/Lua error, or stall. Pass: bounded deterministic evaluation and an exact abort/movement chain preserving cap and return. | `git diff --check` passed. Focused compile/NUnit passed 12/12; only the pre-existing unrelated AircraftHusk CA1825 warning. Run 18 reached tick 3,000/exit 0 in 20.007s (149.914 ticks/s) and passed strict exact-abort/position patterns. Run 19 reached tick 3,000/exit 0 in 15.007s with replay/benchmarks, empty Lua log, and no fatal/desync, but correctly failed because no crush mission started. | Commenter: `cycle-11-commentary/NARRATIVE.md`; Policy: `cycle-11-policy-review/POLICY-REVIEW.md`, insufficient/high. Both accept run 18 only as safe recovery attribution and reject run 19 as movement/contact evidence. Policy recommends a living isolated target and then same-map old control. Reviewer scratchpad output was missing, so canonical scratchpad was retained. | Run 18 started exactly Mammoths `526@110,26,527@112,26` at tick 1451 against `e3#576@109,26`. Mammoth 527 moved to 110,27 while 526 remained at 110,26; neither contacted. Medium Tank 528 killed the target at tick 1548 for 880 external damage, and both returned at 1551 with classified dead target, duration 100, zero mission damage, and `contact-kill=False`. Run 19 never opened a compatible mission despite clean engine progress and ordinary combat, so it does not test movement. The cap is now applied before domain checks in code; literal contact remains unproven. | Keep the bounded shortlist, exact transitions, and safe return. Do not tune thresholds or weaken safety. In cycle 12, inspect the engine's actor-target movement/crush order semantics and replace the demonstrably non-contacting cell `Move` only if a scoped target-following movement activity preserves cancellation and leash overrides. Force a living isolated ordinary-AI target, require observable contact and return, then obtain the exact old-behavior control. |
| 12 | Replace the crush mission's generic eight-cell-radius `Move` with an HTNK-only internal actor-target order whose existing repathing activity selects the target's exact crushable cell; revalidate enemy ownership and crushability at the resolver. | Hypothesis: generic `Move` resolves with an eight-cell `nearEnough` radius and nearest-cell fallback, so it can finish beside an infantry target without entering its cell. Perturbation: retain all mission admission, cap, cooldown, leash, and return rules while changing only the final movement primitive; run 20 uses the byte-identical Cycle 11 held-fire map and seed. Failure: compile/YAML error, custom order leak, rejected valid target, more than two crushers, lost cancellation/leash override, no contact, false contact attribution, fatal/desync/Lua error, or stall. Pass: an isolated living ordinary-AI target is killed by movement/contact, at most two Mammoths participate, and every survivor returns; the same map on exact old control lacks the scoped behavior. | `git diff --check` passed. Focused compile/NUnit passed 12/12; only the pre-existing unrelated AircraftHusk CA1825 warning. Run 20 reached tick 3,000/exit 0 in 14.020s with replay/benchmarks, empty Lua log, and no fatal/desync, but correctly failed the required contact/dead-return patterns. | Commenter: `cycle-12-commentary/NARRATIVE.md`; Policy: `cycle-12-policy-review/POLICY-REVIEW.md`, insufficient/high. Both reject run 20 as contact/control evidence and require assignment-bound movement/order traces. The policy review explicitly preserves named objective/retreat/threat preemption and rejects making the crush uninterruptible. Reviewer scratchpad output was missing, so canonical scratchpad was retained. | At tick 1476 Mammoth `526@112,26` started adjacent to living `e3#574@111,26` with objective `proc#567@116,26`. It remained at 112,26 and returned at tick 1551 after the general objective became invalid, duration 75, zero damage, `contact-kill=False`; the target remained alive. This proves the first exact-cell activity still did not produce contact before a legitimate safety override. | Preserve the valid objective-cancellation return and reject the suggestion to shield crush from mission changes. In cycle 13 add state-change-bounded resolver/activity diagnostics that distinguish dropped/rejected custom order, occupied-cell admission, path start/completion, movement, and external cancellation. Require an assignment-bound contact/return chain in the constructed case before any further movement-policy change; do not depend on a fixed actor ID. |
| 13 | Add HTNK-only, mission-bounded custom-order diagnostics for resolver rejection/acceptance, occupied-target enterability, prior activity, exact-cell activity start/movement/completion, cancellation, and move result. No policy, admission, target, movement, cooldown, ownership, weapon, or balance change. | Hypothesis: run 20 cannot distinguish an order rejected before the trait, a resolver guard rejection, an occupied-cell path rejection, a custom activity that never starts, or repeated cancellation before one-cell completion. Perturbation: rerun the same constructed ordinary-AI case, but bind required evidence to the selected target rather than a fixed ActorID and capture at most the seven 25-tick order attempts for at most two Mammoths. Failure: compile/YAML error, unbounded log flood, ambiguous first failed boundary, changed safety precedence, false contact, fatal/desync/Lua error, or stall. Pass: the trace identifies the first resolver/activity boundary that prevents contact, or records true contact plus prompt return. | `git diff --check` passed. Focused compile/NUnit passed 12/12; only the pre-existing unrelated AircraftHusk CA1825 warning. Run 21 passed its assignment-bound trace contract at tick 3,000/exit 0 in 14.022s (213.896 ticks/s), with replay/benchmarks, empty Lua log, and no fatal/desync. | Commenter: `cycle-13-commentary/NARRATIVE.md`; Policy: `cycle-13-policy-review/POLICY-REVIEW.md`, insufficient/high. Both verify the exact non-contact sequence. The policy review preserves the no-deadlock ordinary-attack fallback and objective/safety precedence, and requests a second case where weapons cannot promptly solve the threat but the occupied target cell is otherwise usable. Reviewer scratchpad output was missing, so canonical scratchpad was retained. | At tick 1726 Mammoth `526@109,27` started against adjacent `e3#594@108,27`. Resolver tick 1731 accepted enemy/crushable target and reported `enter-all=False`, but `enter-stationary=True`, `enter-immovable=True`, `enter-none=True`. The exact-cell activity started at 1732 and completed at 1733 without cancellation as `CompleteDestinationBlocked`, proving the inherited candidate prefilter's `BlockedByActor.All` check discards the moving occupied target cell before the pathfinder can try its normal fallback modes. Mammoth 526 then weapon-killed the target at 1736; mission returned dead-target at 1751 with `contact-kill=False`. | Do not claim contact and do not add a retry or weaken cooldown/safety precedence. Cycle 14 may make the smallest scoped candidate-admission change that ignores only the already-verified selected crushable target actor during the inherited activity's initial candidate prefilter, preserving every other blocker and normal path search. Test it in the same held-fire causal fixture; require true contact plus prompt return before control. |
| 14 | Allow the target-following movement path to ignore one explicitly supplied actor across candidate admission, path search, and final step execution; only the HTNK Economy crush subclass supplies its already-verified selected enemy crush target and revalidates it continuously. All existing movement callers retain the null/default behavior. | Hypothesis: the selected crushable infantry itself is the sole blocker preventing the exact-cell activity from forming and executing a path. Run 22 used the byte-identical Cycle 13 fixture. Run 23 moved the fixture-only Rifle/Mammoth hold-fire window three seconds earlier and extended it five seconds to preserve the first selected target. Failure: compile/YAML error, global movement change, overlap with a no-longer-crushable target, more than two crushers, no contact kill, lost objective return, fatal/desync/Lua error, or stall. Pass: assignment-bound trace shows movement into the live selected target, an observable contact kill, and prompt normal-objective return. | `git diff --check` passed. Focused compile/NUnit passed 12/12; only the pre-existing unrelated AircraftHusk CA1825 warning. Runs 22 and 23 both reached tick 3,000/exit 0 in 14.014s and 14.015s with replay/benchmarks, empty Lua logs, and no fatal/desync, but correctly failed strict contact requirements. | Commenter: `cycle-14-commentary/NARRATIVE.md`; Policy: `cycle-14-policy-review/POLICY-REVIEW.md`, insufficient/high. Both reject the pair as contact evidence, accept Run 22's 25-tick dead-target recovery, and request a map-identical decisive fixture with bounded assignment-to-contact transitions. Reviewer scratchpad output was missing, so canonical scratchpad was retained. | Run 22 selected `e3#571@111,27` at tick 1451, but `e1#553` killed it for 825 damage in that same tick; both queued custom orders resolved as invalid at 1458, and the capped mission returned dead-target at 1476 with `contact-kill=False`. Run 23's earlier fire hold prevented that preemption but altered the combat sequence: no compatible GeneralAttack mission existed at the late scans, so no crush mission or custom order ran. Neither game exercised the Cycle 14 movement path. | Do not claim contact and do not consume a control run. Cycle 15 should add only the smallest bounded selected-target ignore trace, then use a later ready-state opportunity where the ordinary objective was already observed valid before applying a short fixture-only fire hold. Preserve all live admission/safety precedence and reserve the identical map for exact-base control if contact succeeds. |
| 15 | Add one assignment-bound diagnostic to compare normal target-cell enterability with enterability when ignoring exactly the already-verified selected actor; carry the same target ActorID through activity start/move/completion logs. No movement, policy, admission, cooldown, ownership, weapon, or balance change. | Hypothesis: a compatible ready/objective window can exercise the Cycle 14 path, and target-only admission will permit exact-cell movement without relaxing any other blocker. Run 24 preserved normal combat through the observed tick-2851 scan, then presented one adjacent target under a brief fixture fire pause. Run 25 returned to the previously proven compatible assignment fixture, preserved normal combat through tick 1425, then paused Rifle, Mammoth, Medium Tank, and Rocket Launcher fire for eight seconds. Failure: no compatible assignment, target-only admission failure, movement without contact, more than two crushers, missing return, fatal/desync/Lua error, or stall. Pass: one stable assignment ID connects selection, resolver, movement, contact kill, and prompt return. | `git diff --check` passed. Focused compile/NUnit passed 12/12 without a warning. Run 24 reached tick 3,000/exit 0 in 17.019s with replay/benchmarks, empty Lua log, and no fatal/desync; its strict contact harness correctly failed. Run 25 passed every strict assignment/contact/return pattern at tick 3,000/exit 0 in 15.012s (199.795 ticks/s), with replay/benchmarks, empty Lua log, and no fatal/desync. | Paired Commenter: `cycle-15-commentary/NARRATIVE.md`, verifies Run 25's assignment-to-contact chain while correctly rejecting Run 24 as a matched comparison because its map hash differs. Policy: `cycle-15-policy-review/POLICY-REVIEW.md`, insufficient/high; accepts the short contact/return and useful recovery, rejects broader effectiveness claims, and requires byte-identical old control plus adversarial threat/ownership evidence. Its request for more cancellation telemetry is superseded by the Cycle 15 code review's source/evidence diagnosis. Reviewer scratchpad output was missing, so the canonical scratchpad was retained. Cycle checkpoint: `cycle-review-15/CYCLE-REVIEW.md`; advisory adopted because the owner refreshes a stationary-target order over the active activity, causing the tick-1557 re-resolution and tick-1558 cancellation. | Run 24 exposed one visible infantry at tick 2851 but rejected it before locality as off the general formation route (`visible=1 route=0`), so no custom move ran. Run 25 started exactly Mammoths `526@112,27,527@111,26` at tick 1526 against `e3#575@111,27`, objective `silo#565@101,25`. Both resolvers and activities carried assignment 575; Mammoth 526 moved into 111,27 at tick 1547, then killed the target by zero-damage-type contact at tick 1576 for 4,500 damage. The mission returned at the same tick with `contact-kill=True`, both survivors present, duration 50, return 101,25, and cooldown 2326. The repeated resolver/cancellation means the two-Mammoth path is not yet clean. | Adopt the checkpoint concern in cycle 16: preserve each active same-target crush activity and only reissue after target relocation, activity completion/absence, or documented recovery. Add a focused boundary, rerun the byte-identical Run 25 map, and require one resolver per stationary-target Mammoth plus contact/return. Then obtain exact-base control. |
| 16 | Preserve each non-canceling same-target `EconomyMammothCrushMove` activity when the owner reaches its order interval; issue a replacement only when no such activity remains. Add a focused target-ID boundary. | Hypothesis: interval refresh cancels valid in-flight movement and creates order fighting; suppressing only an active same-target refresh will retain moving-target repathing, safety cancellation, and recovery while producing one resolver per Mammoth for a stationary target. Perturbation: rerun the byte-identical Cycle 15 Run 25 map/seed after this fix. Failure: compile/test error, more than one resolver per Mammoth, lost contact, stranded second Mammoth, missing return, fatal/desync/Lua error, or stall. Pass: focused same/different/absent target checks and a clean one-resolution-per-Mammoth contact/return trace. | `git diff --check` passed. Focused compile/NUnit passed 13/13; only the pre-existing unrelated AircraftHusk CA1825 warning. Runs 26 and 27 reached tick 3,000/exit 0 in 16.016s and 16.010s with replay/benchmarks, empty Lua logs, and no fatal/desync. Both correctly failed the strict contact harness. | Commenter: `cycle-16-commentary/NARRATIVE.md`, a limited same-build reproducibility batch with clean execution, one resolver per Mammoth in Run 27, safe recovery, no contact, no winner, and no old control. Policy: `cycle-16-policy-review/POLICY-REVIEW.md`, mixed/high; preserve the cap, no-refresh guard, threat/leash/target invalidation, and recovery, but treat a one-cell move by the same external objective as return context rather than immediate cancellation. Reviewer scratchpad output was missing, so canonical scratchpad was retained. | Both runs loaded exact map SHA `71bc97a559a130b34daf5242cab998873c9f58c1c2787c3b400063a4eaae1c4f`, seed 45007, lobby, launcher, and tick bound but followed distinct trajectories before the refresh guard could matter. Run 26 had readiness cash 19,441 at tick 826 and no assignment. Run 27 had 19,466, started Mammoths `526@111,28,527@112,27` against `e3#575@110,28` at tick 1476, resolved each exactly once at 1482, issued no refresh, then returned safely at tick 1501 because the same general objective moved from 119,28 to 119,27. One Mammoth moved before cancellation; target stayed alive, duration 25, damage zero, `contact-kill=False`, cooldown 2251. | Keep the no-refresh guard. Adopt the policy review's narrowly evidenced concern in cycle 17: preserve an already-selected local target when the same valid external objective moves within the existing leash, while still canceling on objective identity/validity change, urgent mission, threat, ownership, route, leash, timeout, or target invalidation. Add a focused boundary and rerun the constructed case with a fixed-objective perturbation; do not weaken locality or safety thresholds. Exact-base control remains required after current contact becomes reproducible. |
| 17 | Track the post-start general objective by stable actor identity rather than exact cell; retain the start cell as the immutable route/leash/recovery anchor. Add a focused same/different/missing objective identity boundary. | Hypothesis: a same-objective one-cell move can cancel an otherwise valid nearby crush before contact; ignoring cell motion while preserving objective identity/status and the original route anchor may instead reveal remote pursuit or stale-objective behavior. Perturbation: exercise the constructed ordinary-AI fixture after this change, requiring that a moving same objective does not itself cancel, while identity/validity, target, threat, leash, timeout, and ownership overrides remain. Failure: compile/test error, remote chase, lost objective-change cancellation, repeated resolver, no contact/recovery, fatal/desync/Lua error, or stall. Pass: same-ID/different-ID/missing focused checks plus one capped assignment-to-contact-return chain with no refresh. | `git diff --check` passed. Focused compile/NUnit passed 14/14; only the pre-existing unrelated AircraftHusk CA1825 warning. Runs 28 and 29 reached tick 3,000/exit 0 in 18.007s and 22.014s with replay/benchmarks, empty Lua logs, and no fatal/desync; both correctly failed contact. | Commenter: `cycle-17-commentary/NARRATIVE.md`, verifies bounded cancellation/recovery in Run 28 and no-start in Run 29, with no contact, winner, or old control. Policy: `cycle-17-policy-review/POLICY-REVIEW.md`, insufficient/high; external fire invalidation is an expected live cancellation path, run 29's first evidenced rejection is locality, and neither result authorizes weakening safety. Reviewer scratchpad output was missing, so canonical scratchpad was retained. | Run 28 started exactly Mammoths `526@109,27,527@112,26` at tick 1676 against `e3#592@109,27` with fixed factory objective 561@13,8. Mammoth 527 dealt 1,760 ordinary damage and Minigunner 551 killed the target before both custom orders resolved as invalid at tick 1683. It returned dead-target at tick 1701, duration 25, `contact-kill=False`, cooldown 2451. Run 29 extended fixture-only fire suppression through the latest observed window, but this altered the ordinary trajectory: at tick 1901 one infantry was visible/on-route yet outside the five-cell locality bound (`local=0`), so no mission started. Neither run showed objective-cell cancellation, remote chase, repeated resolver, crash, or desync. | Keep the safe identity predicate but do not claim contact or strategic acceptance. Reject further locality/threat relaxation and repeated fixture tuning. Cycle 18 should remove temporary resolver/activity and high-frequency approach diagnostics while retaining concise owner-level readiness/assignment/outcome/return evidence, then run focused and broad validation. Because literal acceptance, exact control, adversarial, persistence, performance, and final natural-match proof remain incomplete, prepare a truthful `First iteration - testing` handoff. |
| 18 | Remove temporary custom-order resolver/activity probes and per-target approach range/shot/damage telemetry; retain state-change owner diagnostics for readiness, mission assignment, outcomes, release, and return. No behavior, policy, ownership, balance, or save-state change. | Hypothesis: diagnostic-only interfaces, collections, path-enterability checks, and movement logs can add avoidable hot-path cost or warning/error noise; removing them might accidentally break compilation/YAML or obscure the concise role lifecycle evidence needed for handoff. Perturbation: validate the cumulative implementation after deleting the probes, then run a fresh ordinary-AI game rather than another tuned contact fixture. Failure: compile/static/YAML error, changed focused policy result, fatal/desync/Lua error, missing owner-level readiness/mission lifecycle evidence, or progress stall. Pass: all scoped and broad gates are clean, the fresh game advances normally with bounded owner logs, and no removed diagnostic strings remain. | `git diff --check` passed. Focused compile/NUnit passed 14/14; only the pre-existing unrelated AircraftHusk CA1825 warning. Initial `make check` failed on four scoped code-style diagnostics: one redundant default accessibility modifier and three unnecessary imports. Fresh ordinary-AI game remains pending. | Final fresh match roles were completed with Cycle 19 after the mechanical release repair. | Source/YAML search confirms the Cycle 13-15 resolver/activity probes and Cycle 2-5 approach telemetry/config switches are gone. Runtime policy tests remained unchanged; release-gate style defects require the mechanical Cycle 19 repair. | Apply only the exact style diagnostics in Cycle 19, rerun focused and broad CNC gates, then run one fresh ordinary-AI endurance game. Publish `First iteration - testing` unless current evidence unexpectedly closes the documented acceptance gaps. |
| 19 | Satisfy repository code style by omitting the default `internal` enum modifier and three analyzer-confirmed unnecessary imports. No runtime behavior, policy, diagnostics, ownership, balance, or save-state change. | Hypothesis: the cumulative implementation cannot pass release checks because style analyzers run as errors; the exact four mechanical changes might reveal further compile/interface/YAML failures. Perturbation: rerun `make check`, focused tests, and CNC MiniYAML from the cleaned source, then exercise the current binary in a fresh ordinary-AI game. Failure: any scoped warning/error, interface or YAML failure, changed focused result, fatal/desync/Lua error, or game progress stall. Pass: all gates clean and fresh engine progress with bounded owner-level diagnostics. | `make check` passed warning-as-error Debug compile plus explicit/conditional interface gates with zero warnings. `make test` passed Release compile and all CNC MiniYAML checks with zero warnings. Final focused NUnit passed 14/14; only the pre-existing unrelated AircraftHusk CA1825 warning. Run 30 passed the harness at tick 60,000/exit 0 in 429.424s (139.709 valid ticks/s), with replay/benchmarks and no fatal/desync/Lua error. | Commenter: `cycle-19-commentary/NARRATIVE.md`, verifies setup, clean bounded execution, no control/winner, severe late Brutalis economy/support deterioration, and no CNC-45 role activation. Its `natural game over` interpretation is qualified because the configured-exit marker occurs first. Policy: `cycle-19-policy-review/POLICY-REVIEW.md`, insufficient evidence/high confidence; preserve scope, obtain explicit winner/control/role telemetry, and defer unavailable-request rate limiting plus extension/zero-harvester recovery to separate work. The reviewer emitted no replacement scratchpad, so canonical scratchpad was retained. | Run 30 used Empire Earth4 SHA `de517bb85418139fb1125888578c06516bea223d0c22652d038e48e70d1d64cc`, seed 45019, SkyNet Nod versus Brutalis GDI, $20,000, all ordinary modules. It produced 107 bounded production owner lines across 60,000 ticks and zero removed resolver/activity or approach telemetry lines. Readiness never opened under sustained screen/artillery/cash/threat failure, so no raid/crush owner mission occurred. The configured-exit marker precedes the generic natural-exit marker; treat it as a safety-bound endurance run, not natural game-over. | Preserve the release-clean implementation and do not spend Cycle 20 on speculative policy changes. Complete report, PR/checks, and final Sol-high review, then hand off `First iteration - testing` with exact unclosed acceptance/control/adversarial/natural-match gaps. |
| 20 | Final-review response: exclude any higher-priority temporarily controlled actor, including an active Mammoth crusher, from general ground-reinforcement hold counts and join orders. Add one pure boundary test. | Hypothesis: while the crush owner controls a Mammoth, `Squad.UpdateGroundReinforcements` can still issue a competing `AttackMove`, canceling or fighting the temporary mission. Perturbation: make the existing protecting/temporary predicates one shared reinforcement-order gate, then rerun the compatible two-Mammoth contact fixture with all ordinary modules. Failure: focused/broad gate error, a protected or temporary actor remains orderable, more than two crushers, no contact/return, fatal/desync/Lua error, or stall. Pass: the pure boundary rejects both owner classes, the full engine completes a capped contact/return, and release gates remain clean. | Focused `EconomyTroopPolicyTest|StrategicGroundScoringTest` passed 21/21; only the unrelated pre-existing AircraftHusk CA1825 warning. `make check` passed Debug warning-as-error and interface gates with zero warnings/errors. `make test` passed Release compile and all CNC MiniYAML maps with zero warnings/errors. Run 31 passed tick 3,000/exit 0 in 30.045s (99.815 valid ticks/s), replay/benchmarks, and no fatal/desync/Lua/unhandled marker. | Commenter: `cycle-20-commentary/NARRATIVE.md`, verifies exactly two Mammoths, a tick-1501 contact kill, immediate return, and clean engine progress, but no control or competing-order trace. Policy: `cycle-20-policy-review/POLICY-REVIEW.md`, insufficient/high; this is valid smoke evidence, not causal ownership proof. Final PR review: `final-pr-review/FINAL-REVIEW.md`, blocked; highest-impact agreed finding fixed in this one allowed response. Cycle checkpoint: `cycle-review-20/CYCLE-REVIEW.md`; accepts the ownership exclusion and raises the late raid candidate cap. | Run 31 used map SHA `71bc97a559a130b34daf5242cab998873c9f58c1c2787c3b400063a4eaae1c4f`, seed 45007, SkyNet Nod versus Brutalis GDI, $20,000. Mammoths 526/527 started at tick 1451 against `e3#571`; Mammoth 526 contact-killed it for 4,500 at tick 1501; both returned toward objective 568 immediately. This falsifies a basic runtime/capped-role regression but cannot prove that a conflicting reinforcement order was attempted and excluded. | Keep the safe ownership exclusion. Accept the checkpoint concern that the Medium Tank raid performs reachability/attack checks and sorting before `MaximumTargetCandidates`; resolving it requires Cycle 21, so do not modify product code. Hand off `First iteration - testing` with this boundedness risk plus the existing acceptance/control/persistence/stress gaps. |

## Handoff receipt

- Proposed status: `First iteration - testing`
- Final branch/head: `agent/round-20260807-cnc45-economy-troop-use`; product/publication head pending Cycle 20 commit
- PR and checks: draft PR `#94`; initial Linux and Windows checks passed on `26c64c59e34975cd2102e902b9beb32ab1255513`; Cycle 20 refresh pending
- Cycles used: `20/20`; Cycle 20 is the single allowed final-review response
- Acceptance evidence: Focused policy boundaries now pass 21/21. Constructed games prove continuous readiness timing, capped/released Medium Tank raids with attributed useful damage, target-valid Mammoth approach-range selection before temporary telemetry cleanup, at-most-two crush ownership, contact kills in Cycles 15 and 20, and bounded return/cancellation. Cycle 20 excludes temporary owners from general reinforcement hold/join orders and reproduces a clean capped contact/return, but lacks the competing-order trace/control needed for causal proof. Brutalis and Iron Reaper mature-force composition, dual-fire outcome, air/paused/modified matrix, persistence, and literal final four-effect acceptance remain incomplete.
- Adversarial evidence: 31 full-engine games covered cash-lapse readiness, threat preemption, false crush attribution, dead/invalid target recovery, objective changes, route locality rejection, cap/release, custom occupied-cell movement, and a post-review ownership smoke case. Required final three clean post-fix adversarial scenarios, blocked topology, five-AI stress, full contention, and save/load remain incomplete.
- Old-behavior control and comparative result: Cycle 1 exact-base control reached game over but the pair was invalid because the changed run stalled and lacked a summary; no valid final matched control or material-improvement claim exists.
- Match narratives and routine policy-review conclusions: Fresh Commenter/Policy Reviewer artifacts exist for every materially judged game/batch. Cycle 20 policy review is `insufficient evidence`/high confidence: the contact/return is valid smoke evidence but does not prove exclusion without a conflicting-order trace and matched old behavior.
- Terra cycle code reviews and dispositions: Cycle 5 continuous entry-cash concern adopted/fixed in Cycle 6; Cycle 10 pre-cap crush reachability scan concern adopted/fixed in Cycle 11; Cycle 15 same-target order refresh concern adopted/fixed in Cycle 16; Cycle 20 late raid target-cap concern accepted and handed off because fixing it requires Cycle 21.
- Sol-xhigh policy escalation (unused, or test count/path/conclusion): unused
- Final regression: After Cycle 20, `make check`, `make test`, and focused NUnit 21/21 pass. Run 31 reached its 3,000-tick bound with exit 0, replay/benchmarks, one contact kill/immediate return, and no fatal/desync/Lua error. Run 30 remains the fresh 60,000-tick endurance result but did not activate CNC-45 roles and is not natural-conclusion proof.
- Error/warning and diagnostic-cleanup result: release gates have zero warnings/errors. The focused command reports only the unrelated pre-existing AircraftHusk CA1825 warning. Temporary custom-order/activity and per-target approach/shot/damage probes were removed; concise owner state-transition diagnostics remain.
- Performance/determinism result: Run 30 achieved 139.709 valid ticks/s; Run 31 achieved 99.815 valid ticks/s in a different short fixture and is not a throughput comparison. Crush candidate/order evaluation uses stable ActorID ordering and caps before expensive checks. The cycle-reviewer found that raid target reachability/attack checks remain pre-cap. No matched five-AI comparison exists; no deterministic outcome claim is made.
- Deferred work: Move the Medium Tank raid's stable cheap shortlist and `MaximumTargetCandidates` cap before reachability/attack/defender work; complete literal composition/raid/crush/weapon matrix for both scoped AIs; deliberately trace reinforcement contention; matched exact-base control; blocked topology and five-AI throughput; save/load/replay recovery; natural winner evidence; separately rate-limit pre-existing unavailable MCV/Stealth-Harvester request logs and investigate extension/zero-harvester recovery.
- Known failures/risks: The raid-target configured cap does not bound all pre-cap work. Readiness did not open in the final endurance game. Cycle 20 reproduced contact/return but not a deliberate competing-order attempt. No final competitive improvement, natural conclusion, persistence, complete ownership contention, or stress-regression evidence. Final PR check refresh pending.
- Relevant artifact paths: `/root/github/LibertyDawn/AUTONOMOUS-CNC-LOGS/20260807-bug-polish-03/WORKER-1-CNC-45`; final review `final-pr-review/FINAL-REVIEW.md`; Cycle 20 run `cycle-20-review-ownership`; narrative `cycle-20-commentary/NARRATIVE.md`; policy `cycle-20-policy-review/POLICY-REVIEW.md`; cycle review `cycle-review-20/CYCLE-REVIEW.md`; report `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260807-bug-polish-03/WORKER-1-CNC-45/REPORT.md`
