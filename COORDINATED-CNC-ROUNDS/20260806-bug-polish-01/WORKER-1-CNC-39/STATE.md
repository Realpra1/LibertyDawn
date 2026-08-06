# Worker State: CNC-39

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `worker-1-cnc-39`
- Task: `CNC-39 — Engineer correction`
- Status: `Specified`
- Common base branch/SHA: `agent/cnc38-early-viki-infantry-rush` / `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
- Task branch: `agent/round-20260806-cnc39-engineer-correction`
- Intended PR base: `agent/cnc38-early-viki-infantry-rush`
- Worktree: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/workers/worker-1-cnc-39`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `0`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260806-bug-polish-01/WORKER-1-CNC-39/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-1-cnc-39`
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

The CNC Engineer's actual `Captures@SABOTAGE` rule captures an enemy building at
exactly 80 percent health or below and sabotages it only when its exact health
ratio is above 80 percent. The ordinary AI instead configures
`SoloBuildingCaptureHealth: 50`, so it withholds lone Engineers from valid
51-through-80-percent captures and may consume or reserve a second 250-credit,
unarmed specialist for no benefit. The capture manager also unconditionally
retains any active two-Engineer assignment while its target remains above the
solo threshold. That bypasses the normal 125-tick/five-second value-distance
comparison and can leave a pair walking a stale long route after a materially
better target, valuable vehicle husk, changed route, or changed target state
appears.

After this task, one ordinary CNC AI Engineer will opportunistically complete a
reachable enemy-building capture whenever the exact health ratio is at or below
80 percent; a target above 80 percent will still require a viable coordinated
pair. Lone and paired assignments will be reconsidered at the configured
five-second cadence without defeating the existing 25-percent anti-churn margin.
Valuable husks will remain competitive through transformed-actor value, healthy
building pairs will still complete sabotage-then-capture, and invalid, broken, or
materially inferior pair commitments will no longer persist solely because the
incumbent building remains healthy. The predicted player-visible result is more
completed captures and restorations with less duplicated Engineer travel, not
merely more selection or retarget logs.

## Authoritative behavior

- Preserve the literal request: "A lone engineer may capture an enemy building
  below 80% health (rather than the old 50% threshold). Revalidate five-second/
  value-distance reassessment, valuable husks, coordinated healthy-building
  captures, and pairing without stale long routes."
- Align AI eligibility to the actual gameplay invariant used by
  `CaptureActor`: exact health `<= 80%` is solo-capturable and exact health
  `> 80%` requires two Engineers. The task's below-80 example is the primary
  proof, but exactly 80 percent must work and any fractional amount above 80
  percent must not be rounded down into a suicidal solo order.
- Keep the policy value in the owning CNC AI rules. Keep exact ratio comparison,
  deterministic planning, reservation, and pair-state invariants in shared code.
  Do not alter the player's 80-percent sabotage/capture rule.
- Continue evaluating capture opportunities every 125 simulation ticks after
  the randomized initial delay. Preserve `CaptureDistanceBias: 10`, the strict
  greater-than-25-percent retarget margin, the 15-candidate bound, incumbent
  inclusion, deterministic tie-breaking, and enemy/neutral relationship policy
  unless direct game evidence shows one must change for this task.
- Treat an active healthy-building pair as one commitment for reassessment. Its
  completion estimate must not be better than the slower/worse-positioned viable
  member. Retain it through marginal alternatives; retarget or dissolve it when
  its premise is invalid or a feasible replacement/allocation is materially
  better after hysteresis. Compare the pair's completed value against sensible
  distinct uses of both Engineers, not only one member's best target.
- If an above-80-percent target becomes solo-capturable, release the surplus
  Engineer deterministically without allowing both specialists to reacquire the
  same solo target. If a pair member dies, leaves the world, becomes unreachable,
  is captured, or is reserved by transport, the survivor may continue only when
  the target is now solo-eligible and still wins normal selection; it must not be
  knowingly spent on sabotage alone.
- Preserve transformed-value scoring for husks and require the final restored
  vehicle actor as proof. Preserve ordinary healthy-building completion: the
  first Engineer may sabotage an above-80-percent building and the viable second
  Engineer must then capture it.
- Transport ownership wins while active. A VIKI or Iron Reaper infantry-assault
  reservation may take Engineers; capture planning must yield without replacing
  `EnterTransport`, travel, or unload orders and must recover cleanly after the
  reservation ends.
- Keep deterministic target ownership so separate solo Engineers do not converge
  on one target and pairs do not claim targets already reserved by another
  capture assignment.
- Do not redesign Engineer/Commando shared building reservations, demolition
  targeting, or queued-C4 relationship revalidation. Those are CNC-39A scope.

## Forbidden behavior and failure signals

- A lone Engineer is ordered into any building whose exact health ratio is above
  80 percent, including 80 percent plus one HP, and is consumed for sabotage
  without a viable second Engineer.
- Exactly 80 percent is treated as requiring a pair, or a 51-through-80-percent
  building consumes, reserves, or strands two Engineers when one can capture it.
- A shared C# default is changed to 80 and silently changes RA, D2K, or TS policy;
  this task changes and validates CNC only.
- The engine `Captures@SABOTAGE` threshold is changed instead of correcting AI
  policy and planning.
- An active pair is exempted from the five-second comparison, uses only the
  nearer member's distance, or remains on an invalid/blocked/stale long route
  despite a materially superior feasible allocation.
- A pair dissolves and reforms every 125 ticks for a change at or below the
  25-percent margin, ping-pongs between equal targets, or loses deterministic
  ordering.
- Pair reassessment treats the two members independently and temporarily sends
  either one alone toward an above-80-percent target.
- Two Engineers or two separate managers acquire the same solo target; a broken
  pair's survivor is left stopped indefinitely while a valid target exists.
- A valuable MCV, Mammoth, Harvester, or other recoverable husk loses its
  transformed economic value, is starved by a clearly inferior distant target,
  or is counted as success without the restored actor entering the world.
- Capture orders overwrite a VIKI/Iron Reaper transport reservation, transport
  orders overwrite an already executing capture without an explicit ownership
  transition, or a released transport passenger never returns to capture
  selection.
- Pair planning performs unbounded per-tick world/path scans, allocates without a
  bound in the simulation hot path, or depends on nondeterministic collection
  order.
- Target-selection, reservation, movement, first sabotage, debug output, or
  surviving until timeout is reported as acceptance without final ownership
  change/restored actor.
- CNC-39A behavior is implemented incidentally, especially shared commando/
  Engineer target reservations or queued-C4 safety.

## Relevant current implementation and control behavior

All facts below are from the recorded base SHA.

- `mods/cnc/rules/infantry.yaml` defines E6 as a 250-credit unarmed Engineer.
  `Captures@SABOTAGE` has `SabotageThreshold: 80`; `CaptureActor.DoCapture`
  compares `100 * HP > threshold * MaxHP`, so exactly 80 percent captures and
  anything above it sabotages. The Engineer is consumed by either outcome.
- `mods/cnc/rules/ai.yaml` enables one shared `CaptureManagerBotModule` for Cabal,
  Watson, HAL 9001, Brutalis, VIKI, SkyNet, IronReaper, WaveMaker, Easy, and
  Easiest. It scans every 125 ticks, considers at most 15 targets, uses distance
  bias 10, requires strictly more than 25-percent improvement to retarget, sets
  `SoloBuildingCaptureHealth: 50`, considers enemy and neutral actors, and has
  visibility checking disabled.
- `CaptureManagerBotModule.CaptureCandidate` currently stores
  `HealthPercent = floor(100 * HP / MaxHP)`. Merely changing the YAML value to 80
  would misclassify 80.01-through-80.99-percent targets as 80 and send a lone
  Engineer to sabotage. The AI comparison must match the exact engine boundary,
  using overflow-safe arithmetic.
- Candidate value is `max(target sell value, transformed actor custom sell value
  or cost)`, so a zero-sell-value husk inherits the recovered vehicle's value.
  Score is `economicValue * 10 / (10 + straight-line distanceCells)`. Selection
  breaks equal scores in favor of buildings, then shorter distance, while global
  candidate order and Engineer order ultimately use ActorID for determinism.
- An incumbent solo target is appended even if it falls outside the current top
  15. An active solo Engineer stays on it unless another available target beats
  its current score by strictly more than 25 percent. A per-scan target-index set
  prevents ordinary duplicate solo assignments.
- Before ordinary solo planning, active assignments are grouped by target. Any
  group of two or more whose target still requires a pair is unconditionally
  marked reserved and both actors are removed from further planning. No score,
  distance, alternate allocation, route, or hysteresis comparison occurs. New
  pairs are formed only from idle unassigned Engineers. Their current score is
  the worse/minimum of the two member scores, but the opportunity-cost check is
  only the maximum of either member's best solo score, not the best distinct use
  of both.
- When a pair target falls to the configured solo threshold, the unconditional
  retention path stops applying. ActorID order normally lets one member retain
  the target and makes the other retarget or stop. Partner loss similarly drops
  the survivor into solo planning, but no explicit pair lifecycle or completion
  feasibility is represented.
- Distances are center-to-center Euclidean distances, not route length or ETA.
  `CanCapture` checks capture traits and relationships, not ground reachability.
  Active assignment dictionaries are not game-save trait data. Blocked/island
  geometry and save/load can therefore expose repeated invalid selection,
  forgotten reservations, or duplicate work and require direct simulation.
- Assignment retirement distinguishes target removed, captured, sabotaged,
  specialist lost, and idle. Existing optional logging records solo assignment,
  retarget, pair creation, transport release, and retirement, but does not explain
  pair retention/rejection, exact fractional boundary, alternate allocation,
  reservation owner, or route feasibility.
- `TransportManagerBotModule` reserves carriers and passengers through both
  `IBotTransportReservations` and `IBotUnitReservations`. VIKI and Iron Reaper
  have a 50-percent chance per game to select the infantry-assault strategy; it
  prefers two nearby idle Engineers when available and issues transport-entry
  orders. Capture planning explicitly excludes and releases actors covered by a
  transport reservation.
- Every ordinary SquadManager config excludes `e6`; CrateCollector also excludes
  `e6`. Those managers should not order Engineers. UnitBuilder does produce E6
  through the shared GDI/Nod infantry queues, consuming the same cash and queue
  time as other units. Enemy `BuildingRepairBotModule`, combat squads, target
  capture/destruction, and target-owner changes can move or invalidate the
  threshold while Engineers travel.
- The specialist capture/reassessment implementation and focused tests entered
  history in `5fce15d081` (integrated AI features). Transport reservation yielding
  was added in `a9c9c0f87f`. No later base commit changes these control semantics.
  The intended PR-base branch's post-base commits at specification time modify
  coordination artifacts/role launcher only, not product capture behavior.

## Likely wrong approaches and challenges

- Changing only `SoloBuildingCaptureHealth: 50` to 80 appears to satisfy the
  headline but leaves unconditional pair retention and the fractional-health
  floor bug intact.
- Interpreting the prose as strict `< 80` conflicts with the actual engine and
  reviewed policy. Tests must include 79, exact 80, 80 percent plus one HP, and
  81, not only integer 79/81 endpoints.
- Reusing the lone scoring loop independently for each pair member can create a
  transient solo suicide, duplicate reservation, or churn. Pair planning needs a
  coherent decision/commit boundary even if the implementation remains compact.
- Scoring a pair from the nearer member, the average member, or straight-line
  distance alone can hide a blocked or distant straggler. A credible completion
  estimate is constrained by the worse viable member. Conversely, running full
  pathfinding for every Engineer/candidate combination every tick is an
  unacceptable hot-path fix; preserve the 125-tick and 15-target bounds and use
  cached/bounded feasibility evidence.
- Comparing a healthy pair only with its best alternative healthy building or
  one best solo target misses that two distinct solo captures/restorations may be
  better. Do not double-count one reserved target as two alternatives.
- Making all strategic building roles outrank husks is not requested and can
  create irrational cross-map commitments. Preserve economic/transformed value
  first; treat any strategic-value enhancement as deferred unless adversarial
  evidence makes it necessary and bounded.
- Adding a new minimum-commitment timer on top of the existing 25-percent margin
  may hide genuine five-second responsiveness. Start with the authored margin;
  add state only if a demonstrated churn case cannot be solved through coherent
  pair comparison.
- A broad refactor of the 475-line capture/demolition module risks colliding with
  CNC-39A. If pair planning makes `QueueCaptureOrders` less cohesive, extract a
  small pure capture-pair planner/helper under `BotModuleLogic`; do not redesign
  demolition or shared specialist reservations in this task.
- A focused map with scripted actors is useful, but replacing the real bot or
  disabling its ordinary modules makes the evidence invalid. Map triggers may
  create damage, repair, actors, and timing; the decisions must still come from an
  ordinary real CNC AI in the full engine.
- Save reload can retain actor activities while the non-persisted module forgets
  assignments. A reloaded success is useful diagnosis but never sole acceptance.

## Competing systems and ownership

- **Policy/config owner:** the shared CNC `CaptureManagerBotModule` block in
  `mods/cnc/rules/ai.yaml` owns the 80-percent solo policy, scan interval,
  distance bias, target cap, hysteresis, relationships, and diagnostic default.
  Do not duplicate the threshold in ten personality blocks.
- **Algorithm owner:** `CaptureManagerBotModule` owns candidate discovery,
  active assignments, target reservation, order issuance, transport yielding,
  completion classification, and pair lifecycle. `CaptureTargeting` is the
  existing pure-policy seam for exact threshold, scores, retarget margin,
  deterministic selection, and any focused pair-allocation helper.
- **Gameplay owner:** `Captures`, `CaptureManager`, and `CaptureActor` validate
  relationships at approach/entry, serialize capture progress, apply sabotage or
  ownership change, and consume E6. They establish the exact 80-percent invariant
  but should not receive task-specific AI policy.
- **Engineer producer/resource consumer:** each enabled `UnitBuilderBotModule`
  can train E6 on `Infantry.GDI`/`Infantry.Nod`, competing for cash, barracks/Hand
  queue time, unit cap, and other infantry demand. At least one integrated game
  must produce rather than only pre-spawn an Engineer.
- **Transport owner/order competitor:** `TransportManagerBotModule` and
  `InfantryAssaultTransportManager` may reserve and issue `EnterTransport`, move,
  and unload orders for a pair of E6 on VIKI/Iron Reaper. They also request APCs
  or transport helicopters from UnitBuilder, adding queue/cash contention.
- **Ordinary non-owners:** SquadManager and CrateCollector exclude E6 in CNC and
  must continue to do so. Verify they are enabled and do not acquire the test
  Engineers. Other special-behavior reservations must not become a hidden way to
  overwrite capture orders.
- **Target state competitors:** the target owner's BuildingRepairBotModule can
  repair across 80 percent; friendly/enemy combat modules and weapons can damage
  across it; capture, sell, destruction, owner/relationship change, fog state,
  and husk transformation can remove or change a candidate. Force repair, damage,
  destruction/partner loss, and owner transition in integrated games.
- **Demolition overlap:** the same bot module independently tracks RMBO C4 orders
  and target IDs. It does not share reservations with Engineers and queued C4
  does not revalidate capture ownership; CNC-39A owns that correction. This task
  must preserve current demolition behavior and avoid claiming its target space.

## Cross-worker dependencies

- CNC-39A, **Engineer/commando target coordination**, is pending and will likely
  touch `CaptureManagerBotModule`, specialist target reservation/ownership, and
  possibly C4 execution validation. At specification time, current local/remote
  branches and open PRs contain no CNC-39/CNC-39A/engineer/commando follow-up.
- Before the first edit and again before publication, list current branches and
  open PRs for `CNC-39A`, `engineer`, `commando`, and `capture`. If a CNC-39A PR
  now exists, inspect only that PR's commits/diff (never its worker spec), record
  the head SHA in this section/report, and preserve or coordinate its shared
  reservation API rather than inventing a competing one.
- Do not claim shared Engineer/Commando reservations or queued-C4 friendly-target
  safety. If satisfying CNC-39 becomes impossible without such a change, stop
  and report the overlap instead of silently absorbing CNC-39A.
- PR #77 (`agent/cnc38-early-viki-infantry-rush`) is the intended cumulative
  base. Its product change is already contained in the recorded base history and
  reserves E2/E5, not E6; the material interaction is only that VIKI's ordinary
  modules and shared `ai.yaml` must remain intact. The task branch must start at
  the recorded base SHA even if the PR-base branch has advanced through later
  coordination-only commits; reconcile the PR base deliberately before opening
  the task PR.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-1-cnc-39/spec-policy/inputs/NARRATIVE.md`
