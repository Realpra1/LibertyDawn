# Worker State: CNC-50

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `WORKER-4`
- Task: `CNC-50 — Late-game engineer stall recovery`
- Change category: `AI capture-assignment recovery bug fix and bounded diagnostics`
- Balance authority: `Frozen. Do not change costs, HP, damage, armor, speed,
  production/demand, capture values or scores, the 80% solo-building threshold,
  engineer pairing, the 25% retarget margin, scan timing, prerequisites, or any
  other balance/policy value to make recovery evidence pass.`
- Status: `Specified`
- Common base branch/SHA: `agent/cnc-20260807-bug-polish-02-release` / `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`
- Task branch: `agent/round-20260807-cnc50-engineer-stall-recovery`
- Intended PR base: `agent/cnc-20260807-bug-polish-02-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `0`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260807-bug-polish-03/WORKER-4-CNC-50/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/analysis/worker-4-cnc50`
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

The literal request is to investigate engineers that stay idle despite nearby
capturable vehicle husks or buildings for every CNC AI type, especially after a
prior target or its owner dies late in the game. Revalidate assignments
periodically and whenever an engineer has no valid order, release stale target
reservations, rank newly available local husks/buildings through the normal rules,
recover instead of standing stopped until death, and add bounded explanations for
candidate exclusion. Preserve CNC-39's engineer value/solo/pair reassessment and
CNC-39A's shared capture/demolition reservation behavior; do not absorb CNC-59's
neutral-building demand or specialist-transport work.

The packet names manual evidence at
`AUTONOMOUS-CNC-LOGS/manual-post-cnc25-20260803-213129/`, including repeated
late-game `no eligible solo target` stops. That directory was absent both from
this checkout and a bounded search under `/root/github` at specification time.
Do not manufacture or overwrite it. If it becomes available, preserve it outside
Git, record checksums and exact cited lines in the analysis/report, and use it as
symptom provenance. Its absence does not justify guessing: first reconstruct the
reported transition in the full engine against the recorded base control.

Predicted observable change: after valid capture work is invalidated by target
death/removal, owner/relationship change, or loss of a valid capture activity, an
otherwise unowned engineer releases only the obsolete assignment/claim and gets
useful ordinary capture work within the bounded latency below. The engineer then
reaches and captures/recovers the normally preferred available target. Healthy
assignments, transport-owned engineers, legitimate pair work, and engineers with
no eligible target remain stable.

## Authoritative behavior

- All configured bot types (`cabal`, `watson`, `hal9001`, `brutalis`, `viki`,
  `skynet`, `ironreaper`, `wavemaker`, `Easy`, and `Easiest`) receive the same
  recovery semantics through the shared capture manager; no personality fork.
- Validate assignment target, relationship, capture eligibility/reachability,
  expected activity, progress, live reservation purpose/target/claimants, and
  explicit competing-module ownership as one coherent state.
- A hard-invalid target (dead, removed, captured/relationship-invalid) or a
  reservation that no longer matches the assignment must release the obsolete
  assignment and only its matching claim by the next bot tick. It must not wait
  for a stall timeout.
- A live, eligible, reachable incumbent with a matching claim may survive a brief
  missing-activity transition for at most the existing 10-tick pending-order
  grace. If valid capture activity is not restored, release it. This grace is an
  implementation safety bound, not permission to change scan timing or balance.
- An idle/unowned engineer with an ordinarily eligible target must re-enter normal
  planning and receive the useful `CaptureActor` order no later than one existing
  `MinimumCaptureDelay` window (125 ticks) after invalidation/grace expiry. Where
  safe, same-evaluation recovery is preferable, but acceptance is the 125-tick
  bound and final player-visible capture progress/outcome.
- Recovered engineers use the unchanged economic value, distance bias,
  reachability, pair/solo threshold, retarget margin, deferral, deterministic tie,
  and capture-versus-demolition reservation rules. “Nearby” never overrides them.
