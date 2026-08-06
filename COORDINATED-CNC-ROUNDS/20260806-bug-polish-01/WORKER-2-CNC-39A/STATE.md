# Worker State: CNC-39A

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `worker-2-cnc-39a`
- Task: `CNC-39A — Engineer/commando target coordination`
- Status: `Implementing`
- Common base branch/SHA: `agent/cnc38-early-viki-infantry-rush` / `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
- Task branch: `agent/round-20260806-cnc39a-engineer-commando`
- Intended PR base: `agent/cnc38-early-viki-infantry-rush`
- Worktree: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/workers/worker-2-cnc-39a`
- Worker state: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260806-bug-polish-01/WORKER-2-CNC-39A/STATE.md`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `10`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260806-bug-polish-01/WORKER-2-CNC-39A/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-2-cnc-39a`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `48`
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

The pinned control lets the ordinary CNC AI's Engineer capture selector and
Commando demolition selector reserve targets independently. Each selector avoids
duplicates within its own purpose, but neither sees the other. Both specialists
can therefore travel to and act on the same building, wasting scarce units and,
in the worst race, letting queued C4 destroy a building after an Engineer has made
it friendly. The generic C4 activity rechecks only `IDemolishable`, whose normal
building implementation checks only whether the trait is enabled, not current
ownership or relationship.

The player-visible change is that a bot makes one deterministic strategic choice
per building: a valid incumbent assignment keeps the objective; a truly
simultaneous unreserved tie goes to capture; the displaced Commando takes a
different eligible enemy building or waits only while useful capture progress is
actually being made. An intentional two-Engineer capture remains one compatible
capture assignment. An ordinary autonomous C4 assignment that becomes obsolete
because the target becomes friendly cancels before entry/planting and cannot
damage that friendly building at detonation. Deliberate force-target and authored
script demolitions retain their existing semantics.

## Authoritative behavior

Literal user requirements, preserved verbatim:

> Prevent engineers and commandos from entering or acting on the same building
> concurrently. Share deterministic target reservations between capture and
> demolition assignments, and revalidate ownership/relationship when a queued C4
> order executes so a building captured in the meantime can never be detonated
> after becoming friendly. Test both simultaneous selection and
> capture-during-commando-travel races.

The implementation contract is:

- Capture and demolition assignments owned by one bot consult a shared,
  deterministic target-reservation authority. A target may have a capture
  purpose or demolition purpose, never both concurrently.
- A capture reservation may name the exact two Engineers required by the healthy
  building policy. This intentional pair is compatible with itself but excludes
  all demolition assignments and unrelated capture assignments.
- Precedence is `valid progressing incumbent > capture in a genuinely
  simultaneous unreserved tie > deterministic alternate objective > bounded
  wait`. A later scan does not steal a valid incumbent merely because its purpose
  has a higher score. Capture-first is not an unconditional override.
- Invalid, completed, dead, removed, idle, interrupted, transported,
  relationship-invalid, or observably non-progressing incumbents release promptly
  enough for normal selection to recover. Existing transport reservations retain
  their current priority.
- Selection, reservation, retention, release, and alternate-target ordering are
  deterministic for the same map/content/seed/options/players/ActorIDs. Do not
  depend on hash/dictionary/world discovery order.
- A normal AI-issued C4 carries enough purpose/safety context through queued
  order, travel, plant, and detonation to revalidate the live target's current
  owner and relationship. If it is friendly at a harmful action, that autonomous
  assignment neither plants nor damages it. Relationship changes back to hostile
  later do not retroactively authorize an obsolete charge without a new valid
  assignment.
- Keep deliberate already-friendly force-target commands and authored scripted
  demolition outside that autonomous safety policy. Preserve ordinary manual C4
  against targets whose relationship remains allowed.
- Preserve CNC-39's ownership of the 80-percent lone-Engineer threshold,
  five-second/value-distance reassessment, valuable husks, healthy-building
  pairing, and pair retargeting. CNC-39A coordinates purposes; it does not retune
  capture desirability.

## Forbidden behavior and failure signals

- The same live building appears under both capture and demolition purpose, or
  receives both `CaptureActor` and autonomous `C4` orders concurrently.
- A Commando enters, plants, flashes, damages, or destroys a target that became
  friendly after its autonomous C4 assignment. A log-only cancellation without
  the friendly building surviving is a failure.
- A capture reservation prevents its required Engineer partner from joining, or
  permits extra Engineers to pile onto the same objective without policy need.
- A later scan steals a still-valid, progressing incumbent; capture always beats
  demolition rather than only resolving a true unreserved tie; or assignments
  oscillate as the 125/375-tick scans fire.
- A dead/idle/interrupted/transported specialist, invalid target, ownership
  change, missing partner, or unreachable/non-progressing route leaves a stale
  reservation across repeated ordinary reassessment opportunities while another
  specialist has useful work.