- Sol-high policy review: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-1-cnc-39/spec-policy/POLICY-REVIEW.md`
- Verdict and confidence: `mostly sensible; high confidence`
- Recommendations adopted as testable hypotheses: `Use the exact <=80/>80 gameplay boundary; evaluate a pair from the slower/worse feasible member; compare completed pair value against distinct uses of both Engineers; keep the initial strict 25% hysteresis; dissolve broken/unavailable pairs safely; release surplus atomically at the boundary; transport ownership wins; prove final captures/restorations and compare travel, losses, churn, and useful value against control.`
- Recommendations rejected or deferred, with reason: `Do not add broad strategic-role modifiers for Construction Yards, production, technology, power, economy, or footholds in the first implementation: CNC-39 asks to preserve/revalidate current value-distance and husk value, and no game evidence yet proves sell/transformed value inadequate. Do not add a second minimum-commitment timer unless the churn trap demonstrates need; the existing 25% margin is the authored control. Do not perform unbounded exact route/ETA searches for all pairs; require bounded feasibility/completion evidence and escalate only if blocked-topology games prove a cheaper method insufficient. CNC-39A shared Engineer/Commando reservation and queued-C4 advice is explicitly deferred to CNC-39A.`

## Acceptance and tests

### Literal black-box acceptance

From a fresh full-engine CNC match at headless MAX, use an ordinary real AI with
all its normal modules enabled, exactly one available E6, and a reachable enemy
building held at 79 percent health long enough for the AI to act. The manifest,
console/game/debug logs, replay/benchmark evidence, or focused map summary must
prove the exact map checksum/title, seed, options, bot type, factions/teams,
initial target HP/MaxHP, one available Engineer, ordinary module enablement,
headless MAX markers, advancing world ticks, and final state.

Acceptance requires that the single AI Engineer reaches the building, is consumed
by a capture (not sabotage), and the building's final owner is the Engineer's AI.
No second Engineer may be produced, reserved, or ordered for that target before
the capture. An assignment or movement line is insufficient. Repeat the same
fresh scenario at exactly 80 percent to prove the real boundary, and separately
prove that a target one HP above 80 percent never receives a lone capture order.

### Focused checks and instrumentation

- Before changing code, run and record the focused current-control test plus CNC
  YAML validation. After each relevant change, use
  `dotnet test OpenRA.Test/OpenRA.Test.csproj --filter FullyQualifiedName~CaptureTargetingTest`
  to challenge exact ratio boundaries, transformed husk value, target reservation,
  strict 25-percent hysteresis, slower-member pair utility, distinct alternatives,
  invalid/broken pair transitions, and deterministic ties. Passive unit tests are
  supplementary and must not delay cycle-1 game feedback.
- Add regression cases for 79%, exactly 80%, `80% + 1 HP`, 81%, zero/invalid
  MaxHP handling, non-building husks at any health, 125%/126% retarget scores,
  equal scores, one reserved alternative, and two distinct solo alternatives
  versus one pair. Use integer HP/MaxHP inputs or an equally exact representation;
  never test only already-rounded percent integers.
- Final code gates are the focused test, full
  `dotnet test OpenRA.Test/OpenRA.Test.csproj`, `make all`, and `make test` (which
  includes CNC MiniYAML). Run only CNC rules/content validation; shared engine
  compilation is allowed, but do not build/test/package RA, D2K, or TS content.
  Wrap the full build/test gate with one `large-build` slot in the recorded lock
  directory.
- Use the existing `DebugLogging` seam, enabled only in focused test rules, or a
  similarly bounded diagnostic. At most once per 125-tick decision/transition,
  make evidence distinguish: Engineer and current mission; exact HP/MaxHP and
  solo/pair classification; candidate rejection reason; direct/transformed value;
  distance/feasibility and score; incumbent/replacement and required margin; both
  pair member IDs and worse-member completion score; distinct solo alternatives;
  target reservation owner; transport/other consumer; pair retain/dissolve/
  surplus release; queued order; and retirement as captured, restored, sabotaged,
  lost, invalid, or idle.
- Final-state evidence must independently inspect actor ownership or restored
  actor identity; do not infer it from manager logs. Treat a missing target,
  missing actor, impossible ratio, invalid reservation owner, or failed route as a
  handled rejection with actionable bounded output, not success or a swallowed
  exception. Remove temporary per-candidate dumps and any per-tick logging before
  publication; leave default production diagnostics false.
- Keep capture planning on the existing 125-tick cadence, respect the 15-target
  cap, and preserve deterministic ActorID ordering. Record allocations/scan and
  headless benchmark throughput for a high-Engineer stress game. There must be no
  new per-tick full-world scan, unbounded retry queue, or sustained material MAX
  slowdown. Treat a repeatable slowdown greater than approximately 5 percent
  against the matched base run as a regression to investigate, not an automatic
  waiver; report unavoidable measurement noise and allocations honestly.

### Ordinary and differential games

Use `launch-ai-parallel.py` manifests under the analysis directory, unique output
directories, and the shared game-slot wrapper. A two-game matched batch must hold
both slots:

```text
python3 .agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py \
  --lock-dir /root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks \
  --resource game --capacity 2 --slots 2 -- \
  python3 launch-ai-parallel.py \
  --manifest /root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-1-cnc-39/manifests/current-vs-control.json \
  --output /root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-1-cnc-39/games/current-vs-control-01 \
  --jobs 2