- Legitimate healthy idleness is allowed when every candidate is excluded. At a
  transition or bounded audit, diagnostics identify the engineer activity and
  assignment/claim/owner state plus the first decisive reason for the highest
  normally ranked excluded candidate.
- Keep audits bounded: active-specialist state may be checked cheaply each tick,
  but no new per-tick world-actor/candidate scan. Full candidate ranking remains on
  the existing planning cadence or a narrowly triggered equivalent with bounded
  work and deterministic order.

## Forbidden behavior and failure signals

- An unowned idle engineer remains without a valid capture order more than 125
  ticks after an eligible replacement exists, or dies while a proven eligible
  local target remains ignored.
- Releasing a valid progressing assignment because of a momentary activity gap;
  repeated Stop/reclaim/retarget oscillation; resetting progress on every audit.
- Stealing an engineer from a live transport mission or another explicit unit
  owner; allowing capture and demolition to claim the same target; exceeding one
  solo or exactly two paired capture claimants; leaking a dead specialist's claim.
- Bypassing capture eligibility, relationship, reachability, deferral, pairing,
  visibility policy, target-value ranking, or the strict retarget-improvement rule
  merely to eliminate idle time.
- Changing engineer production/demand, neutral-building policy, APC/helicopter
  transport behavior, commando demolition policy, or any frozen balance surface.
- Duplicating/replacing CNC-39/CNC-39A state with a second reservation system,
  nearest-target special case, unbounded retry queue, exact optimizer, or new
  personality-specific policy.
- Per-tick whole-world scans/allocations, nondeterministic collection iteration,
  repeated identical rejection logs, log-only acceptance, or retaining noisy
  temporary instrumentation.
- Save/load losing, reviving, or multiplying assignments/claims; replay desync;
  treating a reloaded state or manager-only/passive fixture as sole acceptance.

## Relevant current implementation and control behavior

- At the exact base SHA,
  `OpenRA.Mods.Common/Traits/BotModules/CaptureManagerBotModule.cs` owns capture
  and demolition planning. `mods/cnc/rules/ai.yaml` enables its one shared instance
  for all ten bots with engineers `e6`, commandos `rmbo`, capture delay 125,
  demolition delay 375, 15 candidate options, distance bias 10, retarget margin
  25%, solo-building threshold 80%, Enemy/Neutral relationships, and debug off.
  Those rules are control facts, not tuning authority.
- Every bot tick currently retires only specialists that are no longer orderable.
  When either planning timer expires, `RefreshAssignments` retires target/
  relationship/activity/stall failures and transport-owned specialists, then the
  relevant queue method scans/ranks candidates. Capture retirement releases the
  specialist's shared target claim; a non-progressing target is temporarily
  deferred and the engineer is stopped.
- `QueueCaptureOrders` considers an engineer only while idle or already recorded
  active and not transport-reserved. It globally filters normal candidates, keeps
  the top 15 plus incumbents, reassesses pairs/solos, and either retains, retargets,
  or stops/releases an active engineer with no eligible result. Empty candidate
  sets return early, and current bounded rejection logging explains only a
  higher-scored candidate inside the already materialized candidate set. These
  early exits and lifecycle transitions must be inspected against the symptom;
  do not assume they are the root cause without base-game evidence.
- `ShouldRetireAssignment` rejects unavailable specialists and invalid target
  relationships immediately when called, then after a 10-tick pending grace
  observes movement/target-health progress, uses a 250-tick minimum stall bound,
  and retires idle or unexpected activity. This validation normally runs only on
  a capture/demolition planning scan; target death and mismatched live reservation
  are not a separately evidenced event-triggered recovery path.
- `SpecialistTargetReservations` in
  `BotModuleLogic/CaptureTargeting.cs` deterministically maps one specialist to
  one target, permits at most the requested capture pair, excludes the other
  purpose, releases by specialist, and validates/restores saved claims.