- The displaced Commando repeatedly remains idle despite a valid alternate enemy
  structure, or capture-first repeatedly loses both the capture and a demolition
  that the matched control completes under pressure without a task-specific
  explanation and correction.
- Equal candidates choose differently under identical ActorIDs/seed or after a
  harmless change in enumeration/insertion order.
- The fix changes scripted demolition, explicit force-target behavior, manual C4
  against still-valid enemy/neutral targets, Engineer thresholds/scoring, husk
  recovery, pairing, transport priority, production policy, or unrelated mods.
- Acceptance is claimed from assignment/request/reservation/order logs, a passive
  fixture, a reloaded game alone, or a scenario in which the intended actors,
  owner change, C4 window, and final outcome did not occur.
- New per-tick world scans, unbounded retries/allocations, cross-world static
  reservation state, noisy logs, swallowed errors, desyncs, or a repeatable
  material MAX throughput regression are failures.

## Relevant current implementation and control behavior

All facts below are from pinned SHA
`09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf` unless stated otherwise.

- `CaptureManagerBotModuleInfo` owns `CapturingActorTypes`, capture scan/scoring
  policy, `DemolitionActorTypes`, the demolition scan interval, visibility, and
  debug logging. CNC config in `mods/cnc/rules/ai.yaml` enables the module for all
  ordinary bot types with `e6`, `rmbo`, capture interval 125, demolition interval
  375, top 15 candidates, distance bias 10, retarget improvement 25, lone capture
  health 50 at this base, visibility disabled, and Enemy/Neutral capture targets.
- `CaptureManagerBotModule.cs` has independent
  `activeCapturers` and `activeDemolitionUnits` dictionaries (lines 106-109).
  `BotTick` invokes capture before demolition only when their separately
  randomized timers happen to expire on the same tick (lines 143-155).
- Capture builds a local `reserved` set solely from active capture assignments,
  retains intentional Engineer pairs, and selects in ActorID order using
  value/distance scoring (lines 182-365). It cannot see demolition targets.
- Demolition builds `activeTargetIds` solely from active demolition assignments,
  iterates idle Commandos by ActorID, filters current enemy buildings, takes the
  15 highest-value options, then chooses the nearest (lines 403-436). It cannot
  see capture targets. The two selectors therefore can deterministically disagree
  as a combined strategy even though each is internally deterministic.
- Each assignment dictionary is retired only when its own scan runs. Completion
  classification distinguishes removed, captured, damaged, lost, and idle.
  Capture and demolition yield actors when `TransportManagerBotModule` reserves
  them, but capture-manager assignments are not exposed through generic
  `IBotUnitReservations`. The module has no `IGameSaveTraitData`; inspect actual
  save/load reconstruction rather than assuming transient dictionaries survive.
- `CaptureActor` already revalidates live capture eligibility during travel,
  before entry, on entry completion, and at frame end before owner change. This is
  the useful model for state-transition safety, not proof that C4 is safe.
- `DemolitionOrderTargeter` checks `TargetRelationships`/force relationships only
  during target selection. `Demolition.ResolveOrder` checks only whether an
  actor target still has a valid `IDemolishable` before it queues `Demolish`.
  `Demolish.TryStartEnter` and its frame-end completion repeat only
  `IDemolishable.IsValidTarget`; normal `Demolishable.IsValidTarget` returns true
  whenever the trait is enabled. Its delayed action kills the target without a
  relationship recheck. This is the capture-during-travel and post-plant hole.
- `Demolish` is shared by issued orders and `DemolitionProperties` scripted
  actions; `IDemolishable` also has bridge implementations. Any shared signature
  or activity change must compile all shared consumers while CNC-only behavior is
  built and tested. Do not run or alter RA, D2K, or TS content.
- Existing focused coverage is
  `OpenRA.Test/OpenRA.Mods.Common/CaptureTargetingTest.cs`: economic value,
  deterministic within-capture choice, distance score, 50-percent pair boundary,
  and retarget margin. There is no pinned test for cross-purpose reservations,
  queued C4 relationship changes, or autonomous-versus-forced/script scope.
- Relevant history: `ff5b529a60` introduced specialist capture/demolition orders;
  `c01257b6d1` added per-purpose assignment dictionaries and within-purpose target
  follow-through; `fa97ee1932` added Engineer reassessment/pairing; and
  `81914a0978` added transport missions plus capture-manager yielding. Preserve
  those responsibility boundaries rather than reverting their behavior.

## Likely wrong approaches and challenges

- Unioning target ActorIDs into one exclusive set without a purpose/claimant
  model breaks the required two-Engineer capture pair.
- Filtering only new Commando choices after capture completes misses simultaneous
  selection, staggered scans, existing travel, same-frame ownership changes, and
  planted-charge detonation.
- Checking ownership only in `ResolveOrder` is too early because a queued activity
  executes later. Checking only immediately before planting still violates the
  reviewed literal safety boundary if ownership changes during the 45-tick charge
  delay.