```

Every run must use ordinary real bot types and all relevant normal modules. A
focused `.oramap` may pre-spawn E6/targets and trigger damage, repair, partner
loss, or new opportunities, but it may not substitute a passive/custom bot or
manager-only fixture. Record the failure hypothesis, perturbation, exact failure
signal, and player-visible pass evidence in the cycle journal before each run.

Difficulty ladder and primary feedback:

1. **Cycle-1 matched threshold smoke (first behavioral test after the first
   product change):** run the changed build and a same-build focused-map control
   overriding the solo threshold to 50, with one Cabal/Watson E6 one cell from a
   79-percent enemy building. Failure hypothesis: config changed but exact AI
   eligibility/order completion did not. Control failure/pass distinction: old
   policy must not complete with one Engineer; changed policy must produce final
   ownership with one Engineer and no extra loss. Use both game slots.
2. **Boundary and state-crossing game:** increase distance/pressure and sequence
   exact 80, 80%+1HP, and 81 targets; damage an 81-percent pair target to exact 80
   and repair an exact-80 solo target above 80 before arrival. Failure hypothesis:
   rounding, stale health, or non-atomic surplus release causes sabotage, duplicate
   orders, or wasted pairs. Pass evidence is completed legal captures, no illegal
   lone entry, and the surplus/solo state transitions by the next 125-tick review.
3. **Matched stale-pair allocation:** on the changed build versus exact base SHA,
   start two Engineers on a distant >80-percent target, then expose two distinct
   reachable solo opportunities (one valuable husk) whose conservative combined
   utility materially exceeds the incumbent after the 25-percent margin. Failure
   hypothesis: pairs remain categorically retained or compare only one
   alternative. Changed must redirect/dissolve within one review, complete more
   useful capture/restoration value, and reduce wasted Engineer-distance/ticks;
   base should exhibit the stale commitment.
4. **Healthy-pair completion and churn:** offer one strategically/economically
   worthwhile >80-percent reachable building with two Engineers, then oscillate
   a competitor just below/above marginal score parity for several reviews.
   Failure hypothesis: reassessment breaks valid pairs or ping-pongs. Pass requires
   stable commitment below/equal to the 25-percent margin and final sabotage then
   ownership change; a later decisive opportunity must still cause one prompt,
   stable switch.
5. **Broken pair and competing owner:** kill or remove one member, then in a
   separate phase reserve one member for a forced VIKI/Iron Reaper transport
   assault (`AssaultSelectionPercent: 100` only in focused rules). Failure
   hypothesis: the survivor suicides, stays stranded, or capture overwrites
   transport. Pass requires safe dissolve/retarget, transport order ownership
   while reserved, clean mission release, and a later completed capture or husk
   restoration by the released Engineer.
6. **Connected versus blocked/island topology:** first prove a long but connected
   route, then use Archipelago or a focused island/blocked map with ordinary AI
   and active transport behavior. Failure hypothesis: Euclidean scoring hides an
   infeasible straggler and a pair remains forever or churns on the same target.
   Pass requires bounded rejection/recovery or valid transport completion, no
   repeated hopeless pair order, and a final useful reachable outcome.
7. **Save/load continuity:** save during an active pair route and reload once.
   Failure hypothesis: non-persisted assignment state duplicates the target or
   strands a member. Pass requires deterministic recovery, no duplicate solo
   ownership, and final legal capture/restoration. Then repeat the strongest
   affected scenario fresh; the reload never counts as sole acceptance or final
   regression.
8. **Ordinary production/endurance:** run a real full match at headless MAX to
   natural game over on Empire Earth (and an Archipelago full match if the focused
   blocked case did not exercise normal progression), including VIKI or Iron
   Reaper plus at least one non-transport-specialist AI. Force/verify E6 production
   from the normal infantry queue, target repair/damage, capture and husk events,
   squad/crate exclusion, and transport contention. Compare outcome, economy/army
   value, useful captures/restorations, E6 losses/idle/travel, pair churn, and MAX
   throughput. A natural win where the feature never fires is not acceptance.

After every materially judged game/pair, stage only authorized artifacts for a
fresh Match Commenter and policy-feedback review as required below. Use the
review as the next adversarial hypothesis, not as acceptance.

### Old-behavior control and required improvement

- Immutable old control: `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
  in a separate isolated worktree/install with `SoloBuildingCaptureHealth: 50`
  and the unconditional active-pair retention code. Do not contaminate its content
  or support directory with the changed build.