- CNC-39 is already integrated through merge `b456fd89fac88d71dfadd65c47cfb7b409d44122`
  (task commits `53874e4328` and `0e9efa901a`): exact 80% HP-ratio handling,
  reachability, value/distance ranking, pair/solo reassessment, stall deferral, and
  diagnostics. CNC-39A is integrated through `402c808aeb7bc8c2cddefe25af305a0c261a8fd3`
  (notably `0c6accf17a` and save repair `f3fbbb4da4`): deterministic shared
  capture/demolition reservations, relationship-safe demolition, and assignment/
  deferred-target save restoration. Both merge commits are ancestors of the base.
- `CaptureTargetingTest.cs` covers value, distance, exact pair threshold,
  retarget margin, deterministic allocations, reservation cardinality/release,
  and restoration, but does not itself drive a real manager through target death,
  idle transition, another module's ownership, or completed capture.

## Likely wrong approaches and challenges

- Lowering `MinimumCaptureDelay`, the 80% threshold, deferral/stall duration,
  retarget margin, target scores, or production weights can hide the bug while
  changing strategy. They are frozen.
- Treating `IsIdle` alone as proof of staleness can cancel a pending valid order or
  race the bot order queue. Conversely, treating the stored assignment alone as
  truth can preserve a phantom order. Validate target, claim, activity, progress,
  and external owner together with the narrow grace.
- Clearing every claim or assignment on any target/owner death breaks unrelated
  engineers/commandos and valid pairs. Release by exact specialist and matching
  purpose/target; test partial pair loss and simultaneous claimants.
- A new nearest-husk fast path would bypass the existing economic ranking and
  CNC-39 rules. Recovery must feed the existing planner rather than override it.
- Adding capture ownership to transport by ad-hoc type checks, or issuing Stop to
  a transport-owned engineer, can corrupt boarding missions. Use the existing
  reservation/ownership seams and test VIKI/IronReaper assault transport.
- Logging only candidates that survive the global top-N can make an empty or
  excluded set opaque. Improve state-transition evidence without changing what
  the AI can see/target and without logging all actors every tick.
- A unit test, script-created passive bot, issued-order line, reservation release,
  engineer movement, or reload-only success is not the requested outcome. The
  ordinary AI must capture/recover the target in the full engine.
- One personality, one happy seed, or a short setup that ends before the recovery
  bound cannot establish the “every AI type” contract. Use the matrix and negative
  controls below.
- The missing historical directory is not evidence the symptom vanished. Nor may
  an unreproducible base failure justify speculative machinery: broaden the
  adversarial invalidations and preserve a no-code diagnosis if current base
  behavior already meets every observable contract.

## Competing systems and ownership

- **Capture manager:** owns `e6` candidate scans, active assignments, deferrals,
  pair/solo order issuance, and target claims. Recovery and durable diagnostics
  belong here or in a small cohesive capture-lifecycle helper. Do not put the
  invariant in one personality's YAML.
- **Shared specialist reservations:** `SpecialistTargetReservations` is the sole
  target-claim authority shared with `rmbo` demolition. Any lifecycle enhancement
  must preserve deterministic one-specialist/one-target mapping, one solo/two pair
  cardinality, purpose exclusion, and save restoration.
- **Transport manager:** every bot has the module; infantry assault use of `e6`
  is configured for VIKI and IronReaper. Its coordinator reserves carriers and
  pending passengers through `IBotTransportReservations`/
  `IBotUnitReservations`, issues boarding/travel/unload orders, stops passengers
  on some mission releases, and owns them until release. Capture currently checks
  transport reservations and drops capture state rather than overwriting them.
- **Squad/crate/other tactical managers:** CNC squad configs exclude `e6` and
  `rmbo`; crate collection excludes specialists. Preserve those exclusions and
  verify no generic idle-unit notification or new module begins owning engineers.
  Engineer repair traits exist for player orders but no ordinary bot repair
  manager was found issuing engineer repair orders at the base.