- Globally making all demolition harmless to friendly targets silently removes
  explicit force-target and campaign/script semantics. Conversely, treating an
  AI safety marker as test-only state or failing to serialize it can make replay,
  networking, or save/load behavior diverge.
- Clearing/rebuilding all reservations on every scan discards paid travel and
  causes oscillation. Treating actor/target existence as sufficient validity
  creates ceremonial incumbents that never progress.
- Treating `IsIdle`, movement, or an emitted order as sole progress can misclassify
  queued activities, blocked routes, repeated failed entry, transport takeover,
  or a missing second Engineer. Define progress at the owning activity/assignment
  boundary and verify it in games.
- Iterating dictionary/hash/world actor order to resolve purpose ownership will
  produce nondeterminism. Keep an explicit precedence and stable ActorID tiebreaks.
- Adding another per-tick world scan or LINQ-heavy allocation path to a hot
  activity is unnecessary. Selection already runs at bounded intervals; execution
  safety can be O(1) per activity/action transition.
- Retuning capture health, value/distance, pair reassessment, or tactical threat
  policy to make the test pass overlaps CNC-39. The pressure game may inspire a
  later focused feasibility rule, but only if evidence shows the coordination
  rule itself repeatedly blunders and the change remains within CNC-39A.
- A focused map can accidentally use passive/custom actors, wrong ownership,
  disabled normal modules, instant target death, repaired health, or no actual C4
  window. Manifests/logs must prove setup and final outcomes.

## Competing systems and ownership

- `CaptureManagerBotModule` owns AI capture/demolition selection, incumbent
  bookkeeping, target conflict policy, and bounded assignment diagnostics. Shared
  cross-purpose reservation invariants belong in cohesive module code/helper;
  tunable scan/scoring policy remains in its YAML-owned info/config.
- `Demolition` order resolution, `Demolish` activity, and the relevant
  `IDemolishable` delayed action own execution-time relationship safety. The
  autonomous purpose must remain distinguishable through the final harmful
  action; force-target and script callers must retain deliberate behavior.
- `CaptureActor`/`CaptureManager` own capture eligibility, pairing entry, capture
  conditions, and owner change. Do not duplicate or weaken their live checks.
- `TransportManagerBotModule` and `InfantryAssaultTransportManager` may reserve,
  stop, board, carry, unload, and hand off `e6`/`rmbo`. VIKI and Iron Reaper can
  select these assault missions. Capture manager currently yields once it sees a
  transport reservation; force this competitor to act and verify prompt shared
  target release without stealing transport ownership.
- `SquadManagerBotModule` normally excludes `e6` and `rmbo` for every CNC bot.
  `CrateCollectorBotModule` also excludes them. Keep those exclusions and verify
  they do not become an accidental substitute for target coordination.
- The Commando has normal auto-target/attack behavior when not demolishing; enemy
  defenses and ordinary squads may attack specialists or the same target.
  Building repair/sell/destruction, another player capture, diplomacy/ownership
  change, target trait disablement, and target loss all invalidate or alter work.
- `UnitBuilderBotModule` produces Engineers and Commandos using shared infantry
  queues/cash; this task must not alter production requests or economy. Tests may
  pre-spawn specialists to force timing, but at least one ordinary long game must
  exercise normal production and all enabled modules.
- Reservations are per bot/player. Cross-player or allied-bot captures are not a
  mandate for a global reservation service; the execution-time C4 guard must make
  those unavoidable races safe. Record this scope as an assumption in the report.

## Cross-worker dependencies

- CNC-39 is concurrently assigned on branch
  `agent/round-20260806-cnc39-engineer-correction`, based at the same SHA. At spec
  time its worktree was still at `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
  with no product commit or open PR found. Monitor that exact branch and the later
  CNC-39 task PR before coding around overlapping lines and again before
  publication; inspect its commits, not its worker state/spec.
- CNC-39 owns the 80-percent lone-Engineer threshold and five-second/value-distance
  reassessment, valuable husks, coordinated healthy-building captures, and pair
  retargeting. CNC-39A must preserve those changes and focus its diff on shared
  capture/demolition reservation semantics plus autonomous C4 owner/relationship
  revalidation. Integrate/test against CNC-39's task commits when available and
  report any unresolved textual or behavioral conflict.
- CNC-50 is a later related stall-recovery concern, not an active prerequisite in
  this packet. Record genuine late-game recovery findings as deferred work; do
  not implement CNC-50 here.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-2-cnc-39a/spec-policy/inputs/NARRATIVE.md`