- For the literal threshold pair, prefer a same-build map/rules override back to
  50 so code, content, seed, and timing are identical and only the authored policy
  differs. For stale-pair comparison, use the immutable base because no clean
  feature toggle exists. Do not add a production toggle solely to manufacture a
  test control.
- For every pair, match map bytes/checksum, seed, slots/starts, factions, teams,
  bot types, starting cash, options, initial actors/HP, trigger ticks, opponent,
  speed, exit condition, and diagnostic settings. Record both commit SHA/content
  checksum and unavoidable nondeterminism.
- Required threshold improvement is decisive: changed completes the forced
  51-through-79 and exact-80 capture with one Engineer while the 50 control does
  not; changed uses fewer Engineer-ticks/commitments and incurs no additional
  specialist loss.
- Required stale-pair improvement is material: when the forced better allocation
  appears, changed reacts within the next 125-tick review, completes greater
  capture/restoration value, and reduces wasted pair distance/time without more
  sabotage-only attempts, broken pairs, transport conflicts, or churn than base.
- Preserve control strengths: no lone above-80 suicide, deterministic distinct
  targets, and no switch at or below the 25-percent margin. In ordinary matches,
  changed must not materially worsen useful captures/restorations, Engineer loss,
  economy/army value, match outcome, or simulation cost.