- **Production/economy:** bot unit-builder weights can create engineers and share
  infantry queues/cash, but CNC-50 does not request/cancel production or change
  demand. Record queue/cash as match context only.
- **Engine activities and world state:** `CaptureActor`, target
  `CaptureManager`, `Mobile`/`DomainIndex`, death/removal, ownership and player
  relationship changes decide validity/reachability. Their observable state is
  authoritative; do not retain a manager cache that contradicts it.
- **Idle-unit/assignment ownership:** an engineer is capture-available only when
  the capture manager has a coherent valid assignment or no other explicit module
  owns it. A transport reservation wins while live. A demolition claimant owns
  its target, not the engineer. Player/manual orders are not to be overridden in
  human play; the acceptance games use ordinary AI-owned engineers.

## Cross-worker dependencies

- The common base already contains CNC-39 and CNC-39A. Inspect the named merge/task
  commits above before the first change and again before publication; preserve
  their value, reachability, pair reassessment, capture/demolition reservation,
  relationship, and save/load invariants rather than reimplementing them.
- CNC-39A was recorded as first iteration, but its reviewed reservation/save code
  is present in this base. If a required correction exists only on
  `agent/round-20260806-cnc39a-rc2-repair` (`4c140dc37a` head), route it to the
  coordinator as an explicit dependency; do not silently cherry-pick or broaden
  CNC-50. The inspected common base already contains the relevant shared behavior.
- CNC-59 is later pending work. Do not add neutral-building production demand,
  specialist transport delivery, or a new neutral-target strategy under CNC-50.
- Same-round CNC-45/CNC-46/CNC-47 have no task-packet-declared prerequisite and
  no inspected base evidence ties them to capture lifecycle code. If their PRs
  later touch `CaptureManagerBotModule`, `CaptureTargeting`, transport reservation
  seams, `ai.yaml`, or shared tests, notify the coordinator and rebase/compare only
  those commits before publication; do not read their worker specs.
- Evidence dependency: the packet-named manual log directory is missing. Ask the
  coordinator to route it if recoverable, but do not block base-control fixture
  reproduction or create a fake replacement. Completion must explicitly report
  whether original manual artifacts were obtained.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/analysis/worker-4-cnc50/spec-policy/inputs/NARRATIVE.md`
- Sol-high policy review: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/analysis/worker-4-cnc50/spec-policy/POLICY-REVIEW.md`
- Verdict and confidence: `Mostly sensible; medium confidence. The reported raw
  evidence and a completed base comparison were unavailable, so alternative
  explanations such as reachability, pair, deferral, relationship, reservation,
  transport ownership, or path failure must be falsified.`
- Recommendations adopted as testable hypotheses: `Use one coherent validity
  state and an explicit 125-tick recovery bound; hard invalidations release by
  the next bot tick, while only a live/eligible/reachable/matching-claim incumbent
  gets the narrow existing 10-tick order grace. Require behavioral base/changed
  comparisons, completed capture, valid-incumbent/no-eligible/transport negative
  controls, all-personality coverage, deterministic cardinality, save/load, and
  MAX CPU/log-volume measurement.`
- Recommendations rejected or deferred, with reason: `None rejected. Natural
  late-game occurrence is deferred until after deterministic causal fixtures
  because rarity cannot establish the lifecycle fix, but a natural full match is
  still mandatory regression evidence. No recommendation may alter frozen policy
  values or replace game evidence.`
- Persistent scratchpad update: `Validated regular UTF-8 replacement (1,262
  characters) atomically promoted under the shared one-slot lock to
  /root/github/LibertyDawn/.agents/references/LIBERTY-DAWN-POLICY-SCRATCHPAD.md.`

## Acceptance and tests

### Literal black-box acceptance