- Sol-high policy review: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-2-cnc-39a/spec-policy/POLICY-REVIEW.md`
- Verdict and confidence: `mostly sensible; high confidence`
- Recommendations adopted as testable hypotheses: `valid incumbent > simultaneous capture tie > alternate/wait; progress-based invalidation; normal autonomous C4 safety through detonation; deterministic useful Commando follow-up; defended no-alternate pressure test; pre-plant, post-plant, interruption, relationship-flip, and equal-candidate tests`
- Recommendations rejected or deferred, with reason: `Rejected a global all-C4 friendly-damage ban because it would change deliberate force-target/script semantics. Rejected capture-always-wins because it would steal paid incumbent travel. Deferred a new tactical-feasibility/threat policy unless repeated matched pressure games prove capture-first loses both objectives; speculative threat tuning overlaps adjacent Engineer policy and exceeds the smallest fix.`

## Implementation and publication plan

1. Establish pinned-base focused/unit and matched full-engine control evidence,
   create the task branch/worktree exactly as assigned, and inspect available
   CNC-39 commits before touching overlapping code.
2. Implement the smallest cohesive shared-purpose reservation/lifecycle boundary
   in capture-manager ownership while preserving Engineer pairs, stable ordering,
   incumbent retention, transport priority, and CNC-39 scoring/reassessment.
3. Carry narrowly scoped autonomous-demolition safety to each harmful execution
   boundary in shared code without changing forced/scripted behavior. Add bounded
   transition diagnostics and handled invalid-target/relationship cancellation.
4. On cycle 1, make the first behavioral evidence a matched changed-versus-base
   full-engine ordinary-AI simultaneous-selection pair at headless MAX. Use focused
   checks as supplementary gates, then climb immediately to travel, detonation,
   pressure, contention, save/load, determinism, and natural-game scenarios.
5. Remove noisy temporary diagnostics, run CNC checks/unit suite and final literal
   regression, measure determinism/MAX cost, write the task report, inspect/resolve
   CNC-39 overlap, commit/push the task branch, open one PR to the intended base,
   and wait for required checks. Do not merge.

## Acceptance and tests

### Literal black-box acceptance

Use fresh focused CNC maps under the ignored analysis directory, with the full
engine, real ordinary bot types, and all relevant normal modules active. Test-only
rules may pre-spawn actors, shorten both selector intervals to one tick, arrange
distances, set target health, and add concise outcome logging; they may not replace
the ordinary bot or capture/demolition managers.

1. **Simultaneous selection:** one bot owns at least the exact Engineer count
   required by current CNC-39 policy and one idle Commando. Put one high-value
   enemy building where both selectors rank it first and at least one eligible
   alternate enemy building for the Commando. Align both normal scans. Prove the
   target IDs and same decision tick. Final outcome: the Engineer assignment owns
   and captures the shared first choice; the Commando never receives/enters/plants
   against it and deterministically demolishes the alternate. The captured
   building remains alive and friendly through the scenario.
2. **Capture during Commando travel:** make an ordinary AI issue C4 against a
   distant enemy building, then let an ordinary allied bot's closer Engineer(s)
   capture it while the Commando is visibly travelling. This intentionally crosses
   the per-player reservation scope. Prove order tick, movement, owner-change tick,
   current alliance, plant/detonation window, and final HP/owner. Final outcome:
   the Commando cancels before entry if capture wins before planting; if ownership
   flips after planting, the autonomous charge causes no friendly damage at its
   final harmful action. The building is alive, friendly, and not flashing from an
   active obsolete charge at the judged end; the Commando becomes available for
   ordinary follow-up.

Both scenarios must show actual requests, reservations, competing claims,
movement/state transitions, order/activity execution, and final player-visible
outcomes. A reservation log or absence of an explosion without a real ownership
race is not acceptance.

### Focused checks and instrumentation

- Add focused unit/interface tests for: compatible Engineer-pair claims; capture
  versus demolition exclusion in both arrival orders; valid-incumbent retention;
  true-tie capture precedence; deterministic alternate selection/tiebreaks;
  release on every invalidation class; and autonomous relationship safety before
  plant and at final damage while forced/script scope remains allowed. Include a
  genuinely queued C4 order behind a preceding activity so execution-time safety
  is not inferred only from the AI's normal queue-replacing order.
- Run the relevant fixture immediately with
  `dotnet test OpenRA.Test/OpenRA.Test.csproj --configuration Debug --nologo -p:TargetPlatform=linux-x64 --filter FullyQualifiedName~CaptureTargetingTest`
  plus any new focused fixture filter. Before publication run the full command
  without `--filter`, `make check`, `make check-scripts`, and `make test`, reserving
  the large-build slot where appropriate. Build/test CNC only.
- Gate temporary diagnostics behind the existing owning debug switch or a
  similarly bounded CNC test seam. For each decision/state transition record at
  most once: world tick, bot/player, purpose, specialist type/ActorID, target
  type/ActorID, claimants, reservation purpose/owner, incumbent progress state,
  candidate rejection reason, queued order, transport takeover/release, original
  and current owner/relationship, C4 plant/cancel/disarm/final action, and final
  target HP/owner. This must distinguish request, rejection, reservation owner,
  competing consumer, state transition, order, and final outcome.
- Invalid/dead/out-of-world targets and relationship changes are expected handled
  cancellations, not exceptions. Unexpected duplicate claims, impossible purpose
  transitions, or lost autonomous safety context should emit one actionable
  bounded warning in debug/test mode and fail focused checks; never silently
  substitute success. Remove noisy probe logging and leave production defaults
  false before publication.
- Prove each launch loaded the intended map copy/checksum, CNC content/commit,
  seed, factions, starts, teams/relationships, starting cash/options, bot types,
  exact `e6`/`rmbo`/building ActorIDs, normal modules, headless and MAX markers,
  advancing world ticks, replay/benchmark artifacts, scenario trigger, and final
  owner/HP/outcome.

### Ordinary and differential games

Every entry below is adversarial: record its failure hypothesis, perturbation,
failure signal, and pass evidence before launch.

- **Cycle-1 matched simultaneous pair (first behavioral test):** Hypothesis: the
  shared view still allows both purposes to claim the top building or breaks the
  Engineer pair. Perturbation: both scan intervals one tick, one forced top choice,
  one equal-use alternate, pre-spawned ordinary specialists. Failure: duplicate
  target, wrong pair size, nondeterministic owner, or no final capture/alternate
  demolition. Pass: changed build has disjoint purposes and both useful outcomes;
  pinned-base control with identical map/seed/options demonstrably overlaps. Run
  as the first changed behavior evidence even if compile/unit gates ran first.
- **Matched long-travel relationship pair:** Hypothesis: relationship is checked
  only at selection/plant or autonomous context is lost. Perturbation: a second
  allied real bot captures immediately before entry in one run and during the
  45-tick charge delay in another. Failure: friendly entry/plant/damage/death or
  indefinite Commando stall. Pass: changed build preserves the friendly target
  and recovers the Commando; control exposes the old damage when the setup reaches
  the race. Keep initial state, factions, teams, seed, timing, and target HP matched.
- **Defended no-alternate pressure comparison:** Hypothesis: capture-first is a
  worse survival blunder when two Engineers must cross defense and a Commando
  could deny the only target. Perturbation: high-value frontline building,
  required pair, real defenders/repair, no alternate C4 target, repeated matched
  seeds. Failure: changed AI repeatedly loses both Engineers and leaves the enemy
  building operational while control demolition reliably succeeds. Pass: capture
  succeeds/materially pays off, or bounded invalidation releases the target and a
  later demolition achieves denial without duplicate travel. Investigate repeated
  changed losses; do not rationalize them from feature-fire logs.
- **Contention/recovery game:** Hypothesis: target/unit claims survive interruption
  or transport ownership. Perturbation: kill one pair member, destroy/disable or
  change owner of targets, and make VIKI or Iron Reaper's normal infantry assault
  transport reserve an `e6`/`rmbo`. Failure: other purpose remains blocked across
  two ordinary reassessment opportunities, transport order is overwritten, or
  target is double-claimed. Pass: transport retains its unit, stale target claims
  release, and remaining specialists take deterministic useful work. Exercise the
  ordinary connected case and an island/blocked topology such as Archipelago so
  transport/path pressure cannot hide a stale claim.
- **Save/load differential:** Hypothesis: transient assignment or autonomous C4
  safety context is lost across persistence. Perturbation: save during travel and,
  separately, after plant before owner change/detonation; reload and finish. Failure:
  duplicate claims, friendly damage, or permanent wait after reload. Pass: behavior
  and final outcome match the fresh changed run. This is supplementary and cannot
  be sole acceptance or final evidence.
- **Natural ordinary match:** Hypothesis: focused setup hides production/module or
  long-duration regressions. Perturbation: connected conquest map, ordinary
  production/economy/repair/squads/transports, fastest headless MAX, real opponents,
  and enough cash/tech/duration for both specialists. Failure: no exercised event,
  recurrent duplicate targets, new specialist stall, desync/crash, or material
  regression. Pass: at least one real coordination/C4 invalidation event reaches a
  correct final outcome and the match reaches natural game over. Change seed/setup
  if the event does not occur.

Use `launch-ai-parallel.py` with isolated manifests/support directories and the
global game lock. Use both slots for matched control/changed pairs when practical;
otherwise run serially if contention distorts timing. Store manifests, summaries,
replays, benchmarks, and concise conclusions below the assigned analysis path.

### Old-behavior control and required improvement

- Control is the exact pinned SHA
  `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf` in an isolated read-only/control
  worktree or build. Prefer a same-build feature-disabled control only if the
  implementation adds a production-valid toggle; do not add a test-only product
  toggle solely for comparison.
- For every pair record commit/build identity, CNC content/config checksums, map
  checksum, seed, bots/factions/teams/slots, starts, options, starting actors/HP,
  selector timing, target IDs, world ticks, and benchmark environment. The
  intended behavior must be the meaningful difference.
- Primary success metrics: cross-purpose target collisions `0`; normal autonomous
  C4 damage/deaths against newly friendly targets `0`; intentional Engineer pairs
  preserved `100%`; eligible alternate Commando assignment and useful demolition
  achieved within two normal demolition opportunities; invalid incumbent release
  and useful reselection within two relevant normal selection opportunities; and
  deterministic target/purpose IDs across repeated identical runs.
- Compared with control, changed behavior must eliminate every exercised collision
  and friendly demolition, preserve at least as many useful capture/demolition
  objective outcomes when alternates exist, and decisively preserve the captured
  asset in travel/detonation races. Repeated parity from a Commando that merely
  waits, a marginal log-only difference, or a pressure loss is evidence to
  investigate and correct or explain with concrete task-specific outcomes.
- For the no-alternate pressure case, run enough matched seeds to distinguish a
  rule failure from combat noise. Repeated control demolition success versus
  changed loss of both capture and denial is unacceptable without revising the
  coordination feasibility/invalidation policy.
- The hot path must add no extra per-tick full-world scan and keep reservation
  operations bounded by existing candidate/assignment counts. Compare long-run
  benchmark tick/frame cost and allocations where available across at least three
  matched seeds; investigate any repeatable median MAX throughput regression over
  5%, unbounded growth, or new GC/log spikes. Document the credible measurement
  even when noise prevents a precise percentage claim.

### Adversarial cases

After the latest relevant fix and after normal acceptance first passes, complete
at least three distinct clean full-engine ordinary-AI scenarios. A later fix that
can affect them resets the clean count.

1. **Frontline capture-first under fire/no alternate.** Failure hypothesis:
   capture-first blocks viable denial then loses the pair. Stress healthy target,
   pair separation/death, repair/defenders, route exposure, and missing alternate.
   Failure signal: no capture and no bounded demolition recovery while control
   denies it. Player-visible pass: captured building survives usefully or the
   invalid capture releases and is demolished without concurrent purposes.
2. **Ownership timing ladder.** Failure hypothesis: one execution boundary lacks
   safety. Stress capture before entry, same/adjacent frame as entry, after plant
   before detonation, a genuinely queued C4 behind another activity, and a
   subsequent relationship flip. Failure signal: any normal autonomous C4 harm
   while friendly or force/script compatibility loss.
   Player-visible pass: friendly building survives every autonomous timing case,
   while a separately staged deliberate CNC scripted/force case remains
   observably unchanged.
3. **Contention and missing assets.** Failure hypothesis: reservations outlive
   actor/target purpose. Stress transported specialist, one missing/killed pair
   member, interrupted/idle Commando, target destroyed/disabled/captured, multiple
   alternates, and ordinary competing managers. Failure signal: overwrite,
   duplicate target, or idle across two scans. Player-visible pass: owners retain
   their actors, claims release, and surviving specialists complete alternate work.
4. **Determinism/persistence ladder.** Failure hypothesis: enumeration or reload
   changes purpose ownership. Stress equal-value/equal-distance targets with fixed
   ActorIDs but varied declaration/discovery order, then save/load mid-travel.
   Failure signal: different chosen IDs/purpose, duplicate post-load order, or
   friendly damage. Player-visible pass: identical deterministic choices and fresh
   versus reload outcome. The fresh run, not reload, counts as acceptance.

### Final regression

From a fresh start after all cleanup, rerun a literal combined full-engine map at
headless MAX with ordinary AI and every relevant normal module: align a same-bot
Engineer pair and Commando on a top target plus a deterministic alternate, while a
second allied ordinary Engineer creates a long-travel/post-plant relationship
race on another target. Add real defenders, target repair, and one transport
reservation without invalidating the required events. Prove all setup markers and
ticks. Pass only if the same-bot purposes never overlap, the pair captures its
target, the displaced Commando completes the alternate, every newly friendly race
target survives all autonomous C4 windows with unchanged owner/HP, interrupted or
transported assignments release, no specialist remains stale, benchmark/replay
artifacts flush, and no fatal/desync/warning regression occurs. This must be a
fresh run, not a reload, and should continue to natural game conclusion when the
map supports it.

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
attempted policies, evidence for/against each, and focused questions. The
escalated reviewer still reads only the design document and narrative. Record use
in the assignment field. Never invoke it before test 10 or invoke it twice for one
task.

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
blocked topology such as Archipelago. For persisted behavior, include save/load
and reject a reloaded state as sole acceptance. For hot paths, define a bounded
CPU/allocation expectation and measurement or credible regression signal.

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
| 1 | Shared purpose claims; autonomous-only serialized C4 safety marker and live relationship checks | Same-tick scans may still double-claim; two Engineers plus Commando, shared `weap#157`, alternate `nuke#158`, seed 39001 | Focused 7/7; changed+base full-engine runs reached tick 1000 but summaries invalid because the scenario display marker was not logged | Factual narrative: `cycle-1/commenter/NARRATIVE.md`; confirmed identical contention and no completed outcome. Policy: `cycle-1/policy-review/POLICY-REVIEW.md`; no design objection, requested observable completed outcomes | Failed: changed capture claims were retired as idle during the same tick before queued orders resolved, so both changed and base also assigned C4 to `weap#157`; no fatal/desync. Evidence under `analysis/worker-2-cnc-39a/cycle-1/`, map SHA-256 `9ebfb87a69aab831011c9ef5855019d84ed6b4651adfb56c10e340362ece964d` | Preserve new claims through order resolution, then rerun the matched simultaneous pair with log-visible setup/outcome evidence |
| 2 | Refresh shared lifecycle once per bot tick; retain newly queued assignments for a bounded 10-tick order-resolution grace | One-tick aligned scans may still discard pending claims or fail deterministic alternate selection; exact cycle-1 setup and seed | Focused 7/7; changed+base full-engine pair passed harness to tick 1000 | Corrected factual narrative `cycle-2/commenter/NARRATIVE.md`: changed split objectives while control repeatedly overlapped. Policy `cycle-2/policy-review/POLICY-REVIEW.md`: conditionally acceptable; require completed outcomes and sensible post-resolution reassignment | Selection pass: changed assigned pair to `weap#157` and Commando to `nuke#158` once at tick 1; control repeatedly overlapped capture+C4 on `weap#157`. No completed objective outcome, so not acceptance. Evidence `analysis/worker-2-cnc-39a/cycle-2/` | Correct focused-map geometry/observability and require completed capture plus alternate demolition on the next product cycle |
| 3 | Added bounded autonomous-C4 accept/cancel/plant/disarm/final diagnostics; moved scenario actors to known spawn clearings and disabled starting-unit placement | Split assignments may not persist or complete; same one-tick scan stress with connected geometry and completed outcomes required | Focused 7/7; changed+base pair passed to tick 1000 | Narrative `cycle-3/commenter/NARRATIVE.md`: changed produced a clean forward capture/demolition sequence with comparable one-run runtime. Policy `cycle-3/policy-review/POLICY-REVIEW.md`: aligned with Skynet specialist policy; multi-seed/longer confidence testing remains | Player-visible simultaneous pass: changed captured `weap#157` by tick 103, never assigned C4 to it, destroyed alternate `nuke#158` by tick 540, then selected `fact#160`. Control repeatedly overlapped both purposes on `weap#157`, then worked `nuke#158` later. New transition messages were not present in debug.log. Map SHA-256 `e469891b096c4d9804c2559d3c351a3c8694f8392adde52da7e88a98950d3414`; evidence `analysis/worker-2-cnc-39a/cycle-3/` | Preserve this literal pass; make execution transitions log-visible and test capture-during-Commando-travel ownership races next |
| 4 | Routed bounded autonomous-C4 transitions into debug.log; staged cross-player allied capture race | Marker/context may be lost during travel; distant Commando versus allied near Engineer pair, three ordinary bots | Focused 7/7; changed+base reached tick 1400 but harness failed required Lua patterns | Narrative `cycle-4/commenter/NARRATIVE.md`: traces showed changed autonomous C4 acceptance, allied capture, and later alternate recovery, but no scenario markers. Policy `cycle-4/policy-review/POLICY-REVIEW.md`: conditionally aligned; duplicate expenditure and final ownership/damage remain unproven | Invalid setup for literal outcome: map omitted `World/LuaScript`, so no Lua setup/capture/final markers. Changed logged autonomous accept at tick 6 and allied capture by tick 102 with later Commando recovery; one-tick rescans churned control and did not expose old friendly damage. Map SHA-256 `ef8b32ac65ae706162945a84931e29ca62e71b1a5a57f413ee8b460749397d76`; evidence `analysis/worker-2-cnc-39a/cycle-4/` | Register LuaScript and use ordinary reassessment timing; require explicit post-plant allied capture, changed disarm/survival, and control destruction |
| 5 | Reused one latched autonomous-demolition safety context from accepted order through travel, plant, and delayed action; added relationship-latch tests | Allied capture may occur after planting yet before the delayed charge fires; ordinary allied bot pair, autonomous Commando, delayed charge | Focused 9/9; CNC release build passed with 0 warnings; four changed+base pairs reached tick 1400 but missed required capture marker | Reviews: `cycle-5/`, `cycle-5b/`, `cycle-5c/`, and `cycle-5d/` commenter/policy outputs. Fourth commenter counted 13 accepts/12 plants and no capture. Fourth policy rejected redundant planting and recommended a pending-demolition lock; its cross-player capture-reservation proposal is out of scope because reservations are deliberately per bot | Fourth fixture isolated reassessment and extended charge: changed planted at tick 90, but the manager released the now-idle Commando and replanted 11 more times, starving the Engineer entrance; target stayed enemy and died at tick 691 (control 679). This exposes a pending-charge lifecycle defect, not safety success. Evidence `cycle-5/changed-run-4`, `control-run-4` | Treat a pending autonomous charge as a progressing incumbent, preventing duplicate plants until detonation/disarm, then rerun exact fixture |
| 6 | Retain a demolition incumbent while the target reports a pending autonomous charge from that specialist | A planted charge may be misclassified as idle progress, causing duplicate plants and prevent ownership-race evidence | Focused 9/9; CNC release build passed with 0 warnings; three changed+base pairs reached tick 1400; final pair passed both harnesses | Reviews `cycle-6/`, `cycle-6b/`, `cycle-6c/`. Decisive commenter confirmed matched setup and changed disarm/survival versus control friendly destruction. Policy found the outcome correct and materially aligned; recommended hostile-recapture finality | Safety pass: changed planted once at tick 75, ordinary allied Engineer captured `weap#157` at 438, charge disarmed at 439 on `Ally`, and friendly factory survived tick 751. Pinned control captured at 153 but its old charge destroyed the now-`Multi2` friendly factory at 688. Map target 49% health, one Engineer, Dispose entry, 600-tick charge; both harnesses passed. Evidence `cycle-5/changed-run-7`, `control-run-7` | Preserve decisive differential; add bounded observable-progress retirement, then test blocked/unreachable recovery and pre-plant travel cancellation |
| 7 | Track specialist movement/target-health progress; release after two reassessment windows (minimum 250 ticks) and defer the stalled target for one bounded retry window | An unreachable target may retain a ceremonial activity/reservation forever or be immediately reselected; wall-enclosed primary plus reachable alternate | Focused 9/9; CNC release build passed with 0 warnings; changed failed alternate marker, control passed expected retention, both tick 1400 | Narrative `cycle-7/commenter/NARRATIVE.md`: bounded release occurred but recovery did not complete. Policy `cycle-7/policy-review/POLICY-REVIEW.md`: release aligned; require cleared activity, primary cooldown, bounded alternate selection/C4/destruction | Partial pass: changed released blocked `weap#155` as `non-progressing` at tick 327; control retained it. Changed did not choose alternate because the retired Commando's old `Demolish` activity remained non-idle, so final tick 1001 had both targets alive. Evidence `cycle-7/changed-run`, `control-run` | Next isolated change queues Stop on non-progressing retirement, then rerun exact map and require alternate demolition |
| 8 | Queue a normal Stop order when non-progressing retirement clears the reservation and defers its target | A released specialist may remain trapped in its old activity and never become eligible for alternate selection | Focused 9/9; CNC release build passed with 0 warnings; blocked and post-plant pairs passed; travel changed harness missed cancel marker while control passed | Reviews: `cycle-8/` aligned recovery; `cycle-8b/` strong disarm; `cycle-8c/` travel outcome correct but acceptance incomplete. Policy requires explicit cancel plus preserved ally and bounded follow-up | Travel functional pass: changed accepted C4 at 21, allied capture 96, manager release `relationship-invalid` at 115, no plant/destruction, friendly factory alive 1001. Control also survived. Changed harness failed only because hidden-target path skipped explicit autonomous cancel diagnostic. Evidence `cycle-8/changed-run`, `control-run` | Next isolated change revalidates actor relationship even when hidden, then rerun exact map requiring cancel/no plant/survival |
| 9 | Revalidate autonomous-C4 actor ownership during travel even when Enter marks the actor hidden | Ownership change may cancel generically before the safety latch emits its required transition evidence | Focused 9/9; CNC release build passed with 0 warnings; changed+base reached tick 1400 | Review carried into cycle 10 after the evidence-driven correction | Failed transition evidence: changed accepted C4 at tick 21 and the allied bot captured by tick 102, then the manager released `relationship-invalid` at 109 with no plant/destruction and friendly survival, but the activity still emitted no cancel because recalculation had replaced the hidden actor target. Evidence `cycle-8/changed-run-2`, `control-run-2` | Retain the originally ordered live actor in the autonomous activity so hidden-target travel can latch and report the owner/relationship transition |
| 10 | Retain the originally ordered actor for autonomous travel safety revalidation after target visibility/recalculation changes | `Enter` may replace a hidden actor target before the autonomous safety boundary can inspect its live owner, suppressing cancellation evidence or missing a same-path relationship change | Focused 9/9; full unit 443/443; `make all`, `make check`, `make check-scripts`, and `make test` passed; full-engine counter 48 | Fresh factual/policy reviews under `cycle-10/` accepted target-specific travel cancellation, repeated ownership ladder, scripted-scope compatibility, both persistence boundaries, and the passed combined regression; invalid harness runs are explicitly excluded | Travel canceled on Ally and preserved `weap#157`; natural ladder canceled two friendly targets then destroyed only hostile `nuke#160`; fresh/reload post-plant runs disarmed identically; combined run captured `weap#158`, destroyed alternate `nuke#159`, canceled on captured `fact#161`, and completed hostile follow-up. No fatal/desync. See `REPORT.md` and `analysis/worker-2-cnc-39a/cycle-10/` | Publish as First iteration - testing: literal/core combined behavior passes, but defended natural-production and exercised ordinary transport takeover remain packet-defined completion gaps |

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