- A loss, repeated parity in an exercised stale-route case, only marginal savings,
  or feature logs without final outcomes is presumptive evidence of a bad policy,
  implementation defect, or invalid harness. Investigate and correct it or record
  a concrete task-specific causal explanation; do not dismiss it as noise after a
  single run.

### Adversarial cases

At minimum, all of these must be forced and judged; after the latest relevant fix,
obtain three distinct clean full-engine adversarial scenarios before final
regression:

- **Fractional boundary:** HP/MaxHP is exactly 80%, then one HP above 80%, not
  merely rounded 80/81. Failure is any lone order or sabotage loss above the exact
  boundary; pass is exact-80 ownership and above-80 wait/pair/retarget.
- **Damage/repair crossing:** a pair target falls to solo eligibility and a solo
  target rises above it during travel. Failure is stale classification, duplicate
  reservation, or suicide; pass is coherent transition within one review and a
  completed legal outcome.
- **Two singles versus one pair:** two distinct reachable solo captures/husks
  compete with a distant healthy building. Failure is keeping the pair because
  only one alternative was counted; pass is the higher completed useful value
  without duplicate targets.
- **Straggler/route failure:** one pair member is close and one is far, blocked,
  destroyed, or on an island. Failure is indefinite wait/reissue or the close
  member hiding infeasibility; pass is bounded dissolve/replan or successful
  transport and final outcome.