Run a fresh full-engine, ordinary-AI, headless-MAX game on a compact task-local
map with all normal CNC bot modules enabled. Give the selected bot one `e6` and a
valid reachable target A so the normal capture manager first claims and orders it.
Then destroy/remove A or eliminate/change its owner while leaving a distinct,
reachable, normally solo-eligible damaged building (at or below the unchanged 80%
threshold) or recoverable vehicle husk B nearby. The setup may script the state
transition, but it must not replace the ordinary bot/capture module.

Acceptance requires manifest/log/replay evidence of the intended map, bot type,
faction, seed, starts/options, engineer and target actor IDs, initial assignment,
invalidation tick/reason, claim release, replacement ranking/order tick, advancing
ticks, and final outcome. The stale claim releases by the next bot tick; after any
allowed 10-tick activity grace the engineer receives useful ordinary capture work
within 125 ticks, reaches B, and B actually changes to the bot's ownership or its
recovered vehicle appears under that ownership. The engineer must not merely move
or receive a request. Repeat this literal behavior for every configured bot type;
no personality may remain stopped until death.

### Focused checks and instrumentation

- Before changing product code, preserve a base-SHA control reproduction under the
  task analysis directory. If the historical log appears, checksum/cite it but do
  not add raw logs to Git. Record whether control idleness is stale or correctly
  explained by eligibility/ownership.
- Inspect the exact current branch diff before every cycle. Run the existing
  `CaptureTargetingTest` suite and add focused deterministic tests only for a
  separable lifecycle/reservation invariant exposed by the fix: exact matching
  release, hard invalidation, bounded missing-activity grace, stable valid
  incumbent, deterministic multiple claims, and restoration. Tests must fail for
  the hypothesized base defect; do not create implementation-shaped tautologies.
- Run CNC rules/lint and the narrow shared-engine build/test surface needed by the
  changed C# files. Do not build/package unrelated games; shared compilation that
  necessarily covers them is allowed by repository guidance.
- Retained debug output, behind the existing `DebugLogging` gate or an equally
  bounded owner, must distinguish: tick; engineer type/ID; idle/current and
  expected activity; assignment target/type/ID; target live/owner/relationship/
  eligibility/reachability/health; last progress; reservation target/purpose/
  claimant IDs; transport/other owner; state transition; candidate score; first
  decisive exclusion (`capture-ineligible`, `unreachable-approach`, deferred,
  pair-required, capture/demolition/transport owner, relationship, or no
  candidates); queued order; and final capture/recovery outcome.
- Emit diagnostics on state transition or at most once per engineer per capture
  planning scan, not every tick. Remove noisy temporary traces and raw candidate
  dumps before publication; keep only concise actionable gated diagnostics.
- For every game, save a manifest proving content/config checksum, commit/toggle,
  map, bots, factions, seed, starts, options, scripted fixture events, headless MAX,
  tick range, result, log/replay/save paths, and assertions. An invalid setup is
  labeled invalid, never silently counted as a pass.
- Hot-path gate: no new whole-world scan or LINQ/allocation chain per tick. A
  per-tick audit may be at most linear in the small active-specialist collection;
  global candidate work stays bounded by the existing planning cadence/top-15.
  Across at least three matched debug-off MAX runs of 30,000+ ticks on the same
  fixture/machine, median ticks/second must not regress by more than 5% versus the
  base control and no growing queue/cache/allocation or repeated-log pattern may
  appear. Investigate a larger/noisy result; do not tune balance to compensate.

### Ordinary and differential games

Use both game slots for matched control/changed pairs when practical. The first
behavioral test after cycle 1's first product change is the literal target-death
fixture above, run once at base SHA and once with the change using identical map,
content, factions, bot/opponent, seed, starts, options, actor placements, event
ticks, speed, and duration. A same-build feature-disabled control is preferred if
it truly restores the old lifecycle; otherwise build the recorded base in an
isolated worktree. Both are full-engine ordinary-AI games from test 1.

Difficulty ladder (each step records failure hypothesis, perturbation, exact
failure signal, and player-visible pass evidence before launch):