- **Valuable husk mix:** a valuable MCV/Mammoth/Harvester husk competes with a
  distant structure. Failure is zero/low husk value or selection logs without
  transformation; pass is economically sensible selection and the restored actor
  visible under AI ownership. Also include a high-value production/technology
  building so husks do not become an unconditional preference.
- **Reservation collision:** capture pair selection and deterministic
  VIKI/IronReaper transport selection contend for the same idle E6 on a scan
  boundary. Failure is order overwrite, dual ownership, or stranded survivor;
  pass is one explicit reservation owner, clean handoff, and later useful action.
- **Churn trap:** alternatives alternate below/equal to and above the strict
  25-percent margin across multiple reviews. Failure is repeated pair teardown or
  failure to react to decisive improvement; pass is stability followed by one
  justified switch.
- **Save/load and duration:** reload an in-flight pair, then run a separate fresh
  long match. Failure is forgotten/duplicate assignments or a success found only
  after reload; pass is deterministic recovery plus fresh-match confirmation.

### Final regression

After the latest code/config change and three clean post-fix adversarial games,
rerun the literal acceptance from a fresh process with a new seed and the strongest
compatible stress: one ordinary real-AI Engineer, a reachable 79-percent enemy
building, meaningful enemy pressure/repair enabled but timed so at least one
five-second review occurs, a tempting husk/distant target that does not invalidate
the expected choice, and all normal modules active. Prove headless MAX, intended
map/options/bots/actors/ticks, exactly one Engineer committed to the target, final
AI ownership, no sabotage-only loss, no duplicate reservation, and bounded
diagnostics. This run must not be a save reload. Then rerun the focused/full test
gates and preserve concise artifact paths/results in the report; raw games, logs,
saves, and replays remain outside Git.

## Implementation/publication plan

1. Establish the exact-base focused tests and matched 50-percent control before
   editing. Record current rounding and unconditional-pair behavior.
2. Put the CNC policy threshold in the shared CNC AI rules. Make exact ratio and
   pair allocation invariants pure/testable in `CaptureTargeting` or a focused
   helper; keep manager responsibilities to world-state collection, reservation,
   order commit, and outcome observation.
3. Add coherent active-pair reassessment with deterministic atomic reservation
   updates, transport yielding, invalid/broken-pair recovery, and bounded
   diagnostics. Avoid demolition/CNC-39A changes and unrelated refactors.
4. From cycle 1, use matched full-engine ordinary-AI games as primary feedback,
   then climb through boundary, pair opportunity, churn, husk, transport,
   connected/blocked, save/load, and natural-match stress. Use narratives/policy
   reviews after every materially judged batch.
5. Remove noisy temporary diagnostics, run focused/full CNC gates, measure
   determinism/MAX cost, and write the task report with exact seeds, commits,
   control settings, outcomes, artifact paths, cycles, risks, and deferred work.
6. Commit and push only the task branch, open one PR against
   `agent/cnc38-early-viki-infantry-rush`, wait for all required GitHub checks,
   and do not merge. Propose `Complete - testing` only if every contract item is
   green; otherwise propose `First iteration - testing` with exact failures.

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