1. **Cheese causal smoke:** one engineer, assigned target A destroyed, one obvious
   solo-eligible local husk B. Base must expose the stale window used for the
   comparison; changed must own/recover B within the bounds. Run once only, then
   increase difficulty.
2. **Owner/relationship churn:** invalidate A by owner elimination, capture, and
   relationship change in separate matched cases while B is a damaged building.
   Prove exact old claim release and completed B capture without unrelated claims.
3. **Activity and healthy negative controls:** transiently remove/cancel activity
   while A remains live/eligible/reachable/matching. Prove no order thrash and no
   worse completion; then give no eligible candidates and prove stable explained
   idleness rather than invalid movement/log spam.
4. **All-personality matrix:** run the causal fixture at least once changed and
   once base for each of the ten configured bots. Vary seed/start/target type
   across the matrix, but keep each pair matched. Every changed bot must complete
   the outcome; record recovery latency, idle ticks, claimants, and completion.
5. **Contention and pair semantics:** two or more engineers, simultaneous husks
   and damaged/healthy buildings, then loss of one pair member/target-health state
   change and an `rmbo` demolition claimant. Prove deterministic existing ranking,
   one solo/two pair cardinality, no capture-demolition overlap, and useful work by
   surviving engineers.
6. **Transport and topology:** with ordinary VIKI and IronReaper transport modules
   active, reserve an engineer for a real APC/helicopter mission while a valuable
   target exists. Test a connected map and an island/blocked topology such as
   Archipelago. Capture must not steal the passenger; after legitimate transport
   release a reachable target is acquired, while an island-unreachable target is
   rejected without spinning.
7. **Persistence/determinism:** save/load once during a healthy assignment and once
   after invalidation/before recovery. Verify restored/dropped assignments,
   reservations, grace/progress, scan deadline, and final capture; then replay a
   fresh accepted game to its recorded outcome/checksum with no desync. Reload is
   never the sole acceptance.
8. **Natural late game/endurance:** run at least one debug-off headless-MAX real
   match from ordinary starts to a natural conclusion with normal production,
   economy, squads, combat, capture, demolition, and transport active. Ensure an
   engineer recovery is deliberately reachable (starting engineer/targets is
   allowed), but do not script the winner. Judge match outcome, useful captures,
   engineer survival/idle time, order churn, and simulation throughput.

After every materially judged match or paired batch, stage only its authorized
artifacts for a fresh Commenter, then send that factual narrative through the
serialized routine Policy Reviewer loop before choosing the next code change.

### Old-behavior control and required improvement

- Golden control is exact SHA `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`
  on the recorded common-base branch, or a verified same-build feature-disabled
  mode. Record its checksum and never compare against a different personality,
  content, seed, placement, or target transition.
- Primary metrics per engineer/fixture: invalidation tick; assignment/claim
  release latency; time without valid activity; replacement eligibility tick;
  replacement order latency; first movement/progress tick; capture/recovery
  completion tick; final target owner; engineer survival; number of Stop/capture/
  retarget orders; wrong/duplicate claimant count; transport-owner violations;
  and repeated diagnostic count.
- Strategic/regression metrics: completed useful captures and recovered economic
  value under the unchanged valuation, engineer idle-life fraction, pair/commando
  claim correctness, match outcome/army-economic value where relevant, and MAX
  ticks/second/allocations or credible resource signal.
- On a deterministic causal case that exercises the defect, changed behavior must
  complete the preferred replacement capture in every clean repetition and within
  125 ticks of recovery eligibility (plus only the allowed grace), while base
  remains stale beyond that bound or otherwise has materially worse latency/
  completion. Use at least three matched causal pairs across target death and owner
  change. If base intermittently recovers, require at least 50% lower median
  replacement-order latency and 100% changed completion with no safety loss.
- A tie, marginal gain, loss, or base already meeting the contract is evidence
  against the proposed fix. Investigate fixture validity, alternate exclusions,
  and code; do not call activation logs improvement. If the exact base passes the
  whole ladder, publish a bounded no-code/diagnostic finding rather than inventing
  behavior, and route the missing manual-evidence provenance as unresolved.

### Adversarial cases

After the latest relevant product fix and after ordinary acceptance first passes,
obtain at least three distinct clean full-engine ordinary-AI adversarial scenarios.
Any affected fix restarts the three-clean count.

1. **Death/owner churn under pressure:** late-game unit count and enemy fire;
   target A dies or its owner is eliminated exactly as the engineer approaches;
   two new local candidates appear. Failure is a stale claim, idle >125 ticks,
   wrong normal ranking, or engineer death without a useful order. Pass is exact
   release, deterministic preferred B, and completed capture/recovery.
2. **False-positive recovery:** A remains valid/progressing but its activity has a
   brief queue gap; simultaneously a higher-value alternative appears. Failure is
   premature release, Stop/order oscillation, or bypassed 25% retarget rule. Pass
   is stable incumbent/progress or one normal-rule retarget with no duplicate claim.
3. **Reservation contention:** two engineers, healthy pair target, solo husk,
   commando/demolition target, and real transport ownership overlap in timing.
   Kill one specialist/target and later release the transport. Failure is a leaked
   claim, third claimant, purpose overlap, passenger theft, or surviving idle
   engineer. Pass is exact ownership and useful reassignment after release.
4. **Hostile geometry/no eligible target:** an attractive visible target is
   unreachable across blocked/island topology; other candidates are pair-only,
   deferred, relationship-invalid, or reserved. Failure is movement toward an
   invalid target or diagnostic/retry spam. Pass is explained stable idleness,
   followed by bounded completed capture when one reachable eligible husk appears.
5. **Save/load and simultaneous invalidation:** cross a save boundary with one
   healthy and one soon-stale assignment, then remove a target and expose two equal
   husks after load. Failure is lost/duplicate claims, reset indefinite grace,
   nondeterministic tie, stale-save-only success, or replay desync. Pass is the same
   deterministic ownership and final captures in reload and a subsequent fresh run.

### Final regression

After three clean adversarial scenarios, rerun the literal acceptance from a fresh
process with all normal modules, but add late-game enemy pressure, two engineers,
simultaneous normally ranked husk/building candidates, and target-owner
elimination at approach time. Use a bot personality/seed not used by the first
smoke. The exact stale claims must release, each available engineer must respect
pair/solo/demolition/transport ownership, the normal preferred eligible targets
must be captured/recovered within the bounds, the game must continue to a natural
conclusion at headless MAX, and its replay must complete without desync. A save may
accelerate diagnosis but the final regression must start fresh and may not rely on
reload. Record final actor ownership, survival, idle/order counts, claimant audit,
diagnostic cleanup, and matched throughput.

## Implementation and publication plan

1. Reproduce and classify the base behavior before coding; recover the historical
   evidence if routed, but use the causal full-engine control as authority.
2. Add only bounded diagnostics needed to identify assignment, claim, activity,
   owner, rejection, queued order, and final outcome. Use them to select the
   smallest lifecycle gap; remove temporary noise.
3. Implement the invariant in the shared capture/reservation owner or a small
   cohesive helper. Reuse normal ranking and external reservation seams; change
   rules/config only if ownership of a non-balance policy truly belongs there, and
   do not change any frozen value.
4. From cycle 1 run the matched full-engine pair, then climb the adversarial
   matrix. Add focused tests, save/load/replay, all-bot, CPU/determinism, and final
   literal evidence as specified. Stop at 20 isolated product-change cycles.
5. Keep the cycle journal/state current; run Terra reviews after cycles 5/10/15/20
   that occur; write the report with exact controls/seeds/artifact paths and
   missing-manual-evidence disposition; push the task branch, open one PR against
   the intended base, await checks, and do not merge or push `bleed`.

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
`/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/analysis/worker-4-cnc50/cycle-review-05/CYCLE-REVIEW.md`.

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
